using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Permanent attributes bought with Essence. Values and costs come from CommanderData (never hardcoded here).
public class CommanderMenuController : MonoBehaviour
{
    GameManager gm;
    Text essence, summary;
    readonly List<(AttributeDef def, Text level, Text effect, UI.Btn buy)> rows = new();

    public void Build(GameManager g)
    {
        gm = g;
        var skin = g.config.ui;
        UI.Cover(transform, skin ? skin.commanderBackground : null);
        var c = new Vector2(0.5f, 0.5f);

        // portrait frame of the background: animated idle of the equipped skin
        var equipped = CommanderSkinController.Equipped(g.config, g.save);
        var frame = UI.Rect(transform, "Portrait", c, c, new Vector2(-352, 48), new Vector2(452, 612));
        var splash = equipped ? equipped.splash : null;
        var hero = UI.Image(frame, "Hero", splash, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12),
            splash ? new Vector2(splash.rect.width * 3, splash.rect.height * 3) : new Vector2(360, 460));
        hero.preserveAspect = true;
        var plate = UI.Text(transform, "Plate", c, c, new Vector2(-348, -317), new Vector2(290, 60), 22, TextAnchor.MiddleCenter, UI.Gold, true);
        plate.text = g.config.commander.displayName.ToUpperInvariant();
        var sub = UI.Text(transform, "Title", c, c, new Vector2(-348, -374), new Vector2(460, 30), 17, TextAnchor.MiddleCenter, UI.Dim);
        sub.text = g.config.commander.title + (equipped && !equipped.unlockedByDefault ? $" · {equipped.displayName}" : "");

        var panel = UI.Panel(transform, "Attributes", c, c, new Vector2(340, 10), new Vector2(840, 900), skin ? skin.panelSolid : null).transform;
        UI.Title(panel, "ATRIBUTOS PERMANENTES", new Vector2(0.5f, 1), new Vector2(0, -50), 32, 760);
        essence = UI.Text(panel, "Essence", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -88), new Vector2(760, 36), 24, TextAnchor.MiddleCenter, UI.Violet);
        var icons = new Dictionary<CommanderAttribute, Sprite>
        {
            [CommanderAttribute.Strength] = skin ? skin.axe : null, [CommanderAttribute.Vigor] = skin ? skin.heart : null,
            [CommanderAttribute.Fury] = skin ? skin.fire : null, [CommanderAttribute.Discipline] = skin ? skin.armor : null,
            [CommanderAttribute.Tactics] = skin ? skin.archer : null, [CommanderAttribute.DarkFaith] = skin ? skin.magic : null,
        };
        var defs = g.config.commander.attributes;
        for (int i = 0; i < defs.Count; i++)
        {
            var d = defs[i];
            float y = -150 - i * 104;
            var row = UI.Panel(panel, "Row" + i, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(760, 96), skin ? skin.panelThin : null, false).transform;
            UI.Icon(row, "Icon", d.icon ? d.icon : icons.GetValueOrDefault(d.id), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), 64);
            var name = UI.Text(row, "Name", new Vector2(0, 1), new Vector2(0, 1), new Vector2(92, -8), new Vector2(330, 34), 26, TextAnchor.UpperLeft, UI.Bone, true);
            name.text = d.displayName;
            var desc = UI.Text(row, "Desc", new Vector2(0, 0), new Vector2(0, 0), new Vector2(92, 6), new Vector2(300, 50), 16, TextAnchor.LowerLeft, UI.Dim);
            desc.text = d.description;
            var lv = UI.Text(row, "Level", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-196, -8), new Vector2(170, 30), 22, TextAnchor.UpperRight, UI.Gold);
            var eff = UI.Text(row, "Effect", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-196, 6), new Vector2(190, 50), 16, TextAnchor.LowerRight, UI.Good);
            var buy = UI.Button(row, "Buy", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(170, 70), "", () => Buy(d), 20, false, "upgrade");
            rows.Add((d, lv, eff, buy));
        }
        summary = UI.Text(panel, "Summary", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(780, 80), 17, TextAnchor.MiddleCenter, UI.Dim);
        UI.Button(transform, "Back", new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 36), new Vector2(240, 60), "VOLTAR", () => g.flow.ShowMainMenu(), 24, true);
        Refresh();
    }

    void Buy(AttributeDef d)
    {
        if (gm.save.TryBuyAttribute(d)) Fx2D.Pulse(transform);
        else AudioManager.Play("ui_error");
        Refresh();
    }

    void Refresh()
    {
        var data = gm.save.Data;
        essence.text = $"Essência disponível: {data.essence}";
        foreach (var (def, level, effect, buy) in rows)
        {
            int lv = data.Level(def.id);
            level.text = $"Nível {lv}/{def.maxLevel}";
            effect.text = lv > 0 ? CommanderProgression.Effect(def, lv) : "sem bônus";
            bool max = lv >= def.maxLevel;
            int cost = def.CostFor(lv);
            buy.label.text = max ? "MÁXIMO" : $"+1  ({cost})";
            buy.button.interactable = !max && data.essence >= cost;
        }
        var s = CommanderProgression.Compute(gm.config.commander, data);
        var cd = gm.config.commander;
        summary.text = $"HP {s.maxHp:0} · dano {s.damage:0} · {1 / s.attackInterval:0.00} golpes/s · armadura {s.armor:0.#} · regeneração {s.regen:0.#}/s · tática +{s.tacticsBonus * 100:0}%\n" +
                       $"<color=#D6B264>Poder [Q] {cd.ultimateName}:</color> {s.ultimateDamage:0} de dano em área, recarga {cd.ultimateCooldown:0}s";
    }
}

// Tiny UI feedback helpers (menus have no world to shake).
public static class Fx2D
{
    public static void Pulse(Transform t) => t.gameObject.AddComponent<UiPulse>();
}

public class UiPulse : MonoBehaviour
{
    float t;

    void Update()
    {
        t += Time.unscaledDeltaTime;
        transform.localScale = Vector3.one * (1 + 0.006f * Mathf.Sin(t * 30) * (1 - t / 0.2f));
        if (t >= 0.2f)
        {
            transform.localScale = Vector3.one;
            Destroy(this);
        }
    }
}
