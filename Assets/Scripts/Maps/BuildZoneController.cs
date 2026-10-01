using UnityEngine;

// Where a building may stand. Every rule uses the building's footprint (a disc), never just its centre point.
public class BuildZoneController
{
    readonly Battle battle;
    public BuildZoneController(Battle b) => battle = b;

    public bool CanBuild(TowerData t, Vector2 p, out string reason, bool ignoreGold = false)
    {
        reason = Check(t, p);
        if (reason == null && !ignoreGold && battle.gold < t.cost) reason = "Ouro insuficiente";
        return reason == null;
    }

    string Check(TowerData t, Vector2 p)
    {
        var map = battle.map;
        var route = map.route;
        float r = t.footprintRadius;
        if (!MapController.BuildArea.Contains(p) || !MapController.BuildArea.Contains(p + new Vector2(0, r)))
            return "Fora da área de construção";

        float road = route.DistanceToRoad(p);
        if (t.placement == PlacementRule.OnRoad)
        {
            if (road > route.halfWidth * 0.75f) return "Armadilhas só podem ser armadas sobre a estrada";
            foreach (var z in map.zones)
                if ((z.kind is ZoneKind.Spawn or ZoneKind.Fortress) && Vector2.Distance(p, z.center) < z.radius + 0.8f)
                    return z.kind == ZoneKind.Spawn ? "Perto demais do portão dos mortos" : "Perto demais da fortaleza";
            if (map.InZone(p, ZoneKind.Water)) return "Não há chão firme para armar estacas";
        }
        else
        {
            // the base's ground diamond sits above the pivot (its front corner): check its centre too, and keep clear
            // of the stones that ride over the logical edge (MapController.RoadOverhang)
            float roadAtBase = Mathf.Min(road, route.DistanceToRoad(p + new Vector2(0, r * 0.6f)));
            if (roadAtBase < route.halfWidth + MapController.RoadOverhang + r * 0.75f) return "Não se constrói sobre a estrada";
            if (map.Blocked(p, r, out var why)) return why;
        }

        foreach (var o in battle.towers)
            if (Vector2.Distance(o.Pos, p) < r + o.data.footprintRadius) return "Espaço ocupado por outra construção";
        var c = battle.commander;
        if (c && c.Alive && Vector2.Distance(c.transform.position, p) < r + 0.35f) return "O comandante está no lugar";
        foreach (var e in battle.enemies)
            if (e && Vector2.Distance(e.transform.position, p) < r + e.data.bodyRadius) return "Há inimigos no lugar";
        return null;
    }
}
