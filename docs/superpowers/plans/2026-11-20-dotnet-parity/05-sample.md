# Phase 5: Sample and Consumer Check Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A console sample that exercises the viewer end to end, built only from packed packages in a local feed, run framework-dependent and as a NativeAOT binary, with the three remaining managed packages packed beside `EmbedPDF.Runtime` and `EmbedPDF`.

**Architecture:** A new `packaging/libraries.py` packs `EmbedPDF.Models` (exact `[x.y.z]` dependency on `EmbedPDF`, checked), `EmbedPDF.Extensions.DependencyInjection` and `EmbedPDF.Scripting.Jint` against a packed facade. `dotnet-consumer.sh` builds or takes those packages, copies the sample out of the tree, restores it from the local feed (package source mapping keeps every `EmbedPDF*` id on the local feed), runs it, publishes it with `PublishAot=true`, checks the AOT warnings against an allow list, runs the binary and reports sizes. The release workflow gains a `consumer` job over `runtime-pack`'s packages.

**Tech Stack:** .NET SDK 10.0.403 `net10.0`, NativeAOT (clang, lld), Python 3 `unittest`, bash, `Microsoft.Extensions.DependencyInjection` 10.0.0 (sample only), Jint 4.18.0 through `EmbedPDF.Scripting.Jint`.

**Spec:** `docs/superpowers/plans/2026-11-20-dotnet-parity.md`: Global Constraints, D3, D6, D7, Review Focus 4 and 5, the Phase 1 notes (packaging), Phase 4 notes (exact `EmbedPDF` pin), Phase 5 and Phase 6.

## Global Constraints

