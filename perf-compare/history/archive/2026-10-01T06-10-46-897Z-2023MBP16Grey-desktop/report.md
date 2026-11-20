# Viewer perf comparison: 2026-10-01T06-10-46-897Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | ceec6132ab8d77d579b2df957303068ab898aa45 |
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
| impl-fixed | research/multi-platform-rust-core-impl | 29f02d381 | built (fork 7c7b600b1, wasm32-eh) | ee06b8271360 |  |
| plugins-fixed | research/multi-platform-rust-core-impl-plugins | c92019192 | built (fork 7c7b600b1, wasm32-eh) | ee06b8271360 | d6276d5fb70d |
| main-compat | main | 2516e2786 | fetched | 98c2368a774d |  |
| impl-compat | research/multi-platform-rust-core-impl | 29f02d381 | built (fork 7c7b600b1, wasm32-eh) | ee06b8271360 |  |
| plugins-compat | research/multi-platform-rust-core-impl-plugins | c92019192 | built (fork 7c7b600b1, wasm32-eh) | ee06b8271360 | d6276d5fb70d |

## Gate

- `main`: pass
- `main-aa`: pass
- `impl-fixed`: pass
- `plugins-fixed`: pass
- `main-compat`: pass
- `impl-compat`: pass
- `plugins-compat`: pass

## Sizes

| Metric | main | main-aa | impl-fixed | plugins-fixed | main-compat | impl-compat | plugins-compat |
| --- | --- | --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.60 MiB | 10.60 MiB | 7.19 MiB | 10.60 MiB | 10.60 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 7.93 MiB | 0.00 MiB | 0.00 MiB | 7.93 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 0.00 MiB | 1.70 MiB | 0.00 MiB | 0.00 MiB | 1.70 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 3.36 MiB | 2.63 MiB | 3.37 MiB | 3.36 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.96 MiB | 0.96 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

