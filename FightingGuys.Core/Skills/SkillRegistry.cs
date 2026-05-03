using System.Reflection;

namespace FightingGuys.Core.Skills;

public static class SkillRegistry
{
    private static readonly IReadOnlyDictionary<string, IFighterSkill> Skills = LoadSkills();

    public static IFighterSkill Get(string key) =>
        Skills.TryGetValue(key, out var skill)
            ? skill
            : throw new InvalidOperationException($"Unknown skill key: {key}");

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
