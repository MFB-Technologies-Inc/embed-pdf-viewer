# Viewer perf comparison: 2026-10-08T00-30-29-463Z-macmini2020-slow

10 rounds, profile `slow` (CPU throttle 4x).

The throttle reached the main thread only: Chromium refuses `Emulation.setCPUThrottlingRate` on the engine worker, so worker-side work ran at full speed.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 932bf52f1af7b9d36eb9eafb541f8c97787a41b9 |
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
| plugins-raf | research/rust-kernel-coalesce-raf | 41714a1fc | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | aee9b9460008 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-coalesce`: pass
- `plugins-raf`: pass

## Sizes

| Metric | main | main-aa | plugins-coalesce | plugins-raf |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 8.04 MiB | 8.04 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.72 MiB | 1.72 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

62 of 63 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 175.8 [178.1] | 176.3 [178.0] | 1.003 [0.993, 1.009] | pass |
| open(ebook) / openedMs | 315.4 [319.1] | 315.2 [317.2] | 0.999 [0.991, 1.005] | pass |
| open(ebook) / firstPaintMs | 551.3 [557.5] | 543.4 [562.4] | 0.986 [0.969, 1.013] | pass |
| open(ebook) / settledMs | 731.5 [743.6] | 724.8 [732.1] | 0.991 [0.982, 1.001] | pass |
| open(ebook) / mainTaskMs | 548.8 [564.9] | 545.0 [559.0] | 0.993 [0.971, 1.002] | pass |
| open(ebook) / mainJsMs | 435.9 [445.4] | 431.9 [443.5] | 0.991 [0.970, 1.001] | pass |
| open(large) / readyMs | 175.7 [177.2] | 176.2 [177.9] | 1.003 [0.997, 1.013] | pass |
| open(large) / openedMs | 374.8 [379.2] | 374.0 [376.1] | 0.998 [0.990, 1.002] | pass |
| open(large) / firstPaintMs | 601.3 [616.9] | 589.7 [605.7] | 0.981 [0.968, 0.994] | pass |
| open(large) / settledMs | 615.9 [627.9] | 616.8 [626.9] | 1.001 [0.990, 1.015] | pass |
| open(large) / mainTaskMs | 795.7 [807.8] | 798.6 [806.2] | 1.004 [0.994, 1.023] | pass |
| open(large) / mainJsMs | 690.5 [698.8] | 691.6 [697.4] | 1.002 [0.995, 1.021] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 3083.5 [3180.8] | 3107.2 [3146.6] | 1.008 [0.998, 1.025] | pass |
| scroll / frameP50Ms | 12.0 [12.3] | 11.8 [12.8] | 0.986 [0.966, 1.048] | pass |
| scroll / frameP95Ms | 26.0 [26.3] | 26.0 [26.3] | 1.002 [0.994, 1.010] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 1294.1 [1310.4] | 1299.5 [1334.8] | 1.004 [0.996, 1.024] | pass |
| scroll / mainJsMs | 959.0 [983.1] | 962.8 [984.0] | 1.004 [0.985, 1.026] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 3176.8 [3290.8] | 3176.2 [3253.3] | 1.000 [0.965, 1.026] | pass |
| coldScroll / frameP50Ms | 12.0 [12.5] | 12.2 [12.6] | 1.014 [0.984, 1.047] | pass |
| coldScroll / frameP95Ms | 26.0 [26.5] | 26.2 [26.6] | 1.005 [0.995, 1.022] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 1527.0 [1567.4] | 1543.1 [1605.2] | 1.011 [0.990, 1.041] | pass |
| coldScroll / mainJsMs | 1125.2 [1163.7] | 1137.1 [1190.3] | 1.011 [0.992, 1.037] | pass |
| jump / settleMs | 195.6 [213.7] | 198.2 [216.5] | +2.6 [-6.5, +10.0] | pass |
| jump / mainTaskMs | 226.4 [250.6] | 234.9 [253.5] | 1.038 [0.957, 1.118] | pass |
| jump / mainJsMs | 177.2 [189.4] | 174.3 [188.8] | 0.983 [0.935, 1.063] | pass |
| zoom / settleMs | 630.8 [662.0] | 628.3 [652.0] | -2.5 [-25.0, +28.2] | pass |
| zoom / zoom2SettleMs | 260.1 [282.9] | 267.6 [279.6] | +7.5 [-11.5, +17.8] | pass |
| zoom / zoom4SettleMs | 357.0 [385.1] | 358.4 [387.0] | +1.3 [-24.9, +19.9] | pass |
| zoom / backTo1SettleMs | 155.6 [159.3] | 156.6 [162.1] | +1.0 [-2.0, +6.1] | pass |
| zoom / mainTaskMs | 761.2 [820.3] | 767.6 [803.2] | 1.008 [0.958, 1.058] | pass |
| zoom / mainJsMs | 564.8 [620.7] | 575.4 [603.5] | 1.019 [0.944, 1.056] | pass |
| zoomLarge / settleMs | 399.4 [420.6] | 396.1 [413.7] | -3.3 [-34.2, +19.2] | pass |
| zoomLarge / zoom2SettleMs | 178.8 [188.9] | 171.1 [189.3] | -7.7 [-17.5, +13.7] | pass |
| zoomLarge / zoom4SettleMs | 237.4 [245.0] | 223.9 [234.5] | -13.5 [-39.2, +25.1] | pass |
| zoomLarge / backTo1SettleMs | 158.5 [174.7] | 155.6 [170.5] | -2.9 [-9.9, +2.6] | pass |
| zoomLarge / mainTaskMs | 516.3 [540.2] | 509.2 [552.6] | 0.986 [0.924, 1.037] | pass |
| zoomLarge / mainJsMs | 393.0 [408.5] | 386.2 [418.2] | 0.983 [0.917, 1.044] | pass |
| select / settleMs | 223.4 [262.7] | 236.6 [268.8] | +13.1 [-9.8, +42.3] | pass |
| select / mainTaskMs | 86.0 [88.2] | 85.8 [87.2] | 0.998 [0.973, 1.012] | pass |
| select / mainJsMs | 56.3 [58.4] | 56.9 [58.3] | 1.010 [0.984, 1.045] | pass |
| selectLarge / settleMs | 237.0 [273.5] | 224.7 [252.7] | -12.3 [-27.4, +17.8] | pass |
| selectLarge / mainTaskMs | 97.4 [99.5] | 98.7 [99.9] | 1.014 [0.990, 1.036] | pass |
| selectLarge / mainJsMs | 67.6 [69.5] | 67.9 [70.1] | 1.004 [0.973, 1.032] | pass |
| search / searchMs | 60.5 [61.0] | 60.1 [60.6] | 0.993 [0.986, 1.006] | pass |
| search / mainTaskMs | 17.7 [19.0] | 17.8 [19.3] | 1.003 [0.881, 1.109] | pass |
| search / mainJsMs | 12.3 [13.1] | 12.1 [14.3] | 0.988 [0.931, 1.055] | pass |
| searchLarge / searchMs | 2635.8 [2896.2] | 2641.6 [2846.9] | 1.002 [0.939, 1.066] | pass |
| searchLarge / mainTaskMs | 1206.7 [1437.2] | 1213.7 [1414.3] | 1.006 [0.862, 1.174] | pass |
| searchLarge / mainJsMs | 1070.8 [1295.2] | 1073.6 [1279.6] | 1.003 [0.852, 1.190] | pass |
| annotate / settleMs | 309.5 [333.1] | 276.6 [308.6] | -32.9 [-52.4, -1.4] | A/A failed |
| annotate / mainTaskMs | 162.3 [164.9] | 163.6 [169.6] | 1.008 [0.994, 1.024] | pass |
| annotate / mainJsMs | 99.2 [102.3] | 101.0 [106.2] | 1.018 [0.977, 1.053] | pass |
| annotateLarge / settleMs | 280.4 [298.9] | 272.2 [297.3] | -8.2 [-29.7, +8.7] | pass |
| annotateLarge / mainTaskMs | 164.4 [166.5] | 163.2 [167.8] | 0.993 [0.979, 1.017] | pass |
| annotateLarge / mainJsMs | 100.7 [103.6] | 101.2 [102.1] | 1.005 [0.989, 1.024] | pass |

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
| open(ebook) / readyMs | 175.8 [178.1] | 317.5 [321.1] | 1.807 [1.789, 1.826] | slower |
| open(ebook) / openedMs | 315.4 [319.1] | 493.2 [496.9] | 1.563 [1.552, 1.574] | slower |
| open(ebook) / firstPaintMs | 551.3 [557.5] | 849.6 [882.1] | 1.541 [1.509, 1.591] | slower |
| open(ebook) / settledMs | 731.5 [743.6] | 953.5 [978.5] | 1.304 [1.263, 1.326] | slower |
| open(ebook) / mainTaskMs | 548.8 [564.9] | 948.0 [981.4] | 1.727 [1.682, 1.772] | slower |
| open(ebook) / mainJsMs | 435.9 [445.4] | 788.9 [818.4] | 1.810 [1.753, 1.843] | slower |
| open(large) / readyMs | 175.7 [177.2] | 318.4 [321.6] | 1.812 [1.799, 1.829] | slower |
| open(large) / openedMs | 374.8 [379.2] | 578.6 [582.2] | 1.544 [1.530, 1.553] | slower |
| open(large) / firstPaintMs | 601.3 [616.9] | 969.6 [1012.7] | 1.612 [1.575, 1.640] | slower |
| open(large) / settledMs | 615.9 [627.9] | 993.2 [1007.0] | 1.613 [1.598, 1.628] | slower |
| open(large) / mainTaskMs | 795.7 [807.8] | 1070.4 [1089.0] | 1.345 [1.332, 1.370] | slower |
| open(large) / mainJsMs | 690.5 [698.8] | 914.2 [934.5] | 1.324 [1.309, 1.349] | slower |
| scroll / settleMs | 0.0 [0.0] | 8.9 [40.5] | +8.9 [+0.6, +33.3] | slower |
| scroll / wallMs | 3083.5 [3180.8] | 2882.7 [3018.8] | 0.935 [0.919, 0.964] | faster |
| scroll / frameP50Ms | 12.0 [12.3] | 12.4 [12.9] | 1.035 [1.007, 1.069] | no detectable difference |
| scroll / frameP95Ms | 26.0 [26.3] | 27.2 [27.2] | 1.046 [1.027, 1.053] | slower |
| scroll / jankFrames | 0 [0] | 0 [0.55] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 1294.1 [1310.4] | 2360.8 [2406.5] | 1.824 [1.800, 1.851] | slower |
| scroll / mainJsMs | 959.0 [983.1] | 1985.9 [2007.8] | 2.071 [2.027, 2.094] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 35.2 [61.0] | +35.2 [+17.8, +46.1] | slower |
| coldScroll / wallMs | 3176.8 [3290.8] | 2874.1 [3024.3] | 0.905 [0.868, 0.941] | faster |
| coldScroll / frameP50Ms | 12.0 [12.5] | 12.0 [12.9] | 1.002 [0.974, 1.051] | no detectable difference |
| coldScroll / frameP95Ms | 26.0 [26.5] | 27.0 [27.3] | 1.037 [1.025, 1.048] | slower |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 1527.0 [1567.4] | 2469.5 [2498.9] | 1.617 [1.583, 1.663] | slower |
| coldScroll / mainJsMs | 1125.2 [1163.7] | 2037.2 [2070.0] | 1.810 [1.778, 1.852] | slower |
| jump / settleMs | 195.6 [213.7] | 367.9 [398.0] | +172.2 [+136.9, +194.8] | slower |
| jump / mainTaskMs | 226.4 [250.6] | 510.9 [561.5] | 2.256 [2.101, 2.382] | slower |
| jump / mainJsMs | 177.2 [189.4] | 464.5 [505.9] | 2.621 [2.464, 2.753] | slower |
| zoom / settleMs | 630.8 [662.0] | 859.6 [893.8] | +228.8 [+216.2, +260.5] | slower |
| zoom / zoom2SettleMs | 260.1 [282.9] | 347.6 [366.2] | +87.5 [+69.5, +101.5] | slower |
| zoom / zoom4SettleMs | 357.0 [385.1] | 514.7 [539.5] | +157.6 [+127.7, +174.3] | slower |
| zoom / backTo1SettleMs | 155.6 [159.3] | 204.1 [214.5] | +48.5 [+43.2, +57.9] | slower |
| zoom / mainTaskMs | 761.2 [820.3] | 1305.5 [1351.6] | 1.715 [1.625, 1.781] | slower |
| zoom / mainJsMs | 564.8 [620.7] | 1118.5 [1156.5] | 1.980 [1.835, 2.041] | slower |
| zoomLarge / settleMs | 399.4 [420.6] | 694.8 [789.4] | +295.4 [+252.0, +344.5] | slower |
| zoomLarge / zoom2SettleMs | 178.8 [188.9] | 314.6 [363.5] | +135.8 [+106.6, +161.0] | slower |
| zoomLarge / zoom4SettleMs | 237.4 [245.0] | 384.4 [451.5] | +147.0 [+129.7, +203.9] | slower |
| zoomLarge / backTo1SettleMs | 158.5 [174.7] | 218.8 [234.9] | +60.2 [+52.5, +75.0] | slower |
| zoomLarge / mainTaskMs | 516.3 [540.2] | 1162.2 [1194.8] | 2.251 [2.190, 2.360] | slower |
| zoomLarge / mainJsMs | 393.0 [408.5] | 1007.2 [1036.6] | 2.563 [2.491, 2.721] | slower |
| select / settleMs | 223.4 [262.7] | 321.0 [350.7] | +97.6 [+35.4, +120.8] | slower |
| select / mainTaskMs | 86.0 [88.2] | 154.7 [164.9] | 1.800 [1.757, 1.874] | slower |
| select / mainJsMs | 56.3 [58.4] | 128.6 [133.7] | 2.282 [2.168, 2.389] | slower |
| selectLarge / settleMs | 237.0 [273.5] | 359.9 [401.6] | +122.9 [+111.4, +155.9] | slower |
| selectLarge / mainTaskMs | 97.4 [99.5] | 238.2 [254.3] | 2.446 [2.407, 2.541] | slower |
| selectLarge / mainJsMs | 67.6 [69.5] | 197.4 [210.2] | 2.919 [2.834, 3.012] | slower |
| search / searchMs | 60.5 [61.0] | 69.4 [71.2] | 1.147 [1.139, 1.168] | slower |
| search / mainTaskMs | 17.7 [19.0] | 33.2 [34.2] | 1.874 [1.723, 2.064] | slower |
| search / mainJsMs | 12.3 [13.1] | 23.1 [24.1] | 1.878 [1.763, 1.958] | slower |
| searchLarge / searchMs | 2635.8 [2896.2] | 3455.9 [3487.0] | 1.311 [1.233, 1.333] | slower |
| searchLarge / mainTaskMs | 1206.7 [1437.2] | 3436.9 [3471.3] | 2.848 [2.442, 2.890] | slower |
| searchLarge / mainJsMs | 1070.8 [1295.2] | 3366.2 [3395.4] | 3.144 [2.669, 3.200] | slower |
| annotate / settleMs | 309.5 [333.1] | 398.0 [422.1] | +88.5 [+37.1, +112.8] | unreliable (A/A failed) |
| annotate / mainTaskMs | 162.3 [164.9] | 255.1 [275.3] | 1.572 [1.517, 1.663] | slower |
| annotate / mainJsMs | 99.2 [102.3] | 192.0 [204.6] | 1.935 [1.824, 2.054] | slower |
| annotateLarge / settleMs | 280.4 [298.9] | 449.1 [475.7] | +168.7 [+145.1, +195.3] | slower |
| annotateLarge / mainTaskMs | 164.4 [166.5] | 337.0 [342.8] | 2.050 [2.022, 2.103] | slower |
| annotateLarge / mainJsMs | 100.7 [103.6] | 245.5 [249.8] | 2.438 [2.389, 2.485] | slower |

## `plugins-coalesce` vs `plugins-raf`

| Row | plugins-coalesce median [p95] | plugins-raf median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 317.5 [321.1] | 316.1 [319.9] | 0.995 [0.984, 1.004] | no detectable difference |
| open(ebook) / openedMs | 493.2 [496.9] | 491.2 [494.3] | 0.996 [0.991, 1.001] | no detectable difference |
| open(ebook) / firstPaintMs | 849.6 [882.1] | 869.0 [885.9] | 1.023 [0.999, 1.051] | no detectable difference |
| open(ebook) / settledMs | 953.5 [978.5] | 973.7 [1019.6] | 1.021 [0.999, 1.062] | no detectable difference |
| open(ebook) / mainTaskMs | 948.0 [981.4] | 991.9 [1019.3] | 1.046 [1.013, 1.081] | slower |
| open(ebook) / mainJsMs | 788.9 [818.4] | 825.9 [848.3] | 1.047 [1.014, 1.087] | slower |
| open(large) / readyMs | 318.4 [321.6] | 318.4 [320.9] | 1.000 [0.992, 1.009] | no detectable difference |
| open(large) / openedMs | 578.6 [582.2] | 578.2 [582.5] | 0.999 [0.992, 1.007] | no detectable difference |
| open(large) / firstPaintMs | 969.6 [1012.7] | 992.5 [1062.8] | 1.024 [0.999, 1.069] | no detectable difference |
| open(large) / settledMs | 993.2 [1007.0] | 1024.8 [1048.0] | 1.032 [1.004, 1.045] | slower |
| open(large) / mainTaskMs | 1070.4 [1089.0] | 1098.3 [1131.5] | 1.026 [1.010, 1.043] | no detectable difference |
| open(large) / mainJsMs | 914.2 [934.5] | 940.9 [965.5] | 1.029 [1.012, 1.047] | no detectable difference |
| scroll / settleMs | 8.9 [40.5] | 39.2 [89.2] | +30.3 [-12.6, +55.1] | no detectable difference |
| scroll / wallMs | 2882.7 [3018.8] | 2978.9 [3233.8] | 1.033 [1.001, 1.062] | slower |
| scroll / frameP50Ms | 12.4 [12.9] | 13.5 [14.6] | 1.089 [1.050, 1.146] | slower |
| scroll / frameP95Ms | 27.2 [27.2] | 28.1 [33.3] | 1.033 [1.005, 1.133] | slower |
| scroll / jankFrames | 0 [0.55] | 0.50 [1] | +0.50 [+0, +1] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 2360.8 [2406.5] | 2498.9 [2568.7] | 1.059 [1.045, 1.080] | slower |
| scroll / mainJsMs | 1985.9 [2007.8] | 2100.8 [2149.5] | 1.058 [1.042, 1.085] | slower |
| coldScroll / settleMs | 35.2 [61.0] | 81.7 [155.6] | +46.5 [+15.3, +78.7] | slower |
| coldScroll / wallMs | 2874.1 [3024.3] | 2962.3 [3193.7] | 1.031 [0.999, 1.085] | no detectable difference |
| coldScroll / frameP50Ms | 12.0 [12.9] | 14.3 [14.8] | 1.190 [1.113, 1.221] | slower |
| coldScroll / frameP95Ms | 27.0 [27.3] | 27.4 [32.8] | 1.014 [1.006, 1.137] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 1 [2] | +1 [+0, +2] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 2469.5 [2498.9] | 2601.7 [2704.2] | 1.054 [1.037, 1.077] | slower |
| coldScroll / mainJsMs | 2037.2 [2070.0] | 2168.3 [2265.7] | 1.064 [1.049, 1.086] | slower |
| jump / settleMs | 367.9 [398.0] | 391.7 [423.1] | +23.8 [-7.2, +66.2] | no detectable difference |
| jump / mainTaskMs | 510.9 [561.5] | 528.0 [635.6] | 1.034 [0.960, 1.109] | no detectable difference |
| jump / mainJsMs | 464.5 [505.9] | 475.5 [573.7] | 1.024 [0.947, 1.101] | no detectable difference |
| zoom / settleMs | 859.6 [893.8] | 915.3 [956.4] | +55.7 [+23.1, +80.0] | slower |
| zoom / zoom2SettleMs | 347.6 [366.2] | 368.7 [394.5] | +21.1 [-5.6, +47.4] | no detectable difference |
| zoom / zoom4SettleMs | 514.7 [539.5] | 546.6 [579.4] | +31.9 [+14.0, +49.4] | slower |
| zoom / backTo1SettleMs | 204.1 [214.5] | 210.0 [222.9] | +5.9 [-6.2, +15.5] | no detectable difference |
| zoom / mainTaskMs | 1305.5 [1351.6] | 1415.2 [1480.6] | 1.084 [1.061, 1.114] | slower |
| zoom / mainJsMs | 1118.5 [1156.5] | 1219.9 [1273.7] | 1.091 [1.066, 1.118] | slower |
| zoomLarge / settleMs | 694.8 [789.4] | 763.5 [830.9] | +68.6 [-30.3, +132.0] | no detectable difference |
| zoomLarge / zoom2SettleMs | 314.6 [363.5] | 346.9 [385.2] | +32.3 [-15.9, +65.5] | no detectable difference |
| zoomLarge / zoom4SettleMs | 384.4 [451.5] | 413.9 [473.4] | +29.5 [-32.6, +68.0] | no detectable difference |
| zoomLarge / backTo1SettleMs | 218.8 [234.9] | 240.5 [251.5] | +21.8 [+1.5, +26.6] | slower |
| zoomLarge / mainTaskMs | 1162.2 [1194.8] | 1279.9 [1411.1] | 1.101 [1.011, 1.187] | slower |
| zoomLarge / mainJsMs | 1007.2 [1036.6] | 1115.6 [1226.1] | 1.108 [1.022, 1.195] | slower |
| select / settleMs | 321.0 [350.7] | 331.9 [366.4] | +10.9 [-11.0, +72.9] | no detectable difference |
| select / mainTaskMs | 154.7 [164.9] | 170.9 [174.3] | 1.104 [1.059, 1.127] | slower |
| select / mainJsMs | 128.6 [133.7] | 137.6 [141.0] | 1.070 [1.035, 1.121] | slower |
| selectLarge / settleMs | 359.9 [401.6] | 391.2 [426.7] | +31.3 [+2.7, +59.3] | slower |
| selectLarge / mainTaskMs | 238.2 [254.3] | 242.9 [251.8] | 1.020 [0.978, 1.035] | no detectable difference |
| selectLarge / mainJsMs | 197.4 [210.2] | 198.2 [204.0] | 1.004 [0.969, 1.023] | no detectable difference |
| search / searchMs | 69.4 [71.2] | 69.0 [70.9] | 0.995 [0.984, 1.002] | no detectable difference |
| search / mainTaskMs | 33.2 [34.2] | 33.0 [35.5] | 0.992 [0.951, 1.058] | no detectable difference |
| search / mainJsMs | 23.1 [24.1] | 23.1 [23.6] | 1.001 [0.963, 1.036] | no detectable difference |
| searchLarge / searchMs | 3455.9 [3487.0] | 3475.6 [3511.5] | 1.006 [0.995, 1.011] | no detectable difference |
| searchLarge / mainTaskMs | 3436.9 [3471.3] | 3457.8 [3496.3] | 1.006 [0.995, 1.011] | no detectable difference |
| searchLarge / mainJsMs | 3366.2 [3395.4] | 3387.8 [3426.4] | 1.006 [0.997, 1.013] | no detectable difference |
| annotate / settleMs | 398.0 [422.1] | 414.9 [432.9] | +16.9 [+6.5, +69.3] | unreliable (A/A failed) |
| annotate / mainTaskMs | 255.1 [275.3] | 277.4 [281.7] | 1.087 [1.024, 1.120] | slower |
| annotate / mainJsMs | 192.0 [204.6] | 207.6 [211.6] | 1.081 [1.029, 1.139] | slower |
| annotateLarge / settleMs | 449.1 [475.7] | 463.3 [507.8] | +14.2 [-10.6, +53.2] | no detectable difference |
| annotateLarge / mainTaskMs | 337.0 [342.8] | 347.9 [352.9] | 1.032 [1.017, 1.047] | slower |
| annotateLarge / mainJsMs | 245.5 [249.8] | 253.2 [257.4] | 1.031 [1.011, 1.051] | slower |

## `main` vs `plugins-raf`

| Row | main median [p95] | plugins-raf median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 175.8 [178.1] | 316.1 [319.9] | 1.798 [1.785, 1.808] | slower |
| open(ebook) / openedMs | 315.4 [319.1] | 491.2 [494.3] | 1.557 [1.546, 1.567] | slower |
| open(ebook) / firstPaintMs | 551.3 [557.5] | 869.0 [885.9] | 1.576 [1.555, 1.623] | slower |
| open(ebook) / settledMs | 731.5 [743.6] | 973.7 [1019.6] | 1.331 [1.306, 1.373] | slower |
| open(ebook) / mainTaskMs | 548.8 [564.9] | 991.9 [1019.3] | 1.807 [1.760, 1.852] | slower |
| open(ebook) / mainJsMs | 435.9 [445.4] | 825.9 [848.3] | 1.895 [1.839, 1.942] | slower |
| open(large) / readyMs | 175.7 [177.2] | 318.4 [320.9] | 1.813 [1.802, 1.824] | slower |
| open(large) / openedMs | 374.8 [379.2] | 578.2 [582.5] | 1.543 [1.527, 1.549] | slower |
| open(large) / firstPaintMs | 601.3 [616.9] | 992.5 [1062.8] | 1.650 [1.605, 1.712] | slower |
| open(large) / settledMs | 615.9 [627.9] | 1024.8 [1048.0] | 1.664 [1.617, 1.686] | slower |
| open(large) / mainTaskMs | 795.7 [807.8] | 1098.3 [1131.5] | 1.380 [1.358, 1.412] | slower |
| open(large) / mainJsMs | 690.5 [698.8] | 940.9 [965.5] | 1.363 [1.342, 1.393] | slower |
| scroll / settleMs | 0.0 [0.0] | 39.2 [89.2] | +39.2 [+2.2, +67.5] | slower |
| scroll / wallMs | 3083.5 [3180.8] | 2978.9 [3233.8] | 0.966 [0.947, 0.993] | faster |
| scroll / frameP50Ms | 12.0 [12.3] | 13.5 [14.6] | 1.126 [1.077, 1.193] | slower |
| scroll / frameP95Ms | 26.0 [26.3] | 28.1 [33.3] | 1.081 [1.050, 1.183] | slower |
| scroll / jankFrames | 0 [0] | 0.50 [1] | +0.50 [+0, +1] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 1294.1 [1310.4] | 2498.9 [2568.7] | 1.931 [1.912, 1.972] | slower |
| scroll / mainJsMs | 959.0 [983.1] | 2100.8 [2149.5] | 2.191 [2.149, 2.235] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 81.7 [155.6] | +81.7 [+53.2, +112.6] | slower |
| coldScroll / wallMs | 3176.8 [3290.8] | 2962.3 [3193.7] | 0.932 [0.909, 0.973] | faster |
| coldScroll / frameP50Ms | 12.0 [12.5] | 14.3 [14.8] | 1.192 [1.125, 1.236] | slower |
| coldScroll / frameP95Ms | 26.0 [26.5] | 27.4 [32.8] | 1.051 [1.045, 1.177] | slower |
| coldScroll / jankFrames | 0 [0] | 1 [2] | +1 [+0, +2] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 1527.0 [1567.4] | 2601.7 [2704.2] | 1.704 [1.670, 1.756] | slower |
| coldScroll / mainJsMs | 1125.2 [1163.7] | 2168.3 [2265.7] | 1.927 [1.891, 1.978] | slower |
| jump / settleMs | 195.6 [213.7] | 391.7 [423.1] | +196.1 [+174.7, +217.0] | slower |
| jump / mainTaskMs | 226.4 [250.6] | 528.0 [635.6] | 2.332 [2.132, 2.497] | slower |
| jump / mainJsMs | 177.2 [189.4] | 475.5 [573.7] | 2.684 [2.477, 2.893] | slower |
| zoom / settleMs | 630.8 [662.0] | 915.3 [956.4] | +284.5 [+256.5, +321.5] | slower |
| zoom / zoom2SettleMs | 260.1 [282.9] | 368.7 [394.5] | +108.6 [+79.8, +131.1] | slower |
| zoom / zoom4SettleMs | 357.0 [385.1] | 546.6 [579.4] | +189.5 [+164.6, +200.8] | slower |
| zoom / backTo1SettleMs | 155.6 [159.3] | 210.0 [222.9] | +54.4 [+46.0, +63.4] | slower |
| zoom / mainTaskMs | 761.2 [820.3] | 1415.2 [1480.6] | 1.859 [1.758, 1.938] | slower |
| zoom / mainJsMs | 564.8 [620.7] | 1219.9 [1273.7] | 2.160 [1.998, 2.234] | slower |
| zoomLarge / settleMs | 399.4 [420.6] | 763.5 [830.9] | +364.1 [+271.4, +415.5] | slower |
| zoomLarge / zoom2SettleMs | 178.8 [188.9] | 346.9 [385.2] | +168.1 [+128.1, +192.4] | slower |
| zoomLarge / zoom4SettleMs | 237.4 [245.0] | 413.9 [473.4] | +176.5 [+126.4, +235.8] | slower |
| zoomLarge / backTo1SettleMs | 158.5 [174.7] | 240.5 [251.5] | +82.0 [+63.0, +88.5] | slower |
| zoomLarge / mainTaskMs | 516.3 [540.2] | 1279.9 [1411.1] | 2.479 [2.286, 2.700] | slower |
| zoomLarge / mainJsMs | 393.0 [408.5] | 1115.6 [1226.1] | 2.838 [2.629, 3.114] | slower |
| select / settleMs | 223.4 [262.7] | 331.9 [366.4] | +108.5 [+84.0, +141.5] | slower |
| select / mainTaskMs | 86.0 [88.2] | 170.9 [174.3] | 1.988 [1.938, 2.029] | slower |
| select / mainJsMs | 56.3 [58.4] | 137.6 [141.0] | 2.442 [2.381, 2.532] | slower |
| selectLarge / settleMs | 237.0 [273.5] | 391.2 [426.7] | +154.2 [+142.5, +184.8] | slower |
| selectLarge / mainTaskMs | 97.4 [99.5] | 242.9 [251.8] | 2.495 [2.422, 2.552] | slower |
| selectLarge / mainJsMs | 67.6 [69.5] | 198.2 [204.0] | 2.931 [2.809, 3.007] | slower |
| search / searchMs | 60.5 [61.0] | 69.0 [70.9] | 1.141 [1.136, 1.158] | slower |
| search / mainTaskMs | 17.7 [19.0] | 33.0 [35.5] | 1.859 [1.729, 2.060] | slower |
| search / mainJsMs | 12.3 [13.1] | 23.1 [23.6] | 1.879 [1.772, 1.941] | slower |
| searchLarge / searchMs | 2635.8 [2896.2] | 3475.6 [3511.5] | 1.319 [1.238, 1.338] | slower |
| searchLarge / mainTaskMs | 1206.7 [1437.2] | 3457.8 [3496.3] | 2.866 [2.451, 2.901] | slower |
| searchLarge / mainJsMs | 1070.8 [1295.2] | 3387.8 [3426.4] | 3.164 [2.681, 3.220] | slower |
| annotate / settleMs | 309.5 [333.1] | 414.9 [432.9] | +105.4 [+90.0, +130.7] | unreliable (A/A failed) |
| annotate / mainTaskMs | 162.3 [164.9] | 277.4 [281.7] | 1.709 [1.679, 1.729] | slower |
| annotate / mainJsMs | 99.2 [102.3] | 207.6 [211.6] | 2.092 [2.054, 2.149] | slower |
| annotateLarge / settleMs | 280.4 [298.9] | 463.3 [507.8] | +183.0 [+164.9, +221.0] | slower |
| annotateLarge / mainTaskMs | 164.4 [166.5] | 347.9 [352.9] | 2.116 [2.092, 2.166] | slower |
| annotateLarge / mainJsMs | 100.7 [103.6] | 253.2 [257.4] | 2.514 [2.462, 2.563] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-coalesce median [p95] | plugins-raf median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 35.36 MiB [36.36 MiB] | 35.86 MiB [37.38 MiB] | 38.37 MiB [39.98 MiB] | 37.88 MiB [38.89 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 47.75 MiB [47.85 MiB] | 47.84 MiB [47.87 MiB] | 52.13 MiB [52.15 MiB] | 52.13 MiB [52.16 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.21 MiB [22.21 MiB] | 22.21 MiB [22.21 MiB] | 23.05 MiB [23.05 MiB] | 23.05 MiB [23.05 MiB] |
| soak / heapCycle5Bytes | 38.43 MiB [39.44 MiB] | 38.43 MiB [39.44 MiB] | 40.90 MiB [41.91 MiB] | 40.84 MiB [41.93 MiB] |
| soak / heapCycle30Bytes | 39.27 MiB [40.28 MiB] | 39.27 MiB [40.28 MiB] | 41.30 MiB [42.30 MiB] | 41.30 MiB [42.32 MiB] |
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

`plugins-raf`:

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

