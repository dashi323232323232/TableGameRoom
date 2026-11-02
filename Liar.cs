using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TableGameRoom;

public sealed class LiarState
{
    public bool InRound;
    public string Log = "新局开始。你先手，请选择要盖下的牌与声明牌型，然后点击“盖牌并声明”。";
    public string Winner = "";
    public int Stake = 50;
    public int Pot;
    public int PlayerCommitted;
    public int AiCommitted;
    public double EffectiveTier = 1;
    public bool AwaitingPlayer = true;
    public bool AwaitingDecision;
    public string PlayerEmoji = "😐";
    public string AiEmoji = "😐";
    public string Claim = "A";
    public readonly List<Card> Player = [];
    public readonly List<Card> Ai = [];
    public readonly List<Card> Pile = [];
    public readonly List<Card> LastCards = [];
    public string LastClaim = "";
    public bool LastByPlayer;
    public readonly HashSet<int> Selected = [];
}

public partial class MainWindow
{
    private readonly LiarState _liar = new();

    /// <summary>骗子扑克头顶倒计时（电脑 / 玩家）。</summary>
    private (Border Bubble, TextBlock Text) _liarAiTimer;
    private (Border Bubble, TextBlock Text) _liarPlayerTimer;
    private SeatTimerDriver? _liarTimer;

    private static readonly string[] ClaimRanks = ["A", "K", "Q", "J", "10"];

