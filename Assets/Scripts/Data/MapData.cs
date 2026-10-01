using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PropSprite
{
    public string name;
    public Sprite sprite;
    [Tooltip("Fica deitado no chão (não é ordenado por Y, não bloqueia)")] public bool decal;
    public bool torch;
    public Color lightColor = new(1f, 0.6f, 0.28f);
    [Tooltip("Peso na decoração aleatória (0 = só posicionado à mão)")] public float scatterWeight;
}

// A playable map: look (tiles/props/light/sound), layouts, waves and economy.
[CreateAssetMenu(menuName = "Bastião/Mapa")]
public class MapData : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite preview;

    [Header("Rotas (uma é sorteada a cada etapa)")]
    public List<RouteData> routes = new();

    [Header("Ondas")]
    public List<WaveData> waves = new();
    [Tooltip("Inimigos que aparecem neste mapa (para a UI)")] public List<EnemyData> roster = new();

    [Header("Economia")]
    public int startingGold = 300;
    public int fortressHp = 20;
    [Tooltip("Ouro ao vencer uma onda = base + porOnda × número da onda")] public int waveClearBonus = 20;
    public int waveClearBonusPerWave = 4;

    [Header("Visual")]
    public Sprite[] groundTiles;
    public Sprite[] groundAccentTiles;
    public Sprite[] roadTiles;
    public Sprite[] roadAccentTiles;
    public Sprite[] waterTiles;
    public Sprite[] bridgeTiles;
    [Tooltip("Cor da borda irregular da estrada")] public Color roadRim = new(0.12f, 0.1f, 0.1f);
    public List<PropSprite> props = new();
    public Sprite fortressKeep;
    public Sprite fortressTower;
    public Color globalLightColor = new(0.8f, 0.82f, 1f);
    public float globalLightIntensity = 0.75f;
    public Color backgroundColor = new(0.04f, 0.04f, 0.05f);

    [Header("Áudio")]
    public string musicId;
    public string ambientId;

    public PropSprite Prop(string name) => props.Find(p => p.name == name);
}
