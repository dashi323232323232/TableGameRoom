using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TableGameRoom;

/// <summary>一张扑克牌。Rank 使用 2..14，其中 14=A、13=K、12=Q、11=J。</summary>
public sealed record Card(int Rank, char Suit)
{
    public static readonly char[] Suits = ['♠', '♥', '♦', '♣'];

    public string RankText => Rank switch
    {
        14 => "A",
        13 => "K",
        12 => "Q",
        11 => "J",
        _ => Rank.ToString()
    };

    public bool IsRed => Suit is '♥' or '♦';

    public override string ToString() => RankText + Suit;
}

public static class Deck
{
    public static List<Card> New(Random rng)
    {
        var cards = new List<Card>(52);
        foreach (var suit in Card.Suits)
        {
            for (var v = 2; v <= 14; v++) cards.Add(new Card(v, suit));
        }

        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        return cards;
    }
}

/// <summary>五张牌牌型评分，分数越大牌型越强。</summary>
public static class PokerScore
{
    private static readonly string[] Names =
    [
        "高牌", "对子", "两对", "三条", "顺子", "同花", "葫芦", "四条", "同花顺"
    ];

    private const long Base = 100_000_000_000L;

    public static int Category(long score) => (int)(score / Base);

    public static string Name(long score) => Names[Category(score)];

    public static long Of(IReadOnlyList<Card> five)
    {
        var ranks = five.Select(c => c.Rank).OrderByDescending(x => x).ToArray();
        var flush = five.All(c => c.Suit == five[0].Suit);
        var distinct = ranks.Distinct().ToArray();
        var straight = distinct.Length == 5 && distinct[0] - distinct[4] == 4;
        if (!straight && ranks.SequenceEqual(new[] { 14, 5, 4, 3, 2 })) straight = true;

        var groups = ranks.GroupBy(x => x)
                          .OrderByDescending(g => g.Count())
                          .ThenByDescending(g => g.Key)
                          .ToArray();

        int category;
        int[] tie;

        if (straight && flush)
        {
            category = 8;
            tie = [straightHigh(ranks)];
        }
        else if (groups[0].Count() == 4)
        {
            category = 7;
            tie = [groups[0].Key, groups[1].Key];
        }
        else if (groups[0].Count() == 3 && groups[1].Count() == 2)
        {
            category = 6;
            tie = [groups[0].Key, groups[1].Key];
        }
        else if (flush)
        {
            category = 5;
            tie = ranks;
        }
        else if (straight)
        {
            category = 4;
            tie = [straightHigh(ranks)];
        }
        else if (groups[0].Count() == 3)
        {
            category = 3;
            tie = [groups[0].Key, groups[1].Key, groups[2].Key];
        }
        else if (groups[0].Count() == 2 && groups[1].Count() == 2)
        {
            category = 2;
            tie = [groups[0].Key, groups[1].Key, groups[2].Key];
        }
        else if (groups[0].Count() == 2)
        {
            category = 1;
            tie = [groups[0].Key, groups[1].Key, groups[2].Key, groups[3].Key];
        }
        else
        {
            category = 0;
            tie = ranks;
        }

        long score = category * Base;
        for (var i = 0; i < 5; i++)
        {
            var v = i < tie.Length ? tie[i] : 0;
            score += v * (long)Math.Pow(100, 4 - i);
        }

        return score;

        static int straightHigh(int[] r) => r[0] == 14 && r[4] == 2 ? 5 : r[0];
    }

    /// <summary>从 5~7 张牌里选出最好的五张。</summary>
    public static long Best(IReadOnlyList<Card> cards)
    {
        if (cards.Count < 5) return 0;
        long best = -1;
        var n = cards.Count;
        for (var a = 0; a < n - 4; a++)
            for (var b = a + 1; b < n - 3; b++)
                for (var c = b + 1; c < n - 2; c++)
                    for (var d = c + 1; d < n - 1; d++)
                        for (var e = d + 1; e < n; e++)
                        {
                            long s = Of([cards[a], cards[b], cards[c], cards[d], cards[e]]);
                            if (s > best) best = s;
                        }

        return best;
    }
}

/// <summary>炸金花三张牌牌型评分。</summary>
public static class ThreeScore
{
    private static readonly string[] Names =
    [
        "单张", "对子", "同花", "顺子", "豹子", "同花顺"
    ];

    public static string Name(long score) => Names[(int)(score / 1_000_000L)];

    public static long Of(IReadOnlyList<Card> three)
    {
        var ranks = three.Select(c => c.Rank).OrderByDescending(x => x).ToArray();
        var flush = three.All(c => c.Suit == three[0].Suit);
        var distinct = ranks.Distinct().ToArray();
        var straight = distinct.Length == 3 && distinct[0] - distinct[2] == 2;
        if (!straight && ranks.SequenceEqual(new[] { 14, 3, 2 })) straight = true;

        int category;
        int[] tie;

        if (flush && straight)
        {
            category = 5;
            tie = [straightHigh(ranks)];
        }
        else if (distinct.Length == 1)
        {
            category = 4;
            tie = [ranks[0]];
        }
        else if (straight)
        {
            category = 3;
            tie = [straightHigh(ranks)];
        }
        else if (flush)
        {
            category = 2;
            tie = ranks;
        }
        else if (distinct.Length == 2)
        {
            category = 1;
            var pair = ranks.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;
            var kicker = ranks.First(x => x != pair);
            tie = [pair, kicker];
        }
        else
        {
            category = 0;
            tie = ranks;
        }

        long score = category * 1_000_000L;
        for (var i = 0; i < 3; i++) score += (i < tie.Length ? tie[i] : 0) * (long)Math.Pow(100, 2 - i);
        return score;

        static int straightHigh(int[] r) => r[0] == 14 && r[2] == 2 ? 3 : r[0];
    }
}

