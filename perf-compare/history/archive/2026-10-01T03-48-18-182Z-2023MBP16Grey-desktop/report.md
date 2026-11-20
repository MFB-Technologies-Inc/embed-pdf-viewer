# Viewer perf comparison: 2026-10-01T03-48-18-182Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 54e81956771890db7b87a62dacfcff62fa8825be |
| Chromium / Playwright | 140.0.7339.16 / 1.55.0 |
| Node | v24.20.0 |
| OS | Darwin 27.0.0 arm64 |
| CPU | Apple M2 Max, 12 cores |
| Memory | 64 GiB |
| Power | AC Power |
| Viewport | 1280x800 @2x |
| Fixtures | ebook 2e539f62b5fa, large 10c8668fe55c |

| Arm | Ref | SHA | Payload | Engine wasm sha256 | Kernel wasm sha256 |
| --- | --- | --- | --- | --- | --- |
| main | main | 2516e2786 | fetched | 98c2368a774d |  |
| main-aa | main | 2516e2786 | fetched | 98c2368a774d |  |
| impl-enginelink | research/rust-core-engine-link | 2a3bb96ed | built (fork 7c7b600b1, wasm32-eh) | c3652e01518f |  |
| plugins-enginelink | research/rust-kernel-engine-link | 48fda657e | built (fork 7c7b600b1, wasm32-eh) | c3652e01518f | d6276d5fb70d |
| impl-fixed | research/multi-platform-rust-core-impl | 29f02d381 | built (fork 7c7b600b1, wasm32-eh) | ee06b8271360 |  |
| plugins-fixed | research/multi-platform-rust-core-impl-plugins | c92019192 | built (fork 7c7b600b1, wasm32-eh) | ee06b8271360 | d6276d5fb70d |

## Gate

- `main`: pass
- `main-aa`: pass
- `impl-enginelink`: pass
- `plugins-enginelink`: pass
- `impl-fixed`: pass
- `plugins-fixed`: pass

## Sizes

| Metric | main | main-aa | impl-enginelink | plugins-enginelink | impl-fixed | plugins-fixed |
| --- | --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.63 MiB | 9.63 MiB | 10.60 MiB | 10.60 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 7.93 MiB | 0.00 MiB | 7.93 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 1.70 MiB | 0.00 MiB | 1.70 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 3.34 MiB | 2.61 MiB | 3.36 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.95 MiB | 0.79 MiB | 0.96 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

39 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.5 [54.3] | 52.4 [53.7] | 0.998 [0.954, 1.042] | pass |
| open(ebook) / openedMs | 116.2 [117.0] | 114.6 [116.5] | 0.986 [0.975, 1.019] | pass |
| open(ebook) / firstPaintMs | 218.5 [219.1] | 213.8 [216.1] | 0.979 [0.968, 1.052] | pass |
| open(ebook) / settledMs | 337.1 [339.2] | 333.8 [336.7] | 0.990 [0.980, 1.022] | pass |
| open(ebook) / mainTaskMs | 118.3 [119.9] | 116.9 [117.3] | 0.989 [0.968, 1.002] | pass |
| open(ebook) / mainJsMs | 91.9 [93.0] | 90.6 [91.5] | 0.986 [0.972, 1.010] | pass |
| open(large) / readyMs | 52.5 [53.4] | 53.3 [54.1] | 1.014 [0.988, 1.052] | pass |
| open(large) / openedMs | 134.1 [134.7] | 136.2 [136.4] | 1.016 [0.999, 1.019] | pass |
| open(large) / firstPaintMs | 222.6 [225.1] | 226.2 [229.2] | 1.016 [0.998, 1.040] | pass |
| open(large) / settledMs | 220.9 [227.5] | 224.8 [228.9] | 1.018 [0.979, 1.053] | pass |
| open(large) / mainTaskMs | 260.6 [269.3] | 263.1 [264.3] | 1.009 [0.973, 1.016] | pass |
| open(large) / mainJsMs | 237.3 [246.2] | 239.6 [240.8] | 1.010 [0.966, 1.016] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2008.1 [2015.8] | 2016.5 [2027.4] | 1.004 [0.993, 1.014] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [8.4] | 8.7 [8.9] | 1.028 [1.025, 1.058] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 404.4 [405.4] | 401.7 [406.3] | 0.993 [0.969, 1.053] | pass |
| scroll / mainJsMs | 313.3 [313.4] | 310.7 [313.7] | 0.992 [0.964, 1.059] | pass |
| jump / settleMs | 97.4 [97.8] | 96.6 [100.0] | -0.8 [-5.2, +9.4] | pass |
| jump / mainTaskMs | 59.3 [59.3] | 53.5 [58.5] | 0.901 [0.878, 1.090] | pass |
| jump / mainJsMs | 43.9 [43.9] | 40.1 [43.0] | 0.914 [0.887, 1.004] | pass |
| zoom / settleMs | 380.4 [414.1] | 385.3 [398.6] | +4.9 [-37.0, +31.9] | pass |
| zoom / zoom2SettleMs | 132.5 [132.7] | 133.7 [136.7] | +1.2 [-0.9, +7.4] | pass |
| zoom / zoom4SettleMs | 250.9 [281.7] | 251.5 [266.6] | +0.7 [-41.2, +32.6] | pass |
| zoom / backTo1SettleMs | 69.8 [86.8] | 74.7 [86.9] | +4.9 [-17.8, +19.6] | pass |
| zoom / mainTaskMs | 176.1 [184.6] | 182.0 [185.6] | 1.034 [0.967, 1.068] | pass |
| zoom / mainJsMs | 128.0 [136.8] | 133.4 [135.4] | 1.042 [0.954, 1.080] | pass |
| select / settleMs | 147.5 [148.7] | 147.4 [148.2] | -0.1 [-2.2, +0.8] | pass |
| select / mainTaskMs | 42.6 [44.5] | 39.5 [42.1] | 0.928 [0.878, 1.111] | pass |
| select / mainJsMs | 26.7 [27.4] | 24.4 [26.2] | 0.913 [0.872, 1.136] | pass |
| search / searchMs | 58.8 [64.2] | 49.7 [61.6] | 0.845 [0.740, 1.211] | pass |
| search / mainTaskMs | 4.7 [5.1] | 4.4 [4.7] | 0.929 [0.803, 1.043] | pass |
| search / mainJsMs | 3.1 [3.2] | 3.0 [3.2] | 0.958 [0.938, 1.091] | pass |
| annotate / settleMs | 161.8 [162.0] | 160.7 [161.1] | -1.2 [-9.1, +3.3] | pass |
| annotate / mainTaskMs | 57.4 [59.4] | 56.1 [56.1] | 0.977 [0.929, 1.034] | pass |
| annotate / mainJsMs | 33.5 [34.7] | 34.2 [35.1] | 1.022 [0.953, 1.090] | pass |

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

