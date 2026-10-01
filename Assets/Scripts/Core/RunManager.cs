using System.Collections.Generic;
using UnityEngine;

// Result of one map inside a run (shown on the results screen, fed to Essence).
public class MapResult
{
    public MapData map;
    public RouteData route;
    public bool victory;
    public int tier;
    public int kills, eliteKills, bossKills, wavesCleared, wavesTotal;
    public int goldEarned, goldSpent, goldLeft;
    public int towersBuilt, towersSold, upgrades, trapsBroken;
    public float commanderDamage;
    public int commanderFalls;
    public int fortressHp, fortressHpMax;
    public float seconds;
    public string endReason;
    public int essence;
    public bool replay;       // this phase had been won before: reduced Essence
    public readonly Dictionary<string, float> damageBySource = new();
}

// Difficulty of the current stage of the run. Tier = maps already won in this run.
public readonly struct RunDifficulty
{
    public readonly int tier;
    public readonly float enemyHp, enemyDamage, spawnCount, spawnInterval, eliteChance, bossHp, goldReward;
    public readonly int bossExtraSummons;

    public RunDifficulty(MetaProgressionData m, int tier)
    {
        this.tier = tier = Mathf.Clamp(tier, 0, m ? m.maxTier : 0);
        if (!m)
        {
            enemyHp = enemyDamage = spawnCount = spawnInterval = bossHp = goldReward = 1;
            eliteChance = 0;
            bossExtraSummons = 0;
            return;
        }
        enemyHp = 1 + m.enemyHpPerTier * tier;
        enemyDamage = 1 + m.enemyDamagePerTier * tier;
        spawnCount = 1 + m.spawnCountPerTier * tier;
        spawnInterval = 1 / (1 + m.spawnIntervalPerTier * tier);
        eliteChance = m.eliteChancePerTier * tier;
        bossHp = 1 + m.bossHpPerTier * tier;
        bossExtraSummons = m.bossExtraSummonsPerTier * tier;
        goldReward = 1 + m.goldRewardPerTier * tier;
    }

    public override string ToString() =>
        tier == 0 ? "Normal" : $"Etapa {tier + 1}: HP ×{enemyHp:0.00}, dano ×{enemyDamage:0.00}, densidade ×{spawnCount:0.00}, elites {eliteChance * 100:0}%";
}

// Lives only in memory: a run is lost when the game closes, by design.
public class RunManager
{
    readonly GameConfig config;
    readonly SaveSystem save;

    public bool Active { get; private set; }
    public int Tier { get; private set; }
    public int CarriedGold { get; private set; }
    public int EssenceThisRun { get; private set; }
    public readonly List<MapResult> history = new();
    public MapData CurrentMap { get; private set; }

    public RunDifficulty Difficulty => new(config.meta, Tier);

    public RunManager(GameConfig config, SaveSystem save)
    {
        this.config = config;
        this.save = save;
    }

    public void StartRun()
    {
        Active = true;
        Tier = 0;
        CarriedGold = 0;
        EssenceThisRun = 0;
        history.Clear();
        save.Data.runsStarted++;
        save.Save();
    }

    public void EnterMap(MapData map) => CurrentMap = map;

    public int StartingGold(MapData map)
    {
        int bonus = Tier > 0 && config.meta ? config.meta.transitionBonusPerTier * Tier : 0;
        return map.startingGold + CarriedGold + bonus;
    }

    public static string PhaseKey(MapData map, int tier) => $"{(map ? map.id : "?")}@{tier}";

    // A phase = a map at a stage of the run. The first victory in it pays full Essence; replaying a phase
    // already won pays replayMultiplier. Losing never marks the phase, so retrying it still counts as new.
    public bool IsReplay(MapData map) => save.Data.clearedPhases.Contains(PhaseKey(map, Tier));

    // Scores the map, pays Essence (saved at once) and advances the run on victory.
    public MapResult FinishMap(MapResult r)
    {
        var m = config.meta;
        r.tier = Tier;
        r.replay = IsReplay(r.map);
        float e = 0;
        if (m)
        {
            e = r.kills * m.perKill + r.eliteKills * m.perEliteKill + r.bossKills * m.perBossKill + r.wavesCleared * m.perWaveCleared;
            if (r.victory) e += m.mapVictory;
            e *= 1 + m.perRunTierBonus * Tier;
            if (!r.victory) e *= m.defeatMultiplier;
            if (r.replay) e *= m.replayMultiplier;
        }
        if (r.victory && !r.replay) save.Data.clearedPhases.Add(PhaseKey(r.map, Tier));
        r.essence = Mathf.Max(0, Mathf.RoundToInt(e));
        EssenceThisRun += r.essence;
        history.Add(r);
        save.Data.mapsWon += r.victory ? 1 : 0;
        save.Data.bossesKilled += r.bossKills;
        save.AddEssence(r.essence);
        save.Save();

        if (r.victory)
        {
            Tier++;
            CarriedGold = m ? Mathf.Min(m.goldCarryMax, Mathf.FloorToInt(r.goldLeft * m.goldCarryFraction)) : 0;
        }
        else EndRun();
        return r;
    }

    public void EndRun()
    {
        Active = false;
        CarriedGold = 0;
        CurrentMap = null;
    }
}
