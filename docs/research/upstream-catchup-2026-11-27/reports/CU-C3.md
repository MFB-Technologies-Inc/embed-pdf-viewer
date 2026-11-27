# CU-C3: tooling, vectors, and the work order

The conformance tooling builds against upstream `9af4d9ee7` again: the plugin generator's 48
typecheck errors and its import of the deleted `blank-page.ts` are fixed by following upstream in
the generator, and `coverage:check` runs under node 26 with `c8` 12. The engine vectors and the
plugin vectors were each regenerated twice, byte-identical both times. The evidence archives are
back at their baseline of 769 of 771. `cargo test --workspace` goes from 3,567 passed and 0 failed
to 3,545 passed and 22 failed, all of it upstream behaviour the port doesn't have yet: 216 replayed
cases in 13 plugin vector files and 6 `epdf-engine` tests. No Rust changed. The work order for
CU-C4 to CU-C9 is the last section; it routes by crate, which moves the engine halves of #1140,
#1147 and #1118 to CU-C4 (CU-C3-D9). Decisions are in [`../decisions/CU-C3.md`](../decisions/CU-C3.md).

## Commits

| Commit | Summary |
|---|---|
| `c4175cc8f` | `c8` 12 in `tooling/plugin-conformance`, lockfile re-resolved (CU-C3-D6) |
| `3a587e088` | Generator follows upstream: blank page, form fixtures, form-core signatures, widget box (CU-C3-D2 to D4) |
| `97d0a5d71` | Engine vectors regenerated: `purpose`, `format`, `apiVersion` 3.4, the mark on the wire |
| `8ea991f23` | Plugin vectors, `prelude.js` (CU-C3-D5) and `coverage.json` regenerated |
| `6960bfba7` | Evidence archives re-baselined (CU-C3-D8) |
| the commit that adds this file | Report, decisions, results, scripts, two CU-C2 wording fixes |

## Generator fixes

After the merge `tsc --noEmit` in `tooling/plugin-conformance` reported 48 errors, and every check
stopped on `ERR_MODULE_NOT_FOUND` for `packages/plugin/signature/src/blank-page`. `3a587e088`
follows upstream for each, as `c74ec3e9d` did for `ed064e0c8`:

