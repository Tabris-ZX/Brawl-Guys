namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 物理老师技能：三连发。
/// 命中时：目标未睡眠则进入睡眠；目标已睡眠则造成伤害。
/// </summary>
public sealed class LqSkill : IFighterSkill
{
    private const int ShotCount = 3;
    private const double ShotIntervalSeconds = 0.2;

    public string Key => "lq";

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