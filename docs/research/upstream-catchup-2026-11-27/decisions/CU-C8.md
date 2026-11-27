# CU-C8 decisions

Decisions made while exposing the catch-up's Rust surface through the browser build, Kotlin and
.NET (Job G), on `research/catchup-2026-11-27-platforms` from `e90da2a3b`.

## CU-C8-D1. A worktree and target dir of its own; edits come in from a host worktree

Decision: the branch was made with `git worktree add -b` at `e90da2a3b` in `/workspaces/wt/CU-C8`
and prepped with `scripts/cu-c7/prep.sh` from `/workspaces/wt/CU-C7`'s libraries and payload. Arm64
cargo builds use `/workspaces/target-c8`, a `cp -a` of `/workspaces/target-c5`. Source edits were
made in a detached host worktree (`.claude/worktrees/c8-edit`) and copied in with `tar -m` as the
container user (`scripts/cu-c8/sync.sh`); `cargo fmt` output came back the other way (`pull.sh`).
Every build, test and commit ran in a container. Logs are in `/workspaces/logs/CU-C8/`.
Reason: the brief asks for a worktree and target dir of the job's own, and CU-C7's tree is the
start, so its prep and target were the cheapest warm start. Editing on the host keeps the editor
tools; nothing Node, Rust or .NET ran there for these steps.
Cost: 41 GB more on the volume until CU-C11. A forced checkout of the host worktree once dropped
uncommitted C# edits, which were reapplied from saved scripts (`scratchpad`, not committed).
Reverse: `rm -rf /workspaces/target-c8`, `git worktree remove /workspaces/wt/CU-C8`.

## CU-C8-D2. The record fix labels both coverage rows and the two prose places that cite them

Decision: Job F's re-review Minor 1 is applied to the two evidence rows it names
(`reports/CU-C7.md:119-120` after the rewrap), to the table row at `reports/CU-C7.md:52`, to the
basis sentence at `reports/CU-C7.md:28-34`, and to `decisions.md:13-19`. Minor 2 rewraps the two
paragraphs that held lines 4, 7 and 29 to 100 columns. Commit `8e55095ad`.
Reason: the table row and the basis sentence named the same two runs with the old labels, so fixing
only the evidence rows would leave the report saying two things.
Cost: two more lines touched than the Minor listed.
Reverse: `git revert 8e55095ad`.

## CU-C8-D3. Kotlin runs in an amd64 container from the existing amd64 dev image

Decision: the Kotlin rows (`test-command-context.sh`, the Gradle JVM tests, `apiCheck`,
`build-aar.sh`) ran in `cu-c8-x64`, a `linux/amd64` container from
`embedpdf-dev:20261127-94822b7a9e29` (`9b30cd30bea5`, the image the Android parity job built from
`94822b7a9`) under Rosetta, with the `embedpdf-catchup1127` volume mounted, so it works in the same
`/workspaces/wt/CU-C8` worktree. Its cargo target dir is `/workspaces/target-c8-x64`
(`/workspaces/target-base-x64` for the start). kotlinc 2.2.21, `jna-5.19.1.jar` and
`kotlinx-coroutines-core-jvm-1.10.2.jar` are in `/workspaces/tools`.
Reason: the catch-up's arm64 image has no Android SDK or NDK: `.devcontainer/Dockerfile:95` and
`:122` omit both on arm64, because Google ships no Linux arm64 host tools. Gradle's Android library
plugin needs the SDK even for JVM unit tests, and the AAR needs NDK r29's `linux-x86_64` toolchain.
The amd64 image has both, so the AAR and its 16 KB checks ran in a container, as the brief asks,
where 2026-11-18 ran them on the Mac (CU-C8c-D6).
Cost: the amd64 image's inputs are `94822b7a9`'s, not `d670a1cbf`'s (its `Dockerfile` and
`check-pins.py` differ by 100 lines; Rust 1.100.0 and JDK 21.0.13 are the same). Every Kotlin build
is emulated and several times slower.
Reverse: rebuild the image for amd64 from the catch-up's merge with `.devcontainer/build.sh` and
rerun `scripts/cu-c8/kotlin.sh`.

## CU-C8-D4. The Android slices are built locally at runtime `9472117ed`

