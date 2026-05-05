namespace BrawlGuys.Wpf;

internal static class TexturePathResolver
{
    public static string Resolve(string logicalPath)
    {
        if (string.IsNullOrWhiteSpace(logicalPath)) return logicalPath;

        var normalizedPath = logicalPath.Replace('\\', '/');

        if (normalizedPath.StartsWith("roles/", StringComparison.OrdinalIgnoreCase))
        {
            return $"Resources/Images/Textures/Roles/{normalizedPath["roles/".Length..]}";
        }

        if (normalizedPath.StartsWith("throwable/", StringComparison.OrdinalIgnoreCase))
        {
            return $"Resources/Images/Textures/Throwable/{normalizedPath["throwable/".Length..]}";
        }

        return normalizedPath;
    }
}
