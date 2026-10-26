using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows;

namespace TableGameRoom;
public partial class App : Application
{
    private Mutex? _instanceMutex;
    private bool _ownsMutex;
    public App()
    {
        DispatcherUnhandledException += (_, e) => { Log(e.Exception); e.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { Log(e.Exception); e.SetObserved(); };
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        // 允许通过 --multi 参数跳过单实例限制（用于同机多开测试/联机演示）
        var allowMulti = e.Args.Any(a => a.Equals("--multi", StringComparison.OrdinalIgnoreCase));

        if (!allowMulti)
        {
            var user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
            _instanceMutex = new Mutex(false, @"Local\TableGameRoom.SingleInstance." + user);
            try { _ownsMutex = _instanceMutex.WaitOne(0); }
            catch (AbandonedMutexException) { _ownsMutex = true; }
            if (!_ownsMutex) { Shutdown(); return; }
        }
        else
        {
            _ownsMutex = false;
        }

        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
    internal static void Log(Exception? ex)
    {
        try
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TableGameRoom");
            Directory.CreateDirectory(d);
            File.AppendAllText(Path.Combine(d, "crash.log"), $"{DateTime.Now:u} {ex?.GetType().Name}: {ex?.Message}\n{ex?.StackTrace}\n");
        }
        catch { }
    }
}
