using Xvoid.Launcher.Core;

namespace Xvoid.Launcher.ViewModels;

/// <summary>What every page shares: the settings, the services, and what was last read from the PC.</summary>
public sealed class AppModel : ObservableObject
{
    public AppPaths Paths { get; } = new();
    public Settings Settings { get; }
    public EngineManager Engines { get; }

    SystemSnapshot _system;
    IReadOnlyList<Check> _checks = Array.Empty<Check>();

    public AppModel()
    {
        Paths.EnsureCreated();
        Settings = Settings.Load(Paths.SettingsFile);
        Engines = new EngineManager(Paths);
        _system = SystemInfo.Read(Paths.Cache);
        RefreshChecks();
    }

    public SystemSnapshot System { get => _system; private set => Set(ref _system, value); }
    public IReadOnlyList<Check> Checks { get => _checks; private set => Set(ref _checks, value); }

    public event Action? EngineChanged;

    public string? EnginePath => Engines.Resolve(Settings);
    public string EngineLabel => Settings.EngineSource == EngineSource.Local
        ? "Local build"
        : Settings.InstalledEngineTag ?? "Not installed";

    public void Save() => Settings.Save(Paths.SettingsFile);

    public void RefreshChecks()
    {
        System = SystemInfo.Read(Paths.Cache);
        var exe = EnginePath;
        var pref = exe is not null && SystemFixes.IsHighPerformance(exe);
        Checks = Core.Checks.Run(System, pref);
    }

    public void NotifyEngineChanged()
    {
        Raise(nameof(EnginePath));
        Raise(nameof(EngineLabel));
        EngineChanged?.Invoke();
    }

    /// <summary>Why Play cannot start yet, or null.</summary>
    public string? NotReadyReason()
    {
        if (Settings.GameExe is null || GameLocator.Validate(Settings.GameExe) is not null) return "Choose your game's folder in Settings.";
        if (EnginePath is null) return "Install the engine on the Engine page.";
        if (Core.Checks.Blocking(Checks)) return "This PC does not meet the minimum; see Settings.";
        return null;
    }

    /// <summary>The cache holds translated shaders for this engine.</summary>
    public bool ShadersPrepared => Settings.ShadersPreparedFor is { } f && f == EngineLabel;
    public bool PipelinesPrepared => Settings.PipelinesPreparedFor is { } f && f == EngineLabel;
    public bool PipelineListExists =>
        Directory.Exists(Paths.PipelineFolder) && Directory.EnumerateFileSystemEntries(Paths.PipelineFolder).Any();
}
