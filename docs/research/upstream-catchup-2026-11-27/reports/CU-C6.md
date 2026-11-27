# CU-C6: the annotation port and the payload build

Every item CU-C3's work order gives CU-C6 is done. The 4 failing tests it routed here pass, and
with them all 49 replayed cases in `core/annotation-kinds.json` (30), `core/annotation-update.json`
(1), `plugins/annotation-pure.json` (10) and `plugins/annotation-repository.json` (8).
`cargo test --workspace` has no failures (3,616 passed), `replay:check` replays all 16,076 cases in
124 files, and the 20 uncovered `core/annotation` and `plugin/annotation` lines the range added are
covered by 27 new recorded cases, each red on the code before the port. The Rust-core payload
builds as `wasm32-eh` with all 1,070 bound functions matching. Job D's three review Minors and
CU-C4's clippy warning are the first two commits. Decisions are in
[`../decisions/CU-C6.md`](../decisions/CU-C6.md).

## Commits

On `research/catchup-2026-11-27-c6` from `053b0a901` (CU-C6-D1).

| Commit | Item | Summary |
|---|---|---|
| `4c12a736a` | Minors 1 to 3 | `format_date` makes each time part whole (`MakeTime`); the form harness reads a `takesCursor` answer of `undefined` or `null` as taking the cursor; a `listSelectedFields` scenario and a `takesCursor` scenario in `form-groups.ts`, a fractional-time case in `acrojs.ts` |
| `17ba6fcbd` | CU-C4 clippy | `font_fallback_named.rs:127` `*(&mut job) += 1` is `job += 1` |
| `e859e3112` | 3 | `parseAnnotationDraft` writes zod 4's issues; an infinite number is refused (CU-C6-D2) |
| `ef50e73bf` | 1, 2 | `epdf-annotation-core`: widget as a turned box, `quarterTurns`, `standingRect`, `orientationPatch`, `ORIENTATION`, `placedBoxFields`, `PageBoxes` and `keptOnPages`, the widget raster, `RecordAccent` |
| `cf2dee995` | 1, 2, CU-C5-D5 | `epdf-plugin-annotation` and `epdf-plugin-form`: `accentOf` on the lender and in the view's lent cache, Orientation's value, page boxes on `rotateBy` and `resetRotation`, the drag preview's display rotation, the core's `standingRect` and `placedBoxFields` in the place handler |
| `0f776f4f0` | 1, 2 | New cases and their vectors, the Rust harness's `standingRect`, `placedBoxFields`, `orientationPatch` |
| `76c340cf9` | | `coverage.json`, and `source-evidence.json` pins `suites/acrojs.ts` anew |
| `88707a760` | 1 | The kept-on-page turn answered as written, listed in `historical/catchup-2026-11-27/additions.json` (CU-C6-D6) |
| the docs commit | | This report, the decisions, `scripts/cu-c6/` |

## The three Minors

1. `crates/plugins/acrojs/src/dates.rs`: `make_time` takes `ToIntegerOrInfinity` of each part and
   is NaN for a non-finite one. The new case `dates/format-fractional-time`
   (`setUTCHours(14.5, 7.9, 9.99, 0)`) read `14:38:03` before and reads `14:07:09` now
   (`minors-red.log`, `minors-green.log`).
2. `selected-widgets-name-their-fields-once` selects widget 102 of `one`, a square, and widgets
   101 and 104 of `pair`, then reads `["one", "pair"]`. It passed on its first run (CU-C6-D3).
3. `crates/plugins/form/src/harness/mod.rs`: `Answer::Returns` of `null` or `undefined` takes the
   cursor. `a-target-whose-takes-cursor-says-nothing-takes-it` failed at step 5 before
   (`sign.takesCursor` called where `a.takesCursor` was expected, `minors-red-form.log`).

## Items

