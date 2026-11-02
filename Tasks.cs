using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TableGameRoom;

public partial class MainWindow
{
    /// <summary>任务页面：显示进行中/已完成的任务，可接取新任务。</summary>
    private void ShowTasks()
    {
        Base("任务中心");
        Header();

        if (_tasks.Count == 0) LoadTasks();

        _page.Children.Add(T("完成任务可以领取虚拟奖金。余额不足时，这里也是重新站起来的机会。", 15, Muted));
        _page.Children.Add(T($"当前余额：{( _mode == "智能" ? "∞（无限模式）" : $"${_balance:N0}")}", 16, Accent));

        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 4) };
        actions.Children.Add(B("接取新任务", () => AddTask()));
        actions.Children.Add(B("返回游戏大厅", () => ShowLobby()));
        _page.Children.Add(actions);

        // 未领取奖励（理论上已自动发放，这里做兜底展示）
        var active = _tasks.Where(t => !t.Claimed).ToList();
        var claimed = _tasks.Where(t => t.Claimed).ToList();

        if (active.Count == 0)
        {
            _page.Children.Add(T("当前没有进行中的任务，点击「接取新任务」生成一个。", 16, Muted));
        }

        foreach (var task in active)
        {
            _page.Children.Add(TaskCard(task));
        }

        if (claimed.Count > 0)
        {
            _page.Children.Add(T("已领取奖励的任务", 17));
            foreach (var task in claimed.TakeLast(8).Reverse())
            {
                var line = T($"✓ {task.Title} · 已奖励 ${task.Reward:N0}", 14, Muted);
                _page.Children.Add(line);
            }
        }
    }

    private Border TaskCard(GameTask task)
    {
        var stack = new StackPanel();

        var head = new WrapPanel();
        head.Children.Add(T(task.Completed ? "✓ " : "● ", 18, task.Completed ? Accent : Muted));
        head.Children.Add(T(task.Title, 20));
        stack.Children.Add(head);

        stack.Children.Add(T(task.Detail, 14, Muted));

        // 进度条
        var ratio = task.Target <= 0 ? 0 : Math.Clamp((double)task.Progress / task.Target, 0, 1);
        var barWidth = 420.0;
        var barBg = new Border
        {
            Width = barWidth,
            Height = 12,
            Background = new SolidColorBrush(Color.FromRgb(226, 238, 246)),
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(2, 6, 2, 2)
        };
        var barFill = new Border
        {
            Width = Math.Max(0, barWidth * ratio),
            Height = 12,
            Background = task.Completed ? Accent : new SolidColorBrush(Color.FromRgb(120, 180, 120)),
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        barBg.Child = barFill;
        stack.Children.Add(barBg);

        var progressText = task.Kind is TaskKind.NetProfit or TaskKind.NetLoss or TaskKind.WinBig
            ? $"进度：${task.Progress:N0} / ${task.Target:N0}"
            : $"进度：{task.Progress} / {task.Target}";
        stack.Children.Add(T($"{progressText}    ·    完成奖励 ${task.Reward:N0}", 14, Accent));

        return new Border
        {
            Margin = new Thickness(0, 8, 0, 4),
            Padding = new Thickness(20, 14, 20, 14),
            Background = task.Completed
                ? new SolidColorBrush(Color.FromRgb(232, 246, 238))
                : PanelBrush,
            BorderBrush = task.Completed ? Accent : PanelBorder,
            BorderThickness = new Thickness(task.Completed ? 2 : 1),
            CornerRadius = new CornerRadius(18),
            Child = stack
        };
    }

    private void AddTask()
    {
        if (_tasks.Count(t => !t.Claimed) >= 3)
        {
            Msg("同时最多进行 3 个任务，先完成或清理现有任务。");
            return;
        }
        var task = TaskSystem.Generate(_tierIndex, _tasks);
        _tasks.Add(task);
        SaveTasks();
        ShowTasks();
    }

    /// <summary>余额不足时，引导玩家去做任务。</summary>
    private bool CheckBankrupt()
    {
        if (_mode != "存档") return false;
        // 余额低于最低档位下限即视为无法继续
        var lowest = BetTiers[0].Min;
        if (_balance >= lowest) return false;

        var result = ThemeDialog.ShowConfirm(this,
            $"当前余额 ${_balance:N0} 已不足以继续下注（最低 ${lowest:N0}）。\n\n" +
            "去任务中心接个任务赚点奖金，完成即可领取虚拟奖励。是否前往？",
            "余额不足");

        if (result) ShowTasks();
        return true;
    }
}
