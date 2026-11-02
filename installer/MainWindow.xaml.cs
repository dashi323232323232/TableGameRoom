using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace TableSetup;

public partial class MainWindow : Window
{
    private const string AppName = "TABLE游戏厅";

    public MainWindow()
    {
        InitializeComponent();
        PathBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppName);
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择安装位置",
            InitialDirectory = PathBox.Text
        };

        if (dlg.ShowDialog() == true)
            PathBox.Text = dlg.FolderName;
    }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        var target = PathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            MessageBox.Show("请填写安装位置。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            InstallBtn.IsEnabled = false;
            StatusText.Text = "正在安装…";

            Directory.CreateDirectory(target);
            var exePath = Path.Combine(target, "TableGameRoom.exe");

            // 从嵌入资源解出主程序
            var asm = Assembly.GetExecutingAssembly();
            var resName = Array.Find(asm.GetManifestResourceNames(),
                n => n.EndsWith("payload.TableGameRoom.exe", StringComparison.OrdinalIgnoreCase))
                ?? Array.Find(asm.GetManifestResourceNames(),
                    n => n.EndsWith("TableGameRoom.exe", StringComparison.OrdinalIgnoreCase));

            if (resName is null)
            {
                MessageBox.Show("安装包损坏：找不到主程序。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (var src = asm.GetManifestResourceStream(resName)!)
            using (var dst = File.Create(exePath))
            {
                src.CopyTo(dst);
            }

            // 桌面快捷方式
            if (DesktopBox.IsChecked == true)
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var lnk = Path.Combine(desktop, "TABLE 游戏厅.lnk");
                CreateShortcut(lnk, exePath, target);
            }

            StatusText.Text = "安装完成";
            MessageBox.Show($"安装完成！\n\n程序位置：{exePath}", "成功",
                MessageBoxButton.OK, MessageBoxImage.Information);

            if (RunBox.IsChecked == true)
                Process.Start(new ProcessStartInfo(exePath) { WorkingDirectory = target, UseShellExecute = true });

            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = "安装失败";
            MessageBox.Show($"安装失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            InstallBtn.IsEnabled = true;
        }
    }

    /// <summary>用 PowerShell 创建 .lnk（避免依赖 COM 互操作类型）。</summary>
    private static void CreateShortcut(string lnkPath, string targetExe, string workDir)
    {
        var script =
            "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('" + lnkPath.Replace("'", "''") + "');" +
            "$s.TargetPath='" + targetExe.Replace("'", "''") + "';" +
            "$s.WorkingDirectory='" + workDir.Replace("'", "''") + "';" +
            "$s.IconLocation='" + targetExe.Replace("'", "''") + ",0';" +
            "$s.Save()";

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + script + "\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi);
        p?.WaitForExit(8000);
    }
}
