# Viewer perf comparison: 2026-10-01T04-27-30-603Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 68875083c06d75b8ba0bc17411b1587a857bff24 |
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
| impl-enginelink | research/rust-core-engine-link | 2a3bb96ed | built (fork 7c7b600b1, wasm32-eh) | c3652e01518f |  |
| plugins-enginelink | research/rust-kernel-engine-link | 48fda657e | built (fork 7c7b600b1, wasm32-eh) | c3652e01518f | d6276d5fb70d |
| impl-fixed | research/multi-platform-rust-core-impl | 29f02d381 | built (fork 7c7b600b1, wasm32-eh) | ee06b8271360 |  |
| plugins-fixed | research/multi-platform-rust-core-impl-plugins | c92019192 | built (fork 7c7b600b1, wasm32-eh) | ee06b8271360 | d6276d5fb70d |
| impl-o2 | research/rust-core-engine-o2 | 977a874e4 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 |  |
| plugins-o2 | research/rust-kernel-engine-o2 | 785e68335 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | d6276d5fb70d |

## Gate

- `main`: pass
- `main-aa`: pass
- `impl-enginelink`: pass
- `plugins-enginelink`: pass
- `impl-fixed`: pass
- `plugins-fixed`: pass
- `impl-o2`: pass
- `plugins-o2`: pass

## Sizes

| Metric | main | main-aa | impl-enginelink | plugins-enginelink | impl-fixed | plugins-fixed | impl-o2 | plugins-o2 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.63 MiB | 9.63 MiB | 10.60 MiB | 10.60 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB | 2.85 MiB | 2.85 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 7.93 MiB | 0.00 MiB | 7.93 MiB | 0.00 MiB | 7.93 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 1.70 MiB | 0.00 MiB | 1.70 MiB | 0.00 MiB | 1.70 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 3.34 MiB | 2.61 MiB | 3.36 MiB | 2.63 MiB | 3.36 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.95 MiB | 0.79 MiB | 0.96 MiB | 0.80 MiB | 0.96 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

39 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.5 [52.9] | 52.1 [52.9] | 0.993 [0.983, 1.013] | pass |
| open(ebook) / openedMs | 116.3 [117.0] | 115.2 [117.5] | 0.990 [0.983, 1.015] | pass |
| open(ebook) / firstPaintMs | 217.4 [218.7] | 214.0 [218.5] | 0.984 [0.945, 1.015] | pass |
| open(ebook) / settledMs | 336.9 [337.7] | 334.3 [339.8] | 0.992 [0.980, 1.017] | pass |
| open(ebook) / mainTaskMs | 120.6 [121.5] | 118.9 [121.0] | 0.986 [0.941, 1.007] | pass |
| open(ebook) / mainJsMs | 93.8 [94.9] | 92.8 [94.3] | 0.988 [0.934, 1.013] | pass |
| open(large) / readyMs | 52.6 [55.3] | 53.1 [54.4] | 1.010 [0.943, 1.055] | pass |
| open(large) / openedMs | 134.5 [138.9] | 134.4 [136.5] | 0.999 [0.954, 1.028] | pass |
| open(large) / firstPaintMs | 221.6 [226.5] | 222.6 [225.3] | 1.004 [0.976, 1.022] | pass |
| open(large) / settledMs | 224.1 [226.9] | 219.9 [223.6] | 0.981 [0.964, 1.027] | pass |
| open(large) / mainTaskMs | 260.6 [268.0] | 260.5 [264.2] | 1.000 [0.969, 1.016] | pass |
| open(large) / mainJsMs | 237.2 [244.7] | 237.5 [240.3] | 1.001 [0.967, 1.017] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2019.8 [2033.9] | 2011.6 [2016.7] | 0.996 [0.986, 1.006] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [9.4] | 8.5 [8.8] | 1.006 [0.887, 1.052] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 389.1 [393.1] | 391.5 [394.7] | 1.006 [0.925, 1.072] | pass |
| scroll / mainJsMs | 298.3 [303.0] | 299.6 [303.6] | 1.004 [0.921, 1.077] | pass |
| jump / settleMs | 93.0 [98.7] | 96.8 [97.2] | +3.7 [-10.3, +5.7] | pass |
| jump / mainTaskMs | 55.4 [58.6] | 58.3 [61.8] | 1.053 [0.970, 1.183] | pass |
| jump / mainJsMs | 40.8 [43.3] | 44.2 [44.9] | 1.082 [0.983, 1.152] | pass |
| zoom / settleMs | 392.9 [393.9] | 387.6 [396.2] | -5.3 [-18.8, +27.1] | pass |
| zoom / zoom2SettleMs | 131.9 [133.2] | 126.3 [133.0] | -5.7 [-8.0, +8.8] | pass |
| zoom / zoom4SettleMs | 260.9 [268.2] | 262.2 [263.2] | +1.3 [-20.0, +26.6] | pass |
| zoom / backTo1SettleMs | 81.0 [88.8] | 73.3 [89.1] | -7.7 [-32.1, +29.7] | pass |
| zoom / mainTaskMs | 173.8 [178.6] | 177.4 [179.3] | 1.021 [0.932, 1.061] | pass |
| zoom / mainJsMs | 126.0 [130.4] | 130.8 [131.8] | 1.039 [0.918, 1.073] | pass |
| select / settleMs | 144.3 [146.4] | 148.3 [148.3] | +4.0 [-1.8, +4.2] | pass |
| select / mainTaskMs | 45.7 [49.5] | 47.5 [48.4] | 1.038 [0.910, 1.323] | pass |
| select / mainJsMs | 28.6 [30.6] | 29.8 [29.9] | 1.044 [0.907, 1.312] | pass |
| search / searchMs | 57.9 [61.6] | 48.5 [61.0] | 0.839 [0.770, 1.120] | pass |
| search / mainTaskMs | 4.7 [5.1] | 4.3 [4.6] | 0.916 [0.761, 1.013] | pass |
| search / mainJsMs | 3.2 [3.2] | 3.0 [3.2] | 0.947 [0.899, 1.095] | pass |
| annotate / settleMs | 153.1 [157.1] | 155.7 [160.7] | +2.6 [-4.3, +8.4] | pass |
| annotate / mainTaskMs | 52.9 [58.1] | 55.4 [59.4] | 1.047 [0.928, 1.153] | pass |
| annotate / mainJsMs | 31.8 [33.6] | 33.3 [35.4] | 1.048 [0.963, 1.159] | pass |

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

