using Xvoid.Launcher.Core;
using Xunit;

namespace Xvoid.Launcher.Core.Tests;

public class PlansTests
{
    static SystemSnapshot Laptop(double? vram = 8) => new(32, 20, 30, 28, 16, true, true,
        new[] { new GpuInfo("NVIDIA GeForce RTX 4060 Laptop GPU", GpuVendor.Nvidia, vram, "32.0") }, 70);

    static Settings Configured() => new() { GameExe = Path.Combine(Path.GetTempPath(), "game", "title.exe") };
    static readonly AppPaths Paths = new(Path.Combine(Path.GetTempPath(), "xvoid-test"));

    const EngineKind Nv = EngineKind.Nvidia;
    const EngineKind Amd = EngineKind.Amd;

    static LaunchPlan Play(Settings s, SystemSnapshot? sys = null, EngineKind kind = Nv, string exe = "victory.exe", AppPaths? paths = null) =>
        Plans.Play(s, sys ?? Laptop(), paths ?? Paths, exe, kind);

    /// <summary>Paths whose NVIDIA files are in place, as after "Get NVIDIA DLSS files".</summary>
    static AppPaths WithStreamline()
    {
        var paths = new AppPaths(Directory.CreateTempSubdirectory("xvoid-sl").FullName);
        Directory.CreateDirectory(paths.Streamline);
        File.WriteAllText(Path.Combine(paths.Streamline, "sl.interposer.dll"), "");
        return paths;
    }

    // ---- both engines

    [Theory]
    [InlineData(EngineKind.Nvidia)]
    [InlineData(EngineKind.Amd)]
    public void Play_uses_series_x_and_the_fast_ways_by_default(EngineKind kind)
    {
        var plan = Play(Configured(), kind: kind);
        Assert.Contains("series-x", plan.Arguments);
        Assert.Equal("1", plan.Environment["VICTORY_THIN"]);
        Assert.Equal("1", plan.Environment["VICTORY_RELAY_COPY"]);
    }

    [Theory]
    [InlineData(EngineKind.Nvidia)]
    [InlineData(EngineKind.Amd)]
    public void Ray_tracing_is_off_unless_asked_and_said_outright(EngineKind kind)
    {
        // Off by default, and "0" rather than nothing, so a stray VICTORY_BVH_BUILDER=1 in the user's own
        // environment or an old settings file cannot turn it on behind the launcher's back.
        Assert.Equal("0", Play(Configured(), kind: kind).Environment["VICTORY_BVH_BUILDER"]);
        var s = Configured(); s.RayTracing = true;
        Assert.Equal("1", Play(s, kind: kind).Environment["VICTORY_BVH_BUILDER"]);
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(12, 4)]
    [InlineData(16, 5)]
    [InlineData(32, 8)]
    [InlineData(64, 8)]
    public void List_threads_follow_the_processor_count(int logical, int expected) =>
        Assert.Equal(expected, Plans.AutoListThreads(logical));

    [Theory]
    [InlineData(EngineKind.Nvidia)]
    [InlineData(EngineKind.Amd)]
    public void Safe_preset_leaves_out_the_newer_ways(EngineKind kind)
    {
        var s = Configured(); s.Preset = Preset.Safe;
        var env = Play(s, kind: kind).Environment;
        Assert.False(env.ContainsKey("VICTORY_THIN"));
        Assert.False(env.ContainsKey("VICTORY_UPLOAD_AHEAD"));
        Assert.Equal("0", env["VICTORY_UNDER_WIDE"]);
    }

    [Theory]
    [InlineData(EngineKind.Nvidia)]
    [InlineData(EngineKind.Amd)]
    public void Low_memory_preset_is_series_s_with_a_budget_unless_one_is_set(EngineKind kind)
    {
        var s = Configured(); s.Preset = Preset.LowMemory;
        var plan = Play(s, kind: kind);
        Assert.Contains("series-s", plan.Arguments);
        Assert.Equal("3072", plan.Environment["VICTORY_TEXTURE_BUDGET_MB"]);
        s.TextureBudgetMb = 2048;
        Assert.Equal("2048", Play(s, kind: kind).Environment["VICTORY_TEXTURE_BUDGET_MB"]);
    }

    [Theory]
    [InlineData(EngineKind.Nvidia)]
    [InlineData(EngineKind.Amd)]
    public void Extra_environment_is_limited_to_victory_switches(EngineKind kind)
    {
        var s = Configured();
        s.ExtraEnvironment["VICTORY_PANEL"] = "1";
        s.ExtraEnvironment["PATH"] = "C:\\evil";
        var env = Play(s, kind: kind).Environment;
        Assert.Equal("1", env["VICTORY_PANEL"]);
        Assert.False(env.ContainsKey("PATH"));
    }

