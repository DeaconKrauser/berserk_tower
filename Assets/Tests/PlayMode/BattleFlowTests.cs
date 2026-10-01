using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Real game in the real scene, booted with a throw-away save file.
public class BattleFlowTests
{
    string savePath;
    GameManager gm;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Automation.EnableForTests(false);
        GameManager.SuppressAutoBoot = true;
        savePath = Path.Combine(Application.temporaryCachePath, $"play_meta_{System.Guid.NewGuid():N}.json");
        SceneManager.LoadScene(GameManager.GameScene);
        yield return null;
        gm = GameManager.Boot(savePath);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (gm) Object.Destroy(gm.gameObject);
        foreach (var b in Object.FindObjectsByType<Battle>()) Object.Destroy(b.gameObject);
        Time.timeScale = 1;
        Automation.EnableForTests(false);
        GameManager.SuppressAutoBoot = false;
        yield return null;
        if (File.Exists(savePath)) File.Delete(savePath);
    }

    IEnumerator StartMap(int index = 0, int route = 0)
    {
        gm.run.StartRun();
        var map = gm.config.maps[index];
        gm.flow.StartMap(map, map.routes[route]);
        float t = 0;
        while ((gm.flow.Current != SceneFlow.Screen.Battle || !gm.flow.battle) && (t += Time.unscaledDeltaTime) < 5) yield return null;
        Assert.IsNotNull(gm.flow.battle, "a batalha não começou");
    }

    // A point near the road where the given tower is valid.
    static Vector2 ValidSpot(Battle b, TowerData t)
    {
        var area = MapController.BuildArea;
        for (float x = area.xMin + 1; x < area.xMax - 1; x += 0.4f)
            for (float y = area.yMin + 1; y < area.yMax - 1; y += 0.4f)
            {
                var p = new Vector2(x, y);
                if (b.map.route.DistanceToRoad(p) < 2.6f && b.zones.CanBuild(t, p, out _, ignoreGold: true)) return p;
            }
        Assert.Fail("nenhum lugar válido");
        return default;
    }

    [UnityTest]
    public IEnumerator NewGame_StartsWithoutTowers_AndCorrectGold()
    {
        yield return StartMap();
        var b = gm.flow.battle;
        Assert.AreEqual(0, b.towers.Count);
        Assert.AreEqual(0, Object.FindObjectsByType<TowerController>().Length);
        Assert.AreEqual(gm.config.maps[0].startingGold, b.gold);
    }

    [UnityTest]
    public IEnumerator NoBuildingHappensAutomatically()
    {
        yield return StartMap();
        var b = gm.flow.battle;
        Assert.IsTrue(b.waves.StartWave());
        Time.timeScale = 4;
        yield return new WaitForSeconds(6f);
        Assert.AreEqual(0, b.towers.Count, "nada deve ser construído sem o jogador");
        Assert.AreEqual(0, b.automatedBuilds);
        // the automated entry point refuses without -smoketest
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("bloqueado"));
        var t = gm.config.towers[0];
        Assert.IsNull(b.placement.TryBuild(t, ValidSpot(b, t), automated: true));
        Assert.AreEqual(0, b.towers.Count);
    }

    [UnityTest]
    public IEnumerator CannotBuildOnRoad_OrOverlapTowers()
    {
        yield return StartMap();
        var b = gm.flow.battle;
        var archer = gm.config.towers.Find(x => x.id == "archer");
        var onRoad = b.map.route.Position(0, b.map.route.Length(0) * 0.5f, 0);
        Assert.IsFalse(b.zones.CanBuild(archer, onRoad, out var why));
        StringAssert.Contains("estrada", why);
        var spot = ValidSpot(b, archer);
        Assert.IsNotNull(b.placement.TryBuild(archer, spot));
        Assert.IsFalse(b.zones.CanBuild(archer, spot + new Vector2(0.3f, 0), out why), "torres não podem se sobrepor");
        // the spike trap is the only building that goes ON the road
        var trap = gm.config.towers.Find(x => x.id == "spikes");
        Assert.IsTrue(b.zones.CanBuild(trap, onRoad, out _, ignoreGold: true));
        var offRoad = ValidSpot(b, archer);
        Assert.IsFalse(b.zones.CanBuild(trap, offRoad, out why, ignoreGold: true));
        StringAssert.Contains("estrada", why);
    }

    [UnityTest]
    public IEnumerator Upgrade_SpendsGold_Sell_ReturnsValue()
    {
        yield return StartMap();
        var b = gm.flow.battle;
        b.gold = 5000;
        var archer = gm.config.towers.Find(x => x.id == "archer");
        var t = b.placement.TryBuild(archer, ValidSpot(b, archer));
        Assert.IsNotNull(t);
        int before = b.gold, cost = t.NextUpgrade.cost;
        Assert.IsTrue(b.placement.TryUpgrade(t));
        Assert.AreEqual(before - cost, b.gold);
        Assert.AreEqual(2, t.tier);
        Assert.AreEqual(archer.upgrades[0].sprite, t.data.SpriteAt(t.tier));
        // after the wave starts, selling returns sellRefund of everything invested
        b.waves.StartWave();
        yield return null;
        int expected = Mathf.FloorToInt((archer.cost + cost) * gm.config.sellRefund);
        Assert.AreEqual(expected, b.placement.SellValue(t));
        before = b.gold;
        Assert.IsTrue(b.placement.Sell(t));
        Assert.AreEqual(before + expected, b.gold);
        yield return null;
        Assert.AreEqual(0, b.towers.Count);
    }

    [UnityTest]
    public IEnumerator SubWaves_RunGroupsInOrder()
    {
        yield return StartMap();
        var b = gm.flow.battle;
        var wave = b.mapData.waves[0];
        int expected = wave.groups.Where(g => g.minRunTier == 0).Sum(g => g.Count);
        Assert.IsTrue(b.waves.StartWave());
        Assert.AreEqual(wave.groups.Count(g => g.minRunTier == 0), b.waves.GroupCount);
        Assert.AreEqual(1, b.waves.WaveNumber);
        Time.timeScale = 8;
        int seen = 0, maxGroup = 0;
        var counted = new System.Collections.Generic.HashSet<EnemyController>();
        float t = 0;
        while (b.waves.Spawning && (t += Time.unscaledDeltaTime) < 30)
        {
            foreach (var e in b.enemies)
                if (counted.Add(e)) seen++;
            maxGroup = Mathf.Max(maxGroup, b.waves.Group);
            yield return null;
        }
        foreach (var e in b.enemies)
            if (counted.Add(e)) seen++;
        Assert.AreEqual(b.waves.GroupCount, maxGroup, "todos os grupos rodaram");
        Assert.GreaterOrEqual(seen + b.stats.kills, expected);
    }

    [UnityTest]
    public IEnumerator Boss_EntersAndDiesCorrectly()
    {
        yield return StartMap();
        var b = gm.flow.battle;
        var bossData = b.mapData.waves.SelectMany(w => w.groups).SelectMany(g => g.entries).First(e => e.enemy.IsBoss).enemy;
        bool appeared = false;
        b.BossAppeared += _ => appeared = true;
        var boss = b.waves.SpawnNow(bossData);
        yield return null;
        Assert.IsTrue(appeared);
        Assert.AreEqual(boss, b.boss);
        Assert.IsTrue(boss.boss.Busy, "entrada do chefe");
        Assert.IsTrue(MusicManager.I.BossActive);
        yield return new WaitForSeconds(0.2f);
        boss.Damage(new DamageInfo(boss.MaxHp * 10, DamageType.Magic, "teste"));
        Assert.IsFalse(boss.Alive);
        Assert.IsNull(b.boss);
        Assert.AreEqual(1, b.stats.bossKills);
        Assert.IsFalse(MusicManager.I.BossActive);
        Assert.IsFalse(b.over, "matar o chefe fora da última onda não encerra o mapa");
    }

    [UnityTest]
    public IEnumerator MapEnd_OpensChoice_AndSwitchingMapsClearsBuildings()
    {
        yield return StartMap(0);
        var b = gm.flow.battle;
        b.gold = 2000;
        var archer = gm.config.towers.Find(x => x.id == "archer");
        Assert.IsNotNull(b.placement.TryBuild(archer, ValidSpot(b, archer)));
        b.Victory();
        float t = 0;
        while (gm.flow.Current != SceneFlow.Screen.Results && (t += Time.unscaledDeltaTime) < 8) yield return null;
        Assert.AreEqual(SceneFlow.Screen.Results, gm.flow.Current);
        Assert.AreEqual(1, gm.run.Tier);
        Assert.Greater(gm.save.Data.essence, 0, "Essência creditada");
        gm.flow.ShowMapSelect(false);
        t = 0;
        while (gm.flow.Current != SceneFlow.Screen.MapSelect && (t += Time.unscaledDeltaTime) < 5) yield return null;
        Assert.AreEqual(SceneFlow.Screen.MapSelect, gm.flow.Current);
        Assert.IsTrue(gm.run.Active, "a run continua");
        yield return null;
        Assert.AreEqual(0, Object.FindObjectsByType<TowerController>().Length, "construções não vão para o próximo mapa");
        var map2 = gm.config.maps[1];
        gm.flow.StartMap(map2, map2.routes[0]);
        t = 0;
        while ((gm.flow.Current != SceneFlow.Screen.Battle || !gm.flow.battle) && (t += Time.unscaledDeltaTime) < 5) yield return null;
        Assert.AreEqual(0, gm.flow.battle.towers.Count);
        Assert.AreEqual(gm.run.StartingGold(map2), gm.flow.battle.gold);
        Assert.Greater(gm.flow.battle.difficulty.enemyHp, 1f, "a dificuldade sobe a cada mapa vencido");
    }

    [UnityTest]
    public IEnumerator CommanderPower_DamagesStunsAndRecharges()
    {
        yield return StartMap();
        var b = gm.flow.battle;
        var c = b.commander;
        var soldier = b.mapData.roster[0];
        Assert.IsFalse(c.combat.UltimateReady, "começa parcialmente carregado");
        float wait = c.combat.UltimateCooldownLeft;
        Time.timeScale = 8;
        float t = 0;
        while (!c.combat.UltimateReady && (t += Time.unscaledDeltaTime) < wait / 8 + 3) yield return null;
        Time.timeScale = 1;
        Assert.IsTrue(c.combat.UltimateReady);
        // enemies placed only now, so the normal sword swings have no time to kill them first
        var foes = new System.Collections.Generic.List<EnemyController>();
        for (int i = 0; i < 3; i++)
        {
            var e = b.waves.SpawnNow(soldier);
            e.transform.position = (Vector2)c.transform.position + new Vector2(0.6f + 0.4f * i, 0.3f);
            e.movement.halted = true;
            e.health.max = e.health.current = 5000;
            foes.Add(e);
        }
        float hpBefore = foes[2].Hp;
        Assert.IsTrue(c.combat.UseUltimate());
        Assert.IsFalse(c.combat.UseUltimate(), "não dispara em recarga");
        yield return new WaitForSeconds(0.6f);
        Assert.Less(foes[2].Hp, hpBefore, "o golpe giratório causa dano em área");
        Assert.Greater(foes[2].status.stunLeft, 0, "inimigos comuns ficam atordoados");
        Assert.Greater(c.combat.FuryLeft, 0, "fúria ativa depois do golpe");
        Assert.Greater(c.combat.UltimateCooldownLeft, 0);
    }

    [UnityTest]
    public IEnumerator SpikeTrap_WearsDownAndBreaks()
    {
        yield return StartMap();
        var b = gm.flow.battle;
        b.gold = 1000;
        var trapData = gm.config.towers.Find(x => x.id == "spikes");
        var r = b.map.route;
        var at = r.Position(0, r.Length(0) * 0.5f, 0);
        var trap = b.placement.TryBuild(trapData, at);
        Assert.IsNotNull(trap);
        int full = trap.durability;
        Assert.Greater(full, 0);
        var soldier = b.mapData.roster[0];
        Time.timeScale = 4;
        int spawned = 0;
        float t = 0;
        while (trap && (t += Time.unscaledDeltaTime) < 40)
        {
            if (spawned < 6)
            {
                var e = b.waves.SpawnNow(soldier);
                e.transform.position = at;
                e.health.max = e.health.current = 1e5f;      // they survive: only the wear matters here
                e.movement.halted = true;
                spawned++;
            }
            if (trap && trap.durability < full) Assert.Less(trap.DurabilityFraction, 1f);
            yield return null;
        }
        Time.timeScale = 1;
        Assert.IsTrue(!trap, "a armadilha quebra quando a durabilidade acaba");
        Assert.AreEqual(1, b.stats.trapsBroken);
        Assert.IsFalse(b.towers.Exists(x => x.data == trapData));
    }

    [UnityTest]
    public IEnumerator AutoWaves_StartNextWaveAfterCountdown()
    {
        yield return StartMap();
        var b = gm.flow.battle;
        Assert.IsFalse(b.waves.AutoMode, "o padrão é manual");
        b.waves.AutoMode = true;
        Assert.IsTrue(b.waves.StartWave());
        foreach (var e in b.enemies.ToArray()) e.Leak();
        Time.timeScale = 8;
        float t = 0;
        while (b.waves.WaveNumber < 2 && (t += Time.unscaledDeltaTime) < 30)
        {
            foreach (var e in b.enemies.ToArray()) e.Leak();
            yield return null;
        }
        Time.timeScale = 1;
        Assert.GreaterOrEqual(b.waves.WaveNumber, 2, "a onda seguinte começou sozinha no modo automático");
        Assert.AreEqual(0, b.towers.Count, "modo automático não constrói nada");
    }

    [UnityTest]
    public IEnumerator Audio_MissingClips_NoExceptions()
    {
        AudioManager.Play("som_que_nao_existe");
        AudioManager.Play("som_que_nao_existe", Vector2.one);
        AudioManager.Play(null);
        AudioManager.Ambient("ambiente_inexistente", 0.1f);
        MusicManager.Play("musica_inexistente", 0.1f);
        MusicManager.Boss(true);
        MusicManager.Boss(false);
        yield return new WaitForSecondsRealtime(0.3f);
        // missing clips only log an info line; any exception or error log would fail this test
        Assert.IsNotNull(AudioManager.I);
    }
}