    private void LiarPage()
    {
        Base("骗子扑克");
        Header();

        // ==================== 牌桌（椭圆，电脑在上、玩家在下）====================
        var table = new Grid
        {
            Height = 460,
            Width = 820,
            Margin = new Thickness(4, 4, 4, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // 桌布
        table.Children.Add(new Border
        {
            Width = 800,
            Height = 440,
            CornerRadius = new CornerRadius(220),
            Background = new SolidColorBrush(Color.FromRgb(226, 244, 236)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(169, 215, 204)),
            BorderThickness = new Thickness(3),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        // 内圈装饰
        table.Children.Add(new Border
        {
            Width = 760,
            Height = 400,
            CornerRadius = new CornerRadius(200),
            BorderBrush = new SolidColorBrush(Color.FromRgb(190, 225, 210)),
            BorderThickness = new Thickness(1.5),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        });

        var layout = new Grid { Margin = new Thickness(40, 14, 40, 14) };
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ---------- 电脑座位（上）----------
        var aiSeat = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var aiBubble = SeatTimer.Create(out var aiBubbleText);
        _liarAiTimer = (aiBubble, aiBubbleText);
        aiSeat.Children.Add(aiBubble);
        aiSeat.Children.Add(T($"电脑 {_liar.AiEmoji}  ·  等级 {_liar.EffectiveTier:0.0}  ·  手牌 {_liar.Ai.Count} 张", 15));
        aiSeat.Children.Add(CardRow(Backs(_liar.Ai.Count), true));
        Grid.SetRow(aiSeat, 0);
        layout.Children.Add(aiSeat);

        // ---------- 中央：牌堆 + 声明信息 ----------
        var center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        center.Children.Add(T($"底池 ${_liar.Pot:N0}", 15, Accent));
        center.Children.Add(ChipVisual.Create(_liar.Pot));

        var claimText = _liar.LastCards.Count > 0
            ? $"{( _liar.LastByPlayer ? "你" : "电脑")} 打出 {_liar.LastCards.Count} 张，声明为「{_liar.LastClaim}」"
            : "等待出牌";
        center.Children.Add(T(claimText, 14));

        // 牌堆可视化（背面朝上叠放）
        var pileStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        var showCount = Math.Min(_liar.Pile.Count, 8);
        for (var i = 0; i < showCount; i++)
            pileStack.Children.Add(CardView.Create(null, hidden: true, compact: true));
        if (_liar.Pile.Count == 0) pileStack.Children.Add(T("牌堆为空", 12, Muted));
        center.Children.Add(pileStack);
        center.Children.Add(T($"桌面牌堆：{_liar.Pile.Count} 张（盖牌）", 12, Muted));
        Grid.SetRow(center, 1);
        layout.Children.Add(center);

        // ---------- 玩家座位（下）----------
        var playerSeat = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var playerBubble = SeatTimer.Create(out var playerBubbleText);
        _liarPlayerTimer = (playerBubble, playerBubbleText);
        playerSeat.Children.Add(playerBubble);
        playerSeat.Children.Add(T($"你 {_liar.PlayerEmoji}  ·  手牌 {_liar.Player.Count} 张", 15));

        var hand = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
        for (var i = 0; i < _liar.Player.Count; i++)
        {
            var index = i;
            var view = CardView.Create(_liar.Player[i]);
            if (_liar.Selected.Contains(index))
            {
                view.BorderBrush = Accent;
                view.BorderThickness = new Thickness(2);
            }
            var button = new Button
            {
                Content = view,
                Padding = new Thickness(0),
                Margin = new Thickness(1),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            System.Windows.Automation.AutomationProperties.SetName(button, $"手牌 {_liar.Player[index]}");
            button.Click += (_, _) =>
            {
                if (!_liar.InRound) return;
                if (!_liar.Selected.Add(index)) _liar.Selected.Remove(index);
                LiarPage();
            };
            hand.Children.Add(button);
        }
        if (_liar.Player.Count == 0) hand.Children.Add(T("手牌已出完", 14, Muted));
        playerSeat.Children.Add(hand);
        Grid.SetRow(playerSeat, 2);
        layout.Children.Add(playerSeat);

        table.Children.Add(layout);
        _page.Children.Add(table);

        // ==================== 状态行 ====================
        var turnText = _liar.AwaitingDecision ? "轮到你：可以质疑或继续" : "轮到你：请选择要盖下的牌并声明";
        _page.Children.Add(T(turnText, 16, Accent));

        if (_liar.Log.Length > 0) _page.Children.Add(T(_liar.Log, 14, Muted));
        if (_liar.Winner.Length > 0) _page.Children.Add(T(_liar.Winner, 17));

        if (!_liar.InRound)
        {
            var input = StakeBox();
            _page.Children.Add(B("新开一局", () => StartLiar(input.Text)));
            return;
        }

        var emojiRow = new WrapPanel();
        emojiRow.Children.Add(T("表情：", 14, Muted));
        foreach (var emoji in new[] { "😐", "😄", "😞", "😠" })
        {
            var value = emoji;
            emojiRow.Children.Add(B(value, () =>
            {
                _liar.PlayerEmoji = value;
                LiarPage();
            }));
        }
        _page.Children.Add(emojiRow);

        var claimPicker = new ComboBox { Width = 90, Margin = new Thickness(4) };
        foreach (var rank in ClaimRanks) claimPicker.Items.Add(rank);
        claimPicker.SelectedItem = _liar.Claim;
        claimPicker.SelectionChanged += (_, _) =>
        {
            if (claimPicker.SelectedItem is string rank) _liar.Claim = rank;
        };

        var claimRow = new WrapPanel();
        claimRow.Children.Add(T("声明牌型：", 14, Muted));
        claimRow.Children.Add(claimPicker);
        claimRow.Children.Add(B("盖牌并声明", LiarPlay));
        _page.Children.Add(claimRow);

        var actions = new WrapPanel();
        actions.Children.Add(B("质疑上一手", () =>
        {
            if (!_liar.AwaitingDecision)
            {
                Msg("现在没有可以质疑的电脑出牌。");
                return;
            }
            ResolveLiarChallenge(challengerIsPlayer: true);
        }));
        actions.Children.Add(B("继续", () =>
        {
            _liar.AwaitingDecision = false;
            _liar.Log = "你选择继续。请盖牌并声明牌型。";
            LiarPage();
        }));
        _page.Children.Add(actions);

        // 头顶倒计时：轮到玩家时自己头顶 30 秒
        StartLiarSeatTimer();
    }

    /// <summary>骗子扑克倒计时：玩家行动时自己头顶 30 秒。</summary>
    private void StartLiarSeatTimer()
    {
        _liarTimer?.Stop();
        SeatTimer.Hide(_liarAiTimer.Bubble);
        SeatTimer.Hide(_liarPlayerTimer.Bubble);

        if (!_liar.InRound || _liar.Winner.Length > 0) return;

        _liarTimer ??= new SeatTimerDriver();
        _liarTimer.Start(SeatTimer.TurnSeconds,
            seconds => SeatTimer.Show(_liarPlayerTimer.Bubble, _liarPlayerTimer.Text, seconds),
            () => SeatTimer.Hide(_liarPlayerTimer.Bubble));
    }

    /// <summary>电脑思考时在电脑头顶显示倒计时（按牌堆压力 + 档位）。</summary>
    private void ShowLiarAiThinking()
    {
        var stage = Math.Min(8, _liar.Pile.Count);
        var ms = ThinkTiming.Delay(stage, _liar.Stake, Tier.Max);
        SeatTimer.Show(_liarAiTimer.Bubble, _liarAiTimer.Text, Math.Max(1, (int)Math.Round(ms / 1000.0)));
    }

    private static List<Card> Backs(int count)
    {
        var list = new List<Card>();
        for (var i = 0; i < count; i++) list.Add(new Card(2, '♠'));
        return list;
    }

    private void StartLiar(string stakeText)
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

        var deck = Deck.New(_rng);
        _liar.Player.Clear();
        _liar.Ai.Clear();
        _liar.Pile.Clear();
        _liar.LastCards.Clear();
        _liar.Selected.Clear();
        _liar.LastClaim = "";
        _liar.Pot = 0;
        _liar.PlayerCommitted = 0;
        _liar.AiCommitted = 0;
        _liar.Stake = stake;
        _liar.AwaitingDecision = false;
        // 等级抖动随档位递减，第 7 档无抖动 → 稳定为 7 级（全程平滑，无断档）
        var jitterRange = 0.8 * (1.0 - AiTuning.Strength(_tierIndex));
        _liar.EffectiveTier = Math.Clamp(_tierIndex + 1 + (_rng.NextDouble() * 2 - 1) * jitterRange, 1, 7);
        _liar.AwaitingPlayer = true;
        _liar.InRound = true;
        _roundSettled = false;
        _liar.Winner = "";
        _liar.PlayerEmoji = "😐";
        _liar.AiEmoji = "😐";

        for (var i = 0; i < 5; i++)
        {
            _liar.Player.Add(deck[^1]);
            deck.RemoveAt(deck.Count - 1);
            _liar.Ai.Add(deck[^1]);
            deck.RemoveAt(deck.Count - 1);
        }

        _deck = deck;
        if (_mode == "存档") _balance = Math.Max(0, _balance - stake);
        _liar.PlayerCommitted = stake;
        _liar.AiCommitted = stake;
        _liar.Pot = stake * 2;
        _liar.Log = "新局开始。你先手，请盖牌并声明。";
        _result = "";
        LiarPage();
    }

    private async void LiarPlay()
    {
        if (!_liar.InRound)
        {
            Msg("请先点击“新开一局”。");
            return;
        }
        if (_liar.AwaitingDecision)
        {
            Msg("电脑刚出过牌，请先选择“质疑上一手”或“继续”。");
            return;
        }
        if (_aiThinking) return;
        var selected = _liar.Selected.OrderByDescending(x => x).ToList();
        if (selected.Count == 0)
        {
            Msg("请先点击手牌选择要盖下的牌。");
            return;
        }

        var played = new List<Card>();
        foreach (var index in selected)
        {
            played.Add(_liar.Player[index]);
            _liar.Player.RemoveAt(index);
        }
        _liar.Selected.Clear();
        _liar.Pile.AddRange(played);
        _liar.LastCards.Clear();
        _liar.LastCards.AddRange(played);
        _liar.LastClaim = _liar.Claim;
        _liar.LastByPlayer = true;
        _liar.Log = $"你盖下 {played.Count} 张并声明 {_liar.Claim}。电脑正在判断。";

        if (_liar.Player.Count == 0)
        {
            FinishLiar("你出完手牌");
            return;
        }

        // 电脑思考：牌堆越大越紧张，思考越久
        _liarTimer?.Stop();
        SeatTimer.Hide(_liarPlayerTimer.Bubble);
        ShowLiarAiThinking();
        await AiThink(Math.Min(8, _liar.Pile.Count), _liar.Stake, Tier.Max, "电脑正在判断你的声明…");
        SeatTimer.Hide(_liarAiTimer.Bubble);
        AiRespond();
    }

    private void AiRespond()
    {
        // 7档尺度：1娱乐乱玩，2普通初学偏保守，7顶级精准。仅用己牌+公开信息判断。
        var eff = _liar.EffectiveTier;
        var tierIndex = (int)Math.Round(Math.Clamp(eff - 1, 0, 6));
        var strength = AiTuning.Strength(tierIndex);

        // 1) 启发式质疑率（低档主要依赖：看自己持牌、批量大小、表情）
        var baseRate = AiTuning.Lerp(0.16, 0.43, strength);
        var held = _liar.Ai.Count(c => c.RankText == _liar.LastClaim);
        var heldBonus = Math.Clamp(held * 0.10, 0, 0.22);
        var batchBonus = _liar.LastCards.Count >= 3 ? 0.09 : _liar.LastCards.Count >= 2 ? 0.04 : 0;
        // 表情：档位越高越不被表情干扰
        var emojiAdj = _liar.PlayerEmoji switch { "😠" => 0.10, "😞" => 0.06, "😄" => -0.07, _ => 0 };
        emojiAdj *= AiTuning.Lerp(1.0, 0.35, strength);
        var heuristic = baseRate + heldBonus + batchBonus + emojiAdj;
        // 低档噪声大（有时乱质疑/乱放过），高档噪声趋近 0
        heuristic += (_rng.NextDouble() - 0.5) * AiTuning.Lerp(0.22, 0.0, strength);
        heuristic = Math.Clamp(heuristic, 0.04, 0.88);

        // 2) 贝叶斯质疑率（高档主要依赖：牌库计数推断声明为真的概率）
        var pTruth = EstimateClaimTruthProbability();
        var claimCount = _liar.LastCards.Count;
        var pileRisk = _liar.Pile.Count + _liar.LastCards.Count;
        var threshold = 0.34 + Math.Min(0.22, pileRisk * 0.012);
        if (claimCount >= 3) threshold -= 0.05;
        if (_liar.Ai.Count <= 2) threshold -= 0.06;
        if (_liar.Player.Count <= 2) threshold += 0.05;
        // 声明为真的概率越低，越应该质疑
        var bayes = Math.Clamp((threshold - pTruth) * 2.2 + 0.35, 0.03, 0.95);

        // 3) 按档位连续混合
        var chance = AiTuning.BlendLiar(heuristic, bayes, tierIndex);
        chance = Math.Clamp(chance, 0.03, 0.92);

        if (_rng.NextDouble() < chance)
        {
            _liar.AiEmoji = "😠";
            ResolveLiarChallenge(challengerIsPlayer: false);
            return;
        }

        // 出牌策略：低档随机出牌，高档优先清空手牌，中间连续混合
        if (strength >= 0.5 && _rng.NextDouble() < (strength - 0.5) * 2)
            AiPlayElite();
        else
            AiPlay();
    }

    /// <summary>
    /// 估算玩家上一手「声明为真」的概率。
    /// 基于：自己手牌中该点数的数量、已出牌数量、牌库剩余分布。
    /// 只使用公开信息与自己手牌，不偷看玩家底牌。
    /// </summary>
    private double EstimateClaimTruthProbability()
    {
        var claim = _liar.LastClaim;
        var claimCount = _liar.LastCards.Count;
        if (claimCount <= 0) return 0.5;

        // 每种点数共 4 张。已知：自己手里该点数 X 张；桌面牌堆（已盖下）中可统计的声明未知，
        // 保守起见只扣除自己手里的和已明牌（骗子扑克桌面牌不可见，故仅用自己手牌）。
        var myHeld = _liar.Ai.Count(c => c.RankText == claim);
        var remaining = Math.Max(0, 4 - myHeld);

        // 玩家手里有多少张该点数的先验：玩家持牌 5 张，从剩余 52 - 自己5 - 桌面未知中抽。
        // 用超几何近似：玩家手里该点数的期望 = 5 * remaining / (52 - 5)。
        var unknownPool = 52 - _liar.Ai.Count;
        if (unknownPool <= 0) return 0.5;
        var expectedPlayerHolds = 5.0 * remaining / unknownPool;

        // 若一次声称 claimCount 张，且期望持有数不足，则为真的概率低。
        // 用泊松近似：P(至少持有 claimCount 张该点数)
        var lambda = Math.Max(0.01, expectedPlayerHolds);
        var pAtLeast = 0.0;
        var term = Math.Exp(-lambda); // P(0)
        var cumulative = 0.0;
        for (var k = 0; k < claimCount; k++)
        {
            if (k > 0) term *= lambda / k;
            cumulative += term;
        }
        pAtLeast = Math.Clamp(1.0 - cumulative, 0.0, 1.0);

        // 玩家也可能诚实但手里确实够：加入行为先验（多数玩家不会每次都撒谎）
        // 但若剩余牌里该点数已几乎被自己占满，则声明为真的概率极低。
        if (remaining == 0) return 0.02;
        if (remaining == 1 && claimCount >= 2) return Math.Min(pAtLeast, 0.08);

        return pAtLeast;
    }

    /// <summary>最强档出牌策略：优先清空手牌、保持声明可信度、控制诈唬时机。</summary>
    private void AiPlayElite()
    {
        var count = Math.Min(_liar.Ai.Count, 3);
        if (count < 1) { AiPlay(); return; }

        // 按点数分组，优先出数量最多的组（快速清手牌）
        var groups = _liar.Ai.GroupBy(c => c.RankText)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => RankOrder(g.Key))
            .ToArray();

        var best = groups[0];
        var playCount = Math.Min(best.Count(), count);

        // 判断是否该诈唬：只在手牌少、或对手快赢时诈唬
        var shouldBluff = false;
        if (_liar.Ai.Count <= 3 && _liar.Player.Count > 2)
        {
            // 手牌少时，用一次大胆声明快速结束
            shouldBluff = _rng.NextDouble() < 0.25;
        }

        string claim;
        List<Card> chosen;

        if (shouldBluff)
        {
            // 诈唬：选一个自己完全没有的点数，避免被质疑后立刻暴露
            var emptyRanks = ClaimRanks.Where(r => _liar.Ai.All(c => c.RankText != r)).ToArray();
            claim = emptyRanks.Length > 0 ? emptyRanks[_rng.Next(emptyRanks.Length)] : best.Key;
            chosen = _liar.Ai.OrderBy(_ => _rng.Next()).Take(playCount).ToList();
        }
        else
        {
            claim = best.Key;
            chosen = best.Take(playCount).ToList();
        }

        foreach (var card in chosen) _liar.Ai.Remove(card);
        _liar.Pile.AddRange(chosen);
        _liar.LastCards.Clear();
        _liar.LastCards.AddRange(chosen);
        _liar.LastClaim = claim;
        _liar.LastByPlayer = false;
        _liar.AwaitingDecision = true;
        // 最强档会刻意隐藏真实情绪，只在诈唬成功时露出微表情。
        _liar.AiEmoji = "😐";
        _liar.Log = $"电脑（等级7.0）盖下 {chosen.Count} 张，声明 {claim}。它的表情：{_liar.AiEmoji}。你可以质疑或继续。";

        if (_liar.Ai.Count == 0)
        {
            FinishLiar("电脑出完手牌");
            return;
        }
        LiarPage();
    }

    /// <summary>声明点数的大小排序，A 最大。</summary>
    private static int RankOrder(string rank) => rank switch
    {
        "A" => 14,
        "K" => 13,
        "Q" => 12,
        "J" => 11,
        "10" => 10,
        _ => int.TryParse(rank, out var v) ? v : 0
    };

    private void AiPlay()
    {
        var eff = _liar.EffectiveTier;
        var count = Math.Min(_liar.Ai.Count, _rng.Next(1, Math.Min(3, _liar.Ai.Count) + 1));
        if (count < 1) count = 1;
        var grouped = _liar.Ai.GroupBy(c => c.RankText).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First();
        // 高级更倾向诚实或合理诈唬；低级更随机
        double truthProb = 0.38 + (eff - 1) * 0.07; // 1:0.38 ~7:0.80
        // 若手里该组够数则更可能诚实
        if (grouped.Count() >= count) truthProb += 0.12;
        var truthful = _rng.NextDouble() < Math.Clamp(truthProb, 0.2, 0.88);
        string claim;
        if (truthful)
        {
            claim = grouped.Key;
        }
        else
        {
            // 诈唬：选一个自己没有或很少的声明，等级越高越会挑合理诈唬
            var candidates = ClaimRanks.Where(r => _liar.Ai.Count(c => c.RankText == r) == 0).ToArray();
            if (candidates.Length > 0 && _rng.NextDouble() < 0.7 + (eff - 1) * 0.03)
                claim = candidates[_rng.Next(candidates.Length)];
            else
                claim = ClaimRanks[_rng.Next(ClaimRanks.Length)];
        }

        var chosen = _liar.Ai.Where(c => c.RankText == claim).Take(count).ToList();
        // 诈唬时用任意牌补齐
        foreach (var card in _liar.Ai.Where(c => !chosen.Contains(c)).ToList())
        {
            if (chosen.Count >= count) break;
            chosen.Add(card);
        }
        // 若仍不足，随机补
        while (chosen.Count < count && _liar.Ai.Count > chosen.Count)
        {
            var remaining = _liar.Ai.Where(c => !chosen.Contains(c)).ToList();
            if (remaining.Count == 0) break;
            chosen.Add(remaining[_rng.Next(remaining.Count)]);
        }

        foreach (var card in chosen) _liar.Ai.Remove(card);
        _liar.Pile.AddRange(chosen);
        _liar.LastCards.Clear();
        _liar.LastCards.AddRange(chosen);
        _liar.LastClaim = claim;
        _liar.LastByPlayer = false;
        _liar.AwaitingDecision = true;
        _liar.AiEmoji = truthful ? "😐" : "😄";
        _liar.Log = $"电脑（等级{eff:0.0}）盖下 {chosen.Count} 张，声明 {claim}。它的表情：{_liar.AiEmoji}。你可以质疑或继续。";

        if (_liar.Ai.Count == 0)
        {
            FinishLiar("电脑出完手牌");
            return;
        }
        LiarPage();
    }

    private void ResolveLiarChallenge(bool challengerIsPlayer)
    {
        if (_liar.LastCards.Count == 0)
        {
            Msg("现在没有可以质疑的出牌。");
            return;
        }

        var truthful = _liar.LastCards.All(c => c.RankText == _liar.LastClaim);
        var taker = challengerIsPlayer ? (truthful ? "你" : "电脑") : (truthful ? "电脑" : "你");
        var who = challengerIsPlayer ? "你质疑电脑" : "电脑质疑你";
        var flip = $"翻开：{string.Join(" ", _liar.LastCards)}（声明 {_liar.LastClaim}）";

        if (taker == "你")
        {
            _liar.Player.AddRange(_liar.Pile);
            _liar.Log = $"{who}。{flip}，声明{(truthful ? "属实" : "不实")}；你收走桌面 {_liar.Pile.Count} 张牌。";
        }
        else
        {
            _liar.Ai.AddRange(_liar.Pile);
            _liar.Log = $"{who}。{flip}，声明{(truthful ? "属实" : "不实")}；电脑收走桌面 {_liar.Pile.Count} 张牌。";
        }

        _liar.Pile.Clear();
        _liar.LastCards.Clear();
        _liar.LastClaim = "";
        _liar.AwaitingDecision = false;
        _liar.Selected.Clear();
        _liar.AiEmoji = taker == "你" ? "😞" : "😄";
        _liar.PlayerEmoji = taker == "你" ? "😐" : "😞";

        if (_liar.Player.Count == 0)
        {
            FinishLiar("你出完手牌");
            return;
        }
        if (_liar.Ai.Count == 0)
        {
            FinishLiar("电脑出完手牌");
            return;
        }
        LiarPage();
    }

    private void FinishLiar(string reason)
    {
        _liar.InRound = false;
        var playerWins = reason.StartsWith("你");
        _liar.Winner = playerWins
            ? $"{reason}，你获胜，赢得底池 ${_liar.Pot:N0}。"
            : $"{reason}，电脑获胜，你损失 ${_liar.PlayerCommitted:N0}。";
        SettleRound(_liar.Stake, playerWins ? _liar.Pot : 0, _liar.PlayerCommitted, $"骗子扑克：{reason} · {(playerWins ? $"赢得 ${_liar.Pot:N0}" : $"损失 ${_liar.PlayerCommitted:N0}")}");
        LiarPage();
    }
}
