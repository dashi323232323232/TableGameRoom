using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TableGameRoom;

public partial class MainWindow
{
    /// <summary>电脑思考期间为 true，用于拦截玩家重复点击。</summary>
    private bool _aiThinking;

    /// <summary>
    /// 电脑思考 —— 和安卓端对齐的两段式。
    ///
    /// 每位 AI 同时跑两套时间：
    ///   1. 30 秒行动倒计时（表演用，固定在头顶显示）
    ///   2. 3~25 秒真实思考（到点才真正出结果，压力越大越久）
    ///
    /// 如果传入了座位气泡，这两个数字会逐秒刷新。
    /// </summary>
    /// <param name="seatName">正在行动的座位名；null 表示不显示座位气泡</param>
    /// <param name="stage">对局进度 0..8，越大越靠后</param>
    /// <param name="stake">本局注额，用于衡量压力</param>
    /// <param name="tierMax">档位单次上限</param>
    /// <param name="message">页面顶部的提示文字</param>
    private async Task AiThink(
        int stage, int stake, int? tierMax, string? message = null,
        string? seatName = null, bool pressured = false)
    {
        _aiThinking = true;

        // 真实思考时间：3~25 秒，压力越大越久
        var thinkSeconds = RollThinkSeconds(stage, stake, tierMax, pressured);
        var turnSeconds = SeatTimer.TurnSeconds;

        // 页面顶部提示
        var hint = T(message ?? "电脑思考中…", 14, Accent);
        hint.Opacity = 0.95;
        _page.Children.Insert(Math.Min(_page.Children.Count, 1), hint);

        // 座位头顶的双计时
        FrameworkElement? bubble = null;
        TextBlock? turnLabel = null;
        TextBlock? thinkLabel = null;

        if (seatName is not null && _threeSeatTimers.TryGetValue(seatName, out var t))
        {
            bubble = t.Bubble;
            turnLabel = t.TurnText;
            thinkLabel = t.ThinkText;
            SeatTimer.Show(bubble, turnLabel, thinkLabel, turnSeconds, thinkSeconds);
        }

        try
        {
            // 逐秒推进两个数字。
            //
            // 注意：真实思考只有 3~25 秒，而行动倒计时是 30 秒。
            // 如果两者都按 1 秒 1 秒地减，倒计时永远走不到 0（最多减到 5），
            // 玩家会以为卡住了。所以行动倒计时按比例折算：
            // 思考结束的那一刻，倒计时正好走到 0。
            var remaining = thinkSeconds;
            var turnRemaining = (double)turnSeconds;

            while (remaining > 0)
            {
                await Task.Delay(1000);
                remaining--;

                // 每过 1 秒真实思考，倒计时按 30/thinkSeconds 的比例下降
                turnRemaining -= turnSeconds / (double)Math.Max(1, thinkSeconds);
                if (turnRemaining < 0) turnRemaining = 0;

                if (bubble is not null)
                    SeatTimer.Show(bubble, turnLabel, thinkLabel,
                        (int)Math.Ceiling(turnRemaining), remaining);
            }
        }
        finally
        {
            _aiThinking = false;

            if (bubble is not null) SeatTimer.Hide(bubble);
            try { if (_page.Children.Contains(hint)) _page.Children.Remove(hint); } catch { }
        }
    }

    /// <summary>
    /// 真实思考时间：3~25 秒，压力越大越久。
    /// 压力 = 轮次进度 + 注额相对档位上限的比例 + 是否被加注。
    /// </summary>
    private static int RollThinkSeconds(int stage, int stake, int? tierMax, bool pressured)
    {
        var roundPressure = Math.Clamp(stage / 6.0, 0, 1);

        double stakePressure;
        if (tierMax is > 0)
            stakePressure = Math.Clamp((double)stake / tierMax.Value, 0, 1);
        else
            stakePressure = stake <= 0 ? 0 : Math.Clamp(Math.Log10(stake) / 6.0, 0, 1);

        var pressure = roundPressure * 0.5 + stakePressure * 0.5;
        if (pressured) pressure = Math.Min(1, pressure + 0.2);

        // 3 秒打底，最高 25 秒
        var seconds = 3 + (25 - 3) * pressure;

        // ±25% 随机波动
        seconds *= 0.75 + Random.Shared.NextDouble() * 0.5;

        return (int)Math.Clamp(Math.Round(seconds), 3, 25);
    }

    /// <summary>
    /// 用一个短的「发牌/动作」停顿，避免动作瞬间全部堆在一起。
    /// </summary>
    private static Task AiBeat(int ms = 220) => Task.Delay(ms);
}
