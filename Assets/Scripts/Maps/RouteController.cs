using System.Collections.Generic;
using UnityEngine;

// Logical road: every RoutePath becomes a centripetal Catmull-Rom spline sampled every 5 cm, with normals for lanes.
// A distance field over the map answers "how far from the road is this point" in O(1) (building, painting, traps).
public class RouteController
{
    public const float Step = 0.05f;
    public static readonly Rect Extents = Rect.MinMaxRect(-17f, -10.5f, 17f, 10.5f);
    const float FieldCell = 0.0625f, FieldMax = 3f;

    public readonly RouteData data;
    public readonly float halfWidth;
    readonly List<Vector2[]> pts = new(), nrm = new();
    readonly List<float> lengths = new();
    readonly float[] field;
    readonly int fw, fh;

    public int PathCount => pts.Count;
    public float Length(int path) => lengths[Mathf.Clamp(path, 0, lengths.Count - 1)];
    public Vector2 Spawn(int path) => pts[Mathf.Clamp(path, 0, pts.Count - 1)][0];
    public Vector2 End(int path) { var p = pts[Mathf.Clamp(path, 0, pts.Count - 1)]; return p[^1]; }
    public IReadOnlyList<Vector2> Samples(int path) => pts[path];

    public RouteController(RouteData route)
    {
        data = route;
        halfWidth = route.roadHalfWidth;
        foreach (var p in route.paths) Build(p.points);
        fw = Mathf.CeilToInt(Extents.width / FieldCell) + 1;
        fh = Mathf.CeilToInt(Extents.height / FieldCell) + 1;
        field = new float[fw * fh];
        BuildField();
    }

    void Build(List<Vector2> control)
    {
        var raw = new List<Vector2>();
        int n = control.Count;
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 p0 = i > 0 ? control[i - 1] : control[0] * 2 - control[1];
            Vector2 p1 = control[i], p2 = control[i + 1];
            Vector2 p3 = i + 2 < n ? control[i + 2] : control[n - 1] * 2 - control[n - 2];
            int steps = Mathf.Max(4, Mathf.CeilToInt(Vector2.Distance(p1, p2) / 0.1f));
            for (int k = 0; k < steps; k++) raw.Add(CatmullRom(p0, p1, p2, p3, k / (float)steps));
        }
        raw.Add(control[n - 1]);