Not run: `impl-ts`, `impl-gate`.

## `plugins-head` vs `plugins-buildlto`

Not run: `plugins-head`, `plugins-buildlto`.

## `plugins-head` vs `plugins-wasmopt`

Not run: `plugins-head`, `plugins-wasmopt`.

## `plugins-bump` vs `plugins-buildlto`

Not run: `plugins-bump`, `plugins-buildlto`.

## `impl-head` vs `impl-enginelink`

Not run: `impl-head`.

## `plugins-tip` vs `plugins-enginelink`

Not run: `plugins-tip`.

## `impl-head` vs `impl-fixed`

Not run: `impl-head`.

## `plugins-tip` vs `plugins-fixed`

Not run: `plugins-tip`.

## `impl-fixed` vs `impl-enginelink`

| Row | impl-fixed median [p95] | impl-enginelink median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.5 [53.8] | 52.1 [52.5] | 0.973 [0.950, 1.006] | no detectable difference |
| open(ebook) / openedMs | 122.5 [124.9] | 114.3 [117.7] | 0.933 [0.908, 0.970] | faster |
| open(ebook) / firstPaintMs | 221.0 [222.7] | 214.9 [215.6] | 0.972 [0.951, 0.982] | no detectable difference |
| open(ebook) / settledMs | 269.2 [270.7] | 259.9 [262.9] | 0.966 [0.955, 0.989] | faster |
| open(ebook) / mainTaskMs | 104.8 [107.0] | 104.2 [104.7] | 0.994 [0.952, 1.006] | no detectable difference |
| open(ebook) / mainJsMs | 81.5 [82.5] | 80.3 [81.0] | 0.986 [0.951, 1.014] | no detectable difference |
| open(large) / readyMs | 54.3 [54.7] | 52.1 [52.2] | 0.960 [0.948, 1.005] | no detectable difference |
| open(large) / openedMs | 145.5 [146.4] | 134.5 [136.7] | 0.925 [0.914, 0.959] | faster |
| open(large) / firstPaintMs | 230.3 [234.4] | 221.2 [223.7] | 0.960 [0.919, 0.992] | faster |
| open(large) / settledMs | 226.2 [228.9] | 224.3 [226.8] | 0.992 [0.922, 1.023] | no detectable difference |
| open(large) / mainTaskMs | 162.7 [163.9] | 162.3 [163.9] | 0.997 [0.980, 1.016] | no detectable difference |
| open(large) / mainJsMs | 136.7 [137.2] | 135.8 [137.4] | 0.993 [0.981, 1.015] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2002.0 [2026.5] | 2013.9 [2017.3] | 1.006 [0.977, 1.010] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [9.2] | 8.4 [8.7] | 1.006 [0.904, 1.045] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 322.8 [332.3] | 319.5 [335.2] | 0.990 [0.943, 1.070] | no detectable difference |
| scroll / mainJsMs | 220.2 [226.7] | 220.8 [229.7] | 1.003 [0.940, 1.075] | no detectable difference |
| jump / settleMs | 86.7 [90.6] | 81.7 [88.3] | -5.1 [-17.6, +2.5] | no detectable difference |
| jump / mainTaskMs | 54.6 [55.7] | 52.8 [54.0] | 0.967 [0.870, 1.008] | no detectable difference |
| jump / mainJsMs | 33.9 [35.0] | 32.4 [33.4] | 0.956 [0.909, 1.025] | no detectable difference |
| zoom / settleMs | 372.1 [382.0] | 387.2 [388.9] | +15.1 [-4.7, +34.0] | no detectable difference |
| zoom / zoom2SettleMs | 140.5 [143.3] | 143.7 [145.9] | +3.1 [-11.3, +13.1] | no detectable difference |
| zoom / zoom4SettleMs | 231.6 [238.7] | 242.9 [253.7] | +11.3 [-4.8, +32.9] | no detectable difference |
| zoom / backTo1SettleMs | 66.1 [69.2] | 69.4 [72.3] | +3.4 [-8.6, +12.3] | no detectable difference |
| zoom / mainTaskMs | 144.5 [149.6] | 143.4 [156.9] | 0.993 [0.942, 1.115] | no detectable difference |
| zoom / mainJsMs | 96.1 [97.9] | 100.1 [106.6] | 1.041 [0.999, 1.155] | no detectable difference |
| select / settleMs | 148.9 [151.0] | 147.1 [152.4] | -1.8 [-4.6, +5.1] | no detectable difference |
| select / mainTaskMs | 40.7 [43.5] | 38.9 [39.2] | 0.957 [0.863, 1.045] | no detectable difference |
| select / mainJsMs | 19.3 [22.1] | 19.2 [19.9] | 0.994 [0.815, 1.035] | no detectable difference |
| search / searchMs | 55.3 [63.7] | 50.7 [52.7] | 0.916 [0.701, 1.127] | no detectable difference |
| search / mainTaskMs | 2.4 [2.9] | 2.5 [3.2] | 1.041 [0.799, 1.416] | no detectable difference |
| search / mainJsMs | 1.2 [1.3] | 1.2 [1.3] | 0.988 [0.803, 1.209] | no detectable difference |
| annotate / settleMs | 160.5 [160.6] | 153.3 [158.8] | -7.3 [-8.0, +6.5] | no detectable difference |
| annotate / mainTaskMs | 49.5 [52.4] | 50.0 [54.7] | 1.011 [0.891, 1.209] | no detectable difference |
| annotate / mainJsMs | 23.8 [25.7] | 24.9 [26.0] | 1.044 [0.862, 1.179] | no detectable difference |

