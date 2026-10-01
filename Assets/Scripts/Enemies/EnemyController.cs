using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// One enemy: data + health + status effects. Movement, melee and abilities live in sibling components;
// the cutout rig (RigView) is purely visual. Gameplay runs on timers, never on animation frames.
[RequireComponent(typeof(EnemyMovement), typeof(EnemyCombat))]
public class EnemyController : MonoBehaviour
{
    const float CorpseSeconds = 1.8f, BarHideSeconds = 3f;
    static int serial;

    public EnemyData data;
    public Health health;
    public readonly StatusEffects status = new();
    public EnemyMovement movement;
    public EnemyCombat combat;
    public BossController boss;
    public RigView view;
    public bool elite;
    public float hpMultiplier = 1, damageMultiplier = 1, goldMultiplier = 1;
    public readonly int id = ++serial;

    public bool Alive => health != null && health.Alive;
    public float Hp => health.current;
    public float MaxHp => health.max;
    public bool IsBoss => data.IsBoss;
    public bool IsElite => elite || data.IsElite;
    public float Height { get; private set; }
    public Vector2 Center => (Vector2)transform.position + Vector2.up * (Height * 0.45f);
    public float Progress => movement.progress;

    Bar bar;
    float barTimer, hitAnimCooldown, castTimer;
    Light2D glow;
    EnemyAbilities abilities;

    public void Init(EnemyData d, int path, float progress, float lane, RunDifficulty diff, bool promoteToElite)
    {
        data = d;
        elite = promoteToElite && !d.IsElite && !d.IsBoss;
        hpMultiplier = diff.enemyHp * (d.IsBoss ? diff.bossHp : 1) * (elite ? 1.7f : 1);
        damageMultiplier = diff.enemyDamage * (elite ? 1.35f : 1);
        goldMultiplier = diff.goldReward * (elite ? 2.2f : 1);
        health = new Health(d.maxHp * hpMultiplier);
        name = (elite ? "Elite " : "") + d.displayName;

        float scale = d.rigScale * (elite ? 1.12f : 1);
        view = RigView.Create(transform, d.rig, scale, d.displayName);
        if (elite) view.SetTint(new Color(1f, 0.78f, 0.74f));
        Height = Mathf.Max(0.8f, RigHeight(d) * scale);

        movement = GetComponent<EnemyMovement>();
        combat = GetComponent<EnemyCombat>();
        movement.Init(this, path, progress, lane);
        combat.Init(this);
        if (d.castInterval > 0 || d.splitInto || d.feedHeal > 0)
        {
            abilities = gameObject.AddComponent<EnemyAbilities>();
            abilities.Init(this);
        }
        if (d.boss)
        {
            boss = gameObject.AddComponent<BossController>();
            boss.Init(this, diff);
        }

        Gfx.Shadow(transform, Mathf.Max(0.5f, d.bodyRadius * 2.6f) * scale);
        float barWidth = d.IsBoss ? 0 : Mathf.Clamp(d.bodyRadius * 2.4f, 0.6f, 1.4f);
        if (barWidth > 0)
        {
            bar = new Bar(transform, new Vector2(0, Height + 0.18f), barWidth, IsElite ? Gfx.EliteHp : Gfx.EnemyHp);
            bar.root.SetActive(IsElite);
        }
        if (d.lightRadius > 0)
            glow = Gfx.Light(transform, new Vector2(0, Height * 0.5f), d.lightColor, d.lightRadius * scale, 0.9f);
        castTimer = d.castInterval * Random.Range(0.4f, 0.8f);
        if (!d.boss) view.Play("Spawn");   // bosses make their own entrance
        if (!string.IsNullOrEmpty(d.spawnSfx)) AudioManager.Play(d.spawnSfx, transform.position);
    }

    static float RigHeight(EnemyData d)
    {
        if (!d.rig) return d.bodyRadius * 5;
        float top = 0;
        foreach (var sr in d.rig.GetComponentsInChildren<SpriteRenderer>(true))
            if (sr.sprite) top = Mathf.Max(top, sr.bounds.max.y - d.rig.transform.position.y);
        return top > 0 ? top : d.bodyRadius * 5;
    }

    void Update()
    {
        if (!Alive) return;
        var b = Battle.I;
        if (!b || b.over)
        {
            view.Moving(false);
            return;
        }
        float dt = Time.deltaTime;
        status.Tick(dt);
        hitAnimCooldown -= dt;
        if (status.burnLeft > 0) Damage(new DamageInfo(status.burnDps * dt, DamageType.Fire, status.burnSource, dot: true));
        if (Alive && status.bleedLeft > 0) Damage(new DamageInfo(status.bleedDps * dt, DamageType.Bleed, status.bleedSource, dot: true));
        if (!Alive) return;
        if (data.regenPerSecond > 0 && status.burnLeft <= 0) health.Heal(data.regenPerSecond * hpMultiplier * dt);

        if (bar != null)
        {
            bar.Set(health.Fraction);
            bar.Tick(dt);
            if (!IsElite && (barTimer -= dt) <= 0 && !Selected) bar.root.SetActive(false);
        }
        if (status.Cursed && Random.value < dt * 4) Fx.Burst(Center + Vector2.up * 0.3f, new Color(0.6f, 0.25f, 0.9f), 1, 0.3f, 0.5f, 1.5f);
        if (status.burnLeft > 0 && Random.value < dt * 10) Fx.Embers(Center, 0.15f);
    }

