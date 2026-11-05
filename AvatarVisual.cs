using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TableGameRoom;

/// <summary>
/// 座位头像 —— 从安卓端 AvatarVisual 移植过来。
///
/// 为什么不用 emoji 字符：
///   Windows 上不同系统的 emoji 字体渲染差异很大，而且座位卡片里 emoji 会
///   随字号变化得很怪。这里用几何图形画一张固定的脸，跨平台外观完全一致。
///
/// 四种情绪：Neutral（平静）、Happy（开心）、Sad（失落）、Angry（恼怒）。
/// </summary>
public static class AvatarVisual
{
    public enum Mood { Neutral, Happy, Sad, Angry }

    /// <summary>头像调色板 —— 每个座位一个颜色，方便一眼区分。</summary>
    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x5B, 0x9F, 0xD0),  // 蓝
        Color.FromRgb(0x5F, 0xA8, 0x5F),  // 绿
        Color.FromRgb(0xC5, 0x91, 0x27),  // 金
        Color.FromRgb(0x9B, 0x6B, 0xC4),  // 紫
        Color.FromRgb(0xC0, 0x6B, 0x5A),  // 赭红
        Color.FromRgb(0x4A, 0x9E, 0x9E)   // 青
    ];

    /// <summary>emoji 字符 → 情绪（兼容原有的表情字段）。</summary>
    public static Mood FromEmoji(string? emoji) => emoji switch
    {
        "😄" => Mood.Happy,
        "😞" => Mood.Sad,
        "😠" => Mood.Angry,
        _ => Mood.Neutral
    };

    public static string MoodLabel(Mood mood) => mood switch
    {
        Mood.Happy => "开心",
        Mood.Sad => "失落",
        Mood.Angry => "恼怒",
        _ => "平静"
    };

    /// <summary>创建一个头像（圆形，直径 size）。</summary>
    public static FrameworkElement Create(
        string emoji = "😐",
        int seatIndex = 0,
        bool isPlayer = false,
        double size = 40) =>
        Create(FromEmoji(emoji), seatIndex, isPlayer, size);

    public static FrameworkElement Create(
        Mood mood,
        int seatIndex = 0,
        bool isPlayer = false,
        double size = 40)
    {
        var baseColor = isPlayer ? Color.FromRgb(0x5B, 0x9F, 0xD0) : Palette[Math.Abs(seatIndex) % Palette.Length];

        var canvas = new Canvas
        {
            Width = size,
            Height = size,
            ToolTip = $"表情：{MoodLabel(mood)}"
        };

        var r = size * 0.5;

        // 外发光（玩家自己）
        if (isPlayer)
        {
            canvas.Children.Add(new Ellipse
            {
                Width = size, Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(40, baseColor.R, baseColor.G, baseColor.B))
            });
        }

        // 主体圆
        var face = new Ellipse
        {
            Width = size, Height = size,
            Fill = new LinearGradientBrush(
                Lighten(baseColor, 0.35), baseColor, 90),
            Stroke = Brushes.White,
            StrokeThickness = Math.Max(1.5, size * 0.05)
        };
        canvas.Children.Add(face);

        // 五官（按比例缩放，小尺寸也看得清）
        var ink = new SolidColorBrush(Color.FromRgb(0x24, 0x2E, 0x35));
        var cx = r;
        var cy = r;

        var eyeR = r * 0.11;
        var eyeX = r * 0.36;
        var eyeY = cy - r * 0.18;

        if (mood == Mood.Happy)
        {
            // 眯眼：两段短横
            AddLine(canvas, ink, cx - eyeX - r * 0.14, eyeY, cx - eyeX + r * 0.14, eyeY, r * 0.09);
            AddLine(canvas, ink, cx + eyeX - r * 0.14, eyeY, cx + eyeX + r * 0.14, eyeY, r * 0.09);
        }
        else
        {
            AddDot(canvas, ink, cx - eyeX, eyeY, eyeR);
            AddDot(canvas, ink, cx + eyeX, eyeY, eyeR);
        }

        // 怒眉
        if (mood == Mood.Angry)
        {
            var by = eyeY - r * 0.26;
            AddLine(canvas, ink, cx - eyeX - r * 0.15, by - r * 0.05, cx - eyeX + r * 0.15, by + r * 0.05, r * 0.08);
            AddLine(canvas, ink, cx + eyeX - r * 0.15, by + r * 0.05, cx + eyeX + r * 0.15, by - r * 0.05, r * 0.08);
        }

        // 嘴：用一串小圆点拼成弧线
        var mouthY = cy + r * 0.34;
        var mouthW = r * 0.52;
        var curve = r * 0.20;

        const int dots = 11;
        var dotR = r * 0.055;

        for (var i = 0; i < dots; i++)
        {
            var t = i / (double)(dots - 1);
            var x = cx - mouthW + mouthW * 2 * t;
            var s = 2 * t - 1;

            double y = mood switch
            {
                Mood.Happy => mouthY + curve * (1 - s * s),
                Mood.Sad or Mood.Angry => mouthY - curve * (1 - s * s),
                _ => mouthY
            };

            AddDot(canvas, ink, x, y, dotR);
        }

        return canvas;
    }

    private static void AddDot(Canvas canvas, Brush fill, double x, double y, double r)
    {
        var dot = new Ellipse { Width = r * 2, Height = r * 2, Fill = fill };
        Canvas.SetLeft(dot, x - r);
        Canvas.SetTop(dot, y - r);
        canvas.Children.Add(dot);
    }

    private static void AddLine(Canvas canvas, Brush stroke, double x1, double y1, double x2, double y2, double width)
    {
        var line = new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = stroke,
            StrokeThickness = width,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        canvas.Children.Add(line);
    }

    private static Color Lighten(Color c, double f) =>
        Color.FromRgb(
            (byte)(c.R + (255 - c.R) * f),
            (byte)(c.G + (255 - c.G) * f),
            (byte)(c.B + (255 - c.B) * f));
}
