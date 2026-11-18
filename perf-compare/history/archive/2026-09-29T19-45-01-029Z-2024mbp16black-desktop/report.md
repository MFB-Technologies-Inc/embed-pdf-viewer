# Viewer perf comparison: 2026-09-29T19-45-01-029Z-2024mbp16black-desktop

3 rounds, profile `desktop` (CPU throttle 1x), **quick, not for publication**.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 90e783cab06cfdfede3e96e229ca64006e5ec194 (uncommitted changes) |
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
| plugins | research/multi-platform-rust-core-impl-plugins | bef223d9d | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 992f38174937 |
| plugins-lto | research/multi-platform-rust-core-impl-plugins | bef223d9d | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | ce0b35ed118c |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins`: pass
- `plugins-lto`: pass

## Sizes

| Metric | main | main-aa | plugins | plugins-lto |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 10.78 MiB | 11.68 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.77 MiB | 1.88 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.62 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

36 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 43.6 [43.8] | 42.9 [44.3] | 0.983 [0.962, 1.045] | pass |
| open(ebook) / openedMs | 95.6 [95.7] | 96.1 [97.8] | 1.005 [0.982, 1.034] | pass |
| open(ebook) / firstPaintMs | 177.0 [177.5] | 174.9 [179.4] | 0.988 [0.981, 1.023] | pass |
| open(ebook) / settledMs | 268.0 [271.5] | 264.9 [269.6] | 0.989 [0.970, 1.023] | pass |
| open(ebook) / mainTaskMs | 92.6 [94.8] | 93.1 [93.8] | 1.006 [0.964, 1.025] | pass |
| open(ebook) / mainScriptMs | 15.4 [16.3] | 15.8 [15.8] | 1.027 [0.940, 1.032] | pass |
| open(large) / readyMs | 43.1 [43.1] | 43.5 [44.2] | 1.010 [0.993, 1.035] | pass |
| open(large) / openedMs | 109.3 [109.4] | 109.8 [112.3] | 1.004 [0.996, 1.037] | pass |
| open(large) / firstPaintMs | 177.2 [185.6] | 179.2 [180.0] | 1.011 [0.947, 1.024] | pass |
| open(large) / settledMs | 172.9 [179.9] | 174.7 [175.7] | 1.010 [0.954, 1.024] | pass |
| open(large) / mainTaskMs | 207.0 [207.7] | 205.9 [206.4] | 0.995 [0.990, 1.000] | pass |
| open(large) / mainScriptMs | 18.3 [18.5] | 18.2 [18.2] | 0.994 [0.968, 1.003] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2006.8 [2010.4] | 2010.4 [2017.8] | 1.002 [0.996, 1.009] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.001] | pass |
| scroll / frameP95Ms | 9.0 [9.5] | 8.8 [9.1] | 0.983 [0.896, 1.035] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 338.1 [353.7] | 358.7 [360.4] | 1.061 [1.003, 1.082] | A/A failed |
| scroll / mainScriptMs | 45.9 [46.0] | 45.9 [46.3] | 1.001 [0.998, 1.014] | pass |
| jump / settleMs | 77.5 [82.3] | 80.9 [83.2] | +3.4 [-16.8, +9.2] | pass |
| jump / mainTaskMs | 49.8 [50.6] | 47.4 [48.0] | 0.952 [0.844, 1.123] | pass |
| jump / mainScriptMs | 12.4 [13.2] | 11.9 [11.9] | 0.960 [0.800, 1.013] | pass |
| zoom / settleMs | 305.5 [313.4] | 309.4 [316.6] | +3.9 [-23.8, +18.4] | pass |
| zoom / zoom2SettleMs | 107.1 [109.8] | 103.9 [111.7] | -3.2 [-9.5, +8.8] | pass |
| zoom / zoom4SettleMs | 198.4 [203.7] | 196.9 [211.8] | -1.5 [-14.3, +18.2] | pass |
| zoom / backTo1SettleMs | 59.9 [60.2] | 68.1 [72.8] | +8.2 [-9.3, +14.7] | pass |
| zoom / mainTaskMs | 142.2 [143.7] | 137.3 [143.0] | 0.965 [0.932, 1.027] | pass |
| zoom / mainScriptMs | 20.3 [22.3] | 19.5 [20.3] | 0.964 [0.863, 1.031] | pass |
| select / settleMs | 148.6 [148.7] | 146.8 [148.3] | -1.8 [-10.2, +3.1] | pass |
| select / mainTaskMs | 44.6 [48.1] | 42.2 [43.2] | 0.946 [0.800, 1.000] | A/A failed |
| select / mainScriptMs | 10.4 [11.4] | 9.8 [10.0] | 0.941 [0.800, 0.980] | A/A failed |
| search / searchMs | 49.2 [51.3] | 50.2 [51.3] | 1.019 [0.956, 1.054] | pass |
| search / mainTaskMs | 3.9 [4.0] | 3.9 [3.9] | 0.997 [0.953, 1.028] | pass |
| search / mainScriptMs | 0.1 [0.1] | 0.1 [0.1] | 0.979 [0.890, 1.044] | pass |
| annotate / settleMs | 153.1 [157.4] | 153.7 [157.8] | +0.5 [-7.0, +6.8] | pass |
| annotate / mainTaskMs | 49.0 [49.8] | 51.2 [51.4] | 1.046 [0.970, 1.085] | pass |
| annotate / mainScriptMs | 9.6 [10.0] | 10.5 [10.6] | 1.089 [0.950, 1.142] | pass |

## `main` vs `impl-ts`

Not run: `impl-ts`.

## `impl-ts` vs `impl-rust`

Not run: `impl-ts`, `impl-rust`.

## `impl-ts` vs `plugins`

Not run: `impl-ts`.

## `main` vs `plugins`

| Row | main median [p95] | plugins median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 43.6 [43.8] | 68.4 [68.8] | 1.568 [1.541, 1.619] | slower |
| open(ebook) / openedMs | 95.6 [95.7] | 168.8 [171.0] | 1.765 [1.746, 1.807] | slower |
| open(ebook) / firstPaintMs | 177.0 [177.5] | 327.0 [327.8] | 1.847 [1.820, 1.864] | slower |
| open(ebook) / settledMs | 268.0 [271.5] | 508.7 [517.6] | 1.898 [1.829, 1.964] | slower |
| open(ebook) / mainTaskMs | 92.6 [94.8] | 387.8 [410.8] | 4.188 [3.968, 4.510] | slower |
| open(ebook) / mainScriptMs | 15.4 [16.3] | 205.6 [221.5] | 13.363 [12.057, 14.577] | slower |
| open(large) / readyMs | 43.1 [43.1] | 69.0 [69.5] | 1.602 [1.567, 1.625] | slower |
| open(large) / openedMs | 109.3 [109.4] | 1383.8 [1430.4] | 12.655 [12.483, 13.228] | slower |
| open(large) / firstPaintMs | 177.2 [185.6] | 2318.3 [2335.1] | 13.086 [11.860, 13.290] | slower |
| open(large) / settledMs | 172.9 [179.9] | 2361.3 [2376.2] | 13.659 [12.877, 13.859] | slower |
| open(large) / mainTaskMs | 207.0 [207.7] | 2648.2 [2692.7] | 12.796 [12.599, 13.071] | slower |
| open(large) / mainScriptMs | 18.3 [18.5] | 2172.9 [2212.8] | 118.981 [115.917, 121.899] | slower |
| scroll / settleMs | 0.0 [0.0] | 48.3 [171.1] | +48.3 [+44.5, +184.8] | slower |
| scroll / wallMs | 2006.8 [2010.4] | 16322.4 [16349.5] | 8.134 [7.965, 8.176] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 41.4 [45.2] | 4.970 [4.199, 5.480] | slower |
| scroll / frameP95Ms | 9.0 [9.5] | 208.8 [230.9] | 23.228 [21.655, 26.308] | slower |
| scroll / jankFrames | 0 [0] | 95 [101.30] | +95 [+94, +102] | slower |
| scroll / longTasks | 0 [0] | 63 [67.50] | +63 [+63, +68] | slower |
| scroll / longTaskMs | 0.0 [0.0] | 9566.0 [10186.1] | +9566.0 [+9461.0, +10255.0] | slower |
| scroll / mainTaskMs | 338.1 [353.7] | 16733.2 [16757.1] | 49.490 [46.204, 50.290] | unreliable (A/A failed) |
| scroll / mainScriptMs | 45.9 [46.0] | 9222.2 [9312.8] | 200.894 [195.592, 204.238] | slower |
| jump / settleMs | 77.5 [82.3] | 904.7 [1008.6] | +827.2 [+780.1, +945.8] | slower |
| jump / mainTaskMs | 49.8 [50.6] | 1251.8 [1350.6] | 25.157 [23.798, 31.832] | slower |
| jump / mainScriptMs | 12.4 [13.2] | 796.5 [888.6] | 64.340 [57.635, 76.226] | slower |
| zoom / settleMs | 305.5 [313.4] | 556.4 [564.0] | +250.9 [+236.4, +265.9] | slower |
| zoom / zoom2SettleMs | 107.1 [109.8] | 250.2 [257.7] | +143.1 [+134.4, +154.7] | slower |
| zoom / zoom4SettleMs | 198.4 [203.7] | 306.2 [306.4] | +107.9 [+101.9, +111.1] | slower |
| zoom / backTo1SettleMs | 59.9 [60.2] | 107.0 [109.3] | +47.1 [+46.6, +51.1] | slower |
| zoom / mainTaskMs | 142.2 [143.7] | 924.9 [931.9] | 6.505 [6.280, 6.667] | slower |
| zoom / mainScriptMs | 20.3 [22.3] | 508.8 [512.1] | 25.115 [22.047, 25.923] | slower |
| select / settleMs | 148.6 [148.7] | 292.3 [300.8] | +143.7 [+138.0, +156.3] | slower |
| select / mainTaskMs | 44.6 [48.1] | 233.8 [240.0] | 5.242 [4.699, 5.560] | unreliable (A/A failed) |
| select / mainScriptMs | 10.4 [11.4] | 183.7 [188.9] | 17.644 [15.647, 18.622] | unreliable (A/A failed) |
| search / searchMs | 49.2 [51.3] | 76.5 [79.6] | 1.554 [1.442, 1.639] | slower |
| search / mainTaskMs | 3.9 [4.0] | 37.1 [38.0] | 9.441 [8.931, 9.925] | slower |
| search / mainScriptMs | 0.1 [0.1] | 18.8 [19.2] | 197.705 [183.500, 213.378] | slower |
| annotate / settleMs | 153.1 [157.4] | 330.8 [331.0] | +177.7 [+171.7, +179.6] | slower |
| annotate / mainTaskMs | 49.0 [49.8] | 282.3 [285.3] | 5.767 [5.649, 6.028] | slower |
| annotate / mainScriptMs | 9.6 [10.0] | 210.1 [212.9] | 21.835 [20.907, 22.886] | slower |

## `plugins` vs `plugins-lto`

| Row | plugins median [p95] | plugins-lto median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 68.4 [68.8] | 70.2 [70.4] | 1.027 [0.994, 1.044] | no detectable difference |
| open(ebook) / openedMs | 168.8 [171.0] | 169.7 [170.3] | 1.005 [0.976, 1.020] | no detectable difference |
| open(ebook) / firstPaintMs | 327.0 [327.8] | 321.8 [329.6] | 0.984 [0.956, 1.023] | no detectable difference |
| open(ebook) / settledMs | 508.7 [517.6] | 486.1 [492.3] | 0.956 [0.926, 0.992] | faster |
| open(ebook) / mainTaskMs | 387.8 [410.8] | 369.6 [379.3] | 0.953 [0.874, 1.009] | no detectable difference |
| open(ebook) / mainScriptMs | 205.6 [221.5] | 191.5 [197.7] | 0.932 [0.835, 1.000] | no detectable difference |
| open(large) / readyMs | 69.0 [69.5] | 68.7 [70.0] | 0.995 [0.985, 1.039] | no detectable difference |
| open(large) / openedMs | 1383.8 [1430.4] | 1272.5 [1283.8] | 0.920 [0.874, 0.941] | faster |
| open(large) / firstPaintMs | 2318.3 [2335.1] | 2085.1 [2116.2] | 0.899 [0.859, 0.958] | faster |
| open(large) / settledMs | 2361.3 [2376.2] | 2124.3 [2155.3] | 0.900 [0.885, 0.928] | faster |
| open(large) / mainTaskMs | 2648.2 [2692.7] | 2394.4 [2437.9] | 0.904 [0.881, 0.933] | faster |
| open(large) / mainScriptMs | 2172.9 [2212.8] | 1965.8 [2000.2] | 0.905 [0.880, 0.934] | faster |
| scroll / settleMs | 48.3 [171.1] | 125.9 [197.0] | +77.6 [-143.3, +160.4] | no detectable difference |
| scroll / wallMs | 16322.4 [16349.5] | 14334.9 [14410.7] | 0.878 [0.870, 0.900] | faster |
| scroll / frameP50Ms | 41.4 [45.2] | 25.2 [32.9] | 0.607 [0.548, 0.966] | faster |
| scroll / frameP95Ms | 208.8 [230.9] | 182.9 [183.7] | 0.876 [0.764, 0.885] | faster |
| scroll / jankFrames | 95 [101.30] | 99 [100.80] | +4 [-6, +7] | no detectable difference |
| scroll / longTasks | 63 [67.50] | 62 [65.60] | -1 [-7, +3] | no detectable difference |
| scroll / longTaskMs | 9566.0 [10186.1] | 8281.0 [8708.5] | -1285.0 [-2042.0, -705.0] | faster |
| scroll / mainTaskMs | 16733.2 [16757.1] | 14771.4 [14804.7] | 0.883 [0.874, 0.902] | unreliable (A/A failed) |
| scroll / mainScriptMs | 9222.2 [9312.8] | 8077.5 [8100.3] | 0.876 [0.859, 0.900] | faster |
| jump / settleMs | 904.7 [1008.6] | 1009.4 [1034.4] | +104.8 [-82.2, +174.3] | no detectable difference |
| jump / mainTaskMs | 1251.8 [1350.6] | 1341.4 [1351.7] | 1.072 [0.964, 1.120] | no detectable difference |
| jump / mainScriptMs | 796.5 [888.6] | 912.5 [918.6] | 1.146 [0.985, 1.198] | no detectable difference |
| zoom / settleMs | 556.4 [564.0] | 508.6 [512.4] | -47.8 [-57.9, -37.9] | faster |
| zoom / zoom2SettleMs | 250.2 [257.7] | 236.4 [240.4] | -13.8 [-23.8, -3.6] | faster |
| zoom / zoom4SettleMs | 306.2 [306.4] | 272.2 [272.3] | -34.1 [-34.4, -33.9] | faster |
| zoom / backTo1SettleMs | 107.0 [109.3] | 98.4 [99.0] | -8.6 [-11.8, -7.7] | no detectable difference |
| zoom / mainTaskMs | 924.9 [931.9] | 813.6 [831.8] | 0.880 [0.853, 0.923] | faster |
| zoom / mainScriptMs | 508.8 [512.1] | 438.7 [451.0] | 0.862 [0.825, 0.912] | faster |
| select / settleMs | 292.3 [300.8] | 286.7 [291.6] | -5.6 [-17.2, +5.5] | no detectable difference |
| select / mainTaskMs | 233.8 [240.0] | 221.4 [222.1] | 0.947 [0.918, 0.975] | unreliable (A/A failed) |
| select / mainScriptMs | 183.7 [188.9] | 173.1 [173.4] | 0.943 [0.909, 0.965] | unreliable (A/A failed) |
| search / searchMs | 76.5 [79.6] | 75.8 [77.5] | 0.990 [0.919, 1.045] | no detectable difference |
| search / mainTaskMs | 37.1 [38.0] | 34.5 [36.6] | 0.931 [0.896, 1.034] | no detectable difference |
| search / mainScriptMs | 18.8 [19.2] | 17.1 [17.3] | 0.909 [0.870, 0.945] | no detectable difference |
| annotate / settleMs | 330.8 [331.0] | 323.9 [329.1] | -6.9 [-10.1, +0.1] | no detectable difference |
| annotate / mainTaskMs | 282.3 [285.3] | 258.5 [262.0] | 0.916 [0.885, 0.931] | no detectable difference |
| annotate / mainScriptMs | 210.1 [212.9] | 189.7 [192.6] | 0.903 [0.873, 0.921] | no detectable difference |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins median [p95] | plugins-lto median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.34 MiB [34.34 MiB] | 43.33 MiB [43.34 MiB] | 43.27 MiB [43.28 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.03 MiB [23.03 MiB] | 23.03 MiB [23.03 MiB] |
| open(large) / heapAfterOpenBytes | 48.16 MiB [48.16 MiB] | 48.16 MiB [48.16 MiB] | 58.10 MiB [58.10 MiB] | 58.00 MiB [58.01 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.94 MiB [22.94 MiB] | 22.94 MiB [22.94 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.42 MiB] | 37.42 MiB [37.42 MiB] | 49.04 MiB [49.05 MiB] | 48.97 MiB [48.98 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.28 MiB] | 38.28 MiB [38.28 MiB] | 63.09 MiB [63.11 MiB] | 63.04 MiB [63.05 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.56 MiB [0.56 MiB] | 0.56 MiB [0.56 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins`:

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
| wasmBytes | 9.00 MiB | 9.00 MiB |
| wireRefs | 645 | 645 |

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