## `plugins-fixed` vs `plugins-enginelink`

| Row | plugins-fixed median [p95] | plugins-enginelink median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 75.2 [77.0] | 74.0 [74.8] | 0.984 [0.955, 1.002] | no detectable difference |
| open(ebook) / openedMs | 177.5 [180.1] | 171.3 [172.6] | 0.965 [0.938, 0.988] | faster |
| open(ebook) / firstPaintMs | 305.6 [308.8] | 292.8 [299.8] | 0.958 [0.947, 1.000] | no detectable difference |
| open(ebook) / settledMs | 355.2 [358.4] | 343.0 [348.3] | 0.966 [0.954, 0.993] | faster |
| open(ebook) / mainTaskMs | 237.5 [238.8] | 235.6 [236.5] | 0.992 [0.977, 1.003] | no detectable difference |
| open(ebook) / mainJsMs | 198.9 [199.6] | 197.6 [199.1] | 0.993 [0.986, 1.009] | no detectable difference |
| open(large) / readyMs | 74.7 [75.0] | 74.8 [75.8] | 1.002 [0.991, 1.033] | no detectable difference |
| open(large) / openedMs | 201.4 [203.6] | 197.5 [197.6] | 0.980 [0.969, 0.983] | no detectable difference |
| open(large) / firstPaintMs | 334.7 [335.2] | 327.3 [327.8] | 0.978 [0.975, 0.994] | no detectable difference |
| open(large) / settledMs | 339.4 [339.9] | 337.7 [337.8] | 0.995 [0.991, 1.001] | no detectable difference |
| open(large) / mainTaskMs | 525.4 [527.1] | 526.7 [528.3] | 1.002 [0.997, 1.010] | no detectable difference |
| open(large) / mainJsMs | 489.2 [491.0] | 490.5 [492.1] | 1.003 [0.994, 1.009] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2219.2 [2256.5] | 2221.3 [2227.9] | 1.001 [0.972, 1.067] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.3 [9.5] | 9.7 [9.7] | 1.034 [0.907, 1.088] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 721.6 [759.3] | 746.6 [747.1] | 1.035 [0.875, 1.214] | no detectable difference |
| scroll / mainJsMs | 622.5 [655.0] | 644.9 [645.8] | 1.036 [0.869, 1.237] | no detectable difference |
| jump / settleMs | 130.4 [135.6] | 114.8 [155.1] | -15.6 [-21.6, +44.2] | no detectable difference |
| jump / mainTaskMs | 149.7 [151.3] | 144.8 [147.3] | 0.968 [0.946, 0.994] | no detectable difference |
| jump / mainJsMs | 133.9 [134.8] | 130.8 [132.6] | 0.977 [0.968, 1.013] | no detectable difference |
| zoom / settleMs | 426.4 [426.6] | 429.2 [432.3] | +2.8 [-7.4, +19.8] | no detectable difference |
| zoom / zoom2SettleMs | 151.1 [151.6] | 151.5 [152.1] | +0.4 [-1.5, +9.1] | no detectable difference |
| zoom / zoom4SettleMs | 274.8 [275.5] | 277.8 [282.0] | +3.0 [-8.5, +12.6] | no detectable difference |
| zoom / backTo1SettleMs | 85.7 [86.1] | 84.2 [99.2] | -1.5 [-2.0, +17.4] | no detectable difference |
| zoom / mainTaskMs | 274.3 [274.7] | 272.5 [291.2] | 0.993 [0.985, 1.086] | no detectable difference |
| zoom / mainJsMs | 232.0 [232.5] | 229.0 [240.4] | 0.987 [0.971, 1.071] | no detectable difference |
| select / settleMs | 141.6 [152.5] | 144.4 [148.9] | +2.8 [-9.6, +11.0] | no detectable difference |
| select / mainTaskMs | 65.1 [70.1] | 67.4 [68.1] | 1.035 [0.921, 1.204] | no detectable difference |
| select / mainJsMs | 51.2 [54.8] | 53.9 [54.0] | 1.053 [0.929, 1.336] | no detectable difference |
| search / searchMs | 69.2 [70.2] | 66.6 [69.1] | 0.962 [0.863, 1.009] | no detectable difference |
| search / mainTaskMs | 18.2 [18.4] | 16.5 [19.6] | 0.907 [0.532, 1.198] | no detectable difference |
| search / mainJsMs | 7.6 [8.1] | 7.6 [8.4] | 0.999 [0.837, 1.119] | no detectable difference |
| annotate / settleMs | 164.1 [165.0] | 165.0 [165.2] | +0.9 [-2.0, +3.2] | no detectable difference |
| annotate / mainTaskMs | 83.2 [91.1] | 81.7 [84.7] | 0.982 [0.878, 1.058] | no detectable difference |
| annotate / mainJsMs | 62.4 [67.2] | 60.6 [63.5] | 0.971 [0.892, 1.062] | no detectable difference |

