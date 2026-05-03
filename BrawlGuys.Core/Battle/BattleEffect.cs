namespace BrawlGuys.Core;

public enum BattleEffectType
{
    Spark,
    Impact,
    Ring,
    DamageText,
    Explosion
}

public sealed class BattleEffect
{
    public required BattleEffectType Type { get; init; }
    public required Vec2 Position { get; init; }
    public required string ColorHex { get; init; }
    public string? Text { get; init; }
    public double Radius { get; set; }
    public double RemainingTime { get; set; }
    public double GrowthPerSecond { get; init; }
}
