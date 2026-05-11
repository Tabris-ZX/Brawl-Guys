namespace BrawlGuys.Core.Skills.Roles;

/// <summary>
/// 蓝色海豚：
/// 1. 敌人碰到海豚时会被反弹并受到接触伤害；
/// 2. 每 4 秒朝敌人冲刺一次，冲刺撞人造成双倍伤害；
/// 3. 冲刺命中后会把对手撞晕 0.5 秒，冲刺撞墙后自身眩晕 1 秒，眩晕期间不再造成接触伤害；
/// </summary>
public sealed class LcySkill : IFighterSkill
{
    private const string IsDashingStateKey = "blueDolphin.isDashing";
    private const string CollisionDamageCooldownUntilKey = "blueDolphin.collisionDamageCooldownUntil";

    private const double ContactDamage = 50;
    private const double CollisionDamageCooldownSeconds = 0.35;
    private const double DashSpeedMultiplier = 3.2;
    private const double DashWallStunSeconds = 1.0;
    private const double CollisionStunSeconds = 0.5;

    public string Key => "blue-dolphin";

    public bool RequiresProjectileDefinition => false;

    public void OnMatchStarted(BattleWorld world, FighterState self, FighterState enemy)
    {
        self.SetRuntimeFlag(IsDashingStateKey, false);
        self.SetRuntimeValue(CollisionDamageCooldownUntilKey, 0);
    }

    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        var direction = target.Position - caster.Position;
        world.SetFighterVelocity(caster, direction, caster.Definition.Speed * DashSpeedMultiplier);
        caster.SetRuntimeFlag(IsDashingStateKey, true);
        caster.SkillFlashTime = 0.35;
    }

    public void OnUpdate(BattleWorld world, FighterState self, FighterState enemy, double dt)
    {
        if (self.IsSleeping && IsDashing(self))
        {
            EndDash(world, self);
        }
    }

    public void OnFighterCollision(BattleWorld world, FighterState self, FighterState other)
    {
        if (!self.IsAlive || !other.IsAlive || self.IsSleeping)
        {
            return;
        }

        var cooldownUntil = self.GetRuntimeValue(CollisionDamageCooldownUntilKey);
        if (world.ElapsedTime < cooldownUntil)
        {
            return;
        }

        var damage = ContactDamage;
        var isDashHit = IsDashing(self);
        if (isDashHit)
        {
            damage *= 2;
            EndDash(world, self);
        }

        world.DealDirectDamage(self, other, damage);
        if (isDashHit)
        {
            BounceBackFromTarget(world, self, other);
            other.SleepTime = Math.Max(other.SleepTime, CollisionStunSeconds);
        }
        self.SetRuntimeValue(CollisionDamageCooldownUntilKey, world.ElapsedTime + CollisionDamageCooldownSeconds);
        self.SkillFlashTime = Math.Max(self.SkillFlashTime, 0.2);
    }

    public void OnWallBounce(BattleWorld world, FighterState self)
    {
        if (!IsDashing(self))
        {
            return;
        }

        EndDash(world, self);
        self.SleepTime = Math.Max(self.SleepTime, DashWallStunSeconds);
        self.SkillFlashTime = Math.Max(self.SkillFlashTime, 0.2);
    }

    private static bool IsDashing(FighterState fighter)
    {
        return fighter.GetRuntimeFlag(IsDashingStateKey);
    }

    private static void EndDash(BattleWorld world, FighterState fighter)
    {
        fighter.SetRuntimeFlag(IsDashingStateKey, false);
        world.SetFighterVelocity(fighter, fighter.Velocity, fighter.Definition.Speed);
    }

    private static void BounceBackFromTarget(BattleWorld world, FighterState self, FighterState other)
    {
        var collisionNormal = (self.Position - other.Position).Normalized();
        if (collisionNormal.Length <= 0.0001)
        {
            collisionNormal = new Vec2(1, 0);
        }

        var incomingVelocity = self.Velocity;
        var reflectedVelocity = incomingVelocity - (collisionNormal * (2 * Vec2.Dot(incomingVelocity, collisionNormal)));
        var bounceDirection = reflectedVelocity.Length > 0.0001 ? reflectedVelocity : collisionNormal;
        world.SetFighterVelocity(self, bounceDirection, self.Definition.Speed);
    }
}
