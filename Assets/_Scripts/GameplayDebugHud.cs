using UnityEngine;

/// <summary>
/// In-play IMGUI. Toggle disables auto-align so you can test manual WASD. Auto-align defaults ON each play session.
/// Optional saber diagnostics for desktop debugging (RPi will replace keyboard path later).
/// </summary>
public class GameplayDebugHud : MonoBehaviour
{
    /// <summary>When true (default), sabers follow blocks + camera hand anchors. When false, WASD/IJKL manual drive.</summary>
    public static bool AutoAlignSabersToNotes { get; set; } = true;

    [Tooltip("Show saber positions, swing state, and nearest-note distances.")]
    public bool showSaberDiagnostics = true;

    private const string ToggleLabel = "Debug: auto-align sabers to notes";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        AutoAlignSabersToNotes = true;
    }

    public static void EnsureCreated(Transform parent)
    {
        if (parent == null)
            return;
        if (parent.GetComponentInChildren<GameplayDebugHud>(true) != null)
            return;

        var root = new GameObject("GameplayDebugHud");
        root.transform.SetParent(parent, false);
        root.AddComponent<GameplayDebugHud>();
    }

    private void OnGUI()
    {
        const float w = 420f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool diag = showSaberDiagnostics;
#else
        const bool diag = false;
#endif
        GUILayout.BeginArea(new Rect(12f, 10f, w, diag ? 220f : 44f), GUI.skin.box);
        AutoAlignSabersToNotes = GUILayout.Toggle(AutoAlignSabersToNotes, ToggleLabel);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (showSaberDiagnostics)
        {
            GUILayout.Space(6f);
            var sh = FindAnyObjectByType<SceneHandling>();
            var desktop = FindAnyObjectByType<DesktopSaberTestInput>();
            GUILayout.Label($"Gameplay cam: {(DesktopGameplayCamera.TryGet(out var gc) ? gc.name : "none")}");
            GUILayout.Label($"preferUdpImuWhenValid: {(desktop != null && desktop.preferUdpImuWhenValid)}");

            GameObject L = sh != null ? sh.LeftSaber : GameObject.FindGameObjectWithTag("LeftSaber");
            GameObject R = sh != null ? sh.RightSaber : GameObject.FindGameObjectWithTag("RightSaber");
            GUILayout.Label($"Left saber:  {(L != null ? L.name + " @ " + L.transform.position.ToString("F1") : "null")} self={L != null && L.activeSelf} inHierarchy={L != null && L.activeInHierarchy}");
            GUILayout.Label($"Right saber: {(R != null ? R.name + " @ " + R.transform.position.ToString("F1") : "null")} self={R != null && R.activeSelf} inHierarchy={R != null && R.activeInHierarchy}");

            var swingL = L != null ? L.GetComponentInChildren<SwingDetector>(true) : null;
            var swingR = R != null ? R.GetComponentInChildren<SwingDetector>(true) : null;
            GUILayout.Label($"Swing L: {(swingL != null && swingL.IsSwinging)}  R: {(swingR != null && swingR.IsSwinging)}  (Z / X)");

            float distL = NearestDemonDistance(L != null ? L.transform.position : Vector3.zero);
            float distR = NearestDemonDistance(R != null ? R.transform.position : Vector3.zero);
            GUILayout.Label($"Nearest demon dist L: {distL:F2}m  R: {distR:F2}m");
        }
#endif
        GUILayout.EndArea();
    }

    static float NearestDemonDistance(Vector3 from)
    {
        if (from.sqrMagnitude < 1e-6f) return -1f;
        var demons = FindObjectsByType<DemonHandling>(FindObjectsInactive.Exclude);
        float best = float.MaxValue;
        foreach (var d in demons)
        {
            if (d == null) continue;
            float s = (d.transform.position - from).sqrMagnitude;
            if (s < best) best = s;
        }
        return best < float.MaxValue ? Mathf.Sqrt(best) : -1f;
    }
}
