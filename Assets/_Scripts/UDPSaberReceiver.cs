using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receives IMU data (accelerometer + gyroscope) over UDP from Raspberry Pi Pico W controllers.
/// Supports two sabers via separate UDP ports.
/// Expected packet format: "ax,ay,az,gx,gy,gz" (comma-separated, MPU6050 units: g for accel, deg/s for gyro)
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

        Debug.Log($"[UDPSaberReceiver] Listening on ports {leftSaberPort} (left), {rightSaberPort} (right). Expect format: ax,ay,az,gx,gy,gz");
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
    /// Parse "ax,ay,az,gx,gy,gz" format. Also supports space-separated or JSON-like formats.
    /// </summary>
    public static IMUPacket ParsePacket(string raw)
    {
        var p = new IMUPacket { valid = false, timestamp = Time.realtimeSinceStartup };
        if (string.IsNullOrWhiteSpace(raw)) return p;

        string s = raw.Trim();
        char[] sep = { ',', ' ', '\t' };
        string[] parts = s.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 6)
        {
            if (float.TryParse(parts[0], out float ax) && float.TryParse(parts[1], out float ay) &&
                float.TryParse(parts[2], out float az) && float.TryParse(parts[3], out float gx) &&
                float.TryParse(parts[4], out float gy) && float.TryParse(parts[5], out float gz))
            {
                p.acceleration = new Vector3(ax, ay, az);
                p.angularVelocity = new Vector3(gx, gy, gz);
                p.valid = true;
            }
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
