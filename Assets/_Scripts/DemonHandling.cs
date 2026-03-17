/*
 * Movement logic for demons (replaces CubeHandling for demon prefabs).
 * Same BPM-synced movement as cubes - spawner drives AnticipationPosition, Speed, WarmUpPosition.
 */
using UnityEngine;

public class DemonHandling : MonoBehaviour
{
    public float AnticipationPosition;
    public float Speed;
    public double WarmUpPosition;

    void LateUpdate()
    {
        if (transform.position.z < AnticipationPosition)
        {
            var newPositionZ = transform.position.z + BeatsConstants.BEAT_WARMUP_SPEED * (Time.deltaTime / 1000);
            if (newPositionZ < AnticipationPosition)
            {
                transform.position = new Vector3(transform.position.x, transform.position.y, newPositionZ);
            }
            else
            {
                transform.position = new Vector3(transform.position.x, transform.position.y, AnticipationPosition);
            }
        }
        else
        {
            transform.position -= transform.forward * Speed * (Time.deltaTime);
        }
    }
}