    [Fact]
    public void Describe_prints_the_command_with_quoting()
    {
        var plan = Play(Configured(), exe: "C:\\Program Files\\v\\victory.exe");
        var text = plan.Describe();
        Assert.Contains("\"C:\\Program Files\\v\\victory.exe\" run", text);
        Assert.Contains("VICTORY_THIN=1", text);
    }

    [Theory]
    [InlineData(EngineKind.Nvidia)]
    [InlineData(EngineKind.Amd)]
    public void Play_without_a_game_is_an_error(EngineKind kind) =>
        Assert.Throws<InvalidOperationException>(() => Play(new Settings(), kind: kind));

    [Fact]
    public void Each_engine_has_its_own_cache_and_they_share_the_saves()
    {
        var nv = Play(Configured(), kind: Nv).Environment;
        var amd = Play(Configured(), kind: Amd).Environment;
        Assert.Equal(Paths.CacheFor(Nv), nv["VICTORY_CACHE_DIR"]);
        Assert.Equal(Paths.CacheFor(Amd), amd["VICTORY_CACHE_DIR"]);
        Assert.NotEqual(nv["VICTORY_CACHE_DIR"], amd["VICTORY_CACHE_DIR"]);
        Assert.Equal(nv["VICTORY_DATA_DIR"], amd["VICTORY_DATA_DIR"]);
    }

    // ---- AMD engine: its own fork's settings, as they were

    [Fact]
    public void Amd_ambient_light_is_only_on_when_asked()
    {
        Assert.False(Play(Configured(), kind: Amd).Environment.ContainsKey("VICTORY_AMBIENT"));
        var s = Configured(); s.AmbientLight = true;
        Assert.Equal("0.8", Play(s, kind: Amd).Environment["VICTORY_AMBIENT"]);
    }

    [Fact]
    public void Amd_makes_levels_on_demand_only_for_small_cards()
    {
        Assert.True(Play(Configured(), Laptop(8), Amd).Environment.ContainsKey("VICTORY_LEVELS_ON_DEMAND"));
        Assert.False(Play(Configured(), Laptop(24), Amd).Environment.ContainsKey("VICTORY_LEVELS_ON_DEMAND"));
    }

    [Fact]
    public void Amd_gets_none_of_the_nvidia_switches()
    {
        var env = Play(Configured(), kind: Amd, paths: WithStreamline()).Environment;
        foreach (var k in new[] { "VICTORY_DLSS", "VICTORY_FRAMEGEN", "VICTORY_STREAMLINE_DIR", "VICTORY_NATIVE_1440P", "VICTORY_RENDER_1080P", "VICTORY_MENU_DLSS" })
            Assert.False(env.ContainsKey(k), k);
    }

    // ---- NVIDIA engine: the settings its own launcher starts it with

    [Fact]
    public void Nvidia_starts_with_the_engines_own_table_not_the_amd_one()
    {
        var env = Play(Configured()).Environment;
        // The ones the AMD set does not have, or has differently.
        Assert.Equal("0", env["VICTORY_LEVELS_ON_DEMAND"]);
        Assert.Equal("1", env["VICTORY_EARLY_DEPTH"]);
        Assert.Equal("copies", env["VICTORY_SOFT_VIEWS"]);
        Assert.Equal("1", env["VICTORY_TEXTURE_POOL"]);
        Assert.Equal("1", env["VICTORY_PIPELINE_BOOK"]);
        Assert.Equal("02384a60fda6f2d0,731a39171b568298", env["VICTORY_LEAVE_OUT"]);
        // And none of the AMD fork's own.
        Assert.False(env.ContainsKey("VICTORY_D3D12_DRED"));
        Assert.False(env.ContainsKey("VICTORY_TITLE_PRIORITIES"));
    }

    [Fact]
    public void Nvidia_does_not_make_levels_on_demand_for_an_8_gb_card_unless_low_memory_is_chosen()
    {
        // The AMD set turned this on under 12 GB; it is the likeliest cause of pink patches, and the engine's own set leaves it off.
        Assert.Equal("0", Play(Configured(), Laptop(8)).Environment["VICTORY_LEVELS_ON_DEMAND"]);
        var s = Configured(); s.Preset = Preset.LowMemory;
        Assert.Equal("1", Play(s, Laptop(8)).Environment["VICTORY_LEVELS_ON_DEMAND"]);
    }

