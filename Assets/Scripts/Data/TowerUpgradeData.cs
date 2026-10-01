using UnityEngine;

// One step of a tower's upgrade path: full stats of the new tier, its own sprite and price.
[CreateAssetMenu(menuName = "Bastião/Melhoria de Torre")]
public class TowerUpgradeData : ScriptableObject
{
    public string displayName;
    [TextArea] public string description;
    public int cost;
    [Tooltip("Visual do novo tier")] public Sprite sprite;
    public float muzzleHeight;
    public TowerStats stats;
}
