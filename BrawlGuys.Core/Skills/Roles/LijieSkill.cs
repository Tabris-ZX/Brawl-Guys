namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 空军上将技能：若己方召唤出的无人机不足 3 架，则每次释放召唤 1 架无人机。
/// </summary>
public sealed class LijieSkill : IFighterSkill
{
    /// <summary>
    /// 单个阵营最多同时存在的无人机数量。
    /// </summary>
    private const int MaxSummonableCount = 3;

    /// <summary>
    /// 召唤物配置 key，对应 throwable.json 中带 Unit* 字段的无人机配置。
    /// </summary>
    private const string DroneSummonableKey = "drone";

    /// <summary>
    /// 无人机攻击使用的投掷物配置 key。
    /// 当前与召唤物 key 相同，便于复用同一份配置。
    /// </summary>
    private const string DroneAttackThrowableKey = "drone";

    /// <summary>
    /// 技能 key，对应 roles.json 中的 lijie。
    /// </summary>
    public string Key => "lijie";

    /// <summary>
    /// 空军上将本人不直接发射默认投掷物，而是召唤无人机，因此不要求 lijie 自己有 Projectile* 配置。
    /// </summary>
    public bool RequiresProjectileDefinition => false;

    /// <summary>
    /// 释放技能：若己方存活无人机未达上限，则召唤一架无人机。
    /// 无人机的移动、攻击和受击逻辑由 BattleWorld 统一推进。
    /// </summary>
    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        if (world.CountAliveSummonables(caster.Side, DroneAttackThrowableKey) >= MaxSummonableCount)
        {
            return;
        }

        world.SpawnSummonable(
            owner: caster,
            summonableKey: DroneSummonableKey,
            attackThrowableKey: DroneAttackThrowableKey);

        caster.SkillFlashTime = 0.4;
    }
}
