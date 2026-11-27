namespace Xvoid.Launcher.Core;

/// <summary>Every folder the launcher writes to, under one root.</summary>
public sealed class AppPaths
{
    public string Root { get; }
    /// <summary>Installed engine builds, one folder per engine (see <see cref="EnginesFor"/>).</summary>
    public string Engines => Path.Combine(Root, "engines");
    /// <summary>Translated shaders and pipelines, one folder per engine (see <see cref="CacheFor"/>).</summary>
    public string Cache => Path.Combine(Root, "cache");
    /// <summary>
    /// The engine's own data folder (VICTORY_DATA_DIR): the game's saves. Shared by both engines, so switching engine keeps
    /// the saves.
    /// </summary>
    public string Data => Path.Combine(Root, "data");
    public string Logs => Path.Combine(Root, "logs");
    public string Reports => Path.Combine(Root, "reports");
    public string SettingsFile => Path.Combine(Root, "settings.json");
    /// <summary>One empty file per engine program that was run once for the first-start warm-up.</summary>
    public string Warmed => Path.Combine(Root, "warmed");
    /// <summary>
    /// VICTORY_HOME for the engine's downloads (`victory get-dlss`): NVIDIA's files land under it, inside the launcher's own
    /// folder and not in %LOCALAPPDATA%\victory.
    /// </summary>
    public string EngineHome => Path.Combine(Root, "engine-home");
    /// <summary>Where `victory get-dlss` puts NVIDIA's DLSS and frame generation files (the engine's own layout under VICTORY_HOME).</summary>
    public string Streamline => Path.Combine(EngineHome, "nvidia-sdk", "Streamline", "bin", "x64");

    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "xvoid-launcher");
    }

    public string EnginesFor(EngineKind kind) => Path.Combine(Engines, EngineKinds.Folder(kind));
    public string CacheFor(EngineKind kind) => Path.Combine(Cache, EngineKinds.Folder(kind));

    /// <summary>The engine keeps host pipelines next to its shader cache.</summary>
    public string PipelineFolderFor(EngineKind kind) => Path.Combine(CacheFor(kind), "pipelines");
    public string ShaderFolderFor(EngineKind kind) => Path.Combine(CacheFor(kind), "shaders");

    /// <summary>True when NVIDIA's DLSS files (the interposer the engine looks for) are in place.</summary>
    public bool HasStreamline => File.Exists(Path.Combine(Streamline, "sl.interposer.dll"));

    public void EnsureCreated()
    {
        MoveOldLayout();
        foreach (var dir in new[]
        {
            Root, Engines, EnginesFor(EngineKind.Nvidia), EnginesFor(EngineKind.Amd), Cache, CacheFor(EngineKind.Nvidia),
            CacheFor(EngineKind.Amd), Data, Logs, Reports, Warmed, EngineHome,
        })
            Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Before the toggle there was one engine and one cache. Its builds are of no use to the new layout (they are
    /// downloaded again), but the cache of translated shaders and pipelines can take an hour to make, so it moves to the NVIDIA
    /// engine's folder, the engine it was made for.
    /// </summary>
    void MoveOldLayout()
    {
        try
        {
            if (Directory.Exists(Cache))
            {
                var kept = new[] { EngineKinds.Folder(EngineKind.Nvidia), EngineKinds.Folder(EngineKind.Amd) };
                var old = Directory.GetFileSystemEntries(Cache)
                    .Where(p => !kept.Contains(Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)).ToList();
                if (old.Count > 0)
                {
                    var target = CacheFor(EngineKind.Nvidia);
                    Directory.CreateDirectory(target);
                    foreach (var p in old)
                    {
                        var to = Path.Combine(target, Path.GetFileName(p));
                        if (Directory.Exists(p)) Directory.Move(p, to);
                        else File.Move(p, to);
                    }
                }
            }
            if (Directory.Exists(Engines))
            {
                var kept = new[] { EngineKinds.Folder(EngineKind.Nvidia), EngineKinds.Folder(EngineKind.Amd) };
                foreach (var dir in Directory.GetDirectories(Engines)
                             .Where(p => !kept.Contains(Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)))
                    Directory.Delete(dir, true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // What could not be moved stays where it is; the launcher starts either way.
        }
    }
}
