# Android Parity Phase 2: Facade Core

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** A hand-written `embedpdf` module whose `PdfViewer` does everything Swift's `PDFViewer` does, in Kotlin idiom, tested on the JVM against the Linux host library.

**Architecture:** `platforms/android/embedpdf/embedpdf` is an Android library (`namespace = "com.embedpdf"`) with `api(project(":bindings"))`. Every public type lives in package `com.embedpdf`. Generated records the API passes through are re-exported with `public typealias X = uniffi.<component>.X`. Calls bridge through two internal helpers, `bridged {}` and `bridgedCancellable {}`, that map generated exceptions to `PdfException` and coroutine cancellation to `CancelToken.cancel()`.

**Tech Stack:** as Phase 1, plus `kotlinx-coroutines-core` 1.10.2 (`api`), `kotlinx-coroutines-test`, `org.jetbrains.kotlinx.binary-compatibility-validator` 0.18.x.

**Spec:** `docs/superpowers/plans/2026-11-19-android-parity.md` Phase 2 and D2/D5. The Swift sources named below are the behavioural reference (read them in the container at `platforms/apple/EmbedPDF/Sources/EmbedPDF/`); each Kotlin doc comment carries the same contract as its Swift counterpart.

## Global Constraints

- Every call in `PdfViewer` is a `suspend fun` that throws only `PdfException` (and `CancellationException`).
- Public integers are `Int` or `Long`. `UInt`, `ULong`, `UShort` never appear in a `com.embedpdf` signature; out-of-range input throws `PdfException` with code `INVALID_INPUT`.
- No `Boolean?` parameters meaning "engine default": write the default.
- Flows: each `observe*()` returns a cold `Flow` that installs its own subscription on collection and completes when the viewer is destroyed.
- `PdfViewer` implements `AutoCloseable`; `close()` is a blocking best-effort release for `use {}`. `destroy()` is the suspend teardown.

## Review Focus

- Cancelling the collecting coroutine of `open(url)` cancels the Rust token and closes the loading tab (test with a loopback server that never answers).
- Two concurrent collectors of `observeStateRevisions()` both receive revisions.
- An exception thrown inside a Kotlin `DocumentFetcher` reaches `observeReportedErrors()` and doesn't crash the host thread.
- `page(at = -1)` and `page(at = Int.MAX_VALUE)` return `null`; nothing traps on conversion.
- `destroy()` twice: the second call is a no-op or throws `INSTANCE_CLOSED`, matching Swift.

---

### Task 1: Module, `PdfException`, bridging helpers

**Files:**
- Create: `embedpdf/build.gradle.kts`, `embedpdf/src/main/kotlin/com/embedpdf/PdfException.kt`, `Bridging.kt` (internal), `embedpdf/src/test/kotlin/com/embedpdf/PdfExceptionTest.kt`
- Modify: `settings.gradle.kts`, `gradle/libs.versions.toml`

**Interfaces (produces):**
```kotlin
class PdfException(val code: Code, val name: String, val detail: String, cause: Throwable? = null) : Exception
@JvmInline value class Code(val value: String) { companion object { val PERMISSION_DENIED; UNSUPPORTED; NOT_FOUND; NOT_READY; INVALID_INPUT; CONFLICT; INSTANCE_CLOSED; OPERATION_CANCELLED; OPERATION_FAILED; INVALID_STATE } }
internal suspend fun <T> bridged(block: suspend () -> T): T
internal suspend fun <T> bridgedCancellable(block: suspend (CancelToken) -> T): T
```
Codes and their wire strings come from `PDFError.swift` (`PDFError.Code` constants); the set is open like Swift's, so `Code` is a value class, not an enum.

- [x] Tests: each generated error variant maps to the right `code`, `name` and `detail` (port `PDFErrorTests.swift`); `bridgedCancellable` calls `token.cancel()` when the coroutine is cancelled.
- [x] Implement, run `./gradlew :embedpdf:testDebugUnitTest`, commit.

### Task 2: `PdfViewer` lifecycle, documents, pages, settings

