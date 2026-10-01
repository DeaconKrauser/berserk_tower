using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Writes the data tables of Docs/towers.md, Docs/enemies.md and Docs/maps.md from the real assets,
// so the documentation always matches the numbers the game uses. Hand-written text lives above the marker.
public static class DocsExporter
{
    const string Marker = "<!-- tabelas geradas por Bastião/Exportar tabelas para Docs: não editar abaixo -->";

    [MenuItem("Bastião/Exportar tabelas para Docs")]
    public static void Export()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Resources/GameConfig.asset");
        Write("Docs/towers.md", Towers(cfg));
        Write("Docs/enemies.md", Enemies(cfg));
        Write("Docs/maps.md", Maps(cfg));
        Debug.Log("DocsExporter: Docs/towers.md, enemies.md, maps.md atualizados");
    }

    static void Write(string path, string generated)
    {
        string head = File.Exists(path) ? File.ReadAllText(path) : "";
        int i = head.IndexOf(Marker);
        if (i >= 0) head = head.Substring(0, i);
        File.WriteAllText(path, head.TrimEnd() + "\n\n" + Marker + "\n\n" + generated);
    }

    static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    static string Towers(GameConfig cfg)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Venda: {cfg.sellRefund * 100:0}% do investido (100% se vendida na mesma preparação em que foi construída). " +
                      $"Dano físico mínimo através de armadura: {cfg.minPhysicalDamage * 100:0}%.\n");
        foreach (var t in cfg.towers)
        {
            sb.AppendLine($"### {t.displayName}  (tecla {cfg.towers.IndexOf(t) + 1})");
            sb.AppendLine($"{t.role}. **{t.goodAgainst}.** Tipo: {t.kind}, construção: {(t.placement == PlacementRule.OnRoad ? "sobre a estrada" : "fora da estrada")}, alvo: {t.targeting}, área ocupada: raio {F(t.footprintRadius)}.\n");
            sb.AppendLine("| Tier | Nome | Custo | Dano | Ataques/s | Alcance | Efeitos |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            for (int tier = 1; tier <= t.MaxTier; tier++)
            {
                var s = t.StatsAt(tier);
                string name = tier == 1 ? t.displayName : t.upgrades[tier - 2].displayName;
                int cost = tier == 1 ? t.cost : t.upgrades[tier - 2].cost;
                var lines = TowerPanelController.StatLines(t, s).Where(l => l.label is not ("Dano" or "Ataques/s" or "Alcance" or "Prioridade"));
                string dmg = t.kind == TowerKind.Aura ? "—" : $"{F(s.damage)} {DamageTypeNames.Pt(s.damageType)}";
                string aps = t.kind == TowerKind.Aura ? "—" : F(s.AttacksPerSecond);
                string range = t.kind == TowerKind.Aura ? F(s.auraRadius) : t.kind == TowerKind.Trap ? "sobre ela" : F(s.range);
                sb.AppendLine($"| {tier} | {name} | {cost} | {dmg} | {aps} | {range} | {string.Join("; ", lines.Select(l => $"{l.label} {l.value}"))} |");
            }
            for (int i = 0; i < t.upgrades.Count; i++) sb.AppendLine($"- Tier {i + 2}: {t.upgrades[i].description}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    static string Enemies(GameConfig cfg)
    {
        var all = cfg.maps.SelectMany(m => m.waves).SelectMany(w => w.groups).SelectMany(g => g.entries).Select(e => e.enemy)
            .Concat(cfg.maps.SelectMany(m => m.roster)).Where(e => e).Distinct().ToList();
        foreach (var e in all.ToList())
        {
            if (e.splitInto && !all.Contains(e.splitInto)) all.Add(e.splitInto);
            if (e.boss && e.boss.summon && !all.Contains(e.boss.summon)) all.Add(e.boss.summon);
        }
        var sb = new StringBuilder();
        sb.AppendLine("| Inimigo | Tags | HP | Armadura | Res. física | Res. fogo | Res. magia | Res. sangue | Vel. | Dano fortaleza | Corpo a corpo | Ouro |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var e in all)
            sb.AppendLine($"| {e.displayName} | {EnemyTagNames.Join(e.tags)} | {F(e.maxHp)} | {F(e.armor)} | {e.physicalResistance * 100:0}% | {e.fireResistance * 100:0}% | " +
                          $"{e.magicResistance * 100:0}% | {e.bleedResistance * 100:0}% | {F(e.movementSpeed)} | {e.damageToFortress} | {F(e.meleeDamage)} / {F(e.attackInterval)}s | {e.goldReward} |");
        sb.AppendLine();
        foreach (var e in all)
        {
            var extra = new List<string>();
            if (e.castInterval > 0) extra.Add($"a cada {F(e.castInterval)}s: escudo {F(e.castShield)}{(e.castHeal > 0 ? $" e cura {F(e.castHeal)}" : "")} em aliados num raio {F(e.castRadius)}");
            if (e.feedHeal > 0) extra.Add($"cura {F(e.feedHeal)} quando um inimigo morre a até {F(e.feedRadius)}");
            if (e.regenPerSecond > 0) extra.Add($"regenera {F(e.regenPerSecond)} HP/s (não enquanto queima)");
            if (e.splitInto) extra.Add($"ao morrer vira {e.splitCount}× {e.splitInto.displayName}");
            if (e.boss)
            {
                var b = e.boss;
                extra.Add($"chefe: entrada de {F(b.introSeconds)}s; invoca {b.summonCount}× {(b.summon ? b.summon.displayName : "-")} a cada {F(b.summonInterval)}s; " +
                          $"**{b.specialName}** a cada {F(b.specialInterval)}s (raio {F(b.specialRadius)}, {F(b.specialDamage)} no comandante, torres atordoadas {F(b.specialStunSeconds)}s); " +
                          $"enfurece abaixo de {b.enrageBelowHp * 100:0}% (velocidade ×{F(b.enrageSpeedMultiplier)}, dano ×{F(b.enrageDamageMultiplier)}); " +
                          $"execução da fortaleza: {(b.fortressExecution ? "SIM" : "não")}");
            }
            sb.AppendLine($"- **{e.displayName}**: {e.description}{(extra.Count > 0 ? " — " + string.Join("; ", extra) : "")}");
        }
        return sb.ToString();
    }

    static string Maps(GameConfig cfg)
    {
        var sb = new StringBuilder();
        foreach (var m in cfg.maps)
        {
            sb.AppendLine($"## {m.displayName}");
            sb.AppendLine($"{m.description}\n");
            sb.AppendLine($"Ouro inicial {m.startingGold} · fortaleza {m.fortressHp} HP · bônus por onda {m.waveClearBonus} + {m.waveClearBonusPerWave} × onda · música `{m.musicId}` · ambiente `{m.ambientId}`\n");
            sb.AppendLine("Rotas (uma é sorteada a cada etapa e aparece na tela de escolha):\n");
            foreach (var r in m.routes)
                sb.AppendLine($"- **{r.displayName}** ({r.layout}): {r.paths.Count} caminho(s), largura {F(r.roadHalfWidth * 2)}, {r.zones.Count} zonas sem construção");
            sb.AppendLine("\n| Onda | Título | Grupos (sub-ondas) |");
            sb.AppendLine("|---|---|---|");
            for (int i = 0; i < m.waves.Count; i++)
            {
                var w = m.waves[i];
                var groups = w.groups.Select((g, k) =>
                    $"{(g.minRunTier > 0 ? $"*[etapa {g.minRunTier + 1}+]* " : "")}{(g.delay > 0 ? $"+{F(g.delay)}s: " : "")}" +
                    string.Join(" + ", g.entries.Select(e => $"{e.count}× {(e.enemy ? e.enemy.displayName : "?")}")) + $" (a cada {F(g.interval)}s)");
                sb.AppendLine($"| {i + 1} | {w.title} | {string.Join("<br>", groups)} |");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
