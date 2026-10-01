using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Battle HUD: top plates (gold, fortress, wave/group, enemies alive, speed, pause, start wave),
// commander portrait + HP, tower cards, boss bar, banners, cursor tip and the pause menu.
public class HUDController : MonoBehaviour
{
    GameManager gm;
    Battle b;
    UISkin skin;

    Text gold, fortress, waveText, groupText, alive, nextText, banner, tipText, cmdName, bossName;
    UI.Btn startWave, pauseBtn, autoBtn, targetBtn;
    readonly List<UI.Btn> speedBtns = new();
    UI.HpBar cmdBar, bossBar;
    CanvasGroup bossGroup, bannerGroup;
    RectTransform tip, root;
    GameObject pauseOverlay;
    Image portrait;
    readonly List<(TowerData data, Image frame, Text cost, Image icon)> cards = new();
    TowerPanelController towerPanel;
    UI.Btn ultimate;
    Image ultimateFill;
    Text ultimateTime;
    float bannerUntil, bossAlpha;
    int lastGold = -1;
    Text goldDelta;

    public void Build(GameManager g, Battle battle)
    {
        gm = g;
        b = battle;
        skin = g.config.ui;
        root = (RectTransform)transform;
        b.BannerRequested += Banner;

        BuildTop();
        BuildCommander();
        BuildTowerBar();
        BuildBoss();
        var bn = UI.Text(transform, "Banner", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(1500, 80), 44, TextAnchor.MiddleCenter, UI.Bone, true);
        bannerGroup = bn.gameObject.AddComponent<CanvasGroup>();
        banner = bn;
        tip = UI.Rect(transform, "Tip", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(620, 40));
        var tipBg = UI.Image(tip, "Bg", skin ? skin.panelDark : null, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(600, 40));
        tipBg.color = new Color(1, 1, 1, 0.9f);
        tipText = UI.Text(tip, "Text", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(32, 0), new Vector2(580, 40), 20, TextAnchor.MiddleLeft, UI.Bone);
        towerPanel = UI.Rect(transform, "TowerPanel", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, -10), new Vector2(460, 660)).gameObject.AddComponent<TowerPanelController>();
        towerPanel.Build(g, b);
        BuildPause();
    }

    void OnDestroy()
    {
        if (b) b.BannerRequested -= Banner;
    }

    // ---------------------------------------------------------------- layout

    void Plate(string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Sprite icon, out Text value, int fontSize = 30)
    {
        var p = UI.Panel(transform, name, anchor, pivot, pos, size, skin ? skin.panelDark : null, false);
        if (icon) UI.Icon(p.transform, "Icon", icon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), size.y - 14);
        value = UI.Text(p.transform, "Value", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(icon ? size.y + 4 : 16, 1), new Vector2(size.x - size.y - 16, size.y), fontSize, TextAnchor.MiddleLeft, UI.Bone, true);
    }

    void BuildTop()
    {
        var tl = new Vector2(0, 1);
        Plate("Gold", tl, tl, new Vector2(16, -14), new Vector2(230, 70), skin ? skin.gold : null, out gold);
        gold.color = UI.Ember;
        goldDelta = UI.Text(transform, "GoldDelta", tl, tl, new Vector2(170, -84), new Vector2(120, 30), 20, TextAnchor.MiddleLeft, UI.Good);
        Plate("Fortress", tl, tl, new Vector2(256, -14), new Vector2(230, 70), skin ? skin.heart : null, out fortress);

        var top = new Vector2(0.5f, 1);
        var wave = UI.Panel(transform, "Wave", top, top, new Vector2(-70, -14), new Vector2(560, 84), skin ? skin.panelDark : null, false).transform;
        UI.Icon(wave, "Icon", skin ? skin.wave : null, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), 64);
        waveText = UI.Text(wave, "Wave", new Vector2(0, 1), new Vector2(0, 1), new Vector2(84, -6), new Vector2(240, 40), 30, TextAnchor.MiddleLeft, UI.Bone, true);
        groupText = UI.Text(wave, "Group", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -6), new Vector2(220, 40), 24, TextAnchor.MiddleRight, UI.Gold, true);
        nextText = UI.Text(wave, "Next", new Vector2(0, 0), new Vector2(0, 0), new Vector2(84, 4), new Vector2(460, 34), 17, TextAnchor.MiddleLeft, UI.Dim);
        Plate("Alive", top, top, new Vector2(344, -14), new Vector2(240, 70), skin ? skin.skull : null, out alive, 24);

        var tr = new Vector2(1, 1);
        for (int i = 0; i < b.config.gameSpeeds.Length; i++)
        {
            int idx = i;
            var btn = UI.RoundButton(transform, "Speed" + i, tr, tr, new Vector2(-214 + i * 66 - 66 * (b.config.gameSpeeds.Length - 3), -16), 62, $"{b.config.gameSpeeds[i]:0}×", () => b.SetSpeedIndex(idx));
            speedBtns.Add(btn);
        }
        pauseBtn = UI.RoundButton(transform, "Pause", tr, tr, new Vector2(-16, -16), 62, "II", b.TogglePause);
        startWave = UI.Button(transform, "StartWave", tr, tr, new Vector2(-16, -92), new Vector2(330, 64), "", () => b.waves.StartWave(), 22, true, "wave_start");
        autoBtn = UI.Button(transform, "AutoWaves", tr, tr, new Vector2(-16, -162), new Vector2(330, 44), "", () =>
        {
            b.waves.AutoMode = !b.waves.AutoMode;
            gm.save.Data.settings.autoWaves = b.waves.AutoMode;
            gm.save.Save();
        }, 18, true);
        targetBtn = UI.Button(transform, "TargetAll", tr, tr, new Vector2(-16, -212), new Vector2(330, 44), "", () =>
        {
            b.CycleTargetAll();
            gm.save.Data.settings.towerTargeting = b.targetAll.HasValue ? (int)b.targetAll.Value : -1;
            gm.save.Save();
        }, 18, true);
    }

    void BuildCommander()
    {
        var bl = new Vector2(0, 0);
        var p = UI.Panel(transform, "Commander", bl, bl, new Vector2(16, 16), new Vector2(560, 150)).transform;
        var btn = p.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => { UI.Deselect(); b.input.SelectCommander(); });
        UI.Image(p, "PortraitFrame", skin ? skin.panelThin : null, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(118, 118));
        var sk = b.commander.skin;
        portrait = UI.Image(p, "Portrait", sk ? sk.portrait : null, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(23, 0), new Vector2(100, 100));
        portrait.preserveAspect = true;
        cmdName = UI.Text(p, "Name", new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -14), new Vector2(280, 34), 24, TextAnchor.MiddleLeft, UI.Ember, true);
        var cd = b.commander.data;
        cmdName.text = (sk && !sk.unlockedByDefault ? sk.displayName : cd.displayName).ToUpperInvariant();
        cmdName.horizontalOverflow = HorizontalWrapMode.Wrap;
        cmdName.verticalOverflow = VerticalWrapMode.Truncate;
        cmdName.resizeTextForBestFit = true;
        cmdName.resizeTextMinSize = 14;
        cmdName.resizeTextMaxSize = 24;
        cmdBar = UI.Bar(p, "Hp", new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -54), new Vector2(276, 34), Gfx.CommanderHp, 18);
        var hint = UI.Text(p, "Hint", new Vector2(0, 0), new Vector2(0, 0), new Vector2(146, 12), new Vector2(290, 46), 15, TextAnchor.LowerLeft, UI.Dim);
        hint.text = "[C] seleciona · clique no chão move\nbotão direito sempre move";

        // the power: round button next to the portrait panel, filling up while it recharges
        ultimate = UI.RoundButton(p, "Ultimate", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 10), 92, "", () => b.commander.combat.UseUltimate());
        var icon = UI.Icon(ultimate.rect, "Icon", skin ? skin.fire : null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 56);
        ultimateFill = UI.Image(ultimate.rect, "Charge", Gfx.Dot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(68, 68), new Color(0.05f, 0.03f, 0.05f, 0.7f));
        ultimateFill.type = Image.Type.Filled;
        ultimateFill.fillMethod = Image.FillMethod.Vertical;
        ultimateFill.fillOrigin = (int)Image.OriginVertical.Top;
        ultimateTime = UI.Text(ultimate.rect, "Time", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 40), 26, TextAnchor.MiddleCenter, UI.Bone, true);
        UI.Text(ultimate.rect, "Key", new Vector2(1, 1), new Vector2(1, 1), new Vector2(4, 6), new Vector2(30, 26), 18, TextAnchor.UpperRight, UI.Dim, true).text = "Q";
        var powerName = UI.Text(p, "UltimateName", new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(-62, 6), new Vector2(118, 24), 15, TextAnchor.MiddleCenter, UI.Gold);
        powerName.text = b.commander.combat.PowerName;
        powerName.resizeTextForBestFit = true;
        powerName.verticalOverflow = VerticalWrapMode.Truncate;
        powerName.resizeTextMinSize = 10;
        powerName.resizeTextMaxSize = 15;
        ultimate.button.gameObject.AddComponent<UltimateTooltip>().Init(this, b.commander.combat);
    }

    void BuildTowerBar()
    {
        var towers = b.config.towers;
        const float w = 134, gap = 8;
        float total = towers.Count * w + (towers.Count + 1) * gap;
        var bar = UI.Panel(transform, "TowerBar", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(124, 10), new Vector2(total, 178)).transform;
        for (int i = 0; i < towers.Count; i++)
        {
            var t = towers[i];
            var frame = UI.Image(bar, t.id, skin ? skin.card : null, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(gap + i * (w + gap), 0), new Vector2(w, 160), null, true);
            var button = frame.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => { UI.Deselect(); b.placement.ToggleBuild(t); });
            frame.gameObject.AddComponent<HoverSound>();
            frame.gameObject.AddComponent<TowerCardTooltip>().Init(this, t);
            var icon = UI.Image(frame.transform, "Icon", t.icon ? t.icon : t.sprite, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(84, 84));
            icon.preserveAspect = true;
            UI.Text(frame.transform, "Key", new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -6), new Vector2(30, 28), 20, TextAnchor.UpperLeft, UI.Dim, true).text = (i + 1).ToString();
            var name = UI.Text(frame.transform, "Name", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(w - 8, 40), 15, TextAnchor.MiddleCenter, UI.Bone);
            name.text = t.displayName;
            UI.Icon(frame.transform, "GoldIcon", skin ? skin.gold : null, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-14, 8), 24);
            var cost = UI.Text(frame.transform, "Cost", new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(-12, 6), new Vector2(70, 28), 21, TextAnchor.MiddleLeft, UI.Ember, true);
            cost.text = t.cost.ToString();
            cards.Add((t, frame, cost, icon));
        }
    }

    void BuildBoss()
    {
        var top = new Vector2(0.5f, 1);
        var p = UI.Panel(transform, "Boss", top, top, new Vector2(-70, -104), new Vector2(760, 92), skin ? skin.panelDark : null, false);
        bossGroup = p.gameObject.AddComponent<CanvasGroup>();
        bossGroup.alpha = 0;
        UI.Icon(p.transform, "Icon", skin ? skin.boss : null, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), 72);
        bossName = UI.Text(p.transform, "Name", new Vector2(0, 1), new Vector2(0, 1), new Vector2(92, -6), new Vector2(640, 34), 24, TextAnchor.MiddleLeft, UI.Bone, true);
        bossBar = UI.Bar(p.transform, "Bar", new Vector2(0, 0), new Vector2(0, 0), new Vector2(92, 10), new Vector2(650, 36), new Color32(150, 40, 170, 255), 18);
    }

    void BuildPause()
    {
        var o = UI.Fill(transform, "Pause", new Color(0.02f, 0.02f, 0.03f, 0.6f), true);
        pauseOverlay = o.gameObject;
        var c = new Vector2(0.5f, 0.5f);
        var p = UI.Panel(o.transform, "Panel", c, c, Vector2.zero, new Vector2(520, 420)).transform;
        UI.Title(p, "PAUSADO", new Vector2(0.5f, 1), new Vector2(0, -60), 42, 480);
        UI.Button(p, "Resume", c, c, new Vector2(0, 30), new Vector2(340, 62), "RETOMAR", () => b.SetPaused(false), 24, true);
        UI.Button(p, "Options", c, c, new Vector2(0, -46), new Vector2(340, 62), "SOM: " + (gm.save.Data.settings.music > 0 ? "LIGADO" : "MUDO"), null, 22, true);
        var mute = p.Find("Options").GetComponent<Button>();
        mute.onClick.AddListener(() =>
        {
            var s = gm.save.Data.settings;
            s.master = s.master > 0 ? 0 : 0.9f;
            gm.ApplySettings();
            gm.save.Save();
            mute.GetComponentInChildren<Text>().text = s.master > 0 ? "SOM: LIGADO" : "SOM: MUDO";
        });
        UI.Button(p, "Abandon", c, c, new Vector2(0, -122), new Vector2(340, 62), "ABANDONAR A RUN", gm.flow.AbandonRun, 22, true);
        pauseOverlay.SetActive(false);
    }

    // ---------------------------------------------------------------- per frame

    void Update()
    {
        if (!b) return;
        float dt = Time.unscaledDeltaTime;
        var w = b.waves;

        if (lastGold >= 0 && b.gold != lastGold)
        {
            int d = b.gold - lastGold;
            goldDelta.text = d > 0 ? $"+{d}" : d.ToString();
            goldDelta.color = d > 0 ? UI.Good : UI.Bad;
            goldDelta.canvasRenderer.SetAlpha(1);
            goldDelta.CrossFadeAlpha(0, 1.2f, true);
        }
        lastGold = b.gold;
        gold.text = b.gold.ToString();
        fortress.text = $"{b.fortress.hp}/{b.fortress.maxHp}";
        fortress.color = b.fortress.hp <= b.fortress.maxHp * 0.3f ? Color.Lerp(UI.Bone, UI.Bad, Mathf.PingPong(Time.unscaledTime * 3, 1)) : UI.Bone;
        waveText.text = $"Onda {Mathf.Max(w.WaveNumber, 0)}/{w.TotalWaves}";
        groupText.text = w.Running ? $"Grupo {w.Group}/{w.GroupCount}" : "preparação";
        alive.text = $"{b.enemies.Count} vivos";
        nextText.text = w.Running
            ? (w.Spawning && w.NextGroupIn > 0.2f ? $"Próximo grupo em {w.NextGroupIn:0}s" : w.Spawning ? "Inimigos chegando..." : "Últimos inimigos em campo")
            : w.Next ? "Próxima: " + Composition(w.Next, b.difficulty.tier) : "Última onda.";
        startWave.button.interactable = !w.Running && !b.over && w.Next;
        startWave.label.text = w.Running ? "ONDA EM ANDAMENTO" : !w.Next ? "FIM"
            : w.AutoStartIn >= 0 ? $"ONDA {w.WaveNumber + 1} EM {Mathf.CeilToInt(w.AutoStartIn)}s  [ESPAÇO]"
            : $"INICIAR ONDA {w.WaveNumber + 1}  [ESPAÇO]";
        autoBtn.label.text = w.AutoMode ? "ONDAS: <color=#F2A33A>AUTOMÁTICAS</color>" : "ONDAS: MANUAIS";
        targetBtn.label.text = b.targetAll.HasValue ? $"TORRES MIRAM: <color=#F2A33A>{TargetPriorityNames.Pt(b.targetAll.Value).ToUpperInvariant()}</color>" : "TORRES MIRAM: PADRÃO";
        for (int i = 0; i < speedBtns.Count; i++)
            speedBtns[i].image.sprite = skin ? (b.speedIndex == i ? skin.roundActive : skin.roundNormal) : null;
        pauseBtn.label.text = b.paused ? "▶" : "II";
        pauseOverlay.SetActive(b.paused && !b.over && gm.flow.Current == SceneFlow.Screen.Battle);

        var c = b.commander;
        cmdBar.Set(c.hp / c.maxHp);
        cmdBar.Tick(dt);
        cmdBar.text.text = c.Down ? $"CAÍDO · volta em {Mathf.CeilToInt(c.RespawnIn)}s" : $"{Mathf.CeilToInt(c.hp)} / {c.maxHp:0}";
        cmdBar.fill.color = c.hp < c.maxHp * 0.3f ? Color.Lerp(Gfx.CommanderHp, UI.Bad, Mathf.PingPong(Time.unscaledTime * 3, 1)) : Gfx.CommanderHp;
        portrait.color = c.Alive ? Color.white : new Color(0.4f, 0.3f, 0.3f);
        var cc = c.combat;
        ultimateFill.fillAmount = 1 - cc.UltimateCharge;
        ultimateTime.text = cc.UltimateReady ? "" : c.Down ? "—" : Mathf.CeilToInt(cc.UltimateCooldownLeft).ToString();
        ultimate.button.interactable = cc.UltimateReady && !b.over;
        ultimate.image.sprite = skin ? (cc.FuryLeft > 0 ? skin.roundActive : cc.UltimateReady ? skin.roundHover : skin.roundNormal) : null;

        foreach (var (data, frame, cost, icon) in cards)
        {
            bool afford = b.gold >= data.cost && !b.over;
            frame.sprite = skin ? (b.placement.building == data ? skin.cardSelected : afford ? skin.card : skin.cardDisabled) : null;
            icon.color = afford ? Color.white : new Color(0.45f, 0.42f, 0.45f, 0.9f);
            cost.color = afford ? UI.Ember : UI.Bad;
        }

        var boss = b.boss;
        bossAlpha = Mathf.MoveTowards(bossAlpha, boss ? 1 : 0, dt * 1.6f);
        bossGroup.alpha = bossAlpha;
        bossGroup.transform.localScale = new Vector3(1, Mathf.Lerp(0.6f, 1, bossAlpha), 1);
        if (boss)
        {
            bossName.text = boss.boss.Title + (boss.boss.Enraged ? "  <color=#FF6054>· ENFURECIDO</color>" : "");
            bossBar.Set(boss.Hp / boss.MaxHp);
            bossBar.text.text = $"{Mathf.CeilToInt(boss.Hp)} / {boss.MaxHp:0}";
        }
        bossBar.Tick(dt);

        bannerGroup.alpha = Mathf.Clamp01((bannerUntil - Time.unscaledTime) / 0.6f);
        towerPanel.Sync();

        var p = b.placement;
        string tipStr = p.hint ?? hoverTip;
        bool showTip = tipStr != null && Mouse.current != null;
        tip.gameObject.SetActive(showTip);
        if (showTip)
        {
            Vector2 at = p.previewAt.HasValue && Camera.main ? (Vector2)Camera.main.WorldToScreenPoint(p.previewAt.Value) : Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, at, null, out var local);
            tip.anchoredPosition = local + root.rect.size / 2 + new Vector2(8, 26);
            tipText.text = tipStr;
            tipText.color = p.hint != null ? (p.hintOk ? UI.Bone : UI.Bad) : UI.Bone;
        }
    }

    string hoverTip;
    public void SetHoverTip(string s) => hoverTip = s;

    public void Banner(string text, float seconds)
    {
        banner.text = text;
        bannerUntil = Time.unscaledTime + seconds;
    }

    public static string Composition(WaveData w, int tier) => string.Join("  ·  ",
        w.groups.Where(g => g.minRunTier <= tier).SelectMany(g => g.entries).Where(e => e.enemy).GroupBy(e => e.enemy)
            .Select(x => $"{x.Sum(s => s.count)}× {x.Key.displayName}"));
}

public class UltimateTooltip : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    HUDController hud;
    CommanderCombat c;

    public void Init(HUDController h, CommanderCombat combat)
    {
        hud = h;
        c = combat;
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) =>
        hud.SetHoverTip($"[Q] {c.PowerName}: {c.PowerDescription} Recarga {c.PowerCooldown:0}s.");

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => hud.SetHoverTip(null);
}

// Hovering a tower card shows its role and cost next to the cursor.
public class TowerCardTooltip : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    HUDController hud;
    TowerData t;

    public void Init(HUDController h, TowerData data)
    {
        hud = h;
        t = data;
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) => hud.SetHoverTip($"{t.displayName} — {t.role}  ({t.goodAgainst})");

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => hud.SetHoverTip(null);
}