## `impl-fixed` vs `impl-o2`

| Row | impl-fixed median [p95] | impl-o2 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.5 [53.8] | 52.8 [53.2] | 0.986 [0.974, 1.021] | no detectable difference |
| open(ebook) / openedMs | 122.5 [124.9] | 116.7 [117.1] | 0.953 [0.923, 0.962] | faster |
| open(ebook) / firstPaintMs | 221.0 [222.7] | 216.4 [217.6] | 0.979 [0.957, 0.991] | no detectable difference |
| open(ebook) / settledMs | 269.2 [270.7] | 263.6 [265.6] | 0.979 [0.962, 0.999] | no detectable difference |
| open(ebook) / mainTaskMs | 104.8 [107.0] | 107.2 [107.7] | 1.023 [0.981, 1.034] | no detectable difference |
| open(ebook) / mainJsMs | 81.5 [82.5] | 83.6 [84.0] | 1.026 [0.974, 1.051] | no detectable difference |
| open(large) / readyMs | 54.3 [54.7] | 52.7 [54.3] | 0.971 [0.957, 1.047] | no detectable difference |
| open(large) / openedMs | 145.5 [146.4] | 139.5 [140.5] | 0.959 [0.934, 0.985] | no detectable difference |
| open(large) / firstPaintMs | 230.3 [234.4] | 223.6 [225.0] | 0.971 [0.925, 0.997] | no detectable difference |
| open(large) / settledMs | 226.2 [228.9] | 220.5 [221.3] | 0.975 [0.956, 0.997] | no detectable difference |
| open(large) / mainTaskMs | 162.7 [163.9] | 161.8 [163.0] | 0.994 [0.978, 1.011] | no detectable difference |
| open(large) / mainJsMs | 136.7 [137.2] | 135.0 [136.4] | 0.987 [0.980, 1.007] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2002.0 [2026.5] | 2006.2 [2013.2] | 1.002 [0.989, 1.008] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [9.2] | 8.6 [8.8] | 1.028 [0.912, 1.053] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 322.8 [332.3] | 313.4 [331.5] | 0.971 [0.932, 1.059] | no detectable difference |
| scroll / mainJsMs | 220.2 [226.7] | 217.4 [227.9] | 0.987 [0.942, 1.067] | no detectable difference |
| jump / settleMs | 86.7 [90.6] | 88.1 [89.2] | +1.4 [-11.4, +2.8] | no detectable difference |
| jump / mainTaskMs | 54.6 [55.7] | 51.5 [51.6] | 0.943 [0.915, 0.959] | no detectable difference |
| jump / mainJsMs | 33.9 [35.0] | 32.6 [32.7] | 0.962 [0.927, 1.000] | no detectable difference |
| zoom / settleMs | 372.1 [382.0] | 365.4 [382.6] | -6.7 [-30.9, +29.4] | no detectable difference |
| zoom / zoom2SettleMs | 140.5 [143.3] | 138.8 [140.9] | -1.8 [-14.0, +8.1] | no detectable difference |
| zoom / zoom4SettleMs | 231.6 [238.7] | 226.7 [241.6] | -4.9 [-16.9, +21.2] | no detectable difference |
| zoom / backTo1SettleMs | 66.1 [69.2] | 64.3 [67.5] | -1.7 [-7.0, +7.6] | no detectable difference |
| zoom / mainTaskMs | 144.5 [149.6] | 147.8 [149.2] | 1.023 [0.971, 1.052] | no detectable difference |
| zoom / mainJsMs | 96.1 [97.9] | 97.1 [98.5] | 1.010 [0.952, 1.063] | no detectable difference |
| select / settleMs | 148.9 [151.0] | 150.1 [150.2] | +1.1 [-3.0, +2.3] | no detectable difference |
| select / mainTaskMs | 40.7 [43.5] | 45.6 [47.5] | 1.120 [0.913, 1.271] | no detectable difference |
| select / mainJsMs | 19.3 [22.1] | 21.2 [23.4] | 1.097 [0.927, 1.225] | no detectable difference |
| search / searchMs | 55.3 [63.7] | 53.1 [56.0] | 0.960 [0.754, 1.199] | no detectable difference |
| search / mainTaskMs | 2.4 [2.9] | 2.7 [3.0] | 1.121 [0.871, 1.294] | no detectable difference |
| search / mainJsMs | 1.2 [1.3] | 1.2 [1.3] | 0.992 [0.870, 1.228] | no detectable difference |
| annotate / settleMs | 160.5 [160.6] | 153.0 [160.4] | -7.6 [-8.7, +8.3] | no detectable difference |
| annotate / mainTaskMs | 49.5 [52.4] | 44.9 [53.0] | 0.908 [0.728, 1.181] | no detectable difference |
| annotate / mainJsMs | 23.8 [25.7] | 21.9 [25.2] | 0.917 [0.656, 1.155] | no detectable difference |

