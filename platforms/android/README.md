# Android

The Android package exposes the Rust viewer through typed Kotlin capabilities,
observable models and Compose views, on Android 7.0 (API 24) or later, for
`arm64-v8a` and `x86_64`.

It ships as five Maven artifacts under the group `com.embedpdf`. Each release
is the prerelease `0.0.0-android.<n>` of this repository, with the five
artifacts in one zip that is itself a Maven repository, so a build reads them
with no token. Nothing is released yet: `0.0.0-android.1` will be the first,
and until then an app builds against the libraries from this checkout, as the
demo app does by default.

## Using the package

### Requirements

| | |
|---|---|
| `minSdk` | 24 or later |
| `compileSdk` | 36 or later |
| ABIs | `arm64-v8a`, `x86_64` |
| Built and checked with | AGP 9.5.2, Gradle 9.8.2, Kotlin 2.2.21, JDK 17 bytecode |
| Compose BOM in the app | 2026.06.01 or earlier (Compose UI 1.11.4, Material 3 1.4.0) |

The demo app builds with exactly the versions above. Older AGP and Kotlin
versions were not tried. Kotlin 2.2.21's standard library comes in through
the POMs, and a Kotlin compiler older than 2.1 can't read the libraries'
metadata. A Compose BOM from 2026.08.00 on resolves Compose 1.12, which needs
`compileSdk` 37, and `embedpdf-compose` hasn't been tested against it.

### Which artifacts

| App | Depend on | Brings |
|---|---|---|
| Compose | `embedpdf-compose` | `embedpdf-models`, `embedpdf`, `embedpdf-bindings` |
| Views, or Compose drawn your own way | `embedpdf-models` | `embedpdf`, `embedpdf-bindings`, no Compose |
| Engine and capabilities only | `embedpdf` | `embedpdf-bindings` |
| PDF JavaScript, any of the above | add `embedpdf-javascript` | `androidx.javascriptengine` 1.1.1 |

`embedpdf-bindings` is versioned by engine, `0.0.0-engine.<hash>`, and the
others by release, so depend on the release version and let the POMs pick the
engine. `androidx.javascriptengine` declares `minSdk` 26. The
`embedpdf-javascript` manifest carries
`tools:overrideLibrary="androidx.javascriptengine"`, so an app at `minSdk` 24
merges it without a line of its own, and JavaScript reports itself
unavailable below API 26.

### From the release zip (recommended)

Each release carries `embedpdf-android-maven-<version>.zip` and its
`.sha256`. The zip's root is a Maven repository: all five artifacts, a
`maven-metadata.xml` per artifact and the checksums a remote repository
serves. Download it, check it and unzip it into the app's tree:

```bash
V=0.0.0-android.1
base=https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/releases/download/$V
curl -fLO "$base/embedpdf-android-maven-$V.zip" -fLO "$base/embedpdf-android-maven-$V.zip.sha256"
sha256sum -c "embedpdf-android-maven-$V.zip.sha256"   # shasum -a 256 -c on macOS
unzip -q "embedpdf-android-maven-$V.zip" -d third_party/embedpdf-maven
```

Then name that directory as a repository in `settings.gradle.kts`:

```kotlin
dependencyResolutionManagement {
    repositories {
        google()
        mavenCentral()
        maven {
            url = uri(settingsDir.resolve("third_party/embedpdf-maven"))
            content { includeGroup("com.embedpdf") }
        }
    }
}
```

and depend on it in the module:

```kotlin
dependencies {
    implementation("com.embedpdf:embedpdf-compose:0.0.0-android.1")
    // Only for PDF JavaScript:
    implementation("com.embedpdf:embedpdf-javascript:0.0.0-android.1")
}
```

The cost is size in the app's repository: the zip holds both ABIs' engine and
its native debug symbols. Commit the unzipped directory, keep it in Git LFS,
or download it in CI before the build. `release-android.yml` builds the demo
from the unzipped zip, with these settings, before every release.

### From GitHub Packages

A release may also go to a GitHub Packages Maven registry (the train's
`android_packages_repository` input; the release notes say whether it did).
GitHub Packages asks for a token even to read a public package. Every machine
and CI job that resolves the artifacts needs a classic personal access token
with `read:packages`, or the job's `GITHUB_TOKEN` when the build runs in a
workflow of the repository that holds the packages. With `OWNER/REPO` that
repository:

```kotlin
dependencyResolutionManagement {
    repositories {
        google()
        mavenCentral()
        maven {
            url = uri("https://maven.pkg.github.com/OWNER/REPO")
            content { includeGroup("com.embedpdf") }
            credentials {
                username = providers.gradleProperty("gpr.user").orNull ?: System.getenv("GITHUB_ACTOR")
                password = providers.gradleProperty("gpr.key").orNull ?: System.getenv("GITHUB_TOKEN")
            }
        }
    }
}
```

Put `gpr.user` and `gpr.key` in `~/.gradle/gradle.properties`, not in the
project. The dependencies are the same as for the zip.

### From source

Build the engine, then publish to Maven Local at a version of your choosing:

```bash
bash platforms/android/build-aar.sh
cd platforms/android/embedpdf
./gradlew publishToMavenLocal -Pembedpdf.version=0.0.0-mine.1
```

and add `mavenLocal { content { includeGroup("com.embedpdf") } }` to the
app's repositories. Or include the library build and substitute the
coordinates, as `platforms/android/EmbedPDFDemo/settings.gradle.kts` does, so
a library change shows up in the app's next build:

```kotlin
includeBuild("../embed-pdf-viewer/platforms/android/embedpdf") {
    dependencySubstitution {
        substitute(module("com.embedpdf:embedpdf-bindings")).using(project(":bindings"))
        substitute(module("com.embedpdf:embedpdf")).using(project(":embedpdf"))
        substitute(module("com.embedpdf:embedpdf-javascript")).using(project(":javascript"))
        substitute(module("com.embedpdf:embedpdf-models")).using(project(":models"))
        substitute(module("com.embedpdf:embedpdf-compose")).using(project(":compose"))
    }
}
```

