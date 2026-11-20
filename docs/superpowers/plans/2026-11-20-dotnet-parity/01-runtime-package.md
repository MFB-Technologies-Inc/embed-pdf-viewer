# Phase 1: Runtime Package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `epdf-viewer-host` as an `EmbedPDF.Runtime` NuGet package (`runtimes/<rid>/native/`, both the host and its PDFium), plus a minimal `EmbedPDF` facade assembly whose resolver finds that layout, proven by a consumer that restores both from a local feed and reads a fixture's page count.

**Architecture:** A script generates the internal C# bindings into the facade project. A per-RID build script produces the host cdylib; `package.py` gains a staging action (checksum-verified PDFium, relocatable runpath, native inspection) and a pack action that merges staged RID slices into one `EmbedPDF.Runtime` nupkg. The facade registers a `DllImportResolver` that preloads PDFium beside the host and turns a missing RID into a typed exception. An engine hash, computed like `apple-engine-<hash>`, versions the runtime package `0.0.0-engine.<hash>`.

**Tech Stack:** Rust 1.98.0, `uniffi =0.32.0`, `crates/uniffi-bindgen-cs` (fork at `91345690ef91dd2dbc9fa0738967c8f49478f056`), .NET SDK 10.0.403 `net10.0`, xUnit 2.9.3, Python 3 `unittest`, bash, `patchelf` 0.18.0 (Linux staging), `install_name_tool` (macOS staging).

**Spec:** `docs/superpowers/plans/2026-11-20-dotnet-parity.md`, sections D1 to D3, Review Focus item 5, "Phase 1" and "Phase 0 results".

## Global Constraints

- All work in the dev container `embedpdf-dotnet-parity`, repo `/workspaces/embed-pdf-viewer`. No host (macOS) builds or edits.
- Branch `research/dotnet-parity-p1` from `research/dotnet-parity`. Commits by explicit path, `feat(dotnet): ...` or `chore(dotnet): ...`, no trailers. No push, no merge.
- No TypeScript or `packages/**` change, no plugin Rust behaviour change. `crates/epdf-viewer-host/build.rs` stays as is: it is an input of `apple-engine-hash.sh`, and changing it would force an Apple engine release. Runpaths are fixed at staging time instead.
- `platforms/dotnet/EmbedPDF` (the `EmbedPDF.Engine` package) is not modified. Its tests (`EmbedPDF.Tests`) and `packaging/test_package.py` stay green.
- New facade at `platforms/dotnet/EmbedPDF.Viewer/` (assembly and package id `EmbedPDF`, namespace `EmbedPDF`). Runtime package project at `platforms/dotnet/EmbedPDF.Runtime/`. Generated C# at `platforms/dotnet/EmbedPDF.Viewer/Generated/`, git-ignored, never committed.
- RIDs: `linux-x64`, `linux-arm64`, `osx-arm64`, `osx-x64`, `win-x64`. Runtime package version `0.0.0-engine.<16 hex>`.
- Generated types stay `internal` (`access_modifier = "internal"`).
- One `CARGO_TARGET_DIR` (`target/`). Throwaway outputs deleted after use.
- Repo prose names no outside product, company or person. No em dashes in docs.
- The generator dependency stays `git = "file:///workspaces/uniffi-bindgen-cs"`; hosting it waits for the user.

## Review Focus

1. A consumer with no `EPDF_NATIVE_DIR`, `LD_LIBRARY_PATH` or `DYLD_LIBRARY_PATH` loads the host from the restored package and the host finds PDFium beside itself. Pinned by Task 6's consumer run with those variables removed.
2. A missing native library for the running RID fails with `NativeRuntimeNotFoundException` (a `PdfException`) naming the RID and the searched paths, not a bare `DllNotFoundException`. Pinned by Task 5's resolver tests and Task 6's hidden-library consumer run.
3. A packaged host that still carries a build-tree runpath or an absolute dependency loads on the build machine and fails anywhere else. Pinned by Task 3: staging rejects a runpath other than `$ORIGIN` (existing `check_elf`) and the test feeds it the spike's absolute runpath.
4. A runtime package assembled from a slice whose files changed after staging ships unverified bytes. Pinned by Task 3's `pack-runtime` test that edits a staged file and expects a checksum error.
5. An engine-hash crate list that drifts from what `epdf-viewer-host` links leaves a crate unhashed, so a Rust change ships under an old version. Pinned by Task 4's test comparing `DOTNET_CRATES` with `cargo metadata`.

