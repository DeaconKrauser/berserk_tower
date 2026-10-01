using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

// Builds one map from MapData + RouteData: ground Tilemap, road/water painted on Tilemaps along the spline
// with irregular pixel edges, landmarks, scattered decoration, spawn portals and lights.
public class MapController : MonoBehaviour
{
    // Playable rectangle (the HUD covers the top strip and the bottom corners/bar).
    public static readonly Rect BuildArea = Rect.MinMaxRect(-14.5f, -5.55f, 14.5f, 6.95f);
    public static readonly Rect WalkArea = Rect.MinMaxRect(-14.8f, -6.1f, 14.8f, 7.3f);
    const int TilePx = 32;

    public MapData map;
    public RouteData routeData;
    public RouteController route;
    public readonly List<MapZone> zones = new();
    public readonly List<(Vector2 at, float radius, string name)> blockers = new();

    Transform props;
    System.Random rnd;

    public void Build(MapData m, RouteData r)
    {
        map = m;
        routeData = r;
        rnd = new System.Random(r.decorSeed);
        route = new RouteController(r);
        zones.AddRange(r.zones);
        for (int i = 0; i < route.PathCount; i++)
        {
            var s = route.Spawn(i);
            var inside = new Vector2(Mathf.Clamp(s.x, -14.6f, 14.6f), Mathf.Clamp(s.y, -6.2f, 7.4f));
            if (!zones.Exists(z => z.kind == ZoneKind.Spawn && Vector2.Distance(z.center, inside) < 1f))
                zones.Add(new MapZone { kind = ZoneKind.Spawn, center = inside, radius = 2.1f, reason = "Portão dos mortos: ninguém constrói aqui" });
        }
        zones.Add(new MapZone { kind = ZoneKind.Fortress, center = r.fortressPosition + new Vector2(0, 1.0f), radius = 2.3f, reason = "Muralhas da fortaleza" });

        props = new GameObject("Props").transform;
        props.SetParent(transform, false);
        PaintTiles();
        PlaceSpawnPortals();
        foreach (var p in r.props) PlaceProp(p.prop, p.position, p.block, p.blockRadius, p.flip);
        Scatter();
        SetupLight();
    }

    // ---------------------------------------------------------------- tiles

    Tilemap Layer(Grid grid, string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(grid.transform, false);
        var tm = go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>().sortingOrder = order;
        return tm;
    }

    static Tile TileOf(Sprite s)
    {
        var t = ScriptableObject.CreateInstance<Tile>();
        t.sprite = s;
        return t;
    }

    readonly Dictionary<Sprite, Color32[]> pixelCache = new();

    Color32[] Pixels(Sprite s)
    {
        if (pixelCache.TryGetValue(s, out var px)) return px;
        var r = s.rect;
        px = s.texture.GetPixels32();
        if ((int)r.width != s.texture.width || (int)r.height != s.texture.height)
        {
            var sub = new Color32[(int)r.width * (int)r.height];
            for (int y = 0; y < (int)r.height; y++)
                for (int x = 0; x < (int)r.width; x++)
                    sub[y * (int)r.width + x] = px[((int)r.y + y) * s.texture.width + (int)r.x + x];
            px = sub;
        }
        return pixelCache[s] = px;
    }

    // Periodic placement keeps the delivered 3x3 blocks seamless; sets of loose tiles are picked at random.
    Sprite Pick(Sprite[] set, int x, int y, bool periodic)
    {
        if (set == null || set.Length == 0) return null;
        if (periodic && set.Length == 9)
        {
            int col = ((x % 3) + 3) % 3, row = 2 - ((y % 3) + 3) % 3;
            return set[row * 3 + col];
        }
        uint h = (uint)(x * 73856093 ^ y * 19349663 ^ routeData.decorSeed * 83492791);
        return set[h % (uint)set.Length];
    }

    float EdgeNoise(float wx, float wy) =>
        (Mathf.PerlinNoise(wx * 2.1f + 13.7f, wy * 2.1f + 5.1f) - 0.5f) * 0.28f + (Mathf.PerlinNoise(wx * 6.3f + 1.3f, wy * 6.3f + 9.2f) - 0.5f) * 0.12f;

