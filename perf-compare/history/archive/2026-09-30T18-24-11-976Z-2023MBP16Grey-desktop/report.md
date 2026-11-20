# Viewer perf comparison: 2026-09-30T18-24-11-976Z-2023MBP16Grey-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 8d21589dc2130d94712a6082ae55e2a016d463de |
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
| impl-rust | research/multi-platform-rust-core-impl | c8085bd3c | built (fork d045f389d, wasm32-eh) | 0f7407599d93 |  |
| plugins | research/multi-platform-rust-core-impl-plugins | bef223d9d | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 992f38174937 |
| plugins-lto | research/multi-platform-rust-core-impl-plugins | bef223d9d | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | ce0b35ed118c |
| plugins-getstate | research/rust-kernel-wire | 776f0c265 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 764d1ef49e62 |
| plugins-toolbar | research/rust-kernel-toolbar | e4d533d18 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 764d1ef49e62 |

## Gate

- `main`: pass
- `main-aa`: pass
- `impl-ts`: pass
- `impl-rust`: pass
- `plugins`: pass
- `plugins-lto`: pass
- `plugins-getstate`: pass
- `plugins-toolbar`: pass

## Sizes

| Metric | main | main-aa | impl-ts | impl-rust | plugins | plugins-lto | plugins-getstate | plugins-toolbar |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB | 10.57 MiB | 10.57 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB | 2.83 MiB | 2.83 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 0.00 MiB | 10.78 MiB | 11.68 MiB | 11.69 MiB | 11.69 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 0.00 MiB | 1.77 MiB | 1.88 MiB | 1.88 MiB | 1.88 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 3.35 MiB | 3.35 MiB | 2.62 MiB | 2.62 MiB | 2.62 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

39 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.6 [58.7] | 53.1 [54.6] | 0.992 [0.974, 1.025] | pass |
| open(ebook) / openedMs | 117.4 [122.1] | 116.2 [117.8] | 0.990 [0.967, 1.021] | pass |
| open(ebook) / firstPaintMs | 215.6 [220.0] | 216.3 [218.8] | 1.003 [0.988, 1.033] | pass |
| open(ebook) / settledMs | 336.8 [341.5] | 337.7 [340.6] | 1.003 [0.989, 1.019] | pass |
| open(ebook) / mainTaskMs | 117.2 [124.5] | 118.2 [119.4] | 1.009 [0.968, 1.024] | pass |
| open(ebook) / mainScriptMs | 20.4 [22.3] | 20.2 [21.7] | 0.993 [0.935, 1.053] | pass |
| open(large) / readyMs | 53.0 [54.1] | 52.3 [53.3] | 0.986 [0.971, 0.998] | pass |
| open(large) / openedMs | 134.7 [136.9] | 134.0 [135.9] | 0.995 [0.982, 1.013] | pass |
| open(large) / firstPaintMs | 224.3 [228.8] | 223.8 [229.3] | 0.998 [0.984, 1.012] | pass |
| open(large) / settledMs | 221.3 [227.9] | 220.5 [226.0] | 0.997 [0.980, 1.014] | pass |
| open(large) / mainTaskMs | 260.1 [265.5] | 260.0 [264.7] | 1.000 [0.980, 1.013] | pass |
| open(large) / mainScriptMs | 23.5 [25.4] | 23.3 [24.4] | 0.989 [0.950, 1.032] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2018.2 [2029.9] | 2013.6 [2026.1] | 0.998 [0.993, 1.007] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [9.6] | 8.4 [9.1] | 1.002 [0.952, 1.007] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 401.9 [418.8] | 399.0 [407.7] | 0.993 [0.965, 1.031] | pass |
| scroll / mainScriptMs | 54.3 [57.6] | 54.0 [56.0] | 0.995 [0.949, 1.027] | pass |
| jump / settleMs | 100.1 [103.9] | 100.0 [105.1] | -0.1 [-4.0, +3.7] | pass |
| jump / mainTaskMs | 56.3 [58.3] | 56.5 [62.4] | 1.004 [0.968, 1.082] | pass |
| jump / mainScriptMs | 14.8 [16.5] | 14.6 [15.9] | 0.986 [0.936, 1.047] | pass |
| zoom / settleMs | 405.5 [417.0] | 395.2 [409.9] | -10.3 [-18.1, +3.4] | pass |
| zoom / zoom2SettleMs | 133.1 [137.6] | 131.9 [136.8] | -1.2 [-7.2, +5.8] | pass |
| zoom / zoom4SettleMs | 272.3 [281.6] | 265.3 [274.8] | -7.0 [-17.8, +5.4] | pass |
| zoom / backTo1SettleMs | 77.5 [87.8] | 76.4 [90.6] | -1.1 [-9.6, +9.4] | pass |
| zoom / mainTaskMs | 177.9 [181.2] | 173.8 [179.2] | 0.977 [0.950, 1.015] | pass |
| zoom / mainScriptMs | 24.8 [27.1] | 26.3 [28.4] | 1.059 [0.978, 1.125] | pass |
| select / settleMs | 148.5 [150.7] | 147.5 [152.7] | -1.0 [-6.1, +1.5] | pass |
| select / mainTaskMs | 47.9 [49.3] | 47.0 [50.3] | 0.983 [0.924, 1.035] | pass |
| select / mainScriptMs | 10.4 [10.9] | 10.4 [11.4] | 1.003 [0.918, 1.072] | pass |
| search / searchMs | 59.7 [63.9] | 60.4 [63.3] | 1.012 [0.967, 1.053] | pass |
| search / mainTaskMs | 4.7 [5.4] | 4.7 [5.2] | 0.985 [0.894, 1.043] | pass |
| search / mainScriptMs | 0.1 [0.2] | 0.1 [0.2] | 1.051 [0.872, 1.272] | pass |
| annotate / settleMs | 155.1 [163.5] | 156.0 [165.1] | +0.9 [-4.1, +6.0] | pass |
| annotate / mainTaskMs | 57.8 [62.4] | 58.7 [62.7] | 1.016 [0.920, 1.075] | pass |
| annotate / mainScriptMs | 11.5 [12.1] | 11.2 [12.4] | 0.971 [0.906, 1.056] | pass |

## `main` vs `impl-ts`

