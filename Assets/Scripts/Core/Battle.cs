using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// One map being played: owns its systems and everything spawned in it (destroying the Battle object
// clears map, buildings, enemies, projectiles and effects), the economy, statistics and the end of the map.
public class Battle : MonoBehaviour
{
    public static Battle I;

    public GameConfig config;
    public MapData mapData;
    public RouteData routeData;
    public RunDifficulty difficulty;

    public MapController map;
    public FortressController fortress;
    public CommanderController commander;
    public WaveManager waves;
    public TowerPlacementController placement;
    public BuildZoneController zones;
    public BattleInput input;

    public readonly List<EnemyController> enemies = new();
    public readonly List<TowerController> towers = new();
    public readonly MapResult stats = new();
    readonly Dictionary<string, float> bossDamage = new();

    public int gold;
    public int summoned, automatedBuilds, leaks;
    public bool over, victory;
    public string endReason;
    public bool commanderSelected;
    // Target rule the player chose for every tower (HUD); null = each tower's own default. The tower panel overrides one tower.
    public TargetPriority? targetAll;
    public bool paused;
    public int speedIndex;
    public float elapsed;
    public EnemyController boss;

    public event Action<EnemyController> EnemyKilled;
    public event Action<EnemyController> BossAppeared;
    public event Action<string, float> BannerRequested;
    public event Action<MapResult> Finished;

    public IReadOnlyDictionary<string, float> BossDamage => bossDamage;
    public readonly Dictionary<string, float> enabledDamage = new();   // extra damage made possible by curses (analysis)

    public void RecordEnabled(string source, float amount) => enabledDamage[source] = enabledDamage.GetValueOrDefault(source) + amount;
    public float Speed => config.gameSpeeds[Mathf.Clamp(speedIndex, 0, config.gameSpeeds.Length - 1)];

    public static Battle Create(GameConfig cfg, MapData mapData, RouteData route, RunManager run, SaveSystem save, Transform parent = null)
    {
        var go = new GameObject($"Battle · {mapData.displayName}");
        if (parent) go.transform.SetParent(parent, false);
        var b = go.AddComponent<Battle>();
        I = b;
        b.config = cfg;
        b.mapData = mapData;
        b.routeData = route;
        b.difficulty = run != null ? run.Difficulty : new RunDifficulty(cfg.meta, 0);
        b.gold = run != null ? run.StartingGold(mapData) : mapData.startingGold;
        b.stats.map = mapData;
        b.stats.route = route;
        Fx.Bind(go.transform);

        b.map = new GameObject("Map").AddComponent<MapController>();
        b.map.transform.SetParent(go.transform, false);
        b.map.Build(mapData, route);

        b.fortress = new GameObject("Fortress").AddComponent<FortressController>();
        b.fortress.transform.SetParent(go.transform, false);
        b.fortress.Init(mapData, route, mapData.fortressHp);

        b.zones = new BuildZoneController(b);
        b.waves = go.AddComponent<WaveManager>();
        b.waves.Init(b);
        b.placement = go.AddComponent<TowerPlacementController>();
        b.placement.Init(b);

        var skin = CommanderSkinController.Equipped(cfg, save);
        var cstats = CommanderProgression.Compute(cfg.commander, save != null ? save.Data : null);
        b.commander = new GameObject("Commander").AddComponent<CommanderController>();
        b.commander.transform.SetParent(go.transform, false);
        b.commander.Init(cfg.commander, cstats, skin, route.commanderStart);

        b.waves.AutoMode = save != null && save.Data.settings.autoWaves;
        if (save != null && save.Data.settings.towerTargeting >= 0) b.targetAll = (TargetPriority)save.Data.settings.towerTargeting;
        b.input = go.AddComponent<BattleInput>();
        b.input.Init(b);
        b.ApplyTime();
        return b;
    }

    void OnDestroy()
    {
        if (I == this) I = null;
        Time.timeScale = 1;
        Fx.Clear();
    }

    void Update()
    {
        if (!over) elapsed += Time.deltaTime;
    }

    // ---------------------------------------------------------------- economy

    public void Spend(int amount)
    {
        gold -= amount;
        stats.goldSpent += amount;
    }

    public void Earn(int amount, bool countAsIncome = true)
    {
        gold += amount;
        if (countAsIncome) stats.goldEarned += amount;
    }

    // ---------------------------------------------------------------- time

    public void TogglePause()
    {
        paused = !paused;
        ApplyTime();
        AudioManager.Play("ui_click");
    }

    public void SetPaused(bool on)
    {
        paused = on;
        ApplyTime();
    }

    public void CycleSpeed() => SetSpeedIndex((speedIndex + 1) % config.gameSpeeds.Length);

    public void SetSpeedIndex(int i)
    {
        speedIndex = Mathf.Clamp(i, 0, config.gameSpeeds.Length - 1);
        ApplyTime();
    }

    // Smoke test only: arbitrary time scale (e.g. 8x for balance sweeps).
    public void SetSpeedAutomated(float scale)
    {
        if (!Automation.Require("velocidade automática")) return;
        paused = false;
        Time.timeScale = scale;
    }

    bool slowMo;

    public void ApplyTime() => Time.timeScale = paused ? 0 : slowMo ? 0.35f : over ? 1 : Speed;

