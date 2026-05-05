namespace BrawlGuys.Core;

/// <summary>
/// 战斗特效类型。
/// 当前只保留项目里实际使用的类型，避免无效枚举项制造阅读噪音。
/// </summary>
public enum BattleEffectType
{
    /// <summary>
    /// 命中或碰撞时的短暂冲击效果。
    /// </summary>
    Impact,

    /// <summary>
    /// 飘字伤害文本。
    /// </summary>
    DamageText,

    /// <summary>
    /// 单位死亡或爆裂时的扩散爆炸效果。
    /// </summary>
    Explosion
}

/// <summary>
/// 单个战斗特效实例。
/// </summary>
public sealed class BattleEffect
{
    /// <summary>
    /// 特效类型。
    /// </summary>
    public required BattleEffectType Type { get; init; }

    /// <summary>
    /// 特效中心点坐标。
    /// </summary>
    public required Vec2 Position { get; init; }

    /// <summary>
    /// 特效主颜色。
    /// </summary>
    public required string ColorHex { get; init; }

    /// <summary>
    /// 文本内容。只有伤害飘字会使用。
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// 当前显示半径。
    /// </summary>
    public double Radius { get; set; }

    /// <summary>
    /// 剩余存在时间。
    /// </summary>
    public double RemainingTime { get; set; }

    /// <summary>
    /// 每秒增长多少半径。
    /// </summary>
    public double GrowthPerSecond { get; init; }
}
