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
        // The toggle also lives on the Play and Engine pages; what depends on it here follows.
        _app.EngineChanged += () =>
        {
            foreach (var n in new[] { nameof(EngineAuto), nameof(EngineNvidia), nameof(EngineAmd), nameof(EngineHint), nameof(IsNvidia), nameof(AmbientLight), nameof(CommandPreview) }) Raise(n);
        };
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
        S.ShadersPrepared.Clear();
        S.PipelinesPrepared.Clear();
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

    // Which engine: the same toggle as on the Play page.
    public bool EngineAuto { get => S.Engine == EngineChoice.Auto; set { if (value) _app.Choice = EngineChoice.Auto; } }
    public bool EngineNvidia { get => S.Engine == EngineChoice.Nvidia; set { if (value) _app.Choice = EngineChoice.Nvidia; } }
    public bool EngineAmd { get => S.Engine == EngineChoice.Amd; set { if (value) _app.Choice = EngineChoice.Amd; } }
    public string EngineHint => S.Engine == EngineChoice.Auto
        ? $"Auto picks the {_app.KindName} engine for {_app.System.BestGpu?.Name ?? "this PC"}."
        : $"The {_app.KindName} engine is chosen. Auto would pick it from the graphics card.";

    /// <summary>DLSS, frame generation and the resolution are the NVIDIA engine's settings; the AMD engine ignores them.</summary>
    public bool IsNvidia => _app.Kind == EngineKind.Nvidia;

    public bool ResolutionAuto { get => S.Resolution == Resolution.Auto; set { if (value) SetResolution(Resolution.Auto); } }
    public bool Resolution1440 { get => S.Resolution == Resolution.P1440; set { if (value) SetResolution(Resolution.P1440); } }
    public bool Resolution1080 { get => S.Resolution == Resolution.P1080To1440; set { if (value) SetResolution(Resolution.P1080To1440); } }
    void SetResolution(Resolution r)
    {
        S.Resolution = r;
        _app.Save();
        foreach (var n in new[] { nameof(ResolutionAuto), nameof(Resolution1440), nameof(Resolution1080), nameof(CommandPreview) }) Raise(n);
        _changed();
    }

    public bool DlssOff { get => S.Dlss == DlssMode.Off; set { if (value) SetDlss(DlssMode.Off); } }
    public bool DlssQuality { get => S.Dlss == DlssMode.Quality; set { if (value) SetDlss(DlssMode.Quality); } }
    public bool DlssBalanced { get => S.Dlss == DlssMode.Balanced; set { if (value) SetDlss(DlssMode.Balanced); } }
    public bool DlssPerformance { get => S.Dlss == DlssMode.Performance; set { if (value) SetDlss(DlssMode.Performance); } }
    void SetDlss(DlssMode m)
    {
        S.Dlss = m;
        _app.Save();
        foreach (var n in new[] { nameof(DlssOff), nameof(DlssQuality), nameof(DlssBalanced), nameof(DlssPerformance), nameof(DlssNote), nameof(CommandPreview) }) Raise(n);
        _changed();
    }

    public bool FrameGenReady { get => S.FrameGeneration == FrameGeneration.Ready; set { if (value) SetFrameGen(FrameGeneration.Ready); } }
    public bool FrameGenOff { get => S.FrameGeneration == FrameGeneration.Off; set { if (value) SetFrameGen(FrameGeneration.Off); } }
    public bool FrameGen2x { get => S.FrameGeneration == FrameGeneration.On2x; set { if (value) SetFrameGen(FrameGeneration.On2x); } }
    void SetFrameGen(FrameGeneration f)
    {
        S.FrameGeneration = f;
        _app.Save();
        foreach (var n in new[] { nameof(FrameGenReady), nameof(FrameGenOff), nameof(FrameGen2x), nameof(DlssNote), nameof(CommandPreview) }) Raise(n);
        _changed();
    }

    /// <summary>What DLSS and frame generation will do on this PC, as it stands now.</summary>
    public string DlssNote
    {
        get
        {
            if (S.Dlss == DlssMode.Off) return "DLSS is off: the game upscales itself.";
            if (!_app.Paths.HasStreamline) return "DLSS needs NVIDIA's files: use \"Get NVIDIA DLSS files\" on the Prepare page. Until then the game upscales itself.";
            var gpu = _app.System.BestGpu?.Name ?? "";
            if (S.FrameGeneration == FrameGeneration.On2x && !EngineKinds.HasDlssFrameGeneration(gpu))
                return "2x frame generation needs an RTX 40 series card or newer; this one is not, so it will not work.";
            if (S.FrameGeneration == FrameGeneration.On2x)
                return "DLSS and 2x frame generation are on. Frame generation doubles what is shown, not what the game can compute: a low frame rate before it stays choppy.";
            return "DLSS is on. Frame generation is " + (S.FrameGeneration == FrameGeneration.Ready ? "ready but off; the F1 menu in the game can switch it on." : "off.");
        }
    }

    public bool WarmUp { get => S.WarmUp; set { S.WarmUp = value; Commit(); } }

    public int TextureBudgetMb { get => S.TextureBudgetMb; set { S.TextureBudgetMb = Math.Clamp(value, 0, 16384); Commit(); } }
    public bool RayTracing { get => S.RayTracing; set { S.RayTracing = value; Commit(); } }
    /// <summary>The ambient light that stands in for the title's ray tracing. Not touched, it is the engine's own default: on for NVIDIA, off for AMD.</summary>
    public bool AmbientLight
    {
        get => S.AmbientLight ?? (_app.Kind == EngineKind.Nvidia);
        set { S.AmbientLight = value; Commit(); }
    }
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
                var kind = _app.Kind;
                return Plans.Play(S, _app.System, _app.Paths, _app.EnginePath ?? "victory.exe", kind).Describe();
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
