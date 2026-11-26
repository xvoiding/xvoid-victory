using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using Microsoft.Win32;

namespace Xvoid.Launcher.Core;

public enum GpuVendor { Unknown, Nvidia, Amd, Intel }

public sealed record GpuInfo(string Name, GpuVendor Vendor, double? VramGb, string? Driver)
{
    /// <summary>Integrated parts report little or no dedicated memory.</summary>
    public bool LooksIntegrated => Vendor == GpuVendor.Intel || VramGb is null or < 1.5;
}

public sealed record SystemSnapshot(
    double RamGb,
    double RamFreeGb,
    double CommitLimitGb,
    double CommitFreeGb,
    int LogicalProcessors,
    bool Avx2,
    bool Windows64,
    IReadOnlyList<GpuInfo> Gpus,
    double? CacheDriveFreeGb)
{
    /// <summary>The adapter most likely to be the fast one.</summary>
    public GpuInfo? BestGpu => Gpus.OrderByDescending(g => g.VramGb ?? 0).FirstOrDefault();
    public bool Hybrid => Gpus.Count > 1 && Gpus.Any(g => g.LooksIntegrated) && Gpus.Any(g => !g.LooksIntegrated);
}

/// <summary>Reads what the checks need. Every read can fail on its own; a failed one is left out, not guessed.</summary>
public static class SystemInfo
{
    [StructLayout(LayoutKind.Sequential)]
    struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    const double GiB = 1024.0 * 1024 * 1024;

    public static SystemSnapshot Read(string? cacheFolder = null)
    {
        double ram = 0, ramFree = 0, commit = 0, commitFree = 0;
        if (OperatingSystem.IsWindows())
        {
            var m = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref m))
            {
                ram = m.TotalPhys / GiB; ramFree = m.AvailPhys / GiB;
                commit = m.TotalPageFile / GiB; commitFree = m.AvailPageFile / GiB;
            }
        }
        double? disk = null;
        try
        {
            var root = cacheFolder is null ? null : Path.GetPathRoot(Path.GetFullPath(cacheFolder));
            if (!string.IsNullOrEmpty(root)) disk = new DriveInfo(root).AvailableFreeSpace / GiB;
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { }

        return new SystemSnapshot(ram, ramFree, commit, commitFree, Environment.ProcessorCount,
            Avx2.IsSupported, Environment.Is64BitOperatingSystem && OperatingSystem.IsWindows(), ReadGpus(), disk);
    }

    static IReadOnlyList<GpuInfo> ReadGpus()
    {
        var list = new List<GpuInfo>();
        if (!OperatingSystem.IsWindows()) return list;
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11cd-be10-08002be10318}");
            if (classKey is null) return list;
            foreach (var name in classKey.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit)))
            {
                using var key = classKey.OpenSubKey(name);
                var desc = key?.GetValue("DriverDesc") as string;
                if (string.IsNullOrWhiteSpace(desc) || desc.Contains("Basic Display", StringComparison.OrdinalIgnoreCase)
                    || desc.Contains("Remote Display", StringComparison.OrdinalIgnoreCase)) continue;
                double? vram = key!.GetValue("HardwareInformation.qwMemorySize") switch
                {
                    long l when l > 0 => l / GiB,
                    ulong u when u > 0 => u / GiB,
                    byte[] b when b.Length == 8 => BitConverter.ToInt64(b) / GiB,
                    _ => null,
                };
                list.Add(new GpuInfo(desc, VendorOf(desc), vram, key.GetValue("DriverVersion") as string));
            }
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException) { }
        return list;
    }

    public static GpuVendor VendorOf(string description)
    {
        var d = description.ToLowerInvariant();
        if (d.Contains("nvidia") || d.Contains("geforce") || d.Contains("rtx") || d.Contains("quadro")) return GpuVendor.Nvidia;
        if (d.Contains("amd") || d.Contains("radeon") || d.Contains("ati ")) return GpuVendor.Amd;
        if (d.Contains("intel") || d.Contains("iris") || d.Contains("uhd graphics")) return GpuVendor.Intel;
        return GpuVendor.Unknown;
    }
}
