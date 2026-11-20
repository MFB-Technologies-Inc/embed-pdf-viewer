# Viewer perf comparison: 2026-10-01T07-08-18-637Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | d7f1b1067ea4f6431ea78b1c711d1f484bc9f499 |
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
| plugins-now | research/multi-platform-rust-core-impl-plugins | 4f9ceee9c | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | d6276d5fb70d |
| plugins-slicetext | research/rust-kernel-slice-text | d9c5e4a58 | built (fork 7c7b600b1, wasm32-eh) | c888920f5d27 | 47364f7f1fac |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-now`: pass
- `plugins-slicetext`: pass

## Sizes

| Metric | main | main-aa | plugins-now | plugins-slicetext |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 9.69 MiB | 9.69 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.85 MiB | 2.85 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 7.93 MiB | 7.94 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.70 MiB | 1.70 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.63 MiB | 2.63 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

37 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.0 [53.6] | 51.4 [52.4] | 0.970 [0.954, 1.010] | pass |
| open(ebook) / openedMs | 116.1 [116.2] | 114.6 [115.8] | 0.987 [0.986, 1.013] | pass |
| open(ebook) / firstPaintMs | 212.9 [216.3] | 212.0 [213.3] | 0.996 [0.970, 1.017] | pass |
| open(ebook) / settledMs | 333.8 [337.3] | 334.0 [335.4] | 1.001 [0.985, 1.005] | pass |
| open(ebook) / mainTaskMs | 118.2 [119.5] | 120.3 [120.5] | 1.018 [0.982, 1.026] | pass |
| open(ebook) / mainJsMs | 91.6 [92.9] | 94.5 [94.5] | 1.031 [0.987, 1.033] | pass |
| open(large) / readyMs | 51.5 [53.6] | 53.1 [54.8] | 1.033 [0.970, 1.076] | pass |
| open(large) / openedMs | 133.9 [135.3] | 135.1 [136.1] | 1.009 [0.992, 1.022] | pass |
| open(large) / firstPaintMs | 222.2 [225.9] | 226.4 [229.7] | 1.019 [0.945, 1.046] | pass |
| open(large) / settledMs | 219.4 [222.5] | 223.3 [226.9] | 1.018 [0.979, 1.048] | pass |
| open(large) / mainTaskMs | 262.7 [263.3] | 261.7 [264.9] | 0.996 [0.979, 1.014] | pass |
| open(large) / mainJsMs | 239.4 [239.5] | 238.1 [241.5] | 0.995 [0.979, 1.014] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2008.1 [2021.4] | 2006.4 [2018.7] | 0.999 [0.992, 1.016] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.5 [8.9] | 8.6 [8.6] | 1.010 [0.943, 1.021] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 389.1 [396.7] | 391.0 [406.4] | 1.005 [0.966, 1.059] | pass |
| scroll / mainJsMs | 298.5 [306.8] | 303.0 [313.3] | 1.015 [0.963, 1.057] | pass |
| jump / settleMs | 95.8 [99.2] | 91.2 [94.7] | -4.5 [-11.0, +12.0] | pass |
| jump / mainTaskMs | 55.6 [57.7] | 56.4 [57.0] | 1.015 [0.898, 1.245] | pass |
| jump / mainJsMs | 44.6 [46.1] | 41.8 [43.0] | 0.936 [0.874, 1.263] | pass |
| zoom / settleMs | 381.1 [393.4] | 374.1 [377.5] | -7.0 [-34.7, -0.7] | pass |
| zoom / zoom2SettleMs | 129.4 [135.0] | 130.4 [132.7] | +1.0 [-11.9, +8.0] | pass |
| zoom / zoom4SettleMs | 253.7 [258.6] | 247.5 [250.1] | -6.2 [-32.0, -1.3] | pass |
| zoom / backTo1SettleMs | 72.8 [73.4] | 72.0 [72.8] | -0.7 [-8.5, +2.4] | pass |
| zoom / mainTaskMs | 180.5 [182.0] | 172.0 [176.7] | 0.953 [0.936, 0.984] | A/A failed |
| zoom / mainJsMs | 133.7 [134.7] | 125.1 [127.8] | 0.936 [0.913, 0.984] | A/A failed |
| select / settleMs | 147.1 [148.2] | 148.4 [148.8] | +1.3 [-9.6, +9.0] | pass |
| select / mainTaskMs | 38.6 [41.7] | 44.1 [44.3] | 1.142 [0.896, 1.165] | pass |
| select / mainJsMs | 23.4 [26.0] | 27.2 [27.7] | 1.159 [0.884, 1.195] | pass |
| search / searchMs | 58.1 [60.2] | 48.7 [60.5] | 0.839 [0.803, 1.268] | pass |
| search / mainTaskMs | 4.5 [5.0] | 4.5 [4.8] | 1.005 [0.852, 1.158] | pass |
| search / mainJsMs | 3.1 [3.1] | 3.1 [3.3] | 1.009 [0.947, 1.185] | pass |
| annotate / settleMs | 153.3 [167.4] | 159.1 [165.4] | +5.8 [-17.6, +14.3] | pass |
| annotate / mainTaskMs | 55.5 [60.4] | 56.2 [58.6] | 1.013 [0.825, 1.125] | pass |
| annotate / mainJsMs | 32.3 [35.4] | 33.2 [34.5] | 1.026 [0.840, 1.102] | pass |

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

| Row | plugins-now median [p95] | plugins-slicetext median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 76.6 [77.7] | 75.2 [75.3] | 0.980 [0.963, 0.996] | no detectable difference |
| open(ebook) / openedMs | 174.5 [175.8] | 173.8 [174.4] | 0.996 [0.979, 1.007] | no detectable difference |
| open(ebook) / firstPaintMs | 299.1 [301.3] | 299.8 [301.8] | 1.002 [0.975, 1.017] | no detectable difference |
| open(ebook) / settledMs | 350.0 [355.5] | 351.4 [354.2] | 1.004 [0.963, 1.017] | no detectable difference |
| open(ebook) / mainTaskMs | 238.1 [240.4] | 237.4 [239.0] | 0.997 [0.977, 1.006] | no detectable difference |
| open(ebook) / mainJsMs | 200.2 [200.7] | 199.8 [199.8] | 0.998 [0.985, 1.007] | no detectable difference |
| open(large) / readyMs | 76.0 [76.8] | 75.3 [76.0] | 0.990 [0.969, 1.012] | no detectable difference |
| open(large) / openedMs | 200.2 [202.6] | 200.3 [202.2] | 1.000 [0.984, 1.020] | no detectable difference |
| open(large) / firstPaintMs | 336.1 [346.6] | 333.8 [335.1] | 0.993 [0.955, 1.002] | no detectable difference |
| open(large) / settledMs | 339.0 [342.5] | 342.8 [344.3] | 1.011 [0.997, 1.022] | no detectable difference |
| open(large) / mainTaskMs | 524.6 [529.9] | 516.8 [518.9] | 0.985 [0.973, 0.992] | no detectable difference |
| open(large) / mainJsMs | 487.6 [493.1] | 480.3 [481.0] | 0.985 [0.971, 0.990] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2277.7 [2278.3] | 2269.9 [2282.8] | 0.997 [0.978, 1.037] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.2 [9.6] | 9.7 [9.7] | 1.052 [0.983, 1.058] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 758.4 [766.3] | 761.4 [761.8] | 1.004 [0.966, 1.067] | no detectable difference |
| scroll / mainJsMs | 657.8 [663.8] | 656.2 [656.3] | 0.998 [0.961, 1.068] | no detectable difference |
| jump / settleMs | 130.4 [132.1] | 130.0 [154.8] | -0.3 [-7.0, +46.5] | no detectable difference |
| jump / mainTaskMs | 148.7 [149.3] | 157.0 [158.4] | 1.056 [1.050, 1.157] | no detectable difference |
| jump / mainJsMs | 133.8 [134.2] | 138.9 [140.7] | 1.038 [1.033, 1.100] | no detectable difference |
| zoom / settleMs | 435.0 [446.4] | 432.6 [433.2] | -2.4 [-36.0, +6.8] | no detectable difference |
| zoom / zoom2SettleMs | 152.8 [157.1] | 158.0 [158.6] | +5.2 [-7.0, +13.5] | no detectable difference |
| zoom / zoom4SettleMs | 289.8 [290.1] | 274.6 [274.6] | -15.3 [-29.1, +1.1] | no detectable difference |
| zoom / backTo1SettleMs | 104.7 [104.7] | 87.0 [91.5] | -17.7 [-19.4, +6.4] | no detectable difference |
| zoom / mainTaskMs | 277.4 [288.1] | 272.6 [281.5] | 0.983 [0.930, 1.023] | unreliable (A/A failed) |
| zoom / mainJsMs | 230.1 [240.8] | 231.0 [234.6] | 1.004 [0.943, 1.027] | unreliable (A/A failed) |
| select / settleMs | 145.2 [146.9] | 145.2 [152.4] | -0.1 [-2.5, +8.5] | no detectable difference |
| select / mainTaskMs | 66.3 [67.3] | 65.0 [65.9] | 0.980 [0.944, 1.018] | no detectable difference |
| select / mainJsMs | 52.4 [53.5] | 51.4 [51.8] | 0.982 [0.943, 1.009] | no detectable difference |
| search / searchMs | 68.4 [69.8] | 68.0 [68.4] | 0.994 [0.959, 1.007] | no detectable difference |
| search / mainTaskMs | 17.0 [19.4] | 18.6 [19.1] | 1.096 [0.941, 1.165] | no detectable difference |
| search / mainJsMs | 7.8 [9.2] | 7.7 [8.5] | 0.984 [0.815, 1.133] | no detectable difference |
| annotate / settleMs | 165.7 [166.4] | 166.0 [166.1] | +0.3 [-1.1, +1.4] | no detectable difference |
| annotate / mainTaskMs | 82.5 [87.2] | 80.8 [83.4] | 0.980 [0.905, 1.084] | no detectable difference |
| annotate / mainJsMs | 61.6 [66.7] | 60.3 [61.4] | 0.978 [0.883, 1.061] | no detectable difference |

## `main` vs `plugins-slicetext`

| Row | main median [p95] | plugins-slicetext median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.0 [53.6] | 75.2 [75.3] | 1.417 [1.398, 1.447] | slower |
| open(ebook) / openedMs | 116.1 [116.2] | 173.8 [174.4] | 1.497 [1.481, 1.523] | slower |
| open(ebook) / firstPaintMs | 212.9 [216.3] | 299.8 [301.8] | 1.408 [1.356, 1.439] | slower |
| open(ebook) / settledMs | 333.8 [337.3] | 351.4 [354.2] | 1.053 [1.015, 1.063] | slower |
| open(ebook) / mainTaskMs | 118.2 [119.5] | 237.4 [239.0] | 2.008 [1.965, 2.036] | slower |
| open(ebook) / mainJsMs | 91.6 [92.9] | 199.8 [199.8] | 2.180 [2.126, 2.183] | slower |
| open(large) / readyMs | 51.5 [53.6] | 75.3 [76.0] | 1.463 [1.385, 1.489] | slower |
| open(large) / openedMs | 133.9 [135.3] | 200.3 [202.2] | 1.496 [1.474, 1.518] | slower |
| open(large) / firstPaintMs | 222.2 [225.9] | 333.8 [335.1] | 1.502 [1.467, 1.524] | slower |
| open(large) / settledMs | 219.4 [222.5] | 342.8 [344.3] | 1.562 [1.534, 1.589] | slower |
| open(large) / mainTaskMs | 262.7 [263.3] | 516.8 [518.9] | 1.967 [1.959, 1.985] | slower |
| open(large) / mainJsMs | 239.4 [239.5] | 480.3 [481.0] | 2.006 [2.001, 2.018] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2008.1 [2021.4] | 2269.9 [2282.8] | 1.130 [1.101, 1.149] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.5 [8.9] | 9.7 [9.7] | 1.143 [1.060, 1.149] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 389.1 [396.7] | 761.4 [761.8] | 1.957 [1.864, 1.976] | slower |
| scroll / mainJsMs | 298.5 [306.8] | 656.2 [656.3] | 2.198 [2.075, 2.207] | slower |
| jump / settleMs | 95.8 [99.2] | 130.0 [154.8] | +34.2 [+25.7, +74.5] | slower |
| jump / mainTaskMs | 55.6 [57.7] | 157.0 [158.4] | 2.824 [2.705, 3.461] | slower |
| jump / mainJsMs | 44.6 [46.1] | 138.9 [140.7] | 3.110 [2.998, 4.124] | slower |
| zoom / settleMs | 381.1 [393.4] | 432.6 [433.2] | +51.6 [+16.9, +54.6] | slower |
| zoom / zoom2SettleMs | 129.4 [135.0] | 158.0 [158.6] | +28.6 [+15.0, +33.8] | slower |
| zoom / zoom4SettleMs | 253.7 [258.6] | 274.6 [274.6] | +20.9 [+1.9, +23.0] | no detectable difference |
| zoom / backTo1SettleMs | 72.8 [73.4] | 87.0 [91.5] | +14.2 [+11.9, +21.5] | slower |
| zoom / mainTaskMs | 180.5 [182.0] | 272.6 [281.5] | 1.510 [1.476, 1.568] | unreliable (A/A failed) |
| zoom / mainJsMs | 133.7 [134.7] | 231.0 [234.6] | 1.728 [1.693, 1.805] | unreliable (A/A failed) |
| select / settleMs | 147.1 [148.2] | 145.2 [152.4] | -1.9 [-3.8, +13.3] | no detectable difference |
| select / mainTaskMs | 38.6 [41.7] | 65.0 [65.9] | 1.682 [1.511, 1.735] | slower |
| select / mainJsMs | 23.4 [26.0] | 51.4 [51.8] | 2.195 [1.925, 2.234] | slower |
| search / searchMs | 58.1 [60.2] | 68.0 [68.4] | 1.171 [1.109, 1.405] | no detectable difference |
| search / mainTaskMs | 4.5 [5.0] | 18.6 [19.1] | 4.143 [3.671, 4.573] | slower |
| search / mainJsMs | 3.1 [3.1] | 7.7 [8.5] | 2.500 [2.479, 3.043] | slower |
| annotate / settleMs | 153.3 [167.4] | 166.0 [166.1] | +12.6 [-3.5, +14.2] | no detectable difference |
| annotate / mainTaskMs | 55.5 [60.4] | 80.8 [83.4] | 1.457 [1.303, 1.600] | slower |
| annotate / mainJsMs | 32.3 [35.4] | 60.3 [61.4] | 1.865 [1.663, 1.956] | slower |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-now median [p95] | plugins-slicetext median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.34 MiB [35.25 MiB] | 42.41 MiB [42.42 MiB] | 42.42 MiB [42.43 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.03 MiB [23.03 MiB] | 23.03 MiB [23.03 MiB] |
| open(large) / heapAfterOpenBytes | 48.14 MiB [48.14 MiB] | 48.15 MiB [48.16 MiB] | 56.23 MiB [56.24 MiB] | 56.23 MiB [56.23 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.96 MiB [22.96 MiB] | 22.96 MiB [22.96 MiB] |
| soak / heapCycle5Bytes | 37.43 MiB [37.43 MiB] | 37.41 MiB [37.43 MiB] | 45.22 MiB [45.23 MiB] | 44.23 MiB [44.23 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.27 MiB] | 38.27 MiB [38.28 MiB] | 45.77 MiB [45.78 MiB] | 44.77 MiB [44.78 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-now`:

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

`plugins-slicetext`:

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

