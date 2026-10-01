using System.Collections;
using UnityEngine;

// Music with crossfades. The map track is remembered so the boss track can fade back to it when the boss falls.
public class MusicManager : MonoBehaviour
{
    public static MusicManager I;

    AudioManager audioManager;
    AudioSource a, b;
    string current, mapTrack, bossTrack = "music_boss";
    bool bossActive;
    Coroutine fading;

    public string Current => current;
    public bool BossActive => bossActive;

    public void Init(AudioManager am)
    {
        I = this;
        audioManager = am;
        a = am.NewSource("MusicA", AudioCategory.Music);
        b = am.NewSource("MusicB", AudioCategory.Music);
        a.loop = b.loop = true;
    }

    public static void Play(string id, float fade = 1.5f)
    {
        if (I) I.CrossFade(id, fade);
    }

    public static void PlayMap(string id)
    {
        if (!I) return;
        I.mapTrack = id;
        I.bossActive = false;
        I.CrossFade(id, 2f);
    }

    // Boss enters: map track fades into the boss track; boss dies: back to the map track.
    public static void Boss(bool on)
    {
        if (!I || I.bossActive == on) return;
        I.bossActive = on;
        I.CrossFade(on ? I.bossTrack : I.mapTrack, on ? 1.2f : 3f);
    }

    public static void Stop(float fade = 1f)
    {
        if (I) I.CrossFade(null, fade);
    }

    void CrossFade(string id, float seconds)
    {
        if (id == current && id != null) return;
        current = id;
        if (fading != null) StopCoroutine(fading);
        fading = StartCoroutine(Fade(id, seconds));
    }

    IEnumerator Fade(string id, float seconds)
    {
        var e = audioManager.Find(id);
        var from = a.isPlaying && a.volume > 0.001f ? a : b;
        var to = from == a ? b : a;
        float target = 0;
        if (e != null && e.clips != null && e.clips.Length > 0 && e.clips[0])
        {
            to.clip = e.clips[0];
            to.volume = 0;
            to.loop = e.category == AudioCategory.Music && !id.Contains("victory") && !id.Contains("defeat");
            to.Play();
            target = e.volume;
        }
        else if (!string.IsNullOrEmpty(id)) Debug.Log($"Music: faixa '{id}' sem clip (silêncio)");
        float start = from.volume;
        for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            from.volume = start * (1 - k);
            if (to.isPlaying) to.volume = target * k * audioManager.CategoryVolume(AudioCategory.Music);
            yield return null;
        }
        from.Stop();
        from.volume = 0;
        if (to.isPlaying) to.volume = target * audioManager.CategoryVolume(AudioCategory.Music);
        fading = null;
    }

    public void Refresh()
    {
        var e = audioManager.Find(current);
        var src = a.isPlaying ? a : b;
        if (e != null && src.isPlaying) src.volume = e.volume * audioManager.CategoryVolume(AudioCategory.Music);
    }

    void OnDestroy()
    {
        if (I == this) I = null;
    }
}
