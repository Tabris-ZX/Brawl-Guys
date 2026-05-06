namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 快枪手技能：每次释放都会开启一次短间隔连射。
/// </summary>
public sealed class ShooterSkill : IFighterSkill
{
    /// <summary>
    /// 每次技能连射的子弹数量。
    /// </summary>
    private const int ShotCount = 6;

    /// <summary>
    /// 连射时相邻两发子弹的间隔，数值越小爆发越集中。
    /// </summary>
    private const double ShotIntervalSeconds = 0.08;

    /// <summary>
    /// 技能 key，对应 roles.json 中的 shooter。
    /// </summary>
    public string Key => "shooter";

    /// <summary>
    /// 释放技能：启动一次 6 连发，由 BattleWorld 在后续 Update 中按间隔逐发生成投掷物。
    /// </summary>
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