    [Fact]
    public void Nvidia_ambient_light_is_on_by_default_and_can_be_turned_off()
    {
        var on = Play(Configured()).Environment;
        Assert.Equal("0.8", on["VICTORY_AMBIENT"]);
        Assert.Equal("1.5", on["VICTORY_AMBIENT_AIR"]);
        var s = Configured(); s.AmbientLight = false;
        var off = Play(s).Environment;
        Assert.False(off.ContainsKey("VICTORY_AMBIENT"));
        Assert.False(off.ContainsKey("VICTORY_AMBIENT_AIR"));
        Assert.False(off.ContainsKey("VICTORY_AMBIENT_SPECULAR"));
    }

    [Fact]
    public void Nvidia_can_leave_pipelines_unrecorded()
    {
        var s = Configured(); s.RecordPipelines = false;
        Assert.False(Play(s).Environment.ContainsKey("VICTORY_PIPELINE_BOOK"));
    }

    [Fact]
    public void Nvidia_without_the_downloaded_files_has_no_dlss_and_no_frame_generation()
    {
        var env = Play(Configured()).Environment;
        Assert.False(env.ContainsKey("VICTORY_DLSS"));
        Assert.False(env.ContainsKey("VICTORY_FRAMEGEN"));
        Assert.Equal("off", env["VICTORY_MENU_DLSS"]);
        // Without DLSS the picture is drawn at 1080p and brought up to 1440p.
        Assert.Equal("1", env["VICTORY_RENDER_1080P"]);
        Assert.False(env.ContainsKey("VICTORY_NATIVE_1440P"));
    }

    [Fact]
    public void Nvidia_with_the_files_runs_dlss_at_native_1440p_and_leaves_frame_generation_ready()
    {
        var paths = WithStreamline();
        var env = Play(Configured(), paths: paths).Environment;
        Assert.Equal("1", env["VICTORY_DLSS"]);
        Assert.Equal("quality", env["VICTORY_DLSS_MODE"]);
        Assert.Equal(paths.Streamline, env["VICTORY_STREAMLINE_DIR"]);
        Assert.Equal("1", env["VICTORY_FRAMEGEN"]);
        Assert.Equal("0", env["VICTORY_FRAMEGEN_X"]);
        Assert.Equal("86", env["VICTORY_TEXTURE_ROOM_PERCENT"]);
        Assert.Equal("1", env["VICTORY_NATIVE_1440P"]);
        Assert.False(env.ContainsKey("VICTORY_RENDER_1080P"));
    }

    [Fact]
    public void Nvidia_2x_frame_generation_leaves_room_for_it_in_the_texture_budget()
    {
        var s = Configured(); s.FrameGeneration = FrameGeneration.On2x; s.Dlss = DlssMode.Performance;
        var env = Play(s, paths: WithStreamline()).Environment;
        Assert.Equal("performance", env["VICTORY_DLSS_MODE"]);
        Assert.Equal("1", env["VICTORY_FRAMEGEN"]);
        Assert.Equal("2", env["VICTORY_FRAMEGEN_X"]);
        Assert.Equal("80", env["VICTORY_TEXTURE_ROOM_PERCENT"]);
        Assert.Equal("2", env["VICTORY_MENU_FRAMEGEN"]);
    }

    [Fact]
    public void Nvidia_frame_generation_off_sets_none_of_its_switches()
    {
        var s = Configured(); s.FrameGeneration = FrameGeneration.Off;
        var env = Play(s, paths: WithStreamline()).Environment;
        Assert.Equal("1", env["VICTORY_DLSS"]);
        Assert.False(env.ContainsKey("VICTORY_FRAMEGEN"));
        Assert.False(env.ContainsKey("VICTORY_TEXTURE_ROOM_PERCENT"));
    }

    [Fact]
    public void Nvidia_dlss_off_means_no_frame_generation_either()
    {
        // Frame generation here rides on DLSS upscaling, so with DLSS off neither is started.
        var s = Configured(); s.Dlss = DlssMode.Off; s.FrameGeneration = FrameGeneration.On2x;
        var env = Play(s, paths: WithStreamline()).Environment;
        Assert.False(env.ContainsKey("VICTORY_DLSS"));
        Assert.False(env.ContainsKey("VICTORY_FRAMEGEN"));
    }

    [Theory]
    [InlineData(Resolution.P1440, "VICTORY_NATIVE_1440P", "VICTORY_RENDER_1080P")]
    [InlineData(Resolution.P1080To1440, "VICTORY_RENDER_1080P", "VICTORY_NATIVE_1440P")]
    public void Nvidia_resolution_can_be_chosen(Resolution r, string set, string notSet)
    {
        var s = Configured(); s.Resolution = r;
        var env = Play(s, paths: WithStreamline()).Environment;
        Assert.Equal("1", env[set]);
        Assert.False(env.ContainsKey(notSet));
    }