    // ---------------------------------------------------------------- combat bookkeeping

    readonly List<EnemyController> query = new();

    public List<EnemyController> EnemiesIn(Vector2 at, float radius)
    {
        query.Clear();
        float r2 = radius * radius;
        foreach (var e in enemies)
            if (e && e.Alive)
            {
                float rr = radius + e.data.bodyRadius * 0.5f;
                if (((Vector2)e.transform.position - at).sqrMagnitude <= rr * rr) query.Add(e);
            }
        return query;
    }

    public void RecordDamage(string source, float dealt, bool isBoss)
    {
        if (string.IsNullOrEmpty(source) || dealt <= 0) return;
        stats.damageBySource[source] = stats.damageBySource.GetValueOrDefault(source) + dealt;
        if (isBoss) bossDamage[source] = bossDamage.GetValueOrDefault(source) + dealt;
        if (commander && source == commander.data.displayName) stats.commanderDamage += dealt;
    }

    public void Killed(EnemyController e, string source)
    {
        enemies.Remove(e);
        if (commander) commander.Release(e);
        int reward = Mathf.RoundToInt(e.data.goldReward * e.goldMultiplier);
        Earn(reward);
        stats.kills++;
        if (e.IsBoss) stats.bossKills++;
        else if (e.IsElite) stats.eliteKills++;
        if (reward > 0 && SettingsCache.DamageNumbers) Fx.Text((Vector2)e.transform.position + Vector2.up * (e.Height + 0.3f), $"+{reward}", new Color(0.95f, 0.78f, 0.3f), 0.8f);
        EnemyKilled?.Invoke(e);
        if (e == boss)
        {
            boss = null;
            MusicManager.Boss(false);
            CameraRig.Shake(0.35f);
            AudioManager.Play("boss_death", e.transform.position);
            Banner($"{e.boss.Title} caiu!", 3f);
        }
    }

    public void Leaked(EnemyController e)
    {
        enemies.Remove(e);
        if (commander) commander.Release(e);
        leaks++;
        fortress.Breach(e, e.damageMultiplier);
        if (e == boss)
        {
            boss = null;
            MusicManager.Boss(false);
        }
        if (fortress.Destroyed) Defeat($"{e.data.displayName} atravessou os portões. A fortaleza caiu.");
    }

    public void BossEntered(EnemyController e)
    {
        boss = e;
        BossAppeared?.Invoke(e);
        Banner(e.boss.Title.ToUpperInvariant(), 3f);
    }

    public void Banner(string text, float seconds = 3f) => BannerRequested?.Invoke(text, seconds);

    // ---------------------------------------------------------------- auras (War Chapel, Tactics)

    public float ArmorShredAt(Vector2 p)
    {
        float s = 0;
        foreach (var t in towers)
            if (t.IsSupport && t.stunLeft <= 0 && Vector2.Distance(t.Pos, p) <= t.stats.auraRadius) s = Mathf.Max(s, t.stats.auraArmorShred);
        return s;
    }

    public float CommanderHealAt(Vector2 p)
    {
        float h = 0;
        foreach (var t in towers)
            if (t.IsSupport && t.stunLeft <= 0 && Vector2.Distance(t.Pos, p) <= t.stats.auraRadius) h = Mathf.Max(h, t.stats.commanderHealPerSecond);
        return h;
    }

    // HUD button: default -> first -> strongest -> default. Re-targets every built tower.
    public void CycleTargetAll()
    {
        targetAll = targetAll switch { null => TargetPriority.First, TargetPriority.First => TargetPriority.Strongest, _ => null };
        foreach (var t in towers) t.priority = targetAll ?? t.data.targeting;
    }

    public float TacticsBonusAt(Vector2 p) =>
        commander && commander.Alive && commander.stats.tacticsBonus > 0 && Vector2.Distance(commander.transform.position, p) <= commander.stats.tacticsRadius
            ? commander.stats.tacticsBonus : 0;

    // ---------------------------------------------------------------- end

    public void Defeat(string why) => End(false, why);

    public void Victory() => End(true, mapData.waves.Count > 0 ? $"{mapData.displayName} resistiu. O bastião vive mais uma noite." : "Vitória");

    void End(bool won, string why)
    {
        if (over) return;
        over = true;
        victory = won;
        endReason = why;
        placement.CancelBuild();
        waves.StopAllCoroutines();
        stats.victory = won;
        stats.endReason = why;
        stats.wavesTotal = mapData.waves.Count;
        stats.goldLeft = gold;
        stats.fortressHp = fortress.hp;
        stats.fortressHpMax = fortress.maxHp;
        stats.seconds = elapsed;
        stats.commanderDamage = commander ? commander.damageDealt : stats.commanderDamage;
        AudioManager.Play(won ? "victory" : "defeat");
        MusicManager.Play(won ? "music_victory" : "music_defeat", 1.2f);
        StartCoroutine(EndSequence());
    }

    IEnumerator EndSequence()
    {
        slowMo = true;
        ApplyTime();
        yield return new WaitForSecondsRealtime(Automation.Enabled ? 0.8f : 1.8f);
        slowMo = false;
        Time.timeScale = 1;
        Finished?.Invoke(stats);
    }
}
