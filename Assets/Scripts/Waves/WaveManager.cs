using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Waves made of sub-waves (SpawnGroups). The player starts each wave; groups then run in order, each waiting
// its delay and releasing its mixed entries one by one. Run difficulty scales counts, spacing and elites.
public class WaveManager : MonoBehaviour
{
    public int WaveNumber { get; private set; }        // waves started so far (1-based once running)
    public int Group { get; private set; }
    public int GroupCount { get; private set; }
    public bool Running { get; private set; }
    public bool Spawning { get; private set; }
    public int TotalWaves => battle.mapData.waves.Count;
    public WaveData Current => WaveNumber > 0 ? battle.mapData.waves[WaveNumber - 1] : null;
    public WaveData Next => WaveNumber < TotalWaves ? battle.mapData.waves[WaveNumber] : null;
    public float NextGroupIn { get; private set; }

    // Player option (not automation): after the first wave, the next one starts by itself after a countdown.
    public bool AutoMode { get; set; }
    public float AutoStartIn { get; private set; } = -1;

    Battle battle;
    SpawnController spawner;

    public void Init(Battle b)
    {
        battle = b;
        spawner = new SpawnController(b);
    }

    public bool StartWave(bool automated = false)
    {
        if (automated && !Automation.Require("iniciar onda")) return false;
        if (battle.over || Running || Next == null) return false;
        var w = Next;
        AutoStartIn = -1;
        WaveNumber++;
        Running = Spawning = true;
        Group = 0;
        GroupCount = ActiveGroups(w).Count;
        battle.Banner($"Onda {WaveNumber} · {w.title}");
        AudioManager.Play("wave_start");
        StartCoroutine(Run(w));
        return true;
    }

    List<SpawnGroup> ActiveGroups(WaveData w) => w.groups.FindAll(g => g.minRunTier <= battle.difficulty.tier);

    IEnumerator Run(WaveData w)
    {
        var groups = ActiveGroups(w);
        var diff = battle.difficulty;
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            Group = i + 1;
            for (NextGroupIn = g.delay * diff.spawnInterval; NextGroupIn > 0; NextGroupIn -= Time.deltaTime) yield return null;
            NextGroupIn = 0;
            var order = Interleave(g, diff);
            for (int n = 0; n < order.Count; n++)
            {
                var d = order[n];
                int path = spawner.NextPath(g.path);
                // the gate keeps personal space too: wait (a little) until the start of the road is clear
                for (float wait = 0; spawner.EntranceBlocked(path, d) && wait < g.interval * 3 + 1.5f; wait += Time.deltaTime) yield return null;
                spawner.Spawn(d, path, 0, spawner.Lane(d, n), !d.IsBoss);
                if (n < order.Count - 1) yield return new WaitForSeconds(g.interval * diff.spawnInterval);
            }
        }
        Spawning = false;
    }

    // Mixed group: "8 soldiers + 2 knights" releases them spread out (S S S S K S S S S K), counts scaled by difficulty.
    static List<EnemyData> Interleave(SpawnGroup g, RunDifficulty diff)
    {
        var counts = new List<(EnemyData e, int n)>();
        foreach (var e in g.entries)
        {
            if (!e.enemy || e.count <= 0) continue;
            int n = e.enemy.IsBoss ? e.count : Mathf.Max(1, Mathf.RoundToInt(e.count * diff.spawnCount));
            counts.Add((e.enemy, n));
        }
        var order = new List<EnemyData>();
        int total = 0;
        foreach (var c in counts) total += c.n;
        var placed = new int[counts.Count];
        for (int k = 0; k < total; k++)
        {
            // pick the entry that is most "behind" its proportional share
            int best = 0;
            float bestLag = float.MinValue;
            for (int j = 0; j < counts.Count; j++)
            {
                if (placed[j] >= counts[j].n) continue;
                float lag = (k + 1) * counts[j].n / (float)total - placed[j];
                if (lag > bestLag) { bestLag = lag; best = j; }
            }
            placed[best]++;
            order.Add(counts[best].e);
        }
        return order;
    }

    // Summons, splits: extra enemies that belong to the running wave (no elite promotion).
    public EnemyController SpawnChild(EnemyData d, int path, float progress, float lane) =>
        spawner.Spawn(d, path, Mathf.Max(0, progress), lane, false);

    // Debug / tests: one enemy at the start of the road.
    public EnemyController SpawnNow(EnemyData d, int path = 0) => spawner.Spawn(d, path, 0, 0, false);

    void Update()
    {
        if (!battle || battle.over) return;
        if (Running)
        {
            if (!Spawning && battle.enemies.Count == 0) Clear();
            return;
        }
        if (AutoMode && WaveNumber > 0 && Next != null && !battle.paused)
        {
            if (AutoStartIn < 0) AutoStartIn = battle.config.autoWaveDelay;
            if ((AutoStartIn -= Time.deltaTime) <= 0) StartWave();
        }
        else AutoStartIn = -1;
    }

    void Clear()
    {
        Running = false;
        int bonus = battle.mapData.waveClearBonus + battle.mapData.waveClearBonusPerWave * WaveNumber;
        battle.Earn(bonus);
        battle.stats.wavesCleared = WaveNumber;
        if (battle.commander && battle.commander.Alive) battle.commander.HealFraction(0.35f);
        AudioManager.Play("wave_complete");
        if (Next == null) battle.Victory();
        else battle.Banner($"Onda {WaveNumber} vencida · +{bonus} de ouro");
    }
}
