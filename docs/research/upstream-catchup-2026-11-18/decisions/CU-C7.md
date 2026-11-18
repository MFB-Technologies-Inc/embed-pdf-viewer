# CU-C7 decisions

## CU-C7-D1. Fast-forward, no merge

Decision: `research/catchup-2026-11-18-plugins` is fast-forwarded `9041d7510..0e1586fc0` in the
container clone and on the host (`git fetch . research/catchup-2026-11-18-c6:research/catchup-2026-11-18-plugins`,
which refuses anything but a fast-forward). The job's commits sit on top.
Reason: the port jobs ran one after another on one chain (CU-C3, c4a, c4b, c5, c6), so the plan's
"merge c4, c5 and c6" has nothing to merge, and `coverage.json` has no conflict to regenerate.
Cost: none.
Reverse: `git branch -f research/catchup-2026-11-18-plugins 9041d7510`.

## CU-C7-D2. Where the start column comes from

Decision: the start (`cf7fdbe7a`) numbers for cargo, engine `:check`, the plugin checks,
`replay:check`, evidence and TS tests are CU-C3's baseline run (`/workspaces/logs/CU-C3/base/`,
`ev-base/`), in this container on 2026-11-18 with the same suite script. The rows no earlier job
ran (Rust-core payload, `core-wasm` vitest, `test:gc` and `typecheck`, `epdf-web`, the addon) are
measured here on a fresh worktree `/workspaces/wt/CU-C7-start` detached at `cf7fdbe7a`, with that
commit's own PDFium: wasm32-eh `17d8073d4` (`upstream-catchup-2026-11-12/scripts/wasm32-eh-pin.json`)
and linux-arm64 `aa59ce554` (its `engine-runtime-build.json`).
Reason: the brief allows citing the 2026-11-12 table only if nothing relevant changed, and
`cf7fdbe7a` is 2026-11-12's head plus later plugin work, so measuring is the shorter argument.
Cost: a second payload, built and then deleted.
Reverse: not needed.

## CU-C7-D3. The start checkout builds in its own target dir; the head's wasm rows touch its sources

Decision: `scripts/cu-c7/wasm.sh` and `rustcov.sh` take the target dir from `T` (default
`/workspaces/target`), link `<checkout>/target` to it, and name `PDF_RUNTIME_LIB_DIR` for the
checkout. The start checkout ran with `T=/workspaces/target-start`, deleted afterwards. The head's
wasm rows ran with `TOUCH=1`, which touches every `*.rs`, `Cargo.toml` and `*.h` under `crates/`
first.
Reason: `build-target.sh`, `crates/epdf-web/scripts/build.sh` and `addon.mjs` read
`<repo>/target`, and the disk rule asks for one shared target dir. But Cargo fingerprints a
workspace crate by its path relative to the workspace and by file mtimes, so in a shared target
dir a build from the other checkout that is newer than this checkout's files counts as fresh. It
showed three times. The first head payload (`head-wasm-run1/`) failed with `cannot find function
EPDFDss_GetCount in crate pdfium_sys`: `pdfium-sys` kept the bindings it had generated from the
start checkout's `17d8073d4` headers. The second (`head-wasm-run2/`, `PDF_RUNTIME_LIB_DIR` named)
built, but the plugin crates `epdf-web` and `core-wasm` use could still be the start's. And
`rust-coverage` on the start (`start-rustcov-shared-target/`) failed in `epdf-engine`'s
`wire_contract`, `change_ops` and `signature_protection` tests, the head's engine (96 kinds)
reading the start's vectors (94). The `cargo test` table at head is not affected: it ran from
13:58:35, before any start build. Neither is the head's `rust-coverage`, which built a fresh
`llvm-cov-target` before the start's run. None of the discarded runs' numbers are used.
Cost: a second target dir while the start runs (deleted), and a rebuild of every workspace crate
for the head's wasm rows.
Reverse: not needed.

## CU-C7-D4. Playwright pinned, `next`'s peer left at 1.65.1

