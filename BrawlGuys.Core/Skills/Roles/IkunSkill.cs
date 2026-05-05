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

    private const int InitialBasketballCount = 2;

    public string Key => "ikun";

    public void OnMatchStarted(BattleWorld world, FighterState self, FighterState enemy)
    {
        self.SetRuntimeValue(BasketballCountStateKey, InitialBasketballCount);
    }

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

    public void OnProjectileReclaimed(BattleWorld world, FighterState owner, BattleProjectile projectile)
    {
        var basketballCount = owner.GetRuntimeInt(BasketballCountStateKey);
        owner.SetRuntimeValue(BasketballCountStateKey, basketballCount + 1);
    }

    public string GetDescription(FighterState fighter)
    {
        return $"{fighter.Definition.desc}\n剩余篮球：{fighter.GetRuntimeInt(BasketballCountStateKey)}";
    }
}
