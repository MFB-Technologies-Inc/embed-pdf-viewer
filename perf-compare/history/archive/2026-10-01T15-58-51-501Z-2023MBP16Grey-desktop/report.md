# Viewer perf comparison: 2026-10-01T15-58-51-501Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 92190f9e0a69d37a50e3438cd37e55a65ff9d1e2 |
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
| impl-now | research/multi-platform-rust-core-impl | 65d9798d6 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 |  |
| impl-glyphview | research/rust-core-glyph-view | 061a544c4 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 |  |
| plugins-tip2 | research/multi-platform-rust-core-impl-plugins | d9c5e4a58 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 47364f7f1fac |
| plugins-glyphview | research/rust-kernel-glyph-view | 16f128278 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 47364f7f1fac |

## Gate

- `main`: pass
- `main-aa`: pass
- `impl-now`: pass
- `impl-glyphview`: pass
- `plugins-tip2`: pass
- `plugins-glyphview`: pass

## Sizes

| Metric | main | main-aa | impl-now | impl-glyphview | plugins-tip2 | plugins-glyphview |
| --- | --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 0.00 MiB | 7.94 MiB | 7.94 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 0.00 MiB | 1.70 MiB | 1.70 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 3.36 MiB | 3.36 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

50 of 51 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.7 [53.0] | 51.8 [54.4] | 0.983 [0.972, 1.046] | pass |
| open(ebook) / openedMs | 115.0 [115.6] | 113.8 [116.6] | 0.990 [0.977, 1.023] | pass |
| open(ebook) / firstPaintMs | 214.6 [215.9] | 208.1 [213.9] | 0.970 [0.948, 1.027] | pass |
| open(ebook) / settledMs | 334.1 [336.0] | 332.2 [332.5] | 0.994 [0.967, 1.016] | pass |
| open(ebook) / mainTaskMs | 118.8 [120.1] | 117.2 [119.6] | 0.987 [0.954, 1.019] | pass |
| open(ebook) / mainJsMs | 92.4 [93.4] | 89.9 [91.9] | 0.973 [0.951, 1.016] | pass |
| open(large) / readyMs | 52.9 [53.0] | 52.4 [52.7] | 0.989 [0.982, 0.997] | pass |
| open(large) / openedMs | 132.5 [133.1] | 133.4 [133.6] | 1.007 [0.987, 1.017] | pass |
| open(large) / firstPaintMs | 222.4 [223.1] | 216.9 [216.9] | 0.975 [0.964, 0.977] | pass |
| open(large) / settledMs | 219.2 [220.2] | 214.2 [214.2] | 0.977 [0.961, 0.978] | pass |
| open(large) / mainTaskMs | 258.3 [258.5] | 260.1 [261.6] | 1.007 [0.984, 1.024] | pass |
| open(large) / mainJsMs | 235.7 [235.7] | 237.0 [238.4] | 1.005 [0.984, 1.022] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2007.2 [2009.4] | 2013.6 [2035.3] | 1.003 [0.995, 1.024] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [9.1] | 8.7 [9.0] | 1.034 [0.923, 1.077] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 407.9 [413.3] | 410.9 [424.5] | 1.007 [0.977, 1.055] | pass |
| scroll / mainJsMs | 313.6 [317.6] | 317.3 [329.1] | 1.012 [0.972, 1.061] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2013.0 [2023.1] | 2014.6 [2019.5] | 1.001 [0.990, 1.008] | pass |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| coldScroll / frameP95Ms | 8.9 [9.2] | 8.6 [8.6] | 0.966 [0.929, 1.021] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 445.9 [458.2] | 450.9 [460.3] | 1.011 [0.952, 1.041] | pass |
| coldScroll / mainJsMs | 319.7 [330.7] | 323.8 [331.1] | 1.013 [0.960, 1.041] | pass |
| jump / settleMs | 94.4 [96.4] | 90.1 [91.6] | -4.3 [-7.7, +2.8] | pass |
| jump / mainTaskMs | 55.4 [58.3] | 55.1 [55.6] | 0.994 [0.899, 1.023] | pass |
| jump / mainJsMs | 41.8 [42.1] | 41.7 [44.0] | 0.998 [0.936, 1.115] | pass |
| zoom / settleMs | 340.9 [341.1] | 339.8 [357.8] | -1.1 [-3.8, +26.5] | pass |
| zoom / zoom2SettleMs | 132.3 [132.9] | 131.6 [133.1] | -0.6 [-8.5, +4.6] | pass |
| zoom / zoom4SettleMs | 208.0 [212.1] | 206.6 [232.5] | -1.4 [-6.8, +34.3] | pass |
| zoom / backTo1SettleMs | 61.8 [78.6] | 63.8 [66.0] | +1.9 [-19.7, +4.9] | pass |
| zoom / mainTaskMs | 169.4 [169.9] | 172.9 [175.3] | 1.021 [1.005, 1.053] | pass |
| zoom / mainJsMs | 122.8 [123.2] | 123.6 [127.2] | 1.007 [0.967, 1.063] | pass |
| select / settleMs | 147.2 [148.1] | 146.9 [149.8] | -0.4 [-1.5, +3.1] | pass |
| select / mainTaskMs | 46.6 [48.6] | 46.3 [46.8] | 0.994 [0.921, 1.110] | pass |
| select / mainJsMs | 27.9 [28.7] | 27.5 [27.9] | 0.985 [0.950, 1.094] | pass |
| search / searchMs | 52.5 [59.3] | 59.7 [60.7] | 1.137 [0.980, 1.189] | pass |
| search / mainTaskMs | 4.9 [5.2] | 4.7 [5.1] | 0.967 [0.830, 1.142] | pass |
| search / mainJsMs | 3.0 [3.1] | 3.2 [3.3] | 1.043 [1.002, 1.085] | A/A failed |
| searchLarge / searchMs | 1659.8 [1669.6] | 1624.7 [1646.2] | 0.979 [0.967, 1.021] | pass |
| searchLarge / mainTaskMs | 266.6 [268.6] | 265.9 [266.0] | 0.997 [0.988, 1.000] | pass |
| searchLarge / mainJsMs | 232.7 [233.3] | 230.1 [230.6] | 0.989 [0.985, 1.007] | pass |
| annotate / settleMs | 159.3 [162.9] | 161.0 [166.2] | +1.7 [-3.6, +9.2] | pass |
| annotate / mainTaskMs | 61.1 [62.1] | 59.7 [61.4] | 0.977 [0.759, 1.040] | pass |
| annotate / mainJsMs | 35.7 [36.0] | 34.6 [35.3] | 0.972 [0.711, 1.032] | pass |

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

