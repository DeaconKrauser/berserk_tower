using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Pure rules: persistence (meta survives, run does not), skins, damage math, automation gate, audio safety.
public class MetaAndRulesTests
{
    string path;

    [SetUp]
    public void SetUp()
    {
        path = Path.Combine(Application.temporaryCachePath, $"test_meta_{System.Guid.NewGuid():N}.json");
        Automation.EnableForTests(false);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var f in new[] { path, path + ".tmp", path + ".corrupt" })
            if (File.Exists(f)) File.Delete(f);
        Automation.EnableForTests(false);
    }

    static GameConfig Config()
    {
        var c = GameConfig.Load();
        Assert.IsNotNull(c, "Resources/GameConfig.asset não existe (rode Bastião/Recriar conteúdo)");
        return c;
    }

    [Test]
    public void MetaProgression_SurvivesClosingTheGame()
    {
        var cfg = Config();
        var save = new SaveSystem(path);
        save.AddEssence(500);
        var strength = cfg.commander.Attribute(CommanderAttribute.Strength);
        Assert.IsTrue(save.TryBuyAttribute(strength));
        Assert.IsTrue(save.TryBuyAttribute(strength));
        int left = save.Data.essence;

        var reopened = new SaveSystem(path);   // "closing and opening the game"
        Assert.AreEqual(2, reopened.Data.Level(CommanderAttribute.Strength));
        Assert.AreEqual(left, reopened.Data.essence);
        Assert.AreEqual(MetaSave.CurrentVersion, reopened.Data.version);
        var stats = CommanderProgression.Compute(cfg.commander, reopened.Data);
        Assert.Greater(stats.damage, cfg.commander.damage);
    }

    [Test]
    public void Run_DoesNotSurviveClosingTheGame()
    {
        var cfg = Config();
        var save = new SaveSystem(path);
        var run = new RunManager(cfg, save);
        run.StartRun();
        run.FinishMap(new MapResult { map = cfg.maps[0], victory = true, kills = 50, wavesCleared = 10, goldLeft = 400 });
        Assert.IsTrue(run.Active);
        Assert.AreEqual(1, run.Tier);

        // only the meta file exists, and it carries no run state
        var json = File.ReadAllText(path);
        StringAssert.DoesNotContain("tier", json.ToLowerInvariant().Replace("maxtier", ""));
        StringAssert.DoesNotContain("carriedgold", json.ToLowerInvariant());
        var reopenedRun = new RunManager(cfg, new SaveSystem(path));
        Assert.IsFalse(reopenedRun.Active, "uma run nunca é restaurada");
        Assert.AreEqual(0, reopenedRun.Tier);
    }

    [Test]
    public void Essence_NewPhaseFull_ReplayReduced_DefeatKeepsEverything()
    {
        var cfg = Config();
        var save = new SaveSystem(path);
        var run = new RunManager(cfg, save);
        var map = cfg.maps[0];
        MapResult Play(bool won) => run.FinishMap(new MapResult { map = map, victory = won, kills = 100, wavesCleared = won ? 10 : 6 });

        run.StartRun();
        var lost = Play(false);                         // never won: counts as a new phase, nothing withheld
        Assert.IsFalse(lost.replay);
        Assert.AreEqual(Mathf.RoundToInt(100 * cfg.meta.perKill + 6 * cfg.meta.perWaveCleared), lost.essence);

        run.StartRun();
        var first = Play(true);                         // first victory in map1 @ stage 1: full
        Assert.IsFalse(first.replay);
        int full = first.essence;

        run.StartRun();
        var again = Play(true);                         // same phase again: reduced
        Assert.IsTrue(again.replay);
        Assert.AreEqual(Mathf.RoundToInt(full * cfg.meta.replayMultiplier), again.essence, 1);
        Assert.AreEqual(lost.essence + full + again.essence, new SaveSystem(path).Data.essence, "nada se perde e tudo é salvo");

        // the next stage of the same map is a different phase: full again
        Assert.AreEqual(1, run.Tier);
        Assert.IsFalse(run.IsReplay(map));
    }

    [Test]
    public void Skin_UnlockAndEquip_Persist()
    {
        var cfg = Config();
        var save = new SaveSystem(path);
        var skin = cfg.skins.Find(s => !s.unlockedByDefault);
        Assert.IsNotNull(skin);
        Assert.IsFalse(save.TryUnlockSkin(skin), "sem Essência não desbloqueia");
        save.AddEssence(skin.unlockCost);
        Assert.IsTrue(save.TryUnlockSkin(skin));
        Assert.IsTrue(save.Equip(skin));
        var reopened = new SaveSystem(path);
        Assert.AreEqual(skin.id, reopened.Data.equippedSkin);
        Assert.IsTrue(reopened.Data.HasSkin(skin));
        Assert.AreEqual(skin, CommanderSkinController.Equipped(cfg, reopened));
    }

    [Test]
    public void CorruptSave_StartsFreshWithoutThrowing()
    {
        File.WriteAllText(path, "{ not json");
        LogAssert.ignoreFailingMessages = true;
        var save = new SaveSystem(path);
        LogAssert.ignoreFailingMessages = false;
        Assert.AreEqual(0, save.Data.essence);
    }

    [Test]
    public void SmokeTest_OnlyWithFlag()
    {
        Assert.IsFalse(Automation.RequestedBy(new[] { "Bastiao.exe" }));
        Assert.IsTrue(Automation.RequestedBy(new[] { "Bastiao.exe", "-smoketest" }));
        Automation.Enable(new[] { "Bastiao.exe" });
        Assert.IsFalse(Automation.Enabled);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("bloqueado"));
        Assert.IsFalse(Automation.Require("construir"));
        Automation.Enable(new[] { "Bastiao.exe", "-smoketest" });
        Assert.IsTrue(Automation.Enabled);
        Assert.IsTrue(Automation.Require("construir"));
    }

    [Test]
    public void Damage_ArmorResistanceAndCurse()
    {
        // physical vs 14 armor, no penetration: 20 - 14 = 6 (above the 15% floor of 3)
        Assert.AreEqual(6f, DamageSystem.Resolve(20, DamageType.Physical, 14, 0, 0, 0, 0.15f), 1e-4f);
        // armor can never reduce below the floor
        Assert.AreEqual(3f, DamageSystem.Resolve(20, DamageType.Physical, 40, 0, 0, 0, 0.15f), 1e-4f);
        // penetration
        Assert.AreEqual(14f, DamageSystem.Resolve(20, DamageType.Physical, 14, 8, 0, 0, 0.15f), 1e-4f);
        // fire ignores armor, resistance applies
        Assert.AreEqual(10f, DamageSystem.Resolve(20, DamageType.Fire, 14, 0, 0.5f, 0, 0.15f), 1e-4f);
        // curse: +30% damage taken
        Assert.AreEqual(26f, DamageSystem.Resolve(20, DamageType.Magic, 0, 0, 0, 0.3f, 0.15f), 1e-4f);
    }

    [Test]
    public void RunDifficulty_GrowsPerCompletedMap()
    {
        var cfg = Config();
        var d0 = new RunDifficulty(cfg.meta, 0);
        var d2 = new RunDifficulty(cfg.meta, 2);
        Assert.AreEqual(1f, d0.enemyHp);
        Assert.Greater(d2.enemyHp, d0.enemyHp);
        Assert.Greater(d2.spawnCount, d0.spawnCount);
        Assert.Less(d2.spawnInterval, d0.spawnInterval);
        Assert.Greater(d2.eliteChance, 0);
        Assert.Greater(d2.bossHp, 1f);
    }

    [Test]
    public void Content_EveryTowerHasFourTiersAndGrowingPrices()
    {
        var cfg = Config();
        Assert.AreEqual(7, cfg.towers.Count);
        foreach (var t in cfg.towers)
        {
            Assert.AreEqual(4, t.MaxTier, t.displayName);
            Assert.IsNotNull(t.sprite, t.displayName);
            int last = t.cost;
            foreach (var u in t.upgrades)
            {
                Assert.Greater(u.cost, last * 0.95f, $"{t.displayName}: {u.displayName} deve custar mais");
                Assert.IsNotNull(u.sprite, $"{t.displayName}: {u.displayName} sem visual");
                last = u.cost;
            }
        }
    }

    [Test]
    public void Content_BossesDoNotExecuteFortressByDefault()
    {
        var cfg = Config();
        foreach (var m in cfg.maps)
            foreach (var w in m.waves)
                foreach (var g in w.groups)
                    foreach (var e in g.entries)
                        if (e.enemy && e.enemy.boss)
                        {
                            Assert.IsFalse(e.enemy.boss.fortressExecution, e.enemy.displayName);
                            Assert.Less(e.enemy.damageToFortress, m.fortressHp, "um chefe não deve destruir a fortaleza cheia sozinho");
                        }
    }
}
