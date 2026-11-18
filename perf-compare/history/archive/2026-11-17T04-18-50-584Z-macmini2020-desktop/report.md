# Viewer perf comparison: 2026-11-17T04-18-50-584Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 7e527a415a9dc50b775cadf65ddb4a8d9c97d8f5 (uncommitted changes) |
| Chromium / Playwright | 140.0.7339.16 / 1.55.0 |
| Node | v24.20.0 |
| OS | Darwin 27.3.0 arm64 |
| CPU | Apple M1, 8 cores |
| Memory | 16 GiB |
| Power | AC Power |
| Viewport | 1280x800 @2x |
| Fixtures | ebook 2e539f62b5fa, large 10c8668fe55c, annots f27197ce9ac9, forms b973ccec7bf6 |

| Arm | Ref | SHA | Payload | Engine wasm sha256 | Kernel wasm sha256 |
| --- | --- | --- | --- | --- | --- |
| main | main | 2516e2786 | fetched | 98c2368a774d |  |
| main-aa | main | 2516e2786 | fetched | 98c2368a774d |  |
| main-now | main | ed064e0c8 | fetched | 7195838021c8 |  |
| phase-c-base | research/perf-fixes | 0de4effd9 | built (fork 17d8073d4, wasm32-eh) | 2ecd0bd98574 | 3db0d63cf08a |
| phase-c-end | research/perf-fixes | af9ff5d2b | built (fork 17d8073d4, wasm32-eh) | 2ecd0bd98574 | 57b1b6d02044 |
| phase-d-end | research/perf-fixes | 0d18fb177 | built (fork 17d8073d4, wasm32-eh) | 2ecd0bd98574 | d5eefdeebf95 |

## Gate

- `main`: pass
- `main-aa`: pass
- `main-now`: pass
- `phase-c-base`: pass
- `phase-c-end`: pass
- `phase-d-end`: pass

Not run, because the arm's viewer lacks what the flow needs:

- `main`: `undo`
- `main-aa`: `undo`

## Sizes

| Metric | main | main-aa | main-now | phase-c-base | phase-c-end | phase-d-end |
| --- | --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 7.52 MiB | 14.26 MiB | 14.26 MiB | 14.26 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.35 MiB | 3.79 MiB | 3.79 MiB | 3.79 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 10.35 MiB | 10.70 MiB | 10.73 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 2.44 MiB | 2.50 MiB | 2.50 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 9.50 MiB | 8.50 MiB | 8.50 MiB | 8.50 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 3.12 MiB | 2.90 MiB | 2.90 MiB | 2.90 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

84 of 84 rows pass.