    [Fact]
    public void Nvidia_series_s_has_no_resolution_switch()
    {
        var s = Configured(); s.Console = "series-s";
        var env = Play(s).Environment;
        Assert.False(env.ContainsKey("VICTORY_NATIVE_1440P"));
        Assert.False(env.ContainsKey("VICTORY_RENDER_1080P"));
    }

    [Fact]
    public void Nvidia_runs_with_no_frame_rate_cap_and_tells_the_menu_what_is_chosen()
    {
        var env = Play(Configured(), paths: WithStreamline()).Environment;
        Assert.Equal("1000", env["VICTORY_FRAME_INTERVAL_US"]);
        Assert.Equal("series-x", env["VICTORY_MENU_CONSOLE"]);
        Assert.Equal("1440", env["VICTORY_MENU_RES"]);
        Assert.Equal("quality", env["VICTORY_MENU_DLSS"]);
    }

    // ---- shaders and pipelines

    [Fact]
    public void Shader_prep_uses_the_learned_host_numbers()
    {
        var s = Configured();
        s.Host = new HostProfile { WaveMin = 32, WaveMax = 32, ViewLimit = 1_000_000 };
        var plan = Plans.Shaders(s, Laptop(), Paths, "victory.exe", Nv, out var caveat);
        Assert.Null(caveat);
        Assert.Equal("32", plan.Environment["VICTORY_HOST_WAVE"]);
        Assert.Equal("1000000", plan.Environment["VICTORY_VIEW_LIMIT"]);
        Assert.False(plan.Environment.ContainsKey("VICTORY_HOST_RDNA"));
    }

    [Fact]
    public void Shader_prep_marks_rdna_when_the_host_has_32_to_64_lanes()
    {
        var s = Configured();
        s.Host = new HostProfile { WaveMin = 32, WaveMax = 64, ViewLimit = 1_000_000 };
        Assert.Equal("1", Plans.Shaders(s, Laptop(), Paths, "v.exe", Amd, out _).Environment["VICTORY_HOST_RDNA"]);
    }

    [Fact]
    public void Shader_prep_falls_back_to_nvidia_numbers_with_a_caveat()
    {
        var plan = Plans.Shaders(Configured(), Laptop(), Paths, "v.exe", Nv, out var caveat);
        Assert.NotNull(caveat);
        Assert.Equal("32", plan.Environment["VICTORY_HOST_WAVE"]);
    }

    [Fact]
    public void Shader_prep_for_an_unknown_gpu_sets_nothing_and_says_so()
    {
        var sys = Laptop() with { Gpus = new[] { new GpuInfo("Mystery", GpuVendor.Unknown, null, null) } };
        var plan = Plans.Shaders(Configured(), sys, Paths, "v.exe", Nv, out var caveat);
        Assert.NotNull(caveat);
        Assert.False(plan.Environment.ContainsKey("VICTORY_HOST_WAVE"));
    }

    [Fact]
    public void Shader_prep_uses_the_chosen_engines_cache()
    {
        Assert.Equal(Paths.CacheFor(Amd), Plans.Shaders(Configured(), Laptop(), Paths, "v.exe", Amd, out _).Environment["VICTORY_CACHE_DIR"]);
        Assert.Equal(Paths.CacheFor(Nv), Plans.Shaders(Configured(), Laptop(), Paths, "v.exe", Nv, out _).Environment["VICTORY_CACHE_DIR"]);
    }

    [Fact]
    public void Warm_does_not_set_host_numbers()
    {
        var plan = Plans.Pipelines(Configured(), Laptop(), Paths, "v.exe", Nv);
        Assert.False(plan.Environment.ContainsKey("VICTORY_HOST_WAVE"));
        Assert.Equal(new[] { "warm", "--used" }, plan.Arguments);
    }

    [Fact]
    public void Warm_for_nvidia_runs_with_the_same_switches_a_run_has()
    {
        // Pipelines are made for the settings a run draws with, so the engine's own table is in the environment.
        var env = Plans.Pipelines(Configured(), Laptop(), Paths, "v.exe", Nv).Environment;
        Assert.Equal("1", env["VICTORY_EARLY_DEPTH"]);
        Assert.Equal(Paths.CacheFor(Nv), env["VICTORY_CACHE_DIR"]);
    }

    [Fact]
    public void Get_dlss_keeps_the_download_inside_the_launchers_folder()
    {
        var plan = Plans.GetDlss(Paths, "C:\\e\\victory.exe");
        Assert.Equal(new[] { "get-dlss" }, plan.Arguments);
        Assert.Equal(Paths.EngineHome, plan.Environment["VICTORY_HOME"]);
    }
}
