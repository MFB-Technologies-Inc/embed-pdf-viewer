# Viewer perf comparison: 2026-10-12T01-08-19-312Z-macmini2020-desktop

10 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | f0ca236d0efd90f9ea942ec3180df8ff5d25c0a6 |
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
| plugins-resulttext | research/rust-kernel-result-text | 34644a2b4 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 63a60ebf8d9a |
| plugins-rustreads | research/rust-kernel-result-text | 34644a2b4 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 63a60ebf8d9a |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-resulttext`: pass
- `plugins-rustreads`: pass

## Sizes

| Metric | main | main-aa | plugins-resulttext | plugins-rustreads |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 8.06 MiB | 8.06 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.73 MiB | 1.73 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

58 of 63 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 60.5 [69.6] | 62.2 [64.4] | 1.027 [0.973, 1.060] | pass |
| open(ebook) / openedMs | 136.1 [151.1] | 136.3 [140.6] | 1.001 [0.978, 1.029] | pass |
| open(ebook) / firstPaintMs | 254.1 [266.7] | 253.1 [264.8] | 0.996 [0.969, 1.021] | pass |
| open(ebook) / settledMs | 394.9 [407.9] | 394.7 [401.1] | 0.999 [0.988, 1.013] | pass |
| open(ebook) / mainTaskMs | 145.2 [147.9] | 143.3 [149.5] | 0.987 [0.975, 1.019] | pass |
| open(ebook) / mainJsMs | 111.1 [114.2] | 110.8 [115.2] | 0.997 [0.978, 1.024] | pass |
| open(large) / readyMs | 62.1 [67.3] | 63.4 [68.4] | 1.021 [0.973, 1.065] | pass |
| open(large) / openedMs | 157.8 [163.9] | 159.6 [162.9] | 1.012 [0.999, 1.025] | pass |
| open(large) / firstPaintMs | 268.8 [287.0] | 267.3 [276.1] | 0.994 [0.952, 1.019] | pass |
| open(large) / settledMs | 269.0 [279.9] | 269.0 [282.7] | 1.000 [0.970, 1.022] | pass |
| open(large) / mainTaskMs | 315.7 [318.9] | 316.2 [321.0] | 1.002 [0.993, 1.014] | pass |
| open(large) / mainJsMs | 284.4 [287.4] | 285.3 [290.1] | 1.003 [0.994, 1.016] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2879.6 [2919.0] | 2863.5 [2890.8] | 0.994 [0.985, 1.023] | pass |
| scroll / frameP50Ms | 10.9 [11.5] | 11.2 [11.4] | 1.029 [0.994, 1.045] | pass |
| scroll / frameP95Ms | 22.1 [26.2] | 25.7 [27.1] | 1.164 [1.050, 1.222] | A/A failed |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 536.6 [554.1] | 530.4 [545.9] | 0.988 [0.964, 1.012] | pass |
| scroll / mainJsMs | 398.0 [405.5] | 392.0 [400.1] | 0.985 [0.977, 1.018] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 2853.7 [2905.5] | 2835.4 [2890.9] | 0.994 [0.970, 1.008] | pass |
| coldScroll / frameP50Ms | 11.1 [11.5] | 11.0 [11.6] | 0.990 [0.980, 1.034] | pass |
| coldScroll / frameP95Ms | 22.1 [25.3] | 25.6 [27.2] | 1.157 [1.043, 1.193] | A/A failed |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 561.4 [570.3] | 555.3 [569.0] | 0.989 [0.971, 1.007] | pass |
| coldScroll / mainJsMs | 391.8 [398.2] | 388.0 [398.3] | 0.990 [0.972, 1.010] | pass |
| jump / settleMs | 123.6 [130.6] | 127.6 [142.5] | +4.0 [-0.5, +15.2] | pass |
| jump / mainTaskMs | 72.7 [76.4] | 73.4 [79.1] | 1.010 [0.976, 1.070] | pass |
| jump / mainJsMs | 54.5 [58.4] | 56.7 [60.7] | 1.041 [0.971, 1.080] | pass |
| zoom / settleMs | 497.3 [514.8] | 499.5 [514.5] | +2.2 [-11.5, +20.5] | pass |
| zoom / zoom2SettleMs | 171.0 [187.9] | 168.4 [178.1] | -2.7 [-9.3, +6.7] | pass |
| zoom / zoom4SettleMs | 328.2 [335.9] | 334.1 [344.1] | +5.9 [-5.5, +17.3] | pass |
| zoom / backTo1SettleMs | 90.3 [97.4] | 90.3 [105.6] | -0.0 [-4.2, +7.9] | pass |
| zoom / mainTaskMs | 221.8 [228.6] | 228.2 [234.3] | 1.029 [0.992, 1.052] | pass |
| zoom / mainJsMs | 158.5 [165.9] | 164.3 [170.7] | 1.036 [0.989, 1.067] | pass |
| zoomLarge / settleMs | 321.3 [346.5] | 312.1 [334.6] | -9.2 [-40.1, +24.5] | pass |
| zoomLarge / zoom2SettleMs | 143.5 [172.4] | 144.0 [157.1] | +0.5 [-25.5, +14.2] | pass |
| zoomLarge / zoom4SettleMs | 172.9 [190.4] | 167.2 [179.5] | -5.7 [-18.5, +5.4] | pass |
| zoomLarge / backTo1SettleMs | 73.2 [83.4] | 72.9 [84.5] | -0.3 [-9.8, +8.3] | pass |
| zoomLarge / mainTaskMs | 173.4 [180.0] | 173.0 [182.2] | 0.998 [0.978, 1.016] | pass |
| zoomLarge / mainJsMs | 129.6 [132.6] | 128.7 [136.0] | 0.993 [0.966, 1.021] | pass |
| select / settleMs | 214.1 [264.9] | 213.2 [248.4] | -0.9 [-42.4, +16.8] | pass |
| select / mainTaskMs | 62.3 [66.7] | 60.2 [66.2] | 0.967 [0.895, 1.043] | pass |
| select / mainJsMs | 40.1 [44.3] | 38.9 [43.4] | 0.968 [0.892, 1.050] | pass |
| selectLarge / settleMs | 224.6 [244.9] | 198.6 [226.4] | -26.0 [-45.8, -0.8] | A/A failed |
| selectLarge / mainTaskMs | 71.1 [75.0] | 67.8 [72.4] | 0.953 [0.897, 1.060] | pass |
| selectLarge / mainJsMs | 48.0 [50.6] | 45.4 [49.1] | 0.946 [0.894, 1.069] | pass |
| search / searchMs | 72.3 [76.7] | 72.3 [77.9] | 1.001 [0.908, 1.073] | pass |
| search / mainTaskMs | 5.2 [6.0] | 5.5 [6.1] | 1.063 [0.952, 1.152] | pass |
| search / mainJsMs | 3.7 [3.8] | 3.7 [3.9] | 1.006 [0.954, 1.037] | pass |
| searchLarge / searchMs | 1894.1 [1936.9] | 1884.6 [1929.3] | 0.995 [0.973, 1.002] | pass |
| searchLarge / mainTaskMs | 342.9 [346.6] | 343.6 [347.5] | 1.002 [0.991, 1.009] | pass |
| searchLarge / mainJsMs | 290.5 [293.3] | 289.1 [292.5] | 0.995 [0.988, 1.003] | pass |
| annotate / settleMs | 207.1 [242.2] | 208.4 [239.3] | +1.3 [-15.6, +22.7] | pass |
| annotate / mainTaskMs | 74.6 [84.5] | 76.1 [82.0] | 1.020 [0.950, 1.059] | pass |
| annotate / mainJsMs | 45.4 [51.4] | 45.9 [49.0] | 1.011 [0.952, 1.048] | pass |
| annotateLarge / settleMs | 198.0 [255.6] | 228.7 [269.6] | +30.7 [-1.9, +57.4] | pass |
| annotateLarge / mainTaskMs | 79.1 [88.9] | 85.1 [88.9] | 1.076 [1.006, 1.112] | A/A failed |
| annotateLarge / mainJsMs | 47.8 [53.2] | 51.8 [53.8] | 1.084 [1.003, 1.130] | A/A failed |

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

Not run: `plugins-plainargs`.

## `main` vs `plugins-resulttext`

| Row | main median [p95] | plugins-resulttext median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 60.5 [69.6] | 93.1 [105.4] | 1.537 [1.440, 1.597] | slower |
| open(ebook) / openedMs | 136.1 [151.1] | 180.6 [193.6] | 1.327 [1.284, 1.373] | slower |
| open(ebook) / firstPaintMs | 254.1 [266.7] | 334.1 [354.3] | 1.315 [1.270, 1.356] | slower |
| open(ebook) / settledMs | 394.9 [407.9] | 398.2 [419.0] | 1.008 [0.997, 1.052] | no detectable difference |
| open(ebook) / mainTaskMs | 145.2 [147.9] | 235.4 [241.5] | 1.621 [1.599, 1.674] | slower |
| open(ebook) / mainJsMs | 111.1 [114.2] | 186.6 [193.7] | 1.680 [1.662, 1.732] | slower |
| open(large) / readyMs | 62.1 [67.3] | 90.0 [98.3] | 1.450 [1.386, 1.514] | slower |
| open(large) / openedMs | 157.8 [163.9] | 206.4 [212.6] | 1.308 [1.288, 1.335] | slower |
| open(large) / firstPaintMs | 268.8 [287.0] | 367.7 [388.3] | 1.368 [1.308, 1.405] | slower |
| open(large) / settledMs | 269.0 [279.9] | 359.7 [373.8] | 1.337 [1.301, 1.383] | slower |
| open(large) / mainTaskMs | 315.7 [318.9] | 294.4 [298.7] | 0.933 [0.927, 0.946] | faster |
| open(large) / mainJsMs | 284.4 [287.4] | 247.0 [249.6] | 0.868 [0.862, 0.878] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2879.6 [2919.0] | 2577.3 [2651.4] | 0.895 [0.852, 0.927] | faster |
| scroll / frameP50Ms | 10.9 [11.5] | 11.2 [11.4] | 1.034 [1.005, 1.048] | no detectable difference |
| scroll / frameP95Ms | 22.1 [26.2] | 25.9 [27.2] | 1.173 [1.060, 1.221] | unreliable (A/A failed) |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 536.6 [554.1] | 681.0 [698.9] | 1.269 [1.220, 1.291] | slower |
| scroll / mainJsMs | 398.0 [405.5] | 536.8 [551.4] | 1.349 [1.309, 1.391] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [14.1] | +0.0 [+0.0, +1.3] | no detectable difference |
| coldScroll / wallMs | 2853.7 [2905.5] | 2563.7 [2736.0] | 0.898 [0.876, 0.924] | faster |
| coldScroll / frameP50Ms | 11.1 [11.5] | 11.1 [11.8] | 0.996 [0.974, 1.037] | no detectable difference |
| coldScroll / frameP95Ms | 22.1 [25.3] | 26.2 [27.2] | 1.185 [0.984, 1.217] | unreliable (A/A failed) |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 561.4 [570.3] | 688.0 [708.0] | 1.226 [1.212, 1.252] | slower |
| coldScroll / mainJsMs | 391.8 [398.2] | 532.9 [553.5] | 1.360 [1.337, 1.382] | slower |
| jump / settleMs | 123.6 [130.6] | 129.3 [139.2] | +5.7 [+0.0, +10.5] | no detectable difference |
| jump / mainTaskMs | 72.7 [76.4] | 117.1 [123.3] | 1.612 [1.557, 1.671] | slower |
| jump / mainJsMs | 54.5 [58.4] | 99.9 [105.8] | 1.832 [1.725, 1.886] | slower |
| zoom / settleMs | 497.3 [514.8] | 549.8 [558.4] | +52.5 [+28.5, +66.9] | slower |
| zoom / zoom2SettleMs | 171.0 [187.9] | 192.0 [207.5] | +21.0 [+10.0, +31.9] | slower |
| zoom / zoom4SettleMs | 328.2 [335.9] | 351.7 [360.7] | +23.5 [+17.7, +34.6] | slower |
| zoom / backTo1SettleMs | 90.3 [97.4] | 96.8 [116.0] | +6.5 [+3.8, +15.0] | no detectable difference |
| zoom / mainTaskMs | 221.8 [228.6] | 299.3 [309.7] | 1.349 [1.300, 1.388] | slower |
| zoom / mainJsMs | 158.5 [165.9] | 238.5 [251.4] | 1.504 [1.441, 1.567] | slower |
| zoomLarge / settleMs | 321.3 [346.5] | 331.0 [359.5] | +9.7 [-20.6, +39.6] | no detectable difference |
| zoomLarge / zoom2SettleMs | 143.5 [172.4] | 148.4 [174.6] | +4.9 [-20.3, +24.7] | no detectable difference |
| zoomLarge / zoom4SettleMs | 172.9 [190.4] | 176.3 [209.5] | +3.4 [-19.5, +23.5] | no detectable difference |
| zoomLarge / backTo1SettleMs | 73.2 [83.4] | 81.5 [102.1] | +8.2 [-0.3, +18.2] | no detectable difference |
| zoomLarge / mainTaskMs | 173.4 [180.0] | 246.4 [256.9] | 1.421 [1.381, 1.473] | slower |
| zoomLarge / mainJsMs | 129.6 [132.6] | 202.8 [212.6] | 1.565 [1.517, 1.623] | slower |
| select / settleMs | 214.1 [264.9] | 254.8 [296.8] | +40.7 [-3.3, +68.3] | no detectable difference |
| select / mainTaskMs | 62.3 [66.7] | 82.2 [89.3] | 1.320 [1.247, 1.443] | slower |
| select / mainJsMs | 40.1 [44.3] | 63.2 [68.0] | 1.576 [1.452, 1.712] | slower |
| selectLarge / settleMs | 224.6 [244.9] | 281.7 [317.2] | +57.1 [+29.9, +75.9] | unreliable (A/A failed) |
| selectLarge / mainTaskMs | 71.1 [75.0] | 108.6 [119.5] | 1.527 [1.474, 1.687] | slower |
| selectLarge / mainJsMs | 48.0 [50.6] | 79.9 [89.0] | 1.665 [1.588, 1.862] | slower |
| search / searchMs | 72.3 [76.7] | 71.3 [80.8] | 0.986 [0.952, 1.075] | no detectable difference |
| search / mainTaskMs | 5.2 [6.0] | 8.6 [12.2] | 1.653 [1.556, 1.834] | slower |
| search / mainJsMs | 3.7 [3.8] | 5.9 [6.9] | 1.594 [1.547, 1.665] | slower |
| searchLarge / searchMs | 1894.1 [1936.9] | 1831.6 [1858.2] | 0.967 [0.948, 0.972] | faster |
| searchLarge / mainTaskMs | 342.9 [346.6] | 746.3 [758.3] | 2.177 [2.158, 2.192] | slower |
| searchLarge / mainJsMs | 290.5 [293.3] | 697.2 [708.6] | 2.400 [2.384, 2.418] | slower |
| annotate / settleMs | 207.1 [242.2] | 202.0 [273.6] | -5.1 [-35.7, +57.3] | no detectable difference |
| annotate / mainTaskMs | 74.6 [84.5] | 79.7 [91.5] | 1.069 [0.993, 1.204] | no detectable difference |
| annotate / mainJsMs | 45.4 [51.4] | 53.2 [62.1] | 1.171 [1.080, 1.352] | slower |
| annotateLarge / settleMs | 198.0 [255.6] | 244.2 [272.7] | +46.2 [+8.1, +65.7] | no detectable difference |
| annotateLarge / mainTaskMs | 79.1 [88.9] | 107.0 [115.9] | 1.354 [1.255, 1.409] | unreliable (A/A failed) |
| annotateLarge / mainJsMs | 47.8 [53.2] | 70.1 [74.9] | 1.469 [1.317, 1.532] | unreliable (A/A failed) |

## `plugins-resulttext` vs `plugins-rustreads`

| Row | plugins-resulttext median [p95] | plugins-rustreads median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 93.1 [105.4] | 91.3 [103.1] | 0.980 [0.932, 1.035] | no detectable difference |
| open(ebook) / openedMs | 180.6 [193.6] | 178.2 [189.9] | 0.986 [0.956, 1.018] | no detectable difference |
| open(ebook) / firstPaintMs | 334.1 [354.3] | 332.3 [366.4] | 0.994 [0.963, 1.028] | no detectable difference |
| open(ebook) / settledMs | 398.2 [419.0] | 394.2 [412.0] | 0.990 [0.952, 1.012] | no detectable difference |
| open(ebook) / mainTaskMs | 235.4 [241.5] | 235.4 [240.9] | 1.000 [0.987, 1.015] | no detectable difference |
| open(ebook) / mainJsMs | 186.6 [193.7] | 187.3 [191.8] | 1.004 [0.978, 1.013] | no detectable difference |
| open(large) / readyMs | 90.0 [98.3] | 93.7 [103.7] | 1.040 [0.975, 1.077] | no detectable difference |
| open(large) / openedMs | 206.4 [212.6] | 206.7 [217.6] | 1.001 [0.980, 1.030] | no detectable difference |
| open(large) / firstPaintMs | 367.7 [388.3] | 366.3 [396.0] | 0.996 [0.966, 1.044] | no detectable difference |
| open(large) / settledMs | 359.7 [373.8] | 357.3 [371.7] | 0.993 [0.963, 1.016] | no detectable difference |
| open(large) / mainTaskMs | 294.4 [298.7] | 292.8 [296.0] | 0.994 [0.979, 1.000] | no detectable difference |
| open(large) / mainJsMs | 247.0 [249.6] | 246.5 [248.7] | 0.998 [0.984, 1.007] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [10.4] | +0.0 [+0.0, +4.5] | no detectable difference |
| scroll / wallMs | 2577.3 [2651.4] | 2529.0 [2689.8] | 0.981 [0.965, 1.040] | no detectable difference |
| scroll / frameP50Ms | 11.2 [11.4] | 11.0 [11.3] | 0.982 [0.969, 0.997] | no detectable difference |
| scroll / frameP95Ms | 25.9 [27.2] | 25.0 [27.2] | 0.965 [0.861, 1.049] | unreliable (A/A failed) |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 681.0 [698.9] | 678.1 [707.3] | 0.996 [0.982, 1.032] | no detectable difference |
| scroll / mainJsMs | 536.8 [551.4] | 537.4 [557.9] | 1.001 [0.990, 1.031] | no detectable difference |
| coldScroll / settleMs | 0.0 [14.1] | 0.0 [6.0] | +0.0 [-1.3, +1.9] | no detectable difference |
| coldScroll / wallMs | 2563.7 [2736.0] | 2411.3 [2651.4] | 0.941 [0.916, 0.991] | faster |
| coldScroll / frameP50Ms | 11.1 [11.8] | 11.0 [13.1] | 0.990 [0.961, 1.035] | no detectable difference |
| coldScroll / frameP95Ms | 26.2 [27.2] | 25.2 [26.7] | 0.963 [0.892, 1.144] | unreliable (A/A failed) |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 688.0 [708.0] | 671.1 [698.1] | 0.975 [0.956, 0.995] | no detectable difference |
| coldScroll / mainJsMs | 532.9 [553.5] | 514.2 [538.0] | 0.965 [0.949, 0.988] | faster |
| jump / settleMs | 129.3 [139.2] | 129.8 [154.7] | +0.5 [-10.4, +23.0] | no detectable difference |
| jump / mainTaskMs | 117.1 [123.3] | 115.2 [117.1] | 0.983 [0.968, 1.013] | no detectable difference |
| jump / mainJsMs | 99.9 [105.8] | 98.5 [101.2] | 0.986 [0.967, 1.026] | no detectable difference |
| zoom / settleMs | 549.8 [558.4] | 545.7 [554.1] | -4.1 [-13.8, +16.8] | no detectable difference |
| zoom / zoom2SettleMs | 192.0 [207.5] | 196.2 [205.0] | +4.2 [-7.1, +15.1] | no detectable difference |
| zoom / zoom4SettleMs | 351.7 [360.7] | 348.5 [361.3] | -3.1 [-10.3, +3.5] | no detectable difference |
| zoom / backTo1SettleMs | 96.8 [116.0] | 98.7 [110.1] | +1.9 [-5.8, +5.9] | no detectable difference |
| zoom / mainTaskMs | 299.3 [309.7] | 298.6 [309.9] | 0.998 [0.967, 1.034] | no detectable difference |
| zoom / mainJsMs | 238.5 [251.4] | 241.3 [248.5] | 1.012 [0.958, 1.038] | no detectable difference |
| zoomLarge / settleMs | 331.0 [359.5] | 335.0 [356.6] | +4.0 [-15.4, +25.9] | no detectable difference |
| zoomLarge / zoom2SettleMs | 148.4 [174.6] | 149.2 [162.1] | +0.8 [-17.6, +10.8] | no detectable difference |
| zoomLarge / zoom4SettleMs | 176.3 [209.5] | 187.0 [207.6] | +10.6 [-13.8, +33.2] | no detectable difference |
| zoomLarge / backTo1SettleMs | 81.5 [102.1] | 78.5 [90.1] | -2.9 [-13.2, +3.0] | no detectable difference |
| zoomLarge / mainTaskMs | 246.4 [256.9] | 239.4 [247.8] | 0.971 [0.933, 1.010] | no detectable difference |
| zoomLarge / mainJsMs | 202.8 [212.6] | 196.8 [204.4] | 0.971 [0.931, 1.015] | no detectable difference |
| select / settleMs | 254.8 [296.8] | 261.7 [287.9] | +6.9 [-32.5, +31.7] | no detectable difference |
| select / mainTaskMs | 82.2 [89.3] | 82.5 [91.1] | 1.003 [0.937, 1.080] | no detectable difference |
| select / mainJsMs | 63.2 [68.0] | 62.8 [69.5] | 0.993 [0.926, 1.081] | no detectable difference |
| selectLarge / settleMs | 281.7 [317.2] | 271.6 [291.8] | -10.1 [-31.4, +17.3] | unreliable (A/A failed) |
| selectLarge / mainTaskMs | 108.6 [119.5] | 105.8 [112.6] | 0.975 [0.925, 1.031] | no detectable difference |
| selectLarge / mainJsMs | 79.9 [89.0] | 77.9 [84.9] | 0.975 [0.902, 1.040] | no detectable difference |
| search / searchMs | 71.3 [80.8] | 78.8 [85.3] | 1.105 [1.002, 1.187] | slower |
| search / mainTaskMs | 8.6 [12.2] | 9.1 [19.9] | 1.061 [0.950, 2.150] | no detectable difference |
| search / mainJsMs | 5.9 [6.9] | 6.0 [7.3] | 1.017 [0.975, 1.214] | no detectable difference |
| searchLarge / searchMs | 1831.6 [1858.2] | 1833.8 [1851.0] | 1.001 [0.995, 1.009] | no detectable difference |
| searchLarge / mainTaskMs | 746.3 [758.3] | 745.1 [751.2] | 0.998 [0.990, 1.005] | no detectable difference |
| searchLarge / mainJsMs | 697.2 [708.6] | 697.0 [701.9] | 1.000 [0.991, 1.005] | no detectable difference |
| annotate / settleMs | 202.0 [273.6] | 229.2 [258.1] | +27.2 [-39.9, +62.7] | no detectable difference |
| annotate / mainTaskMs | 79.7 [91.5] | 82.7 [90.9] | 1.037 [0.922, 1.115] | no detectable difference |
| annotate / mainJsMs | 53.2 [62.1] | 56.3 [62.3] | 1.059 [0.905, 1.155] | no detectable difference |
| annotateLarge / settleMs | 244.2 [272.7] | 251.1 [280.8] | +6.9 [-14.6, +46.9] | no detectable difference |
| annotateLarge / mainTaskMs | 107.0 [115.9] | 108.1 [118.1] | 1.010 [0.973, 1.093] | unreliable (A/A failed) |
| annotateLarge / mainJsMs | 70.1 [74.9] | 69.4 [77.7] | 0.989 [0.944, 1.132] | unreliable (A/A failed) |

## `main` vs `plugins-rustreads`

| Row | main median [p95] | plugins-rustreads median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 60.5 [69.6] | 91.3 [103.1] | 1.507 [1.430, 1.561] | slower |
| open(ebook) / openedMs | 136.1 [151.1] | 178.2 [189.9] | 1.309 [1.276, 1.339] | slower |
| open(ebook) / firstPaintMs | 254.1 [266.7] | 332.3 [366.4] | 1.308 [1.267, 1.350] | slower |
| open(ebook) / settledMs | 394.9 [407.9] | 394.2 [412.0] | 0.998 [0.988, 1.021] | no detectable difference |
| open(ebook) / mainTaskMs | 145.2 [147.9] | 235.4 [240.9] | 1.621 [1.611, 1.672] | slower |
| open(ebook) / mainJsMs | 111.1 [114.2] | 187.3 [191.8] | 1.685 [1.662, 1.727] | slower |
| open(large) / readyMs | 62.1 [67.3] | 93.7 [103.7] | 1.508 [1.390, 1.579] | slower |
| open(large) / openedMs | 157.8 [163.9] | 206.7 [217.6] | 1.310 [1.285, 1.348] | slower |
| open(large) / firstPaintMs | 268.8 [287.0] | 366.3 [396.0] | 1.363 [1.311, 1.414] | slower |
| open(large) / settledMs | 269.0 [279.9] | 357.3 [371.7] | 1.328 [1.292, 1.359] | slower |
| open(large) / mainTaskMs | 315.7 [318.9] | 292.8 [296.0] | 0.927 [0.916, 0.935] | faster |
| open(large) / mainJsMs | 284.4 [287.4] | 246.5 [248.7] | 0.867 [0.856, 0.874] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [10.4] | +0.0 [+0.0, +4.5] | no detectable difference |
| scroll / wallMs | 2879.6 [2919.0] | 2529.0 [2689.8] | 0.878 [0.870, 0.909] | faster |
| scroll / frameP50Ms | 10.9 [11.5] | 11.0 [11.3] | 1.016 [0.987, 1.028] | no detectable difference |
| scroll / frameP95Ms | 22.1 [26.2] | 25.0 [27.2] | 1.132 [0.976, 1.227] | unreliable (A/A failed) |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 536.6 [554.1] | 678.1 [707.3] | 1.264 [1.236, 1.290] | slower |
| scroll / mainJsMs | 398.0 [405.5] | 537.4 [557.9] | 1.350 [1.339, 1.393] | slower |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [6.0] | +0.0 [+0.0, +1.9] | no detectable difference |
| coldScroll / wallMs | 2853.7 [2905.5] | 2411.3 [2651.4] | 0.845 [0.829, 0.888] | faster |
| coldScroll / frameP50Ms | 11.1 [11.5] | 11.0 [13.1] | 0.987 [0.960, 1.035] | no detectable difference |
| coldScroll / frameP95Ms | 22.1 [25.3] | 25.2 [26.7] | 1.141 [1.020, 1.167] | unreliable (A/A failed) |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| coldScroll / mainTaskMs | 561.4 [570.3] | 671.1 [698.1] | 1.196 [1.182, 1.222] | slower |
| coldScroll / mainJsMs | 391.8 [398.2] | 514.2 [538.0] | 1.313 [1.294, 1.342] | slower |
| jump / settleMs | 123.6 [130.6] | 129.8 [154.7] | +6.2 [-3.3, +28.9] | no detectable difference |
| jump / mainTaskMs | 72.7 [76.4] | 115.2 [117.1] | 1.585 [1.547, 1.642] | slower |
| jump / mainJsMs | 54.5 [58.4] | 98.5 [101.2] | 1.807 [1.723, 1.864] | slower |
| zoom / settleMs | 497.3 [514.8] | 545.7 [554.1] | +48.4 [+32.8, +64.0] | slower |
| zoom / zoom2SettleMs | 171.0 [187.9] | 196.2 [205.0] | +25.1 [+14.6, +34.0] | slower |
| zoom / zoom4SettleMs | 328.2 [335.9] | 348.5 [361.3] | +20.4 [+14.1, +31.5] | slower |
| zoom / backTo1SettleMs | 90.3 [97.4] | 98.7 [110.1] | +8.4 [+4.7, +15.1] | slower |
| zoom / mainTaskMs | 221.8 [228.6] | 298.6 [309.9] | 1.346 [1.304, 1.386] | slower |
| zoom / mainJsMs | 158.5 [165.9] | 241.3 [248.5] | 1.522 [1.440, 1.565] | slower |
| zoomLarge / settleMs | 321.3 [346.5] | 335.0 [356.6] | +13.7 [-17.4, +46.4] | no detectable difference |
| zoomLarge / zoom2SettleMs | 143.5 [172.4] | 149.2 [162.1] | +5.7 [-20.0, +18.5] | no detectable difference |
| zoomLarge / zoom4SettleMs | 172.9 [190.4] | 187.0 [207.6] | +14.1 [-4.7, +24.2] | no detectable difference |
| zoomLarge / backTo1SettleMs | 73.2 [83.4] | 78.5 [90.1] | +5.3 [-3.4, +13.9] | no detectable difference |
| zoomLarge / mainTaskMs | 173.4 [180.0] | 239.4 [247.8] | 1.381 [1.347, 1.420] | slower |
| zoomLarge / mainJsMs | 129.6 [132.6] | 196.8 [204.4] | 1.519 [1.481, 1.571] | slower |
| select / settleMs | 214.1 [264.9] | 261.7 [287.9] | +47.6 [+0.0, +63.4] | slower |
| select / mainTaskMs | 62.3 [66.7] | 82.5 [91.1] | 1.325 [1.251, 1.453] | slower |
| select / mainJsMs | 40.1 [44.3] | 62.8 [69.5] | 1.565 [1.453, 1.713] | slower |
| selectLarge / settleMs | 224.6 [244.9] | 271.6 [291.8] | +47.0 [+25.0, +65.9] | unreliable (A/A failed) |
| selectLarge / mainTaskMs | 71.1 [75.0] | 105.8 [112.6] | 1.488 [1.436, 1.656] | slower |
| selectLarge / mainJsMs | 48.0 [50.6] | 77.9 [84.9] | 1.623 [1.524, 1.824] | slower |
| search / searchMs | 72.3 [76.7] | 78.8 [85.3] | 1.091 [1.003, 1.203] | no detectable difference |
| search / mainTaskMs | 5.2 [6.0] | 9.1 [19.9] | 1.753 [1.631, 3.552] | slower |
| search / mainJsMs | 3.7 [3.8] | 6.0 [7.3] | 1.621 [1.559, 1.936] | slower |
| searchLarge / searchMs | 1894.1 [1936.9] | 1833.8 [1851.0] | 0.968 [0.948, 0.976] | faster |
| searchLarge / mainTaskMs | 342.9 [346.6] | 745.1 [751.2] | 2.173 [2.149, 2.187] | slower |
| searchLarge / mainJsMs | 290.5 [293.3] | 697.0 [701.9] | 2.399 [2.380, 2.413] | slower |
| annotate / settleMs | 207.1 [242.2] | 229.2 [258.1] | +22.1 [-5.0, +48.9] | no detectable difference |
| annotate / mainTaskMs | 74.6 [84.5] | 82.7 [90.9] | 1.109 [1.026, 1.164] | slower |
| annotate / mainJsMs | 45.4 [51.4] | 56.3 [62.3] | 1.240 [1.122, 1.306] | slower |
| annotateLarge / settleMs | 198.0 [255.6] | 251.1 [280.8] | +53.1 [+23.5, +85.9] | no detectable difference |
| annotateLarge / mainTaskMs | 79.1 [88.9] | 108.1 [118.1] | 1.367 [1.303, 1.451] | unreliable (A/A failed) |
| annotateLarge / mainJsMs | 47.8 [53.2] | 69.4 [77.7] | 1.453 [1.369, 1.589] | unreliable (A/A failed) |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-resulttext median [p95] | plugins-rustreads median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 35.34 MiB [35.36 MiB] | 35.35 MiB [35.36 MiB] | 37.04 MiB [38.08 MiB] | 37.49 MiB [38.58 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 22.97 MiB [22.97 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.19 MiB] | 48.15 MiB [48.18 MiB] | 52.52 MiB [52.55 MiB] | 52.49 MiB [52.49 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.29 MiB] | 22.26 MiB [22.28 MiB] | 23.00 MiB [23.03 MiB] | 22.98 MiB [22.98 MiB] |
| soak / heapCycle5Bytes | 38.42 MiB [38.43 MiB] | 38.42 MiB [38.43 MiB] | 40.18 MiB [42.28 MiB] | 40.13 MiB [41.14 MiB] |
| soak / heapCycle30Bytes | 39.26 MiB [39.28 MiB] | 39.26 MiB [39.27 MiB] | 40.72 MiB [42.82 MiB] | 40.67 MiB [41.68 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-resulttext`:

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
| wasmBytes | 3.13 MiB | 3.13 MiB |
| wireRefs | 658 | 658 |

`plugins-rustreads`:

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
| wasmBytes | 3.13 MiB | 3.13 MiB |
| wireRefs | 658 | 658 |