| Row | main median [p95] | impl-ts median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.6 [58.7] | 52.6 [55.2] | 0.983 [0.954, 1.016] | no detectable difference |
| open(ebook) / openedMs | 117.4 [122.1] | 119.0 [125.4] | 1.014 [0.991, 1.051] | no detectable difference |
| open(ebook) / firstPaintMs | 215.6 [220.0] | 221.9 [225.4] | 1.029 [0.999, 1.061] | no detectable difference |
| open(ebook) / settledMs | 336.8 [341.5] | 344.9 [350.8] | 1.024 [1.009, 1.042] | no detectable difference |
| open(ebook) / mainTaskMs | 117.2 [124.5] | 117.5 [119.3] | 1.003 [0.968, 1.018] | no detectable difference |
| open(ebook) / mainScriptMs | 20.4 [22.3] | 20.0 [21.1] | 0.981 [0.925, 1.025] | no detectable difference |
| open(large) / readyMs | 53.0 [54.1] | 52.2 [54.9] | 0.985 [0.966, 1.009] | no detectable difference |
| open(large) / openedMs | 134.7 [136.9] | 138.1 [142.3] | 1.025 [1.009, 1.051] | no detectable difference |
| open(large) / firstPaintMs | 224.3 [228.8] | 228.9 [234.7] | 1.020 [1.011, 1.039] | no detectable difference |
| open(large) / settledMs | 221.3 [227.9] | 225.8 [232.0] | 1.020 [1.005, 1.039] | no detectable difference |
| open(large) / mainTaskMs | 260.1 [265.5] | 261.5 [265.5] | 1.005 [0.982, 1.024] | no detectable difference |
| open(large) / mainScriptMs | 23.5 [25.4] | 23.3 [25.0] | 0.989 [0.944, 1.045] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2018.2 [2029.9] | 2017.4 [2034.4] | 1.000 [0.992, 1.011] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [9.6] | 8.4 [9.5] | 1.004 [0.968, 1.057] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 401.9 [418.8] | 401.0 [437.9] | 0.998 [0.957, 1.058] | no detectable difference |
| scroll / mainScriptMs | 54.3 [57.6] | 54.7 [60.7] | 1.007 [0.942, 1.066] | no detectable difference |
| jump / settleMs | 100.1 [103.9] | 100.4 [104.9] | +0.3 [-5.8, +4.2] | no detectable difference |
| jump / mainTaskMs | 56.3 [58.3] | 57.5 [62.1] | 1.022 [0.968, 1.099] | no detectable difference |
| jump / mainScriptMs | 14.8 [16.5] | 15.7 [18.5] | 1.058 [0.994, 1.130] | no detectable difference |
| zoom / settleMs | 405.5 [417.0] | 403.1 [421.1] | -2.4 [-10.6, +14.0] | no detectable difference |
| zoom / zoom2SettleMs | 133.1 [137.6] | 139.3 [143.0] | +6.2 [-0.3, +12.6] | no detectable difference |
| zoom / zoom4SettleMs | 272.3 [281.6] | 264.9 [282.3] | -7.3 [-17.9, +12.2] | no detectable difference |
| zoom / backTo1SettleMs | 77.5 [87.8] | 74.3 [87.8] | -3.2 [-12.4, +3.0] | no detectable difference |
| zoom / mainTaskMs | 177.9 [181.2] | 177.1 [184.8] | 0.995 [0.960, 1.039] | no detectable difference |
| zoom / mainScriptMs | 24.8 [27.1] | 25.7 [28.6] | 1.036 [0.973, 1.114] | no detectable difference |
| select / settleMs | 148.5 [150.7] | 149.7 [155.8] | +1.2 [-4.0, +4.1] | no detectable difference |
| select / mainTaskMs | 47.9 [49.3] | 48.0 [49.4] | 1.002 [0.952, 1.026] | no detectable difference |
| select / mainScriptMs | 10.4 [10.9] | 10.5 [11.1] | 1.009 [0.935, 1.058] | no detectable difference |
| search / searchMs | 59.7 [63.9] | 59.4 [62.2] | 0.994 [0.940, 1.030] | no detectable difference |
| search / mainTaskMs | 4.7 [5.4] | 4.5 [6.5] | 0.954 [0.875, 1.072] | no detectable difference |
| search / mainScriptMs | 0.1 [0.2] | 0.2 [0.3] | 1.144 [0.851, 1.379] | no detectable difference |
| annotate / settleMs | 155.1 [163.5] | 157.6 [163.8] | +2.5 [-1.9, +6.5] | no detectable difference |
| annotate / mainTaskMs | 57.8 [62.4] | 59.5 [64.9] | 1.030 [0.973, 1.067] | no detectable difference |
| annotate / mainScriptMs | 11.5 [12.1] | 11.4 [12.9] | 0.992 [0.930, 1.061] | no detectable difference |

## `impl-ts` vs `impl-rust`

| Row | impl-ts median [p95] | impl-rust median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.6 [55.2] | 53.7 [54.6] | 1.020 [0.991, 1.042] | no detectable difference |
| open(ebook) / openedMs | 119.0 [125.4] | 121.2 [124.4] | 1.019 [0.992, 1.036] | no detectable difference |
| open(ebook) / firstPaintMs | 221.9 [225.4] | 222.5 [225.7] | 1.003 [0.990, 1.033] | no detectable difference |
| open(ebook) / settledMs | 344.9 [350.8] | 344.9 [346.5] | 1.000 [0.991, 1.014] | no detectable difference |
| open(ebook) / mainTaskMs | 117.5 [119.3] | 118.1 [121.3] | 1.005 [0.983, 1.024] | no detectable difference |
| open(ebook) / mainScriptMs | 20.0 [21.1] | 20.2 [21.4] | 1.010 [0.976, 1.068] | no detectable difference |
| open(large) / readyMs | 52.2 [54.9] | 52.8 [54.6] | 1.011 [0.976, 1.035] | no detectable difference |
| open(large) / openedMs | 138.1 [142.3] | 138.8 [140.8] | 1.005 [0.981, 1.024] | no detectable difference |
| open(large) / firstPaintMs | 228.9 [234.7] | 230.0 [232.9] | 1.005 [0.986, 1.011] | no detectable difference |
| open(large) / settledMs | 225.8 [232.0] | 227.5 [229.9] | 1.008 [0.991, 1.016] | no detectable difference |
| open(large) / mainTaskMs | 261.5 [265.5] | 261.3 [264.7] | 0.999 [0.975, 1.022] | no detectable difference |
| open(large) / mainScriptMs | 23.3 [25.0] | 23.2 [24.6] | 0.999 [0.945, 1.048] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2017.4 [2034.4] | 2015.4 [2032.8] | 0.999 [0.990, 1.007] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [9.5] | 8.4 [8.9] | 0.995 [0.956, 1.006] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 401.0 [437.9] | 397.7 [421.1] | 0.992 [0.943, 1.043] | no detectable difference |
| scroll / mainScriptMs | 54.7 [60.7] | 53.7 [57.1] | 0.982 [0.921, 1.064] | no detectable difference |
| jump / settleMs | 100.4 [104.9] | 100.1 [102.2] | -0.4 [-4.3, +4.2] | no detectable difference |
| jump / mainTaskMs | 57.5 [62.1] | 55.5 [58.6] | 0.964 [0.910, 1.023] | no detectable difference |
| jump / mainScriptMs | 15.7 [18.5] | 15.0 [18.0] | 0.954 [0.882, 1.046] | no detectable difference |
| zoom / settleMs | 403.1 [421.1] | 401.9 [419.1] | -1.2 [-11.5, +9.9] | no detectable difference |
| zoom / zoom2SettleMs | 139.3 [143.0] | 135.9 [140.8] | -3.4 [-9.2, +3.1] | no detectable difference |
| zoom / zoom4SettleMs | 264.9 [282.3] | 270.1 [283.1] | +5.1 [-11.4, +17.1] | no detectable difference |
| zoom / backTo1SettleMs | 74.3 [87.8] | 79.2 [90.0] | +4.9 [-1.3, +16.7] | no detectable difference |
| zoom / mainTaskMs | 177.1 [184.8] | 177.0 [185.0] | 0.999 [0.967, 1.047] | no detectable difference |
| zoom / mainScriptMs | 25.7 [28.6] | 26.3 [28.8] | 1.022 [0.945, 1.137] | no detectable difference |
| select / settleMs | 149.7 [155.8] | 149.7 [152.2] | +0.0 [-4.4, +5.0] | no detectable difference |
| select / mainTaskMs | 48.0 [49.4] | 47.8 [49.5] | 0.996 [0.972, 1.052] | no detectable difference |
| select / mainScriptMs | 10.5 [11.1] | 10.5 [11.0] | 0.999 [0.936, 1.081] | no detectable difference |
| search / searchMs | 59.4 [62.2] | 61.6 [63.3] | 1.038 [0.991, 1.072] | no detectable difference |
| search / mainTaskMs | 4.5 [6.5] | 5.0 [5.3] | 1.100 [0.943, 1.203] | no detectable difference |
| search / mainScriptMs | 0.2 [0.3] | 0.2 [0.2] | 1.069 [0.814, 1.395] | no detectable difference |
| annotate / settleMs | 157.6 [163.8] | 155.6 [168.9] | -2.1 [-5.3, +3.4] | no detectable difference |
| annotate / mainTaskMs | 59.5 [64.9] | 58.1 [60.8] | 0.976 [0.933, 1.031] | no detectable difference |
| annotate / mainScriptMs | 11.4 [12.9] | 11.0 [11.8] | 0.966 [0.920, 1.051] | no detectable difference |

