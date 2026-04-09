using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receives the same CSV lines as <see cref="UDPSaberReceiver"/> over USB serial (one COM port per hand).
/// Format: ax,ay,az,gx,gy,gz[,jx,jy,sw]. Gyro must be deg/s (integrated by <see cref="SaberMotionController"/>), not Euler angles.
/// </summary>
public class SerialSaberReceiver : MonoBehaviour, IImuSaberReceiver
{
    /// <summary>System.IO.Ports is only usable on Windows in this Unity/.NET setup (Editor + standalone).</summary>
    private static readonly bool SerialRuntimeSupported =
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        true;
#else
        false;
#endif

    private static bool _loggedUnsupportedPlatform;
    /// <summary>True after <see cref="Start"/> if at least one COM port opened successfully.</summary>
    public bool HasOpenSerialPort =>
        (leftPort != null && leftPort.IsOpen) || (rightPort != null && rightPort.IsOpen);

    private static bool _serialApiUnavailable;
    private static bool _loggedSerialApiUnavailable;

    [Header("Serial ports (empty = disabled for that hand)")]
    [Tooltip("e.g. COM5 on Windows")]
    public string leftSaberComPort = "";
    public string rightSaberComPort = "";
    public int baudRate = 115200;

    [Header("Data (read-only)")]
    [SerializeField] private UDPSaberReceiver.IMUPacket leftSaberData;
    [SerializeField] private UDPSaberReceiver.IMUPacket rightSaberData;

    private SerialPort leftPort;
    private SerialPort rightPort;
    private Thread leftThread;
    private Thread rightThread;
    private readonly object leftLock = new object();
    private readonly object rightLock = new object();

    public UDPSaberReceiver.IMUPacket LeftSaberData
    {
        get { lock (leftLock) { return leftSaberData; } }
    }

    public UDPSaberReceiver.IMUPacket RightSaberData
    {
        get { lock (rightLock) { return rightSaberData; } }
    }

    void Start()
    {
        leftSaberData = new UDPSaberReceiver.IMUPacket { valid = false };
        rightSaberData = new UDPSaberReceiver.IMUPacket { valid = false };

        TryOpenPort(leftSaberComPort, ref leftPort, ref leftThread, leftLock, d => leftSaberData = d, "left");
        TryOpenPort(rightSaberComPort, ref rightPort, ref rightThread, rightLock, d => rightSaberData = d, "right");

        if (GetComponent<UdpMenuNavigation>() == null)
            gameObject.AddComponent<UdpMenuNavigation>();
    }

    void TryOpenPort(string portName, ref SerialPort port, ref Thread thread, object lockObj,
        Action<UDPSaberReceiver.IMUPacket> setPacket, string label)
    {
        if (string.IsNullOrWhiteSpace(portName))
            return;

        if (!SerialRuntimeSupported)
        {
            if (!_loggedUnsupportedPlatform)
            {
                _loggedUnsupportedPlatform = true;
                Debug.LogWarning("[SerialSaberReceiver] USB serial (COM ports) is only supported in **Windows** Editor and **Windows** standalone builds. Use UDP (Pico W) on macOS/Linux, or build/run on Windows.");
            }
            return;
        }

        if (_serialApiUnavailable)
            return;

        try
        {
            port = new SerialPort(portName.Trim(), baudRate)
            {
                NewLine = "\n",
                ReadTimeout = 50,
                WriteTimeout = 200,
                DtrEnable = true,
                RtsEnable = true
            };
            port.Open();
            try { port.DiscardInBuffer(); } catch { }
            // Local copy: lambdas cannot capture ref/out parameters (CS1628).
            SerialPort serialForRead = port;
            thread = new Thread(() => ReadLoop(serialForRead, lockObj, setPacket, label)) { IsBackground = true };
            thread.Start();
            Debug.Log($"[SerialSaberReceiver] {label} saber on {portName} @ {baudRate} (DTR+RTS enabled)");
        }
        catch (Exception e)
        {
            if (IsSerialApiUnavailableException(e))
            {
                _serialApiUnavailable = true;
                if (!_loggedSerialApiUnavailable)
                {
                    _loggedSerialApiUnavailable = true;
                    Debug.LogWarning(
                        "[SerialSaberReceiver] System.IO.Ports is not available in this Unity/player configuration " +
                        "(common with some Editor .NET profiles). Use UDP from Pico W, add a UDP receiver in the scene, " +
                        "or use a Windows standalone build. Detail: " + e.Message);
                }
            }
            else
                Debug.LogWarning($"[SerialSaberReceiver] {label} port '{portName}': {e.Message}");

            if (port != null)
            {
                try { port.Close(); } catch { /* ignore */ }
                port = null;
            }
        }
    }

    static bool IsSerialApiUnavailableException(Exception e)
    {
        if (e is NotSupportedException || e is PlatformNotSupportedException)
            return true;
        string m = e.Message ?? "";
        if (m.IndexOf("System.IO.Ports", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (m.IndexOf("only supported on Windows", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return false;
    }

    static void ReadLoop(SerialPort serial, object lockObj,
        Action<UDPSaberReceiver.IMUPacket> setPacket, string handLabel)
    {
        int parseFailures = 0;
        bool loggedFirstValid = false;

        while (serial != null && serial.IsOpen)
        {
            try
            {
                string line = serial.ReadLine();
                var p = UDPSaberReceiver.ParsePacket(line);

                if (p.valid)
                {
                    parseFailures = 0;
                    lock (lockObj) { setPacket(p); }
                    if (!loggedFirstValid)
                    {
                        loggedFirstValid = true;
                        Debug.Log($"[SerialSaberReceiver] {handLabel}: first valid packet received. " +
                                  $"gyro=({p.angularVelocity.x:F1},{p.angularVelocity.y:F1},{p.angularVelocity.z:F1})");
                    }
                }
                else
                {
                    parseFailures++;
                    if (parseFailures % ParseFailureLogInterval == 1)
                    {
                        string snippet = line != null && line.Length > 80
                            ? line.Substring(0, 80) + "…" : (line ?? "(null)");
                        Debug.LogWarning($"[SerialSaberReceiver] {handLabel} parse fail #{parseFailures}: \"{snippet}\"");
                    }
                }
            }
            catch (TimeoutException) { }
            catch (InvalidOperationException) { break; }
            catch (UnauthorizedAccessException) { break; }
            catch (ThreadInterruptedException) { break; }
            catch (System.IO.IOException) { break; }
            catch (Exception ex)
            {
                if (serial == null || !serial.IsOpen)
                    break;
                Debug.LogWarning($"[SerialSaberReceiver] {handLabel} read error: {ex.Message}");
            }
        }
        Debug.Log($"[SerialSaberReceiver] {handLabel}: ReadLoop exited (port open={serial?.IsOpen ?? false}).");
    }

    private const int ParseFailureLogInterval = 20;

    void OnDestroy()
    {
        ClosePort(ref leftPort, ref leftThread);
        ClosePort(ref rightPort, ref rightThread);
    }

    void OnApplicationQuit()
    {
        ClosePort(ref leftPort, ref leftThread);
        ClosePort(ref rightPort, ref rightThread);
    }

    static void ClosePort(ref SerialPort port, ref Thread thread)
    {
        try
        {
            port?.Close();
        }
        catch { /* ignore */ }
        port = null;
        thread?.Join(800);
        thread = null;
    }
}
