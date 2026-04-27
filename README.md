# Open Saber VR

Forked from upstream Open Saber VR. Reimagined for the movie KPop Demon Hunters where the player hits demons to the beat of the song. Made for CMU Asian Students Association (ASA) for Spring Carnival 2026, ASA KPop Demon Hunters Booth. 

Introduces **UDP Saber Mode**: Play without VR using Pico W controllers sending IMU data over UDP.


## Desktop testing (keyboard, no Pi)

While UDP has **no valid packet** for a saber, **`DesktopSaberTestInput`** (auto-created on `SceneHandling`) moves that saber with the keyboard and **Pi data takes over** when packets arrive.

| | **Left saber** | **Right saber** |
|---|----------------|-----------------|
| Move (flat to camera) | **W A S D** | **I J K L** |
| Up / down | **R** / **F** | **U** / **O** |
| Yaw | **Q** / **E** | **,** / **.** |
| Swing pulse (for hits) | **Z** | **X** |
.

## UDP Saber Setup (Pico W Controllers)

The project supports motion-controlled sabers over UDP, no XR/VR required.

### 1. Pico W Firmware
Send IMU packets (MPU6050: accel + gyro) as text over UDP. Format: `ax,ay,az,gx,gy,gz` (comma-separated).

- Left saber: port **5000**
- Right saber: port **5001**
- Units: accel in g, gyro in deg/s

