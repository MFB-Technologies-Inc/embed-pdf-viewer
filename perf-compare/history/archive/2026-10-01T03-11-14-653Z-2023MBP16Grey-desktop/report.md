# Viewer perf comparison: 2026-10-01T03-11-14-653Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 87a569bdf55d1d0cd371797befc55a42021c0431 (uncommitted changes) |
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
| impl-head | research/multi-platform-rust-core-impl | 485d0b893 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 |  |
| impl-enginelink | research/rust-core-engine-link | 7ed02e399 | built (fork d045f389d, wasm32-eh) | 484daac6bf4a |  |
| plugins-tip | research/multi-platform-rust-core-impl-plugins | e981e0cb9 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | d6276d5fb70d |
| plugins-enginelink | research/rust-kernel-engine-link | dfb5c43c8 | built (fork d045f389d, wasm32-eh) | 484daac6bf4a | d6276d5fb70d |

## Gate

- `main`: pass
- `main-aa`: pass
- `impl-head`: pass
- `impl-enginelink`: pass
- `plugins-tip`: pass
- `plugins-enginelink`: pass

## Sizes

| Metric | main | main-aa | impl-head | impl-enginelink | plugins-tip | plugins-enginelink |
| --- | --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 9.60 MiB | 10.57 MiB | 9.60 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.82 MiB | 2.83 MiB | 2.82 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 0.00 MiB | 7.93 MiB | 7.93 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 0.00 MiB | 1.70 MiB | 1.70 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 3.35 MiB | 3.33 MiB | 2.62 MiB | 2.60 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.96 MiB | 0.95 MiB | 0.80 MiB | 0.79 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

38 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.7 [53.0] | 52.4 [52.7] | 0.994 [0.979, 1.010] | pass |
| open(ebook) / openedMs | 114.6 [115.1] | 115.4 [116.1] | 1.007 [0.993, 1.015] | pass |
| open(ebook) / firstPaintMs | 215.7 [216.5] | 213.4 [214.6] | 0.990 [0.980, 1.023] | pass |
| open(ebook) / settledMs | 336.0 [337.0] | 333.9 [334.0] | 0.994 [0.989, 1.014] | pass |
| open(ebook) / mainTaskMs | 118.4 [119.3] | 117.7 [120.5] | 0.994 [0.977, 1.040] | pass |
| open(ebook) / mainJsMs | 92.8 [93.8] | 91.1 [93.4] | 0.982 [0.966, 1.042] | pass |
| open(large) / readyMs | 52.2 [54.7] | 53.4 [53.5] | 1.024 [0.918, 1.035] | pass |
| open(large) / openedMs | 132.2 [135.0] | 134.3 [134.7] | 1.016 [0.974, 1.032] | pass |
| open(large) / firstPaintMs | 224.4 [227.1] | 223.8 [225.1] | 0.997 [0.963, 1.054] | pass |
| open(large) / settledMs | 221.4 [223.8] | 220.8 [221.9] | 0.997 [0.967, 1.055] | pass |
| open(large) / mainTaskMs | 260.9 [261.7] | 261.4 [262.5] | 1.002 [0.998, 1.011] | pass |
| open(large) / mainJsMs | 237.5 [238.2] | 238.7 [239.3] | 1.005 [1.001, 1.010] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2028.9 [2032.8] | 2012.2 [2041.8] | 0.992 [0.984, 1.023] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [8.6] | 8.5 [9.2] | 1.005 [0.975, 1.102] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 403.3 [405.3] | 397.5 [399.6] | 0.986 [0.942, 1.021] | pass |
| scroll / mainJsMs | 312.1 [313.4] | 303.0 [306.1] | 0.971 [0.934, 1.015] | pass |
| jump / settleMs | 96.8 [99.0] | 97.9 [100.2] | +1.2 [-1.7, +7.3] | pass |
| jump / mainTaskMs | 58.1 [59.4] | 56.9 [58.5] | 0.979 [0.929, 1.079] | pass |
| jump / mainJsMs | 42.3 [43.3] | 41.8 [43.2] | 0.989 [0.951, 1.080] | pass |
| zoom / settleMs | 387.7 [393.0] | 398.5 [404.9] | +10.7 [+3.7, +20.7] | pass |
| zoom / zoom2SettleMs | 133.0 [133.2] | 132.5 [132.6] | -0.4 [-1.3, +7.5] | pass |
| zoom / zoom4SettleMs | 260.7 [262.4] | 265.9 [272.4] | +5.2 [+2.8, +21.4] | pass |
| zoom / backTo1SettleMs | 81.3 [84.3] | 67.3 [89.1] | -14.0 [-28.3, +14.2] | pass |
| zoom / mainTaskMs | 176.9 [181.4] | 177.6 [178.2] | 1.004 [0.909, 1.014] | pass |
| zoom / mainJsMs | 129.5 [134.2] | 127.6 [128.5] | 0.985 [0.883, 1.010] | pass |
| select / settleMs | 146.9 [154.7] | 148.9 [149.1] | +2.0 [-8.7, +2.9] | pass |
| select / mainTaskMs | 42.8 [43.3] | 46.1 [46.3] | 1.076 [1.022, 1.223] | A/A failed |
| select / mainJsMs | 26.6 [27.3] | 29.3 [29.5] | 1.101 [0.997, 1.289] | pass |
| search / searchMs | 56.7 [57.0] | 53.9 [61.8] | 0.950 [0.879, 1.124] | pass |
| search / mainTaskMs | 4.6 [4.9] | 4.6 [5.1] | 0.990 [0.840, 1.117] | pass |
| search / mainJsMs | 3.2 [3.2] | 3.1 [3.2] | 0.966 [0.903, 1.041] | pass |
| annotate / settleMs | 153.2 [158.9] | 161.0 [161.3] | +7.7 [-6.2, +8.6] | pass |
| annotate / mainTaskMs | 55.0 [57.6] | 56.1 [57.4] | 1.020 [0.918, 1.104] | pass |
| annotate / mainJsMs | 32.9 [33.9] | 33.6 [34.4] | 1.019 [0.932, 1.097] | pass |

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

