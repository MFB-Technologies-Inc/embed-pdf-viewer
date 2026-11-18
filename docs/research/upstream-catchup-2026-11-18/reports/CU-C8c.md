# CU-C8c: Kotlin and Android

`research/catchup-2026-11-18-c8c`, cut from `research/catchup-2026-11-18-c8b` at `3d2f67b0f`, builds
the Android fork slices for runtime `af480d0ce`, gives the Kotlin viewer its D-1 transport on
`HttpURLConnection`, and proves it on a desktop JVM with a granted timestamp and a long-term
validation fill through upstream's routes. The AAR builds with every 16 KB check passing, and the
smoke test passes 8 of 8 on the 16 KB emulator, but only after a fix: the arm64 library failed
`dlopen` on `__arm_get_current_vg` until `build-aar.sh` linked the NDK's compiler-rt builtins
(CU-C8c-D9). No upstream TS under `packages/**` changed. Decisions are in
[`../decisions/CU-C8c.md`](../decisions/CU-C8c.md).

## Commits

| Commit | Summary |
|---|---|
| `162e160b7` | `engine-runtime-build.local.json` takes the `af480d0ce` slices' sha256; `build-aar.sh` comment |
| `d21c2bd5a` | `platforms/android/viewer/HttpSignatureTransport.kt`, `test-signature.sh`, `tests/SignatureTest.kt`, `tests/signature-routes.mjs`, README section |
| `3453cdea9` | `build-aar.sh` links `libclang_rt.builtins-<arch>-android.a` and refuses an `__arm_` import; README |
| the docs commit | this report, the decisions, the `decisions.md` index for CU-C8a to CU-C8c, `scripts/cu-c8c/`, `scripts/cu-c7/index-check.sh` path |

## Slices

Built in a temporary `linux/amd64` `debian:trixie` container under Rosetta (CU-C8c-D5) with the
fork's `build-target.sh` and `package-target.sh`, `PDF_RUNTIME_TARGET_OS_LIST="linux android"`,
through `heavy-build.sh`. `build-android-arm64` took 174 s and `build-android-x64` 168 s after the
deps sync; both `BUILD-METADATA.json` name `af480d0ce420ed916db0de8d2e8827822ad7be1e`.

| Archive | sha256 |
|---|---|
| `libembedpdf-runtime-android-arm64-af480d0ce.tar.gz` | `83efd1971ff0a36ff6b547e93905af03938d79907bc5359a2462894166d131b0` |
| `libembedpdf-runtime-android-x64-af480d0ce.tar.gz` | `27b931b2c9d1dc0395196ee2a11c4ba454d769b49dbd581b25c858199fa72019` |

Registered in `packages/engine/runtime/engine-runtime-build.local.json` under the same
unversioned cache paths (CU-C8c-D7); `fetch-libpdfium.sh android-arm64` and `android-x64` accepted
both. Kept in `/workspaces/pdfium/archives` and on the host in
`embed-pdf-runtime-builds/c1118-af480d0ce/android/` with `SHA256SUMS`.

## The Kotlin transport

```kotlin
// platforms/android/viewer/HttpSignatureTransport.kt
class HttpSignatureTransport(
    executor: Executor = sharedExecutor,
    timer: ScheduledExecutorService = sharedTimer,
    open: (URL) -> HttpURLConnection = { it.openConnection() as HttpURLConnection },
) : SignatureTransport {
    override fun post(request: SignatureTransportRequest, completion: SignatureTransportCompletion)
    fun send(request, completion: SignatureTransportCompletionInterface, released: () -> Unit = {})
}
```

One `post` is one attempt on a daemon thread. A timer at `timeoutMs` covers connecting, sending and
reading; a 2xx past `maxReplyBytes` is `tooLarge`; every status is `replied`, and a non-2xx body
that runs long, breaks off or is still arriving at the deadline is that status with no body, as in
the browser (CU-C8c-D3, CU-C8b-D1). The first of reply, deadline and failure is the only answer,
and the completion is destroyed after it. It is not in the AAR (CU-C8c-D2). The other catch-up
surfaces need no Kotlin code: the generated bindings carry them (CU-C8c-D1), and no Kotlin
caller set `validation_time`.

## Tests

`tests/SignatureTest.kt`, run by `test-signature.sh` against the full viewer host's generated
bindings, 15 checks, each printed:

