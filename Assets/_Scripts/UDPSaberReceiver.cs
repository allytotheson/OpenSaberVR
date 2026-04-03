using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receives IMU + optional joystick/button from Raspberry Pi Pico W over UDP (two ports for two hands).
/// Formats: "ax,ay,az,gx,gy,gz" or extended "ax,ay,az,gx,gy,gz,jx,jy,sw" (jx,jy normalized 0..1, sw 1=select pressed).
/// See PicoW_Controller_Reference.txt.
/// </summary>
public class UDPSaberReceiver : MonoBehaviour
{
    [Header("UDP Ports (one per saber)")]
    [Tooltip("Port for left saber (Pico W controller 1)")]
    public int leftSaberPort = 5000;
    [Tooltip("Port for right saber (Pico W controller 2)")]
    public int rightSaberPort = 5001;

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

        try
        {
            leftClient = new UdpClient(leftSaberPort);
            leftClient.Client.ReceiveTimeout = 100;
            leftThread = new Thread(() => ReceiveLoop(leftClient, leftLock, (d) => leftSaberData = d)) { IsBackground = true };
            leftThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UDPSaberReceiver] Left saber port {leftSaberPort}: {e.Message}");
        }

        try
        {
            rightClient = new UdpClient(rightSaberPort);
            rightClient.Client.ReceiveTimeout = 100;
            rightThread = new Thread(() => ReceiveLoop(rightClient, rightLock, (d) => rightSaberData = d)) { IsBackground = true };
            rightThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UDPSaberReceiver] Right saber port {rightSaberPort}: {e.Message}");
        }

        Debug.Log($"[UDPSaberReceiver] Listening on ports {leftSaberPort} (left), {rightSaberPort} (right). Format: ax,ay,az,gx,gy,gz[,jx,jy,sw]");

        if (GetComponent<UdpMenuNavigation>() == null)
            gameObject.AddComponent<UdpMenuNavigation>();
    }

    private void ReceiveLoop(UdpClient client, object lockObj, Action<IMUPacket> setData)
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        while (running && client != null)
        {
            try
            {
                byte[] bytes = client.Receive(ref remote);
                string raw = System.Text.Encoding.UTF8.GetString(bytes);
                var p = ParsePacket(raw);
                lock (lockObj) { setData(p); }
            }
            catch (SocketException) { /* timeout */ }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { Debug.LogWarning($"[UDPSaberReceiver] Parse error: {ex.Message}"); }
        }
    }

    /// <summary>
    /// Parse "ax,ay,az,gx,gy,gz" or extended "... ,jx,jy,sw" (jx,jy in 0..1, sw 1 = pressed).
    /// </summary>
    public static IMUPacket ParsePacket(string raw)
    {
        var p = new IMUPacket { valid = false, timestamp = Time.realtimeSinceStartup };
        if (string.IsNullOrWhiteSpace(raw)) return p;

        string s = raw.Trim();
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
    }
}
