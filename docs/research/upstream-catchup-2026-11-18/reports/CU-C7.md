# CU-C7: Rust and Node integration, the full table, and the deferred minors

`research/catchup-2026-11-18-plugins` is fast-forwarded to CU-C6's tip `0e1586fc0` and carries this
job's commits. Every Rust and Node row is measured on the head and on the start `cf7fdbe7a`, and
nothing is worse than the start. The first pass left two rows worse, the plugin evidence tests
(761 of 771) and `rust-coverage:check` (stale record). A controller follow-up closed both: a stale
fixture in one CU-C6 case, the catch-up's cases counted from a generated archive, and a regenerated
coverage record (CU-C7-D11, CU-C7-D9). No Rust behaviour changed here and there is no NEW
DIVERGENCE. All seven deferred minors are fixed or recorded. The decisions index for the whole
catch-up is [`../decisions.md`](../decisions.md); this job's decisions are in
[`../decisions/CU-C7.md`](../decisions/CU-C7.md).

## Commits

| Commit | Minor | Summary |
|---|---|---|
| `9982aeb10` | 1 | `lib-exports.sh` and `runtime-verify.sh` write to `mktemp -d`; `pdfium-sys-check.sh`'s probe glob is `"$C"/target/...` |
| `f6481da43` | 2 | `@playwright/test` `1.63.0` in `tooling/viewer-e2e` and `tooling/wasm-eh-check`; lockfile |
| `d60c1758a` | 4 | `signature_read.rs:32` says policy version 5; `signatures.addValidationInfo` last in `TRANSACTED_KINDS`; two `session/authority.rs` tests |
| `394c34207` | 6 | refusing `image_bytes` test in `commands/tests/lists.rs`; `ToolPermission` deadlock warning |
| `bdc139736` | 2, 5, 7 | `reports/CU-C2.md` "plan D-3"; 43 and 7 answers in the signature-core README and `reports/CU-C4b.md`; `reports/CU-C6.md` coverage and Divergences, `decisions/CU-C6.md` D7 |
| `2615a3bec` | | this report, `decisions/CU-C7.md`, `decisions.md`, `scripts/cu-c7/` (first pass) |
| `46a1089b3` | | follow-up: the pen drawing case answers its writes at 200 ms; `plugins/annotation-tools.json` |
| `c277f21c6` | | follow-up: `historical/catchup-2026-11-18/additions.json`, `test/catchup-additions.ts`, six evidence tests count the catch-up's cases; `task-12b2b1b-ii/family.json` chained pin |
| `5f4abdabc` | | follow-up: `vectors/rust-coverage.json` regenerated |
| the docs commit | | this report, the decisions, `scripts/cu-c7/additions.mjs` and `regen.sh` (follow-up) |

The branch moved `9041d7510..0e1586fc0` by fast-forward (CU-C7-D1), in the container clone and on
the host, before these commits.

## Checks, start against now

Start is `cf7fdbe7a`. Now is `5f4abdabc` for cargo, the plugin checks, `replay:check`, evidence and
`rust-coverage`, re-run after the follow-up, and `bdc139736` for the rest: the follow-up changed
only `tooling/plugin-conformance` test data and records, which those rows don't read. Start numbers
marked "CU-C3" are CU-C3's baseline run of `cf7fdbe7a` in this container on the same day with the
same suite script (CU-C7-D2). Every other start number was measured here.

