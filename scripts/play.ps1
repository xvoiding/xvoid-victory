# Starts the title with the settings this fork is played with (README.md, "How to run it").
#   scripts\play.ps1 -Exe <path of your own copy's executable>
# -Safe         without the newer, faster ways (the first thing to try when something is wrong)
# -RayTracing   the title's own ray-traced indirect light: lit interiors, about a third slower
# -Build        target\diag (the default, the build all measurements are of) or target\release
# -More         other settings: -More @{VICTORY_PANEL='1'}
# -Console      SeriesX (default) or SeriesS: the console model the title sees. Series S gives the
#               title about 7.7 GiB instead of about 13.5 GiB, so less of everything is kept.
# -TextureBudgetMB  limit, in MB, on the host copies of textures the title fills (VICTORY_TEXTURE_BUDGET_MB)
# -LowMemory    for 8 GB cards and 32 GB of RAM: Series S, and a texture budget of 3072 MB unless
#               -TextureBudgetMB says another. A starting point to tune, not a measured setting
#               (docs/low-memory.md).
# -Ambient     the fork's stand-in for the title's ray-traced indirect light (VICTORY_AMBIENT=0.8). OFF by default here:
#              on an RTX 4060 Laptop it blew the picture out to a white blob (2026-11-25, see docs/low-memory.md).
# -ThinThreads threads that translate list calls (VICTORY_THIN_THREADS). Default 4 here (the 7900 XTX fork's is 8,
#              which took all of a laptop i7).
# -NoBook      do not write down the pipelines the run makes (scripts\prep.ps1 -Warm builds them ahead)
# Run it from a normal window, not as administrator: the driver then keeps its shader cache elsewhere.
param(
    [Parameter(Mandatory)][string]$Exe,
    [switch]$Safe,
    [switch]$RayTracing,
    [ValidateSet('diag', 'release')][string]$Build = 'diag',
    [ValidateSet('SeriesX', 'SeriesS')][string]$Console = 'SeriesX',
    [int]$TextureBudgetMB = 0,
    [switch]$LowMemory,
    [switch]$Ambient,
    [switch]$NoBook,
    [int]$ThinThreads = 4,
    [hashtable]$More = @{}
)
if ($LowMemory) {
    $Console = 'SeriesS'
    if ($TextureBudgetMB -le 0) { $TextureBudgetMB = 3072 }
}
$settings = @{
    VICTORY_TEXTURE_NO_WAIT = '1'; VICTORY_RELAY = '2'; VICTORY_WIDE_BACK = '1'; VICTORY_CARD_REHASH_RUNS = '1'
    VICTORY_JOIN_COPIES = '1'; VICTORY_INDIRECT_WINDOW = '1'; VICTORY_RELAY_COPY = '1'; VICTORY_CARD_STEPS = '2'
    VICTORY_INDIRECT_BATCH = '2'; VICTORY_MAP_THREADS = '1'; VICTORY_TITLE_PRIORITIES = '0'; VICTORY_CARD_AWAKE = '1'
    VICTORY_SHOW_AFTER = '1'; VICTORY_D3D12_DRED = 'light'; VICTORY_SKIP_PIPELINES = 'none'
}
if ($Safe) {
    $settings.VICTORY_UNDER_WIDE = '0'; $settings.VICTORY_LAND_ECHO = '0'
} else {
    $settings.VICTORY_THIN = '1'; $settings.VICTORY_THIN_THREADS = [string]$ThinThreads; $settings.VICTORY_JOIN_PARTS = '8'; $settings.VICTORY_UPLOAD_AHEAD = '1'
}
if ($Ambient) { $settings.VICTORY_AMBIENT = '0.8' }
if (-not $NoBook) { $settings.VICTORY_PIPELINE_BOOK = '1' }   # kept for prep.ps1 -Warm
if ($RayTracing) { $settings.VICTORY_BVH_BUILDER = '1' }
if ($TextureBudgetMB -gt 0) { $settings.VICTORY_TEXTURE_BUDGET_MB = [string]$TextureBudgetMB }
foreach ($k in $More.Keys) { $settings[$k] = $More[$k] }
# (clap's spelling of the console names: series-x, series-s.)
$consoleArg = if ($Console -eq 'SeriesS') { 'series-s' } else { 'series-x' }

$victory = Join-Path $PSScriptRoot "..\target\$Build\victory.exe"
if (-not (Test-Path $victory)) { Write-Host "No $victory. Build it first (README.md)."; return }
if (-not (Test-Path $Exe)) { Write-Host "No $Exe."; return }
# (Other settings left over in this window go; where your files are stays.)
$paths = 'VICTORY_CONFIG', 'VICTORY_GAME_DIR', 'VICTORY_CACHE_DIR', 'VICTORY_DATA_DIR'
Get-ChildItem Env:VICTORY_* | Where-Object { $_.Name -notin $paths } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
foreach ($k in $settings.Keys) { Set-Item "Env:$k" $settings[$k] }
$Exe = (Resolve-Path $Exe).Path
Push-Location (Join-Path $PSScriptRoot '..')   # victory.toml is looked for here
try { & $victory run $Exe --execute --console $consoleArg } finally { Pop-Location }
