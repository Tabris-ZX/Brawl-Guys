using BrawlGuys.Core.Skills;

namespace BrawlGuys.Core;

/// <summary>
/// 一局战斗的核心模拟器：角色会移动并反弹，攻击通过投掷物命中造成伤害。
/// 身体碰撞只负责反弹，不造成伤害。
/// </summary>
public sealed class BattleWorld
{
    private const double DefaultDroneAttackInterval = 2.0;
    private const double DefaultDroneHP = 50;
    private const double DefaultDroneRadius = 25;
    private const double MaxFighterProjectileSpreadAngleDegrees = 50;
    private const int QzdMaxPreviewValue = 40;
    private const double WatcherAttackDelaySeconds = 5.0;
    private const string QzdSkillKey = "qzd";
    private const string ReflectorKey = "reflector";
    private const string IkunKey = "ikun";
    private const string WatcherKey = "watcher";

    private readonly Random _random = new();

    public BattleWorld(ArenaDefinition arena)
    {
        Arena = arena;
    }

    public ArenaDefinition Arena { get; }
    public List<FighterState> Fighters { get; } = new();
    public List<DroneState> Drones { get; } = new();
    public List<BattleProjectile> Projectiles { get; } = new();
    public List<BattleEffect> Effects { get; } = new();
    public string StatusText { get; private set; } = "准备开始";
    public FighterState? Winner { get; private set; }
    public bool IsDraw { get; private set; }
    public double ElapsedTime { get; private set; }

    /// <summary>
    /// 开始一局新比赛：创建左右两个角色，并给他们随机初始移动方向。
    /// </summary>
    public void StartMatch(string leftKey, string rightKey)
    {
        Fighters.Clear();
        Drones.Clear();
        Projectiles.Clear();
        Effects.Clear();
        Winner = null;
        IsDraw = false;
        ElapsedTime = 0;
        StatusText = "战斗中";

        var leftDefinition = FighterCatalog.Get(leftKey);
        var rightDefinition = FighterCatalog.Get(rightKey);

        Fighters.Add(CreateFighter(
            leftDefinition,
            "left",
            new Vec2(BattleTuning.FighterSidePadding, Arena.Height / 2),
            CreateRandomVelocity(leftDefinition)));

        Fighters.Add(CreateFighter(
            rightDefinition,
            "right",
            new Vec2(Arena.Width - BattleTuning.FighterSidePadding, Arena.Height / 2),
            CreateRandomVelocity(rightDefinition)));
    }

    /// <summary>
    /// 推进一帧战斗模拟：更新攻击计时器、投掷物、特效和胜负。
    /// </summary>
    public void Update(double dt)
    {
        if (Fighters.Count < 2)
        {
            return;
        }

        var remainingTime = Math.Max(0, dt);
        while (remainingTime > 0)
        {
            var step = Math.Min(BattleTuning.MaxDeltaTime, remainingTime);
            UpdateStep(step);
            remainingTime -= step;
        }
    }

    private void UpdateStep(double dt)
    {
        ElapsedTime += dt;

        if (Winner is null && !IsDraw)
        {
            var left = Fighters[0];
            var right = Fighters[1];

            UpdateFighter(left, right, dt);
            UpdateFighter(right, left, dt);
            UpdateDrones(dt);
            ResolveFighterCollision(left, right);
            UpdateProjectiles(dt);
            CheckResult();
        }

        UpdateEffects(dt);
    }

    /// <summary>
    /// 供技能调用：向目标发射一个投掷物。
    /// </summary>
    public void SpawnProjectile(FighterState owner, FighterState target, ThrowableDefinition throwable)
    {
        SpawnProjectile(owner, target, throwable, throwable.Damage);
    }

    public void SpawnProjectile(FighterState owner, FighterState target, ThrowableDefinition throwable, double damage)
    {
        SpawnProjectile(owner, target, throwable, damage, false);
    }

