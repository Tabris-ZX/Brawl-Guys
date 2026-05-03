namespace FightingGuys.Core.Skills.Roles;

/// <summary>
/// 数学老师技能：蓄力结束后根据预告的 n 和 m，连续发起 n 次攻击，每次造成 m 点伤害。
/// </summary>
public sealed class QzdSkill : IFighterSkill
{
    private const double AttackIntervalSeconds = 0.01;

    public string Key => "qzd";

    public void Execute(SkillContext context)
    {
        var shotCount = Math.Clamp(context.Caster.ChargePreviewShotCount, 1, 40);
        var damagePerShot = Math.Clamp(context.Caster.ChargePreviewDamage, 1, 40);

        context.World.StartBurstFire(
            owner: context.Caster,
            target: context.Target,
            throwableKey: Key,
            shotCount: shotCount,
            intervalSeconds: AttackIntervalSeconds,
            damageOverride: damagePerShot);

        context.World.PrepareQzdNextAttack(context.Caster);
        context.Caster.SkillFlashTime = 0.6;
    }
}