| Break | Upstream | Fix |
|---|---|---|
| `blank-page.ts` deleted | `fcb69611f` (#1145): a clear sends `null` | the two function cases go (CU-C3-D2) |
| 8 errors: field `purpose`, `format` | `833b40015` (#1147) | fixtures carry `purpose: null`, `format: null` |
| 31 errors: `PageWidget` has `ref`, `rotation`, `rect` | `23741e3fd` (#1118) | `picture` and `pictureAt()` (CU-C3-D3) |
| 4 errors: `projectWidget` takes `canFill`; 1: `fillItems` takes two predicates | `d6d7c25e2` (#1141) | `canFill` as `fillable`, both predicates allowing |
| 4 errors: `predictFormOp` takes no context | `fce760aeb` (#1140) | session-group cases replaced (CU-C3-D3) |

With those fixed, the generator stopped on two unanswered `apply` calls in
`plugins/signature.json` `an-armed-stamp-is-not-a-mark`: the fixture's widgets had `box`
(100, 100, 100, 100) far from their `rect`, which upstream now hit-tests by (CU-C3-D4).

## Engine vectors

`regen2x.sh` ran every engine generator twice on `3a587e088`: identical logs and identical sha256
lists over 64 files (`deterministic=yes`), every group `ok`. Seven files changed:

| Files | Change | Upstream |
|---|---|---|
| `forms/{snapshot,write,field,apply}`, `changes/form-copy` | every field reads `purpose: null`; text and combo box fields `format: null` | `833b40015` (#1147) |
| `changes/kinds` | `apiVersion` 3.3 to 3.4 | `MARK_IMAGE_OR_NONE_SINCE` (`fcb69611f`, #1145), `FIELD_PURPOSE_AND_FORMAT_SINCE` (`833b40015`, #1147) |
| `wire/requests` | `forms.setSignatureAppearance` gains optional `appearance`, `pdf` becomes optional | `fcb69611f` (#1145) |
| `wire/requests` | `document.checkPasswordPermissions` `mode` prints `"any" \| "owner"`, was `"owner" \| "any"` | none: the checker's union order (CU-C3-D7) |

All 36 engine `:check` scripts pass on the result (Job A's head had 30 of 36).

## Plugin vectors

`TZ=UTC pnpm run vectors` twice: identical sha256 lists over 126 vector files and `prelude.js`.
Nineteen vector files and `coverage.json` changed (`vector-casediff.log`, `vector-diffpaths.log`); no vector gained an
"is not a function", "unanswered" or `TypeError` string (`errgrep.mjs`).

| Vector files | Change | Upstream |
|---|---|---|
| `plugins/{form-actions,form-core-lifecycle,form-design,form-fill,form-scripting,signature,stamp-api,stamp-placement}` | form session state gains `designGroup: null` and `groups: []` | `d6d7c25e2` (#1141) |
| the same and `actions-*`, `core/acrojs` | engine field rows gain `purpose`, `format` | `833b40015` (#1147) |
| `plugins/form-core` | `projectWidget` and fill items gain `ref`, `rotation`, `rect`, `groupId`, `canFill`, `required`, `empty`, `accent`; form tools gain `upright`; the session-group cases replaced | `23741e3fd` (#1118), `d6d7c25e2` (#1141), `fce760aeb` (#1140) |
| `plugins/form-design` | a placed widget is sent as its `box` | `23741e3fd` (#1118) |
| `plugins/signature` | a mark is sent as its bytes (`appearance`), a clear as `null`; fixture widgets' box at their rect | `fcb69611f` (#1145); CU-C3-D4 |
| `plugins/signature-functions` | `blank-page`, `blank-page-cached` removed | `fcb69611f` (#1145) |
| `core/annotation-kinds` | `caps.quarterTurns`, true for the six widget kinds | `23741e3fd` (#1118) |
| `core/annotation-update`, `plugins/annotation-api`, `plugins/annotation-tools` | a box's rect at a quarter turn is exact (99.99999999999997 becomes 100) | `23741e3fd` (#1118) |
| `plugins/annotation-repository` | a widget create draft is `box` and `rotation`, not `rect` | `23741e3fd` (#1118) |
| `plugins/annotation-pure` | zod 4 issue text; `line-infinite` is refused | `7a878cf7f` (#1151) |
| `core/acrojs` | `prelude-source`, and fields with `purpose` | `833b40015` (#1147) |
| `coverage.json` | rewritten by `pnpm run coverage` under `c8` 12 | |

`prelude.js` is the 32-line, 3-line-removed diff Job A saw, upstream's `prelude.ts` (CU-C3-D5).

## Coverage under node 26

`c8` 10.1.3 depends on `yargs` 17.7.2, whose `yargs/yargs` is an extensionless CommonJS file in a
`"type": "module"` package; node 26.13.1 loads it as ESM and fails with "require is not defined in ES
module scope". `c8` 12.0.0 depends on `yargs` 18 and declares node `>=23`. The fix is the port's
own: upstream has no `c8`, and the repository runs node 26 everywhere (CU-C3-D6). `coverage` and
`coverage:check` pass: statements 78,920 of 83,439, branches 15,505 of 17,059, 42 ported files
unloaded (was 77,931 of 82,243 and 42).

## Evidence archives

`node --test` over every `test/*.test.ts` and `test/*.test.mjs` (`evrun.sh`). At `d250a68e3`: 769 of
771, with the 2 known failures in `current-suites.test.ts` (`grid-nan-columns` expecting
`change-hooks`, and a `TypeError` reading index 6). After the regeneration: 744 of 771, 16 files
failing. `6960bfba7` re-baselines them (CU-C3-D8). Final: 769 of 771, the same 2 failures;
`ev-final/summary.txt` is byte-identical to `ev-base/summary.txt`.

## Tests

All in container `embed-pdf-catchup1127`, worktree `/workspaces/wt/CU-C2` (CU-C3-D1),
`CARGO_TARGET_DIR=/workspaces/target`.

| Check | Start (Job A, `f8a124edc`) | Now |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,567 passed, 0 failed, 2 ignored | 3,545 passed, 22 failed, 2 ignored |
| engine `:check` scripts | 30 of 36 | 36 of 36 |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | 0 of 4 | 4 of 4 |
| plugin `replay:check` | not run | fails: 216 cases in 13 files (the cargo failures) |
| plugin evidence tests | not run (769 of 771 at `d250a68e3`) | 769 of 771, same 2 |
| TS `turbo run test` over `packages/**` | 97 of 98 | not rerun: no package source changed (CU-C3-D11) |

The cargo failures by crate, cause and job, with every failing case, are in
[`../results/CU-C3-cargo-failures.md`](../results/CU-C3-cargo-failures.md).

## Coverage gaps

[`../results/CU-C3-coverage-gaps.md`](../results/CU-C3-coverage-gaps.md): 277 uncovered lines that the
range added, in 17 files, and 3 upstream-touched files no generator loads. `plugin/form` has 236 of
them (groups, the design group, Tab order, the control-attach API), `core/acrojs` 21, `core/annotation`
16, `plugin/annotation` 4. The engine's new behaviour isn't measured by c8; CU-C4 item 9 lists it.

## Work order

Each item names its job. CU-C4 to CU-C6 run in parallel on their own branches from this tip, so the
work is split by crate, not only by PR (CU-C3-D9): CU-C4 owns `pdfium-sys`, `pdfium`, `epdf-engine`,
`epdf-viewer-host` and `epdf-fonts`; CU-C5 owns `epdf-form-core`, `epdf-plugin-form`,
`epdf-plugin-signature`, `epdf-signature-core` and `epdf-acrojs`; CU-C6 owns `epdf-annotation-core`,
`epdf-plugin-annotation`, `epdf-plugin-i18n` and the payload build. A PR that spans them is split,
and each half is listed under its owner. "Case" is a new engine vector case or plugin-conformance
case; "replay" is an existing case that fails today.

### CU-C4: engine and runtime (#1157, #1145 engine half, #1150, #1159, #1152, #1151 engine half, and the engine halves of #1147, #1140, #1118)

Failing now: the 6 `epdf-engine` tests in the results file (`change_ops` twice, `form_apply`,
`form_field`, `form_reader`, `form_write`).

1. D-1 and the push-button icon. `pdfium` keeps its safe `stamp_drawing`, `set_stamp_drawing` and
   `stamp_drawings` over the renamed exports (`f8a124edc`); upstream's TS engine renamed nothing
   public, and no wrapper (`epdf-uniffi`, `epdf-capi`, `epdf-web`, `packages/core-wasm`,
   `platforms/**`) names a stamp drawing, so D-1's wrapper-facing rename is empty. Add safe
   wrappers for `EPDFAnnot_GetMKIcon`, `EPDFAnnot_SetMKIcon`, `EPDFAnnot_HasMKIcon` and
   `EPDFAnnot_ClearMKIcon` with a `pdfium` test. Upstream's TS calls none of them, so they have no
   engine or wrapper surface yet.
2. Text page geometry (#1157): a safe `EPDFText_GetPageGeometry` in `pdfium`, and the engine's glyph
   read through it in batches of 4,096 (`PageGeometryReader.ts` `GLYPHS_PER_CALL`). No vector moved,
   so the existing text and layout vectors are the test; port the cases of
   `packages/engine/main/test/page-geometry-reader.test.ts` (+173 lines).
3. The signature mark (#1145 engine half, `fcb69611f`): `forms.setSignatureAppearance` takes
   `appearance` (a PNG, a JPEG or a one-page PDF, as its bytes) or nothing, and `pdf` only as the
   `{ pdf }` spelling; no mark takes the mark out and the field shows its box again; undo and redo;
   a field with two widgets; refusal of other bytes and of a two-page PDF; signing and
   `signatures.prepare` take an image as the mark. `epdf-engine`'s
   `FormWriter::set_signature_appearance(reference, pdf, cancel)`
   (`crates/epdf-engine/src/features/forms/write.rs:393`) takes PDF bytes only today. Cases: the
   16 tests of `runSignatureMarkConformance.ts` and the stamp-of-a-mark test of
   `runAnnotationResourceConformance.ts`.
4. `apiVersion` 3.4 with `MARK_IMAGE_OR_NONE_SINCE` and `FIELD_PURPOSE_AND_FORMAT_SINCE`
   (`crates/epdf-engine/src/features/changes/kinds.rs:13`). Fixes
   `every_change_kind_has_the_version_it_arrived_in`.
5. Field purpose and format (#1147 engine half, `833b40015`, `d8f715c2b`, `bb6c06b24`,
   `82cd09ddc`): the read (`purpose` from `/EMBD_Metadata`, `format` from the format script as
   Acrobat decides it, `isDatePattern`, `isTimePattern`), create and update with undo, the
   expectation guards, copy and import, `doc.forms.modify` gating, the NUL and whitespace refusal,
   and the field DTO's two members. Fixes `forms_copy`, `form_apply`, `form_field`, `form_reader`
   and `form_write`. Cases: the 24 tests of `runFieldPurposeFormatConformance.ts`.
6. Field groups (#1140 engine half, `fce760aeb`): a new field is in no group unless its draft
   names one, `groupId: null` takes a field out with form design alone, an empty id is refused,
   undo, the `expect` guard, and copies (`authorizeFieldGroup` takes `null`). Cases: the 9 new
   tests of `runFieldGroupsConformance.ts`, and `runAnnotationAttributionConformance.ts`'s
   group-of-the-creator test.
7. Turned widgets (#1118 engine half, `23741e3fd`, `c76603e3f`): `forms.updateWidget` with a `box`
   or `rotation` moves the rect it stands in (`standingRectOf`), and a mark in a turned signature
   field is fitted into its frame and turned with it. Cases: the 3 new tests of
   `runWidgetRotationConformance.ts`.
8. Fonts (#1159, #1152): the runtime's registered fallback fix arrives with `9472117ed`; the case
   is `runDocumentFontsConformance.ts`'s "a page draws a script in a font it only names in the
   fallback". #1152 changes how `packages/core/fonts/src/built-in.ts` reads a face's hash, not the
   hash; `epdf-fonts` needs a check, not a change.
9. #1150 (`faa04047c`): `LocalEngine` now forwards `baseSha256` on a `layerBytes` open. The Rust
   host ignores `baseSha256` by design (`crates/epdf-engine/src/host.rs:2945`), so the engine needs
   nothing; CU-C8 checks the browser client sends it as upstream does.
10. #1151 engine half (`7a878cf7f`): zod 4 refuses an infinite number and words its issues
    differently. No engine vector moved (`schema:check` passes); check that the Rust request
    parsing refuses `Infinity` where upstream's schemas now do. The plugin-visible half is CU-C6
    item 3.
11. The native host's calls (`crates/epdf-viewer-host/src/translate.rs`, as in 2026-11-18 item 12):
    `forms.setSignatureAppearance` sends `appearance` or `pdf` or nothing, with the payload only when
    there is a mark (`LocalDocumentFormsService.ts`); `signatures.prepare` sends the mark as a
    payload (`LocalDocumentSignaturesService.ts`); `WorkerHost.ts`
    `signatureAppearanceOfRequest` reads them back. Tests in
    `crates/epdf-viewer-host/tests/translate.rs`.

### CU-C5: forms (#1138, #1141, #1136, #1146, #1149, #1135, #1145 plugin half, and the plugin halves of #1140, #1147, #1118)

Failing now: 7 `epdf-plugin-form` tests, 3 `epdf-plugin-signature` tests, and the 2
`epdf-plugin-stamp` tests whose only difference is the form plugin's state; 167 replayed cases in
`plugins/{form-actions,form-core,form-core-lifecycle,form-design,form-fill,form-scripting,signature,stamp-api,stamp-placement}.json`.

1. Named groups and the design group (#1141, `d6d7c25e2`): `FormModel.designGroup` and `groups`,
   `listGroups`, `getDesignGroup`, `setGroups`, `setDesignGroup`, `moveToGroup`, `canSetGroup`,
   `onDesignGroupChanged`, `onGroupsChanged`, `listSelectedFields`, the `fields.tint` setting, the
   group's look (`GroupLook`) and a box's accent, and a label ending with its group's label. Fixes
   the `designGroup` replays across the nine files.
2. Fill items and hits (#1118, #1141, #1136): `projectWidget` and `fillItems` with `canFill`, a
   radio button's `inGroup` and name with its option (`cbdb46811`), `required`, `empty`, `accent`,
   `ref`, and the widget place (`box`, `rotation`, `rect`); `widgetAt` hit-tests by `rect`; form
   tools are `upright` and place a widget as its `box`. Fixes `plugins/form-core.json` (39) and the
   `form-design` placements.
3. Prediction without a session group (#1140 plugin half, `fce760aeb`): `predictFormOp` and
   `undoLookOf` take no context. Update `every_form_case_replays`'s pinned count (105 to 104).
4. The control-attach API and focus (#1138 `869997aa0`, #1135, #1146, #1149): `getWidgetAnchor`,
   `isWidgetFocused`, `takesCursor`, `focus` returning a value; Tab passes over controls that can't
   take focus and over a signature field nothing can sign; a radio group's stop
   (`services/tab-order.ts`, `services/cursor.ts`, `write/focus.ts`). Cases for the 236 uncovered
   `plugin/form` lines.
5. Purpose and format in the plugin (#1147): field rows and drafts carry them; `epdf-acrojs` gets
   the host date parse of `packages/core/acrojs/src/dates.ts` (21 uncovered lines) and
   `__acrojsParseDate`. The prelude itself is already in (CU-C3-D5).
6. The signature mark (#1145 plugin half): `setAppearance(field, mark | null)`, a clear sends
   `null`, `sign` and `prepareSignature` send the mark's bytes as `appearance`; remove
   `blank_page_pdf`. Fixes `plugins/signature.json` (12 of its 13) and the two `behavior` tests.
   Depends on CU-C4 item 3 for the engine side and item 11 for the native host.

### CU-C6: annotation and build (#1118 annotation half, #1141 annotation half, #1151 plugin half, #1125, #1143, #1155)

Failing now: 2 `epdf-annotation-core` tests and 2 `epdf-plugin-annotation` tests; 49 replayed
cases in `core/annotation-kinds.json` (30), `core/annotation-update.json` (1),
`plugins/annotation-pure.json` (10) and `plugins/annotation-repository.json` (8).

1. Upright fields in the annotation core (#1118, `23741e3fd`, `ccfcaf0fc`, `282fe57b5`): a widget
   is a turned box (`box`, `rotation`, `rect`); the `quarterTurns` cap; `rotateSelection` by a
   quarter turn and `resetRotation` on widgets; the `orientation` selection property and
   `orientationPatch`; an exact rect at a quarter turn; a turned selection kept on its page;
   `previewPlacement` with a display rotation; widget create drafts as `box`. Fixes the 30 kinds
   cases, `runs/edit-3-edit-move-group-commit` and the 8 repository cases. Cases for the 16
   uncovered `core/annotation` lines.
2. The group accent (#1141 annotation half, `d6d7c25e2`): `RecordAccent` (`{ color, strength }`)
   in `core/annotation` and `accentOf` on the annotation host, read in `plugin/annotation`
   `read/view.ts` (4 uncovered lines). CU-C5 item 1 uses the same shape; whichever job lands
   second takes the other's type.
3. zod 4 in `parseAnnotationDraft` (#1151 plugin half, `7a878cf7f`): the issue text the 9 refused
   drafts carry, and `line-infinite` refused. As with the pkijs 4 messages last time, decide whether
   the port reproduces zod's text or records an outcome exception.
4. i18n: no change. The keys #1118 added (`rotateClockwise`, `rotateCounterclockwise`,
   `orientation`, `orientations`, `group`) are in `packages/viewer/chrome/src/locales/*.ts`, which
   isn't ported; `packages/plugin/i18n/src/strings` didn't change in the range.
5. Build (#1125, #1143, #1155): the Rust-core `wasm32` payload uses the same
   `packages/engine/runtime/scripts/build-target.sh`, `generate-functions.mjs` and
   `render-function-table.mjs` that #1125 and #1143 changed, so build it with the Rust core
   (Job A built it with `EMBEDPDF_RUST_CORE=0`) and check `verify-wasm-abi.mjs` and
   `verify-rust-core.mjs`. #1155 minifies `packages/engine/main`'s worker, which the Rust-core
   payload doesn't use; check `packages/core-wasm` doesn't bundle it.

### CU-C7, CU-C8, CU-C9: what each wrapper exposes

The Rust surface the wrappers reach once CU-C4 to CU-C6 land. Swift (`platforms/apple/EmbedPDF`),
Kotlin (`platforms/android`) and .NET (`platforms/dotnet`) reach the form, signature and annotation
plugins through `epdf-viewer-host` (each has a `Form` facade with `focusNext`, `moveInGroup`,
`clearField`); the browser reaches the same plugins through `packages/core-wasm/src/plugins.ts` and
`epdf-web`.

| Surface | Rust owner | Swift (CU-C9) | Kotlin, .NET (CU-C8) | Browser (CU-C8) |
|---|---|---|---|---|
| Form groups: `listGroups`, `setGroups`, `getDesignGroup`, `setDesignGroup`, `moveToGroup`, `canSetGroup`, `listSelectedFields`, the two events, `fields.tint` | CU-C5 1 | yes | yes | yes |
| Fill item and hit fields: `ref`, `box`, `rotation`, `rect`, `groupId`, `canFill`, `required`, `empty`, `accent`, `inGroup` | CU-C5 2 | yes | yes | yes |
| Control attach: `getWidgetAnchor`, `isWidgetFocused` | CU-C5 4 | yes, and the form controls UI (#1138) where the platform renders forms | yes, same | yes |
| Field `purpose` and `format`, in reads, create and update | CU-C4 5, CU-C5 5 | yes | yes | yes |
| A signature mark as a PNG or JPEG, and a clear that takes it out | CU-C4 3, CU-C5 6 | yes | yes | yes |
| Widget quarter turns: `orientation`, `rotateSelection`, `resetRotation` on widgets | CU-C6 1 | yes | yes | yes |
| `layerBytes` open with `baseSha256` | CU-C4 9 | where the facade opens layers | same | the client forwards it |
| MK icon, stamp drawing names | CU-C4 1 | none: no TS surface | none | none |

CU-C7 merges the three branches and regenerates `coverage.json` on conflict, as planned.

### Upstream PRs in `c4e744345..9af4d9ee7`

| PR | Merge | Items |
|---|---|---|
| #1118 upright fields | `cca9583f9` | CU-C4 7; CU-C5 2; CU-C6 1, 4; viewer chrome not ported |
| #1125 runtime build speed | `9e84385cb` | CU-C6 5 |
| #1135 dropdown keeps focus | `a3870b91a` | CU-C5 4 (framework UI; no Rust source changed) |
| #1136 radio accessible names | `535fe554a` | CU-C5 2 |
| #1138 form controls | `b65cf8511` | CU-C5 4; platform UI in CU-C8, CU-C9 |
| #1140 field group default | `a9022ea77` | CU-C4 6; CU-C5 3 |
| #1141 form signers | `bab772746` | CU-C5 1; CU-C6 2 |
| #1143 compact function table | `ca9770949` | CU-C6 5 |
| #1144 attach scrollbar | `045938a6c` | none: framework packages |
| #1145 signature mark image and clear | `ccc67423d` | CU-C4 3, 4, 11; CU-C5 6 |
| #1146 signature field tab | `2748194e0` | CU-C5 4 |
| #1147 field purpose and format | `c9eed3ada` | CU-C4 4, 5; CU-C5 5 |
| #1149 radio group tab | `1b2c7575e` | CU-C5 4 |
| #1150 layer bytes `baseSha256` | `d8321b794` | CU-C4 9 |
| #1151 zod 4 | `d42379a29` | CU-C4 10; CU-C6 3 |
| #1152 worker fonts | `7cde62d4e` | CU-C4 8 |
| #1155 minify worker | `e685c0a2d` | CU-C6 5 |
| #1157 text page geometry | `d4acef456` | CU-C4 2 |
| #1159 unembedded font fallback | `3726d916d` | CU-C4 8 |
| #1104, #1123, #1126, #1127, #1153, #1156, #1158 | | none: release, docs, website, `cloudpdf/server` |

Moved from the plan's assignment (CU-C3-D9): the engine halves of #1147 and #1140 from CU-C5 to
CU-C4, since the failing tests are in `epdf-engine`; #1118's engine half to CU-C4 and its
`plugin/form` half to CU-C5, leaving CU-C6 the annotation crates; #1141's annotation half to
CU-C6; #1151's `parseAnnotationDraft` replays to CU-C6. The plan's "i18n keys" for CU-C6 turned out
to be viewer chrome only.

## Evidence

Base `d250a68e3` for the evidence baseline, start `a137ca2e6`, tested head `6960bfba7` (vectors at
`8ea991f23`); the head is the commit that adds this file. Logs are in `/workspaces/logs/CU-C3/` in the
container. Scripts are in [`../scripts/cu-c3/`](../scripts/cu-c3/). Hashes are sha256, first 8 hex
digits.

| Check | Command | Exit | Log | sha256 |
|---|---|---|---|---|
| Generator typecheck | `pnpm -s run typecheck` | 0 (48 errors at `a137ca2e6`) | `final-typecheck.log` (empty) | `e3b0c442` |
| `c8` bump | `pnpm --filter @embedpdf/tooling-plugin-conformance add -D c8@^12.0.0` | 0 | `c8-bump.log` | `4a67159d` |
| Engine and plugin regen x2 | `regen2x.sh /workspaces/wt/CU-C2 regen1` | 0 | `regen1/determinism.log`; `regen1/engregen-run{1,2}.sha`; `regen1/plugregen-run{1,2}.sha` | both `8a32f4ca`; both `d7ed2840` |
| Vector review | `zcasediff.mjs`, `diffpaths.mjs`, `errgrep.mjs` | 0 | `vector-casediff.log`, `vector-diffpaths.log`, `vector-errgrep.log` | `a7af1ed1`, `d592aa52`, `5040de32` |
| `prelude.js` | the generator | 0 | `prelude-regenerated.diff` | `ec6e117c` |
| `coverage.json` and check | `pnpm -s run coverage`, `coverage:check` | 0, 0 | `coverage-write.log`, `coverage-check.log` | `fc59ad48`, `dccb401b` |
| Coverage gaps | `python3 gaps.py` | 0 | `coverage-gaps.txt` | `3c5cdcc4` |
| Evidence at base | `evrun.sh /workspaces/wt/base base` | 0 | `ev-base/summary.txt` | `6290cd61` |
| Evidence after regen | `evrun.sh … regen` | 0 | `ev-regen/summary.txt` | `f9214a65` |
| Re-pins | `srcpins.py --write`, `owners.py --write`, `chainpins.py`, ten C-series `test/*-evidence.ts` captures, `restamp.mts`, `repin-loop.sh` | 0 | `srcpins.log`, `owners.log`, `chainpins.log`, `chainpins2.log`, `repin-form-current.log`, `repin-form-script.log` | `c88a825b`, `c1882aa6`, `d9606279`, `5f5e78d5`, `18a8881a`, `7e173c26` |
| Evidence final | `evrun.sh … final` | 0 | `ev-final/summary.txt` | `6290cd61` |
| After suite | `heavy-build.sh CU-C3 -- … after.sh /workspaces/wt/CU-C2 after` | 0 (script) | `after/suite.log`, `after/cargo.log`, `after/plugconf.replay:check.log` | `82fd1ab5`, `3a5f70e3`, `2370fe51` |
| Failures by crate and case | `cargo-by-crate.py`, `replay-cases.py` | 0 | `after/cargo-by-crate.tsv`, `after/replay-cases.tsv` | `1bc38488`, `49b349a9` |
| Plugin checks after the re-baseline | `vectors:check`, `coverage:check`, `test:harness` | 0, 0, 0 | `final-vectors:check.log`, `final-coverage:check.log`, `final-test:harness.log` | `3ac92408`, `cf2f4327`, `b54f07e1` |

## Known gaps

- The 22 cargo failures and 216 replayed cases are the work order, not fixed here.
- The TS package tests were not rerun (CU-C3-D11); the 2 expired OCSP fixtures in
  `@embedpdf/core-signature` (Job A concern 4) still fail.
- `rust-coverage:check` was not run, as in 2026-11-18 (CU-C3-D12).
- `runtime-src` names `9472117ed`, which is only in the local runtime fork until CU-C10 (D-3).
