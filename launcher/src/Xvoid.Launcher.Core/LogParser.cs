using System.Text.RegularExpressions;

namespace Xvoid.Launcher.Core;

/// <summary>Frames rendered a second, ms a frame, draws; Shown is what frame generation puts on the screen (null without it).</summary>
public sealed record FrameStat(double Fps, double Ms, int? Draws, double? Shown = null);
public sealed record MemoryStat(double PrivateGb, double? CommitLeftGb, double? RamFreeGb);
public sealed record WarmProgress(int Done, int Total, int Seconds);

/// <summary>
/// Reads the engine's log lines (tracing's text format on stderr) and the title bar it sets. The engine's logs are for
/// people, so each pattern is loose and a line that does not match is simply not a statistic.
/// </summary>
public static partial class LogParser
{
    [GeneratedRegex("\u001b\\[[0-9;]*m")]
    private static partial Regex Ansi();

    // "5.8 fps, 172.2 ms/frame (89.5 preparing: ..."
    [GeneratedRegex(@"(?<fps>\d+(?:\.\d+)?) fps,\s+(?<ms>\d+(?:\.\d+)?) ms/frame")]
    private static partial Regex Frame();

    [GeneratedRegex(@"(?<n>\d+) draws/frame")]
    private static partial Regex Draws();

    // Title bar: "Americas | VIctory 5 fps 197.9 ms/frame 3668 draws/frame", and from the NVIDIA engine
    // "... | VIctory-NVIDIA 5 fps (frame gen 10 fps) 197.9 ms/frame 3668 draws/frame" with frame generation on.
    [GeneratedRegex(@"(?<fps>\d+(?:\.\d+)?) fps(?: \(frame gen (?<shown>\d+(?:\.\d+)?) fps\))?\s+(?<ms>\d+(?:\.\d+)?) ms/frame\s+(?<draws>\d+) draws/frame")]
    private static partial Regex Title();

    // "memory: 22820 MB private (14949 MB of it in memory; the PC has 406 MB free and 666 MB left to commit)"
    [GeneratedRegex(@"memory:\s+(?<priv>\d+) MB private", RegexOptions.None)]
    private static partial Regex Private();

    [GeneratedRegex(@"has\s+(?<free>\d+) MB free and (?<left>\d+) MB left to commit")]
    private static partial Regex Commit();

    [GeneratedRegex(@"host wave lanes min=(?<min>\d+) max=(?<max>\d+)")]
    private static partial Regex Wave();

    [GeneratedRegex(@"host view heaps hold (?<n>\d+) views")]
    private static partial Regex Views();

    [GeneratedRegex(@"^(?<done>\d+) of (?<total>\d+), (?<s>\d+) s\s*$")]
    private static partial Regex Warm();

    [GeneratedRegex(@"memory allocation of (?<n>\d+) bytes failed")]
    private static partial Regex Oom();

    public static string Clean(string line) => Ansi().Replace(line, "").TrimEnd();

    public static FrameStat? ParseFrame(string line)
    {
        var m = Frame().Match(line);
        if (!m.Success) return null;
        var d = Draws().Match(line);
        return new FrameStat(Num(m, "fps"), Num(m, "ms"), d.Success ? int.Parse(d.Groups["n"].Value) : null);
    }

    /// <summary>A line that only carries the draw count (the frame line wraps in a console).</summary>
    public static int? ParseDrawsOnly(string line) => Draws().Match(line) is { Success: true } m ? int.Parse(m.Groups["n"].Value) : null;

    public static FrameStat? ParseTitle(string? title)
    {
        if (string.IsNullOrEmpty(title)) return null;
        var m = Title().Match(title);
        return m.Success
            ? new FrameStat(Num(m, "fps"), Num(m, "ms"), int.Parse(m.Groups["draws"].Value), m.Groups["shown"].Success ? Num(m, "shown") : null)
            : null;
    }

    public static MemoryStat? ParseMemory(string line)
    {
        var p = Private().Match(line);
        if (!p.Success) return null;
        var c = Commit().Match(line);
        return new MemoryStat(int.Parse(p.Groups["priv"].Value) / 1024.0,
            c.Success ? int.Parse(c.Groups["left"].Value) / 1024.0 : null,
            c.Success ? int.Parse(c.Groups["free"].Value) / 1024.0 : null);
    }

    public static (int Min, int Max)? ParseWave(string line) =>
        Wave().Match(line) is { Success: true } m ? (int.Parse(m.Groups["min"].Value), int.Parse(m.Groups["max"].Value)) : null;

    public static long? ParseViewLimit(string line) =>
        Views().Match(line) is { Success: true } m ? long.Parse(m.Groups["n"].Value) : null;

    public static WarmProgress? ParseWarm(string line) =>
        Warm().Match(line) is { Success: true } m
            ? new WarmProgress(int.Parse(m.Groups["done"].Value), int.Parse(m.Groups["total"].Value), int.Parse(m.Groups["s"].Value))
            : null;

    public static bool IsAllocationFailure(string line) => Oom().IsMatch(line);

    public static bool IsPipelineRefused(string line) => line.Contains("host pipeline refused", StringComparison.Ordinal);

    static double Num(Match m, string group) => double.Parse(m.Groups[group].Value, System.Globalization.CultureInfo.InvariantCulture);
}
