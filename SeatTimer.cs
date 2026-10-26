using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TableGameRoom;

/// <summary>
/// 座位头顶倒计时气泡：轮到谁行动，谁头上就显示 30 秒倒数。
/// 单机版用它显示「电脑正在思考的剩余时间」和「玩家自己的行动时限」。
/// </summary>
public static class SeatTimer
{
    public const int TurnSeconds = 30;

    /// <summary>创建一个倒计时气泡（默认隐藏，调用 Show 才显示）。</summary>
    public static Border Create(out TextBlock label)
    {
        var text = new TextBlock
        {
            Text = "30",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label = text;

        var bubble = new Border
        {
            Width = 34,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush(Color.FromRgb(91, 159, 208)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1.5),
            Child = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 3),
            Visibility = Visibility.Collapsed,
            ToolTip = "行动倒计时"
        };
        return bubble;
    }

    /// <summary>显示气泡并设置剩余秒数。</summary>
    public static void Show(Border? bubble, TextBlock? label, int seconds)
    {
        if (bubble is null || label is null) return;
        bubble.Visibility = Visibility.Visible;
        label.Text = Math.Max(0, seconds).ToString();
        // 最后 10 秒变红警示
        bubble.Background = seconds <= 10
            ? new SolidColorBrush(Color.FromRgb(192, 57, 43))
            : new SolidColorBrush(Color.FromRgb(91, 159, 208));
    }

    /// <summary>隐藏气泡。</summary>
    public static void Hide(Border? bubble)
    {
        if (bubble is null) return;
        bubble.Visibility = Visibility.Collapsed;
    }
}

/// <summary>
/// 单机对局的行动计时器：驱动 UI 上的倒计时气泡，
/// 并把「电脑思考时长」按档位 + 对局压力换算成可见的秒数倒计时。
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
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
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