39 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.3 [53.5] | 52.8 [53.2] | 0.990 [0.978, 1.018] | pass |
| open(ebook) / openedMs | 116.4 [118.5] | 116.4 [116.8] | 1.000 [0.964, 1.016] | pass |
| open(ebook) / firstPaintMs | 217.8 [221.0] | 213.4 [215.5] | 0.980 [0.962, 1.042] | pass |
| open(ebook) / settledMs | 337.3 [342.6] | 335.0 [336.4] | 0.993 [0.971, 1.015] | pass |
| open(ebook) / mainTaskMs | 119.0 [119.9] | 119.8 [120.6] | 1.007 [0.998, 1.036] | pass |
| open(ebook) / mainJsMs | 92.6 [94.1] | 93.1 [94.8] | 1.006 [0.981, 1.053] | pass |
| open(large) / readyMs | 52.6 [53.0] | 53.0 [54.3] | 1.007 [0.990, 1.047] | pass |
| open(large) / openedMs | 133.5 [133.8] | 134.1 [135.4] | 1.004 [0.991, 1.016] | pass |
| open(large) / firstPaintMs | 221.7 [224.9] | 223.1 [224.2] | 1.006 [0.987, 1.015] | pass |
| open(large) / settledMs | 218.4 [222.1] | 220.1 [221.0] | 1.008 [0.987, 1.014] | pass |
| open(large) / mainTaskMs | 262.7 [263.3] | 260.7 [260.9] | 0.993 [0.987, 0.997] | pass |
| open(large) / mainJsMs | 239.3 [239.4] | 237.6 [238.3] | 0.993 [0.988, 1.001] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2015.0 [2037.8] | 2006.9 [2014.4] | 0.996 [0.979, 1.006] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.6 [8.8] | 8.7 [9.0] | 1.015 [0.961, 1.059] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 412.9 [415.2] | 396.9 [407.6] | 0.961 [0.916, 1.017] | pass |
| scroll / mainJsMs | 321.4 [322.7] | 307.1 [316.2] | 0.956 [0.911, 1.020] | pass |
| jump / settleMs | 94.6 [95.4] | 92.3 [99.6] | -2.3 [-4.5, +7.5] | pass |
| jump / mainTaskMs | 56.2 [56.9] | 54.1 [56.4] | 0.964 [0.944, 1.041] | pass |
| jump / mainJsMs | 42.8 [44.5] | 40.7 [45.1] | 0.951 [0.895, 1.100] | pass |
| zoom / settleMs | 339.5 [360.8] | 335.8 [339.2] | -3.7 [-31.5, +2.8] | pass |
| zoom / zoom2SettleMs | 124.8 [128.5] | 124.5 [124.9] | -0.3 [-4.7, +6.8] | pass |
| zoom / zoom4SettleMs | 212.0 [241.7] | 210.8 [214.8] | -1.1 [-37.8, +4.8] | pass |
| zoom / backTo1SettleMs | 78.9 [85.6] | 67.1 [79.2] | -11.8 [-24.0, +18.8] | pass |
| zoom / mainTaskMs | 168.3 [170.7] | 168.2 [170.9] | 0.999 [0.967, 1.018] | pass |
| zoom / mainJsMs | 121.8 [125.1] | 121.3 [122.0] | 0.995 [0.947, 1.003] | pass |
| select / settleMs | 147.6 [148.6] | 148.1 [148.8] | +0.5 [-0.7, +1.3] | pass |
| select / mainTaskMs | 43.5 [48.0] | 43.4 [45.8] | 0.997 [0.860, 1.061] | pass |
| select / mainJsMs | 27.3 [30.7] | 26.9 [28.4] | 0.986 [0.825, 1.069] | pass |
| search / searchMs | 50.2 [54.5] | 59.0 [65.1] | 1.176 [0.885, 1.376] | pass |
| search / mainTaskMs | 4.2 [4.6] | 4.5 [4.6] | 1.060 [0.947, 1.127] | pass |
| search / mainJsMs | 3.1 [3.1] | 3.1 [3.2] | 0.997 [0.959, 1.103] | pass |
| annotate / settleMs | 160.1 [160.6] | 160.9 [161.6] | +0.8 [-3.2, +1.9] | pass |
| annotate / mainTaskMs | 58.0 [58.7] | 55.1 [58.1] | 0.950 [0.854, 1.188] | pass |
| annotate / mainJsMs | 34.6 [34.9] | 32.3 [33.9] | 0.933 [0.855, 1.141] | pass |

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

Not run: `impl-head`.

## `plugins-tip` vs `plugins-fixed`

Not run: `plugins-tip`.

## `impl-fixed` vs `impl-enginelink`

Not run: `impl-enginelink`.

## `plugins-fixed` vs `plugins-enginelink`

Not run: `plugins-enginelink`.

## `impl-fixed` vs `impl-o2`

Not run: `impl-o2`.

## `impl-o2` vs `impl-enginelink`

Not run: `impl-o2`, `impl-enginelink`.

## `plugins-fixed` vs `plugins-o2`

Not run: `plugins-o2`.

## `plugins-o2` vs `plugins-enginelink`

Not run: `plugins-o2`, `plugins-enginelink`.

## `main` vs `main-compat`

