using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TableGameRoom;

public sealed class PokerSeat
{
    public string Name { get; init; } = "";
    public string Emoji { get; set; } = "😐";
    public string Status { get; set; } = "等待";
    public List<Card> Cards { get; } = [];
    public int Committed { get; set; }
    public bool Folded { get; set; }
    public double PressureResistance { get; set; }
    public double RiskTolerance { get; set; }
    public double CalculationNoise { get; set; }
    public double EffectiveTier { get; set; } = 1;
    public double BluffTendency { get; set; }
}

public sealed class PokerState
{
    public int TablePot;
    public int Stage;
    public int Stake = 50;
    public int PlayerCommitted;
    public int LastPlayerAction;
    public int PlayerRaises;
    public int PlayerCalls;
    public bool InRound;
    public bool Showdown;
    public bool PlayerFolded;
    public bool Spectating;
    public string Log = "";
    public string Winner = "";
    public readonly List<Card> Player = [];
    public readonly List<Card> Community = [];
    public readonly List<PokerSeat> Seats = [];
}

public partial class MainWindow
{
    private readonly PokerState _poker = new();

    /// <summary>座位名 → (气泡, 文本)，用于实时更新头顶倒计时。</summary>
    private readonly Dictionary<string, (Border Bubble, TextBlock Text)> _pokerSeatTimers = [];
    private SeatTimerDriver? _pokerTimer;

    private static readonly string[] Emojis = ["😐", "😄", "😞", "😠"];
    private static readonly string[] StageNames = ["翻牌前", "翻牌", "转牌", "河牌摊牌"];

