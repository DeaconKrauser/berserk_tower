using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Only what survives between runs: Essence, permanent commander attributes, skins and player settings.
// A run in progress is never written anywhere (there is no "Continue").
[Serializable]
public class MetaSave
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int essence;
    public int lifetimeEssence;
    public List<AttributeLevel> attributes = new();
    public List<string> unlockedSkins = new();
    [Tooltip("Fases já vencidas: \"<mapa>@<etapa>\". Repetir uma delas rende Essência reduzida.")]
    public List<string> clearedPhases = new();
    public string equippedSkin = "base";
    public Settings settings = new();
    public int runsStarted, mapsWon, bossesKilled;

    [Serializable]
    public class AttributeLevel
    {
        public CommanderAttribute id;
        public int level;
    }

    [Serializable]
    public class Settings
    {
        [Range(0, 1)] public float master = 0.9f, music = 0.6f, sfx = 0.8f, ui = 0.8f, ambient = 0.55f;
        public bool fullscreen = true;
        public bool screenShake = true;
        public bool showDamageNumbers = true;
        public int resolutionWidth, resolutionHeight;   // 0 = resolução nativa do monitor
        public bool autoWaves;                          // próxima onda começa sozinha após a contagem
        public int towerTargeting = -1;                 // alvo de todas as torres: -1 padrão de cada torre, senão TargetPriority
    }

    public int Level(CommanderAttribute id) => attributes.Find(a => a.id == id)?.level ?? 0;

    public void SetLevel(CommanderAttribute id, int level)
    {
        var a = attributes.Find(x => x.id == id);
        if (a == null) attributes.Add(a = new AttributeLevel { id = id });
        a.level = level;
    }

    public bool HasSkin(CommanderSkinData s) => s && (s.unlockedByDefault || unlockedSkins.Contains(s.id));
}

public class SaveSystem
{
    public MetaSave Data { get; private set; }
    public string Path { get; }
    public event Action Changed;

    public static string DefaultPath => System.IO.Path.Combine(Application.persistentDataPath, "meta.json");

    public SaveSystem(string path = null)
    {
        Path = path ?? DefaultPath;
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                Data = JsonUtility.FromJson<MetaSave>(File.ReadAllText(Path)) ?? new MetaSave();
                Migrate(Data);
                return;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveSystem: save ilegível ({e.Message}); mantendo cópia em .corrupt e começando do zero.");
            TryCopy(Path, Path + ".corrupt");
        }
        Data = new MetaSave();
    }

    static void Migrate(MetaSave d)
    {
        if (d.version > MetaSave.CurrentVersion)
            Debug.LogWarning($"SaveSystem: save de versão futura ({d.version}); lendo o que for compatível.");
        d.attributes ??= new List<MetaSave.AttributeLevel>();
        d.unlockedSkins ??= new List<string>();
        d.clearedPhases ??= new List<string>();
        d.settings ??= new MetaSave.Settings();
        if (string.IsNullOrEmpty(d.equippedSkin)) d.equippedSkin = "base";
        d.version = MetaSave.CurrentVersion;
    }

    // Write to a temp file and swap, so a crash mid-write never destroys the previous save.
    public void Save()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = Path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(Data, true));
            if (File.Exists(Path)) File.Replace(tmp, Path, null);
            else File.Move(tmp, Path);
        }
        catch (Exception e)
        {
            Debug.LogError($"SaveSystem: falha ao salvar: {e.Message}");
        }
        Changed?.Invoke();
    }

    public void AddEssence(int amount)
    {
        if (amount <= 0) return;
        Data.essence += amount;
        Data.lifetimeEssence += amount;
        Save();
    }

    public bool TryBuyAttribute(AttributeDef def)
    {
        int lv = Data.Level(def.id);
        int cost = def.CostFor(lv);
        if (lv >= def.maxLevel || Data.essence < cost) return false;
        Data.essence -= cost;
        Data.SetLevel(def.id, lv + 1);
        Save();
        return true;
    }

    public bool TryUnlockSkin(CommanderSkinData skin)
    {
        if (!skin || Data.HasSkin(skin) || Data.essence < skin.unlockCost) return false;
        Data.essence -= skin.unlockCost;
        Data.unlockedSkins.Add(skin.id);
        Save();
        return true;
    }

    public bool Equip(CommanderSkinData skin)
    {
        if (!Data.HasSkin(skin)) return false;
        Data.equippedSkin = skin.id;
        Save();
        return true;
    }

    static void TryCopy(string a, string b)
    {
        try { File.Copy(a, b, true); }
        catch { /* best effort */ }
    }
}
