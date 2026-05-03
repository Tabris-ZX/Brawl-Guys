namespace FightingGuys.Core.Skills.Roles;

/// <summary>
/// 反伤者：每次主动释放时向周围 8 个方向发射尖刺。
/// 受击反伤逻辑在 BattleWorld 中处理。
/// </summary>
public sealed class ReflectorSkill : IFighterSkill
{
    private const int DirectionCount = 8;

    public string Key => "reflector";

    public void Execute(SkillContext context)
    {
        var throwable = ThrowableCatalog.Get(Key);
        context.World.FireRadialProjectiles(context.Caster, context.Target, throwable, throwable.Damage, DirectionCount);
        context.Caster.SkillFlashTime = 0.3;
    }
}