Not run: `impl-head`.

## `plugins-tip` vs `plugins-enginelink`

Not run: `plugins-tip`.

## `impl-head` vs `impl-fixed`

Not run: `impl-head`.

## `plugins-tip` vs `plugins-fixed`

Not run: `plugins-tip`.

## `impl-fixed` vs `impl-enginelink`

| Row | impl-fixed median [p95] | impl-enginelink median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.9 [53.3] | 52.0 [56.6] | 0.982 [0.960, 1.095] | no detectable difference |
| open(ebook) / openedMs | 120.4 [123.5] | 115.8 [118.9] | 0.961 [0.923, 1.006] | no detectable difference |
| open(ebook) / firstPaintMs | 217.3 [224.0] | 218.2 [219.2] | 1.004 [0.944, 1.026] | no detectable difference |
| open(ebook) / settledMs | 265.4 [271.5] | 264.4 [266.1] | 0.997 [0.953, 1.013] | no detectable difference |
| open(ebook) / mainTaskMs | 105.2 [106.0] | 106.7 [108.9] | 1.014 [1.002, 1.062] | no detectable difference |
| open(ebook) / mainJsMs | 81.4 [81.6] | 82.5 [83.5] | 1.013 [1.001, 1.055] | no detectable difference |
| open(large) / readyMs | 52.1 [56.0] | 52.8 [55.1] | 1.014 [0.907, 1.065] | no detectable difference |
| open(large) / openedMs | 143.4 [147.8] | 135.3 [138.2] | 0.944 [0.905, 0.984] | faster |
| open(large) / firstPaintMs | 227.9 [229.4] | 225.5 [225.6] | 0.990 [0.957, 1.022] | no detectable difference |
| open(large) / settledMs | 223.7 [225.1] | 222.1 [222.8] | 0.993 [0.973, 1.032] | no detectable difference |
| open(large) / mainTaskMs | 161.8 [162.7] | 163.0 [164.1] | 1.007 [0.992, 1.022] | no detectable difference |
| open(large) / mainJsMs | 136.1 [136.3] | 136.3 [137.2] | 1.001 [0.993, 1.019] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2014.3 [2014.8] | 2006.4 [2015.5] | 0.996 [0.987, 1.009] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.6] | 9.2 [9.3] | 1.093 [0.972, 1.107] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 330.4 [335.3] | 331.0 [340.1] | 1.002 [0.927, 1.035] | no detectable difference |
| scroll / mainJsMs | 228.7 [233.1] | 227.7 [234.7] | 0.996 [0.922, 1.040] | no detectable difference |
| jump / settleMs | 88.1 [89.0] | 82.2 [88.8] | -5.9 [-8.8, +5.3] | no detectable difference |
| jump / mainTaskMs | 57.6 [59.3] | 49.4 [52.4] | 0.857 [0.808, 0.919] | faster |
| jump / mainJsMs | 34.5 [35.6] | 33.7 [34.4] | 0.975 [0.892, 1.016] | no detectable difference |
| zoom / settleMs | 379.3 [380.7] | 378.0 [378.7] | -1.3 [-23.5, +1.5] | no detectable difference |
| zoom / zoom2SettleMs | 145.8 [145.9] | 137.2 [140.7] | -8.6 [-15.3, -1.1] | faster |
| zoom / zoom4SettleMs | 235.0 [235.1] | 237.0 [241.1] | +1.9 [-8.3, +8.2] | no detectable difference |
| zoom / backTo1SettleMs | 62.0 [68.6] | 68.1 [68.7] | +6.1 [-17.1, +6.9] | no detectable difference |
| zoom / mainTaskMs | 144.7 [146.3] | 137.8 [146.5] | 0.952 [0.925, 1.021] | no detectable difference |
| zoom / mainJsMs | 96.6 [98.4] | 95.9 [101.9] | 0.993 [0.956, 1.068] | no detectable difference |
| select / settleMs | 150.1 [150.8] | 149.7 [152.3] | -0.4 [-5.1, +10.8] | no detectable difference |
| select / mainTaskMs | 37.7 [39.7] | 38.6 [44.5] | 1.022 [0.850, 1.275] | no detectable difference |
| select / mainJsMs | 19.0 [19.7] | 19.4 [22.7] | 1.017 [0.856, 1.287] | no detectable difference |
| search / searchMs | 50.5 [55.8] | 52.6 [54.9] | 1.041 [0.837, 1.263] | no detectable difference |
| search / mainTaskMs | 2.6 [2.7] | 2.6 [2.6] | 0.974 [0.918, 1.142] | no detectable difference |
| search / mainJsMs | 1.2 [1.2] | 1.2 [1.2] | 1.029 [0.922, 1.077] | no detectable difference |
| annotate / settleMs | 155.8 [158.9] | 156.3 [156.5] | +0.5 [-6.3, +4.0] | no detectable difference |
| annotate / mainTaskMs | 46.3 [50.3] | 48.7 [49.1] | 1.051 [0.900, 1.076] | no detectable difference |
| annotate / mainJsMs | 22.7 [23.5] | 23.3 [23.6] | 1.026 [0.934, 1.082] | no detectable difference |