    public void SpawnProjectile(FighterState owner, FighterState target, ThrowableDefinition throwable, double damage, bool dealDamageOnlyIfTargetSleeping)
    {
        var baseDirection = (target.Position - owner.Position).Normalized();
        var direction = ApplyAccuracySpread(baseDirection, owner.Definition.Accuracy);
        SpawnProjectileInDirection(owner, target, throwable, direction, damage, dealDamageOnlyIfTargetSleeping);
    }

    public void SpawnProjectileInDirection(FighterState owner, FighterState target, ThrowableDefinition throwable, Vec2 direction, double damage, bool dealDamageOnlyIfTargetSleeping = false)
    {
        var normalizedDirection = direction.Length <= 0.0001 ? new Vec2(1, 0) : direction.Normalized();
        var spawnPosition = owner.Position + (normalizedDirection * (owner.Definition.Radius + throwable.Radius + 4));

        Projectiles.Add(new BattleProjectile
        {
            Id = $"projectile-{Guid.NewGuid():N}"[..24],
            OwnerId = owner.Id,
            TargetId = target.Id,
            Name = throwable.Name,
            TexturePath = throwable.TexturePath,
            Position = spawnPosition,
            Velocity = normalizedDirection * throwable.Speed,
            Radius = throwable.Radius,
            Damage = damage,
            ColorHex = throwable.ColorHex,
            RemainingLifeTime = BattleTuning.ProjectileLifeTime,
            CanSleepTarget = throwable.CanSleepTarget,
            SleepDuration = throwable.SleepDuration,
            DealDamageOnlyIfTargetSleeping = dealDamageOnlyIfTargetSleeping,
            BounceOnWalls = false,
            CanBeReclaimedByOwner = false,
            ReclaimHealRatio = 0,
            KeepOnFighterHit = false
        });
    }

    public void SpawnProjectile(DroneState owner, FighterState target, ThrowableDefinition throwable)
    {
        var direction = (target.Position - owner.Position).Normalized();
        var spawnPosition = owner.Position + (direction * (owner.Radius + throwable.Radius + 4));

        Projectiles.Add(new BattleProjectile
        {
            Id = $"projectile-{Guid.NewGuid():N}"[..24],
            OwnerId = owner.Id,
            TargetId = target.Id,
            Name = throwable.Name,
            TexturePath = throwable.TexturePath,
            Position = spawnPosition,
            Velocity = direction * throwable.Speed,
            Radius = throwable.Radius,
            Damage = throwable.Damage,
            ColorHex = throwable.ColorHex,
            RemainingLifeTime = BattleTuning.ProjectileLifeTime,
            BounceOnWalls = false,
            CanBeReclaimedByOwner = false,
            ReclaimHealRatio = 0,
            KeepOnFighterHit = false
        });
    }

    public void SpawnIkunBasketball(FighterState owner, FighterState target)
    {
        var throwable = ThrowableCatalog.Get(IkunKey);
        var baseDirection = (target.Position - owner.Position).Normalized();
        var direction = ApplyAccuracySpread(baseDirection, owner.Definition.Accuracy);
        var spawnPosition = owner.Position + (direction * (owner.Definition.Radius + throwable.Radius + 4));

        Projectiles.Add(new BattleProjectile
        {
            Id = $"projectile-{Guid.NewGuid():N}"[..24],
            OwnerId = owner.Id,
            TargetId = target.Id,
            Name = throwable.Name,
            TexturePath = throwable.TexturePath,
            Position = spawnPosition,
            Velocity = direction * throwable.Speed,
            Radius = throwable.Radius,
            Damage = throwable.Damage,
            ColorHex = throwable.ColorHex,
            RemainingLifeTime = BattleTuning.ProjectileLifeTime,
            BounceOnWalls = true,
            CanBeReclaimedByOwner = true,
            ReclaimHealRatio = 0.5,
            KeepOnFighterHit = true
        });
    }

    public void StartBurstFire(FighterState owner, FighterState target, string throwableKey, int shotCount, double intervalSeconds, double? damageOverride = null)
    {
        owner.BurstTargetId = target.Id;
        owner.BurstThrowableKey = throwableKey;
        owner.BurstShotInterval = intervalSeconds;
        owner.BurstShotsRemaining = Math.Max(0, shotCount);
        owner.BurstShotTimer = 0;
        owner.BurstShotDamageOverride = damageOverride;
    }

