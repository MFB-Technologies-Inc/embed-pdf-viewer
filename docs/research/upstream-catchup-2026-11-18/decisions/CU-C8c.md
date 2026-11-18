# CU-C8c decisions

## CU-C8c-D1. The Kotlin surfaces come from the generated bindings; there is no wrapper to extend

Decision: no hand-written Kotlin wraps uniffi in `platforms/android`. The catch-up's surfaces
(signature settings, the per-call authority, `addValidationInfo`, `canAddValidationInfo`, the
verdict fields, `SignatureRequestedTime`, `ViewerConfig.standardCommands`, `setCommandsPlatform`,
`hasCreationAwaitingClick`, `ToolPermission`, `StampImportOptions.id`) reach Kotlin through the
production `uniffi-bindgen` output of `epdf-viewer-host`, which `test-command-context.sh` and the
new `test-signature.sh` generate. The only hand-written source is the transport,
`platforms/android/viewer/HttpSignatureTransport.kt`. No Kotlin caller set
`SignatureValidateRequest.validation_time` (CU-C8a-D9), so none changed.
Reason: `platforms/android` holds the AAR project, the JVM tests and their scripts. The generated
files are not checked in (`.gitignore`), and every exported item already has a Kotlin name.
Cost: Kotlin callers see uniffi's shapes (`UInt`, `UShort`, sealed classes) with no facade.
Reverse: a facade module over the generated package.

## CU-C8c-D2. The transport is not in the AAR

Decision: `HttpSignatureTransport.kt` sits in `platforms/android/viewer/`, outside the Gradle
source sets, and compiles only with the full host's bindings.
Reason: the AAR packages `epdf-uniffi` (`build-aar.sh`), which has no signature plugin and so no
`SignatureTransport` type to implement. The README already records that the AAR does not link
every viewer plugin.
Cost: an Android app cannot get the transport, or any of the new viewer surfaces, from the AAR.
They are proven on a desktop JVM only, not on the emulator.
Reverse: build the AAR from `epdf-viewer-host` and move the file into
`library/src/main/kotlin` beside the generated code (outside the directory `build-aar.sh` clears).

## CU-C8c-D3. One timer covers the whole exchange; a non-2xx status in hand at the deadline is that reply

Decision: `send` schedules the deadline at `timeoutMs` on a shared daemon timer. When it fires
first, the answer is `timedOut` and the connection is cut, unless a status outside 200 to 299 has
already arrived, in which case it is `replied` with that status and no body. `connectTimeout` and
`readTimeout` are set to the same value as a second guard; their `SocketTimeoutException` is also
`timedOut`. An `AtomicBoolean` makes the first of reply, deadline or failure the only answer, and
the uniffi completion is destroyed after it. Every status is `replied`; a 2xx body past
`maxReplyBytes`, by `content-length` or while read, is `tooLarge`.
Reason: `HttpURLConnection`'s timeouts are per operation, so a server that trickles bytes would
outlive `timeoutMs`; the client has no backstop (CU-C8a). The non-2xx rule is CU-C8b-D1's, so the
browser and Kotlin answer the same way.
Cost: a server that holds a non-2xx body open costs up to `timeoutMs` before the status is
answered, as in the browser. `HttpURLConnection` follows same-protocol redirects by default and
the transport leaves that on.
Reverse: answer a non-2xx status at once without reading its body.

## CU-C8c-D4. The JVM test signs against upstream's routes, served by a Node helper

Decision: `tests/signature-routes.mjs` serves `timestampRoute` and `revocationRoute` from
`packages/core/signature/dist` on a loopback port. The timestamp authority behind the route is
`createTestTimestampAuthority()`, with the query's nonce set on the token while it signs (CU-C8b-D4's
patch, taking the asn1js class from the token's own serial number); the revocation authority is
`createTestCertificateAuthority()`. The helper hands the JVM the issued signer's chain and PKCS #8
key, and Kotlin signs with `SHA256withRSA`.
Reason: CU-C8a-D8 leaves a reply computed from the request as the only way to a granted token, and
upstream's own routes are the D-2 wire format a Kotlin app talks to.
Cost: `test-signature.sh` needs `node` and a built `core-signature` dist, and the test depends on
`buildTimestampToken` encoding through `TSTInfo.toSchema`.
Reverse: a Kotlin timestamp authority over a JVM crypto library.