## `impl-o2` vs `impl-enginelink`

| Row | impl-o2 median [p95] | impl-enginelink median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.8 [53.2] | 52.1 [52.5] | 0.987 [0.958, 1.002] | no detectable difference |
| open(ebook) / openedMs | 116.7 [117.1] | 114.3 [117.7] | 0.979 [0.970, 1.022] | no detectable difference |
| open(ebook) / firstPaintMs | 216.4 [217.6] | 214.9 [215.6] | 0.993 [0.974, 1.011] | no detectable difference |
| open(ebook) / settledMs | 263.6 [265.6] | 259.9 [262.9] | 0.986 [0.973, 1.011] | no detectable difference |
| open(ebook) / mainTaskMs | 107.2 [107.7] | 104.2 [104.7] | 0.972 [0.949, 0.995] | no detectable difference |
| open(ebook) / mainJsMs | 83.6 [84.0] | 80.3 [81.0] | 0.961 [0.935, 1.006] | no detectable difference |
| open(large) / readyMs | 52.7 [54.3] | 52.1 [52.2] | 0.989 [0.954, 0.996] | no detectable difference |
| open(large) / openedMs | 139.5 [140.5] | 134.5 [136.7] | 0.965 [0.952, 1.001] | no detectable difference |
| open(large) / firstPaintMs | 223.6 [225.0] | 221.2 [223.7] | 0.989 [0.958, 1.031] | no detectable difference |
| open(large) / settledMs | 220.5 [221.3] | 224.3 [226.8] | 1.017 [0.954, 1.037] | no detectable difference |
| open(large) / mainTaskMs | 161.8 [163.0] | 162.3 [163.9] | 1.003 [0.985, 1.023] | no detectable difference |
| open(large) / mainJsMs | 135.0 [136.4] | 135.8 [137.4] | 1.006 [0.986, 1.024] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2006.2 [2013.2] | 2013.9 [2017.3] | 1.004 [0.984, 1.006] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.6 [8.8] | 8.4 [8.7] | 0.979 [0.950, 1.037] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 313.4 [331.5] | 319.5 [335.2] | 1.019 [0.942, 1.085] | no detectable difference |
| scroll / mainJsMs | 217.4 [227.9] | 220.8 [229.7] | 1.016 [0.933, 1.076] | no detectable difference |
| jump / settleMs | 88.1 [89.2] | 81.7 [88.3] | -6.5 [-15.8, +9.3] | no detectable difference |
| jump / mainTaskMs | 51.5 [51.6] | 52.8 [54.0] | 1.025 [0.943, 1.060] | no detectable difference |
| jump / mainJsMs | 32.6 [32.7] | 32.4 [33.4] | 0.994 [0.978, 1.028] | no detectable difference |
| zoom / settleMs | 365.4 [382.6] | 387.2 [388.9] | +21.8 [-6.1, +36.9] | no detectable difference |
| zoom / zoom2SettleMs | 138.8 [140.9] | 143.7 [145.9] | +4.9 [-8.9, +16.6] | no detectable difference |
| zoom / zoom4SettleMs | 226.7 [241.6] | 242.9 [253.7] | +16.2 [-8.6, +32.3] | no detectable difference |
| zoom / backTo1SettleMs | 64.3 [67.5] | 69.4 [72.3] | +5.1 [-6.9, +10.0] | no detectable difference |
| zoom / mainTaskMs | 147.8 [149.2] | 143.4 [156.9] | 0.970 [0.947, 1.086] | no detectable difference |
| zoom / mainJsMs | 97.1 [98.5] | 100.1 [106.6] | 1.031 [0.992, 1.149] | no detectable difference |
| select / settleMs | 150.1 [150.2] | 147.1 [152.4] | -3.0 [-3.5, +4.8] | no detectable difference |
| select / mainTaskMs | 45.6 [47.5] | 38.9 [39.2] | 0.855 [0.793, 0.980] | no detectable difference |
| select / mainJsMs | 21.2 [23.4] | 19.2 [19.9] | 0.906 [0.774, 0.961] | no detectable difference |
| search / searchMs | 53.1 [56.0] | 50.7 [52.7] | 0.954 [0.805, 1.086] | no detectable difference |
| search / mainTaskMs | 2.7 [3.0] | 2.5 [3.2] | 0.928 [0.787, 1.276] | no detectable difference |
| search / mainJsMs | 1.2 [1.3] | 1.2 [1.3] | 0.997 [0.815, 1.115] | no detectable difference |
| annotate / settleMs | 153.0 [160.4] | 153.3 [158.8] | +0.3 [-8.8, +7.6] | no detectable difference |
| annotate / mainTaskMs | 44.9 [53.0] | 50.0 [54.7] | 1.114 [0.872, 1.437] | no detectable difference |
| annotate / mainJsMs | 21.9 [25.2] | 24.9 [26.0] | 1.138 [0.870, 1.540] | no detectable difference |