| Row | main median [max] | main-aa median [max] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 62.9 [64.5] | 60.8 [70.5] | 0.966 [0.951, 1.025] | pass |
| open(ebook) / openedMs | 136.4 [137.3] | 134.7 [143.4] | 0.988 [0.977, 1.004] | pass |
| open(ebook) / firstPaintMs | 266.0 [270.2] | 263.6 [270.6] | 0.991 [0.979, 1.021] | pass |
| open(ebook) / settledMs | 397.6 [402.2] | 401.3 [410.5] | 1.009 [1.001, 1.024] | pass |
| open(ebook) / mainTaskMs | 170.6 [174.8] | 169.3 [182.3] | 0.992 [0.978, 1.009] | pass |
| open(ebook) / mainJsMs | 140.7 [142.1] | 139.5 [150.2] | 0.992 [0.979, 1.015] | pass |
| open(large) / readyMs | 61.3 [64.0] | 60.3 [70.0] | 0.984 [0.955, 1.043] | pass |
| open(large) / openedMs | 156.4 [157.7] | 156.8 [162.8] | 1.002 [0.990, 1.033] | pass |
| open(large) / firstPaintMs | 263.8 [268.5] | 262.0 [294.6] | 0.993 [0.980, 1.025] | pass |
| open(large) / settledMs | 283.8 [289.1] | 281.4 [291.6] | 0.991 [0.981, 1.017] | pass |
| open(large) / mainTaskMs | 319.9 [322.6] | 320.3 [326.4] | 1.001 [0.997, 1.008] | pass |
| open(large) / mainJsMs | 291.1 [292.9] | 292.5 [298.3] | 1.005 [0.998, 1.009] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 3004.2 [3016.9] | 3015.2 [3031.4] | 1.004 [0.999, 1.006] | pass |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 16.8 [17.3] | 16.9 [17.5] | 1.008 [0.986, 1.019] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 755.9 [770.6] | 762.1 [777.4] | 1.008 [0.991, 1.021] | pass |
| scroll / mainJsMs | 616.5 [629.6] | 621.4 [633.6] | 1.008 [0.990, 1.026] | pass |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scrollMid / wallMs | 3006.3 [3015.4] | 2998.9 [3031.8] | 0.998 [0.995, 1.003] | pass |
| scrollMid / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | pass |
| scrollMid / frameP95Ms | 16.7 [17.3] | 16.8 [17.2] | 1.008 [0.989, 1.024] | pass |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scrollMid / mainTaskMs | 773.1 [798.4] | 766.8 [785.6] | 0.992 [0.981, 1.012] | pass |
| scrollMid / mainJsMs | 627.2 [642.7] | 622.9 [634.9] | 0.993 [0.974, 1.007] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 3000.1 [3017.2] | 3000.3 [3014.9] | 1.000 [0.995, 1.002] | pass |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | pass |
| coldScroll / frameP95Ms | 17.1 [18.6] | 17.0 [18.5] | 0.993 [0.965, 1.011] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 767.0 [783.7] | 762.7 [794.1] | 0.994 [0.986, 1.013] | pass |
| coldScroll / mainJsMs | 599.1 [613.8] | 597.8 [621.8] | 0.998 [0.989, 1.019] | pass |
| jump / settleMs | 130.5 [138.4] | 139.9 [145.5] | +9.4 [-2.6, +13.6] | pass |
| jump / mainTaskMs | 100.5 [110.0] | 101.9 [105.1] | 1.014 [0.951, 1.038] | pass |
| jump / mainJsMs | 87.4 [91.4] | 86.4 [89.7] | 0.989 [0.956, 1.070] | pass |
| zoom / settleMs | 458.1 [490.1] | 447.7 [476.2] | -10.4 [-32.0, +13.5] | pass |
| zoom / zoom2SettleMs | 174.7 [187.9] | 176.2 [180.7] | +1.5 [-2.9, +4.2] | pass |
| zoom / zoom4SettleMs | 286.9 [306.6] | 273.4 [304.9] | -13.5 [-35.3, +12.3] | pass |
| zoom / backTo1SettleMs | 129.5 [133.6] | 126.8 [143.6] | -2.7 [-6.1, +3.8] | pass |
| zoom / mainTaskMs | 380.9 [400.9] | 377.6 [384.7] | 0.991 [0.973, 1.016] | pass |
| zoom / mainJsMs | 315.6 [329.2] | 315.0 [326.9] | 0.998 [0.980, 1.041] | pass |
| zoomLarge / settleMs | 321.6 [325.6] | 323.4 [342.0] | +1.8 [-14.9, +8.2] | pass |
| zoomLarge / zoom2SettleMs | 161.5 [165.0] | 162.7 [182.3] | +1.2 [-14.2, +10.8] | pass |
| zoomLarge / zoom4SettleMs | 161.0 [168.2] | 158.5 [165.0] | -2.5 [-5.3, +3.0] | pass |
| zoomLarge / backTo1SettleMs | 138.9 [149.0] | 144.0 [151.5] | +5.0 [-8.1, +9.9] | pass |
| zoomLarge / mainTaskMs | 343.3 [351.6] | 338.8 [351.3] | 0.987 [0.975, 1.022] | pass |
| zoomLarge / mainJsMs | 300.7 [309.8] | 297.1 [307.9] | 0.988 [0.975, 1.021] | pass |
| select / settleMs | 286.8 [292.7] | 280.1 [291.2] | -6.7 [-12.4, +4.6] | pass |
| select / mainTaskMs | 132.5 [137.5] | 128.8 [134.6] | 0.972 [0.923, 0.997] | pass |
| select / mainJsMs | 99.8 [108.0] | 96.9 [102.8] | 0.971 [0.915, 1.008] | pass |
| selectLarge / settleMs | 274.5 [299.3] | 272.8 [286.2] | -1.7 [-12.1, +7.9] | pass |
| selectLarge / mainTaskMs | 123.3 [137.9] | 123.3 [135.4] | 1.000 [0.947, 1.067] | pass |
| selectLarge / mainJsMs | 102.7 [113.2] | 101.6 [111.5] | 0.989 [0.946, 1.062] | pass |
| search / searchMs | 81.9 [83.1] | 81.8 [84.0] | 0.999 [0.986, 1.011] | pass |
| search / mainTaskMs | 16.3 [17.4] | 16.4 [17.4] | 1.007 [0.957, 1.059] | pass |
| search / mainJsMs | 6.8 [7.8] | 6.9 [7.7] | 1.013 [0.939, 1.095] | pass |
| searchLarge / searchMs | 1883.4 [1895.6] | 1889.2 [1934.1] | 1.003 [0.998, 1.013] | pass |
| searchLarge / mainTaskMs | 371.5 [372.1] | 371.1 [374.5] | 0.999 [0.993, 1.004] | pass |
| searchLarge / mainJsMs | 320.5 [322.3] | 320.7 [324.4] | 1.000 [0.996, 1.006] | pass |
| annotate / settleMs | 291.2 [297.8] | 289.1 [297.2] | -2.1 [-6.3, +1.9] | pass |
| annotate / mainTaskMs | 139.3 [145.5] | 137.0 [144.3] | 0.983 [0.947, 1.030] | pass |
| annotate / mainJsMs | 106.4 [114.6] | 103.3 [112.0] | 0.971 [0.932, 1.041] | pass |
| annotateLarge / settleMs | 290.3 [297.6] | 294.6 [296.2] | +4.3 [-3.0, +6.9] | pass |
| annotateLarge / mainTaskMs | 143.3 [146.1] | 144.8 [147.5] | 1.011 [0.985, 1.051] | pass |
| annotateLarge / mainJsMs | 109.6 [112.8] | 111.9 [114.4] | 1.021 [0.987, 1.048] | pass |
| hover / settleMs | 1323.6 [1327.8] | 1323.0 [1328.6] | -0.6 [-3.3, +2.6] | pass |
| hover / mainTaskMs | 426.2 [434.2] | 427.8 [435.9] | 1.004 [0.986, 1.017] | pass |
| hover / mainJsMs | 384.1 [392.1] | 385.6 [395.2] | 1.004 [0.986, 1.022] | pass |
| dragAnnot / settleMs | 400.9 [411.2] | 405.7 [413.5] | +4.8 [-6.5, +11.8] | pass |
| dragAnnot / mainTaskMs | 171.8 [178.1] | 174.3 [180.2] | 1.015 [0.975, 1.037] | pass |
| dragAnnot / mainJsMs | 140.2 [145.5] | 139.4 [149.0] | 0.995 [0.969, 1.035] | pass |
| drawInk / settleMs | 3884.2 [3896.1] | 3890.0 [3903.7] | +5.8 [-4.7, +11.7] | pass |
| drawInk / mainTaskMs | 1074.8 [1094.2] | 1082.6 [1137.2] | 1.007 [0.986, 1.031] | pass |
| drawInk / mainJsMs | 798.9 [809.6] | 803.8 [835.7] | 1.006 [0.985, 1.029] | pass |
| typeField / settleMs | 175.2 [182.1] | 175.9 [187.4] | +0.7 [-6.1, +7.1] | pass |
| typeField / mainTaskMs | 175.7 [184.6] | 175.7 [181.5] | 1.000 [0.964, 1.025] | pass |
| typeField / mainJsMs | 145.8 [151.5] | 145.4 [150.5] | 0.997 [0.974, 1.027] | pass |

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
| open(ebook) / readyMs | 62.9 [64.5] | 52.3 [57.9] | 0.832 [0.822, 0.876] | ±4.9% | faster |
| open(ebook) / openedMs | 136.4 [137.3] | 203.7 [204.3] | 1.494 [1.473, 1.512] | ±3.0% | slower |
| open(ebook) / firstPaintMs | 266.0 [270.2] | 367.5 [381.1] | 1.381 [1.268, 1.426] | ±3.0% | slower |
| open(ebook) / settledMs | 397.6 [402.2] | 357.4 [369.3] | 0.899 [0.889, 0.915] | ±3.0% | faster |
| open(ebook) / mainTaskMs | 170.6 [174.8] | 222.0 [230.6] | 1.301 [1.276, 1.328] | ±3.0% | slower |
| open(ebook) / mainJsMs | 140.7 [142.1] | 188.9 [196.4] | 1.343 [1.320, 1.372] | ±3.0% | slower |
| open(large) / readyMs | 61.3 [64.0] | 52.0 [57.0] | 0.847 [0.822, 0.874] | ±4.5% | faster |
| open(large) / openedMs | 156.4 [157.7] | 213.6 [215.5] | 1.365 [1.350, 1.377] | ±3.3% | slower |
| open(large) / firstPaintMs | 263.8 [268.5] | 352.2 [363.3] | 1.335 [1.295, 1.358] | ±3.0% | slower |
| open(large) / settledMs | 283.8 [289.1] | 339.6 [350.4] | 1.196 [1.178, 1.218] | ±3.0% | slower |
| open(large) / mainTaskMs | 319.9 [322.6] | 213.2 [217.6] | 0.666 [0.657, 0.676] | ±3.0% | faster |
| open(large) / mainJsMs | 291.1 [292.9] | 180.7 [187.0] | 0.621 [0.614, 0.632] | ±3.0% | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 3004.2 [3016.9] | 3120.0 [3168.8] | 1.039 [1.032, 1.047] | ±3.0% | slower |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scroll / frameP95Ms | 16.8 [17.3] | 17.3 [17.5] | 1.031 [1.007, 1.040] | ±3.0% | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 755.9 [770.6] | 1017.9 [1031.6] | 1.347 [1.322, 1.361] | ±3.0% | slower |
| scroll / mainJsMs | 616.5 [629.6] | 865.0 [879.9] | 1.403 [1.383, 1.419] | ±3.0% | slower |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 3006.3 [3015.4] | 3136.9 [3155.1] | 1.043 [1.040, 1.049] | ±3.0% | slower |
| scrollMid / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scrollMid / frameP95Ms | 16.7 [17.3] | 17.2 [17.5] | 1.032 [1.003, 1.038] | ±3.0% | slower |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 773.1 [798.4] | 1079.3 [1096.8] | 1.396 [1.384, 1.410] | ±3.0% | slower |
| scrollMid / mainJsMs | 627.2 [642.7] | 936.7 [950.4] | 1.493 [1.473, 1.512] | ±3.0% | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 3000.1 [3017.2] | 3122.5 [3154.6] | 1.041 [1.035, 1.048] | ±3.0% | slower |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| coldScroll / frameP95Ms | 17.1 [18.6] | 17.3 [17.6] | 1.007 [0.980, 1.020] | ±3.5% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 767.0 [783.7] | 1045.7 [1052.7] | 1.363 [1.337, 1.378] | ±3.0% | slower |
| coldScroll / mainJsMs | 599.1 [613.8] | 875.1 [884.8] | 1.461 [1.451, 1.488] | ±3.0% | slower |
| jump / settleMs | 130.5 [138.4] | 150.4 [166.9] | +19.9 [+9.0, +30.8] | ±13.6 | slower |
| jump / mainTaskMs | 100.5 [110.0] | 155.5 [166.0] | 1.547 [1.441, 1.614] | ±4.9% | slower |
| jump / mainJsMs | 87.4 [91.4] | 135.8 [146.4] | 1.554 [1.484, 1.673] | ±7.0% | slower |
| zoom / settleMs | 458.1 [490.1] | 598.9 [606.3] | +140.7 [+118.3, +161.2] | ±32.0 | slower |
| zoom / zoom2SettleMs | 174.7 [187.9] | 198.2 [217.1] | +23.5 [+19.5, +28.5] | ±5.2 | slower |
| zoom / zoom4SettleMs | 286.9 [306.6] | 396.4 [408.0] | +109.5 [+93.0, +132.7] | ±35.3 | slower |
| zoom / backTo1SettleMs | 129.5 [133.6] | 176.2 [182.1] | +46.8 [+43.8, +52.5] | ±6.1 | slower |
| zoom / mainTaskMs | 380.9 [400.9] | 526.8 [540.3] | 1.383 [1.353, 1.412] | ±3.0% | slower |
| zoom / mainJsMs | 315.6 [329.2] | 459.4 [472.8] | 1.455 [1.424, 1.517] | ±4.1% | slower |
| zoomLarge / settleMs | 321.6 [325.6] | 315.3 [329.8] | -6.3 [-10.1, +0.4] | ±14.9 | no detectable difference |
| zoomLarge / zoom2SettleMs | 161.5 [165.0] | 164.8 [175.2] | +3.4 [-3.8, +10.0] | ±14.2 | no detectable difference |
| zoomLarge / zoom4SettleMs | 161.0 [168.2] | 154.8 [165.7] | -6.2 [-13.8, -1.6] | ±5.3 | faster |
| zoomLarge / backTo1SettleMs | 138.9 [149.0] | 196.8 [210.0] | +57.8 [+54.2, +63.0] | ±9.9 | slower |
| zoomLarge / mainTaskMs | 343.3 [351.6] | 476.9 [488.2] | 1.389 [1.362, 1.440] | ±3.0% | slower |
| zoomLarge / mainJsMs | 300.7 [309.8] | 411.4 [414.2] | 1.368 [1.351, 1.402] | ±3.0% | slower |
| select / settleMs | 286.8 [292.7] | 271.8 [292.2] | -15.0 [-18.0, -5.4] | ±12.4 | faster |
| select / mainTaskMs | 132.5 [137.5] | 139.8 [143.8] | 1.056 [1.017, 1.088] | ±7.7% | no detectable difference |
| select / mainJsMs | 99.8 [108.0] | 119.8 [123.3] | 1.200 [1.129, 1.240] | ±8.5% | slower |
| selectLarge / settleMs | 274.5 [299.3] | 275.0 [294.7] | +0.5 [-10.1, +3.8] | ±12.1 | no detectable difference |
| selectLarge / mainTaskMs | 123.3 [137.9] | 144.9 [154.9] | 1.175 [1.107, 1.229] | ±6.7% | slower |
| selectLarge / mainJsMs | 102.7 [113.2] | 124.5 [133.3] | 1.212 [1.165, 1.287] | ±6.2% | slower |
| search / searchMs | 81.9 [83.1] | 40.3 [41.3] | 0.492 [0.484, 0.499] | ±3.0% | faster |
| search / mainTaskMs | 16.3 [17.4] | 14.4 [15.2] | 0.882 [0.843, 0.913] | ±5.9% | faster |
| search / mainJsMs | 6.8 [7.8] | 10.4 [10.7] | 1.514 [1.394, 1.580] | ±9.5% | slower |
| searchLarge / searchMs | 1883.4 [1895.6] | 2235.4 [2254.3] | 1.187 [1.183, 1.194] | ±3.0% | slower |
| searchLarge / mainTaskMs | 371.5 [372.1] | 419.7 [422.8] | 1.130 [1.119, 1.135] | ±3.0% | slower |
| searchLarge / mainJsMs | 320.5 [322.3] | 359.0 [362.3] | 1.120 [1.114, 1.125] | ±3.0% | slower |
| annotate / settleMs | 291.2 [297.8] | 272.4 [279.6] | -18.8 [-23.0, -14.0] | ±8.7 | faster |
| annotate / mainTaskMs | 139.3 [145.5] | 154.1 [157.7] | 1.106 [1.055, 1.128] | ±5.3% | slower |
| annotate / mainJsMs | 106.4 [114.6] | 122.4 [129.1] | 1.151 [1.119, 1.211] | ±6.8% | slower |
| annotateLarge / settleMs | 290.3 [297.6] | 277.5 [290.5] | -12.8 [-20.0, -1.2] | ±8.7 | faster |
| annotateLarge / mainTaskMs | 143.3 [146.1] | 149.9 [153.9] | 1.046 [1.020, 1.088] | ±5.1% | no detectable difference |
| annotateLarge / mainJsMs | 109.6 [112.8] | 126.6 [131.2] | 1.155 [1.117, 1.192] | ±4.8% | slower |
| hover / settleMs | 1323.6 [1327.8] | 1320.8 [1333.1] | -2.9 [-6.1, +5.5] | ±39.7 | no detectable difference |
| hover / mainTaskMs | 426.2 [434.2] | 542.0 [550.7] | 1.272 [1.252, 1.291] | ±3.0% | slower |
| hover / mainJsMs | 384.1 [392.1] | 507.5 [516.1] | 1.321 [1.305, 1.347] | ±3.0% | slower |
| dragAnnot / settleMs | 400.9 [411.2] | 448.9 [458.5] | +48.0 [+39.5, +53.3] | ±12.0 | slower |
| dragAnnot / mainTaskMs | 171.8 [178.1] | 271.1 [280.2] | 1.578 [1.549, 1.605] | ±3.7% | slower |
| dragAnnot / mainJsMs | 140.2 [145.5] | 233.6 [241.8] | 1.666 [1.619, 1.711] | ±3.5% | slower |
| drawInk / settleMs | 3884.2 [3896.1] | 3940.9 [3953.3] | +56.8 [+45.9, +65.1] | ±116.5 | no detectable difference |
| drawInk / mainTaskMs | 1074.8 [1094.2] | 1592.5 [1617.2] | 1.482 [1.460, 1.503] | ±3.1% | slower |
| drawInk / mainJsMs | 798.9 [809.6] | 1338.6 [1358.8] | 1.676 [1.664, 1.706] | ±3.0% | slower |
| typeField / settleMs | 175.2 [182.1] | 151.9 [166.9] | -23.3 [-27.4, -11.2] | ±7.1 | faster |
| typeField / mainTaskMs | 175.7 [184.6] | 169.8 [176.6] | 0.966 [0.941, 0.987] | ±3.6% | no detectable difference |
| typeField / mainJsMs | 145.8 [151.5] | 139.2 [146.9] | 0.955 [0.940, 0.982] | ±3.0% | faster |

