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

## Geometry differential with the platform library's answers, 2026-11-17

The rule above was too loose. It accepted any outcome some combination of one-ulp answers reached,
whether or not the platform library gave those answers. A `function-server` built at `022009aef`
from a copy of the Rust sources under `/tmp` in the container, never committed, with
`normalize_pdf_quad` swapping the first two corners before its sort when
`ring[0].x == ring[1].x && ring[0].y != ring[1].y && ring[2].x == ring[3].x`, passed
`--fn rotate,rotateAbout,angleOf,normalizePdfQuad --runs 10000 --seed 90210 --server <binary>`. It
exited 0 with `4 functions, 40000 inputs, 0 mismatched, 26 allowed by the ledger
(PLATFORM-MATH-ATAN2 26)`, where the unchanged copy allowed 8.

The rule now takes the platform library's actual answers. `function-server` answers a
`{"math": "cos" | "sin" | "atan2"}` request with what `f64::cos`, `f64::sin` or `y.atan2(x)` gives,
as the bits of the double. On a mismatch in a function a platform math entry names,
`src/differential/platform-math.ts` asks that server for each distinct `Math` call the TypeScript
made, refuses unless each answer is the runtime's or a double next to it, and runs the TypeScript
once more with exactly those answers. Rust's outcome must equal that run
(`tooling/plugin-conformance/README.md`, "The divergence ledger").

The same mutation against `cff7e66f4`, its binary's SHA-256 starting `0fefe990afe6356e`, now fails
on every seed tried, each with
`ledger refused: PLATFORM-MATH-ATAN2: Rust's outcome is not the TypeScript's with the platform library's answers`:

| seed | mismatch, shrunk | printed | exit |
| --- | --- | --- | --- |
| 90210 | `normalizePdfQuad` on its 13th input, `[{ x: 0, y: 1.0000000000000002e-12 }, { x: 0, y: 0 }, { x: 0, y: 1.5000000000000007e-12 }, { x: 0, y: 1.5000000000000007e-12 }]` | `4 functions, 30013 inputs, 1 mismatched, 0 allowed by the ledger` | 1 |
| 7 | `normalizePdfQuad` on its 2879th input, `[{ x: 0, y: 1.0000000000000002e-12 }, { x: 0, y: 0 }, { x: 0, y: 1.4989742372373589e-12 }, { x: 0, y: 1.5010257627626423e-12 }]` | `4 functions, 32879 inputs, 1 mismatched, 2 allowed by the ledger (PLATFORM-MATH-ATAN2 2)` | 1 |
| 424242 | `normalizePdfQuad` on its 3rd input, `[{ x: 0, y: 1.0000000000000002e-12 }, { x: 0, y: 0 }, { x: 0, y: -5.000000000000002e-13 }, { x: 0, y: -5.000000000000002e-13 }]` | `4 functions, 30003 inputs, 1 mismatched, 0 allowed by the ledger` | 1 |

The unchanged copy (`87a2d3dc999c5ad9`) exited 0 on the same three seeds, allowing 8, 5 and 4
`normalizePdfQuad` inputs (`PLATFORM-MATH-ATAN2`). Seed 90210 allowed 8 under the old rule too: on
every input the platform's real answers explain, both rules agree. The two earlier mutations still
fail at seeds 1 and 90210. `rotate_about` turning by `-rad` (`de11a14886087d35`) mismatched on the
first `rotateAbout` input, shrunk to `rotateAbout({ x: 0, y: 0 }, -1)` and
`rotateAbout({ x: 0, y: 0 }, -5.000000000000001e-13)`. `rotate` taking `rad.cos() * (1.0 + 4e-16)`
(`2eba6b635b099787`) mismatched on the 9th and 24th, shrunk to `rotateAbout({ x: 0, y: 1e+306 }, 0)`
and `rotateAbout({ x: 0, y: -2560.0000000000005 }, 0)`. All four exited 1. The copies were removed
afterwards.

## Bounded shrinking, 2026-11-17

A near-tie bug could hang the differential. Two `function-server` binaries built from copies of the
Rust sources under `/tmp` in the container, never committed, each changed `normalize_pdf_quad`'s
ring sort: one treats two angles a double apart as tied (SHA-256 starting `50e480923a277b74`), the
other compares the first angle's `next_up()` (`e962a73770e39c62`). At `3a3125e67`,
`--fn rotate,rotateAbout,angleOf,normalizePdfQuad --runs 10000 --seed 90210 --server <binary>`
printed only its first line for both, and `timeout 150` killed each with exit 124. The unchanged
copy (`6d8e5d63246cdb3e`) finishes the same command in 3 seconds.

The cause is how fast-check 4.10.2 shrinks a double: it is an index into the order of doubles, and
`shrinkBigInt` tries the target, then half the distance, a quarter, and so on down to one step, up
to about 60 candidates, each one a server call. A threshold bug fails on one of the first
candidates and halves its way to the boundary. A near-tie keeps failing only within a few ulps of
the other corner, so each step gains a few ulps of a distance of up to 2^62 doubles, and every
failing candidate also asks the server for the platform's `atan2` answers.

`differential` now evaluates at most `--shrink-limit` candidates per mismatch, 10000 by default,
and counts the rest as passing unrun, so the printed case is the last input that failed, judged
again with the ledger. The same binaries at `4599b4763`, whose code is `3fede604a`'s, with the
default limit:

