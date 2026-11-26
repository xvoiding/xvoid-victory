using Xvoid.Launcher.Core;
using Xunit;

namespace Xvoid.Launcher.Core.Tests;

public class ChecksAndSettingsTests
{
    static SystemSnapshot Snap(double ram = 32, double commit = 30, double? vram = 8, bool hybrid = false, bool avx2 = true)
    {
        var gpus = new List<GpuInfo> { new("NVIDIA GeForce RTX 4060 Laptop GPU", GpuVendor.Nvidia, vram, null) };
        if (hybrid) gpus.Add(new GpuInfo("Intel(R) UHD Graphics", GpuVendor.Intel, 0.1, null));
        return new SystemSnapshot(ram, ram * 0.6, commit, commit * 0.9, 16, avx2, true, gpus, 70);
    }

    static Check Get(IEnumerable<Check> c, string id) => c.Single(x => x.Id == id);

    [Fact]
    public void A_small_commit_limit_offers_the_paging_file_fix()
    {
        var c = Get(Checks.Run(Snap(commit: 30), false), "commit");
        Assert.Equal(Severity.Warn, c.Severity);
        Assert.Equal(Fix.PagingFile, c.Fix);
    }

    [Fact]
    public void A_large_commit_limit_is_fine() =>
        Assert.Equal(Severity.Ok, Get(Checks.Run(Snap(commit: 90), false), "commit").Severity);

    [Theory]
    [InlineData(8, Severity.Fail)]
    [InlineData(16, Severity.Warn)]
    [InlineData(32, Severity.Ok)]
    public void Ram_thresholds(double ram, Severity expected) =>
        Assert.Equal(expected, Get(Checks.Run(Snap(ram: ram, commit: 90), false), "ram").Severity);

    [Theory]
    [InlineData(4, Severity.Fail)]
    [InlineData(8, Severity.Warn)]
    [InlineData(24, Severity.Ok)]
    public void Video_memory_thresholds(double vram, Severity expected) =>
        Assert.Equal(expected, Get(Checks.Run(Snap(vram: vram), false), "gpu").Severity);

    [Fact]
    public void Hybrid_graphics_asks_for_high_performance_until_it_is_set()
    {
        Assert.Equal(Fix.GpuPreference, Get(Checks.Run(Snap(hybrid: true), false), "gpuPref").Fix);
        Assert.Equal(Severity.Ok, Get(Checks.Run(Snap(hybrid: true), true), "gpuPref").Severity);
        Assert.DoesNotContain(Checks.Run(Snap(hybrid: false), false), c => c.Id == "gpuPref");
    }

    [Fact]
    public void Missing_avx2_blocks()
    {
        var checks = Checks.Run(Snap(avx2: false), false);
        Assert.True(Checks.Blocking(checks));
        Assert.False(Checks.Blocking(Checks.Run(Snap(), false)));
    }

    [Fact]
    public void Settings_survive_a_round_trip_and_a_damaged_file_is_set_aside()
    {
        var dir = Directory.CreateTempSubdirectory("xvoid-set").FullName;
        var file = Path.Combine(dir, "settings.json");
        var s = new Settings { GameExe = "C:\\g\\t.exe", Preset = Preset.Safe, TextureBudgetMb = 2048, Host = new HostProfile { WaveMin = 32, WaveMax = 32, ViewLimit = 5 } };
        s.Save(file);
        var back = Settings.Load(file);
        Assert.Equal(Preset.Safe, back.Preset);
        Assert.Equal(2048, back.TextureBudgetMb);
        Assert.True(back.Host.IsKnown);

        File.WriteAllText(file, "{ not json");
        var fresh = Settings.Load(file);
        Assert.False(fresh.SetupDone);
        Assert.True(File.Exists(file + ".bad"));
    }

    [Theory]
    [InlineData(-1073740791, true, "ran out of memory")]
    [InlineData(-1073740791, false, "aborted")]
    [InlineData(-1073741819, false, "access violation")]
    public void Exit_codes_are_explained(int code, bool oom, string fragment) =>
        Assert.Contains(fragment, ExitAdvice.Explain(code, oom, false), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void A_clean_exit_has_nothing_to_explain() => Assert.Null(ExitAdvice.Explain(0, false, false));

    [Theory]
    [InlineData("NVIDIA GeForce RTX 4060 Laptop GPU", GpuVendor.Nvidia)]
    [InlineData("AMD Radeon RX 7900 XTX", GpuVendor.Amd)]
    [InlineData("Intel(R) UHD Graphics", GpuVendor.Intel)]
    [InlineData("Something Else", GpuVendor.Unknown)]
    public void Vendors(string name, GpuVendor expected) => Assert.Equal(expected, SystemInfo.VendorOf(name));
}
