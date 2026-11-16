# Viewer perf results, 2026-11-16

The plugins tip after the upstream catch-up, `2aaa8f841` (`plugins-next`), is slower than upstream
`main` at `ed064e0c8` (`main-now`) on 44 of 63 rows and faster on 1. Its median latency row is
1.239x and its median main-thread row 1.603x (`node scripts/summary.mjs`). Opening is the largest
latency gap (`open(ebook)` settles in 538.7 ms against 356.1), and `searchLarge` main-thread
JavaScript the largest CPU gap (1,268.1 ms against 360.6, 3.517x). Four of the slower settle rows
(`zoom`, `selectLarge`, `annotate`, `annotateLarge`) are within how far the unchanged `main` build
moved since 2026-10-13, so they're unconfirmed. Upstream itself moved: `main-now` against the old
`main` (`2516e2786`) is 1.034x latency and 1.257x main thread, so the tip's 1.246x and 1.859x
against `main` are not the port's cost alone. Building the tip's kernel with LTO
(`plugins-next-lto`) puts `open`'s main-thread rows at 0.879x to 0.922x and changes nothing
measurable elsewhere (54 of 63 rows no different), with a 14.68 MiB kernel wasm against 10.35 MiB.
The tip against `9d6c2eca1` isn't measured: that build needs a fork build this machine's container
can't do. `FINDINGS.md`, "The tip after the catch-up, against upstream main", has each
no-difference row's threshold, the run-to-run drift and the build notes.

# Viewer perf comparison: 2026-11-16T20-20-49-728Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 92c3bb74e0c5cf6d2c04c970126bdd5556544da4 |
| Chromium / Playwright | 140.0.7339.16 / 1.55.0 |
| Node | v24.20.0 |
| OS | Darwin 27.3.0 arm64 |
| CPU | Apple M1, 8 cores |
| Memory | 16 GiB |
| Power | AC Power |
| Viewport | 1280x800 @2x |
| Fixtures | ebook 2e539f62b5fa, large 10c8668fe55c |

