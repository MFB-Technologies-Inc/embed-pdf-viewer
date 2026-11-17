# Viewer perf results, 2026-11-17

The tip (`plugins-next`, `2aaa8f841` plus the form-fill fix `0de4effd9`) is slower than upstream
`main` at `ed064e0c8` (`main-now`) on 63 of 87 rows and faster on 1. Its median latency row is
1.065x and its median main-thread row 1.817x (`node scripts/summary.mjs`). The new annotation and
form flows show the widest gaps: main-thread JavaScript is 2.372x on `hover`, 2.998x on
`dragAnnot`, 2.984x on `drawInk`, 2.271x on `typeField` and 4.009x on `undo`, and each settles later
(`drawInk` by 3,041.4 ms, `undo` by 644.3 ms). `scrollMid` costs 1.918x the JavaScript of
`main-now`, against 1.741x for `scroll` from page 0. `undo` has no `main` or A/A row, because
`2516e2786` has no history plugin, so its rows are judged against the 3% floor. `dragAnnot` drags a
stamp, because neither API generation moves a highlight. `main` against `main-now` reads 1.015x
latency and 1.325x main thread. The unchanged `main` build moved 0.999x and 0.994x since the
2026-11-16 run, so no row is within its drift. The 23 no-difference rows of the tip against
`main-now`, with thresholds, are listed in FINDINGS.md ("Annotation and form flows").