Decision: `@playwright/test` is `1.63.0` (exact) in `tooling/viewer-e2e` and
`tooling/wasm-eh-check`, with `pnpm install --no-frozen-lockfile`. The lockfile keeps
`@playwright/test@1.65.1` as the optional peer of `next@15.5.12` (docs and examples).
Reason: CU-C2-D3's re-resolve moved both tooling packages to 1.65.1; `cf7fdbe7a` had 1.63.0, whose
browsers (`chromium-1243`) are the ones in the container cache. `pnpm dedupe` removes 1.65.1 but
rewrites 730 lines of the lockfile (vite 7 to 6 for `@vitest/browser-playwright`, among others), a
bigger change than the one asked for.
`npx tsc --noEmit -p tooling/viewer-e2e` gives the same 4 errors (in `examples/viewer-react`) under
1.63.0 and 1.65.1.
Cost: two Playwright versions stay installed.
Reverse: return both specifiers to `^1.55.0` and re-run `pnpm install`.

## CU-C7-D5. The refusing `image_bytes` test is in the commands crate

Decision: `a_picture_that_cannot_be_encoded_fails_snapshot_save_before_anything_is_saved` is in
`crates/plugins/commands/tests/lists.rs`, not in `crates/plugins/render/src/commands.rs`. It
replays the `snapshot-commands` case of `plugins/commands/lists.json` with a render platform whose
`image_bytes` rejects and asserts that no `saveFile` callback is logged and that `snapshot:save`'s
promise rejects with the platform's message.
Reason: `snapshot:save` needs a kernel with render, interaction and the commands plugin holding a
snapshot; `lists.rs` already builds that with the conformance harness, which the render crate
can't reach without a dev-dependency on the commands harness. With `image_bytes` resolving instead,
the test fails on the logged `saveFile` (`minors-mutation.log`).
Cost: a render behaviour is tested in another crate.
Reverse: move it if the render crate gains the harness.

## CU-C7-D6. `validation-info.ts`'s unreached paths are recorded, not filled

Decision: no case is added. The three paths CU-C6's report missed are named in it: the
`!uncovered` early return at line 90 (branch `90:20#0`), the success return at line 95 (branch
`95:4#0`) and the `added.certificates + added.ocspResponses + added.crls > 0` side of `uncovered`.
Reason: the brief asked for a case only if the `LONG_TERM` fixtures made it cheap. They don't. A
document timestamp that succeeds needs an RFC 3161 prepare answer, a token over the fake prepare's
digest from a trusted authority, and a completion answer; the line-90 return needs a document
timestamp already covering the signature in the plugin suite's fake documents. Each is new
fixture work, plus a Rust replay that may expose a difference this job isn't meant to fix.
Cost: the document timestamp's success path in `addValidationInfo` has no vector.
Reverse: add the case in `suites/signature-plugin.ts` with a `tsaKey`-style authority.

## CU-C7-D7. `signatures.addValidationInfo` goes last in `TRANSACTED_KINDS`

Decision: the kind moves from between `metadata.update` and `metadata.updateCustom` to the end of
the list, after `pieceInfo.delete`.
Reason: no other signature kind is in that list (`signatures.complete` replaces the bytes instead),
and the host's other kind lists (`crates/epdf-engine/src/host.rs` lines 164 to 174 and 278 to 314)
put the signature kinds after redaction, measure and pieceInfo.
Cost: none; the list is a membership test.
Reverse: move it back.

## CU-C7-D8. `safe.directory '*'` is recorded and left

Decision: the container's global `safe.directory = *` (`/home/node/.gitconfig`, modified
2026-11-18 08:48:53 UTC, during CU-C3's baseline prep) stays.
Reason: CU-C3 set it and left no record of the cause. Today no repository in the container needs
it: `git -c safe.directory= rev-parse` succeeds in `/workspaces/embed-pdf-viewer`,
`/workspaces/runtime`, `/workspaces/emsdk4`, `/workspaces/wt/CU-C7` and `/workspaces/wt/CU-C6`,
all owned by `node` (uid 1000). The likeliest cause is a git call on something `docker cp` left
owned by uid 501 (two such files remain at `/workspaces/lib-exports.sh` and
`/workspaces/logs/runtime-verify.sh`). The setting is local to a one-user container.
Cost: git in that container won't warn about a repository another user owns.
Reverse: `git config --global --unset-all safe.directory`.

## CU-C7-D9. `vectors/rust-coverage.json` is regenerated (controller follow-up)

