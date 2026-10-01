using UnityEngine;

// Crowd movement. On the road an enemy follows its path with its own lateral lane and keeps personal space:
// bodies never overlap (spacing = summed radii × Spacing). Someone slower or stopped in front makes it side-step
// into free room; if it stays blocked it briefly detours around the obstacle (a fight, a boss) and then returns
// to the road. When it engages the commander it leaves the lane and walks up to him (melee mode), so the ones
// behind can also come close and surround him instead of forming a conga line. After the fight it rejoins its path.
public class EnemyMovement : MonoBehaviour
{
    public const float Spacing = 1.2f;
    const float SideStepSpeed = 1.7f, DetourAfter = 0.35f, DetourLane = 0.6f, ScanFactor = 1.8f, BackOff = 0.6f;

    public enum Mode { Path, Melee, Rejoin }

    public int path;
    public float progress, lane;
    public bool halted;          // casting, making an entrance, attacking in place
    public float speedMultiplier = 1;
    public Mode mode { get; private set; }
    public Vector2 meleeTarget;

    EnemyController owner;
    RouteController route;
    float maxLane, blockedFor, detour;
    Vector2 lastPos;

    public float Radius => owner.data.bodyRadius * owner.view.transform.localScale.x;
    public float Length => (owner.data.bodyLength > 0 ? owner.data.bodyLength : owner.data.bodyRadius) * owner.view.transform.localScale.x;
    public float Speed => owner.data.movementSpeed * speedMultiplier * owner.status.SlowFactor * (owner.status.stunLeft > 0 ? 0 : 1);
    public float Remaining => route.Length(path) - progress;

    public void Init(EnemyController e, int p, float s, float l)
    {
        owner = e;
        route = Battle.I.map.route;
        path = Mathf.Clamp(p, 0, route.PathCount - 1);
        progress = Mathf.Clamp(s, 0, route.Length(path) - 0.5f);
        maxLane = Mathf.Max(0, route.halfWidth - Radius * 0.6f - 0.04f);   // feet stay on the road
        lane = Mathf.Clamp(l, -maxLane, maxLane);
        transform.position = lastPos = route.Position(path, progress, lane);
    }

    void Update()
    {
        if (!owner.Alive || !Battle.I || Battle.I.over) return;
        float dt = Time.deltaTime;
        bool moving = !halted && Speed > 0.01f;
        if (moving)
        {
            switch (mode)
            {
                case Mode.Path: WalkPath(dt); break;
                case Mode.Melee: MoveFree(meleeTarget, dt, 0.02f); break;
                case Mode.Rejoin:
                    var back = route.Position(path, progress, Mathf.Clamp(lane, -maxLane, maxLane));
                    if (MoveFree(back, dt, 0.06f))
                    {
                        lane = Mathf.Clamp(lane, -maxLane, maxLane);
                        mode = Mode.Path;
                    }
                    break;
            }
        }
        Vector2 pos = transform.position;
        float dx = pos.x - lastPos.x;
        bool actuallyMoving = (pos - lastPos).sqrMagnitude > 1e-7f;
        lastPos = pos;
        if (!halted && mode != Mode.Melee) owner.view.Face(dx);
        owner.view.Moving(moving && actuallyMoving, Mathf.Clamp(Speed / Mathf.Max(0.1f, owner.data.movementSpeed), 0.4f, 1.8f));
        if (mode == Mode.Path && progress >= route.Length(path) - 0.02f) owner.Leak();
    }

    // ---------------------------------------------------------------- road

