using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modulates <c>_EmissionColor</c> / URP emissive each frame: <c>base * (1 + sin(t) * depth)</c>.
/// </summary>
[DisallowMultipleComponent]
public sealed class DesktopImportedBladeEmissionPulse : MonoBehaviour
{
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

    public float frequencyHz = 2f;

    [Range(0f, 1f)]
    [Tooltip("How much emission swings around its base (0 = none).")]
    public float depth = 0.28f;

    readonly List<Material> _materials = new List<Material>(8);
    readonly List<Color> _baseEmission = new List<Color>(8);

    public void RebuildFromRenderers()
    {
        _materials.Clear();
        _baseEmission.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null)
                continue;
            foreach (var m in r.materials)
            {
                if (m == null)
                    continue;
                if (!m.HasProperty(EmissionColorId) && !m.HasProperty(EmissiveColorId))
                    continue;
                Color c = m.HasProperty(EmissionColorId)
                    ? m.GetColor(EmissionColorId)
                    : m.GetColor(EmissiveColorId);
                _materials.Add(m);
                _baseEmission.Add(c);
            }
        }
    }

    void OnEnable()
    {
        if (_materials.Count == 0)
            RebuildFromRenderers();
    }

    void Update()
    {
        if (_materials.Count == 0 || frequencyHz <= 0f)
            return;

        float w = Time.time * (Mathf.PI * 2f * frequencyHz);
        float mul = 1f + Mathf.Sin(w) * depth;

        for (int i = 0; i < _materials.Count; i++)
        {
            var m = _materials[i];
            if (m == null)
                continue;
            Color b = _baseEmission[i];
            Color v = b * mul;
            if (m.HasProperty(EmissionColorId))
                m.SetColor(EmissionColorId, v);
            if (m.HasProperty(EmissiveColorId))
                m.SetColor(EmissiveColorId, v);
        }
    }
}