- Dev container `embedpdf-dotnet-parity`, worktree `/workspaces/wt-p5`, branch `research/dotnet-parity-p5` from `d16ef2e6d`. `CARGO_TARGET_DIR=/workspaces/embed-pdf-viewer/target`. Guard script before heavy builds and between tasks.
- Commits by explicit path, `feat(dotnet): ...`, `test(dotnet): ...`, `docs(dotnet): ...`, `ci(dotnet): ...`, no trailers. No push, no merge, no CI run.
- No TypeScript, `packages/**` or Rust change. `platforms/dotnet/EmbedPDF` untouched; `EmbedPDF.Engine` packaging and its tests stay green.
- `platforms/dotnet/packaging/package.py` is an input of `dotnet-engine-hash.sh`; editing it moves the engine version and forces a new `EmbedPDF.Runtime` (Phase 1 notes). Managed packaging therefore lives in a new `packaging/libraries.py`, which imports helpers from `package.py` without changing it.
- No UI framework, no `ICommand`, no framework bitmap type (D6). The PNG encoder is the sample's own: `System.IO.Compression.ZLibStream` plus CRC-32, no image package.
- `net10.0`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`. The sample is not in `EmbedPDF.sln` (it builds only against a feed).
- Prose and comments name no outside product, company or person. No em dashes.
- NativeAOT publish output is deleted after it is measured; report `du -sh` before and after.

## Decisions this plan makes

- **Exact pin:** `EmbedPDF.Models.csproj` gets a target after NuGet's `_GetProjectReferenceVersions` that rewrites the `EmbedPDF.Viewer` reference's `ProjectVersion` to `[V]`, so the nuspec reads `<dependency id="EmbedPDF" version="[V]" />`. `libraries.py` fails the pack when the range is anything else.
- **Package versions:** the three packages take the facade's version (`0.0.0-local` by default); `pack-libraries` reads it from the facade nupkg's file name.
- **Sample output:** one line per step, `PASS <step>: <detail>`, or `FAIL <step>: <exception type>: <message>` and exit 1. Steps in order: `di`, `open`, `search`, `render`, `annotate`, `save`, `model`, `script`. `--no-script` skips the Jint step and prints `SKIP script`.
- **Jint in the AOT sample:** included. Jint's IL2026, IL2104 and IL3053 come from CLR interop paths the engine never enables (Phase 3 wave B notes), so the sample lists exactly those three in `WarningsNotAsErrors` and the consumer script fails on any other IL or AOT warning, or on any of these three naming an `EmbedPDF` assembly.
- **Consumer modes:** `dotnet-consumer.sh` without `--packages` builds the host, stages and packs the runtime (`--allow-dirty`), generates bindings and packs the four managed packages; with `--packages DIR` it uses the five nupkgs in `DIR` (CI). `EPDF_DOTNET` picks the `dotnet` binary, so linux-x64 runs under the container's emulation with `/workspaces/dotnet-x64/dotnet`.

## Review Focus

1. An AOT binary that builds but drops a generated P/Invoke, callback thunk or JSON context fails only at run time. Pinned by Task 4: the AOT binary must print every `PASS` line, including `annotate` (JSON drafts), `model` (plugin event callbacks) and `script` (foreign-trait engine).
2. A consumer that restores `EmbedPDF` from a public feed instead of the local one tests the wrong bytes. Pinned by Task 4's `nuget.config` with `packageSourceMapping` (`EmbedPDF*` on the local feed only) and a private `NUGET_PACKAGES`.
3. `EmbedPDF.Models` packed with a `>=` range lets a consumer pair it with a facade lacking the internals it calls. Pinned by Task 1's `test_models_dependency_must_be_exact`.
4. A PNG with a bad CRC or a short IDAT opens in some viewers and not others. Pinned by Task 2's `png_check.py` tests and Task 4 running it on every sample output.
5. A sample run that inherits `EPDF_NATIVE_DIR` or `LD_LIBRARY_PATH` passes without the package layout. Pinned by Task 4 scrubbing the same variables as `package.py` `SCRUBBED_ENV`.

---

### Task 1: Pack the managed libraries

**Files:**
- Create: `platforms/dotnet/packaging/libraries.py`
- Modify: `platforms/dotnet/EmbedPDF.Models/EmbedPDF.Models.csproj` (exact-range target), `platforms/dotnet/packaging/test_package.py` (class `LibraryPackTests`)

**Interfaces:**
- `libraries.py pack-libraries --facade <EmbedPDF.V.nupkg> --runtime-package <EmbedPDF.Runtime.R.nupkg> --out DIR`: for each of `LIBRARIES = {"EmbedPDF.Models": exact, "EmbedPDF.Extensions.DependencyInjection": minimum, "EmbedPDF.Scripting.Jint": minimum}` runs `dotnet pack <project> -c Release -o DIR -p:PackageVersion=V -p:EmbedPdfPackRuntime=true -p:RestoreAdditionalProjectSources=<runtime dir>` with a private `NUGET_PACKAGES`, then `check_library(nupkg, package_id, facade_version, exact)`. Exit 1 with `ERROR: ...` on failure, as `package.py`.
- `dependency_range(nuspec_text, package_id) -> str | None`; `check_library(...)` raises `ValueError` when the nupkg lacks `lib/net10.0/<id>.dll`, carries `EmbedPDF.Viewer.dll`, or the `EmbedPDF` range is not `[V]` (exact) or `V` (minimum).

- [x] **Step 1: Failing tests** in `LibraryPackTests`, over nupkgs built in a temp dir with `zipfile`: `test_models_dependency_must_be_exact` (`version="0.0.0-local"` raises "exact", `version="[0.0.0-local]"` passes); `test_minimum_dependency_for_extensions` (`[V]` and `V` both accepted for DI, a different version raises); `test_library_must_carry_its_assembly` (no dll raises "lib/net10.0"); `test_library_must_not_carry_the_facade` (raises "EmbedPDF.Viewer.dll"); `test_dependency_range_reads_nuspec` (returns `[1.2.3]`, `None` for an absent id).
- [x] **Step 2:** `cd platforms/dotnet/packaging && python3 -m unittest -v`. Expected: new tests FAIL (no module), the 30 old ones PASS.
- [x] **Step 3:** Implement `libraries.py` and the Models target.
- [x] **Step 4:** Unit tests PASS; a real `pack-libraries` against a facade from `pack-facade` writes three nupkgs and `unzip -p EmbedPDF.Models.*.nupkg EmbedPDF.Models.nuspec` shows `version="[0.0.0-local]"`.
- [x] **Step 5:** Commit `feat(dotnet): pack EmbedPDF.Models, the DI extension and the Jint engine`.

### Task 2: PNG check helper

**Files:**
- Create: `platforms/dotnet/packaging/png_check.py`
- Modify: `platforms/dotnet/packaging/test_package.py` (class `PngCheckTests`)

**Interfaces:**
- `png_check.py <file> --width W --height H`: `check_png(data: bytes, width, height) -> None` raises `ValueError` unless the signature is right, every chunk's CRC-32 matches, IHDR is `W x H`, bit depth 8, colour type 6 (RGBA), and the concatenated IDAT inflates to `H * (1 + 4W)` bytes with filter bytes in 0..4. Prints `PNG ok: WxH` and exits 0, else `ERROR:` and exit 1.

- [x] **Step 1: Failing tests:** a 2x1 PNG built in the test with `zlib` and `zlib.crc32` passes; a flipped CRC byte raises "CRC"; a wrong height raises "IHDR"; a truncated IDAT raises "size".
- [x] **Step 2:** Run. Expected: FAIL (no module).
- [x] **Step 3:** Implement.
- [x] **Step 4:** Run. Expected: PASS.
- [x] **Step 5:** Commit `test(dotnet): add a PNG structure check for the console sample`.

### Task 3: Console sample

**Files:**
- Create: `platforms/dotnet/samples/Directory.Build.props` (stops the repo props and sets `TargetFramework`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors` itself, as a consumer's project would), `platforms/dotnet/samples/EmbedPDF.Sample.Console/EmbedPDF.Sample.Console.csproj`, `Program.cs`, `Png.cs`.