## `plugins-fixed` vs `plugins-o2`

| Row | plugins-fixed median [p95] | plugins-o2 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 75.2 [77.0] | 78.0 [79.7] | 1.037 [0.983, 1.069] | no detectable difference |
| open(ebook) / openedMs | 177.5 [180.1] | 178.7 [179.8] | 1.007 [0.964, 1.029] | no detectable difference |
| open(ebook) / firstPaintMs | 305.6 [308.8] | 307.2 [308.0] | 1.005 [0.990, 1.025] | no detectable difference |
| open(ebook) / settledMs | 355.2 [358.4] | 355.3 [356.0] | 1.000 [0.986, 1.014] | no detectable difference |
| open(ebook) / mainTaskMs | 237.5 [238.8] | 238.6 [241.8] | 1.005 [0.980, 1.026] | no detectable difference |
| open(ebook) / mainJsMs | 198.9 [199.6] | 199.5 [201.7] | 1.003 [0.979, 1.023] | no detectable difference |
| open(large) / readyMs | 74.7 [75.0] | 75.4 [77.8] | 1.010 [0.977, 1.063] | no detectable difference |
| open(large) / openedMs | 201.4 [203.6] | 199.5 [202.0] | 0.990 [0.967, 1.006] | no detectable difference |
| open(large) / firstPaintMs | 334.7 [335.2] | 328.9 [337.3] | 0.983 [0.978, 1.026] | no detectable difference |
| open(large) / settledMs | 339.4 [339.9] | 337.3 [339.7] | 0.994 [0.973, 1.007] | no detectable difference |
| open(large) / mainTaskMs | 525.4 [527.1] | 519.4 [524.5] | 0.988 [0.968, 1.003] | no detectable difference |
| open(large) / mainJsMs | 489.2 [491.0] | 483.1 [486.7] | 0.987 [0.961, 0.998] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2219.2 [2256.5] | 2259.7 [2261.3] | 1.018 [0.929, 1.083] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.3 [9.5] | 9.6 [9.7] | 1.030 [0.911, 1.092] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 721.6 [759.3] | 756.6 [757.1] | 1.048 [0.804, 1.230] | no detectable difference |
| scroll / mainJsMs | 622.5 [655.0] | 651.1 [653.3] | 1.046 [0.792, 1.252] | no detectable difference |
| jump / settleMs | 130.4 [135.6] | 138.3 [144.0] | +7.9 [-8.4, +29.3] | no detectable difference |
| jump / mainTaskMs | 149.7 [151.3] | 146.4 [150.8] | 0.978 [0.942, 1.018] | no detectable difference |
| jump / mainJsMs | 133.9 [134.8] | 129.2 [134.6] | 0.965 [0.940, 1.031] | no detectable difference |
| zoom / settleMs | 426.4 [426.6] | 433.0 [437.6] | +6.6 [+2.6, +25.3] | no detectable difference |
| zoom / zoom2SettleMs | 151.1 [151.6] | 154.8 [158.3] | +3.7 [-1.1, +15.6] | no detectable difference |
| zoom / zoom4SettleMs | 274.8 [275.5] | 279.5 [282.1] | +4.7 [-1.1, +12.6] | no detectable difference |
| zoom / backTo1SettleMs | 85.7 [86.1] | 88.8 [102.7] | +3.1 [+1.0, +20.7] | no detectable difference |
| zoom / mainTaskMs | 274.3 [274.7] | 277.1 [285.5] | 1.010 [1.003, 1.060] | no detectable difference |
| zoom / mainJsMs | 232.0 [232.5] | 234.2 [234.6] | 1.010 [1.007, 1.040] | no detectable difference |
| select / settleMs | 141.6 [152.5] | 144.9 [150.0] | +3.3 [-12.0, +12.2] | no detectable difference |
| select / mainTaskMs | 65.1 [70.1] | 65.7 [71.1] | 1.009 [0.782, 1.265] | no detectable difference |
| select / mainJsMs | 51.2 [54.8] | 52.1 [54.5] | 1.017 [0.768, 1.354] | no detectable difference |
| search / searchMs | 69.2 [70.2] | 64.6 [67.6] | 0.932 [0.689, 0.988] | no detectable difference |
| search / mainTaskMs | 18.2 [18.4] | 16.3 [18.3] | 0.894 [0.765, 1.111] | no detectable difference |
| search / mainJsMs | 7.6 [8.1] | 8.0 [8.4] | 1.043 [0.943, 1.116] | no detectable difference |
| annotate / settleMs | 164.1 [165.0] | 165.4 [168.1] | +1.3 [-1.6, +6.3] | no detectable difference |
| annotate / mainTaskMs | 83.2 [91.1] | 81.7 [81.8] | 0.982 [0.878, 1.018] | no detectable difference |
| annotate / mainJsMs | 62.4 [67.2] | 60.7 [61.2] | 0.972 [0.891, 1.020] | no detectable difference |

