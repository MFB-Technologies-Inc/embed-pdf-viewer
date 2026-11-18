# Viewer perf comparison: 2026-09-29T16-24-58-866Z-2024mbp16black-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 0c56a0e2458dabad25a7bbca679287188142d551 (uncommitted changes) |
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
| impl-ts | research/multi-platform-rust-core-impl | c8085bd3c | built (fork d045f389d, wasm32-eh) | 0f7407599d93 |  |
| impl-rust | research/multi-platform-rust-core-impl | c8085bd3c | built (fork d045f389d, wasm32-eh) | 0f7407599d93 |  |
| plugins | research/multi-platform-rust-core-impl-plugins | bef223d9d | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 992f38174937 |

## Gate

- `main`: pass
- `main-aa`: pass
- `impl-ts`: pass
- `impl-rust`: pass
- `plugins`: pass

## Sizes

| Metric | main | main-aa | impl-ts | impl-rust | plugins |
| --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 0.00 MiB | 10.78 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 0.00 MiB | 1.77 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 3.35 MiB | 3.35 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.96 MiB | 0.96 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

38 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 43.8 [141.5] | 44.1 [138.5] | 1.005 [0.957, 1.041] | pass |
| open(ebook) / openedMs | 95.8 [201.9] | 96.4 [200.2] | 1.006 [0.959, 1.040] | pass |
| open(ebook) / firstPaintMs | 177.8 [286.8] | 177.5 [288.1] | 0.998 [0.953, 1.025] | pass |
| open(ebook) / settledMs | 265.5 [390.5] | 266.6 [389.3] | 1.004 [0.951, 1.040] | pass |
| open(ebook) / mainTaskMs | 93.6 [103.3] | 93.8 [102.7] | 1.002 [0.961, 1.040] | pass |
| open(ebook) / mainScriptMs | 15.9 [18.1] | 15.9 [17.8] | 1.003 [0.940, 1.047] | pass |
| open(large) / readyMs | 44.7 [48.3] | 43.9 [46.4] | 0.982 [0.945, 1.032] | pass |
| open(large) / openedMs | 111.2 [124.4] | 111.0 [124.1] | 0.998 [0.947, 1.054] | pass |
| open(large) / firstPaintMs | 181.8 [197.5] | 180.4 [197.6] | 0.992 [0.951, 1.042] | pass |
| open(large) / settledMs | 177.4 [197.7] | 178.6 [193.5] | 1.007 [0.951, 1.047] | pass |
| open(large) / mainTaskMs | 206.3 [231.5] | 206.2 [231.3] | 0.999 [0.943, 1.052] | pass |
| open(large) / mainScriptMs | 18.2 [20.1] | 18.1 [20.7] | 0.996 [0.958, 1.062] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2003.0 [2022.5] | 2007.4 [2016.1] | 1.002 [0.993, 1.004] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.7 [9.3] | 8.5 [9.0] | 0.974 [0.927, 1.037] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 349.4 [368.7] | 352.9 [365.9] | 1.010 [0.943, 1.052] | pass |
| scroll / mainScriptMs | 46.4 [48.0] | 46.7 [47.3] | 1.005 [0.941, 1.026] | pass |
| jump / settleMs | 79.9 [82.7] | 77.1 [83.4] | -2.9 [-10.9, +5.1] | pass |
| jump / mainTaskMs | 45.7 [50.0] | 48.6 [52.6] | 1.063 [0.943, 1.184] | pass |
| jump / mainScriptMs | 12.2 [14.7] | 12.1 [14.9] | 0.996 [0.850, 1.075] | pass |
| zoom / settleMs | 309.1 [323.6] | 314.9 [321.8] | +5.8 [-15.8, +26.2] | pass |
| zoom / zoom2SettleMs | 110.3 [115.5] | 110.0 [111.8] | -0.3 [-6.8, +11.5] | pass |
| zoom / zoom4SettleMs | 199.7 [211.5] | 206.1 [212.7] | +6.4 [-7.4, +17.7] | pass |
| zoom / backTo1SettleMs | 61.2 [70.8] | 60.4 [61.2] | -0.8 [-11.0, +9.9] | pass |
| zoom / mainTaskMs | 140.8 [149.0] | 140.7 [146.8] | 1.000 [0.947, 1.096] | pass |
| zoom / mainScriptMs | 22.0 [23.5] | 21.4 [24.9] | 0.974 [0.888, 1.107] | pass |
| select / settleMs | 145.3 [148.6] | 142.5 [147.7] | -2.8 [-6.8, +3.3] | pass |
| select / mainTaskMs | 43.5 [47.1] | 41.1 [47.6] | 0.944 [0.859, 1.145] | pass |
| select / mainScriptMs | 10.1 [11.1] | 9.6 [11.2] | 0.956 [0.838, 1.174] | pass |
| search / searchMs | 49.7 [52.3] | 46.4 [52.6] | 0.935 [0.799, 1.103] | pass |
| search / mainTaskMs | 4.0 [4.4] | 3.8 [4.6] | 0.955 [0.816, 1.084] | pass |
| search / mainScriptMs | 0.1 [0.2] | 0.1 [0.1] | 0.894 [0.749, 0.989] | A/A failed |
| annotate / settleMs | 153.0 [155.3] | 152.8 [154.8] | -0.1 [-3.0, +1.0] | pass |
| annotate / mainTaskMs | 51.3 [54.0] | 50.3 [52.8] | 0.979 [0.912, 1.066] | pass |
| annotate / mainScriptMs | 10.6 [11.0] | 10.4 [10.9] | 0.984 [0.891, 1.091] | pass |

