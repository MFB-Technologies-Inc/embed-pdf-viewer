# Viewer perf comparison: 2026-10-13T02-28-33-711Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | aca9e243fcf786dc33698884297faabc461e9c1c |
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
| plugins-resulttext | research/rust-kernel-result-text | 34644a2b4 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 63a60ebf8d9a |
| plugins-tip7 | research/multi-platform-rust-core-impl-plugins | 9d6c2eca1 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 8bf6f966cb38 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-resulttext`: pass
- `plugins-tip7`: pass

## Sizes

| Metric | main | main-aa | plugins-resulttext | plugins-tip7 |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 8.06 MiB | 8.35 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.73 MiB | 1.77 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

63 of 63 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.2 [65.4] | 62.3 [68.4] | 0.986 [0.946, 1.023] | pass |
| open(ebook) / openedMs | 138.7 [144.4] | 138.9 [143.0] | 1.001 [0.987, 1.012] | pass |
| open(ebook) / firstPaintMs | 255.0 [264.0] | 262.5 [274.8] | 1.030 [0.992, 1.052] | pass |
| open(ebook) / settledMs | 396.9 [402.9] | 398.8 [404.2] | 1.005 [0.997, 1.013] | pass |
| open(ebook) / mainTaskMs | 142.9 [145.2] | 144.5 [147.3] | 1.011 [0.982, 1.032] | pass |
| open(ebook) / mainJsMs | 109.4 [111.1] | 111.5 [113.1] | 1.019 [0.987, 1.031] | pass |
| open(large) / readyMs | 63.7 [71.8] | 62.5 [68.5] | 0.981 [0.939, 1.034] | pass |
| open(large) / openedMs | 159.2 [167.8] | 159.7 [163.8] | 1.003 [0.972, 1.030] | pass |
| open(large) / firstPaintMs | 266.2 [280.1] | 270.5 [278.8] | 1.016 [0.970, 1.044] | pass |
| open(large) / settledMs | 268.0 [282.6] | 270.5 [275.6] | 1.009 [0.979, 1.035] | pass |
| open(large) / mainTaskMs | 315.7 [318.4] | 314.5 [316.2] | 0.996 [0.993, 1.005] | pass |
| open(large) / mainJsMs | 284.3 [287.2] | 284.3 [285.5] | 1.000 [0.994, 1.005] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2831.5 [2965.4] | 2849.3 [2972.0] | 1.006 [0.978, 1.043] | pass |
| scroll / frameP50Ms | 11.0 [11.7] | 11.1 [11.4] | 1.014 [0.963, 1.054] | pass |
| scroll / frameP95Ms | 23.4 [27.2] | 23.7 [27.0] | 1.015 [0.826, 1.220] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 522.8 [543.6] | 529.0 [546.1] | 1.012 [0.986, 1.046] | pass |
| scroll / mainJsMs | 391.5 [409.4] | 396.3 [410.7] | 1.012 [0.987, 1.048] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2843.6 [2941.8] | 2842.7 [2944.3] | 1.000 [0.969, 1.043] | pass |
| coldScroll / frameP50Ms | 11.1 [11.6] | 11.1 [11.5] | 1.000 [0.952, 1.019] | pass |
| coldScroll / frameP95Ms | 25.3 [27.2] | 26.2 [27.1] | 1.036 [0.956, 1.185] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 560.0 [577.7] | 562.4 [575.5] | 1.004 [0.971, 1.029] | pass |
| coldScroll / mainJsMs | 392.6 [402.9] | 392.0 [404.4] | 0.999 [0.979, 1.031] | pass |
| jump / settleMs | 125.1 [133.1] | 125.8 [143.6] | +0.7 [-5.3, +9.3] | pass |
| jump / mainTaskMs | 70.3 [78.2] | 74.4 [77.2] | 1.058 [0.977, 1.087] | pass |
| jump / mainJsMs | 54.1 [57.0] | 56.6 [60.4] | 1.045 [0.988, 1.088] | pass |
| zoom / settleMs | 499.6 [519.4] | 499.3 [524.3] | -0.3 [-17.2, +12.5] | pass |
| zoom / zoom2SettleMs | 163.6 [183.8] | 165.9 [179.5] | +2.3 [-12.5, +6.9] | pass |
| zoom / zoom4SettleMs | 335.3 [342.7] | 333.4 [346.8] | -1.9 [-14.2, +7.6] | pass |
| zoom / backTo1SettleMs | 94.4 [115.3] | 92.0 [96.6] | -2.4 [-12.8, +0.9] | pass |
| zoom / mainTaskMs | 227.6 [235.3] | 226.6 [233.1] | 0.996 [0.957, 1.032] | pass |
| zoom / mainJsMs | 164.1 [170.6] | 164.8 [170.6] | 1.004 [0.964, 1.035] | pass |
| zoomLarge / settleMs | 313.2 [343.5] | 324.7 [336.5] | +11.5 [-23.2, +32.5] | pass |
| zoomLarge / zoom2SettleMs | 141.2 [166.8] | 149.7 [167.2] | +8.6 [-8.5, +25.3] | pass |
| zoomLarge / zoom4SettleMs | 161.6 [198.0] | 166.5 [184.6] | +4.9 [-28.8, +20.6] | pass |
| zoomLarge / backTo1SettleMs | 75.4 [88.0] | 77.4 [87.6] | +2.0 [-7.2, +4.7] | pass |
| zoomLarge / mainTaskMs | 175.0 [187.7] | 174.2 [178.6] | 0.995 [0.953, 1.021] | pass |
| zoomLarge / mainJsMs | 130.0 [138.7] | 129.5 [133.1] | 0.996 [0.955, 1.025] | pass |
| select / settleMs | 230.0 [259.5] | 224.2 [289.3] | -5.7 [-32.6, +25.5] | pass |
| select / mainTaskMs | 62.7 [65.8] | 60.3 [69.5] | 0.962 [0.927, 1.100] | pass |
| select / mainJsMs | 40.9 [43.0] | 39.2 [45.7] | 0.958 [0.914, 1.099] | pass |
| selectLarge / settleMs | 207.3 [238.5] | 228.4 [280.2] | +21.0 [-4.9, +32.8] | pass |
| selectLarge / mainTaskMs | 71.3 [74.9] | 72.6 [77.2] | 1.019 [0.950, 1.085] | pass |
| selectLarge / mainJsMs | 47.5 [50.4] | 48.3 [52.1] | 1.017 [0.951, 1.092] | pass |
| search / searchMs | 73.4 [81.0] | 75.7 [84.2] | 1.031 [0.953, 1.078] | pass |
| search / mainTaskMs | 5.5 [6.2] | 5.7 [6.1] | 1.033 [0.921, 1.103] | pass |
| search / mainJsMs | 3.8 [4.1] | 3.8 [4.1] | 1.003 [0.951, 1.038] | pass |
| searchLarge / searchMs | 1894.8 [1952.5] | 1883.2 [1931.6] | 0.994 [0.975, 1.017] | pass |
| searchLarge / mainTaskMs | 343.2 [348.2] | 342.8 [345.2] | 0.999 [0.986, 1.012] | pass |
| searchLarge / mainJsMs | 290.8 [293.0] | 288.5 [293.0] | 0.992 [0.985, 1.007] | pass |
| annotate / settleMs | 230.5 [271.6] | 209.8 [260.5] | -20.6 [-55.0, +29.7] | pass |
| annotate / mainTaskMs | 78.4 [85.1] | 77.6 [82.7] | 0.990 [0.913, 1.075] | pass |
| annotate / mainJsMs | 47.2 [51.1] | 47.6 [50.9] | 1.009 [0.917, 1.078] | pass |
| annotateLarge / settleMs | 218.6 [248.9] | 242.0 [267.3] | +23.3 [-0.0, +42.8] | pass |
| annotateLarge / mainTaskMs | 80.3 [86.8] | 82.6 [90.5] | 1.029 [0.959, 1.116] | pass |
| annotateLarge / mainJsMs | 48.4 [52.3] | 50.2 [55.0] | 1.037 [0.966, 1.118] | pass |

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

Not run: `plugins-tip5`, `plugins-coalesce`.

## `main` vs `plugins-coalesce`

Not run: `plugins-coalesce`.

## `plugins-coalesce` vs `plugins-tip6`

Not run: `plugins-coalesce`, `plugins-tip6`.

## `main` vs `plugins-tip6`

Not run: `plugins-tip6`.

## `plugins-tip6` vs `plugins-plainargs`

Not run: `plugins-tip6`, `plugins-plainargs`.

## `main` vs `plugins-plainargs`

Not run: `plugins-plainargs`.

## `plugins-plainargs` vs `plugins-resulttext`

Not run: `plugins-plainargs`.

## `main` vs `plugins-resulttext`

| Row | main median [p95] | plugins-resulttext median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.2 [65.4] | 90.9 [95.4] | 1.438 [1.392, 1.473] | slower |
| open(ebook) / openedMs | 138.7 [144.4] | 180.9 [185.0] | 1.305 [1.259, 1.321] | slower |
| open(ebook) / firstPaintMs | 255.0 [264.0] | 334.0 [344.6] | 1.310 [1.274, 1.336] | slower |
| open(ebook) / settledMs | 396.9 [402.9] | 397.7 [407.7] | 1.002 [0.984, 1.010] | no detectable difference |
| open(ebook) / mainTaskMs | 142.9 [145.2] | 236.4 [240.3] | 1.654 [1.630, 1.690] | slower |
| open(ebook) / mainJsMs | 109.4 [111.1] | 187.9 [192.1] | 1.718 [1.691, 1.745] | slower |
| open(large) / readyMs | 63.7 [71.8] | 91.7 [99.1] | 1.439 [1.400, 1.514] | slower |
| open(large) / openedMs | 159.2 [167.8] | 210.0 [216.4] | 1.319 [1.287, 1.342] | slower |
| open(large) / firstPaintMs | 266.2 [280.1] | 365.9 [384.8] | 1.375 [1.316, 1.417] | slower |
| open(large) / settledMs | 268.0 [282.6] | 362.9 [371.5] | 1.354 [1.312, 1.391] | slower |
| open(large) / mainTaskMs | 315.7 [318.4] | 293.9 [297.0] | 0.931 [0.926, 0.939] | faster |
| open(large) / mainJsMs | 284.3 [287.2] | 246.4 [249.3] | 0.867 [0.861, 0.872] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2831.5 [2965.4] | 2615.9 [2683.8] | 0.924 [0.886, 0.945] | faster |
| scroll / frameP50Ms | 11.0 [11.7] | 11.0 [11.7] | 1.005 [0.962, 1.064] | no detectable difference |
| scroll / frameP95Ms | 23.4 [27.2] | 25.1 [27.2] | 1.076 [0.915, 1.222] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 522.8 [543.6] | 689.6 [710.4] | 1.319 [1.289, 1.361] | slower |
| scroll / mainJsMs | 391.5 [409.4] | 546.4 [567.6] | 1.396 [1.365, 1.444] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2843.6 [2941.8] | 2461.6 [2681.1] | 0.866 [0.838, 0.932] | faster |
| coldScroll / frameP50Ms | 11.1 [11.6] | 11.1 [11.5] | 1.001 [0.947, 1.029] | no detectable difference |
| coldScroll / frameP95Ms | 25.3 [27.2] | 25.5 [27.1] | 1.007 [0.918, 1.163] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 560.0 [577.7] | 679.5 [705.3] | 1.214 [1.179, 1.252] | slower |
| coldScroll / mainJsMs | 392.6 [402.9] | 524.3 [549.4] | 1.336 [1.308, 1.388] | slower |
| jump / settleMs | 125.1 [133.1] | 128.5 [145.5] | +3.4 [-0.9, +13.5] | no detectable difference |
| jump / mainTaskMs | 70.3 [78.2] | 116.2 [121.6] | 1.653 [1.564, 1.688] | slower |
| jump / mainJsMs | 54.1 [57.0] | 100.0 [103.5] | 1.848 [1.773, 1.895] | slower |
| zoom / settleMs | 499.6 [519.4] | 546.7 [568.3] | +47.1 [+29.5, +58.9] | slower |
| zoom / zoom2SettleMs | 163.6 [183.8] | 190.0 [209.6] | +26.4 [+11.1, +36.0] | slower |
| zoom / zoom4SettleMs | 335.3 [342.7] | 356.0 [369.5] | +20.7 [+4.2, +27.8] | slower |
| zoom / backTo1SettleMs | 94.4 [115.3] | 101.2 [122.3] | +6.8 [-1.6, +21.8] | no detectable difference |
| zoom / mainTaskMs | 227.6 [235.3] | 300.2 [306.3] | 1.319 [1.266, 1.360] | slower |
| zoom / mainJsMs | 164.1 [170.6] | 241.3 [248.3] | 1.470 [1.421, 1.509] | slower |
| zoomLarge / settleMs | 313.2 [343.5] | 311.9 [365.1] | -1.3 [-20.4, +37.6] | no detectable difference |
| zoomLarge / zoom2SettleMs | 141.2 [166.8] | 138.9 [173.1] | -2.3 [-13.9, +23.6] | no detectable difference |
| zoomLarge / zoom4SettleMs | 161.6 [198.0] | 175.3 [192.9] | +13.7 [-20.3, +28.5] | no detectable difference |
| zoomLarge / backTo1SettleMs | 75.4 [88.0] | 82.1 [92.9] | +6.7 [+0.7, +9.9] | no detectable difference |
| zoomLarge / mainTaskMs | 175.0 [187.7] | 241.0 [250.0] | 1.377 [1.317, 1.414] | slower |
| zoomLarge / mainJsMs | 130.0 [138.7] | 198.2 [207.1] | 1.524 [1.454, 1.570] | slower |
| select / settleMs | 230.0 [259.5] | 264.0 [296.6] | +34.0 [-3.8, +60.8] | no detectable difference |
| select / mainTaskMs | 62.7 [65.8] | 84.7 [91.1] | 1.351 [1.233, 1.426] | slower |
| select / mainJsMs | 40.9 [43.0] | 64.0 [70.3] | 1.564 [1.424, 1.705] | slower |
| selectLarge / settleMs | 207.3 [238.5] | 277.5 [325.4] | +70.1 [+48.8, +100.6] | slower |
| selectLarge / mainTaskMs | 71.3 [74.9] | 109.5 [119.5] | 1.536 [1.449, 1.618] | slower |
| selectLarge / mainJsMs | 47.5 [50.4] | 79.9 [88.0] | 1.681 [1.573, 1.774] | slower |
| search / searchMs | 73.4 [81.0] | 72.6 [80.0] | 0.989 [0.904, 1.085] | no detectable difference |
| search / mainTaskMs | 5.5 [6.2] | 8.9 [17.5] | 1.610 [1.456, 2.573] | slower |
| search / mainJsMs | 3.8 [4.1] | 5.9 [6.9] | 1.547 [1.465, 1.802] | slower |
| searchLarge / searchMs | 1894.8 [1952.5] | 1828.4 [1836.7] | 0.965 [0.946, 0.975] | faster |
| searchLarge / mainTaskMs | 343.2 [348.2] | 749.7 [761.1] | 2.184 [2.156, 2.212] | slower |
| searchLarge / mainJsMs | 290.8 [293.0] | 699.5 [710.2] | 2.406 [2.383, 2.430] | slower |
| annotate / settleMs | 230.5 [271.6] | 223.2 [277.8] | -7.3 [-63.4, +28.5] | no detectable difference |
| annotate / mainTaskMs | 78.4 [85.1] | 83.5 [97.3] | 1.065 [0.954, 1.183] | no detectable difference |
| annotate / mainJsMs | 47.2 [51.1] | 56.1 [66.7] | 1.188 [1.042, 1.336] | slower |
| annotateLarge / settleMs | 218.6 [248.9] | 220.0 [280.2] | +1.4 [-19.7, +41.2] | no detectable difference |
| annotateLarge / mainTaskMs | 80.3 [86.8] | 104.8 [118.7] | 1.305 [1.181, 1.432] | slower |
| annotateLarge / mainJsMs | 48.4 [52.3] | 65.2 [76.8] | 1.346 [1.234, 1.539] | slower |

## `plugins-resulttext` vs `plugins-rustreads`

Not run: `plugins-rustreads`.

## `main` vs `plugins-rustreads`

Not run: `plugins-rustreads`.

## `plugins-resulttext` vs `plugins-tip7`

| Row | plugins-resulttext median [p95] | plugins-tip7 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 90.9 [95.4] | 91.8 [104.4] | 1.010 [0.988, 1.062] | no detectable difference |
| open(ebook) / openedMs | 180.9 [185.0] | 178.4 [191.0] | 0.986 [0.972, 1.034] | no detectable difference |
| open(ebook) / firstPaintMs | 334.0 [344.6] | 333.6 [367.5] | 0.999 [0.976, 1.039] | no detectable difference |
| open(ebook) / settledMs | 397.7 [407.7] | 395.9 [416.0] | 0.995 [0.986, 1.019] | no detectable difference |
| open(ebook) / mainTaskMs | 236.4 [240.3] | 239.7 [245.9] | 1.014 [0.988, 1.026] | no detectable difference |
| open(ebook) / mainJsMs | 187.9 [192.1] | 190.9 [196.8] | 1.016 [0.990, 1.029] | no detectable difference |
| open(large) / readyMs | 91.7 [99.1] | 92.5 [107.1] | 1.009 [0.963, 1.083] | no detectable difference |
| open(large) / openedMs | 210.0 [216.4] | 207.7 [223.7] | 0.989 [0.979, 1.030] | no detectable difference |
| open(large) / firstPaintMs | 365.9 [384.8] | 370.4 [388.1] | 1.012 [0.968, 1.048] | no detectable difference |
| open(large) / settledMs | 362.9 [371.5] | 358.4 [376.9] | 0.988 [0.970, 1.035] | no detectable difference |
| open(large) / mainTaskMs | 293.9 [297.0] | 296.7 [298.9] | 1.009 [1.002, 1.019] | no detectable difference |
| open(large) / mainJsMs | 246.4 [249.3] | 248.2 [250.4] | 1.007 [0.999, 1.015] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.6] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2615.9 [2683.8] | 2551.2 [2699.5] | 0.975 [0.953, 1.027] | no detectable difference |
| scroll / frameP50Ms | 11.0 [11.7] | 11.2 [11.5] | 1.015 [0.973, 1.031] | no detectable difference |
| scroll / frameP95Ms | 25.1 [27.2] | 26.1 [27.0] | 1.039 [0.933, 1.144] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 689.6 [710.4] | 689.6 [704.7] | 1.000 [0.980, 1.011] | no detectable difference |
| scroll / mainJsMs | 546.4 [567.6] | 550.8 [565.6] | 1.008 [0.982, 1.022] | no detectable difference |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [10.7] | +0.0 [+0.0, +0.4] | no detectable difference |
| coldScroll / wallMs | 2461.6 [2681.1] | 2529.0 [2835.9] | 1.027 [0.954, 1.101] | no detectable difference |
| coldScroll / frameP50Ms | 11.1 [11.5] | 11.2 [11.6] | 1.009 [0.982, 1.040] | no detectable difference |
| coldScroll / frameP95Ms | 25.5 [27.1] | 26.8 [27.2] | 1.051 [1.007, 1.116] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 679.5 [705.3] | 689.2 [725.6] | 1.014 [0.983, 1.048] | no detectable difference |
| coldScroll / mainJsMs | 524.3 [549.4] | 535.4 [574.1] | 1.021 [0.987, 1.063] | no detectable difference |
| jump / settleMs | 128.5 [145.5] | 131.4 [142.9] | +2.9 [-9.8, +9.8] | no detectable difference |
| jump / mainTaskMs | 116.2 [121.6] | 115.9 [125.8] | 0.997 [0.973, 1.074] | no detectable difference |
| jump / mainJsMs | 100.0 [103.5] | 100.0 [106.3] | 1.000 [0.967, 1.035] | no detectable difference |
| zoom / settleMs | 546.7 [568.3] | 543.1 [569.0] | -3.5 [-15.8, +16.3] | no detectable difference |
| zoom / zoom2SettleMs | 190.0 [209.6] | 185.4 [207.5] | -4.6 [-14.3, +10.8] | no detectable difference |
| zoom / zoom4SettleMs | 356.0 [369.5] | 355.2 [369.5] | -0.8 [-8.7, +16.8] | no detectable difference |
| zoom / backTo1SettleMs | 101.2 [122.3] | 98.2 [118.6] | -3.1 [-19.0, +13.8] | no detectable difference |
| zoom / mainTaskMs | 300.2 [306.3] | 296.8 [307.7] | 0.988 [0.972, 1.023] | no detectable difference |
| zoom / mainJsMs | 241.3 [248.3] | 240.4 [252.3] | 0.996 [0.972, 1.034] | no detectable difference |
| zoomLarge / settleMs | 311.9 [365.1] | 319.6 [364.4] | +7.8 [-29.5, +45.1] | no detectable difference |
| zoomLarge / zoom2SettleMs | 138.9 [173.1] | 156.7 [176.3] | +17.8 [-14.3, +26.1] | no detectable difference |
| zoomLarge / zoom4SettleMs | 175.3 [192.9] | 175.9 [196.7] | +0.6 [-13.1, +18.3] | no detectable difference |
| zoomLarge / backTo1SettleMs | 82.1 [92.9] | 78.8 [82.3] | -3.3 [-6.4, +2.5] | no detectable difference |
| zoomLarge / mainTaskMs | 241.0 [250.0] | 247.5 [254.9] | 1.027 [0.988, 1.050] | no detectable difference |
| zoomLarge / mainJsMs | 198.2 [207.1] | 202.3 [211.3] | 1.021 [0.984, 1.056] | no detectable difference |
| select / settleMs | 264.0 [296.6] | 260.8 [305.6] | -3.2 [-22.3, +32.8] | no detectable difference |
| select / mainTaskMs | 84.7 [91.1] | 81.1 [90.0] | 0.958 [0.907, 1.116] | no detectable difference |
| select / mainJsMs | 64.0 [70.3] | 61.4 [68.6] | 0.960 [0.895, 1.109] | no detectable difference |
| selectLarge / settleMs | 277.5 [325.4] | 285.4 [341.8] | +8.0 [-27.2, +30.5] | no detectable difference |
| selectLarge / mainTaskMs | 109.5 [119.5] | 100.6 [117.7] | 0.919 [0.862, 0.998] | no detectable difference |
| selectLarge / mainJsMs | 79.9 [88.0] | 70.6 [84.9] | 0.884 [0.819, 0.977] | faster |
| search / searchMs | 72.6 [80.0] | 74.3 [81.8] | 1.024 [0.933, 1.133] | no detectable difference |
| search / mainTaskMs | 8.9 [17.5] | 8.9 [18.5] | 0.997 [0.628, 1.544] | no detectable difference |
| search / mainJsMs | 5.9 [6.9] | 5.9 [9.0] | 1.004 [0.865, 1.185] | no detectable difference |
| searchLarge / searchMs | 1828.4 [1836.7] | 1829.3 [1838.1] | 1.000 [0.996, 1.005] | no detectable difference |
| searchLarge / mainTaskMs | 749.7 [761.1] | 747.6 [754.7] | 0.997 [0.993, 1.008] | no detectable difference |
| searchLarge / mainJsMs | 699.5 [710.2] | 699.1 [704.3] | 1.000 [0.995, 1.007] | no detectable difference |
| annotate / settleMs | 223.2 [277.8] | 212.3 [251.6] | -10.9 [-42.0, +36.7] | no detectable difference |
| annotate / mainTaskMs | 83.5 [97.3] | 79.8 [88.2] | 0.956 [0.876, 1.054] | no detectable difference |
| annotate / mainJsMs | 56.1 [66.7] | 51.8 [59.7] | 0.924 [0.838, 1.060] | no detectable difference |
| annotateLarge / settleMs | 220.0 [280.2] | 263.3 [290.8] | +43.3 [+5.0, +65.3] | slower |
| annotateLarge / mainTaskMs | 104.8 [118.7] | 108.3 [117.2] | 1.033 [0.912, 1.145] | no detectable difference |
| annotateLarge / mainJsMs | 65.2 [76.8] | 69.3 [75.2] | 1.064 [0.876, 1.145] | no detectable difference |

## `main` vs `plugins-tip7`

| Row | main median [p95] | plugins-tip7 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.2 [65.4] | 91.8 [104.4] | 1.453 [1.413, 1.527] | slower |
| open(ebook) / openedMs | 138.7 [144.4] | 178.4 [191.0] | 1.286 [1.272, 1.329] | slower |
| open(ebook) / firstPaintMs | 255.0 [264.0] | 333.6 [367.5] | 1.308 [1.274, 1.359] | slower |
| open(ebook) / settledMs | 396.9 [402.9] | 395.9 [416.0] | 0.998 [0.988, 1.019] | no detectable difference |
| open(ebook) / mainTaskMs | 142.9 [145.2] | 239.7 [245.9] | 1.677 [1.634, 1.706] | slower |
| open(ebook) / mainJsMs | 109.4 [111.1] | 190.9 [196.8] | 1.745 [1.701, 1.764] | slower |
| open(large) / readyMs | 63.7 [71.8] | 92.5 [107.1] | 1.452 [1.403, 1.555] | slower |
| open(large) / openedMs | 159.2 [167.8] | 207.7 [223.7] | 1.305 [1.289, 1.356] | slower |
| open(large) / firstPaintMs | 266.2 [280.1] | 370.4 [388.1] | 1.392 [1.334, 1.414] | slower |
| open(large) / settledMs | 268.0 [282.6] | 358.4 [376.9] | 1.337 [1.304, 1.407] | slower |
| open(large) / mainTaskMs | 315.7 [318.4] | 296.7 [298.9] | 0.940 [0.933, 0.951] | faster |
| open(large) / mainJsMs | 284.3 [287.2] | 248.2 [250.4] | 0.873 [0.865, 0.880] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.6] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2831.5 [2965.4] | 2551.2 [2699.5] | 0.901 [0.872, 0.941] | faster |
| scroll / frameP50Ms | 11.0 [11.7] | 11.2 [11.5] | 1.020 [0.972, 1.061] | no detectable difference |
| scroll / frameP95Ms | 23.4 [27.2] | 26.1 [27.0] | 1.118 [0.961, 1.242] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 522.8 [543.6] | 689.6 [704.7] | 1.319 [1.284, 1.358] | slower |
| scroll / mainJsMs | 391.5 [409.4] | 550.8 [565.6] | 1.407 [1.366, 1.450] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [10.7] | +0.0 [+0.0, +0.4] | no detectable difference |
| coldScroll / wallMs | 2843.6 [2941.8] | 2529.0 [2835.9] | 0.889 [0.864, 0.955] | faster |
| coldScroll / frameP50Ms | 11.1 [11.6] | 11.2 [11.6] | 1.010 [0.961, 1.036] | no detectable difference |
| coldScroll / frameP95Ms | 25.3 [27.2] | 26.8 [27.2] | 1.058 [0.988, 1.210] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 560.0 [577.7] | 689.2 [725.6] | 1.231 [1.194, 1.273] | slower |
| coldScroll / mainJsMs | 392.6 [402.9] | 535.4 [574.1] | 1.364 [1.335, 1.424] | slower |
| jump / settleMs | 125.1 [133.1] | 131.4 [142.9] | +6.3 [-2.7, +14.1] | no detectable difference |
| jump / mainTaskMs | 70.3 [78.2] | 115.9 [125.8] | 1.649 [1.565, 1.772] | slower |
| jump / mainJsMs | 54.1 [57.0] | 100.0 [106.3] | 1.849 [1.764, 1.913] | slower |
| zoom / settleMs | 499.6 [519.4] | 543.1 [569.0] | +43.5 [+31.9, +61.2] | slower |
| zoom / zoom2SettleMs | 163.6 [183.8] | 185.4 [207.5] | +21.8 [+7.3, +36.6] | slower |
| zoom / zoom4SettleMs | 335.3 [342.7] | 355.2 [369.5] | +19.9 [+12.3, +29.2] | slower |
| zoom / backTo1SettleMs | 94.4 [115.3] | 98.2 [118.6] | +3.8 [-5.7, +20.8] | no detectable difference |
| zoom / mainTaskMs | 227.6 [235.3] | 296.8 [307.7] | 1.304 [1.268, 1.345] | slower |
| zoom / mainJsMs | 164.1 [170.6] | 240.4 [252.3] | 1.465 [1.425, 1.512] | slower |
| zoomLarge / settleMs | 313.2 [343.5] | 319.6 [364.4] | +6.4 [-14.9, +46.3] | no detectable difference |
| zoomLarge / zoom2SettleMs | 141.2 [166.8] | 156.7 [176.3] | +15.5 [-11.8, +30.6] | no detectable difference |
| zoomLarge / zoom4SettleMs | 161.6 [198.0] | 175.9 [196.7] | +14.3 [-17.5, +27.9] | no detectable difference |
| zoomLarge / backTo1SettleMs | 75.4 [88.0] | 78.8 [82.3] | +3.4 [-0.3, +5.1] | no detectable difference |
| zoomLarge / mainTaskMs | 175.0 [187.7] | 247.5 [254.9] | 1.414 [1.339, 1.455] | slower |
| zoomLarge / mainJsMs | 130.0 [138.7] | 202.3 [211.3] | 1.556 [1.475, 1.609] | slower |
| select / settleMs | 230.0 [259.5] | 260.8 [305.6] | +30.8 [+7.0, +60.3] | no detectable difference |
| select / mainTaskMs | 62.7 [65.8] | 81.1 [90.0] | 1.294 [1.236, 1.434] | slower |
| select / mainJsMs | 40.9 [43.0] | 61.4 [68.6] | 1.501 [1.434, 1.697] | slower |
| selectLarge / settleMs | 207.3 [238.5] | 285.4 [341.8] | +78.1 [+53.2, +97.5] | slower |
| selectLarge / mainTaskMs | 71.3 [74.9] | 100.6 [117.7] | 1.411 [1.336, 1.523] | slower |
| selectLarge / mainJsMs | 47.5 [50.4] | 70.6 [84.9] | 1.485 [1.383, 1.626] | slower |
| search / searchMs | 73.4 [81.0] | 74.3 [81.8] | 1.012 [0.936, 1.100] | no detectable difference |
| search / mainTaskMs | 5.5 [6.2] | 8.9 [18.5] | 1.605 [1.442, 2.458] | slower |
| search / mainJsMs | 3.8 [4.1] | 5.9 [9.0] | 1.552 [1.489, 1.843] | slower |
| searchLarge / searchMs | 1894.8 [1952.5] | 1829.3 [1838.1] | 0.965 [0.946, 0.975] | faster |
| searchLarge / mainTaskMs | 343.2 [348.2] | 747.6 [754.7] | 2.178 [2.157, 2.207] | slower |
| searchLarge / mainJsMs | 290.8 [293.0] | 699.1 [704.3] | 2.404 [2.387, 2.431] | slower |
| annotate / settleMs | 230.5 [271.6] | 212.3 [251.6] | -18.2 [-56.3, +16.7] | no detectable difference |
| annotate / mainTaskMs | 78.4 [85.1] | 79.8 [88.2] | 1.018 [0.941, 1.111] | no detectable difference |
| annotate / mainJsMs | 47.2 [51.1] | 51.8 [59.7] | 1.098 [1.024, 1.219] | slower |
| annotateLarge / settleMs | 218.6 [248.9] | 263.3 [290.8] | +44.7 [+26.9, +64.9] | slower |
| annotateLarge / mainTaskMs | 80.3 [86.8] | 108.3 [117.2] | 1.349 [1.203, 1.465] | slower |
| annotateLarge / mainJsMs | 48.4 [52.3] | 69.3 [75.2] | 1.433 [1.237, 1.545] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-resulttext median [p95] | plugins-tip7 median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 35.34 MiB [35.35 MiB] | 35.35 MiB [35.36 MiB] | 37.10 MiB [37.63 MiB] | 37.14 MiB [38.14 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.16 MiB] | 48.15 MiB [48.17 MiB] | 52.52 MiB [52.55 MiB] | 53.55 MiB [53.57 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.28 MiB] | 23.01 MiB [23.03 MiB] | 23.02 MiB [23.03 MiB] |
| soak / heapCycle5Bytes | 38.42 MiB [38.43 MiB] | 37.92 MiB [38.43 MiB] | 40.67 MiB [41.18 MiB] | 40.01 MiB [41.02 MiB] |
| soak / heapCycle30Bytes | 39.27 MiB [39.28 MiB] | 38.76 MiB [39.27 MiB] | 41.21 MiB [41.71 MiB] | 40.55 MiB [41.56 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-resulttext`:

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
| wasmBytes | 3.13 MiB | 3.13 MiB |
| wireRefs | 658 | 658 |

`plugins-tip7`:

| Table | Cycle 5 | Cycle 30 |
| --- | --- | --- |
| aborts | 0 | 0 |
| allHosts | 297 | 297 |
| described | 17 | 17 |
| documents | 1 | 1 |
| fns | 258 | 258 |
| heldFns | 258 | 258 |
| hosts | 297 | 297 |
| promises | 0 | 0 |
| proxies | 17 | 17 |
| refs | 626 | 626 |
| tokens | 28 | 28 |
| wasmBytes | 2.94 MiB | 2.94 MiB |
| wireRefs | 626 | 626 |

