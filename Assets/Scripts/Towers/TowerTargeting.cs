using System.Collections.Generic;
using UnityEngine;

// Target choice shared by every attacking tower.
public static class TowerTargeting
{
    static readonly List<EnemyController> buffer = new();

    static int ByPriority(EnemyController x, EnemyController y, TargetPriority p)
    {
        switch (p)
        {
            case TargetPriority.Strongest:                       // the toughest body first (bosses, elites), then the healthiest
                int m = y.MaxHp.CompareTo(x.MaxHp);
                return m != 0 ? m : y.Hp.CompareTo(x.Hp);
            case TargetPriority.Elite:
                int ex = x.IsElite ? 1 : 0, ey = y.IsElite ? 1 : 0;
                return ex != ey ? ey.CompareTo(ex) : x.movement.Remaining.CompareTo(y.movement.Remaining);
            default:
                return x.movement.Remaining.CompareTo(y.movement.Remaining);
        }
    }

    // Enemies inside range, best target first. Curse towers prefer enemies that are not cursed yet.
    public static List<EnemyController> InRange(Vector2 at, float range, TargetPriority priority, bool preferUncursed = false)
    {
        buffer.Clear();
        var b = Battle.I;
        if (!b) return buffer;
        float r2 = range * range;
        foreach (var e in b.enemies)
            if (e && e.Alive && ((Vector2)e.transform.position - at).sqrMagnitude <= r2) buffer.Add(e);
        buffer.Sort((x, y) =>
        {
            if (preferUncursed && x.status.Cursed != y.status.Cursed) return x.status.Cursed ? 1 : -1;
            return ByPriority(x, y, priority);
        });
        return buffer;
    }

    public static EnemyController NearestTo(Vector2 at, float range, ICollection<EnemyController> exclude)
    {
        EnemyController best = null;
        float bd = range * range;
        foreach (var e in Battle.I.enemies)
        {
            if (!e || !e.Alive || exclude.Contains(e)) continue;
            float d = ((Vector2)e.transform.position - at).sqrMagnitude;
            if (d <= bd) { bd = d; best = e; }
        }
        return best;
    }
}
