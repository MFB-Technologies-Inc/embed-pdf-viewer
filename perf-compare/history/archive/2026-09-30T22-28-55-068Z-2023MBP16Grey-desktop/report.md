# Viewer perf comparison: 2026-09-30T22-28-55-068Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | x (uncommitted changes) |
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
| plugins-getstate | research/rust-kernel-wire | 776f0c265 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 764d1ef49e62 |
| plugins-bump | research/rust-kernel-bump | dd1b86fcc | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 764d1ef49e62 |
| plugins-reads | research/rust-kernel-read-sets | 685a5472b | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 51fe8d7387b7 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-getstate`: pass
- `plugins-bump`: pass
- `plugins-reads`: pass

## Sizes

| Metric | main | main-aa | plugins-getstate | plugins-bump | plugins-reads |
| --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 11.69 MiB | 11.69 MiB | 11.72 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.88 MiB | 1.88 MiB | 1.89 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.62 MiB | 2.62 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

38 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.6 [52.9] | 52.3 [52.4] | 0.995 [0.983, 1.017] | pass |
| open(ebook) / openedMs | 115.9 [116.2] | 115.0 [116.9] | 0.992 [0.987, 1.027] | pass |
| open(ebook) / firstPaintMs | 215.3 [216.9] | 217.0 [220.1] | 1.008 [0.997, 1.030] | pass |
| open(ebook) / settledMs | 336.0 [337.7] | 337.9 [339.9] | 1.006 [0.997, 1.017] | pass |
| open(ebook) / mainTaskMs | 119.9 [120.3] | 118.5 [119.5] | 0.988 [0.978, 1.008] | pass |
| open(ebook) / mainJsMs | 93.3 [93.3] | 92.0 [93.5] | 0.986 [0.981, 1.017] | pass |
| open(large) / readyMs | 52.0 [52.1] | 52.7 [52.7] | 1.014 [1.001, 1.015] | pass |
| open(large) / openedMs | 132.8 [135.5] | 132.9 [133.3] | 1.001 [0.976, 1.016] | pass |
| open(large) / firstPaintMs | 221.9 [225.9] | 224.3 [225.3] | 1.011 [0.988, 1.018] | pass |
| open(large) / settledMs | 219.0 [222.9] | 221.4 [222.0] | 1.011 [0.987, 1.019] | pass |
| open(large) / mainTaskMs | 263.6 [264.4] | 259.5 [262.3] | 0.985 [0.977, 1.007] | pass |
| open(large) / mainJsMs | 239.4 [240.2] | 236.9 [239.0] | 0.990 [0.981, 1.007] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2015.6 [2025.1] | 2032.2 [2048.1] | 1.008 [0.998, 1.029] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [8.9] | 9.0 [9.3] | 1.070 [0.977, 1.104] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 396.9 [409.8] | 412.0 [412.3] | 1.038 [0.995, 1.053] | pass |
| scroll / mainJsMs | 306.0 [315.5] | 318.2 [318.9] | 1.040 [0.995, 1.057] | pass |
| jump / settleMs | 95.9 [97.2] | 95.1 [99.3] | -0.8 [-7.8, +6.7] | pass |
| jump / mainTaskMs | 56.2 [56.4] | 55.2 [60.1] | 0.982 [0.963, 1.104] | pass |
| jump / mainJsMs | 41.2 [41.2] | 44.3 [44.4] | 1.075 [1.008, 1.114] | A/A failed |
| zoom / settleMs | 391.8 [416.4] | 394.9 [397.4] | +3.0 [-34.6, +13.8] | pass |
| zoom / zoom2SettleMs | 132.8 [135.0] | 132.4 [132.9] | -0.3 [-10.9, +0.7] | pass |
| zoom / zoom4SettleMs | 259.1 [284.0] | 261.9 [264.9] | +2.8 [-26.7, +16.6] | pass |
| zoom / backTo1SettleMs | 83.0 [88.0] | 75.8 [86.5] | -7.2 [-13.4, +11.6] | pass |
| zoom / mainTaskMs | 182.2 [183.6] | 178.2 [188.5] | 0.978 [0.956, 1.064] | pass |
| zoom / mainJsMs | 130.5 [134.7] | 129.3 [139.0] | 0.991 [0.941, 1.078] | pass |
| select / settleMs | 147.5 [150.4] | 147.3 [149.4] | -0.2 [-3.9, +2.3] | pass |
| select / mainTaskMs | 44.2 [44.4] | 42.7 [47.7] | 0.965 [0.950, 1.163] | pass |
| select / mainJsMs | 27.0 [27.3] | 26.5 [29.3] | 0.983 [0.959, 1.176] | pass |
| search / searchMs | 49.3 [56.0] | 54.1 [60.1] | 1.097 [0.911, 1.375] | pass |
| search / mainTaskMs | 4.3 [4.9] | 5.3 [5.3] | 1.234 [0.980, 1.339] | pass |
| search / mainJsMs | 3.0 [3.1] | 3.3 [3.3] | 1.117 [0.936, 1.221] | pass |
| annotate / settleMs | 160.6 [167.5] | 161.0 [161.1] | +0.4 [-15.2, +5.4] | pass |
| annotate / mainTaskMs | 59.7 [60.2] | 57.7 [59.4] | 0.966 [0.856, 1.107] | pass |
| annotate / mainJsMs | 34.7 [35.5] | 34.6 [35.7] | 0.998 [0.856, 1.083] | pass |

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

Not run: `plugins-fix-leak`, `plugins-fix-zoom`.

## `plugins-lto` vs `plugins-fix-zoom`

Not run: `plugins-lto`, `plugins-fix-zoom`.

## `main` vs `plugins-fix-zoom`

Not run: `plugins-fix-zoom`.

## `plugins-fix-zoom` vs `plugins-wire`

Not run: `plugins-fix-zoom`, `plugins-wire`.

## `plugins-wire` vs `plugins-getstate`

Not run: `plugins-wire`.

## `plugins-lto` vs `plugins-getstate`

Not run: `plugins-lto`.

## `main` vs `plugins-getstate`

| Row | main median [p95] | plugins-getstate median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.6 [52.9] | 84.4 [84.4] | 1.605 [1.571, 1.638] | slower |
| open(ebook) / openedMs | 115.9 [116.2] | 186.1 [186.2] | 1.606 [1.580, 1.632] | slower |
| open(ebook) / firstPaintMs | 215.3 [216.9] | 318.7 [321.1] | 1.480 [1.457, 1.502] | slower |
| open(ebook) / settledMs | 336.0 [337.7] | 371.9 [377.7] | 1.107 [1.092, 1.131] | slower |
| open(ebook) / mainTaskMs | 119.9 [120.3] | 257.0 [258.5] | 2.143 [2.112, 2.178] | slower |
| open(ebook) / mainJsMs | 93.3 [93.3] | 211.8 [214.4] | 2.271 [2.242, 2.329] | slower |
| open(large) / readyMs | 52.0 [52.1] | 82.9 [84.7] | 1.594 [1.582, 1.634] | slower |
| open(large) / openedMs | 132.8 [135.5] | 211.1 [213.9] | 1.590 [1.529, 1.633] | slower |
| open(large) / firstPaintMs | 221.9 [225.9] | 343.3 [347.4] | 1.547 [1.516, 1.571] | slower |
| open(large) / settledMs | 219.0 [222.9] | 347.4 [352.1] | 1.586 [1.551, 1.619] | slower |
| open(large) / mainTaskMs | 263.6 [264.4] | 524.8 [525.4] | 1.991 [1.969, 2.014] | slower |
| open(large) / mainJsMs | 239.4 [240.2] | 483.4 [483.9] | 2.019 [1.976, 2.038] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2015.6 [2025.1] | 2330.3 [2360.5] | 1.156 [1.149, 1.187] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.9] | 9.1 [9.1] | 1.078 [1.009, 1.079] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 396.9 [409.8] | 846.2 [871.6] | 2.132 [2.051, 2.234] | slower |
| scroll / mainJsMs | 306.0 [315.5] | 748.0 [770.7] | 2.445 [2.348, 2.563] | slower |
| jump / settleMs | 95.9 [97.2] | 154.2 [157.6] | +58.3 [+7.5, +65.0] | slower |
| jump / mainTaskMs | 56.2 [56.4] | 140.0 [144.9] | 2.489 [2.438, 2.646] | slower |
| jump / mainJsMs | 41.2 [41.2] | 128.1 [131.5] | 3.111 [3.110, 3.307] | unreliable (A/A failed) |
| zoom / settleMs | 391.8 [416.4] | 443.8 [462.3] | +52.0 [+17.4, +80.4] | slower |
| zoom / zoom2SettleMs | 132.8 [135.0] | 152.7 [157.5] | +20.0 [+17.1, +25.7] | slower |
| zoom / zoom4SettleMs | 259.1 [284.0] | 291.1 [304.8] | +32.0 [-2.7, +57.6] | no detectable difference |
| zoom / backTo1SettleMs | 83.0 [88.0] | 107.0 [110.3] | +23.9 [+0.8, +34.6] | slower |
| zoom / mainTaskMs | 182.2 [183.6] | 307.8 [328.5] | 1.689 [1.634, 1.857] | slower |
| zoom / mainJsMs | 130.5 [134.7] | 261.6 [281.5] | 2.004 [1.891, 2.182] | slower |
| select / settleMs | 147.5 [150.4] | 144.5 [145.2] | -3.1 [-6.3, -2.1] | no detectable difference |
| select / mainTaskMs | 44.2 [44.4] | 72.1 [75.2] | 1.631 [1.576, 1.819] | slower |
| select / mainJsMs | 27.0 [27.3] | 58.6 [61.2] | 2.172 [2.100, 2.437] | slower |
| search / searchMs | 49.3 [56.0] | 69.8 [70.0] | 1.416 [0.939, 1.583] | no detectable difference |
| search / mainTaskMs | 4.3 [4.9] | 20.0 [20.9] | 4.687 [2.035, 5.275] | slower |
| search / mainJsMs | 3.0 [3.1] | 10.4 [10.6] | 3.501 [2.355, 3.899] | slower |
| annotate / settleMs | 160.6 [167.5] | 163.9 [168.0] | +3.3 [-4.5, +12.7] | no detectable difference |
| annotate / mainTaskMs | 59.7 [60.2] | 94.5 [95.9] | 1.584 [1.522, 1.783] | slower |
| annotate / mainJsMs | 34.7 [35.5] | 74.9 [76.1] | 2.160 [2.026, 2.307] | slower |

## `plugins-getstate` vs `plugins-toolbar`

Not run: `plugins-toolbar`.

## `main` vs `plugins-toolbar`

Not run: `plugins-toolbar`.

## `plugins-toolbar` vs `plugins-bump`

Not run: `plugins-toolbar`.

## `main` vs `plugins-bump`

| Row | main median [p95] | plugins-bump median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.6 [52.9] | 83.8 [84.5] | 1.594 [1.563, 1.641] | slower |
| open(ebook) / openedMs | 115.9 [116.2] | 183.9 [185.2] | 1.588 [1.582, 1.625] | slower |
| open(ebook) / firstPaintMs | 215.3 [216.9] | 311.1 [312.1] | 1.445 [1.408, 1.459] | slower |
| open(ebook) / settledMs | 336.0 [337.7] | 359.9 [360.6] | 1.071 [1.054, 1.078] | slower |
| open(ebook) / mainTaskMs | 119.9 [120.3] | 240.5 [243.3] | 2.006 [1.985, 2.052] | slower |
| open(ebook) / mainJsMs | 93.3 [93.3] | 197.2 [198.4] | 2.114 [2.089, 2.155] | slower |
| open(large) / readyMs | 52.0 [52.1] | 83.3 [83.8] | 1.603 [1.577, 1.614] | slower |
| open(large) / openedMs | 132.8 [135.5] | 209.5 [210.5] | 1.578 [1.541, 1.605] | slower |
| open(large) / firstPaintMs | 221.9 [225.9] | 334.4 [337.8] | 1.507 [1.476, 1.527] | slower |
| open(large) / settledMs | 219.0 [222.9] | 336.8 [340.3] | 1.538 [1.498, 1.564] | slower |
| open(large) / mainTaskMs | 263.6 [264.4] | 521.8 [522.9] | 1.979 [1.960, 2.005] | slower |
| open(large) / mainJsMs | 239.4 [240.2] | 480.1 [481.3] | 2.005 [1.979, 2.027] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2015.6 [2025.1] | 2236.6 [2243.1] | 1.110 [1.083, 1.126] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.9] | 9.5 [9.7] | 1.128 [1.019, 1.148] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 396.9 [409.8] | 742.5 [748.9] | 1.871 [1.708, 1.915] | slower |
| scroll / mainJsMs | 306.0 [315.5] | 636.2 [644.2] | 2.079 [1.902, 2.138] | slower |
| jump / settleMs | 95.9 [97.2] | 132.8 [142.4] | +36.9 [+19.5, +50.5] | slower |
| jump / mainTaskMs | 56.2 [56.4] | 145.8 [150.0] | 2.592 [2.532, 2.737] | slower |
| jump / mainJsMs | 41.2 [41.2] | 131.0 [131.5] | 3.181 [3.056, 3.301] | unreliable (A/A failed) |
| zoom / settleMs | 391.8 [416.4] | 434.7 [435.7] | +42.9 [+9.3, +51.9] | slower |
| zoom / zoom2SettleMs | 132.8 [135.0] | 150.2 [150.3] | +17.4 [+14.7, +18.0] | slower |
| zoom / zoom4SettleMs | 259.1 [284.0] | 284.4 [285.7] | +25.3 [-8.5, +37.2] | no detectable difference |
| zoom / backTo1SettleMs | 83.0 [88.0] | 89.7 [101.3] | +6.6 [-1.5, +26.5] | no detectable difference |
| zoom / mainTaskMs | 182.2 [183.6] | 274.4 [277.9] | 1.506 [1.490, 1.562] | slower |
| zoom / mainJsMs | 130.5 [134.7] | 227.6 [229.8] | 1.744 [1.679, 1.769] | slower |
| select / settleMs | 147.5 [150.4] | 144.9 [149.1] | -2.6 [-10.7, +2.2] | no detectable difference |
| select / mainTaskMs | 44.2 [44.4] | 63.9 [65.7] | 1.445 [1.353, 1.587] | slower |
| select / mainJsMs | 27.0 [27.3] | 50.6 [51.7] | 1.877 [1.738, 2.056] | slower |
| search / searchMs | 49.3 [56.0] | 68.3 [68.5] | 1.386 [0.875, 1.549] | no detectable difference |
| search / mainTaskMs | 4.3 [4.9] | 14.4 [19.5] | 3.372 [2.175, 5.042] | slower |
| search / mainJsMs | 3.0 [3.1] | 7.9 [8.9] | 2.670 [2.170, 3.293] | slower |
| annotate / settleMs | 160.6 [167.5] | 164.9 [166.7] | +4.3 [-5.2, +11.2] | no detectable difference |
| annotate / mainTaskMs | 59.7 [60.2] | 83.6 [86.9] | 1.401 [1.359, 1.620] | slower |
| annotate / mainJsMs | 34.7 [35.5] | 63.4 [65.3] | 1.827 [1.729, 1.981] | slower |

## `plugins-getstate` vs `plugins-item1`

Not run: `plugins-item1`.

## `plugins-getstate` vs `plugins-item3`

Not run: `plugins-item3`.

## `plugins-getstate` vs `plugins-bump`

| Row | plugins-getstate median [p95] | plugins-bump median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 84.4 [84.4] | 83.8 [84.5] | 0.993 [0.980, 1.018] | no detectable difference |
| open(ebook) / openedMs | 186.1 [186.2] | 183.9 [185.2] | 0.989 [0.988, 1.009] | no detectable difference |
| open(ebook) / firstPaintMs | 318.7 [321.1] | 311.1 [312.1] | 0.976 [0.951, 0.988] | no detectable difference |
| open(ebook) / settledMs | 371.9 [377.7] | 359.9 [360.6] | 0.968 [0.942, 0.977] | faster |
| open(ebook) / mainTaskMs | 257.0 [258.5] | 240.5 [243.3] | 0.936 [0.924, 0.958] | faster |
| open(ebook) / mainJsMs | 211.8 [214.4] | 197.2 [198.4] | 0.931 [0.909, 0.949] | faster |
| open(large) / readyMs | 82.9 [84.7] | 83.3 [83.8] | 1.005 [0.968, 1.017] | no detectable difference |
| open(large) / openedMs | 211.1 [213.9] | 209.5 [210.5] | 0.993 [0.976, 1.015] | no detectable difference |
| open(large) / firstPaintMs | 343.3 [347.4] | 334.4 [337.8] | 0.974 [0.960, 0.986] | no detectable difference |
| open(large) / settledMs | 347.4 [352.1] | 336.8 [340.3] | 0.969 [0.949, 0.984] | faster |
| open(large) / mainTaskMs | 524.8 [525.4] | 521.8 [522.9] | 0.994 [0.986, 1.005] | no detectable difference |
| open(large) / mainJsMs | 483.4 [483.9] | 480.1 [481.3] | 0.993 [0.983, 1.014] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2330.3 [2360.5] | 2236.6 [2243.1] | 0.960 [0.929, 0.963] | faster |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.1 [9.1] | 9.5 [9.7] | 1.046 [0.999, 1.076] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 846.2 [871.6] | 742.5 [748.9] | 0.877 [0.803, 0.889] | faster |
| scroll / mainJsMs | 748.0 [770.7] | 636.2 [644.2] | 0.851 [0.779, 0.868] | faster |
| jump / settleMs | 154.2 [157.6] | 132.8 [142.4] | -21.4 [-41.3, +38.8] | no detectable difference |
| jump / mainTaskMs | 140.0 [144.9] | 145.8 [150.0] | 1.042 [0.983, 1.093] | no detectable difference |
| jump / mainJsMs | 128.1 [131.5] | 131.0 [131.5] | 1.022 [0.954, 1.028] | unreliable (A/A failed) |
| zoom / settleMs | 443.8 [462.3] | 434.7 [435.7] | -9.1 [-35.9, -0.7] | no detectable difference |
| zoom / zoom2SettleMs | 152.7 [157.5] | 150.2 [150.3] | -2.5 [-8.1, -2.1] | no detectable difference |
| zoom / zoom4SettleMs | 291.1 [304.8] | 284.4 [285.7] | -6.7 [-28.1, +1.8] | no detectable difference |
| zoom / backTo1SettleMs | 107.0 [110.3] | 89.7 [101.3] | -17.3 [-23.6, +13.2] | no detectable difference |
| zoom / mainTaskMs | 307.8 [328.5] | 274.4 [277.9] | 0.891 [0.828, 0.927] | faster |
| zoom / mainJsMs | 261.6 [281.5] | 227.6 [229.8] | 0.870 [0.800, 0.900] | faster |
| select / settleMs | 144.5 [145.2] | 144.9 [149.1] | +0.5 [-5.3, +5.2] | no detectable difference |
| select / mainTaskMs | 72.1 [75.2] | 63.9 [65.7] | 0.886 [0.796, 0.941] | no detectable difference |
| select / mainJsMs | 58.6 [61.2] | 50.6 [51.7] | 0.864 [0.772, 0.905] | no detectable difference |
| search / searchMs | 69.8 [70.0] | 68.3 [68.5] | 0.979 [0.709, 1.285] | no detectable difference |
| search / mainTaskMs | 20.0 [20.9] | 14.4 [19.5] | 0.719 [0.520, 1.962] | no detectable difference |
| search / mainJsMs | 10.4 [10.6] | 7.9 [8.9] | 0.763 [0.644, 1.208] | no detectable difference |
| annotate / settleMs | 163.9 [168.0] | 164.9 [166.7] | +1.0 [-5.3, +3.1] | no detectable difference |
| annotate / mainTaskMs | 94.5 [95.9] | 83.6 [86.9] | 0.884 [0.852, 0.953] | no detectable difference |
| annotate / mainJsMs | 74.9 [76.1] | 63.4 [65.3] | 0.846 [0.806, 0.908] | faster |

## `plugins-item1` vs `plugins-bump`

Not run: `plugins-item1`.

## `plugins-bump` vs `plugins-reads`

| Row | plugins-bump median [p95] | plugins-reads median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 83.8 [84.5] | 84.7 [86.5] | 1.011 [0.995, 1.048] | no detectable difference |
| open(ebook) / openedMs | 183.9 [185.2] | 186.9 [189.2] | 1.016 [0.997, 1.030] | no detectable difference |
| open(ebook) / firstPaintMs | 311.1 [312.1] | 312.0 [314.0] | 1.003 [0.984, 1.028] | no detectable difference |
| open(ebook) / settledMs | 359.9 [360.6] | 362.1 [363.7] | 1.006 [0.989, 1.021] | no detectable difference |
| open(ebook) / mainTaskMs | 240.5 [243.3] | 241.6 [244.6] | 1.005 [0.987, 1.025] | no detectable difference |
| open(ebook) / mainJsMs | 197.2 [198.4] | 196.3 [200.4] | 0.995 [0.986, 1.030] | no detectable difference |
| open(large) / readyMs | 83.3 [83.8] | 85.1 [85.1] | 1.022 [1.013, 1.037] | no detectable difference |
| open(large) / openedMs | 209.5 [210.5] | 211.1 [214.4] | 1.007 [0.994, 1.027] | no detectable difference |
| open(large) / firstPaintMs | 334.4 [337.8] | 339.6 [341.7] | 1.016 [1.002, 1.024] | no detectable difference |
| open(large) / settledMs | 336.8 [340.3] | 342.6 [344.0] | 1.017 [0.998, 1.029] | no detectable difference |
| open(large) / mainTaskMs | 521.8 [522.9] | 522.1 [525.6] | 1.001 [0.995, 1.015] | no detectable difference |
| open(large) / mainJsMs | 480.1 [481.3] | 477.9 [480.2] | 0.995 [0.989, 1.010] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2236.6 [2243.1] | 2303.4 [2304.7] | 1.030 [1.004, 1.050] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.5 [9.7] | 9.5 [9.6] | 0.995 [0.942, 1.055] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 742.5 [748.9] | 757.2 [793.7] | 1.020 [1.003, 1.136] | no detectable difference |
| scroll / mainJsMs | 636.2 [644.2] | 654.4 [690.6] | 1.029 [1.005, 1.154] | no detectable difference |
| jump / settleMs | 132.8 [142.4] | 126.2 [155.4] | -6.5 [-21.7, +41.8] | no detectable difference |
| jump / mainTaskMs | 145.8 [150.0] | 157.3 [160.3] | 1.079 [1.006, 1.123] | no detectable difference |
| jump / mainJsMs | 131.0 [131.5] | 140.1 [142.8] | 1.070 [1.061, 1.138] | unreliable (A/A failed) |
| zoom / settleMs | 434.7 [435.7] | 439.7 [445.8] | +5.0 [-2.6, +18.0] | no detectable difference |
| zoom / zoom2SettleMs | 150.2 [150.3] | 150.9 [157.9] | +0.7 [+0.1, +8.8] | no detectable difference |
| zoom / zoom4SettleMs | 284.4 [285.7] | 288.8 [295.4] | +4.5 [-11.4, +17.9] | no detectable difference |
| zoom / backTo1SettleMs | 89.7 [101.3] | 103.4 [105.5] | +13.7 [-10.9, +18.7] | no detectable difference |
| zoom / mainTaskMs | 274.4 [277.9] | 287.3 [291.5] | 1.047 [1.017, 1.067] | no detectable difference |
| zoom / mainJsMs | 227.6 [229.8] | 243.3 [248.6] | 1.069 [1.021, 1.098] | no detectable difference |
| select / settleMs | 144.9 [149.1] | 143.8 [144.6] | -1.1 [-8.9, +4.6] | no detectable difference |
| select / mainTaskMs | 63.9 [65.7] | 56.4 [60.5] | 0.882 [0.846, 1.013] | no detectable difference |
| select / mainJsMs | 50.6 [51.7] | 43.2 [47.2] | 0.854 [0.830, 1.005] | no detectable difference |
| search / searchMs | 68.3 [68.5] | 51.8 [64.5] | 0.758 [0.734, 1.326] | no detectable difference |
| search / mainTaskMs | 14.4 [19.5] | 11.9 [13.1] | 0.828 [0.450, 1.212] | no detectable difference |
| search / mainJsMs | 7.9 [8.9] | 6.8 [7.2] | 0.863 [0.595, 1.062] | no detectable difference |
| annotate / settleMs | 164.9 [166.7] | 162.0 [164.1] | -2.9 [-14.4, +1.2] | no detectable difference |
| annotate / mainTaskMs | 83.6 [86.9] | 74.5 [79.2] | 0.891 [0.659, 0.974] | no detectable difference |
| annotate / mainJsMs | 63.4 [65.3] | 56.3 [59.9] | 0.887 [0.630, 0.980] | no detectable difference |

## `main` vs `plugins-reads`

| Row | main median [p95] | plugins-reads median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.6 [52.9] | 84.7 [86.5] | 1.612 [1.591, 1.682] | slower |
| open(ebook) / openedMs | 115.9 [116.2] | 186.9 [189.2] | 1.613 [1.590, 1.661] | slower |
| open(ebook) / firstPaintMs | 215.3 [216.9] | 312.0 [314.0] | 1.449 [1.415, 1.468] | slower |
| open(ebook) / settledMs | 336.0 [337.7] | 362.1 [363.7] | 1.078 [1.056, 1.088] | slower |
| open(ebook) / mainTaskMs | 119.9 [120.3] | 241.6 [244.6] | 2.015 [1.998, 2.063] | slower |
| open(ebook) / mainJsMs | 93.3 [93.3] | 196.3 [200.4] | 2.104 [2.097, 2.180] | slower |
| open(large) / readyMs | 52.0 [52.1] | 85.1 [85.1] | 1.637 [1.630, 1.639] | slower |
| open(large) / openedMs | 132.8 [135.5] | 211.1 [214.4] | 1.590 [1.542, 1.637] | slower |
| open(large) / firstPaintMs | 221.9 [225.9] | 339.6 [341.7] | 1.531 [1.497, 1.544] | slower |
| open(large) / settledMs | 219.0 [222.9] | 342.6 [344.0] | 1.564 [1.522, 1.580] | slower |
| open(large) / mainTaskMs | 263.6 [264.4] | 522.1 [525.6] | 1.980 [1.967, 2.016] | slower |
| open(large) / mainJsMs | 239.4 [240.2] | 477.9 [480.2] | 1.996 [1.982, 2.023] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2015.6 [2025.1] | 2303.4 [2304.7] | 1.143 [1.112, 1.157] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.9] | 9.5 [9.6] | 1.122 [1.022, 1.137] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 396.9 [409.8] | 757.2 [793.7] | 1.908 [1.828, 2.038] | slower |
| scroll / mainJsMs | 306.0 [315.5] | 654.4 [690.6] | 2.139 [2.048, 2.302] | slower |
| jump / settleMs | 95.9 [97.2] | 126.2 [155.4] | +30.3 [+24.5, +65.6] | slower |
| jump / mainTaskMs | 56.2 [56.4] | 157.3 [160.3] | 2.798 [2.680, 2.921] | slower |
| jump / mainJsMs | 41.2 [41.2] | 140.1 [142.8] | 3.402 [3.391, 3.591] | unreliable (A/A failed) |
| zoom / settleMs | 391.8 [416.4] | 439.7 [445.8] | +47.9 [+14.1, +62.6] | slower |
| zoom / zoom2SettleMs | 132.8 [135.0] | 150.9 [157.9] | +18.1 [+15.1, +26.4] | slower |
| zoom / zoom4SettleMs | 259.1 [284.0] | 288.8 [295.4] | +29.8 [-12.3, +47.4] | no detectable difference |
| zoom / backTo1SettleMs | 83.0 [88.0] | 103.4 [105.5] | +20.3 [+3.1, +29.7] | slower |
| zoom / mainTaskMs | 182.2 [183.6] | 287.3 [291.5] | 1.577 [1.540, 1.639] | slower |
| zoom / mainJsMs | 130.5 [134.7] | 243.3 [248.6] | 1.864 [1.738, 1.917] | slower |
| select / settleMs | 147.5 [150.4] | 143.8 [144.6] | -3.7 [-10.0, -2.7] | no detectable difference |
| select / mainTaskMs | 44.2 [44.4] | 56.4 [60.5] | 1.275 [1.255, 1.467] | slower |
| select / mainJsMs | 27.0 [27.3] | 43.2 [47.2] | 1.602 [1.576, 1.889] | slower |
| search / searchMs | 49.3 [56.0] | 51.8 [64.5] | 1.050 [0.886, 1.490] | no detectable difference |
| search / mainTaskMs | 4.3 [4.9] | 11.9 [13.1] | 2.793 [1.796, 3.328] | slower |
| search / mainJsMs | 3.0 [3.1] | 6.8 [7.2] | 2.305 [1.692, 2.668] | slower |
| annotate / settleMs | 160.6 [167.5] | 162.0 [164.1] | +1.4 [-15.7, +8.6] | no detectable difference |
| annotate / mainTaskMs | 59.7 [60.2] | 74.5 [79.2] | 1.248 [0.956, 1.479] | no detectable difference |
| annotate / mainJsMs | 34.7 [35.5] | 56.3 [59.9] | 1.621 [1.160, 1.823] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-getstate median [p95] | plugins-bump median [p95] | plugins-reads median [p95] |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.35 MiB [34.35 MiB] | 41.51 MiB [41.51 MiB] | 41.31 MiB [41.32 MiB] | 41.38 MiB [41.39 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.16 MiB [48.16 MiB] | 48.15 MiB [48.15 MiB] | 56.52 MiB [56.53 MiB] | 56.13 MiB [56.13 MiB] | 56.17 MiB [56.18 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.42 MiB] | 37.42 MiB [37.43 MiB] | 44.28 MiB [44.29 MiB] | 44.13 MiB [44.14 MiB] | 44.18 MiB [44.19 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.28 MiB] | 38.28 MiB [38.28 MiB] | 44.95 MiB [44.97 MiB] | 44.67 MiB [44.67 MiB] | 44.74 MiB [44.74 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

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

`plugins-bump`:

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

`plugins-reads`:

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

