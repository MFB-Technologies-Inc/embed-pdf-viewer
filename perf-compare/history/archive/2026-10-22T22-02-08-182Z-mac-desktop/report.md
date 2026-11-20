# Viewer perf comparison: 2026-10-22T22-02-08-182Z-mac-desktop

3 rounds, profile `desktop` (CPU throttle 1x), **quick, not for publication**.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | b5db930c78ee8b82819b713f846a4443f3000a76 (uncommitted changes) |
| Chromium / Playwright | 140.0.7339.16 / 1.55.0 |
| Node | v26.12.1 |
| OS | Darwin 27.0.0 arm64 |
| CPU | Apple M4 Max, 16 cores |
| Memory | 64 GiB |
| Power | AC Power |
| Viewport | 1280x800 @2x |
| Fixtures | ebook 2e539f62b5fa, large 10c8668fe55c |

| Arm | Ref | SHA | Payload | Engine wasm sha256 | Kernel wasm sha256 |
| --- | --- | --- | --- | --- | --- |
| main | main | 2516e2786 | fetched | 98c2368a774d |  |
| main-aa | main | 2516e2786 | fetched | 98c2368a774d |  |

## Gate

- `main`: pass
- `main-aa`: pass

## Sizes

| Metric | main | main-aa |
| --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

