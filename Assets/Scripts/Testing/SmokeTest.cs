using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

// Automated run, ONLY with -smoketest (see Automation). Walks the menus, plays a whole run (Map 1 → Map 2)
// with a scripted "reasonable player", logs balance data (lines start with SMOKE) and saves screenshots
// next to the executable's data folder. Real mouse/keyboard are ignored while it runs.
//   -strategy balanced|archers|spam|upgrades   which build plan the bot follows
//   -maps map1,map2                            maps of the run, in order
//   -speed 4                                   time scale (8 for quick balance sweeps)
//   -noshots                                   skip screenshots
public class SmokeTest : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = Automation.CommandLine;
        if (!Automation.RequestedBy(args)) return;
        Automation.Enable(args);
        new GameObject("SmokeTest").AddComponent<SmokeTest>();
    }

    GameManager gm;
    Battle b;
    string strategy = "balanced";
    float speed = 4;
    bool shots = true;
    string[] maps = { "map1", "map2" };
    readonly List<string> plan = new();
    readonly List<TowerController> built = new();
    readonly HashSet<string> taken = new();
    int errors, step, ultimates;
    float cmdMinHp;
    Vector2 post;

    static string Arg(string name, string def)
    {
        var a = Automation.CommandLine;
        int i = System.Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
    }

    IEnumerator Start()
    {
        Debug.Assert(Automation.Enabled, "SmokeTest sem -smoketest");
        strategy = Arg("-strategy", strategy);
        speed = float.Parse(Arg("-speed", "4"), System.Globalization.CultureInfo.InvariantCulture);
        maps = Arg("-maps", "map1,map2").Split(',');
        shots = !Automation.CommandLine.Contains("-noshots");
        Application.logMessageReceived += (msg, stack, type) =>
        {
            if (type == LogType.Log || type == LogType.Warning || errors++ > 8) return;
            Debug.Log($"SMOKE error: {msg}\n{stack}");
        };
        while (!GameManager.I || GameManager.I.flow == null) yield return null;
        gm = GameManager.I;
        if (EventSystem.current) EventSystem.current.enabled = false;
        Log($"start strategy={strategy} speed={speed} maps={string.Join(",", maps)} essence={gm.save.Data.essence}");
        if (Arg("-scenario", "") == "crowd")
        {
            yield return CrowdScenario();
            Application.Quit();
            yield break;
        }

        yield return Wait(1.0f);
        yield return Shot("alpha_main_menu.png");
        Check(!FindObjectsByType<UnityEngine.UI.Text>().Any(t => t.text.Trim().ToUpperInvariant().StartsWith("CONTINUAR")), "menu sem botão Continuar");
        gm.flow.ShowCommander();
        yield return Wait(0.9f);
        yield return Shot("alpha_commander.png");
        gm.flow.ShowSkins();
        yield return Wait(0.9f);
        yield return Shot("alpha_skins.png");
        gm.flow.ShowOptions();
        yield return Wait(0.9f);
        yield return Shot("alpha_options.png");
        gm.flow.ShowMapSelect(true);
        yield return Wait(0.9f);
        yield return Shot("alpha_map_selection.png");

        for (int m = 0; m < maps.Length; m++)
        {
            var map = gm.config.Map(maps[m]);
            int ri = int.Parse(Arg("-route", "-1"));
            var route = map.routes[ri >= 0 ? ri % map.routes.Count : (m + strategy.Length) % map.routes.Count];
            gm.flow.StartMap(map, route);
            while (gm.flow.Current != SceneFlow.Screen.Battle || !gm.flow.battle) yield return null;
            b = gm.flow.battle;
            yield return Wait(0.5f);
            Check(b.towers.Count == 0, "mapa começa sem torres");
            Check(b.gold == gm.run.StartingGold(map), $"ouro inicial {b.gold}");
            yield return PlayMap(m);
            var r = gm.run.history.LastOrDefault();
            if (r == null || !r.victory) break;
            if (m < maps.Length - 1)
            {
                gm.flow.ShowMapSelect(false);
                yield return Wait(1.0f);
                yield return Shot("alpha_next_destination.png");
            }
        }
        Log($"RUN END maps={gm.run.history.Count} won={gm.run.history.Count(h => h.victory)} essenceRun={gm.run.EssenceThisRun} essenceTotal={gm.save.Data.essence} errors={errors}");
        yield return new WaitForSecondsRealtime(1f);
        Application.Quit();
    }

    IEnumerator PlayMap(int mapIndex)
    {
        plan.Clear();
        plan.AddRange(Plan(b.mapData.id, strategy));
        built.Clear();
        taken.Clear();
        step = 0;
        b.SetSpeedAutomated(speed);
        float waveStart = 0, lastBossLog = 0;
        int fortressAtStart = b.fortress.hp, killsAtStart = 0;
        string prefix = mapIndex == 0 ? "alpha_map1" : "alpha_map2";
        if (mapIndex == 0) yield return ShotWithGhost($"{prefix}_build_ghost.png", b.config.towers.Find(t => t.id == "pyre"));

        while (!b.over)
        {
            for (int i = 0; i < Mathf.Min(3, plan.Count); i++)
                if (TryStep(plan[i])) { plan.RemoveAt(i); step++; i = -1; }
            EmergencyBuild();

            if (!b.waves.Running && b.waves.Next && !b.over)
            {
                if (b.waves.WaveNumber > 0) LogWaveEnd(fortressAtStart, killsAtStart, waveStart);
                post = CommanderSpot();
                cmdMinHp = b.commander.maxHp;
                b.commander.MoveTo(post);
                fortressAtStart = b.fortress.hp;
                killsAtStart = b.stats.kills;
                waveStart = Time.time;
                b.waves.StartWave(automated: true);
                b.SetSpeedAutomated(speed);
                Log($"{b.mapData.id} wave={b.waves.WaveNumber} start gold={b.gold} plan={step}/{step + plan.Count} towers={b.towers.Count}");
            }
            ControlCommander();
            cmdMinHp = Mathf.Min(cmdMinHp, b.commander.hp);
            var boss = b.boss;
            if (boss && Time.time - lastBossLog > 4)
            {
                lastBossLog = Time.time;
                Log($"boss {boss.data.id} hp={boss.Hp:0}/{boss.MaxHp:0} route={100 * boss.Progress / b.map.route.Length(boss.movement.path):0}% enraged={boss.boss.Enraged} " +
                    $"fighting={boss.combat.Engaged} summoned={b.summoned} enemies={b.enemies.Count} fortress={b.fortress.hp} cmdHp={b.commander.hp:0} overlaps={Overlaps().Item1}");
            }
            if (shots)
            {
                if (b.waves.WaveNumber == 2 && Time.time - waveStart > 10 && !taken.Contains($"{prefix}_early.png"))
                    yield return ShotWithSelection($"{prefix}_early.png");
                if (b.waves.WaveNumber == 7 && Time.time - waveStart > 12 && !taken.Contains($"{prefix}_upgraded_tower.png"))
                    yield return ShotWithSelection($"{prefix}_upgraded_tower.png");
                if (boss && boss.Progress > 10 && !taken.Contains($"{prefix}_boss.png"))
                    yield return Shot($"{prefix}_boss.png");
                if (boss && boss.boss.Enraged && !taken.Contains($"{prefix}_boss_enraged.png"))
                    yield return Shot($"{prefix}_boss_enraged.png");
                if (mapIndex == 1 && b.waves.WaveNumber == 4 && Time.time - waveStart > 8 && !taken.Contains($"{prefix}_wave4.png"))
                    yield return Shot($"{prefix}_wave4.png");
            }
            yield return new WaitForSeconds(0.25f);
        }
        if (b.victory) LogWaveEnd(fortressAtStart, killsAtStart, waveStart);
        var s = b.stats;
        Log($"{b.mapData.id} END victory={b.victory} wave={b.waves.WaveNumber}/{b.waves.TotalWaves} fortress={b.fortress.hp}/{b.fortress.maxHp} kills={s.kills} ultimates={ultimates} trapsBroken={s.trapsBroken} " +
            $"leaks={b.leaks} gold={b.gold} spent={s.goldSpent} summoned={b.summoned} towers=[{Towers()}] reason=\"{b.endReason}\" route={b.routeData.id()}");
        Log("damage total: " + Share(s.damageBySource));
        Log("damage vs boss: " + Share(b.BossDamage.ToDictionary(x => x.Key, x => x.Value)));
        Log("extra damage enabled by curses: " + string.Join(", ", b.enabledDamage.Select(x => $"{x.Key} {x.Value:0}")));
        Check(b.automatedBuilds == built.Count, $"todas as construções vieram do bot ({b.automatedBuilds}/{built.Count})");
        while (gm.flow.Current != SceneFlow.Screen.Results) yield return null;
        yield return Wait(0.8f);
        var r = gm.run.history.LastOrDefault();
        Log($"result essence=+{r?.essence} total={gm.save.Data.essence} tier={gm.run.Tier}");
        if (mapIndex == 0 || !b.victory) yield return Shot(mapIndex == 0 ? "alpha_map_transition.png" : "alpha_map2_result.png");
    }

    // Crowd check: a dense column against the commander standing on the road. Measures overlaps.
    IEnumerator CrowdScenario()
    {
        gm.run.StartRun();
        var map = gm.config.Map(maps[0]);
        gm.flow.StartMap(map, map.routes[0]);
        while (gm.flow.Current != SceneFlow.Screen.Battle || !gm.flow.battle) yield return null;
        b = gm.flow.battle;
        var r = b.map.route;
        var post = r.Position(0, r.Length(0) * 0.45f, 0);
        b.commander.transform.position = post;
        b.commander.MoveTo(post);
        b.commander.hp = b.commander.maxHp = 1e6f;
        b.SetSpeedAutomated(1.5f);
        var soldier = b.mapData.roster[0];
        var hound = b.mapData.roster[1];
        int worst = 0;
        float worstOverlap = 0;
        for (int i = 0; i < 40; i++)
        {
            b.waves.SpawnChild(i % 4 == 3 ? hound : soldier, 0, 0, ((i % 3) - 1) * 0.3f);
            for (float t = 0; t < 0.35f; t += Time.deltaTime)
            {
                (int n, float o) = Overlaps();
                worst = Mathf.Max(worst, n);
                worstOverlap = Mathf.Max(worstOverlap, o);
                yield return null;
            }
        }
        for (int k = 0; k < 3; k++)
        {
            yield return new WaitForSeconds(4f);
            (int n, float o) = Overlaps();
            Log($"crowd t={k} enemies={b.enemies.Count} engaged={b.commander.engaged.Count} overlappingPairs={n} deepest={o:0.00}");
            yield return Shot($"alpha_crowd_{k}.png");
        }
        Log($"crowd worst overlapping pairs={worst} deepest overlap={worstOverlap:0.00} units");
    }

    // Pairs of enemies whose bodies overlap by more than 15% of their summed radii.
    (int, float) Overlaps()
    {
        int n = 0;
        float deepest = 0;
        var list = b.enemies;
        for (int i = 0; i < list.Count; i++)
            for (int j = i + 1; j < list.Count; j++)
            {
                if (list[i].movement.mode != EnemyMovement.Mode.Path || list[j].movement.mode != EnemyMovement.Mode.Path) continue;
                var mi = list[i].movement;
                Vector2 d = list[j].transform.position - list[i].transform.position;
                Vector2 dir = Battle.I.map.route.Direction(mi.path, mi.progress);
                float along = Mathf.Abs(Vector2.Dot(d, dir)), across = Mathf.Abs(Vector2.Dot(d, new Vector2(-dir.y, dir.x)));
                float gl = mi.Length + list[j].movement.Length, gw = mi.Radius + list[j].movement.Radius;
                if (along < gl * 0.85f && across < gw * 0.85f) { n++; deepest = Mathf.Max(deepest, Mathf.Min(gl - along, gw - across)); }
            }
        return (n, deepest);
    }

    // ---------------------------------------------------------------- build plans

    // +id = build that tower; ^idN = upgrade the N-th tower of that type.
    static IEnumerable<string> Plan(string map, string strategy)
    {
        switch (strategy)
        {
            case "archers":
                return Enumerable.Range(0, 30).Select(i => i % 3 == 2 ? $"^archer{i / 3 % 6}" : "+archer");
            case "spam":
                return Enumerable.Range(0, 40).Select(i => new[] { "+archer", "+pyre", "+ballista", "+crow", "+spikes" }[i % 5]);
            case "upgrades":
                return new[] { "+archer", "^archer0", "^archer0", "+ballista", "^archer0", "^ballista0", "+pyre", "^ballista0", "^pyre0", "^ballista0", "^pyre0", "^pyre0",
                    "+chapel", "^chapel0", "^chapel0", "^chapel0", "+obelisk", "^obelisk0", "^obelisk0", "^obelisk0" };
        }
        if (map == "map2")
            return new[]
            {
                "+archer", "+pyre", "+archer", "^pyre0", "+spikes", "+ballista", "+pyre", "^archer0", "+chapel", "^pyre1", "+obelisk",
                "^ballista0", "^pyre0", "+crow", "^chapel0", "^obelisk0", "^pyre1", "+spikes", "^ballista0", "^obelisk0", "^crow0", "^pyre0",
                "^archer1", "^ballista0", "^chapel0", "^obelisk0", "+pyre", "^pyre2", "^crow0", "^pyre2", "^archer0", "^archer1",
            };
        return new[]
        {
            "+archer", "+archer", "+pyre", "^archer0", "+spikes", "+ballista", "^pyre0", "+chapel", "+crow", "^ballista0", "^archer1",
            "+ballista", "^chapel0", "^pyre0", "^ballista1", "+obelisk", "^crow0", "^ballista0", "^chapel0", "^ballista1", "^obelisk0",
            "^archer0", "^pyre0", "^crow0", "^ballista0", "^obelisk0", "+pyre", "^pyre1", "^archer1", "^crow0", "^obelisk0", "^pyre1",
        };
    }

    bool TryStep(string s)
    {
        if (s[0] == '+')
        {
            var t = b.config.towers.Find(x => x.id == s.Substring(1));
            if (!t) return true;
            if (b.gold < t.cost) return false;
            var spot = BestSpot(t);
            var tower = spot.HasValue ? b.placement.TryBuild(t, spot.Value, automated: true) : null;
            if (tower) built.Add(tower);
            else Log($"could not place {t.displayName}");
            return true;
        }
        string id = new string(s.Substring(1).TakeWhile(char.IsLetter).ToArray());
        int n = int.Parse(s.Substring(1 + id.Length));
        var target = built.Where(x => x && x.data.id == id).ElementAtOrDefault(n);
        if (!target || !target.NextUpgrade) return true;
        return b.gold >= target.NextUpgrade.cost && b.placement.TryUpgrade(target, automated: true);
    }

    // With the boss alive, idle gold goes into towers covering the road the boss still has to walk.
    void EmergencyBuild()
    {
        var boss = b.boss;
        if (!boss || strategy != "balanced") return;
        foreach (var id in new[] { "ballista", "obelisk", "pyre" })
        {
            var t = b.config.towers.Find(x => x.id == id);
            if (b.gold < t.cost) continue;
            var p = BestSpot(t, boss.Progress + 3);
            if (p.HasValue && b.placement.TryBuild(t, p.Value, automated: true) is TowerController tc)
            {
                built.Add(tc);
                Log($"emergency {t.displayName} at {p.Value} boss route={100 * boss.Progress / b.map.route.Length(boss.movement.path):0}%");
            }
        }
    }

    Vector2? BestSpot(TowerData t, float fromRoute = 0)
    {
        Vector2? best = null;
        float bestScore = -1;
        var area = MapController.BuildArea;
        float step = t.placement == PlacementRule.OnRoad ? 0.25f : 0.35f;
        for (float x = area.xMin + 0.3f; x <= area.xMax - 0.3f; x += step)
            for (float y = area.yMin + 0.3f; y <= area.yMax - 0.3f; y += step)
            {
                var p = new Vector2(x, y);
                if (t.placement == PlacementRule.OnRoad && b.map.route.DistanceToRoad(p) > 0.3f) continue;
                if (!b.zones.CanBuild(t, p, out _, ignoreGold: true)) continue;
                float score = t.kind == TowerKind.Aura ? AuraScore(p, t.stats.auraRadius)
                    : t.kind == TowerKind.Trap ? TrapScore(p)
                    : Coverage(p, t.stats.range, fromRoute);   // ponytail: greedy single-spot score, no lookahead
                if (score > bestScore) { bestScore = score; best = p; }
            }
        return best;
    }

    // Road length within range (all paths), favouring the stretch closer to the fortress.
    float Coverage(Vector2 p, float range, float fromRoute = 0)
    {
        float n = 0;
        var r = b.map.route;
        for (int path = 0; path < r.PathCount; path++)
        {
            float L = r.Length(path);
            for (float s = fromRoute; s < L; s += 0.5f)
            {
                var q = r.Position(path, s, 0);
                if (q.x > -15 && q.y < 7.5f && Vector2.Distance(p, q) <= range) n += 1 + 0.35f * s / L;
            }
        }
        return n;
    }

    // Traps go at the entrance of the kill zone: covered by towers, but early enough that enemies still reach them.
    float TrapScore(Vector2 p)
    {
        var (path, s, _) = b.map.route.Nearest(p);
        float L = b.map.route.Length(path);
        if (s < L * 0.25f || s > L * 0.85f) return 0;
        float cover = b.towers.Count(t => !t.IsSupport && !t.IsTrap && Vector2.Distance(t.Pos, p) <= t.Effective().range);
        bool crowded = b.towers.Any(t => t.IsTrap && Vector2.Distance(t.Pos, p) < 2.5f);
        return (crowded ? 0.2f : 1) * (1 + Mathf.Min(cover, 3)) * (1.6f - s / L);
    }

    float AuraScore(Vector2 p, float radius)
    {
        float score = b.towers.Where(t => !t.IsSupport && !t.IsTrap && Vector2.Distance(t.Pos, p) <= radius).Sum(t => t.invested);
        return score + (Vector2.Distance(CommanderSpot(), p) <= radius ? 150 : 0);
    }

    // Holds the best-supported road point in the last stretch of the main path.
    Vector2 CommanderSpot()
    {
        var r = b.map.route;
        Vector2 best = b.routeData.commanderStart;
        float bestScore = 0, L = r.Length(0);
        for (float s = L * 0.62f; s < L * 0.9f; s += 0.5f)
        {
            var p = r.Position(0, s, 0);
            float score = b.towers.Count(t => !t.IsSupport && !t.IsTrap && Vector2.Distance(t.Pos, p) <= t.Effective().range)
                          + 2 * b.towers.Count(t => t.IsSupport && Vector2.Distance(t.Pos, p) <= t.stats.auraRadius);
            if (score >= bestScore) { bestScore = score; best = p; }
        }
        return best;
    }

    // A careful player: steps aside while the boss passes, heals at a chapel when hurt, else holds the post.
    void ControlCommander()
    {
        var c = b.commander;
        if (!c.Alive || c.Down || b.over) return;
        var boss = b.boss;
        if (c.combat.UltimateReady && (b.EnemiesIn(c.transform.position, c.data.ultimateRadius).Count >= 4 ||
                                      (boss && Vector2.Distance(boss.transform.position, c.transform.position) < c.data.ultimateRadius)))
        {
            c.combat.UseUltimate();
            ultimates++;
        }
        if (boss && Vector2.Distance(boss.transform.position, c.transform.position) < 3.6f)
        {
            c.MoveTo(SideStep(boss));
            return;
        }
        if (c.hp < c.maxHp * 0.4f)
        {
            var chapel = b.towers.FirstOrDefault(t => t.IsSupport);
            c.MoveTo(chapel ? chapel.Pos + new Vector2(0.9f, -0.3f) : b.fortress.Door + new Vector2(-1.6f, -1.2f));
            return;
        }
        if (!boss || Vector2.Distance(boss.transform.position, post) > 4.5f) c.MoveTo(post);
    }

    Vector2 SideStep(EnemyController boss)
    {
        Vector2 best = b.commander.transform.position;
        float bestScore = float.MinValue;
        for (float a = 0; a < 360; a += 30)
        {
            var p = MapController.ClampWalk((Vector2)boss.transform.position + (Vector2)(Quaternion.Euler(0, 0, a) * Vector3.right) * 3.2f);
            float score = Mathf.Min(b.map.route.DistanceToRoad(p), 2.5f) * 2 + Vector2.Distance(p, boss.transform.position);
            if (score > bestScore) { bestScore = score; best = p; }
        }
        return best;
    }

    // ---------------------------------------------------------------- screenshots and logs

    IEnumerator ShotWithGhost(string file, TowerData tower)
    {
        if (!shots || !tower) yield break;
        b.placement.building = tower;
        b.placement.previewAt = BestSpot(tower) ?? Vector2.zero;
        yield return null;
        yield return Shot(file);
        b.placement.building = null;
        b.placement.previewAt = null;
    }

    IEnumerator ShotWithSelection(string file)
    {
        var t = b.towers.OrderByDescending(x => x.invested).FirstOrDefault(x => !x.IsSupport && !x.IsTrap);
        b.placement.Select(t);
        yield return null;
        yield return Shot(file);
        b.placement.Select(null);
    }

    IEnumerator Shot(string file)
    {
        taken.Add(file);
        if (!shots) yield break;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(file);
        yield return null;
        Log($"screenshot {file}");
    }

    static IEnumerator Wait(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
    }

    void Check(bool ok, string what)
    {
        Log(ok ? $"assert ok: {what}" : $"ASSERT FAILED: {what}");
        if (!ok) errors++;
    }

    void LogWaveEnd(int fortressAtStart, int killsAtStart, float waveStart) =>
        Log($"{b.mapData.id} wave={b.waves.WaveNumber} end fortress={b.fortress.hp} (-{fortressAtStart - b.fortress.hp}) kills={b.stats.kills - killsAtStart} " +
            $"gold={b.gold} secs={Time.time - waveStart:0} cmdMinHp={cmdMinHp:0}/{b.commander.maxHp:0} towers=[{Towers()}]");

    string Towers() => string.Join(" ", b.towers.Select(t => $"{t.data.id}{t.tier}"));

    static string Share(Dictionary<string, float> d)
    {
        float total = Mathf.Max(1, d.Values.Sum());
        return string.Join(", ", d.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {100 * x.Value / total:0}% ({x.Value:0})"));
    }

    static void Log(string s) => Debug.Log("SMOKE " + s);
}

static class RouteDataExt
{
    public static string id(this RouteData r) => r ? r.name : "-";
}
