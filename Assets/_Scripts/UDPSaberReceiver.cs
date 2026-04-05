using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receives IMU + optional joystick/button from Raspberry Pi Pico W over UDP (two ports for two hands).
/// Formats: "ax,ay,az,gx,gy,gz" or extended "ax,ay,az,gx,gy,gz,jx,jy,sw" (jx,jy normalized 0..1, sw 1=select pressed).
/// See PicoW_Controller_Reference.txt.
/// </summary>
public class UDPSaberReceiver : MonoBehaviour, IImuSaberReceiver
{
    [Header("UDP Ports (one per saber)")]
    [Tooltip("Port the LEFT-hand Pico W sends to. Default 5001 — set PORT = 5001 in firmware main.py.")]
    public int leftSaberPort = 5001;
    [Tooltip("Port the RIGHT-hand Pico W sends to. Default 5000 — set PORT = 5000 in firmware main.py.")]
    public int rightSaberPort = 5000;

    [Header("Data (read-only)")]
    [SerializeField] private IMUPacket leftSaberData;
    [SerializeField] private IMUPacket rightSaberData;

    private UdpClient leftClient;
    private UdpClient rightClient;
    private Thread leftThread;
    private Thread rightThread;
    private volatile bool running = true;
    private readonly object leftLock = new object();
    private readonly object rightLock = new object();
    // Parse-failure counters per hand: log once every N failures to avoid log spam.
    private int _leftParseFailures;
    private int _rightParseFailures;
    private const int ParseFailureLogInterval = 20;

    [Header("Debug CSV")]
    [Tooltip("Enable to write every received UDP packet to a CSV file for network/parse debugging. " +
             "CSV path is logged to the Console on startup. Disable when not debugging to avoid disk writes.")]
    public bool debugCsvEnabled = true;
    [Tooltip("Leave blank to use Application.persistentDataPath/udp_debug.csv")]
    public string debugCsvPath = "";

    // Background-thread-safe queue; flushed to file in Update() on the main thread.
    private readonly ConcurrentQueue<string> _csvQueue = new ConcurrentQueue<string>();
    private StreamWriter _csvWriter;
    private string _resolvedCsvPath;

    public IMUPacket LeftSaberData
    {
        get { lock (leftLock) { return leftSaberData; } }
    }
    public IMUPacket RightSaberData
    {
        get { lock (rightLock) { return rightSaberData; } }
    }

    [Serializable]
    public struct IMUPacket
    {
        public Vector3 acceleration;  // g
        public Vector3 angularVelocity; // deg/s
        public float timestamp;
        public bool valid;
        /// <summary>Normalized 0..1 from ADC (packet fields 7–8). Meaningful when <see cref="hasControllerExtras"/>.</summary>
        public float joystickX;
        public float joystickY;
        /// <summary>True when packet field 9 &gt; 0.5 (e.g. SW pressed sent as 1).</summary>
        public bool selectPressed;
        public bool hasControllerExtras;
    }

    void Start()
    {
        leftSaberData = new IMUPacket { valid = false };
        rightSaberData = new IMUPacket { valid = false };

        leftClient = TryBindPort(leftSaberPort, "left");
        if (leftClient != null)
        {
            leftThread = new Thread(() => ReceiveLoop(leftClient, leftLock, (d) => leftSaberData = d, ref _leftParseFailures, "left")) { IsBackground = true };
            leftThread.Start();
        }

        rightClient = TryBindPort(rightSaberPort, "right");
        if (rightClient != null)
        {
            rightThread = new Thread(() => ReceiveLoop(rightClient, rightLock, (d) => rightSaberData = d, ref _rightParseFailures, "right")) { IsBackground = true };
            rightThread.Start();
        }

        Debug.Log($"[UDPSaberReceiver] Listening — right hand port {rightSaberPort}, left hand port {leftSaberPort}. " +
                  $"Firmware: RIGHT Pico PORT={rightSaberPort}, LEFT Pico PORT={leftSaberPort}. " +
                  $"Packet: ax,ay,az,gx,gy,gz[,jx,jy,sw]. Calibration requires 9-field packets.");

        if (debugCsvEnabled)
            OpenCsvWriter();

        if (GetComponent<UdpMenuNavigation>() == null)
            gameObject.AddComponent<UdpMenuNavigation>();
    }

    void Update()
    {
        if (_csvWriter == null || _csvQueue.IsEmpty)
            return;

        while (_csvQueue.TryDequeue(out string line))
        {
            try { _csvWriter.WriteLine(line); }
            catch { /* disk full / permissions */ }
        }
        try { _csvWriter.Flush(); }
        catch { }
    }

    void OpenCsvWriter()
    {
        _resolvedCsvPath = string.IsNullOrWhiteSpace(debugCsvPath)
            ? Path.Combine(Application.persistentDataPath, "udp_debug.csv")
            : debugCsvPath;

        try
        {
            _csvWriter = new StreamWriter(_resolvedCsvPath, append: false);
            _csvWriter.WriteLine("time_s,hand,raw,valid,ax,ay,az,gx,gy,gz,hasExtras,jx,jy,sw");
            _csvWriter.Flush();
            Debug.Log($"[UDPSaberReceiver] Debug CSV open: {_resolvedCsvPath}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UDPSaberReceiver] Could not open debug CSV at {_resolvedCsvPath}: {e.Message}");
            _csvWriter = null;
        }
    }

