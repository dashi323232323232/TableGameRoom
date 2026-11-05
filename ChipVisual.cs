using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TableGameRoom;

public static class ChipVisual
{
    private static readonly (int Value, Color Color)[] Denominations =
    [
        (1000, Color.FromRgb(43, 91, 145)),
        (500, Color.FromRgb(52, 132, 91)),
        (100, Color.FromRgb(185, 53, 48)),
        (25, Color.FromRgb(197, 145, 39)),
        (5, Color.FromRgb(91, 99, 106)),
        (1, Color.FromRgb(231, 231, 219))
    ];

    /// <summary>
    /// 创建一堆筹码。
    ///
    /// 关键：**不跨面额凑数**。之前用贪心法把 500 直接拆成 1 枚 500，
    /// 5 个人各下 100 的底池 $500 看起来像「一个 500 的筹码」，很怪。
    ///
    /// 现在以「基准面额」为单位：5×100 就是 5 枚 100，堆成 3 摞。
    /// 基准面额取不超过金额的最大面额，且保证枚数不至于爆炸。
    /// </summary>
    public static FrameworkElement Create(int amount)
    {
        var pile = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Height = 34 };
        if (amount <= 0) return pile;

        // 选出基准面额：从大到小找第一个「枚数合理」的
        var baseValue = 1;
        foreach (var (value, _) in Denominations)
        {
            if (value > amount) continue;
            // 用这个面额表示时，枚数不超过 30（3 摞 × 10 枚）就用它
            if (amount / value <= 30)
            {
                baseValue = value;
                break;
            }
        }

        var baseColor = ColorFor(baseValue);
        var pieces = Math.Max(1, amount / baseValue);
        var remainder = amount % baseValue;

        var stackIndex = 0;
        var visibleStacks = (int)Math.Min(3, pieces);
        var eachCount = pieces / visibleStacks;
        var extra = pieces % visibleStacks;

        for (var i = 0; i < visibleStacks; i++)
        {
            var count = eachCount + (i < extra ? 1 : 0);
            if (count <= 0) continue;

            var stack = MakeStack(baseValue, baseColor, count);
            stack.Margin = new Thickness(stackIndex++ == 0 ? 0 : -8, 0, 0, 0);
            pile.Children.Add(stack);
        }

        // 余数用一枚更小的面额补齐，保持总额精确
        if (remainder > 0)
        {
            var small = SmallestFor(remainder);
            var stack = MakeStack(small.Value, small.Color, Math.Max(1, remainder / small.Value));
            stack.Margin = new Thickness(stackIndex++ == 0 ? 0 : -8, 0, 0, 0);
            pile.Children.Add(stack);
        }

        pile.ToolTip = $"精确合计 ${amount:N0}（{pieces} 枚 ${baseValue:N0}" +
                       (remainder > 0 ? $" + ${remainder:N0}" : "") + "）";
        return pile;
    }

    /// <summary>金额对应的颜色（找最接近的面额）。</summary>
    private static Color ColorFor(int value)
    {
        foreach (var (v, c) in Denominations)
            if (v == value) return c;
        return Color.FromRgb(91, 99, 106);
    }

    /// <summary>把余数落到不超过它的最大面额上。</summary>
    private static (int Value, Color Color) SmallestFor(int remainder)
    {
        foreach (var (value, color) in Denominations)
            if (value <= remainder) return (value, color);
        return (1, Color.FromRgb(231, 231, 219));
    }

    private static FrameworkElement MakeStack(int value, Color color, int count)
    {
        var stack = new Grid { Width = 28, Height = 34, VerticalAlignment = VerticalAlignment.Bottom, ToolTip = $"{count} 枚 · 每枚 ${value:N0}" };
        for (var i = Math.Min(count, 6) - 1; i >= 0; i--)
        {
            var chip = MakeChip(value, color);
            chip.Height = 23;
            chip.Margin = new Thickness(0, 2 + i * 1.2, 0, 0);
            chip.VerticalAlignment = VerticalAlignment.Top;
            Panel.SetZIndex(chip, i);
            stack.Children.Add(chip);
        }
        return stack;
    }

    private static FrameworkElement MakeChip(int value, Color color)
    {
        var chip = new Grid { Width = 31, Height = 24, VerticalAlignment = VerticalAlignment.Bottom, ToolTip = $"${value:N0}" };
        chip.Children.Add(new Ellipse { Margin = new Thickness(1, 5, 1, 0), Fill = new SolidColorBrush(Color.FromRgb(27, 34, 39)) });
        chip.Children.Add(new Ellipse { Margin = new Thickness(0, 1, 0, 5), Fill = new LinearGradientBrush(Color.FromRgb(255, 255, 255), Darken(color, 0.55), 90), Stroke = new SolidColorBrush(Darken(color, 0.7)), StrokeThickness = 1 });
        var face = new Ellipse { Margin = new Thickness(2, 0, 2, 7), Fill = new LinearGradientBrush(Lighten(color, 0.55), color, 90), Stroke = Brushes.White, StrokeThickness = 1.4 };
        chip.Children.Add(face);
        var ticks = new Grid { Margin = new Thickness(3, 0, 3, 7), IsHitTestVisible = false };
        for (var i = 0; i < 8; i++)
        {
            var tick = new Rectangle { Width = 2, Height = 4, Fill = Brushes.White, VerticalAlignment = VerticalAlignment.Top, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(i * 45) };
            ticks.Children.Add(tick);
        }
        chip.Children.Add(ticks);
        chip.Children.Add(new Ellipse { Margin = new Thickness(8, 3, 8, 10), Fill = new SolidColorBrush(color), Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), StrokeThickness = 1 });
        chip.Children.Add(new TextBlock { Text = value >= 1000 ? $"{value / 1000}K" : value.ToString(), FontSize = 6, FontWeight = FontWeights.Bold, Foreground = value == 1 ? Brushes.Black : Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0) });
        return chip;
    }

    private static Color Darken(Color c, double f) => Color.FromRgb((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));
    private static Color Lighten(Color c, double f) => Color.FromRgb((byte)(c.R + (255 - c.R) * f), (byte)(c.G + (255 - c.G) * f), (byte)(c.B + (255 - c.B) * f));
}
