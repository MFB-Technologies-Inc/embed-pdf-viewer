# CU-C6 decisions

Decisions made while porting the annotation half of upstream `c4e744345..9af4d9ee7` and checking
the payload build (Job E), on `research/catchup-2026-11-27-c6` from `053b0a901`.

## CU-C6-D1. The branch reuses CU-C5's target dir, and edits come in from a host worktree

Decision: `research/catchup-2026-11-27-c6` was made with `git worktree add -b` at `053b0a901` in
`/workspaces/wt/CU-C6` and prepped with `/workspaces/scripts/prep.sh`. Builds use
`/workspaces/target-c5`, CU-C5's target dir, as it was. Source edits were made in a detached host
worktree at `053b0a901` (`.claude/worktrees/c6-edit`) and copied into the container worktree with
`tar` as the container user, with fresh mtimes; every build, test, format and commit ran in the
container. Logs are in `/workspaces/logs/CU-C6/`, scripts in `/workspaces/scripts/cu-c6/`
(committed as `scripts/cu-c6/`).
Reason: the brief allows reusing `target-c5` now that CU-C5 is done, and a copy would cost 31 GB and
a cold build. The host worktree only holds text: no node, pnpm or cargo ran on the host. A first
`docker cp` kept the host mtimes, which were older than the last build, so cargo did not rebuild;
the `tar -m` copy fixed that, and the first green run after it is the one recorded.
Cost: `target-c5` now holds CU-C6's builds, so CU-C5's tree no longer builds incrementally from it.
Reverse: `cp -a /workspaces/target-c5 /workspaces/target-c6` and point `scripts/cu-c6/*.sh` at it.

## CU-C6-D2. The port reproduces zod 4's issue text

Decision: the harness's `parseAnnotationDraft` (`crates/plugins/annotation/src/harness/functions.rs`)
writes zod 4.6.7's issues: `invalid_type` with `expected`, `code`, `received` (`"NaN"`,
`"Infinity"` or `"-Infinity"`), `path` and `message` ("Invalid input: expected number, received
NaN"), and `too_small` with `origin`, `code`, `minimum`, `inclusive`, `path` and `message` ("Too
small: expected number to be >=0", "Too small: expected array to have >=3 items"). A non-finite
number is refused, so `line-infinite` throws. The `ZodError` carries its name and message only.
Reason: this is the harness's stand-in for the engine's draft schema, not a Rust validator that a
host calls, so matching zod costs two short functions; the alternative, an outcome exception, would
leave 10 cases red on purpose. The key order and the English text come from
`zod@4.6.7/v4/core/schemas.js:645-658` (`$ZodNumber`) and the recorded cases.
Cost: the next zod text change shows up as replay failures again.
Reverse: record the 10 cases as an outcome exception and keep zod 3's text.

## CU-C6-D3. Minor 2 is coverage, not a fix, and has no red run

Decision: the `listSelectedFields` scenario (`selected-widgets-name-their-fields-once` in
`plugins/form-groups.json`) selects widget 102 of `one`, a square, and widgets 101 and 104 of
`pair`, and reads `["one", "pair"]`. It passed on its first run against the Rust code. Minors 1 and
3 each have a red run (`minors-red.log`, `minors-red-form.log`).
Reason: the review asked for a case, not a change; the Rust `list_selected_fields`
(`crates/plugins/form/src/read/fields.rs:375`) already deduplicated, skipped non-widgets and kept
the selection order.
Cost: none beyond the case.
Reverse: not applicable.

## CU-C6-D4. i18n has nothing to port

Decision: no change to `epdf-plugin-i18n`. `git diff --stat c4e744345 9af4d9ee7 --
packages/plugin/i18n` shows only `CHANGELOG.md` and `package.json`. The keys #1118 added
(`rotateClockwise`, `rotateCounterclockwise`, `orientation`, `orientations`, `group`) live in
`packages/viewer/chrome/src/locales/*.ts`, which the Rust port does not carry. The core's
Orientation property names its own `labelKey`s (`annotation.style.orientation`,
`annotation.orientations.0` to `.270`) as upstream does.
Reason: the work order's item 4 says the same; checked again on the range.
Cost: none.
Reverse: not applicable.

## CU-C6-D5. The FFI surface stays as it was

Decision: `epdf-plugin-annotation`'s uniffi `preview_placement` (`src/ffi/drafts.rs`) keeps its
four arguments and passes no display rotation; no FFI record gains `accent`, `orientation` or
the page boxes. `AnnotationLender::accent_of` has a default of `None`, so a lender that is not the
form plugin's keeps compiling unchanged.
Reason: the work order's wrapper table gives the platform exposure of widget quarter turns to
CU-C8 and CU-C9; adding an argument to a uniffi method breaks the Swift, Kotlin and .NET callers
before those jobs update them, as CU-C5-D4 found for the focus target.
Cost: until CU-C8, a native host's drag on a turned page previews the field upright (its commit,
by the form plugin, is placed right), and native hosts cannot read the accent.
Reverse: CU-C8 adds `display_rotation: Option<f64>` to the FFI `preview_placement` and the new
fields to the records.