| Row | impl-now median [p95] | impl-glyphview median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.0 [54.6] | 51.9 [51.9] | 0.998 [0.944, 1.013] | no detectable difference |
| open(ebook) / openedMs | 115.8 [118.4] | 115.6 [116.6] | 0.999 [0.972, 1.008] | no detectable difference |
| open(ebook) / firstPaintMs | 210.9 [215.9] | 213.6 [214.8] | 1.013 [0.978, 1.022] | no detectable difference |
| open(ebook) / settledMs | 258.1 [259.6] | 259.9 [260.7] | 1.007 [0.993, 1.012] | no detectable difference |
| open(ebook) / mainTaskMs | 104.2 [105.2] | 104.3 [106.7] | 1.000 [0.984, 1.040] | no detectable difference |
| open(ebook) / mainJsMs | 80.6 [81.3] | 80.4 [81.9] | 0.997 [0.982, 1.038] | no detectable difference |
| open(large) / readyMs | 51.8 [52.9] | 52.3 [52.7] | 1.010 [0.966, 1.035] | no detectable difference |
| open(large) / openedMs | 136.9 [136.9] | 137.4 [137.6] | 1.004 [0.998, 1.008] | no detectable difference |
| open(large) / firstPaintMs | 217.4 [217.9] | 219.7 [221.2] | 1.010 [0.997, 1.083] | no detectable difference |
| open(large) / settledMs | 213.8 [216.2] | 212.9 [214.8] | 0.996 [0.962, 1.010] | no detectable difference |
| open(large) / mainTaskMs | 160.0 [160.1] | 158.2 [160.8] | 0.989 [0.984, 1.027] | no detectable difference |
| open(large) / mainJsMs | 133.9 [134.2] | 133.2 [134.1] | 0.995 [0.983, 1.024] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2006.9 [2014.1] | 2015.5 [2022.5] | 1.004 [1.000, 1.016] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.5] | 8.5 [8.8] | 1.002 [0.988, 1.058] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 340.0 [340.3] | 342.2 [346.2] | 1.006 [0.965, 1.057] | no detectable difference |
| scroll / mainJsMs | 231.8 [232.2] | 236.8 [241.2] | 1.022 [0.984, 1.068] | no detectable difference |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2008.1 [2019.8] | 2017.5 [2028.2] | 1.005 [0.988, 1.015] | no detectable difference |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 8.7 [9.0] | 8.5 [8.8] | 0.972 [0.928, 1.050] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 354.5 [363.9] | 364.2 [379.0] | 1.028 [0.976, 1.080] | no detectable difference |
| coldScroll / mainJsMs | 242.2 [247.0] | 246.0 [256.8] | 1.016 [0.961, 1.070] | no detectable difference |
| jump / settleMs | 81.5 [87.4] | 81.9 [82.8] | +0.4 [-9.3, +1.4] | no detectable difference |
| jump / mainTaskMs | 52.4 [52.5] | 51.1 [51.8] | 0.975 [0.905, 1.086] | no detectable difference |
| jump / mainJsMs | 32.4 [33.0] | 32.0 [32.2] | 0.985 [0.964, 1.016] | no detectable difference |
| zoom / settleMs | 337.4 [346.7] | 351.1 [362.6] | +13.7 [-2.6, +30.0] | no detectable difference |
| zoom / zoom2SettleMs | 131.5 [131.9] | 132.6 [142.6] | +1.1 [-0.1, +25.0] | no detectable difference |
| zoom / zoom4SettleMs | 215.8 [218.3] | 219.3 [220.0] | +3.5 [-6.1, +17.8] | no detectable difference |
| zoom / backTo1SettleMs | 60.4 [65.3] | 64.5 [76.1] | +4.1 [-6.0, +17.3] | no detectable difference |
| zoom / mainTaskMs | 148.4 [151.0] | 142.4 [144.6] | 0.960 [0.914, 1.015] | no detectable difference |
| zoom / mainJsMs | 98.7 [99.0] | 97.2 [100.9] | 0.984 [0.960, 1.056] | no detectable difference |
| select / settleMs | 148.6 [149.1] | 150.4 [152.1] | +1.8 [-1.4, +3.8] | no detectable difference |
| select / mainTaskMs | 39.7 [47.3] | 42.5 [49.3] | 1.070 [0.860, 1.349] | no detectable difference |
| select / mainJsMs | 19.1 [22.7] | 20.9 [23.8] | 1.092 [0.878, 1.320] | no detectable difference |
| search / searchMs | 54.5 [55.9] | 55.0 [55.7] | 1.009 [0.951, 1.035] | no detectable difference |
| search / mainTaskMs | 2.8 [2.8] | 2.7 [2.9] | 0.963 [0.933, 1.181] | no detectable difference |
| search / mainJsMs | 1.2 [1.3] | 1.3 [1.5] | 1.110 [0.949, 1.342] | unreliable (A/A failed) |
| searchLarge / searchMs | 1618.4 [1624.2] | 1323.9 [1329.2] | 0.818 [0.801, 0.824] | faster |
| searchLarge / mainTaskMs | 226.9 [230.0] | 227.1 [230.8] | 1.001 [0.945, 1.020] | no detectable difference |
| searchLarge / mainJsMs | 191.2 [191.4] | 190.0 [193.2] | 0.994 [0.988, 1.024] | no detectable difference |
| annotate / settleMs | 160.3 [161.2] | 157.2 [157.9] | -3.1 [-5.9, -0.9] | no detectable difference |
| annotate / mainTaskMs | 51.5 [54.6] | 53.1 [53.3] | 1.031 [0.914, 1.057] | no detectable difference |
| annotate / mainJsMs | 24.6 [26.3] | 24.6 [24.7] | 1.001 [0.920, 1.029] | no detectable difference |

