# CU-C4 decisions

Decisions made while porting the engine and runtime half of upstream `c4e744345..9af4d9ee7` to the
Rust crates (Job C), on `research/catchup-2026-11-27-c4` from `0e83ecb84`.

## CU-C4-D1. Work in the branch's own container worktree

Decision: `research/catchup-2026-11-27-c4` was created with `git checkout -b` inside
`/workspaces/wt/CU-C2`, the worktree Job A prepped and Job B worked in. Logs are in
`/workspaces/logs/CU-C4/`, scripts in `/workspaces/scripts/cu-c4/` (committed as `scripts/cu-c4/`).
Reason: the prep (dependencies, the `9472117ed` PDFium pair, the wasm32 payload, package builds, a
warm `/workspaces/target`) is what this job needs, and git refuses a second worktree on a branch
that is checked out. `research/catchup-2026-11-27-plugins` itself did not move.
Cost: the worktree's name says CU-C2 for a third job. It now has `-c4` checked out, so CU-C5 starts
by checking out its own branch there too, or adds a worktree.
Reverse: `git checkout research/catchup-2026-11-27-plugins` in that worktree.

## CU-C4-D2. The D-1 rename reaches the `pdfium` crate's safe wrappers

Decision: `Annotation::stamp_drawing`, `Annotation::set_stamp_drawing` and `Document::stamp_drawings`
are now `drawing`, `set_drawing` and `drawings`, the names of the runtime exports they wrap
(`EPDFAnnot_GetDrawing`, `EPDFAnnot_SetDrawing`, `EPDFDoc_GetDrawings`). `StampFit` keeps its name.
Reason: CU-C3's work order (item 1) kept the old names because upstream's TS renamed nothing public.
The user's ruling for this job is to follow upstream's rename everywhere the Rust engine exposes it,
and the only Rust names that said "stamp drawing" were these three. Upstream's TS still calls the
fit `StampFit` (`APPEARANCE_FIT_CODE` is a table of it), so that name follows upstream as it is. No
wrapper (`epdf-uniffi`, `epdf-capi`, `epdf-web`, `packages/core-wasm`, `platforms/**`) named any of
the three, so the wrapper-facing rename stays empty.
Cost: a source break for any Rust caller of the three old names; inside the repository the four
callers (`annotation/transfer.rs`, `annotation/write/stamp.rs`, `tests/annotation_create.rs`) moved
with them.
Reverse: rename the three back in `crates/pdfium/src/stamp.rs` and its callers.

## CU-C4-D3. The new engine cases are change-generator groups

Decision: the cases for #1147, #1140, #1145 and #1118's mark are four new groups of
`generate-change-vectors.ts` (`field-purpose-format.json`, `field-groups.json`,
`signature-mark.json`, `widget-rotation.json`, 44 cases), replayed by `change_ops.rs`. To carry them
the generator gained forms-service verb steps with op ids, a per-case `open` (scope, identity), an
in-memory fixture as `fixtureHex`, and bytes in a request as `{ "$hex" }`, which the runner sends as
payloads.
Reason: the change generator records the worker exchange of each call and the Rust `Host` replays it
whole, so every answer (the field DTO, the item, the refusal code) is compared, not a view of it.
The upstream suites build their fixtures in memory (`FIELD_PURPOSE_FORMAT_PDF`, `FIELD_GROUPS_PDF`,
`TURNED_FIELDS_PDF`, the mark suite's page), which the generator imports and records. The six
existing change files regenerate byte for byte, so `$hex` changed none of them.
Cost: `field-purpose-format.json` is 1.0 MB, most of it whole form snapshots. A step that the
TypeScript client refuses before sending anything (a filler's design write, raw bytes that are no
picture) records no exchange, so Rust replays nothing for it: the client-side gate is the
wrappers' (CU-C8, CU-C9), not the host's. Forms imports are not recorded (see D9).
Reverse: delete the four groups from `CATCHUP_GROUPS` and the four tests in `change_ops.rs`.

## CU-C4-D4. A verb's `event` is the host's, and is left out of the comparison

Decision: `change_ops.rs` drops `event` from a verb's answer before comparing it, as it drops
`events` and `mutationSequence` from `document.apply`'s.
Reason: the Rust `Host` answers a single verb `{ result, mutationSequence, event }`
(`field_verb`); the TypeScript worker's payload is `{ result }`, and its main thread derives the
event. It is this port's events envelope, the same exception the runner already makes.
Cost: a verb's event is not compared by these vectors; `form_field.rs` and the plugin vectors check
events where they matter.
Reverse: remove `"event"` from `HOST_ONLY`.

## CU-C4-D5. Pixels are checked by hand-written tests over the vector's fixtures

Decision: `tests/signature_mark.rs` ports the raster checks of `runSignatureMarkConformance.ts`,
`runWidgetRotationConformance.ts` and `runAnnotationResourceConformance.ts`'s mark case (7 tests),
reading the marks from `signature-mark.json`'s `fixtures`.
Reason: the change vectors record worker exchanges; a recorded raster would compare anti-aliasing
between two builds, not behaviour. The upstream tests compare a mark's bands, a quarter-turned
raster within 3 levels and a shared image count, which the Rust tests do the same way
(`quarterTurned`, `maxShiftedDifference` with radius 1).
Cost: the expectations are the upstream test's, restated, not recorded from a TypeScript run.
Reverse: delete the file.

## CU-C4-D6. #1159's case runs on the fallback fonts upstream ships

Decision: `tests/font_fallback_named.rs` builds `pdfNamingFont` in Rust, registers
`packages/default-fallback-fonts/fonts/NotoSansHebrew-Regular.ttf` and `NotoNaskhArabic-Regular.ttf`
as `LocalEngine` does (`fonts.register` with `builtIn`, then `fonts.addFallback`), and checks each
character's 28 columns as `runDocumentFontsConformance.ts` does.
Reason: the fix is the runtime's (`9472117ed`, embedpdf/runtime#132) and arrived with Job A, so the
case cannot fail on any commit of this branch. A probe with the fonts registered but not added as
fallbacks draws nothing, which shows the check isn't vacuous (`/workspaces/logs/CU-C4/`, run in
place and removed).
Cost: no red run on this branch; the evidence is the probe.
Reverse: delete the file.

