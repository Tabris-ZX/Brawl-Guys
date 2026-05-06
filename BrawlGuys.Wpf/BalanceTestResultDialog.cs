using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BrawlGuys.Wpf;

/// <summary>
/// 单个对手的平衡性测试汇总结果。
/// Subject 表示被测试角色；Opponent 表示当前对手。
/// </summary>
internal sealed record BalanceOpponentResult(
    string OpponentKey,
    string OpponentName,
    int SubjectWins,
    int OpponentWins,
    int Draws,
    int SubjectLeftWins,
    int OpponentRightWins,
    int FirstHalfDraws,
    int SubjectRightWins,
    int OpponentLeftWins,
    int SecondHalfDraws)
{
    /// <summary>该对手组总对战场数。</summary>
    public int TotalMatches => SubjectWins + OpponentWins + Draws;

    /// <summary>去除平局后的有效胜负场数。</summary>
    public int DecisiveMatches => Math.Max(1, SubjectWins + OpponentWins);

    /// <summary>被测试角色胜率，平局不计入分母。</summary>
    public double SubjectWinRate => SubjectWins * 100.0 / DecisiveMatches;
}

/// <summary>
/// 一次“指定角色 vs 全部其它角色”的平衡性测试结果。
/// </summary>
internal sealed record BalanceTestSummary(
    string SubjectKey,
    string SubjectName,
    int MatchesPerOpponent,
    IReadOnlyList<BalanceOpponentResult> Results)
{
    /// <summary>总模拟场数。</summary>
    public int TotalMatches => Results.Sum(x => x.TotalMatches);

    /// <summary>被测试角色总胜场。</summary>
    public int TotalSubjectWins => Results.Sum(x => x.SubjectWins);

    /// <summary>所有对手总胜场。</summary>
    public int TotalOpponentWins => Results.Sum(x => x.OpponentWins);

    /// <summary>总平局数。</summary>
    public int TotalDraws => Results.Sum(x => x.Draws);

    /// <summary>被测试角色整体胜率，平局不计入分母。</summary>
    public double OverallWinRate => TotalSubjectWins * 100.0 / Math.Max(1, TotalSubjectWins + TotalOpponentWins);
}

/// <summary>
/// 平衡性测试结果弹窗。
/// 样式、布局和颜色都集中在该文件中，避免 MainWindow 承担大量弹窗 UI 构建代码。
/// </summary>
internal static class BalanceTestResultDialog
{
    private const string LeftSideColor = "#5AA9FF";
    private const string RightSideColor = "#FF6B7A";
    private const string DrawColor = "#F6C453";

    private static readonly Dictionary<string, SolidColorBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 显示平衡性测试结果弹窗。
    /// </summary>
    public static void Show(Window owner, BalanceTestSummary summary, bool isDarkTheme)
    {
        var palette = BalanceDialogPalette.Create(isDarkTheme);
        var dialog = CreateWindow(owner);
        var rootBorder = CreateRootBorder(dialog, palette);
        var root = new StackPanel();
        rootBorder.Child = root;

        root.Children.Add(CreateHeader(dialog, summary, palette));
        root.Children.Add(CreateOverview(summary, palette));
        root.Children.Add(CreateOverallResultBar(summary.TotalSubjectWins, summary.TotalOpponentWins, summary.TotalDraws));
        root.Children.Add(CreateOpponentList(summary, palette));

        dialog.Content = rootBorder;
        dialog.ShowDialog();
    }

    private static Window CreateWindow(Window owner)
    {
        return new Window
        {
            Owner = owner,
            Title = "平衡性测试",
            Width = 720,
            MaxHeight = 760,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            ShowInTaskbar = false
        };
    }

    private static Border CreateRootBorder(Window dialog, BalanceDialogPalette palette)
    {
        var border = new Border
        {
            Padding = new Thickness(22),
            Background = palette.Background,
            BorderBrush = palette.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 30,
                ShadowDepth = 0,
                Opacity = palette.IsDark ? 0.45 : 0.18,
                Color = Colors.Black
            }
        };

        border.MouseLeftButtonDown += (_, args) =>
        {
            if (args.OriginalSource is DependencyObject source && HasVisualAncestor<Button>(source))
            {
                return;
            }

            dialog.DragMove();
        };

