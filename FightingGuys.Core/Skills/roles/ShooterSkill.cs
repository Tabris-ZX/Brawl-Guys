namespace FightingGuys.Core.Skills.Roles;

/// <summary>
/// 快枪手技能：每 5 秒开启一次快速射击，连续打出 6 发子弹。
/// </summary>
public sealed class ShooterSkill : IFighterSkill
{
    public string Key => "shooter";

    public void Execute(SkillContext context)
    {
        context.World.StartBurstFire(
            owner: context.Caster,
            target: context.Target,
            throwableKey: Key,
            shotCount: 6,
            intervalSeconds: 0.08);

        context.Caster.SkillFlashTime = 0.35;
    }
}
