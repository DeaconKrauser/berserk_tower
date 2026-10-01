using UnityEngine;

// Flies from the muzzle to the target (homing) or to its predicted ground point (lobbed, for area damage).
// Impact applies damage + on-hit effects; arrows can pierce and stick, fire leaves burning ground.
public class Projectile : MonoBehaviour
{
    ProjectileData data;
    TowerStats stats;
    string source, impactSfx;
    EnemyController target;
    Vector2 from, to, last;
    float t, duration;
    bool crit;
    SpriteRenderer sr;

    public static void Fire(ProjectileData d, Vector2 from, EnemyController target, TowerStats s, string source, bool crit, string impactSfx)
    {
        if (!d || !target) return;
        var p = new GameObject("Projectile").AddComponent<Projectile>();
        if (Battle.I) p.transform.SetParent(Battle.I.transform, true);
        p.source = source;
        p.impactSfx = impactSfx;
        p.data = d;
        p.stats = s;
        p.crit = crit;
        p.target = target;
        p.from = p.last = from;
        float speed = s.projectileSpeed > 0 ? s.projectileSpeed : d.speed;
        p.duration = Mathf.Max(0.05f, Vector2.Distance(from, target.Center) / speed);
        p.to = d.homing ? target.Center : target.movement.Predict(p.duration);
        p.transform.position = from;
        p.sr = Gfx.Renderer(p.transform, "Sprite", d.sprite, Vector2.zero, Gfx.OrderProjectile);
        if (crit) p.sr.color = new Color(1f, 0.85f, 0.55f);
    }

    void Update()
    {
        if (data.homing && target && target.Alive) to = target.Center;
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / duration);
        Vector2 pos = Vector2.Lerp(from, to, k) + Vector2.up * (data.arcHeight * 4 * k * (1 - k));
        if (data.spins) transform.Rotate(0, 0, -720 * Time.deltaTime);
        else if ((pos - last).sqrMagnitude > 1e-6f) transform.right = pos - last;
        if (data.trailColor.a > 0 && Random.value < 0.6f) Fx.Burst(pos, data.trailColor, 1, 0.2f, 0.3f, 0.5f);
        transform.position = last = pos;
        if (k < 1) return;
        Impact();
        Destroy(gameObject);
    }

    void Impact()
    {
        var b = Battle.I;
        if (!b) return;
        float mult = crit ? Mathf.Max(1.5f, stats.critMultiplier) : 1;
        if (stats.splashRadius > 0)
        {
            foreach (var e in b.EnemiesIn(to, stats.splashRadius))
            {
                float boss = e.IsElite ? Mathf.Max(1, stats.bossDamageMultiplier) : 1;
                e.Damage(new DamageInfo(stats.damage * mult * boss, stats.damageType, source, stats.armorPenetration, crit));
                e.ApplyEffects(stats, source);
            }
            if (stats.groundFireDps > 0) Fx.GroundFire(to, stats.groundFireRadius > 0 ? stats.groundFireRadius : stats.splashRadius * 0.8f, stats.groundFireDps, stats.groundFireDuration, source);
            ImpactFx(to, stats.splashRadius);
        }
        else if (target && target.Alive)
        {
            HitOne(target, mult);
            ImpactFx(target.Center, 0);
            if (stats.pierce > 0) Pierce();
            if (data.sticks && target.Alive) Stick(target);
        }
        AudioManager.Play(impactSfx, to, 0.8f);
    }

    void HitOne(EnemyController e, float mult)
    {
        float boss = e.IsElite ? Mathf.Max(1, stats.bossDamageMultiplier) : 1;
        e.Damage(new DamageInfo(stats.damage * mult * boss, stats.damageType, source, stats.armorPenetration, crit));
        e.ApplyEffects(stats, source);
        if (stats.bossDamageMultiplier > 1 && e.IsElite) CameraRig.Shake(0.05f);
    }

    // Archer tier 3+: the arrow keeps going and hits enemies right behind the first one.
    void Pierce()
    {
        var dir = (to - from).normalized;
        int left = stats.pierce;
        foreach (var e in Battle.I.EnemiesIn(to + dir * 1.2f, 1.25f))
        {
            if (e == target || left <= 0) continue;
            if (Vector2.Dot((Vector2)e.transform.position - to, dir) < -0.2f) continue;
            HitOne(e, 0.75f);
            Fx.Sparks(e.Center, data.impactColor);
            left--;
        }
    }

    void Stick(EnemyController e)
    {
        var go = new GameObject("Stuck");
        go.transform.SetParent(e.view.transform, true);
        go.transform.position = e.Center + Random.insideUnitCircle * 0.15f;
        go.transform.right = (to - from).normalized;
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = data.sprite;
        s.sortingOrder = 30;
        go.AddComponent<StuckFade>();
    }

    void ImpactFx(Vector2 at, float radius)
    {
        switch (data.impact)
        {
            case ImpactStyle.Heavy:
                Fx.Dust(at, 1f);
                Fx.Sparks(at, data.impactColor);
                Fx.Flash(at, 0.4f, new Color(1f, 0.95f, 0.8f, 0.7f));
                CameraRig.Shake(0.03f);
                break;
            case ImpactStyle.Fire:
                Fx.Ring(at, radius, data.impactColor, 0.35f);
                Fx.Flash(at, radius * 0.8f, new Color(0.85f, 0.35f, 1f, 0.55f), 0.2f);
                Fx.Embers(at, 1.4f);
                break;
            case ImpactStyle.Curse:
                Fx.Burst(at, new Color(0.55f, 0.2f, 0.85f), 8, 1.2f, 0.6f, 1f);
                Fx.Burst(at, new Color(0.1f, 0.08f, 0.12f), 5, 1.6f, 0.5f, -3f);
                break;
            case ImpactStyle.Blood:
                Fx.Blood(at, 1.2f);
                break;
            default:
                Fx.Sparks(at, data.impactColor);
                break;
        }
    }
}

public class StuckFade : MonoBehaviour
{
    float t;
    SpriteRenderer sr;

    void Start() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        t += Time.deltaTime;
        if (t > 0.8f) sr.color = new Color(1, 1, 1, Mathf.Clamp01(1.4f - t) / 0.6f);
        if (t > 1.4f) Destroy(gameObject);
    }
}
