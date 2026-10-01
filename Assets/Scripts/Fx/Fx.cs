using System.Collections.Generic;
using UnityEngine;

// Short-lived feedback: square pixel particles, rings, floating numbers, chain lightning, lingering ground fire.
// Everything is pooled under the battle root and cleared when the battle is destroyed.
public static class Fx
{
    static Transform root;
    static readonly Stack<PixelParticle> pool = new();

    public static void Bind(Transform battleRoot)
    {
        root = battleRoot;
        pool.Clear();
    }

    static Transform Root => root ? root : (root = new GameObject("Fx").transform);

    // ---------------------------------------------------------------- particles

    public static void Burst(Vector2 at, Color color, int count, float speed, float life, float gravity = -6f, float spread = 1f, Vector2 bias = default, int size = 1)
    {
        for (int i = 0; i < count; i++)
        {
            var p = pool.Count > 0 ? pool.Pop() : null;
            if (!p)
            {
                var sr = Gfx.Renderer(Root, "P", Gfx.Dot, at, Gfx.OrderFx);
                p = sr.gameObject.AddComponent<PixelParticle>();
                p.sr = sr;
            }
            p.gameObject.SetActive(true);
            var dir = Random.insideUnitCircle;
            dir.y = Mathf.Abs(dir.y) * spread + (1 - spread) * dir.y;
            p.Launch(at, (dir * speed) + bias * speed, color, life * Random.Range(0.7f, 1.2f), gravity, size);
        }
    }

    internal static void Return(PixelParticle p)
    {
        p.gameObject.SetActive(false);
        pool.Push(p);
    }

    public static void Sparks(Vector2 at, Color c) => Burst(at, c, 6, 2.4f, 0.28f, -4f);
    public static void Blood(Vector2 at, float amount = 1) => Burst(at, new Color(0.55f, 0.06f, 0.06f), Mathf.RoundToInt(7 * amount), 2.2f, 0.45f, -9f, 0.8f);
    public static void Dust(Vector2 at, float amount = 1) => Burst(at, new Color(0.42f, 0.36f, 0.32f), Mathf.RoundToInt(10 * amount), 1.6f, 0.5f, -2f, 0.9f, new Vector2(0, 0.3f));
    public static void Embers(Vector2 at, float amount = 1) => Burst(at, new Color(1f, 0.55f, 0.95f), Mathf.RoundToInt(8 * amount), 1.4f, 0.7f, 2.5f, 1f);
    public static void Souls(Vector2 at, Color c) => Burst(at, c, 8, 0.8f, 1.0f, 3.5f, 1f, new Vector2(0, 0.6f));

    // ---------------------------------------------------------------- shapes

    public static void Ring(Vector2 at, float radius, Color color, float seconds = 0.3f, int order = Gfx.OrderFx)
    {
        var go = new GameObject("Ring");
        go.transform.SetParent(Root, false);
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Gfx.Circle(radius, new Color(color.r, color.g, color.b, 0.22f), color);
        sr.sortingOrder = order;
        go.AddComponent<FadeScale>().Set(seconds, 0.55f, 1f, sr);
    }

    public static void Flash(Vector2 at, float radius, Color color, float seconds = 0.12f)
    {
        var go = new GameObject("Flash");
        go.transform.SetParent(Root, false);
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Gfx.Blob(Mathf.Max(3, Mathf.RoundToInt(radius * Gfx.PPU)));
        sr.color = color;
        sr.sortingOrder = Gfx.OrderFx;
        go.AddComponent<FadeScale>().Set(seconds, 1f, 1.2f, sr);
    }

    // Sword trail of a heavy swing, mirrored for facing.
    public static void Slash(Vector2 at, bool facingLeft, float radius, Color color)
    {
        var go = new GameObject("Slash");
        go.transform.SetParent(Root, false);
        go.transform.position = at;
        go.transform.localScale = new Vector3(facingLeft ? -1 : 1, 0.8f, 1);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Gfx.Crescent(Mathf.RoundToInt(radius * Gfx.PPU));
        sr.color = color;
        sr.sortingOrder = Gfx.OrderFx + 2;
        go.AddComponent<FadeScale>().Set(0.16f, 0.95f, 1.1f, sr);
    }

    public static void Text(Vector2 at, string text, Color color, float scale = 1f)
    {
        var go = new GameObject("FloatText");
        go.transform.SetParent(Root, false);
        go.transform.position = at;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.color = color;
        tm.anchor = TextAnchor.LowerCenter;
        tm.characterSize = 0.06f * scale;
        tm.fontSize = 48;
        tm.font = UiFonts.Body;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = tm.font.material;
        mr.sortingOrder = Gfx.OrderBars + 5;
        go.AddComponent<FloatText>();
    }

