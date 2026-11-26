using System.ComponentModel;
using System.Windows;
using Xvoid.Launcher.ViewModels;
using Xvoid.Launcher.Views;

namespace Xvoid.Launcher;

public partial class MainWindow : Window
{
    readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        Icon = App.LoadWindowIcon();
        DataContext = _vm;
        Loaded += (_, _) =>
        {
            if (_vm.NeedsSetup) RunSetup();
            else _vm.Engine.CheckCommand.Execute(null);
        };
    }

    void RunSetup()
    {
        var window = new SetupWindow(_vm.Setup) { Owner = this };
        window.ShowDialog();
        _vm.Play.Refresh();
        _vm.Prepare.Refresh();
        _vm.Page = _vm.App.Settings.SetupDone ? PageId.Play : PageId.Settings;
    }

    void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object sender, RoutedEventArgs e) => Close();

    void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_vm.Play.IsRunning || _vm.Prepare.Busy)
        {
            var answer = MessageBox.Show("The engine is still running. Closing the launcher stops it.", "xvoid Launcher",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.OK) { e.Cancel = true; return; }
        }
        _vm.Shutdown();
    }
}
