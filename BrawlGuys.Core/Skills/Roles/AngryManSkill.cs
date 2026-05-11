namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 发怒的男人技能：每次释放向目标扔出一个棒球，且伤害会随已损失生命值提高。
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

    /// <summary>
    /// 伤害机制：每次攻击伤害乘以 (1 + x)，其中 x 为已损失生命值百分比。
    /// 例如损失 30% 血量时，伤害倍率为 1.3。
    /// </summary>
    public double ModifyOutgoingDamage(BattleWorld world, FighterState attacker, FighterState target, double damage)
    {
        if (attacker.Definition.HP <= 0)
        {
            return damage;
        }

        var hpLossPercent = Math.Clamp((attacker.Definition.HP - attacker.Health) / attacker.Definition.HP, 0, 1);
        return damage * (1 + hpLossPercent);
    }
}

