using UnityEngine;

[CreateAssetMenu(menuName = "Bastião/Inimigo")]
public class EnemyData : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea] public string description;
    [Tooltip("Prefab do rig (Assets/Art/Animations/<Nome>/<Nome>.prefab)")] public GameObject rig;
    public float rigScale = 1;
    public Sprite icon;

    [Header("Vida e defesa")]
    public float maxHp;
    [Tooltip("Redução fixa de dano físico por golpe")] public float armor;
    [Range(-1, 1)] public float physicalResistance;
    [Range(-1, 1)] public float fireResistance;
    [Range(-1, 1)] public float magicResistance;
    [Range(-1, 1)] public float bleedResistance;
    public float regenPerSecond;
    public EnemyTag tags;

    [Header("Movimento")]
    [Tooltip("Unidades por segundo")] public float movementSpeed = 1;
    [Tooltip("Meia-largura do corpo: separação lateral na estrada e bloqueio de construção")] public float bodyRadius = 0.3f;
    [Tooltip("Meio-comprimento do corpo na direção da marcha (quadrúpedes são longos); 0 = igual ao raio")] public float bodyLength;

    [Header("Ataque")]
    [Tooltip("Dano à fortaleza ao alcançá-la")] public int damageToFortress = 1;
    [Tooltip("Dano corpo a corpo contra o comandante")] public float meleeDamage;
    [Tooltip("Segundos entre golpes (attack speed = 1 / intervalo)")] public float attackInterval = 1;

    [Header("Recompensa")]
    public int goldReward;

    [Header("Conjurador")]
    [Tooltip("Segundos entre conjurações (0 = não conjura)")] public float castInterval;
    public float castRadius;
    [Tooltip("Escudo dado a aliados próximos (absorve dano)")] public float castShield;
    public float castHeal;

    [Header("Outros")]
    [Tooltip("Ao morrer, divide-se nestes inimigos")] public EnemyData splitInto;
    public int splitCount;
    [Tooltip("Cura ao devorar inimigos que morrem por perto")] public float feedHeal;
    public float feedRadius;
    [Tooltip("Configuração de chefe")] public BossData boss;

    [Header("Apresentação")]
    public Color lightColor = Color.white;
    public float lightRadius;
    public string spawnSfx, attackSfx, hitSfx, deathSfx;

    public bool IsBoss => boss != null || (tags & EnemyTag.Boss) != 0;
    public bool IsElite => (tags & (EnemyTag.Elite | EnemyTag.Boss)) != 0;
    public bool Has(EnemyTag t) => (tags & t) != 0;

    public float Resistance(DamageType t) => t switch
    {
        DamageType.Physical => physicalResistance,
        DamageType.Fire => fireResistance,
        DamageType.Magic => magicResistance,
        DamageType.Bleed => bleedResistance,
        _ => 0,
    };
}
