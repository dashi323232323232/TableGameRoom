using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TableGameRoom.Net;

namespace TableGameRoom;

/// <summary>联机 21 点：一位玩家一手的简化为「每人独立一手，轮流对同一个庄家」。</summary>
public sealed class LanBjPlayer
{
    public string PlayerId { get; set; } = "";
    public List<Card> Hand { get; set; } = [];
    public int Bet { get; set; }
    public bool Stood { get; set; }
    public bool Busted { get; set; }
    public bool Doubled { get; set; }
    public string Result { get; set; } = "";
    /// <summary>true = AI 补位玩家（主机自动行动）。</summary>
    public bool IsAi { get; set; }
}

public sealed class LanBjState
{
    public string RoomId { get; set; } = "";
    /// <summary>betting / playing / settled</summary>
    public string Phase { get; set; } = "betting";
    public List<Card> Dealer { get; set; } = [];
    public List<LanBjPlayer> Players { get; set; } = [];
    public string CurrentPlayerId { get; set; } = "";
    public bool DealerHidden { get; set; } = true;
}

public partial class MainWindow
{
    private LanBjState? _lanBj;
    private int _lanBjBet = 100;

    // ==================== 联机 21 点页面 ====================

    private void ShowLanBlackjack(RoomInfo room)
    {
        _currentGame = "lan-game";
        Base("21 点 · 联机对局");
        Header();

        var me = room.Members.FirstOrDefault(m => m.PlayerId == _myNetId);
        var isHost = room.HostId == _myNetId;
        var playing = me?.Playing == true;
        var state = _lanBj;

        _page.Children.Add(T($"房间：{room.Name} · 参战 {room.Members.Count(m => m.Playing)} 人 · 观战 {room.Members.Count(m => !m.Playing)} 人", 15, Accent));

        if (!playing)
            _page.Children.Add(T("👁 观战中：你看不到任何暗牌。", 15, Accent));

        // 牌桌
        var table = new Grid
        {
            Height = 340,
            Width = 720,
            Margin = new Thickness(4, 6, 4, 8),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        table.Children.Add(new Border
        {
            Width = 700,
            Height = 320,
            CornerRadius = new CornerRadius(160),
            Background = new SolidColorBrush(Color.FromRgb(226, 244, 236)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(169, 215, 204)),
            BorderThickness = new Thickness(3),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        var layout = new Grid { Margin = new Thickness(28, 12, 28, 12) };
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // 庄家
        var dealerPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        dealerPanel.Children.Add(T("庄 家", 12, Muted));
        dealerPanel.Children.Add(BlackjackHand(state?.Dealer ?? [], hideSecond: state?.DealerHidden == true, big: false));
        if (state is not null && state.Dealer.Count > 0)
        {
            var shown = state.DealerHidden ? "? 点（暗牌）" : $"{Points(state.Dealer)} 点";
            dealerPanel.Children.Add(T(shown, 15, Accent));
        }
        Grid.SetRow(dealerPanel, 0);
        layout.Children.Add(dealerPanel);

        // 中间
        var center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var phaseText = state?.Phase switch
        {
            "betting" => "下注阶段",
            "playing" => state.CurrentPlayerId == _myNetId ? "轮到你行动" : "等待其他玩家…",
            "settled" => "本局结束",
            _ => "等待开始"
        };
        center.Children.Add(T(phaseText, 15, Accent));
        var remain = TurnRemainingSeconds(room.Id);
        if (remain >= 0) center.Children.Add(T($"⏱ 剩余 {remain} 秒", 13, Muted));
        Grid.SetRow(center, 1);
        layout.Children.Add(center);

        // 玩家区（显示自己 + 其他玩家概览）
        var playerPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var myBj = state?.Players.FirstOrDefault(p => p.PlayerId == _myNetId);
        if (myBj is not null)
        {
            playerPanel.Children.Add(BlackjackHand(myBj.Hand, hideSecond: false, big: true));
            var sc = Points(myBj.Hand);
            playerPanel.Children.Add(T($"你 · {sc} 点{(myBj.Busted ? " · 爆牌" : "")}{(myBj.Stood ? " · 已停牌" : "")}", 16));
        }
        else
        {
            playerPanel.Children.Add(T("等待发牌", 14, Muted));
        }
        Grid.SetRow(playerPanel, 2);
        layout.Children.Add(playerPanel);

        table.Children.Add(layout);
        _page.Children.Add(table);

        // 其他玩家状态
        if (state is not null && state.Players.Count > 1)
        {
            _page.Children.Add(T("其他玩家", 16));
            foreach (var p in state.Players.Where(p => p.PlayerId != _myNetId))
            {
                var name = DisplayName(p.PlayerId);
                var sc = Points(p.Hand);
                var status = p.Busted ? "爆牌" : p.Stood ? "停牌" : "行动中";
                // 观战/其他玩家：不显示具体牌，只显示点数与状态
                _page.Children.Add(T($"{name}：{(p.Hand.Count == 0 ? "未发牌" : $"{sc} 点 · {status}")}", 14, Muted));
            }
        }

        // 操作区
        if (playing && state is not null)
        {
            if (state.Phase == "betting")
            {
                _page.Children.Add(T($"我的下注：${_lanBjBet:N0}", 14));
                var amountRow = new WrapPanel();
                foreach (var amt in new[] { 50, 100, 500, 1000, 5000 })
                {
                    var v = amt;
                    var chip = B($"${v:N0}", () => { _lanBjBet = v; ShowLanGame(); });
                    if (_lanBjBet == v) { chip.Background = new SolidColorBrush(Color.FromRgb(203, 223, 241)); chip.FontWeight = FontWeights.Bold; }
                    amountRow.Children.Add(chip);
                }
                _page.Children.Add(amountRow);

                var row = new WrapPanel();
                row.Children.Add(B("下注", () => SendOrLocal(new NetMessage
                {
                    Type = "bj_bet",
                    RoomId = room.Id,
                    Data = new Dictionary<string, string> { ["amount"] = _lanBjBet.ToString() }
                })));
                if (isHost)
                    row.Children.Add(B("开始发牌（房主）", () => SendOrLocal(new NetMessage { Type = "bj_deal", RoomId = room.Id })));
                _page.Children.Add(row);
            }
            else if (state.Phase == "playing" && state.CurrentPlayerId == _myNetId)
            {
                var row = new WrapPanel();
                row.Children.Add(B("要牌", () => SendOrLocal(new NetMessage { Type = "bj_hit", RoomId = room.Id })));
                row.Children.Add(B("停牌", () => SendOrLocal(new NetMessage { Type = "bj_stand", RoomId = room.Id })));
                if (myBj is { Doubled: false } && myBj.Hand.Count == 2)
                    row.Children.Add(B("双倍", () => SendOrLocal(new NetMessage { Type = "bj_double", RoomId = room.Id })));
                _page.Children.Add(row);
            }
            else if (state.Phase == "settled")
            {
                if (myBj is not null && myBj.Result.Length > 0)
                    _page.Children.Add(T($"结果：{myBj.Result}", 17));
                var row = new WrapPanel();
                if (isHost)
                    row.Children.Add(B("下一局", () => SendOrLocal(new NetMessage { Type = "bj_reset", RoomId = room.Id })));
                _page.Children.Add(row);
            }
        }

        _page.Children.Add(B("返回房间", () => { _currentGame = "lan-room"; ShowRoomPage(); }));
    }

    // ==================== 主机端：21 点逻辑 ====================

    private LanBjState EnsureBj(string roomId)
    {
        if (_lanBj is null || _lanBj.RoomId != roomId)
            _lanBj = new LanBjState { RoomId = roomId };
        return _lanBj;
    }

    private void LanBjBet(string playerId, string roomId, Dictionary<string, string> data)
    {
        if (_lanServer is null) return;
        var room = _lanServer.State.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null) return;
        var member = room.Members.FirstOrDefault(m => m.PlayerId == playerId);
        if (member is null || !member.Playing) return;

        var st = EnsureBj(roomId);
        if (st.Phase != "betting") return;

        var amount = int.TryParse(data.GetValueOrDefault("amount"), out var a) ? Math.Max(1, a) : 100;
        if (member.Busted || !member.CanAfford(amount))
        {
            _lanServer.SendTo(playerId, new NetMessage { Type = "error", Text = $"筹码不足：当前 ${member.Chips:N0}" });
            return;
        }
        var p = st.Players.FirstOrDefault(x => x.PlayerId == playerId);
        if (p is null)
        {
            p = new LanBjPlayer { PlayerId = playerId };
            st.Players.Add(p);
        }
        p.Bet = amount;
        BroadcastBj(roomId);
    }

    private void LanBjDeal(string playerId, string roomId)
    {
        if (_lanServer is null) return;
        var room = _lanServer.State.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null || room.HostId != playerId) return;

        var st = EnsureBj(roomId);
        if (st.Phase != "betting") return;

        // 只为参战且已下注的玩家发牌
        var playingIds = room.Members.Where(m => m.Playing).Select(m => m.PlayerId).ToHashSet();
        st.Players.RemoveAll(p => !playingIds.Contains(p.PlayerId) || p.Bet <= 0);

        // AI 补位：按房间设定人数补足虚拟玩家（以「参战真人数」为准，不是「已下注人数」）
        st.Players.RemoveAll(p => p.PlayerId.StartsWith("ai-"));
        var realPlaying = room.Members.Count(m => m.Playing);
        var aiNeeded = Math.Max(0, room.Seats - realPlaying);
        for (var i = 0; i < aiNeeded; i++)
        {
            st.Players.Add(new LanBjPlayer
            {
                PlayerId = $"ai-{i + 1}",
                Bet = 100,
                IsAi = true
            });
        }

        if (st.Players.Count == 0) return;

        var deck = Deck.New(_rng);
        st.Dealer = [];
        foreach (var p in st.Players)
        {
            p.Hand = [deck[^1], deck[^2]];
            deck.RemoveRange(deck.Count - 2, 2);
            p.Stood = false;
            p.Busted = false;
            p.Doubled = false;
            p.Result = "";
        }
        st.Dealer = [deck[^1], deck[^2]];
        deck.RemoveRange(deck.Count - 2, 2);

        st.DealerHidden = true;
        st.Phase = "playing";
        st.CurrentPlayerId = st.Players[0].PlayerId;

        // 天然 Blackjack 直接停牌
        foreach (var p in st.Players)
        {
            if (Points(p.Hand) == 21) p.Stood = true;
        }
        AdvanceBjTurn(st);
        BroadcastBj(roomId);
    }

    private void LanBjHit(string playerId, string roomId)
    {
        var st = _lanBj;
        if (st is null || st.Phase != "playing" || st.CurrentPlayerId != playerId) return;
        var p = st.Players.FirstOrDefault(x => x.PlayerId == playerId);
        if (p is null || p.Stood || p.Busted) return;

        var deck = Deck.New(_rng);
        // 从已知牌中排除（简化：重新发一张不在任何手牌里的牌）
        var used = st.Dealer.Concat(st.Players.SelectMany(x => x.Hand)).ToHashSet();
        var card = deck.First(c => !used.Contains(c));
        p.Hand.Add(card);

        if (Points(p.Hand) > 21) { p.Busted = true; p.Stood = true; }
        else if (Points(p.Hand) == 21) { p.Stood = true; }

        AdvanceBjTurn(st);
        BroadcastBj(roomId);
    }

    private void LanBjStand(string playerId, string roomId)
    {
        var st = _lanBj;
        if (st is null || st.Phase != "playing" || st.CurrentPlayerId != playerId) return;
        var p = st.Players.FirstOrDefault(x => x.PlayerId == playerId);
        if (p is null) return;
        p.Stood = true;
        AdvanceBjTurn(st);
        BroadcastBj(roomId);
    }

    private void LanBjDouble(string playerId, string roomId)
    {
        var st = _lanBj;
        if (st is null || st.Phase != "playing" || st.CurrentPlayerId != playerId) return;
        var p = st.Players.FirstOrDefault(x => x.PlayerId == playerId);
        if (p is null || p.Hand.Count != 2 || p.Doubled) return;

        p.Bet *= 2;
        p.Doubled = true;

        var deck = Deck.New(_rng);
        var used = st.Dealer.Concat(st.Players.SelectMany(x => x.Hand)).ToHashSet();
        p.Hand.Add(deck.First(c => !used.Contains(c)));

        if (Points(p.Hand) > 21) p.Busted = true;
        p.Stood = true;
        AdvanceBjTurn(st);
        BroadcastBj(roomId);
    }

    /// <summary>推进到下一个未完成的玩家；AI 自动行动，全部完成则庄家补牌并结算。</summary>
    private void AdvanceBjTurn(LanBjState st)
    {
        var next = st.Players.FirstOrDefault(p => !p.Stood && !p.Busted);
        if (next is not null)
        {
            st.CurrentPlayerId = next.PlayerId;

            // AI 补位玩家：自动按简单策略行动（16 以下要牌，否则停牌）
            if (next.IsAi)
            {
                var guard = 0;
                while (!next.Stood && !next.Busted && guard++ < 10)
                {
                    if (Points(next.Hand) >= 17) { next.Stood = true; break; }
                    var aiDeck = Deck.New(_rng);
                    var aiUsed = st.Dealer.Concat(st.Players.SelectMany(x => x.Hand)).ToHashSet();
                    next.Hand.Add(aiDeck.First(c => !aiUsed.Contains(c)));
                    if (Points(next.Hand) > 21) { next.Busted = true; next.Stood = true; }
                    else if (Points(next.Hand) == 21) next.Stood = true;
                }
                if (!next.Stood && !next.Busted) next.Stood = true;
                // 继续推进
                AdvanceBjTurn(st);
            }
            return;
        }

        // 庄家补牌到 17
        st.DealerHidden = false;
        var deck = Deck.New(_rng);
        var used = st.Dealer.Concat(st.Players.SelectMany(x => x.Hand)).ToHashSet();
        while (Points(st.Dealer) < 17)
        {
            var card = deck.First(c => !used.Contains(c));
            used.Add(card);
            st.Dealer.Add(card);
        }

        var dealerScore = Points(st.Dealer);
        foreach (var p in st.Players)
        {
            var ps = Points(p.Hand);
            var won = ps <= 21 && (dealerScore > 21 || ps > dealerScore);
            var tie = ps == dealerScore;
            var net = ps > 21 ? -p.Bet : won ? p.Bet : tie ? 0 : -p.Bet;

            // 统一筹码结算
            var member = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == st.RoomId)
                ?.Members.FirstOrDefault(m => m.PlayerId == p.PlayerId);
            if (member is not null)
            {
                if (net >= 0) member.Win(net); else member.Pay(-net);
            }

            p.Result = ps > 21 ? $"爆牌 · 输 ${p.Bet:N0}"
                : dealerScore > 21 ? $"庄家爆牌 · 赢 ${p.Bet:N0}"
                : ps > dealerScore ? $"赢 ${p.Bet:N0}"
                : ps == dealerScore ? "平局"
                : $"输 ${p.Bet:N0}";
        }
        st.Phase = "settled";
        st.CurrentPlayerId = "";
    }