        var cum = new float[raw.Count];
        for (int i = 1; i < raw.Count; i++) cum[i] = cum[i - 1] + Vector2.Distance(raw[i - 1], raw[i]);
        float length = cum[^1];
        int count = Mathf.CeilToInt(length / Step) + 1, seg = 0;
        var p = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float s = Mathf.Min(i * Step, length);
            while (seg < raw.Count - 2 && cum[seg + 1] < s) seg++;
            float len = Mathf.Max(1e-5f, cum[seg + 1] - cum[seg]);
            p[i] = Vector2.Lerp(raw[seg], raw[seg + 1], (s - cum[seg]) / len);
        }
        var nm = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            var tan = (p[Mathf.Min(i + 3, count - 1)] - p[Mathf.Max(i - 3, 0)]).normalized;
            nm[i] = new Vector2(-tan.y, tan.x);
        }
        pts.Add(p);
        nrm.Add(nm);
        lengths.Add(length);
    }

    // Centripetal parameterisation: no cusps or self-loops at tight corners.
    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t0 = 0, t1 = t0 + Mathf.Pow(Vector2.Distance(p0, p1), 0.5f) + 1e-4f;
        float t2 = t1 + Mathf.Pow(Vector2.Distance(p1, p2), 0.5f) + 1e-4f;
        float t3 = t2 + Mathf.Pow(Vector2.Distance(p2, p3), 0.5f) + 1e-4f;
        float u = Mathf.Lerp(t1, t2, t);
        Vector2 a1 = (t1 - u) / (t1 - t0) * p0 + (u - t0) / (t1 - t0) * p1;
        Vector2 a2 = (t2 - u) / (t2 - t1) * p1 + (u - t1) / (t2 - t1) * p2;
        Vector2 a3 = (t3 - u) / (t3 - t2) * p2 + (u - t2) / (t3 - t2) * p3;
        Vector2 b1 = (t2 - u) / (t2 - t0) * a1 + (u - t0) / (t2 - t0) * a2;
        Vector2 b2 = (t3 - u) / (t3 - t1) * a2 + (u - t1) / (t3 - t1) * a3;
        return (t2 - u) / (t2 - t1) * b1 + (u - t1) / (t2 - t1) * b2;
    }

    // Point at distance s along a path, shifted sideways by lane (inside the road width).
    public Vector2 Position(int path, float s, float lane)
    {
        var p = pts[path];
        var nm = nrm[path];
        float f = Mathf.Clamp(s, 0, lengths[path]) / Step;
        int i = Mathf.Min((int)f, p.Length - 2);
        float t = f - i;
        return Vector2.Lerp(p[i], p[i + 1], t) + Vector2.Lerp(nm[i], nm[i + 1], t) * lane;
    }

    public Vector2 Direction(int path, float s)
    {
        var a = Position(path, s - 0.1f, 0);
        var b = Position(path, s + 0.1f, 0);
        return (b - a).normalized;
    }

    // ---------------------------------------------------------------- distance field

    void BuildField()
    {
        for (int i = 0; i < field.Length; i++) field[i] = FieldMax;
        int r = Mathf.CeilToInt(FieldMax / FieldCell);
        foreach (var p in pts)
            for (int k = 0; k < p.Length; k += 2)   // 10 cm apart: < 2 mm error at the road edge
            {
                var c = p[k];
                int cx = Mathf.RoundToInt((c.x - Extents.xMin) / FieldCell), cy = Mathf.RoundToInt((c.y - Extents.yMin) / FieldCell);
                int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(fw - 1, cx + r), y0 = Mathf.Max(0, cy - r), y1 = Mathf.Min(fh - 1, cy + r);
                for (int y = y0; y <= y1; y++)
                {
                    float wy = Extents.yMin + y * FieldCell - c.y;
                    for (int x = x0; x <= x1; x++)
                    {
                        float wx = Extents.xMin + x * FieldCell - c.x;
                        float d = wx * wx + wy * wy;
                        int idx = y * fw + x;
                        if (d < field[idx] * field[idx]) field[idx] = Mathf.Sqrt(d);
                    }
                }
            }
    }

    // Distance from p to the nearest road centre line (capped at 3 units).
    public float DistanceToRoad(Vector2 p)
    {
        float fx = (p.x - Extents.xMin) / FieldCell, fy = (p.y - Extents.yMin) / FieldCell;
        if (fx < 0 || fy < 0 || fx >= fw - 1 || fy >= fh - 1) return FieldMax;
        int x = (int)fx, y = (int)fy;
        float tx = fx - x, ty = fy - y;
        float a = field[y * fw + x], b = field[y * fw + x + 1], c = field[(y + 1) * fw + x], d = field[(y + 1) * fw + x + 1];
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    public bool OnRoad(Vector2 p, float margin = 0) => DistanceToRoad(p) <= halfWidth - margin;

    // Distance along a path and signed lateral offset of a point, searched around a known progress (rejoining the road).
    public (float s, float lane) Project(int path, Vector2 p, float around)
    {
        var arr = pts[path];
        var nm = nrm[path];
        int i0 = Mathf.Clamp((int)((around - 4f) / Step), 0, arr.Length - 1), i1 = Mathf.Clamp((int)((around + 6f) / Step), 0, arr.Length - 1);
        int best = i0;
        float bd = float.MaxValue;
        for (int i = i0; i <= i1; i++)
        {
            float d = (arr[i] - p).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return (best * Step, Vector2.Dot(p - arr[best], nm[best]));
    }

    // Nearest path sample (path index, distance along it) for traps and placement hints.
    public (int path, float s, float dist) Nearest(Vector2 p)
    {
        int bp = 0;
        float bs = 0, bd = float.MaxValue;
        for (int i = 0; i < pts.Count; i++)
        {
            var arr = pts[i];
            for (int k = 0; k < arr.Length; k += 2)
            {
                float d = (arr[k] - p).sqrMagnitude;
                if (d < bd) { bd = d; bp = i; bs = k * Step; }
            }
        }
        return (bp, bs, Mathf.Sqrt(bd));
    }
}
