using System.Collections.Generic;
using UnityEngine;

// What a tower does each frame, by kind: fire projectiles, arc blood magic between enemies,
// pulse a support aura, or spring spikes on whoever walks over it.
public class TowerCombat : MonoBehaviour
{
    TowerController t;
    float cooldown = 0.4f, pulse, scan;
    EnemyController aim;                   // what a turret tower is tracking between shots
    readonly List<EnemyController> hitChain = new();

    public void Init(TowerController owner) => t = owner;

    void Update()
    {
        var b = Battle.I;
        if (!b || b.over || !t) return;
        float dt = Time.deltaTime;
        if (t.stunLeft > 0) return;
        switch (t.data.kind)
        {
            case TowerKind.Aura: Aura(dt); break;
            case TowerKind.Trap: Trap(dt); break;
            default:
                if (t.HasTurret) Track(dt);
                if ((cooldown -= dt) > 0) return;
                var s = t.Effective();
                var targets = TowerTargeting.InRange(t.Pos, s.range, t.priority, s.curseDuration > 0);
                if (targets.Count == 0) return;
                cooldown = s.attackInterval;
                aim = targets[0];
                if (t.data.kind == TowerKind.Chain) Chain(s, targets[0]);
                else Shoot(s, targets);
                break;
        }
    }

    // The weapon follows its current target every frame and re-picks the best one a few times per second.
    void Track(float dt)
    {
        if ((scan -= dt) <= 0 || !aim || !aim.Alive)
        {
            scan = 0.15f;
            var s = t.Effective();
            var l = TowerTargeting.InRange(t.Pos, s.range, t.priority, s.curseDuration > 0);
            aim = l.Count > 0 ? l[0] : null;
        }
        if (aim && aim.Alive) t.AimAt(aim.Center);
    }

    void Shoot(in TowerStats s, List<EnemyController> targets)
    {
        int n = Mathf.Min(Mathf.Max(1, s.targets), targets.Count);
        for (int i = 0; i < n; i++)
        {
            bool crit = s.critChance > 0 && Random.value < s.critChance;
            Projectile.Fire(t.data.projectile, t.Muzzle, targets[i], s, t.data.displayName, crit, t.data.impactSfx);
        }
        t.Kick();
        AudioManager.Play(t.data.fireSfx, t.Pos);
    }

    // Blood Obelisk: instant bolt that jumps between nearby enemies, losing strength each jump; executes the weak.
    void Chain(in TowerStats s, EnemyController first)
    {
        hitChain.Clear();
        var from = t.Muzzle;
        var target = first;
        float dmg = s.damage;
        for (int jump = 0; jump <= s.chainCount && target; jump++)
        {
            hitChain.Add(target);
            Fx.Lightning(from, target.Center, new Color(1f, 0.15f, 0.25f), 0.2f);
            Fx.Flash(target.Center, 0.35f, new Color(1f, 0.2f, 0.3f, 0.8f));
            bool crit = s.critChance > 0 && Random.value < s.critChance;
            float amount = dmg * (crit ? s.critMultiplier : 1) * (target.IsElite ? Mathf.Max(1, s.bossDamageMultiplier) : 1);
            if (s.executeThreshold > 0 && !target.IsBoss && !target.IsElite && target.Hp - amount <= target.MaxHp * s.executeThreshold)
            {
                amount = target.Hp + 1;
                Fx.Burst(target.Center, new Color(0.8f, 0.05f, 0.1f), 14, 2.6f, 0.5f, -5f);
                Fx.Text(target.Center + Vector2.up * 0.6f, "EXECUTADO", new Color(1f, 0.3f, 0.3f), 0.9f);
            }
            target.Damage(new DamageInfo(amount, s.damageType, t.data.displayName, s.armorPenetration, crit));
            target.ApplyEffects(s, t.data.displayName);
            from = target.Center;
            dmg *= s.chainFalloff > 0 ? s.chainFalloff : 0.7f;
            target = s.chainCount > jump ? TowerTargeting.NearestTo(target.transform.position, s.chainRange, hitChain) : null;
        }
        t.Kick();
        AudioManager.Play(t.data.fireSfx, t.Pos);
    }

    // War Chapel: its effect is applied by the towers/commander/enemies that query it; here only the visual pulse.
    void Aura(float dt)
    {
        if ((pulse -= dt) > 0) return;
        pulse = 2.4f;
        Fx.Ring(t.Pos, t.stats.auraRadius, new Color(0.95f, 0.8f, 0.45f, 0.5f), 1.2f, Gfx.OrderOverlay + 1);
        var c = Battle.I.commander;
        if (c && c.Alive && Vector2.Distance(c.transform.position, t.Pos) <= t.stats.auraRadius && c.hp < c.maxHp)
        {
            Fx.Burst(c.transform.position + Vector3.up * 1.2f, new Color(0.95f, 0.85f, 0.45f), 6, 0.6f, 0.8f, 1.5f);
            AudioManager.Play("chapel_aura", t.Pos, 0.6f);
        }
    }

    // Spike Trap: hits whatever walks over it. Every enemy hit wears the spikes down (elites 2, bosses 4);
    // at zero durability the trap breaks.
    void Trap(float dt)
    {
        if ((cooldown -= dt) > 0) return;
        var s = t.Effective();
        int wear = 0;
        foreach (var e in Battle.I.EnemiesIn(t.Pos, t.data.footprintRadius + 0.15f))
        {
            e.Damage(new DamageInfo(s.damage * (e.IsElite ? Mathf.Max(1, s.bossDamageMultiplier) : 1), s.damageType, t.data.displayName, s.armorPenetration));
            e.ApplyEffects(s, t.data.displayName);
            Fx.Blood(e.Center, 0.8f);
            wear += e.IsBoss ? 4 : e.IsElite ? 2 : 1;
        }
        if (wear == 0) return;
        cooldown = s.attackInterval;
        t.Kick();
        AudioManager.Play(t.data.fireSfx, t.Pos);
        t.Wear(wear);
    }
}
