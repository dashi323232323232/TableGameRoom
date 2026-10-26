using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TableGameRoom.Net;

namespace TableGameRoom;

/// <summary>联机炸金花：每位参战玩家三张牌，轮流跟注/加注/弃牌，最后比牌。</summary>
public sealed class LanThreePlayer
{
    public string PlayerId { get; set; } = "";
    public List<Card> Cards { get; set; } = [];
    public int Committed { get; set; }
    public bool Folded { get; set; }
    public bool Seen { get; set; }
    public string Result { get; set; } = "";
    /// <summary>true = AI 补位玩家（主机自动行动）。</summary>
    public bool IsAi { get; set; }
}

public sealed class LanThreeState
{
    public string RoomId { get; set; } = "";
    /// <summary>betting / showdown</summary>
    public string Phase { get; set; } = "waiting";
    public List<LanThreePlayer> Players { get; set; } = [];
    public int Pot { get; set; }
    public int Stake { get; set; } = 100;
    public string CurrentPlayerId { get; set; } = "";
    public string Winner { get; set; } = "";
    public string Log { get; set; } = "";
}

public partial class MainWindow
{
    private LanThreeState? _lanThree;

    private void ShowLanThree(RoomInfo room)
    {
        _currentGame = "lan-game";
        Base("炸金花 · 联机对局");
        Header();

        var me = room.Members.FirstOrDefault(m => m.PlayerId == _myNetId);
        var isHost = room.HostId == _myNetId;
        var playing = me?.Playing == true;
        var st = _lanThree;

        _page.Children.Add(T($"房间：{room.Name} · 参战 {room.Members.Count(m => m.Playing)} 人", 15, Accent));

        if (!playing)
            _page.Children.Add(T("👁 观战中：看不到任何手牌。", 15, Accent));

        if (st is null || st.Phase == "waiting")
        {
            _page.Children.Add(T("炸金花联机版：每人三张牌，比牌型大小。", 14, Muted));
            if (isHost)
                _page.Children.Add(B("开始新局（房主）", () => SendOrLocal(new NetMessage { Type = "three_start", RoomId = room.Id })));
            _page.Children.Add(B("返回房间", () => { _currentGame = "lan-room"; ShowRoomPage(); }));
            return;
        }

        var threeRemain = TurnRemainingSeconds(room.Id);
        var threeTimer = threeRemain >= 0 ? $" · ⏱ {threeRemain}s" : "";
        _page.Children.Add(T($"底池：${st.Pot:N0} · 当前注：${st.Stake:N0}{threeTimer}", 15));
        if (st.Log.Length > 0) _page.Children.Add(T(st.Log, 14, Accent));
        if (st.Winner.Length > 0) _page.Children.Add(T($"🏆 {st.Winner}", 18));

        // 我的手牌
        var mine = st.Players.FirstOrDefault(p => p.PlayerId == _myNetId);
        if (playing && mine is not null)
        {
            _page.Children.Add(T($"我的手牌（{ThreeScore.Name(ThreeScore.Of(mine.Cards))}）", 16));
            var row = new WrapPanel();
            foreach (var c in mine.Cards) row.Children.Add(CardView.Create(c));
            _page.Children.Add(row);
        }

        // 所有玩家状态
        _page.Children.Add(T("玩家状态", 16));
        foreach (var p in st.Players)
        {
            var name = DisplayName(p.PlayerId);
            var isMe = p.PlayerId == _myNetId;
            var cardsText = p.Folded ? "已弃牌"
                : isMe ? $"{ThreeScore.Name(ThreeScore.Of(p.Cards))} · 已下注 ${p.Committed:N0}"
                : p.Result.Length > 0 ? $"{p.Result} · 已下注 ${p.Committed:N0}"
                : $"已下注 ${p.Committed:N0}（牌面隐藏）";
            _page.Children.Add(T($"{name}{(isMe ? "（我）" : "")}：{cardsText}", 14, isMe ? Accent : Muted));
        }

        // 操作
        if (playing && st.Phase == "betting" && st.CurrentPlayerId == _myNetId)
        {
            var row = new WrapPanel();
            row.Children.Add(B("看牌", () => SendOrLocal(new NetMessage { Type = "three_see", RoomId = room.Id })));
            row.Children.Add(B($"跟注 ${st.Stake:N0}", () => SendOrLocal(new NetMessage { Type = "three_call", RoomId = room.Id })));
            row.Children.Add(B($"加注 ${st.Stake * 2:N0}", () => SendOrLocal(new NetMessage { Type = "three_raise", RoomId = room.Id })));
            row.Children.Add(B("弃牌", () => SendOrLocal(new NetMessage { Type = "three_fold", RoomId = room.Id })));
            row.Children.Add(B("比牌", () => SendOrLocal(new NetMessage { Type = "three_showdown", RoomId = room.Id })));
            _page.Children.Add(row);
        }
        else if (playing && st.Phase == "betting")
        {
            var waitName = DisplayName(st.CurrentPlayerId);
            _page.Children.Add(T($"等待 {waitName} 行动…", 14, Muted));
        }

        if (st.Phase == "showdown" && isHost)
            _page.Children.Add(B("再来一局", () => SendOrLocal(new NetMessage { Type = "three_start", RoomId = room.Id })));

        _page.Children.Add(B("返回房间", () => { _currentGame = "lan-room"; ShowRoomPage(); }));
    }

