using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
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

    private static readonly Dictionary<string, SolidColorBrush> SolidBrushCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Brush> ImageBrushCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly BattleWorld _world = new();
    private readonly DrawingGroup _arenaDrawingGroup = new();
    private readonly Image _arenaImage = new();
    private Brush _arenaBackgroundBrush = CreateArenaBackgroundBrush(ArenaBackgroundLightColor);

    private readonly DispatcherTimer _timer;
    private DateTime _lastFrameTime;
    private bool _isLoaded;
    private bool _isPaused;
    private double _sidePanelSyncAccumulator;
    private double _SpeedMultiplier = 1.0;
    private bool _isBalanceTesting;
    private bool _isDarkTheme;

    public MainWindow()
    {
        InitializeComponent();
        ApplyTheme(false);

        ArenaCanvas.Width = _world.ArenaWidth;
        ArenaCanvas.Height = _world.ArenaHeight;
        RenderOptions.SetBitmapScalingMode(ArenaCanvas, BitmapScalingMode.LowQuality);
        RenderOptions.SetEdgeMode(ArenaCanvas, EdgeMode.Aliased);

        InitializeArenaRenderer();

        LeftHealthBar.Foreground = CreateBrush(LeftSideColor);
        RightHealthBar.Foreground = CreateBrush(RightSideColor);

        LeftFighterCombo.ItemsSource = FighterCatalog.All;
        RightFighterCombo.ItemsSource = FighterCatalog.All;
        LeftFighterCombo.DisplayMemberPath = nameof(FighterDefinition.Name);
        RightFighterCombo.DisplayMemberPath = nameof(FighterDefinition.Name);
        LeftFighterCombo.SelectedValuePath = nameof(FighterDefinition.Key);
        RightFighterCombo.SelectedValuePath = nameof(FighterDefinition.Key);
        LeftFighterCombo.SelectedValue = "drunkard";
        RightFighterCombo.SelectedValue = "angry-man";
        SpeedCombo.SelectedIndex = 1;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += GameLoopTick;

        Loaded += (_, _) =>
        {
            _isLoaded = true;
            RestartMatch();
            _lastFrameTime = DateTime.Now;
            _timer.Start();
        };

        Closed += (_, _) => _timer.Stop();
    }

    private void RestartButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isPaused = false;
        PauseButton.Content = "暂停";
        _lastFrameTime = DateTime.Now;
        RestartMatch();
    }

    private void PauseButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isPaused = !_isPaused;
        PauseButton.Content = _isPaused ? "继续" : "暂停";
        _lastFrameTime = DateTime.Now;
        SyncSidePanel();
    }

    private void ThemeToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyTheme(!_isDarkTheme);
    }

    private async void BalanceTestButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isBalanceTesting)
        {
            return;
        }

        _isBalanceTesting = true;
        BalanceTestButton.IsEnabled = false;

        try
        {
            var leftKey = LeftFighterCombo.SelectedValue as string ?? "drunkard";
            var rightKey = RightFighterCombo.SelectedValue as string ?? "angry-man";
            var leftName = (LeftFighterCombo.SelectedItem as FighterDefinition)?.Name ?? leftKey;
            var rightName = (RightFighterCombo.SelectedItem as FighterDefinition)?.Name ?? rightKey;
            const int halfMatches = 500;
            const int totalMatches = halfMatches * 2;
            const double simulationSpeed = 32.0;
            const double baseDt = 1.0 / 60.0;
            const double maxSimulatedSecondsPerMatch = 120;

            var result = await System.Threading.Tasks.Task.Run(() =>
            {
                var firstHalfLeftWins = 0;
                var firstHalfRightWins = 0;
                var firstHalfDraws = 0;

                var secondHalfLeftWins = 0;
                var secondHalfRightWins = 0;
                var secondHalfDraws = 0;

                for (var match = 0; match < halfMatches; match++)
                {
                    var simWorld = new BattleWorld();
                    simWorld.StartMatch(leftKey, rightKey);

                    var elapsed = 0.0;
                    while (simWorld.Winner is null && !simWorld.IsDraw && elapsed < maxSimulatedSecondsPerMatch)
                    {
                        simWorld.Update(baseDt * simulationSpeed);
                        elapsed += baseDt * simulationSpeed;
                    }

                    if (simWorld.Winner is null || simWorld.IsDraw)
                    {
                        firstHalfDraws++;
                    }
                    else if (simWorld.Winner.Side.Equals("left", StringComparison.OrdinalIgnoreCase))
                    {
                        firstHalfLeftWins++;
                    }
                    else
                    {
                        firstHalfRightWins++;
                    }
                }

                for (var match = 0; match < halfMatches; match++)
                {
                    var simWorld = new BattleWorld();
                    simWorld.StartMatch(rightKey, leftKey);

                    var elapsed = 0.0;
                    while (simWorld.Winner is null && !simWorld.IsDraw && elapsed < maxSimulatedSecondsPerMatch)
                    {
                        simWorld.Update(baseDt * simulationSpeed);
                        elapsed += baseDt * simulationSpeed;
                    }

                    if (simWorld.Winner is null || simWorld.IsDraw)
                    {
                        secondHalfDraws++;
                    }
                    else if (simWorld.Winner.Side.Equals("left", StringComparison.OrdinalIgnoreCase))
                    {
                        secondHalfRightWins++;
                    }
                    else
                    {
                        secondHalfLeftWins++;
                    }
                }

                var leftTotalWins = firstHalfLeftWins + secondHalfLeftWins;
                var rightTotalWins = firstHalfRightWins + secondHalfRightWins;
                var totalDraws = firstHalfDraws + secondHalfDraws;

                return (
                    firstHalfLeftWins,
                    firstHalfRightWins,
                    firstHalfDraws,
                    secondHalfLeftWins,
                    secondHalfRightWins,
                    secondHalfDraws,
                    leftTotalWins,
                    rightTotalWins,
                    totalDraws);
            });

            StatusTextBlock.Text = $"测试完成：{leftName} 总胜 {result.leftTotalWins} / {rightName} 总胜 {result.rightTotalWins} / 平局 {result.totalDraws}";
            MessageBox.Show(
                this,
                $"32x 快速战斗 {totalMatches} 场结果：\n\n上半场（{halfMatches}场，左右不变）\n- 左方 {leftName} 胜场：{result.firstHalfLeftWins}\n- 右方 {rightName} 胜场：{result.firstHalfRightWins}\n- 平局：{result.firstHalfDraws}\n\n下半场（{halfMatches}场，互换左右）\n- {leftName} 胜场：{result.secondHalfLeftWins}\n- {rightName} 胜场：{result.secondHalfRightWins}\n- 平局：{result.secondHalfDraws}\n\n总计\n- {leftName} 总胜场：{result.leftTotalWins}\n- {rightName} 总胜场：{result.rightTotalWins}\n- 总平局：{result.totalDraws}",
                "平衡性测试",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        finally
        {
            _isBalanceTesting = false;
            BalanceTestButton.IsEnabled = true;
        }
    }

    private void FighterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoaded)
        {
            RestartMatch();
        }
    }

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
        var secondaryButtonForeground = CreateBrush(isDarkTheme ? "#E8EAED" : "#202124");
        var controlBorderBrush = CreateBrush(isDarkTheme ? "#5F6368" : "#DADCE0");
        var controlBackgroundBrush = CreateBrush(isDarkTheme ? "#303134" : "#FFFFFF");

        ThemeToggleButton.Content = isDarkTheme ? "亮色" : "暗色";
        ThemeToggleButton.Background = secondaryButtonBackground;
        ThemeToggleButton.Foreground = secondaryButtonForeground;
        ThemeToggleButton.BorderBrush = controlBorderBrush;

        RestartButton.Background = secondaryButtonBackground;
        RestartButton.Foreground = secondaryButtonForeground;
        RestartButton.BorderBrush = controlBorderBrush;
        BalanceTestButton.Background = secondaryButtonBackground;
        BalanceTestButton.Foreground = secondaryButtonForeground;
        BalanceTestButton.BorderBrush = controlBorderBrush;

        LeftFighterCombo.Background = controlBackgroundBrush;
        LeftFighterCombo.Foreground = primaryTextBrush;
        LeftFighterCombo.BorderBrush = controlBorderBrush;
        RightFighterCombo.Background = controlBackgroundBrush;
        RightFighterCombo.Foreground = primaryTextBrush;
        RightFighterCombo.BorderBrush = controlBorderBrush;
        SpeedCombo.Background = controlBackgroundBrush;
        SpeedCombo.Foreground = primaryTextBrush;
        SpeedCombo.BorderBrush = controlBorderBrush;

        foreach (var textBlock in FindVisualChildren<TextBlock>(this))
        {
            if (ReferenceEquals(textBlock, LeftNameTextBlock)
                || ReferenceEquals(textBlock, RightNameTextBlock)
                || ReferenceEquals(textBlock, LeftHealthTextBlock)
                || ReferenceEquals(textBlock, RightHealthTextBlock)
                || ReferenceEquals(textBlock, LeftdescTextBlock)
                || ReferenceEquals(textBlock, RightdescTextBlock)
                || ReferenceEquals(textBlock, StatusTextBlock))
            {
                continue;
            }

            textBlock.Foreground = mutedTextBrush;
        }

        RenderWorld();
        SyncSidePanel();
    }

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

    private void RestartMatch()
    {
        _sidePanelSyncAccumulator = 0;
        var leftKey = LeftFighterCombo.SelectedValue as string ?? "drunkard";
        var rightKey = RightFighterCombo.SelectedValue as string ?? "angry-man";
        _world.StartMatch(leftKey, rightKey);
        SyncSidePanel();
        RenderWorld();
    }

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
    /// WPF 帧循环：计算 dt，推进 Core 世界，再把世界状态绘制到 Canvas。
    /// </summary>
    private void GameLoopTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var dt = (now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;

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

        var left = _world.Fighters[0];
        var right = _world.Fighters[1];

        StatusTextBlock.Text = _isPaused && _world.Winner is null && !_world.IsDraw
            ? "已暂停"
            : _world.StatusText;

        LeftNameTextBlock.Text = left.Definition.Name;
        LeftNameTextBlock.Foreground = CreateBrush(LeftSideColor);
        LeftdescTextBlock.Text = GetSkill(left).GetDescription(left);
        LeftdescTextBlock.Foreground = CreateBrush(LeftSideTextColor);
        LeftHealthBar.Maximum = left.Definition.HP;
        LeftHealthBar.Value = Math.Max(0, left.Health);
        LeftHealthTextBlock.Text = $"HP {Math.Max(0, left.Health):0} / {left.Definition.HP:0}";
        LeftHealthTextBlock.Foreground = CreateBrush(LeftSideColor);

        RightNameTextBlock.Text = right.Definition.Name;
        RightNameTextBlock.Foreground = CreateBrush(RightSideColor);
        RightdescTextBlock.Text = GetSkill(right).GetDescription(right);
        RightdescTextBlock.Foreground = CreateBrush(RightSideTextColor);
        RightHealthBar.Maximum = right.Definition.HP;
        RightHealthBar.Value = Math.Max(0, right.Health);
        RightHealthTextBlock.Text = $"HP {Math.Max(0, right.Health):0} / {right.Definition.HP:0}";
        RightHealthTextBlock.Foreground = CreateBrush(RightSideColor);
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

    private void DrawProjectile(DrawingContext dc, BattleProjectile projectile)
    {
        var brush = CreateImageFill(projectile.TexturePath, projectile.ColorHex);
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

        var radius = fighter.Definition.Radius;
        var primaryColor = GetPrimaryColor(fighter.Side);
        var secondaryColor = GetSecondaryColor(fighter.Side);

        var enemy = _world.Fighters.FirstOrDefault(x => x.Id != fighter.Id);
        var shouldMirror = fighter.Definition.isMirror == 1 && enemy is not null;
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

    private void DrawWatcherAura(DrawingContext dc, FighterState fighter)
    {
        if (!fighter.Definition.Key.Equals("watcher", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var auraColor = WatcherSkill.IsAngry(fighter) ? "#FF4D4D" : "#4DA6FF";
        var pulse = 0.5 + (0.5 * Math.Sin(_world.ElapsedTime * 5.5));
        var orbitRadius = fighter.Definition.Radius + 3 + (pulse * 2.5);
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

        var softGlowRadius = fighter.Definition.Radius + 6;
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
        var y = fighter.Position.Y - fighter.Definition.Radius - 40 - zOffset;

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
        var top = fighter.Position.Y - fighter.Definition.Radius - 18;
        var hpRatio = Math.Max(0, fighter.Health / fighter.Definition.HP);
        var barColor = fighter.Side.Equals("left", StringComparison.OrdinalIgnoreCase)
            ? LeftSideColor
            : RightSideColor;

        dc.DrawRectangle(CreateBrush("#66000000"), null, new Rect(left, top, width, height));
        dc.DrawRectangle(CreateBrush(barColor), null, new Rect(left, top, width * hpRatio, height));
    }

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

    private static string GetPrimaryColor(string side)
    {
        return side.Equals("left", StringComparison.OrdinalIgnoreCase)
            ? LeftSideColor
            : RightSideColor;
    }

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
        var y = fighter.Position.Y - fighter.Definition.Radius - 40;

        dc.DrawText(shadow, new Point(x + 1, y + 1));
        dc.DrawText(text, new Point(x, y));
    }

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

    private static string GetSecondaryColor(string side)
    {
        return side.Equals("left", StringComparison.OrdinalIgnoreCase)
            ? LeftSideTextColor
            : RightSideTextColor;
    }

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

    private static Brush CreateArenaBackgroundBrush(string color)
    {
        return CreateBrush(color);
    }

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