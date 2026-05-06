using System.Text.Json;
using BrawlGuys.Core.Skills;

namespace BrawlGuys.Core;

public static class FighterCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static IReadOnlyList<FighterDefinition> All { get; } = LoadDefinitions();

    public static FighterDefinition Get(string key) =>
        All.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Unknown fighter key: {key}");

    private static IReadOnlyList<FighterDefinition> LoadDefinitions()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "roles.json");
        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException("Role config file not found.", jsonPath);
        }

        var json = File.ReadAllText(jsonPath);
        var definitions = JsonSerializer.Deserialize<List<FighterDefinition>>(json, JsonOptions);
        if (definitions is not { Count: > 0 })
        {
            throw new InvalidOperationException($"No role definitions found in {jsonPath}");
        }

        ValidateDefinitions(definitions, jsonPath);
        return definitions;
    }

    private static void ValidateDefinitions(IEnumerable<FighterDefinition> definitions, string source)
    {
        var definitionList = definitions.ToList();

        var duplicatedKey = definitionList
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;

        if (!string.IsNullOrWhiteSpace(duplicatedKey))
        {
            throw new InvalidOperationException($"Duplicated fighter key '{duplicatedKey}' in {source}");
        }

        var invalidAccuracy = definitionList
            .FirstOrDefault(x => x.Accuracy is < 0 or > 100);

        if (invalidAccuracy is not null)
        {
            throw new InvalidOperationException($"Fighter '{invalidAccuracy.Key}' Accuracy must be between 0 and 100 in {source}");
        }

        var invalidInlineProjectile = definitionList.FirstOrDefault(x =>
            !x.HasInlineProjectileDefinition
            && (!string.IsNullOrWhiteSpace(x.ProjectileTexturePath)
                || x.ProjectileSpeed is not null
                || x.ProjectileRadius is not null
                || x.ProjectileDamage is not null));

        if (invalidInlineProjectile is not null)
        {
            throw new InvalidOperationException($"Fighter '{invalidInlineProjectile.Key}' inline projectile config is incomplete in {source}");
        }

        var missingSkill = definitionList
            .FirstOrDefault(x => !SkillRegistry.Contains(x.Key));

        if (missingSkill is not null)
        {
            throw new InvalidOperationException($"Fighter '{missingSkill.Key}' exists in {source} but has no matching skill file.");
        }

        SkillRegistry.ValidateAgainstFighterKeys(definitionList.Select(x => x.Key));

        var missingProjectile = definitionList
            .FirstOrDefault(x => SkillRegistry.Get(x.Key).RequiresProjectileDefinition
                && !x.HasInlineProjectileDefinition
                && !ThrowableCatalog.Contains(x.Key));

        if (missingProjectile is not null)
        {
            throw new InvalidOperationException($"Fighter '{missingProjectile.Key}' has neither inline projectile config in roles.json nor legacy throwable.json config.");
        }
    }
}
