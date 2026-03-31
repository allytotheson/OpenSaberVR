using UnityEngine;

/// <summary>
/// Brief local rotation kick on Z/X (and Space) so imported meshes read as slashing; parent hand motion still drives position.
/// </summary>
[DefaultExecutionOrder(440)]
public class DesktopImportedBladeSlashFx : MonoBehaviour
{
    public float slashDuration = 0.13f;

    public float slashMaxDegrees = 48f;

    bool _isLeft;
    bool _configured;
    float _slash01;
    Quaternion _baseLocalAtSlashStart = Quaternion.identity;

    public void Configure(bool isLeftHandSaber)
    {
        UnsubscribeEvents();
        _isLeft = isLeftHandSaber;
        _configured = true;
        if (isActiveAndEnabled)
            SubscribeEvents();
    }

    void OnEnable()
    {
        if (_configured)
            SubscribeEvents();
    }

    void OnDisable()
    {
        UnsubscribeEvents();
    }

    void SubscribeEvents()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (_isLeft)
            DesktopSaberTestInput.LeftKeyboardSlashPressed += OnSlash;
        else
            DesktopSaberTestInput.RightKeyboardSlashPressed += OnSlash;
#endif
    }

    void UnsubscribeEvents()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        DesktopSaberTestInput.LeftKeyboardSlashPressed -= OnSlash;
        DesktopSaberTestInput.RightKeyboardSlashPressed -= OnSlash;
#endif
    }

    void OnSlash()
    {
        _slash01 = 1f;
        _baseLocalAtSlashStart = transform.localRotation;
    }

    void LateUpdate()
    {
        if (_slash01 <= 0f)
            return;

        _slash01 -= Time.deltaTime / Mathf.Max(0.04f, slashDuration);
        float u = Mathf.Clamp01(1f - _slash01);
        float kick = slashMaxDegrees * Mathf.Sin(u * Mathf.PI);
        transform.localRotation = _baseLocalAtSlashStart * Quaternion.Euler(kick, 0f, 0f);
    }
}
