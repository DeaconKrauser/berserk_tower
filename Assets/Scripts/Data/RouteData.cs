using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoutePath
{
    [Tooltip("Pontos de controle (spline Catmull-Rom); o último é a porta da fortaleza")]
    public List<Vector2> points = new();
}

[System.Serializable]
public class MapZone
{
    public ZoneKind kind;
    public Vector2 center;
    public float radius;
    [Tooltip("Mensagem mostrada ao tentar construir aqui")] public string reason;
}

[System.Serializable]
public class PropPlacement
{
    public string prop;
    public Vector2 position;
    [Tooltip("Bloqueia construção")] public bool block = true;
    [Tooltip("<= 0: calculado pelo tamanho do sprite")] public float blockRadius;
    public bool flip;
}

// One road layout of a map. The logical path is a spline; the visible road is painted on a Tilemap along it.
[CreateAssetMenu(menuName = "Bastião/Rota")]
public class RouteData : ScriptableObject
{
    public string displayName;
    [Tooltip("S, zig-zag, curva longa, dois corredores, entrada lateral, gargalos...")] public string layout;
    public List<RoutePath> paths = new();
    public float roadHalfWidth = 0.8f;
    public Vector2 fortressPosition;
    public Vector2 commanderStart;
    public List<MapZone> zones = new();
    public List<PropPlacement> props = new();
    [Tooltip("Semente da decoração espalhada")] public int decorSeed = 1;
    [Range(0, 1)] public float decorDensity = 0.5f;
}
