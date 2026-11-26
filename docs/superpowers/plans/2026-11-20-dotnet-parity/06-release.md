# Phase 6: Release and Documentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** A release path that packs the five viewer packages at a release version and, after the train's one approval, pushes them to the GitHub Packages NuGet feed of the release repository and tags a prerelease; a README a consumer can start from; and the plan's Definition of done checked with evidence.

**Architecture:** `release-dotnet.yml` stays a build workflow that never publishes. It gains a `dotnet_version` input that versions the four managed packages (`EmbedPDF.Runtime` keeps `0.0.0-engine.<hash>`), stamps every package with the repository URL, and, on a manual run, a `viewer-release-preview` job that runs the release script with `--dry-run`. The release script, `platforms/dotnet/packaging/release-viewer.sh`, checks the five packages, pushes them in dependency order with `dotnet nuget push --skip-duplicate` and makes the GitHub prerelease through `scripts/release/github-release.sh`. `release-train.yml` gains an optional `dotnet_version` input and a `dotnet-viewer-release` job behind `approve`, and `check-release-gate.py` learns that a NuGet push publishes.

**Tech Stack:** GitHub Actions, bash, Python 3 `unittest`, .NET SDK 10.0.404 (the merged plugins branch moved `global.json`), Rust 1.100.0 (`rust-toolchain.toml`), xUnit 2.9.3.

**Spec:** `docs/superpowers/plans/2026-11-20-dotnet-parity.md`: Global Constraints, D2, D3, D7, D8, the Phase 1 and Phase 5 notes, Phase 6 and Definition of done.

## Global Constraints

- Dev container `embedpdf-dotnet-parity`, worktree `/workspaces/wt-p6`, branch `research/dotnet-parity-p6` from `647e0e846` with the plugins branch merged in. `CARGO_TARGET_DIR=/workspaces/embed-pdf-viewer/target`. Guard script before heavy builds and between tasks.
- Commits by explicit path, `feat(dotnet)`, `fix(dotnet)`, `test(dotnet)`, `docs(dotnet)`, `ci(dotnet)`, no trailers. Push only `research/dotnet-parity-p6` to `origin`; CI runs one at a time.
- Never publish. Every publish step sits behind `approve` in the train, and every run in this phase is a dry run.
- No TypeScript, `packages/**` or Rust behaviour change. `platforms/dotnet/EmbedPDF` (`EmbedPDF.Engine`) untouched except README cross-references.
- `package.py` is an engine hash input: every edit moves `0.0.0-engine.<hash>`. Re-pin `EmbedPDF.Runtime.props` once, after the last `package.py` edit.
- Repo prose names no outside product, company or person; README prose follows the house voice (no em dashes, exact values). The existing `EmbedPDF.Engine` text stays as it is.

## Decisions this plan makes

