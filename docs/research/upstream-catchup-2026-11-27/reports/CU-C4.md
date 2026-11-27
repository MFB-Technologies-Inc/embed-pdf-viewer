# CU-C4: the engine and runtime port

Every item CU-C3's work order gives CU-C4 is ported or checked. The 6 `epdf-engine` failures CU-C3
recorded pass, and `cargo test --workspace` has no failing name outside CU-C3's list: 3,592 passed
and 16 failed, the 16 being CU-C5's and CU-C6's. The change generator records 44 new engine cases
for field purpose and format, field groups and the signature mark, and all replay; on the code
before the port all 44 differed. Decisions are in
[`../decisions/CU-C4.md`](../decisions/CU-C4.md).

## Commits

On `research/catchup-2026-11-27-c4` from `0e83ecb84`.

| Commit | Item | Summary |
|---|---|---|
| `ea2a53684` | | Job B's review Minors in the CU-C3 record |
| `defe697f2` | 1 | `pdfium`: `drawing`, `set_drawing`, `drawings` (D-1, CU-C4-D2); `MkIcon` and the four MK icon calls |
| `1f2411c1c` | 2 | `TextPage::page_geometry`; the engine reads glyphs 4,096 per call |
| `065b8c3f5` | 4 | `API_VERSION` 3.4, `MARK_IMAGE_OR_NONE_SINCE`, `FIELD_PURPOSE_AND_FORMAT_SINCE`, recorded in `kinds.json` |
| `a88255b34` | 5, 6 | `forms/format.rs`; purpose and format in read, create, update, undo and copy; a new field in no group, `groupId: null` |
| `b04fefba9` | 3, 11 | The mark as a PNG, a JPEG or a PDF, and no mark; the host verb and `epdf-viewer-host` |
| `6770b00a8` | 7 | A marked widget's mark is fitted again when its box or turn changes |
| `6ec03250c` | 3, 5, 6, 7 | Four change-vector groups (44 cases), `change_ops.rs`, `signature_mark.rs` |
| `d208075d5` | 8, 10 | `font_fallback_named.rs`, `wire_numbers.rs` |
| `04def3c6f` | 9 | A `layerBytes` open with `baseSha256` |
| `2f2ccf56c`, `be8505172` | 3 | A stamp of a mark's picture shares its image |
| `82d3476a2` | 5 | `epdf-plugin-form`'s cross-check fixtures carry purpose and format (CU-C4-D11) |
| `5be56d92e` | | This report, the decisions, `scripts/cu-c4/` |