## `impl-ts` vs `plugins`

| Row | impl-ts median [p95] | plugins median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.6 [55.2] | 84.8 [86.1] | 1.611 [1.573, 1.648] | slower |
| open(ebook) / openedMs | 119.0 [125.4] | 216.9 [219.1] | 1.823 [1.775, 1.847] | slower |
| open(ebook) / firstPaintMs | 221.9 [225.4] | 418.4 [431.3] | 1.886 [1.855, 1.952] | slower |
| open(ebook) / settledMs | 344.9 [350.8] | 691.6 [710.9] | 2.005 [1.979, 2.042] | slower |
| open(ebook) / mainTaskMs | 117.5 [119.3] | 568.9 [601.2] | 4.840 [4.759, 5.032] | slower |
| open(ebook) / mainScriptMs | 20.0 [21.1] | 314.9 [337.4] | 15.752 [15.290, 16.490] | slower |
| open(large) / readyMs | 52.2 [54.9] | 83.6 [85.0] | 1.600 [1.560, 1.625] | slower |
| open(large) / openedMs | 138.1 [142.3] | 1719.5 [1744.9] | 12.450 [12.170, 12.659] | slower |
| open(large) / firstPaintMs | 228.9 [234.7] | 2828.0 [2859.2] | 12.357 [12.100, 12.447] | slower |
| open(large) / settledMs | 225.8 [232.0] | 2913.7 [2997.8] | 12.907 [12.732, 13.135] | slower |
| open(large) / mainTaskMs | 261.5 [265.5] | 3237.5 [3335.6] | 12.381 [12.189, 12.676] | slower |
| open(large) / mainScriptMs | 23.3 [25.0] | 2642.1 [2729.3] | 113.598 [107.823, 116.500] | slower |
| scroll / settleMs | 0.0 [0.0] | 281.0 [297.3] | +281.0 [+173.9, +294.5] | slower |
| scroll / wallMs | 2017.4 [2034.4] | 21185.8 [22349.9] | 10.502 [10.363, 10.818] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 58.3 [58.3] | 6.999 [6.998, 6.999] | slower |
| scroll / frameP95Ms | 8.4 [9.5] | 283.8 [290.8] | 33.638 [30.623, 34.282] | slower |
| scroll / jankFrames | 0 [0] | 151.50 [164.10] | +151.50 [+140, +160] | slower |
| scroll / longTasks | 0 [0] | 230.50 [244.75] | +230.50 [+224.50, +238] | slower |
| scroll / longTaskMs | 0.0 [0.0] | 20966.0 [22481.4] | +20966.0 [+20733.0, +21784.8] | slower |
| scroll / mainTaskMs | 401.0 [437.9] | 21695.1 [22879.8] | 54.100 [51.798, 56.289] | slower |
| scroll / mainScriptMs | 54.7 [60.7] | 12325.0 [13145.6] | 225.500 [214.896, 238.683] | slower |
| jump / settleMs | 100.4 [104.9] | 1266.3 [1333.5] | +1165.9 [+1112.0, +1212.5] | slower |
| jump / mainTaskMs | 57.5 [62.1] | 1605.4 [1689.4] | 27.911 [26.457, 29.465] | slower |
| jump / mainScriptMs | 15.7 [18.5] | 1041.2 [1108.3] | 66.318 [61.747, 70.167] | slower |
| zoom / settleMs | 403.1 [421.1] | 673.6 [686.8] | +270.5 [+259.6, +281.8] | slower |
| zoom / zoom2SettleMs | 139.3 [143.0] | 312.9 [326.4] | +173.7 [+165.6, +183.7] | slower |
| zoom / zoom4SettleMs | 264.9 [282.3] | 361.7 [370.3] | +96.8 [+75.7, +109.6] | slower |
| zoom / backTo1SettleMs | 74.3 [87.8] | 143.9 [145.6] | +69.6 [+64.1, +74.3] | slower |
| zoom / mainTaskMs | 177.1 [184.8] | 1215.5 [1242.4] | 6.863 [6.644, 7.167] | slower |
| zoom / mainScriptMs | 25.7 [28.6] | 693.7 [708.2] | 27.001 [25.144, 28.626] | slower |
| select / settleMs | 149.7 [155.8] | 403.4 [409.9] | +253.8 [+245.9, +260.6] | slower |
| select / mainTaskMs | 48.0 [49.4] | 299.7 [304.6] | 6.249 [6.141, 6.558] | slower |
| select / mainScriptMs | 10.5 [11.1] | 237.3 [241.5] | 22.678 [21.879, 24.262] | slower |
| search / searchMs | 59.4 [62.2] | 91.8 [95.4] | 1.547 [1.499, 1.605] | slower |
| search / mainTaskMs | 4.5 [6.5] | 43.1 [45.2] | 9.538 [8.568, 10.240] | slower |
| search / mainScriptMs | 0.2 [0.3] | 23.1 [24.1] | 145.754 [125.472, 185.257] | slower |
| annotate / settleMs | 157.6 [163.8] | 454.2 [466.0] | +296.6 [+292.0, +299.6] | slower |
| annotate / mainTaskMs | 59.5 [64.9] | 364.2 [370.1] | 6.120 [5.977, 6.409] | slower |
| annotate / mainScriptMs | 11.4 [12.9] | 274.7 [277.5] | 24.028 [22.836, 25.237] | slower |

