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

    private const double AttackIntervalSeconds = 0.01;
    private const int MaxPreviewValue = 40;

    public string Key => "qzd";

    public void OnMatchStarted(BattleWorld world, FighterState self, FighterState enemy)
    {
        PrepareNextAttack(world, self);
    }

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

    public string GetDescription(FighterState fighter)
    {
        return $"{fighter.Definition.desc}\n{BuildPreviewText(fighter)}";
    }

    public string? GetArenaHintText(FighterState fighter)
    {
        return BuildPreviewText(fighter);
    }

    private static void PrepareNextAttack(BattleWorld world, FighterState fighter)
    {
        fighter.SetRuntimeValue(PreviewShotCountStateKey, RandomPreviewValue(world));
        fighter.SetRuntimeValue(PreviewDamageStateKey, RandomPreviewValue(world));
    }

    private static int RandomPreviewValue(BattleWorld world)
    {
        return (int)Math.Floor(world.RandomRange(1, MaxPreviewValue + 1));
    }

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

    private static bool ShouldRevealN(FighterState fighter)
    {
        return GetChargeElapsedTime(fighter) >= 4;
    }

    private static bool ShouldRevealM(FighterState fighter)
    {
        return GetChargeElapsedTime(fighter) >= 8;
    }

    private static double GetChargeElapsedTime(FighterState fighter)
    {
        if (fighter.Definition.CD <= 0)
        {
            return 0;
        }

        return Math.Clamp(fighter.Definition.CD - fighter.SkillTimer, 0, fighter.Definition.CD);
    }
}