    private void PokerPage()
    {
        Base("德州扑克");
        Header();

        var players = _people == "双人桌" ? 2 : Math.Clamp(_seats, 3, 7);
        var stageText = StageNames[Math.Clamp(_poker.Stage, 0, 3)];
        if (_poker.PlayerFolded) stageText += " · 观战中";
        _page.Children.Add(T($"牌桌人数 {players} 人（你 + {players - 1} 位电脑）· 阶段：{stageText} · 底池：${_poker.TablePot:N0} · 你的下注：${_poker.PlayerCommitted:N0}", 12));

        const double designW = 860;
        const double designH = 400;
        var canvas = new Canvas { Width = designW, Height = designH, Background = Brushes.Transparent };

        var ellipse = new Ellipse
        {
            Width = designW * 0.78,
            Height = designH * 0.68,
            Fill = new SolidColorBrush(Color.FromRgb(226, 244, 236)),
            Stroke = new SolidColorBrush(Color.FromRgb(169, 215, 204)),
            StrokeThickness = 2
        };
        Canvas.SetLeft(ellipse, designW * 0.11);
        Canvas.SetTop(ellipse, designH * 0.10);
        canvas.Children.Add(ellipse);
        var inner = new Ellipse
        {
            Width = designW * 0.74,
            Height = designH * 0.64,
            Stroke = new SolidColorBrush(Color.FromRgb(190, 225, 210)),
            StrokeThickness = 1,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(inner, designW * 0.13);
        Canvas.SetTop(inner, designH * 0.12);
        canvas.Children.Add(inner);

        var shownSeats = _poker.Seats.Count == 0
            ? Enumerable.Range(0, players - 1).Select(i => new PokerSeat { Name = $"电脑 {i + 1}", Emoji = "😐", Status = "等待开局", EffectiveTier = _tierIndex + 1 }).ToList()
            : _poker.Seats;

        var pos = TableRing.ComputerPositions(shownSeats.Count);
        const double seatW = TableRing.SeatWidth;
        const double seatH = 72;
        for (var i = 0; i < shownSeats.Count; i++)
        {
            var view = SeatView(shownSeats[i]);
            view.Width = seatW;
            var p = TableRing.ToPixels(pos[i], designW, designH, seatW, seatH);
            Canvas.SetLeft(view, p.X);
            Canvas.SetTop(view, p.Y);
            canvas.Children.Add(view);
        }

        var playerView = PokerPlayerSeatView();
        playerView.Width = seatW;
        var pp = TableRing.ToPixels(TableRing.PlayerPos, designW, designH, seatW, seatH);
        Canvas.SetLeft(playerView, pp.X);
        Canvas.SetTop(playerView, pp.Y);
        canvas.Children.Add(playerView);

        var centerPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        centerPanel.Children.Add(T($"底池 ${_poker.TablePot:N0}", 11, Muted));
        centerPanel.Children.Add(ChipVisual.Create(_poker.TablePot));
        centerPanel.Children.Add(T("公共牌", 10, Muted));
        centerPanel.Children.Add(CommunityRow());
        Canvas.SetLeft(centerPanel, designW * 0.5 - 80);
        Canvas.SetTop(centerPanel, designH * 0.38);
        canvas.Children.Add(centerPanel);

        var viewbox = new Viewbox { Stretch = Stretch.Uniform, Child = canvas, StretchDirection = StretchDirection.Both };
        var tableHost = new Grid { Margin = new Thickness(2, 4, 2, 4) };
        tableHost.Children.Add(viewbox);
        tableHost.MaxHeight = 420;
        _page.Children.Add(tableHost);

        if (_poker.Log.Length > 0) _page.Children.Add(T(_poker.Log, 11));
        if (_poker.Winner.Length > 0) _page.Children.Add(T(_poker.Winner, 13));

        var controls = new WrapPanel { Margin = new Thickness(2, 6, 2, 2) };
        if (!_poker.InRound && !_poker.Showdown && !_poker.Spectating)
        {
            var input = StakeBox();
            controls.Children.Add(B("开始牌局", () => StartPoker(input.Text)));
        }
        else if (_poker.InRound && !_poker.Showdown && !_poker.PlayerFolded)
        {
            // 正常对局：玩家仍在桌上
            controls.Children.Add(B($"跟注 ${_poker.Stake:N0}", () => PokerPlayerAct(1)));
            controls.Children.Add(B($"加注 ${_poker.Stake * 2:N0}", () => PokerPlayerAct(2)));
            controls.Children.Add(B("弃牌", () => PokerPlayerAct(0)));
            controls.IsEnabled = !_poker.PlayerFolded;
        }
        else if (_poker.Spectating && _poker.InRound && !_poker.Showdown)
        {
            controls.Children.Add(B("继续观看（下一街）", () => PokerSpectateStep()));
            controls.Children.Add(B("自动完成", () => PokerAutoComplete()));
        }
        else
        {
            controls.Children.Add(B("再来一手", () =>
            {
                _poker.InRound = false;
                _poker.Showdown = false;
                _poker.PlayerFolded = false;
                _poker.Spectating = false;
                _poker.Winner = "";
                _poker.Log = "";
                _poker.Player.Clear();
                _poker.Community.Clear();
                _poker.Seats.Clear();
                _poker.TablePot = 0;
                _poker.PlayerCommitted = 0;
                _poker.LastPlayerAction = 0;
                _poker.PlayerRaises = 0;
                _poker.PlayerCalls = 0;
                _poker.Stage = 0;
                PokerPage();
            }));
        }
        _page.Children.Add(controls);

        // 座位头顶倒计时：轮到玩家行动时显示 30 秒倒数
        StartPokerSeatTimer();
    }

    /// <summary>
    /// 玩家行动时：自己头顶显示 30 秒倒计时；
    /// 电脑行动时：按档位 + 对局压力算出思考秒数，显示在对应电脑头顶。
    /// </summary>
    private void StartPokerSeatTimer()
    {
        _pokerTimer?.Stop();

        // 非对局中或已摊牌：全部隐藏
        var playerActing = _poker.InRound && !_poker.Showdown && !_poker.PlayerFolded;
        foreach (var (_, (bubble, _)) in _pokerSeatTimers)
            SeatTimer.Hide(bubble);

        if (!playerActing) return;

        // 玩家回合：自己头顶 30 秒
        if (_pokerSeatTimers.TryGetValue("你", out var me))
        {
            _pokerTimer ??= new SeatTimerDriver();
            _pokerTimer.Start(SeatTimer.TurnSeconds,
                seconds => SeatTimer.Show(me.Bubble, me.Text, seconds),
                () => { SeatTimer.Hide(me.Bubble); });
        }
    }

    /// <summary>
    /// 电脑思考时：在对应电脑头顶显示倒计时（时长由档位 + 压力决定）。
    /// </summary>
    private void ShowPokerAiThinking(string seatName, int stage)
    {
        if (!_pokerSeatTimers.TryGetValue(seatName, out var seat)) return;
        var ms = ThinkTiming.Delay(stage, _poker.Stake, Tier.Max);
        var seconds = Math.Max(1, (int)Math.Round(ms / 1000.0));
        SeatTimer.Show(seat.Bubble, seat.Text, seconds);
    }

    private Border SeatView(PokerSeat seat)
    {
        var stack = new StackPanel { Margin = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Center };
        var tierLabel = seat.EffectiveTier > 0 ? $" {seat.EffectiveTier:0.0}" : "";

        // 头顶倒计时气泡（轮到该座位行动时才显示）
        var bubble = SeatTimer.Create(out var bubbleText);
        stack.Children.Add(bubble);

        stack.Children.Add(T($"{seat.Name}{tierLabel} {seat.Emoji}", 11));
        var hide = !_poker.Showdown && (!_poker.Spectating || seat.Folded);
        stack.Children.Add(CardRow(seat.Cards, hidden: hide, compact: true));
        stack.Children.Add(T(seat.Folded ? "已弃牌" : $"{seat.Status} · ${seat.Committed:N0}", 9, Muted));
        stack.Children.Add(ChipVisual.Create(seat.Committed));

        // 记录气泡引用，供倒计时驱动更新
        _pokerSeatTimers[seat.Name] = (bubble, bubbleText);

        return new Border
        {
            Background = seat.Folded ? new SolidColorBrush(Color.FromRgb(238, 242, 243)) : Brushes.White,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(4, 3, 4, 3),
            Child = stack
        };
    }

    private Border PokerPlayerSeatView()
    {
        var stack = new StackPanel { Margin = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Center };
        // 玩家头顶倒计时气泡
        var bubble = SeatTimer.Create(out var bubbleText);
        stack.Children.Add(bubble);
        _pokerSeatTimers["你"] = (bubble, bubbleText);

        var label = _poker.PlayerFolded ? "你（已弃牌·观战中）" : $"你 😐 · 已下注 ${_poker.PlayerCommitted:N0}";
        stack.Children.Add(T(label, 11));
        // 玩家弃牌后显示为弃牌状态（盖牌）
        var hidePlayer = _poker.PlayerFolded && !_poker.Showdown;
        stack.Children.Add(CardRow(_poker.Player, hidden: hidePlayer, compact: false));
        if (_poker.PlayerCommitted > 0) stack.Children.Add(ChipVisual.Create(_poker.PlayerCommitted));
        if (!_poker.PlayerFolded && _poker.Player.Count + _poker.Community.Count >= 5)
            stack.Children.Add(T("最佳：" + PokerScore.Name(PokerScore.Best([.. _poker.Player, .. _poker.Community])), 10, Accent));
        else if (_poker.PlayerFolded)
            stack.Children.Add(T("已弃牌，不参与摊牌", 10, Muted));
        return new Border
        {
            Background = _poker.PlayerFolded ? new SolidColorBrush(Color.FromRgb(238, 242, 243)) : Brushes.White,
            BorderBrush = Accent,
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(4, 3, 4, 3),
            Child = stack
        };
    }

    private StackPanel CommunityRow()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        for (var i = 0; i < 5; i++)
        {
            row.Children.Add(i < _poker.Community.Count
                ? CardView.Create(_poker.Community[i])
                : CardView.Create(null, hidden: true));
        }
        return row;
    }