## `main` vs `plugins`

| Row | main median [p95] | plugins median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.6 [58.7] | 84.8 [86.1] | 1.583 [1.550, 1.628] | slower |
| open(ebook) / openedMs | 117.4 [122.1] | 216.9 [219.1] | 1.848 [1.807, 1.901] | slower |
| open(ebook) / firstPaintMs | 215.6 [220.0] | 418.4 [431.3] | 1.941 [1.913, 2.011] | slower |
| open(ebook) / settledMs | 336.8 [341.5] | 691.6 [710.9] | 2.053 [2.021, 2.099] | slower |
| open(ebook) / mainTaskMs | 117.2 [124.5] | 568.9 [601.2] | 4.855 [4.684, 5.042] | slower |
| open(ebook) / mainScriptMs | 20.4 [22.3] | 314.9 [337.4] | 15.453 [14.549, 16.315] | slower |
| open(large) / readyMs | 53.0 [54.1] | 83.6 [85.0] | 1.577 [1.551, 1.593] | slower |
| open(large) / openedMs | 134.7 [136.9] | 1719.5 [1744.9] | 12.764 [12.614, 12.970] | slower |
| open(large) / firstPaintMs | 224.3 [228.8] | 2828.0 [2859.2] | 12.607 [12.380, 12.769] | slower |
| open(large) / settledMs | 221.3 [227.9] | 2913.7 [2997.8] | 13.168 [12.942, 13.480] | slower |
| open(large) / mainTaskMs | 260.1 [265.5] | 3237.5 [3335.6] | 12.446 [12.221, 12.703] | slower |
| open(large) / mainScriptMs | 23.5 [25.4] | 2642.1 [2729.3] | 112.342 [107.723, 116.345] | slower |
| scroll / settleMs | 0.0 [0.0] | 281.0 [297.3] | +281.0 [+173.9, +294.5] | slower |
| scroll / wallMs | 2018.2 [2029.9] | 21185.8 [22349.9] | 10.498 [10.382, 10.822] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 58.3 [58.3] | 6.999 [6.998, 6.999] | slower |
| scroll / frameP95Ms | 8.4 [9.6] | 283.8 [290.8] | 33.776 [30.711, 34.403] | slower |
| scroll / jankFrames | 0 [0] | 151.50 [164.10] | +151.50 [+140, +160] | slower |
| scroll / longTasks | 0 [0] | 230.50 [244.75] | +230.50 [+224.50, +238] | slower |
| scroll / longTaskMs | 0.0 [0.0] | 20966.0 [22481.4] | +20966.0 [+20733.0, +21784.8] | slower |
| scroll / mainTaskMs | 401.9 [418.8] | 21695.1 [22879.8] | 53.980 [52.411, 56.574] | slower |
| scroll / mainScriptMs | 54.3 [57.6] | 12325.0 [13145.6] | 227.041 [216.361, 236.928] | slower |
| jump / settleMs | 100.1 [103.9] | 1266.3 [1333.5] | +1166.2 [+1112.5, +1211.9] | slower |
| jump / mainTaskMs | 56.3 [58.3] | 1605.4 [1689.4] | 28.536 [27.256, 30.596] | slower |
| jump / mainScriptMs | 14.8 [16.5] | 1041.2 [1108.3] | 70.132 [65.993, 73.642] | slower |
| zoom / settleMs | 405.5 [417.0] | 673.6 [686.8] | +268.1 [+262.0, +282.1] | slower |
| zoom / zoom2SettleMs | 133.1 [137.6] | 312.9 [326.4] | +179.9 [+171.0, +190.8] | slower |
| zoom / zoom4SettleMs | 272.3 [281.6] | 361.7 [370.3] | +89.4 [+76.4, +103.1] | slower |
| zoom / backTo1SettleMs | 77.5 [87.8] | 143.9 [145.6] | +66.3 [+57.8, +70.9] | slower |
| zoom / mainTaskMs | 177.9 [181.2] | 1215.5 [1242.4] | 6.831 [6.711, 7.054] | slower |
| zoom / mainScriptMs | 24.8 [27.1] | 693.7 [708.2] | 27.975 [26.760, 28.525] | slower |
| select / settleMs | 148.5 [150.7] | 403.4 [409.9] | +255.0 [+247.0, +259.1] | slower |
| select / mainTaskMs | 47.9 [49.3] | 299.7 [304.6] | 6.263 [6.139, 6.441] | slower |
| select / mainScriptMs | 10.4 [10.9] | 237.3 [241.5] | 22.882 [22.099, 23.812] | slower |
| search / searchMs | 59.7 [63.9] | 91.8 [95.4] | 1.537 [1.466, 1.583] | slower |
| search / mainTaskMs | 4.7 [5.4] | 43.1 [45.2] | 9.098 [8.503, 9.597] | slower |
| search / mainScriptMs | 0.1 [0.2] | 23.1 [24.1] | 166.801 [145.474, 189.171] | slower |
| annotate / settleMs | 155.1 [163.5] | 454.2 [466.0] | +299.1 [+293.9, +302.8] | slower |
| annotate / mainTaskMs | 57.8 [62.4] | 364.2 [370.1] | 6.305 [6.037, 6.517] | slower |
| annotate / mainScriptMs | 11.5 [12.1] | 274.7 [277.5] | 23.831 [22.925, 25.166] | slower |

## `plugins` vs `plugins-lto`