---

### Task 1: Binding generation script

**Files:**
- Create: `platforms/dotnet/generate-bindings.sh`, `platforms/dotnet/generate-bindings.test.sh`, `platforms/dotnet/EmbedPDF.Viewer/uniffi.toml` (moved from `platforms/dotnet/spike/uniffi-cs.toml`, same keys)
- Modify: `platforms/dotnet/.gitignore` (add `EmbedPDF.Viewer/Generated/`), `crates/uniffi-bindgen-cs/Cargo.toml` (comment only: no longer "spike only")

**Interfaces:**
- Produces: `generate-bindings.sh [--library <path>] [--out-dir <dir>]`. Defaults: library `target/release/libepdf_viewer_host.<so|dylib>` (`epdf_viewer_host.dll` on Windows), out dir `platforms/dotnet/EmbedPDF.Viewer/Generated`. Builds `target/debug/uniffi-bindgen-cs` with `cargo build --locked -p epdf-uniffi-bindgen-cs`, empties the out dir, runs the generator with `--no-format --library <lib> --config platforms/dotnet/EmbedPDF.Viewer/uniffi.toml --out-dir <dir>`, and fails unless at least one `.cs` file results. Exit 64 for bad arguments, 66 for a missing library.

- [ ] **Step 1:** Write `generate-bindings.test.sh` (style of `packaging/render-guide.test.sh`): unknown flag exits 64; `--library /nonexistent` exits 66 and prints the path; when `target/release/libepdf_viewer_host.so` exists, generation into a temp dir yields 25 `.cs` files, `grep -L "internal"` is empty for `epdf_viewer_host.cs`, `DestroyViewer` appears, and a stale `stale.cs` placed in the out dir is gone.
- [ ] **Step 2:** Run `bash platforms/dotnet/generate-bindings.test.sh`. Expected: FAIL (script missing).
- [ ] **Step 3:** Implement the script and move the config.
- [ ] **Step 4:** Run the test. Expected: all checks PASS.
- [ ] **Step 5:** Commit `feat(dotnet): generate viewer bindings into the facade project`.

### Task 2: Per-RID host build

**Files:**
- Create: `platforms/dotnet/build-viewer-host.sh`

**Interfaces:**
- Produces: `build-viewer-host.sh <rid>` runs `cargo rustc -p epdf-viewer-host --release --locked --target <triple> --crate-type cdylib` and prints the absolute library path on its last stdout line. Triples are those of `package.py` `RIDS`. Unknown RID exits 64. The PDFium the build links is whatever `build-support` resolves (`PDF_RUNTIME_LIB_DIR`, default `packages/engine/runtime/build/libpdfium/<fork target>`), fetched beforehand with `packages/engine/runtime/scripts/fetch-libpdfium.sh <fork target>`.

- [ ] **Step 1:** Add a `build-viewer-host.sh` case to `generate-bindings.test.sh`: `bash build-viewer-host.sh bogus` exits 64 and lists the five RIDs.
- [ ] **Step 2:** Run it. Expected: FAIL.
- [ ] **Step 3:** Implement the script.
- [ ] **Step 4:** Run the test, then `bash platforms/dotnet/build-viewer-host.sh linux-arm64` (guard first). Expected: test PASS; last line `.../target/aarch64-unknown-linux-gnu/release/libepdf_viewer_host.so`.
- [ ] **Step 5:** Commit `feat(dotnet): build the viewer host per RID`.

### Task 3: Runtime staging and packing in `package.py`

