# CU-C6: Rust and Node integration, and the full table

`research/catchup-2026-11-27b-plugins` merges plugins' tip `e3d6e7fbc`, closes CU-C5's three review
Minors, re-pins the evidence archives and regenerates the Rust coverage record. Every Rust and Node
row is measured on the head and on the start `0afd87825`, and nothing is worse than the start. The
evidence tests are back to the start's 2 failures, with a summary byte-identical to CU-C3's
baseline run. There is no NEW DIVERGENCE. The decisions index is [`../decisions.md`](../decisions.md);
this job's decisions are in [`../decisions/CU-C6.md`](../decisions/CU-C6.md).

## Commits

| Commit | Summary |
|---|---|
| `5a6d266dc` | Merge plugins `e3d6e7fbc` (CU-C6-D1): two `platforms/dotnet` test files |
| `0b55dab9f` | CU-C5 review Minors: `drawing_index`'s doc names stamps, marks and push buttons (`crates/epdf-engine/src/session/document.rs:676-677`); `drawing_for_format`'s PDF refusal takes upstream's text (`crates/epdf-engine/src/features/annotation/write/stamp.rs:180`, CU-C6-D5); the `NativeSecurity` anyone test covers all 8 tokens, `sender` and `designer` added (`crates/epdf-viewer-host/src/native_engine.rs:1296`) |
| `f69b5f3c5` | Evidence archives re-pinned, 18 files under `tooling/plugin-conformance/historical/` (CU-C6-D4) |
| `22f7abd8c` | `tooling/plugin-conformance/vectors/rust-coverage.json` regenerated (CU-C6-D6) |
| the commit that adds this file | Report, `decisions/CU-C6.md`, `decisions.md`, `scripts/cu-c6/` |

`5a6d266dc` and `0b55dab9f` were made on the host and brought into the container by bundle;
`f69b5f3c5` and `22f7abd8c` were made in the container (`/workspaces/wt/CU-C2`) and brought back
as bundles `CU-C6c` and `CU-C6d`, fast-forward only. Plugins' tip merged is `e3d6e7fbc`. Release
train run `46080379002` was `in_progress` at the merge, so an Apple engine pin commit may follow;
the controller merges it before the gate.

## Checks, start against now

Start is `0afd87825`. Now is `0b55dab9f` for cargo test, engine `:check`, clippy, the TS row and
the wasm rows, and `f69b5f3c5` for the evidence tests, the four plugin checks and `replay:check`
(the plugin checks also passed at `0b55dab9f`). `f69b5f3c5` changes only JSON under `historical/`,
which no Rust or wasm row reads. `rust-coverage:check` ran at `f69b5f3c5` (stale) and at
`22f7abd8c`'s tree (current). Start numbers marked CU-C2 or CU-C3 are those jobs' runs of
`0afd87825` in this container; the rest were measured here (CU-C6-D3).

