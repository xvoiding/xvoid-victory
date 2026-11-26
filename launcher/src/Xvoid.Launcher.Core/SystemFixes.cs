using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace Xvoid.Launcher.Core;

/// <summary>The two repairs the setup checks can offer. Neither runs without the person pressing the button.</summary>
public static class SystemFixes
{
    const string GpuKey = @"Software\Microsoft\DirectX\UserGpuPreferences";

    /// <summary>"Graphics settings, high performance" for one executable; per user, no administrator needed.</summary>
    public static bool IsHighPerformance(string exe)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(GpuKey);
            return key?.GetValue(Path.GetFullPath(exe)) is string v && v.Contains("GpuPreference=2", StringComparison.Ordinal);
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException) { return false; }
    }

    public static void SetHighPerformance(string exe)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var key = Registry.CurrentUser.CreateSubKey(GpuKey);
        key.SetValue(Path.GetFullPath(exe), "GpuPreference=2;", RegistryValueKind.String);
    }

    /// <summary>
    /// Sets one fixed paging file on the drive with the most room and turns off automatic management. Asks Windows for
    /// administrator rights (a UAC prompt); returns false if that was declined. Takes effect after a restart.
    /// </summary>
    public static bool SetPagingFile(int sizeGb = 64, int keepFreeGb = 15)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $best = Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | Sort-Object FreeSpace -Descending | Select-Object -First 1
            $freeGB = [math]::Floor($best.FreeSpace / 1GB)
            $mb = [math]::Min({{sizeGb}} * 1024, ($freeGB - {{keepFreeGb}}) * 1024)
            if ($mb -lt 24576) { exit 2 }
            $cs = Get-CimInstance Win32_ComputerSystem
            if ($cs.AutomaticManagedPagefile) { Set-CimInstance -InputObject $cs -Property @{ AutomaticManagedPagefile = $false } }
            Get-CimInstance Win32_PageFileSetting -ErrorAction SilentlyContinue | Remove-CimInstance
            New-CimInstance -ClassName Win32_PageFileSetting -Property @{ Name = "$($best.DeviceID)\pagefile.sys"; InitialSize = [uint32]$mb; MaximumSize = [uint32]$mb } | Out-Null
            """;
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var info = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand {encoded}")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            using var p = Process.Start(info);
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception) { return false; } // the UAC prompt was declined
    }
}
