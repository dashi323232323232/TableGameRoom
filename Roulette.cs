using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace TableGameRoom;

public partial class MainWindow
{
    private static readonly int[] WheelOrder =
    [
        0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10, 5, 24, 16, 33, 1,
        20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26
    ];

    private static readonly int[] RedNumbers =
        [1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36];

    private readonly HashSet<string> _rouletteBets = [];
    private readonly List<(int Number, string Color)> _rouletteRecent = [];
    private HashSet<string> _rouletteLastBets = [];
    private double _wheelRotation;
    private bool _spinning;
    private Ellipse? _rouletteBall;
    private Grid? _rouletteOverlay;

    private static bool IsRed(int n) => RedNumbers.Contains(n);

    private static Brush NumberBrush(int n) => n == 0
        ? new SolidColorBrush(Color.FromRgb(59, 125, 114))
        : IsRed(n)
            ? new SolidColorBrush(Color.FromRgb(196, 86, 74))
            : new SolidColorBrush(Color.FromRgb(48, 58, 64));

    private static string ColorName(int n) => n == 0 ? "绿色" : IsRed(n) ? "红色" : "黑色";

    private static string BetLabel(string key) => key switch
    {
        "red" => "红色 1:1",
        "black" => "黑色 1:1",
        "even" => "双数 1:1",
        "odd" => "单数 1:1",
        "low" => "小号 1:1",
        "high" => "大号 1:1",
        _ => $"号码 {key[4..]} 35:1"
    };

    private void RoulettePage()
    {
        Base("欧洲轮盘");
        Header();

        var wheel = BuildWheel();
        // 指针固定在轮盘正上方，尖朝下指向轮盘（真实赌场样式）。
        // 三角形：底边在上（宽），顶点在下（尖），落在轮盘外沿。
        var pointer = new Polygon
        {
            Points = [new Point(6, 2), new Point(34, 2), new Point(20, 30)],
            Fill = new SolidColorBrush(Color.FromRgb(214, 168, 78)),
            Stroke = new SolidColorBrush(Color.FromRgb(120, 88, 36)),
            StrokeThickness = 1.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 0, 0)
        };

        var hub = new Border
        {
            Width = 78,
            Height = 78,
            CornerRadius = new CornerRadius(40),
            Background = Brushes.White,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (_rouletteRecent.Count > 0)
        {
            var last = _rouletteRecent[0];
            hub.Child = new TextBlock
            {
                Text = last.Number.ToString(),
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = NumberBrush(last.Number),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        var overlay = new Grid { Width = 360, Height = 360 };
        overlay.Children.Add(wheel);
        overlay.Children.Add(hub);
        overlay.Children.Add(pointer);
        // 小球层（在轮盘之上，指针之下）
        _rouletteBall = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = new RadialGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(255, 255, 255), 0),
                    new GradientStop(Color.FromRgb(232, 232, 232), 0.55),
                    new GradientStop(Color.FromRgb(150, 150, 150), 1)
                })
            {
                GradientOrigin = new Point(0.32, 0.28),
                Center = new Point(0.4, 0.35),
                RadiusX = 0.75,
                RadiusY = 0.75
            },
            Stroke = new SolidColorBrush(Color.FromRgb(110, 110, 110)),
            StrokeThickness = 0.6,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        overlay.Children.Add(_rouletteBall);
        _rouletteOverlay = overlay;

        var holder = new Border
        {
            Background = TableBrush,
            BorderBrush = TableBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = overlay
        };
        _page.Children.Add(holder);

        _page.Children.Add(T("投注方式（可多选，点击切换）：", 15));

        var bets = new WrapPanel();
        foreach (var key in new[] { "red", "black", "even", "odd", "low", "high" })
        {
            var betKey = key;
            var label = (_rouletteBets.Contains(betKey) ? "✓ " : "") + BetLabel(betKey);
            var button = B(label, () =>
            {
                if (_spinning) return;
                if (!_rouletteBets.Add(betKey)) _rouletteBets.Remove(betKey);
                RoulettePage();
            });
            if (_rouletteBets.Contains(betKey))
            {
                button.Background = new SolidColorBrush(Color.FromRgb(220, 235, 247));
                button.BorderBrush = Accent;
            }
            bets.Children.Add(button);
        }
        _page.Children.Add(bets);

