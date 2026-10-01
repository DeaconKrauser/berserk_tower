using System.Collections.Generic;
using UnityEngine;

// Auto-attack: a wide greatsword swing on the enemies he holds first, then the closest ones in reach.
// The swing lands after a fixed wind-up (timer, independent of animation frames).
// [Q] is the equipped skin's power (CommanderSkinData.power); every power scales with Strength via stats.ultimateDamage.
public class CommanderCombat : MonoBehaviour
{
    const float ClipLength = 0.8f, ImpactAt = 0.42f;   // the Attack clip's impact key (tools/rig_motion.py, commander/swordsman)
    // Armadura do Abismo: up to ChargeHits dashes, each to the nearest enemy not hit yet within ChargeSearch of him
    const int ChargeHits = 6;
    const float ChargeSearch = 4.5f, ChargeSpeed = 20f, ChargeDamage = 0.6f, ChargeStun = 0.5f;
    // Estandarte Escarlate: towers inside the radius hit harder and faster while it stands; he heals at once
    const float BannerRadius = 4f, BannerSeconds = 10f, BannerDamage = 0.35f, BannerSpeed = 0.25f, BannerHeal = 0.35f;
    // Eclipse: magic burst that curses (+damage taken, -armor) and slows everything in the radius, bosses too
    const float EclipseRadius = 3.6f, EclipseSeconds = 6f, EclipseDamage = 0.6f, EclipseVuln = 0.3f, EclipseShred = 6f, EclipseSlow = 0.45f;

    CommanderController c;
    float attackTimer, windup = -1, ultimateWindup = -1;
    readonly List<EnemyController> swing = new();
    readonly List<EnemyController> charged = new();
    EnemyController chargeTarget;
    int chargesLeft;
    float chargePause;
    GameObject banner;

    public float UltimateCooldownLeft { get; private set; }
    public float FuryLeft { get; private set; }
    public Vector2 BannerAt { get; private set; }
    public float BannerLeft { get; private set; }
    public bool Charging => chargesLeft > 0;
    public bool UltimateReady => c.Alive && !c.Down && UltimateCooldownLeft <= 0 && ultimateWindup < 0 && !Charging;
    public float UltimateCharge => 1 - Mathf.Clamp01(UltimateCooldownLeft / Mathf.Max(0.01f, PowerCooldown));
    float AttackInterval => c.stats.attackInterval / (FuryLeft > 0 ? 1 + c.data.ultimateAttackSpeedBonus : 1);
    float DamageMultiplier => FuryLeft > 0 ? 1 + c.data.ultimateDamageBonus : 1;
    public float DamageTakenMultiplier => FuryLeft > 0 ? 1 - c.data.ultimateDamageReduction : 1;

    public CommanderPower Power => c.skin ? c.skin.power : CommanderPower.BlackFury;
    public string PowerName => PowerNameOf(c.skin, c.data);
    public string PowerDescription => c.skin && !string.IsNullOrEmpty(c.skin.powerDescription) ? c.skin.powerDescription : c.data.ultimateDescription;
    public float PowerCooldown => CooldownOf(c.skin, c.data);

    public static string PowerNameOf(CommanderSkinData s, CommanderData d) => s && !string.IsNullOrEmpty(s.powerName) ? s.powerName : d.ultimateName;
    public static float CooldownOf(CommanderSkinData s, CommanderData d) => s && s.powerCooldown > 0 ? s.powerCooldown : d.ultimateCooldown;

    // Bonus the Scarlet banner gives a tower standing at p (TowerController.Effective).
    public float BannerDamageBonus(Vector2 p) => BannerLeft > 0 && Vector2.Distance(p, BannerAt) <= BannerRadius ? BannerDamage : 0;
    public float BannerSpeedBonus(Vector2 p) => BannerLeft > 0 && Vector2.Distance(p, BannerAt) <= BannerRadius ? BannerSpeed : 0;

    public void Init(CommanderController owner)
    {
        c = owner;
        UltimateCooldownLeft = PowerCooldown * (1 - c.data.ultimateStartCharge);
    }

