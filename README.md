![alt text](https://img.itch.zone/aW1nLzIxNzU5MTkucG5n/original/88KBuM.png "")
# Open Saber VR

Open Saber VR is an open source clone of the famous and fabolous game Beat Saber. 

I started this project by accident and managed to get the main game logic up and running in 3 days.  Thanks to the open source project beatsaver viewer I was able to get the blocks (notes) in sync with the beat!

Now you would maybe ask yourself what is a Beat Saber clone without any music? Yeah, you are right, it's nothing. But I have some vey good answer to this. Because of the great and wide community of the Beat Saber modders and there custom songs, you can use ANY song from their website and it will work in Open Saber VR. So just go to their websites [BeatSaver](https://beatsaver.com), [BeastSaber](https://bsaber.com) and download any song you want.

At the moment Open Saber VR only supports the notes (beat blocks). Obstacles and mines are not supported but will be added in the future.

If you are interested in helping/contributing to the project (no matter if you are a coding monkey or 3D artist or just have some ideas), feel free to contact me, I will be more than happy to have some help. You can find the complete source code here, so if you want to contribute, just have a look there.


## Import songs
Just download any song from BeatSaver or BeastSaber and unzip it to the "OpenSaberVR_data/Playlists" folder. Make sure that each song has its own folder in the Playlists folder. After that run Open Saber VR and the song should be displayed in the menu.

**Playing from the Unity Editor:** unzip songs into `Assets/Playlists` (each song in its own folder with `info.dat`). Open the scene **`Assets/_Scenes/PersistentScene`**, press **Play**. You should see the title UI; click **Songs** to open the library (if the folder is empty you will see the “no songs” message). Pick a song and difficulty to load **OpenSaber** additively. Without a VR headset, the **FallbackCamera_NonVR** is used: in the menu you use the mouse normally; in a level, **hold right mouse** to look around (Esc unlocks the cursor).

Maps with multiple **characteristics** (e.g. **Standard** and **No Arrows**) show separate menu entries like `ExpertPlus (Standard)` and `ExpertPlus (NoArrows)`. The `Playlists/` folder is gitignored (beatmaps stay local). **Beat Sage – Takedown (HUNTR/X)** is installed under `Assets/Playlists/BeatSage_Takedown_HUNTR_X` with all Standard and No Arrows difficulties; **bombs** in the charts are skipped (not supported yet).


## Gameplay
![alt text](https://img.itch.zone/aW1hZ2UvNDMyMDUzLzIyNDc2OTMucG5n/original/%2Bx5231.png "")
If you know how to play Beat Saber then you are good to go. If not then it's really simple to play, just cut the notes (beat blocks) at the side where the glow bar is with the saber in the same color. The blue saber is the right hand, the red saber is the left hand. The notes will be only sliced if you hit with the correct saber on the correct side. Otherwise the block will just went through.

There is no energy or anything right now, so you can't "loose" a game , the song will play until the end. Also there are no points right now, only some fun with the music.

After the song finished, just wait for 5 seconds and you will be pushed back to the main menu where you can select another song.


## Features
 - fully support for the songs from BeastSaber and BeatSaver
 - **UDP Saber Mode**: Play without VR using Pico W controllers sending IMU data over UDP


## Desktop testing (keyboard, no Pi)

While UDP has **no valid packet** for a saber, **`DesktopSaberTestInput`** (auto-created on `SceneHandling`) moves that saber with the keyboard and **Pi data takes over** when packets arrive.

| | **Left saber** | **Right saber** |
|---|----------------|-----------------|
| Move (flat to camera) | **W A S D** | **I J K L** |
| Up / down | **R** / **F** | **U** / **O** |
| Yaw | **Q** / **E** | **,** / **.** |
| Swing pulse (for hits) | **Z** | **X** |

A **cyan rectangular frame** is drawn in front of the camera when a song starts (`NotesSpawner.showHitLineGuide`) as a rough “cut plane” reference. Tune `BeatSaberHitLineGuide` on the spawned object if it doesn’t line up with your blocks.

## UDP Saber Setup (Pico W Controllers)

The project supports motion-controlled sabers over UDP, no XR/VR required.

### 1. Pico W Firmware
Send IMU packets (MPU6050: accel + gyro) as text over UDP. Format: `ax,ay,az,gx,gy,gz` (comma-separated).

- Left saber: port **5000**
- Right saber: port **5001**
- Units: accel in g, gyro in deg/s

### 2. Unity Scene Setup
- **UDPSaberReceiver**: Add to a manager GameObject. Listens on ports 5000 and 5001.
- **LeftSaber** / **RightSaber**: Create two saber GameObjects, tag them `LeftSaber` and `RightSaber`.
- On each saber, add: `SaberMotionController` (hand Left/Right), `SwingDetector`, `DemonHitDetector`, `Slice` (on blade child with Renderer).
- **SceneHandling**: Assign LeftSaber and RightSaber (or leave unset to find by tag).
- **NotesSpawner**: Assign **Demons** array with 4 prefabs (Left, Right, Left NonDir, Right NonDir). Can use existing cube prefabs; `DemonHandling` is added at runtime.
- **ScoreManager**: Optional, for score display.
- Create the "Demon" tag in Project Settings > Tags if missing.

### 3. IMU Filtering (recommended)
MPU6050 needs filtering for drift. Ask Cursor to implement:
- **Complementary filter**, or
- **Madgwick filter**


## Hints
 - Only HTC VIVE is tested, but it should also run on a Oculus Rift or Windows Mixed Reality Headset with SteamVR. If you have one of these, let me know if it works.
 - This is an early development version, so expect some bugs 


## Feedback is welcome 
Feedback is very welcome. If you have questions or ideas, then just leave a mail.

## Links
  [itch.io project page](https://devplayrepeat.itch.io/open-saber-vr)
