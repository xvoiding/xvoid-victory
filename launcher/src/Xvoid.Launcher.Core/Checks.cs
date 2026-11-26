namespace Xvoid.Launcher.Core;

public enum Severity { Ok, Warn, Fail }

/// <summary>What a check can offer to repair.</summary>
public enum Fix { None, PagingFile, GpuPreference }

public sealed record Check(string Id, string Title, string Detail, Severity Severity, Fix Fix = Fix.None);

/// <summary>
/// The things that decided whether the engine ran at all on the first PC it was measured on
/// (an RTX 4060 Laptop with 8 GB, 32 GB of RAM): memory and commit, the right GPU, AVX2.
/// Thresholds come from the engine's notes, not from a benchmark.
/// </summary>
public static class Checks
{
    public static IReadOnlyList<Check> Run(SystemSnapshot s, bool gpuPreferenceSet)
    {
        var list = new List<Check>();

        list.Add(s.Windows64
            ? new("os", "Windows x64", "64-bit Windows.", Severity.Ok)
            : new("os", "Windows x64", "The engine needs 64-bit Windows 10 or 11.", Severity.Fail));

        list.Add(s.Avx2
            ? new("avx2", "Processor", $"{s.LogicalProcessors} logical processors, AVX2 present.", Severity.Ok)
            : new("avx2", "Processor", "No AVX2. The engine's compare loops need it.", Severity.Fail));

        // RAM. The engine used 40-46 GB of private memory on the machine it was tuned on.
        list.Add(s.RamGb switch
        {
            0 => new("ram", "Memory", "Could not be read.", Severity.Warn),
            < 15.5 => new("ram", "Memory", $"{s.RamGb:0} GB of RAM. The engine used 40-46 GB on its first test PC; this will page constantly.", Severity.Fail),
            < 31.5 => new("ram", "Memory", $"{s.RamGb:0} GB of RAM. Expect heavy paging; 32 GB is the least that is workable.", Severity.Warn),
            < 63 => new("ram", "Memory", $"{s.RamGb:0} GB of RAM. Workable with a large paging file.", Severity.Ok),
            _ => new("ram", "Memory", $"{s.RamGb:0} GB of RAM.", Severity.Ok),
        });

        // Commit: RAM plus paging file. A run died at ~24 GB private when this was ~30 GB.
        if (s.CommitLimitGb > 0)
        {
            var enough = s.CommitLimitGb >= 60;
            list.Add(enough
                ? new("commit", "Paging file", $"{s.CommitLimitGb:0} GB of commit (RAM plus paging file).", Severity.Ok)
                : new("commit", "Paging file",
                    $"Only {s.CommitLimitGb:0} GB of commit. A run on a 32 GB PC with no paging file ended at about 24 GB used with \"memory allocation failed\". A fixed 64 GB paging file fixes that (restart needed).",
                    Severity.Warn, Fix.PagingFile));
        }

        // GPU.
        var gpu = s.BestGpu;
        if (gpu is null)
            list.Add(new("gpu", "Graphics", "No adapter could be read. The engine reports its own at start.", Severity.Warn));
        else
        {
            var vram = gpu.VramGb;
            var sev = vram switch { null => Severity.Warn, < 7.5 => Severity.Fail, < 11.5 => Severity.Warn, _ => Severity.Ok };
            var note = vram switch
            {
                null => "memory size unknown",
                < 7.5 => $"{vram:0.#} GB of video memory: below what the engine has run on",
                < 11.5 => $"{vram:0.#} GB of video memory: tight, the engine was tuned for 24 GB",
                _ => $"{vram:0.#} GB of video memory",
            };
            list.Add(new("gpu", "Graphics", $"{gpu.Name}, {note}.", sev));
        }

        if (s.Hybrid)
            list.Add(gpuPreferenceSet
                ? new("gpuPref", "GPU choice", "Windows is set to use the high-performance GPU for the engine.", Severity.Ok)
                : new("gpuPref", "GPU choice",
                    "This PC has an integrated and a dedicated GPU. The engine uses whichever Windows picks, so it should be set to high performance.",
                    Severity.Warn, Fix.GpuPreference));

        if (s.CacheDriveFreeGb is { } free)
            list.Add(free < 15
                ? new("disk", "Disk space", $"{free:0} GB free where the cache lives; translated shaders and pipelines need several GB.", Severity.Warn)
                : new("disk", "Disk space", $"{free:0} GB free for the cache.", Severity.Ok));

        return list;
    }

    public static bool Blocking(IEnumerable<Check> checks) => checks.Any(c => c.Severity == Severity.Fail && c.Id is "os" or "avx2");
}
