using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Mouse and keyboard during a map. Real input is ignored while the smoke test drives the game.
public class BattleInput : MonoBehaviour
{
    Battle b;
    public Vector2 World { get; private set; }
    public bool OverUi { get; private set; }

    public void Init(Battle battle) => b = battle;

    void Update()
    {
        if (!b) return;
        var mouse = Mouse.current;
        var p = b.placement;
        Vector2 world = p.previewAt ?? ScreenToWorld(mouse);
        World = Gfx.Snap(world);
        OverUi = p.previewAt == null && EventSystem.current && EventSystem.current.IsPointerOverGameObject();
        p.UpdateGhost(World, OverUi);

        if (Automation.Enabled || b.over) return;
        Keys();
        if (mouse == null || OverUi || b.paused) return;
        if (mouse.leftButton.wasPressedThisFrame) LeftClick(World);
        else if (mouse.rightButton.wasPressedThisFrame)
        {
            if (p.building) p.CancelBuild();
            else if (b.commander.Alive) b.commander.MoveTo(World);   // right button always moves the commander
        }
    }

    static Vector2 ScreenToWorld(Mouse mouse)
    {
        if (mouse == null || !Camera.main) return Vector2.zero;
        var s = mouse.position.ReadValue();
        return Camera.main.ViewportToWorldPoint(new Vector3(s.x / Screen.width, s.y / Screen.height, 10));
    }

    void Keys()
    {
        var k = Keyboard.current;
        if (k == null) return;
        var p = b.placement;
        if (k.escapeKey.wasPressedThisFrame)
        {
            if (p.building) p.CancelBuild();
            else if (p.selected) p.Select(null);
            else if (b.commanderSelected) b.commanderSelected = false;
            else b.SetPaused(!b.paused);
        }
        if (b.paused) return;
        for (int i = 0; i < b.config.towers.Count && i < 9; i++)
            if (k[Key.Digit1 + i].wasPressedThisFrame) p.ToggleBuild(b.config.towers[i]);
        if (k.spaceKey.wasPressedThisFrame) b.waves.StartWave();
        if (k.uKey.wasPressedThisFrame && p.selected) p.TryUpgrade(p.selected);
        if (k.tKey.wasPressedThisFrame && p.selected) p.selected.CyclePriority();
        if ((k.deleteKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame) && p.selected) p.Sell(p.selected);
        if (k.pKey.wasPressedThisFrame) b.TogglePause();
        if (k.fKey.wasPressedThisFrame) b.CycleSpeed();
        if (k.cKey.wasPressedThisFrame) SelectCommander();
        if (k.qKey.wasPressedThisFrame) b.commander.combat.UseUltimate();
    }

    public void SelectCommander()
    {
        b.placement.Select(null);
        b.placement.CancelBuild();
        b.commanderSelected = b.commander.Alive;
        if (b.commanderSelected) AudioManager.Play("ui_select");
    }

    void LeftClick(Vector2 at)
    {
        var p = b.placement;
        if (p.building)
        {
            bool keep = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            if (p.TryBuild(p.building, at) && !keep) p.CancelBuild();
            return;
        }
        if (b.commander.Hit(at)) { SelectCommander(); return; }
        var t = p.TowerAt(at);
        if (t) { p.Select(t); return; }
        if (b.commanderSelected) { b.commander.MoveTo(at); return; }
        p.Select(null);
    }
}
