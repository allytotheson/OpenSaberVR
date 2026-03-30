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

    void Awake()
    {
        if (GetComponentInChildren<Collider>(true) != null)
            return;
        var mf = GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            var mc = gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true;
        }
        if (GetComponentInChildren<Collider>(true) == null)
            AddBoxFromRendererBounds();
    }

    void AddBoxFromRendererBounds()
    {
        var r = GetComponentInChildren<Renderer>();
        if (r == null)
            return;
        Bounds b = r.bounds;
        var box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(b.center);
        Vector3 scale = transform.lossyScale;
        box.size = new Vector3(
            b.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            b.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
            b.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
    }

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
