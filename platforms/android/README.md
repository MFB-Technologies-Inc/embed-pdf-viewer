# Android

`multiplatform-plan.md` Phase 1 asks for an Android AAR with both ABIs,
`libc++_shared`, the shrinker rules and the 16 KB checks. This is that, built
locally and published nowhere.

```bash
bash platforms/android/build-aar.sh          # build the AAR and run the checks
bash platforms/android/build-aar.sh test     # also run the smoke test on a device
```

`ANDROID_HOME` and `JAVA_HOME` have to be set; the script finds the NDK at
`$ANDROID_NDK_HOME`, defaulting to `~/Library/Android/sdk/ndk/29.0.14206865`.

## The fork slices are prebuilt, and have to be

`libembedpdf.a` for `android-arm64` and `android-x64` comes from fork commit
`af480d0ce`, the commit `runtime-src` pins, registered in
`engine-runtime-build.local.json` as `file://` URLs. They were built in a
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

`library/consumer-rules.pro` keeps JNA and the generated bindings. JNA reaches
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

`library/src/androidTest/kotlin/com/embedpdf/SmokeTest.kt` asserts what
`crates/epdf-uniffi/tests/native_surface.rs` and the iOS bundle assert, in the
same order. Identical assertions across the three mean a divergence is a
packaging problem rather than a logic one.

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

The AAR smoke test builds `epdf-uniffi`, which does not link every viewer
plugin. Command callbacks are tested separately against `epdf-viewer-host`:

```bash
KOTLINC=/path/to/kotlinc \
JNA_JAR=/path/to/jna-5.14.0.jar \
COROUTINES_JAR=/path/to/kotlinx-coroutines-core-jvm-1.10.2.jar \
bash platforms/android/test-command-context.sh
```

Set `PDF_RUNTIME_LIB_DIR` and `LD_LIBRARY_PATH` for the native Linux PDFium
runtime as for other native viewer tests. This test uses JDK 21 and Kotlin
2.2.21. It builds the full host, generates every Kotlin component with the
production `uniffi-bindgen`, compiles them together, and executes real command
callbacks. It covers default JSON state, whole replacement including null,
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

It is not in the AAR. The AAR packages `epdf-uniffi`, which has no signature
plugin, so the file compiles only against the full host's bindings. On Android
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
