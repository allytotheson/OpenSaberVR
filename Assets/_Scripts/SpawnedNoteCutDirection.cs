using UnityEngine;

/// <summary>
/// Cut arrow the player must match for <see cref="DirectedDesktopSliceInput"/> (desktop keyboard cuts).
/// Mirrors <see cref="NotesSpawner.CutDirection"/> for this note instance.
/// </summary>
public class SpawnedNoteCutDirection : MonoBehaviour
{
    public NotesSpawner.CutDirection CutDirection;
}
