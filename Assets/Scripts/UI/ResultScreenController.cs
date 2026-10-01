using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// End of a map: what happened and the Essence earned; then the next destination (victory) or the menu (defeat).
public class ResultScreenController : MonoBehaviour
{
    public void Build(GameManager g, MapResult r)
    {
        var skin = g.config.ui;
        UI.Fill(transform, "Dim", new Color(0.02f, 0.02f, 0.03f, 0.72f), true);
        var c = new Vector2(0.5f, 0.5f);
        var p = UI.Panel(transform, "Panel", c, c, Vector2.zero, new Vector2(980, 860), skin ? skin.panelSolid : null).transform;
        var title = UI.Title(p, r.victory ? "MAPA CONCLUÍDO" : "DERROTA", new Vector2(0.5f, 1), new Vector2(0, -62), 50, 900);
        title.color = r.victory ? UI.Gold : UI.Bad;
        var sub = UI.Text(p, "Map", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(900, 34), 24, TextAnchor.MiddleCenter, UI.Bone);
        sub.text = $"{r.map.displayName} · {r.route.displayName} · etapa {r.tier + 1}";
        var why = UI.Text(p, "Why", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(900, 34), 20, TextAnchor.MiddleCenter, UI.Dim);
        why.text = r.endReason;

        (string, string)[] lines =
        {
            ("Inimigos mortos", $"{r.kills}  ({r.eliteKills} elites, {r.bossKills} chefes)"),
            ("Ondas vencidas", $"{r.wavesCleared}/{r.wavesTotal}"),
            ("Ouro ganho / gasto", $"{r.goldEarned} / {r.goldSpent}"),
            ("Torres construídas", $"{r.towersBuilt}  ({r.upgrades} melhorias, {r.towersSold} vendidas)"),
            ($"Dano de {g.config.commander.displayName}", $"{r.commanderDamage:0}" + (r.commanderFalls > 0 ? $"  (caiu {r.commanderFalls}×)" : "")),
            ("Fortaleza", $"{r.fortressHp}/{r.fortressHpMax}"),
            ("Tempo", $"{(int)(r.seconds / 60)}:{(int)(r.seconds % 60):00}"),
        };
        for (int i = 0; i < lines.Length; i++)
        {
            float y = -210 - i * 44;
            UI.Text(p, "L" + i, new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, y), new Vector2(380, 40), 24, TextAnchor.MiddleLeft, UI.Dim).text = lines[i].Item1;
            UI.Text(p, "V" + i, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-110, y), new Vector2(420, 40), 24, TextAnchor.MiddleRight, UI.Bone).text = lines[i].Item2;
        }
        float total = Mathf.Max(1, r.damageBySource.Values.Sum());
        var share = UI.Text(p, "Share", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -530), new Vector2(860, 60), 18, TextAnchor.UpperCenter, UI.Dim);
        share.text = "Dano: " + string.Join(" · ", r.damageBySource.OrderByDescending(x => x.Value).Take(5).Select(x => $"{x.Key} {100 * x.Value / total:0}%"));

        UI.Icon(p, "EssIcon", skin ? skin.magic : null, new Vector2(0.5f, 1), c, new Vector2(-190, -632), 56);
        var ess = UI.Text(p, "Essence", new Vector2(0.5f, 1), new Vector2(0, 0.5f), new Vector2(-150, -632), new Vector2(560, 50), 32, TextAnchor.MiddleLeft, UI.Violet, true);
        ess.text = $"+{r.essence} Essência  <size=20><color=#968C84>(total {g.save.Data.essence})</color></size>";
        var note = UI.Text(p, "EssenceNote", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -668), new Vector2(860, 30), 17, TextAnchor.MiddleCenter, UI.Dim);
        note.text = r.replay ? "Fase já vencida antes: Essência reduzida. Fases novas (outro mapa ou etapa mais funda) rendem a Essência integral."
                  : r.victory ? "Primeira vitória nesta fase: Essência integral." : "Fase ainda não vencida: a Essência conta integral, tente de novo quando quiser.";

        if (r.victory)
        {
            UI.Button(p, "Next", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(170, 40), new Vector2(380, 68), "PRÓXIMO DESTINO", () => g.flow.ShowMapSelect(false), 26, true, "wave_start");
            UI.Button(p, "End", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-230, 40), new Vector2(340, 68), "ENCERRAR A RUN", () =>
            {
                g.run.EndRun();
                g.flow.ShowMainMenu();
            }, 22, true);
        }
        else
            UI.Button(p, "Menu", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(380, 68), "VOLTAR AO MENU", () => g.flow.ShowMainMenu(), 26, true);
    }
}
