using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Authoring tool: writes every ScriptableObject of the alpha (towers, enemies, bosses, waves, routes, maps,
// commander, skins, meta progression, audio library, UI skin) into Assets/Data and Resources/GameConfig.
// The numbers live here only so they can be regenerated in one go while balancing; the game reads the assets.
// Re-running OVERWRITES the assets: after hand-editing an asset in the Inspector, don't run it again.
public static class ContentBuilder
{
    const string D = "Assets/Data", Art = "Assets/Art", Anim = "Assets/Art/Animations";

    [MenuItem("Bastião/Recriar conteúdo (sobrescreve Assets/Data)")]
    public static void BuildAll()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        RigBuilder.BuildAll();
        var cfg = Asset<GameConfig>("Assets/Resources/GameConfig.asset");
        cfg.ui = BuildUi();
        cfg.audio = BuildAudio();
        var proj = Projectiles();
        var towers = Towers(proj);
        cfg.towers = towers;
        var (enemies, bosses) = Enemies();
        cfg.maps = Maps(enemies);
        cfg.commander = Commander(cfg.ui);
        cfg.skins = Skins();
        cfg.meta = Meta();
        cfg.sellRefund = 0.7f;
        cfg.minPhysicalDamage = 0.15f;
        cfg.sameWaveRefund = 1f;
        cfg.gameSpeeds = new float[] { 1, 2, 4 };
        cfg.autoWaveDelay = 12f;
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log($"ContentBuilder: {towers.Count} torres, {enemies.Count} inimigos, {cfg.maps.Count} mapas, {cfg.skins.Count} skins");
    }

    public static void BuildAllAndPlayer()
    {
        BuildAll();
        Build.Windows();
    }

    // ---------------------------------------------------------------- UI

    static UISkin BuildUi()
    {
        var u = Asset<UISkin>($"{D}/UI/UISkin.asset");
        Sprite F(string n) => S($"{Art}/UI/Frames/{n}.png");
        Sprite I(string n) => S($"{Art}/UI/Icons/{n}_64.png");
        Sprite B(string n) => S($"{Art}/UI/Backgrounds/{n}.png");
        u.panel = F("panel_b6"); u.panelDark = F("panel_dark_b6"); u.panelSolid = F("panel_solid_b6"); u.panelThin = F("panel_thin_b5"); u.plate = F("plate_b8");
        u.card = F("card_b4"); u.cardSelected = F("card_selected_b4"); u.cardDisabled = F("card_disabled_b4");
        u.barFrame = F("bar_frame_b3"); u.divider = F("divider"); u.vignette = F("vignette");
        u.buttonNormal = F("button_normal_b4"); u.buttonHover = F("button_hover_b4"); u.buttonPressed = F("button_pressed_b4"); u.buttonDisabled = F("button_disabled_b4");
        u.roundNormal = F("round_normal"); u.roundHover = F("round_hover"); u.roundPressed = F("round_pressed"); u.roundActive = F("round_active");
        u.gold = I("gold"); u.heart = I("heart"); u.wave = I("wave"); u.skull = I("skull"); u.boss = I("boss"); u.attack = I("attack");
        u.archer = I("archer"); u.axe = I("axe"); u.armor = I("armor"); u.fire = I("fire"); u.magic = I("magic"); u.heal = I("heal");
        u.sell = I("sell"); u.upgrade = I("upgrade");
        u.menuBackground = B("menu"); u.commanderBackground = B("commander"); u.skinsBackground = B("skins"); u.battlefieldBackground = B("battlefield");
        return u;
    }

    // ---------------------------------------------------------------- audio

    static AudioLibrary BuildAudio()
    {
        var lib = Asset<AudioLibrary>($"{D}/Audio/AudioLibrary.asset");
        lib.mixer = AudioMixerBuilder.Build();
        lib.sounds = new List<SoundEntry>();
        var manifest = "Assets/Audio/manifest.json";
        if (!File.Exists(manifest))
        {
            Debug.LogWarning("ContentBuilder: Assets/Audio/manifest.json não existe (rode tools/build_audio.py). Áudio vazio.");
            return lib;
        }
        var list = (List<object>)MiniJson.Parse(File.ReadAllText(manifest));
        foreach (Dictionary<string, object> e in list)
        {
            var files = ((List<object>)e["files"]).Select(f => AssetDatabase.LoadAssetAtPath<AudioClip>((string)f)).Where(c => c).ToArray();
            lib.sounds.Add(new SoundEntry
            {
                id = (string)e["id"],
                category = (AudioCategory)System.Enum.Parse(typeof(AudioCategory), (string)e["category"]),
                clips = files,
                volume = System.Convert.ToSingle(e["volume"]),
                pitchJitter = System.Convert.ToSingle(e["pitch"]),
                cooldown = System.Convert.ToSingle(e["cooldown"]),
                placeholder = (bool)e["placeholder"],
            });
        }
        return lib;
    }

    // ---------------------------------------------------------------- projectiles

    static Dictionary<string, ProjectileData> Projectiles()
    {
        ProjectileData P(string file, string sprite, float speed, float arc, bool homing, Color impact, ImpactStyle style, bool sticks = false, bool spins = false, Color trail = default)
        {
            var p = Asset<ProjectileData>($"{D}/Projectiles/{file}.asset");
            p.sprite = S($"{Art}/Sprites/Projectiles/{sprite}.png");
            p.speed = speed;
            p.arcHeight = arc;
            p.homing = homing;
            p.impactColor = impact;
            p.impact = style;
            p.sticks = sticks;
            p.spins = spins;
            p.trailColor = trail;
            return p;
        }
        return new Dictionary<string, ProjectileData>
        {
            ["arrow"] = P("Arrow", "arrow", 16, 0.35f, true, Hex("E8DCC0"), ImpactStyle.Spark, sticks: true),
            ["bolt"] = P("Bolt", "bolt", 24, 0.06f, true, Hex("C8C8D0"), ImpactStyle.Heavy, sticks: true),
            ["fire"] = P("CursedFire", "cursed_fire", 7.5f, 1.4f, false, Hex("B048D8"), ImpactStyle.Fire, trail: new Color(0.75f, 0.35f, 1f, 0.8f)),
            ["crow"] = P("Crow", "crow", 9, 0.5f, true, Hex("8C46D2"), ImpactStyle.Curse),
            ["blood"] = P("BloodOrb", "blood_orb", 12, 0.2f, true, Hex("E02838"), ImpactStyle.Blood),
        };
    }

    // ---------------------------------------------------------------- towers

    static TowerStats Atk(float dmg, float interval, float range, DamageType type = DamageType.Physical, float pen = 0, float splash = 0,
        int targets = 1, int pierce = 0, float crit = 0, float critX = 0, float boss = 0, float stun = 0, float stunFor = 0,
        float burn = 0, float burnFor = 0, float bleed = 0, float bleedFor = 0, float slow = 0, float slowFor = 0,
        float vuln = 0, float curseFor = 0, float shred = 0, bool spread = false, float fire = 0, float fireFor = 0, float fireR = 0,
        int chain = 0, float chainR = 0, float falloff = 0, float execute = 0, int durability = 0) => new()
    {
        damage = dmg, attackInterval = interval, range = range, damageType = type, armorPenetration = pen, splashRadius = splash,
        targets = targets, pierce = pierce, critChance = crit, critMultiplier = critX, bossDamageMultiplier = boss,
        stunChance = stun, stunDuration = stunFor, burnDps = burn, burnDuration = burnFor, bleedDps = bleed, bleedDuration = bleedFor,
        slowAmount = slow, slowDuration = slowFor, vulnerability = vuln, curseDuration = curseFor, armorShred = shred, curseSpreads = spread,
        groundFireDps = fire, groundFireDuration = fireFor, groundFireRadius = fireR, chainCount = chain, chainRange = chainR,
        chainFalloff = falloff, executeThreshold = execute, trapDurability = durability,
    };

    static TowerStats Aura(float radius, float dmg = 0, float speed = 0, float range = 0, float shred = 0, float heal = 0) => new()
    {
        auraRadius = radius, auraDamageBonus = dmg, auraAttackSpeedBonus = speed, auraRangeBonus = range,
        auraArmorShred = shred, commanderHealPerSecond = heal, attackInterval = 1, targets = 1,
    };

    static (string, string, int, float, TowerStats) Up(string name, string desc, int cost, TowerStats s, float muzzle = 0) => (name, desc, cost, muzzle, s);

    static TowerData Tower(string id, string name, string role, string good, int cost, TowerKind kind, PlacementRule place, float footprint,
        float muzzle, TargetPriority targeting, ProjectileData proj, Color light, float lightR, string fire, string impact,
        TowerStats t1, params (string name, string desc, int cost, float muzzle, TowerStats s)[] ups)
    {
        var t = Asset<TowerData>($"{D}/Towers/{id}.asset");
        t.id = id;
        t.displayName = name;
        t.role = role;
        t.goodAgainst = good;
        t.cost = cost;
        t.kind = kind;
        t.placement = place;
        t.footprintRadius = footprint;
        t.muzzleHeight = muzzle;
        t.targeting = targeting;
        t.projectile = proj;
        t.lightColor = light;
        t.lightRadius = lightR;
        t.fireSfx = fire;
        t.impactSfx = impact;
        t.sprite = S($"{Art}/Towers/{id}_t1.png");
        t.icon = t.sprite;
        t.stats = t1;
        t.upgrades = ups.Select((u, i) =>
        {
            var a = Asset<TowerUpgradeData>($"{D}/Towers/Upgrades/{id}_t{i + 2}.asset");
            a.displayName = u.name;
            a.description = u.desc;
            a.cost = u.cost;
            a.muzzleHeight = u.muzzle;
            a.sprite = S($"{Art}/Towers/{id}_t{i + 2}.png");
            a.stats = u.s;
            return a;
        }).ToList();
        return t;
    }

    static List<TowerData> Towers(Dictionary<string, ProjectileData> p)
    {
        var none = new Color(0, 0, 0, 0);
        return new List<TowerData>
        {
            Tower("archer", "Torre de Arqueiros", "Barata e rápida · alvo único · sofre contra armadura", "Bom contra rápidos e frágeis",
                70, TowerKind.Projectile, PlacementRule.OffRoad, 0.55f, 1.55f, TargetPriority.First, p["arrow"], none, 0, "archer_shot", "arrow_impact",
                Atk(11, 0.6f, 3.6f),
                Up("Arqueiros Veteranos", "Tensão maior e mãos firmes: atiram bem mais rápido.", 100, Atk(12, 0.42f, 3.8f), 1.9f),
                Up("Flechas Perfurantes", "Pontas de ferro negro atravessam o primeiro alvo e ferem quem vem atrás.", 160, Atk(15, 0.44f, 4.0f, pen: 3, pierce: 1), 2.2f),
                Up("Chuva Rubra", "Dois alvos por disparo e golpes críticos.", 290, Atk(16, 0.42f, 4.3f, pen: 4, pierce: 1, targets: 2, crit: 0.15f, critX: 2f), 2.5f)),
            Tower("ballista", "Balista", "Lenta · alcance longo · perfura armadura · caça o mais resistente", "Bom contra blindados, elites e chefes",
                140, TowerKind.Projectile, PlacementRule.OffRoad, 0.6f, 1.2f, TargetPriority.Strongest, p["bolt"], none, 0, "ballista_shot", "ballista_impact",
                Atk(75, 2.6f, 5.0f, pen: 8),
                Up("Molas de Ferro Negro", "Mais tensão: muito mais dano e alcance.", 190, Atk(115, 2.4f, 5.3f, pen: 9), 1.6f),
                Up("Virotes Rúnicos", "Runas de sangue rasgam placas de aço: ignora quase toda armadura.", 280, Atk(140, 2.3f, 5.6f, pen: 20), 2.1f),
                Up("Virote Pesado", "Feito para derrubar colossos: +60% contra elites e chefes, pode atordoar.", 420, Atk(190, 2.1f, 6.0f, pen: 22, boss: 1.6f, stun: 0.25f, stunFor: 0.6f), 2.4f)),
            Tower("pyre", "Pira Amaldiçoada", "Fogo em área · ignora armadura · fraca contra quem resiste ao fogo", "Bom contra hordas",
                120, TowerKind.Projectile, PlacementRule.OffRoad, 0.6f, 1.25f, TargetPriority.First, p["fire"], new Color(0.8f, 0.4f, 1f), 2.4f, "pyre_cast", "fire_impact",
                Atk(16, 1.8f, 3.3f, DamageType.Fire, splash: 1.2f),
                Up("Óleo de Cadáver", "As chamas grudam: inimigos continuam queimando.", 170, Atk(18, 1.7f, 3.4f, DamageType.Fire, splash: 1.25f, burn: 7, burnFor: 3), 1.7f),
                Up("Chamas Negras", "A explosão cresce: área muito maior.", 260, Atk(24, 1.6f, 3.6f, DamageType.Fire, splash: 1.75f, burn: 9, burnFor: 3), 2.3f),
                Up("Inferno do Eclipse", "Cada impacto deixa o chão em chamas por alguns segundos.", 380, Atk(30, 1.5f, 3.8f, DamageType.Fire, splash: 1.9f, burn: 12, burnFor: 3, fire: 18, fireFor: 4, fireR: 1.3f), 2.6f)),
            Tower("chapel", "Capela de Guerra", "Suporte · fortalece torres próximas · cura o comandante na aura", "Multiplica torres vizinhas",
                130, TowerKind.Aura, PlacementRule.OffRoad, 0.65f, 1.4f, TargetPriority.First, null, new Color(1f, 0.82f, 0.5f), 2.2f, "", "",
                Aura(3.0f, dmg: 0.15f, heal: 5),
                Up("Cânticos de Batalha", "As torres na aura atacam mais rápido.", 180, Aura(3.2f, dmg: 0.18f, speed: 0.12f, heal: 7), 1.5f),
                Up("Relicário dos Mártires", "Ossos santos: alcance maior para as torres.", 260, Aura(3.5f, dmg: 0.22f, speed: 0.15f, range: 0.10f, heal: 10), 2.0f),
                Up("Marca da Heresia", "Inimigos na aura perdem armadura.", 380, Aura(3.8f, dmg: 0.26f, speed: 0.18f, range: 0.12f, shred: 5, heal: 14), 2.5f)),
            Tower("crow", "Torre do Corvo", "Debuff · amaldiçoa: mais dano sofrido e lentidão", "Bom contra chefes e blindados (prepara as outras torres)",
                110, TowerKind.Projectile, PlacementRule.OffRoad, 0.55f, 1.6f, TargetPriority.Elite, p["crow"], new Color(0.6f, 0.3f, 0.9f), 1.6f, "crow_curse", "crow_impact",
                Atk(6, 1.2f, 3.8f, DamageType.Magic, vuln: 0.15f, curseFor: 4, slow: 0.15f, slowFor: 2),
                Up("Revoada", "Dois corvos por vez, maldição mais funda.", 150, Atk(7, 1.1f, 3.9f, DamageType.Magic, targets: 2, vuln: 0.18f, curseFor: 4, slow: 0.2f, slowFor: 2), 1.9f),
                Up("Olho do Abismo", "A maldição corrói a armadura.", 230, Atk(8, 1.0f, 4.1f, DamageType.Magic, targets: 2, vuln: 0.22f, curseFor: 4.5f, shred: 6, slow: 0.25f, slowFor: 2), 2.2f),
                Up("Praga dos Corvos", "Três alvos; a maldição pula para os vizinhos quando o amaldiçoado morre.", 340, Atk(9, 0.95f, 4.3f, DamageType.Magic, targets: 3, vuln: 0.3f, curseFor: 5, shred: 8, spread: true, slow: 0.3f, slowFor: 2.2f), 2.8f)),
            Tower("obelisk", "Obelisco de Sangue", "Caro · rajada mágica que salta entre inimigos", "Bom contra grupos densos e elites (dano mágico ignora armadura)",
                200, TowerKind.Chain, PlacementRule.OffRoad, 0.6f, 1.6f, TargetPriority.First, p["blood"], new Color(1f, 0.2f, 0.25f), 2.0f, "obelisk_cast", "",
                Atk(60, 2.4f, 3.9f, DamageType.Magic, chain: 1, chainR: 2.2f, falloff: 0.65f),
                Up("Sangue que Salta", "A rajada salta entre até 3 inimigos.", 240, Atk(70, 2.2f, 4.0f, DamageType.Magic, chain: 3, chainR: 2.3f, falloff: 0.7f), 2.1f),
                Up("Hemorragia Rúnica", "Mais saltos, mais dano, e os atingidos sangram.", 330, Atk(90, 2.0f, 4.1f, DamageType.Magic, chain: 4, chainR: 2.4f, falloff: 0.7f, bleed: 8, bleedFor: 3), 2.5f),
                Up("Sacrifício", "Executa inimigos comuns abaixo de 15% de vida.", 480, Atk(115, 1.9f, 4.4f, DamageType.Magic, chain: 5, chainR: 2.5f, falloff: 0.75f, bleed: 10, bleedFor: 3, execute: 0.15f, crit: 0.15f, critX: 2f), 3.0f)),
            Tower("spikes", "Armadilha de Estacas", "Armada SOBRE a estrada · fere, faz sangrar e atrasa quem passa · se desgasta a cada pisada", "Bom contra hordas e rápidos",
                60, TowerKind.Trap, PlacementRule.OnRoad, 0.5f, 0.2f, TargetPriority.First, null, none, 0, "trap_trigger", "",
                Atk(22, 0.9f, 0.6f, pen: 2, bleed: 4, bleedFor: 3, slow: 0.3f, slowFor: 1.2f, durability: 45),
                Up("Estacas Farpadas", "Ferimentos que não fecham; estacas novas e mais resistentes.", 90, Atk(26, 0.9f, 0.6f, pen: 2, bleed: 7, bleedFor: 3, slow: 0.3f, slowFor: 1.2f, durability: 65), 0.2f),
                Up("Fosso de Lanças", "Mais dano, segura muito mais e aguenta mais pisadas.", 150, Atk(34, 0.85f, 0.65f, pen: 4, bleed: 7, bleedFor: 3, slow: 0.45f, slowFor: 1.5f, durability: 90), 0.2f),
                Up("Empalador", "Chance de empalar (atordoa); ferro negro que quase não gasta.", 240, Atk(44, 0.8f, 0.7f, pen: 5, bleed: 10, bleedFor: 3, slow: 0.5f, slowFor: 1.5f, stun: 0.2f, stunFor: 1f, durability: 130), 0.2f)),
        };
    }

    // ---------------------------------------------------------------- enemies

    static EnemyData Enemy(string id, string name, string rig, string desc, float hp, float armor, float phys, float fire, float magic, float bleed,
        float speed, float radius, int fortress, float melee, float interval, int gold, EnemyTag tags, float scale = 1)
    {
        var e = Asset<EnemyData>($"{D}/Enemies/{id}.asset");
        e.id = id;
        e.displayName = name;
        e.description = desc;
        e.rig = AssetDatabase.LoadAssetAtPath<GameObject>($"{Anim}/{rig}/{rig}.prefab");
        e.icon = S($"{Anim}/{rig}/preview/icon.png");
        e.rigScale = scale;
        e.maxHp = hp;
        e.armor = armor;
        e.physicalResistance = phys;
        e.fireResistance = fire;
        e.magicResistance = magic;
        e.bleedResistance = bleed;
        e.movementSpeed = speed;
        e.bodyRadius = radius;
        e.damageToFortress = fortress;
        e.meleeDamage = melee;
        e.attackInterval = interval;
        e.goldReward = gold;
        e.tags = tags;
        e.castInterval = 0;
        e.splitInto = null;
        e.splitCount = 0;
        e.feedHeal = 0;
        e.regenPerSecond = 0;
        e.boss = null;
        e.lightRadius = 0;
        e.attackSfx = "enemy_attack";
        e.hitSfx = "enemy_hit";
        e.deathSfx = "enemy_death";
        e.spawnSfx = "";
        return e;
    }

    static (Dictionary<string, EnemyData>, Dictionary<string, BossData>) Enemies()
    {
        const EnemyTag U = EnemyTag.Undead;
        var e = new Dictionary<string, EnemyData>
        {
            ["soldier"] = Enemy("CursedSoldier", "Soldado Amaldiçoado", "CursedSoldier", "Morto-vivo de escudo. Vem em massa; queima fácil.",
                175, 3, 0, -0.25f, 0, 0.2f, 0.95f, 0.3f, 1, 10, 1.3f, 5, EnemyTag.Horde | U),
            ["hound"] = Enemy("HellHound", "Cão Infernal", "HellHound", "Rápido e frágil; o fogo do inferno não o queima.",
                70, 0, 0, 0.5f, 0, 0, 2.2f, 0.34f, 1, 6, 0.7f, 3, EnemyTag.Fast | EnemyTag.Beast),
            ["hooded"] = Enemy("Hooded", "Encapuzado", "Hooded", "Elite conjurador: protege os aliados com escudos sombrios. Resiste ao fogo.",
                220, 1, -0.2f, 0.6f, 0.3f, 0, 1.1f, 0.3f, 2, 14, 1.2f, 9, EnemyTag.Elite | EnemyTag.Caster | U),
            ["knight"] = Enemy("BlackKnight", "Cavaleiro Negro", "BlackKnight", "Armadura pesada: flechas mal arranham.",
                860, 14, 0, 0, 0, 0.3f, 0.7f, 0.42f, 3, 32, 1.5f, 22, EnemyTag.Armored | EnemyTag.Elite),
            ["cursedknight"] = Enemy("CursedKnight", "Cavaleiro Amaldiçoado", "CursedKnight", "Chefe. Ergue os mortos, atordoa torres com golpes sísmicos e enfurece ferido.",
                4900, 12, 0.3f, -0.15f, 0, 0.5f, 0.5f, 0.9f, 10, 110, 2.0f, 250, EnemyTag.Boss | EnemyTag.Armored | U),
            ["ghoul"] = Enemy("Ghoul", "Carniçal", "Ghoul", "Devora quem morre por perto e se regenera. Sangra muito.",
                210, 2, 0, -0.3f, 0, -0.3f, 1.25f, 0.38f, 1, 14, 0.9f, 6, EnemyTag.Beast | EnemyTag.Undead | EnemyTag.Horde),
            ["warlock"] = Enemy("CorruptWarlock", "Bruxo Corrompido", "CorruptWarlock", "Conjurador: escuda e cura os aliados. Resiste a magia.",
                220, 0, -0.25f, 0, 0.6f, 0, 0.95f, 0.3f, 2, 10, 1.4f, 12, EnemyTag.Caster | EnemyTag.Elite),
            ["slime"] = Enemy("SlimeAberration", "Aberração do Lodo", "SlimeAberration", "Absorve golpes físicos e se divide ao morrer. Queima bem.",
                520, 0, 0.45f, -0.35f, 0, 0.8f, 0.65f, 0.45f, 2, 18, 1.6f, 14, EnemyTag.Horde),
            ["slimelet"] = Enemy("LesserSlime", "Lodo Menor", "SlimeAberration", "Pedaço da aberração.",
                120, 0, 0.3f, -0.35f, 0, 0.8f, 1.0f, 0.28f, 1, 8, 1.2f, 2, EnemyTag.Horde, 0.6f),
            ["guardian"] = Enemy("SwampGuardian", "Guardião do Pântano", "SwampGuardian", "Chefe. Raízes que atordoam torres, carniçais famintos e fúria.",
                6400, 8, 0.15f, 0.25f, -0.2f, 0.3f, 0.48f, 0.95f, 10, 130, 2.2f, 300, EnemyTag.Boss | EnemyTag.Beast),
        };
        // body ellipses (half-width across the road, half-length along it) sized to the sprites
        foreach (var (k, w, l) in new[] { ("soldier", 0.33f, 0.33f), ("hound", 0.3f, 0.72f), ("hooded", 0.31f, 0.31f), ("knight", 0.45f, 0.45f),
                     ("ghoul", 0.38f, 0.82f), ("warlock", 0.31f, 0.31f), ("slime", 0.5f, 0.5f), ("slimelet", 0.3f, 0.3f), ("cursedknight", 0.9f, 0.9f), ("guardian", 0.95f, 0.95f) })
        {
            e[k].bodyRadius = w;
            e[k].bodyLength = l;
        }
        e["hooded"].castInterval = 8;
        e["hooded"].castRadius = 2.0f;
        e["hooded"].castShield = 35;
        e["hound"].spawnSfx = "hound_growl";
        e["hound"].attackSfx = "hound_bite";
        e["ghoul"].feedHeal = 18;
        e["ghoul"].feedRadius = 1.5f;
        e["ghoul"].regenPerSecond = 1.0f;
        e["ghoul"].meleeDamage = 11;
        e["ghoul"].attackSfx = "hound_bite";
        e["warlock"].castInterval = 7.5f;
        e["warlock"].castRadius = 2.0f;
        e["warlock"].castShield = 45;
        e["warlock"].castHeal = 20;
        e["slime"].splitInto = e["slimelet"];
        e["slime"].splitCount = 2;
        e["slime"].deathSfx = "slime_death";
        e["slimelet"].deathSfx = "slime_death";
        e["cursedknight"].lightColor = new Color(0.6f, 0.25f, 0.9f);
        e["cursedknight"].lightRadius = 4.5f;
        e["guardian"].lightColor = new Color(0.4f, 0.85f, 0.3f);
        e["guardian"].lightRadius = 4.5f;
        foreach (var k in new[] { "cursedknight", "guardian" })
        {
            e[k].attackSfx = "boss_attack";
            e[k].hitSfx = "";
            e[k].deathSfx = "";
        }

        var b1 = Asset<BossData>($"{D}/Bosses/CursedKnight.asset");
        b1.title = "O Cavaleiro Amaldiçoado";
        b1.introLine = "O chão racha sob o aço amaldiçoado.";
        b1.introSeconds = 2.2f;
        b1.introCameraShake = 0.15f;
        b1.summon = e["soldier"];
        b1.summonCount = 2;
        b1.summonInterval = 14;
        b1.enrageBelowHp = 0.4f;
        b1.enrageSpeedMultiplier = 1.45f;
        b1.enrageDamageMultiplier = 1.3f;
        b1.specialName = "Golpe Sísmico";
        b1.specialInterval = 11;
        b1.specialRadius = 2.4f;
        b1.specialDamage = 120;
        b1.specialStunSeconds = 2.5f;
        b1.fortressExecution = false;
        e["cursedknight"].boss = b1;

        var b2 = Asset<BossData>($"{D}/Bosses/SwampGuardian.asset");
        b2.title = "O Guardião do Pântano";
        b2.introLine = "As águas negras se abrem.";
        b2.introSeconds = 2.4f;
        b2.introCameraShake = 0.15f;
        b2.summon = e["ghoul"];
        b2.summonCount = 2;
        b2.summonInterval = 16;
        b2.enrageBelowHp = 0.35f;
        b2.enrageSpeedMultiplier = 1.4f;
        b2.enrageDamageMultiplier = 1.25f;
        b2.specialName = "Raízes do Pântano";
        b2.specialInterval = 12;
        b2.specialRadius = 2.8f;
        b2.specialDamage = 90;
        b2.specialStunSeconds = 3f;
        b2.fortressExecution = false;
        e["guardian"].boss = b2;
        foreach (var x in e.Values) EditorUtility.SetDirty(x);
        return (e, new Dictionary<string, BossData> { ["cursedknight"] = b1, ["guardian"] = b2 });
    }

    // ---------------------------------------------------------------- waves

    static SpawnEntry E(EnemyData e, int n) => new() { enemy = e, count = n };

    static SpawnGroup G(float interval, float delay, params SpawnEntry[] entries) => new() { entries = entries.ToList(), interval = interval, delay = delay, path = -1 };

    static SpawnGroup Hard(int tier, SpawnGroup g)
    {
        g.minRunTier = tier;
        g.label = "Etapa " + (tier + 1) + "+";
        return g;
    }

    static WaveData Wave(string map, int n, string title, params SpawnGroup[] groups)
    {
        var w = Asset<WaveData>($"{D}/Waves/{map}/Wave{n:00}.asset");
        w.title = title;
        w.groups = groups.ToList();
        for (int i = 0; i < w.groups.Count; i++)
            if (string.IsNullOrEmpty(w.groups[i].label)) w.groups[i].label = "Grupo " + (i + 1);
        return w;
    }

    static List<WaveData> Map1Waves(Dictionary<string, EnemyData> e)
    {
        var S = e["soldier"]; var D_ = e["hound"]; var H = e["hooded"]; var K = e["knight"]; var B = e["cursedknight"];
        return new List<WaveData>
        {
            Wave("Map1", 1, "Os Primeiros Mortos", G(1.4f, 0, E(S, 8)), G(1.2f, 6, E(S, 6))),
            Wave("Map1", 2, "Carne Fresca", G(1.1f, 0, E(S, 10)), G(0.6f, 5, E(D_, 6)), G(1.0f, 4, E(S, 8))),
            Wave("Map1", 3, "A Matilha", G(0.5f, 0, E(D_, 10)), G(0.9f, 5, E(S, 10), E(D_, 4)), G(0.4f, 4, E(D_, 12))),
            Wave("Map1", 4, "Os Encapuzados", G(0.9f, 0, E(S, 12)), G(1.2f, 4, E(H, 3), E(S, 6)), G(0.5f, 5, E(D_, 10))),
            Wave("Map1", 5, "Aço Negro", G(0.85f, 0, E(S, 12)), G(2.0f, 5, E(K, 2), E(S, 4)), G(1.3f, 5, E(H, 3)), G(0.5f, 5, E(D_, 12)),
                Hard(1, G(2.5f, 3, E(K, 2)))),
            Wave("Map1", 6, "Maré de Ossos", G(0.38f, 0, E(S, 34)), G(0.5f, 6, E(D_, 14), E(H, 4)), G(0.7f, 4, E(S, 16), E(K, 2)),
                Hard(2, G(0.6f, 3, E(H, 6), E(K, 2)))),
            Wave("Map1", 7, "Uivos na Névoa", G(0.35f, 0, E(D_, 20)), G(0.6f, 3, E(S, 16), E(H, 4)), G(0.3f, 5, E(D_, 24)),
                Hard(1, G(1.6f, 3, E(K, 3)))),
            Wave("Map1", 8, "Muralha de Ferro", G(3.2f, 0, E(K, 3)), G(0.6f, 4, E(S, 18), E(H, 5)), G(1.0f, 5, E(K, 3), E(D_, 10)),
                Hard(1, G(0.8f, 3, E(H, 6)))),
            Wave("Map1", 9, "A Horda", G(0.28f, 0, E(S, 46)), G(0.4f, 4, E(D_, 20), E(H, 6)), G(1.8f, 4, E(K, 6)),
                Hard(1, G(1.5f, 3, E(K, 4))), Hard(2, G(0.35f, 2, E(D_, 20), E(H, 6)))),
            Wave("Map1", 10, "O Cavaleiro Amaldiçoado", G(0.5f, 0, E(S, 18), E(H, 4)), G(0.4f, 4, E(D_, 12)), G(1, 6, E(B, 1)),
                G(0.6f, 10, E(S, 20), E(K, 2)), G(0.45f, 8, E(D_, 16), E(H, 4)),
                Hard(1, G(1.0f, 4, E(K, 4), E(H, 4)))),
        };
    }

    static List<WaveData> Map2Waves(Dictionary<string, EnemyData> e)
    {
        var Gh = e["ghoul"]; var D_ = e["hound"]; var W = e["warlock"]; var A = e["slime"]; var B = e["guardian"];
        return new List<WaveData>
        {
            Wave("Map2", 1, "Lama e Dentes", G(1.6f, 0, E(Gh, 6)), G(0.7f, 6, E(D_, 6))),
            Wave("Map2", 2, "Fome", G(1.1f, 0, E(Gh, 9)), G(0.6f, 5, E(D_, 6)), G(1.1f, 5, E(Gh, 6))),
            Wave("Map2", 3, "Lodo Vivo", G(3.0f, 0, E(A, 3)), G(0.7f, 5, E(Gh, 8), E(D_, 6))),
            Wave("Map2", 4, "Os Bruxos", G(1.0f, 0, E(W, 2), E(Gh, 8)), G(0.5f, 5, E(D_, 10)), G(2.5f, 5, E(A, 3))),
            Wave("Map2", 5, "Raízes Famintas", G(0.7f, 0, E(Gh, 14)), G(1.6f, 4, E(W, 3), E(A, 3)), G(0.35f, 4, E(D_, 16)),
                Hard(1, G(1.2f, 3, E(W, 3), E(A, 2)))),
            Wave("Map2", 6, "Pântano em Fúria", G(2.0f, 0, E(A, 5)), G(0.65f, 5, E(Gh, 14), E(W, 2)), G(0.35f, 5, E(D_, 14)),
                Hard(2, G(0.6f, 3, E(W, 4), E(Gh, 10)))),
            Wave("Map2", 7, "Névoa Verde", G(0.7f, 0, E(W, 5), E(Gh, 12)), G(0.8f, 4, E(A, 6), E(D_, 10)),
                Hard(1, G(1.4f, 3, E(A, 4)))),
            Wave("Map2", 8, "A Grande Carniça", G(0.4f, 0, E(Gh, 30)), G(1.2f, 4, E(W, 4), E(A, 4)), G(0.3f, 4, E(D_, 20)),
                Hard(1, G(0.7f, 3, E(W, 4), E(Gh, 10)))),
            Wave("Map2", 9, "O Lodo Sobe", G(1.2f, 0, E(A, 10)), G(0.5f, 4, E(W, 6), E(Gh, 16)), G(0.3f, 4, E(D_, 24), E(Gh, 10)),
                Hard(1, G(1.0f, 3, E(A, 6))), Hard(2, G(0.5f, 2, E(W, 6), E(D_, 16)))),
            Wave("Map2", 10, "O Guardião do Pântano", G(0.6f, 0, E(Gh, 16), E(W, 4)), G(1.2f, 5, E(A, 6)), G(1, 6, E(B, 1)),
                G(0.4f, 10, E(Gh, 20), E(D_, 12)), G(1.0f, 8, E(W, 4), E(A, 4)),
                Hard(1, G(0.8f, 4, E(A, 4), E(W, 4)))),
        };
    }

    // ---------------------------------------------------------------- maps + routes

    static List<RouteData> Routes(string mapKey, string folder)
    {
        var json = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText("Assets/Editor/Content/routes.json"));
        var list = new List<RouteData>();
        foreach (Dictionary<string, object> r in (List<object>)json[mapKey])
        {
            var route = Asset<RouteData>($"{D}/Routes/{folder}/{(string)r["id"]}.asset");
            route.displayName = (string)r["name"];
            route.layout = (string)r["layout"];
            route.roadHalfWidth = F(r["half"]);
            route.fortressPosition = V(r["fortress"]);
            route.commanderStart = V(r["commander"]);
            route.decorSeed = (int)F(r["seed"]);
            route.decorDensity = F(r["density"]);
            route.paths = ((List<object>)r["paths"]).Select(p => new RoutePath { points = ((List<object>)p).Select(V).ToList() }).ToList();
            route.zones = ((List<object>)r["zones"]).Select(o =>
            {
                var z = (Dictionary<string, object>)o;
                return new MapZone { kind = (ZoneKind)System.Enum.Parse(typeof(ZoneKind), (string)z["kind"]), center = V(z["c"]), radius = F(z["r"]), reason = (string)z["reason"] };
            }).ToList();
            route.props = ((List<object>)r["props"]).Select(o =>
            {
                var p = (Dictionary<string, object>)o;
                return new PropPlacement
                {
                    prop = (string)p["p"], position = V(p["at"]),
                    block = !p.ContainsKey("block") || (bool)p["block"], flip = p.ContainsKey("flip") && (bool)p["flip"],
                };
            }).ToList();
            EditorUtility.SetDirty(route);
            list.Add(route);
        }
        return list;
    }

    static PropSprite Prop(string name, string path, float scatter = 0, bool decal = false, bool torch = false, Color? light = null) => new()
    {
        name = name, sprite = S(path), scatterWeight = scatter, decal = decal, torch = torch, lightColor = light ?? new Color(1f, 0.6f, 0.28f),
    };

    static Sprite[] Tiles(string folder, string prefix, int n) =>
        Enumerable.Range(0, n).Select(i => S($"{Art}/Tiles/{folder}/{prefix}_{i}.png")).Where(s => s).ToArray();

    static List<MapData> Maps(Dictionary<string, EnemyData> e)
    {
        var p1 = $"{Art}/Sprites/Props/Map1";
        var p2 = $"{Art}/Sprites/Props/Map2";
        var m1 = Asset<MapData>($"{D}/Maps/Map1_EstradaDosCondenados.asset");
        m1.id = "map1";
        m1.displayName = "Estrada dos Condenados";
        m1.description = "Ruínas medievais, cemitérios e uma estrada de pedra que leva direto aos portões. Mortos de escudo, cães do inferno, encapuzados e cavaleiros negros.";
        m1.routes = Routes("map1", "Map1");
        m1.waves = Map1Waves(e);
        m1.roster = new List<EnemyData> { e["soldier"], e["hound"], e["hooded"], e["knight"], e["cursedknight"] };
        m1.startingGold = 285;
        m1.fortressHp = 20;
        m1.waveClearBonus = 20;
        m1.waveClearBonusPerWave = 5;
        m1.groundTiles = Tiles("Map1", "dirt", 9);
        m1.groundAccentTiles = new Sprite[0];
        m1.roadTiles = Tiles("Map1", "cobble", 9);
        m1.roadAccentTiles = Tiles("Map1", "cobble_broken", 2);
        m1.waterTiles = new Sprite[0];
        m1.bridgeTiles = new Sprite[0];
        m1.props = new List<PropSprite>
        {
            Prop("ruin_wall", $"{p1}/ruin_wall.png", 0.6f), Prop("cross_grave", $"{p1}/cross_grave.png", 1.2f), Prop("bones", $"{p1}/bones.png", 1.6f, decal: true),
            Prop("torch_post", $"{p1}/torch_post.png", 0.3f, torch: true), Prop("dead_tree", $"{p1}/dead_tree.png", 1.0f), Prop("rock", $"{p1}/rock.png", 1.0f),
            Prop("corruption_pit", $"{p1}/corruption_pit.png", 0, decal: true), Prop("spawn_portal", $"{p1}/corruption_pit.png", 0, decal: true),
            Prop("ruin_0", $"{p1}/ruin_0.png", 0.3f), Prop("ruin_1", $"{p1}/ruin_1.png", 0.2f), Prop("ruin_2", $"{p1}/ruin_2.png", 0.4f),
            Prop("torch_0", $"{p1}/torch_0.png", 0, torch: true), Prop("torch_1", $"{p1}/torch_1.png", 0, torch: true),
            Prop("banner_raven", $"{p1}/banner_raven.png"), Prop("banner_skull", $"{p1}/banner_skull.png"), Prop("shrine", $"{p1}/shrine.png", 0.15f),
        };
        m1.fortressKeep = S($"{Art}/Sprites/Buildings/fortress_keep.png");
        m1.fortressTower = S($"{Art}/Sprites/Buildings/fortress_watchtower.png");
        m1.globalLightColor = new Color(0.78f, 0.8f, 1f);
        m1.globalLightIntensity = 0.78f;
        m1.backgroundColor = new Color32(14, 11, 12, 255);
        m1.musicId = "music_map1";
        m1.ambientId = "ambient_map1";

        var m2 = Asset<MapData>($"{D}/Maps/Map2_PantanoDasCinzas.asset");
        m2.id = "map2";
        m2.displayName = "Pântano das Cinzas";
        m2.description = "Lama, água negra e madeira podre. Carniçais famintos, bruxos que protegem a horda e aberrações de lodo que se dividem.";
        m2.routes = Routes("map2", "Map2");
        m2.waves = Map2Waves(e);
        m2.roster = new List<EnemyData> { e["ghoul"], e["hound"], e["warlock"], e["slime"], e["guardian"] };
        m2.startingGold = 340;
        m2.fortressHp = 20;
        m2.waveClearBonus = 22;
        m2.waveClearBonusPerWave = 5;
        m2.groundTiles = Tiles("Map2", "roots", 4);
        m2.groundAccentTiles = Tiles("Map2", "mud", 2);
        m2.roadTiles = Tiles("Map2", "mud", 4);
        m2.roadAccentTiles = new Sprite[0];
        m2.waterTiles = Tiles("Map2", "water", 4);
        m2.bridgeTiles = Tiles("Map2", "planks", 4);
        m2.roadRim = new Color(0.08f, 0.1f, 0.06f);
        m2.props = new List<PropSprite>
        {
            Prop("reeds_0", $"{p2}/reeds_0.png", 1.4f), Prop("reeds_1", $"{p2}/reeds_1.png", 1.4f),
            Prop("bonepile_0", $"{p2}/bonepile_0.png", 0.8f, decal: true), Prop("bonepile_1", $"{p2}/bonepile_1.png", 0.8f, decal: true),
            Prop("slime_pool", $"{p2}/slime_pool.png", 0.5f, decal: true), Prop("mossy_rock", $"{p2}/mossy_rock.png", 0.5f),
            Prop("mushrooms", $"{p2}/mushrooms.png", 1.0f, light: new Color(0.5f, 1f, 0.4f)), Prop("stump", $"{p2}/stump.png", 0.8f),
            Prop("totem", $"{p2}/totem.png", 0.25f, torch: true, light: new Color(1f, 0.25f, 0.2f)),
            Prop("spawn_portal", $"{p2}/slime_pool.png", 0, decal: true),
            Prop("dead_tree", $"{p1}/dead_tree.png", 0.6f),
        };
        m2.fortressKeep = S($"{Art}/Sprites/Buildings/fortress_keep.png");
        m2.fortressTower = S($"{Art}/Sprites/Buildings/fortress_watchtower.png");
        m2.globalLightColor = new Color(0.72f, 0.88f, 0.78f);
        m2.globalLightIntensity = 0.74f;
        m2.backgroundColor = new Color32(8, 12, 10, 255);
        m2.musicId = "music_map2";
        m2.ambientId = "ambient_map2";
        foreach (var m in new[] { m1, m2 })
        {
            m.preview = null;
            EditorUtility.SetDirty(m);
        }
        return new List<MapData> { m1, m2 };
    }

    // ---------------------------------------------------------------- commander, skins, meta

    static CommanderData Commander(UISkin ui)
    {
        var c = Asset<CommanderData>($"{D}/Commander/Commander.asset");
        c.displayName = "Ulric";
        c.title = "Capitão da Companhia do Corvo";
        c.ultimateName = "Fúria Negra";
        c.ultimateDescription = "Gira a montante em volta de si: dano pesado em área e atordoa inimigos comuns; depois, 8 s de fúria (golpes mais rápidos e fortes, menos dano recebido).";
        c.ultimateCooldown = 45f;
        c.ultimateStartCharge = 0.5f;
        c.ultimateRadius = 2.4f;
        c.ultimateDamage = 160f;
        c.ultimateArmorPenetration = 10f;
        c.ultimateStunSeconds = 1.4f;
        c.ultimateBuffSeconds = 8f;
        c.ultimateAttackSpeedBonus = 0.4f;
        c.ultimateDamageBonus = 0.25f;
        c.ultimateDamageReduction = 0.3f;
        c.maxHp = 700;
        c.damage = 38;
        c.armorPenetration = 4;
        c.attackInterval = 0.95f;
        c.reach = 1.3f;
        c.cleave = 3;
        c.blockCount = 3;
        c.armor = 3;
        c.regenPerSecond = 1;
        c.speed = 3.1f;
        c.tacticsRadius = 3.2f;
        c.hitReactCooldown = 0.5f;
        c.respawnSeconds = 14f;
        c.respawnHealth = 1f;
        AttributeDef A(CommanderAttribute id, string name, string desc, float per, Sprite icon) =>
            new() { id = id, displayName = name, description = desc, perPoint = per, maxLevel = 10, baseCost = 20, costPerLevel = 15, icon = icon };
        c.attributes = new List<AttributeDef>
        {
            A(CommanderAttribute.Strength, "Força", "Dano físico da espada.", 0.06f, ui.axe),
            A(CommanderAttribute.Vigor, "Vigor", "Vida máxima.", 0.08f, ui.heart),
            A(CommanderAttribute.Fury, "Fúria", "Velocidade de ataque.", 0.05f, ui.fire),
            A(CommanderAttribute.Discipline, "Disciplina", "Armadura: menos dano por golpe.", 1.5f, ui.armor),
            A(CommanderAttribute.Tactics, "Tática", "Torres perto dele causam mais dano.", 0.025f, ui.archer),
            A(CommanderAttribute.DarkFaith, "Fé Sombria", "Regeneração de vida em batalha.", 0.6f, ui.magic),
        };
        EditorUtility.SetDirty(c);
        return c;
    }

    static Sprite[] Frames(string rig, string clip) =>
        Directory.Exists($"{Anim}/{rig}/preview")
            ? Directory.GetFiles($"{Anim}/{rig}/preview", clip + "_*.png").OrderBy(f => f).Select(f => S(f.Replace('\\', '/'))).Where(s => s).ToArray()
            : new Sprite[0];

    static List<CommanderSkinData> Skins()
    {
        CommanderSkinData Skin(string id, string name, string desc, string rig, int cost, bool free, Color accent)
        {
            var s = Asset<CommanderSkinData>($"{D}/Skins/{id}.asset");
            s.id = id;
            s.displayName = name;
            s.description = desc;
            s.rig = AssetDatabase.LoadAssetAtPath<GameObject>($"{Anim}/{rig}/{rig}.prefab");
            s.rigBack = AssetDatabase.LoadAssetAtPath<GameObject>($"{Anim}/{rig}Back/{rig}Back.prefab");
            s.splash = S($"{Anim}/{rig}/splash.png");
            s.portrait = S($"{Anim}/{rig}/portrait.png");
            s.previewIdle = Frames(rig, "idle");
            s.previewAttack = Frames(rig, "attack");
            s.unlockCost = cost;
            s.unlockedByDefault = free;
            s.accent = accent;
            EditorUtility.SetDirty(s);
            return s;
        }
        return new List<CommanderSkinData>
        {
            Skin("base", "Ulric", "Capitão da Companhia do Corvo: o mercenário que se recusou a abandonar o último bastião. Placas enegrecidas, manto rubro, espada maior que a fé.", "Commander", 0, true, new Color(0.95f, 0.64f, 0.23f)),
            Skin("black_swordsman", "Espadachim Negro", "Um mercenário sem nome, encapuzado, com uma lâmina larga às costas e cicatrizes que não contam histórias.", "BlackSwordsman", 150, false, new Color(0.7f, 0.7f, 0.8f)),
            Skin("scarlet_veteran", "Veterano Escarlate", "Armadura laqueada de vermelho e manto cor de cinza: sobreviveu a cem cercos.", "CommanderScarlet", 100, false, new Color(1f, 0.35f, 0.3f)),
            Skin("eclipse_knight", "Cavaleiro do Eclipse", "Aço negro tingido de violeta pela noite em que o sol morreu.", "CommanderEclipse", 250, false, new Color(0.65f, 0.35f, 1f)),
        };
    }

    static MetaProgressionData Meta()
    {
        var m = Asset<MetaProgressionData>($"{D}/Meta/MetaProgression.asset");
        m.perKill = 0.1f;
        m.perEliteKill = 1f;
        m.perBossKill = 15f;
        m.perWaveCleared = 2f;
        m.mapVictory = 25f;
        m.perRunTierBonus = 0.25f;
        m.defeatMultiplier = 1f;      // nothing earned is lost on defeat
        m.replayMultiplier = 0.35f;   // a phase (map + stage) already won pays 35%
        m.goldCarryFraction = 0.25f;
        m.goldCarryMax = 150;
        m.transitionBonusPerTier = 40;
        m.enemyHpPerTier = 0.22f;
        m.enemyDamagePerTier = 0.15f;
        m.spawnCountPerTier = 0.12f;
        m.spawnIntervalPerTier = 0.08f;
        m.eliteChancePerTier = 0.06f;
        m.bossHpPerTier = 0.3f;
        m.bossExtraSummonsPerTier = 1;
        m.goldRewardPerTier = 0.05f;
        m.maxTier = 6;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------------------------------------------------------- helpers

    static float F(object o) => System.Convert.ToSingle(o, System.Globalization.CultureInfo.InvariantCulture);

    static Vector2 V(object o)
    {
        var l = (List<object>)o;
        return new Vector2(F(l[0]), F(l[1]));
    }

    public static T Asset<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!a)
        {
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
        }
        EditorUtility.SetDirty(a);
        return a;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, i));
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }

    static Sprite S(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (!s && File.Exists(path))
        {
            AssetDatabase.ImportAsset(path);
            s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return s;
    }

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.white;
}