| seed | printed case | shrinking | printed | exit |
| --- | --- | --- | --- | --- |
| 90210 | `[{ x: 9999.999999997632, y: 0 }, { x: -9999.999999997712, y: -9999.999999999973 }, { x: -5e-324, y: -743 }, { x: 6.507426219359772e-255, y: -785 }]` | `shrinking stopped after 10000 attempts; this case may not be minimal` | `4 functions, 32756 inputs, 1 mismatched, 3 allowed by the ledger (PLATFORM-MATH-ATAN2 3)` | 1 |
| 7 | `[{ x: 1e+306, y: 0 }, { x: 0, y: 1e+306 }, { x: -1, y: 1.000000000000001e+306 }, { x: 0, y: 0 }]` | `shrunk in 117 attempts` | `4 functions, 35957 inputs, 1 mismatched, 2 allowed by the ledger (PLATFORM-MATH-ATAN2 2)` | 1 |
| 424242 | `[{ x: 0, y: 0 }, { x: 0, y: -46 }, { x: 1, y: 0 }, { x: 1.9567680809018388e-15, y: 5e-324 }]` | `shrunk in 902 attempts` | `4 functions, 34275 inputs, 1 mismatched, 3 allowed by the ledger (PLATFORM-MATH-ATAN2 3)` | 1 |

Both binaries printed the same three rows. Each run took 2 to 4 seconds of wall time, and each
mismatch was refused with `PLATFORM-MATH-ATAN2: Rust's outcome is not the TypeScript's with the
platform library's answers`. Fed back alone to each mutated server through `runDifferential`, every
printed case mismatched again (seed 90210: `$.returned.upperLeft.y: expected -785, got -743`), and
the unchanged server matched all three.

The earlier mutations shrink to the end well inside the limit. With `--shrink-limit 100000000` they
printed the cases recorded above and took: `rotate_about` turning by `-rad` 12 attempts at seed 1
and 68 at 90210; `rotate` taking `rad.cos() * (1.0 + 4e-16)` 4 and 68; the corner swap 3023 at
90210, 2894 at 7 and 2575 at 424242. 3023 is the most, a third of the default.

`a_degenerate_quad_is_labelled_only_as_one_ulp_of_the_platform_atan2_allows` had stopped calling
`angle_of` (`f217af58f`), so an `angle_of` returning `m[0].atan2(m[1])` passed both pins. It now
requires `angle_of` to give this platform's `atan2` to the bit on each recorded pair, and that
mutation, in a copy under `/tmp`, fails it:
``assertion `left == right` failed: angle_of([3.6140106882042333e-20, -7.5e-1, ..])``. The cos and sin
pin already calls `rotate` beside `rotate_about`. The copies were removed afterwards.

## Full suite, 2026-11-17

At `4599b4763`, whose code is `3fede604a`'s, on a clean tree, in container `embed-pdf` with Node
v24.20.0, pnpm 10.34.0, rustc 1.98.0 (88d9e12ae 2026-08-18), cargo 1.98.0 (797e8a9bc 2026-08-05) and
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
| `pnpm run test:harness` | 0 | tests 103, pass 103, fail 0 |
| `pnpm run vectors:check` | 0 | `119 vector files are current, and each is replayed` |
| `pnpm run coverage:check` | 0 | `coverage is current: statements 72853/77093, branches 14365/15719, 935 lines reached only by snapshot-none cases, 36 ported files no generator loads` |
| `pnpm run replay:check` | 0 | `cargo: passed`, `checkout: 4599b476381c243fcee50eec4d4a137b2f52d7a9, clean`, `receipts: 119 read`, `119 vector files, 15922 cases, all replayed` |
| `pnpm run rust-coverage:check` | 0 | `rust coverage is current: replay reaches 64253/112591 lines; 37035 lines only other tests reach, in 673 files (74 excluded)` |
| `pnpm run differential -- --crate geometry` | 0 | the five runs below |

| seed | runs | printed | exit |
| --- | --- | --- | --- |
| 1 | 10000 | `37 functions, 370000 inputs, 0 mismatched, 5 allowed by the ledger (PLATFORM-MATH-ATAN2 5)` | 0 |
| 2 | 10000 | `37 functions, 370000 inputs, 0 mismatched, 3 allowed by the ledger (PLATFORM-MATH-ATAN2 3)` | 0 |
| 3 | 10000 | `37 functions, 370000 inputs, 0 mismatched, 3 allowed by the ledger (PLATFORM-MATH-ATAN2 3)` | 0 |
| 90210 | 10000 | `37 functions, 370000 inputs, 0 mismatched, 8 allowed by the ledger (PLATFORM-MATH-ATAN2 8)` | 0 |
| 31337 | 50000 | `37 functions, 1850000 inputs, 0 mismatched, 24 allowed by the ledger (PLATFORM-MATH-COS-SIN 1, PLATFORM-MATH-ATAN2 23)` | 0 |

The allowed counts are the ones recorded at `cff7e66f4`: the shrink limit acts only after a
mismatch, and these runs have none. A 10000 run took 28 seconds of wall time (seed 3), the 50000
run 134. `test:harness` has 103 tests, the 101 at `cff7e66f4` and the two shrinking tests this
change adds.

The cargo test run inside `replay:check` printed 162 `test result:` lines, which sum to 872 passed,
0 failed and 1 ignored. `replay:check` exits 0 only when every receipt carried the run's id, the
commit, a clean-tree flag and the vector hash, none was written with failures, member order off or
the allowlist replaced, and every one of the 19 paths in `member-order-allow.json` was used.
