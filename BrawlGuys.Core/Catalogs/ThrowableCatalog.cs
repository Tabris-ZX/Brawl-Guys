using System.Text.Json;

namespace BrawlGuys.Core;

/// <summary>
/// 投掷物配置目录。由 Data/throwable.json 反序列化生成。
/// </summary>
public static class ThrowableCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static IReadOnlyList<ThrowableDefinition> All { get; } = LoadDefinitions();

    public static bool Contains(string key) =>
        All.Any(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public static ThrowableDefinition Get(string key) =>
        All.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Unknown throwable key: {key}");

    private static IReadOnlyList<ThrowableDefinition> LoadDefinitions()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "throwable.json");
        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException("Throwable config file not found.", jsonPath);
        }

        var json = File.ReadAllText(jsonPath);
        var definitions = JsonSerializer.Deserialize<List<ThrowableDefinition>>(json, JsonOptions);
        if (definitions is not { Count: > 0 })
        {
            throw new InvalidOperationException($"No throwable definitions found in {jsonPath}");
        }

        ValidateDefinitions(definitions, jsonPath);
        return definitions;
    }

    private static void ValidateDefinitions(IEnumerable<ThrowableDefinition> definitions, string source)
    {
        var duplicatedKey = definitions
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;

        if (!string.IsNullOrWhiteSpace(duplicatedKey))
        {
            throw new InvalidOperationException($"Duplicated throwable key '{duplicatedKey}' in {source}");
        }
    }
}
