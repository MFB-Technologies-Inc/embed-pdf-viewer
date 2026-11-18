# Viewer perf comparison: 2026-11-17T16-59-17-202Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 5f6b0eca5a7ef2575532a672f3e734503c23ab2a |
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
| perf-fixes-end | research/perf-fixes | a6d4d5d14 | built (fork 17d8073d4, wasm32-eh) | 172b5a139647 | d5eefdeebf95 |

## Gate

- `main`: pass
- `main-aa`: pass
- `main-now`: pass
- `phase-c-base`: pass
- `phase-c-end`: pass
- `phase-d-end`: pass
- `perf-fixes-end`: pass

Not run, because the arm's viewer lacks what the flow needs:

- `main`: `undo`
- `main-aa`: `undo`

## Sizes

| Metric | main | main-aa | main-now | phase-c-base | phase-c-end | phase-d-end | perf-fixes-end |
| --- | --- | --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 7.52 MiB | 14.26 MiB | 14.26 MiB | 14.26 MiB | 14.15 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.35 MiB | 3.79 MiB | 3.79 MiB | 3.79 MiB | 3.77 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 10.35 MiB | 10.70 MiB | 10.73 MiB | 10.73 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 2.44 MiB | 2.50 MiB | 2.50 MiB | 2.50 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 9.50 MiB | 8.50 MiB | 8.50 MiB | 8.50 MiB | 8.50 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 3.12 MiB | 2.90 MiB | 2.90 MiB | 2.90 MiB | 2.90 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

83 of 84 rows pass.