    public void PrepareQzdNextAttack(FighterState fighter)
    {
        if (!fighter.Definition.Key.Equals(QzdSkillKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        fighter.ChargePreviewShotCount = _random.Next(1, QzdMaxPreviewValue + 1);
        fighter.ChargePreviewDamage = _random.Next(1, QzdMaxPreviewValue + 1);
    }

    public int CountDrones(string side)
    {
        return Drones.Count(x => x.IsAlive && x.Side.Equals(side, StringComparison.OrdinalIgnoreCase));
    }

    public void SummonDrone(FighterState owner)
    {
        var droneDefinition = ThrowableCatalog.Get("drone");
        var summonPosition = ClampSummonPosition(owner.Position, droneDefinition.UnitRadius ?? DefaultDroneRadius);

        Drones.Add(new DroneState
        {
            Id = $"drone-{Guid.NewGuid():N}"[..18],
            OwnerId = owner.Id,
            Side = owner.Side,
            Name = $"{owner.Definition.Name}无人机",
            TexturePath = droneDefinition.UnitTexturePath ?? "roles/drone.png",
            Position = summonPosition,
            Radius = droneDefinition.UnitRadius ?? DefaultDroneRadius,
            HP = droneDefinition.unitHP ?? DefaultDroneHP,
            Health = droneDefinition.unitHP ?? DefaultDroneHP,
            AttackInterval = droneDefinition.unitCD ?? DefaultDroneAttackInterval,
            AttackTimer = droneDefinition.unitCD ?? DefaultDroneAttackInterval,
            ThrowableKey = droneDefinition.Key
        });

        SpawnImpact(summonPosition, GetSecondaryColor(owner.Side));
    }

    private FighterState CreateFighter(FighterDefinition definition, string side, Vec2 position, Vec2 velocity)
    {
        var fighter = new FighterState
        {
            Id = $"{side}-{definition.Key}-{Guid.NewGuid():N}"[..18],
            Side = side,
            Definition = definition,
            Position = position,
            Velocity = velocity,
            Health = definition.HP,
            SkillTimer = definition.CD,
            BurstShotsRemaining = 0,
            BurstShotInterval = 0,
            BurstShotTimer = 0,
            BurstTargetId = null,
            BurstThrowableKey = null,
            BurstShotDamageOverride = null,
            ChargePreviewShotCount = 0,
            ChargePreviewDamage = 0,
            IkunBasketballCount = definition.Key.Equals(IkunKey, StringComparison.OrdinalIgnoreCase) ? 2 : 0,
            WatcherIsAngry = false,
            SkillFlashTime = 0
        };

        PrepareQzdNextAttack(fighter);
        return fighter;
    }

    /// <summary>
    /// 更新单个角色：先按速度移动并处理墙壁反弹，再更新攻击 CD。
    /// </summary>
    private void UpdateFighter(FighterState self, FighterState enemy, double dt)
    {
        if (!self.IsAlive)
        {
            return;
        }

        var wasSleeping = self.IsSleeping;
        self.SleepTime = Math.Max(0, self.SleepTime - dt);

        if (!wasSleeping)
        {
            self.Position += self.Velocity * dt;
            BounceOnWalls(self);
            KeepMinimumSpeed(self);

            var canAttackNow = CanAttackNow(self);
            if (canAttackNow)
            {
                self.SkillTimer -= dt;
                UpdateBurstFire(self, dt);

                if (self.SkillTimer <= 0 && enemy.IsAlive)
                {
                    UseSkill(self, enemy);
                    self.SkillTimer += self.Definition.CD;
                }
            }
        }

        self.SkillFlashTime = Math.Max(0, self.SkillFlashTime - dt);
    }

    private void UpdateBurstFire(FighterState fighter, double dt)
    {
        if (fighter.BurstShotsRemaining <= 0 || string.IsNullOrWhiteSpace(fighter.BurstThrowableKey) || string.IsNullOrWhiteSpace(fighter.BurstTargetId))
        {
            return;
        }

        fighter.BurstShotTimer -= dt;
        while (fighter.BurstShotsRemaining > 0 && fighter.BurstShotTimer <= 0)
        {
            var target = Fighters.FirstOrDefault(x => x.Id == fighter.BurstTargetId);
            if (target is null || !target.IsAlive)
            {
                fighter.BurstShotsRemaining = 0;
                fighter.BurstTargetId = null;
                fighter.BurstThrowableKey = null;
                fighter.BurstShotDamageOverride = null;
                fighter.BurstShotTimer = 0;
                return;
            }

            var throwable = ThrowableCatalog.Get(fighter.BurstThrowableKey);
            var damage = fighter.BurstShotDamageOverride ?? throwable.Damage;
            var dealDamageOnlyIfTargetSleeping = fighter.BurstThrowableKey.Equals("lq", StringComparison.OrdinalIgnoreCase);
            SpawnProjectile(fighter, target, throwable, damage, dealDamageOnlyIfTargetSleeping);
            fighter.BurstShotsRemaining--;
            fighter.BurstShotTimer += fighter.BurstShotInterval;
        }

        if (fighter.BurstShotsRemaining <= 0)
        {
            fighter.BurstTargetId = null;
            fighter.BurstThrowableKey = null;
            fighter.BurstShotDamageOverride = null;
            fighter.BurstShotTimer = 0;
        }
    }

    private void UpdateDrones(double dt)
    {
        for (var i = Drones.Count - 1; i >= 0; i--)
        {
            var drone = Drones[i];
            if (!drone.IsAlive)
            {
                Drones.RemoveAt(i);
                continue;
            }

            drone.AttackTimer -= dt;
            while (drone.AttackTimer <= 0)
            {
                var target = FindNearestEnemyFighter(drone.Side, drone.Position);
                if (target is not null && target.IsAlive)
                {
                    SpawnProjectile(drone, target, ThrowableCatalog.Get(drone.ThrowableKey));
                    drone.AttackTimer += drone.AttackInterval;
                }
                else
                {
                    drone.AttackTimer = drone.AttackInterval;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 角色碰到场地边界时反弹。只改变方向，不通过阻尼降低速度。
    /// </summary>
    private void BounceOnWalls(FighterState fighter)
    {
        var radius = fighter.Definition.Radius;
        var position = fighter.Position;
        var velocity = fighter.Velocity;
        var bounced = false;

        if (position.X - radius < 0)
        {
            position = position with { X = radius };
            velocity = velocity with { X = Math.Abs(velocity.X) };
            bounced = true;
        }
        else if (position.X + radius > Arena.Width)
        {
            position = position with { X = Arena.Width - radius };
            velocity = velocity with { X = -Math.Abs(velocity.X) };
            bounced = true;
        }

        if (position.Y - radius < 0)
        {
            position = position with { Y = radius };
            velocity = velocity with { Y = Math.Abs(velocity.Y) };
            bounced = true;
        }
        else if (position.Y + radius > Arena.Height)
        {
            position = position with { Y = Arena.Height - radius };
            velocity = velocity with { Y = -Math.Abs(velocity.Y) };
            bounced = true;
        }

        fighter.Position = position;
        fighter.Velocity = velocity;

        if (bounced)
        {
            SpawnImpact(position, GetPrimaryColor(fighter.Side));
        }
    }

    /// <summary>
    /// 两个角色身体接触时只反弹，不造成伤害。
    /// </summary>
    private static void ResolveFighterCollision(FighterState a, FighterState b)
    {
        if (!a.IsAlive || !b.IsAlive)
        {
            return;
        }

        var delta = b.Position - a.Position;
        var distance = Math.Max(0.0001, delta.Length);
        var minimumDistance = a.Definition.Radius + b.Definition.Radius;
        if (distance >= minimumDistance)
        {
            return;
        }

        var normal = delta / distance;
        var overlap = minimumDistance - distance;
        a.Position -= normal * (overlap * 0.5);
        b.Position += normal * (overlap * 0.5);

        var relativeVelocity = b.Velocity - a.Velocity;
        var separatingVelocity = Vec2.Dot(relativeVelocity, normal);
        if (separatingVelocity >= 0)
        {
            return;
        }

        const double restitution = 1.0;
        var impulse = normal * (-(1 + restitution) * separatingVelocity / 2);
        a.Velocity -= impulse;
        b.Velocity += impulse;
    }

    /// <summary>
    /// 防止角色因为外部速度修改过小而停住。
    /// </summary>
    private static void KeepMinimumSpeed(FighterState fighter)
    {
        var Speed = fighter.Velocity.Length;
        if (Speed > 0.001 && Speed < BattleTuning.MinimumSpeed)
        {
            fighter.Velocity = fighter.Velocity.Normalized() * BattleTuning.MinimumSpeed;
        }
    }

    /// <summary>
    /// 生成一个随机方向的初始速度。
    /// </summary>
    private Vec2 CreateRandomVelocity(FighterDefinition definition)
    {
        var angle = RandomRange(0, Math.PI * 2);
        return new Vec2(Math.Cos(angle), Math.Sin(angle)) * definition.Speed;
    }

    private Vec2 ApplyAccuracySpread(Vec2 direction, double Accuracy)
    {
        if (direction.Length <= 0.0001)
        {
            return new Vec2(1, 0);
        }

        var clampedAccuracy = Math.Clamp(Accuracy, 0, 100);
        var spreadDegrees = Math.Min(MaxFighterProjectileSpreadAngleDegrees, (100 - clampedAccuracy) / 2.0);
        if (spreadDegrees <= 0.0001)
        {
            return direction;
        }

        var spreadRadians = spreadDegrees * Math.PI / 180.0;
        var offsetAngle = RandomRange(-spreadRadians, spreadRadians);
        return Rotate(direction, offsetAngle).Normalized();
    }

    private static Vec2 Rotate(Vec2 vector, double angle)
    {
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        return new Vec2(
            (vector.X * cos) - (vector.Y * sin),
            (vector.X * sin) + (vector.Y * cos));
    }

    private Vec2 ClampSummonPosition(Vec2 position, double radius)
    {
        return new Vec2(
            Math.Clamp(position.X, radius, Arena.Width - radius),
            Math.Clamp(position.Y, radius, Arena.Height - radius));
    }

    private FighterState? FindNearestEnemyFighter(string side, Vec2 fromPosition)
    {
        return Fighters
            .Where(x => x.IsAlive && !x.Side.Equals(side, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => (x.Position - fromPosition).Length)
            .FirstOrDefault();
    }

    /// <summary>
    /// 更新所有投掷物位置并判定是否命中目标。
    /// 命中后直接扣血，不再依赖角色身体碰撞。
    /// </summary>
    private void UpdateProjectiles(double dt)
    {
        for (var i = Projectiles.Count - 1; i >= 0; i--)
        {
            var projectile = Projectiles[i];
            projectile.Position += projectile.Velocity * dt;

            var owner = Fighters.FirstOrDefault(x => x.Id == projectile.OwnerId);
            if (owner is not null
                && owner.IsAlive
                && projectile.CanBeReclaimedByOwner
                && IsProjectileHit(projectile, owner.Position, owner.Definition.Radius))
            {
                owner.Health = Math.Min(owner.Definition.HP, owner.Health + (projectile.Damage * projectile.ReclaimHealRatio));
                if (owner.Definition.Key.Equals(IkunKey, StringComparison.OrdinalIgnoreCase))
                {
                    owner.IkunBasketballCount++;
                }
                SpawnImpact(projectile.Position, projectile.ColorHex);
                Projectiles.RemoveAt(i);
                continue;
            }

            if (projectile.BounceOnWalls)
            {
                BounceProjectileOnWalls(projectile);
            }
            else if (IsProjectileHitWall(projectile))
            {
                SpawnImpact(projectile.Position, projectile.ColorHex);
                Projectiles.RemoveAt(i);
                continue;
            }

            var ownerSide = Fighters.FirstOrDefault(x => x.Id == projectile.OwnerId)?.Side
                ?? Drones.FirstOrDefault(x => x.Id == projectile.OwnerId)?.Side;

            if (!string.IsNullOrWhiteSpace(ownerSide))
            {
                var hitDrone = Drones
                    .Where(x => x.IsAlive && !x.Side.Equals(ownerSide, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => (x.Position - projectile.Position).Length)
                    .FirstOrDefault(x => IsProjectileHit(projectile, x.Position, x.Radius));

                if (hitDrone is not null)
                {
                    hitDrone.Health -= projectile.Damage;
                    SpawnImpact(projectile.Position, projectile.ColorHex);
                    SpawnDamageText(hitDrone.Position, projectile.Damage, hitDrone.Side);
                    if (!hitDrone.IsAlive)
                    {
                        SpawnExplosion(hitDrone.Position, GetPrimaryColor(hitDrone.Side));
                        Drones.Remove(hitDrone);
                    }

                    if (projectile.KeepOnFighterHit)
                    {
                        BounceProjectileOnUnit(projectile, hitDrone.Position, hitDrone.Radius);
                    }
                    else
                    {
                        Projectiles.RemoveAt(i);
                    }
                    continue;
                }
            }

            var target = Fighters.FirstOrDefault(x => x.Id == projectile.TargetId);
            if (target is not null && target.IsAlive && IsProjectileHit(projectile, target))
            {
                var canDealDamage = !projectile.DealDamageOnlyIfTargetSleeping || target.IsSleeping;
                if (canDealDamage)
                {
                    var damage = ApplyDamageToFighter(target, projectile.Damage, owner);
                    SpawnDamageText(target.Position, damage, target.Side);
                    HandleFighterDamaged(target, damage);
                    HandleSuccessfulFighterHit(owner, target, damage);
                }
                else if (projectile.CanSleepTarget)
                {
                    target.SleepTime = Math.Max(target.SleepTime, projectile.SleepDuration);
                }

                SpawnImpact(projectile.Position, projectile.ColorHex);
                if (projectile.KeepOnFighterHit)
                {
                    BounceProjectileOnFighter(projectile, target);
                }
                else
                {
                    Projectiles.RemoveAt(i);
                }
                continue;
            }

            if (IsOutsideArena(projectile.Position, projectile.Radius))
            {
                Projectiles.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 角色 Key 同时也是技能 Key，根据角色 Key 找到技能类并执行。
    /// </summary>
    private void UseSkill(FighterState self, FighterState enemy)
    {
        var skill = SkillRegistry.Get(self.Definition.Key);
        skill.Execute(new SkillContext
        {
            World = this,
            Caster = self,
            Target = enemy,
            RandomRange = RandomRange
        });
    }

    private void CheckResult()
    {
        var alive = Fighters.Where(x => x.IsAlive).ToList();
        if (alive.Count == 2)
        {
            return;
        }

        if (alive.Count == 1)
        {
            Winner = alive[0];
            foreach (var dead in Fighters.Where(x => !x.IsAlive))
            {
                SpawnExplosion(dead.Position, GetPrimaryColor(dead.Side));
            }
            StatusText = $"{Winner.Definition.Name} 获胜！";
            return;
        }

        IsDraw = true;
        foreach (var dead in Fighters.Where(x => !x.IsAlive))
        {
            SpawnExplosion(dead.Position, GetPrimaryColor(dead.Side));
        }
        StatusText = "双败平局！";
    }

    private void UpdateEffects(double dt)
    {
        for (var i = Effects.Count - 1; i >= 0; i--)
        {
            var effect = Effects[i];
            effect.RemainingTime -= dt;
            effect.Radius += effect.GrowthPerSecond * dt;
            if (effect.RemainingTime <= 0)
            {
                Effects.RemoveAt(i);
            }
        }
    }

    private void SpawnImpact(Vec2 position, string colorHex)
    {
        Effects.Add(new BattleEffect
        {
            Type = BattleEffectType.Impact,
            Position = position,
            Radius = 14,
            RemainingTime = 0.22,
            GrowthPerSecond = 45,
            ColorHex = colorHex
        });
    }

    private void SpawnDamageText(Vec2 targetPosition, double damage, string side)
    {
        Effects.Add(new BattleEffect
        {
            Type = BattleEffectType.DamageText,
            Position = targetPosition + new Vec2(RandomRange(-28, 28), -72 + RandomRange(-12, 8)),
            Text = $"-{damage:0}",
            Radius = 0,
            RemainingTime = 0.8,
            GrowthPerSecond = 0,
            ColorHex = side.Equals("left", StringComparison.OrdinalIgnoreCase) ? "#5AA9FF" : "#FF6B7A"
        });
    }

    private void SpawnExplosion(Vec2 position, string colorHex)
    {
        Effects.Add(new BattleEffect
        {
            Type = BattleEffectType.Explosion,
            Position = position,
            Radius = 34,
            RemainingTime = 0.85,
            GrowthPerSecond = 245,
            ColorHex = colorHex
        });

        for (var i = 0; i < 10; i++)
        {
            Effects.Add(new BattleEffect
            {
                Type = BattleEffectType.Impact,
                Position = position + new Vec2(RandomRange(-55, 55), RandomRange(-55, 55)),
                Radius = RandomRange(10, 22),
                RemainingTime = RandomRange(0.25, 0.55),
                GrowthPerSecond = RandomRange(70, 150),
                ColorHex = i % 2 == 0 ? colorHex : "#FFE08A"
            });
        }
    }

    private static string GetPrimaryColor(string side)
    {
        return side.Equals("left", StringComparison.OrdinalIgnoreCase) ? "#5AA9FF" : "#FF6B7A";
    }

    private static string GetSecondaryColor(string side)
    {
        return side.Equals("left", StringComparison.OrdinalIgnoreCase) ? "#8FC8FF" : "#FFB3BC";
    }

    private static bool IsProjectileHit(BattleProjectile projectile, FighterState target)
    {
        return IsProjectileHit(projectile, target.Position, target.Definition.Radius);
    }

    private bool CanAttackNow(FighterState fighter)
    {
        return !fighter.Definition.Key.Equals(WatcherKey, StringComparison.OrdinalIgnoreCase)
            || ElapsedTime >= WatcherAttackDelaySeconds;
    }

    private double ApplyDamageToFighter(FighterState target, double baseDamage, FighterState? attacker = null)
    {
        var damage = baseDamage;

        if (attacker is not null
            && attacker.IsAlive
            && attacker.Definition.Key.Equals(WatcherKey, StringComparison.OrdinalIgnoreCase)
            && attacker.WatcherIsAngry)
        {
            damage *= 2;
        }

        if (target.Definition.Key.Equals(WatcherKey, StringComparison.OrdinalIgnoreCase)
            && target.WatcherIsAngry)
        {
            damage *= 2;
        }

        target.Health -= damage;
        return damage;
    }

    private void HandleSuccessfulFighterHit(FighterState? attacker, FighterState target, double damage)
    {
        if (attacker is null
            || !attacker.IsAlive
            || !attacker.Definition.Key.Equals(WatcherKey, StringComparison.OrdinalIgnoreCase)
            || attacker.Id == target.Id)
        {
            return;
        }

        attacker.WatcherSuccessfulHitCount++;

        if (attacker.WatcherSuccessfulHitCount == 1)
        {
            attacker.WatcherIsAngry = true;
            attacker.SkillFlashTime = Math.Max(attacker.SkillFlashTime, 0.35);
            return;
        }

        if (!attacker.WatcherIsAngry)
        {
            attacker.Health = Math.Min(attacker.Definition.HP, attacker.Health + damage);
        }

        attacker.WatcherIsAngry = !attacker.WatcherIsAngry;
        attacker.SkillFlashTime = Math.Max(attacker.SkillFlashTime, 0.35);
    }

    private void HandleFighterDamaged(FighterState target, double damage)
    {
        if (!target.IsAlive
            || damage < 4
            || !target.Definition.Key.Equals(ReflectorKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var enemy = Fighters.FirstOrDefault(x => x.IsAlive && !x.Side.Equals(target.Side, StringComparison.OrdinalIgnoreCase));
        if (enemy is null)
        {
            return;
        }

        var spike = ThrowableCatalog.Get(ReflectorKey);
        var counterDamage = damage * 0.3;
        FireRadialProjectiles(target, enemy, spike, counterDamage);
        target.SkillFlashTime = Math.Max(target.SkillFlashTime, 0.2);
    }

    public void FireRadialProjectiles(FighterState owner, FighterState target, ThrowableDefinition throwable, double damage, int directionCount = 8)
    {
        var count = Math.Max(1, directionCount);
        for (var i = 0; i < count; i++)
        {
            var angle = (Math.PI * 2 * i) / count;
            var direction = new Vec2(Math.Cos(angle), Math.Sin(angle));
            SpawnProjectileInDirection(owner, target, throwable, direction, damage);
        }
    }

    private static bool IsProjectileHit(BattleProjectile projectile, Vec2 targetPosition, double targetRadius)
    {
        var hitDistance = projectile.Radius + targetRadius;
        return (projectile.Position - targetPosition).Length <= hitDistance;
    }

    private static bool IsProjectileHitWall(BattleProjectile projectile, ArenaDefinition arena)
    {
        var position = projectile.Position;
        var radius = projectile.Radius;
        return position.X - radius < 0
            || position.X + radius > arena.Width
            || position.Y - radius < 0
            || position.Y + radius > arena.Height;
    }

    private bool IsProjectileHitWall(BattleProjectile projectile)
    {
        return IsProjectileHitWall(projectile, Arena);
    }

    private void BounceProjectileOnWalls(BattleProjectile projectile)
    {
        var position = projectile.Position;
        var velocity = projectile.Velocity;
        var radius = projectile.Radius;

        if (position.X - radius < 0)
        {
            position = position with { X = radius };
            velocity = velocity with { X = Math.Abs(velocity.X) };
        }
        else if (position.X + radius > Arena.Width)
        {
            position = position with { X = Arena.Width - radius };
            velocity = velocity with { X = -Math.Abs(velocity.X) };
        }

        if (position.Y - radius < 0)
        {
            position = position with { Y = radius };
            velocity = velocity with { Y = Math.Abs(velocity.Y) };
        }
        else if (position.Y + radius > Arena.Height)
        {
            position = position with { Y = Arena.Height - radius };
            velocity = velocity with { Y = -Math.Abs(velocity.Y) };
        }

        projectile.Position = position;
        projectile.Velocity = velocity;
    }

    private void BounceProjectileOnFighter(BattleProjectile projectile, FighterState target)
    {
        BounceProjectileOnUnit(projectile, target.Position, target.Definition.Radius);
    }

    private void BounceProjectileOnUnit(BattleProjectile projectile, Vec2 targetPosition, double targetRadius)
    {
        var hitNormal = (projectile.Position - targetPosition).Normalized();
        if (hitNormal.Length <= 0.0001)
        {
            hitNormal = projectile.Velocity.Length <= 0.0001
                ? new Vec2(1, 0)
                : projectile.Velocity.Normalized();
        }

        var speed = Math.Max(projectile.Velocity.Length, 1);
        projectile.Velocity = hitNormal * speed;
        projectile.Position = targetPosition + (hitNormal * (targetRadius + projectile.Radius + 1));
    }

    private bool IsOutsideArena(Vec2 position, double radius)
    {
        return position.X + radius < 0
            || position.X - radius > Arena.Width
            || position.Y + radius < 0
            || position.Y - radius > Arena.Height;
    }

    private double RandomRange(double min, double max) => min + (_random.NextDouble() * (max - min));
}
