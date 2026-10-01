using System.Collections.Generic;
using UnityEngine;

// Auto-attack: a wide greatsword swing on the enemies he holds first, then the closest ones in reach.
// The swing lands after a fixed wind-up (timer, independent of animation frames).
public class CommanderCombat : MonoBehaviour
{
    const float ClipLength = 0.8f, ImpactAt = 0.42f;   // the Attack clip's impact key (tools/rig_motion.py, commander)
    CommanderController c;
    float attackTimer, windup = -1, ultimateWindup = -1;
    readonly List<EnemyController> swing = new();

    public float UltimateCooldownLeft { get; private set; }
    public float FuryLeft { get; private set; }
    public bool UltimateReady => c.Alive && !c.Down && UltimateCooldownLeft <= 0 && ultimateWindup < 0;
    public float UltimateCharge => 1 - Mathf.Clamp01(UltimateCooldownLeft / Mathf.Max(0.01f, c.data.ultimateCooldown));
    float AttackInterval => c.stats.attackInterval / (FuryLeft > 0 ? 1 + c.data.ultimateAttackSpeedBonus : 1);
    float DamageMultiplier => FuryLeft > 0 ? 1 + c.data.ultimateDamageBonus : 1;
    public float DamageTakenMultiplier => FuryLeft > 0 ? 1 - c.data.ultimateDamageReduction : 1;

    public void Init(CommanderController owner)
    {
        c = owner;
        UltimateCooldownLeft = c.data.ultimateCooldown * (1 - c.data.ultimateStartCharge);
    }

    // The power: a two-handed spinning cleave around him (damage + stun), then a short black fury
    // (faster, harder swings, less damage taken). Timer-driven; the Special clip only shows it.
    public bool UseUltimate()
    {
        if (!UltimateReady || !Battle.I || Battle.I.over) return false;
        UltimateCooldownLeft = c.data.ultimateCooldown;
        ultimateWindup = 0.35f;
        windup = -1;
        c.ShowBack(false);
        c.view.Play("Special");
        Fx.Ring(transform.position, c.data.ultimateRadius * 0.6f, new Color(1f, 0.25f, 0.2f), 0.35f);
        AudioManager.Play("commander_ultimate", transform.position);
        Battle.I.Banner(c.data.ultimateName.ToUpperInvariant() + "!", 1.5f);
        return true;
    }

    void Unleash()
    {
        ultimateWindup = -1;
        Vector2 at = transform.position;
        float r = c.data.ultimateRadius;
        var hit = new List<EnemyController>(Battle.I.EnemiesIn(at, r));
        foreach (var e in hit)
        {
            float removed = e.Damage(new DamageInfo(c.stats.ultimateDamage, DamageType.Physical, c.data.displayName, c.data.ultimateArmorPenetration, true));
            c.damageDealt += removed;
            if (e.Alive && !e.IsBoss) e.status.ApplyStun(c.data.ultimateStunSeconds);
            Fx.Blood(e.Center, 0.8f);
        }
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            var p = at + new Vector2(Mathf.Cos(a) * r * 0.55f, Mathf.Sin(a) * r * 0.35f + 0.8f);
            Fx.Slash(p, Mathf.Cos(a) < 0, r * 0.6f, new Color(1f, 0.45f, 0.4f));
        }
        Fx.Ring(at, r, new Color(1f, 0.3f, 0.25f), 0.45f);
        Fx.Dust(at, 2.5f);
        CameraRig.Shake(0.25f);
        FuryLeft = c.data.ultimateBuffSeconds;
        c.view.SetTint(new Color(1f, 0.75f, 0.7f));
    }

    void Update()
    {
        var b = Battle.I;
        if (!b || b.over) return;
        float dt = Time.deltaTime;
        UltimateCooldownLeft = Mathf.Max(0, UltimateCooldownLeft - dt);
        if (FuryLeft > 0 && (FuryLeft -= dt) <= 0) c.view.SetTint(Color.white);
        if (!c.Alive) return;
        if (FuryLeft > 0 && Random.value < dt * 10) Fx.Burst((Vector2)transform.position + Vector2.up * Random.Range(0.3f, 1.8f), new Color(0.9f, 0.15f, 0.12f), 1, 0.6f, 0.5f, 2f);
        if (ultimateWindup >= 0)
        {
            if ((ultimateWindup -= dt) < 0) Unleash();
            return;
        }
        if (windup >= 0)
        {
            if ((windup -= dt) < 0) Strike();
            return;
        }
        if ((attackTimer -= dt) > 0 || PickSwing() == 0) return;
        float speed = Mathf.Clamp(ClipLength / Mathf.Max(0.3f, AttackInterval), 0.9f, 1.8f);
        attackTimer = AttackInterval;
        windup = ImpactAt / speed;
        c.ShowBack(false);
        c.view.Face(swing[0].transform.position.x - transform.position.x);
        c.view.Play("Attack", speed);
        AudioManager.Play("commander_swing", transform.position, 0.8f);
    }

    void Strike()
    {
        windup = -1;
        PickSwing();
        bool any = false;
        foreach (var e in swing)
        {
            if (!e || !e.Alive) continue;
            float removed = e.Damage(new DamageInfo(c.stats.damage * DamageMultiplier, DamageType.Physical, c.data.displayName, c.stats.armorPenetration));
            c.damageDealt += removed;
            Fx.Sparks(e.Center, new Color(1f, 0.9f, 0.7f));
            any = true;
        }
        Fx.Slash((Vector2)transform.position + new Vector2(c.view.FacingLeft ? -0.45f : 0.45f, 0.95f), c.view.FacingLeft, 1.25f, new Color(0.95f, 0.9f, 0.8f));
        if (any)
        {
            AudioManager.Play("commander_attack", transform.position);
            CameraRig.Shake(0.02f);
        }
    }

    int PickSwing()
    {
        swing.Clear();
        foreach (var e in c.engaged)
            if (e && e.Alive && swing.Count < c.stats.cleave) swing.Add(e);
        Vector2 pos = transform.position;
        while (swing.Count < c.stats.cleave)
        {
            EnemyController best = null;
            float bestD = c.stats.reach + 0.001f;
            foreach (var e in Battle.I.enemies)
            {
                if (!e || !e.Alive || swing.Contains(e)) continue;
                float d = Vector2.Distance(pos, e.transform.position) - e.data.bodyRadius * 0.5f;
                if (d <= bestD) { best = e; bestD = d; }
            }
            if (!best) break;
            swing.Add(best);
        }
        return swing.Count;
    }
}
