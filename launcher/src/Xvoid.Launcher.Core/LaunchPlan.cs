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
/// Turns the launcher's settings into the engine's switches, for either engine. The AMD build gets the set the 7900 XTX fork
/// is played with (scripts/play.ps1); the NVIDIA build gets the set its own launcher (`victory play`) starts it with.
/// What differs on top is chosen from what is known about this PC.
/// </summary>
public static class Plans
{
    // The AMD fork's own settings, as scripts/play.ps1 has them.
    static readonly (string, string)[] AmdBase =
    {
        ("VICTORY_TEXTURE_NO_WAIT", "1"), ("VICTORY_RELAY", "2"), ("VICTORY_WIDE_BACK", "1"), ("VICTORY_CARD_REHASH_RUNS", "1"),
        ("VICTORY_JOIN_COPIES", "1"), ("VICTORY_INDIRECT_WINDOW", "1"), ("VICTORY_RELAY_COPY", "1"), ("VICTORY_CARD_STEPS", "2"),
        ("VICTORY_INDIRECT_BATCH", "2"), ("VICTORY_MAP_THREADS", "1"), ("VICTORY_TITLE_PRIORITIES", "0"), ("VICTORY_CARD_AWAKE", "1"),
        ("VICTORY_SHOW_AFTER", "1"), ("VICTORY_D3D12_DRED", "light"), ("VICTORY_SKIP_PIPELINES", "none"),
    };

    // The NVIDIA engine's own settings: the SETTINGS table of apps/victory/src/play.rs in trustno1-coder/VIctory-NVIDIA, which
    // is what `victory.exe` starts the game with. Its author tuned them on an RTX 5070 with 12 GB. Note LEVELS_ON_DEMAND "0":
    // the AMD set turned it on for small cards, and this engine's own set leaves it off.
    static readonly (string, string)[] NvidiaBase =
    {
        // Submission and copy machinery
        ("VICTORY_THIN", "1"), ("VICTORY_JOIN_PARTS", "8"), ("VICTORY_RELAY", "2"), ("VICTORY_RELAY_COPY", "1"),
        ("VICTORY_WIDE_BACK", "1"), ("VICTORY_CARD_REHASH_RUNS", "1"), ("VICTORY_JOIN_COPIES", "1"), ("VICTORY_CARD_STEPS", "2"),
        ("VICTORY_CARD_AWAKE", "1"), ("VICTORY_CARD_BUFFERS_MIN_KB", "64"), ("VICTORY_INDIRECT_WINDOW", "1"),
        ("VICTORY_INDIRECT_BATCH", "2"), ("VICTORY_UPLOAD_AHEAD", "1"), ("VICTORY_SHOW_AFTER", "1"), ("VICTORY_MAP_THREADS", "1"),
        // Shader writes into big write-combined copies stay on the card
        ("VICTORY_WC_NO_BACK", "1"), ("VICTORY_WC_NO_BACK_MIN_KB", "1024"),
        // The title's page-mapping copies kept without Windows views
        ("VICTORY_SOFT_VIEWS", "copies"),
        // Textures
        ("VICTORY_TEXTURE_POOL", "1"), ("VICTORY_RIPE_BLOCKS", "0"), ("VICTORY_TEXTURE_NO_WAIT", "1"), ("VICTORY_TEXTURE_CATCHUP", "0"),
        ("VICTORY_TEXTURE_STAGE_MS", "16"), ("VICTORY_LEVELS_ON_DEMAND", "0"), ("VICTORY_PRELUDE_UPLOADS", "1"),
        ("VICTORY_SHOW_NEW_LATER", "1"), ("VICTORY_POOL_SMALL", "0"), ("VICTORY_TABLED_REREAD", "1"), ("VICTORY_QUEUE_HOST_TEXTURES", "0"),
        // Descriptor heaps' records
        ("VICTORY_HEAP_SYNC_FIRST", "1"), ("VICTORY_SYNC_IN_PLACE", "1"), ("VICTORY_SPANS_QUICK", "1"), ("VICTORY_SYNC_FINE", "1"),
        ("VICTORY_HEAP_SYNC_AT_SUBMIT", "1"),
        // Light without the title's ray tracing
        ("VICTORY_AMBIENT", "0.8"), ("VICTORY_AMBIENT_AIR", "1.5"), ("VICTORY_AMBIENT_SPECULAR", "1"),
        // Pixel programs keep the console's early depth test
        ("VICTORY_EARLY_DEPTH", "1"),
        // Left out: the decal heap's defragmentation and the heat-haze pass
        ("VICTORY_LEAVE_OUT", "02384a60fda6f2d0,731a39171b568298"),
        // Pipelines the title makes are written down, for `victory warm`
        ("VICTORY_PIPELINE_BOOK", "1"),
    };

