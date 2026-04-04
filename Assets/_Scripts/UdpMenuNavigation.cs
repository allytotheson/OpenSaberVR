using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Uses the extended UDP packet (jx, jy, sw) from the Pico to drive the main menu before gameplay loads.
/// Left port by default. Title: select = Start. Quit confirm: stick X = No / Yes; select confirms.
/// Song list: stick X = Prev / Next (repeat), select = LoadSong.
/// Difficulty panel: stick X picks option; select confirms.
/// Requires <see cref="UDPSaberReceiver.IMUPacket.hasControllerExtras"/>.
/// </summary>
[DisallowMultipleComponent]
public class UdpMenuNavigation : MonoBehaviour
{
    [FormerlySerializedAs("receiver")]
    [Tooltip("UDP or serial IMU receiver on this GameObject (or drag explicitly).")]
    public MonoBehaviour imuSource;
    [Tooltip("Use left-hand channel (UDP port 5000 or left COM) for menu; set false for right.")]
    public bool useLeftPort = true;
    public bool enableNavigation = true;

    [Tooltip("Stick X below this (0..1) selects left option.")]
    [Range(0f, 0.49f)] public float stickLeftThreshold = 0.4f;
    [Tooltip("Stick X above this selects right option.")]
    [Range(0.51f, 1f)] public float stickRightThreshold = 0.6f;

    [Tooltip("Min seconds between Prev/Next when browsing songs.")]
    public float songStickRepeatSeconds = 0.45f;

    int _sureSel;
    int _difficultySel;
    bool _prevSelect;
    float _nextSongStickTime;
    readonly List<Button> _difficultyButtonsScratch = new List<Button>(8);

    void Awake()
    {
        if (imuSource == null)
        {
            imuSource = GetComponent<UDPSaberReceiver>() as MonoBehaviour
                     ?? GetComponent<SerialSaberReceiver>() as MonoBehaviour
                     ?? ImuSourceResolver.GetActiveSourceBehaviour();
        }
    }

    void Update()
    {
        var imu = imuSource as IImuSaberReceiver;
        if (!enableNavigation || imu == null)
            return;

        var mm = Object.FindAnyObjectByType<MainMenu>();
        if (mm == null)
            return;

        var d = useLeftPort ? imu.LeftSaberData : imu.RightSaberData;
        if (!d.valid || !d.hasControllerExtras)
            return;

        float jx = d.joystickX;
        bool sel = d.selectPressed;
        bool rising = sel && !_prevSelect;
        _prevSelect = sel;

        if (IsHomeOrNoSongsScreen(mm))
        {
            if (mm.NoSongsFound != null && mm.NoSongsFound.activeSelf)
            {
                ApplyNoSongsFocus(mm);
                if (rising)
                    mm.ExitApplication();
            }
            else
            {
                ApplyTitleFocus(mm);
                if (rising)
                    mm.ShowSongs();
            }
        }
        else if (mm.PanelAreYouSure != null && mm.PanelAreYouSure.activeSelf)
        {
            UpdateSureSelection(jx);
            ApplySureFocus(mm);
            if (rising)
            {
                if (_sureSel == 0)
                    mm.No();
                else
                    mm.Yes();
            }
        }
        else if (mm.LevelChooser != null && mm.LevelChooser.activeSelf)
        {
            BuildDifficultyButtonList(mm);
            int n = _difficultyButtonsScratch.Count;
            if (n > 0)
            {
                _difficultySel = PickDifficultyIndex(jx, n, _difficultySel);
                ApplyDifficultyFocus(mm);
                if (rising && _difficultySel >= 0 && _difficultySel < n)
                    _difficultyButtonsScratch[_difficultySel].onClick.Invoke();
            }
        }
        else if (mm.SongChooser != null && mm.SongChooser.activeSelf)
        {
            ApplySongBrowserFocus(mm, jx);
            if (Time.time >= _nextSongStickTime)
            {
                if (jx < stickLeftThreshold)
                {
                    mm.PreviousSong();
                    _nextSongStickTime = Time.time + songStickRepeatSeconds;
                }
                else if (jx > stickRightThreshold)
                {
                    mm.NextSong();
                    _nextSongStickTime = Time.time + songStickRepeatSeconds;
                }
            }

            if (rising)
                mm.LoadSong();
        }
    }

    static bool IsHomeOrNoSongsScreen(MainMenu mm)
    {
        if (mm.SongChooser != null && mm.SongChooser.activeSelf)
            return false;
        if (mm.PanelAreYouSure != null && mm.PanelAreYouSure.activeSelf)
            return false;
        if (mm.LevelChooser != null && mm.LevelChooser.activeSelf)
            return false;
        if (mm.NoSongsFound != null && mm.NoSongsFound.activeSelf)
            return true;
        return mm.Title != null && mm.Title.activeSelf;
    }

    void UpdateSureSelection(float jx)
    {
        if (jx < stickLeftThreshold)
            _sureSel = 0;
        else if (jx > stickRightThreshold)
            _sureSel = 1;
    }

