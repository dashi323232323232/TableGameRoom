using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TableGameRoom.Net;

namespace TableGameRoom;

/// <summary>联机骗子扑克状态（1v1 或多人轮流，简化为两方对局）。</summary>
public sealed class LanLiarState
{
    public string RoomId { get; set; } = "";
    /// <summary>waiting / playing / settled</summary>
    public string Phase { get; set; } = "waiting";
    /// <summary>当前轮到谁出牌</summary>
    public string CurrentPlayerId { get; set; } = "";
    /// <summary>玩家的手牌（只发给本人，其他人看不到）</summary>
    public Dictionary<string, List<Card>> Hands { get; set; } = [];
    public List<Card> Pile { get; set; } = [];
    public List<Card> LastCards { get; set; } = [];
    public string LastClaim { get; set; } = "";
    public string LastPlayerId { get; set; } = "";
    /// <summary>等待下家决定：继续 or 质疑</summary>
    public bool AwaitingDecision { get; set; }
    public string Winner { get; set; } = "";
    public string Log { get; set; } = "";
    public int Stake { get; set; } = 100;
}

public partial class MainWindow
{
    private LanLiarState? _lanLiar;
    private int _lanLiarCount = 1;
    private string _lanLiarClaim = "A";
    private readonly HashSet<int> _lanLiarSelected = [];

    // ==================== 联机骗子扑克页面 ====================