## `plugins-fixed` vs `plugins-enginelink`

| Row | plugins-fixed median [p95] | plugins-enginelink median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 75.2 [79.4] | 77.5 [78.2] | 1.031 [0.941, 1.045] | no detectable difference |
| open(ebook) / openedMs | 178.0 [179.8] | 172.8 [174.6] | 0.971 [0.950, 1.000] | no detectable difference |
| open(ebook) / firstPaintMs | 301.6 [307.0] | 298.8 [304.1] | 0.991 [0.965, 1.022] | no detectable difference |
| open(ebook) / settledMs | 354.8 [355.5] | 347.7 [352.5] | 0.980 [0.972, 1.015] | no detectable difference |
| open(ebook) / mainTaskMs | 235.9 [239.8] | 235.2 [236.0] | 0.997 [0.971, 1.003] | no detectable difference |
| open(ebook) / mainJsMs | 198.4 [201.4] | 196.3 [198.1] | 0.989 [0.971, 1.009] | no detectable difference |
| open(large) / readyMs | 75.6 [76.0] | 76.3 [76.6] | 1.009 [0.973, 1.029] | no detectable difference |
| open(large) / openedMs | 204.0 [205.5] | 196.3 [197.2] | 0.962 [0.947, 0.979] | faster |
| open(large) / firstPaintMs | 334.0 [334.8] | 330.5 [333.9] | 0.990 [0.978, 1.004] | no detectable difference |
| open(large) / settledMs | 343.4 [343.8] | 336.3 [337.0] | 0.979 [0.966, 0.985] | no detectable difference |
| open(large) / mainTaskMs | 527.2 [528.4] | 526.5 [531.9] | 0.999 [0.984, 1.014] | no detectable difference |
| open(large) / mainJsMs | 488.6 [490.0] | 491.2 [495.7] | 1.005 [0.989, 1.016] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2261.4 [2309.6] | 2278.5 [2280.0] | 1.008 [0.937, 1.017] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.5 [9.7] | 9.7 [9.8] | 1.016 [0.963, 1.040] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 762.5 [777.3] | 762.6 [773.9] | 1.000 [0.910, 1.033] | no detectable difference |
| scroll / mainJsMs | 659.0 [670.6] | 657.1 [664.9] | 0.997 [0.906, 1.028] | no detectable difference |
| jump / settleMs | 146.1 [147.8] | 142.5 [156.7] | -3.7 [-39.5, +43.9] | no detectable difference |
| jump / mainTaskMs | 145.9 [146.0] | 143.5 [143.9] | 0.983 [0.934, 0.992] | no detectable difference |
| jump / mainJsMs | 131.1 [131.1] | 128.8 [130.2] | 0.983 [0.971, 1.001] | no detectable difference |
| zoom / settleMs | 423.9 [439.8] | 429.1 [450.3] | +5.1 [-27.1, +34.0] | no detectable difference |
| zoom / zoom2SettleMs | 151.9 [152.9] | 150.5 [157.9] | -1.4 [-2.5, +8.5] | no detectable difference |
| zoom / zoom4SettleMs | 271.0 [289.2] | 278.6 [292.4] | +7.6 [-27.4, +27.1] | no detectable difference |
| zoom / backTo1SettleMs | 102.3 [104.8] | 89.7 [103.3] | -12.6 [-22.1, +18.6] | no detectable difference |
| zoom / mainTaskMs | 274.2 [274.3] | 272.0 [287.2] | 0.992 [0.976, 1.089] | no detectable difference |
| zoom / mainJsMs | 228.5 [229.1] | 225.4 [242.9] | 0.986 [0.963, 1.094] | no detectable difference |
| select / settleMs | 144.9 [147.1] | 144.9 [144.9] | +0.0 [-2.7, +3.2] | no detectable difference |
| select / mainTaskMs | 67.2 [69.4] | 66.3 [69.9] | 0.986 [0.915, 1.060] | no detectable difference |
| select / mainJsMs | 53.2 [54.5] | 52.7 [54.3] | 0.991 [0.926, 1.040] | no detectable difference |
| search / searchMs | 68.3 [69.6] | 64.1 [64.7] | 0.938 [0.905, 1.101] | no detectable difference |
| search / mainTaskMs | 15.5 [18.7] | 12.5 [15.5] | 0.804 [0.498, 1.841] | no detectable difference |
| search / mainJsMs | 7.5 [8.2] | 7.0 [7.6] | 0.941 [0.796, 1.234] | no detectable difference |
| annotate / settleMs | 165.3 [165.8] | 167.0 [167.6] | +1.7 [-0.8, +6.0] | no detectable difference |
| annotate / mainTaskMs | 79.3 [84.5] | 83.1 [85.2] | 1.048 [0.948, 1.082] | no detectable difference |
| annotate / mainJsMs | 59.5 [63.0] | 61.3 [63.8] | 1.031 [0.943, 1.079] | no detectable difference |

