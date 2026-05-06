using System.Reflection;

namespace BrawlGuys.Core.Skills;

/// <summary>
/// 角色技能注册表。
/// 启动时通过反射扫描 BrawlGuys.Core.Skills.Roles 命名空间下所有 IFighterSkill 实现，
/// 从而做到“新增角色技能文件后无需手动注册”。
/// </summary>
public static class SkillRegistry
{
    /// <summary>
    /// 所有已加载技能，key 使用不区分大小写的比较器，避免 JSON 配置大小写差异导致查找失败。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IFighterSkill> Skills = LoadSkills();

    /// <summary>
    /// 判断指定角色 key 是否存在对应技能实现。
    /// FighterCatalog 校验 roles.json 时会使用该方法检查缺失技能。
    /// </summary>
    public static bool Contains(string key) => Skills.ContainsKey(key);

    /// <summary>
    /// 校验所有技能 key 都能在角色配置中找到对应项。
    /// 这可以发现“新增了 Skill 文件但忘记写 roles.json”的错误。
    /// </summary>
    public static void ValidateAgainstFighterKeys(IEnumerable<string> fighterKeys)
    {
        var fighterKeySet = fighterKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownSkill = Skills.Values.FirstOrDefault(skill => !fighterKeySet.Contains(skill.Key));
        if (unknownSkill is not null)
        {
            throw new InvalidOperationException($"Skill file '{unknownSkill.GetType().Name}' has key '{unknownSkill.Key}', but roles.json has no matching fighter entry.");
        }
    }

    /// <summary>
    /// 根据角色 key 获取技能实例。
    /// 战斗逻辑和 UI 描述都会通过该方法访问技能钩子。
    /// </summary>
    public static IFighterSkill Get(string key) =>
        Skills.TryGetValue(key, out var skill)
            ? skill
            : throw new InvalidOperationException($"Unknown skill key: {key}");

    /// <summary>
    /// 通过反射加载角色技能。
    /// 只扫描 Roles 命名空间下的非抽象类，并要求能用无参构造函数创建；同时检查重复 key。
    /// </summary>
    private static IReadOnlyDictionary<string, IFighterSkill> LoadSkills()
    {
        var skillInterfaceType = typeof(IFighterSkill);
        var roleSkillNamespacePrefix = typeof(SkillRegistry).Namespace + ".Roles";

        var skills = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.Namespace?.StartsWith(roleSkillNamespacePrefix, StringComparison.Ordinal) == true
                && skillInterfaceType.IsAssignableFrom(type))
            .Select(type => Activator.CreateInstance(type) as IFighterSkill
                ?? throw new InvalidOperationException($"Cannot create skill instance: {type.FullName}"))
            .ToList();

        if (skills.Count == 0)
        {
            throw new InvalidOperationException($"No role skills found under namespace '{roleSkillNamespacePrefix}'.");
        }

        var duplicatedKey = skills
            .GroupBy(skill => skill.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (!string.IsNullOrWhiteSpace(duplicatedKey))
        {
            throw new InvalidOperationException($"Duplicated skill key: {duplicatedKey}");
        }

        return skills.ToDictionary(skill => skill.Key, StringComparer.OrdinalIgnoreCase);
    }
}
