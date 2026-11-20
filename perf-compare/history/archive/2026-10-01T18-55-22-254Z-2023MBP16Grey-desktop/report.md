# Viewer perf comparison: 2026-10-01T18-55-22-254Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | c45b8a7680109679eb8e613a0e9ae0f2d3d46f43 |
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
| plugins-tip3 | research/multi-platform-rust-core-impl-plugins | 373b7f73b | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 5c2e682425eb |
| plugins-sharedhits | research/rust-kernel-shared-hits | b450a9909 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 5f383fbcd10b |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-tip3`: pass
- `plugins-sharedhits`: pass

## Sizes

| Metric | main | main-aa | plugins-tip3 | plugins-sharedhits |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 7.94 MiB | 7.94 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.71 MiB | 1.71 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

49 of 51 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.8 [53.0] | 52.5 [54.0] | 0.994 [0.964, 1.041] | pass |
| open(ebook) / openedMs | 115.6 [116.1] | 114.7 [117.0] | 0.992 [0.981, 1.014] | pass |
| open(ebook) / firstPaintMs | 212.9 [216.2] | 211.6 [214.4] | 0.994 [0.977, 1.018] | pass |
| open(ebook) / settledMs | 334.3 [335.3] | 330.7 [334.4] | 0.989 [0.982, 1.014] | pass |
| open(ebook) / mainTaskMs | 118.9 [119.9] | 118.8 [119.9] | 0.999 [0.986, 1.026] | pass |
| open(ebook) / mainJsMs | 92.6 [94.2] | 92.8 [93.7] | 1.002 [0.976, 1.029] | pass |
| open(large) / readyMs | 52.3 [54.5] | 52.3 [53.3] | 1.001 [0.949, 1.024] | pass |
| open(large) / openedMs | 134.0 [135.4] | 133.5 [135.3] | 0.996 [0.975, 1.023] | pass |
| open(large) / firstPaintMs | 223.0 [223.7] | 222.9 [225.4] | 0.999 [0.996, 1.021] | pass |
| open(large) / settledMs | 220.0 [220.5] | 221.2 [222.6] | 1.005 [0.996, 1.030] | pass |
| open(large) / mainTaskMs | 263.0 [267.3] | 259.4 [264.3] | 0.986 [0.968, 1.012] | pass |
| open(large) / mainJsMs | 239.2 [244.0] | 236.6 [240.8] | 0.989 [0.967, 1.012] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2016.4 [2026.8] | 2032.6 [2050.6] | 1.008 [0.986, 1.027] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.8 [9.0] | 8.9 [9.1] | 1.013 [0.957, 1.089] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 408.8 [409.1] | 417.1 [423.7] | 1.020 [0.985, 1.043] | pass |
| scroll / mainJsMs | 316.8 [317.0] | 323.6 [326.5] | 1.021 [0.982, 1.036] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2025.0 [2025.6] | 2023.1 [2026.3] | 0.999 [0.998, 1.013] | pass |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| coldScroll / frameP95Ms | 9.2 [9.2] | 8.6 [8.9] | 0.941 [0.939, 1.021] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 465.0 [479.5] | 443.5 [445.8] | 0.954 [0.883, 0.980] | A/A failed |
| coldScroll / mainJsMs | 335.5 [346.0] | 317.3 [323.7] | 0.946 [0.858, 0.980] | A/A failed |
| jump / settleMs | 94.5 [97.0] | 96.8 [97.9] | +2.2 [-7.5, +8.5] | pass |
| jump / mainTaskMs | 56.7 [58.9] | 57.2 [59.5] | 1.008 [0.954, 1.068] | pass |
| jump / mainJsMs | 42.7 [46.8] | 42.9 [45.2] | 1.004 [0.903, 1.074] | pass |
| zoom / settleMs | 336.7 [359.4] | 338.4 [341.8] | +1.7 [-30.6, +6.5] | pass |
| zoom / zoom2SettleMs | 128.6 [130.0] | 125.2 [130.4] | -3.4 [-5.7, +5.5] | pass |
| zoom / zoom4SettleMs | 211.2 [229.7] | 207.4 [216.1] | -3.8 [-24.8, +9.9] | pass |
| zoom / backTo1SettleMs | 71.9 [79.5] | 64.9 [68.1] | -7.0 [-16.2, +2.7] | pass |
| zoom / mainTaskMs | 170.7 [174.0] | 167.4 [168.5] | 0.981 [0.916, 1.005] | pass |
| zoom / mainJsMs | 124.7 [127.2] | 121.5 [122.9] | 0.974 [0.927, 1.002] | pass |
| select / settleMs | 147.6 [148.4] | 146.5 [148.4] | -1.2 [-3.3, +1.9] | pass |
| select / mainTaskMs | 39.2 [41.8] | 42.3 [42.8] | 1.077 [0.999, 1.147] | pass |
| select / mainJsMs | 23.6 [25.9] | 26.2 [26.5] | 1.108 [0.962, 1.153] | pass |
| search / searchMs | 51.9 [58.3] | 50.1 [57.7] | 0.964 [0.844, 1.199] | pass |
| search / mainTaskMs | 4.6 [4.7] | 4.3 [4.4] | 0.927 [0.898, 1.057] | pass |
| search / mainJsMs | 2.9 [3.3] | 3.0 [3.0] | 1.019 [0.900, 1.047] | pass |
| searchLarge / searchMs | 1650.5 [1654.9] | 1610.0 [1655.2] | 0.975 [0.972, 1.025] | pass |
| searchLarge / mainTaskMs | 269.6 [271.4] | 268.0 [270.8] | 0.994 [0.985, 1.010] | pass |
| searchLarge / mainJsMs | 232.4 [233.9] | 231.4 [233.3] | 0.996 [0.987, 1.011] | pass |
| annotate / settleMs | 153.9 [168.1] | 158.4 [160.1] | +4.5 [-16.3, +7.0] | pass |
| annotate / mainTaskMs | 56.1 [59.1] | 52.9 [58.0] | 0.942 [0.875, 1.046] | pass |
| annotate / mainJsMs | 33.4 [35.1] | 32.3 [35.4] | 0.965 [0.883, 1.082] | pass |

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

Not run: `plugins-lazy`.

## `main` vs `plugins-tip3`

| Row | main median [p95] | plugins-tip3 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.8 [53.0] | 75.5 [80.1] | 1.431 [1.418, 1.548] | slower |
| open(ebook) / openedMs | 115.6 [116.1] | 174.4 [177.4] | 1.508 [1.486, 1.538] | slower |
| open(ebook) / firstPaintMs | 212.9 [216.2] | 296.1 [298.1] | 1.391 [1.353, 1.414] | slower |
| open(ebook) / settledMs | 334.3 [335.3] | 350.8 [352.9] | 1.049 [1.015, 1.070] | slower |
| open(ebook) / mainTaskMs | 118.9 [119.9] | 234.7 [234.9] | 1.973 [1.913, 2.007] | slower |
| open(ebook) / mainJsMs | 92.6 [94.2] | 195.0 [195.6] | 2.107 [2.030, 2.146] | slower |
| open(large) / readyMs | 52.3 [54.5] | 74.4 [74.9] | 1.424 [1.357, 1.437] | slower |
| open(large) / openedMs | 134.0 [135.4] | 200.0 [200.7] | 1.492 [1.469, 1.517] | slower |
| open(large) / firstPaintMs | 223.0 [223.7] | 324.0 [325.3] | 1.453 [1.446, 1.472] | slower |
| open(large) / settledMs | 220.0 [220.5] | 330.2 [330.4] | 1.501 [1.474, 1.528] | slower |
| open(large) / mainTaskMs | 263.0 [267.3] | 505.4 [505.5] | 1.922 [1.881, 1.932] | slower |
| open(large) / mainJsMs | 239.2 [244.0] | 468.8 [469.6] | 1.960 [1.913, 1.970] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2016.4 [2026.8] | 2246.9 [2256.5] | 1.114 [1.104, 1.129] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.8 [9.0] | 9.2 [9.2] | 1.041 [1.019, 1.101] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 408.8 [409.1] | 749.5 [761.3] | 1.833 [1.806, 1.875] | slower |
| scroll / mainJsMs | 316.8 [317.0] | 646.6 [656.1] | 2.041 [2.005, 2.083] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2025.0 [2025.6] | 2221.7 [2244.1] | 1.097 [1.085, 1.123] | slower |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 9.2 [9.2] | 9.5 [9.5] | 1.040 [1.023, 1.090] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 465.0 [479.5] | 736.0 [774.2] | 1.583 [1.491, 1.710] | unreliable (A/A failed) |
| coldScroll / mainJsMs | 335.5 [346.0] | 619.1 [650.2] | 1.845 [1.747, 1.975] | unreliable (A/A failed) |
| jump / settleMs | 94.5 [97.0] | 170.2 [171.6] | +75.6 [+62.4, +82.3] | slower |
| jump / mainTaskMs | 56.7 [58.9] | 153.8 [155.8] | 2.712 [2.490, 2.787] | slower |
| jump / mainJsMs | 42.7 [46.8] | 140.8 [141.7] | 3.294 [2.844, 3.349] | slower |
| zoom / settleMs | 336.7 [359.4] | 394.8 [411.1] | +58.1 [+17.5, +77.2] | slower |
| zoom / zoom2SettleMs | 128.6 [130.0] | 150.0 [152.5] | +21.4 [+12.0, +27.2] | slower |
| zoom / zoom4SettleMs | 211.2 [229.7] | 252.7 [261.9] | +41.5 [-5.1, +55.8] | no detectable difference |
| zoom / backTo1SettleMs | 71.9 [79.5] | 83.2 [97.8] | +11.3 [+2.1, +33.7] | no detectable difference |
| zoom / mainTaskMs | 170.7 [174.0] | 264.9 [268.8] | 1.552 [1.479, 1.605] | slower |
| zoom / mainJsMs | 124.7 [127.2] | 220.8 [225.3] | 1.771 [1.698, 1.838] | slower |
| select / settleMs | 147.6 [148.4] | 144.7 [145.0] | -2.9 [-7.4, -1.7] | no detectable difference |
| select / mainTaskMs | 39.2 [41.8] | 63.0 [63.7] | 1.606 [1.431, 1.708] | slower |
| select / mainJsMs | 23.6 [25.9] | 47.5 [49.3] | 2.007 [1.732, 2.156] | slower |
| search / searchMs | 51.9 [58.3] | 66.5 [67.7] | 1.281 [1.005, 1.390] | slower |
| search / mainTaskMs | 4.6 [4.7] | 17.6 [19.2] | 3.806 [1.992, 4.651] | slower |
| search / mainJsMs | 2.9 [3.3] | 10.2 [10.6] | 3.494 [1.806, 3.676] | slower |
| searchLarge / searchMs | 1650.5 [1654.9] | 1873.5 [1879.5] | 1.135 [1.129, 1.161] | slower |
| searchLarge / mainTaskMs | 269.6 [271.4] | 1038.0 [1038.1] | 3.849 [3.803, 3.866] | slower |
| searchLarge / mainJsMs | 232.4 [233.9] | 1000.1 [1000.8] | 4.304 [4.244, 4.336] | slower |
| annotate / settleMs | 153.9 [168.1] | 162.1 [167.6] | +8.2 [-11.0, +14.9] | no detectable difference |
| annotate / mainTaskMs | 56.1 [59.1] | 78.9 [79.7] | 1.406 [1.237, 1.426] | slower |
| annotate / mainJsMs | 33.4 [35.1] | 57.4 [58.3] | 1.715 [1.522, 1.771] | slower |

## `plugins-tip3` vs `plugins-sharedhits`

| Row | plugins-tip3 median [p95] | plugins-sharedhits median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 75.5 [80.1] | 77.0 [77.2] | 1.019 [0.940, 1.028] | no detectable difference |
| open(ebook) / openedMs | 174.4 [177.4] | 174.7 [176.2] | 1.002 [0.980, 1.022] | no detectable difference |
| open(ebook) / firstPaintMs | 296.1 [298.1] | 300.4 [300.6] | 1.015 [0.999, 1.027] | no detectable difference |
| open(ebook) / settledMs | 350.8 [352.9] | 347.1 [347.9] | 0.989 [0.979, 1.022] | no detectable difference |
| open(ebook) / mainTaskMs | 234.7 [234.9] | 233.8 [234.8] | 0.996 [0.993, 1.023] | no detectable difference |
| open(ebook) / mainJsMs | 195.0 [195.6] | 194.6 [195.9] | 0.998 [0.995, 1.024] | no detectable difference |
| open(large) / readyMs | 74.4 [74.9] | 74.6 [75.0] | 1.003 [0.986, 1.010] | no detectable difference |
| open(large) / openedMs | 200.0 [200.7] | 197.1 [200.2] | 0.986 [0.981, 1.006] | no detectable difference |
| open(large) / firstPaintMs | 324.0 [325.3] | 322.1 [327.6] | 0.994 [0.981, 1.014] | no detectable difference |
| open(large) / settledMs | 330.2 [330.4] | 326.9 [329.1] | 0.990 [0.964, 1.013] | no detectable difference |
| open(large) / mainTaskMs | 505.4 [505.5] | 504.1 [506.7] | 0.997 [0.983, 1.007] | no detectable difference |
| open(large) / mainJsMs | 468.8 [469.6] | 467.3 [469.0] | 0.997 [0.982, 1.003] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2246.9 [2256.5] | 2236.1 [2249.3] | 0.995 [0.990, 1.006] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.2 [9.2] | 9.6 [9.7] | 1.052 [0.999, 1.058] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 749.5 [761.3] | 746.4 [751.9] | 0.996 [0.957, 1.019] | no detectable difference |
| scroll / mainJsMs | 646.6 [656.1] | 639.7 [645.8] | 0.989 [0.957, 1.017] | no detectable difference |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2221.7 [2244.1] | 2230.7 [2259.7] | 1.004 [0.981, 1.030] | no detectable difference |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 9.5 [9.5] | 9.4 [9.4] | 0.988 [0.950, 1.006] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 736.0 [774.2] | 759.4 [764.9] | 1.032 [0.966, 1.067] | unreliable (A/A failed) |
| coldScroll / mainJsMs | 619.1 [650.2] | 639.9 [642.3] | 1.034 [0.968, 1.060] | unreliable (A/A failed) |
| jump / settleMs | 170.2 [171.6] | 121.2 [168.2] | -49.0 [-55.1, +13.7] | no detectable difference |
| jump / mainTaskMs | 153.8 [155.8] | 147.7 [155.5] | 0.960 [0.928, 1.061] | no detectable difference |
| jump / mainJsMs | 140.8 [141.7] | 136.0 [141.7] | 0.966 [0.938, 1.058] | no detectable difference |
| zoom / settleMs | 394.8 [411.1] | 390.4 [393.3] | -4.5 [-26.6, +14.2] | no detectable difference |
| zoom / zoom2SettleMs | 150.0 [152.5] | 150.3 [152.1] | +0.3 [-6.0, +10.2] | no detectable difference |
| zoom / zoom4SettleMs | 252.7 [261.9] | 243.3 [243.6] | -9.4 [-29.0, +16.9] | no detectable difference |
| zoom / backTo1SettleMs | 83.2 [97.8] | 80.9 [82.4] | -2.3 [-21.0, +0.1] | no detectable difference |
| zoom / mainTaskMs | 264.9 [268.8] | 260.6 [265.6] | 0.984 [0.966, 1.032] | no detectable difference |
| zoom / mainJsMs | 220.8 [225.3] | 221.1 [221.7] | 1.001 [0.971, 1.025] | no detectable difference |
| select / settleMs | 144.7 [145.0] | 144.6 [144.9] | -0.1 [-2.6, +3.9] | no detectable difference |
| select / mainTaskMs | 63.0 [63.7] | 61.7 [62.7] | 0.979 [0.930, 1.043] | no detectable difference |
| select / mainJsMs | 47.5 [49.3] | 48.0 [48.6] | 1.011 [0.922, 1.074] | no detectable difference |
| search / searchMs | 66.5 [67.7] | 61.5 [66.0] | 0.924 [0.781, 1.121] | no detectable difference |
| search / mainTaskMs | 17.6 [19.2] | 14.0 [17.0] | 0.799 [0.388, 1.837] | no detectable difference |
| search / mainJsMs | 10.2 [10.6] | 6.2 [6.9] | 0.607 [0.483, 1.177] | no detectable difference |
| searchLarge / searchMs | 1873.5 [1879.5] | 1749.3 [1754.2] | 0.934 [0.930, 0.939] | faster |
| searchLarge / mainTaskMs | 1038.0 [1038.1] | 889.5 [897.5] | 0.857 [0.855, 0.870] | faster |
| searchLarge / mainJsMs | 1000.1 [1000.8] | 853.8 [859.6] | 0.854 [0.852, 0.866] | faster |
| annotate / settleMs | 162.1 [167.6] | 166.2 [167.2] | +4.1 [-4.3, +8.7] | no detectable difference |
| annotate / mainTaskMs | 78.9 [79.7] | 74.8 [75.5] | 0.947 [0.909, 1.028] | no detectable difference |
| annotate / mainJsMs | 57.4 [58.3] | 54.8 [54.9] | 0.954 [0.907, 1.023] | no detectable difference |

## `main` vs `plugins-sharedhits`

| Row | main median [p95] | plugins-sharedhits median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.8 [53.0] | 77.0 [77.2] | 1.459 [1.429, 1.484] | slower |
| open(ebook) / openedMs | 115.6 [116.1] | 174.7 [176.2] | 1.511 [1.500, 1.526] | slower |
| open(ebook) / firstPaintMs | 212.9 [216.2] | 300.4 [300.6] | 1.411 [1.376, 1.425] | slower |
| open(ebook) / settledMs | 334.3 [335.3] | 347.1 [347.9] | 1.038 [1.030, 1.054] | slower |
| open(ebook) / mainTaskMs | 118.9 [119.9] | 233.8 [234.8] | 1.966 [1.944, 2.007] | slower |
| open(ebook) / mainJsMs | 92.6 [94.2] | 194.6 [195.9] | 2.103 [2.063, 2.150] | slower |
| open(large) / readyMs | 52.3 [54.5] | 74.6 [75.0] | 1.429 [1.349, 1.439] | slower |
| open(large) / openedMs | 134.0 [135.4] | 197.1 [200.2] | 1.471 [1.453, 1.514] | slower |
| open(large) / firstPaintMs | 223.0 [223.7] | 322.1 [327.6] | 1.444 [1.426, 1.484] | slower |
| open(large) / settledMs | 220.0 [220.5] | 326.9 [329.1] | 1.486 [1.444, 1.523] | slower |
| open(large) / mainTaskMs | 263.0 [267.3] | 504.1 [506.7] | 1.917 [1.856, 1.937] | slower |
| open(large) / mainJsMs | 239.2 [244.0] | 467.3 [469.0] | 1.954 [1.887, 1.968] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2016.4 [2026.8] | 2236.1 [2249.3] | 1.109 [1.102, 1.126] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.8 [9.0] | 9.6 [9.7] | 1.094 [1.029, 1.153] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 408.8 [409.1] | 746.4 [751.9] | 1.826 [1.783, 1.850] | slower |
| scroll / mainJsMs | 316.8 [317.0] | 639.7 [645.8] | 2.019 [1.984, 2.049] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2025.0 [2025.6] | 2230.7 [2259.7] | 1.102 [1.088, 1.131] | slower |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| coldScroll / frameP95Ms | 9.2 [9.2] | 9.4 [9.4] | 1.027 [0.989, 1.076] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 465.0 [479.5] | 759.4 [764.9] | 1.633 [1.563, 1.681] | unreliable (A/A failed) |
| coldScroll / mainJsMs | 335.5 [346.0] | 639.9 [642.3] | 1.907 [1.823, 1.941] | unreliable (A/A failed) |
| jump / settleMs | 94.5 [97.0] | 121.2 [168.2] | +26.6 [+19.3, +83.9] | slower |
| jump / mainTaskMs | 56.7 [58.9] | 147.7 [155.5] | 2.604 [2.448, 2.793] | slower |
| jump / mainJsMs | 42.7 [46.8] | 136.0 [141.7] | 3.181 [2.814, 3.360] | slower |
| zoom / settleMs | 336.7 [359.4] | 390.4 [393.3] | +53.7 [+24.4, +57.8] | slower |
| zoom / zoom2SettleMs | 128.6 [130.0] | 150.3 [152.1] | +21.7 [+16.6, +26.8] | slower |
| zoom / zoom4SettleMs | 211.2 [229.7] | 243.3 [243.6] | +32.1 [+2.2, +36.4] | slower |
| zoom / backTo1SettleMs | 71.9 [79.5] | 80.9 [82.4] | +9.0 [-2.0, +16.9] | no detectable difference |
| zoom / mainTaskMs | 170.7 [174.0] | 260.6 [265.6] | 1.527 [1.491, 1.587] | slower |
| zoom / mainJsMs | 124.7 [127.2] | 221.1 [221.7] | 1.773 [1.721, 1.805] | slower |
| select / settleMs | 147.6 [148.4] | 144.6 [144.9] | -3.0 [-6.1, -1.8] | no detectable difference |
| select / mainTaskMs | 39.2 [41.8] | 61.7 [62.7] | 1.573 [1.409, 1.682] | slower |
| select / mainJsMs | 23.6 [25.9] | 48.0 [48.6] | 2.029 [1.744, 2.119] | slower |
| search / searchMs | 51.9 [58.3] | 61.5 [66.0] | 1.184 [0.897, 1.363] | no detectable difference |
| search / mainTaskMs | 4.6 [4.7] | 14.0 [17.0] | 3.040 [1.592, 4.151] | slower |
| search / mainJsMs | 2.9 [3.3] | 6.2 [6.9] | 2.120 [1.563, 2.415] | slower |
| searchLarge / searchMs | 1650.5 [1654.9] | 1749.3 [1754.2] | 1.060 [1.056, 1.084] | slower |
| searchLarge / mainTaskMs | 269.6 [271.4] | 889.5 [897.5] | 3.299 [3.269, 3.346] | slower |
| searchLarge / mainJsMs | 232.4 [233.9] | 853.8 [859.6] | 3.674 [3.642, 3.726] | slower |
| annotate / settleMs | 153.9 [168.1] | 166.2 [167.2] | +12.3 [-5.8, +14.0] | no detectable difference |
| annotate / mainTaskMs | 56.1 [59.1] | 74.8 [75.5] | 1.332 [1.219, 1.351] | slower |
| annotate / mainJsMs | 33.4 [35.1] | 54.8 [54.9] | 1.637 [1.501, 1.666] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-tip3 median [p95] | plugins-sharedhits median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.34 MiB [34.35 MiB] | 41.34 MiB [42.25 MiB] | 41.33 MiB [42.23 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.16 MiB [48.16 MiB] | 48.15 MiB [48.18 MiB] | 57.61 MiB [57.62 MiB] | 57.61 MiB [57.61 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.28 MiB] | 23.01 MiB [23.01 MiB] | 23.00 MiB [23.01 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.41 MiB] | 37.42 MiB [37.42 MiB] | 44.13 MiB [44.14 MiB] | 44.15 MiB [45.05 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.27 MiB] | 38.28 MiB [38.28 MiB] | 44.69 MiB [44.69 MiB] | 44.70 MiB [45.61 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-tip3`:

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

`plugins-sharedhits`:

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

