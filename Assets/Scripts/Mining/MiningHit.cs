using UnityEngine;

public enum MiningSourceType
{
    Unknown,
    Player,
    MiningRobot
}

public readonly struct MiningHit
{
    public readonly Transform Source;
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly float Damage;
    public readonly MiningSourceType SourceType;

    public MiningHit(
        Transform source,
        Vector3 point,
        Vector3 normal,
        float damage,
        MiningSourceType sourceType = MiningSourceType.Unknown)
    {
        Source = source;
        Point = point;
        Normal = normal;
        Damage = damage;
        SourceType = sourceType;
    }
}