1. Upright fields (#1118): done. A widget is a `TURNING_KINDS` box: it reads `box` and `rotation`,
   or its `rect` unturned while it has no `box`, and writes `box` and `rotation`, so a widget
   create drafts as `box` (the 8 repository cases). An upright tool places it with its turn
   (`READS_ONE_WAY`). `box_rect` is `standing_rect` for a box that draws inside itself, which fixes
   `runs/edit-3-edit-move-group-commit`. `quarterTurns` is on every widget kind (the 30 kinds
   cases); `rotate_selection` and `reset_rotation` take a kind with it through `takes_turn` and
   take `PageBoxes`, moving what turned back onto its page by `clamp_delta_to` over
   `union_bounds_of` (`keptOnPages`). `with_orientation` puts `ORIENTATION` before the flags when
   every selected kind turns by quarter turns. `orientation_patch` and `placed_box_fields` are in
   the core and re-exported from the plugin.
2. The group accent (#1141 annotation half): done. `RecordAccent` (`{ color, strength }`) is on
   `ModelAnnotation` and `RenderItem`, printed after `lentScene`. `AnnotationLender::accent_of`
   defaults to `None`; the form lender answers it from `group_look_of`, and its records memo is
   keyed on `list_groups()` and the tint too. The view caches a lent record per accent
   (`same_accent`). CU-C5's `{ color, strength }` JSON is parsed into the core type, so there is
   one shape.
3. zod 4 (#1151 plugin half): done, reproduced rather than excepted (CU-C6-D2).
4. i18n: nothing to port (CU-C6-D4).
5. Build (#1125, #1143, #1155): the Rust-core payload builds as `wasm32-eh`
   (`payload-wasm32-eh/payload.log`): `verify-wasm-abi.mjs` reports all 1,070 bound functions
   matching, `verify-rust-core.mjs` reports the core present, and `generate-functions.mjs` with
   #1143's `render-function-table.mjs` leaves `pdf-functions.generated.ts` unchanged. The plain
   `wasm32` target does not link with the core (`__cpp_exception`, CU-C6-D9). `packages/core-wasm`
   does not bundle `packages/engine/main`'s worker: `src/` imports no `@embedpdf/engine`, which is
   a devDependency its tests use for `createLocalEngine`.

CU-C5-D5's three pieces are in `cf2dee995`: the lender's accent, the drag preview's display
rotation (the uniffi `preview_placement` passes none yet, CU-C6-D5), and the core's
`standing_rect` in `tools/handlers.rs`.

## TDD

The routed replays were red at the start (`base-anno.log`: 2 tests in each crate, 49 cases). The
new cases, replayed against `e859e3112` (before the port) by `scripts/cu-c6/red.sh`, all differ:
8 `standingRect` and 9 placement cases (no Rust function), the accented `pageItems` case (no
`accent`), the rotation runs (`runs/rotate90-28-rotate90-single`), the two `form-groups` scenarios
(no accent at step 3, no `displayRotation` at step 4), and the `annotation-api` turn (no moved
`box` at step 3) (`red-new-cases-at-e859e3112/summary.txt`, `red-api-at-e859e3112/summary.txt`).
All pass at head (`new-cases-green.log`, `api-green.log`).

## Tests

All in container `embed-pdf-catchup1127`, worktree `/workspaces/wt/CU-C6`,
`CARGO_TARGET_DIR=/workspaces/target-c5`, at `88707a760`.

| Check | Start (`053b0a901`, CU-C5's record) | Now |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,612 passed, 4 failed, 2 ignored | 3,616 passed, 0 failed, 2 ignored |
| engine `:check` scripts | 36 of 36 | 36 of 36 |
| plugin vectors regenerated twice | | 128 files, identical, no vector diff |
| `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | 4 of 4 | 4 of 4 |
| `replay:check` | 49 cases in 4 files fail | 0 fail, 16,076 cases in 124 files |
| evidence | 769 of 771 | 769 of 771, `ev/summary.txt` byte-identical (`6290cd61`) |
| `cargo clippy --all-targets`, 5 touched crates | 1 warning | 0 |
| uncovered lines the range added | 37 | 13, all `plugin/form` |
| Rust-core payload | not built | `wasm32-eh` built, ABI and core checks pass |

The first after-suite (`after1`) had 735 of 771 evidence passes: the first version of the
`annotation-api` turn was refused, and `annotation-live.test.ts` pins every scenario's refusals in
a preserved file. `88707a760` answers it as written (CU-C6-D6); `after2` is the record above. The
2 evidence failures are `current-suites.test.ts`, as at the start.

## Coverage gaps

`coverage.json` reads statements 79,276 of 83,439 and branches 15,750 of 17,297 (CU-C5: 79,252
and 15,712). `gaps.py` finds 13 uncovered lines the range added, all in `plugin/form` (CU-C5's 17
less the four `listSelectedFields` lines at `read/fields.ts:226-229`). `core/annotation/src/types.ts`
and `plugin/annotation/src/index.ts` stay unloaded: types and re-exports.

## Evidence

Logs in `/workspaces/logs/CU-C6/` in the container. Scripts in [`../scripts/cu-c6/`](../scripts/cu-c6/).
sha256, first 8 hex digits.

| Check | Command | Exit | Log | sha256 |
|---|---|---|---|---|
| Start, annotation crates | `t.sh base-anno -- cargo test --no-fail-fast -p epdf-annotation-core -p epdf-plugin-annotation` | 101 (the 4) | `base-anno.log` | `7469119d` |
| Minors, red | `--test conformance` for `epdf-acrojs`, then `epdf-plugin-form` | 101, 101 | `minors-red.log`, `minors-red-form.log` | `c87d77db`, `bfaafa1f` |
| Minors | `cargo test --no-fail-fast -p epdf-acrojs -p epdf-plugin-form` | 0 | `minors-green.log` | `8f673552` |
| Clippy fix | `cargo clippy -p epdf-engine --test font_fallback_named -- -D warnings` | 0 | `clippy-font.log` | `137a10e4` |
| zod 4 | `cargo test -p epdf-plugin-annotation --test conformance pure_functions` | 0 | `zod-green.log` | `dbe8ff14` |
| Core port | `cargo test --no-fail-fast -p epdf-annotation-core` | 0 | `core1.log` | `ede9441d` |
| Plugin port | the five touched plugin crates | 0 | `step1.log` | `bb5af803` |
| Workspace compiles | `cargo check --workspace --all-targets` | 0 | `check-ws.log` | `e8dcff01` |
| New cases, red | `red.sh e859e3112 0f776f4f0 new-cases-at-e859e3112 …` | 0 (script) | `red-new-cases-at-e859e3112/summary.txt` | `732b8aa9` |
| Turn case, red | `red.sh e859e3112 88707a760 api-at-e859e3112 …` | 0 (script) | `red-api-at-e859e3112/summary.txt` | `ec2e8c5f` |
| New cases | four crates' tests | 0 | `new-cases-green.log`, `api-green.log` | `0824b3ea`, `de65ad71` |
| Clippy | `cargo clippy -p epdf-annotation-core -p epdf-plugin-annotation -p epdf-plugin-form -p epdf-acrojs -p epdf-engine --all-targets` | 0 | `clippy.log` | `fc612378` |
| Payload `wasm32` | `payload.sh … payload-wasm32 wasm32` | 1 (link) | `payload-wasm32/payload.log` | `b760ba5d` |
| Payload `wasm32-eh` | `payload.sh … payload-wasm32-eh wasm32-eh` | 0 | `payload-wasm32-eh/payload.log`, `verify-rust-core.log` | `1d5a8bfd`, `0fca4c73` |
| Plugin regeneration x2, coverage | `regen-plugins2x.sh … regen1` | 0 | `regen1/determinism.log` | `1fed386b` |
| Coverage gaps | `gaps.py /workspaces/wt/CU-C6` | 0 | `coverage-gaps.txt` | `285220ea` |
| After suite | `heavy-build.sh CU-C6 -- docker exec … after.sh /workspaces/wt/CU-C6 after2` | 0 (script) | `after2/cargo.log`, `cargo.summary` | `f94d6455`, `40bf05a1` |
| Engine `:check` | in `after2` | 0 | `after2/engconf.all` | `27a10bbd` |
| Plugin checks, replay | in `after2` | 0, 0 | `after2/plugconf.all`, `plugconf.replay:check.log` | `cf0bfd16`, `e53f2fdb` |
| Evidence | in `after2` | | `after2/ev/summary.txt` | `6290cd61` |

## For CU-C7, CU-C8 and CU-C9

CU-C7: the Rust-core payload is `wasm32-eh` (CU-C6-D9); the built one is kept at
`/workspaces/logs/CU-C6/payload-wasm32-eh/rust-core-lib/`. CU-C8 and CU-C9: the FFI has no
`display_rotation` on `preview_placement` and no `accent` on records (CU-C6-D5); the Rust surface
is `rotate_selection`/`reset_rotation` with `PageBoxes` (reached through `selection.rotateBy` and
`resetRotation`), `orientation_patch`, the Orientation property, and `RecordAccent`.

## Known gaps

13 upstream-added `plugin/form` lines remain uncovered. `/workspaces/target-c5` and the bundle at
`/workspaces/bundles/catchup-2026-11-27-c6.bundle` stay for CU-C11's cleanup, as does the host
editing worktree `.claude/worktrees/c6-edit`.
