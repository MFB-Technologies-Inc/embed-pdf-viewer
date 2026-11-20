# Viewer perf comparison: 2026-10-22T22-12-06-838Z-mac-slow

3 rounds, profile `slow` (CPU throttle 4x), **quick, not for publication**.

The throttle reached the main thread only: Chromium refuses `Emulation.setCPUThrottlingRate` on the engine worker, so worker-side work ran at full speed.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | b5db930c78ee8b82819b713f846a4443f3000a76 (uncommitted changes) |
| Chromium / Playwright | 140.0.7339.16 / 1.55.0 |
| Node | v24.20.0 |
| OS | Darwin 27.0.0 arm64 |
| CPU | Apple M4 Max, 16 cores |
| Memory | 64 GiB |
| Power | AC Power |
| Viewport | 1280x800 @2x |
| Fixtures | ebook 2e539f62b5fa, large 10c8668fe55c |

| Arm | Ref | SHA | Payload | Engine wasm sha256 | Kernel wasm sha256 |
| --- | --- | --- | --- | --- | --- |
| main | main | 2516e2786 | fetched | 98c2368a774d |  |
| main-aa | main | 2516e2786 | fetched | 98c2368a774d |  |

## Gate

- `main`: pass
- `main-aa`: pass

## Sizes

| Metric | main | main-aa |
| --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

30 of 30 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 122.2 [124.5] | 123.8 [134.7] | 1.013 [0.989, 1.121] | pass |
| open(ebook) / openedMs | 216.4 [222.3] | 220.5 [230.3] | 1.019 [0.985, 1.073] | pass |
| open(ebook) / firstPaintMs | 370.3 [371.3] | 376.5 [388.1] | 1.017 [0.989, 1.052] | pass |
| open(ebook) / settledMs | 506.6 [519.3] | 515.9 [525.3] | 1.018 [0.975, 1.055] | pass |
| open(ebook) / mainTaskMs | 411.7 [417.9] | 415.1 [427.8] | 1.008 [0.948, 1.071] | pass |
| open(ebook) / mainJsMs | 342.6 [348.8] | 337.7 [351.1] | 0.986 [0.922, 1.044] | pass |
| open(large) / readyMs | 122.7 [123.9] | 122.3 [147.3] | 0.996 [0.972, 1.228] | pass |
| open(large) / openedMs | 262.2 [262.4] | 264.0 [307.0] | 1.007 [0.990, 1.211] | pass |
| open(large) / firstPaintMs | 405.6 [407.5] | 411.0 [468.2] | 1.013 [0.925, 1.191] | pass |
| open(large) / settledMs | 433.6 [439.2] | 441.3 [498.7] | 1.018 [0.962, 1.191] | pass |
| open(large) / mainTaskMs | 635.7 [641.3] | 644.5 [687.4] | 1.014 [0.962, 1.127] | pass |
| open(large) / mainJsMs | 566.9 [578.6] | 575.4 [615.4] | 1.015 [0.955, 1.139] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2402.1 [2443.5] | 2311.2 [2375.0] | 0.962 [0.940, 1.018] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.002 [0.998, 1.004] | pass |
| scroll / frameP95Ms | 10.1 [10.1] | 10.1 [10.2] | 0.999 [0.993, 1.010] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 1361.8 [1524.2] | 1197.9 [1355.8] | 0.880 [0.777, 1.140] | pass |
| scroll / mainJsMs | 1111.2 [1231.7] | 997.0 [1115.8] | 0.897 [0.797, 1.141] | pass |
| jump / settleMs | 148.8 [163.6] | 147.2 [161.8] | -1.5 [-43.5, +30.8] | pass |
| jump / mainTaskMs | 203.4 [239.1] | 198.4 [232.6] | 0.976 [0.795, 1.297] | pass |
| jump / mainJsMs | 162.7 [203.0] | 172.9 [186.4] | 1.063 [0.738, 1.327] | pass |
| zoom / settleMs | 496.8 [534.2] | 473.3 [478.8] | -23.5 [-66.4, +7.6] | pass |
| zoom / zoom2SettleMs | 201.5 [220.6] | 189.7 [206.6] | -11.8 [-44.2, +20.8] | pass |
| zoom / zoom4SettleMs | 295.3 [313.5] | 282.3 [293.5] | -13.0 [-44.5, +10.5] | pass |
| zoom / backTo1SettleMs | 267.5 [305.2] | 261.0 [261.5] | -6.5 [-48.6, +0.6] | pass |
| zoom / mainTaskMs | 864.1 [1025.3] | 855.0 [855.3] | 0.990 [0.814, 1.001] | pass |
| zoom / mainJsMs | 740.6 [869.8] | 727.6 [732.9] | 0.982 [0.804, 1.004] | pass |

