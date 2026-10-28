using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TableGameRoom;

public sealed class ThreeSeat
{
    public string Name { get; init; } = "";
    public string Emoji { get; set; } = "😐";
    public string Status { get; set; } = "等待";
    public List<Card> Cards { get; } = [];
    public int Committed { get; set; }
    public bool Folded { get; set; }
    public bool Revealed { get; set; }
    public double PressureResistance { get; set; }
    public double RiskTolerance { get; set; }
    public double CalculationNoise { get; set; }
    public double EffectiveTier { get; set; } = 1;
    public double BluffTendency { get; set; }
}

public sealed class ThreeState
{
    public int TablePot;
    public int Stake = 50;
    public int PlayerCommitted;
    public int ChipStack;
    public bool InRound;
    public bool Seen;
    public bool Finished;
    public bool PlayerFolded;
    public bool Spectating;
    public string Log = "";
    public string Winner = "";
    public readonly List<Card> Player = [];
    public readonly List<ThreeSeat> Seats = [];
}

public partial class MainWindow
{
    private readonly ThreeState _three = new();

    /// <summary>座位名 → (气泡, 文本)，用于实时更新头顶倒计时。</summary>
    private readonly Dictionary<string, (Border Bubble, TextBlock Text)> _threeSeatTimers = [];
    private SeatTimerDriver? _threeTimer;

