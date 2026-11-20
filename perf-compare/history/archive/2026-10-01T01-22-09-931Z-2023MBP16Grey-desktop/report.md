# Viewer perf comparison: 2026-10-01T01-22-09-931Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 2be8c88cb57f0f666fabd32157b913ab39dbb338 |
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
| impl-ts | research/multi-platform-rust-core-impl | c8085bd3c | built (fork d045f389d, wasm32-eh) | 0f7407599d93 |  |
| impl-gate | research/rust-core-toolbar-gate | 30f4295d7 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 |  |

## Gate

- `main`: pass
- `main-aa`: pass
- `impl-ts`: pass
- `impl-gate`: pass

## Sizes

| Metric | main | main-aa | impl-ts | impl-gate |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 0.00 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 0.00 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 3.35 MiB | 3.35 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.96 MiB | 0.96 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

39 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.2 [53.1] | 52.1 [52.3] | 0.997 [0.969, 1.011] | pass |
| open(ebook) / openedMs | 116.3 [116.5] | 114.8 [116.6] | 0.986 [0.982, 1.015] | pass |
| open(ebook) / firstPaintMs | 214.8 [216.8] | 210.7 [218.0] | 0.981 [0.963, 1.029] | pass |
| open(ebook) / settledMs | 335.5 [335.5] | 334.0 [339.1] | 0.996 [0.980, 1.020] | pass |
| open(ebook) / mainTaskMs | 117.5 [118.7] | 118.7 [119.1] | 1.010 [0.966, 1.016] | pass |
| open(ebook) / mainJsMs | 91.3 [92.1] | 93.2 [93.6] | 1.020 [0.966, 1.029] | pass |
| open(large) / readyMs | 51.6 [53.0] | 52.8 [54.4] | 1.022 [0.987, 1.064] | pass |
| open(large) / openedMs | 134.4 [135.5] | 134.6 [135.3] | 1.002 [0.987, 1.014] | pass |
| open(large) / firstPaintMs | 224.4 [227.1] | 225.8 [230.8] | 1.006 [0.979, 1.094] | pass |
| open(large) / settledMs | 221.3 [224.4] | 223.8 [227.7] | 1.012 [0.977, 1.052] | pass |
| open(large) / mainTaskMs | 263.6 [264.6] | 264.0 [266.1] | 1.001 [0.987, 1.011] | pass |
| open(large) / mainJsMs | 240.4 [240.7] | 240.4 [242.4] | 1.000 [0.987, 1.011] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2016.2 [2037.7] | 2026.0 [2027.8] | 1.005 [0.979, 1.014] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.5 [9.3] | 8.7 [8.9] | 1.023 [0.918, 1.053] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 408.5 [409.9] | 409.0 [411.5] | 1.001 [0.964, 1.038] | pass |
| scroll / mainJsMs | 315.2 [316.9] | 315.4 [318.7] | 1.001 [0.963, 1.042] | pass |
| jump / settleMs | 97.5 [100.6] | 97.4 [99.5] | -0.1 [-4.3, +4.5] | pass |
| jump / mainTaskMs | 57.6 [57.8] | 53.9 [56.4] | 0.935 [0.927, 1.090] | pass |
| jump / mainJsMs | 41.8 [45.4] | 39.9 [41.4] | 0.954 [0.868, 1.078] | pass |
| zoom / settleMs | 398.3 [401.5] | 389.5 [408.9] | -8.8 [-32.8, +33.5] | pass |
| zoom / zoom2SettleMs | 124.3 [131.9] | 125.2 [132.2] | +0.9 [-8.3, +9.3] | pass |
| zoom / zoom4SettleMs | 269.0 [273.5] | 256.4 [283.5] | -12.6 [-30.2, +32.7] | pass |
| zoom / backTo1SettleMs | 70.7 [90.9] | 75.6 [76.2] | +5.0 [-23.6, +7.3] | pass |
| zoom / mainTaskMs | 173.4 [183.6] | 180.1 [184.9] | 1.038 [0.962, 1.101] | pass |
| zoom / mainJsMs | 126.1 [134.0] | 131.1 [136.5] | 1.039 [0.951, 1.128] | pass |
| select / settleMs | 147.3 [147.5] | 147.6 [148.7] | +0.3 [-2.8, +2.1] | pass |
| select / mainTaskMs | 39.6 [42.3] | 44.6 [47.1] | 1.128 [0.921, 1.246] | pass |
| select / mainJsMs | 24.3 [26.4] | 28.0 [28.8] | 1.154 [0.894, 1.259] | pass |
| search / searchMs | 58.2 [58.4] | 59.5 [61.3] | 1.022 [0.955, 1.248] | pass |
| search / mainTaskMs | 4.8 [5.0] | 4.6 [4.7] | 0.953 [0.915, 1.110] | pass |
| search / mainJsMs | 3.1 [3.2] | 3.2 [3.2] | 1.048 [0.953, 1.102] | pass |
| annotate / settleMs | 160.4 [161.6] | 157.3 [160.7] | -3.1 [-8.8, +7.4] | pass |
| annotate / mainTaskMs | 52.8 [55.4] | 55.7 [58.7] | 1.054 [0.936, 1.126] | pass |
| annotate / mainJsMs | 31.6 [33.4] | 33.3 [35.3] | 1.054 [0.939, 1.127] | pass |