| Row | main median [p95] | main-compat median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.3 [53.5] | 51.6 [65.3] | 0.968 [0.955, 1.276] | no detectable difference |
| open(ebook) / openedMs | 116.4 [118.5] | 115.0 [129.0] | 0.988 [0.961, 1.135] | no detectable difference |
| open(ebook) / firstPaintMs | 217.8 [221.0] | 211.4 [226.1] | 0.971 [0.939, 1.100] | no detectable difference |
| open(ebook) / settledMs | 337.3 [342.6] | 331.9 [344.7] | 0.984 [0.951, 1.044] | no detectable difference |
| open(ebook) / mainTaskMs | 119.0 [119.9] | 116.0 [120.6] | 0.975 [0.943, 1.039] | no detectable difference |
| open(ebook) / mainJsMs | 92.6 [94.1] | 89.9 [131.6] | 0.971 [0.934, 1.511] | no detectable difference |
| open(large) / readyMs | 52.6 [53.0] | 52.9 [52.9] | 1.005 [0.983, 1.018] | no detectable difference |
| open(large) / openedMs | 133.5 [133.8] | 132.9 [133.6] | 0.996 [0.992, 1.002] | no detectable difference |
| open(large) / firstPaintMs | 221.7 [224.9] | 221.5 [222.6] | 0.999 [0.967, 1.007] | no detectable difference |
| open(large) / settledMs | 218.4 [222.1] | 218.2 [219.3] | 0.999 [0.964, 1.006] | no detectable difference |
| open(large) / mainTaskMs | 262.7 [263.3] | 244.0 [244.1] | 0.929 [0.917, 0.933] | faster |
| open(large) / mainJsMs | 239.3 [239.4] | 220.9 [221.2] | 0.923 [0.914, 0.929] | faster |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2015.0 [2037.8] | 2000.0 [2007.5] | 0.993 [0.979, 1.003] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.6 [8.8] | 8.5 [8.8] | 0.989 [0.962, 1.037] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 412.9 [415.2] | 397.9 [399.0] | 0.964 [0.856, 0.993] | no detectable difference |
| scroll / mainJsMs | 321.4 [322.7] | 306.2 [307.4] | 0.953 [0.839, 0.989] | no detectable difference |
| jump / settleMs | 94.6 [95.4] | 91.9 [95.8] | -2.7 [-6.0, +3.5] | no detectable difference |
| jump / mainTaskMs | 56.2 [56.9] | 56.0 [56.4] | 0.997 [0.950, 1.038] | no detectable difference |
| jump / mainJsMs | 42.8 [44.5] | 41.9 [42.8] | 0.978 [0.904, 1.038] | no detectable difference |
| zoom / settleMs | 339.5 [360.8] | 335.3 [359.1] | -4.1 [-29.0, +24.9] | no detectable difference |
| zoom / zoom2SettleMs | 124.8 [128.5] | 126.1 [126.8] | +1.3 [-4.0, +8.7] | no detectable difference |
| zoom / zoom4SettleMs | 212.0 [241.7] | 210.4 [233.1] | -1.6 [-37.7, +25.1] | no detectable difference |
| zoom / backTo1SettleMs | 78.9 [85.6] | 65.2 [67.2] | -13.7 [-28.2, +5.7] | no detectable difference |
| zoom / mainTaskMs | 168.3 [170.7] | 163.4 [172.7] | 0.971 [0.952, 1.033] | no detectable difference |
| zoom / mainJsMs | 121.8 [125.1] | 117.3 [119.1] | 0.963 [0.914, 0.980] | no detectable difference |
| select / settleMs | 147.6 [148.6] | 147.8 [149.7] | +0.2 [-1.0, +2.3] | no detectable difference |
| select / mainTaskMs | 43.5 [48.0] | 43.0 [45.1] | 0.989 [0.768, 1.043] | no detectable difference |
| select / mainJsMs | 27.3 [30.7] | 26.5 [26.8] | 0.970 [0.714, 1.006] | no detectable difference |
| search / searchMs | 50.2 [54.5] | 55.7 [57.9] | 1.110 [0.902, 1.218] | no detectable difference |
| search / mainTaskMs | 4.2 [4.6] | 4.2 [4.3] | 1.001 [0.858, 1.046] | no detectable difference |
| search / mainJsMs | 3.1 [3.1] | 2.8 [2.8] | 0.900 [0.841, 0.988] | no detectable difference |
| annotate / settleMs | 160.1 [160.6] | 159.0 [161.4] | -1.1 [-4.5, +1.9] | no detectable difference |
| annotate / mainTaskMs | 58.0 [58.7] | 52.7 [57.5] | 0.908 [0.869, 1.180] | no detectable difference |
| annotate / mainJsMs | 34.6 [34.9] | 30.5 [33.8] | 0.882 [0.817, 1.145] | no detectable difference |

