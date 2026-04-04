/// <summary>
/// IMU packet source for saber motion (UDP or USB serial). Uses <see cref="UDPSaberReceiver.IMUPacket"/> as the shared layout.
/// </summary>
public interface IImuSaberReceiver
{
    UDPSaberReceiver.IMUPacket LeftSaberData { get; }
    UDPSaberReceiver.IMUPacket RightSaberData { get; }
}
