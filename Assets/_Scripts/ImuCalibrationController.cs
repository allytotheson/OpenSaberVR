using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Calibration screen: hold the controller button for 3 seconds per hand to capture rest orientation
/// and gyro bias. Builds its own UI at runtime. Loaded between Menu and OpenSaber.
/// </summary>
public class ImuCalibrationController : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds the button must be held continuously.")]
    public float holdDuration = 3f;
    [Tooltip("Seconds to wait after both hands calibrate before loading gameplay.")]
    public float autoAdvanceDelay = 1f;

    private float _leftHoldTime;
    private float _rightHoldTime;
    private bool _leftDone;
    private bool _rightDone;
    private bool _advancing;

    private readonly List<Vector3> _leftGyroSamples = new List<Vector3>(256);
    private readonly List<Vector3> _leftAccelSamples = new List<Vector3>(256);
    private readonly List<Vector3> _rightGyroSamples = new List<Vector3>(256);
    private readonly List<Vector3> _rightAccelSamples = new List<Vector3>(256);

    // UI refs (built at Awake)
    private Text _instructionText;
    private Image _leftBar;
    private Image _rightBar;
    private Text _leftLabel;
    private Text _rightLabel;
    private Text _statusText;
    private Button _skipButton;

    void Awake()
    {
        BuildUI();
    }

    void Update()
    {
        if (_advancing) return;

        var imu = ImuSourceResolver.GetActiveSource();
        if (imu == null)
        {
            _statusText.text = "No IMU receiver detected. Plug in controller or connect via Wi-Fi.";
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
                SkipCalibration();
            return;
        }

        UpdateHand(imu.LeftSaberData, SaberMotionController.SaberHand.Left,
            ref _leftHoldTime, ref _leftDone, _leftGyroSamples, _leftAccelSamples,
            _leftBar, _leftLabel);

        UpdateHand(imu.RightSaberData, SaberMotionController.SaberHand.Right,
            ref _rightHoldTime, ref _rightDone, _rightGyroSamples, _rightAccelSamples,
            _rightBar, _rightLabel);

        if (_leftDone && _rightDone && !_advancing)
        {
            _advancing = true;
            _statusText.text = "Calibration complete!";
            StartCoroutine(AdvanceAfterDelay());
        }

        if (Input.GetKeyDown(KeyCode.Escape))
            SkipCalibration();
    }

    void UpdateHand(UDPSaberReceiver.IMUPacket data, SaberMotionController.SaberHand hand,
        ref float holdTime, ref bool done,
        List<Vector3> gyroSamples, List<Vector3> accelSamples,
        Image bar, Text label)
    {
        if (done)
        {
            bar.fillAmount = 1f;
            label.text = hand + ": DONE";
            return;
        }

        if (!data.valid || !data.hasControllerExtras)
        {
            label.text = hand + ": waiting for data...";
            bar.fillAmount = 0f;
            holdTime = 0f;
            gyroSamples.Clear();
            accelSamples.Clear();
            return;
        }

        if (data.selectPressed)
        {
            holdTime += Time.deltaTime;
            gyroSamples.Add(data.angularVelocity);
            accelSamples.Add(data.acceleration);
            bar.fillAmount = Mathf.Clamp01(holdTime / holdDuration);
            label.text = hand + ": hold... " + Mathf.CeilToInt(holdDuration - holdTime) + "s";

            if (holdTime >= holdDuration)
            {
                FinalizeCalibration(hand, gyroSamples, accelSamples);
                done = true;
            }
        }
        else
        {
            holdTime = 0f;
            gyroSamples.Clear();
            accelSamples.Clear();
            bar.fillAmount = 0f;
            label.text = hand + ": press & hold button";
        }
    }

    void FinalizeCalibration(SaberMotionController.SaberHand hand,
        List<Vector3> gyroSamples, List<Vector3> accelSamples)
    {
        Vector3 avgGyro = Vector3.zero;
        foreach (var g in gyroSamples) avgGyro += g;
        if (gyroSamples.Count > 0) avgGyro /= gyroSamples.Count;

        Vector3 avgAccel = Vector3.zero;
        foreach (var a in accelSamples) avgAccel += a;
        if (accelSamples.Count > 0) avgAccel /= accelSamples.Count;

        Vector3 up = avgAccel.normalized;
        Vector3 fwd = Vector3.Cross(up, hand == SaberMotionController.SaberHand.Left ? Vector3.right : -Vector3.right);
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        else fwd.Normalize();
        Quaternion restOri = Quaternion.LookRotation(fwd, up);

        Vector3 rawBias = new Vector3(-avgGyro.x, -avgGyro.y, -avgGyro.z);

        CalibrationData.Set(hand, new CalibrationData.HandCalibration
        {
            gyroBias = rawBias,
            restOrientation = restOri,
            restAccel = avgAccel,
            isCalibrated = true
        });

        Debug.Log($"[Calibration] {hand} done. Bias={rawBias}, RestAccel={avgAccel}");
    }

    IEnumerator AdvanceAfterDelay()
    {
        yield return new WaitForSeconds(autoAdvanceDelay);
        LoadGameplay();
    }

    public void SkipCalibration()
    {
        if (_advancing) return;
        _advancing = true;
        LoadGameplay();
    }

    void LoadGameplay()
    {
        GameplayCalibrationGate.BlocksNoteTimeline = false;

        var sh = Object.FindAnyObjectByType<SceneHandling>();
        if (sh != null)
        {
            StartCoroutine(LoadViaSceneHandling(sh));
        }
        else
        {
            SceneManager.LoadScene("OpenSaber", LoadSceneMode.Additive);
            SceneManager.UnloadSceneAsync("Calibration");
        }
    }

    IEnumerator LoadViaSceneHandling(SceneHandling sh)
    {
        yield return sh.LoadScene("OpenSaber", LoadSceneMode.Additive);
        yield return sh.UnloadScene("Calibration");
    }

    // ---- Procedural UI ----

    void BuildUI()
    {
        var canvasGo = new GameObject("CalibrationCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var bg = CreatePanel(canvasGo.transform, "Bg", Color.black);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.06f, 0.02f, 0.12f, 1f);

        Font font = GetDefaultFont();

        _instructionText = CreateText(canvasGo.transform, "Instructions", font,
            "Hold your controller in rest position\nand press the button for 3 seconds.",
            32, new Vector2(0f, 180f));

        _leftLabel = CreateText(canvasGo.transform, "LeftLabel", font, "Left: press & hold button", 24, new Vector2(-240f, 40f));
        _leftBar = CreateProgressBar(canvasGo.transform, "LeftBar", new Vector2(-240f, -10f), new Color(0.2f, 0.5f, 1f));

        _rightLabel = CreateText(canvasGo.transform, "RightLabel", font, "Right: press & hold button", 24, new Vector2(240f, 40f));
        _rightBar = CreateProgressBar(canvasGo.transform, "RightBar", new Vector2(240f, -10f), new Color(1f, 0.3f, 0.3f));

        _statusText = CreateText(canvasGo.transform, "Status", font, "", 22, new Vector2(0f, -100f));
        _statusText.color = new Color(0.7f, 0.7f, 0.7f);

        var skipGo = new GameObject("SkipButton");
        skipGo.transform.SetParent(canvasGo.transform, false);
        var skipRt = skipGo.AddComponent<RectTransform>();
        skipRt.anchorMin = skipRt.anchorMax = new Vector2(0.5f, 0f);
        skipRt.pivot = new Vector2(0.5f, 0f);
        skipRt.anchoredPosition = new Vector2(0f, 40f);
        skipRt.sizeDelta = new Vector2(200f, 50f);

        var skipImg = skipGo.AddComponent<Image>();
        skipImg.color = new Color(0.25f, 0.25f, 0.3f);
        _skipButton = skipGo.AddComponent<Button>();
        _skipButton.targetGraphic = skipImg;
        _skipButton.onClick.AddListener(SkipCalibration);

        var skipLabel = CreateText(skipGo.transform, "SkipLabel", font, "Skip", 22, Vector2.zero);
        var srt = skipLabel.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.sizeDelta = Vector2.zero;
        skipLabel.alignment = TextAnchor.MiddleCenter;
    }

    static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    static Text CreateText(Transform parent, string name, Font font, string content, int size, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(600f, 80f);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        return t;
    }

    static Image CreateProgressBar(Transform parent, string name, Vector2 pos, Color fillColor)
    {
        var bgGo = new GameObject(name + "Bg");
        bgGo.transform.SetParent(parent, false);
        var bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = pos;
        bgRt.sizeDelta = new Vector2(360f, 30f);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.2f);

        var fillGo = new GameObject(name + "Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = fillColor;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 0f;
        return fillImg;
    }

    static Font GetDefaultFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 24);
        return font;
    }
}
