using System.Collections.Generic;
using UnityEngine;

// Complete stat block of one tower tier (tiers store full values, never multipliers, so each tower grows its own way).
// Unused fields stay 0. Projectile towers use the attack + on-hit groups, Chain towers add the chain group,
// Aura towers the aura group, Traps the trap group.
[System.Serializable]
public struct TowerStats
{
    [Header("Ataque")]
    public float damage;
    public DamageType damageType;
    [Tooltip("Segundos entre ataques (attack rate = 1 / intervalo)")] public float attackInterval;
    public float range;
    [Tooltip("Velocidade do projétil; 0 = a do ProjectileData")] public float projectileSpeed;
    [Tooltip("Armadura ignorada por golpe")] public float armorPenetration;
    [Tooltip("Raio de dano em área; 0 = alvo único")] public float splashRadius;
    [Tooltip("Alvos por ataque (multishot)")] public int targets;
    [Tooltip("Inimigos extras atravessados pelo projétil")] public int pierce;
    [Range(0, 1)] public float critChance;
    public float critMultiplier;
    [Tooltip("Multiplicador contra chefes e elites")] public float bossDamageMultiplier;
    [Range(0, 1)] public float stunChance;
    public float stunDuration;

    [Header("Efeitos ao acertar")]
    public float burnDps;
    public float burnDuration;
    public float bleedDps;
    public float bleedDuration;
    [Tooltip("0.3 = 30% mais lento")] [Range(0, 0.9f)] public float slowAmount;
    public float slowDuration;
    [Tooltip("Maldição: +% de dano sofrido")] public float vulnerability;
    public float curseDuration;
    [Tooltip("Armadura removida enquanto amaldiçoado")] public float armorShred;
    [Tooltip("Maldição pula para inimigos próximos quando o alvo morre")] public bool curseSpreads;
    public float groundFireDps;
    public float groundFireDuration;
    public float groundFireRadius;

    [Header("Corrente / execução")]
    public int chainCount;
    public float chainRange;
    [Tooltip("Dano mantido a cada salto (0.7 = perde 30%)")] public float chainFalloff;
    [Tooltip("Executa inimigos comuns abaixo desta fração de HP")] [Range(0, 0.5f)] public float executeThreshold;

    [Header("Suporte (aura)")]
    public float auraRadius;
    public float auraDamageBonus;
    public float auraAttackSpeedBonus;
    public float auraRangeBonus;
    public float auraArmorShred;
    public float commanderHealPerSecond;

    [Header("Armadilha")]
    [Tooltip("Durabilidade: cada inimigo atingido gasta 1 (elite 2, chefe 4); em 0 a armadilha quebra. Melhorar restaura.")]
    public int trapDurability;

    public float AttacksPerSecond => attackInterval > 0 ? 1f / attackInterval : 0;
}

[CreateAssetMenu(menuName = "Bastião/Torre")]
public class TowerData : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea] public string role;
    [Tooltip("Ex.: 'Bom contra armadura'")] [TextArea] public string goodAgainst;
    public int cost;
    public Sprite icon;
    [Tooltip("Tier 1")] public Sprite sprite;
    public TowerKind kind;
    public PlacementRule placement;
    [Tooltip("Raio ocupado no chão (unidades)")] public float footprintRadius = 0.6f;
    [Tooltip("Altura de onde saem os projéteis (unidades, tier 1)")] public float muzzleHeight = 1.6f;
    public TargetPriority targeting;
    public ProjectileData projectile;
    public Color lightColor = Color.white;
    [Tooltip("0 = sem luz")] public float lightRadius;
    [Header("Áudio")] public string fireSfx;
    public string impactSfx;
    [Header("Tier 1")] public TowerStats stats;
    [Tooltip("Tiers 2, 3 e 4 (caminho de upgrade)")] public List<TowerUpgradeData> upgrades = new();

    public int MaxTier => upgrades.Count + 1;

    public TowerStats StatsAt(int tier) => tier <= 1 ? stats : upgrades[Mathf.Min(tier, MaxTier) - 2].stats;

    public Sprite SpriteAt(int tier)
    {
        for (int t = Mathf.Min(tier, MaxTier); t >= 2; t--)
            if (upgrades[t - 2].sprite) return upgrades[t - 2].sprite;
        return sprite;
    }
}
