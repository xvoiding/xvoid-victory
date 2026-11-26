# Builds the launcher as one self-contained exe (no .NET install needed on the target PC).
#   launcher\build.ps1            -> launcher\dist\xvoid-launcher.exe
#   launcher\build.ps1 -FrameworkDependent   a ~1 MB exe that needs the .NET 8 Desktop Runtime installed
# Needs the .NET 8 SDK. The tests run with `dotnet test launcher\XvoidLauncher.sln` on any OS.
param([string]$Configuration = 'Release', [switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$out = Join-Path $PSScriptRoot 'dist'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
$self = if ($FrameworkDependent) { 'false' } else { 'true' }
$extra = if ($FrameworkDependent) { @() } else { @('-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true') }
dotnet publish src/Xvoid.Launcher -c $Configuration -r win-x64 --self-contained $self `
    -p:PublishSingleFile=true @extra -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }
$exe = Join-Path $out 'xvoid-launcher.exe'
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLower()
"$hash  xvoid-launcher.exe" | Set-Content (Join-Path $out 'xvoid-launcher.exe.sha256') -Encoding ascii
Write-Host ("Built {0} ({1} MB)" -f $exe, [math]::Round((Get-Item $exe).Length / 1MB, 1))