| Check | Start (`cf7fdbe7a`) | Now |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,467 passed, 0 failed, 2 ignored (CU-C3) | 3,514 passed, 0 failed, 2 ignored |
| engine `:check` scripts | 35 of 35 (CU-C3) | 36 of 36 (`signature-ltv:check` is new) |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | all pass (CU-C3) | all pass |
| plugin `replay:check` | pass: 119 files, 15,922 cases (CU-C3) | pass: 123 files, 16,005 cases |
| plugin evidence tests | 766 of 768 (CU-C3) | 769 of 771, the same 2 failing |
| TS `turbo run test` over `packages/**` | 98 of 98 (CU-C3) | 98 of 98, run with `--force` |
| Rust-core `wasm32` payload (`wasm32-eh`, emsdk 4.0.0) | built; 1,061 bound functions match | built; 1,065 bound functions match |
| `core-wasm` vitest | 108 of 108 (12 files) | 108 of 108 (12 files) |
| `core-wasm` `test:gc` | 13 of 13 | 13 of 13 |
| `core-wasm` `typecheck` | pass | pass |
| `epdf-web` wasm tests, 8 integration binaries | 59 passed, 0 failed | 59 passed, 0 failed |
| `epdf-node` addon (`addon.mjs`, release build) | 6 checks passed | 6 checks passed |
| `rust-coverage:check` | current: a fresh measurement reproduces the record exactly | exit 0 after `5f4abdabc` (was exit 1, stale, at `bdc139736`) |

The 47 more cargo tests are CU-C4a to CU-C6's and this job's 3 (two in `session/authority.rs`,
one in `commands/tests/lists.rs`). The 4 more bound functions are the DSS exports
(`EPDFDss_GetCount`, `EPDFDss_GetData`, `EPDFDss_Add`, `EPDFDoc_GetLoadedObjectValue`).

### Plugin evidence tests

The first pass at `bdc139736` had 761 of 771: the 2 start failures and 8 new ones, all in tests
that pin archive counts or IDs.

| File | Test | Message at `bdc139736` |
|---|---|---|
| `annotation-live-ii.test.ts` | every owned mutation has exact current ops, result kind, refs, schema fields and pre-settlement event provenance | `ink-press-on-an-annotation-keeps-the-waiting-stroke`: opId `00000000-0000-7000-8000-000000000001`, expected `00000000-00c8-7000-8000-000000000001` |
| `annotation-live-ii.test.ts` | every original66 definition and canonical input maps exactly to the current68 cases | `38 !== 36` |
| `annotation-live.test.ts` | all82 current live scenarios execute with exact named refusals and no unexpected diagnostics | `86 !== 85` |
| `annotation-live.test.ts` | the original49 live definitions, all3 renames,33 additions and27 historical mappings account exactly | `19 !== 20` |
| `form-current.test.ts` | all 220 original definitions are archived, all 101 owned cases map exactly and the original archives remain available | `108 !== 102` |
| `form-script.test.ts` | exact 59-case migration closes every original 220 input against immutable archives and reviewed i/ii manifests | `231 !== 225` |
| `stamp-mutations.test.ts` | all original41 map exhaustively to complete current inputs with verified source owners | `42 !== 41` |
| `stamp-placement.test.ts` | aggregate accounts for all original211 and preserves every reviewed earlier disposition exactly | `224 !== 223` |

The opId failure is a stale fixture, not a TS and Rust difference. The recorded TS trace sends all
three `apply` calls of that case with `00000000-00c8-…` (200 ms, after its `advance(200)`), and
`replay:check` passes, so the Rust replay sends the same. Only the scripted answers CU-C6 wrote said
`00000000-0000-…`. `46a1089b3` gives them `{ ms: 200 }`, as the other live answers carry theirs; the
vectors regenerate identically twice and only `plugins/annotation-tools.json` changes. A scan of
every vector (`opids.mjs`) finds no other call and answer opId that differ.

The seven count failures are the cases CU-C6 added to `annotation-api`, `annotation-tools`,
`form-core` and `stamp-api`. CU-C3's re-pin scripts move hashes of changed source and don't touch
counts, so `c277f21c6` follows the tests' own 2026-11-12 mechanism (lists of later cases) with the
list generated rather than typed: `additions.mjs` writes the 83 cases the catch-up added between
`cf7fdbe7a` and `2615a3bec` to `historical/catchup-2026-11-18/additions.json`, and the tests count a
file's additions on top of their archives. CU-C3's `chainpins.py` then moved the one chained pin
that names a changed test file; `srcpins.py` and `owners.py` find nothing. Result: 769 of 771.

