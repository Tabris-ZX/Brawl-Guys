namespace FightingGuys.Core.Skills;

/// <summary>
/// 角色技能接口。每个技能类用唯一 Key 注册到 SkillRegistry。
/// </summary>
public interface IFighterSkill
{
    /// <summary>技能唯一 ID，与角色 Key 一致。</summary>
    string Key { get; }

    /// <summary>执行技能。技能通过 SkillContext 读写战斗状态。</summary>
    void Execute(SkillContext context);
}
