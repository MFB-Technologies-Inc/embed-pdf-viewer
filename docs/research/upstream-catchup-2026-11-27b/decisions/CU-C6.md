# CU-C6 decisions

Decisions made while merging plugins' tip, closing CU-C5's review Minors, re-pinning the evidence
archives and running every Rust and Node row against the `0afd87825` start.

## CU-C6-D1. Plugins' tip `e3d6e7fbc` is merged, not fast-forwarded

Decision: `5a6d266dc` merges `fork/research/multi-platform-rust-core-impl-plugins` as fetched at the
start of the job, `e3d6e7fbc`, into `research/catchup-2026-11-27b-plugins` with `--no-ff`. The
plugins branch itself is not touched.
Reason: D-4 asks for plugins' post-release tip. It is one commit past the start
(`test(dotnet): wait on deterministic conditions in two flaky model tests`), two files under
`platforms/dotnet`, so the merge had no conflict and cannot move a Rust or Node row. Release train
run `46080379002` was still `in_progress` when the merge was made (`updatedAt`
`2026-11-27T23:32:49Z`), so an Apple engine pin commit may still land on plugins; the controller
merges it before the gate.
Cost: one merge commit in the catch-up history.
Reverse: `git reset --hard 7b18a5eb6` before anything else lands, or `git revert -m 1 5a6d266dc`.

## CU-C6-D2. The head runs in CU-C2's worktree; each side has its own target dirs

Decision: the head rows ran in `/workspaces/wt/CU-C2` (on the branch) with
`CARGO_TARGET_DIR=/workspaces/target`, which CU-C2 to CU-C5 had used for this checkout alone. The
start's rows ran in `/workspaces/wt/base` (`0afd87825`) with `/workspaces/target-base`. Coverage
used `/workspaces/target-cov` (head) and `/workspaces/target-cov-base` (start). Both sides used
`/workspaces/wasm32-eh-pin.json` (`9472117ed`), since both pin `runtime-src` at `9472117ed`.
Reason: CU-C2's worktree is already prepped (PDFium `7f399f838`, package builds, the addon,
FreeType), and no `packages/**` file changed since, so a fresh worktree buys nothing. Separate
target dirs keep one checkout from taking the other's builds as fresh (2026-11-18 CU-C7-D3), so
`TOUCH` was not needed.
Cost: three extra target dirs (18.4 GB) for the length of the job; all three were deleted
afterwards.
Reverse: rerun with fresh `T` values.

## CU-C6-D3. Start rows CU-C2 and CU-C3 measured are reused; the rest are measured here

Decision: at `0afd87825` this job ran `cargo clippy --workspace --all-targets`, `replay:check`, both
payload builds, `verify-rust-core.mjs`, the three `core-wasm` checks, the `epdf-web` wasm tests, the
`epdf-node` addon test and `rust-coverage:check`. cargo test, engine `:check`, the four plugin
checks and the TS tests are CU-C2's fresh baseline run (`reports/CU-C2.md`); the evidence count is
CU-C3's run at `0afd87825` (`/workspaces/logs/CU-C3/ev-base/summary.txt`, `6290cd61`).
Reason: the brief compares every row with `0afd87825`, and those two jobs already measured these
rows in this container on the same image.
Cost: the TS baseline is CU-C2's cached-or-not turbo run, not a `--force` run (2026-11-27
CU-C7-D5 accepted the same).
Reverse: not applicable.

## CU-C6-D4. The evidence archives are re-pinned by the 2026-11-27 sequence, with no witness changed

