namespace BrawlGuys.Core;

/// <summary>
/// 连发技能的运行时状态。
/// 这是通用战斗机制，因此保留在 Core 公共层，而不是塞进具体角色技能里。
/// </summary>
public sealed class BurstFireState
{
    /// <summary>
    /// 当前连发锁定的目标角色 Id。
    /// </summary>
    public required string TargetId { get; init; }

    /// <summary>
    /// 连发过程中使用的投掷物配置 Key。
    /// </summary>
    public required string ThrowableKey { get; init; }

    /// <summary>
    /// 剩余待发射次数。
    /// </summary>
    public required int ShotsRemaining { get; set; }

    /// <summary>
    /// 每发之间的时间间隔，单位秒。
    /// </summary>
    public required double ShotInterval { get; init; }

    /// <summary>
    /// 下一发剩余倒计时，单位秒。
    /// </summary>
    public required double ShotTimer { get; set; }

    /// <summary>
    /// 若不为空，则使用该伤害覆盖投掷物默认伤害。
    /// </summary>
    public double? DamageOverride { get; init; }

    /// <summary>
    /// 是否只有目标处于睡眠状态时才能造成伤害。
    /// </summary>
    public bool DealDamageOnlyIfTargetSleeping { get; init; }
}