## `plugins-tip2` vs `plugins-glyphview`

| Row | plugins-tip2 median [p95] | plugins-glyphview median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 74.6 [74.6] | 74.6 [74.8] | 1.001 [0.985, 1.013] | no detectable difference |
| open(ebook) / openedMs | 172.6 [173.6] | 172.0 [172.9] | 0.996 [0.990, 1.006] | no detectable difference |
| open(ebook) / firstPaintMs | 301.1 [304.7] | 299.3 [299.5] | 0.994 [0.973, 1.015] | no detectable difference |
| open(ebook) / settledMs | 349.8 [353.2] | 345.8 [351.2] | 0.988 [0.977, 1.007] | no detectable difference |
| open(ebook) / mainTaskMs | 235.8 [237.9] | 236.7 [236.8] | 1.004 [0.993, 1.008] | no detectable difference |
| open(ebook) / mainJsMs | 197.9 [198.8] | 198.0 [198.2] | 1.001 [0.991, 1.008] | no detectable difference |
| open(large) / readyMs | 75.4 [79.7] | 74.0 [74.5] | 0.980 [0.916, 0.993] | no detectable difference |
| open(large) / openedMs | 200.2 [202.3] | 198.3 [200.1] | 0.991 [0.974, 1.007] | no detectable difference |
| open(large) / firstPaintMs | 332.9 [334.0] | 327.8 [328.0] | 0.985 [0.979, 0.990] | no detectable difference |
| open(large) / settledMs | 335.2 [336.2] | 327.9 [328.7] | 0.978 [0.972, 0.986] | no detectable difference |
| open(large) / mainTaskMs | 512.9 [514.3] | 505.8 [507.2] | 0.986 [0.982, 1.001] | no detectable difference |
| open(large) / mainJsMs | 475.6 [476.9] | 468.7 [470.9] | 0.985 [0.982, 1.001] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2251.0 [2259.6] | 2252.3 [2267.7] | 1.001 [0.992, 1.026] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.1 [9.3] | 9.0 [9.1] | 0.990 [0.961, 1.039] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 755.8 [766.2] | 757.2 [760.9] | 1.002 [0.982, 1.024] | no detectable difference |
| scroll / mainJsMs | 653.1 [662.3] | 653.1 [654.8] | 1.000 [0.979, 1.021] | no detectable difference |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2220.4 [2236.5] | 2207.6 [2264.4] | 0.994 [0.970, 1.037] | no detectable difference |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 9.3 [9.6] | 9.7 [9.7] | 1.045 [0.997, 1.086] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 797.3 [808.4] | 802.0 [803.2] | 1.006 [0.966, 1.015] | no detectable difference |
| coldScroll / mainJsMs | 681.2 [690.9] | 686.0 [689.8] | 1.007 [0.962, 1.024] | no detectable difference |
| jump / settleMs | 139.4 [145.8] | 142.9 [173.3] | +3.5 [-22.9, +60.0] | no detectable difference |
| jump / mainTaskMs | 153.5 [157.7] | 154.8 [157.0] | 1.008 [0.971, 1.078] | no detectable difference |
| jump / mainJsMs | 136.0 [140.0] | 136.6 [139.6] | 1.005 [0.967, 1.075] | no detectable difference |
| zoom / settleMs | 384.0 [406.3] | 384.4 [395.3] | +0.4 [-25.6, +26.5] | no detectable difference |
| zoom / zoom2SettleMs | 149.2 [150.4] | 150.4 [151.7] | +1.2 [-1.2, +15.9] | no detectable difference |
| zoom / zoom4SettleMs | 234.8 [255.9] | 235.0 [245.0] | +0.2 [-26.8, +12.0] | no detectable difference |
| zoom / backTo1SettleMs | 83.9 [95.4] | 82.4 [85.5] | -1.5 [-18.3, +2.7] | no detectable difference |
| zoom / mainTaskMs | 271.8 [272.5] | 263.4 [279.9] | 0.969 [0.963, 1.045] | no detectable difference |
| zoom / mainJsMs | 226.3 [229.2] | 218.2 [233.9] | 0.964 [0.949, 1.053] | no detectable difference |
| select / settleMs | 142.9 [152.0] | 145.3 [152.0] | +2.4 [-7.9, +17.1] | no detectable difference |
| select / mainTaskMs | 68.5 [70.2] | 69.2 [69.6] | 1.010 [0.968, 1.036] | no detectable difference |
| select / mainJsMs | 53.6 [53.9] | 53.9 [54.4] | 1.005 [0.974, 1.053] | no detectable difference |
| search / searchMs | 60.7 [66.0] | 59.4 [62.3] | 0.978 [0.760, 1.201] | no detectable difference |
| search / mainTaskMs | 9.5 [14.9] | 8.7 [12.7] | 0.907 [0.557, 1.410] | no detectable difference |
| search / mainJsMs | 6.5 [7.5] | 6.5 [7.6] | 0.992 [0.778, 1.220] | unreliable (A/A failed) |
| searchLarge / searchMs | 37925.1 [39480.7] | 38009.9 [38306.3] | 1.002 [0.949, 1.015] | no detectable difference |
| searchLarge / mainTaskMs | 37870.4 [39429.2] | 37967.8 [38265.1] | 1.003 [0.950, 1.016] | no detectable difference |
| searchLarge / mainJsMs | 37843.5 [39402.5] | 37940.6 [38238.2] | 1.003 [0.950, 1.016] | no detectable difference |
| annotate / settleMs | 163.9 [169.1] | 162.0 [162.6] | -1.9 [-8.2, -1.0] | no detectable difference |
| annotate / mainTaskMs | 86.2 [88.6] | 83.2 [84.0] | 0.966 [0.922, 1.003] | no detectable difference |
| annotate / mainJsMs | 63.3 [64.2] | 60.8 [61.9] | 0.960 [0.932, 1.006] | no detectable difference |

