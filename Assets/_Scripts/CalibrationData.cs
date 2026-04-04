using UnityEngine;

/// <summary>
/// Stores per-hand IMU calibration (gyro bias, rest orientation) with PlayerPrefs persistence.
/// Populated by <see cref="ImuCalibrationController"/> in the Calibration scene.
/// </summary>
public static class CalibrationData
{
    public struct HandCalibration
    {
        public Vector3 gyroBias;
        public Quaternion restOrientation;
        public Vector3 restAccel;
        public bool isCalibrated;
    }

    private static HandCalibration _left;
    private static HandCalibration _right;

    static CalibrationData()
    {
        Load();
    }

    public static bool TryGet(SaberMotionController.SaberHand hand, out HandCalibration cal)
    {
        cal = hand == SaberMotionController.SaberHand.Left ? _left : _right;
        return cal.isCalibrated;
    }

    public static void Set(SaberMotionController.SaberHand hand, HandCalibration cal)
    {
        if (hand == SaberMotionController.SaberHand.Left)
            _left = cal;
        else
            _right = cal;
        Save();
    }

    public static void Clear()
    {
        _left = default;
        _right = default;
        PlayerPrefs.DeleteKey("CalibLeft");
        PlayerPrefs.DeleteKey("CalibRight");
        PlayerPrefs.Save();
    }

    static void Save()
    {
        PlayerPrefs.SetString("CalibLeft", ToJson(_left));
        PlayerPrefs.SetString("CalibRight", ToJson(_right));
        PlayerPrefs.Save();
    }

    static void Load()
    {
        if (PlayerPrefs.HasKey("CalibLeft"))
            _left = FromJson(PlayerPrefs.GetString("CalibLeft"));
        if (PlayerPrefs.HasKey("CalibRight"))
            _right = FromJson(PlayerPrefs.GetString("CalibRight"));
    }

    static string ToJson(HandCalibration c)
    {
        return JsonUtility.ToJson(new SerializableCalibration
        {
            gyroBiasX = c.gyroBias.x, gyroBiasY = c.gyroBias.y, gyroBiasZ = c.gyroBias.z,
            restRotX = c.restOrientation.x, restRotY = c.restOrientation.y,
            restRotZ = c.restOrientation.z, restRotW = c.restOrientation.w,
            restAccelX = c.restAccel.x, restAccelY = c.restAccel.y, restAccelZ = c.restAccel.z,
            isCalibrated = c.isCalibrated
        });
    }

    static HandCalibration FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return default;
        var s = JsonUtility.FromJson<SerializableCalibration>(json);
        return new HandCalibration
        {
            gyroBias = new Vector3(s.gyroBiasX, s.gyroBiasY, s.gyroBiasZ),
            restOrientation = new Quaternion(s.restRotX, s.restRotY, s.restRotZ, s.restRotW),
            restAccel = new Vector3(s.restAccelX, s.restAccelY, s.restAccelZ),
            isCalibrated = s.isCalibrated
        };
    }

    [System.Serializable]
    struct SerializableCalibration
    {
        public float gyroBiasX, gyroBiasY, gyroBiasZ;
        public float restRotX, restRotY, restRotZ, restRotW;
        public float restAccelX, restAccelY, restAccelZ;
        public bool isCalibrated;
    }
}