    private StackPanel CardRow(IReadOnlyList<Card> cards, bool hidden, bool compact = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        if (cards.Count == 0)
        {
            row.Children.Add(T("—", 10, Brushes.Gray));
            return row;
        }
        foreach (var card in cards) row.Children.Add(CardView.Create(card, hidden, compact));
        return row;
    }

    private void StartPoker(string stakeText)
    {
        if (!int.TryParse(stakeText.Trim(), out var stake) || stake <= 0)
        {
            Msg("请输入有效的下注金额。");
            return;
        }
        if (!TryStake(stake, out var error))
        {
            Msg(error);
            return;
        }

        var players = _people == "双人桌" ? 2 : Math.Clamp(_seats, 3, 7);
        var deck = Deck.New(_rng);

        _poker.Player.Clear();
        _poker.Community.Clear();
        _poker.Seats.Clear();
        _poker.PlayerCommitted = 0;
        _poker.LastPlayerAction = 0;
        _poker.PlayerRaises = 0;
        _poker.PlayerCalls = 0;
        _poker.TablePot = 0;
        _poker.Stage = 0;
        _poker.Stake = stake;
        _poker.Showdown = false;
        _poker.PlayerFolded = false;
        _poker.Spectating = false;
        _poker.Winner = "";
        _poker.InRound = true;
        _roundSettled = false;

        var baseTier = _tierIndex + 1;
        for (var i = 0; i < players - 1; i++)
        {
            // 座位等级以档位为基准做小幅抖动，但第 7 档抖动为 0，保证最强档稳定为 7。
            var jitterRange = 0.8 * (1.0 - AiTuning.Strength(_tierIndex));
            var jitter = (_rng.NextDouble() * 2 - 1) * jitterRange;
            var eff = Math.Clamp(baseTier + jitter, 1, 7);
            // 用「有效等级」反推调参：eff 越小越弱，越大越强，全程连续。
            var effIndex = eff - 1; // 0..6

            _poker.Seats.Add(new PokerSeat
            {
                Name = $"电脑 {i + 1}",
                Emoji = Emojis[_rng.Next(Emojis.Length)],
                EffectiveTier = Math.Round(eff, 1),
                PressureResistance = AiTuning.PressureResistance((int)Math.Round(effIndex)),
                RiskTolerance = AiTuning.RiskTolerance((int)Math.Round(effIndex)),
                CalculationNoise = AiTuning.CalculationNoise((int)Math.Round(effIndex)),
                BluffTendency = AiTuning.BluffTendency((int)Math.Round(effIndex))
            });
        }

        foreach (var seat in _poker.Seats)
        {
            seat.Cards.Add(deck[^1]);
            deck.RemoveAt(deck.Count - 1);
            seat.Cards.Add(deck[^1]);
            deck.RemoveAt(deck.Count - 1);
        }
        _poker.Player.Add(deck[^1]);
        deck.RemoveAt(deck.Count - 1);
        _poker.Player.Add(deck[^1]);
        deck.RemoveAt(deck.Count - 1);

        _deck = deck;

        CommitPlayer(stake);
        foreach (var seat in _poker.Seats)
        {
            seat.Committed = stake;
            _poker.TablePot += stake;
        }

        _poker.Log = $"牌局开始：你下注 ${stake:N0}，{players - 1} 位电脑跟注。";
        _result = "";
        PokerPage();
    }