    public bool Selected { get; set; }

    // Armor after curses and War Chapel heresy auras.
    public float Armor
    {
        get
        {
            float shred = status.ArmorShred + (Battle.I ? Battle.I.ArmorShredAt(transform.position) : 0);
            return Mathf.Max(0, data.armor + (elite ? 3 : 0) - shred);
        }
    }

    // Returns HP removed.
    public float Damage(DamageInfo info)
    {
        if (!Alive || info.amount <= 0) return 0;
        var b = Battle.I;
        float vuln = status.Vulnerability;
        float dealt = DamageSystem.Resolve(info.amount, info.type, Armor, info.armorPenetration, data.Resistance(info.type), vuln, b ? b.config.minPhysicalDamage : 0.15f);
        float removed = health.Take(dealt);
        if (b)
        {
            b.RecordDamage(info.source, removed, IsBoss);
            if (vuln > 0 && status.curseSource != null) b.RecordEnabled(status.curseSource, removed * vuln / (1 + vuln));
        }
        if (bar != null)
        {
            bar.root.SetActive(true);
            barTimer = BarHideSeconds;
        }
        if (!info.isDot)
        {
            view.Flash();
            if (info.crit && SettingsCache.DamageNumbers) Fx.Text(Center + Vector2.up * 0.5f, Mathf.RoundToInt(dealt).ToString() + "!", new Color(1f, 0.85f, 0.3f), 1.2f);
            if (hitAnimCooldown <= 0 && (dealt >= MaxHp * 0.04f || info.crit))
            {
                hitAnimCooldown = IsBoss ? 1.2f : 0.35f;
                view.Play("Hit");
                if (!string.IsNullOrEmpty(data.hitSfx)) AudioManager.Play(data.hitSfx, transform.position);
            }
        }
        if (!health.Alive) Die(info.source);
        return removed;
    }

    // On-hit effects of a tower attack (the damage itself goes through Damage()).
    public void ApplyEffects(in TowerStats s, string source)
    {
        if (!Alive) return;
        status.ApplyBurn(s.burnDps, s.burnDuration, source);
        status.ApplyBleed(s.bleedDps, s.bleedDuration, source);
        status.ApplySlow(IsBoss ? s.slowAmount * 0.5f : s.slowAmount, s.slowDuration);
        if (s.curseDuration > 0) status.ApplyCurse(s.vulnerability, s.armorShred, s.curseDuration, s.curseSpreads, source);
        if (s.stunChance > 0 && !IsBoss && Random.value < s.stunChance) status.ApplyStun(s.stunDuration);
    }

    public void Shield(float amount)
    {
        health.shield = Mathf.Max(health.shield, amount);
        Fx.Ring(Center, 0.5f, new Color(0.55f, 0.95f, 0.45f), 0.35f);
    }

    public void CastFx()
    {
        view.Play("Cast");
    }

    void Die(string source)
    {
        var b = Battle.I;
        view.Dead(true);
        view.Moving(false);
        if (bar != null) bar.root.SetActive(false);
        if (glow) glow.enabled = false;
        Fx.Blood(Center, IsBoss ? 3 : 1);
        if (data.Has(EnemyTag.Undead)) Fx.Souls(Center, new Color(0.6f, 0.5f, 0.85f));
        if (!string.IsNullOrEmpty(data.deathSfx)) AudioManager.Play(data.deathSfx, transform.position);
        if (status.curseSpreads) SpreadCurse();
        if (b) b.Killed(this, source);
        abilities?.OnDeath();
        StartCoroutine(Corpse());
    }

    void SpreadCurse()
    {
        var b = Battle.I;
        if (!b) return;
        foreach (var e in b.EnemiesIn(transform.position, 1.8f))
            if (e != this) e.status.ApplyCurse(status.vulnerability, status.armorShred, 3f, false, status.curseSource);
        Fx.Ring(Center, 1.8f, new Color(0.55f, 0.2f, 0.85f), 0.4f);
    }

    public void Leak()
    {
        if (!Alive) return;
        health.current = 0;
        if (Battle.I) Battle.I.Leaked(this);
        Destroy(gameObject);
    }

    IEnumerator Corpse()
    {
        yield return new WaitForSeconds(IsBoss ? 3.5f : CorpseSeconds);
        for (float t = 0; t < 0.6f; t += Time.deltaTime)
        {
            view.SetAlpha(1 - t / 0.6f);
            yield return null;
        }
        Destroy(gameObject);
    }
}

// Settings read every frame by gameplay visuals (kept here so Fx does not depend on the save system).
public static class SettingsCache
{
    public static bool DamageNumbers = true;
}
