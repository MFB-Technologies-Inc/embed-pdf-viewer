# Viewer perf comparison: 2026-10-01T05-53-43-271Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x).

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | a575675b462b0084337b17e244c8183ab2cb252e |
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
- `main-compat`: excluded: the page reports {"id":"main-compat","kernel":"typescript","rustReads":false,"dedupeCompat":true}, arms.json says {"id":"main-compat","kernel":"typescript","rustReads":false}
- `impl-compat`: excluded: the page reports {"id":"impl-compat","kernel":"typescript","rustReads":false,"dedupeCompat":true}, arms.json says {"id":"impl-compat","kernel":"typescript","rustReads":false}
- `plugins-compat`: excluded: the page reports {"id":"plugins-compat","kernel":"rust","rustReads":false,"dedupeCompat":true}, arms.json says {"id":"plugins-compat","kernel":"rust","rustReads":false}

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

36 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 52.8 [53.9] | 54.8 [54.8] | 1.038 [0.975, 1.040] | pass |
| open(ebook) / openedMs | 118.1 [120.1] | 118.0 [118.3] | 1.000 [0.953, 1.020] | pass |
| open(ebook) / firstPaintMs | 216.1 [221.2] | 218.2 [218.6] | 1.010 [0.977, 1.016] | pass |
| open(ebook) / settledMs | 337.1 [343.5] | 339.4 [339.5] | 1.007 [0.980, 1.012] | pass |
| open(ebook) / mainTaskMs | 122.0 [122.1] | 120.3 [120.4] | 0.985 [0.964, 0.993] | pass |
| open(ebook) / mainJsMs | 94.2 [95.2] | 93.5 [93.9] | 0.992 [0.964, 0.998] | pass |
| open(large) / readyMs | 52.8 [53.9] | 52.8 [54.4] | 1.000 [0.967, 1.051] | pass |
| open(large) / openedMs | 135.0 [135.4] | 134.2 [136.4] | 0.994 [0.984, 1.019] | pass |
| open(large) / firstPaintMs | 225.4 [226.3] | 223.1 [226.2] | 0.990 [0.977, 1.023] | pass |
| open(large) / settledMs | 223.1 [224.7] | 220.5 [222.6] | 0.988 [0.971, 1.025] | pass |
| open(large) / mainTaskMs | 263.0 [263.9] | 261.1 [262.8] | 0.993 [0.984, 1.013] | pass |
| open(large) / mainJsMs | 239.1 [240.0] | 238.1 [239.5] | 0.996 [0.987, 1.015] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2011.6 [2041.1] | 2035.9 [2044.3] | 1.012 [0.986, 1.018] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.7 [9.0] | 8.9 [9.0] | 1.017 [0.981, 1.079] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 408.9 [412.3] | 405.5 [408.7] | 0.992 [0.942, 1.043] | pass |
| scroll / mainJsMs | 315.9 [318.7] | 313.9 [317.1] | 0.994 [0.938, 1.050] | pass |
| jump / settleMs | 95.7 [98.5] | 99.1 [100.0] | +3.4 [-11.3, +7.3] | pass |
| jump / mainTaskMs | 53.6 [55.3] | 56.9 [58.2] | 1.060 [1.020, 1.092] | A/A failed |
| jump / mainJsMs | 42.2 [43.8] | 42.7 [45.0] | 1.011 [0.937, 1.133] | pass |
| zoom / settleMs | 395.1 [401.8] | 399.0 [404.7] | +3.9 [-38.2, +26.9] | pass |
| zoom / zoom2SettleMs | 132.4 [133.4] | 133.1 [133.4] | +0.7 [-1.1, +4.1] | pass |
| zoom / zoom4SettleMs | 261.5 [269.3] | 266.6 [271.3] | +5.1 [-38.9, +22.8] | pass |
| zoom / backTo1SettleMs | 73.5 [75.2] | 76.2 [87.4] | +2.7 [-10.5, +23.7] | pass |
| zoom / mainTaskMs | 175.3 [179.2] | 173.2 [186.0] | 0.988 [0.960, 1.104] | pass |
| zoom / mainJsMs | 122.5 [129.9] | 126.1 [136.6] | 1.029 [0.958, 1.125] | pass |
| select / settleMs | 141.5 [146.6] | 147.5 [147.9] | +6.0 [-3.1, +7.1] | pass |
| select / mainTaskMs | 38.0 [38.6] | 42.8 [46.1] | 1.128 [1.001, 1.418] | A/A failed |
| select / mainJsMs | 23.4 [23.6] | 26.5 [29.2] | 1.132 [1.006, 1.563] | A/A failed |
| search / searchMs | 57.2 [60.3] | 50.0 [56.8] | 0.875 [0.793, 1.053] | pass |
| search / mainTaskMs | 4.7 [5.2] | 4.4 [4.5] | 0.937 [0.807, 1.062] | pass |
| search / mainJsMs | 3.2 [3.2] | 3.1 [3.1] | 0.947 [0.868, 1.023] | pass |
| annotate / settleMs | 161.0 [161.8] | 161.1 [161.3] | +0.0 [-1.1, +1.3] | pass |
| annotate / mainTaskMs | 55.9 [57.9] | 57.7 [57.8] | 1.032 [0.891, 1.039] | pass |
| annotate / mainJsMs | 33.3 [34.4] | 34.5 [34.7] | 1.037 [0.897, 1.061] | pass |

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

`main-compat` excluded: the page reports {"id":"main-compat","kernel":"typescript","rustReads":false,"dedupeCompat":true}, arms.json says {"id":"main-compat","kernel":"typescript","rustReads":false}

## `impl-fixed` vs `impl-compat`

`impl-compat` excluded: the page reports {"id":"impl-compat","kernel":"typescript","rustReads":false,"dedupeCompat":true}, arms.json says {"id":"impl-compat","kernel":"typescript","rustReads":false}

## `plugins-fixed` vs `plugins-compat`

`plugins-compat` excluded: the page reports {"id":"plugins-compat","kernel":"rust","rustReads":false,"dedupeCompat":true}, arms.json says {"id":"plugins-compat","kernel":"rust","rustReads":false}

## Memory

| Row | main median [p95] | main-aa median [p95] | impl-fixed median [p95] | plugins-fixed median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.34 MiB] | 34.35 MiB [34.35 MiB] | 34.95 MiB [34.96 MiB] | 41.44 MiB [41.44 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.06 MiB [23.06 MiB] | 23.06 MiB [23.06 MiB] |
| open(large) / heapAfterOpenBytes | 48.16 MiB [48.16 MiB] | 48.15 MiB [48.15 MiB] | 48.63 MiB [48.64 MiB] | 56.26 MiB [56.27 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 23.03 MiB [23.03 MiB] | 22.99 MiB [22.99 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.43 MiB] | 37.41 MiB [37.43 MiB] | 38.10 MiB [38.10 MiB] | 45.27 MiB [45.27 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.28 MiB] | 38.26 MiB [38.27 MiB] | 38.82 MiB [38.83 MiB] | 45.80 MiB [45.80 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.02 MiB [0.02 MiB] |

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