    /// <summary>2 options: left / right + hysteresis. 3+: map stick across inner range to indices.</summary>
    int PickDifficultyIndex(float jx, int count, int current)
    {
        if (count <= 1)
            return 0;
        if (count == 2)
        {
            if (jx < stickLeftThreshold)
                return 0;
            if (jx > stickRightThreshold)
                return 1;
            return current;
        }

        float u = Mathf.InverseLerp(0.2f, 0.8f, Mathf.Clamp(jx, 0.2f, 0.8f));
        return Mathf.Clamp(Mathf.RoundToInt(u * (count - 1)), 0, count - 1);
    }

    void ApplyNoSongsFocus(MainMenu mm)
    {
        var es = EventSystem.current;
        if (es == null)
            return;
        var exitB = ResolveHomeExitButton(mm);
        if (exitB != null && exitB.gameObject.activeInHierarchy)
            es.SetSelectedGameObject(exitB.gameObject);
    }

    void ApplyTitleFocus(MainMenu mm)
    {
        var es = EventSystem.current;
        if (es == null)
            return;
        var startB = ResolveHomeStartButton(mm);
        if (startB != null && startB.gameObject.activeInHierarchy)
            es.SetSelectedGameObject(startB.gameObject);
    }

    void ApplySureFocus(MainMenu mm)
    {
        var es = EventSystem.current;
        if (es == null || mm.PanelAreYouSure == null)
            return;
        var noB = FindButtonByObjectName(mm.PanelAreYouSure.transform, "Btn_No");
        var yesB = FindButtonByObjectName(mm.PanelAreYouSure.transform, "Btn_Yes");
        if (_sureSel == 0 && noB != null)
            es.SetSelectedGameObject(noB.gameObject);
        else if (_sureSel == 1 && yesB != null)
            es.SetSelectedGameObject(yesB.gameObject);
    }

    void BuildDifficultyButtonList(MainMenu mm)
    {
        _difficultyButtonsScratch.Clear();
        var lc = mm.LevelChooser;
        if (lc == null)
            return;
        foreach (var b in lc.GetComponentsInChildren<Button>(true))
        {
            if (b == null || !b.gameObject.activeInHierarchy)
                continue;
            if (b.gameObject.name == "ButtonTemplate")
                continue;
            _difficultyButtonsScratch.Add(b);
        }

        _difficultyButtonsScratch.Sort((a, b) =>
        {
            var ra = a != null ? a.GetComponent<RectTransform>() : null;
            var rb = b != null ? b.GetComponent<RectTransform>() : null;
            if (ra == null || rb == null)
                return 0;
            return ra.anchoredPosition.x.CompareTo(rb.anchoredPosition.x);
        });
    }

    void ApplyDifficultyFocus(MainMenu mm)
    {
        var es = EventSystem.current;
        if (es == null || _difficultyButtonsScratch.Count == 0)
            return;
        int i = Mathf.Clamp(_difficultySel, 0, _difficultyButtonsScratch.Count - 1);
        var b = _difficultyButtonsScratch[i];
        if (b != null)
            es.SetSelectedGameObject(b.gameObject);
    }

    void ApplySongBrowserFocus(MainMenu mm, float jx)
    {
        var es = EventSystem.current;
        if (es == null || mm.SongChooser == null)
            return;
        var root = mm.SongChooser.transform;
        var prev = FindButtonByObjectName(root, "Btn_Prev");
        var next = FindButtonByObjectName(root, "Btn_Next");
        var play = FindButtonByObjectName(root, "PlaySong");
        if (jx < stickLeftThreshold && prev != null)
            es.SetSelectedGameObject(prev.gameObject);
        else if (jx > stickRightThreshold && next != null)
            es.SetSelectedGameObject(next.gameObject);
        else if (play != null)
            es.SetSelectedGameObject(play.gameObject);
    }

    static Button ResolveHomeStartButton(MainMenu mm)
    {
        if (mm.HomeScreenButtonStart != null)
        {
            var b = mm.HomeScreenButtonStart.GetComponent<Button>();
            if (b != null)
                return b;
        }
        if (mm.Title != null)
        {
            var t = FindButtonByObjectName(mm.Title.transform, "Btn_Start");
            if (t != null)
                return t;
        }
        return FindButtonByObjectName(mm.transform, "Btn_Start");
    }

    static Button ResolveHomeExitButton(MainMenu mm)
    {
        if (mm.HomeScreenButtonExit != null)
        {
            var b = mm.HomeScreenButtonExit.GetComponent<Button>();
            if (b != null)
                return b;
        }
        if (mm.Title != null)
        {
            var t = FindButtonByObjectName(mm.Title.transform, "Btn_Exit");
            if (t != null)
                return t;
        }
        return FindButtonByObjectName(mm.transform, "Btn_Exit");
    }

    static Button FindButtonByObjectName(Transform root, string objectName)
    {
        if (root == null)
            return null;
        foreach (var b in root.GetComponentsInChildren<Button>(true))
        {
            if (b != null && b.gameObject.name == objectName)
                return b;
        }
        return null;
    }
}
