# CU-C9: Swift on the Mac

`research/catchup-2026-11-27-swift`, cut from `research/catchup-2026-11-27-platforms` at
`9c8416faf`, builds the four Apple fork slices for runtime `9472117ed` without Node, packages the
engine framework locally, and gives the Swift package the surface Kotlin and .NET got in CU-C8. All
three Swift runs pass at head: 623 of 623 `EmbedPDFTests` on the iOS simulator, macOS arm64 and
macOS x86_64, where the start ran 618; the five new tests are the whole difference. The coverage
check reports the same 7 errors as `d250a68e3`, so the nine rows CU-C8 left pending are mapped and
nothing is new. Nothing was published and neither `Package.swift` pin moved. Decisions are in
[`../decisions/CU-C9.md`](../decisions/CU-C9.md).

## Commits

| Commit | Summary |
|---|---|
| `d2d0a4386` | `Form.swift`, `Annotation.swift`, `AnnotationInfo.swift`, `swift-api-coverage.json` |
| `5a7abb9fe` | `FormGroupsTests.swift`; `FormTests` and `AnnotationInfoTests` adapted to the bindings |
| the docs commit | this report, the decisions, the `decisions.md` index, `scripts/cu-c9/` |

## Slices and framework

`scripts/cu-c9/slices.sh` ran `buildFork`'s native steps in shell (CU-C9-D1) in
`perf-compare/.work/fork/9472117edbc7d9d980888c2418573081a76f532f/` of the main checkout, a clone of
the cached `af480d0ce` tree. The first target resynced the gclient deps, because `9472117ed`'s `DEPS`
moves FreeType, V8 and LLVM libc; its time includes that sync. Every `BUILD-METADATA.json` names
`9472117edbc7d9d980888c2418573081a76f532f`.

| Target | Build | `libembedpdf.a` sha256 |
|---|---|---|
| `ios-arm64` | 232 s, with the sync | `35c701615d4f8f653eca32a0c413b198c66ebdaa317a6a5917a7b67b2816dd77` |
| `ios-sim-arm64` | 79 s | `6e6916e2b12fe5a90e57c3c6067a2fc94b5a531ad9324efac9c7ce588b4e5083` |
| `darwin-static-arm64` | 53 s | `cf184371316a47618ea394c48cc7f689e08d1eaab0fc29160eb4c5ebb2026733` |
| `darwin-static-x64` | 57 s | `f2b64b06de4661735480a546f34ead3f0cc5dca86dec318b95a4c4b2103b69d2` |

`scripts/cu-c9/slice-exports.sh` read each archive with Rust 1.100.0's `llvm-nm -gUj`
(`~/.rustup/toolchains/1.100.0-aarch64-apple-darwin/lib/rustlib/aarch64-apple-darwin/bin/llvm-nm`).
In all four: the three renamed exports (`EPDFAnnot_GetDrawing`, `EPDFAnnot_SetDrawing`,
`EPDFDoc_GetDrawings`) and the five new ones (`EPDFAnnot_GetMKIcon`, `EPDFAnnot_SetMKIcon`,
`EPDFAnnot_HasMKIcon`, `EPDFAnnot_ClearMKIcon`, `EPDFText_GetPageGeometry`) are present, the three
`*StampDrawing*` names are gone, and there are 604 `EPDF*` exports, CU-C1's count
([`CU-C1.md`](CU-C1.md), export table).

`build-xcframework.sh` (`LIB_ROOT` at the staging root
`…/runtime-src/out/embedpdf-runtime-staging`, `EMBEDPDF_NM` the `llvm-nm` above,
`CARGO_BUILD_JOBS=8`) took 811 s and printed `native graph verified`. `package-engine-framework.sh`
wrote `platforms/apple/.build/publish/EmbedPDFBindings.xcframework.zip`, 116,595,241 bytes, SwiftPM
checksum `f347d199d5c4adfee09a18ab73f8aa6f20e09b2067f366def5edd26eb7fcb87f` (`swift package
compute-checksum` agrees). Each `EmbedPDFBindings` binary holds the eight renamed and new exports
per architecture and no `StampDrawing` symbol. `apple-engine-hash.sh` gives `ddad56cdb86961ce` for
`5a7abb9fe`'s tree, the release CU-C10 would publish; the Swift sources are not among its inputs, so
the hash was the same before the commits. `APPLE_CRATES` matches `cargo metadata`'s local crates
under `epdf-viewer-host` (`scripts/cu-c9/apple-crates.py`: none missing, none extra), so it is
unchanged.

## Swift API added

Aliases: `FormFieldFormat`, `FormGroup`, `FormGroupInput`, `FormGroupPosition`,
`FormWidgetAnchor`, `RecordAccent`.

```swift
FormCapability.groups() async throws(PDFError) -> [FormGroup]
FormCapability.setGroups(_ groups: [FormGroupInput]) async throws(PDFError)
FormCapability.designGroup() async throws(PDFError) -> String?
FormCapability.setDesignGroup(_ groupID: String?) async throws(PDFError)
FormCapability.canSetGroup(_ groupID: String) async throws(PDFError) -> Bool
FormCapability.moveToGroup(_ fields: [FormFieldRef], groupID: String?) async throws(PDFError) -> [FormField]  // @discardableResult
FormCapability.selectedFields() async throws(PDFError) -> [FormField]
FormCapability.widgetAnchor(_ widget: AnnotationRef) async throws(PDFError) -> FormWidgetAnchor?
FormCapability.isWidgetFocused(_ widget: AnnotationRef) async throws(PDFError) -> Bool
AnnotationDraftsCapability.previewPlacement(tool:from:to:on:displayRotation: Double? = nil)
AnnotationInfo.accent: RecordAccent?
extension FormFocusTarget { public func takesCursor() -> Bool { true } }  // focus(select:) now -> Bool
```