| Row | plugins median [p95] | plugins-lto median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 84.8 [86.1] | 85.9 [88.6] | 1.013 [0.997, 1.036] | no detectable difference |
| open(ebook) / openedMs | 216.9 [219.1] | 215.0 [220.9] | 0.991 [0.978, 1.006] | no detectable difference |
| open(ebook) / firstPaintMs | 418.4 [431.3] | 408.0 [442.4] | 0.975 [0.955, 1.033] | no detectable difference |
| open(ebook) / settledMs | 691.6 [710.9] | 660.5 [677.0] | 0.955 [0.939, 0.973] | faster |
| open(ebook) / mainTaskMs | 568.9 [601.2] | 539.2 [553.0] | 0.948 [0.909, 0.970] | faster |
| open(ebook) / mainScriptMs | 314.9 [337.4] | 294.5 [301.8] | 0.935 [0.881, 0.960] | no detectable difference |
| open(large) / readyMs | 83.6 [85.0] | 86.4 [89.7] | 1.033 [1.016, 1.052] | slower |
| open(large) / openedMs | 1719.5 [1744.9] | 1581.5 [1622.8] | 0.920 [0.908, 0.942] | faster |
| open(large) / firstPaintMs | 2828.0 [2859.2] | 2578.8 [2630.3] | 0.912 [0.883, 0.927] | faster |
| open(large) / settledMs | 2913.7 [2997.8] | 2668.3 [2717.7] | 0.916 [0.894, 0.933] | faster |
| open(large) / mainTaskMs | 3237.5 [3335.6] | 2955.1 [2995.5] | 0.913 [0.889, 0.924] | faster |
| open(large) / mainScriptMs | 2642.1 [2729.3] | 2417.9 [2452.6] | 0.915 [0.891, 0.926] | faster |
| scroll / settleMs | 281.0 [297.3] | 247.6 [303.3] | -33.4 [-128.3, +74.2] | no detectable difference |
| scroll / wallMs | 21185.8 [22349.9] | 18538.4 [19068.8] | 0.875 [0.844, 0.887] | faster |
| scroll / frameP50Ms | 58.3 [58.3] | 50.0 [50.0] | 0.857 [0.857, 0.857] | faster |
| scroll / frameP95Ms | 283.8 [290.8] | 241.7 [259.5] | 0.852 [0.819, 0.935] | faster |
| scroll / jankFrames | 151.50 [164.10] | 101.50 [110.85] | -50 [-59, -36.50] | faster |
| scroll / longTasks | 230.50 [244.75] | 82 [110.60] | -148.50 [-163, -128.50] | faster |
| scroll / longTaskMs | 20966.0 [22481.4] | 11632.0 [13386.8] | -9334.0 [-10473.5, -8196.0] | faster |
| scroll / mainTaskMs | 21695.1 [22879.8] | 18987.5 [19549.1] | 0.875 [0.844, 0.886] | faster |
| scroll / mainScriptMs | 12325.0 [13145.6] | 10594.4 [11033.6] | 0.860 [0.825, 0.880] | faster |
| jump / settleMs | 1266.3 [1333.5] | 1118.8 [1415.2] | -147.6 [-228.6, -11.9] | faster |
| jump / mainTaskMs | 1605.4 [1689.4] | 1476.0 [1808.3] | 0.919 [0.878, 1.009] | no detectable difference |
| jump / mainScriptMs | 1041.2 [1108.3] | 968.6 [1168.6] | 0.930 [0.875, 1.037] | no detectable difference |
| zoom / settleMs | 673.6 [686.8] | 637.1 [651.5] | -36.5 [-57.0, -25.8] | faster |
| zoom / zoom2SettleMs | 312.9 [326.4] | 291.5 [297.4] | -21.4 [-34.8, -13.0] | faster |
| zoom / zoom4SettleMs | 361.7 [370.3] | 342.7 [359.6] | -19.0 [-31.6, +0.1] | no detectable difference |
| zoom / backTo1SettleMs | 143.9 [145.6] | 135.3 [139.1] | -8.6 [-10.1, -5.6] | no detectable difference |
| zoom / mainTaskMs | 1215.5 [1242.4] | 1153.1 [1191.0] | 0.949 [0.925, 0.966] | faster |
| zoom / mainScriptMs | 693.7 [708.2] | 651.0 [677.2] | 0.938 [0.922, 0.959] | no detectable difference |
| select / settleMs | 403.4 [409.9] | 344.5 [382.2] | -58.9 [-71.2, -38.4] | faster |
| select / mainTaskMs | 299.7 [304.6] | 276.7 [281.1] | 0.923 [0.908, 0.937] | faster |
| select / mainScriptMs | 237.3 [241.5] | 217.2 [220.9] | 0.915 [0.901, 0.928] | faster |
| search / searchMs | 91.8 [95.4] | 90.7 [93.4] | 0.988 [0.947, 1.017] | no detectable difference |
| search / mainTaskMs | 43.1 [45.2] | 40.1 [41.4] | 0.932 [0.887, 0.946] | no detectable difference |
| search / mainScriptMs | 23.1 [24.1] | 21.3 [21.6] | 0.922 [0.885, 0.943] | no detectable difference |
| annotate / settleMs | 454.2 [466.0] | 436.9 [443.8] | -17.3 [-20.6, -13.4] | faster |
| annotate / mainTaskMs | 364.2 [370.1] | 340.9 [345.0] | 0.936 [0.924, 0.955] | no detectable difference |
| annotate / mainScriptMs | 274.7 [277.5] | 254.1 [257.5] | 0.925 [0.919, 0.948] | no detectable difference |

## `plugins-lto` vs `plugins-fix`

Not run: `plugins-fix`.

## `plugins-fix` vs `plugins-fix34`

Not run: `plugins-fix`, `plugins-fix34`.

## `plugins-fix34` vs `plugins-fix-leak`

Not run: `plugins-fix34`, `plugins-fix-leak`.

## `plugins-fix-leak` vs `plugins-fix-zoom`

Not run: `plugins-fix-leak`, `plugins-fix-zoom`.

## `plugins-lto` vs `plugins-fix-zoom`

Not run: `plugins-fix-zoom`.

## `main` vs `plugins-fix-zoom`

Not run: `plugins-fix-zoom`.

## `plugins-fix-zoom` vs `plugins-wire`

Not run: `plugins-fix-zoom`, `plugins-wire`.

## `plugins-wire` vs `plugins-getstate`

Not run: `plugins-wire`.

## `plugins-lto` vs `plugins-getstate`