## CU-C8c-D5. The slices were built in an amd64 Debian container under Rosetta

Decision: `android-arm64` and `android-x64` at fork `af480d0ce` were built in container
`cu-c8c-android` from `debian:trixie` (`linux/amd64`, image
`sha256:51e269e6af18ae477f7880a0820f34863e34366c621d93b0dd939260ea62e59b`), with `git`, `curl`,
`python3`, `xz-utils`, `lsb-release`, `sudo`, `file`, `pkg-config`, `procps`, `patch` and
depot_tools from `main`, the fork cloned from the host's `catchup/2026-11-18-runtime`. The first
run stopped at `apply-patches.sh` for want of `patch`. The container, its volume and image were
removed once the archives were copied out.
Reason: the brief, and `BUILDCONFIG.gn` asserts a Linux host for Android.
Cost: emulated, as the 2026-11-09 build was. Its `gclient sync` fetched the fork's deps fresh.
Reverse: a native x64 Linux host.

## CU-C8c-D6. The AAR and its 16 KB checks ran on the Mac

Decision: `build-aar.sh` and `build-aar.sh test` ran on the Mac host in
a separate host worktree, with the SDK already there (platform 36, build-tools 36.0.0, NDK
29.0.14206865, the `Pixel_4` AVD on `android-37.1` `google_apis_playstore_ps16k` arm64), Gradle
from Homebrew and JDK 21.0.2.
Reason: NDK r29 ships only a `linux-x86_64` host toolchain, and the job's container is arm64. An
amd64 container would need the SDK and NDK installed (several GB) and could not run the emulator,
which has to run on the Mac anyway.
Cost: the AAR rows are not from the project's container.
Reverse: an amd64 container with the SDK, as on 2026-11-09.

## CU-C8c-D7. The slices keep their unversioned file names in the local pin

Decision: `engine-runtime-build.local.json` keeps its `android-*` URLs
(`pdfium-multiplatform-prototype/build/cache/libembedpdf-runtime-android-<cpu>.tar.gz`) and takes
the new sha256 values; `fetch-libpdfium.sh` checks them.
Reason: the sha256 is what names the build, and the cache directory is ignored, so a versioned
name would only move the files.
Cost: an old archive left in the cache fails the check instead of being found beside the new one.
Reverse: put the fork commit in the file name.

## CU-C8c-D8. `index-check.sh` reads the directory it sits in

Decision: `scripts/cu-c7/index-check.sh` changes to the decisions directory relative to itself
instead of `/workspaces/wt/CU-C7/...`, and is executable.
Reason: that worktree no longer exists, and the brief asks for the check to be run.
Cost: none.
Reverse: not intended.

## CU-C8c-D9. `build-aar.sh` links the NDK's compiler-rt builtins

Decision: each slice's `cargo build` gets `-C link-arg=<NDK>/lib/clang/21/lib/linux/libclang_rt.builtins-<arch>-android.a`
through `CARGO_TARGET_<T>_RUSTFLAGS`, and the script fails if `libepdf_uniffi.so` still imports an
`__arm_` symbol.
Reason: the first emulator run failed 8 of 8 at `dlopen`: `cannot locate symbol
"__arm_get_current_vg"`. The fork's arm64 libyuv objects `rotate_sme.o`, `row_sme.o` and
`scale_sme.o` import `__arm_get_current_vg` and `__arm_tpidr2_save`; rustc links with
`-nodefaultlibs`, so clang's builtins, which define both, were never linked. The `3c6e02297`
slices import the same three objects' symbols (`llvm-nm`), so the AAR registered at `cf7fdbe7a`
would fail the same way. That is inferred, not run: its AAR was built on 2026-11-09 without an
emulator, and the last emulator pass (2026-09-23) predates libyuv's SME rows.
Cost: the link now depends on the NDK's builtins path layout (`lib/clang/*/lib/linux`); a layout
change fails the build at `ls` rather than silently.
Reverse: build the fork's arm64 slice without libyuv's SME rows.
