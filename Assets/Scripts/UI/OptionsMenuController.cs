using UnityEngine;
using UnityEngine.UI;

// Volumes (Master, Música, Efeitos, Interface, Ambiente), window and feedback options. Saved with the meta save.
public class OptionsMenuController : MonoBehaviour
{
    // 16:9 sizes the monitor supports (plus the common ones up to its size), smallest first.
    public static System.Collections.Generic.List<Vector2Int> Resolutions()
    {
        var set = new System.Collections.Generic.SortedSet<(int, int)>();
        foreach (var r in Screen.resolutions) set.Add((r.width, r.height));
        int maxW = Mathf.Max(Display.main.systemWidth, 1280), maxH = Mathf.Max(Display.main.systemHeight, 720);
        foreach (var (w, h) in new[] { (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160) })
            if (w <= maxW && h <= maxH) set.Add((w, h));
        var list = new System.Collections.Generic.List<Vector2Int>();
        foreach (var (w, h) in set)
            if (w >= 1024 && h >= 576) list.Add(new Vector2Int(w, h));
        if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
        return list;
    }

    public void Build(GameManager g)
    {
        var skin = g.config.ui;
        UI.Cover(transform, skin ? skin.menuBackground : null, new Color(0.45f, 0.45f, 0.5f));
        var c = new Vector2(0.5f, 0.5f);
        var panel = UI.Panel(transform, "Panel", c, c, Vector2.zero, new Vector2(820, 860), skin ? skin.panelSolid : null).transform;
        UI.Title(panel, "OPÇÕES", new Vector2(0.5f, 1), new Vector2(0, -56), 40, 600);
        var s = g.save.Data.settings;
        (string label, float value, System.Action<float> set)[] rows =
        {
            ("Volume geral", s.master, v => s.master = v),
            ("Música", s.music, v => s.music = v),
            ("Efeitos", s.sfx, v => s.sfx = v),
            ("Interface", s.ui, v => s.ui = v),
            ("Ambiente", s.ambient, v => s.ambient = v),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            float y = -140 - i * 70;
            UI.Text(panel, "L" + i, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(70, y), new Vector2(260, 40), 26, TextAnchor.MiddleLeft, UI.Bone).text = r.label;
            UI.Slider(panel, "S" + i, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(340, y), new Vector2(400, 40), r.value, v =>
            {
                r.set(v);
                g.ApplySettings();
            });
        }
        // resolution: ◀ 1920×1080 ▶ (applied at once, saved with the options)
        var res = Resolutions();
        int current = res.FindIndex(r => r.x == (s.resolutionWidth > 0 ? s.resolutionWidth : Screen.width) && r.y == (s.resolutionHeight > 0 ? s.resolutionHeight : Screen.height));
        if (current < 0) current = res.Count - 1;
        UI.Text(panel, "ResLabel", new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(70, -490), new Vector2(260, 40), 26, TextAnchor.MiddleLeft, UI.Bone).text = "Resolução";
        var resText = UI.Text(panel, "ResValue", new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(540, -490), new Vector2(240, 40), 24, TextAnchor.MiddleCenter, UI.Ember, true);
        void ShowRes() => resText.text = $"{res[current].x} × {res[current].y}";
        void Pick(int delta)
        {
            current = Mathf.Clamp(current + delta, 0, res.Count - 1);
            s.resolutionWidth = res[current].x;
            s.resolutionHeight = res[current].y;
            ShowRes();
            g.ApplySettings();
        }
        ShowRes();
        UI.Button(panel, "ResPrev", new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(390, -490), new Vector2(56, 44), "◀", () => Pick(-1), 22);
        UI.Button(panel, "ResNext", new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(690, -490), new Vector2(56, 44), "▶", () => Pick(1), 22);
        UI.Toggle(panel, "Fullscreen", new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(70, -546), "Tela cheia", s.fullscreen, v => { s.fullscreen = v; g.ApplySettings(); });
        UI.Toggle(panel, "Shake", new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(70, -596), "Tremor de tela", s.screenShake, v => { s.screenShake = v; g.ApplySettings(); });
        UI.Toggle(panel, "Numbers", new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(70, -646), "Números de dano e ouro", s.showDamageNumbers, v => { s.showDamageNumbers = v; g.ApplySettings(); });
        UI.Toggle(panel, "AutoWaves", new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(70, -696), $"Ondas automáticas ({g.config.autoWaveDelay:0}s entre ondas)", s.autoWaves, v => { s.autoWaves = v; g.save.Save(); });
        UI.Button(panel, "Back", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(300, 60), "SALVAR E VOLTAR", () =>
        {
            g.save.Save();
            g.flow.ShowMainMenu();
        }, 24, true);
    }
}
