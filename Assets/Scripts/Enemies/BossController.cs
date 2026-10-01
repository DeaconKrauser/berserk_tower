using UnityEngine;

// Boss layer on top of an enemy: entrance (stands, roars, camera emphasis, music swap), summons,
// a telegraphed special attack, and an enraged phase. Never blocks gameplay: towers keep firing throughout.
public class BossController : MonoBehaviour
{
    EnemyController owner;
    BossData b;
    float introLeft, summonTimer, specialTimer, specialWindup;
    int extraSummons;
    bool enraged, announcedSummon;
    Vector2 specialAt;
    SpriteRenderer telegraph;

    public bool Busy => introLeft > 0 || specialWindup > 0;
    public bool Enraged => enraged;
    public float DamageMultiplier => enraged ? b.enrageDamageMultiplier : 1;
    public string Title => string.IsNullOrEmpty(b.title) ? owner.data.displayName : b.title;

    public void Init(EnemyController e, RunDifficulty diff)
    {
        owner = e;
        b = e.data.boss;
        extraSummons = diff.bossExtraSummons;
        summonTimer = b.summonInterval;
        specialTimer = b.specialInterval * 0.7f;
        introLeft = b.introSeconds;
        owner.movement.halted = true;
        owner.view.Play("Intro");
        CameraRig.Emphasize(transform.position, b.introSeconds);
        CameraRig.Shake(b.introCameraShake);
        AudioManager.Play("boss_roar", transform.position);
        MusicManager.Boss(true);
        Battle.I.BossEntered(owner);
    }

    void Update()
    {
        if (!owner.Alive || !Battle.I || Battle.I.over) return;
        float dt = Time.deltaTime;
        if (introLeft > 0)
        {
            if ((introLeft -= dt) <= 0) owner.movement.halted = false;
            if (Random.value < dt * 8) Fx.Dust((Vector2)transform.position + Random.insideUnitCircle * 0.8f, 0.4f);
            return;
        }
        if (specialWindup > 0)
        {
            specialWindup -= dt;
            if (telegraph) telegraph.color = new Color(1f, 0.2f, 0.2f, 0.25f + 0.35f * Mathf.PingPong(Time.time * 6, 1));
            if (specialWindup <= 0) Slam();
            return;
        }

        if (b.summon && b.summonCount > 0 && (summonTimer -= dt) <= 0)
        {
            summonTimer = b.summonInterval;
            int n = b.summonCount + extraSummons;
            for (int i = 0; i < n; i++)
                Battle.I.waves.SpawnChild(b.summon, owner.movement.path, owner.movement.progress + 1.4f + i * 0.7f, Random.Range(-0.5f, 0.5f));
            owner.view.Play("Cast");
            AudioManager.Play("boss_summon", transform.position);
            Fx.Ring(owner.Center, 1.6f, new Color(0.6f, 0.25f, 0.9f), 0.5f);
            if (!announcedSummon) Battle.I.Banner($"{Title} ergue os mortos!");
            announcedSummon = true;
            Battle.I.summoned += n;
        }

        if (b.specialInterval > 0 && (specialTimer -= dt) <= 0) StartSpecial();

        if (!enraged && b.enrageBelowHp > 0 && owner.Hp <= owner.MaxHp * b.enrageBelowHp)
        {
            enraged = true;
            owner.movement.speedMultiplier = b.enrageSpeedMultiplier;
            owner.view.SetTint(new Color(1f, 0.72f, 0.72f));
            CameraRig.Shake(0.2f);
            AudioManager.Play("boss_roar", transform.position);
            Battle.I.Banner($"{Title} enfurece!");
        }
    }

    // Telegraphed: a red circle grows under the boss for a moment, then the slam lands.
    void StartSpecial()
    {
        specialTimer = b.specialInterval;
        specialWindup = 1.0f;
        specialAt = transform.position;
        owner.movement.halted = true;
        owner.view.Play("Special");
        var go = new GameObject("Telegraph");
        go.transform.SetParent(Battle.I.transform, false);
        go.transform.position = specialAt;
        telegraph = go.AddComponent<SpriteRenderer>();
        telegraph.sprite = Gfx.Circle(b.specialRadius, new Color32(255, 40, 40, 50), new Color32(255, 60, 60, 220));
        telegraph.sortingOrder = Gfx.OrderOverlay + 2;
        go.transform.localScale = new Vector3(1, 0.6f, 1);
        Battle.I.Banner(string.IsNullOrEmpty(b.specialName) ? "Golpe do chefe!" : b.specialName + "!", 1.5f);
    }

    void Slam()
    {
        owner.movement.halted = false;
        if (telegraph) Destroy(telegraph.gameObject);
        CameraRig.Shake(b.specialCameraShake);
        AudioManager.Play("boss_attack", specialAt);
        Fx.Ring(specialAt, b.specialRadius, new Color(1f, 0.3f, 0.25f), 0.45f);
        Fx.Dust(specialAt, 3f);
        var c = Battle.I.commander;
        if (c && c.Alive && Vector2.Distance(c.transform.position, specialAt) <= b.specialRadius)
            c.TakeDamage(b.specialDamage * owner.damageMultiplier * DamageMultiplier, owner);
        if (b.specialStunSeconds > 0)
            foreach (var t in Battle.I.towers)
                if (Vector2.Distance(t.Pos, specialAt) <= b.specialRadius + t.data.footprintRadius) t.Stun(b.specialStunSeconds);
    }

    void OnDestroy()
    {
        if (telegraph) Destroy(telegraph.gameObject);
    }
}
