# CU-C9 decisions

Decisions made while bringing Swift level with CU-C8 (Job H), on
`research/catchup-2026-11-27-swift` from `9c8416faf`, on the Mac host.

## CU-C9-D1. The Apple slices are built by a shell port of `buildFork`, seeded from the `af480d0ce` tree

Decision: `scripts/cu-c9/slices.sh` runs the steps `perf-compare/scripts/build-arm.mjs` `buildFork`
runs for a native target (`FORK_RECIPE` 4): `build-target.sh` then `package-target.sh` in
`perf-compare/.work/fork/9472117ed…/runtime-src`, with `depot_tools` on `PATH`,
`DEPOT_TOOLS_UPDATE=0`, `PDF_RUNTIME_TARGET_OS_LIST="emscripten ios mac"` and the artifacts in the
tree's `artifacts/`, and it writes the same `<target>.json` stamps. The tree was made by cloning
(`cp -cR`, APFS) the cached `af480d0ce` tree and its gclient files, deleting `out/`, fetching
`9472117ed` from the worktree's submodule and checking it out. Every target ran with
`PDF_RUNTIME_SYNC=auto`: the first (`ios-arm64`) found the seed's stamp naming the old `DEPS` hash
and ran a full `gclient sync`, which writes the new stamp (`deps_sha=a37659b2f…`); the other three
skipped the sync.
Reason: the brief forbids Node on the host, and `buildFork` is Node. Unlike `af480d0ce` (CU-C9-D1 of
2026-11-18), `9472117ed`'s `DEPS` moves `freetype_revision`, `v8_revision` and `llvm_libc_revision`,
which can reach the build graph, so skipping the sync as last time would have built against the old
FreeType. `auto` resyncs exactly when the `DEPS` hash differs, which is what a fresh `buildFork`
tree gets.
Cost: the shell copy can drift from `buildFork`; it does not check `hostClangWorks` (the resync
replaces `llvm-build` anyway), and its stamps say `"seeded": false`. The seed clone printed 491
`error: packfile … index unavailable` lines, all before the first build
(`tmp/cu1127/logs/CU-C9/slices.log:2-492`): 149 for `pack-80e7d99…` and 342 for `pack-ef140c3…`.
Both packs exist in the `af480d0ce` seed tree but not in the new tree, whose store was repacked to
4 packs. They were harmless: `git count-objects -v` reports garbage 0, `git status` is clean, and
`git cat-file` resolves the pin.
Reverse: delete `perf-compare/.work/fork/9472117ed…/` and let `buildFork` build it.

## CU-C9-D2. `takesCursor()` has a Swift default of `true`; `focus(select:)` must answer a `Bool`

Decision: `Form.swift` adds `extension FormFocusTarget { public func takesCursor() -> Bool { true } }`,
so a Swift target that writes only `focus(select:)` still compiles and takes the cursor. `focus`
now returns `Bool`, a source break for every Swift target. `FormGroupsTests.FocusOnly` relies on the
default.
Reason: .NET gives `IFormFocusTarget.TakesCursor` the same default (CU-C8-D6); Kotlin has no
default. A Swift protocol method cannot throw here (the generated protocol is non-throwing), so
.NET's "a throw counts as `true`" has no Swift case.
Cost: a target that means to decline must remember to write `takesCursor()`; the compiler will not
ask for it.
Reverse: delete the extension, and every target writes both.

## CU-C9-D3. The Swift names follow the facade, not the FFI

Decision: the nine handle methods are `groups()`, `setGroups(_:)`, `designGroup()`,
`setDesignGroup(_:)`, `canSetGroup(_:)`, `moveToGroup(_:groupID:)` (discardable, cancellable),
`selectedFields()`, `widgetAnchor(_:)`, `isWidgetFocused(_:)`; group ids are `groupID` in Swift
labels. `previewPlacement(tool:from:to:on:displayRotation:)` gets `displayRotation: Double? = nil`;
the deprecated `String` overload does not. `AnnotationInfo.accent` is a stored member and goes back
into `record`. `FormFillItem` and `FormWidgetHit` aliases say that `frame` is upstream's `box`
(CU-C8-D5).
Reason: the facade already writes `selectedField()`, `focusedField()` and `groupID`; the coverage
manifest maps each FFI member to one Swift symbol.
Cost: none beyond the names.
Reverse: rename in `Form.swift` and `swift-api-coverage.json`.

