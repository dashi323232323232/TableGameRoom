using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TableGameRoom.Net;

namespace TableGameRoom;

/// <summary>联机德州扑克：多位玩家，四街下注，摊牌比牌型。</summary>
public sealed class LanPokerPlayer
{
    public string PlayerId { get; set; } = "";
    public List<Card> Cards { get; set; } = [];
    public int Committed { get; set; }
    public bool Folded { get; set; }
    public bool AllIn { get; set; }
    public string Result { get; set; } = "";
    /// <summary>true = AI 补位玩家（主机自动行动）。</summary>
    public bool IsAi { get; set; }
}

public sealed class LanPokerState
{
    public string RoomId { get; set; } = "";
    /// <summary>waiting / preflop / flop / turn / river / showdown</summary>
    public string Phase { get; set; } = "waiting";
    public List<LanPokerPlayer> Players { get; set; } = [];
    public List<Card> Community { get; set; } = [];
    public int Pot { get; set; }
    public int Stake { get; set; } = 100;
    public string CurrentPlayerId { get; set; } = "";
    public string Winner { get; set; } = "";
    public string Log { get; set; } = "";
    /// <summary>本轮已行动过的玩家</summary>
    public HashSet<string> ActedThisRound { get; set; } = [];
}

public partial class MainWindow
{
    private LanPokerState? _lanPoker;

    private static string PokerPhaseName(string phase) => phase switch
    {
        "preflop" => "翻牌前",
        "flop" => "翻牌",
        "turn" => "转牌",
        "river" => "河牌",
        "showdown" => "摊牌",
        _ => "等待"
    };

    private void ShowLanPoker(RoomInfo room)
    {
        _currentGame = "lan-game";
        Base("德州扑克 · 联机对局");
        Header();

        var me = room.Members.FirstOrDefault(m => m.PlayerId == _myNetId);
        var isHost = room.HostId == _myNetId;
        var playing = me?.Playing == true;
        var st = _lanPoker;

        _page.Children.Add(T($"房间：{room.Name} · 参战 {room.Members.Count(m => m.Playing)} 人", 15, Accent));

        if (!playing)
            _page.Children.Add(T("👁 观战中：看不到任何底牌。", 15, Accent));

        if (st is null || st.Phase == "waiting")
        {
            _page.Children.Add(T("德州扑克联机版：两张底牌 + 五张公共牌，四街下注。", 14, Muted));
            if (isHost)
                _page.Children.Add(B("开始新局（房主）", () => SendOrLocal(new NetMessage { Type = "poker_start", RoomId = room.Id })));
            _page.Children.Add(B("返回房间", () => { _currentGame = "lan-room"; ShowRoomPage(); }));
            return;
        }

        // 公共牌
        var pokerRemain = TurnRemainingSeconds(room.Id);
        var timerText = pokerRemain >= 0 ? $" · ⏱ {pokerRemain}s" : "";
        _page.Children.Add(T($"阶段：{PokerPhaseName(st.Phase)} · 底池：${st.Pot:N0} · 当前注：${st.Stake:N0}{timerText}", 15));
        var board = new WrapPanel { Margin = new Thickness(0, 4, 0, 6) };
        board.Children.Add(T("公共牌：", 14));
        if (st.Community.Count == 0) board.Children.Add(T("尚未翻牌", 13, Muted));
        foreach (var c in st.Community) board.Children.Add(CardView.Create(c));
        _page.Children.Add(board);

        if (st.Log.Length > 0) _page.Children.Add(T(st.Log, 14, Accent));
        if (st.Winner.Length > 0) _page.Children.Add(T($"🏆 {st.Winner}", 18));

        // 我的手牌
        var mine = st.Players.FirstOrDefault(p => p.PlayerId == _myNetId);
        if (playing && mine is not null && mine.Cards.Count > 0)
        {
            var row = new WrapPanel();
            row.Children.Add(T("我的底牌：", 14));
            foreach (var c in mine.Cards) row.Children.Add(CardView.Create(c));
            if (st.Community.Count >= 3)
                row.Children.Add(T($"  牌型：{PokerScore.Name(FastPokerScore.Best([.. mine.Cards, .. st.Community]))}", 14, Accent));
            _page.Children.Add(row);
        }

        // 所有玩家
        _page.Children.Add(T("玩家状态", 16));
        foreach (var p in st.Players)
        {
            var name = DisplayName(p.PlayerId);
            var isMe = p.PlayerId == _myNetId;
            var status = p.Folded ? "已弃牌"
                : st.Phase == "showdown" && p.Result.Length > 0 ? p.Result
                : isMe ? "在局中"
                : "在局中（底牌隐藏）";
            _page.Children.Add(T($"{name}{(isMe ? "（我）" : "")}：{status} · 已下注 ${p.Committed:N0}", 14, isMe ? Accent : Muted));
        }

        // 操作
        if (playing && st.Phase is "preflop" or "flop" or "turn" or "river" && st.CurrentPlayerId == _myNetId)
        {
            var row = new WrapPanel();
            row.Children.Add(B($"跟注 ${st.Stake:N0}", () => SendOrLocal(new NetMessage { Type = "poker_call", RoomId = room.Id })));
            row.Children.Add(B($"加注 ${st.Stake * 2:N0}", () => SendOrLocal(new NetMessage { Type = "poker_raise", RoomId = room.Id })));
            row.Children.Add(B("弃牌", () => SendOrLocal(new NetMessage { Type = "poker_fold", RoomId = room.Id })));
            _page.Children.Add(row);
        }
        else if (playing && st.Phase is "preflop" or "flop" or "turn" or "river")
        {
            var waitName = DisplayName(st.CurrentPlayerId);
            _page.Children.Add(T($"等待 {waitName} 行动…", 14, Muted));
        }

        if (st.Phase == "showdown" && isHost)
            _page.Children.Add(B("再来一局", () => SendOrLocal(new NetMessage { Type = "poker_start", RoomId = room.Id })));

        _page.Children.Add(B("返回房间", () => { _currentGame = "lan-room"; ShowRoomPage(); }));
    }