        _page.Children.Add(T("具体号码（35:1）：", 15));
        var numbers = new UniformGrid { Columns = 10, Width = 620, HorizontalAlignment = HorizontalAlignment.Left };
        for (var n = 0; n <= 36; n++)
        {
            var number = n;
            var key = $"num:{number}";
            var button = B(number.ToString(), () =>
            {
                if (_spinning) return;
                if (!_rouletteBets.Add(key)) _rouletteBets.Remove(key);
                RoulettePage();
            });
            button.Padding = new Thickness(2);
            button.Margin = new Thickness(2);
            if (_rouletteBets.Contains(key))
            {
                button.Background = NumberBrush(number);
                button.Foreground = Brushes.White;
                button.BorderBrush = Ink;
            }
            numbers.Children.Add(button);
        }
        _page.Children.Add(numbers);

        var selection = _rouletteBets.Count == 0
            ? "当前投注：未选择"
            : $"当前投注：{string.Join("、", _rouletteBets.Select(BetLabel))} · 共 {_rouletteBets.Count} 项";
        _page.Children.Add(T(selection, 15));

        var controls = new WrapPanel();
        controls.Children.Add(B("清空投注", () =>
        {
            if (_spinning) return;
            _rouletteBets.Clear();
            RoulettePage();
        }));
        controls.Children.Add(B("撤销最后一项", () =>
        {
            if (_spinning) return;
            var last = _rouletteBets.LastOrDefault();
            if (last is not null) _rouletteBets.Remove(last);
            RoulettePage();
        }));
        controls.Children.Add(B("重复上局投注", () =>
        {
            if (_spinning) return;
            if (_rouletteLastBets.Count == 0)
            {
                Msg("还没有上一局投注记录。");
                return;
            }
            _rouletteBets.Clear();
            foreach (var key in _rouletteLastBets) _rouletteBets.Add(key);
            RoulettePage();
        }));
        _page.Children.Add(controls);

        var stakeInput = StakeBox();

        var spin = B("旋转轮盘", () =>
        {
            if (_spinning) return;
            if (_rouletteBets.Count == 0)
            {
                Msg("请先选择至少一种投注方式。");
                return;
            }
            if (!int.TryParse(stakeInput.Text.Trim(), out var stake) || stake <= 0)
            {
                Msg("请输入有效的下注金额。");
                return;
            }
            var total = stake * _rouletteBets.Count;
            if (!TryStake(total, out var error))
            {
                Msg(error);
                return;
            }
            SpinWheel(stake, total);
        });
        _page.Children.Add(spin);