## `main-now` vs `plugins-next`

Not run: `plugins-next`.

## `plugins-next` vs `plugins-next-lto`

Not run: `plugins-next`, `plugins-next-lto`.

## `phase-c-base` vs `phase-c-end`

| Row | phase-c-base median [max] | phase-c-end median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 106.8 [110.7] | 110.8 [118.3] | 1.038 [1.004, 1.061] | ±4.9% | no detectable difference |
| open(ebook) / openedMs | 286.1 [289.3] | 290.8 [293.3] | 1.017 [0.995, 1.026] | ±3.0% | no detectable difference |
| open(ebook) / firstPaintMs | 490.8 [548.1] | 484.4 [524.7] | 0.987 [0.905, 1.021] | ±3.0% | no detectable difference |
| open(ebook) / settledMs | 528.7 [539.9] | 522.8 [536.0] | 0.989 [0.970, 1.009] | ±3.0% | no detectable difference |
| open(ebook) / mainTaskMs | 420.3 [434.7] | 407.5 [420.8] | 0.969 [0.951, 0.990] | ±3.0% | faster |
| open(ebook) / mainJsMs | 360.9 [371.1] | 348.7 [358.9] | 0.966 [0.947, 0.985] | ±3.0% | faster |
| open(large) / readyMs | 107.3 [113.3] | 108.7 [112.8] | 1.013 [0.989, 1.046] | ±4.5% | no detectable difference |
| open(large) / openedMs | 308.9 [315.1] | 308.7 [313.7] | 1.000 [0.988, 1.014] | ±3.3% | no detectable difference |
| open(large) / firstPaintMs | 514.4 [541.2] | 502.0 [537.6] | 0.976 [0.935, 1.031] | ±3.0% | no detectable difference |
| open(large) / settledMs | 545.6 [601.5] | 528.9 [567.5] | 0.969 [0.897, 1.018] | ±3.0% | no detectable difference |
| open(large) / mainTaskMs | 443.1 [463.0] | 422.3 [432.7] | 0.953 [0.932, 0.969] | ±3.0% | faster |
| open(large) / mainJsMs | 384.5 [405.2] | 364.1 [372.7] | 0.947 [0.920, 0.970] | ±3.0% | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 3108.1 [3166.8] | 3054.6 [3071.0] | 0.983 [0.970, 0.987] | ±3.0% | no detectable difference |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scroll / frameP95Ms | 17.5 [18.3] | 16.9 [17.6] | 0.966 [0.954, 0.986] | ±3.0% | faster |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 1637.4 [1653.3] | 1379.1 [1418.7] | 0.842 [0.831, 0.853] | ±3.0% | faster |
| scroll / mainJsMs | 1529.5 [1540.2] | 1225.2 [1244.5] | 0.801 [0.795, 0.815] | ±3.0% | faster |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 3352.3 [3408.1] | 3063.5 [3104.5] | 0.914 [0.898, 0.922] | ±3.0% | faster |
| scrollMid / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scrollMid / frameP95Ms | 17.8 [18.1] | 17.0 [17.3] | 0.954 [0.937, 0.968] | ±3.0% | faster |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 1900.6 [1946.7] | 1425.2 [1458.5] | 0.750 [0.731, 0.767] | ±3.0% | faster |
| scrollMid / mainJsMs | 1804.2 [1851.1] | 1287.2 [1315.1] | 0.713 [0.703, 0.722] | ±3.0% | faster |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 3113.7 [3168.6] | 3021.6 [3073.1] | 0.970 [0.954, 0.980] | ±3.0% | no detectable difference |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| coldScroll / frameP95Ms | 17.5 [18.4] | 16.8 [18.0] | 0.960 [0.945, 1.004] | ±3.5% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 1657.7 [1680.5] | 1394.1 [1419.7] | 0.841 [0.831, 0.851] | ±3.0% | faster |
| coldScroll / mainJsMs | 1519.9 [1540.9] | 1237.8 [1260.2] | 0.814 [0.806, 0.825] | ±3.0% | faster |
| jump / settleMs | 164.3 [175.3] | 144.5 [169.5] | -19.8 [-33.4, -1.8] | ±13.6 | faster |
| jump / mainTaskMs | 249.7 [268.6] | 186.6 [199.7] | 0.747 [0.701, 0.784] | ±4.9% | faster |
| jump / mainJsMs | 235.6 [251.3] | 175.1 [186.8] | 0.743 [0.702, 0.786] | ±7.0% | faster |
| zoom / settleMs | 656.6 [664.7] | 643.3 [659.9] | -13.3 [-28.0, +0.7] | ±32.0 | no detectable difference |
| zoom / zoom2SettleMs | 235.5 [245.7] | 226.9 [238.1] | -8.7 [-15.4, -0.1] | ±7.1 | faster |
| zoom / zoom4SettleMs | 422.1 [427.5] | 414.4 [422.0] | -7.7 [-15.9, +10.3] | ±35.3 | no detectable difference |
| zoom / backTo1SettleMs | 121.0 [183.5] | 116.3 [179.6] | -4.7 [-36.8, +54.5] | ±6.1 | no detectable difference |
| zoom / mainTaskMs | 629.1 [701.8] | 587.4 [637.3] | 0.934 [0.890, 1.003] | ±3.0% | no detectable difference |
| zoom / mainJsMs | 573.2 [643.2] | 530.5 [578.1] | 0.926 [0.883, 1.002] | ±4.1% | no detectable difference |
| zoomLarge / settleMs | 372.0 [391.4] | 345.0 [362.8] | -27.0 [-40.8, -11.8] | ±14.9 | faster |
| zoomLarge / zoom2SettleMs | 198.9 [210.8] | 182.2 [187.3] | -16.7 [-26.2, -8.4] | ±14.2 | faster |
| zoomLarge / zoom4SettleMs | 173.0 [186.8] | 164.1 [176.6] | -8.9 [-18.8, +0.2] | ±5.3 | no detectable difference |
| zoomLarge / backTo1SettleMs | 197.6 [202.6] | 206.9 [211.9] | +9.3 [+2.9, +15.2] | ±9.9 | no detectable difference |
| zoomLarge / mainTaskMs | 605.4 [618.3] | 567.8 [589.1] | 0.938 [0.926, 0.955] | ±3.0% | faster |
| zoomLarge / mainJsMs | 566.1 [572.1] | 518.7 [542.0] | 0.916 [0.898, 0.940] | ±3.0% | faster |
| select / settleMs | 278.9 [295.8] | 281.1 [291.8] | +2.2 [-3.5, +9.2] | ±12.4 | no detectable difference |
| select / mainTaskMs | 187.4 [192.9] | 172.3 [178.6] | 0.920 [0.882, 0.944] | ±7.7% | faster |
| select / mainJsMs | 167.6 [172.2] | 149.5 [157.3] | 0.892 [0.861, 0.931] | ±8.5% | faster |
| selectLarge / settleMs | 312.1 [340.3] | 284.2 [308.3] | -27.9 [-32.2, -19.4] | ±12.1 | faster |
| selectLarge / mainTaskMs | 212.2 [224.3] | 174.3 [181.2] | 0.821 [0.799, 0.838] | ±6.7% | faster |
| selectLarge / mainJsMs | 184.2 [190.7] | 152.1 [158.2] | 0.826 [0.788, 0.863] | ±6.2% | faster |
| search / searchMs | 45.2 [46.4] | 45.2 [45.7] | 1.000 [0.979, 1.009] | ±3.0% | no detectable difference |
| search / mainTaskMs | 30.2 [31.2] | 28.1 [28.8] | 0.930 [0.902, 0.949] | ±5.9% | faster |
| search / mainJsMs | 25.4 [27.2] | 23.0 [24.2] | 0.905 [0.872, 0.928] | ±9.5% | no detectable difference |
| searchLarge / searchMs | 2188.8 [2244.0] | 1924.3 [2010.9] | 0.879 [0.872, 0.883] | ±3.0% | faster |
| searchLarge / mainTaskMs | 1506.5 [1547.0] | 1228.5 [1287.9] | 0.815 [0.782, 0.825] | ±3.0% | faster |
| searchLarge / mainJsMs | 1270.6 [1285.8] | 911.0 [953.5] | 0.717 [0.712, 0.723] | ±3.0% | faster |
| annotate / settleMs | 288.4 [293.7] | 272.1 [276.2] | -16.3 [-23.5, +1.6] | ±8.7 | no detectable difference |
| annotate / mainTaskMs | 196.5 [200.4] | 176.1 [183.6] | 0.896 [0.883, 0.929] | ±5.3% | faster |
| annotate / mainJsMs | 169.3 [172.4] | 148.8 [158.3] | 0.879 [0.865, 0.915] | ±6.8% | faster |
| annotateLarge / settleMs | 299.9 [322.4] | 273.9 [289.5] | -26.1 [-30.3, -17.1] | ±9.0 | faster |
| annotateLarge / mainTaskMs | 222.6 [225.5] | 178.8 [189.8] | 0.803 [0.779, 0.836] | ±5.1% | faster |
| annotateLarge / mainJsMs | 180.5 [183.0] | 155.2 [167.9] | 0.860 [0.833, 0.887] | ±4.8% | faster |
| hover / settleMs | 1663.6 [1673.7] | 1347.0 [1387.7] | -316.7 [-326.0, -308.4] | ±49.9 | faster |
| hover / mainTaskMs | 1233.9 [1253.3] | 916.6 [927.9] | 0.743 [0.737, 0.748] | ±3.0% | faster |
| hover / mainJsMs | 1199.9 [1218.3] | 886.6 [894.4] | 0.739 [0.733, 0.742] | ±3.0% | faster |
| dragAnnot / settleMs | 946.0 [956.7] | 870.2 [879.3] | -75.8 [-80.1, -62.7] | ±28.4 | faster |
| dragAnnot / mainTaskMs | 780.7 [786.4] | 621.0 [632.0] | 0.795 [0.791, 0.802] | ±3.7% | faster |
| dragAnnot / mainJsMs | 715.3 [720.2] | 583.2 [593.3] | 0.815 [0.811, 0.822] | ±3.5% | faster |
| drawInk / settleMs | 6971.2 [7029.0] | 4147.0 [4272.8] | -2824.2 [-2867.8, -2760.6] | ±209.1 | faster |
| drawInk / mainTaskMs | 4279.5 [4307.3] | 2765.3 [2792.7] | 0.646 [0.642, 0.649] | ±3.1% | faster |
| drawInk / mainJsMs | 4006.5 [4031.6] | 2508.5 [2533.3] | 0.626 [0.621, 0.630] | ±3.0% | faster |
| typeField / settleMs | 292.2 [305.6] | 271.4 [289.0] | -20.8 [-30.5, -12.4] | ±8.8 | faster |
| typeField / mainTaskMs | 357.8 [365.6] | 330.7 [340.7] | 0.924 [0.912, 0.945] | ±3.6% | faster |
| typeField / mainJsMs | 318.7 [326.4] | 297.4 [308.2] | 0.933 [0.925, 0.957] | ±3.0% | faster |
| undo / settleMs | 842.4 [852.8] | 693.2 [696.3] | -149.1 [-158.0, -138.5] | ±25.3 | faster |
| undo / mainTaskMs | 871.3 [886.4] | 722.4 [726.0] | 0.829 [0.818, 0.836] | ±3.0% | faster |
| undo / mainJsMs | 628.3 [640.9] | 478.9 [481.8] | 0.762 [0.750, 0.772] | ±3.0% | faster |