    bool InWater(float wx, float wy, out float depth)
    {
        depth = -1;
        foreach (var z in zones)
        {
            if (z.kind != ZoneKind.Water) continue;
            float d = z.radius - Vector2.Distance(new Vector2(wx, wy), z.center) + EdgeNoise(wx + 40, wy) * 1.6f;
            if (d > depth) depth = d;
        }
        return depth > 0;
    }

    float ZoneTint(float wx, float wy, ZoneKind kind)
    {
        float best = 0;
        foreach (var z in zones)
        {
            if (z.kind != kind) continue;
            float d = 1 - Vector2.Distance(new Vector2(wx, wy), z.center) / z.radius + EdgeNoise(wx, wy + 30) * 0.8f;
            best = Mathf.Max(best, Mathf.Clamp01(d * 2.5f));
        }
        return best;
    }

    void PaintTiles()
    {
        var grid = new GameObject("Grid").AddComponent<Grid>();
        grid.transform.SetParent(transform, false);
        grid.cellSize = Vector3.one;
        var ground = Layer(grid, "Ground", Gfx.OrderGround);
        var painted = Layer(grid, "Road", Gfx.OrderRoad);
        bool periodicGround = map.groundTiles != null && map.groundTiles.Length == 9;
        var tileCache = new Dictionary<Sprite, Tile>();
        Tile T(Sprite s) => tileCache.TryGetValue(s, out var t) ? t : tileCache[s] = TileOf(s);

        const int x0 = -16, x1 = 16, y0 = -9, y1 = 9;
        var cells = new List<Vector2Int>();
        float hw = route.halfWidth;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                var g = Pick(map.groundTiles, x, y, periodicGround);
                if (map.groundAccentTiles != null && map.groundAccentTiles.Length > 0 && Hash(x, y, 7) % 100 < 16)
                    g = Pick(map.groundAccentTiles, x, y, false);
                ground.SetTile(new Vector3Int(x, y, 0), T(g));
                var c = new Vector2(x + 0.5f, y + 0.5f);
                bool near = route.DistanceToRoad(c) < hw + 1.0f;
                bool zone = false;
                foreach (var z in zones)
                    if (z.kind is ZoneKind.Water or ZoneKind.Cursed or ZoneKind.Graveyard && Vector2.Distance(c, z.center) < z.radius + 1.6f)
                        zone = true;
                if (near || zone) cells.Add(new Vector2Int(x, y));
            }

        BuildRoadPalette();

