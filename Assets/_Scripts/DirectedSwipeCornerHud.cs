using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Four screen-corner dots (Q left-down, Z left-up, O right-down, M right-up) that flash when that directed swipe registers a hit.
/// </summary>
public sealed class DirectedSwipeCornerHud : MonoBehaviour
{
    const int SortOrder = 175;

    static DirectedSwipeCornerHud _instance;

    Image[] _dots;
    Color[] _idleColors;
    Coroutine[] _flashRoutines;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureBootstrap()
    {
        if (FindAnyObjectByType<DirectedSwipeCornerHud>(FindObjectsInactive.Include) != null)
            return;
        var go = new GameObject(nameof(DirectedSwipeCornerHud));
        DontDestroyOnLoad(go);
        go.AddComponent<DirectedSwipeCornerHud>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        Build();
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortOrder;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        gameObject.AddComponent<GraphicRaycaster>();

        var group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        Sprite circle = CreateCircleSprite(22);

        _dots = new Image[4];
        _idleColors = new Color[4];
        _flashRoutines = new Coroutine[4];

        float margin = 22f;
        float size = 18f;

        // 0 bottom-left (Q), 1 top-left (Z), 2 bottom-right (O), 3 top-right (M)
        SetupDot(0, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(margin, margin), size, circle, new Color(0.92f, 0.28f, 0.38f, 0.35f));
        SetupDot(1, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(margin, -margin), size, circle, new Color(0.92f, 0.28f, 0.38f, 0.35f));
        SetupDot(2, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-margin, margin), size, circle, new Color(0.22f, 0.58f, 1f, 0.35f));
        SetupDot(3, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-margin, -margin), size, circle, new Color(0.22f, 0.58f, 1f, 0.35f));
    }

    void SetupDot(int i, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, float size, Sprite circle, Color idle)
    {
        var go = new GameObject($"SwipeDot_{i}");
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);

        var img = go.AddComponent<Image>();
        img.sprite = circle;
        img.raycastTarget = false;
        img.color = idle;

        _idleColors[i] = idle;
        _dots[i] = img;
    }

    static Sprite CreateCircleSprite(int resolution)
    {
        var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float cx = (resolution - 1) * 0.5f;
        float cy = (resolution - 1) * 0.5f;
        float r = resolution * 0.48f;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = Mathf.Clamp01(1f - (d - r + 0.5f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Maps to corner: left+down → BL, left+up → TL, right+down → BR, right+up → TR.</summary>
    public static void FlashRegisteredSwipe(bool leftHand, bool cutUp)
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (GameplayCameraEnsurer.IsXrDeviceActive())
            return;
#endif
        if (_instance == null)
            return;

        int idx = (leftHand ? 0 : 2) + (cutUp ? 1 : 0);
        _instance.FlashCorner(idx);
    }

    void FlashCorner(int index)
    {
        if (_dots == null || index < 0 || index >= _dots.Length)
            return;

        Image img = _dots[index];
        if (img == null)
            return;

        if (_flashRoutines[index] != null)
            StopCoroutine(_flashRoutines[index]);
        _flashRoutines[index] = StartCoroutine(FlashCoroutine(img, index));
    }

    IEnumerator FlashCoroutine(Image img, int index)
    {
        Color idle = _idleColors[index];
        Color peak = new Color(Mathf.Min(1f, idle.r * 1.15f + 0.35f), Mathf.Min(1f, idle.g * 1.1f + 0.45f), Mathf.Min(1f, idle.b * 1.1f + 0.4f), 0.98f);
        float up = 0.06f;
        float hold = 0.12f;
        float down = 0.22f;

        float t = 0f;
        while (t < up)
        {
            t += Time.unscaledDeltaTime;
            img.color = Color.Lerp(idle, peak, Mathf.Clamp01(t / up));
            yield return null;
        }

        img.color = peak;
        yield return new WaitForSecondsRealtime(hold);

        t = 0f;
        while (t < down)
        {
            t += Time.unscaledDeltaTime;
            img.color = Color.Lerp(peak, idle, Mathf.Clamp01(t / down));
            yield return null;
        }

        img.color = idle;
        _flashRoutines[index] = null;
    }
}
