using BrawlGuys.Core.Skills;

namespace BrawlGuys.Core;

/// <summary>
/// 一局战斗的核心模拟器。
/// 这里尽量只保留所有角色共享的战斗流程：移动、碰撞、投掷物、召唤物、特效与胜负判定。
/// 角色差异逻辑统一交给技能接口扩展，避免 BattleWorld 持续膨胀。
/// </summary>
public sealed class BattleWorld
{
    private const double DefaultDroneAttackInterval = 2.0;
    private const double DefaultDroneHP = 50;
    private const double DefaultDroneRadius = 25;
    private const double MaxFighterProjectileSpreadAngleDegrees = 50;

    private readonly Random _random = new();

    /// <summary>
    /// 当前竞技场宽度。
    /// </summary>
    public double ArenaWidth => BattleTuning.ArenaSize;

    /// <summary>
    /// 当前竞技场高度。
    /// </summary>
    public double ArenaHeight => BattleTuning.ArenaSize;

    /// <summary>
    /// 当前局内角色列表。当前版本固定为左右两名角色。
    /// </summary>
    public List<FighterState> Fighters { get; } = new();

    /// <summary>
    /// 当前局内存活/待清理的召唤物列表。
    /// </summary>
    public List<SummonableState> Summonables { get; } = new();

    /// <summary>
    /// 当前所有在飞行中的投掷物。
    /// </summary>
    public List<BattleProjectile> Projectiles { get; } = new();

    /// <summary>
    /// 当前所有表现层特效。
    /// </summary>
    public List<BattleEffect> Effects { get; } = new();

    /// <summary>
    /// 展示给 UI 的状态文本。
    /// </summary>
    public string StatusText { get; private set; } = "准备开始";

    /// <summary>
    /// 当前胜者。未分出胜负时为 null。
    /// </summary>
    public FighterState? Winner { get; private set; }

    /// <summary>
    /// 是否为双败平局。
    /// </summary>
    public bool IsDraw { get; private set; }

    /// <summary>
    /// 本局已经推进的总时间，单位秒。
    /// </summary>
    public double ElapsedTime { get; private set; }

    /// <summary>
    /// 开始一局新比赛：创建左右两个角色，并给他们随机初始移动方向。
    /// 创建完成后，会立即触发每个角色技能的开场初始化钩子。
    /// </summary>
    public void StartMatch(string leftKey, string rightKey)
    {
        Fighters.Clear();
        Summonables.Clear();
        Projectiles.Clear();
        Effects.Clear();
        Winner = null;
        IsDraw = false;
        ElapsedTime = 0;
        StatusText = "战斗中";

        var leftDefinition = FighterCatalog.Get(leftKey);
        var rightDefinition = FighterCatalog.Get(rightKey);

        var left = CreateFighter(
            leftDefinition,
            "left",
            new Vec2(BattleTuning.FighterSidePadding, ArenaHeight / 2),
            CreateRandomVelocity(leftDefinition));

        var right = CreateFighter(
            rightDefinition,
            "right",
            new Vec2(ArenaWidth - BattleTuning.FighterSidePadding, ArenaHeight / 2),
            CreateRandomVelocity(rightDefinition));

        Fighters.Add(left);
        Fighters.Add(right);

        InitializeFighterForMatch(left, right);
        InitializeFighterForMatch(right, left);
    }

    /// <summary>
    /// 推进一帧战斗模拟。
    /// 为了避免大 dt 导致穿模，这里会自动切成多个小步推进。
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

    public void SpawnProjectile(
        FighterState owner,
        FighterState target,
        double? damageOverride = null,
        bool dealDamageOnlyIfTargetSleeping = false,
        bool bounceOnWalls = false,
        bool canBeReclaimedByOwner = false,
        double reclaimHealRatio = 0,
        bool keepOnFighterHit = false)
    {
        SpawnProjectile(
            owner,
            target,
            GetThrowable(owner),
            damageOverride,
            dealDamageOnlyIfTargetSleeping,
            bounceOnWalls,
            canBeReclaimedByOwner,
            reclaimHealRatio,
            keepOnFighterHit);
    }

