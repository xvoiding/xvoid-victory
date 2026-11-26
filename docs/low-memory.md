# Running on 8 GB of video memory and 32 GB of RAM

Target: an NVIDIA RTX 4060 Laptop (8 GB), an Intel i7, 32 GB of RAM, Windows 11.

**Nothing on this page has been measured on that machine.** It is what the code
and the 7900 XTX fork's notes ([findings.md](findings.md)) say, turned into things
to check and to try. Where a line is a guess it says so. Replace the guesses with
numbers as they come in.

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

## Things to set, in order

1. **A big page file.** 48-64 GB, on the fastest drive, so that running over RAM
   is slow and not a crash. The title also needs several GiB of free Windows
   commit at startup ([README-fxceless.md](README-fxceless.md)). Close browsers and
   launchers first.
2. **The Series S console model**: `scripts\play.ps1 -Console SeriesS`, which runs
   `victory run <exe> --execute --console series-s`. The title is told it has
   7,776 MiB, not 10 GiB + 3.5 GiB (`crates/victory-gpu/src/console.rs`).
   The 7900 XTX fork measured this as "not worth it", but on a card and a PC with
   room to spare; here memory is the limit, so it may go the other way. *Guess,
   unmeasured. It may also change what the title draws.*
3. **A texture budget**: `-TextureBudgetMB 3072`. It limits the bytes of host
   copies the layer makes of textures the title fills, for shaders that pick
   textures by index (`VICTORY_TEXTURE_BUDGET_MB`, `resource.rs`). It does not
   limit every texture. 3072 is a starting point: try 2048 and 4096 and compare.
4. **No ray tracing**: leave `-RayTracing` out. It is about a third slower, and
   `VICTORY_MEMORY_CARD_MB` (default 4096, half of this card) is for the copies of
   title memory that programs read by address, which the ray-tracing notes
   ([fork-ray-tracing.md](fork-ray-tracing.md)) describe; it may need lowering
   if ray tracing is tried.
5. **`-LowMemory`** is items 2 and 3 together.
6. **The right GPU.** On a laptop with an Intel iGPU and an NVIDIA card, set the
   executable to *High performance* in Windows' graphics settings (Settings,
   System, Display, Graphics), or use the dGPU-only mode in the maker's tool. Check
   in the log which adapter was picked; this page has not checked how the code
   picks it.
7. **Plugged in, performance power mode.** The slowest part on the 7900 XTX fork's
   machine was one thread preparing submissions (48 ms of a 55 ms frame), so
   single-core speed under heat matters.
8. **Before the first run:** `victory shaders <archives>` and `victory warm --used`,
   so that pipelines are not made while playing. Run from a normal window, not as
   administrator.

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

## Not known

- Whether any of the AMD-specific fixes in the fork behave differently on NVIDIA.
  The 7900 XTX fork says it was tested on one machine only, and on NVIDIA nobody
  has reported.
- Whether 8 GB of video memory is enough at all for the Series X profile; the
  title's own memory use on the card has not been measured here.
- Whether the Series S model works with every build of the title.
