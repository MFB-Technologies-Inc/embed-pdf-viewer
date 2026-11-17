# perf-compare

Builds the web viewer from `main` and the two Rust research branches at pinned commits, drives the
same flows against each in Chromium, and reports load, interaction, CPU and memory differences with
bootstrap confidence intervals. It answers "is the viewer faster or slower on branch X than on
`main`", which none of `tooling/viewer-e2e`, `tooling/engine-baseline` or
`examples/rust-kernel-eval` can.

```sh
cd perf-compare
pnpm install --ignore-workspace
pnpm exec playwright-core install chromium
pnpm compare -- --rounds 10 --profile desktop
```

`pnpm compare` runs `scripts/doctor.mjs`, `scripts/materialize.mjs`, `scripts/build-entry.mjs`,
`run.mjs` and `report.mjs --latest` in that order. The report lands in
`results/<run-id>/report.md` and on stdout. `--quick` runs 3 rounds instead of 10 and stamps the
report "quick, not for publication". `--arms` and `--flows` take comma lists. `--profile slow`
applies a 4x CPU throttle.

This directory isn't a pnpm workspace member. Keep it out of `pnpm-workspace.yaml` and
`turbo.json`.

## What it compares

Six arms, defined in `arms.json`, which is the only file that knows which branches exist:

| Arm | Branch | Kernel | Engine requests |
| --- | --- | --- | --- |
| `main` | `main` | TypeScript | All TypeScript |
| `main-aa` | `main` | TypeScript | All TypeScript (a second copy, served separately) |
| `impl-ts` | `research/multi-platform-rust-core-impl` | TypeScript | TypeScript, Rust core linked but unused |
| `impl-rust` | same | TypeScript | `pages.geometry` in Rust (`rustRouting: { rustReads: true }`) |
| `plugins` | `research/multi-platform-rust-core-impl-plugins` | Rust (`packages/core-wasm`) | TypeScript |
| `plugins-lto` | same | Rust, kernel built with LTO | TypeScript |

The report compares each pair in `arms.json`. `main` vs `main-aa` comes first: it's the A/A
check, and its interval sets the noise band every other pair has to clear. `impl-ts` vs
`impl-rust` isolates the Rust geometry reader. `impl-ts` vs `plugins` isolates the Rust kernel,
with the engine held the same (both arms run the byte-identical engine wasm). `main` vs `impl-ts`
is the aggregate cost of the impl branch with its switch off, not the cost of the unused Rust code
alone.

`plugins` vs `plugins-lto` isolates link-time optimization of the wire kernel. The branch sets
`lto = true` in `[profile.release]`, but `crates/epdf-web/Cargo.toml` declares
`crate-type = ["cdylib", "rlib"]`, and with an `rlib` in the set Cargo passes no `-C lto`, so the
kernel `plugins` ships was never link-time optimized. `plugins-lto` is the same commit with the
kernel built by `cargo rustc --crate-type cdylib`, which does get `-C lto`. A quick run on
2026-09-29 put the gain at about 10% where the kernel is the bottleneck (`open(large) / openedMs`
1384 to 1273 ms, `scroll / longTaskMs` 9.6 to 8.3 s) and nothing detectable on `select`, `search`,
`annotate` or `jump`, for a kernel 0.9 MiB larger. The Rust engine core (`crates/epdf-wasm`,
`["staticlib", "rlib"]`) has the same problem, and no arm measures it yet.

Every arm runs one shared entry page (`entry/`), built with Vite from each worktree's own
`packages/viewer/main` as a production bundle and served statically with COOP/COEP. A gate
(`lib/gate.mjs`) opens every arm first and excludes any arm whose page count, search hits,
selected text, annotation count or rendered page-1 bitmap differs from `main`'s. The report prints
an excluded arm as `excluded: <reason>` rather than dropping it.

