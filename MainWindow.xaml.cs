using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace TableGameRoom;

public partial class MainWindow : Window
{
    private static readonly (int Min, int? Max, string Name)[] BetTiers =
    [
        (10, 500, "休闲"),
        (50, 1_000, "轻松"),
        (100, 5_000, "入门"),
        (500, 10_000, "普通"),
        (1_000, 50_000, "进阶"),
        (5_000, 100_000, "强手"),
        (10_000, null, "极强")
    ];

    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(32, 42, 48));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(83, 99, 107));
    private static readonly Brush PanelBrush = new SolidColorBrush(Color.FromRgb(239, 247, 252));
    private static readonly Brush TableBrush = new SolidColorBrush(Color.FromRgb(226, 244, 236));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(91, 159, 208));
    private static readonly Brush PanelBorder = new SolidColorBrush(Color.FromRgb(217, 231, 232));
    private static readonly Brush TableBorder = new SolidColorBrush(Color.FromRgb(169, 215, 204));

    private readonly Random _rng = new();

    private StackPanel _page = new();
    private string _mode = "存档";
    private int _balance = 1000;
    private int _round = 1;
    private string _category = "全部游戏";
    private string _people = "全部人数";
    private int _tierIndex = 0;
    private int _seats = 3;
    private string _result = "";
    private string _currentGame = "";
    private bool _roundSettled = true;
    private bool _allowClose;
    private DateTime _historyMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime? _historyDate;
    private int _rouletteCommitted;
    private long _rouletteGeneration;

    private bool HasActiveRound => _currentGame switch
    {
        "poker" => _poker.InRound || _poker.Spectating,
        "three" => _three.InRound || _three.Spectating,
        "blackjack" => _bjInRound,
        "roulette" => _spinning,
        "liar" => _liar.InRound,
        _ => false
    };

    private bool ConfirmAbandon(string destination)
    {
        var isSettledSpectating = _roundSettled && ((_currentGame == "poker" && _poker.Spectating) || (_currentGame == "three" && _three.Spectating));
        if (isSettledSpectating)
        {
            AbandonCurrentRound();
            return true;
        }
        if (!HasActiveRound) return true;
        var ok = ThemeDialog.ShowConfirm(this,
            $"对局尚未结束，确定{destination}吗？\n已投入筹码不退还，并额外罚没当前底池的 30%（向上取整）。",
            "放弃当前对局", "确定放弃", "取消");
        if (!ok) return false;
        AbandonCurrentRound();
        return true;
    }

    private void AbandonCurrentRound()
    {
        // 玩家弃牌后已结算（_roundSettled=true），观战退出/切模式不应再罚已退出玩家
        var isSpectating = (_currentGame == "poker" && _poker.Spectating) || (_currentGame == "three" && _three.Spectating);
        if (isSpectating && _roundSettled)
        {
            // 观战阶段退出不再罚金，仅结束观战状态
            if (_currentGame == "poker") { _poker.Spectating = false; _poker.InRound = false; _poker.Showdown = true; }
            if (_currentGame == "three") { _three.Spectating = false; _three.InRound = false; _three.Finished = true; }
            return;
        }
        if (!HasActiveRound || _roundSettled) return;
        _roundSettled = true;
        var pot = _currentGame switch
        {
            "poker" => _poker.TablePot,
            "three" => _three.TablePot,
            "roulette" => _rouletteCommitted,
            "blackjack" => _bjStake,
            "liar" => _liar.Pot,
            _ => 0
        };
        var penalty = (int)Math.Ceiling(Math.Max(0, pot) * 0.30);
        var abandonedStake = _currentGame switch
        {
            "poker" => _poker.PlayerCommitted,
            "three" => _three.PlayerCommitted,
            "blackjack" => _bjStake,
            "roulette" => _rouletteCommitted,
            "liar" => _liar.PlayerCommitted,
            _ => 0
        };
        // 牌类游戏投入已在下注时扣除；Blackjack 到结算时才入账本金。
        var unchargedStake = _currentGame == "blackjack" ? abandonedStake : 0;
        if (_mode == "存档") _balance = Math.Max(0, _balance - penalty - unchargedStake);
        switch (_currentGame)
        {
            case "poker": _poker.InRound = false; _poker.Spectating = false; _poker.Showdown = true; _poker.Winner = $"已退出对局，投入不退；额外罚金 ${penalty:N0}。"; break;
            case "three": _three.InRound = false; _three.Spectating = false; _three.Finished = true; _three.Winner = $"已退出对局，投入不退；额外罚金 ${penalty:N0}。"; break;
            case "blackjack": _bjInRound = false; _bjOver = true; _bjOutcome = $"已退出对局，额外罚金 ${penalty:N0}"; break;
            case "roulette": _spinning = false; break;
            case "liar": _liar.InRound = false; _liar.Winner = $"已退出对局，投入不退；额外罚金 ${penalty:N0}。"; break;
        }
        _round++;
        var abandonedNet = (int)Math.Clamp(-(long)abandonedStake - penalty, int.MinValue, int.MaxValue);
        RecordHistory(abandonedStake, abandonedNet, $"{ModeDisplay.GameName(_currentGame)}：放弃对局 · 投入 ${abandonedStake:N0} · 罚金 ${penalty:N0}");
        SaveProgress();
    }

    private (int Min, int? Max, string Name) Tier => BetTiers[_tierIndex];

    // 档位上限是“单次下注”上限；一局累计上限 = 单次上限 × 25 × 10。
    private int RoundCap => Tier.Max is null
        ? int.MaxValue
        : (int)Math.Min(int.MaxValue, (long)Tier.Max.Value * 25 * 10);

    public MainWindow()
    {
        InitializeComponent();
        RoundedCorners.Apply(this);
        MaximizeFix.Apply(this);
        StateChanged += (_, _) => SyncMaximizeGlyph();
        Closing += (_, e) =>
        {
            if (_allowClose) return;
            if (!ConfirmAbandon("关闭窗口")) e.Cancel = true;
            else _allowClose = true;
        };
        ShowModeSelect();
    }

    // ---------- 公共控件 ----------

    private Button B(string text, Action? action = null)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(4),
            Padding = new Thickness(14, 7, 14, 7),
            FontSize = 14,
            Background = PanelBrush,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            Foreground = Ink,
            Cursor = Cursors.Hand,
            Template = RoundedButtonTemplate()
        };
        if (action is not null) button.Click += (_, _) => action();
        return button;
    }

    private ControlTemplate RoundedButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Bd";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        template.VisualTree = border;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(220, 235, 247)), "Bd"));
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, Accent, "Bd"));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.78));
        template.Triggers.Add(pressed);
        return template;
    }

    private TextBlock T(string text, int size = 14, Brush? brush = null) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = brush ?? Ink,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(2, 3, 2, 3)
    };

    private void Base(string heading)
    {
        // 页面重建时，让仍在进行的轮盘小球动画自行退出（避免静态事件累积）
        if (_rouletteGeneration > 0) _rouletteGeneration++;
        _rouletteBall = null;
        _rouletteOverlay = null;

        _page = new StackPanel { Margin = new Thickness(26) };
        _page.Children.Add(T(heading, 26));
        Root.Content = new ScrollViewer
        {
            Content = _page,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(4)
        };
    }

    private void Header()
    {
        var bar = new WrapPanel { Margin = new Thickness(0, 4, 0, 10) };
        bar.Children.Add(T($"模式：{ModeDisplay.ToDisplay(_mode)}    余额：{BalanceText()}", 15));
        _page.Children.Add(bar);

        var actions = new WrapPanel();
        actions.Children.Add(B("退出到模式选择", () => { if (ConfirmAbandon("退出到模式选择")) ShowModeSelect(); }));
        if (_currentGame.Length > 0)
            actions.Children.Add(B("返回游戏大厅", () => { if (ConfirmAbandon("返回游戏大厅")) ShowLobby(); }));
        actions.Children.Add(B("🎯 任务", () => { if (ConfirmAbandon("前往任务中心")) { LoadTasks(); ShowTasks(); } }));
        _page.Children.Add(actions);

        if (_result.Length > 0) _page.Children.Add(T(_result, 15, Muted));
    }

    private string BalanceText() => _mode == "智能" ? "∞（无限模式）" : $"${_balance:N0}";

    private string TierText() => Tier.Max is null
        ? $"${Tier.Min:N0} 起，无上限"
        : $"${Tier.Min:N0}–${Tier.Max.Value:N0}";

    // ---------- 存档 ----------

    // 未知 JSON 字段（包括旧版 History）会被忽略，新存档不再写入历史。
    private sealed record SaveData(int Balance, int Round, int TablePlayers = 3, int Tier = 0);

    private string SavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TableGameRoom",
        ModeDisplay.FileName(_mode));

    private string HistoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TableGameRoom",
        _mode == "存档" ? "progress-history.json" : "custom-progress-history.json");

    private void RecordHistory(int stake, int net, string summary)
    {
        try
        {
            var entries = LoadHistory().ToList();
            entries.Add(new GameHistoryEntry(Guid.NewGuid().ToString("N"), DateTime.Now, _currentGame,
                ModeDisplay.GameName(_currentGame), Math.Max(0, stake), net, summary));
            var cutoff = ModeDisplay.HistoryCutoff;
            entries = entries.Where(x => x.Timestamp != default && x.Timestamp.Date >= cutoff).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(entries));
        }
        catch { }
    }

    private List<GameHistoryEntry> LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryPath)) return [];
            return (JsonSerializer.Deserialize<List<GameHistoryEntry>>(File.ReadAllText(HistoryPath)) ?? [])
                .Where(x => x.Timestamp != default && ModeDisplay.IsInWindow(x.Timestamp))
                .OrderByDescending(x => x.Timestamp).ToList();
        }
        catch { return []; }
    }

    private void LoadProgress()
    {
        try
        {
            if (!File.Exists(SavePath)) return;
            var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SavePath));
            if (data is null) return;
            if (_mode == "存档") _balance = Math.Max(0, data.Balance);
            _round = Math.Max(1, data.Round);
            _seats = Math.Clamp(data.TablePlayers, 2, 7);
            _tierIndex = Math.Clamp(data.Tier, 0, BetTiers.Length - 1);
        }
        catch
        {
            _balance = 1000;
            _round = 1;
        }
    }

    private void SaveProgress()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
            var balance = _mode == "智能" ? int.MaxValue : _balance;
            var data = new SaveData(balance, _round, _seats, _tierIndex);
            File.WriteAllText(SavePath, JsonSerializer.Serialize(data));
        }
        catch
        {
            // 存档失败不影响游戏流程
        }
    }

    // ---------- 下注 ----------

    private bool TryStake(int total, out string error)
    {
        error = "";
        if (total < Tier.Min)
        {
            error = $"当前档位最低下注 ${Tier.Min:N0}，本局需要 ${total:N0}。";
            return false;
        }
        if (Tier.Max is not null && total > Tier.Max.Value)
        {
            error = $"当前档位单次下注上限 ${Tier.Max.Value:N0}，本次需要 ${total:N0}。";
            return false;
        }
        if (_mode == "存档" && total > _balance)
        {
            error = $"余额不足：需要 ${total:N0}，当前余额 ${_balance:N0}。";
            // 余额连最低档都打不起时，引导去做任务
            if (_balance < BetTiers[0].Min) CheckBankrupt();
            return false;
        }
        return true;
    }

    private int[] QuickStakes()
    {
        var min = Tier.Min;
        var max = Tier.Max ?? Math.Max(min * 8, min + 1000);
        var list = new List<int>();
        foreach (var f in new[] { 1, 2, 5, 10 })
        {
            var v = min * f;
            if (v <= max && !list.Contains(v)) list.Add(v);
        }
        if (!list.Contains(min)) list.Insert(0, min);
        return [.. list];
    }

    private int SuggestedStake()
    {
        var min = Tier.Min;
        var max = Tier.Max ?? Math.Max(min * 4, min + 500);
        var suggestion = Math.Min(max, Math.Max(min, min * 2));
        if (_mode == "存档") suggestion = Math.Min(suggestion, Math.Max(min, _balance));
        return suggestion;
    }

    /// <summary>统一下注控件：档位校验 + 动态快捷金额。</summary>
    private TextBox StakeBox()
    {
        _page.Children.Add(T($"本局下注（虚拟美元）· 当前档位 {Tier.Name}：{TierText()}", 14, Muted));
        var input = new TextBox
        {
            Text = SuggestedStake().ToString(),
            Width = 120,
            Margin = new Thickness(2, 2, 2, 4)
        };
        var row = new WrapPanel();
        foreach (var v in QuickStakes())
        {
            var value = v;
            row.Children.Add(B($"${value:N0}", () => input.Text = value.ToString()));
        }
        _page.Children.Add(input);
        _page.Children.Add(row);
        return input;
    }

    private void Settle(int stake, int net, string summary)
    {
        if (_roundSettled) return;
        _roundSettled = true;
        if (_mode == "存档") _balance = Math.Max(0, _balance + net);
        _round++;
        _result = $"结果：{summary}    净收益 {(net >= 0 ? "+" : "-")}${Math.Abs(net):N0}";
        RecordHistory(stake, net, summary);
        UpdateTasks(net);
        SaveProgress();
    }

    // ---------- 任务 ----------

    private List<GameTask> _tasks = [];

    private void LoadTasks() => _tasks = TaskSystem.Load(_mode);

    private void SaveTasks() => TaskSystem.Save(_mode, _tasks);

    /// <summary>结算后推进任务进度，并在完成时提示。</summary>
    private void UpdateTasks(int net)
    {
        if (_tasks.Count == 0) LoadTasks();
        if (_tasks.Count == 0) return;

        var completed = TaskSystem.OnRoundSettled(_tasks, _currentGame, net, _mode);
        SaveTasks();

        foreach (var task in completed)
        {
            // 自动发放奖励，避免玩家忘记领取
            if (!task.Claimed)
            {
                task.Claimed = true;
                if (_mode == "存档") _balance += task.Reward;
                _result += $"  🎯 任务完成：{task.Title}，奖励 +${task.Reward:N0}";
            }
        }
        if (completed.Count > 0) SaveTasks();
    }

    // 牌类游戏在每一步下注时就已经从余额里扣掉了本局投入（committed）。
    // 结算时先把这笔投入退回，再按 payout - committed 的真实盈亏入账。
    private void SettleRound(int stake, int payout, int committed, string summary)
    {
        if (_roundSettled) return;
        if (_mode == "存档") _balance = Math.Max(0, _balance + committed);
        Settle(stake, payout - committed, summary);
    }

    private void Msg(string text) => ThemeDialog.ShowInfo(this, text, "提示");

    private void ResetChipState()
    {
        _result = "";
        _poker.TablePot = 0;
        _three.TablePot = 0;
    }

    // ---------- 模式选择 ----------

    private void ShowModeSelect()
    {
        if (!ConfirmAbandon("退出到模式选择")) return;
        _mode = "存档";
        ResetChipState();
        _currentGame = "";
        _lanPage = "";
        LoadProgress();

        Base("欢迎来到 TABLE 游戏厅");
        _page.Children.Add(T("全部筹码均为虚拟筹码，程序离线运行，不使用数据库、不联网、不涉及真实货币。", 15, Muted));

        var cards = new Grid { Margin = new Thickness(0, 16, 0, 10) };
        cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        cards.Children.Add(ModeCard(
            0,
            "普通模式",
            "本机保存的余额与进度",
            $"当前存档：余额 ${_balance:N0} · 已玩 {_round - 1} 局 · 桌人数 {_seats} 人",
            "选择普通模式",
            () =>
            {
                _mode = "存档";
                LoadProgress();
                ShowLobby();
            },
            "\U0001F4BE"));

        cards.Children.Add(ModeCard(
            1,
            "无限模式",
            "独立进度 · 无限虚拟筹码",
            "不影响普通模式，使用独立进度文件，余额显示为 ∞。",
            "选择无限模式",
            () =>
            {
                _mode = "智能";
                _balance = 1000;
                _round = 1;
                LoadProgress();
                ShowLobby();
            },
            "\U0001F9E0"));

        _page.Children.Add(cards);
        var taskEntry = B("🎯 任务中心（做任务赚奖金）", () => { LoadTasks(); ShowTasks(); });
        taskEntry.FontSize = 17;
        taskEntry.Padding = new Thickness(22, 11, 22, 11);
        taskEntry.Margin = new Thickness(2, 6, 2, 6);
        _page.Children.Add(taskEntry);

        var lanEntry = B("🌐 联机大厅（局域网对战）", () => ShowLanEntry());
        lanEntry.FontSize = 17;
        lanEntry.Padding = new Thickness(22, 11, 22, 11);
        lanEntry.Margin = new Thickness(2, 6, 2, 6);
        _page.Children.Add(lanEntry);

        _page.Children.Add(T("存档位置：%LOCALAPPDATA%\\TableGameRoom\\progress.json（无限模式为 custom-progress.json）", 13, Muted));
    }

    private Border ModeCard(int column, string title, string subtitle, string detail, string action, Action onClick, string icon)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(T(icon, 52));
        stack.Children.Add(T(title, 42));
        stack.Children.Add(T(subtitle, 19, Accent));
        stack.Children.Add(T(detail, 15, Muted));
        var button = B(action, onClick);
        button.FontSize = 21;
        button.Padding = new Thickness(30, 15, 30, 15);
        button.Margin = new Thickness(0, 20, 0, 0);
        button.HorizontalAlignment = HorizontalAlignment.Left;
        stack.Children.Add(button);

        var card = new Border
        {
            MinHeight = 340,
            Margin = new Thickness(12),
            Padding = new Thickness(30),
            Background = PanelBrush,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(24),
            Cursor = Cursors.Hand,
            Child = stack
        };
        card.MouseLeftButtonDown += (_, _) => onClick();
        card.MouseEnter += (_, _) =>
        {
            card.Background = new SolidColorBrush(Color.FromRgb(220, 235, 247));
            card.BorderBrush = Accent;
        };
        card.MouseLeave += (_, _) =>
        {
            card.Background = PanelBrush;
            card.BorderBrush = PanelBorder;
        };
        Grid.SetColumn(card, column);
        return card;
    }

    // ---------- 玩法选择（第一层界面）----------

    private UIElement BuildHistoryPanel(bool compact = false) => BuildHistoryPanelCore(compact);

    private UIElement BuildHistoryPanelLegacyOld(bool compact = false)
    {
        var panel = new StackPanel { Margin = new Thickness(compact ? 10 : 26) };
        var nav = new WrapPanel();
        nav.Children.Add(B("‹", () => { _historyMonth = _historyMonth.AddMonths(-1); ShowHistory(); }));
        nav.Children.Add(T(_historyMonth.ToString("yyyy 年 MM 月"), 19));
        nav.Children.Add(B("›", () => { _historyMonth = _historyMonth.AddMonths(1); ShowHistory(); }));
        if (!compact) nav.Children.Add(B("返回大厅", ShowLobby));
        panel.Children.Add(T("日历历史", 18));
        panel.Children.Add(nav);

        var history = LoadHistory();
        var first = _historyMonth;
        var offset = (int)first.DayOfWeek;
        var days = DateTime.DaysInMonth(first.Year, first.Month);
        var grid = new UniformGrid { Columns = 7, Margin = new Thickness(0, 8, 0, 8) };
        foreach (var label in new[] { "日", "一", "二", "三", "四", "五", "六" }) grid.Children.Add(T(label, 14, Muted));
        for (var i = 0; i < offset; i++) grid.Children.Add(new Border());
        for (var day = 1; day <= days; day++)
        {
            var date = new DateTime(first.Year, first.Month, day);
            var records = history.Where(x => x.Timestamp.Date == date).ToArray();
            var button = B(records.Length == 0 ? day.ToString() : $"{day}\n{records.Length} 局", () => { _historyDate = date; ShowHistory(); });
            button.MinWidth = compact ? 28 : 52;
            button.MinHeight = compact ? 30 : 46;
            button.Margin = new Thickness(2);
            if (_historyDate == date) button.BorderBrush = Accent;
            grid.Children.Add(button);
        }
        panel.Children.Add(grid);
        var selected = _historyDate is null ? [] : history.Where(x => x.Timestamp.Date == _historyDate.Value).ToArray();
        panel.Children.Add(T(_historyDate is null ? "选择日期查看记录" : _historyDate.Value.ToString("yyyy年MM月dd日"), 14));
        foreach (var entry in selected)
            panel.Children.Add(T($"{entry.Timestamp:HH:mm:ss} · {entry.GameName}\n下注 ${entry.Stake:N0} · 净变化 {(entry.Net >= 0 ? "+" : "-")}${Math.Abs(entry.Net):N0}\n{entry.Summary}", compact ? 12 : 14, Muted));
        if (compact && selected.Length == 0) panel.Children.Add(T("当天暂无对局记录", 12, Muted));
        return panel;
    }

    private void ShowHistory()
    {
        Base("日历历史");
        Header();
        _page.Children.Add(BuildHistoryPanel());
    }

    private void ShowLobby()
    {
        if (_currentGame.Length > 0 && !ConfirmAbandon("返回游戏大厅")) return;
        Base("选择玩法");
        Header();

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        var lobby = new StackPanel();
        Grid.SetColumn(lobby, 0);
        content.Children.Add(lobby);
        var history = new Border { Margin = new Thickness(14, 2, 0, 0), Padding = new Thickness(6), Background = PanelBrush, BorderBrush = PanelBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Child = BuildHistoryPanel(true) };
        Grid.SetColumn(history, 1);
        content.Children.Add(history);
        var filters = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
        filters.Children.Add(T("分类筛选", 14, Muted));
        foreach (var item in new[] { "全部游戏", "桌面牌类", "轮盘", "21 点" })
        {
            var label = item;
            filters.Children.Add(Chip(label, _category == label, () =>
            {
                _category = label;
                if (label is "轮盘" or "21 点") _people = "全部人数";
                SaveProgress();
                ShowLobby();
            }));
        }
        lobby.Children.Add(filters);

        
        lobby.Children.Add(T("点击任意玩法卡片进入下一步筛选，然后开始游戏。", 14, Accent));

        var games = AllGames().Where(Visible).ToArray();
        if (games.Length == 0) lobby.Children.Add(T("当前筛选条件下没有可用玩法。", 15, Muted));

        var wrap = new WrapPanel();
        var index = 0;
        foreach (var game in games)
        {
            index++;
            wrap.Children.Add(GameCard(index, game));
        }
        lobby.Children.Add(wrap);
        _page.Children.Add(content);
    }

    private Button Chip(string label, bool selected, Action action)
    {
        var button = B((selected ? "✓ " : "") + label, action);
        button.FontSize = 15;
        button.Padding = new Thickness(18, 9, 18, 9);
        button.Margin = new Thickness(6, 4, 6, 4);
        if (selected)
        {
            button.Background = new SolidColorBrush(Color.FromRgb(203, 223, 241));
            button.BorderBrush = Accent;
            button.FontWeight = FontWeights.Bold;
        }
        return button;
    }

    // ---------- 筛选（第二层界面）----------

    private void ShowGameSetup(string key)
    {
        if (key != "poker" && _people == "双人桌") _people = "3–7 人";
        var game = AllGames().First(g => g.Key == key);
        var tableGame = key is "poker" or "three" or "liar";

        Base($"筛选 · {game.Name}");
        Header();

        _page.Children.Add(T(game.Intro, 15, Muted));
        _page.Children.Add(T($"规则：{RuleText(key).Replace("\n", " ")}", 13, Muted));

        var cats = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        cats.Children.Add(T("分类筛选", 14, Muted));
        foreach (var item in new[] { "全部游戏", "桌面牌类", "轮盘", "21 点" })
        {
            var label = item;
            cats.Children.Add(Chip(label, _category == label, () =>
            {
                _category = label;
                if (label is "轮盘" or "21 点") _people = "全部人数";
                SaveProgress();
                ShowLobby();
            }));
        }
        _page.Children.Add(cats);

        var seats = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        seats.Children.Add(T("人数筛选", 14, Muted));
        if (tableGame)
        {
            seats.Children.Add(Chip("3–7 人", _people != "双人桌", () =>
            {
                _people = "3–7 人";
                SaveProgress();
                ShowGameSetup(key);
            }));
            var duo = Chip("双人桌", _people == "双人桌", () =>
            {
                _people = "双人桌";
                SaveProgress();
                ShowGameSetup(key);
            });
            if (key != "poker")
            {
                duo.IsEnabled = false;
                duo.Opacity = 0.45;
                duo.ToolTip = "仅德州扑克支持双人桌";
            }
            seats.Children.Add(duo);
        }
        else
        {
            seats.Children.Add(T("轮盘与 21 点不支持人数筛选。", 14, Muted));
        }
        _page.Children.Add(seats);

        if (tableGame)
        {
            var count = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
            count.Children.Add(T("桌人数", 14, Muted));
            if (_people == "双人桌" && key == "poker")
            {
                count.Children.Add(T("双人桌固定 2 人：你 + 1 位电脑。", 15, Accent));
            }
            else
            {
                foreach (var n in new[] { 3, 4, 5, 6, 7 })
                {
                    var value = n;
                    count.Children.Add(Chip($"{value} 人", _seats == value, () =>
                    {
                        _seats = value;
                        SaveProgress();
                        ShowGameSetup(key);
                    }));
                }
            }
            _page.Children.Add(count);
        }

        _page.Children.Add(T("下注档位筛选", 14, Muted));
        var tiers = new WrapPanel();
        for (var i = 0; i < BetTiers.Length; i++)
        {
            var index = i;
            var t = BetTiers[i];
            var text = t.Max is null ? $"{t.Name}：${t.Min:N0} 起" : $"{t.Name}：${t.Min:N0}–${t.Max.Value:N0}";
            tiers.Children.Add(Chip(text, _tierIndex == index, () =>
            {
                _tierIndex = index;
                SaveProgress();
                ShowGameSetup(key);
            }));
        }
        _page.Children.Add(tiers);

        var players = key == "poker" && _people == "双人桌" ? 2 : Math.Clamp(_seats, 3, 7);
        _page.Children.Add(T($"当前选择：{game.Name} · 人数 {(tableGame ? $"{players} 人" : "不适用")} · 档位 {Tier.Name}（{TierText()}）· 模式 {ModeDisplay.ToDisplay(_mode)}", 16));

        var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        var start = B("开始游戏 →", () => OpenGame(key));
        start.FontSize = 19;
        start.Padding = new Thickness(26, 13, 26, 13);
        actions.Children.Add(start);
        actions.Children.Add(B("返回选择玩法", ShowLobby));
        actions.Children.Add(B("退出到模式选择", ShowModeSelect));
        _page.Children.Add(actions);

        // ---- 联机组队入口（用这个游戏开房 / 加入别人的房） ----
        _page.Children.Add(T("联机对战", 18));
        var lanRow = new WrapPanel();
        var createLan = B($"🌐 用「{game.Name}」创建联机房间", () =>
        {
            if (!EnsureLanConnected()) return;
            _pendingRoomGame = key;
            _currentGame = "";
            ShowLanConnected();
        });
        lanRow.Children.Add(createLan);
        lanRow.Children.Add(B("加入局域网房间", () =>
        {
            if (!EnsureLanConnected()) return;
            _pendingRoomGame = null;
            _currentGame = "";
            ShowLanConnected();
        }));
        _page.Children.Add(lanRow);
        _page.Children.Add(T("创建后房间会出现在联机大厅的房间列表里，可邀请局域网内的玩家加入。", 12, Muted));
    }

    /// <summary>确保已接入局域网；未接入则跳转联机大厅。</summary>
    private bool EnsureLanConnected()
    {
        if (_lanServer is not null || _lanClient is not null) return true;
        Msg("请先在联机大厅创建或加入一个局域网会话。");
        ShowLanEntry();
        return false;
    }

    private static (string Key, string Name, string Intro, string Tag)[] AllGames() =>
    [
        ("roulette", "欧洲轮盘", "单零轮盘：选择红黑、单双、大小或具体号码，转动轮盘等待结果。", "37 格 · 单项与多项下注"),
        ("blackjack", "21 点", "要牌、停牌或双倍下注，让手牌尽量接近 21 点且不爆牌。", "紧凑真实牌面"),
        ("poker", "德州扑克", "两张底牌与五张公共牌，翻牌前、翻牌、转牌、河牌摊牌。", "桌面牌类 · 支持双人桌"),
        ("three", "炸金花", "三张牌比较牌型与胆量，可以看牌、跟注、加注、比牌或弃牌。", "桌面牌类 · 3–7 人"),
        ("liar", "骗子扑克", "盖牌出牌并声明牌型，可以诚实也可以虚张声势，对手可以质疑。", "桌面牌类 · 3–7 人")
    ];

    private bool Visible((string Key, string Name, string Intro, string Tag) game) => _category switch
    {
        "桌面牌类" => game.Key is "poker" or "three" or "liar",
        "轮盘" => game.Key == "roulette",
        "21 点" => game.Key == "blackjack",
        _ => true
    };

    private Border GameCard(int number, (string Key, string Name, string Intro, string Tag) game)
    {
        var stack = new StackPanel();
        stack.Children.Add(T($"{number:00}  {game.Name}", 27));
        stack.Children.Add(T(game.Intro, 15, Muted));
        stack.Children.Add(T($"{game.Tag} · 档位 {Tier.Name}：{TierText()}", 13, Accent));
        var button = B("进入筛选 →", () => ShowGameSetup(game.Key));
        button.FontSize = 16;
        button.Padding = new Thickness(20, 10, 20, 10);
        stack.Children.Add(button);

        var card = new Border
        {
            Width = 366,
            MinHeight = 200,
            Margin = new Thickness(8),
            Padding = new Thickness(18),
            Background = PanelBrush,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Cursor = Cursors.Hand,
            Child = stack
        };
        card.MouseLeftButtonDown += (_, _) => ShowGameSetup(game.Key);
        card.MouseEnter += (_, _) =>
        {
            card.Background = new SolidColorBrush(Color.FromRgb(220, 235, 247));
            card.BorderBrush = Accent;
        };
        card.MouseLeave += (_, _) =>
        {
            card.Background = PanelBrush;
            card.BorderBrush = PanelBorder;
        };

        var tip = new ToolTip
        {
            Placement = PlacementMode.Mouse,
            MaxWidth = 430,
            Background = Brushes.White,
            BorderBrush = Accent,
            Content = new TextBlock
            {
                Text = RuleText(game.Key),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = Ink,
                Margin = new Thickness(6)
            }
        };
        card.ToolTip = tip;
        return card;
    }

    private static string RuleText(string key) => key switch
    {
        "roulette" => "欧洲单零轮盘共 37 个号码：0–36。\n红黑、单双、大小为 1:1。\n1–18 为小号，19–36 为大号。\n单号码赔率为 35:1。\n0 为绿色，不属于红黑、单双和大小。\n先选择投注方式和金额，再旋转轮盘。\n命中后返还本金加赔率收益。",
        "blackjack" => "目标是让手牌点数尽量接近 21 且不爆牌。\n数字牌按面值计算。\nJ、Q、K 均为 10 点。\nA 可以计算为 1 点或 11 点。\n初始发两张牌。\n玩家可以要牌、停牌或双倍下注。\n庄家至少补牌到 17 点。\n超过 21 点即爆牌。\n点数更高者获胜，点数相同为平局。",
        "poker" => "每位玩家拥有两张底牌。\n桌面依次发出五张公共牌。\n牌局阶段为翻牌前、翻牌、转牌和河牌。\n玩家可以跟注、加注或弃牌。\n摊牌时从七张牌中选出最佳五张牌。\n牌型从高到低为：高牌、对子、两对、三条、顺子、同花、葫芦、四条、同花顺。",
        "three" => "每位玩家获得三张牌。\n牌型从高到低为：同花顺、豹子、顺子、同花、对子、单张。\n先比较牌型，再比较点数和单牌。\n电脑根据难度决定跟注、弃牌或虚张声势。\n所有仍在牌局中的玩家完成比牌后结算。",
        _ => "玩家和电脑轮流盖牌出牌并声明牌型。\n玩家可以诚实出牌，也可以虚张声势。\n下一位玩家可以继续出牌或质疑。\n质疑后翻开上一手牌。\n如果声明不实，说谎者收走桌面牌。\n如果声明属实，质疑者收走桌面牌。\n先出完手牌的一方获胜。"
    };

    private void OpenGame(string key)
    {
        _currentGame = key;
        _roundSettled = true;
        _result = "";
        // 重新进入牌桌时清空上一局残留，避免旧状态与InRound/Spectating stale
        if (key is "poker" or "three")
        {
            _poker.InRound = false;
            _poker.Showdown = false;
            _poker.PlayerFolded = false;
            _poker.Spectating = false;
            _poker.Stage = 0;
            _poker.TablePot = 0;
            _poker.Stake = 50;
            _poker.PlayerCommitted = 0;
            _poker.LastPlayerAction = 0;
            _poker.PlayerRaises = 0;
            _poker.PlayerCalls = 0;
            _poker.Log = "";
            _poker.Winner = "";
            _poker.Player.Clear();
            _poker.Community.Clear();
            _poker.Seats.Clear();
            _three.InRound = false;
            _three.Finished = false;
            _three.PlayerFolded = false;
            _three.Spectating = false;
            _three.Seen = false;
            _three.TablePot = 0;
            _three.Stake = 50;
            _three.PlayerCommitted = 0;
            _three.ChipStack = 0;
            _three.Log = "";
            _three.Winner = "";
            _three.Player.Clear();
            _three.Seats.Clear();
        }
        switch (key)
        {
            case "roulette": RoulettePage(); break;
            case "blackjack": BlackjackPage(); break;
            case "poker": PokerPage(); break;
            case "three": ThreePage(); break;
            default: LiarPage(); break;
        }
    }

    // ---------- 窗口 ----------

    private void DragTitle(object sender, MouseButtonEventArgs e)
    {
        // 仅普通状态可拖动；最大化时避免 DragMove 造成窗口跳动
        if (e.LeftButton == MouseButtonState.Pressed && WindowState == WindowState.Normal) DragMove();
    }

    private void Minimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void ToggleMaximize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    /// <summary>最大化 / 还原时切换标题栏图标，并同步 ToolTip。</summary>
    private void SyncMaximizeGlyph()
    {
        if (MaxGlyph is null || MaxBtn is null) return;

        if (WindowState == WindowState.Maximized)
        {
            // 还原图标：两个重叠的方块
            MaxGlyph.Data = Geometry.Parse("M2.5,0.5 L9.5,0.5 L9.5,7.5 M0.5,2.5 L7.5,2.5 L7.5,9.5 L0.5,9.5 Z");
            MaxBtn.ToolTip = "还原";
            System.Windows.Automation.AutomationProperties.SetName(MaxBtn, "还原");
        }
        else
        {
            // 最大化图标：单个方块
            MaxGlyph.Data = Geometry.Parse("M0.5,0.5 L9.5,0.5 L9.5,9.5 L0.5,9.5 Z");
            MaxBtn.ToolTip = "最大化";
            System.Windows.Automation.AutomationProperties.SetName(MaxBtn, "最大化");
        }
    }

    private void NewInstance(object sender, RoutedEventArgs e)
    {
        var unlocked = MultiInstanceGate.IsUnlocked();
        var dialog = new MultiInstanceDialog(needsCode: !unlocked) { Owner = this };
        dialog.ShowDialog();

        if (!dialog.Confirmed) return;

        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                Msg("找不到程序自身路径，无法新建实例。");
                return;
            }

            // --multi 跳过单实例互斥锁；--child 让新窗口不重复弹"多开"确认
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "--multi",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory
            });
        }
        catch (Exception ex)
        {
            App.Log(ex);
            Msg($"新建实例失败：{ex.Message}");
        }
    }

    private void CloseWindow(object sender, RoutedEventArgs e)
    {
        if (!ConfirmAbandon("关闭窗口")) return;
        _allowClose = true;
        Close();
    }
}
