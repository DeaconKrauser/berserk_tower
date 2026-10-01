using UnityEngine;

// Melee against the commander. When he is close and still has a free block slot, the enemy leaves the road,
// walks up to him and fights; enemies keep personal space, so several of them surround him instead of stacking.
// Bosses fight in place on the road. If he walks away (or falls), the enemy goes back to its path.
public class EnemyCombat : MonoBehaviour
{
    const float AggroRange = 1.5f, Leash = 2.6f;

    public bool Engaged { get; private set; }
    EnemyController owner;
    float attackTimer;

    public void Init(EnemyController e) => owner = e;

    void Update()
    {
        if (!owner.Alive || !Battle.I || Battle.I.over) return;
        if (owner.boss && owner.boss.Busy)
        {
            Release();
            return;
        }
        var c = Battle.I.commander;
        var m = owner.movement;
        float r = m.Radius;
        float dist = c && c.Alive ? Vector2.Distance(transform.position, c.transform.position) : float.MaxValue;
        float contact = (r + EnemyMovement.CommanderRadius) * EnemyMovement.Spacing + 0.18f;
        bool canFight = c && c.Alive && owner.status.stunLeft <= 0;

        if (!Engaged)
        {
            float notice = owner.IsBoss ? contact + 0.25f : contact + AggroRange;
            if (!canFight || dist > notice || !c.TryEngage(owner)) return;
            Engaged = true;
            attackTimer = owner.data.attackInterval * 0.5f;
        }
        else if (!canFight || dist > contact + Leash)
        {
            Release();
            return;
        }

        if (owner.IsBoss) m.halted = dist <= contact + 0.3f;   // the boss plants itself and swings
        else
        {
            // stand at the commander's side that faces this enemy
            Vector2 from = (Vector2)transform.position - (Vector2)c.transform.position;
            Vector2 slot = (Vector2)c.transform.position + (from.sqrMagnitude > 1e-4f ? from.normalized : Vector2.left) * (contact - 0.12f);
            m.EnterMelee(slot);
        }
        owner.view.Face(c.transform.position.x - transform.position.x);
        if (dist > contact + 0.25f) return;    // still walking up to him

        if ((attackTimer -= Time.deltaTime) <= 0)
        {
            float enrage = owner.boss ? owner.boss.DamageMultiplier : 1;
            attackTimer = owner.data.attackInterval;
            owner.view.Play("Attack", Mathf.Clamp(1f / Mathf.Max(0.3f, owner.data.attackInterval), 0.8f, 1.6f));
            c.TakeDamage(owner.data.meleeDamage * owner.damageMultiplier * enrage, owner);
            if (!string.IsNullOrEmpty(owner.data.attackSfx)) AudioManager.Play(owner.data.attackSfx, transform.position);
            if (owner.IsBoss) CameraRig.Shake(0.08f);
        }
    }

    void Release()
    {
        if (!Engaged) return;
        Engaged = false;
        var m = owner.movement;
        m.halted = owner.boss && owner.boss.Busy;
        m.LeaveMelee();
        if (Battle.I && Battle.I.commander) Battle.I.commander.Release(owner);
    }

    void OnDestroy()
    {
        if (Engaged && Battle.I && Battle.I.commander) Battle.I.commander.Release(owner);
    }
}