    private void LanBjReset(string playerId, string roomId)
    {
        if (_lanServer is null) return;
        var room = _lanServer.State.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null || room.HostId != playerId) return;
        _lanBj = new LanBjState { RoomId = roomId };
        BroadcastBj(roomId);
    }

    private void BroadcastBj(string roomId)
    {
        if (_lanServer is null || _lanBj is null) return;

        var data = new Dictionary<string, string>
        {
            ["phase"] = _lanBj.Phase,
            ["current"] = _lanBj.CurrentPlayerId,
            ["dealerHidden"] = _lanBj.DealerHidden ? "1" : "0",
            ["dealer"] = string.Join(",", _lanBj.Dealer.Select(c => $"{c.Rank}{c.Suit}"))
        };
        foreach (var p in _lanBj.Players)
        {
            data[$"p.{p.PlayerId}.hand"] = string.Join(",", p.Hand.Select(c => $"{c.Rank}{c.Suit}"));
            data[$"p.{p.PlayerId}.bet"] = p.Bet.ToString();
            data[$"p.{p.PlayerId}.stood"] = p.Stood ? "1" : "0";
            data[$"p.{p.PlayerId}.busted"] = p.Busted ? "1" : "0";
            data[$"p.{p.PlayerId}.result"] = p.Result;
        }

        _lanServer.BroadcastRaw(new NetMessage { Type = "bj_state", RoomId = roomId, Data = data });
        var secs = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == roomId)?.TurnSeconds ?? 30;
        SyncTurnTimer(roomId, _lanBj?.Phase == "playing" ? _lanBj.CurrentPlayerId : "", secs);
        _session = _lanServer?.Snapshot();
        ShowLanGame();
    }

    private void ApplyBjState(string roomId, Dictionary<string, string> data)
    {
        var st = new LanBjState { RoomId = roomId };
        st.Phase = data.GetValueOrDefault("phase") ?? "betting";
        st.CurrentPlayerId = data.GetValueOrDefault("current") ?? "";
        st.DealerHidden = data.GetValueOrDefault("dealerHidden") == "1";
        st.Dealer = ParseCards(data.GetValueOrDefault("dealer"));

        foreach (var kv in data)
        {
            if (!kv.Key.StartsWith("p.")) continue;
            var parts = kv.Key.Split('.');
            if (parts.Length != 3) continue;
            var pid = parts[1];
            var p = st.Players.FirstOrDefault(x => x.PlayerId == pid);
            if (p is null) { p = new LanBjPlayer { PlayerId = pid }; st.Players.Add(p); }
            switch (parts[2])
            {
                case "hand": p.Hand = ParseCards(kv.Value); break;
                case "bet": int.TryParse(kv.Value, out var b); p.Bet = b; break;
                case "stood": p.Stood = kv.Value == "1"; break;
                case "busted": p.Busted = kv.Value == "1"; break;
                case "result": p.Result = kv.Value; break;
            }
        }

        _lanBj = st;
        if (_currentGame == "lan-game") ShowLanGame();
    }

    private static List<Card> ParseCards(string? text)
    {
        var list = new List<Card>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        foreach (var token in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length < 2) continue;
            var suit = token[^1];
            var rankText = token[..^1];
            var rank = rankText switch
            {
                "A" => 14,
                "K" => 13,
                "Q" => 12,
                "J" => 11,
                _ => int.TryParse(rankText, out var r) ? r : 2
            };
            list.Add(new Card(rank, suit));
        }
        return list;
    }
}
