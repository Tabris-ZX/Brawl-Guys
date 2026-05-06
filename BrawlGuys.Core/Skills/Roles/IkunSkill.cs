namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// ikun：开局自带 2 个篮球。
/// 每次出手会扔出一个可反弹、可回收、回收后可回血并返还库存的篮球。
/// </summary>
public sealed class IkunSkill : IFighterSkill
{
    /// <summary>
    /// 运行时状态键：当前剩余篮球数量。
    /// </summary>
    public const string BasketballCountStateKey = "ikun.basketballCount";

    /// <summary>
    /// 开局篮球库存。篮球不是无限投掷物，必须回收后才会返还。
    /// </summary>
    private const int InitialBasketballCount = 2;

    /// <summary>
    /// 技能 key，对应 roles.json 中的 ikun。
    /// </summary>
    public string Key => "ikun";

    /// <summary>
    /// 每局开始时重置篮球库存，避免上一局剩余数量影响新局。
    /// </summary>
    public void OnMatchStarted(BattleWorld world, FighterState self, FighterState enemy)
    {
        self.SetRuntimeValue(BasketballCountStateKey, InitialBasketballCount);
    }

    /// <summary>
    /// 释放技能：若还有篮球库存，则发射一个会墙面反弹、命中后继续存在、可被自己回收的篮球。
    /// 发射后立即扣除库存，防止短时间内无限投篮。
    /// </summary>
    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        var basketballCount = caster.GetRuntimeInt(BasketballCountStateKey);
        if (basketballCount <= 0)
        {
            return;
        }

        world.SpawnProjectile(
            owner: caster,
            target: target,
            bounceOnWalls: true,
            canBeReclaimedByOwner: true,
            reclaimHealRatio: 0.5,
            keepOnFighterHit: true);

        caster.SetRuntimeValue(BasketballCountStateKey, basketballCount - 1);
        caster.SkillFlashTime = 0.25;
    }

    /// <summary>
    /// 篮球被拥有者回收时返还 1 个库存。
    /// 基础回血由 BattleWorld 根据 reclaimHealRatio 处理，这里只负责技能自己的弹药计数。
    /// </summary>
    public void OnProjectileReclaimed(BattleWorld world, FighterState owner, BattleProjectile projectile)
    {
        var basketballCount = owner.GetRuntimeInt(BasketballCountStateKey);
        owner.SetRuntimeValue(BasketballCountStateKey, basketballCount + 1);
    }

    /// <summary>
    /// 右侧信息面板描述：基础描述 + 当前剩余篮球数。
    /// </summary>
    public string GetDescription(FighterState fighter)
    {
        return $"{fighter.Definition.desc}\n剩余篮球：{fighter.GetRuntimeInt(BasketballCountStateKey)}";
    }
}
