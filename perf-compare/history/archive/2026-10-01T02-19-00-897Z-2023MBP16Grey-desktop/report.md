# Viewer perf comparison: 2026-10-01T02-19-00-897Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 0569b6daaf43ae53c0208506820c38a9cd9886f9 |
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
| plugins-bump | research/rust-kernel-bump | dd1b86fcc | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 764d1ef49e62 |
| plugins-head | research/multi-platform-rust-core-impl-plugins | 51130b042 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 8118f9d9ee26 |
| plugins-buildlto | research/rust-kernel-build-lto | 99d1a8c04 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 764d1ef49e62 |
| plugins-wasmopt | research/rust-kernel-wasm-opt | e981e0cb9 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | d6276d5fb70d |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-bump`: pass
- `plugins-head`: pass
- `plugins-buildlto`: pass
- `plugins-wasmopt`: pass

## Sizes

| Metric | main | main-aa | plugins-bump | plugins-head | plugins-buildlto | plugins-wasmopt |
| --- | --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 11.69 MiB | 10.81 MiB | 11.69 MiB | 7.93 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.88 MiB | 1.77 MiB | 1.88 MiB | 1.70 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.62 MiB | 2.62 MiB | 2.62 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

39 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.5 [54.4] | 52.7 [52.7] | 1.002 [0.957, 1.010] | pass |
| open(ebook) / openedMs | 117.0 [120.6] | 115.1 [115.8] | 0.984 [0.949, 1.011] | pass |
| open(ebook) / firstPaintMs | 216.6 [217.1] | 213.6 [214.6] | 0.987 [0.978, 1.007] | pass |
| open(ebook) / settledMs | 337.6 [340.5] | 334.7 [335.2] | 0.991 [0.977, 0.993] | pass |
| open(ebook) / mainTaskMs | 119.7 [120.1] | 117.0 [118.3] | 0.977 [0.970, 0.993] | pass |
| open(ebook) / mainJsMs | 93.0 [93.8] | 90.9 [92.0] | 0.978 [0.969, 0.992] | pass |
| open(large) / readyMs | 54.0 [55.4] | 53.0 [54.7] | 0.981 [0.937, 1.056] | pass |
| open(large) / openedMs | 136.0 [136.1] | 135.6 [136.0] | 0.997 [0.985, 1.005] | pass |
| open(large) / firstPaintMs | 226.0 [227.8] | 226.0 [226.3] | 1.000 [0.979, 1.005] | pass |
| open(large) / settledMs | 224.0 [225.2] | 223.6 [223.8] | 0.998 [0.979, 1.007] | pass |
| open(large) / mainTaskMs | 263.7 [268.9] | 261.0 [268.6] | 0.990 [0.967, 1.034] | pass |
| open(large) / mainJsMs | 239.6 [244.3] | 237.9 [244.5] | 0.993 [0.970, 1.034] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2006.5 [2029.6] | 2032.4 [2038.5] | 1.013 [0.993, 1.020] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.5 [8.6] | 8.8 [9.0] | 1.044 [0.990, 1.070] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 406.5 [410.0] | 383.6 [391.8] | 0.944 [0.925, 1.004] | pass |
| scroll / mainJsMs | 315.6 [318.2] | 296.7 [303.8] | 0.940 [0.920, 1.012] | pass |
| jump / settleMs | 97.4 [100.6] | 92.8 [96.5] | -4.6 [-9.3, +8.2] | pass |
| jump / mainTaskMs | 59.3 [60.2] | 54.7 [58.4] | 0.922 [0.857, 1.015] | pass |
| jump / mainJsMs | 41.8 [43.9] | 42.5 [43.6] | 1.015 [0.927, 1.055] | pass |
| zoom / settleMs | 397.7 [404.6] | 396.3 [397.6] | -1.4 [-52.0, +18.1] | pass |
| zoom / zoom2SettleMs | 132.9 [136.1] | 129.0 [135.8] | -3.9 [-11.9, +13.6] | pass |
| zoom / zoom4SettleMs | 264.8 [268.5] | 259.7 [267.8] | -5.1 [-40.1, +12.1] | pass |
| zoom / backTo1SettleMs | 68.3 [84.0] | 75.2 [75.9] | +7.0 [-17.5, +20.2] | pass |
| zoom / mainTaskMs | 172.4 [185.0] | 174.9 [179.0] | 1.014 [0.919, 1.081] | pass |
| zoom / mainJsMs | 126.5 [136.3] | 127.1 [129.2] | 1.005 [0.914, 1.083] | pass |
| select / settleMs | 148.4 [150.9] | 146.2 [151.0] | -2.3 [-7.9, +4.3] | pass |
| select / mainTaskMs | 44.7 [47.2] | 43.7 [46.3] | 0.979 [0.845, 1.073] | pass |
| select / mainJsMs | 27.6 [29.1] | 27.4 [28.7] | 0.990 [0.854, 1.061] | pass |
| search / searchMs | 57.6 [58.8] | 57.6 [59.2] | 0.999 [0.841, 1.051] | pass |
| search / mainTaskMs | 4.7 [5.0] | 4.4 [5.4] | 0.943 [0.844, 1.194] | pass |
| search / mainJsMs | 3.2 [3.3] | 3.1 [3.4] | 0.998 [0.894, 1.137] | pass |
| annotate / settleMs | 156.4 [160.5] | 160.9 [161.0] | +4.4 [-8.8, +7.9] | pass |
| annotate / mainTaskMs | 56.1 [59.8] | 54.4 [56.7] | 0.969 [0.902, 1.081] | pass |
| annotate / mainJsMs | 34.3 [36.4] | 33.0 [34.1] | 0.962 [0.896, 1.113] | pass |

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

Not run: `plugins-toolbar`.

## `main` vs `plugins-bump`

| Row | main median [p95] | plugins-bump median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.5 [54.4] | 84.8 [85.0] | 1.613 [1.518, 1.630] | slower |
| open(ebook) / openedMs | 117.0 [120.6] | 188.2 [188.3] | 1.609 [1.503, 1.643] | slower |
| open(ebook) / firstPaintMs | 216.6 [217.1] | 312.7 [316.3] | 1.444 [1.424, 1.485] | slower |
| open(ebook) / settledMs | 337.6 [340.5] | 357.2 [363.3] | 1.058 [1.048, 1.078] | slower |
| open(ebook) / mainTaskMs | 119.7 [120.1] | 240.5 [242.2] | 2.009 [1.985, 2.033] | slower |
| open(ebook) / mainJsMs | 93.0 [93.8] | 197.3 [198.2] | 2.120 [2.074, 2.136] | slower |
| open(large) / readyMs | 54.0 [55.4] | 85.3 [85.6] | 1.580 [1.500, 1.645] | slower |
| open(large) / openedMs | 136.0 [136.1] | 211.4 [211.7] | 1.554 [1.530, 1.564] | slower |
| open(large) / firstPaintMs | 226.0 [227.8] | 336.9 [348.2] | 1.491 [1.471, 1.552] | slower |
| open(large) / settledMs | 224.0 [225.2] | 338.9 [340.3] | 1.513 [1.501, 1.533] | slower |
| open(large) / mainTaskMs | 263.7 [268.9] | 522.7 [526.2] | 1.982 [1.931, 2.021] | slower |
| open(large) / mainJsMs | 239.6 [244.3] | 478.6 [485.1] | 1.997 [1.956, 2.048] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2006.5 [2029.6] | 2261.5 [2267.7] | 1.127 [1.084, 1.135] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.5 [8.6] | 9.4 [9.6] | 1.113 [1.077, 1.141] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 406.5 [410.0] | 761.6 [764.3] | 1.873 [1.822, 1.955] | slower |
| scroll / mainJsMs | 315.6 [318.2] | 653.6 [654.4] | 2.071 [2.022, 2.176] | slower |
| jump / settleMs | 97.4 [100.6] | 138.7 [156.9] | +41.2 [-2.6, +70.3] | no detectable difference |
| jump / mainTaskMs | 59.3 [60.2] | 142.1 [142.6] | 2.396 [2.284, 2.463] | slower |
| jump / mainJsMs | 41.8 [43.9] | 128.1 [132.2] | 3.061 [2.896, 3.205] | slower |
| zoom / settleMs | 397.7 [404.6] | 420.1 [431.6] | +22.4 [+3.0, +53.3] | no detectable difference |
| zoom / zoom2SettleMs | 132.9 [136.1] | 141.8 [144.9] | +8.9 [+5.1, +22.2] | no detectable difference |
| zoom / zoom4SettleMs | 264.8 [268.5] | 278.6 [286.8] | +13.8 [-2.3, +31.0] | no detectable difference |
| zoom / backTo1SettleMs | 68.3 [84.0] | 90.2 [103.1] | +22.0 [+2.0, +48.8] | slower |
| zoom / mainTaskMs | 172.4 [185.0] | 269.5 [279.3] | 1.563 [1.422, 1.689] | slower |
| zoom / mainJsMs | 126.5 [136.3] | 227.3 [234.0] | 1.797 [1.588, 1.964] | slower |
| select / settleMs | 148.4 [150.9] | 145.3 [145.7] | -3.2 [-6.4, -1.5] | no detectable difference |
| select / mainTaskMs | 44.7 [47.2] | 64.7 [65.2] | 1.448 [1.335, 1.503] | slower |
| select / mainJsMs | 27.6 [29.1] | 50.9 [51.2] | 1.842 [1.707, 1.886] | slower |
| search / searchMs | 57.6 [58.8] | 63.3 [66.6] | 1.098 [0.891, 1.185] | no detectable difference |
| search / mainTaskMs | 4.7 [5.0] | 15.6 [16.8] | 3.309 [1.847, 3.632] | slower |
| search / mainJsMs | 3.2 [3.3] | 7.3 [7.7] | 2.316 [1.886, 2.524] | slower |
| annotate / settleMs | 156.4 [160.5] | 165.7 [166.3] | +9.3 [+3.3, +13.2] | slower |
| annotate / mainTaskMs | 56.1 [59.8] | 78.5 [79.9] | 1.398 [1.295, 1.521] | slower |
| annotate / mainJsMs | 34.3 [36.4] | 58.9 [60.0] | 1.717 [1.594, 1.955] | slower |

## `plugins-getstate` vs `plugins-item1`

Not run: `plugins-getstate`, `plugins-item1`.

## `plugins-getstate` vs `plugins-item3`

Not run: `plugins-getstate`, `plugins-item3`.

## `plugins-getstate` vs `plugins-bump`

Not run: `plugins-getstate`.

## `plugins-item1` vs `plugins-bump`

Not run: `plugins-item1`.

## `plugins-bump` vs `plugins-reads`

Not run: `plugins-reads`.

## `main` vs `plugins-reads`

Not run: `plugins-reads`.

## `impl-ts` vs `impl-gate`

Not run: `impl-ts`, `impl-gate`.

## `plugins-head` vs `plugins-buildlto`

| Row | plugins-head median [p95] | plugins-buildlto median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 82.1 [82.9] | 82.8 [83.2] | 1.009 [0.992, 1.033] | no detectable difference |
| open(ebook) / openedMs | 182.0 [184.8] | 183.0 [183.5] | 1.006 [0.978, 1.013] | no detectable difference |
| open(ebook) / firstPaintMs | 301.6 [301.9] | 305.6 [307.2] | 1.013 [1.011, 1.020] | no detectable difference |
| open(ebook) / settledMs | 351.8 [354.8] | 355.3 [359.8] | 1.010 [1.000, 1.025] | no detectable difference |
| open(ebook) / mainTaskMs | 237.5 [242.0] | 240.6 [243.2] | 1.013 [0.982, 1.032] | no detectable difference |
| open(ebook) / mainJsMs | 194.4 [197.5] | 196.0 [198.2] | 1.008 [0.985, 1.023] | no detectable difference |
| open(large) / readyMs | 80.7 [81.7] | 83.5 [83.9] | 1.035 [1.005, 1.046] | no detectable difference |
| open(large) / openedMs | 209.6 [209.7] | 210.7 [213.4] | 1.005 [0.992, 1.029] | no detectable difference |
| open(large) / firstPaintMs | 335.1 [338.6] | 338.2 [346.2] | 1.010 [0.986, 1.041] | no detectable difference |
| open(large) / settledMs | 337.4 [341.3] | 340.3 [348.9] | 1.008 [0.986, 1.047] | no detectable difference |
| open(large) / mainTaskMs | 524.6 [528.9] | 523.7 [532.1] | 0.998 [0.989, 1.018] | no detectable difference |
| open(large) / mainJsMs | 481.9 [486.1] | 482.1 [489.7] | 1.001 [0.988, 1.019] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2260.7 [2283.5] | 2227.4 [2280.2] | 0.985 [0.966, 1.029] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.7 [9.8] | 9.4 [9.8] | 0.972 [0.912, 1.037] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 767.7 [770.9] | 743.7 [766.1] | 0.969 [0.936, 1.076] | no detectable difference |
| scroll / mainJsMs | 664.4 [664.7] | 639.6 [658.5] | 0.963 [0.933, 1.067] | no detectable difference |
| jump / settleMs | 115.1 [125.8] | 129.8 [147.1] | +14.8 [-12.0, +48.6] | no detectable difference |
| jump / mainTaskMs | 146.7 [149.2] | 147.6 [147.9] | 1.006 [0.901, 1.041] | no detectable difference |
| jump / mainJsMs | 131.0 [132.9] | 130.4 [132.0] | 0.996 [0.928, 1.048] | no detectable difference |
| zoom / settleMs | 429.9 [437.0] | 430.0 [436.8] | +0.0 [-11.9, +8.3] | no detectable difference |
| zoom / zoom2SettleMs | 155.3 [162.4] | 150.4 [150.7] | -4.9 [-16.6, -4.4] | no detectable difference |
| zoom / zoom4SettleMs | 274.6 [274.7] | 283.4 [286.7] | +8.8 [+0.5, +13.0] | no detectable difference |
| zoom / backTo1SettleMs | 87.5 [91.7] | 104.1 [104.1] | +16.6 [-4.0, +31.4] | no detectable difference |
| zoom / mainTaskMs | 275.6 [287.5] | 274.8 [283.2] | 0.997 [0.926, 1.050] | no detectable difference |
| zoom / mainJsMs | 232.8 [239.9] | 233.0 [237.7] | 1.001 [0.944, 1.066] | no detectable difference |
| select / settleMs | 153.1 [153.8] | 140.7 [151.9] | -12.4 [-17.9, +8.7] | no detectable difference |
| select / mainTaskMs | 68.9 [71.3] | 64.1 [65.8] | 0.931 [0.673, 1.026] | no detectable difference |
| select / mainJsMs | 54.8 [57.1] | 50.7 [52.1] | 0.924 [0.638, 1.006] | no detectable difference |
| search / searchMs | 62.8 [67.7] | 64.1 [71.4] | 1.020 [0.863, 1.391] | no detectable difference |
| search / mainTaskMs | 14.7 [17.3] | 12.4 [19.0] | 0.845 [0.542, 2.193] | no detectable difference |
| search / mainJsMs | 7.4 [8.2] | 7.1 [8.4] | 0.957 [0.781, 1.359] | no detectable difference |
| annotate / settleMs | 165.9 [167.5] | 166.2 [166.6] | +0.3 [-3.6, +0.7] | no detectable difference |
| annotate / mainTaskMs | 82.6 [87.5] | 80.8 [85.1] | 0.978 [0.912, 1.059] | no detectable difference |
| annotate / mainJsMs | 62.2 [66.3] | 60.1 [65.1] | 0.966 [0.889, 1.066] | no detectable difference |

## `plugins-head` vs `plugins-wasmopt`

| Row | plugins-head median [p95] | plugins-wasmopt median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 82.1 [82.9] | 75.7 [75.9] | 0.922 [0.903, 0.943] | faster |
| open(ebook) / openedMs | 182.0 [184.8] | 176.9 [177.6] | 0.972 [0.954, 0.980] | no detectable difference |
| open(ebook) / firstPaintMs | 301.6 [301.9] | 299.3 [300.1] | 0.992 [0.988, 0.997] | no detectable difference |
| open(ebook) / settledMs | 351.8 [354.8] | 349.7 [354.2] | 0.994 [0.980, 1.009] | no detectable difference |
| open(ebook) / mainTaskMs | 237.5 [242.0] | 235.6 [237.2] | 0.992 [0.966, 1.007] | no detectable difference |
| open(ebook) / mainJsMs | 194.4 [197.5] | 197.7 [198.0] | 1.017 [0.995, 1.021] | no detectable difference |
| open(large) / readyMs | 80.7 [81.7] | 75.5 [76.5] | 0.936 [0.917, 0.955] | faster |
| open(large) / openedMs | 209.6 [209.7] | 204.9 [205.9] | 0.978 [0.964, 0.992] | no detectable difference |
| open(large) / firstPaintMs | 335.1 [338.6] | 339.8 [340.0] | 1.014 [0.991, 1.020] | no detectable difference |
| open(large) / settledMs | 337.4 [341.3] | 342.1 [343.1] | 1.014 [0.987, 1.027] | no detectable difference |
| open(large) / mainTaskMs | 524.6 [528.9] | 526.3 [529.1] | 1.003 [0.983, 1.011] | no detectable difference |
| open(large) / mainJsMs | 481.9 [486.1] | 490.6 [493.2] | 1.018 [0.995, 1.025] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2260.7 [2283.5] | 2195.2 [2254.2] | 0.971 [0.956, 1.018] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.7 [9.8] | 9.7 [9.8] | 1.001 [0.985, 1.035] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 767.7 [770.9] | 729.4 [777.9] | 0.950 [0.919, 1.096] | no detectable difference |
| scroll / mainJsMs | 664.4 [664.7] | 629.4 [672.8] | 0.947 [0.916, 1.094] | no detectable difference |
| jump / settleMs | 115.1 [125.8] | 113.7 [142.1] | -1.3 [-14.6, +44.8] | no detectable difference |
| jump / mainTaskMs | 146.7 [149.2] | 144.5 [147.8] | 0.985 [0.966, 1.043] | no detectable difference |
| jump / mainJsMs | 131.0 [132.9] | 129.5 [131.0] | 0.989 [0.969, 1.040] | no detectable difference |
| zoom / settleMs | 429.9 [437.0] | 435.3 [441.5] | +5.4 [-19.8, +13.0] | no detectable difference |
| zoom / zoom2SettleMs | 155.3 [162.4] | 151.8 [164.8] | -3.4 [-12.7, +11.1] | no detectable difference |
| zoom / zoom4SettleMs | 274.6 [274.7] | 276.0 [282.7] | +1.4 [-7.1, +9.4] | no detectable difference |
| zoom / backTo1SettleMs | 87.5 [91.7] | 92.9 [104.4] | +5.4 [-5.9, +32.9] | no detectable difference |
| zoom / mainTaskMs | 275.6 [287.5] | 277.5 [283.6] | 1.007 [0.934, 1.051] | no detectable difference |
| zoom / mainJsMs | 232.8 [239.9] | 229.7 [235.2] | 0.987 [0.923, 1.055] | no detectable difference |
| select / settleMs | 153.1 [153.8] | 143.7 [145.1] | -9.4 [-14.3, +0.9] | no detectable difference |
| select / mainTaskMs | 68.9 [71.3] | 65.9 [66.2] | 0.957 [0.731, 1.029] | no detectable difference |
| select / mainJsMs | 54.8 [57.1] | 52.1 [52.3] | 0.950 [0.710, 1.008] | no detectable difference |
| search / searchMs | 62.8 [67.7] | 67.8 [68.1] | 1.079 [0.897, 1.313] | no detectable difference |
| search / mainTaskMs | 14.7 [17.3] | 17.5 [18.3] | 1.191 [0.527, 2.050] | no detectable difference |
| search / mainJsMs | 7.4 [8.2] | 7.6 [7.7] | 1.020 [0.809, 1.218] | no detectable difference |
| annotate / settleMs | 165.9 [167.5] | 165.9 [167.0] | +0.0 [-2.1, +1.3] | no detectable difference |
| annotate / mainTaskMs | 82.6 [87.5] | 78.4 [85.6] | 0.950 [0.882, 1.068] | no detectable difference |
| annotate / mainJsMs | 62.2 [66.3] | 59.1 [64.9] | 0.950 [0.876, 1.064] | no detectable difference |

## `plugins-bump` vs `plugins-buildlto`

| Row | plugins-bump median [p95] | plugins-buildlto median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 84.8 [85.0] | 82.8 [83.2] | 0.977 [0.968, 1.005] | no detectable difference |
| open(ebook) / openedMs | 188.2 [188.3] | 183.0 [183.5] | 0.973 [0.962, 1.009] | no detectable difference |
| open(ebook) / firstPaintMs | 312.7 [316.3] | 305.6 [307.2] | 0.977 [0.964, 0.994] | no detectable difference |
| open(ebook) / settledMs | 357.2 [363.3] | 355.3 [359.8] | 0.995 [0.975, 1.009] | no detectable difference |
| open(ebook) / mainTaskMs | 240.5 [242.2] | 240.6 [243.2] | 1.000 [0.982, 1.021] | no detectable difference |
| open(ebook) / mainJsMs | 197.3 [198.2] | 196.0 [198.2] | 0.994 [0.983, 1.020] | no detectable difference |
| open(large) / readyMs | 85.3 [85.6] | 83.5 [83.9] | 0.979 [0.960, 1.008] | no detectable difference |
| open(large) / openedMs | 211.4 [211.7] | 210.7 [213.4] | 0.997 [0.982, 1.026] | no detectable difference |
| open(large) / firstPaintMs | 336.9 [348.2] | 338.2 [346.2] | 1.004 [0.957, 1.035] | no detectable difference |
| open(large) / settledMs | 338.9 [340.3] | 340.3 [348.9] | 1.004 [0.989, 1.034] | no detectable difference |
| open(large) / mainTaskMs | 522.7 [526.2] | 523.7 [532.1] | 1.002 [0.994, 1.024] | no detectable difference |
| open(large) / mainJsMs | 478.6 [485.1] | 482.1 [489.7] | 1.007 [0.989, 1.025] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2261.5 [2267.7] | 2227.4 [2280.2] | 0.985 [0.974, 1.038] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 9.4 [9.6] | 9.4 [9.8] | 1.001 [0.930, 1.061] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 761.6 [764.3] | 743.7 [766.1] | 0.976 [0.944, 1.028] | no detectable difference |
| scroll / mainJsMs | 653.6 [654.4] | 639.6 [658.5] | 0.979 [0.948, 1.026] | no detectable difference |
| jump / settleMs | 138.7 [156.9] | 129.8 [147.1] | -8.9 [-44.0, +50.7] | no detectable difference |
| jump / mainTaskMs | 142.1 [142.6] | 147.6 [147.9] | 1.039 [0.944, 1.074] | no detectable difference |
| jump / mainJsMs | 128.1 [132.2] | 130.4 [132.0] | 1.018 [0.931, 1.034] | no detectable difference |
| zoom / settleMs | 420.1 [431.6] | 430.0 [436.8] | +9.9 [-7.0, +29.1] | no detectable difference |
| zoom / zoom2SettleMs | 141.8 [144.9] | 150.4 [150.7] | +8.6 [+1.3, +9.2] | no detectable difference |
| zoom / zoom4SettleMs | 278.6 [286.8] | 283.4 [286.7] | +4.9 [-12.5, +20.5] | no detectable difference |
| zoom / backTo1SettleMs | 90.2 [103.1] | 104.1 [104.1] | +13.9 [-16.4, +16.4] | no detectable difference |
| zoom / mainTaskMs | 269.5 [279.3] | 274.8 [283.2] | 1.020 [0.954, 1.072] | no detectable difference |
| zoom / mainJsMs | 227.3 [234.0] | 233.0 [237.7] | 1.025 [0.968, 1.091] | no detectable difference |
| select / settleMs | 145.3 [145.7] | 140.7 [151.9] | -4.5 [-9.7, +8.2] | no detectable difference |
| select / mainTaskMs | 64.7 [65.2] | 64.1 [65.8] | 0.992 [0.739, 1.042] | no detectable difference |
| select / mainJsMs | 50.9 [51.2] | 50.7 [52.1] | 0.996 [0.714, 1.044] | no detectable difference |
| search / searchMs | 63.3 [66.6] | 64.1 [71.4] | 1.013 [0.879, 1.376] | no detectable difference |
| search / mainTaskMs | 15.6 [16.8] | 12.4 [19.0] | 0.795 [0.564, 2.105] | no detectable difference |
| search / mainJsMs | 7.3 [7.7] | 7.1 [8.4] | 0.972 [0.842, 1.385] | no detectable difference |
| annotate / settleMs | 165.7 [166.3] | 166.2 [166.6] | +0.5 [-2.4, +2.3] | no detectable difference |
| annotate / mainTaskMs | 78.5 [79.9] | 80.8 [85.1] | 1.029 [1.003, 1.099] | no detectable difference |
| annotate / mainJsMs | 58.9 [60.0] | 60.1 [65.1] | 1.021 [0.987, 1.126] | no detectable difference |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-bump median [p95] | plugins-head median [p95] | plugins-buildlto median [p95] | plugins-wasmopt median [p95] |
| --- | --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.35 MiB [34.36 MiB] | 34.35 MiB [34.36 MiB] | 41.31 MiB [41.32 MiB] | 41.39 MiB [41.39 MiB] | 41.31 MiB [41.32 MiB] | 41.38 MiB [42.28 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.15 MiB] | 48.16 MiB [48.17 MiB] | 56.12 MiB [56.13 MiB] | 56.20 MiB [56.22 MiB] | 56.12 MiB [56.13 MiB] | 56.19 MiB [56.21 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] |
| soak / heapCycle5Bytes | 37.42 MiB [37.43 MiB] | 37.41 MiB [37.43 MiB] | 44.12 MiB [44.13 MiB] | 44.19 MiB [44.20 MiB] | 44.14 MiB [44.14 MiB] | 44.19 MiB [45.10 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.28 MiB] | 38.27 MiB [38.27 MiB] | 44.67 MiB [44.68 MiB] | 44.73 MiB [44.75 MiB] | 44.67 MiB [44.68 MiB] | 44.73 MiB [45.64 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-bump`:

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
| wasmBytes | 8.94 MiB | 8.94 MiB |
| wireRefs | 645 | 645 |

`plugins-head`:

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

`plugins-buildlto`:

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
| wasmBytes | 8.94 MiB | 8.94 MiB |
| wireRefs | 645 | 645 |

`plugins-wasmopt`:

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

