using Xvoid.Launcher.Core;

namespace Xvoid.Launcher.ViewModels;

public enum PageId { Play, Prepare, Engine, Settings, Logs }

public sealed class MainViewModel : ObservableObject
{
    PageId _page = PageId.Play;

    public MainViewModel()
    {
        App = new AppModel();
        Logs = new LogsViewModel(App);
        Play = new PlayViewModel(App, Logs);
        Prepare = new PrepViewModel(App, Logs);
        Engine = new EngineViewModel(App);
        Settings = new SettingsViewModel(App, () => { Play.Refresh(); Prepare.Refresh(); });
        Setup = new SetupViewModel(App, Settings, Engine);
        App.EngineChanged += () => { Raise(nameof(EngineLabel)); };
    }

    public AppModel App { get; }
    public PlayViewModel Play { get; }
    public PrepViewModel Prepare { get; }
    public EngineViewModel Engine { get; }
    public SettingsViewModel Settings { get; }
    public LogsViewModel Logs { get; }
    public SetupViewModel Setup { get; }

    public string EngineLabel => App.EngineLabel;
    public string Version => typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    public PageId Page
    {
        get => _page;
        set
        {
            if (!Set(ref _page, value)) return;
            foreach (var n in new[] { nameof(OnPlay), nameof(OnPrepare), nameof(OnEngine), nameof(OnSettings), nameof(OnLogs) }) Raise(n);
        }
    }

    public bool OnPlay { get => Page == PageId.Play; set { if (value) Page = PageId.Play; } }
    public bool OnPrepare { get => Page == PageId.Prepare; set { if (value) Page = PageId.Prepare; } }
    public bool OnEngine { get => Page == PageId.Engine; set { if (value) Page = PageId.Engine; } }
    public bool OnSettings { get => Page == PageId.Settings; set { if (value) Page = PageId.Settings; } }
    public bool OnLogs { get => Page == PageId.Logs; set { if (value) Page = PageId.Logs; } }

    public bool NeedsSetup => !App.Settings.SetupDone;

    public void Shutdown()
    {
        Play.StopIfRunning();
        Prepare.StopIfRunning();
    }
}