| Group | What it proves |
|---|---|
| loopback `HttpServer` | `replied`: POST, `content-type` then the headers (a later one wins), the body, 201 and 404; `timedOut` on a held reply and on a stalled body, each between 250 and 1,400 ms for a 300 ms limit; a 429 stalled at the deadline is `replied 429` with no body; `tooLarge` by `content-length` and while streaming; exactly 1,000 bytes for a 1,000 limit is a reply; a long 503 is `replied 503` with no body; `failed` with nothing listening and for a malformed URL; after the held replies finish, each completion has one answer and one release |
| the viewer, through upstream's routes | a granted timestamp asked per call (`SignatureCallAuthority.Http`) through `timestampRoute`: verdict `VALID`, timestamp `cryptography` `VALID`, the authority's name and certificate, an 8-octet nonce; a long-term fill from the settings' authority and route: `validationInfo.added.ocspResponses` > 0, an OCSP POST through `revocationRoute`, then `validate` with `SignatureRequestedTime.EarliestTimestamp`: `VALID`, revocation `GOOD`, `longTerm.enabled`, nothing missing |
| one call per surface | `settings()` and `updateSettings` (`clearRevocation`, then `revocation`); `canAddValidationInfo`; `addValidationInfo` refusing `invalid-input` without a route and adding nothing once filled; `SignatureCallAuthority.Without` sending no query; `standardCommands` joined in upstream's order and `document:download` reaching `setCommandsPlatform`'s `saveFile`; `ToolPermission` deciding `canUseTool`; `hasCreationAwaitingClick`; `StampImportOptions.id` naming the library |

The routes come from `tests/signature-routes.mjs`, which serves `packages/core/signature/dist`
with a nonce-echoing stub authority (CU-C8c-D4). The signer is the test CA's issued key, signed
with on the JVM.

## Results

| Check | Where | `3d2f67b0f` | Now (`d21c2bd5a`, AAR rows `3453cdea9`) |
|---|---|---|---|
| `test-command-context.sh`, JDK 21.0.13, kotlinc 2.2.21 | container | pass | pass |
| `test-signature.sh` | container | | pass, 15 of 15 |
| `build-aar.sh`: `LOAD` alignment, both ABIs, both `.so` | Mac | | `0x4000` everywhere |
| `build-aar.sh`: no `__arm_` import in `libepdf_uniffi.so` | Mac | | pass (new check) |
| `assembleRelease` | Mac | | `library-release.aar`, 21,535,845 bytes, sha256 `bc1b2677c4b124012c0830da463d5730b6e957dee139556bef7307cfba9ec6ec` |
| test APK `zipalign -c -P 16` | Mac | | 16 KB page aligned |
| `build-aar.sh test`, `Pixel_4`, `android-37.1` `ps16k` arm64, `getconf PAGE_SIZE` 16384 | Mac | | 8 of 8 (first run 0 of 8, `dlopen`, CU-C8c-D9) |

The 2026-11-12 Kotlin row was a pass, and stays one. Mac steps and why (CU-C8c-D6): NDK r29 has
only a `linux-x86_64` host toolchain and the container is arm64; the emulator needs the Mac. The
host already had platform 36, build-tools 36.0.0, NDK 29.0.14206865, the AVD, Gradle and JDK 21.0.2,
so nothing was installed there. `x86_64` is built and checked but not run, as before.

## Not done

- The transport and the new viewer surfaces are not in the AAR and not run on the emulator
  (CU-C8c-D2).
- Whether the `cf7fdbe7a` AAR fails `dlopen` the same way is inferred from `llvm-nm` on the
  `3c6e02297` slice, not run (CU-C8c-D9).

## Evidence

Logs in `/workspaces/logs/CU-C8c/` (`host/` holds the slice and Mac logs) and
`tmp/cu1118/logs/CU-C8c/` of the host checkout: `slices.status`, `build-android-*.log`,
`package-android-*.log`, `base/`, `head-command-context/`, `head-signature/` (each `summary.txt` and
`run.log`), `mac/aar.log`, `mac/emulator-test.log`, `mac/emulator-test-try1.log` (the `dlopen`
failure), `mac/emulator.log`. Scripts: `scripts/cu-c8c/kotlin.sh`, `slices-setup.sh`, `slices.sh`.
`scripts/cu-c7/index-check.sh`: 86 headings, 0 missing.

## Cleanup

Host free space (`df -g /System/Volumes/Data`) read 438 GB at the start and 429 GB at the end;
`/workspaces` went from 8.8 GB to 12 GB. Deleted, about 9.9 GB: container `cu-c8c-android` and its
volume (8.3 GB, the fork checkout with its Android deps and build trees), its amd64 image (142 MB;
`debian:trixie` was re-tagged to the arm64 image it named before), the Mac worktree (1.4 GB,
with its Cargo target and Gradle build), the generated Kotlin and jars under `/workspaces/target`
(42 MB), the bundles and two scratch `nm` extracts. The emulator is stopped. No trim was run.

Kept: the two slice archives (`/workspaces/pdfium/archives`, 44 MB with the others, and the host
copies with the AAR), `/workspaces/tools` (432 MB: JDK 21.0.13, kotlinc 2.2.21, the JNA and
coroutines jars) for a later Kotlin rerun, and `/workspaces/wt/CU-C8c` (CU-C8b's worktree moved,
with its `node_modules` and the built `core-signature` dist the JVM test reads).