    // ==================== 主机端：炸金花逻辑 ====================

    private void LanThreeStart(string playerId, string roomId)
    {
        if (_lanServer is null) return;
        var room = _lanServer.State.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null || room.HostId != playerId) return;

        var playingIds = room.Members.Where(m => m.Playing).Select(m => m.PlayerId).ToList();

        // AI 补位
        var aiNeeded = Math.Max(0, room.Seats - playingIds.Count);
        for (var i = 0; i < aiNeeded; i++) playingIds.Add($"ai-{i + 1}");

        if (playingIds.Count < 2) return;

        var deck = Deck.New(_rng);
        var st = new LanThreeState
        {
            RoomId = roomId,
            Phase = "betting",
            Stake = 100,
            Pot = 0
        };

        foreach (var pid in playingIds)
        {
            var p = new LanThreePlayer { PlayerId = pid, Cards = deck.Take(3).ToList(), IsAi = pid.StartsWith("ai-") };
            deck.RemoveRange(0, 3);
            var member = room.Members.FirstOrDefault(m => m.PlayerId == pid);
            var ante = member?.Unlimited == true ? 100 : Math.Min(100, member?.Chips ?? 100);
            member?.Pay(ante);
            p.Committed = ante;
            st.Pot += ante;
            st.Players.Add(p);
        }

