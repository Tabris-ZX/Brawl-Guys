namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 反伤者：每次主动释放时向周围 8 个方向发射尖刺。
/// 受到足够伤害后，还会自动触发一轮反击。
/// </summary>
public sealed class ReflectorSkill : IFighterSkill
{
    private const int DirectionCount = 8;
    private const double MinimumReflectDamage = 4;
    private const double ReflectRatio = 0.3;

    public string Key => "reflector";

    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        world.FireRadialProjectiles(caster, target, caster.Definition.ProjectileDamage ?? ThrowableCatalog.Get(Key).Damage, DirectionCount);
        caster.SkillFlashTime = 0.3;
    }

    public void OnDamaged(BattleWorld world, FighterState? attacker, FighterState target, double damageDealt)
    {
        if (!target.IsAlive || damageDealt < MinimumReflectDamage || attacker is null)
        {
            return;
        }

        var counterDamage = damageDealt * ReflectRatio;
        world.FireRadialProjectiles(target, attacker, counterDamage, DirectionCount);
        target.SkillFlashTime = Math.Max(target.SkillFlashTime, 0.2);
    }
}
