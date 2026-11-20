# Viewer perf comparison: 2026-10-05T18-16-21-739Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 663a93ec7200348db365af6fec2a966832028a31 |
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
| plugins-sharedhits | research/rust-kernel-shared-hits | b450a9909 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 5f383fbcd10b |
| plugins-promise | research/rust-kernel-typed-results | 19f07e077 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 39e811f6ff08 |
| plugins-typed | research/rust-kernel-typed-results | b88217027 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | e9bf2bfc551a |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-sharedhits`: pass
- `plugins-promise`: pass
- `plugins-typed`: pass

## Sizes

| Metric | main | main-aa | plugins-sharedhits | plugins-promise | plugins-typed |
| --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 7.94 MiB | 7.99 MiB | 8.03 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.71 MiB | 1.71 MiB | 1.72 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

63 of 63 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.4 [71.7] | 62.5 [67.8] | 0.985 [0.942, 1.052] | pass |
| open(ebook) / openedMs | 137.8 [151.8] | 137.1 [142.6] | 0.995 [0.900, 1.022] | pass |
| open(ebook) / firstPaintMs | 263.7 [273.4] | 251.9 [269.8] | 0.956 [0.927, 1.005] | pass |
| open(ebook) / settledMs | 395.2 [409.5] | 392.2 [400.8] | 0.992 [0.958, 1.022] | pass |
| open(ebook) / mainTaskMs | 143.3 [147.7] | 143.5 [147.9] | 1.001 [0.966, 1.028] | pass |
| open(ebook) / mainJsMs | 110.5 [113.8] | 110.1 [114.2] | 0.997 [0.972, 1.027] | pass |
| open(large) / readyMs | 64.1 [73.8] | 62.3 [65.4] | 0.973 [0.922, 1.031] | pass |
| open(large) / openedMs | 159.0 [168.2] | 158.9 [162.9] | 1.000 [0.978, 1.025] | pass |
| open(large) / firstPaintMs | 266.5 [281.8] | 266.2 [280.3] | 0.999 [0.964, 1.040] | pass |
| open(large) / settledMs | 269.5 [276.7] | 270.2 [277.7] | 1.003 [0.978, 1.031] | pass |
| open(large) / mainTaskMs | 314.2 [316.5] | 315.0 [319.9] | 1.003 [0.997, 1.015] | pass |
| open(large) / mainJsMs | 283.3 [285.2] | 284.3 [289.4] | 1.003 [0.997, 1.014] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2904.9 [2922.5] | 2888.8 [2994.3] | 0.994 [0.982, 1.021] | pass |
| scroll / frameP50Ms | 11.0 [11.5] | 11.1 [11.6] | 1.005 [0.984, 1.039] | pass |
| scroll / frameP95Ms | 22.2 [26.7] | 23.0 [27.0] | 1.036 [0.855, 1.166] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 547.9 [562.4] | 547.9 [566.3] | 1.000 [0.980, 1.024] | pass |
| scroll / mainJsMs | 410.7 [417.4] | 409.4 [426.1] | 0.997 [0.982, 1.030] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2831.3 [2913.1] | 2828.0 [2956.0] | 0.999 [0.978, 1.025] | pass |
| coldScroll / frameP50Ms | 11.1 [11.6] | 11.1 [11.9] | 1.007 [0.962, 1.057] | pass |
| coldScroll / frameP95Ms | 25.0 [26.5] | 22.7 [27.2] | 0.908 [0.858, 1.145] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 569.5 [582.0] | 568.1 [589.6] | 0.998 [0.982, 1.016] | pass |
| coldScroll / mainJsMs | 398.7 [406.7] | 397.4 [414.2] | 0.997 [0.982, 1.019] | pass |
| jump / settleMs | 125.0 [132.4] | 125.0 [136.9] | -0.0 [-9.5, +7.4] | pass |
| jump / mainTaskMs | 70.2 [77.8] | 73.3 [80.2] | 1.045 [0.984, 1.090] | pass |
| jump / mainJsMs | 53.1 [57.3] | 54.9 [59.4] | 1.034 [0.977, 1.082] | pass |
| zoom / settleMs | 443.3 [491.3] | 450.6 [474.3] | +7.3 [-31.7, +32.5] | pass |
| zoom / zoom2SettleMs | 164.7 [189.9] | 164.7 [172.5] | +0.0 [-14.4, +6.8] | pass |
| zoom / zoom4SettleMs | 283.1 [304.5] | 286.6 [303.6] | +3.5 [-21.7, +30.0] | pass |
| zoom / backTo1SettleMs | 81.2 [96.8] | 85.9 [89.7] | +4.7 [-3.5, +8.6] | pass |
| zoom / mainTaskMs | 214.9 [228.0] | 223.2 [227.7] | 1.039 [0.991, 1.075] | pass |
| zoom / mainJsMs | 155.1 [165.5] | 157.1 [166.6] | 1.013 [0.994, 1.079] | pass |
| zoomLarge / settleMs | 291.4 [334.5] | 291.9 [303.3] | +0.5 [-26.5, +19.4] | pass |
| zoomLarge / zoom2SettleMs | 140.9 [159.8] | 134.9 [150.0] | -6.0 [-14.3, +9.4] | pass |
| zoomLarge / zoom4SettleMs | 158.6 [174.7] | 157.0 [166.7] | -1.6 [-21.5, +17.4] | pass |
| zoomLarge / backTo1SettleMs | 72.4 [84.6] | 77.7 [96.7] | +5.4 [-7.5, +15.8] | pass |
| zoomLarge / mainTaskMs | 177.1 [186.3] | 178.2 [182.9] | 1.006 [0.987, 1.023] | pass |
| zoomLarge / mainJsMs | 132.4 [138.8] | 132.4 [136.4] | 1.001 [0.978, 1.022] | pass |
| select / settleMs | 215.9 [252.0] | 215.2 [261.9] | -0.7 [-27.1, +30.1] | pass |
| select / mainTaskMs | 61.3 [67.0] | 63.5 [67.8] | 1.037 [0.982, 1.079] | pass |
| select / mainJsMs | 39.5 [43.4] | 41.2 [44.0] | 1.043 [0.983, 1.100] | pass |
| selectLarge / settleMs | 209.1 [247.4] | 217.6 [265.1] | +8.5 [-18.6, +35.7] | pass |
| selectLarge / mainTaskMs | 70.6 [75.5] | 71.6 [78.3] | 1.014 [0.977, 1.107] | pass |
| selectLarge / mainJsMs | 47.6 [50.8] | 47.9 [53.3] | 1.005 [0.965, 1.110] | pass |
| search / searchMs | 69.8 [77.7] | 74.0 [77.9] | 1.060 [0.974, 1.141] | pass |
| search / mainTaskMs | 5.2 [6.4] | 5.4 [5.9] | 1.040 [0.973, 1.116] | pass |
| search / mainJsMs | 3.6 [4.1] | 3.7 [3.8] | 1.031 [0.969, 1.050] | pass |
| searchLarge / searchMs | 1897.5 [1933.3] | 1892.8 [1931.9] | 0.998 [0.981, 1.007] | pass |
| searchLarge / mainTaskMs | 345.2 [349.3] | 345.5 [349.1] | 1.001 [0.989, 1.010] | pass |
| searchLarge / mainJsMs | 291.7 [294.6] | 291.1 [293.5] | 0.998 [0.993, 1.006] | pass |
| annotate / settleMs | 216.1 [268.2] | 221.8 [258.1] | +5.8 [-35.0, +52.8] | pass |
| annotate / mainTaskMs | 77.9 [83.5] | 78.3 [85.2] | 1.004 [0.946, 1.057] | pass |
| annotate / mainJsMs | 46.1 [50.4] | 47.6 [52.4] | 1.031 [0.958, 1.079] | pass |
| annotateLarge / settleMs | 207.2 [251.5] | 216.2 [263.5] | +9.0 [-37.0, +39.9] | pass |
| annotateLarge / mainTaskMs | 79.4 [86.9] | 82.8 [87.6] | 1.042 [0.945, 1.106] | pass |
| annotateLarge / mainJsMs | 48.2 [51.8] | 49.6 [52.5] | 1.030 [0.943, 1.087] | pass |

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

Not run: `plugins-tip3`.

## `main` vs `plugins-sharedhits`

| Row | main median [p95] | plugins-sharedhits median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.4 [71.7] | 93.6 [167.1] | 1.476 [1.381, 2.074] | slower |
| open(ebook) / openedMs | 137.8 [151.8] | 215.8 [343.8] | 1.565 [1.408, 1.970] | slower |
| open(ebook) / firstPaintMs | 263.7 [273.4] | 369.5 [497.0] | 1.401 [1.361, 1.652] | slower |
| open(ebook) / settledMs | 395.2 [409.5] | 438.3 [560.9] | 1.109 [1.060, 1.254] | slower |
| open(ebook) / mainTaskMs | 143.3 [147.7] | 292.9 [302.8] | 2.044 [1.998, 2.097] | slower |
| open(ebook) / mainJsMs | 110.5 [113.8] | 241.7 [250.5] | 2.188 [2.146, 2.250] | slower |
| open(large) / readyMs | 64.1 [73.8] | 90.1 [94.3] | 1.405 [1.355, 1.467] | slower |
| open(large) / openedMs | 159.0 [168.2] | 241.7 [249.5] | 1.520 [1.491, 1.541] | slower |
| open(large) / firstPaintMs | 266.5 [281.8] | 412.3 [426.1] | 1.548 [1.504, 1.610] | slower |
| open(large) / settledMs | 269.5 [276.7] | 416.8 [428.7] | 1.546 [1.510, 1.581] | slower |
| open(large) / mainTaskMs | 314.2 [316.5] | 573.0 [582.3] | 1.824 [1.797, 1.846] | slower |
| open(large) / mainJsMs | 283.3 [285.2] | 525.4 [534.0] | 1.855 [1.814, 1.876] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2904.9 [2922.5] | 3055.8 [3162.3] | 1.052 [1.040, 1.071] | slower |
| scroll / frameP50Ms | 11.0 [11.5] | 11.1 [11.7] | 1.010 [0.990, 1.051] | no detectable difference |
| scroll / frameP95Ms | 22.2 [26.7] | 26.5 [27.2] | 1.195 [0.999, 1.264] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 547.9 [562.4] | 909.3 [935.0] | 1.660 [1.632, 1.695] | slower |
| scroll / mainJsMs | 410.7 [417.4] | 770.5 [792.9] | 1.876 [1.845, 1.913] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2831.3 [2913.1] | 3062.8 [3167.9] | 1.082 [1.050, 1.104] | slower |
| coldScroll / frameP50Ms | 11.1 [11.6] | 11.5 [11.7] | 1.038 [0.981, 1.063] | no detectable difference |
| coldScroll / frameP95Ms | 25.0 [26.5] | 26.9 [27.2] | 1.077 [1.009, 1.231] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 569.5 [582.0] | 913.4 [948.1] | 1.604 [1.573, 1.636] | slower |
| coldScroll / mainJsMs | 398.7 [406.7] | 755.6 [792.5] | 1.895 [1.859, 1.938] | slower |
| jump / settleMs | 125.0 [132.4] | 174.8 [211.9] | +49.8 [+34.3, +68.0] | slower |
| jump / mainTaskMs | 70.2 [77.8] | 191.7 [199.7] | 2.732 [2.587, 2.832] | slower |
| jump / mainJsMs | 53.1 [57.3] | 175.8 [183.5] | 3.309 [3.134, 3.420] | slower |
| zoom / settleMs | 443.3 [491.3] | 518.0 [538.4] | +74.8 [+41.7, +93.4] | slower |
| zoom / zoom2SettleMs | 164.7 [189.9] | 193.3 [214.3] | +28.6 [+15.7, +40.2] | slower |
| zoom / zoom4SettleMs | 283.1 [304.5] | 327.0 [332.4] | +43.9 [+25.9, +64.8] | slower |
| zoom / backTo1SettleMs | 81.2 [96.8] | 105.6 [117.2] | +24.4 [+16.7, +28.1] | slower |
| zoom / mainTaskMs | 214.9 [228.0] | 332.9 [345.9] | 1.549 [1.506, 1.608] | slower |
| zoom / mainJsMs | 155.1 [165.5] | 276.8 [289.9] | 1.785 [1.712, 1.882] | slower |
| zoomLarge / settleMs | 291.4 [334.5] | 299.4 [324.6] | +8.0 [-20.8, +25.9] | no detectable difference |
| zoomLarge / zoom2SettleMs | 140.9 [159.8] | 137.8 [145.0] | -3.1 [-12.4, +14.2] | no detectable difference |
| zoomLarge / zoom4SettleMs | 158.6 [174.7] | 162.3 [184.3] | +3.7 [-17.9, +26.2] | no detectable difference |
| zoomLarge / backTo1SettleMs | 72.4 [84.6] | 88.6 [105.2] | +16.2 [+7.7, +25.1] | slower |
| zoomLarge / mainTaskMs | 177.1 [186.3] | 268.4 [276.6] | 1.515 [1.470, 1.547] | slower |
| zoomLarge / mainJsMs | 132.4 [138.8] | 226.3 [233.1] | 1.709 [1.654, 1.749] | slower |
| select / settleMs | 215.9 [252.0] | 211.8 [268.5] | -4.0 [-40.7, +35.8] | no detectable difference |
| select / mainTaskMs | 61.3 [67.0] | 80.9 [91.3] | 1.321 [1.254, 1.409] | slower |
| select / mainJsMs | 39.5 [43.4] | 64.2 [72.1] | 1.623 [1.541, 1.758] | slower |
| selectLarge / settleMs | 209.1 [247.4] | 238.4 [278.2] | +29.3 [+0.1, +48.8] | no detectable difference |
| selectLarge / mainTaskMs | 70.6 [75.5] | 112.1 [121.9] | 1.588 [1.514, 1.737] | slower |
| selectLarge / mainJsMs | 47.6 [50.8] | 87.4 [95.6] | 1.834 [1.743, 2.026] | slower |
| search / searchMs | 69.8 [77.7] | 81.8 [84.6] | 1.172 [1.058, 1.267] | slower |
| search / mainTaskMs | 5.2 [6.4] | 15.8 [21.3] | 3.059 [2.006, 4.025] | slower |
| search / mainJsMs | 3.6 [4.1] | 7.9 [8.9] | 2.194 [1.971, 2.457] | slower |
| searchLarge / searchMs | 1897.5 [1933.3] | 2076.4 [2079.6] | 1.094 [1.078, 1.099] | slower |
| searchLarge / mainTaskMs | 345.2 [349.3] | 1112.2 [1118.9] | 3.222 [3.195, 3.245] | slower |
| searchLarge / mainJsMs | 291.7 [294.6] | 1061.1 [1069.5] | 3.638 [3.621, 3.655] | slower |
| annotate / settleMs | 216.1 [268.2] | 226.3 [277.7] | +10.2 [-33.7, +61.4] | no detectable difference |
| annotate / mainTaskMs | 77.9 [83.5] | 103.3 [112.9] | 1.326 [1.252, 1.409] | slower |
| annotate / mainJsMs | 46.1 [50.4] | 75.4 [83.5] | 1.635 [1.558, 1.748] | slower |
| annotateLarge / settleMs | 207.2 [251.5] | 230.8 [278.0] | +23.6 [-19.8, +49.2] | no detectable difference |
| annotateLarge / mainTaskMs | 79.4 [86.9] | 124.9 [131.9] | 1.572 [1.453, 1.637] | slower |
| annotateLarge / mainJsMs | 48.2 [51.8] | 83.9 [89.0] | 1.741 [1.610, 1.828] | slower |

## `plugins-sharedhits` vs `plugins-promise`

| Row | plugins-sharedhits median [p95] | plugins-promise median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 93.6 [167.1] | 90.0 [102.5] | 0.961 [0.695, 1.054] | no detectable difference |
| open(ebook) / openedMs | 215.8 [343.8] | 210.4 [222.9] | 0.975 [0.763, 1.030] | no detectable difference |
| open(ebook) / firstPaintMs | 369.5 [497.0] | 365.6 [386.7] | 0.990 [0.853, 1.038] | no detectable difference |
| open(ebook) / settledMs | 438.3 [560.9] | 427.5 [450.0] | 0.975 [0.863, 1.016] | no detectable difference |
| open(ebook) / mainTaskMs | 292.9 [302.8] | 292.8 [302.1] | 1.000 [0.962, 1.020] | no detectable difference |
| open(ebook) / mainJsMs | 241.7 [250.5] | 243.1 [249.5] | 1.006 [0.961, 1.026] | no detectable difference |
| open(large) / readyMs | 90.1 [94.3] | 90.8 [109.5] | 1.008 [0.975, 1.102] | no detectable difference |
| open(large) / openedMs | 241.7 [249.5] | 240.7 [260.2] | 0.996 [0.982, 1.034] | no detectable difference |
| open(large) / firstPaintMs | 412.3 [426.1] | 418.6 [436.9] | 1.015 [0.981, 1.043] | no detectable difference |
| open(large) / settledMs | 416.8 [428.7] | 416.2 [435.5] | 0.998 [0.985, 1.038] | no detectable difference |
| open(large) / mainTaskMs | 573.0 [582.3] | 574.6 [584.9] | 1.003 [0.992, 1.022] | no detectable difference |
| open(large) / mainJsMs | 525.4 [534.0] | 524.6 [536.0] | 0.998 [0.988, 1.024] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 3055.8 [3162.3] | 3068.0 [3126.7] | 1.004 [0.986, 1.019] | no detectable difference |
| scroll / frameP50Ms | 11.1 [11.7] | 11.3 [11.5] | 1.020 [0.984, 1.034] | no detectable difference |
| scroll / frameP95Ms | 26.5 [27.2] | 27.1 [27.2] | 1.023 [0.952, 1.078] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 909.3 [935.0] | 880.4 [888.4] | 0.968 [0.943, 0.982] | faster |
| scroll / mainJsMs | 770.5 [792.9] | 747.0 [757.9] | 0.969 [0.950, 0.984] | faster |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 3062.8 [3167.9] | 3014.1 [3119.4] | 0.984 [0.957, 1.008] | no detectable difference |
| coldScroll / frameP50Ms | 11.5 [11.7] | 11.0 [11.3] | 0.956 [0.928, 1.008] | no detectable difference |
| coldScroll / frameP95Ms | 26.9 [27.2] | 24.6 [27.2] | 0.915 [0.869, 1.036] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 913.4 [948.1] | 876.6 [892.1] | 0.960 [0.946, 0.978] | faster |
| coldScroll / mainJsMs | 755.6 [792.5] | 729.5 [737.8] | 0.965 [0.948, 0.984] | faster |
| jump / settleMs | 174.8 [211.9] | 161.2 [198.1] | -13.6 [-33.7, +18.0] | no detectable difference |
| jump / mainTaskMs | 191.7 [199.7] | 171.1 [178.2] | 0.892 [0.867, 0.914] | faster |
| jump / mainJsMs | 175.8 [183.5] | 154.1 [161.6] | 0.877 [0.854, 0.905] | faster |
| zoom / settleMs | 518.0 [538.4] | 513.6 [535.8] | -4.4 [-20.4, +4.4] | no detectable difference |
| zoom / zoom2SettleMs | 193.3 [214.3] | 188.7 [213.1] | -4.6 [-15.5, +5.9] | no detectable difference |
| zoom / zoom4SettleMs | 327.0 [332.4] | 320.6 [329.8] | -6.4 [-14.4, +7.1] | no detectable difference |
| zoom / backTo1SettleMs | 105.6 [117.2] | 105.0 [127.9] | -0.7 [-2.8, +20.0] | no detectable difference |
| zoom / mainTaskMs | 332.9 [345.9] | 326.3 [341.5] | 0.980 [0.951, 1.020] | no detectable difference |
| zoom / mainJsMs | 276.8 [289.9] | 272.6 [286.2] | 0.985 [0.946, 1.025] | no detectable difference |
| zoomLarge / settleMs | 299.4 [324.6] | 320.7 [346.5] | +21.3 [+4.7, +37.9] | no detectable difference |
| zoomLarge / zoom2SettleMs | 137.8 [145.0] | 148.8 [158.0] | +11.0 [+4.4, +18.0] | no detectable difference |
| zoomLarge / zoom4SettleMs | 162.3 [184.3] | 171.2 [198.6] | +8.9 [-8.3, +25.4] | no detectable difference |
| zoomLarge / backTo1SettleMs | 88.6 [105.2] | 92.8 [113.5] | +4.2 [-2.7, +13.4] | no detectable difference |
| zoomLarge / mainTaskMs | 268.4 [276.6] | 256.1 [270.6] | 0.954 [0.933, 0.992] | faster |
| zoomLarge / mainJsMs | 226.3 [233.1] | 214.4 [227.8] | 0.948 [0.926, 0.993] | faster |
| select / settleMs | 211.8 [268.5] | 207.9 [264.6] | -3.9 [-42.5, +33.7] | no detectable difference |
| select / mainTaskMs | 80.9 [91.3] | 82.5 [90.8] | 1.019 [0.931, 1.088] | no detectable difference |
| select / mainJsMs | 64.2 [72.1] | 64.7 [72.1] | 1.007 [0.924, 1.078] | no detectable difference |
| selectLarge / settleMs | 238.4 [278.2] | 244.5 [270.4] | +6.2 [-21.4, +30.2] | no detectable difference |
| selectLarge / mainTaskMs | 112.1 [121.9] | 112.2 [115.6] | 1.001 [0.961, 1.045] | no detectable difference |
| selectLarge / mainJsMs | 87.4 [95.6] | 88.0 [90.8] | 1.007 [0.956, 1.053] | no detectable difference |
| search / searchMs | 81.8 [84.6] | 81.9 [84.5] | 1.001 [0.964, 1.082] | no detectable difference |
| search / mainTaskMs | 15.8 [21.3] | 19.1 [20.6] | 1.214 [0.772, 1.877] | no detectable difference |
| search / mainJsMs | 7.9 [8.9] | 8.5 [10.0] | 1.080 [0.959, 1.189] | no detectable difference |
| searchLarge / searchMs | 2076.4 [2079.6] | 1882.3 [1892.2] | 0.907 [0.904, 0.910] | faster |
| searchLarge / mainTaskMs | 1112.2 [1118.9] | 909.4 [915.0] | 0.818 [0.815, 0.822] | faster |
| searchLarge / mainJsMs | 1061.1 [1069.5] | 862.4 [868.0] | 0.813 [0.810, 0.816] | faster |
| annotate / settleMs | 226.3 [277.7] | 221.4 [247.8] | -4.9 [-49.3, +19.6] | no detectable difference |
| annotate / mainTaskMs | 103.3 [112.9] | 102.2 [107.6] | 0.989 [0.945, 1.022] | no detectable difference |
| annotate / mainJsMs | 75.4 [83.5] | 75.0 [79.3] | 0.995 [0.939, 1.015] | no detectable difference |
| annotateLarge / settleMs | 230.8 [278.0] | 225.4 [262.4] | -5.4 [-37.8, +20.0] | no detectable difference |
| annotateLarge / mainTaskMs | 124.9 [131.9] | 126.1 [132.9] | 1.010 [0.963, 1.049] | no detectable difference |
| annotateLarge / mainJsMs | 83.9 [89.0] | 83.5 [90.0] | 0.995 [0.948, 1.051] | no detectable difference |

## `plugins-promise` vs `plugins-typed`

| Row | plugins-promise median [p95] | plugins-typed median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 90.0 [102.5] | 93.2 [103.5] | 1.036 [0.957, 1.130] | no detectable difference |
| open(ebook) / openedMs | 210.4 [222.9] | 212.5 [223.6] | 1.010 [0.964, 1.052] | no detectable difference |
| open(ebook) / firstPaintMs | 365.6 [386.7] | 373.7 [380.6] | 1.022 [0.974, 1.040] | no detectable difference |
| open(ebook) / settledMs | 427.5 [450.0] | 428.7 [445.6] | 1.003 [0.972, 1.034] | no detectable difference |
| open(ebook) / mainTaskMs | 292.8 [302.1] | 288.9 [295.2] | 0.987 [0.964, 1.024] | no detectable difference |
| open(ebook) / mainJsMs | 243.1 [249.5] | 238.7 [243.0] | 0.982 [0.961, 1.025] | no detectable difference |
| open(large) / readyMs | 90.8 [109.5] | 94.1 [104.0] | 1.036 [0.961, 1.118] | no detectable difference |
| open(large) / openedMs | 240.7 [260.2] | 246.6 [254.4] | 1.025 [0.993, 1.052] | no detectable difference |
| open(large) / firstPaintMs | 418.6 [436.9] | 422.6 [447.0] | 1.009 [0.986, 1.058] | no detectable difference |
| open(large) / settledMs | 416.2 [435.5] | 421.3 [433.3] | 1.012 [0.974, 1.039] | no detectable difference |
| open(large) / mainTaskMs | 574.6 [584.9] | 573.3 [588.7] | 0.998 [0.982, 1.011] | no detectable difference |
| open(large) / mainJsMs | 524.6 [536.0] | 527.1 [537.6] | 1.005 [0.984, 1.012] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 3068.0 [3126.7] | 3017.1 [3065.6] | 0.983 [0.969, 1.004] | no detectable difference |
| scroll / frameP50Ms | 11.3 [11.5] | 11.2 [11.7] | 0.987 [0.964, 1.026] | no detectable difference |
| scroll / frameP95Ms | 27.1 [27.2] | 24.4 [27.2] | 0.902 [0.844, 1.023] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 880.4 [888.4] | 848.7 [864.5] | 0.964 [0.958, 0.982] | faster |
| scroll / mainJsMs | 747.0 [757.9] | 720.3 [732.4] | 0.964 [0.956, 0.977] | faster |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 3014.1 [3119.4] | 2997.2 [3113.9] | 0.994 [0.977, 1.018] | no detectable difference |
| coldScroll / frameP50Ms | 11.0 [11.3] | 11.1 [11.6] | 1.011 [0.988, 1.039] | no detectable difference |
| coldScroll / frameP95Ms | 24.6 [27.2] | 26.1 [27.2] | 1.062 [0.925, 1.122] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 876.6 [892.1] | 840.4 [861.5] | 0.959 [0.948, 0.973] | faster |
| coldScroll / mainJsMs | 729.5 [737.8] | 701.5 [720.3] | 0.962 [0.955, 0.984] | faster |
| jump / settleMs | 161.2 [198.1] | 150.2 [173.8] | -11.0 [-39.2, +10.8] | no detectable difference |
| jump / mainTaskMs | 171.1 [178.2] | 149.8 [156.1] | 0.876 [0.856, 0.904] | faster |
| jump / mainJsMs | 154.1 [161.6] | 134.5 [140.6] | 0.873 [0.841, 0.900] | faster |
| zoom / settleMs | 513.6 [535.8] | 516.7 [533.4] | +3.0 [-8.5, +16.2] | no detectable difference |
| zoom / zoom2SettleMs | 188.7 [213.1] | 190.6 [206.5] | +1.9 [-10.3, +15.6] | no detectable difference |
| zoom / zoom4SettleMs | 320.6 [329.8] | 323.7 [332.3] | +3.0 [-9.1, +13.1] | no detectable difference |
| zoom / backTo1SettleMs | 105.0 [127.9] | 108.5 [129.1] | +3.5 [-19.8, +21.4] | no detectable difference |
| zoom / mainTaskMs | 326.3 [341.5] | 330.8 [347.6] | 1.014 [0.976, 1.048] | no detectable difference |
| zoom / mainJsMs | 272.6 [286.2] | 273.6 [291.9] | 1.004 [0.977, 1.042] | no detectable difference |
| zoomLarge / settleMs | 320.7 [346.5] | 314.3 [350.4] | -6.4 [-23.6, +25.0] | no detectable difference |
| zoomLarge / zoom2SettleMs | 148.8 [158.0] | 140.1 [163.9] | -8.8 [-23.5, +8.2] | no detectable difference |
| zoomLarge / zoom4SettleMs | 171.2 [198.6] | 181.1 [196.5] | +9.9 [-7.5, +22.5] | no detectable difference |
| zoomLarge / backTo1SettleMs | 92.8 [113.5] | 87.4 [99.2] | -5.4 [-15.0, +0.3] | no detectable difference |
| zoomLarge / mainTaskMs | 256.1 [270.6] | 264.6 [279.2] | 1.033 [0.992, 1.069] | no detectable difference |
| zoomLarge / mainJsMs | 214.4 [227.8] | 222.7 [234.3] | 1.038 [0.988, 1.074] | no detectable difference |
| select / settleMs | 207.9 [264.6] | 237.0 [295.9] | +29.1 [-4.7, +48.9] | no detectable difference |
| select / mainTaskMs | 82.5 [90.8] | 85.4 [100.0] | 1.036 [0.963, 1.116] | no detectable difference |
| select / mainJsMs | 64.7 [72.1] | 67.8 [80.1] | 1.048 [0.962, 1.133] | no detectable difference |
| selectLarge / settleMs | 244.5 [270.4] | 247.8 [263.4] | +3.2 [-21.8, +22.5] | no detectable difference |
| selectLarge / mainTaskMs | 112.2 [115.6] | 111.9 [118.3] | 0.997 [0.966, 1.041] | no detectable difference |
| selectLarge / mainJsMs | 88.0 [90.8] | 87.6 [92.4] | 0.996 [0.959, 1.041] | no detectable difference |
| search / searchMs | 81.9 [84.5] | 84.3 [85.0] | 1.030 [0.899, 1.049] | no detectable difference |
| search / mainTaskMs | 19.1 [20.6] | 20.6 [21.3] | 1.074 [0.509, 1.321] | no detectable difference |
| search / mainJsMs | 8.5 [10.0] | 8.5 [8.6] | 1.003 [0.794, 1.035] | no detectable difference |
| searchLarge / searchMs | 1882.3 [1892.2] | 1885.1 [1908.5] | 1.001 [0.997, 1.004] | no detectable difference |
| searchLarge / mainTaskMs | 909.4 [915.0] | 914.5 [921.2] | 1.006 [0.999, 1.011] | no detectable difference |
| searchLarge / mainJsMs | 862.4 [868.0] | 866.6 [871.4] | 1.005 [0.999, 1.010] | no detectable difference |
| annotate / settleMs | 221.4 [247.8] | 216.7 [242.9] | -4.7 [-23.1, +20.2] | no detectable difference |
| annotate / mainTaskMs | 102.2 [107.6] | 103.1 [107.1] | 1.009 [0.982, 1.031] | no detectable difference |
| annotate / mainJsMs | 75.0 [79.3] | 75.6 [79.1] | 1.007 [0.985, 1.033] | no detectable difference |
| annotateLarge / settleMs | 225.4 [262.4] | 236.8 [263.4] | +11.4 [-7.2, +38.7] | no detectable difference |
| annotateLarge / mainTaskMs | 126.1 [132.9] | 128.0 [133.0] | 1.015 [0.971, 1.057] | no detectable difference |
| annotateLarge / mainJsMs | 83.5 [90.0] | 84.0 [89.1] | 1.006 [0.966, 1.071] | no detectable difference |

## `plugins-sharedhits` vs `plugins-typed`

| Row | plugins-sharedhits median [p95] | plugins-typed median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 93.6 [167.1] | 93.2 [103.5] | 0.996 [0.720, 1.109] | no detectable difference |
| open(ebook) / openedMs | 215.8 [343.8] | 212.5 [223.6] | 0.985 [0.764, 1.034] | no detectable difference |
| open(ebook) / firstPaintMs | 369.5 [497.0] | 373.7 [380.6] | 1.011 [0.861, 1.030] | no detectable difference |
| open(ebook) / settledMs | 438.3 [560.9] | 428.7 [445.6] | 0.978 [0.864, 1.018] | no detectable difference |
| open(ebook) / mainTaskMs | 292.9 [302.8] | 288.9 [295.2] | 0.987 [0.967, 1.000] | no detectable difference |
| open(ebook) / mainJsMs | 241.7 [250.5] | 238.7 [243.0] | 0.988 [0.965, 1.003] | no detectable difference |
| open(large) / readyMs | 90.1 [94.3] | 94.1 [104.0] | 1.045 [1.010, 1.121] | no detectable difference |
| open(large) / openedMs | 241.7 [249.5] | 246.6 [254.4] | 1.020 [0.999, 1.048] | no detectable difference |
| open(large) / firstPaintMs | 412.3 [426.1] | 422.6 [447.0] | 1.025 [1.005, 1.070] | no detectable difference |
| open(large) / settledMs | 416.8 [428.7] | 421.3 [433.3] | 1.011 [0.996, 1.037] | no detectable difference |
| open(large) / mainTaskMs | 573.0 [582.3] | 573.3 [588.7] | 1.001 [0.985, 1.020] | no detectable difference |
| open(large) / mainJsMs | 525.4 [534.0] | 527.1 [537.6] | 1.003 [0.984, 1.025] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 3055.8 [3162.3] | 3017.1 [3065.6] | 0.987 [0.977, 1.000] | no detectable difference |
| scroll / frameP50Ms | 11.1 [11.7] | 11.2 [11.7] | 1.006 [0.971, 1.038] | no detectable difference |
| scroll / frameP95Ms | 26.5 [27.2] | 24.4 [27.2] | 0.923 [0.863, 1.044] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 909.3 [935.0] | 848.7 [864.5] | 0.933 [0.916, 0.947] | faster |
| scroll / mainJsMs | 770.5 [792.9] | 720.3 [732.4] | 0.935 [0.915, 0.950] | faster |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 3062.8 [3167.9] | 2997.2 [3113.9] | 0.979 [0.956, 1.000] | no detectable difference |
| coldScroll / frameP50Ms | 11.5 [11.7] | 11.1 [11.6] | 0.967 [0.950, 1.013] | no detectable difference |
| coldScroll / frameP95Ms | 26.9 [27.2] | 26.1 [27.2] | 0.972 [0.886, 1.037] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 913.4 [948.1] | 840.4 [861.5] | 0.920 [0.907, 0.940] | faster |
| coldScroll / mainJsMs | 755.6 [792.5] | 701.5 [720.3] | 0.928 [0.917, 0.953] | faster |
| jump / settleMs | 174.8 [211.9] | 150.2 [173.8] | -24.7 [-44.7, -5.0] | faster |
| jump / mainTaskMs | 191.7 [199.7] | 149.8 [156.1] | 0.781 [0.763, 0.807] | faster |
| jump / mainJsMs | 175.8 [183.5] | 134.5 [140.6] | 0.765 [0.742, 0.790] | faster |
| zoom / settleMs | 518.0 [538.4] | 516.7 [533.4] | -1.4 [-14.9, +7.5] | no detectable difference |
| zoom / zoom2SettleMs | 193.3 [214.3] | 190.6 [206.5] | -2.7 [-12.6, +11.0] | no detectable difference |
| zoom / zoom4SettleMs | 327.0 [332.4] | 323.7 [332.3] | -3.4 [-11.6, +8.1] | no detectable difference |
| zoom / backTo1SettleMs | 105.6 [117.2] | 108.5 [129.1] | +2.9 [-5.6, +21.0] | no detectable difference |
| zoom / mainTaskMs | 332.9 [345.9] | 330.8 [347.6] | 0.994 [0.960, 1.030] | no detectable difference |
| zoom / mainJsMs | 276.8 [289.9] | 273.6 [291.9] | 0.988 [0.947, 1.041] | no detectable difference |
| zoomLarge / settleMs | 299.4 [324.6] | 314.3 [350.4] | +14.9 [-1.0, +45.9] | no detectable difference |
| zoomLarge / zoom2SettleMs | 137.8 [145.0] | 140.1 [163.9] | +2.3 [-12.7, +18.8] | no detectable difference |
| zoomLarge / zoom4SettleMs | 162.3 [184.3] | 181.1 [196.5] | +18.8 [-2.8, +35.6] | no detectable difference |
| zoomLarge / backTo1SettleMs | 88.6 [105.2] | 87.4 [99.2] | -1.2 [-8.3, +3.5] | no detectable difference |
| zoomLarge / mainTaskMs | 268.4 [276.6] | 264.6 [279.2] | 0.986 [0.954, 1.026] | no detectable difference |
| zoomLarge / mainJsMs | 226.3 [233.1] | 222.7 [234.3] | 0.984 [0.945, 1.027] | no detectable difference |
| select / settleMs | 211.8 [268.5] | 237.0 [295.9] | +25.2 [-17.1, +54.0] | no detectable difference |
| select / mainTaskMs | 80.9 [91.3] | 85.4 [100.0] | 1.056 [0.964, 1.124] | no detectable difference |
| select / mainJsMs | 64.2 [72.1] | 67.8 [80.1] | 1.056 [0.957, 1.132] | no detectable difference |
| selectLarge / settleMs | 238.4 [278.2] | 247.8 [263.4] | +9.4 [-20.7, +30.9] | no detectable difference |
| selectLarge / mainTaskMs | 112.1 [121.9] | 111.9 [118.3] | 0.998 [0.970, 1.044] | no detectable difference |
| selectLarge / mainJsMs | 87.4 [95.6] | 87.6 [92.4] | 1.003 [0.965, 1.043] | no detectable difference |
| search / searchMs | 81.8 [84.6] | 84.3 [85.0] | 1.031 [0.903, 1.098] | no detectable difference |
| search / mainTaskMs | 15.8 [21.3] | 20.6 [21.3] | 1.304 [0.592, 1.930] | no detectable difference |
| search / mainJsMs | 7.9 [8.9] | 8.5 [8.6] | 1.083 [0.847, 1.184] | no detectable difference |
| searchLarge / searchMs | 2076.4 [2079.6] | 1885.1 [1908.5] | 0.908 [0.906, 0.909] | faster |
| searchLarge / mainTaskMs | 1112.2 [1118.9] | 914.5 [921.2] | 0.822 [0.818, 0.828] | faster |
| searchLarge / mainJsMs | 1061.1 [1069.5] | 866.6 [871.4] | 0.817 [0.813, 0.820] | faster |
| annotate / settleMs | 226.3 [277.7] | 216.7 [242.9] | -9.6 [-54.1, +19.6] | no detectable difference |
| annotate / mainTaskMs | 103.3 [112.9] | 103.1 [107.1] | 0.998 [0.950, 1.028] | no detectable difference |
| annotate / mainJsMs | 75.4 [83.5] | 75.6 [79.1] | 1.002 [0.947, 1.025] | no detectable difference |
| annotateLarge / settleMs | 230.8 [278.0] | 236.8 [263.4] | +5.9 [-14.2, +29.8] | no detectable difference |
| annotateLarge / mainTaskMs | 124.9 [131.9] | 128.0 [133.0] | 1.024 [0.980, 1.055] | no detectable difference |
| annotateLarge / mainJsMs | 83.9 [89.0] | 84.0 [89.1] | 1.002 [0.968, 1.060] | no detectable difference |

## `main` vs `plugins-typed`

| Row | main median [p95] | plugins-typed median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 63.4 [71.7] | 93.2 [103.5] | 1.469 [1.397, 1.626] | slower |
| open(ebook) / openedMs | 137.8 [151.8] | 212.5 [223.6] | 1.542 [1.398, 1.602] | slower |
| open(ebook) / firstPaintMs | 263.7 [273.4] | 373.7 [380.6] | 1.417 [1.375, 1.482] | slower |
| open(ebook) / settledMs | 395.2 [409.5] | 428.7 [445.6] | 1.085 [1.047, 1.126] | slower |
| open(ebook) / mainTaskMs | 143.3 [147.7] | 288.9 [295.2] | 2.016 [1.964, 2.066] | slower |
| open(ebook) / mainJsMs | 110.5 [113.8] | 238.7 [243.0] | 2.161 [2.113, 2.218] | slower |
| open(large) / readyMs | 64.1 [73.8] | 94.1 [104.0] | 1.469 [1.406, 1.596] | slower |
| open(large) / openedMs | 159.0 [168.2] | 246.6 [254.4] | 1.551 [1.510, 1.595] | slower |
| open(large) / firstPaintMs | 266.5 [281.8] | 422.6 [447.0] | 1.586 [1.548, 1.671] | slower |
| open(large) / settledMs | 269.5 [276.7] | 421.3 [433.3] | 1.563 [1.525, 1.616] | slower |
| open(large) / mainTaskMs | 314.2 [316.5] | 573.3 [588.7] | 1.825 [1.806, 1.850] | slower |
| open(large) / mainJsMs | 283.3 [285.2] | 527.1 [537.6] | 1.860 [1.830, 1.874] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2904.9 [2922.5] | 3017.1 [3065.6] | 1.039 [1.027, 1.058] | slower |
| scroll / frameP50Ms | 11.0 [11.5] | 11.2 [11.7] | 1.017 [0.988, 1.058] | no detectable difference |
| scroll / frameP95Ms | 22.2 [26.7] | 24.4 [27.2] | 1.103 [0.920, 1.243] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 547.9 [562.4] | 848.7 [864.5] | 1.549 [1.528, 1.568] | slower |
| scroll / mainJsMs | 410.7 [417.4] | 720.3 [732.4] | 1.754 [1.734, 1.774] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / wallMs | 2831.3 [2913.1] | 2997.2 [3113.9] | 1.059 [1.033, 1.075] | slower |
| coldScroll / frameP50Ms | 11.1 [11.6] | 11.1 [11.6] | 1.004 [0.970, 1.029] | no detectable difference |
| coldScroll / frameP95Ms | 25.0 [26.5] | 26.1 [27.2] | 1.047 [0.952, 1.197] | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 569.5 [582.0] | 840.4 [861.5] | 1.476 [1.458, 1.505] | slower |
| coldScroll / mainJsMs | 398.7 [406.7] | 701.5 [720.3] | 1.759 [1.744, 1.806] | slower |
| jump / settleMs | 125.0 [132.4] | 150.2 [173.8] | +25.1 [+18.1, +43.0] | slower |
| jump / mainTaskMs | 70.2 [77.8] | 149.8 [156.1] | 2.135 [2.026, 2.219] | slower |
| jump / mainJsMs | 53.1 [57.3] | 134.5 [140.6] | 2.532 [2.390, 2.621] | slower |
| zoom / settleMs | 443.3 [491.3] | 516.7 [533.4] | +73.4 [+40.4, +87.7] | slower |
| zoom / zoom2SettleMs | 164.7 [189.9] | 190.6 [206.5] | +25.9 [+13.2, +39.6] | slower |
| zoom / zoom4SettleMs | 283.1 [304.5] | 323.7 [332.3] | +40.5 [+23.2, +61.5] | slower |
| zoom / backTo1SettleMs | 81.2 [96.8] | 108.5 [129.1] | +27.3 [+16.3, +45.2] | slower |
| zoom / mainTaskMs | 214.9 [228.0] | 330.8 [347.6] | 1.539 [1.497, 1.604] | slower |
| zoom / mainJsMs | 155.1 [165.5] | 273.6 [291.9] | 1.764 [1.720, 1.857] | slower |
| zoomLarge / settleMs | 291.4 [334.5] | 314.3 [350.4] | +22.9 [-4.7, +55.0] | no detectable difference |
| zoomLarge / zoom2SettleMs | 140.9 [159.8] | 140.1 [163.9] | -0.8 [-17.6, +21.9] | no detectable difference |
| zoomLarge / zoom4SettleMs | 158.6 [174.7] | 181.1 [196.5] | +22.5 [+0.5, +42.2] | slower |
| zoomLarge / backTo1SettleMs | 72.4 [84.6] | 87.4 [99.2] | +15.0 [+6.0, +22.0] | no detectable difference |
| zoomLarge / mainTaskMs | 177.1 [186.3] | 264.6 [279.2] | 1.494 [1.445, 1.541] | slower |
| zoomLarge / mainJsMs | 132.4 [138.8] | 222.7 [234.3] | 1.682 [1.619, 1.738] | slower |
| select / settleMs | 215.9 [252.0] | 237.0 [295.9] | +21.2 [-12.3, +49.3] | no detectable difference |
| select / mainTaskMs | 61.3 [67.0] | 85.4 [100.0] | 1.394 [1.321, 1.469] | slower |
| select / mainJsMs | 39.5 [43.4] | 67.8 [80.1] | 1.714 [1.596, 1.840] | slower |
| selectLarge / settleMs | 209.1 [247.4] | 247.8 [263.4] | +38.7 [+11.1, +48.6] | slower |
| selectLarge / mainTaskMs | 70.6 [75.5] | 111.9 [118.3] | 1.585 [1.529, 1.736] | slower |
| selectLarge / mainJsMs | 47.6 [50.8] | 87.6 [92.4] | 1.839 [1.761, 2.023] | slower |
| search / searchMs | 69.8 [77.7] | 84.3 [85.0] | 1.208 [1.042, 1.283] | slower |
| search / mainTaskMs | 5.2 [6.4] | 20.6 [21.3] | 3.989 [1.871, 4.102] | slower |
| search / mainJsMs | 3.6 [4.1] | 8.5 [8.6] | 2.376 [1.873, 2.415] | slower |
| searchLarge / searchMs | 1897.5 [1933.3] | 1885.1 [1908.5] | 0.993 [0.978, 0.998] | no detectable difference |
| searchLarge / mainTaskMs | 345.2 [349.3] | 914.5 [921.2] | 2.649 [2.625, 2.674] | slower |
| searchLarge / mainJsMs | 291.7 [294.6] | 866.6 [871.4] | 2.971 [2.952, 2.987] | slower |
| annotate / settleMs | 216.1 [268.2] | 216.7 [242.9] | +0.6 [-36.4, +31.5] | no detectable difference |
| annotate / mainTaskMs | 77.9 [83.5] | 103.1 [107.1] | 1.324 [1.256, 1.389] | slower |
| annotate / mainJsMs | 46.1 [50.4] | 75.6 [79.1] | 1.639 [1.557, 1.705] | slower |
| annotateLarge / settleMs | 207.2 [251.5] | 236.8 [263.4] | +29.6 [-11.5, +52.5] | no detectable difference |
| annotateLarge / mainTaskMs | 79.4 [86.9] | 128.0 [133.0] | 1.611 [1.479, 1.667] | slower |
| annotateLarge / mainJsMs | 48.2 [51.8] | 84.0 [89.1] | 1.744 [1.624, 1.861] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-sharedhits median [p95] | plugins-promise median [p95] | plugins-typed median [p95] |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.85 MiB [35.35 MiB] | 35.35 MiB [35.36 MiB] | 42.34 MiB [42.35 MiB] | 42.34 MiB [42.90 MiB] | 42.34 MiB [42.90 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.16 MiB [48.16 MiB] | 48.15 MiB [48.16 MiB] | 54.99 MiB [55.11 MiB] | 54.94 MiB [55.10 MiB] | 54.99 MiB [55.11 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.28 MiB] | 22.26 MiB [22.26 MiB] | 22.96 MiB [22.96 MiB] | 22.96 MiB [22.96 MiB] | 22.96 MiB [22.96 MiB] |
| soak / heapCycle5Bytes | 37.42 MiB [38.42 MiB] | 38.42 MiB [38.43 MiB] | 45.13 MiB [45.15 MiB] | 45.13 MiB [46.15 MiB] | 45.14 MiB [45.16 MiB] |
| soak / heapCycle30Bytes | 38.26 MiB [39.27 MiB] | 39.26 MiB [39.27 MiB] | 45.68 MiB [45.69 MiB] | 45.68 MiB [46.68 MiB] | 45.68 MiB [45.70 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

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

`plugins-promise`:

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

`plugins-typed`:

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

