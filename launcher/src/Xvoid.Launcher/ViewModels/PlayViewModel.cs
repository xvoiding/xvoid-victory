using System.Windows.Threading;
using Xvoid.Launcher.Core;

namespace Xvoid.Launcher.ViewModels;

public enum PlayState { Idle, Running, Exited }

public sealed class PlayViewModel : ObservableObject
{
    readonly AppModel _app;
    readonly LogsViewModel _logs;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    EngineProcess? _process;

    PlayState _state;
    string _fps = "-", _ms = "-", _draws = "-", _memory = "-", _commit = "-";
    string? _notice, _advice;

    public PlayViewModel(AppModel app, LogsViewModel logs)
    {
        _app = app;
        _logs = logs;
        PlayCommand = new RelayCommand(Start, () => State != PlayState.Running && _app.NotReadyReason() is null);
        StopCommand = new RelayCommand(() => _process?.Stop(), () => State == PlayState.Running);
        _timer.Tick += (_, _) => Poll();
        _app.EngineChanged += Refresh;
        Refresh();
    }

    public RelayCommand PlayCommand { get; }
    public RelayCommand StopCommand { get; }

    public PlayState State { get => _state; private set { if (Set(ref _state, value)) { Raise(nameof(StateText)); Raise(nameof(IsRunning)); PlayCommand.NotifyCanExecuteChanged(); StopCommand.NotifyCanExecuteChanged(); } } }
    public bool IsRunning => State == PlayState.Running;
    public string StateText => State switch { PlayState.Running => "Running", PlayState.Exited => "Stopped", _ => "Ready" };
    public string Fps { get => _fps; private set => Set(ref _fps, value); }
    public string FrameTime { get => _ms; private set => Set(ref _ms, value); }
    public string Draws { get => _draws; private set => Set(ref _draws, value); }
    public string Memory { get => _memory; private set => Set(ref _memory, value); }
    public string CommitLeft { get => _commit; private set => Set(ref _commit, value); }
    /// <summary>What stands in the way of Play, or a hint about preparing first.</summary>
    public string? Notice { get => _notice; private set { if (Set(ref _notice, value)) Raise(nameof(HasNotice)); } }
    public bool HasNotice => Notice is not null;
    /// <summary>Why the last run ended, in words.</summary>
    public string? Advice { get => _advice; private set { if (Set(ref _advice, value)) Raise(nameof(HasAdvice)); } }
    public bool HasAdvice => Advice is not null;
    public string GameName => _app.Settings.GameExe is { } g ? Path.GetFileNameWithoutExtension(g) : "No game chosen";
    public string EngineLabel => _app.EngineLabel;

    public void Refresh()
    {
        Raise(nameof(GameName));
        Raise(nameof(EngineLabel));
        var reason = _app.NotReadyReason();
        Notice = reason ?? (!_app.ShadersPrepared ? "Shaders are not prepared. The first start will stutter while they are made; Prepare does it ahead of time." : null);
        PlayCommand.NotifyCanExecuteChanged();
    }

    void Start()
    {
        var enginePath = _app.EnginePath!;
        var plan = Plans.Play(_app.Settings, _app.System, _app.Paths, enginePath);
        EngineProcess.PruneLogs(_app.Paths.Logs);
        var log = Path.Combine(_app.Paths.Logs, $"run-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var p = new EngineProcess(log);
        _logs.Attach(p);
        p.Wave += w => LearnHost(w.Min, w.Max, null);
        p.ViewLimit += v => LearnHost(null, null, v);
        p.Exited += code => Ui.Post(() => OnExited(p, code));
        Advice = null;
        Fps = FrameTime = Draws = Memory = CommitLeft = "-";
        try { p.Start(plan); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            p.Dispose();
            throw new InvalidOperationException($"The engine could not be started: {e.Message}");
        }
        _process = p;
        State = PlayState.Running;
        _timer.Start();
    }

    void LearnHost(int? min, int? max, long? views)
    {
        var h = _app.Settings.Host;
        var before = (h.WaveMin, h.WaveMax, h.ViewLimit);
        if (min is { } a && max is { } b) { h.WaveMin = a; h.WaveMax = b; }
        if (views is { } v) h.ViewLimit = v;
        if (before != (h.WaveMin, h.WaveMax, h.ViewLimit)) Ui.Post(_app.Save);
    }

    void Poll()
    {
        var p = _process;
        if (p is null) return;
        var f = p.ReadTitle() ?? p.LastFrame;
        if (f is not null)
        {
            Fps = f.Fps >= 10 ? $"{f.Fps:0}" : $"{f.Fps:0.0}";
            FrameTime = $"{f.Ms:0}";
            Draws = f.Draws is { } d ? $"{d:N0}" : "-";
        }
        if (p.PrivateGb() is { } priv) Memory = $"{priv:0.0}";
        if (p.LastMemory?.CommitLeftGb is { } left) CommitLeft = $"{left:0.0}";
    }

    void OnExited(EngineProcess p, int code)
    {
        if (!ReferenceEquals(p, _process)) return;
        _timer.Stop();
        State = PlayState.Exited;
        Advice = ExitAdvice.Explain(code, p.AllocationFailed, p.PipelinesRefused);
        Refresh();
        p.Dispose();
        _process = null;
    }

    public void StopIfRunning() => _process?.Stop();
}
