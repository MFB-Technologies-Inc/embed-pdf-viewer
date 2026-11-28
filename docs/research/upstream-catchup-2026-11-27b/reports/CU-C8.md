# CU-C8: Swift

`research/catchup-2026-11-27b-swift`, cut from `research/catchup-2026-11-27b-platforms` at
`064026d17`, exposes anyone fields (#1163) and picture buttons (#1165) in `platforms/apple/EmbedPDF`,
the six items of [`CU-C7.md`](CU-C7.md) ("What CU-C8 (Swift) must expose"). All three Swift runs
pass: 627 of 627 `EmbedPDFTests` on the iOS simulator, macOS arm64 and macOS x86_64, where the start
ran 623; the four `PictureButtonsTests` are the difference. The coverage check reports the same 7
errors as `0afd87825`. Nothing was published and neither `Package.swift` pin moved. Decisions are in
[`../decisions/CU-C8.md`](../decisions/CU-C8.md).

## Commits

| Commit | Summary |
|---|---|
| `663cccac6` | `Form.swift`: the `FormWidgetLayout` and `FormFieldUpdateResult` aliases, `setButtonImage(_:image:)`; `PictureButtonsTests.swift` |
| the docs commit | this report, `decisions/CU-C8.md`, the `decisions.md` lines, `scripts/cu-c8/` |

## Swift API added

```swift
public typealias FormWidgetLayout = EmbedPDFBindings.FormWidgetLayout          // Form.swift:46
public typealias FormFieldUpdateResult = EmbedPDFBindings.FormFieldUpdateResult // Form.swift:49
@discardableResult
FormCapability.setButtonImage(_ field: FormFieldRef, image: Data?) async throws(PDFError)
  -> FormFieldUpdateResult                                                      // Form.swift:228
```

`FormField.forAnyone`, `FormFieldDetail.pushbutton(hasImage:)`, `FormWidgetRecord.layout`, `forAnyone`
and `layout` on `FormCreateFieldInput`, `layout` on `FormWidgetPlacement` and `forAnyone` on
`FormFieldPatch` arrive through the generated records the existing aliases expose. The doc on
`setButtonImage` says what the Kotlin and C# docs say: it goes straight to the engine as one change,
outside the history's undo (`HistoryCapability.undo()`), so an undo after it steps back over the
change before it, not the picture. The source break is `case .pushbutton`, which now binds
`hasImage`; no Swift source in the package matched it. No `swift-api-coverage.json` row, no
`*_SINCE` constant and no D-1 deprecation (CU-C8-D2, CU-C7-D2, CU-C7-D7).

## Tests

`platforms/apple/EmbedPDF/Tests/EmbedPDFTests/PictureButtonsTests.swift`, on `report.pdf`, Kotlin's
`PictureButtonsTest` cases and one more:

| Test | What it proves |
|---|---|
| `forAnyoneIsCreatedPatchedAndSaved` (`PictureButtonsTests.swift:44`) | `forAnyone` created; refused `invalid-input` with a group and on a signature field; patched on and off; read back after a save and reopen |
| `aPushButtonsWidgetsKeepTheirLayoutAndAPictureGoesInAndOut` (`PictureButtonsTests.swift:74`) | a push button created laid out `.image` reads `.pushbutton(hasImage: false)` and its widget's `layout`; a layout on a text field refused; a placement `.captionBelow`; `setButtonImage` with `tiny.png` gives `hasImage: true` and both widgets keep their layouts; the form hears it; `updateWidget` to `caption-over`; `nil` takes it out; the saved copy keeps the picture and the layout |
| `aPictureForAFieldOfAnotherKindIsRefused` (`PictureButtonsTests.swift:116`) | a text field throws; a missing field throws `not-found` |
| `setButtonImageCancelsWhenTheTaskIsCancelled` (`PictureButtonsTests.swift:125`) | a call made in a cancelled task throws `operation-cancelled` and leaves the button empty |

## Framework

`build-xcframework.sh` took 1,056 s and printed `native graph verified`, with the four
`9472117ed` slices of 2026-11-27 (CU-C8-D1). `package-engine-framework.sh` wrote
`platforms/apple/.build/publish/EmbedPDFBindings.xcframework.zip`, 116,823,262 bytes, SwiftPM checksum
`99b63b38c5035b5202460b89ca88e7f154ffe0d85315b1386b6fd0845abc82b6` (`swift package compute-checksum`
agrees). `apple-engine-hash.sh` gives `b2a6390e5f338702` for this tree, the release the train would
publish. `scripts/cu-c8/ffi-exports.sh` read every slice with Rust 1.100.0's `llvm-nm -gUj`: in
`ios-arm64`, `ios-arm64-simulator`, and both architectures of `macos-arm64_x86_64`,
`uniffi_epdf_plugin_form_fn_method_formhandle_set_button_image` and its checksum are exported, with
24 `FormWidgetLayout` and 33 `FormFieldUpdateResult` Swift symbols each.

## Results, start against now

`scripts/cu-c8/swift-tests.sh` (2026-11-27 CU-C9's runner): the iOS simulator in place, macOS through
a private consumer staged by `stage-mac-consumer.py`, all with `EMBEDPDF_PREBUILT_ENGINE=local`.
Xcode 27.2 (27B52). Each cell is `EmbedPDFTests`, `EmbedPDFModelsTests`, `EmbedPDFUITests`. Start is
`0afd87825`; its Swift rows are 2026-11-27 CU-C9's head (`5a7abb9fe`), whose `platforms/apple`
differs from `0afd87825` in the `Package.swift` pin alone (CU-C8-D3).

| Check | Start `0afd87825` | Now `663cccac6` |
|---|---|---|
| `check-swift-api-coverage.py` | exit 1, 7 errors (rerun) | exit 1, the same 7 |
| iOS simulator (iOS 27.2) | 623, 147, 82 of 82 | 627, 147, 82 of 82 |
| macOS arm64 | 623, 147, 81 of 81 | 627, 147, 81 of 81 |
| macOS x86_64 | 623, 147, 81 of 81 | 627, 147, 81 of 81 |
| signature route tests (3) | skipped | passed in all three runs (CU-C8-D4) |
| new FFI symbols, four slice architectures | | present in each |

Skipped at now: `PointerLatencyTests` and `StageReaderDragLagTests`, and on macOS
`theRepositoryRootManifestExportsOnlyTheFacadeAndItsUI` (the staged copy is outside the repository).
The 7 coverage errors predate both catch-ups (`resolve_command_text`, `page_list_calls`,
`load_starts`, `wheel_by`, `scene_key_builds`, `page_state_reads`, `settings_revision`).

The first attempt stopped at compile: three `var` inputs captured in `@Sendable` closures
(`#SendableClosureCaptures`); they are captured by value now. Its log is in
`head-attempt1-compile/`.

## Not run

The publish script and any release. `EmbedPDFBindingsSmokeTests`, which exists only in
`EMBEDPDF_LOCAL_ENGINE=1` mode. The start's Swift runs (CU-C8-D3).

## Evidence

Logs in `tmp/cu1127b/logs/CU-C8/` of the host worktree `.claude/worktrees/catchup-1127b-swift`:
`build-xcframework.log`, `package.log`, `ffi-exports.log`, `coverage-start-0afd87825.log`,
`coverage-head.log`, `routes-bridge.log`, `head/` (each run's `xcodebuild` log and `status.txt`),
`head-attempt1-compile/`. Scripts: `scripts/cu-c8/swift-tests.sh`, `stage-mac-consumer.py` (copied
from 2026-11-27's `cu-c9/`), `ffi-exports.sh`, `routes-bridge.py`.

## Known gaps

- The root SwiftPM package does not compile against the pinned engine until the release train
  publishes `apple-engine-b2a6390e5f338702` and re-pins both manifests.
- A `setButtonImage` call is not in the history plugin's undo (CU-C7-D2); the doc says so.
