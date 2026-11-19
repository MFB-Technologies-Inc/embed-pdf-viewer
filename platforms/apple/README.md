# Apple

The Apple package exposes the Rust viewer through typed Swift capabilities and
SwiftUI controls on iOS 17 and macOS 14 or later.

Both manifests pin the engine release `apple-engine-8a472a28bcfb9c14`:
`platforms/apple/EmbedPDF/Package.swift:9` and the repository-root
`Package.swift:16`. SwiftPM downloads that binary by default, so nothing builds
Rust, and the package's tests and the demo run on it.
`bash platforms/apple/check-apple-engine.sh` still reports the pin as not
current on this branch, because `platforms/apple/build-xcframework.sh` is an
input to the engine hash (`platforms/apple/apple-engine-hash.sh:40`) and this
branch changed it. A release with the new hash has to be published and pinned
in both manifests before that check passes.

Two local modes remain for engine work: `EMBEDPDF_LOCAL_ENGINE=1` builds
against the source engine and `EMBEDPDF_PREBUILT_ENGINE=local` against a
locally packaged framework. [Where the engine comes from](#where-the-engine-comes-from)
has both. The local source recipe:

```bash
bash platforms/apple/build-xcframework.sh
cd platforms/apple/EmbedPDF
EMBEDPDF_LOCAL_ENGINE=1 xcodebuild test -scheme EmbedPDF-Package -destination 'platform=iOS Simulator,id=<UDID>'
```

When two simulators share a name, `name=` is ambiguous. Pass the device's id
instead, from `xcrun simctl list devices`:
`-destination 'platform=iOS Simulator,id=<UDID>'`.

The package has three products over a fourth, internal target. `EmbedPDF` is
the Swift facade. `EmbedPDFModels` holds the observable models
(`StageReaderModel`, `SearchModel`, `PDFReaderModel` and the others) and
imports no SwiftUI, so a UIKit or AppKit host can drive them. `EmbedPDFUI` is
the SwiftUI layer. It re-exports the models but not the facade, so a SwiftUI
app imports `EmbedPDF` and `EmbedPDFUI`. Capabilities retain their viewer and
marshal all kernel work to Rust; models and controls project that state onto
the main actor.
`EmbedPDFBindings` is the internal target under them, holding the uniffi
Swift for every crate in the archive: `epdf_uniffi`, `epdf_ffi` and
`epdf_viewer_host` today, and each plugin's `ffi` module as it lands. It
compiles in Swift 5 mode, because generated code isn't ours to make strict
concurrency clean. The handwritten targets use Swift 6 mode.

`Package.swift` and the handwritten sources and tests are tracked. The
XCFramework, `Sources/EmbedPDFBindings/` and every `Tests/*/Fixtures/` are
generated, and `platforms/apple/.gitignore` keeps them out of the repository.
The packaged engine ZIP is approximately 332 MiB with all four slices.
Generated bindings and binaries are verified build outputs, not reviewable
handwritten sources.

## Products, and why the generated layer isn't one

The package exports three products, `EmbedPDF`, `EmbedPDFModels` and `EmbedPDFUI`
(`platforms/apple/EmbedPDF/Package.swift:18-20`, and the repository-root `Package.swift:9-11`).
`EmbedPDFBindings`, the UniFFI-generated Swift, is a target only. Use `PDFViewer` and its typed
capabilities. For a field the typed layer doesn't cover, use the JSON-string entry points, which
take the engine's own payload: `ViewerConfigBuilder.annotation(settingsJSON:)`, the annotation
`update(_:_:)` field bag (`patchJSON`), and the form `create(draftJSON:)`, `update(_:patchJSON:)`
and `addWidget(to:placementJSON:)` overloads. Needing the raw generated API means asking us for a
typed member, not importing the bindings.

We decided against an `EmbedPDFBindings` library product, on 2026-11-10. It would make all 710
public generated types API. Every Rust change to a record, method or callback interface would then
be a break consumers see, where today only the 539 types that `EmbedPDF` re-exports are. It would
also leave the module half-usable: the linker settings (`c++`, `CoreFoundation`, `CoreGraphics`,
`Foundation`, and `AppKit` on macOS) live on the `EmbedPDFBindings` target in the source layout
(`Package.swift:34-40`), but in the binary layouts that target is a `.binaryTarget` with no
settings, and the loop that adds them (`Package.swift:116-124`) only touches `EmbedPDF`. The
root `Package.swift` declares the same settings inline on its `EmbedPDF` target (lines 22-28). A
direct importer of the binary would have to repeat them. We'll revisit this
if a typed member can't cover a real use.

This isn't a hard wall. `EmbedPDF` writes `public import EmbedPDFBindings` in the files that
re-export, so a client that imports only `EmbedPDF` sees the generated types below, and
`ProductsTests` fails if either manifest gains a `.library`. We checked by compiling a file that
imports only `EmbedPDF` and names each of the 710 generated public types unqualified:

- 539 resolve, through the same-name `public typealias X = EmbedPDFBindings.X` lines in
  `Sources/EmbedPDF/Exports.swift` and `Sources/EmbedPDF/Plugins/*.swift`. (The deprecated
  `PDFDocument` and `Annotation` aren't among them: they alias `DocumentInfo` and
  `AnnotationRecord`.) `RenderSnapshot` also resolves, but to the handwritten
  `EmbedPDF.RenderSnapshot` (`Plugins/Render.swift:58`), not the generated type.
- 170 don't: `ViewerHost`, `CancelToken`, `Subscription` and `PdfError`, 60 `*Handle` and
  `*HandleProtocol` types, 22 `*EventListener` types, 45 `*Impl` types, 17 other `*Protocol`
  types, and 22 more records and callback types such as `PdfEngine`, `RenderBatch` and
  `ViewerDelegate`.

Three of the names that resolve are there so you can write your own fetcher: `DocumentFetcher`,
`DocumentFetchCompletion` and `DocumentFetchCancellation`, aliased in
`Sources/EmbedPDF/Exports.swift`. They are the types in `PDFViewer.init(_:documentFetcher:)` and
`URLSessionDocumentFetcher.fetch(url:completion:)`, so a conformer imports only `EmbedPDF`.
`FetcherExportTests` compiles one and opens a URL document through it. The cost is that all three
are generated types, so their shape follows the engine's interface, not a protocol we wrote.

Could UniFFI hide the generated layer instead, so we'd get this by construction? Not with 0.32.0,
the version pinned as `=0.32.0` in `crates/uniffi-bindgen/Cargo.toml:14`, each plugin crate
(for example `crates/plugins/form/Cargo.toml:44`) and `Cargo.lock:2343-2344`. The Swift `Config`
(`uniffi_bindgen-0.32.0/src/bindings/swift/gen_swift/mod.rs:167-189`) has `module_name`,
`ffi_module_name`, `generate_immutable_records`, `omit_argument_labels`, `exclude`, `rename` and
the conformance switches, but no access-level option. The templates write `public` literally
(`RecordTemplate.swift:5,11,16`, `EnumTemplate.swift:5`, `ObjectTemplate.swift:68`,
`TopLevelFunctionTemplate.swift:1`, `Protocol.swift:2`). Hiding the types means rewriting the
generated files after `uniffi-bindgen generate`, and then `Exports.swift` and the hand-written
layer, which re-export and conform them publicly, stop compiling.

## Where the engine comes from

`Package.swift` gets the engine one of three ways, chosen by environment variables when the
manifest is evaluated:

- By default, SwiftPM downloads `EmbedPDFBindings.xcframework.zip` from the `engineRelease`
  release on `roanutil/embed-pdf-runtime` and verifies it against `engineChecksum`. Nothing
  builds Rust.
- `EMBEDPDF_PREBUILT_ENGINE=local` uses the same binary target, read from
  `EmbedPDF/EmbedPDFBindings.xcframework`, which `package-engine-framework.sh` writes.
- `EMBEDPDF_LOCAL_ENGINE=1` is the source layout above: `EmbedPDFFFI.xcframework` plus the
  generated Swift as a source target. `EMBEDPDF_EXPERIMENTAL_BRIDGE=1` implies it. It's the only
  mode with `EmbedPDFBindingsSmokeTests`, because `@testable import` can't see into a binary.

The downloaded engine needs Xcode 27.1 or later: its `.swiftinterface` was written by Swift 6.4
with `-language-mode 5`, and only the Xcode 27.1 toolchain has been checked against it.

The pin goes stale whenever a crate the engine links changes. `bash
platforms/apple/check-apple-engine.sh` hashes the tree with `apple-engine-hash.sh` and compares
the result with `engineRelease`. Exit 0 means the pin is current. Exit 1 means it's stale and
nothing is published for this tree yet. Exit 2 means the release exists and only the
`Package.swift` bump is missing. Codes 64, 69 and 70 are errors, not verdicts: bad arguments or
manifest, `gh` failing, the hash failing.

To publish:

```sh
node scripts/publish-engine-payload.mjs --dry-run --only apple
node scripts/publish-engine-payload.mjs --only apple
```

It wants a clean, committed tree, since the release notes record `HEAD`. It first checks
`APPLE_CRATES` in `apple-engine-hash.sh` against what `epdf-viewer-host` links according to
`cargo metadata`, and stops on a mismatch. On exit 1 it builds the fork's `ios-arm64` and
`ios-sim-arm64` slices (cached under `perf-compare/.work`, or `PERF_WORK_DIR`), then runs
`build-xcframework.sh` and `package-engine-framework.sh`. Then it runs the `EmbedPDF-Package`
tests against the packaged framework with `EMBEDPDF_PREBUILT_ENGINE=local`, so a broken framework
never reaches a release. Last, it creates `apple-engine-<hash>` with the zip and its `.sha256`, and
rewrites `engineRelease` and `engineChecksum`. On exit 2 it builds nothing and pins from the
release's `.sha256`. `--dry-run` does everything except create the release and rewrite the
manifest.

The test run uses the simulator named iPhone 17. When two share that name, set
`EMBEDPDF_TEST_DESTINATION='platform=iOS Simulator,id=<UDID>'`.

Either way the script leaves `platforms/apple/EmbedPDF/Package.swift` modified for you to commit.
That costs a window: on the commit the release was built from, `check-apple-engine.sh` exits 2.
It has to. The release names a commit, and that commit can't contain its own checksum.

## Plugins

| Plugin | Swift | `ViewerConfig` | Plan |
|---|---|---|---|
| metadata | `MetadataCapability` | `metadata: true` | `2026-10-01-swift-package.md` Task 6 |
| search | `SearchCapability` | `search: SearchConfig()` | `2026-10-01-swift-package.md` Task 7 |
| render | `RenderCapability` | `render: RenderConfig()` | `2026-10-01-swift-package.md` Task 8 |
| interaction | `InteractionCapability` | `interaction: true` | `2026-10-06-swift-plugins/01-interaction.md` |
| feedback | `FeedbackCapability` | `feedback: FeedbackConfig(provider: HapticFeedback())` | `2026-10-06-swift-plugins/01-interaction.md` |
| stage | `StageCapability`, `StageReaderView` (EmbedPDFUI) | `stage: StageConfig()` | `2026-10-06-swift-plugins/02-stage.md` |
| selection | `SelectionCapability` | `interaction: true, selection: true` | `2026-10-06-swift-plugins/03-selection.md` |
| link | `LinkCapability` | `interaction: true, link: true` | `2026-10-06-swift-plugins/04-link.md` |
| annotation | `AnnotationCapability`, `AnnotationFilePicker` | `interaction: true, annotation: ""` | `2026-10-06-swift-plugins/05-annotation.md` |
| history | `HistoryCapability`, `HistoryModel`, `HistoryControls` | `history: true` | `2026-11-06-swift-api-plugin-parity.md` |
| outline | `OutlineCapability`, `OutlineModel`, `OutlinePanel` | `outline: true` | `2026-11-06-swift-api-plugin-parity.md` |
| attachment | `AttachmentCapability`, `AttachmentModel`, `AttachmentPanel` | `attachment: true` | `2026-11-06-swift-api-plugin-parity.md` |
| form | `FormCapability` | `interaction: true, form: FormConfig()` | `2026-10-06-swift-plugins/06-form.md` |
| redaction | `RedactionCapability` | `interaction: true, annotation: "", redaction: RedactionConfig()` | `2026-10-06-swift-plugins/07-redaction.md` |
| measurement | `MeasurementCapability` | `interaction: true, annotation: "", measurement: MeasurementConfig()` | `2026-10-06-swift-plugins/08-measurement.md` |
| stamp | `StampCapability`, `FileStampLibraryStore` | `stamp: StampConfig()` (with `interaction: true, annotation: ""` to place) | `2026-10-06-swift-plugins/09-stamp.md` |
| page-edit | `PageEditCapability` | `pageEdit: true` | `2026-10-06-swift-plugins/10-page-edit.md` |
| view-manager | `ViewManagerCapability` | `viewManager: true` | `2026-10-06-swift-plugins/11-view-manager.md` |
| shell | `ShellCapability` | `shell: true` | `2026-10-06-swift-plugins/12-shell.md` |
| i18n | `I18nCapability`, `BundleLocaleLoader` | `i18n: I18nConfig(locales: [...], lazyLocales: [...], loader: BundleLocaleLoader())` | `2026-10-06-swift-plugins/13-i18n.md` |
| commands | `CommandsCapability`, `CommandHandler` | `commands: true` | `2026-10-06-swift-plugins/14-commands.md` |
| actions | `ActionsCapability`, `JavaScriptCoreEngineFactory` | `actions: .forCurrentTimeZone(javascript: ActionsJavaScriptConfig(enabled: true, engine: JavaScriptCoreEngineFactory()))` | `2026-10-06-swift-plugins/15-actions.md` |
| signature | `SignatureCapability`, `KeychainSigner`, `TrustAnchors` | `interaction: true, form: FormConfig(), signature: SignatureConfig()` | `2026-10-06-swift-plugins/16-signature.md` |

A plugin left out of `ViewerConfig` is refused at its accessor. `search(for:)`, `stamps()` and the
other accessors throw `PDFError` with code `.unsupported`, even for a document that is closed or
still loading, so a capability you hold has its own plugin. `DisabledPluginTests` checks every
accessor and fails when `ViewerConfig` gains a plugin without a row.

A few calls also use a companion plugin and refuse on each call, with `.unsupported`, when it is
left out. `RedactionCapability.markSelection()` needs `selection` and `markMatches(_:on:)` needs
`search`. `StageCapability.registerScrollHandler(panFallback:)` needs `interaction`.
`SignatureCapability.place(_:at:)` with a `.free` target needs `stamp` for an `.asset` mark and
`annotation` for a `.source` mark. Stamp placement without `annotation` throws
`.permissionDenied` instead, and `canPlace(in:)` reads `false`. Every other optional companion
is skipped without an error.

> Note: `JavaScriptCoreEngine` enforces a script's time budget by abandoning the realm, because
> JavaScriptCore has no public execution limit. A runaway script keeps one thread busy until the
> app exits. Memory and stack budgets aren't enforced. JavaScript on a real document also needs
> the form and annotation plugins (plans 05 and 06), whose engine paths the script world reads.
>
> Note: trust anchors are the app's own, set with `PDFViewer.setTrust(_:)`. iOS can't list the
> system roots (`SecTrustCopyAnchorCertificates` is macOS only), and validating against the system
> store would need a `SecTrust`-evaluating port in `epdf-signature-core`. Without anchors a good
> signature reads `.validUntrusted`. Document permissions follow `ViewerConfig.scope`; `nil` grants
> everything.
>
> Note: timestamps and revocation answers come over the network through one transport per
> viewer, `PDFViewer.setSignatureTransport(URLSessionSignatureTransport())`. The authority and the
> route are data on `SignatureConfig` (`timestampAuthority`, `revocation`) or on one call
> (`SignatureSignInput.timestampAuthority`); the RFC 3161 query, retries and what a status means
> stay in Rust. `URLSessionSignatureTransport` makes one attempt per call, holds it to the
> request's `timeoutMs` from connect to the last byte, stops reading a 2xx reply past
> `maxReplyBytes`, and answers every status as a reply. A status outside 2xx whose body runs long,
> breaks off or stalls is answered with no body. Without a transport every request fails with no
> reply. On iOS, a cleartext `http://` route also needs App Transport Security to allow it.

### Building a `ViewerConfig` with `ViewerConfigBuilder`

`ViewerConfig(search: SearchConfig(), ...)` keeps working. Its memberwise initializer gains a
parameter with every plugin we add, though, so we also ship a chained builder, one method per
field, each returning a changed copy:

```swift
let viewer = try PDFViewer(
  ViewerConfigBuilder().interaction().annotation(settingsJSON: "").search(SearchConfig()))
```

An unset field is the engine's default, so `ViewerConfigBuilder().build()` equals `ViewerConfig()`:
every plugin left out and `scope` at `nil`. `PDFViewer.init(_:documentFetcher:)` takes either
spelling. `ViewerConfigBuilderTests` fails when `ViewerConfig` gains a field the builder lacks.
The cost is a second spelling to keep in step, which is what that test is for.

### Task-named entry points

`StageCapability` and `InteractionCapability` are named after the engine part that does the work,
and each has dozens of members. So we also ship three small entry points named after what you want
to do. Each one forwards to its capability, and every member has the capability member's name,
signature and errors:

| To | Get | It forwards to | For everything else |
|---|---|---|---|
| move between pages | `viewer.pageNavigator(for: document)` | `StageCapability` | `navigator.stage` |
| zoom and fit pages | `viewer.pageZoom(for: document)` | `StageCapability` | `zoom.stage` |
| pick the active tool | `viewer.toolSwitcher(for: document)` | `InteractionCapability` | `switcher.interaction` |

```swift
let navigator = try await viewer.pageNavigator(for: document)
if try await navigator.canGoNext() { try await navigator.nextPage() }
```

These are additive. The capabilities and their accessors are unchanged and stay the escape hatch:
an entry point needs the same `ViewerConfig` plugin and throws the same `PDFError`.
`ShellCapability` (which panels, sheets and menus are open) and `ViewManagerCapability` (documents
as tabs in panes) don't get one. They're small and their members already say the task, so an entry
point would mostly repeat them. The cost of the three we do ship is a second spelling to keep in
step; `TaskEntryPointTests` fails when a member on either side disappears or changes signature.

## What the script assembles

Per slice, `libtool -static` merges `libepdf_viewer_host.a` with the fork's
`libembedpdf.a` into one archive, because `xcodebuild -create-xcframework` takes
one `-library` per platform. The fork halves come from
`packages/engine/runtime/build/libpdfium/{ios-arm64,ios-sim-arm64}`, put there by
`scripts/fetch-libpdfium.sh` reading `engine-runtime-build.local.json`. There is
no download: those entries are `file://` URLs into a local fork checkout, per
the standing decision not to cut fork releases.

The headers and the Swift are regenerated from the built library on every run
rather than checked in. uniffi's proc macros are the only thing that knows the
current shape of the API, and a hand-kept copy drifts without saying so.

Bindgen runs in library mode against the simulator's static
`libepdf_viewer_host.a`, and uniffi 0.32 reads the metadata straight out of the
archive, so no dylib is needed. It writes one `*FFI.h` and one
`*FFI.modulemap` per crate, and the script concatenates the maps into the
single `module.modulemap` the XCFramework wants. A crate only shows up if the
archive links it: rustc leaves an unreferenced dependency out of a staticlib,
which is why `crates/epdf-viewer-host/src/lib.rs` names `extern crate
epdf_uniffi`. Without that line the bindings come out with no `PdfEngine` and
the smoke tests don't compile.

## Observing viewer state

`PDFViewer.observeStateRevisions()` returns a new subscription on each call.
It installs synchronously, keeps the newest revision, and finishes on viewer
destruction. Subscribe before reading the initial snapshot so a change during
that read is queued. Revisions invalidate reads; they are not an event log and
there is no initial revision emission. Cancelling a consuming task cancels only
that subscription. The stored `stateRevisions` property supports one consumer
and is deprecated in favor of this method.

`PDFViewer.observeReportedErrors()` follows the same rule for errors the kernel
reports rather than throws. Each call is its own subscription that keeps the
newest 16 errors and sees only errors reported after it returns, so subscribe
before the call that might report one. The stored `reportedErrors` property
supports one consumer and is deprecated.

```swift
let revisions = viewer.observeStateRevisions()
var rows = try await outline.rows()
for await _ in revisions {
  rows = try await outline.rows()
}
```

Models and views should own the consuming task for their visible lifetime.
Each panel may subscribe independently; no panel needs to forward revisions to
another panel.

`RenderRevisions` counts render invalidations per page. `revision(for:)` is
`0` for a page never invalidated and rises by one each time the engine
invalidates it, so a view can key a page image on it and redraw only the pages
an edit touched. The model observes by itself, from `init` until the document
closes or the model is released. It has no `task()` to run. `StageReaderModel`
owns one as `revisions`, and `ThumbnailModel` keeps its own.

## Page overlays

`StageReaderView` takes a `pageOverlay` closure and lays its view over each
visible page, above the page image. The overlay gets the page's frame, origin
at the page's top left, and the `StageVisiblePage` it covers.
`viewportPoint(_:)` turns a point in the overlay into the viewport point
`StageCapability.viewportToPage(_:_:)` takes.

```swift
StageReaderView(model: reader) { page in
  Color.clear
    .contentShape(Rectangle())
    .onTapGesture { location in
      Task {
        // A page point, or nil when the location is off the page or the call fails.
        if let point = try? await reader.stage.viewportToPage(page.page, page.viewportPoint(location)) {
          print("tapped", point)
        }
      }
    }
}
```

SwiftUI already gives an overlay's own gestures priority over the reader's
drag, so an overlay's tap or drag gesture needs nothing more. An overlay
that needs every touch, such as a drawing tool or a UIKit recognizer, sets
`reader.isPanEnabled = false` while it works. The reader then stops panning and
ends any drag it had started, without a fling. Pinch and double tap stay active
alongside overlay gestures either way. `StageReaderView(model:)` still builds a
reader with no overlay.

## Page rotation

Engine geometry is in unrotated page space. Annotation rects, text boxes and
search hits all ignore the page's rotation. `PageInfo.geometry` returns a
`PageGeometry` that converts points and rects between that space and the page
as shown, turned clockwise. Both spaces put the origin at the top left with y
pointing down.

Renders follow the same split. `renderPage` draws unrotated unless you pass
`geometry.turn`, the page's clockwise turn as a `RenderRotation`.
`renderThumbnail` has no rotation option, so turn its image with
`geometry.toDisplay(_:)`. That call costs one extra bitmap copy per thumbnail,
and it throws `.operationFailed` when no drawing context can be made for the
turned size. `ThumbnailModel` makes the same call and caches the result, so a
thumbnail strip doesn't need it.

```swift
let geometry = page.geometry
let rendered = try await render.renderPage(
  page.page, options: RenderPageOptions(rotation: geometry.turn, width: 400))
let thumbnail = try geometry.toDisplay(try await render.renderThumbnail(page.page).image)
// A point on the page as shown, in PDF points.
let unrotatedPoint = geometry.toPage(CGPoint(x: 72, y: 36))
```

The conversions take CoreGraphics values too. `toPage(_:)` and `toDisplay(_:)`
have `CGPoint` and `CGRect` overloads beside the `Point` and `Rect` ones,
`displayCGSize` is the shown size as a `CGSize`, and `Point`, `Size` and `Rect`
convert to and from `CGPoint`, `CGSize` and `CGRect` through an unlabeled
initializer (`Sources/EmbedPDF/CoreGraphicsBridges.swift`).

`PageInfo.geometry` has only the page's own rotation. A stage can turn the
whole view as well (`setViewRotation`, `rotateViewBy`), so for a page a
`StageReaderView` shows, use `StageVisiblePage.geometry(for:)`, which includes
it.

## Text selection

`TextSelectionOverlay` selects text on one page by dragging and draws the
selected rects. Return it from a reader's page overlay, with a
`TextSelectionModel` made once per document:

```swift
let selection = TextSelectionModel(selection: try await viewer.selection(for: document))

// `pages` holds the document's `PageInfo` values, from `viewer.pages(of:)`.
StageReaderView(model: reader) { page in
  TextSelectionOverlay(model: selection, visiblePage: page, info: pages[Int(page.pageIndex)])
}
```

The model observes by itself, so `rects` also follows a selection made
elsewhere, such as `selectAll()`, and there is no `task()` to run. Pass
`start: .word` to begin from the word under the finger. Read `isSelecting` for
whether the drag found text and `error` for a failure.

With the default `activation: .drag`, the overlay takes every touch in its
frame, so the reader under it stops panning with one finger. That fits a
selection mode the app switches on. `activation: .longPress` leaves a plain
drag to the reader: a long press arms the selection, and it begins where the
finger is when it next moves. The cost is on the reader's side. Its drag now
waits for the long press to fail, so it reports at the first move or at the
lift, not on touch-down. A tap's drag then reaches the stage after the tap's
own effect has started and cancels it: a double tap's zoom, or the scroll to a
link's destination. Set `StageReaderModel.panBeginsOnMovement` to `true` on a
reader under a `.longPress` overlay, so a drag that hasn't moved opens no
stage gesture. With it on, a still touch or a tap no longer stops a fling; the
finger has to move. `PDFReaderView` uses this form and sets the switch itself;
see [A reader in one view](#a-reader-in-one-view).

To drive the model from your own gesture, call `dragBegan(at:on:start:)` with
the drag's first point, `dragChanged(to:on:)` with each later one and
`dragEnded()` when the finger lifts. The first two return at once and run in
the order they were made, and points that arrive while the engine is still
extending collapse into the newest one, so a fast drag costs one engine call
per extension, not one per frame. `dragEnded()` is `async` and returns whether
the drag began on text. SwiftUI can cancel a `DragGesture` without calling
`onEnded`, but it still resets `@GestureState`, so end the drag from an
`onChange` of such a flag too. The model takes unrotated page points and
`rects` holds them: `StageVisiblePage.pagePoint(_:info:)` converts a point in
a page overlay, and `localRect(_:info:)` converts a rect back for drawing.

## A reader in one view

`PDFReaderModel` starts a viewer, opens one document at a time and builds the
models a reader shows. `PDFReaderView` draws it: a progress view while it
loads, a password prompt for a protected document, the error's detail after a
failure, and for a ready document a `StageReaderView` with a `SearchBar` above
it.

```swift
import EmbedPDF
import EmbedPDFUI
import SwiftUI

struct ReaderScreen: View {
  let data: Data
  @State private var model = PDFReaderModel()
  @Environment(\.openURL) private var openURL

  var body: some View {
    PDFReaderView(
      model: model,
      onLinkActivation: { activation in
        if case let .uri(uri) = activation, let url = URL(string: uri) { openURL(url) }
      }
    )
    .task { await model.open(data, name: "report.pdf") }
  }
}
```

`open(_:name:password:)`, `open(_:name:)` for a URL, `unlock(password:)` and
`close()` never throw. The outcome is `phase`: `.idle`, `.loading`,
`.locked`, `.ready` with a `Session`, or `.failed` with the `PDFError`. A wrong
password leaves the document `.locked` and puts the reason in `unlockError`,
which the prompt shows under its field. A call made while an earlier `open` is
still loading supersedes it.

`PDFReaderModel(features:)` picks what the reader offers beyond showing and
scrolling pages: `.search`, `.selection`, `.tools` and `.links`, all four by
default. Each installs only the plugins it needs. The `Session` in `.ready`
holds the models for the open document: `reader`, and `search`, `selection`
and `tools` when their feature is on, plus the `viewer`, the `document` and
its `pages`. A session belongs to one document, so read it from `phase` each
time instead of keeping one.

Touch on a ready document divides like this. A drag pans, a pinch zooms and a
double tap zooms. With `.selection`, a long press followed by a drag selects
text, and a tap clears the selection. With `.links`, a tap on a link activates
it and the outcome reaches the view's `onLinkActivation`; opening a `.uri` is
the app's job. `.links` needs no other feature: the reader loads each page's
links when the page first comes into view. While a tool other than the default
is active, a drag goes to that tool instead of the pan, and text selection is
off until the default tool is back.

Long-press selection has two costs for the pan. With `.selection`, a pan
begins only once the finger has moved more than 10 points, so the first 10
points of a drag don't scroll. The pan starts from the point where the drag
crosses 10 points, so the page doesn't jump. A still touch or a tap also
doesn't stop a fling: the page keeps moving until the finger drags past the
10 points. A reader without `.selection` pans from touch-down and stops a
fling where the touch lands. `Tests/EmbedPDFUITests/StageReaderModelTests.swift`
pins both halves: `aTapThatMovesWithinTenPointsLeavesADoubleTapZooming` and
`withPanBeginsOnMovementThePanStartsAtTheCrossingWithNoJump` with
`StageReaderModel.panBeginsOnMovement` on, which is how `.selection` gets this
behavior, and `aZeroTranslationChangeBeginsAPanByDefault` with it off.

The view scrolls to each search hit the bar reports active. It draws no
highlight on hits. An app that wants one composes it from `SearchModel.hits`
in the `pageOverlay`. The view places no tool picker either: give
`Session.tools` to a `ToolPicker` where your layout wants one. It runs the
reader's and the search bar's `task()` itself, so don't run them again. The
reason differs by model. A second `SearchModel.task()` supersedes the first,
so the bar's own run stops updating the model. A second
`StageReaderModel.task()` supersedes nothing: both run, so the model handles
every stage event twice and registers the link tap handler twice.

The features cover search, selection, tools and links. For any other plugin,
pass `configure:`. It gets the `ViewerConfigBuilder` the model prepared for
its features and returns the one to start the viewer with, and the model calls
it for each viewer it starts. The demo adds the four plugins its panels need:

```swift
let model = PDFReaderModel(configure: { $0.metadata().history().outline().attachment() })
```

The added capabilities come from the session's viewer:

```swift
if case let .ready(session) = model.phase {
  let history = HistoryModel(
    viewer: session.viewer, history: try await session.viewer.history(for: session.document))
}
```

`configure` can also break the reader. Turning off a plugin a feature needs,
as `{ $0.selection(false) }` does with `.selection` on, makes every open end
in `.failed` with that capability's `unsupported` error. Leave the feature out
of `features` instead. One more cost of the model: it reads every page's
`PageInfo` at each open, which grows with the page count.

## Tools and links

A `StageReaderModel` made with an `InteractionCapability` sends the reader's
touches to the document's pointer hub, so the active tool draws. Made with a
`LinkCapability` too, it activates the link under a tap. It loads a page's
links when the page first comes into view, so there's no
`LinkCapability.ensureLoaded` for you to call. That costs one engine call per
page the first time it shows, and a tap that lands before its page's load has
finished finds no link. `ToolModel` mirrors the hub's tools for SwiftUI and
`ToolPicker` shows one toggle per tool.

```swift
let interaction = try await viewer.interaction(for: document)
let reader = StageReaderModel(
  stage: try await viewer.stage(for: document), render: try await viewer.render(for: document),
  interaction: interaction, link: try await viewer.link(for: document))
let tools = ToolModel(interaction: interaction)
```

```swift
VStack {
  StageReaderView(
    model: reader,
    onLinkActivation: { activation in
      if case let .uri(uri) = activation, let url = URL(string: uri) { openURL(url) }
    }
  )
  ToolPicker(model: tools, tools: [.ink, .highlight]) { id in
    Label(id.rawValue.capitalized, systemImage: "pencil")
  }
}
```

A tool's annotation appears when its gesture commits. An ink stroke shows when
the finger lifts, not while it moves: the reader draws no live preview.
`aForwardedDragUnderTheInkToolCreatesInk` in
`Tests/EmbedPDFModelsTests/StagePointerForwarderTests.swift` pins the commit:
one ink annotation after the lift.

Under the default tool a drag pans. Under any other tool the drag goes to that
tool, and the route is chosen when the drag starts. A tap reaches the hub once
either way. A pinch still zooms under a tool, and it cancels the drag its
first finger opened, so a pinch draws nothing. The built-in pan tool scrolls
only after `StageCapability.registerScrollHandler()`, which `PDFReaderModel`
calls for a reader with `.tools`.

Tapping a tool in the picker activates it, and tapping the active tool returns
to the default one. The picker runs the model's `task()`, so don't run it
again. The cost is in `ToolModel`: it asks `canUseTool` once per registered
tool after each state revision, 20 calls with the annotation plugin's tools,
and a scroll is a revision.

Each link activation arrives on `StageReaderModel.linkActivations` and on the
view's `onLinkActivation`. A link to a place in the document moves the stage
there and reports `.revealed`. A web link reports `.uri`, and the package
never opens it.

Link targets are typed as `AnnotationLinkTarget`: `.goto` with a
`PageDestination`, `.uri` or `.named`. On `AnnotationCapability.threads`,
`linkTarget(_:)` reads the link attached to an annotation and `setLink(_:on:)`
writes one.
`LinkCapability.resolve(_:)`, `activate(_:)` and `label(for:)` take the same
type, beside the JSON entry points they sit next to. A `.goto` destination is
in page space, measured from the top left of the destination page's visible
box with y pointing down.

## What the generated Swift gives the facade

Records conform to everything the facade needs to re-export them. uniffi 0.32
declares `public struct DocumentInfo: Equatable, Hashable` and
`public struct PageRef: Equatable, Hashable`, each followed by
`extension DocumentInfo: Sendable {}` inside `#if compiler(>=6)`. Every record
and enum in the three generated files has the same shape, so the facade needs
no `@retroactive @unchecked Sendable` lines.

Cancelling a Swift `Task` does not cancel the Rust call. The generated
`uniffiRustCallAsync` awaits each poll in a bare `withUnsafeContinuation`, with
no `withTaskCancellationHandler` around it, and only calls `freeFunc(rustFuture)`
from a `defer` once the future reports ready. The headers declare the
`ffi_*_rust_future_cancel_*` functions, but the generated Swift never calls
them. So the Rust future runs to completion, and abort-on-drop in `epdf-ffi`
never fires from Swift. Task 7's `searchCancelsWhenTheTaskIsCancelled` therefore
needs the plan's fallback: a `CancelToken` object passed to every
promise-returning export and cancelled from `withTaskCancellationHandler` in the
facade's `bridged`. The cost is one extra parameter on every such export.

## The smoke test

`Tests/EmbedPDFBindingsSmokeTests/SmokeTests.swift` asserts exactly what
`crates/epdf-uniffi/tests/native_surface.rs` asserts, deliberately. The Rust
test already covers the logic on the host; what only an iOS process can say is
that the XCFramework links, loads and runs there against the simulator slice of
the fork's PDFium. Keeping the assertions identical means a divergence between
the two is a distribution problem and nothing else.

Results, 2026-09-23, Xcode 27.0, iPhone 17 simulator (iOS 26.0): 8 tests, 0
failures, 0.28s.

Results, 2026-10-01, with the archive built from `epdf-viewer-host`: 8 tests, 0
failures, 0.60s.

This is a test bundle rather than an app with a screen. A bundle runs on the
simulator's real iOS runtime through the same dyld path an app uses, and it
runs unattended, which an app with a button does not. What it does not cover is
a physical device: code signing and the hardened runtime are only exercised
there, and Phase 1's device run still needs a device and a signing identity.

## One thing this shook out

`PdfError` carried `#[uniffi(flat_error)]`. Flattening keeps the fields on the
Rust side and lowers only the `Display` string, so `native_surface.rs` went on
matching `PdfError::Engine { code, .. }` while Swift received `case
Engine(message: String)` and no `code` at all. Every facade contract that says
"match on the code and prompt for a password" was unimplementable, and nothing
in the Rust test suite could notice. Removing the attribute gives Swift `case
Engine(code: String, detail: String)`.

The cost of removing it is that every field on every error variant now has to be
an FFI type, so an error can no longer carry an arbitrary Rust payload. That is
the right trade here and worth knowing before adding a variant.

## What `import EmbedPDF` exposes

`EmbedPDF` re-exports the generated records through one `public typealias` each
in `Exports.swift`, and Swift rejects that over an `internal import`: with
`internal import EmbedPDFBindings` the compiler reports "type alias cannot be
declared public because its underlying type uses an internal type", and a public
signature in `PDFViewer.swift` that uses an alias reports "'DocumentInfo' aliases
'EmbedPDFBindings.DocumentInfo' and cannot be used here because
'EmbedPDFBindings' was not imported publicly". So `Exports.swift` and
`PDFViewer.swift` use `public import EmbedPDFBindings`, and every other file
keeps `internal import`.

The cost is that the generated records, enums and callback protocols
re-exported by `Exports.swift` and `Plugins/*.swift` become API. `ViewerHost` and the plugin
handles don't resolve from `import EmbedPDF` alone; see "Products, and why the generated layer
isn't one". The supported API is
`PDFViewer` and the capabilities; anything else from the bindings module is
unsupported and can change with every regeneration.

The document record is `DocumentInfo`, the name the bindings give it. It was
`PDFDocument` until 2026-10-16, which PDFKit also declares, so any file that
imported both modules got "'PDFDocument' is ambiguous for type lookup in this
context". `PDFDocument` is still there as a deprecated typealias, to be removed in a
future release. The deprecated alias collides exactly as before: it keeps old code
compiling only in files that don't import PDFKit.

The annotation read record is `AnnotationRecord` for the same reason. It was
`Annotation`, and MapKit's SwiftUI overlay (iOS 17) declares a generic
`Annotation` view, so a file importing EmbedPDF, SwiftUI and MapKit could not
write `[Annotation]`. This one was renamed at the source,
`crates/plugins/annotation/src/ffi/mod.rs`, so the generated Swift and Kotlin
both say `AnnotationRecord`. The deprecated `Annotation` alias has the same
limit as `PDFDocument`.

Reads no longer hand out `AnnotationRecord` itself. `get(_:)`, `list(_:)`,
`selected()` and `hitTest(at:on:)` return `AnnotationInfo`, a hand-written
struct over it with `Date` timestamps, a typed `blendMode`, and typed members
for every subtype but popup and widget. `AnnotationInfo.record` gives the
generated record back.

Writes take the same types. Every subtype the engine creates from a field
bag has a typed draft (`UnderlineAnnotationDraft`, `LineAnnotationDraft`,
`FreeTextAnnotationDraft`, …) and a patch, and stamps and file attachments
have a patch only, since their create needs image or file bytes. The typed
layer leaves out two subtypes, popup and widget; the JSON-string overloads
still take those. A link
and a stamp have no `blendMode`, because the engine writes `/BM` while it
draws an appearance and draws neither (`write/update.rs` in
`crates/epdf-engine/src/features/annotation`). The cost is that each typed
member has to track the engine's schema. `AnnotationKindDraftTests` writes
every member through the engine and fails if one doesn't come back.

## Moving from 0.0.0-swift.4

Most renames since `0.0.0-swift.4` keep the old spelling as a deprecated
shim, so the compiler names the replacement. These changes have no shim and
need an edit:

- `PDFError.code` is a non-optional `PDFError.Code`, not `String?`.
  `error.code == "not-found"` still compiles, because a string literal is a
  code, and `if let code = error.code` doesn't. `error.code == nil` compiles
  with only a warning and is always false, so a branch behind it silently
  never runs: delete it. Prefer `catch PDFError.Code.notFound`.
- `AnnotationInfo`, the new read result, types what `AnnotationRecord` kept
  as text: `createdAt` and `modifiedAt` are `Date?` (the engine's text is
  `rawCreatedAt` and `rawModifiedAt`), `subtype` is an `AnnotationSubtype`,
  and ink, highlight and note colours are `AnnotationColor`. Code that names
  `AnnotationRecord` as the result type still compiles against a deprecated
  overload.
- Counts and indexes are `Int`. An untyped `let count = try await
  viewer.documentCount()` is now an `Int`, so arithmetic that mixed it with
  `UInt64` or `UInt32` needs a conversion. A value out of the engine's range
  throws `.invalidInput` instead of trapping.
- The read-side `intent` and `borderStyle` members of `AnnotationInfo`'s
  details (`AnnotationInfo.Ink.intent`, `AnnotationInfo.Ink.borderStyle` and
  the same members on the other kinds) are open types, `AnnotationIntent?`
  and `AnnotationInfo.BorderStyle`, not `String?` and `String`. Comparing with
  a string literal still compiles. Code that needs a `String` uses
  `.rawValue`. Interpolating the optional `intent`, or passing it to
  `String(describing:)`, now prints the intent without quotes:
  `Optional(ink-highlight)`, where it printed `Optional("ink-highlight")`.
- `AnnotationPatch` requires `Decodable` as well as `Encodable`, so
  `defaults(for:as:)` can read a tool's defaults typed. Every patch in the
  package conforms. Only a type outside the package that conforms to
  `AnnotationPatch` needs an `init(from:)`.
- An `AnnotationLinkTarget.goto` carries a page-space `PageDestination`, and
  `AnnotationInfo.Link.target` reads one. Before, a goto target dropped its
  coordinates.

The API that first appeared after `0.0.0-swift.7` was never tagged, and it
changed without shims too:

- `PageGeometry.renderRotation` is `turn`, and
  `PageGeometry.toDisplay(_:)` for a `CGImage` throws.
- `RenderRevisions.task()` and `TextSelectionModel.task()` are gone. Both
  models observe by themselves, so delete the call.
- `TextSelectionModel.dragBegan(at:on:start:)` is synchronous and returns
  nothing: call it without `await` or a `Task`. `dragEnded()` now returns
  whether the drag began on text.

The deprecated spellings, with their replacements:

| Deprecated | Use |
|---|---|
| `PDFViewer.stateRevisions`, `reportedErrors` | `observeStateRevisions()`, `observeReportedErrors()` |
| `PDFError(name:code:detail:)` with a `String?` code | `PDFError(_:detail:name:)` |
| `allows(_:in:)` with a `String` | a `DocumentPermission`, or a string literal |
| tool ids as `String` | a `ToolID`, or a string literal |
| annotation subtypes as `String` (`createMarkup`, `previewMarkup`) | an `AnnotationSubtype`, or `AnnotationSubtype(_:)` for a string |
| `activeToolId()`, `defaultToolId()` | `activeToolID()`, `defaultToolID()`, which return a `ToolID` |
| `reorder(_:to:)` with a JSON string | an `AnnotationPosition` |
| `Bool?` parameters whose `nil` meant the engine default | a plain `Bool`, or a `ContentInclusion` where the default follows the session's scope |
| `UInt32`, `UInt64` and `Int64` counts and indexes | `Int` |
| `SearchModel.activeIndex` | `activeHitIndex` |
| `StageReaderModel.currentPageIndex` | `pageIndex` |
| `AnnotationInfo.fieldsJson`, `groupId` | `fieldsJSON`, `groupID` |
| `get(_:)`, `list(_:)`, `selected()` and `hitTest(at:on:)` returning `AnnotationRecord` | the overloads returning `AnnotationInfo`; `AnnotationInfo.record` gives the record |
| `PDFViewer.render(in:)`, `search(in:)` | `render(for:)`, `search(for:)` |

The two renamed types go in a future release, not yet chosen, and their deprecation messages say so:

| Deprecated | Use | Removed in |
|---|---|---|
| `PDFDocument` | `DocumentInfo` | a future release |
| `Annotation` | `AnnotationRecord` | a future release |

Every other shim above is removed no earlier than 0.0.0-swift.6. The rule is
that a deprecated shim survives at least one tagged release: 0.0.0-swift.5
ships with all of them in place, so a project can build against it, read the
warnings and move over before anything is deleted. `render(in:)` and
`search(in:)` are the exception: they are deprecated as of the next tag after
0.0.0-swift.7 and survive at least that release. We haven't picked a release
for the overloads beyond that floor. `AliasRemovalTests` fails if `PDFDocument`
or `Annotation` is deleted early or its message stops saying it will be removed.

## Names that overlap Apple frameworks

We compiled a probe that imports `EmbedPDF` and an Apple framework and uses
each unqualified public type name in a type position. It covered the 600
top-level public names `EmbedPDF` and `EmbedPDFUI` declare or re-export,
including `Rect`, `Point`, `Size`, `PageInfo`, `Quad` and `Camera`, and the 687
further public names in `EmbedPDFBindings`, which `Exports.swift` imports
publicly. The frameworks were Foundation, CoreGraphics, CoreImage, CoreText,
SwiftUI, PDFKit, AppKit, UIKit (iOS 17 simulator SDK), MapKit, Combine,
Observation, UniformTypeIdentifiers, Security and Vision, then SwiftUI, MapKit,
PDFKit and AppKit together. The probe ran on Xcode 27.2 (Swift 6.4.2) against
a stub module that declares the same names, so it tests name lookup and not
the types themselves.

| Name | Framework | What happens | Workaround |
|---|---|---|---|
| `PDFDocument` (deprecated alias) | PDFKit | ambiguous for type lookup | `EmbedPDF.PDFDocument`, or use `DocumentInfo`; removed in a future release |
| `Annotation` (deprecated alias) | MapKit with SwiftUI (iOS 17, macOS 14) | ambiguous for type lookup | `EmbedPDF.Annotation`, or use `AnnotationRecord`; removed in a future release |
| every other name, including `Rect`, `Point`, `Size`, `PageInfo` | none | no clash | none needed |

No name collides with the Swift standard library. `Point`, `Size` and `Rect`
are not CoreGraphics' `CGPoint`, `CGSize` and `CGRect`, so they never meet
them. A clash only shows when a file imports both modules. When one does, write
the module name: `EmbedPDF.Rect`.

## The demo app

`EmbedPDFDemo/` is a small SwiftUI app over the one-call reader. A
`PDFReaderModel` opens `report.pdf` on launch and a `PDFReaderView` shows it,
with the package's search bar, long-press text selection and link taps. The
demo adds a `ToolPicker` with the default tool and ink, and a label with the
last link activation. Its model is made with
`configure: { $0.metadata().history().outline().attachment() }`, and those four
plugins back the rest: standard and custom document metadata, undo and redo
for the "Add note" button and the ink tool, bookmark navigation and editing,
and attachment import and export. Native Files sharing exposes its Documents
folder. The open menu takes a PDF from Files or one of two bundled samples.
`encrypted-hello.pdf` shows the reader's password prompt; its password is
`hôtel`.

The Xcode project is generated by Tuist from `Project.swift` and isn't checked in.
`mise.toml` pins the Tuist version. The demo builds against the engine the
package resolves. By default that's the pinned download, `engineRelease` at
`platforms/apple/EmbedPDF/Package.swift:9`, so nothing builds Rust first.
`EMBEDPDF_LOCAL_ENGINE=1` and `EMBEDPDF_PREBUILT_ENGINE=local` select a local
engine instead.

```sh
cd platforms/apple/EmbedPDFDemo
tuist generate
```

It signs ad hoc, which is enough for the simulator. A device run needs a development team
in `Project.swift`.

## Profiling the demo app

`EmbedPDF` emits `os_signpost` intervals under Points of Interest (subsystem
`com.embedpdf`): `open`, `pages`, `search`, `hits`, and `renderPage` split into
`renderPage.rust` (the call into Rust, including the kernel and engine threads) and
`renderPage.cgImage` (wrapping the pixels). They show up in any Instruments template that
has the Points of Interest track, Time Profiler included.

`EmbedPDFDemoUITests` is the scenario: it flings through `report.pdf` and back, searches
for "trace" and steps through ten hits. With `TEST_RUNNER_EMBEDPDF_PROFILE_DELAY=10` it
waits ten seconds after the document appears, long enough to attach Instruments to the
app's process. `scripts/profile.sh` does both:

```sh
cd platforms/apple/EmbedPDFDemo
tuist generate
xcodebuild build-for-testing -workspace EmbedPDFDemo.xcworkspace -scheme EmbedPDFDemo \
  -destination 'platform=iOS Simulator,name=iPhone 17' -derivedDataPath .build/dd
scripts/profile.sh "Time Profiler" time.trace
```

Attach to the process; don't record all processes. An `--all-processes` recording on the
simulator could not be symbolicated, and `XCUIApplication.activate()` relaunches an app
Instruments launched, so the traced process is not the one the test drives.

The simulator runs on the Mac's CPU and GPU. Trust its CPU profiles, allocations and
signpost durations; don't trust its frame times or hitch counts.

## The Nutrient comparison app

`NutrientDemo/` is the same app over Nutrient (PSPDFKit) 26.11.0: the same views,
accessibility identifiers, UI test and signpost names, with `renderPage.engine` standing in
for `renderPage.rust`. It renders through Nutrient's render queue with its default cache
policy and searches with `TextSearch`. It runs without a license key, so Nutrient draws its
evaluation watermark on every page. Generate it with `tuist generate` in `NutrientDemo/`
and profile it with `NutrientDemo/scripts/profile.sh`, exactly as above.

Nutrient keeps a disk cache of rendered pages across launches. Uninstall the app from the
simulator before a measured run (`xcrun simctl uninstall <sim> com.embedpdf.nutrientdemo`),
or its renders are mostly cache hits and the comparison is meaningless.


## History, bookmarks and embedded files

Construct one set of models per open document, and retain them in your host's
observable state. Each control owns its observation with `.task`; changing its
model identity cancels the old subscription. All three can observe one viewer
without consuming each other's revisions. The deprecated `stateRevisions`
property is a single-consumer stream; create `observeStateRevisions()` for every
independent observer and subscribe before reading its initial snapshot.

```swift
import EmbedPDF
import EmbedPDFUI
import SwiftUI

@MainActor
struct PluginReader: View {
  let reader: StageReaderModel
  let history: HistoryModel
  let outline: OutlineModel
  let attachments: AttachmentModel

  var body: some View {
    TabView {
      StageReaderView(model: reader)
        .tabItem { Label("Reader", systemImage: "doc") }
      OutlinePanel(model: outline)
        .tabItem { Label("Bookmarks", systemImage: "bookmark") }
      AttachmentPanel(model: attachments)
        .tabItem { Label("Attachments", systemImage: "paperclip") }
    }
    .safeAreaInset(edge: .bottom) {
      HistoryControls(model: history).padding()
    }
  }
}

// In the host's document-open task:
let viewer = try PDFViewer(ViewerConfig(
  render: RenderConfig(), interaction: true, stage: StageConfig(),
  annotation: "", history: true, outline: true, attachment: true))
try await viewer.start()
let document = try await viewer.open(pdfData)
let screen = PluginReader(
  reader: StageReaderModel(stage: try await viewer.stage(for: document),
    render: try await viewer.render(for: document)),
  history: HistoryModel(viewer: viewer, history: try await viewer.history(for: document)),
  outline: OutlineModel(viewer: viewer, outline: try await viewer.outline(for: document)),
  attachments: AttachmentModel(viewer: viewer, attachments: try await viewer.attachments(for: document)))
```

Enable annotation support to include page file-attachment annotations alongside
document embedded files. Bookmark activation reveals through the installed stage;
`OutlinePanel` also accepts `onActivation` for results the host must handle, such
as external addresses. Swift does not calculate destinations or rebuild trees.
An omitted outline patch property preserves it; use `clear: [.target, .color]`
to remove those values explicitly. Unknown PDF action data remains on the item.

History undo/redo returns when submitted, not when confirmed. Observe
`HistoryCapability.events` for completion/refusal and skipped parts. Empty
history can return without an event; `HistoryControls` clears its submission
indicator as soon as the call returns. Configure its global limit through
`updateSettings`; optional `I18nCapability` translates change labels in the model.

Attachment inputs and downloads use owned `Data`, preserving zero-byte files,
Unicode names and MIME metadata. File selection reads off the main actor and
balances security-scoped URL access. Export begins only after an explicit Save
copy action and removal asks for confirmation. Arbitrary attachment bytes export
as the FileDocument's declared `.data` type, with the original suggested filename
and extension. The API retains MIME metadata; it never executes a downloaded file.

Capability calls preserve `PDFError` codes and cooperative cancellation. Close
finishes document subscriptions; destroy finishes viewer revision subscriptions.
Cached snapshots in controls refresh on revisions/events, and a generation guard
rejects stale or cancelled reads. Reopening a document creates new capabilities
and models. Panel editor, confirmation and file-operation state is scoped to
model identity, so swapping models disposes pending presentations and tasks.
`StageReaderModel` observes Rust render invalidations; `StageReaderView` passes
per-page revisions to `PageImageView` so document edits, undo and redo refresh
visible pixels. Direct `PageImageView` hosts can pass its defaulted `revision`
parameter when their page is invalidated. The host owns `close`, `destroy`, and
handling locked tabs/passwords.

## Audited API coverage

`swift-api-coverage.json` accounts for 860 public members across 31 Rust plugin-directory
crates and the kernel: 724 mapped members and 136 explicit exclusions. The nine
pure libraries are classified as owner-consumed code without fictional plugin APIs.
The checker inventories public kernel `Documents` operations and `Kernel` methods,
including viewer settings; internal kernel types are classified separately.
These are accounting counts, not an implementation parity percentage or a count
of Swift methods. Open form payload overloads are checked alongside typed conveniences,
matched by Swift argument label so removing one overload cannot hide behind the other.

Delivered workflows include document lookup/order/retry/URL loading and layer
save/open, live settings/read/update/reset for 13 plugins, command disabling and
refusal details, named-action permission checks, annotation bundle import/export
and selected-link edits, independent form widget edits/deletion and calculation
order, snapshots with cancellable owned images, and native zoom boxes. Rust owns
permissions, registration reset values, geometry and document lifetime.

Form authoring keeps its typed convenience methods and accepts the engine's open
payloads through `create(draftJSON:)`, `update(_:patchJSON:)` and
`addWidget(to:placementJSON:)`. A patch's omitted members preserve values;
explicit null removes nullable values. Field script writes use
`{"type":"javascript","script":"event.value = 7"}` inside their event,
rather than the read tree's `root` wrapper. `fieldSettings(_:)` returns typed
family-specific panel descriptors and current values JSON. Malformed JSON or a
non-object payload refuses with `invalid-input` before mutation.

Remaining exclusions name concrete resources or host integrations in the inventory:
Rust caches and ownership handles, painter/appearance and text-editor/pointer
presentation seams, scripting sibling ports, equivalent JSON settings forms, and
startup registration helpers. Printing still requires a native `HostPrinter`
with prepared PDF/picture representations and disposal lifetime; it is not
implemented. Observe current settings through `observeStateRevisions()` and a
fresh settings read. Native rendering uses owned images and render view demand.

The [validation report](../../docs/superpowers/reports/2026-11-09-swift-app-api-coverage.md)
records exact source-engine results. Source commits do not publish an engine:
matching downloaded artifacts and both package pins still need a separate release.

```sh
python3 platforms/apple/check-swift-api-coverage.py
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s platforms/apple/tests -p test_swift_api_coverage.py
```

For local binary verification, first run `package-engine-framework.sh` after
building the source engine, then run the same package tests with
`EMBEDPDF_PREBUILT_ENGINE=local` and `EMBEDPDF_LOCAL_ENGINE` unset. This verifies
the compiled Swift module in addition to the Rust archive. The release workflow
owns release names, hashes and checksums:

```sh
node scripts/publish-engine-payload.mjs --dry-run --only apple
```

The dry run builds and verifies a concrete payload without publishing or changing
engine pins. A real release and updating both manifests are separate authorized
publishing work. Never attach a new checksum to the old release tag.
