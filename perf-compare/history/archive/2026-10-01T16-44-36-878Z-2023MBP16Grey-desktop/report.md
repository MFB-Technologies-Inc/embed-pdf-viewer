# Viewer perf comparison: 2026-10-01T16-44-36-878Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 898be39d258eb25e1e04835c5f75a8cb263e43b8 |
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
| plugins-lazy | research/rust-kernel-lazy-state | f83e60654 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 5c2e682425eb |

## Gate

- `main`: pass
- `main-aa`: pass
- `impl-now`: pass
- `impl-glyphview`: pass
- `plugins-tip2`: pass
- `plugins-lazy`: pass

## Sizes

| Metric | main | main-aa | impl-now | impl-glyphview | plugins-tip2 | plugins-lazy |
| --- | --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 0.00 MiB | 7.94 MiB | 7.94 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 0.00 MiB | 1.70 MiB | 1.71 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 3.36 MiB | 3.36 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

50 of 51 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.5 [53.0] | 52.4 [54.1] | 0.998 [0.977, 1.035] | pass |
| open(ebook) / openedMs | 114.3 [116.2] | 114.7 [116.0] | 1.003 [0.976, 1.021] | pass |
| open(ebook) / firstPaintMs | 215.2 [215.6] | 210.2 [212.7] | 0.977 [0.962, 1.024] | pass |
| open(ebook) / settledMs | 331.8 [332.9] | 328.5 [330.7] | 0.990 [0.986, 0.999] | pass |
| open(ebook) / mainTaskMs | 118.9 [119.3] | 115.3 [116.7] | 0.970 [0.965, 1.008] | pass |
| open(ebook) / mainJsMs | 91.5 [92.3] | 89.2 [90.0] | 0.974 [0.963, 1.010] | pass |
| open(large) / readyMs | 51.9 [52.3] | 53.3 [54.5] | 1.027 [0.999, 1.057] | pass |
| open(large) / openedMs | 132.4 [132.8] | 132.4 [135.4] | 1.000 [0.996, 1.027] | pass |
| open(large) / firstPaintMs | 221.1 [224.7] | 224.2 [226.0] | 1.014 [0.983, 1.032] | pass |
| open(large) / settledMs | 216.3 [217.5] | 221.4 [224.0] | 1.023 [1.003, 1.055] | pass |
| open(large) / mainTaskMs | 260.4 [262.4] | 260.8 [268.2] | 1.002 [0.991, 1.041] | pass |
| open(large) / mainJsMs | 237.6 [238.8] | 237.6 [244.8] | 1.000 [0.993, 1.046] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2011.7 [2014.6] | 2032.5 [2049.8] | 1.010 [1.005, 1.022] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.000] | pass |
| scroll / frameP95Ms | 8.6 [8.7] | 9.0 [9.1] | 1.053 [1.000, 1.071] | A/A failed |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 420.3 [420.4] | 421.8 [423.4] | 1.004 [0.961, 1.045] | pass |
| scroll / mainJsMs | 323.8 [324.9] | 323.1 [326.1] | 0.998 [0.951, 1.052] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2014.5 [2020.2] | 2029.5 [2033.1] | 1.007 [0.993, 1.013] | pass |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| coldScroll / frameP95Ms | 9.0 [9.1] | 8.7 [9.0] | 0.967 [0.930, 1.055] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 467.4 [473.0] | 466.7 [468.4] | 0.999 [0.916, 1.009] | pass |
| coldScroll / mainJsMs | 337.3 [339.1] | 334.6 [335.2] | 0.992 [0.929, 1.007] | pass |
| jump / settleMs | 90.7 [95.1] | 91.0 [94.7] | +0.3 [-7.1, +6.2] | pass |
| jump / mainTaskMs | 55.8 [58.3] | 53.2 [56.6] | 0.953 [0.906, 1.034] | pass |
| jump / mainJsMs | 41.9 [42.4] | 41.6 [41.9] | 0.992 [0.936, 1.018] | pass |
| zoom / settleMs | 340.8 [350.3] | 338.5 [370.9] | -2.4 [-17.0, +43.9] | pass |
| zoom / zoom2SettleMs | 132.0 [135.2] | 132.7 [136.7] | +0.6 [-19.3, +23.0] | pass |
| zoom / zoom4SettleMs | 215.8 [225.5] | 218.0 [235.4] | +2.2 [-20.8, +38.8] | pass |
| zoom / backTo1SettleMs | 68.9 [76.3] | 67.2 [72.9] | -1.7 [-10.5, +6.3] | pass |
| zoom / mainTaskMs | 177.3 [191.6] | 172.6 [180.4] | 0.974 [0.885, 1.080] | pass |
| zoom / mainJsMs | 127.6 [136.5] | 124.4 [126.3] | 0.974 [0.893, 1.053] | pass |
| select / settleMs | 146.1 [146.2] | 146.7 [147.8] | +0.6 [-1.0, +7.7] | pass |
| select / mainTaskMs | 46.7 [47.3] | 48.5 [49.3] | 1.039 [0.977, 1.120] | pass |
| select / mainJsMs | 27.0 [27.6] | 28.8 [29.3] | 1.066 [0.976, 1.092] | pass |
| search / searchMs | 55.4 [57.2] | 60.4 [61.4] | 1.090 [0.905, 1.117] | pass |
| search / mainTaskMs | 4.6 [5.0] | 4.7 [5.2] | 1.024 [0.935, 1.235] | pass |
| search / mainJsMs | 3.0 [3.0] | 3.2 [3.3] | 1.057 [0.962, 1.153] | pass |
| searchLarge / searchMs | 1664.1 [1666.4] | 1653.5 [1657.1] | 0.994 [0.964, 1.007] | pass |
| searchLarge / mainTaskMs | 266.9 [268.0] | 267.6 [267.7] | 1.003 [0.981, 1.010] | pass |
| searchLarge / mainJsMs | 230.9 [231.0] | 230.8 [231.3] | 1.000 [0.985, 1.007] | pass |
| annotate / settleMs | 158.9 [166.5] | 153.0 [157.8] | -5.9 [-17.7, +3.7] | pass |
| annotate / mainTaskMs | 59.5 [62.3] | 57.3 [58.5] | 0.964 [0.865, 1.029] | pass |
| annotate / mainJsMs | 34.4 [35.1] | 31.8 [33.9] | 0.925 [0.857, 1.018] | pass |

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
| open(ebook) / readyMs | 52.8 [54.3] | 52.2 [54.5] | 0.989 [0.957, 1.067] | no detectable difference |
| open(ebook) / openedMs | 115.9 [117.5] | 116.2 [121.1] | 1.003 [0.979, 1.060] | no detectable difference |
| open(ebook) / firstPaintMs | 217.4 [217.6] | 215.4 [219.0] | 0.991 [0.969, 1.033] | no detectable difference |
| open(ebook) / settledMs | 259.4 [259.6] | 259.1 [266.3] | 0.999 [0.994, 1.045] | no detectable difference |
| open(ebook) / mainTaskMs | 103.7 [105.6] | 105.6 [105.9] | 1.018 [0.983, 1.032] | no detectable difference |
| open(ebook) / mainJsMs | 79.7 [80.9] | 81.3 [81.6] | 1.020 [0.987, 1.037] | no detectable difference |
| open(large) / readyMs | 52.1 [52.7] | 52.0 [52.9] | 0.998 [0.959, 1.018] | no detectable difference |
| open(large) / openedMs | 139.1 [139.1] | 136.4 [136.9] | 0.981 [0.976, 0.996] | no detectable difference |
| open(large) / firstPaintMs | 221.3 [225.2] | 216.9 [217.3] | 0.980 [0.959, 0.982] | no detectable difference |
| open(large) / settledMs | 221.3 [223.8] | 212.3 [213.0] | 0.959 [0.946, 0.983] | no detectable difference |
| open(large) / mainTaskMs | 160.8 [161.7] | 159.8 [160.7] | 0.994 [0.985, 1.006] | no detectable difference |
| open(large) / mainJsMs | 133.5 [135.2] | 133.5 [133.9] | 1.000 [0.983, 1.009] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2006.5 [2009.4] | 1998.7 [2006.2] | 0.996 [0.994, 1.008] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.9 [8.9] | 8.5 [8.9] | 0.960 [0.938, 1.055] | unreliable (A/A failed) |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 357.1 [359.0] | 352.5 [354.2] | 0.987 [0.944, 1.046] | no detectable difference |
| scroll / mainJsMs | 244.2 [244.6] | 239.8 [243.1] | 0.982 [0.948, 1.054] | no detectable difference |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2000.1 [2015.7] | 2006.9 [2010.3] | 1.003 [0.987, 1.011] | no detectable difference |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 8.5 [8.7] | 8.5 [8.6] | 1.001 [0.967, 1.020] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 365.2 [371.7] | 369.3 [371.4] | 1.011 [0.977, 1.035] | no detectable difference |
| coldScroll / mainJsMs | 244.6 [252.3] | 247.6 [249.4] | 1.013 [0.969, 1.024] | no detectable difference |
| jump / settleMs | 87.3 [97.1] | 80.1 [94.7] | -7.2 [-19.2, +16.0] | no detectable difference |
| jump / mainTaskMs | 60.0 [62.4] | 56.6 [57.8] | 0.943 [0.809, 1.267] | no detectable difference |
| jump / mainJsMs | 37.2 [37.2] | 33.4 [37.9] | 0.898 [0.865, 1.262] | no detectable difference |
| zoom / settleMs | 346.5 [351.4] | 341.2 [343.7] | -5.4 [-18.6, -0.5] | no detectable difference |
| zoom / zoom2SettleMs | 132.9 [139.1] | 131.0 [131.5] | -1.9 [-12.5, -0.5] | no detectable difference |
| zoom / zoom4SettleMs | 212.3 [213.5] | 209.6 [216.0] | -2.7 [-11.4, +4.5] | no detectable difference |
| zoom / backTo1SettleMs | 63.7 [67.1] | 65.4 [70.1] | +1.7 [-7.5, +7.8] | no detectable difference |
| zoom / mainTaskMs | 150.1 [151.9] | 151.0 [151.9] | 1.006 [0.990, 1.051] | no detectable difference |
| zoom / mainJsMs | 98.8 [106.0] | 97.6 [99.0] | 0.988 [0.890, 1.039] | no detectable difference |
| select / settleMs | 150.2 [150.8] | 148.0 [151.3] | -2.2 [-4.2, +7.2] | no detectable difference |
| select / mainTaskMs | 44.9 [46.7] | 46.2 [46.6] | 1.029 [0.972, 1.106] | no detectable difference |
| select / mainJsMs | 22.4 [23.0] | 22.6 [22.8] | 1.010 [0.957, 1.111] | no detectable difference |
| search / searchMs | 53.4 [55.5] | 53.4 [55.3] | 1.001 [0.950, 1.163] | no detectable difference |
| search / mainTaskMs | 3.0 [3.4] | 2.6 [3.1] | 0.870 [0.743, 1.280] | no detectable difference |
| search / mainJsMs | 1.1 [1.2] | 1.2 [1.3] | 1.077 [0.964, 1.161] | no detectable difference |
| searchLarge / searchMs | 1624.2 [1653.7] | 1336.5 [1348.9] | 0.823 [0.796, 0.836] | faster |
| searchLarge / mainTaskMs | 229.7 [230.0] | 230.6 [230.8] | 1.004 [0.987, 1.048] | no detectable difference |
| searchLarge / mainJsMs | 190.3 [191.0] | 192.2 [193.9] | 1.010 [0.996, 1.020] | no detectable difference |
| annotate / settleMs | 158.2 [158.6] | 158.2 [159.0] | +0.0 [-1.0, +3.7] | no detectable difference |
| annotate / mainTaskMs | 51.7 [52.8] | 53.9 [55.5] | 1.043 [0.937, 1.087] | no detectable difference |
| annotate / mainJsMs | 24.3 [24.5] | 25.3 [25.6] | 1.041 [0.968, 1.061] | no detectable difference |

