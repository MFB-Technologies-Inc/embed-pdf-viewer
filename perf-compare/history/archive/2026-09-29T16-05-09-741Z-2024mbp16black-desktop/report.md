# Viewer perf comparison: 2026-09-29T16-05-09-741Z-2024mbp16black-desktop

3 rounds, profile `desktop` (CPU throttle 1x), **quick, not for publication**.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 8f612e74a2035c7ce14ed9f68f0740cba759ec32 (uncommitted changes) |
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

Measured from the current `.work/dist`, not recorded at run time.

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

39 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 44.1 [45.3] | 42.8 [43.3] | 0.969 [0.936, 1.006] | pass |
| open(ebook) / openedMs | 96.1 [96.2] | 95.1 [95.3] | 0.989 [0.970, 1.009] | pass |
| open(ebook) / firstPaintMs | 178.8 [180.1] | 171.1 [176.1] | 0.957 [0.942, 1.040] | pass |
| open(ebook) / settledMs | 267.4 [268.7] | 263.1 [267.2] | 0.984 [0.970, 1.014] | pass |
| open(ebook) / mainTaskMs | 94.9 [95.2] | 93.1 [93.2] | 0.981 [0.978, 0.995] | pass |
| open(ebook) / mainScriptMs | 16.0 [16.4] | 15.9 [16.0] | 0.993 [0.949, 1.011] | pass |
| open(large) / readyMs | 42.8 [43.9] | 43.2 [44.8] | 1.010 [0.966, 1.078] | pass |
| open(large) / openedMs | 108.6 [110.6] | 109.4 [111.9] | 1.008 [0.981, 1.036] | pass |
| open(large) / firstPaintMs | 184.6 [185.2] | 177.6 [180.5] | 0.962 [0.952, 1.038] | pass |
| open(large) / settledMs | 180.4 [180.9] | 173.6 [176.2] | 0.963 [0.949, 1.038] | pass |
| open(large) / mainTaskMs | 205.7 [206.9] | 205.9 [207.2] | 1.001 [0.992, 1.012] | pass |
| open(large) / mainScriptMs | 18.2 [18.2] | 18.2 [18.5] | 1.000 [0.992, 1.020] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2008.8 [2017.6] | 2002.0 [2008.4] | 0.997 [0.991, 1.003] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.8 [8.9] | 8.9 [8.9] | 1.007 [0.958, 1.013] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 347.3 [351.6] | 359.4 [369.3] | 1.035 [1.000, 1.133] | pass |
| scroll / mainScriptMs | 47.1 [48.7] | 46.6 [47.0] | 0.990 [0.947, 1.078] | pass |
| jump / settleMs | 81.1 [82.2] | 76.9 [81.4] | -4.2 [-19.8, +16.2] | pass |
| jump / mainTaskMs | 47.1 [49.4] | 43.6 [48.4] | 0.926 [0.669, 1.446] | pass |
| jump / mainScriptMs | 11.9 [12.0] | 10.8 [11.7] | 0.902 [0.824, 1.171] | pass |
| zoom / settleMs | 308.8 [310.8] | 298.8 [314.6] | -10.0 [-67.1, +35.2] | pass |
| zoom / zoom2SettleMs | 110.8 [111.9] | 108.0 [112.2] | -2.8 [-22.5, +6.4] | pass |
| zoom / zoom4SettleMs | 196.8 [203.9] | 190.9 [202.3] | -6.0 [-50.3, +33.3] | pass |
| zoom / backTo1SettleMs | 56.9 [68.9] | 60.6 [61.3] | +3.8 [-31.7, +7.2] | pass |
| zoom / mainTaskMs | 138.4 [138.9] | 142.1 [143.4] | 1.026 [0.750, 1.042] | pass |
| zoom / mainScriptMs | 20.2 [20.6] | 20.5 [22.6] | 1.017 [0.858, 1.185] | pass |
| select / settleMs | 141.1 [141.3] | 149.0 [149.0] | +7.9 [-3.1, +8.0] | pass |
| select / mainTaskMs | 35.4 [42.5] | 43.2 [46.7] | 1.221 [0.367, 2.859] | pass |
| select / mainScriptMs | 8.2 [10.0] | 9.9 [11.1] | 1.217 [0.351, 2.936] | pass |
| search / searchMs | 51.5 [51.5] | 49.1 [52.4] | 0.954 [0.677, 1.502] | pass |
| search / mainTaskMs | 4.0 [4.0] | 4.2 [4.5] | 1.063 [0.752, 1.458] | pass |
| search / mainScriptMs | 0.1 [0.1] | 0.1 [0.1] | 1.010 [0.969, 1.169] | pass |
| annotate / settleMs | 155.8 [158.9] | 155.3 [159.0] | -0.5 [-7.6, +7.6] | pass |
| annotate / mainTaskMs | 49.2 [53.2] | 49.0 [50.1] | 0.997 [0.514, 1.775] | pass |
| annotate / mainScriptMs | 9.9 [10.9] | 10.4 [10.5] | 1.050 [0.481, 1.925] | pass |