## `impl-fixed` vs `impl-compat`

| Row | impl-fixed median [p95] | impl-compat median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 51.5 [51.9] | 52.0 [52.6] | 1.009 [0.985, 1.046] | no detectable difference |
| open(ebook) / openedMs | 120.0 [120.4] | 119.2 [122.0] | 0.993 [0.988, 1.037] | no detectable difference |
| open(ebook) / firstPaintMs | 218.5 [219.5] | 217.2 [220.5] | 0.994 [0.986, 1.016] | no detectable difference |
| open(ebook) / settledMs | 264.3 [266.6] | 266.4 [267.7] | 1.008 [0.989, 1.027] | no detectable difference |
| open(ebook) / mainTaskMs | 104.7 [106.7] | 103.4 [105.9] | 0.987 [0.966, 1.024] | no detectable difference |
| open(ebook) / mainJsMs | 80.4 [82.5] | 79.8 [82.0] | 0.993 [0.964, 1.031] | no detectable difference |
| open(large) / readyMs | 52.4 [53.8] | 53.3 [53.9] | 1.017 [0.970, 1.045] | no detectable difference |
| open(large) / openedMs | 141.0 [143.8] | 142.0 [143.3] | 1.007 [0.981, 1.020] | no detectable difference |
| open(large) / firstPaintMs | 223.3 [231.0] | 231.5 [233.0] | 1.037 [0.961, 1.060] | no detectable difference |
| open(large) / settledMs | 218.9 [226.5] | 226.9 [229.1] | 1.037 [0.959, 1.064] | no detectable difference |
| open(large) / mainTaskMs | 161.2 [163.1] | 160.8 [162.0] | 0.997 [0.981, 1.010] | no detectable difference |
| open(large) / mainJsMs | 135.5 [137.0] | 134.5 [135.5] | 0.993 [0.980, 1.003] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 1989.3 [1996.9] | 1998.8 [2006.5] | 1.005 [0.996, 1.014] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.5] | 8.7 [9.1] | 1.027 [0.987, 1.083] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 329.3 [331.6] | 330.8 [342.4] | 1.004 [0.990, 1.060] | no detectable difference |
| scroll / mainJsMs | 226.4 [227.5] | 228.1 [235.8] | 1.007 [0.994, 1.059] | no detectable difference |
| jump / settleMs | 79.5 [81.1] | 80.6 [80.9] | +1.1 [-8.9, +2.0] | no detectable difference |
| jump / mainTaskMs | 51.0 [60.4] | 49.8 [49.9] | 0.978 [0.769, 1.047] | no detectable difference |
| jump / mainJsMs | 32.5 [36.6] | 31.3 [32.1] | 0.963 [0.809, 1.052] | no detectable difference |
| zoom / settleMs | 358.5 [360.5] | 341.9 [344.1] | -16.6 [-21.4, +6.4] | no detectable difference |
| zoom / zoom2SettleMs | 140.7 [145.3] | 132.4 [133.7] | -8.3 [-13.4, +3.6] | no detectable difference |
| zoom / zoom4SettleMs | 214.9 [217.5] | 209.5 [210.4] | -5.4 [-11.0, +2.8] | no detectable difference |
| zoom / backTo1SettleMs | 62.9 [63.5] | 60.8 [62.7] | -2.1 [-5.5, +1.7] | no detectable difference |
| zoom / mainTaskMs | 138.8 [143.4] | 136.4 [137.6] | 0.983 [0.928, 1.006] | no detectable difference |
| zoom / mainJsMs | 95.3 [96.1] | 93.1 [94.2] | 0.977 [0.967, 1.015] | no detectable difference |
| select / settleMs | 150.4 [165.9] | 150.6 [151.5] | +0.2 [-18.6, +1.2] | no detectable difference |
| select / mainTaskMs | 40.8 [45.0] | 40.6 [40.7] | 0.997 [0.767, 1.027] | no detectable difference |
| select / mainJsMs | 21.2 [23.7] | 20.7 [20.8] | 0.977 [0.751, 1.023] | no detectable difference |
| search / searchMs | 50.1 [54.0] | 47.3 [54.2] | 0.944 [0.861, 1.150] | no detectable difference |
| search / mainTaskMs | 2.5 [2.7] | 2.4 [2.6] | 0.963 [0.869, 1.023] | no detectable difference |
| search / mainJsMs | 1.1 [1.2] | 1.1 [1.2] | 0.983 [0.890, 1.080] | no detectable difference |
| annotate / settleMs | 158.6 [160.0] | 158.2 [158.4] | -0.4 [-2.6, +2.5] | no detectable difference |
| annotate / mainTaskMs | 51.0 [51.6] | 50.1 [51.0] | 0.983 [0.946, 1.186] | no detectable difference |
| annotate / mainJsMs | 24.9 [26.0] | 25.1 [25.1] | 1.007 [0.941, 1.157] | no detectable difference |

