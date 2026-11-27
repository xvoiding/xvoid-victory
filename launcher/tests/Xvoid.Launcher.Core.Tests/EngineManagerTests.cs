using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xvoid.Launcher.Core;
using Xunit;

namespace Xvoid.Launcher.Core.Tests;

public class EngineManagerTests
{
    [Theory]
    [InlineData("v0.1.0", "v0.2.0", true)]
    [InlineData("v0.2.0", "v0.2.0", false)]
    [InlineData("v0.10.0", "v0.9.0", false)]
    [InlineData("v1.0.0-rc1", "v1.0.1", true)]
    [InlineData(null, "v0.1.0", true)]
    [InlineData("nightly-1", "nightly-2", true)]
    [InlineData("nightly-2", "nightly-2", false)]
    public void Newer_versions(string? installed, string candidate, bool expected) =>
        Assert.Equal(expected, EngineManager.IsNewer(installed, candidate));

    [Fact]
    public void Checksum_file_formats()
    {
        var hash = new string('a', 64);
        Assert.Equal(hash, EngineManager.ParseChecksum(hash + "  victory-engine-win-x64.zip\n"));
        Assert.Equal(hash, EngineManager.ParseChecksum(hash));
        Assert.Throws<InvalidDataException>(() => EngineManager.ParseChecksum("not a hash"));
    }

    [Fact]
    public void Release_list_picks_the_first_with_the_asset_and_skips_drafts()
    {
        var json = $$"""
        [
          {"tag_name":"v0.3.0","draft":true,"assets":[]},
          {"tag_name":"v0.2.0","name":"No engine","assets":[{"name":"other.zip","browser_download_url":"u","size":1}]},
          {"tag_name":"v0.1.0","name":"First","prerelease":true,"published_at":"2026-11-26T00:00:00Z","assets":[
            {"name":"{{Branding.EngineAsset(EngineKind.Nvidia)}}","browser_download_url":"https://x/engine.zip","size":123},
            {"name":"{{Branding.EngineAsset(EngineKind.Nvidia)}}.sha256","browser_download_url":"https://x/engine.zip.sha256","size":65}]}
        ]
        """;
        var r = EngineManager.ParseReleases(JsonDocument.Parse(json), EngineKind.Nvidia);
        Assert.NotNull(r);
        Assert.Equal(EngineKind.Nvidia, r!.Kind);
        Assert.Equal("v0.1.0", r!.Tag);
        Assert.True(r.Prerelease);
        Assert.Equal("https://x/engine.zip.sha256", r.ChecksumUrl);
        Assert.Equal(123, r.AssetSize);
    }

    [Fact]
    public void No_matching_release_is_null() =>
        Assert.Null(EngineManager.ParseReleases(JsonDocument.Parse("[]"), EngineKind.Nvidia));

    [Fact]
    public void Each_engine_finds_only_its_own_asset_in_one_release()
    {
        // One release carries both engines' zips; the toggle must never install the wrong one.
        var json = $$"""
        [{"tag_name":"v0.1.5","published_at":"2026-11-27T00:00:00Z","assets":[
          {"name":"{{Branding.EngineAsset(EngineKind.Nvidia)}}","browser_download_url":"https://x/nvidia.zip","size":10},
          {"name":"{{Branding.EngineAsset(EngineKind.Nvidia)}}.sha256","browser_download_url":"https://x/nvidia.zip.sha256","size":65},
          {"name":"{{Branding.EngineAsset(EngineKind.Amd)}}","browser_download_url":"https://x/amd.zip","size":20},
          {"name":"{{Branding.EngineAsset(EngineKind.Amd)}}.sha256","browser_download_url":"https://x/amd.zip.sha256","size":65}]}]
        """;
        var nv = EngineManager.ParseReleases(JsonDocument.Parse(json), EngineKind.Nvidia)!;
        var amd = EngineManager.ParseReleases(JsonDocument.Parse(json), EngineKind.Amd)!;
        Assert.Equal("https://x/nvidia.zip", nv.AssetUrl);
        Assert.Equal("https://x/nvidia.zip.sha256", nv.ChecksumUrl);
        Assert.Equal("https://x/amd.zip", amd.AssetUrl);
        Assert.Equal("https://x/amd.zip.sha256", amd.ChecksumUrl);
    }

