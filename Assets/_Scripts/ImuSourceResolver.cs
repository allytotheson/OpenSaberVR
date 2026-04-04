using UnityEngine;

/// <summary>
/// Centralized resolution of the active <see cref="IImuSaberReceiver"/>.
/// Serial (USB) takes priority over UDP (Wi-Fi) when the component is present and enabled.
/// </summary>
public static class ImuSourceResolver
{
    public static IImuSaberReceiver GetActiveSource()
    {
        foreach (var serial in Object.FindObjectsByType<SerialSaberReceiver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (serial != null && serial.isActiveAndEnabled && serial.HasOpenSerialPort)
                return serial;
        }

        foreach (var udp in Object.FindObjectsByType<UDPSaberReceiver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (udp != null && udp.isActiveAndEnabled)
                return udp;
        }

        return null;
    }

    /// <summary>Returns the active source as a <see cref="MonoBehaviour"/> for inspector assignment.</summary>
    public static MonoBehaviour GetActiveSourceBehaviour()
    {
        var src = GetActiveSource();
        return src as MonoBehaviour;
    }
}