    private void CommitPlayer(int amount)
    {
        if (_mode == "存档") _balance = Math.Max(0, _balance - amount);
        _poker.PlayerCommitted += amount;
        _poker.TablePot += amount;
        if (_mode == "存档") SaveProgress();
    }

    private async void PokerPlayerAct(int action)
    {
        if (!_poker.InRound || _poker.Showdown || _poker.PlayerFolded || _aiThinking) return;

        if (action == 0)
        {
            // 玩家弃牌：只结算玩家投入，不结束AI对局
            var loss = _poker.PlayerCommitted;
            _poker.PlayerFolded = true;
            _poker.Spectating = true;
            _poker.Winner = "";
            _poker.Log = $"你弃牌，损失 ${loss:N0}。进入观战，AI继续对局。";
            SettleRound(_poker.Stake, 0, loss, $"德州扑克：弃牌 · 损失 ${loss:N0}");
            PokerPage();
            return;
        }

        var amount = action == 1 ? _poker.Stake : _poker.Stake * 2;
        if (Tier.Max is not null && amount > Tier.Max.Value)
        {
            Msg($"当前档位单次下注上限 ${Tier.Max.Value:N0}，本次需要 ${amount:N0}。");
            return;
        }
        if (_poker.PlayerCommitted + amount > RoundCap)
        {
            Msg($"本局累计下注上限 ${RoundCap:N0}（单次上限 × 25 × 10），本局已下注 ${_poker.PlayerCommitted:N0}，本次需要 ${amount:N0}。");
            return;
        }
        if (_mode == "存档" && amount > _balance)
        {
            Msg($"余额不足：需要 ${amount:N0}，当前余额 ${_balance:N0}。");
            return;
        }

        CommitPlayer(amount);
        _poker.LastPlayerAction = action;
        if (action == 1) _poker.PlayerCalls++;
        if (action == 2) _poker.PlayerRaises++;
        _poker.Log = action == 1
            ? $"你跟注 ${amount:N0}。"
            : $"你加注 ${amount:N0}。";

        if (action == 2) _poker.Stake = SafePokerDouble(_poker.Stake);

        // 电脑依次思考并行动（玩家操作即时响应，只有电脑需要等待）
        var thinkStage = ThinkTiming.PokerStage(_poker.Stage);
        // 玩家不在行动，停掉并隐藏自己的倒计时
        _pokerTimer?.Stop();
        if (_pokerSeatTimers.TryGetValue("你", out var meSeat)) SeatTimer.Hide(meSeat.Bubble);
        // 在每台电脑头顶显示它的思考倒计时（时长按档位 + 压力算）
        foreach (var s in _poker.Seats.Where(x => !x.Folded))
            ShowPokerAiThinking(s.Name, thinkStage);
        await AiThink(thinkStage, _poker.Stake, Tier.Max, "电脑正在思考…");
        // 思考结束，清空所有电脑气泡
        foreach (var s in _poker.Seats)
            if (_pokerSeatTimers.TryGetValue(s.Name, out var b)) SeatTimer.Hide(b.Bubble);

        foreach (var seat in _poker.Seats)
        {
            if (seat.Folded) continue;
            if (!AiContinues(seat))
            {
                seat.Folded = true;
                seat.Status = "弃牌";
                seat.Emoji = "😞";
                continue;
            }

            var raise = AiRaises(seat);
            var put = raise ? SafePokerDouble(_poker.Stake) : _poker.Stake;
            put = Math.Min(put, Math.Max(0, RoundCap - seat.Committed));
            if (Tier.Max is not null) put = Math.Min(put, Tier.Max.Value);
            if (put == 0) { seat.Folded = true; seat.Status = "弃牌"; continue; }
            seat.Committed += put;
            _poker.TablePot += put;
            if (raise) _poker.Stake = Math.Max(_poker.Stake, put);
            seat.Status = raise ? "加注" : "跟注";
            seat.Emoji = raise ? "😄" : "😐";
            _poker.Log += $" {seat.Name} {(raise ? "加注" : "跟注")} ${put:N0}。";
        }

        if (_poker.Seats.All(s => s.Folded))
        {
            _poker.InRound = false;
            _poker.Showdown = true;
            _poker.Winner = $"全部电脑弃牌，你赢得底池 ${_poker.TablePot:N0}。";
            SettleRound(_poker.Stake, _poker.TablePot, _poker.PlayerCommitted, $"德州扑克：电脑全部弃牌 · 赢得 ${_poker.TablePot:N0}");
            PokerPage();
            return;
        }

        PokerNextStage();
    }