Fix round 1 (the task review's Important 1 and Minors 1 to 4):

| Commit | Item | Summary |
|---|---|---|
| `188440dc5` | 3, 7 | A signing's PNG mark, refused later marks, bytes that are no picture (verb and prepare), a signed field's mark turning |
| `937eb6767` | 3 | The op's `appearance` key is required: missing is `WireFormat`, `null` is a clear |
| `c77936a7b` | 3 | `bake_widget_appearance` reads the mark's format before looking for the widget |
| `ecf7f339c` | 5 | An import keeps a purpose and a format, with form design alone |
| the docs commit | | This report and the decisions updated |

## Items

1. Done (`defe697f2`). The three safe drawing wrappers take the runtime's names; no wrapper named
   them. `Annotation::mk_icon`, `set_mk_icon`, `has_mk_icon` and `clear_mk_icon` over `MkIcon`
   (`EPDF_MK_ICON_I`, `_RI`, `_IX`), tested in `crates/pdfium/tests/drawings.rs`: each icon set and
   read alone, a clear of nothing, a drawing that is no form XObject refused, a non-widget refused.
2. Done (`1f2411c1c`). `PageCharGeometry` mirrors `EPDF_PAGE_CHAR_GEOMETRY` (132 bytes, the object
   run at 124, the font size at 128). `crates/pdfium/tests/page_geometry.rs` checks that one call
   says what the per-character calls say on three fixtures, and that a page read in pieces of 7
   is the whole. `read_page_geometry` follows `PageGeometryReader.ts`: 4,096 per call, a short read
   is `RuntimeUnavailable`, and an upright glyph's cells go unread. The five `appendGlyphRecords`
   cases of `page-geometry-reader.test.ts` are ported in `geometry.rs`. The text and layout corpora
   pass unchanged.
3. Done (`b04fefba9`, `6ec03250c`, `2f2ccf56c`). `SignatureAppearance` is the mark's resource or
   `{ pdf }`; `forms.setSignatureAppearance` takes it or `null` as op and verb, `signatures.prepare`
   takes either spelling. An image becomes a page of its own size (`Document::image_document`);
   other bytes are `InvalidArg`, an image under `{ pdf }` is `MalformedPdf`, two pages `InvalidArg`.
   `null` clears the fit record and draws the box again; a widget with no mark is left alone. The
   op's `appearance` key is required, `null` included. A mark's format is read before its widget is
   looked for, as `markFormat` runs before `withWidgetAnnotation`. The 8 cases of
   `signature-mark.json` cover the structural tests of `runSignatureMarkConformance.ts`;
   `signature_mark.rs` the pixel tests, the two signing tests (a PNG mark kept and sealed against a
   JPEG and `null`; a prepare refusing bytes that are no picture), the verb's refusal of such bytes,
   which is the only gate on the native path, and the resource suite's shared-image case
   (CU-C4-D5). Not covered: a Blob, which has no Rust spelling.
4. Done (`065b8c3f5`). `every_change_kind_has_the_version_it_arrived_in` passes and checks the two
   new versions.
5. Done (`a88255b34`). `forms/format.rs` ports `purpose.ts`, `format.ts` and `datePattern.ts`, with
   13 unit tests from `datePattern.test.ts` and `format.test.ts`. JavaScript semantics are kept where
   they differ from Rust's: lengths in UTF-16 units, `trim` over `js_space`. `forms_copy`,
   `form_apply`, `form_field`, `form_reader` and `form_write` pass. `field-purpose-format.json` (25
   cases) covers 23 of the 24 tests of `runFieldPurposeFormatConformance.ts`; the import test is
   hand-written in `form_transfer.rs` (CU-C4-D9).
6. Done (`a88255b34`). `newFieldGroup` names no group by default, `namedGroup` refuses an empty or
   over-long id, `null` takes a field out with form design alone, and `propertiesOf` carries
   `groupId: null`, so undo and `expect` work on it. `field-groups.json` (9 cases) covers the new
   tests of `runFieldGroupsConformance.ts`; the hand-written designer test in `form_widgets.rs`
   follows upstream's. The attribution suite's group-of-the-creator test is behaviour the port
   already had (`annotations.rs`, `authorize_create`).
7. Done (`6770b00a8`). `refit_widget_mark` fits a marked widget's mark by its recorded fit, else
   `contain`, when an update changes its box or turn. `widget-rotation.json` (2 cases) and
   `signature_mark.rs` cover the rotation suite's three mark tests, the signed field's through a
   placeholder-CMS completion as `signature_sign.rs` signs.
8. Checked (`d208075d5`). The runtime fix arrived with `9472117ed`; `font_fallback_named.rs` passes
   for Hebrew and Arabic in a simple and a composite font, and a probe without the fallbacks draws
   nothing (CU-C4-D6). #1152: `epdf-fonts` builds its faces from `faces.generated.ts`, so no change.
9. Checked (`04def3c6f`): the host opens a `layerBytes` request with `baseSha256`.
10. Checked (`d208075d5`): no infinite number reaches a Rust schema check (CU-C4-D7).
11. Done (`b04fefba9`). `translate.rs` sends `appearance` or `pdf` with payload 0, or
    `appearance: null` with none; prepare's mark goes as a payload in both spellings.
    `crates/epdf-viewer-host/tests/translate.rs` has both, and a mark drawn in and taken out through
    the host.

## TDD

Each item's failing run comes before its fix in `/workspaces/logs/CU-C4/`. Items 1 and 2: the new
tests did not compile (`red-item1.log`, `red-item2.log`). Item 4: `change_ops` did not compile
against the old constants (`red-item4.log`). Item 5: the 6 recorded failures (`red-engine.log`).
Item 11: 3 translate tests failed (`red-item11.log`). For the vectors and pixel tests,
`scripts/cu-c4/red.sh` ran this branch's tests against `065b8c3f5` (item 4 done, no item 5 or later):
all 44 cases of the four new change files differ, and 7 of 9 in `form-copy.json`, and 6 of 7 mark
tests fail; the
one that passes is a PDF under `{ pdf }`, which the port drew before
(`red-engine-cases-2/summary.txt`). Items 8, 9 and 10 are checks of behaviour already there, so
they have no red run (CU-C4-D6, D7). In fix round 1 the two code fixes failed first
(`fix1-red-minor1.log`: an op without `appearance` parsed as a clear; `fix1-red-minor3.log`:
`NotFound` before `InvalidArg`), and `red.sh` against `065b8c3f5` fails the import test and 10 of
the 11 mark tests (`red-fix1/summary.txt`).

## Tests

All in container `embed-pdf-catchup1127`, worktree `/workspaces/wt/CU-C2` (CU-C4-D1),
`CARGO_TARGET_DIR=/workspaces/target`.

| Check | CU-C3 (`0e83ecb84`) | Now |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,545 passed, 22 failed, 2 ignored | 3,589 passed, 19 failed at `be8505172`; with `82d3476a2`, 3,592 and 16 |
| engine `:check` scripts | 36 of 36 | 36 of 36 |
| engine regeneration twice | 64 files, identical | 68 files, identical, no diff to the tree |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | 4 of 4 | 4 of 4 |
| plugin `replay:check` | 216 cases in 13 files | the same 216 (`replay-cases.tsv` byte-identical) |
| plugin evidence tests | 769 of 771 | 769 of 771, the same 2 (`ev/summary.txt` byte-identical) |
| TS `turbo run test` over `packages/**` | not rerun | not rerun: no file under `packages/` changed |

The 16 failures against `../results/CU-C3-cargo-failures.md`: `epdf-annotation-core` 2 and
`epdf-plugin-annotation` 2 (CU-C6); `epdf-plugin-form` 7, `epdf-plugin-signature` 3 and
`epdf-plugin-stamp` 2 (CU-C5). The 6 `epdf-engine` names are gone. The 3
`epdf-plugin-form` `engine_cross_check` failures the first full run showed were this job's (the DTO
gained two members); `82d3476a2` fixes them and the binary passes 8 of 8.

