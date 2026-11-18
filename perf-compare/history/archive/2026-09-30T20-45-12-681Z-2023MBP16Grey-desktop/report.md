# Viewer perf comparison: 2026-09-30T20-45-12-681Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 993b28ff513e1dbad9e22abd021c6bd6d75617f7 (uncommitted changes) |
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
| plugins-item1 | research/rust-kernel-toolbar-gate | 1efa42de6 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 764d1ef49e62 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-getstate`: pass
- `plugins-bump`: pass
- `plugins-item1`: pass

## Sizes

| Metric | main | main-aa | plugins-getstate | plugins-bump | plugins-item1 |
| --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 11.69 MiB | 11.69 MiB | 11.69 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.88 MiB | 1.88 MiB | 1.88 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.62 MiB | 2.62 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

39 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 54.0 [54.4] | 53.0 [53.6] | 0.982 [0.970, 1.030] | pass |
| open(ebook) / openedMs | 115.9 [117.7] | 114.8 [115.4] | 0.990 [0.973, 1.007] | pass |
| open(ebook) / firstPaintMs | 214.9 [215.2] | 213.6 [217.3] | 0.994 [0.968, 1.037] | pass |
| open(ebook) / settledMs | 335.4 [336.0] | 333.4 [337.6] | 0.994 [0.985, 1.012] | pass |
| open(ebook) / mainTaskMs | 115.1 [117.1] | 117.9 [118.4] | 1.024 [0.960, 1.031] | pass |
| open(ebook) / mainScriptMs | 19.7 [20.7] | 21.0 [21.1] | 1.064 [0.902, 1.085] | pass |
| open(large) / readyMs | 51.8 [52.4] | 52.7 [52.7] | 1.018 [0.980, 1.028] | pass |
| open(large) / openedMs | 133.4 [134.2] | 132.9 [133.6] | 0.996 [0.987, 1.010] | pass |
| open(large) / firstPaintMs | 220.2 [220.9] | 221.0 [222.0] | 1.004 [0.917, 1.012] | pass |
| open(large) / settledMs | 217.3 [217.8] | 217.9 [218.6] | 1.002 [0.988, 1.013] | pass |
| open(large) / mainTaskMs | 259.7 [260.4] | 259.8 [260.1] | 1.000 [0.994, 1.005] | pass |
| open(large) / mainScriptMs | 23.9 [24.3] | 23.0 [25.4] | 0.959 [0.938, 1.102] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2018.5 [2021.5] | 2010.0 [2030.8] | 0.996 [0.990, 1.013] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [8.4] | 9.3 [9.4] | 1.106 [0.990, 1.132] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 424.2 [425.3] | 415.4 [428.3] | 0.979 [0.931, 1.080] | pass |
| scroll / mainScriptMs | 56.9 [58.0] | 56.1 [59.3] | 0.986 [0.939, 1.120] | pass |
| jump / settleMs | 94.9 [100.3] | 96.5 [99.8] | +1.6 [-7.7, +5.5] | pass |
| jump / mainTaskMs | 55.0 [59.1] | 58.4 [59.1] | 1.063 [0.954, 1.098] | pass |
| jump / mainScriptMs | 14.3 [17.7] | 14.4 [15.5] | 1.005 [0.793, 1.103] | pass |
| zoom / settleMs | 338.9 [359.2] | 346.7 [364.7] | +7.8 [-28.4, +33.2] | pass |
| zoom / zoom2SettleMs | 131.8 [135.1] | 127.6 [134.6] | -4.2 [-9.5, +3.6] | pass |
| zoom / zoom4SettleMs | 207.1 [224.1] | 219.1 [230.1] | +12.0 [-19.0, +29.6] | pass |
| zoom / backTo1SettleMs | 65.9 [66.5] | 71.1 [79.4] | +5.2 [-3.8, +18.7] | pass |
| zoom / mainTaskMs | 167.3 [168.1] | 162.5 [170.9] | 0.971 [0.927, 1.062] | pass |
| zoom / mainScriptMs | 23.9 [26.4] | 22.9 [27.8] | 0.959 [0.834, 1.206] | pass |
| select / settleMs | 144.4 [149.5] | 148.5 [149.0] | +4.1 [-2.3, +6.8] | pass |
| select / mainTaskMs | 47.3 [48.7] | 47.5 [47.7] | 1.003 [0.936, 1.074] | pass |
| select / mainScriptMs | 10.2 [10.7] | 10.5 [11.2] | 1.021 [0.911, 1.140] | pass |
| search / searchMs | 60.6 [60.7] | 58.6 [60.2] | 0.967 [0.961, 1.006] | pass |
| search / mainTaskMs | 4.8 [5.0] | 4.9 [5.0] | 1.025 [0.878, 1.124] | pass |
| search / mainScriptMs | 0.2 [0.2] | 0.2 [0.2] | 1.030 [0.862, 1.504] | pass |
| annotate / settleMs | 156.1 [161.4] | 153.6 [167.4] | -2.4 [-8.6, +16.4] | pass |
| annotate / mainTaskMs | 60.0 [61.0] | 54.7 [56.2] | 0.913 [0.891, 1.016] | pass |
| annotate / mainScriptMs | 11.8 [11.8] | 11.3 [11.6] | 0.954 [0.925, 1.054] | pass |

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
| open(ebook) / readyMs | 54.0 [54.4] | 83.6 [83.7] | 1.550 [1.534, 1.605] | slower |
| open(ebook) / openedMs | 115.9 [117.7] | 182.2 [183.6] | 1.572 [1.541, 1.602] | slower |
| open(ebook) / firstPaintMs | 214.9 [215.2] | 307.6 [311.1] | 1.431 [1.419, 1.484] | slower |
| open(ebook) / settledMs | 335.4 [336.0] | 364.4 [367.1] | 1.086 [1.083, 1.100] | slower |
| open(ebook) / mainTaskMs | 115.1 [117.1] | 255.2 [256.5] | 2.218 [2.135, 2.233] | slower |
| open(ebook) / mainScriptMs | 19.7 [20.7] | 109.1 [109.7] | 5.528 [5.102, 5.627] | slower |
| open(large) / readyMs | 51.8 [52.4] | 83.3 [83.5] | 1.608 [1.581, 1.629] | slower |
| open(large) / openedMs | 133.4 [134.2] | 208.9 [209.5] | 1.566 [1.554, 1.583] | slower |
| open(large) / firstPaintMs | 220.2 [220.9] | 340.9 [343.6] | 1.548 [1.514, 1.567] | slower |
| open(large) / settledMs | 217.3 [217.8] | 347.2 [349.1] | 1.598 [1.575, 1.618] | slower |
| open(large) / mainTaskMs | 259.7 [260.4] | 522.4 [523.9] | 2.012 [1.990, 2.026] | slower |
| open(large) / mainScriptMs | 23.9 [24.3] | 321.5 [322.7] | 13.429 [13.210, 13.875] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2018.5 [2021.5] | 2383.6 [2410.7] | 1.181 [1.115, 1.202] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.4] | 9.7 [10.0] | 1.149 [1.057, 1.208] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 424.2 [425.3] | 872.0 [872.6] | 2.056 [1.995, 2.193] | slower |
| scroll / mainScriptMs | 56.9 [58.0] | 371.2 [371.6] | 6.522 [6.241, 6.978] | slower |
| jump / settleMs | 94.9 [100.3] | 182.8 [183.6] | +87.9 [+6.7, +89.0] | slower |
| jump / mainTaskMs | 55.0 [59.1] | 189.1 [190.6] | 3.438 [2.425, 3.540] | slower |
| jump / mainScriptMs | 14.3 [17.7] | 127.0 [127.5] | 8.851 [5.034, 9.002] | slower |
| zoom / settleMs | 338.9 [359.2] | 417.8 [419.2] | +79.0 [+48.0, +85.9] | slower |
| zoom / zoom2SettleMs | 131.8 [135.1] | 162.8 [164.3] | +31.0 [+19.8, +32.7] | slower |
| zoom / zoom4SettleMs | 207.1 [224.1] | 254.9 [255.0] | +47.8 [+28.3, +53.4] | slower |
| zoom / backTo1SettleMs | 65.9 [66.5] | 99.8 [100.6] | +33.9 [+19.9, +39.1] | slower |
| zoom / mainTaskMs | 167.3 [168.1] | 313.7 [317.6] | 1.875 [1.763, 1.967] | slower |
| zoom / mainScriptMs | 23.9 [26.4] | 125.3 [126.4] | 5.253 [4.526, 5.390] | slower |
| select / settleMs | 144.4 [149.5] | 149.0 [149.3] | +4.5 [-5.0, +7.1] | no detectable difference |
| select / mainTaskMs | 47.3 [48.7] | 79.1 [79.3] | 1.671 [1.593, 1.785] | slower |
| select / mainScriptMs | 10.2 [10.7] | 37.7 [38.9] | 3.683 [3.490, 3.947] | slower |
| search / searchMs | 60.6 [60.7] | 75.0 [77.1] | 1.237 [1.203, 1.290] | slower |
| search / mainTaskMs | 4.8 [5.0] | 22.4 [23.6] | 4.641 [4.471, 5.368] | slower |
| search / mainScriptMs | 0.2 [0.2] | 7.5 [7.6] | 44.556 [43.023, 57.969] | slower |
| annotate / settleMs | 156.1 [161.4] | 166.6 [167.1] | +10.6 [+4.4, +14.6] | no detectable difference |
| annotate / mainTaskMs | 60.0 [61.0] | 96.6 [96.8] | 1.611 [1.475, 1.745] | slower |
| annotate / mainScriptMs | 11.8 [11.8] | 44.5 [44.9] | 3.759 [3.515, 4.086] | slower |

## `plugins-getstate` vs `plugins-toolbar`

Not run: `plugins-toolbar`.

## `main` vs `plugins-toolbar`

Not run: `plugins-toolbar`.

## `plugins-toolbar` vs `plugins-bump`

Not run: `plugins-toolbar`.

## `main` vs `plugins-bump`

| Row | main median [p95] | plugins-bump median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 54.0 [54.4] | 82.5 [84.3] | 1.529 [1.511, 1.619] | slower |
| open(ebook) / openedMs | 115.9 [117.7] | 182.0 [185.1] | 1.571 [1.529, 1.617] | slower |
| open(ebook) / firstPaintMs | 214.9 [215.2] | 309.2 [310.6] | 1.439 [1.407, 1.480] | slower |
| open(ebook) / settledMs | 335.4 [336.0] | 358.1 [358.7] | 1.068 [1.061, 1.074] | slower |
| open(ebook) / mainTaskMs | 115.1 [117.1] | 238.9 [240.1] | 2.076 [2.016, 2.090] | slower |
| open(ebook) / mainScriptMs | 19.7 [20.7] | 113.0 [113.8] | 5.725 [5.357, 5.835] | slower |
| open(large) / readyMs | 51.8 [52.4] | 82.9 [86.0] | 1.601 [1.577, 1.683] | slower |
| open(large) / openedMs | 133.4 [134.2] | 210.1 [212.6] | 1.575 [1.558, 1.608] | slower |
| open(large) / firstPaintMs | 220.2 [220.9] | 337.5 [338.0] | 1.533 [1.519, 1.541] | slower |
| open(large) / settledMs | 217.3 [217.8] | 338.5 [341.0] | 1.557 [1.542, 1.581] | slower |
| open(large) / mainTaskMs | 259.7 [260.4] | 519.9 [521.5] | 2.002 [1.991, 2.016] | slower |
| open(large) / mainScriptMs | 23.9 [24.3] | 394.7 [398.4] | 16.487 [16.094, 17.143] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2018.5 [2021.5] | 2206.4 [2217.3] | 1.093 [1.044, 1.105] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.4] | 9.2 [9.9] | 1.094 [1.013, 1.189] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 424.2 [425.3] | 728.6 [734.5] | 1.718 [1.670, 1.847] | slower |
| scroll / mainScriptMs | 56.9 [58.0] | 386.9 [392.4] | 6.798 [6.500, 7.379] | slower |
| jump / settleMs | 94.9 [100.3] | 148.2 [150.9] | +53.3 [+12.5, +56.6] | slower |
| jump / mainTaskMs | 55.0 [59.1] | 143.3 [146.3] | 2.605 [2.334, 2.720] | slower |
| jump / mainScriptMs | 14.3 [17.7] | 95.0 [97.2] | 6.626 [5.223, 6.872] | slower |
| zoom / settleMs | 338.9 [359.2] | 392.9 [397.4] | +54.0 [+23.3, +64.4] | slower |
| zoom / zoom2SettleMs | 131.8 [135.1] | 143.5 [151.3] | +11.7 [+7.1, +20.3] | slower |
| zoom / zoom4SettleMs | 207.1 [224.1] | 245.8 [249.9] | +38.7 [+15.3, +48.7] | slower |
| zoom / backTo1SettleMs | 65.9 [66.5] | 85.3 [95.5] | +19.3 [+16.2, +35.0] | slower |
| zoom / mainTaskMs | 167.3 [168.1] | 263.7 [270.5] | 1.576 [1.514, 1.677] | slower |
| zoom / mainScriptMs | 23.9 [26.4] | 127.6 [133.4] | 5.350 [4.701, 5.706] | slower |
| select / settleMs | 144.4 [149.5] | 145.4 [146.8] | +1.0 [-6.3, +4.7] | no detectable difference |
| select / mainTaskMs | 47.3 [48.7] | 65.6 [67.5] | 1.385 [1.296, 1.524] | slower |
| select / mainScriptMs | 10.2 [10.7] | 42.6 [43.5] | 4.162 [3.750, 4.402] | slower |
| search / searchMs | 60.6 [60.7] | 72.5 [74.0] | 1.196 [1.104, 1.236] | slower |
| search / mainTaskMs | 4.8 [5.0] | 18.9 [22.0] | 3.908 [2.616, 5.039] | slower |
| search / mainScriptMs | 0.2 [0.2] | 8.6 [8.9] | 50.982 [39.414, 67.992] | slower |
| annotate / settleMs | 156.1 [161.4] | 163.7 [169.7] | +7.6 [+0.4, +17.8] | no detectable difference |
| annotate / mainTaskMs | 60.0 [61.0] | 85.1 [85.2] | 1.419 [1.325, 1.537] | slower |
| annotate / mainScriptMs | 11.8 [11.8] | 49.5 [50.2] | 4.187 [3.978, 4.574] | slower |

## `plugins-getstate` vs `plugins-item1`

| Row | plugins-getstate median [p95] | plugins-item1 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 83.6 [83.7] | 83.3 [84.4] | 0.996 [0.983, 1.012] | no detectable difference |
| open(ebook) / openedMs | 182.2 [183.6] | 183.3 [183.7] | 1.006 [0.989, 1.011] | no detectable difference |
| open(ebook) / firstPaintMs | 307.6 [311.1] | 308.6 [310.7] | 1.003 [0.990, 1.018] | no detectable difference |
| open(ebook) / settledMs | 364.4 [367.1] | 355.4 [358.4] | 0.975 [0.967, 0.985] | no detectable difference |
| open(ebook) / mainTaskMs | 255.2 [256.5] | 240.4 [240.7] | 0.942 [0.934, 0.961] | faster |
| open(ebook) / mainScriptMs | 109.1 [109.7] | 113.3 [114.1] | 1.038 [1.026, 1.073] | no detectable difference |
| open(large) / readyMs | 83.3 [83.5] | 83.1 [83.3] | 0.998 [0.983, 1.003] | no detectable difference |
| open(large) / openedMs | 208.9 [209.5] | 210.4 [212.7] | 1.007 [0.993, 1.020] | no detectable difference |
| open(large) / firstPaintMs | 340.9 [343.6] | 337.6 [339.8] | 0.990 [0.964, 1.017] | no detectable difference |
| open(large) / settledMs | 347.2 [349.1] | 339.9 [341.8] | 0.979 [0.958, 0.997] | no detectable difference |
| open(large) / mainTaskMs | 522.4 [523.9] | 522.8 [523.3] | 1.001 [0.989, 1.010] | no detectable difference |
| open(large) / mainScriptMs | 321.5 [322.7] | 396.0 [397.0] | 1.232 [1.217, 1.237] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2383.6 [2410.7] | 2312.3 [2351.8] | 0.970 [0.900, 1.045] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.7 [10.0] | 8.7 [9.5] | 0.898 [0.852, 1.069] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 872.0 [872.6] | 796.1 [802.7] | 0.913 [0.899, 0.947] | faster |
| scroll / mainScriptMs | 371.2 [371.6] | 411.0 [411.6] | 1.107 [1.071, 1.136] | no detectable difference |
| jump / settleMs | 182.8 [183.6] | 158.8 [160.9] | -24.0 [-75.3, +53.6] | no detectable difference |
| jump / mainTaskMs | 189.1 [190.6] | 144.2 [146.3] | 0.763 [0.728, 1.016] | no detectable difference |
| jump / mainScriptMs | 127.0 [127.5] | 95.8 [97.1] | 0.754 [0.709, 1.068] | no detectable difference |
| zoom / settleMs | 417.8 [419.2] | 400.9 [414.8] | -16.9 [-22.8, +6.9] | no detectable difference |
| zoom / zoom2SettleMs | 162.8 [164.3] | 154.6 [163.4] | -8.2 [-16.1, +9.1] | no detectable difference |
| zoom / zoom4SettleMs | 254.9 [255.0] | 252.1 [252.5] | -2.8 [-13.0, -1.8] | no detectable difference |
| zoom / backTo1SettleMs | 99.8 [100.6] | 84.8 [96.6] | -15.0 [-16.1, +11.4] | no detectable difference |
| zoom / mainTaskMs | 313.7 [317.6] | 282.2 [283.6] | 0.899 [0.883, 0.957] | faster |
| zoom / mainScriptMs | 125.3 [126.4] | 134.2 [135.4] | 1.071 [1.049, 1.121] | no detectable difference |
| select / settleMs | 149.0 [149.3] | 149.0 [149.2] | +0.0 [-1.5, +4.1] | no detectable difference |
| select / mainTaskMs | 79.1 [79.3] | 69.6 [70.0] | 0.880 [0.822, 0.899] | faster |
| select / mainScriptMs | 37.7 [38.9] | 43.2 [43.3] | 1.146 [1.013, 1.150] | slower |
| search / searchMs | 75.0 [77.1] | 73.5 [73.8] | 0.979 [0.936, 1.010] | no detectable difference |
| search / mainTaskMs | 22.4 [23.6] | 21.3 [21.7] | 0.949 [0.844, 0.982] | no detectable difference |
| search / mainScriptMs | 7.5 [7.6] | 8.8 [8.9] | 1.173 [1.015, 1.192] | no detectable difference |
| annotate / settleMs | 166.6 [167.1] | 167.3 [168.7] | +0.6 [-1.1, +2.4] | no detectable difference |
| annotate / mainTaskMs | 96.6 [96.8] | 85.5 [87.7] | 0.885 [0.842, 0.975] | faster |
| annotate / mainScriptMs | 44.5 [44.9] | 47.1 [48.5] | 1.060 [0.969, 1.168] | no detectable difference |

## `plugins-getstate` vs `plugins-item3`

Not run: `plugins-item3`.

## `plugins-getstate` vs `plugins-bump`

| Row | plugins-getstate median [p95] | plugins-bump median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 83.6 [83.7] | 82.5 [84.3] | 0.987 [0.982, 1.012] | no detectable difference |
| open(ebook) / openedMs | 182.2 [183.6] | 182.0 [185.1] | 0.999 [0.982, 1.020] | no detectable difference |
| open(ebook) / firstPaintMs | 307.6 [311.1] | 309.2 [310.6] | 1.005 [0.972, 1.018] | no detectable difference |
| open(ebook) / settledMs | 364.4 [367.1] | 358.1 [358.7] | 0.983 [0.971, 0.986] | no detectable difference |
| open(ebook) / mainTaskMs | 255.2 [256.5] | 238.9 [240.1] | 0.936 [0.922, 0.959] | faster |
| open(ebook) / mainScriptMs | 109.1 [109.7] | 113.0 [113.8] | 1.036 [1.018, 1.069] | no detectable difference |
| open(large) / readyMs | 83.3 [83.5] | 82.9 [86.0] | 0.996 [0.991, 1.040] | no detectable difference |
| open(large) / openedMs | 208.9 [209.5] | 210.1 [212.6] | 1.006 [0.999, 1.020] | no detectable difference |
| open(large) / firstPaintMs | 340.9 [343.6] | 337.5 [338.0] | 0.990 [0.976, 1.011] | no detectable difference |
| open(large) / settledMs | 347.2 [349.1] | 338.5 [341.0] | 0.975 [0.962, 0.995] | no detectable difference |
| open(large) / mainTaskMs | 522.4 [523.9] | 519.9 [521.5] | 0.995 [0.989, 1.006] | no detectable difference |
| open(large) / mainScriptMs | 321.5 [322.7] | 394.7 [398.4] | 1.228 [1.212, 1.242] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2383.6 [2410.7] | 2206.4 [2217.3] | 0.926 [0.875, 0.984] | faster |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.7 [10.0] | 9.2 [9.9] | 0.952 [0.848, 1.113] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 872.0 [872.6] | 728.6 [734.5] | 0.835 [0.814, 0.866] | faster |
| scroll / mainScriptMs | 371.2 [371.6] | 386.9 [392.4] | 1.042 [1.016, 1.084] | no detectable difference |
| jump / settleMs | 182.8 [183.6] | 148.2 [150.9] | -34.6 [-70.3, +43.7] | no detectable difference |
| jump / mainTaskMs | 189.1 [190.6] | 143.3 [146.3] | 0.758 [0.728, 1.016] | no detectable difference |
| jump / mainScriptMs | 127.0 [127.5] | 95.0 [97.2] | 0.749 [0.741, 1.070] | no detectable difference |
| zoom / settleMs | 417.8 [419.2] | 392.9 [397.4] | -24.9 [-34.6, -11.6] | no detectable difference |
| zoom / zoom2SettleMs | 162.8 [164.3] | 143.5 [151.3] | -19.3 [-21.9, -3.1] | faster |
| zoom / zoom4SettleMs | 254.9 [255.0] | 245.8 [249.9] | -9.1 [-13.8, -3.9] | no detectable difference |
| zoom / backTo1SettleMs | 99.8 [100.6] | 85.3 [95.5] | -14.5 [-18.0, +10.2] | no detectable difference |
| zoom / mainTaskMs | 313.7 [317.6] | 263.7 [270.5] | 0.841 [0.800, 0.915] | faster |
| zoom / mainScriptMs | 125.3 [126.4] | 127.6 [133.4] | 1.019 [0.993, 1.108] | no detectable difference |
| select / settleMs | 149.0 [149.3] | 145.4 [146.8] | -3.6 [-5.5, +1.9] | no detectable difference |
| select / mainTaskMs | 79.1 [79.3] | 65.6 [67.5] | 0.829 [0.798, 0.870] | faster |
| select / mainScriptMs | 37.7 [38.9] | 42.6 [43.5] | 1.130 [1.035, 1.157] | no detectable difference |
| search / searchMs | 75.0 [77.1] | 72.5 [74.0] | 0.966 [0.867, 1.015] | no detectable difference |
| search / mainTaskMs | 22.4 [23.6] | 18.9 [22.0] | 0.842 [0.547, 1.004] | no detectable difference |
| search / mainScriptMs | 7.5 [7.6] | 8.6 [8.9] | 1.144 [0.903, 1.190] | no detectable difference |
| annotate / settleMs | 166.6 [167.1] | 163.7 [169.7] | -3.0 [-4.8, +4.0] | no detectable difference |
| annotate / mainTaskMs | 96.6 [96.8] | 85.1 [85.2] | 0.881 [0.837, 0.945] | faster |
| annotate / mainScriptMs | 44.5 [44.9] | 49.5 [50.2] | 1.114 [1.049, 1.207] | slower |

## `plugins-item1` vs `plugins-bump`

| Row | plugins-item1 median [p95] | plugins-bump median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 83.3 [84.4] | 82.5 [84.3] | 0.991 [0.973, 1.026] | no detectable difference |
| open(ebook) / openedMs | 183.3 [183.7] | 182.0 [185.1] | 0.993 [0.982, 1.020] | no detectable difference |
| open(ebook) / firstPaintMs | 308.6 [310.7] | 309.2 [310.6] | 1.002 [0.974, 1.007] | no detectable difference |
| open(ebook) / settledMs | 355.4 [358.4] | 358.1 [358.7] | 1.008 [0.995, 1.010] | no detectable difference |
| open(ebook) / mainTaskMs | 240.4 [240.7] | 238.9 [240.1] | 0.994 [0.983, 1.002] | no detectable difference |
| open(ebook) / mainScriptMs | 113.3 [114.1] | 113.0 [113.8] | 0.998 [0.979, 1.011] | no detectable difference |
| open(large) / readyMs | 83.1 [83.3] | 82.9 [86.0] | 0.998 [0.994, 1.051] | no detectable difference |
| open(large) / openedMs | 210.4 [212.7] | 210.1 [212.6] | 0.999 [0.983, 1.023] | no detectable difference |
| open(large) / firstPaintMs | 337.6 [339.8] | 337.5 [338.0] | 1.000 [0.987, 1.020] | no detectable difference |
| open(large) / settledMs | 339.9 [341.8] | 338.5 [341.0] | 0.996 [0.982, 1.020] | no detectable difference |
| open(large) / mainTaskMs | 522.8 [523.3] | 519.9 [521.5] | 0.994 [0.991, 1.006] | no detectable difference |
| open(large) / mainScriptMs | 396.0 [397.0] | 394.7 [398.4] | 0.997 [0.985, 1.015] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2312.3 [2351.8] | 2206.4 [2217.3] | 0.954 [0.896, 1.021] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.001] | no detectable difference |
| scroll / frameP95Ms | 8.7 [9.5] | 9.2 [9.9] | 1.061 [0.897, 1.155] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 796.1 [802.7] | 728.6 [734.5] | 0.915 [0.884, 0.937] | faster |
| scroll / mainScriptMs | 411.0 [411.6] | 386.9 [392.4] | 0.941 [0.917, 0.987] | no detectable difference |
| jump / settleMs | 158.8 [160.9] | 148.2 [150.9] | -10.7 [-47.8, +42.9] | no detectable difference |
| jump / mainTaskMs | 144.2 [146.3] | 143.3 [146.3] | 0.994 [0.947, 1.056] | no detectable difference |
| jump / mainScriptMs | 95.8 [97.1] | 95.0 [97.2] | 0.992 [0.972, 1.077] | no detectable difference |
| zoom / settleMs | 400.9 [414.8] | 392.9 [397.4] | -8.0 [-31.6, +1.3] | no detectable difference |
| zoom / zoom2SettleMs | 154.6 [163.4] | 143.5 [151.3] | -11.1 [-21.8, +3.7] | no detectable difference |
| zoom / zoom4SettleMs | 252.1 [252.5] | 245.8 [249.9] | -6.3 [-11.3, +8.3] | no detectable difference |
| zoom / backTo1SettleMs | 84.8 [96.6] | 85.3 [95.5] | +0.5 [-15.2, +12.0] | no detectable difference |
| zoom / mainTaskMs | 282.2 [283.6] | 263.7 [270.5] | 0.935 [0.897, 0.966] | no detectable difference |
| zoom / mainScriptMs | 134.2 [135.4] | 127.6 [133.4] | 0.951 [0.927, 1.009] | no detectable difference |
| select / settleMs | 149.0 [149.2] | 145.4 [146.8] | -3.6 [-5.4, -0.9] | no detectable difference |
| select / mainTaskMs | 69.6 [70.0] | 65.6 [67.5] | 0.942 [0.904, 1.038] | no detectable difference |
| select / mainScriptMs | 43.2 [43.3] | 42.6 [43.5] | 0.986 [0.935, 1.100] | no detectable difference |
| search / searchMs | 73.5 [73.8] | 72.5 [74.0] | 0.987 [0.908, 1.025] | no detectable difference |
| search / mainTaskMs | 21.3 [21.7] | 18.9 [22.0] | 0.887 [0.596, 1.112] | no detectable difference |
| search / mainScriptMs | 8.8 [8.9] | 8.6 [8.9] | 0.975 [0.769, 1.156] | no detectable difference |
| annotate / settleMs | 167.3 [168.7] | 163.7 [169.7] | -3.6 [-6.4, +4.3] | no detectable difference |
| annotate / mainTaskMs | 85.5 [87.7] | 85.1 [85.2] | 0.996 [0.922, 1.045] | no detectable difference |
| annotate / mainScriptMs | 47.1 [48.5] | 49.5 [50.2] | 1.051 [0.969, 1.155] | no detectable difference |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-getstate median [p95] | plugins-bump median [p95] | plugins-item1 median [p95] |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.34 MiB] | 34.34 MiB [34.34 MiB] | 41.50 MiB [41.51 MiB] | 41.31 MiB [41.32 MiB] | 41.29 MiB [41.30 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.15 MiB] | 48.15 MiB [48.15 MiB] | 56.53 MiB [56.53 MiB] | 56.12 MiB [56.14 MiB] | 56.10 MiB [56.11 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] |
| soak / heapCycle5Bytes | 37.42 MiB [37.42 MiB] | 37.42 MiB [37.42 MiB] | 44.29 MiB [44.30 MiB] | 44.14 MiB [44.14 MiB] | 44.11 MiB [44.12 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.27 MiB] | 38.26 MiB [38.27 MiB] | 44.95 MiB [44.96 MiB] | 44.68 MiB [44.69 MiB] | 44.66 MiB [44.67 MiB] |
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

`plugins-item1`:

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

