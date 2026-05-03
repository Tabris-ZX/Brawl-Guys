namespace FightingGuys.Core;

/// <summary>
/// 角色静态配置。主要由 Data/roles.json 反序列化生成。
/// </summary>
public sealed class FighterDefinition
{
    /// <summary>角色唯一 ID，用于选择角色和查找配置。</summary>
    public required string Key { get; init; }

    /// <summary>界面显示名称。</summary>
    public required string Name { get; init; }

    /// <summary>角色说明，显示在右侧信息面板。</summary>
    public required string desc { get; init; }

    /// <summary>角色贴图逻辑路径，具体文件位置由表现层资源映射解析。</summary>
    public required string TexturePath { get; init; }

    /// <summary>角色碰撞/受击半径。投掷物进入这个范围就会命中。</summary>
    public required double Radius { get; init; }

    /// <summary>最大生命值。</summary>
    public required double HP { get; init; }

    /// <summary>移动速度。开局会按这个速度朝随机方向移动。</summary>
    public required double Speed { get; init; }

    /// <summary>攻击间隔，单位秒。当前用 SkillTimer 驱动普通投掷攻击。</summary>
    public required double CD { get; init; }

    /// <summary>命中率，范围 0~100。越低则远程攻击偏移角越大。</summary>
    public required double Accuracy { get; init; }

    /// <summary>是否根据对方位置镜像翻转贴图。1=会翻转,0=保持原样。</summary>
    public int isMirror { get; init; }
}