| Row | impl-head median [p95] | impl-enginelink median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.7 [53.7] | 52.2 [53.9] | 0.973 [0.963, 1.034] | no detectable difference |
| open(ebook) / openedMs | 121.6 [121.8] | 114.3 [116.1] | 0.940 [0.923, 0.976] | faster |
| open(ebook) / firstPaintMs | 217.9 [218.7] | 214.0 [217.8] | 0.982 [0.963, 1.006] | no detectable difference |
| open(ebook) / settledMs | 265.9 [266.6] | 258.5 [265.8] | 0.972 [0.961, 1.007] | no detectable difference |
| open(ebook) / mainTaskMs | 103.1 [103.9] | 106.1 [106.2] | 1.029 [1.013, 1.039] | no detectable difference |
| open(ebook) / mainJsMs | 79.6 [79.8] | 81.9 [82.1] | 1.029 [1.016, 1.053] | no detectable difference |
| open(large) / readyMs | 52.2 [52.7] | 53.4 [53.4] | 1.022 [0.992, 1.040] | no detectable difference |
| open(large) / openedMs | 140.0 [141.0] | 134.8 [135.6] | 0.963 [0.951, 0.973] | faster |
| open(large) / firstPaintMs | 226.5 [230.4] | 215.7 [217.8] | 0.952 [0.931, 0.969] | no detectable difference |
| open(large) / settledMs | 226.1 [228.1] | 211.3 [213.4] | 0.934 [0.921, 0.961] | faster |
| open(large) / mainTaskMs | 161.7 [162.2] | 160.4 [162.0] | 0.992 [0.984, 1.014] | no detectable difference |
| open(large) / mainJsMs | 135.4 [136.1] | 134.1 [135.3] | 0.990 [0.983, 1.010] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2019.3 [2025.3] | 2014.5 [2015.8] | 0.998 [0.990, 0.998] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.001] | no detectable difference |
| scroll / frameP95Ms | 8.7 [8.9] | 8.5 [8.6] | 0.978 [0.949, 1.022] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 332.8 [337.5] | 336.5 [357.5] | 1.011 [0.983, 1.104] | no detectable difference |
| scroll / mainJsMs | 231.2 [232.4] | 237.4 [244.7] | 1.027 [0.982, 1.092] | no detectable difference |
| jump / settleMs | 87.6 [89.1] | 89.4 [92.0] | +1.8 [-0.6, +5.0] | no detectable difference |
| jump / mainTaskMs | 58.8 [59.1] | 53.3 [54.4] | 0.907 [0.849, 1.075] | no detectable difference |
| jump / mainJsMs | 34.3 [34.8] | 33.4 [34.8] | 0.973 [0.923, 1.107] | no detectable difference |
| zoom / settleMs | 371.9 [380.5] | 376.5 [383.0] | +4.6 [-15.2, +16.7] | no detectable difference |
| zoom / zoom2SettleMs | 138.5 [140.8] | 140.0 [141.0] | +1.4 [-8.2, +10.3] | no detectable difference |
| zoom / zoom4SettleMs | 236.3 [242.3] | 242.6 [243.6] | +6.4 [-16.6, +12.9] | no detectable difference |
| zoom / backTo1SettleMs | 65.4 [66.3] | 68.0 [78.0] | +2.6 [-3.0, +17.8] | no detectable difference |
| zoom / mainTaskMs | 141.5 [146.2] | 147.0 [148.3] | 1.039 [0.939, 1.081] | no detectable difference |
| zoom / mainJsMs | 95.5 [98.6] | 100.5 [101.2] | 1.053 [0.949, 1.114] | no detectable difference |
| select / settleMs | 149.9 [150.1] | 149.1 [150.3] | -0.8 [-2.0, +3.8] | no detectable difference |
| select / mainTaskMs | 39.3 [39.4] | 35.0 [37.5] | 0.891 [0.882, 1.053] | unreliable (A/A failed) |
| select / mainJsMs | 20.4 [20.6] | 17.7 [18.9] | 0.869 [0.835, 1.057] | no detectable difference |
| search / searchMs | 54.5 [56.2] | 54.6 [55.1] | 1.001 [0.946, 1.014] | no detectable difference |
| search / mainTaskMs | 2.5 [2.7] | 3.3 [3.5] | 1.284 [1.112, 1.443] | slower |
| search / mainJsMs | 1.2 [1.2] | 1.3 [1.3] | 1.118 [0.972, 1.172] | no detectable difference |
| annotate / settleMs | 152.6 [159.4] | 159.2 [160.2] | +6.7 [-1.3, +8.3] | no detectable difference |
| annotate / mainTaskMs | 46.9 [47.1] | 50.1 [52.5] | 1.067 [0.951, 1.150] | no detectable difference |
| annotate / mainJsMs | 22.5 [22.9] | 24.3 [26.6] | 1.076 [0.987, 1.220] | no detectable difference |

