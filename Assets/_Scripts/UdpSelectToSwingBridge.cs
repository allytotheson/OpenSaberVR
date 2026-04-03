using UnityEngine;

/// <summary>
/// When UDP packets include <see cref="UDPSaberReceiver.IMUPacket.selectPressed"/> (joystick SW),
/// fires a short <see cref="SwingDetector"/> pulse on rising edge — same idea as Z/X for cuts.
/// </summary>
[DefaultExecutionOrder(58)]
public class UdpSelectToSwingBridge : MonoBehaviour
{
    public UDPSaberReceiver receiver;
    bool _prevLeftSel;
    bool _prevRightSel;

    void Awake()
    {
        if (receiver == null)
            receiver = FindAnyObjectByType<UDPSaberReceiver>();
    }

    void Update()
    {
        if (receiver == null)
            return;
        if (!DesktopSaberTestInput.TryResolveSabers(out GameObject left, out GameObject right))
            return;

        var ld = receiver.LeftSaberData;
        if (ld.valid && ld.hasControllerExtras)
        {
            if (ld.selectPressed && !_prevLeftSel)
                TryPulse(left);
            _prevLeftSel = ld.selectPressed;
        }
        else
            _prevLeftSel = false;

        var rd = receiver.RightSaberData;
        if (rd.valid && rd.hasControllerExtras)
        {
            if (rd.selectPressed && !_prevRightSel)
                TryPulse(right);
            _prevRightSel = rd.selectPressed;
        }
        else
            _prevRightSel = false;
    }

    static void TryPulse(GameObject hand)
    {
        if (hand == null)
            return;
        foreach (var sw in hand.GetComponentsInChildren<SwingDetector>(true))
        {
            if (sw != null && sw.isActiveAndEnabled)
                sw.PulseTestSwing(0.45f);
        }
    }
}
