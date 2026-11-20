# Viewer perf comparison: 2026-10-08T14-34-05-698Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 18a99cb32825310e8478b03814fc0b601957e2d6 |
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
| plugins-coalesce | research/rust-kernel-coalesce-notify | c0fcd2746 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | aee9b9460008 |
| plugins-tip6 | research/multi-platform-rust-core-impl-plugins | faa2c6189 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 5bf5fb28d0f2 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-coalesce`: pass
- `plugins-tip6`: pass

## Sizes

| Metric | main | main-aa | plugins-coalesce | plugins-tip6 |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 8.04 MiB | 8.05 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.72 MiB | 1.73 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

62 of 63 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.8 [67.3] | 60.5 [65.8] | 0.949 [0.932, 1.021] | pass |
| open(ebook) / openedMs | 137.6 [144.2] | 136.2 [140.5] | 0.989 [0.973, 1.005] | pass |
| open(ebook) / firstPaintMs | 253.6 [274.0] | 250.4 [257.4] | 0.987 [0.958, 1.008] | pass |
| open(ebook) / settledMs | 395.3 [398.3] | 393.3 [396.4] | 0.995 [0.988, 1.004] | pass |
| open(ebook) / mainTaskMs | 143.8 [147.7] | 143.9 [146.9] | 1.000 [0.962, 1.018] | pass |
| open(ebook) / mainJsMs | 110.6 [113.8] | 110.0 [113.1] | 0.995 [0.965, 1.015] | pass |
| open(large) / readyMs | 62.5 [66.4] | 61.2 [63.7] | 0.979 [0.937, 1.004] | pass |
| open(large) / openedMs | 159.1 [163.1] | 157.6 [160.4] | 0.990 [0.976, 1.006] | pass |
| open(large) / firstPaintMs | 261.8 [270.6] | 265.7 [284.0] | 1.015 [0.991, 1.047] | pass |
| open(large) / settledMs | 264.7 [269.7] | 268.6 [280.6] | 1.015 [0.980, 1.040] | pass |
| open(large) / mainTaskMs | 313.8 [320.2] | 314.1 [317.3] | 1.001 [0.994, 1.010] | pass |
| open(large) / mainJsMs | 283.1 [288.9] | 284.0 [287.0] | 1.003 [0.996, 1.011] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2866.0 [2980.6] | 2875.5 [2940.5] | 1.003 [0.977, 1.025] | pass |
| scroll / frameP50Ms | 11.0 [11.9] | 11.0 [11.4] | 0.994 [0.932, 1.033] | pass |
| scroll / frameP95Ms | 23.7 [27.2] | 23.5 [27.1] | 0.990 [0.840, 1.169] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 546.8 [569.7] | 545.7 [559.0] | 0.998 [0.983, 1.026] | pass |
| scroll / mainJsMs | 411.5 [429.5] | 408.8 [420.2] | 0.993 [0.972, 1.029] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2855.6 [2926.3] | 2817.6 [2911.8] | 0.987 [0.968, 1.021] | pass |
| coldScroll / frameP50Ms | 11.0 [11.5] | 11.1 [11.6] | 1.006 [0.976, 1.031] | pass |
| coldScroll / frameP95Ms | 24.1 [26.2] | 26.2 [27.2] | 1.087 [0.971, 1.224] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 566.3 [586.0] | 565.6 [586.4] | 0.999 [0.961, 1.032] | pass |
| coldScroll / mainJsMs | 396.6 [411.7] | 394.4 [411.5] | 0.994 [0.957, 1.038] | pass |
| jump / settleMs | 126.3 [130.8] | 120.4 [123.7] | -5.9 [-8.4, +0.1] | pass |
| jump / mainTaskMs | 71.0 [75.4] | 74.2 [76.4] | 1.045 [1.008, 1.066] | A/A failed |
| jump / mainJsMs | 54.2 [58.1] | 54.1 [57.9] | 0.998 [0.949, 1.050] | pass |
| zoom / settleMs | 456.7 [481.3] | 449.0 [485.3] | -7.7 [-21.4, +10.2] | pass |
| zoom / zoom2SettleMs | 163.9 [167.5] | 161.0 [168.8] | -2.9 [-8.3, +3.0] | pass |
| zoom / zoom4SettleMs | 295.1 [314.0] | 292.4 [317.3] | -2.7 [-14.9, +12.9] | pass |
| zoom / backTo1SettleMs | 88.0 [109.9] | 89.2 [95.1] | +1.2 [-11.4, +7.0] | pass |
| zoom / mainTaskMs | 220.4 [228.4] | 218.3 [225.1] | 0.990 [0.969, 1.039] | pass |
| zoom / mainJsMs | 156.1 [163.1] | 159.4 [164.7] | 1.021 [0.978, 1.044] | pass |
| zoomLarge / settleMs | 306.4 [348.9] | 294.1 [315.7] | -12.4 [-33.5, +3.0] | pass |
| zoomLarge / zoom2SettleMs | 144.8 [162.0] | 133.3 [160.4] | -11.6 [-25.0, +6.9] | pass |
| zoomLarge / zoom4SettleMs | 167.0 [203.2] | 151.3 [175.3] | -15.7 [-37.6, +17.6] | pass |
| zoomLarge / backTo1SettleMs | 77.2 [92.8] | 74.9 [81.1] | -2.3 [-9.1, +4.4] | pass |
| zoomLarge / mainTaskMs | 182.0 [186.4] | 178.4 [182.3] | 0.980 [0.961, 1.026] | pass |
| zoomLarge / mainJsMs | 134.7 [138.7] | 132.8 [137.1] | 0.985 [0.957, 1.027] | pass |
| select / settleMs | 237.7 [254.6] | 222.0 [275.1] | -15.7 [-40.2, +20.6] | pass |
| select / mainTaskMs | 61.9 [68.9] | 63.6 [71.3] | 1.027 [0.931, 1.107] | pass |
| select / mainJsMs | 40.2 [44.8] | 41.6 [47.3] | 1.034 [0.923, 1.124] | pass |
| selectLarge / settleMs | 223.5 [252.3] | 223.8 [247.7] | +0.3 [-44.6, +25.5] | pass |
| selectLarge / mainTaskMs | 71.7 [75.7] | 65.4 [75.4] | 0.912 [0.870, 1.019] | pass |
| selectLarge / mainJsMs | 48.4 [51.6] | 43.9 [51.4] | 0.907 [0.862, 1.025] | pass |
| search / searchMs | 70.0 [80.4] | 75.6 [82.1] | 1.080 [0.997, 1.188] | pass |
| search / mainTaskMs | 5.3 [6.5] | 5.4 [6.0] | 1.033 [0.963, 1.103] | pass |
| search / mainJsMs | 3.7 [4.3] | 3.7 [4.0] | 1.002 [0.969, 1.040] | pass |
| searchLarge / searchMs | 1887.4 [1994.9] | 1884.7 [1956.6] | 0.999 [0.979, 1.023] | pass |
| searchLarge / mainTaskMs | 343.8 [347.2] | 342.6 [349.4] | 0.996 [0.988, 1.005] | pass |
| searchLarge / mainJsMs | 290.2 [292.1] | 290.1 [294.2] | 1.000 [0.993, 1.007] | pass |
| annotate / settleMs | 218.7 [256.9] | 204.5 [254.5] | -14.2 [-52.9, +18.7] | pass |
| annotate / mainTaskMs | 77.9 [84.5] | 75.1 [81.3] | 0.964 [0.898, 1.042] | pass |
| annotate / mainJsMs | 47.6 [51.6] | 45.6 [49.5] | 0.958 [0.895, 1.043] | pass |
| annotateLarge / settleMs | 213.6 [250.4] | 232.9 [267.1] | +19.3 [-13.8, +54.6] | pass |
| annotateLarge / mainTaskMs | 82.6 [85.9] | 85.2 [87.8] | 1.031 [0.984, 1.100] | pass |
| annotateLarge / mainJsMs | 49.4 [52.6] | 50.9 [53.1] | 1.031 [0.972, 1.093] | pass |

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

Not run: `plugins-tip5`, `plugins-geomdecode`.

## `main` vs `plugins-geomdecode`

Not run: `plugins-geomdecode`.

## `plugins-tip5` vs `plugins-coalesce`

Not run: `plugins-tip5`.

## `main` vs `plugins-coalesce`

| Row | main median [p95] | plugins-coalesce median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.8 [67.3] | 93.6 [107.0] | 1.468 [1.429, 1.601] | slower |
| open(ebook) / openedMs | 137.6 [144.2] | 179.7 [194.5] | 1.305 [1.290, 1.355] | slower |
| open(ebook) / firstPaintMs | 253.6 [274.0] | 333.2 [348.3] | 1.314 [1.273, 1.337] | slower |
| open(ebook) / settledMs | 395.3 [398.3] | 403.5 [415.5] | 1.021 [0.999, 1.034] | no detectable difference |
| open(ebook) / mainTaskMs | 143.8 [147.7] | 243.1 [248.1] | 1.690 [1.652, 1.716] | slower |
| open(ebook) / mainJsMs | 110.6 [113.8] | 194.7 [198.8] | 1.760 [1.732, 1.785] | slower |
| open(large) / readyMs | 62.5 [66.4] | 90.7 [98.2] | 1.450 [1.399, 1.494] | slower |
| open(large) / openedMs | 159.1 [163.1] | 208.5 [214.4] | 1.310 [1.292, 1.332] | slower |
| open(large) / firstPaintMs | 261.8 [270.6] | 365.1 [371.5] | 1.394 [1.314, 1.411] | slower |
| open(large) / settledMs | 264.7 [269.7] | 356.4 [370.1] | 1.346 [1.326, 1.381] | slower |
| open(large) / mainTaskMs | 313.8 [320.2] | 305.2 [307.1] | 0.972 [0.965, 0.977] | no detectable difference |
| open(large) / mainJsMs | 283.1 [288.9] | 258.0 [259.9] | 0.911 [0.906, 0.917] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [16.8] | +0.0 [+0.0, +5.2] | no detectable difference |
| scroll / wallMs | 2866.0 [2980.6] | 2582.2 [2643.4] | 0.901 [0.879, 0.924] | faster |
| scroll / frameP50Ms | 11.0 [11.9] | 11.0 [11.4] | 0.995 [0.932, 1.033] | no detectable difference |
| scroll / frameP95Ms | 23.7 [27.2] | 25.5 [27.2] | 1.074 [0.908, 1.232] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 546.8 [569.7] | 766.7 [789.5] | 1.402 [1.357, 1.459] | slower |
| scroll / mainJsMs | 411.5 [429.5] | 644.6 [663.8] | 1.566 [1.495, 1.637] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [7.9] | +0.0 [+0.0, +2.4] | no detectable difference |
| coldScroll / wallMs | 2855.6 [2926.3] | 2576.9 [2733.8] | 0.902 [0.864, 0.940] | faster |
| coldScroll / frameP50Ms | 11.0 [11.5] | 11.1 [11.3] | 1.003 [0.972, 1.027] | no detectable difference |
| coldScroll / frameP95Ms | 24.1 [26.2] | 26.0 [27.2] | 1.081 [0.850, 1.238] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 566.3 [586.0] | 774.9 [794.3] | 1.368 [1.337, 1.414] | slower |
| coldScroll / mainJsMs | 396.6 [411.7] | 634.3 [653.4] | 1.600 [1.565, 1.654] | slower |
| jump / settleMs | 126.3 [130.8] | 138.3 [155.4] | +12.0 [+5.6, +21.2] | slower |
| jump / mainTaskMs | 71.0 [75.4] | 133.3 [135.4] | 1.877 [1.808, 1.909] | unreliable (A/A failed) |
| jump / mainJsMs | 54.2 [58.1] | 116.6 [118.3] | 2.150 [2.068, 2.234] | slower |
| zoom / settleMs | 456.7 [481.3] | 517.7 [529.6] | +61.0 [+48.9, +78.8] | slower |
| zoom / zoom2SettleMs | 163.9 [167.5] | 196.2 [220.9] | +32.3 [+23.2, +46.8] | slower |
| zoom / zoom4SettleMs | 295.1 [314.0] | 321.0 [335.9] | +25.9 [+15.2, +41.3] | slower |
| zoom / backTo1SettleMs | 88.0 [109.9] | 100.5 [109.6] | +12.6 [+0.0, +21.2] | slower |
| zoom / mainTaskMs | 220.4 [228.4] | 318.3 [331.3] | 1.444 [1.421, 1.522] | slower |
| zoom / mainJsMs | 156.1 [163.1] | 260.9 [268.2] | 1.671 [1.606, 1.722] | slower |
| zoomLarge / settleMs | 306.4 [348.9] | 323.2 [375.3] | +16.7 [+2.8, +52.0] | no detectable difference |
| zoomLarge / zoom2SettleMs | 144.8 [162.0] | 163.8 [179.8] | +18.9 [-10.8, +37.5] | no detectable difference |
| zoomLarge / zoom4SettleMs | 167.0 [203.2] | 171.7 [204.4] | +4.7 [-21.7, +34.7] | no detectable difference |
| zoomLarge / backTo1SettleMs | 77.2 [92.8] | 87.2 [99.1] | +10.0 [+2.6, +16.2] | slower |
| zoomLarge / mainTaskMs | 182.0 [186.4] | 266.7 [271.7] | 1.465 [1.413, 1.536] | slower |
| zoomLarge / mainJsMs | 134.7 [138.7] | 223.9 [229.8] | 1.662 [1.592, 1.729] | slower |
| select / settleMs | 237.7 [254.6] | 263.3 [285.4] | +25.6 [+8.3, +52.5] | no detectable difference |
| select / mainTaskMs | 61.9 [68.9] | 91.4 [95.3] | 1.476 [1.374, 1.558] | slower |
| select / mainJsMs | 40.2 [44.8] | 71.6 [75.4] | 1.778 [1.655, 1.924] | slower |
| selectLarge / settleMs | 223.5 [252.3] | 277.2 [315.9] | +53.8 [+14.3, +85.9] | slower |
| selectLarge / mainTaskMs | 71.7 [75.7] | 112.2 [121.7] | 1.565 [1.434, 1.703] | slower |
| selectLarge / mainJsMs | 48.4 [51.6] | 86.1 [92.5] | 1.781 [1.630, 1.991] | slower |
| search / searchMs | 70.0 [80.4] | 80.0 [82.5] | 1.143 [1.016, 1.232] | no detectable difference |
| search / mainTaskMs | 5.3 [6.5] | 15.6 [21.4] | 2.955 [1.872, 3.944] | slower |
| search / mainJsMs | 3.7 [4.3] | 8.2 [8.5] | 2.248 [1.880, 2.312] | slower |
| searchLarge / searchMs | 1887.4 [1994.9] | 1832.6 [1841.1] | 0.971 [0.952, 0.977] | no detectable difference |
| searchLarge / mainTaskMs | 343.8 [347.2] | 869.5 [877.3] | 2.529 [2.510, 2.545] | slower |
| searchLarge / mainJsMs | 290.2 [292.1] | 820.7 [828.4] | 2.828 [2.811, 2.846] | slower |
| annotate / settleMs | 218.7 [256.9] | 231.9 [269.4] | +13.2 [-21.4, +39.7] | no detectable difference |
| annotate / mainTaskMs | 77.9 [84.5] | 92.8 [101.0] | 1.191 [1.107, 1.264] | slower |
| annotate / mainJsMs | 47.6 [51.6] | 65.8 [73.0] | 1.384 [1.287, 1.486] | slower |
| annotateLarge / settleMs | 213.6 [250.4] | 233.1 [281.6] | +19.5 [-19.2, +68.5] | no detectable difference |
| annotateLarge / mainTaskMs | 82.6 [85.9] | 114.3 [122.0] | 1.383 [1.310, 1.503] | slower |
| annotateLarge / mainJsMs | 49.4 [52.6] | 72.2 [80.9] | 1.462 [1.390, 1.612] | slower |

## `plugins-coalesce` vs `plugins-tip6`

| Row | plugins-coalesce median [p95] | plugins-tip6 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 93.6 [107.0] | 90.1 [95.7] | 0.962 [0.890, 0.983] | no detectable difference |
| open(ebook) / openedMs | 179.7 [194.5] | 177.1 [183.7] | 0.985 [0.947, 1.004] | no detectable difference |
| open(ebook) / firstPaintMs | 333.2 [348.3] | 339.1 [354.8] | 1.018 [0.989, 1.040] | no detectable difference |
| open(ebook) / settledMs | 403.5 [415.5] | 397.9 [408.7] | 0.986 [0.969, 1.008] | no detectable difference |
| open(ebook) / mainTaskMs | 243.1 [248.1] | 242.4 [249.5] | 0.997 [0.987, 1.016] | no detectable difference |
| open(ebook) / mainJsMs | 194.7 [198.8] | 195.0 [200.4] | 1.001 [0.981, 1.023] | no detectable difference |
| open(large) / readyMs | 90.7 [98.2] | 90.0 [92.4] | 0.993 [0.957, 1.004] | no detectable difference |
| open(large) / openedMs | 208.5 [214.4] | 205.9 [210.7] | 0.987 [0.973, 1.004] | no detectable difference |
| open(large) / firstPaintMs | 365.1 [371.5] | 366.7 [391.5] | 1.004 [0.975, 1.067] | no detectable difference |
| open(large) / settledMs | 356.4 [370.1] | 355.2 [366.8] | 0.997 [0.977, 1.012] | no detectable difference |
| open(large) / mainTaskMs | 305.2 [307.1] | 297.0 [299.8] | 0.973 [0.966, 0.980] | no detectable difference |
| open(large) / mainJsMs | 258.0 [259.9] | 248.9 [253.0] | 0.964 [0.961, 0.975] | faster |
| scroll / settleMs | 0.0 [16.8] | 0.0 [6.8] | +0.0 [-5.2, +1.5] | no detectable difference |
| scroll / wallMs | 2582.2 [2643.4] | 2589.7 [2696.1] | 1.003 [0.962, 1.030] | no detectable difference |
| scroll / frameP50Ms | 11.0 [11.4] | 11.0 [11.5] | 1.005 [0.982, 1.026] | no detectable difference |
| scroll / frameP95Ms | 25.5 [27.2] | 26.2 [27.0] | 1.027 [0.941, 1.155] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 766.7 [789.5] | 759.2 [770.3] | 0.990 [0.964, 1.024] | no detectable difference |
| scroll / mainJsMs | 644.6 [663.8] | 622.7 [631.1] | 0.966 [0.933, 1.011] | no detectable difference |
| coldScroll / settleMs | 0.0 [7.9] | 0.0 [3.2] | +0.0 [-1.9, +0.9] | no detectable difference |
| coldScroll / wallMs | 2576.9 [2733.8] | 2504.0 [2665.4] | 0.972 [0.938, 1.024] | no detectable difference |
| coldScroll / frameP50Ms | 11.1 [11.3] | 11.0 [11.4] | 0.992 [0.974, 1.017] | no detectable difference |
| coldScroll / frameP95Ms | 26.0 [27.2] | 26.3 [27.2] | 1.009 [0.879, 1.237] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 774.9 [794.3] | 763.7 [779.6] | 0.986 [0.964, 1.002] | no detectable difference |
| coldScroll / mainJsMs | 634.3 [653.4] | 612.7 [624.4] | 0.966 [0.943, 0.983] | no detectable difference |
| jump / settleMs | 138.3 [155.4] | 137.2 [148.1] | -1.1 [-13.4, +9.5] | no detectable difference |
| jump / mainTaskMs | 133.3 [135.4] | 119.2 [124.3] | 0.894 [0.879, 0.927] | unreliable (A/A failed) |
| jump / mainJsMs | 116.6 [118.3] | 103.9 [106.1] | 0.891 [0.861, 0.909] | faster |
| zoom / settleMs | 517.7 [529.6] | 525.3 [542.6] | +7.6 [-12.2, +14.5] | no detectable difference |
| zoom / zoom2SettleMs | 196.2 [220.9] | 193.4 [210.4] | -2.7 [-16.5, +8.8] | no detectable difference |
| zoom / zoom4SettleMs | 321.0 [335.9] | 325.1 [342.2] | +4.1 [-9.6, +21.9] | no detectable difference |
| zoom / backTo1SettleMs | 100.5 [109.6] | 96.5 [115.8] | -4.1 [-11.4, +4.6] | no detectable difference |
| zoom / mainTaskMs | 318.3 [331.3] | 319.4 [331.5] | 1.003 [0.974, 1.024] | no detectable difference |
| zoom / mainJsMs | 260.9 [268.2] | 263.3 [272.9] | 1.009 [0.974, 1.029] | no detectable difference |
| zoomLarge / settleMs | 323.2 [375.3] | 323.3 [347.9] | +0.1 [-37.7, +17.8] | no detectable difference |
| zoomLarge / zoom2SettleMs | 163.8 [179.8] | 137.8 [166.7] | -25.9 [-39.8, +14.8] | no detectable difference |
| zoomLarge / zoom4SettleMs | 171.7 [204.4] | 181.2 [189.7] | +9.5 [-5.6, +28.2] | no detectable difference |
| zoomLarge / backTo1SettleMs | 87.2 [99.1] | 85.3 [98.9] | -1.9 [-5.0, +6.2] | no detectable difference |
| zoomLarge / mainTaskMs | 266.7 [271.7] | 258.9 [267.6] | 0.971 [0.946, 1.016] | no detectable difference |
| zoomLarge / mainJsMs | 223.9 [229.8] | 217.0 [225.0] | 0.969 [0.939, 1.009] | no detectable difference |
| select / settleMs | 263.3 [285.4] | 247.9 [301.0] | -15.3 [-35.4, +28.9] | no detectable difference |
| select / mainTaskMs | 91.4 [95.3] | 89.2 [104.6] | 0.976 [0.932, 1.087] | no detectable difference |
| select / mainJsMs | 71.6 [75.4] | 69.6 [83.6] | 0.973 [0.916, 1.107] | no detectable difference |
| selectLarge / settleMs | 277.2 [315.9] | 299.2 [333.4] | +22.0 [-23.6, +61.1] | no detectable difference |
| selectLarge / mainTaskMs | 112.2 [121.7] | 114.4 [120.8] | 1.020 [0.947, 1.105] | no detectable difference |
| selectLarge / mainJsMs | 86.1 [92.5] | 86.4 [91.3] | 1.003 [0.893, 1.086] | no detectable difference |
| search / searchMs | 80.0 [82.5] | 72.6 [82.5] | 0.908 [0.862, 1.022] | no detectable difference |
| search / mainTaskMs | 15.6 [21.4] | 9.7 [14.9] | 0.623 [0.469, 1.024] | no detectable difference |
| search / mainJsMs | 8.2 [8.5] | 6.8 [9.1] | 0.821 [0.783, 0.986] | faster |
| searchLarge / searchMs | 1832.6 [1841.1] | 1836.7 [1840.2] | 1.002 [0.998, 1.005] | no detectable difference |
| searchLarge / mainTaskMs | 869.5 [877.3] | 868.0 [872.4] | 0.998 [0.993, 1.004] | no detectable difference |
| searchLarge / mainJsMs | 820.7 [828.4] | 821.5 [824.2] | 1.001 [0.995, 1.005] | no detectable difference |
| annotate / settleMs | 231.9 [269.4] | 220.2 [261.4] | -11.7 [-49.3, +16.8] | no detectable difference |
| annotate / mainTaskMs | 92.8 [101.0] | 88.6 [102.2] | 0.955 [0.870, 1.070] | no detectable difference |
| annotate / mainJsMs | 65.8 [73.0] | 62.5 [73.8] | 0.950 [0.856, 1.081] | no detectable difference |
| annotateLarge / settleMs | 233.1 [281.6] | 253.5 [314.6] | +20.4 [-48.8, +57.0] | no detectable difference |
| annotateLarge / mainTaskMs | 114.3 [122.0] | 120.3 [131.2] | 1.053 [0.934, 1.133] | no detectable difference |
| annotateLarge / mainJsMs | 72.2 [80.9] | 76.3 [86.8] | 1.057 [0.913, 1.165] | no detectable difference |

## `main` vs `plugins-tip6`

| Row | main median [p95] | plugins-tip6 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.8 [67.3] | 90.1 [95.7] | 1.413 [1.387, 1.444] | slower |
| open(ebook) / openedMs | 137.6 [144.2] | 177.1 [183.7] | 1.287 [1.270, 1.308] | slower |
| open(ebook) / firstPaintMs | 253.6 [274.0] | 339.1 [354.8] | 1.338 [1.291, 1.366] | slower |
| open(ebook) / settledMs | 395.3 [398.3] | 397.9 [408.7] | 1.007 [0.992, 1.016] | no detectable difference |
| open(ebook) / mainTaskMs | 143.8 [147.7] | 242.4 [249.5] | 1.686 [1.647, 1.724] | slower |
| open(ebook) / mainJsMs | 110.6 [113.8] | 195.0 [200.4] | 1.763 [1.722, 1.802] | slower |
| open(large) / readyMs | 62.5 [66.4] | 90.0 [92.4] | 1.439 [1.393, 1.463] | slower |
| open(large) / openedMs | 159.1 [163.1] | 205.9 [210.7] | 1.294 [1.278, 1.315] | slower |
| open(large) / firstPaintMs | 261.8 [270.6] | 366.7 [391.5] | 1.401 [1.353, 1.431] | slower |
| open(large) / settledMs | 264.7 [269.7] | 355.2 [366.8] | 1.342 [1.326, 1.372] | slower |
| open(large) / mainTaskMs | 313.8 [320.2] | 297.0 [299.8] | 0.946 [0.937, 0.953] | faster |
| open(large) / mainJsMs | 283.1 [288.9] | 248.9 [253.0] | 0.879 [0.874, 0.889] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [6.8] | +0.0 [+0.0, +1.5] | no detectable difference |
| scroll / wallMs | 2866.0 [2980.6] | 2589.7 [2696.1] | 0.904 [0.868, 0.926] | faster |
| scroll / frameP50Ms | 11.0 [11.9] | 11.0 [11.5] | 1.000 [0.937, 1.035] | no detectable difference |
| scroll / frameP95Ms | 23.7 [27.2] | 26.2 [27.0] | 1.103 [0.969, 1.241] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 546.8 [569.7] | 759.2 [770.3] | 1.388 [1.369, 1.426] | slower |
| scroll / mainJsMs | 411.5 [429.5] | 622.7 [631.1] | 1.513 [1.481, 1.560] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [3.2] | +0.0 [+0.0, +1.5] | no detectable difference |
| coldScroll / wallMs | 2855.6 [2926.3] | 2504.0 [2665.4] | 0.877 [0.853, 0.919] | faster |
| coldScroll / frameP50Ms | 11.0 [11.5] | 11.0 [11.4] | 0.995 [0.964, 1.023] | no detectable difference |
| coldScroll / frameP95Ms | 24.1 [26.2] | 26.3 [27.2] | 1.091 [0.936, 1.257] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 566.3 [586.0] | 763.7 [779.6] | 1.348 [1.319, 1.389] | slower |
| coldScroll / mainJsMs | 396.6 [411.7] | 612.7 [624.4] | 1.545 [1.508, 1.597] | slower |
| jump / settleMs | 126.3 [130.8] | 137.2 [148.1] | +10.9 [+1.1, +20.6] | slower |
| jump / mainTaskMs | 71.0 [75.4] | 119.2 [124.3] | 1.679 [1.623, 1.737] | unreliable (A/A failed) |
| jump / mainJsMs | 54.2 [58.1] | 103.9 [106.1] | 1.916 [1.823, 1.993] | slower |
| zoom / settleMs | 456.7 [481.3] | 525.3 [542.6] | +68.6 [+49.3, +82.6] | slower |
| zoom / zoom2SettleMs | 163.9 [167.5] | 193.4 [210.4] | +29.5 [+26.1, +39.2] | slower |
| zoom / zoom4SettleMs | 295.1 [314.0] | 325.1 [342.2] | +30.0 [+16.0, +52.2] | slower |
| zoom / backTo1SettleMs | 88.0 [109.9] | 96.5 [115.8] | +8.5 [-2.1, +16.6] | no detectable difference |
| zoom / mainTaskMs | 220.4 [228.4] | 319.4 [331.5] | 1.449 [1.422, 1.518] | slower |
| zoom / mainJsMs | 156.1 [163.1] | 263.3 [272.9] | 1.686 [1.614, 1.723] | slower |
| zoomLarge / settleMs | 306.4 [348.9] | 323.3 [347.9] | +16.9 [-0.2, +33.0] | no detectable difference |
| zoomLarge / zoom2SettleMs | 144.8 [162.0] | 137.8 [166.7] | -7.0 [-21.5, +21.8] | no detectable difference |
| zoomLarge / zoom4SettleMs | 167.0 [203.2] | 181.2 [189.7] | +14.2 [-5.4, +40.6] | no detectable difference |
| zoomLarge / backTo1SettleMs | 77.2 [92.8] | 85.3 [98.9] | +8.1 [+2.1, +15.6] | no detectable difference |
| zoomLarge / mainTaskMs | 182.0 [186.4] | 258.9 [267.6] | 1.422 [1.384, 1.508] | slower |
| zoomLarge / mainJsMs | 134.7 [138.7] | 217.0 [225.0] | 1.610 [1.554, 1.675] | slower |
| select / settleMs | 237.7 [254.6] | 247.9 [301.0] | +10.2 [-11.5, +57.5] | no detectable difference |
| select / mainTaskMs | 61.9 [68.9] | 89.2 [104.6] | 1.441 [1.337, 1.613] | slower |
| select / mainJsMs | 40.2 [44.8] | 69.6 [83.6] | 1.731 [1.596, 1.991] | slower |
| selectLarge / settleMs | 223.5 [252.3] | 299.2 [333.4] | +75.7 [+27.2, +108.0] | slower |
| selectLarge / mainTaskMs | 71.7 [75.7] | 114.4 [120.8] | 1.596 [1.491, 1.717] | slower |
| selectLarge / mainJsMs | 48.4 [51.6] | 86.4 [91.3] | 1.787 [1.615, 1.948] | slower |
| search / searchMs | 70.0 [80.4] | 72.6 [82.5] | 1.038 [0.950, 1.171] | no detectable difference |
| search / mainTaskMs | 5.3 [6.5] | 9.7 [14.9] | 1.840 [1.733, 2.036] | slower |
| search / mainJsMs | 3.7 [4.3] | 6.8 [9.1] | 1.845 [1.751, 1.946] | slower |
| searchLarge / searchMs | 1887.4 [1994.9] | 1836.7 [1840.2] | 0.973 [0.953, 0.979] | no detectable difference |
| searchLarge / mainTaskMs | 343.8 [347.2] | 868.0 [872.4] | 2.525 [2.505, 2.540] | slower |
| searchLarge / mainJsMs | 290.2 [292.1] | 821.5 [824.2] | 2.831 [2.812, 2.847] | slower |
| annotate / settleMs | 218.7 [256.9] | 220.2 [261.4] | +1.5 [-42.7, +30.1] | no detectable difference |
| annotate / mainTaskMs | 77.9 [84.5] | 88.6 [102.2] | 1.137 [1.022, 1.278] | slower |
| annotate / mainJsMs | 47.6 [51.6] | 62.5 [73.8] | 1.314 [1.170, 1.513] | slower |
| annotateLarge / settleMs | 213.6 [250.4] | 253.5 [314.6] | +39.9 [-26.4, +79.3] | no detectable difference |
| annotateLarge / mainTaskMs | 82.6 [85.9] | 120.3 [131.2] | 1.456 [1.303, 1.588] | slower |
| annotateLarge / mainJsMs | 49.4 [52.6] | 76.3 [86.8] | 1.545 [1.356, 1.735] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-coalesce median [p95] | plugins-tip6 median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 35.34 MiB [35.35 MiB] | 35.34 MiB [35.36 MiB] | 36.85 MiB [37.42 MiB] | 37.05 MiB [38.05 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.17 MiB [48.18 MiB] | 48.15 MiB [48.17 MiB] | 52.08 MiB [52.10 MiB] | 52.51 MiB [52.54 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.27 MiB [22.28 MiB] | 22.26 MiB [22.29 MiB] | 23.01 MiB [23.03 MiB] | 23.00 MiB [23.03 MiB] |
| soak / heapCycle5Bytes | 37.92 MiB [38.97 MiB] | 37.92 MiB [38.43 MiB] | 39.74 MiB [40.74 MiB] | 40.23 MiB [41.22 MiB] |
| soak / heapCycle30Bytes | 38.76 MiB [39.83 MiB] | 38.76 MiB [39.27 MiB] | 40.28 MiB [41.28 MiB] | 40.76 MiB [41.76 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

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

`plugins-tip6`:

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
| wasmBytes | 3.19 MiB | 3.19 MiB |
| wireRefs | 658 | 658 |