Decision: `cargo-llvm-cov` 0.9.1 and the `llvm-tools` component are installed in the container.
`rust-coverage` was measured on both sides first (`scripts/cu-c7/rustcov.sh`, write mode, file
restored). At `cf7fdbe7a` the write reproduces the committed record exactly: replay reaches 64,882
of 113,799 lines, 37,520 lines only other tests reach, 675 files, 75 excluded. At `bdc139736`
`rust-coverage:check` exited 1 (`vectors/rust-coverage.json is stale`). After the controller asked
for the row to be closed, `pnpm --filter @embedpdf/tooling-plugin-conformance run rust-coverage`
rewrote the record on top of the evidence commits, in its own target dir
(`CARGO_TARGET_DIR=/workspaces/target-cov`, deleted afterwards), and `rust-coverage:check` then
exits 0 (`rustcov-regen/`). The record is its own commit.
Reason: the record lagged the whole port. CU-C3-D9 left the check out and no job since ran it.
Cost: the record's diff (1,291 insertions, 994 deletions; replay now reaches 68,345 of 118,359
lines, 38,382 only other tests reach, 700 files, 76 excluded) is reviewed as a whole rather than per job.
Reverse: revert that commit.

## CU-C7-D10. CU-C1's logs are in the container too

Decision: the five logs CU-C1's report lists but the container lacked (`container-state.log`,
`CU-C1-export-callers.log`, `CU-C1-pdfium-sys-linux-arm64.log`, `CU-C1-pdfium-sys-wasm32-eh.log`,
`wasm32-eh-run.out`) and the `SHA256SUMS` manifest are copied from `/tmp/cu1118/logs/CU-C1/` on the
host to `/workspaces/logs/CU-C1/`; `sha256sum -c SHA256SUMS` passes there.
Reason: that makes the report's sentence true rather than editing it.
Cost: none.
Reverse: not needed.

## CU-C7-D11. The evidence failures are closed by a fixture fix and a generated additions archive (controller follow-up)

Decision: the eight evidence failures new since `cf7fdbe7a` are closed in two commits. First, the
`ink-press-on-an-annotation-keeps-the-waiting-stroke` answers in
`src/suites/annotation-live-answers.ts` give their three `apply` writes `{ ms: 200 }`, and the
plugin vectors are regenerated (twice, identical; only `plugins/annotation-tools.json` changes).
Second, `scripts/cu-c7/additions.mjs` writes `historical/catchup-2026-11-18/additions.json`, the
83 cases the catch-up added to the vectors between `cf7fdbe7a` and `2615a3bec` (file, name,
covers), read from git; `test/catchup-additions.ts` reads it, and the six evidence tests count a
file's additions on top of their archives, as their 2026-11-12 lists do. CU-C3's `chainpins.py`
then moved the one chained pin that names a changed test file
(`historical/task-12b2b1b-ii/family.json`, `test/annotation-live.test.ts`); `srcpins.py` and
`owners.py` find nothing to re-pin.
Reason: seven failures were counts, which no CU-C3 script re-pins: those scripts move hashes of
changed source, while these tests count cases against archives. The archive keeps the counts out of
hand-typed lists. The eighth was the opId check, and it is a stale fixture, not a TS and Rust
difference. In the recorded TS trace all three `apply` calls carry `00000000-00c8-…` (200 ms, after
the case's `advance(200)`). `replay:check` passes, so the Rust replay sends the same. Only the
scripted answers said `00000000-0000-…`, because CU-C6 wrote them without the `ms` option the other
live answers pass. `opids.mjs` finds no other case in any vector whose call and answer opIds
differ.
The two failures left are the start's, in `current-suites.test.ts`. `grid-nan-columns`
(`f629719b5`, 2026-11-16, before `cf7fdbe7a`) observes the kept M27 NaN divergence: TS's kernel
reports `RangeError: Invalid array length`, and the test allows a report only from `change-hooks`.
The second (`Cannot read properties of undefined (reading '6')`) is that test aborting before it
records `smooth-navigation-tween`, which the next test reads.
Cost: the evidence tests depend on one more archive file, regenerated if the catch-up adds cases.
Reverse: revert the two commits.