    /// <summary>
    /// 蒙特卡洛胜率估算。
    /// elite=true 时进入「最强档」模式：模拟次数大幅提高、对手底牌按真实范围加权抽取，
    /// 且用高性能评估器保证耗时可接受。
    /// </summary>
    private double AiEquity(PokerSeat seat, int trials)
    {
        if (seat.Cards.Count < 2) return 0.5;
        // 仅用自己的牌+公共牌+未知牌模拟，不偷看真人或其他电脑底牌
        var boardCount = 5 - _poker.Community.Count;
        var activeOpponents = _poker.Seats.Count(o => !o.Folded && !ReferenceEquals(o, seat) && !_poker.PlayerFolded) + (_poker.PlayerFolded ? 0 : 1);
        // 若玩家已弃牌，不计入对手数；若仍在桌上则模拟一个对手（玩家）
        var needed = activeOpponents * 2 + boardCount;
        var known = seat.Cards.Concat(_poker.Community).ToHashSet();
        var fullDeck = (from suit in Card.Suits from rank in Enumerable.Range(2, 13) select new Card(rank, suit))
            .Where(card => !known.Contains(card)).ToList();
        if (fullDeck.Count < needed) return 0.5;

        // 模拟次数由调用方按档位给出，这里只做安全上限。
        trials = Math.Clamp(trials, 20, 2000);

        var completed = 0;
        var equity = 0.0;
        // 复用一个池与缓冲区，避免每轮分配。
        var pool = new Card[fullDeck.Count];
        var boardBuf = new Card[5];
        var oppBuf = new Card[32];

        for (var t = 0; t < trials; t++)
        {
            if (fullDeck.Count < needed) break;

            // 拷贝可用牌到池
            var poolCount = 0;
            for (var i = 0; i < fullDeck.Count; i++) pool[poolCount++] = fullDeck[i];

            // 抽取对手底牌（不偷看真实底牌）
            var op = 0;
            for (var oi = 0; oi < activeOpponents; oi++)
            {
                for (var k = 0; k < 2; k++)
                {
                    var idx = _rng.Next(poolCount);
                    oppBuf[op++] = pool[idx];
                    pool[idx] = pool[--poolCount];
                }
            }

            // 补齐公共牌
            for (var k = 0; k < _poker.Community.Count; k++) boardBuf[k] = _poker.Community[k];
            for (var k = 0; k < boardCount; k++)
            {
                var idx = _rng.Next(poolCount);
                boardBuf[_poker.Community.Count + k] = pool[idx];
                pool[idx] = pool[--poolCount];
            }

            var totalBoard = _poker.Community.Count + boardCount;

            // 自己的 7 张：底牌 2 + 公共牌
            var mine = new Card[7];
            mine[0] = seat.Cards[0];
            mine[1] = seat.Cards[1];
            for (var k = 0; k < totalBoard; k++) mine[2 + k] = boardBuf[k];
            var aiScore = FastPokerScore.Best(mine.AsSpan(0, 2 + totalBoard));

            long bestOpp = long.MinValue;
            for (var oi = 0; oi < activeOpponents; oi++)
            {
                var theirs = new Card[7];
                theirs[0] = oppBuf[oi * 2];
                theirs[1] = oppBuf[oi * 2 + 1];
                for (var k = 0; k < totalBoard; k++) theirs[2 + k] = boardBuf[k];
                var s = FastPokerScore.Best(theirs.AsSpan(0, 2 + totalBoard));
                if (s > bestOpp) bestOpp = s;
            }

            // 若没有活跃对手，以公共牌牌力为基准
            if (activeOpponents == 0) bestOpp = FastPokerScore.Best(boardBuf.AsSpan(0, totalBoard));

            equity += aiScore > bestOpp ? 1 : aiScore == bestOpp ? 0.5 : 0;
            completed++;
        }

        return completed == 0 ? 0.5 : equity / completed;
    }