    /// <summary>
    /// 从角色朝目标发射一个投掷物。
    /// 命中率偏移、出生点计算和通用投射规则都在这里统一处理。
    /// </summary>
    public void SpawnProjectile(
        FighterState owner,
        FighterState target,
        ThrowableDefinition throwable,
        double? damageOverride = null,
        bool dealDamageOnlyIfTargetSleeping = false,
        bool bounceOnWalls = false,
        bool canBeReclaimedByOwner = false,
        double reclaimHealRatio = 0,
        bool keepOnFighterHit = false)
    {
        var baseDirection = (target.Position - owner.Position).Normalized();
        var direction = ApplyAccuracySpread(baseDirection, owner.Definition.Accuracy);
        SpawnProjectileInDirection(
            owner,
            target,
            throwable,
            direction,
            damageOverride,
            dealDamageOnlyIfTargetSleeping,
            bounceOnWalls,
            canBeReclaimedByOwner,
            reclaimHealRatio,
            keepOnFighterHit);
    }

    public void SpawnProjectileInDirection(
        FighterState owner,
        FighterState target,
        Vec2 direction,
        double? damageOverride = null,
        bool dealDamageOnlyIfTargetSleeping = false,
        bool bounceOnWalls = false,
        bool canBeReclaimedByOwner = false,
        double reclaimHealRatio = 0,
        bool keepOnFighterHit = false)
    {
        SpawnProjectileInDirection(
            owner,
            target,
            GetThrowable(owner),
            direction,
            damageOverride,
            dealDamageOnlyIfTargetSleeping,
            bounceOnWalls,
            canBeReclaimedByOwner,
            reclaimHealRatio,
            keepOnFighterHit);
    }

    /// <summary>
    /// 从角色朝指定方向发射一个投掷物。
    /// 适用于环形散射、穿刺反弹等不依赖目标方向的技能。
    /// </summary>
    public void SpawnProjectileInDirection(
        FighterState owner,
        FighterState target,
        ThrowableDefinition throwable,
        Vec2 direction,
        double? damageOverride = null,
        bool dealDamageOnlyIfTargetSleeping = false,
        bool bounceOnWalls = false,
        bool canBeReclaimedByOwner = false,
        double reclaimHealRatio = 0,
        bool keepOnFighterHit = false)
    {
        var normalizedDirection = direction.Length <= 0.0001 ? new Vec2(1, 0) : direction.Normalized();
        var spawnPosition = owner.Position + (normalizedDirection * (owner.Definition.Radius + throwable.Radius + 4));
        Projectiles.Add(CreateProjectile(
            owner.Id,
            target.Id,
            spawnPosition,
            normalizedDirection * throwable.Speed,
            throwable,
            damageOverride,
            dealDamageOnlyIfTargetSleeping,
            bounceOnWalls,
            canBeReclaimedByOwner,
            reclaimHealRatio,
            keepOnFighterHit));
    }

    /// <summary>
    /// 从无人机朝目标发射一个投掷物。
    /// 无人机不受角色命中率偏移影响。
    /// </summary>
    public void SpawnProjectile(
        SummonableState owner,
        FighterState target,
        ThrowableDefinition throwable,
        double? damageOverride = null,
        bool dealDamageOnlyIfTargetSleeping = false,
        bool bounceOnWalls = false,
        bool canBeReclaimedByOwner = false,
        double reclaimHealRatio = 0,
        bool keepOnFighterHit = false)
    {
        var direction = (target.Position - owner.Position).Normalized();
        var spawnPosition = owner.Position + (direction * (owner.Radius + throwable.Radius + 4));
        Projectiles.Add(CreateProjectile(
            owner.Id,
            target.Id,
            spawnPosition,
            direction * throwable.Speed,
            throwable,
            damageOverride,
            dealDamageOnlyIfTargetSleeping,
            bounceOnWalls,
            canBeReclaimedByOwner,
            reclaimHealRatio,
            keepOnFighterHit));
    }

    /// <summary>
    /// 启动一段通用连发流程。
    /// 连发本身属于公共战斗机制，所以放在世界层统一维护。
    /// </summary>
    public void StartBurstFire(
        FighterState owner,
        FighterState target,
        string throwableKey,
        int shotCount,
        double intervalSeconds,
        double? damageOverride = null,
        bool dealDamageOnlyIfTargetSleeping = false)
    {
        owner.ActiveBurstFire = new BurstFireState
        {
            TargetId = target.Id,
            ThrowableKey = throwableKey,
            ShotsRemaining = Math.Max(0, shotCount),
            ShotInterval = Math.Max(0, intervalSeconds),
            ShotTimer = 0,
            DamageOverride = damageOverride,
            DealDamageOnlyIfTargetSleeping = dealDamageOnlyIfTargetSleeping
        };
    }