    // Jagged line made of pixels between points (Blood Obelisk chains).
    public static void Lightning(Vector2 a, Vector2 b, Color color, float seconds = 0.18f)
    {
        var go = new GameObject("Bolt");
        go.transform.SetParent(Root, false);
        var parts = new List<SpriteRenderer>();
        Vector2 prev = a;
        int steps = Mathf.Max(3, Mathf.RoundToInt(Vector2.Distance(a, b) * 4));
        var n = Vector2.Perpendicular((b - a).normalized);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            var p = Vector2.Lerp(a, b, t) + n * (i == steps ? 0 : Random.Range(-0.18f, 0.18f));
            var sr = Gfx.Renderer(go.transform, "Seg", Gfx.Pixel, prev, Gfx.OrderFx + 1);
            var d = p - prev;
            sr.transform.right = d;
            sr.transform.localScale = new Vector3(d.magnitude * Gfx.PPU, 2, 1);
            sr.color = color;
            parts.Add(sr);
            prev = p;
        }
        go.AddComponent<FadeGroup>().Set(seconds, parts);
    }

    public static void GroundFire(Vector2 at, float radius, float dps, float seconds, string source)
    {
        var go = new GameObject("GroundFire");
        go.transform.SetParent(Root, false);
        go.transform.position = at;
        go.AddComponent<GroundFire>().Init(radius, dps, seconds, source);
    }

    public static void Clear()
    {
        pool.Clear();
        root = null;
    }
}

public class PixelParticle : MonoBehaviour
{
    public SpriteRenderer sr;
    Vector2 v, pos;
    float life, age, gravity;
    Color c;

    public void Launch(Vector2 at, Vector2 velocity, Color color, float seconds, float g, int size)
    {
        pos = at;
        transform.position = at;
        transform.localScale = Vector3.one * size;
        v = velocity;
        life = seconds;
        age = 0;
        gravity = g;
        c = color;
        sr.color = c;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        v.y += gravity * dt;
        v *= 1 - 1.5f * dt;
        pos += v * dt;
        transform.position = Gfx.Snap(pos);   // particles live on the pixel grid like the sprites
        sr.color = new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01((1 - age / life) * 1.4f));
        if (age >= life) Fx.Return(this);
    }
}

public class FadeScale : MonoBehaviour
{
    float t, seconds, from, to;
    SpriteRenderer sr;
    Color c;

    Vector3 baseScale;

    public void Set(float s, float f, float tt, SpriteRenderer r)
    {
        seconds = s;
        from = f;
        to = tt;
        sr = r;
        c = r.color;
        baseScale = transform.localScale;
    }

    void Update()
    {
        t += Time.deltaTime / seconds;
        transform.localScale = baseScale * Mathf.Lerp(from, to, t);
        sr.color = new Color(c.r, c.g, c.b, c.a * (1 - t));
        if (t >= 1) Destroy(gameObject);
    }
}

public class FadeGroup : MonoBehaviour
{
    float t, seconds;
    List<SpriteRenderer> parts;

    public void Set(float s, List<SpriteRenderer> p)
    {
        seconds = s;
        parts = p;
    }

    void Update()
    {
        t += Time.deltaTime / seconds;
        foreach (var sr in parts)
        {
            var c = sr.color;
            c.a = 1 - t;
            sr.color = c;
        }
        if (t >= 1) Destroy(gameObject);
    }
}

public class FloatText : MonoBehaviour
{
    float t;
    TextMesh tm;
    Color c;

    void Start()
    {
        tm = GetComponent<TextMesh>();
        c = tm.color;
    }

    void Update()
    {
        t += Time.deltaTime;
        transform.position += Vector3.up * (Time.deltaTime * 0.9f);
        tm.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(1.4f - t * 1.6f));
        if (t > 0.9f) Destroy(gameObject);
    }
}

// Pyre tier 4: burning ground left by the impact. Damages enemies standing in it.
public class GroundFire : MonoBehaviour
{
    float radius, dps, left, tick, seconds;
    string source;
    SpriteRenderer sr;

    public void Init(float r, float d, float s, string src)
    {
        radius = r;
        dps = d;
        left = seconds = s;
        source = src;
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = Gfx.Blob(Mathf.RoundToInt(r * Gfx.PPU));
        sr.color = new Color(0.55f, 0.15f, 0.7f, 0.35f);
        sr.sortingOrder = Gfx.OrderDecal + 2;
        transform.localScale = new Vector3(1, 0.55f, 1);
        var l = Gfx.Light(transform, Vector2.zero, new Color(0.75f, 0.3f, 1f), r * 1.6f, 0.6f);
        l.gameObject.AddComponent<Flicker>().amount = 0.5f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        left -= dt;
        sr.color = new Color(0.55f, 0.15f, 0.7f, 0.35f * Mathf.Clamp01(left / 0.6f));
        if ((tick -= dt) <= 0)
        {
            tick = 0.25f;
            var b = Battle.I;
            if (b)
                foreach (var e in b.EnemiesIn(transform.position, radius))
                    e.Damage(new DamageInfo(dps * 0.25f, DamageType.Fire, source));
            if (Random.value < 0.6f) Fx.Embers((Vector2)transform.position + Random.insideUnitCircle * radius * 0.6f, 0.3f);
        }
        if (left <= 0) Destroy(gameObject);
    }
}
