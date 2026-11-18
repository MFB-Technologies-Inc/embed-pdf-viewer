# Viewer perf comparison: 2026-09-29T15-59-00-348Z-2024mbp16black-desktop

3 rounds, profile `desktop` (CPU throttle 1x), **quick, not for publication**.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | e6834aacd5b9a3c72f333518231d9cf4d3fb1c23 (uncommitted changes) |
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

Measured from the current `.work/dist`, not recorded at run time.

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

38 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 44.6 [45.2] | 42.6 [43.1] | 0.957 [0.938, 1.002] | pass |
| open(ebook) / openedMs | 96.6 [97.4] | 94.4 [95.4] | 0.978 [0.965, 0.997] | pass |
| open(ebook) / firstPaintMs | 176.4 [177.0] | 176.9 [178.4] | 1.003 [0.959, 1.033] | pass |
| open(ebook) / settledMs | 266.2 [267.5] | 265.0 [267.1] | 0.995 [0.988, 1.008] | pass |
| open(ebook) / mainTaskMs | 93.3 [93.9] | 93.5 [95.0] | 1.003 [0.993, 1.031] | pass |
| open(ebook) / mainScriptMs | 15.4 [15.9] | 15.6 [16.1] | 1.011 [0.976, 1.051] | pass |
| open(large) / readyMs | 43.4 [43.6] | 43.7 [44.7] | 1.007 [0.996, 1.050] | pass |
| open(large) / openedMs | 110.5 [110.6] | 111.3 [111.7] | 1.008 [0.996, 1.019] | pass |
| open(large) / firstPaintMs | 177.1 [178.6] | 177.7 [179.4] | 1.003 [0.988, 1.020] | pass |
| open(large) / settledMs | 172.7 [174.7] | 174.8 [175.8] | 1.012 [0.986, 1.021] | pass |
| open(large) / mainTaskMs | 207.2 [208.1] | 206.0 [206.3] | 0.994 [0.989, 0.996] | pass |
| open(large) / mainScriptMs | 18.5 [18.5] | 18.0 [18.1] | 0.972 [0.968, 0.990] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2002.3 [2015.7] | 2002.2 [2002.2] | 1.000 [0.992, 1.000] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.001] | pass |
| scroll / frameP95Ms | 8.9 [9.0] | 9.0 [9.2] | 1.001 [0.985, 1.053] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 341.9 [364.0] | 337.0 [358.1] | 0.986 [0.914, 1.087] | pass |
| scroll / mainScriptMs | 47.8 [47.9] | 46.7 [47.0] | 0.977 [0.975, 1.033] | pass |
| jump / settleMs | 77.7 [80.5] | 78.1 [82.5] | +0.4 [-3.6, +17.7] | pass |
| jump / mainTaskMs | 44.0 [45.5] | 45.6 [47.2] | 1.034 [0.974, 1.079] | pass |
| jump / mainScriptMs | 11.4 [12.1] | 12.2 [12.3] | 1.066 [0.989, 1.090] | pass |
| zoom / settleMs | 283.3 [293.6] | 269.9 [281.2] | -13.4 [-34.2, +12.2] | pass |
| zoom / zoom2SettleMs | 104.1 [110.1] | 105.1 [111.0] | +1.0 [-16.1, +16.2] | pass |
| zoom / zoom4SettleMs | 187.9 [190.4] | 165.8 [170.4] | -22.1 [-25.8, +11.4] | pass |
| zoom / backTo1SettleMs | 47.3 [60.8] | 53.2 [54.7] | +5.8 [-10.6, +9.6] | pass |
| zoom / mainTaskMs | 128.0 [139.0] | 129.3 [139.2] | 1.010 [0.908, 1.109] | pass |
| zoom / mainScriptMs | 21.5 [22.3] | 21.0 [21.5] | 0.979 [0.879, 1.136] | pass |
| select / settleMs | 145.2 [145.9] | 147.4 [149.2] | +2.3 [-8.5, +6.5] | pass |
| select / mainTaskMs | 40.8 [41.5] | 38.8 [42.7] | 0.952 [0.789, 1.125] | pass |
| select / mainScriptMs | 9.7 [9.7] | 8.8 [9.8] | 0.914 [0.785, 1.096] | pass |
| search / searchMs | 50.3 [52.7] | 51.6 [51.8] | 1.025 [0.959, 1.067] | pass |
| search / mainTaskMs | 4.5 [4.6] | 3.9 [4.4] | 0.881 [0.837, 1.104] | pass |
| search / mainScriptMs | 0.1 [0.1] | 0.1 [0.1] | 0.920 [0.817, 0.928] | A/A failed |
| annotate / settleMs | 153.7 [155.9] | 154.0 [156.3] | +0.2 [-4.7, +3.0] | pass |
| annotate / mainTaskMs | 51.1 [52.4] | 49.3 [52.3] | 0.967 [0.911, 1.081] | pass |
| annotate / mainScriptMs | 10.5 [10.6] | 10.4 [11.1] | 0.988 [0.907, 1.149] | pass |

## `main` vs `impl-ts`

Not run: `impl-ts`.

## `impl-ts` vs `impl-rust`

Not run: `impl-ts`, `impl-rust`.

## `impl-ts` vs `plugins`

Not run: `impl-ts`, `plugins`.

## `main` vs `plugins`

Not run: `plugins`.

## Memory

| Row | main median [p95] | main-aa median [p95] |
| --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.34 MiB] | 34.33 MiB [34.34 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] |
| open(large) / heapAfterOpenBytes | 48.17 MiB [48.17 MiB] | 48.16 MiB [48.18 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.28 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.43 MiB] | 37.43 MiB [37.43 MiB] |
| soak / heapCycle30Bytes | 38.28 MiB [38.28 MiB] | 38.28 MiB [38.28 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] |
