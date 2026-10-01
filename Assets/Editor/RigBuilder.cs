using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

// Turns Assets/Art/Animations/<Name>/{rig.json, clips.json, parts/*.png} (written by tools/build_rigs.py)
// into a cutout-rig prefab: Root (Animator + SortingGroup) / Flip / Body / joints... with one SpriteRenderer per part,
// one AnimationClip per state and an AnimatorController (Idle, Walk, Attack, Hit, Death [+ Cast, Special, Spawn, Intro]).
// The root of the prefab never moves: gameplay moves the parent; animation happens below it.
public static class RigBuilder
{
    const string Root = "Assets/Art/Animations";
    public static readonly string[] Triggers = { "Attack", "Hit", "Cast", "Special", "Spawn", "Intro" };

    [Serializable] class RigPart { public string name, parent, sprite; public float z; public float[] pivot; public int[] rect; }
    [Serializable] class Flipbook { public string name; public string[] frames; public int[] size; public float[] origin; }
    [Serializable] class RigFile { public string name, portrait; public int ppu, height, width; public float[] feet; public RigPart[] parts; public Flipbook flipbook; }

    [MenuItem("Bastião/Reconstruir rigs dos personagens")]
    public static void BuildAll()
    {
        foreach (var dir in Directory.GetDirectories(Root))
            if (File.Exists(Path.Combine(dir, "rig.json")))
                Build(dir.Replace('\\', '/'));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static GameObject Build(string dir)
    {
        var rig = JsonUtility.FromJson<RigFile>(File.ReadAllText(Path.Combine(dir, "rig.json")));
        var clipsJson = File.Exists(Path.Combine(dir, "clips.json")) ? MiniJson.Parse(File.ReadAllText(Path.Combine(dir, "clips.json"))) as Dictionary<string, object> : new();
        float ppu = rig.ppu;
        var feet = new Vector2(rig.feet[0], rig.feet[1]);
        var byName = rig.parts.ToDictionary(p => p.name);

        // ---- sprites: pivot exactly on the joint, so each part rotates about its GameObject
        foreach (var p in rig.parts.Where(p => !string.IsNullOrEmpty(p.sprite)))
        {
            var path = $"{dir}/{p.sprite}";
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) { AssetDatabase.ImportAsset(path); ti = (TextureImporter)AssetImporter.GetAtPath(path); }
            var pivot = new Vector2((p.pivot[0] - p.rect[0]) / p.rect[2], 1f - (p.pivot[1] - p.rect[1]) / p.rect[3]);
            SetPivot(ti, pivot);
        }
        if (rig.flipbook != null && rig.flipbook.frames != null)
            foreach (var f in rig.flipbook.frames)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath($"{dir}/{f}");
                if (ti == null) { AssetDatabase.ImportAsset($"{dir}/{f}"); ti = (TextureImporter)AssetImporter.GetAtPath($"{dir}/{f}"); }
                SetPivot(ti, new Vector2(rig.flipbook.origin[0] / rig.flipbook.size[0], 1f - rig.flipbook.origin[1] / rig.flipbook.size[1]));
            }

        // ---- hierarchy
        var root = new GameObject(rig.name);
        root.AddComponent<SortingGroup>();
        var animator = root.AddComponent<Animator>();
        // Root (gameplay never animates it) / Flip (mirrored for facing, never animated) / Body (whole-body clip curves) / joints
        var flipT = new GameObject("Flip").transform;
        flipT.SetParent(root.transform, false);
        var body = new GameObject("Body").transform;
        body.SetParent(flipT, false);
        var made = new Dictionary<string, Transform>();
        var paths = new Dictionary<string, string>();
        var rest = new Dictionary<string, Vector3>();
        var order = rig.parts.OrderBy(p => p.z).Select(p => p.name).ToList();

