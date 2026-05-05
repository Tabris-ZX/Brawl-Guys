namespace BrawlGuys.Core;

/// <summary>
/// 飞行道具状态，例如酒瓶、棒球、子弹。
/// 这里只保留战斗模拟真正需要的字段，避免无效冗余信息继续堆积。
/// </summary>
public sealed class BattleProjectile
{
    /// <summary>
    /// 当前投掷物实例唯一 Id。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 发射者 Id。可能是角色，也可能是无人机。
    /// </summary>
    public required string OwnerId { get; init; }

    /// <summary>
    /// 预设目标角色 Id。
    /// 当前一对一战斗里主要用于锁定被攻击方。
    /// </summary>
    public required string TargetId { get; init; }

    /// <summary>
    /// 投掷物贴图逻辑路径。
    /// </summary>
    public required string TexturePath { get; init; }

    /// <summary>
    /// 当前世界坐标。
    /// </summary>
    public required Vec2 Position { get; set; }

    /// <summary>
    /// 当前速度向量。
    /// </summary>
    public required Vec2 Velocity { get; set; }

    /// <summary>
    /// 碰撞半径。
    /// </summary>
    public required double Radius { get; init; }

    /// <summary>
    /// 基础伤害值。
    /// </summary>
    public required double Damage { get; init; }

    /// <summary>
    /// 贴图缺失时使用的回退颜色，也是特效颜色来源。
    /// </summary>
    public required string ColorHex { get; init; }

    /// <summary>
    /// 是否能让目标进入睡眠。
    /// </summary>
    public bool CanSleepTarget { get; init; }

    /// <summary>
    /// 睡眠持续时间，单位秒。
    /// </summary>
    public double SleepDuration { get; init; }

    /// <summary>
    /// 是否只有目标已睡眠时才能造成伤害。
    /// </summary>
    public bool DealDamageOnlyIfTargetSleeping { get; init; }

    /// <summary>
    /// 是否允许碰墙反弹。
    /// </summary>
    public bool BounceOnWalls { get; init; }

    /// <summary>
    /// 是否允许被原主人回收。
    /// </summary>
    public bool CanBeReclaimedByOwner { get; init; }

    /// <summary>
    /// 被回收时转化为治疗量的比例。
    /// </summary>
    public double ReclaimHealRatio { get; init; }

    /// <summary>
    /// 命中单位后是否继续存在。
    /// </summary>
    public bool KeepOnFighterHit { get; init; }
}