## Evidence

Logs in `/workspaces/logs/CU-C4/` in the container. Scripts in [`../scripts/cu-c4/`](../scripts/cu-c4/).
sha256, first 8 hex digits.

| Check | Command | Exit | Log | sha256 |
|---|---|---|---|---|
| Baseline engine | `cargo test -p epdf-engine` | 101 (the 6) | `red-engine.log` | `ad98ae3f` |
| Item 1 | `cargo test -p pdfium --test drawings` | 101, then 0 | `red-item1.log`, `green-item1.log` | `3df20e8a`, `4ed4f2bd` |
| Item 2 | `--test page_geometry`, `--lib geometry::`, corpora | 101, then 0 | `red-item2.log`, `green-item2.log` | `ba90993c`, `465b4b28` |
| Item 4 | `--test change_ops every_change_kind` | 101, then 0 | `red-item4.log`, `green-item4.log` | `17ab882a`, `f521ce83` |
| Item 5 | `--lib forms::format`; `cargo test -p epdf-engine` | 0, 0 | `item5-format-unit.log`, `item5-engine.log` | `659d2677`, `70550c58` |
| Item 11 | `cargo test -p epdf-viewer-host --test translate` | 101, then 0 | `red-item11.log`, `green-item11.log` | `626ea3de`, `7c659aa4` |
| Items 3, 11 | `cargo test -p epdf-engine -p epdf-viewer-host -p pdfium` | 0 | `item3-run2.log` | `ab7926a5` |
| Mark pixels | `--test signature_mark` | 0 | `mark-pixels-run3.log` | `26b01b7d` |
| Item 8 | `--test font_fallback_named` | 0 | `item8-run1.log` | `18abee9d` |
| New vectors | `--test change_ops` | 0 | `vectors-run2.log` | `97a17cc7` |
| Red run | `red.sh 065b8c3f5 be8505172 red-engine-cases-2` | 0 (script) | `red-engine-cases-2/summary.txt` | `159bded8` |
| Engine regeneration x2 | `after.sh` | 0 | `after/determinism.log`, `after/engregen-run{1,2}.sha` | `5a9313bc`; both `801c593a` |
| Workspace tests | `suite.sh … cargo` | 101 | `after/cargo.log`, `after/cargo.summary` | `5cca4d6b`, `5a016a2a` |
| Engine `:check` | `suite.sh … engconf` | 0 | `after/engconf.all` | `27a10bbd` |
| Plugin checks | `suite.sh … plugconf`, `replay:check` | 0, 1 | `after/plugconf.all`, `after/plugconf.replay:check.log` | `09dd3f6f`, `aa171f04` |
| Replayed cases | `replay-cases.py` | 0 | `after/replay-cases.tsv` | `49b349a9` |
| Evidence | `after.sh`, evidence part | 0 | `after/ev/summary.txt` | `6290cd61` |
| Fix 1: signing and mark tests | `--test signature_mark` | 0 | `fix1-signing.log` | `18cb27b3` |
| Fix 1: required key | `--lib changes::op` | 101, then 0 | `fix1-red-minor1.log`, `fix1-green-minor1.log` | `3a8f6ed5`, `692ca509` |
| Fix 1: format before widget | `--lib a_mark_that_is_no_picture` | 101, then 0 | `fix1-red-minor3.log`, `fix1-green-minor3.log` | `b7dd93e4`, `ddbf981c` |
| Fix 1: import | `--test form_transfer` | 0 | `fix1-import.log` | `ee0854de` |
| Fix 1: red run | `red.sh 065b8c3f5 ecf7f339c red-fix1` | 0 (script) | `red-fix1/summary.txt` | `62f0c497` |
| Fix 1: covering suites | `cargo test -p epdf-engine -p epdf-viewer-host`; `suite.sh … engconf` | 0, 0 | `fix1/cargo.log` (1,822 passed, 0 failed), `fix1/engconf.all` (36 of 36) | `2ef19695`, `27a10bbd` |

## For CU-C5 and the wrappers

The engine side CU-C5 builds on: field rows carry `purpose` and, for text and dropdowns, `format`;
a create or update takes both; a new field is in no group unless named; `groupId: null` takes a
field out. `forms.setSignatureAppearance` takes the mark's bytes, `{ pdf }` or `null` as op and
verb, and `epdf-viewer-host` sends what its caller passes (`$bytes`, `{ pdf: $bytes }` or `null`),
so CU-C5's `setAppearance(field, mark | null)` and `sign` need no further host change. The kernel
paths are unchanged. MK icons and the drawing names have no engine or wrapper surface.

## Known gaps

- No import case is recorded by the generator (it is hand-written), and `codec::parse` does not
  complete an older bundle (CU-C4-D9).
- `rust-coverage:check` was not run, as in CU-C3 (CU-C3-D12).
- `runtime-src` names `9472117ed`, only in the local runtime fork until CU-C10 (D-3).
