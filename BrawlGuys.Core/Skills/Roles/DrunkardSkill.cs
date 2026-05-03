namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 酒鬼技能：每次释放向目标扔出一个酒瓶。
/// </summary>
public sealed class DrunkardSkill : IFighterSkill
{
    public string Key => "drunkard";

    public void Execute(SkillContext context)
    {
        context.World.SpawnProjectile(
            owner: context.Caster,
            target: context.Target,
            throwable: ThrowableCatalog.Get(Key));

        context.Caster.SkillFlashTime = 0.25;
    }
}
