using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SoundEntry
{
    public string id;
    public AudioCategory category;
    public AudioClip[] clips;
    [Range(0, 1)] public float volume = 0.8f;
    [Tooltip("Variação aleatória de pitch (±)")] public float pitchJitter = 0.06f;
    [Tooltip("Intervalo mínimo entre repetições (evita metralhadora de som)")] public float cooldown = 0.04f;
    [Tooltip("Placeholder gerado por código: trocar pelo som final")] public bool placeholder;
}

[CreateAssetMenu(menuName = "Bastião/Biblioteca de Áudio")]
public class AudioLibrary : ScriptableObject
{
    public UnityEngine.Audio.AudioMixer mixer;
    public List<SoundEntry> sounds = new();

    Dictionary<string, SoundEntry> index;

    public SoundEntry Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (index == null || index.Count != sounds.Count)
        {
            index = new Dictionary<string, SoundEntry>();
            foreach (var s in sounds)
                if (s != null && !string.IsNullOrEmpty(s.id)) index[s.id] = s;
        }
        return index.TryGetValue(id, out var e) ? e : null;
    }
}
