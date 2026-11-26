using System.Windows;
using System.Windows.Media;
using Xvoid.Launcher.Core;

namespace Xvoid.Launcher.ViewModels;

/// <summary>One line of the system check, with the repair it offers.</summary>
public sealed class CheckItem
{
    public CheckItem(Check check, AppModel app, Action? changed = null)
    {
        Check = check;
        Title = check.Title;
        Detail = check.Detail;
        Mark = check.Severity switch { Severity.Ok => "OK", Severity.Warn => "CHECK", _ => "NO" };
        Brush = (Brush)Application.Current.FindResource(check.Severity switch { Severity.Ok => "Good", Severity.Warn => "Warn", _ => "Bad" });
        FixLabel = check.Fix switch { Fix.PagingFile => "Set 64 GB paging file", Fix.GpuPreference => "Use the fast GPU", _ => null };
        FixCommand = new RelayCommand(() => Apply(app, check.Fix, changed));
    }

    public Check Check { get; }
    public string Title { get; }
    public string Detail { get; }
    public string Mark { get; }
    public Brush Brush { get; }
    public string? FixLabel { get; }
    public bool HasFix => FixLabel is not null;
    public RelayCommand FixCommand { get; }

    static void Apply(AppModel app, Fix fix, Action? changed)
    {
        switch (fix)
        {
            case Fix.GpuPreference:
                if (app.EnginePath is null)
                    throw new InvalidOperationException("Install the engine first; Windows keeps this setting per program.");
                SystemFixes.SetHighPerformance(app.EnginePath);
                break;
            case Fix.PagingFile:
                var ok = MessageBox.Show(
                    "This sets a fixed 64 GB paging file on the drive with the most free space (keeping 15 GB free) and turns off automatic management. " +
                    "Windows will ask for administrator rights, and you need to restart afterwards.",
                    "Paging file", MessageBoxButton.OKCancel, MessageBoxImage.Information);
                if (ok != MessageBoxResult.OK) return;
                if (!SystemFixes.SetPagingFile())
                    throw new InvalidOperationException("The paging file was not changed (the prompt was declined, or there is not enough free space).");
                MessageBox.Show("Done. Restart Windows for it to apply.", "Paging file");
                break;
        }
        app.RefreshChecks();
        changed?.Invoke();
    }
}