- **Version:** `dotnet_version` is `0.0.0-dotnet.<n>`, matching `0.0.0-swift.<n>`. It is optional on the train: empty skips the viewer release and packs at `0.0.0-local` as today, so existing dispatches keep working.
- **Feed:** `https://nuget.pkg.github.com/<owner>/index.json`, the owner taken from `GH_REPO` (the train's `github.repository`, where its releases already go). Packages carry `RepositoryUrl=https://github.com/<owner>/<repo>` so the feed links them to that repository. The job's `GITHUB_TOKEN` gets `packages: write`.
- **Tag:** the GitHub prerelease is tagged `<dotnet_version>` at the dispatched commit, notes `docs/releases/<dotnet_version>.md`, assets the five nupkgs, their `.sha256` files and the merged runtime manifest.
- **Re-runs:** `--skip-duplicate` lets a re-run pass over packages already pushed, and `github-release.sh` skips a complete release, as every other train job does.
- **Restore:** `smoke-runtime` writes a `nuget.config` beside its consumer with `packageSourceMapping`: `EmbedPDF` and `EmbedPDF.*` from the local feed only, everything else from nuget.org.
- **Toolchain checks:** `package.py` reads the required SDK from `platforms/dotnet/global.json` and the Rust version from `rust-toolchain.toml` instead of literals, since the merge left `Rust 1.98.0 required` in place against a 1.100.0 toolchain.

## Review Focus

1. A consumer restore that finds `EmbedPDF` on nuget.org tests the wrong bytes. Pinned by Task 1's mapping test: `EmbedPDF` and `EmbedPDF.*` map to the local source only.
2. A push of `EmbedPDF` before `EmbedPDF.Runtime` leaves a package on the feed whose dependency is missing. Pinned by Task 2's order assertion.
3. A dry run that leaks the token into the log. Pinned by Task 2: the printed command shows `***`, and the test greps for the token.
4. A release job that publishes without approval. Pinned by Task 3's gate tests: removing `approve` from the new job's needs fails the gate, and a preview line with `--dry-run` does not count as publishing.
5. A README plugin table that drifts from `PdfViewerOptions`. Pinned by Task 4's table test.

---

### Task 1: Consumer restore and toolchain checks in `package.py`

**Files:** Modify `platforms/dotnet/packaging/package.py`, `platforms/dotnet/packaging/test_package.py`, `.github/workflows/release-dotnet.yml` (drop both `RestoreAdditionalProjectSources` env blocks).

**Interfaces:**
- `consumer_nuget_config(feed: Path) -> str`: the XML described under Decisions; `smoke_runtime` writes it to `<work>/nuget.config` and restores with `--configfile` instead of `--source`.
- `required_sdk() -> str` from `global.json`'s `sdk.version`; `required_rust() -> str` from `rust-toolchain.toml`'s `channel`. `check_toolchain` and `stage_runtime` use them.

- [x] **Step 1: Failing tests** `ConsumerConfigTests`: the config clears sources, has `local` = feed path and `nuget.org`; `EmbedPDF` and `EmbedPDF.*` appear only under `local`; `*` only under `nuget.org`. `ToolchainTests`: `required_sdk()` equals the json's version and `required_rust()` the toml's channel.
- [x] **Step 2:** `cd platforms/dotnet/packaging && python3 -m unittest -v`. Expected: the new tests fail (no attribute).
- [x] **Step 3:** Implement, and remove the workflow env workaround.
- [x] **Step 4:** Unit tests pass. `validate-runtime` on `linux-arm64` without `RestoreAdditionalProjectSources` passes (it failed with NU1101 before).
- [x] **Step 5:** Commit `fix(dotnet): restore the runtime consumer's other dependencies from nuget.org`.

### Task 2: Release script

**Files:** Create `platforms/dotnet/packaging/release-viewer.sh`, `platforms/dotnet/packaging/release-viewer.test.sh`.

**Interfaces:**
- `release-viewer.sh <packages-dir> <version> <sha> [--dry-run]`, env `GH_REPO`, `GH_TOKEN`, `DRY_RUN` (`--dry-run` sets it), `EPDF_DOTNET` (default `dotnet`). Requires `<version>` to match `^0\.0\.0-dotnet\.[0-9]+$`, exactly one `EmbedPDF.Runtime.0.0.0-engine.<16 hex>.nupkg` and the four managed packages at `<version>`, `EmbedPDF.Models` depending on `[<version>]`, every nuspec's repository URL `https://github.com/$GH_REPO`, and `package-manifest.runtime.json`. Writes `<nupkg>.sha256`, pushes Runtime, EmbedPDF, Models, DI, Jint in that order, then `github-release.sh publish <version> <sha> "C# viewer <version>" docs/releases/<version>.md <assets>`. Dry run prints `dry run: would run dotnet nuget push ... --api-key *** --skip-duplicate` per package.

- [x] **Step 1: Failing tests** in `release-viewer.test.sh`, with stand-in `dotnet` and `gh` on `PATH` and nupkgs built with `python3 -c zipfile`: dry run prints five pushes in order and never the token; a bad version exits 64; a missing package exits 66; a `>=` Models range fails; a missing repository URL fails; a real run calls the stand-in `dotnet nuget push` five times.
- [x] **Step 2:** Run `bash platforms/dotnet/packaging/release-viewer.test.sh`. Expected: fail (no script).
- [x] **Step 3:** Implement.
- [x] **Step 4:** Tests pass.
- [x] **Step 5:** Commit `feat(dotnet): add the viewer package release script`.

### Task 3: Workflows and the release gate

**Files:** Modify `.github/workflows/release-dotnet.yml`, `.github/workflows/release-train.yml`, `scripts/release/check-release-gate.py`, `scripts/release/check-release-gate.test.py`, `docs/releases/README.md`.

- `release-dotnet.yml`: inputs `dotnet_version` (dispatch and call, default `''`) and `preview` (dispatch only, boolean, default `true`). `runtime-pack` packs the facade and libraries at `${dotnet_version:-0.0.0-local}` with `RepositoryUrl` set, and the later jobs read the facade's name instead of `0.0.0-local`. New job `viewer-release-preview` (needs `runtime-pack`, `runtime-record`; `if: inputs.preview`) runs the release script with `--dry-run`. The viewer jobs move to SDK 10.0.404 and the action versions the merge brought to the engine jobs.
- `release-train.yml`: input `dotnet_version`; preflight checks its shape, its notes (soft on a dry run) and its release state; summarize lists the release; job `dotnet-viewer-release` needs `[approve, preflight, dotnet]`, `if: inputs.dotnet_version != ''`, `permissions: {contents: write, packages: write}`.
- Gate: `PUBLISHES` gains `dotnet nuget push` and `release-viewer\.sh` on a line without `--dry-run`; `packages: write` counts as writing.

- [x] **Step 1: Failing gate tests:** the train passes with `dotnet-viewer-release` among the publishers; removing its `approve` fails; a preview line with `--dry-run` in `release-dotnet.yml` does not count; a job with `packages: write` and no approve fails.
- [x] **Step 2:** `python3 scripts/release/check-release-gate.test.py`. Expected: the new tests fail.
- [x] **Step 3:** Implement the gate and workflow changes.
- [x] **Step 4:** Gate tests pass; `check-release-gate.py .github/workflows/release-train.yml` exits 0; `actionlint` if present.
- [x] **Step 5:** Commit `ci(dotnet): pack the viewer packages at a release version and push them after approval`.

### Task 4: README and the plugin-table test

**Files:** Modify `platforms/dotnet/README.md`, `platforms/dotnet/EmbedPDF.Viewer.Tests/DisabledPluginTests.cs`.

- README, in the shape of `platforms/apple/README.md`, after the untouched `EmbedPDF.Engine` sections: packages and what each is for; where the native library comes from (engine hash, local engine mode); the plugin table (options member, accessor, platform services); `EmbedPDF.Engine` or the viewer (D2); threading and disposal; binding the models (kept); console sample (kept); releases and feed; known gaps.
- `TheReadmePluginTableHasARowPerPlugin`: parses the table under `## Plugins` (first column the options member in backticks, second the accessor) and asserts the members equal `Accessors`' fields and every accessor is a public `PdfViewer` method.

- [x] **Step 1:** Write the test; run it. Expected: FAIL (no section).
- [x] **Step 2:** Write the README.
- [x] **Step 3:** Test passes, and `ReadmeExampleTests` still passes.
- [x] **Step 4:** Commit `docs(dotnet): document the viewer packages, plugins and release`.

### Task 5: Re-pin, local verification, CI and records

- [x] Re-pin `EmbedPDF.Runtime.props` to `dotnet-engine-hash.sh`; `check-dotnet-engine.sh` exits 1 (unpublished) with the pin equal to `current`.
- [x] Rebuild with Rust 1.100.0; regenerate bindings; Release build of `EmbedPDF.sln` (0 warnings); `EmbedPDF.Viewer.Tests`, `EmbedPDF.Models.Tests`, packaging tests, shell tests, coverage check; `dotnet-consumer.sh` on `linux-arm64`.
- [x] Push; dispatch `release-dotnet.yml` with `dotnet_version=0.0.0-dotnet.0` (preview on); fix until green.
- [x] Replace the Phase 1 runtime records with the run's records under `platforms/dotnet/validation/2026-11-26-phase-6-runtime-<rid>/`.
- [x] Umbrella: Phase 6 boxes, Phase 6 notes (merge, re-pin, runs, Swift-to-C# test mapping), Definition of done with evidence.