**Files:**
- Modify: `platforms/dotnet/packaging/package.py`, `platforms/dotnet/packaging/schema.py` (add `else`), `platforms/dotnet/package-manifest.schema.json`, `platforms/dotnet/validation/record.schema.json`, `platforms/dotnet/packaging/test_package.py`, `platforms/dotnet/packaging/README.md` (new section only)
- Create: `platforms/dotnet/EmbedPDF.Runtime/EmbedPDF.Runtime.csproj`, `platforms/dotnet/EmbedPDF.Runtime/EmbedPDF.Runtime.props`

**Interfaces:**
- `VIEWER_RIDS`: rid to `(rust triple, fork target, host file, PDFium archive member)`, host files `libepdf_viewer_host.so`, `libepdf_viewer_host.dylib`, `epdf_viewer_host.dll`.
- `package.py stage-runtime --rid R --native-lib PATH --out DIR [--runtime-manifest M] [--runtime-archive A]`: same pin and checksum rule as `pack`; copies the host and PDFium into `DIR/runtimes/R/native/`; Linux: `patchelf --set-rpath '$ORIGIN'`; macOS: `install_name_tool` sets the id `@rpath/libepdf_viewer_host.dylib`, removes every LC_RPATH but `@loader_path`, adds `@loader_path`; then runs `inspect()` (extended so the host is held to the `$ORIGIN` rule like `libepdf_capi.so`, and `check_viewer_exports` requires `ffi_epdf_viewer_host_uniffi_contract_version`); writes `DIR/runtime-slice.R.json` `{schemaVersion:1, rid, source, runtimeFork, runtimeArchiveSha256, runtimeTarget, files:[{path,sha256,bytes}]}` plus notices under `runtimes/R/native/`.
- `package.py pack-runtime --slice DIR [--slice DIR ...] --version V --out OUT`: re-hashes every slice file against its slice record (error `slice checksum mismatch: <path>`), refuses two slices for one RID or differing `runtimeFork`, runs `dotnet pack EmbedPDF.Runtime/EmbedPDF.Runtime.csproj -p:PackageVersion=V -p:EpdfNativeAssets=<merged runtimes>`, checks every slice file is in the nupkg, writes `OUT/package-manifest.runtime.json` with `packageId: "EmbedPDF.Runtime"`, `rids[r].status` `PACKED_UNVALIDATED` for packed RIDs, `UNVALIDATED` for the rest.
- Schema: `packageId` enum `EmbedPDF.Engine`, `EmbedPDF.Runtime`; `fontManifest` and the one-packed-RID rule apply only to `EmbedPDF.Engine`. Record schema: optional `package` (same enum) and `phase` (1 to 6); `slice` required unless `package` is `EmbedPDF.Runtime`, which requires `phase`.
- `EmbedPDF.Runtime.props` holds `<EmbedPdfRuntimeVersion>0.0.0-engine.<hash></EmbedPdfRuntimeVersion>`, the pin Task 4 checks.

- [ ] **Step 1: Failing tests** in `test_package.py`, class `RuntimeTests`: `test_runtime_manifest_allows_several_packed_rids` (linux-arm64 and linux-x64 both `PACKED_UNVALIDATED`, no `fontManifest`, validates); `test_engine_manifest_rule_unchanged` (same rids under `EmbedPDF.Engine` raises); `test_record_requires_phase_for_runtime` (runtime record without `phase` raises "phase", with `phase: 1` and no `slice` validates; engine record without `slice` raises "slice"); `test_viewer_host_needs_origin_runpath` (`check_elf("libepdf_viewer_host.so", "(RUNPATH) Library runpath: [/workspaces/x/lib]", "")` raises "relative"); `test_viewer_exports` (`check_viewer_exports("x")` raises "contract_version"); `test_pack_runtime_rejects_edited_slice` (a fake slice dir whose file differs from its record makes `package.py pack-runtime` exit 1 with "slice checksum mismatch"); `test_pack_runtime_rejects_duplicate_rid`.
- [ ] **Step 2:** Run `cd platforms/dotnet/packaging && python3 -m unittest -v`. Expected: the new tests FAIL, the old ones PASS.
- [ ] **Step 3:** Implement the schema, `schema.py` `else`, `package.py` actions, the csproj (no compile items, `IncludeBuildOutput=false`, `NoWarn NU5128`, `SuppressDependenciesWhenPacking`, `RequireNativeAssets` target as in `EmbedPDF.csproj`) and the props file.
- [ ] **Step 4:** Run the unit tests (all PASS), then stage and pack linux-arm64 for real (guard first): `python3 platforms/dotnet/packaging/package.py stage-runtime --rid linux-arm64 --native-lib <Task 2 path> --out /tmp/epdf-rt/linux-arm64` and `pack-runtime --slice /tmp/epdf-rt/linux-arm64 --version 0.0.0-engine.<hash> --out /tmp/epdf-rt/pack`. Expected: `readelf -d` on the packaged host shows `RUNPATH [$ORIGIN]`, `ldd` resolves `libembedpdf.so` inside the native dir.
- [ ] **Step 5:** Commit `feat(dotnet): stage and pack the EmbedPDF.Runtime package`.