## `main` vs `impl-ts`

Not run: `impl-ts`.

## `impl-ts` vs `impl-rust`

Not run: `impl-ts`, `impl-rust`.

## `impl-ts` vs `plugins`

Not run: `impl-ts`, `plugins`.

## `main` vs `plugins`

Not run: `plugins`.

## `plugins` vs `plugins-lto`

Not run: `plugins`, `plugins-lto`.

## `plugins-lto` vs `plugins-fix`

Not run: `plugins-lto`, `plugins-fix`.

## `plugins-fix` vs `plugins-fix34`

Not run: `plugins-fix`, `plugins-fix34`.

## `plugins-fix34` vs `plugins-fix-leak`

Not run: `plugins-fix34`, `plugins-fix-leak`.

## `plugins-fix-leak` vs `plugins-fix-zoom`

Not run: `plugins-fix-leak`, `plugins-fix-zoom`.

## `plugins-lto` vs `plugins-fix-zoom`

Not run: `plugins-lto`, `plugins-fix-zoom`.

## `main` vs `plugins-fix-zoom`

Not run: `plugins-fix-zoom`.

## `plugins-fix-zoom` vs `plugins-wire`

Not run: `plugins-fix-zoom`, `plugins-wire`.

## `plugins-wire` vs `plugins-getstate`

Not run: `plugins-wire`, `plugins-getstate`.

## `plugins-lto` vs `plugins-getstate`

Not run: `plugins-lto`, `plugins-getstate`.

## `main` vs `plugins-getstate`

Not run: `plugins-getstate`.

## `plugins-getstate` vs `plugins-toolbar`

Not run: `plugins-getstate`, `plugins-toolbar`.

## `main` vs `plugins-toolbar`

Not run: `plugins-toolbar`.

## `plugins-toolbar` vs `plugins-bump`

Not run: `plugins-toolbar`, `plugins-bump`.

## `main` vs `plugins-bump`

Not run: `plugins-bump`.

## `plugins-getstate` vs `plugins-item1`

Not run: `plugins-getstate`, `plugins-item1`.

## `plugins-getstate` vs `plugins-item3`

Not run: `plugins-getstate`, `plugins-item3`.

## `plugins-getstate` vs `plugins-bump`

Not run: `plugins-getstate`, `plugins-bump`.

## `plugins-item1` vs `plugins-bump`

Not run: `plugins-item1`, `plugins-bump`.

## `plugins-bump` vs `plugins-reads`

Not run: `plugins-bump`, `plugins-reads`.

## `main` vs `plugins-reads`

Not run: `plugins-reads`.

## `impl-ts` vs `impl-gate`

Not run: `impl-ts`, `impl-gate`.

## `plugins-head` vs `plugins-buildlto`

Not run: `plugins-head`, `plugins-buildlto`.

## `plugins-head` vs `plugins-wasmopt`

Not run: `plugins-head`, `plugins-wasmopt`.

## `plugins-bump` vs `plugins-buildlto`

Not run: `plugins-bump`, `plugins-buildlto`.

## `impl-head` vs `impl-enginelink`

Not run: `impl-head`, `impl-enginelink`.

## `plugins-tip` vs `plugins-enginelink`

Not run: `plugins-tip`, `plugins-enginelink`.

## `impl-head` vs `impl-fixed`

Not run: `impl-head`, `impl-fixed`.

## `plugins-tip` vs `plugins-fixed`

Not run: `plugins-tip`, `plugins-fixed`.

## `impl-fixed` vs `impl-enginelink`

Not run: `impl-fixed`, `impl-enginelink`.

## `plugins-fixed` vs `plugins-enginelink`

Not run: `plugins-fixed`, `plugins-enginelink`.

## `impl-fixed` vs `impl-o2`

Not run: `impl-fixed`, `impl-o2`.

## `impl-o2` vs `impl-enginelink`

Not run: `impl-o2`, `impl-enginelink`.

## `plugins-fixed` vs `plugins-o2`

Not run: `plugins-fixed`, `plugins-o2`.

## `plugins-o2` vs `plugins-enginelink`

