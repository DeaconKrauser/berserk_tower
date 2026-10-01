using System;

public enum DamageType { Physical, Fire, Magic, Bleed }

[Flags]
public enum EnemyTag
{
    None = 0, Fast = 1, Armored = 2, Horde = 4, Elite = 8, Boss = 16, Undead = 32, Beast = 64, Caster = 128,
}

public enum TargetPriority { First, Strongest, Elite }

public static class TargetPriorityNames
{
    public static string Pt(TargetPriority p) => p switch
    {
        TargetPriority.Strongest => "mais forte",
        TargetPriority.Elite => "elites",
        _ => "primeiro à frente",
    };
}

// How a tower acts: fires projectiles, arcs magic between enemies, buffs neighbours, or waits on the road.
public enum TowerKind { Projectile, Chain, Aura, Trap }

public enum PlacementRule { OffRoad, OnRoad }

// How MapController paints the road surface (always continuous, never tile blocks).
public enum RoadStyle { Cobble, Mud }

// The [Q] power each skin brings (CommanderCombat).
public enum CommanderPower { BlackFury, AbyssArmor, ScarletBanner, Eclipse }

public enum CommanderAttribute { Strength, Vigor, Fury, Discipline, Tactics, DarkFaith }

public enum AudioCategory { Sfx, UI, Ambient, Music }

public enum ZoneKind { Blocked, Cursed, Water, Graveyard, Ruins, Spawn, Fortress }

public static class EnemyTagNames
{
    public static string Pt(EnemyTag t) => t switch
    {
        EnemyTag.Fast => "Rápido",
        EnemyTag.Armored => "Blindado",
        EnemyTag.Horde => "Horda",
        EnemyTag.Elite => "Elite",
        EnemyTag.Boss => "Chefe",
        EnemyTag.Undead => "Morto-vivo",
        EnemyTag.Beast => "Fera",
        EnemyTag.Caster => "Conjurador",
        _ => "",
    };

    public static string Join(EnemyTag tags)
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (EnemyTag t in Enum.GetValues(typeof(EnemyTag)))
            if (t != EnemyTag.None && (tags & t) != 0) parts.Add(Pt(t));
        return string.Join(" · ", parts);
    }
}

public static class DamageTypeNames
{
    public static string Pt(DamageType t) => t switch
    {
        DamageType.Physical => "físico",
        DamageType.Fire => "fogo",
        DamageType.Magic => "mágico",
        DamageType.Bleed => "sangramento",
        _ => "",
    };
}
