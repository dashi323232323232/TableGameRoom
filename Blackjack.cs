using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TableGameRoom;

public partial class MainWindow
{
    private List<Card> _bjPlayer = [];
    private List<Card> _bjDealer = [];
    private List<Card> _deck = [];
    private int _bjStake;
    private bool _bjDoubled;
    private bool _bjOver;
    private bool _bjInRound;
    private string _bjOutcome = "";

    private void BlackjackPage()
    {
        Base("21 点");
        Header();

        // ---------- 牌桌（绿色椭圆，庄家在上、玩家在下）----------
        var table = new Grid
        {
            Height = 400,
            Width = 760,
            Margin = new Thickness(4, 6, 4, 10),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // 椭圆桌布
        var felt = new Border
        {
            Width = 740,
            Height = 380,
            CornerRadius = new CornerRadius(190),
            Background = new SolidColorBrush(Color.FromRgb(226, 244, 236)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(169, 215, 204)),
            BorderThickness = new Thickness(3),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        table.Children.Add(felt);

        // 内圈装饰线
        var innerLine = new Border
        {
            Width = 700,
            Height = 340,
            CornerRadius = new CornerRadius(170),
            BorderBrush = new SolidColorBrush(Color.FromRgb(190, 225, 210)),
            BorderThickness = new Thickness(1.5),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        table.Children.Add(innerLine);

        // ---------- 内容布局：庄家 / 中间 / 玩家 ----------
        var layout = new Grid { Margin = new Thickness(30, 16, 30, 16) };
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ---- 庄家区 ----
        var dealerPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        dealerPanel.Children.Add(T("庄 家", 13, Muted));
        dealerPanel.Children.Add(BlackjackHand(_bjDealer, hideSecond: _bjInRound && !_bjOver, big: false));
        if (_bjDealer.Count > 0)
        {
            var dealerScore = _bjInRound && !_bjOver
                ? "? 点（暗牌）"
                : $"{Points(_bjDealer)} 点";
            dealerPanel.Children.Add(T(dealerScore, 16, Accent));
        }
        Grid.SetRow(dealerPanel, 0);
        layout.Children.Add(dealerPanel);

        // ---- 中间：底池 / 状态 ----
        var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        if (_bjInRound || _bjOver)
        {
            center.Children.Add(T($"底池 ${_bjStake:N0}", 15, Accent));
            // 筹码可视化
            center.Children.Add(ChipVisual.Create(Math.Min(_bjStake, 5000)));
        }
        if (_bjOutcome.Length > 0)
        {
            center.Children.Add(T(_bjOutcome, 18));
        }
        Grid.SetRow(center, 1);
        layout.Children.Add(center);

        // ---- 玩家区 ----
        var playerPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        playerPanel.Children.Add(BlackjackHand(_bjPlayer, hideSecond: false, big: true));
        if (_bjPlayer.Count > 0)
        {
            var score = Points(_bjPlayer);
            var scoreText = score > 21 ? $"{score} 点 · 爆牌" : score == 21 ? $"{score} 点 · Blackjack!" : $"{score} 点";
            playerPanel.Children.Add(T(scoreText, 17, score > 21 ? new SolidColorBrush(Color.FromRgb(192, 57, 43)) : Ink));
        }
        else
        {
            playerPanel.Children.Add(T("你", 13, Muted));
        }
        Grid.SetRow(playerPanel, 2);
        layout.Children.Add(playerPanel);

        table.Children.Add(layout);
        _page.Children.Add(table);

        // ---------- 说明与操作 ----------
        if (!_bjInRound)
        {
            var input = StakeBox();
            // StakeBox 已经把输入框和快捷金额加到页面了，这里只补「开始新局」按钮
            var startRow = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            startRow.Children.Add(B("开始新局", () =>
            {
                if (!int.TryParse(input.Text.Trim(), out var stake) || stake <= 0)
                {
                    Msg("请输入有效的下注金额。");
                    return;
                }
                if (!TryStake(stake, out var error))
                {
                    Msg(error);
                    return;
                }
                _bjStake = stake;
                _deck = Deck.New(_rng);
                _bjPlayer = [Draw(), Draw()];
                _bjDealer = [Draw(), Draw()];
                _bjDoubled = false;
                _bjOver = false;
                _bjInRound = true;
                _roundSettled = false;
                _bjOutcome = "";
                _result = "";
                if (Points(_bjPlayer) == 21)
                {
                    FinishBlackjack("Blackjack");
                    return;
                }
                BlackjackPage();
            }));
            _page.Children.Add(startRow);
        }
        else if (!_bjOver)
        {
            var actions = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            actions.Children.Add(B("要牌", () =>
            {
                _bjPlayer.Add(Draw());
                if (Points(_bjPlayer) > 21) FinishBlackjack("玩家爆牌");
                else BlackjackPage();
            }));
            actions.Children.Add(B("停牌", async () =>
            {
                await DealerPlayAsync();
                var p = Points(_bjPlayer);
                var d = Points(_bjDealer);
                FinishBlackjack(d > 21 ? "庄家爆牌" : p > d ? "玩家胜" : p == d ? "平局" : "庄家胜");
            }));
            var doubleButton = B("双倍下注", async () =>
            {
                if (_bjPlayer.Count != 2 || _bjDoubled)
                {
                    Msg("只有初始两张牌且未双倍时才能双倍下注。");
                    return;
                }
                if (_mode == "存档" && _balance < _bjStake * 2)
                {
                    Msg($"余额不足：双倍下注需要 ${_bjStake * 2:N0}，当前余额 ${_balance:N0}。");
                    return;
                }
                var doubled = _bjStake * 2;
                if (Tier.Max is not null && doubled > Tier.Max.Value)
                {
                    Msg($"当前档位单次下注上限 ${Tier.Max.Value:N0}，双倍后为 ${doubled:N0}。");
                    return;
                }
                _bjStake = doubled;
                _bjDoubled = true;
                _bjPlayer.Add(Draw());
                if (Points(_bjPlayer) > 21)
                {
                    FinishBlackjack("玩家爆牌（双倍）");
                    return;
                }
                await DealerPlayAsync();
                var p = Points(_bjPlayer);
                var d = Points(_bjDealer);
                FinishBlackjack(d > 21 ? "庄家爆牌（双倍）" : p > d ? "玩家胜（双倍）" : p == d ? "平局（双倍）" : "庄家胜（双倍）");
            });
            actions.Children.Add(doubleButton);
            _page.Children.Add(actions);
        }
        else
        {
            var again = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            again.Children.Add(B("再来一局", () =>
            {
                _bjInRound = false;
                _bjOver = false;
                _bjPlayer = [];
                _bjDealer = [];
                _bjOutcome = "";
                BlackjackPage();
            }));
            _page.Children.Add(again);
        }

        _page.Children.Add(T("庄家至少补牌到 17 点 · A 可按 1 或 11 点计算 · 目标是接近 21 点且不爆牌", 13, Muted));
    }

    /// <summary>21 点专用牌面（比通用 CardView 更大，适合赌桌展示）。</summary>
    private UIElement BlackjackHand(IReadOnlyList<Card> cards, bool hideSecond, bool big)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 6)
        };

        if (cards.Count == 0)
        {
            row.Children.Add(T("—", 14, Muted));
            return row;
        }

        var w = big ? 62.0 : 52.0;
        var h = big ? 88.0 : 74.0;

        for (var i = 0; i < cards.Count; i++)
        {
            var hidden = hideSecond && i == 1;
            row.Children.Add(PlayingCardView(cards[i], hidden, w, h));
        }
        return row;
    }

