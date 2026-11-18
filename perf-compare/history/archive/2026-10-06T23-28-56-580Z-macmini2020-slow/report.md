# Viewer perf comparison: 2026-10-06T23-28-56-580Z-macmini2020-slow

10 rounds, profile `slow` (CPU throttle 4x).

The throttle reached the main thread only: Chromium refuses `Emulation.setCPUThrottlingRate` on the engine worker, so worker-side work ran at full speed.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 61027635dbe6511c6053f5660e76acaef4a19a29 |
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
| plugins-tip5 | research/multi-platform-rust-core-impl-plugins | a8252d0ea | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | aee9b9460008 |
| plugins-coalesce | research/rust-kernel-coalesce-notify | c0fcd2746 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | aee9b9460008 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-tip5`: pass
- `plugins-coalesce`: pass

10 samples failed and are left out:

- plugins-tip5 selectLarge(large): the drag selected no text (x10)

## Sizes

| Metric | main | main-aa | plugins-tip5 | plugins-coalesce |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 8.04 MiB | 8.04 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.72 MiB | 1.72 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

61 of 63 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 176.3 [177.4] | 175.8 [178.1] | 0.997 [0.990, 1.005] | pass |
| open(ebook) / openedMs | 316.2 [319.1] | 314.7 [317.9] | 0.995 [0.986, 1.005] | pass |
| open(ebook) / firstPaintMs | 543.1 [557.3] | 554.8 [566.8] | 1.022 [1.000, 1.035] | pass |
| open(ebook) / settledMs | 732.8 [756.1] | 729.3 [737.9] | 0.995 [0.977, 1.016] | pass |
| open(ebook) / mainTaskMs | 553.2 [568.9] | 551.9 [560.8] | 0.998 [0.973, 1.037] | pass |
| open(ebook) / mainJsMs | 436.7 [455.4] | 435.3 [449.2] | 0.997 [0.961, 1.045] | pass |
| open(large) / readyMs | 176.6 [177.6] | 176.4 [177.7] | 0.999 [0.994, 1.005] | pass |
| open(large) / openedMs | 374.3 [378.7] | 373.6 [375.6] | 0.998 [0.989, 1.005] | pass |
| open(large) / firstPaintMs | 593.6 [614.7] | 587.2 [605.9] | 0.989 [0.966, 1.005] | pass |
| open(large) / settledMs | 608.4 [618.8] | 614.1 [620.6] | 1.009 [0.996, 1.024] | pass |
| open(large) / mainTaskMs | 781.2 [797.2] | 791.7 [799.7] | 1.013 [0.995, 1.031] | pass |
| open(large) / mainJsMs | 677.4 [693.3] | 687.6 [695.2] | 1.015 [1.003, 1.030] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 3099.1 [3148.4] | 3125.5 [3178.3] | 1.009 [0.984, 1.029] | pass |
| scroll / frameP50Ms | 12.2 [12.5] | 12.0 [12.3] | 0.981 [0.959, 1.003] | pass |
| scroll / frameP95Ms | 26.0 [26.1] | 25.9 [26.1] | 0.996 [0.993, 1.003] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 1289.1 [1332.5] | 1308.5 [1349.8] | 1.015 [0.992, 1.042] | pass |
| scroll / mainJsMs | 952.8 [970.4] | 977.1 [989.6] | 1.026 [0.998, 1.034] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 3225.3 [3293.5] | 3161.4 [3223.2] | 0.980 [0.966, 0.991] | pass |
| coldScroll / frameP50Ms | 12.0 [12.2] | 12.2 [12.5] | 1.014 [0.991, 1.031] | pass |
| coldScroll / frameP95Ms | 25.9 [26.4] | 26.0 [26.6] | 1.004 [0.994, 1.013] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 1512.1 [1547.9] | 1528.3 [1555.1] | 1.011 [0.989, 1.033] | pass |
| coldScroll / mainJsMs | 1121.7 [1138.7] | 1120.5 [1157.4] | 0.999 [0.983, 1.034] | pass |
| jump / settleMs | 185.4 [210.8] | 197.9 [215.4] | +12.5 [-4.4, +22.1] | pass |
| jump / mainTaskMs | 227.8 [244.2] | 242.9 [251.3] | 1.066 [0.964, 1.137] | pass |
| jump / mainJsMs | 169.7 [187.3] | 178.9 [197.4] | 1.054 [0.964, 1.107] | pass |
| zoom / settleMs | 633.5 [655.6] | 614.6 [640.8] | -18.9 [-32.2, +13.2] | pass |
| zoom / zoom2SettleMs | 266.0 [278.1] | 260.1 [276.4] | -5.9 [-21.1, +9.5] | pass |
| zoom / zoom4SettleMs | 361.1 [388.8] | 356.1 [382.0] | -5.0 [-21.2, +13.9] | pass |
| zoom / backTo1SettleMs | 154.1 [160.9] | 155.0 [159.8] | +1.0 [-3.1, +5.5] | pass |
| zoom / mainTaskMs | 766.2 [806.1] | 756.4 [787.4] | 0.987 [0.948, 1.033] | pass |
| zoom / mainJsMs | 578.2 [610.4] | 567.9 [595.6] | 0.982 [0.952, 1.030] | pass |
| zoomLarge / settleMs | 370.3 [396.9] | 389.7 [419.3] | +19.5 [-0.5, +36.6] | pass |
| zoomLarge / zoom2SettleMs | 177.7 [189.7] | 172.2 [197.8] | -5.5 [-18.5, +15.0] | pass |
| zoomLarge / zoom4SettleMs | 200.8 [212.7] | 217.9 [238.1] | +17.1 [-5.1, +40.7] | pass |
| zoomLarge / backTo1SettleMs | 156.4 [176.3] | 156.2 [169.8] | -0.2 [-15.2, +9.6] | pass |
| zoomLarge / mainTaskMs | 480.9 [530.2] | 504.8 [549.9] | 1.050 [1.026, 1.096] | A/A failed |
| zoomLarge / mainJsMs | 361.8 [404.6] | 387.3 [421.7] | 1.071 [1.030, 1.098] | A/A failed |
| select / settleMs | 238.6 [258.6] | 220.2 [244.7] | -18.5 [-35.4, +0.7] | pass |
| select / mainTaskMs | 86.8 [89.1] | 86.7 [88.6] | 0.999 [0.982, 1.031] | pass |
| select / mainJsMs | 56.5 [57.8] | 56.9 [58.4] | 1.007 [0.991, 1.030] | pass |
| selectLarge / settleMs | 229.5 [258.1] | 253.9 [276.9] | +24.4 [-5.9, +37.8] | pass |
| selectLarge / mainTaskMs | 97.5 [104.1] | 98.4 [104.7] | 1.009 [0.965, 1.042] | pass |
| selectLarge / mainJsMs | 67.5 [71.4] | 68.9 [72.1] | 1.020 [0.988, 1.060] | pass |
| search / searchMs | 60.6 [61.1] | 60.3 [60.9] | 0.996 [0.987, 1.004] | pass |
| search / mainTaskMs | 16.6 [19.2] | 16.6 [19.2] | 0.998 [0.906, 1.112] | pass |
| search / mainJsMs | 12.2 [12.9] | 12.2 [13.5] | 0.995 [0.942, 1.061] | pass |
| searchLarge / searchMs | 2634.0 [2854.2] | 2644.0 [2839.0] | 1.004 [0.923, 1.030] | pass |
| searchLarge / mainTaskMs | 1225.9 [1423.5] | 1210.5 [1434.0] | 0.987 [0.858, 1.027] | pass |
| searchLarge / mainJsMs | 1078.3 [1275.2] | 1070.3 [1291.3] | 0.993 [0.846, 1.024] | pass |
| annotate / settleMs | 295.5 [306.8] | 284.9 [318.2] | -10.6 [-23.8, +14.4] | pass |
| annotate / mainTaskMs | 163.6 [168.8] | 165.0 [168.2] | 1.008 [0.979, 1.029] | pass |
| annotate / mainJsMs | 98.3 [103.7] | 99.4 [102.0] | 1.010 [0.983, 1.032] | pass |
| annotateLarge / settleMs | 274.1 [300.2] | 272.3 [302.0] | -1.8 [-21.3, +23.1] | pass |
| annotateLarge / mainTaskMs | 162.3 [166.4] | 162.3 [165.7] | 1.000 [0.985, 1.017] | pass |
| annotateLarge / mainJsMs | 101.3 [104.0] | 101.2 [103.3] | 1.000 [0.977, 1.015] | pass |

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

Not run: `plugins-geomdecode`.

## `main` vs `plugins-geomdecode`

Not run: `plugins-geomdecode`.

## `plugins-tip5` vs `plugins-coalesce`

| Row | plugins-tip5 median [p95] | plugins-coalesce median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 318.2 [323.0] | 317.5 [323.9] | 0.998 [0.987, 1.008] | no detectable difference |
| open(ebook) / openedMs | 492.3 [497.7] | 492.9 [497.5] | 1.001 [0.993, 1.009] | no detectable difference |
| open(ebook) / firstPaintMs | 881.5 [903.6] | 857.1 [877.9] | 0.972 [0.951, 0.985] | no detectable difference |
| open(ebook) / settledMs | 990.7 [1011.4] | 968.1 [975.7] | 0.977 [0.952, 0.989] | no detectable difference |
| open(ebook) / mainTaskMs | 993.5 [1034.1] | 956.2 [981.0] | 0.962 [0.935, 0.992] | faster |
| open(ebook) / mainJsMs | 824.6 [865.1] | 788.5 [811.9] | 0.956 [0.929, 0.981] | no detectable difference |
| open(large) / readyMs | 320.2 [322.9] | 317.0 [320.4] | 0.990 [0.982, 1.010] | no detectable difference |
| open(large) / openedMs | 578.8 [583.6] | 577.2 [581.5] | 0.997 [0.990, 1.007] | no detectable difference |
| open(large) / firstPaintMs | 986.0 [1030.5] | 958.8 [1014.9] | 0.972 [0.962, 0.999] | no detectable difference |
| open(large) / settledMs | 1026.8 [1036.9] | 991.5 [1001.5] | 0.966 [0.957, 0.973] | faster |
| open(large) / mainTaskMs | 1209.0 [1221.8] | 1066.8 [1073.1] | 0.882 [0.873, 0.890] | faster |
| open(large) / mainJsMs | 1049.7 [1065.6] | 914.2 [919.8] | 0.871 [0.858, 0.877] | faster |
| scroll / settleMs | 0.0 [0.0] | 28.2 [54.9] | +28.2 [+4.6, +38.5] | slower |
| scroll / wallMs | 4190.8 [4220.0] | 2857.9 [3020.2] | 0.682 [0.672, 0.701] | faster |
| scroll / frameP50Ms | 13.7 [14.4] | 12.4 [12.9] | 0.907 [0.861, 0.925] | faster |
| scroll / frameP95Ms | 29.6 [33.1] | 26.5 [27.2] | 0.894 [0.817, 0.973] | faster |
| scroll / jankFrames | 1 [2] | 0 [0] | -1 [-2, +0] | no detectable difference |
| scroll / longTasks | 0 [0.55] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [30.2] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 3092.0 [3224.4] | 2348.3 [2385.7] | 0.759 [0.738, 0.779] | faster |
| scroll / mainJsMs | 2662.3 [2770.7] | 1967.3 [1995.1] | 0.739 [0.717, 0.755] | faster |
| coldScroll / settleMs | 0.0 [24.9] | 36.6 [75.1] | +36.6 [+20.9, +55.1] | slower |
| coldScroll / wallMs | 4067.4 [4125.6] | 2803.0 [2999.7] | 0.689 [0.680, 0.730] | faster |
| coldScroll / frameP50Ms | 13.6 [14.4] | 12.3 [12.9] | 0.907 [0.877, 0.927] | faster |
| coldScroll / frameP95Ms | 27.5 [34.1] | 26.9 [27.2] | 0.978 [0.814, 0.991] | no detectable difference |
| coldScroll / jankFrames | 2 [3] | 0 [0.55] | -2 [-3, -1] | faster |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 3927.6 [3986.9] | 2474.7 [2511.7] | 0.630 [0.616, 0.642] | faster |
| coldScroll / mainJsMs | 3484.4 [3518.0] | 2034.6 [2079.9] | 0.584 [0.574, 0.598] | faster |
| jump / settleMs | 311.6 [385.2] | 351.7 [383.0] | +40.1 [+1.0, +77.9] | slower |
| jump / mainTaskMs | 558.6 [635.0] | 508.7 [544.9] | 0.911 [0.818, 0.984] | no detectable difference |
| jump / mainJsMs | 495.2 [564.1] | 462.4 [495.3] | 0.934 [0.838, 0.998] | no detectable difference |
| zoom / settleMs | 898.7 [917.2] | 837.2 [868.2] | -61.5 [-81.2, -37.6] | faster |
| zoom / zoom2SettleMs | 375.7 [382.4] | 332.6 [358.6] | -43.1 [-59.8, -25.9] | faster |
| zoom / zoom4SettleMs | 522.2 [547.7] | 501.9 [529.2] | -20.3 [-34.3, -0.9] | no detectable difference |
| zoom / backTo1SettleMs | 200.5 [212.3] | 206.7 [214.9] | +6.2 [-2.6, +8.2] | no detectable difference |
| zoom / mainTaskMs | 1352.0 [1436.8] | 1285.2 [1331.2] | 0.951 [0.912, 0.978] | no detectable difference |
| zoom / mainJsMs | 1156.3 [1241.5] | 1100.3 [1143.4] | 0.952 [0.915, 0.974] | faster |
| zoomLarge / settleMs | 787.1 [860.8] | 690.2 [750.2] | -96.9 [-175.0, -32.5] | faster |
| zoomLarge / zoom2SettleMs | 343.8 [375.0] | 298.1 [334.8] | -45.7 [-68.1, -16.0] | faster |
| zoomLarge / zoom4SettleMs | 441.6 [503.6] | 398.0 [433.8] | -43.6 [-129.5, +5.3] | no detectable difference |
| zoomLarge / backTo1SettleMs | 261.6 [271.0] | 216.9 [230.8] | -44.7 [-50.7, -24.3] | faster |
| zoomLarge / mainTaskMs | 1792.1 [1874.5] | 1126.3 [1209.8] | 0.628 [0.589, 0.672] | unreliable (A/A failed) |
| zoomLarge / mainJsMs | 1626.0 [1699.9] | 976.0 [1048.5] | 0.600 [0.566, 0.639] | unreliable (A/A failed) |
| select / settleMs | 333.6 [358.2] | 313.0 [366.9] | -20.6 [-41.1, +37.5] | no detectable difference |
| select / mainTaskMs | 168.6 [174.2] | 157.9 [166.2] | 0.937 [0.909, 0.978] | faster |
| select / mainJsMs | 137.7 [140.8] | 128.6 [137.4] | 0.934 [0.916, 0.965] | faster |
| search / searchMs | 69.2 [72.2] | 69.4 [70.8] | 1.003 [0.989, 1.013] | no detectable difference |
| search / mainTaskMs | 32.9 [35.0] | 32.7 [34.4] | 0.995 [0.972, 1.027] | no detectable difference |
| search / mainJsMs | 23.0 [25.2] | 23.0 [23.8] | 0.999 [0.969, 1.026] | no detectable difference |
| searchLarge / searchMs | 4592.8 [4636.7] | 3470.2 [3571.0] | 0.756 [0.749, 0.762] | faster |
| searchLarge / mainTaskMs | 4567.9 [4614.6] | 3456.6 [3555.9] | 0.757 [0.750, 0.764] | faster |
| searchLarge / mainJsMs | 4461.2 [4510.1] | 3376.8 [3482.1] | 0.757 [0.751, 0.765] | faster |
| annotate / settleMs | 407.5 [428.7] | 386.5 [417.2] | -21.0 [-36.2, +8.9] | no detectable difference |
| annotate / mainTaskMs | 275.8 [278.1] | 259.3 [268.7] | 0.940 [0.909, 0.973] | faster |
| annotate / mainJsMs | 205.9 [209.3] | 197.3 [200.8] | 0.958 [0.903, 0.974] | faster |
| annotateLarge / settleMs | 504.4 [519.4] | 463.6 [500.5] | -40.8 [-58.4, -14.3] | faster |
| annotateLarge / mainTaskMs | 373.2 [382.9] | 335.5 [344.0] | 0.899 [0.882, 0.913] | faster |
| annotateLarge / mainJsMs | 274.4 [276.3] | 243.2 [248.8] | 0.886 [0.855, 0.909] | faster |

## `main` vs `plugins-coalesce`

| Row | main median [p95] | plugins-coalesce median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 176.3 [177.4] | 317.5 [323.9] | 1.801 [1.789, 1.818] | slower |
| open(ebook) / openedMs | 316.2 [319.1] | 492.9 [497.5] | 1.559 [1.547, 1.576] | slower |
| open(ebook) / firstPaintMs | 543.1 [557.3] | 857.1 [877.9] | 1.578 [1.538, 1.597] | slower |
| open(ebook) / settledMs | 732.8 [756.1] | 968.1 [975.7] | 1.321 [1.287, 1.346] | slower |
| open(ebook) / mainTaskMs | 553.2 [568.9] | 956.2 [981.0] | 1.729 [1.683, 1.807] | slower |
| open(ebook) / mainJsMs | 436.7 [455.4] | 788.5 [811.9] | 1.806 [1.744, 1.887] | slower |
| open(large) / readyMs | 176.6 [177.6] | 317.0 [320.4] | 1.795 [1.787, 1.814] | slower |
| open(large) / openedMs | 374.3 [378.7] | 577.2 [581.5] | 1.542 [1.528, 1.554] | slower |
| open(large) / firstPaintMs | 593.6 [614.7] | 958.8 [1014.9] | 1.615 [1.579, 1.661] | slower |
| open(large) / settledMs | 608.4 [618.8] | 991.5 [1001.5] | 1.630 [1.613, 1.654] | slower |
| open(large) / mainTaskMs | 781.2 [797.2] | 1066.8 [1073.1] | 1.366 [1.344, 1.387] | slower |
| open(large) / mainJsMs | 677.4 [693.3] | 914.2 [919.8] | 1.350 [1.330, 1.368] | slower |
| scroll / settleMs | 0.0 [0.0] | 28.2 [54.9] | +28.2 [+4.6, +38.5] | slower |
| scroll / wallMs | 3099.1 [3148.4] | 2857.9 [3020.2] | 0.922 [0.908, 0.949] | faster |
| scroll / frameP50Ms | 12.2 [12.5] | 12.4 [12.9] | 1.020 [0.974, 1.043] | no detectable difference |
| scroll / frameP95Ms | 26.0 [26.1] | 26.5 [27.2] | 1.020 [1.006, 1.044] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 1289.1 [1332.5] | 2348.3 [2385.7] | 1.822 [1.778, 1.850] | slower |
| scroll / mainJsMs | 952.8 [970.4] | 1967.3 [1995.1] | 2.065 [2.027, 2.087] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 36.6 [75.1] | +36.6 [+20.9, +55.1] | slower |
| coldScroll / wallMs | 3225.3 [3293.5] | 2803.0 [2999.7] | 0.869 [0.854, 0.922] | faster |
| coldScroll / frameP50Ms | 12.0 [12.2] | 12.3 [12.9] | 1.023 [0.995, 1.039] | no detectable difference |
| coldScroll / frameP95Ms | 25.9 [26.4] | 26.9 [27.2] | 1.037 [1.020, 1.049] | slower |
| coldScroll / jankFrames | 0 [0] | 0 [0.55] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 1512.1 [1547.9] | 2474.7 [2511.7] | 1.637 [1.591, 1.656] | slower |
| coldScroll / mainJsMs | 1121.7 [1138.7] | 2034.6 [2079.9] | 1.814 [1.780, 1.868] | slower |
| jump / settleMs | 185.4 [210.8] | 351.7 [383.0] | +166.3 [+142.7, +179.9] | slower |
| jump / mainTaskMs | 227.8 [244.2] | 508.7 [544.9] | 2.233 [2.059, 2.399] | slower |
| jump / mainJsMs | 169.7 [187.3] | 462.4 [495.3] | 2.724 [2.498, 2.826] | slower |
| zoom / settleMs | 633.5 [655.6] | 837.2 [868.2] | +203.7 [+182.9, +238.4] | slower |
| zoom / zoom2SettleMs | 266.0 [278.1] | 332.6 [358.6] | +66.6 [+48.3, +85.3] | slower |
| zoom / zoom4SettleMs | 361.1 [388.8] | 501.9 [529.2] | +140.8 [+121.9, +161.5] | slower |
| zoom / backTo1SettleMs | 154.1 [160.9] | 206.7 [214.9] | +52.6 [+46.9, +54.5] | slower |
| zoom / mainTaskMs | 766.2 [806.1] | 1285.2 [1331.2] | 1.677 [1.610, 1.744] | slower |
| zoom / mainJsMs | 578.2 [610.4] | 1100.3 [1143.4] | 1.903 [1.839, 1.982] | slower |
| zoomLarge / settleMs | 370.3 [396.9] | 690.2 [750.2] | +319.9 [+281.6, +364.3] | slower |
| zoomLarge / zoom2SettleMs | 177.7 [189.7] | 298.1 [334.8] | +120.4 [+107.7, +141.8] | slower |
| zoomLarge / zoom4SettleMs | 200.8 [212.7] | 398.0 [433.8] | +197.2 [+144.9, +233.5] | slower |
| zoomLarge / backTo1SettleMs | 156.4 [176.3] | 216.9 [230.8] | +60.6 [+46.4, +68.9] | slower |
| zoomLarge / mainTaskMs | 480.9 [530.2] | 1126.3 [1209.8] | 2.342 [2.201, 2.483] | unreliable (A/A failed) |
| zoomLarge / mainJsMs | 361.8 [404.6] | 976.0 [1048.5] | 2.698 [2.529, 2.845] | unreliable (A/A failed) |
| select / settleMs | 238.6 [258.6] | 313.0 [366.9] | +74.4 [+58.2, +121.5] | slower |
| select / mainTaskMs | 86.8 [89.1] | 157.9 [166.2] | 1.820 [1.766, 1.916] | slower |
| select / mainJsMs | 56.5 [57.8] | 128.6 [137.4] | 2.274 [2.235, 2.356] | slower |
| selectLarge / settleMs | 229.5 [258.1] | 401.3 [434.6] | +171.8 [+138.4, +196.8] | slower |
| selectLarge / mainTaskMs | 97.5 [104.1] | 250.9 [264.0] | 2.573 [2.419, 2.625] | slower |
| selectLarge / mainJsMs | 67.5 [71.4] | 205.3 [211.1] | 3.040 [2.919, 3.164] | slower |
| search / searchMs | 60.6 [61.1] | 69.4 [70.8] | 1.145 [1.133, 1.156] | slower |
| search / mainTaskMs | 16.6 [19.2] | 32.7 [34.4] | 1.966 [1.810, 2.094] | slower |
| search / mainJsMs | 12.2 [12.9] | 23.0 [23.8] | 1.884 [1.836, 1.939] | slower |
| searchLarge / searchMs | 2634.0 [2854.2] | 3470.2 [3571.0] | 1.317 [1.221, 1.341] | slower |
| searchLarge / mainTaskMs | 1225.9 [1423.5] | 3456.6 [3555.9] | 2.820 [2.446, 2.910] | slower |
| searchLarge / mainJsMs | 1078.3 [1275.2] | 3376.8 [3482.1] | 3.132 [2.672, 3.212] | slower |
| annotate / settleMs | 295.5 [306.8] | 386.5 [417.2] | +91.0 [+71.7, +126.7] | slower |
| annotate / mainTaskMs | 163.6 [168.8] | 259.3 [268.7] | 1.585 [1.530, 1.639] | slower |
| annotate / mainJsMs | 98.3 [103.7] | 197.3 [200.8] | 2.007 [1.888, 2.050] | slower |
| annotateLarge / settleMs | 274.1 [300.2] | 463.6 [500.5] | +189.5 [+164.4, +219.4] | slower |
| annotateLarge / mainTaskMs | 162.3 [166.4] | 335.5 [344.0] | 2.067 [2.027, 2.095] | slower |
| annotateLarge / mainJsMs | 101.3 [104.0] | 243.2 [248.8] | 2.401 [2.308, 2.447] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-tip5 median [p95] | plugins-coalesce median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 35.36 MiB [36.36 MiB] | 35.35 MiB [35.36 MiB] | 36.86 MiB [36.87 MiB] | 37.86 MiB [38.87 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 47.81 MiB [47.93 MiB] | 47.79 MiB [48.00 MiB] | 52.33 MiB [52.35 MiB] | 52.14 MiB [52.16 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.21 MiB [22.21 MiB] | 22.21 MiB [22.21 MiB] | 22.96 MiB [22.96 MiB] | 23.04 MiB [23.05 MiB] |
| soak / heapCycle5Bytes | 38.43 MiB [39.45 MiB] | 38.42 MiB [39.44 MiB] | 39.90 MiB [41.85 MiB] | 40.32 MiB [40.91 MiB] |
| soak / heapCycle30Bytes | 39.27 MiB [40.27 MiB] | 39.27 MiB [40.28 MiB] | 40.28 MiB [42.30 MiB] | 40.78 MiB [41.30 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-tip5`:

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

