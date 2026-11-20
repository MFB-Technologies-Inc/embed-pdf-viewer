# Viewer perf comparison: 2026-09-30T16-55-46-584Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x), **quick, not for publication**.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 1c8004c56e0faad4ddf21247ea5cd5443215d18d |
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
| plugins-wire | research/rust-kernel-wire | da947a564 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | dcdef9772bf9 |
| plugins-getstate | research/rust-kernel-wire | 776f0c265 | built (fork d045f389d, wasm32-eh) | 0f7407599d93 | 764d1ef49e62 |

## Gate

- `main`: pass
- `main-aa`: pass
- `plugins-wire`: pass
- `plugins-getstate`: pass

## Sizes

| Metric | main | main-aa | plugins-wire | plugins-getstate |
| --- | --- | --- | --- | --- |
| engineWasmBytes | 7.19 MiB | 7.19 MiB | 10.57 MiB | 10.57 MiB |
| engineWasmBrotliBytes | 2.15 MiB | 2.15 MiB | 2.83 MiB | 2.83 MiB |
| kernelWasmBytes | 0.00 MiB | 0.00 MiB | 11.68 MiB | 11.69 MiB |
| kernelWasmBrotliBytes | 0.00 MiB | 0.00 MiB | 1.88 MiB | 1.88 MiB |
| jsBytes | 3.37 MiB | 3.37 MiB | 2.62 MiB | 2.62 MiB |
| jsBrotliBytes | 0.96 MiB | 0.96 MiB | 0.80 MiB | 0.80 MiB |

## A/A: `main` vs `main-aa`

A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.

39 of 39 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 51.8 [52.5] | 51.7 [51.8] | 0.997 [0.975, 1.003] | pass |
| open(ebook) / openedMs | 115.2 [116.7] | 114.1 [115.1] | 0.990 [0.964, 1.020] | pass |
| open(ebook) / firstPaintMs | 211.9 [211.9] | 209.4 [213.7] | 0.989 [0.969, 1.047] | pass |
| open(ebook) / settledMs | 331.3 [334.8] | 332.4 [333.3] | 1.003 [0.981, 1.013] | pass |
| open(ebook) / mainTaskMs | 114.4 [118.1] | 113.9 [115.5] | 0.996 [0.957, 1.017] | pass |
| open(ebook) / mainScriptMs | 19.6 [21.0] | 19.5 [19.8] | 0.994 [0.921, 1.021] | pass |
| open(large) / readyMs | 52.9 [54.8] | 53.1 [53.9] | 1.005 [0.930, 1.045] | pass |
| open(large) / openedMs | 133.1 [135.2] | 132.7 [134.6] | 0.998 [0.969, 1.020] | pass |
| open(large) / firstPaintMs | 222.5 [224.9] | 221.4 [224.8] | 0.995 [0.964, 1.040] | pass |
| open(large) / settledMs | 222.0 [222.1] | 217.9 [223.0] | 0.981 [0.962, 1.018] | pass |
| open(large) / mainTaskMs | 259.1 [261.2] | 254.0 [257.9] | 0.980 [0.971, 1.003] | pass |
| open(large) / mainScriptMs | 23.9 [24.3] | 22.6 [25.3] | 0.942 [0.922, 1.107] | pass |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / wallMs | 2002.0 [2013.6] | 2001.4 [2013.9] | 1.000 [0.993, 1.008] | pass |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | pass |
| scroll / frameP95Ms | 8.4 [8.4] | 8.4 [8.4] | 1.000 [0.999, 1.010] | pass |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | pass |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | pass |
| scroll / mainTaskMs | 391.5 [397.4] | 398.9 [402.2] | 1.019 [0.969, 1.037] | pass |
| scroll / mainScriptMs | 52.2 [54.2] | 55.3 [55.3] | 1.058 [0.966, 1.065] | pass |
| jump / settleMs | 97.2 [100.7] | 99.1 [99.2] | +1.9 [-4.3, +4.0] | pass |
| jump / mainTaskMs | 56.9 [58.5] | 56.5 [58.1] | 0.992 [0.960, 1.102] | pass |
| jump / mainScriptMs | 17.1 [18.2] | 14.7 [15.3] | 0.864 [0.799, 1.033] | pass |
| zoom / settleMs | 364.0 [371.3] | 357.5 [363.2] | -6.5 [-35.8, +20.6] | pass |
| zoom / zoom2SettleMs | 134.5 [135.3] | 130.9 [132.8] | -3.6 [-6.2, +2.5] | pass |
| zoom / zoom4SettleMs | 228.6 [240.3] | 224.5 [232.1] | -4.1 [-34.5, +24.2] | pass |
| zoom / backTo1SettleMs | 74.7 [81.7] | 67.9 [69.3] | -6.8 [-16.9, +5.4] | pass |
| zoom / mainTaskMs | 172.2 [175.7] | 163.0 [163.7] | 0.946 [0.919, 1.036] | pass |
| zoom / mainScriptMs | 27.1 [27.8] | 22.7 [22.8] | 0.839 [0.812, 1.015] | pass |
| select / settleMs | 151.0 [158.4] | 147.2 [149.2] | -3.8 [-15.5, +4.9] | pass |
| select / mainTaskMs | 45.2 [47.4] | 44.2 [44.6] | 0.978 [0.875, 1.071] | pass |
| select / mainScriptMs | 10.4 [11.0] | 9.8 [10.3] | 0.943 [0.824, 1.165] | pass |
| search / searchMs | 56.8 [60.1] | 58.7 [61.6] | 1.033 [0.893, 1.102] | pass |
| search / mainTaskMs | 4.5 [5.2] | 4.5 [5.1] | 0.985 [0.799, 1.138] | pass |
| search / mainScriptMs | 0.2 [0.2] | 0.2 [0.2] | 0.844 [0.650, 1.398] | pass |
| annotate / settleMs | 155.3 [156.0] | 153.2 [164.5] | -2.2 [-3.1, +14.8] | pass |
| annotate / mainTaskMs | 57.0 [57.1] | 57.7 [61.9] | 1.014 [0.992, 1.117] | pass |
| annotate / mainScriptMs | 11.5 [11.8] | 11.9 [12.0] | 1.037 [0.934, 1.051] | pass |

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