63 of 63 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.1 [205.4] | 45.1 [46.8] | 0.865 [0.200, 1.042] | pass |
| open(ebook) / openedMs | 108.6 [263.6] | 97.8 [102.3] | 0.900 [0.343, 1.034] | pass |
| open(ebook) / firstPaintMs | 188.8 [341.3] | 180.8 [187.3] | 0.958 [0.483, 1.036] | pass |
| open(ebook) / settledMs | 291.7 [443.6] | 275.0 [288.8] | 0.943 [0.597, 1.050] | pass |
| open(ebook) / mainTaskMs | 118.1 [126.2] | 121.5 [130.0] | 1.029 [0.891, 1.157] | pass |
| open(ebook) / mainJsMs | 97.5 [139.6] | 99.5 [105.4] | 1.020 [0.636, 1.151] | pass |
| open(large) / readyMs | 44.1 [47.4] | 44.8 [47.1] | 1.015 [0.936, 1.118] | pass |
| open(large) / openedMs | 112.1 [118.3] | 115.2 [117.2] | 1.027 [0.941, 1.073] | pass |
| open(large) / firstPaintMs | 179.9 [189.1] | 175.9 [186.4] | 0.978 [0.909, 1.061] | pass |
| open(large) / settledMs | 190.5 [200.0] | 186.8 [197.1] | 0.981 [0.913, 1.057] | pass |
| open(large) / mainTaskMs | 220.9 [232.4] | 223.5 [231.0] | 1.012 [0.933, 1.052] | pass |
| open(large) / mainJsMs | 201.6 [210.4] | 204.1 [209.1] | 1.013 [0.940, 1.043] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2000.1 [2001.2] | 2001.0 [2005.3] | 1.000 [0.999, 1.007] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 0.999 [0.999, 1.000] | pass |
| scroll / frameP95Ms | 9.1 [9.8] | 9.3 [10.2] | 1.019 [0.892, 1.204] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 480.6 [482.9] | 478.1 [483.3] | 0.995 [0.841, 1.385] | pass |
| scroll / mainJsMs | 386.4 [398.3] | 388.9 [394.9] | 1.006 [0.830, 1.406] | pass |
| coldScroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / wallMs | 1999.4 [2014.4] | 2000.3 [2005.3] | 1.000 [0.992, 1.004] | pass |
| coldScroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 0.999 [0.999, 1.001] | pass |
| coldScroll / frameP95Ms | 9.2 [9.7] | 9.0 [9.6] | 0.974 [0.904, 1.069] | pass |
| coldScroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| coldScroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| coldScroll / mainTaskMs | 505.0 [525.0] | 520.9 [531.1] | 1.031 [0.829, 1.195] | pass |
| coldScroll / mainJsMs | 403.1 [404.2] | 408.3 [416.5] | 1.013 [0.855, 1.197] | pass |
| jump / settleMs | 84.3 [85.6] | 83.0 [85.4] | -1.3 [-8.1, +8.1] | pass |
| jump / mainTaskMs | 71.3 [73.7] | 72.6 [79.0] | 1.019 [0.941, 1.172] | pass |
| jump / mainJsMs | 60.7 [61.3] | 63.7 [66.1] | 1.050 [0.918, 1.203] | pass |
| zoom / settleMs | 325.5 [350.2] | 330.1 [333.0] | +4.6 [-30.8, +11.7] | pass |
| zoom / zoom2SettleMs | 114.2 [123.0] | 113.2 [122.9] | -1.0 [-12.0, +10.7] | pass |
| zoom / zoom4SettleMs | 211.4 [227.2] | 216.9 [220.8] | +5.5 [-30.7, +12.9] | pass |
| zoom / backTo1SettleMs | 93.8 [96.6] | 90.8 [90.9] | -2.9 [-11.4, +0.1] | pass |
| zoom / mainTaskMs | 274.3 [276.1] | 268.0 [274.7] | 0.977 [0.957, 1.033] | pass |
| zoom / mainJsMs | 231.4 [235.5] | 224.2 [235.3] | 0.969 [0.948, 1.033] | pass |
| zoomLarge / settleMs | 212.1 [212.2] | 220.3 [226.6] | +8.2 [-9.9, +36.1] | pass |
| zoomLarge / zoom2SettleMs | 95.7 [104.1] | 109.0 [109.2] | +13.3 [-9.2, +17.2] | pass |
| zoomLarge / zoom4SettleMs | 107.2 [115.5] | 111.3 [117.4] | +4.1 [-10.0, +18.9] | pass |
| zoomLarge / backTo1SettleMs | 89.4 [95.5] | 96.4 [97.5] | +7.0 [-3.2, +16.9] | pass |
| zoomLarge / mainTaskMs | 225.1 [244.9] | 252.3 [260.2] | 1.121 [0.948, 1.210] | pass |
| zoomLarge / mainJsMs | 197.3 [214.7] | 222.5 [229.8] | 1.128 [0.936, 1.239] | pass |
| select / settleMs | 139.9 [142.6] | 141.3 [146.1] | +1.4 [-2.3, +11.2] | pass |
| select / mainTaskMs | 72.9 [96.0] | 77.4 [90.4] | 1.063 [0.673, 1.321] | pass |
| select / mainJsMs | 59.6 [77.9] | 62.1 [76.7] | 1.042 [0.646, 1.483] | pass |
| selectLarge / settleMs | 140.0 [141.8] | 138.4 [140.4] | -1.6 [-4.0, +0.7] | pass |
| selectLarge / mainTaskMs | 72.0 [94.9] | 69.2 [93.7] | 0.960 [0.468, 1.384] | pass |
| selectLarge / mainJsMs | 50.7 [80.0] | 54.9 [78.8] | 1.084 [0.379, 1.637] | pass |
| search / searchMs | 53.4 [57.0] | 37.7 [52.5] | 0.706 [0.598, 1.092] | pass |
| search / mainTaskMs | 10.2 [12.4] | 7.5 [10.7] | 0.734 [0.412, 1.092] | pass |
| search / mainJsMs | 5.1 [6.2] | 3.9 [5.2] | 0.767 [0.581, 1.134] | pass |
| searchLarge / searchMs | 1259.8 [1362.5] | 1267.0 [1332.2] | 1.006 [0.901, 1.085] | pass |
| searchLarge / mainTaskMs | 220.0 [257.6] | 221.7 [251.6] | 1.008 [0.829, 1.162] | pass |
| searchLarge / mainJsMs | 192.9 [224.3] | 195.7 [219.3] | 1.014 [0.840, 1.151] | pass |
| annotate / settleMs | 150.9 [152.1] | 153.7 [155.9] | +2.7 [+0.5, +5.4] | pass |
| annotate / mainTaskMs | 74.5 [94.9] | 82.4 [93.1] | 1.105 [0.784, 1.778] | pass |
| annotate / mainJsMs | 55.9 [77.2] | 59.8 [75.7] | 1.070 [0.709, 1.974] | pass |
| annotateLarge / settleMs | 156.8 [156.9] | 155.8 [158.3] | -1.0 [-10.6, +4.4] | pass |
| annotateLarge / mainTaskMs | 78.1 [95.1] | 80.6 [100.4] | 1.033 [0.574, 2.114] | pass |
| annotateLarge / mainJsMs | 53.3 [72.9] | 55.8 [79.1] | 1.047 [0.509, 2.445] | pass |

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

## Memory

| Row | main median [p95] | main-aa median [p95] |
| --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.40 MiB [34.41 MiB] | 34.40 MiB [34.41 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] |
| open(large) / heapAfterOpenBytes | 48.19 MiB [48.20 MiB] | 48.21 MiB [48.22 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.28 MiB] |
| soak / heapCycle5Bytes | 37.47 MiB [37.48 MiB] | 37.47 MiB [37.47 MiB] |
| soak / heapCycle30Bytes | 38.30 MiB [38.31 MiB] | 38.29 MiB [38.29 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] |
