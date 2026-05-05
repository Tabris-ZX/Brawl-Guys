namespace BrawlGuys.Core;

/// <summary>
/// 召唤物运行时状态。
/// 例如无人机、炮台、分身这类可被场上维护和结算的单位，都可以复用这个模型。
/// </summary>
public sealed class SummonableState
{
    /// <summary>
    /// 当前召唤物实例唯一 Id。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 召唤者角色 Id。
    /// </summary>
    public required string OwnerId { get; init; }

    /// <summary>
    /// 所属阵营。当前只会是 left 或 right。
    /// </summary>
    public required string Side { get; init; }

    /// <summary>
    /// 召唤物贴图逻辑路径。
    /// </summary>
    public required string TexturePath { get; init; }

    /// <summary>
    /// 当前世界坐标。
    /// </summary>
    public required Vec2 Position { get; init; }

    /// <summary>
    /// 碰撞半径。
    /// </summary>
    public required double Radius { get; init; }

    /// <summary>
    /// 最大生命值。
    /// </summary>
    public required double HP { get; init; }

    /// <summary>
    /// 当前生命值。
    /// </summary>
    public required double Health { get; set; }

    /// <summary>
    /// 攻击间隔，单位秒。
    /// </summary>
    public required double AttackInterval { get; init; }

    /// <summary>
    /// 距离下一次攻击的剩余时间。
    /// </summary>
    public required double AttackTimer { get; set; }

    /// <summary>
    /// 召唤物主动攻击时使用的投掷物 Key。
    /// </summary>
    public required string AttackThrowableKey { get; init; }

    /// <summary>
    /// 召唤物是否还活着。
    /// </summary>
    public bool IsAlive => Health > 0;
}
