namespace FightingGuys.Core.Skills.Roles;

/// <summary>
/// 空军上将技能：若己方无人机不足 3 架，则每次释放召唤 1 架无人机。
/// </summary>
public sealed class LijieSkill : IFighterSkill
{
    public string Key => "lijie";

    public void Execute(SkillContext context)
    {
        if (context.World.CountDrones(context.Caster.Side) >= 3) return;

        context.World.SummonDrone(context.Caster);
        context.Caster.SkillFlashTime = 0.4;
    }
}