    [Fact]
    public void A_release_with_only_one_engine_is_skipped_for_the_other()
    {
        var json = $$"""
        [{"tag_name":"v0.1.4","assets":[
          {"name":"{{Branding.EngineAsset(EngineKind.Nvidia)}}","browser_download_url":"https://x/nvidia.zip","size":10}]}]
        """;
        Assert.NotNull(EngineManager.ParseReleases(JsonDocument.Parse(json), EngineKind.Nvidia));
        Assert.Null(EngineManager.ParseReleases(JsonDocument.Parse(json), EngineKind.Amd));
    }

    [Fact]
    public void Asset_names_are_per_engine() =>
        Assert.NotEqual(Branding.EngineAsset(EngineKind.Nvidia), Branding.EngineAsset(EngineKind.Amd));

    [Fact]
    public void Extract_refuses_paths_that_leave_the_target()
    {
        var dir = Directory.CreateTempSubdirectory("xvoid-zip").FullName;
        var zip = Path.Combine(dir, "evil.zip");
        using (var fs = File.Create(zip))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var e = archive.CreateEntry("../escaped.txt");
            using var w = new StreamWriter(e.Open(), Encoding.UTF8);
            w.Write("x");
        }
        Assert.Throws<InvalidDataException>(() => EngineManager.Extract(zip, Path.Combine(dir, "out")));
        Assert.False(File.Exists(Path.Combine(dir, "escaped.txt")));
    }

    [Fact]
    public async Task Sha256_of_a_file_matches_the_known_value()
    {
        var f = Path.GetTempFileName();
        await File.WriteAllTextAsync(f, "abc");
        // SHA-256("abc")
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", await EngineManager.Sha256Async(f));
    }

    [Fact]
    public void Resolve_prefers_a_local_build_when_chosen()
    {
        var paths = new AppPaths(Directory.CreateTempSubdirectory("xvoid-eng").FullName);
        var m = new EngineManager(paths);
        var exe = Path.Combine(paths.Root, "victory.exe");
        File.WriteAllText(exe, "");
        Assert.Equal(exe, m.Resolve(new Settings { EngineSource = EngineSource.Local, LocalEnginePath = exe }, EngineKind.Nvidia));
        Assert.Null(m.Resolve(new Settings { EngineSource = EngineSource.Local, LocalEnginePath = exe + "x" }, EngineKind.Nvidia));
        Assert.Null(m.Resolve(new Settings { EngineSource = EngineSource.Release, InstalledNvidiaTag = "v9" }, EngineKind.Nvidia));
    }

    [Fact]
    public void Resolve_finds_each_engines_own_installed_build()
    {
        var paths = new AppPaths(Directory.CreateTempSubdirectory("xvoid-eng2").FullName);
        var m = new EngineManager(paths);
        var nv = m.ExeFor(EngineKind.Nvidia, "v1");
        Assert.Null(nv);
        Directory.CreateDirectory(m.FolderFor(EngineKind.Nvidia, "v1"));
        File.WriteAllText(Path.Combine(m.FolderFor(EngineKind.Nvidia, "v1"), Branding.EngineExe), "");
        Directory.CreateDirectory(m.FolderFor(EngineKind.Amd, "v2"));
        File.WriteAllText(Path.Combine(m.FolderFor(EngineKind.Amd, "v2"), Branding.EngineExe), "");
        var s = new Settings { InstalledNvidiaTag = "v1", InstalledAmdTag = "v2" };
        var nvExe = m.Resolve(s, EngineKind.Nvidia);
        var amdExe = m.Resolve(s, EngineKind.Amd);
        Assert.NotNull(nvExe);
        Assert.NotNull(amdExe);
        Assert.NotEqual(nvExe, amdExe);
        Assert.Contains("nvidia", nvExe!);
        Assert.Contains("amd", amdExe!);
        // An AMD tag does not make the NVIDIA engine "installed".
        Assert.Null(m.Resolve(new Settings { InstalledAmdTag = "v2" }, EngineKind.Nvidia));
    }

    [Fact]
    public void Prune_only_touches_the_engine_it_is_asked_about()
    {
        var paths = new AppPaths(Directory.CreateTempSubdirectory("xvoid-prune").FullName);
        var m = new EngineManager(paths);
        foreach (var (k, t) in new[] { (EngineKind.Nvidia, "v1"), (EngineKind.Nvidia, "v2"), (EngineKind.Amd, "v1") })
            Directory.CreateDirectory(m.FolderFor(k, t));
        m.Prune(EngineKind.Nvidia, "v2");
        Assert.False(Directory.Exists(m.FolderFor(EngineKind.Nvidia, "v1")));
        Assert.True(Directory.Exists(m.FolderFor(EngineKind.Nvidia, "v2")));
        Assert.True(Directory.Exists(m.FolderFor(EngineKind.Amd, "v1")));
    }
}
