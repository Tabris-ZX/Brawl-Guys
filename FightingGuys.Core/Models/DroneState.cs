namespace FightingGuys.Core;

/// <summary>
/// 无人机单位：由特定角色召唤，固定悬停在场地上并周期性开火。
/// </summary>
public sealed class DroneState
{
    public required string Id { get; init; }
    public required string OwnerId { get; init; }
    public required string Side { get; init; }
    public required string Name { get; init; }
    public required string TexturePath { get; init; }
    public required Vec2 Position { get; init; }
    public required double Radius { get; init; }
    public required double HP { get; init; }
    public required double Health { get; set; }
    public required double AttackInterval { get; init; }
    public required double AttackTimer { get; set; }
    public required string ThrowableKey { get; init; }

    public bool IsAlive => Health > 0;
}