Two fixtures exist for the annotation and form flows, both written by `fixtures/generate.mjs`:
`annots.pdf` is `large.pdf` with 1,000 highlights, 150 ink drawings and 50 stamps, and `forms.pdf`
has 1,000 text and checkbox fields over 100 pages. `hover`, `dragAnnot`, `drawInk`, `typeField` and
`undo` run on them, and the gate compares each one's answer. `dragAnnot` drags a stamp: neither
viewer API generation moves a highlight. A flow can declare what it `needs` (`lib/flows.mjs`), and
an arm whose viewer lacks it (`capabilities` in `entry/api-v*.ts`) skips that flow. `undo` needs a
history plugin, which `main` 2516e2786 doesn't have, so `main` and `main-aa` skip it. The gate
runs every flow once on every arm, whatever `--flows` says, so it always has an `undo` answer from
the arms with history. It compares that answer with `arms.json`'s `fallbackReference` (`main-now`)
when `main-now` is in the run, as it is in the default arms. Otherwise it uses the first arm in
run order that gave one, and prints a `gate:` warning naming that arm. When only one arm answers,
nothing checks it: the gate marks `undo` unchecked for that arm (`unchecked` and `references` in
its `gate.json` entry), prints a `gate:` warning that no comparison happened, and the report
leaves that arm's `undo` rows out. A run of `main`, `main-aa`, `plugins-base` and `plugins-next`
is that case, since `plugins-base` (`9d6c2eca1`) builds as viewer API v1 and skips `undo` like
`main`. Add `main-now` to such a run to get `undo` checked and timed. The report judges `undo`'s
rows against the 3% floor alone, because there's no A/A pair for them.

## The standard run

A run with no `--arms`, and `scripts/build-entry.mjs` with no arms, use `defaultArms` in
`arms.json`: `main`, `main-aa`, `main-now` and `perf-fixes-end`. `main` against `main-aa` is the
A/A check. `main-now` (upstream `main` at `ed064e0c8`) against `perf-fixes-end` (the
`research/perf-fixes` tip at `a6d4d5d14`) is where the port stands, the pair `FINDINGS.md` quotes
from `2026-11-17T16-59-17-202Z`, the last run with all four. `main-now` also gives the gate its
`undo` reference. The other arms stay in the file as the record of what each arm name in
`FINDINGS.md` and the results meant; their checkouts aren't kept, and naming one rebuilds it
(`materialize.mjs`, then `build-entry.mjs`).

From 2026-10-14 to 2026-11-17 the defaults were `main`, `main-aa`, `plugins-base` and
`plugins-next`, meant as the plugins branch before and after a change under test. Neither means
that now. `plugins-base` (`9d6c2eca1`) can't be built in the arm64 Linux devcontainer: its fork
commit has no published `wasm32-eh` archive, and the fork build needs an amd64 host
(`FINDINGS.md`, "The tip after the catch-up, against upstream main"). `plugins-next` is
`0de4effd9`, the base of `research/perf-fixes`.

To test a change, add an arm for the commit before it and one for the commit with it, as
`phase-c-base` and `phase-c-end` were, add their pair to `pairs`, and name them with the defaults
in `--arms`. That pair is the answer, and `main-now` against either shows where the branch stands.
Each arm costs run time: `2026-11-17T16-59-17-202Z` took 150.2 min for seven arms at R=10 on the
Mac mini, about 21 min an arm.

## How long it takes

Measured on an Apple M4 Max (16 cores, 64 GiB), on AC power, on 2026-09-29.

The first build of all three worktrees is dominated by the two Rust-core payloads. `main` built in
68 s. Copying the fork's dependencies from the host checkout took about 40 s, once. After that,
`impl` built in 78 s including a clean `wasm32-eh` fork build, and `plugins` in 103 s reusing it.
A second run finds everything cached and `scripts/build-arm.mjs` returns in 0.7 s. The entry is
rebuilt on every run, about 5 s per arm.

A `--quick` run of all five arms took 17.0 min. A full `desktop` run took 57.3 min and a
full `slow` run took 86.8 min. `plugins` dominates both: its `scroll` flow alone runs about
30 s per sample, against about 4.5 s on `main`. Those timings are for five arms. `plugins-lto`
came later and adds a second `plugins`-sized arm; a quick run of `main`, `main-aa`, `plugins` and
`plugins-lto` took 17.9 min. We haven't timed a full six-arm run.

`.work/` holds about 18 GB by `du`: 9 GB of worktrees, 7 GB of fork checkout, 1.6 GB of emsdk and
0.7 GB of depot_tools. Most of the fork checkout is APFS clones of the host's
`packages/engine/runtime/runtime-src` dependencies, so it costs far less real disk than `du` says.

