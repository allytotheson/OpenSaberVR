using UnityEngine;

/// <summary>Subtle scale pulse for world-space TMP title text.</summary>
public class NeonLogoPulse : MonoBehaviour
{
    [SerializeField] float amount = 0.035f;
    [SerializeField] float speed = 2.1f;

    Vector3 _base;

    void Awake()
    {
        _base = transform.localScale;
    }

    void Update()
    {
        float w = 1f + amount * Mathf.Sin(Time.unscaledTime * speed);
        transform.localScale = _base * w;
    }
}