| Row | main median [max] | main-aa median [max] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 62.3 [70.5] | 61.6 [72.1] | 0.988 [0.933, 1.055] | pass |
| open(ebook) / openedMs | 136.3 [176.4] | 136.5 [186.1] | 1.002 [0.959, 1.027] | pass |
| open(ebook) / firstPaintMs | 260.4 [278.1] | 254.7 [295.9] | 0.978 [0.935, 1.057] | pass |
| open(ebook) / settledMs | 399.0 [439.4] | 399.5 [442.6] | 1.001 [0.984, 1.023] | pass |
| open(ebook) / mainTaskMs | 153.9 [158.3] | 154.4 [161.4] | 1.003 [0.978, 1.012] | pass |
| open(ebook) / mainJsMs | 121.7 [124.8] | 121.9 [127.4] | 1.001 [0.981, 1.015] | pass |
| open(large) / readyMs | 63.4 [64.6] | 63.3 [65.9] | 0.999 [0.952, 1.027] | pass |
| open(large) / openedMs | 159.0 [162.0] | 159.5 [163.5] | 1.003 [0.983, 1.027] | pass |
| open(large) / firstPaintMs | 265.8 [286.1] | 259.3 [286.4] | 0.976 [0.921, 1.059] | pass |
| open(large) / settledMs | 275.0 [293.8] | 277.0 [304.5] | 1.007 [0.980, 1.040] | pass |
| open(large) / mainTaskMs | 320.2 [322.2] | 319.6 [321.9] | 0.998 [0.993, 1.009] | pass |
| open(large) / mainJsMs | 289.4 [291.9] | 289.4 [291.2] | 1.000 [0.993, 1.007] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2878.5 [3144.2] | 2898.6 [3028.4] | 1.007 [0.964, 1.035] | pass |
| scroll / frameP50Ms | 11.3 [17.3] | 11.1 [11.8] | 0.980 [0.954, 1.026] | pass |
| scroll / frameP95Ms | 24.6 [27.2] | 25.0 [27.1] | 1.014 [0.858, 1.202] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 591.7 [613.8] | 593.2 [606.5] | 1.002 [0.976, 1.027] | pass |
| scroll / mainJsMs | 468.2 [490.0] | 467.9 [486.7] | 0.999 [0.977, 1.031] | pass |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scrollMid / wallMs | 2908.6 [2947.6] | 2972.7 [3699.8] | 1.022 [0.989, 1.087] | pass |
| scrollMid / frameP50Ms | 11.1 [11.4] | 11.4 [20.8] | 1.031 [0.991, 1.350] | pass |
| scrollMid / frameP95Ms | 23.8 [26.8] | 22.3 [27.1] | 0.941 [0.865, 1.070] | pass |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scrollMid / mainTaskMs | 613.1 [639.0] | 631.2 [690.0] | 1.030 [0.968, 1.051] | pass |
| scrollMid / mainJsMs | 478.7 [491.2] | 490.3 [550.9] | 1.024 [0.971, 1.056] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2840.1 [2902.5] | 2870.8 [2980.1] | 1.011 [0.995, 1.032] | pass |
| coldScroll / frameP50Ms | 10.9 [11.9] | 11.2 [11.9] | 1.025 [0.996, 1.050] | pass |
| coldScroll / frameP95Ms | 22.1 [27.2] | 25.6 [26.4] | 1.159 [0.981, 1.210] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 604.7 [627.3] | 614.3 [623.1] | 1.016 [0.985, 1.026] | pass |
| coldScroll / mainJsMs | 455.1 [466.6] | 459.4 [465.9] | 1.009 [0.982, 1.019] | pass |
| jump / settleMs | 132.1 [145.6] | 135.3 [161.7] | +3.2 [-1.7, +18.6] | pass |
| jump / mainTaskMs | 83.8 [89.1] | 80.9 [89.4] | 0.965 [0.935, 1.015] | pass |
| jump / mainJsMs | 66.3 [68.0] | 66.9 [67.8] | 1.009 [0.953, 1.072] | pass |
| zoom / settleMs | 500.3 [515.2] | 498.9 [530.8] | -1.4 [-15.4, +12.3] | pass |
| zoom / zoom2SettleMs | 172.6 [184.5] | 172.1 [185.3] | -0.6 [-5.4, +7.0] | pass |
| zoom / zoom4SettleMs | 326.2 [350.3] | 323.2 [349.2] | -2.9 [-13.5, +10.5] | pass |
| zoom / backTo1SettleMs | 103.7 [115.7] | 103.5 [111.8] | -0.2 [-6.0, +5.9] | pass |
| zoom / mainTaskMs | 271.8 [285.2] | 273.6 [287.1] | 1.007 [0.975, 1.048] | pass |
| zoom / mainJsMs | 212.1 [224.0] | 216.6 [220.9] | 1.021 [0.984, 1.031] | pass |
| zoomLarge / settleMs | 335.4 [347.3] | 330.1 [382.5] | -5.3 [-19.8, +15.2] | pass |
| zoomLarge / zoom2SettleMs | 156.8 [181.3] | 149.5 [177.1] | -7.4 [-25.6, +3.8] | pass |
| zoomLarge / zoom4SettleMs | 167.3 [194.1] | 186.1 [205.4] | +18.9 [-3.6, +32.6] | pass |
| zoomLarge / backTo1SettleMs | 90.1 [108.8] | 89.3 [93.1] | -0.7 [-10.2, +5.4] | pass |
| zoomLarge / mainTaskMs | 219.0 [223.5] | 218.1 [228.3] | 0.996 [0.978, 1.034] | pass |
| zoomLarge / mainJsMs | 177.8 [181.4] | 175.9 [185.5] | 0.990 [0.969, 1.038] | pass |
| select / settleMs | 209.2 [308.0] | 207.5 [266.5] | -1.7 [-48.6, +19.4] | pass |
| select / mainTaskMs | 79.0 [90.0] | 77.5 [85.3] | 0.981 [0.924, 1.024] | pass |
| select / mainJsMs | 57.4 [66.5] | 55.6 [61.5] | 0.969 [0.912, 1.028] | pass |
| selectLarge / settleMs | 208.4 [241.7] | 220.0 [254.4] | +11.7 [-27.6, +52.9] | pass |
| selectLarge / mainTaskMs | 82.1 [93.2] | 87.0 [92.3] | 1.060 [0.951, 1.189] | pass |
| selectLarge / mainJsMs | 61.5 [68.4] | 65.2 [68.5] | 1.060 [0.954, 1.193] | pass |
| search / searchMs | 69.3 [71.8] | 71.7 [84.0] | 1.036 [1.002, 1.168] | A/A failed |
| search / mainTaskMs | 5.9 [6.8] | 6.4 [15.6] | 1.081 [0.992, 2.358] | pass |
| search / mainJsMs | 4.4 [4.7] | 4.6 [6.5] | 1.048 [0.957, 1.307] | pass |
| searchLarge / searchMs | 1895.9 [1947.1] | 1890.2 [1944.7] | 0.997 [0.978, 1.021] | pass |
| searchLarge / mainTaskMs | 361.5 [364.5] | 359.3 [361.3] | 0.994 [0.988, 1.001] | pass |
| searchLarge / mainJsMs | 309.4 [311.4] | 308.2 [310.0] | 0.996 [0.990, 1.001] | pass |
| annotate / settleMs | 212.0 [259.5] | 235.4 [287.0] | +23.4 [-19.4, +78.0] | pass |
| annotate / mainTaskMs | 89.5 [95.4] | 93.4 [98.5] | 1.044 [0.983, 1.093] | pass |
| annotate / mainJsMs | 58.5 [64.6] | 61.9 [67.3] | 1.057 [0.961, 1.164] | pass |
| annotateLarge / settleMs | 219.3 [260.7] | 223.5 [242.3] | +4.2 [-25.0, +32.9] | pass |
| annotateLarge / mainTaskMs | 90.6 [99.3] | 91.9 [99.2] | 1.014 [0.944, 1.060] | pass |
| annotateLarge / mainJsMs | 63.4 [69.4] | 63.9 [69.8] | 1.008 [0.940, 1.055] | pass |
| hover / settleMs | 1086.0 [1137.7] | 1052.4 [1183.9] | -33.6 [-94.3, +19.9] | pass (no done condition on `main` and `main-aa`) |
| hover / mainTaskMs | 303.6 [309.6] | 298.1 [312.1] | 0.982 [0.962, 0.999] | pass (no done condition on `main` and `main-aa`) |
| hover / mainJsMs | 273.1 [279.0] | 268.8 [281.3] | 0.984 [0.964, 0.999] | pass (no done condition on `main` and `main-aa`) |
| dragAnnot / settleMs | 307.0 [374.5] | 298.3 [348.3] | -8.6 [-51.7, +17.3] | pass |
| dragAnnot / mainTaskMs | 134.3 [143.3] | 131.1 [146.6] | 0.976 [0.907, 1.051] | pass |
| dragAnnot / mainJsMs | 101.3 [108.7] | 98.5 [109.6] | 0.972 [0.933, 1.040] | pass |
| drawInk / settleMs | 2907.5 [3040.5] | 2885.9 [3025.8] | -21.6 [-128.1, +38.1] | pass |
| drawInk / mainTaskMs | 677.6 [705.5] | 671.9 [704.3] | 0.992 [0.958, 1.026] | pass |
| drawInk / mainJsMs | 494.9 [518.6] | 487.2 [511.7] | 0.985 [0.950, 1.024] | pass |
| typeField / settleMs | 170.9 [178.6] | 164.3 [173.4] | -6.6 [-11.6, +2.4] | pass |
| typeField / mainTaskMs | 126.4 [131.2] | 125.2 [130.6] | 0.990 [0.956, 1.007] | pass |
| typeField / mainJsMs | 89.1 [91.9] | 87.6 [90.7] | 0.984 [0.959, 1.004] | pass |

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
| open(ebook) / readyMs | 62.3 [70.5] | 52.5 [58.7] | 0.842 [0.806, 0.916] | ±6.7% | faster |
| open(ebook) / openedMs | 136.3 [176.4] | 204.5 [242.8] | 1.501 [1.436, 1.625] | ±4.1% | slower |
| open(ebook) / firstPaintMs | 260.4 [278.1] | 325.5 [379.6] | 1.250 [1.193, 1.326] | ±6.5% | slower |
| open(ebook) / settledMs | 399.0 [439.4] | 356.6 [394.5] | 0.894 [0.877, 0.942] | ±3.0% | faster |
| open(ebook) / mainTaskMs | 153.9 [158.3] | 216.0 [225.0] | 1.403 [1.378, 1.439] | ±3.0% | slower |
| open(ebook) / mainJsMs | 121.7 [124.8] | 181.8 [190.0] | 1.494 [1.460, 1.532] | ±3.0% | slower |
| open(large) / readyMs | 63.4 [64.6] | 52.6 [60.2] | 0.831 [0.820, 0.899] | ±4.8% | faster |
| open(large) / openedMs | 159.0 [162.0] | 214.4 [217.5] | 1.348 [1.331, 1.370] | ±3.0% | slower |
| open(large) / firstPaintMs | 265.8 [286.1] | 315.3 [343.8] | 1.186 [1.126, 1.246] | ±7.9% | slower |
| open(large) / settledMs | 275.0 [293.8] | 353.1 [364.0] | 1.284 [1.227, 1.326] | ±4.0% | slower |
| open(large) / mainTaskMs | 320.2 [322.2] | 214.3 [220.4] | 0.669 [0.662, 0.675] | ±3.0% | faster |
| open(large) / mainJsMs | 289.4 [291.9] | 179.0 [184.9] | 0.618 [0.609, 0.624] | ±3.0% | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 2878.5 [3144.2] | 2975.1 [3046.8] | 1.034 [0.993, 1.045] | ±3.6% | no detectable difference |
| scroll / frameP50Ms | 11.3 [17.3] | 11.0 [11.7] | 0.971 [0.943, 1.014] | ±4.6% | no detectable difference |
| scroll / frameP95Ms | 24.6 [27.2] | 25.0 [27.2] | 1.013 [0.838, 1.183] | ±20.2% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 591.7 [613.8] | 869.7 [889.2] | 1.470 [1.436, 1.498] | ±3.0% | slower |
| scroll / mainJsMs | 468.2 [490.0] | 735.2 [747.4] | 1.570 [1.545, 1.594] | ±3.1% | slower |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 2908.6 [2947.6] | 3055.8 [3094.9] | 1.051 [1.033, 1.075] | ±8.7% | no detectable difference |
| scrollMid / frameP50Ms | 11.1 [11.4] | 10.9 [11.8] | 0.986 [0.977, 1.025] | ±35.0% | no detectable difference |
| scrollMid / frameP95Ms | 23.8 [26.8] | 26.0 [27.2] | 1.094 [1.036, 1.184] | ±13.5% | no detectable difference |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 613.1 [639.0] | 942.9 [994.8] | 1.538 [1.504, 1.587] | ±5.1% | slower |
| scrollMid / mainJsMs | 478.7 [491.2] | 820.9 [856.6] | 1.715 [1.682, 1.766] | ±5.6% | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 2840.1 [2902.5] | 2983.7 [3026.4] | 1.051 [1.035, 1.067] | ±3.2% | slower |
| coldScroll / frameP50Ms | 10.9 [11.9] | 11.1 [11.4] | 1.017 [0.999, 1.043] | ±5.0% | no detectable difference |
| coldScroll / frameP95Ms | 22.1 [27.2] | 25.9 [27.2] | 1.174 [0.960, 1.218] | ±21.0% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 604.7 [627.3] | 906.9 [940.0] | 1.500 [1.466, 1.532] | ±3.0% | slower |
| coldScroll / mainJsMs | 455.1 [466.6] | 754.7 [776.5] | 1.658 [1.636, 1.696] | ±3.0% | slower |
| jump / settleMs | 132.1 [145.6] | 150.9 [161.4] | +18.8 [+12.2, +26.4] | ±18.6 | slower |
| jump / mainTaskMs | 83.8 [89.1] | 134.1 [138.2] | 1.599 [1.529, 1.671] | ±6.5% | slower |
| jump / mainJsMs | 66.3 [68.0] | 115.5 [125.0] | 1.742 [1.691, 1.873] | ±7.2% | slower |
| zoom / settleMs | 500.3 [515.2] | 354.9 [660.7] | -145.3 [-156.7, +145.4] | ±15.4 | no detectable difference |
| zoom / zoom2SettleMs | 172.6 [184.5] | 205.6 [229.3] | +32.9 [+29.1, +42.4] | ±7.0 | slower |
| zoom / zoom4SettleMs | 326.2 [350.3] | 147.0 [439.7] | -179.2 [-189.2, +101.2] | ±13.5 | no detectable difference |
| zoom / backTo1SettleMs | 103.7 [115.7] | 82.2 [103.7] | -21.4 [-30.9, -4.5] | ±6.0 | faster |
| zoom / mainTaskMs | 271.8 [285.2] | 353.3 [397.7] | 1.300 [1.260, 1.423] | ±4.8% | slower |
| zoom / mainJsMs | 212.1 [224.0] | 284.1 [316.7] | 1.340 [1.279, 1.435] | ±3.1% | slower |
| zoomLarge / settleMs | 335.4 [347.3] | 296.2 [326.3] | -39.1 [-55.5, -20.7] | ±19.8 | faster |
| zoomLarge / zoom2SettleMs | 156.8 [181.3] | 157.0 [168.4] | +0.1 [-18.1, +7.9] | ±25.6 | no detectable difference |
| zoomLarge / zoom4SettleMs | 167.3 [194.1] | 142.9 [157.9] | -24.3 [-46.8, -5.7] | ±32.6 | no detectable difference |
| zoomLarge / backTo1SettleMs | 90.1 [108.8] | 104.8 [119.9] | +14.7 [+9.4, +22.9] | ±10.2 | slower |
| zoomLarge / mainTaskMs | 219.0 [223.5] | 325.0 [338.1] | 1.484 [1.438, 1.538] | ±3.4% | slower |
| zoomLarge / mainJsMs | 177.8 [181.4] | 269.8 [281.5] | 1.518 [1.471, 1.578] | ±3.8% | slower |
| select / settleMs | 209.2 [308.0] | 211.1 [242.0] | +2.0 [-51.1, +11.8] | ±48.6 | no detectable difference |
| select / mainTaskMs | 79.0 [90.0] | 91.1 [95.2] | 1.153 [1.086, 1.212] | ±7.6% | slower |
| select / mainJsMs | 57.4 [66.5] | 73.7 [77.9] | 1.284 [1.220, 1.370] | ±8.8% | slower |
| selectLarge / settleMs | 208.4 [241.7] | 227.0 [255.3] | +18.6 [-13.7, +48.1] | ±52.9 | no detectable difference |
| selectLarge / mainTaskMs | 82.1 [93.2] | 108.2 [110.5] | 1.317 [1.201, 1.479] | ±18.9% | slower |
| selectLarge / mainJsMs | 61.5 [68.4] | 90.2 [93.2] | 1.467 [1.330, 1.659] | ±19.3% | slower |
| search / searchMs | 69.3 [71.8] | 40.6 [46.6] | 0.586 [0.466, 0.634] |  | unreliable (A/A failed) |
| search / mainTaskMs | 5.9 [6.8] | 19.1 [23.0] | 3.222 [2.736, 3.610] | ±135.8% | slower |
| search / mainJsMs | 4.4 [4.7] | 9.3 [14.0] | 2.103 [1.883, 2.547] | ±30.7% | slower |
| searchLarge / searchMs | 1895.9 [1947.1] | 2241.7 [2256.6] | 1.182 [1.159, 1.189] | ±3.0% | slower |
| searchLarge / mainTaskMs | 361.5 [364.5] | 412.1 [415.8] | 1.140 [1.123, 1.149] | ±3.0% | slower |
| searchLarge / mainJsMs | 309.4 [311.4] | 348.0 [351.3] | 1.125 [1.116, 1.132] | ±3.0% | slower |
| annotate / settleMs | 212.0 [259.5] | 234.3 [265.6] | +22.3 [-31.4, +69.1] | ±78.0 | no detectable difference |
| annotate / mainTaskMs | 89.5 [95.4] | 110.5 [117.7] | 1.234 [1.183, 1.284] | ±9.3% | slower |
| annotate / mainJsMs | 58.5 [64.6] | 88.9 [95.6] | 1.518 [1.404, 1.612] | ±16.4% | slower |
| annotateLarge / settleMs | 219.3 [260.7] | 215.0 [236.5] | -4.3 [-32.0, +20.6] | ±32.9 | no detectable difference |
| annotateLarge / mainTaskMs | 90.6 [99.3] | 113.8 [122.6] | 1.256 [1.170, 1.303] | ±6.0% | slower |
| annotateLarge / mainJsMs | 63.4 [69.4] | 92.4 [100.4] | 1.457 [1.360, 1.520] | ±6.0% | slower |
| hover / settleMs | 1086.0 [1137.7] | 1028.2 [1085.3] | -57.8 [-114.8, -16.6] | ±94.3 | no detectable difference (no done condition on `main`) |
| hover / mainTaskMs | 303.6 [309.6] | 453.1 [459.0] | 1.492 [1.461, 1.508] | ±3.8% | slower (no done condition on `main`) |
| hover / mainJsMs | 273.1 [279.0] | 422.7 [429.7] | 1.548 [1.514, 1.564] | ±3.6% | slower (no done condition on `main`) |
| dragAnnot / settleMs | 307.0 [374.5] | 386.5 [419.7] | +79.5 [+14.7, +99.7] | ±51.7 | slower |
| dragAnnot / mainTaskMs | 134.3 [143.3] | 227.5 [230.9] | 1.693 [1.630, 1.760] | ±9.3% | slower |
| dragAnnot / mainJsMs | 101.3 [108.7] | 201.9 [205.5] | 1.993 [1.903, 2.060] | ±6.7% | slower |
| drawInk / settleMs | 2907.5 [3040.5] | 3093.3 [3235.9] | +185.8 [+114.9, +277.3] | ±128.1 | slower |
| drawInk / mainTaskMs | 677.6 [705.5] | 1343.7 [1362.3] | 1.983 [1.941, 2.041] | ±4.2% | slower |
| drawInk / mainJsMs | 494.9 [518.6] | 1136.0 [1144.2] | 2.296 [2.238, 2.364] | ±5.0% | slower |
| typeField / settleMs | 170.9 [178.6] | 151.0 [161.4] | -19.9 [-26.0, -10.3] | ±11.6 | faster |
| typeField / mainTaskMs | 126.4 [131.2] | 125.8 [128.4] | 0.995 [0.959, 1.004] | ±4.4% | no detectable difference |
| typeField / mainJsMs | 89.1 [91.9] | 93.3 [95.7] | 1.048 [0.990, 1.070] | ±4.1% | no detectable difference |