**Files:**
- Create: `PdfViewer.kt`, `ViewerConfigBuilder.kt`, `DocumentPermission.kt`, `Exports.kt`, `WireInteger.kt` (internal), `StateRevisionHub.kt` and `EventRelay.kt` (internal), `Registration.kt`, `ContentInclusion.kt`, `Experimental.kt` (`@RequiresOptIn` annotation `ExperimentalEmbedPdfApi`), `Tracing.kt` (internal, `androidx.tracing` `trace()` around open/search/render, port of `Signposts.swift`)
- Test: `ViewerTest.kt`, `DocumentApiTest.kt`, `RevisionHubTest.kt`, `StreamRuleTest.kt`, `IntegerSurfaceTest.kt`, `ViewerConfigBuilderTest.kt`

**Interfaces (produces):** every public member of `PDFViewer.swift`, renamed to Kotlin idiom. Examples that fix the pattern:
```kotlin
class PdfViewer(config: ViewerConfig, documentFetcher: DocumentFetcher = HttpDocumentFetcher()) : AutoCloseable
suspend fun start(); suspend fun destroy()
suspend fun open(bytes: ByteArray, name: String? = null, password: String? = null, activate: Boolean = true): DocumentInfo
suspend fun open(url: java.net.URL, name: String? = null, password: String? = null, activate: Boolean = true): DocumentInfo
suspend fun open(base: ByteArray, layer: ByteArray, name: String? = null, password: String? = null, activate: Boolean = true): DocumentInfo
suspend fun page(atIndex: Int, document: DocumentInfo? = null): PageInfo?
suspend fun allows(permission: DocumentPermission, document: DocumentInfo? = null): Boolean
suspend fun save(document: DocumentInfo, mode: SaveMode = SaveMode.INCREMENTAL): ByteArray
fun observeStateRevisions(): Flow<Long>
fun observeReportedErrors(): Flow<PdfException>
fun observeDocumentEvents(): Flow<DocumentEvent>
class ViewerConfigBuilder { fun metadata(enabled: Boolean = true): ViewerConfigBuilder; ...; fun build(): ViewerConfig }
fun viewerConfig(block: ViewerConfigBuilder.() -> Unit): ViewerConfig
@JvmInline value class DocumentPermission(val value: String) { companion object { OPEN = "doc.open", ... } }
```
Deprecated Swift spellings are not ported.

- [x] Port each listed Swift test file's cases; they are the spec. Fixtures from `pdfium-multiplatform-prototype/fixtures/` and the Apple test fixtures generator (check `platforms/apple/build-xcframework.sh` for which fixtures it copies).
- [x] Implement until green; commit per file group.

### Task 3: Document fetchers

**Files:**
- Create: `HttpDocumentFetcher.kt` (`HttpURLConnection`, port of `DocumentFetch.swift` contract: cancellation, status and non-PDF handling), `ContentResolverDocumentFetcher.kt` (`content://` and `file://` URIs through a `ContentResolver`, delegating other schemes to an inner fetcher), `PdfViewer.open(uri: android.net.Uri, ...)` overload
- Test: `FetcherTest.kt` with an in-process loopback HTTP server (port `LoopbackServer.swift`), `ContentResolverFetcherTest.kt` under Robolectric

- [x] Tests first: 200 PDF opens; 404 leaves a failed tab that `retry` can reopen; non-PDF body fails; cancelling the collecting coroutine during a stalled response closes the tab (Review Focus 1); an exception thrown by a custom fetcher is reported, not fatal (Review Focus 3).
- [x] Implement, commit.

### Task 4: API coverage and API dump

**Files:**
- Create: `scripts/check-api-coverage.py` (generalized from `platforms/apple/check-swift-api-coverage.py`; `--platform swift` keeps the exact current behaviour and output), `platforms/android/kotlin-api-coverage.json`
- Modify: `platforms/apple/check-swift-api-coverage.py` (becomes a thin wrapper calling the shared script with `--platform swift`), `build-aar.sh` (runs `--platform kotlin` and `./gradlew apiCheck`)

- [x] The Kotlin manifest lists every member the Swift manifest lists, with `kotlin` set to the Kotlin symbol for members ported in this phase and `"pending": "phase-3-<wave>"` for the rest. The check passes when every non-pending member's symbol is found as `fun <name>` / `val <name>` in `platforms/android/embedpdf/*/src/main/kotlin`.
- [x] `python3 platforms/apple/check-swift-api-coverage.py` output is byte-identical before and after.
- [x] Add the binary-compatibility-validator plugin, run `./gradlew apiDump`, commit `embedpdf/api/embedpdf.api`.
- [x] Commit.
