namespace Xvoid.Launcher.Core;

/// <summary>A command for the engine: arguments and the VICTORY_* environment it runs with.</summary>
public sealed record LaunchPlan(string EnginePath, IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string> Environment, string WorkingDirectory)
{
    /// <summary>The same command as a line a person could paste, for the Settings page and bug reports.</summary>
    public string Describe(bool environment = true)
    {
        static string Q(string s) => s.Contains(' ') ? $"\"{s}\"" : s;
        var lines = new List<string>();
        if (environment)
            foreach (var (k, v) in Environment.OrderBy(p => p.Key, StringComparer.Ordinal))
                lines.Add($"{k}={v}");
        lines.Add(Q(EnginePath) + " " + string.Join(' ', Arguments.Select(Q)));
        return string.Join('\n', lines);
    }
}

/// <summary>
/// Turns the launcher's settings into the engine's switches. The base set is the one the 7900 XTX fork is played with
/// (scripts/play.ps1); what differs here is chosen from what is known about this PC.
/// </summary>
public static class Plans
{
    // The fork's own settings, as scripts/play.ps1 has them.
    static readonly (string, string)[] Base =
    {
        ("VICTORY_TEXTURE_NO_WAIT", "1"), ("VICTORY_RELAY", "2"), ("VICTORY_WIDE_BACK", "1"), ("VICTORY_CARD_REHASH_RUNS", "1"),
        ("VICTORY_JOIN_COPIES", "1"), ("VICTORY_INDIRECT_WINDOW", "1"), ("VICTORY_RELAY_COPY", "1"), ("VICTORY_CARD_STEPS", "2"),
        ("VICTORY_INDIRECT_BATCH", "2"), ("VICTORY_MAP_THREADS", "1"), ("VICTORY_TITLE_PRIORITIES", "0"), ("VICTORY_CARD_AWAKE", "1"),
        ("VICTORY_SHOW_AFTER", "1"), ("VICTORY_D3D12_DRED", "light"), ("VICTORY_SKIP_PIPELINES", "none"),
    };

    /// <summary>Threads that translate list calls. The fork's 8 took all of a laptop i7.</summary>
    public static int AutoListThreads(int logicalProcessors) => Math.Clamp(logicalProcessors / 3, 2, 8);

    static Dictionary<string, string> Common(Settings s, AppPaths paths)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["VICTORY_GAME_DIR"] = s.GameFolder ?? "",
            ["VICTORY_CACHE_DIR"] = paths.Cache,
            ["VICTORY_DATA_DIR"] = paths.Data,
            // Plain log lines: the launcher reads them.
            ["NO_COLOR"] = "1",
        };
        return env;
    }

    public static LaunchPlan Play(Settings s, SystemSnapshot sys, AppPaths paths, string enginePath)
    {
        if (string.IsNullOrEmpty(s.GameExe)) throw new InvalidOperationException("No game executable is set.");
        var env = Common(s, paths);
        foreach (var (k, v) in Base) env[k] = v;

        var console = s.Console;
        var budget = s.TextureBudgetMb;
        if (s.Preset == Preset.LowMemory)
        {
            console = "series-s";
            if (budget <= 0) budget = 3072;
        }

        if (s.Preset == Preset.Safe)
        {
            env["VICTORY_UNDER_WIDE"] = "0";
            env["VICTORY_LAND_ECHO"] = "0";
        }
        else
        {
            env["VICTORY_THIN"] = "1";
            env["VICTORY_THIN_THREADS"] = (s.ListThreads > 0 ? s.ListThreads : AutoListThreads(sys.LogicalProcessors)).ToString();
            env["VICTORY_JOIN_PARTS"] = "8";
            env["VICTORY_UPLOAD_AHEAD"] = "1";
        }

        // Frees 2-3 GB of card memory; only worth it when the card is small.
        if (sys.BestGpu?.VramGb is < 12) env["VICTORY_LEVELS_ON_DEMAND"] = "1";
        if (s.RayTracing) env["VICTORY_BVH_BUILDER"] = "1";
        if (s.AmbientLight) env["VICTORY_AMBIENT"] = "0.8";
        if (budget > 0) env["VICTORY_TEXTURE_BUDGET_MB"] = budget.ToString();
        if (s.RecordPipelines) env["VICTORY_PIPELINE_BOOK"] = "1";

        foreach (var (k, v) in s.ExtraEnvironment)
            if (k.StartsWith("VICTORY_", StringComparison.Ordinal)) env[k] = v;

        var args = new List<string> { "run", s.GameExe, "--execute", "--console", console, "--unimplemented", "trap" };
        return new LaunchPlan(enginePath, args, env, Path.GetDirectoryName(enginePath)!);
    }

    /// <summary>
    /// Translates every shader of the title's folder. The cache is keyed by the host's wave size and view limit, so they
    /// must be the ones a run reports ("host wave lanes ...", "host view heaps hold ... views"); without them the cache
    /// would hold translations no run is ever served.
    /// </summary>
    public static LaunchPlan Shaders(Settings s, SystemSnapshot sys, AppPaths paths, string enginePath, out string? caveat)
    {
        if (s.GameFolder is null) throw new InvalidOperationException("No game folder is set.");
        var env = Common(s, paths);
        caveat = null;
        if (s.Host.IsKnown)
        {
            env["VICTORY_HOST_WAVE"] = s.Host.WaveMax.ToString();
            env["VICTORY_VIEW_LIMIT"] = s.Host.ViewLimit.ToString();
            if (s.Host.WaveMin == 32 && s.Host.WaveMax == 64) env["VICTORY_HOST_RDNA"] = "1";
        }
        else if (sys.BestGpu?.Vendor == GpuVendor.Nvidia)
        {
            // What an RTX 4060 Laptop's run reported.
            env["VICTORY_HOST_WAVE"] = "32";
            env["VICTORY_VIEW_LIMIT"] = "1000000";
            caveat = "Using the numbers an NVIDIA card reported. Run the game once and prepare again if the first start is still slow.";
        }
        else
        {
            caveat = "This GPU's numbers are not known yet. Run the game once, then prepare, so the cache matches.";
        }
        return new LaunchPlan(enginePath, new[] { "shaders", s.GameFolder }, env, Path.GetDirectoryName(enginePath)!);
    }

    /// <summary>Host pipelines that earlier runs drew with. Made on this GPU with the run's own settings, so no host numbers.</summary>
    public static LaunchPlan Pipelines(Settings s, AppPaths paths, string enginePath, int jobs = 0)
    {
        if (s.GameFolder is null) throw new InvalidOperationException("No game folder is set.");
        var env = Common(s, paths);
        var args = new List<string> { "warm", "--used" };
        if (jobs > 0) { args.Add("--jobs"); args.Add(jobs.ToString()); }
        return new LaunchPlan(enginePath, args, env, Path.GetDirectoryName(enginePath)!);
    }
}
