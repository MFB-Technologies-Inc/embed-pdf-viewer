# Viewer perf comparison: 2026-10-06T22-32-02-965Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 61027635dbe6511c6053f5660e76acaef4a19a29 |
| Chromium / Playwright | 140.0.7339.16 / 1.55.0 |
| Node | v24.20.0 |
| OS | Darwin 27.2.0 arm64 |
| CPU | Apple M1, 8 cores |
| Memory | 16 GiB |
| Power | AC Power |
| Viewport | 1280x800 @2x |
| Fixtures | ebook 2e539f62b5fa, large 10c8668fe55c |

| Arm | Ref | SHA | Payload | Engine wasm sha256 | Kernel wasm sha256 |
| --- | --- | --- | --- | --- | --- |
| main | main | 2516e2786 | fetched | 98c2368a774d |  |
| main-aa | main | 2516e2786 | fetched | 98c2368a774d |  |
| plugins-tip5 | research/multi-platform-rust-core-impl-plugins | a8252d0ea | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | aee9b9460008 |
| plugins-coalesce | research/rust-kernel-coalesce-notify | c0fcd2746 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | aee9b9460008 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-tip5`: pass
- `plugins-coalesce`: pass

## Sizes

| Metric | main | main-aa | plugins-tip5 | plugins-coalesce |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 8.04 MiB | 8.04 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.72 MiB | 1.72 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

63 of 63 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 62.5 [65.7] | 63.6 [70.9] | 1.017 [0.957, 1.053] | pass |
| open(ebook) / openedMs | 137.5 [148.4] | 136.8 [143.5] | 0.995 [0.960, 1.012] | pass |
| open(ebook) / firstPaintMs | 256.6 [275.3] | 254.4 [263.2] | 0.992 [0.956, 1.037] | pass |
| open(ebook) / settledMs | 396.4 [406.9] | 392.4 [401.1] | 0.990 [0.974, 1.011] | pass |
| open(ebook) / mainTaskMs | 143.1 [147.8] | 142.0 [147.2] | 0.992 [0.963, 1.026] | pass |
| open(ebook) / mainJsMs | 110.7 [114.4] | 109.2 [113.2] | 0.986 [0.961, 1.027] | pass |
| open(large) / readyMs | 61.9 [70.3] | 61.4 [68.3] | 0.991 [0.921, 1.047] | pass |
| open(large) / openedMs | 158.5 [164.1] | 158.9 [164.2] | 1.002 [0.992, 1.019] | pass |
| open(large) / firstPaintMs | 265.4 [269.8] | 265.1 [280.5] | 0.999 [0.964, 1.016] | pass |
| open(large) / settledMs | 264.9 [281.4] | 265.8 [283.2] | 1.003 [0.981, 1.054] | pass |
| open(large) / mainTaskMs | 314.6 [318.0] | 314.6 [321.4] | 1.000 [0.991, 1.010] | pass |
| open(large) / mainJsMs | 283.2 [286.7] | 283.6 [289.9] | 1.001 [0.993, 1.008] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2880.5 [3003.1] | 2854.3 [2984.7] | 0.991 [0.960, 1.013] | pass |
| scroll / frameP50Ms | 11.1 [11.6] | 10.9 [11.6] | 0.986 [0.958, 1.001] | pass |
| scroll / frameP95Ms | 23.9 [27.2] | 23.1 [25.8] | 0.966 [0.839, 1.080] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 548.2 [564.2] | 543.8 [565.4] | 0.992 [0.966, 1.021] | pass |
| scroll / mainJsMs | 412.1 [424.4] | 408.0 [423.2] | 0.990 [0.962, 1.022] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2860.3 [2976.0] | 2816.3 [2875.1] | 0.985 [0.968, 1.002] | pass |
| coldScroll / frameP50Ms | 11.0 [11.5] | 11.1 [11.6] | 1.003 [0.982, 1.032] | pass |
| coldScroll / frameP95Ms | 24.4 [27.0] | 25.3 [27.2] | 1.038 [0.932, 1.147] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 570.2 [584.2] | 561.7 [583.0] | 0.985 [0.957, 1.005] | pass |
| coldScroll / mainJsMs | 400.8 [413.2] | 395.4 [407.7] | 0.986 [0.959, 1.001] | pass |
| jump / settleMs | 123.0 [132.6] | 124.0 [135.9] | +1.0 [-7.1, +10.5] | pass |
| jump / mainTaskMs | 73.2 [78.5] | 73.3 [77.1] | 1.000 [0.959, 1.032] | pass |
| jump / mainJsMs | 55.3 [59.0] | 53.5 [57.3] | 0.968 [0.897, 1.007] | pass |
| zoom / settleMs | 457.8 [468.9] | 448.0 [462.4] | -9.8 [-15.3, +16.3] | pass |
| zoom / zoom2SettleMs | 162.2 [171.0] | 165.3 [169.5] | +3.1 [-0.7, +7.5] | pass |
| zoom / zoom4SettleMs | 287.3 [306.6] | 286.1 [297.1] | -1.2 [-19.7, +15.6] | pass |
| zoom / backTo1SettleMs | 91.0 [100.3] | 87.1 [89.6] | -3.9 [-8.2, +4.9] | pass |
| zoom / mainTaskMs | 214.6 [229.4] | 219.3 [228.4] | 1.022 [0.985, 1.045] | pass |
| zoom / mainJsMs | 155.1 [165.3] | 157.2 [163.8] | 1.014 [0.994, 1.048] | pass |
| zoomLarge / settleMs | 298.8 [314.9] | 294.9 [303.1] | -3.9 [-17.0, +11.2] | pass |
| zoomLarge / zoom2SettleMs | 144.0 [165.8] | 136.1 [157.4] | -7.9 [-22.5, +0.1] | pass |
| zoomLarge / zoom4SettleMs | 148.7 [171.9] | 157.3 [167.4] | +8.6 [-6.7, +20.9] | pass |
| zoomLarge / backTo1SettleMs | 76.5 [90.6] | 80.2 [91.2] | +3.8 [-9.0, +10.3] | pass |
| zoomLarge / mainTaskMs | 180.9 [186.4] | 176.8 [182.5] | 0.977 [0.936, 1.027] | pass |
| zoomLarge / mainJsMs | 135.1 [139.8] | 131.0 [136.1] | 0.970 [0.928, 1.018] | pass |
| select / settleMs | 220.3 [249.4] | 226.0 [259.5] | +5.7 [-33.5, +31.8] | pass |
| select / mainTaskMs | 60.6 [66.9] | 61.3 [66.5] | 1.012 [0.939, 1.070] | pass |
| select / mainJsMs | 39.3 [43.3] | 39.5 [44.2] | 1.007 [0.939, 1.090] | pass |
| selectLarge / settleMs | 207.2 [250.9] | 202.7 [223.1] | -4.5 [-38.3, +17.8] | pass |
| selectLarge / mainTaskMs | 69.3 [73.6] | 70.4 [73.6] | 1.016 [0.955, 1.078] | pass |
| selectLarge / mainJsMs | 46.6 [49.7] | 47.4 [49.4] | 1.016 [0.952, 1.076] | pass |
| search / searchMs | 68.8 [78.6] | 73.1 [80.1] | 1.063 [0.927, 1.138] | pass |
| search / mainTaskMs | 5.3 [5.9] | 5.3 [6.5] | 1.003 [0.927, 1.132] | pass |
| search / mainJsMs | 3.7 [3.9] | 3.7 [4.1] | 1.006 [0.960, 1.064] | pass |
| searchLarge / searchMs | 1892.1 [1949.2] | 1882.0 [1923.8] | 0.995 [0.972, 1.004] | pass |
| searchLarge / mainTaskMs | 345.0 [348.9] | 342.9 [346.4] | 0.994 [0.983, 1.003] | pass |
| searchLarge / mainJsMs | 291.5 [293.8] | 290.3 [293.4] | 0.996 [0.991, 1.006] | pass |
| annotate / settleMs | 207.9 [275.3] | 200.9 [274.9] | -7.0 [-42.7, +46.5] | pass |
| annotate / mainTaskMs | 78.4 [89.7] | 76.2 [86.9] | 0.971 [0.895, 1.107] | pass |
| annotate / mainJsMs | 46.9 [54.4] | 45.7 [52.4] | 0.974 [0.912, 1.107] | pass |
| annotateLarge / settleMs | 230.2 [260.8] | 189.6 [262.1] | -40.7 [-63.6, +5.8] | pass |
| annotateLarge / mainTaskMs | 83.2 [88.9] | 79.8 [89.9] | 0.960 [0.931, 1.017] | pass |
| annotateLarge / mainJsMs | 50.2 [53.6] | 48.3 [54.7] | 0.961 [0.917, 1.019] | pass |

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

Not run: `plugins-tip2`, `plugins-lazy`.

## `main` vs `plugins-lazy`

Not run: `plugins-lazy`.

## `plugins-lazy` vs `plugins-tip3`

Not run: `plugins-lazy`, `plugins-tip3`.

## `main` vs `plugins-tip3`

Not run: `plugins-tip3`.

## `plugins-tip3` vs `plugins-sharedhits`

Not run: `plugins-tip3`, `plugins-sharedhits`.

## `main` vs `plugins-sharedhits`

Not run: `plugins-sharedhits`.

## `plugins-sharedhits` vs `plugins-promise`

Not run: `plugins-sharedhits`, `plugins-promise`.

## `plugins-promise` vs `plugins-typed`

Not run: `plugins-promise`, `plugins-typed`.

## `plugins-sharedhits` vs `plugins-typed`

Not run: `plugins-sharedhits`, `plugins-typed`.

## `main` vs `plugins-typed`

Not run: `plugins-typed`.

## `plugins-typed` vs `plugins-fixes`

Not run: `plugins-typed`, `plugins-fixes`.

## `plugins-fixes` vs `plugins-encode`

Not run: `plugins-fixes`, `plugins-encode`.

## `main` vs `plugins-encode`

Not run: `plugins-encode`.

## `plugins-tip4` vs `plugins-hostbytes`

Not run: `plugins-tip4`, `plugins-hostbytes`.

## `main` vs `plugins-hostbytes`

Not run: `plugins-hostbytes`.

## `plugins-tip5` vs `plugins-geomdecode`

Not run: `plugins-geomdecode`.

## `main` vs `plugins-geomdecode`

Not run: `plugins-geomdecode`.

## `plugins-tip5` vs `plugins-coalesce`

| Row | plugins-tip5 median [p95] | plugins-coalesce median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 93.4 [105.0] | 89.9 [100.6] | 0.962 [0.873, 1.027] | no detectable difference |
| open(ebook) / openedMs | 182.5 [196.1] | 177.2 [189.0] | 0.971 [0.923, 1.010] | no detectable difference |
| open(ebook) / firstPaintMs | 343.2 [360.6] | 333.9 [348.0] | 0.973 [0.944, 1.004] | no detectable difference |
| open(ebook) / settledMs | 405.7 [423.9] | 398.2 [414.2] | 0.982 [0.958, 1.010] | no detectable difference |
| open(ebook) / mainTaskMs | 261.7 [274.3] | 245.8 [253.8] | 0.939 [0.922, 0.972] | faster |
| open(ebook) / mainJsMs | 212.4 [219.7] | 197.6 [202.4] | 0.930 [0.908, 0.963] | faster |
| open(large) / readyMs | 90.5 [100.0] | 90.7 [107.8] | 1.001 [0.947, 1.061] | no detectable difference |
| open(large) / openedMs | 209.3 [216.7] | 204.8 [225.6] | 0.978 [0.955, 1.029] | no detectable difference |
| open(large) / firstPaintMs | 381.1 [386.9] | 361.8 [387.2] | 0.949 [0.897, 0.967] | faster |
| open(large) / settledMs | 381.1 [393.7] | 356.4 [374.6] | 0.935 [0.914, 0.962] | faster |
| open(large) / mainTaskMs | 532.1 [553.2] | 306.7 [310.1] | 0.576 [0.563, 0.585] | faster |
| open(large) / mainJsMs | 488.1 [507.5] | 259.4 [260.9] | 0.532 [0.521, 0.542] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [1.3] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 3018.3 [3130.0] | 2565.0 [2748.2] | 0.850 [0.829, 0.873] | faster |
| scroll / frameP50Ms | 11.0 [11.5] | 11.1 [11.5] | 1.007 [0.989, 1.029] | no detectable difference |
| scroll / frameP95Ms | 26.3 [27.0] | 26.4 [27.1] | 1.007 [0.945, 1.169] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 838.8 [866.2] | 768.1 [791.8] | 0.916 [0.897, 0.928] | faster |
| scroll / mainJsMs | 707.8 [731.9] | 643.9 [667.5] | 0.910 [0.892, 0.923] | faster |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [1.5] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2990.7 [3055.0] | 2553.3 [2615.1] | 0.854 [0.803, 0.878] | faster |
| coldScroll / frameP50Ms | 11.2 [11.4] | 11.1 [11.6] | 0.995 [0.973, 1.026] | no detectable difference |
| coldScroll / frameP95Ms | 26.2 [27.2] | 26.1 [27.2] | 0.996 [0.923, 1.031] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 847.0 [864.9] | 763.7 [788.1] | 0.902 [0.873, 0.925] | faster |
| coldScroll / mainJsMs | 697.5 [712.1] | 626.0 [642.8] | 0.897 [0.864, 0.917] | faster |
| jump / settleMs | 141.2 [157.6] | 139.1 [145.4] | -2.0 [-11.2, +7.2] | no detectable difference |
| jump / mainTaskMs | 144.5 [147.3] | 137.0 [140.6] | 0.948 [0.927, 0.975] | faster |
| jump / mainJsMs | 128.0 [131.5] | 120.2 [124.9] | 0.939 [0.912, 0.968] | no detectable difference |
| zoom / settleMs | 502.6 [526.5] | 516.4 [537.1] | +13.9 [-7.2, +23.6] | no detectable difference |
| zoom / zoom2SettleMs | 185.5 [207.2] | 199.8 [211.2] | +14.3 [-3.1, +21.5] | no detectable difference |
| zoom / zoom4SettleMs | 314.2 [342.6] | 317.8 [332.3] | +3.6 [-10.0, +15.1] | no detectable difference |
| zoom / backTo1SettleMs | 103.0 [110.5] | 95.8 [110.1] | -7.3 [-13.8, -1.9] | no detectable difference |
| zoom / mainTaskMs | 333.1 [348.3] | 318.2 [332.5] | 0.955 [0.918, 0.986] | no detectable difference |
| zoom / mainJsMs | 272.3 [284.1] | 258.8 [269.4] | 0.950 [0.917, 1.008] | no detectable difference |
| zoomLarge / settleMs | 305.3 [329.8] | 325.6 [342.5] | +20.3 [-0.0, +42.2] | no detectable difference |
| zoomLarge / zoom2SettleMs | 137.0 [161.1] | 146.8 [159.6] | +9.8 [+1.4, +19.6] | no detectable difference |
| zoomLarge / zoom4SettleMs | 164.1 [189.3] | 176.1 [199.8] | +12.0 [-12.3, +32.5] | no detectable difference |
| zoomLarge / backTo1SettleMs | 86.2 [98.9] | 86.9 [97.2] | +0.8 [-5.3, +4.1] | no detectable difference |
| zoomLarge / mainTaskMs | 260.3 [272.7] | 259.0 [263.7] | 0.995 [0.970, 1.021] | no detectable difference |
| zoomLarge / mainJsMs | 219.4 [229.8] | 216.9 [219.7] | 0.989 [0.967, 1.019] | no detectable difference |
| select / settleMs | 231.7 [266.7] | 256.7 [311.2] | +24.9 [+11.8, +74.4] | no detectable difference |
| select / mainTaskMs | 83.5 [91.7] | 90.6 [103.2] | 1.084 [1.007, 1.198] | slower |
| select / mainJsMs | 66.6 [73.2] | 70.8 [82.0] | 1.063 [0.989, 1.198] | no detectable difference |
| selectLarge / settleMs | 238.2 [275.2] | 270.3 [301.4] | +32.1 [+4.5, +57.1] | no detectable difference |
| selectLarge / mainTaskMs | 111.8 [115.4] | 112.6 [122.4] | 1.007 [0.968, 1.079] | no detectable difference |
| selectLarge / mainJsMs | 87.6 [90.5] | 85.7 [93.5] | 0.978 [0.943, 1.082] | no detectable difference |
| search / searchMs | 72.7 [83.4] | 76.0 [83.9] | 1.046 [0.888, 1.164] | no detectable difference |
| search / mainTaskMs | 10.1 [19.8] | 12.3 [20.0] | 1.220 [0.702, 1.950] | no detectable difference |
| search / mainJsMs | 6.8 [9.6] | 7.5 [10.0] | 1.092 [0.821, 1.263] | no detectable difference |
| searchLarge / searchMs | 1820.9 [1830.2] | 1839.5 [1859.3] | 1.010 [1.007, 1.017] | no detectable difference |
| searchLarge / mainTaskMs | 839.1 [846.8] | 870.5 [880.7] | 1.037 [1.032, 1.045] | slower |
| searchLarge / mainJsMs | 798.7 [804.5] | 824.7 [829.5] | 1.033 [1.027, 1.039] | slower |
| annotate / settleMs | 224.4 [235.6] | 209.7 [271.9] | -14.7 [-40.8, +26.6] | no detectable difference |
| annotate / mainTaskMs | 103.2 [109.6] | 86.8 [95.7] | 0.841 [0.793, 0.885] | faster |
| annotate / mainJsMs | 75.8 [80.5] | 60.6 [68.7] | 0.800 [0.745, 0.856] | faster |
| annotateLarge / settleMs | 212.0 [258.9] | 241.3 [288.1] | +29.3 [+2.9, +60.4] | no detectable difference |
| annotateLarge / mainTaskMs | 122.5 [130.7] | 113.4 [125.4] | 0.925 [0.896, 0.984] | faster |
| annotateLarge / mainJsMs | 82.2 [87.8] | 72.5 [80.2] | 0.882 [0.841, 0.954] | faster |

## `main` vs `plugins-coalesce`

| Row | main median [p95] | plugins-coalesce median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 62.5 [65.7] | 89.9 [100.6] | 1.438 [1.391, 1.523] | slower |
| open(ebook) / openedMs | 137.5 [148.4] | 177.2 [189.0] | 1.288 [1.247, 1.342] | slower |
| open(ebook) / firstPaintMs | 256.6 [275.3] | 333.9 [348.0] | 1.301 [1.250, 1.358] | slower |
| open(ebook) / settledMs | 396.4 [406.9] | 398.2 [414.2] | 1.004 [0.990, 1.037] | no detectable difference |
| open(ebook) / mainTaskMs | 143.1 [147.8] | 245.8 [253.8] | 1.718 [1.668, 1.755] | slower |
| open(ebook) / mainJsMs | 110.7 [114.4] | 197.6 [202.4] | 1.785 [1.732, 1.835] | slower |
| open(large) / readyMs | 61.9 [70.3] | 90.7 [107.8] | 1.464 [1.383, 1.555] | slower |
| open(large) / openedMs | 158.5 [164.1] | 204.8 [225.6] | 1.292 [1.282, 1.352] | slower |
| open(large) / firstPaintMs | 265.4 [269.8] | 361.8 [387.2] | 1.363 [1.285, 1.387] | slower |
| open(large) / settledMs | 264.9 [281.4] | 356.4 [374.6] | 1.345 [1.310, 1.372] | slower |
| open(large) / mainTaskMs | 314.6 [318.0] | 306.7 [310.1] | 0.975 [0.964, 0.985] | no detectable difference |
| open(large) / mainJsMs | 283.2 [286.7] | 259.4 [260.9] | 0.916 [0.907, 0.919] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [1.3] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2880.5 [3003.1] | 2565.0 [2748.2] | 0.890 [0.860, 0.913] | faster |
| scroll / frameP50Ms | 11.1 [11.6] | 11.1 [11.5] | 1.000 [0.978, 1.017] | no detectable difference |
| scroll / frameP95Ms | 23.9 [27.2] | 26.4 [27.1] | 1.108 [0.978, 1.211] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 548.2 [564.2] | 768.1 [791.8] | 1.401 [1.364, 1.441] | slower |
| scroll / mainJsMs | 412.1 [424.4] | 643.9 [667.5] | 1.562 [1.516, 1.611] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [1.5] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2860.3 [2976.0] | 2553.3 [2615.1] | 0.893 [0.840, 0.913] | faster |
| coldScroll / frameP50Ms | 11.0 [11.5] | 11.1 [11.6] | 1.005 [0.984, 1.039] | no detectable difference |
| coldScroll / frameP95Ms | 24.4 [27.0] | 26.1 [27.2] | 1.069 [0.982, 1.149] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 570.2 [584.2] | 763.7 [788.1] | 1.339 [1.291, 1.374] | slower |
| coldScroll / mainJsMs | 400.8 [413.2] | 626.0 [642.8] | 1.562 [1.495, 1.597] | slower |
| jump / settleMs | 123.0 [132.6] | 139.1 [145.4] | +16.2 [+6.6, +25.6] | slower |
| jump / mainTaskMs | 73.2 [78.5] | 137.0 [140.6] | 1.870 [1.805, 1.905] | slower |
| jump / mainJsMs | 55.3 [59.0] | 120.2 [124.9] | 2.174 [2.051, 2.231] | slower |
| zoom / settleMs | 457.8 [468.9] | 516.4 [537.1] | +58.7 [+38.7, +85.6] | slower |
| zoom / zoom2SettleMs | 162.2 [171.0] | 199.8 [211.2] | +37.6 [+22.0, +45.4] | slower |
| zoom / zoom4SettleMs | 287.3 [306.6] | 317.8 [332.3] | +30.6 [+14.0, +46.6] | slower |
| zoom / backTo1SettleMs | 91.0 [100.3] | 95.8 [110.1] | +4.8 [+1.5, +14.5] | no detectable difference |
| zoom / mainTaskMs | 214.6 [229.4] | 318.2 [332.5] | 1.483 [1.415, 1.520] | slower |
| zoom / mainJsMs | 155.1 [165.3] | 258.8 [269.4] | 1.668 [1.627, 1.725] | slower |
| zoomLarge / settleMs | 298.8 [314.9] | 325.6 [342.5] | +26.7 [+8.8, +47.4] | slower |
| zoomLarge / zoom2SettleMs | 144.0 [165.8] | 146.8 [159.6] | +2.8 [-9.8, +10.2] | no detectable difference |
| zoomLarge / zoom4SettleMs | 148.7 [171.9] | 176.1 [199.8] | +27.5 [+9.5, +42.0] | slower |
| zoomLarge / backTo1SettleMs | 76.5 [90.6] | 86.9 [97.2] | +10.5 [-1.6, +13.9] | no detectable difference |
| zoomLarge / mainTaskMs | 180.9 [186.4] | 259.0 [263.7] | 1.432 [1.412, 1.484] | slower |
| zoomLarge / mainJsMs | 135.1 [139.8] | 216.9 [219.7] | 1.605 [1.581, 1.655] | slower |
| select / settleMs | 220.3 [249.4] | 256.7 [311.2] | +36.4 [+20.7, +69.6] | slower |
| select / mainTaskMs | 60.6 [66.9] | 90.6 [103.2] | 1.496 [1.371, 1.628] | slower |
| select / mainJsMs | 39.3 [43.3] | 70.8 [82.0] | 1.803 [1.661, 1.995] | slower |
| selectLarge / settleMs | 207.2 [250.9] | 270.3 [301.4] | +63.1 [+32.5, +89.5] | slower |
| selectLarge / mainTaskMs | 69.3 [73.6] | 112.6 [122.4] | 1.626 [1.543, 1.742] | slower |
| selectLarge / mainJsMs | 46.6 [49.7] | 85.7 [93.5] | 1.837 [1.746, 1.979] | slower |
| search / searchMs | 68.8 [78.6] | 76.0 [83.9] | 1.106 [0.989, 1.227] | no detectable difference |
| search / mainTaskMs | 5.3 [5.9] | 12.3 [20.0] | 2.315 [1.765, 3.715] | slower |
| search / mainJsMs | 3.7 [3.9] | 7.5 [10.0] | 2.035 [1.783, 2.341] | slower |
| searchLarge / searchMs | 1892.1 [1949.2] | 1839.5 [1859.3] | 0.972 [0.951, 0.978] | no detectable difference |
| searchLarge / mainTaskMs | 345.0 [348.9] | 870.5 [880.7] | 2.523 [2.509, 2.550] | slower |
| searchLarge / mainJsMs | 291.5 [293.8] | 824.7 [829.5] | 2.829 [2.815, 2.853] | slower |
| annotate / settleMs | 207.9 [275.3] | 209.7 [271.9] | +1.8 [-43.4, +29.8] | no detectable difference |
| annotate / mainTaskMs | 78.4 [89.7] | 86.8 [95.7] | 1.107 [1.014, 1.180] | slower |
| annotate / mainJsMs | 46.9 [54.4] | 60.6 [68.7] | 1.292 [1.189, 1.379] | slower |
| annotateLarge / settleMs | 230.2 [260.8] | 241.3 [288.1] | +11.0 [-13.7, +46.5] | no detectable difference |
| annotateLarge / mainTaskMs | 83.2 [88.9] | 113.4 [125.4] | 1.363 [1.314, 1.459] | slower |
| annotateLarge / mainJsMs | 50.2 [53.6] | 72.5 [80.2] | 1.444 [1.367, 1.565] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-tip5 median [p95] | plugins-coalesce median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 35.34 MiB [35.36 MiB] | 34.35 MiB [35.35 MiB] | 36.86 MiB [37.42 MiB] | 36.86 MiB [37.86 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.18 MiB] | 48.15 MiB [48.18 MiB] | 51.56 MiB [51.67 MiB] | 52.08 MiB [52.10 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.28 MiB] | 22.26 MiB [22.29 MiB] | 22.96 MiB [22.96 MiB] | 23.01 MiB [23.03 MiB] |
| soak / heapCycle5Bytes | 37.92 MiB [38.97 MiB] | 37.42 MiB [38.42 MiB] | 39.74 MiB [40.75 MiB] | 39.73 MiB [41.84 MiB] |
| soak / heapCycle30Bytes | 38.76 MiB [39.82 MiB] | 38.26 MiB [39.27 MiB] | 40.28 MiB [41.29 MiB] | 40.27 MiB [42.37 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-tip5`:

| Table | Cycle 5 | Cycle 30 |
| --- | --- | --- |
| aborts | 0 | 0 |
| allHosts | 297 | 297 |
| described | 18 | 18 |
| documents | 1 | 1 |
| fns | 259 | 259 |
| heldFns | 259 | 259 |
| hosts | 297 | 297 |
| promises | 0 | 0 |
| proxies | 18 | 18 |
| refs | 658 | 658 |
| wasmBytes | 2.69 MiB | 2.69 MiB |
| wireRefs | 658 | 658 |

`plugins-coalesce`:

| Table | Cycle 5 | Cycle 30 |
| --- | --- | --- |
| aborts | 0 | 0 |
| allHosts | 297 | 297 |
| described | 18 | 18 |
| documents | 1 | 1 |
| fns | 259 | 259 |
| heldFns | 259 | 259 |
| hosts | 297 | 297 |
| promises | 0 | 0 |
| proxies | 18 | 18 |
| refs | 658 | 658 |
| wasmBytes | 2.69 MiB | 2.69 MiB |
| wireRefs | 658 | 658 |