## `main` vs `impl-ts`

| Row | main median [p95] | impl-ts median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.2 [53.1] | 51.4 [52.0] | 0.984 [0.963, 1.005] | no detectable difference |
| open(ebook) / openedMs | 116.3 [116.5] | 118.7 [122.4] | 1.021 [1.004, 1.068] | no detectable difference |
| open(ebook) / firstPaintMs | 214.8 [216.8] | 215.9 [219.3] | 1.005 [0.987, 1.033] | no detectable difference |
| open(ebook) / settledMs | 335.5 [335.5] | 340.1 [343.3] | 1.014 [1.014, 1.032] | no detectable difference |
| open(ebook) / mainTaskMs | 117.5 [118.7] | 116.4 [117.6] | 0.991 [0.978, 1.005] | no detectable difference |
| open(ebook) / mainJsMs | 91.3 [92.1] | 90.5 [91.4] | 0.991 [0.978, 1.005] | no detectable difference |
| open(large) / readyMs | 51.6 [53.0] | 51.9 [54.1] | 1.004 [0.962, 1.060] | no detectable difference |
| open(large) / openedMs | 134.4 [135.5] | 137.3 [142.6] | 1.022 [1.006, 1.073] | no detectable difference |
| open(large) / firstPaintMs | 224.4 [227.1] | 230.9 [234.3] | 1.029 [1.007, 1.109] | no detectable difference |
| open(large) / settledMs | 221.3 [224.4] | 227.6 [231.9] | 1.029 [1.004, 1.072] | no detectable difference |
| open(large) / mainTaskMs | 263.6 [264.6] | 264.3 [264.9] | 1.002 [0.993, 1.006] | no detectable difference |
| open(large) / mainJsMs | 240.4 [240.7] | 240.8 [240.9] | 1.002 [0.993, 1.004] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2016.2 [2037.7] | 2024.5 [2056.5] | 1.004 [0.990, 1.030] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.5 [9.3] | 8.6 [8.9] | 1.009 [0.916, 1.054] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 408.5 [409.9] | 400.2 [401.4] | 0.980 [0.964, 1.012] | no detectable difference |
| scroll / mainJsMs | 315.2 [316.9] | 308.7 [310.3] | 0.979 [0.960, 1.014] | no detectable difference |
| jump / settleMs | 97.5 [100.6] | 97.1 [99.2] | -0.4 [-10.6, +4.2] | no detectable difference |
| jump / mainTaskMs | 57.6 [57.8] | 60.6 [61.1] | 1.051 [0.942, 1.176] | no detectable difference |
| jump / mainJsMs | 41.8 [45.4] | 42.7 [43.4] | 1.022 [0.868, 1.127] | no detectable difference |
| zoom / settleMs | 398.3 [401.5] | 410.6 [412.0] | +12.3 [-3.1, +34.6] | no detectable difference |
| zoom / zoom2SettleMs | 124.3 [131.9] | 132.1 [132.7] | +7.8 [-1.7, +9.0] | no detectable difference |
| zoom / zoom4SettleMs | 269.0 [273.5] | 277.8 [280.7] | +8.8 [-7.4, +27.2] | no detectable difference |
| zoom / backTo1SettleMs | 70.7 [90.9] | 88.0 [89.6] | +17.3 [-17.3, +20.8] | no detectable difference |
| zoom / mainTaskMs | 173.4 [183.6] | 180.0 [182.6] | 1.038 [0.955, 1.086] | no detectable difference |
| zoom / mainJsMs | 126.1 [134.0] | 132.6 [134.2] | 1.051 [0.952, 1.106] | no detectable difference |
| select / settleMs | 147.3 [147.5] | 147.4 [149.0] | +0.1 [-5.1, +2.5] | no detectable difference |
| select / mainTaskMs | 39.6 [42.3] | 44.3 [45.0] | 1.120 [0.921, 1.185] | no detectable difference |
| select / mainJsMs | 24.3 [26.4] | 27.1 [29.0] | 1.117 [0.897, 1.270] | no detectable difference |
| search / searchMs | 58.2 [58.4] | 52.0 [57.7] | 0.893 [0.821, 1.184] | no detectable difference |
| search / mainTaskMs | 4.8 [5.0] | 4.5 [4.6] | 0.941 [0.844, 1.096] | no detectable difference |
| search / mainJsMs | 3.1 [3.2] | 3.0 [3.1] | 0.994 [0.935, 1.067] | no detectable difference |
| annotate / settleMs | 160.4 [161.6] | 160.7 [168.4] | +0.3 [-2.6, +15.5] | no detectable difference |
| annotate / mainTaskMs | 52.8 [55.4] | 58.6 [59.5] | 1.109 [0.976, 1.137] | no detectable difference |
| annotate / mainJsMs | 31.6 [33.4] | 34.4 [35.1] | 1.088 [0.945, 1.118] | no detectable difference |

