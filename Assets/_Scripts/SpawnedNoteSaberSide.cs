using UnityEngine;

/// <summary>
/// Set on spawned notes so desktop auto-slice knows which saber (left/red vs right/blue) should register the hit.
/// </summary>
public class SpawnedNoteSaberSide : MonoBehaviour
{
    public bool isLeftHandSaber;
}
