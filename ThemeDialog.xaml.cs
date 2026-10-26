using System.Windows;
using System.Windows.Input;

namespace TableGameRoom;

public partial class ThemeDialog : Window
{
    private bool _closingHandled;

    public ThemeDialog() => InitializeComponent();

    private void DragTitle(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        // 关闭按钮等同取消，不改变业务状态
        DialogResult = false;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 避免窗口关闭事件递归：DialogResult 已触发 Closing 时不再重入
        if (_closingHandled) { base.OnClosing(e); return; }
        _closingHandled = true;
        if (DialogResult is null) DialogResult = false;
        base.OnClosing(e);
    }

    /// <summary>统一自定义主题模态窗口：白浅蓝浅绿圆角，500-600 宽，正文换行，中文按钮，Owner 居中，Escape/Enter 支持。</summary>
    public static bool ShowConfirm(Window owner, string message, string title, string okText = "确定", string cancelText = "取消")
    {
        var dlg = new ThemeDialog
        {
            Owner = owner,
            Title = title,
            Width = 560
        };
        dlg.TitleText.Text = title;
        dlg.BodyText.Text = message ?? string.Empty;
        dlg.OkBtn.Content = okText;
        dlg.CancelBtn.Content = cancelText;
        dlg.CancelBtn.Visibility = Visibility.Visible;
        dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        return dlg.ShowDialog() == true;
    }

    public static void ShowInfo(Window owner, string message, string title = "提示", string okText = "确定")
    {
        var dlg = new ThemeDialog
        {
            Owner = owner,
            Title = title,
            Width = 560
        };
        dlg.TitleText.Text = title;
        dlg.BodyText.Text = message ?? string.Empty;
        dlg.OkBtn.Content = okText;
        dlg.CancelBtn.Visibility = Visibility.Collapsed;
        dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        dlg.ShowDialog();
    }
}