## CU-C4-D7. #1152 and #1150 need no engine change; #1151 none either

Decision: no Rust change for #1152 (`built-in.ts` reads `FACES[name].programHash`), #1150
(`LocalEngine` forwards `baseSha256`) or the engine half of #1151 (zod 4 refuses `Infinity`).
`host_sessions.rs` pins that a `layerBytes` open with `baseSha256` opens, and `wire_numbers.rs` that
a number past the largest double is refused whole as `WireFormat`.
Reason: `epdf-fonts`'s `build.rs` generates its faces from `faces.generated.ts`, so the hash it
names already is `FACES[name].programHash`. The host reads only the open fields it takes. JSON has no
infinite number (`JSON.stringify` writes `null`), so none reaches a Rust schema check.
Cost: on the TypeScript worker an in-process `Infinity` is an `InvalidArg` from zod; on the Rust wire
the nearest thing, `1e999`, is a `WireFormat` for the whole request. The plugin half
(`parseAnnotationDraft`) is CU-C6's.
Reverse: not applicable.

## CU-C4-D8. The host's mark wire is upstream's, and the payload-only shape is refused

Decision: `forms.setSignatureAppearance` on the Rust host reads `appearance` (a payload, or `null`
for no mark) or `pdf` (a payload, a PDF only), and refuses a request with neither with
`InvalidArg`, as `signatureAppearanceOfRequest` does. `epdf-viewer-host` sends the mark in the
spelling its caller chose. The change op's `appearance` is the mark's resource, `{ "pdf": resource }`
or `null`; `signatures.prepare`'s takes both spellings.
Reason: before, the host took payload 0 as a PDF and read nothing from the body. Upstream's worker
request now says which spelling the caller chose, and `{ pdf }` must refuse an image.
Cost: a caller that sends only payload 0 is now refused. In the repository that was
`epdf-viewer-host` and seven engine test calls, all updated.
Reverse: treat a body with neither key as `pdf`.

## CU-C4-D9. Imports and `completeFormBundle` are not recorded or ported

Decision: no engine case runs `forms.import`, and `codec::parse` does not complete an older bundle's
fields (`completeFormBundle`, `withPurposeAndFormat`).
Reason: an import route carries the bundle's resources as payloads the change generator's step kinds
don't model, and the copy cases exercise the same `fieldDraftOf` (`field_draft_of`). The Rust import
reads each field row as JSON: a row with no `purpose` gets none, and a format is read from the row's
scripts, which is what completing the row would give it. No Rust caller reads the fields of a parsed
bundle (`codec::parse` is called from tests only).
Cost: the upstream import test (`an import keeps a purpose and a format, with form design alone`)
has no Rust replay, and a wrapper that someday shows a parsed older bundle's rows would see no
`purpose` member.
Reverse: add an import step kind to the generator, and a `complete` pass over form rows in
`codec::parse`.

## CU-C4-D10. A red run builds in a worktree that shares the target dir

Decision: `scripts/cu-c4/red.sh` runs the job's new tests and vectors against an earlier commit in a
temporary worktree with `CARGO_TARGET_DIR=/workspaces/target`, then touches the job's sources.
Reason: a separate target dir means a full rebuild of every dependency. Cargo hashes a path package
by its path inside the workspace, so the temporary worktree's build writes the same artifacts as
the job's own; the first red run left `epdf-engine` linking an old `pdfium` until the sources were
touched (`image_document` missing).
Cost: the job's next build after a red run rebuilds the workspace crates.
Reverse: give the red worktree a target dir of its own.

## CU-C4-D11. The plugin cross-check's fixtures gain the two new members

Decision: `crates/plugins/form/tests/engine_cross_check.rs`, in CU-C5's crate, gives its fixture
fields `purpose: null`, and its text and dropdown fields `format: null`. No plugin source changed.
Reason: the test compares the plugin's prediction of a fixture field with the engine's
(`epdf_engine` `field_after_value`, `field_after_reset`, `form_field_after`). The engine's DTO now
writes both members, so a fixture built without them failed 3 tests that CU-C3's list didn't have.
Upstream's `FormField` has both members, so the fixture now matches what a read gives.
Cost: an edit in a crate the work order routes to CU-C5; CU-C5 may change the same file when the
plugin's own rows carry purpose and format.
Reverse: revert `82d3476a2`.
