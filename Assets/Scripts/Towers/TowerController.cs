using UnityEngine;
using UnityEngine.Rendering.Universal;

// A built tower: tier, invested gold, visuals (sprite per tier, selection, range), buffs it receives.
// What it does each frame lives in TowerCombat.
[RequireComponent(typeof(TowerCombat))]
public class TowerController : MonoBehaviour
{
    public TowerData data;
    public int tier = 1, invested, builtAtWave;
    public TowerStats stats;
    public float stunLeft;
    public int durability;                 // traps only: wears down as enemies step on it

    public TowerUpgradeData NextUpgrade => tier - 1 < data.upgrades.Count ? data.upgrades[tier - 1] : null;
    public int MaxTier => data.MaxTier;
    public bool IsSupport => data.kind == TowerKind.Aura;
    public bool IsTrap => data.kind == TowerKind.Trap;
    public string TierName => tier == 1 ? data.displayName : data.upgrades[tier - 2].displayName;
    public Vector2 Pos => transform.position;
    public float MuzzleHeight => tier > 1 && data.upgrades[tier - 2].muzzleHeight > 0 ? data.upgrades[tier - 2].muzzleHeight : data.muzzleHeight;
    public Vector2 Muzzle => Pos + Vector2.up * MuzzleHeight;
    public TowerCombat combat;

    SpriteRenderer body, ring, range;
    Bar wear;
    SpriteRenderer[] pips;
    Light2D glow;
    float punch, buildAnim;
    bool selected;
    GameObject stunFx;

    public void Init(TowerData d, Vector2 at, int waveIndex)
    {
        data = d;
        stats = d.stats;
        invested = d.cost;
        builtAtWave = waveIndex;
        durability = d.stats.trapDurability;
        transform.position = at;
        name = d.displayName;
        bool trap = d.kind == TowerKind.Trap;
        body = Gfx.Renderer(transform, "Body", d.sprite, new Vector2(0, trap ? -0.35f : 0), trap ? Gfx.OrderDecal + 3 : Gfx.OrderWorld);
        if (!trap) Gfx.Shadow(transform, d.footprintRadius * 2.4f);
        int w = Mathf.RoundToInt(d.footprintRadius * 2.6f * Gfx.PPU);
        ring = Gfx.Renderer(transform, "Ring", Gfx.Ellipse(w, Mathf.Max(6, w / 2), new Color32(242, 163, 58, 30), new Color32(242, 163, 58, 255)), Vector2.zero, Gfx.OrderOverlay + 1);
        range = Gfx.Renderer(transform, "Range", null, Vector2.zero, Gfx.OrderOverlay);
        ring.enabled = range.enabled = false;
        pips = new SpriteRenderer[MaxTier];
        for (int i = 0; i < MaxTier; i++)
            pips[i] = Gfx.Renderer(transform, "Pip", Gfx.Pip, new Vector2((i - (MaxTier - 1) / 2f) * 0.19f, trap ? -0.75f : -0.3f), Gfx.OrderBars);
        if (d.lightRadius > 0)
        {
            glow = Gfx.Light(transform, new Vector2(0, MuzzleHeight * 0.75f), d.lightColor, d.lightRadius, 0.9f);
            glow.gameObject.AddComponent<Flicker>();
        }
        combat = GetComponent<TowerCombat>();
        combat.Init(this);
        if (trap && durability > 0) wear = new Bar(transform, new Vector2(0, 0.42f), 0.8f, new Color(0.62f, 0.45f, 0.25f));
        RefreshPips();
        buildAnim = 0.35f;
        Fx.Dust(at, 1.2f);
    }

    public void Upgrade()
    {
        var u = NextUpgrade;
        invested += u.cost;
        stats = u.stats;
        tier++;
        durability = stats.trapDurability;   // an upgrade rebuilds the spikes
        if (wear != null) wear.Set(1, true);
        body.sprite = data.SpriteAt(tier);
        if (glow)
        {
            glow.pointLightOuterRadius = data.lightRadius * (1 + 0.15f * (tier - 1));
            glow.transform.localPosition = new Vector2(0, MuzzleHeight * 0.75f);
        }
        RefreshPips();
        punch = 0.3f;
        Fx.Burst(Pos + Vector2.up * 0.8f, new Color(1f, 0.8f, 0.4f), 16, 2.2f, 0.7f, 1.5f);
        Fx.Ring(Pos, data.footprintRadius * 1.6f, new Color(1f, 0.75f, 0.35f), 0.4f);
        if (selected) Select(true);
    }

    void RefreshPips()
    {
        for (int i = 0; i < pips.Length; i++)
            pips[i].color = i < tier ? new Color32(242, 163, 58, 255) : new Color32(60, 56, 64, 255);
    }

