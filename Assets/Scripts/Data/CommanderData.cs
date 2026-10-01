using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AttributeDef
{
    public CommanderAttribute id;
    public string displayName;
    [TextArea] public string description;
    [Tooltip("Efeito por ponto (fração ou valor fixo, ver descrição)")] public float perPoint;
    public int maxLevel = 10;
    [Tooltip("Custo do nível n = base + porNível × n")] public int baseCost = 20;
    public int costPerLevel = 15;
    public Sprite icon;

    public int CostFor(int currentLevel) => baseCost + costPerLevel * currentLevel;
}

// Base stats of the commander. Persistent attributes add to these through CommanderStats (never hardcoded in the controller).
[CreateAssetMenu(menuName = "Bastião/Comandante")]
public class CommanderData : ScriptableObject
{
    [Tooltip("Nome do personagem (HUD, menus, banners)")] public string displayName = "Ulric";
    public string title = "Capitão da Companhia do Corvo";
    [Header("Base")]
    public float maxHp = 600;
    public float damage = 38;
    public float armorPenetration = 4;
    [Tooltip("Segundos entre golpes")] public float attackInterval = 0.95f;
    public float reach = 1.3f;
    [Tooltip("Inimigos atingidos por golpe")] public int cleave = 3;
    [Tooltip("Inimigos comuns segurados ao mesmo tempo (chefes sempre param)")] public int blockCount = 3;
    [Tooltip("Redução fixa de dano por golpe recebido")] public float armor = 2;
    public float regenPerSecond = 1;
    public float speed = 3.1f;
    [Tooltip("Raio em que Tática fortalece torres")] public float tacticsRadius = 3.2f;
    [Tooltip("Segundos de imunidade ao trocar de alvo de golpe recebido (anti-stagger)")] public float hitReactCooldown = 0.5f;
    [Tooltip("Segundos caído antes de voltar na fortaleza. 0 = morte do comandante encerra o mapa")] public float respawnSeconds = 14f;
    [Tooltip("Fração da vida com que ele volta")] [Range(0.1f, 1)] public float respawnHealth = 1f;
    [Header("Poder (ultimate)")]
    public string ultimateName = "Fúria Negra";
    [TextArea] public string ultimateDescription;
    public float ultimateCooldown = 45f;
    [Tooltip("Fração já carregada no início do mapa")] [Range(0, 1)] public float ultimateStartCharge = 0.5f;
    public float ultimateRadius = 2.4f;
    [Tooltip("Dano físico em área (escala com Força)")] public float ultimateDamage = 160f;
    public float ultimateArmorPenetration = 10f;
    [Tooltip("Atordoa inimigos comuns atingidos")] public float ultimateStunSeconds = 1.4f;
    [Tooltip("Duração da fúria depois do golpe")] public float ultimateBuffSeconds = 8f;
    public float ultimateAttackSpeedBonus = 0.4f;
    public float ultimateDamageBonus = 0.25f;
    [Range(0, 0.9f)] public float ultimateDamageReduction = 0.3f;
    public List<AttributeDef> attributes = new();

    public AttributeDef Attribute(CommanderAttribute id) => attributes.Find(a => a.id == id);
}