Purpose, format and group in reads, `FormCreateFieldInput` and `FormFieldPatch` (with the
`.purpose`, `.format` and `.groupId` clear keys), the fill item's and hit's `frame` and `rotation`,
the fill item's `groupId`, `canFill`, `required`, `empty`, `accent` and a toggle's `inGroup`,
`fields.tint`, and the `.groupsChanged` and `.designGroupChanged` events arrive through the
generated records the aliases already expose. The source breaks are `focus(select:)` answering a
`Bool` and `AnnotationRecord`'s new `accent` argument. No form controls UI, MK icon, stamp-drawing
or text-geometry surface was added (CU-C9-D4).

## Tests

`FormGroupsTests`, 5 tests on `report.pdf`, the cases of Kotlin's `FormGroupsTest`: purpose, format
and group created, patched (`.other` refused `invalid-input`, purpose cleared), saved, reopened and
moved out of the group; groups listed, named, announced, settable and shown on a fill item with
accent `#0055aa` at 0.16, which `tint: 0` removes; a widget turned 90 degrees fills with a 120 by 24
frame in a 24 by 120 rect, anchors at that rect and hits with the same frame and turn; a target that
declines the cursor is never focused, one whose `focus` answers `false` makes `focus` answer
`false`, and one that writes only `focus` takes the cursor through the default; a lent widget shows
its accent, its field is the selection's, Orientation is offered, it turns and resets, and a drag
preview takes a display rotation. `FormTests.CursorTarget` answers `true`;
`AnnotationInfoTests.theRoundTripLosesNothing` round-trips a record with an accent.

## Results

`scripts/cu-c9/swift-tests.sh`, the three runs the publish script makes: iOS simulator `iPhone 17`
(`8D9A9044-5872-498A-80F7-D88D9C2C4BD3`, iOS 27.2) in place, macOS through a private consumer
staged by `stage-mac-consumer.py`. Xcode 27.2 (27B52). Each cell is `EmbedPDFTests`,
`EmbedPDFModelsTests`, `EmbedPDFUITests`.

| Check | Start `89dde2cf9` (`d250a68e3`'s tree, published `apple-engine-c55b85806b96c5b2`) | Head `5a7abb9fe` (local framework) |
|---|---|---|
| `check-swift-api-coverage.py` | exit 1, 7 errors (at `d250a68e3`) | exit 1, the same 7 (16 at `9c8416faf`: those 7 and the 9 pending rows) |
| iOS simulator | 618, 147, 82 of 82 (first run 80 of 82, see below) | 623, 147, 82 of 82 |
| macOS arm64 | 618, 147, 81 of 81 | 623, 147, 81 of 81 |
| macOS x86_64 | 618, 147, 81 of 81 | 623, 147, 81 of 81 |

Every cell passed. Skipped in both columns: the three signature route tests (CU-C9-D7),
`PointerLatencyTests` and `StageReaderDragLagTests`, and on macOS
`theRepositoryRootManifestExportsOnlyTheFacadeAndItsUI` (the staged copy is outside the
repository). The UI target counts one test fewer on macOS, where some of its tests are
`canImport(UIKit)` only. The 7
coverage errors predate the catch-up (`resolve_command_text`, `page_list_calls`, `load_starts`,
`wheel_by`, `scene_key_builds`, `page_state_reads`, `settings_revision`).
`check-api-coverage.py --platform kotlin` reads 882 members, 746 ported, 0 pending.

The start's first iOS run ran while `build-xcframework.sh` compiled and failed two UI tests:
`aHitIsFilledWhereItsWordIs` (the flake the 2026-11-26 release prep saw on a hosted simulator) and
`importDownloadExportAndBothHomesUseOwnedBytes` (its 60-second limit). Run again alone, the start's
iOS run passed 82 of 82 (`base-ios-rerun/`). The head's first attempt stopped at compile: six
`expectNoDifference(try await …)` calls in the new test file, an `async` call in an autoclosure; the
values were hoisted into locals and its log was not kept.

## Not run

The publish script and any release, by D-3. `EmbedPDFBindingsSmokeTests`, which exists only in
`EMBEDPDF_LOCAL_ENGINE=1` mode. The signature route tests (CU-C9-D7).

## Evidence

Logs in `tmp/cu1127/logs/CU-C9/` of the host checkout: `slices.log`, `slice-exports.log`,
`build-xcframework.log`, `package.log`, `apple-crates.log`, `coverage-base-d250a68e3.log`,
`coverage-start-9c8416faf.log`, `coverage-head.log`, `base/` and `head/` (each run's `xcodebuild`
log and `status.txt`), `base-ios-rerun/`. Scripts: `scripts/cu-c9/slices.sh`,
`slice-exports.sh`, `apple-crates.py`, `fixtures.sh`, `stage-mac-consumer.py`, `swift-tests.sh`.

## Left in place

For the final review and CU-C10: the fork tree `perf-compare/.work/fork/9472117edbc7d9d980888c2418573081a76f532f/`
(its four stamps let `buildFork` skip the slice builds, and `artifacts/` holds the four tarballs),
and the worktree `.claude/worktrees/catchup-1127-swift` with `platforms/apple/EmbedPDF/EmbedPDFFFI.xcframework`,
`EmbedPDFBindings.xcframework`, `platforms/apple/.build/publish/EmbedPDFBindings.xcframework.zip`
and its cargo `target/`. The start worktree `.claude/worktrees/catchup-1127-swift-base` is removed
at cleanup. The test runs' derived data lived in temporary directories the run script deletes.