    private bool AiContinues(PokerSeat seat)
    {
        var eff = seat.EffectiveTier;
        var tierIndex = (int)Math.Round(Math.Clamp(eff - 1, 0, 6));

        // 模拟次数按档位平滑递增（1 档 40 次 → 7 档 1600 次）
        var equity = AiEquity(seat, AiTuning.Trials(tierIndex));

        // 阈值：低档用固定保守阈值，高档用真实底池赔率，中间线性混合 → 强度连续。
        var callCost = Math.Max(1, _poker.Stake);
        var potOdds = callCost / (double)(_poker.TablePot + callCost);
        var baseThreshold = AiTuning.PokerCallThreshold(tierIndex, potOdds);

        // 风险容忍微调
        baseThreshold -= (seat.RiskTolerance - 0.5) * 0.06;

        // 施压弃牌：等级越低越容易被玩家加注吓退（连续变化，不再是分段跳变）
        if (!_poker.PlayerFolded)
        {
            var strength = AiTuning.Strength(tierIndex); // 0..1
            double pressure = 0;
            if (_poker.LastPlayerAction == 2) pressure += AiTuning.Lerp(0.22, 0.05, strength);
            if (_poker.PlayerRaises >= 2) pressure += AiTuning.Lerp(0.10, 0.04, strength);
            pressure *= 1.10 - seat.PressureResistance * 0.55;
            if (_rng.NextDouble() < Math.Clamp(pressure, 0, 0.65)) return false;
        }

        // 计算误差也随档位连续减小（1 档 0.20 → 7 档 0.00）
        var noise = (_rng.NextDouble() - 0.5) * seat.CalculationNoise;
        return equity + noise >= baseThreshold;
    }