## `plugins-fixed` vs `plugins-compat`

| Row | plugins-fixed median [p95] | plugins-compat median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 75.5 [75.7] | 75.2 [77.1] | 0.997 [0.992, 1.027] | no detectable difference |
| open(ebook) / openedMs | 175.5 [175.7] | 177.6 [179.0] | 1.012 [1.001, 1.022] | no detectable difference |
| open(ebook) / firstPaintMs | 297.8 [298.2] | 306.6 [307.3] | 1.030 [1.003, 1.034] | no detectable difference |
| open(ebook) / settledMs | 347.3 [347.8] | 355.3 [361.4] | 1.023 [1.017, 1.046] | no detectable difference |
| open(ebook) / mainTaskMs | 232.6 [233.7] | 235.7 [238.7] | 1.013 [1.004, 1.029] | no detectable difference |
| open(ebook) / mainJsMs | 195.7 [196.2] | 197.4 [199.2] | 1.009 [0.999, 1.023] | no detectable difference |
| open(large) / readyMs | 75.3 [78.3] | 74.8 [74.8] | 0.994 [0.948, 1.001] | no detectable difference |
| open(large) / openedMs | 202.7 [205.6] | 202.7 [205.0] | 1.000 [0.978, 1.013] | no detectable difference |
| open(large) / firstPaintMs | 333.8 [337.0] | 332.4 [334.6] | 0.996 [0.979, 1.012] | no detectable difference |
| open(large) / settledMs | 343.0 [345.7] | 339.3 [341.6] | 0.989 [0.977, 1.004] | no detectable difference |
| open(large) / mainTaskMs | 526.7 [528.5] | 525.0 [527.9] | 0.997 [0.991, 1.005] | no detectable difference |
| open(large) / mainJsMs | 489.4 [492.5] | 488.7 [491.6] | 0.999 [0.990, 1.008] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2261.5 [2262.0] | 2200.5 [2232.0] | 0.973 [0.970, 1.015] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.6 [9.6] | 9.4 [9.6] | 0.987 [0.975, 1.040] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 756.8 [763.5] | 727.3 [744.3] | 0.961 [0.936, 1.003] | no detectable difference |
| scroll / mainJsMs | 651.9 [655.6] | 623.8 [640.6] | 0.957 [0.934, 0.999] | no detectable difference |
| jump / settleMs | 136.8 [148.5] | 118.2 [153.2] | -18.6 [-59.3, +30.3] | no detectable difference |
| jump / mainTaskMs | 141.9 [151.2] | 146.1 [314.4] | 1.029 [0.935, 2.401] | no detectable difference |
| jump / mainJsMs | 126.9 [135.8] | 131.2 [300.1] | 1.034 [0.944, 2.528] | no detectable difference |
| zoom / settleMs | 398.8 [399.1] | 394.9 [404.6] | -3.8 [-17.6, +36.5] | no detectable difference |
| zoom / zoom2SettleMs | 143.9 [144.3] | 148.5 [151.9] | +4.6 [-3.8, +17.0] | no detectable difference |
| zoom / zoom4SettleMs | 254.5 [255.2] | 246.5 [252.7] | -8.0 [-14.2, +19.5] | no detectable difference |
| zoom / backTo1SettleMs | 86.2 [98.4] | 82.1 [82.9] | -4.1 [-21.2, +3.8] | no detectable difference |
| zoom / mainTaskMs | 257.7 [259.2] | 265.9 [266.7] | 1.032 [1.021, 1.051] | no detectable difference |
| zoom / mainJsMs | 219.5 [220.8] | 225.0 [225.6] | 1.025 [1.014, 1.051] | no detectable difference |
| select / settleMs | 145.3 [145.7] | 144.8 [146.8] | -0.5 [-1.1, +1.7] | no detectable difference |
| select / mainTaskMs | 67.8 [70.3] | 66.7 [66.7] | 0.983 [0.905, 0.985] | no detectable difference |
| select / mainJsMs | 53.4 [54.8] | 53.1 [53.3] | 0.995 [0.931, 1.006] | no detectable difference |
| search / searchMs | 53.8 [62.6] | 65.2 [68.1] | 1.211 [0.985, 1.281] | no detectable difference |
| search / mainTaskMs | 8.5 [14.8] | 12.5 [16.8] | 1.470 [0.594, 2.040] | no detectable difference |
| search / mainJsMs | 6.2 [7.0] | 7.3 [7.7] | 1.171 [0.872, 1.339] | no detectable difference |
| annotate / settleMs | 166.5 [167.9] | 166.7 [166.8] | +0.2 [-3.2, +0.5] | no detectable difference |
| annotate / mainTaskMs | 80.4 [80.7] | 78.9 [82.2] | 0.982 [0.967, 1.043] | no detectable difference |
| annotate / mainJsMs | 59.8 [60.3] | 58.6 [61.6] | 0.979 [0.969, 1.041] | no detectable difference |

