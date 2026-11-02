using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace TableGameRoom;

public partial class MainWindow
{
    private enum HistoryMetric { Turnover, Count }
    private HistoryMetric _historyMetric;
    private StackPanel? _compactHistoryHost;

    private UIElement BuildHistoryPanelCore(bool compact = false)
    {
        var panel = new StackPanel { Margin = new Thickness(compact ? 4 : 18), MaxWidth = compact ? 288 : double.PositiveInfinity };
        if (compact) _compactHistoryHost = panel;
        RefreshHistoryPanel(panel, compact);
        return panel;
    }

    private void RefreshHistoryPanel(StackPanel panel, bool compact)
    {
        panel.Children.Clear();
        var today = DateTime.Today;
        var currentMonth = new DateTime(today.Year, today.Month, 1);
        if (_historyMonth > currentMonth) _historyMonth = currentMonth;
        if (_historyDate is DateTime selectedDate && selectedDate.Date > today) _historyDate = null;
        panel.Children.Add(T("日历历史", compact ? 14 : 18));
        if (!compact) panel.Children.Add(B("返回大厅", ShowLobby));
        var nav = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var previous = B("‹", () => { _historyMonth = _historyMonth.AddMonths(-1); RefreshHistoryPanel(panel, compact); });
        var next = B("›", () =>
        {
            if (_historyMonth >= currentMonth) return;
            _historyMonth = _historyMonth.AddMonths(1);
            RefreshHistoryPanel(panel, compact);
        });
        next.IsEnabled = _historyMonth < currentMonth;
        if (compact)
        {
            previous.Padding = next.Padding = new Thickness(8, 4, 8, 4);
            previous.Margin = next.Margin = new Thickness(1);
            previous.FontSize = next.FontSize = 12;
        }
        nav.Children.Add(previous);
        nav.Children.Add(T(_historyMonth.ToString("yyyy 年 MM 月"), compact ? 13 : 18));
        nav.Children.Add(next);
        panel.Children.Add(nav);
        var modes = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, compact ? 2 : 6),
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.White
        };
        var segments = new UniformGrid { Rows = 1, Columns = 2 };
        segments.Children.Add(HistorySegment("流水", HistoryMetric.Turnover, compact, true));
        segments.Children.Add(HistorySegment("次数", HistoryMetric.Count, compact, false));
        modes.Child = segments;
        panel.Children.Add(modes);
        RenderHistoryCalendar(panel, compact);
    }

    private Button HistorySegment(string label, HistoryMetric metric, bool compact, bool first)
    {
        var selected = _historyMetric == metric;
        var button = new Button
        {
            Content = label,
            MinWidth = compact ? 74 : 96,
            Padding = new Thickness(compact ? 6 : 12, compact ? 4 : 7, compact ? 6 : 12, compact ? 4 : 7),
            Margin = new Thickness(0),
            FontSize = compact ? 11 : 13,
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = selected ? Brushes.White : Ink,
            Background = selected ? Accent : Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = HistoryToolTip($"按{label}查看每日历史"),
            Template = HistorySegmentTemplate(first)
        };
        button.Click += (_, _) =>
        {
            _historyMetric = metric;
            var panel = FindHistoryPanel(button);
            RefreshHistoryPanel(panel, ReferenceEquals(panel, _compactHistoryHost));
        };
        return button;
    }

    private static ControlTemplate HistorySegmentTemplate(bool first)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "SegmentBorder";
        border.SetValue(Border.CornerRadiusProperty, first ? new CornerRadius(5, 0, 0, 5) : new CornerRadius(0, 5, 5, 0));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        template.VisualTree = border;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.82));
        template.Triggers.Add(hover);
        return template;
    }

    private static Border HistoryToolTip(string text)
    {
        var tip = new Border
        {
            Background = Brushes.White,
            BorderBrush = Accent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8),
            Child = new TextBlock { Text = text, Foreground = Ink, FontSize = 12, TextWrapping = TextWrapping.Wrap }
        };
        ToolTipService.SetInitialShowDelay(tip, 350);
        return tip;
    }

    private StackPanel FindHistoryPanel(DependencyObject element)
    {
        DependencyObject? current = element;
        while (current is not null && current is not StackPanel) current = VisualTreeHelper.GetParent(current);
        return current as StackPanel ?? _compactHistoryHost ?? throw new InvalidOperationException("History panel is not attached.");
    }

    private void RenderHistoryCalendar(StackPanel panel, bool compact)
    {
        var today = DateTime.Today;
        var history = LoadHistory().Where(x => x.Timestamp.Date <= today).ToArray();
        var byDay = history.Where(x => x.Timestamp.Year == _historyMonth.Year && x.Timestamp.Month == _historyMonth.Month)
            .GroupBy(x => x.Timestamp.Date).ToDictionary(g => g.Key, g => g.ToArray());
        var values = byDay.ToDictionary(x => x.Key, x => _historyMetric == HistoryMetric.Count
            ? (long)x.Value.Length
            : x.Value.Aggregate(0L, (sum, item) => sum + item.Stake));
        var maximum = values.Count == 0 ? 0L : values.Values.Max();
        var offset = (int)_historyMonth.DayOfWeek;
        var days = DateTime.DaysInMonth(_historyMonth.Year, _historyMonth.Month);
        var grid = new UniformGrid { Columns = 7, Margin = new Thickness(0, compact ? 2 : 6, 0, compact ? 2 : 6), HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var label in new[] { "日", "一", "二", "三", "四", "五", "六" }) grid.Children.Add(T(label, compact ? 10 : 13, Muted));
        for (var i = 0; i < offset; i++) grid.Children.Add(new Border());
        for (var day = 1; day <= days; day++)
        {
            var date = new DateTime(_historyMonth.Year, _historyMonth.Month, day);
            byDay.TryGetValue(date, out var records);
            records ??= [];
            var value = values.GetValueOrDefault(date);
            var enabled = date <= today;
            var cell = B(day.ToString(), () =>
            {
                if (date > DateTime.Today) return;
                _historyDate = date;
                RefreshHistoryPanel(panel, compact);
            });
            cell.IsEnabled = enabled;
            cell.Opacity = enabled ? 1 : 0.42;
            cell.Padding = new Thickness(0);
            cell.Margin = new Thickness(compact ? 0.5 : 2);
            cell.HorizontalAlignment = HorizontalAlignment.Stretch;
            cell.HorizontalContentAlignment = HorizontalAlignment.Center;
            cell.MinWidth = 0;
            cell.Width = double.NaN;
            cell.MinHeight = compact ? 24 : 40;
            cell.BorderBrush = _historyDate == date ? Accent : new SolidColorBrush(Color.FromRgb(185, 215, 240));
            cell.BorderThickness = new Thickness(1);
            cell.Background = !enabled || value == 0 ? Brushes.White : HeatColor(value, maximum);
            cell.Template = CalendarCellTemplate(compact);
            cell.ToolTip = enabled ? HistoryToolTip($"{date:yyyy年MM月dd日}\n对局 {records.Length} 次\n流水 ${records.Aggregate(0L, (s, x) => s + x.Stake):N0}\n收入 ${records.Where(x => x.Net > 0).Aggregate(0L, (s, x) => s + x.Net):N0}\n消耗 ${records.Where(x => x.Net < 0).Aggregate(0L, (s, x) => s + Math.Abs((long)x.Net)):N0}") : null;
            grid.Children.Add(cell);
        }
        panel.Children.Add(grid);
        var legend = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, compact ? 2 : 6) };
        legend.Children.Add(T("0", compact ? 9 : 11, Muted));
        foreach (var fraction in new[] { 0.25, 0.5, 0.75, 1.0 })
        {
            var sampleValue = maximum == 0 ? 0 : Math.Max(1, (long)Math.Round(maximum * fraction));
            legend.Children.Add(new Border { Width = compact ? 12 : 16, Height = compact ? 9 : 12, Margin = new Thickness(2, 2, 2, 0), Background = sampleValue == 0 ? Brushes.White : HeatColor(sampleValue, maximum), BorderBrush = PanelBorder, BorderThickness = new Thickness(0.5) });
        }
        legend.Children.Add(T(maximum == 0 ? "暂无数据 · 最高 0" : $"最高 {_historyMetric switch { HistoryMetric.Count => $"{maximum} 次", _ => $"${maximum:N0}" }}", compact ? 9 : 11, Muted));
        panel.Children.Add(legend);
        if (_historyDate is not DateTime selected || selected.Year != _historyMonth.Year || selected.Month != _historyMonth.Month || selected.Date > today) return;
        var selectedEntries = history.Where(x => x.Timestamp.Date == selected.Date).ToArray();
        panel.Children.Add(T($"{selected:yyyy年MM月dd日} · {selectedEntries.Length} 次", compact ? 11 : 14));
        foreach (var entry in selectedEntries)
            panel.Children.Add(T($"{entry.Timestamp:HH:mm:ss} · {entry.GameName} · 投入 ${entry.Stake:N0} · 净变化 {(entry.Net >= 0 ? "+" : "-")}${Math.Abs((long)entry.Net):N0}\n{entry.Summary}", compact ? 10 : 13, Muted));
    }

    private ControlTemplate CalendarCellTemplate(bool compact)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(compact ? 4 : 6));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        template.VisualTree = border;
        return template;
    }

    private static Brush HeatColor(long value, long maximum)
    {
        if (value <= 0 || maximum <= 0) return Brushes.White;
        var t = Math.Clamp((double)value / maximum, 0, 1);
        var alpha = (byte)Math.Round(255 * (0.15 + 0.80 * t));
        return new SolidColorBrush(Color.FromArgb(alpha, 239, 68, 68));
    }
}
