namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// ikun：初始2个篮球。若手里有球，则每5秒扔出1个会反弹的篮球。
/// </summary>
public sealed class IkunSkill : IFighterSkill
{
    public string Key => "ikun";

    public void Execute(SkillContext context)
    {
        if (context.Caster.IkunBasketballCount <= 0)
        {
            return;
        }

        context.World.SpawnIkunBasketball(context.Caster, context.Target);
        context.Caster.IkunBasketballCount--;
        context.Caster.SkillFlashTime = 0.25;
    }
}
