# Performance findings, 2026-09-29

The Rust kernel on the plugins branch is slow because of its architecture, not its compiler flags.
Every store change re-serializes the whole kernel state to JSON, many times, and nearly every read
of the page registry deep-clones every page layout. Both scale with page count. Fixing three
things (a revision-cached `getState`, coalesced change notifications, and a shared page registry)
should remove most of the 12x gap to `main` on a 400-page document. Every build setting we found is
worth single-digit percent by comparison.

This comes from main-thread and worker CPU profiles, call counts, heap snapshots and JSON-size
probes taken in the harness browser, plus a read-only review of the Rust kernel, the wasm/TS wire,
the Rust engine core and the build settings. Nothing on the branches was changed. The benchmark
numbers it explains are in `RESULTS.md`.

Paths are relative to a checkout of the branch named in each section:
`research/multi-platform-rust-core-impl-plugins` at `bef223d9d` ("plugins") or
`research/multi-platform-rust-core-impl` at `c8085bd3c` ("impl"). Line numbers are at those commits.

## Caveats on the 2026-11-16 and 2026-11-17 runs (added 2026-11-17)

A second review of the harness at `5bb019688` found biases in the three runs the sections from
"The tip after the catch-up" on rest on: `results/2026-11-16T20-20-49-728Z`,
`2026-11-17T02-25-30-951Z` and `2026-11-17T04-18-50-584Z` (all `-macmini2020-desktop`). The
recorded numbers below stay as measured. Read them with these limits.

Every CPU row carries the probe's own cost. The probe in those runs (`lib/probe.js`, sha256
`29ce19545e8b`) sampled every page image on every animation frame, and every 16 ms while a settle
waited, with a `getComputedStyle` walk up each image's ancestors, offscreen images included. That
work runs on the main thread inside every `mainJsMs` and `mainTaskMs` window. `main`'s
`scroll / mainJsMs` was 391.5 ms on 2026-10-13 and 616.5 ms on 2026-11-17 (`04-18-50`), the same
build. How much of that is the probe, the macOS update (Darwin 27.2.0 to 27.3.0), the rebuilt
entry and the frame interval wasn't measured. The interval differed: `main`'s median
`scroll / frameP50Ms` was 11.0 ms on 2026-10-13 and 16.7 ms in `04-18-50`. The same build reads
468.2 ms in `2026-11-17T16-59-17-202Z`, at 11.3 ms and under the corrected probe. Both arms of
a pair pay the probe's cost, so it works as an addend and pulls every CPU
ratio toward 1.0: a 1.427x gap is wider than printed, and Phase C's 0.835x is a larger saving than
printed. The addend isn't exactly constant, since it grows with the window, so an arm that takes
longer also pays a little more probe. `7d9648d6d` skips the style reads for offscreen images and
reads each shared ancestor once per sample.

The done conditions of `typeField`, `dragAnnot` and `drawInk` polled heavy reads inside the CPU
window, every 16 ms. `typeField` ran `form.list().find()` over the 1,000 fields of `forms.pdf`.
`dragAnnot` listed the stamp page's annotations. `drawInk` read its page's count through
`documents.listPages()` and the page's list, and worked out its 180 stroke points, each one a
`pageBox` call that walks the viewer's shadow DOM, inside the window. On a Rust arm a list read can
cross the wasm wire and be serialized (`packages/core-wasm/src/proxy.ts` caches an answer only until
the next real call). So the "2.271x to 4.009x the main-thread JavaScript" in "Annotation and form
flows" includes harness work that may cost a Rust arm more than `main-now`, and nothing measured
how much. `undo`, at the 4.009x end of that range, and `annotate` and `annotateLarge` polled
their page's count by index the same way. `00ff9e715` finds the field, stamp or page once before
the window and polls only it, for `typeField`, `dragAnnot` and `drawInk`; `e9c7df9e1` does the
same for `undo`, `annotate` and `annotateLarge`, and works out the `annotate` and `select` drag
ends before the window.

The "median latency row" headlines leave settle rows out. `scripts/summary.mjs` before `703629a7c`
took the median over the ratio rows that aren't main-thread rows: `open`, `search`, `wallMs` and
the `frameP50Ms` and `frameP95Ms` rows. Frame intervals sit near the run's frame interval on every
arm, 16.7 ms in these three runs and 11.0 to 11.3 ms in `2026-11-17T16-59-17-202Z`, so their
ratios stay near 1.0.
Settle rows are differences in ms, with no ratio, so none of them counted. For `main-now` against
`phase-d-end` the headline is 1.000x (`RESULTS.md` quoted it until `96fe83aff` republished that
file from `2026-11-17T16-59-17-202Z`). Split by the current
script, its timing rows have a median of 1.415x (13 rows), its frame-interval rows 0.999x (6 rows),
and its settle rows a median of +10.4 ms (21 rows, -46.3 ms to +394.7 ms). The same table has
seven settle rows slower: `dragAnnot / settleMs` +394.7 ms, `undo / settleMs` +206.9 ms,
`typeField / settleMs` +88.2 ms, `zoom / settleMs` +37.0 ms, `zoom / zoom2SettleMs` +29.5 ms,
`zoomLarge / settleMs` +28.7 ms and `zoomLarge / zoom2SettleMs` +18.4 ms. The baseline run's
`main-now` against `plugins-next` (1.065x) splits into timing 1.414x, frame interval 1.010x and
settle +24.1 ms (-58.3 ms to +3,041.4 ms); the 2026-11-16 run's (1.239x) into 1.454x, 1.003x and
+20.5 ms (-47.9 ms to +49.8 ms).

"No detectable difference" on `hover` and `drawInk` settle is a coarse statement. Neither has an
A/A band wider than the 3% floor, and the floor is 3% of a median that is mostly paced input:
`hover` settles in about 1,320 ms (`main-now`, 1,320.8 ms) of 20 pointer moves of four steps each,
and `drawInk` in about 3,940 ms (3,940.9 ms) of 180 pointer moves and the ink tool's 800 ms
grouping. The thresholds are ±39.6 and ±118.2 ms in `main-now` against `phase-d-end`, and ±40.4
and ±124.4 ms in Phase D. So "no difference" there means no change of 40 ms on `hover` or 120 ms on
`drawInk`, which is most of what either flow's handling could cost.

`hover` had no done condition. It ended at its last pointer move on every arm, so its settle row
says when the page images stopped changing, not when the viewer handled the hover, and an arm that
dropped hover handling would have read fastest. `00ff9e715` waits on viewer API v2 until
`getHovered()` reports the last highlight. API v1 (`main` at `2516e2786`, `9d6c2eca1`) has no hover
read, so a `hover` row with a v1 arm now reads `(no done condition on <arm>)`.

