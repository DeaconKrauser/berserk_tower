using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// SFX, UI sounds and ambience, routed through the AudioMixer groups (Master/Music/SFX/UI/Ambient).
// Missing ids or clips are silently ignored (logged once): sounds are never required by gameplay.
public class AudioManager : MonoBehaviour
{
    public static AudioManager I;
    const int PoolSize = 28;

    AudioLibrary library;
    AudioMixer mixer;
    readonly Dictionary<AudioCategory, AudioMixerGroup> groups = new();
    readonly List<AudioSource> pool = new();
    readonly Dictionary<string, float> lastPlayed = new();
    readonly HashSet<string> warned = new();
    AudioSource ambientA, ambientB;
    string ambientId;
    MetaSave.Settings settings = new();
    int next;

    public static AudioManager Create(AudioLibrary lib, Transform parent)
    {
        var go = new GameObject("Audio");
        go.transform.SetParent(parent, false);
        var a = go.AddComponent<AudioManager>();
        a.library = lib;
        a.mixer = lib ? lib.mixer : null;
        if (a.mixer)
            foreach (AudioCategory c in System.Enum.GetValues(typeof(AudioCategory)))
            {
                var g = a.mixer.FindMatchingGroups(GroupName(c));
                if (g != null && g.Length > 0) a.groups[c] = g[0];
            }
        for (int i = 0; i < PoolSize; i++) a.pool.Add(a.NewSource($"Sfx{i}", AudioCategory.Sfx));
        a.ambientA = a.NewSource("AmbientA", AudioCategory.Ambient);
        a.ambientB = a.NewSource("AmbientB", AudioCategory.Ambient);
        a.ambientA.loop = a.ambientB.loop = true;
        go.AddComponent<MusicManager>().Init(a);
        return I = a;
    }

    public static string GroupName(AudioCategory c) => c switch
    {
        AudioCategory.Sfx => "SFX",
        AudioCategory.UI => "UI",
        AudioCategory.Ambient => "Ambient",
        AudioCategory.Music => "Music",
        _ => "Master",
    };

    public AudioSource NewSource(string name, AudioCategory c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0;
        if (groups.TryGetValue(c, out var g)) s.outputAudioMixerGroup = g;
        return s;
    }

    public SoundEntry Find(string id) => library ? library.Find(id) : null;

    // Category volume when no mixer is present (the mixer path applies it through exposed parameters).
    public float CategoryVolume(AudioCategory c)
    {
        if (mixer) return 1;
        float v = settings.master;
        return v * c switch
        {
            AudioCategory.Music => settings.music,
            AudioCategory.UI => settings.ui,
            AudioCategory.Ambient => settings.ambient,
            _ => settings.sfx,
        };
    }

    public void ApplySettings(MetaSave.Settings s)
    {
        settings = s ?? new MetaSave.Settings();
        if (!mixer) return;
        SetDb("MasterVolume", settings.master);
        SetDb("MusicVolume", settings.music);
        SetDb("SFXVolume", settings.sfx);
        SetDb("UIVolume", settings.ui);
        SetDb("AmbientVolume", settings.ambient);
    }

    void SetDb(string param, float linear) => mixer.SetFloat(param, linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f);

    public static void Play(string id, float volumeScale = 1)
    {
        if (I) I.PlayInternal(id, null, volumeScale);
    }

    public static void Play(string id, Vector2 at, float volumeScale = 1)
    {
        if (I) I.PlayInternal(id, at, volumeScale);
    }

    void PlayInternal(string id, Vector2? at, float volumeScale)
    {
        if (string.IsNullOrEmpty(id)) return;
        var e = Find(id);
        if (e == null || e.clips == null || e.clips.Length == 0)
        {
            if (warned.Add(id)) Debug.Log($"Audio: som '{id}' sem clip (ignorado)");
            return;
        }
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out var t) && now - t < e.cooldown) return;
        lastPlayed[id] = now;
        var clip = e.clips[Random.Range(0, e.clips.Length)];
        if (!clip) return;
        var src = pool[next];
        next = (next + 1) % pool.Count;
        src.outputAudioMixerGroup = groups.TryGetValue(e.category, out var g) ? g : null;
        src.clip = clip;
        src.volume = e.volume * volumeScale * CategoryVolume(e.category);
        src.pitch = 1 + Random.Range(-e.pitchJitter, e.pitchJitter);
        src.panStereo = at.HasValue ? Mathf.Clamp(at.Value.x / 15f, -1, 1) * 0.55f : 0;
        src.Play();
    }

    public static void Ambient(string id, float fade = 2f)
    {
        if (I) I.StartCoroutine(I.SwapAmbient(id, fade));
    }

    IEnumerator SwapAmbient(string id, float fade)
    {
        if (id == ambientId) yield break;
        ambientId = id;
        var e = Find(id);
        var from = ambientA.isPlaying ? ambientA : ambientB;
        var to = from == ambientA ? ambientB : ambientA;
        float target = 0;
        if (e != null && e.clips != null && e.clips.Length > 0 && e.clips[0])
        {
            to.clip = e.clips[0];
            to.volume = 0;
            to.Play();
            target = e.volume;
        }
        float startFrom = from.volume;
        for (float t = 0; t < fade; t += Time.unscaledDeltaTime)
        {
            float k = t / fade;
            from.volume = startFrom * (1 - k);
            if (to.isPlaying) to.volume = target * k * CategoryVolume(AudioCategory.Ambient);
            yield return null;
        }
        from.Stop();
        if (to.isPlaying) to.volume = target * CategoryVolume(AudioCategory.Ambient);
    }

    void OnDestroy()
    {
        if (I == this) I = null;
    }
}
