using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Selected tower: name, tier, damage, attack rate, range, penetration/effects, what it is good against,
// the next upgrade (with "before → after"), UPGRADE and SELL buttons with prices.
public class TowerPanelController : MonoBehaviour
{
    GameManager gm;
    Battle b;
    RectTransform rt;
    Text title, tier, role, good, labels, values, buff, next;
    Image icon;
    UI.Btn upgrade, sell, target;
    readonly Image[] pips = new Image[4];

    public void Build(GameManager g, Battle battle)
    {
        gm = g;
        b = battle;
        rt = (RectTransform)transform;
        var skin = g.config.ui;
        var bg = UI.Stretch(transform, "Bg").gameObject.AddComponent<Image>();
        bg.sprite = skin ? skin.panel : null;
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = UI.PixelScale;
        var tl = new Vector2(0, 1);
        icon = UI.Image(transform, "Icon", null, tl, tl, new Vector2(20, -20), new Vector2(84, 96));
        icon.preserveAspect = true;
        title = UI.Text(transform, "Title", tl, tl, new Vector2(116, -18), new Vector2(330, 40), 28, TextAnchor.MiddleLeft, UI.Ember, true);
        title.verticalOverflow = VerticalWrapMode.Truncate;
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 15;
        title.resizeTextMaxSize = 28;
        tier = UI.Text(transform, "Tier", tl, tl, new Vector2(116, -58), new Vector2(330, 30), 20, TextAnchor.MiddleLeft, UI.Gold);
        for (int i = 0; i < pips.Length; i++)
            pips[i] = UI.Image(transform, "Pip" + i, null, tl, tl, new Vector2(118 + i * 22, -92), new Vector2(16, 16));
        target = UI.Button(transform, "Target", tl, tl, new Vector2(214, -82), new Vector2(226, 36), "", () => b.placement.selected?.CyclePriority(), 15, true, "ui_select");
        role = UI.Text(transform, "Role", tl, tl, new Vector2(22, -124), new Vector2(416, 50), 17, TextAnchor.UpperLeft, UI.Dim);
        good = UI.Text(transform, "Good", tl, tl, new Vector2(22, -174), new Vector2(416, 30), 18, TextAnchor.UpperLeft, UI.Good);
        labels = UI.Text(transform, "Labels", tl, tl, new Vector2(22, -210), new Vector2(230, 250), 20, TextAnchor.UpperLeft, UI.Dim);
        values = UI.Text(transform, "Values", tl, tl, new Vector2(196, -210), new Vector2(242, 250), 20, TextAnchor.UpperRight, UI.Bone);
        buff = UI.Text(transform, "Buff", tl, tl, new Vector2(22, -466), new Vector2(416, 44), 16, TextAnchor.UpperLeft, UI.Gold);
        next = UI.Text(transform, "Next", tl, tl, new Vector2(22, -508), new Vector2(416, 130), 17, TextAnchor.UpperLeft, UI.Bone);
        upgrade = UI.Button(transform, "Upgrade", new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 16), new Vector2(262, 62), "", () => b.placement.TryUpgrade(b.placement.selected), 21, true, "ui_select");
        sell = UI.Button(transform, "Sell", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 16), new Vector2(158, 62), "", () => b.placement.Sell(b.placement.selected), 19, true, "ui_select");
        gameObject.SetActive(false);
    }

    // Called by the HUD every frame (this object is inactive while nothing is selected).
    public void Sync()
    {
        var t = b ? b.placement.selected : null;
        gameObject.SetActive(t);
        if (!t) return;
        bool right = Camera.main && Camera.main.WorldToViewportPoint(t.Pos).x < 0.6f;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(right ? 1 : 0, 0.5f);
        rt.anchoredPosition = new Vector2(right ? -16 : 16, -10);

        var skin = gm.config.ui;
        icon.sprite = t.data.SpriteAt(t.tier);
        title.text = t.data.displayName;
        tier.text = $"Tier {t.tier}/{t.MaxTier} · {t.TierName}";
        for (int i = 0; i < pips.Length; i++)
        {
            pips[i].gameObject.SetActive(i < t.MaxTier);
            pips[i].sprite = Gfx.Pip;
            pips[i].color = i < t.tier ? UI.Ember : new Color32(60, 56, 64, 255);
        }
        role.text = t.data.role;
        good.text = string.IsNullOrEmpty(t.data.goodAgainst) ? "" : "▲ " + t.data.goodAgainst;
        var eff = t.Effective(out float db, out float sb, out float rb);
        var lines = StatLines(t.data, eff);
        labels.text = string.Join("\n", lines.Select(l => l.label));
        values.text = string.Join("\n", lines.Select(l => l.value));
        var parts = new List<string>();
        if (db > 0 || sb > 0 || rb > 0) parts.Add($"Bônus: +{db * 100:0}% dano, +{sb * 100:0}% velocidade, +{rb * 100:0}% alcance");
        if (t.stunLeft > 0) parts.Add($"<color=#8CE65A>ATORDOADA ({t.stunLeft:0.0}s)</color>");
        if (t.IsTrap && t.stats.trapDurability > 0)
            parts.Add($"Durabilidade: {t.durability}/{t.stats.trapDurability}" + (t.DurabilityFraction < 0.3f ? "  <color=#FF6054>quase quebrando</color>" : ""));
        buff.text = string.Join("\n", parts);
        // stack the blocks below the stat list so long lists never run into the upgrade text
        float y = -210 - labels.preferredHeight - 10;
        buff.rectTransform.anchoredPosition = new Vector2(22, y);
        if (buff.text.Length > 0) y -= buff.preferredHeight + 10;
        next.rectTransform.anchoredPosition = new Vector2(22, y);
        next.rectTransform.sizeDelta = new Vector2(416, Mathf.Max(60, y + rt.rect.height - 86));   // down to the buttons

        var n = t.NextUpgrade;
        if (n)
        {
            next.text = $"<color=#F2A33A>Próximo: {n.displayName}</color>\n{n.description}\n<color=#A89C8E>{Diff(t.data, t.stats, n.stats)}</color>";
            upgrade.label.text = $"MELHORAR  {n.cost} [U]";
            upgrade.button.interactable = !b.over && b.gold >= n.cost;
        }
        else
        {
            next.text = "<color=#D6B264>Tier máximo.</color>";
            upgrade.label.text = "TIER MÁXIMO";
            upgrade.button.interactable = false;
        }
        sell.label.text = $"VENDER +{b.placement.SellValue(t)}";
        bool aims = t.data.kind is TowerKind.Projectile or TowerKind.Chain;
        target.button.gameObject.SetActive(aims);
        if (aims) target.label.text = $"ALVO: {TargetPriorityNames.Pt(t.priority).ToUpperInvariant()} [T]";
        sell.button.interactable = !b.over;
    }

    public static List<(string label, string value)> StatLines(TowerData d, TowerStats s)
    {
        var l = new List<(string, string)>();
        if (d.kind == TowerKind.Aura)
        {
            l.Add(("Raio da aura", $"{s.auraRadius:0.0}"));
            if (s.auraDamageBonus > 0) l.Add(("Dano das torres", $"+{s.auraDamageBonus * 100:0}%"));
            if (s.auraAttackSpeedBonus > 0) l.Add(("Vel. das torres", $"+{s.auraAttackSpeedBonus * 100:0}%"));
            if (s.auraRangeBonus > 0) l.Add(("Alcance das torres", $"+{s.auraRangeBonus * 100:0}%"));
            if (s.auraArmorShred > 0) l.Add(("Armadura inimiga", $"-{s.auraArmorShred:0}"));
            if (s.commanderHealPerSecond > 0) l.Add(("Cura do comandante", $"{s.commanderHealPerSecond:0} HP/s"));
            return l;
        }
        l.Add(("Dano", $"{s.damage:0} {DamageTypeNames.Pt(s.damageType)}"));
        l.Add(("Ataques/s", $"{s.AttacksPerSecond:0.00}"));
        if (d.kind != TowerKind.Trap) l.Add(("Alcance", $"{s.range:0.0}"));
        if (s.armorPenetration > 0) l.Add(("Penetração", $"{s.armorPenetration:0}"));
        if (s.splashRadius > 0) l.Add(("Área", $"{s.splashRadius:0.0}"));
        if (s.targets > 1) l.Add(("Alvos por disparo", $"{s.targets}"));
        if (s.pierce > 0) l.Add(("Perfura", $"+{s.pierce} inimigos"));
        if (s.critChance > 0) l.Add(("Crítico", $"{s.critChance * 100:0}% ×{s.critMultiplier:0.0}"));
        if (s.bossDamageMultiplier > 1) l.Add(("Contra elites/chefes", $"×{s.bossDamageMultiplier:0.0}"));
        if (s.burnDps > 0) l.Add(("Queimadura", $"{s.burnDps:0}/s · {s.burnDuration:0.#}s"));
        if (s.groundFireDps > 0) l.Add(("Chão em chamas", $"{s.groundFireDps:0}/s · {s.groundFireDuration:0.#}s"));
        if (s.bleedDps > 0) l.Add(("Sangramento", $"{s.bleedDps:0}/s · {s.bleedDuration:0.#}s"));
        if (s.slowAmount > 0) l.Add(("Lentidão", $"{s.slowAmount * 100:0}% · {s.slowDuration:0.#}s"));
        if (s.vulnerability > 0) l.Add(("Maldição", $"+{s.vulnerability * 100:0}% dano sofrido"));
        if (s.armorShred > 0) l.Add(("Quebra armadura", $"-{s.armorShred:0}"));
        if (s.curseSpreads) l.Add(("Praga", "espalha ao morrer"));
        if (s.chainCount > 0) l.Add(("Corrente", $"{s.chainCount} saltos"));
        if (s.executeThreshold > 0) l.Add(("Execução", $"< {s.executeThreshold * 100:0}% HP"));
        if (s.stunChance > 0) l.Add(("Atordoar", $"{s.stunChance * 100:0}% · {s.stunDuration:0.#}s"));
        if (s.trapDurability > 0) l.Add(("Durabilidade", $"{s.trapDurability} pisadas"));
        return l;
    }

    static string Diff(TowerData d, TowerStats now, TowerStats nxt)
    {
        var before = StatLines(d, now).ToDictionary(x => x.label, x => x.value);
        var parts = new List<string>();
        foreach (var (label, value) in StatLines(d, nxt))
            if (!before.TryGetValue(label, out var old)) parts.Add($"{label}: {value} (novo)");
            else if (old != value) parts.Add($"{label} {old} → {value}");
        return string.Join("\n", parts.Take(5));
    }
}
