namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 发怒的男人技能：每次释放向目标扔出一个棒球。
/// </summary>
public sealed class AngryManSkill : IFighterSkill
{
    public string Key => "angry-man";

    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        world.SpawnProjectile(
            owner: caster,
            target: target);

        caster.SkillFlashTime = 0.25;
    }
}