## `main` vs `impl-ts`

| Row | main median [p95] | impl-ts median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 43.8 [141.5] | 44.6 [142.5] | 1.018 [0.957, 1.053] | no detectable difference |
| open(ebook) / openedMs | 95.8 [201.9] | 100.1 [209.3] | 1.044 [0.991, 1.084] | no detectable difference |
| open(ebook) / firstPaintMs | 177.8 [286.8] | 181.5 [299.7] | 1.021 [0.982, 1.060] | no detectable difference |
| open(ebook) / settledMs | 265.5 [390.5] | 275.2 [402.1] | 1.036 [0.985, 1.072] | no detectable difference |
| open(ebook) / mainTaskMs | 93.6 [103.3] | 93.9 [103.0] | 1.002 [0.960, 1.044] | no detectable difference |
| open(ebook) / mainScriptMs | 15.9 [18.1] | 16.0 [17.8] | 1.005 [0.940, 1.060] | no detectable difference |
| open(large) / readyMs | 44.7 [48.3] | 44.4 [46.8] | 0.993 [0.952, 1.044] | no detectable difference |
| open(large) / openedMs | 111.2 [124.4] | 113.4 [128.9] | 1.020 [0.968, 1.083] | no detectable difference |
| open(large) / firstPaintMs | 181.8 [197.5] | 183.1 [201.2] | 1.007 [0.961, 1.058] | no detectable difference |
| open(large) / settledMs | 177.4 [197.7] | 181.1 [197.1] | 1.021 [0.966, 1.069] | no detectable difference |
| open(large) / mainTaskMs | 206.3 [231.5] | 205.3 [230.9] | 0.995 [0.940, 1.059] | no detectable difference |
| open(large) / mainScriptMs | 18.2 [20.1] | 18.0 [20.8] | 0.990 [0.944, 1.058] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2003.0 [2022.5] | 2002.6 [2023.2] | 1.000 [0.992, 1.004] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.7 [9.3] | 8.5 [8.9] | 0.973 [0.926, 1.021] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 349.4 [368.7] | 344.2 [365.2] | 0.985 [0.935, 1.045] | no detectable difference |
| scroll / mainScriptMs | 46.4 [48.0] | 46.2 [47.3] | 0.996 [0.945, 1.020] | no detectable difference |
| jump / settleMs | 79.9 [82.7] | 76.2 [85.4] | -3.7 [-7.1, +7.0] | no detectable difference |
| jump / mainTaskMs | 45.7 [50.0] | 45.9 [49.4] | 1.004 [0.938, 1.135] | no detectable difference |
| jump / mainScriptMs | 12.2 [14.7] | 11.8 [13.9] | 0.971 [0.846, 1.025] | no detectable difference |
| zoom / settleMs | 309.1 [323.6] | 311.1 [321.3] | +1.9 [-13.4, +22.6] | no detectable difference |
| zoom / zoom2SettleMs | 110.3 [115.5] | 112.1 [120.3] | +1.8 [-1.2, +14.4] | no detectable difference |
| zoom / zoom4SettleMs | 199.7 [211.5] | 195.3 [206.5] | -4.4 [-15.9, +9.5] | no detectable difference |
| zoom / backTo1SettleMs | 61.2 [70.8] | 59.0 [71.9] | -2.2 [-8.1, +10.0] | no detectable difference |
| zoom / mainTaskMs | 140.8 [149.0] | 141.6 [144.2] | 1.006 [0.965, 1.109] | no detectable difference |
| zoom / mainScriptMs | 22.0 [23.5] | 20.2 [22.3] | 0.922 [0.875, 1.022] | no detectable difference |
| select / settleMs | 145.3 [148.6] | 141.7 [146.5] | -3.7 [-6.6, +1.0] | no detectable difference |
| select / mainTaskMs | 43.5 [47.1] | 40.8 [44.5] | 0.937 [0.834, 1.126] | no detectable difference |
| select / mainScriptMs | 10.1 [11.1] | 9.6 [10.4] | 0.950 [0.819, 1.152] | no detectable difference |
| search / searchMs | 49.7 [52.3] | 41.9 [51.6] | 0.844 [0.790, 1.041] | no detectable difference |
| search / mainTaskMs | 4.0 [4.4] | 3.6 [4.3] | 0.902 [0.847, 0.995] | no detectable difference |
| search / mainScriptMs | 0.1 [0.2] | 0.1 [0.1] | 0.879 [0.764, 1.042] | unreliable (A/A failed) |
| annotate / settleMs | 153.0 [155.3] | 153.4 [156.3] | +0.5 [-2.0, +2.6] | no detectable difference |
| annotate / mainTaskMs | 51.3 [54.0] | 50.8 [53.0] | 0.989 [0.900, 1.082] | no detectable difference |
| annotate / mainScriptMs | 10.6 [11.0] | 10.5 [11.3] | 0.996 [0.850, 1.103] | no detectable difference |

