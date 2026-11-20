# Viewer perf comparison: 2026-10-09T21-14-02-964Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 9b1e0f9bc596293279ce1a064b1b00e4422117dd |
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
| plugins-plainargs | research/rust-kernel-plain-args | 45c920b26 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 5bf5fb28d0f2 |
| plugins-resulttext | research/rust-kernel-result-text | 34644a2b4 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 63a60ebf8d9a |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-plainargs`: pass
- `plugins-resulttext`: pass

## Sizes

| Metric | main | main-aa | plugins-plainargs | plugins-resulttext |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 8.05 MiB | 8.06 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.73 MiB | 1.73 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

62 of 63 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.2 [67.9] | 61.8 [71.7] | 0.978 [0.936, 1.028] | pass |
| open(ebook) / openedMs | 137.0 [142.3] | 136.2 [147.0] | 0.994 [0.977, 1.018] | pass |
| open(ebook) / firstPaintMs | 252.8 [284.2] | 254.8 [271.1] | 1.008 [0.930, 1.045] | pass |
| open(ebook) / settledMs | 397.0 [405.4] | 395.0 [403.6] | 0.995 [0.981, 1.010] | pass |
| open(ebook) / mainTaskMs | 145.5 [147.9] | 145.1 [146.5] | 0.998 [0.979, 1.020] | pass |
| open(ebook) / mainJsMs | 111.9 [114.4] | 111.4 [112.7] | 0.995 [0.982, 1.017] | pass |
| open(large) / readyMs | 62.6 [64.8] | 61.9 [64.9] | 0.990 [0.944, 1.030] | pass |
| open(large) / openedMs | 158.1 [160.9] | 158.4 [161.7] | 1.001 [0.989, 1.020] | pass |
| open(large) / firstPaintMs | 266.5 [275.5] | 265.3 [285.8] | 0.996 [0.958, 1.036] | pass |
| open(large) / settledMs | 267.2 [276.5] | 271.1 [283.7] | 1.015 [0.982, 1.044] | pass |
| open(large) / mainTaskMs | 315.0 [318.0] | 314.8 [318.8] | 0.999 [0.992, 1.006] | pass |
| open(large) / mainJsMs | 284.8 [288.3] | 283.9 [286.8] | 0.997 [0.993, 1.005] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2863.2 [2975.7] | 2896.2 [2956.4] | 1.012 [0.983, 1.032] | pass |
| scroll / frameP50Ms | 11.2 [11.7] | 11.0 [11.4] | 0.984 [0.947, 1.013] | pass |
| scroll / frameP95Ms | 24.7 [27.1] | 21.9 [26.5] | 0.887 [0.813, 1.087] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 534.3 [546.9] | 532.3 [563.2] | 0.996 [0.980, 1.020] | pass |
| scroll / mainJsMs | 400.8 [410.4] | 399.1 [424.4] | 0.996 [0.979, 1.028] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2846.1 [2908.4] | 2870.6 [2929.7] | 1.009 [0.980, 1.050] | pass |
| coldScroll / frameP50Ms | 11.1 [11.7] | 11.1 [11.7] | 1.006 [0.965, 1.051] | pass |
| coldScroll / frameP95Ms | 25.5 [26.9] | 25.5 [27.2] | 1.002 [0.847, 1.094] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 560.6 [581.9] | 571.5 [595.4] | 1.019 [0.985, 1.059] | pass |
| coldScroll / mainJsMs | 389.0 [410.8] | 400.6 [419.1] | 1.030 [0.972, 1.069] | pass |
| jump / settleMs | 123.3 [138.4] | 130.1 [138.5] | +6.8 [-2.4, +10.1] | pass |
| jump / mainTaskMs | 73.6 [78.5] | 72.9 [79.8] | 0.991 [0.957, 1.029] | pass |
| jump / mainJsMs | 55.2 [59.0] | 53.5 [58.5] | 0.969 [0.925, 1.011] | pass |
| zoom / settleMs | 495.0 [517.8] | 496.1 [516.2] | +1.0 [-19.9, +19.7] | pass |
| zoom / zoom2SettleMs | 166.9 [178.9] | 171.1 [180.4] | +4.2 [-2.8, +13.1] | pass |
| zoom / zoom4SettleMs | 329.5 [343.8] | 324.7 [341.1] | -4.9 [-21.0, +10.1] | pass |
| zoom / backTo1SettleMs | 89.8 [96.3] | 91.2 [103.4] | +1.4 [-7.5, +5.0] | pass |
| zoom / mainTaskMs | 228.6 [234.2] | 223.3 [234.5] | 0.977 [0.947, 1.023] | pass |
| zoom / mainJsMs | 165.0 [168.5] | 161.8 [166.7] | 0.981 [0.933, 1.025] | pass |
| zoomLarge / settleMs | 311.9 [328.5] | 305.8 [337.1] | -6.1 [-26.8, +20.4] | pass |
| zoomLarge / zoom2SettleMs | 135.7 [166.7] | 145.3 [168.7] | +9.6 [-13.0, +19.6] | pass |
| zoomLarge / zoom4SettleMs | 169.8 [187.2] | 156.3 [186.2] | -13.4 [-24.9, +15.0] | pass |
| zoomLarge / backTo1SettleMs | 72.8 [81.3] | 75.6 [83.3] | +2.8 [-4.2, +8.7] | pass |
| zoomLarge / mainTaskMs | 173.6 [179.5] | 177.7 [183.0] | 1.024 [0.970, 1.038] | pass |
| zoomLarge / mainJsMs | 129.5 [133.0] | 132.1 [136.7] | 1.020 [0.976, 1.047] | pass |
| select / settleMs | 212.4 [240.4] | 214.2 [245.4] | +1.8 [-27.9, +18.5] | pass |
| select / mainTaskMs | 61.6 [63.7] | 59.0 [62.5] | 0.957 [0.939, 1.015] | pass |
| select / mainJsMs | 39.9 [41.2] | 37.9 [40.6] | 0.949 [0.919, 1.028] | pass |
| selectLarge / settleMs | 216.1 [255.2] | 218.5 [238.6] | +2.4 [-19.9, +25.1] | pass |
| selectLarge / mainTaskMs | 71.4 [76.5] | 71.1 [73.7] | 0.996 [0.915, 1.074] | pass |
| selectLarge / mainJsMs | 47.9 [51.5] | 47.8 [50.2] | 0.998 [0.910, 1.081] | pass |
| search / searchMs | 70.9 [73.3] | 73.0 [78.4] | 1.029 [0.960, 1.187] | pass |
| search / mainTaskMs | 5.5 [6.0] | 5.5 [6.0] | 1.006 [0.914, 1.112] | pass |
| search / mainJsMs | 3.6 [3.8] | 3.7 [3.9] | 1.037 [1.006, 1.069] | A/A failed |
| searchLarge / searchMs | 1886.1 [1933.6] | 1906.3 [1932.7] | 1.011 [0.998, 1.026] | pass |
| searchLarge / mainTaskMs | 343.3 [345.8] | 344.1 [347.4] | 1.002 [0.998, 1.014] | pass |
| searchLarge / mainJsMs | 289.7 [292.4] | 292.1 [294.6] | 1.008 [0.998, 1.014] | pass |
| annotate / settleMs | 230.6 [281.7] | 254.0 [266.7] | +23.4 [-21.4, +54.2] | pass |
| annotate / mainTaskMs | 80.5 [88.0] | 79.8 [85.0] | 0.991 [0.919, 1.074] | pass |
| annotate / mainJsMs | 49.0 [52.1] | 48.8 [51.5] | 0.997 [0.925, 1.073] | pass |
| annotateLarge / settleMs | 233.9 [273.6] | 225.4 [251.6] | -8.4 [-32.3, +42.1] | pass |
| annotateLarge / mainTaskMs | 83.8 [92.1] | 82.4 [86.2] | 0.984 [0.925, 1.063] | pass |
| annotateLarge / mainJsMs | 50.2 [55.5] | 49.3 [52.3] | 0.983 [0.915, 1.061] | pass |

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

Not run: `plugins-tip6`.

## `main` vs `plugins-plainargs`

| Row | main median [p95] | plugins-plainargs median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.2 [67.9] | 90.7 [96.3] | 1.436 [1.389, 1.487] | slower |
| open(ebook) / openedMs | 137.0 [142.3] | 177.9 [184.3] | 1.299 [1.279, 1.334] | slower |
| open(ebook) / firstPaintMs | 252.8 [284.2] | 336.1 [355.1] | 1.330 [1.227, 1.369] | slower |
| open(ebook) / settledMs | 397.0 [405.4] | 397.6 [411.0] | 1.001 [0.976, 1.024] | no detectable difference |
| open(ebook) / mainTaskMs | 145.5 [147.9] | 237.3 [242.9] | 1.631 [1.605, 1.680] | slower |
| open(ebook) / mainJsMs | 111.9 [114.4] | 190.8 [195.6] | 1.704 [1.673, 1.748] | slower |
| open(large) / readyMs | 62.6 [64.8] | 91.5 [105.2] | 1.462 [1.397, 1.576] | slower |
| open(large) / openedMs | 158.1 [160.9] | 209.9 [221.3] | 1.327 [1.290, 1.356] | slower |
| open(large) / firstPaintMs | 266.5 [275.5] | 370.2 [396.6] | 1.389 [1.346, 1.430] | slower |
| open(large) / settledMs | 267.2 [276.5] | 365.9 [382.4] | 1.369 [1.320, 1.410] | slower |
| open(large) / mainTaskMs | 315.0 [318.0] | 295.9 [301.9] | 0.939 [0.930, 0.949] | faster |
| open(large) / mainJsMs | 284.8 [288.3] | 249.0 [253.5] | 0.874 [0.868, 0.883] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [6.3] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2863.2 [2975.7] | 2548.6 [2712.2] | 0.890 [0.834, 0.918] | faster |
| scroll / frameP50Ms | 11.2 [11.7] | 11.1 [11.5] | 0.994 [0.961, 1.021] | no detectable difference |
| scroll / frameP95Ms | 24.7 [27.1] | 25.7 [27.2] | 1.039 [0.894, 1.164] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 534.3 [546.9] | 720.5 [763.6] | 1.348 [1.324, 1.395] | slower |
| scroll / mainJsMs | 400.8 [410.4] | 585.3 [619.5] | 1.460 [1.417, 1.516] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [1.5] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2846.1 [2908.4] | 2560.9 [2638.3] | 0.900 [0.839, 0.938] | faster |
| coldScroll / frameP50Ms | 11.1 [11.7] | 11.1 [11.5] | 1.001 [0.968, 1.016] | no detectable difference |
| coldScroll / frameP95Ms | 25.5 [26.9] | 26.5 [27.2] | 1.042 [0.981, 1.119] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 560.6 [581.9] | 739.3 [759.3] | 1.319 [1.266, 1.355] | slower |
| coldScroll / mainJsMs | 389.0 [410.8] | 584.7 [605.1] | 1.503 [1.417, 1.547] | slower |
| jump / settleMs | 123.3 [138.4] | 128.6 [150.8] | +5.3 [-3.8, +12.5] | no detectable difference |
| jump / mainTaskMs | 73.6 [78.5] | 120.6 [129.1] | 1.638 [1.597, 1.684] | slower |
| jump / mainJsMs | 55.2 [59.0] | 103.6 [108.9] | 1.878 [1.804, 1.952] | slower |
| zoom / settleMs | 495.0 [517.8] | 538.2 [551.7] | +43.2 [+31.1, +55.5] | slower |
| zoom / zoom2SettleMs | 166.9 [178.9] | 188.6 [199.0] | +21.7 [+16.7, +28.6] | slower |
| zoom / zoom4SettleMs | 329.5 [343.8] | 347.0 [361.9] | +17.5 [+13.0, +29.5] | no detectable difference |
| zoom / backTo1SettleMs | 89.8 [96.3] | 98.5 [109.9] | +8.7 [+2.1, +15.4] | slower |
| zoom / mainTaskMs | 228.6 [234.2] | 315.1 [330.5] | 1.378 [1.333, 1.422] | slower |
| zoom / mainJsMs | 165.0 [168.5] | 256.6 [267.3] | 1.555 [1.515, 1.625] | slower |
| zoomLarge / settleMs | 311.9 [328.5] | 316.0 [370.9] | +4.1 [-13.6, +35.4] | no detectable difference |
| zoomLarge / zoom2SettleMs | 135.7 [166.7] | 146.6 [174.4] | +10.9 [-12.0, +22.6] | no detectable difference |
| zoomLarge / zoom4SettleMs | 169.8 [187.2] | 179.8 [200.7] | +10.1 [-5.7, +25.1] | no detectable difference |
| zoomLarge / backTo1SettleMs | 72.8 [81.3] | 82.4 [97.3] | +9.6 [+4.0, +18.0] | slower |
| zoomLarge / mainTaskMs | 173.6 [179.5] | 252.9 [261.6] | 1.457 [1.406, 1.487] | slower |
| zoomLarge / mainJsMs | 129.5 [133.0] | 210.0 [217.1] | 1.622 [1.573, 1.677] | slower |
| select / settleMs | 212.4 [240.4] | 260.0 [287.6] | +47.5 [+23.8, +72.3] | slower |
| select / mainTaskMs | 61.6 [63.7] | 91.4 [95.0] | 1.483 [1.435, 1.548] | slower |
| select / mainJsMs | 39.9 [41.2] | 71.5 [75.1] | 1.792 [1.734, 1.909] | slower |
| selectLarge / settleMs | 216.1 [255.2] | 289.1 [310.4] | +73.0 [+44.4, +93.5] | slower |
| selectLarge / mainTaskMs | 71.4 [76.5] | 115.6 [120.4] | 1.619 [1.515, 1.730] | slower |
| selectLarge / mainJsMs | 47.9 [51.5] | 84.6 [89.4] | 1.768 [1.620, 1.902] | slower |
| search / searchMs | 70.9 [73.3] | 79.5 [82.8] | 1.120 [0.981, 1.282] | no detectable difference |
| search / mainTaskMs | 5.5 [6.0] | 11.0 [15.5] | 1.990 [1.673, 2.546] | slower |
| search / mainJsMs | 3.6 [3.8] | 7.2 [8.2] | 1.980 [1.789, 2.220] | unreliable (A/A failed) |
| searchLarge / searchMs | 1886.1 [1933.6] | 1840.4 [1855.2] | 0.976 [0.965, 0.983] | no detectable difference |
| searchLarge / mainTaskMs | 343.3 [345.8] | 862.5 [883.7] | 2.512 [2.499, 2.546] | slower |
| searchLarge / mainJsMs | 289.7 [292.4] | 816.0 [834.7] | 2.816 [2.796, 2.829] | slower |
| annotate / settleMs | 230.6 [281.7] | 218.1 [291.4] | -12.5 [-58.0, +39.9] | no detectable difference |
| annotate / mainTaskMs | 80.5 [88.0] | 88.6 [104.0] | 1.100 [0.981, 1.237] | no detectable difference |
| annotate / mainJsMs | 49.0 [52.1] | 61.3 [74.3] | 1.252 [1.123, 1.464] | slower |
| annotateLarge / settleMs | 233.9 [273.6] | 238.8 [268.5] | +5.0 [-28.3, +55.0] | no detectable difference |
| annotateLarge / mainTaskMs | 83.8 [92.1] | 116.9 [123.3] | 1.395 [1.304, 1.500] | slower |
| annotateLarge / mainJsMs | 50.2 [55.5] | 69.4 [75.1] | 1.382 [1.304, 1.505] | slower |

## `plugins-plainargs` vs `plugins-resulttext`

| Row | plugins-plainargs median [p95] | plugins-resulttext median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 90.7 [96.3] | 90.7 [96.7] | 1.000 [0.985, 1.021] | no detectable difference |
| open(ebook) / openedMs | 177.9 [184.3] | 177.1 [182.7] | 0.995 [0.975, 1.013] | no detectable difference |
| open(ebook) / firstPaintMs | 336.1 [355.1] | 335.5 [354.4] | 0.998 [0.963, 1.038] | no detectable difference |
| open(ebook) / settledMs | 397.6 [411.0] | 395.1 [403.0] | 0.994 [0.971, 1.018] | no detectable difference |
| open(ebook) / mainTaskMs | 237.3 [242.9] | 234.0 [239.5] | 0.986 [0.965, 1.001] | no detectable difference |
| open(ebook) / mainJsMs | 190.8 [195.6] | 186.6 [190.2] | 0.978 [0.958, 0.998] | no detectable difference |
| open(large) / readyMs | 91.5 [105.2] | 92.1 [106.8] | 1.007 [0.945, 1.087] | no detectable difference |
| open(large) / openedMs | 209.9 [221.3] | 206.7 [221.9] | 0.985 [0.963, 1.023] | no detectable difference |
| open(large) / firstPaintMs | 370.2 [396.6] | 368.2 [382.1] | 0.995 [0.955, 1.029] | no detectable difference |
| open(large) / settledMs | 365.9 [382.4] | 363.8 [379.6] | 0.994 [0.974, 1.022] | no detectable difference |
| open(large) / mainTaskMs | 295.9 [301.9] | 294.6 [297.9] | 0.996 [0.979, 1.007] | no detectable difference |
| open(large) / mainJsMs | 249.0 [253.5] | 248.2 [250.4] | 0.997 [0.978, 1.005] | no detectable difference |
| scroll / settleMs | 0.0 [6.3] | 0.0 [15.5] | +0.0 [+0.0, +3.3] | no detectable difference |
| scroll / wallMs | 2548.6 [2712.2] | 2567.5 [2689.1] | 1.007 [0.964, 1.075] | no detectable difference |
| scroll / frameP50Ms | 11.1 [11.5] | 11.2 [11.7] | 1.011 [0.967, 1.035] | no detectable difference |
| scroll / frameP95Ms | 25.7 [27.2] | 26.0 [27.2] | 1.012 [0.868, 1.161] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 720.5 [763.6] | 682.2 [709.6] | 0.947 [0.919, 0.964] | faster |
| scroll / mainJsMs | 585.3 [619.5] | 540.2 [567.9] | 0.923 [0.896, 0.953] | faster |
| coldScroll / settleMs | 0.0 [1.5] | 0.0 [5.6] | +0.0 [+0.0, +3.7] | no detectable difference |
| coldScroll / wallMs | 2560.9 [2638.3] | 2505.4 [2905.9] | 0.978 [0.941, 1.047] | no detectable difference |
| coldScroll / frameP50Ms | 11.1 [11.5] | 11.1 [15.3] | 1.003 [0.990, 1.026] | no detectable difference |
| coldScroll / frameP95Ms | 26.5 [27.2] | 25.9 [27.0] | 0.975 [0.919, 1.035] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 739.3 [759.3] | 684.6 [717.6] | 0.926 [0.901, 0.959] | faster |
| coldScroll / mainJsMs | 584.7 [605.1] | 527.0 [559.8] | 0.901 [0.875, 0.930] | faster |
| jump / settleMs | 128.6 [150.8] | 129.7 [147.0] | +1.2 [-7.3, +9.1] | no detectable difference |
| jump / mainTaskMs | 120.6 [129.1] | 118.7 [123.0] | 0.984 [0.951, 1.001] | no detectable difference |
| jump / mainJsMs | 103.6 [108.9] | 100.6 [103.7] | 0.971 [0.937, 0.991] | no detectable difference |
| zoom / settleMs | 538.2 [551.7] | 532.3 [559.9] | -5.9 [-14.3, +19.1] | no detectable difference |
| zoom / zoom2SettleMs | 188.6 [199.0] | 186.7 [195.9] | -1.9 [-5.9, +1.9] | no detectable difference |
| zoom / zoom4SettleMs | 347.0 [361.9] | 345.9 [370.0] | -1.1 [-10.8, +13.6] | no detectable difference |
| zoom / backTo1SettleMs | 98.5 [109.9] | 96.7 [117.1] | -1.8 [-8.4, +7.0] | no detectable difference |
| zoom / mainTaskMs | 315.1 [330.5] | 298.5 [307.5] | 0.947 [0.918, 0.982] | no detectable difference |
| zoom / mainJsMs | 256.6 [267.3] | 240.5 [248.1] | 0.937 [0.914, 0.964] | no detectable difference |
| zoomLarge / settleMs | 316.0 [370.9] | 328.8 [356.6] | +12.8 [-20.7, +37.8] | no detectable difference |
| zoomLarge / zoom2SettleMs | 146.6 [174.4] | 148.5 [164.0] | +1.9 [-13.6, +18.6] | no detectable difference |
| zoomLarge / zoom4SettleMs | 179.8 [200.7] | 183.3 [198.9] | +3.5 [-18.5, +25.0] | no detectable difference |
| zoomLarge / backTo1SettleMs | 82.4 [97.3] | 80.5 [95.5] | -1.9 [-9.7, +2.6] | no detectable difference |
| zoomLarge / mainTaskMs | 252.9 [261.6] | 238.8 [252.4] | 0.944 [0.922, 0.983] | faster |
| zoomLarge / mainJsMs | 210.0 [217.1] | 195.7 [209.0] | 0.932 [0.911, 0.970] | faster |
| select / settleMs | 260.0 [287.6] | 268.2 [283.3] | +8.2 [-17.0, +22.3] | no detectable difference |
| select / mainTaskMs | 91.4 [95.0] | 83.5 [87.3] | 0.913 [0.890, 0.950] | faster |
| select / mainJsMs | 71.5 [75.1] | 63.2 [66.7] | 0.884 [0.856, 0.922] | faster |
| selectLarge / settleMs | 289.1 [310.4] | 261.9 [294.9] | -27.2 [-44.5, +10.1] | no detectable difference |
| selectLarge / mainTaskMs | 115.6 [120.4] | 107.1 [110.6] | 0.926 [0.869, 0.977] | no detectable difference |
| selectLarge / mainJsMs | 84.6 [89.4] | 78.1 [81.5] | 0.923 [0.856, 0.991] | no detectable difference |
| search / searchMs | 79.5 [82.8] | 71.7 [82.9] | 0.903 [0.878, 1.071] | no detectable difference |
| search / mainTaskMs | 11.0 [15.5] | 8.7 [19.7] | 0.796 [0.627, 1.314] | no detectable difference |
| search / mainJsMs | 7.2 [8.2] | 5.9 [9.7] | 0.824 [0.729, 1.026] | unreliable (A/A failed) |
| searchLarge / searchMs | 1840.4 [1855.2] | 1834.1 [1839.9] | 0.997 [0.993, 1.001] | no detectable difference |
| searchLarge / mainTaskMs | 862.5 [883.7] | 748.5 [762.3] | 0.868 [0.861, 0.872] | faster |
| searchLarge / mainJsMs | 816.0 [834.7] | 698.8 [713.3] | 0.856 [0.853, 0.864] | faster |
| annotate / settleMs | 218.1 [291.4] | 239.7 [258.8] | +21.6 [-42.6, +48.4] | no detectable difference |
| annotate / mainTaskMs | 88.6 [104.0] | 84.5 [90.0] | 0.954 [0.846, 1.045] | no detectable difference |
| annotate / mainJsMs | 61.3 [74.3] | 57.1 [62.4] | 0.931 [0.793, 1.033] | no detectable difference |
| annotateLarge / settleMs | 238.8 [268.5] | 245.3 [291.9] | +6.4 [-34.0, +42.4] | no detectable difference |
| annotateLarge / mainTaskMs | 116.9 [123.3] | 108.1 [122.0] | 0.924 [0.857, 0.984] | faster |
| annotateLarge / mainJsMs | 69.4 [75.1] | 68.1 [80.2] | 0.981 [0.877, 1.058] | no detectable difference |

## `main` vs `plugins-resulttext`

| Row | main median [p95] | plugins-resulttext median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.2 [67.9] | 90.7 [96.7] | 1.436 [1.386, 1.497] | slower |
| open(ebook) / openedMs | 137.0 [142.3] | 177.1 [182.7] | 1.293 [1.271, 1.324] | slower |
| open(ebook) / firstPaintMs | 252.8 [284.2] | 335.5 [354.4] | 1.327 [1.227, 1.366] | slower |
| open(ebook) / settledMs | 397.0 [405.4] | 395.1 [403.0] | 0.995 [0.984, 1.008] | no detectable difference |
| open(ebook) / mainTaskMs | 145.5 [147.9] | 234.0 [239.5] | 1.609 [1.590, 1.647] | slower |
| open(ebook) / mainJsMs | 111.9 [114.4] | 186.6 [190.2] | 1.667 [1.646, 1.707] | slower |
| open(large) / readyMs | 62.6 [64.8] | 92.1 [106.8] | 1.473 [1.410, 1.602] | slower |
| open(large) / openedMs | 158.1 [160.9] | 206.7 [221.9] | 1.307 [1.290, 1.347] | slower |
| open(large) / firstPaintMs | 266.5 [275.5] | 368.2 [382.1] | 1.382 [1.336, 1.412] | slower |
| open(large) / settledMs | 267.2 [276.5] | 363.8 [379.6] | 1.362 [1.326, 1.396] | slower |
| open(large) / mainTaskMs | 315.0 [318.0] | 294.6 [297.9] | 0.935 [0.923, 0.943] | faster |
| open(large) / mainJsMs | 284.8 [288.3] | 248.2 [250.4] | 0.872 [0.858, 0.878] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [15.5] | +0.0 [+0.0, +3.3] | no detectable difference |
| scroll / wallMs | 2863.2 [2975.7] | 2567.5 [2689.1] | 0.897 [0.861, 0.919] | faster |
| scroll / frameP50Ms | 11.2 [11.7] | 11.2 [11.7] | 1.005 [0.957, 1.033] | no detectable difference |
| scroll / frameP95Ms | 24.7 [27.1] | 26.0 [27.2] | 1.052 [0.883, 1.190] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 534.3 [546.9] | 682.2 [709.6] | 1.277 [1.261, 1.305] | slower |
| scroll / mainJsMs | 400.8 [410.4] | 540.2 [567.9] | 1.348 [1.324, 1.393] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [5.6] | +0.0 [+0.0, +3.7] | no detectable difference |
| coldScroll / wallMs | 2846.1 [2908.4] | 2505.4 [2905.9] | 0.880 [0.849, 0.917] | faster |
| coldScroll / frameP50Ms | 11.1 [11.7] | 11.1 [15.3] | 1.004 [0.973, 1.027] | no detectable difference |
| coldScroll / frameP95Ms | 25.5 [26.9] | 25.9 [27.0] | 1.016 [0.957, 1.096] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 560.6 [581.9] | 684.6 [717.6] | 1.221 [1.173, 1.257] | slower |
| coldScroll / mainJsMs | 389.0 [410.8] | 527.0 [559.8] | 1.355 [1.276, 1.390] | slower |
| jump / settleMs | 123.3 [138.4] | 129.7 [147.0] | +6.5 [-2.9, +11.9] | no detectable difference |
| jump / mainTaskMs | 73.6 [78.5] | 118.7 [123.0] | 1.613 [1.549, 1.656] | slower |
| jump / mainJsMs | 55.2 [59.0] | 100.6 [103.7] | 1.823 [1.741, 1.882] | slower |
| zoom / settleMs | 495.0 [517.8] | 532.3 [559.9] | +37.3 [+24.4, +62.2] | slower |
| zoom / zoom2SettleMs | 166.9 [178.9] | 186.7 [195.9] | +19.8 [+15.0, +27.0] | slower |
| zoom / zoom4SettleMs | 329.5 [343.8] | 345.9 [370.0] | +16.3 [+11.9, +34.1] | no detectable difference |
| zoom / backTo1SettleMs | 89.8 [96.3] | 96.7 [117.1] | +6.9 [+2.5, +16.1] | no detectable difference |
| zoom / mainTaskMs | 228.6 [234.2] | 298.5 [307.5] | 1.306 [1.268, 1.353] | slower |
| zoom / mainJsMs | 165.0 [168.5] | 240.5 [248.1] | 1.458 [1.418, 1.523] | slower |
| zoomLarge / settleMs | 311.9 [328.5] | 328.8 [356.6] | +16.9 [-4.9, +41.8] | no detectable difference |
| zoomLarge / zoom2SettleMs | 135.7 [166.7] | 148.5 [164.0] | +12.8 [-5.8, +22.3] | no detectable difference |
| zoomLarge / zoom4SettleMs | 169.8 [187.2] | 183.3 [198.9] | +13.6 [-9.7, +38.3] | no detectable difference |
| zoomLarge / backTo1SettleMs | 72.8 [81.3] | 80.5 [95.5] | +7.8 [+1.4, +14.1] | no detectable difference |
| zoomLarge / mainTaskMs | 173.6 [179.5] | 238.8 [252.4] | 1.375 [1.333, 1.427] | slower |
| zoomLarge / mainJsMs | 129.5 [133.0] | 195.7 [209.0] | 1.511 [1.479, 1.579] | slower |
| select / settleMs | 212.4 [240.4] | 268.2 [283.3] | +55.7 [+30.1, +71.3] | slower |
| select / mainTaskMs | 61.6 [63.7] | 83.5 [87.3] | 1.354 [1.318, 1.421] | slower |
| select / mainJsMs | 39.9 [41.2] | 63.2 [66.7] | 1.584 [1.533, 1.699] | slower |
| selectLarge / settleMs | 216.1 [255.2] | 261.9 [294.9] | +45.8 [+20.3, +80.7] | slower |
| selectLarge / mainTaskMs | 71.4 [76.5] | 107.1 [110.6] | 1.500 [1.402, 1.607] | slower |
| selectLarge / mainJsMs | 47.9 [51.5] | 78.1 [81.5] | 1.632 [1.499, 1.760] | slower |
| search / searchMs | 70.9 [73.3] | 71.7 [82.9] | 1.011 [0.981, 1.185] | no detectable difference |
| search / mainTaskMs | 5.5 [6.0] | 8.7 [19.7] | 1.585 [1.447, 2.537] | slower |
| search / mainJsMs | 3.6 [3.8] | 5.9 [9.7] | 1.632 [1.563, 1.974] | unreliable (A/A failed) |
| searchLarge / searchMs | 1886.1 [1933.6] | 1834.1 [1839.9] | 0.972 [0.962, 0.979] | no detectable difference |
| searchLarge / mainTaskMs | 343.3 [345.8] | 748.5 [762.3] | 2.180 [2.168, 2.205] | slower |
| searchLarge / mainJsMs | 289.7 [292.4] | 698.8 [713.3] | 2.412 [2.399, 2.430] | slower |
| annotate / settleMs | 230.6 [281.7] | 239.7 [258.8] | +9.1 [-53.3, +38.5] | no detectable difference |
| annotate / mainTaskMs | 80.5 [88.0] | 84.5 [90.0] | 1.050 [0.950, 1.136] | no detectable difference |
| annotate / mainJsMs | 49.0 [52.1] | 57.1 [62.4] | 1.166 [1.043, 1.299] | slower |
| annotateLarge / settleMs | 233.9 [273.6] | 245.3 [291.9] | +11.4 [-29.1, +67.8] | no detectable difference |
| annotateLarge / mainTaskMs | 83.8 [92.1] | 108.1 [122.0] | 1.289 [1.190, 1.391] | slower |
| annotateLarge / mainJsMs | 50.2 [55.5] | 68.1 [80.2] | 1.357 [1.206, 1.493] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-plainargs median [p95] | plugins-resulttext median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 35.35 MiB [35.37 MiB] | 35.35 MiB [35.91 MiB] | 37.05 MiB [38.04 MiB] | 37.11 MiB [38.11 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.16 MiB [48.17 MiB] | 48.16 MiB [48.18 MiB] | 52.51 MiB [52.54 MiB] | 52.52 MiB [52.55 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.28 MiB] | 22.26 MiB [22.29 MiB] | 23.00 MiB [23.03 MiB] | 23.01 MiB [23.03 MiB] |
| soak / heapCycle5Bytes | 38.42 MiB [38.98 MiB] | 38.42 MiB [38.42 MiB] | 40.23 MiB [41.24 MiB] | 41.16 MiB [42.28 MiB] |
| soak / heapCycle30Bytes | 39.26 MiB [39.82 MiB] | 39.26 MiB [39.28 MiB] | 40.77 MiB [41.78 MiB] | 41.70 MiB [42.82 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-plainargs`:

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

