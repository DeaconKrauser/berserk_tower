using UnityEngine;

public struct DamageInfo
{
    public float amount;
    public DamageType type;
    public float armorPenetration;
    public string source;
    public bool crit;
    public bool isDot;

    public DamageInfo(float amount, DamageType type, string source, float penetration = 0, bool crit = false, bool dot = false)
    {
        this.amount = amount;
        this.type = type;
        this.source = source;
        armorPenetration = penetration;
        this.crit = crit;
        isDot = dot;
    }
}

// The single place where damage meets armor and resistances (enemies, commander).
public static class DamageSystem
{
    // Physical: flat armor per hit (minus penetration), never below minPhysical of the hit, then resistance.
    // Fire / Magic / Bleed ignore armor; only resistance applies. Vulnerability (curses) multiplies everything.
    public static float Resolve(float amount, DamageType type, float armor, float penetration, float resistance, float vulnerability, float minPhysical)
    {
        if (amount <= 0) return 0;
        float dealt = amount;
        if (type == DamageType.Physical)
            dealt = Mathf.Max(amount * minPhysical, amount - Mathf.Max(0, armor - penetration));
        dealt *= 1 - Mathf.Clamp(resistance, -1f, 0.95f);
        dealt *= 1 + Mathf.Max(0, vulnerability);
        return Mathf.Max(0, dealt);
    }

    public static Color ColorOf(DamageType t) => t switch
    {
        DamageType.Fire => new Color(0.9f, 0.45f, 1f),
        DamageType.Magic => new Color(1f, 0.25f, 0.3f),
        DamageType.Bleed => new Color(0.7f, 0.08f, 0.08f),
        _ => new Color(0.92f, 0.88f, 0.8f),
    };
}

// HP + shield with events, shared by enemies and the commander.
public class Health
{
    public float max, current, shield;
    public bool Alive => current > 0;
    public float Fraction => max > 0 ? current / max : 0;

    public Health(float max)
    {
        this.max = current = max;
    }

    // Returns the HP actually removed (shield absorbs first).
    public float Take(float amount)
    {
        if (!Alive || amount <= 0) return 0;
        if (shield > 0)
        {
            float s = Mathf.Min(shield, amount);
            shield -= s;
            amount -= s;
        }
        float hp = Mathf.Min(current, amount);
        current -= hp;
        return hp;
    }

    public void Heal(float amount)
    {
        if (Alive) current = Mathf.Min(max, current + amount);
    }
}

// Timed effects on an enemy. The strongest instance of each kind wins (no infinite stacking).
public class StatusEffects
{
    public float burnDps, burnLeft, bleedDps, bleedLeft, slow, slowLeft, vulnerability, armorShred, curseLeft, stunLeft;
    public bool curseSpreads;
    public string burnSource, bleedSource, curseSource;

    public bool Cursed => curseLeft > 0;
    public float SlowFactor => slowLeft > 0 ? 1 - slow : 1;
    public float Vulnerability => curseLeft > 0 ? vulnerability : 0;
    public float ArmorShred => curseLeft > 0 ? armorShred : 0;

    public void ApplyBurn(float dps, float seconds, string source)
    {
        if (dps <= 0) return;
        burnDps = burnLeft > 0 ? Mathf.Max(burnDps, dps) : dps;
        burnLeft = Mathf.Max(burnLeft, seconds);
        burnSource = source;
    }

    public void ApplyBleed(float dps, float seconds, string source)
    {
        if (dps <= 0) return;
        bleedDps = bleedLeft > 0 ? Mathf.Max(bleedDps, dps) : dps;
        bleedLeft = Mathf.Max(bleedLeft, seconds);
        bleedSource = source;
    }

    public void ApplySlow(float amount, float seconds)
    {
        if (amount <= 0) return;
        slow = slowLeft > 0 ? Mathf.Max(slow, amount) : amount;
        slowLeft = Mathf.Max(slowLeft, seconds);
    }

    public void ApplyCurse(float vuln, float shred, float seconds, bool spreads, string source)
    {
        if (seconds <= 0) return;
        vulnerability = curseLeft > 0 ? Mathf.Max(vulnerability, vuln) : vuln;
        armorShred = curseLeft > 0 ? Mathf.Max(armorShred, shred) : shred;
        curseSpreads |= spreads;
        curseLeft = Mathf.Max(curseLeft, seconds);
        curseSource = source;
    }

    public void ApplyStun(float seconds) => stunLeft = Mathf.Max(stunLeft, seconds);

    public void Tick(float dt)
    {
        burnLeft -= dt;
        bleedLeft -= dt;
        slowLeft -= dt;
        curseLeft -= dt;
        stunLeft -= dt;
        if (curseLeft <= 0) curseSpreads = false;
    }
}