/// <summary>结构化历史记录：每局一次，带时间戳与唯一id。</summary>
public sealed record GameHistoryEntry(
    string Id,
    DateTime Timestamp,
    string GameKey,
    string GameName,
    int Stake,
    int Net,
    string Summary);

public static class ModeDisplay
{
    public static string ToDisplay(string internalMode) => internalMode == "智能" ? "无限模式" : "普通模式";
    public static string FileName(string internalMode) => internalMode == "存档" ? "progress.json" : "custom-progress.json";
    public static DateTime HistoryCutoff => DateTime.Today.AddMonths(-3).Date;
    public static bool IsInWindow(DateTime ts) => ts.Date >= HistoryCutoff;
    public static string GameName(string key) => key switch
    {
        "poker" => "德州扑克",
        "three" => "炸金花",
        "blackjack" => "21点",
        "roulette" => "欧洲轮盘",
        "liar" => "骗子扑克",
        _ => key
    };
}

/// <summary>统一的紧凑牌面控件 — 阶段2整体缩小，紧凑可读。</summary>
public static class CardView
{
    public const double Width = 26;
    public const double Height = 36;

    public static Border Create(Card? card, bool hidden = false, bool compact = false)
    {
        var w = compact ? 22 : Width;
        var h = compact ? 30 : Height;
        var border = new Border
        {
            Width = w,
            Height = h,
            Margin = new Thickness(compact ? 1 : 1.5, 0, compact ? 1 : 1.5, 0),
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(201, 214, 218)),
            Background = new SolidColorBrush(Colors.White)
        };

        if (hidden || card is null)
        {
            border.Background = new SolidColorBrush(Color.FromRgb(207, 226, 238));
            border.Child = new TextBlock
            {
                Text = "✦",
                FontSize = 9,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(91, 159, 208))
            };
            return border;
        }

        var color = card.IsRed
            ? new SolidColorBrush(Color.FromRgb(192, 57, 43))
            : new SolidColorBrush(Color.FromRgb(32, 42, 48));

        var stack = new StackPanel
        {
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        stack.Children.Add(new TextBlock
        {
            Text = card.RankText,
            FontSize = compact ? 9 : 10,
            FontWeight = System.Windows.FontWeights.SemiBold,
            Foreground = color,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = card.Suit.ToString(),
            FontSize = compact ? 9 : 10,
            Foreground = color,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        });

        border.Child = stack;
        return border;
    }
}

/// <summary>环桌布局辅助：椭圆锚点与座位尺寸。</summary>
public static class TableRing
{
    // 座位视觉尺寸：比之前显著缩小
    public const double SeatWidth = 132;
    public const double SeatMinWidth = 110;

    /// <summary>根据电脑数量返回归一化椭圆坐标 (x,y) 0..1，椭圆中心0.5,0.5，桌大座位小。</summary>
    public static Point[] ComputerPositions(int computerCount)
    {
        // 预设归一化坐标，保证视觉上环绕椭圆上弧/左右/下侧，不与中央重叠
        return computerCount switch
        {
            1 => [new Point(0.50, 0.12)],
            2 => [new Point(0.22, 0.18), new Point(0.78, 0.18)],
            3 => [new Point(0.18, 0.30), new Point(0.50, 0.10), new Point(0.82, 0.30)],
            4 => [new Point(0.16, 0.42), new Point(0.32, 0.12), new Point(0.68, 0.12), new Point(0.84, 0.42)],
            5 => [new Point(0.14, 0.48), new Point(0.26, 0.14), new Point(0.50, 0.08), new Point(0.74, 0.14), new Point(0.86, 0.48)],
            _ => [new Point(0.12, 0.50), new Point(0.24, 0.14), new Point(0.50, 0.08), new Point(0.76, 0.14), new Point(0.88, 0.50), new Point(0.18, 0.78)] // 6; keep lower-middle exclusive to the player
        };
    }

    public static Point PlayerPos => new(0.50, 0.84);

    /// <summary>按容器实际尺寸计算像素坐标。</summary>
    public static Point ToPixels(Point norm, double w, double h, double seatW, double seatH)
        => new(norm.X * w - seatW / 2, norm.Y * h - seatH / 2);

    /// <summary>创建带椭圆背景的响应式容器（Viewbox+Canvas），随尺寸缩放。</summary>
    public static Grid BuildOvalContainer(double designW, double designH, out Canvas canvas, out Viewbox viewbox)
    {
        canvas = new Canvas { Width = designW, Height = designH, Background = Brushes.Transparent };
        // 椭圆桌
        var ellipse = new Ellipse
        {
            Width = designW * 0.78,
            Height = designH * 0.62,
            Fill = new SolidColorBrush(Color.FromRgb(226, 244, 236)),
            Stroke = new SolidColorBrush(Color.FromRgb(169, 215, 204)),
            StrokeThickness = 2
        };
        Canvas.SetLeft(ellipse, designW * 0.11);
        Canvas.SetTop(ellipse, designH * 0.14);
        canvas.Children.Add(ellipse);
        // 内圈装饰
        var inner = new Ellipse
        {
            Width = designW * 0.74,
            Height = designH * 0.58,
            Stroke = new SolidColorBrush(Color.FromRgb(190, 225, 210)),
            StrokeThickness = 1,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(inner, designW * 0.13);
        Canvas.SetTop(inner, designH * 0.16);
        canvas.Children.Add(inner);

        viewbox = new Viewbox { Stretch = Stretch.Uniform, Child = canvas, StretchDirection = StretchDirection.Both };
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        grid.Children.Add(viewbox);
        return grid;
    }
}