## `phase-c-end` vs `phase-d-end`

| Row | phase-c-end median [max] | phase-d-end median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 110.8 [118.3] | 109.5 [114.0] | 0.988 [0.963, 1.019] | ±4.9% | no detectable difference |
| open(ebook) / openedMs | 290.8 [293.3] | 288.3 [293.7] | 0.991 [0.983, 1.015] | ±3.0% | no detectable difference |
| open(ebook) / firstPaintMs | 484.4 [524.7] | 496.0 [533.1] | 1.024 [0.988, 1.060] | ±3.0% | no detectable difference |
| open(ebook) / settledMs | 522.8 [536.0] | 524.5 [546.7] | 1.003 [0.982, 1.026] | ±3.0% | no detectable difference |
| open(ebook) / mainTaskMs | 407.5 [420.8] | 410.6 [421.8] | 1.008 [0.992, 1.027] | ±3.0% | no detectable difference |
| open(ebook) / mainJsMs | 348.7 [358.9] | 352.3 [359.2] | 1.010 [0.997, 1.027] | ±3.0% | no detectable difference |
| open(large) / readyMs | 108.7 [112.8] | 110.4 [118.3] | 1.015 [0.976, 1.035] | ±4.5% | no detectable difference |
| open(large) / openedMs | 308.7 [313.7] | 309.7 [318.4] | 1.003 [0.991, 1.021] | ±3.3% | no detectable difference |
| open(large) / firstPaintMs | 502.0 [537.6] | 499.5 [529.6] | 0.995 [0.954, 1.045] | ±3.0% | no detectable difference |
| open(large) / settledMs | 528.9 [567.5] | 548.4 [574.7] | 1.037 [0.995, 1.084] | ±3.0% | no detectable difference |
| open(large) / mainTaskMs | 422.3 [432.7] | 433.8 [443.4] | 1.027 [1.005, 1.053] | ±3.0% | no detectable difference |
| open(large) / mainJsMs | 364.1 [372.7] | 372.4 [379.3] | 1.023 [0.996, 1.043] | ±3.0% | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 3054.6 [3071.0] | 3036.1 [3088.4] | 0.994 [0.989, 1.007] | ±3.0% | no detectable difference |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scroll / frameP95Ms | 16.9 [17.6] | 17.1 [18.1] | 1.009 [0.981, 1.056] | ±3.0% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 1379.1 [1418.7] | 1388.4 [1401.7] | 1.007 [0.993, 1.020] | ±3.0% | no detectable difference |
| scroll / mainJsMs | 1225.2 [1244.5] | 1231.3 [1243.7] | 1.005 [0.993, 1.014] | ±3.0% | no detectable difference |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 3063.5 [3104.5] | 3070.5 [3097.5] | 1.002 [0.985, 1.017] | ±3.0% | no detectable difference |
| scrollMid / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scrollMid / frameP95Ms | 17.0 [17.3] | 17.2 [17.9] | 1.012 [0.996, 1.033] | ±3.0% | no detectable difference |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 1425.2 [1458.5] | 1455.2 [1493.8] | 1.021 [0.986, 1.046] | ±3.0% | no detectable difference |
| scrollMid / mainJsMs | 1287.2 [1315.1] | 1295.4 [1319.2] | 1.006 [0.991, 1.022] | ±3.0% | no detectable difference |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 3021.6 [3073.1] | 3030.9 [3089.2] | 1.003 [0.995, 1.011] | ±3.0% | no detectable difference |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| coldScroll / frameP95Ms | 16.8 [18.0] | 17.2 [18.0] | 1.019 [0.977, 1.037] | ±3.5% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 1394.1 [1419.7] | 1408.5 [1434.9] | 1.010 [0.995, 1.018] | ±3.0% | no detectable difference |
| coldScroll / mainJsMs | 1237.8 [1260.2] | 1251.9 [1272.9] | 1.011 [0.996, 1.020] | ±3.0% | no detectable difference |
| jump / settleMs | 144.5 [169.5] | 156.7 [189.9] | +12.2 [-7.7, +28.8] | ±13.6 | no detectable difference |
| jump / mainTaskMs | 186.6 [199.7] | 193.9 [206.3] | 1.039 [0.992, 1.075] | ±4.9% | no detectable difference |
| jump / mainJsMs | 175.1 [186.8] | 181.3 [195.0] | 1.035 [0.988, 1.088] | ±7.0% | no detectable difference |
| zoom / settleMs | 643.3 [659.9] | 635.9 [655.9] | -7.4 [-14.2, +11.5] | ±32.0 | no detectable difference |
| zoom / zoom2SettleMs | 226.9 [238.1] | 227.7 [244.1] | +0.8 [-7.0, +7.4] | ±6.8 | no detectable difference |
| zoom / zoom4SettleMs | 414.4 [422.0] | 409.7 [427.1] | -4.7 [-13.8, +2.7] | ±35.3 | no detectable difference |
| zoom / backTo1SettleMs | 116.3 [179.6] | 130.0 [178.5] | +13.7 [-43.1, +40.5] | ±6.1 | no detectable difference |
| zoom / mainTaskMs | 587.4 [637.3] | 595.6 [627.2] | 1.014 [0.944, 1.047] | ±3.0% | no detectable difference |
| zoom / mainJsMs | 530.5 [578.1] | 532.4 [565.6] | 1.004 [0.928, 1.049] | ±4.1% | no detectable difference |
| zoomLarge / settleMs | 345.0 [362.8] | 344.1 [356.7] | -0.9 [-24.5, +13.2] | ±14.9 | no detectable difference |
| zoomLarge / zoom2SettleMs | 182.2 [187.3] | 183.3 [196.0] | +1.1 [-8.2, +9.1] | ±14.2 | no detectable difference |
| zoomLarge / zoom4SettleMs | 164.1 [176.6] | 163.6 [175.0] | -0.5 [-14.9, +8.8] | ±5.3 | no detectable difference |
| zoomLarge / backTo1SettleMs | 206.9 [211.9] | 204.4 [210.4] | -2.5 [-10.7, +6.5] | ±9.9 | no detectable difference |
| zoomLarge / mainTaskMs | 567.8 [589.1] | 566.7 [578.8] | 0.998 [0.977, 1.011] | ±3.0% | no detectable difference |
| zoomLarge / mainJsMs | 518.7 [542.0] | 520.7 [530.8] | 1.004 [0.970, 1.028] | ±3.0% | no detectable difference |
| select / settleMs | 281.1 [291.8] | 279.5 [289.4] | -1.6 [-8.5, +5.5] | ±12.4 | no detectable difference |
| select / mainTaskMs | 172.3 [178.6] | 171.3 [180.0] | 0.994 [0.962, 1.032] | ±7.7% | no detectable difference |
| select / mainJsMs | 149.5 [157.3] | 150.4 [156.5] | 1.006 [0.962, 1.034] | ±8.5% | no detectable difference |
| selectLarge / settleMs | 284.2 [308.3] | 285.4 [304.3] | +1.2 [-6.4, +9.8] | ±12.1 | no detectable difference |
| selectLarge / mainTaskMs | 174.3 [181.2] | 173.1 [180.7] | 0.993 [0.973, 1.036] | ±6.7% | no detectable difference |
| selectLarge / mainJsMs | 152.1 [158.2] | 149.1 [158.6] | 0.981 [0.960, 1.025] | ±6.2% | no detectable difference |
| search / searchMs | 45.2 [45.7] | 45.2 [46.0] | 1.000 [0.988, 1.018] | ±3.0% | no detectable difference |
| search / mainTaskMs | 28.1 [28.8] | 28.3 [29.7] | 1.007 [0.981, 1.026] | ±5.9% | no detectable difference |
| search / mainJsMs | 23.0 [24.2] | 23.2 [24.5] | 1.008 [0.984, 1.047] | ±9.5% | no detectable difference |
| searchLarge / searchMs | 1924.3 [2010.9] | 1923.6 [1958.2] | 1.000 [0.988, 1.013] | ±3.0% | no detectable difference |
| searchLarge / mainTaskMs | 1228.5 [1287.9] | 1188.0 [1277.3] | 0.967 [0.914, 1.024] | ±3.0% | no detectable difference |
| searchLarge / mainJsMs | 911.0 [953.5] | 909.5 [935.7] | 0.998 [0.982, 1.010] | ±3.0% | no detectable difference |
| annotate / settleMs | 272.1 [276.2] | 272.3 [280.1] | +0.1 [-8.2, +6.7] | ±8.2 | no detectable difference |
| annotate / mainTaskMs | 176.1 [183.6] | 177.9 [182.8] | 1.010 [0.987, 1.027] | ±5.3% | no detectable difference |
| annotate / mainJsMs | 148.8 [158.3] | 149.8 [154.3] | 1.007 [0.977, 1.029] | ±6.8% | no detectable difference |
| annotateLarge / settleMs | 273.9 [289.5] | 272.8 [290.7] | -1.1 [-8.3, +4.4] | ±8.2 | no detectable difference |
| annotateLarge / mainTaskMs | 178.8 [189.8] | 182.5 [188.3] | 1.021 [0.970, 1.054] | ±5.1% | no detectable difference |
| annotateLarge / mainJsMs | 155.2 [167.9] | 154.2 [158.9] | 0.993 [0.955, 1.027] | ±4.8% | no detectable difference |
| hover / settleMs | 1347.0 [1387.7] | 1340.9 [1353.5] | -6.1 [-17.0, +4.5] | ±40.4 | no detectable difference |
| hover / mainTaskMs | 916.6 [927.9] | 873.4 [882.2] | 0.953 [0.946, 0.963] | ±3.0% | faster |
| hover / mainJsMs | 886.6 [894.4] | 844.7 [852.4] | 0.953 [0.946, 0.960] | ±3.0% | faster |
| dragAnnot / settleMs | 870.2 [879.3] | 843.5 [853.1] | -26.6 [-33.8, -18.6] | ±26.1 | faster |
| dragAnnot / mainTaskMs | 621.0 [632.0] | 578.0 [599.2] | 0.931 [0.926, 0.957] | ±3.7% | faster |
| dragAnnot / mainJsMs | 583.2 [593.3] | 550.1 [561.2] | 0.943 [0.938, 0.952] | ±3.5% | faster |
| drawInk / settleMs | 4147.0 [4272.8] | 4037.8 [4120.1] | -109.2 [-178.6, -66.0] | ±124.4 | no detectable difference |
| drawInk / mainTaskMs | 2765.3 [2792.7] | 2623.6 [2634.8] | 0.949 [0.943, 0.953] | ±3.1% | faster |
| drawInk / mainJsMs | 2508.5 [2533.3] | 2363.4 [2373.5] | 0.942 [0.936, 0.949] | ±3.0% | faster |
| typeField / settleMs | 271.4 [289.0] | 240.1 [260.8] | -31.3 [-39.4, -21.8] | ±8.1 | faster |
| typeField / mainTaskMs | 330.7 [340.7] | 289.1 [296.4] | 0.874 [0.848, 0.886] | ±3.6% | faster |
| typeField / mainJsMs | 297.4 [308.2] | 246.7 [253.0] | 0.829 [0.806, 0.844] | ±3.0% | faster |
| undo / settleMs | 693.2 [696.3] | 407.0 [408.3] | -286.3 [-292.5, -284.0] | ±20.8 | faster |
| undo / mainTaskMs | 722.4 [726.0] | 436.6 [440.4] | 0.604 [0.596, 0.609] | ±3.0% | faster |
| undo / mainJsMs | 478.9 [481.8] | 315.0 [318.7] | 0.658 [0.648, 0.667] | ±3.0% | faster |

