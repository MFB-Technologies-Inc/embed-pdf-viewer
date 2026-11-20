# Viewer perf comparison: 2026-09-30T16-40-41-784Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x), **quick, not for publication**.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 579304f75904b2378b2e0a66dc544a766a5b9e20 |
| Chromium / Playwright | 140.0.7339.16 / 1.55.0 |
| Node | v24.20.0 |
| OS | Darwin 27.0.0 arm64 |
| CPU | Apple M2 Max, 12 cores |
| Memory | 64 GiB |
| Power | AC Power |
| Viewport | 1280x800 @2x |
| Fixtures | ebook 2e539f62b5fa, large 10c8668fe55c |

| Arm | Ref | SHA | Payload | Engine wasm sha256 | Kernel wasm sha256 |
| --- | --- | --- | --- | --- | --- |
| main | main | 2516e2786 | fetched | 98c2368a774d |  |
| main-aa | main | 2516e2786 | fetched | 98c2368a774d |  |
| plugins-fix-zoom | research/rust-kernel-state-cache | 8cef92c76 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | df47d777780e |
| plugins-wire | research/rust-kernel-wire | da947a564 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | dcdef9772bf9 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-fix-zoom`: pass
- `plugins-wire`: pass

## Sizes

| Metric | main | main-aa | plugins-fix-zoom | plugins-wire |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 11.68 MiB | 11.68 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.88 MiB | 1.88 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.62 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

37 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 51.9 [54.1] | 54.3 [54.4] | 1.046 [0.919, 1.052] | pass |
| open(ebook) / openedMs | 116.6 [117.8] | 116.5 [116.9] | 0.999 [0.965, 1.032] | pass |
| open(ebook) / firstPaintMs | 211.9 [217.9] | 216.4 [217.9] | 1.021 [0.958, 1.058] | pass |
| open(ebook) / settledMs | 332.8 [338.0] | 330.7 [337.1] | 0.994 [0.972, 1.022] | pass |
| open(ebook) / mainTaskMs | 115.2 [117.0] | 114.2 [115.3] | 0.991 [0.965, 1.014] | pass |
| open(ebook) / mainScriptMs | 19.9 [20.5] | 19.4 [20.5] | 0.977 [0.927, 1.085] | pass |
| open(large) / readyMs | 52.0 [54.3] | 52.8 [54.2] | 1.016 [0.949, 1.052] | pass |
| open(large) / openedMs | 133.1 [134.4] | 133.3 [134.1] | 1.001 [0.980, 1.026] | pass |
| open(large) / firstPaintMs | 223.0 [224.8] | 222.5 [223.8] | 0.998 [0.983, 1.011] | pass |
| open(large) / settledMs | 218.4 [221.5] | 218.9 [220.7] | 1.002 [0.984, 1.042] | pass |
| open(large) / mainTaskMs | 257.4 [259.4] | 258.2 [258.6] | 1.003 [0.976, 1.008] | pass |
| open(large) / mainScriptMs | 23.9 [24.2] | 22.7 [23.8] | 0.949 [0.925, 1.070] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2008.4 [2019.1] | 2001.7 [2009.0] | 0.997 [0.990, 1.004] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [8.4] | 8.4 [8.4] | 0.999 [0.999, 1.005] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 402.1 [411.2] | 399.8 [412.6] | 0.994 [0.928, 1.036] | pass |
| scroll / mainScriptMs | 54.6 [55.5] | 54.1 [55.6] | 0.991 [0.934, 1.026] | pass |
| jump / settleMs | 102.6 [105.3] | 98.0 [103.5] | -4.6 [-10.9, +12.4] | pass |
| jump / mainTaskMs | 59.0 [60.9] | 55.1 [59.4] | 0.934 [0.807, 1.023] | pass |
| jump / mainScriptMs | 15.1 [15.2] | 14.9 [17.1] | 0.990 [0.863, 1.230] | pass |
| zoom / settleMs | 379.5 [412.1] | 404.8 [410.6] | +25.3 [-36.7, +39.9] | pass |
| zoom / zoom2SettleMs | 131.5 [133.3] | 131.3 [133.4] | -0.2 [-2.7, +13.5] | pass |
| zoom / zoom4SettleMs | 251.2 [279.1] | 271.2 [279.1] | +20.0 [-34.0, +32.0] | pass |
| zoom / backTo1SettleMs | 74.2 [85.7] | 70.5 [85.4] | -3.7 [-16.8, +18.6] | pass |
| zoom / mainTaskMs | 173.9 [177.9] | 175.1 [177.9] | 1.007 [0.947, 1.042] | pass |
| zoom / mainScriptMs | 25.3 [27.0] | 24.3 [26.6] | 0.959 [0.875, 1.122] | pass |
| select / settleMs | 143.9 [151.2] | 150.8 [152.8] | +6.9 [-8.0, +9.8] | pass |
| select / mainTaskMs | 43.6 [45.1] | 41.5 [46.4] | 0.952 [0.817, 1.089] | pass |
| select / mainScriptMs | 9.4 [10.3] | 9.3 [10.7] | 0.990 [0.787, 1.172] | pass |
| search / searchMs | 56.8 [57.8] | 55.9 [60.1] | 0.983 [0.953, 1.076] | pass |
| search / mainTaskMs | 5.2 [5.2] | 4.8 [4.8] | 0.915 [0.836, 0.972] | A/A failed |
| search / mainScriptMs | 0.2 [0.2] | 0.1 [0.2] | 0.702 [0.524, 0.928] | A/A failed |
| annotate / settleMs | 155.4 [155.7] | 155.8 [159.1] | +0.4 [-0.7, +5.2] | pass |
| annotate / mainTaskMs | 54.9 [55.6] | 53.3 [57.1] | 0.970 [0.951, 1.067] | pass |
| annotate / mainScriptMs | 10.7 [11.1] | 10.2 [11.7] | 0.957 [0.914, 1.124] | pass |

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

Not run: `plugins-fix-leak`.

## `plugins-lto` vs `plugins-fix-zoom`

Not run: `plugins-lto`.

## `main` vs `plugins-fix-zoom`

| Row | main median [p95] | plugins-fix-zoom median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 51.9 [54.1] | 84.1 [86.8] | 1.619 [1.525, 1.684] | slower |
| open(ebook) / openedMs | 116.6 [117.8] | 184.3 [186.9] | 1.581 [1.550, 1.651] | slower |
| open(ebook) / firstPaintMs | 211.9 [217.9] | 316.9 [320.0] | 1.495 [1.437, 1.554] | slower |
| open(ebook) / settledMs | 332.8 [338.0] | 376.6 [379.5] | 1.132 [1.108, 1.149] | slower |
| open(ebook) / mainTaskMs | 115.2 [117.0] | 272.4 [275.3] | 2.364 [2.323, 2.422] | slower |
| open(ebook) / mainScriptMs | 19.9 [20.5] | 122.4 [122.6] | 6.158 [5.908, 6.434] | slower |
| open(large) / readyMs | 52.0 [54.3] | 84.1 [85.6] | 1.617 [1.530, 1.660] | slower |
| open(large) / openedMs | 133.1 [134.4] | 211.1 [213.2] | 1.585 [1.568, 1.631] | slower |
| open(large) / firstPaintMs | 223.0 [224.8] | 375.3 [379.2] | 1.683 [1.658, 1.715] | slower |
| open(large) / settledMs | 218.4 [221.5] | 393.4 [393.7] | 1.802 [1.746, 1.857] | slower |
| open(large) / mainTaskMs | 257.4 [259.4] | 575.2 [575.5] | 2.234 [2.183, 2.242] | slower |
| open(large) / mainScriptMs | 23.9 [24.2] | 388.4 [390.1] | 16.265 [15.908, 17.433] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2008.4 [2019.1] | 2844.8 [2896.8] | 1.416 [1.392, 1.450] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.4] | 16.7 [16.7] | 1.995 [1.994, 1.995] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 402.1 [411.2] | 1936.7 [2016.5] | 4.816 [4.639, 5.071] | slower |
| scroll / mainScriptMs | 54.6 [55.5] | 1368.8 [1430.9] | 25.086 [24.133, 26.439] | slower |
| jump / settleMs | 102.6 [105.3] | 144.6 [152.2] | +42.0 [+27.1, +61.3] | slower |
| jump / mainTaskMs | 59.0 [60.9] | 394.8 [414.3] | 6.687 [6.383, 7.112] | slower |
| jump / mainScriptMs | 15.1 [15.2] | 333.6 [348.0] | 22.151 [21.499, 24.758] | slower |
| zoom / settleMs | 379.5 [412.1] | 459.9 [465.5] | +80.3 [+27.3, +94.8] | slower |
| zoom / zoom2SettleMs | 131.5 [133.3] | 166.1 [168.7] | +34.6 [+24.8, +48.8] | slower |
| zoom / zoom4SettleMs | 251.2 [279.1] | 293.8 [296.9] | +42.6 [+2.5, +49.2] | slower |
| zoom / backTo1SettleMs | 74.2 [85.7] | 95.6 [102.9] | +21.4 [+0.7, +35.3] | slower |
| zoom / mainTaskMs | 173.9 [177.9] | 356.6 [376.4] | 2.050 [1.990, 2.213] | slower |
| zoom / mainScriptMs | 25.3 [27.0] | 169.0 [172.6] | 6.682 [6.030, 7.212] | slower |
| select / settleMs | 143.9 [151.2] | 150.0 [154.8] | +6.1 [-10.7, +12.1] | no detectable difference |
| select / mainTaskMs | 43.6 [45.1] | 88.6 [90.2] | 2.031 [1.811, 2.097] | slower |
| select / mainScriptMs | 9.4 [10.3] | 47.8 [49.3] | 5.092 [4.298, 5.318] | slower |
| search / searchMs | 56.8 [57.8] | 62.7 [74.2] | 1.104 [0.925, 1.341] | no detectable difference |
| search / mainTaskMs | 5.2 [5.2] | 14.2 [25.2] | 2.738 [2.449, 5.331] | unreliable (A/A failed) |
| search / mainScriptMs | 0.2 [0.2] | 7.7 [10.2] | 42.713 [32.887, 57.961] | unreliable (A/A failed) |
| annotate / settleMs | 155.4 [155.7] | 166.3 [168.1] | +11.0 [+10.2, +14.0] | slower |
| annotate / mainTaskMs | 54.9 [55.6] | 106.5 [107.3] | 1.940 [1.864, 1.993] | slower |
| annotate / mainScriptMs | 10.7 [11.1] | 55.3 [55.5] | 5.182 [4.817, 5.264] | slower |

## `plugins-fix-zoom` vs `plugins-wire`

| Row | plugins-fix-zoom median [p95] | plugins-wire median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 84.1 [86.8] | 84.1 [84.6] | 1.001 [0.958, 1.021] | no detectable difference |
| open(ebook) / openedMs | 184.3 [186.9] | 183.7 [186.2] | 0.997 [0.978, 1.020] | no detectable difference |
| open(ebook) / firstPaintMs | 316.9 [320.0] | 318.3 [318.9] | 1.004 [0.979, 1.016] | no detectable difference |
| open(ebook) / settledMs | 376.6 [379.5] | 371.4 [374.5] | 0.986 [0.978, 0.999] | no detectable difference |
| open(ebook) / mainTaskMs | 272.4 [275.3] | 265.7 [265.8] | 0.975 [0.935, 0.976] | no detectable difference |
| open(ebook) / mainScriptMs | 122.4 [122.6] | 115.0 [116.5] | 0.939 [0.910, 0.961] | no detectable difference |
| open(large) / readyMs | 84.1 [85.6] | 83.7 [85.5] | 0.995 [0.957, 1.025] | no detectable difference |
| open(large) / openedMs | 211.1 [213.2] | 210.2 [211.7] | 0.996 [0.968, 1.004] | no detectable difference |
| open(large) / firstPaintMs | 375.3 [379.2] | 357.1 [365.8] | 0.952 [0.927, 0.983] | faster |
| open(large) / settledMs | 393.4 [393.7] | 359.5 [365.8] | 0.914 [0.907, 0.946] | faster |
| open(large) / mainTaskMs | 575.2 [575.5] | 533.9 [545.6] | 0.928 [0.927, 0.965] | faster |
| open(large) / mainScriptMs | 388.4 [390.1] | 346.1 [353.7] | 0.891 [0.887, 0.918] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2844.8 [2896.8] | 2535.7 [2535.8] | 0.891 [0.873, 0.902] | faster |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 16.7 [16.7] | 8.4 [8.4] | 0.501 [0.501, 0.506] | faster |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 1936.7 [2016.5] | 1097.4 [1099.5] | 0.567 [0.536, 0.575] | faster |
| scroll / mainScriptMs | 1368.8 [1430.9] | 604.0 [605.3] | 0.441 [0.415, 0.451] | faster |
| jump / settleMs | 144.6 [152.2] | 104.1 [118.8] | -40.5 [-51.0, -12.2] | faster |
| jump / mainTaskMs | 394.8 [414.3] | 372.2 [373.6] | 0.943 [0.863, 0.959] | no detectable difference |
| jump / mainScriptMs | 333.6 [348.0] | 299.0 [302.0] | 0.896 [0.824, 0.925] | no detectable difference |
| zoom / settleMs | 459.9 [465.5] | 439.6 [454.8] | -20.2 [-31.2, +13.5] | no detectable difference |
| zoom / zoom2SettleMs | 166.1 [168.7] | 162.6 [167.8] | -3.5 [-9.6, +10.1] | no detectable difference |
| zoom / zoom4SettleMs | 293.8 [296.9] | 277.0 [287.0] | -16.7 [-21.6, +3.4] | no detectable difference |
| zoom / backTo1SettleMs | 95.6 [102.9] | 94.6 [106.4] | -1.0 [-12.5, +20.1] | no detectable difference |
| zoom / mainTaskMs | 356.6 [376.4] | 333.4 [347.1] | 0.935 [0.865, 0.982] | faster |
| zoom / mainScriptMs | 169.0 [172.6] | 141.3 [146.4] | 0.836 [0.804, 0.897] | faster |
| select / settleMs | 150.0 [154.8] | 141.4 [144.5] | -8.6 [-16.5, +3.5] | no detectable difference |
| select / mainTaskMs | 88.6 [90.2] | 78.9 [80.5] | 0.891 [0.835, 0.985] | no detectable difference |
| select / mainScriptMs | 47.8 [49.3] | 41.7 [42.4] | 0.871 [0.807, 0.948] | no detectable difference |
| search / searchMs | 62.7 [74.2] | 72.8 [73.0] | 1.161 [0.888, 1.362] | no detectable difference |
| search / mainTaskMs | 14.2 [25.2] | 22.7 [22.7] | 1.591 [0.791, 1.771] | unreliable (A/A failed) |
| search / mainScriptMs | 7.7 [10.2] | 9.0 [9.9] | 1.164 [0.786, 1.428] | unreliable (A/A failed) |
| annotate / settleMs | 166.3 [168.1] | 168.0 [170.7] | +1.6 [-4.8, +5.0] | no detectable difference |
| annotate / mainTaskMs | 106.5 [107.3] | 97.0 [98.1] | 0.911 [0.894, 0.947] | faster |
| annotate / mainScriptMs | 55.3 [55.5] | 48.7 [49.3] | 0.880 [0.839, 0.920] | no detectable difference |

## `plugins-wire` vs `plugins-getstate`

Not run: `plugins-getstate`.

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-fix-zoom median [p95] | plugins-wire median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.34 MiB [34.35 MiB] | 41.56 MiB [41.56 MiB] | 41.55 MiB [41.56 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.14 MiB [48.17 MiB] | 48.15 MiB [48.16 MiB] | 57.25 MiB [57.27 MiB] | 57.06 MiB [57.07 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] |
| soak / heapCycle5Bytes | 37.42 MiB [37.43 MiB] | 37.42 MiB [37.43 MiB] | 44.36 MiB [44.37 MiB] | 44.35 MiB [44.35 MiB] |
| soak / heapCycle30Bytes | 38.26 MiB [38.28 MiB] | 38.27 MiB [38.27 MiB] | 45.03 MiB [45.03 MiB] | 45.01 MiB [45.01 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-fix-zoom`:

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

`plugins-wire`:

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

