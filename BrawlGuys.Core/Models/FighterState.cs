namespace BrawlGuys.Core;

/// <summary>
/// 角色运行时状态。
/// 这里只保留所有角色都通用的战斗字段；
/// 角色专属状态统一放进 RuntimeValues，避免每新增一个角色就继续污染公共模型。
/// </summary>
public sealed class FighterState
{
    /// <summary>
    /// 当前实例唯一 Id。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 所属阵营。当前只会是 left 或 right。
    /// </summary>
    public required string Side { get; init; }

    /// <summary>
    /// 对应的静态角色配置。
    /// </summary>
    public required FighterDefinition Definition { get; init; }

    /// <summary>
    /// 当前世界坐标。
    /// </summary>
    public required Vec2 Position { get; set; }

    /// <summary>
    /// 当前速度向量。
    /// </summary>
    public required Vec2 Velocity { get; set; }

    /// <summary>
    /// 当前生命值。
    /// </summary>
    public required double Health { get; set; }

    /// <summary>
    /// 距离下一次可主动释放技能的剩余时间。
    /// </summary>
    public required double SkillTimer { get; set; }

    /// <summary>
    /// 当前正在进行的连发状态。
    /// 若为空，则表示没有连发任务。
    /// </summary>
    public BurstFireState? ActiveBurstFire { get; set; }

    /// <summary>
    /// 技能闪光特效剩余时间。
    /// 这是通用视觉反馈，因此保留在公共模型里。
    /// </summary>
    public double SkillFlashTime { get; set; }

    /// <summary>
    /// 睡眠剩余时间。
    /// 这是通用控制效果，因此保留在公共模型里。
    /// </summary>
    public double SleepTime { get; set; }

    /// <summary>
    /// 角色专属运行时状态。
    /// 例如某个角色的层数、怒气、库存、预告值等，都应存这里。
    /// </summary>
    public Dictionary<string, double> RuntimeValues { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 角色是否还活着。
    /// </summary>
    public bool IsAlive => Health > 0;

    /// <summary>
    /// 角色是否处于睡眠状态。
    /// </summary>
    public bool IsSleeping => SleepTime > 0;

    /// <summary>
    /// 读取一个运行时数值；若不存在则返回默认值。
    /// </summary>
    public double GetRuntimeValue(string key, double defaultValue = 0)
    {
        return RuntimeValues.TryGetValue(key, out var value)
            ? value
            : defaultValue;
    }

    /// <summary>
    /// 读取一个运行时整数；内部仍复用数值字典存储。
    /// </summary>
    public int GetRuntimeInt(string key, int defaultValue = 0)
    {
        return (int)Math.Round(GetRuntimeValue(key, defaultValue));
    }

    /// <summary>
    /// 读取一个运行时布尔值。非 0 视为 true。
    /// </summary>
    public bool GetRuntimeFlag(string key, bool defaultValue = false)
    {
        return GetRuntimeValue(key, defaultValue ? 1 : 0) > 0.5;
    }

    /// <summary>
    /// 写入一个运行时数值。
    /// </summary>
    public void SetRuntimeValue(string key, double value)
    {
        RuntimeValues[key] = value;
    }

    /// <summary>
    /// 写入一个运行时布尔值。true 会存成 1，false 会存成 0。
    /// </summary>
    public void SetRuntimeFlag(string key, bool value)
    {
        RuntimeValues[key] = value ? 1 : 0;
    }
}
