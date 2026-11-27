# CU-C8: browser, Kotlin and .NET

`research/catchup-2026-11-27-platforms`, cut from `research/catchup-2026-11-27-plugins` at
`e90da2a3b`, makes the catch-up's new form and annotation surface reachable from uniffi, Kotlin
and .NET, and proves the browser already reaches it. Every platform row passes and none is
worse than `d250a68e3`. The form controls UI (#1138) has no platform to land in (CU-C8-D10), and
`epdf-capi` needs nothing (CU-C8-D9). win-x64 was not run: there is no Windows host. Decisions
are in [`../decisions/CU-C8.md`](../decisions/CU-C8.md).

## Commits

| Commit | Summary |
|---|---|
| `8e55095ad` | Job F re-review Minors: `reports/CU-C7.md` and `decisions.md` label the two `rust-coverage` runs by `cf65c1cc2`, rewrapped to 100 columns (CU-C8-D2) |
| `e3c57618e` | uniffi: purpose and format, groups, the design group, `move_to_group`, `selected_fields`, `widget_anchor`, `is_widget_focused`, the two group events, the fill item's and hit's frame and turn, the fill item's group, accent and radio place, `fields.tint`, `FormFocusTarget.takes_cursor` and its `bool` answer, `preview_placement`'s display rotation, `AnnotationRecord.accent`; Rust tests in `form`, `annotation` and `epdf-viewer-host` |
| `ad53d425a` | `crates/epdf-web/tests/form_groups.rs`: the surface through `WasmKernel` |
| `f9bda565e` | .NET: the surface on `FormCapability`, `AnnotationInfo` and `AnnotationDraftsCapability`, `FormGroupsTests`, `PublicAPI.Unshipped.txt`, `csharp-api-coverage.json` |
| `81fac2a99` | Kotlin: the surface on `FormCapability`, `AnnotationInfo` and `AnnotationDraftsCapability`, `FormGroupsTest`, a device test, `embedpdf.api`, the Kotlin and Swift coverage rows, the local Android slice pin |
| the docs commit | this report, `decisions/CU-C8.md`, the `decisions.md` index, `scripts/cu-c8/` |

## What each platform exposes

The wrapper table is `reports/CU-C3.md` ("CU-C7, CU-C8, CU-C9: what each wrapper exposes").

| Surface | uniffi (`ffi.rs`) | Kotlin | .NET | Browser |
|---|---|---|---|---|
| Groups | `groups`, `set_groups`, `design_group`, `set_design_group`, `can_set_group`, `move_to_group`, `selected_fields`; events `GroupsChanged`, `DesignGroupChanged`; `FormFieldSettings.tint` | the same names on `FormCapability`; `FormEventGroupsChanged`, `FormEventDesignGroupChanged` | `GetGroupsAsync`, `SetGroupsAsync`, `GetDesignGroupAsync`, `SetDesignGroupAsync`, `CanSetGroupAsync`, `MoveToGroupAsync`, `GetSelectedFieldsAsync`; `FormEvent.GroupsChanged`, `.DesignGroupChanged`; `FormFieldSettings.Tint` | by name through the form harness |
| Fill item and hit | `frame` (upstream `box`, CU-C8-D5), `rotation`, `group_id`, `can_fill`, `required`, `empty`, `accent`, `Toggle.in_group`; hit `frame`, `rotation` | generated records | `Frame`, `Rotation`, `GroupId`, `CanFill`, `Required`, `Empty`, `Accent`, `Toggle.InGroup` | as upstream |
| Control attach | `widget_anchor`, `is_widget_focused`, `FormFocusTarget.takes_cursor`, `focus -> bool` (CU-C5-D4) | `widgetAnchor`, `isWidgetFocused`, `FormFocusTarget` | `GetWidgetAnchorAsync`, `IsWidgetFocusedAsync`, `IFormFocusTarget.TakesCursor` (CU-C8-D6) | as upstream |
| Purpose and format | `FormField.purpose`, `.format`; `FormCreateFieldInput` and `FormFieldPatch` `purpose`, `format`, `group_id`; patch keys `Purpose`, `Format`, `GroupId` | generated records, `FormFieldFormat*` aliases | `FormField.Purpose`, `.Format`, `FormFieldFormat`, the input and patch members | as upstream |
| Signature mark | no change: a mark is bytes (CU-C5's hand-off) | none | none | engine payload |
| Widget turns | selection `rotate_by` and `reset_rotation` turn lent widgets; Orientation in the generic properties; `preview_placement(display_rotation)`; `AnnotationRecord.accent` (CU-C8-D8) | `previewPlacement(displayRotation)`, `AnnotationInfo.accent` | `PreviewPlacementAsync(displayRotation)`, `AnnotationInfo.Accent` | as upstream |
| `layerBytes` `baseSha256` | the engine wire, unchanged | none | none | the client forwards it |
| MK icon, stamp drawing names, text page geometry | none (CU-C8-D9) | none | none | none |

## Tests added

| Where | Tests | What they prove |
|---|---|---|
| `crates/plugins/form/tests/ffi.rs` | 8 | purpose and format read; a create sends purpose, format and group; a patch sets and clears them, and refuses setting and clearing one key; a turned grouped widget's frame, turn, accent and hit, tint 0 and a NaN tint; groups named, listed, announced and moved into; a refused group; an anchor and the focused widget; a native target that takes no cursor and one whose focus finds nothing |
| `crates/plugins/form/src/ffi.rs` (unit) | 2 | a radio item's place in its group and an unturned frame; the format's spelling both ways |
| `crates/plugins/annotation/tests/ffi.rs` | 2 | a drag preview lays out by the display rotation; an owned record has no accent |
| `crates/epdf-viewer-host/tests/editing_workflows.rs` | 2 | through the native engine: purpose, format and group saved and reopened, `Other` refused, a quarter-turned widget's frame and anchor, groups and accent; a lent widget's accent, its field in `selected_fields`, Orientation offered, a turn and its reset |
| `crates/epdf-web/tests/form_groups.rs` | 2 | the same members by name through `WasmKernel`, events through `$fn` callbacks, `moveToGroup`'s op |
| `platforms/dotnet/EmbedPDF.Viewer.Tests/FormGroupsTests.cs` | 6 | the viewer-host tests' cases in C#, plus a drag preview with a rotation |
| `platforms/android/.../FormGroupsTest.kt` | 5 | the same in Kotlin on the JVM |
| `platforms/android/.../DeviceViewerTest.kt` | 1 | purpose, format, groups, accent, a turned widget and a declining target on the device |

## Results, start against now

Start is `d250a68e3`; "CU-C7" marks a number from that job's table at the same start. Now is
`81fac2a99`'s tree, except where a row names another commit.

| Check | Where | Start | Now |
|---|---|---|---|
| `cargo test --workspace --no-fail-fast` | container arm64 | 3,567 passed, 0 failed, 2 ignored (CU-C7) | 3,630 passed, 0 failed, 2 ignored (`f9bda565e`'s tree) |
| `cargo clippy --workspace --all-targets` | container arm64 | exit 0, 1 warning (CU-C7) | exit 0, the same 1 warning |
| touched crates, `uniffi` features, fmt, clippy, test | container arm64 | | exit 0, 0, 0; 469 passed |
| `epdf-web` wasm tests | container arm64 | 63 passed, 9 binaries (CU-C7) | 65 passed, 10 binaries |
| `dotnet test` `EmbedPDF.Tests`, net10.0 and net11.0 | container arm64 | 36 of 36 each | 36 of 36 each |
| `dotnet test` `EmbedPDF.Viewer.Tests` | container arm64 | 1,155 passed, 4 skipped of 1,159 each | 1,161 passed, 4 skipped of 1,165 each |
| `dotnet test` `EmbedPDF.Models.Tests` | container arm64 | 163 passed, 1 skipped each | 163 passed, 1 skipped each |
| parity self-test | container arm64 | 16 of 16 | 16 of 16 |
| packaging unit tests | container arm64 | OK | OK |
| C# API coverage | container arm64 | 880 members (739 ported, 141 exclusions) | 889 (748 ported, 141 exclusions) |
| `pack.sh`, `validate.sh` linux-arm64 | container arm64 | PASS | PASS |
| `dotnet test`, parity, packaging, C# coverage, `pack.sh`, `validate.sh` linux-x64 | container amd64, Rosetta | | `EmbedPDF.Tests` 36 of 36, `Viewer.Tests` 1,161 passed and 4 skipped of 1,165, `Models.Tests` 163 passed and 1 skipped, each on both frameworks; parity 16 of 16; packaging OK (4 skipped); C# coverage 889; pack and validate PASS |
| `pack.sh`, `validate.sh` osx-arm64 | Mac | | PASS |
| `pack.sh`, `validate.sh` osx-x64 | Mac, Rosetta | | PASS |
| `check-dotnet-engine.sh` | container arm64; Mac | exit 0, `current 0.0.0-engine.39d10114bde3ec2a` | container exit 69 (`gh` has no token); Mac exit 1, stale: `0.0.0-engine.11ca75894cda26ea` is unpublished |
| Kotlin API coverage | container amd64 | 873 members (737 ported, 136 exclusions) | 882 (746 ported, 136 exclusions) |
| Android Python unit tests | container amd64 | | OK |
| `test-command-context.sh` | container amd64 | 1 of 1; the checksum gate rejects the corrupted copy | the same |
| Gradle JVM tests, six modules | container amd64 | 957 tests, 0 failures, 1 skipped | 962 tests, 0 failures, 1 skipped (`FormGroupsTest`'s 5) |
| `apiCheck` | container amd64 | exit 0 | exit 0 after `apiDump` (`embedpdf.api`, 12 lines added, 1 changed) |
| `build-aar.sh`, 16 KB checks | container amd64 | exit 0; `LOAD` alignment `0x4000` for both `.so` in both ABIs; test APK 16 KB aligned | the same; `bindings-release.aar` 44,123,855 bytes, sha256 `2526a56c57999034ed2dbe1a076fe3840fa429fa5be32e339eef046ba62b8a86` |
| `connectedDebugAndroidTest`, `Pixel_4` `ps16k`, three modules | Mac | 16 of 16 (`embedpdf` 7, `compose` 4, `javascript` 5) | 17 of 17 (`embedpdf` 8 with the new form test) |
| win-x64 | | not run | not run |

## Where each step ran, and why

The arm64 container `embed-pdf-catchup1127` ran Rust, the browser, and every .NET row for
linux-arm64. Kotlin ran in `cu-c8-x64`, an amd64 container on the same volume, because the arm64
image has no Android SDK or NDK (CU-C8-D3); the Android slices were built in `cu-c8-android`, an
amd64 Debian container (CU-C8-D4). linux-x64 ran in `cu-c8-x64` too, with SDK 11.0.100 installed
there (CU-C8-D14). The start's Kotlin rows used the `af480d0ce` Android slices and
`/workspaces/target-base-x64`. The Mac ran only what needs macOS: `pack.sh` and `validate.sh` for
osx-arm64 and osx-x64 (`otool`, the darwin libraries and the host RID check),
`check-dotnet-engine.sh`'s verdict (the container's `gh` has no token; the check only reads package
versions), and the emulator runs, from `.claude/worktrees/c8-mac` at `81fac2a99` and
`.claude/worktrees/c8-base-mac` at `d250a68e3`, each with its container-built Android libraries
(CU-C8-D13). For the osx rows cargo built `epdf-capi` on the Mac into a scratch target dir, with
.NET SDK 11.0.100 and runtime 10.0.15 installed under the job's scratch dir; no Node tool ran there.
osx-x64 ran under Rosetta with `/usr/bin/python3`: a first run with the arm64-only `mise` Python
stopped at `native host RID osx-arm64 differs from requested osx-x64`
(`logs/dotnet-head-osx-x64-try1/`).

## Not done

- The Swift rows of the coverage manifest are pending CU-C9 (CU-C8-D7).
- `EmbedPDF.Runtime` `0.0.0-engine.11ca75894cda26ea` is unpublished; the pin moves in CU-C10.
- win-x64: no Windows host.

## Evidence

Container logs are in `/workspaces/logs/CU-C8/` (`rust1/`, `workspace/`,
`dotnet-base-linux-arm64/`, `dotnet-head-linux-arm64/`, `kotlin-head/`, `kotlin-head-aar/`,
`kotlin-base/`), the Mac's in the job's scratch dir under `c8/logs/`, and the slice builds in
`~/repos/embed-pdf-runtime-builds/c1127-9472117ed/android-logs/`. Scripts are in
[`../scripts/cu-c8/`](../scripts/cu-c8/).

## Left for CU-C11

`/workspaces/wt/CU-C8`, `/workspaces/target-c8` (41 GB), `/workspaces/target-c8-x64`,
`/workspaces/target-base-x64`, `/workspaces/tools`, the containers `cu-c8-x64` and
`cu-c8-android` and the volume `cu-c8-android` (the runtime checkout and its Android deps), the
host worktrees `.claude/worktrees/c8-edit`, `c8-mac` and `c8-base-mac`, the Mac's scratch
target dir and .NET installs, and the bundles. The slice archives stay in
`~/repos/embed-pdf-runtime-builds/c1127-9472117ed/android/` for CU-C10.
