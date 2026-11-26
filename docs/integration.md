# This tree as the base, and what to bring into it

Decided 2026-11-26. The reason is measured, not preferred: on this host
(Ryzen 9 9950X3D, RTX 3080 Ti 12 GB, 32 GB) this tree's build renders 9 645
draws a frame and drops 176 of them for want of a host pipeline, at 20.51 ms
per 1000 draws over 357 frames without a device loss; the same scene class in
ztrx drops 735 and costs 25.88 ms per 1000 draws. `docs/findings.md` says why
(below), and `C:\GTA\logs\cgbj-20261126-114737.err.log` is the run.

## Why this direction and not the other one

The history here is squashed: the "RTX 5090 fidelity and 32 GB RAM" fork came
in as one commit with one parent (`e05d463`), so nothing in this tree can be
cherry-picked out of it. Upstream `avatarmanz/VIctory-7900XTX` and our own
`ztrx` are granular - 140 and 9 commits - so *their* work can be taken one
commit at a time. Integration therefore only works inwards.

Remotes are set up for exactly that:

    upstream  https://github.com/avatarmanz/VIctory-7900XTX (fetched directly)
    mine      C:\GTA\ztrx
    origin    https://github.com/Pavl3000/cgbj-fork - this fork, public
              (the old origin, github.com/codec-d/cool_fox, is deleted: 404)

Upstream is a moving target: 140 commits ahead of our base at 08:34 UTC on
2026-11-26, 146 commits and tip 9576c45 by 10:20 UTC. **Pin a commit per wave**
- `git fetch upstream` then `git checkout -b wave1 upstream/main` - and write the
hash into the wave's commit message, or the tree will not be reproducible.

`git diff HEAD upstream/main` is about 94 files and 10 700 lines, of which
roughly 800 are this tree's own work that upstream left out on purpose ("the
AMD-safe parts"). Never merge upstream wholesale - bring commits.

## What is here and nowhere else

* `crates/victory-gpu/src/fence.rs` - WaitOnAddress fence wakes (upstream has
  32 lines fewer here).
* `draw.rs`, `texture.rs`, `texture/ahead.rs`, `pipeline/make.rs`,
  `victory-shader/src/resource.rs` - the NVIDIA-side rendering work, about 800
  lines that upstream does not have.
* `compose.rs` and the RetailPlane 72-byte stride fix (Series S two-plane).
* `ACTIVATE_RAYS` / `RT_RESOLVE`, which upstream did not take.

## Waves, in this order

Status, so a later session does not guess:

* **Wave 1 done on branch `wave1/upstream`** (not merged into `main`): `bcd7462`
  memory category log (Adika713), `53e4e8b` `VICTORY_CACHE_CORES` (Jake),
  `319fcf0` adapter selection (gomeaw). Two conflicts, both resolved: `cores.rs`
  taken from upstream whole - this tree had never touched it, it was still the
  base's 98-line version; `queue.rs`'s diagnostics block added at the end of
  `frame_ends`. Release and diag profiles build; `cargo test --release -p
  victory-gpu --lib cores` passes 3 of 3, `cpu_sets()` non-empty on this 9950X3D.
* **Wave 2 done on branch `wave2/upstream`** (stacked on wave 1): `6e965d0`
  residency from upstream with **this tree's default kept** - on unless
  `VICTORY_RESIDENCY=0`, because codec-d ran those budgets unconditionally and
  they are part of why it holds 357 frames on 32 GB - which brings
  `VICTORY_RESIDENCY_LIMITED`; `bcb2ef5` `VICTORY_POOL_SMALL=0` (64 KiB
  placement, the way out if a device loss ever names a 4 KiB-placed texture).
  Public surface of `residency.rs` is identical (17 functions), so no caller
  changed. Release and diag build; `cargo test --release -p victory-gpu --lib
  pool` 8 of 8, `--lib resid` 3 of 3.
* **Not taken from wave 2:** `VICTORY_WINDOW_BUDGET_MB`. It needs upstream's RAM
  band model (`RamBand`, `ram_sample`, `ram_band`, `ram_idle_ms` in `host.rs`),
  which this tree does not have - `shadow.rs` here has its own `let_go_idle`.
  That is a port of its own, and the knob is opt-in, so it waits.
* `VICTORY_TIMER_LOG` is **not** wave 1: it arrived with `49cb83b`
  (`FRAME_INTERVAL_MS`) and needs `victory-loader/src/frame_rate.rs`, which this
  tree does not have. It belongs to wave 3.

Each wave: cherry-pick, `cargo build --release --locked -p victory`, then
`C:\GTA\ab.ps1` on one fixed scene, then a tag if it wins. Everything is off by
default where upstream made it opt-in, so a wave that changes no number is the
expected result for the first one.

1. **Cheap and independent.** `VICTORY_CACHE_CORES` (upstream PR #147: threads
   prefer the big-cache cores on dual-CCD X3D - this host is one), the memory
   category log (PR #69), `TIMER_LOG`, `VICTORY_D3D12_ADAPTER` (PR #7).
2. **For 32 GB of RAM.** `VICTORY_RESIDENCY`, `RESIDENCY_LIMITED`,
   `WINDOW_BUDGET_MB`, `POOL_SMALL`, and PR #77's system-RAM budget for idle
   windows. Conflicts land in `pool.rs` and `residency.rs`.
3. **The frame rate switch.** `VICTORY_FRAME_INTERVAL_MS` + `VICTORY_TITLE_FPS`
   (the title's own 30 fps setting). Check game clocks after it.
4. **Renderer patches 1 and 2.** `VICTORY_PORT_CONSTANTS`,
   `VICTORY_PORT_DESCRIPTORS`: the measured 57-75 ms to about 53 ms upstream,
   and the end of the ~600 000-slot descriptor scan this tree still pays for.
   They patch the title's own bytes (byte-checked), so they break on another
   dump. Highest value, largest conflict (`descriptor/host.rs`, `draw.rs`).
5. **32-lane fixes, individually.** Upstream PRs #142, #82, #66, #4 - BVH
   prefix scans, tiled cascade shadows, wave64 builder thread counts, LDS flag
   regions. Small, and each names the program it fixes.
6. **From `ztrx`.** `362d0d3` (`translate --warm` takes `--pipelines`), then
   whatever survives of the in-place and prefetch work once the baseline here
   is the reference - it is the right place to measure it.

## Rules

* One upstream commit per local commit, and the message names its source and
  author ("upstream #66: ..."), which is also what GPL-3.0 asks for.
* `LICENSE` and the credits in `README.md` stay; `asset-policy.md` still binds:
  no game data in the tree, and `bcpack-tables.bin` is not the licence's.
* The bundle `C:\GTA\cgbj-f4b48ad.bundle` (3.1 MB) is the only copy of this
  history besides `.git`. Re-make it after every wave.
* `run.bat` in the parent folder still starts ztrx; `run-cgbj.bat` starts this
  tree. Do not point one at the other's build.
