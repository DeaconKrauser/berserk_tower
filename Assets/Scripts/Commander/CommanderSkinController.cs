using UnityEngine;

// Skins change presentation (rig prefab, light accent) and the [Q] power (CommanderCombat). Stats never read the skin.
public static class CommanderSkinController
{
    public static RigView Spawn(Transform parent, CommanderSkinData skin) => SpawnViews(parent, skin).front;

    // Front rig always; back rig when the skin has one (shown while he walks up the screen).
    public static (RigView front, RigView back) SpawnViews(Transform parent, CommanderSkinData skin)
    {
        var front = RigView.Create(parent, skin ? skin.rig : null, 1f, "CommanderRig");
        RigView back = null;
        if (skin && skin.rigBack)
        {
            back = RigView.Create(parent, skin.rigBack, 1f, "CommanderRigBack");
            back.gameObject.SetActive(false);
        }
        if (skin)
        {
            var l = Gfx.Light(parent, new Vector2(0, 1.2f), skin.accent, 1.6f, 0.45f);
            l.name = "SkinAccent";
        }
        return (front, back);
    }

    public static CommanderSkinData Equipped(GameConfig config, SaveSystem save)
    {
        var s = config.Skin(save != null ? save.Data.equippedSkin : null);
        return save != null && !save.Data.HasSkin(s) ? config.skins.Find(x => x.unlockedByDefault) : s;
    }
}
