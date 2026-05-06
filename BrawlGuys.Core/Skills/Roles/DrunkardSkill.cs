namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 酒鬼技能：每次释放向目标扔出一个酒瓶，碰墙或砸中单位后会炸成 5 块碎片。
/// </summary>
public sealed class DrunkardSkill : IFighterSkill
{
    private const int BottleFragmentCount = 5;
    private const double BottleFragmentDamageRatio = 1.0 / 4.0;
    private const double BottleFragmentRadiusScale = 0.45;

    public string Key => "drunkard";

    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        world.SpawnProjectile(
            owner: caster,
            target: target,
            splitOnWallImpact: true,
            splitOnUnitImpact: true,
            fragmentCount: BottleFragmentCount,
            fragmentDamageRatio: BottleFragmentDamageRatio,
            fragmentRadiusScale: BottleFragmentRadiusScale);

        caster.SkillFlashTime = 0.25;
    }
}
