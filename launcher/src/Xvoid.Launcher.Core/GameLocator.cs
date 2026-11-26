namespace Xvoid.Launcher.Core;

/// <summary>Helps the person point at the title's executable; the engine reads everything else from that folder.</summary>
public static class GameLocator
{
    /// <summary>The executable to start, given a folder or a file the person chose.</summary>
    public static string? Find(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (File.Exists(path)) return path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? Path.GetFullPath(path) : null;
        if (!Directory.Exists(path)) return null;
        var exes = Directory.EnumerateFiles(path, "*.exe", SearchOption.TopDirectoryOnly).ToList();
        // A console title's package has one main executable; a launcher stub or redistributable is not it.
        var best = exes
            .Where(e => !Path.GetFileName(e).Contains("setup", StringComparison.OrdinalIgnoreCase)
                     && !Path.GetFileName(e).Contains("redist", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => new FileInfo(e).Length)
            .FirstOrDefault();
        return best is null ? null : Path.GetFullPath(best);
    }

    public static string? Validate(string? exe)
    {
        if (string.IsNullOrEmpty(exe)) return "Choose the game's executable.";
        if (!File.Exists(exe)) return "That file is not there any more.";
        var dir = Path.GetDirectoryName(exe)!;
        return Directory.EnumerateFileSystemEntries(dir).Take(3).Count() < 3
            ? "That folder has almost nothing in it. Choose the folder that holds your game's files."
            : null;
    }
}
