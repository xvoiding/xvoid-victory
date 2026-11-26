# xvoid-victory

A fork of [avatarmanz/VIctory-7900XTX](https://github.com/avatarmanz/VIctory-7900XTX),
itself a fork of [VIctory](https://github.com/Marvisak/VIctory): a compatibility
layer that runs an Xbox Series title natively on Windows from files the user
supplies. It is made by xvoid with [Claude Code](https://claude.com/claude-code).

The 7900 XTX fork was tuned for **AMD RDNA 3 with 24 GB of video memory and 64 GB
of RAM**. This one is about getting the same layer to start and stay stable on
**NVIDIA laptops with 8 GB of video memory and 32 GB of RAM** (the target is an
RTX 4060 Laptop with an Intel i7), where the memory the title needs is the
first wall. The aim is steady frame times first and a higher number second. It
is not there yet.

## Status of this fork

First measurements on the target (RTX 4060 Laptop 8 GB, Intel i7, 32 GB, Windows 11, 2026-11-25; one PC, one
person's screenshots and logs, Series X profile, the 7900 XTX fork's code with the changes in this tree):

- **About 4-6 fps indoors** (170-260 ms a frame, 3,700-4,600 draws a frame), with the card at about 98 % and the
  CPU at 100 %. Before the changes below it was 0.6 fps while ~850 pipelines were still being made.
- **A first run ended** at about 24 GB private memory with "memory allocation of 1048576 bytes failed": the PC had no
  paging file, so about 30 GB of commit. A 64 GB paging file (`scripts\set-pagefile.ps1`) ended that.
- **`VICTORY_AMBIENT=0.8` made the picture a white blob** on this card; it is off here unless `-Ambient` is given.
  Likely cause (not proven): its stand-in reads results of compute programs that this card refuses (next point).
- **Some compute pipelines are refused** (`The parameter is incorrect`, 0x80070057): the recompiler keeps a 64-lane
  wave size on shaders it cannot split into two 32-lane halves (an LDS read and write in one lane-branch region,
  lane masks used as data), and NVIDIA's waves are 32. About 340 draws a frame are dropped for it. This, not memory,
  is why lighting and effects are missing. `-LowMemory` (Series S) was reported to render black on this card on an
  earlier build of the layer; not retried in this tree.
- The picture is **blurry** indoors; whether that is textures still streaming or a pass that is not running is not
  known yet.

What this fork adds: the two-plane (scene and interface) blend so the HUD and menus show; `shaders` translating for
the host's wave size and view limit, so `scripts\prep.ps1` fills a cache the run can use; NVIDIA-safe defaults
(`play.ps1`: ambient off, 4 list threads); ways to ask for less memory (`-LowMemory`); a paging-file script; and an
ordered checklist for steady frames on this hardware ([docs/low-memory.md](docs/low-memory.md)). The speed work
underneath is the 7900 XTX fork's, and its numbers below are its numbers, not this fork's.

**This repository contains no game files, code or assets.** You need your own
copy of the game. It is not affiliated with or endorsed by the game's developer
or publisher.

## Where the 7900 XTX fork stood (2026-11-25, inherited, not measured here)

Tested on one machine only: Windows 11, Intel i9-12900K, 64 GB RAM, Radeon
RX 7900 XTX, 1440p, the Series X profile.

| | |
|---|---|
| Menus, videos, loading screens | 30 fps |
| Prologue, indoors | 50-75 ms a frame (13-20 fps) |
| First mission with the boat | 46-53 ms (19-22 fps) |
| Street chase with many cars and people | 100-165 ms (6-10 fps), single frames far longer |
| Outdoors on foot, the camp | 60-90 ms |
| Flying | 42-57 ms |
| Longest session so far | 41 minutes, no crash |
| Memory | 40-46 GB of private memory: **64 GB of RAM recommended** |

It started at 280 ms a frame in the same scenes two days earlier.

What is known to be wrong:

- Trees, plants and far buildings flicker outdoors in some places.
- Rooms with little direct light are too dark without `-RayTracing`.
- Textures of what just came into view appear late while the title streams.
- The first run with a new driver or a new build stutters while the driver makes
  pipelines (`victory warm` makes them ahead).
- Recording a frame in the world makes the title's own watchdog end it.

## How to run it

You need Windows 10/11 x64, a CPU with AVX2, a Direct3D 12 GPU with shader
model 6.6 and mesh shaders, Rust (the toolchain is pinned), the MSVC build
tools, and your own dump of the title.

```powershell
git clone https://github.com/xvoiding/xvoid-victory
cd xvoid-victory
copy victory.example.toml victory.toml      # then set your paths, outside this folder
cargo build --locked --profile diag -p victory --features diagnostics
scripts\play.ps1 -Exe <path of your copy's executable>
```

`scripts\play.ps1 -Safe` leaves out the newest, faster ways; try it first when
something is wrong. `-RayTracing` turns the title's ray-traced indirect light on.

On 8 GB of video memory and 32 GB of RAM, start with `-LowMemory` (the Series S
console model and a texture budget of 3072 MB; `-Console` and `-TextureBudgetMB`
set them one by one) and read [docs/low-memory.md](docs/low-memory.md) first.

Before the first run, ahead of time (both can be stopped and go on later; the script sets the host numbers the cache
is keyed by, which a bare `victory shaders` did not before this fork):

```powershell
scripts\set-pagefile.ps1 -SizeGB 64                  # once, as admin, then restart Windows
scripts\prep.ps1 -Exe <your exe>                     # every shader translated, minutes
scripts\play.ps1 -Exe <your exe>                     # one run, writes down the pipelines it makes; close it
scripts\prep.ps1 -Exe <your exe> -Warm               # those pipelines made ahead, for a new driver or build
```

Commands, debugging switches and upstream's own description:
[docs/README-fxceless.md](docs/README-fxceless.md). Every `VICTORY_...` switch is
described in a comment where it is read.

## What is different here

In short; the reasons and the measurements are in [docs/findings.md](docs/findings.md).

- **List calls are written down, not carried out, on the title's threads**
  (`VICTORY_THIN`): eight threads of ours translate them.
- **Submissions are prepared by a thread of ours and joined**
  (`VICTORY_ASYNC_SUBMIT`, `VICTORY_JOIN_PARTS`), with what can be spread over
  threads spread: refreshes, compares (AVX2), descriptor records, landings.
- **What shaders wrote comes back beside the write guards**, by the dwords that
  changed, on a thread of its own (`VICTORY_RELAY`, `VICTORY_LAND`).
- **Buffer fills that need bytes still on the card are done on the card**
  (`VICTORY_CARD_STEPS`) instead of waiting for it.
- **The title's writes are caught more cheaply**: one view opened at a fault,
  blocks that rest, places in the title's code that look before they write.
- **Textures go up on a copy queue of their own** and a slot shows one only after
  its upload ran, which also ended a device loss that came every 15 minutes or so.
- **Boats float**: textures that shaders write and the title's CPU reads go back
  to title memory (from fxceless and Geckomacabre, on by default here).
- **Frame record and replay** with per-pass times and shader variants compared
  turn by turn ([docs/replay.md](docs/replay.md)).

## Credits

This is a tree of other people's work with more on top. The history is kept, and
commits taken from elsewhere name the commit and its author.

- **[VIctory](https://github.com/Marvisak/VIctory)** by Marvisak and its
  contributors: the loader, the runtime services, the Direct3D 12 back end and
  the shader recompiler.
- **[avatarmanz/VIctory-7900XTX](https://github.com/avatarmanz/VIctory-7900XTX)**:
  the fork this one started from, and all of its speed work (the history is kept).
- **[fxceless/victory-fork](https://github.com/fxceless/victory-fork)**: the fork
  this one started from, and the people whose pull requests it carries.
- **[DexNom/VIctory](https://github.com/DexNom/VIctory)**: ray tracing fixes,
  cube gradients, sparse textures.
- **[Belobug/VIctory-Belobug-AMD](https://github.com/Belobug/VIctory-Belobug-AMD)**:
  the ambient light idea, pipeline workers, and work by romainhedouin.
- **[FQY55kai/ZJOU-VICTORY](https://github.com/FQY55kai/ZJOU-VICTORY)**.
- **[myokex/victory-amd](https://github.com/myokex/victory-amd)**: the guard over
  every view of a page (with Spexivus, joroyt and tightfall), and Joshua Odom's
  reads into title memory.
- **Geckomacabre** and **cxy0072019-sketch**: the texture read-back.
- **[XCL](https://github.com/ivall/xcl)** (GPL-2.0-only) was read for how the same
  title behaves under another layer; no code of it is here.
- This fork's changes were made with [Claude Code](https://claude.com/claude-code).

## License

GPL-3.0-or-later, as VIctory. See [LICENSE](LICENSE); the one file that is not
covered is named in [docs/asset-policy.md](docs/asset-policy.md).

ZETTROX