### Task 4: Engine hash and pin check

**Files:**
- Create: `platforms/dotnet/dotnet-engine-hash.sh`, `platforms/dotnet/check-dotnet-engine.sh`, `platforms/dotnet/dotnet-engine-hash.test.sh`
- Modify: `platforms/dotnet/EmbedPDF.Runtime/EmbedPDF.Runtime.props` (the real pin)

**Interfaces:**
- `dotnet-engine-hash.sh [--full]`: same algorithm as `platforms/apple/apple-engine-hash.sh`; `DOTNET_CRATES` is every workspace crate `epdf-viewer-host` links; extra inputs `Cargo.toml`, `Cargo.lock`, `rust-toolchain.toml`, `platforms/dotnet/build-viewer-host.sh`, `platforms/dotnet/packaging/package.py`, `platforms/dotnet/EmbedPDF.Runtime/EmbedPDF.Runtime.csproj`, and the `runtime-src` gitlink. Prints 16 hex chars (64 with `--full`).
- `check-dotnet-engine.sh [--props <path>]`: compares `EmbedPdfRuntimeVersion` with `0.0.0-engine.<hash>`. Exit codes as `check-apple-engine.sh`: 0 current, 1 stale and release `dotnet-engine-<hash>` unpublished, 2 stale but published, 64 bad args or no pin line, 69 `gh` failure, 70 hash failure. `EMBEDPDF_RUNTIME_REPO` as for Apple.

- [ ] **Step 1:** Write `dotnet-engine-hash.test.sh`: output matches `^[0-9a-f]{16}$`, `--full` matches 64 hex and starts with the short hash, a bad flag exits 2; `DOTNET_CRATES` equals the set of workspace path dependencies reachable from `epdf-viewer-host` (normal and build edges) per `cargo metadata --locked --format-version 1`, as `crates/` relative directories; `check-dotnet-engine.sh --props <temp props with the current version>` exits 0 and prints `current 0.0.0-engine.<hash>`; a props file without the line exits 64; `--bogus` exits 64.
- [ ] **Step 2:** Run it. Expected: FAIL (scripts missing).
- [ ] **Step 3:** Implement both scripts.
- [ ] **Step 4:** Run the test. Expected: PASS.
- [ ] **Step 5:** Commit `feat(dotnet): hash and check the .NET engine pin`. The pin itself is rewritten last, in Task 8, after every hashed input is final.

### Task 5: Facade skeleton, resolver and ported tests

**Files:**
- Create: `platforms/dotnet/EmbedPDF.Viewer/EmbedPDF.Viewer.csproj`, `NativeRuntime.cs`, `NativeLibraryResolver.cs`, `PdfException.cs`, `NativeRuntimeNotFoundException.cs`; `platforms/dotnet/EmbedPDF.Viewer.Tests/EmbedPDF.Viewer.Tests.csproj`, `ViewerHostTests.cs` (the four spike tests), `NativeLibraryResolverTests.cs`
- Modify: `platforms/dotnet/EmbedPDF.sln` (add both projects)
- Delete: `platforms/dotnet/spike/`