## Memory

| Row | main median [p95] | main-aa median [p95] | impl-fixed median [p95] | plugins-fixed median [p95] | main-compat median [p95] | impl-compat median [p95] | plugins-compat median [p95] |
| --- | --- | --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.35 MiB [34.35 MiB] | 34.34 MiB [34.35 MiB] | 34.95 MiB [34.95 MiB] | 41.45 MiB [41.45 MiB] | 34.26 MiB [34.27 MiB] | 34.88 MiB [34.89 MiB] | 41.42 MiB [41.42 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.06 MiB [23.06 MiB] | 23.06 MiB [23.06 MiB] | 22.27 MiB [22.27 MiB] | 23.06 MiB [23.06 MiB] | 23.06 MiB [23.06 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.15 MiB] | 48.16 MiB [48.17 MiB] | 48.62 MiB [48.64 MiB] | 56.27 MiB [56.27 MiB] | 48.08 MiB [48.09 MiB] | 48.55 MiB [48.55 MiB] | 56.23 MiB [56.23 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 23.03 MiB [23.03 MiB] | 22.99 MiB [22.99 MiB] | 22.26 MiB [22.26 MiB] | 23.03 MiB [23.03 MiB] | 22.99 MiB [22.99 MiB] |
| soak / heapCycle5Bytes | 37.42 MiB [37.42 MiB] | 37.41 MiB [37.42 MiB] | 38.10 MiB [38.10 MiB] | 44.26 MiB [45.17 MiB] | 37.35 MiB [37.35 MiB] | 38.01 MiB [38.03 MiB] | 44.21 MiB [45.11 MiB] |
| soak / heapCycle30Bytes | 38.26 MiB [38.26 MiB] | 38.27 MiB [38.27 MiB] | 38.82 MiB [38.83 MiB] | 44.79 MiB [45.71 MiB] | 38.20 MiB [38.21 MiB] | 38.75 MiB [38.76 MiB] | 44.76 MiB [45.65 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

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

`plugins-compat`:

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