    public void Select(bool on)
    {
        selected = on;
        ring.enabled = range.enabled = on;
        if (!on) return;
        bool aura = IsSupport;
        float r = aura ? stats.auraRadius : Effective().range;
        range.sprite = aura
            ? Gfx.Circle(r, new Color32(168, 137, 74, 26), new Color32(210, 170, 90, 210))
            : Gfx.DashedCircle(r, new Color32(232, 220, 192, 200), new Color32(232, 220, 192, 16));
        if (IsTrap) range.transform.localScale = new Vector3(1, 0.6f, 1);
    }

    public void Stun(float seconds)
    {
        if (IsTrap) return;
        stunLeft = Mathf.Max(stunLeft, seconds);
        if (!stunFx)
        {
            stunFx = new GameObject("Stun");
            stunFx.transform.SetParent(transform, false);
            stunFx.transform.localPosition = new Vector2(0, body.sprite ? body.sprite.bounds.max.y + 0.15f : 2f);
            var sr = stunFx.AddComponent<SpriteRenderer>();
            sr.sprite = Gfx.Circle(0.18f, new Color32(80, 160, 60, 160), new Color32(140, 230, 90, 255));
            sr.sortingOrder = Gfx.OrderBars;
        }
        stunFx.SetActive(true);
    }

    // Stats after War Chapel auras (strongest chapel wins; no stacking) and the commander's Tactics.
    public TowerStats Effective(out float damageBonus, out float speedBonus, out float rangeBonus)
    {
        damageBonus = speedBonus = rangeBonus = 0;
        var b = Battle.I;
        if (b)
            foreach (var t in b.towers)
                if (t != this && t.IsSupport && Vector2.Distance(t.Pos, Pos) <= t.stats.auraRadius)
                {
                    damageBonus = Mathf.Max(damageBonus, t.stats.auraDamageBonus);
                    speedBonus = Mathf.Max(speedBonus, t.stats.auraAttackSpeedBonus);
                    rangeBonus = Mathf.Max(rangeBonus, t.stats.auraRangeBonus);
                }
        var s = stats;
        if (IsSupport) return s;
        float tactics = b ? b.TacticsBonusAt(Pos) : 0;
        damageBonus += tactics;
        s.damage *= 1 + damageBonus;
        s.burnDps *= 1 + damageBonus;
        s.bleedDps *= 1 + damageBonus;
        s.groundFireDps *= 1 + damageBonus;
        s.attackInterval /= 1 + speedBonus;
        if (!IsTrap) s.range *= 1 + rangeBonus;
        return s;
    }

    public TowerStats Effective() => Effective(out _, out _, out _);

    public bool Contains(Vector2 p)
    {
        float top = body.sprite ? body.sprite.bounds.size.y : 2f;
        return Mathf.Abs(p.x - Pos.x) < data.footprintRadius + 0.2f && p.y > Pos.y - data.footprintRadius * 0.8f && p.y < Pos.y + Mathf.Max(top * 0.9f, 0.6f);
    }

    public void Kick() => punch = Mathf.Max(punch, 0.12f);

    public float DurabilityFraction => stats.trapDurability > 0 ? durability / (float)stats.trapDurability : 1;

    // Traps: every enemy hit costs durability; at zero the spikes break and the trap is gone.
    public void Wear(int amount)
    {
        if (!IsTrap || stats.trapDurability <= 0) return;
        durability = Mathf.Max(0, durability - amount);
        wear?.Set(DurabilityFraction);
        body.color = Color.Lerp(new Color(0.55f, 0.5f, 0.45f), Color.white, DurabilityFraction);
        if (durability == 0) Battle.I.placement.Break(this);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (stunLeft > 0)
        {
            stunLeft -= dt;
            if (stunLeft <= 0 && stunFx) stunFx.SetActive(false);
            body.color = new Color(0.7f, 0.85f, 0.7f);
        }
        else if (!IsTrap) body.color = Color.white;
        wear?.Tick(dt);
        if (selected && !IsSupport && Time.frameCount % 15 == 0) Select(true);   // aura buffs can change the range
        // build: rises from the ground; upgrade/fire: small squash
        if (buildAnim > 0)
        {
            buildAnim -= dt;
            float k = 1 - Mathf.Clamp01(buildAnim / 0.35f);
            body.transform.localScale = new Vector3(1, Mathf.Lerp(0.2f, 1f, k * k * (3 - 2 * k)), 1);
        }
        else if (punch > 0)
        {
            punch -= dt;
            float k = Mathf.Sin(punch / 0.3f * Mathf.PI) * 0.06f;
            body.transform.localScale = new Vector3(1 + k, 1 - k, 1);
        }
        else body.transform.localScale = Vector3.one;
    }
}