    private void ThreePage()
    {
        Base("炸金花");
        Header();

        var players = _people == "双人桌" ? 2 : Math.Clamp(_seats, 3, 7);
        _three.ChipStack = _three.TablePot <= 0 ? 0 : Math.Clamp((int)Math.Ceiling(_three.TablePot / 100.0), 1, 24);
        var sub = _three.PlayerFolded ? " · 观战中（你已弃牌）" : _three.Seen ? " · 已看牌" : " · 未看牌";
        _page.Children.Add(T($"牌桌人数 {players} 人（你 + {players - 1} 位电脑）{sub} · 底池：${_three.TablePot:N0} · 你的下注：${_three.PlayerCommitted:N0}", 12));

        const double designW = 860;
        const double designH = 380;
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

        var shown = _three.Seats.Count == 0
            ? Enumerable.Range(0, players - 1).Select(i => new ThreeSeat { Name = $"电脑 {i + 1}", Emoji = "😐", Status = "等待开局", EffectiveTier = _tierIndex + 1 }).ToList()
            : _three.Seats;
        var pos = TableRing.ComputerPositions(shown.Count);
        const double seatW = TableRing.SeatWidth;
        const double seatH = 72;
        for (var i = 0; i < shown.Count; i++)
        {
            var seat = shown[i];
            Border v;
            if (_three.Seats.Count == 0)
                v = ThreeSeatView(seat.Name, seat.Emoji, [], false, seat.Status, 0, seat.EffectiveTier);
            else
            {
                var hide = false;
                if (seat.Folded && !_three.Finished) hide = true;
                else if (!seat.Folded && !seat.Revealed && !_three.Finished) hide = true;
                // Finished时全部Revealed则亮牌
                // 观战中活跃AI亮牌更直观
                if (_three.Spectating && !seat.Folded) hide = false;
                if (_three.Finished) hide = !seat.Revealed;
                v = ThreeSeatView(seat.Name, seat.Emoji, seat.Cards, hide, seat.Folded ? "已弃牌" : seat.Status, seat.Committed, seat.EffectiveTier);
                if (seat.Folded) v.Opacity = 0.72;
            }
            v.Width = seatW;
            var p = TableRing.ToPixels(pos[i], designW, designH, seatW, seatH);
            Canvas.SetLeft(v, p.X);
            Canvas.SetTop(v, p.Y);
            canvas.Children.Add(v);
        }

        var playerHide = !_three.Seen && !_three.Finished && !_three.PlayerFolded;
        if (_three.PlayerFolded && !_three.Finished) playerHide = true;
        var playerStatus = _three.PlayerFolded ? "已弃牌·观战中" : _three.Seen ? $"已看牌 · {(_three.Player.Count == 3 ? ThreeScore.Name(ThreeScore.Of(_three.Player)) : "—")}" : "未看牌";
        var playerSeat = ThreeSeatView(_three.PlayerFolded ? "你（观战）" : "你", _three.PlayerFolded ? "🚫" : _three.Seen ? "😄" : "😐", _three.Player, playerHide, playerStatus, _three.PlayerCommitted, 0);
        playerSeat.Width = seatW;
        playerSeat.BorderBrush = _three.PlayerFolded ? PanelBorder : Accent;
        playerSeat.BorderThickness = new Thickness(_three.PlayerFolded ? 1 : 1.2);
        if (_three.PlayerFolded) playerSeat.Opacity = 0.72;
        var pp = TableRing.ToPixels(TableRing.PlayerPos, designW, designH, seatW, seatH);
        Canvas.SetLeft(playerSeat, pp.X);
        Canvas.SetTop(playerSeat, pp.Y);
        canvas.Children.Add(playerSeat);

        var center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        center.Children.Add(T($"底池 ${_three.TablePot:N0}", 11, Muted));
        center.Children.Add(ChipVisual.Create(_three.TablePot));
        if (_three.PlayerFolded) center.Children.Add(T("你已弃牌，AI继续对局", 9, Muted));
        Canvas.SetLeft(center, designW * 0.5 - 70);
        Canvas.SetTop(center, designH * 0.40);
        canvas.Children.Add(center);

        var viewbox = new Viewbox { Stretch = Stretch.Uniform, Child = canvas, StretchDirection = StretchDirection.Both };
        var host = new Grid { Margin = new Thickness(2, 4, 2, 4) };
        host.Children.Add(viewbox);
        host.MaxHeight = 400;
        _page.Children.Add(host);

        var controls = new StackPanel { Margin = new Thickness(2, 4, 2, 2) };
        if (!_three.InRound && !_three.Spectating)
        {
            // 未开局或已结算
            var input = StakeBox();
            controls.Children.Add(B("新开一局", () => StartThree(input.Text)));
        }
        else if (_three.InRound && !_three.PlayerFolded && !_three.Finished)
        {
            var actions = new WrapPanel();
            actions.Children.Add(B("看牌", () =>
            {
                _three.Seen = true;
                _three.Log = $"你看了牌：{string.Join(" ", _three.Player)} · {ThreeScore.Name(ThreeScore.Of(_three.Player))}。电脑根据你的表情调整策略。";
                ThreePage();
            }));
            actions.Children.Add(B($"跟注 ${_three.Stake:N0}", () => ThreePlayerBet(1)));
            actions.Children.Add(B($"加注 ${_three.Stake * 2:N0}", () => ThreePlayerBet(2)));
            actions.Children.Add(B($"比牌 ${_three.Stake:N0}", ThreeCompare));
            actions.Children.Add(B("弃牌", () =>
            {
                var loss = _three.PlayerCommitted;
                _three.PlayerFolded = true;
                _three.Spectating = true;
                // 玩家弃牌后不结束AI对局
                _three.Log = $"你弃牌，损失 ${loss:N0}。进入观战，AI继续对局。";
                SettleRound(_three.Stake, 0, loss, $"炸金花：弃牌 · 损失 ${loss:N0}");
                ThreePage();
            }));
            // 看牌前禁用比牌由ThreeCompare校验
            controls.Children.Add(actions);
        }
        else if (_three.Spectating && _three.InRound && !_three.Finished)
        {
            var spec = new WrapPanel();
            spec.Children.Add(B("继续观看（AI回合）", () => ThreeSpectateStep()));
            spec.Children.Add(B("自动完成", () => ThreeAutoComplete()));
            spec.Children.Add(B("离开观战", () =>
            {
                _three.Spectating = false;
                _three.InRound = false;
                _three.Finished = true;
                if (_three.Winner.Length == 0) _three.Winner = $"已离开观战。你已损失 ${_three.PlayerCommitted:N0}，底池 ${_three.TablePot:N0} 由AI继续。";
                ThreePage();
            }));
            controls.Children.Add(spec);
        }
        else
        {
            // 已结束（含观战结束）
            controls.Children.Add(B("再来一局", () =>
            {
                _three.InRound = false;
                _three.Finished = false;
                _three.PlayerFolded = false;
                _three.Spectating = false;
                _three.Seen = false;
                _three.Winner = "";
                _three.Log = "";
                _three.Player.Clear();
                _three.Seats.Clear();
                _three.TablePot = 0;
                _three.PlayerCommitted = 0;
                _three.ChipStack = 0;
                ThreePage();
            }));
        }
        _page.Children.Add(controls);

        if (_three.Log.Length > 0) _page.Children.Add(T(_three.Log, 11));
        if (_three.Winner.Length > 0) _page.Children.Add(T(_three.Winner, 13));

        // 座位头顶倒计时
        StartThreeSeatTimer();
    }

