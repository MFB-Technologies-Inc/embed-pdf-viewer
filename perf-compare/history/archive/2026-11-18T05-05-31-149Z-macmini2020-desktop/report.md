# Viewer perf comparison: 2026-11-18T05-05-31-149Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 096115a21c513dc106535b0c0b54b91b5f3d8422 |
| Chromium / Playwright | 140.0.7339.16 / 1.55.0 |
| Node | v24.20.0 |
| OS | Darwin 27.3.0 arm64 |
| CPU | Apple M1, 8 cores |
| Memory | 16 GiB |
| Power | AC Power |
| Viewport | 1280x800 @2x |
| Frame interval | 11.1 ms (median `scroll / frameP50Ms` of `main`, 10 samples) |
| Fixtures | ebook 2e539f62b5fa, large 10c8668fe55c, annots f27197ce9ac9, forms b973ccec7bf6 |

| Arm | Ref | SHA | Payload | Engine wasm sha256 | Kernel wasm sha256 |
| --- | --- | --- | --- | --- | --- |
| main | main | 2516e2786 | fetched | 98c2368a774d |  |
| main-aa | main | 2516e2786 | fetched | 98c2368a774d |  |
| main-now | main | ed064e0c8 | fetched | 7195838021c8 |  |
| perf-fixes-end | research/perf-fixes | a6d4d5d14 | built (fork 17d8073d4, wasm32-eh) | 172b5a139647 | d5eefdeebf95 |

## Gate

- `main`: pass
- `main-aa`: pass
- `main-now`: pass
- `perf-fixes-end`: pass

Not run, because the arm's viewer lacks what the flow needs:

- `main`: `undo`
- `main-aa`: `undo`

## Sizes

| Metric | main | main-aa | main-now | perf-fixes-end |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 7.52 MiB | 14.15 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.35 MiB | 3.77 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 10.73 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 2.50 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 9.50 MiB | 8.50 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 3.12 MiB | 2.90 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

83 of 86 rows pass.