    void WalkPath(float dt)
    {
        float r = Radius, len = Length;
        float speed = Speed;
        float next = progress + speed * dt;
        Vector2 me = transform.position;
        Vector2 dir = route.Direction(path, progress);
        Vector2 left = new(-dir.y, dir.x);
        bool blocked = false;
        float steer = 0;

        // `first`: the other body has priority (it is further along). Only the one behind yields, so two bodies that
        // touch never block each other; an overlapping follower backs off a little to give the leader its space.
        // Bodies are ellipses aligned with the march: `radius` across the road, `length` along it.
        void Obstacle(Vector2 at, float radius, float length, bool first, int otherId)
        {
            float gapSide = (r + radius) * Spacing, gapAlong = (len + length) * Spacing;
            Vector2 d = at - me;
            float ahead = Vector2.Dot(d, dir), side = Vector2.Dot(d, left);
            if (Mathf.Abs(ahead) > gapAlong * ScanFactor || Mathf.Abs(side) > gapSide * ScanFactor) return;
            float away = Mathf.Abs(side) > 0.02f ? -Mathf.Sign(side) : (owner.id < otherId ? 1 : -1);
            if (first && ahead > -gapAlong * 0.5f && Mathf.Abs(side) < gapSide * 0.95f)
            {
                float free = ahead - gapAlong;
                next = Mathf.Min(next, progress + Mathf.Max(free, -BackOff * dt));
                blocked |= free < gapAlong * 0.3f;
                steer += away;
            }
            else if (Mathf.Abs(side) < gapSide && Mathf.Abs(ahead) < gapAlong)
                steer += away * (1 - Mathf.Abs(side) / gapSide) * 2;   // shoulder to shoulder: drift apart
        }

        foreach (var o in Battle.I.enemies)
        {
            if (o == owner || !o.Alive) continue;
            var om = o.movement;
            bool first = om.mode == Mode.Path && om.path == path
                ? om.progress > progress + 0.01f || (Mathf.Abs(om.progress - progress) <= 0.01f && o.id < owner.id)
                : Vector2.Dot((Vector2)o.transform.position - me, dir) > 0.05f;
            Obstacle(o.transform.position, om.Radius, om.Length, first, o.id);
        }
        var c = Battle.I.commander;
        if (c && c.Alive) Obstacle(c.transform.position, CommanderRadius, CommanderRadius, Vector2.Dot((Vector2)c.transform.position - me, dir) > 0.05f, -1);

        blockedFor = blocked ? blockedFor + dt : Mathf.Max(0, blockedFor - dt * 2);
        detour = Mathf.MoveTowards(detour, blockedFor > DetourAfter ? DetourLane : 0, dt * 1.6f);
        float limit = maxLane + detour;
        lane += Mathf.Clamp(steer, -1, 1) * SideStepSpeed * dt;
        lane = Mathf.Abs(lane) > limit ? Mathf.MoveTowards(lane, Mathf.Clamp(lane, -limit, limit), SideStepSpeed * dt) : lane;
        progress = Mathf.Max(0, next);
        transform.position = route.Position(path, progress, lane);
    }

    public const float CommanderRadius = 0.36f;

    // ---------------------------------------------------------------- free movement (melee, rejoin)

    // Walks towards a point keeping personal space. Returns true when it got there.
    bool MoveFree(Vector2 target, float dt, float arrive)
    {
        Vector2 me = transform.position;
        Vector2 to = target - me;
        float r = (Radius + Length) * 0.5f;
        Vector2 step = to.sqrMagnitude > arrive * arrive ? Vector2.ClampMagnitude(to, Speed * dt) : Vector2.zero;
        Vector2 push = Vector2.zero;
        foreach (var o in Battle.I.enemies)
        {
            if (o == owner || !o.Alive) continue;
            Vector2 d = me - (Vector2)o.transform.position;
            float gap = (r + (o.movement.Radius + o.movement.Length) * 0.5f) * Spacing, dist = d.magnitude;
            if (dist < gap) push += (dist > 0.001f ? d / dist : new Vector2(owner.id % 2 == 0 ? 1 : -1, 0)) * (gap - dist);
        }
        var c = Battle.I.commander;
        if (c && c.Alive)
        {
            Vector2 d = me - (Vector2)c.transform.position;
            float gap = (r + CommanderRadius) * Spacing, dist = d.magnitude;
            if (dist < gap) push += (dist > 0.001f ? d / dist : Vector2.right) * (gap - dist);
        }
        var next = MapController.ClampWalk(me + step + Vector2.ClampMagnitude(push, Speed * dt * 1.5f + 0.01f));
        transform.position = next;
        return (target - next).sqrMagnitude <= arrive * arrive * 4;
    }

    public void EnterMelee(Vector2 target)
    {
        mode = Mode.Melee;
        meleeTarget = target;
    }

    // Back to the road: progress/lane from the nearest point of its own path ahead of where it left it.
    public void LeaveMelee()
    {
        if (mode != Mode.Melee) return;
        var (s, l) = route.Project(path, transform.position, progress);
        progress = Mathf.Max(progress, s);
        lane = l;
        mode = Mode.Rejoin;
    }

    // Where the enemy will stand after `seconds` (lobbed projectiles aim there).
    public Vector2 Predict(float seconds) =>
        halted || mode != Mode.Path ? (Vector2)transform.position : route.Position(path, progress + Speed * seconds, lane);
}
