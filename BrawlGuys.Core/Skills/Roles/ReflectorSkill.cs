namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 反伤者：每次主动释放时向周围 8 个方向发射尖刺。
/// 受到足够伤害后，还会自动触发一轮反击。
/// </summary>
public sealed class ReflectorSkill : IFighterSkill
{
    /// <summary>
    /// 环形发射方向数量。8 表示上下左右和四个斜向都发射一枚投掷物。
    /// </summary>
    private const int DirectionCount = 8;

    /// <summary>
    /// 触发受击反击的最小实际伤害，过滤极小伤害导致的过度反击。
    /// </summary>
    private const double MinimumReflectDamage = 4;

    /// <summary>
    /// 反击伤害比例：反击投掷物伤害 = 本次受到的实际伤害 * 该比例。
    /// </summary>
    private const double ReflectRatio = 0.3;

    /// <summary>
    /// 技能 key，对应 roles.json 中的 reflector。
    /// </summary>
    public string Key => "reflector";

    /// <summary>
    /// 主动技能：以自身为中心向 8 个方向发射尖刺。
    /// 目标参数用于确定初始参考方向，具体环形弹幕由 BattleWorld.FireRadialProjectiles 处理。
    /// </summary>
    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        world.FireRadialProjectiles(caster, target, caster.Definition.ProjectileDamage ?? ThrowableCatalog.Get(Key).Damage, DirectionCount);
        caster.SkillFlashTime = 0.3;
    }

    /// <summary>
    /// 受击后反击：当反伤者仍存活、伤害达到阈值且存在攻击者时，向周围发射一轮按受伤量缩放的尖刺。
    /// </summary>
    public void OnDamaged(BattleWorld world, FighterState? attacker, FighterState target, double damageDealt)
    {
        if (!target.IsAlive || damageDealt < MinimumReflectDamage || attacker is null) return;

        var counterDamage = damageDealt * ReflectRatio;
        world.FireRadialProjectiles(target, attacker, counterDamage, DirectionCount);
        target.SkillFlashTime = Math.Max(target.SkillFlashTime, 0.2);
    }
}