# Viewer perf comparison: 2026-11-17T02-25-30-951Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | cb3167fcd560fbdce91ab100c59581318b46f3f3 |
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
| plugins-next | research/perf-fixes | 0de4effd9 | built (fork 17d8073d4, wasm32-eh) | 2ecd0bd98574 | 3db0d63cf08a |
| main-now | main | ed064e0c8 | fetched | 7195838021c8 |  |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-next`: pass
- `main-now`: pass

Not run, because the arm's viewer lacks what the flow needs:

- `main`: `undo`
- `main-aa`: `undo`

## Sizes

| Metric | main | main-aa | plugins-next | main-now |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 14.26 MiB | 7.52 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 3.79 MiB | 2.35 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 10.35 MiB | 0.00 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 2.44 MiB | 0.00 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 8.50 MiB | 9.50 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 2.90 MiB | 3.12 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

84 of 84 rows pass.

| Row | main median [max] | main-aa median [max] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 61.9 [64.2] | 60.2 [64.4] | 0.973 [0.939, 1.011] | pass |
| open(ebook) / openedMs | 135.9 [140.9] | 134.6 [137.8] | 0.990 [0.962, 1.016] | pass |
| open(ebook) / firstPaintMs | 261.2 [269.2] | 259.8 [264.8] | 0.994 [0.965, 1.023] | pass |
| open(ebook) / settledMs | 400.2 [405.7] | 398.7 [402.0] | 0.996 [0.979, 1.002] | pass |
| open(ebook) / mainTaskMs | 171.5 [174.6] | 169.8 [172.3] | 0.990 [0.984, 1.005] | pass |
| open(ebook) / mainJsMs | 141.4 [144.2] | 140.0 [142.0] | 0.990 [0.980, 1.006] | pass |
| open(large) / readyMs | 63.5 [64.8] | 63.6 [69.0] | 1.001 [0.958, 1.050] | pass |
| open(large) / openedMs | 157.3 [158.6] | 157.6 [163.5] | 1.002 [0.991, 1.022] | pass |
| open(large) / firstPaintMs | 265.6 [272.4] | 268.1 [272.2] | 1.009 [0.984, 1.045] | pass |
| open(large) / settledMs | 286.0 [292.4] | 288.4 [291.9] | 1.009 [0.986, 1.037] | pass |
| open(large) / mainTaskMs | 320.2 [322.6] | 320.8 [327.1] | 1.002 [0.996, 1.010] | pass |
| open(large) / mainJsMs | 291.2 [294.6] | 292.0 [297.3] | 1.003 [0.996, 1.007] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2998.1 [3031.5] | 3014.5 [3031.6] | 1.005 [1.000, 1.008] | pass |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 16.9 [17.5] | 17.0 [17.7] | 1.010 [0.996, 1.026] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 752.9 [776.0] | 755.1 [764.9] | 1.003 [0.988, 1.018] | pass |
| scroll / mainJsMs | 614.3 [632.4] | 616.9 [624.8] | 1.004 [0.986, 1.019] | pass |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scrollMid / wallMs | 3006.4 [3015.5] | 3014.6 [3031.1] | 1.003 [0.995, 1.006] | pass |
| scrollMid / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | pass |
| scrollMid / frameP95Ms | 17.3 [17.7] | 16.9 [17.5] | 0.977 [0.956, 1.002] | pass |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scrollMid / mainTaskMs | 767.7 [781.8] | 767.8 [783.5] | 1.000 [0.983, 1.012] | pass |
| scrollMid / mainJsMs | 625.4 [643.3] | 626.8 [636.6] | 1.002 [0.985, 1.014] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 3007.7 [3031.9] | 3016.4 [3033.5] | 1.003 [0.997, 1.006] | pass |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | pass |
| coldScroll / frameP95Ms | 17.0 [17.7] | 17.2 [17.7] | 1.013 [0.978, 1.035] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 762.3 [797.7] | 767.9 [797.2] | 1.007 [0.981, 1.018] | pass |
| coldScroll / mainJsMs | 594.0 [625.8] | 600.5 [622.5] | 1.011 [0.985, 1.024] | pass |
| jump / settleMs | 130.9 [141.5] | 136.8 [145.2] | +5.8 [-6.7, +10.5] | pass |
| jump / mainTaskMs | 99.9 [105.8] | 100.8 [109.5] | 1.009 [0.982, 1.064] | pass |
| jump / mainJsMs | 83.4 [89.7] | 86.2 [92.8] | 1.033 [0.987, 1.052] | pass |
| zoom / settleMs | 459.9 [481.6] | 446.3 [470.7] | -13.6 [-25.5, +7.0] | pass |
| zoom / zoom2SettleMs | 173.1 [177.8] | 174.3 [178.4] | +1.2 [-1.0, +2.6] | pass |
| zoom / zoom4SettleMs | 287.3 [308.7] | 272.3 [296.7] | -15.0 [-26.1, +8.6] | pass |
| zoom / backTo1SettleMs | 130.6 [143.2] | 124.2 [132.5] | -6.4 [-10.9, +5.3] | pass |
| zoom / mainTaskMs | 376.2 [394.0] | 375.5 [386.7] | 0.998 [0.977, 1.005] | pass |
| zoom / mainJsMs | 313.8 [326.5] | 312.7 [315.8] | 0.997 [0.969, 1.007] | pass |
| zoomLarge / settleMs | 324.2 [338.5] | 317.9 [343.4] | -6.4 [-21.7, +1.2] | pass |
| zoomLarge / zoom2SettleMs | 162.6 [166.4] | 157.3 [182.2] | -5.2 [-19.6, +2.7] | pass |
| zoomLarge / zoom4SettleMs | 161.8 [182.2] | 161.1 [169.4] | -0.7 [-7.0, +5.3] | pass |
| zoomLarge / backTo1SettleMs | 143.3 [153.9] | 141.6 [151.5] | -1.7 [-6.6, +10.2] | pass |
| zoomLarge / mainTaskMs | 346.4 [355.2] | 342.0 [349.4] | 0.987 [0.970, 1.025] | pass |
| zoomLarge / mainJsMs | 303.9 [311.3] | 300.3 [308.0] | 0.988 [0.970, 1.024] | pass |
| select / settleMs | 282.0 [293.7] | 288.6 [292.8] | +6.6 [-5.2, +12.7] | pass |
| select / mainTaskMs | 131.6 [134.3] | 127.4 [133.1] | 0.968 [0.940, 1.012] | pass |
| select / mainJsMs | 100.3 [110.3] | 101.6 [107.1] | 1.013 [0.971, 1.079] | pass |
| selectLarge / settleMs | 277.1 [296.7] | 282.5 [288.1] | +5.4 [-4.1, +9.7] | pass |
| selectLarge / mainTaskMs | 124.0 [137.9] | 125.1 [137.1] | 1.009 [0.925, 1.064] | pass |
| selectLarge / mainJsMs | 102.4 [113.6] | 103.5 [113.3] | 1.011 [0.929, 1.062] | pass |
| search / searchMs | 81.1 [83.2] | 82.1 [84.1] | 1.011 [0.999, 1.023] | pass |
| search / mainTaskMs | 16.2 [16.8] | 16.1 [17.2] | 0.993 [0.965, 1.041] | pass |
| search / mainJsMs | 6.8 [8.2] | 7.2 [10.5] | 1.059 [0.900, 1.099] | pass |
| searchLarge / searchMs | 1890.4 [1921.9] | 1888.9 [1971.1] | 0.999 [0.993, 1.021] | pass |
| searchLarge / mainTaskMs | 371.1 [373.9] | 370.2 [376.8] | 0.998 [0.992, 1.004] | pass |
| searchLarge / mainJsMs | 321.0 [322.7] | 319.7 [325.1] | 0.996 [0.991, 1.002] | pass |
| annotate / settleMs | 293.1 [294.7] | 293.8 [302.2] | +0.7 [-8.6, +6.0] | pass |
| annotate / mainTaskMs | 137.8 [143.0] | 138.1 [149.5] | 1.002 [0.963, 1.049] | pass |
| annotate / mainJsMs | 103.4 [109.8] | 105.5 [109.4] | 1.021 [0.986, 1.079] | pass |
| annotateLarge / settleMs | 289.3 [297.7] | 290.9 [302.1] | +1.5 [-6.7, +8.0] | pass |
| annotateLarge / mainTaskMs | 143.2 [148.1] | 143.2 [147.3] | 1.000 [0.971, 1.021] | pass |
| annotateLarge / mainJsMs | 109.8 [114.0] | 109.9 [114.3] | 1.000 [0.970, 1.035] | pass |
| hover / settleMs | 1322.2 [1338.2] | 1322.8 [1333.4] | +0.6 [-4.0, +4.0] | pass |
| hover / mainTaskMs | 427.2 [440.3] | 429.3 [448.1] | 1.005 [0.985, 1.028] | pass |
| hover / mainJsMs | 387.4 [397.9] | 389.7 [405.0] | 1.006 [0.987, 1.024] | pass |
| dragAnnot / settleMs | 410.6 [419.5] | 403.0 [412.4] | -7.6 [-11.6, +1.4] | pass |
| dragAnnot / mainTaskMs | 171.8 [179.4] | 171.0 [187.4] | 0.995 [0.967, 1.036] | pass |
| dragAnnot / mainJsMs | 142.4 [148.0] | 140.1 [146.2] | 0.984 [0.962, 1.018] | pass |
| drawInk / settleMs | 3888.0 [3894.7] | 3888.6 [3907.9] | +0.6 [-6.4, +7.7] | pass |
| drawInk / mainTaskMs | 1071.2 [1117.7] | 1083.4 [1137.9] | 1.011 [0.978, 1.025] | pass |
| drawInk / mainJsMs | 793.5 [835.2] | 802.7 [841.8] | 1.012 [0.976, 1.027] | pass |
| typeField / settleMs | 175.6 [184.9] | 169.2 [181.6] | -6.4 [-13.4, +1.4] | pass |
| typeField / mainTaskMs | 175.9 [182.5] | 172.8 [180.9] | 0.982 [0.951, 1.005] | pass |
| typeField / mainJsMs | 144.8 [152.1] | 144.2 [147.0] | 0.996 [0.965, 1.030] | pass |

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

Not run: `plugins-base`.

## `main` vs `plugins-next`

| Row | main median [max] | plugins-next median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 61.9 [64.2] | 108.4 [110.1] | 1.752 [1.676, 1.820] | ±6.1% | slower |
| open(ebook) / openedMs | 135.9 [140.9] | 287.4 [303.5] | 2.114 [2.054, 2.152] | ±3.8% | slower |
| open(ebook) / firstPaintMs | 261.2 [269.2] | 495.3 [539.9] | 1.896 [1.827, 1.967] | ±3.5% | slower |
| open(ebook) / settledMs | 400.2 [405.7] | 532.0 [546.8] | 1.329 [1.300, 1.350] | ±3.0% | slower |
| open(ebook) / mainTaskMs | 171.5 [174.6] | 414.7 [429.3] | 2.418 [2.389, 2.469] | ±3.0% | slower |
| open(ebook) / mainJsMs | 141.4 [144.2] | 358.5 [370.8] | 2.534 [2.491, 2.591] | ±3.0% | slower |
| open(large) / readyMs | 63.5 [64.8] | 111.1 [116.2] | 1.748 [1.686, 1.831] | ±5.0% | slower |
| open(large) / openedMs | 157.3 [158.6] | 309.4 [324.2] | 1.967 [1.950, 1.995] | ±3.0% | slower |
| open(large) / firstPaintMs | 265.6 [272.4] | 504.2 [550.2] | 1.898 [1.863, 1.978] | ±4.5% | slower |
| open(large) / settledMs | 286.0 [292.4] | 541.7 [561.1] | 1.894 [1.850, 1.953] | ±3.7% | slower |
| open(large) / mainTaskMs | 320.2 [322.6] | 440.6 [445.5] | 1.376 [1.364, 1.384] | ±3.0% | slower |
| open(large) / mainJsMs | 291.2 [294.6] | 383.2 [387.0] | 1.316 [1.298, 1.325] | ±3.0% | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.8] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 2998.1 [3031.5] | 3099.1 [3166.4] | 1.034 [1.023, 1.048] | ±3.0% | slower |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scroll / frameP95Ms | 16.9 [17.5] | 17.5 [18.0] | 1.035 [1.012, 1.059] | ±3.0% | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 752.9 [776.0] | 1618.3 [1658.3] | 2.149 [2.117, 2.197] | ±3.0% | slower |
| scroll / mainJsMs | 614.3 [632.4] | 1504.4 [1540.7] | 2.449 [2.404, 2.518] | ±3.0% | slower |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 3006.4 [3015.5] | 3352.6 [3431.5] | 1.115 [1.104, 1.123] | ±3.0% | slower |
| scrollMid / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scrollMid / frameP95Ms | 17.3 [17.7] | 17.8 [18.2] | 1.032 [1.013, 1.060] | ±4.4% | no detectable difference |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 767.7 [781.8] | 1899.3 [1925.6] | 2.474 [2.433, 2.500] | ±3.0% | slower |
| scrollMid / mainJsMs | 625.4 [643.3] | 1800.3 [1830.2] | 2.879 [2.838, 2.915] | ±3.0% | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 3007.7 [3031.9] | 3134.6 [3225.1] | 1.042 [1.036, 1.050] | ±3.0% | slower |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| coldScroll / frameP95Ms | 17.0 [17.7] | 17.7 [18.2] | 1.042 [1.004, 1.067] | ±3.5% | slower |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 762.3 [797.7] | 1664.6 [1715.5] | 2.183 [2.134, 2.211] | ±3.0% | slower |
| coldScroll / mainJsMs | 594.0 [625.8] | 1521.0 [1562.4] | 2.561 [2.498, 2.603] | ±3.0% | slower |
| jump / settleMs | 130.9 [141.5] | 173.2 [192.1] | +42.3 [+32.0, +47.7] | ±10.5 | slower |
| jump / mainTaskMs | 99.9 [105.8] | 257.6 [271.2] | 2.578 [2.459, 2.650] | ±6.4% | slower |
| jump / mainJsMs | 83.4 [89.7] | 238.9 [248.1] | 2.863 [2.719, 2.936] | ±5.2% | slower |
| zoom / settleMs | 459.9 [481.6] | 654.0 [673.4] | +194.1 [+181.8, +212.9] | ±25.5 | slower |
| zoom / zoom2SettleMs | 173.1 [177.8] | 236.7 [245.9] | +63.7 [+61.4, +68.8] | ±5.2 | slower |
| zoom / zoom4SettleMs | 287.3 [308.7] | 419.0 [427.5] | +131.7 [+119.2, +153.8] | ±26.1 | slower |
| zoom / backTo1SettleMs | 130.6 [143.2] | 121.1 [196.6] | -9.5 [-14.5, +21.7] | ±10.9 | no detectable difference |
| zoom / mainTaskMs | 376.2 [394.0] | 630.3 [697.3] | 1.675 [1.623, 1.750] | ±3.0% | slower |
| zoom / mainJsMs | 313.8 [326.5] | 570.1 [642.6] | 1.817 [1.747, 1.883] | ±3.1% | slower |
| zoomLarge / settleMs | 324.2 [338.5] | 367.9 [391.8] | +43.6 [+33.5, +52.4] | ±21.7 | slower |
| zoomLarge / zoom2SettleMs | 162.6 [166.4] | 199.2 [211.7] | +36.6 [+27.5, +42.0] | ±19.6 | slower |
| zoomLarge / zoom4SettleMs | 161.8 [182.2] | 174.7 [180.1] | +12.9 [+3.2, +20.3] | ±7.0 | slower |
| zoomLarge / backTo1SettleMs | 143.3 [153.9] | 198.1 [203.1] | +54.8 [+51.0, +66.6] | ±10.2 | slower |
| zoomLarge / mainTaskMs | 346.4 [355.2] | 603.3 [632.4] | 1.742 [1.715, 1.809] | ±3.0% | slower |
| zoomLarge / mainJsMs | 303.9 [311.3] | 563.8 [585.2] | 1.855 [1.830, 1.926] | ±3.0% | slower |
| select / settleMs | 282.0 [293.7] | 278.5 [296.0] | -3.5 [-9.9, +8.0] | ±12.7 | no detectable difference |
| select / mainTaskMs | 131.6 [134.3] | 188.4 [191.6] | 1.432 [1.402, 1.497] | ±6.0% | slower |
| select / mainJsMs | 100.3 [110.3] | 167.0 [172.1] | 1.666 [1.604, 1.760] | ±7.9% | slower |
| selectLarge / settleMs | 277.1 [296.7] | 312.9 [332.4] | +35.7 [+28.4, +44.9] | ±9.7 | slower |
| selectLarge / mainTaskMs | 124.0 [137.9] | 210.5 [218.1] | 1.698 [1.548, 1.736] | ±7.5% | slower |
| selectLarge / mainJsMs | 102.4 [113.6] | 179.6 [195.5] | 1.754 [1.615, 1.820] | ±7.1% | slower |
| search / searchMs | 81.1 [83.2] | 45.2 [46.3] | 0.557 [0.550, 0.565] | ±3.0% | faster |
| search / mainTaskMs | 16.2 [16.8] | 30.5 [31.2] | 1.885 [1.856, 1.940] | ±4.1% | slower |
| search / mainJsMs | 6.8 [8.2] | 25.9 [27.1] | 3.823 [3.367, 3.881] | ±10.0% | slower |
| searchLarge / searchMs | 1890.4 [1921.9] | 2179.5 [2251.9] | 1.153 [1.148, 1.163] | ±3.0% | slower |
| searchLarge / mainTaskMs | 371.1 [373.9] | 1497.8 [1574.5] | 4.037 [4.002, 4.066] | ±3.0% | slower |
| searchLarge / mainJsMs | 321.0 [322.7] | 1263.6 [1282.8] | 3.936 [3.910, 3.963] | ±3.0% | slower |
| annotate / settleMs | 293.1 [294.7] | 287.4 [296.1] | -5.7 [-19.5, +1.4] | ±8.8 | no detectable difference |
| annotate / mainTaskMs | 137.8 [143.0] | 198.0 [204.9] | 1.438 [1.382, 1.496] | ±4.9% | slower |
| annotate / mainJsMs | 103.4 [109.8] | 170.3 [175.3] | 1.648 [1.584, 1.731] | ±7.9% | slower |
| annotateLarge / settleMs | 289.3 [297.7] | 292.1 [309.5] | +2.7 [-13.3, +16.7] | ±8.7 | no detectable difference |
| annotateLarge / mainTaskMs | 143.2 [148.1] | 222.3 [235.7] | 1.552 [1.502, 1.577] | ±3.0% | slower |
| annotateLarge / mainJsMs | 109.8 [114.0] | 181.2 [193.1] | 1.650 [1.598, 1.696] | ±3.5% | slower |
| hover / settleMs | 1322.2 [1338.2] | 1666.3 [1676.5] | +344.1 [+336.3, +350.2] | ±39.7 | slower |
| hover / mainTaskMs | 427.2 [440.3] | 1239.9 [1266.4] | 2.902 [2.846, 2.942] | ±3.0% | slower |
| hover / mainJsMs | 387.4 [397.9] | 1205.6 [1216.7] | 3.112 [3.067, 3.157] | ±3.0% | slower |
| dragAnnot / settleMs | 410.6 [419.5] | 934.3 [948.4] | +523.6 [+520.1, +537.4] | ±12.3 | slower |
| dragAnnot / mainTaskMs | 171.8 [179.4] | 777.1 [784.1] | 4.522 [4.468, 4.606] | ±3.6% | slower |
| dragAnnot / mainJsMs | 142.4 [148.0] | 712.9 [720.0] | 5.007 [4.913, 5.087] | ±3.8% | slower |
| drawInk / settleMs | 3888.0 [3894.7] | 6989.1 [7033.8] | +3101.2 [+3078.4, +3140.8] | ±116.6 | slower |
| drawInk / mainTaskMs | 1071.2 [1117.7] | 4288.8 [4304.9] | 4.004 [3.903, 4.042] | ±3.0% | slower |
| drawInk / mainJsMs | 793.5 [835.2] | 4014.1 [4028.2] | 5.059 [4.928, 5.105] | ±3.0% | slower |
| typeField / settleMs | 175.6 [184.9] | 289.9 [298.1] | +114.3 [+107.1, +122.1] | ±13.4 | slower |
| typeField / mainTaskMs | 175.9 [182.5] | 353.2 [366.1] | 2.007 [1.978, 2.067] | ±4.9% | slower |
| typeField / mainJsMs | 144.8 [152.1] | 314.5 [326.5] | 2.172 [2.120, 2.262] | ±3.5% | slower |

## `main` vs `main-now`

| Row | main median [max] | main-now median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 61.9 [64.2] | 52.5 [57.2] | 0.849 [0.819, 0.875] | ±6.1% | faster |
| open(ebook) / openedMs | 135.9 [140.9] | 203.2 [205.2] | 1.495 [1.452, 1.521] | ±3.8% | slower |
| open(ebook) / firstPaintMs | 261.2 [269.2] | 366.4 [379.9] | 1.402 [1.290, 1.450] | ±3.5% | slower |
| open(ebook) / settledMs | 400.2 [405.7] | 358.0 [368.0] | 0.894 [0.878, 0.909] | ±3.0% | faster |
| open(ebook) / mainTaskMs | 171.5 [174.6] | 222.8 [227.8] | 1.299 [1.284, 1.316] | ±3.0% | slower |
| open(ebook) / mainJsMs | 141.4 [144.2] | 190.4 [193.5] | 1.346 [1.331, 1.364] | ±3.0% | slower |
| open(large) / readyMs | 63.5 [64.8] | 52.4 [56.7] | 0.824 [0.809, 0.863] | ±5.0% | faster |
| open(large) / openedMs | 157.3 [158.6] | 213.3 [215.1] | 1.356 [1.342, 1.363] | ±3.0% | slower |
| open(large) / firstPaintMs | 265.6 [272.4] | 335.4 [367.7] | 1.263 [1.235, 1.338] | ±4.5% | slower |
| open(large) / settledMs | 286.0 [292.4] | 354.7 [369.7] | 1.240 [1.195, 1.278] | ±3.7% | slower |
| open(large) / mainTaskMs | 320.2 [322.6] | 214.3 [222.8] | 0.669 [0.662, 0.685] | ±3.0% | faster |
| open(large) / mainJsMs | 291.2 [294.6] | 181.7 [187.8] | 0.624 [0.618, 0.640] | ±3.0% | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 2998.1 [3031.5] | 3113.3 [3169.6] | 1.038 [1.032, 1.042] | ±3.0% | slower |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scroll / frameP95Ms | 16.9 [17.5] | 17.1 [17.6] | 1.015 [0.999, 1.043] | ±3.0% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 752.9 [776.0] | 1007.6 [1039.8] | 1.338 [1.315, 1.362] | ±3.0% | slower |
| scroll / mainJsMs | 614.3 [632.4] | 864.1 [871.7] | 1.407 [1.378, 1.428] | ±3.0% | slower |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 3006.4 [3015.5] | 3148.0 [3186.7] | 1.047 [1.041, 1.051] | ±3.0% | slower |
| scrollMid / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scrollMid / frameP95Ms | 17.3 [17.7] | 17.0 [17.5] | 0.987 [0.965, 1.015] | ±4.4% | no detectable difference |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 767.7 [781.8] | 1071.9 [1085.5] | 1.396 [1.373, 1.411] | ±3.0% | slower |
| scrollMid / mainJsMs | 625.4 [643.3] | 938.5 [950.8] | 1.501 [1.478, 1.518] | ±3.0% | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 3007.7 [3031.9] | 3117.2 [3154.8] | 1.036 [1.029, 1.046] | ±3.0% | slower |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| coldScroll / frameP95Ms | 17.0 [17.7] | 17.0 [17.7] | 0.997 [0.959, 1.032] | ±3.5% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 762.3 [797.7] | 1043.3 [1055.9] | 1.368 [1.337, 1.376] | ±3.0% | slower |
| coldScroll / mainJsMs | 594.0 [625.8] | 874.9 [890.4] | 1.473 [1.435, 1.490] | ±3.0% | slower |
| jump / settleMs | 130.9 [141.5] | 150.1 [167.9] | +19.1 [+6.3, +23.6] | ±10.5 | slower |
| jump / mainTaskMs | 99.9 [105.8] | 157.5 [161.5] | 1.576 [1.526, 1.607] | ±6.4% | slower |
| jump / mainJsMs | 83.4 [89.7] | 135.2 [145.9] | 1.621 [1.549, 1.660] | ±5.2% | slower |
| zoom / settleMs | 459.9 [481.6] | 594.3 [611.0] | +134.4 [+123.5, +153.7] | ±25.5 | slower |
| zoom / zoom2SettleMs | 173.1 [177.8] | 198.5 [215.9] | +25.5 [+23.2, +34.4] | ±5.2 | slower |
| zoom / zoom4SettleMs | 287.3 [308.7] | 394.9 [404.3] | +107.6 [+96.4, +129.4] | ±26.1 | slower |
| zoom / backTo1SettleMs | 130.6 [143.2] | 179.5 [189.3] | +48.9 [+44.1, +57.1] | ±10.9 | slower |
| zoom / mainTaskMs | 376.2 [394.0] | 527.4 [540.4] | 1.402 [1.370, 1.419] | ±3.0% | slower |
| zoom / mainJsMs | 313.8 [326.5] | 459.6 [481.4] | 1.464 [1.421, 1.495] | ±3.1% | slower |
| zoomLarge / settleMs | 324.2 [338.5] | 324.0 [327.9] | -0.3 [-10.5, +3.9] | ±21.7 | no detectable difference |
| zoomLarge / zoom2SettleMs | 162.6 [166.4] | 165.4 [171.0] | +2.8 [-1.3, +7.3] | ±19.6 | no detectable difference |
| zoomLarge / zoom4SettleMs | 161.8 [182.2] | 157.2 [165.1] | -4.5 [-12.7, +1.1] | ±7.0 | no detectable difference |
| zoomLarge / backTo1SettleMs | 143.3 [153.9] | 196.7 [205.2] | +53.3 [+48.6, +65.5] | ±10.2 | slower |
| zoomLarge / mainTaskMs | 346.4 [355.2] | 482.2 [491.8] | 1.392 [1.332, 1.435] | ±3.0% | slower |
| zoomLarge / mainJsMs | 303.9 [311.3] | 413.2 [430.6] | 1.360 [1.337, 1.409] | ±3.0% | slower |
| select / settleMs | 282.0 [293.7] | 275.4 [286.5] | -6.6 [-15.8, +4.0] | ±12.7 | no detectable difference |
| select / mainTaskMs | 131.6 [134.3] | 140.2 [147.6] | 1.065 [1.043, 1.118] | ±6.0% | slower |
| select / mainJsMs | 100.3 [110.3] | 120.0 [126.6] | 1.197 [1.130, 1.268] | ±7.9% | slower |
| selectLarge / settleMs | 277.1 [296.7] | 276.4 [300.1] | -0.8 [-6.2, +8.3] | ±9.7 | no detectable difference |
| selectLarge / mainTaskMs | 124.0 [137.9] | 146.5 [153.5] | 1.182 [1.075, 1.214] | ±7.5% | slower |
| selectLarge / mainJsMs | 102.4 [113.6] | 126.7 [133.2] | 1.237 [1.130, 1.287] | ±7.1% | slower |
| search / searchMs | 81.1 [83.2] | 40.1 [43.8] | 0.495 [0.489, 0.501] | ±3.0% | faster |
| search / mainTaskMs | 16.2 [16.8] | 14.1 [18.5] | 0.871 [0.831, 0.907] | ±4.1% | faster |
| search / mainJsMs | 6.8 [8.2] | 10.3 [14.4] | 1.515 [1.322, 1.558] | ±10.0% | slower |
| searchLarge / searchMs | 1890.4 [1921.9] | 2235.9 [2284.2] | 1.183 [1.179, 1.198] | ±3.0% | slower |
| searchLarge / mainTaskMs | 371.1 [373.9] | 417.4 [426.8] | 1.125 [1.117, 1.134] | ±3.0% | slower |
| searchLarge / mainJsMs | 321.0 [322.7] | 360.2 [367.5] | 1.122 [1.115, 1.136] | ±3.0% | slower |
| annotate / settleMs | 293.1 [294.7] | 281.4 [293.1] | -11.7 [-15.0, -3.6] | ±8.8 | faster |
| annotate / mainTaskMs | 137.8 [143.0] | 154.1 [163.9] | 1.118 [1.085, 1.173] | ±4.9% | slower |
| annotate / mainJsMs | 103.4 [109.8] | 125.2 [136.4] | 1.211 [1.152, 1.281] | ±7.9% | slower |
| annotateLarge / settleMs | 289.3 [297.7] | 273.4 [279.8] | -15.9 [-23.8, -8.4] | ±8.7 | faster |
| annotateLarge / mainTaskMs | 143.2 [148.1] | 146.0 [150.6] | 1.020 [0.986, 1.040] | ±3.0% | no detectable difference |
| annotateLarge / mainJsMs | 109.8 [114.0] | 123.0 [126.6] | 1.120 [1.073, 1.153] | ±3.5% | slower |
| hover / settleMs | 1322.2 [1338.2] | 1322.4 [1334.1] | +0.2 [-5.8, +6.0] | ±39.7 | no detectable difference |
| hover / mainTaskMs | 427.2 [440.3] | 541.9 [556.3] | 1.269 [1.243, 1.296] | ±3.0% | slower |
| hover / mainJsMs | 387.4 [397.9] | 508.3 [524.0] | 1.312 [1.292, 1.341] | ±3.0% | slower |
| dragAnnot / settleMs | 410.6 [419.5] | 453.0 [475.0] | +42.3 [+38.3, +55.8] | ±12.3 | slower |
| dragAnnot / mainTaskMs | 171.8 [179.4] | 275.0 [281.3] | 1.600 [1.575, 1.628] | ±3.6% | slower |
| dragAnnot / mainJsMs | 142.4 [148.0] | 237.8 [241.7] | 1.670 [1.634, 1.697] | ±3.8% | slower |
| drawInk / settleMs | 3888.0 [3894.7] | 3947.8 [3977.3] | +59.8 [+52.4, +68.6] | ±116.6 | no detectable difference |
| drawInk / mainTaskMs | 1071.2 [1117.7] | 1605.7 [1616.8] | 1.499 [1.462, 1.514] | ±3.0% | slower |
| drawInk / mainJsMs | 793.5 [835.2] | 1345.3 [1351.3] | 1.695 [1.652, 1.712] | ±3.0% | slower |
| typeField / settleMs | 175.6 [184.9] | 152.2 [171.8] | -23.4 [-30.4, -14.6] | ±13.4 | faster |
| typeField / mainTaskMs | 175.9 [182.5] | 167.1 [173.6] | 0.950 [0.935, 0.971] | ±4.9% | faster |
| typeField / mainJsMs | 144.8 [152.1] | 138.5 [145.6] | 0.956 [0.932, 0.983] | ±3.5% | faster |

## `main-now` vs `plugins-next`

| Row | main-now median [max] | plugins-next median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.5 [57.2] | 108.4 [110.1] | 2.063 [2.031, 2.095] | ±6.1% | slower |
| open(ebook) / openedMs | 203.2 [205.2] | 287.4 [303.5] | 1.414 [1.403, 1.433] | ±3.8% | slower |
| open(ebook) / firstPaintMs | 366.4 [379.9] | 495.3 [539.9] | 1.352 [1.301, 1.464] | ±3.5% | slower |
| open(ebook) / settledMs | 358.0 [368.0] | 532.0 [546.8] | 1.486 [1.452, 1.515] | ±3.0% | slower |
| open(ebook) / mainTaskMs | 222.8 [227.8] | 414.7 [429.3] | 1.861 [1.838, 1.901] | ±3.0% | slower |
| open(ebook) / mainJsMs | 190.4 [193.5] | 358.5 [370.8] | 1.882 [1.849, 1.923] | ±3.0% | slower |
| open(large) / readyMs | 52.4 [56.7] | 111.1 [116.2] | 2.121 [2.047, 2.156] | ±5.0% | slower |
| open(large) / openedMs | 213.3 [215.1] | 309.4 [324.2] | 1.451 [1.437, 1.477] | ±3.0% | slower |
| open(large) / firstPaintMs | 335.4 [367.7] | 504.2 [550.2] | 1.503 [1.426, 1.549] | ±4.5% | slower |
| open(large) / settledMs | 354.7 [369.7] | 541.7 [561.1] | 1.527 [1.495, 1.584] | ±3.7% | slower |
| open(large) / mainTaskMs | 214.3 [222.8] | 440.6 [445.5] | 2.056 [2.008, 2.078] | ±3.0% | slower |
| open(large) / mainJsMs | 181.7 [187.8] | 383.2 [387.0] | 2.109 [2.053, 2.130] | ±3.0% | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.8] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 3113.3 [3169.6] | 3099.1 [3166.4] | 0.995 [0.985, 1.011] | ±3.0% | no detectable difference |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scroll / frameP95Ms | 17.1 [17.6] | 17.5 [18.0] | 1.020 [0.988, 1.041] | ±3.0% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 1007.6 [1039.8] | 1618.3 [1658.3] | 1.606 [1.585, 1.640] | ±3.0% | slower |
| scroll / mainJsMs | 864.1 [871.7] | 1504.4 [1540.7] | 1.741 [1.722, 1.789] | ±3.0% | slower |
| scrollMid / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / wallMs | 3148.0 [3186.7] | 3352.6 [3431.5] | 1.065 [1.054, 1.075] | ±3.0% | slower |
| scrollMid / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scrollMid / frameP95Ms | 17.0 [17.5] | 17.8 [18.2] | 1.046 [1.030, 1.065] | ±4.4% | slower |
| scrollMid / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scrollMid / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scrollMid / mainTaskMs | 1071.9 [1085.5] | 1899.3 [1925.6] | 1.772 [1.758, 1.785] | ±3.0% | slower |
| scrollMid / mainJsMs | 938.5 [950.8] | 1800.3 [1830.2] | 1.918 [1.900, 1.940] | ±3.0% | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 3117.2 [3154.8] | 3134.6 [3225.1] | 1.006 [0.997, 1.015] | ±3.0% | no detectable difference |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| coldScroll / frameP95Ms | 17.0 [17.7] | 17.7 [18.2] | 1.045 [1.011, 1.068] | ±3.5% | slower |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 1043.3 [1055.9] | 1664.6 [1715.5] | 1.596 [1.578, 1.619] | ±3.0% | slower |
| coldScroll / mainJsMs | 874.9 [890.4] | 1521.0 [1562.4] | 1.739 [1.720, 1.764] | ±3.0% | slower |
| jump / settleMs | 150.1 [167.9] | 173.2 [192.1] | +23.1 [+15.0, +32.8] | ±10.5 | slower |
| jump / mainTaskMs | 157.5 [161.5] | 257.6 [271.2] | 1.635 [1.563, 1.687] | ±6.4% | slower |
| jump / mainJsMs | 135.2 [145.9] | 238.9 [248.1] | 1.767 [1.705, 1.819] | ±5.2% | slower |
| zoom / settleMs | 594.3 [611.0] | 654.0 [673.4] | +59.7 [+48.5, +68.7] | ±25.5 | slower |
| zoom / zoom2SettleMs | 198.5 [215.9] | 236.7 [245.9] | +38.2 [+29.9, +43.1] | ±6.0 | slower |
| zoom / zoom4SettleMs | 394.9 [404.3] | 419.0 [427.5] | +24.1 [+14.3, +31.1] | ±26.1 | no detectable difference |
| zoom / backTo1SettleMs | 179.5 [189.3] | 121.1 [196.6] | -58.3 [-61.7, -30.4] | ±10.9 | faster |
| zoom / mainTaskMs | 527.4 [540.4] | 630.3 [697.3] | 1.195 [1.159, 1.247] | ±3.0% | slower |
| zoom / mainJsMs | 459.6 [481.4] | 570.1 [642.6] | 1.240 [1.205, 1.298] | ±3.1% | slower |
| zoomLarge / settleMs | 324.0 [327.9] | 367.9 [391.8] | +43.9 [+37.0, +53.9] | ±21.7 | slower |
| zoomLarge / zoom2SettleMs | 165.4 [171.0] | 199.2 [211.7] | +33.8 [+24.6, +38.8] | ±19.6 | slower |
| zoomLarge / zoom4SettleMs | 157.2 [165.1] | 174.7 [180.1] | +17.4 [+9.3, +23.1] | ±7.0 | slower |
| zoomLarge / backTo1SettleMs | 196.7 [205.2] | 198.1 [203.1] | +1.5 [-4.4, +5.6] | ±10.2 | no detectable difference |
| zoomLarge / mainTaskMs | 482.2 [491.8] | 603.3 [632.4] | 1.251 [1.235, 1.314] | ±3.0% | slower |
| zoomLarge / mainJsMs | 413.2 [430.6] | 563.8 [585.2] | 1.365 [1.337, 1.394] | ±3.0% | slower |
| select / settleMs | 275.4 [286.5] | 278.5 [296.0] | +3.1 [-4.6, +14.1] | ±12.7 | no detectable difference |
| select / mainTaskMs | 140.2 [147.6] | 188.4 [191.6] | 1.344 [1.306, 1.374] | ±6.0% | slower |
| select / mainJsMs | 120.0 [126.6] | 167.0 [172.1] | 1.391 [1.346, 1.463] | ±7.9% | slower |
| selectLarge / settleMs | 276.4 [300.1] | 312.9 [332.4] | +36.5 [+26.4, +45.4] | ±9.7 | slower |
| selectLarge / mainTaskMs | 146.5 [153.5] | 210.5 [218.1] | 1.437 [1.393, 1.493] | ±7.5% | slower |
| selectLarge / mainJsMs | 126.7 [133.2] | 179.6 [195.5] | 1.417 [1.375, 1.470] | ±7.1% | slower |
| search / searchMs | 40.1 [43.8] | 45.2 [46.3] | 1.126 [1.111, 1.143] | ±3.0% | slower |
| search / mainTaskMs | 14.1 [18.5] | 30.5 [31.2] | 2.165 [2.092, 2.276] | ±4.1% | slower |
| search / mainJsMs | 10.3 [14.4] | 25.9 [27.1] | 2.523 [2.430, 2.686] | ±10.0% | slower |
| searchLarge / searchMs | 2235.9 [2284.2] | 2179.5 [2251.9] | 0.975 [0.965, 0.980] | ±3.0% | no detectable difference |
| searchLarge / mainTaskMs | 417.4 [426.8] | 1497.8 [1574.5] | 3.589 [3.555, 3.615] | ±3.0% | slower |
| searchLarge / mainJsMs | 360.2 [367.5] | 1263.6 [1282.8] | 3.508 [3.463, 3.533] | ±3.0% | slower |
| annotate / settleMs | 281.4 [293.1] | 287.4 [296.1] | +5.9 [-9.9, +12.2] | ±8.6 | no detectable difference |
| annotate / mainTaskMs | 154.1 [163.9] | 198.0 [204.9] | 1.285 [1.232, 1.308] | ±4.9% | slower |
| annotate / mainJsMs | 125.2 [136.4] | 170.3 [175.3] | 1.361 [1.295, 1.420] | ±7.9% | slower |
| annotateLarge / settleMs | 273.4 [279.8] | 292.1 [309.5] | +18.6 [+3.2, +32.4] | ±8.2 | slower |
| annotateLarge / mainTaskMs | 146.0 [150.6] | 222.3 [235.7] | 1.522 [1.488, 1.565] | ±3.0% | slower |
| annotateLarge / mainJsMs | 123.0 [126.6] | 181.2 [193.1] | 1.473 [1.437, 1.527] | ±3.5% | slower |
| hover / settleMs | 1322.4 [1334.1] | 1666.3 [1676.5] | +343.9 [+335.3, +351.5] | ±39.7 | slower |
| hover / mainTaskMs | 541.9 [556.3] | 1239.9 [1266.4] | 2.288 [2.252, 2.311] | ±3.0% | slower |
| hover / mainJsMs | 508.3 [524.0] | 1205.6 [1216.7] | 2.372 [2.335, 2.394] | ±3.0% | slower |
| dragAnnot / settleMs | 453.0 [475.0] | 934.3 [948.4] | +481.3 [+470.4, +494.9] | ±13.6 | slower |
| dragAnnot / mainTaskMs | 275.0 [281.3] | 777.1 [784.1] | 2.826 [2.807, 2.859] | ±3.6% | slower |
| dragAnnot / mainJsMs | 237.8 [241.7] | 712.9 [720.0] | 2.998 [2.983, 3.030] | ±3.8% | slower |
| drawInk / settleMs | 3947.8 [3977.3] | 6989.1 [7033.8] | +3041.4 [+3017.6, +3081.1] | ±118.4 | slower |
| drawInk / mainTaskMs | 1605.7 [1616.8] | 4288.8 [4304.9] | 2.671 [2.653, 2.682] | ±3.0% | slower |
| drawInk / mainJsMs | 1345.3 [1351.3] | 4014.1 [4028.2] | 2.984 [2.966, 2.995] | ±3.0% | slower |
| typeField / settleMs | 152.2 [171.8] | 289.9 [298.1] | +137.7 [+129.8, +145.0] | ±13.4 | slower |
| typeField / mainTaskMs | 167.1 [173.6] | 353.2 [366.1] | 2.113 [2.077, 2.175] | ±4.9% | slower |
| typeField / mainJsMs | 138.5 [145.6] | 314.5 [326.5] | 2.271 [2.237, 2.342] | ±3.5% | slower |
| undo / settleMs | 202.5 [207.6] | 846.7 [854.4] | +644.3 [+627.3, +648.8] | ±6.1 | slower |
| undo / mainTaskMs | 234.4 [243.2] | 876.0 [884.8] | 3.737 [3.640, 3.815] | ±3.0% | slower |
| undo / mainJsMs | 157.9 [159.5] | 633.1 [640.6] | 4.009 [3.917, 4.104] | ±3.0% | slower |

## `plugins-next` vs `plugins-next-lto`

Not run: `plugins-next-lto`.

## Memory

| Row | main median [max] | main-aa median [max] | plugins-next median [max] | main-now median [max] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.39 MiB [35.40 MiB] | 34.38 MiB [35.40 MiB] | 56.37 MiB [56.87 MiB] | 50.72 MiB [50.73 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 27.70 MiB [27.70 MiB] | 27.73 MiB [27.73 MiB] |
| open(large) / heapAfterOpenBytes | 48.20 MiB [48.23 MiB] | 48.20 MiB [48.23 MiB] | 71.23 MiB [71.25 MiB] | 52.67 MiB [52.70 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.28 MiB] | 22.26 MiB [22.29 MiB] | 27.61 MiB [27.62 MiB] | 27.63 MiB [27.64 MiB] |
| soak / heapCycle5Bytes | 37.47 MiB [38.46 MiB] | 37.47 MiB [38.48 MiB] | 63.78 MiB [64.27 MiB] | 58.31 MiB [58.34 MiB] |
| soak / heapCycle30Bytes | 38.28 MiB [39.28 MiB] | 38.28 MiB [39.29 MiB] | 64.29 MiB [64.79 MiB] | 59.36 MiB [59.38 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.05 MiB [0.06 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-next`:

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
| wasmBytes | 7.63 MiB | 7.63 MiB |
| wireRefs | 916 | 916 |

