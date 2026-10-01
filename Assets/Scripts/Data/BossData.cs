using UnityEngine;

// Everything that makes a boss more than a big enemy: entrance, phases, special attack, summons, fortress rule.
[CreateAssetMenu(menuName = "Bastião/Chefe")]
public class BossData : ScriptableObject
{
    public string title;
    [TextArea] public string introLine;
    [Tooltip("Segundos parado fazendo a entrada (o jogo continua)")] public float introSeconds = 2.2f;
    public float introCameraShake = 0.12f;

    [Header("Invocação")]
    public EnemyData summon;
    public int summonCount;
    public float summonInterval;

    [Header("Fúria")]
    [Range(0, 1)] public float enrageBelowHp;
    public float enrageSpeedMultiplier = 1;
    public float enrageDamageMultiplier = 1;

    [Header("Ataque especial")]
    public string specialName;
    public float specialInterval;
    public float specialRadius;
    [Tooltip("Dano ao comandante dentro do raio")] public float specialDamage;
    [Tooltip("Torres dentro do raio ficam atordoadas")] public float specialStunSeconds;
    public float specialCameraShake = 0.18f;

    [Header("Fortaleza")]
    [Tooltip("Só com isto ligado o chefe destrói a fortaleza inteira ao chegar")] public bool fortressExecution;
}
