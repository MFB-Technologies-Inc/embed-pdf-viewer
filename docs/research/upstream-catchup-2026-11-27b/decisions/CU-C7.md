# CU-C7 decisions

Decisions made while exposing anyone fields (#1163) and picture buttons (#1165) through
`epdf-web`, `epdf-uniffi` (Kotlin) and `epdf-capi` and the .NET viewer, on
`research/catchup-2026-11-27b-platforms` from `0c6097639`.

## CU-C7-D1. A worktree of its own on the shared target dir; edits and commits on the host

Decision: the container worktree is `/workspaces/wt/CU-C7` (detached at `0c6097639`, then at each
commit by bundle), with `packages/engine/runtime/build/libpdfium` copied from `/workspaces/wt/CU-C2`.
Arm64 cargo builds use `/workspaces/target`, CU-C2's to CU-C6's target dir. Source edits were made
in the host worktree `.claude/worktrees/catchup-1127b-platforms` and copied in with
`scripts/cu-c7/sync.sh`; `cargo fmt` and the PublicAPI update came back with `pull.sh`. Commits were
made on the host, as text only; every build and test ran in a container or, for the osx and
emulator rows, on the Mac.
Reason: CU-C6 closed, so the target dir has no other user, and a workspace member at a new path
gets its own fingerprint, so the two checkouts don't take each other's builds as fresh. A copy of
the 22 GB dir would have bought nothing.
Cost: CU-C2's next build in `/workspaces/target` recompiles the crates this job touched.
Reverse: `git worktree remove /workspaces/wt/CU-C7`.

## CU-C7-D2. `set_button_image` is a `FormHandle` export over the engine's `apply`

Decision: `FormHandle::set_button_image(field_ref, image: Option<Vec<u8>>, cancel)`
(`crates/plugins/form/src/ffi/editing.rs`) sends one `forms.setButtonImage` op through the
document's `apply`, the picture as `{"$bytes"}`, and reads the change's one item as
`FormFieldUpdateResult { field, widgets }`; an item of another type is `Unknown`, as upstream's
`wroteNothing` is (`packages/engine/core/src/engine/formVerbs.ts`). No `Core` method is added, so the
form plugin's capability, harness and member order stay upstream's, and none of the three coverage
manifests gains a row: `scripts/check-api-coverage.py` inventories `Core` and `*Capability` methods
only. The form hears the write through the engine's `forms.updated`, which the viewer-host test
checks. The browser gets no new call: a page calls upstream's `doc.forms.setButtonImage` on the
engine.
Reason: the controller's ruling (engine-verb parity, no form plugin verb upstream). Going around
the plugin's change queue is what TS does when an app calls the engine verb.
Cost: the call is not in the plugin's undo history, and a coverage check would not notice it
leaving the FFI; each platform's test is its only guard. CU-C8 needs no Swift manifest row either.
Reverse: add `Core::set_button_image` in `crates/plugins/form/src/write/` staging the op, and a row
per manifest.

## CU-C7-D3. Kotlin runs in an amd64 container of this job's own

Decision: the Kotlin rows ran in `embed-pdf-1127b-x64`, a `linux/amd64` container from
`embedpdf-dev:20261127-94822b7a9e29` with the `embedpdf-catchup1127b` volume, so it works in the same
`/workspaces/wt/CU-C7`. Its cargo target dir is `/workspaces/target-x64`; kotlinc 2.2.21,
`jna-5.19.1.jar` and `kotlinx-coroutines-core-jvm-1.10.2.jar` are in `/workspaces/tools-1127b`
(`scripts/cu-c7/x64-setup.sh`).
Reason: as 2026-11-27 CU-C8-D3, the arm64 image has no Android SDK or NDK. `cu-c8-x64` belongs to
the other run.
Cost: every Kotlin build is emulated under Rosetta; the image's inputs are `94822b7a9`'s.
Reverse: remove the container and `/workspaces/target-x64`, `/workspaces/tools-1127b`.

## CU-C7-D4. The Android slices are 2026-11-27's local `9472117ed` build

Decision: `x64-setup.sh` unpacks `libembedpdf-runtime-android-{arm64,x64}-9472117ed.tar.gz` from
`~/repos/embed-pdf-runtime-builds/c1127-9472117ed/android/`, checked against the sha256 that
`packages/engine/runtime/engine-runtime-build.local.json` pins (`5157d722...`, `95b14e7c...`). The
linux-x64 runtime for the JVM tests is fetched with `curl` and checked against
`engine-runtime-build.json` (`29659621...`).
Reason: the head's `pdfium` crate calls `EPDFAnnot_GetMKTextPosition` and
`EPDFForm_GetFieldEmbedMetadataBoolean`, which `9472117ed` has (`public/epdf_annot.h`,
`public/epdf_form.h`); the pin is the same as at `0afd87825`. `fetch-libpdfium.sh` calls `node`.
Cost: the slices are a local build, not a release's.
Reverse: `fetch-android-runtime.sh` once the runtime is published.

## CU-C7-D5. `layout` is on `FormWidgetRecord`, not `FormWidget`

Decision: `FormWidgetRecord.layout: Option<FormWidgetLayout>` reads the widget annotation row's
`layout`; `FormWidget`, the field's own widget entry, is unchanged. `FormWidgetLayout` is a
seven-value enum; a name outside the seven reads as `None`, as
`a_layout_outside_the_seven_is_no_layout` reads it in the engine. Writes take it on
`FormCreateFieldInput.layout` (the first widget), `FormWidgetPlacement.layout` and, as before, the
open `update_widget` JSON patch.
Reason: CU-C3's table names `FormWidget` (`crates/plugins/form/src/ffi.rs:175`), but upstream keeps
`layout` on `WidgetAnnotation` (`packages/engine/core/src/annotation/kinds/widget/declaration.ts:67`);
a field's `widgets` entries (`FormFieldWidget`) carry only the address and page, so a member there
would always read `None`.
Cost: reading every widget's layout takes `widget()` or `field_widgets()`, not the field alone.
Reverse: move the member, and read it from the snapshot's widget rows.

## CU-C7-D6. `Pushbutton` gains `has_image`, a source break on every platform

Decision: `FormFieldDetail::Pushbutton` becomes `Pushbutton { has_image }`; `FormField` gains
`for_anyone` after `group_id`; `FormWidgetRecord` gains `layout` before `fields_json`. In C# the
positional records `FormFieldDetail.Pushbutton(bool HasImage)`, `FormField(...)` and
`FormWidgetRecord(...)` change their constructors and `Deconstruct`; `PublicAPI.Unshipped.txt` drops
5 lines and adds 43 (`scripts/cu-c7/public-api.py`). Kotlin's generated data classes change the same
way, and `embedpdf.api` is regenerated.
Reason: upstream reads `hasImage` on the push button DTO (`packages/engine/core/src/forms/field.ts:230`);
the detail enum is where each family's members live.
Cost: code that builds or deconstructs these records by position breaks; there is no released
user who does (D-1's ruling).
Reverse: keep the unit variant and put `has_image` on `FormField`.

## CU-C7-D7. No `*_SINCE` constant and no `epdf-capi` change

Decision: neither `BUTTON_IMAGE_AND_LAYOUT_SINCE` nor `ANYONE_FIELDS_SINCE` is exported, and
`epdf-capi` changes nothing.
Reason: no wrapper exposes any `*_SINCE` constant (`git grep _SINCE -- platforms crates/epdf-ffi
'crates/plugins/*/src/ffi*'` finds none), so the controller's condition is not met. `epdf-capi` has
seven fixed engine operations and no forms (`crates/epdf-capi/src/operations.rs:39-47`), as
2026-11-27 CU-C8-D9 found; its rows are run only as regressions.
Cost: none.
Reverse: not applicable.

## CU-C7-D8. The start's platform rows come from 2026-11-27 CU-C8

Decision: the `0afd87825` column takes 2026-11-27 CU-C8's head numbers (`81fac2a99`, with
`f9bda565e`'s tree for cargo), not a rerun. The .NET linux-arm64 row was rerun at `0afd87825` in
`/workspaces/wt/base` as a check (see the report).
Reason: `git diff --stat 81fac2a99 0afd87825 -- platforms` touches only Swift sources and the
Swift manifest, `platforms/android/embedpdf/gradle.properties` (the version) and
`EmbedPDF.Runtime.props` (the engine pin); no Kotlin or C# test or source changed. CU-C6 measured
cargo and `epdf-web` at `0afd87825` with the same numbers as `81fac2a99`.
Cost: the Kotlin and osx start rows are a day old and from another container and Mac session.
Reverse: rerun `kotlin.sh` and `dotnet.sh` at `/workspaces/wt/base`.

## CU-C7-D9. The osx rows build `epdf-capi` with cargo on the Mac

Decision: as 2026-11-27 CU-C8-D11, `dotnet.sh` ran on the Mac with `HOST_TESTS=0`, cargo building
`epdf-capi` for `aarch64-apple-darwin` and `x86_64-apple-darwin` into the job's scratch target dir.
.NET SDK 11.0.100 and runtime 10.0.15 for both architectures came from `dotnet-install.sh` into the
scratch dir, and the darwin runtimes from `curl`, checked against `engine-runtime-build.json`
(`scripts/cu-c7/mac-setup.sh`). osx-x64 ran under `arch -x86_64` with `/usr/bin/python3`.
Reason: the brief puts these two rows on the Mac and allows host cargo for Apple targets.
Cost: the scratch target dir and SDKs are deleted at cleanup.
Reverse: not applicable.

## CU-C7-D10. The linux-x64 .NET rows install SDK 11.0.100 in the amd64 container

Decision: `scripts/cu-c7/x64-dotnet.sh` installs .NET SDK 11.0.100 and runtime 10.0.15 with
`dotnet-install.sh` into `/workspaces/tools-1127b/dotnet-x64`, fetches the linux-x64 runtime with
`curl` against `engine-runtime-build.json`'s sha256, and runs `dotnet.sh` in `embed-pdf-1127b-x64`
with `EPDF_IMAGE_ID` naming the amd64 image (`sha256:9b30cd30bea5...`) and `ENGINE_CHECK=0`.
Reason: as 2026-11-27 CU-C8-D14, `platforms/dotnet/global.json` pins SDK 11.0.100 with
`rollForward: disable` and the amd64 image has 10.0.404. The engine check only reads package
versions and needs `gh`, which the containers lack; the Mac gives its verdict.
Cost: the SDK is not the image's, and the row is emulated, not native.
Reverse: rebuild the amd64 image from the catch-up's merge.

## CU-C7-D11. Two Kotlin rows needed the environment the 2026-11-27 runs had

Decision: `SignatureRoutesTest` (3 tests) was skipped in the first JVM run, since
`/workspaces/wt/CU-C7` had no built `packages/core/signature/dist`; it was rerun alone with
`EMBEDPDF_SIGNATURE_ROUTES_REQUIRED=1` after copying CU-C2's built `dist` (no file under
`packages/` differs between the two checkouts) and a `pnpm install --frozen-lockfile` in the
container. On the emulator, `DeviceKeyStoreTest.aKeyThatNeedsTheUserIsPermissionDeniedUserNotAuthenticated`
needs a lock-screen credential set more than 30 seconds before it runs: the first run failed with
`Secure lock screen must be enabled`, a second with the PIN just set signed with the 30 second key.
The `embedpdf` module was rerun with a PIN set 45 seconds ahead (`locksettings set-pin`), then
cleared.
Reason: both are properties of the run environment, not of the code: the test files and the
Kotlin they reach are unchanged in this job, and `turbo run build` of `core-signature` in the fresh
worktree stops at `@embedpdf/engine`, which needs the wasm32 payload this worktree doesn't have.
Cost: the JVM count is two runs' sum, and the device count takes `compose` and `javascript` from
the first run and `embedpdf` from the third.
Reverse: prep the worktree with `scripts/prep.sh` and keep the AVD's lock screen set.

## CU-C7-D12. The linux-x64 Jint recursion failure is counted as the start's

Decision: `JintScriptEngineTests.ADeepRecursionIsABudgetFailure` failed once in the head's
linux-x64 run (net11.0) and failed in all 6 runs of `JintScriptEngineTests` alone at the head and at
`0afd87825` (built in `/workspaces/wt/base` with `/workspaces/target-base-x64`, logs in
`/workspaces/logs/CU-C7/dotnet-base-linux-x64-jint/`); the row is reported as no worse than the
start.
Reason: the failure (`The script sandbox has been disposed`, `operation-failed`) is the same at both
commits, and nothing between them touches scripting: `git diff --stat 0afd87825 HEAD` over
`crates/plugins/actions`, `crates/plugins/form/src/scripting`, `crates/epdf-ffi` and the Jint and
script-engine C# is empty, and `crates/epdf-viewer-host/src/native_engine.rs` changes only the
anyone-field fill check. The test passes on linux-arm64 at both ends.
Cost: the linux-x64 row is not green, and why the emulated deep recursion now ends the sandbox,
where 2026-11-27 CU-C8's run passed it, is not known.
Reverse: not applicable; a fix belongs to the Jint sandbox's recursion budget.