    // ==================== 主机端：德州扑克逻辑 ====================

    private void LanPokerStart(string playerId, string roomId)
    {
        if (_lanServer is null) return;
        var room = _lanServer.State.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null || room.HostId != playerId) return;

        var playingIds = room.Members.Where(m => m.Playing).Select(m => m.PlayerId).ToList();

        // AI 补位：按房间设定人数补足虚拟玩家
        var aiNeeded = Math.Max(0, room.Seats - playingIds.Count);
        for (var i = 0; i < aiNeeded; i++) playingIds.Add($"ai-{i + 1}");

        if (playingIds.Count < 2) return;

        var deck = Deck.New(_rng);
        var st = new LanPokerState
        {
            RoomId = roomId,
            Phase = "preflop",
            Stake = 100,
            Pot = 0,
            Community = []
        };

        foreach (var pid in playingIds)
        {
            var p = new LanPokerPlayer { PlayerId = pid, Cards = deck.Take(2).ToList(), IsAi = pid.StartsWith("ai-") };
            deck.RemoveRange(0, 2);
            var member = room.Members.FirstOrDefault(m => m.PlayerId == pid);
            var ante = member?.Unlimited == true ? 100 : Math.Min(100, member?.Chips ?? 100);
            member?.Pay(ante);
            p.Committed = ante;
            st.Pot += ante;
            st.Players.Add(p);
        }

