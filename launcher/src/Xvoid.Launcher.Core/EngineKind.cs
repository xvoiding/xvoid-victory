namespace Xvoid.Launcher.Core;

/// <summary>
/// The two engine builds the launcher can run: the NVIDIA one (trustno1-coder/VIctory-NVIDIA) and the AMD one
/// (Belobug/VIctory-Belobug-AMD). They are different programs with different tuning, caches and release files.
/// </summary>
public enum EngineKind { Nvidia, Amd }

/// <summary>What was picked on the toggle: one of the two, or Auto, where this PC's graphics card decides.</summary>
public enum EngineChoice { Auto, Nvidia, Amd }

public static class EngineKinds
{
    public static string Name(EngineKind kind) => kind == EngineKind.Nvidia ? "NVIDIA" : "AMD";

    /// <summary>A folder-safe name, for the engine's builds and its own cache.</summary>
    public static string Folder(EngineKind kind) => kind == EngineKind.Nvidia ? "nvidia" : "amd";

    /// <summary>Auto picks the AMD build when the fastest card is a Radeon and the NVIDIA build for everything else.</summary>
    public static EngineKind Resolve(EngineChoice choice, SystemSnapshot? system) => choice switch
    {
        EngineChoice.Nvidia => EngineKind.Nvidia,
        EngineChoice.Amd => EngineKind.Amd,
        _ => system?.BestGpu?.Vendor == GpuVendor.Amd ? EngineKind.Amd : EngineKind.Nvidia,
    };

    /// <summary>RTX 40 and 50 cards run DLSS frame generation; older cards do not (the engine says so at the start).</summary>
    public static bool HasDlssFrameGeneration(string gpuName)
    {
        var d = gpuName.ToLowerInvariant();
        var at = d.IndexOf("rtx ", StringComparison.Ordinal);
        if (at < 0) return false;
        var digits = new string(d.Skip(at + 4).SkipWhile(char.IsWhiteSpace).TakeWhile(char.IsDigit).ToArray());
        // "rtx 4060" -> 40, "rtx 5070 ti" -> 50; "rtx a2000" has no digits first and is not counted.
        return digits.Length >= 4 && int.TryParse(digits[..2], out var series) && series >= 40;
    }
}
