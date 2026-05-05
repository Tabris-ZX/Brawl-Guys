namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 酒鬼技能：每次释放向目标扔出一个酒瓶。
/// </summary>
public sealed class DrunkardSkill : IFighterSkill
{
    public string Key => "drunkard";

    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        world.SpawnProjectile(
            owner: caster,
            target: target);

        caster.SkillFlashTime = 0.25;
    }
}
