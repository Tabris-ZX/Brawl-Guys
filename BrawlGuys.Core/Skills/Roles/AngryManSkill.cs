namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 发怒的男人技能：每次释放向目标扔出一个棒球。
/// </summary>
public sealed class AngryManSkill : IFighterSkill
{
    public string Key => "angry-man";

    public void Execute(SkillContext context)
    {
        context.World.SpawnProjectile(
            owner: context.Caster,
            target: context.Target,
            throwable: ThrowableCatalog.Get(Key));

        context.Caster.SkillFlashTime = 0.25;
    }
}