| Arm | Ref | SHA | Payload | Engine wasm sha256 | Kernel wasm sha256 |
| --- | --- | --- | --- | --- | --- |
| main | main | 2516e2786 | fetched | 98c2368a774d |  |
| main-aa | main | 2516e2786 | fetched | 98c2368a774d |  |
| plugins-next | research/multi-platform-rust-core-impl-plugins | 2aaa8f841 | built (fork 17d8073d4, wasm32-eh) | 2ecd0bd98574 | 5a061a4a9cd1 |
| main-now | main | ed064e0c8 | fetched | 7195838021c8 |  |
| plugins-next-lto | research/multi-platform-rust-core-impl-plugins | 2aaa8f841 | built (fork 17d8073d4, wasm32-eh) | 2ecd0bd98574 | 7a132eddda4f |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-next`: pass
- `main-now`: pass
- `plugins-next-lto`: pass

## Sizes

| Metric | main | main-aa | plugins-next | main-now | plugins-next-lto |
| --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 14.26 MiB | 7.52 MiB | 14.26 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 3.79 MiB | 2.35 MiB | 3.79 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 10.35 MiB | 0.00 MiB | 14.68 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 2.44 MiB | 0.00 MiB | 2.69 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 8.50 MiB | 9.50 MiB | 8.50 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 2.90 MiB | 3.12 MiB | 2.90 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

63 of 63 rows pass.

| Row | main median [max] | main-aa median [max] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 64.0 [74.4] | 60.7 [64.1] | 0.948 [0.932, 1.014] | pass |
| open(ebook) / openedMs | 136.4 [169.8] | 134.9 [138.2] | 0.989 [0.954, 1.010] | pass |
| open(ebook) / firstPaintMs | 261.4 [302.1] | 258.9 [269.5] | 0.990 [0.962, 1.026] | pass |
| open(ebook) / settledMs | 402.7 [431.8] | 399.3 [405.3] | 0.992 [0.975, 1.001] | pass |
| open(ebook) / mainTaskMs | 172.7 [174.4] | 170.1 [175.0] | 0.985 [0.965, 1.010] | pass |
| open(ebook) / mainJsMs | 142.1 [143.4] | 140.1 [142.7] | 0.986 [0.965, 1.010] | pass |
| open(large) / readyMs | 63.1 [64.5] | 64.2 [64.9] | 1.017 [0.955, 1.057] | pass |
| open(large) / openedMs | 157.6 [160.5] | 157.5 [161.6] | 0.999 [0.977, 1.015] | pass |
| open(large) / firstPaintMs | 265.8 [272.8] | 268.5 [270.5] | 1.010 [0.980, 1.048] | pass |
| open(large) / settledMs | 286.2 [292.8] | 288.9 [290.9] | 1.009 [0.982, 1.044] | pass |
| open(large) / mainTaskMs | 320.4 [322.4] | 320.3 [321.4] | 1.000 [0.992, 1.002] | pass |
| open(large) / mainJsMs | 291.9 [294.8] | 291.2 [292.6] | 0.998 [0.991, 1.004] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 3014.8 [3022.8] | 2998.1 [3015.4] | 0.994 [0.994, 1.002] | pass |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 16.9 [17.5] | 17.1 [17.8] | 1.016 [0.987, 1.038] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 758.6 [769.5] | 752.5 [772.4] | 0.992 [0.976, 1.004] | pass |
| scroll / mainJsMs | 619.9 [630.9] | 615.1 [631.5] | 0.992 [0.974, 1.004] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 3008.2 [3033.4] | 3016.2 [3033.4] | 1.003 [0.995, 1.006] | pass |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | pass |
| coldScroll / frameP95Ms | 17.3 [17.7] | 17.2 [17.4] | 0.992 [0.970, 1.029] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 766.0 [780.3] | 764.4 [777.0] | 0.998 [0.986, 1.009] | pass |
| coldScroll / mainJsMs | 597.8 [607.1] | 596.3 [604.8] | 0.997 [0.987, 1.015] | pass |
| jump / settleMs | 130.7 [141.8] | 137.5 [142.1] | +6.8 [-3.6, +11.3] | pass |
| jump / mainTaskMs | 101.3 [108.8] | 101.7 [111.0] | 1.004 [0.966, 1.060] | pass |
| jump / mainJsMs | 85.2 [90.5] | 88.1 [92.1] | 1.034 [0.962, 1.097] | pass |
| zoom / settleMs | 451.6 [475.1] | 458.4 [477.1] | +6.8 [-14.9, +25.9] | pass |
| zoom / zoom2SettleMs | 173.6 [177.7] | 173.9 [190.6] | +0.3 [-1.7, +3.6] | pass |
| zoom / zoom4SettleMs | 276.6 [305.0] | 281.5 [303.5] | +4.8 [-19.9, +21.2] | pass |
| zoom / backTo1SettleMs | 126.5 [137.8] | 130.0 [137.5] | +3.5 [-2.9, +10.0] | pass |
| zoom / mainTaskMs | 379.3 [390.2] | 378.4 [391.2] | 0.998 [0.975, 1.015] | pass |
| zoom / mainJsMs | 312.0 [323.5] | 314.4 [327.0] | 1.008 [0.978, 1.029] | pass |
| zoomLarge / settleMs | 325.6 [350.9] | 318.6 [332.2] | -7.0 [-26.3, +4.3] | pass |
| zoomLarge / zoom2SettleMs | 162.1 [187.6] | 159.9 [166.4] | -2.1 [-19.2, +8.8] | pass |
| zoomLarge / zoom4SettleMs | 163.4 [182.1] | 160.9 [169.2] | -2.5 [-5.4, -0.7] | pass |
| zoomLarge / backTo1SettleMs | 143.6 [147.6] | 143.9 [145.2] | +0.4 [-5.4, +7.8] | pass |
| zoomLarge / mainTaskMs | 339.2 [353.2] | 339.4 [350.2] | 1.001 [0.977, 1.012] | pass |
| zoomLarge / mainJsMs | 298.0 [310.6] | 297.8 [308.5] | 0.999 [0.977, 1.014] | pass |
| select / settleMs | 285.6 [292.3] | 280.2 [291.5] | -5.4 [-12.0, +2.8] | pass |
| select / mainTaskMs | 129.4 [135.9] | 126.6 [134.8] | 0.978 [0.947, 1.039] | pass |
| select / mainJsMs | 102.4 [109.6] | 99.8 [109.3] | 0.974 [0.905, 1.041] | pass |
| selectLarge / settleMs | 272.3 [295.2] | 274.5 [287.9] | +2.2 [-13.8, +7.8] | pass |
| selectLarge / mainTaskMs | 123.4 [145.2] | 122.6 [139.9] | 0.993 [0.925, 1.096] | pass |
| selectLarge / mainJsMs | 102.2 [110.7] | 100.5 [114.3] | 0.983 [0.927, 1.081] | pass |
| search / searchMs | 81.7 [83.1] | 81.8 [84.0] | 1.001 [0.995, 1.006] | pass |
| search / mainTaskMs | 16.4 [17.8] | 16.5 [17.2] | 1.004 [0.943, 1.036] | pass |
| search / mainJsMs | 6.8 [7.7] | 6.8 [8.2] | 1.008 [0.926, 1.112] | pass |
| searchLarge / searchMs | 1886.8 [1921.0] | 1882.4 [1928.3] | 0.998 [0.989, 1.008] | pass |
| searchLarge / mainTaskMs | 370.4 [371.8] | 370.6 [372.3] | 1.001 [0.995, 1.005] | pass |
| searchLarge / mainJsMs | 320.0 [322.9] | 321.0 [324.4] | 1.003 [0.994, 1.009] | pass |
| annotate / settleMs | 291.0 [293.6] | 292.2 [297.2] | +1.2 [-7.4, +5.0] | pass |
| annotate / mainTaskMs | 141.3 [147.6] | 140.2 [146.5] | 0.992 [0.967, 1.018] | pass |
| annotate / mainJsMs | 105.8 [115.1] | 105.2 [113.9] | 0.994 [0.938, 1.052] | pass |
| annotateLarge / settleMs | 291.9 [298.3] | 290.5 [295.9] | -1.4 [-5.7, +3.4] | pass |
| annotateLarge / mainTaskMs | 144.2 [149.8] | 142.9 [148.3] | 0.991 [0.966, 1.022] | pass |
| annotateLarge / mainJsMs | 110.9 [116.6] | 109.0 [114.2] | 0.983 [0.959, 1.017] | pass |

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
| open(ebook) / readyMs | 64.0 [74.4] | 109.9 [121.3] | 1.717 [1.664, 1.813] | ±6.8% | slower |
| open(ebook) / openedMs | 136.4 [169.8] | 288.6 [301.1] | 2.116 [2.043, 2.166] | ±4.6% | slower |
| open(ebook) / firstPaintMs | 261.4 [302.1] | 501.5 [552.5] | 1.918 [1.853, 2.017] | ±3.8% | slower |
| open(ebook) / settledMs | 402.7 [431.8] | 538.7 [548.1] | 1.338 [1.308, 1.358] | ±3.0% | slower |
| open(ebook) / mainTaskMs | 172.7 [174.4] | 423.0 [430.8] | 2.449 [2.412, 2.476] | ±3.5% | slower |
| open(ebook) / mainJsMs | 142.1 [143.4] | 363.3 [371.2] | 2.557 [2.517, 2.611] | ±3.5% | slower |
| open(large) / readyMs | 63.1 [64.5] | 109.2 [111.5] | 1.730 [1.690, 1.810] | ±5.7% | slower |
| open(large) / openedMs | 157.6 [160.5] | 310.8 [314.1] | 1.972 [1.943, 1.997] | ±3.0% | slower |
| open(large) / firstPaintMs | 265.8 [272.8] | 502.4 [550.1] | 1.890 [1.855, 1.964] | ±4.8% | slower |
| open(large) / settledMs | 286.2 [292.8] | 541.7 [589.6] | 1.893 [1.852, 1.958] | ±4.4% | slower |
| open(large) / mainTaskMs | 320.4 [322.4] | 443.7 [453.3] | 1.385 [1.369, 1.396] | ±3.0% | slower |
| open(large) / mainJsMs | 291.9 [294.8] | 384.4 [392.4] | 1.317 [1.304, 1.331] | ±3.0% | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 3014.8 [3022.8] | 3097.7 [3156.8] | 1.027 [1.017, 1.044] | ±3.0% | no detectable difference |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scroll / frameP95Ms | 16.9 [17.5] | 17.6 [18.0] | 1.041 [1.017, 1.063] | ±3.8% | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 758.6 [769.5] | 1625.6 [1674.9] | 2.143 [2.114, 2.170] | ±3.0% | slower |
| scroll / mainJsMs | 619.9 [630.9] | 1504.5 [1559.5] | 2.427 [2.390, 2.458] | ±3.0% | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 3008.2 [3033.4] | 3166.5 [3205.2] | 1.053 [1.026, 1.064] | ±3.0% | slower |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| coldScroll / frameP95Ms | 17.3 [17.7] | 17.8 [18.1] | 1.028 [1.009, 1.065] | ±3.0% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 766.0 [780.3] | 1671.3 [1714.2] | 2.182 [2.153, 2.218] | ±3.0% | slower |
| coldScroll / mainJsMs | 597.8 [607.1] | 1530.0 [1567.4] | 2.559 [2.513, 2.609] | ±3.0% | slower |
| jump / settleMs | 130.7 [141.8] | 174.9 [192.2] | +44.2 [+31.6, +56.9] | ±11.3 | slower |
| jump / mainTaskMs | 101.3 [108.8] | 260.6 [266.1] | 2.572 [2.472, 2.663] | ±6.0% | slower |
| jump / mainJsMs | 85.2 [90.5] | 240.7 [250.7] | 2.824 [2.692, 2.989] | ±9.7% | slower |
| zoom / settleMs | 451.6 [475.1] | 651.6 [683.2] | +200.0 [+179.3, +225.2] | ±25.9 | slower |
| zoom / zoom2SettleMs | 173.6 [177.7] | 234.2 [260.0] | +60.6 [+56.3, +68.9] | ±5.2 | slower |
| zoom / zoom4SettleMs | 276.6 [305.0] | 414.4 [445.9] | +137.7 [+117.7, +154.9] | ±21.2 | slower |
| zoom / backTo1SettleMs | 126.5 [137.8] | 131.1 [190.8] | +4.6 [-9.3, +28.7] | ±10.0 | no detectable difference |
| zoom / mainTaskMs | 379.3 [390.2] | 630.6 [678.1] | 1.663 [1.629, 1.736] | ±3.0% | slower |
| zoom / mainJsMs | 312.0 [323.5] | 573.8 [624.7] | 1.839 [1.796, 1.920] | ±3.0% | slower |
| zoomLarge / settleMs | 325.6 [350.9] | 371.5 [389.7] | +45.9 [+26.4, +60.3] | ±26.3 | slower |
| zoomLarge / zoom2SettleMs | 162.1 [187.6] | 196.4 [210.5] | +34.4 [+20.8, +46.0] | ±19.2 | slower |
| zoomLarge / zoom4SettleMs | 163.4 [182.1] | 179.2 [185.9] | +15.8 [+9.5, +18.9] | ±5.4 | slower |
| zoomLarge / backTo1SettleMs | 143.6 [147.6] | 198.6 [213.4] | +55.0 [+51.7, +63.4] | ±7.8 | slower |
| zoomLarge / mainTaskMs | 339.2 [353.2] | 607.6 [637.0] | 1.792 [1.745, 1.810] | ±3.0% | slower |
| zoomLarge / mainJsMs | 298.0 [310.6] | 568.7 [580.6] | 1.909 [1.859, 1.938] | ±3.0% | slower |
| select / settleMs | 285.6 [292.3] | 286.7 [293.4] | +1.1 [-7.8, +10.5] | ±12.0 | no detectable difference |
| select / mainTaskMs | 129.4 [135.9] | 190.5 [196.0] | 1.473 [1.431, 1.519] | ±5.3% | slower |
| select / mainJsMs | 102.4 [109.6] | 170.1 [176.3] | 1.661 [1.576, 1.770] | ±9.5% | slower |
| selectLarge / settleMs | 272.3 [295.2] | 308.9 [326.4] | +36.6 [+18.7, +43.0] | ±13.8 | slower |
| selectLarge / mainTaskMs | 123.4 [145.2] | 211.5 [219.4] | 1.714 [1.604, 1.752] | ±9.6% | slower |
| selectLarge / mainJsMs | 102.2 [110.7] | 179.2 [188.0] | 1.754 [1.655, 1.798] | ±8.1% | slower |
| search / searchMs | 81.7 [83.1] | 45.3 [46.4] | 0.554 [0.548, 0.560] | ±3.0% | faster |
| search / mainTaskMs | 16.4 [17.8] | 30.8 [32.5] | 1.880 [1.821, 1.953] | ±5.7% | slower |
| search / mainJsMs | 6.8 [7.7] | 25.8 [26.7] | 3.816 [3.491, 3.933] | ±11.2% | slower |
| searchLarge / searchMs | 1886.8 [1921.0] | 2177.8 [2204.4] | 1.154 [1.146, 1.165] | ±3.0% | slower |
| searchLarge / mainTaskMs | 370.4 [371.8] | 1514.6 [1553.5] | 4.090 [3.964, 4.152] | ±3.0% | slower |
| searchLarge / mainJsMs | 320.0 [322.9] | 1268.1 [1311.2] | 3.962 [3.933, 4.011] | ±3.0% | slower |
| annotate / settleMs | 291.0 [293.6] | 289.8 [294.7] | -1.2 [-9.1, +3.7] | ±8.7 | no detectable difference |
| annotate / mainTaskMs | 141.3 [147.6] | 195.8 [216.0] | 1.385 [1.365, 1.441] | ±3.3% | slower |
| annotate / mainJsMs | 105.8 [115.1] | 169.6 [181.8] | 1.603 [1.522, 1.673] | ±6.2% | slower |
| annotateLarge / settleMs | 291.9 [298.3] | 292.7 [315.0] | +0.7 [-4.0, +13.8] | ±8.8 | no detectable difference |
| annotateLarge / mainTaskMs | 144.2 [149.8] | 221.4 [229.9] | 1.536 [1.508, 1.573] | ±3.4% | slower |
| annotateLarge / mainJsMs | 110.9 [116.6] | 180.4 [185.6] | 1.627 [1.589, 1.668] | ±4.1% | slower |

## `main` vs `main-now`

| Row | main median [max] | main-now median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 64.0 [74.4] | 52.8 [61.4] | 0.825 [0.799, 0.910] | ±6.8% | faster |
| open(ebook) / openedMs | 136.4 [169.8] | 201.3 [209.1] | 1.475 [1.420, 1.510] | ±4.6% | slower |
| open(ebook) / firstPaintMs | 261.4 [302.1] | 367.7 [372.2] | 1.406 [1.374, 1.452] | ±3.8% | slower |
| open(ebook) / settledMs | 402.7 [431.8] | 356.1 [361.6] | 0.884 [0.875, 0.897] | ±3.0% | faster |
| open(ebook) / mainTaskMs | 172.7 [174.4] | 222.8 [226.1] | 1.290 [1.267, 1.305] | ±3.5% | slower |
| open(ebook) / mainJsMs | 142.1 [143.4] | 189.8 [191.4] | 1.336 [1.309, 1.357] | ±3.5% | slower |
| open(large) / readyMs | 63.1 [64.5] | 52.1 [59.2] | 0.825 [0.809, 0.859] | ±5.7% | faster |
| open(large) / openedMs | 157.6 [160.5] | 210.9 [215.9] | 1.338 [1.322, 1.363] | ±3.0% | slower |
| open(large) / firstPaintMs | 265.8 [272.8] | 335.6 [365.6] | 1.262 [1.226, 1.351] | ±4.8% | slower |
| open(large) / settledMs | 286.2 [292.8] | 346.0 [353.9] | 1.209 [1.175, 1.251] | ±4.4% | slower |
| open(large) / mainTaskMs | 320.4 [322.4] | 214.9 [221.4] | 0.671 [0.660, 0.679] | ±3.0% | faster |
| open(large) / mainJsMs | 291.9 [294.8] | 182.4 [189.9] | 0.625 [0.613, 0.633] | ±3.0% | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 3014.8 [3022.8] | 3119.3 [3168.7] | 1.035 [1.029, 1.043] | ±3.0% | slower |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scroll / frameP95Ms | 16.9 [17.5] | 17.5 [17.7] | 1.034 [0.998, 1.052] | ±3.8% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 758.6 [769.5] | 1014.1 [1021.8] | 1.337 [1.320, 1.344] | ±3.0% | slower |
| scroll / mainJsMs | 619.9 [630.9] | 859.6 [878.6] | 1.387 [1.371, 1.408] | ±3.0% | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 3008.2 [3033.4] | 3121.0 [3165.8] | 1.037 [1.029, 1.046] | ±3.0% | slower |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| coldScroll / frameP95Ms | 17.3 [17.7] | 17.4 [17.9] | 1.008 [0.988, 1.046] | ±3.0% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 766.0 [780.3] | 1041.8 [1046.3] | 1.360 [1.339, 1.373] | ±3.0% | slower |
| coldScroll / mainJsMs | 597.8 [607.1] | 873.6 [879.5] | 1.461 [1.447, 1.482] | ±3.0% | slower |
| jump / settleMs | 130.7 [141.8] | 152.6 [156.9] | +21.9 [+10.1, +25.0] | ±11.3 | slower |
| jump / mainTaskMs | 101.3 [108.8] | 156.4 [166.6] | 1.543 [1.487, 1.606] | ±6.0% | slower |
| jump / mainJsMs | 85.2 [90.5] | 133.5 [146.4] | 1.566 [1.489, 1.666] | ±9.7% | slower |
| zoom / settleMs | 451.6 [475.1] | 604.6 [616.2] | +153.0 [+130.7, +165.2] | ±25.9 | slower |
| zoom / zoom2SettleMs | 173.6 [177.7] | 204.8 [213.7] | +31.2 [+23.5, +35.5] | ±5.2 | slower |
| zoom / zoom4SettleMs | 276.6 [305.0] | 393.9 [407.9] | +117.2 [+99.1, +134.3] | ±21.2 | slower |
| zoom / backTo1SettleMs | 126.5 [137.8] | 179.0 [186.3] | +52.5 [+47.5, +55.7] | ±10.0 | slower |
| zoom / mainTaskMs | 379.3 [390.2] | 525.8 [532.0] | 1.387 [1.357, 1.409] | ±3.0% | slower |
| zoom / mainJsMs | 312.0 [323.5] | 456.0 [466.3] | 1.462 [1.431, 1.490] | ±3.0% | slower |
| zoomLarge / settleMs | 325.6 [350.9] | 321.7 [346.4] | -3.9 [-21.1, +7.7] | ±26.3 | no detectable difference |
| zoomLarge / zoom2SettleMs | 162.1 [187.6] | 165.7 [186.0] | +3.7 [-7.8, +15.0] | ±19.2 | no detectable difference |
| zoomLarge / zoom4SettleMs | 163.4 [182.1] | 158.1 [162.9] | -5.3 [-11.8, -3.0] | ±5.4 | no detectable difference |
| zoomLarge / backTo1SettleMs | 143.6 [147.6] | 197.8 [211.4] | +54.3 [+51.5, +62.8] | ±7.8 | slower |
| zoomLarge / mainTaskMs | 339.2 [353.2] | 481.5 [494.9] | 1.420 [1.368, 1.443] | ±3.0% | slower |
| zoomLarge / mainJsMs | 298.0 [310.6] | 414.4 [424.5] | 1.391 [1.357, 1.414] | ±3.0% | slower |
| select / settleMs | 285.6 [292.3] | 274.8 [286.2] | -10.8 [-15.9, -1.4] | ±12.0 | no detectable difference |
| select / mainTaskMs | 129.4 [135.9] | 139.6 [148.4] | 1.079 [1.048, 1.129] | ±5.3% | slower |
| select / mainJsMs | 102.4 [109.6] | 119.6 [126.0] | 1.167 [1.118, 1.242] | ±9.5% | slower |
| selectLarge / settleMs | 272.3 [295.2] | 276.5 [291.6] | +4.2 [-11.9, +7.1] | ±13.8 | no detectable difference |
| selectLarge / mainTaskMs | 123.4 [145.2] | 146.1 [148.2] | 1.184 [1.102, 1.208] | ±9.6% | slower |
| selectLarge / mainJsMs | 102.2 [110.7] | 125.1 [127.2] | 1.224 [1.150, 1.258] | ±8.1% | slower |
| search / searchMs | 81.7 [83.1] | 40.7 [46.0] | 0.497 [0.491, 0.523] | ±3.0% | faster |
| search / mainTaskMs | 16.4 [17.8] | 14.6 [19.4] | 0.892 [0.853, 1.043] | ±5.7% | no detectable difference |
| search / mainJsMs | 6.8 [7.7] | 10.4 [15.8] | 1.540 [1.412, 1.871] | ±11.2% | slower |
| searchLarge / searchMs | 1886.8 [1921.0] | 2231.9 [2244.5] | 1.183 [1.171, 1.189] | ±3.0% | slower |
| searchLarge / mainTaskMs | 370.4 [371.8] | 416.1 [424.0] | 1.123 [1.113, 1.134] | ±3.0% | slower |
| searchLarge / mainJsMs | 320.0 [322.9] | 360.6 [363.2] | 1.127 [1.118, 1.134] | ±3.0% | slower |
| annotate / settleMs | 291.0 [293.6] | 279.1 [291.2] | -11.9 [-15.4, -5.4] | ±8.7 | faster |
| annotate / mainTaskMs | 141.3 [147.6] | 152.9 [159.3] | 1.082 [1.034, 1.116] | ±3.3% | slower |
| annotate / mainJsMs | 105.8 [115.1] | 121.7 [134.2] | 1.150 [1.092, 1.207] | ±6.2% | slower |
| annotateLarge / settleMs | 291.9 [298.3] | 277.1 [284.2] | -14.8 [-20.2, -10.5] | ±8.8 | faster |
| annotateLarge / mainTaskMs | 144.2 [149.8] | 149.0 [152.2] | 1.033 [1.007, 1.062] | ±3.4% | no detectable difference |
| annotateLarge / mainJsMs | 110.9 [116.6] | 125.4 [130.3] | 1.131 [1.095, 1.166] | ±4.1% | slower |

## `main-now` vs `plugins-next`

| Row | main-now median [max] | plugins-next median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.8 [61.4] | 109.9 [121.3] | 2.081 [1.895, 2.155] | ±6.8% | slower |
| open(ebook) / openedMs | 201.3 [209.1] | 288.6 [301.1] | 1.434 [1.414, 1.455] | ±4.6% | slower |
| open(ebook) / firstPaintMs | 367.7 [372.2] | 501.5 [552.5] | 1.364 [1.325, 1.432] | ±3.8% | slower |
| open(ebook) / settledMs | 356.1 [361.6] | 538.7 [548.1] | 1.513 [1.477, 1.532] | ±3.0% | slower |
| open(ebook) / mainTaskMs | 222.8 [226.1] | 423.0 [430.8] | 1.898 [1.865, 1.940] | ±3.5% | slower |
| open(ebook) / mainJsMs | 189.8 [191.4] | 363.3 [371.2] | 1.914 [1.884, 1.965] | ±3.5% | slower |
| open(large) / readyMs | 52.1 [59.2] | 109.2 [111.5] | 2.097 [2.059, 2.143] | ±5.7% | slower |
| open(large) / openedMs | 210.9 [215.9] | 310.8 [314.1] | 1.474 [1.447, 1.487] | ±3.0% | slower |
| open(large) / firstPaintMs | 335.6 [365.6] | 502.4 [550.1] | 1.497 [1.414, 1.545] | ±4.8% | slower |
| open(large) / settledMs | 346.0 [353.9] | 541.7 [589.6] | 1.566 [1.528, 1.613] | ±4.4% | slower |
| open(large) / mainTaskMs | 214.9 [221.4] | 443.7 [453.3] | 2.064 [2.033, 2.095] | ±3.0% | slower |
| open(large) / mainJsMs | 182.4 [189.9] | 384.4 [392.4] | 2.108 [2.077, 2.150] | ±3.0% | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 3119.3 [3168.7] | 3097.7 [3156.8] | 0.993 [0.983, 1.009] | ±3.0% | no detectable difference |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scroll / frameP95Ms | 17.5 [17.7] | 17.6 [18.0] | 1.007 [0.984, 1.045] | ±3.8% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 1014.1 [1021.8] | 1625.6 [1674.9] | 1.603 [1.586, 1.626] | ±3.0% | slower |
| scroll / mainJsMs | 859.6 [878.6] | 1504.5 [1559.5] | 1.750 [1.720, 1.776] | ±3.0% | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 3121.0 [3165.8] | 3166.5 [3205.2] | 1.015 [0.987, 1.026] | ±3.0% | no detectable difference |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| coldScroll / frameP95Ms | 17.4 [17.9] | 17.8 [18.1] | 1.020 [0.998, 1.042] | ±3.0% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 1041.8 [1046.3] | 1671.3 [1714.2] | 1.604 [1.584, 1.640] | ±3.0% | slower |
| coldScroll / mainJsMs | 873.6 [879.5] | 1530.0 [1567.4] | 1.751 [1.724, 1.774] | ±3.0% | slower |
| jump / settleMs | 152.6 [156.9] | 174.9 [192.2] | +22.3 [+14.7, +36.1] | ±11.3 | slower |
| jump / mainTaskMs | 156.4 [166.6] | 260.6 [266.1] | 1.666 [1.612, 1.711] | ±6.0% | slower |
| jump / mainJsMs | 133.5 [146.4] | 240.7 [250.7] | 1.803 [1.743, 1.849] | ±9.7% | slower |
| zoom / settleMs | 604.6 [616.2] | 651.6 [683.2] | +47.0 [+32.2, +75.3] | ±25.9 | slower |
| zoom / zoom2SettleMs | 204.8 [213.7] | 234.2 [260.0] | +29.4 [+24.1, +39.8] | ±6.1 | slower |
| zoom / zoom4SettleMs | 393.9 [407.9] | 414.4 [445.9] | +20.5 [+6.4, +32.6] | ±21.2 | no detectable difference |
| zoom / backTo1SettleMs | 179.0 [186.3] | 131.1 [190.8] | -47.9 [-60.4, -22.6] | ±10.0 | faster |
| zoom / mainTaskMs | 525.8 [532.0] | 630.6 [678.1] | 1.199 [1.180, 1.251] | ±3.0% | slower |
| zoom / mainJsMs | 456.0 [466.3] | 573.8 [624.7] | 1.258 [1.233, 1.312] | ±3.0% | slower |
| zoomLarge / settleMs | 321.7 [346.4] | 371.5 [389.7] | +49.8 [+39.3, +62.6] | ±26.3 | slower |
| zoomLarge / zoom2SettleMs | 165.7 [186.0] | 196.4 [210.5] | +30.7 [+19.6, +39.1] | ±19.2 | slower |
| zoomLarge / zoom4SettleMs | 158.1 [162.9] | 179.2 [185.9] | +21.1 [+14.8, +26.9] | ±5.4 | slower |
| zoomLarge / backTo1SettleMs | 197.8 [211.4] | 198.6 [213.4] | +0.8 [-1.6, +2.9] | ±7.8 | no detectable difference |
| zoomLarge / mainTaskMs | 481.5 [494.9] | 607.6 [637.0] | 1.262 [1.242, 1.298] | ±3.0% | slower |
| zoomLarge / mainJsMs | 414.4 [424.5] | 568.7 [580.6] | 1.372 [1.351, 1.385] | ±3.0% | slower |
| select / settleMs | 274.8 [286.2] | 286.7 [293.4] | +11.9 [-1.1, +20.2] | ±12.0 | no detectable difference |
| select / mainTaskMs | 139.6 [148.4] | 190.5 [196.0] | 1.365 [1.305, 1.404] | ±5.3% | slower |
| select / mainJsMs | 119.6 [126.0] | 170.1 [176.3] | 1.423 [1.368, 1.470] | ±9.5% | slower |
| selectLarge / settleMs | 276.5 [291.6] | 308.9 [326.4] | +32.4 [+25.7, +40.1] | ±13.8 | slower |
| selectLarge / mainTaskMs | 146.1 [148.2] | 211.5 [219.4] | 1.448 [1.428, 1.488] | ±9.6% | slower |
| selectLarge / mainJsMs | 125.1 [127.2] | 179.2 [188.0] | 1.433 [1.395, 1.474] | ±8.1% | slower |
| search / searchMs | 40.7 [46.0] | 45.3 [46.4] | 1.114 [1.062, 1.133] | ±3.0% | slower |
| search / mainTaskMs | 14.6 [19.4] | 30.8 [32.5] | 2.107 [1.808, 2.205] | ±5.7% | slower |
| search / mainJsMs | 10.4 [15.8] | 25.8 [26.7] | 2.477 [2.016, 2.540] | ±11.2% | slower |
| searchLarge / searchMs | 2231.9 [2244.5] | 2177.8 [2204.4] | 0.976 [0.972, 0.984] | ±3.0% | no detectable difference |
| searchLarge / mainTaskMs | 416.1 [424.0] | 1514.6 [1553.5] | 3.640 [3.537, 3.702] | ±3.0% | slower |
| searchLarge / mainJsMs | 360.6 [363.2] | 1268.1 [1311.2] | 3.517 [3.488, 3.557] | ±3.0% | slower |
| annotate / settleMs | 279.1 [291.2] | 289.8 [294.7] | +10.6 [+0.9, +16.7] | ±8.4 | slower |
| annotate / mainTaskMs | 152.9 [159.3] | 195.8 [216.0] | 1.280 [1.248, 1.358] | ±3.3% | slower |
| annotate / mainJsMs | 121.7 [134.2] | 169.6 [181.8] | 1.394 [1.343, 1.428] | ±6.2% | slower |
| annotateLarge / settleMs | 277.1 [284.2] | 292.7 [315.0] | +15.6 [+11.1, +28.9] | ±8.3 | slower |
| annotateLarge / mainTaskMs | 149.0 [152.2] | 221.4 [229.9] | 1.486 [1.458, 1.521] | ±3.4% | slower |
| annotateLarge / mainJsMs | 125.4 [130.3] | 180.4 [185.6] | 1.438 [1.400, 1.474] | ±4.1% | slower |

## `plugins-next` vs `plugins-next-lto`

| Row | plugins-next median [max] | plugins-next-lto median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 109.9 [121.3] | 122.5 [124.9] | 1.115 [1.083, 1.150] | ±6.8% | slower |
| open(ebook) / openedMs | 288.6 [301.1] | 298.1 [302.8] | 1.033 [1.018, 1.046] | ±4.6% | no detectable difference |
| open(ebook) / firstPaintMs | 501.5 [552.5] | 455.6 [508.7] | 0.909 [0.865, 0.971] | ±3.8% | faster |
| open(ebook) / settledMs | 538.7 [548.1] | 492.8 [500.9] | 0.915 [0.901, 0.934] | ±3.0% | faster |
| open(ebook) / mainTaskMs | 423.0 [430.8] | 382.4 [391.8] | 0.904 [0.884, 0.928] | ±3.5% | faster |
| open(ebook) / mainJsMs | 363.3 [371.2] | 319.5 [329.3] | 0.879 [0.858, 0.905] | ±3.5% | faster |
| open(large) / readyMs | 109.2 [111.5] | 120.2 [126.4] | 1.101 [1.082, 1.132] | ±5.7% | slower |
| open(large) / openedMs | 310.8 [314.1] | 319.0 [326.7] | 1.026 [1.019, 1.037] | ±3.0% | no detectable difference |
| open(large) / firstPaintMs | 502.4 [550.1] | 467.6 [519.8] | 0.931 [0.902, 0.974] | ±4.8% | faster |
| open(large) / settledMs | 541.7 [589.6] | 507.3 [545.5] | 0.936 [0.914, 1.010] | ±4.4% | no detectable difference |
| open(large) / mainTaskMs | 443.7 [453.3] | 409.2 [415.8] | 0.922 [0.909, 0.933] | ±3.0% | faster |
| open(large) / mainJsMs | 384.4 [392.4] | 347.1 [353.0] | 0.903 [0.890, 0.914] | ±3.0% | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [5.4] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / wallMs | 3097.7 [3156.8] | 3116.7 [3172.9] | 1.006 [0.990, 1.019] | ±3.0% | no detectable difference |
| scroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| scroll / frameP95Ms | 17.6 [18.0] | 17.7 [18.0] | 1.009 [0.984, 1.032] | ±3.8% | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| scroll / mainTaskMs | 1625.6 [1674.9] | 1621.7 [1655.8] | 0.998 [0.982, 1.016] | ±3.0% | no detectable difference |
| scroll / mainJsMs | 1504.5 [1559.5] | 1497.7 [1552.6] | 0.995 [0.978, 1.023] | ±3.0% | no detectable difference |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / wallMs | 3166.5 [3205.2] | 3087.7 [3192.5] | 0.975 [0.963, 1.006] | ±3.0% | no detectable difference |
| coldScroll / frameP50Ms | 16.7 [16.7] | 16.7 [16.7] | 1.000 [1.000, 1.000] | ±3.0% | no detectable difference |
| coldScroll / frameP95Ms | 17.8 [18.1] | 17.7 [17.9] | 0.993 [0.976, 1.015] | ±3.0% | no detectable difference |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | ±0 | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | ±0.0 | no detectable difference |
| coldScroll / mainTaskMs | 1671.3 [1714.2] | 1648.4 [1671.2] | 0.986 [0.972, 1.003] | ±3.0% | no detectable difference |
| coldScroll / mainJsMs | 1530.0 [1567.4] | 1507.9 [1530.2] | 0.986 [0.972, 1.007] | ±3.0% | no detectable difference |
| jump / settleMs | 174.9 [192.2] | 174.4 [197.7] | -0.5 [-13.4, +13.1] | ±11.3 | no detectable difference |
| jump / mainTaskMs | 260.6 [266.1] | 257.2 [272.0] | 0.987 [0.961, 1.018] | ±6.0% | no detectable difference |
| jump / mainJsMs | 240.7 [250.7] | 237.3 [253.8] | 0.986 [0.967, 1.022] | ±9.7% | no detectable difference |
| zoom / settleMs | 651.6 [683.2] | 655.0 [675.9] | +3.4 [-21.8, +18.5] | ±25.9 | no detectable difference |
| zoom / zoom2SettleMs | 234.2 [260.0] | 242.9 [253.5] | +8.7 [-1.2, +14.9] | ±7.0 | no detectable difference |
| zoom / zoom4SettleMs | 414.4 [445.9] | 410.5 [435.7] | -3.9 [-16.7, +4.0] | ±21.2 | no detectable difference |
| zoom / backTo1SettleMs | 131.1 [190.8] | 142.5 [194.6] | +11.4 [-12.9, +60.6] | ±10.0 | no detectable difference |
| zoom / mainTaskMs | 630.6 [678.1] | 646.8 [689.9] | 1.026 [0.986, 1.071] | ±3.0% | no detectable difference |
| zoom / mainJsMs | 573.8 [624.7] | 590.0 [634.0] | 1.028 [0.986, 1.079] | ±3.0% | no detectable difference |
| zoomLarge / settleMs | 371.5 [389.7] | 376.3 [392.0] | +4.8 [-11.6, +15.1] | ±26.3 | no detectable difference |
| zoomLarge / zoom2SettleMs | 196.4 [210.5] | 200.9 [207.8] | +4.4 [-9.1, +14.7] | ±19.2 | no detectable difference |
| zoomLarge / zoom4SettleMs | 179.2 [185.9] | 176.0 [187.6] | -3.2 [-7.1, +4.3] | ±5.4 | no detectable difference |
| zoomLarge / backTo1SettleMs | 198.6 [213.4] | 199.0 [204.4] | +0.4 [-2.7, +4.4] | ±7.8 | no detectable difference |
| zoomLarge / mainTaskMs | 607.6 [637.0] | 608.1 [629.7] | 1.001 [0.989, 1.023] | ±3.0% | no detectable difference |
| zoomLarge / mainJsMs | 568.7 [580.6] | 568.3 [591.8] | 0.999 [0.983, 1.014] | ±3.0% | no detectable difference |
| select / settleMs | 286.7 [293.4] | 282.3 [309.9] | -4.4 [-12.8, +8.0] | ±12.0 | no detectable difference |
| select / mainTaskMs | 190.5 [196.0] | 187.9 [202.5] | 0.986 [0.961, 1.011] | ±5.3% | no detectable difference |
| select / mainJsMs | 170.1 [176.3] | 167.2 [180.8] | 0.983 [0.954, 1.024] | ±9.5% | no detectable difference |
| selectLarge / settleMs | 308.9 [326.4] | 309.2 [326.1] | +0.3 [-11.6, +10.4] | ±13.8 | no detectable difference |
| selectLarge / mainTaskMs | 211.5 [219.4] | 209.6 [216.3] | 0.991 [0.968, 1.001] | ±9.6% | no detectable difference |
| selectLarge / mainJsMs | 179.2 [188.0] | 177.3 [183.4] | 0.989 [0.962, 1.007] | ±8.1% | no detectable difference |
| search / searchMs | 45.3 [46.4] | 46.1 [46.7] | 1.017 [1.000, 1.032] | ±3.0% | no detectable difference |
| search / mainTaskMs | 30.8 [32.5] | 31.1 [33.0] | 1.009 [0.981, 1.046] | ±5.7% | no detectable difference |
| search / mainJsMs | 25.8 [26.7] | 25.4 [27.7] | 0.988 [0.956, 1.035] | ±11.2% | no detectable difference |
| searchLarge / searchMs | 2177.8 [2204.4] | 2165.8 [2183.6] | 0.994 [0.984, 1.002] | ±3.0% | no detectable difference |
| searchLarge / mainTaskMs | 1514.6 [1553.5] | 1490.5 [1519.3] | 0.984 [0.968, 1.018] | ±3.0% | no detectable difference |
| searchLarge / mainJsMs | 1268.1 [1311.2] | 1241.8 [1276.4] | 0.979 [0.969, 0.989] | ±3.0% | no detectable difference |
| annotate / settleMs | 289.8 [294.7] | 286.0 [295.0] | -3.8 [-15.7, +4.8] | ±8.7 | no detectable difference |
| annotate / mainTaskMs | 195.8 [216.0] | 197.1 [202.7] | 1.007 [0.963, 1.022] | ±3.3% | no detectable difference |
| annotate / mainJsMs | 169.6 [181.8] | 168.6 [176.7] | 0.994 [0.964, 1.011] | ±6.2% | no detectable difference |
| annotateLarge / settleMs | 292.7 [315.0] | 292.1 [305.0] | -0.5 [-13.4, +4.2] | ±8.8 | no detectable difference |
| annotateLarge / mainTaskMs | 221.4 [229.9] | 220.9 [227.4] | 0.998 [0.980, 1.015] | ±3.4% | no detectable difference |
| annotateLarge / mainJsMs | 180.4 [185.6] | 180.0 [185.4] | 0.998 [0.981, 1.012] | ±4.1% | no detectable difference |

## Memory

| Row | main median [max] | main-aa median [max] | plugins-next median [max] | main-now median [max] | plugins-next-lto median [max] |
| --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.38 MiB [35.39 MiB] | 34.38 MiB [35.39 MiB] | 56.36 MiB [56.84 MiB] | 50.72 MiB [50.73 MiB] | 51.24 MiB [52.26 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 27.70 MiB [27.70 MiB] | 27.72 MiB [27.73 MiB] | 27.70 MiB [27.70 MiB] |
| open(large) / heapAfterOpenBytes | 48.19 MiB [48.21 MiB] | 48.18 MiB [48.21 MiB] | 71.22 MiB [71.24 MiB] | 52.68 MiB [52.69 MiB] | 71.13 MiB [71.16 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.29 MiB] | 22.26 MiB [22.29 MiB] | 27.61 MiB [27.62 MiB] | 27.63 MiB [27.64 MiB] | 27.60 MiB [27.61 MiB] |
| soak / heapCycle5Bytes | 37.46 MiB [38.47 MiB] | 37.47 MiB [38.48 MiB] | 63.76 MiB [69.28 MiB] | 58.32 MiB [58.32 MiB] | 58.55 MiB [62.68 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [39.28 MiB] | 38.27 MiB [39.29 MiB] | 64.30 MiB [69.77 MiB] | 59.36 MiB [59.38 MiB] | 59.17 MiB [63.18 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.05 MiB [0.06 MiB] | 0.03 MiB [0.03 MiB] |

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

`plugins-next-lto`:

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
| wasmBytes | 7.56 MiB | 7.56 MiB |
| wireRefs | 916 | 916 |

