using System.Text.Json;

namespace BrawlGuys.Core;

/// <summary>
/// 战斗全局调参。配置来自 Config/global.json。
/// </summary>
public static class BattleTuning
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Lazy<GlobalConfig> Config = new(LoadConfig);

    /// <summary>
    /// 单帧最大模拟时间，防止窗口卡顿后一次性推进太多时间导致投掷物穿过目标。
    /// </summary>
    public static double MaxDeltaTime => Config.Value.MaxDeltaTime;

    /// <summary>
    /// 角色开局位置离场地边缘的距离。
    /// </summary>
    public static double FighterSidePadding => Config.Value.FighterSidePadding;

    /// <summary>
    /// 角色碰墙后的最低速度，避免外部速度修改过小后看起来停住。
    /// </summary>
    public static double MinimumSpeed => Config.Value.MinimumSpeed;

    /// <summary>
    /// 投掷物存在的最长时间，超时会自动消失。
    /// </summary>
    public static double ProjectileLifeTime => Config.Value.ProjectileLifeTime;

    private static GlobalConfig LoadConfig()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Config", "global.json");
        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException("Global config file not found.", jsonPath);
        }

        var json = File.ReadAllText(jsonPath);
        var config = JsonSerializer.Deserialize<GlobalConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Invalid global config file: {jsonPath}");

        ValidateConfig(config, jsonPath);
        return config;
    }

    private static void ValidateConfig(GlobalConfig config, string source)
    {
        if (config.MaxDeltaTime <= 0)
        {
            throw new InvalidOperationException($"maxDeltaTime must be greater than 0 in {source}");
        }

        if (config.FighterSidePadding < 0)
        {
            throw new InvalidOperationException($"fighterSidePadding cannot be negative in {source}");
        }

        if (config.MinimumSpeed < 0)
        {
            throw new InvalidOperationException($"minimumSpeed cannot be negative in {source}");
        }

        if (config.ProjectileLifeTime <= 0)
        {
            throw new InvalidOperationException($"projectileLifeTime must be greater than 0 in {source}");
        }
    }

    private sealed class GlobalConfig
    {
        public double MaxDeltaTime { get; init; } = 0.033;
        public double FighterSidePadding { get; init; } = 170;
        public double MinimumSpeed { get; init; } = 90;
        public double ProjectileLifeTime { get; init; } = 5.0;
    }
}
