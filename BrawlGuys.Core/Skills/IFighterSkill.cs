namespace BrawlGuys.Core.Skills;

/// <summary>
/// 角色技能接口。
/// 保持“一角色一文件”的写法：常用扩展点直接放这里，
/// 大多数角色只实现 Execute，其他钩子按需覆写即可。
/// </summary>
public interface IFighterSkill
{
    string Key { get; }

    bool RequiresProjectileDefinition => true;

    void Execute(BattleWorld world, FighterState caster, FighterState target);

    void OnMatchStarted(BattleWorld world, FighterState self, FighterState enemy)
    {
    }

    bool CanUseSkill(BattleWorld world, FighterState self, FighterState enemy) => true;

    double ModifyOutgoingDamage(BattleWorld world, FighterState attacker, FighterState target, double damage) => damage;

    double ModifyIncomingDamage(BattleWorld world, FighterState? attacker, FighterState target, double damage) => damage;

    void OnHitTarget(BattleWorld world, FighterState attacker, FighterState target, double damageDealt)
    {
    }

    void OnProjectileReclaimed(BattleWorld world, FighterState owner, BattleProjectile projectile)
    {
    }

    void OnDamaged(BattleWorld world, FighterState? attacker, FighterState target, double damageDealt)
    {
    }

    string GetDescription(FighterState fighter) => fighter.Definition.desc;

    string? GetArenaHintText(FighterState fighter) => null;
}