| Row | plugins-lto median [p95] | plugins-getstate median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 85.9 [88.6] | 84.6 [85.7] | 0.985 [0.965, 0.998] | no detectable difference |
| open(ebook) / openedMs | 215.0 [220.9] | 184.9 [188.8] | 0.860 [0.850, 0.876] | faster |
| open(ebook) / firstPaintMs | 408.0 [442.4] | 312.4 [324.8] | 0.766 [0.726, 0.792] | faster |
| open(ebook) / settledMs | 660.5 [677.0] | 368.0 [375.3] | 0.557 [0.549, 0.568] | faster |
| open(ebook) / mainTaskMs | 539.2 [553.0] | 258.5 [262.1] | 0.479 [0.469, 0.489] | faster |
| open(ebook) / mainScriptMs | 294.5 [301.8] | 109.4 [110.8] | 0.371 [0.363, 0.386] | faster |
| open(large) / readyMs | 86.4 [89.7] | 85.3 [86.4] | 0.987 [0.958, 1.006] | no detectable difference |
| open(large) / openedMs | 1581.5 [1622.8] | 213.8 [215.9] | 0.135 [0.131, 0.137] | faster |
| open(large) / firstPaintMs | 2578.8 [2630.3] | 348.6 [351.4] | 0.135 [0.132, 0.140] | faster |
| open(large) / settledMs | 2668.3 [2717.7] | 352.8 [362.9] | 0.132 [0.130, 0.136] | faster |
| open(large) / mainTaskMs | 2955.1 [2995.5] | 527.4 [538.3] | 0.178 [0.176, 0.183] | faster |
| open(large) / mainScriptMs | 2417.9 [2452.6] | 324.2 [334.0] | 0.134 [0.133, 0.137] | faster |
| scroll / settleMs | 247.6 [303.3] | 0.0 [0.0] | -247.6 [-258.1, -153.1] | faster |
| scroll / wallMs | 18538.4 [19068.8] | 2414.6 [2459.1] | 0.130 [0.128, 0.134] | faster |
| scroll / frameP50Ms | 50.0 [50.0] | 8.3 [8.3] | 0.167 [0.167, 0.167] | faster |
| scroll / frameP95Ms | 241.7 [259.5] | 8.7 [9.7] | 0.036 [0.034, 0.038] | faster |
| scroll / jankFrames | 101.50 [110.85] | 0 [0] | -101.50 [-107, -100] | faster |
| scroll / longTasks | 82 [110.60] | 0 [0] | -82 [-100, -71] | faster |
| scroll / longTaskMs | 11632.0 [13386.8] | 0.0 [0.0] | -11632.0 [-12736.0, -11050.5] | faster |
| scroll / mainTaskMs | 18987.5 [19549.1] | 859.9 [886.2] | 0.045 [0.044, 0.047] | faster |
| scroll / mainScriptMs | 10594.4 [11033.6] | 362.3 [377.2] | 0.034 [0.033, 0.035] | faster |
| jump / settleMs | 1118.8 [1415.2] | 154.5 [165.7] | -964.2 [-1081.3, -889.4] | faster |
| jump / mainTaskMs | 1476.0 [1808.3] | 163.2 [189.0] | 0.111 [0.097, 0.129] | faster |
| jump / mainScriptMs | 968.6 [1168.6] | 108.7 [127.3] | 0.112 [0.095, 0.133] | faster |
| zoom / settleMs | 637.1 [651.5] | 448.6 [463.1] | -188.5 [-204.3, -169.5] | faster |
| zoom / zoom2SettleMs | 291.5 [297.4] | 161.0 [164.1] | -130.5 [-134.0, -119.2] | faster |
| zoom / zoom4SettleMs | 342.7 [359.6] | 287.5 [301.7] | -55.2 [-73.2, -44.0] | faster |
| zoom / backTo1SettleMs | 135.3 [139.1] | 101.4 [108.5] | -33.9 [-42.6, -28.5] | faster |
| zoom / mainTaskMs | 1153.1 [1191.0] | 313.4 [325.1] | 0.272 [0.265, 0.283] | faster |
| zoom / mainScriptMs | 651.0 [677.2] | 127.3 [131.1] | 0.196 [0.186, 0.201] | faster |
| select / settleMs | 344.5 [382.2] | 150.6 [155.4] | -193.9 [-213.0, -179.3] | faster |
| select / mainTaskMs | 276.7 [281.1] | 77.2 [80.5] | 0.279 [0.271, 0.285] | faster |
| select / mainScriptMs | 217.2 [220.9] | 37.2 [39.0] | 0.171 [0.167, 0.176] | faster |
| search / searchMs | 90.7 [93.4] | 69.4 [74.5] | 0.765 [0.702, 0.816] | faster |
| search / mainTaskMs | 40.1 [41.4] | 16.6 [23.8] | 0.414 [0.316, 0.581] | faster |
| search / mainScriptMs | 21.3 [21.6] | 6.6 [8.6] | 0.311 [0.293, 0.368] | faster |
| annotate / settleMs | 436.9 [443.8] | 166.0 [171.6] | -270.9 [-272.8, -266.2] | faster |
| annotate / mainTaskMs | 340.9 [345.0] | 96.5 [99.1] | 0.283 [0.275, 0.287] | faster |
| annotate / mainScriptMs | 254.1 [257.5] | 44.2 [45.7] | 0.174 [0.169, 0.177] | faster |

## `main` vs `plugins-getstate`

| Row | main median [p95] | plugins-getstate median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.6 [58.7] | 84.6 [85.7] | 1.579 [1.548, 1.621] | slower |
| open(ebook) / openedMs | 117.4 [122.1] | 184.9 [188.8] | 1.576 [1.541, 1.632] | slower |
| open(ebook) / firstPaintMs | 215.6 [220.0] | 312.4 [324.8] | 1.449 [1.433, 1.512] | slower |
| open(ebook) / settledMs | 336.8 [341.5] | 368.0 [375.3] | 1.093 [1.078, 1.119] | slower |
| open(ebook) / mainTaskMs | 117.2 [124.5] | 258.5 [262.1] | 2.206 [2.130, 2.242] | slower |
| open(ebook) / mainScriptMs | 20.4 [22.3] | 109.4 [110.8] | 5.369 [5.055, 5.586] | slower |
| open(large) / readyMs | 53.0 [54.1] | 85.3 [86.4] | 1.608 [1.559, 1.631] | slower |
| open(large) / openedMs | 134.7 [136.9] | 213.8 [215.9] | 1.587 [1.559, 1.610] | slower |
| open(large) / firstPaintMs | 224.3 [228.8] | 348.6 [351.4] | 1.554 [1.518, 1.575] | slower |
| open(large) / settledMs | 221.3 [227.9] | 352.8 [362.9] | 1.594 [1.571, 1.636] | slower |
| open(large) / mainTaskMs | 260.1 [265.5] | 527.4 [538.3] | 2.028 [1.993, 2.059] | slower |
| open(large) / mainScriptMs | 23.5 [25.4] | 324.2 [334.0] | 13.787 [13.214, 14.258] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2018.2 [2029.9] | 2414.6 [2459.1] | 1.196 [1.181, 1.214] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [9.6] | 8.7 [9.7] | 1.032 [0.987, 1.091] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 401.9 [418.8] | 859.9 [886.2] | 2.139 [2.071, 2.229] | slower |
| scroll / mainScriptMs | 54.3 [57.6] | 362.3 [377.2] | 6.674 [6.355, 6.883] | slower |
| jump / settleMs | 100.1 [103.9] | 154.5 [165.7] | +54.4 [+42.7, +58.6] | slower |
| jump / mainTaskMs | 56.3 [58.3] | 163.2 [189.0] | 2.900 [2.636, 3.429] | slower |
| jump / mainScriptMs | 14.8 [16.5] | 108.7 [127.3] | 7.320 [6.382, 8.562] | slower |
| zoom / settleMs | 405.5 [417.0] | 448.6 [463.1] | +43.1 [+31.4, +56.9] | slower |
| zoom / zoom2SettleMs | 133.1 [137.6] | 161.0 [164.1] | +28.0 [+22.7, +34.6] | slower |
| zoom / zoom4SettleMs | 272.3 [281.6] | 287.5 [301.7] | +15.2 [+4.2, +27.1] | no detectable difference |
| zoom / backTo1SettleMs | 77.5 [87.8] | 101.4 [108.5] | +23.8 [+14.0, +30.6] | slower |
| zoom / mainTaskMs | 177.9 [181.2] | 313.4 [325.1] | 1.761 [1.712, 1.848] | slower |
| zoom / mainScriptMs | 24.8 [27.1] | 127.3 [131.1] | 5.134 [4.873, 5.267] | slower |
| select / settleMs | 148.5 [150.7] | 150.6 [155.4] | +2.2 [-0.7, +6.2] | no detectable difference |
| select / mainTaskMs | 47.9 [49.3] | 77.2 [80.5] | 1.613 [1.568, 1.665] | slower |
| select / mainScriptMs | 10.4 [10.9] | 37.2 [39.0] | 3.586 [3.452, 3.742] | slower |
| search / searchMs | 59.7 [63.9] | 69.4 [74.5] | 1.162 [1.055, 1.229] | slower |
| search / mainTaskMs | 4.7 [5.4] | 16.6 [23.8] | 3.510 [2.633, 4.913] | slower |
| search / mainScriptMs | 0.1 [0.2] | 6.6 [8.6] | 47.888 [40.941, 58.488] | slower |
| annotate / settleMs | 155.1 [163.5] | 166.0 [171.6] | +10.9 [+6.7, +16.0] | slower |
| annotate / mainTaskMs | 57.8 [62.4] | 96.5 [99.1] | 1.670 [1.592, 1.728] | slower |
| annotate / mainScriptMs | 11.5 [12.1] | 44.2 [45.7] | 3.834 [3.680, 4.066] | slower |

