namespace BrawlGuys.Core;

public sealed class FighterState
{
    public required string Id { get; init; }
    public required string Side { get; init; }
    public required FighterDefinition Definition { get; init; }
    public required Vec2 Position { get; set; }
    public required Vec2 Velocity { get; set; }
    public required double Health { get; set; }
    public required double SkillTimer { get; set; }

    public int BurstShotsRemaining { get; set; }
    public double BurstShotInterval { get; set; }
    public double BurstShotTimer { get; set; }
    public string? BurstTargetId { get; set; }
    public string? BurstThrowableKey { get; set; }
    public double? BurstShotDamageOverride { get; set; }
    public int ChargePreviewShotCount { get; set; }
    public int ChargePreviewDamage { get; set; }
    public int IkunBasketballCount { get; set; }
    public double SkillFlashTime { get; set; }
    public double SleepTime { get; set; }
    public bool IsAlive => Health > 0;
    public bool IsSleeping => SleepTime > 0;
}
