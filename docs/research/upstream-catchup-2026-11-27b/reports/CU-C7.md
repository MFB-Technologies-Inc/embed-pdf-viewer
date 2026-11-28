# CU-C7: browser, Kotlin and .NET

`research/catchup-2026-11-27b-platforms`, cut from `research/catchup-2026-11-27b-plugins` at
`0c6097639`, exposes anyone fields (#1163) and picture buttons (#1165) through uniffi, Kotlin and
.NET, and proves the browser reaches them. Each platform has a test for each new call, and no row
is worse than `0afd87825`. One test fails on linux-x64, at the start too (CU-C7-D12). `epdf-capi` needs nothing (CU-C7-D7). win-x64 was not
run: there is no Windows host. Decisions are in [`../decisions/CU-C7.md`](../decisions/CU-C7.md).

## Commits

| Commit | Summary |
|---|---|
| `e52137ce0` | uniffi: `FormField.for_anyone`, `FormFieldDetail::Pushbutton { has_image }`, `FormWidgetLayout` and `FormWidgetRecord.layout`, `for_anyone` and `layout` on `FormCreateFieldInput`, `layout` on `FormWidgetPlacement`, `for_anyone` on `FormFieldPatch` (`to_json`'s array grows to 13), `set_button_image` and `FormFieldUpdateResult`; tests in `crates/plugins/form/tests/ffi.rs` and `crates/epdf-viewer-host/tests/editing_workflows.rs` |
| `5503edcaf` | `crates/epdf-web/tests/picture_buttons.rs`: the members by name through `WasmKernel` |
| `68ab7e46d` | .NET: the surface on `FormCapability`, `PictureButtonsTests`, `EnumMirrorTests`, `PublicAPI.Unshipped.txt` |
| `81e940680` | Kotlin: the surface on `FormCapability`, `PictureButtonsTest`, a device test, `embedpdf.api` |
| the docs commit | this report, `decisions/CU-C7.md`, the `decisions.md` lines, `scripts/cu-c7/` |

## What each platform exposes

| Surface | uniffi (`crates/plugins/form/src/ffi.rs`, `ffi/editing.rs`) | Kotlin | .NET | Browser |
|---|---|---|---|---|
| Anyone field read | `FormField.for_anyone` | `FormField.forAnyone` | `FormField.ForAnyone` | `forAnyone` on the field row, by name |
| Anyone field write | `FormCreateFieldInput.for_anyone`, `FormFieldPatch.for_anyone` | generated records | `FormCreateFieldInput.ForAnyone`, `FormFieldPatch.ForAnyone` | the draft and patch JSON, as upstream |
| Picture held | `FormFieldDetail::Pushbutton { has_image }` | `FormFieldDetailPushbutton.hasImage` | `FormFieldDetail.Pushbutton(bool HasImage)` | `hasImage`, by name |
| Widget layout read | `FormWidgetRecord.layout: Option<FormWidgetLayout>` (CU-C7-D5) | `FormWidgetRecord.layout`, `FormWidgetLayout` | `FormWidgetRecord.Layout`, `FormWidgetLayout` | `layout` on the widget row |
| Widget layout write | `FormCreateFieldInput.layout`, `FormWidgetPlacement.layout`; `update_widget`'s JSON patch already took it | generated records | `FormCreateFieldInput.Layout`, `FormWidgetPlacement.Layout`; `UpdateWidgetAsync` JSON | placement and patch JSON |
| Put or take out a picture | `set_button_image(field_ref, image: Option<Vec<u8>>, cancel) -> FormFieldUpdateResult` over the engine's `apply` (CU-C7-D2) | `setButtonImage(field, image: ByteArray?)`, `FormFieldUpdateResult` | `SetButtonImageAsync(field, ReadOnlyMemory<byte>? image)`, `FormFieldUpdateResult` | upstream's `doc.forms.setButtonImage` on the engine |
| `*_SINCE` constants | none (CU-C7-D7) | none | none | none |
| D-1 deprecations | none (CU-C3-D5) | none | none | none |

The coverage manifests are unchanged: `scripts/check-api-coverage.py` inventories plugin `Core` and
capability methods, and `set_button_image` has none (CU-C7-D2). Kotlin's accounting stays at 882
members, C#'s at 889.

## Tests added

| Where | Tests | What they prove |
|---|---|---|
| `crates/plugins/form/tests/ffi.rs` | 4 | `for_anyone`, `has_image` and a widget's `layout` read; a create sends `forAnyone` and the widget's `layout`, a patch `forAnyone`, a placement its `layout`; a picture goes out as one `forms.setButtonImage` op with `{"$bytes"}` and `null` takes it out; a closed document refuses |
| `crates/epdf-viewer-host/tests/editing_workflows.rs` | 1 | through the native engine: an anyone field created, refused on a signature field, patched; a push button laid out `image`, a layout refused on a text field, a second place `caption-below`, a picture in (each widget keeps its layout, the form hears it), refused on a text field, taken out; all of it after a save and reopen |
| `crates/epdf-web/tests/picture_buttons.rs` | 2 | the three members by name through `WasmKernel`; `update`, `updateWidget` and `create` reach `apply` with `forAnyone` and `layout` |
| `platforms/dotnet/EmbedPDF.Viewer.Tests/PictureButtonsTests.cs` | 3 | the viewer-host case in C#, plus a group with `ForAnyone` refused and `NotFound` for a missing field |
| `platforms/dotnet/EmbedPDF.Viewer.Tests/EnumMirrorTests.cs` | 1 row | `FormWidgetLayout` mirrors the generated enum |
| `platforms/android/.../test/.../PictureButtonsTest.kt` | 3 | the same in Kotlin on the JVM |
| `platforms/android/.../androidTest/.../DeviceViewerTest.kt` | 1 | an anyone field, a push button laid out `image` and a picture in and out on the device |

## Results, start against now

Start is `0afd87825`. A start number marked "2026-11-27" is 2026-11-27 CU-C8's head
(`81fac2a99`); `git diff --stat 81fac2a99 0afd87825 -- platforms` changes no Kotlin or C# source or
test (CU-C7-D8). Now is `81e940680`'s tree; the .NET and osx rows ran on the same tree before its
commits were made, which `git diff FETCH_HEAD` in the container confirmed.

| Check | Where | Start | Now |
|---|---|---|---|
| touched crates (`epdf-ffi`, `epdf-plugin-form`, `epdf-plugin-annotation`, `epdf-viewer-host`) with `uniffi`: fmt, clippy, test | container arm64 | 2026-11-27: exit 0, 0, 0; 469 passed | exit 0, 0 (no warning), 0; 476 passed, 0 failed |
| `epdf-web` wasm tests | container arm64 | 65 passed, 10 binaries (CU-C6) | 67 passed, 11 binaries |
| `dotnet test` `EmbedPDF.Tests`, net10.0 and net11.0 | container arm64 | 36 of 36 each | 36 of 36 each |
| `dotnet test` `EmbedPDF.Viewer.Tests` | container arm64 | 1,161 passed, 4 skipped of 1,165 each | 1,165 passed, 4 skipped of 1,169 each |
| `dotnet test` `EmbedPDF.Models.Tests` | container arm64 | 163 passed, 1 skipped each | 163 passed, 1 skipped each |
| parity self-test | container arm64 | 16 of 16 | 16 of 16 |
| packaging unit tests | container arm64 | OK | OK |
| C# API coverage | container arm64 | 889 members (748 ported, 141 exclusions) | 889 (748 ported, 141 exclusions) |
| `pack.sh`, `validate.sh` linux-arm64 | container arm64 | PASS | PASS |
| .NET rows, `pack.sh`, `validate.sh` linux-x64 | container amd64, Rosetta | 2026-11-27: `Tests` 36 of 36, `Viewer.Tests` 1,161 passed and 4 skipped of 1,165, `Models.Tests` 163 and 1 skipped, each on both frameworks; parity 16 of 16; packaging OK; coverage 889; PASS | `Tests` 36 of 36, `Viewer.Tests` 1,165 passed and 4 skipped of 1,169 on net10.0, 1,164 passed, 1 failed and 4 skipped on net11.0, `Models.Tests` 163 and 1 skipped on both; parity 16 of 16; packaging OK (4 skipped); coverage 889; PASS. The one failure, `JintScriptEngineTests.ADeepRecursionIsABudgetFailure`, fails at `0afd87825` too (CU-C7-D12) |
| `pack.sh`, `validate.sh` osx-arm64 | Mac | 2026-11-27: PASS | PASS |
| `pack.sh`, `validate.sh` osx-x64 | Mac, Rosetta | 2026-11-27: PASS | PASS |
| `check-dotnet-engine.sh` | Mac | exit 0, `current 0.0.0-engine.11ca75894cda26ea` | exit 1, stale: `0.0.0-engine.1c26d987b4f53e5d` is unpublished |
| `check-dotnet-engine.sh` | container arm64 | 2026-11-27: exit 69 (`gh` has no token) | exit 69, the same |
| Kotlin API coverage | container amd64 | 2026-11-27: 882 members (746 ported, 136 exclusions) | 882 (746 ported, 136 exclusions) |
| Android Python unit tests | container amd64 | 2026-11-27: OK | OK |
| `test-command-context.sh` | container amd64 | 2026-11-27: 1 of 1; the checksum gate rejects the corrupted copy | the same |
| Gradle JVM tests, six modules | container amd64 | 2026-11-27: 962 tests, 0 failures, 1 skipped | 965 tests, 0 failures, 1 skipped (`PictureButtonsTest`'s 3; `SignatureRoutesTest` rerun, CU-C7-D11) |
| `apiCheck` | container amd64 | 2026-11-27: exit 0 | exit 0 after `apiDump` (`embedpdf.api`, 1 line added) |
| `build-aar.sh`, 16 KB checks | container amd64 | 2026-11-27: exit 0; `LOAD` alignment `0x4000` for both `.so` in both ABIs; test APK 16 KB aligned | the same; `bindings-release.aar` 44,222,965 bytes, sha256 `3f8508ca13af29f066a8a495a10985ef940fa46e9b1d53c938d48819cece670d` |
| `connectedDebugAndroidTest`, `Pixel_4` `ps16k`, three modules | Mac | 2026-11-27: 17 of 17 (`embedpdf` 8, `compose` 4, `javascript` 5) | 18 of 18 (`embedpdf` 9 with the new device test; CU-C7-D11) |
| win-x64 | | not run | not run |

The full `cargo test --workspace` and engine and plugin conformance rows are CU-C6's; this job
changes only the form plugin's FFI module and tests, which the touched-crates row covers.

## Where each step ran, and why

The arm64 container `embed-pdf-catchup1127b` ran the Rust rows, the browser, and every .NET row for
linux-arm64, in `/workspaces/wt/CU-C7` with `/workspaces/target` (CU-C7-D1); the start's .NET row
ran in `/workspaces/wt/base` with a temporary `/workspaces/target-base-c7`. Kotlin and linux-x64 ran
in `embed-pdf-1127b-x64`, an amd64 container from `embedpdf-dev:20261127-94822b7a9e29` on the same
volume (CU-C7-D3, CU-C7-D10), with the Android slices of the local `9472117ed` build (CU-C7-D4). The
Mac ran only what needs macOS: `pack.sh` and `validate.sh` for osx-arm64 and osx-x64, with host cargo
for the two Apple targets and .NET from `dotnet-install.sh` in the job's scratch dir (CU-C7-D9);
`check-dotnet-engine.sh`, which needs the user's `gh`; and the emulator, with
`bindings/src/main/jniLibs` and `bindings/src/main/kotlin` copied from the container's
`build-aar.sh` output, so Gradle on the Mac built only the APKs. No Node tool ran on the Mac.

## What CU-C8 (Swift) must expose

The same uniffi records, so the generated Swift has them; the facade work is in
`platforms/apple/EmbedPDF`:

1. `FormField.forAnyone` (read).
2. `FormFieldDetail.pushbutton(hasImage:)`, a source break for any `case .pushbutton` match.
3. `FormWidgetLayout` (seven cases) and `FormWidgetRecord.layout`.
4. `forAnyone` and `layout` on `FormCreateFieldInput`, `layout` on `FormWidgetPlacement`,
   `forAnyone` on `FormFieldPatch`.
5. `setButtonImage(_ field: FormFieldRef, image: Data?) -> FormFieldUpdateResult` on the form
   capability, over `FormHandle.setButtonImage(fieldRef:image:cancel:)`.
6. A test per call, as `PictureButtonsTests.cs` has. No `swift-api-coverage.json` row
   (CU-C7-D2), no `*_SINCE` constant and no D-1 deprecation.

## Evidence

Container logs are in `/workspaces/logs/CU-C7/` (`rust-pre/`, `build/`, `dotnet-head-linux-arm64/`,
`dotnet-base-linux-arm64/`, `dotnet-head-linux-x64/`, `kotlin-head-pre/`, `kotlin-head-post/`); the
Mac's under the job's scratch dir in `c7/logs/` (`dotnet-head-osx-arm64/`, `dotnet-head-osx-x64/`,
`check-engine-{base,head}.log`, `emulator*.log`). Scripts are in [`../scripts/cu-c7/`](../scripts/cu-c7/).

## Known gaps

- `JintScriptEngineTests.ADeepRecursionIsABudgetFailure` fails on linux-x64 under Rosetta at both
  ends when run alone (6 of 6 runs each, `The script sandbox has been disposed`), and failed on
  net11.0 in the full head run; it passes on linux-arm64. It is not this job's (CU-C7-D12), but
  2026-11-27 CU-C8's linux-x64 row passed it, so something in this container or Rosetta session
  differs.

- `EmbedPDF.Runtime` `0.0.0-engine.1c26d987b4f53e5d` is unpublished; the pin moves at release prep.
- A `set_button_image` call is not in the form plugin's undo history; the engine's `opId` undo is
  not exposed (CU-C7-D2).
- win-x64: no Windows host.