        st.CurrentPlayerId = playingIds[0];
        st.ActedThisRound.Clear();
        st.Log = "新局开始（翻牌前）。";
        _lanPoker = st;
        BroadcastPoker(roomId);
    }

    private void LanPokerAction(string playerId, string roomId, string action)
    {
        var st = _lanPoker;
        if (st is null || st.Phase is "waiting" or "showdown") return;
        var p = st.Players.FirstOrDefault(x => x.PlayerId == playerId);
        if (p is null || p.Folded) return;
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
        }

        st.ActedThisRound.Add(playerId);
        AdvancePoker(st, playerId);
        BroadcastPoker(roomId);
    }

    private void AdvancePoker(LanPokerState st, string current)
    {
        var alive = st.Players.Where(p => !p.Folded).ToList();
        if (alive.Count <= 1)
        {
            SettlePoker(st);
            return;
        }

        // 找下一个未行动且未弃牌的玩家
        var idx = st.Players.FindIndex(p => p.PlayerId == current);
        for (var i = 1; i <= st.Players.Count; i++)
        {
            var next = st.Players[(idx + i) % st.Players.Count];
            if (next.Folded) continue;
            if (st.ActedThisRound.Contains(next.PlayerId)) continue;
            st.CurrentPlayerId = next.PlayerId;

            // AI 补位玩家：自动行动（简单策略：胜率低则弃牌，否则跟注/偶尔加注）
            if (next.IsAi)
            {
                var equity = FastPokerScore.Best([.. next.Cards, .. st.Community]);
                var random = _rng.NextDouble();
                if (random < 0.18) next.Folded = true;
                else if (random > 0.85) { st.Stake *= 2; next.Committed += st.Stake; st.Pot += st.Stake; }
                else { next.Committed += st.Stake; st.Pot += st.Stake; }
                st.ActedThisRound.Add(next.PlayerId);
                AdvancePoker(st, next.PlayerId);
            }
            return;
        }

        // 本轮全部行动完毕 → 进入下一街
        st.ActedThisRound.Clear();
        switch (st.Phase)
        {
            case "preflop":
                st.Phase = "flop";
                DealCommunity(st, 3);
                break;
            case "flop":
                st.Phase = "turn";
                DealCommunity(st, 1);
                break;
            case "turn":
                st.Phase = "river";
                DealCommunity(st, 1);
                break;
            case "river":
                SettlePoker(st);
                return;
        }

        st.Log = $"进入{PokerPhaseName(st.Phase)}。";
        st.CurrentPlayerId = st.Players.First(p => !p.Folded).PlayerId;
    }

    private void DealCommunity(LanPokerState st, int count)
    {
        var used = st.Players.SelectMany(p => p.Cards).Concat(st.Community).ToHashSet();
        var deck = Deck.New(_rng);
        for (var i = 0; i < count; i++)
        {
            var card = deck.First(c => !used.Contains(c));
            used.Add(card);
            st.Community.Add(card);
        }
    }

    private void SettlePoker(LanPokerState st)
    {
        // 补齐 5 张公共牌
        while (st.Community.Count < 5) DealCommunity(st, 1);

        var alive = st.Players.Where(p => !p.Folded).ToList();
        if (alive.Count == 0) { st.Phase = "showdown"; st.Winner = "无人获胜"; return; }

        LanPokerPlayer best = alive[0];
        var bestScore = FastPokerScore.Best([.. best.Cards, .. st.Community]);
        foreach (var p in alive.Skip(1))
        {
            var s = FastPokerScore.Best([.. p.Cards, .. st.Community]);
            if (s > bestScore) { best = p; bestScore = s; }
        }

        st.Phase = "showdown";
        st.Winner = $"{NameOf(best.PlayerId)} 获胜，赢得底池 ${st.Pot:N0}";

        var winnerMember = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == st.RoomId)
            ?.Members.FirstOrDefault(m => m.PlayerId == best.PlayerId);
        winnerMember?.Win(st.Pot);

        foreach (var p in st.Players)
        {
            p.Result = p.Folded ? "弃牌" : PokerScore.Name(FastPokerScore.Best([.. p.Cards, .. st.Community]));
        }
    }

    private void BroadcastPoker(string roomId)
    {
        if (_lanServer is null || _lanPoker is null) return;

        var data = new Dictionary<string, string>
        {
            ["phase"] = _lanPoker.Phase,
            ["current"] = _lanPoker.CurrentPlayerId,
            ["pot"] = _lanPoker.Pot.ToString(),
            ["stake"] = _lanPoker.Stake.ToString(),
            ["winner"] = _lanPoker.Winner,
            ["log"] = _lanPoker.Log,
            ["community"] = string.Join(",", _lanPoker.Community.Select(c => $"{c.Rank}{c.Suit}"))
        };

        foreach (var p in _lanPoker.Players)
        {
            data[$"p.{p.PlayerId}.committed"] = p.Committed.ToString();
            data[$"p.{p.PlayerId}.folded"] = p.Folded ? "1" : "0";
            data[$"p.{p.PlayerId}.result"] = p.Result;
        }

        // 每人只收到自己的底牌
        foreach (var p in _lanPoker.Players)
        {
            var mine = new Dictionary<string, string>(data)
            {
                ["cards"] = string.Join(",", p.Cards.Select(c => $"{c.Rank}{c.Suit}"))
            };
            _lanServer.SendTo(p.PlayerId, new NetMessage { Type = "poker_state", RoomId = roomId, Data = mine });
        }

        // 观战者
        foreach (var m in _lanServer.State.Rooms.FirstOrDefault(r => r.Id == roomId)?.Members ?? [])
        {
            if (_lanPoker.Players.Any(p => p.PlayerId == m.PlayerId)) continue;
            _lanServer.SendTo(m.PlayerId, new NetMessage { Type = "poker_state", RoomId = roomId, Data = data });
        }

        var activePhase = _lanPoker?.Phase is "preflop" or "flop" or "turn" or "river";
        var secs = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == roomId)?.TurnSeconds ?? 30;
        SyncTurnTimer(roomId, activePhase ? _lanPoker!.CurrentPlayerId : "", secs);
        _session = _lanServer?.Snapshot();
        ShowLanGame();
    }

    private void ApplyPokerState(string roomId, Dictionary<string, string> data)
    {
        var st = new LanPokerState { RoomId = roomId };
        st.Phase = data.GetValueOrDefault("phase") ?? "waiting";
        st.CurrentPlayerId = data.GetValueOrDefault("current") ?? "";
        st.Winner = data.GetValueOrDefault("winner") ?? "";
        st.Log = data.GetValueOrDefault("log") ?? "";
        int.TryParse(data.GetValueOrDefault("pot"), out var pot); st.Pot = pot;
        int.TryParse(data.GetValueOrDefault("stake"), out var stake); st.Stake = stake;
        st.Community = ParseCards(data.GetValueOrDefault("community"));

        var myCards = ParseCards(data.GetValueOrDefault("cards"));
        if (myCards.Count > 0)
            st.Players.Add(new LanPokerPlayer { PlayerId = _myNetId, Cards = myCards });

        foreach (var kv in data.Where(k => k.Key.StartsWith("p.")))
        {
            var parts = kv.Key.Split('.');
            if (parts.Length != 3) continue;
            var pid = parts[1];
            var p = st.Players.FirstOrDefault(x => x.PlayerId == pid);
            if (p is null) { p = new LanPokerPlayer { PlayerId = pid }; st.Players.Add(p); }
            switch (parts[2])
            {
                case "committed": int.TryParse(kv.Value, out var c); p.Committed = c; break;
                case "folded": p.Folded = kv.Value == "1"; break;
                case "result": p.Result = kv.Value; break;
            }
        }

        _lanPoker = st;
        if (_currentGame == "lan-game") ShowLanGame();
    }
}