## `main` vs `impl-glyphview`

| Row | main median [p95] | impl-glyphview median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.7 [53.0] | 51.9 [51.9] | 0.985 [0.978, 0.993] | no detectable difference |
| open(ebook) / openedMs | 115.0 [115.6] | 115.6 [116.6] | 1.005 [0.997, 1.020] | no detectable difference |
| open(ebook) / firstPaintMs | 214.6 [215.9] | 213.6 [214.8] | 0.995 [0.980, 1.028] | no detectable difference |
| open(ebook) / settledMs | 334.1 [336.0] | 259.9 [260.7] | 0.778 [0.768, 0.797] | faster |
| open(ebook) / mainTaskMs | 118.8 [120.1] | 104.3 [106.7] | 0.878 [0.861, 0.909] | faster |
| open(ebook) / mainJsMs | 92.4 [93.4] | 80.4 [81.9] | 0.871 [0.854, 0.905] | faster |
| open(large) / readyMs | 52.9 [53.0] | 52.3 [52.7] | 0.988 [0.967, 0.997] | no detectable difference |
| open(large) / openedMs | 132.5 [133.1] | 137.4 [137.6] | 1.037 [1.026, 1.048] | slower |
| open(large) / firstPaintMs | 222.4 [223.1] | 219.7 [221.2] | 0.988 [0.974, 0.997] | no detectable difference |
| open(large) / settledMs | 219.2 [220.2] | 212.9 [214.8] | 0.971 [0.945, 0.981] | no detectable difference |
| open(large) / mainTaskMs | 258.3 [258.5] | 158.2 [160.8] | 0.612 [0.609, 0.630] | faster |
| open(large) / mainJsMs | 235.7 [235.7] | 133.2 [134.1] | 0.565 [0.560, 0.575] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2007.2 [2009.4] | 2015.5 [2022.5] | 1.004 [1.002, 1.017] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [9.1] | 8.5 [8.8] | 1.001 [0.914, 1.061] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 407.9 [413.3] | 342.2 [346.2] | 0.839 [0.793, 0.858] | faster |
| scroll / mainJsMs | 313.6 [317.6] | 236.8 [241.2] | 0.755 [0.719, 0.776] | faster |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2013.0 [2023.1] | 2017.5 [2028.2] | 1.002 [0.987, 1.012] | no detectable difference |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 8.9 [9.2] | 8.5 [8.8] | 0.955 [0.912, 1.045] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 445.9 [458.2] | 364.2 [379.0] | 0.817 [0.775, 0.859] | faster |
| coldScroll / mainJsMs | 319.7 [330.7] | 246.0 [256.8] | 0.769 [0.717, 0.810] | faster |
| jump / settleMs | 94.4 [96.4] | 81.9 [82.8] | -12.4 [-17.9, -6.0] | faster |
| jump / mainTaskMs | 55.4 [58.3] | 51.1 [51.8] | 0.922 [0.812, 0.953] | no detectable difference |
| jump / mainJsMs | 41.8 [42.1] | 32.0 [32.2] | 0.765 [0.755, 0.813] | faster |
| zoom / settleMs | 340.9 [341.1] | 351.1 [362.6] | +10.2 [+3.9, +30.5] | no detectable difference |
| zoom / zoom2SettleMs | 132.3 [132.9] | 132.6 [142.6] | +0.4 [-1.2, +15.1] | no detectable difference |
| zoom / zoom4SettleMs | 208.0 [212.1] | 219.3 [220.0] | +11.3 [-0.1, +19.0] | no detectable difference |
| zoom / backTo1SettleMs | 61.8 [78.6] | 64.5 [76.1] | +2.6 [-20.6, +16.0] | no detectable difference |
| zoom / mainTaskMs | 169.4 [169.9] | 142.4 [144.6] | 0.841 [0.814, 0.868] | faster |
| zoom / mainJsMs | 122.8 [123.2] | 97.2 [100.9] | 0.791 [0.772, 0.844] | faster |
| select / settleMs | 147.2 [148.1] | 150.4 [152.1] | +3.2 [-0.4, +5.3] | no detectable difference |
| select / mainTaskMs | 46.6 [48.6] | 42.5 [49.3] | 0.912 [0.847, 1.186] | no detectable difference |
| select / mainJsMs | 27.9 [28.7] | 20.9 [23.8] | 0.749 [0.703, 0.942] | faster |
| search / searchMs | 52.5 [59.3] | 55.0 [55.7] | 1.047 [0.888, 1.089] | no detectable difference |
| search / mainTaskMs | 4.9 [5.2] | 2.7 [2.9] | 0.541 [0.490, 0.653] | faster |
| search / mainJsMs | 3.0 [3.1] | 1.3 [1.5] | 0.436 [0.408, 0.504] | unreliable (A/A failed) |
| searchLarge / searchMs | 1659.8 [1669.6] | 1323.9 [1329.2] | 0.798 [0.779, 0.824] | faster |
| searchLarge / mainTaskMs | 266.6 [268.6] | 227.1 [230.8] | 0.852 [0.810, 0.869] | faster |
| searchLarge / mainJsMs | 232.7 [233.3] | 190.0 [193.2] | 0.816 [0.810, 0.845] | faster |
| annotate / settleMs | 159.3 [162.9] | 157.2 [157.9] | -2.1 [-7.9, +0.5] | no detectable difference |
| annotate / mainTaskMs | 61.1 [62.1] | 53.1 [53.3] | 0.870 [0.807, 0.901] | no detectable difference |
| annotate / mainJsMs | 35.7 [36.0] | 24.6 [24.7] | 0.691 [0.675, 0.721] | faster |

