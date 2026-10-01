using System.Collections.Generic;
using UnityEngine;

// The commander on the battlefield: selectable, click-to-move (CommanderMovement), auto-attacks (CommanderCombat).
// Holds a few enemies in melee; bosses always stop to fight him. When he falls he returns at the fortress after
// CommanderData.respawnSeconds (0 = his death ends the map).
[RequireComponent(typeof(CommanderMovement), typeof(CommanderCombat))]
public class CommanderController : MonoBehaviour
{
    public CommanderData data;
    public CommanderStats stats;
    public CommanderSkinData skin;
    public float hp, maxHp;
    public float damageDealt;
    public bool Alive => hp > 0;
    public bool Down { get; private set; }
    public float RespawnIn { get; private set; }
    public RigView view => backView && showingBack ? backView : frontView;
    RigView frontView, backView;
    bool showingBack;
    public CommanderMovement movement;
    public CommanderCombat combat;
    public readonly List<EnemyController> engaged = new();

    SpriteRenderer ring;
    float hitReact;

    public void Init(CommanderData d, CommanderStats s, CommanderSkinData sk, Vector2 at)
    {
        data = d;
        stats = s;
        skin = sk;
        hp = maxHp = s.maxHp;
        transform.position = at;
        name = "Commander";
        (frontView, backView) = CommanderSkinController.SpawnViews(transform, sk);
        Gfx.Shadow(transform, 1.1f);
        ring = Gfx.Renderer(transform, "Ring", Gfx.Ellipse(52, 18, new Color32(242, 163, 58, 40), new Color32(242, 163, 58, 255)), Vector2.zero, Gfx.OrderOverlay);
        movement = GetComponent<CommanderMovement>();
        combat = GetComponent<CommanderCombat>();
        movement.Init(this);
        combat.Init(this);
        view.Play("Spawn");
    }

    public bool Hit(Vector2 p) =>
        Alive && Mathf.Abs(p.x - transform.position.x) < 0.7f && p.y > transform.position.y - 0.35f && p.y < transform.position.y + 2.3f;

    public void MoveTo(Vector2 p) => movement.MoveTo(p);

    public bool TryEngage(EnemyController e)
    {
        if (engaged.Contains(e)) return true;
        if (!e.IsBoss && engaged.FindAll(x => x && !x.IsBoss).Count >= stats.blockCount) return false;
        engaged.Add(e);
        return true;
    }

    public void Release(EnemyController e) => engaged.Remove(e);

    // Discipline armor removes a flat amount per hit (never more than 75% of the hit).
    public void TakeDamage(float amount, EnemyController from)
    {
        var b = Battle.I;
        if (!Alive || !b || b.over || amount <= 0) return;
        float dealt = Mathf.Max(amount * 0.25f, amount - stats.armor) * combat.DamageTakenMultiplier;
        hp -= dealt;
        view.Flash();
        if (hp <= 0)
        {
            Fall();
            return;
        }
        if (hitReact <= 0)
        {
            hitReact = data.hitReactCooldown;
            if (!movement.Moving) ShowBack(false);
            view.Play("Hit");
            AudioManager.Play("commander_hit", transform.position);
            Fx.Blood((Vector2)transform.position + Vector2.up * 1.1f, 0.6f);
        }
    }

    // Back view while walking up the screen; attacks, hits and death always use the front view.
    public void ShowBack(bool on)
    {
        if (!backView || on == showingBack) return;
        bool left = view.FacingLeft;
        showingBack = on;
        backView.gameObject.SetActive(on);
        frontView.gameObject.SetActive(!on);
        view.Face(left ? -1 : 1);
    }

    void Fall()
    {
        var b = Battle.I;
        ShowBack(false);
        hp = 0;
        Down = true;
        engaged.Clear();
        view.Dead(true);
        view.Moving(false);
        AudioManager.Play("commander_death", transform.position);
        CameraRig.Shake(0.3f);
        b.stats.commanderFalls++;
        if (data.respawnSeconds <= 0)
        {
            b.Defeat($"{data.displayName} caiu. Sem ele, o bastião não resiste.");
            return;
        }
        RespawnIn = data.respawnSeconds;
        b.Banner($"{data.displayName} caiu! Volta em {RespawnIn:0}s", 2.5f);
    }

    void Respawn()
    {
        var b = Battle.I;
        Down = false;
        hp = maxHp * data.respawnHealth;
        var door = b.fortress.Door + new Vector2(-1.4f, -0.6f);
        transform.position = movement.target = MapController.ClampWalk(door);
        view.Dead(false);
        view.Play("Spawn");
        Fx.Ring(transform.position, 1.2f, new Color(1f, 0.75f, 0.35f), 0.5f);
        AudioManager.Play("wave_start", transform.position, 0.5f);
        b.Banner($"{data.displayName} volta à luta", 2f);
    }

    public void HealFull() => hp = maxHp;

    public void HealFraction(float f) => hp = Mathf.Min(maxHp, hp + maxHp * f);

    void Update()
    {
        var b = Battle.I;
        ring.enabled = Alive && b && b.commanderSelected;
        if (!b || b.over) return;
        float dt = Time.deltaTime;
        if (Down)
        {
            if ((RespawnIn -= dt) <= 0) Respawn();
            return;
        }
        hitReact -= dt;
        engaged.RemoveAll(e => !e || !e.Alive);
        hp = Mathf.Min(maxHp, hp + (stats.regen + b.CommanderHealAt(transform.position)) * dt);
    }
}