    /// <summary>
    /// 炸金花倒计时：玩家行动时自己头顶 30 秒；
    /// 电脑行动时按档位 + 对局压力显示思考秒数。
    /// </summary>
    private void StartThreeSeatTimer()
    {
        _threeTimer?.Stop();
        foreach (var (_, (bubble, _)) in _threeSeatTimers)
            SeatTimer.Hide(bubble);

        var playerActing = _three.InRound && !_three.Finished && !_three.PlayerFolded;
        if (!playerActing) return;

        if (_threeSeatTimers.TryGetValue("你", out var me))
        {
            _threeTimer ??= new SeatTimerDriver();
            _threeTimer.Start(SeatTimer.TurnSeconds,
                seconds => SeatTimer.Show(me.Bubble, me.Text, seconds),
                () => SeatTimer.Hide(me.Bubble));
        }
    }

    /// <summary>电脑思考时在对应座位头顶显示倒计时。</summary>
    private void ShowThreeAiThinking(int stage)
    {
        foreach (var seat in _three.Seats.Where(s => !s.Folded))
        {
            if (!_threeSeatTimers.TryGetValue(seat.Name, out var t)) continue;
            var ms = ThinkTiming.Delay(stage, _three.Stake, Tier.Max);
            SeatTimer.Show(t.Bubble, t.Text, Math.Max(1, (int)Math.Round(ms / 1000.0)));
        }
    }

