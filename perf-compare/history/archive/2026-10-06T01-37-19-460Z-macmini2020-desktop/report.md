# Viewer perf comparison: 2026-10-06T01-37-19-460Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 8dad2d808c4d6110ef2c20b6ad3f98bc53497dbb |
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
| plugins-tip4 | research/multi-platform-rust-core-impl-plugins | 7455a5294 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 5eadabd61463 |
| plugins-hostbytes | research/rust-kernel-host-bytes | 81899a80a | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 00de09f8d708 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-tip4`: pass
- `plugins-hostbytes`: pass

## Sizes

| Metric | main | main-aa | plugins-tip4 | plugins-hostbytes |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 8.03 MiB | 8.03 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.72 MiB | 1.72 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

62 of 63 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.1 [67.6] | 61.6 [66.4] | 0.977 [0.949, 1.024] | pass |
| open(ebook) / openedMs | 137.6 [142.3] | 137.4 [140.7] | 0.999 [0.974, 1.014] | pass |
| open(ebook) / firstPaintMs | 258.9 [273.6] | 252.5 [258.9] | 0.976 [0.950, 1.001] | pass |
| open(ebook) / settledMs | 397.9 [403.5] | 395.1 [398.7] | 0.993 [0.976, 1.002] | pass |
| open(ebook) / mainTaskMs | 145.1 [149.6] | 143.9 [146.6] | 0.992 [0.965, 1.021] | pass |
| open(ebook) / mainJsMs | 111.8 [115.6] | 110.9 [112.6] | 0.992 [0.965, 1.020] | pass |
| open(large) / readyMs | 61.5 [64.7] | 60.9 [64.9] | 0.991 [0.955, 1.038] | pass |
| open(large) / openedMs | 157.5 [160.6] | 158.5 [160.9] | 1.006 [0.985, 1.024] | pass |
| open(large) / firstPaintMs | 268.8 [276.6] | 268.5 [278.9] | 0.999 [0.983, 1.030] | pass |
| open(large) / settledMs | 267.4 [281.1] | 273.7 [283.1] | 1.023 [0.995, 1.049] | pass |
| open(large) / mainTaskMs | 317.4 [318.4] | 314.5 [317.9] | 0.991 [0.983, 1.004] | pass |
| open(large) / mainJsMs | 285.8 [287.3] | 283.2 [286.3] | 0.991 [0.983, 1.003] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2876.0 [2910.7] | 2915.8 [2965.5] | 1.014 [0.984, 1.031] | pass |
| scroll / frameP50Ms | 10.9 [11.4] | 11.2 [11.5] | 1.028 [0.967, 1.076] | pass |
| scroll / frameP95Ms | 23.4 [27.2] | 25.2 [27.1] | 1.077 [0.951, 1.203] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 526.9 [550.1] | 541.2 [560.1] | 1.027 [1.002, 1.062] | pass |
| scroll / mainJsMs | 395.9 [413.7] | 400.4 [408.7] | 1.011 [0.990, 1.040] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2822.6 [2886.0] | 2864.0 [2926.0] | 1.015 [0.989, 1.037] | pass |
| coldScroll / frameP50Ms | 11.1 [11.4] | 11.0 [11.1] | 0.989 [0.978, 1.004] | pass |
| coldScroll / frameP95Ms | 25.6 [27.1] | 22.7 [25.4] | 0.887 [0.830, 0.973] | A/A failed |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 553.6 [566.0] | 561.6 [575.0] | 1.015 [0.992, 1.036] | pass |
| coldScroll / mainJsMs | 387.7 [396.3] | 395.3 [400.1] | 1.020 [0.992, 1.030] | pass |
| jump / settleMs | 128.1 [139.2] | 124.7 [134.7] | -3.4 [-10.6, +5.5] | pass |
| jump / mainTaskMs | 74.6 [76.8] | 74.8 [77.5] | 1.003 [0.962, 1.058] | pass |
| jump / mainJsMs | 53.6 [56.5] | 55.8 [58.6] | 1.042 [0.970, 1.114] | pass |
| zoom / settleMs | 497.2 [532.1] | 494.5 [515.9] | -2.7 [-25.4, +13.2] | pass |
| zoom / zoom2SettleMs | 165.2 [174.1] | 168.0 [181.7] | +2.8 [-3.1, +7.7] | pass |
| zoom / zoom4SettleMs | 332.0 [359.4] | 328.1 [335.0] | -3.8 [-21.3, +7.6] | pass |
| zoom / backTo1SettleMs | 93.7 [105.9] | 92.8 [103.3] | -0.8 [-7.4, +6.4] | pass |
| zoom / mainTaskMs | 227.7 [231.0] | 221.7 [232.9] | 0.974 [0.938, 1.002] | pass |
| zoom / mainJsMs | 163.6 [167.5] | 159.5 [169.3] | 0.975 [0.944, 1.007] | pass |
| zoomLarge / settleMs | 318.6 [357.3] | 300.6 [324.7] | -18.0 [-44.3, +4.4] | pass |
| zoomLarge / zoom2SettleMs | 143.6 [164.6] | 138.7 [161.2] | -4.9 [-21.4, +7.3] | pass |
| zoomLarge / zoom4SettleMs | 167.8 [207.1] | 160.5 [172.3] | -7.2 [-27.4, +4.0] | pass |
| zoomLarge / backTo1SettleMs | 75.2 [94.0] | 73.6 [95.6] | -1.6 [-8.5, +12.2] | pass |
| zoomLarge / mainTaskMs | 175.3 [180.6] | 173.0 [182.8] | 0.987 [0.968, 1.014] | pass |
| zoomLarge / mainJsMs | 131.1 [135.1] | 127.9 [135.8] | 0.976 [0.953, 1.014] | pass |
| select / settleMs | 221.4 [236.9] | 216.0 [259.6] | -5.4 [-20.8, +19.5] | pass |
| select / mainTaskMs | 61.3 [64.0] | 61.4 [65.0] | 1.002 [0.954, 1.043] | pass |
| select / mainJsMs | 39.9 [41.8] | 39.5 [42.4] | 0.990 [0.947, 1.047] | pass |
| selectLarge / settleMs | 217.6 [278.2] | 245.1 [252.3] | +27.5 [-11.8, +41.9] | pass |
| selectLarge / mainTaskMs | 70.8 [81.1] | 69.6 [76.9] | 0.983 [0.927, 1.075] | pass |
| selectLarge / mainJsMs | 47.7 [55.0] | 47.1 [52.6] | 0.986 [0.924, 1.081] | pass |
| search / searchMs | 74.8 [80.8] | 73.7 [79.0] | 0.984 [0.906, 1.047] | pass |
| search / mainTaskMs | 5.3 [6.0] | 5.4 [6.0] | 1.011 [0.954, 1.070] | pass |
| search / mainJsMs | 3.7 [4.1] | 3.7 [3.9] | 1.007 [0.961, 1.038] | pass |
| searchLarge / searchMs | 1886.3 [1933.7] | 1905.2 [1958.5] | 1.010 [0.986, 1.023] | pass |
| searchLarge / mainTaskMs | 344.0 [347.3] | 345.8 [348.7] | 1.005 [0.992, 1.011] | pass |
| searchLarge / mainJsMs | 291.1 [293.7] | 291.0 [294.5] | 1.000 [0.992, 1.009] | pass |
| annotate / settleMs | 214.1 [271.7] | 208.8 [260.6] | -5.3 [-41.1, +32.9] | pass |
| annotate / mainTaskMs | 75.5 [82.8] | 77.6 [84.6] | 1.028 [0.962, 1.088] | pass |
| annotate / mainJsMs | 45.5 [49.4] | 46.3 [50.8] | 1.017 [0.972, 1.099] | pass |
| annotateLarge / settleMs | 234.2 [267.1] | 238.9 [263.6] | +4.7 [-36.3, +36.4] | pass |
| annotateLarge / mainTaskMs | 83.7 [87.9] | 85.7 [90.0] | 1.024 [0.948, 1.042] | pass |
| annotateLarge / mainJsMs | 50.3 [52.8] | 51.7 [54.2] | 1.027 [0.944, 1.067] | pass |

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

| Row | plugins-tip4 median [p95] | plugins-hostbytes median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 90.6 [100.6] | 90.6 [97.2] | 1.001 [0.955, 1.021] | no detectable difference |
| open(ebook) / openedMs | 210.1 [219.8] | 178.1 [186.2] | 0.848 [0.831, 0.860] | faster |
| open(ebook) / firstPaintMs | 366.7 [382.8] | 345.3 [361.8] | 0.941 [0.910, 0.967] | faster |
| open(ebook) / settledMs | 431.6 [442.2] | 396.3 [411.8] | 0.918 [0.903, 0.954] | faster |
| open(ebook) / mainTaskMs | 286.5 [292.6] | 254.4 [267.0] | 0.888 [0.873, 0.911] | faster |
| open(ebook) / mainJsMs | 235.3 [240.2] | 206.2 [216.6] | 0.876 [0.861, 0.898] | faster |
| open(large) / readyMs | 91.4 [100.7] | 93.0 [108.8] | 1.018 [0.971, 1.104] | no detectable difference |
| open(large) / openedMs | 240.0 [252.4] | 209.1 [226.9] | 0.871 [0.850, 0.908] | faster |
| open(large) / firstPaintMs | 415.0 [444.2] | 378.4 [412.8] | 0.912 [0.883, 0.969] | faster |
| open(large) / settledMs | 420.5 [438.6] | 385.6 [399.2] | 0.917 [0.891, 0.948] | faster |
| open(large) / mainTaskMs | 575.9 [588.7] | 539.6 [553.5] | 0.937 [0.929, 0.953] | faster |
| open(large) / mainJsMs | 527.8 [542.2] | 494.7 [506.4] | 0.937 [0.927, 0.954] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 3018.5 [3124.7] | 3043.2 [3107.1] | 1.008 [0.975, 1.028] | no detectable difference |
| scroll / frameP50Ms | 11.1 [11.5] | 11.0 [11.9] | 0.990 [0.966, 1.036] | no detectable difference |
| scroll / frameP95Ms | 26.1 [27.0] | 25.7 [27.2] | 0.986 [0.823, 1.057] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 842.5 [857.7] | 840.7 [859.0] | 0.998 [0.980, 1.011] | no detectable difference |
| scroll / mainJsMs | 712.0 [722.7] | 709.0 [722.2] | 0.996 [0.979, 1.013] | no detectable difference |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2966.7 [3063.9] | 2988.4 [3079.3] | 1.007 [0.983, 1.040] | no detectable difference |
| coldScroll / frameP50Ms | 11.1 [11.8] | 11.0 [11.5] | 0.985 [0.944, 1.009] | no detectable difference |
| coldScroll / frameP95Ms | 26.3 [27.2] | 25.5 [27.2] | 0.973 [0.869, 1.029] | unreliable (A/A failed) |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 825.3 [842.6] | 844.5 [855.5] | 1.023 [1.000, 1.035] | no detectable difference |
| coldScroll / mainJsMs | 685.0 [703.0] | 693.6 [703.0] | 1.013 [0.990, 1.027] | no detectable difference |
| jump / settleMs | 143.6 [166.6] | 146.7 [163.7] | +3.2 [-17.1, +10.7] | no detectable difference |
| jump / mainTaskMs | 141.4 [149.6] | 144.1 [146.9] | 1.019 [0.952, 1.048] | no detectable difference |
| jump / mainJsMs | 124.1 [132.3] | 127.1 [129.6] | 1.024 [0.960, 1.052] | no detectable difference |
| zoom / settleMs | 542.6 [562.3] | 544.5 [564.0] | +1.9 [-7.0, +16.9] | no detectable difference |
| zoom / zoom2SettleMs | 191.1 [200.3] | 192.3 [204.8] | +1.2 [-10.9, +13.2] | no detectable difference |
| zoom / zoom4SettleMs | 352.0 [371.3] | 355.2 [364.6] | +3.2 [-9.1, +13.7] | no detectable difference |
| zoom / backTo1SettleMs | 107.7 [113.3] | 108.2 [128.4] | +0.5 [-3.5, +9.3] | no detectable difference |
| zoom / mainTaskMs | 335.5 [343.8] | 348.8 [358.3] | 1.040 [1.015, 1.074] | no detectable difference |
| zoom / mainJsMs | 275.8 [286.4] | 283.9 [291.3] | 1.029 [1.001, 1.055] | no detectable difference |
| zoomLarge / settleMs | 327.0 [346.4] | 326.5 [343.8] | -0.5 [-34.3, +21.6] | no detectable difference |
| zoomLarge / zoom2SettleMs | 147.9 [164.4] | 143.0 [167.9] | -4.9 [-18.2, +3.8] | no detectable difference |
| zoomLarge / zoom4SettleMs | 179.8 [196.4] | 177.4 [198.2] | -2.4 [-20.9, +18.3] | no detectable difference |
| zoomLarge / backTo1SettleMs | 78.1 [95.8] | 87.1 [100.4] | +9.0 [-2.8, +15.4] | no detectable difference |
| zoomLarge / mainTaskMs | 247.0 [260.4] | 259.4 [269.6] | 1.050 [1.018, 1.079] | slower |
| zoomLarge / mainJsMs | 206.1 [217.9] | 219.1 [226.3] | 1.063 [1.024, 1.088] | slower |
| select / settleMs | 227.7 [266.9] | 225.0 [246.5] | -2.7 [-23.6, +25.1] | no detectable difference |
| select / mainTaskMs | 81.1 [89.6] | 82.1 [86.4] | 1.012 [0.950, 1.078] | no detectable difference |
| select / mainJsMs | 63.9 [71.2] | 65.3 [68.8] | 1.021 [0.938, 1.076] | no detectable difference |
| selectLarge / settleMs | 225.0 [248.6] | 226.2 [248.0] | +1.2 [-11.2, +19.3] | no detectable difference |
| selectLarge / mainTaskMs | 109.5 [112.6] | 108.7 [114.8] | 0.993 [0.957, 1.050] | no detectable difference |
| selectLarge / mainJsMs | 85.6 [88.6] | 83.8 [90.5] | 0.979 [0.952, 1.046] | no detectable difference |
| search / searchMs | 76.8 [84.3] | 73.8 [81.6] | 0.960 [0.880, 1.065] | no detectable difference |
| search / mainTaskMs | 11.4 [21.4] | 9.8 [19.0] | 0.860 [0.473, 1.497] | no detectable difference |
| search / mainJsMs | 7.5 [8.6] | 6.7 [10.2] | 0.898 [0.783, 1.115] | no detectable difference |
| searchLarge / searchMs | 1820.0 [1845.3] | 1819.9 [1846.9] | 1.000 [0.994, 1.004] | no detectable difference |
| searchLarge / mainTaskMs | 835.0 [839.2] | 840.7 [848.2] | 1.007 [1.002, 1.012] | no detectable difference |
| searchLarge / mainJsMs | 798.6 [803.0] | 798.3 [803.7] | 1.000 [0.997, 1.005] | no detectable difference |
| annotate / settleMs | 225.4 [259.9] | 217.7 [237.6] | -7.8 [-32.9, +8.4] | no detectable difference |
| annotate / mainTaskMs | 102.8 [107.6] | 102.6 [110.7] | 0.998 [0.961, 1.049] | no detectable difference |
| annotate / mainJsMs | 75.6 [79.8] | 75.2 [81.7] | 0.995 [0.952, 1.049] | no detectable difference |
| annotateLarge / settleMs | 249.3 [275.6] | 204.8 [247.8] | -44.5 [-72.5, -8.5] | faster |
| annotateLarge / mainTaskMs | 127.9 [133.3] | 123.6 [128.2] | 0.967 [0.933, 0.994] | no detectable difference |
| annotateLarge / mainJsMs | 85.3 [90.3] | 82.9 [87.4] | 0.973 [0.924, 1.004] | no detectable difference |

## `main` vs `plugins-hostbytes`

| Row | main median [p95] | plugins-hostbytes median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.1 [67.6] | 90.6 [97.2] | 1.437 [1.407, 1.500] | slower |
| open(ebook) / openedMs | 137.6 [142.3] | 178.1 [186.2] | 1.295 [1.267, 1.317] | slower |
| open(ebook) / firstPaintMs | 258.9 [273.6] | 345.3 [361.8] | 1.334 [1.286, 1.368] | slower |
| open(ebook) / settledMs | 397.9 [403.5] | 396.3 [411.8] | 0.996 [0.983, 1.030] | no detectable difference |
| open(ebook) / mainTaskMs | 145.1 [149.6] | 254.4 [267.0] | 1.754 [1.722, 1.816] | slower |
| open(ebook) / mainJsMs | 111.8 [115.6] | 206.2 [216.6] | 1.844 [1.805, 1.904] | slower |
| open(large) / readyMs | 61.5 [64.7] | 93.0 [108.8] | 1.513 [1.444, 1.640] | slower |
| open(large) / openedMs | 157.5 [160.6] | 209.1 [226.9] | 1.327 [1.296, 1.380] | slower |
| open(large) / firstPaintMs | 268.8 [276.6] | 378.4 [412.8] | 1.407 [1.378, 1.494] | slower |
| open(large) / settledMs | 267.4 [281.1] | 385.6 [399.2] | 1.442 [1.409, 1.494] | slower |
| open(large) / mainTaskMs | 317.4 [318.4] | 539.6 [553.5] | 1.700 [1.689, 1.733] | slower |
| open(large) / mainJsMs | 285.8 [287.3] | 494.7 [506.4] | 1.731 [1.715, 1.761] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2876.0 [2910.7] | 3043.2 [3107.1] | 1.058 [1.030, 1.080] | slower |
| scroll / frameP50Ms | 10.9 [11.4] | 11.0 [11.9] | 1.010 [0.967, 1.071] | no detectable difference |
| scroll / frameP95Ms | 23.4 [27.2] | 25.7 [27.2] | 1.096 [0.885, 1.223] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 526.9 [550.1] | 840.7 [859.0] | 1.595 [1.554, 1.629] | slower |
| scroll / mainJsMs | 395.9 [413.7] | 709.0 [722.2] | 1.791 [1.751, 1.833] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2822.6 [2886.0] | 2988.4 [3079.3] | 1.059 [1.043, 1.088] | slower |
| coldScroll / frameP50Ms | 11.1 [11.4] | 11.0 [11.5] | 0.987 [0.973, 1.012] | no detectable difference |
| coldScroll / frameP95Ms | 25.6 [27.1] | 25.5 [27.2] | 0.997 [0.887, 1.098] | unreliable (A/A failed) |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 553.6 [566.0] | 844.5 [855.5] | 1.526 [1.495, 1.550] | slower |
| coldScroll / mainJsMs | 387.7 [396.3] | 693.6 [703.0] | 1.789 [1.768, 1.813] | slower |
| jump / settleMs | 128.1 [139.2] | 146.7 [163.7] | +18.6 [+8.6, +29.6] | slower |
| jump / mainTaskMs | 74.6 [76.8] | 144.1 [146.9] | 1.931 [1.853, 2.028] | slower |
| jump / mainJsMs | 53.6 [56.5] | 127.1 [129.6] | 2.372 [2.251, 2.497] | slower |
| zoom / settleMs | 497.2 [532.1] | 544.5 [564.0] | +47.3 [+37.1, +63.8] | slower |
| zoom / zoom2SettleMs | 165.2 [174.1] | 192.3 [204.8] | +27.1 [+15.4, +35.6] | slower |
| zoom / zoom4SettleMs | 332.0 [359.4] | 355.2 [364.6] | +23.2 [+15.0, +34.7] | slower |
| zoom / backTo1SettleMs | 93.7 [105.9] | 108.2 [128.4] | +14.6 [+10.6, +23.3] | slower |
| zoom / mainTaskMs | 227.7 [231.0] | 348.8 [358.3] | 1.532 [1.497, 1.573] | slower |
| zoom / mainJsMs | 163.6 [167.5] | 283.9 [291.3] | 1.735 [1.697, 1.782] | slower |
| zoomLarge / settleMs | 318.6 [357.3] | 326.5 [343.8] | +7.9 [-32.8, +31.9] | no detectable difference |
| zoomLarge / zoom2SettleMs | 143.6 [164.6] | 143.0 [167.9] | -0.6 [-17.8, +6.3] | no detectable difference |
| zoomLarge / zoom4SettleMs | 167.8 [207.1] | 177.4 [198.2] | +9.6 [-15.9, +28.5] | no detectable difference |
| zoomLarge / backTo1SettleMs | 75.2 [94.0] | 87.1 [100.4] | +11.9 [+5.0, +18.8] | no detectable difference |
| zoomLarge / mainTaskMs | 175.3 [180.6] | 259.4 [269.6] | 1.480 [1.457, 1.529] | slower |
| zoomLarge / mainJsMs | 131.1 [135.1] | 219.1 [226.3] | 1.671 [1.634, 1.725] | slower |
| select / settleMs | 221.4 [236.9] | 225.0 [246.5] | +3.6 [-16.7, +23.6] | no detectable difference |
| select / mainTaskMs | 61.3 [64.0] | 82.1 [86.4] | 1.339 [1.270, 1.414] | slower |
| select / mainJsMs | 39.9 [41.8] | 65.3 [68.8] | 1.636 [1.527, 1.750] | slower |
| selectLarge / settleMs | 217.6 [278.2] | 226.2 [248.0] | +8.6 [-22.1, +29.1] | no detectable difference |
| selectLarge / mainTaskMs | 70.8 [81.1] | 108.7 [114.8] | 1.536 [1.463, 1.636] | slower |
| selectLarge / mainJsMs | 47.7 [55.0] | 83.8 [90.5] | 1.756 [1.682, 1.882] | slower |
| search / searchMs | 74.8 [80.8] | 73.8 [81.6] | 0.986 [0.922, 1.069] | no detectable difference |
| search / mainTaskMs | 5.3 [6.0] | 9.8 [19.0] | 1.838 [1.716, 2.776] | slower |
| search / mainJsMs | 3.7 [4.1] | 6.7 [10.2] | 1.817 [1.763, 2.115] | slower |
| searchLarge / searchMs | 1886.3 [1933.7] | 1819.9 [1846.9] | 0.965 [0.945, 0.969] | faster |
| searchLarge / mainTaskMs | 344.0 [347.3] | 840.7 [848.2] | 2.444 [2.429, 2.455] | slower |
| searchLarge / mainJsMs | 291.1 [293.7] | 798.3 [803.7] | 2.742 [2.729, 2.754] | slower |
| annotate / settleMs | 214.1 [271.7] | 217.7 [237.6] | +3.5 [-34.0, +35.6] | no detectable difference |
| annotate / mainTaskMs | 75.5 [82.8] | 102.6 [110.7] | 1.359 [1.304, 1.415] | slower |
| annotate / mainJsMs | 45.5 [49.4] | 75.2 [81.7] | 1.653 [1.611, 1.738] | slower |
| annotateLarge / settleMs | 234.2 [267.1] | 204.8 [247.8] | -29.4 [-53.3, +2.8] | no detectable difference |
| annotateLarge / mainTaskMs | 83.7 [87.9] | 123.6 [128.2] | 1.477 [1.424, 1.513] | slower |
| annotateLarge / mainJsMs | 50.3 [52.8] | 82.9 [87.4] | 1.649 [1.579, 1.720] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-tip4 median [p95] | plugins-hostbytes median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 35.35 MiB [35.36 MiB] | 35.34 MiB [35.35 MiB] | 42.36 MiB [42.92 MiB] | 36.85 MiB [37.41 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.16 MiB [48.18 MiB] | 48.15 MiB [48.17 MiB] | 54.96 MiB [55.09 MiB] | 51.51 MiB [51.63 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.29 MiB] | 22.26 MiB [22.29 MiB] | 22.96 MiB [23.03 MiB] | 22.96 MiB [22.96 MiB] |
| soak / heapCycle5Bytes | 38.42 MiB [38.43 MiB] | 37.92 MiB [38.43 MiB] | 45.16 MiB [46.18 MiB] | 40.74 MiB [41.30 MiB] |
| soak / heapCycle30Bytes | 39.26 MiB [39.28 MiB] | 38.76 MiB [39.27 MiB] | 45.70 MiB [46.71 MiB] | 41.27 MiB [41.84 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-tip4`:

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
| refs | 658 | 658 |
| wasmBytes | 9.00 MiB | 9.00 MiB |
| wireRefs | 658 | 658 |

`plugins-hostbytes`:

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

