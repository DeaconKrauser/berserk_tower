using UnityEngine;

// Final commander numbers for a battle: CommanderData base values + permanent attributes from the meta save.
public struct CommanderStats
{
    public float maxHp, damage, attackInterval, armor, regen, reach, speed, armorPenetration, tacticsBonus, tacticsRadius, ultimateDamage;
    public int cleave, blockCount;
}

public static class CommanderProgression
{
    public static CommanderStats Compute(CommanderData d, MetaSave save)
    {
        var s = new CommanderStats
        {
            maxHp = d.maxHp, damage = d.damage, attackInterval = d.attackInterval, armor = d.armor, regen = d.regenPerSecond,
            reach = d.reach, speed = d.speed, armorPenetration = d.armorPenetration, cleave = d.cleave, blockCount = d.blockCount,
            tacticsRadius = d.tacticsRadius, ultimateDamage = d.ultimateDamage,
        };
        foreach (var a in d.attributes)
        {
            int lv = save != null ? save.Level(a.id) : 0;
            if (lv <= 0) continue;
            float v = a.perPoint * lv;
            switch (a.id)
            {
                case CommanderAttribute.Strength: s.damage *= 1 + v; s.ultimateDamage *= 1 + v; break;
                case CommanderAttribute.Vigor: s.maxHp *= 1 + v; break;
                case CommanderAttribute.Fury: s.attackInterval /= 1 + v; break;
                case CommanderAttribute.Discipline: s.armor += v; break;
                case CommanderAttribute.Tactics: s.tacticsBonus += v; break;
                case CommanderAttribute.DarkFaith: s.regen += v; break;
            }
        }
        return s;
    }

    // "+12% dano" style text for the commander screen.
    public static string Effect(AttributeDef a, int level)
    {
        float v = a.perPoint * level;
        return a.id switch
        {
            CommanderAttribute.Strength => $"+{v * 100:0}% dano físico",
            CommanderAttribute.Vigor => $"+{v * 100:0}% vida máxima",
            CommanderAttribute.Fury => $"+{v * 100:0}% velocidade de ataque",
            CommanderAttribute.Discipline => $"+{v:0.#} armadura",
            CommanderAttribute.Tactics => $"+{v * 100:0}% dano das torres próximas",
            CommanderAttribute.DarkFaith => $"+{v:0.#} HP/s de regeneração",
            _ => "",
        };
    }
}
