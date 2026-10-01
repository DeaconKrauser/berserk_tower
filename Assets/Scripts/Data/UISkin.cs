using UnityEngine;

// Sprites of the HUD/menus (Assets/Art/UI). Frames are 9-sliced pixel art shown at 2x.
[CreateAssetMenu(menuName = "Bastião/UI Skin")]
public class UISkin : ScriptableObject
{
    [Header("Molduras")]
    public Sprite panel, panelDark, panelSolid, panelThin, plate, card, cardSelected, cardDisabled, barFrame, divider, vignette;
    public Sprite buttonNormal, buttonHover, buttonPressed, buttonDisabled;
    public Sprite roundNormal, roundHover, roundPressed, roundActive;

    [Header("Ícones")]
    public Sprite gold, heart, wave, skull, boss, attack, archer, axe, armor, fire, magic, heal, sell, upgrade;

    [Header("Fundos")]
    public Sprite menuBackground, commanderBackground, skinsBackground, battlefieldBackground;

    [Header("Fontes do sistema (primeira encontrada)")]
    public string[] titleFonts = { "Castellar", "Felix Titling", "Palatino Linotype", "Georgia" };
    public string[] bodyFonts = { "Palatino Linotype", "Book Antiqua", "Georgia", "Times New Roman" };
}
