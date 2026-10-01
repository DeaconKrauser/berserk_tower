using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

// Creates Assets/Audio/Bastiao.mixer with groups Master > Music, SFX, UI, Ambient and exposed volume
// parameters (MasterVolume, MusicVolume, SFXVolume, UIVolume, AmbientVolume).
// Unity has no public API to author mixers, so this uses the editor's internal AudioMixerController by reflection.
// If that ever fails, AudioManager falls back to per-category volumes on its sources (no mixer needed).
public static class AudioMixerBuilder
{
    public const string Path = "Assets/Audio/Bastiao.mixer";
    static readonly string[] Groups = { "Music", "SFX", "UI", "Ambient" };

    public static AudioMixer Build()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(Path);
        if (existing && Groups.All(g => existing.FindMatchingGroups(g).Length > 0)) return existing;
        try
        {
            if (existing) AssetDatabase.DeleteAsset(Path);
            var asm = typeof(EditorWindow).Assembly;
            var controllerType = asm.GetType("UnityEditor.Audio.AudioMixerController");
            var groupType = asm.GetType("UnityEditor.Audio.AudioMixerGroupController");
            var create = controllerType.GetMethod("CreateMixerControllerAtPath", BindingFlags.Public | BindingFlags.Static);
            var controller = create.Invoke(null, new object[] { Path });
            var master = controllerType.GetProperty("masterGroup").GetValue(controller);
            Expose(controller, controllerType, groupType, master, "MasterVolume");
            foreach (var name in Groups)
            {
                var g = controllerType.GetMethod("CreateNewGroup").Invoke(controller, new object[] { name, false });
                controllerType.GetMethod("AddChildToParent").Invoke(controller, new[] { g, master });
                Expose(controller, controllerType, groupType, g, name == "SFX" ? "SFXVolume" : name + "Volume");
            }
            EditorUtility.SetDirty((UnityEngine.Object)controller);
            AssetDatabase.SaveAssets();
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(Path);
            Debug.Log($"AudioMixerBuilder: {Path} criado ({string.Join(", ", Groups)})");
            return mixer;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AudioMixerBuilder: não foi possível criar o mixer ({e.GetBaseException().Message}); o jogo usa volumes por categoria.");
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(Path);
        }
    }

    static void Expose(object controller, Type controllerType, Type groupType, object group, string name)
    {
        var asm = typeof(EditorWindow).Assembly;
        var guid = groupType.GetMethod("GetGUIDForVolume").Invoke(group, null);
        var pathType = asm.GetType("UnityEditor.Audio.AudioGroupParameterPath");
        var path = Activator.CreateInstance(pathType, group, guid);
        controllerType.GetMethod("AddExposedParameter").Invoke(controller, new[] { path });
        // rename the exposed parameter (it is created as "MyExposedParam")
        var prop = controllerType.GetProperty("exposedParameters");
        var arr = (Array)prop.GetValue(controller);
        for (int i = 0; i < arr.Length; i++)
        {
            var p = arr.GetValue(i);
            var f = p.GetType().GetField("guid");
            if (f.GetValue(p).Equals(guid))
            {
                p.GetType().GetField("name").SetValue(p, name);
                arr.SetValue(p, i);
            }
        }
        prop.SetValue(controller, arr);
    }
}
