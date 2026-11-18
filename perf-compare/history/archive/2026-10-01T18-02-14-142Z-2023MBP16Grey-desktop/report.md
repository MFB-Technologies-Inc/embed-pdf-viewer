# Viewer perf comparison: 2026-10-01T18-02-14-142Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | dce59fa9d1e7f2234995257e11aacfabcb937d98 |
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
| plugins-lazy | research/rust-kernel-lazy-state | f83e60654 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 5c2e682425eb |
| plugins-tip3 | research/multi-platform-rust-core-impl-plugins | 373b7f73b | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 5c2e682425eb |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-lazy`: pass
- `plugins-tip3`: pass

## Sizes

| Metric | main | main-aa | plugins-lazy | plugins-tip3 |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 7.94 MiB | 7.94 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.71 MiB | 1.71 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

50 of 51 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.5 [52.7] | 52.1 [52.6] | 0.993 [0.983, 1.013] | pass |
| open(ebook) / openedMs | 115.5 [115.8] | 114.9 [116.8] | 0.995 [0.991, 1.032] | pass |
| open(ebook) / firstPaintMs | 214.8 [216.1] | 212.2 [213.7] | 0.988 [0.981, 0.998] | pass |
| open(ebook) / settledMs | 336.0 [345.6] | 332.9 [333.8] | 0.991 [0.959, 0.998] | pass |
| open(ebook) / mainTaskMs | 118.4 [119.2] | 116.6 [121.6] | 0.985 [0.973, 1.054] | pass |
| open(ebook) / mainJsMs | 92.5 [93.2] | 90.6 [95.1] | 0.980 [0.967, 1.060] | pass |
| open(large) / readyMs | 51.5 [54.2] | 52.8 [52.9] | 1.024 [0.941, 1.037] | pass |
| open(large) / openedMs | 133.6 [135.6] | 133.2 [133.5] | 0.997 [0.975, 1.016] | pass |
| open(large) / firstPaintMs | 220.3 [226.0] | 225.3 [225.6] | 1.023 [0.969, 1.033] | pass |
| open(large) / settledMs | 217.2 [222.8] | 222.4 [222.5] | 1.024 [0.970, 1.034] | pass |
| open(large) / mainTaskMs | 260.3 [262.6] | 260.8 [262.5] | 1.002 [0.986, 1.010] | pass |
| open(large) / mainJsMs | 237.2 [239.4] | 237.1 [239.4] | 1.000 [0.985, 1.012] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2001.9 [2009.6] | 2014.9 [2016.5] | 1.006 [1.002, 1.009] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [8.5] | 8.9 [9.2] | 1.060 [0.991, 1.091] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 417.0 [417.9] | 408.3 [420.7] | 0.979 [0.959, 1.053] | pass |
| scroll / mainJsMs | 320.5 [322.1] | 315.3 [325.8] | 0.984 [0.965, 1.056] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2017.0 [2022.5] | 2018.2 [2019.9] | 1.001 [0.992, 1.004] | pass |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| coldScroll / frameP95Ms | 8.5 [8.6] | 8.5 [8.6] | 1.001 [0.976, 1.019] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 456.7 [460.6] | 446.0 [449.7] | 0.977 [0.967, 1.018] | pass |
| coldScroll / mainJsMs | 328.8 [331.2] | 323.6 [324.8] | 0.984 [0.963, 1.026] | pass |
| jump / settleMs | 96.3 [96.5] | 90.3 [90.9] | -6.0 [-8.3, +1.8] | pass |
| jump / mainTaskMs | 54.6 [58.0] | 52.3 [53.6] | 0.958 [0.877, 1.006] | pass |
| jump / mainJsMs | 42.3 [43.6] | 38.8 [40.0] | 0.916 [0.862, 0.973] | A/A failed |
| zoom / settleMs | 352.4 [360.4] | 343.5 [359.2] | -8.9 [-23.5, +17.8] | pass |
| zoom / zoom2SettleMs | 130.7 [131.5] | 131.6 [133.0] | +0.9 [-7.0, +9.0] | pass |
| zoom / zoom4SettleMs | 228.2 [229.6] | 213.2 [226.3] | -15.0 [-17.8, +15.3] | pass |
| zoom / backTo1SettleMs | 79.6 [86.3] | 61.5 [75.4] | -18.1 [-26.9, +8.6] | pass |
| zoom / mainTaskMs | 167.7 [175.8] | 166.6 [170.6] | 0.993 [0.937, 1.045] | pass |
| zoom / mainJsMs | 119.8 [128.7] | 120.7 [121.0] | 1.007 [0.921, 1.013] | pass |
| select / settleMs | 149.3 [158.3] | 147.3 [148.4] | -2.1 [-12.6, +1.8] | pass |
| select / mainTaskMs | 43.5 [48.1] | 38.9 [43.4] | 0.896 [0.760, 1.048] | pass |
| select / mainJsMs | 26.7 [30.0] | 23.2 [26.9] | 0.871 [0.759, 1.071] | pass |
| search / searchMs | 57.7 [61.6] | 54.2 [59.3] | 0.941 [0.839, 1.039] | pass |
| search / mainTaskMs | 4.5 [4.8] | 4.4 [4.7] | 0.981 [0.874, 1.088] | pass |
| search / mainJsMs | 3.1 [3.1] | 3.0 [3.2] | 0.967 [0.957, 1.041] | pass |
| searchLarge / searchMs | 1650.1 [1657.0] | 1726.5 [1728.1] | 1.046 [0.967, 1.074] | pass |
| searchLarge / mainTaskMs | 267.8 [268.1] | 275.3 [276.9] | 1.028 [0.977, 1.050] | pass |
| searchLarge / mainJsMs | 230.7 [233.1] | 237.6 [240.1] | 1.030 [0.975, 1.053] | pass |
| annotate / settleMs | 153.7 [158.9] | 155.7 [158.6] | +2.0 [-7.3, +5.8] | pass |
| annotate / mainTaskMs | 53.1 [55.3] | 57.1 [59.6] | 1.075 [0.932, 1.132] | pass |
| annotate / mainJsMs | 31.6 [32.8] | 33.2 [35.5] | 1.050 [0.942, 1.144] | pass |

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

Not run: `impl-head`, `impl-enginelink`.

## `plugins-tip` vs `plugins-enginelink`

Not run: `plugins-tip`, `plugins-enginelink`.

## `impl-head` vs `impl-fixed`

Not run: `impl-head`, `impl-fixed`.

## `plugins-tip` vs `plugins-fixed`

Not run: `plugins-tip`, `plugins-fixed`.

## `impl-fixed` vs `impl-enginelink`

Not run: `impl-fixed`, `impl-enginelink`.

## `plugins-fixed` vs `plugins-enginelink`

Not run: `plugins-fixed`, `plugins-enginelink`.

## `impl-fixed` vs `impl-o2`

Not run: `impl-fixed`, `impl-o2`.

## `impl-o2` vs `impl-enginelink`

Not run: `impl-o2`, `impl-enginelink`.

## `plugins-fixed` vs `plugins-o2`

Not run: `plugins-fixed`, `plugins-o2`.

## `plugins-o2` vs `plugins-enginelink`

Not run: `plugins-o2`, `plugins-enginelink`.

## `main` vs `main-compat`

Not run: `main-compat`.

## `impl-fixed` vs `impl-compat`

Not run: `impl-fixed`, `impl-compat`.

## `plugins-fixed` vs `plugins-compat`

Not run: `plugins-fixed`, `plugins-compat`.

## `plugins-now` vs `plugins-slicetext`

Not run: `plugins-now`, `plugins-slicetext`.

## `main` vs `plugins-slicetext`

Not run: `plugins-slicetext`.

## `impl-now` vs `impl-glyphview`

Not run: `impl-now`, `impl-glyphview`.

## `plugins-tip2` vs `plugins-glyphview`

Not run: `plugins-tip2`, `plugins-glyphview`.

## `main` vs `impl-glyphview`

Not run: `impl-glyphview`.

## `plugins-tip2` vs `plugins-lazy`

Not run: `plugins-tip2`.

## `main` vs `plugins-lazy`

| Row | main median [p95] | plugins-lazy median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.5 [52.7] | 75.9 [77.7] | 1.447 [1.402, 1.497] | slower |
| open(ebook) / openedMs | 115.5 [115.8] | 173.1 [174.2] | 1.499 [1.486, 1.537] | slower |
| open(ebook) / firstPaintMs | 214.8 [216.1] | 297.3 [298.8] | 1.384 [1.352, 1.395] | slower |
| open(ebook) / settledMs | 336.0 [345.6] | 346.6 [351.2] | 1.031 [0.981, 1.052] | no detectable difference |
| open(ebook) / mainTaskMs | 118.4 [119.2] | 237.3 [238.0] | 2.005 [1.935, 2.054] | slower |
| open(ebook) / mainJsMs | 92.5 [93.2] | 198.1 [198.9] | 2.142 [2.068, 2.208] | slower |
| open(large) / readyMs | 51.5 [54.2] | 75.4 [75.4] | 1.463 [1.374, 1.478] | slower |
| open(large) / openedMs | 133.6 [135.6] | 198.7 [200.0] | 1.488 [1.455, 1.523] | slower |
| open(large) / firstPaintMs | 220.3 [226.0] | 326.0 [327.5] | 1.480 [1.431, 1.500] | slower |
| open(large) / settledMs | 217.2 [222.8] | 324.1 [329.2] | 1.492 [1.450, 1.532] | slower |
| open(large) / mainTaskMs | 260.3 [262.6] | 507.1 [507.4] | 1.948 [1.923, 1.952] | slower |
| open(large) / mainJsMs | 237.2 [239.4] | 468.9 [470.2] | 1.977 [1.951, 1.986] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2001.9 [2009.6] | 2268.7 [2272.7] | 1.133 [1.095, 1.137] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.5] | 9.5 [9.5] | 1.128 [1.049, 1.131] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 417.0 [417.9] | 751.5 [752.5] | 1.802 [1.794, 1.878] | slower |
| scroll / mainJsMs | 320.5 [322.1] | 645.8 [647.5] | 2.015 [2.000, 2.092] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2017.0 [2022.5] | 2207.8 [2257.9] | 1.095 [1.082, 1.125] | slower |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 8.5 [8.6] | 9.7 [10.0] | 1.133 [1.088, 1.196] | slower |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 456.7 [460.6] | 749.8 [784.8] | 1.642 [1.554, 1.784] | slower |
| coldScroll / mainJsMs | 328.8 [331.2] | 629.8 [664.3] | 1.916 [1.809, 2.109] | slower |
| jump / settleMs | 96.3 [96.5] | 120.0 [148.0] | +23.8 [+21.0, +62.0] | slower |
| jump / mainTaskMs | 54.6 [58.0] | 145.3 [150.3] | 2.664 [2.470, 2.823] | slower |
| jump / mainJsMs | 42.3 [43.6] | 133.8 [137.7] | 3.163 [3.050, 3.343] | unreliable (A/A failed) |
| zoom / settleMs | 352.4 [360.4] | 383.7 [395.6] | +31.3 [+16.0, +53.8] | slower |
| zoom / zoom2SettleMs | 130.7 [131.5] | 141.9 [141.9] | +11.2 [+8.8, +17.7] | slower |
| zoom / zoom4SettleMs | 228.2 [229.6] | 241.9 [253.7] | +13.6 [+7.1, +42.6] | no detectable difference |
| zoom / backTo1SettleMs | 79.6 [86.3] | 83.3 [95.7] | +3.7 [-5.4, +28.7] | no detectable difference |
| zoom / mainTaskMs | 167.7 [175.8] | 269.0 [273.1] | 1.604 [1.463, 1.672] | slower |
| zoom / mainJsMs | 119.8 [128.7] | 228.1 [228.6] | 1.903 [1.645, 1.913] | slower |
| select / settleMs | 149.3 [158.3] | 143.6 [147.1] | -5.7 [-18.5, +0.8] | no detectable difference |
| select / mainTaskMs | 43.5 [48.1] | 64.4 [64.9] | 1.481 [1.318, 1.550] | slower |
| select / mainJsMs | 26.7 [30.0] | 49.9 [50.2] | 1.870 [1.635, 1.969] | slower |
| search / searchMs | 57.7 [61.6] | 63.4 [66.7] | 1.100 [0.825, 1.164] | no detectable difference |
| search / mainTaskMs | 4.5 [4.8] | 8.9 [16.5] | 1.957 [1.558, 3.991] | slower |
| search / mainJsMs | 3.1 [3.1] | 6.5 [6.8] | 2.103 [1.676, 2.242] | slower |
| searchLarge / searchMs | 1650.1 [1657.0] | 2161.4 [2175.7] | 1.310 [1.300, 1.353] | slower |
| searchLarge / mainTaskMs | 267.8 [268.1] | 1017.3 [1031.2] | 3.798 [3.781, 3.915] | slower |
| searchLarge / mainJsMs | 230.7 [233.1] | 978.5 [991.6] | 4.241 [4.175, 4.350] | slower |
| annotate / settleMs | 153.7 [158.9] | 165.1 [167.3] | +11.4 [-2.6, +14.4] | no detectable difference |
| annotate / mainTaskMs | 53.1 [55.3] | 74.5 [76.7] | 1.401 [1.336, 1.455] | slower |
| annotate / mainJsMs | 31.6 [32.8] | 54.2 [54.6] | 1.714 [1.642, 1.749] | slower |

## `plugins-lazy` vs `plugins-tip3`

| Row | plugins-lazy median [p95] | plugins-tip3 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 75.9 [77.7] | 76.0 [76.1] | 1.002 [0.948, 1.029] | no detectable difference |
| open(ebook) / openedMs | 173.1 [174.2] | 173.4 [178.1] | 1.002 [0.992, 1.037] | no detectable difference |
| open(ebook) / firstPaintMs | 297.3 [298.8] | 299.7 [300.9] | 1.008 [0.984, 1.030] | no detectable difference |
| open(ebook) / settledMs | 346.6 [351.2] | 347.2 [347.2] | 1.002 [0.970, 1.021] | no detectable difference |
| open(ebook) / mainTaskMs | 237.3 [238.0] | 232.4 [235.1] | 0.979 [0.968, 1.020] | no detectable difference |
| open(ebook) / mainJsMs | 198.1 [198.9] | 194.2 [196.5] | 0.980 [0.965, 1.020] | no detectable difference |
| open(large) / readyMs | 75.4 [75.4] | 74.6 [75.4] | 0.989 [0.988, 1.008] | no detectable difference |
| open(large) / openedMs | 198.7 [200.0] | 198.0 [199.2] | 0.996 [0.986, 1.009] | no detectable difference |
| open(large) / firstPaintMs | 326.0 [327.5] | 324.1 [326.5] | 0.994 [0.989, 1.008] | no detectable difference |
| open(large) / settledMs | 324.1 [329.2] | 331.1 [332.8] | 1.022 [0.981, 1.029] | no detectable difference |
| open(large) / mainTaskMs | 507.1 [507.4] | 506.6 [510.6] | 0.999 [0.991, 1.011] | no detectable difference |
| open(large) / mainJsMs | 468.9 [470.2] | 471.1 [474.8] | 1.005 [0.991, 1.016] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2268.7 [2272.7] | 2261.2 [2308.8] | 0.997 [0.980, 1.051] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.001] | no detectable difference |
| scroll / frameP95Ms | 9.5 [9.5] | 9.6 [9.6] | 1.011 [0.988, 1.079] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 751.5 [752.5] | 763.0 [769.2] | 1.015 [0.973, 1.027] | no detectable difference |
| scroll / mainJsMs | 645.8 [647.5] | 653.1 [661.8] | 1.011 [0.975, 1.028] | no detectable difference |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2207.8 [2257.9] | 2226.6 [2282.1] | 1.009 [0.958, 1.045] | no detectable difference |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 9.7 [10.0] | 9.6 [9.8] | 0.993 [0.940, 1.043] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 749.8 [784.8] | 755.6 [766.2] | 1.008 [0.908, 1.071] | no detectable difference |
| coldScroll / mainJsMs | 629.8 [664.3] | 636.7 [645.1] | 1.011 [0.901, 1.077] | no detectable difference |
| jump / settleMs | 120.0 [148.0] | 123.8 [152.9] | +3.7 [-27.7, +38.6] | no detectable difference |
| jump / mainTaskMs | 145.3 [150.3] | 151.8 [152.6] | 1.045 [0.997, 1.059] | no detectable difference |
| jump / mainJsMs | 133.8 [137.7] | 139.4 [139.5] | 1.041 [0.998, 1.047] | unreliable (A/A failed) |
| zoom / settleMs | 383.7 [395.6] | 387.6 [393.0] | +3.9 [-21.2, +16.4] | no detectable difference |
| zoom / zoom2SettleMs | 141.9 [141.9] | 142.1 [151.8] | +0.3 [-1.8, +12.4] | no detectable difference |
| zoom / zoom4SettleMs | 241.9 [253.7] | 240.8 [246.8] | -1.0 [-21.4, +10.6] | no detectable difference |
| zoom / backTo1SettleMs | 83.3 [95.7] | 85.1 [85.4] | +1.8 [-19.1, +3.9] | no detectable difference |
| zoom / mainTaskMs | 269.0 [273.1] | 263.6 [266.5] | 0.980 [0.940, 1.032] | no detectable difference |
| zoom / mainJsMs | 228.1 [228.6] | 221.3 [221.3] | 0.970 [0.949, 1.037] | no detectable difference |
| select / settleMs | 143.6 [147.1] | 145.1 [150.7] | +1.5 [-3.6, +10.5] | no detectable difference |
| select / mainTaskMs | 64.4 [64.9] | 59.3 [63.9] | 0.921 [0.804, 1.004] | no detectable difference |
| select / mainJsMs | 49.9 [50.2] | 46.3 [49.0] | 0.928 [0.774, 0.993] | no detectable difference |
| search / searchMs | 63.4 [66.7] | 60.5 [63.6] | 0.955 [0.767, 1.249] | no detectable difference |
| search / mainTaskMs | 8.9 [16.5] | 9.4 [12.7] | 1.066 [0.497, 1.735] | no detectable difference |
| search / mainJsMs | 6.5 [6.8] | 6.2 [6.9] | 0.953 [0.861, 1.348] | no detectable difference |
| searchLarge / searchMs | 2161.4 [2175.7] | 1866.5 [1871.5] | 0.864 [0.855, 0.869] | faster |
| searchLarge / mainTaskMs | 1017.3 [1031.2] | 1026.7 [1028.4] | 1.009 [0.989, 1.015] | no detectable difference |
| searchLarge / mainJsMs | 978.5 [991.6] | 989.0 [990.4] | 1.011 [0.991, 1.016] | no detectable difference |
| annotate / settleMs | 165.1 [167.3] | 166.7 [170.3] | +1.6 [-5.7, +13.8] | no detectable difference |
| annotate / mainTaskMs | 74.5 [76.7] | 76.8 [78.3] | 1.031 [0.957, 1.057] | no detectable difference |
| annotate / mainJsMs | 54.2 [54.6] | 54.9 [57.6] | 1.013 [0.985, 1.071] | no detectable difference |

## `main` vs `plugins-tip3`

| Row | main median [p95] | plugins-tip3 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.5 [52.7] | 76.0 [76.1] | 1.450 [1.400, 1.463] | slower |
| open(ebook) / openedMs | 115.5 [115.8] | 173.4 [178.1] | 1.501 [1.493, 1.575] | slower |
| open(ebook) / firstPaintMs | 214.8 [216.1] | 299.7 [300.9] | 1.395 [1.360, 1.405] | slower |
| open(ebook) / settledMs | 336.0 [345.6] | 347.2 [347.2] | 1.033 [0.984, 1.038] | no detectable difference |
| open(ebook) / mainTaskMs | 118.4 [119.2] | 232.4 [235.1] | 1.963 [1.931, 2.031] | slower |
| open(ebook) / mainJsMs | 92.5 [93.2] | 194.2 [196.5] | 2.100 [2.059, 2.183] | slower |
| open(large) / readyMs | 51.5 [54.2] | 74.6 [75.4] | 1.448 [1.366, 1.479] | slower |
| open(large) / openedMs | 133.6 [135.6] | 198.0 [199.2] | 1.482 [1.454, 1.516] | slower |
| open(large) / firstPaintMs | 220.3 [226.0] | 324.1 [326.5] | 1.471 [1.430, 1.497] | slower |
| open(large) / settledMs | 217.2 [222.8] | 331.1 [332.8] | 1.524 [1.448, 1.548] | slower |
| open(large) / mainTaskMs | 260.3 [262.6] | 506.6 [510.6] | 1.946 [1.913, 1.966] | slower |
| open(large) / mainJsMs | 237.2 [239.4] | 471.1 [474.8] | 1.986 [1.946, 2.007] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2001.9 [2009.6] | 2261.2 [2308.8] | 1.130 [1.108, 1.158] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.5] | 9.6 [9.6] | 1.140 [1.108, 1.142] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 417.0 [417.9] | 763.0 [769.2] | 1.830 [1.753, 1.922] | slower |
| scroll / mainJsMs | 320.5 [322.1] | 653.1 [661.8] | 2.038 [1.960, 2.141] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2017.0 [2022.5] | 2226.6 [2282.1] | 1.104 [1.071, 1.137] | slower |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 8.5 [8.6] | 9.6 [9.8] | 1.125 [1.093, 1.167] | slower |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 456.7 [460.6] | 755.6 [766.2] | 1.654 [1.554, 1.735] | slower |
| coldScroll / mainJsMs | 328.8 [331.2] | 636.7 [645.1] | 1.937 [1.815, 2.039] | slower |
| jump / settleMs | 96.3 [96.5] | 123.8 [152.9] | +27.5 [+26.8, +67.1] | slower |
| jump / mainTaskMs | 54.6 [58.0] | 151.8 [152.6] | 2.783 [2.578, 2.857] | slower |
| jump / mainJsMs | 42.3 [43.6] | 139.4 [139.5] | 3.294 [3.152, 3.378] | unreliable (A/A failed) |
| zoom / settleMs | 352.4 [360.4] | 387.6 [393.0] | +35.2 [+14.5, +50.5] | slower |
| zoom / zoom2SettleMs | 130.7 [131.5] | 142.1 [151.8] | +11.5 [+8.5, +28.7] | slower |
| zoom / zoom4SettleMs | 228.2 [229.6] | 240.8 [246.8] | +12.6 [+3.9, +35.0] | no detectable difference |
| zoom / backTo1SettleMs | 79.6 [86.3] | 85.1 [85.4] | +5.5 [-9.1, +17.1] | no detectable difference |
| zoom / mainTaskMs | 167.7 [175.8] | 263.6 [266.5] | 1.571 [1.455, 1.631] | slower |
| zoom / mainJsMs | 119.8 [128.7] | 221.3 [221.3] | 1.847 [1.673, 1.852] | slower |
| select / settleMs | 149.3 [158.3] | 145.1 [150.7] | -4.2 [-15.4, +4.7] | no detectable difference |
| select / mainTaskMs | 43.5 [48.1] | 59.3 [63.9] | 1.365 [1.073, 1.538] | slower |
| select / mainJsMs | 26.7 [30.0] | 46.3 [49.0] | 1.735 [1.281, 1.932] | slower |
| search / searchMs | 57.7 [61.6] | 60.5 [63.6] | 1.050 [0.829, 1.110] | no detectable difference |
| search / mainTaskMs | 4.5 [4.8] | 9.4 [12.7] | 2.086 [1.782, 3.007] | slower |
| search / mainJsMs | 3.1 [3.1] | 6.2 [6.9] | 2.004 [1.909, 2.284] | slower |
| searchLarge / searchMs | 1650.1 [1657.0] | 1866.5 [1871.5] | 1.131 [1.123, 1.164] | slower |
| searchLarge / mainTaskMs | 267.8 [268.1] | 1026.7 [1028.4] | 3.833 [3.807, 3.900] | slower |
| searchLarge / mainJsMs | 230.7 [233.1] | 989.0 [990.4] | 4.287 [4.218, 4.339] | slower |
| annotate / settleMs | 153.7 [158.9] | 166.7 [170.3] | +13.0 [+2.3, +17.5] | slower |
| annotate / mainTaskMs | 53.1 [55.3] | 76.8 [78.3] | 1.445 [1.324, 1.485] | slower |
| annotate / mainJsMs | 31.6 [32.8] | 54.9 [57.6] | 1.737 [1.638, 1.849] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-lazy median [p95] | plugins-tip3 median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.35 MiB [34.35 MiB] | 34.34 MiB [34.35 MiB] | 41.34 MiB [41.34 MiB] | 41.33 MiB [41.33 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.03 MiB [23.03 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.15 MiB] | 48.14 MiB [48.15 MiB] | 57.60 MiB [57.61 MiB] | 57.60 MiB [57.61 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 23.00 MiB [23.00 MiB] | 23.00 MiB [23.01 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.43 MiB] | 37.41 MiB [37.42 MiB] | 44.14 MiB [44.15 MiB] | 44.14 MiB [44.15 MiB] |
| soak / heapCycle30Bytes | 38.26 MiB [38.27 MiB] | 38.26 MiB [38.27 MiB] | 44.70 MiB [44.70 MiB] | 44.69 MiB [44.70 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-lazy`:

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

`plugins-tip3`:

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

