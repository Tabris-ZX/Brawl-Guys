namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 数学老师技能：蓄力结束后根据预告的 n 和 m，连续发起 n 次攻击，每次造成 m 点伤害。
/// 预告值完全由技能自己维护，不再把角色私货塞进 BattleWorld。
/// </summary>
public sealed class QzdSkill : IFighterSkill
{
    /// <summary>
    /// 运行时状态键：下一次攻击的连发数量预告值 n。
    /// </summary>
    public const string PreviewShotCountStateKey = "qzd.previewShotCount";

    /// <summary>
    /// 运行时状态键：下一次攻击的单发伤害预告值 m。
    /// </summary>
    public const string PreviewDamageStateKey = "qzd.previewDamage";

    /// <summary>
    /// 连发中每一发之间的间隔。数值很小，表现为蓄力完成后的瞬间爆发。
    /// </summary>
    private const double AttackIntervalSeconds = 0.01;

    /// <summary>
    /// n 和 m 的最大随机值，最终会同时限制连发数量和单发伤害。
    /// </summary>
    private const int MaxPreviewValue = 40;

    /// <summary>
    /// 技能 key，对应 roles.json 中的 qzd。
    /// </summary>
    public string Key => "qzd";

    /// <summary>
    /// 开局时先生成下一次攻击的 n 和 m 预告值。
    /// </summary>
    public void OnMatchStarted(BattleWorld world, FighterState self, FighterState enemy)
    {
        PrepareNextAttack(world, self);
    }

    /// <summary>
    /// 释放技能：读取当前预告值 n 和 m，启动 n 次超短间隔连发，每发固定造成 m 点伤害。
    /// 释放后立即生成下一轮预告值，供下一次蓄力期间显示。
    /// </summary>
    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        var shotCount = Math.Clamp(caster.GetRuntimeInt(PreviewShotCountStateKey, 1), 1, MaxPreviewValue);
        var damagePerShot = Math.Clamp(caster.GetRuntimeInt(PreviewDamageStateKey, 1), 1, MaxPreviewValue);

        world.StartBurstFire(
            owner: caster,
            target: target,
            throwableKey: Key,
            shotCount: shotCount,
            intervalSeconds: AttackIntervalSeconds,
            damageOverride: damagePerShot);

        PrepareNextAttack(world, caster);
        caster.SkillFlashTime = 0.6;
    }

    /// <summary>
    /// 右侧信息面板描述：基础描述 + 当前蓄力预告公式。
    /// </summary>
    public string GetDescription(FighterState fighter)
    {
        return $"{fighter.Definition.desc}\n{BuildPreviewText(fighter)}";
    }

    /// <summary>
    /// 竞技场头顶提示：显示当前蓄力公式，使玩家能看到 n、m 的逐步揭示。
    /// </summary>
    public string? GetArenaHintText(FighterState fighter)
    {
        return BuildPreviewText(fighter);
    }

    /// <summary>
    /// 为下一次攻击随机生成 n 和 m，并写入角色运行时状态。
    /// </summary>
    private static void PrepareNextAttack(BattleWorld world, FighterState fighter)
    {
        fighter.SetRuntimeValue(PreviewShotCountStateKey, RandomPreviewValue(world));
        fighter.SetRuntimeValue(PreviewDamageStateKey, RandomPreviewValue(world));
    }

    /// <summary>
    /// 从战斗世界随机数中生成 [1, MaxPreviewValue] 的整数，保证模拟结果由 BattleWorld 统一控制。
    /// </summary>
    private static int RandomPreviewValue(BattleWorld world)
    {
        return (int)Math.Floor(world.RandomRange(1, MaxPreviewValue + 1));
    }

    /// <summary>
    /// 构建预告公式文本。
    /// 蓄力未满 4 秒时隐藏 n；未满 8 秒时隐藏 m 和总伤害，制造逐步揭示效果。
    /// </summary>
    private static string BuildPreviewText(FighterState fighter)
    {
        var nText = ShouldRevealN(fighter)
            ? fighter.GetRuntimeInt(PreviewShotCountStateKey, 1).ToString()
            : "?";
        var mText = ShouldRevealM(fighter)
            ? fighter.GetRuntimeInt(PreviewDamageStateKey, 1).ToString()
            : "?";
        var totalText = ShouldRevealM(fighter)
            ? (fighter.GetRuntimeInt(PreviewShotCountStateKey, 1) * fighter.GetRuntimeInt(PreviewDamageStateKey, 1)).ToString()
            : "?";
        return $"{nText} * {mText} = {totalText}";
    }

    /// <summary>
    /// 判断是否显示连发数量 n。
    /// </summary>
    private static bool ShouldRevealN(FighterState fighter)
    {
        return GetChargeElapsedTime(fighter) >= 4;
    }

    /// <summary>
    /// 判断是否显示单发伤害 m 和总伤害。
    /// </summary>
    private static bool ShouldRevealM(FighterState fighter)
    {
        return GetChargeElapsedTime(fighter) >= 8;
    }

    /// <summary>
    /// 计算本轮技能从开始冷却到现在已经蓄力的时间。
    /// SkillTimer 是剩余冷却时间，因此用 CD - SkillTimer 得到已蓄力时长。
    /// </summary>
    private static double GetChargeElapsedTime(FighterState fighter)
    {
        if (fighter.Definition.CD <= 0)
        {
            return 0;
        }

        return Math.Clamp(fighter.Definition.CD - fighter.SkillTimer, 0, fighter.Definition.CD);
    }
}