    /// <summary>Frames a second at most, as the engine's launcher counts it: 1000 is no cap (the title's own rate is 30).</summary>
    const int NvidiaFps = 1000;

    /// <summary>Threads that translate list calls. The fork's 8 took all of a laptop i7.</summary>
    public static int AutoListThreads(int logicalProcessors) => Math.Clamp(logicalProcessors / 3, 2, 8);

    static Dictionary<string, string> Common(Settings s, AppPaths paths, EngineKind kind)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["VICTORY_GAME_DIR"] = s.GameFolder ?? "",
            // Each engine keeps its own translations; the saves are shared, so switching engine keeps them.
            ["VICTORY_CACHE_DIR"] = paths.CacheFor(kind),
            ["VICTORY_DATA_DIR"] = paths.Data,
            // Plain log lines: the launcher reads them.
            ["NO_COLOR"] = "1",
        };
        return env;
    }

    public static LaunchPlan Play(Settings s, SystemSnapshot sys, AppPaths paths, string enginePath, EngineKind kind)
    {
        if (string.IsNullOrEmpty(s.GameExe)) throw new InvalidOperationException("No game executable is set.");
        var (env, console) = kind == EngineKind.Nvidia ? NvidiaEnvironment(s, sys, paths) : AmdEnvironment(s, sys, paths);
        var args = new List<string> { "run", s.GameExe, "--execute", "--console", console, "--unimplemented", "trap" };
        return new LaunchPlan(enginePath, args, env, Path.GetDirectoryName(enginePath)!);
    }

    static (Dictionary<string, string> Env, string Console) AmdEnvironment(Settings s, SystemSnapshot sys, AppPaths paths)
    {
        var env = Common(s, paths, EngineKind.Amd);
        foreach (var (k, v) in AmdBase) env[k] = v;

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
        // The title's own ray tracing stays off unless asked for: it is slow and breaks the picture on these engines.
        // Said outright ("0") so nothing else in the environment can turn it on.
        env["VICTORY_BVH_BUILDER"] = s.RayTracing ? "1" : "0";
        if (s.AmbientLight == true) env["VICTORY_AMBIENT"] = "0.8";
        if (budget > 0) env["VICTORY_TEXTURE_BUDGET_MB"] = budget.ToString();
        if (s.RecordPipelines) env["VICTORY_PIPELINE_BOOK"] = "1";

        foreach (var (k, v) in s.ExtraEnvironment)
            if (k.StartsWith("VICTORY_", StringComparison.Ordinal)) env[k] = v;
        return (env, console);
    }

    /// <summary>
    /// What `victory play` would set for this PC, with the launcher's own choices over it: the engine's table, the resolution,
    /// DLSS and frame generation when NVIDIA's files are on the PC, and the menu's current choices (F1 shows them).
    /// </summary>
    static (Dictionary<string, string> Env, string Console) NvidiaEnvironment(Settings s, SystemSnapshot sys, AppPaths paths)
    {
        var env = Common(s, paths, EngineKind.Nvidia);
        foreach (var (k, v) in NvidiaBase) env[k] = v;

        var console = s.Console;
        var budget = s.TextureBudgetMb;
        if (s.Preset == Preset.LowMemory)
        {
            console = "series-s";
            if (budget <= 0) budget = 3072;
            // The one place this engine's levels are made on demand: the card is small and the person asked for it.
            env["VICTORY_LEVELS_ON_DEMAND"] = "1";
        }

        if (s.Preset == Preset.Safe)
        {
            foreach (var k in new[] { "VICTORY_THIN", "VICTORY_JOIN_PARTS", "VICTORY_UPLOAD_AHEAD" }) env.Remove(k);
            env["VICTORY_UNDER_WIDE"] = "0";
            env["VICTORY_LAND_ECHO"] = "0";
        }
        else
        {
            env["VICTORY_THIN_THREADS"] = (s.ListThreads > 0 ? s.ListThreads : AutoListThreads(sys.LogicalProcessors)).ToString();
        }

        // DLSS and frame generation need NVIDIA's files (the Prepare page fetches them); without them the title upscales itself.
        var dlssOn = s.Dlss != DlssMode.Off && paths.HasStreamline;
        var mode = s.Dlss.ToString().ToLowerInvariant();
        var framegen = 0;
        if (dlssOn)
        {
            env["VICTORY_DLSS"] = "1";
            env["VICTORY_DLSS_MODE"] = mode;
            env["VICTORY_STREAMLINE_DIR"] = paths.Streamline;
            if (s.FrameGeneration != FrameGeneration.Off)
            {
                framegen = s.FrameGeneration == FrameGeneration.On2x ? 2 : 0;
                env["VICTORY_FRAMEGEN"] = "1";
                env["VICTORY_FRAMEGEN_X"] = framegen.ToString();
                // Frame generation takes 1.5 to 2 GB of the card's memory, which the layer otherwise fills with textures:
                // on from the start it is left a fifth of the budget, ready but off a little less is held back.
                env["VICTORY_TEXTURE_ROOM_PERCENT"] = framegen > 0 ? "80" : "86";
            }
        }

        var res = "1440";
        if (console == "series-x")
        {
            res = s.Resolution switch
            {
                Resolution.P1440 => "1440",
                Resolution.P1080To1440 => "1080to1440",
                _ => dlssOn ? "1440" : "1080to1440",
            };
            env[res == "1440" ? "VICTORY_NATIVE_1440P" : "VICTORY_RENDER_1080P"] = "1";
        }

        env["VICTORY_FRAME_INTERVAL_US"] = Math.Round(1_000_000.0 / NvidiaFps).ToString("0");
        // The title's own ray tracing: off, and said outright so nothing else in the environment can turn it on.
        env["VICTORY_BVH_BUILDER"] = s.RayTracing ? "1" : "0";
        if (s.AmbientLight == false)
            foreach (var k in new[] { "VICTORY_AMBIENT", "VICTORY_AMBIENT_AIR", "VICTORY_AMBIENT_SPECULAR" }) env.Remove(k);
        if (budget > 0) env["VICTORY_TEXTURE_BUDGET_MB"] = budget.ToString();
        if (!s.RecordPipelines) env.Remove("VICTORY_PIPELINE_BOOK");

        // What the F1 menu shows as the current choices.
        env["VICTORY_MENU_CONSOLE"] = console;
        env["VICTORY_MENU_RES"] = res;
        env["VICTORY_MENU_DLSS"] = dlssOn ? mode : "off";
        env["VICTORY_MENU_FRAMEGEN"] = framegen.ToString();
        env["VICTORY_MENU_FG_TYPE"] = "auto";
        env["VICTORY_MENU_FPS"] = NvidiaFps.ToString();

        foreach (var (k, v) in s.ExtraEnvironment)
            if (k.StartsWith("VICTORY_", StringComparison.Ordinal)) env[k] = v;
        return (env, console);
    }

    /// <summary>
    /// Translates every shader of the title's folder. The cache is keyed by the host's wave size and view limit, so they
    /// must be the ones a run reports ("host wave lanes ...", "host view heaps hold ... views"); without them the cache
    /// would hold translations no run is ever served. The NVIDIA engine's translations also depend on its play switches,
    /// so it translates with the same environment a run has.
    /// </summary>
    public static LaunchPlan Shaders(Settings s, SystemSnapshot sys, AppPaths paths, string enginePath, EngineKind kind, out string? caveat)
    {
        if (s.GameFolder is null) throw new InvalidOperationException("No game folder is set.");
        var env = kind == EngineKind.Nvidia ? NvidiaEnvironment(s, sys, paths).Env : Common(s, paths, kind);
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
    public static LaunchPlan Pipelines(Settings s, SystemSnapshot sys, AppPaths paths, string enginePath, EngineKind kind, int jobs = 0)
    {
        if (s.GameFolder is null) throw new InvalidOperationException("No game folder is set.");
        var env = kind == EngineKind.Nvidia ? NvidiaEnvironment(s, sys, paths).Env : Common(s, paths, kind);
        var args = new List<string> { "warm", "--used" };
        if (jobs > 0) { args.Add("--jobs"); args.Add(jobs.ToString()); }
        return new LaunchPlan(enginePath, args, env, Path.GetDirectoryName(enginePath)!);
    }

    /// <summary>
    /// NVIDIA's DLSS and frame generation files, fetched by the engine's own `get-dlss` (about 70 MB, from NVIDIA's GitHub).
    /// VICTORY_HOME keeps them inside the launcher's folder, where Play looks for them.
    /// </summary>
    public static LaunchPlan GetDlss(AppPaths paths, string enginePath) =>
        new(enginePath, new[] { "get-dlss" },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["VICTORY_HOME"] = paths.EngineHome, ["NO_COLOR"] = "1" },
            Path.GetDirectoryName(enginePath)!);
}
