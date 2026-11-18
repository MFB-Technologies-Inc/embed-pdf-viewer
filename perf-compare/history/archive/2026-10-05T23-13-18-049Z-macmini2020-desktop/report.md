# Viewer perf comparison: 2026-10-05T23-13-18-049Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | f824a683230ac3ed0aa0a3766d26b976a00cf36b |
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
| plugins-fixes | research/multi-platform-rust-core-impl-plugins | adda0bbea | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 4f7659f6fba3 |
| plugins-encode | research/rust-kernel-bridge-encode | 57f627ed5 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 4f7659f6fba3 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-fixes`: pass
- `plugins-encode`: pass

## Sizes

| Metric | main | main-aa | plugins-fixes | plugins-encode |
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
| open(ebook) / readyMs | 63.2 [67.5] | 63.2 [67.7] | 1.000 [0.963, 1.042] | pass |
| open(ebook) / openedMs | 138.1 [152.8] | 137.4 [142.5] | 0.995 [0.926, 1.030] | pass |
| open(ebook) / firstPaintMs | 257.4 [280.6] | 256.1 [281.4] | 0.995 [0.947, 1.039] | pass |
| open(ebook) / settledMs | 397.1 [418.1] | 397.6 [409.8] | 1.001 [0.963, 1.019] | pass |
| open(ebook) / mainTaskMs | 145.7 [151.1] | 144.4 [154.4] | 0.991 [0.972, 1.030] | pass |
| open(ebook) / mainJsMs | 111.5 [116.3] | 111.5 [119.6] | 1.000 [0.968, 1.032] | pass |
| open(large) / readyMs | 63.3 [68.7] | 62.2 [65.0] | 0.982 [0.930, 1.034] | pass |
| open(large) / openedMs | 159.0 [166.7] | 157.4 [163.1] | 0.990 [0.957, 1.014] | pass |
| open(large) / firstPaintMs | 275.4 [293.3] | 269.0 [278.6] | 0.977 [0.948, 1.012] | pass |
| open(large) / settledMs | 272.7 [284.2] | 270.7 [279.4] | 0.993 [0.968, 1.017] | pass |
| open(large) / mainTaskMs | 315.9 [341.9] | 314.2 [338.4] | 0.994 [0.951, 1.041] | pass |
| open(large) / mainJsMs | 285.5 [308.3] | 283.8 [306.5] | 0.994 [0.955, 1.032] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2873.1 [2980.1] | 2842.6 [2935.4] | 0.989 [0.968, 1.016] | pass |
| scroll / frameP50Ms | 11.3 [12.0] | 11.0 [11.4] | 0.976 [0.931, 1.010] | pass |
| scroll / frameP95Ms | 26.7 [27.3] | 24.8 [26.8] | 0.929 [0.808, 0.977] | A/A failed |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 527.9 [550.4] | 534.7 [550.9] | 1.013 [0.959, 1.057] | pass |
| scroll / mainJsMs | 395.6 [406.8] | 399.0 [405.9] | 1.009 [0.968, 1.041] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2825.7 [2960.1] | 2858.5 [2927.3] | 1.012 [0.983, 1.034] | pass |
| coldScroll / frameP50Ms | 11.2 [12.0] | 11.5 [11.8] | 1.026 [0.968, 1.066] | pass |
| coldScroll / frameP95Ms | 26.4 [27.2] | 25.0 [27.2] | 0.947 [0.880, 1.035] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 552.2 [577.1] | 553.0 [568.4] | 1.001 [0.965, 1.042] | pass |
| coldScroll / mainJsMs | 383.7 [404.0] | 385.4 [395.3] | 1.005 [0.966, 1.039] | pass |
| jump / settleMs | 119.1 [133.6] | 127.9 [134.6] | +8.8 [-1.7, +19.6] | pass |
| jump / mainTaskMs | 72.3 [79.2] | 72.0 [76.1] | 0.996 [0.928, 1.069] | pass |
| jump / mainJsMs | 54.9 [57.2] | 54.2 [56.8] | 0.987 [0.926, 1.100] | pass |
| zoom / settleMs | 482.0 [510.5] | 480.3 [496.8] | -1.6 [-24.2, +13.8] | pass |
| zoom / zoom2SettleMs | 163.1 [167.5] | 163.4 [169.5] | +0.3 [-9.7, +7.5] | pass |
| zoom / zoom4SettleMs | 319.8 [346.5] | 313.6 [340.9] | -6.2 [-24.1, +7.7] | pass |
| zoom / backTo1SettleMs | 88.1 [107.7] | 88.5 [95.7] | +0.4 [-13.7, +5.9] | pass |
| zoom / mainTaskMs | 226.0 [233.6] | 222.2 [233.1] | 0.983 [0.958, 1.038] | pass |
| zoom / mainJsMs | 163.5 [167.7] | 160.7 [169.5] | 0.983 [0.956, 1.054] | pass |
| zoomLarge / settleMs | 300.5 [322.8] | 305.5 [321.7] | +5.0 [-34.8, +24.7] | pass |
| zoomLarge / zoom2SettleMs | 137.7 [151.5] | 137.3 [151.1] | -0.4 [-18.6, +10.6] | pass |
| zoomLarge / zoom4SettleMs | 157.2 [176.8] | 159.4 [182.0] | +2.2 [-15.5, +19.2] | pass |
| zoomLarge / backTo1SettleMs | 69.9 [80.7] | 73.8 [86.8] | +3.9 [-4.2, +12.3] | pass |
| zoomLarge / mainTaskMs | 169.7 [181.1] | 175.5 [183.8] | 1.034 [0.903, 1.158] | pass |
| zoomLarge / mainJsMs | 126.8 [135.2] | 130.3 [136.5] | 1.027 [0.882, 1.166] | pass |
| select / settleMs | 229.7 [251.4] | 241.4 [262.8] | +11.8 [-23.0, +31.4] | pass |
| select / mainTaskMs | 61.9 [64.8] | 62.4 [65.9] | 1.009 [0.924, 1.070] | pass |
| select / mainJsMs | 39.9 [42.2] | 40.8 [42.6] | 1.023 [0.930, 1.082] | pass |
| selectLarge / settleMs | 222.2 [246.0] | 224.5 [252.9] | +2.3 [-32.4, +16.3] | pass |
| selectLarge / mainTaskMs | 71.8 [74.8] | 71.3 [76.7] | 0.992 [0.895, 1.098] | pass |
| selectLarge / mainJsMs | 48.6 [50.8] | 48.3 [52.3] | 0.994 [0.876, 1.107] | pass |
| search / searchMs | 72.0 [77.2] | 69.6 [75.5] | 0.967 [0.866, 1.106] | pass |
| search / mainTaskMs | 5.2 [5.8] | 5.5 [5.9] | 1.058 [0.938, 1.155] | pass |
| search / mainJsMs | 3.6 [3.7] | 3.7 [3.8] | 1.023 [0.978, 1.060] | pass |
| searchLarge / searchMs | 1911.2 [1972.7] | 1900.3 [1945.6] | 0.994 [0.975, 1.021] | pass |
| searchLarge / mainTaskMs | 342.5 [354.4] | 346.6 [355.0] | 1.012 [0.998, 1.022] | pass |
| searchLarge / mainJsMs | 290.2 [297.9] | 291.9 [300.2] | 1.006 [0.999, 1.018] | pass |
| annotate / settleMs | 215.0 [257.7] | 203.1 [234.6] | -12.0 [-41.2, +25.5] | pass |
| annotate / mainTaskMs | 79.3 [84.5] | 77.6 [83.2] | 0.978 [0.915, 1.070] | pass |
| annotate / mainJsMs | 46.8 [51.2] | 46.1 [50.1] | 0.985 [0.883, 1.071] | pass |
| annotateLarge / settleMs | 219.7 [253.1] | 194.1 [246.6] | -25.6 [-52.1, +43.7] | pass |
| annotateLarge / mainTaskMs | 80.8 [84.0] | 78.2 [84.2] | 0.967 [0.898, 1.162] | pass |
| annotateLarge / mainJsMs | 48.3 [50.7] | 46.3 [51.2] | 0.958 [0.879, 1.184] | pass |

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

Not run: `plugins-typed`.

## `plugins-fixes` vs `plugins-encode`

| Row | plugins-fixes median [p95] | plugins-encode median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 92.0 [97.3] | 93.9 [103.1] | 1.021 [0.946, 1.087] | no detectable difference |
| open(ebook) / openedMs | 211.8 [220.5] | 213.9 [224.7] | 1.010 [0.972, 1.045] | no detectable difference |
| open(ebook) / firstPaintMs | 368.6 [391.1] | 367.9 [415.7] | 0.998 [0.948, 1.054] | no detectable difference |
| open(ebook) / settledMs | 435.7 [460.7] | 431.4 [459.5] | 0.990 [0.952, 1.022] | no detectable difference |
| open(ebook) / mainTaskMs | 291.6 [303.3] | 286.0 [311.9] | 0.981 [0.959, 1.027] | no detectable difference |
| open(ebook) / mainJsMs | 238.9 [283.7] | 234.5 [258.7] | 0.982 [0.947, 1.031] | no detectable difference |
| open(large) / readyMs | 91.9 [100.3] | 91.9 [106.6] | 1.000 [0.952, 1.086] | no detectable difference |
| open(large) / openedMs | 241.9 [254.6] | 241.2 [257.7] | 0.997 [0.969, 1.025] | no detectable difference |
| open(large) / firstPaintMs | 420.5 [436.9] | 423.7 [446.4] | 1.007 [0.978, 1.038] | no detectable difference |
| open(large) / settledMs | 417.8 [442.2] | 419.4 [442.7] | 1.004 [0.956, 1.023] | no detectable difference |
| open(large) / mainTaskMs | 574.9 [601.9] | 574.6 [589.2] | 0.999 [0.968, 1.024] | no detectable difference |
| open(large) / mainJsMs | 529.4 [550.4] | 528.5 [537.5] | 0.998 [0.971, 1.027] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 3007.4 [3127.2] | 3016.5 [3149.2] | 1.003 [0.980, 1.033] | no detectable difference |
| scroll / frameP50Ms | 11.1 [11.7] | 11.1 [11.8] | 0.997 [0.960, 1.041] | no detectable difference |
| scroll / frameP95Ms | 26.2 [27.2] | 26.4 [27.2] | 1.010 [0.956, 1.193] | unreliable (A/A failed) |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 848.0 [865.8] | 843.4 [867.1] | 0.995 [0.910, 1.089] | no detectable difference |
| scroll / mainJsMs | 714.9 [732.8] | 709.7 [734.1] | 0.993 [0.903, 1.094] | no detectable difference |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2941.6 [3062.1] | 2944.1 [3055.6] | 1.001 [0.970, 1.035] | no detectable difference |
| coldScroll / frameP50Ms | 11.1 [11.7] | 11.2 [11.8] | 1.003 [0.973, 1.042] | no detectable difference |
| coldScroll / frameP95Ms | 26.2 [26.9] | 26.0 [27.1] | 0.992 [0.927, 1.048] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 836.3 [846.4] | 822.9 [837.9] | 0.984 [0.910, 1.067] | no detectable difference |
| coldScroll / mainJsMs | 697.6 [704.3] | 685.3 [696.8] | 0.982 [0.894, 1.072] | no detectable difference |
| jump / settleMs | 152.7 [171.0] | 143.5 [163.6] | -9.1 [-34.4, +9.7] | no detectable difference |
| jump / mainTaskMs | 152.7 [164.3] | 142.0 [148.5] | 0.930 [0.869, 0.983] | no detectable difference |
| jump / mainJsMs | 136.5 [146.7] | 125.4 [131.3] | 0.918 [0.868, 0.979] | no detectable difference |
| zoom / settleMs | 529.9 [537.3] | 524.4 [563.6] | -5.4 [-23.1, +16.4] | no detectable difference |
| zoom / zoom2SettleMs | 188.3 [193.1] | 182.0 [219.1] | -6.3 [-12.3, +13.9] | no detectable difference |
| zoom / zoom4SettleMs | 343.3 [349.2] | 338.7 [351.0] | -4.6 [-14.1, +5.8] | no detectable difference |
| zoom / backTo1SettleMs | 103.3 [123.0] | 103.4 [108.3] | +0.1 [-14.3, +9.1] | no detectable difference |
| zoom / mainTaskMs | 330.7 [347.1] | 330.9 [347.3] | 1.001 [0.961, 1.043] | no detectable difference |
| zoom / mainJsMs | 274.3 [286.2] | 271.5 [289.9] | 0.990 [0.960, 1.045] | no detectable difference |
| zoomLarge / settleMs | 306.5 [336.6] | 316.1 [331.9] | +9.6 [-15.0, +27.9] | no detectable difference |
| zoomLarge / zoom2SettleMs | 135.8 [152.2] | 142.4 [156.7] | +6.5 [-6.6, +20.5] | no detectable difference |
| zoomLarge / zoom4SettleMs | 172.6 [187.0] | 176.6 [190.1] | +4.0 [-13.0, +13.1] | no detectable difference |
| zoomLarge / backTo1SettleMs | 76.6 [101.3] | 83.6 [87.5] | +7.0 [-14.2, +14.4] | no detectable difference |
| zoomLarge / mainTaskMs | 250.1 [264.4] | 257.4 [269.0] | 1.029 [0.953, 1.103] | no detectable difference |
| zoomLarge / mainJsMs | 210.0 [221.8] | 216.0 [226.3] | 1.028 [0.948, 1.115] | no detectable difference |
| select / settleMs | 219.9 [273.5] | 214.3 [245.5] | -5.6 [-40.5, +22.5] | no detectable difference |
| select / mainTaskMs | 78.5 [94.7] | 80.5 [86.8] | 1.025 [0.911, 1.131] | no detectable difference |
| select / mainJsMs | 62.5 [75.7] | 63.5 [68.1] | 1.017 [0.906, 1.156] | no detectable difference |
| selectLarge / settleMs | 223.6 [253.6] | 230.2 [258.9] | +6.6 [-23.7, +19.8] | no detectable difference |
| selectLarge / mainTaskMs | 106.4 [114.5] | 110.5 [114.4] | 1.039 [0.973, 1.210] | no detectable difference |
| selectLarge / mainJsMs | 82.8 [89.4] | 86.6 [89.4] | 1.045 [0.975, 1.231] | no detectable difference |
| search / searchMs | 80.4 [82.3] | 79.7 [83.5] | 0.991 [0.875, 1.097] | no detectable difference |
| search / mainTaskMs | 12.7 [20.7] | 14.0 [21.3] | 1.102 [0.732, 1.782] | no detectable difference |
| search / mainJsMs | 7.7 [8.6] | 8.1 [9.3] | 1.058 [0.802, 1.266] | no detectable difference |
| searchLarge / searchMs | 1882.9 [1925.1] | 1823.2 [1874.8] | 0.968 [0.961, 0.985] | faster |
| searchLarge / mainTaskMs | 914.4 [932.6] | 837.4 [896.5] | 0.916 [0.912, 0.938] | faster |
| searchLarge / mainJsMs | 864.8 [884.5] | 801.0 [859.9] | 0.926 [0.923, 0.950] | faster |
| annotate / settleMs | 240.7 [264.7] | 221.6 [244.6] | -19.1 [-39.9, +12.2] | no detectable difference |
| annotate / mainTaskMs | 102.6 [109.6] | 102.2 [109.1] | 0.995 [0.967, 1.123] | no detectable difference |
| annotate / mainJsMs | 75.7 [81.6] | 75.3 [80.6] | 0.995 [0.952, 1.150] | no detectable difference |
| annotateLarge / settleMs | 241.3 [266.9] | 239.7 [275.1] | -1.6 [-42.8, +21.3] | no detectable difference |
| annotateLarge / mainTaskMs | 126.6 [130.1] | 125.5 [133.1] | 0.991 [0.887, 1.060] | no detectable difference |
| annotateLarge / mainJsMs | 84.6 [86.7] | 82.7 [89.6] | 0.978 [0.871, 1.032] | no detectable difference |

## `main` vs `plugins-encode`

| Row | main median [p95] | plugins-encode median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.2 [67.5] | 93.9 [103.1] | 1.486 [1.413, 1.567] | slower |
| open(ebook) / openedMs | 138.1 [152.8] | 213.9 [224.7] | 1.548 [1.437, 1.603] | slower |
| open(ebook) / firstPaintMs | 257.4 [280.6] | 367.9 [415.7] | 1.430 [1.359, 1.514] | slower |
| open(ebook) / settledMs | 397.1 [418.1] | 431.4 [459.5] | 1.086 [1.038, 1.112] | slower |
| open(ebook) / mainTaskMs | 145.7 [151.1] | 286.0 [311.9] | 1.963 [1.924, 2.033] | slower |
| open(ebook) / mainJsMs | 111.5 [116.3] | 234.5 [258.7] | 2.103 [2.043, 2.172] | slower |
| open(large) / readyMs | 63.3 [68.7] | 91.9 [106.6] | 1.451 [1.375, 1.575] | slower |
| open(large) / openedMs | 159.0 [166.7] | 241.2 [257.7] | 1.517 [1.464, 1.563] | slower |
| open(large) / firstPaintMs | 275.4 [293.3] | 423.7 [446.4] | 1.539 [1.490, 1.571] | slower |
| open(large) / settledMs | 272.7 [284.2] | 419.4 [442.7] | 1.538 [1.502, 1.579] | slower |
| open(large) / mainTaskMs | 315.9 [341.9] | 574.6 [589.2] | 1.819 [1.751, 1.843] | slower |
| open(large) / mainJsMs | 285.5 [308.3] | 528.5 [537.5] | 1.851 [1.782, 1.875] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2873.1 [2980.1] | 3016.5 [3149.2] | 1.050 [1.026, 1.078] | slower |
| scroll / frameP50Ms | 11.3 [12.0] | 11.1 [11.8] | 0.983 [0.935, 1.026] | no detectable difference |
| scroll / frameP95Ms | 26.7 [27.3] | 26.4 [27.2] | 0.991 [0.912, 1.027] | unreliable (A/A failed) |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 527.9 [550.4] | 843.4 [867.1] | 1.598 [1.468, 1.661] | slower |
| scroll / mainJsMs | 395.6 [406.8] | 709.7 [734.1] | 1.794 [1.632, 1.849] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2825.7 [2960.1] | 2944.1 [3055.6] | 1.042 [1.012, 1.082] | slower |
| coldScroll / frameP50Ms | 11.2 [12.0] | 11.2 [11.8] | 0.996 [0.935, 1.039] | no detectable difference |
| coldScroll / frameP95Ms | 26.4 [27.2] | 26.0 [27.1] | 0.988 [0.924, 1.066] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 552.2 [577.1] | 822.9 [837.9] | 1.490 [1.389, 1.545] | slower |
| coldScroll / mainJsMs | 383.7 [404.0] | 685.3 [696.8] | 1.786 [1.638, 1.844] | slower |
| jump / settleMs | 119.1 [133.6] | 143.5 [163.6] | +24.4 [+4.3, +41.1] | slower |
| jump / mainTaskMs | 72.3 [79.2] | 142.0 [148.5] | 1.965 [1.841, 2.118] | slower |
| jump / mainJsMs | 54.9 [57.2] | 125.4 [131.3] | 2.283 [2.148, 2.540] | slower |
| zoom / settleMs | 482.0 [510.5] | 524.4 [563.6] | +42.5 [+19.6, +68.4] | slower |
| zoom / zoom2SettleMs | 163.1 [167.5] | 182.0 [219.1] | +18.9 [+13.6, +38.7] | slower |
| zoom / zoom4SettleMs | 319.8 [346.5] | 338.7 [351.0] | +19.0 [+0.9, +33.1] | no detectable difference |
| zoom / backTo1SettleMs | 88.1 [107.7] | 103.4 [108.3] | +15.3 [+3.9, +20.7] | slower |
| zoom / mainTaskMs | 226.0 [233.6] | 330.9 [347.3] | 1.464 [1.399, 1.544] | slower |
| zoom / mainJsMs | 163.5 [167.7] | 271.5 [289.9] | 1.660 [1.601, 1.789] | slower |
| zoomLarge / settleMs | 300.5 [322.8] | 316.1 [331.9] | +15.6 [+0.3, +36.4] | no detectable difference |
| zoomLarge / zoom2SettleMs | 137.7 [151.5] | 142.4 [156.7] | +4.7 [-6.5, +14.0] | no detectable difference |
| zoomLarge / zoom4SettleMs | 157.2 [176.8] | 176.6 [190.1] | +19.4 [-2.9, +27.5] | no detectable difference |
| zoomLarge / backTo1SettleMs | 69.9 [80.7] | 83.6 [87.5] | +13.7 [+4.4, +20.1] | slower |
| zoomLarge / mainTaskMs | 169.7 [181.1] | 257.4 [269.0] | 1.516 [1.402, 1.694] | slower |
| zoomLarge / mainJsMs | 126.8 [135.2] | 216.0 [226.3] | 1.703 [1.569, 1.927] | slower |
| select / settleMs | 229.7 [251.4] | 214.3 [245.5] | -15.4 [-36.6, +7.4] | no detectable difference |
| select / mainTaskMs | 61.9 [64.8] | 80.5 [86.8] | 1.301 [1.184, 1.387] | slower |
| select / mainJsMs | 39.9 [42.2] | 63.5 [68.1] | 1.593 [1.443, 1.705] | slower |
| selectLarge / settleMs | 222.2 [246.0] | 230.2 [258.9] | +8.0 [-20.3, +23.6] | no detectable difference |
| selectLarge / mainTaskMs | 71.8 [74.8] | 110.5 [114.4] | 1.539 [1.445, 1.659] | slower |
| selectLarge / mainJsMs | 48.6 [50.8] | 86.6 [89.4] | 1.781 [1.654, 1.951] | slower |
| search / searchMs | 72.0 [77.2] | 79.7 [83.5] | 1.107 [0.953, 1.249] | no detectable difference |
| search / mainTaskMs | 5.2 [5.8] | 14.0 [21.3] | 2.689 [1.859, 3.909] | slower |
| search / mainJsMs | 3.6 [3.7] | 8.1 [9.3] | 2.263 [1.801, 2.410] | slower |
| searchLarge / searchMs | 1911.2 [1972.7] | 1823.2 [1874.8] | 0.954 [0.936, 0.981] | faster |
| searchLarge / mainTaskMs | 342.5 [354.4] | 837.4 [896.5] | 2.445 [2.425, 2.501] | slower |
| searchLarge / mainJsMs | 290.2 [297.9] | 801.0 [859.9] | 2.761 [2.744, 2.828] | slower |
| annotate / settleMs | 215.0 [257.7] | 221.6 [244.6] | +6.6 [-24.2, +50.8] | no detectable difference |
| annotate / mainTaskMs | 79.3 [84.5] | 102.2 [109.1] | 1.288 [1.240, 1.397] | slower |
| annotate / mainJsMs | 46.8 [51.2] | 75.3 [80.6] | 1.609 [1.491, 1.724] | slower |
| annotateLarge / settleMs | 219.7 [253.1] | 239.7 [275.1] | +20.1 [-24.9, +71.0] | no detectable difference |
| annotateLarge / mainTaskMs | 80.8 [84.0] | 125.5 [133.1] | 1.552 [1.428, 1.863] | slower |
| annotateLarge / mainJsMs | 48.3 [50.7] | 82.7 [89.6] | 1.711 [1.573, 2.108] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-fixes median [p95] | plugins-encode median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 35.34 MiB [35.35 MiB] | 35.35 MiB [35.36 MiB] | 42.36 MiB [43.36 MiB] | 43.36 MiB [43.37 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.18 MiB] | 48.15 MiB [48.16 MiB] | 55.05 MiB [55.11 MiB] | 54.99 MiB [55.14 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.29 MiB] | 22.26 MiB [22.26 MiB] | 22.96 MiB [22.97 MiB] | 22.96 MiB [22.96 MiB] |
| soak / heapCycle5Bytes | 37.92 MiB [38.98 MiB] | 37.92 MiB [38.43 MiB] | 45.15 MiB [46.15 MiB] | 45.66 MiB [46.17 MiB] |
| soak / heapCycle30Bytes | 38.77 MiB [39.82 MiB] | 38.77 MiB [39.27 MiB] | 45.69 MiB [46.69 MiB] | 46.20 MiB [46.71 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-fixes`:

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

`plugins-encode`:

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