## Memory

| Row | main median [p95] | main-aa median [p95] | impl-now median [p95] | impl-glyphview median [p95] | plugins-tip2 median [p95] | plugins-glyphview median [p95] |
| --- | --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.34 MiB [34.35 MiB] | 34.91 MiB [34.92 MiB] | 34.91 MiB [34.92 MiB] | 41.41 MiB [41.41 MiB] | 41.41 MiB [42.32 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.03 MiB [23.03 MiB] | 23.02 MiB [23.02 MiB] | 23.03 MiB [23.03 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.16 MiB] | 48.15 MiB [48.15 MiB] | 48.59 MiB [48.60 MiB] | 48.61 MiB [48.61 MiB] | 56.22 MiB [56.24 MiB] | 56.22 MiB [56.25 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 23.00 MiB [23.00 MiB] | 23.01 MiB [23.01 MiB] | 22.96 MiB [22.96 MiB] | 22.96 MiB [22.96 MiB] |
| soak / heapCycle5Bytes | 37.43 MiB [37.43 MiB] | 37.41 MiB [37.42 MiB] | 38.06 MiB [38.07 MiB] | 38.06 MiB [38.06 MiB] | 44.23 MiB [45.14 MiB] | 44.21 MiB [44.22 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.28 MiB] | 38.27 MiB [38.27 MiB] | 38.79 MiB [38.80 MiB] | 38.78 MiB [38.78 MiB] | 44.78 MiB [45.68 MiB] | 44.76 MiB [44.77 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-tip2`:

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

`plugins-glyphview`:

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