## Timing on another machine

A run on the machine you're working on picks up whatever else it's doing. To time on an idle one
instead, set `PERF_REMOTE`:

```sh
PERF_REMOTE=andrew@2023mbp16grey.local pnpm compare -- --rounds 10 --profile desktop
# or, with the entries already built:
PERF_REMOTE=andrew@2023mbp16grey.local node scripts/remote.mjs --quick --arms main,main-aa,plugins
```

Builds stay local. `scripts/remote.mjs` copies the harness, its `node_modules`, the fixtures, the
stamps and each timed arm's built entry to `~/perf-remote/perf-compare/` on the remote, runs
`run.mjs` there under `caffeinate` with this machine's Node version (through `mise`), and copies
`results/<run-id>/` back. It never touches a checkout on the remote, so an arm built from a commit
that only exists here works too. The remote needs SSH key access, `mise` and `rsync`; Chromium is
installed there on first use.

The run is detached from the SSH session, so a dropped connection or a stopped `remote.mjs` doesn't
end it. Its output goes to `.work/remote-run.log` on the remote, which `remote.mjs` follows every
15 s, and its exit status to `.work/remote-run.exit`. If `remote.mjs` stops early, the run finishes
anyway, and its `results/<run-id>/` can be copied back with `rsync`.

One run per host. `PERF_PORT_OFFSET` lets two runs share a machine without colliding, and
`scripts/concurrency-check.mjs` tests whether their numbers survive it. On the Mac mini M1
(`macmini2020`) they don't: against a solo run, two concurrent runs of `main` and the plugins kernel
held the plugins/`main` ratio on 47 of 62 rows (76%) and agreed with each other on 48 (77%), where
the check needs 95%. Absolute medians barely moved (0.99x), but the CPU rows' ratios rose, `select`
JavaScript time from 1.58x to 2.22x, so a shared host overstates the plugins kernel's CPU gap. The
runs are `results/2026-10-02T02-02-24-660Z-macmini2020-desktop` (solo) and
`results/2026-10-02T02-18-19-070Z-macmini2020-desktop-p0` and `-p100`.

`meta.json` records the remote's host, CPU and power source, and the local harness commit.
The report compares arms within one run, never a remote run with a local one (see Moving a pin).

## Moving a pin

`scripts/pin.mjs` sets a worktree's `sha` in `arms.json` to the current tip of its `ref`. It takes
worktree ids, not arm ids, and with no argument it moves all 56 worktrees in the file. We don't move
the pins that are there: each records what an arm name meant when it was timed, so moving one
changes what that name means in `FINDINGS.md` and the results, and it moves every arm on that
worktree too
(`phase-c-base` builds from the `plugins-next` worktree). Use it to pin a new worktree instead: add
the entry to `worktrees` with its `ref`, its `recipe` and `"sha": ""`, then

```sh
node scripts/pin.mjs <new-worktree>
git commit perf-compare/arms.json
```

A pinned commit has to exist on one of the checkout's remotes. `materialize.mjs` tries each remote
in turn, so a branch pushed only to a private fork works, as long as every machine that runs the
harness has that remote. Push before pinning.

To see whether a change on a branch helped, do what "The standard run" describes: add an arm for
the commit before the change and one for the commit with it, add their pair to `pairs`, and name
both with the defaults in `--arms`. Then time them in one run and read that pair. It costs about
21 min of Mac mini time per added arm at R=10.

`report.mjs --against` used to compare two runs. It now exits 2 with
`cross-run comparison removed`, because the same build's medians spread by a median of 6.3%, and
up to 17%, from one run to the next. That's wider than most of the changes it was meant to find,
and no A/A band covers it. The cost is run time: every comparison times both builds, not just the
new one.

## Adding an arm

Add it to `arms` in `arms.json`, pointing at an existing worktree or a new one, and add the pairs
you want reported. A new worktree needs a `recipe`: `typescript` fetches the published engine
payload, `rust-core` builds the fork and links the Rust engine core, `rust-kernel` does the
same plus the wire kernel, and `rust-kernel-lto` builds that kernel as a `cdylib` only, so LTO
applies. A branch that needs a different build means a new recipe in
`scripts/build-arm.mjs`, and that file's contents are the worktree cache key, so editing it
rebuilds every worktree.

