using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Runtime-made overlay sprites (range circles, shadows, bars) and small factory helpers.
// World sprites share sorting order 0 and are Y-sorted by the 2D renderer (custom axis 0,1,0, pivot at the feet).
public static class Gfx
{
    public const int PPU = 32;
    public const int OrderGround = -100, OrderRoad = -99, OrderRoadEdge = -98, OrderDecal = -90, OrderShadow = -60, OrderOverlay = -50,
        OrderWorld = 0, OrderProjectile = 50, OrderFx = 60, OrderBars = 100, OrderGhost = 200;

    public static readonly Color EnemyHp = new(0.62f, 0.1f, 0.1f), EliteHp = new(0.85f, 0.45f, 0.1f),
        CommanderHp = new(0.95f, 0.64f, 0.23f), Trail = new(0.95f, 0.85f, 0.6f);

    static readonly Dictionary<string, Sprite> cache = new();

    static Sprite Make(int w, int h, Vector2 pivot, System.Func<int, int, Color32> paint, string key)
    {
        if (cache.TryGetValue(key, out var s) && s) return s;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = paint(x, y);
        t.SetPixels32(px);
        t.Apply();
        return cache[key] = Sprite.Create(t, new Rect(0, 0, w, h), pivot, PPU);
    }

    // Pixel-exact disc: faint fill and a 1 px rim. radius in world units.
    public static Sprite Circle(float radius, Color32 fill, Color32 rim)
    {
        int r = Mathf.Max(2, Mathf.RoundToInt(radius * PPU)), n = 2 * r + 1;
        float outer = (r + 0.5f) * (r + 0.5f), inner = (r - 0.5f) * (r - 0.5f);
        return Make(n, n, new Vector2(0.5f, 0.5f), (x, y) =>
        {
            float d = (x - r) * (x - r) + (y - r) * (y - r);
            return d > outer ? new Color32(0, 0, 0, 0) : d > inner ? rim : fill;
        }, $"c{r}{fill}{rim}");
    }

    // Dashed ring (range preview), so overlapping ranges stay readable.
    public static Sprite DashedCircle(float radius, Color32 rim, Color32 fill)
    {
        int r = Mathf.Max(2, Mathf.RoundToInt(radius * PPU)), n = 2 * r + 1;
        float outer = (r + 0.5f) * (r + 0.5f), inner = (r - 0.5f) * (r - 0.5f);
        return Make(n, n, new Vector2(0.5f, 0.5f), (x, y) =>
        {
            float d = (x - r) * (x - r) + (y - r) * (y - r);
            if (d > outer) return new Color32(0, 0, 0, 0);
            if (d <= inner) return fill;
            float a = Mathf.Atan2(y - r, x - r) * r;
            return ((int)Mathf.Floor(a / 3f) & 1) == 0 ? rim : fill;
        }, $"dc{r}{fill}{rim}");
    }

    // Ground ellipse in pixels (shadows, selection rings).
    public static Sprite Ellipse(int w, int h, Color32 fill, Color32 rim)
    {
        float a = w / 2f, b = h / 2f;
        return Make(w, h, new Vector2(0.5f, 0.5f), (x, y) =>
        {
            float dx = (x + 0.5f - a) / a, dy = (y + 0.5f - b) / b, d = dx * dx + dy * dy;
            float edge = 1 - 2.2f / Mathf.Min(w, h);
            return d > 1 ? new Color32(0, 0, 0, 0) : d > edge * edge ? rim : fill;
        }, $"e{w}x{h}{fill}{rim}");
    }

    public static Sprite Pixel => Make(1, 1, new Vector2(0, 0.5f), (_, _) => new Color32(255, 255, 255, 255), "px");

    // Downward crescent (sword trail): thick in the middle of the arc, tapering at both ends. Faces right.
    public static Sprite Crescent(int r) => Make(2 * r + 2, 2 * r + 2, new Vector2(0.5f, 0.5f), (x, y) =>
    {
        float dx = x - r - 0.5f, dy = y - r - 0.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
        float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;          // +90 top, -90 bottom
        if (a < -80 || a > 95) return new Color32(0, 0, 0, 0);
        float t = Mathf.InverseLerp(95, -80, a);                 // 0 at the start (top), 1 at the end
        float thick = Mathf.Lerp(1, r * 0.28f, Mathf.Sin(t * Mathf.PI));
        if (d > r || d < r - thick) return new Color32(0, 0, 0, 0);
        byte alpha = (byte)(255 * Mathf.Clamp01(0.35f + t * 0.8f));
        return d > r - 1.5f ? new Color32(255, 255, 255, alpha) : new Color32(220, 210, 190, (byte)(alpha * 0.7f));
    }, $"cres{r}");
    public static Sprite Dot => Make(1, 1, new Vector2(0.5f, 0.5f), (_, _) => new Color32(255, 255, 255, 255), "dot");