| Row | main median [max] | main-aa median [max] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.8 [70.0] | 62.6 [72.3] | 0.981 [0.929, 1.070] | pass |
| open(ebook) / openedMs | 193.2 [204.7] | 185.2 [199.5] | 0.959 [0.714, 1.296] | pass |
| open(ebook) / firstPaintMs | 306.7 [322.3] | 298.9 [338.6] | 0.975 [0.828, 1.168] | pass |
| open(ebook) / settledMs | 460.8 [465.0] | 446.9 [465.9] | 0.970 [0.869, 1.087] | pass |
| open(ebook) / mainTaskMs | 151.7 [157.9] | 150.6 [156.2] | 0.993 [0.978, 1.022] | pass |
| open(ebook) / mainJsMs | 118.1 [124.4] | 118.1 [121.5] | 1.000 [0.985, 1.026] | pass |
| open(large) / readyMs | 62.7 [68.5] | 60.9 [70.2] | 0.972 [0.938, 1.013] | pass |
| open(large) / openedMs | 159.2 [165.9] | 158.1 [165.5] | 0.993 [0.977, 1.017] | pass |
| open(large) / firstPaintMs | 256.4 [273.8] | 256.7 [269.6] | 1.001 [0.965, 1.056] | pass |
| open(large) / settledMs | 273.0 [298.4] | 276.8 [286.6] | 1.014 [0.978, 1.044] | pass |
| open(large) / mainTaskMs | 321.3 [328.7] | 317.8 [324.0] | 0.989 [0.979, 1.002] | pass |
| open(large) / mainJsMs | 290.1 [297.0] | 287.6 [294.0] | 0.991 [0.982, 1.007] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2836.7 [2922.2] | 2848.2 [3743.9] | 1.004 [0.965, 1.038] | pass |
| scroll / frameP50Ms | 11.1 [11.9] | 11.1 [21.1] | 0.999 [0.972, 1.046] | pass |
| scroll / frameP95Ms | 26.2 [27.3] | 25.4 [27.0] | 0.969 [0.891, 1.170] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 585.1 [607.5] | 582.6 [677.2] | 0.996 [0.969, 1.030] | pass |
| scroll / mainJsMs | 464.4 [477.9] | 465.7 [547.3] | 1.003 [0.968, 1.039] | pass |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [2.6] | +0.0 [+0.0, +0.0] | pass |
| scrollMid / wallMs | 2854.9 [3790.5] | 2900.2 [3015.7] | 1.016 [0.870, 1.035] | pass |
| scrollMid / frameP50Ms | 11.1 [20.7] | 11.2 [11.9] | 1.009 [0.701, 1.040] | pass |
| scrollMid / frameP95Ms | 24.8 [27.2] | 25.4 [27.1] | 1.024 [0.886, 1.119] | pass |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scrollMid / mainTaskMs | 613.5 [721.0] | 613.2 [629.1] | 1.000 [0.915, 1.038] | pass |
| scrollMid / mainJsMs | 475.7 [567.1] | 482.0 [498.9] | 1.013 [0.917, 1.054] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2885.4 [3530.1] | 2821.9 [2883.1] | 0.978 [0.943, 0.999] | pass |
| coldScroll / frameP50Ms | 11.4 [20.1] | 11.2 [11.9] | 0.986 [0.768, 1.039] | pass |
| coldScroll / frameP95Ms | 24.4 [27.2] | 26.4 [27.2] | 1.081 [0.942, 1.210] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 616.0 [672.0] | 599.9 [627.0] | 0.974 [0.943, 1.005] | pass |
| coldScroll / mainJsMs | 456.0 [508.0] | 445.5 [469.9] | 0.977 [0.943, 0.997] | pass |
| jump / settleMs | 127.9 [143.0] | 132.8 [145.0] | +4.9 [-4.4, +20.1] | pass |
| jump / mainTaskMs | 76.8 [89.0] | 76.5 [83.4] | 0.997 [0.929, 1.100] | pass |
| jump / mainJsMs | 59.6 [69.5] | 59.8 [68.5] | 1.002 [0.958, 1.097] | pass |
| zoom / settleMs | 445.7 [518.7] | 478.1 [517.3] | +32.4 [-10.9, +52.9] | pass |
| zoom / zoom2SettleMs | 169.7 [179.8] | 175.8 [194.4] | +6.1 [-9.3, +20.4] | pass |
| zoom / zoom4SettleMs | 275.3 [343.1] | 303.1 [330.7] | +27.7 [-13.4, +51.7] | pass |
| zoom / backTo1SettleMs | 99.1 [114.4] | 100.2 [120.2] | +1.1 [-10.9, +14.5] | pass |
| zoom / mainTaskMs | 240.7 [273.2] | 258.2 [275.2] | 1.073 [0.948, 1.139] | pass |
| zoom / mainJsMs | 184.3 [208.9] | 196.4 [211.2] | 1.066 [0.957, 1.118] | pass |
| zoomLarge / settleMs | 304.5 [352.3] | 310.0 [362.1] | +5.5 [-6.9, +22.0] | pass |
| zoomLarge / zoom2SettleMs | 144.2 [167.2] | 144.2 [180.2] | +0.0 [-10.0, +17.0] | pass |
| zoomLarge / zoom4SettleMs | 165.6 [205.2] | 168.0 [185.4] | +2.4 [-7.7, +16.6] | pass |
| zoomLarge / backTo1SettleMs | 89.6 [106.2] | 95.4 [132.0] | +5.9 [-9.8, +19.1] | pass |
| zoomLarge / mainTaskMs | 212.6 [220.2] | 215.5 [226.4] | 1.014 [0.979, 1.039] | pass |
| zoomLarge / mainJsMs | 171.4 [176.6] | 172.8 [183.3] | 1.008 [0.978, 1.037] | pass |
| select / settleMs | 206.6 [261.2] | 250.4 [291.7] | +43.8 [+7.7, +57.1] | A/A failed |
| select / mainTaskMs | 69.5 [77.0] | 75.2 [84.4] | 1.082 [1.004, 1.166] | A/A failed |
| select / mainJsMs | 50.0 [56.1] | 54.2 [61.6] | 1.083 [1.006, 1.172] | A/A failed |
| selectLarge / settleMs | 212.4 [246.3] | 223.7 [258.7] | +11.3 [-20.4, +30.3] | pass |
| selectLarge / mainTaskMs | 80.0 [88.0] | 78.0 [87.7] | 0.975 [0.909, 1.036] | pass |
| selectLarge / mainJsMs | 59.0 [64.6] | 57.6 [64.5] | 0.976 [0.899, 1.039] | pass |
| search / searchMs | 74.8 [79.0] | 73.6 [83.1] | 0.985 [0.941, 1.110] | pass |
| search / mainTaskMs | 5.7 [6.6] | 6.0 [7.1] | 1.059 [0.953, 1.195] | pass |
| search / mainJsMs | 4.2 [4.8] | 4.4 [4.8] | 1.054 [0.936, 1.123] | pass |
| searchLarge / searchMs | 1904.5 [1983.5] | 1892.2 [2085.4] | 0.994 [0.978, 1.010] | pass |
| searchLarge / mainTaskMs | 361.1 [363.3] | 360.2 [368.3] | 0.997 [0.990, 1.004] | pass |
| searchLarge / mainJsMs | 308.3 [313.8] | 307.8 [312.7] | 0.998 [0.992, 1.009] | pass |
| annotate / settleMs | 215.2 [250.9] | 222.5 [256.6] | +7.4 [-15.1, +47.3] | pass |
| annotate / mainTaskMs | 79.6 [90.9] | 84.0 [92.5] | 1.056 [0.987, 1.126] | pass |
| annotate / mainJsMs | 54.9 [62.2] | 57.0 [63.2] | 1.039 [0.991, 1.115] | pass |
| annotateLarge / settleMs | 211.9 [259.0] | 226.5 [257.9] | +14.5 [-36.1, +43.1] | pass |
| annotateLarge / mainTaskMs | 86.4 [94.4] | 85.2 [97.3] | 0.986 [0.908, 1.088] | pass |
| annotateLarge / mainJsMs | 58.7 [64.3] | 57.9 [66.7] | 0.987 [0.906, 1.092] | pass |
| hover / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass (no done condition on `main` and `main-aa`) |
| hover / wallMs | 1032.7 [1260.8] | 1075.8 [1163.2] | 1.042 [0.983, 1.089] | pass (no done condition on `main` and `main-aa`) |
| hover / mainTaskMs | 289.4 [313.9] | 293.6 [315.2] | 1.014 [0.974, 1.070] | pass (no done condition on `main` and `main-aa`) |
| hover / mainJsMs | 259.9 [282.1] | 264.2 [282.8] | 1.016 [0.970, 1.074] | pass (no done condition on `main` and `main-aa`) |
| dragAnnot / settleMs | 297.5 [346.8] | 321.8 [362.0] | +24.4 [-16.9, +48.7] | pass |
| dragAnnot / mainTaskMs | 131.4 [137.0] | 128.5 [146.1] | 0.978 [0.917, 1.072] | pass |
| dragAnnot / mainJsMs | 99.2 [102.5] | 98.6 [109.4] | 0.994 [0.953, 1.060] | pass |
| drawInk / settleMs | 832.0 [838.6] | 833.2 [836.8] | +1.2 [-4.5, +5.4] | pass |
| drawInk / wallMs | 2893.3 [3013.5] | 2904.8 [4650.5] | 1.004 [0.978, 1.035] | pass |
| drawInk / mainTaskMs | 663.4 [694.1] | 663.7 [1002.6] | 1.000 [0.971, 1.042] | pass |
| drawInk / mainJsMs | 476.0 [498.8] | 481.9 [729.5] | 1.012 [0.980, 1.051] | pass |
| typeField / settleMs | 161.2 [172.1] | 167.3 [180.8] | +6.1 [-1.6, +16.4] | pass |
| typeField / mainTaskMs | 115.6 [119.5] | 118.7 [125.3] | 1.027 [0.999, 1.125] | pass |
| typeField / mainJsMs | 79.9 [85.4] | 79.5 [83.3] | 0.995 [0.970, 1.043] | pass |

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

