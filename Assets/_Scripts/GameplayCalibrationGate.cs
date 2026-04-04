using UnityEngine;

/// <summary>
/// When true, <see cref="NotesSpawner"/> does not advance beat time or spawn notes.
/// Used while the IMU calibration overlay is shown after OpenSaber has loaded.
/// </summary>
public static class GameplayCalibrationGate
{
    public static bool BlocksNoteTimeline { get; set; }
}