Decision: `libembedpdf-runtime-android-arm64-9472117ed.tar.gz` (sha256 `5157d722...`) and
`android-x64` (`95b14e7c...`) were built in a temporary `linux/amd64` `debian:trixie` container
(`cu-c8-android`) under Rosetta with the fork's `build-target.sh` and `package-target.sh`,
`scripts/cu-c8/slices-setup.sh` and `slices.sh`. They were unpacked straight into
`packages/engine/runtime/build/libpdfium/android-{arm64,x64}`, and
`packages/engine/runtime/engine-runtime-build.local.json` takes their sha256, as `162e160b7` did for
`af480d0ce`. The start's rows use the `af480d0ce` slices from
`~/repos/embed-pdf-runtime-builds/c1118-af480d0ce/android/`.
Reason: the `af480d0ce` slices export `EPDFAnnot_GetStampDrawing`, which CU-C4-D2's `pdfium` crate
no longer calls; the renamed calls exist only from `9472117ed`. `fetch-android-runtime.sh` downloads
`runtime-<sha>` releases, and `9472117ed` is unpublished until CU-C10 (D-3). The first build attempt
failed in `apply-patches.sh` for want of `patch`; the setup script now installs it.
Cost: the slices are this Mac's build, not a release's; CU-C10 publishes the real ones.
Reverse: `git checkout e90da2a3b -- packages/engine/runtime/engine-runtime-build.local.json` and
`fetch-android-runtime.sh` once the release exists.

## CU-C8-D5. FFI names for upstream's members

Decision: upstream's `box` is `frame` on `FormFillItem` and `FormWidgetHit` (`box` is a Rust
keyword); `bounds` stays the rect the widget stands in (CU-C5-D4). `listGroups`, `getDesignGroup`,
`listSelectedFields`, `getWidgetAnchor` are `groups`, `design_group`, `selected_fields`,
`widget_anchor`, after the handle's existing `focused_field` and `selected_field`. `RecordAccent`
(`{ color, strength }`) lives in `epdf-ffi`, so the form's fill item and the annotation record share
one type. One `FormFieldFormat` enum (`Date`, `Time`, `Other`) serves reads and writes; the engine
refuses a write of `Other` with `invalid-input`, as upstream's `checkedFormat` does.
`FormFieldPatchKey` gains `Purpose`, `Format` and `GroupId` for clearing. `fields.tint` that is not
finite is refused at the FFI with `invalid-input`, as the duplicate offset is, since JSON would
carry it as `null`.
Reason: keep each name a platform reads close to the capability member and to what the handle
already calls its siblings.
Cost: `frame` is not upstream's word; the Swift, Kotlin and C# docs say it is `box`.
Reverse: rename in `crates/plugins/form/src/ffi.rs` and the three platforms.

## CU-C8-D6. The focus target trait takes the source break; C# keeps a default