    private bool AiRaises(PokerSeat seat)
    {
        var eff = seat.EffectiveTier;
        var tierIndex = (int)Math.Round(Math.Clamp(eff - 1, 0, 6));
        var strength = AiTuning.Strength(tierIndex);

        // 加注判断用较少的模拟次数（约为跟注的一半），但同样按档位平滑递增。
        var equity = AiEquity(seat, Math.Max(20, AiTuning.Trials(tierIndex) / 2));

        // 价值加注门槛随强度下降（越强越会抓住薄价值），幅度随强度略增
        var valueThreshold = AiTuning.Lerp(0.58, 0.62, strength);
        var valueScale = AiTuning.Lerp(0.85, 1.6, strength);
        var valueCap = AiTuning.Lerp(0.30, 0.55, strength);
        var valueRaise = Math.Clamp((equity - valueThreshold) * valueScale, 0, valueCap);

        // 强牌在早期街更愿意做大底池（仅高档生效，权重随强度渐变）
        if (_poker.Stage == 0 && equity >= 0.75)
            valueRaise = Math.Max(valueRaise, 0.35 * strength);

        // 诈唬：频率由座位属性给出（已随档位递减），高档会挑对手少的时机
        var liveOpponents = _poker.Seats.Count(s => !s.Folded) + (_poker.PlayerFolded ? 0 : 1);
        var bluffBonus = liveOpponents <= 2 ? 0.05 * strength : 0.0;
        var bluffProb = seat.BluffTendency * (1.2 - _poker.Stage * 0.15) + bluffBonus;
        var bluff = _rng.NextDouble() < Math.Clamp(bluffProb, 0.01, 0.14);

        return bluff || _rng.NextDouble() < valueRaise;
    }

    private void PokerNextStage()
    {
        _poker.Stage++;
        if (_poker.Stage <= 3)
        {
            var need = _poker.Stage == 1 ? 3 : 1;
            for (var i = 0; i < need && _deck.Count > 0; i++)
            {
                _poker.Community.Add(_deck[^1]);
                _deck.RemoveAt(_deck.Count - 1);
            }
            _poker.Log += $" 进入{StageNames[_poker.Stage]}：公共牌 {string.Join(" ", _poker.Community)}。";
            // 若玩家已弃牌进入观战，保持InRound由观战推进控制；未弃牌则直接刷新页面
            if (_poker.PlayerFolded)
            {
                PokerPage();
                return;
            }
            PokerPage();
            return;
        }
        if (_poker.PlayerFolded) PokerSpectateShowdown();
        else PokerShowdown();
    }

    private void PokerSpectateStep()
    {
        if (!_poker.Spectating || !_poker.InRound || _poker.Showdown) return;
        // 模拟本街AI决策（独立随机，无捆绑）
        var activeBefore = _poker.Seats.Count(s => !s.Folded);
        if (activeBefore <= 1)
        {
            PokerSpectateShowdown();
            return;
        }
        foreach (var seat in _poker.Seats)
        {
            if (seat.Folded) continue;
            if (!AiContinues(seat))
            {
                seat.Folded = true;
                seat.Status = "弃牌";
                seat.Emoji = "😞";
                _poker.Log += $" {seat.Name} 弃牌。";
                continue;
            }
            var raise = AiRaises(seat);
            var put = raise ? SafePokerDouble(_poker.Stake) : _poker.Stake;
            put = Math.Min(put, Math.Max(0, RoundCap - seat.Committed));
            if (Tier.Max is not null) put = Math.Min(put, Tier.Max.Value);
            if (put == 0) { seat.Folded = true; seat.Status = "弃牌"; continue; }
            seat.Committed += put;
            _poker.TablePot += put;
            if (raise) _poker.Stake = Math.Max(_poker.Stake, put);
            seat.Status = raise ? "加注" : "跟注";
            seat.Emoji = raise ? "😄" : "😐";
            _poker.Log += $" {seat.Name} {(raise ? "加注" : "跟注")} ${put:N0}。";
        }
        var activeAfter = _poker.Seats.Count(s => !s.Folded);
        if (activeAfter == 0)
        {
            _poker.Spectating = false;
            _poker.InRound = false;
            _poker.Showdown = true;
            _poker.Winner = $"全部电脑弃牌，底池 ${_poker.TablePot:N0} 无人赢得（你已弃牌观战，损失 ${_poker.PlayerCommitted:N0}）。";
            PokerPage();
            return;
        }
        if (activeAfter == 1)
        {
            _poker.Spectating = false;
            var winnerSeat = _poker.Seats.First(s => !s.Folded);
            _poker.InRound = false;
            _poker.Showdown = true;
            _poker.Winner = $"{winnerSeat.Name} 获胜，赢得底池 ${_poker.TablePot:N0}。{string.Join(" ", winnerSeat.Cards)} · {PokerScore.Name(PokerScore.Best([.. winnerSeat.Cards, .. _poker.Community]))}。";
            _poker.Log += $" 仅剩 {winnerSeat.Name}，直接获胜。";
            PokerPage();
            return;
        }
        PokerNextStage();
    }