        Transform Make(string n)
        {
            if (made.TryGetValue(n, out var t)) return t;
            var p = byName[n];
            Transform parent = string.IsNullOrEmpty(p.parent) ? body : Make(p.parent);
            var parentPivot = string.IsNullOrEmpty(p.parent) ? feet : new Vector2(byName[p.parent].pivot[0], byName[p.parent].pivot[1]);
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            var local = new Vector3((p.pivot[0] - parentPivot.x) / ppu, -(p.pivot[1] - parentPivot.y) / ppu, 0);
            go.transform.localPosition = local;
            rest[n] = local;
            if (!string.IsNullOrEmpty(p.sprite))
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{p.sprite}");
                sr.sortingOrder = order.IndexOf(n);
            }
            made[n] = go.transform;
            paths[n] = AnimationUtility.CalculateTransformPath(go.transform, root.transform);
            return go.transform;
        }
        foreach (var p in rig.parts) Make(p.name);

        SpriteRenderer flip = null;
        Sprite[] flipFrames = null;
        if (rig.flipbook != null && rig.flipbook.frames != null && rig.flipbook.frames.Length > 0)
        {
            flipFrames = rig.flipbook.frames.Select(f => AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{f}")).ToArray();
            var go = new GameObject(rig.flipbook.name);
            go.transform.SetParent(body, false);
            flip = go.AddComponent<SpriteRenderer>();
            flip.sprite = flipFrames[0];
            flip.sortingOrder = order.Count;
            flip.enabled = false;
        }

        // ---- clips
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var (name, value) in clipsJson)
        {
            var c = (Dictionary<string, object>)value;
            var clip = Clip(dir, rig.name, name, c, ppu, paths, rest, made, root, flip, flipFrames);
            clips[name] = clip;
        }

        // ---- controller
        var controller = Controller(dir, rig.name, clips);
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var prefabPath = $"{dir}/{rig.name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        Debug.Log($"RigBuilder: {prefabPath} ({rig.parts.Length} parts, {clips.Count} clips)");
        return prefab;
    }

    static void SetPivot(TextureImporter ti, Vector2 pivot)
    {
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        bool changed = s.spriteAlignment != (int)SpriteAlignment.Custom || (s.spritePivot - pivot).sqrMagnitude > 1e-8f;
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = pivot;
        ti.SetTextureSettings(s);
        if (changed) ti.SaveAndReimport();
    }

    static AnimationClip Clip(string dir, string rigName, string name, Dictionary<string, object> c, float ppu,
        Dictionary<string, string> paths, Dictionary<string, Vector3> rest, Dictionary<string, Transform> made, GameObject root,
        SpriteRenderer flip, Sprite[] flipFrames)
    {
        string path = $"{dir}/{rigName}_{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (!clip)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), null);
        float length = Convert.ToSingle(c["length"]);
        bool loop = c.TryGetValue("loop", out var l) && (bool)l;
        clip.frameRate = 30;

        var curves = c.TryGetValue("curves", out var cv) ? (Dictionary<string, object>)cv : new();
        foreach (var (part, value) in curves)
        {
            var kinds = (Dictionary<string, object>)value;
            string p = part == "_root" ? "Flip/Body" : paths.GetValueOrDefault(part);
            if (p == null) continue;
            Vector3 r0 = part == "_root" ? Vector3.zero : rest[part];
            if (kinds.TryGetValue("rot", out var rot))
            {
                Set(clip, p, "localEulerAnglesRaw.z", Keys(rot, 1, v => v, length, loop));
                Set(clip, p, "localEulerAnglesRaw.x", Const(0, length));
                Set(clip, p, "localEulerAnglesRaw.y", Const(0, length));
            }
            if (kinds.TryGetValue("pos", out var pos))
            {
                Set(clip, p, "m_LocalPosition.x", Keys(pos, 1, v => r0.x + v / ppu, length, loop));
                Set(clip, p, "m_LocalPosition.y", Keys(pos, 2, v => r0.y + v / ppu, length, loop));
            }
            if (kinds.TryGetValue("scale", out var sc))
            {
                Set(clip, p, "m_LocalScale.x", Keys(sc, 1, v => v, length, loop));
                Set(clip, p, "m_LocalScale.y", Keys(sc, 2, v => v, length, loop));
            }
        }

        // flipbook clips show the frames and hide the rig; every other clip does the opposite
        if (flip)
        {
            bool isFlip = c.TryGetValue("flipbook", out var fb);
            string flipPath = AnimationUtility.CalculateTransformPath(flip.transform, root.transform);
            Set(clip, flipPath, "m_Enabled", Const(isFlip ? 1 : 0, length), typeof(SpriteRenderer));
            foreach (var (n, t) in made)
                if (t.GetComponent<SpriteRenderer>())
                    Set(clip, paths[n], "m_Enabled", Const(isFlip ? 0 : 1, length), typeof(SpriteRenderer));
            if (isFlip)
            {
                float fps = Convert.ToSingle(((Dictionary<string, object>)fb)["fps"]);
                var keys = flipFrames.Select((s, i) => new ObjectReferenceKeyframe { time = i / fps, value = s })
                    .Append(new ObjectReferenceKeyframe { time = flipFrames.Length / fps, value = flipFrames[0] }).ToArray();
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(flipPath, typeof(SpriteRenderer), "m_Sprite"), keys);
                length = flipFrames.Length / fps;
            }
        }

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.stopTime = length;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void Set(AnimationClip clip, string path, string prop, AnimationCurve curve, Type type = null) =>
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type ?? typeof(Transform), prop), curve);

    static AnimationCurve Const(float v, float length) => new(new Keyframe(0, v), new Keyframe(Mathf.Max(length, 0.01f), v));

    static AnimationCurve Keys(object list, int index, Func<float, float> map, float length, bool loop)
    {
        var keys = new List<Keyframe>();
        foreach (var k in (List<object>)list)
        {
            var arr = (List<object>)k;
            keys.Add(new Keyframe(Convert.ToSingle(arr[0]), map(Convert.ToSingle(arr[Mathf.Min(index, arr.Count - 1)]))));
        }
        var curve = new AnimationCurve(keys.ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        if (loop) { curve.preWrapMode = WrapMode.Loop; curve.postWrapMode = WrapMode.Loop; }
        return curve;
    }

    static AnimatorController Controller(string dir, string rigName, Dictionary<string, AnimationClip> clips)
    {
        string path = $"{dir}/{rigName}.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path)) AssetDatabase.DeleteAsset(path);
        var c = AnimatorController.CreateAnimatorControllerAtPath(path);
        c.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        c.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        c.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
        c.AddParameter("ActionSpeed", AnimatorControllerParameterType.Float);
        foreach (var t in Triggers) c.AddParameter(t, AnimatorControllerParameterType.Trigger);
        foreach (var p in c.parameters)
            if (p.type == AnimatorControllerParameterType.Float) p.defaultFloat = 1;
        c.parameters = c.parameters.Select(p => { if (p.type == AnimatorControllerParameterType.Float) p.defaultFloat = 1; return p; }).ToArray();

        var sm = c.layers[0].stateMachine;
        var states = new Dictionary<string, AnimatorState>();
        AnimatorState State(string n, Vector3 at)
        {
            var s = sm.AddState(n, at);
            s.motion = clips[n];
            s.writeDefaultValues = true;
            states[n] = s;
            return s;
        }
        var idle = State("Idle", new Vector3(300, 0));
        sm.defaultState = idle;
        if (clips.ContainsKey("Walk"))
        {
            var walk = State("Walk", new Vector3(300, 100));
            walk.speedParameter = "MoveSpeed";
            walk.speedParameterActive = true;
            var a = idle.AddTransition(walk);
            a.hasExitTime = false; a.duration = 0.08f;
            a.AddCondition(AnimatorConditionMode.If, 0, "Moving");
            var b = walk.AddTransition(idle);
            b.hasExitTime = false; b.duration = 0.1f;
            b.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
        }
        int y = 200;
        foreach (var t in Triggers)
        {
            if (!clips.ContainsKey(t)) continue;
            var s = State(t, new Vector3(560, y));
            y += 70;
            if (t is "Attack" or "Cast" or "Special") { s.speedParameter = "ActionSpeed"; s.speedParameterActive = true; }
            var any = sm.AddAnyStateTransition(s);
            any.hasExitTime = false;
            any.duration = t == "Hit" ? 0.03f : 0.05f;
            any.canTransitionToSelf = t == "Attack";
            any.AddCondition(AnimatorConditionMode.If, 0, t);
            any.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var back = s.AddTransition(idle);
            back.hasExitTime = true; back.exitTime = 1; back.duration = 0.1f;
        }
        if (clips.ContainsKey("Death"))
        {
            var death = State("Death", new Vector3(560, -120));
            var any = sm.AddAnyStateTransition(death);
            any.hasExitTime = false; any.duration = 0.05f; any.canTransitionToSelf = false;
            any.AddCondition(AnimatorConditionMode.If, 0, "Dead");
        }
        EditorUtility.SetDirty(c);
        return c;
    }
}

// Tiny JSON reader (objects, arrays, numbers, strings, bools, null) for clips.json; Unity's JsonUtility can't read maps.
public static class MiniJson
{
    public static object Parse(string s) { int i = 0; return Value(s, ref i); }

    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    static object Value(string s, ref int i)
    {
        Ws(s, ref i);
        char ch = s[i];
        if (ch == '{')
        {
            var d = new Dictionary<string, object>();
            i++;
            Ws(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(s, ref i);
                var k = (string)Value(s, ref i);
                Ws(s, ref i); i++; // :
                d[k] = Value(s, ref i);
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; return d;
            }
        }
        if (ch == '[')
        {
            var l = new List<object>();
            i++;
            Ws(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; return l;
            }
        }
        if (ch == '"')
        {
            var sb = new System.Text.StringBuilder();
            i++;
            while (s[i] != '"')
            {
                if (s[i] == '\\') { i++; sb.Append(s[i] switch { 'n' => '\n', 't' => '\t', _ => s[i] }); }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }
        if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
        if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
        if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
        int start = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        return double.Parse(s.Substring(start, i - start), System.Globalization.CultureInfo.InvariantCulture);
    }
}
