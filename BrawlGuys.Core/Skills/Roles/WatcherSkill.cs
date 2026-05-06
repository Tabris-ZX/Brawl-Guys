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

    /// <summary>
    /// 技能 key，对应 roles.json 中的 watcher。
    /// </summary>
    public string Key => "watcher";

    /// <summary>
    /// 开局初始化观者状态：默认平静，并清空命中计数。
    /// </summary>
    public void OnMatchStarted(BattleWorld world, FighterState self, FighterState enemy)
    {
        self.SetRuntimeFlag(AngryStateKey, false);
        self.SetRuntimeValue(SuccessfulHitCountStateKey, 0);
    }

    /// <summary>
    /// 修改观者造成的伤害：愤怒状态下造成双倍伤害。
    /// </summary>
    public double ModifyOutgoingDamage(BattleWorld world, FighterState attacker, FighterState target, double damage)
    {
        return IsAngry(attacker) ? damage * 2 : damage;
    }

    /// <summary>
    /// 修改观者受到的伤害：愤怒状态下自身也会受到双倍伤害，形成高风险高收益状态。
    /// </summary>
    public double ModifyIncomingDamage(BattleWorld world, FighterState? attacker, FighterState target, double damage)
    {
        return IsAngry(target) ? damage * 2 : damage;
    }

    /// <summary>
    /// 主动技能：向目标发射默认投掷物。
    /// 观者的核心特殊效果不在发射时处理，而是在命中与伤害钩子中处理。
    /// </summary>
    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        world.SpawnProjectile(
            owner: caster,
            target: target);

        caster.SkillFlashTime = 0.25;
    }

    /// <summary>
    /// 命中后切换平静/愤怒状态。
    /// 第一次命中必定进入愤怒；之后若在平静状态命中，会按实际伤害回血，然后再切换状态。
    /// </summary>
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

    /// <summary>
    /// 判断指定角色当前是否处于观者的愤怒状态。
    /// 该方法也被 WPF 表现层用于决定观者光环颜色。
    /// </summary>
    public static bool IsAngry(FighterState? fighter)
    {
        return fighter is not null && fighter.GetRuntimeFlag(AngryStateKey);
    }
}