    // Starts the power: a short wind-up (the Special clip shows it), then Unleash. Timer-driven, like the swings.
    public bool UseUltimate()
    {
        if (!UltimateReady || !Battle.I || Battle.I.over) return false;
        UltimateCooldownLeft = PowerCooldown;
        windup = -1;
        c.ShowBack(false);
        c.view.Play("Special");
        Color tell = Power switch
        {
            CommanderPower.AbyssArmor => new Color(0.55f, 0.08f, 0.1f),
            CommanderPower.ScarletBanner => new Color(1f, 0.3f, 0.25f),
            CommanderPower.Eclipse => new Color(0.6f, 0.3f, 1f),
            _ => new Color(1f, 0.25f, 0.2f),
        };
        ultimateWindup = Power == CommanderPower.Eclipse ? 0.45f : Power == CommanderPower.ScarletBanner ? 0.3f : 0.35f;
        Fx.Ring(transform.position, 1.5f, tell, 0.35f);
        AudioManager.Play("commander_ultimate", transform.position);
        Battle.I.Banner(PowerName.ToUpperInvariant() + "!", 1.5f);
        return true;
    }

    void Unleash()
    {
        ultimateWindup = -1;
        switch (Power)
        {
            case CommanderPower.AbyssArmor: AbyssArmor(); break;
            case CommanderPower.ScarletBanner: PlantBanner(); break;
            case CommanderPower.Eclipse: Eclipse(); break;
            default: BlackFury(); break;
        }
    }

