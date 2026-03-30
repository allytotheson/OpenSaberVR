// Compatibility helpers for Unity XR API changes (XRDevice.model / refreshRate / isPresent removed in newer Unity).
#if UNITY_2017_2_OR_NEWER
namespace VRTK
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.XR;

    internal static class VRTK_XRCompat
    {
        public static string GetHeadsetModelName()
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, devices);
            if (devices.Count > 0)
            {
                return devices[0].name;
            }
            return string.Empty;
        }

        public static float GetDisplayRefreshRate()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays)
            {
                if (!display.running)
                {
                    continue;
                }
#if UNITY_2020_2_OR_NEWER
                if (display.TryGetDisplayRefreshRate(out float refreshRate) && refreshRate > 1f)
                {
                    return refreshRate;
                }
#endif
                return 90f;
            }
            return 60f;
        }

        public static bool IsHmdPresent()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays)
            {
                if (display.running)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
#endif
