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
    /// 显示「电脑思考中」状态并等待随机时长。
    /// 仅用于电脑行动；玩家操作永远即时响应。
    /// </summary>
    private async Task AiThink(int stage, int stake, int? tierMax, string? message = null)
    {
        _aiThinking = true;
        var delay = ThinkTiming.Delay(stage, stake, tierMax);

        // 在页面顶部插入一行思考提示（若页面已重建则不插入）
        var hint = T(message ?? "电脑思考中…", 14, Accent);
        hint.Opacity = 0.95;
        _page.Children.Insert(Math.Min(_page.Children.Count, 1), hint);

        try
        {
            await Task.Delay(delay);
        }
        finally
        {
            _aiThinking = false;
            // 页面可能在等待期间被重建，安全移除
            try { if (_page.Children.Contains(hint)) _page.Children.Remove(hint); } catch { }
        }
    }

    /// <summary>
    /// 用一个短的「发牌/动作」停顿，避免动作瞬间全部堆在一起。
    /// </summary>
    private static Task AiBeat(int ms = 220) => Task.Delay(ms);
}
