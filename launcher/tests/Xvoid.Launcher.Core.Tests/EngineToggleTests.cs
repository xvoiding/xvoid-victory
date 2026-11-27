using Xvoid.Launcher.Core;
using Xunit;

namespace Xvoid.Launcher.Core.Tests;

/// <summary>The AMD / NVIDIA toggle: which engine Auto means, what an older settings file turns into, and the first-start warm-up.</summary>
public class EngineToggleTests
{
    static SystemSnapshot WithGpu(string name, GpuVendor vendor) =>
        new(32, 20, 30, 28, 16, true, true, new[] { new GpuInfo(name, vendor, 8, null) }, 70);

    [Theory]
    [InlineData(EngineChoice.Nvidia, EngineKind.Nvidia)]
    [InlineData(EngineChoice.Amd, EngineKind.Amd)]
    public void A_picked_engine_is_used_whatever_the_card_is(EngineChoice choice, EngineKind expected)
    {
        Assert.Equal(expected, EngineKinds.Resolve(choice, WithGpu("AMD Radeon RX 7900 XTX", GpuVendor.Amd)));
        Assert.Equal(expected, EngineKinds.Resolve(choice, WithGpu("NVIDIA GeForce RTX 4060 Laptop GPU", GpuVendor.Nvidia)));
        Assert.Equal(expected, EngineKinds.Resolve(choice, null));
    }

    [Fact]
    public void Auto_follows_the_graphics_card()
    {
        Assert.Equal(EngineKind.Amd, EngineKinds.Resolve(EngineChoice.Auto, WithGpu("AMD Radeon RX 7900 XTX", GpuVendor.Amd)));
        Assert.Equal(EngineKind.Nvidia, EngineKinds.Resolve(EngineChoice.Auto, WithGpu("NVIDIA GeForce RTX 4060 Laptop GPU", GpuVendor.Nvidia)));
        // No card to go by (or one that is neither): the NVIDIA engine, the one that is tuned and tested more.
        Assert.Equal(EngineKind.Nvidia, EngineKinds.Resolve(EngineChoice.Auto, WithGpu("Intel(R) UHD Graphics", GpuVendor.Intel)));
        Assert.Equal(EngineKind.Nvidia, EngineKinds.Resolve(EngineChoice.Auto, null));
    }

    [Fact]
    public void Settings_resolve_their_own_kind()
    {
        var amd = WithGpu("AMD Radeon RX 7900 XTX", GpuVendor.Amd);
        Assert.Equal(EngineKind.Amd, new Settings().Kind(amd));
        Assert.Equal(EngineKind.Nvidia, new Settings { Engine = EngineChoice.Nvidia }.Kind(amd));
    }

    [Theory]
    [InlineData("NVIDIA GeForce RTX 4060 Laptop GPU", true)]
    [InlineData("NVIDIA GeForce RTX 4090", true)]
    [InlineData("NVIDIA GeForce RTX 5070 Ti", true)]
    [InlineData("NVIDIA GeForce RTX 3050 4GB Laptop GPU", false)]
    [InlineData("NVIDIA GeForce RTX 3080", false)]
    [InlineData("NVIDIA GeForce GTX 1660", false)]
    [InlineData("NVIDIA RTX A2000", false)]
    [InlineData("AMD Radeon RX 7900 XTX", false)]
    public void Frame_generation_needs_an_rtx_40_series_or_newer(string gpu, bool expected) =>
        Assert.Equal(expected, EngineKinds.HasDlssFrameGeneration(gpu));

    [Fact]
    public void An_installed_tag_is_kept_per_engine()
    {
        var s = new Settings();
        s.SetInstalledTag(EngineKind.Nvidia, "v1");
        s.SetInstalledTag(EngineKind.Amd, "v2");
        Assert.Equal("v1", s.InstalledTag(EngineKind.Nvidia));
        Assert.Equal("v2", s.InstalledTag(EngineKind.Amd));
        s.SetInstalledTag(EngineKind.Nvidia, null);
        Assert.Null(s.InstalledTag(EngineKind.Nvidia));
        Assert.Equal("v2", s.InstalledTag(EngineKind.Amd));
    }

