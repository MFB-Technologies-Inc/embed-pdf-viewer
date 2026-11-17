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

Neither is fixed or in the ledger; both wait on a decision. The printed cases reproduce them, for
example
`{ name: 'diff-normalizePdfQuad-1', fn: 'normalizePdfQuad', args: [[{ x: 0, y: -1 }, { x: -1.4456042752816933e-19, y: 0 }, { x: 0, y: 0 }, { x: 0, y: 5e-324 }]] }`.

## Full suite, 2026-11-16

At `d37f70fe0`, from `tooling/plugin-conformance`, in this order:

| command | exit | printed |
| --- | --- | --- |
| `pnpm run typecheck` | 0 | |
| `pnpm run test:harness` | 0 | tests 78, pass 78, fail 0 |
| `pnpm run vectors:check` | 0 | `119 vector files are current, and each is replayed` |
| `pnpm run coverage:check` | 0 | `coverage is current: statements 72853/77093, branches 14365/15719, 935 lines reached only by snapshot-none cases, 36 ported files no generator loads` |
| `pnpm run replay:check` | 0 | `cargo: passed`, `receipts: 119 read`, `119 vector files, 15916 cases, all replayed` |
| `pnpm run rust-coverage:check` | 0 | `rust coverage is current: replay reaches 64233/112503 lines; 36975 lines only other tests reach, in 673 files (74 excluded)` |

`replay:check` ran with this wave's receipts: every receipt carried the run's id, commit and vector
hash, none was written with failures, member order off or the allowlist replaced, and every one of
the 19 allowlist paths was used.
