using UnityEngine;

// Click-to-move. The root moves in the world; the rig below it plays Walk/Idle.
public class CommanderMovement : MonoBehaviour
{
    public Vector2 target;
    public bool Moving { get; private set; }
    CommanderController c;
    SpriteRenderer marker;
    float markerLeft;

    public void Init(CommanderController owner)
    {
        c = owner;
        target = transform.position;
        marker = Gfx.Renderer(Battle.I.transform, "MoveMarker", Gfx.Ellipse(20, 8, new Color32(242, 163, 58, 30), new Color32(242, 163, 58, 230)), Vector2.zero, Gfx.OrderOverlay);
        marker.enabled = false;
    }

    public void MoveTo(Vector2 p)
    {
        if (!c.Alive || c.Down) return;
        target = MapController.ClampWalk(p);
        marker.transform.position = target;
        marker.enabled = true;
        markerLeft = 0.6f;
    }

    void Update()
    {
        if (markerLeft > 0 && (markerLeft -= Time.deltaTime) <= 0) marker.enabled = false;
        if (marker.enabled) marker.transform.localScale = Vector3.one * (0.7f + markerLeft * 0.5f);
        var b = Battle.I;
        if (!c.Alive || !b || b.over)
        {
            Moving = false;
            return;
        }
        Vector2 pos = transform.position, to = target - pos;
        Moving = to.sqrMagnitude > 0.0004f;
        if (Moving)
        {
            c.ShowBack(to.y > Mathf.Abs(to.x) * 0.7f);   // heading up the screen: we see his back
            c.view.Face(to.x);
            transform.position = Vector2.MoveTowards(pos, target, c.stats.speed * Time.deltaTime);
        }
        c.view.Moving(Moving);
    }
}
