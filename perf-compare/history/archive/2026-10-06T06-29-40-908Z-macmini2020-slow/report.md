# Viewer perf comparison: 2026-10-06T06-29-40-908Z-macmini2020-slow

10 rounds, profile `slow` (CPU throttle 4x).

The throttle reached the main thread only: Chromium refuses `Emulation.setCPUThrottlingRate` on the engine worker, so worker-side work ran at full speed.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | e07c025c69ee02bbd6e1addd06e563e24ac13b34 |
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
| plugins-hostbytes | research/rust-kernel-host-bytes | 81899a80a | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 00de09f8d708 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-hostbytes`: pass

10 samples failed and are left out:

- plugins-hostbytes selectLarge(large): the drag selected no text (x10)

## Sizes

| Metric | main | main-aa | plugins-hostbytes |
| --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 8.03 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.72 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

62 of 63 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 175.7 [178.4] | 176.1 [179.8] | 1.002 [0.989, 1.013] | pass |
| open(ebook) / openedMs | 315.1 [327.4] | 313.9 [319.5] | 0.996 [0.986, 1.008] | pass |
| open(ebook) / firstPaintMs | 549.5 [560.0] | 547.7 [570.5] | 0.997 [0.970, 1.029] | pass |
| open(ebook) / settledMs | 724.5 [750.7] | 724.7 [745.2] | 1.000 [0.973, 1.026] | pass |
| open(ebook) / mainTaskMs | 541.8 [568.3] | 549.1 [571.4] | 1.014 [0.970, 1.038] | pass |
| open(ebook) / mainJsMs | 430.1 [451.5] | 434.6 [453.1] | 1.010 [0.960, 1.040] | pass |
| open(large) / readyMs | 177.4 [179.0] | 175.4 [176.6] | 0.989 [0.982, 1.000] | pass |
| open(large) / openedMs | 376.5 [379.2] | 372.9 [377.0] | 0.991 [0.982, 0.998] | pass |
| open(large) / firstPaintMs | 591.3 [618.4] | 590.8 [608.5] | 0.999 [0.966, 1.013] | pass |
| open(large) / settledMs | 614.1 [621.1] | 613.9 [620.7] | 1.000 [0.980, 1.013] | pass |
| open(large) / mainTaskMs | 790.7 [803.2] | 796.4 [800.1] | 1.007 [0.980, 1.025] | pass |
| open(large) / mainJsMs | 684.9 [699.8] | 690.3 [696.7] | 1.008 [0.986, 1.023] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 3053.2 [3181.0] | 3079.3 [3174.4] | 1.009 [0.979, 1.041] | pass |
| scroll / frameP50Ms | 11.8 [12.4] | 12.1 [12.4] | 1.026 [0.990, 1.048] | pass |
| scroll / frameP95Ms | 25.9 [26.3] | 26.0 [26.3] | 1.005 [0.994, 1.013] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 1292.8 [1337.4] | 1306.0 [1347.5] | 1.010 [0.987, 1.027] | pass |
| scroll / mainJsMs | 959.0 [975.3] | 958.2 [972.6] | 0.999 [0.983, 1.011] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 3195.9 [3261.8] | 3167.0 [3260.9] | 0.991 [0.974, 1.015] | pass |
| coldScroll / frameP50Ms | 12.3 [12.5] | 11.8 [12.8] | 0.964 [0.947, 0.999] | A/A failed |
| coldScroll / frameP95Ms | 25.9 [26.4] | 26.1 [26.4] | 1.006 [0.988, 1.015] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 1537.6 [1667.8] | 1517.2 [1562.4] | 0.987 [0.966, 1.012] | pass |
| coldScroll / mainJsMs | 1131.0 [1242.3] | 1121.9 [1160.8] | 0.992 [0.968, 1.011] | pass |
| jump / settleMs | 188.6 [210.4] | 180.8 [206.4] | -7.8 [-21.2, +5.6] | pass |
| jump / mainTaskMs | 229.3 [245.5] | 212.7 [248.9] | 0.928 [0.878, 1.032] | pass |
| jump / mainJsMs | 168.4 [180.5] | 169.8 [187.2] | 1.008 [0.912, 1.054] | pass |
| zoom / settleMs | 617.3 [637.5] | 617.9 [651.5] | +0.6 [-17.3, +23.5] | pass |
| zoom / zoom2SettleMs | 260.1 [279.3] | 255.7 [273.3] | -4.5 [-17.1, +18.2] | pass |
| zoom / zoom4SettleMs | 355.9 [375.9] | 362.2 [383.6] | +6.3 [-5.5, +19.9] | pass |
| zoom / backTo1SettleMs | 153.5 [156.6] | 154.8 [160.2] | +1.3 [-3.9, +3.4] | pass |
| zoom / mainTaskMs | 756.5 [783.9] | 762.5 [809.7] | 1.008 [0.952, 1.038] | pass |
| zoom / mainJsMs | 566.8 [590.3] | 572.4 [607.2] | 1.010 [0.954, 1.053] | pass |
| zoomLarge / settleMs | 393.2 [423.0] | 386.8 [424.6] | -6.3 [-22.9, +27.6] | pass |
| zoomLarge / zoom2SettleMs | 170.8 [200.5] | 176.3 [185.8] | +5.5 [-20.3, +22.0] | pass |
| zoomLarge / zoom4SettleMs | 219.2 [242.3] | 213.3 [251.0] | -6.0 [-22.4, +21.5] | pass |
| zoomLarge / backTo1SettleMs | 157.0 [169.0] | 155.5 [171.0] | -1.5 [-8.9, +4.9] | pass |
| zoomLarge / mainTaskMs | 497.7 [543.0] | 517.7 [552.3] | 1.040 [0.960, 1.081] | pass |
| zoomLarge / mainJsMs | 373.9 [412.7] | 391.1 [424.7] | 1.046 [0.955, 1.107] | pass |
| select / settleMs | 236.0 [246.8] | 231.2 [247.8] | -4.9 [-17.2, +13.3] | pass |
| select / mainTaskMs | 85.3 [88.6] | 86.3 [88.1] | 1.012 [0.992, 1.032] | pass |
| select / mainJsMs | 56.5 [57.3] | 57.2 [58.2] | 1.012 [0.976, 1.040] | pass |
| selectLarge / settleMs | 225.8 [267.1] | 245.5 [269.6] | +19.7 [-5.5, +35.3] | pass |
| selectLarge / mainTaskMs | 97.4 [105.1] | 98.0 [104.4] | 1.007 [0.950, 1.068] | pass |
| selectLarge / mainJsMs | 67.3 [73.2] | 68.4 [70.9] | 1.017 [0.970, 1.052] | pass |
| search / searchMs | 60.6 [61.9] | 60.5 [61.7] | 0.998 [0.990, 1.015] | pass |
| search / mainTaskMs | 17.4 [19.4] | 18.3 [19.0] | 1.052 [0.974, 1.112] | pass |
| search / mainJsMs | 12.5 [13.1] | 12.1 [13.5] | 0.965 [0.935, 1.044] | pass |
| searchLarge / searchMs | 2687.5 [2848.4] | 2617.9 [2801.9] | 0.974 [0.917, 1.039] | pass |
| searchLarge / mainTaskMs | 1292.5 [1428.0] | 1212.0 [1429.6] | 0.938 [0.853, 1.100] | pass |
| searchLarge / mainJsMs | 1152.7 [1285.6] | 1078.5 [1290.9] | 0.936 [0.836, 1.117] | pass |
| annotate / settleMs | 281.0 [307.0] | 288.1 [303.5] | +7.1 [-15.0, +21.0] | pass |
| annotate / mainTaskMs | 162.3 [164.0] | 163.3 [165.9] | 1.006 [0.992, 1.027] | pass |
| annotate / mainJsMs | 97.2 [100.8] | 99.2 [102.2] | 1.021 [0.999, 1.044] | pass |
| annotateLarge / settleMs | 275.7 [299.2] | 275.5 [315.3] | -0.2 [-23.8, +32.3] | pass |
| annotateLarge / mainTaskMs | 163.5 [166.4] | 164.0 [165.6] | 1.003 [0.986, 1.032] | pass |
| annotateLarge / mainJsMs | 100.5 [101.7] | 101.0 [104.0] | 1.005 [0.975, 1.027] | pass |

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

Not run: `plugins-tip4`.

## `main` vs `plugins-hostbytes`

| Row | main median [p95] | plugins-hostbytes median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 175.7 [178.4] | 317.0 [321.1] | 1.804 [1.784, 1.825] | slower |
| open(ebook) / openedMs | 315.1 [327.4] | 492.1 [495.0] | 1.562 [1.544, 1.576] | slower |
| open(ebook) / firstPaintMs | 549.5 [560.0] | 888.3 [925.8] | 1.617 [1.564, 1.652] | slower |
| open(ebook) / settledMs | 724.5 [750.7] | 994.3 [1026.6] | 1.372 [1.334, 1.414] | slower |
| open(ebook) / mainTaskMs | 541.8 [568.3] | 1010.3 [1056.4] | 1.865 [1.806, 1.913] | slower |
| open(ebook) / mainJsMs | 430.1 [451.5] | 839.4 [885.5] | 1.952 [1.895, 2.008] | slower |
| open(large) / readyMs | 177.4 [179.0] | 315.1 [318.9] | 1.777 [1.763, 1.797] | slower |
| open(large) / openedMs | 376.5 [379.2] | 574.3 [580.4] | 1.525 [1.513, 1.537] | slower |
| open(large) / firstPaintMs | 591.3 [618.4] | 972.8 [986.3] | 1.645 [1.601, 1.660] | slower |
| open(large) / settledMs | 614.1 [621.1] | 1019.1 [1039.3] | 1.659 [1.641, 1.680] | slower |
| open(large) / mainTaskMs | 790.7 [803.2] | 1196.1 [1227.1] | 1.513 [1.494, 1.548] | slower |
| open(large) / mainJsMs | 684.9 [699.8] | 1041.0 [1065.2] | 1.520 [1.500, 1.551] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 3053.2 [3181.0] | 4150.0 [4354.6] | 1.359 [1.326, 1.395] | slower |
| scroll / frameP50Ms | 11.8 [12.4] | 13.6 [14.5] | 1.151 [1.130, 1.197] | slower |
| scroll / frameP95Ms | 25.9 [26.3] | 29.8 [37.5] | 1.153 [1.057, 1.299] | slower |
| scroll / jankFrames | 0 [0] | 1 [3.65] | +1 [+1, +2] | slower |
| scroll / longTasks | 0 [0] | 0 [1] | +0 [+0, +1] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [57.2] | +0.0 [+0.0, +51.0] | no detectable difference |
| scroll / mainTaskMs | 1292.8 [1337.4] | 3077.5 [3517.9] | 2.381 [2.326, 2.422] | slower |
| scroll / mainJsMs | 959.0 [975.3] | 2635.4 [3041.7] | 2.748 [2.698, 2.816] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [22.6] | +0.0 [+0.0, +11.3] | no detectable difference |
| coldScroll / wallMs | 3195.9 [3261.8] | 4046.2 [4110.9] | 1.266 [1.246, 1.295] | slower |
| coldScroll / frameP50Ms | 12.3 [12.5] | 13.9 [14.2] | 1.129 [1.086, 1.158] | unreliable (A/A failed) |
| coldScroll / frameP95Ms | 25.9 [26.4] | 27.8 [33.5] | 1.073 [1.043, 1.190] | slower |
| coldScroll / jankFrames | 0 [0] | 1.50 [3.55] | +1.50 [+1, +2.50] | slower |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 1537.6 [1667.8] | 3880.6 [3930.8] | 2.524 [2.479, 2.564] | slower |
| coldScroll / mainJsMs | 1131.0 [1242.3] | 3425.9 [3465.8] | 3.029 [2.969, 3.082] | slower |
| jump / settleMs | 188.6 [210.4] | 330.5 [409.1] | +141.9 [+91.9, +178.4] | slower |
| jump / mainTaskMs | 229.3 [245.5] | 581.5 [665.7] | 2.536 [2.293, 2.728] | slower |
| jump / mainJsMs | 168.4 [180.5] | 516.2 [589.5] | 3.065 [2.787, 3.275] | slower |
| zoom / settleMs | 617.3 [637.5] | 897.4 [935.8] | +280.1 [+251.8, +303.6] | slower |
| zoom / zoom2SettleMs | 260.1 [279.3] | 374.6 [390.3] | +114.5 [+95.6, +139.1] | slower |
| zoom / zoom4SettleMs | 355.9 [375.9] | 526.5 [557.8] | +170.6 [+150.0, +189.0] | slower |
| zoom / backTo1SettleMs | 153.5 [156.6] | 203.4 [214.1] | +49.9 [+45.3, +57.2] | slower |
| zoom / mainTaskMs | 756.5 [783.9] | 1365.0 [1454.3] | 1.804 [1.759, 1.871] | slower |
| zoom / mainJsMs | 566.8 [590.3] | 1167.8 [1246.3] | 2.060 [2.012, 2.140] | slower |
| zoomLarge / settleMs | 393.2 [423.0] | 735.9 [814.6] | +342.8 [+304.3, +387.4] | slower |
| zoomLarge / zoom2SettleMs | 170.8 [200.5] | 319.5 [374.9] | +148.8 [+111.2, +178.9] | slower |
| zoomLarge / zoom4SettleMs | 219.2 [242.3] | 407.9 [480.5] | +188.7 [+172.1, +221.9] | slower |
| zoomLarge / backTo1SettleMs | 157.0 [169.0] | 259.8 [277.0] | +102.8 [+93.2, +108.9] | slower |
| zoomLarge / mainTaskMs | 497.7 [543.0] | 1731.8 [1805.3] | 3.480 [3.298, 3.604] | slower |
| zoomLarge / mainJsMs | 373.9 [412.7] | 1574.6 [1638.2] | 4.212 [3.939, 4.400] | slower |
| select / settleMs | 236.0 [246.8] | 320.3 [349.0] | +84.3 [+66.3, +113.6] | slower |
| select / mainTaskMs | 85.3 [88.6] | 171.3 [173.0] | 2.009 [1.974, 2.048] | slower |
| select / mainJsMs | 56.5 [57.3] | 138.8 [141.9] | 2.456 [2.431, 2.536] | slower |
| search / searchMs | 60.6 [61.9] | 69.3 [71.3] | 1.144 [1.131, 1.160] | slower |
| search / mainTaskMs | 17.4 [19.4] | 32.3 [33.7] | 1.861 [1.732, 1.976] | slower |
| search / mainJsMs | 12.5 [13.1] | 23.1 [24.2] | 1.845 [1.785, 1.918] | slower |
| searchLarge / searchMs | 2687.5 [2848.4] | 4569.6 [4746.4] | 1.700 [1.608, 1.769] | slower |
| searchLarge / mainTaskMs | 1292.5 [1428.0] | 4543.6 [4719.1] | 3.515 [3.197, 3.831] | slower |
| searchLarge / mainJsMs | 1152.7 [1285.6] | 4440.7 [4612.7] | 3.853 [3.457, 4.253] | slower |
| annotate / settleMs | 281.0 [307.0] | 412.1 [430.0] | +131.1 [+108.4, +141.9] | slower |
| annotate / mainTaskMs | 162.3 [164.0] | 273.2 [280.2] | 1.683 [1.669, 1.724] | slower |
| annotate / mainJsMs | 97.2 [100.8] | 207.7 [209.9] | 2.137 [2.097, 2.174] | slower |
| annotateLarge / settleMs | 275.7 [299.2] | 497.0 [519.3] | +221.3 [+192.0, +242.6] | slower |
| annotateLarge / mainTaskMs | 163.5 [166.4] | 370.0 [374.7] | 2.263 [2.233, 2.327] | slower |
| annotateLarge / mainJsMs | 100.5 [101.7] | 273.6 [275.7] | 2.723 [2.695, 2.763] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-hostbytes median [p95] |
| --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 35.36 MiB [36.36 MiB] | 35.36 MiB [36.36 MiB] | 36.87 MiB [37.87 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 47.75 MiB [47.89 MiB] | 47.74 MiB [47.83 MiB] | 52.21 MiB [52.34 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.21 MiB [22.21 MiB] | 22.21 MiB [22.21 MiB] | 22.96 MiB [22.96 MiB] |
| soak / heapCycle5Bytes | 38.43 MiB [39.44 MiB] | 38.44 MiB [39.44 MiB] | 39.91 MiB [40.91 MiB] |
| soak / heapCycle30Bytes | 39.27 MiB [40.28 MiB] | 39.28 MiB [40.27 MiB] | 40.30 MiB [41.30 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

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