## `plugins-tip2` vs `plugins-glyphview`

Not run: `plugins-glyphview`.

## `main` vs `impl-glyphview`

| Row | main median [p95] | impl-glyphview median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.5 [53.0] | 52.2 [54.5] | 0.994 [0.982, 1.045] | no detectable difference |
| open(ebook) / openedMs | 114.3 [116.2] | 116.2 [121.1] | 1.016 [0.990, 1.069] | no detectable difference |
| open(ebook) / firstPaintMs | 215.2 [215.6] | 215.4 [219.0] | 1.001 [0.978, 1.055] | no detectable difference |
| open(ebook) / settledMs | 331.8 [332.9] | 259.1 [266.3] | 0.781 [0.775, 0.807] | faster |
| open(ebook) / mainTaskMs | 118.9 [119.3] | 105.6 [105.9] | 0.888 [0.871, 0.914] | faster |
| open(ebook) / mainJsMs | 91.5 [92.3] | 81.3 [81.6] | 0.888 [0.866, 0.916] | faster |
| open(large) / readyMs | 51.9 [52.3] | 52.0 [52.9] | 1.003 [0.966, 1.025] | no detectable difference |
| open(large) / openedMs | 132.4 [132.8] | 136.4 [136.9] | 1.030 [1.022, 1.036] | no detectable difference |
| open(large) / firstPaintMs | 221.1 [224.7] | 216.9 [217.3] | 0.981 [0.961, 0.992] | no detectable difference |
| open(large) / settledMs | 216.3 [217.5] | 212.3 [213.0] | 0.981 [0.974, 1.002] | no detectable difference |
| open(large) / mainTaskMs | 260.4 [262.4] | 159.8 [160.7] | 0.614 [0.607, 0.622] | faster |
| open(large) / mainJsMs | 237.6 [238.8] | 133.5 [133.9] | 0.562 [0.557, 0.571] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2011.7 [2014.6] | 1998.7 [2006.2] | 0.994 [0.992, 1.000] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.6 [8.7] | 8.5 [8.9] | 0.991 [0.956, 1.050] | unreliable (A/A failed) |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 420.3 [420.4] | 352.5 [354.2] | 0.839 [0.807, 0.875] | faster |
| scroll / mainJsMs | 323.8 [324.9] | 239.8 [243.1] | 0.740 [0.713, 0.785] | faster |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2014.5 [2020.2] | 2006.9 [2010.3] | 0.996 [0.986, 1.002] | no detectable difference |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 9.0 [9.1] | 8.5 [8.6] | 0.952 [0.928, 0.994] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 467.4 [473.0] | 369.3 [371.4] | 0.790 [0.768, 0.800] | faster |
| coldScroll / mainJsMs | 337.3 [339.1] | 247.6 [249.4] | 0.734 [0.723, 0.750] | faster |
| jump / settleMs | 90.7 [95.1] | 80.1 [94.7] | -10.6 [-16.6, +7.5] | no detectable difference |
| jump / mainTaskMs | 55.8 [58.3] | 56.6 [57.8] | 1.015 [0.865, 1.053] | no detectable difference |
| jump / mainJsMs | 41.9 [42.4] | 33.4 [37.9] | 0.796 [0.757, 0.932] | faster |
| zoom / settleMs | 340.8 [350.3] | 341.2 [343.7] | +0.3 [-18.0, +13.4] | no detectable difference |
| zoom / zoom2SettleMs | 132.0 [135.2] | 131.0 [131.5] | -1.1 [-8.3, +17.4] | no detectable difference |
| zoom / zoom4SettleMs | 215.8 [225.5] | 209.6 [216.0] | -6.2 [-24.3, +18.2] | no detectable difference |
| zoom / backTo1SettleMs | 68.9 [76.3] | 65.4 [70.1] | -3.4 [-17.1, +3.4] | no detectable difference |
| zoom / mainTaskMs | 177.3 [191.6] | 151.0 [151.9] | 0.852 [0.780, 0.906] | faster |
| zoom / mainJsMs | 127.6 [136.5] | 97.6 [99.0] | 0.765 [0.691, 0.826] | faster |
| select / settleMs | 146.1 [146.2] | 148.0 [151.3] | +1.9 [+0.4, +11.4] | no detectable difference |
| select / mainTaskMs | 46.7 [47.3] | 46.2 [46.6] | 0.991 [0.961, 1.056] | no detectable difference |
| select / mainJsMs | 27.0 [27.6] | 22.6 [22.8] | 0.839 [0.798, 0.847] | faster |
| search / searchMs | 55.4 [57.2] | 53.4 [55.3] | 0.964 [0.922, 1.007] | no detectable difference |
| search / mainTaskMs | 4.6 [5.0] | 2.6 [3.1] | 0.565 [0.509, 0.730] | faster |
| search / mainJsMs | 3.0 [3.0] | 1.2 [1.3] | 0.403 [0.395, 0.449] | faster |
| searchLarge / searchMs | 1664.1 [1666.4] | 1336.5 [1348.9] | 0.803 [0.791, 0.821] | faster |
| searchLarge / mainTaskMs | 266.9 [268.0] | 230.6 [230.8] | 0.864 [0.847, 0.870] | faster |
| searchLarge / mainJsMs | 230.9 [231.0] | 192.2 [193.9] | 0.833 [0.824, 0.845] | faster |
| annotate / settleMs | 158.9 [166.5] | 158.2 [159.0] | -0.7 [-9.6, +4.4] | no detectable difference |
| annotate / mainTaskMs | 59.5 [62.3] | 53.9 [55.5] | 0.907 [0.792, 0.977] | no detectable difference |
| annotate / mainJsMs | 34.4 [35.1] | 25.3 [25.6] | 0.736 [0.674, 0.764] | faster |

