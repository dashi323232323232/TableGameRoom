using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TableGameRoom;

/// <summary>
/// Windows 11 原生窗口圆角（DWM），让无边框窗口四角像系统应用一样是圆的。
/// 低版本 Windows 自动忽略，不影响运行。
/// </summary>
public static class RoundedCorners
{
    private const int DwmwaWindowCornerPreference = 33;

    private enum CornerPreference
    {
        Default = 0,
        DoNotRound = 1,
        Round = 2,
        RoundSmall = 3
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>在窗口创建后调用，启用系统圆角。</summary>
    public static void Apply(Window window)
    {
        if (window.IsLoaded)
        {
            ApplyCore(window);
            return;
        }

        window.SourceInitialized += (_, _) => ApplyCore(window);
    }

    private static void ApplyCore(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            var preference = (int)CornerPreference.Round;
            // Win11 22H2+ 生效；旧版本返回非 0，忽略即可。
            DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch
        {
            // 非 Win11 或调用失败时静默跳过，窗口仍为直角。
        }
    }
}
