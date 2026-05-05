namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 观者：默认以平静状态开始战斗。
/// 命中后会在平静/愤怒两种状态之间切换，并影响伤害与回血。
/// </summary>
public sealed class WatcherSkill : IFighterSkill
{
    /// <summary>
    /// 运行时状态键：当前是否处于愤怒状态。
    /// </summary>
    public const string AngryStateKey = "watcher.isAngry";

    /// <summary>
    /// 运行时状态键：成功命中次数。
    /// </summary>
    public const string SuccessfulHitCountStateKey = "watcher.successfulHitCount";

    public string Key => "watcher";

    public void OnMatchStarted(BattleWorld world, FighterState self, FighterState enemy)
    {
        self.SetRuntimeFlag(AngryStateKey, false);
        self.SetRuntimeValue(SuccessfulHitCountStateKey, 0);
    }

    public double ModifyOutgoingDamage(BattleWorld world, FighterState attacker, FighterState target, double damage)
    {
        return IsAngry(attacker) ? damage * 2 : damage;
    }

    public double ModifyIncomingDamage(BattleWorld world, FighterState? attacker, FighterState target, double damage)
    {
        return IsAngry(target) ? damage * 2 : damage;
    }

    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        world.SpawnProjectile(
            owner: caster,
            target: target);

        caster.SkillFlashTime = 0.25;
    }

    public void OnHitTarget(BattleWorld world, FighterState attacker, FighterState target, double damageDealt)
    {
        var hitCount = attacker.GetRuntimeInt(SuccessfulHitCountStateKey) + 1;
        attacker.SetRuntimeValue(SuccessfulHitCountStateKey, hitCount);

        if (hitCount == 1)
        {
            attacker.SetRuntimeFlag(AngryStateKey, true);
            attacker.SkillFlashTime = Math.Max(attacker.SkillFlashTime, 0.35);
            return;
        }

        if (!IsAngry(attacker))
        {
            attacker.Health = Math.Min(attacker.Definition.HP, attacker.Health + damageDealt);
        }

        attacker.SetRuntimeFlag(AngryStateKey, !IsAngry(attacker));
        attacker.SkillFlashTime = Math.Max(attacker.SkillFlashTime, 0.35);
    }

    public static bool IsAngry(FighterState? fighter)
    {
        return fighter is not null && fighter.GetRuntimeFlag(AngryStateKey);
    }
}