Not run: `plugins-fix-zoom`.

## `plugins-wire` vs `plugins-getstate`

| Row | plugins-wire median [p95] | plugins-getstate median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 83.4 [83.8] | 82.8 [85.1] | 0.993 [0.986, 1.023] | no detectable difference |
| open(ebook) / openedMs | 184.9 [185.7] | 182.2 [185.0] | 0.985 [0.980, 1.016] | no detectable difference |
| open(ebook) / firstPaintMs | 317.7 [322.4] | 313.1 [314.0] | 0.985 [0.960, 1.010] | no detectable difference |
| open(ebook) / settledMs | 373.6 [375.5] | 359.0 [363.8] | 0.961 [0.954, 1.000] | no detectable difference |
| open(ebook) / mainTaskMs | 258.5 [258.7] | 252.1 [254.3] | 0.975 [0.963, 0.992] | no detectable difference |
| open(ebook) / mainScriptMs | 112.6 [112.7] | 107.1 [108.5] | 0.951 [0.940, 0.972] | no detectable difference |
| open(large) / readyMs | 83.7 [84.9] | 85.1 [85.5] | 1.016 [0.979, 1.026] | no detectable difference |
| open(large) / openedMs | 210.6 [210.6] | 211.3 [211.5] | 1.003 [1.001, 1.011] | no detectable difference |
| open(large) / firstPaintMs | 346.6 [350.0] | 341.1 [343.8] | 0.984 [0.963, 0.994] | no detectable difference |
| open(large) / settledMs | 360.3 [360.7] | 346.8 [348.6] | 0.962 [0.957, 0.971] | no detectable difference |
| open(large) / mainTaskMs | 541.5 [542.1] | 520.8 [522.5] | 0.962 [0.960, 0.973] | faster |
| open(large) / mainScriptMs | 352.1 [354.9] | 321.5 [324.1] | 0.913 [0.896, 0.927] | no detectable difference |
| scroll / settleMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / wallMs | 2526.9 [2542.8] | 2345.8 [2402.3] | 0.928 [0.903, 0.956] | faster |
| scroll / frameP50Ms | 8.3 [8.3] | 8.3 [8.3] | 1.000 [1.000, 1.000] | no detectable difference |
| scroll / frameP95Ms | 8.4 [8.4] | 8.4 [8.9] | 1.011 [1.000, 1.079] | no detectable difference |
| scroll / jankFrames | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTasks | 0 [0] | 0 [0] | +0 [+0, +0] | no detectable difference |
| scroll / longTaskMs | 0.0 [0.0] | 0.0 [0.0] | +0.0 [+0.0, +0.0] | no detectable difference |
| scroll / mainTaskMs | 1101.5 [1116.1] | 864.8 [884.0] | 0.785 [0.773, 0.822] | faster |
| scroll / mainScriptMs | 608.7 [616.5] | 370.5 [377.4] | 0.609 [0.597, 0.639] | faster |
| jump / settleMs | 117.5 [118.4] | 143.7 [157.1] | +26.2 [-1.2, +48.0] | no detectable difference |
| jump / mainTaskMs | 374.0 [377.7] | 149.2 [150.0] | 0.399 [0.393, 0.416] | faster |
| jump / mainScriptMs | 301.0 [306.4] | 96.1 [96.1] | 0.319 [0.311, 0.330] | faster |
| zoom / settleMs | 417.4 [442.2] | 408.8 [411.0] | -8.6 [-50.5, +3.6] | no detectable difference |
| zoom / zoom2SettleMs | 155.3 [169.9] | 154.7 [155.8] | -0.7 [-17.5, +6.6] | no detectable difference |
| zoom / zoom4SettleMs | 268.0 [273.0] | 252.8 [256.9] | -15.2 [-33.7, +5.0] | no detectable difference |
| zoom / backTo1SettleMs | 92.0 [102.1] | 84.8 [86.4] | -7.3 [-21.9, +1.3] | no detectable difference |
| zoom / mainTaskMs | 325.2 [325.5] | 309.9 [314.9] | 0.953 [0.931, 0.981] | no detectable difference |
| zoom / mainScriptMs | 142.4 [143.5] | 123.4 [124.1] | 0.867 [0.848, 0.896] | no detectable difference |
| select / settleMs | 142.9 [155.9] | 143.0 [148.2] | +0.1 [-15.6, +7.8] | no detectable difference |
| select / mainTaskMs | 79.0 [80.7] | 68.8 [73.7] | 0.871 [0.833, 0.983] | faster |
| select / mainScriptMs | 41.1 [42.9] | 33.6 [35.4] | 0.818 [0.764, 0.895] | faster |
| search / searchMs | 72.4 [72.6] | 73.6 [74.3] | 1.017 [0.821, 1.139] | no detectable difference |
| search / mainTaskMs | 23.2 [24.5] | 23.7 [24.1] | 1.022 [0.725, 1.541] | no detectable difference |
| search / mainScriptMs | 9.1 [9.4] | 7.9 [8.2] | 0.866 [0.701, 1.193] | no detectable difference |
| annotate / settleMs | 167.0 [167.2] | 167.4 [167.8] | +0.4 [-0.9, +1.0] | no detectable difference |
| annotate / mainTaskMs | 98.6 [100.2] | 95.9 [95.9] | 0.973 [0.934, 0.990] | no detectable difference |
| annotate / mainScriptMs | 49.1 [50.2] | 44.7 [44.7] | 0.910 [0.874, 0.916] | faster |

## Memory

| Row | main median [p95] | main-aa median [p95] | plugins-wire median [p95] | plugins-getstate median [p95] |
| --- | --- | --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.35 MiB [34.35 MiB] | 41.54 MiB [41.54 MiB] | 41.50 MiB [41.50 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] | 23.02 MiB [23.02 MiB] | 23.02 MiB [23.02 MiB] |
| open(large) / heapAfterOpenBytes | 48.15 MiB [48.15 MiB] | 48.14 MiB [48.16 MiB] | 57.07 MiB [57.08 MiB] | 56.53 MiB [56.53 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] | 22.95 MiB [22.95 MiB] | 22.95 MiB [22.95 MiB] |
| soak / heapCycle5Bytes | 37.41 MiB [37.43 MiB] | 37.42 MiB [37.42 MiB] | 44.34 MiB [44.35 MiB] | 44.30 MiB [44.30 MiB] |
| soak / heapCycle30Bytes | 38.27 MiB [38.27 MiB] | 38.26 MiB [38.27 MiB] | 45.01 MiB [45.01 MiB] | 44.96 MiB [44.96 MiB] |
| soak / heapSoakSlopeBytes | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] | 0.03 MiB [0.03 MiB] |

## Diagnostics

Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.

`plugins-wire`:

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

