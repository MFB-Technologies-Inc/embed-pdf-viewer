# Viewer perf comparison: 2026-10-08T20-19-04-702Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | c2d75b4c763b7cf1e59622bc45157e177836af1d |
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
| plugins-tip6 | research/multi-platform-rust-core-impl-plugins | faa2c6189 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 5bf5fb28d0f2 |
| plugins-plainargs | research/rust-kernel-plain-args | 45c920b26 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 5bf5fb28d0f2 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-tip6`: pass
- `plugins-plainargs`: pass

## Sizes

| Metric | main | main-aa | plugins-tip6 | plugins-plainargs |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 8.05 MiB | 8.05 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.73 MiB | 1.73 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

61 of 63 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 61.5 [64.2] | 63.6 [65.2] | 1.035 [0.978, 1.056] | pass |
| open(ebook) / openedMs | 135.3 [139.5] | 137.9 [139.0] | 1.019 [1.002, 1.026] | pass |
| open(ebook) / firstPaintMs | 254.4 [273.0] | 255.8 [273.4] | 1.006 [0.965, 1.037] | pass |
| open(ebook) / settledMs | 390.5 [400.1] | 394.4 [397.2] | 1.010 [0.998, 1.016] | pass |
| open(ebook) / mainTaskMs | 143.0 [149.8] | 143.7 [149.1] | 1.005 [0.971, 1.032] | pass |
| open(ebook) / mainJsMs | 110.2 [114.9] | 110.3 [112.5] | 1.001 [0.972, 1.028] | pass |
| open(large) / readyMs | 64.4 [68.1] | 60.8 [64.4] | 0.944 [0.932, 0.997] | A/A failed |
| open(large) / openedMs | 159.1 [163.3] | 157.7 [162.3] | 0.991 [0.971, 1.009] | pass |
| open(large) / firstPaintMs | 265.6 [279.0] | 263.4 [277.2] | 0.992 [0.954, 1.029] | pass |
| open(large) / settledMs | 269.2 [272.5] | 268.8 [278.5] | 0.998 [0.977, 1.027] | pass |
| open(large) / mainTaskMs | 313.5 [317.7] | 313.4 [315.6] | 1.000 [0.991, 1.004] | pass |
| open(large) / mainJsMs | 283.1 [287.7] | 282.7 [285.0] | 0.999 [0.990, 1.004] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2885.3 [3079.5] | 2878.7 [2966.3] | 0.998 [0.971, 1.024] | pass |
| scroll / frameP50Ms | 11.1 [14.6] | 11.0 [11.9] | 0.994 [0.975, 1.032] | pass |
| scroll / frameP95Ms | 24.4 [27.1] | 25.1 [27.0] | 1.027 [0.888, 1.183] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 547.9 [572.8] | 542.1 [575.6] | 0.990 [0.960, 1.034] | pass |
| scroll / mainJsMs | 412.5 [433.4] | 406.3 [434.3] | 0.985 [0.959, 1.021] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2820.5 [2888.0] | 2868.0 [2976.5] | 1.017 [0.990, 1.046] | pass |
| coldScroll / frameP50Ms | 11.0 [11.6] | 11.2 [11.8] | 1.022 [0.987, 1.074] | pass |
| coldScroll / frameP95Ms | 24.4 [26.2] | 24.9 [26.9] | 1.020 [0.950, 1.206] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 562.5 [585.8] | 569.6 [588.4] | 1.013 [0.985, 1.032] | pass |
| coldScroll / mainJsMs | 395.5 [410.0] | 400.2 [411.4] | 1.012 [0.980, 1.031] | pass |
| jump / settleMs | 124.9 [138.7] | 122.9 [127.1] | -2.0 [-9.7, +4.7] | pass |
| jump / mainTaskMs | 72.5 [81.1] | 73.0 [77.3] | 1.007 [0.918, 1.059] | pass |
| jump / mainJsMs | 53.8 [61.1] | 54.9 [58.6] | 1.021 [0.917, 1.071] | pass |
| zoom / settleMs | 444.7 [469.5] | 443.4 [474.0] | -1.3 [-19.3, +25.8] | pass |
| zoom / zoom2SettleMs | 163.5 [170.3] | 161.9 [166.5] | -1.6 [-9.8, +5.9] | pass |
| zoom / zoom4SettleMs | 279.6 [301.2] | 282.4 [312.1] | +2.8 [-12.4, +20.8] | pass |
| zoom / backTo1SettleMs | 79.4 [101.9] | 85.2 [105.3] | +5.8 [-8.0, +13.2] | pass |
| zoom / mainTaskMs | 211.8 [224.6] | 218.7 [232.0] | 1.033 [0.993, 1.068] | pass |
| zoom / mainJsMs | 151.6 [160.5] | 160.2 [165.8] | 1.057 [1.023, 1.079] | A/A failed |
| zoomLarge / settleMs | 304.1 [340.6] | 292.4 [304.5] | -11.7 [-30.7, +2.5] | pass |
| zoomLarge / zoom2SettleMs | 143.0 [159.6] | 137.0 [154.4] | -6.0 [-20.2, +7.5] | pass |
| zoomLarge / zoom4SettleMs | 163.4 [193.0] | 152.8 [173.6] | -10.6 [-24.8, +0.9] | pass |
| zoomLarge / backTo1SettleMs | 78.4 [98.8] | 76.6 [87.6] | -1.8 [-10.0, +6.0] | pass |
| zoomLarge / mainTaskMs | 176.0 [182.5] | 175.5 [185.0] | 0.997 [0.967, 1.030] | pass |
| zoomLarge / mainJsMs | 130.4 [135.2] | 131.2 [137.4] | 1.006 [0.973, 1.038] | pass |
| select / settleMs | 234.1 [272.5] | 218.5 [290.2] | -15.6 [-56.1, +16.6] | pass |
| select / mainTaskMs | 64.7 [70.6] | 60.8 [65.8] | 0.940 [0.884, 1.017] | pass |
| select / mainJsMs | 41.9 [46.6] | 39.2 [43.7] | 0.933 [0.874, 1.034] | pass |
| selectLarge / settleMs | 212.6 [265.4] | 216.3 [245.8] | +3.6 [-32.9, +31.0] | pass |
| selectLarge / mainTaskMs | 72.7 [78.2] | 70.2 [74.7] | 0.966 [0.898, 1.034] | pass |
| selectLarge / mainJsMs | 49.0 [53.9] | 47.4 [51.0] | 0.969 [0.872, 1.041] | pass |
| search / searchMs | 73.7 [79.4] | 71.9 [82.0] | 0.977 [0.879, 1.101] | pass |
| search / mainTaskMs | 5.7 [6.3] | 5.3 [6.5] | 0.934 [0.877, 1.068] | pass |
| search / mainJsMs | 3.7 [3.9] | 3.7 [4.2] | 0.999 [0.956, 1.051] | pass |
| searchLarge / searchMs | 1911.6 [1935.7] | 1895.8 [2070.3] | 0.992 [0.975, 1.035] | pass |
| searchLarge / mainTaskMs | 343.3 [346.2] | 342.6 [349.5] | 0.998 [0.987, 1.011] | pass |
| searchLarge / mainJsMs | 289.7 [292.2] | 290.9 [293.1] | 1.004 [0.994, 1.008] | pass |
| annotate / settleMs | 216.4 [266.2] | 202.7 [246.6] | -13.7 [-52.6, +38.6] | pass |
| annotate / mainTaskMs | 78.2 [82.7] | 77.1 [83.9] | 0.985 [0.932, 1.046] | pass |
| annotate / mainJsMs | 47.2 [50.7] | 46.4 [51.5] | 0.983 [0.921, 1.064] | pass |
| annotateLarge / settleMs | 229.0 [254.4] | 239.0 [265.7] | +9.9 [-23.4, +27.2] | pass |
| annotateLarge / mainTaskMs | 83.4 [86.6] | 83.9 [90.2] | 1.005 [0.983, 1.049] | pass |
| annotateLarge / mainJsMs | 50.2 [52.7] | 50.9 [54.8] | 1.015 [0.979, 1.057] | pass |

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

Not run: `plugins-coalesce`.

## `main` vs `plugins-tip6`

| Row | main median [p95] | plugins-tip6 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 61.5 [64.2] | 94.7 [108.1] | 1.540 [1.468, 1.605] | slower |
| open(ebook) / openedMs | 135.3 [139.5] | 181.5 [194.6] | 1.342 [1.318, 1.390] | slower |
| open(ebook) / firstPaintMs | 254.4 [273.0] | 336.5 [349.1] | 1.323 [1.267, 1.372] | slower |
| open(ebook) / settledMs | 390.5 [400.1] | 405.9 [416.7] | 1.039 [1.024, 1.058] | slower |
| open(ebook) / mainTaskMs | 143.0 [149.8] | 245.2 [251.2] | 1.715 [1.671, 1.757] | slower |
| open(ebook) / mainJsMs | 110.2 [114.9] | 195.1 [200.3] | 1.770 [1.728, 1.824] | slower |
| open(large) / readyMs | 64.4 [68.1] | 92.5 [110.3] | 1.437 [1.400, 1.515] | unreliable (A/A failed) |
| open(large) / openedMs | 159.1 [163.3] | 209.9 [226.5] | 1.320 [1.281, 1.341] | slower |
| open(large) / firstPaintMs | 265.6 [279.0] | 368.6 [378.9] | 1.388 [1.339, 1.433] | slower |
| open(large) / settledMs | 269.2 [272.5] | 358.5 [387.2] | 1.332 [1.308, 1.379] | slower |
| open(large) / mainTaskMs | 313.5 [317.7] | 296.3 [305.7] | 0.945 [0.934, 0.960] | faster |
| open(large) / mainJsMs | 283.1 [287.7] | 249.6 [273.3] | 0.882 [0.872, 0.896] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [2.6] | +0.0 [+0.0, +1.3] | no detectable difference |
| scroll / wallMs | 2885.3 [3079.5] | 2532.4 [2666.4] | 0.878 [0.851, 0.917] | faster |
| scroll / frameP50Ms | 11.1 [14.6] | 11.0 [11.4] | 0.989 [0.964, 1.004] | no detectable difference |
| scroll / frameP95Ms | 24.4 [27.1] | 24.7 [27.2] | 1.011 [0.888, 1.189] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 547.9 [572.8] | 757.5 [782.8] | 1.383 [1.342, 1.413] | slower |
| scroll / mainJsMs | 412.5 [433.4] | 616.9 [639.5] | 1.496 [1.451, 1.540] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [1.5] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2820.5 [2888.0] | 2464.8 [2661.5] | 0.874 [0.838, 0.935] | faster |
| coldScroll / frameP50Ms | 11.0 [11.6] | 11.0 [11.7] | 0.999 [0.970, 1.061] | no detectable difference |
| coldScroll / frameP95Ms | 24.4 [26.2] | 26.4 [27.2] | 1.080 [0.976, 1.259] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 562.5 [585.8] | 757.0 [787.0] | 1.346 [1.318, 1.379] | slower |
| coldScroll / mainJsMs | 395.5 [410.0] | 604.2 [632.7] | 1.528 [1.490, 1.588] | slower |
| jump / settleMs | 124.9 [138.7] | 127.3 [135.8] | +2.4 [-5.0, +11.7] | no detectable difference |
| jump / mainTaskMs | 72.5 [81.1] | 122.1 [127.6] | 1.684 [1.550, 1.757] | slower |
| jump / mainJsMs | 53.8 [61.1] | 104.9 [108.3] | 1.950 [1.779, 2.047] | slower |
| zoom / settleMs | 444.7 [469.5] | 508.1 [543.9] | +63.3 [+49.4, +94.0] | slower |
| zoom / zoom2SettleMs | 163.5 [170.3] | 192.7 [216.2] | +29.2 [+15.9, +49.4] | slower |
| zoom / zoom4SettleMs | 279.6 [301.2] | 319.1 [328.8] | +39.4 [+27.2, +52.9] | slower |
| zoom / backTo1SettleMs | 79.4 [101.9] | 95.6 [106.6] | +16.2 [+3.1, +21.5] | slower |
| zoom / mainTaskMs | 211.8 [224.6] | 323.0 [338.9] | 1.525 [1.472, 1.576] | slower |
| zoom / mainJsMs | 151.6 [160.5] | 266.5 [273.7] | 1.757 [1.706, 1.787] | unreliable (A/A failed) |
| zoomLarge / settleMs | 304.1 [340.6] | 321.9 [351.5] | +17.8 [-8.6, +32.8] | no detectable difference |
| zoomLarge / zoom2SettleMs | 143.0 [159.6] | 143.0 [173.2] | +0.1 [-12.3, +21.2] | no detectable difference |
| zoomLarge / zoom4SettleMs | 163.4 [193.0] | 170.9 [191.8] | +7.5 [-7.9, +21.3] | no detectable difference |
| zoomLarge / backTo1SettleMs | 78.4 [98.8] | 80.9 [86.7] | +2.5 [-6.4, +8.3] | no detectable difference |
| zoomLarge / mainTaskMs | 176.0 [182.5] | 258.5 [265.0] | 1.469 [1.424, 1.503] | slower |
| zoomLarge / mainJsMs | 130.4 [135.2] | 215.2 [222.6] | 1.650 [1.600, 1.705] | slower |
| select / settleMs | 234.1 [272.5] | 263.6 [299.3] | +29.5 [+5.8, +62.5] | no detectable difference |
| select / mainTaskMs | 64.7 [70.6] | 94.3 [101.1] | 1.459 [1.383, 1.547] | slower |
| select / mainJsMs | 41.9 [46.6] | 74.3 [80.5] | 1.770 [1.669, 1.901] | slower |
| selectLarge / settleMs | 212.6 [265.4] | 272.7 [295.0] | +60.1 [+34.4, +82.3] | slower |
| selectLarge / mainTaskMs | 72.7 [78.2] | 112.9 [118.4] | 1.554 [1.479, 1.648] | slower |
| selectLarge / mainJsMs | 49.0 [53.9] | 83.5 [91.5] | 1.704 [1.617, 1.852] | slower |
| search / searchMs | 73.7 [79.4] | 77.2 [84.6] | 1.047 [0.961, 1.141] | no detectable difference |
| search / mainTaskMs | 5.7 [6.3] | 9.8 [18.8] | 1.734 [1.627, 2.733] | slower |
| search / mainJsMs | 3.7 [3.9] | 7.0 [9.0] | 1.890 [1.779, 2.318] | slower |
| searchLarge / searchMs | 1911.6 [1935.7] | 1838.6 [1848.5] | 0.962 [0.950, 0.979] | faster |
| searchLarge / mainTaskMs | 343.3 [346.2] | 870.5 [878.7] | 2.536 [2.515, 2.560] | slower |
| searchLarge / mainJsMs | 289.7 [292.2] | 822.1 [832.1] | 2.838 [2.821, 2.859] | slower |
| annotate / settleMs | 216.4 [266.2] | 202.2 [269.2] | -14.2 [-53.5, +44.9] | no detectable difference |
| annotate / mainTaskMs | 78.2 [82.7] | 83.0 [100.4] | 1.061 [1.012, 1.237] | no detectable difference |
| annotate / mainJsMs | 47.2 [50.7] | 58.1 [72.6] | 1.230 [1.153, 1.490] | slower |
| annotateLarge / settleMs | 229.0 [254.4] | 259.7 [304.0] | +30.7 [-8.6, +63.0] | no detectable difference |
| annotateLarge / mainTaskMs | 83.4 [86.6] | 122.2 [129.3] | 1.464 [1.321, 1.513] | slower |
| annotateLarge / mainJsMs | 50.2 [52.7] | 77.6 [84.5] | 1.546 [1.375, 1.674] | slower |

## `plugins-tip6` vs `plugins-plainargs`

| Row | plugins-tip6 median [p95] | plugins-plainargs median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 94.7 [108.1] | 90.7 [99.7] | 0.959 [0.919, 1.006] | no detectable difference |
| open(ebook) / openedMs | 181.5 [194.6] | 178.6 [184.9] | 0.984 [0.949, 0.994] | no detectable difference |
| open(ebook) / firstPaintMs | 336.5 [349.1] | 330.1 [349.9] | 0.981 [0.954, 1.007] | no detectable difference |
| open(ebook) / settledMs | 405.9 [416.7] | 394.5 [399.9] | 0.972 [0.955, 0.986] | no detectable difference |
| open(ebook) / mainTaskMs | 245.2 [251.2] | 235.9 [244.4] | 0.962 [0.946, 0.987] | faster |
| open(ebook) / mainJsMs | 195.1 [200.3] | 190.0 [196.0] | 0.974 [0.947, 0.996] | no detectable difference |
| open(large) / readyMs | 92.5 [110.3] | 91.4 [100.4] | 0.988 [0.954, 1.054] | unreliable (A/A failed) |
| open(large) / openedMs | 209.9 [226.5] | 209.6 [216.3] | 0.998 [0.971, 1.035] | no detectable difference |
| open(large) / firstPaintMs | 368.6 [378.9] | 376.7 [390.1] | 1.022 [0.989, 1.056] | no detectable difference |
| open(large) / settledMs | 358.5 [387.2] | 359.5 [367.9] | 1.003 [0.977, 1.025] | no detectable difference |
| open(large) / mainTaskMs | 296.3 [305.7] | 295.6 [298.0] | 0.998 [0.981, 1.007] | no detectable difference |
| open(large) / mainJsMs | 249.6 [273.3] | 248.6 [250.9] | 0.996 [0.977, 1.004] | no detectable difference |
| scroll / settleMs | 0.0 [2.6] | 0.0 [2.4] | +0.0 [-1.3, +1.2] | no detectable difference |
| scroll / wallMs | 2532.4 [2666.4] | 2599.9 [2788.3] | 1.027 [0.975, 1.064] | no detectable difference |
| scroll / frameP50Ms | 11.0 [11.4] | 11.1 [11.7] | 1.014 [0.997, 1.048] | no detectable difference |
| scroll / frameP95Ms | 24.7 [27.2] | 26.4 [27.2] | 1.070 [0.975, 1.163] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 757.5 [782.8] | 733.2 [781.6] | 0.968 [0.943, 1.004] | no detectable difference |
| scroll / mainJsMs | 616.9 [639.5] | 595.4 [630.5] | 0.965 [0.929, 1.002] | no detectable difference |
| coldScroll / settleMs | 0.0 [1.5] | 0.0 [6.6] | +0.0 [+0.0, +2.7] | no detectable difference |
| coldScroll / wallMs | 2464.8 [2661.5] | 2518.5 [2641.8] | 1.022 [0.948, 1.073] | no detectable difference |
| coldScroll / frameP50Ms | 11.0 [11.7] | 11.2 [11.7] | 1.022 [0.979, 1.047] | no detectable difference |
| coldScroll / frameP95Ms | 26.4 [27.2] | 26.0 [27.2] | 0.986 [0.897, 1.092] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 757.0 [787.0] | 730.5 [750.8] | 0.965 [0.937, 0.988] | faster |
| coldScroll / mainJsMs | 604.2 [632.7] | 577.9 [596.0] | 0.956 [0.920, 0.983] | faster |
| jump / settleMs | 127.3 [135.8] | 125.6 [129.1] | -1.7 [-9.4, +4.0] | no detectable difference |
| jump / mainTaskMs | 122.1 [127.6] | 117.6 [122.6] | 0.963 [0.942, 0.976] | no detectable difference |
| jump / mainJsMs | 104.9 [108.3] | 101.7 [105.1] | 0.970 [0.938, 0.987] | no detectable difference |
| zoom / settleMs | 508.1 [543.9] | 519.3 [533.2] | +11.3 [-20.4, +20.6] | no detectable difference |
| zoom / zoom2SettleMs | 192.7 [216.2] | 196.7 [208.2] | +4.0 [-15.9, +16.9] | no detectable difference |
| zoom / zoom4SettleMs | 319.1 [328.8] | 320.4 [332.8] | +1.4 [-8.8, +11.0] | no detectable difference |
| zoom / backTo1SettleMs | 95.6 [106.6] | 99.6 [117.7] | +3.9 [-3.7, +12.5] | no detectable difference |
| zoom / mainTaskMs | 323.0 [338.9] | 311.0 [330.5] | 0.963 [0.932, 0.994] | no detectable difference |
| zoom / mainJsMs | 266.5 [273.7] | 255.4 [270.4] | 0.958 [0.935, 0.985] | unreliable (A/A failed) |
| zoomLarge / settleMs | 321.9 [351.5] | 315.7 [351.2] | -6.2 [-18.5, +15.8] | no detectable difference |
| zoomLarge / zoom2SettleMs | 143.0 [173.2] | 141.0 [169.5] | -2.0 [-20.8, +5.2] | no detectable difference |
| zoomLarge / zoom4SettleMs | 170.9 [191.8] | 177.9 [185.5] | +7.0 [-6.4, +18.3] | no detectable difference |
| zoomLarge / backTo1SettleMs | 80.9 [86.7] | 84.6 [101.3] | +3.7 [-2.2, +5.8] | no detectable difference |
| zoomLarge / mainTaskMs | 258.5 [265.0] | 258.3 [265.5] | 0.999 [0.963, 1.026] | no detectable difference |
| zoomLarge / mainJsMs | 215.2 [222.6] | 216.2 [221.1] | 1.005 [0.956, 1.031] | no detectable difference |
| select / settleMs | 263.6 [299.3] | 235.4 [285.5] | -28.2 [-56.5, +1.4] | no detectable difference |
| select / mainTaskMs | 94.3 [101.1] | 85.4 [94.4] | 0.906 [0.859, 0.953] | no detectable difference |
| select / mainJsMs | 74.3 [80.5] | 66.8 [74.7] | 0.899 [0.842, 0.947] | no detectable difference |
| selectLarge / settleMs | 272.7 [295.0] | 263.6 [282.4] | -9.1 [-24.7, +7.8] | no detectable difference |
| selectLarge / mainTaskMs | 112.9 [118.4] | 109.4 [117.7] | 0.969 [0.945, 1.010] | no detectable difference |
| selectLarge / mainJsMs | 83.5 [91.5] | 79.5 [86.3] | 0.953 [0.901, 0.990] | no detectable difference |
| search / searchMs | 77.2 [84.6] | 76.9 [83.6] | 0.997 [0.919, 1.105] | no detectable difference |
| search / mainTaskMs | 9.8 [18.8] | 11.2 [18.3] | 1.134 [0.693, 1.465] | no detectable difference |
| search / mainJsMs | 7.0 [9.0] | 7.0 [8.3] | 1.004 [0.790, 1.204] | no detectable difference |
| searchLarge / searchMs | 1838.6 [1848.5] | 1837.9 [1841.8] | 1.000 [0.997, 1.002] | no detectable difference |
| searchLarge / mainTaskMs | 870.5 [878.7] | 861.2 [870.3] | 0.989 [0.984, 0.995] | no detectable difference |
| searchLarge / mainJsMs | 822.1 [832.1] | 811.6 [822.6] | 0.987 [0.981, 0.992] | no detectable difference |
| annotate / settleMs | 202.2 [269.2] | 219.7 [264.8] | +17.5 [-39.9, +47.0] | no detectable difference |
| annotate / mainTaskMs | 83.0 [100.4] | 85.3 [97.4] | 1.027 [0.872, 1.130] | no detectable difference |
| annotate / mainJsMs | 58.1 [72.6] | 59.3 [69.4] | 1.022 [0.829, 1.171] | no detectable difference |
| annotateLarge / settleMs | 259.7 [304.0] | 259.0 [300.8] | -0.7 [-43.0, +39.7] | no detectable difference |
| annotateLarge / mainTaskMs | 122.2 [129.3] | 120.2 [130.3] | 0.984 [0.914, 1.090] | no detectable difference |
| annotateLarge / mainJsMs | 77.6 [84.5] | 75.3 [83.7] | 0.971 [0.855, 1.096] | no detectable difference |

## `main` vs `plugins-plainargs`

| Row | main median [p95] | plugins-plainargs median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 61.5 [64.2] | 90.7 [99.7] | 1.476 [1.437, 1.515] | slower |
| open(ebook) / openedMs | 135.3 [139.5] | 178.6 [184.9] | 1.320 [1.292, 1.332] | slower |
| open(ebook) / firstPaintMs | 254.4 [273.0] | 330.1 [349.9] | 1.298 [1.245, 1.338] | slower |
| open(ebook) / settledMs | 390.5 [400.1] | 394.5 [399.9] | 1.010 [0.998, 1.021] | no detectable difference |
| open(ebook) / mainTaskMs | 143.0 [149.8] | 235.9 [244.4] | 1.649 [1.614, 1.693] | slower |
| open(ebook) / mainJsMs | 110.2 [114.9] | 190.0 [196.0] | 1.724 [1.684, 1.766] | slower |
| open(large) / readyMs | 64.4 [68.1] | 91.4 [100.4] | 1.419 [1.395, 1.521] | unreliable (A/A failed) |
| open(large) / openedMs | 159.1 [163.3] | 209.6 [216.3] | 1.318 [1.279, 1.353] | slower |
| open(large) / firstPaintMs | 265.6 [279.0] | 376.7 [390.1] | 1.418 [1.368, 1.469] | slower |
| open(large) / settledMs | 269.2 [272.5] | 359.5 [367.9] | 1.335 [1.310, 1.378] | slower |
| open(large) / mainTaskMs | 313.5 [317.7] | 295.6 [298.0] | 0.943 [0.932, 0.949] | faster |
| open(large) / mainJsMs | 283.1 [287.7] | 248.6 [250.9] | 0.878 [0.867, 0.883] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [2.4] | +0.0 [+0.0, +1.2] | no detectable difference |
| scroll / wallMs | 2885.3 [3079.5] | 2599.9 [2788.3] | 0.901 [0.862, 0.933] | faster |
| scroll / frameP50Ms | 11.1 [14.6] | 11.1 [11.7] | 1.003 [0.990, 1.027] | no detectable difference |
| scroll / frameP95Ms | 24.4 [27.1] | 26.4 [27.2] | 1.082 [0.977, 1.249] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 547.9 [572.8] | 733.2 [781.6] | 1.338 [1.290, 1.389] | slower |
| scroll / mainJsMs | 412.5 [433.4] | 595.4 [630.5] | 1.444 [1.379, 1.503] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [6.6] | +0.0 [+0.0, +2.7] | no detectable difference |
| coldScroll / wallMs | 2820.5 [2888.0] | 2518.5 [2641.8] | 0.893 [0.860, 0.922] | faster |
| coldScroll / frameP50Ms | 11.0 [11.6] | 11.2 [11.7] | 1.020 [0.985, 1.071] | no detectable difference |
| coldScroll / frameP95Ms | 24.4 [26.2] | 26.0 [27.2] | 1.064 [0.968, 1.246] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 562.5 [585.8] | 730.5 [750.8] | 1.299 [1.262, 1.332] | slower |
| coldScroll / mainJsMs | 395.5 [410.0] | 577.9 [596.0] | 1.461 [1.418, 1.506] | slower |
| jump / settleMs | 124.9 [138.7] | 125.6 [129.1] | +0.7 [-4.7, +6.4] | no detectable difference |
| jump / mainTaskMs | 72.5 [81.1] | 117.6 [122.6] | 1.621 [1.498, 1.689] | slower |
| jump / mainJsMs | 53.8 [61.1] | 101.7 [105.1] | 1.891 [1.712, 1.976] | slower |
| zoom / settleMs | 444.7 [469.5] | 519.3 [533.2] | +74.6 [+56.0, +95.7] | slower |
| zoom / zoom2SettleMs | 163.5 [170.3] | 196.7 [208.2] | +33.3 [+23.3, +42.3] | slower |
| zoom / zoom4SettleMs | 279.6 [301.2] | 320.4 [332.8] | +40.8 [+27.4, +54.9] | slower |
| zoom / backTo1SettleMs | 79.4 [101.9] | 99.6 [117.7] | +20.1 [+5.1, +27.1] | slower |
| zoom / mainTaskMs | 211.8 [224.6] | 311.0 [330.5] | 1.469 [1.420, 1.511] | slower |
| zoom / mainJsMs | 151.6 [160.5] | 255.4 [270.4] | 1.684 [1.634, 1.718] | unreliable (A/A failed) |
| zoomLarge / settleMs | 304.1 [340.6] | 315.7 [351.2] | +11.6 [-9.3, +29.6] | no detectable difference |
| zoomLarge / zoom2SettleMs | 143.0 [159.6] | 141.0 [169.5] | -2.0 [-14.3, +9.5] | no detectable difference |
| zoomLarge / zoom4SettleMs | 163.4 [193.0] | 177.9 [185.5] | +14.5 [-0.5, +25.7] | no detectable difference |
| zoomLarge / backTo1SettleMs | 78.4 [98.8] | 84.6 [101.3] | +6.2 [-4.2, +10.8] | no detectable difference |
| zoomLarge / mainTaskMs | 176.0 [182.5] | 258.3 [265.5] | 1.468 [1.412, 1.495] | slower |
| zoomLarge / mainJsMs | 130.4 [135.2] | 216.2 [221.1] | 1.658 [1.586, 1.692] | slower |
| select / settleMs | 234.1 [272.5] | 235.4 [285.5] | +1.3 [-24.1, +38.9] | no detectable difference |
| select / mainTaskMs | 64.7 [70.6] | 85.4 [94.4] | 1.321 [1.244, 1.406] | slower |
| select / mainJsMs | 41.9 [46.6] | 66.8 [74.7] | 1.592 [1.476, 1.708] | slower |
| selectLarge / settleMs | 212.6 [265.4] | 263.6 [282.4] | +51.0 [+25.0, +80.7] | slower |
| selectLarge / mainTaskMs | 72.7 [78.2] | 109.4 [117.7] | 1.505 [1.442, 1.610] | slower |
| selectLarge / mainJsMs | 49.0 [53.9] | 79.5 [86.3] | 1.624 [1.532, 1.748] | slower |
| search / searchMs | 73.7 [79.4] | 76.9 [83.6] | 1.044 [0.981, 1.134] | no detectable difference |
| search / mainTaskMs | 5.7 [6.3] | 11.2 [18.3] | 1.967 [1.614, 2.679] | slower |
| search / mainJsMs | 3.7 [3.9] | 7.0 [8.3] | 1.896 [1.734, 2.230] | slower |
| searchLarge / searchMs | 1911.6 [1935.7] | 1837.9 [1841.8] | 0.961 [0.950, 0.979] | faster |
| searchLarge / mainTaskMs | 343.3 [346.2] | 861.2 [870.3] | 2.509 [2.491, 2.529] | slower |
| searchLarge / mainJsMs | 289.7 [292.2] | 811.6 [822.6] | 2.801 [2.788, 2.813] | slower |
| annotate / settleMs | 216.4 [266.2] | 219.7 [264.8] | +3.3 [-41.5, +41.1] | no detectable difference |
| annotate / mainTaskMs | 78.2 [82.7] | 85.3 [97.4] | 1.090 [1.010, 1.212] | slower |
| annotate / mainJsMs | 47.2 [50.7] | 59.3 [69.4] | 1.257 [1.135, 1.448] | slower |
| annotateLarge / settleMs | 229.0 [254.4] | 259.0 [300.8] | +30.0 [-2.0, +55.2] | no detectable difference |
| annotateLarge / mainTaskMs | 83.4 [86.6] | 120.2 [130.3] | 1.441 [1.343, 1.494] | slower |
| annotateLarge / mainJsMs | 50.2 [52.7] | 75.3 [83.7] | 1.501 [1.358, 1.572] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-tip6 median [p95] | plugins-plainargs median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [35.35 MiB] | 34.34 MiB [35.35 MiB] | 37.04 MiB [37.05 MiB] | 37.05 MiB [38.06 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.17 MiB] | 48.15 MiB [48.19 MiB] | 52.53 MiB [52.56 MiB] | 52.52 MiB [52.54 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.28 MiB] | 22.26 MiB [22.29 MiB] | 23.01 MiB [23.03 MiB] | 23.01 MiB [23.03 MiB] |
| soak / heapCycle5Bytes | 37.42 MiB [38.43 MiB] | 37.43 MiB [38.42 MiB] | 40.73 MiB [41.22 MiB] | 40.23 MiB [41.23 MiB] |
| soak / heapCycle30Bytes | 38.26 MiB [39.27 MiB] | 38.27 MiB [39.27 MiB] | 41.26 MiB [41.76 MiB] | 40.77 MiB [41.77 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

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