The 2 failures left are the start's, in `current-suites.test.ts`. `grid-nan-columns` (`f629719b5`,
2026-11-16, already at `cf7fdbe7a`) observes the kept M27 NaN divergence: TS's kernel reports
`RangeError: Invalid array length`, and the test allows a kernel report only from `change-hooks`.
The second, `Cannot read properties of undefined (reading '6')`, follows from the first: that test
aborts before it records `smooth-navigation-tween`, which the next test reads.

### `rust-coverage:check`

At `cf7fdbe7a` a fresh measurement reproduces `vectors/rust-coverage.json` byte for byte: replay
reaches 64,882 of 113,799 lines, 37,520 lines only other tests reach, 675 files (75 excluded). At
`bdc139736` the check exited 1, `vectors/rust-coverage.json is stale`. `5f4abdabc` regenerates the
record with the package's `rust-coverage` script in its own target dir: 68,345 of 118,359, 38,382,
700 files (76 excluded), a 1,291-insertion, 994-deletion diff. `rust-coverage:check` then exits 0.
Replay reaches more lines than before, so the drift was the record lagging the port, not lost
coverage.

### How the wasm and coverage rows ran

`scripts/cu-c7/wasm.sh` builds the payload (`build-target.sh wasm32-eh` with emsdk 4.0.0 and
`EM_BINARYEN_ROOT` and `EM_CONFIG` unset, as the 2026-11-12 `build-rust-payload.sh` does), then
runs `build:wasm`, the three `core-wasm` checks, `epdf-web` under `wasm-bindgen-test-runner` with
`--test '*'`, and `cargo build -p epdf-node --release` with `addon.mjs`. The numbers above come
from the start in its own target dir (`T=/workspaces/target-start`, `start-wasm/`,
`start-rustcov/`) and the head in the shared one with its sources touched first (`TOUCH=1`,
`head-wasm/`). Earlier runs that shared one target dir between the two checkouts are kept but not
used: Cargo took the other checkout's newer builds as fresh (CU-C7-D3). That showed as a head
payload failing on the start's `pdfium-sys` bindings (`head-wasm-run1/`) and the start's
`rust-coverage` failing on the head's engine (`start-rustcov-shared-target/`). The `cargo test`
table at head ran before any start build, so it isn't affected.

## Deferred minors

1. CU-C1 helper scripts: fixed (`9982aeb10`). Re-run, both give output identical to CU-C1's logs
   (`minor1-runtime-verify.log`, `minor1-lib-exports.log`). The five logs missing from the
   container are copied in, so CU-C1's sentence now holds (CU-C7-D10).
2. CU-C2: Playwright pinned to `1.63.0` (`f6481da43`); `next`'s optional peer keeps 1.65.1 in the
   lockfile (CU-C7-D4). "(D-3)" reads "(plan D-3)" (`bdc139736`).
3. CU-C3's `safe.directory '*'`: recorded (CU-C7-D8). It was set at 08:48:53 UTC during CU-C3's
   baseline prep with no cause written down, and no repository in the container needs it now.
4. CU-C4a: fixed (`d60c1758a`): the comment, `TRANSACTED_KINDS` (CU-C7-D7), and the tests
   `what_a_signature_forbids_is_protected_document_and_what_the_scope_lacks_is_forbidden` and
   `permit_enforces_nothing_and_still_reports_the_protection`.
5. CU-C4b: fixed (`bdc139736`). `core/signature-long-term.json` has 43 `checkRevocationAnswer`
   calls (`answers-ocsp` 26, `answers-crl` 17), against 36 at fix round 1 (`990c8a3ab`), so round
   2 added 7.
6. CU-C5: fixed (`394c34207`), with the test placed as CU-C7-D5 says. With `image_bytes`
   resolving it fails on the logged `saveFile` (`minors-mutation.log`).
7. CU-C6: report and D7 corrected (`bdc139736`); no case added (CU-C7-D6).

Left for the final whole-branch review, untouched: CU-C4a's `open_candidate` with unchanged bytes
and edits relying on two fork checks; CU-C4b's client trusting the host's reply limit; CU-C4b's
`route-max-reply-bytes` vector, which only proves the option parses; CU-C4b's content-type header
override.

## Divergences

