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
pin it. The `atan2` difference still waits on a decision. The printed cases reproduce it, for example
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
`normalizePdfQuad` (1095, 545 and 1651), and each still exits 1.

## Full suite, 2026-11-17

At `f02f7419a`, on a clean tree, in container `embed-pdf-conformance` with Node v24.20.0, pnpm
10.34.0, rustc 1.98.0 (88d9e12ae 2026-08-18), cargo 1.98.0 (797e8a9bc 2026-08-05) and
cargo-llvm-cov 0.9.1. The `pnpm run` commands ran from `tooling/plugin-conformance`, the cargo ones
from the repository root. `<crates>` is every crate with a file this branch changed under `crates/`
(`git diff --name-only 2aaa8f841..HEAD -- crates`): `-p conformance-function-server -p epdf-capi
-p epdf-engine -p plugin-conformance -p epdf-acrojs -p epdf-plugin-actions -p epdf-plugin-annotation
-p epdf-annotation-core -p epdf-plugin-selection -p epdf-plugin-stage -p epdf-ui-core`.

| command | exit | printed |
| --- | --- | --- |
| `cargo fmt <crates> --check` | 0 | nothing |
| `cargo clippy <crates> --all-targets -- -D warnings` | 0 | no warnings or errors |
| `pnpm run typecheck` | 0 | |
| `pnpm run test:harness` | 0 | tests 87, pass 87, fail 0 |
| `pnpm run vectors:check` | 0 | `119 vector files are current, and each is replayed` |
| `pnpm run coverage:check` | 0 | `coverage is current: statements 72853/77093, branches 14365/15719, 935 lines reached only by snapshot-none cases, 36 ported files no generator loads` |
| `pnpm run replay:check` | 0 | `cargo: passed`, `checkout: f02f7419afe1734b68d1f2923324a914d5dee144, clean`, `receipts: 119 read`, `119 vector files, 15916 cases, all replayed` |
| `pnpm run rust-coverage:check` | 0 | `rust coverage is current: replay reaches 64233/112503 lines; 36975 lines only other tests reach, in 673 files (74 excluded)` |

The cargo test run inside `replay:check` printed 161 `test result:` lines, which sum to 863 passed,
0 failed and 1 ignored. Every receipt carried the run's id, the commit, a clean-tree flag and the
vector hash, none was written with failures, member order off or the allowlist replaced, and every
one of the 19 allowlist paths was used.
