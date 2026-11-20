# Android

The `bindings` AAR packages `epdf-viewer-host`, the full viewer host with every
plugin, for `arm64-v8a` and `x86_64`, with `libc++_shared`, the generated Kotlin
for every uniffi component, the shrinker rules and the 16 KB checks. It's built
locally and published nowhere yet.

## Modules

`platforms/android/embedpdf` is one Gradle build with a wrapper (Gradle 9.8.2)
and a version catalog, `gradle/libs.versions.toml`.

| Module | What it is | Published |
|---|---|---|
| `bindings` | `libepdf_viewer_host.so` per ABI and the generated `uniffi.*` Kotlin | yes, as the AAR |
| `embedpdf` | the hand-written `com.embedpdf` facade: `PdfViewer`, `PdfException`, the document fetchers | not yet |
| `javascript` | `com.embedpdf.javascript.JavaScriptSandboxEngineFactory`, PDF JavaScript in `androidx.javascriptengine`, as `embedpdf-javascript` | not yet |
| `smoke` | JVM tests of the engine-only `epdf-uniffi` crate | no |

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

The models and Compose modules come in later phases of
`docs/superpowers/plans/2026-11-19-android-parity.md`.

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

### No emulator

There is no Android emulator in the dev container: Docker on a Mac host has no
nested virtualization, so none can run (checked 2026-11-20). Nothing in this
directory runs on a device any more. What a device run proved is covered two
other ways. The Kotlin and the native contract are tested on the JVM against
the same sources built for Linux, and the property only a device can show,
that the `.so` maps under Android's linker on a 16 KB page, is covered by the
static ELF and `zipalign` checks below. The cost is that a loader or linker
problem specific to bionic would go unseen until a device or CI emulator runs
the AAR.

### Library sizes

Release profile, 2026-11-20, as packaged in `bindings-release.aar`:

| ABI | `libepdf_viewer_host.so` | `libc++_shared.so` |
|---|---|---|
| `arm64-v8a` | 53,312,672 bytes (51 MB) | 9,290,184 bytes (8.9 MB) |
| `x86_64` | 55,724,344 bytes (54 MB) | 9,015,544 bytes (8.7 MB) |

The `epdf-uniffi` library this replaced was 16 MB per ABI. The host libraries
keep their symbol tables (`file` reports "not stripped"); an app build with the
NDK available normally strips packaged libraries, so what ships in an APK
should be smaller. That hasn't been measured. A release build of both ABIs took
4m37s and 3m52s of Cargo time in the container.

## Engine identity

`android-engine-hash.sh` prints a 16-hex hash over every input that decides the
AAR's contents: the Rust crates `epdf-viewer-host` links (the same list as
`platforms/apple/apple-engine-hash.sh`, read from it), `uniffi-bindgen`,
`Cargo.lock`, `rust-toolchain.toml`, `build-aar.sh`, `fetch-android-runtime.sh`,
the `bindings` build files and the `runtime-src` commit. `gradle.properties`
pins `embedpdf.engineRelease=android-engine-<hash>`, and
`check-android-engine.sh` exits 0 when the pin matches the tree, 1 or 2 when it
doesn't (with the same meanings as `platforms/apple/check-apple-engine.sh`),
and 64, 69 or 70 on a hard error. Nothing is published under that name yet.

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
start on a 16 KB boundary. The two checks answer different questions. The only
APK this project builds is the test one; an app that packages the AAR has to
run the same check on its own output.

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
container, because the container has no emulator (see "No emulator"). 8 tests,
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
JNA_JAR=/path/to/jna-5.14.0.jar \
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

`viewer/HttpSignatureTransport.kt` is the viewer's HTTP door for signatures:
the POST a timestamp authority or a revocation route sends, on
`HttpURLConnection`. Set it on the full viewer host, beside the signer and the
trust anchors:

```kotlin
viewer.setSignatureTransport(HttpSignatureTransport())
```

One `post` is one attempt. Retries, the delay between them, what a status means
and the error texts stay in the Rust clients, so every status answers
`replied`. The transport enforces the request's limits itself, since the
client has no timer: `timeoutMs` covers connecting, sending and reading, and a
2xx reply past `maxReplyBytes` is `tooLarge`. A reply outside 2xx that runs
past the limit, breaks off or is still arriving at the deadline is `replied`
with its status and no body. The completion is answered exactly once.

It isn't in the AAR yet. The AAR's bindings now carry the signature plugin, so
the file compiles against them, and it moves into the `embedpdf` facade module
with the signature capability. On Android
a cleartext `http://` route also needs the app's network security config to
allow it.

```bash
KOTLINC=/path/to/kotlinc \
JNA_JAR=/path/to/jna-5.14.0.jar \
COROUTINES_JAR=/path/to/kotlinx-coroutines-core-jvm-1.10.2.jar \
bash platforms/android/test-signature.sh
```

`tests/SignatureTest.kt` sends requests to a loopback server for each outcome,
the deadline and the size limit, and counts the answers. It then signs through
the viewer against upstream's `timestampRoute` and `revocationRoute`, which
`tests/signature-routes.mjs` serves from `packages/core/signature/dist` (build
that package first). That covers a granted timestamp, a long-term validation
fill and one call through each surface the catch-up added: the signature
settings and per-call authority, `addValidationInfo`, the verdict's
revocation, timestamp and long-term fields, `SignatureRequestedTime`, the
standard command lists, `ToolPermission`, `hasCreationAwaitingClick` and
`StampImportOptions.id`.

The test authority writes no nonce and the Rust client checks one, so the
helper sets the query's nonce on the token while it signs. A real authority
answers it on its own.