    [Fact]
    public void The_new_settings_round_trip()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory("xvoid-set2").FullName, "settings.json");
        new Settings
        {
            Engine = EngineChoice.Amd, Resolution = Resolution.P1080To1440, Dlss = DlssMode.Performance,
            FrameGeneration = FrameGeneration.On2x, WarmUp = false, AmbientLight = false, InstalledNvidiaTag = "v0.1.5",
        }.Save(file);
        var back = Settings.Load(file);
        Assert.Equal(EngineChoice.Amd, back.Engine);
        Assert.Equal(Resolution.P1080To1440, back.Resolution);
        Assert.Equal(DlssMode.Performance, back.Dlss);
        Assert.Equal(FrameGeneration.On2x, back.FrameGeneration);
        Assert.False(back.WarmUp);
        Assert.False(back.AmbientLight);
        Assert.Equal("v0.1.5", back.InstalledNvidiaTag);
        Assert.Equal(Settings.CurrentVersion, back.Version);
    }

    [Fact]
    public void A_settings_file_from_before_the_toggle_loads_with_the_engines_own_ambient_default()
    {
        // What 0.1.4 wrote: no Version, AmbientLight a plain false, one engine tag.
        var file = Path.Combine(Directory.CreateTempSubdirectory("xvoid-old").FullName, "settings.json");
        File.WriteAllText(file, """
        {
          "SetupDone": true,
          "GameExe": "C:\\g\\t.exe",
          "Preset": "Safe",
          "AmbientLight": false,
          "InstalledEngineTag": "v0.1.4"
        }
        """);
        var s = Settings.Load(file);
        Assert.True(s.SetupDone);
        Assert.Equal(Preset.Safe, s.Preset);
        Assert.Equal(EngineChoice.Auto, s.Engine);
        // The saved "off" was only the old default for every engine: it is the engine's own choice again.
        Assert.Null(s.AmbientLight);
        Assert.Null(s.InstalledNvidiaTag);
        Assert.Equal(Settings.CurrentVersion, s.Version);
    }

    [Fact]
    public void An_ambient_choice_made_on_the_new_version_is_kept()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory("xvoid-amb").FullName, "settings.json");
        new Settings { AmbientLight = false }.Save(file);
        Assert.False(Settings.Load(file).AmbientLight);
    }

    // ---- folders

    [Fact]
    public void The_old_single_cache_moves_to_the_nvidia_engines_folder_and_old_builds_are_dropped()
    {
        var paths = new AppPaths(Directory.CreateTempSubdirectory("xvoid-layout").FullName);
        // What 0.1.4 left: one cache with shaders and pipelines, one build folder per tag.
        Directory.CreateDirectory(Path.Combine(paths.Cache, "shaders"));
        File.WriteAllText(Path.Combine(paths.Cache, "shaders", "a.bin"), "translated");
        Directory.CreateDirectory(Path.Combine(paths.Cache, "pipelines"));
        Directory.CreateDirectory(Path.Combine(paths.Engines, "v0.1.4"));
        File.WriteAllText(Path.Combine(paths.Engines, "v0.1.4", "victory.exe"), "old");

        paths.EnsureCreated();

        Assert.True(File.Exists(Path.Combine(paths.CacheFor(EngineKind.Nvidia), "shaders", "a.bin")));
        Assert.True(Directory.Exists(paths.PipelineFolderFor(EngineKind.Nvidia)));
        Assert.False(Directory.Exists(Path.Combine(paths.Cache, "shaders")));
        Assert.False(Directory.Exists(Path.Combine(paths.Engines, "v0.1.4")));
        Assert.True(Directory.Exists(paths.EnginesFor(EngineKind.Nvidia)));
        Assert.True(Directory.Exists(paths.EnginesFor(EngineKind.Amd)));
        Assert.True(Directory.Exists(paths.CacheFor(EngineKind.Amd)));
    }

    [Fact]
    public void Preparing_the_layout_twice_changes_nothing()
    {
        var paths = new AppPaths(Directory.CreateTempSubdirectory("xvoid-layout2").FullName);
        paths.EnsureCreated();
        File.WriteAllText(Path.Combine(paths.CacheFor(EngineKind.Amd), "keep.bin"), "x");
        paths.EnsureCreated();
        Assert.True(File.Exists(Path.Combine(paths.CacheFor(EngineKind.Amd), "keep.bin")));
    }

    [Fact]
    public void Nvidias_files_are_found_only_once_the_interposer_is_there()
    {
        var paths = new AppPaths(Directory.CreateTempSubdirectory("xvoid-sl2").FullName);
        Assert.False(paths.HasStreamline);
        Directory.CreateDirectory(paths.Streamline);
        Assert.False(paths.HasStreamline);
        File.WriteAllText(Path.Combine(paths.Streamline, "sl.interposer.dll"), "");
        Assert.True(paths.HasStreamline);
    }

    // ---- warm-up

    [Fact]
    public void The_warm_up_is_due_once_per_program_and_only_for_the_nvidia_engine()
    {
        var dir = Directory.CreateTempSubdirectory("xvoid-warm").FullName;
        var paths = new AppPaths(dir);
        var exe = Path.Combine(dir, "victory.exe");
        File.WriteAllText(exe, "build one");
        var mark = WarmUp.MarkFile(paths, exe);

        Assert.True(WarmUp.Due(new Settings(), EngineKind.Nvidia, mark));
        Assert.False(WarmUp.Due(new Settings(), EngineKind.Amd, mark));
        Assert.False(WarmUp.Due(new Settings { WarmUp = false }, EngineKind.Nvidia, mark));

        WarmUp.Mark(mark);
        Assert.False(WarmUp.Due(new Settings(), EngineKind.Nvidia, mark));

        // A different program (a new build) has its own mark and is due again.
        File.WriteAllText(exe, "build two");
        var next = WarmUp.MarkFile(paths, exe);
        Assert.NotEqual(mark, next);
        Assert.True(WarmUp.Due(new Settings(), EngineKind.Nvidia, next));
    }

    [Fact]
    public void The_same_program_gets_the_same_mark_wherever_it_sits()
    {
        var paths = new AppPaths(Directory.CreateTempSubdirectory("xvoid-warm2").FullName);
        var a = Path.Combine(paths.Root, "a.exe");
        var b = Path.Combine(paths.Root, "b.exe");
        File.WriteAllText(a, "same bytes");
        File.WriteAllText(b, "same bytes");
        Assert.Equal(WarmUp.MarkFile(paths, a), WarmUp.MarkFile(paths, b));
    }
}
