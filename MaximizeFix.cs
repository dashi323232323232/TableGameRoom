using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TableGameRoom;

/// <summary>
/// 无边框窗口（WindowStyle=None）最大化时会盖住任务栏，也不考虑多显示器的工作区。
/// 这里拦截 WM_GETMINMAXINFO，把最大化尺寸限制到当前显示器的「工作区」（不含任务栏）。
/// </summary>
internal static class MaximizeFix
{
    private const int WmGetMinMaxInfo = 0x0024;

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    private const uint MonitorDefaultToNearest = 2;

    public static void Apply(Window window)
    {
        if (window.IsLoaded)
        {
            Attach(window);
            return;
        }
        window.SourceInitialized += (_, _) => Attach(window);
    }

    private static void Attach(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        HwndSource.FromHwnd(handle)?.AddHook(Hook);
    }

    private static IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmGetMinMaxInfo) return IntPtr.Zero;

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return IntPtr.Zero;

        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return IntPtr.Zero;

        var mmi = Marshal.PtrToStructure<MinMaxInfo>(lParam);

        // 工作区（已排除任务栏），坐标需转换成相对显示器左上角
        var work = info.WorkArea;
        var mon = info.Monitor;

        mmi.MaxPosition.X = work.Left - mon.Left;
        mmi.MaxPosition.Y = work.Top - mon.Top;
        mmi.MaxSize.X = work.Right - work.Left;
        mmi.MaxSize.Y = work.Bottom - work.Top;

        // 保留窗口自身的最小尺寸约束
        mmi.MinTrackSize.X = (int)Math.Max(mmi.MinTrackSize.X, 0);
        mmi.MinTrackSize.Y = (int)Math.Max(mmi.MinTrackSize.Y, 0);

        Marshal.StructureToPtr(mmi, lParam, true);
        handled = true;
        return IntPtr.Zero;
    }
}