Decision: `f69b5f3c5` re-pins 18 files under `tooling/plugin-conformance/historical/`, in this
order: `srcpins.py --write` (rows whose commit was the path's last at `0afd87825`;
`packages/engine/core/src/auth/scope/types.ts` to `445e34a69`), `owners.py --write` (range
`9af4d9ee7..cda9e82a4`; `packages/engine/core/src/mutation/Change.ts` to `984348e18`), the ten
C-series captures (`actions` through `view-manager`), `restamp.mts` on
`historical/task-12b2b3-v` then Prettier, `chainpins.py`, one `sourceHashes` entry by hand,
`repin-loop.sh` on `annotation-live-ii`, `form-current`, `form-live` and `form-script`, then
`chainpins.py` twice more (no swaps). No test file changed.
Reason: CU-C3-D4 left the re-pin here. Every input that changed differs only by rows the engine
records: `forAnyone: false` on field rows (62 lines in the re-stamped stamp-placement inputs, 78 in
`task-12b2c4/current-inputs-runtime.json`, 6 in `task-12b2c2`) and `layout: null` on widget rows
(20 and 24). Every digest `repin-loop.sh` moved is a canonical input digest
(`form-current.test.ts:592`, `form-live.test.ts:816`, `form-script.test.ts:1056`,
`annotation-live-ii.test.ts:890`, all under `tooling/plugin-conformance/test/`), never an
outcome. `srcpins.py` reads `{path, sha256, commit}` rows only, so
`task-12b2c4/source-evidence.json`'s `sourceHashes` map kept `types.ts`'s old digest; that one value
was replaced with `sed` and logged in `repin/sourcehashes.log`.
Cost: the archives move another step from the bytes they were cut from. The hand edit is one
digest outside the scripts.
Reverse: `git revert f69b5f3c5`.

## CU-C6-D5. The shared drawing's PDF refusal takes upstream `drawingFor`'s text

Decision: `drawing_for_format` (`crates/epdf-engine/src/features/annotation/write/stamp.rs:166`)
refuses a PDF it cannot open with "the appearance PDF could not be opened"
(`crates/epdf-engine/src/features/annotation/write/stamp.rs:180`), the text upstream's
`drawingFor` raises through `withAppearanceDocument`
(`packages/engine/services/src/features/annotations/internal/appearanceDocument.ts:32`). It said
"the stamp's appearance PDF could not be opened". A stamp's own preflight keeps that text, as
`requireStampContent` does (`writeStampAnnotation.ts:198` in the same TS feature).
Reason: the CU-C5 review asked to reword only if the TS message for that path differs. It does,
and since `7d5691a5a` a signature mark and a push button's image reach the same function.
Cost: none. No vector and no Rust test reads either text on this path.
Reverse: `git revert 0b55dab9f` (it also carries the other two Minors).

## CU-C6-D6. `vectors/rust-coverage.json` is regenerated

Decision: `rust-coverage:check` (`scripts/cu-c6/rustcov.sh`, `cargo-llvm-cov` 0.9.2) passes at
`0afd87825`: the record 2026-11-27 CU-C7 wrote is current there, 67,204 of 116,903 lines from replay,
37,516 only other tests reach, 706 files, 76 excluded. A write at the start left the file unchanged.
At `f69b5f3c5` the check exits 1, stale, because CU-C4 and CU-C5 added engine code. `22f7abd8c`
rewrites the record in write mode, and a second check exits 0 with the same numbers:
67,236 of 117,613 lines, 38,193 only other tests reach, 708 files, 76 excluded.
Reason: the record has to match the tree for `rust-coverage:check` to pass, as 2026-11-27
CU-C7-D8 did. Both sides were measured in this container on the same toolchain.
Replay reaches 32 more lines than at the start, but the share falls from 57.49% to 57.17%: the
710 new lines are CU-C4's and CU-C5's engine code, which the engine vectors and the Rust tests
reach and the plugin replay does not.
Cost: the record's diff (2,075 insertions, 1,945 deletions) is reviewed as a whole. The second check is the
determinism evidence; no second write was compared byte for byte.
Reverse: `git revert 22f7abd8c`.

## CU-C6-D7. The TS row at head is a `turbo --force` run

Decision: the head's `packages/**` row is `turbo run test --force` (`table.sh … tsforce`,
`head/ts-force.log`): 98 tasks, 0 cached.
Reason: turbo's cache is shared by the container's worktrees (2026-11-27 CU-C7-D5).
Cost: one full TS run.
Reverse: not applicable.

## CU-C6-D8. `cargo fmt --all --check`'s two dirty files are left

Decision: `cargo fmt --all --check` reports diffs in `crates/epdf-node/src/lib.rs` (2 hunks) and
`crates/pdfium/tests/documents.rs` (3 hunks), at `0afd87825` and at the head alike (rustfmt 1.11.0).
This job formats only the files it touched.
Reason: the brief says to revert formatting of lines a job did not touch, and neither file is this
catch-up's.
Cost: a workspace-wide `cargo fmt --check` stays red until someone formats those two files.
Reverse: run `cargo fmt -p epdf-node -p pdfium` in a commit of its own.