The upstream catch-up renamed five of the viewer calls the entry makes. `goToPageIndex` and
`getPageFrame` became `goToPage` and `getPageBox`, `viewer.on('opened')` became
`documents.onOpened`, `viewer.execute` became `commands.execute`, and the annotation filter's
`page` became `pages`. The two commands the flows run were renamed too: `pointer:toggle` is
`tool:pointer` and `annotation:add-highlight` is `tool:highlight`. `entry/api-v1.ts` and
`entry/api-v2.ts` make those calls the old way and the new way. `scripts/build-entry.mjs` reads
the worktree's stage contract with `lib/api-generation.mjs`, builds the arm with the matching
one, and prints `<arm>: viewer API v1` or `v2`. `main` at `2516e2786` and the plugins branch up
to `9d6c2eca1` are v1; upstream `main` from `ed064e0c8` and the plugins branch from `2aaa8f841`
are v2.

## Reading a run

`node scripts/summary.mjs results/<run-id> [arm ...]` prints the numbers worth quoting from a run's
`report.md`: each pair's verdict counts, the median ratio over three groups of rows, the median
difference over its settle rows, and, for pairs not against `main`, the rows that aren't "no
detectable difference". The three ratio groups are timing rows (open, search and wall times),
frame-interval rows (`frameP50Ms`, `frameP95Ms`) and main-thread rows, each with its row count.
They're apart because frame intervals sit near the run's frame interval on every arm (16.7 ms or
11.0 to 11.3 ms on the Mac mini, below), so their ratios stay near 1.0: mixed in, they
pulled one pair's median to 1.000x while the same table had `dragAnnot` settle 394.7 ms slower.
Settle rows are differences in ms with no ratio, so they get their own line. Before calling a row a regression, check the same unchanged build in earlier runs: on
the remote, `annotateLarge` settle for one build moved between 220 and 245 ms from run to run, and
`main` against `main-aa` differed by about 20 ms on it within a run. `--drift
results/<earlier-run> main` does that check: it prints `main`'s median on every row in both runs,
and each moved row of this run's pairs whose change is no larger than `main`'s own move on that
row. It exits 2 instead when the runs don't compare, which `viewer-perf-compare-plan.md` (Task 6,
Step 6) defines: a probe hash that differs or wasn't recorded, a different browser, CPU, profile,
viewport or scale factor, or a fixture both runs have with different bytes. A different commit or
engine wasm for the drift arm is refused too, since then it isn't the same build, and so is one
that either run didn't record. So any run before `probeSha256` was recorded, and any run before a
probe change, can't serve as the earlier run.

`--drift` also refuses two runs whose frame intervals are more than 1 ms apart, or a run whose
interval can't be determined. A run's frame interval is the median `scroll / frameP50Ms` of `main`
in its `samples.jsonl`; `summary.mjs` prints it first and the report's Environment table has it.
It matters because pointer input is frame-paced: the flows that drive the pointer (`hover`,
`dragAnnot`, `drawInk`, and the drags of `select` and `annotate`) take whole frames per move, so
their settle rows scale with the interval, and an arm whose per-move work fits
one frame at one interval can need two at a shorter one. The Mac mini has run at 16.7 ms
(`2026-11-16T20-20-49-728Z`, `2026-11-17T02-25-30-951Z`, `2026-11-17T04-18-50-584Z`) and at
11.0 to 11.3 ms (`2026-10-13T02-28-33-711Z`, `2026-11-17T16-59-17-202Z`), and we don't know why.
The interval comes from the scroll flow rather than an idle measurement so that recorded runs have
it too. On `main` the scroll's median frame has matched the display's interval in every recorded
run, but a run without `main`'s `scroll` samples has none, and `--drift` refuses it.