`undo` has no A/A band. `main` and `main-aa` skip it, so every `undo` verdict (`+206.9 ms`, ±6.0, in
`main-now` against `phase-d-end`; Phase D's -286.3 ms) is judged against the 3% floor alone, and
nothing measured `undo`'s own run-to-run noise. The report now prints those verdicts as, for
example, `slower (no A/A band)`.

Seven settle rows in "The tip after the catch-up" were called unconfirmed by a drift check the plan
rejects. The check was `--drift` against `results/2026-10-13T02-28-33-711Z`, which recorded no
probe hash. `viewer-perf-compare-plan.md` (Task 6, Step 6) says to "reject comparisons with a
different or missing probe hash". So that check doesn't count: the four tip rows (`zoom /
settleMs` +47.0, `selectLarge / settleMs` +32.4, `annotate / settleMs` +10.6, `annotateLarge /
settleMs` +15.6) and upstream's three (`zoomLarge / backTo1SettleMs` +54.3, `annotate / settleMs`
-11.9, `annotateLarge / settleMs` -14.8) keep their in-run verdicts, with no earlier run of the
same build and probe to check them against. `summary.mjs --drift` now exits 2 on such a pair. The
two later drift checks (`main` from `20-20-49` to `02-25-30`, `main-now` from `02-25-30` to
`04-18-50`) compare the same build under the same probe and stand.

`plugins-base` (`9d6c2eca1`) has never been measured as that arm. None of these runs could build it
(below), and `9d6c2eca1` was last timed as `plugins-tip7` on 2026-10-13, under the uncorrected
probe, so the tip against October is still unmeasured.

## What the profiles show

All numbers are for the `plugins-lto` arm on `large.pdf` (400 pages), Chromium 140, Apple M4 Max.

A 60-wheel scroll spends 14.2 s on the main thread. 7.3 s of that is under JS `getState` calling
the wasm export `state_json`, which runs `epdf_kernel::store::Store::state_json` and serializes the
entire store. Precise coverage counted 920 `state_json` calls for 10 wheel events: 92 full-state
serializations per event. In those 10 events the page ran `JSON.parse` 66,692 times over 27.1 MiB
of text, 182 of those parses over 100 KiB (largest 167 KiB). On the 4-page `ebook.pdf` the largest
payload is 28 KiB, so the state grows with the document, at roughly 350 bytes per page.

Kernel self time is dominated by allocation and `Value` handling: dlmalloc `free`, `malloc` and
`unlink_chunk` (about 3.4 s), `IndexMap<String, serde_json::Value>` clone and insert (about 2 s),
SipHash (0.7 s) and `serde_json` serialization (about 1.7 s). The measurement plugin's
`refresh` then `publish` path costs 3.6 s during a scroll that never touches a measurement.

Opening the document takes 2.6 s of main thread, of which `state_json` is 859 ms and the
measurement plugin's `connect_viewports` then `ensure_loaded` path is 996 ms.

Heap snapshots after soak cycles 5 and 30 grow from 39.7 to 54.2 MiB. The growth is JS-side:
+12.9 MB of `ArrayBuffer` data across 250 buffers (about 10 per cycle, about 51 KB each), with
+600 `AbortController`/`AbortSignal` pairs and +600 `DOMRectReadOnly`. Strings grew only 236 KiB, so
this isn't a cache of JSON text. It looks like retained render requests, but the retainer isn't
traced yet.

In the engine worker, `handlePagesGeometry` costs 5.8 ms on `impl-ts` and 5.4 ms on `impl-rust`
across the whole `select` sequence, against 283 ms for `handlePagesRender`. Geometry is about 2% of
the worker's time there, which is why the Rust geometry reader shows no difference in the viewer.

## Plugins kernel: the fixes that matter

### 1. `getState` re-serializes everything on every read

`Wire.getState` (`packages/core-wasm/src/proxy.ts:418-454`) reuses its parsed state only while its
`version` hasn't moved, but `version` is bumped after every real `invoke` (`proxy.ts:588`), every
Rust-to-JS callback (`proxy.ts:363`) and every kernel notification. During a notify pass the hooks
alternate invokes and `getState` reads, so the cache never hits. Each `useT()`
(`packages/framework/react/src/i18n.ts:41`, about 35 call sites in the chrome) reads
`getState().plugins['i18n']`, and each miss rebuilds every slice from scratch
(`crates/epdf-kernel/src/store.rs:295-302`), including `DocumentMeta.pages`, all 400 page layouts
(`crates/epdf-kernel/src/types.rs:42`).

The fix is a Rust `state_revision()` bumped at mutation time, JSON cached per slice keyed on the
slice's `Rc`, and `useT` reading an i18n capability value instead of `getState`. This should remove
most of the 7.3 s in scroll and the 859 ms at open. The cost is small; per-slice objects must keep
reference identity while their revision is unchanged, which the existing slice cache already does.

### 2. Change notifications aren't coalesced

`SliceLease::commit` calls `emit_change` on every reducer result (`store.rs:135-152`), and one
listener is `listener.call0` into JS (`crates/epdf-web/src/exports.rs:559-569`), which notifies every
mounted hook synchronously, from inside the Rust call (`packages/core-wasm/src/kernel.ts:156-160`).
There are 200 to 300 such subscriptions, and Preact's `useSyncExternalStore` reads the snapshot again
during render and in a layout effect, because `get` is a new closure each render
(`packages/framework/react/src/runtime.tsx:59`, `:152`, `:181`). One scroll event makes several store
changes (camera, cursor, motion cause, camera rest), each a full pass.

The fix is to have the Rust listener set a dirty flag and call JS once at the end of the outermost
entry (`runtime::at_exit`, `crates/epdf-web/src/runtime.rs:77-86`), or from one microtask. It's about
ten lines. The proxy already tolerates a notification that lags the dispatch (`proxy.ts:41-51`), so
the risk is low. A stable `getSnapshot` in `runtime.tsx` cuts the Preact re-reads, but that adapter is
shared with `main`.

Done since, and capped at one notification a frame: see "Store notifications, at most once a
frame" below.

### 3. The page registry is deep-cloned on almost every read

`DocumentMeta.pages` is a bare `serde_json::Value`, and `PluginContext::document()`
(`crates/epdf-kernel/src/plugin.rs:278-286`), `ControllerContext::meta()` and `page_of()`
(`plugin.rs:717-758`), `Session::staged_meta()` (`crates/epdf-kernel/src/kernel.rs:158-160`) and
`Documents::meta_of`/`pages_of` (`kernel.rs:1660-1671`) all clone it. That's roughly 20,000
allocations per clone at 400 pages. Stage does five to ten of these per store change, sometimes to
read one integer (`crates/plugins/stage/src/sync/watchers.rs:65`), and every `build_scene` does two
even on a cache hit (`crates/plugins/stage/src/services/scene.rs:42`, `:65`, `:268`). The same
pattern is in measurement, annotation, selection, link, actions, redaction and page-edit. Render
already avoids it with `with_meta` (`crates/plugins/render/src/controller.rs:139-148`).

The fix is `Rc<DocumentMeta>` in `CoreState`, pages parsed once into `Rc<[PageLayout]>` with a
`pon -> index` map, and accessors that don't clone (`document_revision()`, `page_count()`,
`page_by_pon()`). It's about 25 mechanical call-site edits with no change in behavior, and it
removes most of the allocator, clone and SipHash time.

### 4. Measurement is O(pages²) at open and keeps publishing during scroll

`reconcile` (`crates/plugins/measurement/src/sync/viewports.rs:206-227`) runs `ensure_loaded` and
`refresh` for each of the 400 pages. Each refresh does a registry clone and a linear scan
(`services/store.rs:92-94`), two bridge calls into JS, and a `PageScale` dispatch whose reducer starts
with `let mut next = s.clone();` over the whole 400-entry map (`model.rs:71`). Each dispatch then runs
the full fan-out from items 1 to 3, including `reconcile` itself. The 400 async viewport answers
land over the next seconds and each publishes again (`viewports.rs:40-63`), which is the 3.6 s under
`publish` in the scroll profile.

The fix is items 1 to 3 plus a revision-keyed `pon -> PageMeta` cache in `Services`, `Rc` entries in
`MeasurementState.pages`, and optionally one batched `PageScales` action at open. The batching changes
action granularity, so the conformance vectors need checking.

### 5. Per-call waste on the wire

These are individually smaller but multiply with the notify fan-out:

- `useCapability` makes two crossings per read (`take_evicted` then `try_capability`) and hashes a
  KB-sized description string (`proxy.ts:407-411`, `:492-498`). Cache proxies in JS and invalidate on
  a documents-lifecycle revision.
- `usePages` returns all 400 layouts to read `.length` (`packages/framework/react/src/stage.tsx:386`,
  used three times in `viewer/chrome/src/ui/panels.tsx`). Read `pageCount` instead.
- `invoke` parses the answer before comparing it with the cached text (`proxy.ts:575-636`). Compare
  first, and skip the ref-marker regex when there are no markers.
- Every engine answer is JSON-encoded twice and parsed twice: the bridge resolves with JSON text
  (`packages/core-wasm/src/engine-bridge.ts:527`), and `from_js` stringifies that string again
  (`crates/epdf-web/src/runtime.rs:255-270`) before `engine_call` parses it (`engine.rs:137-139`).
- The PDF's bytes cross at open as base64 JSON, about six passes over 1.4 MB
  (`engine-bridge.ts:101-114`). Pass them as a `$host` handle; Rust never needs them.
- The toolbar re-renders on every store change and resolves every command, each resolution calling
  back into JS predicates (`packages/framework/react/src/toolbar.tsx:508-551`).
- Every export runs `after_entry` (`runtime.rs:160-200`) even for read-only calls.

### 6. Every hook re-runs on every change

Measured 2026-09-30 on `research/rust-kernel-wire` at `776f0c265` (the `plugins-getstate` arm),
with an unminified build and source maps; line numbers are at that commit. After items 1 to 5 the
60-wheel scroll is 1292 ms busy on the main thread. By source, 569 ms is Rust, 287 ms is the JS wire
(`packages/core-wasm/src/proxy.ts` and `engine-bridge.ts`), 127 ms is Preact and about 35 ms is the
app's own components. Rendering is not where the time goes. What rendering decides is how many wire
calls there are.

Every hook subscribes to the whole kernel change stream: `useKernelValue`, `useSelector` and
`useOptionalSelector` all pass `kernel.subscribe` to `useSyncExternalStore`
(`packages/framework/react/src/runtime.tsx:65`, `:158`, `:196`). Each change re-runs every selector
on the page, the wire's per-change answer cache is dropped on that same change, and so every re-run
is a wasm call. Tallying `Wire.invoke` over the scroll gave 10,150 `resolveCommand` misses (98 ms),
12,992 `isOpen` misses, and about 2,500 misses each for some 20 per-page reads (`getChromeSettings`,
`listPageItems`, `getSourceKey`, `listLinks` and the rest) that a scroll doesn't change.

Three changes, in order of payoff for effort:

- The toolbar forces a render on every change (`packages/framework/react/src/toolbar.tsx:509`) and
  resolves every command each time. The scroll rendered `Toolbar` 404 times, `CommandButton` 11,312
  times and `Icon` 9,696 times, 67 ms inclusive before counting the `resolveCommand` crossings. It
  should re-render only when a resolved command differs, which `resolvedCommandsEqual` already
  decides. `main` has the same line, so the TS kernel gains too, just less.
- The command predicates ask the shell `isOpen(id)` once per surface: `mode:view` alone checks all
  six mode surfaces (`packages/viewer/chrome/src/config/commands.ts:417`, and `:785`, `:860`, plus
  `packages/viewer/chrome/src/Shell.tsx:71`). `listOpenSurfaces()` takes no arguments, so reads of
  it can share one cached answer. (`menus.isOpen` in `MenuAnchoredButton` looks like the culprit but
  is a local array check.)
- A selector could skip its re-run while its capability's slice revision holds, using the per-slice
  revisions item 1 added. That's up to about 200 ms (the selector re-runs under `flush`), but some
  reads depend on other slices (`getPlan` reads the stage camera), so each capability has to declare
  what it reads. A wrong declaration shows up as stale UI, not as an error, which makes this the
  riskiest of the three.

The first two are prototyped on `research/rust-kernel-toolbar` at `e4d533d18` (the
`plugins-toolbar` arm). The toolbar no longer renders during a scroll: 0 renders where there were
404. It still resolves as many commands (10,125 against 10,150), because the change check now does
the reads the render used to. `isOpen` calls fell from 12,992 to 5,265, but only 2,025 of 5,670
`listOpenSurfaces` reads hit the cache: `Wire.bump()` drops every cached answer after any call that
missed (`packages/core-wasm/src/proxy.ts:309`), so the cache only helps calls made back to back. A
local profile of the scroll went from 1,277 to 1,126 ms busy, Preact from 132 to 39 ms.

The quick remote run (R=3, `results/2026-09-30T18-07-04-233Z-2023MBP16Grey-desktop`) agrees only in
part. `zoom` main-thread task time fell 12% and `open(ebook)` 5%, and `scroll` task time didn't move
beyond noise. But script time rose: 20% for `scroll`, 24% for `open(large)` and 15% for `annotate`.
The full R=10 run (`results/2026-09-30T18-24-11-976Z-2023MBP16Grey-desktop`) says it's real, and
so is the saving: against `plugins-getstate`, scroll task time fell 8% while its script time rose
12%, and `open(large)` script time rose 22% with task time unchanged.

Split into one arm per change (R=3, `results/2026-09-30T20-09-12-448Z-2023MBP16Grey-desktop`), the
toolbar gate alone (`plugins-item1`, `1efa42de6`) carries both effects: scroll task time -8%, script
+12.5%, `open(large)` script +24%. The surface predicates alone (`plugins-item3`, `eeea6d289`) show
nothing beyond noise on 15 of 17 main-thread rows, and read slower on `jump` script time (+36%, from a
noisy baseline) and `search` task time (+24%). On its own it isn't worth keeping.

Keeping cached answers while the store's revision holds (`plugins-bump`, `eba60e600`, on top of both)
is the clear win. Against the toolbar gate alone it cuts scroll task time 10.5% and script time
7.6%, `zoom` task time 12%, and `select` task time 25% with 12 ms off its settle. Against
`plugins-getstate` that's scroll task time -18% and wall time -8%, `zoom` -13% and `select` -31%.
The `open(large)` script cost of the gate is still there (+23%).

That cost isn't more JavaScript. A main-thread trace of `open(large)` (the `devtools.timeline`
category, self time) shows the same JS on both arms, 419.8 ms on `plugins-getstate` and 420.2 ms
with the gate. What moved is where it runs: `FunctionCall` went from 292.6 to 356.1 ms and
`RunMicrotasks` from 127.2 to 64.1 ms. A Preact render is scheduled as a microtask, and the
`ScriptDuration` metric `mainScriptMs` reads doesn't count microtask JS; the change check runs in the
kernel's change listener, which it does count. So `mainScriptMs` undercounts any arm that renders
through microtasks, `main` included, and `mainTaskMs` is the one to trust. Item 3 is reverted on
`research/rust-kernel-bump` (`dd1b86fcc`).

Without item 3 the cache fix holds (R=3, `results/2026-09-30T20-45-12-681Z-2023MBP16Grey-desktop`).
Against the gate alone, scroll task time falls 8.5%. Against `plugins-getstate`, the gate and the
cache fix together make scroll task time 16.5% lower and wall time 7.4% lower, `zoom` 16%, `select`
17%, `annotate` 12% and `open(ebook)` 6.4%, with `zoom2SettleMs` 19 ms sooner.

The third fix, prototyped without declarations (`research/rust-kernel-read-sets` at `685a5472b`, the
`plugins-reads` arm): the store records which slices, and whether core, each capability call reads
(`Store::track_begin`/`track_end`), and the wire keeps a read's answer across store changes until
one of those parts moves. A read that called JavaScript back, read nothing from the store, or read
the whole tree isn't kept, and a call that isn't a read retires the kept answers of its own
capability (the wire learns each member ref's capability from its pinned description). It works as
a cache: in the 60-wheel scroll, misses in the 40 busiest members fell from 61,341 to 27,994 and the
per-page reads from about 2,200 misses each to about 1,300. It doesn't pay. Against `plugins-bump`
(R=3, `results/2026-09-30T22-28-55-068Z-2023MBP16Grey-desktop`) no row moves beyond noise; the point
estimates are 2 to 3% slower for `scroll` and 5 to 8% slower for `jump` and `zoom` task time, 11 to
17% faster for `select`, `search` and `annotate`. What a miss costs now is small, and each kept
answer pays for a `last_reads` crossing and parse, a `BTreeSet<String>` per tracked call, and a
`state_revisions` parse per change. We'd leave it out. Its residual risk also stays: a read that
depends on another plugin's state outside the store, changed by a call that doesn't touch the
store, would read stale.

The selector re-runs themselves are now mostly JavaScript: `plugins-reads` spends 654 ms of JS in
the scroll (`mainJsMs`) against 306 ms on `main`, which runs the same hooks against a TypeScript
kernel.

That run also prices the trace behind `mainJsMs`. Against the untraced run before it
(`results/2026-09-30T20-45-12-681Z-2023MBP16Grey-desktop`), most timing rows on `main` and
`plugins-bump` moved by 3% or less, which is inside run-to-run noise. `zoom` settle moved more on
both, 16% and 11%, so traced and untraced `zoom` timings shouldn't be compared.

The toolbar gate on the TypeScript kernel (`research/rust-core-toolbar-gate` at `30f4295d7`, the
impl pin plus the gate, the `impl-gate` arm) is a clear win too, and a larger one in proportion
(R=3, `results/2026-10-01T01-22-09-931Z-2023MBP16Grey-desktop`, A/A 39 of 39). Against `impl-ts`,
`open(large)` task time falls 38% (264 to 164 ms), scroll 16% (400 to 337 ms), `zoom` 15%,
`annotate` 14% and `search` 37%, and a jump settles 16 ms sooner. JavaScript time falls 21 to 61%
on the same rows. One row reads slower, `open(ebook) / readyMs` by 3.4 ms, which is measured
before any document exists and so before the gate runs. `main` has the same subscription
(`packages/framework/react/src/toolbar.tsx:509`), so the gate would help it as well.

### Serialization formats

The first win isn't a faster encoder but not serializing at all: items 1 and 2 remove the repeated
full-state builds. What remains is dominated by building `serde_json::Value` trees, not by writing
text. Serializing typed structs straight to a reused buffer with `serde_json::to_writer`, caching
each slice's text, and swapping SipHash for `rustc-hash` or `ahash` in the kernel's own maps all help
more than a format change would. The SIMD JSON crates (`simd-json`, `sonic-rs`) don't target
`wasm32-unknown-unknown` as built.

On the JS side, `JSON.parse` is native and hard to beat for building object graphs. MessagePack
(`rmp-serde` with `msgpackr`) or CBOR (`ciborium` with `cbor-x`) would shrink numeric payloads and
might match `JSON.parse` speed, but they change both ends of every call. We haven't measured them.
`serde-wasm-bindgen` avoids text entirely but makes one boundary call per field, which usually
loses on large payloads. For bulk numeric data (page layouts, glyph geometry) packed `Float32Array`
views over wasm memory are the clear answer: no parse at all, and transferable across the worker
boundary.

## The heap leak

About ten `ArrayBuffer`s and 24 `AbortController`s survive each soak cycle. The next step is a
retainer path from a heap snapshot. Separately, `Session.staged_meta` is never cleared after
`commit_ready` (`kernel.rs:617-633`), so each open document holds a second full copy of its pages;
`staged_meta.take()` fixes that in one line.

## Engine, on every branch

These apply to `main` too, since they're in the shared TypeScript engine and its build.

The engine's final link is unoptimized. `packages/engine/runtime/build/compile.esm.sh` (`:35-50` on
`main`, `:80-97` on impl) passes no `-O` and sets `-sASSERTIONS=1`. That wraps all 915 exports in
checking wrappers, links emcc's debug system libraries, and blocks metadce. Linking with
`-O2 -sASSERTIONS=0 -sSTACK_OVERFLOW_CHECK=0` cuts the wasm 8% raw (`wasm-opt -O3` alone takes the
impl engine from 11,084,440 to 10,198,993 bytes) and should speed up the worker by an estimated 2 to
8%. It can be tested without a branch edit, because emcc appends `EMCC_CFLAGS` last. The cost is
that a runtime misuse becomes an opaque trap instead of a named assert.

The TypeScript per-glyph cost is mostly glue. Each PDFium call goes through `cwrap`, a `bridge`
closure with `args.map`, `ccall` and the assert wrapper (`packages/engine/runtime/src/wasm/wasm-runtime.ts:246-272`,
`:473-487`). Each returned pointer allocates a BigInt (`wasm-runtime.ts:43-45`), and each f32 read goes
through `getValue` (`structs.ts:69-71`), about 20 per glyph. Binding raw exports and reading
`HEAPF32` views in hot loops helps geometry and annotation reads on every branch.

Search always uses the TypeScript geometry reader, with no cache, for up to 64 matched pages per
slice (`packages/engine/services/src/features/search/SearchReader.ts:139`), and its folding loop
allocates several strings per code point (`packages/engine/core/src/search/fold.ts:60-86`). Caching
geometry per page by `mutationSeq`, as `pageCorpusCache.ts` already does for text, and an ASCII fast
path in folding are both small.

Geometry travels as about 1.7 MB of JSON per dense page, with every f32 widened to f64 and printed to
17 digits (`crates/epdf-engine/src/features/geometry.rs:344-360`). It's then structured-cloned to the
main thread with no transfer list (`WorkerHost.ts:1299`), about 25,000 objects per page. Packed
typed arrays on the transfer list fix both, at the cost of changing the `PageGeometrySnapshot` DTO.

Every text read loads and closes the text page (`PageGeometryReader.ts:94`, `PageTextReader.ts:46`,
`geometry.rs:264`), and a matched search page loads it twice. Caching `FPDF_TEXTPAGE` in
`PagePtrPool` needs its lifetime and leak checks extended.

## Build settings

These are cheap and each worth measuring as its own arm, but none changes the picture above.

| Change | Evidence | Expected |
| --- | --- | --- |
| Real LTO for `epdf-web`: `crate-type = ["cdylib", "rlib"]` makes Cargo skip `-C lto` | measured with the `plugins-lto` arm | about 10% on kernel-heavy rows |
| `wasm-opt -O3 --strip-debug` after `wasm-bindgen` (`crates/epdf-web/scripts/build.sh` runs neither) | measured on the built wasm | 11.3 to 8.3 MB raw, brotli 2.13 to 2.03 MB; speed 0 to 5%, unmeasured |
| WebAssembly SIMD: no `+simd128` in any `target_features` section, no `-msimd128` in the fork | artifact inspection | 0 to 15% on render rows, a guess |
| `main`'s payload uses emsdk 3.1.72 without bulk-memory: large `memcpy` and longjmp go through JS imports | artifact imports | within the noise band for these fixtures |
| Two copies of `preact/compat` in every bundle, including the shipped viewer (`compat.mjs` via the alias, `compat.module.js` via `compat/client`) | bundle contents | 1 to 3% of main-thread script, plus a correctness risk |
| Stage B's 65-op `Host` is linked into every Rust-core payload (`compile.esm.sh:68-69`) | export list | most of the 7.19 to 10.57 MiB engine growth |
| `serde_json`'s `float_roundtrip` is on across the kernel | crate features | about 2x on float parsing; risky for JS parity |
| PDFium with `-flto` | none | 5 to 15%, a guess, at much longer builds |

Checked and left alone: the kernel already aborts on panic (`wasm32-unknown-unknown` defaults to it),
the engine needs unwind for its poisoned-session path, the allocator and memory settings are
reasonable, the exnref exception encoding costs nothing when nothing throws, `wasm-bindgen` already
uses externref, both wasm files are streamed, and the Vite build is a clean production build.

## What the harness should measure next

The `select` flow doesn't time `pages.geometry`, because page 1's geometry is loaded before `t0`.
`impl-ts` against `impl-rust` needs a flow that reads cold geometry on many dense pages, and the gate
should prove the Rust route ran rather than only that the payload contains the core
(`lib/gate.mjs`). The runs are cold, so wasm starts in Liftoff; a diagnostic arm with
`--js-flags=--no-liftoff` would show how much of `openedMs` is baseline-tier execution.

Items 1 and 2 below, the next step this section first named, are done. The large flows came after
(item 10); the three gaps in the paragraph above are still open.

## Not tried yet

Updated 2026-10-05, with every kept fix merged into
`research/multi-platform-rust-core-impl-plugins` (`c8e53c523`). Each item is described in the
section named, and says whether it was tried. Items 8, 9, 12 to 15 and the rest of 10 are open.

1. LTO in the branch's own build (Build settings). Tried since, and measured on its own below: it
   no longer pays.
2. The engine's final link with `-O2 -sASSERTIONS=0` (Engine, on every branch). Tried since, each
   flag on its own: `-O2` is merged, and assertions stay on by choice. See "`-O2` and assertions,
   apart" below.
3. `wasm-opt -O3 --strip-debug` on the kernel wasm (Build settings). Tried since, and measured on
   its own below: a small win, kept.
4. `staged_meta.take()` (The heap leak). Tried since, and left alone: it isn't a second copy any
   more, and the conformance vectors pin what it holds. See below.
5. The second copy of `preact/compat` in every bundle (Build settings). Tried since, and left
   out of the research branches: see below.
6. Typed serialization with `serde_json::to_writer` into reused buffers, and `rustc-hash` for the
   kernel's own maps (Serialization formats). The serialization half was narrowed to the one slice
   the profile points at, and tried: see "The measurement slice's text" below. The `rustc-hash`
   half wasn't, and is item 13.
7. The engine glue and geometry work shared with `main` (Engine, on every branch): raw exports and
   `HEAPF32` reads, a per-page search geometry cache and an ASCII folding fast path, geometry as
   transferable typed arrays, and a cached `FPDF_TEXTPAGE`. Profiled since: only the per-glyph
   reads paid, and their fix is below. Measuring it turned up the search regression below it.
8. The PDF's bytes as a `$host` handle instead of base64 at open (section 5). About 20 ms of main
   thread, and every Rust call site that takes bytes changes. Tried since, and kept: see "The PDF's bytes as a
   host handle" below.
9. SIMD, PDFium with `-flto`, and `float_roundtrip` (Build settings). All three expectations are
   guesses, and `float_roundtrip` risks JS parity.
10. The harness gaps above: a cold-geometry flow, a gate that proves the Rust route ran, and a
    `--no-liftoff` diagnostic arm. Large-document flows were added since (`coldScroll`,
    `searchLarge`, `zoomLarge`, `selectLarge`, `annotateLarge`); those three gaps are still open.
11. Typed engine answers in the kernel, without the `Value` copies (Where the plugins tip's main
    thread goes). Added 2026-10-05, tried since, and kept: see that section.
12. Resolved commands cached per kernel revision (Where the plugins tip's main thread goes). Added
    2026-10-05. It's what `open` still costs.
13. `rustc-hash` in place of SipHash for the kernel's own maps (Serialization formats, and Where the
    plugins tip's main thread goes). About 5 ms of `jump`. Decided against, 2026-10-09: of the 9.6
    ms of SipHash in the `plugins-tip6` `scroll` profile, 3.9 ms was the `json!` wrapper item 17
    removed, and about 4 ms is `serde_json` objects, which are `IndexMap`s with the standard hasher
    in every crate because `plugin-conformance` turns on `preserve_order` and Cargo unifies the
    feature; `serde_json` has no way to change that hasher. The kernel's own maps are 1 to 2 ms. A
    new hasher also changes iteration order in 47 files that use `HashMap` or `HashSet`, where any
    map iterated into output could drift from the conformance vectors.
14. The engine bridge's `canonical()` encoding of every engine answer, the JavaScript half of item
    11 (Where the plugins tip's main thread goes). 10.5 ms of `jump` in the profile. Tried since,
    and kept: see that section.
15. The search panel reading `listHits()` at most once a frame during a search (Search on the Rust
    kernel). Only `searchLarge` would gain.
16. `rustReads` with the Rust kernel. Every plugins arm runs with `rustReads: false`, and
    `lib/config.mjs:18` refuses the pair ("rustReads needs the typescript kernel"), a rule from the
    harness's first commit (`400575787`) that nobody has checked is still needed; the plugins
    branch still has `rustRouting` (`packages/engine/main/src/index.ts:359`). To find out for
    certain: lift the guard, add an arm with both, and time it. Expected small: geometry was about
    2% of the worker's time (`impl-ts` against `impl-rust`, above), and it runs in the worker, not
    on the main thread where the plugins gap is. Added 2026-10-08. Measured since, and left
    off: see below.
17. Each call's result written straight to JSON text, without a `serde_json::Value` (Where the
    plugins tip goes now). Up to about 40 ms of `scroll`. Added 2026-10-08. Tried since, and kept:
    the copy came from `json!`, not from the plugins, so no harness `call` had to change. See that
    section.

Decided against: a binary wire format (Serialization formats; JS-side decode is about 2% of busy
time), skipping `after_entry` on read-only exports (section 5; the capability cache removed most of
what it would save), and read sets for the hook selectors (section 6; no row moved).

### LTO and `wasm-opt`, each on its own

Both are prototyped on the plugins tip (`51130b042`) as separate branches, so either can go
without the other, and timed against that tip built the branch's own way, without LTO (R=3,
`results/2026-10-01T02-19-00-897Z-2023MBP16Grey-desktop`, A/A 39 of 39).

LTO (`research/rust-kernel-build-lto` at `99d1a8c04`, the `plugins-buildlto` arm) builds `epdf-web`
as a cdylib only in `crates/epdf-web/scripts/build.sh`, since the crate's integration tests need the
rlib in `Cargo.toml`. No row moves beyond noise: the point estimates are 3% faster on scroll task
time, 0.5 to 1% slower on the open rows, and 15 ms later on jump settle with an interval from -12 to
+49 ms. It costs 921,595 bytes of kernel wasm (+8.1%), 117,163 brotli (+6.3%). The 10% it was
worth on `plugins-lto` came from a kernel that spent most of every row in Rust; the fixes since
took most of that time away. It's built exactly as the harness's `rust-kernel-lto` recipe builds
it (the same 12,254,473 bytes, and no difference from `plugins-bump` on any row). We'd leave it
out.

`wasm-opt -O3 --strip-debug` after `wasm-bindgen` (`research/rust-kernel-wasm-opt` at
`e981e0cb9`, the `plugins-wasmopt` arm) takes the kernel from 11,332,878 to 8,311,869 bytes
(-26.7%), 1,849,657 to 1,776,551 brotli (-4.0%). `readyMs` is 7.8% sooner on `open(ebook)` (82.1
to 75.7 ms) and 6.4% on `open(large)`: a smaller module compiles and instantiates sooner. No row
is slower, and the open and scroll point estimates are 2 to 5% faster. Its cost is a build
dependency: `build.sh` now needs binaryen's `wasm-opt` (version 133 here) wherever the kernel is
built. It's merged into `research/multi-platform-rust-core-impl-plugins` (a fast-forward to
`e981e0cb9`); the LTO branch is deleted.

### The engine's optimized link

`-O2 -sASSERTIONS=0` in the final emcc link (`packages/engine/runtime/build/compile.esm.sh`),
measured on both research branches (`research/rust-core-engine-link` at `7ed02e399` on impl,
`research/rust-kernel-engine-link` at `dfb5c43c8` on plugins; R=3,
`results/2026-10-01T03-11-14-653Z-2023MBP16Grey-desktop`, A/A 38 of 39). The engine wasm goes from
11,084,440 to 10,065,349 bytes (-9.2%), 2,999,741 to 2,986,548 brotli. On impl, `open(ebook)`
opens 6% sooner and `open(large)` opens 4% and settles 7% sooner (226.1 to 211.3 ms); on plugins
the open rows don't move beyond noise. `search` main-thread task time reads slower on both (2.5 to
3.3 ms on impl, 9.0 to 17.1 ms on plugins against a baseline p95 of 15.3 ms).

The link keeps `-sSTACK_OVERFLOW_CHECK=1`, which `ASSERTIONS=1` used to turn on: a cookie at the
top of the C stack, checked at the end of each tick, so a stack overflow aborts instead of silently
overwriting the data below the stack. What turning assertions off costs otherwise is the JS glue's
named aborts (a call with the wrong argument count, a runtime method that wasn't exported), and it
cost us twice before it landed:

- Without assertions, emscripten's `cwrap` returns the raw export for every all-numeric signature
  and counts `'boolean'` as numeric (`library_ccall.js`), so C `bool` results came back as 0 and
  1. Ten engine tests failed that way. `toJsResult` in `packages/engine/runtime/src/wasm/wasm-runtime.ts`
  now maps them to booleans whatever the build.
- The argument-count assertion is what exposed a real bug on both research branches: their
  `wasm32-eh` engine is linked with emcc 4.0.0, which turns `WASM_BIGINT` on by default, while the
  TypeScript runtime splits each i64 argument into two i32 halves. `EPDFSig_Seal` was called with
  13 arguments where it takes 10, and signing, attachment extraction and revision reads aborted (47
  of the 68 engine test failures). `-sWASM_BIGINT=0` fixes it (`fba088f7d` on impl, `dea3e7e77` on
  plugins). With assertions off it would have read wrong arguments instead of aborting.

Both conditions are now met. `build/verify-wasm-abi.mjs` reads the linked wasm's export types and
fails the link when a bound function's parameter count doesn't match what `wasm-runtime.ts` passes
(an i64 counts twice) or an export takes an i64; the old pin fails it on 16 functions, the fixed
builds pass all 918. The engine-link branches are rebased onto the fixed tips
(`research/rust-core-engine-link` at `2a3bb96ed`, `research/rust-kernel-engine-link` at
`48fda657e`), and every arm fails the same 21 engine tests as its base.

Re-measured on the rebased branches (R=3, `results/2026-10-01T03-48-18-182Z-2023MBP16Grey-desktop`,
A/A 39 of 39), no row is slower on either branch, and the `search` slowdown above doesn't
reappear. On impl, `open(large)` opens 5.6% sooner (143.4 to 135.3 ms), `jump` main-thread task
time is 14% lower (57.6 to 49.4 ms) and the first zoom step settles 8.6 ms sooner; on plugins,
`open(large)` opens 3.8% sooner (204.0 to 196.3 ms). The engine is 11,120,042 to 10,098,549 bytes
(-9.2%), 3,019,872 to 2,994,491 brotli. We'd keep it.

Of the 21 engine test failures left on every arm, 17 need the native Node build
(`pdf-runtime.node` and `libembedpdf.dylib`), which the harness's worktrees don't build; with it
present those 7 files pass, 38 of 38. The other 4 looked like the Rust core's own but were test bugs:
the rehearsal test read `doc.security.encryptionState`, where the state is
`doc.security.current.encryption` (`test/session-rust-rehearsal.test.ts:59`, `:75`), and the parity
test addressed pages by `layout.pageObjectNumber`, where the layout carries a `PageRef`
(`test/geometry-rust-parity.test.ts:65`). With those reads fixed (`65d9798d6` on impl, `4f9ceee9c`
on plugins) the session and parity tests pass, 7 of 7, so the Rust session and geometry reader
agree with TypeScript on both fixtures. What's left is the 17 that need the native build.

#### `-O2` and assertions, apart

The link above changes two things, so each is measured on its own as well: `-O2` with assertions
kept (`research/rust-core-engine-o2` at `977a874e4`, `research/rust-kernel-engine-o2` at
`785e68335`), against the same base and the full link (R=3,
`results/2026-10-01T04-27-30-603Z-2023MBP16Grey-desktop`, A/A 39 of 39).

| Build | Engine wasm | Brotli |
| --- | ---: | ---: |
| base | 11,120,042 | 3,019,872 |
| `-O2`, assertions on | 10,164,511 (-8.6%) | 3,013,134 (-0.2%) |
| `-O2`, assertions off | 10,098,549 (-9.2%) | 2,994,491 (-0.8%) |

`-O2` is 94% of the size. On speed, only the open rows move, and only by 3 to 7%. On impl, `-O2`
alone opens `ebook.pdf` 4.7% sooner, and turning assertions off on top shows no detectable
difference. On plugins, `-O2` alone shows none, and turning assertions off on top opens `ebook.pdf`
4% sooner and settles it 3.4% sooner. No row is slower in any of the four comparisons. The 14% on
`jump` in the run before didn't reproduce.

We'd take `-O2` and keep assertions: it gets nearly all the size and part of the speed, and what
assertions cost is a few percent on one row of one branch. They already caught one real bug here
(the i64 split), and the named abort is worth more than 3% on opening a small document. That's
what's merged: `-O2` with assertions on, plus the `toJsResult` bool fix, which the raw-export
fallback needs in any build (`a1cc26fe3` and `ec18b766d` on impl, `f625ccfe4` and `fa97fb3e8` on
plugins). The assertions-off branches are deleted.

### `staged_meta`

The heap-leak section's one-line fix is out of date and wrong. Since the page-registry fix,
`DocumentMeta.pages` is an `Rc<Value>` (`crates/epdf-kernel/src/types.rs:45`), so the staged meta
shares the pages core holds rather than copying them. What it does hold on to is the meta the
document opened with, until the document closes; after a page edit replaces the registry, that's
one stale page registry per edited open document.

And that stale meta is behavior the TypeScript kernel has, which two conformance vectors pin:

- `staged_meta.borrow_mut().take()` in `commit_ready` fails `plugins/page-edit.json`,
  `a-captured-capability-outlives-its-document`: a capability captured before the close reads its
  document's meta through `ctx.document()` and must reach the lifetime guard ("document instance
  a#1 was closed"); with the meta gone it fails earlier, "no page 20".
- Parking core's current meta at close as well fixes that and fails `plugins/stage-feeds.json`,
  `page-mutations-refit`, step 11: stage's refit during the close has to see the pages the document
  opened with to report what the TypeScript kernel reports.

So the kernel keeps it, at the cost of one page registry per page-edited open document, until
close. Changing it means changing the TypeScript reference and the vectors with it.

### One copy of `preact/compat`

The viewer's `react` and `react-dom` aliases resolve `preact/compat` the way Node does, to
`compat/dist/compat.mjs`, but `compat/client.mjs` imports `preact/compat` by name and Vite resolves
that the way a browser does, to `compat/dist/compat.module.js`. So every bundle carried both, the
shipped viewer included, and each copy patches Preact's `options` hooks when it loads. Aliasing
them to `compat.module.js` leaves one (source maps of the built viewer list `compat/client.mjs`
and `compat/dist/compat.module.js` only). The bundle shrinks by about 5.4 KB raw and doesn't move
compressed.

Measured with the harness's `dedupeCompat` switch, each arm against its own build (R=3,
`results/2026-10-01T06-10-46-897Z-2023MBP16Grey-desktop`, A/A 39 of 39): on `main`, `open(large)`
main-thread task time is 7.1% lower (262.7 to 244.0 ms) and JavaScript time 7.7% lower; on impl
and plugins no row moves beyond noise. `main`'s toolbar still re-renders on every change, which
the toolbar gate removed on the research branches, so there's less there for a second set of hooks
to slow down. With no gain on either research branch, what's left is the cost: an alias to a file
inside preact's package, which a preact release that renames its dist files would break at build
time. The two copies haven't caused a bug we've seen, so the research branches keep their aliases
as they are. `main` has the same aliases and would gain the 7% there, which is a change for `main`
to decide.

### The measurement slice's text

Profiled on the current kernel, a first scroll through `large.pdf` spends 155 of its 493 ms of wasm
self time in `SliceLease::text`, nearly all of it on one slice: the measurement slice, re-read 198
times in 60 wheels at about 82 KB each (20.6 MB). Each change replaces exactly one page's entry,
the page whose scale just loaded, and the slice's text was rebuilt from the whole value each time.

`research/rust-kernel-slice-text` (`d9c5e4a58`, on the plugins tip `4f9ceee9c`) lets a slice
register a text writer (`SliceText`, through `PluginBuilder::slice_text`), which the store uses in
place of `to_json(state).to_string()`. Measurement's keeps each shared `Rc<PageEntry>`'s text and
serializes only replaced entries; the text is byte for byte `js::to_value(state).to_string()`,
which its tests check (numeric key order, `NaN`, the cache across replaced entries). Rust
workspace tests 256 of 256.

The harness's `scroll` didn't see it: its warm-up loads the pages' scales before the measured
window, so no row moved. The new `coldScroll` flow is the same 60 wheels on a document just opened.
There (R=3, `results/2026-10-01T08-25-07-278Z-2023MBP16Grey-desktop`, A/A 47 of 48), main-thread
task time falls 25% (1048.2 to 787.3 ms) and JavaScript time 28% (934.0 to 672.4 ms); every other
row shows no detectable difference. Against `main`'s 436.5 ms, the cold first scroll goes from
2.4x to 1.8x, level with the warmed one. The cost is 15,788 bytes of kernel wasm and a second
serialization path a plugin has to keep identical to its `to_json`. We'd keep it.

The JavaScript side still parses the whole 82 KB once per change. Batching the scale loads, so the
slice changes once per scroll step rather than once per page, would cut that too, at the cost of
changing the plugin's action sequence, which the conformance vectors check.

### Glyph reads, one view per struct

A worker profile of the engine during a cold search for "the" on `large.pdf` (on impl's TypeScript
kernel) puts 1,267 ms in the worker, 783 ms of it JavaScript: `readGlyphRaw` 485 ms inclusive,
`peek` 204 ms self. Each glyph's geometry struct (`EPDF_CHAR_GEOMETRY_LAYOUT`) was read up to 33
values at a time through `mem.peek` and emscripten's `getValue`. Open and a cold scroll barely
touch the engine (the worker is idle 2,427 of 2,645 ms), so the rest of item 7 has nothing to win
there. Reading `f32`/`i32` from the heap inside `peek` itself was slower, not faster: each call then
paid for `HEAPU8.buffer`.

`research/rust-core-glyph-view` (`061a544c4`, impl) and `research/rust-kernel-glyph-view`
(`16f128278`, plugins) add an optional `PdfRuntimeMemory.peekWords(ptr, bytes)`, f32 and i32 views
over a struct that the wasm runtime implements and the native runtime doesn't, and the reader
takes each glyph from one view. Locally `readGlyphRaw` falls 44% and the worker's search 18%; the
engine suite fails the same 21 tests as before, and the Rust-TypeScript geometry parity tests pass
(18 of 18). A new `searchLarge` flow, `search` on `large.pdf`, measures it: on impl, 1,624 to 1,337
ms (-18%; R=3, `results/2026-10-01T16-44-36-878Z-2023MBP16Grey-desktop`), with no row slower.

### Search on the Rust kernel

`searchLarge` on the plugins kernel took 37.9 s, all of it main thread, against `main`'s 1.6 s.
Two things, both fixed on `research/rust-kernel-lazy-state`:

- Two selectors read `kernel.getState()` on every change for one part of it, i18n's slice
  (`packages/framework/react/src/i18n.ts:41`) and core's documents
  (`packages/framework/react/src/annotation.tsx:1138`), and `getState()` read and parsed every
  changed slice. The search slice holds every hit so far and changes once per page of results, so
  the whole hit list was rebuilt in Rust and re-parsed in JavaScript once per page. Each plugin
  slice is now a getter that reads it on first access (`36ae29149`); core is still read once per
  change. An outdated state's unread slice answers with the current one, and one `core-wasm` test
  now reads its slices while its state is current. This was also what kept re-reading the
  measurement slice during a cold scroll.
- `Core::hits_from_slice` found each match's page in the registry twice, each a scan of every
  page's ref. It now looks each page up once per slice (`f83e60654`).

On the remote (same run), `searchLarge` goes from 37,907 to 2,176 ms and its main thread from
37,856 to 1,020 ms, against `main`'s 231 ms of JavaScript; no other row moves. Rust workspace tests
256 of 256, `core-wasm` 57 of 57 and 10 of 10. What's left of the gap is the plugin's per-page
work on the main thread and the worker's glyph reads, which the glyph view above would cut too.

With both merged (the plugins tip at `373b7f73b`, the `plugins-tip3` arm), `searchLarge` is 1,866
ms against `main`'s 1,650 (1.13x; R=3, `results/2026-10-01T18-02-14-142Z-2023MBP16Grey-desktop`).
Profiled with a named kernel, its 994 ms of main thread is about 300 ms of per-page plumbing in the
engine callback (futures, promise guards, frees), about 231 ms of the search panel's
`useSelector((c) => c.listHits())` (`packages/viewer/chrome/src/ui/search-panel.tsx:128`) reading
every hit so far on every change (348 calls, 17.3 MB of answers), about 213 ms of moving engine
answers across the boundary, 101 ms of `reduce_search`, and 20 ms of building hits.

`reduce_search` copied every hit so far, segments and snippet included, each time a page of
results arrived, and a page's bucket once per hit added to it. `research/rust-kernel-shared-hits`
(`b450a9909`) holds hits as `Rc<SearchHit>` in the state and in the `Append` action, and replaces
each bucket once per append; the serialized state and actions are unchanged (serde's `rc`
feature). Rust workspace 256 of 256, search conformance 4 of 4, two new reducer tests for order
and sharing. `searchLarge` goes from 1,873.5 to 1,749.3 ms (-6.6%), its main thread from 1,038 to
890 ms (-14%), and it's 1.06x `main` (R=3, `results/2026-10-01T18-55-22-254Z-2023MBP16Grey-desktop`).

The panel's full-list reads aren't worth a paged API at that point: they're the worst case of one
flow, they shrink quadratically with fewer hits, and paging would give the Rust kernel a different
panel from the TypeScript reference. Reading `listHits()` at most once a frame during a search
would be the cheap version, if it's ever wanted.

### Where the plugins tip's main thread goes

On the six-arm run (RESULTS.md, 2026-10-02) the plugins tip (`b450a9909`) still spends a median
1.80x `main`'s main-thread time across the flows. We profiled `jump` and `open(large)` on this
machine (Apple M4 Max), 5 runs each at a 100 µs sampling interval, inside the harness's own
measured window. The arm was rebuilt for it with an unminified bundle and a kernel that keeps its
function names (`wasm-opt -O3 -g` in place of `--strip-debug`; the stripped rebuild is
byte-identical to the shipped kernel, `5f383fbcd10b`). Two costs stand out, and neither is in the
sections above.

`jump` runs 113 ms of main-thread JavaScript here against `main`'s 31. Most of the gap is the page
geometry `SelectionCapability::load` (`crates/plugins/selection/src/controller.rs:319`) fetches for
the page it lands on, and how that answer gets into the kernel. The engine bridge encodes it with
`canonical()` (`packages/core-wasm/src/engine-bridge.ts:162`, 10.5 ms). `engine_call`
(`crates/epdf-web/src/engine.rs:117`) parses the string into a `serde_json::Value` (12.8 ms).
`Promise::settle` (`crates/epdf-kernel/src/rt.rs:364`), `Guard::promise` and `PromiseFuture::poll`
then each deep-clone or drop that `Value` (about 14 ms together), and the selection plugin converts
it into `Vec<PageGeometryRun>` (10.9 ms). That's about 48 of the 82 ms gap, and most of the 20 ms
of `dlmalloc` self time. Parsing the answer straight into the type the caller wants, and moving
the settled value instead of cloning it, should remove most of it. Page geometry loads on
`scroll`, `zoom` and `open` too, so they should gain as well; we haven't profiled them for it.

`open(large)` runs about 465 ms of JavaScript, and 255 ms of it is `flush`, the subscription that
re-runs every hook selector after each kernel change. The largest part is command resolution:
`CommandsCapability::resolve_command` (`crates/plugins/commands/src/controller.rs:183`), reached
from the toolbar and from the page strip's `visible`, takes about 65 ms, and each resolution calls
the command's scripted predicates back into JavaScript through `live`
(`crates/epdf-web/src/exports.rs:169`). The wire's revision cache doesn't cover these calls, and
the toolbar gate (section 6) didn't remove them. Caching resolved commands per kernel revision, or
re-resolving one only when its inputs change, would. That's a bigger change than the first: it
changes when the commands plugin evaluates predicates, which the TypeScript reference does on read.

Smaller: SipHash is about 5 ms of `jump` (the `rustc-hash` half of item 6, never tried), and
Preact's diff is about 48 ms of `open`, which `main` pays as well.

Item 11 is two commits on `research/rust-kernel-typed-results`, timed apart. `19f07e077` stops the
kernel's `Promise` (`crates/epdf-kernel/src/rt.rs`) from copying a result nothing else reads:
`settle` moves it into the state when no observer is waiting, and `PromiseFuture::poll` moves it out
when the future holds the promise's last handle, and no longer clones it on the poll that only
yields a turn. `b88217027` adds `DocumentHandle::call_answer` and `Answer`
(`crates/epdf-kernel/src/engine.rs`): the web host hands over the answer's JSON text, and selection
parses `page.geometry.read` and `page.text.read` straight into its snapshots. Other hosts keep
`call`'s parsed value, in the same turn. Rust workspace 873 of 873, `core-wasm` 57 of 57 and 10 of
10.

On the remote (R=10, `results/2026-10-05T18-16-21-739Z-macmini2020-desktop`, A/A 63 of 63, no
failed samples), neither commit is slower than the plugins tip on any row. The promise change cuts
`jump` main-thread JavaScript 12% (175.8 to 154.1 ms), `scroll` and `coldScroll` 3 to 4%, and
`searchLarge` 19% (1,061 to 862 ms), its time 9% (2,076 to 1,882 ms): search answers stopped being
copied too, which the profile didn't predict. The typed answers cut `jump` another 13% (to 134.5 ms)
and `scroll` and `coldScroll` 4%. Together `jump` settles 24.7 ms sooner, and against `main` its
JavaScript goes from 3.28x to 2.53x. `open` doesn't move: its cost is item 12. The kernel grows 0.09
MiB (7.94 to 8.03), 0.01 MiB brotli. The JavaScript half, `canonical()` encoding the answer, is
still there (10.5 ms of `jump` in the profile). Both are merged into
`research/multi-platform-rust-core-impl-plugins`.

Item 14 is `57f627ed5` on `research/rust-kernel-bridge-encode`. The bridge's `canonicalText`
(`packages/core-wasm/src/engine-bridge.ts`) checks whether an answer is plain JSON data (finite
numbers, strings, booleans, `null`, and arrays and plain objects of them) and writes it with
`JSON.stringify` without building the canonical copy; anything else still goes through
`canonical()`. A test holds the two to the same text for 22 kinds of value, cycles included.
`core-wasm` 61 of 61 and 10 of 10. On the remote (R=10,
`results/2026-10-05T23-13-18-049Z-macmini2020-desktop`, A/A 62 of 63, no failed samples), against
the plugins tip before it (`adda0bbea`), `searchLarge` main-thread JavaScript drops 7.4% (864.8 to
801.0 ms) and its time 3.2%. `jump` JavaScript reads 8% lower (136.5 to 125.4 ms, interval 0.868
to 0.979), inside that row's noise band, so the report doesn't count it. No row is slower, and the
kernel is unchanged. It's merged into `research/multi-platform-rust-core-impl-plugins`.

### The PDF's bytes as a host handle

Item 8 is two commits on `research/rust-kernel-host-bytes`. No Rust call site had to change: the
kernel never reads an open source's bytes, it only passes the source to `engine.open`, and the
bridge's `decode` already turns a `$host` back into the value it holds. So the shim
(`packages/core-wasm/src/proxy.ts`) holds a document's `Uint8Array` in the bridge's host table and
sends `{"$host": n}` in its place, for bytes passed to `documents.open` or `openAll`
(`db0bc2fd8`) and for bytes a source function passed to them produces (`81899a80a`). The viewer
opens through such a function (`packages/viewer/main/src/kernel/config.ts:47`), so the first
commit alone moved nothing in the harness. The kernel releases the handle when `close()` drops the
session's record of its source: a `HostHold` in `crates/epdf-kernel/src/kernel.rs`, which a failed
tab's `retry()` carries to the next session so the bytes outlive it. Two test files never installed
the releaser `initWasm()` installs in the browser; they do now. Rust workspace 981 of 981,
`core-wasm` 61 of 61 and 13 of 13, three of them new.

On the remote (R=10, `results/2026-10-06T01-37-19-460Z-macmini2020-desktop`, A/A 62 of 63, no
failed samples), against the plugins tip before it (`7455a5294`): `open(ebook)` opens 15% sooner
(210.1 to 178.1 ms) and settles 8% sooner, now level with `main` (396.3 against 397.9 ms), and
`open(large)` opens 13% and settles 8% sooner. Main-thread JavaScript at open drops 12% and 6%.
The base64 copies are gone from the heap: 5.5 MiB less after opening `ebook.pdf`, 3.5 MiB after
`large.pdf`, 4.4 MiB less across the soak, whose slope stays at 0.02 MiB a cycle. One row is
slower: `zoomLarge` main-thread JavaScript, 1.063x [1.024, 1.088], with its settle times unmoved.
The zoom flow passes no bytes; we haven't looked into it. It's merged into
`research/multi-platform-rust-core-impl-plugins`.

### Page geometry in one pass

`selectLarge` failing under `slow` first looked like the geometry decode. `PageGeometryRun`'s
deserializer (`crates/plugins/selection/src/text_layout.rs`) built a `serde_json::Value` per run
to check for `rotation`, then converted that into the run. `e5e1c8d65` on `research/rust-kernel-geometry-decode` reads each
run in one pass through wire structs, and `crates/plugins/selection/tests/geometry_decode.rs` pins
it to the old decode's results, the answers it rejected included.

It was first measured on this machine (Apple M4 Max): a probe that hovers page 200 of `large.pdf`
and times until its geometry is usable, 5 runs per arm under the 4x throttle. The median drops from 229 ms (range 224 to 381) on the plugins tip (`a8252d0ea`) to 207 ms (202 to
333), about 20 ms. A drag with no pause after the hover still succeeded in 3 of 5 runs on both, so
it didn't close the race; the next section's fix did. It's merged into
`research/multi-platform-rust-core-impl-plugins` (`a14b6502d`) for the 20 ms. Rust workspace 996
of 996 on the merged tip.

On the remote since (R=10, `results/2026-10-08T14-34-05-698Z-macmini2020-desktop`, A/A 62 of 63, no
failed samples), the plugins tip with it (`faa2c6189`, arm `plugins-tip6`) against the tip before it
(`c0fcd2746`): `jump` main-thread JavaScript drops 11% (116.6 to 103.9 ms), `open(large)` 3.6%
(258.0 to 248.9 ms), and `search` 18% (8.2 to 6.8 ms). No row is slower. Against `main` the median
row goes from 1.112x to 1.097x latency and from 1.583x to 1.571x main-thread time.

### Store notifications, at most once a frame

Under the `slow` profile (4x CPU throttle on the main thread) the plugins tip (`a8252d0ea`) failed
`selectLarge` in 10 of 10 rounds (`results/2026-10-06T06-29-40-908Z-macmini2020-slow`). The drag
pressed on page 200 before that page's geometry answer had been processed, so `isOverText` was
false and no selection started. The geometry had been requested early. Its answer sat behind about
400 `page.measure.viewports` answers, one per page, which the measurement plugin asks for at
connect, as the TypeScript reference does. On the Rust kernel each answer is a task of its own,
each makes a store change, and each change ran every subscribed hook through the wasm boundary. The
page 200 answer was processed 2.2 to 2.6 s late. Section 2's fix had moved the flush into a
microtask, which batches a call's changes but not a run of tasks.

`c0fcd2746` on `research/rust-kernel-coalesce-notify` caps listeners at one notification per
16 ms (`packages/core-wasm/src/kernel.ts`). A change after a quiet frame still flushes in a
microtask; the frame's later changes flush once, by `setTimeout`, at its end. The caches still
invalidate on every change, so a read in between sees the new state. `core-wasm` 64 of 64 and 13
of 13, one test new.

On the remote, desktop (R=10, `results/2026-10-06T22-32-02-965Z-macmini2020-desktop`, A/A 63 of
63, no failed samples), against `a8252d0ea`: `open(large)` main-thread JavaScript drops 47% (488.1
to 259.4 ms) and settles 6.5% sooner, and `scroll` and `coldScroll` take 15% less wall time. Two
rows are slower: `select` main-thread task time 1.084x, and `searchLarge` JavaScript 1.033x. Under
`slow` (R=10, `results/2026-10-06T23-28-56-580Z-macmini2020-slow`, A/A 61 of 63), `selectLarge`
fails in no round. `scroll` takes 32% less wall time (4,190.8 to 2,857.9 ms), `coldScroll`
JavaScript drops 42% and its jank frames go from 2 to 0, and `searchLarge` takes 24% less time
(4,592.8 to 3,470.2 ms). `zoomLarge` settles 96.9 ms sooner. The cost is the frame's last change:
`scroll` and `coldScroll` settle 28.2 and 36.6 ms after the last wheel event (they settled at once
before), and `jump` settles 40.1 ms later [+1.0, +77.9]. Against `main` under `slow`, the median
row goes from 1.449x to 1.319x latency and from 2.322x to 1.986x main-thread time. It's merged into
`research/multi-platform-rust-core-impl-plugins`.

Flushing at the next animation frame instead of after a fixed 16 ms was meant to shorten that tail,
and did the reverse. `41714a1fc` kept the microtask for a change after a quiet frame and flushed the
rest in `requestAnimationFrame`, with a 100 ms timer for hidden tabs. Under `slow` (R=10,
`results/2026-10-08T00-30-29-463Z-macmini2020-slow`, A/A 62 of 63), against `c0fcd2746`, it was
slower on 26 rows and faster on none: `coldScroll` settles 46.5 ms later [+15.3, +78.7], `zoom`
55.7 ms later, `selectLarge` 31.3 ms later, and `zoomLarge` JavaScript is 1.108x. The hook pass
runs inside the frame, before its paint, where the fixed wait lets it run between frames. It was
dropped, and not run on desktop.

### Where the plugins tip goes now, and the shim's arguments

We profiled the plugins tip again after the fixes above (`faa2c6189`, arm `plugins-tip6`), on this
machine (Apple M4 Max), the same way as before: a kernel that keeps its names, byte-identical to
the shipped one once stripped (`5bf5fb28d0f2`), 5 runs per flow against 3 of `main`. `open(large)`
is no longer behind: 182 ms of main-thread JavaScript against `main`'s 220. `jump` is 58 against
30, spread over many small costs. `scroll` is the gap that is left, 354 against 208 ms. Of the 146
ms, about 80 ms is each call's arguments and result crossing the boundary as JSON: `canonical()`
encoding the arguments in the shim (17 ms), the result's `serde_json::Value` written as text in
`invoke_ref` (22 ms), `dlmalloc` mostly building and freeing those results (29 ms), and the shim
decoding the text (15 ms). Command resolution with its scripted predicates (item 12) is 24 ms, and
SipHash (item 13) is 10 ms.

`45c920b26` on `research/rust-kernel-plain-args` takes the first of those. The shim's `invoke`
(`packages/core-wasm/src/proxy.ts`) writes a call's arguments with `JSON.stringify` when they are
plain data, the check item 14 added to the engine bridge, now exported as `isPlainJson`, and builds
the canonical copy only otherwise. A test holds that plain values never reach one of the shim's
encode hooks, and that functions, signals, `Blob`s, bytes, `undefined`, `NaN` and dates do not
pass. `core-wasm` 65 of 65 and 13 of 13. On this machine `scroll` JavaScript drops 17 ms (364.6 to
347.5 ms, 8 runs each, interleaved).

On the remote (R=10, `results/2026-10-08T20-19-04-702Z-macmini2020-desktop`, A/A 61 of 63, no
failed samples), against `faa2c6189`: `coldScroll` main-thread JavaScript drops 4.4% (604.2 to
577.9 ms) and `open(ebook)` main-thread task time 3.8%. `scroll` JavaScript reads 3.5% lower (616.9
to 595.4 ms), with an interval of 0.929 to 1.002, so the report doesn't count it. No row is slower.
Against `main` the median row goes from 1.55x to 1.503x main-thread time; latency reads 1.063x and
1.073x, inside the noise between arms. The result's text (`invoke_ref`) and its allocations are
the larger part of the 80 ms, and need each plugin's harness `call` to write JSON without a
`Value`; that's not tried yet.

Most of the result's cost turned out to be a copy, not the writing. `outcome_json_raw`
(`crates/plugin-conformance/src/harness.rs`) wrapped each result as `json!({ "returned": value })`,
and `json!` copies an interpolated value by serializing it into a new `Value`: every `invoke_ref`
deep-copied its answer, printed the copy, and freed both. That was 20 ms of `scroll` in the
profile, 12.5 of it the copy. `34644a2b4` on `research/rust-kernel-result-text` adds
`outcome_text_raw`, which writes the wrapper and the value straight to text, and `invoke_ref`
(`crates/epdf-web/src/exports.rs`) uses it. A test holds its text to the old output for a returned
value, `undefined`, a throw and a promise. No plugin's harness changed. Rust workspace 997 of 997.
On this machine `scroll` JavaScript drops 32 ms (352.9 to 321.0 ms, 8 runs each, interleaved), more
than the copy alone: the allocator's share went with it.

On the remote (R=10, `results/2026-10-09T21-14-02-964Z-macmini2020-desktop`, A/A 62 of 63, no
failed samples), against `45c920b26`, 11 rows are faster and none slower. Main-thread JavaScript
drops 14% on `searchLarge` (816.0 to 698.8 ms), whose answers are the largest, 12% on `select`
(71.5 to 63.2 ms), 10% on `coldScroll` (584.7 to 527.0 ms), 8% on `scroll` (585.3 to 540.2 ms) and
7% on `zoomLarge` (210.0 to 195.7 ms). Against `main` the median row goes from 1.529x to 1.417x
main-thread time and from 1.081x to 1.034x latency. It's merged into
`research/multi-platform-rust-core-impl-plugins` (`910980671`). What is left of the round trip is
the plugins building their answers as `Value`s (about 8 ms) and the shim parsing the text (15 ms)
of `scroll`.

### `rustReads` with the Rust kernel

Item 16. The guard was a harness rule only: the plugins branch's engine reads `rustRouting`
whichever kernel runs the plugins (`packages/engine/main/src/index.ts:167-219`), and the plugins
arms' engine payload already carries the Rust core (`verify-rust-core` passes on it). `f0ca236d0`
drops the guard from `lib/config.mjs`, adds `scripts/verify-core.mjs`, which records a verified
core in a built worktree's stamp for the gate without rebuilding it, and adds the arm
`plugins-rustreads`: the `plugins-resulttext` build (`34644a2b4`) with `rustReads: true`, so only
the worker's geometry reads differ.

On the remote (R=10, `results/2026-10-12T01-08-19-312Z-macmini2020-desktop`, A/A 58 of 63, no
failed samples), against `plugins-resulttext`, 55 rows show no difference and 5 are unreliable.
`coldScroll`, the one flow that reads geometry for pages not seen before, takes 5.9% less wall time
(2,563.7 to 2,411.3 ms) and 3.5% less main-thread JavaScript (532.9 to 514.2 ms). `search` takes
10.5% longer (71.3 to 78.8 ms, interval 1.002 to 1.187), which we haven't looked into. `select`,
`selectLarge`, `jump` and `open(large)` don't move. As with `impl-ts` against `impl-rust`, geometry
is too small a share of the worker's time to show, so the plugins arms stay on `rustReads: false`.


### After the review fixes

The whole-branch review's fixes (`docs/research/rust-plugins-branch-review.md`, "Status") went into
`research/multi-platform-rust-core-impl-plugins` as one batch: 4 Highs, 29 Mediums and 63 Lows,
ending at `9d6c2eca1` (arm `plugins-tip7`). Several touch hot paths: promise turn counts (M1 to
M3), copy-on-write annotation and form models (M17, M21), selection page lookups (M22), action
listener types (L-K1), and the shim forgetting unused functions a task after a call (L-W2).

On the remote (R=10, `results/2026-10-13T02-28-33-711Z-macmini2020-desktop`, A/A 63 of 63, no
failed samples), against the tip before them (`34644a2b4`, arm `plugins-resulttext`), 61 rows show
no difference. `selectLarge` main-thread JavaScript drops 12% (79.9 to 70.6 ms), which fits M22.
`annotateLarge` reads 43.3 ms slower to settle [+5.0, +65.3], but that is noise: on this machine
the two arms settle at 153.5 and 153.3 ms (8 runs each, interleaved), and `plugins-resulttext`'s own
median on the remote was 245, 244 and 220 ms across three runs of the same build
(`2026-10-09T21-14-02-964Z`, `2026-10-12T01-08-19-312Z`, this one), while `main` and `main-aa`
differ by about 20 ms on the row here. Against `main`, main-thread time is 1.423x (1.433x before);
latency reads 1.088x against 1.042x, from rows that each moved inside their noise. The kernel wasm
grows 0.29 MiB (8.06 to 8.35), 0.04 MiB brotli.

### The tip after the catch-up, against upstream main

The plugins tip is now `2aaa8f841` (arm `plugins-next`), after the upstream catch-up. Upstream
`main` has moved to `ed064e0c8` (arm `main-now`), and `main` stays at `2516e2786` so earlier runs
keep their baseline. `plugins-next-lto` is the tip's kernel built with the `rust-kernel-lto`
recipe. The run is `results/2026-11-16T20-20-49-728Z-macmini2020-desktop`: R=10 on the Mac mini,
harness `92c3bb74e`, A/A 63 of 63, all five arms through the gate, 700 samples with none failed.

The tip against `9d6c2eca1` isn't measured. `plugins-base` is still pinned there in `arms.json`,
but its fork commit `7c7b600b1` has no prebuilt `wasm32-eh` archive and no published payload
(hash `e1b4027f945241e7`). A cold fork build failed in the arm64 Linux container: gclient asks
CIPD for `infra/rbe/client/linux-arm64`, which doesn't exist, and the fork's DEPS pins
`third_party/llvm-build` to a `Linux_x64` clang. The build needs an amd64 host.

Against `main-now`, the tip is slower on 44 rows, faster on 1, and no different on 18. The median
latency row is 1.239x and the median main-thread row 1.603x (`node scripts/summary.mjs results/<run>
main-now` prints them). Startup is the largest latency gap: `open(ebook) / readyMs` 52.8 to 109.9 ms
(2.081x), `openedMs` 201.3 to 288.6 ms (1.434x), `settledMs` 356.1 to 538.7 ms (1.513x), with
`open(large)` close behind. Main-thread JavaScript is 1.750x on `scroll` (859.6 to 1,504.5 ms),
1.751x on `coldScroll`, 1.803x on `jump` and 3.517x on `searchLarge` (360.6 to 1,268.1 ms), which is
the widest gap in the run and the first place to profile. `searchLarge / searchMs` itself shows no
difference (threshold ±3.0%). The settle rows that moved are 10.6 to 49.8 ms slower, except `zoom /
backTo1SettleMs`, 47.9 ms faster. Four of them, `zoom / settleMs` +47.0, `selectLarge / settleMs`
+32.4, `annotate / settleMs` +10.6 and `annotateLarge / settleMs` +15.6, are within how far the
unchanged `main` build moved between this run and 2026-10-13 (below). That earlier run recorded no
probe hash, and the plan rejects such a comparison, so it confirms nothing either way: these rows
stand as slower in this run, with no earlier run to check them against. The other 17
no-difference rows, with thresholds: `scroll` and `coldScroll` `settleMs` ±0.0, `wallMs` ±3.0%,
`frameP50Ms` ±3.0%, `frameP95Ms` ±3.8% and ±3.0%, `jankFrames` ±0, `longTasks` ±0 and `longTaskMs`
±0.0; `zoom / zoom4SettleMs` ±21.2; `zoomLarge / backTo1SettleMs` ±7.8; `select / settleMs` ±12.0.

Against `main`, the tip reads 1.246x latency and 1.859x main thread (46 slower, 1 faster, 16 no
difference). That isn't the port's cost alone: `main` against `main-now` is upstream's own movement
since `2516e2786`, 1.034x latency and 1.257x main thread (36 slower, 8 faster, 19 no difference).
Upstream got faster at reaching `readyMs` (0.825x on both fixtures), at `open(large)` main thread
(0.625x JavaScript) and at `search / searchMs` (81.7 to 40.7 ms, 0.497x). It got slower at opening
`ebook` (`openedMs` 1.475x), at `scroll` and `coldScroll` main thread (1.387x and 1.461x
JavaScript), at zoom settling (`zoom / settleMs` +153.0 ms) and at `searchLarge / searchMs` (1,886.8
to 2,231.9 ms, 1.183x). Its no-difference rows: `scroll` and `coldScroll` `settleMs` ±0.0,
`frameP50Ms` ±3.0%, `frameP95Ms` ±3.8% and ±3.0%, `jankFrames` ±0, `longTasks` ±0 and `longTaskMs`
±0.0; `zoomLarge / settleMs` ±26.3, `zoom2SettleMs` ±19.2 and `zoom4SettleMs` ±5.4; `select /
settleMs` ±12.0; `selectLarge / settleMs` ±13.8; `search / mainTaskMs` ±5.7%; `annotateLarge /
mainTaskMs` ±3.4%. Of upstream's moved settle rows, `zoomLarge / backTo1SettleMs` +54.3, `annotate /
settleMs` -11.9 and `annotateLarge / settleMs` -14.8 are within `main`'s move since 2026-10-13 and
`zoom / settleMs` +153.0 is not; that comparison is the rejected one above, so all four keep their
in-run verdicts.

The same `main` build (`2516e2786`, engine wasm `98c2368a774d` in both) ran on 2026-10-13
(`results/2026-10-13T02-28-33-711Z-macmini2020-desktop`). Between the two runs its median
latency row moved 1.014x and its median main-thread row 1.699x, from `node scripts/summary.mjs
results/2026-11-16T20-20-49-728Z-macmini2020-desktop --drift
results/2026-10-13T02-28-33-711Z-macmini2020-desktop main`; that command now exits 2, because the
2026-10-13 run recorded no probe hash, and the "latency row" it printed mixed frame intervals in
(caveats at the top). Its settle rows moved from -58.7 ms
(`zoom / zoom4SettleMs`) to +73.3 ms (`annotateLarge / settleMs`). The main-thread move is not
noise alone: the earlier run predates the probe correction (it records no `probeSha256`; this one
has `29ce19545e8b`), whose visibility sampling adds main-thread work, the Mac mini went from
Darwin 27.2.0 to 27.3.0, and the entry was rebuilt (`jsBytes` 3,536,548 to 3,536,649). So
main-thread numbers from before 2026-11-16 don't compare with these at all. For the record only,
the tip before the catch-up read 1.088x latency and 1.423x main thread against `main` on
2026-10-13.

LTO still helps only where the kernel starts. Against `plugins-next`, `plugins-next-lto` is
faster on 7 rows, slower on 2 and no different on 54; the median latency row is 1.000x and the
median main-thread row 0.988x. The gains are all in `open`: `open(ebook) / mainJsMs` 0.879x (363.3
to 319.5 ms), `settledMs` 0.915x, `firstPaintMs` 0.909x, `mainTaskMs` 0.904x, and on
`open(large)` `mainJsMs` 0.903x, `mainTaskMs` 0.922x and `firstPaintMs` 0.931x. `readyMs` is
slower on both fixtures (1.115x and 1.101x), most likely the larger module's compile: the kernel
wasm is 14.68 MiB against 10.35 MiB (2.69 against 2.44 MiB brotli). The engine wasm is the same
file in both arms. No-difference rows, with thresholds: `open(ebook) / openedMs` ±4.6%;
`open(large) / openedMs` ±3.0% and `settledMs` ±4.4%; on `scroll` and on `coldScroll`, `settleMs`
±0.0, `wallMs` ±3.0%, `frameP50Ms` ±3.0%, `frameP95Ms` ±3.8% and ±3.0%, `jankFrames` ±0,
`longTasks` ±0, `longTaskMs` ±0.0, `mainTaskMs` ±3.0% and `mainJsMs` ±3.0%; `jump` `settleMs`
±11.3, `mainTaskMs` ±6.0%, `mainJsMs` ±9.7%; `zoom` `settleMs` ±25.9, `zoom2SettleMs` ±7.0,
`zoom4SettleMs` ±21.2, `backTo1SettleMs` ±10.0, `mainTaskMs` and `mainJsMs` ±3.0%; `zoomLarge`
`settleMs` ±26.3, `zoom2SettleMs` ±19.2, `zoom4SettleMs` ±5.4, `backTo1SettleMs` ±7.8,
`mainTaskMs` and `mainJsMs` ±3.0%; `select` `settleMs` ±12.0, `mainTaskMs` ±5.3%, `mainJsMs`
±9.5%; `selectLarge` `settleMs` ±13.8, `mainTaskMs` ±9.6%, `mainJsMs` ±8.1%; `search`
`searchMs` ±3.0%, `mainTaskMs` ±5.7%, `mainJsMs` ±11.2%; `searchLarge` `searchMs`, `mainTaskMs`
and `mainJsMs` ±3.0%; `annotate` `settleMs` ±8.7, `mainTaskMs` ±3.3%, `mainJsMs` ±6.2%;
`annotateLarge` `settleMs` ±8.8, `mainTaskMs` ±3.4%, `mainJsMs` ±4.1%.

Memory moved with upstream as much as with the port. After opening `ebook` the page heap is
34.38 MiB on `main`, 50.72 MiB on `main-now` and 56.36 MiB on the tip; for `large` it is 48.19,
52.68 and 71.22 MiB. The worker heap is the same on `main-now` and the tip (27.72 and 27.70 MiB).
The kernel's `refs` table grows from 1,026 at soak cycle 5 to 1,526 at cycle 30 on both
`plugins-next` arms, while the heap slope stays at 0.03 MiB. We haven't checked
whether that predates the catch-up.

How the arms were driven and built. The catch-up renamed the viewer calls the entry makes, so
`92c3bb74e` adds `entry/api-v1.ts` and `entry/api-v2.ts` and picks one per arm from its stage
contract (`lib/api-generation.mjs`). `main` and `main-aa` ran v1; `main-now`, `plugins-next` and
`plugins-next-lto` ran v2. The same commit lets the gate read a selection's line breaks as one:
the catch-up's engine ends each selected line with ` \n` where `2516e2786` gives `\r\n`, and
otherwise the 150 characters match. The arms built in the arm64 Linux devcontainer. There,
`packages/engine/runtime/build/generate-functions.mjs` parses the fork headers with the host
`clang` and no target, so `uint64_t` reads as `unsigned long` and is bound as `i32`; the ABI check
then fails on `EPDFAttachment_ExtractFile` and `EPDFAttachment_ExtractFileToOwnedBuffer` (5
arguments against 6). The build used a PATH shim that retargets only that header dump to
`wasm32-unknown-emscripten`. With it the generator reproduces the tracked
`pdf-functions.generated.ts` byte for byte and all 1,061 bindings pass. The plugins arms link
fork `17d8073d4`'s released `wasm32-eh` archive (`libembedpdf.a` sha256 `315bb221...`) rather than
a local fork build.

### Annotation and form flows: the baseline for Phases C and D

The Rust kernel's cost is widest on the new annotation and form flows. Against `main-now`, the tip
spends 2.271x to 4.009x the main-thread JavaScript on `hover`, `dragAnnot`, `drawInk`, `typeField`
and `undo`, and each of them settles later. The only older row that compares is `searchLarge`
(3.508x). This run is the baseline that Phases C and D measure against:
`results/2026-11-17T02-25-30-951Z-macmini2020-desktop`, R=10 on the Mac mini, harness `cb3167fcd`,
A/A 84 of 84, all four arms through the gate, 780 samples, none failed.

The `plugins-next` here is `2aaa8f841` plus `0de4effd9` ("fix(epdf-web): forward allowsField to the
bridge so form widgets take input", on `research/perf-fixes`). Before that commit, the Rust web
host's `JsSecurity` had no `allows_field`, so it fell back to the trait default in
`crates/epdf-kernel/src/engine.rs`, which answers `false`. Every form widget was a disabled input,
and `typeField` couldn't type on the tip. The engine wasm is the same file with and without the fix
(`2ecd0bd98574`). `main` and `main-aa` are `2516e2786` and ran the v1 viewer API; `main-now`
(`ed064e0c8`) and `plugins-next` ran v2. The new flows go through the same adapter
(`entry/api-v1.ts`, `entry/api-v2.ts`). `plugins-base` isn't in the run, since `9d6c2eca1` can't be
built in the arm64 container (above).

The fixtures are `annots.pdf` (`f27197ce9ac9`), which is `large.pdf` with 1,000 highlights, 150 ink
drawings and 50 stamps, and `forms.pdf` (`b973ccec7bf6`), which has 1,000 text and checkbox fields
over 100 pages. Both come from `fixtures/generate.mjs`. `test/fixtures.selftest.mjs` checks that
`main`'s TypeScript engine lists all of them, and the gate compares the two totals on every arm.

Two flows don't measure what their names first suggest. `dragAnnot` drags a stamp 200 px, not a
highlight: neither viewer API generation moves a highlight (it's selectable but not movable,
`packages/plugin/annotation/src/read/chrome.ts`). `undo` needs a history plugin, which `2516e2786`
doesn't have, so `main` and `main-aa` skip it. The gate compared its answer with `plugins-next`'s,
not `main-now`'s: the gate at `cb3167fcd` took the first arm in run order that answered, and
`arms.json` lists `plugins-next` before `main-now`. Both answered `countBefore` 2, `countEdited`
12, `countAfter` 2 (`gate.json`), so the answers agree. Since then the gate takes this reference
from `arms.json`'s `fallbackReference` (`main-now`) whenever `main-now` is in the run, and falls
back to run order only without it, with a `gate:` warning naming the arm used. When only one arm
answers `undo`, nothing checks it. The default arms would be that case, because `plugins-base`
(`9d6c2eca1`) is viewer API v1 and would skip `undo`. No run has had `plugins-base` in it, since it
can't be built here (above), so this is the rule `test/gate.test.mjs` checks, not something a run
showed. The gate then records `undo` as unchecked
in that arm's `gate.json` entry, warns that no comparison happened, and the report leaves out that
arm's `undo` rows.
`undo` has no `main` baseline and no A/A noise, so its threshold is the floor: ±6.1 ms on
`settleMs`, ±3.0% on the CPU rows.

The tip against `main-now` (`node scripts/summary.mjs <run> main-now`): 63 rows slower, 1 faster,
23 no difference. The median latency row is 1.065x and the median main-thread row 1.817x. This pair
has 87 rows here against 63 in the run above, so its medians don't compare with that run's 1.239x
and 1.603x. The new flows, `main-now` to `plugins-next`, medians:

| Flow | `mainJsMs` | `settleMs` |
| --- | --- | --- |
| `hover` (20 highlights) | 508.3 to 1,205.6 (2.372x) | 1,322.4 to 1,666.3 (+343.9) |
| `dragAnnot` (one stamp) | 237.8 to 712.9 (2.998x) | 453.0 to 934.3 (+481.3) |
| `drawInk` (3 strokes of 60 samples) | 1,345.3 to 4,014.1 (2.984x) | 3,947.8 to 6,989.1 (+3,041.4) |
| `typeField` (40 characters) | 138.5 to 314.5 (2.271x) | 152.2 to 289.9 (+137.7) |
| `undo` (10 steps) | 157.9 to 633.1 (4.009x) | 202.5 to 846.7 (+644.3) |
| `scrollMid` (60 wheels from page 300) | 938.5 to 1,800.3 (1.918x) | 0.0 to 0.0 (no difference) |

`drawInk`'s settle includes the ink tool's 800 ms stroke grouping on both arms. `scrollMid` also
reads `wallMs` 1.065x and `frameP95Ms` 1.046x. Against `scroll` from page 0 (`mainJsMs` 1.741x),
the tip loses more in the middle of the document. The one faster row is `zoom / backTo1SettleMs`,
-58.3 ms. The 23 no-difference rows, with thresholds: `scroll` `settleMs` ±0.0, `wallMs` ±3.0%,
`frameP50Ms` ±3.0%, `frameP95Ms` ±3.0%, `jankFrames` ±0, `longTasks` ±0, `longTaskMs` ±0.0;
`scrollMid` `settleMs` ±0.0, `frameP50Ms` ±3.0%, `jankFrames` ±0, `longTasks` ±0, `longTaskMs` ±0.0;
`coldScroll` `settleMs` ±0.0, `wallMs` ±3.0%, `frameP50Ms` ±3.0%, `jankFrames` ±0, `longTasks` ±0,
`longTaskMs` ±0.0; `zoom / zoom4SettleMs` ±26.1; `zoomLarge / backTo1SettleMs` ±10.2; `select /
settleMs` ±12.7; `searchLarge / searchMs` ±3.0%; `annotate / settleMs` ±8.6.

Upstream moved on the new flows too. `main` against `main-now` is 46 slower, 12 faster and 26 no
difference, with medians of 1.015x latency and 1.325x main thread. On the new flows, `main-now`'s
JavaScript is 1.312x on `hover`, 1.670x on `dragAnnot` (settle +42.3 ms), 1.695x on `drawInk`
(settle no difference, ±116.6) and 1.501x on `scrollMid`. On `typeField` it's faster: 0.956x
JavaScript and -23.4 ms settle. Against `main`, the tip reads 1.115x latency and 2.178x main thread
(63 slower, 1 faster, 20 no difference).

`main` held still since the run above. Same commit, engine wasm and probe; median latency 0.999x and
main thread 0.994x (`node scripts/summary.mjs <run> --drift
results/2026-11-16T20-20-49-728Z-macmini2020-desktop main`). Its settle rows moved by -3.6 to
+10.7 ms, and no moved row of either pair is within that drift, so none is unconfirmed this time.

### Phases C and D on `research/perf-fixes`

One run measures both phases: `results/2026-11-17T04-18-50-584Z-macmini2020-desktop`, R=10 on the
Mac mini, 130.1 min, harness `7e527a415` with the `arms.json` of `a4a680e29` uncommitted. A/A 84
of 84, all six arms through the gate, 1,180 samples, none failed. The arms, all tagged `arm/<id>`
in the host repository:

| Arm | Commit | What it is |
| --- | --- | --- |
| `main`, `main-aa` | `2516e2786` | A/A pair, viewer API v1 |
| `main-now` | `ed064e0c8` | upstream TypeScript reference, v2 |
| `phase-c-base` | `0de4effd9` | the baseline run's `plugins-next`, same worktree and build |
| `phase-c-end` | `af9ff5d2b` | Phase C: Tasks 6 to 12, 30 and 12a |
| `phase-d-end` | `0d18fb177` | Phase D: Tasks 13 to 17 |

All three Rust arms run the same engine wasm (`2ecd0bd98574`, fork `17d8073d4`). The kernel wasm
grew from 10.35 MiB to 10.70 MiB in Phase C and to 10.73 MiB in Phase D (2.44 to 2.50 MiB brotli).

`main-now` held still since the baseline run: same commit, engine wasm and probe, median latency
1.001x and main thread 0.998x, settle rows -9.0 to +4.6 ms (`node scripts/summary.mjs <run> --drift
results/2026-11-17T02-25-30-951Z-macmini2020-desktop main-now`). No moved row of any pair falls
inside that drift, so none is unconfirmed.

`undo` still has no `main` baseline and judges against the 3% floor. Its gate reference was
`main-now`, because `main-now` came first in `arms.json` order among the arms that ran it in this
run (`run.mjs` sorts `--arms` into that order). `dragAnnot` still drags a stamp, not a highlight.
Task 12 (`efac793af`) changed the sample a live harness hands interaction handlers: the typed
sample, with members in the generator's order (`page` last), instead of the host's raw JSON. The
handlers read only contract members, and every gate answer matched, so the flows measure the same
work.

#### Phase C: every watched flow got faster, and nothing is slower

`phase-c-base` against `phase-c-end`: 50 rows faster, 37 no difference, none slower. Median latency
0.987x, median main thread 0.835x. The watched rows:

| Row | `phase-c-base` | `phase-c-end` | Change |
| --- | --- | --- | --- |
| `jump / mainJsMs` | 235.6 | 175.1 | 0.743x |
| `jump / settleMs` | 164.3 | 144.5 | -19.8 ms |
| `selectLarge / mainJsMs` | 184.2 | 152.1 | 0.826x |
| `selectLarge / settleMs` | 312.1 | 284.2 | -27.9 ms |
| `scroll / mainJsMs` | 1,529.5 | 1,225.2 | 0.801x |
| `scrollMid / mainJsMs` | 1,804.2 | 1,287.2 | 0.713x |
| `scrollMid / wallMs` | 3,352.3 | 3,063.5 | 0.914x |
| `zoomLarge / mainJsMs` | 566.1 | 518.7 | 0.916x |
| `zoomLarge / settleMs` | 372.0 | 345.0 | -27.0 ms |
| `hover / mainJsMs` | 1,199.9 | 886.6 | 0.739x |
| `hover / settleMs` | 1,663.6 | 1,347.0 | -316.7 ms |
| `searchLarge / mainJsMs` | 1,270.6 | 911.0 | 0.717x |
| `searchLarge / searchMs` | 2,188.8 | 1,924.3 | 0.879x |

The watched rows that didn't move: `scroll / wallMs` (0.983x, ±3.0%), the frame, jank and
long-task rows of `scroll` and `scrollMid`, `zoomLarge / zoom4SettleMs` (-8.9 ms, ±5.3) and
`zoomLarge / backTo1SettleMs` (+9.3 ms, ±9.9). The largest moves are off the watch list:
`drawInk` main-thread JavaScript 0.626x and settle -2,824.2 ms (6,971.2 to 4,147.0), and `undo`
0.762x and -149.1 ms. `annotateLarge`, also off the list, got faster too: main-thread JavaScript
0.860x and settle -26.1 ms.

#### Phase D: the form and undo rows moved, `annotateLarge` didn't

`phase-c-end` against `phase-d-end`: 13 rows faster, 74 no difference, none slower. Median latency
1.002x, median main thread 1.004x, because Phase D touches only the annotation, form and history
flows. The watched rows:

| Row | `phase-c-end` | `phase-d-end` | Change |
| --- | --- | --- | --- |
| `undo / mainJsMs` | 478.9 | 315.0 | 0.658x |
| `undo / settleMs` | 693.2 | 407.0 | -286.3 ms |
| `typeField / mainJsMs` | 297.4 | 246.7 | 0.829x |
| `typeField / settleMs` | 271.4 | 240.1 | -31.3 ms |
| `dragAnnot / mainJsMs` | 583.2 | 550.1 | 0.943x |
| `dragAnnot / settleMs` | 870.2 | 843.5 | -26.6 ms |
| `drawInk / mainJsMs` | 2,508.5 | 2,363.4 | 0.942x |
| `hover / mainJsMs` | 886.6 | 844.7 | 0.953x |

No difference on `hover / settleMs` (-6.1 ms, ±40.4) or any `annotateLarge` row. `drawInk /
settleMs` (-109.2 ms, ±124.4) is no difference by the harness rule, but its whole interval is below
zero ([-178.6, -66.0]), so it leans faster. The `annotateLarge` rows: `settleMs` -1.1 ms (±8.2),
`mainJsMs` 0.993x (±4.8%), `mainTaskMs` 1.021x (±5.1%).

#### Where the fix branch stands against upstream

`main-now` against `phase-d-end`: 54 slower, 31 no difference, 2 faster. Median latency 1.000x,
median main thread 1.427x. The same pair with the baseline's tip read 63 slower, 1 faster and 23 no
difference, at 1.065x and 1.817x. That comparison crosses runs, which the report doesn't support,
but `main-now`'s drift between the two runs is under 0.3% on both medians. The new flows'
`mainJsMs` against `main-now`, then and now:

| Flow | Baseline tip | `phase-d-end` |
| --- | --- | --- |
| `hover` | 2.372x | 1.664x |
| `dragAnnot` | 2.998x | 2.355x |
| `drawInk` | 2.984x | 1.766x |
| `typeField` | 2.271x | 1.772x |
| `undo` | 4.009x | 2.039x |
| `scrollMid` | 1.918x | 1.383x |
| `searchLarge` | 3.508x | 2.533x |

The two faster rows are `searchLarge / searchMs` (0.861x, 2,235.4 to 1,923.6 ms) and
`zoom / backTo1SettleMs` (-46.3 ms). The widest remaining gaps are `searchLarge / mainTaskMs`
2.831x, `searchLarge / mainJsMs` 2.533x, `dragAnnot / mainJsMs` 2.355x, `search / mainJsMs` 2.234x and the `open` main-thread rows
at 1.850x to 2.061x, with `open(*) / readyMs` at 2.092x and 2.124x.

### Phase E: PDFium call counts

Phase E cut the PDFium calls on these host operations, but not all of them. Opening no longer
loads every page (`LoadPage` 400 to 0). The first search for a needle no page holds (`zqxjv`) still
loads every page and its text: Phase E halved its `LoadPage` (800 to 400) and left its
`TextLoadPage` at 400. The same search again drops `TextLoadPage` from 400 to 0, and a search for a
needle on one page (`Page 200 line`) from 401 to 1. The two writes read fewer annotations
(`GetAnnotRaw` 5 to 1 and 13 to 5). These are call counts, not timings: the `call-count` feature of
the `pdfium` crate counts the six counters in the table (`LoadPage`, `FormLoadModel`, `GetAnnotRaw`,
`GetAnnotIndexRaw`, `TextLoadPage`, `SigLoadModel`), not every PDFium call. The native engine timer
below times the perf-fixes tip, Phase E included.

The counts come from the ignored test `hot_host_calls` in `crates/epdf-engine/tests/call_counts.rs`,
committed as `fa3c0d3fb` on `research/perf-fixes`. It ran at `0d18fb177`, the last commit before
Phase E, and at `82cfb6e20`, Phase E's head:
`cargo test -p epdf-engine --test call_counts -- --ignored --nocapture`. Both builds link the same
`libembedpdf.so`. At `0d18fb177` the test needed two local edits to compile: it drops the
`GetAnnotIndexRaw` counter, and `crates/epdf-engine/Cargo.toml` adds the `pdfium` dev-dependency
with `features = ["call-count"]`. Neither edit was committed.

The test builds its own file in memory (1,402,476 bytes): 400 US Letter pages of 40 Courier lines,
each page with three highlights and one text field in a single `/AcroForm`. The writes target page
index 200: `forms.setValue` sets its field, and `annotations.update` patches `contents` on its
second highlight. Counters reset before each operation, and each search runs every slice to the
end (2 slices). Before to after:

| Operation | `LoadPage` | `FormLoadModel` | `GetAnnotRaw` | `GetAnnotIndexRaw` | `TextLoadPage` | `SigLoadModel` |
| --- | --- | --- | --- | --- | --- | --- |
| `open.fatMem` | 400 to 0 | 0 to 0 | 0 to 0 | n/a to 0 | 0 to 0 | 1 to 1 |
| `pages.list` | 0 to 0 | 0 to 0 | 0 to 0 | n/a to 0 | 0 to 0 | 0 to 0 |
| `search.query` (miss) | 800 to 400 | 0 to 0 | 0 to 0 | n/a to 0 | 400 to 400 | 0 to 0 |
| `search.query` (repeat) | 0 to 0 | 0 to 0 | 0 to 0 | n/a to 0 | 400 to 0 | 0 to 0 |
| `search.query` (1 page) | 0 to 0 | 0 to 0 | 0 to 0 | n/a to 0 | 401 to 1 | 0 to 0 |
| `forms.setValue` | 0 to 0 | 4 to 2 | 5 to 1 | n/a to 1 | 0 to 0 | 1 to 1 |
| `annotations.update` | 0 to 0 | 0 to 0 | 13 to 5 | n/a to 4 | 0 to 0 | 0 to 0 |

The miss searches for "zqxjv", which no page holds; the repeat searches for it again; the one-page
search is "Page 200 line" (40 matches). `GetAnnotIndexRaw` reads n/a before because the counter, and
the index lookup it counts, arrived inside Phase E (`ef17b2441`).

### Native engine timer: the Task 3 baseline (`plugins-next` at 0de4effd9) against the perf-fixes tip

The perf-fixes tip is faster than the Task 3 baseline on 15 of 18 native rows and slower on none.
Measured on the Mac mini, 10 rounds. Opening `large.pdf` takes 0.163x the time (20.7 ms to 3.37
ms). A whole-document search for `the` takes 0.472x (818.4 ms to 386.0 ms on `large.pdf`).
`pages.list` and `pages.geometry` are about 0.6x on every fixture, and `forms.setValue` is 0.638x
(40.5 ms to 25.9 ms). Opening `forms.pdf` is 0.959x (21.5 ms to 20.6 ms), the smallest gain past
its threshold. Rendering doesn't move on `large.pdf` or `annots.pdf` (0.983, 0.984, inside
the 3% floor). It is 0.927x on `forms.pdf`. `annotations.list` on `forms.pdf` doesn't move. Every
A/A pair passed, the largest A/A distance from 1.0 being 1.2%. Every threshold is the 3% floor
except `forms.setValue`'s, which is 3.1%.

10 rounds, ms per call, median [max] over rounds; ratio is tip / base

| row | base | base-aa | tip | A/A ratio | ratio [95% CI] | threshold | verdict |
| --- | --- | --- | --- | --- | --- | --- | --- |
| large/open | 20.7 [21.1] | 20.5 [21.1] | 3.37 [3.43] | 0.990 | 0.163 [0.162, 0.165] | 3.0% | faster |
| large/pages.list | 5.08 [5.17] | 5.08 [5.17] | 3.05 [3.09] | 0.999 | 0.600 [0.590, 0.604] | 3.0% | faster |
| large/pages.geometry | 7.02 [7.12] | 6.99 [7.12] | 4.13 [4.17] | 0.995 | 0.588 [0.578, 0.595] | 3.0% | faster |
| large/pages.render | 0.627 [0.639] | 0.626 [0.628] | 0.617 [0.619] | 0.999 | 0.983 [0.981, 0.987] | 3.0% | no detectable difference |
| large/search.query | 818.4 [835.1] | 817.2 [832.2] | 386.0 [391.2] | 0.999 | 0.472 [0.464, 0.476] | 3.0% | faster |
| large/annotations.list | 0.319 [0.325] | 0.317 [0.325] | 0.218 [0.226] | 0.994 | 0.684 [0.671, 0.697] | 3.0% | faster |
| annots/open | 36.8 [37.6] | 36.7 [37.5] | 19.8 [20.0] | 0.996 | 0.539 [0.526, 0.544] | 3.0% | faster |
| annots/pages.list | 5.10 [5.19] | 5.09 [5.18] | 3.08 [3.12] | 0.998 | 0.605 [0.591, 0.611] | 3.0% | faster |
| annots/pages.geometry | 7.01 [7.10] | 6.97 [7.10] | 4.12 [4.16] | 0.993 | 0.587 [0.578, 0.595] | 3.0% | faster |
| annots/pages.render | 0.744 [0.746] | 0.744 [0.746] | 0.731 [0.734] | 1.001 | 0.984 [0.981, 0.986] | 3.0% | no detectable difference |
| annots/search.query | 819.3 [834.6] | 817.2 [832.1] | 385.8 [391.0] | 0.997 | 0.471 [0.461, 0.476] | 3.0% | faster |
| annots/annotations.list | 44.8 [45.5] | 44.8 [45.6] | 36.5 [36.8] | 0.999 | 0.814 [0.798, 0.822] | 3.0% | faster |
| forms/open | 21.5 [21.8] | 21.2 [21.7] | 20.6 [20.9] | 0.988 | 0.959 [0.942, 0.971] | 3.0% | faster |
| forms/pages.list | 1.22 [1.24] | 1.21 [1.23] | 0.708 [0.717] | 0.989 | 0.579 [0.567, 0.590] | 3.0% | faster |
| forms/pages.geometry | 0.306 [0.309] | 0.303 [0.309] | 0.182 [0.185] | 0.991 | 0.597 [0.586, 0.609] | 3.0% | faster |
| forms/pages.render | 0.148 [0.149] | 0.147 [0.148] | 0.137 [0.139] | 0.995 | 0.927 [0.924, 0.931] | 3.0% | faster |
| forms/annotations.list | 1.25 [1.28] | 1.25 [1.27] | 1.23 [1.26] | 0.998 | 0.986 [0.971, 0.997] | 3.0% | no detectable difference |
| forms/forms.setValue | 40.5 [41.4] | 40.2 [40.6] | 25.9 [26.3] | 0.993 | 0.638 [0.623, 0.648] | 3.1% | faster |

Results: the `compare.mjs --out` JSON of the run started 2026-11-17T07:55:56.639Z (the
`tooling/engine-timer` README's command, at `a5f7f59e3`), run 2026-11-17 07:55 to 08:05 UTC on
`macmini2020` (Apple M1, AC power, load average 1.54 before and 1.65 after). The JSON, with its raw
per-round samples, and the run's logs aren't committed.

The timer is `tooling/engine-timer` (b3f41cd38, 93342cdf6 on `research/perf-fixes`). It sends
`open.fatMem`, `pages.list`, `pages.geometry`, `pages.render`, a whole-document `search.query`,
`annotations.list` and `forms.setValue` to `Host::call` on `large.pdf`, `annots.pdf` and
`forms.pdf`, after each build passes the answer check against `tooling/engine-conformance/vectors`.
Every round runs A, a byte-identical A/A copy and B in rotating order, and the thresholds come from
the harness's own `stats.mjs`. Geometry and render read a different page on every call. A search
row is the summed time of the whole cursor chain.

Arms: `base` is 0de4effd9 with the timer source copied in uncommitted (the crate didn't exist
there), `tip` is 93342cdf6, whose engine is the same as 5617106e5. Both link the fork's static
`darwin-static-arm64` archive for runtime 17d8073d4 (`libembedpdf.a` sha256
`09a082d8226133e55dd5416f6a72b3ad9669bf6960c27d21c198e36d2c2e9cd2`), so PDFium is the same bytes
on both sides. Both are Cargo `bin` builds, so both get `-C lto`. Binary sha256: base
`179c123f029bc47144239ba2c6ffb3b388419bd6336b0954143939188a6c6a60`, tip
`6d5714a9bdf0d601886d5030f8f27fb4fc849a9631601119ca6aec5e4405264b`.

The two builds' answers agree at the corpus quantum, not byte for byte. 0de4effd9 prints
`annotations[348].rect.width` on `annots.pdf` as `310.2000122070313` where the tip prints
`310.20001220703125`, and some `pages.geometry` glyph widths differ in their last bits the same way.
The vectors allow that (1e-4 point), so the timer compares quantized answers (93342cdf6).

### LTO on the native archives

LTO stays, on a small effect from one run. Built with `-C lto`, the timer is faster past
threshold on 4 of 18 rows and slower on none. `pages.geometry` is 0.968 on `large.pdf`, 0.965 on
`annots.pdf` and 0.964 on `forms.pdf`, and `annotations.list` on `forms.pdf` is 0.969. The other 14
rows moved 2.9% or less either way (`forms/pages.render` is 1.007), inside the floor. The plan's
Task 28, Step 4, keeps LTO when a row moves past its threshold, so `build-xcframework.sh` now builds
`epdf-viewer-host` with `cargo rustc --crate-type staticlib`, and the C# README documents `cargo
rustc -p epdf-capi --release --crate-type cdylib` (66a459832).

Read the four rows with care. They clear the 3% floor by 0.10 to 0.64 points (improvements of
3.10% to 3.64%). The LTO binary is the same file as the tip in the run before, and its medians
moved between the two runs. On these four rows the drift was 0.52% to 0.76%, at or above each
row's margin past the floor. The largest drift across all 18 rows was 1.70% (`large/open` 3.37 ms,
then 3.43 ms), about half of each row's whole improvement. The ratios come from a single nolto/lto
run, and the passing within-run A/A pairs are what make this run's verdicts valid. The link time
and the archive sizes, with and without LTO, weren't measured.

10 rounds, ms per call, median [max] over rounds; ratio is lto / nolto

| row | nolto | nolto-aa | lto | A/A ratio | ratio [95% CI] | threshold | verdict |
| --- | --- | --- | --- | --- | --- | --- | --- |
| large/open | 3.43 [3.46] | 3.44 [3.50] | 3.43 [3.44] | 1.003 | 0.998 [0.994, 1.001] | 3.0% | no detectable difference |
| large/pages.list | 3.15 [3.16] | 3.15 [3.16] | 3.09 [3.11] | 1.000 | 0.982 [0.979, 0.987] | 3.0% | no detectable difference |
| large/pages.geometry | 4.30 [4.32] | 4.31 [4.32] | 4.16 [4.17] | 1.003 | 0.968 [0.960, 0.973] | 3.0% | faster |
| large/pages.render | 0.618 [0.620] | 0.618 [0.620] | 0.616 [0.617] | 0.999 | 0.996 [0.993, 0.998] | 3.0% | no detectable difference |
| large/search.query | 399.1 [399.9] | 398.6 [399.3] | 387.6 [389.6] | 0.999 | 0.971 [0.963, 0.975] | 3.0% | no detectable difference |
| large/annotations.list | 0.228 [0.231] | 0.228 [0.232] | 0.222 [0.224] | 0.999 | 0.972 [0.958, 0.980] | 3.0% | no detectable difference |
| annots/open | 20.2 [20.5] | 20.2 [20.5] | 20.0 [20.0] | 1.002 | 0.990 [0.973, 0.997] | 3.0% | no detectable difference |
| annots/pages.list | 3.15 [3.16] | 3.16 [3.16] | 3.10 [3.12] | 1.001 | 0.984 [0.977, 0.987] | 3.0% | no detectable difference |
| annots/pages.geometry | 4.30 [4.30] | 4.30 [4.31] | 4.15 [4.17] | 1.001 | 0.965 [0.953, 0.970] | 3.0% | faster |
| annots/pages.render | 0.733 [0.737] | 0.733 [0.736] | 0.733 [0.735] | 1.001 | 1.001 [0.997, 1.004] | 3.0% | no detectable difference |
| annots/search.query | 398.8 [399.8] | 398.1 [399.3] | 387.7 [389.0] | 0.998 | 0.972 [0.964, 0.976] | 3.0% | no detectable difference |
| annots/annotations.list | 36.9 [37.2] | 37.0 [37.2] | 36.1 [36.8] | 1.002 | 0.979 [0.971, 0.995] | 3.0% | no detectable difference |
| forms/open | 21.1 [21.3] | 21.0 [21.2] | 20.8 [21.0] | 0.998 | 0.988 [0.979, 0.995] | 3.0% | no detectable difference |
| forms/pages.list | 0.726 [0.730] | 0.726 [0.730] | 0.713 [0.717] | 0.999 | 0.982 [0.976, 0.986] | 3.0% | no detectable difference |
| forms/pages.geometry | 0.191 [0.191] | 0.191 [0.192] | 0.184 [0.185] | 1.000 | 0.964 [0.957, 0.968] | 3.0% | faster |
| forms/pages.render | 0.137 [0.138] | 0.137 [0.138] | 0.138 [0.139] | 1.002 | 1.007 [1.003, 1.012] | 3.0% | no detectable difference |
| forms/annotations.list | 1.27 [1.29] | 1.27 [1.29] | 1.23 [1.25] | 1.006 | 0.969 [0.959, 0.976] | 3.0% | faster |
| forms/forms.setValue | 26.4 [27.3] | 26.3 [27.3] | 26.0 [26.5] | 0.996 | 0.984 [0.958, 0.999] | 3.7% | no detectable difference |

Results: the `compare.mjs --out` JSON of the run started 2026-11-17T08:05:43.158Z, not committed,
run 2026-11-17 08:05 to 08:13 UTC on the same machine (load average 1.65 before and 1.90 after).
Every A/A pair passed, the largest A/A distance 0.6%.

The timer can't link the staticlib, so the arms are the timer itself. `lto` is the tip binary
above (`-C lto` on its final `rustc` line). `nolto` is the same commit built with
`CARGO_PROFILE_RELEASE_LTO=false`, which passes no `-C lto`, as `cargo build` did for the shipped
archive (sha256 `d8f472c9721b71811ee86150de079e7feba4c7ced166a2d3c6f850178cf45a26`). Rust LTO is
the same in both places. The archive's Swift and uniffi boundary isn't in the timer.

`-C lto` now reaches both archives. `cargo rustc -p epdf-viewer-host --target aarch64-apple-darwin
--profile release --crate-type staticlib -v` runs `rustc --crate-name epdf_viewer_host ...
--crate-type staticlib --emit=dep-info,link -C opt-level=3 -C lto -C codegen-units=1`.
`cargo rustc -p epdf-capi --release --crate-type cdylib -v` runs `--crate-type cdylib ...
-C opt-level=3 -C lto -C codegen-units=1`. Before the change, `cargo build -p epdf-capi
--release -v` ran `--crate-type cdylib --crate-type rlib ... -C opt-level=3 -C codegen-units=1`
with no `-C lto`. These three `-v` logs aren't committed; the commands rerun them.

The Swift suite passes on the LTO archive. `build-xcframework.sh` with the change, then
`EMBEDPDF_LOCAL_ENGINE=1 swift test` in `platforms/apple/EmbedPDF`, ran 506 tests in 51 suites,
71 in 14 and 8 XCTest cases, all passing. The test log isn't committed.

### The fix branch under the corrected harness (2026-11-17)

This run replaces the CPU and settle claims of the three runs in the caveats at the top
(`20-20-49`, `02-25-30` and `04-18-50`). Their numbers stay as recorded. Phase C is still the large
win: main-thread median 0.826x, every `mainJsMs` row on the Phase C watch list faster except
`selectLarge`, whose A/A band is ±19.3%. Phase D is smaller than first recorded, and Phase E moved
nothing. The tip still spends 1.468x the main-thread time of upstream `main`.

Run `results/2026-11-17T16-59-17-202Z-macmini2020-desktop`: R=10 on the Mac mini, 150.2 min,
harness `5f6b0eca5` (clean), probe `d3edaa255947`. A/A 83 of 84. The one failure is
`search / searchMs` (1.036x, [1.002, 1.168]), so that row reads "unreliable" in every pair. All
seven arms passed the gate, and none of the 1,380 samples failed. `main` and `main-aa` skipped
`undo`. The new arm is `perf-fixes-end`, `a6d4d5d14` (the `research/perf-fixes` tip with Phase E and
the final fixes, tag `arm/perf-fixes-end`). The branch has since moved to `2da733fc8`, with review
fixes to cancellation, Swift and timers and no web-path perf change intended. That commit isn't
measured. `perf-fixes-end` ships a different engine wasm (`172b5a139647`, 14.15 MiB) from the three
phase arms (`2ecd0bd98574`, 14.26 MiB). The kernel is 10.73 MiB on both `phase-d-end` and
`perf-fixes-end`.

This run is the drift reference for the next run at the same frame interval. Its probe hash differs
from every earlier run's, and so does its frame interval: `main`'s median `scroll / frameP50Ms` is
11.3 ms here and 16.7 ms in `04-18-50`, `02-25-30` and `20-20-49`. `summary.mjs --drift` against
`04-18-50` exits 2 on both ("probe hash 29ce19545e8b -> d3edaa255947; frame interval 16.7 -> 11.3
ms, more than 1 ms apart"), and no row here has a same-build check yet. A next run at 16.7 ms
won't compare with this one either. Medians below come from `node scripts/summary.mjs`, which
now reports timing, frame-interval, main-thread and settle rows apart. `hover` rows end on
`getHovered()` on every v2 arm and carry "(no done condition on `main`)" against `main`. `undo`
still has no A/A band, and its gate reference is `main-now`, the first arm in `arms.json` order
that runs it. `dragAnnot` drags a stamp.

#### Phase C (`phase-c-base` 0de4effd9 to `phase-c-end` af9ff5d2b)

39 rows faster (3 of them `undo`, with no A/A band), 47 no difference, none slower. Timing median
0.995x, frame interval 0.999x, main thread 0.826x. Settle median -10.0 ms (-295.8 to +48.6 ms).
The watched rows:

| Row | `phase-c-base` | `phase-c-end` | Change | Threshold |
| --- | --- | --- | --- | --- |
| `jump / mainJsMs` | 223.5 | 164.7 | 0.737x | |
| `scroll / mainJsMs` | 1,320.0 | 1,067.3 | 0.809x | |
| `scrollMid / mainJsMs` | 1,604.1 | 1,124.0 | 0.701x | |
| `zoomLarge / mainJsMs` | 413.2 | 346.1 | 0.838x | |
| `zoomLarge / settleMs` | 348.2 | 325.5 | -22.7 ms | |
| `hover / mainJsMs` | 1,147.8 | 832.8 | 0.726x | |
| `hover / settleMs` | 1,552.2 | 1,256.4 | -295.8 ms | |
| `searchLarge / mainJsMs` | 1,258.3 | 886.7 | 0.705x | |
| `searchLarge / searchMs` | 2,187.2 | 1,926.8 | 0.881x | |
| `selectLarge / mainJsMs` | 142.7 | 122.5 | 0.859x, no difference | ±19.3% |
| `selectLarge / settleMs` | 284.1 | 270.4 | -13.7 ms, no difference | ±52.9 |
| `jump / settleMs` | 167.0 | 160.1 | -6.9 ms, no difference | ±18.6 |
| `scroll / wallMs` | 2,752.0 | 2,695.4 | 0.979x, no difference | ±3.6% |
| `scrollMid / wallMs` | 2,874.5 | 2,644.1 | 0.920x, no difference | ±8.7% |

Off the watch list, `dragAnnot` is 0.787x with settle -180.0 ms, `annotateLarge` 0.765x and
-36.1 ms, `drawInk` 0.733x, `typeField` 0.911x and -22.8 ms, and `undo` 0.744x and -149.9 ms.
`drawInk / settleMs` shows no difference here (+48.6 ms, ±171.2), where `04-18-50` recorded
-2,824.2 ms ("Phases C and D"). We think both are right, each at its own frame interval. Pointer
input is frame-paced, and the two runs ran at different intervals: 16.7 ms in `04-18-50` and
11.0 to 11.3 ms here, on every arm. `drawInk` is 180 pointer moves and the ink tool's 800 ms
grouping, so a move whose handling fits in a frame costs one interval and one that doesn't costs
two or more. `main` shows the shape: at 3 to 4 ms of main-thread JavaScript a move, it took
3,884.2 ms in `04-18-50` and 2,907.5 ms here, each 180 frames plus about 875 ms. `phase-c-end`
spends about 14 ms a move (2,508.5 ms of `mainJsMs` there, 2,541.7 ms here), one frame at 16.7 ms
and more than one at 11.1 ms. `phase-c-base` spends 19 to 22 ms (4,006.5 and 3,467.1 ms), more
than a frame at either interval. So at 16.7 ms Phase C took `drawInk` from about two frames a move
to about one (6,971.2 to 4,147.0 ms), and at 11.1 ms both arms need two or more and the rows meet
(5,707.0 and 5,755.7 ms). Phase C's `drawInk / mainJsMs` saving holds in both runs (0.733x here).
This is an inference from recorded rows of two runs that also differ in probe, which is the kind of
cross-run comparison this file otherwise doesn't count. No run has timed the same arms at both
intervals under one probe, and we don't know why the Mac mini's interval changed.

#### Phase D (`phase-c-end` to `phase-d-end` 0d18fb177)

10 rows faster (3 of them `undo`), 75 no difference, 1 slower. Timing median 0.997x, frame interval
1.012x, main thread 0.992x. Settle median -1.8 ms (-292.7 to +12.0 ms). `undo` is 0.640x
(457.2 to 292.7 ms) with settle -292.7 ms. `typeField` is 0.817x (248.4 to 203.0 ms) with
-37.3 ms. `drawInk` is 0.934x and `hover` 0.953x, and neither settle moved (`drawInk` -93.7 ms,
±172.7; `hover` -41.8 ms, ±94.3). `dragAnnot` and `annotateLarge` show no difference on any row.
`dragAnnot / mainJsMs` reads 0.953x, but its A/A band is ±6.7%. The earlier run had it at 0.943x
and settle -26.6 ms, under a done condition that listed the stamp page inside the window.

The slower row is `open(ebook) / mainJsMs`, 336.0 to 346.9 ms, 1.032x [1.002, 1.048] against a
±3.0% floor. Phase D doesn't change the open path, `open(large) / mainJsMs` didn't move, and the
same pair read no difference on this row in `04-18-50`. No same-build drift check is possible yet
(see above), so it stays unconfirmed. If the next run under this probe repeats it, it's a
regression.

#### Phase E (`phase-d-end` to `perf-fixes-end` a6d4d5d14)

No detectable difference on any row: 83, plus `undo`'s 3 judged against the floor. Timing median
1.002x, frame interval 1.004x, main thread 1.000x, settle median +0.3 ms (-16.7 to +26.0 ms). The
engine wasm changed and is 0.11 MiB smaller, and no flow sees it.

#### Where the port stands (`main-now` ed064e0c8 to `perf-fixes-end`)

53 rows slower (3 of them `undo`), 29 no difference, 4 faster. Timing median 1.447x, frame interval
1.019x, main thread 1.468x. Settle median +20.9 ms (-19.5 to +2,574.1 ms). `phase-d-end` reads
almost the same against `main-now`: timing 1.459x, main thread 1.466x, settle +20.1 ms. The four
faster rows are the scroll `wallMs` rows (`scroll` 0.925x, `scrollMid` 0.869x, `coldScroll`
0.895x) and `searchLarge / searchMs` 0.859x. The widest gaps, `mainJsMs`:

| Row | `main-now` | `perf-fixes-end` | Ratio |
| --- | --- | --- | --- |
| `searchLarge` | 348.0 | 901.1 | 2.590x |
| `dragAnnot` | 201.9 | 505.9 | 2.506x |
| `search` | 9.3 | 22.1 | 2.392x |
| `undo` | 134.2 | 293.6 | 2.188x |
| `typeField` | 93.3 | 200.8 | 2.152x |
| `drawInk` | 1,136.0 | 2,370.9 | 2.087x |
| `open(large)` | 179.0 | 363.5 | 2.031x |
| `open(ebook)` | 181.8 | 344.5 | 1.895x |
| `hover` | 422.7 | 788.4 | 1.865x |

The largest settle gaps are `dragAnnot` +339.7 ms, `undo` +205.6 ms, `hover` +193.3 ms and
`typeField` +74.3 ms. Open is about twice as slow to `readyMs` (2.061x on `ebook`, 2.058x on
`large`).

`drawInk / settleMs` reads +2,574.1 ms (3,093.3 to 5,667.4). In `04-18-50`, `main-now` against
`phase-d-end` read +96.8 ms (3,940.9 to 4,037.8, no difference). We think the frame interval
explains the change (Phase C above). `phase-d-end` and `perf-fixes-end` spend about 13 ms of
main-thread JavaScript an ink move (2,374.8 and 2,370.9 ms over 180 moves here), which fits a
16.7 ms frame and not an 11.1 ms one. So they took about one frame a move in `04-18-50` (4,037.8 ms)
and two to three here (5,662.0 and 5,667.4 ms). `main-now`, at 6 to 7 ms a move, stays near one
frame at either interval (3,940.9 and 3,093.3 ms). `main`'s `hover / settleMs` fell from 1,323.6
to 1,086.0 ms between the two runs too, although that flow has no done condition on `main` and
didn't change. If the inference holds, the +2,574.1 ms is what the port's slower ink handling costs
at an 11.1 ms frame, and at 16.7 ms it cost nothing detectable: the row measures whether a move
fits a frame, not the handling itself, and the `drawInk / mainJsMs` row (2.087x) is the steadier
measure of the port's cost. Like the Phase C reading, this rests on two runs that differ in probe
as well as interval, not on an experiment. Every settle row of a flow that drives the pointer
(`hover`, `dragAnnot`, `drawInk`, and the drags of `select`, `selectLarge`, `annotate` and
`annotateLarge`) is counted in frames this way, so it depends on the interval, and `summary.mjs
--drift` now refuses two runs whose intervals are more than 1 ms apart.

`main` against `main-now`, for reference: 42 slower (plus 2 `hover` rows with no done condition
on `main`), 8 faster, 30 no difference. Timing 1.051x, frame interval 1.015x, main thread 1.488x,
settle median +0.0 ms (-179.2 to +185.8 ms).