    private void ShowLanLiar(RoomInfo room)
    {
        _currentGame = "lan-game";
        Base("骗子扑克 · 联机对局");
        Header();

        var me = room.Members.FirstOrDefault(m => m.PlayerId == _myNetId);
        var isHost = room.HostId == _myNetId;
        var playing = me?.Playing == true;
        var state = _lanLiar;

        _page.Children.Add(T($"房间：{room.Name} · 参战 {room.Members.Count(m => m.Playing)} 人", 15, Accent));

        if (!playing)
            _page.Children.Add(T("👁 观战中：你看不到任何手牌。", 15, Accent));

        if (state is null || state.Phase == "waiting")
        {
            _page.Children.Add(T("骗子扑克联机版：轮流盖牌并声明牌型，下家可质疑。", 14, Muted));
            if (isHost)
                _page.Children.Add(B("开始新局（房主）", () => SendOrLocal(new NetMessage { Type = "liar_start", RoomId = room.Id })));
            _page.Children.Add(B("返回房间", () => { _currentGame = "lan-room"; ShowRoomPage(); }));
            return;
        }

        // 状态信息
        var liarRemain = TurnRemainingSeconds(room.Id);
        var liarTimer = liarRemain >= 0 ? $" · ⏱ {liarRemain}s" : "";
        _page.Children.Add(T($"桌面牌堆：{state.Pile.Count} 张 · 我的下注：${state.Stake:N0}{liarTimer}", 14));
        if (state.Log.Length > 0) _page.Children.Add(T(state.Log, 15, Accent));
        if (state.Winner.Length > 0) _page.Children.Add(T($"🏆 {state.Winner}", 18));

        // 我的手牌
        var myHand = state.Hands.GetValueOrDefault(_myNetId) ?? [];
        if (playing)
        {
            _page.Children.Add(T($"我的手牌（{myHand.Count} 张）", 16));
            var handRow = new WrapPanel();
            for (var i = 0; i < myHand.Count; i++)
            {
                var idx = i;
                var card = myHand[i];
                var btn = B($"{card.RankText}{card.Suit}", () =>
                {
                    if (_lanLiarSelected.Contains(idx)) _lanLiarSelected.Remove(idx);
                    else _lanLiarSelected.Add(idx);
                    ShowLanGame();
                });
                if (_lanLiarSelected.Contains(idx))
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(203, 223, 241));
                    btn.FontWeight = FontWeights.Bold;
                }
                handRow.Children.Add(btn);
            }
            _page.Children.Add(handRow);
        }

        // 出牌操作
        if (playing && state.Phase == "playing" && state.CurrentPlayerId == _myNetId && !state.AwaitingDecision)
        {
            var claimRow = new WrapPanel();
            claimRow.Children.Add(T("声明：", 14));
            foreach (var rank in new[] { "A", "K", "Q", "J", "10" })
            {
                var r = rank;
                var chip = B(r, () => { _lanLiarClaim = r; ShowLanGame(); });
                if (_lanLiarClaim == r) { chip.Background = new SolidColorBrush(Color.FromRgb(203, 223, 241)); chip.FontWeight = FontWeights.Bold; }
                claimRow.Children.Add(chip);
            }
            _page.Children.Add(claimRow);

            var countRow = new WrapPanel();
            countRow.Children.Add(T("张数：", 14));
            foreach (var n in new[] { 1, 2, 3 })
            {
                var v = n;
                var chip = B($"{v} 张", () => { _lanLiarCount = v; ShowLanGame(); });
                if (_lanLiarCount == v) { chip.Background = new SolidColorBrush(Color.FromRgb(203, 223, 241)); chip.FontWeight = FontWeights.Bold; }
                countRow.Children.Add(chip);
            }
            _page.Children.Add(countRow);

            _page.Children.Add(B("盖牌并声明", () =>
            {
                var indices = string.Join(",", _lanLiarSelected.OrderBy(x => x));
                SendOrLocal(new NetMessage
                {
                    Type = "liar_play",
                    RoomId = room.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["indices"] = indices,
                        ["claim"] = _lanLiarClaim,
                        ["count"] = _lanLiarCount.ToString()
                    }
                });
                _lanLiarSelected.Clear();
            }));
        }

        // 质疑 / 继续
        if (playing && state.Phase == "playing" && state.CurrentPlayerId == _myNetId && state.AwaitingDecision)
        {
            var row = new WrapPanel();
            row.Children.Add(B("质疑上一手", () => SendOrLocal(new NetMessage { Type = "liar_challenge", RoomId = room.Id })));
            row.Children.Add(B("继续（我出牌）", () => SendOrLocal(new NetMessage { Type = "liar_pass", RoomId = room.Id })));
            _page.Children.Add(row);
        }

        if (state.Phase == "settled")
        {
            if (isHost)
                _page.Children.Add(B("再来一局", () => SendOrLocal(new NetMessage { Type = "liar_start", RoomId = room.Id })));
        }

        // 其他玩家手牌数量（不显示牌面）
        if (state.Hands.Count > 1)
        {
            _page.Children.Add(T("其他玩家", 16));
            foreach (var kv in state.Hands.Where(h => h.Key != _myNetId))
            {
                var name = DisplayName(kv.Key);
                _page.Children.Add(T($"{name}：{kv.Value.Count} 张", 14, Muted));
            }
        }

        _page.Children.Add(B("返回房间", () => { _currentGame = "lan-room"; ShowRoomPage(); }));
    }

    // ==================== 主机端：骗子扑克逻辑 ====================

    private void LanLiarStart(string playerId, string roomId)
    {
        if (_lanServer is null) return;
        var room = _lanServer.State.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null || room.HostId != playerId) return;

        var playingIds = room.Members.Where(m => m.Playing).Select(m => m.PlayerId).ToList();

        // AI 补位（骗子扑克最多 2 人）
        var aiNeeded = Math.Max(0, Math.Min(2, room.Seats) - playingIds.Count);
        for (var i = 0; i < aiNeeded; i++) playingIds.Add($"ai-{i + 1}");

        if (playingIds.Count < 2) return;

        var st = new LanLiarState { RoomId = roomId, Phase = "playing", Stake = 100 };
        var deck = Deck.New(_rng);

        // 每人 5 张
        foreach (var pid in playingIds)
        {
            var hand = deck.Take(5).ToList();
            deck.RemoveRange(0, 5);
            st.Hands[pid] = hand;
        }

        st.CurrentPlayerId = playingIds[0];
        st.Log = "新局开始，请出牌并声明。";
        _lanLiar = st;
        BroadcastLiar(roomId);
    }

    private void LanLiarPlay(string playerId, string roomId, Dictionary<string, string> data)
    {
        var st = _lanLiar;
        if (st is null || st.Phase != "playing" || st.CurrentPlayerId != playerId || st.AwaitingDecision) return;

        var hand = st.Hands.GetValueOrDefault(playerId);
        if (hand is null) return;

        var indices = (data.GetValueOrDefault("indices") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .Where(i => i >= 0 && i < hand.Count)
            .Distinct().OrderByDescending(i => i).ToList();
        if (indices.Count == 0) return;

        var claim = data.GetValueOrDefault("claim") ?? "A";
        var played = new List<Card>();
        foreach (var i in indices)
        {
            played.Add(hand[i]);
            hand.RemoveAt(i);
        }

        st.Pile.AddRange(played);
        st.LastCards = played;
        st.LastClaim = claim;
        st.LastPlayerId = playerId;
        st.AwaitingDecision = true;

        var next = NextLiarPlayer(st, playerId);
        st.CurrentPlayerId = next ?? playerId;

        if (next is null)
        {
            // 没有其他玩家
            st.Phase = "settled";
            st.Winner = "对局结束";
        }

        st.Log = $"{(st.Hands[playerId].Count == 0 ? "出完手牌！" : "盖下 " + played.Count + " 张")}，声明 {claim}。等待质疑或继续。";
        BroadcastLiar(roomId);
    }

    private void LanLiarChallenge(string playerId, string roomId)
    {
        var st = _lanLiar;
        if (st is null || st.Phase != "playing" || st.CurrentPlayerId != playerId || !st.AwaitingDecision) return;

        var truthful = st.LastCards.All(c => c.RankText == st.LastClaim);
        var challenger = playerId;
        var challenged = st.LastPlayerId;

        // 质疑成功（声明为假）→ 出牌者收牌；失败 → 质疑者收牌
        var loser = truthful ? challenger : challenged;
        var loserHand = st.Hands.GetValueOrDefault(loser);
        if (loserHand is not null) loserHand.AddRange(st.Pile);

        st.Pile.Clear();
        st.LastCards.Clear();
        st.AwaitingDecision = false;
        st.Log = $"{(truthful ? "声明属实" : "声明不实")}，{NameOf(loser)} 收走桌面 {0} 张牌。";

        // 检查胜负
        var winner = st.Hands.FirstOrDefault(h => h.Value.Count == 0);
        if (winner.Key is not null)
        {
            st.Phase = "settled";
            st.Winner = $"{NameOf(winner.Key)} 出完手牌，获胜！";
        }
        else
        {
            st.CurrentPlayerId = loser;   // 收牌者重新出牌
        }

        BroadcastLiar(roomId);
    }

    private void LanLiarPass(string playerId, string roomId)
    {
        var st = _lanLiar;
        if (st is null || st.Phase != "playing" || st.CurrentPlayerId != playerId || !st.AwaitingDecision) return;
        st.AwaitingDecision = false;
        st.Log = "继续出牌。";
        BroadcastLiar(roomId);
    }

    private string? NextLiarPlayer(LanLiarState st, string current)
    {
        var ids = st.Hands.Keys.ToList();
        var idx = ids.IndexOf(current);
        if (idx < 0) return ids.FirstOrDefault();
        return ids[(idx + 1) % ids.Count];
    }

    private string NameOf(string playerId) => DisplayName(playerId);

    private void BroadcastLiar(string roomId)
    {
        if (_lanServer is null || _lanLiar is null) return;

        var data = new Dictionary<string, string>
        {
            ["phase"] = _lanLiar.Phase,
            ["current"] = _lanLiar.CurrentPlayerId,
            ["pile"] = _lanLiar.Pile.Count.ToString(),
            ["lastClaim"] = _lanLiar.LastClaim,
            ["lastCount"] = _lanLiar.LastCards.Count.ToString(),
            ["awaiting"] = _lanLiar.AwaitingDecision ? "1" : "0",
            ["winner"] = _lanLiar.Winner,
            ["log"] = _lanLiar.Log,
            ["stake"] = _lanLiar.Stake.ToString()
        };

        // 只把「本人手牌」发给本人（防作弊）
        foreach (var kv in _lanLiar.Hands)
        {
            var myData = new Dictionary<string, string>(data)
            {
                ["hand"] = string.Join(",", kv.Value.Select(c => $"{c.Rank}{c.Suit}")),
                ["handCount"] = kv.Value.Count.ToString()
            };
            // 其他玩家的手牌数量
            foreach (var other in _lanLiar.Hands.Where(h => h.Key != kv.Key))
                myData[$"cnt.{other.Key}"] = other.Value.Count.ToString();

            _lanServer.SendTo(kv.Key, new NetMessage { Type = "liar_state", RoomId = roomId, Data = myData });
        }

        var secs = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == roomId)?.TurnSeconds ?? 30;
        SyncTurnTimer(roomId, _lanLiar?.Phase == "playing" ? _lanLiar.CurrentPlayerId : "", secs);
        _session = _lanServer?.Snapshot();
        ShowLanGame();

        // AI 补位：若轮到 AI 行动，自动出牌
        if (_lanLiar is not null && _lanLiar.Phase == "playing" && _lanLiar.CurrentPlayerId.StartsWith("ai-"))
        {
            var aiId = _lanLiar.CurrentPlayerId;
            var hand = _lanLiar.Hands.GetValueOrDefault(aiId);
            if (hand is not null && hand.Count > 0)
            {
                var claim = hand[0].RankText;
                if (!ClaimRanks.Contains(claim)) claim = "A";
                var data2 = new Dictionary<string, string>
                {
                    ["indices"] = "0",
                    ["claim"] = claim,
                    ["count"] = "1"
                };
                LanLiarPlay(aiId, roomId, data2);
            }
        }
    }

    private void ApplyLiarState(string roomId, Dictionary<string, string> data)
    {
        var st = new LanLiarState { RoomId = roomId };
        st.Phase = data.GetValueOrDefault("phase") ?? "waiting";
        st.CurrentPlayerId = data.GetValueOrDefault("current") ?? "";
        st.LastClaim = data.GetValueOrDefault("lastClaim") ?? "";
        st.AwaitingDecision = data.GetValueOrDefault("awaiting") == "1";
        st.Winner = data.GetValueOrDefault("winner") ?? "";
        st.Log = data.GetValueOrDefault("log") ?? "";
        st.Stake = int.TryParse(data.GetValueOrDefault("stake"), out var s) ? s : 100;
        var pileCount = int.TryParse(data.GetValueOrDefault("pile"), out var pc) ? pc : 0;
        st.Pile = Enumerable.Range(0, pileCount).Select(_ => new Card(2, '♠')).ToList();

        var myHand = ParseCards(data.GetValueOrDefault("hand"));
        if (myHand.Count > 0) st.Hands[_myNetId] = myHand;

        foreach (var kv in data.Where(k => k.Key.StartsWith("cnt.")))
        {
            var pid = kv.Key[4..];
            var count = int.TryParse(kv.Value, out var c) ? c : 0;
            st.Hands[pid] = Enumerable.Range(0, count).Select(_ => new Card(2, '♠')).ToList();
        }

        _lanLiar = st;
        if (_currentGame == "lan-game") ShowLanGame();
    }
}