## `impl-ts` vs `impl-rust`

Not run: `impl-rust`.

## `impl-ts` vs `plugins`

Not run: `plugins`.

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

| Row | impl-ts median [p95] | impl-gate median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 51.4 [52.0] | 54.8 [55.7] | 1.067 [1.008, 1.088] | slower |
| open(ebook) / openedMs | 118.7 [122.4] | 122.8 [125.2] | 1.035 [0.982, 1.072] | no detectable difference |
| open(ebook) / firstPaintMs | 215.9 [219.3] | 219.2 [226.2] | 1.015 [0.985, 1.060] | no detectable difference |
| open(ebook) / settledMs | 340.1 [343.3] | 345.9 [347.6] | 1.017 [1.002, 1.023] | no detectable difference |
| open(ebook) / mainTaskMs | 116.4 [117.6] | 108.4 [111.0] | 0.931 [0.919, 0.957] | faster |
| open(ebook) / mainJsMs | 90.5 [91.4] | 82.8 [84.7] | 0.915 [0.896, 0.942] | faster |
| open(large) / readyMs | 51.9 [54.1] | 52.8 [53.2] | 1.019 [0.972, 1.042] | no detectable difference |
| open(large) / openedMs | 137.3 [142.6] | 142.7 [142.7] | 1.039 [0.995, 1.046] | no detectable difference |
| open(large) / firstPaintMs | 230.9 [234.3] | 231.6 [233.6] | 1.003 [0.953, 1.020] | no detectable difference |
| open(large) / settledMs | 227.6 [231.9] | 227.4 [228.5] | 0.999 [0.942, 1.013] | no detectable difference |
| open(large) / mainTaskMs | 264.3 [264.9] | 163.8 [164.7] | 0.620 [0.608, 0.627] | faster |
| open(large) / mainJsMs | 240.8 [240.9] | 137.0 [138.1] | 0.569 [0.561, 0.578] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2024.5 [2056.5] | 2002.5 [2013.0] | 0.989 [0.970, 0.998] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.6 [8.9] | 8.6 [8.6] | 0.993 [0.932, 1.001] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 400.2 [401.4] | 337.3 [338.4] | 0.843 [0.816, 0.856] | faster |
| scroll / mainJsMs | 308.7 [310.3] | 231.7 [235.7] | 0.751 [0.717, 0.775] | faster |
| jump / settleMs | 97.1 [99.2] | 81.1 [89.2] | -15.9 [-19.0, -0.2] | faster |
| jump / mainTaskMs | 60.6 [61.1] | 50.6 [55.1] | 0.835 [0.787, 1.019] | no detectable difference |
| jump / mainJsMs | 42.7 [43.4] | 32.5 [32.7] | 0.760 [0.737, 0.821] | faster |
| zoom / settleMs | 410.6 [412.0] | 395.3 [397.3] | -15.3 [-28.1, -1.2] | no detectable difference |
| zoom / zoom2SettleMs | 132.1 [132.7] | 132.7 [135.0] | +0.6 [-0.4, +4.2] | no detectable difference |
| zoom / zoom4SettleMs | 277.8 [280.7] | 260.0 [264.4] | -17.8 [-29.4, -1.8] | no detectable difference |
| zoom / backTo1SettleMs | 88.0 [89.6] | 70.9 [72.2] | -17.1 [-22.2, -3.5] | no detectable difference |
| zoom / mainTaskMs | 180.0 [182.6] | 153.9 [161.1] | 0.855 [0.814, 0.918] | faster |
| zoom / mainJsMs | 132.6 [134.2] | 104.5 [105.0] | 0.788 [0.753, 0.818] | faster |
| select / settleMs | 147.4 [149.0] | 142.1 [150.4] | -5.3 [-7.6, +8.9] | no detectable difference |
| select / mainTaskMs | 44.3 [45.0] | 38.3 [41.2] | 0.864 [0.821, 1.060] | no detectable difference |
| select / mainJsMs | 27.1 [29.0] | 19.5 [21.1] | 0.718 [0.632, 0.889] | faster |
| search / searchMs | 52.0 [57.7] | 48.8 [63.2] | 0.938 [0.804, 1.349] | no detectable difference |
| search / mainTaskMs | 4.5 [4.6] | 2.8 [2.9] | 0.626 [0.542, 0.687] | faster |
| search / mainJsMs | 3.0 [3.1] | 1.2 [1.2] | 0.386 [0.347, 0.419] | faster |
| annotate / settleMs | 160.7 [168.4] | 158.3 [158.3] | -2.4 [-14.7, -0.8] | no detectable difference |
| annotate / mainTaskMs | 58.6 [59.5] | 50.2 [50.2] | 0.858 [0.830, 0.925] | faster |
| annotate / mainJsMs | 34.4 [35.1] | 24.8 [25.0] | 0.723 [0.698, 0.789] | faster |

## Memory

| Row | main median [p95] | main-aa median [p95] | impl-ts median [p95] | impl-gate median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.36 MiB [35.25 MiB] | 35.03 MiB [35.03 MiB] | 34.87 MiB [34.87 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.00 MiB [23.00 MiB] | 23.00 MiB [23.00 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.16 MiB] | 48.15 MiB [48.16 MiB] | 48.83 MiB [48.84 MiB] | 48.56 MiB [48.58 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.99 MiB [22.99 MiB] | 22.99 MiB [23.01 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.41 MiB] | 37.42 MiB [37.43 MiB] | 38.19 MiB [38.19 MiB] | 38.03 MiB [38.03 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.27 MiB] | 38.27 MiB [38.27 MiB] | 39.04 MiB [39.04 MiB] | 38.75 MiB [38.76 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] |
