using System.Windows;
using Xvoid.Launcher.Core;

namespace Xvoid.Launcher;

public partial class App : Application
{
    static Mutex? _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        // One launcher at a time: two would fight over the settings file and the engine's cache.
        _single = new Mutex(true, "xvoid-launcher-single-instance", out var first);
        if (!first)
        {
            MessageBox.Show($"{Branding.AppName} is already open.", Branding.AppName);
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, Branding.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        base.OnStartup(e);
        new MainWindow().Show();
    }
}