        if (_rouletteRecent.Count > 0)
        {
            _page.Children.Add(T("最近结果：" + string.Join(" · ", _rouletteRecent.Take(12).Select(r => $"{ColorName(r.Number)} {r.Number}")), 15));
        }
    }

    private void SpinWheel(int stake, int total)
    {
        var generation = ++_rouletteGeneration;
        var bets = _rouletteBets.ToArray();
        _spinning = true;
        _roundSettled = false;
        _rouletteCommitted = total;
        if (_mode == "存档") _balance = Math.Max(0, _balance - total);
        _rouletteLastBets = [.. bets];

        var index = _rng.Next(37);
        var number = WheelOrder[index];
        var step = 360.0 / 37;
        var jitter = (_rng.NextDouble() - 0.5) * step * 0.6;
        var target = -(index * step + jitter);
        var current = _wheelRotation % 360;
        var delta = ((target - current) % 360 + 360) % 360;
        var final = _wheelRotation + 360 * 8 + delta;

        if (Root.Content is not ScrollViewer viewer)
        {
            FinishRoulette(generation, stake, total, number, bets);
            return;
        }
        var wheel = FindWheel(viewer);
        if (wheel?.RenderTransform is not RotateTransform rotate) { FinishRoulette(generation, stake, total, number, bets); return; }

        // 轮盘旋转（3 秒）
        var animation = new DoubleAnimation
        {
            From = _wheelRotation,
            To = final,
            Duration = TimeSpan.FromSeconds(3),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        animation.Completed += (_, _) =>
        {
            if (generation != _rouletteGeneration || !_spinning || _roundSettled) return;
            _wheelRotation = final;
            rotate.Angle = final;
        };
        rotate.BeginAnimation(RotateTransform.AngleProperty, animation);

        // 小球：绕圈 → 减速 → 螺旋落入目标格
        StartBallAnimation(generation, index, final, stake, total, number, bets);
    }

    /// <summary>
    /// 小球动画：从外圈开始高速绕行，逐渐减速并向内螺旋，
    /// 最后落到目标数字格上。与轮盘旋转同步（约 4.2 秒）。
    /// </summary>
    private void StartBallAnimation(long generation, int targetIndex, double wheelFinalAngle,
        int stake, int total, int number, IReadOnlyList<string> bets)
    {
        if (_rouletteBall is null || _rouletteOverlay is null) return;

        const double size = 360;
        const double cx = size / 2;
        const double cy = size / 2;
        const double ballSize = 14;

        // 球道半径：从最外（球道）螺旋到色块环中部
        const double startRadius = 150;   // 球道外沿
        const double endRadius = 82;      // 色块环中部（球最终落点）
        var startAngle = _rng.NextDouble() * 360;

        _rouletteBall.Visibility = Visibility.Visible;

        var duration = TimeSpan.FromSeconds(4.2);
        var startTime = DateTime.Now;

        // 用 CompositionTarget.Rendering 逐帧计算，做真正的螺旋轨迹
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (generation != _rouletteGeneration)
            {
                CompositionTarget.Rendering -= handler;
                if (_rouletteBall is not null) _rouletteBall.Visibility = Visibility.Collapsed;
                return;
            }

            var t = Math.Clamp((DateTime.Now - startTime).TotalSeconds / duration.TotalSeconds, 0, 1);

            // 缓动：先快后慢（三次方减速）
            var ease = 1 - Math.Pow(1 - t, 3);

            // 角度：先转很多圈，最后落到目标格
            // 目标格在轮盘自身坐标系里的角度 = targetIndex * step
            // 轮盘整体旋转了 wheelFinalAngle，所以屏幕上的绝对角度要加上它
            var targetSlotAngle = targetIndex * (360.0 / 37) + wheelFinalAngle;
            var totalSpin = 360 * 9; // 球自己转 9 圈
            // 让球最终角度 = targetSlotAngle（对 360 取模后对齐）
            var startNorm = startAngle % 360;
            var targetNorm = ((targetSlotAngle % 360) + 360) % 360;
            var alignDelta = ((targetNorm - startNorm) % 360 + 360) % 360;
            var angle = startAngle + totalSpin * ease + alignDelta * Math.Pow(ease, 3);

            // 半径：从球道逐渐螺旋向内
            var radius = startRadius - (startRadius - endRadius) * Math.Pow(ease, 2.2);

            // 小球抖动（模拟滚动）
            var wobble = (1 - t) * Math.Sin(t * Math.PI * 30) * 1.8;
            var rad = (angle - 90) * Math.PI / 180;
            var x = cx + (radius + wobble) * Math.Cos(rad) - ballSize / 2;
            var y = cy + (radius + wobble) * Math.Sin(rad) - ballSize / 2;

            Canvas.SetLeft(_rouletteBall, x);
            Canvas.SetTop(_rouletteBall, y);

            if (t >= 1)
            {
                CompositionTarget.Rendering -= handler;
                // 球停在最终格
                var finalRad = (targetSlotAngle - 90) * Math.PI / 180;
                Canvas.SetLeft(_rouletteBall, cx + endRadius * Math.Cos(finalRad) - ballSize / 2);
                Canvas.SetTop(_rouletteBall, cy + endRadius * Math.Sin(finalRad) - ballSize / 2);

                if (generation != _rouletteGeneration || !_spinning || _roundSettled) return;
                FinishRoulette(generation, stake, total, number, bets);
            }
        };

        CompositionTarget.Rendering += handler;
    }

    private static Canvas? FindWheel(DependencyObject root)
    {
        if (root is Canvas canvas && canvas.Children.Count > 30) return canvas;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindWheel(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }
        return null;
    }

    private void FinishRoulette(long generation, int stake, int total, int number, IReadOnlyList<string> bets)
    {
        if (generation != _rouletteGeneration || !_spinning || _roundSettled) return;
        _spinning = false;
        var hits = new List<string>();
        var returned = 0;
        foreach (var key in bets)
        {
            var hit = key switch
            {
                "red" => number != 0 && IsRed(number),
                "black" => number != 0 && !IsRed(number),
                "even" => number != 0 && number % 2 == 0,
                "odd" => number != 0 && number % 2 == 1,
                "low" => number is >= 1 and <= 18,
                "high" => number >= 19,
                _ => int.TryParse(key[4..], out var n) && n == number
            };
            if (!hit) continue;
            hits.Add(BetLabel(key));
            returned += key.StartsWith("num:") ? stake * 36 : stake * 2;
        }

        _rouletteRecent.Insert(0, (number, ColorName(number)));
        if (_rouletteRecent.Count > 12) _rouletteRecent.RemoveRange(12, _rouletteRecent.Count - 12);

        var summary = $"{ColorName(number)} {number}；选择 {string.Join("、", bets.Select(BetLabel))}；命中 {hits.Count}/{bets.Count} 项" +
                      (hits.Count > 0 ? $"（{string.Join("、", hits)}）" : "") +
                      $"；返还 ${returned:N0}";
        if (_roundSettled || generation != _rouletteGeneration) return;
        if (_mode == "存档") _balance = Math.Max(0, _balance + total);
        _rouletteCommitted = 0;
        Settle(total, returned - total, summary);
        Header();
        RoulettePage();
    }

    private Canvas BuildWheel()
    {
        const double size = 360;
        const double cx = size / 2;
        const double cy = size / 2;
        const double step = 360.0 / 37;

        // 真实轮盘的同心圆分层（半径从内到外）：
        //   轮毂中心 0-52 | 色块环 52-104 | 数字环 104-138 | 球道 138-160 | 碗边 160-180
        const double hubRadius = 52;
        const double pocketOuter = 104;   // 色块环外沿
        const double numberOuter = 138;   // 数字环外沿
        const double trackOuter = 160;    // 球道外沿

        var canvas = new Canvas
        {
            Width = size,
            Height = size,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(_wheelRotation)
        };

        // ---------- 1) 最外圈碗边（静止的木质外框）----------
        var rim = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(Color.FromRgb(110, 82, 48)),
            Stroke = new SolidColorBrush(Color.FromRgb(196, 164, 110)),
            StrokeThickness = 2
        };
        canvas.Children.Add(rim);

        // ---------- 2) 球道（球在这里滚动，深色凹槽）----------
        var track = new Ellipse
        {
            Width = trackOuter * 2,
            Height = trackOuter * 2,
            Fill = new SolidColorBrush(Color.FromRgb(74, 56, 34)),
            Stroke = new SolidColorBrush(Color.FromRgb(150, 120, 78)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(track, cx - trackOuter);
        Canvas.SetTop(track, cy - trackOuter);
        canvas.Children.Add(track);

        // ---------- 3) 数字环（木色/米色带，数字印在上面）----------
        var numberRing = new Ellipse
        {
            Width = numberOuter * 2,
            Height = numberOuter * 2,
            Fill = new SolidColorBrush(Color.FromRgb(214, 196, 160)), // 米色
            Stroke = new SolidColorBrush(Color.FromRgb(150, 120, 78)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(numberRing, cx - numberOuter);
        Canvas.SetTop(numberRing, cy - numberOuter);
        canvas.Children.Add(numberRing);

        // ---------- 4) 色块环 + 分隔条 + 数字 ----------
        for (var i = 0; i < 37; i++)
        {
            var number = WheelOrder[i];
            var start = i * step - step / 2;
            var end = i * step + step / 2;

            // 4a) 色块（环形扇区：从 hubRadius 到 pocketOuter）
            var pocket = RingSector(cx, cy, hubRadius, pocketOuter, start, end);
            canvas.Children.Add(new Path
            {
                Data = pocket,
                Fill = NumberBrush(number),
                Stroke = new SolidColorBrush(Color.FromRgb(238, 232, 220)),
                StrokeThickness = 0.8
            });

            // 4b) 数字（印在数字环正中）
            // 让数字始终「头朝外、正立可读」：旋转角度超过 90° 时翻转 180°，
            // 这样下半圈的数字不会倒过来。
            var mid = (start + end) / 2;
            var labelRadius = (pocketOuter + numberOuter) / 2;
            var pos = PointAt(cx, cy, labelRadius, mid);
            var rotation = mid;
            // 归一化到 -180..180
            var norm = ((rotation % 360) + 360) % 360;
            if (norm > 90 && norm < 270) rotation += 180; // 下半圈翻转，保持可读
            var label = new TextBlock
            {
                Text = number.ToString(),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(46, 38, 28)),
                FontWeight = FontWeights.Bold,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(rotation)
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, pos.X - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, pos.Y - label.DesiredSize.Height / 2);
            canvas.Children.Add(label);

            // 4c) 数字环上的分隔刻线
            var lineStart = PointAt(cx, cy, pocketOuter, start);
            var lineEnd = PointAt(cx, cy, numberOuter, start);
            canvas.Children.Add(new Line
            {
                X1 = lineStart.X, Y1 = lineStart.Y,
                X2 = lineEnd.X, Y2 = lineEnd.Y,
                Stroke = new SolidColorBrush(Color.FromRgb(160, 138, 104)),
                StrokeThickness = 0.7
            });
        }

        // ---------- 5) 中央轮毂 ----------
        var hubOuter = new Ellipse
        {
            Width = hubRadius * 2,
            Height = hubRadius * 2,
            Fill = new SolidColorBrush(Color.FromRgb(52, 68, 52)),
            Stroke = new SolidColorBrush(Color.FromRgb(160, 130, 85)),
            StrokeThickness = 3
        };
        Canvas.SetLeft(hubOuter, cx - hubRadius);
        Canvas.SetTop(hubOuter, cy - hubRadius);
        canvas.Children.Add(hubOuter);

        var hubCircle = new Ellipse
        {
            Width = 78,
            Height = 78,
            Fill = Brushes.White,
            Stroke = PanelBorder,
            StrokeThickness = 2
        };
        Canvas.SetLeft(hubCircle, cx - 39);
        Canvas.SetTop(hubCircle, cy - 39);
        canvas.Children.Add(hubCircle);

        return canvas;
    }

    /// <summary>构造一个环形扇区（内半径 inner 到外半径 outer，角度 start→end）。</summary>
    private static Geometry RingSector(double cx, double cy, double inner, double outer, double start, double end)
    {
        var figure = new PathFigure
        {
            StartPoint = PointAt(cx, cy, inner, start),
            IsClosed = true
        };
        figure.Segments.Add(new LineSegment(PointAt(cx, cy, outer, start), true));
        figure.Segments.Add(new ArcSegment(PointAt(cx, cy, outer, end),
            new Size(outer, outer), 0, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(PointAt(cx, cy, inner, end), true));
        figure.Segments.Add(new ArcSegment(PointAt(cx, cy, inner, start),
            new Size(inner, inner), 0, false, SweepDirection.Counterclockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static Point PointAt(double cx, double cy, double radius, double degrees)
    {
        var rad = (degrees - 90) * Math.PI / 180;
        return new Point(cx + radius * Math.Cos(rad), cy + radius * Math.Sin(rad));
    }
}
