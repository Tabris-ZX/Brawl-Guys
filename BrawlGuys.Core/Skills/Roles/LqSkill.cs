namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 物理老师技能：三连发。
/// 命中时：目标未睡眠则进入睡眠；目标已睡眠则造成伤害。
/// </summary>
public sealed class LqSkill : IFighterSkill
{
    /// <summary>
    /// 每次技能发射的粉笔数量。
    /// </summary>
    private const int ShotCount = 3;

    /// <summary>
    /// 三连发之间的间隔。
    /// </summary>
    private const double ShotIntervalSeconds = 0.2;

    /// <summary>
    /// 技能 key，对应 roles.json 中的 lq。
    /// </summary>
    public string Key => "lq";

    /// <summary>
    /// 释放技能：启动三连发，并要求投掷物只有在目标已睡眠时才造成伤害。
    /// 若目标未睡眠，投掷物仍可通过配置施加睡眠效果，从而为后续命中创造伤害条件。
    /// </summary>
    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        world.StartBurstFire(
            owner: caster,
            target: target,
            throwableKey: Key,
            shotCount: ShotCount,
            intervalSeconds: ShotIntervalSeconds,
            dealDamageOnlyIfTargetSleeping: true);

        caster.SkillFlashTime = 0.3;
    }
}