## `plugins-o2` vs `plugins-enginelink`

| Row | plugins-o2 median [p95] | plugins-enginelink median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 78.0 [79.7] | 74.0 [74.8] | 0.949 [0.924, 0.987] | faster |
| open(ebook) / openedMs | 178.7 [179.8] | 171.3 [172.6] | 0.958 [0.941, 0.993] | faster |
| open(ebook) / firstPaintMs | 307.2 [308.0] | 292.8 [299.8] | 0.953 [0.950, 0.982] | no detectable difference |
| open(ebook) / settledMs | 355.3 [356.0] | 343.0 [348.3] | 0.966 [0.962, 0.987] | faster |
| open(ebook) / mainTaskMs | 238.6 [241.8] | 235.6 [236.5] | 0.987 [0.964, 1.011] | no detectable difference |
| open(ebook) / mainJsMs | 199.5 [201.7] | 197.6 [199.1] | 0.990 [0.975, 1.019] | no detectable difference |
| open(large) / readyMs | 75.4 [77.8] | 74.8 [75.8] | 0.992 [0.953, 1.034] | no detectable difference |
| open(large) / openedMs | 199.5 [202.0] | 197.5 [197.6] | 0.990 [0.976, 1.003] | no detectable difference |
| open(large) / firstPaintMs | 328.9 [337.3] | 327.3 [327.8] | 0.995 [0.966, 1.000] | no detectable difference |
| open(large) / settledMs | 337.3 [339.7] | 337.7 [337.8] | 1.001 [0.990, 1.021] | no detectable difference |
| open(large) / mainTaskMs | 519.4 [524.5] | 526.7 [528.3] | 1.014 [1.001, 1.036] | no detectable difference |
| open(large) / mainJsMs | 483.1 [486.7] | 490.5 [492.1] | 1.015 [1.002, 1.043] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2259.7 [2261.3] | 2221.3 [2227.9] | 0.983 [0.972, 1.061] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.6 [9.7] | 9.7 [9.7] | 1.004 [0.889, 1.117] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 756.6 [757.1] | 746.6 [747.1] | 0.987 [0.882, 1.218] | no detectable difference |
| scroll / mainJsMs | 651.1 [653.3] | 644.9 [645.8] | 0.990 [0.876, 1.239] | no detectable difference |
| jump / settleMs | 138.3 [144.0] | 114.8 [155.1] | -23.5 [-30.1, +31.7] | no detectable difference |
| jump / mainTaskMs | 146.4 [150.8] | 144.8 [147.3] | 0.990 [0.948, 1.035] | no detectable difference |
| jump / mainJsMs | 129.2 [134.6] | 130.8 [132.6] | 1.012 [0.966, 1.048] | no detectable difference |
| zoom / settleMs | 433.0 [437.6] | 429.2 [432.3] | -3.8 [-19.0, +3.5] | no detectable difference |
| zoom / zoom2SettleMs | 154.8 [158.3] | 151.5 [152.1] | -3.4 [-8.5, +1.6] | no detectable difference |
| zoom / zoom4SettleMs | 279.5 [282.1] | 277.8 [282.0] | -1.7 [-15.4, +8.1] | no detectable difference |
| zoom / backTo1SettleMs | 88.8 [102.7] | 84.2 [99.2] | -4.7 [-20.0, +13.8] | no detectable difference |
| zoom / mainTaskMs | 277.1 [285.5] | 272.5 [291.2] | 0.983 [0.945, 1.064] | no detectable difference |
| zoom / mainJsMs | 234.2 [234.6] | 229.0 [240.4] | 0.978 [0.962, 1.032] | no detectable difference |
| select / settleMs | 144.9 [150.0] | 144.4 [148.9] | -0.5 [-6.5, +7.8] | no detectable difference |
| select / mainTaskMs | 65.7 [71.1] | 67.4 [68.1] | 1.026 [0.908, 1.234] | no detectable difference |
| select / mainJsMs | 52.1 [54.5] | 53.9 [54.0] | 1.035 [0.936, 1.275] | no detectable difference |
| search / searchMs | 64.6 [67.6] | 66.6 [69.1] | 1.032 [0.893, 1.431] | no detectable difference |
| search / mainTaskMs | 16.3 [18.3] | 16.5 [19.6] | 1.014 [0.530, 1.416] | no detectable difference |
| search / mainJsMs | 8.0 [8.4] | 7.6 [8.4] | 0.958 [0.807, 1.102] | no detectable difference |
| annotate / settleMs | 165.4 [168.1] | 165.0 [165.2] | -0.4 [-5.3, +1.8] | no detectable difference |
| annotate / mainTaskMs | 81.7 [81.8] | 81.7 [84.7] | 1.000 [0.987, 1.053] | no detectable difference |
| annotate / mainJsMs | 60.7 [61.2] | 60.6 [63.5] | 0.999 [0.985, 1.059] | no detectable difference |