Not run: `plugins-o2`, `plugins-enginelink`.

## `main` vs `main-compat`

Not run: `main-compat`.

## `impl-fixed` vs `impl-compat`

Not run: `impl-fixed`, `impl-compat`.

## `plugins-fixed` vs `plugins-compat`

Not run: `plugins-fixed`, `plugins-compat`.

## `plugins-now` vs `plugins-slicetext`

Not run: `plugins-now`, `plugins-slicetext`.

## `main` vs `plugins-slicetext`

Not run: `plugins-slicetext`.

## `impl-now` vs `impl-glyphview`

Not run: `impl-now`, `impl-glyphview`.

## `plugins-tip2` vs `plugins-glyphview`

Not run: `plugins-tip2`, `plugins-glyphview`.

## `main` vs `impl-glyphview`

Not run: `impl-glyphview`.

## `plugins-tip2` vs `plugins-lazy`

Not run: `plugins-tip2`, `plugins-lazy`.

## `main` vs `plugins-lazy`

Not run: `plugins-lazy`.

## `plugins-lazy` vs `plugins-tip3`

Not run: `plugins-lazy`, `plugins-tip3`.

## `main` vs `plugins-tip3`

Not run: `plugins-tip3`.

## `plugins-tip3` vs `plugins-sharedhits`

Not run: `plugins-tip3`, `plugins-sharedhits`.

## `main` vs `plugins-sharedhits`

Not run: `plugins-sharedhits`.

## `plugins-sharedhits` vs `plugins-promise`

Not run: `plugins-sharedhits`, `plugins-promise`.

## `plugins-promise` vs `plugins-typed`

Not run: `plugins-promise`, `plugins-typed`.

## `plugins-sharedhits` vs `plugins-typed`

Not run: `plugins-sharedhits`, `plugins-typed`.

## `main` vs `plugins-typed`

Not run: `plugins-typed`.

## `plugins-typed` vs `plugins-fixes`

Not run: `plugins-typed`, `plugins-fixes`.

## `plugins-fixes` vs `plugins-encode`

Not run: `plugins-fixes`, `plugins-encode`.

## `main` vs `plugins-encode`

Not run: `plugins-encode`.

## `plugins-tip4` vs `plugins-hostbytes`

Not run: `plugins-tip4`, `plugins-hostbytes`.

## `main` vs `plugins-hostbytes`

Not run: `plugins-hostbytes`.

## `plugins-tip5` vs `plugins-geomdecode`

Not run: `plugins-tip5`, `plugins-geomdecode`.

## `main` vs `plugins-geomdecode`

Not run: `plugins-geomdecode`.

## `plugins-tip5` vs `plugins-coalesce`

Not run: `plugins-tip5`, `plugins-coalesce`.

## `main` vs `plugins-coalesce`

Not run: `plugins-coalesce`.

## `plugins-coalesce` vs `plugins-tip6`

Not run: `plugins-coalesce`, `plugins-tip6`.

## `main` vs `plugins-tip6`

Not run: `plugins-tip6`.

## `plugins-tip6` vs `plugins-plainargs`

Not run: `plugins-tip6`, `plugins-plainargs`.

## `main` vs `plugins-plainargs`

Not run: `plugins-plainargs`.

## `plugins-plainargs` vs `plugins-resulttext`

Not run: `plugins-plainargs`, `plugins-resulttext`.

## `main` vs `plugins-resulttext`

Not run: `plugins-resulttext`.

## `plugins-resulttext` vs `plugins-rustreads`

Not run: `plugins-resulttext`, `plugins-rustreads`.

## `main` vs `plugins-rustreads`

Not run: `plugins-rustreads`.

## `plugins-resulttext` vs `plugins-tip7`

Not run: `plugins-resulttext`, `plugins-tip7`.

## `main` vs `plugins-tip7`

Not run: `plugins-tip7`.

## `main` vs `plugins-base`

Not run: `plugins-base`.

## `plugins-base` vs `plugins-next`

Not run: `plugins-base`, `plugins-next`.

## `main` vs `plugins-next`

Not run: `plugins-next`.

## Memory

| Row | main median [p95] | main-aa median [p95] |
| --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 35.42 MiB [35.42 MiB] | 35.40 MiB [35.41 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] |
| open(large) / heapAfterOpenBytes | 47.66 MiB [47.68 MiB] | 47.56 MiB [47.63 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.21 MiB [22.21 MiB] | 22.21 MiB [22.21 MiB] |
