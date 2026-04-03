using UnityEngine;

/// <summary>
/// Builds emissive blue edge strips on the player platform (top face perimeter).
/// </summary>
public class PlatformNeonTrim : MonoBehaviour
{
    [SerializeField] float stripWidth = 0.062f;
    [SerializeField] float stripRaise = 0.004f;
    [SerializeField] Color emissionColor = new Color(0.25f, 0.75f, 2.2f, 1f);

    static Material s_sharedTrimMat;

    void Awake()
    {
        RebuildTrim();
    }

    void RebuildTrim()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (c.name.StartsWith("TrimStrip") || c.name.StartsWith("TrimPost"))
                Destroy(c.gameObject);
        }

        if (s_sharedTrimMat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null)
                sh = Shader.Find("Standard");
            if (sh == null)
                return;
            s_sharedTrimMat = new Material(sh);
            s_sharedTrimMat.SetColor("_BaseColor", new Color(0.02f, 0.04f, 0.1f, 1f));
            s_sharedTrimMat.SetFloat("_Metallic", 0.15f);
            s_sharedTrimMat.SetFloat("_Smoothness", 0.55f);
            s_sharedTrimMat.EnableKeyword("_EMISSION");
        }

        s_sharedTrimMat.SetColor("_EmissionColor", emissionColor);

        Vector3 s = transform.localScale;
        float hx = 0.5f * s.x;
        float hz = 0.5f * s.z;
        float hy = 0.5f * s.y;
        float y = hy + stripRaise + stripWidth * 0.5f;

        AddStrip("Z+", new Vector3(0f, y, hz), new Vector3(2f * hx, stripWidth, stripWidth));
        AddStrip("Z-", new Vector3(0f, y, -hz), new Vector3(2f * hx, stripWidth, stripWidth));
        AddStrip("X+", new Vector3(hx, y, 0f), new Vector3(stripWidth, stripWidth, 2f * hz));
        AddStrip("X-", new Vector3(-hx, y, 0f), new Vector3(stripWidth, stripWidth, 2f * hz));

        // Vertical corner posts (full outline in perspective when the slab is thin).
        float inset = stripWidth * 0.5f;
        float postHalfY = hy + stripWidth * 5f;
        AddCornerPost("NN", new Vector3(-hx + inset, 0f, -hz + inset), postHalfY, stripWidth);
        AddCornerPost("NP", new Vector3(-hx + inset, 0f, hz - inset), postHalfY, stripWidth);
        AddCornerPost("PN", new Vector3(hx - inset, 0f, -hz + inset), postHalfY, stripWidth);
        AddCornerPost("PP", new Vector3(hx - inset, 0f, hz - inset), postHalfY, stripWidth);
    }

    void AddStrip(string suffix, Vector3 localPos, Vector3 localScale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "TrimStrip_" + suffix;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        var col = go.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = s_sharedTrimMat;
    }

    void AddCornerPost(string suffix, Vector3 localCenterXZ, float halfHeightY, float w)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "TrimPost_" + suffix;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(localCenterXZ.x, localCenterXZ.y, localCenterXZ.z);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(w, 2f * halfHeightY, w);
        var col = go.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = s_sharedTrimMat;
    }
}
