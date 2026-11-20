# Android Parity Phases 0 and 1: Toolchain and Full Engine Artifact

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a `bindings` Android library from `epdf-viewer-host` (every plugin) in the dev container, with a Gradle wrapper, a version catalog, JVM tests that load the Linux host library, and the 16 KB checks extended to the new `.so`.

**Architecture:** `platforms/android/embedpdf` becomes a multi-module Gradle build. `bindings` holds the NDK `.so` files and the generated Kotlin. `smoke` keeps the existing `epdf-uniffi` smoke assertions as a JVM test. Every command runs inside the `embed-pdf-android` container.

**Tech Stack:** Gradle 8.14.3 wrapper, AGP 8.13.0, Kotlin 2.2.21, JDK 21 (Debian `openjdk-21-jdk-headless`) targeting JVM 17, Android SDK command-line tools with `platforms;android-36` and `build-tools;36.0.0`, NDK r29, JNA 5.14.0, `kotlinx-coroutines` 1.10.2, JUnit 4.13.2.

**Spec:** `docs/superpowers/plans/2026-11-19-android-parity.md`, Phases 0 and 1.

## Global Constraints

- Every command runs in the container (`docker exec embed-pdf-android ...`). Nothing builds or runs on the Mac host.
- No Android emulator exists in the container (Docker on this Mac has no nested virtualization). Device-only checks become JVM tests plus static ELF and zip checks; the README records that limitation with the date.
- Generated Kotlin is never committed.
- Commits: `feat(android): ...` / `build(android): ...`, author `Andrew Roan <roanutil@users.noreply.github.com>`, no co-author or session trailer.
- Package prose names no outside product, company or person.
- If a version above doesn't resolve, use the nearest released one and record it in the commit message.

## Review Focus

- A `.so` built for the full host leaves a compiler-rt `__arm_` symbol undefined: `build-aar.sh` fails, as it does today for `epdf-uniffi`.
- The Linux JVM test loads a host `.so` built in the same container, with `jna.library.path` set by Gradle, not by the caller's shell.
- The generated Kotlin for all components compiles in one Gradle module without the 2 GB heap tweak leaking into consumers (set it in `gradle.properties` for this build only).
- R8: `consumer-rules.pro` keeps every `uniffi.**` package, not just `uniffi.epdf_uniffi`.

---

### Task 1: Container toolchain and Gradle wrapper

**Files:**
- Modify: `.devcontainer/Dockerfile` (JDK 21 and Android SDK layer, amd64 only like the NDK), `.devcontainer/README.md` (tool table rows)
- Create: `platforms/android/embedpdf/gradlew`, `gradlew.bat`, `gradle/wrapper/*`, `gradle/libs.versions.toml`
- Modify: `platforms/android/embedpdf/settings.gradle.kts`, `build.gradle.kts`, `library/build.gradle.kts`, `platforms/android/build-aar.sh` (`./gradlew`, `ANDROID_NDK_HOME` default `/opt/android-ndk-r29` when it exists)

- [x] Install the same layer into the running container by hand (the image is rebuilt from the branch at the end), SDK at `/opt/android-sdk`, `ANDROID_HOME` exported in `/etc/profile.d/android.sh`. Pin the command-line tools zip by SHA-256 in the Dockerfile.
- [x] Generate the wrapper, move versions into the catalog.
- [x] Run `bash platforms/android/build-aar.sh` (still `epdf-uniffi`). Expected: builds, both `.so` report `LOAD alignment 0x4000`.
- [x] Commit.

### Task 2: `bindings` module from `epdf-viewer-host`

**Files:**
- Rename: `library/` to `bindings/` (namespace `com.embedpdf.bindings`)
- Modify: `platforms/android/build-aar.sh` (build `-p epdf-viewer-host`, `--crate-type cdylib` through `cargo rustc` if the crate doesn't declare one; check `crates/epdf-viewer-host/Cargo.toml`), `bindings/consumer-rules.pro` (`-keep class uniffi.** { *; }`, JNA rules unchanged)
- Create: `platforms/android/embedpdf/smoke/` (JVM test module holding `SmokeTest` as a local test)

- [x] Fetch the Android slices from the runtime fork's release `runtime-af480d0ce420ed916db0de8d2e8827822ad7be1e` into `packages/engine/runtime/build/libpdfium/android-{arm64,x64}` with a new `platforms/android/fetch-android-runtime.sh` that verifies each archive against `pdf-runtime-build.generated.json` in that release. `build-aar.sh` calls it when a slice is missing.
- [x] Build both ABIs. Expected: `libepdf_viewer_host.so` per ABI, `LOAD alignment 0x4000`, no undefined `__arm_` symbol.
- [x] Generate Kotlin for every component into `bindings/src/main/kotlin`. Expected: `./gradlew :bindings:assembleRelease` compiles.
- [x] Record `.so` sizes per ABI (`du -h`) for the README.
- [x] Commit.

### Task 3: JVM tests against the Linux host library

**Files:**
- Create: `platforms/android/embedpdf/bindings/src/test/kotlin/com/embedpdf/bindings/ViewerHostLoadTest.kt`
- Modify: `bindings/build.gradle.kts` (a `buildLinuxHost` task that runs `cargo build -p epdf-viewer-host` for the container's host target and sets `systemProperty("jna.library.path", ...)` on `testDebugUnitTest`; Robolectric not needed yet), `smoke/build.gradle.kts` the same for `epdf-uniffi`

- [x] Port `SmokeTest`'s eight tests to `smoke/src/test`, reading fixtures from `pdfium-multiplatform-prototype/fixtures/`. Expected: 8 pass.
- [x] `ViewerHostLoadTest`: build a `ViewerHost` with a config enabling every plugin, `start()`, open `report.pdf` bytes, assert 14 pages, `destroy()`. Expected: pass.
- [x] Port `platforms/android/tests/CommandContextTest.kt` into `bindings/src/test` so `test-command-context.sh` becomes a Gradle test. Keep the script working.
- [x] Commit.

### Task 4: Engine hash and check scripts, README

**Files:**
- Create: `platforms/android/android-engine-hash.sh`, `platforms/android/check-android-engine.sh` (mirror `platforms/apple/apple-engine-hash.sh` and `check-apple-engine.sh`: same Rust inputs, plus `build-aar.sh` and the runtime slice SHA)
- Modify: `platforms/android/README.md` (modules, how to build in the container, the no-emulator limitation, sizes)

- [x] `android-engine-hash.sh` prints a 16-hex hash; running it twice prints the same value; touching a file under `crates/epdf-viewer-host/src` changes it.
- [x] Commit.
