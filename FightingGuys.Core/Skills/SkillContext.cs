namespace FightingGuys.Core.Skills;

/// <summary>
/// 技能执行上下文。避免技能直接依赖 UI，同时给技能足够的信息修改战斗状态。
/// </summary>
public sealed class SkillContext
{
    /// <summary>当前战斗世界，可用于生成特效或访问竞技场。</summary>
    public required BattleWorld World { get; init; }

    /// <summary>释放技能的角色。</summary>
    public required FighterState Caster { get; init; }

    /// <summary>当前目标。当前版本是一对一战斗，所以就是敌方角色。</summary>
    public required FighterState Target { get; init; }

    /// <summary>统一随机数入口，方便以后做回放/固定随机种子。</summary>
    public required Func<double, double, double> RandomRange { get; init; }
}