No NEW DIVERGENCE: no vector observes a difference. Two Rust-only differences are recorded,
neither visible to a caller that gets an answer: CU-C6-D7 (the pen drawing's wait timer drops a
throwing `finishInkDraft` where upstream's escapes as an uncaught timer exception) and CU-C6-D2
(the two new signature ports compare by handle identity, extending `SigningKey::same`). The one
open proposed exception is CU-C4b-D3 (transport limits). `decisions.md` lists these first, under
the same heading wording.

## Evidence

Logs in `/workspaces/logs/CU-C7/` in the container and `tmp/cu1118/logs/CU-C7/` of the host viewer
checkout, hashed in its `SHA256SUMS`. Scripts in [`../scripts/cu-c7/`](../scripts/cu-c7/):
`env.sh`, `prep.sh`, `start-prep.sh`, `table.sh`, `evrun.sh`, `wasm.sh`, `rustcov.sh`, and for the
follow-up `regen.sh`, `additions.mjs`, `opids.mjs`. Hashes are
sha256, first 8 hex digits.

| Check | Head | Command | Exit | Log | sha256 |
|---|---|---|---|---|---|
| Prep, head worktree | `0e1586fc0` | `prep.sh /workspaces/wt/CU-C7 /workspaces/wt/CU-C6` | 0 | `prep-tip.log` | `a8cf6c75` |
| Prep, start worktree | `cf7fdbe7a` | `start-prep.sh /workspaces/wt/CU-C7-start <2026-11-12 pin>` | 0 | `start-prep.log` | `7dffe99e` |
| cargo, engine `:check`, plugin checks, TS | `bdc139736` | `table.sh /workspaces/wt/CU-C7 head` | 0 | `head/cargo.log`, `head/cargo.summary`, `head/engconf.all`, `head/plugconf.all` | `3f8269e5`, `fdd2b5b5`, `27a10bbd`, `cf0bfd16` |
| `replay:check` | `bdc139736` | `table.sh` | 0 | `head/plugconf.replay:check.log` | `6d1b8a37` |
| Evidence tests | `bdc139736` | `table.sh` (`evrun.sh`) | 7 files exit 1; 761 pass, 10 fail | `head/evidence/summary.txt` | `8fca6af5` |
| TS tests without the cache | `bdc139736` | `turbo run test --force --continue --concurrency=6 --filter='./packages/**'` | 0 | `head/ts-force.log` | `b156420b` |
| wasm rows, start | `cf7fdbe7a` | `T=/workspaces/target-start wasm.sh /workspaces/wt/CU-C7-start start-wasm <2026-11-12 pin>` | 0 each | `start-wasm/wasm.all`, `payload.log`, `core-wasm-vitest.log`, `core-wasm-gc.log`, `epdf-web.log`, `addon.log` | `cf61b475`, `78cc37b3`, `37506672`, `a7af5f32`, `7f021e56`, `17b0639b` |
| wasm rows, head | `bdc139736` | `TOUCH=1 wasm.sh /workspaces/wt/CU-C7 head-wasm <2026-11-18 pin>` | 0 each | `head-wasm/wasm.all`, `payload.log`, `core-wasm-vitest.log`, `core-wasm-gc.log`, `epdf-web.log`, `addon.log` | `cf61b475`, `a2311add`, `0843bcae`, `d29f69bb`, `f4cce9ef`, `17b0639b` |
| `rust-coverage:check`, head | `bdc139736` | `pnpm -s run rust-coverage:check` | 1 (stale) | `head/rust-coverage-check.log` | `5a8b35d0` |
| `rust-coverage`, start and head | both | `T=/workspaces/target-start rustcov.sh …` and `rustcov.sh …` | 0, 0 | `start-rustcov/rust-coverage.log`, `head-rustcov/rust-coverage.log`, `head-rustcov/rust-coverage.diff` | `3d2e7d76`, `02d40d10`, `5e8ae473` |
| New Rust tests | `bdc139736` tree | `cargo test` of `session::authority`, `signature_read` and `lists` | 0 | `minors-tests.log` | `e59b2dc2` |
| Rejection test mutated | `394c34207` tree, `image_bytes` resolving | `cargo test -p epdf-plugin-commands --features harness --test lists` | 101 | `minors-mutation.log` | `fc2c0c75` |
| CU-C1 scripts re-run | `9982aeb10` | `runtime-verify.sh`; `lib-exports.sh` over the two linux-arm64 libraries | 0, 0 | `minor1-runtime-verify.log`, `minor1-lib-exports.log` | `c20c6965`, `e5d865c9` |
| Lockfile with the pin | `f6481da43` | `pnpm install --no-frozen-lockfile` | 0 | `minor2-pnpm-install.log` | `14f3c7fc` |
| Follow-up: vectors twice, `coverage.json`, plugin checks, `replay:check` | `c277f21c6` tree | `regen.sh regen1` | 0 each; identical runs | `regen1/regen.all`, `regen1/plugvec-run1.sha` | `7ab324e1`, `70582d10` |
| Follow-up: chained pins | `c277f21c6` tree | CU-C3 `chainpins.py` (root `/workspaces/wt/CU-C7`); `srcpins.py`, `owners.py` report nothing | 0 | `chainpins.log` | `edd3c868` |
| Follow-up: evidence tests | `c277f21c6` tree | `evrun.sh /workspaces/wt/CU-C7 ev-fix2` | 769 pass, 2 fail | `ev-fix2/summary.txt` | `6290cd61` |
| Follow-up: `rust-coverage` write and check | `c277f21c6`, then `5f4abdabc` tree | `CARGO_TARGET_DIR=/workspaces/target-cov pnpm -s run rust-coverage`, then `rust-coverage:check` | 0, 0 | `rustcov-regen/write.log`, `rustcov-regen/check.log` | `29815157`, `c6ef06f1` |
| Follow-up: cargo and plugin checks once more | `5f4abdabc` | `suite.sh … final cargo plugconf`, `replay:check` | 0; 3,514/0/2 | `final/cargo.log`, `final/cargo.summary`, `final/plugconf.all`, `final/plugconf.replay:check.log` | `84c80405`, `fdd2b5b5`, `cf0bfd16`, `33e4cbb8` |

Environment: container `embed-pdf-catchup1118`, linux-arm64, Rust 1.98.0, node 24.20.0, emsdk
4.0.0 at `/workspaces/emsdk4`, `wasm-bindgen` 0.2.127, `wasm-opt` 133, `cargo-llvm-cov` 0.9.1
(installed for this job). Every heavy step ran through `heavy-build.sh` with owner `CU-C7-<label>`.

## Cleanup

Host free space (`df -g /System/Volumes/Data`) read 424 GB at the start and 415 GB at the end;
`/workspaces` went from 33 GB to 35 GB. Deleted, about 28.4 GB inside the volume: the follow-up's
`/workspaces/target-cov` (6.7 GB), the two `llvm-cov-target` dirs (8.9 GB at head, and the start's inside `/workspaces/target-start`, 8.5 GB
with the start's wasm builds), `/workspaces/target/release` (580 MB, the addon builds) and
`/workspaces/target/wasm32-unknown-emscripten` (255 MB, the payload builds), the worktree
`/workspaces/wt/CU-C7-start` (674 MB) and CU-C6's worktree `/workspaces/wt/CU-C6` (2.8 GB, whose
payload this job copied). The CU-C7 bundle is deleted once the host has fetched the branch. No
trim was run; Docker's disk image gives the space back only after one.

Kept for CU-C8 and CU-C9: `/workspaces/wt/CU-C7` (3.0 GB) on `research/catchup-2026-11-18-plugins`,
with its `node_modules`, the PDFium libraries, and the head's Rust-core payload in
`packages/engine/runtime/npm/wasm32/lib` (CU-C6's payload without the core is gone from it);
`/workspaces/target/debug` and `/workspaces/target/wasm32-unknown-unknown` (the browser build);
`cargo-llvm-cov` 0.9.1 in `/home/node/.cargo/bin` (4.3 MB) and the `llvm-tools` component. The
worktree's `target` is a symlink to `/workspaces/target`.
