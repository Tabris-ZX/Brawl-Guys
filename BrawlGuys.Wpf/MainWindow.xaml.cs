using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BrawlGuys.Core;
using BrawlGuys.Core.Skills;
using BrawlGuys.Core.Skills.Roles;

namespace BrawlGuys.Wpf;

public partial class MainWindow : Window
{
    private const string ArenaBackgroundLightColor = "#3768B8";
    private const string ArenaBackgroundDarkColor = "#234A8F";
    private const string LeftSideColor = "#5AA9FF";
    private const string LeftSideTextColor = "#8FC8FF";
    private const string RightSideColor = "#FF6B7A";
    private const string RightSideTextColor = "#FFB3BC";
    private const double ArenaContentScale = 1.0;

    private static readonly Dictionary<string, SolidColorBrush> SolidBrushCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Brush> ImageBrushCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly BattleWorld _world = new();
    private readonly DrawingGroup _arenaDrawingGroup = new();
    private readonly Image _arenaImage = new();
    private Brush _arenaBackgroundBrush = CreateArenaBackgroundBrush(ArenaBackgroundLightColor);

    private readonly Stopwatch _frameStopwatch = new();
    private double _lastFrameTimestamp;
    private bool _isLoaded;
    private bool _isPaused;
    private double _sidePanelSyncAccumulator;
    private double _SpeedMultiplier = 1.0;
    private double _arenaContentScale = ArenaContentScale;
    private bool _isBalanceTesting;
    private bool _isDarkTheme;
    private bool _isTwoVsTwoMode;

    /// <summary>
    /// 初始化主窗口、战斗画布、角色选择框和 60FPS 左右的 WPF 定时器。
    /// 构造阶段只做 UI 与渲染器准备；真正开局放在 Loaded 事件里，避免控件尚未完成加载时访问视觉树。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        ApplyTheme(false);

        UpdateArenaViewport();
        RenderOptions.SetBitmapScalingMode(ArenaCanvas, BitmapScalingMode.LowQuality);
        RenderOptions.SetEdgeMode(ArenaCanvas, EdgeMode.Aliased);

        InitializeArenaRenderer();

        LeftHealthBar.Foreground = CreateBrush(LeftSideColor);
        RightHealthBar.Foreground = CreateBrush(RightSideColor);

        SetupFighterCombo(LeftFighterCombo, "drunkard");
        SetupFighterCombo(RightFighterCombo, "angry-man");
        SetupFighterCombo(LeftFighterCombo2, "watcher");
        SetupFighterCombo(RightFighterCombo2, "qzd");
        SpeedCombo.SelectedIndex = 1;
        ScaleCombo.SelectedIndex = 1;

        Loaded += (_, _) =>
        {
            _isLoaded = true;
            RestartMatch();
            _frameStopwatch.Start();
            _lastFrameTimestamp = _frameStopwatch.Elapsed.TotalSeconds;
            CompositionTarget.Rendering += GameLoopTick;
        };

