namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 酒鬼技能：每次释放向目标扔出一个酒瓶，碰墙或砸中单位后会炸成 5 块碎片。
/// </summary>
public sealed class DrunkardSkill : IFighterSkill
{
    /// <summary>
    /// 酒瓶破碎后生成的碎片数量。
    /// </summary>
    private const int BottleFragmentCount = 5;

    /// <summary>
    /// 每块碎片继承原投掷物伤害的比例。
    /// </summary>
    private const double BottleFragmentDamageRatio = 1.0 / 4.0;

    /// <summary>
    /// 碎片半径相对原酒瓶半径的缩放比例。
    /// </summary>
    private const double BottleFragmentRadiusScale = 0.45;

    /// <summary>
    /// 技能 key，对应 roles.json 中的 drunkard。
    /// </summary>
    public string Key => "drunkard";

    /// <summary>
    /// 释放技能：发射一个酒瓶，酒瓶命中墙体或单位时都会裂解为多个碎片。
    /// 碎片继承原投掷物阵营与贴图，但使用更小半径和按比例缩放后的伤害。
    /// </summary>
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