Decision: uniffi's `FormFocusTarget` now has `takes_cursor() -> bool` and `focus(select) -> bool`
(CU-C5-D4's reverse), and the native target is the plugin's `FocusTarget` with both. Kotlin
implementers must write both. The C# `IFormFocusTarget` gets `bool TakesCursor() => true` as a
default interface member and `bool Focus(bool select)`; a throw from either counts as `true`, so
`AThrowingFocusTargetLeavesFocusAnswering` keeps its answer.
Reason: Tab passing over a signature field nobody can sign needs the target to say so; the platforms
have no users (D-1's ruling).
Cost: a source break for every Kotlin and C# target, and for Swift in CU-C9.
Reverse: give the trait back its one `focus(select)` and build `FocusTarget::new`.

## CU-C8-D7. The new capability members get coverage rows in all three manifests

Decision: the nine new form members are added to `platforms/dotnet/csharp-api-coverage.json` and
`platforms/android/kotlin-api-coverage.json` with their C# and Kotlin mappings, and to
`platforms/apple/swift-api-coverage.json` with the uniffi export and `"pending": "CU-C9"`
(`scripts/cu-c8/coverage-rows.py`).
Reason: the Kotlin check requires its manifest's members to be the Swift manifest's, so a Kotlin row
needs a Swift row. The Swift mapping is CU-C9's.
Cost: `check-api-coverage.py --platform swift` without `--allow-pending` now reports 9 `incomplete
mapping` lines instead of 9 `unaccounted Rust member` lines; it failed at `d250a68e3` and at
`e90da2a3b` already (7 older unaccounted members). CU-C9 maps them.
Reverse: drop the nine rows from the three files.

## CU-C8-D8. A lent record's accent is read from the model record

Decision: `AnnotationRecord.accent` is `ModelAnnotation.accent` of the record the model holds for
the ref (`crates/plugins/annotation/src/ffi/mod.rs`, `accent_of`), not a member of the object `get`
and `list` answer.
Reason: upstream keeps the accent off the record itself (`types.rs:366-368`): `get` returns the
annotation object, so a first version that read `dto.accent` was always `None`, which the
viewer-host test showed.
Cost: one model lookup per record read.
Reverse: read nothing, and drop the member.

## CU-C8-D9. `epdf-capi` and the browser bindings need no code

Decision: `epdf-capi` changes nothing. `epdf-web` and `packages/core-wasm` change nothing; the
browser is covered by a new `epdf-web` test (`crates/epdf-web/tests/form_groups.rs`).
Reason: `epdf-capi` has seven fixed engine operations (`crates/epdf-capi/src/operations.rs:39-47`)
and no form or annotation plugin, so purpose, format, groups and turns have nowhere to go there.
Text page geometry (#1157) changed how the engine reads glyphs, not the `pages.geometry` answer, and
the wrapper table lists no MK icon or stamp-name surface (`reports/CU-C3.md`). The browser reaches
every new member by name through the form and annotation harnesses, and
`packages/core-wasm/src/plugins.ts:157` types `formPlugin` as upstream's.
Cost: none beyond the test.
Reverse: not applicable.

## CU-C8-D10. The form controls UI (#1138) has no platform to land in

Decision: no form controls UI is added to Kotlin or .NET.
Reason: neither renders forms. The Compose module draws pages, text selection, search and annotation
tools but has no form layer
(`platforms/android/embedpdf/compose/src/main/kotlin/com/embedpdf/compose/`), and .NET has no UI
package (D6 of `docs/superpowers/plans/2026-11-20-dotnet-parity.md`). The attach surface #1138 needs
(`widget_anchor`, `is_widget_focused`, `takes_cursor`) is exposed for an app that draws its own.
Cost: an app builds the controls itself on both platforms.
Reverse: not applicable.

## CU-C8-D11. The osx rows build `epdf-capi` with cargo on the Mac

Decision: for osx-arm64 and osx-x64, `scripts/cu-c8/dotnet.sh` ran on the Mac with `HOST_TESTS=0`:
cargo built `epdf-capi` for `aarch64-apple-darwin` and `x86_64-apple-darwin` into a scratch target
dir, then `pack.sh` and `validate.sh`. .NET SDK 11.0.100 and runtime 10.0.15 for both architectures
were installed under the job's scratch dir with `dotnet-install.sh`; the darwin archives were
downloaded with `curl` and checked against `engine-runtime-build.json`'s sha256
(`fetch-libpdfium.sh` calls `node`). osx-x64 ran under `arch -x86_64` with `/usr/bin/python3`.
Reason: the brief puts these two `validate.sh` rows on the Mac and allows non-Node tools for them;
the package needs a darwin `libepdf_capi.dylib`, which only a darwin cargo build makes.
`validate.sh` checks the host RID with `platform.machine()`, and the `mise` Python shim is arm64
only, so the first osx-x64 run stopped at the RID check.
Cost: a 2026-11-27 exception to "no cargo on the host", for two `epdf-capi` builds; the target dir
and SDKs are deleted at cleanup.
Reverse: not applicable.

## CU-C8-D12. `check-dotnet-engine.sh`'s verdict comes from the Mac

Decision: the container's run exits 69 (`gh` has no token); the Mac's run, with the user's `gh`,
exits 1: the pin names `0.0.0-engine.39d10114bde3ec2a`, the tree hashes to
`0.0.0-engine.11ca75894cda26ea`, and that runtime is not on the feed. The pin is not moved.
Reason: the check only reads package versions; the container is not logged in to GitHub. The engine
changed in CU-C4 to CU-C8, so a stale pin is the expected answer; moving it is part of
`release-train-prep` in CU-C10.
Cost: until CU-C10 the check fails on this branch, as it would for any engine change.
Reverse: not applicable.

## CU-C8-D13. The emulator runs the container's AAR libraries

Decision: the instrumented tests ran on the Mac from `.claude/worktrees/c8-mac` at `81fac2a99`, with
`bindings/src/main/jniLibs` and `bindings/src/main/kotlin` copied from the container's
`build-aar.sh` output, so Gradle on the Mac built only the APKs. The emulator is the `Pixel_4` AVD
on `android-37.1` `ps16k` arm64 (`CP31.260623.012`, `getconf PAGE_SIZE` 16384).
Reason: `build-aar.sh` on the Mac would compile the Rust for both Android ABIs with host cargo; the
container already built and checked those exact libraries.
Cost: the device run tests the container's build, not one made on the Mac.
Reverse: run `build-aar.sh` on the Mac first.

## CU-C8-D14. The linux-x64 .NET rows use SDK 11.0.100 installed into the amd64 container

Decision: the .NET rows for linux-x64 ran in `cu-c8-x64` with .NET SDK 11.0.100 and runtime 10.0.15
installed by `dotnet-install.sh` into `/workspaces/tools/dotnet-x64`, and `EPDF_IMAGE_ID` naming the
amd64 image. A first run with the image's own SDK 10.0.404 failed at `dotnet test` and is kept as
`logs/CU-C8/dotnet-head-linux-x64-sdk10/`.
Reason: `platforms/dotnet/global.json` pins SDK 11.0.100 with `rollForward: disable`, and the amd64
image predates that pin. linux-x64 runs on Rosetta here, as in 2026-11-18 (CU-C8b-D5).
Cost: the SDK is not the image's; the row is emulated, not native.
Reverse: rebuild the amd64 image from the catch-up's merge.