        Closed += (_, _) =>
        {
            CompositionTarget.Rendering -= GameLoopTick;
            _frameStopwatch.Stop();
        };
    }

    /// <summary>
    /// “重开”按钮事件：清除暂停状态，重新按当前选择的角色开始一局。
    /// </summary>
    private void RestartButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isPaused = false;
        PauseButton.Content = "暂停";
        _lastFrameTimestamp = _frameStopwatch.Elapsed.TotalSeconds;
        RestartMatch();
    }

    /// <summary>
    /// “暂停/继续”按钮事件：只暂停战斗世界推进，仍保持画面和右侧状态面板刷新。
    /// </summary>
    private void PauseButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isPaused = !_isPaused;
        PauseButton.Content = _isPaused ? "继续" : "暂停";
        _lastFrameTimestamp = _frameStopwatch.Elapsed.TotalSeconds;
        SyncSidePanel();
    }

    /// <summary>
    /// 切换亮色/暗色主题，并同步所有动态创建或模板内控件的颜色。
    /// </summary>
    private void ThemeToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyTheme(!_isDarkTheme);
    }

    /// <summary>
    /// 切换 1v1 / 2v2 模式。2v2 模式会显示第二组选角框，并隐藏只支持 1v1 的平衡性测试入口。
    /// </summary>
    private void ModeToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isTwoVsTwoMode = !_isTwoVsTwoMode;
        UpdateModeControls();
        ApplyDefaultArenaScaleForMode();
        RestartMatch();
    }

    /// <summary>
    /// 初始化一个角色下拉框的数据源、显示字段、选中字段和默认角色。
    /// </summary>
    private void SetupFighterCombo(ComboBox comboBox, string selectedKey)
    {
        comboBox.ItemsSource = FighterCatalog.All;
        comboBox.DisplayMemberPath = nameof(FighterDefinition.Name);
        comboBox.SelectedValuePath = nameof(FighterDefinition.Key);
        comboBox.SelectedValue = selectedKey;
    }

    /// <summary>
    /// 平衡性测试入口：在后台线程以 64x 速度模拟“当前选中角色 vs 所有其它角色”。
    /// 每个对手的对战场数可在 UI 中配置，并会自动拆分为原始站位和左右互换两部分。
    /// </summary>
    private async void BalanceTestButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isBalanceTesting || _isTwoVsTwoMode) return;

        if (!TryGetBalanceMatchCount(out var matchesPerOpponent))
        {
            MessageBox.Show(this, "请输入 2 到 20000 之间的对战场数。", "平衡性测试", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isBalanceTesting = true;
        BalanceTestButton.IsEnabled = false;
        BalanceTestButton.Content = "测试中...";

        try
        {
            var subjectKey = LeftFighterCombo.SelectedValue as string ?? "drunkard";
            var subjectName = (LeftFighterCombo.SelectedItem as FighterDefinition)?.Name ?? subjectKey;
            StatusTextBlock.Text = $"正在测试 {subjectName} 与所有其它角色...";

            var summary = await System.Threading.Tasks.Task.Run(() => SimulateBalanceAgainstAll(subjectKey, matchesPerOpponent));

            StatusTextBlock.Text = $"测试完成：{summary.SubjectName} 总胜 {summary.TotalSubjectWins} / 对手总胜 {summary.TotalOpponentWins} / 平局 {summary.TotalDraws}";
            BalanceTestResultDialog.Show(this, summary, _isDarkTheme);
        }
        finally
        {
            _isBalanceTesting = false;
            BalanceTestButton.IsEnabled = true;
            BalanceTestButton.Content = "开始全角色平衡性测试";
        }
    }

    /// <summary>
    /// 读取并校验平衡性测试场数。该数值表示每个对手的总场数，会在模拟时拆分为换边前后两段。
    /// </summary>
    private bool TryGetBalanceMatchCount(out int matchesPerOpponent)
    {
        return int.TryParse(BalanceMatchCountTextBox.Text, out matchesPerOpponent)
            && matchesPerOpponent is >= 2 and <= 20000;
    }

    /// <summary>
    /// 执行“指定角色 vs 所有其它角色”的平衡性测试。
    /// </summary>
    private static BalanceTestSummary SimulateBalanceAgainstAll(string subjectKey, int matchesPerOpponent)
    {
        var subject = FighterCatalog.Get(subjectKey);
        var opponents = FighterCatalog.All
            .Where(x => !x.Key.Equals(subjectKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var results = opponents
            .Select(opponent => SimulateBalancePair(subject, opponent, matchesPerOpponent))
            .ToList();

        return new BalanceTestSummary(subject.Key, subject.Name, matchesPerOpponent, results);
    }

    /// <summary>
    /// 执行单个对手的换边测试。
    /// 前半段被测试角色在左侧，后半段被测试角色在右侧，以抵消出生位置影响。
    /// </summary>
    private static BalanceOpponentResult SimulateBalancePair(FighterDefinition subject, FighterDefinition opponent, int totalMatches)
    {
        var firstHalfMatches = totalMatches / 2;
        var secondHalfMatches = totalMatches - firstHalfMatches;

        var subjectLeftWins = 0;
        var opponentRightWins = 0;
        var firstHalfDraws = 0;
        var subjectRightWins = 0;
        var opponentLeftWins = 0;
        var secondHalfDraws = 0;

        for (var match = 0; match < firstHalfMatches; match++)
        {
            var winnerSide = SimulateSingleBalanceMatch(subject.Key, opponent.Key);
            if (winnerSide is null)
            {
                firstHalfDraws++;
            }
            else if (winnerSide.Equals("left", StringComparison.OrdinalIgnoreCase))
            {
                subjectLeftWins++;
            }
            else
            {
                opponentRightWins++;
            }
        }

        for (var match = 0; match < secondHalfMatches; match++)
        {
            var winnerSide = SimulateSingleBalanceMatch(opponent.Key, subject.Key);
            if (winnerSide is null)
            {
                secondHalfDraws++;
            }
            else if (winnerSide.Equals("right", StringComparison.OrdinalIgnoreCase))
            {
                subjectRightWins++;
            }
            else
            {
                opponentLeftWins++;
            }
        }

        return new BalanceOpponentResult(
            opponent.Key,
            opponent.Name,
            subjectLeftWins + subjectRightWins,
            opponentRightWins + opponentLeftWins,
            firstHalfDraws + secondHalfDraws,
            subjectLeftWins,
            opponentRightWins,
            firstHalfDraws,
            subjectRightWins,
            opponentLeftWins,
            secondHalfDraws);
    }

    /// <summary>
    /// 快速模拟单局 1v1，返回获胜阵营；返回 null 表示超时或双败平局。
    /// </summary>
    private static string? SimulateSingleBalanceMatch(string leftKey, string rightKey)
    {
        const double simulationSpeed = 64.0;
        const double baseDt = 1.0 / 60.0;
        const double maxSimulatedSecondsPerMatch = 120;

        var simWorld = new BattleWorld();
        simWorld.StartMatch(leftKey, rightKey);

        var elapsed = 0.0;
        while (simWorld.Winner is null && !simWorld.IsDraw && elapsed < maxSimulatedSecondsPerMatch)
        {
            simWorld.Update(baseDt * simulationSpeed);
            elapsed += baseDt * simulationSpeed;
        }

        return simWorld.Winner?.Side;
    }

    /// <summary>
    /// 角色选择框变化事件。窗口加载完成后才触发重开，避免初始化下拉框时重复开局。
    /// </summary>
    private void FighterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoaded)
        {
            RestartMatch();
        }
    }

    /// <summary>
    /// 应用整窗主题色。
    /// 这里不仅修改显式控件颜色，也会更新 ComboBox 模板使用的 DynamicResource，以及按钮内部 TextBlock 的前景色。
    /// </summary>
    private void ApplyTheme(bool isDarkTheme)
    {
        _isDarkTheme = isDarkTheme;

        Background = CreateBrush(isDarkTheme ? "#202124" : "#F8F9FA");

        TopBarCard.Background = CreateBrush(isDarkTheme ? "#2D2F31" : "#FFFFFF");
        TopBarCard.BorderBrush = CreateBrush(isDarkTheme ? "#3C4043" : "#E0E3E7");

        InfoPanelCard.Background = CreateBrush(isDarkTheme ? "#2D2F31" : "#FFFFFF");
        InfoPanelCard.BorderBrush = CreateBrush(isDarkTheme ? "#3C4043" : "#E0E3E7");
        BottomPlaceholderCard.Background = CreateBrush(isDarkTheme ? "#2D2F31" : "#FFFFFF");
        BottomPlaceholderCard.BorderBrush = CreateBrush(isDarkTheme ? "#3C4043" : "#E0E3E7");

        StatusBanner.Background = CreateBrush(isDarkTheme ? "#3C3322" : "#FEF7E0");
        StatusBanner.BorderBrush = CreateBrush(isDarkTheme ? "#8D6E00" : "#FDD663");
        StatusTextBlock.Foreground = CreateBrush(isDarkTheme ? "#F6C453" : "#B06000");

        LeftInfoCard.Background = CreateBrush(isDarkTheme ? "#303134" : "#F8F9FA");
        LeftInfoCard.BorderBrush = CreateBrush(isDarkTheme ? "#3C4043" : "#E8EAED");
        RightInfoCard.Background = CreateBrush(isDarkTheme ? "#303134" : "#F8F9FA");
        RightInfoCard.BorderBrush = CreateBrush(isDarkTheme ? "#3C4043" : "#E8EAED");

        ArenaOuterBorder.Background = CreateBrush(isDarkTheme ? "#2A56C6" : "#4285F4");
        ArenaOuterBorder.BorderBrush = CreateBrush(isDarkTheme ? "#6EA0FF" : "#AECBFA");
        ArenaInnerBorder.Background = CreateBrush(isDarkTheme ? "#174EA6" : "#1967D2");
        ArenaInnerBorder.BorderBrush = CreateBrush(isDarkTheme ? "#9CC0FF" : "#D2E3FC");
        _arenaBackgroundBrush = CreateArenaBackgroundBrush(isDarkTheme ? ArenaBackgroundDarkColor : ArenaBackgroundLightColor);

        var primaryTextBrush = CreateBrush(isDarkTheme ? "#E8EAED" : "#202124");
        var mutedTextBrush = CreateBrush(isDarkTheme ? "#BDC1C6" : "#5F6368");
        var secondaryButtonBackground = CreateBrush(isDarkTheme ? "#303134" : "#FFFFFF");
        var secondaryButtonForeground = CreateBrush(isDarkTheme ? "#F1F3F4" : "#202124");
        var primaryButtonBackground = CreateBrush(isDarkTheme ? "#8AB4F8" : "#1A73E8");
        var primaryButtonForeground = CreateBrush(isDarkTheme ? "#202124" : "#FFFFFF");
        var controlBorderBrush = CreateBrush(isDarkTheme ? "#5F6368" : "#DADCE0");
        var controlBackgroundBrush = CreateBrush(isDarkTheme ? "#303134" : "#FFFFFF");

        Resources["ComboItemForegroundBrush"] = primaryTextBrush;
        Resources["ComboItemBackgroundBrush"] = controlBackgroundBrush;
        Resources["ComboItemHoverBrush"] = CreateBrush(isDarkTheme ? "#3C4043" : "#F1F5FF");
        Resources["ComboItemSelectedBrush"] = CreateBrush(isDarkTheme ? "#174EA6" : "#E8F0FE");
        Resources["ComboPopupBorderBrush"] = controlBorderBrush;
        Resources["ComboPopupBackgroundBrush"] = controlBackgroundBrush;
        Resources["ComboArrowBrush"] = mutedTextBrush;

        LeftFighterCombo.Background = controlBackgroundBrush;
        LeftFighterCombo.Foreground = primaryTextBrush;
        LeftFighterCombo.BorderBrush = controlBorderBrush;
        LeftFighterCombo2.Background = controlBackgroundBrush;
        LeftFighterCombo2.Foreground = primaryTextBrush;
        LeftFighterCombo2.BorderBrush = controlBorderBrush;
        RightFighterCombo.Background = controlBackgroundBrush;
        RightFighterCombo.Foreground = primaryTextBrush;
        RightFighterCombo.BorderBrush = controlBorderBrush;
        RightFighterCombo2.Background = controlBackgroundBrush;
        RightFighterCombo2.Foreground = primaryTextBrush;
        RightFighterCombo2.BorderBrush = controlBorderBrush;
        SpeedCombo.Background = controlBackgroundBrush;
        SpeedCombo.Foreground = primaryTextBrush;
        SpeedCombo.BorderBrush = controlBorderBrush;
        ScaleCombo.Background = controlBackgroundBrush;
        ScaleCombo.Foreground = primaryTextBrush;
        ScaleCombo.BorderBrush = controlBorderBrush;
        BalanceMatchCountTextBox.Background = controlBackgroundBrush;
        BalanceMatchCountTextBox.Foreground = primaryTextBrush;
        BalanceMatchCountTextBox.BorderBrush = controlBorderBrush;

        foreach (var textBlock in FindVisualChildren<TextBlock>(this))
        {
            if (ReferenceEquals(textBlock, LeftNameTextBlock)
                || ReferenceEquals(textBlock, RightNameTextBlock)
                || ReferenceEquals(textBlock, LeftHealthTextBlock)
                || ReferenceEquals(textBlock, RightHealthTextBlock)
                || ReferenceEquals(textBlock, LeftdescTextBlock)
                || ReferenceEquals(textBlock, RightdescTextBlock)
                || ReferenceEquals(textBlock, StatusTextBlock)
                || HasVisualAncestor<Button>(textBlock)
                || HasVisualAncestor<ComboBox>(textBlock)
                || HasVisualAncestor<TextBox>(textBlock))
            {
                continue;
            }

            textBlock.Foreground = mutedTextBrush;
        }

        ThemeToggleButton.Content = isDarkTheme ? "亮色" : "暗色";
        ApplySecondaryButtonTheme(ThemeToggleButton, secondaryButtonBackground, secondaryButtonForeground, controlBorderBrush);
        ApplySecondaryButtonTheme(ModeToggleButton, secondaryButtonBackground, secondaryButtonForeground, controlBorderBrush);
        ApplyPrimaryButtonTheme(PauseButton, primaryButtonBackground, primaryButtonForeground);
        ApplySecondaryButtonTheme(RestartButton, secondaryButtonBackground, secondaryButtonForeground, controlBorderBrush);
        ApplySecondaryButtonTheme(BalanceTestButton, secondaryButtonBackground, secondaryButtonForeground, controlBorderBrush);

        RenderWorld();
        SyncSidePanel();
    }

    /// <summary>
    /// 深度遍历 WPF 视觉树，查找指定类型的子元素。
    /// 主要用于批量同步 TextBlock 和按钮模板内文字颜色。
    /// </summary>
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is null)
        {
            yield break;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var nestedChild in FindVisualChildren<T>(child))
            {
                yield return nestedChild;
            }
        }
    }

    /// <summary>
    /// 判断某个视觉元素是否位于指定类型的祖先控件内部。
    /// 主题切换和弹窗拖动时用它排除按钮、下拉框等交互控件。
    /// </summary>
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

    /// <summary>
    /// 初始化竞技场渲染器。
    /// 使用一个 Image 承载 DrawingImage，每帧只重开 DrawingGroup 绘制，避免频繁增删 Canvas 子元素造成卡顿。
    /// </summary>
    private void InitializeArenaRenderer()
    {
        _arenaImage.Width = _world.ArenaWidth;
        _arenaImage.Height = _world.ArenaHeight;
        _arenaImage.Stretch = Stretch.Fill;
        _arenaImage.HorizontalAlignment = HorizontalAlignment.Left;
        _arenaImage.VerticalAlignment = VerticalAlignment.Top;
        _arenaImage.SnapsToDevicePixels = true;
        _arenaImage.IsHitTestVisible = false;
        Canvas.SetLeft(_arenaImage, 0);
        Canvas.SetTop(_arenaImage, 0);

        _arenaDrawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0, 0, _world.ArenaWidth, _world.ArenaHeight));
        _arenaDrawingGroup.GuidelineSet = new GuidelineSet(new[] { 0.0, _world.ArenaWidth }, new[] { 0.0, _world.ArenaHeight });
        _arenaImage.Source = new DrawingImage(_arenaDrawingGroup);

        ArenaCanvas.Children.Clear();
        ArenaCanvas.Children.Add(_arenaImage);
    }

    /// <summary>
    /// 根据当前战斗场地尺寸和缩放系数更新画布与渲染器。
    /// </summary>
    private void UpdateArenaViewport()
    {
        ArenaCanvas.Width = _world.ArenaWidth;
        ArenaCanvas.Height = _world.ArenaHeight;
        ArenaCanvas.LayoutTransform = new ScaleTransform(_arenaContentScale, _arenaContentScale);

        InitializeArenaRenderer();
    }
    /// <summary>
    /// 根据当前 UI 选角重新创建一局比赛。
    /// 1v1 时每边一个角色；2v2 时读取两组下拉框，并把团队生命值和描述聚合显示到侧栏。
    /// </summary>
    private void RestartMatch()
    {
        _sidePanelSyncAccumulator = 0;
        var leftKey = LeftFighterCombo.SelectedValue as string ?? "drunkard";
        var rightKey = RightFighterCombo.SelectedValue as string ?? "angry-man";

        if (_isTwoVsTwoMode)
        {
            var leftKey2 = LeftFighterCombo2.SelectedValue as string ?? leftKey;
            var rightKey2 = RightFighterCombo2.SelectedValue as string ?? rightKey;
            _world.StartMatch(new[] { leftKey, leftKey2 }, new[] { rightKey, rightKey2 });
        }
        else
        {
            _world.StartMatch(leftKey, rightKey);
        }

        UpdateArenaViewport();
        SyncSidePanel();
        RenderWorld();
    }

    /// <summary>
    /// 根据当前模式更新 2v2 额外下拉框、列宽占位、按钮文字和平衡性测试卡片可见性。
    /// </summary>
    private void UpdateModeControls()
    {
        LeftFighterCombo2.Visibility = _isTwoVsTwoMode ? Visibility.Visible : Visibility.Collapsed;
        RightFighterCombo2.Visibility = _isTwoVsTwoMode ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumnSpan(LeftFighterCombo, _isTwoVsTwoMode ? 1 : 2);
        Grid.SetColumnSpan(RightFighterCombo, _isTwoVsTwoMode ? 1 : 2);
        LeftFighterCombo.Margin = _isTwoVsTwoMode ? new Thickness(0, 0, 8, 0) : new Thickness(0);
        RightFighterCombo.Margin = _isTwoVsTwoMode ? new Thickness(0, 0, 8, 0) : new Thickness(0);
        BottomPlaceholderCard.Visibility = _isTwoVsTwoMode ? Visibility.Collapsed : Visibility.Visible;
        ModeToggleButton.Content = _isTwoVsTwoMode ? "切换1v1" : "切换2v2";
    }

    /// <summary>
    /// 根据当前模式自动选择推荐的场地缩放比例。当前仅保留用户手动缩放，不再随 2v2 自动改变画布大小。
    /// </summary>
    private void ApplyDefaultArenaScaleForMode()
    {
        SetArenaScale(1.0);
    }

    /// <summary>
    /// 设置场地缩放，并同步下拉框与渲染尺寸。
    /// </summary>
    private void SetArenaScale(double scale)
    {
        _arenaContentScale = scale;
        foreach (ComboBoxItem item in ScaleCombo.Items)
        {
            if (double.TryParse(item.Tag?.ToString(), out var tagScale) && Math.Abs(tagScale - scale) < 0.0001)
            {
                if (!ReferenceEquals(ScaleCombo.SelectedItem, item))
                {
                    ScaleCombo.SelectedItem = item;
                }
                UpdateArenaViewport();
                RenderWorld();
                return;
            }
        }

        UpdateArenaViewport();
        RenderWorld();
    }

    /// <summary>
    /// 倍速下拉框事件：读取 ComboBoxItem.Tag 中的倍率，后续帧循环会用该倍率缩放 dt。
    /// </summary>
    private void SpeedCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SpeedCombo.SelectedItem is ComboBoxItem item
            && double.TryParse(item.Tag?.ToString(), out var Speed)
            && Speed > 0)
        {
            _SpeedMultiplier = Speed;
        }
    }

    /// <summary>
    /// 场地缩放下拉框事件：读取 ComboBoxItem.Tag 中的缩放比例，并实时更新场地显示尺寸。
    /// </summary>
    private void ScaleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScaleCombo.SelectedItem is ComboBoxItem item
            && double.TryParse(item.Tag?.ToString(), out var scale)
            && scale > 0)
        {
            SetArenaScale(scale);
        }
    }

    /// <summary>
    /// WPF 帧循环：计算 dt，推进 Core 世界，再把世界状态绘制到 Canvas。
    /// </summary>
    private void GameLoopTick(object? sender, EventArgs e)
    {
        var now = _frameStopwatch.Elapsed.TotalSeconds;
        var dt = now - _lastFrameTimestamp;
        _lastFrameTimestamp = now;

        if (dt <= 0)
        {
            return;
        }

        dt = Math.Min(dt, 0.05);
        var scaledDt = dt * _SpeedMultiplier;

        if (!_isPaused)
        {
            _world.Update(scaledDt);
        }

        _sidePanelSyncAccumulator += scaledDt;
        if (_sidePanelSyncAccumulator >= 0.1 || _world.Winner is not null || _world.IsDraw || _isPaused)
        {
            _sidePanelSyncAccumulator = 0;
            SyncSidePanel();
        }

        RenderWorld();
    }

    /// <summary>
    /// 把 Core 里的角色状态同步到右侧 UI 面板。
    /// </summary>
    private void SyncSidePanel()
    {
        if (_world.Fighters.Count < 2)
        {
            return;
        }

        var leftTeam = _world.Fighters.Where(x => x.Side.Equals("left", StringComparison.OrdinalIgnoreCase)).ToList();
        var rightTeam = _world.Fighters.Where(x => x.Side.Equals("right", StringComparison.OrdinalIgnoreCase)).ToList();
        if (leftTeam.Count == 0 || rightTeam.Count == 0)
        {
            return;
        }

        StatusTextBlock.Text = _isPaused && _world.Winner is null && !_world.IsDraw
            ? "已暂停"
            : _world.StatusText;

        SyncTeamPanel(
            leftTeam,
            LeftNameTextBlock,
            LeftdescTextBlock,
            LeftHealthBar,
            LeftHealthTextBlock,
            LeftSideColor,
            LeftSideTextColor);

        SyncTeamPanel(
            rightTeam,
            RightNameTextBlock,
            RightdescTextBlock,
            RightHealthBar,
            RightHealthTextBlock,
            RightSideColor,
            RightSideTextColor);
    }

    /// <summary>
    /// 同步单个阵营的信息面板，包括队伍名称、技能描述、总血量进度条和血量文本。
    /// </summary>
    private void SyncTeamPanel(
        List<FighterState> team,
        TextBlock nameTextBlock,
        TextBlock descTextBlock,
        ProgressBar healthBar,
        TextBlock healthTextBlock,
        string sideColor,
        string descColor)
    {
        var totalHp = team.Sum(x => x.Definition.HP);
        var currentHp = team.Sum(x => Math.Max(0, x.Health));
        nameTextBlock.Text = string.Join(" / ", team.Select(x => x.Definition.Name));
        nameTextBlock.Foreground = CreateBrush(sideColor);
        descTextBlock.Text = string.Join("\n", team.Select(x => $"{x.Definition.Name}: {GetSkill(x).GetDescription(x)}"));
        descTextBlock.Foreground = CreateBrush(descColor);
        healthBar.Maximum = totalHp;
        healthBar.Value = Math.Max(0, currentHp);
        healthTextBlock.Text = $"团队 HP {currentHp:0} / {totalHp:0}";
        healthTextBlock.Foreground = CreateBrush(sideColor);
    }

    /// <summary>
    /// 使用单个 DrawingGroup 重绘竞技场，避免每帧创建/销毁大量 WPF UIElement 导致卡顿。
    /// </summary>
    private void RenderWorld()
    {
        using var dc = _arenaDrawingGroup.Open();
        DrawArenaBackground(dc);

        foreach (var effect in _world.Effects.Where(effect => effect.Type != BattleEffectType.DamageText))
        {
            DrawEffect(dc, effect);
        }

        foreach (var projectile in _world.Projectiles)
        {
            DrawProjectile(dc, projectile);
        }

        foreach (var fighter in _world.Fighters)
        {
            DrawFighter(dc, fighter);
        }

        foreach (var summonable in _world.Summonables)
        {
            DrawSummonable(dc, summonable);
        }

        foreach (var effect in _world.Effects.Where(effect => effect.Type == BattleEffectType.DamageText))
        {
            DrawEffect(dc, effect);
        }
    }

    /// <summary>
    /// 绘制竞技场背景。使用纯蓝色背景，避免网格干扰画面。
    /// </summary>
    private void DrawArenaBackground(DrawingContext dc)
    {
        dc.DrawRectangle(_arenaBackgroundBrush, null, new Rect(0, 0, _world.ArenaWidth, _world.ArenaHeight));
        dc.DrawRectangle(
            Brushes.Transparent,
            new Pen(CreateBrush("#B9DDFF", 0.65), 3),
            new Rect(1.5, 1.5, _world.ArenaWidth - 3, _world.ArenaHeight - 3));
    }

    /// <summary>
    /// 绘制战斗特效。
    /// 普通特效绘制为圆形光斑，爆炸特效绘制为外圈；伤害数字会转交给 DrawDamageText 单独处理。
    /// </summary>
    private void DrawEffect(DrawingContext dc, BattleEffect effect)
    {
        if (effect.Type == BattleEffectType.DamageText)
        {
            DrawDamageText(dc, effect);
            return;
        }

        var alpha = effect.Type == BattleEffectType.Explosion
            ? Math.Max(0, Math.Min(1, effect.RemainingTime / 0.85))
            : Math.Max(0.15, Math.Min(1, effect.RemainingTime / 0.4));
        var brush = CreateBrush(effect.ColorHex, alpha);
        var center = new Point(effect.Position.X, effect.Position.Y);

        if (effect.Type == BattleEffectType.Explosion)
        {
            dc.DrawEllipse(CreateBrush(effect.ColorHex, alpha * 0.12), new Pen(brush, 4), center, effect.Radius, effect.Radius);
            return;
        }

        dc.DrawEllipse(brush, null, center, effect.Radius, effect.Radius);
    }

    /// <summary>
    /// 绘制向上飘动并逐渐淡出的伤害数字，包含阴影层以保证在深浅背景上都清晰。
    /// </summary>
    private void DrawDamageText(DrawingContext dc, BattleEffect effect)
    {
        const double lifeTime = 0.8;
        var alpha = Math.Max(0, Math.Min(1, effect.RemainingTime / lifeTime));
        var rise = (1 - alpha) * 46;
        var brush = CreateBrush(effect.ColorHex, alpha);
        var text = CreateFormattedText(effect.Text ?? string.Empty, 26, brush, FontWeights.Black);
        var position = new Point(effect.Position.X - 18, effect.Position.Y - rise);

        dc.DrawText(text, position + new Vector(1, 1));
        dc.DrawText(CreateFormattedText(effect.Text ?? string.Empty, 26, CreateBrush("#000000", alpha * 0.45), FontWeights.Black), position);
        dc.DrawText(text, position);
    }

    /// <summary>
    /// 绘制投掷物贴图，并根据速度方向旋转，使棒球、瓶子等飞行方向更自然。
    /// </summary>
    private void DrawProjectile(DrawingContext dc, BattleProjectile projectile)
    {
        var brush = CreateImageFill(projectile.TexturePath, GetPrimaryColor(projectile.Side));
        var radius = projectile.Radius;
        var rect = new Rect(projectile.Position.X - radius, projectile.Position.Y - radius, radius * 2, radius * 2);
        var angleDegrees = Math.Atan2(projectile.Velocity.Y, projectile.Velocity.X) * 180 / Math.PI;

        dc.PushTransform(new RotateTransform(angleDegrees, projectile.Position.X, projectile.Position.Y));
        dc.DrawEllipse(brush, null, new Point(projectile.Position.X, projectile.Position.Y), radius, radius);
        dc.Pop();
    }

    /// <summary>
    /// 绘制单个角色，包括贴图身体、技能光圈和头顶血条。
    /// </summary>
    private void DrawFighter(DrawingContext dc, FighterState fighter)
    {
        if (!fighter.IsAlive)
        {
            return;
        }

        var radius = fighter.Definition.Radius * _world.EntityScale;
        var primaryColor = GetPrimaryColor(fighter.Side);
        var secondaryColor = GetSecondaryColor(fighter.Side);

        var enemy = FindNearestEnemyForRender(fighter);
        var shouldMirror = fighter.Definition.IsMirror == 1 && enemy is not null;
        var isFlipped = shouldMirror && (enemy!.Position.X - fighter.Position.X) < 0;

        DrawWatcherAura(dc, fighter);

        if (fighter.SkillFlashTime > 0)
        {
            var flashRadius = radius + 10 + (fighter.SkillFlashTime * 14);
            dc.DrawEllipse(
                CreateBrush(secondaryColor, 0.25 + (fighter.SkillFlashTime * 0.7)),
                null,
                new Point(fighter.Position.X, fighter.Position.Y),
                flashRadius,
                flashRadius);
        }

        var bodyBrush = CreateImageFill(fighter.Definition.TexturePath, primaryColor);
        var opacity = fighter.IsSleeping ? 0.5 : 1.0;

        dc.PushOpacity(opacity);
        if (isFlipped)
        {
            dc.PushTransform(new ScaleTransform(-1, 1, fighter.Position.X, fighter.Position.Y));
        }

        dc.DrawEllipse(bodyBrush, null, new Point(fighter.Position.X, fighter.Position.Y), radius, radius);

        if (isFlipped)
        {
            dc.Pop();
        }

        dc.Pop();

        if (fighter.IsSleeping)
        {
            DrawSleepZ(dc, fighter);
        }

        DrawArenaHintText(dc, fighter);
        DrawHealthBarAboveFighter(dc, fighter);
    }

    /// <summary>
    /// 为渲染阶段寻找最近敌人，避免每帧使用 LINQ 产生额外分配。
    /// </summary>
    private FighterState? FindNearestEnemyForRender(FighterState fighter)
    {
        FighterState? nearestEnemy = null;
        var nearestDistanceSquared = double.MaxValue;

        foreach (var candidate in _world.Fighters)
        {
            if (!candidate.IsAlive || candidate.Side.Equals(fighter.Side, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var delta = candidate.Position - fighter.Position;
            var distanceSquared = (delta.X * delta.X) + (delta.Y * delta.Y);
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestEnemy = candidate;
            }
        }

        return nearestEnemy;
    }

    /// <summary>
    /// 绘制观者角色专属环绕光效。根据观者当前状态切换蓝/红色，并带有轻微脉冲和旋转动画。
    /// </summary>
    private void DrawWatcherAura(DrawingContext dc, FighterState fighter)
    {
        if (!fighter.Definition.Key.Equals("watcher", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var auraColor = WatcherSkill.IsAngry(fighter) ? "#FF4D4D" : "#4DA6FF";
        var pulse = 0.5 + (0.5 * Math.Sin(_world.ElapsedTime * 5.5));
        var orbitRadius = (fighter.Definition.Radius * _world.EntityScale) + 3 + (pulse * 2.5);
        var time = _world.ElapsedTime;

        for (var i = 0; i < 12; i++)
        {
            var angle = ((Math.PI * 2) / 12 * i) + (time * 1.8);
            var wobble = Math.Sin((time * 7) + (i * 1.3)) * 3.5;
            var center = fighter.Position + new Vec2(
                Math.Cos(angle) * (orbitRadius + wobble),
                Math.Sin(angle) * (orbitRadius + wobble * 0.7));

            var width = 14 + ((i % 3) * 3) + (pulse * 2);
            var height = 7 + ((i % 2) * 2) + (pulse * 1.5);
            var alpha = WatcherSkill.IsAngry(fighter) ? 0.22 : 0.18;

            dc.PushTransform(new RotateTransform((angle * 180 / Math.PI) + 90, center.X, center.Y));
            dc.DrawEllipse(CreateBrush(auraColor, alpha), null, new Point(center.X, center.Y), width / 2, height / 2);
            dc.Pop();
        }

        var softGlowRadius = (fighter.Definition.Radius * _world.EntityScale) + 6;
        dc.DrawEllipse(
            CreateBrush(auraColor, WatcherSkill.IsAngry(fighter) ? 0.11 : 0.09),
            null,
            new Point(fighter.Position.X, fighter.Position.Y),
            softGlowRadius,
            softGlowRadius);
    }

    /// <summary>
    /// 绘制角色头顶的睡眠提示文字。
    /// </summary>
    private void DrawSleepZ(DrawingContext dc, FighterState fighter)
    {
        var zOffset = (2 - fighter.SleepTime) * 10;
        var shadow = CreateFormattedText("Zzz...", 18, CreateBrush("#000000", 0.7), FontWeights.Bold);
        var text = CreateFormattedText("Zzz...", 18, CreateBrush(GetSecondaryColor(fighter.Side)), FontWeights.Bold);
        var x = fighter.Position.X - (text.Width / 2);
        var y = fighter.Position.Y - (fighter.Definition.Radius * _world.EntityScale) - 40 - zOffset;

        dc.DrawText(shadow, new Point(x + 1, y + 1));
        dc.DrawText(text, new Point(x, y));
    }

    /// <summary>
    /// 绘制角色头顶血条。
    /// </summary>
    private void DrawHealthBarAboveFighter(DrawingContext dc, FighterState fighter)
    {
        const double width = 70;
        const double height = 6;
        var left = fighter.Position.X - (width / 2);
        var top = fighter.Position.Y - (fighter.Definition.Radius * _world.EntityScale) - 18;
        var hpRatio = Math.Max(0, fighter.Health / fighter.Definition.HP);
        var barColor = fighter.Side.Equals("left", StringComparison.OrdinalIgnoreCase)
            ? LeftSideColor
            : RightSideColor;

        dc.DrawRectangle(CreateBrush("#66000000"), null, new Rect(left, top, width, height));
        dc.DrawRectangle(CreateBrush(barColor), null, new Rect(left, top, width * hpRatio, height));
    }

    /// <summary>
    /// 绘制召唤物实体与血条。
    /// </summary>
    private void DrawSummonable(DrawingContext dc, SummonableState summonable)
    {
        if (!summonable.IsAlive)
        {
            return;
        }

        dc.DrawEllipse(
            CreateImageFill(summonable.TexturePath, GetPrimaryColor(summonable.Side)),
            null,
            new Point(summonable.Position.X, summonable.Position.Y),
            summonable.Radius,
            summonable.Radius);

        DrawHealthBarAboveSummonable(dc, summonable);
    }

    /// <summary>
    /// 绘制召唤物头顶小血条。召唤物尺寸更小，因此血条宽高也比角色血条更紧凑。
    /// </summary>
    private void DrawHealthBarAboveSummonable(DrawingContext dc, SummonableState summonable)
    {
        const double width = 40;
        const double height = 4;
        var left = summonable.Position.X - (width / 2);
        var top = summonable.Position.Y - summonable.Radius - 12;
        var hpRatio = Math.Max(0, summonable.Health / summonable.HP);
        var barColor = summonable.Side.Equals("left", StringComparison.OrdinalIgnoreCase)
            ? LeftSideColor
            : RightSideColor;

        dc.DrawRectangle(CreateBrush("#66000000"), null, new Rect(left, top, width, height));
        dc.DrawRectangle(CreateBrush(barColor), null, new Rect(left, top, width * hpRatio, height));
    }

    /// <summary>
    /// 获取阵营主色。左方固定为蓝色，右方固定为红色，用于角色、投掷物和 UI 强调色。
    /// </summary>
    private static string GetPrimaryColor(string side)
    {
        return side.Equals("left", StringComparison.OrdinalIgnoreCase)
            ? LeftSideColor
            : RightSideColor;
    }

    /// <summary>
    /// 从技能注册表中获取角色技能实例。
    /// </summary>
    private IFighterSkill GetSkill(FighterState fighter)
    {
        return SkillRegistry.Get(fighter.Definition.Key);
    }

    /// <summary>
    /// 绘制技能提供的场内提示文字。
    /// 例如 qzd 的蓄力预告，会通过技能接口返回，而不是在窗口类里硬编码角色分支。
    /// </summary>
    private void DrawArenaHintText(DrawingContext dc, FighterState fighter)
    {
        var hintText = GetSkill(fighter).GetArenaHintText(fighter);
        if (string.IsNullOrWhiteSpace(hintText))
        {
            return;
        }

        var shadow = CreateFormattedText(hintText, 14, CreateBrush("#000000", 0.65), FontWeights.Bold);
        var text = CreateFormattedText(hintText, 14, CreateBrush(GetSecondaryColor(fighter.Side)), FontWeights.Bold);
        var x = fighter.Position.X - (text.Width / 2);
        var y = fighter.Position.Y - (fighter.Definition.Radius * _world.EntityScale) - 40;

        dc.DrawText(shadow, new Point(x + 1, y + 1));
        dc.DrawText(text, new Point(x, y));
    }

    /// <summary>
    /// 创建 WPF 文本绘制对象，统一字体、语言环境、字号、颜色和字重。
    /// </summary>
    private static FormattedText CreateFormattedText(string text, double fontSize, Brush brush, FontWeight fontWeight)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, fontWeight, FontStretches.Normal),
            fontSize,
            brush,
            1.0);
    }

    /// <summary>
    /// 获取阵营辅助文字色，用于技能提示和描述文本。
    /// </summary>
    private static string GetSecondaryColor(string side)
    {
        return side.Equals("left", StringComparison.OrdinalIgnoreCase)
            ? LeftSideTextColor
            : RightSideTextColor;
    }

    /// <summary>
    /// 根据逻辑贴图路径创建 ImageBrush。
    /// 若资源不存在则退回纯色画刷；成功加载后会缓存并 Freeze，降低每帧绘制开销。
    /// </summary>
    private static Brush CreateImageFill(string texturePath, string fallbackColorHex)
    {
        var resolvedTexturePath = TexturePathResolver.Resolve(texturePath);
        var absoluteTexturePath = global::System.IO.Path.Combine(AppContext.BaseDirectory, resolvedTexturePath);
        if (!global::System.IO.File.Exists(absoluteTexturePath))
        {
            return CreateBrush(fallbackColorHex);
        }

        if (ImageBrushCache.TryGetValue(absoluteTexturePath, out var cachedBrush))
        {
            return cachedBrush;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.UriSource = new global::System.Uri(absoluteTexturePath, global::System.UriKind.Absolute);
        image.EndInit();
        image.Freeze();

        var brush = new ImageBrush
        {
            ImageSource = image,
            Stretch = Stretch.UniformToFill
        };
        brush.Freeze();
        ImageBrushCache[absoluteTexturePath] = brush;
        return brush;
    }

    /// <summary>
    /// 应用次级按钮主题，通常用于重开、切换模式、平衡性测试等白底/暗底按钮。
    /// </summary>
    private static void ApplySecondaryButtonTheme(Button button, Brush background, Brush foreground, Brush borderBrush)
    {
        button.Background = background;
        button.Foreground = foreground;
        button.BorderBrush = borderBrush;
        ApplyButtonContentForeground(button, foreground);
    }

    /// <summary>
    /// 应用主按钮主题，通常用于“暂停/继续”等主要操作按钮。
    /// </summary>
    private static void ApplyPrimaryButtonTheme(Button button, Brush background, Brush foreground)
    {
        button.Background = background;
        button.Foreground = foreground;
        button.BorderBrush = background;
        ApplyButtonContentForeground(button, foreground);
    }

    /// <summary>
    /// 同步按钮模板内部 TextBlock 的前景色，避免 WPF 模板文本仍保留旧主题颜色。
    /// </summary>
    private static void ApplyButtonContentForeground(Button button, Brush foreground)
    {
        foreach (var textBlock in FindVisualChildren<TextBlock>(button))
        {
            textBlock.Foreground = foreground;
        }
    }

    /// <summary>
    /// 创建竞技场背景画刷。当前是纯色入口，保留方法方便未来替换为渐变或纹理背景。
    /// </summary>
    private static Brush CreateArenaBackgroundBrush(string color)
    {
        return CreateBrush(color);
    }

    /// <summary>
    /// 创建并缓存 SolidColorBrush。
    /// 缓存键包含颜色和透明度；画刷会 Freeze，便于跨绘制调用安全复用。
    /// </summary>
    private static SolidColorBrush CreateBrush(string hex, double opacity = 1)
    {
        var cacheKey = $"{hex}|{Math.Round(opacity, 3)}";
        if (SolidBrushCache.TryGetValue(cacheKey, out var cachedBrush))
        {
            return cachedBrush;
        }

        var color = (Color)ColorConverter.ConvertFromString(hex)!;
        color.A = (byte)Math.Clamp(opacity * 255, 0, 255);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        SolidBrushCache[cacheKey] = brush;
        return brush;
    }
}
