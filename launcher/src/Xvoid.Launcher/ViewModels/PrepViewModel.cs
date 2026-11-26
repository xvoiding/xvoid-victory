using Xvoid.Launcher.Core;

namespace Xvoid.Launcher.ViewModels;

/// <summary>Translating every shader ahead of time, then making the pipelines earlier runs drew with.</summary>
public sealed class PrepViewModel : ObservableObject
{
    readonly AppModel _app;
    readonly LogsViewModel _logs;
    EngineProcess? _job;
    bool _cancelRequested;
    bool _busy;
    double _progress;
    bool _indeterminate;
    string _message = "";
    string _lastLine = "";

    public PrepViewModel(AppModel app, LogsViewModel logs)
    {
        _app = app;
        _logs = logs;
        ShadersCommand = new RelayCommand(() => Run(shaders: true), () => !Busy && Ready);
        PipelinesCommand = new RelayCommand(() => Run(shaders: false), () => !Busy && Ready && _app.PipelineListExists);
        CancelCommand = new RelayCommand(() => { _cancelRequested = true; _job?.Stop(); }, () => Busy);
        _app.EngineChanged += Refresh;
    }

    public RelayCommand ShadersCommand { get; }
    public RelayCommand PipelinesCommand { get; }
    public RelayCommand CancelCommand { get; }

    bool Ready => _app.EnginePath is not null && _app.Settings.GameExe is not null;

    public bool Busy { get => _busy; private set { if (Set(ref _busy, value)) NotifyAll(); } }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }
    public bool Indeterminate { get => _indeterminate; private set => Set(ref _indeterminate, value); }
    public string Message { get => _message; private set { if (Set(ref _message, value)) Raise(nameof(HasMessage)); } }
    public bool HasMessage => _message.Length > 0;
    public string LastLine { get => _lastLine; private set => Set(ref _lastLine, value); }

    public string ShadersState => _app.ShadersPrepared ? "Done for this engine build" : "Not done";
    public string PipelinesState => _app.PipelinesPrepared
        ? "Done for this engine build"
        : _app.PipelineListExists ? "Ready to build" : "Play once first so the engine can write down what it draws with";
    public string HostNote => _app.Settings.Host.IsKnown
        ? $"Using this GPU's numbers from a run: waves {_app.Settings.Host.WaveMin}-{_app.Settings.Host.WaveMax}."
        : "This GPU's numbers are learned from the first run; until then a vendor default is used.";

    public void Refresh()
    {
        Raise(nameof(ShadersState)); Raise(nameof(PipelinesState)); Raise(nameof(HostNote));
        NotifyAll();
    }

    void NotifyAll()
    {
        ShadersCommand.NotifyCanExecuteChanged();
        PipelinesCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    void Run(bool shaders)
    {
        var enginePath = _app.EnginePath!;
        string? caveat = null;
        var plan = shaders
            ? Plans.Shaders(_app.Settings, _app.System, _app.Paths, enginePath, out caveat)
            : Plans.Pipelines(_app.Settings, _app.Paths, enginePath);
        var tag = _app.EngineLabel;
        var log = Path.Combine(_app.Paths.Logs, $"{(shaders ? "shaders" : "pipelines")}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var p = new EngineProcess(log);
        _logs.Attach(p);
        _job = p;
        p.Line += l => Ui.Post(() => LastLine = l.Length > 160 ? l[..160] : l);
        p.Warm += w => Ui.Post(() =>
        {
            Indeterminate = false;
            Progress = w.Total == 0 ? 0 : (double)w.Done / w.Total;
            Message = $"{w.Done:N0} of {w.Total:N0} pipelines, {w.Seconds / 60}:{w.Seconds % 60:00}";
        });
        _cancelRequested = false;
        p.Exited += code => Ui.Post(() =>
        {
            Busy = false;
            Progress = code == 0 ? 1 : Progress;
            if (code == 0)
            {
                if (shaders) _app.Settings.ShadersPreparedFor = tag; else _app.Settings.PipelinesPreparedFor = tag;
                _app.Save();
                Message = shaders ? "Shaders translated." : "Pipelines made.";
            }
            else Message = _cancelRequested ? "Stopped. Starting it again carries on where it left off." : $"Ended with code {code}. See Logs.";
            Refresh();
            p.Dispose();
            _job = null;
        });

        Message = caveat ?? (shaders ? "Translating every shader. This takes a few minutes; leave the PC alone." : "Making pipelines on this GPU. This can take 10 to 60 minutes and can be stopped.");
        Progress = 0;
        Indeterminate = shaders;
        try { p.Start(plan); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            p.Dispose(); _job = null;
            throw new InvalidOperationException($"The engine could not be started: {e.Message}");
        }
        Busy = true;
    }

    public void StopIfRunning() => _job?.Stop();
}