    /// <summary>绘制一张大牌（白底圆角，红黑花色）。</summary>
    private static Border PlayingCardView(Card? card, bool hidden, double width, double height)
    {
        var border = new Border
        {
            Width = width,
            Height = height,
            Margin = new Thickness(4, 0, 4, 0),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1.2),
            BorderBrush = new SolidColorBrush(Color.FromRgb(201, 214, 218)),
            Background = Brushes.White
        };

        if (hidden || card is null)
        {
            border.Background = new SolidColorBrush(Color.FromRgb(207, 226, 238));
            border.Child = new TextBlock
            {
                Text = "✦",
                FontSize = width * 0.32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(91, 159, 208))
            };
            return border;
        }

        var red = card.IsRed;
        var brush = red
            ? new SolidColorBrush(Color.FromRgb(192, 57, 43))
            : new SolidColorBrush(Color.FromRgb(32, 42, 48));

        var grid = new Grid();
        // 左上角：点数 + 花色
        var topLeft = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(4, 2, 0, 0)
        };
        topLeft.Children.Add(new TextBlock
        {
            Text = card.RankText,
            FontSize = width * 0.26,
            FontWeight = FontWeights.Bold,
            Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        topLeft.Children.Add(new TextBlock
        {
            Text = card.Suit.ToString(),
            FontSize = width * 0.22,
            Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        grid.Children.Add(topLeft);

        // 中央大花色
        grid.Children.Add(new TextBlock
        {
            Text = card.Suit.ToString(),
            FontSize = width * 0.46,
            Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.85
        });

        border.Child = grid;
        return border;
    }

    // ---------- 庄家补牌 ----------

    private void DealerPlay()
    {
        while (Points(_bjDealer) < 17) _bjDealer.Add(Draw());
    }

    /// <summary>庄家逐步补牌：每补一张先展示，再停顿随机时长。</summary>
    private async Task DealerPlayAsync()
    {
        var draws = 0;
        while (Points(_bjDealer) < 17)
        {
            _bjDealer.Add(Draw());
            draws++;
            BlackjackPage();
            await AiThink(draws + 1, _bjStake, Tier.Max, "庄家正在补牌…");
        }
    }

    private void FinishBlackjack(string outcome)
    {
        if (_bjOver) return;
        _bjOver = true;
        _bjInRound = false;
        _bjOutcome = outcome;
        var p = Points(_bjPlayer);
        var d = Points(_bjDealer);
        var net = outcome switch
        {
            "平局" or "平局（双倍）" => 0,
            "玩家胜" or "玩家胜（双倍）" => _bjStake,
            "庄家爆牌" or "庄家爆牌（双倍）" => _bjStake,
            "Blackjack" => _bjStake,
            _ => -_bjStake
        };
        Settle(_bjStake, net, $"21点：{outcome} · 玩家 {p} 点 · 庄家 {d} 点 · 下注 ${_bjStake:N0}");
        BlackjackPage();
    }

    private static int Points(IReadOnlyList<Card> cards)
    {
        var sum = 0;
        var aces = 0;
        foreach (var card in cards)
        {
            if (card.Rank == 14)
            {
                sum += 11;
                aces++;
            }
            else
            {
                sum += Math.Min(card.Rank, 10);
            }
        }
        while (sum > 21 && aces > 0)
        {
            sum -= 10;
            aces--;
        }
        return sum;
    }

    private Card Draw()
    {
        if (_deck.Count == 0) _deck = Deck.New(_rng);
        var card = _deck[^1];
        _deck.RemoveAt(_deck.Count - 1);
        return card;
    }
}
