# CU-C3 decisions

Decisions made while making the conformance tooling build against upstream `9af4d9ee7`,
regenerating the vectors and writing the work order (Job B), on
`research/catchup-2026-11-27-plugins` from `a137ca2e6`.

## CU-C3-D1. Work in the branch's own container worktree

Decision: the job ran in `/workspaces/wt/CU-C2`, the container worktree that already has
`research/catchup-2026-11-27-plugins` checked out and was prepped by Job A, rather than a new
`/workspaces/wt/CU-C3`. Logs are in `/workspaces/logs/CU-C3/`, scripts in
`/workspaces/scripts/cu-c3/` (committed as `scripts/cu-c3/`).
Reason: git refuses a second worktree on a checked-out branch, and the prep (dependencies, PDFium
of the `9472117ed` pin, a wasm32 payload built from it, the linux-arm64 addon, package builds) is
the one this job needs.
Cost: the worktree's name says CU-C2. The scripts take the checkout as an argument, so only the
hard-coded root in `gaps.py`, `owners.py`, `srcpins.py` and `chainpins.py` names it.
Reverse: add a `/workspaces/wt/CU-C3` worktree on a new branch from the tip and rerun the scripts
there.

## CU-C3-D2. The blank-page function cases go, as upstream's `blank-page.ts` did

