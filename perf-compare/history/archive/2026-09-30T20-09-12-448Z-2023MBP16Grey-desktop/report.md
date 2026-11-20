# Viewer perf comparison: 2026-09-30T20-09-12-448Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 8d21589dc2130d94712a6082ae55e2a016d463de (uncommitted changes) |
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
| plugins-getstate | research/rust-kernel-wire | 776f0c265 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 764d1ef49e62 |
| plugins-bump | research/rust-kernel-bump | eba60e600 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 764d1ef49e62 |
| plugins-item1 | research/rust-kernel-toolbar-gate | 1efa42de6 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 764d1ef49e62 |
| plugins-item3 | research/rust-kernel-open-surfaces | eeea6d289 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 764d1ef49e62 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-getstate`: pass
- `plugins-bump`: pass
- `plugins-item1`: pass
- `plugins-item3`: pass

## Sizes

| Metric | main | main-aa | plugins-getstate | plugins-bump | plugins-item1 | plugins-item3 |
| --- | --- | --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 11.69 MiB | 11.69 MiB | 11.69 MiB | 11.69 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.88 MiB | 1.88 MiB | 1.88 MiB | 1.88 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.62 MiB | 2.62 MiB | 2.62 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

39 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.9 [54.5] | 54.4 [54.8] | 1.009 [0.957, 1.021] | pass |
| open(ebook) / openedMs | 116.8 [118.5] | 117.6 [117.8] | 1.007 [0.960, 1.019] | pass |
| open(ebook) / firstPaintMs | 212.3 [216.1] | 210.4 [217.2] | 0.991 [0.951, 1.038] | pass |
| open(ebook) / settledMs | 333.2 [334.2] | 335.8 [337.4] | 1.008 [0.983, 1.028] | pass |
| open(ebook) / mainTaskMs | 117.0 [117.9] | 116.6 [118.6] | 0.996 [0.974, 1.045] | pass |
| open(ebook) / mainScriptMs | 19.4 [19.7] | 20.0 [20.5] | 1.027 [0.995, 1.076] | pass |
| open(large) / readyMs | 55.4 [57.6] | 54.1 [56.3] | 0.976 [0.931, 1.082] | pass |
| open(large) / openedMs | 135.9 [138.8] | 134.3 [138.0] | 0.988 [0.961, 1.048] | pass |
| open(large) / firstPaintMs | 226.1 [230.2] | 223.2 [231.5] | 0.987 [0.943, 1.042] | pass |
| open(large) / settledMs | 224.7 [227.2] | 219.4 [228.3] | 0.976 [0.942, 1.046] | pass |
| open(large) / mainTaskMs | 260.3 [267.7] | 255.7 [265.1] | 0.982 [0.950, 1.036] | pass |
| open(large) / mainScriptMs | 23.6 [23.7] | 23.0 [24.3] | 0.971 [0.967, 1.054] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2015.9 [2018.2] | 2010.3 [2015.0] | 0.997 [0.992, 1.007] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [8.5] | 8.6 [8.7] | 1.020 [0.982, 1.045] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 405.5 [413.8] | 401.0 [415.7] | 0.989 [0.931, 1.046] | pass |
| scroll / mainScriptMs | 54.6 [56.4] | 53.9 [56.3] | 0.987 [0.943, 1.054] | pass |
| jump / settleMs | 98.4 [100.0] | 95.3 [99.4] | -3.1 [-5.3, +6.8] | pass |
| jump / mainTaskMs | 56.8 [57.2] | 55.0 [57.3] | 0.969 [0.935, 1.036] | pass |
| jump / mainScriptMs | 15.8 [17.1] | 16.4 [16.9] | 1.034 [0.879, 1.077] | pass |
| zoom / settleMs | 339.6 [354.7] | 356.1 [361.9] | +16.5 [-19.8, +28.2] | pass |
| zoom / zoom2SettleMs | 129.2 [136.0] | 134.1 [134.7] | +4.8 [-3.7, +7.4] | pass |
| zoom / zoom4SettleMs | 205.0 [226.5] | 221.3 [227.7] | +16.3 [-25.4, +25.5] | pass |
| zoom / backTo1SettleMs | 66.1 [68.4] | 67.6 [79.1] | +1.5 [-4.0, +23.6] | pass |
| zoom / mainTaskMs | 164.4 [166.3] | 166.8 [167.2] | 1.014 [0.993, 1.045] | pass |
| zoom / mainScriptMs | 25.8 [26.7] | 23.4 [25.5] | 0.910 [0.860, 1.114] | pass |
| select / settleMs | 147.2 [148.1] | 150.9 [151.1] | +3.7 [-9.6, +6.4] | pass |
| select / mainTaskMs | 45.7 [46.0] | 45.8 [48.2] | 1.003 [0.824, 1.094] | pass |
| select / mainScriptMs | 10.3 [10.4] | 9.8 [10.4] | 0.951 [0.844, 1.113] | pass |
| search / searchMs | 58.9 [59.5] | 60.9 [63.1] | 1.034 [0.979, 1.084] | pass |
| search / mainTaskMs | 4.7 [4.8] | 4.5 [4.8] | 0.963 [0.924, 1.041] | pass |
| search / mainScriptMs | 0.1 [0.2] | 0.2 [0.2] | 1.323 [0.791, 1.573] | pass |
| annotate / settleMs | 157.6 [161.8] | 157.3 [162.3] | -0.3 [-10.0, +9.7] | pass |
| annotate / mainTaskMs | 56.9 [59.8] | 58.7 [60.3] | 1.032 [0.907, 1.336] | pass |
| annotate / mainScriptMs | 11.0 [11.5] | 11.1 [11.4] | 1.012 [0.937, 1.291] | pass |

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

Not run: `plugins-wire`.

## `plugins-lto` vs `plugins-getstate`

Not run: `plugins-lto`.

## `main` vs `plugins-getstate`

| Row | main median [p95] | plugins-getstate median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.9 [54.5] | 83.1 [85.2] | 1.543 [1.517, 1.591] | slower |
| open(ebook) / openedMs | 116.8 [118.5] | 182.6 [183.1] | 1.563 [1.513, 1.584] | slower |
| open(ebook) / firstPaintMs | 212.3 [216.1] | 315.2 [316.2] | 1.485 [1.441, 1.507] | slower |
| open(ebook) / settledMs | 333.2 [334.2] | 364.1 [365.4] | 1.093 [1.081, 1.113] | slower |
| open(ebook) / mainTaskMs | 117.0 [117.9] | 250.7 [251.2] | 2.143 [2.115, 2.210] | slower |
| open(ebook) / mainScriptMs | 19.4 [19.7] | 107.2 [107.3] | 5.515 [5.390, 5.619] | slower |
| open(large) / readyMs | 55.4 [57.6] | 82.7 [84.0] | 1.492 [1.422, 1.609] | slower |
| open(large) / openedMs | 135.9 [138.8] | 211.4 [212.1] | 1.555 [1.495, 1.607] | slower |
| open(large) / firstPaintMs | 226.1 [230.2] | 340.8 [344.3] | 1.507 [1.463, 1.545] | slower |
| open(large) / settledMs | 224.7 [227.2] | 348.2 [348.5] | 1.550 [1.518, 1.590] | slower |
| open(large) / mainTaskMs | 260.3 [267.7] | 520.8 [522.1] | 2.001 [1.930, 2.033] | slower |
| open(large) / mainScriptMs | 23.6 [23.7] | 321.9 [322.9] | 13.614 [13.543, 13.910] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2015.9 [2018.2] | 2414.3 [2452.9] | 1.198 [1.122, 1.227] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.5] | 8.4 [8.8] | 1.001 [0.988, 1.052] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 405.5 [413.8] | 872.9 [882.0] | 2.153 [1.999, 2.214] | slower |
| scroll / mainScriptMs | 54.6 [56.4] | 364.4 [373.7] | 6.674 [6.307, 6.983] | slower |
| jump / settleMs | 98.4 [100.0] | 145.4 [155.2] | +46.9 [+33.0, +63.2] | slower |
| jump / mainTaskMs | 56.8 [57.2] | 146.7 [182.5] | 2.584 [2.272, 3.358] | slower |
| jump / mainScriptMs | 15.8 [17.1] | 94.6 [122.4] | 5.975 [5.098, 7.968] | slower |
| zoom / settleMs | 339.6 [354.7] | 392.4 [397.2] | +52.7 [+27.5, +63.5] | slower |
| zoom / zoom2SettleMs | 129.2 [136.0] | 154.9 [158.0] | +25.7 [+10.0, +31.0] | slower |
| zoom / zoom4SettleMs | 205.0 [226.5] | 234.0 [249.3] | +29.0 [+0.1, +48.1] | slower |
| zoom / backTo1SettleMs | 66.1 [68.4] | 80.8 [82.4] | +14.6 [+2.4, +25.9] | no detectable difference |
| zoom / mainTaskMs | 164.4 [166.3] | 297.8 [301.5] | 1.811 [1.738, 1.887] | slower |
| zoom / mainScriptMs | 25.8 [26.7] | 117.9 [119.4] | 4.577 [4.282, 5.170] | slower |
| select / settleMs | 147.2 [148.1] | 142.4 [147.1] | -4.8 [-8.7, +2.9] | no detectable difference |
| select / mainTaskMs | 45.7 [46.0] | 73.1 [75.5] | 1.601 [1.554, 1.709] | slower |
| select / mainScriptMs | 10.3 [10.4] | 35.5 [36.7] | 3.438 [3.343, 3.899] | slower |
| search / searchMs | 58.9 [59.5] | 64.9 [71.4] | 1.102 [0.885, 1.235] | no detectable difference |
| search / mainTaskMs | 4.7 [4.8] | 19.2 [20.7] | 4.100 [2.571, 4.500] | slower |
| search / mainScriptMs | 0.1 [0.2] | 7.1 [7.4] | 55.913 [37.945, 59.597] | slower |
| annotate / settleMs | 157.6 [161.8] | 166.7 [168.6] | +9.0 [+3.9, +15.7] | no detectable difference |
| annotate / mainTaskMs | 56.9 [59.8] | 94.4 [95.9] | 1.659 [1.522, 2.122] | slower |
| annotate / mainScriptMs | 11.0 [11.5] | 42.5 [44.2] | 3.863 [3.612, 5.028] | slower |

## `plugins-getstate` vs `plugins-toolbar`

Not run: `plugins-toolbar`.

## `main` vs `plugins-toolbar`

Not run: `plugins-toolbar`.

## `plugins-toolbar` vs `plugins-bump`

Not run: `plugins-toolbar`.

## `main` vs `plugins-bump`

| Row | main median [p95] | plugins-bump median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 53.9 [54.5] | 84.0 [84.5] | 1.559 [1.532, 1.574] | slower |
| open(ebook) / openedMs | 116.8 [118.5] | 183.2 [184.3] | 1.569 [1.538, 1.595] | slower |
| open(ebook) / firstPaintMs | 212.3 [216.1] | 308.8 [314.7] | 1.455 [1.423, 1.502] | slower |
| open(ebook) / settledMs | 333.2 [334.2] | 355.2 [361.0] | 1.066 [1.060, 1.101] | slower |
| open(ebook) / mainTaskMs | 117.0 [117.9] | 237.9 [241.7] | 2.033 [2.016, 2.130] | slower |
| open(ebook) / mainScriptMs | 19.4 [19.7] | 112.2 [114.6] | 5.772 [5.616, 6.015] | slower |
| open(large) / readyMs | 55.4 [57.6] | 85.0 [85.0] | 1.533 [1.422, 1.625] | slower |
| open(large) / openedMs | 135.9 [138.8] | 211.6 [213.8] | 1.557 [1.509, 1.621] | slower |
| open(large) / firstPaintMs | 226.1 [230.2] | 335.2 [341.5] | 1.482 [1.432, 1.534] | slower |
| open(large) / settledMs | 224.7 [227.2] | 337.8 [342.4] | 1.503 [1.461, 1.564] | slower |
| open(large) / mainTaskMs | 260.3 [267.7] | 518.2 [521.5] | 1.991 [1.904, 2.031] | slower |
| open(large) / mainScriptMs | 23.6 [23.7] | 394.6 [398.4] | 16.689 [16.411, 17.171] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2015.9 [2018.2] | 2214.1 [2235.7] | 1.098 [1.059, 1.118] | slower |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.5] | 8.8 [8.9] | 1.048 [1.002, 1.071] | slower |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 405.5 [413.8] | 719.1 [741.2] | 1.773 [1.645, 1.865] | slower |
| scroll / mainScriptMs | 54.6 [56.4] | 378.8 [391.0] | 6.937 [6.413, 7.312] | slower |
| jump / settleMs | 98.4 [100.0] | 116.8 [155.9] | +18.4 [+11.3, +67.2] | slower |
| jump / mainTaskMs | 56.8 [57.2] | 144.1 [147.3] | 2.538 [2.514, 2.659] | slower |
| jump / mainScriptMs | 15.8 [17.1] | 94.5 [97.6] | 5.972 [5.438, 6.218] | slower |
| zoom / settleMs | 339.6 [354.7] | 397.8 [410.1] | +58.2 [+18.9, +77.2] | slower |
| zoom / zoom2SettleMs | 129.2 [136.0] | 156.7 [157.7] | +27.5 [-2.6, +30.4] | no detectable difference |
| zoom / zoom4SettleMs | 205.0 [226.5] | 241.1 [252.4] | +36.1 [+12.2, +50.7] | slower |
| zoom / backTo1SettleMs | 66.1 [68.4] | 85.3 [99.5] | +19.2 [+9.7, +44.4] | no detectable difference |
| zoom / mainTaskMs | 164.4 [166.3] | 260.0 [263.9] | 1.581 [1.547, 1.652] | slower |
| zoom / mainScriptMs | 25.8 [26.7] | 125.7 [128.3] | 4.877 [4.669, 5.561] | slower |
| select / settleMs | 147.2 [148.1] | 137.1 [141.3] | -10.1 [-13.0, -2.9] | faster |
| select / mainTaskMs | 45.7 [46.0] | 50.6 [62.4] | 1.107 [1.001, 1.438] | no detectable difference |
| select / mainScriptMs | 10.3 [10.4] | 31.9 [40.2] | 3.085 [2.683, 4.355] | slower |
| search / searchMs | 58.9 [59.5] | 71.0 [72.7] | 1.205 [0.877, 1.248] | no detectable difference |
| search / mainTaskMs | 4.7 [4.8] | 19.3 [20.4] | 4.112 [2.274, 4.436] | slower |
| search / mainScriptMs | 0.1 [0.2] | 7.8 [8.2] | 61.732 [38.540, 66.145] | slower |
| annotate / settleMs | 157.6 [161.8] | 166.0 [168.0] | +8.4 [+3.2, +15.0] | no detectable difference |
| annotate / mainTaskMs | 56.9 [59.8] | 83.7 [85.1] | 1.471 [1.391, 1.883] | slower |
| annotate / mainScriptMs | 11.0 [11.5] | 49.2 [49.4] | 4.468 [4.177, 5.596] | slower |

## `plugins-getstate` vs `plugins-item1`

| Row | plugins-getstate median [p95] | plugins-item1 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 83.1 [85.2] | 82.7 [83.7] | 0.995 [0.965, 1.013] | no detectable difference |
| open(ebook) / openedMs | 182.6 [183.1] | 184.6 [184.8] | 1.011 [0.985, 1.029] | no detectable difference |
| open(ebook) / firstPaintMs | 315.2 [316.2] | 307.2 [307.4] | 0.975 [0.958, 0.986] | no detectable difference |
| open(ebook) / settledMs | 364.1 [365.4] | 354.7 [354.8] | 0.974 [0.960, 0.982] | no detectable difference |
| open(ebook) / mainTaskMs | 250.7 [251.2] | 236.1 [239.3] | 0.942 [0.935, 0.960] | faster |
| open(ebook) / mainScriptMs | 107.2 [107.3] | 109.9 [113.2] | 1.025 [1.024, 1.068] | no detectable difference |
| open(large) / readyMs | 82.7 [84.0] | 82.0 [83.7] | 0.991 [0.970, 1.021] | no detectable difference |
| open(large) / openedMs | 211.4 [212.1] | 209.6 [211.0] | 0.991 [0.984, 1.015] | no detectable difference |
| open(large) / firstPaintMs | 340.8 [344.3] | 337.6 [340.1] | 0.991 [0.966, 1.009] | no detectable difference |
| open(large) / settledMs | 348.2 [348.5] | 339.1 [341.2] | 0.974 [0.962, 0.989] | no detectable difference |
| open(large) / mainTaskMs | 520.8 [522.1] | 520.8 [522.2] | 1.000 [0.993, 1.008] | no detectable difference |
| open(large) / mainScriptMs | 321.9 [322.9] | 398.8 [399.6] | 1.239 [1.222, 1.245] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2414.3 [2452.9] | 2331.7 [2382.5] | 0.966 [0.905, 1.054] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.8] | 8.5 [8.9] | 1.008 [0.953, 1.063] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 872.9 [882.0] | 803.5 [807.6] | 0.920 [0.896, 0.975] | faster |
| scroll / mainScriptMs | 364.4 [373.7] | 409.8 [412.9] | 1.125 [1.089, 1.158] | slower |
| jump / settleMs | 145.4 [155.2] | 144.7 [157.7] | -0.7 [-42.6, +26.0] | no detectable difference |
| jump / mainTaskMs | 146.7 [182.5] | 145.1 [147.1] | 0.990 [0.758, 1.133] | no detectable difference |
| jump / mainScriptMs | 94.6 [122.4] | 95.5 [98.8] | 1.010 [0.737, 1.129] | no detectable difference |
| zoom / settleMs | 392.4 [397.2] | 395.4 [411.6] | +3.0 [-10.2, +29.5] | no detectable difference |
| zoom / zoom2SettleMs | 154.9 [158.0] | 156.3 [161.9] | +1.5 [-12.2, +15.7] | no detectable difference |
| zoom / zoom4SettleMs | 234.0 [249.3] | 241.4 [255.5] | +7.3 [-18.1, +28.1] | no detectable difference |
| zoom / backTo1SettleMs | 80.8 [82.4] | 88.6 [99.8] | +7.9 [+3.5, +30.0] | no detectable difference |
| zoom / mainTaskMs | 297.8 [301.5] | 296.6 [299.2] | 0.996 [0.901, 1.034] | no detectable difference |
| zoom / mainScriptMs | 117.9 [119.4] | 138.6 [142.6] | 1.175 [1.108, 1.246] | slower |
| select / settleMs | 142.4 [147.1] | 149.0 [153.7] | +6.6 [-1.8, +14.6] | no detectable difference |
| select / mainTaskMs | 73.1 [75.5] | 67.6 [69.9] | 0.924 [0.889, 0.981] | no detectable difference |
| select / mainScriptMs | 35.5 [36.7] | 41.8 [42.6] | 1.176 [1.123, 1.227] | slower |
| search / searchMs | 64.9 [71.4] | 71.9 [73.2] | 1.108 [0.886, 1.391] | no detectable difference |
| search / mainTaskMs | 19.2 [20.7] | 20.0 [20.4] | 1.040 [0.523, 1.637] | no detectable difference |
| search / mainScriptMs | 7.1 [7.4] | 8.0 [8.4] | 1.123 [0.888, 1.365] | no detectable difference |
| annotate / settleMs | 166.7 [168.6] | 165.8 [166.3] | -0.9 [-5.7, +0.2] | no detectable difference |
| annotate / mainTaskMs | 94.4 [95.9] | 87.5 [88.6] | 0.927 [0.884, 0.970] | no detectable difference |
| annotate / mainScriptMs | 42.5 [44.2] | 48.1 [48.8] | 1.130 [1.058, 1.172] | no detectable difference |

## `plugins-getstate` vs `plugins-item3`

| Row | plugins-getstate median [p95] | plugins-item3 median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 83.1 [85.2] | 83.9 [84.0] | 1.009 [0.961, 1.016] | no detectable difference |
| open(ebook) / openedMs | 182.6 [183.1] | 182.8 [184.9] | 1.002 [0.985, 1.031] | no detectable difference |
| open(ebook) / firstPaintMs | 315.2 [316.2] | 309.1 [310.1] | 0.981 [0.975, 0.995] | no detectable difference |
| open(ebook) / settledMs | 364.1 [365.4] | 360.1 [365.1] | 0.989 [0.984, 1.013] | no detectable difference |
| open(ebook) / mainTaskMs | 250.7 [251.2] | 251.4 [256.4] | 1.003 [0.993, 1.029] | no detectable difference |
| open(ebook) / mainScriptMs | 107.2 [107.3] | 106.8 [109.9] | 0.996 [0.985, 1.037] | no detectable difference |
| open(large) / readyMs | 82.7 [84.0] | 84.0 [85.0] | 1.016 [0.995, 1.035] | no detectable difference |
| open(large) / openedMs | 211.4 [212.1] | 211.7 [215.7] | 1.002 [0.983, 1.039] | no detectable difference |
| open(large) / firstPaintMs | 340.8 [344.3] | 342.8 [349.7] | 1.006 [0.980, 1.039] | no detectable difference |
| open(large) / settledMs | 348.2 [348.5] | 348.2 [354.8] | 1.000 [0.977, 1.030] | no detectable difference |
| open(large) / mainTaskMs | 520.8 [522.1] | 520.6 [528.3] | 0.999 [0.987, 1.021] | no detectable difference |
| open(large) / mainScriptMs | 321.9 [322.9] | 321.4 [326.9] | 0.998 [0.975, 1.020] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2414.3 [2452.9] | 2365.1 [2434.3] | 0.980 [0.962, 1.078] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.8] | 8.4 [8.6] | 1.000 [0.960, 1.017] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 872.9 [882.0] | 856.8 [876.5] | 0.981 [0.933, 1.060] | no detectable difference |
| scroll / mainScriptMs | 364.4 [373.7] | 359.3 [369.3] | 0.986 [0.921, 1.038] | no detectable difference |
| jump / settleMs | 145.4 [155.2] | 153.1 [154.9] | +7.7 [-11.9, +21.9] | no detectable difference |
| jump / mainTaskMs | 146.7 [182.5] | 186.1 [195.0] | 1.269 [0.989, 1.508] | no detectable difference |
| jump / mainScriptMs | 94.6 [122.4] | 128.4 [130.0] | 1.358 [1.011, 1.482] | slower |
| zoom / settleMs | 392.4 [397.2] | 391.3 [406.9] | -1.1 [-24.3, +24.7] | no detectable difference |
| zoom / zoom2SettleMs | 154.9 [158.0] | 151.8 [154.0] | -3.1 [-10.0, +7.5] | no detectable difference |
| zoom / zoom4SettleMs | 234.0 [249.3] | 239.4 [252.9] | +5.4 [-26.0, +25.4] | no detectable difference |
| zoom / backTo1SettleMs | 80.8 [82.4] | 91.2 [97.1] | +10.5 [+4.2, +26.8] | no detectable difference |
| zoom / mainTaskMs | 297.8 [301.5] | 304.5 [305.4] | 1.022 [0.930, 1.055] | no detectable difference |
| zoom / mainScriptMs | 117.9 [119.4] | 119.2 [119.9] | 1.011 [0.947, 1.045] | no detectable difference |
| select / settleMs | 142.4 [147.1] | 143.8 [147.5] | +1.4 [-7.0, +8.4] | no detectable difference |
| select / mainTaskMs | 73.1 [75.5] | 73.2 [74.5] | 1.001 [0.942, 1.043] | no detectable difference |
| select / mainScriptMs | 35.5 [36.7] | 35.3 [35.8] | 0.994 [0.935, 1.029] | no detectable difference |
| search / searchMs | 64.9 [71.4] | 74.0 [74.3] | 1.141 [1.012, 1.409] | slower |
| search / mainTaskMs | 19.2 [20.7] | 23.8 [24.5] | 1.240 [1.028, 1.964] | slower |
| search / mainScriptMs | 7.1 [7.4] | 8.1 [8.5] | 1.144 [0.997, 1.375] | no detectable difference |
| annotate / settleMs | 166.7 [168.6] | 167.8 [168.2] | +1.1 [-5.0, +2.1] | no detectable difference |
| annotate / mainTaskMs | 94.4 [95.9] | 92.9 [95.7] | 0.984 [0.956, 1.049] | no detectable difference |
| annotate / mainScriptMs | 42.5 [44.2] | 43.0 [43.6] | 1.011 [0.956, 1.046] | no detectable difference |

## `plugins-getstate` vs `plugins-bump`

| Row | plugins-getstate median [p95] | plugins-bump median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 83.1 [85.2] | 84.0 [84.5] | 1.011 [0.978, 1.022] | no detectable difference |
| open(ebook) / openedMs | 182.6 [183.1] | 183.2 [184.3] | 1.004 [0.997, 1.027] | no detectable difference |
| open(ebook) / firstPaintMs | 315.2 [316.2] | 308.8 [314.7] | 0.980 [0.974, 1.011] | no detectable difference |
| open(ebook) / settledMs | 364.1 [365.4] | 355.2 [361.0] | 0.976 [0.970, 1.001] | no detectable difference |
| open(ebook) / mainTaskMs | 250.7 [251.2] | 237.9 [241.7] | 0.949 [0.947, 0.970] | faster |
| open(ebook) / mainScriptMs | 107.2 [107.3] | 112.2 [114.6] | 1.046 [1.033, 1.080] | no detectable difference |
| open(large) / readyMs | 82.7 [84.0] | 85.0 [85.0] | 1.027 [0.977, 1.033] | no detectable difference |
| open(large) / openedMs | 211.4 [212.1] | 211.6 [213.8] | 1.001 [0.990, 1.029] | no detectable difference |
| open(large) / firstPaintMs | 340.8 [344.3] | 335.2 [341.5] | 0.984 [0.958, 1.014] | no detectable difference |
| open(large) / settledMs | 348.2 [348.5] | 337.8 [342.4] | 0.970 [0.954, 0.993] | no detectable difference |
| open(large) / mainTaskMs | 520.8 [522.1] | 518.2 [521.5] | 0.995 [0.979, 1.007] | no detectable difference |
| open(large) / mainScriptMs | 321.9 [322.9] | 394.6 [398.4] | 1.226 [1.204, 1.242] | slower |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2414.3 [2452.9] | 2214.1 [2235.7] | 0.917 [0.870, 0.988] | faster |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.8] | 8.8 [8.9] | 1.047 [0.972, 1.062] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 872.9 [882.0] | 719.1 [741.2] | 0.824 [0.773, 0.897] | faster |
| scroll / mainScriptMs | 364.4 [373.7] | 378.8 [391.0] | 1.039 [0.969, 1.099] | no detectable difference |
| jump / settleMs | 145.4 [155.2] | 116.8 [155.9] | -28.6 [-44.8, +27.1] | no detectable difference |
| jump / mainTaskMs | 146.7 [182.5] | 144.1 [147.3] | 0.983 [0.771, 1.136] | no detectable difference |
| jump / mainScriptMs | 94.6 [122.4] | 94.5 [97.6] | 1.000 [0.747, 1.115] | no detectable difference |
| zoom / settleMs | 392.4 [397.2] | 397.8 [410.1] | +5.4 [-22.6, +27.6] | no detectable difference |
| zoom / zoom2SettleMs | 154.9 [158.0] | 156.7 [157.7] | +1.8 [-24.3, +11.1] | no detectable difference |
| zoom / zoom4SettleMs | 234.0 [249.3] | 241.1 [252.4] | +7.1 [-9.9, +24.6] | no detectable difference |
| zoom / backTo1SettleMs | 80.8 [82.4] | 85.3 [99.5] | +4.6 [-4.2, +30.1] | no detectable difference |
| zoom / mainTaskMs | 297.8 [301.5] | 260.0 [263.9] | 0.873 [0.854, 0.913] | faster |
| zoom / mainScriptMs | 117.9 [119.4] | 125.7 [128.3] | 1.066 [1.047, 1.120] | no detectable difference |
| select / settleMs | 142.4 [147.1] | 137.1 [141.3] | -5.3 [-12.3, +2.2] | no detectable difference |
| select / mainTaskMs | 73.1 [75.5] | 50.6 [62.4] | 0.691 [0.608, 0.891] | faster |
| select / mainScriptMs | 35.5 [36.7] | 31.9 [40.2] | 0.897 [0.758, 1.183] | no detectable difference |
| search / searchMs | 64.9 [71.4] | 71.0 [72.7] | 1.094 [0.725, 1.381] | no detectable difference |
| search / mainTaskMs | 19.2 [20.7] | 19.3 [20.4] | 1.003 [0.529, 1.647] | no detectable difference |
| search / mainScriptMs | 7.1 [7.4] | 7.8 [8.2] | 1.104 [0.850, 1.326] | no detectable difference |
| annotate / settleMs | 166.7 [168.6] | 166.0 [168.0] | -0.6 [-3.4, +2.0] | no detectable difference |
| annotate / mainTaskMs | 94.4 [95.9] | 83.7 [85.1] | 0.887 [0.870, 0.932] | no detectable difference |
| annotate / mainScriptMs | 42.5 [44.2] | 49.2 [49.4] | 1.156 [1.088, 1.183] | no detectable difference |

## `plugins-item1` vs `plugins-bump`

| Row | plugins-item1 median [p95] | plugins-bump median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 82.7 [83.7] | 84.0 [84.5] | 1.016 [0.997, 1.025] | no detectable difference |
| open(ebook) / openedMs | 184.6 [184.8] | 183.2 [184.3] | 0.992 [0.988, 1.023] | no detectable difference |
| open(ebook) / firstPaintMs | 307.2 [307.4] | 308.8 [314.7] | 1.005 [1.002, 1.041] | no detectable difference |
| open(ebook) / settledMs | 354.7 [354.8] | 355.2 [361.0] | 1.001 [0.999, 1.031] | no detectable difference |
| open(ebook) / mainTaskMs | 236.1 [239.3] | 237.9 [241.7] | 1.008 [0.993, 1.031] | no detectable difference |
| open(ebook) / mainScriptMs | 109.9 [113.2] | 112.2 [114.6] | 1.021 [0.976, 1.045] | no detectable difference |
| open(large) / readyMs | 82.0 [83.7] | 85.0 [85.0] | 1.037 [0.979, 1.041] | no detectable difference |
| open(large) / openedMs | 209.6 [211.0] | 211.6 [213.8] | 1.010 [0.994, 1.026] | no detectable difference |
| open(large) / firstPaintMs | 337.6 [340.1] | 335.2 [341.5] | 0.993 [0.970, 1.027] | no detectable difference |
| open(large) / settledMs | 339.1 [341.2] | 337.8 [342.4] | 0.996 [0.973, 1.023] | no detectable difference |
| open(large) / mainTaskMs | 520.8 [522.2] | 518.2 [521.5] | 0.995 [0.979, 1.006] | no detectable difference |
| open(large) / mainScriptMs | 398.8 [399.6] | 394.6 [398.4] | 0.990 [0.973, 1.010] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2331.7 [2382.5] | 2214.1 [2235.7] | 0.950 [0.895, 1.007] | no detectable difference |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.5 [8.9] | 8.8 [8.9] | 1.039 [0.954, 1.068] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 803.5 [807.6] | 719.1 [741.2] | 0.895 [0.844, 0.940] | faster |
| scroll / mainScriptMs | 409.8 [412.9] | 378.8 [391.0] | 0.924 [0.878, 0.961] | faster |
| jump / settleMs | 144.7 [157.7] | 116.8 [155.9] | -27.9 [-47.7, +46.6] | no detectable difference |
| jump / mainTaskMs | 145.1 [147.1] | 144.1 [147.3] | 0.993 [0.977, 1.045] | no detectable difference |
| jump / mainScriptMs | 95.5 [98.8] | 94.5 [97.6] | 0.990 [0.945, 1.059] | no detectable difference |
| zoom / settleMs | 395.4 [411.6] | 397.8 [410.1] | +2.5 [-38.2, +23.9] | no detectable difference |
| zoom / zoom2SettleMs | 156.3 [161.9] | 156.7 [157.7] | +0.4 [-28.3, +11.6] | no detectable difference |
| zoom / zoom4SettleMs | 241.4 [255.5] | 241.1 [252.4] | -0.3 [-15.9, +20.7] | no detectable difference |
| zoom / backTo1SettleMs | 88.6 [99.8] | 85.3 [99.5] | -3.3 [-22.6, +15.0] | no detectable difference |
| zoom / mainTaskMs | 296.6 [299.2] | 260.0 [263.9] | 0.877 [0.861, 0.972] | faster |
| zoom / mainScriptMs | 138.6 [142.6] | 125.7 [128.3] | 0.907 [0.876, 0.971] | no detectable difference |
| select / settleMs | 149.0 [153.7] | 137.1 [141.3] | -11.9 [-18.9, -4.1] | faster |
| select / mainTaskMs | 67.6 [69.9] | 50.6 [62.4] | 0.748 [0.656, 0.946] | faster |
| select / mainScriptMs | 41.8 [42.6] | 31.9 [40.2] | 0.763 [0.654, 0.994] | faster |
| search / searchMs | 71.9 [73.2] | 71.0 [72.7] | 0.987 [0.712, 1.140] | no detectable difference |
| search / mainTaskMs | 20.0 [20.4] | 19.3 [20.4] | 0.965 [0.540, 1.887] | no detectable difference |
| search / mainScriptMs | 8.0 [8.4] | 7.8 [8.2] | 0.983 [0.744, 1.250] | no detectable difference |
| annotate / settleMs | 165.8 [166.3] | 166.0 [168.0] | +0.2 [-0.9, +5.1] | no detectable difference |
| annotate / mainTaskMs | 87.5 [88.6] | 83.7 [85.1] | 0.957 [0.942, 1.003] | no detectable difference |
| annotate / mainScriptMs | 48.1 [48.8] | 49.2 [49.4] | 1.023 [0.987, 1.051] | no detectable difference |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-getstate median [p95] | plugins-bump median [p95] | plugins-item1 median [p95] | plugins-item3 median [p95] |
| --- | --- | --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.35 MiB [34.35 MiB] | 41.51 MiB [42.40 MiB] | 41.32 MiB [41.32 MiB] | 41.29 MiB [41.31 MiB] | 41.50 MiB [41.50 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.16 MiB] | 48.15 MiB [48.16 MiB] | 56.53 MiB [56.54 MiB] | 56.12 MiB [56.14 MiB] | 56.09 MiB [56.10 MiB] | 56.52 MiB [56.52 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] |
| soak / heapCycle5Bytes | 37.43 MiB [37.43 MiB] | 37.41 MiB [37.42 MiB] | 44.29 MiB [44.29 MiB] | 44.13 MiB [44.13 MiB] | 44.11 MiB [44.12 MiB] | 44.29 MiB [44.30 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.27 MiB] | 38.26 MiB [38.28 MiB] | 44.96 MiB [44.96 MiB] | 44.69 MiB [44.69 MiB] | 44.66 MiB [44.66 MiB] | 44.95 MiB [44.96 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] | 0.02 MiB [0.02 MiB] | 0.03 MiB [0.03 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-getstate`:

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

`plugins-item1`:

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

`plugins-item3`:

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

