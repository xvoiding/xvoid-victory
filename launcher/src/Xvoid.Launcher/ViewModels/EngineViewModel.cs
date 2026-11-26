using System.Diagnostics;
using Microsoft.Win32;
using Xvoid.Launcher.Core;

namespace Xvoid.Launcher.ViewModels;

public sealed class EngineViewModel : ObservableObject
{
    readonly AppModel _app;
    EngineRelease? _latest;
    string _status = "Not checked yet.";
    double _progress;
    bool _showProgress;

    public EngineViewModel(AppModel app)
    {
        _app = app;
        CheckCommand = new RelayCommand(CheckAsync);
        InstallCommand = new RelayCommand(InstallAsync, () => _latest is not null && CanInstall());
        BrowseCommand = new RelayCommand(Browse);
        OpenFolderCommand = new RelayCommand(() => Process.Start(new ProcessStartInfo(_app.Paths.Engines) { UseShellExecute = true }));
    }

    public RelayCommand CheckCommand { get; }
    public RelayCommand InstallCommand { get; }
    public RelayCommand BrowseCommand { get; }
    public RelayCommand OpenFolderCommand { get; }

    public string Installed => _app.EngineLabel;
    public string? Latest => _latest is null ? null : $"{_latest.Tag}{(_latest.Prerelease ? " (pre-release)" : "")}";
    public string Status { get => _status; private set => Set(ref _status, value); }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }
    public bool ShowProgress { get => _showProgress; private set => Set(ref _showProgress, value); }

    public bool UseLocal
    {
        get => _app.Settings.EngineSource == EngineSource.Local;
        set
        {
            _app.Settings.EngineSource = value ? EngineSource.Local : EngineSource.Release;
            _app.Save();
            Raise(); Raise(nameof(UseRelease)); Raise(nameof(LocalPath));
            _app.NotifyEngineChanged();
            Raise(nameof(Installed));
        }
    }
    public bool UseRelease { get => !UseLocal; set => UseLocal = !value; }
    public string LocalPath => _app.Settings.LocalEnginePath ?? "No file chosen";

    bool CanInstall() => _latest is not null && (_app.Settings.InstalledEngineTag != _latest.Tag || _app.Engines.ExeFor(_app.Settings.InstalledEngineTag) is null);

    async Task CheckAsync()
    {
        Status = "Checking GitHub...";
        try
        {
            _latest = await _app.Engines.LatestAsync();
            Raise(nameof(Latest));
            if (_latest is null) Status = "No engine build has been published yet. You can choose a local build below.";
            else if (EngineManager.IsNewer(_app.Settings.InstalledEngineTag, _latest.Tag) || _app.Engines.ExeFor(_app.Settings.InstalledEngineTag) is null)
                Status = $"{_latest.Tag} is available ({_latest.AssetSize / (1024 * 1024)} MB).";
            else Status = "You have the newest build.";
            InstallCommand.NotifyCanExecuteChanged();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            Status = $"Could not reach GitHub: {e.Message}";
        }
    }

    async Task InstallAsync()
    {
        if (_latest is null) return;
        ShowProgress = true; Progress = 0;
        Status = $"Downloading {_latest.Tag}...";
        try
        {
            var progress = new Progress<double>(v => Progress = v);
            await _app.Engines.InstallAsync(_latest, progress);
            var old = _app.Settings.InstalledEngineTag;
            _app.Settings.InstalledEngineTag = _latest.Tag;
            _app.Settings.EngineSource = EngineSource.Release;
            // A new build has its own translations and pipelines to make.
            _app.Settings.ShadersPreparedFor = null;
            _app.Settings.PipelinesPreparedFor = null;
            _app.Save();
            _app.Engines.Prune(_latest.Tag);
            Status = $"{_latest.Tag} installed." + (old is null ? "" : " Prepare again for this build.");
            _app.NotifyEngineChanged();
            Raise(nameof(Installed)); Raise(nameof(UseLocal)); Raise(nameof(UseRelease));
        }
        catch (Exception e) when (e is HttpRequestException or InvalidDataException or InvalidOperationException or IOException or TaskCanceledException)
        {
            Status = e.Message;
        }
        finally { ShowProgress = false; InstallCommand.NotifyCanExecuteChanged(); }
    }

    void Browse()
    {
        var dialog = new OpenFileDialog { Title = "Choose victory.exe", Filter = "victory.exe|victory.exe|Programs|*.exe" };
        if (dialog.ShowDialog() != true) return;
        _app.Settings.LocalEnginePath = dialog.FileName;
        _app.Settings.EngineSource = EngineSource.Local;
        _app.Save();
        Raise(nameof(LocalPath)); Raise(nameof(UseLocal)); Raise(nameof(UseRelease));
        _app.NotifyEngineChanged();
        Raise(nameof(Installed));
    }
}