        st.CurrentPlayerId = playingIds[0];
        st.Log = "新局开始，请下注。";
        _lanThree = st;
        BroadcastThree(roomId);
    }

    private void LanThreeAction(string playerId, string roomId, string action)
    {
        var st = _lanThree;
        if (st is null || st.Phase != "betting") return;
        var p = st.Players.FirstOrDefault(x => x.PlayerId == playerId);
        if (p is null || p.Folded) return;

        if (action == "see")
        {
            p.Seen = true;
            st.Log = $"{NameOf(playerId)} 看了牌。";
            BroadcastThree(roomId);
            return;
        }

        if (st.CurrentPlayerId != playerId) return;

        switch (action)
        {
            case "call":
            {
                var m = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == roomId)?.Members.FirstOrDefault(x => x.PlayerId == playerId);
                var pay = Math.Min(st.Stake, m?.Chips ?? st.Stake);
                m?.Pay(pay);
                p.Committed += pay;
                st.Pot += pay;
                st.Log = $"{NameOf(playerId)} 跟注 ${pay:N0}。";
                break;
            }
            case "raise":
            {
                st.Stake *= 2;
                var m = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == roomId)?.Members.FirstOrDefault(x => x.PlayerId == playerId);
                var pay = Math.Min(st.Stake, m?.Chips ?? st.Stake);
                m?.Pay(pay);
                p.Committed += pay;
                st.Pot += pay;
                st.Log = $"{NameOf(playerId)} 加注到 ${st.Stake:N0}。";
                break;
            }
            case "fold":
                p.Folded = true;
                st.Log = $"{NameOf(playerId)} 弃牌。";
                break;
            case "showdown":
                SettleThree(st);
                BroadcastThree(roomId);
                return;
        }

        AdvanceThree(st, playerId);
        BroadcastThree(roomId);
    }

    private void AdvanceThree(LanThreeState st, string current)
    {
        var alive = st.Players.Where(p => !p.Folded).ToList();
        if (alive.Count <= 1)
        {
            SettleThree(st);
            return;
        }

        var idx = st.Players.IndexOf(st.Players.First(p => p.PlayerId == current));
        for (var i = 1; i <= st.Players.Count; i++)
        {
            var next = st.Players[(idx + i) % st.Players.Count];
            if (!next.Folded)
            {
                st.CurrentPlayerId = next.PlayerId;

                // AI 补位：自动行动（牌力弱则弃牌，否则跟注/偶尔加注）
                if (next.IsAi)
                {
                    var cat = (int)(ThreeScore.Of(next.Cards) / 1_000_000L);
                    var rnd = _rng.NextDouble();
                    if (cat == 0 && rnd < 0.45) next.Folded = true;
                    else if (rnd > 0.88) { st.Stake *= 2; next.Committed += st.Stake; st.Pot += st.Stake; }
                    else { next.Committed += st.Stake; st.Pot += st.Stake; }
                    AdvanceThree(st, next.PlayerId);
                }
                return;
            }
        }
    }

    private void SettleThree(LanThreeState st)
    {
        var alive = st.Players.Where(p => !p.Folded).ToList();
        if (alive.Count == 0) { st.Phase = "showdown"; st.Winner = "无人获胜"; return; }

        LanThreePlayer best = alive[0];
        foreach (var p in alive.Skip(1))
        {
            if (ThreeScore.Of(p.Cards) > ThreeScore.Of(best.Cards)) best = p;
        }

        st.Phase = "showdown";
        st.Winner = $"{NameOf(best.PlayerId)} 获胜，赢得底池 ${st.Pot:N0}";

        // 统一筹码：赢家拿走底池
        var winnerMember = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == st.RoomId)
            ?.Members.FirstOrDefault(m => m.PlayerId == best.PlayerId);
        winnerMember?.Win(st.Pot);

        foreach (var p in st.Players)
        {
            p.Result = p.Folded ? "弃牌" : ThreeScore.Name(ThreeScore.Of(p.Cards));
        }
    }

    private void BroadcastThree(string roomId)
    {
        if (_lanServer is null || _lanThree is null) return;

        var data = new Dictionary<string, string>
        {
            ["phase"] = _lanThree.Phase,
            ["current"] = _lanThree.CurrentPlayerId,
            ["pot"] = _lanThree.Pot.ToString(),
            ["stake"] = _lanThree.Stake.ToString(),
            ["winner"] = _lanThree.Winner,
            ["log"] = _lanThree.Log
        };

        foreach (var p in _lanThree.Players)
        {
            data[$"p.{p.PlayerId}.committed"] = p.Committed.ToString();
            data[$"p.{p.PlayerId}.folded"] = p.Folded ? "1" : "0";
            data[$"p.{p.PlayerId}.seen"] = p.Seen ? "1" : "0";
            data[$"p.{p.PlayerId}.result"] = p.Result;
        }

        // 每人只收到自己的牌（未弃牌时）
        foreach (var p in _lanThree.Players)
        {
            var mine = new Dictionary<string, string>(data)
            {
                ["cards"] = string.Join(",", p.Cards.Select(c => $"{c.Rank}{c.Suit}"))
            };
            _lanServer.SendTo(p.PlayerId, new NetMessage { Type = "three_state", RoomId = roomId, Data = mine });
        }

        // 观战者：无牌
        foreach (var m in _lanServer.State.Rooms.FirstOrDefault(r => r.Id == roomId)?.Members ?? [])
        {
            if (_lanThree.Players.Any(p => p.PlayerId == m.PlayerId)) continue;
            var spec = new Dictionary<string, string>(data);
            foreach (var p in _lanThree.Players)
                spec[$"p.{p.PlayerId}.result"] = p.Folded ? "弃牌" : "（观战不可见）";
            _lanServer.SendTo(m.PlayerId, new NetMessage { Type = "three_state", RoomId = roomId, Data = spec });
        }

        var secs = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == roomId)?.TurnSeconds ?? 30;
        SyncTurnTimer(roomId, _lanThree?.Phase == "betting" ? _lanThree.CurrentPlayerId : "", secs);
        _session = _lanServer?.Snapshot();
        ShowLanGame();
    }

    private void ApplyThreeState(string roomId, Dictionary<string, string> data)
    {
        var st = new LanThreeState { RoomId = roomId };
        st.Phase = data.GetValueOrDefault("phase") ?? "waiting";
        st.CurrentPlayerId = data.GetValueOrDefault("current") ?? "";
        st.Winner = data.GetValueOrDefault("winner") ?? "";
        st.Log = data.GetValueOrDefault("log") ?? "";
        int.TryParse(data.GetValueOrDefault("pot"), out var pot); st.Pot = pot;
        int.TryParse(data.GetValueOrDefault("stake"), out var stake); st.Stake = stake;

        var myCards = ParseCards(data.GetValueOrDefault("cards"));
        if (myCards.Count > 0)
            st.Players.Add(new LanThreePlayer { PlayerId = _myNetId, Cards = myCards });

        foreach (var kv in data.Where(k => k.Key.StartsWith("p.")))
        {
            var parts = kv.Key.Split('.');
            if (parts.Length != 3) continue;
            var pid = parts[1];
            var p = st.Players.FirstOrDefault(x => x.PlayerId == pid);
            if (p is null) { p = new LanThreePlayer { PlayerId = pid }; st.Players.Add(p); }
            switch (parts[2])
            {
                case "committed": int.TryParse(kv.Value, out var c); p.Committed = c; break;
                case "folded": p.Folded = kv.Value == "1"; break;
                case "seen": p.Seen = kv.Value == "1"; break;
                case "result": p.Result = kv.Value; break;
            }
        }

        _lanThree = st;
        if (_currentGame == "lan-game") ShowLanGame();
    }
}