Decision: `tooling/plugin-conformance/src/suites/signature-plugin.ts` no longer imports
`packages/plugin/signature/src/blank-page.ts` and its two function cases (`blank-page`,
`blank-page-cached`) are removed from `plugins/signature-functions.json`.
Reason: upstream deleted the file in `fcb69611f` (#1145). A clear now calls
`forms.setSignatureAppearance(field, null)` and the engine takes the mark out, so nothing in
upstream builds a blank page any more. `c74ec3e9d` did the same for `clearLoadError` and
`pageScaleOf` last time.
Cost: `epdf-plugin-signature`'s `blank_page_pdf` loses its oracle; CU-C5 removes it with the rest
of the #1145 plugin half.
Reverse: restore the import and the two cases from `a137ca2e6`.

## CU-C3-D3. Form-core cases follow upstream's new signatures

Decision: in `tooling/plugin-conformance/src/suites/form-core.ts` and `form-current-fixtures.ts`:

- every fixture field has `purpose: null`, and text and combo box fields `format: null`
  (`833b40015`, #1147);
- `picture` is a full `PageWidget` (`ref`, `box`, `rotation: 0`, `rect`), and every case that moved
  its box uses `pictureAt(box)`, which sets `box` and `rect` together (`23741e3fd`, #1118);
- `projectWidget` gets `canFill` equal to the case's `fillable` argument, and `fillItemsAllowed`
  passes both predicates as allowing (`d6d7c25e2`, #1141);
- `predictFormOp` and `undoLookOf` lose the prediction context (`fce760aeb`, #1140). The five
  session-group cases become four: `update/create-without-a-group`, `update/create-names-its-group`,
  `update/copy-through-create` and `update/undo-look-of-a-create`.

Reason: these were the 47 non-import typecheck errors. Each follows the upstream signature it
broke on; none changes what upstream code does.
Cost: `update/create-in-the-session-group`, `update/create-without-a-session`,
`update/copy-in-the-session-group` and `update/undo-look-in-the-session-group` leave the vectors,
with the behaviour upstream removed. The new arguments (`inGroup`, `group`, a turned widget) are
not exercised by these function cases; the coverage gaps list them for CU-C5.
Reverse: revert the generator commit.

## CU-C3-D4. A fixture widget's box is its rect

Decision: `widget()` in `tooling/plugin-conformance/src/suites/annotation-current-fixtures.ts`
takes its `box` from the override's `box`, else its `rect`, else `BOX`.
Reason: after `23741e3fd` (#1118) the annotation core reads a widget as a turned box, with `box` its
frame and `rect` where it stands; "an unturned widget's frame is its rect". The helper gave every
widget `box: BOX` (100, 100, 100, 100) whatever its `rect`, a row no engine reports. Under upstream
that row hit-tests at `BOX`, so `plugins/signature.json` `an-armed-stamp-is-not-a-mark` placed a
stamp beside a signature widget instead of over it and stopped with two unanswered `apply` calls.
With the box at the rect the case does what its `covers` says.
Cost: every vector that builds a widget with a `rect` and no `box` now has a different `box` in its
input. That is input the Rust port replays as given, so a replay only moves where the Rust side
reads `box`.
Reverse: restore `box: BOX` and answer the two `apply` calls in that case.

## CU-C3-D5. The regenerated `prelude.js` is upstream's output and is committed

Decision: `crates/plugins/acrojs/prelude.js` is committed as the generator writes it (32 lines
added, 3 removed), the same bytes Job A saw.
Reason: the change is `packages/core/acrojs/src/prelude.ts` from `833b40015` (#1147):
`parseDateLoose` becomes `parseDateFrom` with `today` passed in, and the host gets a fixed
`__acrojsParseDate` for the date keystroke check. `parseDateLoose` stays as a wrapper over
`parseDateFrom(..., nowWall())`. The generator copies `PRELUDE_SOURCE` verbatim,
so this is not tool drift.
Cost: `epdf-acrojs` embeds the new prelude, and `core/acrojs.json` `prelude-source` changes
with it; any Rust test that pins the prelude's bytes moves too.
Reverse: `git checkout a137ca2e6 -- crates/plugins/acrojs/prelude.js`, which then fails
`vectors:check`.

## CU-C3-D6. `c8` 12 in the plugin conformance tooling

Decision: `tooling/plugin-conformance/package.json` takes `c8` `^12.0.0` (was `^10.1.3`), and
`pnpm-lock.yaml` is re-resolved by `pnpm --filter @embedpdf/tooling-plugin-conformance add -D
c8@^12.0.0`.
Reason: `c8` 10 and 11 depend on `yargs` 17.7.2, whose `yargs/yargs` entry is an extensionless
CommonJS file in a `"type": "module"` package. Node 26.13.1 loads it as ESM and fails with
"require is not defined in ES module scope". `c8` 12.0.0 (2026-07-14) uses `yargs` 18 and declares
`node ^20.19.0 || ^22.12.0 || >=23`. The repository runs node 26 (`.devcontainer/Dockerfile`,
`mise.toml`, `release-train.yml`), so the fix belongs in the port's own tooling, not in an older
node. Upstream has no `c8`.
Cost: the lockfile also moves `test-exclude` 7.0.2 to 8.0.0 and `glob` to 13.0.6 under it, and
pnpm's re-resolution moves four transitive snapshots (`@parcel/node-resolver-core`,
`editorconfig`, `is-bun-module`, and `jsonwebtoken`'s optional dependency) from `semver` 7.7.4 to
7.8.6, a version already in the lockfile at `a137ca2e6`. It also swaps the peer suffixes of two
`nextra@4.6.1` entries, which drops a dangling `next@15.5.12(@opentelemetry...)` key that Job A's
merge had left.
Reverse: revert the two files; `coverage:check` then needs node 22.

## CU-C3-D7. The wire vector's flipped union order is kept

Decision: `wire/requests.json` is committed with `document.checkPasswordPermissions`'s `mode`
printed as `"any" | "owner"`, where `d250a68e3` printed `"owner" | "any"`. The generator is not
changed to sort union members.
Reason: both sources (`packages/engine/core/src/wire/worker-protocol.ts:1368` and its
`c4e744345` version) say `'any' | 'owner'`. The order comes from `checker.typeToString` in
`tooling/engine-conformance/src/worker-kinds.ts`, which prints literals in the order the program
first created them, and the merged program creates them in another order. It is stable between
runs (both regenerations identical), and no Rust test reads a field's type text (`wire_contract`
passes).
Cost: the next upstream change to the program can flip it back, a one-line diff with no meaning.
Sorting the members would end that but rewrite every union in the vector now.
Reverse: sort union members in `worker-kinds.ts` and regenerate `wire`.

## CU-C3-D8. Re-baseline the evidence archives by re-pin, re-capture and re-stamp

Decision: `6960bfba7` re-pins source rows to their upstream owners (`srcpins.py`, `owners.py`,
limited to paths upstream changed in `c4e744345..9af4d9ee7`), reruns the ten C-series captures in
order (`test/{actions,actions-lifecycle,actions-script,commands,link,metadata,page-edit,i18n,shell,view-manager}-evidence.ts`),
re-stamps `historical/task-12b2b3-v/current-inputs.json` (`restamp.mts`, then Prettier, as the
archive was), replaces the canonical input digests a test names (`repin-loop.sh`), moves the
chained integrity and aggregate pins (`chainpins.py`, which now keeps its last pin of each file in
a state file so a second run moves a pin the first already moved), and changes four witnesses to
upstream's behaviour: zod 4 refuses `Infinity` (`annotation-current-pure.test.ts`, three counts),
the form session holds `designGroup` and `groups` (`form-current.test.ts`), and a placed widget is
sent as its `box` (`form-live.test.ts`, two reads). The cases this catch-up removed or renamed are
recorded in `historical/catchup-2026-11-27/additions.json` and folded into
`test/catchup-additions.ts`, so `caughtUp20261118` returns the 2026-11-18 additions less the removed
ones, then the new ones.
Reason: the brief asks for a re-baseline; this is CU-C3-D5 and CU-C7-D5 of the earlier catch-ups
applied to a larger change. Every failing assertion was an input digest, a source hash or one of
the four witnesses; none was a changed outcome of a case whose input stayed the same.
Cost: the archives move another step from the bytes they were cut from, and the four witnesses
now assert current behaviour. The 2026-11-18 additions record stays as it was, so reading the
helper takes two files.
Reverse: revert `6960bfba7`.

## CU-C3-D9. Route the work order by crate, which moves three PR halves

Decision: CU-C4 owns `pdfium-sys`, `pdfium`, `epdf-engine`, `epdf-viewer-host` and `epdf-fonts`;
CU-C5 owns `epdf-form-core`, `epdf-plugin-form`, `epdf-plugin-signature`, `epdf-signature-core`
and `epdf-acrojs`; CU-C6 owns `epdf-annotation-core`, `epdf-plugin-annotation`, `epdf-plugin-i18n`
and the payload build. A PR that spans them is split. Against the plan's list this moves the engine
halves of #1147 and #1140 from CU-C5 to CU-C4, #1118's engine half to CU-C4 and its `plugin/form`
half to CU-C5, #1141's annotation half to CU-C6, and the `parseAnnotationDraft` replays of #1151 to
CU-C6.
Reason: the three jobs run in parallel on `-c4`, `-c5` and `-c6` from this tip and merge in CU-C7.
Two jobs editing one crate is where those merges would conflict, and the evidence puts each half
in a different crate: the 6 `epdf-engine` failures are all #1147 and #1145, the `plugin/form`
replays mix #1118, #1140, #1141 and #1147, and the only Rust-visible effect of zod 4 is in
`epdf-plugin-annotation`. Upstream touched 16 engine files for #1140, 25 for #1147 and 8 for #1118
(`git diff --name-only` per merge).
Cost: CU-C4 grows by most of #1147, the largest engine change in the range, and CU-C5's plugin
work on purpose, format and groups waits on it to replay green. `RecordAccent` is needed by CU-C5
and owned by CU-C6; whichever lands second adapts.
Reverse: move the items back in the work order; nothing is built yet.

## CU-C3-D10. List coverage gaps, add no cases

Decision: no generator cases for upstream code no vector reaches; `results/CU-C3-coverage-gaps.md`
lists the 277 lines with the job that adds them.
Reason: the brief, and 2026-11-18 CU-C3-D6. Writing cases for code the port hasn't got is the port
jobs' work, and their cases are what they test against.
Cost: `coverage:check` passes against a `coverage.json` that records the gaps.
Reverse: not applicable.

## CU-C3-D11. The TS package tests are not rerun

Decision: `turbo run test` over `packages/**` was not run in this job; Job A's 97 of 98 tasks at
`f8a124edc` stands.
Reason: no file under `packages/` changed between `f8a124edc` and `6960bfba7`
(`git diff --stat f8a124edc 6960bfba7 -- packages` is empty). The lockfile change moves only the
`c8` subtree and `semver` 7.8.6 for four importers.
Cost: if the `semver` bump changed a package's behaviour, this job would not see it; CU-C7 runs the
TS suites.
Reverse: run `scripts/suite.sh /workspaces/wt/CU-C2 <out> ts`.

## CU-C3-D12. `rust-coverage:check` is not run

Decision: as 2026-11-18 CU-C3-D9: not run.
Reason: it needs `cargo-llvm-cov` and an instrumented build of every reported crate, and the record
moves with the port; CU-C7 is the job that regenerates it.
Cost: `vectors/rust-coverage.json` is not checked here.
Reverse: install `cargo-llvm-cov` and run it.