    private Border ThreeSeatView(string name, string emoji, IReadOnlyList<Card> cards, bool hidden, string status, int committed, double effTier = 0)
    {
        var stack = new StackPanel { Margin = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Center };
        // 头顶倒计时气泡
        var bubble = SeatTimer.Create(out var bubbleText);
        stack.Children.Add(bubble);
        _threeSeatTimers[name] = (bubble, bubbleText);

        var tierLabel = effTier > 0 ? $" {effTier:0.0}" : "";
        stack.Children.Add(T($"{name}{tierLabel} {emoji}", 11));
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        if (cards.Count == 0) row.Children.Add(T("未发牌", 10, Muted));
        foreach (var card in cards) row.Children.Add(CardView.Create(card, hidden, compact: true));
        stack.Children.Add(row);
        var stakeRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        stakeRow.Children.Add(T(status, 9, Muted));
        if (committed > 0) stakeRow.Children.Add(ChipVisual.Create(committed));
        stack.Children.Add(stakeRow);
        stack.Children.Add(T($"已下注 ${committed:N0}", 9));

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Margin = new Thickness(1),
            Padding = new Thickness(4, 3, 4, 3),
            Child = stack
        };
    }

    private void StartThree(string stakeText)
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

        _three.Player.Clear();
        _three.Seats.Clear();
        _three.PlayerCommitted = 0;
        _three.TablePot = 0;
        _three.ChipStack = 0;
        _three.Stake = stake;
        _three.InRound = true;
        _roundSettled = false;
        _three.Seen = false;
        _three.Finished = false;
        _three.PlayerFolded = false;
        _three.Spectating = false;
        _three.Winner = "";
        _three.Log = "";

        var baseTier = _tierIndex + 1;
        for (var i = 0; i < players - 1; i++)
        {
            // 抖动随档位递减，第 7 档无抖动 → 稳定为 7 级
            var jitterRange = 0.8 * (1.0 - AiTuning.Strength(_tierIndex));
            var jitter = (_rng.NextDouble() * 2 - 1) * jitterRange;
            var eff = Math.Clamp(baseTier + jitter, 1, 7);
            var effIndex = (int)Math.Round(Math.Clamp(eff - 1, 0, 6));

            _three.Seats.Add(new ThreeSeat
            {
                Name = $"电脑 {i + 1}",
                Emoji = Emojis[_rng.Next(Emojis.Length)],
                EffectiveTier = Math.Round(eff, 1),
                PressureResistance = AiTuning.PressureResistance(effIndex),
                RiskTolerance = AiTuning.RiskTolerance(effIndex),
                CalculationNoise = AiTuning.CalculationNoise(effIndex),
                BluffTendency = AiTuning.BluffTendency(effIndex)
            });
        }

        foreach (var seat in _three.Seats)
        {
            for (var i = 0; i < 3; i++)
            {
                seat.Cards.Add(deck[^1]);
                deck.RemoveAt(deck.Count - 1);
            }
        }
        for (var i = 0; i < 3; i++)
        {
            _three.Player.Add(deck[^1]);
            deck.RemoveAt(deck.Count - 1);
        }

        _deck = deck;
        CommitThree(stake);
        foreach (var seat in _three.Seats)
        {
            seat.Committed = stake;
            _three.TablePot += stake;
        }

        _three.Log = $"新局开始：你下注 ${stake:N0}，{players - 1} 位电脑跟注。可以先看牌再决定。";
        _result = "";
        ThreePage();
    }

    private void CommitThree(int amount)
    {
        if (_mode == "存档") _balance = Math.Max(0, _balance - amount);
        _three.PlayerCommitted += amount;
        _three.TablePot += amount;
        if (_mode == "存档") SaveProgress();
    }

    private bool AiThreeKeeps(ThreeSeat seat, int playerAction)
    {
        var score = ThreeScore.Of(seat.Cards);
        var category = (int)(score / 1_000_000L);
        var eff = seat.EffectiveTier;
        var tierIndex = (int)Math.Round(Math.Clamp(eff - 1, 0, 6));
        var strength = AiTuning.Strength(tierIndex);

        // 1) 查表概率（低档主要依赖，直观但粗糙）
        double tableProb;
        switch (category)
        {
            case 5: tableProb = 0.96; break; // 同花顺
            case 4: tableProb = 0.94; break; // 豹子
            case 3: tableProb = 0.78; break; // 顺子
            case 2: tableProb = 0.66; break; // 同花
            case 1:
                {
                    // 对子：看对子大小
                    var pairRank = GetThreePairRank(seat.Cards);
                    tableProb = 0.52 + (pairRank - 7) * 0.03; // A对约0.73, 小对约0.40
                    break;
                }
            default:
                {
                    var high = seat.Cards.Max(c => c.Rank);
                    tableProb = 0.28 + (high - 7) * 0.022; // A高~0.43, 小牌~0.18
                    break;
                }
        }

        // 风险容忍：高风险更愿意用弱牌跟
        tableProb += (seat.RiskTolerance - 0.5) * 0.10;
        // 低档向 0.45 回归（行为更随机）
        if (strength < 0.5) tableProb = 0.45 + (tableProb - 0.45) * (0.55 + strength * 0.9);

        // 施压：玩家加注时降概率（低档更怕，高档几乎不怕），连续变化
        double pressure = 0;
        if (playerAction == 2) pressure += AiTuning.Lerp(0.22, 0.05, strength);
        pressure *= 1.10 - seat.PressureResistance * 0.55;
        tableProb -= pressure;

        tableProb += (_rng.NextDouble() - 0.5) * seat.CalculationNoise;
        tableProb = Math.Clamp(tableProb, 0.05, 0.98);

        // 2) 真实胜率（高档主要依赖，准确但耗算力）
        // 模拟次数按档位平滑递增，低档几乎不模拟，高档充分模拟。
        var liveOpponents = _three.Seats.Count(s => !s.Folded && !ReferenceEquals(s, seat)) + (_three.PlayerFolded ? 0 : 1);
        if (liveOpponents < 1) liveOpponents = 1;
        var simTrials = (int)Math.Round(AiTuning.Lerp(0, 2400, strength * strength));
        double winRateProb;
        if (simTrials < 30)
        {
            // 低档不做模拟，直接用查表结果
            winRateProb = tableProb;
        }
        else
        {
            var winRate = EstimateThreeWinRate(seat.Cards, liveOpponents, simTrials);
            var callCost = Math.Max(1, _three.Stake);
            var potOdds = callCost / (double)(_three.TablePot + callCost);
            var required = potOdds * 0.9 + 0.02;
            if (playerAction == 2) required += 0.06;
            // 胜率映射为「跟注概率」：高于所需胜率则倾向跟注
            winRateProb = winRate >= required ? Math.Clamp(0.55 + (winRate - required) * 1.5, 0, 0.99)
                                              : Math.Clamp(0.55 - (required - winRate) * 1.5, 0.02, 0.55);
        }

        // 3) 按档位连续混合：低档偏查表，高档偏真实胜率
        var finalProb = AiTuning.BlendThree(tableProb, winRateProb, tierIndex);

        // 弱牌诈唬（概率随档位下降，由 seat.BluffTendency 控制）
        if (category <= 1 && _rng.NextDouble() < seat.BluffTendency)
            return true;

        return _rng.NextDouble() < Math.Clamp(finalProb, 0.03, 0.99);
    }

    /// <summary>
    /// 炸金花胜率蒙特卡洛估算：从剩余牌中为对手发牌，比较牌型大小。
    /// 仅使用自己的三张牌与未知牌，不偷看对手底牌。
    /// </summary>
    private double EstimateThreeWinRate(IReadOnlyList<Card> mine, int opponents, int trials)
    {
        var known = mine.ToHashSet();
        var pool = (from suit in Card.Suits from rank in Enumerable.Range(2, 13) select new Card(rank, suit))
            .Where(c => !known.Contains(c)).ToList();
        var needed = opponents * 3;
        if (pool.Count < needed) return 0.5;

        var myScore = ThreeScore.Of(mine);
        var wins = 0.0;
        var completed = 0;

        for (var t = 0; t < trials; t++)
        {
            var p = pool.ToList();
            long bestOpp = long.MinValue;
            for (var oi = 0; oi < opponents; oi++)
            {
                var hand = new Card[3];
                for (var k = 0; k < 3; k++)
                {
                    var idx = _rng.Next(p.Count);
                    hand[k] = p[idx];
                    p.RemoveAt(idx);
                }
                var s = ThreeScore.Of(hand);
                if (s > bestOpp) bestOpp = s;
            }
            wins += myScore > bestOpp ? 1 : myScore == bestOpp ? 0.5 : 0;
            completed++;
        }
        return completed == 0 ? 0.5 : wins / completed;
    }

    private static int GetThreePairRank(IReadOnlyList<Card> cards)
    {
        if (cards.Count < 2) return 7;
        if (cards[0].Rank == cards[1].Rank) return cards[0].Rank;
        if (cards[1].Rank == cards[2].Rank) return cards[1].Rank;
        if (cards[0].Rank == cards[2].Rank) return cards[0].Rank;
        return cards.Max(c => c.Rank);
    }

    private async void ThreePlayerBet(int action)
    {
        if (!_three.InRound || _three.PlayerFolded || _three.Finished || _aiThinking) return;
        var amount = action == 1 ? _three.Stake : _three.Stake * 2;
        if (Tier.Max is not null && amount > Tier.Max.Value)
        {
            Msg($"当前档位单次下注上限 ${Tier.Max.Value:N0}，本次需要 ${amount:N0}。");
            return;
        }
        if (_three.PlayerCommitted + amount > RoundCap)
        {
            Msg($"本局累计下注上限 ${RoundCap:N0}（单次上限 × 25 × 10），本局已下注 ${_three.PlayerCommitted:N0}，本次需要 ${amount:N0}。");
            return;
        }
        if (_mode == "存档" && amount > _balance)
        {
            Msg($"余额不足：需要 ${amount:N0}，当前余额 ${_balance:N0}。");
            return;
        }

        CommitThree(amount);
        if (action == 2) _three.Stake *= 2;
        _three.Log = action == 1 ? $"你跟注 ${amount:N0}。" : $"你加注 ${amount:N0}。";

        // 电脑思考（玩家操作即时响应，仅电脑等待）
        // 玩家不在行动，停掉并隐藏自己的倒计时
        _threeTimer?.Stop();
        if (_threeSeatTimers.TryGetValue("你", out var meSeat)) SeatTimer.Hide(meSeat.Bubble);
        // 在每台电脑头顶显示思考倒计时（时长按档位 + 压力算）
        ShowThreeAiThinking(4);
        await AiThink(4, _three.Stake, Tier.Max, "电脑正在考虑是否跟注…");
        // 思考结束清空电脑气泡
        foreach (var s in _three.Seats)
            if (_threeSeatTimers.TryGetValue(s.Name, out var b)) SeatTimer.Hide(b.Bubble);

        foreach (var seat in _three.Seats)
        {
            if (seat.Folded) continue;
            // 仅用自己的牌评估，不偷看玩家或其他电脑底牌
            var keeps = AiThreeKeeps(seat, action);
            if (keeps)
            {
                seat.Committed += _three.Stake;
                _three.TablePot += _three.Stake;
                seat.Status = "跟注";
                var cat = (int)(ThreeScore.Of(seat.Cards) / 1_000_000L);
                seat.Emoji = cat >= 3 ? "😄" : cat == 0 ? "😐" : "😞";
                _three.Log += $" {seat.Name}({seat.EffectiveTier:0.0}) 跟注 ${_three.Stake:N0}。";
            }
            else
            {
                seat.Folded = true;
                seat.Status = "弃牌";
                seat.Emoji = "😞";
                _three.Log += $" {seat.Name}({seat.EffectiveTier:0.0}) 弃牌。";
            }
        }

        if (_three.Seats.All(s => s.Folded))
        {
            FinishThree("全部电脑弃牌");
            return;
        }
        ThreePage();
    }

    private void ThreeSpectateStep()
    {
        if (!_three.Spectating || !_three.InRound || _three.Finished) return;
        var activeBefore = _three.Seats.Count(s => !s.Folded);
        if (activeBefore <= 1)
        {
            ThreeSpectateShowdown();
            return;
        }
        // 每位剩余AI独立决策，无捆绑弃牌
        foreach (var seat in _three.Seats)
        {
            if (seat.Folded) continue;
            var keeps = AiThreeKeeps(seat, 0);
            if (keeps)
            {
                seat.Committed += _three.Stake;
                _three.TablePot += _three.Stake;
                seat.Status = "跟注";
                _three.Log += $" {seat.Name}({seat.EffectiveTier:0.0}) 跟注 ${_three.Stake:N0}。";
            }
            else
            {
                seat.Folded = true;
                seat.Status = "弃牌";
                seat.Emoji = "😞";
                _three.Log += $" {seat.Name}({seat.EffectiveTier:0.0}) 弃牌。";
            }
        }
        var activeAfter = _three.Seats.Count(s => !s.Folded);
        if (activeAfter == 0)
        {
            _three.Spectating = false;
            _three.InRound = false;
            _three.Finished = true;
            foreach (var s in _three.Seats) s.Revealed = true;
            _three.Winner = $"AI全部弃牌，底池 ${_three.TablePot:N0} 无人赢得（你已弃牌损失 ${_three.PlayerCommitted:N0}）。";
            ThreePage();
            return;
        }
        if (activeAfter == 1)
        {
            _three.Spectating = false;
            var w = _three.Seats.First(s => !s.Folded);
            w.Revealed = true;
            foreach (var s in _three.Seats.Where(s => s.Folded)) s.Revealed = true;
            _three.InRound = false;
            _three.Finished = true;
            _three.Winner = $"{w.Name}({w.EffectiveTier:0.0}) 获胜，赢得底池 ${_three.TablePot:N0}。{string.Join(" ", w.Cards)} · {ThreeScore.Name(ThreeScore.Of(w.Cards))}（你已弃牌损失 ${_three.PlayerCommitted:N0}）。";
            _three.Log += $" 仅剩 {w.Name}，直接获胜。";
            ThreePage();
            return;
        }
        // 仍有多人，等待下一回合或直接摊牌（避免无限轮）
        // 若已进行若干回合，自动摊牌更符合观战预期：此处等待用户再点一次
        ThreePage();
    }

    private void ThreeAutoComplete()
    {
        var guard = 0;
        while (_three.Spectating && _three.InRound && !_three.Finished && guard < 12)
        {
            guard++;
            // 若剩余多人且轮次较多，直接摊牌
            if (guard >= 3 && _three.Seats.Count(s => !s.Folded) > 1)
            {
                ThreeSpectateShowdown();
                break;
            }
            ThreeSpectateStep();
            if (_three.Finished) break;
        }
        if (!_three.Finished && _three.Spectating) ThreePage();
    }

    private void ThreeSpectateShowdown()
    {
        _three.InRound = false;
        _three.Spectating = false;
        _three.Finished = true;
        foreach (var s in _three.Seats) s.Revealed = true;
        var active = _three.Seats.Where(s => !s.Folded).ToList();
        if (active.Count == 0)
        {
            _three.Winner = $"无人摊牌，底池 ${_three.TablePot:N0}（你已弃牌损失 ${_three.PlayerCommitted:N0}）。";
            ThreePage();
            return;
        }
        long best = long.MinValue;
        ThreeSeat? bestSeat = null;
        var lines = new List<string>();
        foreach (var seat in active)
        {
            var sc = ThreeScore.Of(seat.Cards);
            lines.Add($"{seat.Name}({seat.EffectiveTier:0.0}) {string.Join(" ", seat.Cards)} {ThreeScore.Name(sc)}");
            if (sc > best) { best = sc; bestSeat = seat; }
        }
        _three.Log = "观战摊牌：" + string.Join(" · ", lines) + "。";
        _three.Winner = $"{bestSeat!.Name}({bestSeat.EffectiveTier:0.0}) 获胜，赢得底池 ${_three.TablePot:N0}（你已弃牌损失 ${_three.PlayerCommitted:N0}）。";
        ThreePage();
    }

    private void ThreeCompare()
    {
        if (!_three.InRound || _three.Finished) return;
        if (_three.PlayerFolded)
        {
            Msg("你已弃牌，无法比牌。");
            return;
        }
        var target = _three.Seats.FirstOrDefault(s => !s.Folded);
        if (target is null)
        {
            FinishThree("没有可比牌的对手");
            return;
        }
        if (!_three.Seen)
        {
            Msg("比牌前需要先看牌。");
            return;
        }
        var amount = _three.Stake;
        if (_mode == "存档" && amount > _balance)
        {
            Msg($"余额不足：比牌需要 ${amount:N0}，当前余额 ${_balance:N0}。");
            return;
        }
        if (Tier.Max is not null && amount > Tier.Max.Value)
        {
            Msg($"当前档位单次下注上限 ${Tier.Max.Value:N0}，比牌需要 ${amount:N0}。");
            return;
        }
        if (_three.PlayerCommitted + amount > RoundCap)
        {
            Msg($"本局累计下注上限 ${RoundCap:N0}（单次上限 × 25 × 10），本局已下注 ${_three.PlayerCommitted:N0}，比牌需要 ${amount:N0}。");
            return;
        }

        CommitThree(amount);

        var mine = ThreeScore.Of(_three.Player);
        var theirs = ThreeScore.Of(target.Cards);
        target.Revealed = true;
        if (mine >= theirs)
        {
            target.Folded = true;
            target.Status = "比牌失败";
            target.Emoji = "😠";
            _three.Log = $"比牌：你 {ThreeScore.Name(mine)} 对 {target.Name}({target.EffectiveTier:0.0}) {ThreeScore.Name(theirs)}，你获胜，{target.Name} 出局。剩余 {string.Join(" ", _three.Seats.Where(s=>!s.Folded).Select(s=>$"{s.Name}({s.EffectiveTier:0.0})"))}。";
            if (_three.Seats.All(s => s.Folded))
            {
                FinishThree("比牌淘汰最后一位电脑");
                return;
            }
        }
        else
        {
            _three.InRound = false;
            _three.Finished = true;
            foreach (var s in _three.Seats) s.Revealed = true;
            _three.Winner = $"比牌失败：你 {ThreeScore.Name(mine)}，{target.Name}({target.EffectiveTier:0.0}) {ThreeScore.Name(theirs)}，损失 ${_three.PlayerCommitted:N0}。";
            SettleRound(_three.Stake, 0, _three.PlayerCommitted, $"炸金花：比牌失败 {ThreeScore.Name(mine)} · 损失 ${_three.PlayerCommitted:N0}");
            ThreePage();
            return;
        }
        ThreePage();
    }

    private void FinishThree(string reason)
    {
        _three.InRound = false;
        _three.Finished = true;
        _three.Spectating = false;
        foreach (var seat in _three.Seats) seat.Revealed = true;
        var mine = ThreeScore.Name(ThreeScore.Of(_three.Player));
        var details = "";
        if (_three.Seats.Any(s => !s.Folded))
        {
            details = " · 剩余：" + string.Join(" ", _three.Seats.Where(s=>!s.Folded).Select(s=>$"{s.Name}({s.EffectiveTier:0.0})"));
        }
        // 若玩家已弃牌，FinishThree不应再次结算（已在弃牌时结算）
        if (_three.PlayerFolded)
        {
            _three.Winner = $"{reason}（你已弃牌观战，损失 ${_three.PlayerCommitted:N0}）。底池 ${_three.TablePot:N0}{details}";
            ThreePage();
            return;
        }
        _three.Winner = $"{reason}，你赢得底池 ${_three.TablePot:N0}（{mine}）。{details}";
        SettleRound(_three.Stake, _three.TablePot, _three.PlayerCommitted, $"炸金花：{reason} · 赢得 ${_three.TablePot:N0} · {mine}");
        ThreePage();
    }
}