    // Small diamond, white inside so SpriteRenderer.color tints it.
    public static Sprite Pip => Make(5, 5, new Vector2(0.5f, 0.5f), (x, y) =>
    {
        int d = Mathf.Abs(x - 2) + Mathf.Abs(y - 2);
        return d > 2 ? new Color32(0, 0, 0, 0) : d == 2 ? new Color32(11, 10, 13, 255) : new Color32(255, 255, 255, 255);
    }, "pip");

    // Soft-edged blob (dithered) used for fog, glows and ground fire.
    public static Sprite Blob(int r) => Make(2 * r, 2 * r, new Vector2(0.5f, 0.5f), (x, y) =>
    {
        float d = Mathf.Sqrt((x - r + 0.5f) * (x - r + 0.5f) + (y - r + 0.5f) * (y - r + 0.5f)) / r;
        int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        float th = bayer[(y & 3) * 4 + (x & 3)] / 16f;
        return 1 - d > th * 0.9f + 0.05f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
    }, $"blob{r}");

    public static SpriteRenderer Renderer(Transform parent, string name, Sprite s, Vector2 localPos, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = order;
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        return sr;
    }

    public static SpriteRenderer Shadow(Transform parent, float width)
    {
        int w = Mathf.Max(6, Mathf.RoundToInt(width * PPU));
        var s = Ellipse(w, Mathf.Max(3, w / 3), new Color32(0, 0, 0, 110), new Color32(0, 0, 0, 60));
        return Renderer(parent, "Shadow", s, Vector2.zero, OrderShadow);
    }

    public static Light2D Light(Transform parent, Vector2 localPos, Color color, float radius, float intensity)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.pointLightOuterRadius = radius;
        l.pointLightInnerRadius = radius * 0.15f;
        l.falloffIntensity = 0.65f;
        return l;
    }

    public static Vector2 Snap(Vector2 p) => new(Mathf.Round(p.x * PPU) / PPU, Mathf.Round(p.y * PPU) / PPU);
}

// World-space HP bar: the fill drops at once, the pale "recent loss" trail slides down after it.
public class Bar
{
    const float Height = 3f / Gfx.PPU;
    public readonly GameObject root;
    readonly Transform fill, trail;
    readonly SpriteRenderer fillSr;
    readonly float width;
    float shown = 1, target = 1, hold;

    public Bar(Transform parent, Vector2 localPos, float width, Color color)
    {
        this.width = width;
        root = new GameObject("Bar");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        var bg = Gfx.Renderer(root.transform, "Bg", Gfx.Pixel, new Vector2(-width / 2 - 1f / Gfx.PPU, 0), Gfx.OrderBars);
        bg.color = new Color(0.04f, 0.04f, 0.05f, 0.92f);
        bg.transform.localScale = new Vector3((width + 2f / Gfx.PPU) * Gfx.PPU, (Height + 2f / Gfx.PPU) * Gfx.PPU, 1);
        var t = Gfx.Renderer(root.transform, "Trail", Gfx.Pixel, new Vector2(-width / 2, 0), Gfx.OrderBars + 1);
        t.color = Gfx.Trail;
        trail = t.transform;
        fillSr = Gfx.Renderer(root.transform, "Fill", Gfx.Pixel, new Vector2(-width / 2, 0), Gfx.OrderBars + 2);
        fillSr.color = color;
        fill = fillSr.transform;
        Set(1, true);
    }

    public void SetColor(Color c) => fillSr.color = c;

    public void Set(float fraction, bool instant = false)
    {
        fraction = Mathf.Clamp01(fraction);
        if (fraction < target) hold = 0.25f;
        target = fraction;
        if (instant) shown = fraction;
        Apply();
    }

    public void Tick(float dt)
    {
        if (shown <= target) shown = target;
        else if ((hold -= dt) <= 0) shown = Mathf.MoveTowards(shown, target, dt / 0.4f);
        Apply();
    }

    void Apply()
    {
        fill.localScale = new Vector3(width * target * Gfx.PPU, Height * Gfx.PPU, 1);
        trail.localScale = new Vector3(width * shown * Gfx.PPU, Height * Gfx.PPU, 1);
    }
}

// Torch-like flicker for point lights.
public class Flicker : MonoBehaviour
{
    UnityEngine.Rendering.Universal.Light2D l;
    float baseIntensity, seed;
    public float amount = 0.36f, speed = 4f;

    void Start()
    {
        l = GetComponent<UnityEngine.Rendering.Universal.Light2D>();
        baseIntensity = l.intensity;
        seed = Random.value * 100;
    }

    void Update() => l.intensity = baseIntensity * (1 - amount / 2 + amount * Mathf.PerlinNoise(Time.time * speed, seed));
}