A verdict carries a note when it rests on less than usual. `slower (no A/A band)` is a row
judged against the 3% floor alone, because `main` and `main-aa` skip its flow (`undo`). `(no done
condition on <arm>)` is a `hover` row where that arm can't report the hovered annotation
(`lib/done.mjs`; viewer API v1 can't), so its row ends at the last pointer move and times input
pacing, not hover handling. On a v2 arm the `hover` CPU rows include the done-condition poll
of `getHovered()`, which on a Rust arm is a kernel read.

A sample whose warm-up failed is kept. Its line in `samples.jsonl` carries the failure's first line
as `warmupFailed`, and the report lists those samples by arm and flow under the gate, beside the
samples that failed and were left out. Its measured window followed an incomplete warm-up, so it
can read slower than its arm's other rounds.

Each arm's cell in the report's tables is `median [max]`: the median over rounds, then the slowest
round. Each pair table's `threshold` column is the smallest change that row's verdict counts: its
A/A noise band or 3% of the reference, whichever is wider. A ratio row shows it as a percentage, a
difference row in the metric's unit. It's blank when the row's A/A check failed. A row that moved
less than its threshold reads "no detectable difference", even when its interval leaves out the
null value.

An arm whose payload has the Rust engine core but whose recipe isn't `rust-core` can still run
with `rustReads: true`: `node scripts/verify-core.mjs <worktree>` checks the payload and records
it in the stamp, which the gate reads.

## Profiling an arm

The scripts in `scripts/prof/` profile the main thread inside the harness's own measured window,
on this machine (not the remote):

1. `node scripts/prof/build-prof.mjs <arm>` builds `.work/dist/<arm>-prof`: the arm with an
   unminified bundle and a kernel that keeps its function names (`wasm-opt -O3 -g`). It stops if
   the stripped rebuild isn't byte-identical to the arm's shipped kernel. Rust-kernel arms only.
2. `node scripts/prof/flowprof.mjs <arm>-prof <flow> [reps]` writes one `.cpuprofile` per rep to
   `.work/prof/out` (or `$PROF_OUT`), and prints each rep's metrics. Any arm works, without names.
3. `node scripts/prof/profsum.mjs <flow>-<arm>-prof [top]` sums self and inclusive time per
   function and per category (wasm, JS file, GC, idle), averaged over the reps.
   `scripts/prof/proftree.mjs <prefix> [minMs] [maxDepth]` prints the merged call tree, and
   `scripts/prof/callers.mjs <prefix> <regex> [depth]` totals what calls the frames that match.

Profile a few reps of the baseline the same way and compare categories before reading the
function lists: a gap shows up first as wasm time with no `main` counterpart.

## How the Rust-core payload is built

No fork release publishes the `wasm32-eh` target the Rust engine core links against. The branch's
own `engine-runtime-build.json` marks it `"status": "pending"`. So the `rust-core`,
`rust-kernel` and `rust-kernel-lto` recipes build it: they check the fork out at the worktree's recorded
`runtime-src` commit under `.work/fork/`, copy its gclient dependencies from the host checkout
when the host synced the same `DEPS` (otherwise they run `gclient sync`; the host's synced copy is 7.4 GB), install
emsdk 4.0.0 into the fork's own `third_party/emsdk`, and run the fork's `build-target.sh
wasm32-eh`. The Rust core is then linked with the steps `tooling/engine-conformance/README.md`
documents.

emsdk 4.0.0 is hard-coded in `scripts/build-arm.mjs`, unlike every other toolchain version here,
because no branch declares it anywhere a script can read. `compile.esm.sh` passes
`-sWASM_LEGACY_EXCEPTIONS=0`, which first exists in 4.0.0, and
`tooling/engine-baseline/results/geometry-service-boundary.json` records emcc 4.0.0.

The research pins were moved to make this build work at all. Both earlier pins had a Rust core
that called the fork's old integer `/Name` API, while their own fork submodule and TypeScript
bindings used the new text API, so the `pdfium` crate compiled against no fork commit. The fix is
`c8085bd3c` on the impl branch, merged into the plugins branch as `bef223d9d`.

## Caveats

The probe now waits for each visible image's current-source decode promise and tracks clipped
viewport membership, movement, and removal. Offscreen changes do not extend the quiet window.
The returned settle timestamp is the latest observed visible change or decode completion,
bounded below by the action's `doneAt`, before a 250 ms quiet window. This is a visual heuristic,
not proof that no future render can run. Sampling visibility performs geometry and style reads;
that instrumentation cost is included in the reported CPU and frame metrics. Since `7d9648d6d` an
image outside the browser viewport costs one `getBoundingClientRect` and no style reads, and one
sample reads each shared ancestor's style once.
`meta.json` records `probeSha256`, so a run's probe version is on record. `RESULTS.md` publishes
`2026-11-17T16-59-17-202Z`, recorded with probe `d3edaa255947`, the version at `7d9648d6d`.

> Note: The `slow` profile throttles the main thread only. Chromium 140 answers
> `Emulation.setCPUThrottlingRate` on a worker target with "Operation is only supported for pages,
> not workers", and page throttling doesn't reach the worker: at 4x a busy loop took 4.4x longer on
> the main thread and no longer in the worker. `meta.json` records `workerThrottled: false` and the
> report says so. For the same reason `workerTaskMs` is n/a: workers have no `Performance` domain.

"First paint" is a frame opportunity after a visible page image has decoded, not a compositor
timestamp, the same rule as `tooling/viewer-e2e/performance.ts`. `jankFrames` counts
`requestAnimationFrame` intervals over 50 ms in headless Chromium, which runs frames at about
120 Hz here (`frameP50Ms` is 8.3 on `main`).

Memory comes from `performance.measureUserAgentSpecificMemory()` after a forced GC, which counts
wasm linear memory (`test/memory.selftest.mjs` checks a 64 MiB `WebAssembly.Memory` moves the
total). It needs the full Chromium build in new-headless mode; the headless shell rejects the call.
If a run ever falls back to `Runtime.getHeapUsage`, the report labels memory "JS heap only".

The gate compares page 1's rendered bitmap, read back from the viewer's full-page image, instead
of a screenshot. Two identical `main` builds differed in 0.01% of compositor-screenshot pixels.

`mainTaskMs` is Chromium's `TaskDuration` over the measured window: everything the page's main
thread did. `mainJsMs` is the JavaScript part of it, read from a trace of the same window as the
outermost `FunctionCall`, `RunMicrotasks`, `EvaluateScript` and `v8.evaluateModule` events. It
replaced `mainScriptMs` (`ScriptDuration`) on 2026-09-30, because `ScriptDuration` leaves microtasks
out, and a Preact render is one: moving work out of a render read as more script with the same
JavaScript run (FINDINGS.md, section 6). Runs from before then have `mainScriptMs` instead. The
trace is on during every measured window, so its overhead is in every timing.

`scroll / settleMs` is usually 0 on the TypeScript arms: every render lands during the wheel burst,
so the page is already settled when the last wheel event goes out.

`hover / settleMs` and `drawInk / settleMs` are `settled - t0`, where `t0` is before the first
pointer move, so they include the frame-paced input: 80 pointer moves for `hover` (four steps
between each of 20 highlights), and 180 moves plus the ink tool's 800 ms grouping window for
`drawInk`. Most of each row is pacing, which scales with the run's frame interval, and the 3%
floor is taken from that total: `main-now` against `phase-d-end` in `2026-11-17T04-18-50-584Z`
had thresholds of ±39.6 ms on `hover` and ±118.2 ms on `drawInk`. `dragAnnot`, `typeField`, `select` and `annotate` include their shorter input the same
way. `scroll` splits the two, `wallMs` from `t0` and `settleMs` from the last wheel; these flows
don't yet (`FINDINGS.md`, "What the harness should measure next").

A done condition's poll is a small read on every arm, found once before the timed window
(`entry/main.ts` `watch`), but not the same read: the count watch that `drawInk`, `undo`,
`annotate` and `annotateLarge` poll lists one page's annotations (`annotationCountOn` in
`entry/api-v*.ts`), which a Rust arm serializes across the wire whenever its store has changed,
and every read on a Rust arm first checks the kernel's state revision.

The `slow` profile approximates a mid-range laptop by CPU throttling. It is not a real device.

## Tests

`pnpm test` runs the Node tests, including controlled-clock probe regressions. Browser checks:

```sh
node --test test/probe.selftest.mjs test/memory.selftest.mjs
node --test test/flow-state.selftest.mjs test/fixtures.selftest.mjs
```

The synthetic probe and memory tests need Chromium. The flow-state test additionally needs a
built `main` arm (`node scripts/build-entry.mjs main`). It checks each interaction flow's
starting state after warm-up. The fixtures test needs the same build and checks that `main`'s
TypeScript engine lists 1,200 annotations in `annots.pdf` and 1,000 fields in `forms.pdf`.
