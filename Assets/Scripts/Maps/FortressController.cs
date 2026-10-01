using UnityEngine;
using UnityEngine.Rendering.Universal;

// The bastion at the end of the road: has HP, shows damage, and decides what an arriving enemy costs.
// A boss only destroys it outright when its BossData says fortressExecution = true.
public class FortressController : MonoBehaviour
{
    public int hp, maxHp;
    public bool Destroyed => hp <= 0;
    public Vector2 Door { get; private set; }

    SpriteRenderer keep, tower;
    Light2D glow;
    float shake, flash;
    Vector3 keepHome;

    public void Init(MapData map, RouteData route, int hitPoints)
    {
        hp = maxHp = Mathf.Max(1, hitPoints);
        Door = route.fortressPosition;
        transform.position = Door;
        if (map.fortressTower)
        {
            tower = Gfx.Renderer(transform, "Watchtower", map.fortressTower, new Vector2(-2.1f, 1.7f), Gfx.OrderWorld);
            Gfx.Shadow(tower.transform, 2.2f);
        }
        if (map.fortressKeep)
        {
            keep = Gfx.Renderer(transform, "Keep", map.fortressKeep, new Vector2(0.15f, 0.25f), Gfx.OrderWorld);
            keepHome = keep.transform.localPosition;
            Gfx.Shadow(keep.transform, 3.2f);
        }
        glow = Gfx.Light(transform, new Vector2(0.1f, 3.4f), new Color(1f, 0.22f, 0.18f), 4.2f, 0.9f);
        glow.gameObject.AddComponent<Flicker>().amount = 0.2f;
        var gate = Gfx.Light(transform, new Vector2(0, 0.6f), new Color(1f, 0.62f, 0.3f), 2.6f, 1.0f);
        gate.gameObject.AddComponent<Flicker>();
    }

    // Damage for one enemy reaching the door. Returns the damage applied.
    public int Breach(EnemyController e, float damageMultiplier = 1)
    {
        if (Destroyed) return 0;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(e.data.damageToFortress * damageMultiplier));
        if (e.data.boss && e.data.boss.fortressExecution) dmg = hp;
        hp = Mathf.Max(0, hp - dmg);
        shake = 0.35f;
        flash = 0.25f;
        Fx.Dust(Door + Vector2.up * 0.4f, 1.5f);
        Fx.Burst(Door + Vector2.up * 1.6f, new Color(0.35f, 0.32f, 0.3f), 10, 2f, 0.6f, -6f);
        CameraRig.Shake(e.data.IsBoss ? 0.35f : 0.08f);
        AudioManager.Play("fortress_hit", Door);
        return dmg;
    }

    void Update()
    {
        if (keep)
        {
            shake = Mathf.Max(0, shake - Time.deltaTime);
            var o = shake > 0 ? new Vector3(Random.Range(-1, 2), Random.Range(-1, 2), 0) / Gfx.PPU : Vector3.zero;
            keep.transform.localPosition = keepHome + o;
            flash = Mathf.Max(0, flash - Time.deltaTime);
            float low = 1 - hp / (float)maxHp;
            keep.color = flash > 0 ? new Color(1f, 0.6f, 0.55f) : Color.Lerp(Color.white, new Color(0.85f, 0.7f, 0.66f), low);
            if (low > 0.5f && Random.value < Time.deltaTime * 6 * low)
                Fx.Burst(Door + new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(1.5f, 4.5f)), new Color(0.25f, 0.22f, 0.22f, 0.8f), 2, 0.3f, 1.4f, 0.8f);
        }
        if (glow) glow.intensity = 0.9f + 0.6f * (1 - hp / (float)Mathf.Max(1, maxHp));
    }
}
