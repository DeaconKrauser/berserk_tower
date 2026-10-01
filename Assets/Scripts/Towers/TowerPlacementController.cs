using UnityEngine;

// Free placement, selection, upgrade and sale of buildings. All gold changes for buildings go through here.
// Methods taking `automated` are the only entry points used by the smoke test; they refuse to run
// without -smoketest (Automation.Require), so normal play only builds what the player clicks.
public class TowerPlacementController : MonoBehaviour
{
    static readonly Color32 Ok = new(110, 255, 130, 255), Bad = new(255, 70, 70, 255);

    public TowerData building;          // chosen in the tower bar, ghost follows the cursor
    public TowerController selected;
    public string hint;                 // text next to the cursor
    public bool hintOk;
    public Vector2? previewAt;          // smoke test drives the ghost without a mouse

    Battle battle;
    SpriteRenderer ghostBody, ghostRange, ghostFoot;
    GameObject ghost;

    public void Init(Battle b)
    {
        battle = b;
        ghost = new GameObject("BuildGhost");
        ghost.transform.SetParent(transform, false);
        ghostBody = Gfx.Renderer(ghost.transform, "Body", null, Vector2.zero, Gfx.OrderGhost);
        ghostRange = Gfx.Renderer(ghost.transform, "Range", null, Vector2.zero, Gfx.OrderOverlay);
        ghostFoot = Gfx.Renderer(ghost.transform, "Foot", null, Vector2.zero, Gfx.OrderOverlay + 1);
        ghost.SetActive(false);
    }

    // Called every frame by BattleInput with the snapped world point under the cursor.
    public void UpdateGhost(Vector2 world, bool overUi)
    {
        hint = null;
        if (!building || overUi || battle.over)
        {
            ghost.SetActive(false);
            return;
        }
        bool ok = hintOk = battle.zones.CanBuild(building, world, out var reason);
        ShowGhost(building, world, ok);
        hint = ok ? $"{building.displayName} · {building.cost} de ouro" : reason;
    }

    void ShowGhost(TowerData d, Vector2 at, bool valid)
    {
        ghost.SetActive(true);
        ghost.transform.position = at;
        var c = valid ? Ok : Bad;
        bool trap = d.kind == TowerKind.Trap;
        ghostBody.sprite = d.sprite;
        ghostBody.transform.localPosition = new Vector2(0, trap ? -0.35f : 0);
        ghostBody.color = new Color32(c.r, c.g, c.b, 170);
        float r = d.kind == TowerKind.Aura ? d.stats.auraRadius : d.stats.range;
        ghostRange.sprite = r > 0 ? Gfx.DashedCircle(r, new Color32(c.r, c.g, c.b, 190), new Color32(c.r, c.g, c.b, 18)) : null;
        int w = Mathf.RoundToInt(d.footprintRadius * 2 * Gfx.PPU);
        ghostFoot.sprite = Gfx.Ellipse(w, Mathf.Max(6, trap ? w * 2 / 3 : w / 2), new Color32(c.r, c.g, c.b, 70), c);
    }

    public void ToggleBuild(TowerData t)
    {
        if (battle.over) return;
        building = building == t ? null : t;
        if (building)
        {
            Select(null);
            battle.commanderSelected = false;
            AudioManager.Play("ui_select");
        }
    }

    public void CancelBuild() => building = null;

    public TowerController TryBuild(TowerData t, Vector2 p, bool automated = false)
    {
        if (automated && !Automation.Require("construir " + t.displayName)) return null;
        string reason = null;
        if (battle.over || !battle.zones.CanBuild(t, p, out reason))
        {
            if (!automated && reason != null) battle.Banner(reason, 1.2f);
            AudioManager.Play("ui_error");
            return null;
        }
        battle.Spend(t.cost);
        var tower = new GameObject(t.displayName).AddComponent<TowerController>();
        tower.transform.SetParent(battle.transform, false);
        tower.Init(t, p, battle.waves.WaveNumber);
        battle.towers.Add(tower);
        battle.stats.towersBuilt++;
        if (automated) battle.automatedBuilds++;
        AudioManager.Play("build", p);
        return tower;
    }

    public bool TryUpgrade(TowerController t, bool automated = false)
    {
        if (!t) return false;
        if (automated && !Automation.Require("melhorar " + t.data.displayName)) return false;
        var next = t.NextUpgrade;
        if (battle.over || !next || battle.gold < next.cost)
        {
            if (!automated) AudioManager.Play("ui_error");
            return false;
        }
        battle.Spend(next.cost);
        t.Upgrade();
        battle.stats.upgrades++;
        AudioManager.Play("upgrade", t.Pos);
        return true;
    }

    // Full refund while still in the same preparation phase it was built in (misclick protection), else sellRefund.
    // Traps sell for what is left of them (durability).
    public int SellValue(TowerController t)
    {
        float refund = t.builtAtWave == battle.waves.WaveNumber && !battle.waves.Running ? battle.config.sameWaveRefund : battle.config.sellRefund;
        return Mathf.FloorToInt(t.invested * refund * (t.IsTrap ? t.DurabilityFraction : 1));
    }

    // A trap worn down to nothing: no refund, it just falls apart.
    public void Break(TowerController t)
    {
        if (!t) return;
        battle.towers.Remove(t);
        battle.stats.trapsBroken++;
        if (selected == t) Select(null);
        Fx.Dust(t.Pos, 1.6f);
        Fx.Burst(t.Pos, new Color(0.45f, 0.32f, 0.2f), 12, 2.2f, 0.6f, -6f);
        AudioManager.Play("sell", t.Pos, 0.7f);
        battle.Banner($"{t.data.displayName} se desfez", 1.5f);
        Destroy(t.gameObject);
    }

    public bool Sell(TowerController t, bool automated = false)
    {
        if (!t || battle.over) return false;
        if (automated && !Automation.Require("vender " + t.data.displayName)) return false;
        int value = SellValue(t);
        battle.Earn(value, false);
        battle.towers.Remove(t);
        battle.stats.towersSold++;
        if (selected == t) Select(null);
        Fx.Dust(t.Pos, 1.4f);
        AudioManager.Play("sell", t.Pos);
        Destroy(t.gameObject);
        return true;
    }

    public void Select(TowerController t)
    {
        if (selected) selected.Select(false);
        selected = t;
        if (t)
        {
            building = null;
            battle.commanderSelected = false;
            t.Select(true);
            AudioManager.Play("tower_select", t.Pos);
        }
    }

    public TowerController TowerAt(Vector2 p)
    {
        TowerController best = null;
        foreach (var t in battle.towers)
            if (t.Contains(p) && (!best || t.Pos.y < best.Pos.y)) best = t;
        return best;
    }
}