Not run: `plugins-plainargs`, `plugins-resulttext`.

## `main` vs `plugins-resulttext`

Not run: `plugins-resulttext`.

## `plugins-resulttext` vs `plugins-rustreads`

Not run: `plugins-resulttext`, `plugins-rustreads`.

## `main` vs `plugins-rustreads`

Not run: `plugins-rustreads`.

## `plugins-resulttext` vs `plugins-tip7`

Not run: `plugins-resulttext`, `plugins-tip7`.

## `main` vs `plugins-tip7`

Not run: `plugins-tip7`.

## `main` vs `plugins-base`

Not run: `plugins-base`.

## `plugins-base` vs `plugins-next`

Not run: `plugins-base`, `plugins-next`.

## `main` vs `plugins-next`

Not run: `plugins-next`.

## `main` vs `main-now`

| Row | main median [max] | main-now median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.8 [70.0] | 57.6 [62.8] | 0.903 [0.813, 0.924] | ±7.1% | faster |
| open(ebook) / openedMs | 193.2 [204.7] | 209.1 [230.4] | 1.082 [1.058, 1.460] | ±29.6% | no detectable difference |
| open(ebook) / firstPaintMs | 306.7 [322.3] | 342.9 [385.1] | 1.118 [1.067, 1.340] | ±17.2% | no detectable difference |
| open(ebook) / settledMs | 460.8 [465.0] | 368.6 [386.7] | 0.800 [0.780, 0.899] | ±13.1% | faster |
| open(ebook) / mainTaskMs | 151.7 [157.9] | 213.0 [228.8] | 1.404 [1.377, 1.427] | ±3.0% | slower |
| open(ebook) / mainJsMs | 118.1 [124.4] | 177.7 [192.2] | 1.505 [1.480, 1.521] | ±3.0% | slower |
| open(large) / readyMs | 62.7 [68.5] | 52.5 [59.5] | 0.837 [0.806, 0.906] | ±6.2% | faster |
| open(large) / openedMs | 159.2 [165.9] | 216.3 [223.9] | 1.359 [1.347, 1.380] | ±3.0% | slower |
| open(large) / firstPaintMs | 256.4 [273.8] | 321.0 [359.7] | 1.252 [1.209, 1.368] | ±5.6% | slower |
| open(large) / settledMs | 273.0 [298.4] | 348.6 [368.4] | 1.277 [1.229, 1.321] | ±4.4% | slower |
| open(large) / mainTaskMs | 321.3 [328.7] | 208.6 [222.1] | 0.649 [0.642, 0.666] | ±3.0% | faster |
| open(large) / mainJsMs | 290.1 [297.0] | 175.3 [184.2] | 0.604 [0.591, 0.614] | ±3.0% | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 2836.7 [2922.2] | 3008.7 [3082.8] | 1.061 [1.024, 1.083] | ±3.8% | slower |
| scroll / frameP50Ms | 11.1 [11.9] | 11.3 [11.8] | 1.010 [0.972, 1.058] | ±4.6% | no detectable difference |
| scroll / frameP95Ms | 26.2 [27.3] | 25.8 [27.2] | 0.982 [0.860, 1.189] | ±17.0% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 585.1 [607.5] | 864.9 [896.0] | 1.478 [1.432, 1.524] | ±3.1% | slower |
| scroll / mainJsMs | 464.4 [477.9] | 734.7 [761.2] | 1.582 [1.530, 1.637] | ±3.9% | slower |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 2854.9 [3790.5] | 3039.4 [3178.0] | 1.065 [0.910, 1.085] | ±13.0% | no detectable difference |
| scrollMid / frameP50Ms | 11.1 [20.7] | 11.2 [11.8] | 1.015 [0.705, 1.040] | ±29.9% | no detectable difference |
| scrollMid / frameP95Ms | 24.8 [27.2] | 26.6 [27.3] | 1.071 [1.019, 1.156] | ±11.9% | no detectable difference |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 613.5 [721.0] | 941.8 [955.0] | 1.535 [1.407, 1.596] | ±8.5% | slower |
| scrollMid / mainJsMs | 475.7 [567.1] | 815.0 [828.7] | 1.713 [1.557, 1.776] | ±8.3% | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 2885.4 [3530.1] | 2958.2 [3218.2] | 1.025 [0.991, 1.050] | ±5.7% | no detectable difference |
| coldScroll / frameP50Ms | 11.4 [20.1] | 11.1 [15.1] | 0.980 [0.767, 1.087] | ±23.2% | no detectable difference |
| coldScroll / frameP95Ms | 24.4 [27.2] | 25.2 [27.0] | 1.031 [0.851, 1.172] | ±21.0% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 616.0 [672.0] | 901.2 [939.9] | 1.463 [1.418, 1.501] | ±5.7% | slower |
| coldScroll / mainJsMs | 456.0 [508.0] | 746.0 [789.7] | 1.636 [1.585, 1.669] | ±5.7% | slower |
| jump / settleMs | 127.9 [143.0] | 141.0 [168.2] | +13.1 [-4.8, +37.6] | ±20.1 | no detectable difference |
| jump / mainTaskMs | 76.8 [89.0] | 129.5 [143.0] | 1.687 [1.602, 1.876] | ±10.0% | slower |
| jump / mainJsMs | 59.6 [69.5] | 113.6 [121.9] | 1.905 [1.819, 2.082] | ±9.7% | slower |
| zoom / settleMs | 445.7 [518.7] | 610.0 [646.8] | +164.3 [+127.9, +182.6] | ±52.9 | slower |
| zoom / zoom2SettleMs | 169.7 [179.8] | 207.7 [232.0] | +38.0 [+12.5, +54.6] | ±20.4 | slower |
| zoom / zoom4SettleMs | 275.3 [343.1] | 407.5 [418.7] | +132.2 [+98.6, +147.7] | ±51.7 | slower |
| zoom / backTo1SettleMs | 99.1 [114.4] | 99.3 [112.2] | +0.2 [-10.6, +11.4] | ±14.5 | no detectable difference |
| zoom / mainTaskMs | 240.7 [273.2] | 370.1 [381.7] | 1.538 [1.375, 1.598] | ±13.9% | slower |
| zoom / mainJsMs | 184.3 [208.9] | 290.6 [304.8] | 1.577 [1.415, 1.622] | ±11.8% | slower |
| zoomLarge / settleMs | 304.5 [352.3] | 299.4 [313.9] | -5.1 [-20.5, +5.9] | ±22.0 | no detectable difference |
| zoomLarge / zoom2SettleMs | 144.2 [167.2] | 152.9 [159.4] | +8.7 [-2.9, +24.7] | ±17.0 | no detectable difference |
| zoomLarge / zoom4SettleMs | 165.6 [205.2] | 150.4 [166.8] | -15.2 [-29.5, -5.1] | ±16.6 | no detectable difference |
| zoomLarge / backTo1SettleMs | 89.6 [106.2] | 110.0 [127.2] | +20.4 [+10.1, +24.6] | ±19.1 | slower |
| zoomLarge / mainTaskMs | 212.6 [220.2] | 311.3 [319.8] | 1.464 [1.368, 1.513] | ±3.9% | slower |
| zoomLarge / mainJsMs | 171.4 [176.6] | 256.1 [261.4] | 1.494 [1.401, 1.549] | ±3.7% | slower |
| select / settleMs | 206.6 [261.2] | 228.3 [254.9] | +21.7 [-10.7, +39.4] |  | unreliable (A/A failed) |
| select / mainTaskMs | 69.5 [77.0] | 89.4 [94.1] | 1.287 [1.189, 1.379] |  | unreliable (A/A failed) |
| select / mainJsMs | 50.0 [56.1] | 71.6 [75.6] | 1.433 [1.316, 1.529] |  | unreliable (A/A failed) |
| selectLarge / settleMs | 212.4 [246.3] | 220.9 [264.5] | +8.5 [-28.5, +29.8] | ±30.3 | no detectable difference |
| selectLarge / mainTaskMs | 80.0 [88.0] | 101.4 [110.6] | 1.267 [1.214, 1.326] | ±9.1% | slower |
| selectLarge / mainJsMs | 59.0 [64.6] | 83.6 [91.4] | 1.417 [1.367, 1.515] | ±10.1% | slower |
| search / searchMs | 74.8 [79.0] | 26.2 [29.8] | 0.350 [0.334, 0.370] | ±11.0% | faster |
| search / mainTaskMs | 5.7 [6.6] | 10.2 [17.7] | 1.789 [1.652, 2.259] | ±19.5% | slower |
| search / mainJsMs | 4.2 [4.8] | 7.6 [8.3] | 1.792 [1.670, 1.889] | ±12.3% | slower |
| searchLarge / searchMs | 1904.5 [1983.5] | 2235.2 [2249.9] | 1.174 [1.156, 1.186] | ±3.0% | slower |
| searchLarge / mainTaskMs | 361.1 [363.3] | 397.7 [408.0] | 1.101 [1.092, 1.126] | ±3.0% | slower |
| searchLarge / mainJsMs | 308.3 [313.8] | 345.1 [349.2] | 1.119 [1.112, 1.128] | ±3.0% | slower |
| annotate / settleMs | 215.2 [250.9] | 217.4 [280.7] | +2.3 [-32.5, +61.1] | ±47.3 | no detectable difference |
| annotate / mainTaskMs | 79.6 [90.9] | 105.5 [112.7] | 1.325 [1.247, 1.374] | ±12.6% | slower |
| annotate / mainJsMs | 54.9 [62.2] | 84.2 [90.9] | 1.534 [1.470, 1.601] | ±11.5% | slower |
| annotateLarge / settleMs | 211.9 [259.0] | 228.0 [288.0] | +16.1 [-37.2, +51.8] | ±43.1 | no detectable difference |
| annotateLarge / mainTaskMs | 86.4 [94.4] | 112.0 [121.5] | 1.296 [1.226, 1.379] | ±9.2% | slower |
| annotateLarge / mainJsMs | 58.7 [64.3] | 90.8 [99.6] | 1.546 [1.444, 1.649] | ±9.4% | slower |
| hover / settleMs | 0.0 [0.0] | 1.3 [1.5] | +1.3 [+1.2, +1.4] | ±0.0 | slower (no done condition on `main`) |
| hover / wallMs | 1032.7 [1260.8] | 1055.0 [1118.3] | 1.022 [0.979, 1.072] | ±8.9% | no detectable difference (no done condition on `main`) |
| hover / mainTaskMs | 289.4 [313.9] | 446.2 [462.8] | 1.541 [1.509, 1.610] | ±7.0% | slower (no done condition on `main`) |
| hover / mainJsMs | 259.9 [282.1] | 413.5 [433.0] | 1.591 [1.556, 1.667] | ±7.4% | slower (no done condition on `main`) |
| dragAnnot / settleMs | 297.5 [346.8] | 424.5 [457.5] | +127.0 [+40.9, +146.6] | ±48.7 | slower |
| dragAnnot / mainTaskMs | 131.4 [137.0] | 227.7 [238.2] | 1.733 [1.621, 1.817] | ±8.3% | slower |
| dragAnnot / mainJsMs | 99.2 [102.5] | 201.8 [210.4] | 2.035 [1.884, 2.128] | ±6.0% | slower |
| drawInk / settleMs | 832.0 [838.6] | 864.1 [872.9] | +32.1 [+21.2, +37.5] | ±25.0 | slower |
| drawInk / wallMs | 2893.3 [3013.5] | 3061.1 [3260.5] | 1.058 [1.033, 1.087] | ±3.5% | slower |
| drawInk / mainTaskMs | 663.4 [694.1] | 1323.5 [1352.7] | 1.995 [1.943, 2.041] | ±4.2% | slower |
| drawInk / mainJsMs | 476.0 [498.8] | 1117.8 [1141.5] | 2.348 [2.287, 2.433] | ±5.1% | slower |
| typeField / settleMs | 161.2 [172.1] | 153.8 [171.7] | -7.3 [-17.0, +2.5] | ±16.4 | no detectable difference |
| typeField / mainTaskMs | 115.6 [119.5] | 119.6 [128.3] | 1.034 [1.001, 1.143] | ±12.5% | no detectable difference |
| typeField / mainJsMs | 79.9 [85.4] | 85.2 [93.5] | 1.066 [1.029, 1.137] | ±4.3% | slower |