## `impl-ts` vs `impl-rust`

| Row | impl-ts median [p95] | impl-rust median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 44.6 [142.5] | 43.3 [213.6] | 0.972 [0.939, 2.882] | no detectable difference |
| open(ebook) / openedMs | 100.1 [209.3] | 98.8 [285.6] | 0.987 [0.946, 1.917] | no detectable difference |
| open(ebook) / firstPaintMs | 181.5 [299.7] | 182.9 [379.1] | 1.008 [0.962, 1.550] | no detectable difference |
| open(ebook) / settledMs | 275.2 [402.1] | 272.8 [479.2] | 0.991 [0.957, 1.366] | no detectable difference |
| open(ebook) / mainTaskMs | 93.9 [103.0] | 94.2 [103.9] | 1.003 [0.970, 1.054] | no detectable difference |
| open(ebook) / mainScriptMs | 16.0 [17.8] | 15.9 [18.0] | 0.997 [0.963, 1.075] | no detectable difference |
| open(large) / readyMs | 44.4 [46.8] | 44.9 [49.3] | 1.011 [0.969, 1.048] | no detectable difference |
| open(large) / openedMs | 113.4 [128.9] | 114.2 [128.0] | 1.007 [0.946, 1.063] | no detectable difference |
| open(large) / firstPaintMs | 183.1 [201.2] | 185.6 [200.9] | 1.013 [0.966, 1.050] | no detectable difference |
| open(large) / settledMs | 181.1 [197.1] | 181.2 [197.8] | 1.001 [0.960, 1.048] | no detectable difference |
| open(large) / mainTaskMs | 205.3 [230.9] | 205.7 [230.6] | 1.002 [0.944, 1.063] | no detectable difference |
| open(large) / mainScriptMs | 18.0 [20.8] | 18.0 [20.4] | 1.002 [0.947, 1.062] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2002.6 [2023.2] | 2010.1 [2022.5] | 1.004 [0.998, 1.008] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.5 [8.9] | 8.6 [9.2] | 1.013 [0.974, 1.064] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 344.2 [365.2] | 346.4 [365.7] | 1.006 [0.946, 1.046] | no detectable difference |
| scroll / mainScriptMs | 46.2 [47.3] | 46.2 [48.1] | 1.000 [0.957, 1.053] | no detectable difference |
| jump / settleMs | 76.2 [85.4] | 79.6 [82.2] | +3.3 [-7.9, +6.6] | no detectable difference |
| jump / mainTaskMs | 45.9 [49.4] | 45.7 [49.2] | 0.996 [0.910, 1.059] | no detectable difference |
| jump / mainScriptMs | 11.8 [13.9] | 11.6 [12.7] | 0.980 [0.954, 1.035] | no detectable difference |
| zoom / settleMs | 311.1 [321.3] | 304.1 [315.9] | -7.0 [-14.0, +6.2] | no detectable difference |
| zoom / zoom2SettleMs | 112.1 [120.3] | 112.7 [118.7] | +0.6 [-5.6, +5.6] | no detectable difference |
| zoom / zoom4SettleMs | 195.3 [206.5] | 191.8 [200.6] | -3.5 [-16.1, +6.5] | no detectable difference |
| zoom / backTo1SettleMs | 59.0 [71.9] | 56.8 [62.9] | -2.2 [-10.1, +5.4] | no detectable difference |
| zoom / mainTaskMs | 141.6 [144.2] | 142.1 [146.1] | 1.003 [0.937, 1.033] | no detectable difference |
| zoom / mainScriptMs | 20.2 [22.3] | 20.5 [22.8] | 1.011 [0.968, 1.106] | no detectable difference |
| select / settleMs | 141.7 [146.5] | 142.9 [146.4] | +1.3 [-3.2, +2.9] | no detectable difference |
| select / mainTaskMs | 40.8 [44.5] | 39.9 [43.1] | 0.978 [0.927, 1.105] | no detectable difference |
| select / mainScriptMs | 9.6 [10.4] | 9.2 [10.0] | 0.961 [0.904, 1.106] | no detectable difference |
| search / searchMs | 41.9 [51.6] | 46.7 [54.7] | 1.114 [0.917, 1.269] | no detectable difference |
| search / mainTaskMs | 3.6 [4.3] | 3.9 [4.5] | 1.069 [0.968, 1.174] | no detectable difference |
| search / mainScriptMs | 0.1 [0.1] | 0.1 [0.1] | 1.044 [0.921, 1.171] | unreliable (A/A failed) |
| annotate / settleMs | 153.4 [156.3] | 154.4 [157.6] | +1.0 [-2.0, +3.3] | no detectable difference |
| annotate / mainTaskMs | 50.8 [53.0] | 50.1 [53.3] | 0.988 [0.944, 1.092] | no detectable difference |
| annotate / mainScriptMs | 10.5 [11.3] | 10.3 [11.0] | 0.979 [0.914, 1.159] | no detectable difference |