    private void PokerAutoComplete()
    {
        var guard = 0;
        while (_poker.Spectating && _poker.InRound && !_poker.Showdown && guard < 10)
        {
            guard++;
            PokerSpectateStep();
            if (_poker.Showdown) break;
        }
        if (!_poker.Showdown) PokerPage();
    }

    private void PokerSpectateShowdown()
    {
        _poker.InRound = false;
        _poker.Spectating = false;
        _poker.Showdown = true;
        if (_poker.Community.Count < 5)
        {
            while (_poker.Community.Count < 5 && _deck.Count > 0)
            {
                _poker.Community.Add(_deck[^1]);
                _deck.RemoveAt(_deck.Count - 1);
            }
        }
        var active = _poker.Seats.Where(s => !s.Folded).ToList();
        if (active.Count == 0)
        {
            _poker.Winner = $"无人摊牌，底池 ${_poker.TablePot:N0}（你已弃牌损失 ${_poker.PlayerCommitted:N0}）。公共牌：{string.Join(" ", _poker.Community)}。";
            PokerPage();
            return;
        }
        long best = long.MinValue;
        PokerSeat? bestSeat = null;
        var lines = new List<string>();
        foreach (var seat in active)
        {
            var sc = PokerScore.Best([.. seat.Cards, .. _poker.Community]);
            lines.Add($"{seat.Name}({seat.EffectiveTier:0.0}) {string.Join(" ", seat.Cards)} {PokerScore.Name(sc)}");
            if (sc > best) { best = sc; bestSeat = seat; }
        }
        _poker.Log = "观战摊牌：" + string.Join(" · ", lines) + $" · 公共牌 {string.Join(" ", _poker.Community)}。";
        _poker.Winner = $"{bestSeat!.Name} 获胜，赢得底池 ${_poker.TablePot:N0}（你已弃牌损失 ${_poker.PlayerCommitted:N0}）。";
        PokerPage();
    }

    private static int SafePokerDouble(int value) => (int)Math.Min(int.MaxValue, (long)value * 2);

    private void PokerShowdown()
    {
        _poker.InRound = false;
        _poker.Showdown = true;
        _poker.Stage = 3;
        // 若公共牌不足5张补齐
        while (_poker.Community.Count < 5 && _deck.Count > 0)
        {
            _poker.Community.Add(_deck[^1]);
            _deck.RemoveAt(_deck.Count - 1);
        }
        var mine = PokerScore.Best([.. _poker.Player, .. _poker.Community]);
        var lines = new List<string> { $"你 · {PokerScore.Name(mine)}" };
        var best = mine;
        var winner = "你";
        foreach (var seat in _poker.Seats)
        {
            if (seat.Folded)
            {
                lines.Add($"{seat.Name} · 已弃牌");
                continue;
            }
            var score = PokerScore.Best([.. seat.Cards, .. _poker.Community]);
            lines.Add($"{seat.Name}({seat.EffectiveTier:0.0}) · {PokerScore.Name(score)}");
            if (score > best)
            {
                best = score;
                winner = seat.Name;
            }
        }
        _poker.Log = "摊牌：" + string.Join(" · ", lines);
        if (winner == "你")
        {
            _poker.Winner = $"你获胜，赢得底池 ${_poker.TablePot:N0}。";
            SettleRound(_poker.Stake, _poker.TablePot, _poker.PlayerCommitted, $"德州扑克：摊牌 {PokerScore.Name(mine)} 获胜 · 赢得 ${_poker.TablePot:N0}");
        }
        else
        {
            _poker.Winner = $"{winner} 获胜，你损失 ${_poker.PlayerCommitted:N0}。";
            SettleRound(_poker.Stake, 0, _poker.PlayerCommitted, $"德州扑克：摊牌 {PokerScore.Name(mine)} · {winner} 获胜 · 损失 ${_poker.PlayerCommitted:N0}");
        }
        PokerPage();
    }
}