## `main` vs `impl-ts`

| Row | main median [p95] | impl-ts median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 44.1 [45.3] | 43.3 [43.7] | 0.982 [0.940, 1.015] | no detectable difference |
| open(ebook) / openedMs | 96.1 [96.2] | 98.0 [98.6] | 1.019 [1.012, 1.044] | no detectable difference |
| open(ebook) / firstPaintMs | 178.8 [180.1] | 177.8 [178.1] | 0.994 [0.984, 1.048] | no detectable difference |
| open(ebook) / settledMs | 267.4 [268.7] | 272.5 [277.0] | 1.019 [1.006, 1.051] | no detectable difference |
| open(ebook) / mainTaskMs | 94.9 [95.2] | 91.7 [93.5] | 0.966 [0.961, 1.000] | faster |
| open(ebook) / mainScriptMs | 16.0 [16.4] | 15.4 [16.2] | 0.967 [0.934, 1.027] | no detectable difference |
| open(large) / readyMs | 42.8 [43.9] | 44.7 [45.6] | 1.046 [0.975, 1.094] | no detectable difference |
| open(large) / openedMs | 108.6 [110.6] | 113.8 [116.0] | 1.048 [1.016, 1.074] | slower |
| open(large) / firstPaintMs | 184.6 [185.2] | 184.2 [190.0] | 0.998 [0.979, 1.094] | no detectable difference |
| open(large) / settledMs | 180.4 [180.9] | 180.8 [185.9] | 1.002 [0.978, 1.096] | no detectable difference |
| open(large) / mainTaskMs | 205.7 [206.9] | 205.9 [207.6] | 1.001 [0.991, 1.015] | no detectable difference |
| open(large) / mainScriptMs | 18.2 [18.2] | 18.3 [18.3] | 1.005 [0.969, 1.009] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2008.8 [2017.6] | 2001.2 [2002.0] | 0.996 [0.991, 1.000] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.8 [8.9] | 8.8 [8.8] | 0.998 [0.993, 1.000] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 347.3 [351.6] | 351.9 [356.4] | 1.013 [0.932, 1.092] | no detectable difference |
| scroll / mainScriptMs | 47.1 [48.7] | 45.4 [45.6] | 0.965 [0.920, 1.045] | no detectable difference |
| jump / settleMs | 81.1 [82.2] | 83.0 [83.6] | +1.9 [-21.5, +18.0] | no detectable difference |
| jump / mainTaskMs | 47.1 [49.4] | 47.7 [47.9] | 1.012 [0.667, 1.417] | no detectable difference |
| jump / mainScriptMs | 11.9 [12.0] | 12.5 [12.7] | 1.048 [0.962, 1.262] | no detectable difference |
| zoom / settleMs | 308.8 [310.8] | 287.6 [315.8] | -21.2 [-47.9, +37.8] | no detectable difference |
| zoom / zoom2SettleMs | 110.8 [111.9] | 104.0 [116.2] | -6.7 [-23.7, +11.2] | no detectable difference |
| zoom / zoom4SettleMs | 196.8 [203.9] | 183.5 [199.5] | -13.3 [-29.9, +30.9] | no detectable difference |
| zoom / backTo1SettleMs | 56.9 [68.9] | 53.3 [58.0] | -3.6 [-29.9, +4.3] | no detectable difference |
| zoom / mainTaskMs | 138.4 [138.9] | 136.7 [141.5] | 0.987 [0.749, 1.030] | no detectable difference |
| zoom / mainScriptMs | 20.2 [20.6] | 20.4 [20.4] | 1.009 [0.954, 1.056] | no detectable difference |
| select / settleMs | 141.1 [141.3] | 143.8 [154.7] | +2.8 [-0.4, +14.9] | no detectable difference |
| select / mainTaskMs | 35.4 [42.5] | 42.9 [43.1] | 1.215 [0.361, 2.613] | no detectable difference |
| select / mainScriptMs | 8.2 [10.0] | 9.8 [10.0] | 1.207 [0.357, 2.623] | no detectable difference |
| search / searchMs | 51.5 [51.5] | 49.1 [50.6] | 0.953 [0.681, 1.445] | no detectable difference |
| search / mainTaskMs | 4.0 [4.0] | 3.8 [4.1] | 0.948 [0.731, 1.336] | no detectable difference |
| search / mainScriptMs | 0.1 [0.1] | 0.1 [0.1] | 1.073 [0.928, 1.303] | no detectable difference |
| annotate / settleMs | 155.8 [158.9] | 154.2 [155.7] | -1.6 [-7.0, +4.0] | no detectable difference |
| annotate / mainTaskMs | 49.2 [53.2] | 50.1 [50.8] | 1.018 [0.875, 1.799] | no detectable difference |
| annotate / mainScriptMs | 9.9 [10.9] | 10.1 [10.5] | 1.019 [0.863, 1.944] | no detectable difference |