    static UdpClient TryBindPort(int port, string label)
    {
        try
        {
            var client = new UdpClient();
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            client.Client.ReceiveTimeout = 100;
            return client;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UDPSaberReceiver] {label} saber port {port}: {e.Message}");
            return null;
        }
    }

    private void ReceiveLoop(UdpClient client, object lockObj, Action<IMUPacket> setData,
                             ref int failureCounter, string handLabel)
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        while (running && client != null)
        {
            try
            {
                byte[] bytes = client.Receive(ref remote);
                string raw = System.Text.Encoding.UTF8.GetString(bytes);
                var p = ParsePacket(raw);

                EnqueueCsvRow(handLabel, raw, p);

                if (p.valid)
                {
                    failureCounter = 0;
                    lock (lockObj) { setData(p); }
                }
                else
                {
                    failureCounter++;
                    if (failureCounter % ParseFailureLogInterval == 1)
                    {
                        string snippet = raw.Length > 80 ? raw.Substring(0, 80) + "…" : raw;
                        Debug.LogWarning($"[UDPSaberReceiver] Parse failed (failure #{failureCounter}). " +
                                         $"Raw bytes: \"{snippet}\" — expected ax,ay,az,gx,gy,gz[,jx,jy,sw]");
                    }
                }
            }
            catch (SocketException) { /* receive timeout — normal */ }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { Debug.LogWarning($"[UDPSaberReceiver] Receive error: {ex.Message}"); }
        }
    }

    void EnqueueCsvRow(string hand, string raw, IMUPacket p)
    {
        if (_csvWriter == null) return;

        // Called from background thread — only enqueue; main thread writes.
        string safeRaw = raw.Replace(",", ";").Replace("\n", "").Replace("\r", "");
        string line = string.Format(CultureInfo.InvariantCulture,
            "{0:F3},{1},{2},{3},{4:F4},{5:F4},{6:F4},{7:F4},{8:F4},{9:F4},{10},{11:F4},{12:F4},{13}",
            Environment.TickCount / 1000.0,
            hand, safeRaw,
            p.valid ? "1" : "0",
            p.acceleration.x, p.acceleration.y, p.acceleration.z,
            p.angularVelocity.x, p.angularVelocity.y, p.angularVelocity.z,
            p.hasControllerExtras ? "1" : "0",
            p.joystickX, p.joystickY,
            p.selectPressed ? "1" : "0");

        _csvQueue.Enqueue(line);
    }

    /// <summary>
    /// Parse "ax,ay,az,gx,gy,gz" or extended "... ,jx,jy,sw" (jx,jy in 0..1, sw 1 = pressed).
    /// </summary>
    public static IMUPacket ParsePacket(string raw)
    {
        var p = new IMUPacket { valid = false, timestamp = Time.realtimeSinceStartup };
        if (string.IsNullOrWhiteSpace(raw)) return p;

        // Strip UTF-8 BOM (\uFEFF) and any surrounding whitespace/CR that may arrive from MicroPython.
        string s = raw.TrimStart('\uFEFF').Trim();
        char[] sep = { ',', ' ', '\t' };
        string[] parts = s.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 6)
            return p;

        var inv = CultureInfo.InvariantCulture;
        if (!float.TryParse(parts[0], NumberStyles.Float, inv, out float ax) ||
            !float.TryParse(parts[1], NumberStyles.Float, inv, out float ay) ||
            !float.TryParse(parts[2], NumberStyles.Float, inv, out float az) ||
            !float.TryParse(parts[3], NumberStyles.Float, inv, out float gx) ||
            !float.TryParse(parts[4], NumberStyles.Float, inv, out float gy) ||
            !float.TryParse(parts[5], NumberStyles.Float, inv, out float gz))
            return p;

        p.acceleration = new Vector3(ax, ay, az);
        p.angularVelocity = new Vector3(gx, gy, gz);
        p.valid = true;
        p.joystickX = 0.5f;
        p.joystickY = 0.5f;
        p.selectPressed = false;
        p.hasControllerExtras = false;

        if (parts.Length >= 9 &&
            float.TryParse(parts[6], NumberStyles.Float, inv, out float jx) &&
            float.TryParse(parts[7], NumberStyles.Float, inv, out float jy) &&
            float.TryParse(parts[8], NumberStyles.Float, inv, out float sw))
        {
            p.joystickX = Mathf.Clamp01(jx);
            p.joystickY = Mathf.Clamp01(jy);
            p.selectPressed = sw > 0.5f;
            p.hasControllerExtras = true;
        }

        return p;
    }

    void OnDestroy()
    {
        running = false;
        leftClient?.Close();
        rightClient?.Close();
        leftThread?.Join(500);
        rightThread?.Join(500);

        if (_csvWriter != null)
        {
            // Flush any remaining queued rows before closing.
            while (_csvQueue.TryDequeue(out string line))
            {
                try { _csvWriter.WriteLine(line); } catch { }
            }
            try { _csvWriter.Close(); } catch { }
            _csvWriter = null;
            if (_resolvedCsvPath != null)
                Debug.Log($"[UDPSaberReceiver] Debug CSV closed: {_resolvedCsvPath}");
        }
    }
}
