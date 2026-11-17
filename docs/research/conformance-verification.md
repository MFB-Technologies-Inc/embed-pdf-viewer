# Conformance verification record

What the conformance checks printed when they were last run in full, so a reader can tell what was
verified and when without rerunning them. Every run below is in the dev container.

## Geometry differential, 2026-11-16

`pnpm --filter @embedpdf/tooling-plugin-conformance run differential -- --crate geometry --runs 10000 --seed <s>`,
seeds 1, 2 and 3, at `82c1f37d7`:

| seed | inputs, 37 functions | mismatched (failing input) | exit |
| --- | --- | --- | --- |
| 1 | 349676 | `matrixToCss` (8319), `pageTransform` (262), `normalizePdfQuad` (1095) | 1 |
| 2 | 350207 | `matrixToCss` (7437), `pageTransform` (2225), `normalizePdfQuad` (545) | 1 |
| 3 | 346069 | `matrixToCss` (4322), `pageTransform` (96), `normalizePdfQuad` (1651) | 1 |

The other 34 functions ran clean on every seed. Two causes account for all nine mismatches.

`matrixToCss` and the `cssMatrix` of `pageTransform` differ when an `f64` sits exactly halfway
between two shortest digit strings. `(-1655218367829957.2).toPrecision(21)` is
`-1655218367829957.25000`, and `-1655218367829957.2 === -1655218367829957.3`. The TypeScript prints
`...957.2`, the even digit; `epdf_kernel::js::number_to_string` (`crates/epdf-kernel/src/js.rs`)
rounds up to `...957.3`.

`normalizePdfQuad` labels the corners of a degenerate quad differently. Its ring sort compares
`atan2` angles, and for the seed 1 case V8's `Math.atan2` gives `1.5707963267948968` for one corner
where glibc, which Rust's `f64::atan2` calls, gives `1.5707963267948966`. One ulp flips the
comparator between near-tied corners.

Neither was fixed or in the ledger at `82c1f37d7`. `971ed565e` fixes the halfway rounding: every
port of `Number::toString` now picks the even digit, and the cases `matrixToCss/digit-tie-to-even*`
and `pageTransform/css-digit-tie-to-even*` in `tooling/plugin-conformance/src/suites/geometry.ts`
pin it. What still waits on the owner's decision is the last-bit difference between the platform
math libraries: `atan2` here, and `cos` in the seed 31337 run below, where the sines agree. The printed cases
reproduce the `atan2` one, for example
`{ name: 'diff-normalizePdfQuad-1', fn: 'normalizePdfQuad', args: [[{ x: 0, y: -1 }, { x: -1.4456042752816933e-19, y: 0 }, { x: 0, y: 0 }, { x: 0, y: 5e-324 }]] }`.

## Geometry differential, 2026-11-17

The same command and seeds at `5ff124ed9` printed the same inputs and mismatches as the run above,
and each seed exited 1:

| seed | inputs, 37 functions | mismatched (failing input) | exit |
| --- | --- | --- | --- |
| 1 | 349676 | `matrixToCss` (8319), `pageTransform` (262), `normalizePdfQuad` (1095) | 1 |
| 2 | 350207 | `matrixToCss` (7437), `pageTransform` (2225), `normalizePdfQuad` (545) | 1 |
| 3 | 346069 | `matrixToCss` (4322), `pageTransform` (96), `normalizePdfQuad` (1651) | 1 |

The two causes were unchanged: the halfway digit rounding of `number_to_string` and the one-ulp
`atan2` difference. At `86c22c127`, after the rounding fix, the same seeds mismatch only in
`normalizePdfQuad` (1095, 545 and 1651), and each still exits 1. At `ef439fbba` they print the same:

| seed | inputs, 37 functions | mismatched (failing input) | exit |
| --- | --- | --- | --- |
| 1 | 361095 | `normalizePdfQuad` (1095) | 1 |
| 2 | 360545 | `normalizePdfQuad` (545) | 1 |
| 3 | 361651 | `normalizePdfQuad` (1651) | 1 |

These 10000 runs never reach the `cos` difference below, so they show only one of the two.

## Geometry differential, seed 31337, 2026-11-17

`pnpm run differential -- --crate geometry --runs 50000 --seed 31337` printed, at `da89a8ead` and
again at `ef439fbba`, 1791277 inputs over 37 functions with two mismatched, `rotateAbout` (39703)
and `normalizePdfQuad` (1574), and exited 1. The `normalizePdfQuad` case is the `atan2` difference
again. The `rotateAbout` one is new:

```
{ name: 'diff-rotateAbout-31337', fn: 'rotateAbout', args: [{ x: 0, y: -4915517297019373 }, -6] },
```

TypeScript returns `[0.9601702866503661, 0.27941549819892586, -0.27941549819892586,
0.9601702866503661, -1373471714452105.5, -195783644905449]`, Rust the same with `0.960170286650366`
for both cosines and `-195783644905450` last. `Math.cos(-6)` in Node v24.20.0 is
`0.9601702866503661`; `(-6f64).cos()` in the container, which calls glibc 2.41, is
`0.960170286650366`, one ulp lower. The sines agree at `0.27941549819892586`. The translation
multiplies that ulp by `4915517297019373`, so it reaches the last digit of an integer. This is the
`atan2` family, a last-bit difference in the platform math library, and it waits on the same
decision. Neither is fixed or in `divergences.json`, so the differential exits 1 by design until
that decision is made.

