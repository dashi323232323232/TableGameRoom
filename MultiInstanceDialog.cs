using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TableGameRoom;

/// <summary>
/// 多开权限弹窗。
/// 第一阶段：输入验证码（仅首次需要）；验证通过后原地切换到第二阶段。
/// 第二阶段：询问是否新建一个实例，确认 / 取消。
/// </summary>
public sealed class MultiInstanceDialog : Window
{
    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(32, 42, 48));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(83, 99, 107));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(91, 159, 208));
    private static readonly Brush Error = new SolidColorBrush(Color.FromRgb(192, 57, 43));

    private readonly StackPanel _host;
    private readonly TextBlock _heading;
    private readonly TextBlock _hint;
    private readonly TextBlock _errorText;
    private readonly PasswordBox _input;
    private readonly WrapPanel _buttons;
    private readonly Button _ok;

    /// <summary>true = 用户确认新建实例。</summary>
    public bool Confirmed { get; private set; }

    public MultiInstanceDialog(bool needsCode)
    {
        Title = "多开";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");
        ShowInTaskbar = false;

        _host = new StackPanel { Margin = new Thickness(30, 26, 30, 24) };

        _heading = new TextBlock
        {
            Text = "🔐 解锁多开权限",
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = Ink
        };
        _host.Children.Add(_heading);

        _hint = new TextBlock
        {
            Text = "多开属于高级功能，首次使用需要输入验证码。解锁后本机不再询问。",
            FontSize = 13,
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        _host.Children.Add(_hint);

        _input = new PasswordBox
        {
            Margin = new Thickness(0, 18, 0, 0),
            Padding = new Thickness(12, 10, 12, 10),
            FontSize = 15,
            BorderBrush = Accent,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(239, 247, 252)),
            PasswordChar = '●'
        };
        _input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { OkClick(); e.Handled = true; }
        };
        _host.Children.Add(_input);

        _errorText = new TextBlock
        {
            Text = "",
            FontSize = 13,
            Foreground = Error,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
            Visibility = Visibility.Collapsed
        };
        _host.Children.Add(_errorText);

        _buttons = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };
        _ok = MakeButton("确定", OkClick, primary: true);
        _buttons.Children.Add(_ok);
        _buttons.Children.Add(MakeButton("取消", () => { Confirmed = false; Close(); }));
        _host.Children.Add(_buttons);

        Content = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(217, 231, 232)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = _host
        };

        // 已解锁：直接进第二阶段
        if (!needsCode) SwitchToConfirm();
        else _input.Focus();

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        };
    }

    private Button MakeButton(string text, Action action, bool primary = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 92,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(18, 8, 18, 8),
            FontSize = 14,
            Cursor = Cursors.Hand,
            Foreground = primary ? Brushes.White : Ink,
            Background = primary ? Accent : new SolidColorBrush(Color.FromRgb(239, 247, 252)),
            BorderBrush = primary ? Accent : new SolidColorBrush(Color.FromRgb(217, 231, 232)),
            BorderThickness = new Thickness(1)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void OkClick()
    {
        // 第二阶段：确认新建实例
        if (!_input.IsVisible)
        {
            Confirmed = true;
            Close();
            return;
        }

        var code = _input.Password;
        if (!MultiInstanceGate.Verify(code))
        {
            _errorText.Text = "验证码错误，请输入正确验证码";
            _errorText.Visibility = Visibility.Visible;
            _input.SelectAll();
            _input.Focus();
            return;
        }

        SwitchToConfirm();
    }

    private void SwitchToConfirm()
    {
        _heading.Text = "是否新建一个实例？";
        _hint.Text = "将再启动一个独立的游戏窗口，可以自己和自己联机，或同时观察多个牌桌。";
        _errorText.Visibility = Visibility.Collapsed;
        _input.Visibility = Visibility.Collapsed;

        _ok.Content = "确认";
        _ok.Focus();
        Height = double.NaN;
    }
}
