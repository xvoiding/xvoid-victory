using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Xvoid.Launcher.Core;

namespace Xvoid.Launcher.ViewModels;

/// <summary>The live log of whatever engine process was started last (a run or a prepare job).</summary>
public sealed class LogsViewModel : ObservableObject
{
    const int MaxLines = 2000;
    readonly AppModel _app;
    readonly ConcurrentQueue<string> _incoming = new();
    readonly List<string> _all = new();
    readonly DispatcherTimer _flush = new() { Interval = TimeSpan.FromMilliseconds(250) };
    EngineProcess? _current;
    string _filter = "";
    string? _status;

    public LogsViewModel(AppModel app)
    {
        _app = app;
        _flush.Tick += (_, _) => Flush();
        _flush.Start();
        OpenFolderCommand = new RelayCommand(() => Process.Start(new ProcessStartInfo(_app.Paths.Logs) { UseShellExecute = true }));
        ReportCommand = new RelayCommand(CreateReport);
        ClearCommand = new RelayCommand(() => { _all.Clear(); Lines.Clear(); });
    }

    public ObservableCollection<string> Lines { get; } = new();
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand ReportCommand { get; }
    public RelayCommand ClearCommand { get; }

    public string Filter { get => _filter; set { if (Set(ref _filter, value)) Rebuild(); } }
    public string? Status { get => _status; private set => Set(ref _status, value); }

    public void Attach(EngineProcess p)
    {
        _current = p;
        p.Line += l => _incoming.Enqueue(l);
        Ui.Post(() => { _all.Clear(); Lines.Clear(); Status = null; });
    }

    /// <summary>The newest lines of the current process, for reports.</summary>
    public string[] Tail() => _current?.Tail() ?? _all.ToArray();

    void Flush()
    {
        if (_incoming.IsEmpty) return;
        var added = new List<string>();
        while (_incoming.TryDequeue(out var l)) added.Add(l);
        _all.AddRange(added);
        if (_all.Count > MaxLines) _all.RemoveRange(0, _all.Count - MaxLines);
        foreach (var l in added)
            if (Matches(l)) Lines.Add(l);
        while (Lines.Count > MaxLines) Lines.RemoveAt(0);
    }

    bool Matches(string l) => _filter.Length == 0 || l.Contains(_filter, StringComparison.OrdinalIgnoreCase);

    void Rebuild()
    {
        Lines.Clear();
        foreach (var l in _all.Where(Matches)) Lines.Add(l);
    }

    void CreateReport()
    {
        var file = Report.Create(_app.Paths, _app.Settings, _app.System, _app.Checks, _app.EngineLabel, Tail());
        Clipboard.SetText(file);
        Status = "Report written; its path is on the clipboard.";
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
    }
}
