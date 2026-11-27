# CU-C7 decisions

Decisions made while integrating the port jobs onto `research/catchup-2026-11-27-plugins` and
running the Rust and Node suites against the `d250a68e3` baseline (Job F).

## CU-C7-D1. A fast-forward to CU-C6's tip, not a merge of three branches

Decision: `research/catchup-2026-11-27-plugins` moved `0e83ecb84..643d2b429` by fast-forward, on
the host (`git merge --ff-only`) and in the container clone (`git fetch .
research/catchup-2026-11-27-c6:research/catchup-2026-11-27-plugins`).
Reason: C4, C5 and C6 ran on one chain (controller ruling), so `-c6` already contains `-c4` and
`-c5`, and `0e83ecb84` is an ancestor of `643d2b429`. There was nothing to merge and no
`coverage.json` conflict to regenerate.
Cost: none. The branch is a catch-up branch, not a canonical one.
Reverse: `git branch -f research/catchup-2026-11-27-plugins 0e83ecb84` on both sides.

## CU-C7-D2. A worktree of its own, CU-C6's target dir, and a target dir for the baseline

Decision: the head ran in a new container worktree `/workspaces/wt/CU-C7` on the branch, prepped
by `scripts/cu-c7/prep.sh` (CU-C6's PDFium libraries and prep payload copied, `pnpm install`, the
package builds, the linux-arm64 addon). Cargo builds used `/workspaces/target-c5` with `TOUCH=1`
(every `crates/**` source touched first). The baseline's extra rows ran in Job A's
`/workspaces/wt/base` (`d250a68e3`) with a new `/workspaces/target-base`.
Reason: `target-c5` holds CU-C6's builds of almost the same tree, so only the workspace crates
rebuild. Two checkouts of one workspace in one target dir can take each other's builds as fresh
(2026-11-18 CU-C7-D3), so the head's sources are touched and the baseline gets its own dir.
Cost: `/workspaces/target-base` adds space on the volume until CU-C11.
Reverse: `rm -rf /workspaces/target-base`; rerun with a fresh `T`.

## CU-C7-D3. Baseline rows Job A did not run are measured here; the rest come from CU-C2 and CU-C3

Decision: at `d250a68e3` this job ran `cargo clippy --workspace --all-targets`, `replay:check`,
both payload builds, `verify-rust-core.mjs`, the three `core-wasm` checks, the `epdf-web` wasm
tests and the `epdf-node` addon test. cargo, engine `:check`, the four plugin checks and the TS
tests come from `reports/CU-C2.md` (Job A's fresh baseline run), and the evidence count (769 of
771) from `reports/CU-C3.md`, measured at `d250a68e3`. The baseline's `wasm32-eh` payload used
the archive its runtime pin names, `af480d0ce` (`scripts/cu-c7/base-pin.json`), not the
`9472117ed` one in `/workspaces/wasm32-eh-pin.json`.
Reason: the brief compares every row against `d250a68e3`, and Job A's table did not have these.
Cost: about an hour of container time.
Reverse: not applicable.

## CU-C7-D4. The baseline's plain `wasm32` row is its second run

Decision: the baseline's first `build-target.sh wasm32` (`base-wasm/payload-wasm32.log`) failed to
compile `pdfium` (`EPDFDoc_GetStampDrawings` not found) and is not used. The row is the rerun
after the `wasm32-eh` step (`base-wasm/payload-wasm32-rerun.log`), which fails to link with
`undefined symbol: __cpp_exception`, as the head does.
Reason: `pdfium-sys` for `wasm32-unknown-emscripten` reads the `wasm32-eh` headers, and Job A's
prep had left the `9472117ed` archive (renamed drawing calls) in the baseline checkout. The
`wasm32-eh` step put `af480d0ce` back, so the rerun builds the baseline's own bindings.
Cost: none; the first run is kept as a log.
Reverse: not applicable.

## CU-C7-D5. The TS row is a `turbo --force` run

Decision: the head's TS row is `turbo run test --force` (`head/ts-force.log`). The run inside
`suite.sh` (`head/ts.log`) replayed 95 of 98 test tasks from turbo's cache and is not used.
Reason: the cache is shared across the container's worktrees, so a replay could be Job A's
`f8a124edc` run rather than this tree's. The forced run executes every task, which is what checks
the lockfile move CU-C3-D11 did not rerun.
Cost: one more full TS run. The baseline numbers are Job A's runs, which turbo cached from
executions on the same day; they are not forced.
Reverse: not applicable.

## CU-C7-D6. Job E's Minors and the clippy fix are edited on the host and verified in the container

Decision: the four Minors (`84a64f0d8`) and the unused-import fix (`2c49e0685`) were edited in the
host worktree, copied into `/workspaces/wt/CU-C7` with `tar -m`, checked there (`cargo fmt
--check`, `cargo clippy`, tests), then committed on the host with the container identity and
brought back by bundle. `lent_record` uses a `OnceLock` empty object, the pattern
`sync/records.rs` `by_key` already uses.
Reason: the host worktree is the branch's; only text was edited there, and nothing Node or Rust
ran on the host.
Cost: none.
Reverse: `git revert 84a64f0d8 2c49e0685`.

## CU-C7-D7. The clippy warning in `form/src/ffi.rs` is the port's and is fixed

Decision: `cargo clippy --workspace --all-targets` at head warned `unused import: std::rc::Rc` at
`crates/plugins/form/src/ffi.rs:18`; `2c49e0685` removes it. The head's wasm rows ran with that
one-line change already copied into the worktree (it reached the worktree before they finished).
Reason: CU-C5's `46be230a5` moved the only `Rc::new` there to `FocusTarget::new`. The file
compiles only with the `uniffi` feature, which CU-C5's and CU-C6's per-crate clippy runs did not
enable. Neither the payload, `epdf-web` nor `epdf-node` builds that feature, so the wasm rows
are unaffected.
Cost: none.
Reverse: `git revert 2c49e0685`.

## CU-C7-D8. `rust-coverage:check` is not run

Decision: as CU-C3-D12: not run. The brief's list does not include it.
Reason: it needs an instrumented `cargo llvm-cov` build of the whole replay, and no job in this
catch-up has measured it at `d250a68e3` to compare against.
Cost: `vectors/rust-coverage.json` may be stale against the port; the next job that runs it will
see it.
Reverse: `pnpm -s run rust-coverage:check` in `tooling/plugin-conformance`.
