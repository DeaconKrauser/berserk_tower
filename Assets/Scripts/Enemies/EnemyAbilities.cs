using UnityEngine;

// Per-type tricks that make enemies of a map different from each other:
// casters shield/heal allies, ghouls feed on nearby deaths, slime splits when killed.
public class EnemyAbilities : MonoBehaviour
{
    EnemyController owner;
    float castTimer;

    public void Init(EnemyController e)
    {
        owner = e;
        castTimer = e.data.castInterval * Random.Range(0.4f, 0.8f);
        if (e.data.feedHeal > 0 && Battle.I) Battle.I.EnemyKilled += OnSomeoneDied;
    }

    void OnDestroy()
    {
        if (Battle.I) Battle.I.EnemyKilled -= OnSomeoneDied;
    }

    void Update()
    {
        var d = owner.data;
        if (!owner.Alive || d.castInterval <= 0 || !Battle.I || Battle.I.over || owner.status.stunLeft > 0) return;
        if ((castTimer -= Time.deltaTime) > 0) return;
        castTimer = d.castInterval;
        owner.CastFx();
        AudioManager.Play("warlock_cast", transform.position);
        Fx.Ring(owner.Center, d.castRadius, new Color(0.55f, 0.95f, 0.45f), 0.45f);
        foreach (var e in Battle.I.EnemiesIn(transform.position, d.castRadius))
        {
            if (d.castShield > 0) e.Shield(d.castShield * owner.damageMultiplier);
            if (d.castHeal > 0) e.health.Heal(d.castHeal * owner.hpMultiplier);
        }
    }

    void OnSomeoneDied(EnemyController dead)
    {
        if (!owner || !owner.Alive || dead == owner) return;
        if (Vector2.Distance(dead.transform.position, transform.position) > owner.data.feedRadius) return;
        owner.health.Heal(owner.data.feedHeal * owner.hpMultiplier);
        Fx.Burst(owner.Center, new Color(0.65f, 0.08f, 0.08f), 5, 1f, 0.5f, 2f);
    }

    public void OnDeath()
    {
        var d = owner.data;
        if (!d.splitInto || d.splitCount <= 0 || !Battle.I) return;
        for (int i = 0; i < d.splitCount; i++)
        {
            float back = -0.35f * i;
            Battle.I.waves.SpawnChild(d.splitInto, owner.movement.path, owner.movement.progress + back,
                owner.movement.lane + (i % 2 == 0 ? 0.3f : -0.3f));
        }
        Fx.Burst(owner.Center, new Color(0.45f, 0.8f, 0.3f), 14, 2.2f, 0.6f, -6f);
    }
}
