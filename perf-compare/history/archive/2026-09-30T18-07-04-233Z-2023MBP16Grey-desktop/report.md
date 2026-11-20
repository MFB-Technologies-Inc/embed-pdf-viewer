# Viewer perf comparison: 2026-09-30T18-07-04-233Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 267af66b7b1c63fb96c84cd9bd67221147240937 (uncommitted changes) |
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
| plugins-toolbar | research/rust-kernel-toolbar | e4d533d18 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 764d1ef49e62 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-getstate`: pass
- `plugins-toolbar`: pass

## Sizes

| Metric | main | main-aa | plugins-getstate | plugins-toolbar |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 11.69 MiB | 11.69 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.88 MiB | 1.88 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.62 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

39 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 51.8 [51.9] | 52.2 [54.7] | 1.009 [0.999, 1.075] | pass |
| open(ebook) / openedMs | 114.4 [114.6] | 115.1 [117.9] | 1.006 [1.001, 1.049] | pass |
| open(ebook) / firstPaintMs | 213.6 [213.8] | 213.7 [217.7] | 1.001 [0.994, 1.062] | pass |
| open(ebook) / settledMs | 333.5 [333.9] | 334.0 [336.3] | 1.002 [0.995, 1.020] | pass |
| open(ebook) / mainTaskMs | 115.5 [116.1] | 116.2 [117.4] | 1.006 [0.997, 1.028] | pass |
| open(ebook) / mainScriptMs | 19.6 [20.4] | 19.9 [20.9] | 1.012 [0.938, 1.083] | pass |
| open(large) / readyMs | 52.2 [54.8] | 53.4 [54.1] | 1.023 [0.953, 1.043] | pass |
| open(large) / openedMs | 135.7 [137.1] | 134.7 [135.5] | 0.993 [0.968, 1.014] | pass |
| open(large) / firstPaintMs | 220.8 [226.5] | 224.4 [226.3] | 1.016 [0.901, 1.028] | pass |
| open(large) / settledMs | 217.6 [223.3] | 221.3 [222.8] | 1.017 [0.967, 1.027] | pass |
| open(large) / mainTaskMs | 265.3 [266.3] | 257.3 [258.8] | 0.970 [0.966, 1.007] | pass |
| open(large) / mainScriptMs | 23.1 [23.3] | 23.2 [23.4] | 1.005 [0.990, 1.030] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2007.2 [2023.0] | 2007.6 [2025.5] | 1.000 [0.988, 1.013] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [8.4] | 8.4 [8.5] | 1.000 [0.996, 1.024] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 399.9 [405.7] | 400.5 [401.0] | 1.002 [0.974, 1.006] | pass |
| scroll / mainScriptMs | 53.9 [55.6] | 54.9 [55.0] | 1.018 [0.953, 1.023] | pass |
| jump / settleMs | 97.8 [97.9] | 97.9 [99.8] | +0.2 [-3.4, +5.8] | pass |
| jump / mainTaskMs | 56.7 [58.8] | 57.9 [58.8] | 1.021 [0.946, 1.069] | pass |
| jump / mainScriptMs | 15.4 [15.5] | 14.5 [16.7] | 0.940 [0.917, 1.153] | pass |
| zoom / settleMs | 370.7 [376.5] | 352.7 [368.4] | -18.0 [-33.2, +24.0] | pass |
| zoom / zoom2SettleMs | 133.4 [138.7] | 136.3 [137.1] | +2.9 [-9.6, +10.0] | pass |
| zoom / zoom4SettleMs | 243.5 [243.7] | 215.5 [232.0] | -28.0 [-29.6, +27.0] | pass |
| zoom / backTo1SettleMs | 73.4 [75.7] | 67.8 [68.7] | -5.6 [-9.4, +2.6] | pass |
| zoom / mainTaskMs | 164.7 [170.1] | 166.3 [166.5] | 1.010 [0.914, 1.015] | pass |
| zoom / mainScriptMs | 23.0 [23.2] | 24.6 [26.3] | 1.073 [0.982, 1.180] | pass |
| select / settleMs | 144.1 [150.2] | 148.1 [171.2] | +4.0 [-4.7, +30.7] | pass |
| select / mainTaskMs | 43.5 [46.1] | 44.5 [49.1] | 1.023 [0.944, 1.233] | pass |
| select / mainScriptMs | 9.8 [9.9] | 9.9 [11.5] | 1.008 [0.989, 1.352] | pass |
| search / searchMs | 58.2 [58.4] | 59.4 [62.3] | 1.021 [1.010, 1.282] | pass |
| search / mainTaskMs | 4.5 [4.6] | 4.4 [5.4] | 0.979 [0.926, 1.260] | pass |
| search / mainScriptMs | 0.1 [0.1] | 0.1 [0.2] | 1.027 [0.721, 1.387] | pass |
| annotate / settleMs | 155.5 [156.4] | 159.5 [167.4] | +4.0 [-2.9, +12.8] | pass |
| annotate / mainTaskMs | 54.8 [55.0] | 56.2 [57.7] | 1.026 [1.003, 1.064] | pass |
| annotate / mainScriptMs | 10.7 [11.1] | 11.1 [11.2] | 1.030 [0.955, 1.057] | pass |

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
| open(ebook) / readyMs | 51.8 [51.9] | 82.6 [84.6] | 1.595 [1.565, 1.658] | slower |
| open(ebook) / openedMs | 114.4 [114.6] | 181.2 [183.3] | 1.583 [1.579, 1.629] | slower |
| open(ebook) / firstPaintMs | 213.6 [213.8] | 312.5 [314.2] | 1.463 [1.460, 1.531] | slower |
| open(ebook) / settledMs | 333.5 [333.9] | 364.5 [370.1] | 1.093 [1.086, 1.123] | slower |
| open(ebook) / mainTaskMs | 115.5 [116.1] | 253.0 [253.5] | 2.190 [2.146, 2.218] | slower |
| open(ebook) / mainScriptMs | 19.6 [20.4] | 108.1 [108.2] | 5.502 [5.187, 5.592] | slower |
| open(large) / readyMs | 52.2 [54.8] | 84.3 [86.4] | 1.615 [1.523, 1.668] | slower |
| open(large) / openedMs | 135.7 [137.1] | 210.8 [211.7] | 1.553 [1.532, 1.583] | slower |
| open(large) / firstPaintMs | 220.8 [226.5] | 343.2 [345.6] | 1.554 [1.486, 1.569] | slower |
| open(large) / settledMs | 217.6 [223.3] | 349.0 [350.5] | 1.604 [1.522, 1.615] | slower |
| open(large) / mainTaskMs | 265.3 [266.3] | 522.9 [523.3] | 1.971 [1.926, 2.036] | slower |
| open(large) / mainScriptMs | 23.1 [23.3] | 322.1 [323.4] | 13.930 [13.579, 14.230] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2007.2 [2023.0] | 2239.7 [2368.9] | 1.116 [1.102, 1.191] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.4] | 8.8 [8.9] | 1.052 [1.019, 1.064] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 399.9 [405.7] | 824.0 [847.5] | 2.060 [2.013, 2.132] | slower |
| scroll / mainScriptMs | 53.9 [55.6] | 348.4 [359.0] | 6.460 [6.244, 6.706] | slower |
| jump / settleMs | 97.8 [97.9] | 129.2 [148.8] | +31.5 [+5.4, +56.8] | slower |
| jump / mainTaskMs | 56.7 [58.8] | 145.2 [152.0] | 2.558 [2.395, 2.772] | slower |
| jump / mainScriptMs | 15.4 [15.5] | 94.4 [96.5] | 6.113 [6.000, 6.567] | slower |
| zoom / settleMs | 370.7 [376.5] | 422.7 [431.1] | +51.9 [+13.2, +85.9] | slower |
| zoom / zoom2SettleMs | 133.4 [138.7] | 155.2 [163.6] | +21.8 [+15.8, +37.2] | slower |
| zoom / zoom4SettleMs | 243.5 [243.7] | 267.5 [267.5] | +24.0 [-8.6, +60.7] | no detectable difference |
| zoom / backTo1SettleMs | 73.4 [75.7] | 86.9 [103.4] | +13.5 [-1.1, +39.0] | no detectable difference |
| zoom / mainTaskMs | 164.7 [170.1] | 317.6 [318.3] | 1.929 [1.732, 1.940] | slower |
| zoom / mainScriptMs | 23.0 [23.2] | 127.0 [128.3] | 5.534 [5.122, 5.716] | slower |
| select / settleMs | 144.1 [150.2] | 154.5 [156.4] | +10.3 [-1.1, +13.5] | no detectable difference |
| select / mainTaskMs | 43.5 [46.1] | 75.1 [79.6] | 1.726 [1.601, 1.993] | slower |
| select / mainScriptMs | 9.8 [9.9] | 37.0 [39.1] | 3.782 [3.591, 4.539] | slower |
| search / searchMs | 58.2 [58.4] | 64.7 [71.3] | 1.112 [1.081, 1.474] | no detectable difference |
| search / mainTaskMs | 4.5 [4.6] | 12.5 [19.4] | 2.778 [2.467, 4.592] | slower |
| search / mainScriptMs | 0.1 [0.1] | 6.2 [7.4] | 54.496 [38.896, 67.937] | slower |
| annotate / settleMs | 155.5 [156.4] | 165.4 [169.0] | +9.9 [+8.8, +14.0] | no detectable difference |
| annotate / mainTaskMs | 54.8 [55.0] | 94.5 [97.3] | 1.723 [1.698, 1.793] | slower |
| annotate / mainScriptMs | 10.7 [11.1] | 43.9 [45.4] | 4.091 [3.860, 4.288] | slower |

## `plugins-getstate` vs `plugins-toolbar`

| Row | plugins-getstate median [p95] | plugins-toolbar median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 82.6 [84.6] | 83.8 [85.0] | 1.014 [0.966, 1.047] | no detectable difference |
| open(ebook) / openedMs | 181.2 [183.3] | 183.8 [187.1] | 1.014 [0.988, 1.036] | no detectable difference |
| open(ebook) / firstPaintMs | 312.5 [314.2] | 309.7 [309.9] | 0.991 [0.971, 0.993] | no detectable difference |
| open(ebook) / settledMs | 364.5 [370.1] | 359.8 [364.3] | 0.987 [0.964, 1.007] | no detectable difference |
| open(ebook) / mainTaskMs | 253.0 [253.5] | 239.9 [240.9] | 0.948 [0.941, 0.967] | faster |
| open(ebook) / mainScriptMs | 108.1 [108.2] | 112.8 [113.3] | 1.043 [1.030, 1.067] | no detectable difference |
| open(large) / readyMs | 84.3 [86.4] | 84.2 [84.4] | 0.999 [0.969, 1.006] | no detectable difference |
| open(large) / openedMs | 210.8 [211.7] | 211.3 [214.5] | 1.002 [0.995, 1.022] | no detectable difference |
| open(large) / firstPaintMs | 343.2 [345.6] | 334.6 [344.4] | 0.975 [0.964, 1.024] | no detectable difference |
| open(large) / settledMs | 349.0 [350.5] | 335.4 [345.2] | 0.961 [0.955, 1.016] | no detectable difference |
| open(large) / mainTaskMs | 522.9 [523.3] | 524.5 [529.3] | 1.003 [0.990, 1.033] | no detectable difference |
| open(large) / mainScriptMs | 322.1 [323.4] | 398.7 [401.6] | 1.238 [1.202, 1.271] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2239.7 [2368.9] | 2271.0 [2310.5] | 1.014 [0.943, 1.038] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.001] | no detectable difference |
| scroll / frameP95Ms | 8.8 [8.9] | 8.8 [9.1] | 0.996 [0.970, 1.065] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 824.0 [847.5] | 803.3 [809.6] | 0.975 [0.925, 0.991] | no detectable difference |
| scroll / mainScriptMs | 348.4 [359.0] | 416.2 [420.8] | 1.195 [1.123, 1.210] | slower |
| jump / settleMs | 129.2 [148.8] | 123.3 [141.8] | -5.9 [-37.2, +40.5] | no detectable difference |
| jump / mainTaskMs | 145.2 [152.0] | 142.7 [145.6] | 0.983 [0.924, 1.032] | no detectable difference |
| jump / mainScriptMs | 94.4 [96.5] | 93.8 [97.0] | 0.993 [0.957, 1.047] | no detectable difference |
| zoom / settleMs | 422.7 [431.1] | 406.1 [417.2] | -16.5 [-33.5, +28.1] | no detectable difference |
| zoom / zoom2SettleMs | 155.2 [163.6] | 155.0 [155.1] | -0.2 [-17.7, -0.1] | no detectable difference |
| zoom / zoom4SettleMs | 267.5 [267.5] | 259.3 [263.0] | -8.2 [-24.1, +28.2] | no detectable difference |
| zoom / backTo1SettleMs | 86.9 [103.4] | 87.0 [99.2] | +0.1 [-19.2, +25.8] | no detectable difference |
| zoom / mainTaskMs | 317.6 [318.3] | 280.5 [282.9] | 0.883 [0.852, 0.957] | faster |
| zoom / mainScriptMs | 127.0 [128.3] | 132.5 [135.1] | 1.043 [1.021, 1.135] | no detectable difference |
| select / settleMs | 154.5 [156.4] | 145.6 [150.9] | -8.8 [-13.1, +1.7] | no detectable difference |
| select / mainTaskMs | 75.1 [79.6] | 68.5 [68.9] | 0.912 [0.838, 0.928] | no detectable difference |
| select / mainScriptMs | 37.0 [39.1] | 41.9 [42.5] | 1.134 [1.050, 1.190] | no detectable difference |
| search / searchMs | 64.7 [71.3] | 71.6 [71.7] | 1.107 [0.905, 1.135] | no detectable difference |
| search / mainTaskMs | 12.5 [19.4] | 20.1 [20.6] | 1.608 [0.572, 1.796] | no detectable difference |
| search / mainScriptMs | 6.2 [7.4] | 7.8 [8.4] | 1.272 [0.874, 1.407] | no detectable difference |
| annotate / settleMs | 165.4 [169.0] | 165.7 [167.2] | +0.3 [-3.9, +2.0] | no detectable difference |
| annotate / mainTaskMs | 94.5 [97.3] | 90.0 [90.1] | 0.953 [0.901, 0.965] | no detectable difference |
| annotate / mainScriptMs | 43.9 [45.4] | 50.6 [51.4] | 1.152 [1.062, 1.194] | slower |

## `main` vs `plugins-toolbar`

| Row | main median [p95] | plugins-toolbar median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 51.8 [51.9] | 83.8 [85.0] | 1.617 [1.578, 1.664] | slower |
| open(ebook) / openedMs | 114.4 [114.6] | 183.8 [187.1] | 1.606 [1.582, 1.664] | slower |
| open(ebook) / firstPaintMs | 213.6 [213.8] | 309.7 [309.9] | 1.450 [1.427, 1.509] | slower |
| open(ebook) / settledMs | 333.5 [333.9] | 359.8 [364.3] | 1.079 [1.070, 1.105] | slower |
| open(ebook) / mainTaskMs | 115.5 [116.1] | 239.9 [240.9] | 2.077 [2.054, 2.108] | slower |
| open(ebook) / mainScriptMs | 19.6 [20.4] | 112.8 [113.3] | 5.741 [5.440, 5.857] | slower |
| open(large) / readyMs | 52.2 [54.8] | 84.2 [84.4] | 1.614 [1.524, 1.624] | slower |
| open(large) / openedMs | 135.7 [137.1] | 211.3 [214.5] | 1.557 [1.534, 1.607] | slower |
| open(large) / firstPaintMs | 220.8 [226.5] | 334.6 [344.4] | 1.515 [1.468, 1.567] | slower |
| open(large) / settledMs | 217.6 [223.3] | 335.4 [345.2] | 1.541 [1.495, 1.595] | slower |
| open(large) / mainTaskMs | 265.3 [266.3] | 524.5 [529.3] | 1.977 [1.945, 2.061] | slower |
| open(large) / mainScriptMs | 23.1 [23.3] | 398.7 [401.6] | 17.243 [16.701, 17.680] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2007.2 [2023.0] | 2271.0 [2310.5] | 1.131 [1.110, 1.157] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.4] | 8.8 [9.1] | 1.048 [1.029, 1.088] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 399.9 [405.7] | 803.3 [809.6] | 2.009 [1.936, 2.032] | slower |
| scroll / mainScriptMs | 53.9 [55.6] | 416.2 [420.8] | 7.716 [7.252, 7.844] | slower |
| jump / settleMs | 97.8 [97.9] | 123.3 [141.8] | +25.6 [+15.8, +49.6] | slower |
| jump / mainTaskMs | 56.7 [58.8] | 142.7 [145.6] | 2.516 [2.389, 2.648] | slower |
| jump / mainScriptMs | 15.4 [15.5] | 93.8 [97.0] | 6.070 [5.973, 6.605] | slower |
| zoom / settleMs | 370.7 [376.5] | 406.1 [417.2] | +35.4 [+21.4, +72.3] | slower |
| zoom / zoom2SettleMs | 133.4 [138.7] | 155.0 [155.1] | +21.6 [+7.5, +27.8] | slower |
| zoom / zoom4SettleMs | 243.5 [243.7] | 259.3 [263.0] | +15.8 [-0.3, +56.6] | no detectable difference |
| zoom / backTo1SettleMs | 73.4 [75.7] | 87.0 [99.2] | +13.6 [+10.0, +34.4] | slower |
| zoom / mainTaskMs | 164.7 [170.1] | 280.5 [282.9] | 1.703 [1.588, 1.726] | slower |
| zoom / mainScriptMs | 23.0 [23.2] | 132.5 [135.1] | 5.772 [5.637, 6.023] | slower |
| select / settleMs | 144.1 [150.2] | 145.6 [150.9] | +1.5 [-7.4, +8.4] | no detectable difference |
| select / mainTaskMs | 43.5 [46.1] | 68.5 [68.9] | 1.573 [1.448, 1.715] | slower |
| select / mainScriptMs | 9.8 [9.9] | 41.9 [42.5] | 4.287 [4.147, 4.910] | slower |
| search / searchMs | 58.2 [58.4] | 71.6 [71.7] | 1.231 [1.115, 1.468] | no detectable difference |
| search / mainTaskMs | 4.5 [4.6] | 20.1 [20.6] | 4.467 [2.471, 4.710] | slower |
| search / mainScriptMs | 0.1 [0.1] | 7.8 [8.4] | 69.336 [42.812, 75.946] | slower |
| annotate / settleMs | 155.5 [156.4] | 165.7 [167.2] | +10.2 [+9.0, +11.9] | no detectable difference |
| annotate / mainTaskMs | 54.8 [55.0] | 90.0 [90.1] | 1.642 [1.599, 1.656] | slower |
| annotate / mainScriptMs | 10.7 [11.1] | 50.6 [51.4] | 4.713 [4.333, 4.844] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-getstate median [p95] | plugins-toolbar median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.34 MiB [34.35 MiB] | 41.50 MiB [41.51 MiB] | 41.30 MiB [41.31 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.15 MiB] | 48.14 MiB [48.14 MiB] | 56.52 MiB [56.54 MiB] | 56.10 MiB [56.11 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.41 MiB] | 37.42 MiB [37.42 MiB] | 44.29 MiB [44.30 MiB] | 44.11 MiB [44.11 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.27 MiB] | 38.27 MiB [38.27 MiB] | 44.95 MiB [44.96 MiB] | 44.66 MiB [44.67 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] |

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

