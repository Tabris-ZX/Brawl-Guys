namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 观者：前 5 秒不会攻击，之后每次释放向目标扔出一个投掷物。
/// 形态切换与伤害/回血逻辑在 BattleWorld 中处理。
/// </summary>
public sealed class WatcherSkill : IFighterSkill
{
    public string Key => "watcher";

    public void Execute(SkillContext context)
    {
        context.World.SpawnProjectile(
            owner: context.Caster,
            target: context.Target,
            throwable: ThrowableCatalog.Get(Key));

        context.Caster.SkillFlashTime = 0.25;
    }
}