## `plugins-getstate` vs `plugins-toolbar`

| Row | plugins-getstate median [p95] | plugins-toolbar median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 84.6 [85.7] | 83.6 [86.4] | 0.989 [0.979, 1.008] | no detectable difference |
| open(ebook) / openedMs | 184.9 [188.8] | 183.3 [188.0] | 0.991 [0.974, 1.012] | no detectable difference |
| open(ebook) / firstPaintMs | 312.4 [324.8] | 307.3 [315.5] | 0.984 [0.949, 0.999] | no detectable difference |
| open(ebook) / settledMs | 368.0 [375.3] | 354.9 [363.7] | 0.964 [0.949, 0.980] | faster |
| open(ebook) / mainTaskMs | 258.5 [262.1] | 240.5 [245.6] | 0.930 [0.914, 0.952] | faster |
| open(ebook) / mainScriptMs | 109.4 [110.8] | 112.8 [115.6] | 1.031 [1.012, 1.052] | no detectable difference |
| open(large) / readyMs | 85.3 [86.4] | 83.9 [87.4] | 0.984 [0.962, 1.016] | no detectable difference |
| open(large) / openedMs | 213.8 [215.9] | 210.8 [217.1] | 0.986 [0.968, 1.014] | no detectable difference |
| open(large) / firstPaintMs | 348.6 [351.4] | 341.5 [352.0] | 0.980 [0.963, 1.003] | no detectable difference |
| open(large) / settledMs | 352.8 [362.9] | 340.6 [351.5] | 0.966 [0.941, 0.978] | faster |
| open(large) / mainTaskMs | 527.4 [538.3] | 525.1 [530.9] | 0.995 [0.978, 1.005] | no detectable difference |
| open(large) / mainScriptMs | 324.2 [334.0] | 396.9 [403.6] | 1.224 [1.200, 1.236] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2414.6 [2459.1] | 2345.9 [2390.8] | 0.972 [0.947, 0.986] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.7 [9.7] | 9.0 [10.0] | 1.036 [0.974, 1.131] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 859.9 [886.2] | 792.6 [828.6] | 0.922 [0.906, 0.946] | faster |
| scroll / mainScriptMs | 362.3 [377.2] | 406.3 [419.5] | 1.121 [1.101, 1.144] | slower |
| jump / settleMs | 154.5 [165.7] | 138.4 [157.1] | -16.1 [-38.2, +4.9] | no detectable difference |
| jump / mainTaskMs | 163.2 [189.0] | 143.4 [148.2] | 0.879 [0.760, 0.963] | faster |
| jump / mainScriptMs | 108.7 [127.3] | 95.8 [99.9] | 0.881 [0.754, 1.006] | no detectable difference |
| zoom / settleMs | 448.6 [463.1] | 449.2 [463.4] | +0.6 [-9.6, +14.9] | no detectable difference |
| zoom / zoom2SettleMs | 161.0 [164.1] | 157.7 [162.8] | -3.4 [-7.2, +2.0] | no detectable difference |
| zoom / zoom4SettleMs | 287.5 [301.7] | 290.5 [303.9] | +3.0 [-4.6, +12.8] | no detectable difference |
| zoom / backTo1SettleMs | 101.4 [108.5] | 106.0 [108.1] | +4.6 [-1.7, +13.6] | no detectable difference |
| zoom / mainTaskMs | 313.4 [325.1] | 284.2 [293.3] | 0.907 [0.874, 0.934] | faster |
| zoom / mainScriptMs | 127.3 [131.1] | 136.7 [140.0] | 1.074 [1.026, 1.129] | no detectable difference |
| select / settleMs | 150.6 [155.4] | 149.1 [155.0] | -1.6 [-7.3, +3.4] | no detectable difference |
| select / mainTaskMs | 77.2 [80.5] | 71.2 [72.9] | 0.923 [0.899, 0.949] | faster |
| select / mainScriptMs | 37.2 [39.0] | 42.9 [43.9] | 1.154 [1.116, 1.188] | slower |
| search / searchMs | 69.4 [74.5] | 68.9 [72.5] | 0.993 [0.885, 1.108] | no detectable difference |
| search / mainTaskMs | 16.6 [23.8] | 18.5 [22.1] | 1.112 [0.674, 1.598] | no detectable difference |
| search / mainScriptMs | 6.6 [8.6] | 8.0 [10.6] | 1.204 [0.996, 1.384] | no detectable difference |
| annotate / settleMs | 166.0 [171.6] | 165.8 [168.4] | -0.3 [-4.6, +1.6] | no detectable difference |
| annotate / mainTaskMs | 96.5 [99.1] | 89.2 [95.1] | 0.925 [0.912, 0.970] | no detectable difference |
| annotate / mainScriptMs | 44.2 [45.7] | 49.6 [52.9] | 1.122 [1.095, 1.176] | slower |

## `main` vs `plugins-toolbar`

