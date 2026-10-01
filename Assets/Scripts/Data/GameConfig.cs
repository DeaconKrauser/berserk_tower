using System.Collections.Generic;
using UnityEngine;

// Root of all tunable data (Assets/Resources/GameConfig.asset).
[CreateAssetMenu(menuName = "Bastião/Configuração do Jogo")]
public class GameConfig : ScriptableObject
{
    public CommanderData commander;
    public List<CommanderSkinData> skins = new();
    public List<MapData> maps = new();
    [Tooltip("Ordem = barra de construção e teclas 1-7")] public List<TowerData> towers = new();
    public MetaProgressionData meta;
    public AudioLibrary audio;
    public UISkin ui;

    [Header("Regras gerais")]
    [Range(0, 1)] public float sellRefund = 0.7f;
    [Tooltip("Fração mínima do dano físico que atravessa qualquer armadura")] [Range(0, 1)] public float minPhysicalDamage = 0.15f;
    [Tooltip("Ouro devolvido se vender na mesma preparação em que construiu")] [Range(0, 1)] public float sameWaveRefund = 1f;
    public float[] gameSpeeds = { 1, 2, 4 };
    [Tooltip("Modo automático de ondas: segundos de preparação antes da próxima onda começar sozinha")]
    public float autoWaveDelay = 12f;

    public static GameConfig Load() => Resources.Load<GameConfig>("GameConfig");

    public CommanderSkinData Skin(string id) => skins.Find(s => s.id == id) ?? (skins.Count > 0 ? skins[0] : null);
    public MapData Map(string id) => maps.Find(m => m.id == id);
}