## `plugins-tip2` vs `plugins-lazy`

| Row | plugins-tip2 median [p95] | plugins-lazy median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 74.7 [75.2] | 74.4 [76.1] | 0.996 [0.986, 1.022] | no detectable difference |
| open(ebook) / openedMs | 173.1 [174.0] | 171.6 [172.5] | 0.991 [0.980, 1.004] | no detectable difference |
| open(ebook) / firstPaintMs | 299.0 [299.3] | 295.6 [296.4] | 0.989 [0.977, 0.998] | no detectable difference |
| open(ebook) / settledMs | 347.0 [347.3] | 343.3 [344.0] | 0.989 [0.975, 0.997] | no detectable difference |
| open(ebook) / mainTaskMs | 234.9 [236.9] | 233.7 [234.5] | 0.995 [0.964, 1.002] | no detectable difference |
| open(ebook) / mainJsMs | 196.6 [197.6] | 195.0 [195.3] | 0.991 [0.965, 0.998] | no detectable difference |
| open(large) / readyMs | 76.0 [76.2] | 74.6 [74.8] | 0.981 [0.968, 1.006] | no detectable difference |
| open(large) / openedMs | 198.7 [198.9] | 197.1 [198.6] | 0.992 [0.990, 1.015] | no detectable difference |
| open(large) / firstPaintMs | 325.6 [330.4] | 323.9 [324.3] | 0.995 [0.959, 1.007] | no detectable difference |
| open(large) / settledMs | 331.4 [333.4] | 322.8 [325.4] | 0.974 [0.947, 0.983] | no detectable difference |
| open(large) / mainTaskMs | 510.4 [510.5] | 502.7 [503.5] | 0.985 [0.976, 0.993] | no detectable difference |
| open(large) / mainJsMs | 473.1 [473.9] | 464.8 [466.2] | 0.982 [0.975, 0.989] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2265.6 [2268.8] | 2252.9 [2276.8] | 0.994 [0.991, 1.024] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.001] | no detectable difference |
| scroll / frameP95Ms | 9.6 [9.9] | 9.7 [9.8] | 1.015 [0.971, 1.042] | unreliable (A/A failed) |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 759.2 [770.3] | 757.0 [762.1] | 0.997 [0.981, 1.026] | no detectable difference |
| scroll / mainJsMs | 654.2 [664.3] | 652.5 [652.7] | 0.997 [0.973, 1.024] | no detectable difference |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2214.0 [2231.1] | 2259.3 [2275.4] | 1.020 [1.006, 1.036] | no detectable difference |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.001] | no detectable difference |
| coldScroll / frameP95Ms | 9.4 [10.1] | 9.7 [9.8] | 1.030 [0.929, 1.038] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 788.8 [794.4] | 759.9 [785.3] | 0.963 [0.946, 1.010] | no detectable difference |
| coldScroll / mainJsMs | 671.8 [672.4] | 643.3 [661.4] | 0.958 [0.941, 0.998] | no detectable difference |
| jump / settleMs | 135.4 [147.8] | 126.0 [151.0] | -9.4 [-28.0, +20.5] | no detectable difference |
| jump / mainTaskMs | 157.1 [163.3] | 149.0 [151.4] | 0.949 [0.907, 0.980] | no detectable difference |
| jump / mainJsMs | 139.1 [144.8] | 136.7 [137.1] | 0.983 [0.936, 0.997] | no detectable difference |
| zoom / settleMs | 368.8 [400.7] | 386.1 [399.5] | +17.3 [-22.1, +39.1] | no detectable difference |
| zoom / zoom2SettleMs | 141.2 [152.0] | 145.4 [146.4] | +4.2 [-11.5, +8.6] | no detectable difference |
| zoom / zoom4SettleMs | 227.6 [248.8] | 240.7 [253.0] | +13.1 [-10.7, +30.5] | no detectable difference |
| zoom / backTo1SettleMs | 83.9 [104.8] | 85.0 [85.1] | +1.1 [-24.3, +6.4] | no detectable difference |
| zoom / mainTaskMs | 266.0 [281.3] | 265.4 [271.5] | 0.998 [0.927, 1.034] | no detectable difference |
| zoom / mainJsMs | 222.6 [236.8] | 223.4 [227.8] | 1.003 [0.931, 1.031] | no detectable difference |
| select / settleMs | 144.8 [151.4] | 142.9 [145.4] | -1.8 [-9.7, +2.8] | no detectable difference |
| select / mainTaskMs | 66.5 [71.9] | 67.1 [67.2] | 1.008 [0.917, 1.016] | no detectable difference |
| select / mainJsMs | 52.7 [56.1] | 49.9 [50.0] | 0.947 [0.867, 0.962] | no detectable difference |
| search / searchMs | 65.3 [67.4] | 63.1 [64.8] | 0.967 [0.896, 1.031] | no detectable difference |
| search / mainTaskMs | 14.8 [17.5] | 9.3 [12.6] | 0.631 [0.454, 1.049] | no detectable difference |
| search / mainJsMs | 7.2 [7.9] | 6.2 [7.0] | 0.856 [0.678, 0.985] | no detectable difference |
| searchLarge / searchMs | 37907.2 [37970.7] | 2175.6 [2178.0] | 0.057 [0.057, 0.058] | faster |
| searchLarge / mainTaskMs | 37856.3 [37917.6] | 1019.8 [1027.6] | 0.027 [0.027, 0.027] | faster |
| searchLarge / mainJsMs | 37829.2 [37890.6] | 983.1 [986.1] | 0.026 [0.026, 0.026] | faster |
| annotate / settleMs | 162.5 [165.2] | 163.3 [168.8] | +0.7 [-3.8, +7.5] | no detectable difference |
| annotate / mainTaskMs | 81.9 [82.4] | 76.5 [77.3] | 0.934 [0.909, 0.953] | no detectable difference |
| annotate / mainJsMs | 60.7 [60.9] | 54.8 [55.1] | 0.903 [0.893, 0.914] | no detectable difference |

