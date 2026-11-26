using System.Diagnostics;
using System.Text;

namespace Xvoid.Launcher.Core;

/// <summary>
/// One engine process: starts it, keeps its output in a log file and a short in-memory tail, turns what it prints into
/// statistics, and ends the whole process tree when asked.
/// </summary>
public sealed class EngineProcess : IDisposable
{
    readonly Process _process = new();
    readonly object _gate = new();
    readonly Queue<string> _tail = new();
    StreamWriter? _log;
    int _drawsPending;
    bool _allocationFailed, _refused;

    public const int TailLines = 3000;

    public string LogFile { get; }
    public bool Running { get; private set; }
    public int? ExitCode { get; private set; }
    public bool AllocationFailed => _allocationFailed;
    public bool PipelinesRefused => _refused;
    public FrameStat? LastFrame { get; private set; }
    public MemoryStat? LastMemory { get; private set; }

    /// <summary>Raised on a thread of the pool for every cleaned log line.</summary>
    public event Action<string>? Line;
    public event Action<FrameStat>? Frame;
    public event Action<MemoryStat>? Memory;
    public event Action<(int Min, int Max)>? Wave;
    public event Action<long>? ViewLimit;
    public event Action<WarmProgress>? Warm;
    public event Action<int>? Exited;

    public EngineProcess(string logFile) => LogFile = logFile;

    public void Start(LaunchPlan plan)
    {
        var info = new ProcessStartInfo(plan.EnginePath)
        {
            WorkingDirectory = plan.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in plan.Arguments) info.ArgumentList.Add(a);
        // The engine reads only VICTORY_* from its environment; leftovers from the launcher's own would change what runs.
        foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("VICTORY_", StringComparison.OrdinalIgnoreCase)).ToList())
            info.Environment.Remove(key);
        foreach (var (k, v) in plan.Environment) info.Environment[k] = v;

        Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
        _log = new StreamWriter(LogFile, false, new UTF8Encoding(false)) { AutoFlush = true };
        _log.WriteLine("# " + plan.Describe().Replace("\n", "\n# "));

        _process.StartInfo = info;
        _process.EnableRaisingEvents = true;
        _process.OutputDataReceived += (_, e) => OnData(e.Data);
        _process.ErrorDataReceived += (_, e) => OnData(e.Data);
        _process.Exited += (_, _) => OnExited();
        _process.Start();
        Running = true;
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    /// <summary>The title bar the engine sets is the cheapest reliable frame counter.</summary>
    public FrameStat? ReadTitle()
    {
        try
        {
            if (!Running) return null;
            _process.Refresh();
            return LogParser.ParseTitle(_process.MainWindowTitle);
        }
        catch (InvalidOperationException) { return null; }
    }

    public double? PrivateGb()
    {
        try
        {
            if (!Running) return null;
            _process.Refresh();
            return _process.PrivateMemorySize64 / (1024.0 * 1024 * 1024);
        }
        catch (InvalidOperationException) { return null; }
    }

    public string[] Tail()
    {
        lock (_gate) return _tail.ToArray();
    }

    void OnData(string? raw)
    {
        if (raw is null) return;
        var line = LogParser.Clean(raw);
        if (line.Length == 0) return;
        lock (_gate)
        {
            _tail.Enqueue(line);
            while (_tail.Count > TailLines) _tail.Dequeue();
            try { _log?.WriteLine(line); } catch (ObjectDisposedException) { }
        }

        if (LogParser.IsAllocationFailure(line)) _allocationFailed = true;
        if (LogParser.IsPipelineRefused(line)) _refused = true;

        if (LogParser.ParseFrame(line) is { } f)
        {
            LastFrame = f;
            _drawsPending = f.Draws is null ? 1 : 0;
            Frame?.Invoke(f);
        }
        else if (_drawsPending > 0 && LogParser.ParseDrawsOnly(line) is { } d && LastFrame is { } last)
        {
            _drawsPending = 0;
            LastFrame = last with { Draws = d };
            Frame?.Invoke(LastFrame);
        }
        if (LogParser.ParseMemory(line) is { } m) { LastMemory = m; Memory?.Invoke(m); }
        if (LogParser.ParseWave(line) is { } w) Wave?.Invoke(w);
        if (LogParser.ParseViewLimit(line) is { } v) ViewLimit?.Invoke(v);
        if (LogParser.ParseWarm(line) is { } p) Warm?.Invoke(p);
        Line?.Invoke(line);
    }

    void OnExited()
    {
        // Let the reader threads drain what is left before reporting.
        try { _process.WaitForExit(); } catch (InvalidOperationException) { }
        Running = false;
        var code = _process.ExitCode;
        ExitCode = code;
        lock (_gate)
        {
            try { _log?.WriteLine($"# exit code {code}"); _log?.Dispose(); } catch (ObjectDisposedException) { }
            _log = null;
        }
        Exited?.Invoke(code);
    }

    public void Stop()
    {
        try { if (Running) _process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    public void Dispose()
    {
        Stop();
        _process.Dispose();
    }

    /// <summary>Deletes the oldest run logs, keeping the newest <paramref name="keep"/>.</summary>
    public static void PruneLogs(string folder, int keep = 12)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var f in new DirectoryInfo(folder).GetFiles("*.log").OrderByDescending(f => f.LastWriteTimeUtc).Skip(keep))
            try { f.Delete(); } catch (IOException) { }
    }
}
