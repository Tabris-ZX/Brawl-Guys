using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using FightingGuys.Core;

namespace FightingGuys.Wpf;

public partial class MainWindow : Window
{
    private const string ArenaBackgroundColor = "#3768B8";
    private const string LeftSideColor = "#5AA9FF";
    private const string LeftSideTextColor = "#8FC8FF";
    private const string RightSideColor = "#FF6B7A";
    private const string RightSideTextColor = "#FFB3BC";

    private static readonly Dictionary<string, SolidColorBrush> SolidBrushCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Brush> ImageBrushCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly BattleWorld _world = new(new ArenaDefinition());
    private readonly Brush _arenaBackgroundBrush = CreateArenaBackgroundBrush();

    private readonly DispatcherTimer _timer;
    private DateTime _lastFrameTime;
    private bool _isLoaded;
    private bool _isPaused;
    private double _sidePanelSyncAccumulator;
    private double _SpeedMultiplier = 1.0;
    private bool _isBalanceTesting;

    public MainWindow()
    {
        InitializeComponent();

        ArenaCanvas.Width = _world.Arena.Width;
        ArenaCanvas.Height = _world.Arena.Height;
        RenderOptions.SetBitmapScalingMode(ArenaCanvas, BitmapScalingMode.LowQuality);
        RenderOptions.SetEdgeMode(ArenaCanvas, EdgeMode.Aliased);

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
            const int halfMatches = 100;
            const int totalMatches = halfMatches * 2;
            const double simulationSpeed = 16.0;
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
                    var simWorld = new BattleWorld(new ArenaDefinition());
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
                    var simWorld = new BattleWorld(new ArenaDefinition());
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
                $"16x 快速战斗 {totalMatches} 场结果：\n\n上半场（{halfMatches}场，左右不变）\n- 左方 {leftName} 胜场：{result.firstHalfLeftWins}\n- 右方 {rightName} 胜场：{result.firstHalfRightWins}\n- 平局：{result.firstHalfDraws}\n\n下半场（{halfMatches}场，互换左右）\n- {leftName} 胜场：{result.secondHalfLeftWins}\n- {rightName} 胜场：{result.secondHalfRightWins}\n- 平局：{result.secondHalfDraws}\n\n总计\n- {leftName} 总胜场：{result.leftTotalWins}\n- {rightName} 总胜场：{result.rightTotalWins}\n- 总平局：{result.totalDraws}",
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

    private void RestartMatch()
    {
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
        LeftdescTextBlock.Text = GetFighterDescriptionText(left);
        LeftdescTextBlock.Foreground = CreateBrush(LeftSideTextColor);
        LeftHealthBar.Maximum = left.Definition.HP;
        LeftHealthBar.Value = Math.Max(0, left.Health);
        LeftHealthTextBlock.Text = $"HP {Math.Max(0, left.Health):0} / {left.Definition.HP:0}";
        LeftHealthTextBlock.Foreground = CreateBrush(LeftSideColor);

        RightNameTextBlock.Text = right.Definition.Name;
        RightNameTextBlock.Foreground = CreateBrush(RightSideColor);
        RightdescTextBlock.Text = GetFighterDescriptionText(right);
        RightdescTextBlock.Foreground = CreateBrush(RightSideTextColor);
        RightHealthBar.Maximum = right.Definition.HP;
        RightHealthBar.Value = Math.Max(0, right.Health);
        RightHealthTextBlock.Text = $"HP {Math.Max(0, right.Health):0} / {right.Definition.HP:0}";
        RightHealthTextBlock.Foreground = CreateBrush(RightSideColor);
    }

    /// <summary>
    /// 重新绘制整个竞技场。当前 MVP 直接清空重画，后续可优化为对象复用。
    /// </summary>
    private void RenderWorld()
    {
        ArenaCanvas.Children.Clear();
        DrawArenaBackground();

        foreach (var effect in _world.Effects.Where(effect => effect.Type != BattleEffectType.DamageText))
        {
            DrawEffect(effect);
        }

        foreach (var projectile in _world.Projectiles)
        {
            DrawProjectile(projectile);
        }

        foreach (var fighter in _world.Fighters)
        {
            DrawFighter(fighter);
        }

        foreach (var drone in _world.Drones)
        {
            DrawDrone(drone);
        }

        foreach (var effect in _world.Effects.Where(effect => effect.Type == BattleEffectType.DamageText))
        {
            DrawEffect(effect);
        }
    }

    /// <summary>
    /// 绘制竞技场背景。使用纯蓝色背景，避免网格干扰画面。
    /// </summary>
    private void DrawArenaBackground()
    {
        var background = new Rectangle
        {
            Width = _world.Arena.Width,
            Height = _world.Arena.Height,
            Fill = _arenaBackgroundBrush,
            IsHitTestVisible = false
        };
        ArenaCanvas.Children.Add(background);

        var borderGlow = new Rectangle
        {
            Width = _world.Arena.Width,
            Height = _world.Arena.Height,
            Stroke = CreateBrush("#B9DDFF", 0.65),
            StrokeThickness = 3,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        ArenaCanvas.Children.Add(borderGlow);
    }

    private void DrawEffect(BattleEffect effect)
    {
        if (effect.Type == BattleEffectType.DamageText)
        {
            DrawDamageText(effect);
            return;
        }

        var alpha = effect.Type == BattleEffectType.Explosion
            ? Math.Max(0, Math.Min(1, effect.RemainingTime / 0.85))
            : Math.Max(0.15, Math.Min(1, effect.RemainingTime / 0.4));
        var brush = CreateBrush(effect.ColorHex, alpha);

        var shape = new Ellipse
        {
            Width = effect.Radius * 2,
            Height = effect.Radius * 2,
            StrokeThickness = effect.Type is BattleEffectType.Ring or BattleEffectType.Explosion ? 4 : 0,
            Stroke = effect.Type is BattleEffectType.Ring or BattleEffectType.Explosion ? brush : null,
            Fill = effect.Type is BattleEffectType.Ring or BattleEffectType.Explosion ? CreateBrush(effect.ColorHex, alpha * 0.12) : brush,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(shape, effect.Position.X - effect.Radius);
        Canvas.SetTop(shape, effect.Position.Y - effect.Radius);
        ArenaCanvas.Children.Add(shape);
    }

    private void DrawDamageText(BattleEffect effect)
    {
        const double lifeTime = 0.8;
        var alpha = Math.Max(0, Math.Min(1, effect.RemainingTime / lifeTime));
        var rise = (1 - alpha) * 46;

        var text = new TextBlock
        {
            Text = effect.Text ?? string.Empty,
            Foreground = CreateBrush(effect.ColorHex, alpha),
            FontSize = 26,
            FontWeight = FontWeights.Black,
            IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 5,
                ShadowDepth = 1,
                Color = Colors.Black,
                Opacity = alpha
            }
        };

        Canvas.SetLeft(text, effect.Position.X - 18);
        Canvas.SetTop(text, effect.Position.Y - rise);
        ArenaCanvas.Children.Add(text);
    }

    private void DrawProjectile(BattleProjectile projectile)
    {
        var angleDegrees = Math.Atan2(projectile.Velocity.Y, projectile.Velocity.X) * 180 / Math.PI;
        var body = new Ellipse
        {
            Width = projectile.Radius * 2,
            Height = projectile.Radius * 2,
            Fill = CreateImageFill(projectile.TexturePath, projectile.ColorHex),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(angleDegrees),
            IsHitTestVisible = false
        };

        Canvas.SetLeft(body, projectile.Position.X - projectile.Radius);
        Canvas.SetTop(body, projectile.Position.Y - projectile.Radius);
        ArenaCanvas.Children.Add(body);
    }

    /// <summary>
    /// 绘制单个角色，包括贴图身体、技能光圈和头顶血条。
    /// </summary>
    private void DrawFighter(FighterState fighter)
    {
        if (!fighter.IsAlive)
        {
            return;
        }

        var radius = fighter.Definition.Radius;
        var primaryColor = GetPrimaryColor(fighter.Side);
        var secondaryColor = GetSecondaryColor(fighter.Side);

        // 计算镜像
        var enemy = _world.Fighters.FirstOrDefault(x => x.Id != fighter.Id);
        var shouldMirror = fighter.Definition.isMirror == 1 && enemy is not null;
        var isFlipped = shouldMirror && (enemy!.Position.X - fighter.Position.X) < 0;

        var body = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = CreateImageFill(fighter.Definition.TexturePath, primaryColor),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(isFlipped ? -1 : 1, 1),
            IsHitTestVisible = false
        };

        if (fighter.SkillFlashTime > 0)
        {
            var flashRadius = radius + 10 + (fighter.SkillFlashTime * 14);
            var aura = new Ellipse
            {
                Width = flashRadius * 2,
                Height = flashRadius * 2,
                Fill = CreateBrush(secondaryColor, 0.25 + (fighter.SkillFlashTime * 0.7)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(aura, fighter.Position.X - flashRadius);
            Canvas.SetTop(aura, fighter.Position.Y - flashRadius);
            ArenaCanvas.Children.Add(aura);
        }

        if (fighter.IsSleeping)
        {
            // 睡眠时变暗
            body.Opacity = 0.5;
        }

        Canvas.SetLeft(body, fighter.Position.X - radius);
        Canvas.SetTop(body, fighter.Position.Y - radius);
        ArenaCanvas.Children.Add(body);

        if (fighter.IsSleeping)
        {
            DrawSleepZ(fighter);
        }

        DrawChargePreview(fighter);
        DrawHealthBarAboveFighter(fighter);
    }

    private void DrawSleepZ(FighterState fighter)
    {
        var zText = new TextBlock
        {
            Text = "Zzz...",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = CreateBrush(GetSecondaryColor(fighter.Side)),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 3,
                ShadowDepth = 1,
                Color = Colors.Black,
                Opacity = 0.7
            },
            IsHitTestVisible = false
        };

        zText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var zSize = zText.DesiredSize;
        var zOffset = (2 - fighter.SleepTime) * 10; // 慢慢向上飘
        Canvas.SetLeft(zText, fighter.Position.X - zSize.Width / 2);
        Canvas.SetTop(zText, fighter.Position.Y - fighter.Definition.Radius - 40 - zOffset);
        ArenaCanvas.Children.Add(zText);
    }

    private void DrawEye(FighterState fighter, double radius, double offsetYScale)
    {
        var direction = fighter.Velocity.Length > 0.001 ? fighter.Velocity.Normalized() : new Vec2(1, 0);
        var eyeCenter = fighter.Position + new Vec2(direction.X * radius * 0.35, direction.Y * radius * 0.35 + (radius * offsetYScale));

        var eyeWhite = new Ellipse
        {
            Width = radius * 0.44,
            Height = radius * 0.44,
            Fill = CreateBrush(GetSecondaryColor(fighter.Side)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(eyeWhite, eyeCenter.X - (eyeWhite.Width / 2));
        Canvas.SetTop(eyeWhite, eyeCenter.Y - (eyeWhite.Height / 2));
        ArenaCanvas.Children.Add(eyeWhite);

        var pupil = new Ellipse
        {
            Width = radius * 0.2,
            Height = radius * 0.2,
            Fill = CreateBrush("#0D1320"),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(pupil, eyeCenter.X - (pupil.Width / 2));
        Canvas.SetTop(pupil, eyeCenter.Y - (pupil.Height / 2));
        ArenaCanvas.Children.Add(pupil);
    }

    private void DrawHealthBarAboveFighter(FighterState fighter)
    {
        const double width = 70;
        const double height = 6;
        var left = fighter.Position.X - (width / 2);
        var top = fighter.Position.Y - fighter.Definition.Radius - 18;
        var hpRatio = Math.Max(0, fighter.Health / fighter.Definition.HP);

        var background = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = CreateBrush("#66000000"),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(background, left);
        Canvas.SetTop(background, top);
        ArenaCanvas.Children.Add(background);

        var barColor = fighter.Side.Equals("left", StringComparison.OrdinalIgnoreCase)
            ? LeftSideColor
            : RightSideColor;

        var foreground = new Rectangle
        {
            Width = width * hpRatio,
            Height = height,
            Fill = CreateBrush(barColor),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(foreground, left);
        Canvas.SetTop(foreground, top);
        ArenaCanvas.Children.Add(foreground);
    }

    private void DrawDrone(DroneState drone)
    {
        if (!drone.IsAlive)
        {
            return;
        }

        var body = new Ellipse
        {
            Width = drone.Radius * 2,
            Height = drone.Radius * 2,
            Fill = CreateImageFill(drone.TexturePath, GetPrimaryColor(drone.Side)),
            IsHitTestVisible = false
        };

        Canvas.SetLeft(body, drone.Position.X - drone.Radius);
        Canvas.SetTop(body, drone.Position.Y - drone.Radius);
        ArenaCanvas.Children.Add(body);

        DrawHealthBarAboveDrone(drone);
    }

    private void DrawHealthBarAboveDrone(DroneState drone)
    {
        const double width = 40;
        const double height = 4;
        var left = drone.Position.X - (width / 2);
        var top = drone.Position.Y - drone.Radius - 12;
        var hpRatio = Math.Max(0, drone.Health / drone.HP);
        var barColor = drone.Side.Equals("left", StringComparison.OrdinalIgnoreCase)
            ? LeftSideColor
            : RightSideColor;

        var background = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = CreateBrush("#66000000"),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(background, left);
        Canvas.SetTop(background, top);
        ArenaCanvas.Children.Add(background);

        var foreground = new Rectangle
        {
            Width = width * hpRatio,
            Height = height,
            Fill = CreateBrush(barColor),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(foreground, left);
        Canvas.SetTop(foreground, top);
        ArenaCanvas.Children.Add(foreground);
    }

    private static string GetPrimaryColor(string side)
    {
        return side.Equals("left", StringComparison.OrdinalIgnoreCase)
            ? LeftSideColor
            : RightSideColor;
    }

    private static bool IsQzd(FighterState fighter)
    {
        return fighter.Definition.Key.Equals("qzd", StringComparison.OrdinalIgnoreCase);
    }

    private static double GetChargeElapsedTime(FighterState fighter)
    {
        if (fighter.Definition.CD <= 0)
        {
            return 0;
        }

        return Math.Clamp(fighter.Definition.CD - fighter.SkillTimer, 0, fighter.Definition.CD);
    }

    private static bool ShouldRevealQzdN(FighterState fighter)
    {
        return GetChargeElapsedTime(fighter) >= 4;
    }

    private static bool ShouldRevealQzdM(FighterState fighter)
    {
        return GetChargeElapsedTime(fighter) >= 8;
    }

    private static string GetFighterDescriptionText(FighterState fighter)
    {
        var description = fighter.Definition.desc;
        if (!IsQzd(fighter))
        {
            return description;
        }

        var nText = ShouldRevealQzdN(fighter)
            ? fighter.ChargePreviewShotCount.ToString()
            : "?";
        var mText = ShouldRevealQzdM(fighter)
            ? fighter.ChargePreviewDamage.ToString()
            : "?";
        var totalText = ShouldRevealQzdM(fighter)
            ? (fighter.ChargePreviewShotCount * fighter.ChargePreviewDamage).ToString()
            : "?";
        return $"{description}\n{nText} * {mText} = {totalText}";
    }

    private void DrawChargePreview(FighterState fighter)
    {
        if (!IsQzd(fighter))
        {
            return;
        }

        var nText = ShouldRevealQzdN(fighter)
            ? fighter.ChargePreviewShotCount.ToString()
            : "?";
        var mText = ShouldRevealQzdM(fighter)
            ? fighter.ChargePreviewDamage.ToString()
            : "?";
        var totalText = ShouldRevealQzdM(fighter)
            ? (fighter.ChargePreviewShotCount * fighter.ChargePreviewDamage).ToString()
            : "?";
        var text = new TextBlock
        {
            Text = $"{nText} * {mText} = {totalText}",
            Foreground = CreateBrush(GetSecondaryColor(fighter.Side)),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 4,
                ShadowDepth = 1,
                Color = Colors.Black,
                Opacity = 0.65
            }
        };

        text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = text.DesiredSize;
        Canvas.SetLeft(text, fighter.Position.X - (size.Width / 2));
        Canvas.SetTop(text, fighter.Position.Y - fighter.Definition.Radius - 40);
        ArenaCanvas.Children.Add(text);
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

    private static Brush CreateArenaBackgroundBrush()
    {
        return CreateBrush(ArenaBackgroundColor);
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