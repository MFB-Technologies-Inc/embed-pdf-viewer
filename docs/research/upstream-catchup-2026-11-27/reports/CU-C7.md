# CU-C7: Rust and Node integration, and the full table

`research/catchup-2026-11-27-plugins` is fast-forwarded to CU-C6's reviewed tip `643d2b429`
(CU-C7-D1) and carries two code commits, a regenerated coverage record and this record. Every Rust and Node row is measured on
the head and on the start `d250a68e3`, and nothing is worse than the start. One row was worse on
the first pass: workspace clippy found an unused import the port left in
`crates/plugins/form/src/ffi.rs`, fixed in `2c49e0685` (CU-C7-D7). A review fix round added `rust-coverage:check`, stale at both
commits, and regenerated the record in `57ad31610` (CU-C7-D8). There is no NEW DIVERGENCE. The
decisions index for the whole catch-up is [`../decisions.md`](../decisions.md); this job's
decisions are in [`../decisions/CU-C7.md`](../decisions/CU-C7.md).

## Commits

| Commit | Summary |
|---|---|
| `84a64f0d8` | Job E review Minors: `lent_record` borrows the shown row's object and clones it only on a miss (`crates/plugins/annotation/src/read/view.rs`); `read/property_values.rs` doc says four reads and names orientation, as upstream's `property-values.ts`; `annotation-core/src/shapes/boxes.rs` doc rewrapped to 100 columns; `decisions/CU-C6.md` D9 cites `../../upstream-catchup-2026-11-18/reports/CU-C7.md:47` and `:108` |
| `2c49e0685` | `crates/plugins/form/src/ffi.rs` drops the unused `use std::rc::Rc;` |
| `cf65c1cc2` | this report, `decisions/CU-C7.md`, `decisions.md`, `scripts/cu-c7/` |
| `57ad31610` | `tooling/plugin-conformance/vectors/rust-coverage.json` regenerated (CU-C7-D8) |
| the fix-round docs commit | D8 rewritten, this row and the coverage row, `scripts/cu-c7/rustcov.sh` |

The branch moved `0e83ecb84..643d2b429` by fast-forward, on the host and in the container clone,
before these commits. Container worktree: `/workspaces/wt/CU-C7`.

## Checks, start against now

Start is `d250a68e3`. Now is `84a64f0d8` for cargo test, engine `:check`, the plugin checks,
`replay:check`, evidence and the wasm rows, `2c49e0685` for clippy, `2c49e0685`'s tree for the
TS row, and `57ad31610` for `rust-coverage:check`. `2c49e0685` removes one import from a file only the `uniffi` feature compiles, so it cannot
move the other rows; workspace clippy compiled it with every target. Start numbers marked "CU-C2"
or "CU-C3" are those jobs' runs of `d250a68e3` in this container today; every other start number
was measured here (CU-C7-D3).

| Check | Start (`d250a68e3`) | Now |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,567 passed, 0 failed, 2 ignored (CU-C2) | 3,616 passed, 0 failed, 2 ignored |
| `cargo clippy --workspace --all-targets` | exit 0; 1 warning, unused dependency `uniffi` in `epdf-uniffi-bindgen-cs` | exit 0; the same 1 warning (was 2 at `84a64f0d8`) |
| engine `:check` scripts | 36 of 36 (CU-C2) | 36 of 36 |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | 3 of 4, `coverage:check` fails in `c8` under node 26 (CU-C2) | 4 of 4 |
| plugin `replay:check` | pass: 123 files, 16,005 cases | pass: 124 files, 16,076 cases |
| plugin evidence tests | 769 of 771 (CU-C3) | 769 of 771, the same 2 failing |
| TS `turbo run test` over `packages/**` | 97 of 98 tasks; 8,767 passed, 2 failed, 23 skipped (CU-C2) | 97 of 98 tasks, `--force`, 0 cached; 9,225 passed, 2 failed, 23 skipped |
| Rust-core `wasm32-eh` payload (emsdk 4.0.0) | built; 1,065 bound functions match; `verify-rust-core.mjs` ok | built; 1,070 bound functions match; `verify-rust-core.mjs` ok |
| Rust-core plain `wasm32` payload | does not link: `undefined symbol: __cpp_exception` | does not link: `undefined symbol: __cpp_exception` |
| `core-wasm` vitest | 131 of 131 (13 files) | 131 of 131 (13 files) |
| `core-wasm` `test:gc` | 13 of 13 | 13 of 13 |
| `core-wasm` `typecheck` | pass | pass |
| `epdf-web` wasm tests, 9 integration binaries | 63 passed, 0 failed | 63 passed, 0 failed |
| `epdf-node` addon (`addon.mjs`, release build) | 6 checks passed | 6 checks passed |
| `rust-coverage:check` | exit 1, stale; measured: replay reaches 66,015 of 115,197 lines (57.31%), 37,102 only other tests reach, 701 files | exit 1, stale, at `2c49e0685`; exit 0 after `57ad31610`: 67,204 of 116,903 (57.49%), 37,516, 706 files |