## `impl-ts` vs `plugins`

| Row | impl-ts median [p95] | plugins median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 44.6 [142.5] | 69.0 [146.4] | 1.547 [1.496, 2.366] | slower |
| open(ebook) / openedMs | 100.1 [209.3] | 168.8 [263.9] | 1.687 [1.626, 2.122] | slower |
| open(ebook) / firstPaintMs | 181.5 [299.7] | 328.4 [444.3] | 1.809 [1.739, 2.109] | slower |
| open(ebook) / settledMs | 275.2 [402.1] | 543.0 [693.7] | 1.973 [1.882, 2.241] | slower |
| open(ebook) / mainTaskMs | 93.9 [103.0] | 436.7 [514.7] | 4.652 [4.454, 5.043] | slower |
| open(ebook) / mainScriptMs | 16.0 [17.8] | 240.2 [287.3] | 15.044 [14.154, 16.391] | slower |
| open(large) / readyMs | 44.4 [46.8] | 68.4 [76.2] | 1.541 [1.494, 1.614] | slower |
| open(large) / openedMs | 113.4 [128.9] | 1364.0 [1610.4] | 12.029 [11.326, 13.028] | slower |
| open(large) / firstPaintMs | 183.1 [201.2] | 2240.8 [2626.1] | 12.237 [11.671, 13.092] | slower |
| open(large) / settledMs | 181.1 [197.1] | 2323.7 [2761.0] | 12.832 [12.286, 13.855] | slower |
| open(large) / mainTaskMs | 205.3 [230.9] | 2643.6 [3035.1] | 12.879 [12.127, 13.727] | slower |
| open(large) / mainScriptMs | 18.0 [20.8] | 2171.3 [2491.6] | 120.743 [112.803, 128.356] | slower |
| scroll / settleMs | 0.0 [0.0] | 141.4 [378.0] | +141.4 [+48.9, +265.7] | slower |
| scroll / wallMs | 2002.6 [2023.2] | 16466.5 [18985.0] | 8.223 [8.063, 8.924] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 45.5 [50.0] | 5.454 [3.999, 5.999] | slower |
| scroll / frameP95Ms | 8.5 [8.9] | 209.3 [263.4] | 24.711 [23.895, 28.129] | slower |
| scroll / jankFrames | 0 [0] | 102.50 [115] | +102.50 [+97, +109.50] | slower |
| scroll / longTasks | 0 [0] | 64 [123.50] | +64 [+62, +86.50] | slower |
| scroll / longTaskMs | 0.0 [0.0] | 9749.0 [14169.3] | +9749.0 [+9462.0, +11665.5] | slower |
| scroll / mainTaskMs | 344.2 [365.2] | 16875.1 [19466.6] | 49.026 [46.074, 52.214] | slower |
| scroll / mainScriptMs | 46.2 [47.3] | 9370.1 [10892.0] | 202.745 [195.549, 220.992] | slower |
| jump / settleMs | 76.2 [85.4] | 950.5 [1117.1] | +874.3 [+802.8, +908.5] | slower |
| jump / mainTaskMs | 45.9 [49.4] | 1316.3 [1523.7] | 28.689 [25.907, 30.490] | slower |
| jump / mainScriptMs | 11.8 [13.9] | 859.3 [1019.7] | 72.750 [66.467, 75.109] | slower |
| zoom / settleMs | 311.1 [321.3] | 543.9 [577.1] | +232.8 [+222.6, +251.3] | slower |
| zoom / zoom2SettleMs | 112.1 [120.3] | 250.9 [263.0] | +138.8 [+126.0, +148.9] | slower |
| zoom / zoom4SettleMs | 195.3 [206.5] | 293.9 [316.9] | +98.5 [+85.1, +115.5] | slower |
| zoom / backTo1SettleMs | 59.0 [71.9] | 108.8 [126.5] | +49.8 [+43.1, +58.6] | slower |
| zoom / mainTaskMs | 141.6 [144.2] | 934.5 [1071.0] | 6.598 [6.324, 6.971] | slower |
| zoom / mainScriptMs | 20.2 [22.3] | 518.9 [620.8] | 25.625 [24.151, 27.753] | slower |
| select / settleMs | 141.7 [146.5] | 291.4 [314.8] | +149.8 [+144.5, +156.9] | slower |
| select / mainTaskMs | 40.8 [44.5] | 236.5 [244.9] | 5.801 [5.507, 6.515] | slower |
| select / mainScriptMs | 9.6 [10.4] | 186.1 [196.0] | 19.453 [18.345, 22.343] | slower |
| search / searchMs | 41.9 [51.6] | 71.9 [79.1] | 1.715 [1.411, 1.879] | slower |
| search / mainTaskMs | 3.6 [4.3] | 35.3 [37.1] | 9.760 [8.805, 10.321] | slower |
| search / mainScriptMs | 0.1 [0.1] | 19.2 [19.7] | 210.637 [188.470, 234.085] | unreliable (A/A failed) |
| annotate / settleMs | 153.4 [156.3] | 331.1 [423.1] | +177.7 [+175.3, +221.6] | slower |
| annotate / mainTaskMs | 50.8 [53.0] | 282.7 [328.5] | 5.570 [5.388, 6.212] | slower |
| annotate / mainScriptMs | 10.5 [11.3] | 210.2 [247.4] | 19.995 [19.122, 23.470] | slower |

