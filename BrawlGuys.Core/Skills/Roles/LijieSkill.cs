namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 空军上将技能：若己方召唤出的无人机不足 3 架，则每次释放召唤 1 架无人机。
/// </summary>
public sealed class LijieSkill : IFighterSkill
{
    private const int MaxSummonableCount = 3;
    private const string DroneSummonableKey = "drone";
    private const string DroneAttackThrowableKey = "drone";

    public string Key => "lijie";

    public bool RequiresProjectileDefinition => false;

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
