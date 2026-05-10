using UnityEngine;

public readonly struct MiningHit
{
    public readonly Transform Source;
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly float Damage;

    public MiningHit(Transform source, Vector3 point, Vector3 normal, float damage)
    {
        Source = source;
        Point = point;
        Normal = normal;
        Damage = damage;
    }
}