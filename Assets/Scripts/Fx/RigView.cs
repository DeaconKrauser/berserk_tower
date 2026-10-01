using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Visual side of a character: an instance of its cutout-rig prefab (built by RigBuilder) under the gameplay object.
// The gameplay object moves in the world; the rig animates below it. Nothing here feeds gameplay.
public class RigView : MonoBehaviour
{
    static readonly int MovingId = Animator.StringToHash("Moving"), DeadId = Animator.StringToHash("Dead"),
        MoveSpeedId = Animator.StringToHash("MoveSpeed"), ActionSpeedId = Animator.StringToHash("ActionSpeed");

    public Animator animator;
    public SortingGroup group;
    Transform body;
    readonly List<SpriteRenderer> renderers = new();
    readonly List<Color> baseColors = new();
    readonly HashSet<int> triggers = new();
    Color tint = Color.white;
    float flash, alpha = 1;
    bool facingLeft;

    public static RigView Create(Transform parent, GameObject prefab, float scale = 1, string fallbackName = "Rig")
    {
        GameObject go;
        if (prefab) go = Instantiate(prefab, parent, false);
        else
        {
            go = new GameObject(fallbackName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Gfx.Ellipse(20, 40, new Color32(90, 30, 40, 255), new Color32(11, 10, 13, 255));
        }
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one * scale;
        var v = go.AddComponent<RigView>();
        v.animator = go.GetComponent<Animator>();
        v.group = go.GetComponent<SortingGroup>();
        if (!v.group) v.group = go.AddComponent<SortingGroup>();
        var flip = go.transform.Find("Flip");
        v.body = flip ? flip : go.transform;
        go.GetComponentsInChildren(true, v.renderers);
        foreach (var r in v.renderers) v.baseColors.Add(r.color);
        if (v.animator)
        {
            foreach (var p in v.animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger) v.triggers.Add(p.nameHash);
            v.animator.SetFloat(MoveSpeedId, 1);
            v.animator.SetFloat(ActionSpeedId, 1);
        }
        return v;
    }

    public bool Has(string trigger) => triggers.Contains(Animator.StringToHash(trigger));

    public void Moving(bool on, float speedScale = 1)
    {
        if (!animator) return;
        animator.SetBool(MovingId, on);
        animator.SetFloat(MoveSpeedId, speedScale);
    }

    public void Play(string trigger, float speed = 1)
    {
        if (!animator) return;
        int h = Animator.StringToHash(trigger);
        if (!triggers.Contains(h)) return;
        animator.SetFloat(ActionSpeedId, speed);
        animator.SetTrigger(h);
    }

    public void Dead(bool on)
    {
        if (animator) animator.SetBool(DeadId, on);
    }

    // Rigs are drawn facing right; facing left mirrors the Flip node (never animated, pivot at the feet).
    public void Face(float dx)
    {
        if (Mathf.Abs(dx) < 0.0005f) return;
        bool left = dx < 0;
        if (left == facingLeft) return;
        facingLeft = left;
        var s = body.localScale;
        s.x = Mathf.Abs(s.x) * (left ? -1 : 1);
        body.localScale = s;
    }

    public bool FacingLeft => facingLeft;

    public void Flash(float seconds = 0.08f) => flash = seconds;

    public void SetTint(Color c) => tint = c;

    public void SetAlpha(float a) => alpha = a;

    public void SetSortingOrder(int order)
    {
        if (group) group.sortingOrder = order;
    }

    void LateUpdate()
    {
        if (flash > 0) flash -= Time.deltaTime;
        var c = flash > 0 ? new Color(1f, 0.55f, 0.5f) : tint;
        for (int i = 0; i < renderers.Count; i++)
        {
            if (!renderers[i]) continue;
            var b = baseColors[i];
            renderers[i].color = new Color(b.r * c.r, b.g * c.g, b.b * c.b, b.a * alpha);
        }
    }
}
