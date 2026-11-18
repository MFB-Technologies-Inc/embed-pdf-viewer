# Viewer perf comparison: 2026-09-30T00-48-54-249Z-mac-desktop

3 rounds, profile `desktop` (CPU throttle 1x), **quick, not for publication**.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | cdebc3b6e566d5ad82a796426c4244a9234f0343 (uncommitted changes) |
| Chromium / Playwright | 140.0.7339.16 / 1.55.0 |
| Node | v24.20.0 |
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
| plugins-fix-leak | research/rust-kernel-state-cache | 2ed312446 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 4dff8a45fd6f |
| plugins-fix-zoom | research/rust-kernel-state-cache | 8cef92c76 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | df47d777780e |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-fix-leak`: pass
- `plugins-fix-zoom`: pass

## Sizes

| Metric | main | main-aa | plugins-fix-leak | plugins-fix-zoom |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 11.68 MiB | 11.68 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.88 MiB | 1.88 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.62 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

37 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 45.2 [45.3] | 45.3 [45.7] | 1.001 [0.932, 1.072] | pass |
| open(ebook) / openedMs | 96.3 [96.8] | 96.6 [97.8] | 1.003 [0.958, 1.031] | pass |
| open(ebook) / firstPaintMs | 173.4 [174.4] | 171.3 [172.3] | 0.988 [0.972, 1.003] | pass |
| open(ebook) / settledMs | 264.5 [266.6] | 263.7 [264.1] | 0.997 [0.985, 1.013] | pass |
| open(ebook) / mainTaskMs | 93.7 [93.9] | 93.0 [93.1] | 0.993 [0.988, 1.005] | pass |
| open(ebook) / mainScriptMs | 15.7 [15.8] | 15.5 [15.7] | 0.985 [0.979, 1.023] | pass |
| open(large) / readyMs | 42.5 [42.7] | 45.4 [45.4] | 1.066 [1.046, 1.071] | A/A failed |
| open(large) / openedMs | 109.2 [110.8] | 111.8 [113.6] | 1.024 [0.997, 1.045] | pass |
| open(large) / firstPaintMs | 176.5 [177.7] | 180.7 [181.4] | 1.024 [1.016, 1.036] | pass |
| open(large) / settledMs | 172.8 [173.6] | 177.1 [177.7] | 1.025 [1.015, 1.039] | pass |
| open(large) / mainTaskMs | 205.6 [205.8] | 204.0 [204.2] | 0.992 [0.987, 1.000] | pass |
| open(large) / mainScriptMs | 18.0 [18.1] | 17.5 [17.7] | 0.970 [0.958, 0.986] | A/A failed |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2009.2 [2010.4] | 2001.9 [2009.6] | 0.996 [0.996, 1.001] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [0.999, 1.001] | pass |
| scroll / frameP95Ms | 8.8 [8.9] | 8.4 [8.4] | 0.949 [0.942, 1.002] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 366.1 [367.9] | 344.7 [367.3] | 0.941 [0.934, 1.048] | pass |
| scroll / mainScriptMs | 46.9 [47.9] | 47.0 [47.4] | 1.000 [0.978, 1.013] | pass |
| jump / settleMs | 77.3 [78.0] | 79.1 [82.3] | +1.8 [-10.6, +5.4] | pass |
| jump / mainTaskMs | 46.2 [46.9] | 48.4 [51.4] | 1.048 [0.947, 1.123] | pass |
| jump / mainScriptMs | 12.1 [12.4] | 11.9 [12.1] | 0.986 [0.890, 1.018] | pass |
| zoom / settleMs | 283.7 [284.9] | 278.6 [291.4] | -5.0 [-15.4, +30.8] | pass |
| zoom / zoom2SettleMs | 109.5 [111.7] | 104.2 [110.9] | -5.3 [-8.6, +10.3] | pass |
| zoom / zoom4SettleMs | 171.7 [175.1] | 174.4 [180.5] | +2.7 [-9.2, +20.4] | pass |
| zoom / backTo1SettleMs | 56.1 [64.5] | 58.4 [70.5] | +2.3 [-11.9, +18.2] | pass |
| zoom / mainTaskMs | 136.2 [137.2] | 133.7 [142.5] | 0.982 [0.974, 1.056] | pass |
| zoom / mainScriptMs | 19.8 [22.5] | 19.0 [22.5] | 0.961 [0.821, 1.171] | pass |
| select / settleMs | 139.6 [144.7] | 147.2 [148.1] | +7.7 [-3.0, +8.8] | pass |
| select / mainTaskMs | 39.0 [41.1] | 41.8 [43.3] | 1.072 [0.998, 1.327] | pass |
| select / mainScriptMs | 9.1 [9.7] | 9.7 [9.9] | 1.063 [0.989, 1.315] | pass |
| search / searchMs | 44.0 [51.5] | 48.9 [49.9] | 1.111 [0.906, 1.236] | pass |
| search / mainTaskMs | 4.0 [4.1] | 4.0 [4.3] | 1.009 [0.977, 1.115] | pass |
| search / mainScriptMs | 0.1 [0.1] | 0.1 [0.1] | 1.060 [0.679, 1.197] | pass |
| annotate / settleMs | 153.3 [154.8] | 152.7 [153.7] | -0.6 [-2.9, +0.7] | pass |
| annotate / mainTaskMs | 49.3 [52.4] | 48.6 [48.6] | 0.987 [0.864, 1.006] | pass |
| annotate / mainScriptMs | 9.7 [11.4] | 10.3 [10.5] | 1.063 [0.786, 1.105] | pass |

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

Not run: `plugins-fix34`.

## `plugins-fix-leak` vs `plugins-fix-zoom`

| Row | plugins-fix-leak median [p95] | plugins-fix-zoom median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 69.8 [70.5] | 67.4 [67.4] | 0.965 [0.953, 0.968] | no detectable difference |
| open(ebook) / openedMs | 148.9 [149.6] | 146.0 [148.2] | 0.981 [0.970, 0.999] | no detectable difference |
| open(ebook) / firstPaintMs | 267.9 [269.5] | 252.1 [257.4] | 0.941 [0.935, 0.988] | faster |
| open(ebook) / settledMs | 355.6 [362.7] | 292.8 [298.0] | 0.823 [0.805, 0.848] | faster |
| open(ebook) / mainTaskMs | 240.4 [244.5] | 215.2 [215.7] | 0.895 [0.872, 0.903] | faster |
| open(ebook) / mainScriptMs | 101.4 [103.1] | 95.9 [96.9] | 0.946 [0.922, 0.965] | faster |
| open(large) / readyMs | 71.3 [73.1] | 68.1 [68.6] | 0.955 [0.914, 0.992] | unreliable (A/A failed) |
| open(large) / openedMs | 172.0 [173.6] | 167.9 [169.0] | 0.976 [0.959, 1.000] | no detectable difference |
| open(large) / firstPaintMs | 303.2 [307.4] | 296.1 [301.2] | 0.977 [0.938, 1.003] | no detectable difference |
| open(large) / settledMs | 315.9 [320.7] | 308.1 [311.4] | 0.975 [0.927, 0.997] | no detectable difference |
| open(large) / mainTaskMs | 519.0 [524.5] | 503.9 [507.6] | 0.971 [0.932, 0.996] | no detectable difference |
| open(large) / mainScriptMs | 338.6 [343.3] | 352.1 [356.5] | 1.040 [0.994, 1.075] | unreliable (A/A failed) |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2752.6 [2755.8] | 2574.7 [2631.4] | 0.935 [0.930, 0.972] | faster |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 16.6 [16.7] | 9.3 [9.4] | 0.559 [0.517, 0.572] | faster |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 1732.2 [1752.5] | 1270.0 [1392.8] | 0.733 [0.717, 0.818] | faster |
| scroll / mainScriptMs | 1128.1 [1151.2] | 836.1 [943.7] | 0.741 [0.719, 0.851] | faster |
| jump / settleMs | 118.7 [121.9] | 108.0 [108.9] | -10.7 [-15.7, -7.8] | faster |
| jump / mainTaskMs | 377.2 [390.8] | 374.4 [376.9] | 0.993 [0.919, 1.006] | no detectable difference |
| jump / mainScriptMs | 301.5 [316.7] | 323.9 [326.0] | 1.074 [0.973, 1.088] | no detectable difference |
| zoom / settleMs | 409.3 [427.5] | 339.9 [340.4] | -69.4 [-98.9, -67.1] | faster |
| zoom / zoom2SettleMs | 148.6 [152.3] | 139.4 [139.9] | -9.2 [-13.5, -8.0] | no detectable difference |
| zoom / zoom4SettleMs | 260.7 [275.2] | 200.0 [200.9] | -60.7 [-85.3, -58.7] | faster |
| zoom / backTo1SettleMs | 84.8 [86.2] | 72.4 [74.4] | -12.4 [-13.9, +0.7] | no detectable difference |
| zoom / mainTaskMs | 351.7 [356.0] | 286.0 [288.7] | 0.813 [0.790, 0.856] | faster |
| zoom / mainScriptMs | 154.5 [154.8] | 132.6 [134.9] | 0.858 [0.829, 0.907] | no detectable difference |
| select / settleMs | 153.1 [160.0] | 144.6 [144.8] | -8.5 [-22.0, +5.4] | no detectable difference |
| select / mainTaskMs | 89.7 [91.8] | 76.3 [78.7] | 0.851 [0.813, 0.887] | no detectable difference |
| select / mainScriptMs | 43.9 [45.6] | 41.9 [43.7] | 0.954 [0.904, 1.014] | no detectable difference |
| search / searchMs | 55.7 [55.7] | 62.0 [62.0] | 1.113 [1.108, 1.145] | no detectable difference |
| search / mainTaskMs | 14.4 [14.6] | 20.7 [21.6] | 1.436 [1.319, 1.521] | slower |
| search / mainScriptMs | 7.2 [7.3] | 8.7 [9.0] | 1.205 [1.055, 1.269] | no detectable difference |
| annotate / settleMs | 168.2 [169.7] | 166.0 [166.9] | -2.2 [-6.3, +3.1] | no detectable difference |
| annotate / mainTaskMs | 107.2 [107.3] | 92.0 [93.3] | 0.858 [0.848, 0.876] | faster |
| annotate / mainScriptMs | 50.2 [50.3] | 47.5 [48.0] | 0.946 [0.931, 0.968] | no detectable difference |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-fix-leak median [p95] | plugins-fix-zoom median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.33 MiB [34.34 MiB] | 34.33 MiB [34.34 MiB] | 41.55 MiB [41.56 MiB] | 41.55 MiB [41.56 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.16 MiB [48.16 MiB] | 48.16 MiB [48.16 MiB] | 57.58 MiB [57.59 MiB] | 57.27 MiB [57.28 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.94 MiB [22.94 MiB] | 22.95 MiB [22.95 MiB] |
| soak / heapCycle5Bytes | 37.42 MiB [37.43 MiB] | 37.41 MiB [37.43 MiB] | 44.37 MiB [44.37 MiB] | 44.36 MiB [44.37 MiB] |
| soak / heapCycle30Bytes | 38.28 MiB [38.28 MiB] | 38.27 MiB [38.27 MiB] | 45.05 MiB [45.05 MiB] | 45.04 MiB [45.05 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-fix-leak`:

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

`plugins-fix-zoom`:

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

