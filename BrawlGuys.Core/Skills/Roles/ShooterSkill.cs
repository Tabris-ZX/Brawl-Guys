namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 快枪手技能：每次释放都会开启一次短间隔连射。
/// </summary>
public sealed class ShooterSkill : IFighterSkill
{
    private const int ShotCount = 6;
    private const double ShotIntervalSeconds = 0.08;

    public string Key => "shooter";

    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        world.StartBurstFire(
            owner: caster,
            target: target,
            throwableKey: Key,
            shotCount: ShotCount,
            intervalSeconds: ShotIntervalSeconds);

        caster.SkillFlashTime = 0.35;
    }
}
