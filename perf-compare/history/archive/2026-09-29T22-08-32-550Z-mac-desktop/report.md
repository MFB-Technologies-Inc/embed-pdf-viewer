# Viewer perf comparison: 2026-09-29T22-08-32-550Z-mac-desktop

3 rounds, profile `desktop` (CPU throttle 1x), **quick, not for publication**.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 29d271020f95ed4b076ae83f493b849aeaa53be8 (uncommitted changes) |
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
| plugins-fix | research/rust-kernel-state-cache | e0f33b1c4 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | b660fde1b0fb |
| plugins-fix34 | research/rust-kernel-state-cache | 2b07fb371 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | c9c67a1d2747 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-fix`: pass
- `plugins-fix34`: pass

## Sizes

| Metric | main | main-aa | plugins-fix | plugins-fix34 |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 11.68 MiB | 11.66 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.88 MiB | 1.88 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.62 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

34 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 45.0 [45.2] | 42.6 [45.4] | 0.947 [0.938, 1.064] | pass |
| open(ebook) / openedMs | 96.2 [96.6] | 95.5 [97.9] | 0.993 [0.985, 1.025] | pass |
| open(ebook) / firstPaintMs | 173.7 [175.7] | 177.6 [180.3] | 1.023 [0.966, 1.049] | pass |
| open(ebook) / settledMs | 263.7 [263.9] | 265.8 [268.3] | 1.008 [1.004, 1.020] | pass |
| open(ebook) / mainTaskMs | 93.4 [96.9] | 94.9 [96.3] | 1.016 [0.963, 1.053] | pass |
| open(ebook) / mainScriptMs | 15.4 [16.3] | 16.3 [16.3] | 1.058 [0.958, 1.058] | pass |
| open(large) / readyMs | 45.8 [46.6] | 44.9 [46.1] | 0.979 [0.945, 1.098] | pass |
| open(large) / openedMs | 112.8 [113.6] | 110.1 [112.9] | 0.976 [0.965, 1.043] | pass |
| open(large) / firstPaintMs | 180.2 [182.7] | 180.3 [180.4] | 1.000 [0.985, 1.018] | pass |
| open(large) / settledMs | 176.0 [178.3] | 176.6 [176.9] | 1.003 [0.984, 1.022] | pass |
| open(large) / mainTaskMs | 206.2 [208.0] | 206.6 [208.3] | 1.002 [0.980, 1.012] | pass |
| open(large) / mainScriptMs | 18.2 [18.5] | 18.1 [18.4] | 0.994 [0.958, 1.036] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2010.8 [2018.4] | 2010.3 [2015.5] | 1.000 [0.992, 1.003] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [8.5] | 8.6 [9.1] | 1.017 [0.983, 1.091] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 346.3 [367.1] | 345.4 [362.6] | 0.997 [0.917, 1.060] | pass |
| scroll / mainScriptMs | 47.2 [47.6] | 47.2 [48.0] | 1.000 [0.970, 1.018] | pass |
| jump / settleMs | 77.5 [80.4] | 76.2 [82.4] | -1.3 [-5.1, +8.6] | pass |
| jump / mainTaskMs | 47.8 [51.4] | 47.2 [47.7] | 0.988 [0.853, 1.032] | pass |
| jump / mainScriptMs | 12.2 [12.2] | 12.2 [12.6] | 1.001 [0.967, 1.039] | pass |
| zoom / settleMs | 276.6 [297.2] | 290.3 [299.7] | +13.6 [-21.7, +24.8] | pass |
| zoom / zoom2SettleMs | 108.7 [110.3] | 110.1 [112.3] | +1.3 [-0.6, +11.2] | pass |
| zoom / zoom4SettleMs | 174.6 [189.1] | 180.2 [189.8] | +5.6 [-25.5, +24.8] | pass |
| zoom / backTo1SettleMs | 59.4 [60.0] | 58.7 [70.1] | -0.8 [-2.2, +12.4] | pass |
| zoom / mainTaskMs | 139.0 [144.4] | 140.3 [141.1] | 1.010 [0.964, 1.027] | pass |
| zoom / mainScriptMs | 19.6 [19.6] | 19.5 [21.7] | 0.997 [0.972, 1.122] | pass |
| select / settleMs | 144.7 [145.1] | 140.1 [145.1] | -4.6 [-5.6, +7.0] | pass |
| select / mainTaskMs | 36.7 [39.0] | 41.5 [43.2] | 1.130 [1.011, 1.310] | A/A failed |
| select / mainScriptMs | 8.2 [9.1] | 9.6 [10.4] | 1.172 [1.025, 1.410] | A/A failed |
| search / searchMs | 49.6 [50.4] | 51.5 [52.2] | 1.038 [1.011, 1.087] | A/A failed |
| search / mainTaskMs | 3.9 [4.1] | 4.0 [4.2] | 1.024 [0.946, 1.109] | pass |
| search / mainScriptMs | 0.1 [0.1] | 0.1 [0.1] | 0.913 [0.840, 1.093] | pass |
| annotate / settleMs | 153.6 [153.8] | 154.1 [155.1] | +0.6 [-1.6, +3.1] | pass |
| annotate / mainTaskMs | 50.3 [51.8] | 48.7 [49.2] | 0.967 [0.894, 0.990] | A/A failed |
| annotate / mainScriptMs | 10.6 [11.1] | 9.7 [10.2] | 0.923 [0.857, 0.987] | A/A failed |

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

Not run: `plugins-lto`.

## `plugins-fix` vs `plugins-fix34`

| Row | plugins-fix median [p95] | plugins-fix34 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 70.2 [73.1] | 69.9 [71.6] | 0.996 [0.922, 1.035] | no detectable difference |
| open(ebook) / openedMs | 151.3 [153.5] | 150.5 [151.5] | 0.995 [0.953, 1.009] | no detectable difference |
| open(ebook) / firstPaintMs | 272.2 [273.1] | 262.7 [271.9] | 0.965 [0.959, 1.040] | no detectable difference |
| open(ebook) / settledMs | 368.5 [369.1] | 356.7 [364.3] | 0.968 [0.961, 1.021] | no detectable difference |
| open(ebook) / mainTaskMs | 243.8 [247.7] | 239.5 [245.4] | 0.982 [0.961, 1.015] | no detectable difference |
| open(ebook) / mainScriptMs | 104.2 [105.3] | 101.5 [103.2] | 0.974 [0.943, 0.996] | no detectable difference |
| open(large) / readyMs | 74.2 [75.5] | 69.5 [72.2] | 0.937 [0.915, 1.053] | no detectable difference |
| open(large) / openedMs | 909.5 [915.2] | 170.4 [172.2] | 0.187 [0.185, 0.192] | faster |
| open(large) / firstPaintMs | 1230.2 [1247.0] | 298.1 [306.5] | 0.242 [0.239, 0.251] | faster |
| open(large) / settledMs | 1250.5 [1265.1] | 318.6 [322.4] | 0.255 [0.250, 0.260] | faster |
| open(large) / mainTaskMs | 1471.3 [1492.0] | 524.4 [528.4] | 0.356 [0.346, 0.361] | faster |
| open(large) / mainScriptMs | 1286.6 [1305.3] | 345.5 [345.7] | 0.269 [0.259, 0.270] | faster |
| scroll / settleMs | 3.0 [3.2] | 0.0 [0.0] | -3.0 [-3.2, -1.1] | faster |
| scroll / wallMs | 5875.4 [5915.4] | 2803.9 [2828.1] | 0.477 [0.469, 0.485] | faster |
| scroll / frameP50Ms | 8.3 [8.4] | 8.3 [8.3] | 0.999 [0.988, 0.999] | no detectable difference |
| scroll / frameP95Ms | 66.7 [66.7] | 16.7 [16.7] | 0.250 [0.249, 0.286] | faster |
| scroll / jankFrames | 38 [42.50] | 0 [0] | -38 [-43, -38] | faster |
| scroll / longTasks | 27 [28.80] | 0 [0] | -27 [-29, -26] | faster |
| scroll / longTaskMs | 1533.0 [1605.0] | 0.0 [0.0] | -1533.0 [-1613.0, -1424.0] | faster |
| scroll / mainTaskMs | 6169.1 [6208.5] | 1749.8 [1757.3] | 0.284 [0.281, 0.287] | faster |
| scroll / mainScriptMs | 4415.9 [4443.6] | 1143.2 [1149.4] | 0.259 [0.257, 0.264] | faster |
| jump / settleMs | 267.0 [319.3] | 129.6 [133.6] | -137.4 [-203.4, -118.5] | faster |
| jump / mainTaskMs | 548.3 [598.0] | 387.9 [402.3] | 0.707 [0.640, 0.742] | faster |
| jump / mainScriptMs | 445.5 [491.5] | 308.9 [323.0] | 0.693 [0.620, 0.735] | faster |
| zoom / settleMs | 424.7 [443.9] | 425.7 [431.5] | +1.0 [-30.4, +14.2] | no detectable difference |
| zoom / zoom2SettleMs | 156.9 [158.6] | 148.3 [156.4] | -8.6 [-12.6, +8.3] | no detectable difference |
| zoom / zoom4SettleMs | 269.1 [285.4] | 274.9 [277.1] | +5.8 [-17.8, +9.5] | no detectable difference |
| zoom / backTo1SettleMs | 86.4 [90.4] | 84.8 [84.8] | -1.5 [-6.1, -0.2] | no detectable difference |
| zoom / mainTaskMs | 362.8 [364.8] | 348.2 [348.5] | 0.960 [0.952, 0.965] | faster |
| zoom / mainScriptMs | 161.9 [162.1] | 150.4 [151.2] | 0.929 [0.923, 0.943] | no detectable difference |
| select / settleMs | 146.8 [147.4] | 146.8 [152.8] | +0.0 [-1.5, +8.8] | no detectable difference |
| select / mainTaskMs | 91.6 [94.9] | 90.7 [90.9] | 0.990 [0.929, 1.053] | unreliable (A/A failed) |
| select / mainScriptMs | 47.5 [49.6] | 44.2 [44.6] | 0.930 [0.877, 0.998] | unreliable (A/A failed) |
| search / searchMs | 56.1 [56.8] | 54.4 [55.0] | 0.970 [0.919, 1.106] | unreliable (A/A failed) |
| search / mainTaskMs | 15.8 [22.3] | 15.0 [15.4] | 0.949 [0.612, 1.114] | no detectable difference |
| search / mainScriptMs | 7.9 [9.4] | 7.1 [7.3] | 0.899 [0.745, 1.030] | no detectable difference |
| annotate / settleMs | 166.3 [167.0] | 165.3 [165.6] | -1.0 [-3.4, +0.7] | no detectable difference |
| annotate / mainTaskMs | 112.1 [112.2] | 108.8 [109.2] | 0.970 [0.953, 0.977] | unreliable (A/A failed) |
| annotate / mainScriptMs | 54.8 [55.1] | 50.8 [50.8] | 0.927 [0.905, 0.933] | unreliable (A/A failed) |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-fix median [p95] | plugins-fix34 median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.34 MiB] | 34.34 MiB [34.34 MiB] | 43.02 MiB [43.03 MiB] | 43.03 MiB [43.03 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.17 MiB [48.19 MiB] | 48.16 MiB [48.17 MiB] | 58.85 MiB [58.85 MiB] | 59.01 MiB [59.02 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.28 MiB] | 22.26 MiB [22.26 MiB] | 22.94 MiB [22.94 MiB] | 22.94 MiB [22.94 MiB] |
| soak / heapCycle5Bytes | 37.42 MiB [37.43 MiB] | 37.42 MiB [37.43 MiB] | 48.78 MiB [48.79 MiB] | 48.78 MiB [48.79 MiB] |
| soak / heapCycle30Bytes | 38.28 MiB [38.29 MiB] | 38.28 MiB [38.28 MiB] | 62.74 MiB [62.74 MiB] | 62.74 MiB [62.75 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.56 MiB [0.56 MiB] | 0.56 MiB [0.56 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

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

