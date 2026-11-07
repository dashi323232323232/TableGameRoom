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

    /// <summary>本局是否已经决定「继续跟」。一旦为 true 就不再重新掷骰。</summary>
    public bool Staying { get; set; }

    /// <summary>本局是否看过牌。闷牌跟注只要 1 倍，看牌后 3 倍 / 2 倍。</summary>
    public bool Looked { get; set; }

    /// <summary>
    /// 是否已经做过「要不要看牌」的判定。
    /// 看牌是整局一次的决定，不能每轮重掷 —— 否则几轮下来全员都会看牌。
    /// </summary>
    public bool LookDecided { get; set; }

    /// <summary>看牌后已经跟过几轮（用于 3 倍 / 2 倍规则）。</summary>
    public int CallCount { get; set; }

    /// <summary>本轮具体做了什么（「加注 $200」这种），显示在座位上。</summary>
    public string LastAction { get; set; } = "";
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

    /// <summary>本局已进行的轮数（玩家每次跟/加算一轮）。</summary>
    public int Rounds;

    /// <summary>玩家本轮做了什么（「跟注 $200」这种）。</summary>
    public string PlayerAction = "";

    /// <summary>玩家看牌后已经跟过几轮（用于 3 倍 / 2 倍规则）。</summary>
    public int PlayerCalls;

    public readonly List<Card> Player = [];
    public readonly List<ThreeSeat> Seats = [];
}

public partial class MainWindow
{
    private readonly ThreeState _three = new();

    /// <summary>座位名 → (气泡, 文本)，用于实时更新头顶倒计时。</summary>
    /// <summary>座位名 → 双计时气泡（行动倒计时 + 真实思考）。</summary>
    private readonly Dictionary<string, SeatTimer.SeatBubble> _threeSeatTimers = [];
    private SeatTimerDriver? _threeTimer;

    /// <summary>驱动「当前行动电脑」的逐秒倒计时（同时只有一个在跑）。</summary>
    private SeatTimerDriver? _threeAiTimer;

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
                v = ThreeSeatView(seat.Name, seat.Emoji, seat.Cards, hide,
                    seat.Folded ? "已弃牌" : seat.Status, seat.Committed, seat.EffectiveTier,
                    seat.Looked, seat.Folded, seat.LastAction, i);
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
        var playerSeat = ThreeSeatView(_three.PlayerFolded ? "你（观战）" : "你",
            _three.PlayerFolded ? "🚫" : _three.Seen ? "😄" : "😐",
            _three.Player, playerHide, playerStatus, _three.PlayerCommitted, 0,
            _three.Seen, _three.PlayerFolded, _three.PlayerAction);
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
            actions.Children.Add(B($"跟注 ${ThreeCallCost:N0}", () => ThreePlayerBet(1)));
            actions.Children.Add(B("加注…", ThreeChooseRaise));
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
        foreach (var (_, b) in _threeSeatTimers)
            SeatTimer.Hide(b.Bubble);

        var playerActing = _three.InRound && !_three.Finished && !_three.PlayerFolded;
        if (!playerActing) return;