## `main-now` vs `plugins-next`

Not run: `plugins-next`.

## `plugins-next` vs `plugins-next-lto`

Not run: `plugins-next`, `plugins-next-lto`.

## `phase-c-base` vs `phase-c-end`

Not run: `phase-c-base`, `phase-c-end`.

## `phase-c-end` vs `phase-d-end`

Not run: `phase-c-end`, `phase-d-end`.

## `main-now` vs `phase-d-end`

Not run: `phase-d-end`.

## `phase-d-end` vs `perf-fixes-end`

Not run: `phase-d-end`.

## `main-now` vs `perf-fixes-end`

| Row | main-now median [max] | perf-fixes-end median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 57.6 [62.8] | 114.5 [123.5] | 1.987 [1.930, 2.201] | ±7.1% | slower |
| open(ebook) / openedMs | 209.1 [230.4] | 337.0 [347.7] | 1.612 [1.402, 1.646] | ±29.6% | slower |
| open(ebook) / firstPaintMs | 342.9 [385.1] | 530.3 [552.6] | 1.546 [1.379, 1.619] | ±17.2% | slower |
| open(ebook) / settledMs | 368.6 [386.7] | 574.2 [588.1] | 1.558 [1.466, 1.614] | ±13.1% | slower |
| open(ebook) / mainTaskMs | 213.0 [228.8] | 403.8 [414.2] | 1.896 [1.880, 1.929] | ±3.0% | slower |
| open(ebook) / mainJsMs | 177.7 [192.2] | 341.7 [351.6] | 1.923 [1.901, 1.955] | ±3.0% | slower |
| open(large) / readyMs | 52.5 [59.5] | 111.5 [123.0] | 2.124 [1.944, 2.259] | ±6.2% | slower |
| open(large) / openedMs | 216.3 [223.9] | 316.2 [343.1] | 1.461 [1.432, 1.500] | ±3.0% | slower |
| open(large) / firstPaintMs | 321.0 [359.7] | 500.1 [532.7] | 1.558 [1.451, 1.656] | ±5.6% | slower |
| open(large) / settledMs | 348.6 [368.4] | 551.5 [579.5] | 1.582 [1.519, 1.628] | ±4.4% | slower |
| open(large) / mainTaskMs | 208.6 [222.1] | 424.8 [445.3] | 2.037 [1.978, 2.073] | ±3.0% | slower |
| open(large) / mainJsMs | 175.3 [184.2] | 366.0 [380.4] | 2.088 [2.044, 2.136] | ±3.0% | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [6.2] | +0.0 [+0.0, +0.6] | ±0.0 | no detectable difference |
| scroll / wallMs | 3008.7 [3082.8] | 2671.4 [3593.2] | 0.888 [0.858, 0.923] | ±3.8% | faster |
| scroll / frameP50Ms | 11.3 [11.8] | 11.2 [20.6] | 0.997 [0.958, 1.045] | ±4.6% | no detectable difference |
| scroll / frameP95Ms | 25.8 [27.2] | 26.6 [27.2] | 1.032 [0.957, 1.179] | ±17.0% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 864.9 [896.0] | 1192.3 [1293.7] | 1.379 [1.339, 1.425] | ±3.1% | slower |
| scroll / mainJsMs | 734.7 [761.2] | 1051.9 [1146.1] | 1.432 [1.404, 1.477] | ±3.9% | slower |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [9.5] | +0.0 [+0.0, +0.6] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 3039.4 [3178.0] | 2714.3 [2828.7] | 0.893 [0.829, 0.924] | ±13.0% | no detectable difference |
| scrollMid / frameP50Ms | 11.2 [11.8] | 11.2 [11.8] | 0.997 [0.977, 1.046] | ±29.9% | no detectable difference |
| scrollMid / frameP95Ms | 26.6 [27.3] | 26.5 [27.3] | 0.997 [0.857, 1.035] | ±11.9% | no detectable difference |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 941.8 [955.0] | 1274.0 [1298.5] | 1.353 [1.272, 1.377] | ±8.5% | slower |
| scrollMid / mainJsMs | 815.0 [828.7] | 1127.0 [1152.9] | 1.383 [1.343, 1.409] | ±8.3% | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [12.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 2958.2 [3218.2] | 2640.2 [2953.5] | 0.893 [0.866, 0.923] | ±5.7% | faster |
| coldScroll / frameP50Ms | 11.1 [15.1] | 11.0 [11.7] | 0.993 [0.862, 1.017] | ±23.2% | no detectable difference |
| coldScroll / frameP95Ms | 25.2 [27.0] | 26.6 [27.2] | 1.056 [0.993, 1.274] | ±21.0% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 901.2 [939.9] | 1237.2 [1289.7] | 1.373 [1.349, 1.393] | ±5.7% | slower |
| coldScroll / mainJsMs | 746.0 [789.7] | 1081.1 [1131.4] | 1.449 [1.421, 1.470] | ±5.7% | slower |
| jump / settleMs | 141.0 [168.2] | 133.7 [170.4] | -7.3 [-37.1, +16.3] | ±20.1 | no detectable difference |
| jump / mainTaskMs | 129.5 [143.0] | 167.2 [181.5] | 1.291 [1.179, 1.333] | ±10.0% | slower |
| jump / mainJsMs | 113.6 [121.9] | 155.0 [160.8] | 1.364 [1.252, 1.412] | ±9.7% | slower |
| zoom / settleMs | 610.0 [646.8] | 659.0 [711.3] | +49.0 [+36.5, +68.5] | ±52.9 | no detectable difference |
| zoom / zoom2SettleMs | 207.7 [232.0] | 232.9 [242.8] | +25.2 [+7.5, +49.7] | ±20.4 | slower |
| zoom / zoom4SettleMs | 407.5 [418.7] | 428.6 [475.5] | +21.1 [+9.0, +37.6] | ±51.7 | no detectable difference |
| zoom / backTo1SettleMs | 99.3 [112.2] | 88.0 [106.4] | -11.3 [-27.8, -2.9] | ±14.5 | no detectable difference |
| zoom / mainTaskMs | 370.1 [381.7] | 459.7 [507.6] | 1.242 [1.183, 1.341] | ±13.9% | slower |
| zoom / mainJsMs | 290.6 [304.8] | 392.1 [430.7] | 1.349 [1.305, 1.432] | ±11.8% | slower |
| zoomLarge / settleMs | 299.4 [313.9] | 312.5 [330.9] | +13.1 [+1.3, +28.9] | ±22.0 | no detectable difference |
| zoomLarge / zoom2SettleMs | 152.9 [159.4] | 160.4 [176.7] | +7.5 [-3.9, +20.0] | ±17.0 | no detectable difference |
| zoomLarge / zoom4SettleMs | 150.4 [166.8] | 153.5 [161.4] | +3.1 [-5.1, +15.6] | ±16.6 | no detectable difference |
| zoomLarge / backTo1SettleMs | 110.0 [127.2] | 100.4 [112.0] | -9.6 [-14.3, -0.1] | ±19.1 | no detectable difference |
| zoomLarge / mainTaskMs | 311.3 [319.8] | 368.8 [395.2] | 1.185 [1.143, 1.270] | ±3.9% | slower |
| zoomLarge / mainJsMs | 256.1 [261.4] | 332.1 [356.5] | 1.297 [1.244, 1.380] | ±3.7% | slower |
| select / settleMs | 228.3 [254.9] | 275.7 [328.5] | +47.4 [+19.9, +78.5] |  | unreliable (A/A failed) |
| select / mainTaskMs | 89.4 [94.1] | 128.3 [142.1] | 1.435 [1.295, 1.572] |  | unreliable (A/A failed) |
| select / mainJsMs | 71.6 [75.6] | 113.6 [126.0] | 1.586 [1.431, 1.734] |  | unreliable (A/A failed) |
| selectLarge / settleMs | 220.9 [264.5] | 263.3 [280.2] | +42.4 [+20.4, +67.2] | ±30.3 | slower |
| selectLarge / mainTaskMs | 101.4 [110.6] | 132.0 [140.4] | 1.302 [1.225, 1.349] | ±9.1% | slower |
| selectLarge / mainJsMs | 83.6 [91.4] | 118.1 [126.5] | 1.413 [1.309, 1.468] | ±10.1% | slower |
| search / searchMs | 26.2 [29.8] | 46.3 [49.7] | 1.770 [1.240, 1.863] | ±11.0% | slower |
| search / mainTaskMs | 10.2 [17.7] | 30.0 [35.9] | 2.932 [2.130, 3.278] | ±19.5% | slower |
| search / mainJsMs | 7.6 [8.3] | 21.8 [25.8] | 2.886 [2.373, 3.176] | ±12.3% | slower |
| searchLarge / searchMs | 2235.2 [2249.9] | 1934.7 [1957.4] | 0.866 [0.861, 0.872] | ±3.0% | faster |
| searchLarge / mainTaskMs | 397.7 [408.0] | 1211.2 [1278.9] | 3.046 [2.774, 3.158] | ±3.0% | slower |
| searchLarge / mainJsMs | 345.1 [349.2] | 909.2 [922.2] | 2.635 [2.580, 2.651] | ±3.0% | slower |
| annotate / settleMs | 217.4 [280.7] | 216.0 [272.2] | -1.5 [-41.4, +43.5] | ±47.3 | no detectable difference |
| annotate / mainTaskMs | 105.5 [112.7] | 124.4 [139.0] | 1.179 [1.106, 1.247] | ±12.6% | slower |
| annotate / mainJsMs | 84.2 [90.9] | 105.1 [117.5] | 1.248 [1.148, 1.332] | ±11.5% | slower |
| annotateLarge / settleMs | 228.0 [288.0] | 204.6 [233.5] | -23.5 [-59.6, +34.2] | ±43.1 | no detectable difference |
| annotateLarge / mainTaskMs | 112.0 [121.5] | 121.4 [131.3] | 1.084 [1.015, 1.161] | ±9.2% | no detectable difference |
| annotateLarge / mainJsMs | 90.8 [99.6] | 103.5 [113.5] | 1.140 [1.059, 1.233] | ±9.4% | slower |
| hover / settleMs | 1.3 [1.5] | 1.2 [1.3] | -0.1 [-0.2, -0.1] | ±0.0 | faster |
| hover / wallMs | 1055.0 [1118.3] | 1241.3 [1277.3] | 1.177 [1.118, 1.218] | ±8.9% | slower |
| hover / mainTaskMs | 446.2 [462.8] | 813.3 [818.4] | 1.823 [1.799, 1.849] | ±7.0% | slower |
| hover / mainJsMs | 413.5 [433.0] | 785.2 [789.7] | 1.899 [1.864, 1.921] | ±7.4% | slower |
| dragAnnot / settleMs | 424.5 [457.5] | 695.8 [740.2] | +271.3 [+244.6, +359.4] | ±48.7 | slower |
| dragAnnot / mainTaskMs | 227.7 [238.2] | 519.6 [529.1] | 2.282 [2.202, 2.465] | ±8.3% | slower |
| dragAnnot / mainJsMs | 201.8 [210.4] | 495.9 [500.4] | 2.457 [2.364, 2.668] | ±6.0% | slower |
| drawInk / settleMs | 864.1 [872.9] | 914.2 [921.5] | +50.1 [+45.6, +61.4] | ±25.9 | slower |
| drawInk / wallMs | 3061.1 [3260.5] | 5595.9 [5773.5] | 1.828 [1.777, 1.876] | ±3.5% | slower |
| drawInk / mainTaskMs | 1323.5 [1352.7] | 2558.2 [2663.4] | 1.933 [1.904, 1.966] | ±4.2% | slower |
| drawInk / mainJsMs | 1117.8 [1141.5] | 2349.2 [2451.6] | 2.102 [2.072, 2.144] | ±5.1% | slower |
| typeField / settleMs | 153.8 [171.7] | 230.9 [247.0] | +77.1 [+69.6, +92.5] | ±16.4 | slower |
| typeField / mainTaskMs | 119.6 [128.3] | 224.8 [237.1] | 1.881 [1.795, 1.961] | ±12.5% | slower |
| typeField / mainJsMs | 85.2 [93.5] | 200.1 [212.2] | 2.348 [2.233, 2.424] | ±4.3% | slower |
| undo / settleMs | 198.3 [207.8] | 402.3 [410.5] | +204.1 [+200.6, +207.9] | ±5.9 | slower (no A/A band) |
| undo / mainTaskMs | 205.3 [214.0] | 410.4 [416.4] | 1.999 [1.973, 2.019] | ±3.0% | slower (no A/A band) |
| undo / mainJsMs | 132.2 [137.0] | 291.3 [294.7] | 2.203 [2.170, 2.228] | ±3.0% | slower (no A/A band) |

## Memory

| Row | main median [max] | main-aa median [max] | main-now median [max] | perf-fixes-end median [max] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 35.41 MiB [35.42 MiB] | 35.40 MiB [36.42 MiB] | 50.73 MiB [50.77 MiB] | 54.27 MiB [54.77 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 27.72 MiB [27.73 MiB] | 27.70 MiB [27.71 MiB] |
| open(large) / heapAfterOpenBytes | 48.22 MiB [48.25 MiB] | 48.20 MiB [48.21 MiB] | 52.71 MiB [52.73 MiB] | 63.01 MiB [63.03 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.27 MiB [22.29 MiB] | 22.26 MiB [22.26 MiB] | 27.63 MiB [27.64 MiB] | 27.61 MiB [27.62 MiB] |
| soak / heapCycle5Bytes | 38.50 MiB [38.52 MiB] | 38.50 MiB [38.52 MiB] | 57.94 MiB [58.33 MiB] | 61.43 MiB [61.74 MiB] |
| soak / heapCycle30Bytes | 39.33 MiB [39.33 MiB] | 39.32 MiB [39.34 MiB] | 59.38 MiB [59.40 MiB] | 62.18 MiB [62.46 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.06 MiB [0.06 MiB] | 0.03 MiB [0.03 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`perf-fixes-end`:

| Table | Cycle 5 | Cycle 30 |
| --- | --- | --- |
| aborts | 0 | 0 |
| allHosts | 499 | 499 |
| described | 22 | 22 |
| documents | 1 | 1 |
| fns | 413 | 413 |
| heldFns | 413 | 413 |
| hosts | 499 | 499 |
| promises | 0 | 0 |
| proxies | 22 | 22 |
| refs | 1020 | 1520 |
| tokens | 31 | 31 |
| wasmBytes | 5.25 MiB | 5.25 MiB |
| wireRefs | 910 | 910 |

