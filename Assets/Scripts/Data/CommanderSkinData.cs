using UnityEngine;

// A skin swaps the commander's look (rig prefab + portrait) and his [Q] power. Base stats and attributes never read it.
[CreateAssetMenu(menuName = "Bastião/Skin do Comandante")]
public class CommanderSkinData : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea] public string description;
    [Tooltip("Rig de frente (SE)")] public GameObject rig;
    [Tooltip("Rig de costas (NE), usado ao andar para cima da tela")] public GameObject rigBack;
    [Tooltip("Arte grande para os menus (pixel art em grade nativa, exibida a 3x)")] public Sprite splash;
    public Sprite portrait;
    [Tooltip("Quadros do rig renderizados para os menus")] public Sprite[] previewIdle;
    public Sprite[] previewAttack;
    public int unlockCost;
    public bool unlockedByDefault;
    public Color accent = new(0.95f, 0.64f, 0.23f);
    [Header("Poder [Q] desta skin")]
    public CommanderPower power;
    public string powerName = "Fúria Negra";
    [TextArea] public string powerDescription;
    public float powerCooldown = 45f;
}
