using UnityEngine;

/// <summary>
/// Inspector toggle for debug-only behavior: HUD, optional hit FX, extra saber smoothing, optional camera nudges.
/// The zoomed desktop play camera runs for everyone; <see cref="DesktopPlayerViewRig"/> adds dev-only offsets when enabled.
/// </summary>
[DefaultExecutionOrder(-200)]
public class DeveloperGameplayMode : MonoBehaviour
{
    public static DeveloperGameplayMode Instance { get; private set; }

    /// <summary>True when any loaded instance has <see cref="developerMode"/> enabled.</summary>
    public static bool Enabled { get; private set; }

    [Tooltip("Enables GameplayDebugHud, procedural hit burst, and optional extra camera/saber tuning below.")]
    public bool developerMode;

    [Header("Desktop saber (dev)")]
    [Tooltip("Extra minimum align smoothing when dev mode is on (stacked with DesktopSaberTestInput).")]
    public float developerAlignSmoothing = 26f;

    [Tooltip("When dev mode is on, force-disable idle saber jitter regardless of DesktopSaberTestInput.")]
    public bool suppressSaberIdleJitter = true;

    [Header("Desktop camera (dev-only nudge)")]
    [Tooltip("Added on top of the normal zoomed rig when dev mode is on (world space).")]
    public Vector3 devExtraEyeOffsetWorld;

    [Tooltip("Extra pitch (degrees) when dev mode is on, applied after the normal desktop rig.")]
    public float devExtraPitchDegrees;

    [Header("Hit feedback (dev)")]
    public Color hitBurstColor = new Color(0.5f, 0.95f, 1f, 1f);

    void OnEnable()
    {
        if (Instance == null)
            Instance = this;
    }

    void OnDisable()
    {
        if (Instance == this)
            Instance = FindAnyObjectByType<DeveloperGameplayMode>();
    }

    void LateUpdate()
    {
        Enabled = false;
        foreach (var m in FindObjectsByType<DeveloperGameplayMode>(FindObjectsInactive.Exclude))
        {
            if (m != null && m.developerMode)
            {
                Enabled = true;
                Instance = m;
                return;
            }
        }
    }

    public static void EnsureOnSpawner(Transform spawnerTransform)
    {
        if (spawnerTransform == null)
            return;
        if (spawnerTransform.GetComponent<DeveloperGameplayMode>() != null)
            return;
        if (FindAnyObjectByType<DeveloperGameplayMode>() != null)
            return;
        spawnerTransform.gameObject.AddComponent<DeveloperGameplayMode>();
    }
}
