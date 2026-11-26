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
            {"name":"{{Branding.EngineAsset}}","browser_download_url":"https://x/engine.zip","size":123},
            {"name":"{{Branding.EngineAsset}}.sha256","browser_download_url":"https://x/engine.zip.sha256","size":65}]}
        ]
        """;
        var r = EngineManager.ParseReleases(JsonDocument.Parse(json));
        Assert.NotNull(r);
        Assert.Equal("v0.1.0", r!.Tag);
        Assert.True(r.Prerelease);
        Assert.Equal("https://x/engine.zip.sha256", r.ChecksumUrl);
        Assert.Equal(123, r.AssetSize);
    }

    [Fact]
    public void No_matching_release_is_null() =>
        Assert.Null(EngineManager.ParseReleases(JsonDocument.Parse("[]")));

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
        Assert.Equal(exe, m.Resolve(new Settings { EngineSource = EngineSource.Local, LocalEnginePath = exe }));
        Assert.Null(m.Resolve(new Settings { EngineSource = EngineSource.Local, LocalEnginePath = exe + "x" }));
        Assert.Null(m.Resolve(new Settings { EngineSource = EngineSource.Release, InstalledEngineTag = "v9" }));
    }
}
