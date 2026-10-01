using UnityEngine;

// Creates enemies on the road with the run's difficulty applied (HP, damage, elites).
public class SpawnController
{
    readonly Battle battle;
    int nextPath;

    public SpawnController(Battle b) => battle = b;

    public EnemyController Spawn(EnemyData d, int path, float progress, float lane, bool allowElite)
    {
        var route = battle.map.route;
        if (path < 0) path = nextPath++ % route.PathCount;
        var go = new GameObject(d.displayName);
        go.transform.SetParent(battle.transform, false);
        var e = go.AddComponent<EnemyController>();
        bool elite = allowElite && battle.difficulty.eliteChance > 0 && !d.IsElite && Random.value < battle.difficulty.eliteChance;
        e.Init(d, path, progress, lane, battle.difficulty, elite);
        battle.enemies.Add(e);
        return e;
    }

    public int NextPath(int fixedPath) => fixedPath >= 0 ? fixedPath : nextPath++ % battle.map.route.PathCount;

    // Alternating lanes (left, centre, right + jitter) so a column forms instead of a pile.
    public float Lane(EnemyData d, int n)
    {
        float max = Mathf.Max(0, battle.map.route.halfWidth - d.bodyRadius * 0.6f - 0.04f);
        return ((n % 3) - 1) * max * 0.75f + Random.Range(-0.08f, 0.08f);
    }

    public bool EntranceBlocked(int path, EnemyData d)
    {
        foreach (var e in battle.enemies)
        {
            if (!e || !e.Alive || e.movement.path != path || e.movement.mode != EnemyMovement.Mode.Path) continue;
            float len = d.bodyLength > 0 ? d.bodyLength : d.bodyRadius;
            if (e.movement.progress < (len + e.movement.Length) * EnemyMovement.Spacing * 0.9f) return true;
        }
        return false;
    }

    public float RandomLane(EnemyData d) => Random.Range(-1f, 1f) * Mathf.Max(0, battle.map.route.halfWidth - d.bodyRadius - 0.1f);
}
