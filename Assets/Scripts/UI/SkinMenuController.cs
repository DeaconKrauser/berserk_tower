using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Skins: unlock with Essence, equip one. Presentation only — stats never change.
public class SkinMenuController : MonoBehaviour
{
    GameManager gm;
    CommanderSkinData shown;
    Image preview, splash;
    UiFlipbook flip;
    Text title, desc, status, essence;
    UI.Btn action;
    readonly List<(CommanderSkinData skin, Image frame, Text state)> cards = new();
    float swap;
    bool attacking;

    public void Build(GameManager g)
    {
        gm = g;
        var skin = g.config.ui;
        UI.Cover(transform, skin ? skin.skinsBackground : null);
        var c = new Vector2(0.5f, 0.5f);

        var list = UI.Panel(transform, "Cards", c, c, new Vector2(-372, 20), new Vector2(640, 900), skin ? skin.panelSolid : null).transform;
        UI.Title(list, "SKINS", new Vector2(0.5f, 1), new Vector2(0, -50), 34, 500);
        essence = UI.Text(list, "Essence", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -86), new Vector2(520, 34), 22, TextAnchor.MiddleCenter, UI.Violet);
        for (int i = 0; i < g.config.skins.Count; i++)
        {
            var s = g.config.skins[i];
            var pos = new Vector2(i % 2 == 0 ? -136 : 136, -290 - (i / 2) * 368);
            var card = UI.Image(list, "Card_" + s.id, skin ? skin.card : null, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), pos, new Vector2(256, 350), null, true);
            var img = UI.Image(card.transform, "Preview", s.splash ? s.splash : s.portrait,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(s.splash ? s.splash.rect.width * 1.25f : 200, s.splash ? s.splash.rect.height * 1.25f : 240));
            img.preserveAspect = true;
            var name = UI.Text(card.transform, "Name", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 52), new Vector2(240, 50), 19, TextAnchor.MiddleCenter, UI.Bone, true);
            name.text = s.displayName;
            var state = UI.Text(card.transform, "State", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(240, 30), 18, TextAnchor.MiddleCenter, UI.Dim);
            var btn = card.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { AudioManager.Play("ui_click"); Show(s); });
            card.gameObject.AddComponent<HoverSound>();
            cards.Add((s, card, state));
        }

        var right = UI.Panel(transform, "Preview", c, c, new Vector2(360, 18), new Vector2(700, 900), skin ? skin.panelSolid : null).transform;
        title = UI.Title(right, "", new Vector2(0.5f, 1), new Vector2(0, -50), 30, 600);
        splash = UI.Image(right, "Splash", null, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-70, -92), new Vector2(441, 570));
        splash.preserveAspect = true;
        var inGame = UI.Image(right, "InGameFrame", gm.config.ui ? gm.config.ui.panelThin : null, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -110), new Vector2(170, 210));
        UI.Text(inGame.transform, "Label", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(160, 24), 15, TextAnchor.MiddleCenter, UI.Dim).text = "em batalha";
        preview = UI.Image(inGame.transform, "Big", null, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(150, 170));
        preview.preserveAspect = true;
        flip = preview.gameObject.AddComponent<UiFlipbook>();
        desc = UI.Text(right, "Desc", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(600, 70), 19, TextAnchor.UpperCenter, UI.Bone);
        status = UI.Text(right, "Status", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 112), new Vector2(560, 34), 19, TextAnchor.MiddleCenter, UI.Dim);
        action = UI.Button(right, "Action", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(360, 66), "", Act, 24, true, "upgrade");
        var note = UI.Text(transform, "Note", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(360, 30), new Vector2(700, 30), 17, TextAnchor.MiddleCenter, UI.Dim);
        note.text = "Cada skin muda a aparência e o poder [Q] do comandante; os atributos são os mesmos.";
        UI.Button(transform, "Back", new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 36), new Vector2(240, 60), "VOLTAR", () => g.flow.ShowMainMenu(), 24, true);
        Show(CommanderSkinController.Equipped(g.config, g.save));
    }

    void Show(CommanderSkinData s)
    {
        shown = s;
        attacking = false;
        swap = 3f;
        Refresh();
    }

    void Act()
    {
        var data = gm.save.Data;
        if (!data.HasSkin(shown))
        {
            if (!gm.save.TryUnlockSkin(shown)) AudioManager.Play("ui_error");
        }
        else gm.save.Equip(shown);
        Refresh();
    }

    void Refresh()
    {
        if (!shown) return;
        var data = gm.save.Data;
        essence.text = $"Essência: {data.essence}";
        title.text = shown.displayName.ToUpperInvariant();
        desc.text = shown.description + (string.IsNullOrEmpty(shown.powerName) ? "" : $"\n<color=#D6B264>Poder [Q] {shown.powerName}</color> · recarga {shown.powerCooldown:0}s: {shown.powerDescription}");
        bool owned = data.HasSkin(shown), equipped = data.equippedSkin == shown.id;
        status.text = equipped ? "Equipada" : owned ? "Possuída" : $"Bloqueada · {shown.unlockCost} de Essência";
        action.label.text = equipped ? "EQUIPADA" : owned ? "EQUIPAR" : $"DESBLOQUEAR ({shown.unlockCost})";
        action.button.interactable = !equipped && (owned || data.essence >= shown.unlockCost);
        flip.frames = shown.previewIdle;
        splash.sprite = shown.splash;
        if (shown.splash) splash.rectTransform.sizeDelta = new Vector2(shown.splash.rect.width * 3, shown.splash.rect.height * 3);
        if (shown.previewIdle != null && shown.previewIdle.Length > 0) preview.sprite = shown.previewIdle[0];
        foreach (var (s, frame, state) in cards)
        {
            bool own = data.HasSkin(s), eq = data.equippedSkin == s.id;
            frame.sprite = gm.config.ui ? (s == shown ? gm.config.ui.cardSelected : own ? gm.config.ui.card : gm.config.ui.cardDisabled) : null;
            state.text = eq ? "<color=#F2A33A>EQUIPADA</color>" : own ? "possuída" : $"{s.unlockCost} Essência";
        }
    }

    // The big preview alternates idle and attack so the player sees the skin in motion.
    void Update()
    {
        if (!shown) return;
        if ((swap -= Time.unscaledDeltaTime) > 0) return;
        attacking = !attacking;
        swap = attacking ? 1.2f : 3f;
        var f = attacking ? shown.previewAttack : shown.previewIdle;
        if (f != null && f.Length > 0) flip.frames = f;
        flip.fps = attacking ? 10 : 7;
    }
}