    /// <summary>
    /// 统计某一侧当前存活的召唤物数量。
    /// </summary>
    public int CountAliveSummonables(string side, string? attackThrowableKey = null)
    {
        return Summonables.Count(x => x.IsAlive
            && x.Side.Equals(side, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(attackThrowableKey)
                || x.AttackThrowableKey.Equals(attackThrowableKey, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// 生成一个召唤物。
    /// 召唤时机由技能决定，生成与基础战斗规则由世界层处理。
    /// </summary>
    public void SpawnSummonable(
        FighterState owner,
        string summonableKey,
        string attackThrowableKey,
        string? texturePath = null,
        double? radiusOverride = null,
        double? hpOverride = null,
        double? attackIntervalOverride = null)
    {
        var summonableDefinition = ThrowableCatalog.Get(summonableKey);
        var radius = radiusOverride ?? summonableDefinition.UnitRadius ?? DefaultDroneRadius;
        var hp = hpOverride ?? summonableDefinition.unitHP ?? DefaultDroneHP;
        var attackInterval = attackIntervalOverride ?? summonableDefinition.unitCD ?? DefaultDroneAttackInterval;
        var summonPosition = ClampSummonPosition(owner.Position, radius);

        Summonables.Add(new SummonableState
        {
            Id = $"summonable-{Guid.NewGuid():N}"[..23],
            OwnerId = owner.Id,
            Side = owner.Side,
            TexturePath = texturePath ?? summonableDefinition.UnitTexturePath ?? "roles/drone.png",
            Position = summonPosition,
            Radius = radius,
            HP = hp,
            Health = hp,
            AttackInterval = attackInterval,
            AttackTimer = attackInterval,
            AttackThrowableKey = attackThrowableKey
        });

        SpawnImpact(summonPosition, GetSecondaryColor(owner.Side));
    }

    public void FireRadialProjectiles(FighterState owner, FighterState target, double damage, int directionCount = 8)
    {
        FireRadialProjectiles(owner, target, GetThrowable(owner), damage, directionCount);
    }

    /// <summary>
    /// 以角色为中心向多个方向发射投掷物。
    /// </summary>
    public void FireRadialProjectiles(FighterState owner, FighterState target, ThrowableDefinition throwable, double damage, int directionCount = 8)
    {
        var count = Math.Max(1, directionCount);
        for (var i = 0; i < count; i++)
        {
            var angle = (Math.PI * 2 * i) / count;
            var direction = new Vec2(Math.Cos(angle), Math.Sin(angle));
            SpawnProjectileInDirection(
                owner,
                target,
                throwable,
                direction,
                damageOverride: damage);
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
            UpdateSummonables(dt);
            ResolveFighterCollision(left, right);
            UpdateProjectiles(dt);
            CheckResult();
        }

        UpdateEffects(dt);
    }

    private BattleProjectile CreateProjectile(
        string ownerId,
        string targetId,
        Vec2 position,
        Vec2 velocity,
        ThrowableDefinition throwable,
        double? damageOverride = null,
        bool dealDamageOnlyIfTargetSleeping = false,
        bool bounceOnWalls = false,
        bool canBeReclaimedByOwner = false,
        double reclaimHealRatio = 0,
        bool keepOnFighterHit = false)
    {
        return new BattleProjectile
        {
            Id = $"projectile-{Guid.NewGuid():N}"[..24],
            OwnerId = ownerId,
            TargetId = targetId,
            TexturePath = throwable.TexturePath,
            Position = position,
            Velocity = velocity,
            Radius = throwable.Radius,
            Damage = damageOverride ?? throwable.Damage,
            ColorHex = throwable.ColorHex,
            CanSleepTarget = throwable.CanSleepTarget,
            SleepDuration = throwable.SleepDuration,
            DealDamageOnlyIfTargetSleeping = dealDamageOnlyIfTargetSleeping,
            BounceOnWalls = bounceOnWalls,
            CanBeReclaimedByOwner = canBeReclaimedByOwner,
            ReclaimHealRatio = reclaimHealRatio,
            KeepOnFighterHit = keepOnFighterHit
        };
    }

    private void InitializeFighterForMatch(FighterState fighter, FighterState enemy)
    {
        GetSkill(fighter).OnMatchStarted(this, fighter, enemy);
    }

    private FighterState CreateFighter(FighterDefinition definition, string side, Vec2 position, Vec2 velocity)
    {
        return new FighterState
        {
            Id = $"{side}-{definition.Key}-{Guid.NewGuid():N}"[..18],
            Side = side,
            Definition = definition,
            Position = position,
            Velocity = velocity,
            Health = definition.HP,
            SkillTimer = definition.CD,
            ActiveBurstFire = null,
            SkillFlashTime = 0,
            SleepTime = 0
        };
    }

    /// <summary>
    /// 更新单个角色：移动、反弹、睡眠衰减、技能 CD 与连发执行。
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

            var skill = GetSkill(self);
            if (skill.CanUseSkill(this, self, enemy))
            {
                self.SkillTimer -= dt;
                UpdateBurstFire(self, dt);

                if (self.SkillTimer <= 0 && enemy.IsAlive)
                {
                    skill.Execute(this, self, enemy);
                    self.SkillTimer += self.Definition.CD;
                }
            }
        }

        self.SkillFlashTime = Math.Max(0, self.SkillFlashTime - dt);
    }

    private void UpdateBurstFire(FighterState fighter, double dt)
    {
        var burstFire = fighter.ActiveBurstFire;
        if (burstFire is null || burstFire.ShotsRemaining <= 0 || string.IsNullOrWhiteSpace(burstFire.ThrowableKey))
        {
            fighter.ActiveBurstFire = null;
            return;
        }

        burstFire.ShotTimer -= dt;
        while (burstFire.ShotsRemaining > 0 && burstFire.ShotTimer <= 0)
        {
            var target = Fighters.FirstOrDefault(x => x.Id == burstFire.TargetId);
            if (target is null || !target.IsAlive)
            {
                fighter.ActiveBurstFire = null;
                return;
            }

            SpawnProjectile(
                owner: fighter,
                target: target,
                throwable: GetBurstThrowable(fighter, burstFire.ThrowableKey),
                damageOverride: burstFire.DamageOverride,
                dealDamageOnlyIfTargetSleeping: burstFire.DealDamageOnlyIfTargetSleeping);

            burstFire.ShotsRemaining--;
            burstFire.ShotTimer += burstFire.ShotInterval;
        }

        if (burstFire.ShotsRemaining <= 0)
        {
            fighter.ActiveBurstFire = null;
        }
    }

    /// <summary>
    /// 更新所有召唤物：周期性寻找最近敌人并开火。
    /// </summary>
    private void UpdateSummonables(double dt)
    {
        for (var i = Summonables.Count - 1; i >= 0; i--)
        {
            var summonable = Summonables[i];
            if (!summonable.IsAlive)
            {
                Summonables.RemoveAt(i);
                continue;
            }

            summonable.AttackTimer -= dt;
            while (summonable.AttackTimer <= 0)
            {
                var target = FindNearestEnemyFighter(summonable.Side, summonable.Position);
                if (target is not null && target.IsAlive)
                {
                    SpawnProjectile(summonable, target, ThrowableCatalog.Get(summonable.AttackThrowableKey));
                    summonable.AttackTimer += summonable.AttackInterval;
                }
                else
                {
                    summonable.AttackTimer = summonable.AttackInterval;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 角色碰到场地边界时反弹。只改变方向，不做速度衰减。
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
        else if (position.X + radius > ArenaWidth)
        {
            position = position with { X = ArenaWidth - radius };
            velocity = velocity with { X = -Math.Abs(velocity.X) };
            bounced = true;
        }

        if (position.Y - radius < 0)
        {
            position = position with { Y = radius };
            velocity = velocity with { Y = Math.Abs(velocity.Y) };
            bounced = true;
        }
        else if (position.Y + radius > ArenaHeight)
        {
            position = position with { Y = ArenaHeight - radius };
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
        var speed = fighter.Velocity.Length;
        if (speed > 0.001 && speed < BattleTuning.MinimumSpeed)
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

    /// <summary>
    /// 根据命中率给投掷方向施加偏移。
    /// 命中率越低，偏移角越大。
    /// </summary>
    private Vec2 ApplyAccuracySpread(Vec2 direction, double accuracy)
    {
        if (direction.Length <= 0.0001)
        {
            return new Vec2(1, 0);
        }

        var clampedAccuracy = Math.Clamp(accuracy, 0, 100);
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
            Math.Clamp(position.X, radius, ArenaWidth - radius),
            Math.Clamp(position.Y, radius, ArenaHeight - radius));
    }

    private ThrowableDefinition GetBurstThrowable(FighterState fighter, string throwableKey)
    {
        return throwableKey.Equals(fighter.Definition.Key, StringComparison.OrdinalIgnoreCase)
            ? GetThrowable(fighter)
            : ThrowableCatalog.Get(throwableKey);
    }

    private static ThrowableDefinition GetThrowable(FighterState fighter)
    {
        var definition = fighter.Definition;
        if (definition.HasInlineProjectileDefinition)
        {
            return new ThrowableDefinition
            {
                Key = definition.Key,
                Name = definition.Name,
                TexturePath = definition.ProjectileTexturePath!,
                Speed = definition.ProjectileSpeed!.Value,
                Radius = definition.ProjectileRadius!.Value,
                Damage = definition.ProjectileDamage!.Value,
                ColorHex = definition.ProjectileColorHex!,
                CanSleepTarget = definition.ProjectileCanSleepTarget,
                SleepDuration = definition.ProjectileSleepDuration
            };
        }

        return ThrowableCatalog.Get(definition.Key);
    }

    private FighterState? FindNearestEnemyFighter(string side, Vec2 fromPosition)
    {
        return Fighters
            .Where(x => x.IsAlive && !x.Side.Equals(side, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => (x.Position - fromPosition).Length)
            .FirstOrDefault();
    }

    /// <summary>
    /// 更新所有投掷物位置并判定命中。
    /// 命中后直接结算伤害，不再依赖角色身体碰撞造成伤害。
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
                GetSkill(owner).OnProjectileReclaimed(this, owner, projectile);
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
                ?? Summonables.FirstOrDefault(x => x.Id == projectile.OwnerId)?.Side;

            if (!string.IsNullOrWhiteSpace(ownerSide))
            {
                var hitSummonable = Summonables
                    .Where(x => x.IsAlive && !x.Side.Equals(ownerSide, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => (x.Position - projectile.Position).Length)
                    .FirstOrDefault(x => IsProjectileHit(projectile, x.Position, x.Radius));

                if (hitSummonable is not null)
                {
                    hitSummonable.Health -= projectile.Damage;
                    SpawnImpact(projectile.Position, projectile.ColorHex);
                    SpawnDamageText(hitSummonable.Position, projectile.Damage, hitSummonable.Side);
                    if (!hitSummonable.IsAlive)
                    {
                        SpawnExplosion(hitSummonable.Position, GetPrimaryColor(hitSummonable.Side));
                        Summonables.Remove(hitSummonable);
                    }

                    if (projectile.KeepOnFighterHit)
                    {
                        BounceProjectileOnUnit(projectile, hitSummonable.Position, hitSummonable.Radius);
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

                    if (owner is not null && owner.IsAlive && owner.Id != target.Id)
                    {
                        GetSkill(owner).OnHitTarget(this, owner, target, damage);
                    }

                    GetSkill(target).OnDamaged(this, owner, target, damage);
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

    private double ApplyDamageToFighter(FighterState target, double baseDamage, FighterState? attacker = null)
    {
        var damage = baseDamage;

        if (attacker is not null && attacker.IsAlive)
        {
            damage = GetSkill(attacker).ModifyOutgoingDamage(this, attacker, target, damage);
        }

        damage = GetSkill(target).ModifyIncomingDamage(this, attacker, target, damage);

        target.Health -= damage;
        return damage;
    }

    private static bool IsProjectileHit(BattleProjectile projectile, Vec2 targetPosition, double targetRadius)
    {
        var hitDistance = projectile.Radius + targetRadius;
        return (projectile.Position - targetPosition).Length <= hitDistance;
    }

    private bool IsProjectileHitWall(BattleProjectile projectile)
    {
        var position = projectile.Position;
        var radius = projectile.Radius;
        return position.X - radius < 0
            || position.X + radius > ArenaWidth
            || position.Y - radius < 0
            || position.Y + radius > ArenaHeight;
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
        else if (position.X + radius > ArenaWidth)
        {
            position = position with { X = ArenaWidth - radius };
            velocity = velocity with { X = -Math.Abs(velocity.X) };
        }

        if (position.Y - radius < 0)
        {
            position = position with { Y = radius };
            velocity = velocity with { Y = Math.Abs(velocity.Y) };
        }
        else if (position.Y + radius > ArenaHeight)
        {
            position = position with { Y = ArenaHeight - radius };
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
            || position.X - radius > ArenaWidth
            || position.Y + radius < 0
            || position.Y - radius > ArenaHeight;
    }

    private IFighterSkill GetSkill(FighterState fighter)
    {
        return SkillRegistry.Get(fighter.Definition.Key);
    }

    public double RandomRange(double min, double max) => min + (_random.NextDouble() * (max - min));
}
