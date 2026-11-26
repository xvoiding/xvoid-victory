using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xvoid.Launcher.Core;

namespace Xvoid.Launcher;

public partial class App : Application
{
    const string LockName = "xvoid-launcher-single-instance";
    const string WakeName = "xvoid-launcher-wake";
    const string ProcessName = "xvoid-launcher";

    static Mutex? _single;
    static EventWaitHandle? _wake;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => WriteCrashLog(args.ExceptionObject as Exception);

        if (!TakeLock())
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            Die(ex);
        }
    }

    /// <summary>The window icon. A problem with it should cost the icon, not the launcher.</summary>
    internal static ImageSource? LoadWindowIcon()
    {
        try
        {
            var icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/xvoid.ico", UriKind.Absolute),
                BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            icon.Freeze();
            return icon;
        }
        catch (Exception e)
        {
            WriteCrashLog(e);
            return null;
        }
    }

    /// <summary>
    /// One launcher at a time: two would fight over the settings file and the engine's cache.
    /// A second start brings the first window forward instead of complaining.
    /// </summary>
    bool TakeLock()
    {
        _single = new Mutex(true, LockName, out var first);
        if (first)
        {
            ListenForWake();
            return true;
        }

        if (WakeRunningInstance()) return false;

        // Nobody answered, so the lock is held by a process with no window: an older build that
        // hit an error while starting and kept running. Clear it away and carry on.
        _single.Dispose();
        _single = null;
        if (ClearLeftovers())
        {
            for (var i = 0; i < 10 && _single is null; i++)
            {
                var m = new Mutex(true, LockName, out var got);
                if (got) { _single = m; ListenForWake(); return true; }
                m.Dispose();
                Thread.Sleep(200);
            }
        }

        MessageBox.Show(
            $"{Branding.AppName} is already running but has no window.\n\nEnd \"xvoid-launcher.exe\" in Task Manager (Details tab) and start it again.",
            Branding.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    static bool WakeRunningInstance()
    {
        try
        {
            using var wake = EventWaitHandle.OpenExisting(WakeName);
            wake.Set();
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    void ListenForWake()
    {
        var wake = new EventWaitHandle(false, EventResetMode.AutoReset, WakeName);
        _wake = wake;
        new Thread(() =>
        {
            while (wake.WaitOne())
                Dispatcher.BeginInvoke(new Action(BringForward));
        })
        { IsBackground = true, Name = "xvoid-wake" }.Start();
    }

    void BringForward()
    {
        if (MainWindow is not { } w) return;
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Show();
        w.Activate();
        w.Topmost = true;
        w.Topmost = false;
    }

    /// <summary>Ends other copies of this app that have been running a while without ever showing a window.</summary>
    static bool ClearLeftovers()
    {
        var cleared = false;
        foreach (var p in Process.GetProcessesByName(ProcessName))
        {
            using (p)
            {
                try
                {
                    if (p.Id == Environment.ProcessId) continue;
                    if (p.MainWindowHandle != IntPtr.Zero) continue;
                    if (DateTime.Now - p.StartTime < TimeSpan.FromSeconds(15)) continue; // still starting up
                    p.Kill();
                    p.WaitForExit(3000);
                    cleared = true;
                }
                catch (Exception) { /* not ours to end, or already gone */ }
            }
        }
        return cleared;
    }

    void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        args.Handled = true;
        // Once the window is up, an error in one command shouldn't take the app down.
        // Before that there is nothing to keep alive, so show it and leave.
        if (MainWindow is { IsVisible: true })
            Report(args.Exception);
        else
            Die(args.Exception);
    }

    void Die(Exception ex)
    {
        Report(ex);
        Environment.Exit(1);
    }

    static void Report(Exception ex)
    {
        var log = WriteCrashLog(ex);
        var where = log is null ? "" : $"\n\nDetails: {log}";
        // WPF wraps most start-up failures in an XamlParseException whose message says nothing; the cause is underneath.
        var root = ex.GetBaseException();
        var cause = ReferenceEquals(root, ex) ? "" : $"\n\nCause: {root.GetType().Name}: {root.Message}";
        MessageBox.Show($"{ex.GetType().Name}: {ex.Message}{cause}{where}", Branding.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    static string? WriteCrashLog(Exception? ex)
    {
        if (ex is null) return null;
        try
        {
            var paths = new AppPaths();
            paths.EnsureCreated();
            var file = Path.Combine(paths.Logs, "launcher-crash.txt");
            File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}\n\n");
            return file;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
