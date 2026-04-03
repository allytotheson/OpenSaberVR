using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Faint hover feedback for world-space UI (works with GraphicRaycaster + pointer events).
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButtonHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] float hoverScale = 1.045f;
    [SerializeField] float lerpSpeed = 12f;

    Button _button;
    RectTransform _rt;
    Image _image;
    Vector3 _baseScale;
    Color _baseImageColor;
    bool _hover;
    bool _hasImage;

    void Awake()
    {
        _button = GetComponent<Button>();
        _rt = transform as RectTransform;
        _baseScale = _rt != null ? _rt.localScale : Vector3.one;
        _image = GetComponent<Image>();
        if (_image != null)
        {
            _hasImage = true;
            _baseImageColor = _image.color;
        }
    }

    void OnDisable()
    {
        _hover = false;
        ApplyVisuals(0f);
    }

    void Update()
    {
        float target = _hover && _button != null && _button.interactable ? 1f : 0f;
        float w = Mathf.MoveTowards(CurrentBlend(), target, Time.unscaledDeltaTime * lerpSpeed);
        ApplyVisuals(w);
    }

    float CurrentBlend()
    {
        if (_rt == null) return 0f;
        float z = _baseScale.x > 0.0001f ? _rt.localScale.x / _baseScale.x : 1f;
        return Mathf.InverseLerp(1f, hoverScale, z);
    }

    void ApplyVisuals(float blend)
    {
        if (_rt != null)
            _rt.localScale = Vector3.Lerp(_baseScale, _baseScale * hoverScale, blend);
        if (_hasImage)
        {
            var c = Color.Lerp(_baseImageColor, _button.colors.highlightedColor, blend * 0.55f);
            _image.color = c;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button != null && _button.interactable)
            _hover = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hover = false;
    }
}