**Interfaces:**
- Public: `class PdfException : Exception` (message, inner ctors); `sealed class NativeRuntimeNotFoundException : PdfException` with `string RuntimeIdentifier` and `IReadOnlyList<string> SearchedPaths`, message naming both; `static class NativeRuntime` with `string RuntimeIdentifier { get; }`, `void EnsureLoaded()`, `Task<int> ReadPageCountAsync(ReadOnlyMemory<byte> pdf, CancellationToken cancellationToken = default)` (a load check: opens the bytes in a throwaway viewer and returns the page count; Phase 2's `PdfViewer` is the real API).
- Internal: `NativeLibraryResolver.Resolve(string rid, string baseDirectory, INativeLoader loader) -> IntPtr`, where `INativeLoader` has `bool Exists(string path)` and `bool TryLoad(string path, out IntPtr handle)`. Search order: `baseDirectory`, then `baseDirectory/runtimes/<rid>/native`; in each, preload PDFium (`libembedpdf.so`, `libembedpdf.dylib`, `embedpdf.dll`) if present, then load the host. RID outside the five throws at once. Nothing found throws `NativeRuntimeNotFoundException`. A `[ModuleInitializer]` registers it with `NativeLibrary.SetDllImportResolver` for the name `epdf_viewer_host` only; before throwing, the registered resolver falls back to `NativeLibrary.TryLoad(name, assembly, searchPath)`.
- csproj: `AssemblyName`/`PackageId` `EmbedPDF`, `RootNamespace` `EmbedPDF`, `IsAotCompatible`, `Compile Include="Generated/*.cs"`, an error target when `Generated/` is empty naming `generate-bindings.sh`, `InternalsVisibleTo EmbedPDF.Viewer.Tests`, `PackageReference EmbedPDF.Runtime` at `$(EmbedPdfRuntimeVersion)` only when packing (`-p:EmbedPdfPackRuntime=true`), so in-repo builds need no feed.

- [ ] **Step 1: Failing tests** in `NativeLibraryResolverTests`: `MissingLibraryThrowsTypedExceptionNamingRid` (fake loader with nothing present, rid `linux-arm64`: exception type `NativeRuntimeNotFoundException`, `RuntimeIdentifier == "linux-arm64"`, message contains `linux-arm64`, `SearchedPaths` holds both directories); `UnsupportedRidThrowsAtOnce` (`linux-musl-x64`, message contains it, loader never called); `PreloadsPdfiumBesideHostInPackageLayout` (files present only under `runtimes/linux-x64/native`: loader sees `libembedpdf.so` before `libepdf_viewer_host.so`, returns the host handle); `PrefersApplicationDirectory`; `IsAssignableToPdfException`. Port the four spike tests unchanged except the namespace `EmbedPDF.Tests.Viewer`.
- [ ] **Step 2:** Generate bindings, then `dotnet test platforms/dotnet/EmbedPDF.Viewer.Tests`. Expected: build FAIL (types missing).
- [ ] **Step 3:** Implement the skeleton. The test project copies the host from `$(EpdfViewerNativeDir)` (default `target/release/`) beside the tests.
- [ ] **Step 4:** Run the tests. Expected: 9 passed, 0 warnings. Delete `platforms/dotnet/spike/` and `target/csharp-spike/`.
- [ ] **Step 5:** Commit `feat(dotnet): add the EmbedPDF facade skeleton and native resolver`.

### Task 6: Consumer test from a local feed

**Files:**
- Create: `platforms/dotnet/packaging/ViewerSmoke.cs`
- Modify: `platforms/dotnet/packaging/package.py` (`validate-runtime` action), `platforms/dotnet/packaging/test_package.py`

**Interfaces:**
- `package.py validate-runtime --rid R --package <EmbedPDF.Runtime nupkg> --facade <EmbedPDF nupkg> --manifest <package-manifest.runtime.json> --record <path>`: requires the native host RID (or `EPDF_EMULATED=true` for linux-x64), verifies the nupkg hash against the manifest, builds a fresh consumer (`Consumer.csproj` referencing `EmbedPDF` only, `NUGET_PACKAGES` private, `EPDF_NATIVE_DIR`, `LD_LIBRARY_PATH`, `DYLD_*` removed), runs it on `pdfium-multiplatform-prototype/fixtures/report.pdf` expecting `PASS: rid=R pages=14`, then hides the host and expects exit 3 with `NativeRuntimeNotFoundException` and `R` in the output, then hides PDFium only and expects a non-zero exit. Marks the RID `VALIDATED` (or `container-emulated`) and writes a record with `package: "EmbedPDF.Runtime"`, `phase: 1`.
- `ViewerSmoke.cs`: prints `PASS: rid=<RuntimeIdentifier> pages=<n>`; catches `NativeRuntimeNotFoundException`, prints it and exits 3.

- [ ] **Step 1:** Failing test `test_validate_runtime_rejects_unpacked_rid` (manifest with linux-arm64 `UNVALIDATED`: exit 1, "no packed artifact").
- [ ] **Step 2:** Run it. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Unit tests PASS; real run for linux-arm64 writes `platforms/dotnet/validation/2026-11-20-phase-1-runtime-linux-arm64/record.json` with exit 0.
- [ ] **Step 5:** Commit `feat(dotnet): validate the runtime package from a local feed`, then the record in its own `chore(dotnet): record ...` commit.

### Task 7: linux-x64 and the release workflow

**Files:**
- Modify: `.github/workflows/release-dotnet.yml`
- Create (if the build works): `platforms/dotnet/validation/2026-11-20-phase-1-runtime-linux-x64/record.json`

- [ ] **Step 1:** In the container: `sudo apt-get install patchelf crossbuild-essential-amd64`, `rustup target add x86_64-unknown-linux-gnu`, fetch the linux-x64 PDFium archive, build with `CARGO_TARGET_X86_64_UNKNOWN_LINUX_GNU_LINKER=x86_64-linux-gnu-gcc`, stage and pack both RIDs into one package. If x86_64 binaries execute in the container (binfmt), validate with `EPDF_EMULATED=true` and an x64 SDK; otherwise record linux-x64 as packed only.
- [ ] **Step 2:** Workflow: a `runtime` job with one row per RID (all five; `osx-x64` on `macos-15-intel`), each building the host, staging its slice and uploading `dotnet-runtime-slice-<rid>`; a `runtime-pack` job on `ubuntu-24.04-arm` merging the slices into `EmbedPDF.Runtime`, generating bindings, packing `EmbedPDF`, and running `validate-runtime` for linux-arm64; an `osx-arm64` validate step on `macos-15` downloading the merged packages. Checked with `python3 -c "import yaml; yaml.safe_load(open(...))"`; not run.
- [ ] **Step 3:** Commit `feat(dotnet): build and pack EmbedPDF.Runtime in the release workflow`.

### Task 8: Pin, sizes, plan records

**Files:**
- Modify: `platforms/dotnet/EmbedPDF.Runtime/EmbedPDF.Runtime.props`, `platforms/dotnet/README.md` (new section "Viewer packages"), `docs/superpowers/plans/2026-11-20-dotnet-parity.md` (Phase 1 boxes, exit, Definition of done, Phase 1 notes)

- [ ] **Step 1:** Write the pin from `dotnet-engine-hash.sh`; `check-dotnet-engine.sh` exits 0.
- [ ] **Step 2:** Record packaged sizes per built RID from the pack manifest; README section.
- [ ] **Step 3:** Run every suite: `generate-bindings.test.sh`, `dotnet-engine-hash.test.sh`, `python3 -m unittest discover -s platforms/dotnet/packaging -v`, `dotnet test EmbedPDF.Viewer.Tests`, `dotnet test EmbedPDF.Tests`, `cargo fmt --check -p epdf-uniffi-bindgen-cs`, `cargo clippy -p epdf-uniffi-bindgen-cs -- -D warnings`.
- [ ] **Step 4:** Commit `chore(dotnet): pin the .NET engine and record Phase 1`.