## `impl-ts` vs `impl-rust`

| Row | impl-ts median [p95] | impl-rust median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 43.3 [43.7] | 43.1 [43.9] | 0.995 [0.983, 1.030] | no detectable difference |
| open(ebook) / openedMs | 98.0 [98.6] | 98.5 [98.5] | 1.005 [0.985, 1.012] | no detectable difference |
| open(ebook) / firstPaintMs | 177.8 [178.1] | 179.6 [186.6] | 1.010 [1.008, 1.056] | no detectable difference |
| open(ebook) / settledMs | 272.5 [277.0] | 272.7 [277.3] | 1.001 [0.970, 1.028] | no detectable difference |
| open(ebook) / mainTaskMs | 91.7 [93.5] | 92.7 [94.4] | 1.011 [0.969, 1.033] | no detectable difference |
| open(ebook) / mainScriptMs | 15.4 [16.2] | 15.7 [16.2] | 1.015 [0.922, 1.053] | no detectable difference |
| open(large) / readyMs | 44.7 [45.6] | 43.1 [45.1] | 0.963 [0.930, 1.056] | no detectable difference |
| open(large) / openedMs | 113.8 [116.0] | 113.8 [114.7] | 1.000 [0.970, 1.019] | no detectable difference |
| open(large) / firstPaintMs | 184.2 [190.0] | 186.2 [187.8] | 1.011 [0.975, 1.037] | no detectable difference |
| open(large) / settledMs | 180.8 [185.9] | 182.1 [182.6] | 1.007 [0.976, 1.032] | no detectable difference |
| open(large) / mainTaskMs | 205.9 [207.6] | 205.5 [207.2] | 0.998 [0.981, 1.012] | no detectable difference |
| open(large) / mainScriptMs | 18.3 [18.3] | 17.6 [17.9] | 0.961 [0.949, 1.014] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2001.2 [2002.0] | 2017.8 [2018.5] | 1.008 [1.004, 1.009] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.001] | no detectable difference |
| scroll / frameP95Ms | 8.8 [8.8] | 8.6 [8.8] | 0.974 [0.954, 1.009] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 351.9 [356.4] | 346.1 [364.5] | 0.984 [0.952, 1.117] | no detectable difference |
| scroll / mainScriptMs | 45.4 [45.6] | 47.4 [47.5] | 1.043 [1.012, 1.056] | no detectable difference |
| jump / settleMs | 83.0 [83.6] | 81.7 [82.6] | -1.3 [-21.6, +21.9] | no detectable difference |
| jump / mainTaskMs | 47.7 [47.9] | 44.4 [47.0] | 0.932 [0.689, 1.427] | no detectable difference |
| jump / mainScriptMs | 12.5 [12.7] | 12.4 [14.2] | 0.993 [0.920, 1.254] | no detectable difference |
| zoom / settleMs | 287.6 [315.8] | 294.3 [296.0] | +6.7 [-67.3, +33.1] | no detectable difference |
| zoom / zoom2SettleMs | 104.0 [116.2] | 111.6 [117.4] | +7.5 [-34.7, +29.7] | no detectable difference |
| zoom / zoom4SettleMs | 183.5 [199.5] | 178.1 [182.3] | -5.4 [-32.6, +8.0] | no detectable difference |
| zoom / backTo1SettleMs | 53.3 [58.0] | 53.5 [53.7] | +0.3 [-19.2, +13.4] | no detectable difference |
| zoom / mainTaskMs | 136.7 [141.5] | 135.2 [138.9] | 0.989 [0.746, 1.339] | no detectable difference |
| zoom / mainScriptMs | 20.4 [20.4] | 19.8 [22.6] | 0.972 [0.877, 1.164] | no detectable difference |
| select / settleMs | 143.8 [154.7] | 143.1 [147.4] | -0.7 [-15.0, +6.9] | no detectable difference |
| select / mainTaskMs | 42.9 [43.1] | 41.5 [44.5] | 0.965 [0.386, 2.864] | no detectable difference |
| select / mainScriptMs | 9.8 [10.0] | 10.0 [10.4] | 1.017 [0.391, 2.860] | no detectable difference |
| search / searchMs | 49.1 [50.6] | 45.3 [48.9] | 0.924 [0.712, 1.406] | no detectable difference |
| search / mainTaskMs | 3.8 [4.1] | 3.9 [4.2] | 1.042 [0.746, 1.463] | no detectable difference |
| search / mainScriptMs | 0.1 [0.1] | 0.1 [0.1] | 0.913 [0.776, 1.089] | no detectable difference |
| annotate / settleMs | 154.2 [155.7] | 153.3 [155.1] | -0.9 [-3.5, +3.0] | no detectable difference |
| annotate / mainTaskMs | 50.1 [50.8] | 50.4 [52.9] | 1.007 [0.930, 1.134] | no detectable difference |
| annotate / mainScriptMs | 10.1 [10.5] | 10.0 [11.2] | 0.992 [0.911, 1.192] | no detectable difference |