## CU-C9-D4. No form controls UI, MK icon, stamp-drawing or text-geometry surface in Swift

Decision: `EmbedPDFUI` gets no form controls (#1138), and nothing is added for MK icons, the D-1
stamp-drawing rename or text page geometry.
Reason: `EmbedPDFUI` and `EmbedPDFModels` have no form layer (no `FormCapability` use outside
`Sources/EmbedPDF/Plugins/Form.swift`), the same reason as CU-C8-D10. The wrapper table
(`reports/CU-C3.md`, "MK icon, stamp drawing names") and CU-C8-D9 list no wrapper surface for the
other three, and no Swift source named a stamp-drawing call, so D-1 breaks nothing here. The attach
surface #1138 needs (`widgetAnchor`, `isWidgetFocused`, `takesCursor`) is exposed.
Cost: an app draws its own form controls.
Reverse: not applicable.

## CU-C9-D5. The start column is `89dde2cf9`, `d250a68e3`'s tree with its own published engine

Decision: the baseline runs used a detached worktree at `89dde2cf9`, which is `d250a68e3` plus the
release train's pin commit (`Package.swift` and `platforms/apple/EmbedPDF/Package.swift` only), in
download mode with `apple-engine-c55b85806b96c5b2`; `apple-engine-hash.sh` gives
`c55b85806b96c5b2` for that tree. Fixtures were copied in by `scripts/cu-c9/fixtures.sh`.
Reason: no 2026-11-18 or later record holds Swift counts at `d250a68e3` (the release-train dry run
ran them on GitHub's runners, unrecorded here). `d250a68e3`'s own pin, `apple-engine-087f5a74ff6916ab`,
is the engine of an earlier tree; `89dde2cf9`'s is built from `d250a68e3`'s sources.
Cost: the start is a downloaded release, the head a local build.
Reverse: run `swift-tests.sh` at `d250a68e3` with a local framework built there.

## CU-C9-D6. The test runner stages the macOS consumer with Python

Decision: `scripts/cu-c9/swift-tests.sh` is the 2026-11-18 runner with `stage-mac-consumer.py` in
place of the `node -e` call to `macos-consumer.mjs`, and it requires `EMBEDPDF_TEST_DESTINATION`
(the run used `8D9A9044-5872-498A-80F7-D88D9C2C4BD3`, iOS 27.2, one of the two "iPhone 17"
simulators). The Python port makes the same checks and edits; on today's `Package.swift`, which
already names macOS 14, Foundation and AppKit, it copies the manifest unchanged.
`apple-crates.py` does `scripts/lib/linked-crates.mjs`'s `APPLE_CRATES` check from
`cargo metadata` output.
Reason: no Node on the host.
Cost: two Python copies of `.mjs` logic that can drift.
Reverse: run the `.mjs` files where Node is allowed.

## CU-C9-D7. The signature route tests stay skipped

Decision: neither column set `EMBEDPDF_SIGNATURE_ROUTES`, so `aGrantedTimestampThroughTheTimestampRoute`,
`aLongTermFillThroughTheRevocationRoute` and `aCallWithoutAnAuthoritySendsNoQuery` are skipped in
all six runs (seven with the start's iOS rerun).
Reason: the routes helper (`platforms/android/tests/signature-routes.mjs`) is Node, and the catch-up
changed nothing they cover.
Cost: those three paths are not exercised on Swift this catch-up.
Reverse: start the helper in the container with a port the simulator reaches and pass its address.