## Memory

| Row | main median [p95] | main-aa median [p95] | impl-enginelink median [p95] | plugins-enginelink median [p95] | impl-fixed median [p95] | plugins-fixed median [p95] | impl-o2 median [p95] | plugins-o2 median [p95] |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.35 MiB [34.36 MiB] | 34.34 MiB [34.35 MiB] | 34.61 MiB [34.61 MiB] | 42.12 MiB [42.12 MiB] | 34.94 MiB [34.94 MiB] | 41.44 MiB [41.46 MiB] | 34.92 MiB [35.82 MiB] | 41.41 MiB [42.32 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 22.77 MiB [22.77 MiB] | 22.77 MiB [22.77 MiB] | 23.06 MiB [23.06 MiB] | 23.06 MiB [23.06 MiB] | 23.03 MiB [23.03 MiB] | 23.03 MiB [23.03 MiB] |
| open(large) / heapAfterOpenBytes | 48.16 MiB [48.16 MiB] | 48.15 MiB [48.15 MiB] | 48.33 MiB [48.33 MiB] | 55.94 MiB [55.94 MiB] | 48.63 MiB [48.63 MiB] | 56.26 MiB [56.27 MiB] | 48.60 MiB [48.60 MiB] | 56.23 MiB [56.24 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.77 MiB [22.77 MiB] | 22.71 MiB [22.71 MiB] | 23.03 MiB [23.03 MiB] | 22.99 MiB [22.99 MiB] | 23.00 MiB [23.00 MiB] | 22.96 MiB [22.96 MiB] |
| soak / heapCycle5Bytes | 37.42 MiB [37.42 MiB] | 37.42 MiB [37.43 MiB] | 37.78 MiB [37.79 MiB] | 43.94 MiB [44.84 MiB] | 38.09 MiB [38.09 MiB] | 44.25 MiB [45.16 MiB] | 38.07 MiB [38.07 MiB] | 45.23 MiB [45.24 MiB] |
| soak / heapCycle30Bytes | 38.26 MiB [38.26 MiB] | 38.27 MiB [38.28 MiB] | 38.50 MiB [38.50 MiB] | 44.49 MiB [45.38 MiB] | 38.81 MiB [38.82 MiB] | 44.80 MiB [45.70 MiB] | 38.78 MiB [38.78 MiB] | 45.77 MiB [45.77 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-enginelink`:

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

`plugins-fixed`:

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

`plugins-o2`:

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