## `main-now` vs `plugins-next`

Not run: `plugins-next`.

## `plugins-next` vs `plugins-next-lto`

Not run: `plugins-next`, `plugins-next-lto`.

## `phase-c-base` vs `phase-c-end`

| Row | phase-c-base median [max] | phase-c-end median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 115.0 [142.5] | 114.6 [124.8] | 0.996 [0.875, 1.063] | ±6.7% | no detectable difference |
| open(ebook) / openedMs | 295.6 [352.0] | 297.0 [357.8] | 1.005 [0.916, 1.101] | ±4.1% | no detectable difference |
| open(ebook) / firstPaintMs | 495.7 [568.1] | 519.2 [547.5] | 1.047 [0.971, 1.122] | ±6.5% | no detectable difference |
| open(ebook) / settledMs | 542.0 [596.3] | 535.1 [593.7] | 0.987 [0.952, 1.030] | ±3.0% | no detectable difference |
| open(ebook) / mainTaskMs | 420.6 [428.8] | 395.6 [416.6] | 0.941 [0.926, 0.973] | ±3.0% | faster |
| open(ebook) / mainJsMs | 360.1 [368.8] | 336.0 [357.8] | 0.933 [0.917, 0.959] | ±3.0% | faster |
| open(large) / readyMs | 110.9 [119.9] | 110.4 [122.0] | 0.995 [0.970, 1.048] | ±4.8% | no detectable difference |
| open(large) / openedMs | 313.6 [323.1] | 313.3 [319.9] | 0.999 [0.977, 1.015] | ±3.0% | no detectable difference |
| open(large) / firstPaintMs | 503.1 [510.7] | 503.0 [546.0] | 1.000 [0.983, 1.040] | ±7.9% | no detectable difference |
| open(large) / settledMs | 549.4 [575.3] | 540.6 [593.9] | 0.984 [0.957, 1.044] | ±4.0% | no detectable difference |
| open(large) / mainTaskMs | 443.6 [452.0] | 423.4 [435.9] | 0.954 [0.942, 0.964] | ±3.0% | faster |
| open(large) / mainJsMs | 384.4 [392.9] | 365.4 [370.1] | 0.950 [0.942, 0.964] | ±3.0% | faster |
| scroll / settleMs | 0.0 [5.4] | 0.0 [2.2] | +0.0 [-0.6, +0.8] | ±0.0 | no detectable difference |
| scroll / wallMs | 2752.0 [2892.9] | 2695.4 [2814.7] | 0.979 [0.941, 1.029] | ±3.6% | no detectable difference |
| scroll / frameP50Ms | 11.1 [11.3] | 11.1 [11.7] | 0.997 [0.982, 1.035] | ±4.6% | no detectable difference |
| scroll / frameP95Ms | 24.4 [27.2] | 24.9 [27.2] | 1.020 [0.892, 1.121] | ±20.2% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 1449.8 [1485.2] | 1210.1 [1253.6] | 0.835 [0.814, 0.854] | ±3.0% | faster |
| scroll / mainJsMs | 1320.0 [1368.2] | 1067.3 [1087.8] | 0.809 [0.793, 0.823] | ±3.1% | faster |
| scrollMid / settleMs | 0.0 [2.6] | 0.0 [1.2] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 2874.5 [3253.4] | 2644.1 [2865.0] | 0.920 [0.886, 0.984] | ±8.7% | no detectable difference |
| scrollMid / frameP50Ms | 11.1 [11.6] | 11.0 [11.3] | 0.990 [0.967, 1.006] | ±35.0% | no detectable difference |
| scrollMid / frameP95Ms | 25.8 [27.2] | 25.7 [27.2] | 0.993 [0.906, 1.070] | ±13.5% | no detectable difference |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 1706.8 [1799.5] | 1274.3 [1316.2] | 0.747 [0.723, 0.766] | ±5.1% | faster |
| scrollMid / mainJsMs | 1604.1 [1687.7] | 1124.0 [1157.1] | 0.701 [0.688, 0.722] | ±5.6% | faster |
| coldScroll / settleMs | 0.5 [7.3] | 0.0 [2.1] | -0.5 [-1.4, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 2713.0 [2890.7] | 2685.2 [2827.3] | 0.990 [0.946, 1.083] | ±3.2% | no detectable difference |
| coldScroll / frameP50Ms | 11.1 [11.8] | 11.2 [11.8] | 1.012 [0.967, 1.054] | ±5.0% | no detectable difference |
| coldScroll / frameP95Ms | 26.4 [27.2] | 26.5 [27.2] | 1.001 [0.913, 1.080] | ±21.0% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 1464.3 [1531.5] | 1251.0 [1275.7] | 0.854 [0.825, 0.879] | ±3.0% | faster |
| coldScroll / mainJsMs | 1322.5 [1386.2] | 1096.8 [1117.7] | 0.829 [0.803, 0.854] | ±3.0% | faster |
| jump / settleMs | 167.0 [188.4] | 160.1 [175.7] | -6.9 [-28.4, +3.6] | ±18.6 | no detectable difference |
| jump / mainTaskMs | 242.1 [274.8] | 178.2 [193.6] | 0.736 [0.691, 0.764] | ±6.5% | faster |
| jump / mainJsMs | 223.5 [240.4] | 164.7 [171.3] | 0.737 [0.699, 0.763] | ±7.2% | faster |
| zoom / settleMs | 676.6 [704.1] | 661.0 [682.3] | -15.6 [-275.2, +252.8] | ±20.3 | no detectable difference |
| zoom / zoom2SettleMs | 239.4 [249.6] | 229.4 [238.1] | -10.0 [-21.2, +1.7] | ±7.2 | no detectable difference |
| zoom / zoom4SettleMs | 443.0 [458.4] | 437.2 [454.2] | -5.8 [-279.7, +267.7] | ±13.5 | no detectable difference |
| zoom / backTo1SettleMs | 91.7 [111.7] | 90.4 [103.4] | -1.3 [-8.2, +9.2] | ±6.0 | no detectable difference |
| zoom / mainTaskMs | 504.7 [531.0] | 475.4 [491.4] | 0.942 [0.881, 0.970] | ±4.8% | faster |
| zoom / mainJsMs | 450.9 [477.8] | 420.7 [430.7] | 0.933 [0.878, 0.962] | ±3.1% | faster |
| zoomLarge / settleMs | 348.2 [362.7] | 325.5 [349.0] | -22.7 [-44.5, -8.8] | ±19.8 | faster |
| zoomLarge / zoom2SettleMs | 184.7 [204.3] | 168.5 [189.3] | -16.2 [-29.2, -7.5] | ±25.6 | no detectable difference |
| zoomLarge / zoom4SettleMs | 158.9 [174.9] | 160.0 [165.3] | +1.1 [-16.8, +4.6] | ±32.6 | no detectable difference |
| zoomLarge / backTo1SettleMs | 104.3 [115.1] | 94.4 [107.7] | -9.9 [-13.8, -5.2] | ±10.2 | no detectable difference |
| zoomLarge / mainTaskMs | 453.3 [466.1] | 384.1 [398.1] | 0.847 [0.820, 0.874] | ±3.4% | faster |
| zoomLarge / mainJsMs | 413.2 [429.2] | 346.1 [360.2] | 0.838 [0.809, 0.860] | ±3.8% | faster |
| select / settleMs | 260.1 [369.7] | 293.7 [328.4] | +33.7 [-15.5, +67.0] | ±48.6 | no detectable difference |
| select / mainTaskMs | 145.9 [171.3] | 136.1 [146.4] | 0.933 [0.867, 0.953] | ±7.6% | no detectable difference |
| select / mainJsMs | 127.1 [151.2] | 122.0 [132.5] | 0.960 [0.884, 0.981] | ±8.8% | no detectable difference |
| selectLarge / settleMs | 284.1 [317.3] | 270.4 [301.2] | -13.7 [-37.7, +19.3] | ±52.9 | no detectable difference |
| selectLarge / mainTaskMs | 154.4 [173.8] | 135.5 [146.4] | 0.878 [0.824, 0.906] | ±18.9% | no detectable difference |
| selectLarge / mainJsMs | 142.7 [162.0] | 122.5 [131.9] | 0.859 [0.801, 0.886] | ±19.3% | no detectable difference |
| search / searchMs | 46.0 [50.8] | 46.1 [46.4] | 1.002 [0.980, 1.007] |  | unreliable (A/A failed) |
| search / mainTaskMs | 30.4 [34.8] | 27.4 [28.8] | 0.902 [0.877, 0.934] | ±135.8% | no detectable difference |
| search / mainJsMs | 25.0 [29.8] | 21.9 [22.7] | 0.877 [0.844, 0.904] | ±30.7% | no detectable difference |
| searchLarge / searchMs | 2187.2 [2207.4] | 1926.8 [1960.4] | 0.881 [0.871, 0.887] | ±3.0% | faster |
| searchLarge / mainTaskMs | 1478.3 [1524.4] | 1150.8 [1225.0] | 0.778 [0.698, 0.821] | ±3.0% | faster |
| searchLarge / mainJsMs | 1258.3 [1270.4] | 886.7 [907.7] | 0.705 [0.691, 0.716] | ±3.0% | faster |
| annotate / settleMs | 232.8 [268.8] | 206.2 [269.9] | -26.6 [-45.9, +17.3] | ±78.0 | no detectable difference |
| annotate / mainTaskMs | 150.0 [157.4] | 123.0 [141.2] | 0.820 [0.776, 0.894] | ±9.3% | faster |
| annotate / mainJsMs | 127.3 [133.3] | 104.1 [121.2] | 0.818 [0.769, 0.908] | ±16.4% | faster |
| annotateLarge / settleMs | 246.0 [279.0] | 210.0 [263.0] | -36.1 [-72.0, -10.5] | ±32.9 | faster |
| annotateLarge / mainTaskMs | 158.3 [175.4] | 126.6 [136.4] | 0.800 [0.718, 0.822] | ±6.0% | faster |
| annotateLarge / mainJsMs | 141.6 [157.1] | 108.3 [117.9] | 0.765 [0.682, 0.796] | ±6.0% | faster |
| hover / settleMs | 1552.2 [1649.2] | 1256.4 [1359.8] | -295.8 [-333.0, -269.1] | ±94.3 | faster |
| hover / mainTaskMs | 1180.9 [1218.7] | 863.2 [870.9] | 0.731 [0.724, 0.736] | ±3.8% | faster |
| hover / mainJsMs | 1147.8 [1189.4] | 832.8 [843.7] | 0.726 [0.717, 0.729] | ±3.6% | faster |
| dragAnnot / settleMs | 882.0 [939.2] | 702.0 [758.8] | -180.0 [-227.8, -159.4] | ±51.7 | faster |
| dragAnnot / mainTaskMs | 696.8 [708.3] | 554.4 [560.8] | 0.796 [0.785, 0.803] | ±9.3% | faster |
| dragAnnot / mainJsMs | 669.8 [682.7] | 526.9 [537.8] | 0.787 [0.777, 0.793] | ±6.7% | faster |
| drawInk / settleMs | 5707.0 [6313.4] | 5755.7 [5937.2] | +48.6 [-74.6, +168.4] | ±171.2 | no detectable difference |
| drawInk / mainTaskMs | 3701.7 [3940.0] | 2749.6 [2777.7] | 0.743 [0.734, 0.750] | ±4.2% | faster |
| drawInk / mainJsMs | 3467.1 [3705.9] | 2541.7 [2563.0] | 0.733 [0.724, 0.739] | ±5.0% | faster |
| typeField / settleMs | 285.9 [298.0] | 263.1 [275.9] | -22.8 [-35.9, -7.7] | ±11.6 | faster |
| typeField / mainTaskMs | 297.5 [309.5] | 273.1 [285.9] | 0.918 [0.893, 0.938] | ±4.4% | faster |
| typeField / mainJsMs | 272.6 [286.3] | 248.4 [261.5] | 0.911 [0.883, 0.936] | ±4.1% | faster |
| undo / settleMs | 845.6 [977.7] | 695.7 [699.5] | -149.9 [-155.4, -136.5] | ±25.4 | faster (no A/A band) |
| undo / mainTaskMs | 855.0 [988.7] | 704.1 [709.4] | 0.823 [0.817, 0.835] | ±3.0% | faster (no A/A band) |
| undo / mainJsMs | 614.9 [729.7] | 457.2 [459.8] | 0.744 [0.740, 0.764] | ±3.0% | faster (no A/A band) |

## `phase-c-end` vs `phase-d-end`

| Row | phase-c-end median [max] | phase-d-end median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 114.6 [124.8] | 110.6 [169.3] | 0.965 [0.941, 1.029] | ±6.7% | no detectable difference |
| open(ebook) / openedMs | 297.0 [357.8] | 290.0 [363.7] | 0.977 [0.888, 1.022] | ±4.1% | no detectable difference |
| open(ebook) / firstPaintMs | 519.2 [547.5] | 494.1 [566.3] | 0.952 [0.883, 1.066] | ±6.5% | no detectable difference |
| open(ebook) / settledMs | 535.1 [593.7] | 534.3 [613.9] | 0.999 [0.956, 1.029] | ±3.0% | no detectable difference |
| open(ebook) / mainTaskMs | 395.6 [416.6] | 407.9 [418.8] | 1.031 [0.997, 1.047] | ±3.0% | no detectable difference |
| open(ebook) / mainJsMs | 336.0 [357.8] | 346.9 [386.4] | 1.032 [1.002, 1.048] | ±3.0% | slower |
| open(large) / readyMs | 110.4 [122.0] | 110.5 [120.4] | 1.001 [0.951, 1.033] | ±4.8% | no detectable difference |
| open(large) / openedMs | 313.3 [319.9] | 312.8 [319.5] | 0.998 [0.986, 1.018] | ±3.0% | no detectable difference |
| open(large) / firstPaintMs | 503.0 [546.0] | 501.5 [520.7] | 0.997 [0.953, 1.026] | ±7.9% | no detectable difference |
| open(large) / settledMs | 540.6 [593.9] | 555.5 [591.6] | 1.028 [0.957, 1.069] | ±4.0% | no detectable difference |
| open(large) / mainTaskMs | 423.4 [435.9] | 426.5 [448.1] | 1.007 [0.995, 1.027] | ±3.0% | no detectable difference |
| open(large) / mainJsMs | 365.4 [370.1] | 368.5 [382.4] | 1.009 [0.999, 1.016] | ±3.0% | no detectable difference |
| scroll / settleMs | 0.0 [2.2] | 0.0 [1.7] | +0.0 [-0.8, +1.2] | ±0.0 | no detectable difference |
| scroll / wallMs | 2695.4 [2814.7] | 2702.4 [2949.0] | 1.003 [0.948, 1.069] | ±3.6% | no detectable difference |
| scroll / frameP50Ms | 11.1 [11.7] | 11.1 [11.4] | 1.005 [0.974, 1.023] | ±4.6% | no detectable difference |
| scroll / frameP95Ms | 24.9 [27.2] | 25.8 [27.2] | 1.036 [0.916, 1.152] | ±20.2% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 1210.1 [1253.6] | 1216.5 [1253.7] | 1.005 [0.987, 1.037] | ±3.0% | no detectable difference |
| scroll / mainJsMs | 1067.3 [1087.8] | 1069.1 [1106.4] | 1.002 [0.980, 1.029] | ±3.1% | no detectable difference |
| scrollMid / settleMs | 0.0 [1.2] | 0.0 [7.3] | +0.0 [+0.0, +1.3] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 2644.1 [2865.0] | 2612.2 [2871.8] | 0.988 [0.935, 1.031] | ±8.7% | no detectable difference |
| scrollMid / frameP50Ms | 11.0 [11.3] | 11.0 [11.5] | 1.005 [0.989, 1.025] | ±35.0% | no detectable difference |
| scrollMid / frameP95Ms | 25.7 [27.2] | 26.5 [27.2] | 1.034 [0.946, 1.108] | ±13.5% | no detectable difference |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 1274.3 [1316.2] | 1261.9 [1335.6] | 0.990 [0.964, 1.027] | ±5.1% | no detectable difference |
| scrollMid / mainJsMs | 1124.0 [1157.1] | 1120.0 [1174.7] | 0.996 [0.977, 1.020] | ±5.6% | no detectable difference |
| coldScroll / settleMs | 0.0 [2.1] | 0.0 [2.0] | +0.0 [+0.0, +0.8] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 2685.2 [2827.3] | 2623.9 [2769.5] | 0.977 [0.940, 1.026] | ±3.2% | no detectable difference |
| coldScroll / frameP50Ms | 11.2 [11.8] | 11.3 [11.6] | 1.011 [0.962, 1.045] | ±5.0% | no detectable difference |
| coldScroll / frameP95Ms | 26.5 [27.2] | 26.8 [27.2] | 1.014 [0.975, 1.125] | ±21.0% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 1251.0 [1275.7] | 1242.1 [1257.6] | 0.993 [0.965, 1.015] | ±3.0% | no detectable difference |
| coldScroll / mainJsMs | 1096.8 [1117.7] | 1090.8 [1108.2] | 0.994 [0.963, 1.017] | ±3.0% | no detectable difference |
| jump / settleMs | 160.1 [175.7] | 157.6 [200.4] | -2.5 [-17.2, +22.8] | ±18.6 | no detectable difference |
| jump / mainTaskMs | 178.2 [193.6] | 178.3 [188.4] | 1.001 [0.961, 1.062] | ±6.5% | no detectable difference |
| jump / mainJsMs | 164.7 [171.3] | 166.1 [176.3] | 1.008 [0.964, 1.065] | ±7.2% | no detectable difference |
| zoom / settleMs | 661.0 [682.3] | 668.4 [688.9] | +7.4 [-264.9, +267.0] | ±19.8 | no detectable difference |
| zoom / zoom2SettleMs | 229.4 [238.1] | 229.5 [236.7] | +0.2 [-6.3, +7.7] | ±7.0 | no detectable difference |
| zoom / zoom4SettleMs | 437.2 [454.2] | 439.6 [457.4] | +2.4 [-274.9, +277.7] | ±13.5 | no detectable difference |
| zoom / backTo1SettleMs | 90.4 [103.4] | 92.0 [102.6] | +1.6 [-8.5, +10.8] | ±6.0 | no detectable difference |
| zoom / mainTaskMs | 475.4 [491.4] | 470.2 [485.0] | 0.989 [0.956, 1.051] | ±4.8% | no detectable difference |
| zoom / mainJsMs | 420.7 [430.7] | 416.1 [428.0] | 0.989 [0.950, 1.039] | ±3.1% | no detectable difference |
| zoomLarge / settleMs | 325.5 [349.0] | 316.3 [347.5] | -9.2 [-20.3, +14.0] | ±19.8 | no detectable difference |
| zoomLarge / zoom2SettleMs | 168.5 [189.3] | 164.8 [187.6] | -3.7 [-9.8, +9.2] | ±25.6 | no detectable difference |
| zoomLarge / zoom4SettleMs | 160.0 [165.3] | 150.5 [163.8] | -9.5 [-12.1, +8.6] | ±32.6 | no detectable difference |
| zoomLarge / backTo1SettleMs | 94.4 [107.7] | 94.3 [104.5] | -0.1 [-5.6, +5.9] | ±10.2 | no detectable difference |
| zoomLarge / mainTaskMs | 384.1 [398.1] | 387.7 [402.6] | 1.009 [0.973, 1.043] | ±3.4% | no detectable difference |
| zoomLarge / mainJsMs | 346.1 [360.2] | 349.5 [358.3] | 1.010 [0.976, 1.041] | ±3.8% | no detectable difference |
| select / settleMs | 293.7 [328.4] | 279.7 [339.4] | -14.0 [-51.5, +18.1] | ±48.6 | no detectable difference |
| select / mainTaskMs | 136.1 [146.4] | 131.1 [143.8] | 0.963 [0.923, 1.036] | ±7.6% | no detectable difference |
| select / mainJsMs | 122.0 [132.5] | 115.8 [128.0] | 0.949 [0.914, 1.031] | ±8.8% | no detectable difference |
| selectLarge / settleMs | 270.4 [301.2] | 282.5 [318.0] | +12.0 [-13.7, +30.1] | ±52.9 | no detectable difference |
| selectLarge / mainTaskMs | 135.5 [146.4] | 136.9 [149.8] | 1.010 [0.931, 1.081] | ±18.9% | no detectable difference |
| selectLarge / mainJsMs | 122.5 [131.9] | 123.5 [136.6] | 1.008 [0.920, 1.085] | ±19.3% | no detectable difference |
| search / searchMs | 46.1 [46.4] | 45.6 [51.0] | 0.988 [0.978, 1.071] |  | unreliable (A/A failed) |
| search / mainTaskMs | 27.4 [28.8] | 28.0 [33.2] | 1.023 [0.987, 1.146] | ±135.8% | no detectable difference |
| search / mainJsMs | 21.9 [22.7] | 22.1 [27.7] | 1.011 [0.983, 1.157] | ±30.7% | no detectable difference |
| searchLarge / searchMs | 1926.8 [1960.4] | 1928.8 [1954.7] | 1.001 [0.992, 1.012] | ±3.0% | no detectable difference |
| searchLarge / mainTaskMs | 1150.8 [1225.0] | 1150.2 [1236.9] | 1.000 [0.918, 1.136] | ±3.0% | no detectable difference |
| searchLarge / mainJsMs | 886.7 [907.7] | 897.9 [909.2] | 1.013 [0.994, 1.030] | ±3.0% | no detectable difference |
| annotate / settleMs | 206.2 [269.9] | 210.7 [237.6] | +4.6 [-37.6, +24.1] | ±78.0 | no detectable difference |
| annotate / mainTaskMs | 123.0 [141.2] | 120.7 [129.7] | 0.982 [0.897, 1.035] | ±9.3% | no detectable difference |
| annotate / mainJsMs | 104.1 [121.2] | 101.4 [110.5] | 0.974 [0.880, 1.037] | ±16.4% | no detectable difference |
| annotateLarge / settleMs | 210.0 [263.0] | 196.0 [263.5] | -14.0 [-24.5, +30.7] | ±32.9 | no detectable difference |
| annotateLarge / mainTaskMs | 126.6 [136.4] | 124.0 [133.7] | 0.980 [0.932, 1.088] | ±6.0% | no detectable difference |
| annotateLarge / mainJsMs | 108.3 [117.9] | 106.2 [115.1] | 0.980 [0.925, 1.098] | ±6.0% | no detectable difference |
| hover / settleMs | 1256.4 [1359.8] | 1214.6 [1272.8] | -41.8 [-64.1, +1.3] | ±94.3 | no detectable difference |
| hover / mainTaskMs | 863.2 [870.9] | 821.0 [846.8] | 0.951 [0.940, 0.962] | ±3.8% | faster |
| hover / mainJsMs | 832.8 [843.7] | 793.8 [819.2] | 0.953 [0.942, 0.966] | ±3.6% | faster |
| dragAnnot / settleMs | 702.0 [758.8] | 700.2 [737.4] | -1.8 [-38.2, +49.9] | ±51.7 | no detectable difference |
| dragAnnot / mainTaskMs | 554.4 [560.8] | 525.4 [531.9] | 0.948 [0.940, 0.958] | ±9.3% | no detectable difference |
| dragAnnot / mainJsMs | 526.9 [537.8] | 502.1 [508.7] | 0.953 [0.943, 0.962] | ±6.7% | no detectable difference |
| drawInk / settleMs | 5755.7 [5937.2] | 5662.0 [5818.0] | -93.7 [-226.5, +5.0] | ±172.7 | no detectable difference |
| drawInk / mainTaskMs | 2749.6 [2777.7] | 2590.0 [2614.7] | 0.942 [0.934, 0.951] | ±4.2% | faster |
| drawInk / mainJsMs | 2541.7 [2563.0] | 2374.8 [2400.0] | 0.934 [0.927, 0.944] | ±5.0% | faster |
| typeField / settleMs | 263.1 [275.9] | 225.7 [232.8] | -37.3 [-46.0, -24.9] | ±11.6 | faster |
| typeField / mainTaskMs | 273.1 [285.9] | 228.4 [234.3] | 0.836 [0.814, 0.862] | ±4.4% | faster |
| typeField / mainJsMs | 248.4 [261.5] | 203.0 [208.5] | 0.817 [0.793, 0.844] | ±4.1% | faster |
| undo / settleMs | 695.7 [699.5] | 402.9 [407.1] | -292.7 [-295.1, -288.9] | ±20.9 | faster (no A/A band) |
| undo / mainTaskMs | 704.1 [709.4] | 413.1 [417.4] | 0.587 [0.584, 0.591] | ±3.0% | faster (no A/A band) |
| undo / mainJsMs | 457.2 [459.8] | 292.7 [297.1] | 0.640 [0.637, 0.647] | ±3.0% | faster (no A/A band) |

## `main-now` vs `phase-d-end`

| Row | main-now median [max] | phase-d-end median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.5 [58.7] | 110.6 [169.3] | 2.108 [1.932, 2.236] | ±6.7% | slower |
| open(ebook) / openedMs | 204.5 [242.8] | 290.0 [363.7] | 1.418 [1.307, 1.471] | ±4.1% | slower |
| open(ebook) / firstPaintMs | 325.5 [379.6] | 494.1 [566.3] | 1.518 [1.423, 1.664] | ±6.5% | slower |
| open(ebook) / settledMs | 356.6 [394.5] | 534.3 [613.9] | 1.498 [1.412, 1.543] | ±3.0% | slower |
| open(ebook) / mainTaskMs | 216.0 [225.0] | 407.9 [418.8] | 1.889 [1.831, 1.921] | ±3.0% | slower |
| open(ebook) / mainJsMs | 181.8 [190.0] | 346.9 [386.4] | 1.908 [1.848, 1.949] | ±3.0% | slower |
| open(large) / readyMs | 52.6 [60.2] | 110.5 [120.4] | 2.099 [1.940, 2.173] | ±4.8% | slower |
| open(large) / openedMs | 214.4 [217.5] | 312.8 [319.5] | 1.459 [1.437, 1.479] | ±3.0% | slower |
| open(large) / firstPaintMs | 315.3 [343.8] | 501.5 [520.7] | 1.591 [1.506, 1.636] | ±7.9% | slower |
| open(large) / settledMs | 353.1 [364.0] | 555.5 [591.6] | 1.573 [1.508, 1.665] | ±4.0% | slower |
| open(large) / mainTaskMs | 214.3 [220.4] | 426.5 [448.1] | 1.990 [1.966, 2.031] | ±3.0% | slower |
| open(large) / mainJsMs | 179.0 [184.9] | 368.5 [382.4] | 2.059 [2.038, 2.092] | ±3.0% | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [1.7] | +0.0 [+0.0, +1.2] | ±0.0 | no detectable difference |
| scroll / wallMs | 2975.1 [3046.8] | 2702.4 [2949.0] | 0.908 [0.869, 0.962] | ±3.6% | faster |
| scroll / frameP50Ms | 11.0 [11.7] | 11.1 [11.4] | 1.010 [0.966, 1.035] | ±4.6% | no detectable difference |
| scroll / frameP95Ms | 25.0 [27.2] | 25.8 [27.2] | 1.033 [0.899, 1.192] | ±20.2% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 869.7 [889.2] | 1216.5 [1253.7] | 1.399 [1.373, 1.425] | ±3.0% | slower |
| scroll / mainJsMs | 735.2 [747.4] | 1069.1 [1106.4] | 1.454 [1.422, 1.490] | ±3.1% | slower |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [7.3] | +0.0 [+0.0, +1.3] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 3055.8 [3094.9] | 2612.2 [2871.8] | 0.855 [0.834, 0.897] | ±8.7% | faster |
| scrollMid / frameP50Ms | 10.9 [11.8] | 11.0 [11.5] | 1.008 [0.966, 1.020] | ±35.0% | no detectable difference |
| scrollMid / frameP95Ms | 26.0 [27.2] | 26.5 [27.2] | 1.020 [0.927, 1.048] | ±13.5% | no detectable difference |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 942.9 [994.8] | 1261.9 [1335.6] | 1.338 [1.286, 1.364] | ±5.1% | slower |
| scrollMid / mainJsMs | 820.9 [856.6] | 1120.0 [1174.7] | 1.364 [1.325, 1.387] | ±5.6% | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [2.0] | +0.0 [+0.0, +0.8] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 2983.7 [3026.4] | 2623.9 [2769.5] | 0.879 [0.859, 0.923] | ±3.2% | faster |
| coldScroll / frameP50Ms | 11.1 [11.4] | 11.3 [11.6] | 1.017 [0.977, 1.034] | ±5.0% | no detectable difference |
| coldScroll / frameP95Ms | 25.9 [27.2] | 26.8 [27.2] | 1.035 [0.991, 1.236] | ±21.0% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 906.9 [940.0] | 1242.1 [1257.6] | 1.370 [1.333, 1.398] | ±3.0% | slower |
| coldScroll / mainJsMs | 754.7 [776.5] | 1090.8 [1108.2] | 1.445 [1.395, 1.470] | ±3.0% | slower |
| jump / settleMs | 150.9 [161.4] | 157.6 [200.4] | +6.7 [-3.9, +26.1] | ±18.6 | no detectable difference |
| jump / mainTaskMs | 134.1 [138.2] | 178.3 [188.4] | 1.330 [1.286, 1.390] | ±6.5% | slower |
| jump / mainJsMs | 115.5 [125.0] | 166.1 [176.3] | 1.438 [1.365, 1.482] | ±7.2% | slower |
| zoom / settleMs | 354.9 [660.7] | 668.4 [688.9] | +313.4 [-100.7, +328.3] | ±15.4 | no detectable difference |
| zoom / zoom2SettleMs | 205.6 [229.3] | 229.5 [236.7] | +24.0 [+14.8, +28.0] | ±7.0 | slower |
| zoom / zoom4SettleMs | 147.0 [439.7] | 439.6 [457.4] | +292.6 [-123.3, +304.2] | ±13.5 | no detectable difference |
| zoom / backTo1SettleMs | 82.2 [103.7] | 92.0 [102.6] | +9.8 [-8.4, +22.9] | ±6.0 | no detectable difference |
| zoom / mainTaskMs | 353.3 [397.7] | 470.2 [485.0] | 1.331 [1.214, 1.364] | ±4.8% | slower |
| zoom / mainJsMs | 284.1 [316.7] | 416.1 [428.0] | 1.464 [1.356, 1.515] | ±3.1% | slower |
| zoomLarge / settleMs | 296.2 [326.3] | 316.3 [347.5] | +20.1 [+2.5, +38.5] | ±19.8 | slower |
| zoomLarge / zoom2SettleMs | 157.0 [168.4] | 164.8 [187.6] | +7.8 [-1.0, +21.1] | ±25.6 | no detectable difference |
| zoomLarge / zoom4SettleMs | 142.9 [157.9] | 150.5 [163.8] | +7.6 [-6.5, +24.5] | ±32.6 | no detectable difference |
| zoomLarge / backTo1SettleMs | 104.8 [119.9] | 94.3 [104.5] | -10.4 [-17.1, -4.3] | ±10.2 | faster |
| zoomLarge / mainTaskMs | 325.0 [338.1] | 387.7 [402.6] | 1.193 [1.147, 1.236] | ±3.4% | slower |
| zoomLarge / mainJsMs | 269.8 [281.5] | 349.5 [358.3] | 1.295 [1.240, 1.340] | ±3.8% | slower |
| select / settleMs | 211.1 [242.0] | 279.7 [339.4] | +68.6 [+53.0, +96.6] | ±48.6 | slower |
| select / mainTaskMs | 91.1 [95.2] | 131.1 [143.8] | 1.439 [1.377, 1.536] | ±7.6% | slower |
| select / mainJsMs | 73.7 [77.9] | 115.8 [128.0] | 1.571 [1.498, 1.690] | ±8.8% | slower |
| selectLarge / settleMs | 227.0 [255.3] | 282.5 [318.0] | +55.5 [+25.7, +78.8] | ±52.9 | slower |
| selectLarge / mainTaskMs | 108.2 [110.5] | 136.9 [149.8] | 1.265 [1.163, 1.338] | ±18.9% | slower |
| selectLarge / mainJsMs | 90.2 [93.2] | 123.5 [136.6] | 1.370 [1.248, 1.460] | ±19.3% | slower |
| search / searchMs | 40.6 [46.6] | 45.6 [51.0] | 1.123 [1.048, 1.451] |  | unreliable (A/A failed) |
| search / mainTaskMs | 19.1 [23.0] | 28.0 [33.2] | 1.468 [1.311, 1.784] | ±135.8% | no detectable difference |
| search / mainJsMs | 9.3 [14.0] | 22.1 [27.7] | 2.392 [1.947, 2.797] | ±30.7% | slower |
| searchLarge / searchMs | 2241.7 [2256.6] | 1928.8 [1954.7] | 0.860 [0.854, 0.866] | ±3.0% | faster |
| searchLarge / mainTaskMs | 412.1 [415.8] | 1150.2 [1236.9] | 2.791 [2.662, 2.961] | ±3.0% | slower |
| searchLarge / mainJsMs | 348.0 [351.3] | 897.9 [909.2] | 2.580 [2.551, 2.615] | ±3.0% | slower |
| annotate / settleMs | 234.3 [265.6] | 210.7 [237.6] | -23.6 [-47.3, +21.9] | ±78.0 | no detectable difference |
| annotate / mainTaskMs | 110.5 [117.7] | 120.7 [129.7] | 1.093 [1.068, 1.142] | ±9.3% | slower |
| annotate / mainJsMs | 88.9 [95.6] | 101.4 [110.5] | 1.141 [1.116, 1.198] | ±16.4% | no detectable difference |
| annotateLarge / settleMs | 215.0 [236.5] | 196.0 [263.5] | -19.1 [-29.8, +11.2] | ±32.9 | no detectable difference |
| annotateLarge / mainTaskMs | 113.8 [122.6] | 124.0 [133.7] | 1.090 [1.026, 1.122] | ±6.0% | slower |
| annotateLarge / mainJsMs | 92.4 [100.4] | 106.2 [115.1] | 1.150 [1.082, 1.186] | ±6.0% | slower |
| hover / settleMs | 1028.2 [1085.3] | 1214.6 [1272.8] | +186.3 [+164.0, +249.0] | ±94.3 | slower |
| hover / mainTaskMs | 453.1 [459.0] | 821.0 [846.8] | 1.812 [1.789, 1.837] | ±3.8% | slower |
| hover / mainJsMs | 422.7 [429.7] | 793.8 [819.2] | 1.878 [1.851, 1.903] | ±3.6% | slower |
| dragAnnot / settleMs | 386.5 [419.7] | 700.2 [737.4] | +313.7 [+279.5, +377.0] | ±51.7 | slower |
| dragAnnot / mainTaskMs | 227.5 [230.9] | 525.4 [531.9] | 2.310 [2.277, 2.345] | ±9.3% | slower |
| dragAnnot / mainJsMs | 201.9 [205.5] | 502.1 [508.7] | 2.487 [2.444, 2.533] | ±6.7% | slower |
| drawInk / settleMs | 3093.3 [3235.9] | 5662.0 [5818.0] | +2568.7 [+2426.2, +2656.9] | ±128.1 | slower |
| drawInk / mainTaskMs | 1343.7 [1362.3] | 2590.0 [2614.7] | 1.927 [1.908, 1.941] | ±4.2% | slower |
| drawInk / mainJsMs | 1136.0 [1144.2] | 2374.8 [2400.0] | 2.090 [2.073, 2.114] | ±5.0% | slower |
| typeField / settleMs | 151.0 [161.4] | 225.7 [232.8] | +74.8 [+69.2, +82.5] | ±11.6 | slower |
| typeField / mainTaskMs | 125.8 [128.4] | 228.4 [234.3] | 1.815 [1.770, 1.872] | ±4.4% | slower |
| typeField / mainJsMs | 93.3 [95.7] | 203.0 [208.5] | 2.175 [2.119, 2.289] | ±4.1% | slower |
| undo / settleMs | 197.6 [206.0] | 402.9 [407.1] | +205.3 [+201.3, +208.5] | ±5.9 | slower (no A/A band) |
| undo / mainTaskMs | 207.6 [217.6] | 413.1 [417.4] | 1.990 [1.949, 2.014] | ±3.0% | slower (no A/A band) |
| undo / mainJsMs | 134.2 [138.7] | 292.7 [297.1] | 2.181 [2.153, 2.207] | ±3.0% | slower (no A/A band) |

## `phase-d-end` vs `perf-fixes-end`

| Row | phase-d-end median [max] | perf-fixes-end median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 110.6 [169.3] | 108.1 [129.7] | 0.978 [0.925, 1.030] | ±6.7% | no detectable difference |
| open(ebook) / openedMs | 290.0 [363.7] | 290.2 [311.1] | 1.000 [0.965, 1.026] | ±4.1% | no detectable difference |
| open(ebook) / firstPaintMs | 494.1 [566.3] | 495.3 [518.6] | 1.002 [0.900, 1.051] | ±6.5% | no detectable difference |
| open(ebook) / settledMs | 534.3 [613.9] | 535.8 [564.0] | 1.003 [0.975, 1.029] | ±3.0% | no detectable difference |
| open(ebook) / mainTaskMs | 407.9 [418.8] | 408.1 [412.3] | 1.001 [0.975, 1.018] | ±3.0% | no detectable difference |
| open(ebook) / mainJsMs | 346.9 [386.4] | 344.5 [354.6] | 0.993 [0.972, 1.020] | ±3.0% | no detectable difference |
| open(large) / readyMs | 110.5 [120.4] | 108.3 [121.1] | 0.981 [0.949, 1.078] | ±4.8% | no detectable difference |
| open(large) / openedMs | 312.8 [319.5] | 310.1 [325.8] | 0.992 [0.970, 1.021] | ±3.0% | no detectable difference |
| open(large) / firstPaintMs | 501.5 [520.7] | 504.1 [529.8] | 1.005 [0.977, 1.039] | ±7.9% | no detectable difference |
| open(large) / settledMs | 555.5 [591.6] | 562.1 [576.5] | 1.012 [0.956, 1.059] | ±4.0% | no detectable difference |
| open(large) / mainTaskMs | 426.5 [448.1] | 422.2 [436.6] | 0.990 [0.971, 1.006] | ±3.0% | no detectable difference |
| open(large) / mainJsMs | 368.5 [382.4] | 363.5 [374.0] | 0.986 [0.973, 0.998] | ±3.0% | no detectable difference |
| scroll / settleMs | 0.0 [1.7] | 0.0 [0.0] | +0.0 [-1.2, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 2702.4 [2949.0] | 2752.7 [3284.1] | 1.019 [0.952, 1.076] | ±3.6% | no detectable difference |
| scroll / frameP50Ms | 11.1 [11.4] | 11.1 [18.6] | 0.999 [0.977, 1.040] | ±4.6% | no detectable difference |
| scroll / frameP95Ms | 25.8 [27.2] | 26.1 [27.2] | 1.011 [0.917, 1.121] | ±20.2% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 1216.5 [1253.7] | 1211.8 [1265.1] | 0.996 [0.975, 1.024] | ±3.0% | no detectable difference |
| scroll / mainJsMs | 1069.1 [1106.4] | 1065.8 [1130.7] | 0.997 [0.972, 1.033] | ±3.1% | no detectable difference |
| scrollMid / settleMs | 0.0 [7.3] | 0.0 [3.0] | +0.0 [-1.3, +0.7] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 2612.2 [2871.8] | 2655.8 [2940.7] | 1.017 [0.979, 1.103] | ±8.7% | no detectable difference |
| scrollMid / frameP50Ms | 11.0 [11.5] | 11.1 [11.5] | 1.007 [0.991, 1.032] | ±35.0% | no detectable difference |
| scrollMid / frameP95Ms | 26.5 [27.2] | 26.7 [27.2] | 1.008 [0.978, 1.105] | ±13.5% | no detectable difference |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 1261.9 [1335.6] | 1266.7 [1337.2] | 1.004 [0.953, 1.046] | ±5.1% | no detectable difference |
| scrollMid / mainJsMs | 1120.0 [1174.7] | 1143.4 [1178.4] | 1.021 [0.990, 1.040] | ±5.6% | no detectable difference |
| coldScroll / settleMs | 0.0 [2.0] | 0.0 [6.7] | +0.0 [-0.8, +0.7] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 2623.9 [2769.5] | 2671.3 [2845.9] | 1.018 [0.966, 1.058] | ±3.2% | no detectable difference |
| coldScroll / frameP50Ms | 11.3 [11.6] | 11.3 [12.5] | 1.001 [0.965, 1.045] | ±5.0% | no detectable difference |
| coldScroll / frameP95Ms | 26.8 [27.2] | 26.4 [27.2] | 0.985 [0.868, 1.028] | ±21.0% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 1242.1 [1257.6] | 1242.9 [1289.9] | 1.001 [0.986, 1.040] | ±3.0% | no detectable difference |
| coldScroll / mainJsMs | 1090.8 [1108.2] | 1090.8 [1131.2] | 1.000 [0.984, 1.045] | ±3.0% | no detectable difference |
| jump / settleMs | 157.6 [200.4] | 170.4 [194.0] | +12.8 [-6.7, +33.4] | ±18.6 | no detectable difference |
| jump / mainTaskMs | 178.3 [188.4] | 180.4 [193.1] | 1.011 [0.977, 1.049] | ±6.5% | no detectable difference |
| jump / mainJsMs | 166.1 [176.3] | 168.4 [174.7] | 1.014 [0.981, 1.051] | ±7.2% | no detectable difference |
| zoom / settleMs | 668.4 [688.9] | 666.3 [687.1] | -2.1 [-283.2, +267.7] | ±20.1 | no detectable difference |
| zoom / zoom2SettleMs | 229.5 [236.7] | 230.4 [248.2] | +0.8 [-4.2, +5.3] | ±7.0 | no detectable difference |
| zoom / zoom4SettleMs | 439.6 [457.4] | 433.3 [455.9] | -6.3 [-284.0, +271.0] | ±13.5 | no detectable difference |
| zoom / backTo1SettleMs | 92.0 [102.6] | 87.2 [105.9] | -4.8 [-15.6, +3.6] | ±6.0 | no detectable difference |
| zoom / mainTaskMs | 470.2 [485.0] | 467.8 [487.3] | 0.995 [0.935, 1.033] | ±4.8% | no detectable difference |
| zoom / mainJsMs | 416.1 [428.0] | 415.7 [435.0] | 0.999 [0.938, 1.038] | ±3.1% | no detectable difference |
| zoomLarge / settleMs | 316.3 [347.5] | 317.1 [338.2] | +0.8 [-10.1, +14.4] | ±19.8 | no detectable difference |
| zoomLarge / zoom2SettleMs | 164.8 [187.6] | 168.9 [189.6] | +4.1 [-5.8, +13.1] | ±25.6 | no detectable difference |
| zoomLarge / zoom4SettleMs | 150.5 [163.8] | 148.6 [159.7] | -1.9 [-14.6, +3.8] | ±32.6 | no detectable difference |
| zoomLarge / backTo1SettleMs | 94.3 [104.5] | 94.8 [115.1] | +0.4 [-5.9, +6.4] | ±10.2 | no detectable difference |
| zoomLarge / mainTaskMs | 387.7 [402.6] | 387.3 [407.0] | 0.999 [0.974, 1.034] | ±3.4% | no detectable difference |
| zoomLarge / mainJsMs | 349.5 [358.3] | 348.5 [361.6] | 0.997 [0.975, 1.028] | ±3.8% | no detectable difference |
| select / settleMs | 279.7 [339.4] | 263.0 [324.3] | -16.7 [-44.9, +18.0] | ±48.6 | no detectable difference |
| select / mainTaskMs | 131.1 [143.8] | 132.1 [146.0] | 1.008 [0.855, 1.053] | ±7.6% | no detectable difference |
| select / mainJsMs | 115.8 [128.0] | 117.0 [129.6] | 1.010 [0.853, 1.055] | ±8.8% | no detectable difference |
| selectLarge / settleMs | 282.5 [318.0] | 276.3 [299.7] | -6.2 [-40.4, +30.2] | ±52.9 | no detectable difference |
| selectLarge / mainTaskMs | 136.9 [149.8] | 137.8 [141.6] | 1.007 [0.904, 1.083] | ±18.9% | no detectable difference |
| selectLarge / mainJsMs | 123.5 [136.6] | 124.4 [128.0] | 1.007 [0.897, 1.094] | ±19.3% | no detectable difference |
| search / searchMs | 45.6 [51.0] | 45.6 [50.6] | 1.001 [0.926, 1.028] |  | unreliable (A/A failed) |
| search / mainTaskMs | 28.0 [33.2] | 28.1 [32.6] | 1.003 [0.890, 1.075] | ±135.8% | no detectable difference |
| search / mainJsMs | 22.1 [27.7] | 22.1 [27.4] | 1.000 [0.873, 1.035] | ±30.7% | no detectable difference |
| searchLarge / searchMs | 1928.8 [1954.7] | 1926.0 [1950.2] | 0.999 [0.987, 1.011] | ±3.0% | no detectable difference |
| searchLarge / mainTaskMs | 1150.2 [1236.9] | 1141.9 [1260.8] | 0.993 [0.872, 1.089] | ±3.0% | no detectable difference |
| searchLarge / mainJsMs | 897.9 [909.2] | 901.1 [912.2] | 1.004 [0.965, 1.016] | ±3.0% | no detectable difference |
| annotate / settleMs | 210.7 [237.6] | 214.8 [271.4] | +4.1 [-13.4, +36.5] | ±78.0 | no detectable difference |
| annotate / mainTaskMs | 120.7 [129.7] | 122.8 [136.6] | 1.017 [0.965, 1.095] | ±9.3% | no detectable difference |
| annotate / mainJsMs | 101.4 [110.5] | 103.3 [116.5] | 1.019 [0.967, 1.114] | ±16.4% | no detectable difference |
| annotateLarge / settleMs | 196.0 [263.5] | 204.5 [269.3] | +8.5 [-30.1, +30.5] | ±32.9 | no detectable difference |
| annotateLarge / mainTaskMs | 124.0 [133.7] | 123.1 [129.9] | 0.993 [0.941, 1.052] | ±6.0% | no detectable difference |
| annotateLarge / mainJsMs | 106.2 [115.1] | 105.6 [110.6] | 0.995 [0.934, 1.055] | ±6.0% | no detectable difference |
| hover / settleMs | 1214.6 [1272.8] | 1221.5 [1303.4] | +7.0 [-45.5, +30.8] | ±94.3 | no detectable difference |
| hover / mainTaskMs | 821.0 [846.8] | 817.1 [829.5] | 0.995 [0.984, 1.012] | ±3.8% | no detectable difference |
| hover / mainJsMs | 793.8 [819.2] | 788.4 [801.1] | 0.993 [0.982, 1.012] | ±3.6% | no detectable difference |
| dragAnnot / settleMs | 700.2 [737.4] | 726.2 [829.8] | +26.0 [-11.0, +63.9] | ±51.7 | no detectable difference |
| dragAnnot / mainTaskMs | 525.4 [531.9] | 528.6 [551.9] | 1.006 [0.998, 1.026] | ±9.3% | no detectable difference |
| dragAnnot / mainJsMs | 502.1 [508.7] | 505.9 [519.2] | 1.008 [0.999, 1.018] | ±6.7% | no detectable difference |
| drawInk / settleMs | 5662.0 [5818.0] | 5667.4 [5826.4] | +5.4 [-110.3, +157.4] | ±169.9 | no detectable difference |
| drawInk / mainTaskMs | 2590.0 [2614.7] | 2582.9 [2615.9] | 0.997 [0.989, 1.009] | ±4.2% | no detectable difference |
| drawInk / mainJsMs | 2374.8 [2400.0] | 2370.9 [2398.0] | 0.998 [0.989, 1.010] | ±5.0% | no detectable difference |
| typeField / settleMs | 225.7 [232.8] | 225.3 [236.9] | -0.4 [-6.8, +5.2] | ±11.6 | no detectable difference |
| typeField / mainTaskMs | 228.4 [234.3] | 225.9 [233.8] | 0.989 [0.966, 1.014] | ±4.4% | no detectable difference |
| typeField / mainJsMs | 203.0 [208.5] | 200.8 [208.3] | 0.989 [0.964, 1.019] | ±4.1% | no detectable difference |
| undo / settleMs | 402.9 [407.1] | 403.2 [405.0] | +0.3 [-3.4, +1.8] | ±12.1 | no detectable difference (no A/A band) |
| undo / mainTaskMs | 413.1 [417.4] | 412.9 [415.5] | 1.000 [0.991, 1.004] | ±3.0% | no detectable difference (no A/A band) |
| undo / mainJsMs | 292.7 [297.1] | 293.6 [296.0] | 1.003 [0.992, 1.009] | ±3.0% | no detectable difference (no A/A band) |

## `main-now` vs `perf-fixes-end`

| Row | main-now median [max] | perf-fixes-end median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.5 [58.7] | 108.1 [129.7] | 2.061 [1.897, 2.164] | ±6.7% | slower |
| open(ebook) / openedMs | 204.5 [242.8] | 290.2 [311.1] | 1.419 [1.303, 1.457] | ±4.1% | slower |
| open(ebook) / firstPaintMs | 325.5 [379.6] | 495.3 [518.6] | 1.521 [1.424, 1.558] | ±6.5% | slower |
| open(ebook) / settledMs | 356.6 [394.5] | 535.8 [564.0] | 1.502 [1.416, 1.538] | ±3.0% | slower |
| open(ebook) / mainTaskMs | 216.0 [225.0] | 408.1 [412.3] | 1.890 [1.830, 1.907] | ±3.0% | slower |
| open(ebook) / mainJsMs | 181.8 [190.0] | 344.5 [354.6] | 1.895 [1.844, 1.934] | ±3.0% | slower |
| open(large) / readyMs | 52.6 [60.2] | 108.3 [121.1] | 2.058 [1.903, 2.264] | ±4.8% | slower |
| open(large) / openedMs | 214.4 [217.5] | 310.1 [325.8] | 1.447 [1.415, 1.486] | ±3.0% | slower |
| open(large) / firstPaintMs | 315.3 [343.8] | 504.1 [529.8] | 1.599 [1.524, 1.643] | ±7.9% | slower |
| open(large) / settledMs | 353.1 [364.0] | 562.1 [576.5] | 1.592 [1.521, 1.671] | ±4.0% | slower |
| open(large) / mainTaskMs | 214.3 [220.4] | 422.2 [436.6] | 1.970 [1.957, 1.999] | ±3.0% | slower |
| open(large) / mainJsMs | 179.0 [184.9] | 363.5 [374.0] | 2.031 [2.005, 2.063] | ±3.0% | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 2975.1 [3046.8] | 2752.7 [3284.1] | 0.925 [0.886, 0.958] | ±3.6% | faster |
| scroll / frameP50Ms | 11.0 [11.7] | 11.1 [18.6] | 1.009 [0.970, 1.049] | ±4.6% | no detectable difference |
| scroll / frameP95Ms | 25.0 [27.2] | 26.1 [27.2] | 1.044 [0.932, 1.202] | ±20.2% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 869.7 [889.2] | 1211.8 [1265.1] | 1.393 [1.373, 1.424] | ±3.0% | slower |
| scroll / mainJsMs | 735.2 [747.4] | 1065.8 [1130.7] | 1.450 [1.428, 1.487] | ±3.1% | slower |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [3.0] | +0.0 [+0.0, +0.7] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 3055.8 [3094.9] | 2655.8 [2940.7] | 0.869 [0.860, 0.941] | ±8.7% | faster |
| scrollMid / frameP50Ms | 10.9 [11.8] | 11.1 [11.5] | 1.015 [0.973, 1.037] | ±35.0% | no detectable difference |
| scrollMid / frameP95Ms | 26.0 [27.2] | 26.7 [27.2] | 1.028 [0.973, 1.056] | ±13.5% | no detectable difference |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 942.9 [994.8] | 1266.7 [1337.2] | 1.343 [1.268, 1.391] | ±5.1% | slower |
| scrollMid / mainJsMs | 820.9 [856.6] | 1143.4 [1178.4] | 1.393 [1.340, 1.413] | ±5.6% | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [6.7] | +0.0 [+0.0, +0.7] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 2983.7 [3026.4] | 2671.3 [2845.9] | 0.895 [0.874, 0.925] | ±3.2% | faster |
| coldScroll / frameP50Ms | 11.1 [11.4] | 11.3 [12.5] | 1.018 [0.975, 1.050] | ±5.0% | no detectable difference |
| coldScroll / frameP95Ms | 25.9 [27.2] | 26.4 [27.2] | 1.020 [0.903, 1.212] | ±21.0% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 906.9 [940.0] | 1242.9 [1289.9] | 1.371 [1.346, 1.411] | ±3.0% | slower |
| coldScroll / mainJsMs | 754.7 [776.5] | 1090.8 [1131.2] | 1.445 [1.415, 1.486] | ±3.0% | slower |
| jump / settleMs | 150.9 [161.4] | 170.4 [194.0] | +19.5 [+11.9, +38.6] | ±18.6 | slower |
| jump / mainTaskMs | 134.1 [138.2] | 180.4 [193.1] | 1.346 [1.295, 1.410] | ±6.5% | slower |
| jump / mainJsMs | 115.5 [125.0] | 168.4 [174.7] | 1.458 [1.387, 1.510] | ±7.2% | slower |
| zoom / settleMs | 354.9 [660.7] | 666.3 [687.1] | +311.4 [-235.2, +331.8] | ±15.4 | no detectable difference |
| zoom / zoom2SettleMs | 205.6 [229.3] | 230.4 [248.2] | +24.8 [+15.3, +28.8] | ±7.0 | slower |
| zoom / zoom4SettleMs | 147.0 [439.7] | 433.3 [455.9] | +286.3 [-261.2, +302.1] | ±13.5 | no detectable difference |
| zoom / backTo1SettleMs | 82.2 [103.7] | 87.2 [105.9] | +5.0 [-12.7, +14.1] | ±6.0 | no detectable difference |
| zoom / mainTaskMs | 353.3 [397.7] | 467.8 [487.3] | 1.324 [1.176, 1.358] | ±4.8% | slower |
| zoom / mainJsMs | 284.1 [316.7] | 415.7 [435.0] | 1.463 [1.326, 1.511] | ±3.1% | slower |
| zoomLarge / settleMs | 296.2 [326.3] | 317.1 [338.2] | +20.9 [+2.3, +38.6] | ±19.8 | slower |
| zoomLarge / zoom2SettleMs | 157.0 [168.4] | 168.9 [189.6] | +11.9 [+1.5, +24.8] | ±25.6 | no detectable difference |
| zoomLarge / zoom4SettleMs | 142.9 [157.9] | 148.6 [159.7] | +5.7 [-10.2, +18.3] | ±32.6 | no detectable difference |
| zoomLarge / backTo1SettleMs | 104.8 [119.9] | 94.8 [115.1] | -10.0 [-16.8, -4.3] | ±10.2 | no detectable difference |
| zoomLarge / mainTaskMs | 325.0 [338.1] | 387.3 [407.0] | 1.192 [1.150, 1.240] | ±3.4% | slower |
| zoomLarge / mainJsMs | 269.8 [281.5] | 348.5 [361.6] | 1.292 [1.243, 1.335] | ±3.8% | slower |
| select / settleMs | 211.1 [242.0] | 263.0 [324.3] | +51.9 [+25.6, +90.3] | ±48.6 | slower |
| select / mainTaskMs | 91.1 [95.2] | 132.1 [146.0] | 1.450 [1.235, 1.524] | ±7.6% | slower |
| select / mainJsMs | 73.7 [77.9] | 117.0 [129.6] | 1.587 [1.343, 1.666] | ±8.8% | slower |
| selectLarge / settleMs | 227.0 [255.3] | 276.3 [299.7] | +49.3 [+15.4, +77.8] | ±52.9 | no detectable difference |
| selectLarge / mainTaskMs | 108.2 [110.5] | 137.8 [141.6] | 1.274 [1.143, 1.315] | ±18.9% | slower |
| selectLarge / mainJsMs | 90.2 [93.2] | 124.4 [128.0] | 1.379 [1.230, 1.425] | ±19.3% | slower |
| search / searchMs | 40.6 [46.6] | 45.6 [50.6] | 1.124 [1.045, 1.452] |  | unreliable (A/A failed) |
| search / mainTaskMs | 19.1 [23.0] | 28.1 [32.6] | 1.472 [1.308, 1.736] | ±135.8% | no detectable difference |
| search / mainJsMs | 9.3 [14.0] | 22.1 [27.4] | 2.392 [1.942, 2.657] | ±30.7% | slower |
| searchLarge / searchMs | 2241.7 [2256.6] | 1926.0 [1950.2] | 0.859 [0.850, 0.868] | ±3.0% | faster |
| searchLarge / mainTaskMs | 412.1 [415.8] | 1141.9 [1260.8] | 2.771 [2.503, 2.951] | ±3.0% | slower |
| searchLarge / mainJsMs | 348.0 [351.3] | 901.1 [912.2] | 2.590 [2.498, 2.613] | ±3.0% | slower |
| annotate / settleMs | 234.3 [265.6] | 214.8 [271.4] | -19.5 [-45.1, +31.8] | ±78.0 | no detectable difference |
| annotate / mainTaskMs | 110.5 [117.7] | 122.8 [136.6] | 1.111 [1.065, 1.200] | ±9.3% | slower |
| annotate / mainJsMs | 88.9 [95.6] | 103.3 [116.5] | 1.162 [1.118, 1.281] | ±16.4% | no detectable difference |
| annotateLarge / settleMs | 215.0 [236.5] | 204.5 [269.3] | -10.5 [-33.1, +14.2] | ±32.9 | no detectable difference |
| annotateLarge / mainTaskMs | 113.8 [122.6] | 123.1 [129.9] | 1.082 [1.018, 1.119] | ±6.0% | slower |
| annotateLarge / mainJsMs | 92.4 [100.4] | 105.6 [110.6] | 1.144 [1.067, 1.181] | ±6.0% | slower |
| hover / settleMs | 1028.2 [1085.3] | 1221.5 [1303.4] | +193.3 [+163.9, +239.6] | ±94.3 | slower |
| hover / mainTaskMs | 453.1 [459.0] | 817.1 [829.5] | 1.803 [1.788, 1.831] | ±3.8% | slower |
| hover / mainJsMs | 422.7 [429.7] | 788.4 [801.1] | 1.865 [1.847, 1.896] | ±3.6% | slower |
| dragAnnot / settleMs | 386.5 [419.7] | 726.2 [829.8] | +339.7 [+316.4, +395.3] | ±51.7 | slower |
| dragAnnot / mainTaskMs | 227.5 [230.9] | 528.6 [551.9] | 2.324 [2.293, 2.376] | ±9.3% | slower |
| dragAnnot / mainJsMs | 201.9 [205.5] | 505.9 [519.2] | 2.506 [2.463, 2.553] | ±6.7% | slower |
| drawInk / settleMs | 3093.3 [3235.9] | 5667.4 [5826.4] | +2574.1 [+2438.7, +2678.5] | ±128.1 | slower |
| drawInk / mainTaskMs | 1343.7 [1362.3] | 2582.9 [2615.9] | 1.922 [1.899, 1.945] | ±4.2% | slower |
| drawInk / mainJsMs | 1136.0 [1144.2] | 2370.9 [2398.0] | 2.087 [2.067, 2.115] | ±5.0% | slower |
| typeField / settleMs | 151.0 [161.4] | 225.3 [236.9] | +74.3 [+67.4, +82.4] | ±11.6 | slower |
| typeField / mainTaskMs | 125.8 [128.4] | 225.9 [233.8] | 1.795 [1.767, 1.849] | ±4.4% | slower |
| typeField / mainJsMs | 93.3 [95.7] | 200.8 [208.3] | 2.152 [2.112, 2.267] | ±4.1% | slower |
| undo / settleMs | 197.6 [206.0] | 403.2 [405.0] | +205.6 [+200.8, +206.7] | ±5.9 | slower (no A/A band) |
| undo / mainTaskMs | 207.6 [217.6] | 412.9 [415.5] | 1.989 [1.947, 2.012] | ±3.0% | slower (no A/A band) |
| undo / mainJsMs | 134.2 [138.7] | 293.6 [296.0] | 2.188 [2.151, 2.206] | ±3.0% | slower (no A/A band) |

## Memory

| Row | main median [max] | main-aa median [max] | main-now median [max] | phase-c-base median [max] | phase-c-end median [max] | phase-d-end median [max] | perf-fixes-end median [max] |
| --- | --- | --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 35.42 MiB [35.42 MiB] | 34.91 MiB [35.41 MiB] | 50.75 MiB [51.24 MiB] | 56.64 MiB [56.91 MiB] | 54.27 MiB [54.78 MiB] | 54.77 MiB [54.79 MiB] | 54.26 MiB [54.76 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 27.72 MiB [27.73 MiB] | 27.70 MiB [27.70 MiB] | 27.70 MiB [27.70 MiB] | 27.70 MiB [27.71 MiB] | 27.70 MiB [27.70 MiB] |
| open(large) / heapAfterOpenBytes | 48.20 MiB [48.24 MiB] | 48.21 MiB [48.24 MiB] | 52.70 MiB [52.74 MiB] | 71.24 MiB [71.25 MiB] | 62.94 MiB [62.96 MiB] | 63.00 MiB [63.02 MiB] | 63.00 MiB [63.03 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.28 MiB] | 22.26 MiB [22.29 MiB] | 27.63 MiB [27.64 MiB] | 27.60 MiB [27.61 MiB] | 27.61 MiB [27.62 MiB] | 27.61 MiB [27.61 MiB] | 27.60 MiB [27.62 MiB] |
| soak / heapCycle5Bytes | 38.50 MiB [38.52 MiB] | 38.51 MiB [38.51 MiB] | 57.94 MiB [57.96 MiB] | 64.07 MiB [69.29 MiB] | 61.42 MiB [61.91 MiB] | 61.80 MiB [66.44 MiB] | 61.71 MiB [66.41 MiB] |
| soak / heapCycle30Bytes | 39.32 MiB [39.33 MiB] | 39.32 MiB [39.33 MiB] | 59.37 MiB [59.41 MiB] | 64.78 MiB [69.82 MiB] | 61.93 MiB [62.42 MiB] | 62.42 MiB [66.99 MiB] | 62.43 MiB [66.94 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.06 MiB [0.06 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] |

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
| refs | 1026 | 1526 |
| tokens | 31 | 31 |
| wasmBytes | 5.25 MiB | 5.25 MiB |
| wireRefs | 916 | 916 |