The 49 more cargo tests are CU-C4's to CU-C6's. The 71 more replayed cases and the new file are
`plugins/form-groups.json` and the cases CU-C5 and CU-C6 added. The 5 more bound functions are
CU-C1's export changes (8 added, 3 removed). The 458 more TS tests are upstream's, already counted
at Job A's `f8a124edc` (9,225), so the lockfile moves of CU-C3-D6 change no TS result
(CU-C3-D11 closed). The plugin check that passes now and failed at the start is CU-C3-D6's `c8` 12.

### The failures left, at both commits

The 2 evidence failures are `current-suites.test.ts`: `grid-nan-columns` observes the kept M27 NaN
divergence, and the second test then reads a result the first did not record (`Cannot read
properties of undefined (reading '6')`), as 2026-11-18 CU-C7 describes. The 2 TS failures are
`@embedpdf/core-signature` `test/revocation/interop.test.ts`, whose OCSP answers expired
`2026-11-25T00:00:00Z` (CU-C2). Both are upstream's tests.

### The plain `wasm32` row

`build-target.sh wasm32` with the Rust core fails at both commits with the same link error, from
`libepdf_wasm.a`'s `epdf_wasm` and `pdfium` objects. The payload has been `wasm32-eh` since
2026-11-12 (`../../upstream-catchup-2026-11-18/reports/CU-C7.md:47`), so this is not a regression
(CU-C6-D9). The start's first run of this row failed earlier, on stale headers, and its rerun is the
one in the table (CU-C7-D4).

## How the rows ran

`scripts/cu-c7/prep.sh` prepped `/workspaces/wt/CU-C7` from CU-C6's worktree. `scripts/cu-c7/table.sh`
runs `../suite.sh` (cargo, engine `:check`, the four plugin checks, TS), workspace clippy,
`replay:check` and the evidence tests. `scripts/cu-c7/wasm.sh` builds the plain `wasm32` payload,
puts prep's payload back, builds `wasm32-eh` with `/opt/emsdk-eh`, runs `verify-rust-core.mjs`,
`build:wasm`, the three `core-wasm` checks, `epdf-web` under `wasm-bindgen-test-runner`, and the
release addon with `addon.mjs`, then restores prep's payload (the Rust-core one is kept in the
log dir as `rust-core-lib/`). `scripts/cu-c7/ts-count.py` sums a turbo log's test counts; it
gives Job A's 8,767 and 9,225 back from their logs. Head: `T=/workspaces/target-c5`, `TOUCH=1`.
Start: `/workspaces/wt/base`, `T=/workspaces/target-base`, the `wasm32-eh` pin
`scripts/cu-c7/base-pin.json` (`af480d0ce`, sha256 `c6597d25…`).

## Divergences

No NEW DIVERGENCE: no vector observes a difference. `decisions.md` lists the Rust-only differences
the port jobs recorded (CU-C4-D7, CU-C4-D9, CU-C6-D7) first, and every decision so far, 59
headings (`scripts/cu-c7/index-check.sh`: `missing=0`).

`rust-coverage` was stale before this catch-up: the record dates from `94b2e7be0`, and its 68,355
of 118,531 lines were measured on 2026-11-18's toolchain. Both sides were measured here on the
same container, each in its own target dir, so they compare; the second check after the write
re-measured the same numbers (CU-C7-D8).

## Evidence

Logs in `/workspaces/logs/CU-C7/` in the container. Hashes are sha256, first 8 hex digits.