## `plugins-tip` vs `plugins-enginelink`

| Row | plugins-tip median [p95] | plugins-enginelink median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 74.1 [76.6] | 75.3 [76.0] | 1.016 [0.965, 1.038] | no detectable difference |
| open(ebook) / openedMs | 174.5 [177.9] | 170.8 [173.5] | 0.979 [0.948, 1.005] | no detectable difference |
| open(ebook) / firstPaintMs | 308.6 [309.7] | 297.0 [301.4] | 0.962 [0.948, 1.002] | no detectable difference |
| open(ebook) / settledMs | 356.0 [357.3] | 344.0 [350.1] | 0.966 [0.960, 1.011] | no detectable difference |
| open(ebook) / mainTaskMs | 235.0 [236.4] | 234.3 [239.6] | 0.997 [0.983, 1.028] | no detectable difference |
| open(ebook) / mainJsMs | 196.0 [197.7] | 195.9 [200.3] | 0.999 [0.989, 1.025] | no detectable difference |
| open(large) / readyMs | 74.5 [75.6] | 74.8 [76.0] | 1.004 [0.981, 1.025] | no detectable difference |
| open(large) / openedMs | 201.8 [202.5] | 195.4 [198.5] | 0.968 [0.958, 0.998] | no detectable difference |
| open(large) / firstPaintMs | 332.2 [334.9] | 328.5 [332.3] | 0.989 [0.963, 1.010] | no detectable difference |
| open(large) / settledMs | 336.4 [344.5] | 338.2 [342.7] | 1.005 [0.962, 1.034] | no detectable difference |
| open(large) / mainTaskMs | 523.1 [530.7] | 530.0 [534.4] | 1.013 [0.984, 1.025] | no detectable difference |
| open(large) / mainJsMs | 486.1 [494.7] | 492.8 [497.4] | 1.014 [0.982, 1.025] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2240.7 [2259.9] | 2244.7 [2259.3] | 1.002 [0.985, 1.023] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.001] | no detectable difference |
| scroll / frameP95Ms | 9.3 [9.4] | 9.4 [9.5] | 1.012 [0.935, 1.100] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 734.5 [747.8] | 756.9 [761.2] | 1.030 [1.004, 1.044] | no detectable difference |
| scroll / mainJsMs | 633.3 [646.8] | 654.5 [655.8] | 1.033 [1.001, 1.040] | no detectable difference |
| jump / settleMs | 130.3 [142.6] | 113.3 [149.9] | -17.0 [-32.3, +40.2] | no detectable difference |
| jump / mainTaskMs | 147.5 [148.6] | 142.1 [143.8] | 0.964 [0.943, 0.990] | no detectable difference |
| jump / mainJsMs | 130.3 [134.9] | 127.3 [128.0] | 0.976 [0.932, 0.993] | no detectable difference |
| zoom / settleMs | 435.8 [441.7] | 441.7 [447.5] | +5.9 [-7.9, +37.5] | no detectable difference |
| zoom / zoom2SettleMs | 149.0 [155.0] | 150.8 [151.0] | +1.7 [-5.2, +8.7] | no detectable difference |
| zoom / zoom4SettleMs | 286.7 [286.8] | 290.7 [296.8] | +4.1 [-2.9, +29.0] | no detectable difference |
| zoom / backTo1SettleMs | 103.4 [103.6] | 104.7 [106.2] | +1.3 [-24.7, +16.7] | no detectable difference |
| zoom / mainTaskMs | 277.1 [283.8] | 278.4 [283.0] | 1.005 [0.894, 1.029] | no detectable difference |
| zoom / mainJsMs | 232.5 [237.0] | 234.0 [235.7] | 1.007 [0.906, 1.029] | no detectable difference |
| select / settleMs | 143.2 [152.6] | 143.9 [144.6] | +0.7 [-12.3, +3.6] | no detectable difference |
| select / mainTaskMs | 67.9 [70.3] | 62.6 [67.1] | 0.921 [0.881, 1.074] | unreliable (A/A failed) |
| select / mainJsMs | 53.6 [55.5] | 49.6 [53.9] | 0.924 [0.884, 1.082] | no detectable difference |
| search / searchMs | 58.6 [67.6] | 67.2 [67.7] | 1.146 [0.970, 1.174] | no detectable difference |
| search / mainTaskMs | 9.0 [15.3] | 17.1 [18.4] | 1.900 [1.044, 2.090] | slower |
| search / mainJsMs | 6.2 [7.3] | 7.5 [8.4] | 1.223 [1.016, 1.398] | slower |
| annotate / settleMs | 165.9 [168.7] | 164.8 [165.2] | -1.0 [-5.1, +1.9] | no detectable difference |
| annotate / mainTaskMs | 83.0 [86.6] | 83.0 [83.8] | 1.000 [0.910, 1.033] | no detectable difference |
| annotate / mainJsMs | 62.2 [65.0] | 62.8 [63.0] | 1.011 [0.906, 1.049] | no detectable difference |

