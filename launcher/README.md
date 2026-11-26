# xvoid Launcher

A small Windows launcher for this fork of VIctory. It sets the engine up, checks that the PC can hold it, prepares the
game's shaders ahead of time, starts the game with settings that suit the PC, and shows what the engine is doing while it
runs. It replaces the PowerShell scripts in `scripts\` for people who do not want a terminal.

**It contains no game files and does not download any.** You need your own copy of the game.

## What it does

| Page | |
|---|---|
| **Setup** (first run) | What this is, the game's folder, a check of the PC, the engine, a preset. |
| **Play** | Starts the engine. While it runs: frames per second, ms per frame, draws per frame (read from the engine's title bar and log), the engine's memory, and how much Windows commit is left. When a run ends it says why, if it can tell. |
| **Prepare** | Translates every shader ahead of time (`victory shaders`), then builds the pipelines earlier runs drew with (`victory warm --used`), with progress. Either can be stopped and carries on later. |
| **Engine** | Checks the project's GitHub releases, downloads the engine build and verifies its SHA-256 before unpacking. Or point it at a build of your own. |
| **Settings** | Preset (Auto, Safe, Low memory), console model, list-thread count, texture budget, ray tracing, the ambient stand-in, and the PC check with its repairs. The exact command and environment Play runs are shown, so nothing is hidden. |
| **Logs** | The live log with a filter, the folder of past logs, and a report file (paths shortened) for bug reports. |

### The checks and their repairs

Both repairs are offered, never applied on their own.

- **Paging file.** A run on a 32 GB PC with no paging file ended at about 24 GB used (`memory allocation of 1048576 bytes
  failed`); the engine used 40-46 GB on the PC it was tuned on. The fix sets one fixed 64 GB paging file on the drive with
  the most room, keeping 15 GB free. Windows asks for administrator rights for that one step, and a restart is needed.
- **GPU choice.** On a laptop with an integrated and a dedicated GPU, Windows decides per program which one the engine
  gets. The fix sets "high performance" for the engine's executable (per user, no administrator rights).

### What it assumes about the GPU

The shader cache is keyed by the host GPU's wave size and view limit. The launcher learns both from the engine's own log
on the first run (`host wave lanes min=.. max=..`, `host view heaps hold .. views`) and uses them for Prepare. Before the
first run it falls back to the numbers an NVIDIA card reported, and says so; for any other GPU it asks you to run the game
once first.

## Build

Needs the .NET 8 SDK. From the repository root:

```powershell
launcher\build.ps1                     # launcher\dist\xvoid-launcher.exe, self-contained (about 60 MB)
launcher\build.ps1 -FrameworkDependent # about 1 MB, needs the .NET 8 Desktop Runtime
dotnet test launcher\XvoidLauncher.sln # the logic tests; they run on any OS
```

The launcher never runs as administrator. Its data lives in `%LOCALAPPDATA%\xvoid-launcher` (settings, engine builds,
the shader and pipeline cache, logs, reports).

## Releasing

Pushing a tag (`v0.1.0`) runs `.github/workflows/release.yml`: it builds the engine (`diag` profile with diagnostics, the
build every measurement here is of) and the launcher, runs the tests, and publishes a pre-release with

- `victory-engine-win-x64.zip` and `victory-engine-win-x64.zip.sha256` (the launcher refuses an engine with no checksum),
- `xvoid-launcher.exe` and `xvoid-launcher.exe.sha256`.

## Layout

```
launcher/
  src/Xvoid.Launcher.Core/   settings, system checks, launch plans, log parser, engine download, process handling (no UI)
  src/Xvoid.Launcher/        the WPF app: theme, pages, view models
  tests/                     xUnit tests for the Core library
```

The Core library has no UI dependency, so what decides behaviour (the environment each preset builds, how the log is
read, how an archive is unpacked, what the checks say) is tested without a window.

## Status

The Core library is covered by tests, including log lines copied from a real run. **The windows have been compiled but not
yet looked at on a Windows desktop by the author of this change**, and the release workflow has not run: the first tag
will show whether it does. Expect rough edges in layout.
