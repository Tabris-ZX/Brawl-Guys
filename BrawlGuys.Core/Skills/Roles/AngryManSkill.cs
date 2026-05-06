namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 发怒的男人技能：每次释放向目标扔出一个棒球。
/// </summary>
public sealed class AngryManSkill : IFighterSkill
{
    /// <summary>
    /// 技能 key，对应 roles.json 中的 angry-man。
    /// </summary>
    public string Key => "angry-man";

    /// <summary>
    /// 释放技能：向当前目标投掷一颗默认棒球。
    /// 棒球的速度、半径、伤害和贴图来自角色对应的投掷物配置。
    /// </summary>
    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        world.SpawnProjectile(
            owner: caster,
            target: target);

        caster.SkillFlashTime = 0.25;
    }
}