    // Ulric: a two-handed spinning cleave around him (damage + stun), then a short black fury
    // (faster, harder swings, less damage taken).
    void BlackFury()
    {
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

    // Espadachim Negro: the cursed armour closes over him (same fury buff as Ulric's, darker), then he
    // dashes through the nearest enemies one after another, cutting each on arrival.
    void AbyssArmor()
    {
        Vector2 at = transform.position;
        FuryLeft = c.data.ultimateBuffSeconds;
        c.view.SetTint(new Color(0.6f, 0.42f, 0.45f));
        Fx.Flash(at + Vector2.up * 1.1f, 0.55f, new Color(0.7f, 0.05f, 0.08f, 0.6f), 0.15f);
        Fx.Ring(at, 1.1f, new Color(0.75f, 0.08f, 0.1f), 0.3f);
        Fx.Burst(at + Vector2.up * 1.1f, new Color(0.08f, 0.05f, 0.07f), 22, 2.2f, 0.5f, 0f);
        Fx.Burst(at + Vector2.up * 1.1f, new Color(0.85f, 0.1f, 0.12f), 12, 1.8f, 0.5f, 1f);
        CameraRig.Shake(0.12f);
        charged.Clear();
        chargeTarget = null;
        chargePause = 0.15f;
        chargesLeft = ChargeHits;
    }

    void Charge(float dt)
    {
        c.movement.target = transform.position;              // the dash owns his position until it ends
        if ((chargePause -= dt) > 0) return;
        if (!chargeTarget || !chargeTarget.Alive)
        {
            chargeTarget = NextChargeTarget();
            if (!chargeTarget)
            {
                EndCharge();
                return;
            }
            c.ShowBack(false);
            c.view.Face(chargeTarget.transform.position.x - transform.position.x);
        }
        Vector2 pos = transform.position, to = (Vector2)chargeTarget.transform.position - pos;
        float step = ChargeSpeed * dt, reach = 0.5f + chargeTarget.data.bodyRadius * 0.5f;
        Fx.Burst(pos + Vector2.up * Random.Range(0.4f, 1.6f), new Color(0.1f, 0.05f, 0.07f), 2, 0.3f, 0.3f, 0f);
        Fx.Burst(pos + Vector2.up * Random.Range(0.4f, 1.6f), new Color(0.75f, 0.08f, 0.1f), 1, 0.3f, 0.25f, 0f);
        if (to.magnitude > reach + step)
        {
            transform.position = MapController.ClampWalk(pos + to.normalized * step);
            c.movement.target = transform.position;
            return;
        }
        var e = chargeTarget;
        float removed = e.Damage(new DamageInfo(c.stats.ultimateDamage * ChargeDamage, DamageType.Physical, c.data.displayName, c.data.ultimateArmorPenetration, true));
        c.damageDealt += removed;
        if (e.Alive && !e.IsBoss) e.status.ApplyStun(ChargeStun);
        Fx.Slash((Vector2)transform.position + new Vector2(c.view.FacingLeft ? -0.45f : 0.45f, 0.95f), c.view.FacingLeft, 1.35f, new Color(0.85f, 0.15f, 0.15f));
        Fx.Blood(e.Center, 1f);
        AudioManager.Play("commander_attack", transform.position);
        CameraRig.Shake(0.05f);
        c.view.Play("Attack", 2.2f);
        charged.Add(e);
        chargeTarget = null;
        chargePause = 0.14f;
        if (--chargesLeft <= 0) EndCharge();
    }

    EnemyController NextChargeTarget()
    {
        EnemyController best = null;
        float bestD = ChargeSearch;
        Vector2 pos = transform.position;
        foreach (var e in Battle.I.enemies)
        {
            if (!e || !e.Alive || charged.Contains(e)) continue;
            float d = Vector2.Distance(pos, e.transform.position);
            if (d <= bestD) { best = e; bestD = d; }
        }
        return best;
    }

    void EndCharge()
    {
        chargesLeft = 0;
        chargeTarget = null;
        c.movement.target = transform.position;
    }

    // Veterano Escarlate: plants a war banner where he stands. Towers near it fire harder and faster; he heals.
    void PlantBanner()
    {
        Vector2 at = MapController.ClampWalk(transform.position + new Vector3(c.view.FacingLeft ? 0.95f : -0.95f, 0.3f));   // beside and behind him, never hidden by his body
        BannerAt = at;
        BannerLeft = BannerSeconds;
        if (banner) Destroy(banner);
        banner = new GameObject("ScarletBanner");
        banner.transform.SetParent(Battle.I.transform, false);
        banner.transform.position = at;
        Gfx.Renderer(banner.transform, "Flag", Gfx.WarBanner, Vector2.zero, Gfx.OrderWorld);
        Gfx.Shadow(banner.transform, 0.5f);
        Gfx.Light(banner.transform, new Vector2(0, 1.2f), new Color(1f, 0.35f, 0.25f), 2.2f, 0.8f).gameObject.AddComponent<Flicker>();
        c.HealFraction(BannerHeal);
        Fx.Ring(at, BannerRadius, new Color(1f, 0.35f, 0.3f, 0.3f), 0.5f, Gfx.OrderOverlay + 1);
        Fx.Burst((Vector2)transform.position + Vector2.up * 1.1f, new Color(1f, 0.85f, 0.5f), 14, 1.2f, 0.8f, 1.5f);
        Fx.Dust(at, 1.2f);
        CameraRig.Shake(0.08f);
    }

    // Cavaleiro do Eclipse: a black sun bursts around him.
    void Eclipse()
    {
        Vector2 at = transform.position;
        foreach (var e in new List<EnemyController>(Battle.I.EnemiesIn(at, EclipseRadius)))
        {
            float removed = e.Damage(new DamageInfo(c.stats.ultimateDamage * EclipseDamage, DamageType.Magic, c.data.displayName, 0, true));
            c.damageDealt += removed;
            if (!e.Alive) continue;
            e.status.ApplyCurse(EclipseVuln, EclipseShred, EclipseSeconds, false, PowerName);
            e.status.ApplySlow(EclipseSlow, EclipseSeconds);
            Fx.Souls(e.Center, new Color(0.65f, 0.35f, 1f));
        }
        Fx.Flash(at + Vector2.up * 0.6f, EclipseRadius * 0.45f, new Color(0.12f, 0.02f, 0.2f, 0.45f), 0.25f);
        Fx.Ring(at, EclipseRadius, new Color(0.7f, 0.4f, 1f, 0.35f), 0.5f);
        Fx.Ring(at, EclipseRadius * 0.55f, new Color(0.35f, 0.1f, 0.6f, 0.35f), 0.4f);
        Fx.Burst(at + Vector2.up * 1.2f, new Color(0.55f, 0.25f, 0.95f), 24, 3.2f, 0.7f, 0f);
        CameraRig.Shake(0.2f);
    }

    void Update()
    {
        var b = Battle.I;
        if (!b || b.over) return;
        float dt = Time.deltaTime;
        UltimateCooldownLeft = Mathf.Max(0, UltimateCooldownLeft - dt);
        if (FuryLeft > 0 && (FuryLeft -= dt) <= 0) c.view.SetTint(Color.white);
        if (BannerLeft > 0 && (BannerLeft -= dt) <= 0 && banner) Destroy(banner);
        if (!c.Alive)
        {
            EndCharge();
            return;
        }
        if (FuryLeft > 0 && Random.value < dt * 10)
            Fx.Burst((Vector2)transform.position + Vector2.up * Random.Range(0.3f, 1.8f),
                Power == CommanderPower.AbyssArmor ? new Color(0.12f, 0.04f, 0.06f) : new Color(0.9f, 0.15f, 0.12f), 1, 0.6f, 0.5f, 2f);
        if (ultimateWindup >= 0)
        {
            if ((ultimateWindup -= dt) < 0) Unleash();
            return;
        }
        if (Charging)
        {
            Charge(dt);
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
