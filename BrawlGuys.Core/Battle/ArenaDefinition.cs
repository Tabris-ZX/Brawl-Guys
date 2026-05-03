namespace BrawlGuys.Core;

public sealed class ArenaDefinition
{
    /// <summary>
    /// 方形对战场地的边长。
    /// </summary>
    public double Length { get; init; } = 600;

    public double Width => Length;
    public double Height => Length;
}
