using System.Collections.Concurrent;
using System.Windows;
using TableGameRoom.Net;

namespace TableGameRoom;

/// <summary>
/// 联机行动倒计时：主机为每个房间维护一个计时器，
/// 超时自动让当前玩家弃牌（轮盘/21点/炸金花/德州/骗子通用）。
/// </summary>
public partial class MainWindow
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _turnTimers = new();
    private readonly ConcurrentDictionary<string, DateTime> _turnDeadlines = new();

    /// <summary>当前玩家剩余秒数（-1 表示没有倒计时）。</summary>
    private int TurnRemainingSeconds(string roomId)
    {
        if (!_turnDeadlines.TryGetValue(roomId, out var deadline)) return -1;
        var left = (int)Math.Ceiling((deadline - DateTime.UtcNow).TotalSeconds);
        return Math.Max(0, left);
    }

    /// <summary>重置某房间的行动倒计时。</summary>
    private void ResetTurnTimer(string roomId, string playerId, int seconds)
    {
        CancelTurnTimer(roomId);
        if (seconds <= 0) return;

        _turnDeadlines[roomId] = DateTime.UtcNow.AddSeconds(seconds);
        var cts = new CancellationTokenSource();
        _turnTimers[roomId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), cts.Token);
                if (cts.IsCancellationRequested) return;
                // 超时：自动弃牌
                Application.Current.Dispatcher.Invoke(() => OnTurnTimeout(roomId, playerId));
            }
            catch (OperationCanceledException) { }
            catch { }
        }, cts.Token);
    }

    private void CancelTurnTimer(string roomId)
    {
        if (_turnTimers.TryRemove(roomId, out var cts))
        {
            try { cts.Cancel(); } catch { }
            cts.Dispose();
        }
        _turnDeadlines.TryRemove(roomId, out _);
    }

    /// <summary>超时处理：按游戏类型让当前玩家自动弃牌。</summary>
    private void OnTurnTimeout(string roomId, string playerId)
    {
        if (_lanServer is null) return;
        var room = _lanServer.State.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null) return;

        switch (room.GameKey)
        {
            case "roulette":
                // 轮盘无轮流行动，超时只是提示
                break;
            case "blackjack":
                if (_lanBj is not null && _lanBj.CurrentPlayerId == playerId)
                    LanBjStand(playerId, roomId);
                break;
            case "three":
                if (_lanThree is not null && _lanThree.CurrentPlayerId == playerId)
                    LanThreeAction(playerId, roomId, "fold");
                break;
            case "poker":
                if (_lanPoker is not null && _lanPoker.CurrentPlayerId == playerId)
                    LanPokerAction(playerId, roomId, "fold");
                break;
            case "liar":
                if (_lanLiar is not null && _lanLiar.CurrentPlayerId == playerId && _lanLiar.AwaitingDecision)
                    LanLiarPass(playerId, roomId);
                break;
        }
    }

    /// <summary>广播后统一同步计时器（各游戏广播时调用）。</summary>
    private void SyncTurnTimer(string roomId, string currentPlayerId, int seconds)
    {
        if (_lanServer is null) return;
        if (string.IsNullOrEmpty(currentPlayerId) || seconds <= 0)
        {
            CancelTurnTimer(roomId);
            return;
        }
        ResetTurnTimer(roomId, currentPlayerId, seconds);
    }
}