## `main` vs `plugins`

| Row | main median [p95] | plugins median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 43.8 [141.5] | 69.0 [146.4] | 1.574 [1.488, 2.405] | slower |
| open(ebook) / openedMs | 95.8 [201.9] | 168.8 [263.9] | 1.762 [1.681, 2.213] | slower |
| open(ebook) / firstPaintMs | 177.8 [286.8] | 328.4 [444.3] | 1.847 [1.777, 2.147] | slower |
| open(ebook) / settledMs | 265.5 [390.5] | 543.0 [693.7] | 2.045 [1.935, 2.312] | slower |
| open(ebook) / mainTaskMs | 93.6 [103.3] | 436.7 [514.7] | 4.664 [4.458, 5.055] | slower |
| open(ebook) / mainScriptMs | 15.9 [18.1] | 240.2 [287.3] | 15.124 [14.153, 16.419] | slower |
| open(large) / readyMs | 44.7 [48.3] | 68.4 [76.2] | 1.530 [1.486, 1.612] | slower |
| open(large) / openedMs | 111.2 [124.4] | 1364.0 [1610.4] | 12.270 [11.652, 13.265] | slower |
| open(large) / firstPaintMs | 181.8 [197.5] | 2240.8 [2626.1] | 12.325 [11.782, 13.217] | slower |
| open(large) / settledMs | 177.4 [197.7] | 2323.7 [2761.0] | 13.096 [12.506, 14.173] | slower |
| open(large) / mainTaskMs | 206.3 [231.5] | 2643.6 [3035.1] | 12.817 [12.094, 13.664] | slower |
| open(large) / mainScriptMs | 18.2 [20.1] | 2171.3 [2491.6] | 119.579 [113.703, 127.247] | slower |
| scroll / settleMs | 0.0 [0.0] | 141.4 [378.0] | +141.4 [+48.9, +265.7] | slower |
| scroll / wallMs | 2003.0 [2022.5] | 16466.5 [18985.0] | 8.221 [8.035, 8.901] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 45.5 [50.0] | 5.454 [3.999, 5.999] | slower |
| scroll / frameP95Ms | 8.7 [9.3] | 209.3 [263.4] | 24.050 [22.828, 27.513] | slower |
| scroll / jankFrames | 0 [0] | 102.50 [115] | +102.50 [+97, +109.50] | slower |
| scroll / longTasks | 0 [0] | 64 [123.50] | +64 [+62, +86.50] | slower |
| scroll / longTaskMs | 0.0 [0.0] | 9749.0 [14169.3] | +9749.0 [+9462.0, +11665.5] | slower |
| scroll / mainTaskMs | 349.4 [368.7] | 16875.1 [19466.6] | 48.299 [45.839, 51.666] | slower |
| scroll / mainScriptMs | 46.4 [48.0] | 9370.1 [10892.0] | 201.863 [193.596, 219.509] | slower |
| jump / settleMs | 79.9 [82.7] | 950.5 [1117.1] | +870.6 [+802.4, +906.7] | slower |
| jump / mainTaskMs | 45.7 [50.0] | 1316.3 [1523.7] | 28.793 [26.338, 31.959] | slower |
| jump / mainScriptMs | 12.2 [14.7] | 859.3 [1019.7] | 70.651 [58.835, 74.552] | slower |
| zoom / settleMs | 309.1 [323.6] | 543.9 [577.1] | +234.8 [+221.0, +258.8] | slower |
| zoom / zoom2SettleMs | 110.3 [115.5] | 250.9 [263.0] | +140.6 [+129.0, +157.8] | slower |
| zoom / zoom4SettleMs | 199.7 [211.5] | 293.9 [316.9] | +94.2 [+84.0, +111.2] | slower |
| zoom / backTo1SettleMs | 61.2 [70.8] | 108.8 [126.5] | +47.6 [+45.0, +60.3] | slower |
| zoom / mainTaskMs | 140.8 [149.0] | 934.5 [1071.0] | 6.639 [6.345, 7.323] | slower |
| zoom / mainScriptMs | 22.0 [23.5] | 518.9 [620.8] | 23.635 [22.091, 26.378] | slower |
| select / settleMs | 145.3 [148.6] | 291.4 [314.8] | +146.1 [+139.9, +154.6] | slower |
| select / mainTaskMs | 43.5 [47.1] | 236.5 [244.9] | 5.433 [5.175, 6.481] | slower |
| select / mainScriptMs | 10.1 [11.1] | 186.1 [196.0] | 18.475 [17.060, 22.345] | slower |
| search / searchMs | 49.7 [52.3] | 71.9 [79.1] | 1.448 [1.309, 1.712] | slower |
| search / mainTaskMs | 4.0 [4.4] | 35.3 [37.1] | 8.801 [8.112, 9.443] | slower |
| search / mainScriptMs | 0.1 [0.2] | 19.2 [19.7] | 185.198 [155.359, 203.316] | unreliable (A/A failed) |
| annotate / settleMs | 153.0 [155.3] | 331.1 [423.1] | +178.1 [+176.4, +222.1] | slower |
| annotate / mainTaskMs | 51.3 [54.0] | 282.7 [328.5] | 5.507 [5.353, 6.142] | slower |
| annotate / mainScriptMs | 10.6 [11.0] | 210.2 [247.4] | 19.914 [19.440, 22.492] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | impl-ts median [p95] | impl-rust median [p95] | plugins median [p95] |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.34 MiB [34.34 MiB] | 35.03 MiB [35.03 MiB] | 34.98 MiB [34.99 MiB] | 43.36 MiB [43.92 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.00 MiB [23.00 MiB] | 22.95 MiB [22.95 MiB] | 23.03 MiB [23.03 MiB] |
| open(large) / heapAfterOpenBytes | 48.16 MiB [48.17 MiB] | 48.15 MiB [48.16 MiB] | 48.84 MiB [48.86 MiB] | 48.81 MiB [48.82 MiB] | 58.10 MiB [58.10 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.99 MiB [23.02 MiB] | 22.96 MiB [22.96 MiB] | 22.94 MiB [22.94 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.42 MiB] | 37.42 MiB [37.42 MiB] | 38.19 MiB [38.20 MiB] | 38.14 MiB [38.14 MiB] | 49.05 MiB [49.07 MiB] |
| soak / heapCycle30Bytes | 38.28 MiB [38.29 MiB] | 38.27 MiB [38.28 MiB] | 39.05 MiB [39.05 MiB] | 39.00 MiB [39.01 MiB] | 63.11 MiB [63.11 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.56 MiB [0.56 MiB] |

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

