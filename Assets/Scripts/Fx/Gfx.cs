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
    // Scarlet war banner (Veterano Escarlate's power): dark pole, crimson swallowtail cloth, pale crest. Pivot at the foot.
    public static Sprite WarBanner => Make(16, 46, new Vector2(0.25f, 0), (x, y) =>
    {
        Color32 none = new(0, 0, 0, 0), ink = new(11, 10, 13, 255);
        if (x >= 3 && x <= 4 && y <= 44) return x == 3 ? new Color32(74, 52, 38, 255) : new Color32(46, 32, 26, 255);   // pole
        if (y == 44 && x >= 2 && x <= 5) return new Color32(200, 170, 110, 255);                                       // finial
        int top = 42, bottom = 20, notch = 6 - Mathf.Abs(x - 10);                                                       // swallowtail
        if (x < 5 || x > 15 || y > top || y < bottom + Mathf.Max(0, notch)) return none;
        if (x == 15 || y == top || y == bottom + Mathf.Max(0, notch)) return ink;
        bool crest = (x == 10 && y >= 28 && y <= 37) || (y == 34 && x >= 8 && x <= 12);
        if (crest) return new Color32(226, 210, 176, 255);
        bool fold = (x + y) % 7 == 0;
        return fold ? new Color32(110, 18, 22, 255) : x < 8 ? new Color32(150, 28, 30, 255) : new Color32(176, 36, 36, 255);
    }, "warbanner");

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

    // Seats a building on the terrain: a patch of trodden dark earth with loose rubble around its base plus a contact
    // shadow, both centred on the base's ground diamond (the sprite pivot is its front corner, so the centre is
    // ~a quarter of the width above it). width = the building's base width in units.
    public static void Grounding(Transform parent, float width, int seed = 0)
    {
        float lift = width * 0.22f;
        int w = Mathf.Max(8, Mathf.RoundToInt(width * 1.4f * PPU)), h = Mathf.Max(5, Mathf.RoundToInt(w * 0.52f));
        Renderer(parent, "Footing", Footing(w, h, seed), new Vector2(0, lift), OrderDecal + 1);
        int sw = Mathf.Max(6, Mathf.RoundToInt(width * 1.12f * PPU));
        var s = Ellipse(sw, Mathf.Max(3, Mathf.RoundToInt(sw * 0.46f)), new Color32(0, 0, 0, 120), new Color32(0, 0, 0, 70));
        Renderer(parent, "Shadow", s, new Vector2(0, lift), OrderShadow);
    }

    static Sprite Footing(int w, int h, int seed) => Make(w, h, new Vector2(0.5f, 0.5f), (x, y) =>
    {
        float dx = (x + 0.5f - w / 2f) / (w / 2f), dy = (y + 0.5f - h / 2f) / (h / 2f);
        float ang = Mathf.Atan2(dy, dx);
        float wobble = 1 + 0.08f * Mathf.Sin(ang * 3 + seed) + 0.05f * Mathf.Sin(ang * 7 + seed * 2.3f);
        float d = Mathf.Sqrt(dx * dx + dy * dy) / wobble;
        if (d > 1) return new Color32(0, 0, 0, 0);
        uint hsh = (uint)((x * 73856093) ^ (y * 19349663) ^ (seed * 83492791)) % 1000;
        if (d > 0.62f && hsh < 55)                                              // loose rubble around the rim
            return hsh < 18 ? new Color32(118, 110, 112, 255) : new Color32(70, 64, 68, 255);
        int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        float edge = Mathf.InverseLerp(1f, 0.7f, d);                             // dithered fade to the terrain
        if (edge < bayer[(y & 3) * 4 + (x & 3)] / 16f) return new Color32(0, 0, 0, 0);
        byte a = (byte)Mathf.Lerp(70, 135, Mathf.InverseLerp(1f, 0.3f, d));
        return hsh > 940 ? new Color32(48, 40, 42, a) : new Color32(24, 19, 22, a);
    }, $"foot{w}x{h}s{seed}");

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
// Animated torch flame (cutout, like the rigs): the flame sprite stretches, breathes and leans around its foot,
// and an ember rises now and then. The flame sprite shares the torch's canvas; its foot is found from its pixels.
public class TorchFlame : MonoBehaviour
{
    float seed, ember;
    Vector2 tip;

    public static void Attach(Transform torch, Sprite flame, bool flip)
    {
        var px = flame.texture.isReadable ? flame.texture.GetPixels32() : null;
        int w = flame.texture.width, foot = 0, top = 0;
        if (px != null)
        {
            foot = int.MaxValue;
            for (int i = 0; i < px.Length; i++)
                if (px[i].a > 0)
                {
                    foot = Mathf.Min(foot, i / w);
                    top = Mathf.Max(top, i / w);
                }
            if (foot == int.MaxValue) foot = 0;
        }
        var pivot = new GameObject("Flame").transform;
        pivot.SetParent(torch, false);
        pivot.localPosition = new Vector2(0, (foot - flame.pivot.y) / Gfx.PPU);
        var sr = Gfx.Renderer(pivot, "Sprite", flame, -(Vector2)pivot.localPosition, Gfx.OrderWorld);
        sr.flipX = flip;
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        // sort with the torch (same feet), drawn just over it
        var group = torch.gameObject.GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (!group) group = torch.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
        group.sortingOrder = torch.GetComponent<SpriteRenderer>().sortingOrder;
        sr.sortingOrder = 1;
        var f = pivot.gameObject.AddComponent<TorchFlame>();
        f.tip = new Vector2(0, (top - foot + 1) / (float)Gfx.PPU);
    }

    void Start() => seed = Random.value * 100;

    void Update()
    {
        float t = Time.time * 5.5f + seed;
        float n1 = Mathf.PerlinNoise(t, 0.37f), n2 = Mathf.PerlinNoise(0.71f, t * 1.3f);
        transform.localScale = new Vector3(0.9f + 0.14f * n2, 0.84f + 0.3f * n1, 1);
        transform.localRotation = Quaternion.Euler(0, 0, (n2 - 0.5f) * 12f);
        if ((ember -= Time.deltaTime) > 0) return;
        ember = Random.Range(0.6f, 1.6f);
        Fx.Burst((Vector2)transform.TransformPoint(tip * 0.7f), new Color(1f, 0.62f, 0.25f), 1, 0.35f, 0.9f, 1.4f);
    }
}

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