`build-aar.sh` needs NDK r29, JDK 21 and the Rust toolchain
`rust-toolchain.toml` pins; [Local engine mode](#local-engine-mode) has the rest.

### Compose quick start

`PdfReaderModel` starts a viewer with the plugins its features need, plus
whatever `configure` adds, and `PdfReader` draws it. Hold the model in a
`ViewModel` so a rotation keeps the document:

```kotlin
import android.app.Application
import android.net.Uri
import android.util.Log
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.compose.viewModel
import com.embedpdf.ToolId
import com.embedpdf.compose.PdfReader
import com.embedpdf.models.PdfReaderModel
import kotlinx.coroutines.DelicateCoroutinesApi
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.GlobalScope
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.net.URL

class ReaderViewModel(application: Application) : AndroidViewModel(application) {
    val reader = PdfReaderModel(
        configure = { it.outline().attachment().history() },
        actionScope = viewModelScope,
    )

    fun openBytes(bytes: ByteArray) = viewModelScope.launch { reader.open(bytes, name = "report.pdf") }

    fun openUrl(url: String) = viewModelScope.launch { reader.open(URL(url)) }

    // PdfReaderModel opens bytes or a URL; read a content Uri yourself.
    fun openUri(uri: Uri) = viewModelScope.launch {
        val bytes = withContext(Dispatchers.IO) {
            getApplication<Application>().contentResolver.openInputStream(uri)?.use { it.readBytes() }
        } ?: return@launch
        reader.open(bytes)
    }

    // The scope is already cancelled here; close destroys the viewer.
    @OptIn(DelicateCoroutinesApi::class)
    override fun onCleared() {
        GlobalScope.launch(Dispatchers.Main) { reader.close() }
    }
}

@Composable
fun ReaderScreen(model: ReaderViewModel = viewModel()) {
    PdfReader(
        model.reader,
        Modifier.fillMaxSize(),
        tools = listOf(ToolId.POINTER, ToolId.INK),
        onError = { error -> Log.w("Reader", error.detail) },
    )
}
```

`viewModel()` comes from `androidx.lifecycle:lifecycle-viewmodel-compose`
2.9.4, which the app adds itself.
`open` never throws a `PdfException`: a failure is `PdfReaderModel.Phase.Failed`,
which `PdfReader` shows and passes to `onError`. A locked document shows a
password prompt. The demo's
`platforms/android/EmbedPDFDemo/app/src/main/kotlin/com/embedpdf/demo/DocumentViewModel.kt`
is the same shape with a document picker, a metadata sheet and a note button.

### View-based quick start

`embedpdf-models` has no Compose. `PdfViewerViewModel` holds one `PdfViewer`
across configuration changes, started when it is created and destroyed in
`onCleared()`. The viewer's `DocumentFetcher` carries URL and `Uri` opens;
`ContentResolverDocumentFetcher` reads `content:` Uris and hands anything else
to `HttpDocumentFetcher`:

```kotlin
import android.os.Bundle
import android.util.Log
import android.widget.ImageView
import androidx.activity.ComponentActivity
import androidx.activity.viewModels
import androidx.lifecycle.lifecycleScope
import com.embedpdf.ContentResolverDocumentFetcher
import com.embedpdf.PdfException
import com.embedpdf.RenderConfig
import com.embedpdf.RenderPageOptions
import com.embedpdf.SearchConfig
import com.embedpdf.ViewerConfig
import com.embedpdf.models.PdfViewerViewModel
import com.embedpdf.render
import kotlinx.coroutines.launch
import java.net.URL

class ReaderActivity : ComponentActivity() {
    private val holder: PdfViewerViewModel by viewModels {
        PdfViewerViewModel.factory(
            ViewerConfig(render = RenderConfig(), search = SearchConfig(), metadata = true),
            ContentResolverDocumentFetcher(applicationContext.contentResolver),
        )
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val page = ImageView(this).also(::setContentView)
        lifecycleScope.launch {
            try {
                val viewer = holder.startedViewer()
                val uri = intent.data
                // A Uri, a URL, or bytes: viewer.open(bytes, name = "report.pdf").
                val document = if (uri != null) viewer.open(uri) else viewer.open(URL("https://example.com/report.pdf"))
                val first = viewer.pages(document).first()
                val rendered = viewer.render(document).renderPage(first.page, RenderPageOptions(scale = 1.0))
                page.setImageBitmap(rendered.image.toBitmap())
            } catch (e: PdfException) {
                Log.w("Reader", e.detail)
            }
        }
    }
}
```

`viewModels` and `lifecycleScope` come from `androidx.activity:activity`
1.10.1, which the app adds itself. Unlike the model, `PdfViewer` throws
`PdfException`. A locked document opens
with its state `LOCKED`, not as an error. For more than one page image, build
the models over the viewer's capabilities (`StageReaderModel`,
`TiledPageModel`, `SearchModel` and the rest under [Models](#models)) and
draw their state with your own Views.

### Manifest, R8, symbols and size

The libraries' manifests ask for no permissions. Opening a URL needs
`<uses-permission android:name="android.permission.INTERNET" />` in the app,
and an `http://` URL also needs the app's network security config to allow
cleartext traffic to that host; since API 28 it is off by default. Bytes and
`content:` Uris need neither.

R8 needs nothing from the app. `embedpdf-bindings` ships consumer rules that
keep JNA and every generated `uniffi.*` class, which JNA reaches by
reflection. `android-consumer.sh` checks that on a minified build of the
demo before each release ([The demo app](#the-demo-app)).

The engine ships stripped. Its unstripped libraries are the
`native-debug-symbols` artifact of `embedpdf-bindings`, in the zip and in
GitHub Packages, for the store console or `ndk-stack`; [Native crash
symbols](#native-crash-symbols) has the Gradle line.

The engine is large, and stored uncompressed so it maps straight out of the
APK on a 16 KB page device:

| ABI | `libepdf_viewer_host.so` | `libc++_shared.so` | per-ABI total |
|---|---|---|---|
| `arm64-v8a` | 39,838,344 bytes | 1,374,336 bytes | 41,212,680 bytes |
| `x86_64` | 45,100,200 bytes | 1,337,488 bytes | 46,437,688 bytes |

A universal APK carries both: the demo's minified release APK is
92,403,389 bytes. Ship an App Bundle and the store delivers one ABI per
device. For APKs, split by ABI:

```kotlin
android {
    splits {
        abi {
            isEnable = true
            reset()
            include("arm64-v8a", "x86_64")
            isUniversalApk = false
        }
    }
}
```

or keep only `arm64-v8a` with `defaultConfig { ndk { abiFilters += "arm64-v8a" } }`,
at the cost of x86_64 emulators and devices. Keep
`packaging { jniLibs { useLegacyPackaging = false } }` (the default at
`minSdk` 23 and above), and run `zipalign -c -P 16 4` on the APK you ship:
[What the script checks](#what-the-script-checks) explains why.

## Products

`platforms/android/embedpdf` is one Gradle build with a wrapper (Gradle 9.8.2)
and a version catalog, `gradle/libs.versions.toml`. Its five library modules
are the five artifacts; `smoke` is a test module.

| Module | Artifact | What it is | Apple counterpart |
|---|---|---|---|
| `bindings` | `embedpdf-bindings` | `libepdf_viewer_host.so` and `libc++_shared.so` per ABI, the generated `uniffi.*` Kotlin and the shrinker rules | `EmbedPDFBindings` |
| `embedpdf` | `embedpdf` | the hand-written `com.embedpdf` facade: `PdfViewer`, `PdfException`, a capability per plugin, the document fetchers | `EmbedPDF` |
| `javascript` | `embedpdf-javascript` | `com.embedpdf.javascript.JavaScriptSandboxEngineFactory`, PDF JavaScript in `androidx.javascriptengine` | `JavaScriptCoreEngine` in `EmbedPDF` |
| `models` | `embedpdf-models` | `com.embedpdf.models`: observable models over the facade, with no Compose dependency | `EmbedPDFModels` |
| `compose` | `embedpdf-compose` | `com.embedpdf.compose`: Compose views over the models, with Material 3 | `EmbedPDFUI` |
| `smoke` | none | JVM tests of the engine-only `epdf-uniffi` crate | none |

`compose` depends on `models`, `models` and `javascript` on `embedpdf`, and
`embedpdf` on `bindings`, each as `api`, so `embedpdf-compose` alone brings
everything but `embedpdf-javascript`. The generated `uniffi.*`
types are visible to an app through that chain, because the facade's
typealiases name them. They are not supported API: the API dumps cover
`com.embedpdf.*` only, and a Rust change can rename a generated type without
notice. Apple made the same call on 2026-11-10 for the same reason.

`javascript` is a module of its own so that only an app that wants PDF
JavaScript takes `androidx.javascriptengine`. That library declares minSdk 26
and ours 24, so the module's manifest carries
`tools:overrideLibrary="androidx.javascriptengine"` and the factory reports
JavaScript unavailable below API 26 without touching the library. An app with
minSdk 24 that depends on `:javascript` merges its manifest without a line of
its own: a throwaway app module checked that on 2026-11-20 with a project
dependency. The facade sees the factory only through
`ScriptEngineAvailability`, an opt-in hook (`@EmbedPdfEngineIntegration`) that
lets the viewer disable JavaScript and report why.

`compose` builds against Compose BOM 2026.06.01 (Compose UI 1.11.4, Material 3
1.4.0), the newest that compiles against `compileSdk` 36. From 2026.08.00 on,
Compose 1.12 requires `compileSdk` 37 of every app that uses it.

So an app that uses `compose` needs `compileSdk` 36 or later, and can move its
own Compose BOM up to 2026.06.01 without touching that floor. A newer BOM in
the app resolves Compose 1.12, which raises the app to `compileSdk` 37, and
the module has not been tested against it. The module takes
the BOM as an `implementation` platform. That keeps it off the app's compile
classpath, but its constraints do reach the app's runtime classpath, where
they can raise the app's Compose artifacts to the BOM's versions; they never
lower them. Compose UI, its graphics and Foundation are `api`, because the
public signatures use `Modifier`, `Color` and `Orientation`, and Material 3
and its adaptive layouts are `implementation`.
`./gradlew :compose:checkApiDependencies`, part of `check`, fails if the BOM
or any artifact in a group under `androidx.compose.material3` becomes an
`api` dependency.

The facade is written for Kotlin callers. Calling it from Java isn't a goal,
so it carries no `@JvmName` or `@JvmOverloads` adjustments.

`embedpdf/api/embedpdf.api` is the facade's public surface, and
`javascript/api/javascript.api` the javascript module's. `./gradlew apiCheck`
fails when the code and a dump disagree, and `./gradlew apiDump` rewrites the
dumps; a change to the surface commits both. The tasks are registered in each
module's `build.gradle.kts` rather
than by the binary-compatibility validator plugin, because the plugin only
wires itself to the Kotlin Android plugin and AGP 9 compiles Kotlin without
it. `python3 scripts/check-api-coverage.py --platform kotlin` checks
`kotlin-api-coverage.json`: every member the Swift manifest lists is either
ported, with the Kotlin symbol it maps to, or `pending` with the phase that
ports it. `build-aar.sh` runs both.

## Where the engine comes from

`bash platforms/android/build-aar.sh` builds `epdf-viewer-host`, the full
viewer host with every plugin, for `aarch64-linux-android` and
`x86_64-linux-android` against NDK r29 at API 24. It links the fork's
`libembedpdf.a` for `android-arm64` and `android-x64`, which
`fetch-android-runtime.sh` downloads from the `runtime-<sha>` release for the
commit `packages/engine/runtime/runtime-src` pins (`af480d0ce` today). Then
it generates the Kotlin for every uniffi component from the arm64 library
with the repository's `crates/uniffi-bindgen`, and packages both into
`bindings/src/main`. Neither the `.so` files nor the Kotlin are committed.

`embedpdf-bindings` is versioned by engine, not by release:
`0.0.0-engine.<hash>`, where `<hash>` is the pin
`embedpdf.engineRelease=android-engine-<hash>` in
`platforms/android/embedpdf/gradle.properties`. A facade or UI release whose
engine didn't change reuses the engine already published. The publish task of
`:bindings` runs `android-engine-hash.sh` and refuses to publish when the tree
builds another hash than the pin names, so a version always names the sources
it was built from. The other four artifacts take `embedpdf.version`
(`0.0.0-local` in `gradle.properties`; `release-android.yml` passes its
`version` input).

The pin goes stale whenever an input to the hash changes. [Engine
identity](#engine-identity) lists the inputs; the Gradle build files and the
version catalog are among them, so a dependency bump moves the engine version
too. `bash platforms/android/check-android-engine.sh` exits 0 when the pin is
current. When it isn't, the script asks GitHub Packages for the POM of
`com.embedpdf:embedpdf-bindings:0.0.0-engine.<hash>` and exits 1 when that
engine isn't published or 2 when it is, so only the pin needs committing. The
same codes mean the same as in `platforms/apple/check-apple-engine.sh`, which
asks for a GitHub release instead. The registry wants a token even to read:
`GH_TOKEN`, `GITHUB_TOKEN` or a `gh` login, with `read:packages`. With none,
the script exits 69. `release-android.yml` compares the pin with
`android-engine-hash.sh` itself, without the registry, and stops a real
release on a stale one.

## Plugins

| Plugin | Kotlin | `ViewerConfig` |
|---|---|---|
| metadata | `MetadataCapability` | `metadata = true` |
| search | `SearchCapability`, `SearchModel`, `SearchBar` | `search = SearchConfig()` |
| render | `RenderCapability`, `PageImageView`, `TiledPageImage` | `render = RenderConfig()` |
| interaction | `InteractionCapability`, `ToolModel`, `ToolPicker` | `interaction = true` |
| feedback | `FeedbackCapability`, `HapticFeedback` | `feedback = FeedbackConfig(HapticFeedback(view))` |
| stage | `StageCapability`, `StageReaderModel`, `StageReader` | `stage = StageConfig()` |
| selection | `SelectionCapability`, `TextSelectionModel`, `TextSelectionOverlay` | `interaction = true, selection = true` |
| link | `LinkCapability` | `interaction = true, link = true` |
| annotation | `AnnotationCapability`, `AnnotationModel` | `interaction = true, annotation = ""` |
| history | `HistoryCapability`, `HistoryModel`, `HistoryControls` | `history = true` |
| outline | `OutlineCapability`, `OutlineModel`, `OutlinePanel` | `outline = true` |
| attachment | `AttachmentCapability`, `AttachmentModel`, `AttachmentPanel` | `attachment = true` |
| form | `FormCapability` | `interaction = true, form = FormConfig()` |
| redaction | `RedactionCapability` | `interaction = true, annotation = "", redaction = RedactionConfig()` |
| measurement | `MeasurementCapability` | `interaction = true, annotation = "", measurement = MeasurementConfig()` |
| stamp | `StampCapability`, `FileStampLibraryStore` | `stamp = StampConfig()` (with `interaction = true, annotation = ""` to place) |
| page-edit | `PageEditCapability` | `pageEdit = true` |
| view-manager | `ViewManagerCapability` | `viewManager = true` |
| shell | `ShellCapability` | `shell = true` |
| i18n | `I18nCapability`, `AssetLocaleLoader` | `i18n = I18nConfig(...)` with `AssetLocaleLoader(context.assets)` as its loader |
| commands | `CommandsCapability`, `CommandHandler` | `commands = true` |
| actions | `ActionsCapability`, `JavaScriptSandboxEngineFactory` (`embedpdf-javascript`) | `actions = ActionsConfig.forCurrentTimeZone(javascript = ActionsJavaScriptConfig(enabled = true, engine = JavaScriptSandboxEngineFactory(context)))` |
| signature | `SignatureCapability`, `AndroidKeyStoreSigner`, `TrustAnchors`, `HttpSignatureTransport` | `interaction = true, form = FormConfig(), signature = SignatureConfig()` |

`ViewerConfigBuilder` spells the same settings as chained calls:
`ViewerConfigBuilder().interaction().annotation().search(SearchConfig()).build()`.

A plugin left out of `ViewerConfig` is refused at its accessor:
`viewer.search(document)`, `viewer.stamps()` and the others throw
`PdfException` with `PdfException.Code.UNSUPPORTED`, also for a document that
is closed or still loading. `DisabledPluginTest`
(`embedpdf/embedpdf/src/test/kotlin/com/embedpdf/DisabledPluginTest.kt`) has
one row per accessor and fails when `ViewerConfig` gains a plugin without one.
The companion-plugin refusals are the ones `platforms/apple/README.md` lists
under Plugins; the Rust is the same.

## Local engine mode

Everything runs in the dev container on amd64, which has JDK 21, the Android
SDK at `/opt/android-sdk` and NDK r29 at `/opt/android-ndk-r29`
(`.devcontainer/README.md` lists the versions). An app or the demo in this
checkout uses the libraries as projects, with the engine `build-aar.sh` left
in `bindings/src/main`:

```bash
bash platforms/android/build-aar.sh          # the engine for both ABIs, then the checks
bash platforms/android/build-aar.sh test     # also the JVM tests
PROFILE=dev bash platforms/android/build-aar.sh test   # unoptimized, for iterating
cd platforms/android/embedpdf && ./gradlew check       # unit tests, lint, apiCheck
```

To try a published layout without publishing anywhere, `./gradlew
publishToMavenLocal` writes all five artifacts to Maven Local, and
`bash platforms/android/android-consumer.sh` does that into a directory of its
own and builds the demo from it (see [The demo app](#the-demo-app)).

## Releasing

`.github/workflows/release-android.yml` builds and releases the package, in
two stages with one approval between them, as `release-apple.yml` does for the
Apple engine. The release train runs it on every dispatch, and with an
`android_version` (`0.0.0-android.<n>`, notes in
`docs/releases/<android_version>.md`) it releases. `android-release.yml` runs
the same workflow on its own, for an Android-only release, with its own
preflight, the two Android slices and an `approve` job.

The build stage runs in the dev image on `ubuntu-24.04`. It links the engine
against the `android-arm64` and `android-x64` fork slices that
`release-fork-slices.yml` built earlier in the same run, not a downloaded
release, and checks that both are their target at the `runtime-src` gitlink.
Then it runs `build-aar.sh test`, the Kotlin API coverage check, `apiCheck`
and `android-consumer.sh`, packs the staged repository with
`package-maven-zip.py`, and builds the minified demo once more from the
unzipped zip with `-Pembedpdf.mavenRepository`. It uploads `android-maven`
(the staged files) and `android-release` (the zip and its `.sha256`). Its
token reads contents and packages and writes neither.

A stale engine pin stops a real run with a version, both in the train's
preflight and in the build stage, because the engine version must name the
sources it was built from. A dry run or a run without a version only warns,
and pins the tree's engine in its own checkout.

After approval, the publish stage's `release` job tags the dispatched commit
as `<version>` and makes the prerelease with the zip and its `.sha256`
(`scripts/release/github-release.sh`, which skips a release already out with
both assets). With a packages repository (`android_packages_repository` in
the train, `packages_repository` in the wrapper), the `packages` job also
uploads `android-maven` with `platforms/android/publish-staged.sh`, the only
job with `packages: write`. A registry in another repository needs the
`ANDROID_PACKAGES_TOKEN` secret, a classic token with `write:packages` there.
`scripts/release/check-release-gate.py` fails both workflows when a job that
can write contents or packages, or that runs `publish-staged.sh`, doesn't wait
for `approve`.

The cost of the zip as the main channel is that an app keeps the repository
itself, about 90 MB for `0.0.0-android.1`'s dry run, where GitHub Packages
would be one line once the token is set up. Maven Central would avoid both,
at the cost of a namespace and signing setup we don't have.

The first release, then: commit release notes at
`docs/releases/0.0.0-android.1.md`, dispatch the train (or
`android-release.yml`) with `dry_run` on and look at the build stage's summary
and `android-release`, then dispatch it with `dry_run` off and approve.

## Cancellation

A cancelled caller never gets a value from the facade. Cancelling the
coroutine cancels the call's `CancelToken`, waits for the kernel's answer,
and then throws the caller's own `CancellationException`, even when the work
finished first. Kotlin would drop that value at the caller's next suspension
anyway (`withTimeout` and `async` do), so handing it back would only look
like a promise we can't keep. The rule lives in `bridgedCancellable` in
`embedpdf/embedpdf/src/main/kotlin/com/embedpdf/Bridging.kt`.

Swift differs here. A cancelled task in `EmbedPDF` still receives what the
call returned (`bridgedCancellable` in
`platforms/apple/EmbedPDF/Sources/EmbedPDF/PDFError.swift`), because Swift
cancellation is cooperative and a value that arrives is delivered.

The cost is on calls that change something. An edit, an outline change, a
metadata update, an attachment add or a save that had started when the
cancel arrived still lands, and the caller sees only its cancellation. Each
such call says so in its KDoc; after a cancelled one, read the state again.
Opens are the exception: they pass an `onDiscard` that closes the document
the cancelled open made. A cancel that arrives after that check passed but
before the caller resumed still leaves the document open.

## Models

`models` is the Kotlin counterpart of `EmbedPDFModels`: one plain class per
Swift model (`SearchModel`, `HistoryModel`, `StageReaderModel`,
`TextSelectionModel`, `TiledPageModel` and the rest), each taking its
capabilities in the constructor and exposing `StateFlow`s. It works from a
View-based app or from Compose, and takes no Compose dependency:
`./gradlew :models:checkNoCompose` fails on an `androidx.compose` dependency,
direct or transitive, or import. Its public surface is
`models/api/models.api`, checked by `apiCheck` like the others.

A model never creates a `CoroutineScope`. Swift's `task()` is
`suspend fun run()`, which follows the capability's events and state
revisions until the document closes or the caller cancels it. The caller
picks the scope:

```kotlin
lifecycleScope.launch { repeatOnLifecycle(Lifecycle.State.STARTED) { search.run() } }
```

Actions are `suspend fun`s and switch to `Dispatchers.Main.immediate`
themselves, where every model keeps its state.

A few calls return at once and queue their work instead: the
`TextSelectionModel` drag calls, the `StageReaderModel` pointer calls, its
pan switch and its link loads. `run()` sends them. A model with such calls
also takes `actionScope: CoroutineScope? = null`; when a call arrives while
`run()` isn't draining, the model starts one drain in that scope, which is
still the caller's. Without one the call waits for the next suspend call on
its queue, and the first such call sets `error` to `invalid-state` saying
that `run()` isn't running. `PdfReaderModel` passes its `actionScope` on to
the models it builds. The cost of passing `viewModelScope` here is that a
drag sent after the screen stops still reaches the engine, which is why it's
opt-in.

`PdfViewerViewModel` holds one `PdfViewer` across configuration changes. It
starts the viewer when it's created and destroys it once in `onCleared()`,
also when the start is still in flight:

```kotlin
private val holder: PdfViewerViewModel by viewModels {
    PdfViewerViewModel.factory(ViewerConfig(render = RenderConfig(), stage = StageConfig()))
}
```

Read `holder.viewer` to subscribe, and await `holder.startedViewer()` before
an open.

The module has no `Context`, so it can't read string resources. `HistoryModel`
and `SearchModel` take optional `HistoryStrings` and `SearchStrings`
providers, whose members all default to English (`undo`, `redo`, `change`,
`undoTitle(label)`, `redoTitle(label)`, `notice(notice)`,
`searchSummary(index, count, searching)`). The compose module's
`ResourceHistoryStrings` and `ResourceSearchStrings` read them from string
resources; in composition, `rememberHistoryStrings()` and
`rememberSearchStrings()` make them for the current configuration. `HistoryModel.notice` is a `HistoryNotice` value
(`SkippedUndo(count)`, `SkippedRedo(count)`) for a caller that words it
itself, and `noticeText` is the provider's wording.

## Compose

`embedpdf-compose` is the Kotlin counterpart of `EmbedPDFUI`. `PdfReader` is
the whole reader in one composable, over a `PdfReaderModel`:

```kotlin
val model = remember { PdfReaderModel(configure = { it.outline().attachment().history() }) }
LaunchedEffect(model) { model.open(bytes, name = "report.pdf") }
PdfReader(
    model,
    tools = listOf(ToolId.POINTER, ToolId.INK),
    onLinkActivation = { activation -> /* open a Uri */ },
    onError = { error -> Log.w("Reader", error.detail) },
)
```

It shows a password prompt for a locked document, a localized message for a
failure (the exception goes to `onError`), and for a ready document the
pages, a search bar, a toggle per panel the viewer's plugins allow
(thumbnails, bookmarks, attachments), a tool picker and history controls. A
panel sits beside the pages from 600 dp of width and in a bottom sheet below
it; below 600 dp the search bar also takes a row of its own. Hold the model in
a `ViewModel` so a rotation keeps the document, as the demo does. Open
forms, confirmations and an attachment copy waiting for the save picker are
saved state, so they outlive a configuration change; the typed password is
not, so it never lands in the saved-state bundle.

The pieces are public too: `StageReader`, `TiledPageImage`, `PageImageView`,
`ThumbnailStrip`, `SearchBar`, `OutlinePanel`, `AttachmentPanel`,
`HistoryControls`, `ToolPicker`, `TextSelectionOverlay` and
`SearchHighlightOverlay`. Each takes a model from `embedpdf-models` and runs it
while composed, except the two overlays, which draw a model run elsewhere.
`compose/api/compose.api` is the checked surface.

The engine paints no search hits into the tiles, so `SearchHighlightOverlay`
draws them, as each web framework's search layer does
(`packages/framework/web/src/search-highlights.ts`). It fills each line of each
hit on one page with the search plugin's `highlight.color`, the active hit's
lines with `highlight.activeColor`, and blends them with the page in
`highlight.blendMode`, through the same page-to-frame mapping as
`TextSelectionOverlay`, so zoom and rotation place it. `SearchModel.highlight`
carries the setting and follows `updateSettings`. `PdfReader` stacks it under
the text selection on every page. A hand-built `StageReader` returns it from
`pageOverlay`:

```kotlin
StageReader(reader) { visiblePage ->
    val info = pages.firstOrNull { it.page == visiblePage.page } ?: return@StageReader
    SearchHighlightOverlay(search, visiblePage, info, Modifier.fillMaxSize())
}
```

Colors are read as CSS hex, `rgb()`, `rgba()` or a basic color name; one it
can't read draws the plugin's default for that slot. Below API 29 a blend mode
other than the Porter-Duff ones falls back to multiply. The overlay has no
semantics and takes no touches. The cost: the model reads the settings on
every state revision, one more engine round trip per scroll step.

## The demo app

`platforms/android/EmbedPDFDemo` is a Compose app at parity with
`platforms/apple/EmbedPDFDemo`: it opens the bundled `report.pdf`, offers
`encrypted-hello.pdf` (password `hôtel`) and any PDF from the system picker
(`ACTION_OPEN_DOCUMENT`), shows it in `PdfReader` with the ink tool, the
bookmark, attachment and thumbnail panels and history, adds a note on the
first page, and shows the document's metadata in a sheet. `DocumentViewModel`
holds the `PdfReaderModel`, so a rotation keeps the document, and closes it
when the activity finishes.

It is a Gradle build of its own rather than a module of
`platforms/android/embedpdf`, so `./gradlew check` over the libraries never
configures an app. By default its `settings.gradle.kts` includes the library
build and substitutes the five coordinates with its projects; with
`-Pembedpdf.fromMavenLocal` it resolves them from Maven Local instead. It has
no wrapper of its own and uses the library build's:

```bash
bash platforms/android/build-aar.sh
platforms/android/embedpdf/gradlew -p platforms/android/EmbedPDFDemo :app:assembleDebug
```

The release build type is minified with R8 and signed with the debug key, so
it installs, but it is not a store build. `bash platforms/android/android-consumer.sh`
publishes the five artifacts into `embedpdf/build/consumer-maven` with
`publishToMavenLocal -Dmaven.repo.local=...`, checks that exactly those five
publish, each at one version, and that every POM names a version for every
dependency, assembles the release demo from those
coordinates, checks that both ABIs' engine is stored uncompressed, runs
`zipalign -c -P 16 4`, and checks R8's `usage.txt` and `mapping.txt`: no
`uniffi.` or `com.sun.jna.` class or member removed, none renamed, and every
such class in the bindings AAR and the JNA AAR present. Results, 2026-11-26:
`app-release.apk` is 92,419,773 bytes with both ABIs, `zipalign -c -P 16 4`
passes, and R8 kept all 5,566 `uniffi.*` classes of `embedpdf-bindings` and
all 125 `com.sun.jna.*` classes of `jna-5.19.1.aar` under their own names.
It was 132,142,645 bytes before `build-aar.sh` stripped the engine. AGP still
reports it could not strip the native libraries, since the demo build has no
NDK, but the engine's are already stripped in the AAR.
The first run found that a minified app did not build at all: JNA's
`Native$AWT` names `java.awt` classes Android lacks, so
`bindings/consumer-rules.pro` now adds `-dontwarn java.awt.**`.

There are no demo UI tests. The Apple ones drive a simulator; on Android the
demo was installed and checked by hand on an emulator, debug and minified
release both (see [Device runs](#device-runs)), and what it calls is covered
by the compose module's Robolectric and instrumented tests.

## Building in the dev container

Everything runs in the dev container on amd64, which has JDK 21, the Android
SDK at `/opt/android-sdk` and NDK r29 at `/opt/android-ndk-r29`
(`.devcontainer/README.md` lists the versions):

```bash
bash platforms/android/build-aar.sh          # build the AAR and run the checks
bash platforms/android/build-aar.sh test     # also run the JVM tests
PROFILE=dev bash platforms/android/build-aar.sh test   # unoptimized, for iterating
```

Outside the container, `ANDROID_HOME` and `JAVA_HOME` have to be set and the
NDK is found at `$ANDROID_NDK_HOME`, defaulting to
`~/Library/Android/sdk/ndk/29.0.14206865`. A missing runtime slice is fetched by
`fetch-android-runtime.sh`, described below.

The JVM tests can also run on their own, from a fresh checkout too:

```bash
cd platforms/android/embedpdf
./gradlew :bindings:testDebugUnitTest :embedpdf:testDebugUnitTest :smoke:test
```

`bindings` builds `epdf-viewer-host` and `uniffi-bindgen` for the container
(in `$CARGO_TARGET_DIR/debug`, `target/debug` by default). When
`bindings/src/main/kotlin` is missing, or older than that library or the
generator, `generateLinuxBindings` writes it from the Linux library before the
debug compile; `build-aar.sh` still writes it from the Android library for the
AAR. `smoke` builds `epdf-uniffi` and generates its Kotlin into its own build
directory. Gradle sets `jna.library.path`, `LD_LIBRARY_PATH` for the Linux
runtime slice and the fixture directory itself, so no shell setup is needed.
`ViewerHostLoadTest` builds a viewer with every plugin, opens `report.pdf` and
reads its 14 pages; `CommandContextTest` is described under "Full viewer
command callbacks on Linux".

### Device runs

There is no Android emulator in the dev container: Docker on a Mac host has no
nested virtualization, so none can run there (checked 2026-11-20). The
instrumented tests run from a macOS host instead, against an emulator or a
device, after `build-aar.sh` has built the engine on that host:

```bash
bash platforms/android/build-aar.sh
cd platforms/android/embedpdf
./gradlew :embedpdf:connectedDebugAndroidTest :javascript:connectedDebugAndroidTest :compose:connectedDebugAndroidTest
```

They live in each module's `src/androidTest`, with fixture access shared
from `embedpdf/src/androidTestShared`. Each module's `deviceFixtures` task
copies the PDFs they open into the test APK's assets. The user-authentication
case in `DeviceKeyStoreTest` needs a secure lock screen to generate its keys:
`adb shell locksettings set-pin 1234` before the run, `adb shell locksettings
clear --old 1234` after.

Results, 2026-11-26, on the `Pixel_4` AVD running
`system-images/android-37.1/google_apis_playstore_ps16k/arm64-v8a` (build
`CP31.260623.012`, `getconf PAGE_SIZE` 16384) with WebView 157.0.8103.50:
15 tests, 0 failures. A second compose run the same day, with
`DeviceSearchHighlightTest` added: 4 tests, 0 failures.

`DeviceViewerTest` (3 tests): the engine starts on the 16 KB page device and
runs mapped straight out of the APK at a 16 KB aligned offset; a viewer with
every plugin opens `report.pdf` with 14 pages, renders page 1 to a 612 by 792
`Bitmap` with 6,365 dark samples, finds "Report", and saves in both modes to
bytes that reopen; `encrypted-hello.pdf` opens `LOCKED`, refuses a wrong
password and unlocks with `hôtel`.

`DeviceKeyStoreTest` (4 tests): an EC P-256 key generated in the Keystore
signs a message that verifies with the public key of the certificate the
Keystore made for it. Keys generated with `setUserAuthenticationRequired(true)`,
one per use and one with a 30 second window, both fail with `permission-denied`
named `UserNotAuthenticated`, directly and through the viewer, with nothing
left pending. `setUserAuthenticationRequired(true)` alone, which means a
biometric per use, can't be generated here: the emulator has no biometric
enrolled. A document signed through `SignatureCapability` with the generated
key is `VALID_UNTRUSTED`: integrity and cryptography are valid, but with the
Keystore's self-signed certificate as the anchor the engine answers "One of
intermediate certificates is not a CA certificate". That certificate is no CA,
so an app needs a CA-issued chain on the key. With the test signer's RSA key
imported into the Keystore and its certificate as the anchor, the verdict is
`TRUSTED` and `VALID`. No test answers a biometric or credential prompt; a
test can't.

`DeviceSandboxTest` (5 tests): the sandbox is supported and evaluates; it has
`JS_FEATURE_ISOLATE_TERMINATION`, and `while (true) {}` times out at about 200
ms with a 200 ms budget, after which the factory makes working realms again;
two factories share one connection, which survives closing one of them;
`document_actions.pdf`'s will-close script runs through `ActionsCapability`.
Pressing `btn-chain` in `action_buttons_form.pdf` runs its JavaScript,
ResetForm and JavaScript nodes, all `EXECUTED`, and resets `alpha`, but leaves
the log field at "B". The TypeScript plugin's e2e
(`packages/plugin/form/test/action-buttons.integration.test.ts`) expects "AB"
for the same press: the second script doesn't see the first one's write. That
is the Rust actions plugin rather than the sandbox, and it is not fixed here.

`DeviceStageReaderTest` (3 tests): `StageReader` draws the document and names
its pages "Page 1 of 14" and on; a drag pans the camera and a pinch zooms it
(0.471 to 0.660); with the ink tool active, the first stroke creates an ink
annotation, where Robolectric needed two.

`DeviceSearchHighlightTest` (1 test): searching "Trace" in `report.pdf` makes
416 hits, and the first one, 27.3 by 10.1 dp on screen, changes color in 1,351
of the 1,704 pixels sampled inside it. Its 515 dark text pixels stay dark,
so the fill multiplies rather than covers. Before the overlay existed the
demo found the same 416 hits and drew none of them.

The run found two crashes no JVM test could. Both are JNA running a nested
callback on a native thread and detaching that thread when the inner callback
returns, which ART aborts ("attempting to detach while still running code")
and HotSpot refuses quietly. It happens when a generated async call resumes on
the thread that delivered the kernel's answer, so its next poll answers
inside the first callback. The facade's cancellable calls did that on
`Dispatchers.Unconfined`, so every one of them crashed the process on a
device; they run on `Dispatchers.Default` now. A caller whose own dispatcher
resumes inline still crashes: Compose's test rule runs effects on an
`UnconfinedTestDispatcher` by default, so `DeviceStageReaderTest` passes a
`StandardTestDispatcher` as the effect context. An app's own instrumented
tests over these views need the same until the generated continuation stops
resuming inline.

The demo app, debug and minified release, installs, opens `report.pdf` and
draws its first page (screenshots identical across the two); the release
build's search finds 416 hits for "Trace". Neither logcat shows a crash or an
`UnsatisfiedLinkError`. With `SearchHighlightOverlay`, the debug demo built
from source draws the hits: the title's "Trace" in orange as "1 of 416", and
the page's other "trace" hits in yellow with the text readable through them.

The `x86_64` engine is built and checked but not executed, since the only
image installed is arm64. A run on physical hardware is still to do.

### Library sizes

Release profile, 2026-11-26, as packaged in `bindings-release.aar`, stripped:

| ABI | `libepdf_viewer_host.so` | `libc++_shared.so` |
|---|---|---|
| `arm64-v8a` | 39,817,544 bytes (38 MB) | 1,374,336 bytes (1.3 MB) |
| `x86_64` | 45,188,968 bytes (43 MB) | 1,337,488 bytes (1.3 MB) |

Unstripped they were 53,312,672 and 55,724,344 bytes for the host library and
9,290,184 and 9,015,544 bytes for `libc++_shared.so`. The `epdf-uniffi`
library the host replaced was 16 MB per ABI. A release build of both ABIs took
4m37s and 3m52s of Cargo time in the container.

### Native crash symbols

`build-aar.sh` runs the NDK's `llvm-strip --strip-unneeded` on every `.so` it
copies into `jniLibs`, so the AAR, and every app that packages it, ships
without symbol tables. Leaving it to the app did not work: AGP strips packaged
libraries only when it finds an NDK, and the consumer check's release APK
carried the full engine. The alignment and `__arm_` checks run on the
stripped files, since those are what ship.

The libraries as built are kept in `bindings/build/native-debug-symbols/<abi>/`
and zipped to `bindings/build/outputs/native-debug-symbols.zip`, with one
directory per ABI at the top. The `embedpdf-bindings` publication carries that
zip under the `native-debug-symbols` classifier, so an app symbolicates a
native crash from the published engine it uses:

```kotlin
// build.gradle.kts of an app: fetch the zip matching the engine version.
val engineSymbols by configurations.creating
dependencies {
    engineSymbols("com.embedpdf:embedpdf-bindings:0.0.0-engine.<hash>:native-debug-symbols@zip")
}
```

Upload it as the release's native debug symbols in the store console, or
unzip it and point `ndk-stack -sym <dir>/<abi>` at a tombstone. The cost is
one more artifact per engine release, 43,587,493 bytes for this one.

## Engine identity

`android-engine-hash.sh` prints a 16-hex hash over every input that decides the
AAR's contents: the Rust crates `epdf-viewer-host` links (the same list as
`platforms/apple/apple-engine-hash.sh`, read from it), `uniffi-bindgen`,
`Cargo.lock`, `rust-toolchain.toml`, `build-aar.sh`, `fetch-android-runtime.sh`,
the `bindings` build files and the `runtime-src` commit. `gradle.properties`
pins `embedpdf.engineRelease=android-engine-<hash>`, and
`check-android-engine.sh` exits 0 when the pin matches the tree, and 1 or 2
when it doesn't: 1 when GitHub Packages has no
`com.embedpdf:embedpdf-bindings:0.0.0-engine.<hash>` POM, 2 when it has one.
It exits 64, 69 or 70 on a hard error; 69 covers a missing token and any
registry answer other than 200 or 404. Nothing is published under that
version yet.

## The fork slices are prebuilt, and have to be

`fetch-android-runtime.sh` downloads `libembedpdf.a` for `android-arm64` and
`android-x64` from the `runtime-<sha>` release for the commit `runtime-src`
pins (`af480d0ce` today), and checks each archive's SHA-256 against that
release's `pdf-runtime-build.generated.json` before unpacking it into
`packages/engine/runtime/build/libpdfium/`. They were built in a
`linux/amd64` container with the fork's own `build-target.sh` and
`package-target.sh`, with `PDF_RUNTIME_TARGET_OS_LIST="linux android"`. They
cannot be built on a Mac: `runtime-src/build/config/BUILDCONFIG.gn` asserts

```
assert(host_os == "linux", "Android builds are only supported on Linux.")
```

so `scripts/embedpdf-runtime/build-target.sh android-arm64` fails at `gn gen` on
macOS, after a full gclient resync that is wasted. Rebuilding these needs a Linux
host. That is worth knowing before anyone tries, and it is the one part of the
Android path that is not reproducible on this machine.

## What the script checks

Every `.so` it packages is read with `llvm-readelf` and its `LOAD` alignment has
to be 16 KB or better. Both are `0x4000` today, because NDK r28 and later align
that way by default. A default is exactly the thing that changes back under you,
and a 4 KB-aligned library does not load at all on a 16 KB page-size device, so
it is checked rather than trusted.

The APK is checked separately, with `zipalign -c -P 16`, because an ELF with
16 KB `LOAD` segments still fails to map if the zip entry holding it does not
start on a 16 KB boundary. The two checks answer different questions. This
project builds two APKs, and both are checked: `build-aar.sh` builds the
`bindings` debug test APK only to give this check an APK, and
`android-consumer.sh` checks the minified release demo. An app that packages
the AAR has to run the same check on its own output.

`useLegacyPackaging = false` keeps the libraries stored rather than deflated
inside the archive, which is what lets the linker map one straight out of the
APK. Compressing would also throw away the alignment the ELF was built with.

`bindings/consumer-rules.pro` keeps JNA and every generated `uniffi.**` package
(one per component: the host, `epdf_ffi` and each plugin), with every callback
interface an app implements. JNA reaches
both by reflection, so R8 sees no reference and strips them; the failure is an
`UnsatisfiedLinkError` in release builds only, naming a class the source
obviously contains.

## compiler-rt's SME routines

The fork's arm64 libyuv has SME rows (`rotate_sme.o`, `row_sme.o`,
`scale_sme.o`) that call `__arm_get_current_vg` and `__arm_tpidr2_save`.
Those live in clang's builtins, and rustc links with `-nodefaultlibs`, so the
`.so` used to leave them undefined. Every smoke test then failed at `dlopen`:

```
cannot locate symbol "__arm_get_current_vg" referenced by ".../libepdf_uniffi.so"
```

`build-aar.sh` passes the NDK's `libclang_rt.builtins-<arch>-android.a` as a
link argument, which takes only the members that answer an undefined symbol,
and fails the build if the library still imports an `__arm_` routine.

## The smoke test

`smoke/src/test/kotlin/com/embedpdf/smoke/SmokeTest.kt` asserts what
`crates/epdf-uniffi/tests/native_surface.rs` and the iOS bundle assert, in the
same order. Identical assertions across the three mean a divergence is a
packaging problem rather than a logic one.

Since 2026-11-20 it's a JVM test against `libepdf_uniffi.so` built for the dev
container, because the container has no emulator (see "Device runs"). 8 tests,
0 failures. The two device runs below were with the instrumented version, when
the AAR still packaged `epdf-uniffi`.

Results, 2026-09-23: 8 tests, 0 failures, on the `Pixel_4` AVD running
`system-images/android-37.1/google_apis_playstore_ps16k/arm64-v8a`. That image
is the 16 KB page-size one, confirmed by `getconf PAGE_SIZE` reporting 16384 on
the device, so this is the 16 KB emulator run the plan asks for and not a 4 KB
image that happens to load a 16 KB-aligned library.

Again on 2026-11-18, with the slices from fork `af480d0ce` and the builtins
link above: 8 tests, 0 failures, same AVD, `getconf PAGE_SIZE` 16384.

Not covered: a signed run on physical hardware, which needs a device and a
signing identity, and `x86_64`, which is built and checked but not executed
because the only image installed here is arm64.

## Two things this shook out

Both were invisible from Rust and from Swift, and both came from uniffi's Kotlin
backend mapping exported items onto Java types that already have members.

`PdfError::Engine` carried a field named `message`. Kotlin renders an error
variant as a `Throwable` subclass with the fields as constructor properties, so
the generated file declared `val message: String` twice and did not compile. The
field is `detail` now.

`PdfEngine::close` and `PdfDocument::close` collided the same way: uniffi gives
every exported object an `AutoCloseable.close()` that disposes the handle, and
two `fun close(): Unit` in one class is not legal Kotlin. Both are `shutdown()`
now. Dropping the last reference already does the same work through
`OwnerThread`'s `Drop`, so `use { }` in Kotlin and ARC in Swift stay correct
whether or not anyone calls it.

The cost of the second rename is that `shutdown` is not the word either platform
expects for this, and a facade will want to present it as `close`. That is the
facade's job and a reason it exists.


## Full viewer command callbacks on Linux

`bindings/src/test/kotlin/com/embedpdf/bindings/CommandContextTest.kt` runs
command callbacks against `epdf-viewer-host` and is part of
`./gradlew :bindings:testDebugUnitTest`. The script runs it against a chosen
library and adds the checksum check, which needs `kotlinc`:

```bash
KOTLINC=/path/to/kotlinc \
JNA_JAR=/path/to/jna-5.19.1.jar \
COROUTINES_JAR=/path/to/kotlinx-coroutines-core-jvm-1.10.2.jar \
bash platforms/android/test-command-context.sh
```

This test uses JDK 21 and Kotlin 2.2.21. It builds the full host, generates
every Kotlin component with the production `uniffi-bindgen` into
`bindings/src/main/kotlin`, compiles them together through Gradle, and
executes real command callbacks. It covers default JSON state, whole replacement including null,
invalid-input preservation, state updates during successful and failing runs,
and errors thrown by Kotlin callbacks across the native boundary. It also
changes one expected checksum in a separate test module and requires native
initialization to reject it. An optional
first argument selects an already-built full-host `.so`.

Kotlin exposes the viewer's asynchronous `destroy` as `destroyViewer()`;
`shutdown()` still stops its kernel thread, and generated `close()` releases
its handle. `FormSubmissionValue.List` is named `Values` in Kotlin so it does
not shadow Kotlin's collection type. These supported binding renames leave the
Rust and Swift names intact.

The production generator also adapts imported callback-error buffer wrappers
without copying their Rust allocation. Its checksum declarations use the
native unsigned 16-bit return width and compare unsigned values while retaining
UniFFI's checksum constants, mismatch exceptions, and contract-version checks.

## The signature transport

`HttpSignatureTransport` (`embedpdf/embedpdf/src/main/kotlin/com/embedpdf/HttpSignatureTransport.kt`)
is the viewer's HTTP door for signatures: the POST a timestamp authority or a
revocation route sends, on `HttpURLConnection`. Set it beside the signer and
the trust anchors:

```kotlin
viewer.setSigner(AndroidKeyStoreSigner("my-signing-key"))
viewer.setTrust(TrustAnchors.fromAssets(context.assets, listOf("trust/our-ca.pem")))
viewer.setSignatureTransport(HttpSignatureTransport())
```

One `post` is one attempt. Retries, the delay between them, what a status means
and the error texts stay in the Rust clients, so every status answers
`replied`. The transport enforces the request's limits itself, since the
client has no timer: `timeoutMs` covers connecting, sending and reading, and a
2xx reply past `maxReplyBytes` is `tooLarge`. A reply outside 2xx that runs
past the limit, breaks off or is still arriving at the deadline is `replied`
with its status and no body. The completion is answered exactly once. On
Android a cleartext `http://` route also needs the app's network security
config to allow it.

It moved from `viewer/` into the facade with the signature capability, and its
tests are Gradle tests now. `HttpSignatureTransportTest` sends requests to a
loopback server for each outcome, the deadline and the size limit, and counts
the answers. `SignatureRoutesTest` signs through the viewer against
upstream's `timestampRoute` and `revocationRoute`, which
`tests/signature-routes.mjs` serves from `packages/core/signature/dist`: a
granted timestamp, a long-term validation fill and a call with no authority.
The test starts the helper with `node` and is skipped when `node` or the
`dist` is missing. `test-signature.sh` replaces the old `kotlinc` script and
runs both classes with that skip turned into a failure:

```bash
pnpm --filter @embedpdf/core-signature build
bash platforms/android/test-signature.sh
```

The cost is that a plain `./gradlew :embedpdf:testDebugUnitTest` on a checkout
without the `dist` passes with the route tests skipped.

The test authority writes no nonce and the Rust client checks one, so the
helper sets the query's nonce on the token while it signs. A real authority
answers it on its own.

## Limitations

The instrumented tests ran on one arm64 16 KB page emulator on 2026-11-26
([Device runs](#device-runs)), not on physical hardware, and they need a
macOS host since the dev container has no emulator. The `x86_64` engine is
built and checked but has never been executed. PDF JavaScript's
JS→ResetForm→JS chain diverges from the TypeScript plugin, and an inline
dispatcher calling into the viewer crashes the process on Android; the same
section has both. The
release workflows (`release-android.yml`, the train, `android-release.yml`)
have been linted, and their build, staging and zip steps run on a macOS host,
but they have not run on a hosted runner, and nothing is published. PDF
JavaScript needs API 26 and a WebView whose `JavaScriptSandbox.isSupported()`
is true; elsewhere it reports itself unavailable. The demo app has no UI
tests.