        // One atlas texture holds every painted cell (pixel-exact edges, then sliced back into Tiles).
        int perRow = 32, rows = Mathf.CeilToInt(cells.Count / (float)perRow);
        var atlas = new Texture2D(perRow * TilePx, Mathf.Max(1, rows) * TilePx, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "RoadAtlas" };
        var clear = new Color32[atlas.width * atlas.height];
        atlas.SetPixels32(clear);
        var buffer = new Color32[TilePx * TilePx];
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var groundPx = Pixels(ground.GetSprite(new Vector3Int(cell.x, cell.y, 0)));
            var waterPx = map.waterTiles != null && map.waterTiles.Length > 0 ? Pixels(Pick(map.waterTiles, cell.x, cell.y, false)) : null;
            var bridgePx = map.bridgeTiles != null && map.bridgeTiles.Length > 0 ? Pixels(Pick(map.bridgeTiles, cell.x, cell.y, false)) : null;
            bool any = false;
            for (int py = 0; py < TilePx; py++)
                for (int px = 0; px < TilePx; px++)
                {
                    float wx = cell.x + (px + 0.5f) / TilePx, wy = cell.y + (py + 0.5f) / TilePx;
                    int k = py * TilePx + px, gx = cell.x * TilePx + px, gy = cell.y * TilePx + py;
                    float depth = -1;
                    bool water = waterPx != null && InWater(wx, wy, out depth);
                    Color32 o = new(0, 0, 0, 0);
                    if (RoadAt(gx, gy, out var road))
                        o = water && bridgePx != null ? bridgePx[k] : road;
                    else if (water)
                    {
                        o = waterPx[k];
                        if (depth < 0.12f) o = Mul(Blend(o, groundPx[k], 0.35f), 0.55f);   // muddy bank
                    }
                    else if (route.DistanceToRoad(new Vector2(wx, wy)) < hw + RoadOverhang + 0.12f &&
                             (RoadAt(gx + 1, gy, out _) || RoadAt(gx - 1, gy, out _) || RoadAt(gx, gy + 1, out _) || RoadAt(gx, gy - 1, out _)))
                        o = Mul(groundPx[k], 0.45f + 0.2f * ((px * 7 + py * 3) % 5 == 0 ? 1 : 0));   // dark gutter with grit
                    else
                    {
                        float cursed = ZoneTint(wx, wy, ZoneKind.Cursed), grave = ZoneTint(wx, wy, ZoneKind.Graveyard);
                        if (cursed > 0.02f)
                        {
                            var g = groundPx[k];
                            o = new Color32((byte)(g.r * (1 - 0.3f * cursed)), (byte)(g.g * (1 - 0.55f * cursed)), (byte)(g.b * (1 - 0.1f * cursed) + 18 * cursed), (byte)(255 * Mathf.Clamp01(cursed * 1.5f)));
                        }
                        else if (grave > 0.02f) o = Mul(groundPx[k], 1 - 0.3f * grave, (byte)(200 * grave));
                    }
                    buffer[k] = o;
                    any |= o.a > 0;
                }
            if (!any) continue;
            int ax = i % perRow * TilePx, ay = i / perRow * TilePx;
            atlas.SetPixels32(ax, ay, TilePx, TilePx, buffer);
            var sprite = Sprite.Create(atlas, new Rect(ax, ay, TilePx, TilePx), new Vector2(0.5f, 0.5f), Gfx.PPU, 0, SpriteMeshType.FullRect);
            painted.SetTile(new Vector3Int(cell.x, cell.y, 0), TileOf(sprite));
        }
        atlas.Apply();
    }

    // ---------------------------------------------------------------- road surface
    // The road is painted pixel by pixel in world space, so it is one continuous surface along any curve (no repeated
    // tile blocks, no seams). Its colours and texture come from the delivered road tiles (roadTiles): a luminance ramp
    // of their palette plus their own stone texture sampled inside each stone.

    public const float RoadOverhang = 0.14f;      // how far whole edge stones may reach past the logical road edge
    const int StoneCell = 11;                     // cobble size in pixels (jittered grid)
    Color32[] roadRamp;                           // road tile colours sorted dark -> light
    float rampMid, rampLow;

    void BuildRoadPalette()
    {
        var all = new List<Color32>();
        if (map.roadTiles != null)
            foreach (var t in map.roadTiles)
                if (t) all.AddRange(Pixels(t));
        if (all.Count == 0) all.Add(new Color32(90, 84, 88, 255));
        // the distinct colours of the art, dark -> light: every step of the ramp is a real colour of the tiles
        var distinct = new HashSet<Color32>(all);
        var ramp = new List<Color32>(distinct);
        ramp.Sort((a, b) => Lum(a).CompareTo(Lum(b)));
        roadRamp = ramp.ToArray();
        all.Sort((a, b) => Lum(a).CompareTo(Lum(b)));
        rampMid = Lum(all[all.Count * 6 / 10]);
        rampLow = Lum(all[all.Count * 3 / 10]);
    }

    static float Lum(Color32 c) => (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) / 255f;

    Color32 Ramp(float t) => roadRamp[Mathf.Clamp(Mathf.RoundToInt(t * (roadRamp.Length - 1)), 0, roadRamp.Length - 1)];

    static float H01(int x, int y, int salt) => Hash(x, y, salt) % 10007u / 10007f;

    static int FloorDiv(int a, int b) => a >= 0 ? a / b : (a - b + 1) / b;

    static int Mod(int a, int m) => ((a % m) + m) % m;

    // Road colour at world pixel (gx, gy); false off the road.
    bool RoadAt(int gx, int gy, out Color32 c)
    {
        float dl = route.DistanceToRoad(new Vector2((gx + 0.5f) / TilePx, (gy + 0.5f) / TilePx));
        c = default;
        if (dl > route.halfWidth + RoadOverhang) return false;
        return map.roadStyle == RoadStyle.Mud ? Mud(gx, gy, dl, out c) : Cobble(gx, gy, out c);
    }

    // Irregular flagstones (Voronoi on a jittered grid, squashed for the 3/4 view) with mortar joints, a lit upper-left
    // bevel and a shaded lower-right one, like the delivered cobble. The edge is made of whole stones.
    bool Cobble(int gx, int gy, out Color32 c)
    {
        c = default;
        int cx = FloorDiv(gx, StoneCell), cy = FloorDiv(gy, StoneCell);
        float f1 = 1e9f, f2 = 1e9f;
        Vector2 centre = default;
        uint id = 0;
        for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int sx = cx + i, sy = cy + j;
                float fx = (sx + 0.15f + 0.7f * H01(sx, sy, 11)) * StoneCell, fy = (sy + 0.15f + 0.7f * H01(sx, sy, 12)) * StoneCell;
                float dx = gx + 0.5f - fx, dy = (gy + 0.5f - fy) * 1.3f, dd = Mathf.Sqrt(dx * dx + dy * dy);
                if (dd < f1)
                {
                    f2 = f1;
                    f1 = dd;
                    centre = new Vector2(fx, fy);
                    id = Hash(sx, sy, 13);
                }
                else if (dd < f2) f2 = dd;
            }
        float hw = route.halfWidth, stoneRoad = route.DistanceToRoad(centre / TilePx);
        if (stoneRoad > hw - 0.04f) return false;
        float joint = f2 - f1;                                                    // ~2x the distance to the stone's edge, in px
        if (joint < 1.5f)
        {
            c = Ramp(joint < 0.7f ? 0f : 0.1f);                                  // mortar
            return true;
        }
        // stone body in the upper-middle of the ramp, each stone its own shade; lit from the upper left
        float t = 0.58f + 0.16f * (id % 97 / 97f);
        if (stoneRoad > hw - 0.34f) t -= 0.1f;                                    // sunken, worn edge stones
        var fromCentre = new Vector2(gx + 0.5f, gy + 0.5f) - centre;
        t -= Mathf.Clamp(-fromCentre.y / StoneCell, -0.5f, 0.5f) * 0.12f;         // lower half a little darker
        if (joint < 4.4f)
        {
            float lit = Vector2.Dot(fromCentre.normalized, new Vector2(-0.6f, 0.8f));   // rim facing the light
            t += lit > 0.25f ? 0.26f : lit < -0.25f ? -0.26f : 0;
        }
        else if (joint < 6.4f && Vector2.Dot(fromCentre.normalized, new Vector2(-0.6f, 0.8f)) < -0.4f) t -= 0.12f;
        // the delivered stone texture, sampled stone-locally (each stone shows a different patch of the tiles)
        if (map.roadTiles != null && map.roadTiles.Length == 9)
        {
            int u = Mod(gx - (int)centre.x + (int)(id & 63), 3 * TilePx), v = Mod(gy - (int)centre.y + (int)((id >> 6) & 63), 3 * TilePx);
            var tile = map.roadTiles[(2 - v / TilePx) * 3 + u / TilePx];
            if (tile)
            {
                float l = Lum(Pixels(tile)[v % TilePx * TilePx + u % TilePx]);
                if (l > rampLow) t += (l - rampMid) * 1.6f;
            }
        }
        if (H01(gx, gy, 14) < 0.012f) t -= 0.25f;                                 // pits and chips
        c = Ramp(Mathf.Clamp01(t));
        return true;
    }

    // Continuous swamp mud built like the delivered mud tiles: olive-brown base, dark puddles with a black rim and
    // moss specks, light-green moss blobs; a slightly ragged edge and a trampled darker margin. Colours are picked
    // from the tiles' own palette (dark -> light ramp).
    bool Mud(int gx, int gy, float dl, out Color32 c)
    {
        c = default;
        float hw = route.halfWidth;
        float edge = hw + (Mathf.PerlinNoise(gx * 0.05f + 3.1f, gy * 0.05f + 11.7f) - 0.5f) * 0.22f;
        if (dl > edge) return false;
        float low = Mathf.PerlinNoise(gx * 0.04f + 7.3f, gy * 0.055f + 3.9f), high = Mathf.PerlinNoise(gx * 0.23f + 1.7f, gy * 0.29f + 5.2f);
        float puddle = Mathf.PerlinNoise(gx * 0.035f + 40.2f, gy * 0.05f + 17.6f) + (high - 0.5f) * 0.08f;
        float t;
        if (dl < hw - 0.12f && puddle < 0.27f)
            t = puddle > 0.25f ? 0.1f : high > 0.78f ? 0.95f : puddle < 0.19f ? 0.14f : 0.35f;   // rim, moss afloat, deep, water
        else
        {
            t = 0.76f + low * 0.12f;                                                   // olive mud
            if (high > 0.74f) t = high > 0.8f ? 1f : 0.95f;                            // moss blobs
            else if (high < 0.22f) t -= 0.12f;                                         // wet pockets
            if (dl > edge - 0.09f) t -= 0.1f;                                          // trampled margin
        }
        c = Ramp(Mathf.Clamp01(t));
        return true;
    }

    static Color32 Mul(Color32 c, float k, byte a = 255) => new((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), a);

    static Color32 Blend(Color32 a, Color32 b, float t) =>
        new((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), 255);

    static uint Hash(int x, int y, int salt) => (uint)((x * 92837111) ^ (y * 689287499) ^ (salt * 283923481)) & 0x7fffffff;

    // ---------------------------------------------------------------- props

    public SpriteRenderer PlaceProp(string name, Vector2 at, bool block = true, float blockRadius = 0, bool flip = false)
    {
        var p = map.Prop(name);
        if (p == null || !p.sprite)
        {
            Debug.LogWarning($"Map {map.id}: prop '{name}' não existe");
            return null;
        }
        var sr = Gfx.Renderer(props, name, p.sprite, at, p.decal ? Gfx.OrderDecal + 1 : Gfx.OrderWorld);
        sr.flipX = flip;
        if (!p.decal)
        {
            Gfx.Shadow(sr.transform, Mathf.Min(p.sprite.bounds.size.x * 0.75f, 2.6f));
            if (block) blockers.Add((at, blockRadius > 0 ? blockRadius : Mathf.Clamp(p.sprite.bounds.size.x * 0.32f, 0.25f, 1.4f), name));
        }
        if (p.flame) TorchFlame.Attach(sr.transform, p.flame, flip);
        if (p.torch)
        {
            var l = Gfx.Light(sr.transform, new Vector2(0, p.sprite.bounds.size.y * 0.85f), p.lightColor, 3.0f, 1.05f);
            l.gameObject.AddComponent<Flicker>();
        }
        return sr;
    }

    void PlaceSpawnPortals()
    {
        var portal = map.props.Find(p => p.name == "spawn_portal");
        for (int i = 0; i < route.PathCount; i++)
        {
            var s = route.Spawn(i);
            if (i > 0 && Vector2.Distance(s, route.Spawn(0)) < 1.5f) continue;
            var inside = new Vector2(Mathf.Clamp(s.x, -14.4f, 14.4f), Mathf.Clamp(s.y, -6.3f, 7.4f));
            if (portal != null && portal.sprite)
            {
                var sr = Gfx.Renderer(props, "SpawnPortal", portal.sprite, inside, Gfx.OrderDecal + 2);
                sr.transform.localScale = Vector3.one * 1.3f;
            }
            Gfx.Light(props, inside + Vector2.up * 0.4f, new Color(0.62f, 0.2f, 0.85f), 3.4f, 1.1f).gameObject.AddComponent<Flicker>();
        }
    }

    // Weighted random decoration away from the road, zones and other props.
    void Scatter()
    {
        var pool = map.props.FindAll(p => p.scatterWeight > 0 && p.sprite);
        if (pool.Count == 0) return;
        float total = 0;
        foreach (var p in pool) total += p.scatterWeight;
        int attempts = Mathf.RoundToInt(220 * routeData.decorDensity);
        var placed = new List<Vector2>();
        foreach (var b in blockers) placed.Add(b.at);
        for (int i = 0; i < attempts; i++)
        {
            var at = new Vector2((float)(rnd.NextDouble() * 30 - 15), (float)(rnd.NextDouble() * 15.2 - 7.2));
            float r = (float)rnd.NextDouble() * total;
            PropSprite pick = pool[0];
            foreach (var p in pool)
            {
                if ((r -= p.scatterWeight) <= 0) { pick = p; break; }
            }
            float clearance = pick.decal ? 0.15f : 0.9f;
            if (route.DistanceToRoad(at) < route.halfWidth + clearance) continue;
            bool bad = false;
            foreach (var z in zones)
                if (z.kind is ZoneKind.Spawn or ZoneKind.Fortress or ZoneKind.Water && Vector2.Distance(at, z.center) < z.radius + 0.4f) bad = true;
            foreach (var q in placed)
                if (Vector2.Distance(q, at) < (pick.decal ? 0.9f : 1.6f)) bad = true;
            if (bad) continue;
            // Keep the build area readable: big blockers mostly at the edges, small ones anywhere.
            bool inBuild = BuildArea.Contains(at);
            if (inBuild && !pick.decal && pick.sprite.bounds.size.x > 1.2f && rnd.NextDouble() < 0.7) continue;
            placed.Add(at);
            PlaceProp(pick.name, Gfx.Snap(at), !pick.decal, 0, rnd.NextDouble() < 0.5);
        }
    }

    void SetupLight()
    {
        foreach (var l in FindObjectsByType<Light2D>())
            if (l.lightType == Light2D.LightType.Global)
            {
                l.intensity = map.globalLightIntensity;
                l.color = map.globalLightColor;
            }
        if (Camera.main) Camera.main.backgroundColor = map.backgroundColor;
    }

    // ---------------------------------------------------------------- queries

    public bool Blocked(Vector2 p, float radius, out string reason)
    {
        foreach (var z in zones)
            if (Vector2.Distance(p, z.center) < z.radius + radius * 0.5f) { reason = z.reason; return true; }
        foreach (var (at, r, _) in blockers)
            if (Vector2.Distance(p, at) < r + radius * 0.6f) { reason = "Obstáculo no terreno"; return true; }
        reason = null;
        return false;
    }

    public bool InZone(Vector2 p, ZoneKind kind)
    {
        foreach (var z in zones)
            if (z.kind == kind && Vector2.Distance(p, z.center) < z.radius) return true;
        return false;
    }

    public static Vector2 ClampWalk(Vector2 p) =>
        new(Mathf.Clamp(p.x, WalkArea.xMin, WalkArea.xMax), Mathf.Clamp(p.y, WalkArea.yMin, WalkArea.yMax));

    // Top-down miniature of a layout for the map selection screen (no Tilemap needed).
    public static Texture2D Preview(MapData m, RouteData r, int w = 240, int h = 135)
    {
        var rc = new RouteController(r);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        Color ground = Average(m.groundTiles), road = Average(m.roadTiles), water = Average(m.waterTiles);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(-15 + 30f * (x + 0.5f) / w, -8.44f + 16.88f * (y + 0.5f) / h);
                float d = rc.DistanceToRoad(p);
                Color c = ground * (0.85f + 0.15f * Mathf.PerlinNoise(p.x * 0.8f, p.y * 0.8f));
                foreach (var z in r.zones)
                    if (z.kind == ZoneKind.Water && Vector2.Distance(p, z.center) < z.radius && m.waterTiles != null && m.waterTiles.Length > 0) c = water;
                if (d < rc.halfWidth) c = road;
                else if (d < rc.halfWidth + 0.12f) c = ground * 0.45f;
                if (Vector2.Distance(p, r.fortressPosition + Vector2.up * 0.5f) < 1.0f) c = new Color(0.75f, 0.2f, 0.18f);
                for (int i = 0; i < rc.PathCount; i++)
                {
                    var s = rc.Spawn(i);
                    s = new Vector2(Mathf.Clamp(s.x, -14.6f, 14.6f), Mathf.Clamp(s.y, -8f, 8f));
                    if (Vector2.Distance(p, s) < 0.8f) c = new Color(0.55f, 0.2f, 0.75f);
                }
                c.a = 1;
                px[y * w + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Color Average(Sprite[] set)
    {
        if (set == null || set.Length == 0 || !set[0]) return new Color(0.2f, 0.2f, 0.2f);
        try
        {
            var px = set[0].texture.GetPixels32();
            long r = 0, g = 0, b = 0;
            foreach (var p in px) { r += p.r; g += p.g; b += p.b; }
            return new Color(r / 255f / px.Length, g / 255f / px.Length, b / 255f / px.Length);
        }
        catch
        {
            return new Color(0.2f, 0.2f, 0.2f);
        }
    }
}
