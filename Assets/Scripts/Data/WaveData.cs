using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnEntry
{
    public EnemyData enemy;
    public int count = 1;
}

// Sub-wave: waits `delay`, then releases its entries (mixed, round-robin) one every `interval` seconds.
[System.Serializable]
public class SpawnGroup
{
    public string label;
    public List<SpawnEntry> entries = new();
    [Tooltip("Segundos entre unidades do grupo")] public float interval = 1f;
    [Tooltip("Segundos de espera antes do grupo começar")] public float delay;
    [Tooltip("Caminho da rota (-1 = alterna entre todos)")] public int path = -1;
    [Tooltip("Só aparece a partir deste nível de dificuldade da run (0 = sempre)")] public int minRunTier;

    public int Count
    {
        get
        {
            int n = 0;
            foreach (var e in entries) n += e.count;
            return n;
        }
    }
}

[CreateAssetMenu(menuName = "Bastião/Onda")]
public class WaveData : ScriptableObject
{
    public string title;
    public List<SpawnGroup> groups = new();
    public bool IsBossWave => groups.Exists(g => g.entries.Exists(e => e.enemy && e.enemy.IsBoss));
}