## Memory

| Row | main median [p95] | main-aa median [p95] | impl-enginelink median [p95] | plugins-enginelink median [p95] | impl-fixed median [p95] | plugins-fixed median [p95] |
| --- | --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.34 MiB [34.35 MiB] | 34.62 MiB [34.62 MiB] | 42.12 MiB [42.12 MiB] | 34.94 MiB [34.95 MiB] | 41.44 MiB [41.44 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 22.77 MiB [22.77 MiB] | 22.77 MiB [22.77 MiB] | 23.06 MiB [23.06 MiB] | 23.06 MiB [23.06 MiB] |
| open(large) / heapAfterOpenBytes | 48.16 MiB [48.16 MiB] | 48.15 MiB [48.16 MiB] | 48.31 MiB [48.31 MiB] | 55.94 MiB [55.94 MiB] | 48.62 MiB [48.63 MiB] | 56.27 MiB [56.28 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.77 MiB [22.77 MiB] | 22.71 MiB [22.71 MiB] | 23.03 MiB [23.03 MiB] | 22.99 MiB [22.99 MiB] |
| soak / heapCycle5Bytes | 37.42 MiB [37.43 MiB] | 37.43 MiB [37.43 MiB] | 37.77 MiB [37.78 MiB] | 44.93 MiB [44.94 MiB] | 38.09 MiB [38.10 MiB] | 45.25 MiB [45.26 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.27 MiB] | 38.27 MiB [38.27 MiB] | 38.49 MiB [38.50 MiB] | 45.47 MiB [45.49 MiB] | 38.81 MiB [38.83 MiB] | 45.80 MiB [45.82 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-enginelink`:

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

`plugins-fixed`:

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