## Geometry differential with the platform math entries, 2026-11-17

The owner decided to keep the last-bit difference between the platform math libraries as a
platform-dependent divergence, so it is now in `divergences.json` as `PLATFORM-MATH-COS-SIN` and
`PLATFORM-MATH-ATAN2`, each pinned by a test in `crates/plugins/geometry/tests/platform_math.rs`.
The differential accepts a difference in a function those entries name only when Rust's outcome is
the TypeScript's with each `Math.cos`, `Math.sin` or `Math.atan2` call answering one ulp off
(`tooling/plugin-conformance/README.md`, "The divergence ledger"). The same command at `fd1081000`,
with the seed 31337 run added:

| seed | runs | inputs, 37 functions | mismatched | allowed by the ledger | exit |
| --- | --- | --- | --- | --- | --- |
| 1 | 10000 | 370000 | 0 | 5, all `normalizePdfQuad` (`PLATFORM-MATH-ATAN2`) | 0 |
| 2 | 10000 | 370000 | 0 | 3, all `normalizePdfQuad` (`PLATFORM-MATH-ATAN2`) | 0 |
| 3 | 10000 | 370000 | 0 | 3, all `normalizePdfQuad` (`PLATFORM-MATH-ATAN2`) | 0 |
| 31337 | 50000 | 1850000 | 0 | 24: `rotateAbout` 1 (`PLATFORM-MATH-COS-SIN`), `normalizePdfQuad` 23 (`PLATFORM-MATH-ATAN2`) | 0 |

Seed 31337 printed `37 functions, 1850000 inputs, 0 mismatched, 24 allowed by the ledger
(PLATFORM-MATH-COS-SIN 1, PLATFORM-MATH-ATAN2 23)`. Each 10000 run now covers every input of every
function; before, fast-check stopped `normalizePdfQuad` at its first mismatch.

The rule still catches a real bug in those functions. In a copy of the Rust sources under `/tmp` in
the container, never committed, a `function-server` built with `rotate_about` turning by `-rad`
made `--fn rotateAbout,normalizePdfQuad --runs 10000 --seed 1` exit 1 on its first input, shrunk
to `rotateAbout({ x: 0, y: 0 }, -1)`. One built with `normalize_pdf_quad` swapping the lower
corners of a quad named from its shape exited 1 on the third input. The unchanged copy exited 0
with the same 5 inputs allowed.

## Full suite, 2026-11-17

At `fd1081000`, on a clean tree, in container `embed-pdf` with Node v24.20.0, pnpm
10.34.0, rustc 1.98.0 (88d9e12ae 2026-08-18), cargo 1.98.0 (797e8a9bc 2026-08-05) and
cargo-llvm-cov 0.9.1. The `pnpm run` commands ran from `tooling/plugin-conformance`, the cargo ones
from the repository root. `<crates>` is every crate with a file this branch changed under `crates/`
(`git diff --name-only 2aaa8f841..HEAD -- crates`): `-p conformance-function-server -p epdf-capi
-p epdf-engine -p epdf-kernel -p plugin-conformance -p epdf-acrojs -p epdf-plugin-actions
-p epdf-plugin-annotation -p epdf-annotation-core -p epdf-geometry -p epdf-plugin-i18n
-p epdf-plugin-selection -p epdf-plugin-stage -p epdf-ui-core`.

| command | exit | printed |
| --- | --- | --- |
| `cargo fmt <crates> --check` | 0 | nothing |
| `cargo clippy <crates> --all-targets -- -D warnings` | 0 | no warnings or errors |
| `pnpm run typecheck` | 0 | |
| `pnpm run test:harness` | 0 | tests 87, pass 87, fail 0 |
| `pnpm run vectors:check` | 0 | `119 vector files are current, and each is replayed` |
| `pnpm run coverage:check` | 0 | `coverage is current: statements 72853/77093, branches 14365/15719, 935 lines reached only by snapshot-none cases, 36 ported files no generator loads` |
| `pnpm run replay:check` | 0 | `cargo: passed`, `checkout: fd10810007008a896584471f6d6d92bd4113fb7f, clean`, `receipts: 119 read`, `119 vector files, 15922 cases, all replayed` |
| `pnpm run rust-coverage:check` | 0 | `rust coverage is current: replay reaches 64253/112591 lines; 37035 lines only other tests reach, in 673 files (74 excluded)` |
| `pnpm run differential -- --crate geometry` | 0 | the four runs in the section above |

The cargo test run inside `replay:check` printed 162 `test result:` lines, which sum to 869 passed,
0 failed and 1 ignored. Every receipt carried the run's id, the commit, a clean-tree flag and the
vector hash, none was written with failures, member order off or the allowlist replaced, and every
one of the 19 allowlist paths was used.
