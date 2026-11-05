using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TableGameRoom;

/// <summary>
/// 座位头顶的双计时气泡 —— 和安卓端完全一致。
///
/// 两个数字并排：
///   左（蓝/红） = 30 秒行动倒计时，表演用，让玩家看到「他还有多少时间做决定」
///   右（灰）    = 3~25 秒真实思考剩余，到点才真正出结果
/// </summary>
public static class SeatTimer
{
    /// <summary>一个座位的双计时气泡：容器 + 两个数字标签。</summary>
    public readonly struct SeatBubble
    {
        public readonly FrameworkElement Bubble;
        public readonly TextBlock TurnText;
        public readonly TextBlock ThinkText;

        public SeatBubble(FrameworkElement bubble, TextBlock turnText, TextBlock thinkText)
        {
            Bubble = bubble;
            TurnText = turnText;
            ThinkText = thinkText;
        }
    }

    /// <summary>行动倒计时总长（秒）。</summary>
    public const int TurnSeconds = 30;

    /// <summary>创建双计时气泡（默认隐藏）。</summary>
    public static FrameworkElement Create(out TextBlock turnLabel, out TextBlock thinkLabel)
    {
        var turn = new TextBlock
        {
            Text = "30",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        turnLabel = turn;

        var turnBox = new Border
        {
            Width = 38,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush(Color.FromRgb(91, 159, 208)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1.4),
            Child = turn
        };

        // 真实思考时间只在内部逻辑里用，不显示给玩家 ——
        // 两个数字并排反而让人看不懂哪个是哪个。
        var think = new TextBlock { Text = "", FontSize = 1, Width = 0, Height = 0 };
        thinkLabel = think;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 3),
            Visibility = Visibility.Collapsed,
            ToolTip = "行动倒计时（30 秒）"
        };
        row.Children.Add(turnBox);

        return row;
    }

    /// <summary>
    /// 兼容旧调用：只要一个数字的座位（扑克 / 骗子扑克）。
    /// 返回的容器里只放左格数字，右格不放。
    /// </summary>
    public static Border Create(out TextBlock label)
    {
        var turn = new TextBlock
        {
            Text = "30",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label = turn;

        return new Border
        {
            Width = 34,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush(Color.FromRgb(91, 159, 208)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1.5),
            Child = turn,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 3),
            Visibility = Visibility.Collapsed,
            ToolTip = "行动倒计时"
        };
    }

    /// <summary>更新两个数字（双计时座位用）。</summary>
    public static void Show(FrameworkElement? bubble, TextBlock? turnLabel, TextBlock? thinkLabel,
        int turnSeconds, int thinkSeconds)
    {
        if (bubble is null) return;

        bubble.Visibility = Visibility.Visible;

        if (turnLabel is not null)
        {
            turnLabel.Text = Math.Max(0, turnSeconds).ToString();

            // 最后 5 秒变红
            if (bubble is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is Border turnBox)
            {
                turnBox.Background = turnSeconds <= 5
                    ? new SolidColorBrush(Color.FromRgb(192, 57, 43))
                    : new SolidColorBrush(Color.FromRgb(91, 159, 208));
            }
        }

        if (thinkLabel is not null)
            thinkLabel.Text = $"{Math.Max(0, thinkSeconds)}s";
    }

    /// <summary>
    /// 只更新左格（行动倒计时）。玩家自己用这个 —— 他没有「真实思考」的概念。
    /// </summary>
    public static void ShowTurnOnly(FrameworkElement? bubble, TextBlock? turnLabel, int turnSeconds)
    {
        if (bubble is null) return;

        bubble.Visibility = Visibility.Visible;

        if (turnLabel is not null)
            turnLabel.Text = Math.Max(0, turnSeconds).ToString();

        if (bubble is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is Border turnBox)
        {
            turnBox.Background = turnSeconds <= 5
                ? new SolidColorBrush(Color.FromRgb(192, 57, 43))
                : new SolidColorBrush(Color.FromRgb(91, 159, 208));
        }
    }

    /// <summary>
    /// 兼容旧调用：只有单个数字的座位（扑克 / 骗子扑克）直接用这个。
    /// </summary>
    public static void Show(Border? bubble, TextBlock? label, int seconds)
    {
        if (bubble is null || label is null) return;

        bubble.Visibility = Visibility.Visible;
        label.Text = Math.Max(0, seconds).ToString();

        bubble.Background = seconds <= 5
            ? new SolidColorBrush(Color.FromRgb(192, 57, 43))
            : new SolidColorBrush(Color.FromRgb(91, 159, 208));
    }

    /// <summary>隐藏气泡。</summary>
    public static void Hide(FrameworkElement? bubble)
    {
        if (bubble is null) return;
        bubble.Visibility = Visibility.Collapsed;
    }
}

/// <summary>
/// 单机对局的行动计时器：驱动 UI 上的双计时气泡。
/// 把「电脑思考时长」按档位 + 对局压力换算成可见的秒数倒计时。
/// </summary>
public sealed class SeatTimerDriver
{
    private readonly DispatcherTimer _timer;
    private DateTime _deadline;
    private bool _running;
    private Action<int>? _onTick;
    private Action? _onElapsed;

    public SeatTimerDriver()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) =>
        {
            if (!_running) return;
            var left = (int)Math.Ceiling((_deadline - DateTime.Now).TotalSeconds);
            _onTick?.Invoke(Math.Max(0, left));
            if (left > 0) return;
            Stop();
            _onElapsed?.Invoke();
        };
    }

    public void Start(int seconds, Action<int> onTick, Action? onElapsed = null)
    {
        Stop();
        _deadline = DateTime.Now.AddSeconds(Math.Max(1, seconds));
        _onTick = onTick;
        _onElapsed = onElapsed;
        _running = true;
        _timer.Start();
        onTick(Math.Max(0, seconds));
    }

    public void Stop()
    {
        _running = false;
        _timer.Stop();
        _onTick = null;
        _onElapsed = null;
    }

    public bool IsRunning => _running;
}
