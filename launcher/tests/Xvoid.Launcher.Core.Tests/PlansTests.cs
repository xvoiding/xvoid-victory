using Xvoid.Launcher.Core;
using Xunit;

namespace Xvoid.Launcher.Core.Tests;

public class PlansTests
{
    static SystemSnapshot Laptop(double? vram = 8) => new(32, 20, 30, 28, 16, true, true,
        new[] { new GpuInfo("NVIDIA GeForce RTX 4060 Laptop GPU", GpuVendor.Nvidia, vram, "32.0") }, 70);

    static Settings Configured() => new() { GameExe = Path.Combine(Path.GetTempPath(), "game", "title.exe") };
    static readonly AppPaths Paths = new(Path.Combine(Path.GetTempPath(), "xvoid-test"));

    [Fact]
    public void Play_uses_series_x_and_the_fast_ways_by_default()
    {
        var plan = Plans.Play(Configured(), Laptop(), Paths, "victory.exe");
        Assert.Contains("series-x", plan.Arguments);
        Assert.Equal("1", plan.Environment["VICTORY_THIN"]);
        Assert.False(plan.Environment.ContainsKey("VICTORY_AMBIENT"));
    }

    [Fact]
    public void Ambient_light_is_only_on_when_asked()
    {
        var s = Configured(); s.AmbientLight = true;
        Assert.Equal("0.8", Plans.Play(s, Laptop(), Paths, "victory.exe").Environment["VICTORY_AMBIENT"]);
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(12, 4)]
    [InlineData(16, 5)]
    [InlineData(32, 8)]
    [InlineData(64, 8)]
    public void List_threads_follow_the_processor_count(int logical, int expected) =>
        Assert.Equal(expected, Plans.AutoListThreads(logical));

    [Fact]
    public void Safe_preset_leaves_out_the_newer_ways()
    {
        var s = Configured(); s.Preset = Preset.Safe;
        var env = Plans.Play(s, Laptop(), Paths, "victory.exe").Environment;
        Assert.False(env.ContainsKey("VICTORY_THIN"));
        Assert.Equal("0", env["VICTORY_UNDER_WIDE"]);
    }

    [Fact]
    public void Low_memory_preset_is_series_s_with_a_budget_unless_one_is_set()
    {
        var s = Configured(); s.Preset = Preset.LowMemory;
        var plan = Plans.Play(s, Laptop(), Paths, "victory.exe");
        Assert.Contains("series-s", plan.Arguments);
        Assert.Equal("3072", plan.Environment["VICTORY_TEXTURE_BUDGET_MB"]);
        s.TextureBudgetMb = 2048;
        Assert.Equal("2048", Plans.Play(s, Laptop(), Paths, "victory.exe").Environment["VICTORY_TEXTURE_BUDGET_MB"]);
    }

    [Fact]
    public void Levels_on_demand_only_for_small_cards()
    {
        Assert.True(Plans.Play(Configured(), Laptop(8), Paths, "v.exe").Environment.ContainsKey("VICTORY_LEVELS_ON_DEMAND"));
        Assert.False(Plans.Play(Configured(), Laptop(24), Paths, "v.exe").Environment.ContainsKey("VICTORY_LEVELS_ON_DEMAND"));
    }

    [Fact]
    public void Extra_environment_is_limited_to_victory_switches()
    {
        var s = Configured();
        s.ExtraEnvironment["VICTORY_PANEL"] = "1";
        s.ExtraEnvironment["PATH"] = "C:\\evil";
        var env = Plans.Play(s, Laptop(), Paths, "victory.exe").Environment;
        Assert.Equal("1", env["VICTORY_PANEL"]);
        Assert.False(env.ContainsKey("PATH"));
    }

    [Fact]
    public void Shader_prep_uses_the_learned_host_numbers()
    {
        var s = Configured();
        s.Host = new HostProfile { WaveMin = 32, WaveMax = 32, ViewLimit = 1_000_000 };
        var plan = Plans.Shaders(s, Laptop(), Paths, "victory.exe", out var caveat);
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
        Assert.Equal("1", Plans.Shaders(s, Laptop(), Paths, "v.exe", out _).Environment["VICTORY_HOST_RDNA"]);
    }

    [Fact]
    public void Shader_prep_falls_back_to_nvidia_numbers_with_a_caveat()
    {
        var plan = Plans.Shaders(Configured(), Laptop(), Paths, "v.exe", out var caveat);
        Assert.NotNull(caveat);
        Assert.Equal("32", plan.Environment["VICTORY_HOST_WAVE"]);
    }

    [Fact]
    public void Shader_prep_for_an_unknown_gpu_sets_nothing_and_says_so()
    {
        var sys = Laptop() with { Gpus = new[] { new GpuInfo("Mystery", GpuVendor.Unknown, null, null) } };
        var plan = Plans.Shaders(Configured(), sys, Paths, "v.exe", out var caveat);
        Assert.NotNull(caveat);
        Assert.False(plan.Environment.ContainsKey("VICTORY_HOST_WAVE"));
    }

    [Fact]
    public void Warm_does_not_set_host_numbers()
    {
        var plan = Plans.Pipelines(Configured(), Paths, "v.exe");
        Assert.False(plan.Environment.ContainsKey("VICTORY_HOST_WAVE"));
        Assert.Equal(new[] { "warm", "--used" }, plan.Arguments);
    }

    [Fact]
    public void Describe_prints_the_command_with_quoting()
    {
        var plan = Plans.Play(Configured(), Laptop(), Paths, "C:\\Program Files\\v\\victory.exe");
        var text = plan.Describe();
        Assert.Contains("\"C:\\Program Files\\v\\victory.exe\" run", text);
        Assert.Contains("VICTORY_THIN=1", text);
    }

    [Fact]
    public void Play_without_a_game_is_an_error() =>
        Assert.Throws<InvalidOperationException>(() => Plans.Play(new Settings(), Laptop(), Paths, "v.exe"));
}