| Check | Start (`0afd87825`) | Now |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,630 passed, 0 failed, 2 ignored (CU-C2) | 3,698 passed, 0 failed, 2 ignored |
| `cargo clippy --workspace --all-targets` | exit 0; 1 warning, unused dependency `uniffi` in `epdf-uniffi-bindgen-cs` | exit 0; the same 1 warning |
| engine `:check` scripts | 36 of 36 (CU-C2) | 36 of 36 (`engconf.all` `27a10bbd`, as at the start) |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | 4 of 4 (CU-C2) | 4 of 4 |
| plugin `replay:check` | pass: 124 files, 16,076 cases | pass: 124 files, 16,076 cases |
| plugin evidence tests | 769 of 771 (CU-C3) | 769 of 771, the same 2 failing |
| TS `turbo run test` over `packages/**` | 97 of 98 tasks; 9,226 passed, 2 failed, 22 skipped (CU-C2) | 97 of 98 tasks, `--force`, 0 cached; 9,362 passed, 2 failed, 22 skipped |
| Rust-core `wasm32-eh` payload (emsdk 4.0.0) | built; 1,070 bound functions match | built; 1,070 bound functions match |
| `verify-rust-core.mjs` | ok | ok |
| Rust-core plain `wasm32` payload | does not link: `undefined symbol: __cpp_exception` | does not link: `undefined symbol: __cpp_exception` |
| `core-wasm` vitest | 131 of 131 (13 files) | 131 of 131 (13 files) |
| `core-wasm` `test:gc` | 13 of 13 | 13 of 13 |
| `core-wasm` `typecheck` | pass | pass |
| `epdf-web` wasm tests, 10 integration binaries | 65 passed, 0 failed | 65 passed, 0 failed |
| `epdf-node` addon (`addon.mjs`, release build) | 6 checks passed | 6 checks passed |
| `rust-coverage:check` | exit 0, current: replay reaches 67,204 of 116,903 lines (57.49%), 37,516 only other tests reach, 706 files | exit 1, stale, at `f69b5f3c5`; exit 0 at `22f7abd8c`: 67,236 of 117,613 (57.17%), 38,193, 708 files |

The 68 more cargo tests are CU-C4's and CU-C5's; this job's Minors extend one test and add none.
The 136 more TS tests are upstream's range, already counted at CU-C2's head `117945420` (9,362),
so the merged `packages/**` is unchanged by this catch-up's commits. The 2026-11-27 run counted 9
`epdf-web` binaries and 63 tests; `tests/form_groups.rs` (2 tests) was added on plugins before
`0afd87825`.

Replay reaches 32 more lines than at the start, but its share falls by 0.32 points. The 710 lines
the record gained are CU-C4's and CU-C5's engine code (anyone fields, picture buttons, the shared
drawing), which the engine vectors and the Rust tests reach and the plugin replay does not. The
check itself is a pass at both ends.

### The failures left, at both commits

The 2 evidence failures are `current-suites.test.ts`: `grid-nan-columns` where `change-hooks` was
expected (the kept M27 NaN divergence), then `Cannot read properties of undefined (reading '6')`
in the test that reads the first one's result. The 2 TS failures are `@embedpdf/core-signature`
`test/revocation/interop.test.ts`, whose OCSP answers expired `2026-11-25T00:00:00Z` (CU-C2). Both
are upstream's tests.

## Evidence re-pin

At `0b55dab9f` the evidence tests passed 752 of 771, the 2 known failures plus 17 in 13 files:
input digests, source hashes and C-series capture inputs. `f69b5f3c5` re-pins them with the
2026-11-27 sequence (CU-C6-D4). After the source re-pins, the captures and the re-stamp, 764 of 771
passed; after `repin-loop.sh` (62 more swaps for the two form files) and `chainpins.py`, 769 of 771.
`head-ev2/evidence/summary.txt` and CU-C3's `ev-base/summary.txt` are byte-identical (`6290cd61`).
No test file changed: every moved digest is a canonical input digest or a source hash, and every
changed input differs only by `forAnyone: false` on field rows and `layout: null` on widget rows.

## CU-C5 review Minors

All three are in `0b55dab9f`. `drawing_for_format` now refuses an unopenable PDF with "the
appearance PDF could not be opened", the text upstream's `drawingFor` raises through
`withAppearanceDocument`
(`packages/engine/services/src/features/annotations/internal/appearanceDocument.ts:32`); the stamp
preflight keeps "the stamp's appearance PDF could not be opened", which `requireStampContent` uses
(`packages/engine/services/src/features/annotations/internal/write/writeStampAnnotation.ts:198`).
The anyone test now asks all 8 tokens of `ANYONE_FIELD_TOKENS`
(`packages/engine/core/src/conformance/runAnyoneFieldsConformance.ts:108`); `sender` and `designer`
fill in all four targets, as the runner's table expects
(`packages/engine/core/src/conformance/runAnyoneFieldsConformance.ts:322`, `:328`). `cargo fmt
--check` is clean on the three files; `cargo test -p epdf-viewer-host --lib
a_group_token_fills_in` passes.

