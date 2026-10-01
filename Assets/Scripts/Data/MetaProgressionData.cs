using UnityEngine;

// Essence rewards and run scaling. Read by RunManager; nothing here is hardcoded in gameplay code.
[CreateAssetMenu(menuName = "Bastião/Meta-progressão")]
public class MetaProgressionData : ScriptableObject
{
    [Header("Essência por mapa")]
    public float perKill = 0.1f;
    public float perEliteKill = 1f;
    public float perBossKill = 15f;
    public float perWaveCleared = 2f;
    public float mapVictory = 25f;
    [Tooltip("+% por nível de dificuldade da run")] public float perRunTierBonus = 0.25f;
    [Tooltip("Fração mantida em derrota (1 = nada se perde)")] [Range(0, 1)] public float defeatMultiplier = 1f;
    [Tooltip("Repetir uma fase (mapa + etapa) já vencida rende esta fração")] [Range(0, 1)] public float replayMultiplier = 0.35f;

    [Header("Transição entre mapas")]
    [Range(0, 1)] public float goldCarryFraction = 0.25f;
    public int goldCarryMax = 150;
    public int transitionBonusPerTier = 40;

    [Header("Dificuldade por mapa concluído na run (RunDifficultyMultiplier)")]
    public float enemyHpPerTier = 0.22f;
    public float enemyDamagePerTier = 0.15f;
    [Tooltip("Mais unidades por grupo")] public float spawnCountPerTier = 0.12f;
    [Tooltip("Intervalo entre unidades encolhe")] public float spawnIntervalPerTier = 0.08f;
    [Tooltip("Chance de um inimigo comum virar a versão elite do grupo")] public float eliteChancePerTier = 0.06f;
    public float bossHpPerTier = 0.3f;
    public int bossExtraSummonsPerTier = 1;
    public float goldRewardPerTier = 0.05f;
    public int maxTier = 6;
}
