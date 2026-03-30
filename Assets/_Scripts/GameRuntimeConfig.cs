/// <summary>
/// High-level deployment / input mode. Developer desktop uses mouse + fixed camera;
/// Raspberry Pi will later swap in GPIO / IMU-driven input while keeping the same gameplay flow.
/// </summary>
public static class GameRuntimeConfig
{
    public enum DeploymentMode
    {
        DeveloperDesktop,
        RaspberryPi
    }

    /// <summary>
    /// Default is developer desktop (PC, no headset). Set to RaspberryPi when building for the device.
    /// </summary>
    public static DeploymentMode Mode = DeploymentMode.DeveloperDesktop;
}
