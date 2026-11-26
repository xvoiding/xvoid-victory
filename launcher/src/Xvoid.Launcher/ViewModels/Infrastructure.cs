using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace Xvoid.Launcher.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RelayCommand : ICommand
{
    readonly Func<object?, Task>? _async;
    readonly Action<object?>? _sync;
    readonly Func<bool>? _canExecute;
    bool _running;

    public RelayCommand(Action<object?> execute, Func<bool>? canExecute = null) { _sync = execute; _canExecute = canExecute; }
    public RelayCommand(Func<object?, Task> execute, Func<bool>? canExecute = null) { _async = execute; _canExecute = canExecute; }
    public RelayCommand(Action execute, Func<bool>? canExecute = null) : this(_ => execute(), canExecute) { }
    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : this(_ => execute(), canExecute) { }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_running && (_canExecute?.Invoke() ?? true);
    public void NotifyCanExecuteChanged() => Ui.Post(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        try
        {
            if (_async is not null)
            {
                _running = true;
                NotifyCanExecuteChanged();
                try { await _async(parameter); }
                finally { _running = false; NotifyCanExecuteChanged(); }
            }
            else _sync!(parameter);
        }
        catch (Exception e)
        {
            // A command must not take the launcher down; say what happened.
            MessageBox.Show(e.Message, Core.Branding.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

public static class Ui
{
    public static void Post(Action action)
    {
        var d = Application.Current?.Dispatcher;
        if (d is null) return;
        if (d.CheckAccess()) action(); else d.BeginInvoke(action);
    }
}
