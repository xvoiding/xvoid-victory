using System.Security.Cryptography;

namespace Xvoid.Launcher.Core;

/// <summary>
/// The first-start warm-up. The NVIDIA engine's author found that every new victory.exe stays at a flat 14 fps on its first
/// run, however long that run is, and that every later run is normal (27-29 fps on an RTX 5070); the likely cause is the
/// driver's cache of the program. The engine's own launcher plays a new exe for 20 seconds and starts it again, once. This
/// does the same from the launcher, where the engine cannot restart itself (the launcher reads the engine's output).
/// </summary>
public static class WarmUp
{
    /// <summary>Seconds of play, counted from the first frame, after which the build is marked and started again.</summary>
    public const int Seconds = 20;

    /// <summary>The mark for this exact program: the first 16 hex digits of its SHA-256, as the engine names its own marks.</summary>
    public static string MarkFile(AppPaths paths, string exe)
    {
        using var stream = File.OpenRead(exe);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return Path.Combine(paths.Warmed, hash[..16]);
    }

    /// <summary>True when this start should be the warm-up one: the NVIDIA engine, a program with no mark, and the setting on.</summary>
    public static bool Due(Settings s, EngineKind kind, string markFile) =>
        s.WarmUp && kind == EngineKind.Nvidia && !File.Exists(markFile);

    public static void Mark(string markFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(markFile)!);
        File.WriteAllText(markFile, "this victory.exe was run once for the warm-up (its first start, then a restart)\r\n");
    }
}