## `main-now` vs `phase-d-end`

| Row | main-now median [max] | phase-d-end median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.3 [57.9] | 109.5 [114.0] | 2.092 [2.014, 2.131] | ±4.9% | slower |
| open(ebook) / openedMs | 203.7 [204.3] | 288.3 [293.7] | 1.415 [1.408, 1.440] | ±3.0% | slower |
| open(ebook) / firstPaintMs | 367.5 [381.1] | 496.0 [533.1] | 1.350 [1.308, 1.474] | ±3.0% | slower |
| open(ebook) / settledMs | 357.4 [369.3] | 524.5 [546.7] | 1.468 [1.436, 1.501] | ±3.0% | slower |
| open(ebook) / mainTaskMs | 222.0 [230.6] | 410.6 [421.8] | 1.850 [1.817, 1.889] | ±3.0% | slower |
| open(ebook) / mainJsMs | 188.9 [196.4] | 352.3 [359.2] | 1.865 [1.833, 1.898] | ±3.0% | slower |
| open(large) / readyMs | 52.0 [57.0] | 110.4 [118.3] | 2.124 [2.030, 2.152] | ±4.5% | slower |
| open(large) / openedMs | 213.6 [215.5] | 309.7 [318.4] | 1.450 [1.437, 1.479] | ±3.3% | slower |
| open(large) / firstPaintMs | 352.2 [363.3] | 499.5 [529.6] | 1.418 [1.396, 1.495] | ±3.0% | slower |
| open(large) / settledMs | 339.6 [350.4] | 548.4 [574.7] | 1.615 [1.580, 1.689] | ±3.0% | slower |
| open(large) / mainTaskMs | 213.2 [217.6] | 433.8 [443.4] | 2.035 [1.985, 2.082] | ±3.0% | slower |
| open(large) / mainJsMs | 180.7 [187.0] | 372.4 [379.3] | 2.061 [2.010, 2.095] | ±3.0% | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 3120.0 [3168.8] | 3036.1 [3088.4] | 0.973 [0.964, 0.981] | ±3.0% | no detectable difference |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scroll / frameP95Ms | 17.3 [17.5] | 17.1 [18.1] | 0.987 [0.967, 1.033] | ±3.0% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 1017.9 [1031.6] | 1388.4 [1401.7] | 1.364 [1.350, 1.389] | ±3.0% | slower |
| scroll / mainJsMs | 865.0 [879.9] | 1231.3 [1243.7] | 1.423 [1.409, 1.438] | ±3.0% | slower |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 3136.9 [3155.1] | 3070.5 [3097.5] | 0.979 [0.962, 0.985] | ±3.0% | no detectable difference |
| scrollMid / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scrollMid / frameP95Ms | 17.2 [17.5] | 17.2 [17.9] | 0.998 [0.984, 1.025] | ±3.0% | no detectable difference |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 1079.3 [1096.8] | 1455.2 [1493.8] | 1.348 [1.310, 1.365] | ±3.0% | slower |
| scrollMid / mainJsMs | 936.7 [950.4] | 1295.4 [1319.2] | 1.383 [1.369, 1.401] | ±3.0% | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 3122.5 [3154.6] | 3030.9 [3089.2] | 0.971 [0.962, 0.975] | ±3.0% | no detectable difference |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| coldScroll / frameP95Ms | 17.3 [17.6] | 17.2 [18.0] | 0.994 [0.972, 1.017] | ±3.5% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 1045.7 [1052.7] | 1408.5 [1434.9] | 1.347 [1.330, 1.375] | ±3.0% | slower |
| coldScroll / mainJsMs | 875.1 [884.8] | 1251.9 [1272.9] | 1.431 [1.410, 1.441] | ±3.0% | slower |
| jump / settleMs | 150.4 [166.9] | 156.7 [189.9] | +6.3 [-7.1, +23.3] | ±13.6 | no detectable difference |
| jump / mainTaskMs | 155.5 [166.0] | 193.9 [206.3] | 1.247 [1.178, 1.291] | ±4.9% | slower |
| jump / mainJsMs | 135.8 [146.4] | 181.3 [195.0] | 1.335 [1.273, 1.395] | ±7.0% | slower |
| zoom / settleMs | 598.9 [606.3] | 635.9 [655.9] | +37.0 [+30.0, +54.4] | ±32.0 | slower |
| zoom / zoom2SettleMs | 198.2 [217.1] | 227.7 [244.1] | +29.5 [+23.8, +34.6] | ±5.9 | slower |
| zoom / zoom4SettleMs | 396.4 [408.0] | 409.7 [427.1] | +13.3 [+2.7, +22.0] | ±35.3 | no detectable difference |
| zoom / backTo1SettleMs | 176.2 [182.1] | 130.0 [178.5] | -46.3 [-60.7, -18.6] | ±6.1 | faster |
| zoom / mainTaskMs | 526.8 [540.3] | 595.6 [627.2] | 1.131 [1.097, 1.164] | ±3.0% | slower |
| zoom / mainJsMs | 459.4 [472.8] | 532.4 [565.6] | 1.159 [1.112, 1.210] | ±4.1% | slower |
| zoomLarge / settleMs | 315.3 [329.8] | 344.1 [356.7] | +28.7 [+12.5, +41.0] | ±14.9 | slower |
| zoomLarge / zoom2SettleMs | 164.8 [175.2] | 183.3 [196.0] | +18.4 [+10.0, +26.8] | ±14.2 | slower |
| zoomLarge / zoom4SettleMs | 154.8 [165.7] | 163.6 [175.0] | +8.8 [-4.2, +18.9] | ±5.3 | no detectable difference |
| zoomLarge / backTo1SettleMs | 196.8 [210.0] | 204.4 [210.4] | +7.7 [-1.4, +13.7] | ±9.9 | no detectable difference |
| zoomLarge / mainTaskMs | 476.9 [488.2] | 566.7 [578.8] | 1.188 [1.162, 1.214] | ±3.0% | slower |
| zoomLarge / mainJsMs | 411.4 [414.2] | 520.7 [530.8] | 1.266 [1.224, 1.289] | ±3.0% | slower |
| select / settleMs | 271.8 [292.2] | 279.5 [289.4] | +7.7 [+3.0, +12.0] | ±12.4 | no detectable difference |
| select / mainTaskMs | 139.8 [143.8] | 171.3 [180.0] | 1.225 [1.188, 1.270] | ±7.7% | slower |
| select / mainJsMs | 119.8 [123.3] | 150.4 [156.5] | 1.255 [1.225, 1.296] | ±8.5% | slower |
| selectLarge / settleMs | 275.0 [294.7] | 285.4 [304.3] | +10.4 [+3.6, +18.7] | ±12.1 | no detectable difference |
| selectLarge / mainTaskMs | 144.9 [154.9] | 173.1 [180.7] | 1.195 [1.180, 1.233] | ±6.7% | slower |
| selectLarge / mainJsMs | 124.5 [133.3] | 149.1 [158.6] | 1.198 [1.171, 1.248] | ±6.2% | slower |
| search / searchMs | 40.3 [41.3] | 45.2 [46.0] | 1.122 [1.101, 1.139] | ±3.0% | slower |
| search / mainTaskMs | 14.4 [15.2] | 28.3 [29.7] | 1.963 [1.907, 2.052] | ±5.9% | slower |
| search / mainJsMs | 10.4 [10.7] | 23.2 [24.5] | 2.234 [2.185, 2.352] | ±9.5% | slower |
| searchLarge / searchMs | 2235.4 [2254.3] | 1923.6 [1958.2] | 0.861 [0.849, 0.871] | ±3.0% | faster |
| searchLarge / mainTaskMs | 419.7 [422.8] | 1188.0 [1277.3] | 2.831 [2.671, 2.977] | ±3.0% | slower |
| searchLarge / mainJsMs | 359.0 [362.3] | 909.5 [935.7] | 2.533 [2.491, 2.563] | ±3.0% | slower |
| annotate / settleMs | 272.4 [279.6] | 272.3 [280.1] | -0.1 [-8.8, +4.9] | ±8.2 | no detectable difference |
| annotate / mainTaskMs | 154.1 [157.7] | 177.9 [182.8] | 1.155 [1.137, 1.207] | ±5.3% | slower |
| annotate / mainJsMs | 122.4 [129.1] | 149.8 [154.3] | 1.224 [1.194, 1.253] | ±6.8% | slower |
| annotateLarge / settleMs | 277.5 [290.5] | 272.8 [290.7] | -4.7 [-16.3, +3.1] | ±8.3 | no detectable difference |
| annotateLarge / mainTaskMs | 149.9 [153.9] | 182.5 [188.3] | 1.218 [1.169, 1.248] | ±5.1% | slower |
| annotateLarge / mainJsMs | 126.6 [131.2] | 154.2 [158.9] | 1.217 [1.175, 1.252] | ±4.8% | slower |
| hover / settleMs | 1320.8 [1333.1] | 1340.9 [1353.5] | +20.1 [+7.9, +25.9] | ±39.6 | no detectable difference |
| hover / mainTaskMs | 542.0 [550.7] | 873.4 [882.2] | 1.611 [1.590, 1.633] | ±3.0% | slower |
| hover / mainJsMs | 507.5 [516.1] | 844.7 [852.4] | 1.664 [1.639, 1.676] | ±3.0% | slower |
| dragAnnot / settleMs | 448.9 [458.5] | 843.5 [853.1] | +394.7 [+390.5, +403.3] | ±13.5 | slower |
| dragAnnot / mainTaskMs | 271.1 [280.2] | 578.0 [599.2] | 2.132 [2.104, 2.191] | ±3.7% | slower |
| dragAnnot / mainJsMs | 233.6 [241.8] | 550.1 [561.2] | 2.355 [2.324, 2.398] | ±3.5% | slower |
| drawInk / settleMs | 3940.9 [3953.3] | 4037.8 [4120.1] | +96.8 [+78.0, +135.7] | ±118.2 | no detectable difference |
| drawInk / mainTaskMs | 1592.5 [1617.2] | 2623.6 [2634.8] | 1.648 [1.641, 1.654] | ±3.1% | slower |
| drawInk / mainJsMs | 1338.6 [1358.8] | 2363.4 [2373.5] | 1.766 [1.754, 1.775] | ±3.0% | slower |
| typeField / settleMs | 151.9 [166.9] | 240.1 [260.8] | +88.2 [+77.9, +90.6] | ±7.1 | slower |
| typeField / mainTaskMs | 169.8 [176.6] | 289.1 [296.4] | 1.703 [1.647, 1.736] | ±3.6% | slower |
| typeField / mainJsMs | 139.2 [146.9] | 246.7 [253.0] | 1.772 [1.700, 1.809] | ±3.0% | slower |
| undo / settleMs | 200.1 [206.6] | 407.0 [408.3] | +206.9 [+199.7, +207.9] | ±6.0 | slower |
| undo / mainTaskMs | 232.4 [238.6] | 436.6 [440.4] | 1.878 [1.844, 1.914] | ±3.0% | slower |
| undo / mainJsMs | 154.5 [161.3] | 315.0 [318.7] | 2.039 [1.981, 2.076] | ±3.0% | slower |

