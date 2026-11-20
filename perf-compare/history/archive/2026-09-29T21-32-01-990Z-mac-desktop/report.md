# Viewer perf comparison: 2026-09-29T21-32-01-990Z-mac-desktop

3 rounds, profile `desktop` (CPU throttle 1x), **quick, not for publication**.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | fe3cbe0961ea05653063255985755044bd82c0b6 (uncommitted changes) |
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
| plugins-lto | research/multi-platform-rust-core-impl-plugins | bef223d9d | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | ce0b35ed118c |
| plugins-fix | research/rust-kernel-state-cache | e0f33b1c4 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | b660fde1b0fb |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-lto`: pass
- `plugins-fix`: pass

## Sizes

| Metric | main | main-aa | plugins-lto | plugins-fix |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 11.68 MiB | 11.68 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.88 MiB | 1.88 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.62 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

39 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 43.4 [45.5] | 44.9 [46.3] | 1.034 [0.931, 1.085] | pass |
| open(ebook) / openedMs | 96.5 [96.6] | 98.1 [100.6] | 1.016 [0.976, 1.075] | pass |
| open(ebook) / firstPaintMs | 174.0 [175.4] | 180.3 [181.3] | 1.036 [0.961, 1.071] | pass |
| open(ebook) / settledMs | 271.1 [273.0] | 270.6 [275.9] | 0.998 [0.958, 1.051] | pass |
| open(ebook) / mainTaskMs | 95.3 [97.8] | 93.2 [97.8] | 0.978 [0.950, 1.044] | pass |
| open(ebook) / mainScriptMs | 16.4 [16.6] | 15.8 [16.6] | 0.963 [0.948, 1.063] | pass |
| open(large) / readyMs | 45.1 [45.5] | 45.2 [45.2] | 1.001 [0.949, 1.067] | pass |
| open(large) / openedMs | 111.4 [112.5] | 112.1 [114.8] | 1.006 [0.989, 1.040] | pass |
| open(large) / firstPaintMs | 180.3 [180.5] | 180.7 [183.9] | 1.002 [0.932, 1.051] | pass |
| open(large) / settledMs | 176.7 [176.8] | 180.0 [180.6] | 1.019 [0.999, 1.058] | pass |
| open(large) / mainTaskMs | 207.5 [208.6] | 209.4 [211.7] | 1.009 [0.987, 1.023] | pass |
| open(large) / mainScriptMs | 18.2 [18.3] | 18.2 [18.3] | 1.001 [0.960, 1.030] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2002.0 [2008.7] | 2001.0 [2008.6] | 0.999 [0.995, 1.005] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [8.4] | 8.3 [8.4] | 1.000 [0.998, 1.001] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 337.6 [353.1] | 350.7 [358.7] | 1.039 [0.976, 1.068] | pass |
| scroll / mainScriptMs | 46.4 [47.5] | 47.4 [47.9] | 1.021 [0.981, 1.037] | pass |
| jump / settleMs | 73.9 [81.0] | 75.5 [80.6] | +1.5 [-7.9, +13.8] | pass |
| jump / mainTaskMs | 48.0 [48.8] | 47.6 [50.7] | 0.993 [0.970, 1.085] | pass |
| jump / mainScriptMs | 12.0 [12.1] | 12.3 [12.5] | 1.025 [0.962, 1.138] | pass |
| zoom / settleMs | 276.4 [293.6] | 269.6 [272.9] | -6.8 [-28.1, +32.8] | pass |
| zoom / zoom2SettleMs | 103.6 [114.4] | 102.0 [103.9] | -1.6 [-19.9, +9.6] | pass |
| zoom / zoom4SettleMs | 172.8 [179.2] | 171.3 [171.7] | -1.5 [-14.4, +25.8] | pass |
| zoom / backTo1SettleMs | 52.6 [55.3] | 67.6 [71.5] | +15.0 [-5.2, +20.7] | pass |
| zoom / mainTaskMs | 137.4 [144.4] | 130.7 [136.4] | 0.951 [0.890, 1.078] | pass |
| zoom / mainScriptMs | 20.9 [21.1] | 20.0 [22.6] | 0.957 [0.923, 1.144] | pass |
| select / settleMs | 142.8 [146.2] | 143.6 [148.8] | +0.8 [-4.6, +8.2] | pass |
| select / mainTaskMs | 39.0 [40.4] | 40.5 [43.0] | 1.039 [0.935, 1.222] | pass |
| select / mainScriptMs | 8.8 [9.4] | 9.6 [9.8] | 1.083 [0.910, 1.197] | pass |
| search / searchMs | 49.9 [51.1] | 49.7 [52.2] | 0.995 [0.766, 1.078] | pass |
| search / mainTaskMs | 4.1 [5.1] | 3.7 [4.8] | 0.911 [0.631, 1.219] | pass |
| search / mainScriptMs | 0.1 [0.1] | 0.1 [0.1] | 0.945 [0.562, 1.200] | pass |
| annotate / settleMs | 152.4 [152.9] | 155.6 [156.2] | +3.2 [-0.9, +4.8] | pass |
| annotate / mainTaskMs | 42.5 [49.8] | 50.9 [51.9] | 1.198 [0.839, 1.553] | pass |
| annotate / mainScriptMs | 8.2 [9.9] | 9.9 [10.6] | 1.212 [0.826, 1.637] | pass |

## `main` vs `impl-ts`

Not run: `impl-ts`.

## `impl-ts` vs `impl-rust`

Not run: `impl-ts`, `impl-rust`.

## `impl-ts` vs `plugins`

Not run: `impl-ts`, `plugins`.

## `main` vs `plugins`

Not run: `plugins`.

## `plugins` vs `plugins-lto`

Not run: `plugins`.

## `plugins-lto` vs `plugins-fix`

| Row | plugins-lto median [p95] | plugins-fix median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 69.5 [70.7] | 72.0 [72.2] | 1.035 [0.991, 1.053] | no detectable difference |
| open(ebook) / openedMs | 171.6 [172.3] | 154.7 [155.7] | 0.902 [0.879, 0.927] | faster |
| open(ebook) / firstPaintMs | 326.8 [327.0] | 272.6 [275.4] | 0.834 [0.816, 0.858] | faster |
| open(ebook) / settledMs | 497.7 [525.0] | 371.5 [373.7] | 0.746 [0.680, 0.769] | faster |
| open(ebook) / mainTaskMs | 383.0 [410.4] | 250.8 [251.6] | 0.655 [0.587, 0.666] | faster |
| open(ebook) / mainScriptMs | 199.1 [217.4] | 107.1 [107.3] | 0.538 [0.470, 0.542] | faster |
| open(large) / readyMs | 69.2 [70.2] | 70.7 [71.1] | 1.022 [1.000, 1.033] | no detectable difference |
| open(large) / openedMs | 1301.9 [1304.1] | 927.1 [927.4] | 0.712 [0.709, 0.726] | faster |
| open(large) / firstPaintMs | 2106.8 [2111.9] | 1271.3 [1274.6] | 0.603 [0.598, 0.623] | faster |
| open(large) / settledMs | 2146.6 [2148.7] | 1284.2 [1287.6] | 0.598 [0.597, 0.600] | faster |
| open(large) / mainTaskMs | 2426.7 [2427.5] | 1515.1 [1521.9] | 0.624 [0.622, 0.628] | faster |
| open(large) / mainScriptMs | 1990.6 [1991.3] | 1322.1 [1328.7] | 0.664 [0.663, 0.668] | faster |
| scroll / settleMs | 46.1 [119.7] | 69.4 [70.0] | +23.3 [-109.6, +27.1] | no detectable difference |
| scroll / wallMs | 15102.4 [15131.8] | 6011.8 [6155.7] | 0.398 [0.389, 0.428] | faster |
| scroll / frameP50Ms | 41.7 [41.7] | 8.3 [8.3] | 0.200 [0.200, 0.334] | faster |
| scroll / frameP95Ms | 194.5 [202.3] | 66.7 [66.7] | 0.343 [0.328, 0.378] | faster |
| scroll / jankFrames | 100 [100] | 50 [50.90] | -50 [-53, -47] | faster |
| scroll / longTasks | 66 [66.90] | 37 [37.90] | -29 [-34, -25] | faster |
| scroll / longTaskMs | 9133.0 [9177.1] | 2102.0 [2158.7] | -7031.0 [-7305.0, -6246.0] | faster |
| scroll / mainTaskMs | 15520.0 [15542.6] | 6284.8 [6452.4] | 0.405 [0.397, 0.437] | faster |
| scroll / mainScriptMs | 8578.7 [8639.3] | 4439.7 [4567.1] | 0.518 [0.502, 0.566] | faster |
| jump / settleMs | 836.3 [898.0] | 259.2 [367.7] | -577.1 [-661.4, -421.4] | faster |
| jump / mainTaskMs | 1177.9 [1205.7] | 544.7 [654.3] | 0.462 [0.418, 0.603] | faster |
| jump / mainScriptMs | 777.1 [800.2] | 440.8 [531.0] | 0.567 [0.503, 0.760] | faster |
| zoom / settleMs | 523.6 [523.9] | 420.9 [421.0] | -102.6 [-110.1, -92.3] | faster |
| zoom / zoom2SettleMs | 243.3 [250.1] | 155.8 [158.9] | -87.5 [-101.6, -81.8] | faster |
| zoom / zoom4SettleMs | 272.7 [279.8] | 264.5 [265.1] | -8.2 [-19.0, -7.0] | no detectable difference |
| zoom / backTo1SettleMs | 101.2 [106.6] | 85.1 [86.7] | -16.1 [-25.1, -12.6] | no detectable difference |
| zoom / mainTaskMs | 808.2 [844.4] | 362.3 [364.2] | 0.448 [0.422, 0.467] | faster |
| zoom / mainScriptMs | 428.8 [453.5] | 162.8 [165.6] | 0.380 [0.354, 0.407] | faster |
| select / settleMs | 296.1 [296.5] | 141.8 [142.2] | -154.3 [-156.1, -149.2] | faster |
| select / mainTaskMs | 220.3 [222.5] | 83.5 [93.7] | 0.379 [0.352, 0.434] | faster |
| select / mainScriptMs | 170.7 [173.4] | 44.1 [48.4] | 0.258 [0.241, 0.288] | faster |
| search / searchMs | 74.2 [74.4] | 55.9 [56.1] | 0.754 [0.651, 0.842] | faster |
| search / mainTaskMs | 34.2 [34.5] | 15.8 [16.1] | 0.463 [0.422, 0.515] | faster |
| search / mainScriptMs | 17.0 [17.5] | 7.8 [7.9] | 0.462 [0.429, 0.488] | faster |
| annotate / settleMs | 322.8 [325.2] | 164.6 [167.8] | -158.2 [-161.6, -154.3] | faster |
| annotate / mainTaskMs | 262.7 [270.4] | 111.0 [111.1] | 0.422 [0.361, 0.425] | faster |
| annotate / mainScriptMs | 192.3 [197.8] | 53.9 [54.6] | 0.280 [0.243, 0.285] | faster |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-lto median [p95] | plugins-fix median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.33 MiB [34.34 MiB] | 43.27 MiB [43.28 MiB] | 43.02 MiB [43.03 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.03 MiB [23.03 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.16 MiB] | 48.15 MiB [48.18 MiB] | 58.01 MiB [58.01 MiB] | 58.85 MiB [58.85 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.28 MiB] | 22.94 MiB [22.94 MiB] | 22.94 MiB [22.94 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.42 MiB] | 37.42 MiB [37.42 MiB] | 48.96 MiB [48.98 MiB] | 48.78 MiB [48.80 MiB] |
| soak / heapCycle30Bytes | 38.28 MiB [38.29 MiB] | 38.28 MiB [38.29 MiB] | 63.02 MiB [63.02 MiB] | 62.74 MiB [62.74 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.56 MiB [0.56 MiB] | 0.56 MiB [0.56 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-lto`:

| Table | Cycle 5 | Cycle 30 |
| --- | --- | --- |
| aborts | 0 | 0 |
| allHosts | 296 | 296 |
| described | 18 | 18 |
| documents | 1 | 1 |
| fns | 259 | 259 |
| heldFns | 259 | 259 |
| hosts | 296 | 296 |
| promises | 0 | 0 |
| proxies | 18 | 18 |
| refs | 645 | 645 |
| wasmBytes | 8.94 MiB | 8.94 MiB |
| wireRefs | 645 | 645 |

`plugins-fix`:

| Table | Cycle 5 | Cycle 30 |
| --- | --- | --- |
| aborts | 0 | 0 |
| allHosts | 296 | 296 |
| described | 18 | 18 |
| documents | 1 | 1 |
| fns | 259 | 259 |
| heldFns | 259 | 259 |
| hosts | 296 | 296 |
| promises | 0 | 0 |
| proxies | 18 | 18 |
| refs | 645 | 645 |
| wasmBytes | 8.94 MiB | 8.94 MiB |
| wireRefs | 645 | 645 |

