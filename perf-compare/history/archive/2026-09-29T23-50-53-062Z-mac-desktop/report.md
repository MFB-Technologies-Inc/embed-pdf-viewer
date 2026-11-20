# Viewer perf comparison: 2026-09-29T23-50-53-062Z-mac-desktop

3 rounds, profile `desktop` (CPU throttle 1x), **quick, not for publication**.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 7d4bd2fb8a6db3a92acc0b60f033e05c47edcf7d (uncommitted changes) |
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
| plugins-fix34 | research/rust-kernel-state-cache | 2b07fb371 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | c9c67a1d2747 |
| plugins-fix-leak | research/rust-kernel-state-cache | 2ed312446 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 4dff8a45fd6f |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-fix34`: pass
- `plugins-fix-leak`: pass

## Sizes

| Metric | main | main-aa | plugins-fix34 | plugins-fix-leak |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 11.66 MiB | 11.68 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.88 MiB | 1.88 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.62 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

35 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 44.4 [383.7] | 43.0 [323.4] | 0.967 [0.101, 8.233] | pass |
| open(ebook) / openedMs | 95.3 [443.9] | 95.5 [402.7] | 1.002 [0.194, 4.619] | pass |
| open(ebook) / firstPaintMs | 176.2 [528.3] | 175.8 [495.1] | 0.998 [0.298, 3.110] | pass |
| open(ebook) / settledMs | 265.7 [630.2] | 268.9 [598.4] | 1.012 [0.393, 2.402] | pass |
| open(ebook) / mainTaskMs | 94.0 [108.3] | 94.1 [115.5] | 1.001 [0.846, 1.279] | pass |
| open(ebook) / mainScriptMs | 16.0 [18.9] | 15.8 [19.6] | 0.988 [0.807, 1.303] | pass |
| open(large) / readyMs | 46.7 [46.9] | 44.9 [47.6] | 0.962 [0.920, 1.103] | pass |
| open(large) / openedMs | 115.1 [119.8] | 113.2 [122.3] | 0.983 [0.906, 1.138] | pass |
| open(large) / firstPaintMs | 180.8 [191.9] | 180.1 [193.6] | 0.996 [0.910, 1.092] | pass |
| open(large) / settledMs | 176.9 [188.0] | 176.1 [196.0] | 0.995 [0.907, 1.137] | pass |
| open(large) / mainTaskMs | 211.9 [230.4] | 206.1 [230.8] | 0.973 [0.882, 1.133] | pass |
| open(large) / mainScriptMs | 18.9 [20.6] | 18.4 [20.9] | 0.973 [0.861, 1.180] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2000.8 [2016.0] | 2001.8 [2008.9] | 1.000 [0.990, 1.005] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.001] | pass |
| scroll / frameP95Ms | 8.4 [8.5] | 8.5 [8.8] | 1.019 [0.982, 1.053] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 342.3 [359.9] | 330.9 [349.0] | 0.967 [0.850, 1.043] | pass |
| scroll / mainScriptMs | 46.2 [46.5] | 45.5 [49.3] | 0.985 [0.922, 1.089] | pass |
| jump / settleMs | 74.6 [79.6] | 74.3 [81.5] | -0.3 [-15.8, +12.6] | pass |
| jump / mainTaskMs | 46.2 [47.7] | 47.6 [48.8] | 1.031 [0.854, 1.077] | pass |
| jump / mainScriptMs | 11.8 [13.0] | 12.0 [12.4] | 1.014 [0.828, 1.092] | pass |
| zoom / settleMs | 285.1 [293.4] | 268.7 [271.9] | -16.4 [-26.6, -6.6] | A/A failed |
| zoom / zoom2SettleMs | 111.8 [112.0] | 103.7 [109.6] | -8.2 [-10.1, +2.1] | pass |
| zoom / zoom4SettleMs | 177.0 [182.0] | 164.1 [166.5] | -12.9 [-20.5, -0.0] | A/A failed |
| zoom / backTo1SettleMs | 53.8 [56.2] | 57.3 [58.9] | +3.5 [-4.4, +7.1] | pass |
| zoom / mainTaskMs | 137.6 [138.9] | 133.2 [135.5] | 0.968 [0.943, 0.999] | A/A failed |
| zoom / mainScriptMs | 22.7 [23.7] | 21.2 [23.0] | 0.936 [0.806, 1.061] | pass |
| select / settleMs | 140.3 [141.5] | 142.2 [145.6] | +1.9 [-0.2, +6.0] | pass |
| select / mainTaskMs | 38.7 [39.1] | 40.1 [42.8] | 1.037 [0.913, 1.280] | pass |
| select / mainScriptMs | 9.0 [9.2] | 9.1 [9.8] | 1.015 [0.854, 1.304] | pass |
| search / searchMs | 45.8 [48.2] | 49.4 [50.0] | 1.078 [1.013, 1.157] | A/A failed |
| search / mainTaskMs | 3.8 [4.2] | 4.0 [4.1] | 1.062 [0.901, 1.133] | pass |
| search / mainScriptMs | 0.1 [0.1] | 0.1 [0.1] | 1.110 [0.817, 1.422] | pass |
| annotate / settleMs | 154.5 [155.0] | 152.2 [154.4] | -2.4 [-3.0, +4.1] | pass |
| annotate / mainTaskMs | 49.6 [50.5] | 50.4 [51.1] | 1.015 [0.955, 1.245] | pass |
| annotate / mainScriptMs | 10.3 [10.5] | 10.3 [10.4] | 1.001 [0.921, 1.293] | pass |

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

Not run: `plugins-fix`.

## `plugins-fix34` vs `plugins-fix-leak`

| Row | plugins-fix34 median [p95] | plugins-fix-leak median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 69.2 [84.2] | 70.6 [83.0] | 1.020 [0.819, 1.237] | no detectable difference |
| open(ebook) / openedMs | 148.9 [182.0] | 150.3 [172.4] | 1.009 [0.808, 1.180] | no detectable difference |
| open(ebook) / firstPaintMs | 269.7 [320.4] | 262.5 [302.9] | 0.973 [0.799, 1.192] | no detectable difference |
| open(ebook) / settledMs | 356.1 [439.7] | 358.2 [418.7] | 1.006 [0.794, 1.205] | no detectable difference |
| open(ebook) / mainTaskMs | 238.4 [296.6] | 242.5 [279.1] | 1.017 [0.800, 1.208] | no detectable difference |
| open(ebook) / mainScriptMs | 99.3 [125.2] | 101.8 [119.3] | 1.025 [0.793, 1.245] | no detectable difference |
| open(large) / readyMs | 69.5 [77.5] | 72.2 [76.3] | 1.039 [0.877, 1.114] | no detectable difference |
| open(large) / openedMs | 170.9 [188.3] | 174.2 [189.7] | 1.020 [0.900, 1.126] | no detectable difference |
| open(large) / firstPaintMs | 305.1 [349.1] | 305.6 [329.3] | 1.002 [0.854, 1.113] | no detectable difference |
| open(large) / settledMs | 323.3 [358.2] | 317.9 [343.0] | 0.983 [0.864, 1.074] | no detectable difference |
| open(large) / mainTaskMs | 528.0 [559.8] | 523.4 [544.7] | 0.991 [0.917, 1.046] | no detectable difference |
| open(large) / mainScriptMs | 346.5 [359.1] | 342.3 [349.1] | 0.988 [0.933, 1.020] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2788.5 [2863.5] | 2735.5 [2780.9] | 0.981 [0.929, 1.018] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.000] | no detectable difference |
| scroll / frameP95Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [0.999, 1.001] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 1954.8 [1970.9] | 1726.2 [1958.7] | 0.883 [0.866, 1.107] | no detectable difference |
| scroll / mainScriptMs | 1292.2 [1307.1] | 1125.9 [1304.2] | 0.871 [0.855, 1.123] | no detectable difference |
| jump / settleMs | 119.4 [121.9] | 117.2 [133.4] | -2.2 [-10.8, +20.3] | no detectable difference |
| jump / mainTaskMs | 376.1 [384.7] | 367.4 [389.4] | 0.977 [0.930, 1.060] | no detectable difference |
| jump / mainScriptMs | 301.3 [307.3] | 292.8 [310.3] | 0.972 [0.925, 1.062] | no detectable difference |
| zoom / settleMs | 429.8 [431.2] | 415.5 [420.6] | -14.3 [-28.5, +16.1] | unreliable (A/A failed) |
| zoom / zoom2SettleMs | 156.0 [158.0] | 147.3 [148.6] | -8.7 [-18.3, -3.6] | no detectable difference |
| zoom / zoom4SettleMs | 271.6 [275.0] | 268.2 [272.0] | -3.4 [-12.5, +19.7] | unreliable (A/A failed) |
| zoom / backTo1SettleMs | 86.7 [87.6] | 85.2 [87.2] | -1.5 [-19.7, +2.4] | no detectable difference |
| zoom / mainTaskMs | 353.3 [365.2] | 347.8 [354.1] | 0.984 [0.949, 1.046] | unreliable (A/A failed) |
| zoom / mainScriptMs | 155.9 [161.4] | 154.9 [155.9] | 0.994 [0.941, 1.047] | no detectable difference |
| select / settleMs | 150.8 [150.8] | 143.2 [145.1] | -7.6 [-11.3, +2.3] | no detectable difference |
| select / mainTaskMs | 89.1 [91.5] | 87.1 [90.0] | 0.978 [0.918, 1.018] | no detectable difference |
| select / mainScriptMs | 44.1 [45.0] | 42.1 [45.1] | 0.955 [0.929, 1.042] | no detectable difference |
| search / searchMs | 56.0 [56.1] | 49.6 [52.4] | 0.885 [0.729, 0.942] | unreliable (A/A failed) |
| search / mainTaskMs | 14.9 [15.7] | 14.1 [14.7] | 0.944 [0.884, 1.015] | no detectable difference |
| search / mainScriptMs | 7.3 [7.9] | 7.3 [7.5] | 0.994 [0.855, 1.029] | no detectable difference |
| annotate / settleMs | 168.3 [169.2] | 166.8 [168.0] | -1.5 [-3.8, +8.7] | no detectable difference |
| annotate / mainTaskMs | 107.2 [108.5] | 105.1 [107.3] | 0.981 [0.956, 1.029] | no detectable difference |
| annotate / mainScriptMs | 50.1 [50.7] | 49.1 [50.1] | 0.980 [0.959, 1.021] | no detectable difference |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-fix34 median [p95] | plugins-fix-leak median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.35 MiB [34.35 MiB] | 34.33 MiB [34.35 MiB] | 43.02 MiB [43.03 MiB] | 41.54 MiB [41.55 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.15 MiB] | 48.17 MiB [48.17 MiB] | 59.00 MiB [59.02 MiB] | 57.57 MiB [57.59 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.94 MiB [22.94 MiB] | 22.94 MiB [22.94 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.42 MiB] | 37.42 MiB [37.42 MiB] | 48.80 MiB [49.70 MiB] | 44.37 MiB [44.37 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.27 MiB] | 38.27 MiB [38.28 MiB] | 62.75 MiB [63.65 MiB] | 45.05 MiB [45.05 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.56 MiB [0.56 MiB] | 0.03 MiB [0.03 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-fix34`:

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

`plugins-fix-leak`:

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

