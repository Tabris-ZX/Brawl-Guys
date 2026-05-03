namespace BrawlGuys.Core;

/// <summary>
/// 投掷物静态配置。Key 与释放它的角色 Key 保持一致。
/// </summary>
public sealed class ThrowableDefinition
{
    /// <summary>投掷物配置 Key，当前与角色 Key 一致，例如 drunkard。</summary>
    public required string Key { get; init; }

    /// <summary>界面/调试显示名称。</summary>
    public required string Name { get; init; }

    /// <summary>投掷物贴图逻辑路径，具体文件位置由表现层资源映射解析。</summary>
    public required string TexturePath { get; init; }

    /// <summary>飞行速度。</summary>
    public required double Speed { get; init; }

    /// <summary>碰撞/显示半径。</summary>
    public required double Radius { get; init; }

    /// <summary>命中伤害。</summary>
    public required double Damage { get; init; }

    /// <summary>贴图缺失时的回退颜色。</summary>
    public required string ColorHex { get; init; }

    /// <summary>若该投掷物还承担召唤物配置，则这里表示召唤物贴图。</summary>
    public string? UnitTexturePath { get; init; }

    /// <summary>若该投掷物还承担召唤物配置，则这里表示召唤物半径。</summary>
    public double? UnitRadius { get; init; }

    /// <summary>若该投掷物还承担召唤物配置，则这里表示召唤物生命值。</summary>
    public double? unitHP { get; init; }

    /// <summary>若该投掷物还承担召唤物配置，则这里表示召唤物攻击间隔。</summary>
    public double? unitCD { get; init; }

    /// <summary>是否能让目标陷入睡眠。</summary>
    public bool CanSleepTarget { get; init; }

    /// <summary>若能睡眠目标，睡眠持续时间（秒）。</summary>
    public double SleepDuration { get; init; }
}
