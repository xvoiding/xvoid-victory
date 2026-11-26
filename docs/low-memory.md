# Steady frames on an RTX 4060 Laptop (8 GB), Intel i7, 32 GB of RAM

Target: an NVIDIA RTX 4060 Laptop, an Intel i7, 32 GB of RAM, Windows 11.

**Nothing on this page has been measured on that machine.** It is what the code
and the 7900 XTX fork's notes ([findings.md](findings.md)) say, turned into things
to check and to try. Where a line is a guess it says so. The aim is steady frame
times first (no paging, no pipeline stalls, the right GPU), a higher number
second. For scale: the 7900 XTX fork's best frames in the world were 13-22 fps on a
card with three times the video memory and a PC with twice the RAM.

## What the fork was built for, and what is missing here

| | 7900 XTX fork's machine | Target |
|---|---|---|
| Video memory | 24 GB | 8 GB |
| RAM | 64 GB | 32 GB |
| Private memory the title used | 40-46 GB | the same, unless asked for less |
| Best frames seen in the world | 13-22 fps, street chases 6-10 fps | unknown |

So the first thing to expect is not a slow frame but **paging**: more memory in use
than there is RAM, so Windows moves it to disk and every frame waits on the disk.
The second is the card's budget: past it, Windows moves the card's memory into the
PC's (`crates/victory-gpu/src/resource.rs`, "Past the budget the system moves the
card's memory into the PC's, which ended runs 20 and 24").

## The order to do it in

1. **A big page file.** 48-64 GB, on the fastest drive, so that running over RAM
   is slow and not a crash. The title also needs several GiB of free Windows
   commit at startup ([README-fxceless.md](README-fxceless.md)). Close browsers and
   launchers first.
2. **The right GPU.** The layer asks Windows for the high-performance card and
   makes its device there (`high_performance_device`,
   `crates/victory-gpu/src/host.rs`), so on a laptop with an Intel iGPU and an
   NVIDIA card it should be the NVIDIA one. The log's line `host D3D12 device
   ready` names the card. Check in Task Manager's GPU tab, during play, that the
   RTX 4060 is the one with load. To use Windows' default adapter instead, which
   follows the choice made per executable (Settings, System, Display, Graphics):
   `scripts\play.ps1 ... -More @{VICTORY_GPU='default'}`.
3. **Plugged in, performance power mode**, and in the NVIDIA Control Panel (Manage
   3D settings, Program settings, `victory.exe`) *Power management mode: Prefer
   maximum performance*. The slowest part on the 7900 XTX fork's machine was one
   thread preparing submissions (48 ms of a 55 ms frame), so single-core speed
   under heat matters. *Standard NVIDIA settings; their effect here is a guess.*
4. **A driver shader cache that does not forget.** NVIDIA Control Panel, Manage 3D
   settings, Global settings, *Shader Cache Size: Unlimited*. The first run with a
   new driver or a new build stutters while pipelines are made
   ([README.md](../README.md)); a cache that evicts them brings that back. *Guess:
   the cache's size has not been measured against this layer.*
5. **Make pipelines before playing**, not during: `victory shaders <the shader
   archives>`, then `victory warm --used` (both can be stopped and go on later).
   In the 7900 XTX fork's sessions 270-660 s were full of frames over 200 ms,
   almost all while new pipelines were made ([findings.md](findings.md), "What is
   next", 3). Run from a normal window, not as administrator, so the driver keeps
   its cache where it did.
6. **Put the cache on the fast drive**: `cache_dir` in `victory.toml`
   (`victory.example.toml`), outside the repository.
7. **The Series S console model**: `scripts\play.ps1 -Console SeriesS`, which runs
   `victory run <exe> --execute --console series-s`. The title is told it has
   7,776 MiB, not 10 GiB + 3.5 GiB (`crates/victory-gpu/src/console.rs`).
   The 7900 XTX fork measured this as "not worth it", but on a card and a PC with
   room to spare; here memory is the limit, so it may go the other way. *Guess,
   unmeasured. It may also change what the title draws, and may not work with
   every build of the title.*
8. **A texture budget**: `-TextureBudgetMB 3072`. It limits the bytes of host
   copies the layer makes of textures the title fills, for shaders that pick
   textures by index (`VICTORY_TEXTURE_BUDGET_MB`, `resource.rs`). It does not
   limit every texture. 3072 is a starting point: try 2048 and 4096 and compare.
9. **`-LowMemory`** is items 7 and 8 together.
10. **No ray tracing**: leave `-RayTracing` out. It is about a third slower, and
    `VICTORY_MEMORY_CARD_MB` (default 4096, half of this card) is for the copies
    of title memory that programs read by address, which the ray-tracing notes
    ([fork-ray-tracing.md](fork-ray-tracing.md)) describe; it may need lowering
    if ray tracing is tried.

## Threads (a laptop i7 has fewer than a 12900K)

Most counts follow the processor: pipeline workers are two thirds of the logical
processors, 2 to 16, and run below the title's own threads; `victory warm` starts
as many processes; the landing threads are a quarter of them. Two are fixed:

- `VICTORY_THIN_THREADS`: the threads that translate list calls, **8 unless set**
  (`crates/victory-gpu/src/list/thin.rs`). On a 12-16 thread laptop part these
  compete with the title's own threads. Try 4 and 6 against the default, one at a
  time: `scripts\play.ps1 ... -More @{VICTORY_THIN_THREADS='6'}`. *Guess: fewer may
  help, or may not; it was only ever run at 8.*
- `VICTORY_JOIN_PARTS=8`, set by `play.ps1`: how many parts a joined submission is
  prepared in. Leave it unless the first is tuned.

## Hitches: a choice, not set for you

When a draw's pipeline is not made yet, a frame may wait for it, up to
`VICTORY_PIPELINE_WAIT_MS` per frame (500 by default). `VICTORY_PIPELINE_WAIT_MS=24`
waits far less and **omits the draws** until their pipelines are ready: smoother
frame times, holes in the picture. `play.ps1` leaves it at the default; use
`-More @{VICTORY_PIPELINE_WAIT_MS='24'}` after the pipelines are made ahead (item
5), when it should matter less.

`VICTORY_D3D12_BACKGROUND=0` (the driver's own background shader work off) is an
experiment of the 7900 XTX fork, not a recommendation.

## What to measure and send back

- Task Manager during play: Memory (in use, committed), Disk activity, GPU
  *dedicated* and *shared* memory. Shared memory climbing means the card is over
  its budget.
- `VICTORY_MEMORY_MAP=1`: where the memory goes (on the 7900 XTX fork: lists'
  allocators 9 GB, windows 7, uploads 4).
- `VICTORY_PANEL=1`, then F3 in the title: where a frame goes.
- The log's frame lines, and the ms per frame in the same scene (the prologue
  indoors was 50-75 ms on the 7900 XTX fork).
- Which of the settings above were on. Change one at a time; scenes differ from
  run to run, so differences under about 5 % mean nothing.

## Results on the target (fill in; empty means not measured)

| Scene | ms a frame (median) | Longest frames | Settings | Date |
|---|---|---|---|---|
| Menus | | | | |
| Prologue, indoors | | | | |
| Outdoors on foot | | | | |

## Not known

- Whether any of the AMD-specific fixes in the fork behave differently on NVIDIA.
  The 7900 XTX fork says it was tested on one machine only, and on NVIDIA nobody
  has reported.
- Whether 8 GB of video memory is enough at all for the Series X profile; the
  title's own memory use on the card has not been measured here.
- Whether the Series S model works with every build of the title.
- Whether any frame rate near 30 is possible on this hardware with this code.
