using UnityEngine;
using UnityEngine.UI;

// NOVO JOGO · COMANDANTE · SKINS · OPÇÕES · SAIR. There is intentionally no "Continuar": runs are never saved.
public class MainMenuController : MonoBehaviour
{
    public static readonly string[] Entries = { "NOVO JOGO", "COMANDANTE", "SKINS", "OPÇÕES", "SAIR" };
    Text essence;
    GameManager gm;

    public void Build(GameManager g)
    {
        gm = g;
        var skin = g.config.ui;
        UI.Cover(transform, skin ? skin.menuBackground : null);
        var vignette = UI.Stretch(transform, "Vignette").gameObject.AddComponent<Image>();
        vignette.sprite = skin ? skin.vignette : null;
        vignette.color = new Color(1, 1, 1, 0.85f);
        vignette.raycastTarget = false;

        var c = new Vector2(0.5f, 0.5f);
        var title = UI.Title(transform, "O ÚLTIMO BASTIÃO", c, new Vector2(2, 128), 40, 520);
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        var sub = UI.Text(transform, "Subtitle", c, c, new Vector2(2, 86), new Vector2(500, 36), 22, TextAnchor.MiddleCenter, UI.Dim);
        sub.text = "— Eclipse Negro —";
        UI.Image(transform, "Divider", skin ? skin.divider : null, c, c, new Vector2(2, 58), new Vector2(320, 10));

        var actions = new System.Action[]
        {
            () => g.flow.ShowMapSelect(true),
            g.flow.ShowCommander,
            g.flow.ShowSkins,
            g.flow.ShowOptions,
            g.flow.Quit,
        };
        for (int i = 0; i < Entries.Length; i++)
        {
            var b = UI.Button(transform, "Btn_" + Entries[i], c, c, new Vector2(2, 8 - i * 74), new Vector2(330, 60), Entries[i], actions[i], 26, true);
            b.label.color = i == 0 ? UI.Ember : UI.Bone;
            b.label.GetComponent<DisabledText>().SetBase(b.label.color);
        }

        var corner = new Vector2(1, 1);
        UI.Panel(transform, "EssencePanel", corner, corner, new Vector2(-24, -24), new Vector2(330, 70), skin ? skin.panelDark : null, false);
        UI.Icon(transform, "EssenceIcon", skin ? skin.magic : null, corner, new Vector2(0.5f, 0.5f), new Vector2(-312, -59), 44);
        essence = UI.Text(transform, "Essence", corner, corner, new Vector2(-40, -36), new Vector2(240, 44), 26, TextAnchor.MiddleRight, UI.Violet);
        var ver = UI.Text(transform, "Version", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 14), new Vector2(500, 30), 18, TextAnchor.LowerRight, UI.Dim);
        ver.text = "Alpha 0.2 · arte provisória Syntx";
    }

    void Update()
    {
        if (gm && essence) essence.text = $"Essência  {gm.save.Data.essence}";
    }
}
