using UnityEngine;

public enum ImpactStyle { Spark, Heavy, Fire, Curse, Blood }

[CreateAssetMenu(menuName = "Bastião/Projétil")]
public class ProjectileData : ScriptableObject
{
    public Sprite sprite;
    [Tooltip("Unidades por segundo")] public float speed = 12f;
    [Tooltip("Altura do arco em unidades; 0 = trajetória reta")] public float arcHeight;
    [Tooltip("Persegue o alvo; senão cai no ponto previsto (bom para área)")] public bool homing = true;
    [Tooltip("Fica cravado no alvo por um instante")] public bool sticks;
    public bool spins;
    public Color impactColor = Color.white;
    public ImpactStyle impact;
    public Color trailColor = new(0, 0, 0, 0);
}
