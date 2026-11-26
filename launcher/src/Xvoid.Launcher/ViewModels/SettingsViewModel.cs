using System.Collections.ObjectModel;
using Microsoft.Win32;
using Xvoid.Launcher.Core;

namespace Xvoid.Launcher.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    readonly AppModel _app;
    readonly Action _changed;

    public SettingsViewModel(AppModel app, Action changed)
    {
        _app = app;
        _changed = changed;
        BrowseGameCommand = new RelayCommand(BrowseGame);
        RecheckCommand = new RelayCommand(Recheck);
        ResetHostCommand = new RelayCommand(() => { _app.Settings.Host = new HostProfile(); _app.Save(); Raise(nameof(HostText)); });
        Recheck();
    }

    public RelayCommand BrowseGameCommand { get; }
    public RelayCommand RecheckCommand { get; }
    public RelayCommand ResetHostCommand { get; }
    public ObservableCollection<CheckItem> Checks { get; } = new();

    Settings S => _app.Settings;

    void Commit([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        _app.Save();
        Raise(name);
        Raise(nameof(CommandPreview));
        _changed();
    }

    public string GamePath => S.GameExe ?? "No folder chosen";
    public string? GameProblem => S.GameExe is null ? null : GameLocator.Validate(S.GameExe);

    void BrowseGame()
    {
        var dialog = new OpenFolderDialog { Title = "Choose the folder that holds your game's files" };
        if (dialog.ShowDialog() != true) return;
        var exe = GameLocator.Find(dialog.FolderName);
        if (exe is null) throw new InvalidOperationException("No program was found in that folder. Choose the folder that holds the game's executable.");
        S.GameExe = exe;
        // A different game folder has its own shaders.
        S.ShadersPreparedFor = null;
        S.PipelinesPreparedFor = null;
        _app.Save();
        Raise(nameof(GamePath)); Raise(nameof(GameProblem)); Raise(nameof(CommandPreview));
        _app.NotifyEngineChanged();
        _changed();
    }

    // Presets and options are radio groups; each property is true when it is the current choice.
    public bool PresetAuto { get => S.Preset == Preset.Auto; set { if (value) { S.Preset = Preset.Auto; Commit(); Raise(nameof(PresetSafe)); Raise(nameof(PresetLow)); } } }
    public bool PresetSafe { get => S.Preset == Preset.Safe; set { if (value) { S.Preset = Preset.Safe; Commit(); Raise(nameof(PresetAuto)); Raise(nameof(PresetLow)); } } }
    public bool PresetLow { get => S.Preset == Preset.LowMemory; set { if (value) { S.Preset = Preset.LowMemory; Commit(); Raise(nameof(PresetAuto)); Raise(nameof(PresetSafe)); } } }

    public bool ConsoleX { get => S.Console == "series-x"; set { if (value) { S.Console = "series-x"; Commit(); Raise(nameof(ConsoleS)); } } }
    public bool ConsoleS { get => S.Console == "series-s"; set { if (value) { S.Console = "series-s"; Commit(); Raise(nameof(ConsoleX)); } } }

    public bool Threads0 { get => S.ListThreads == 0; set { if (value) SetThreads(0); } }
    public bool Threads2 { get => S.ListThreads == 2; set { if (value) SetThreads(2); } }
    public bool Threads4 { get => S.ListThreads == 4; set { if (value) SetThreads(4); } }
    public bool Threads6 { get => S.ListThreads == 6; set { if (value) SetThreads(6); } }
    public bool Threads8 { get => S.ListThreads == 8; set { if (value) SetThreads(8); } }

    void SetThreads(int n)
    {
        S.ListThreads = n;
        _app.Save();
        foreach (var p in new[] { nameof(Threads0), nameof(Threads2), nameof(Threads4), nameof(Threads6), nameof(Threads8), nameof(CommandPreview) }) Raise(p);
        _changed();
    }

    public int TextureBudgetMb { get => S.TextureBudgetMb; set { S.TextureBudgetMb = Math.Clamp(value, 0, 16384); Commit(); } }
    public bool RayTracing { get => S.RayTracing; set { S.RayTracing = value; Commit(); } }
    public bool AmbientLight { get => S.AmbientLight; set { S.AmbientLight = value; Commit(); } }
    public bool RecordPipelines { get => S.RecordPipelines; set { S.RecordPipelines = value; Commit(); } }

    public string HostText => S.Host.IsKnown
        ? $"Learned from a run: waves {S.Host.WaveMin}-{S.Host.WaveMax}, {S.Host.ViewLimit:N0} views."
        : "Not learned yet; it is read from the first run.";

    public string AutoThreadsText => $"Auto uses {Plans.AutoListThreads(_app.System.LogicalProcessors)} of {_app.System.LogicalProcessors} logical processors.";

    public string CommandPreview
    {
        get
        {
            try
            {
                return _app.EnginePath is null && S.GameExe is not null
                    ? Plans.Play(S, _app.System, _app.Paths, "victory.exe").Describe()
                    : _app.EnginePath is { } e ? Plans.Play(S, _app.System, _app.Paths, e).Describe() : "Choose your game to see the command.";
            }
            catch (InvalidOperationException) { return "Choose your game to see the command."; }
        }
    }

    void Recheck()
    {
        _app.RefreshChecks();
        Checks.Clear();
        foreach (var c in _app.Checks) Checks.Add(new CheckItem(c, _app, Recheck));
        Raise(nameof(AutoThreadsText));
        _changed();
    }
}