## `impl-ts` vs `plugins`

| Row | impl-ts median [p95] | plugins median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 43.3 [43.7] | 68.4 [69.1] | 1.579 [1.526, 1.622] | slower |
| open(ebook) / openedMs | 98.0 [98.6] | 169.3 [169.6] | 1.728 [1.683, 1.742] | slower |
| open(ebook) / firstPaintMs | 177.8 [178.1] | 327.2 [329.3] | 1.840 [1.816, 1.858] | slower |
| open(ebook) / settledMs | 272.5 [277.0] | 518.5 [535.2] | 1.903 [1.849, 1.987] | slower |
| open(ebook) / mainTaskMs | 91.7 [93.5] | 412.1 [435.3] | 4.495 [4.095, 4.785] | slower |
| open(ebook) / mainScriptMs | 15.4 [16.2] | 221.9 [238.6] | 14.368 [12.459, 15.600] | slower |
| open(large) / readyMs | 44.7 [45.6] | 66.5 [68.1] | 1.486 [1.455, 1.592] | slower |
| open(large) / openedMs | 113.8 [116.0] | 1362.0 [1375.1] | 11.973 [11.655, 12.219] | slower |
| open(large) / firstPaintMs | 184.2 [190.0] | 2229.2 [2275.3] | 12.101 [11.559, 12.576] | slower |
| open(large) / settledMs | 180.8 [185.9] | 2318.6 [2340.6] | 12.822 [12.423, 13.235] | slower |
| open(large) / mainTaskMs | 205.9 [207.6] | 2642.1 [2663.7] | 12.832 [12.699, 13.001] | slower |
| open(large) / mainScriptMs | 18.3 [18.3] | 2170.5 [2186.3] | 118.854 [118.371, 123.800] | slower |
| scroll / settleMs | 0.0 [0.0] | 227.8 [235.8] | +227.8 [+50.2, +236.7] | slower |
| scroll / wallMs | 2001.2 [2002.0] | 16792.6 [17003.0] | 8.391 [8.043, 8.509] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 41.7 [49.2] | 4.999 [3.999, 6.002] | slower |
| scroll / frameP95Ms | 8.8 [8.8] | 218.3 [231.8] | 24.741 [22.654, 26.555] | slower |
| scroll / jankFrames | 0 [0] | 100 [103.60] | +100 [+96, +104] | slower |
| scroll / longTasks | 0 [0] | 63 [66.60] | +63 [+62, +67] | slower |
| scroll / longTaskMs | 0.0 [0.0] | 9732.0 [10293.6] | +9732.0 [+9349.0, +10356.0] | slower |
| scroll / mainTaskMs | 351.9 [356.4] | 17206.1 [17419.1] | 48.895 [46.396, 53.163] | slower |
| scroll / mainScriptMs | 45.4 [45.6] | 9550.1 [9804.3] | 210.178 [198.652, 218.685] | slower |
| jump / settleMs | 83.0 [83.6] | 879.7 [901.7] | +796.7 [+770.9, +843.3] | slower |
| jump / mainTaskMs | 47.7 [47.9] | 1243.6 [1268.6] | 26.069 [25.056, 38.397] | slower |
| jump / mainScriptMs | 12.5 [12.7] | 789.0 [797.5] | 63.015 [59.857, 69.456] | slower |
| zoom / settleMs | 287.6 [315.8] | 533.4 [538.5] | +245.9 [+160.7, +276.0] | slower |
| zoom / zoom2SettleMs | 104.0 [116.2] | 249.2 [251.8] | +145.2 [+97.1, +163.8] | slower |
| zoom / zoom4SettleMs | 183.5 [199.5] | 281.3 [289.0] | +97.8 [+63.6, +115.1] | slower |
| zoom / backTo1SettleMs | 53.3 [58.0] | 113.8 [117.0] | +60.5 [+48.9, +77.0] | slower |
| zoom / mainTaskMs | 136.7 [141.5] | 928.7 [939.0] | 6.794 [6.479, 9.037] | slower |
| zoom / mainScriptMs | 20.4 [20.4] | 525.3 [531.3] | 25.797 [25.423, 27.017] | slower |
| select / settleMs | 143.8 [154.7] | 306.9 [325.4] | +163.0 [+111.1, +186.4] | slower |
| select / mainTaskMs | 42.9 [43.1] | 239.2 [246.9] | 5.569 [4.928, 15.836] | slower |
| select / mainScriptMs | 9.8 [10.0] | 188.7 [195.2] | 19.163 [16.758, 53.524] | slower |
| search / searchMs | 49.1 [50.6] | 76.4 [77.2] | 1.557 [1.176, 2.204] | slower |
| search / mainTaskMs | 3.8 [4.1] | 36.1 [36.3] | 9.553 [7.360, 12.417] | slower |
| search / mainScriptMs | 0.1 [0.1] | 18.7 [19.1] | 181.689 [148.655, 213.167] | slower |
| annotate / settleMs | 154.2 [155.7] | 332.0 [334.3] | +177.8 [+173.8, +182.3] | slower |
| annotate / mainTaskMs | 50.1 [50.8] | 282.1 [286.4] | 5.634 [5.540, 6.113] | slower |
| annotate / mainScriptMs | 10.1 [10.5] | 210.6 [213.5] | 20.902 [19.896, 22.516] | slower |

