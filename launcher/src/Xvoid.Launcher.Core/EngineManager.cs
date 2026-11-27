using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Xvoid.Launcher.Core;

public sealed record EngineRelease(EngineKind Kind, string Tag, string Name, bool Prerelease, DateTimeOffset Published, string AssetUrl, long AssetSize, string? ChecksumUrl);

/// <summary>
/// Finds, downloads, verifies and unpacks engine builds from the project's GitHub releases. Each release carries one zip per
/// engine (NVIDIA, AMD); each engine has its own folder of installed builds.
/// </summary>
public sealed class EngineManager
{
    readonly AppPaths _paths;
    readonly HttpClient _http;

    public EngineManager(AppPaths paths, HttpClient? http = null)
    {
        _paths = paths;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("xvoid-launcher", "0.1"));
    }

    public string FolderFor(EngineKind kind, string tag) => Path.Combine(_paths.EnginesFor(kind), Sanitize(tag));

    public string? ExeFor(EngineKind kind, string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;
        var exe = Path.Combine(FolderFor(kind, tag), Branding.EngineExe);
        return File.Exists(exe) ? exe : null;
    }

    /// <summary>The engine to run: a local build if one is chosen, else the installed release of that engine.</summary>
    public string? Resolve(Settings s, EngineKind kind) =>
        s.EngineSource == EngineSource.Local
            ? (File.Exists(s.LocalEnginePath) ? s.LocalEnginePath : null)
            : ExeFor(kind, s.InstalledTag(kind));

    /// <summary>The newest release that has this engine's asset (pre-releases count: early builds are published as such).</summary>
    public async Task<EngineRelease?> LatestAsync(EngineKind kind, CancellationToken ct = default)
    {
        var url = $"https://api.github.com/repos/{Branding.RepoOwner}/{Branding.RepoName}/releases?per_page=10";
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return ParseReleases(await JsonDocument.ParseAsync(stream, cancellationToken: ct), kind);
    }

    public static EngineRelease? ParseReleases(JsonDocument doc, EngineKind kind)
    {
        var wanted = Branding.EngineAsset(kind);
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            if (r.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            string? asset = null, sum = null; long size = 0;
            foreach (var a in r.GetProperty("assets").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString();
                var link = a.GetProperty("browser_download_url").GetString();
                if (name == wanted) { asset = link; size = a.GetProperty("size").GetInt64(); }
                else if (name == wanted + Branding.ChecksumSuffix) sum = link;
            }
            if (asset is null) continue;
            var tag = r.GetProperty("tag_name").GetString() ?? "unknown";
            var title = r.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } t ? t : tag;
            var published = r.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetDateTimeOffset() : DateTimeOffset.MinValue;
            return new EngineRelease(kind, tag, title, r.TryGetProperty("prerelease", out var pre) && pre.GetBoolean(), published, asset, size, sum);
        }
        return null;
    }

    /// <summary>Downloads, checks the SHA-256 and unpacks. A release with no checksum file is refused.</summary>
    public async Task<string> InstallAsync(EngineRelease release, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (release.ChecksumUrl is null)
            throw new InvalidOperationException("This release has no checksum file, so it cannot be verified. Nothing was installed.");
        _paths.EnsureCreated();
        var expected = ParseChecksum(await _http.GetStringAsync(release.ChecksumUrl, ct));

        var zip = Path.Combine(Path.GetTempPath(), $"xvoid-engine-{Guid.NewGuid():N}.zip");
        try
        {
            using (var response = await _http.GetAsync(release.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? release.AssetSize;
                await using var src = await response.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(zip);
                var buffer = new byte[1 << 16];
                long done = 0; int n;
                while ((n = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                    done += n;
                    if (total > 0) progress?.Report(Math.Min(0.9, 0.9 * done / total));
                }
            }

            var actual = await Sha256Async(zip, ct);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The download does not match its checksum. Nothing was installed.");

            var target = FolderFor(release.Kind, release.Tag);
            var staging = target + ".part";
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            Extract(zip, staging);
            if (!File.Exists(Path.Combine(staging, Branding.EngineExe)))
                throw new InvalidDataException($"The archive has no {Branding.EngineExe}.");
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.Move(staging, target);
            progress?.Report(1);
            return Path.Combine(target, Branding.EngineExe);
        }
        finally
        {
            try { File.Delete(zip); } catch (IOException) { }
        }
    }

    /// <summary>Removes this engine's installed builds other than the one in use. The other engine's are left alone.</summary>
    public void Prune(EngineKind kind, string keepTag)
    {
        var folder = _paths.EnginesFor(kind);
        if (!Directory.Exists(folder)) return;
        foreach (var dir in Directory.GetDirectories(folder))
            if (!string.Equals(Path.GetFileName(dir), Sanitize(keepTag), StringComparison.OrdinalIgnoreCase))
                try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    public static string ParseChecksum(string text)
    {
        // "<hex>  file" (sha256sum) or just "<hex>".
        var first = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (first.Length != 64 || !first.All(Uri.IsHexDigit))
            throw new InvalidDataException("The checksum file is not a SHA-256 value.");
        return first;
    }

    public static async Task<string> Sha256Async(string file, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(file);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
    }

    /// <summary>Unpacks without letting an entry write outside the target (zip-slip).</summary>
    public static void Extract(string zip, string target)
    {
        Directory.CreateDirectory(target);
        var root = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(zip);
        foreach (var entry in archive.Entries)
        {
            var path = Path.GetFullPath(Path.Combine(target, entry.FullName));
            if (!path.StartsWith(root, StringComparison.Ordinal))
                throw new InvalidDataException($"Unsafe path in archive: {entry.FullName}");
            if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(path); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path, overwrite: true);
        }
    }

    /// <summary>True when <paramref name="candidate"/> is a later version than <paramref name="installed"/> (v1.2.3 style).</summary>
    public static bool IsNewer(string? installed, string candidate)
    {
        if (string.IsNullOrEmpty(installed)) return true;
        if (TryVersion(installed, out var a) && TryVersion(candidate, out var b)) return b > a;
        return !string.Equals(installed, candidate, StringComparison.OrdinalIgnoreCase);
    }

    static bool TryVersion(string tag, out Version v)
    {
        var t = tag.TrimStart('v', 'V');
        var cut = t.IndexOfAny(new[] { '-', '+' });
        if (cut >= 0) t = t[..cut];
        return Version.TryParse(t, out v!);
    }

    static string Sanitize(string tag) => string.Concat(tag.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
