using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// New run: choose the first map. After a won map: ESCOLHA SEU PRÓXIMO DESTINO (repeat it, or go to the other one).
// Each card already shows the road layout that will be used if chosen (layouts are drawn per stage).
public class MapSelectionController : MonoBehaviour
{
    public void Build(GameManager g, bool newRun)
    {
        var skin = g.config.ui;
        UI.Cover(transform, skin ? skin.battlefieldBackground : null, new Color(0.42f, 0.42f, 0.48f));
        var c = new Vector2(0.5f, 0.5f);
        var run = g.run;
        UI.Title(transform, newRun ? "ESCOLHA SEU DESTINO" : "ESCOLHA SEU PRÓXIMO DESTINO", new Vector2(0.5f, 1), new Vector2(0, -70), 46);
        var info = UI.Text(transform, "RunInfo", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -116), new Vector2(1400, 36), 22, TextAnchor.MiddleCenter, UI.Dim);
        var diff = run.Difficulty;
        info.text = newRun
            ? "Uma run continua de mapa em mapa até a derrota. As construções não acompanham você."
            : $"Etapa {run.Tier + 1} da run · {diff} · ouro levado: {run.CarriedGold} · Essência na run: {run.EssenceThisRun}";

        var maps = g.config.maps;
        for (int i = 0; i < maps.Count; i++)
        {
            var m = maps[i];
            var route = m.routes[Random.Range(0, m.routes.Count)];
            float x = (i - (maps.Count - 1) / 2f) * 760;
            var card = UI.Panel(transform, "Map_" + m.id, c, c, new Vector2(x, -36), new Vector2(720, 800), skin ? skin.panelSolid : null).transform;
            UI.Title(card, m.displayName.ToUpperInvariant(), new Vector2(0.5f, 1), new Vector2(0, -48), 32, 680);
            var tex = MapController.Preview(m, route);
            var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 32);
            UI.Image(card, "PreviewFrame", skin ? skin.panelThin : null, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -88), new Vector2(624, 354));
            var pv = UI.Image(card, "Preview", spr, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(600, 330));
            pv.preserveAspect = true;
            var layout = UI.Text(card, "Layout", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -440), new Vector2(660, 30), 20, TextAnchor.MiddleCenter, UI.Gold);
            layout.text = $"Rota: {route.displayName} ({route.layout})";
            var desc = UI.Text(card, "Desc", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -476), new Vector2(640, 80), 19, TextAnchor.UpperCenter, UI.Bone);
            desc.text = m.description;
            var roster = UI.Text(card, "Roster", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -560), new Vector2(640, 120), 18, TextAnchor.UpperCenter, UI.Dim);
            roster.text = "Inimigos: " + string.Join(" · ", m.roster.Select(e => $"<color=#E8DCC0>{e.displayName}</color> ({EnemyTagNames.Join(e.tags & ~EnemyTag.Undead)})"));
            var gold = UI.Text(card, "Gold", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 104), new Vector2(640, 30), 20, TextAnchor.MiddleCenter, UI.Ember);
            gold.text = $"{m.waves.Count} ondas · ouro inicial {run.StartingGold(m)} · fortaleza {m.fortressHp} HP";
            var ess = UI.Text(card, "Essence", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 136), new Vector2(640, 26), 17, TextAnchor.MiddleCenter, UI.Violet);
            ess.text = run.IsReplay(m) ? $"Fase já vencida: Essência reduzida ({g.config.meta.replayMultiplier * 100:0}%)" : "Fase nova: Essência integral";
            var mm = m;
            var rr = route;
            UI.Button(card, "Go", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(320, 64),
                newRun ? "MARCHAR" : (run.history.Count > 0 && run.history[^1].map == m ? "REPETIR" : "SEGUIR PARA LÁ"), () => g.flow.StartMap(mm, rr), 26, true, "wave_start");
        }
        UI.Button(transform, "Back", new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 36), new Vector2(300, 60), newRun ? "VOLTAR" : "ENCERRAR A RUN", () =>
        {
            g.run.EndRun();
            g.flow.ShowMainMenu();
        }, 22, true);
    }
}
