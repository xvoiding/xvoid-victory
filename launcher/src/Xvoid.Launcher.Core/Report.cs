using System.Text;

namespace Xvoid.Launcher.Core;

/// <summary>A text file with what is needed to understand a problem. Personal paths are shortened.</summary>
public static class Report
{
    public static string Create(AppPaths paths, Settings settings, SystemSnapshot sys, IEnumerable<Check> checks,
        string? engineVersion, string[] logTail, string? note = null)
    {
        paths.EnsureCreated();
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string Redact(string? s) => string.IsNullOrEmpty(s) || string.IsNullOrEmpty(user) ? s ?? "" : s.Replace(user, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine($"{Branding.AppName} report, {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}");
        if (!string.IsNullOrWhiteSpace(note)) { sb.AppendLine().AppendLine("Note: " + note); }
        sb.AppendLine().AppendLine("== Engine");
        sb.AppendLine($"version: {engineVersion ?? "none"}   source: {settings.EngineSource}");
        sb.AppendLine().AppendLine("== System");
        sb.AppendLine($"RAM {sys.RamGb:0.#} GB ({sys.RamFreeGb:0.#} free), commit limit {sys.CommitLimitGb:0.#} GB ({sys.CommitFreeGb:0.#} free), {sys.LogicalProcessors} logical processors, AVX2 {sys.Avx2}");
        foreach (var g in sys.Gpus) sb.AppendLine($"GPU: {g.Name}, {(g.VramGb is { } v ? $"{v:0.#} GB" : "memory unknown")}, driver {g.Driver ?? "?"}");
        sb.AppendLine().AppendLine("== Checks");
        foreach (var c in checks) sb.AppendLine($"[{c.Severity}] {c.Title}: {c.Detail}");
        sb.AppendLine().AppendLine("== Settings");
        sb.AppendLine($"preset {settings.Preset}, console {settings.Console}, texture budget {settings.TextureBudgetMb} MB, list threads {settings.ListThreads}, ray tracing {settings.RayTracing}, ambient {settings.AmbientLight}");
        sb.AppendLine($"host profile: wave {settings.Host.WaveMin}-{settings.Host.WaveMax}, view limit {settings.Host.ViewLimit}");
        sb.AppendLine($"game: {Redact(settings.GameExe)}");
        sb.AppendLine().AppendLine("== Last log lines");
        foreach (var l in logTail.TakeLast(400)) sb.AppendLine(Redact(l));

        var file = Path.Combine(paths.Reports, $"report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
        return file;
    }
}
