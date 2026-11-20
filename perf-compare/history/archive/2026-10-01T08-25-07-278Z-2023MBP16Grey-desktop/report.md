# Viewer perf comparison: 2026-10-01T08-25-07-278Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 32745220c04b39b51c2385cbd723f69c8065f602 |
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
| plugins-now | research/multi-platform-rust-core-impl-plugins | 4f9ceee9c | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | d6276d5fb70d |
| plugins-slicetext | research/rust-kernel-slice-text | d9c5e4a58 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 47364f7f1fac |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-now`: pass
- `plugins-slicetext`: pass

## Sizes

| Metric | main | main-aa | plugins-now | plugins-slicetext |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 7.93 MiB | 7.94 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.70 MiB | 1.70 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

47 of 48 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.2 [53.1] | 52.0 [52.0] | 0.997 [0.973, 1.014] | pass |
| open(ebook) / openedMs | 115.9 [117.3] | 115.1 [116.3] | 0.993 [0.975, 1.034] | pass |
| open(ebook) / firstPaintMs | 212.7 [214.4] | 212.6 [217.1] | 0.999 [0.982, 1.064] | pass |
| open(ebook) / settledMs | 333.7 [336.7] | 332.0 [336.5] | 0.995 [0.981, 1.037] | pass |
| open(ebook) / mainTaskMs | 121.0 [121.6] | 119.0 [119.1] | 0.983 [0.962, 1.000] | pass |
| open(ebook) / mainJsMs | 94.8 [94.8] | 91.3 [92.2] | 0.963 [0.963, 1.003] | pass |
| open(large) / readyMs | 52.8 [52.8] | 52.3 [52.8] | 0.991 [0.987, 1.013] | pass |
| open(large) / openedMs | 133.7 [135.2] | 133.3 [136.4] | 0.997 [0.975, 1.038] | pass |
| open(large) / firstPaintMs | 225.0 [229.5] | 223.0 [223.6] | 0.991 [0.964, 1.007] | pass |
| open(large) / settledMs | 221.9 [226.9] | 220.4 [227.5] | 0.993 [0.963, 1.039] | pass |
| open(large) / mainTaskMs | 261.8 [268.7] | 260.6 [260.8] | 0.995 [0.963, 0.998] | pass |
| open(large) / mainJsMs | 238.2 [245.1] | 236.7 [237.4] | 0.994 [0.962, 0.997] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2003.3 [2005.0] | 2017.6 [2031.8] | 1.007 [1.002, 1.017] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.5 [8.6] | 8.6 [8.6] | 1.006 [0.980, 1.025] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 393.7 [411.8] | 406.9 [413.6] | 1.034 [0.972, 1.061] | pass |
| scroll / mainJsMs | 303.1 [316.4] | 312.7 [320.0] | 1.032 [0.982, 1.065] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2013.6 [2019.9] | 2010.1 [2011.3] | 0.998 [0.994, 1.006] | pass |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| coldScroll / frameP95Ms | 8.9 [9.0] | 8.6 [8.7] | 0.962 [0.929, 0.985] | A/A failed |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 436.5 [438.9] | 441.4 [443.3] | 1.011 [0.969, 1.065] | pass |
| coldScroll / mainJsMs | 313.4 [315.9] | 316.5 [318.5] | 1.010 [0.951, 1.068] | pass |
| jump / settleMs | 96.0 [97.6] | 90.0 [96.2] | -6.0 [-12.4, +3.9] | pass |
| jump / mainTaskMs | 54.8 [57.6] | 51.6 [59.8] | 0.941 [0.880, 1.188] | pass |
| jump / mainJsMs | 40.8 [41.5] | 40.9 [43.1] | 1.001 [0.911, 1.140] | pass |
| zoom / settleMs | 396.7 [399.7] | 389.5 [391.8] | -7.2 [-27.8, +2.0] | pass |
| zoom / zoom2SettleMs | 132.1 [133.1] | 128.7 [130.5] | -3.4 [-8.1, -0.9] | pass |
| zoom / zoom4SettleMs | 265.1 [267.7] | 260.8 [266.4] | -4.3 [-26.4, +10.2] | pass |
| zoom / backTo1SettleMs | 73.4 [77.5] | 82.8 [88.1] | +9.4 [-0.1, +15.8] | pass |
| zoom / mainTaskMs | 179.6 [184.7] | 177.1 [187.0] | 0.986 [0.955, 1.054] | pass |
| zoom / mainJsMs | 129.5 [134.0] | 129.8 [134.1] | 1.002 [0.928, 1.040] | pass |
| select / settleMs | 146.5 [148.9] | 145.0 [150.6] | -1.5 [-10.5, +15.9] | pass |
| select / mainTaskMs | 43.3 [45.3] | 41.7 [48.9] | 0.963 [0.851, 1.195] | pass |
| select / mainJsMs | 26.6 [27.7] | 26.0 [30.1] | 0.978 [0.859, 1.182] | pass |
| search / searchMs | 57.3 [58.3] | 59.9 [60.4] | 1.045 [0.858, 1.282] | pass |
| search / mainTaskMs | 5.1 [5.1] | 5.4 [6.0] | 1.066 [0.799, 1.388] | pass |
| search / mainJsMs | 3.2 [3.5] | 3.3 [3.6] | 1.037 [0.835, 1.184] | pass |
| annotate / settleMs | 158.9 [160.0] | 161.0 [167.6] | +2.1 [-0.4, +15.7] | pass |
| annotate / mainTaskMs | 58.9 [59.4] | 55.5 [62.9] | 0.942 [0.918, 1.112] | pass |
| annotate / mainJsMs | 35.1 [35.9] | 33.2 [36.0] | 0.945 [0.897, 1.072] | pass |

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

| Row | plugins-now median [p95] | plugins-slicetext median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 75.7 [76.2] | 74.6 [74.8] | 0.985 [0.963, 1.002] | no detectable difference |
| open(ebook) / openedMs | 173.7 [174.3] | 172.0 [172.1] | 0.990 [0.974, 1.005] | no detectable difference |
| open(ebook) / firstPaintMs | 299.3 [299.7] | 301.2 [301.4] | 1.006 [0.975, 1.012] | no detectable difference |
| open(ebook) / settledMs | 348.3 [352.5] | 345.9 [355.0] | 0.993 [0.958, 1.028] | no detectable difference |
| open(ebook) / mainTaskMs | 236.7 [240.6] | 235.9 [242.3] | 0.996 [0.976, 1.029] | no detectable difference |
| open(ebook) / mainJsMs | 198.2 [200.7] | 197.7 [202.8] | 0.998 [0.983, 1.031] | no detectable difference |
| open(large) / readyMs | 76.3 [76.4] | 74.7 [75.2] | 0.979 [0.976, 1.005] | no detectable difference |
| open(large) / openedMs | 203.5 [203.7] | 198.3 [198.5] | 0.975 [0.969, 0.996] | no detectable difference |
| open(large) / firstPaintMs | 335.2 [338.9] | 333.6 [336.4] | 0.995 [0.961, 1.017] | no detectable difference |
| open(large) / settledMs | 339.3 [341.1] | 335.4 [338.4] | 0.988 [0.971, 1.007] | no detectable difference |
| open(large) / mainTaskMs | 523.0 [525.2] | 517.8 [519.4] | 0.990 [0.980, 1.006] | no detectable difference |
| open(large) / mainJsMs | 486.9 [487.9] | 480.0 [481.5] | 0.986 [0.980, 1.003] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2269.0 [2329.3] | 2210.3 [2222.9] | 0.974 [0.926, 1.010] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 0.999 [0.999, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.6 [9.7] | 9.7 [9.9] | 1.016 [0.959, 1.046] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 762.2 [827.8] | 724.5 [749.3] | 0.951 [0.866, 0.991] | no detectable difference |
| scroll / mainJsMs | 654.8 [726.1] | 624.8 [646.1] | 0.954 [0.846, 0.995] | no detectable difference |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2250.1 [2294.5] | 2214.5 [2248.4] | 0.984 [0.959, 1.004] | no detectable difference |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 9.6 [10.0] | 9.4 [9.8] | 0.984 [0.862, 1.035] | unreliable (A/A failed) |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 1048.2 [1056.9] | 787.3 [793.0] | 0.751 [0.743, 0.757] | faster |
| coldScroll / mainJsMs | 934.0 [940.3] | 672.4 [674.6] | 0.720 [0.714, 0.723] | faster |
| jump / settleMs | 149.8 [160.1] | 139.4 [171.7] | -10.4 [-30.6, +63.1] | no detectable difference |
| jump / mainTaskMs | 145.0 [147.2] | 159.0 [160.2] | 1.097 [1.064, 1.166] | no detectable difference |
| jump / mainJsMs | 128.9 [132.1] | 141.3 [141.9] | 1.096 [1.048, 1.138] | no detectable difference |
| zoom / settleMs | 425.5 [430.5] | 426.1 [433.4] | +0.6 [-13.2, +37.1] | no detectable difference |
| zoom / zoom2SettleMs | 148.9 [152.1] | 149.3 [150.7] | +0.4 [-4.6, +3.3] | no detectable difference |
| zoom / zoom4SettleMs | 276.6 [278.4] | 275.3 [284.0] | -1.3 [-8.6, +35.4] | no detectable difference |
| zoom / backTo1SettleMs | 86.5 [87.3] | 87.0 [87.3] | +0.5 [-7.9, +7.9] | no detectable difference |
| zoom / mainTaskMs | 271.4 [277.9] | 265.4 [277.5] | 0.978 [0.935, 1.032] | no detectable difference |
| zoom / mainJsMs | 224.7 [229.2] | 224.1 [232.9] | 0.997 [0.957, 1.046] | no detectable difference |
| select / settleMs | 147.0 [151.5] | 142.9 [144.0] | -4.1 [-16.3, -0.4] | no detectable difference |
| select / mainTaskMs | 66.3 [72.8] | 63.0 [64.2] | 0.951 [0.850, 1.004] | no detectable difference |
| select / mainJsMs | 51.5 [58.3] | 50.3 [50.3] | 0.976 [0.837, 0.986] | no detectable difference |
| search / searchMs | 68.1 [70.5] | 68.1 [70.0] | 1.000 [0.888, 1.402] | no detectable difference |
| search / mainTaskMs | 17.5 [20.1] | 15.7 [17.7] | 0.898 [0.540, 2.190] | no detectable difference |
| search / mainJsMs | 7.8 [9.6] | 7.4 [7.5] | 0.942 [0.735, 1.221] | no detectable difference |
| annotate / settleMs | 163.9 [167.8] | 164.9 [167.5] | +1.0 [-3.7, +6.3] | no detectable difference |
| annotate / mainTaskMs | 81.6 [83.1] | 86.1 [87.1] | 1.054 [1.024, 1.085] | no detectable difference |
| annotate / mainJsMs | 61.2 [61.6] | 64.1 [64.3] | 1.047 [1.030, 1.084] | no detectable difference |

## `main` vs `plugins-slicetext`

| Row | main median [p95] | plugins-slicetext median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.2 [53.1] | 74.6 [74.8] | 1.430 [1.380, 1.457] | slower |
| open(ebook) / openedMs | 115.9 [117.3] | 172.0 [172.1] | 1.485 [1.446, 1.528] | slower |
| open(ebook) / firstPaintMs | 212.7 [214.4] | 301.2 [301.4] | 1.416 [1.361, 1.473] | slower |
| open(ebook) / settledMs | 333.7 [336.7] | 345.9 [355.0] | 1.037 [1.004, 1.095] | no detectable difference |
| open(ebook) / mainTaskMs | 121.0 [121.6] | 235.9 [242.3] | 1.949 [1.933, 2.040] | slower |
| open(ebook) / mainJsMs | 94.8 [94.8] | 197.7 [202.8] | 2.086 [2.085, 2.209] | slower |
| open(large) / readyMs | 52.8 [52.8] | 74.7 [75.2] | 1.416 [1.413, 1.445] | slower |
| open(large) / openedMs | 133.7 [135.2] | 198.3 [198.5] | 1.483 [1.458, 1.508] | slower |
| open(large) / firstPaintMs | 225.0 [229.5] | 333.6 [336.4] | 1.482 [1.418, 1.517] | slower |
| open(large) / settledMs | 221.9 [226.9] | 335.4 [338.4] | 1.511 [1.456, 1.542] | slower |
| open(large) / mainTaskMs | 261.8 [268.7] | 517.8 [519.4] | 1.978 [1.911, 1.988] | slower |
| open(large) / mainJsMs | 238.2 [245.1] | 480.0 [481.5] | 2.015 [1.946, 2.023] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2003.3 [2005.0] | 2210.3 [2222.9] | 1.103 [1.079, 1.113] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 0.999 [0.999, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.5 [8.6] | 9.7 [9.9] | 1.141 [1.079, 1.173] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 393.7 [411.8] | 724.5 [749.3] | 1.840 [1.747, 1.926] | slower |
| scroll / mainJsMs | 303.1 [316.4] | 624.8 [646.1] | 2.061 [1.953, 2.152] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2013.6 [2019.9] | 2214.5 [2248.4] | 1.100 [1.091, 1.126] | slower |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 8.9 [9.0] | 9.4 [9.8] | 1.056 [0.953, 1.111] | unreliable (A/A failed) |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 436.5 [438.9] | 787.3 [793.0] | 1.804 [1.790, 1.906] | slower |
| coldScroll / mainJsMs | 313.4 [315.9] | 672.4 [674.6] | 2.146 [2.123, 2.261] | slower |
| jump / settleMs | 96.0 [97.6] | 139.4 [171.7] | +43.3 [+32.9, +82.3] | slower |
| jump / mainTaskMs | 54.8 [57.6] | 159.0 [160.2] | 2.901 [2.708, 3.135] | slower |
| jump / mainJsMs | 40.8 [41.5] | 141.3 [141.9] | 3.460 [3.339, 3.731] | slower |
| zoom / settleMs | 396.7 [399.7] | 426.1 [433.4] | +29.5 [+17.8, +44.2] | slower |
| zoom / zoom2SettleMs | 132.1 [133.1] | 149.3 [150.7] | +17.2 [+14.7, +19.3] | slower |
| zoom / zoom4SettleMs | 265.1 [267.7] | 275.3 [284.0] | +10.2 [+2.0, +28.2] | no detectable difference |
| zoom / backTo1SettleMs | 73.4 [77.5] | 87.0 [87.3] | +13.6 [+1.6, +14.4] | no detectable difference |
| zoom / mainTaskMs | 179.6 [184.7] | 265.4 [277.5] | 1.478 [1.405, 1.563] | slower |
| zoom / mainJsMs | 129.5 [134.0] | 224.1 [232.9] | 1.731 [1.634, 1.808] | slower |
| select / settleMs | 146.5 [148.9] | 142.9 [144.0] | -3.6 [-13.5, +8.9] | no detectable difference |
| select / mainTaskMs | 43.3 [45.3] | 63.0 [64.2] | 1.455 [1.372, 1.546] | slower |
| select / mainJsMs | 26.6 [27.7] | 50.3 [50.3] | 1.893 [1.778, 1.949] | slower |
| search / searchMs | 57.3 [58.3] | 68.1 [70.0] | 1.187 [1.074, 1.491] | no detectable difference |
| search / mainTaskMs | 5.1 [5.1] | 15.7 [17.7] | 3.098 [2.138, 4.108] | slower |
| search / mainJsMs | 3.2 [3.5] | 7.4 [7.5] | 2.331 [2.049, 2.447] | slower |
| annotate / settleMs | 158.9 [160.0] | 164.9 [167.5] | +6.0 [+4.4, +15.2] | no detectable difference |
| annotate / mainTaskMs | 58.9 [59.4] | 86.1 [87.1] | 1.462 [1.435, 1.522] | slower |
| annotate / mainJsMs | 35.1 [35.9] | 64.1 [64.3] | 1.825 [1.766, 1.900] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-now median [p95] | plugins-slicetext median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.35 MiB [34.35 MiB] | 34.35 MiB [34.35 MiB] | 42.41 MiB [42.41 MiB] | 42.41 MiB [42.41 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.03 MiB [23.03 MiB] | 23.03 MiB [23.03 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.16 MiB] | 48.17 MiB [48.17 MiB] | 56.23 MiB [56.23 MiB] | 56.24 MiB [56.24 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.27 MiB] | 22.26 MiB [22.28 MiB] | 22.96 MiB [22.96 MiB] | 22.96 MiB [22.96 MiB] |
| soak / heapCycle5Bytes | 37.42 MiB [37.42 MiB] | 37.41 MiB [37.42 MiB] | 45.23 MiB [45.24 MiB] | 44.23 MiB [45.13 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.27 MiB] | 38.26 MiB [38.27 MiB] | 45.77 MiB [45.77 MiB] | 44.78 MiB [45.67 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-now`:

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

`plugins-slicetext`:

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

