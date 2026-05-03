namespace BrawlGuys.Core;

/// <summary>
/// 飞行道具状态，例如酒瓶、棒球。由技能生成，由 BattleWorld 统一移动和命中判定。
/// </summary>
public sealed class BattleProjectile
{
    public required string Id { get; init; }
    public required string OwnerId { get; init; }
    public required string TargetId { get; init; }
    public required string Name { get; init; }
    public required string TexturePath { get; init; }
    public required Vec2 Position { get; set; }
    public required Vec2 Velocity { get; set; }
    public required double Radius { get; init; }
    public required double Damage { get; init; }
    public required string ColorHex { get; init; }
    public double RemainingLifeTime { get; set; }
    public bool CanSleepTarget { get; init; }
    public double SleepDuration { get; init; }
    public bool DealDamageOnlyIfTargetSleeping { get; init; }
    public bool BounceOnWalls { get; init; }
    public bool CanBeReclaimedByOwner { get; init; }
    public double ReclaimHealRatio { get; init; }
    public bool KeepOnFighterHit { get; init; }
}