## Rust API surface for CU-C7 and CU-C8

Unchanged from CU-C5's list ([`CU-C5.md`](CU-C5.md), "New Rust API surface for CU-C7 and CU-C8").
This job changed one error text and two tests, and the merge brought only `platforms/dotnet` tests.

## How the rows ran

`scripts/cu-c6/` holds 2026-11-27's `cu-c7/table.sh`, `wasm.sh`, `rustcov.sh`, `ts-count.py` and
`index-check.sh` with the log root moved to `CU-C6` and `T` defaulting to `/workspaces/target`;
`table.sh` gains a `tsforce` part and a `SUITE_PARTS` override. The re-pin scripts are copies of
`cu-c3/` with their refs moved (`srcpins.py` to `0afd87825`, `owners.py` to
`9af4d9ee7..cda9e82a4`, `chainpins.py`'s state under `CU-C6`). Head:
`table.sh /workspaces/wt/CU-C2 head suite clippy replay evidence tsforce`, then `wasm.sh
/workspaces/wt/CU-C2 head-wasm /workspaces/wasm32-eh-pin.json`. Start: `T=/workspaces/target-base
table.sh /workspaces/wt/base base clippy replay` and `wasm.sh /workspaces/wt/base base-wasm
/workspaces/wasm32-eh-pin.json`. Coverage: `rustcov.sh` with `T=/workspaces/target-cov` (head) and
`/workspaces/target-cov-base` (start).

## Evidence

Logs in `/workspaces/logs/CU-C6/` in container `embed-pdf-catchup1127b`. Hashes are sha256, first 8
hex digits.

| Check | Tree | Command | Exit | Log | sha256 |
|---|---|---|---|---|---|
| Minors test | `0b55dab9f` tree | `cargo fmt --all --check` (three files clean), `cargo test -p epdf-viewer-host --lib a_group_token_fills_in` | 0 | (console) | |
| cargo, engine `:check`, plugin checks | `0b55dab9f` | `table.sh … head` | 0 | `head/cargo.log`, `head/cargo.summary`, `head/engconf.all`, `head/plugconf.all` | `003bcea4`, `2014460a`, `27a10bbd`, `cf0bfd16` |
| clippy | `0b55dab9f` | same | 0, 1 warning | `head/clippy.log` | `0021d974` |
| TS | `0b55dab9f` | same, `tsforce` | 1 (2 OCSP tests) | `head/ts-force.log` | `6a043608` |
| Evidence before the re-pin | `0b55dab9f` | `table.sh … head-ev0 evidence` | 752 pass, 19 fail | `head-ev0/evidence/summary.txt` | `ceca2baf` |
| Re-pin steps | | `srcpins.py`, `owners.py`, captures, `restamp.mts`, `chainpins.py`, `sed`, `repin-loop.sh` | 0 | `repin/srcpins.log`, `owners.log`, `restamp.log`, `chainpins.log`, `sourcehashes.log`, `repin-loop.log`, `repin-loop2.log` | `2f18074e`, `73911066`, `b9b1cb45`, `89e7cc5e`, `6386780e`, `b30c330e`, `a9248503` |
| Evidence midway | uncommitted | `table.sh … head-ev1 evidence` | 764 pass, 7 fail | `head-ev1/evidence/summary.txt` | `2b4803d3` |
| Evidence final | `f69b5f3c5` tree | `table.sh … head-ev2 evidence` | 769 pass, 2 fail | `head-ev2/evidence/summary.txt` | `6290cd61` |
| Plugin checks, `replay:check` | `f69b5f3c5` | `SUITE_PARTS=plugconf table.sh … head-final suite replay` | 0 | `head-final/plugconf.all`, `head-final/plugconf.replay:check.log` | `cf0bfd16`, `d02db678` |
| wasm rows, head | `0b55dab9f` | `wasm.sh /workspaces/wt/CU-C2 head-wasm …` | payload-wasm32 1, the rest 0 | `head-wasm/wasm.all`, `payload.log`, `payload-wasm32.log`, `verify-rust-core.log`, `core-wasm-vitest.log`, `core-wasm-gc.log`, `epdf-web.log`, `addon.log` | `ba079472`, `27e515a1`, `815cfa62`, `0fca4c73`, `03b0a4c8`, `bfd7bf39`, `0e3b9b1e`, `17b0639b` |
| clippy, `replay:check`, start | `0afd87825` | `T=/workspaces/target-base table.sh /workspaces/wt/base base clippy replay` | 0, 0 | `base/clippy.log`, `base/plugconf.replay:check.log` | `583ede6d`, `bf11df73` |
| wasm rows, start | `0afd87825` | `T=/workspaces/target-base wasm.sh /workspaces/wt/base base-wasm …` | payload-wasm32 1, the rest 0 | `base-wasm/wasm.all`, `payload.log`, `payload-wasm32.log`, `core-wasm-vitest.log`, `core-wasm-gc.log`, `epdf-web.log`, `addon.log` | `ba079472`, `e2ba4dc0`, `b6c6907d`, `70b256aa`, `9eb8b2f0`, `47c1498d`, `17b0639b` |
| `rust-coverage`, start | `0afd87825` | `T=/workspaces/target-cov-base rustcov.sh /workspaces/wt/base rustcov-base check`, then `write` (no diff) | 0, 0 | `rustcov-base/rust-coverage-check.log`, `rust-coverage-write.log` | `a466d919`, `be1e7321` |
| `rust-coverage:check`, head | `f69b5f3c5` | `rustcov.sh /workspaces/wt/CU-C2 rustcov check` | 1 (stale) | `rustcov/rust-coverage-check-first.log` | `898fcb8f` |
| `rust-coverage` write, then check | `f69b5f3c5`, then `22f7abd8c`'s tree | `rustcov.sh … rustcov write`, `rustcov.sh … rustcov check` | 0, 0 | `rustcov/rust-coverage-write.log`, `rustcov/rust-coverage-check.log` | `678b87a6`, `c35e5fde` |

`head/evidence/summary.txt` (`f5662c89`, 767 pass, 4 fail) is the table's own evidence pass, which
ran while the re-pin was half done; it is not used. Environment: image `embedpdf-dev:latest`
(`a3b58f11b8e0`), linux-arm64, rustfmt 1.11.0, emsdk 4.0.0 (`/opt/emsdk-eh`) for `wasm32-eh`,
`cargo-llvm-cov` 0.9.2.

## Known gaps

- `cargo fmt --all --check` fails at both commits on `crates/epdf-node/src/lib.rs` and
  `crates/pdfium/tests/documents.rs`, neither touched by this catch-up (CU-C6-D8).
- CU-C5-D3 stands: through the form plugin a group's token can't fill in a field anyone fills in,
  as on upstream.
- An Apple engine pin commit from release train `46080379002` may land on plugins after
  `e3d6e7fbc`.

## Cleanup

Deleted after the runs: `/workspaces/target-base` (4.6 GB), `/workspaces/target-cov` (7.0 GB),
`/workspaces/target-cov-base` (6.8 GB), the `target` symlinks `wasm.sh` left in both worktrees, and
the host copies of the bundles. `fstrim` is not permitted in the container. Kept for CU-C7:
`/workspaces/target` (22 GB), `/workspaces/wt/CU-C2` on the branch, `/workspaces/wt/base`, and
bundles `CU-C6a` to `CU-C6d` in `/workspaces/bundles/`. Volume use after cleanup: 435 GB of 1.8 TB.