        return border;
    }

    private static UIElement CreateHeader(Window dialog, BalanceTestSummary summary, BalanceDialogPalette palette)
    {
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        var closeButton = new Button
        {
            Width = 34,
            Height = 34,
            Content = "×",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Background = palette.CloseBackground,
            Foreground = palette.CloseForeground,
            BorderBrush = palette.CloseBackground,
            Padding = new Thickness(0)
        };
        closeButton.Click += (_, _) => dialog.Close();
        DockPanel.SetDock(closeButton, Dock.Right);
        header.Children.Add(closeButton);

        var titleStack = new StackPanel();
        titleStack.Children.Add(new TextBlock
        {
            Text = $"{summary.SubjectName} · 平衡性测试",
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = palette.Foreground
        });
        titleStack.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            Text = $"64x 快速模拟 · 每个对手 {summary.MatchesPerOpponent} 场 · 共 {summary.TotalMatches} 场",
            FontSize = 13,
            Foreground = palette.Muted
        });
        header.Children.Add(titleStack);
        return header;
    }

    private static UIElement CreateOverview(BalanceTestSummary summary, BalanceDialogPalette palette)
    {
        var card = CreateCard(palette.CardBackground, palette.Border, new Thickness(0, 0, 0, 12));
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        //grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        card.Child = grid;

        grid.Children.Add(CreateMetricBlock(summary.SubjectName, summary.TotalSubjectWins.ToString(), $"总胜率 {summary.OverallWinRate:0.0}%", CreateBrush(LeftSideColor), palette.Foreground, palette.Muted, 0));
        grid.Children.Add(CreateMetricBlock("其它角色", summary.TotalOpponentWins.ToString(), "总胜场", CreateBrush(RightSideColor), palette.Foreground, palette.Muted, 1));
        //grid.Children.Add(CreateMetricBlock("平局", summary.TotalDraws.ToString(), "未计入胜率", CreateBrush(DrawColor), palette.Foreground, palette.Muted, 2));
        return card;
    }

    private static UIElement CreateOpponentList(BalanceTestSummary summary, BalanceDialogPalette palette)
    {
        var list = new StackPanel();
        list.Children.Add(new TextBlock
        {
            Text = "对手明细",
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = palette.Foreground,
            Margin = new Thickness(0, 2, 0, 8)
        });

        foreach (var result in summary.Results.OrderBy(x => x.SubjectWinRate))
        {
            list.Children.Add(CreateOpponentCard(summary.SubjectName, result, palette));
        }

        return new ScrollViewer
        {
            MaxHeight = 430,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = list
        };
    }

    private static UIElement CreateOpponentCard(string subjectName, BalanceOpponentResult result, BalanceDialogPalette palette)
    {
        var card = CreateCard(palette.CardBackground, palette.Border, new Thickness(0, 0, 0, 10));
        var stack = new StackPanel();
        card.Child = stack;

        var header = new DockPanel { LastChildFill = true };
        var rateText = new TextBlock
        {
            Text = $"{result.SubjectWinRate:0.0}%",
            FontSize = 18,
            FontWeight = FontWeights.Black,
            Foreground = GetRateBrush(result.SubjectWinRate),
            MinWidth = 76,
            TextAlignment = TextAlignment.Right
        };
        DockPanel.SetDock(rateText, Dock.Right);
        header.Children.Add(rateText);
        header.Children.Add(new TextBlock
        {
            Text = $"{subjectName}  vs  {result.OpponentName}",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = palette.Foreground,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        stack.Children.Add(header);

        stack.Children.Add(CreateResultBar(result.SubjectWins, result.OpponentWins, result.Draws, new Thickness(0, 10, 0, 8)));
        stack.Children.Add(CreateStatRow(subjectName, result.SubjectWins, CreateBrush(LeftSideColor), palette.Muted));
        stack.Children.Add(CreateStatRow(result.OpponentName, result.OpponentWins, CreateBrush(RightSideColor), palette.Muted));
        stack.Children.Add(CreateStatRow("平局", result.Draws, CreateBrush(DrawColor), palette.Muted));
        stack.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 6, 0, 0),
            Text = $"上半场：{subjectName} 左 {result.SubjectLeftWins} 胜 / {result.OpponentName} 右 {result.OpponentRightWins} 胜 / 平 {result.FirstHalfDraws}\n下半场：{subjectName} 右 {result.SubjectRightWins} 胜 / {result.OpponentName} 左 {result.OpponentLeftWins} 胜 / 平 {result.SecondHalfDraws}",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = palette.Muted
        });
        return card;
    }

    private static UIElement CreateMetricBlock(string title, string value, string subtitle, Brush accent, Brush foreground, Brush muted, int column)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetColumn(stack, column);
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = muted,
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 180
        });
        stack.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            Text = value,
            Foreground = accent,
            FontSize = 30,
            FontWeight = FontWeights.Black,
            TextAlignment = TextAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle,
            Foreground = foreground,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center
        });
        return stack;
    }

    private static UIElement CreateOverallResultBar(int subjectWins, int opponentWins, int draws)
    {
        return CreateResultBar(subjectWins, opponentWins, draws, new Thickness(0, 0, 0, 14));
    }

    private static Border CreateResultBar(int subjectWins, int opponentWins, int draws, Thickness margin)
    {
        var total = Math.Max(1, subjectWins + opponentWins + draws);
        var grid = new Grid { Height = 16, ClipToBounds = true };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.001, subjectWins), GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.001, opponentWins), GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.001, draws), GridUnitType.Star) });

        var subject = new Border { Background = CreateBrush(LeftSideColor), ToolTip = $"测试角色 {subjectWins * 100.0 / total:0.0}%" };
        var opponent = new Border { Background = CreateBrush(RightSideColor), ToolTip = $"对手 {opponentWins * 100.0 / total:0.0}%" };
        var draw = new Border { Background = CreateBrush(DrawColor), ToolTip = $"平局 {draws * 100.0 / total:0.0}%" };
        Grid.SetColumn(opponent, 1);
        Grid.SetColumn(draw, 2);
        grid.Children.Add(subject);
        grid.Children.Add(opponent);
        grid.Children.Add(draw);

        return new Border
        {
            Margin = margin,
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Child = grid
        };
    }

    private static UIElement CreateStatRow(string label, int value, Brush accent, Brush muted)
    {
        var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 3, 0, 0) };
        var valueText = new TextBlock
        {
            Text = value.ToString(CultureInfo.InvariantCulture),
            Foreground = accent,
            FontWeight = FontWeights.Bold,
            MinWidth = 52,
            TextAlignment = TextAlignment.Right
        };
        DockPanel.SetDock(valueText, Dock.Right);
        row.Children.Add(valueText);
        row.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = muted,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        return row;
    }

    private static Border CreateCard(Brush background, Brush borderBrush, Thickness margin)
    {
        return new Border
        {
            Margin = margin,
            Padding = new Thickness(14),
            Background = background,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16)
        };
    }

    private static Brush GetRateBrush(double winRate)
    {
        if (winRate >= 55) return CreateBrush("#34A853");
        if (winRate <= 45) return CreateBrush("#EA4335");
        return CreateBrush(DrawColor);
    }

    private static bool HasVisualAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(child);
        while (current is not null)
        {
            if (current is T)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        if (BrushCache.TryGetValue(hex, out var cachedBrush))
        {
            return cachedBrush;
        }

        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        BrushCache[hex] = brush;
        return brush;
    }

    private sealed record BalanceDialogPalette(
        bool IsDark,
        Brush Background,
        Brush Foreground,
        Brush Muted,
        Brush CardBackground,
        Brush Border,
        Brush CloseBackground,
        Brush CloseForeground)
    {
        public static BalanceDialogPalette Create(bool isDark)
        {
            return new BalanceDialogPalette(
                isDark,
                CreateBrush(isDark ? "#2D2F31" : "#FFFFFF"),
                CreateBrush(isDark ? "#E8EAED" : "#202124"),
                CreateBrush(isDark ? "#BDC1C6" : "#5F6368"),
                CreateBrush(isDark ? "#303134" : "#F8F9FA"),
                CreateBrush(isDark ? "#3C4043" : "#E0E3E7"),
                CreateBrush(isDark ? "#3C4043" : "#F1F3F4"),
                CreateBrush(isDark ? "#F1F3F4" : "#202124"));
        }
    }
}
