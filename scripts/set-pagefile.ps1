# A fixed paging file big enough for the title (docs/low-memory.md, item 1). The title's private memory was 40-46 GB on the
# 7900 XTX fork's PC; on a 32 GB PC with no paging file (AtlasOS and other debloated Windows builds turn it off) a run ended at
# about 24 GB private with "memory allocation of 1048576 bytes failed". Needs admin (it asks) and a REBOOT afterwards.
param([int]$SizeGB = 64, [int]$KeepFreeGB = 15)
$ErrorActionPreference = 'Stop'
$me = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $me.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-SizeGB', $SizeGB, '-KeepFreeGB', $KeepFreeGB
    return
}
$best = Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | Sort-Object FreeSpace -Descending | Select-Object -First 1
$freeGB = [math]::Floor($best.FreeSpace / 1GB)
$mb = [math]::Min($SizeGB * 1024, ($freeGB - $KeepFreeGB) * 1024)
Write-Host "Best drive $($best.DeviceID): $freeGB GB free."
if ($mb -lt 24576) { Write-Host "!! Not enough room for a 24 GB+ paging file while keeping $KeepFreeGB GB free. Free up space and run this again." -ForegroundColor Red; Read-Host 'Enter to close'; return }
Write-Host "Setting $($best.DeviceID)\pagefile.sys to $mb MB, fixed."
$cs = Get-CimInstance Win32_ComputerSystem
if ($cs.AutomaticManagedPagefile) { Set-CimInstance -InputObject $cs -Property @{ AutomaticManagedPagefile = $false } }
Get-CimInstance Win32_PageFileSetting -ErrorAction SilentlyContinue | Remove-CimInstance
New-CimInstance -ClassName Win32_PageFileSetting -Property @{ Name = "$($best.DeviceID)\pagefile.sys"; InitialSize = [uint32]$mb; MaximumSize = [uint32]$mb } | Out-Null
Write-Host 'Done. Restart Windows for it to apply.' -ForegroundColor Green
Read-Host 'Enter to close'
