# Viewer perf comparison: 2026-09-30T16-38-39-372Z-2023MBP16Grey-desktop

3 rounds, profile `desktop` (CPU throttle 1x), **quick, not for publication**.

`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.

## Environment

| Field | Value |
| --- | --- |
| Harness commit | 04ff546ab3ebe2894bf8ff55afe8933d5870867a (uncommitted changes) |
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

14 of 15 rows pass.

| Row | main median [p95] | main-aa median [p95] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
| open(ebook) / readyMs | 51.4 [51.6] | 51.4 [53.3] | 0.999 [0.988, 1.048] | pass |
| open(ebook) / openedMs | 112.8 [114.5] | 113.3 [115.4] | 1.004 [0.977, 1.025] | pass |
| open(ebook) / firstPaintMs | 203.3 [212.1] | 214.5 [217.0] | 1.055 [0.968, 1.069] | pass |
| open(ebook) / settledMs | 323.9 [331.7] | 332.8 [334.3] | 1.027 [0.988, 1.045] | pass |
| open(ebook) / mainTaskMs | 113.1 [116.6] | 114.0 [116.2] | 1.008 [0.954, 1.034] | pass |
| open(ebook) / mainScriptMs | 19.3 [20.9] | 20.3 [20.5] | 1.049 [0.897, 1.065] | pass |
| open(large) / readyMs | 51.6 [51.8] | 51.1 [51.5] | 0.990 [0.981, 1.004] | pass |
| open(large) / openedMs | 130.4 [131.5] | 131.0 [131.4] | 1.004 [0.990, 1.012] | pass |
| open(large) / firstPaintMs | 213.4 [220.4] | 220.3 [221.4] | 1.032 [0.963, 1.041] | pass |
| open(large) / settledMs | 217.2 [218.0] | 217.4 [217.9] | 1.001 [0.957, 1.040] | pass |
| open(large) / mainTaskMs | 251.4 [252.7] | 254.9 [255.8] | 1.014 [1.003, 1.022] | pass |
| open(large) / mainScriptMs | 22.8 [23.2] | 22.4 [22.4] | 0.979 [0.958, 1.010] | pass |
| search / searchMs | 58.8 [58.9] | 58.7 [60.7] | 0.999 [0.949, 1.064] | pass |
| search / mainTaskMs | 4.5 [4.5] | 4.4 [4.6] | 0.983 [0.947, 1.060] | pass |
| search / mainScriptMs | 0.1 [0.1] | 0.2 [0.2] | 1.248 [1.071, 1.369] | A/A failed |

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

## Memory

| Row | main median [p95] | main-aa median [p95] |
| --- | --- | --- |
| open(ebook) / heapAfterOpenBytes | 34.34 MiB [34.35 MiB] | 34.34 MiB [34.35 MiB] |
| open(ebook) / workerHeapAfterOpenBytes | 22.27 MiB [22.27 MiB] | 22.27 MiB [22.27 MiB] |
| open(large) / heapAfterOpenBytes | 48.14 MiB [48.15 MiB] | 48.15 MiB [48.16 MiB] |
| open(large) / workerHeapAfterOpenBytes | 22.26 MiB [22.26 MiB] | 22.26 MiB [22.26 MiB] |