| Check | Tree | Command | Exit | Log | sha256 |
|---|---|---|---|---|---|
| Prep | `643d2b429` | `prep.sh /workspaces/wt/CU-C7 /workspaces/wt/CU-C6` | 0 | `prep.log` | `81b5fa33` |
| Minors | `84a64f0d8` tree | `cargo fmt --check`, `cargo clippy --all-targets`, `cargo test` of `epdf-plugin-annotation` and `epdf-annotation-core` | 0, 0, 0 (103 passed) | `minors/fmt.log`, `minors/clippy.log`, `minors/test.log` | `e3b0c442`, `9a9dc03d`, `76c9260f` |
| cargo, engine `:check`, plugin checks | `84a64f0d8` | `TOUCH=1 table.sh /workspaces/wt/CU-C7 head` | 0 | `head/cargo.log`, `head/cargo.summary`, `head/engconf.all`, `head/plugconf.all` | `d197d3e4`, `40bf05a1`, `27a10bbd`, `cf0bfd16` |
| clippy, first pass | `84a64f0d8` | `table.sh` | 0, 2 warnings | `head/clippy.log` | `ea55937f` |
| clippy, after the fix | `2c49e0685` tree | `cargo clippy --workspace --all-targets` | 0, 1 warning | `head-fix/clippy.log` | `1c75f8ba` |
| `replay:check` | `84a64f0d8` | `table.sh` | 0 | `head/plugconf.replay:check.log` | `bafacbe5` |
| Evidence | `84a64f0d8` | `table.sh` | 769 pass, 2 fail | `head/evidence/summary.txt` | `6290cd61` |
| TS, cached (not used) | `84a64f0d8` | `suite.sh … ts` | 1 | `head/ts.log` | `e29db181` |
| TS | `2c49e0685` tree | `turbo run test --force --continue --concurrency=6 --filter='./packages/**'` | 1 (2 OCSP tests) | `head/ts-force.log` | `849418ae` |
| wasm rows, head | `84a64f0d8` | `wasm.sh /workspaces/wt/CU-C7 head-wasm /workspaces/wasm32-eh-pin.json` | payload-wasm32 1, the rest 0 | `head-wasm/wasm.all`, `payload.log`, `payload-wasm32.log`, `verify-rust-core.log`, `core-wasm-vitest.log`, `core-wasm-gc.log`, `epdf-web.log`, `addon.log` | `ba079472`, `54256647`, `4d5e127e`, `0fca4c73`, `5eae6e18`, `1678f512`, `64ca062b`, `17b0639b` |
| clippy, start | `d250a68e3` | `T=/workspaces/target-base table.sh /workspaces/wt/base base clippy replay` | 0, 1 warning | `base/clippy.log` | `252836f4` |
| `replay:check`, start | `d250a68e3` | same | 0 | `base/plugconf.replay:check.log` | `97358d10` |
| wasm rows, start | `d250a68e3` | `T=/workspaces/target-base wasm.sh /workspaces/wt/base base-wasm scripts/cu-c7/base-pin.json` | payload-wasm32 101 (stale headers), the rest 0 | `base-wasm/wasm.all`, `payload.log`, `core-wasm-vitest.log`, `core-wasm-gc.log`, `epdf-web.log`, `addon.log` | `5868a357`, `f46a8a21`, `5f3bc98e`, `23c4f167`, `e97dd906`, `17b0639b` |
| `rust-coverage:check`, head | `2c49e0685` | `rustcov.sh /workspaces/wt/CU-C7 rustcov check` (`T=/workspaces/target-cov`) | 1 (stale) | first run of `rustcov/rust-coverage-check.log` (overwritten; output in the job report) | |
| `rust-coverage` write, then check | `2c49e0685` tree, then `57ad31610` | `rustcov.sh … rustcov write`, `rustcov.sh … rustcov check` | 0, 0 | `rustcov/rust-coverage-write.log`, `rustcov/rust-coverage-check.log` | `ea63b806`, `9f485efd` |
| `rust-coverage`, start | `d250a68e3` | `T=/workspaces/target-cov-base rustcov.sh /workspaces/wt/base rustcov-base check`, then `write` (file restored) | 1 (stale), 0 | `rustcov-base/rust-coverage-check.log`, `rustcov-base/rust-coverage-write.log` | `8398e9b0`, `52a870f4` |
| plain `wasm32`, start rerun | `d250a68e3` | `build-target.sh wasm32` | 1 (link) | `base-wasm/payload-wasm32.log`, `payload-wasm32-rerun.log` | `2d3e1961`, `e12da2d2` |

Environment: container `embed-pdf-catchup1127`, image `embedpdf-dev:20261127-d670a1cbfb10`,
linux-arm64, cargo 1.100.0, node 26.13.1, emsdk 4.0.0 (`/opt/emsdk-eh`) for `wasm32-eh` and
3.1.72 for plain `wasm32`, `wasm-bindgen` 0.2.130. Every heavy step ran through `heavy-build.sh`
with owner `CU-C7-*`; host free space read 628 GB at the start.

## Left for CU-C11

`/workspaces/wt/CU-C7` (keep for CU-C8, which starts from this tip), `/workspaces/target-base`
(the start's builds, deletable), `/workspaces/target-cov` (6.8 GB, the coverage build;
`/workspaces/target-cov-base` is already deleted), `/workspaces/bundles/CU-C7c.bundle`, `/workspaces/bundles/CU-C7a.bundle`, and the earlier jobs'
leftovers CU-C6 listed. `/workspaces/target-c5` now holds this tip's builds.
