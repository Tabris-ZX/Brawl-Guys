namespace BrawlGuys.Core.Skills;

/// <summary>
/// 角色技能接口。
/// 保持“一角色一文件”的写法：常用扩展点直接放这里，
/// 大多数角色只实现 Execute，其他钩子按需覆写即可。
/// </summary>
public interface IFighterSkill
{
    /// <summary>
    /// 技能唯一标识，必须与 roles.json 中角色的 key 完全一致。
    /// SkillRegistry 会用该值建立“角色配置 -> 技能实现”的映射。
    /// </summary>
    string Key { get; }

    /// <summary>
    /// 当前技能是否依赖默认投掷物配置。
    /// 为 true 时，角色必须在 roles.json 内写完整 Projectile* 字段，或在 throwable.json 中存在同 key 配置。
    /// 纯召唤、纯状态类技能可覆写为 false。
    /// </summary>
    bool RequiresProjectileDefinition => true;

    /// <summary>
    /// 技能释放的核心逻辑。
    /// BattleWorld 在角色技能冷却结束且 CanUseSkill 返回 true 后调用该方法。
    /// </summary>
    /// <param name="world">当前战斗世界，可用于生成投掷物、召唤物、特效或读取随机数。</param>
    /// <param name="caster">释放技能的角色。</param>
    /// <param name="target">当前锁定的目标，一般是最近的敌方角色。</param>
    void Execute(BattleWorld world, FighterState caster, FighterState target);

    /// <summary>
    /// 每帧更新时调用。
    /// 适合处理持续状态、专属计时器、冲刺状态等被动逻辑。
    /// </summary>
    void OnUpdate(BattleWorld world, FighterState self, FighterState enemy, double dt)
    {
    }

    /// <summary>
    /// 每局开始时调用一次，用于初始化角色运行时状态。
    /// 例如库存数量、怒气状态、下一次技能预告等都推荐写入 FighterState.RuntimeValues。
    /// </summary>
    void OnMatchStarted(BattleWorld world, FighterState self, FighterState enemy)
    {
    }

    /// <summary>
    /// 判断当前角色是否可以释放技能。
    /// 返回 false 时不会消耗本次技能逻辑，适合用于“无库存时不释放”等场景。
    /// </summary>
    bool CanUseSkill(BattleWorld world, FighterState self, FighterState enemy) => true;

    /// <summary>
    /// 修改攻击者即将造成的伤害。
    /// 该钩子在最终扣血前调用，可用于暴击、增伤、状态加成等逻辑。
    /// </summary>
    double ModifyOutgoingDamage(BattleWorld world, FighterState attacker, FighterState target, double damage) => damage;

    /// <summary>
    /// 修改目标即将受到的伤害。
    /// 该钩子在最终扣血前调用，可用于减伤、易伤、护盾等逻辑。
    /// </summary>
    double ModifyIncomingDamage(BattleWorld world, FighterState? attacker, FighterState target, double damage) => damage;

    /// <summary>
    /// 投掷物成功命中敌方角色并造成伤害后调用。
    /// 适合处理命中计数、吸血、状态切换等“命中后”效果。
    /// </summary>
    void OnHitTarget(BattleWorld world, FighterState attacker, FighterState target, double damageDealt)
    {
    }

    /// <summary>
    /// 可回收投掷物被拥有者回收时调用。
    /// 适合恢复弹药库存、刷新状态或追加回血以外的额外效果。
    /// </summary>
    void OnProjectileReclaimed(BattleWorld world, FighterState owner, BattleProjectile projectile)
    {
    }

    /// <summary>
    /// 角色实际受到伤害后调用。
    /// 适合反伤、受击触发、受伤叠层等逻辑；attacker 可能为空，例如环境或无明确来源的伤害。
    /// </summary>
    void OnDamaged(BattleWorld world, FighterState? attacker, FighterState target, double damageDealt)
    {
    }

    /// <summary>
    /// 两名角色身体发生碰撞时调用。
    /// 每次碰撞会分别以 self 视角回调双方技能。
    /// </summary>
    void OnFighterCollision(BattleWorld world, FighterState self, FighterState other)
    {
    }

    /// <summary>
    /// 角色碰到场地边界并发生反弹后调用。
    /// 适合处理撞墙惩罚、额外特效等逻辑。
    /// </summary>
    void OnWallBounce(BattleWorld world, FighterState self)
    {
    }

    /// <summary>
    /// 返回右侧信息面板展示的技能/角色描述。
    /// 默认直接使用角色配置里的 desc，复杂技能可拼接运行时状态。
    /// </summary>
    string GetDescription(FighterState fighter) => fighter.Definition.desc;

    /// <summary>
    /// 返回竞技场内角色头顶的提示文字。
    /// 默认不显示；适合蓄力预告、库存提示等需要场内可视化的信息。
    /// </summary>
    string? GetArenaHintText(FighterState fighter) => null;
}
