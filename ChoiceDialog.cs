using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TableGameRoom;

/// <summary>
/// 通用选择框 / 输入框。
///
/// 桌面版之前只有「提示」和「确认」两种对话框，加注要选倍数、金额要自己填，
/// 都缺一个能列选项、能收输入的窗口。这里补上，风格和 ThemeDialog 保持一致。
/// </summary>
public static class ChoiceDialog
{
    /// <summary>弹出选项列表，返回选中索引；取消返回 -1。</summary>
    public static int Choose(Window owner, string title, IReadOnlyList<string> options)
    {
        var win = CreateWindow(owner, title, out var body);

        var result = -1;

        for (var i = 0; i < options.Count; i++)
        {
            var index = i;
            var btn = MakeButton(options[i]);
            btn.Click += (_, _) => { result = index; win.Close(); };
            body.Children.Add(btn);
        }

        var cancel = MakeButton("取消", subtle: true);
        cancel.Click += (_, _) => { result = -1; win.Close(); };
        body.Children.Add(cancel);

        win.ShowDialog();
        return result;
    }

    /// <summary>弹出输入框，返回输入内容；取消返回 null。</summary>
    public static string? AskText(Window owner, string title, string hint, string initial)
    {
        var win = CreateWindow(owner, title, out var body);

        if (!string.IsNullOrEmpty(hint))
        {
            body.Children.Add(new TextBlock
            {
                Text = hint,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(83, 99, 107)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
        }

        var box = new TextBox
        {
            Text = initial ?? "",
            FontSize = 18,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 14)
        };
        body.Children.Add(box);

        string? result = null;

        var ok = MakeButton("确定");
        ok.Click += (_, _) => { result = box.Text; win.Close(); };
        body.Children.Add(ok);

        var cancel = MakeButton("取消", subtle: true);
        cancel.Click += (_, _) => { result = null; win.Close(); };
        body.Children.Add(cancel);

        // 回车 = 确定
        box.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                result = box.Text;
                win.Close();
            }
        };

        win.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        win.ShowDialog();
        return result;
    }

    // ==================== 内部 ====================

    private static Window CreateWindow(Window owner, string title, out StackPanel body)
    {
        var win = new Window
        {
            Title = title,
            Owner = owner,
            Width = 380,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false,
            Background = Brushes.White
        };

        body = new StackPanel { Margin = new Thickness(20) };
        win.Content = body;

        return win;
    }

    private static Button MakeButton(string text, bool subtle = false)
    {
        var btn = new Button
        {
            Content = text,
            FontSize = 15,
            Padding = new Thickness(16, 10, 16, 10),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = subtle
                ? new SolidColorBrush(Color.FromRgb(239, 247, 252))
                : new SolidColorBrush(Color.FromRgb(91, 159, 208)),
            Foreground = subtle ? new SolidColorBrush(Color.FromRgb(32, 42, 48)) : Brushes.White,
            BorderThickness = new Thickness(0)
        };

        return btn;
    }
}