## `main` vs `plugins`

| Row | main median [p95] | plugins median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 44.1 [45.3] | 68.4 [69.1] | 1.551 [1.471, 1.605] | slower |
| open(ebook) / openedMs | 96.1 [96.2] | 169.3 [169.6] | 1.761 [1.726, 1.794] | slower |
| open(ebook) / firstPaintMs | 178.8 [180.1] | 327.2 [329.3] | 1.830 [1.794, 1.940] | slower |
| open(ebook) / settledMs | 267.4 [268.7] | 518.5 [535.2] | 1.939 [1.909, 2.034] | slower |
| open(ebook) / mainTaskMs | 94.9 [95.2] | 412.1 [435.3] | 4.341 [4.029, 4.673] | slower |
| open(ebook) / mainScriptMs | 16.0 [16.4] | 221.9 [238.6] | 13.899 [12.314, 15.148] | slower |
| open(large) / readyMs | 42.8 [43.9] | 66.5 [68.1] | 1.554 [1.509, 1.637] | slower |
| open(large) / openedMs | 108.6 [110.6] | 1362.0 [1375.1] | 12.547 [12.222, 12.718] | slower |
| open(large) / firstPaintMs | 184.6 [185.2] | 2229.2 [2275.3] | 12.077 [11.895, 13.084] | slower |
| open(large) / settledMs | 180.4 [180.9] | 2318.6 [2340.6] | 12.852 [12.798, 13.775] | slower |
| open(large) / mainTaskMs | 205.7 [206.9] | 2642.1 [2663.7] | 12.843 [12.750, 13.017] | slower |
| open(large) / mainScriptMs | 18.2 [18.2] | 2170.5 [2186.3] | 119.409 [118.806, 120.673] | slower |
| scroll / settleMs | 0.0 [0.0] | 227.8 [235.8] | +227.8 [+50.2, +236.7] | slower |
| scroll / wallMs | 2008.8 [2017.6] | 16792.6 [17003.0] | 8.360 [7.977, 8.504] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 41.7 [49.2] | 4.999 [3.999, 5.999] | slower |
| scroll / frameP95Ms | 8.8 [8.9] | 218.3 [231.8] | 24.699 [22.597, 26.437] | slower |
| scroll / jankFrames | 0 [0] | 100 [103.60] | +100 [+96, +104] | slower |
| scroll / longTasks | 0 [0] | 63 [66.60] | +63 [+62, +67] | slower |
| scroll / longTaskMs | 0.0 [0.0] | 9732.0 [10293.6] | +9732.0 [+9349.0, +10356.0] | slower |
| scroll / mainTaskMs | 347.3 [351.6] | 17206.1 [17419.1] | 49.537 [47.032, 53.364] | slower |
| scroll / mainScriptMs | 47.1 [48.7] | 9550.1 [9804.3] | 202.912 [185.417, 225.165] | slower |
| jump / settleMs | 81.1 [82.2] | 879.7 [901.7] | +798.6 [+772.2, +838.5] | slower |
| jump / mainTaskMs | 47.1 [49.4] | 1243.6 [1268.6] | 26.392 [24.190, 37.614] | slower |
| jump / mainScriptMs | 11.9 [12.0] | 789.0 [797.5] | 66.071 [63.788, 79.110] | slower |
| zoom / settleMs | 308.8 [310.8] | 533.4 [538.5] | +224.6 [+168.6, +257.9] | slower |
| zoom / zoom2SettleMs | 110.8 [111.9] | 249.2 [251.8] | +138.5 [+102.6, +145.7] | slower |
| zoom / zoom4SettleMs | 196.8 [203.9] | 281.3 [289.0] | +84.5 [+60.3, +119.5] | slower |
| zoom / backTo1SettleMs | 56.9 [68.9] | 113.8 [117.0] | +56.9 [+37.2, +63.2] | slower |
| zoom / mainTaskMs | 138.4 [138.9] | 928.7 [939.0] | 6.708 [6.619, 6.823] | slower |
| zoom / mainScriptMs | 20.2 [20.6] | 525.3 [531.3] | 26.020 [25.107, 27.576] | slower |
| select / settleMs | 141.1 [141.3] | 306.9 [325.4] | +165.8 [+125.6, +186.5] | slower |
| select / mainTaskMs | 35.4 [42.5] | 239.2 [246.9] | 6.765 [4.898, 15.029] | slower |
| select / mainScriptMs | 8.2 [10.0] | 188.7 [195.2] | 23.123 [16.459, 51.056] | slower |
| search / searchMs | 51.5 [51.5] | 76.4 [77.2] | 1.483 [1.159, 2.200] | no detectable difference |
| search / mainTaskMs | 4.0 [4.0] | 36.1 [36.3] | 9.057 [7.588, 11.766] | slower |
| search / mainScriptMs | 0.1 [0.1] | 18.7 [19.1] | 194.938 [177.773, 215.562] | slower |
| annotate / settleMs | 155.8 [158.9] | 332.0 [334.3] | +176.2 [+170.3, +182.8] | slower |
| annotate / mainTaskMs | 49.2 [53.2] | 282.1 [286.4] | 5.738 [5.261, 10.140] | slower |
| annotate / mainScriptMs | 9.9 [10.9] | 210.6 [213.5] | 21.301 [19.131, 39.293] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | impl-ts median [p95] | impl-rust median [p95] | plugins median [p95] |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.34 MiB [34.34 MiB] | 35.02 MiB [35.03 MiB] | 34.97 MiB [34.97 MiB] | 43.34 MiB [43.35 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.00 MiB [23.00 MiB] | 22.95 MiB [22.95 MiB] | 23.03 MiB [23.03 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.15 MiB] | 48.15 MiB [48.16 MiB] | 48.86 MiB [48.87 MiB] | 48.81 MiB [48.81 MiB] | 58.10 MiB [58.10 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 23.01 MiB [23.01 MiB] | 22.96 MiB [22.96 MiB] | 22.94 MiB [22.94 MiB] |
| soak / heapCycle5Bytes | 37.42 MiB [37.43 MiB] | 37.41 MiB [37.42 MiB] | 38.19 MiB [38.19 MiB] | 38.14 MiB [38.14 MiB] | 49.05 MiB [49.07 MiB] |
| soak / heapCycle30Bytes | 38.28 MiB [38.29 MiB] | 38.28 MiB [38.28 MiB] | 39.05 MiB [39.06 MiB] | 38.99 MiB [38.99 MiB] | 63.10 MiB [63.11 MiB] |
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

