using System.IO;
using System.Text.Json;

namespace BrawlGuys.Wpf;

internal static class TexturePathResolver
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> PathMap = new(LoadPathMap);

    public static string Resolve(string logicalPath)
    {
        if (string.IsNullOrWhiteSpace(logicalPath))
        {
            return logicalPath;
        }

        var normalizedPath = logicalPath.Replace('\\', '/');
        return PathMap.Value.TryGetValue(normalizedPath, out var mappedPath)
            ? mappedPath
            : normalizedPath;
    }

    private static IReadOnlyDictionary<string, string> LoadPathMap()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "texture-map.json");
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var json = File.ReadAllText(path);
        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        return map is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(
                map.ToDictionary(
                    pair => pair.Key.Replace('\\', '/'),
                    pair => pair.Value.Replace('\\', '/')),
                StringComparer.OrdinalIgnoreCase);
    }
}