## `main` vs `plugins-lazy`

| Row | main median [p95] | plugins-lazy median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.5 [53.0] | 74.4 [76.1] | 1.418 [1.398, 1.454] | slower |
| open(ebook) / openedMs | 114.3 [116.2] | 171.6 [172.5] | 1.501 [1.464, 1.517] | slower |
| open(ebook) / firstPaintMs | 215.2 [215.6] | 295.6 [296.4] | 1.374 [1.357, 1.425] | slower |
| open(ebook) / settledMs | 331.8 [332.9] | 343.3 [344.0] | 1.035 [1.017, 1.039] | slower |
| open(ebook) / mainTaskMs | 118.9 [119.3] | 233.7 [234.5] | 1.965 [1.915, 2.023] | slower |
| open(ebook) / mainJsMs | 91.5 [92.3] | 195.0 [195.3] | 2.130 [2.066, 2.191] | slower |
| open(large) / readyMs | 51.9 [52.3] | 74.6 [74.8] | 1.439 [1.409, 1.447] | slower |
| open(large) / openedMs | 132.4 [132.8] | 197.1 [198.6] | 1.488 [1.482, 1.504] | slower |
| open(large) / firstPaintMs | 221.1 [224.7] | 323.9 [324.3] | 1.465 [1.409, 1.480] | slower |
| open(large) / settledMs | 216.3 [217.5] | 322.8 [325.4] | 1.492 [1.453, 1.533] | slower |
| open(large) / mainTaskMs | 260.4 [262.4] | 502.7 [503.5] | 1.931 [1.898, 1.949] | slower |
| open(large) / mainJsMs | 237.6 [238.8] | 464.8 [466.2] | 1.956 [1.933, 1.986] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2011.7 [2014.6] | 2252.9 [2276.8] | 1.120 [1.116, 1.136] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.6 [8.7] | 9.7 [9.8] | 1.131 [1.099, 1.162] | unreliable (A/A failed) |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 420.3 [420.4] | 757.0 [762.1] | 1.801 [1.800, 1.883] | slower |
| scroll / mainJsMs | 323.8 [324.9] | 652.5 [652.7] | 2.015 [1.992, 2.104] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2014.5 [2020.2] | 2259.3 [2275.4] | 1.122 [1.112, 1.135] | slower |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 9.0 [9.1] | 9.7 [9.8] | 1.085 [1.038, 1.134] | slower |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 467.4 [473.0] | 759.9 [785.3] | 1.626 [1.587, 1.696] | slower |
| coldScroll / mainJsMs | 337.3 [339.1] | 643.3 [661.4] | 1.907 [1.866, 1.993] | slower |
| jump / settleMs | 90.7 [95.1] | 126.0 [151.0] | +35.3 [+25.6, +64.9] | slower |
| jump / mainTaskMs | 55.8 [58.3] | 149.0 [151.4] | 2.670 [2.539, 2.755] | slower |
| jump / mainJsMs | 41.9 [42.4] | 136.7 [137.1] | 3.260 [3.206, 3.328] | slower |
| zoom / settleMs | 340.8 [350.3] | 386.1 [399.5] | +45.3 [+30.9, +70.4] | slower |
| zoom / zoom2SettleMs | 132.0 [135.2] | 145.4 [146.4] | +13.4 [+6.2, +32.4] | no detectable difference |
| zoom / zoom4SettleMs | 215.8 [225.5] | 240.7 [253.0] | +25.0 [+13.8, +55.9] | no detectable difference |
| zoom / backTo1SettleMs | 68.9 [76.3] | 85.0 [85.1] | +16.1 [+5.7, +17.9] | slower |
| zoom / mainTaskMs | 177.3 [191.6] | 265.4 [271.5] | 1.497 [1.359, 1.622] | slower |
| zoom / mainJsMs | 127.6 [136.5] | 223.4 [227.8] | 1.750 [1.615, 1.900] | slower |
| select / settleMs | 146.1 [146.2] | 142.9 [145.4] | -3.1 [-3.8, +5.4] | no detectable difference |
| select / mainTaskMs | 46.7 [47.3] | 67.1 [67.2] | 1.437 [1.402, 1.523] | slower |
| select / mainJsMs | 27.0 [27.6] | 49.9 [50.0] | 1.848 [1.770, 1.859] | slower |
| search / searchMs | 55.4 [57.2] | 63.1 [64.8] | 1.139 [1.055, 1.181] | slower |
| search / mainTaskMs | 4.6 [5.0] | 9.3 [12.6] | 2.030 [1.611, 3.030] | slower |
| search / mainJsMs | 3.0 [3.0] | 6.2 [7.0] | 2.039 [1.773, 2.449] | slower |
| searchLarge / searchMs | 1664.1 [1666.4] | 2175.6 [2178.0] | 1.307 [1.298, 1.324] | slower |
| searchLarge / mainTaskMs | 266.9 [268.0] | 1019.8 [1027.6] | 3.821 [3.802, 3.878] | slower |
| searchLarge / mainJsMs | 230.9 [231.0] | 983.1 [986.1] | 4.259 [4.256, 4.295] | slower |
| annotate / settleMs | 158.9 [166.5] | 163.3 [168.8] | +4.4 [-5.6, +14.8] | no detectable difference |
| annotate / mainTaskMs | 59.5 [62.3] | 76.5 [77.3] | 1.287 [1.197, 1.358] | slower |
| annotate / mainJsMs | 34.4 [35.1] | 54.8 [55.1] | 1.595 [1.546, 1.642] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | impl-now median [p95] | impl-glyphview median [p95] | plugins-tip2 median [p95] | plugins-lazy median [p95] |
| --- | --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [35.25 MiB] | 34.34 MiB [34.35 MiB] | 34.91 MiB [34.92 MiB] | 34.91 MiB [34.91 MiB] | 41.41 MiB [41.41 MiB] | 41.34 MiB [41.34 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.03 MiB [23.03 MiB] | 23.02 MiB [23.02 MiB] | 23.03 MiB [23.03 MiB] | 23.03 MiB [23.03 MiB] |
| open(large) / heapAfterOpenBytes | 48.16 MiB [48.16 MiB] | 48.16 MiB [48.16 MiB] | 48.61 MiB [48.61 MiB] | 48.60 MiB [48.60 MiB] | 56.22 MiB [56.23 MiB] | 57.60 MiB [57.63 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.27 MiB] | 23.00 MiB [23.00 MiB] | 23.01 MiB [23.01 MiB] | 22.96 MiB [22.96 MiB] | 23.00 MiB [23.03 MiB] |
| soak / heapCycle5Bytes | 37.43 MiB [37.43 MiB] | 37.41 MiB [37.42 MiB] | 38.07 MiB [38.07 MiB] | 38.06 MiB [38.06 MiB] | 44.21 MiB [44.23 MiB] | 44.15 MiB [44.16 MiB] |
| soak / heapCycle30Bytes | 38.26 MiB [38.27 MiB] | 38.27 MiB [38.27 MiB] | 38.79 MiB [38.79 MiB] | 38.78 MiB [38.78 MiB] | 44.76 MiB [44.78 MiB] | 44.70 MiB [44.70 MiB] |
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