**Interfaces:**
- csproj: `OutputType Exe`, `PackageReference` `EmbedPDF`, `EmbedPDF.Models`, `EmbedPDF.Extensions.DependencyInjection`, `EmbedPDF.Scripting.Jint` at `$(EmbedPdfVersion)` (default `0.0.0-local`), `Microsoft.Extensions.DependencyInjection` 10.0.0, trim and AOT analyzers on, `InvariantGlobalization`, `WarningsNotAsErrors` `IL2026;IL2104;IL3053`.
- `static class Png { static void Write(Stream output, PixelBuffer image) }`: straight-alpha packed RGBA, filter byte 0 per row, one IDAT through `ZLibStream`, CRC-32 table computed once.
- `Program`: `EmbedPDF.Sample.Console <input.pdf> <output-dir> [--no-script]`. Steps: `di` (a `ServiceCollection` with `AddEmbedPdf(b => ...)` installing search, render, annotation, history and actions with `JintScriptEngineFactory`, resolves `PdfViewer`, then `StartAsync`), `open` (`OpenFileAsync`, page count > 0), `search` (`SearchAsync(new SearchQuery("the"))`, hits > 0), `render` (page 1 at scale 1, `page-1.png` written), `annotate` (`SquareAnnotationDraft` on page 1, then `ListAsync` contains it), `save` (`SaveAsync`, reopen the bytes, the square is listed), `model` (`new SearchModel(search)`, `Text = "the"`, `SubmitSearchAsync`, `NextHitAsync`, prints `Summary`, requires `ActiveHit`), `script` (`ScriptEngineCheck.RunAsync(new JintScriptEngineFactory(), "app.alert('sample')")` alerts `["sample"]`, no error). Usage error exits 64.

- [x] **Step 1:** Write the project; build it in a copy against Task 1's feed. Expected: 0 warnings, every step `PASS`, `png_check.py` accepts `page-1.png`.
- [x] **Step 2:** Commit `feat(dotnet): add the console sample`.