| Row | main median [p95] | plugins-toolbar median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.6 [58.7] | 83.6 [86.4] | 1.562 [1.532, 1.612] | slower |
| open(ebook) / openedMs | 117.4 [122.1] | 183.3 [188.0] | 1.562 [1.529, 1.621] | slower |
| open(ebook) / firstPaintMs | 215.6 [220.0] | 307.3 [315.5] | 1.425 [1.410, 1.471] | slower |
| open(ebook) / settledMs | 336.8 [341.5] | 354.9 [363.7] | 1.054 [1.043, 1.076] | slower |
| open(ebook) / mainTaskMs | 117.2 [124.5] | 240.5 [245.6] | 2.053 [1.979, 2.098] | slower |
| open(ebook) / mainScriptMs | 20.4 [22.3] | 112.8 [115.6] | 5.535 [5.212, 5.763] | slower |
| open(large) / readyMs | 53.0 [54.1] | 83.9 [87.4] | 1.583 [1.544, 1.620] | slower |
| open(large) / openedMs | 134.7 [136.9] | 210.8 [217.1] | 1.565 [1.535, 1.607] | slower |
| open(large) / firstPaintMs | 224.3 [228.8] | 341.5 [352.0] | 1.522 [1.495, 1.542] | slower |
| open(large) / settledMs | 221.3 [227.9] | 340.6 [351.5] | 1.539 [1.500, 1.569] | slower |
| open(large) / mainTaskMs | 260.1 [265.5] | 525.1 [530.9] | 2.019 [1.978, 2.043] | slower |
| open(large) / mainScriptMs | 23.5 [25.4] | 396.9 [403.6] | 16.877 [16.155, 17.420] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2018.2 [2029.9] | 2345.9 [2390.8] | 1.162 [1.134, 1.174] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [9.6] | 9.0 [10.0] | 1.070 [1.009, 1.160] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 401.9 [418.8] | 792.6 [828.6] | 1.972 [1.922, 2.055] | slower |
| scroll / mainScriptMs | 54.3 [57.6] | 406.3 [419.5] | 7.484 [7.127, 7.710] | slower |
| jump / settleMs | 100.1 [103.9] | 138.4 [157.1] | +38.3 [+16.0, +56.0] | slower |
| jump / mainTaskMs | 56.3 [58.3] | 143.4 [148.2] | 2.549 [2.468, 2.730] | slower |
| jump / mainScriptMs | 14.8 [16.5] | 95.8 [99.9] | 6.450 [6.106, 6.685] | slower |
| zoom / settleMs | 405.5 [417.0] | 449.2 [463.4] | +43.7 [+35.6, +58.1] | slower |
| zoom / zoom2SettleMs | 133.1 [137.6] | 157.7 [162.8] | +24.6 [+19.9, +32.0] | slower |
| zoom / zoom4SettleMs | 272.3 [281.6] | 290.5 [303.9] | +18.2 [+11.6, +29.3] | slower |
| zoom / backTo1SettleMs | 77.5 [87.8] | 106.0 [108.1] | +28.4 [+19.4, +33.9] | slower |
| zoom / mainTaskMs | 177.9 [181.2] | 284.2 [293.3] | 1.597 [1.563, 1.655] | slower |
| zoom / mainScriptMs | 24.8 [27.1] | 136.7 [140.0] | 5.514 [5.266, 5.645] | slower |
| select / settleMs | 148.5 [150.7] | 149.1 [155.0] | +0.6 [-3.5, +5.0] | no detectable difference |
| select / mainTaskMs | 47.9 [49.3] | 71.2 [72.9] | 1.488 [1.459, 1.532] | slower |
| select / mainScriptMs | 10.4 [10.9] | 42.9 [43.9] | 4.139 [3.982, 4.306] | slower |
| search / searchMs | 59.7 [63.9] | 68.9 [72.5] | 1.154 [1.028, 1.222] | slower |
| search / mainTaskMs | 4.7 [5.4] | 18.5 [22.1] | 3.902 [2.793, 4.584] | slower |
| search / mainScriptMs | 0.1 [0.2] | 8.0 [10.6] | 57.650 [48.236, 68.153] | slower |
| annotate / settleMs | 155.1 [163.5] | 165.8 [168.4] | +10.6 [+6.2, +13.4] | slower |
| annotate / mainTaskMs | 57.8 [62.4] | 89.2 [95.1] | 1.545 [1.486, 1.628] | slower |
| annotate / mainScriptMs | 11.5 [12.1] | 49.6 [52.9] | 4.301 [4.141, 4.590] | slower |

## `plugins-toolbar` vs `plugins-bump`

Not run: `plugins-bump`.

## `main` vs `plugins-bump`

Not run: `plugins-bump`.

## `plugins-getstate` vs `plugins-item1`

Not run: `plugins-item1`.

## `plugins-getstate` vs `plugins-item3`

Not run: `plugins-item3`.

## Memory

| Row | main median [p95] | main-aa median [p95] | impl-ts median [p95] | impl-rust median [p95] | plugins median [p95] | plugins-lto median [p95] | plugins-getstate median [p95] | plugins-toolbar median [p95] |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.35 MiB [34.35 MiB] | 34.34 MiB [34.35 MiB] | 35.03 MiB [35.03 MiB] | 34.98 MiB [34.99 MiB] | 43.36 MiB [44.36 MiB] | 43.29 MiB [43.30 MiB] | 41.50 MiB [42.06 MiB] | 41.30 MiB [41.31 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.00 MiB [23.00 MiB] | 22.95 MiB [22.95 MiB] | 23.03 MiB [23.03 MiB] | 23.03 MiB [23.03 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.18 MiB] | 48.15 MiB [48.19 MiB] | 48.84 MiB [48.85 MiB] | 48.81 MiB [48.82 MiB] | 58.09 MiB [58.10 MiB] | 58.02 MiB [58.04 MiB] | 56.53 MiB [56.54 MiB] | 56.11 MiB [56.14 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.29 MiB] | 22.26 MiB [22.29 MiB] | 22.99 MiB [22.99 MiB] | 22.96 MiB [22.96 MiB] | 22.94 MiB [22.94 MiB] | 22.94 MiB [22.94 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.43 MiB] | 37.41 MiB [37.43 MiB] | 38.18 MiB [38.19 MiB] | 38.13 MiB [38.14 MiB] | 49.05 MiB [49.08 MiB] | 48.99 MiB [49.00 MiB] | 44.29 MiB [44.30 MiB] | 44.11 MiB [44.67 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.27 MiB] | 38.27 MiB [38.28 MiB] | 39.04 MiB [39.05 MiB] | 38.99 MiB [39.00 MiB] | 63.11 MiB [63.13 MiB] | 63.05 MiB [63.06 MiB] | 44.96 MiB [44.97 MiB] | 44.67 MiB [45.22 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.56 MiB [0.56 MiB] | 0.56 MiB [0.56 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] |

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

`plugins-getstate`:

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

`plugins-toolbar`:

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