## Memory

| Row | main median [max] | main-aa median [max] | main-now median [max] | phase-c-base median [max] | phase-c-end median [max] | phase-d-end median [max] |
| --- | --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.40 MiB [35.40 MiB] | 35.39 MiB [35.40 MiB] | 50.72 MiB [50.73 MiB] | 56.36 MiB [56.89 MiB] | 54.26 MiB [54.76 MiB] | 54.24 MiB [54.78 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 27.72 MiB [27.73 MiB] | 27.70 MiB [27.70 MiB] | 27.70 MiB [27.70 MiB] | 27.70 MiB [27.70 MiB] |
| open(large) / heapAfterOpenBytes | 48.19 MiB [48.21 MiB] | 48.19 MiB [48.22 MiB] | 52.68 MiB [52.69 MiB] | 71.23 MiB [71.25 MiB] | 62.93 MiB [62.97 MiB] | 63.00 MiB [63.03 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.28 MiB] | 22.26 MiB [22.28 MiB] | 27.63 MiB [27.64 MiB] | 27.61 MiB [27.61 MiB] | 27.61 MiB [27.61 MiB] | 27.61 MiB [27.62 MiB] |
| soak / heapCycle5Bytes | 37.46 MiB [38.46 MiB] | 37.96 MiB [38.48 MiB] | 58.31 MiB [58.33 MiB] | 63.75 MiB [64.26 MiB] | 61.39 MiB [61.91 MiB] | 61.38 MiB [65.37 MiB] |
| soak / heapCycle30Bytes | 38.28 MiB [39.28 MiB] | 38.78 MiB [39.29 MiB] | 59.36 MiB [59.38 MiB] | 64.27 MiB [64.79 MiB] | 61.91 MiB [62.43 MiB] | 61.91 MiB [65.88 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.05 MiB [0.06 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`phase-c-base`:

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
| wasmBytes | 7.63 MiB | 7.63 MiB |
| wireRefs | 910 | 910 |

`phase-c-end`:

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
| refs | 1026 | 1526 |
| tokens | 31 | 31 |
| wasmBytes | 5.25 MiB | 5.25 MiB |
| wireRefs | 916 | 916 |

`phase-d-end`:

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

