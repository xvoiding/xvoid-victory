# Does ahead of time what the layer otherwise does while you play (README.md, "How to run it"):
#   scripts\prep.ps1 -Exe <your exe>          every shader of the title's folder translated into the cache (minutes)
#   scripts\prep.ps1 -Exe <your exe> -Warm    then the host pipelines earlier runs drew with (10-60 minutes; needs one run of
#                                             scripts\play.ps1 first, which writes them down). Both can be stopped and go on.
# It translates for THIS GPU: the cache is keyed by the host's wave size and view limit, so those are set to what the run log
# of an NVIDIA card says (wave lanes 32, 1000000 views). For an AMD card pass -Wave 64 -ViewLimit 0 -Rdna (the log of a run
# says which numbers your card has: "host wave lanes min=.. max=..", "host view heaps hold .. views").
param(
    [Parameter(Mandatory)][string]$Exe,
    [switch]$Warm,
    [ValidateSet('diag', 'release')][string]$Build = 'diag',
    [int]$Wave = 32,
    [int]$ViewLimit = 1000000,
    [switch]$Rdna,
    [int]$Jobs = 0
)
$victory = Join-Path $PSScriptRoot "..\target\$Build\victory.exe"
if (-not (Test-Path $victory)) { Write-Host "No $victory. Build it first (README.md)."; return }
if (-not (Test-Path $Exe)) { Write-Host "No $Exe."; return }
$Exe = (Resolve-Path $Exe).Path
$paths = 'VICTORY_CONFIG', 'VICTORY_GAME_DIR', 'VICTORY_CACHE_DIR', 'VICTORY_DATA_DIR'
Get-ChildItem Env:VICTORY_* | Where-Object { $_.Name -notin $paths } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
if (-not $env:VICTORY_GAME_DIR) { $env:VICTORY_GAME_DIR = Split-Path $Exe }
$jobArgs = @(); if ($Jobs -gt 0) { $jobArgs = @('--jobs', "$Jobs") }
Push-Location (Join-Path $PSScriptRoot '..')
try {
    if (-not $Warm) {
        if ($Wave -gt 0) { $env:VICTORY_HOST_WAVE = "$Wave" }
        if ($ViewLimit -gt 0) { $env:VICTORY_VIEW_LIMIT = "$ViewLimit" }
        if ($Rdna) { $env:VICTORY_HOST_RDNA = '1' }
        Write-Host "== Translating every shader under $env:VICTORY_GAME_DIR (wave $Wave, view limit $ViewLimit). Leave the PC alone." -ForegroundColor Cyan
        & $victory shaders $env:VICTORY_GAME_DIR @jobArgs 2>&1 | Tee-Object prep_shaders.txt
        Write-Host 'Done. Next: scripts\play.ps1 once (it writes down the pipelines), then this script with -Warm.' -ForegroundColor Green
    } else {
        # `warm` starts processes that make the pipelines on the real GPU with the run's own settings: none of the above is set.
        Write-Host '== Making the host pipelines earlier runs drew with (goes on where it was stopped; Ctrl+C is safe).' -ForegroundColor Cyan
        & $victory warm --used @jobArgs 2>&1 | Tee-Object prep_warm.txt
        Write-Host 'Done.' -ForegroundColor Green
    }
} finally { Pop-Location }