        if (_threeSeatTimers.TryGetValue("你", out var me))
        {
            _threeTimer ??= new SeatTimerDriver();
            _threeTimer.Start(SeatTimer.TurnSeconds,
                seconds => SeatTimer.ShowTurnOnly(me.Bubble, me.TurnText, seconds),
                () =>
                {
                    // 超时不再只是把气泡藏起来 —— 必须给出提示并自动弃牌，
                    // 否则玩家会卡在一个「倒计时停在 0」的死状态里。
                    SeatTimer.Hide(me.Bubble);
                    if (!_three.InRound || _three.Finished || _three.PlayerFolded) return;
                    _three.Log += " 行动超时，已自动弃牌。";
                    ThreePlayerBet(0);
                });
        }
    }

    /// <summary>清掉所有座位头顶的气泡（一次行动结束后调用）。</summary>
    private void HideAllThreeTimers()
    {
        _threeAiTimer?.Stop();
        foreach (var s in _three.Seats)
            if (_threeSeatTimers.TryGetValue(s.Name, out var b)) SeatTimer.Hide(b.Bubble);
    }

    private Border ThreeSeatView(string name, string emoji, IReadOnlyList<Card> cards, bool hidden, string status, int committed, double effTier = 0, bool looked = false, bool folded = false, string lastAction = "", int seatIndex = 0)
    {
        var stack = new StackPanel { Margin = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Center };
        // 头顶双计时气泡（行动倒计时 + 真实思考）
        var bubble = SeatTimer.Create(out var turnText, out var thinkText);
        stack.Children.Add(bubble);
        _threeSeatTimers[name] = new SeatTimer.SeatBubble(bubble, turnText, thinkText);

        // ---------- 头像 + 名字（横排） ----------
        var head = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

        var avatar = AvatarVisual.Create(emoji, seatIndex, name == "你", 22);
        avatar.Margin = new Thickness(0, 0, 4, 0);
        head.Children.Add(avatar);

        var tierLabel = effTier > 0 ? $" {effTier:0.0}" : "";

        // 名字太长就省略，完整内容放 ToolTip（对应安卓端的「显示不下就简略」）
        var shortName = name.Length <= 5 ? name : name[..5] + "…";
        var nameText = T($"{shortName}{tierLabel}", 11);
        nameText.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(nameText);

        // 悬停显示完整信息
        var fullName = $"{name}{tierLabel}\n表情：{AvatarVisual.MoodLabel(AvatarVisual.FromEmoji(emoji))}";
        if (looked) fullName += "\n状态：已看牌";
        else if (!folded && cards.Count > 0) fullName += "\n状态：未看牌（闷牌）";
        if (committed > 0) fullName += $"\n累计下注：${committed:N0}";
        if (!string.IsNullOrEmpty(lastAction)) fullName += $"\n本轮：{lastAction}";
        if (folded) fullName += "\n已弃牌";
        head.ToolTip = fullName;
        nameText.ToolTip = fullName;
        avatar.ToolTip = fullName;

        stack.Children.Add(head);

        // ---------- 看牌状态（单独一行，太挤就只显示图标） ----------
        if (!folded && cards.Count > 0)
        {
            var lookText = T(looked ? "👁 已看" : "🙈 闷牌", 9, looked ? Accent : Muted);
            lookText.ToolTip = looked ? "这名玩家已经看过自己的牌" : "还没看牌，按闷牌下注";
            stack.Children.Add(lookText);
        }

        // ---------- 牌 ----------
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        if (cards.Count == 0) row.Children.Add(T("未发牌", 10, Muted));
        foreach (var card in cards) row.Children.Add(CardView.Create(card, hidden, compact: true));
        stack.Children.Add(row);

        // ---------- 本轮动作（省略显示 + ToolTip 完整） ----------
        var actionText = folded ? "已弃牌"
            : !string.IsNullOrEmpty(lastAction) ? lastAction
            : committed > 0 ? $"{status} · ${committed:N0}"
            : status;

        var actionColor = folded ? Muted
            : lastAction.StartsWith("加注") ? new SolidColorBrush(Color.FromRgb(192, 57, 43))
            : lastAction.Contains("跟注") ? Accent
            : Muted;

        // 一行放不下就截断，鼠标悬停看全部
        var shown = actionText.Length <= 8 ? actionText : actionText[..8] + "…";
        var actionLabel = T(shown, 9, actionColor);
        actionLabel.ToolTip = actionText;
        stack.Children.Add(actionLabel);

        var totalLabel = T($"累计 ${committed:N0}", 9, Muted);
        totalLabel.ToolTip = $"本局累计下注 ${committed:N0}";
        stack.Children.Add(totalLabel);
        stack.Children.Add(T($"累计 ${committed:N0}", 9, Muted));

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
        _three.Rounds = 0;
        _three.PlayerAction = "";
        _three.PlayerCalls = 0;

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
        // 已经表态过「跟」，本局不再反悔。
        // 之前每轮独立重掷，玩家跟 3 轮后每个 AI 被抽掉的概率接近 93%，
        // 6 人桌几乎必然全灭 —— 和安卓端同一个问题。
        if (seat.Staying) return true;

        var score = ThreeScore.Of(seat.Cards);
        var category = (int)(score / 1_000_000L);
        var eff = seat.EffectiveTier;
        var tierIndex = (int)Math.Round(Math.Clamp(eff - 1, 0, 6));
        var strength = AiTuning.Strength(tierIndex);

        // 1) 闷牌几乎必跟：没看牌时只要 1 倍底注，很便宜
        if (!seat.Looked)
        {
            var blindFold = 0.04;
            if (playerAction == 2) blindFold += 0.12;
            return _rng.NextDouble() >= blindFold;
        }

        // 2) 看牌后按牌型定基准
        double tableProb;
        switch (category)
        {
            case 5: tableProb = 0.98; break; // 同花顺
            case 4: tableProb = 0.96; break; // 豹子
            case 3: tableProb = 0.90; break; // 顺子
            case 2: tableProb = 0.84; break; // 同花
            case 1:
                {
                    var pairRank = GetThreePairRank(seat.Cards);
                    tableProb = 0.62 + (pairRank - 7) * 0.025;
                    break;
                }
            default:
                {
                    var high = seat.Cards.Max(c => c.Rank);
                    tableProb = 0.40 + (high - 7) * 0.040;
                    break;
                }
        }

        // 风险容忍：高风险更愿意用弱牌跟
        tableProb += (seat.RiskTolerance - 0.5) * 0.12;

        // 低档向 0.55 回归（幅度小，别把所有牌型拉平）
        if (strength < 0.5) tableProb = 0.55 + (tableProb - 0.55) * (0.75 + strength * 0.5);

        // 施压：玩家加注时降概率
        double pressure = 0;
        if (playerAction == 2) pressure = AiTuning.Lerp(0.18, 0.04, strength) * (1.10 - seat.PressureResistance * 0.55);
        tableProb -= pressure;

        tableProb += (_rng.NextDouble() - 0.5) * seat.CalculationNoise;
        tableProb = Math.Clamp(tableProb, 0.05, 0.97);

        // 3) 真实胜率（高档才模拟）
        var simTrials = (int)Math.Round(AiTuning.Lerp(0, 2000, strength * strength));
        double winRateProb = tableProb;
        if (simTrials >= 30)
        {
            var liveOpponents = _three.Seats.Count(s => !s.Folded && !ReferenceEquals(s, seat)) + (_three.PlayerFolded ? 0 : 1);
            if (liveOpponents < 1) liveOpponents = 1;

            var winRate = EstimateThreeWinRate(seat.Cards, liveOpponents, simTrials);
            var callCost = Math.Max(1, _three.Stake);
            var potOdds = callCost / (double)(_three.TablePot + callCost);
            var required = potOdds * 0.85;
            winRateProb = Math.Clamp(tableProb + (winRate - required) * 1.2, 0.05, 0.97);
        }

        var finalProb = AiTuning.BlendThree(tableProb, winRateProb, tierIndex);
        finalProb = Math.Clamp(finalProb, 0.05, 0.97);

        var keeps = _rng.NextDouble() < finalProb;

        // 看牌后决定跟就锁定，本局不再重掷
        if (keeps) seat.Staying = true;
        return keeps;
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

    /// <summary>最低加注倍数。</summary>
    private const double MinRaiseMultiplier = 1.5;

    /// <summary>
    /// 本轮跟注要多少。炸金花规则：
    ///   闷牌（没看牌）     → 1 倍底注
    ///   看牌后的第一次跟注  → 3 倍底注
    ///   看牌之后的每轮      → 2 倍底注
    /// </summary>
    private int ThreeCallCost =>
        !_three.Seen ? _three.Stake
        : _three.PlayerCalls == 0 ? _three.Stake * 3
        : _three.Stake * 2;

    /// <summary>
    /// 加注：直接弹输入框。
    ///
    /// 之前给了一堆快捷倍数（1.5/2/2.5/3…），但既然能自己填，
    /// 那些选项就是多余的 —— 直接输入更快也更自由。
    /// </summary>
    private void ThreeChooseRaise()
    {
        if (!_three.InRound || _three.PlayerFolded || _three.Finished) return;

        var baseCost = ThreeCallCost;
        var minCost = (int)Math.Ceiling(baseCost * MinRaiseMultiplier);
        var cap = Tier.Max ?? int.MaxValue;

        var input = AskText("加注金额",
            $"跟注额 ${baseCost:N0}　最低 ${minCost:N0}（{MinRaiseMultiplier:0.#} 倍）　" +
            $"上限 {(Tier.Max is int c ? $"${c:N0}" : "无上限")}",
            minCost.ToString());

        if (!int.TryParse(input?.Trim(), out var amount)) return;

        if (amount < minCost)
        {
            Msg($"加注金额不能低于 ${minCost:N0}（{MinRaiseMultiplier:0.#} 倍）。");
            return;
        }
        if (amount > cap)
        {
            Msg($"超过档位单次上限 ${cap:N0}。");
            return;
        }

        ThreeRaiseTo(amount);
    }

    /// <summary>按指定金额加注。</summary>
    private void ThreeRaiseTo(int amount)
    {
        if (!_three.InRound || _three.PlayerFolded || _three.Finished) return;

        if (Tier.Max is not null && amount > Tier.Max.Value)
        {
            Msg($"当前档位单次下注上限 ${Tier.Max.Value:N0}。");
            return;
        }
        if (_mode == "存档" && amount > _balance)
        {
            Msg($"余额不足：需要 ${amount:N0}，当前余额 ${_balance:N0}。");
            return;
        }

        CommitThree(amount);
        _three.Stake = Math.Max(_three.Stake, amount);   // 全桌跟注额抬到这一注
        _three.Rounds++;
        _three.PlayerAction = $"加注 ${amount:N0}";
        _three.Log = $"你加注 ${amount:N0}。";

        _threeTimer?.Stop();
        if (_threeSeatTimers.TryGetValue("你", out var me)) SeatTimer.Hide(me.Bubble);

        // 电脑逐个行动（复用原来的循环）
        ThreeRunAiTurn(2);
    }

    /// <summary>玩家行动：0 = 弃牌，1 = 跟注。</summary>
    private async void ThreePlayerBet(int action)
    {
        if (!_three.InRound || _three.PlayerFolded || _three.Finished || _aiThinking) return;

        // 跟注额按炸金花规则：
        //   闷牌（没看牌）     → 1 倍底注
        //   看牌后的第一次跟注  → 3 倍底注
        //   看牌之后的每轮      → 2 倍底注
        var amount = action switch
        {
            0 => 0,                                       // 弃牌不花钱
            1 => !_three.Seen ? _three.Stake
                 : _three.PlayerCalls == 0 ? _three.Stake * 3
                 : _three.Stake * 2,
            _ => _three.Stake * 2                          // 加注
        };

        if (action != 0 && Tier.Max is not null && amount > Tier.Max.Value)
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
        _three.Rounds++;
        if (action == 1) _three.PlayerCalls++;

        // 座位卡片上直接显示玩家做了什么
        _three.PlayerAction = action switch
        {
            0 => "已弃牌",
            1 => !_three.Seen ? $"闷牌跟注 ${amount:N0}"
                 : _three.PlayerCalls == 1 ? $"看牌首轮 ${amount:N0}"
                 : $"跟注 ${amount:N0}",
            _ => $"加注 ${amount:N0}"
        };

        _three.Log = action == 1 ? $"你跟注 ${amount:N0}。" : $"你加注 ${amount:N0}。";

        // 玩家不在行动，停掉自己的倒计时
        _threeTimer?.Stop();
        if (_threeSeatTimers.TryGetValue("你", out var meSeat)) SeatTimer.Hide(meSeat.Bubble);

        ThreeRunAiTurn(action);
    }

    /// <summary>
    /// 电脑逐个行动。每位 AI 走两段式思考：
    ///   1. 30 秒行动倒计时（表演用，让玩家看到「他还有多少时间」）
    ///   2. 3~25 秒真实思考（到点才真正做决定，随压力增长）
    /// 中途可能先看牌，看完重置思考时间再想一轮。
    /// </summary>
    private async void ThreeRunAiTurn(int action)
    {
        foreach (var seat in _three.Seats)
        {
            if (seat.Folded) continue;

            // ---------- 第一段：思考（30 秒行动倒计时 + 3~25 秒真实思考同时跑）----------
            await AiThink(4, _three.Stake, Tier.Max,
                $"{seat.Name} 正在考虑…", seat.Name, action == 2);

            // ---------- 决定看牌 → 重置思考时间再想一轮 ----------
            // 看牌是**整局一次**的决定，不是每轮重掷。
            // 之前每轮 `rng < 0.7`，第一轮 70% 看，第二轮剩下的又 70% 看，
            // 两轮下来几乎全员都看了牌，一个闷的都没有。
            if (!seat.LookDecided)
            {
                seat.LookDecided = true;

                // 低档更爱闷牌（省注），高档更愿意花 3 倍看清楚
                var lookChance = 0.22 + 0.28 * AiTuning.Strength(
                    (int)Math.Round(Math.Clamp(seat.EffectiveTier - 1, 0, 6)));

                if (_rng.NextDouble() < lookChance)
                {
                    seat.Looked = true;
                    seat.LastAction = "看牌";
                    ThreePage();

                    await AiThink(4, _three.Stake, Tier.Max,
                        $"{seat.Name} 看完牌，正在盘算…", seat.Name, action == 2);
                }
            }

            // 跟注额：闷牌 1 倍，看牌后 3 倍 / 2 倍。
            // 用 CallCount 计数，和玩家侧的 PlayerCalls 同一套逻辑。
            var seatCall = !seat.Looked ? _three.Stake
                         : seat.CallCount == 0 ? _three.Stake * 3
                         : _three.Stake * 2;

            // 仅用自己的牌评估，不偷看玩家或其他电脑底牌
            var keeps = AiThreeKeeps(seat, action);
            if (keeps)
            {
                seat.Committed += seatCall;
                _three.TablePot += seatCall;
                seat.CallCount++;
                seat.Status = "跟注";
                seat.LastAction = $"跟注 ${seatCall:N0}";
                var cat = (int)(ThreeScore.Of(seat.Cards) / 1_000_000L);
                seat.Emoji = cat >= 3 ? "😄" : cat == 0 ? "😐" : "😞";
                _three.Log += $" {seat.Name}({seat.EffectiveTier:0.0}) 跟注 ${seatCall:N0}。";
            }
            else
            {
                seat.Folded = true;
                seat.Status = "弃牌";
                seat.LastAction = "已弃牌";
                seat.Emoji = "😞";
                _three.Log += $" {seat.Name}({seat.EffectiveTier:0.0}) 弃牌。";
            }

            // 每行动完一位就重绘，玩家能立刻看到这一步的结果
            ThreePage();
        }

        if (_three.Seats.All(s => s.Folded))
        {
            FinishThree("全部电脑弃牌");
            return;
        }

        // 轮数封顶：真实炸金花也封顶，否则玩家无限跟注会把 AI 一个个抽干
        if (_three.Rounds >= 6)
        {
            _three.Log += " 已达 6 轮上限，强制摊牌。";
            ThreeForceShowdown();
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

    /// <summary>
    /// 强制摊牌：亮出所有还在局中的牌，比大小定输赢。
    /// 轮数封顶时调用，避免「玩家一直跟注 → AI 被反复重掷抽干」。
    /// </summary>
    private void ThreeForceShowdown()
    {
        _three.InRound = false;
        _three.Finished = true;
        _three.Spectating = false;
        foreach (var seat in _three.Seats) seat.Revealed = true;

        var mine = ThreeScore.Of(_three.Player);
        var best = mine;
        var winner = "你";

        foreach (var s in _three.Seats.Where(s => !s.Folded))
        {
            var theirs = ThreeScore.Of(s.Cards);
            if (theirs > best) { best = theirs; winner = s.Name; }
        }

        var lines = new List<string>();
        foreach (var s in _three.Seats.Where(s => !s.Folded).OrderByDescending(s => ThreeScore.Of(s.Cards)))
        {
            lines.Add($"{s.Name}：{ThreeScore.Name(ThreeScore.Of(s.Cards))}");
        }
        var detail = lines.Count > 0 ? " · " + string.Join(" / ", lines) : "";

        if (winner == "你")
        {
            _three.Winner = $"摊牌：你以{ThreeScore.Name(mine)}赢得底池 ${_three.TablePot:N0}{detail}";
            SettleRound(_three.Stake, _three.TablePot, _three.PlayerCommitted,
                $"炸金花：摊牌胜出（{ThreeScore.Name(mine)}）");
        }
        else
        {
            _three.Winner = $"摊牌：{winner} 以{ThreeScore.Name(best)}赢得底池 ${_three.TablePot:N0}（你：{ThreeScore.Name(mine)}）{detail}";
            SettleRound(_three.Stake, 0, _three.PlayerCommitted,
                $"炸金花：摊牌输给 {winner}（{ThreeScore.Name(best)}）");
        }

        ThreePage();
    }
}