## `impl-head` vs `impl-fixed`

Not run: `impl-fixed`.

## `plugins-tip` vs `plugins-fixed`

Not run: `plugins-fixed`.

## Memory

| Row | main median [p95] | main-aa median [p95] | impl-head median [p95] | impl-enginelink median [p95] | plugins-tip median [p95] | plugins-enginelink median [p95] |
| --- | --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.34 MiB [34.34 MiB] | 34.88 MiB [34.89 MiB] | 34.55 MiB [34.55 MiB] | 41.37 MiB [41.38 MiB] | 42.06 MiB [42.07 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.01 MiB [23.01 MiB] | 22.73 MiB [22.73 MiB] | 23.02 MiB [23.02 MiB] | 22.73 MiB [22.73 MiB] |
| open(large) / heapAfterOpenBytes | 48.16 MiB [48.18 MiB] | 48.15 MiB [48.15 MiB] | 48.56 MiB [48.56 MiB] | 48.25 MiB [48.25 MiB] | 56.21 MiB [56.21 MiB] | 55.89 MiB [55.89 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.28 MiB] | 22.26 MiB [22.26 MiB] | 22.99 MiB [22.99 MiB] | 22.73 MiB [22.73 MiB] | 22.95 MiB [22.95 MiB] | 22.67 MiB [22.67 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.42 MiB] | 37.42 MiB [37.42 MiB] | 38.03 MiB [38.03 MiB] | 37.72 MiB [37.72 MiB] | 44.18 MiB [44.19 MiB] | 44.87 MiB [44.88 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.27 MiB] | 38.27 MiB [38.28 MiB] | 38.76 MiB [38.76 MiB] | 38.43 MiB [38.44 MiB] | 44.74 MiB [44.74 MiB] | 45.42 MiB [45.42 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-tip`:

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