### Task 4: `dotnet-consumer.sh`

**Files:**
- Create: `platforms/dotnet/dotnet-consumer.sh`, `platforms/dotnet/dotnet-consumer.test.sh`

**Interfaces:**
- `dotnet-consumer.sh [--rid RID] [--packages DIR] [--no-aot] [--work DIR] [--keep]`. RID defaults to the host's; `--work` defaults to a `mktemp -d` removed on exit unless `--keep`. Exit 64 bad arguments or RID, 66 a missing `--packages` dir or a missing nupkg in it, 1 any failed step.
- Steps: build or locate packages; copy the sample, `global.json` and `samples/Directory.Build.props` into `WORK/src`; write `nuget.config` (`clear`, `local` = feed, `nuget.org`, `packageSourceMapping`: `EmbedPDF*` on `local`, `*` on `nuget.org`); scrub `SCRUBBED_ENV`; private `NUGET_PACKAGES`; framework-dependent: `build -c Release -o WORK/fdd`, run it on `report.pdf`, require all eight `PASS` lines, `png_check.py`; AOT: `publish -c Release -r RID -p:PublishAot=true -o WORK/aot`, keep the log, collect IL warnings, fail on any code outside the allow list or any allowed code naming an `EmbedPDF` assembly, run the binary the same way, print `SIZE aot-binary=<bytes>` and `SIZE aot-dir=<bytes>`, then delete `WORK/aot`. Final line `PASS: consumer rid=RID fdd aot` (or `fdd` alone).
- `dotnet-consumer.test.sh` (style of `generate-bindings.test.sh`): `--bogus` exits 64; `--rid bogus` exits 64; `--packages /nonexistent` exits 66; an empty `--packages` dir exits 66 naming `EmbedPDF.Runtime`.

- [x] **Step 1:** Write the test. **Step 2:** Run it: FAIL (no script).
- [x] **Step 3:** Implement. **Step 4:** Test PASS; then (guard first) `bash platforms/dotnet/dotnet-consumer.sh --rid linux-arm64`: every line PASS for FDD and AOT.
- [x] **Step 5:** `EPDF_DOTNET=/workspaces/dotnet-x64/dotnet bash platforms/dotnet/dotnet-consumer.sh --rid linux-x64 --packages <packages with a linux-x64 slice>` if the slice and the AOT cross link work under emulation; otherwise record why not.
- [x] **Step 6:** Commit `feat(dotnet): check the console sample from packed packages and as NativeAOT`.

### Task 5: Workflow, README, plan records

**Files:**
- Modify: `.github/workflows/release-dotnet.yml` (`runtime-pack` runs `libraries.py pack-libraries` and uploads the three nupkgs with the others; new `consumer` job, needs `[image, runtime-pack]`, rows `linux-arm64` and `linux-x64` in the Debian image with AOT, `osx-arm64` and `win-x64` with `--no-aot`, each running `dotnet-consumer.sh --packages dotnet-runtime`; nothing published), `platforms/dotnet/README.md` (section "Console sample and consumer check"), `docs/superpowers/plans/2026-11-20-dotnet-parity.md` (Phase 5 boxes, Phase 6 pack-check box, "Phase 5 notes").

- [x] **Step 1:** Workflow edit; `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/release-dotnet.yml'))"` passes.
- [x] **Step 2:** Run every suite: `python3 -m unittest discover -s platforms/dotnet/packaging`, `bash platforms/dotnet/dotnet-consumer.test.sh`, `bash platforms/dotnet/generate-bindings.test.sh`, `bash platforms/dotnet/dotnet-engine-hash.test.sh`, `bash platforms/dotnet/check-dotnet-engine.sh` (still current), Release build of `EmbedPDF.sln` with 0 warnings.
- [x] **Step 3:** README and plan notes (sample output, AOT sizes, warnings, deviations).
- [x] **Step 4:** Commits `ci(dotnet): run the consumer check in release-dotnet` and `docs(dotnet): record Phase 5`.
