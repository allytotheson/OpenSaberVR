using EzySlice;
using UnityEngine;

public class Slice : MonoBehaviour
{
    Material mat;

    private void Start()
    {
        mat = mat = GetComponent<Renderer>().material;
    }

    public GameObject[] SliceObject(GameObject obj, Material crossSectionMaterial = null)
    {
        Material use = crossSectionMaterial != null ? crossSectionMaterial : mat;
        if (use == null)
        {
            var r = GetComponent<Renderer>();
            if (r != null)
                use = r.sharedMaterial;
        }

        return obj.SliceInstantiate(transform.position, transform.up, use);
    }

#if UNITY_EDITOR
    public void OnDrawGizmos()
    {
        EzySlice.Plane cuttingPlane = new EzySlice.Plane(transform.position, transform.up);
        cuttingPlane.Compute(transform);
        cuttingPlane.OnDebugDraw();
    }

#endif
}