## CU-C6-D6. The new cases go into the existing files

Decision: the new core cases join `core/annotation-geometry-basics.json` (8 `standingRect`),
`core/annotation-placement.json` (2 `placedBoxFields`, 7 `orientationPatch`),
`core/annotation-view.json` (1 `pageItems`) and the rotation runs of `core/annotation-update.json`
(3 runs). The plugin cases join `plugins/annotation-api.json` (1) and `plugins/form-groups.json`
(4, with Minors 2 and 3; `every_form_case_replays` now pins 14 and 244). The form drag is answered
with a refusal; the annotation turn is answered as written, and the case is listed in
`historical/catchup-2026-11-27/additions.json`.
Reason: each function's cases sit beside its siblings', so a failure points at its upstream file.
The drag's case pins the turned frame in the `forms.create` op the engine is sent, which a refusal
leaves in the trace. The turn could not be refused: `annotation-live.test.ts` runs every
`annotation-api` scenario against `test/annotation-live-refusals.ts`, a file whose sha256 is pinned
in `historical/task-12b2b1b-ii/family.json` and nine preservation records, so a new refusal there
would mean re-pinning archived evidence. A first version that refused both turns failed that test
(after1: 34 failures) and was replaced in `88707a760`.
Cost: the drag's accepted path is covered only by the existing `a-tool-places-upright-in-the-design-group`.
Reverse: answer the drag with `apply(fieldAnswer(...))`.

## CU-C6-D7. `PageBoxes` is keyed by the page number as JavaScript prints it

Decision: `PageBoxes` is `IndexMap<String, Rect>` (`crates/plugins/annotation-core/src/types.rs`),
keyed by `js::number_to_string(objectNumber)`; `keptOnPages` groups ids by that key in first-seen
order and looks the page's box up by it.
Reason: upstream's `PageBoxes` is a `Record<number, Rect>`, so its keys are strings once it is JSON;
the message arrives as JSON in the harness and in the plugin.
Cost: a page number that is not an integer would print with a fraction, as it does in JavaScript.
Reverse: key by `u32` and parse the keys on read.

## CU-C6-D8. Prettier only where the file was clean

Decision: `prettier --write` ran on `annotation-api.ts`, `annotation-core-model.ts` and
`form-groups.ts`, which were prettier-clean at `053b0a901`; `acrojs.ts` was not clean there and
was left as it was.
Reason: formatting a file that was already off would add unrelated diff.
Cost: none.
Reverse: not applicable.

## CU-C6-D9. The Rust-core payload is the `wasm32-eh` build, and the suites ran on prep's payload

Decision: the Rust-core payload was built with `build-target.sh wasm32-eh` (emsdk at
`/opt/emsdk-eh`, the archive pinned in `/workspaces/wasm32-eh-pin.json`), as the 2026-11-18
CU-C7 built it (`../../upstream-catchup-2026-11-18/reports/CU-C7.md:47` and `:108`).
`build-target.sh wasm32` with the Rust core does not link: `wasm-ld` reports
`undefined symbol: __cpp_exception` from `libepdf_wasm.a` (`payload-wasm32/payload.log`). The
built payload was copied to `/workspaces/logs/CU-C6/payload-wasm32-eh/rust-core-lib/`, and
`npm/wasm32/lib` was put back to the payload prep made, before the vector regeneration and the
after-suites.
Reason: the Rust crates are compiled with WebAssembly exception handling, which only the `-eh`
PDFium archive and emsdk link against; that is unchanged by #1125 and #1143, which touch the
scripts both targets share. Prep's payload is what CU-C5's after-suite ran on, so the numbers stay
comparable.
Cost: none of this job's suites ran on the Rust-core payload; CU-C7 runs the `core-wasm` and
`epdf-web` rows on it.
Reverse: leave the Rust-core payload in `npm/wasm32/lib` and rerun the after-suite.
