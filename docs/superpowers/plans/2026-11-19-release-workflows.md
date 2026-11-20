# Release Workflows Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One `workflow_dispatch` run on the MFB viewer fork builds, tests and publishes the six Rust-port releases (`runtime-<sha>`, `runtime-payloads-<hash>`, `apple-engine-<hash>`, `0.0.0-swift.N`, `rust-viewer-0.0.0-preview.N`, `dotnet-engine-0.0.0-local-<sha12>`) on GitHub-hosted runners, with every independent piece running in parallel and every slow, deterministic piece cached.

**Architecture:** A `release-train.yml` orchestrator calls reusable workflows. Fork PDFium slices are built once per target in a parallel matrix and handed to consumers as artifacts, cached by runtime-src commit and recipe. The payload and Apple jobs feed those slices to `scripts/publish-engine-payload.mjs` through the stamp files `buildFork` already reuses, so the publish script stays the single place that decides hashes, notes and skips. A pin job commits the two `Package.swift` bumps; the Swift, viewer bundle and .NET releases are tagged at that pin commit.

**Tech Stack:** GitHub Actions (hosted `ubuntu-24.04`, `ubuntu-24.04-arm`, `xcode-27` preview), Node 24 + pnpm 10.34.0, Rust 1.98.0, emsdk 3.1.72 and 4.0.0, Xcode 27.2, .NET SDK 10.0.403, `gh`, `actions/cache@v5`, `actions/upload-artifact@v4`, `Swatinem/rust-cache@v2`, `actions/create-github-app-token@v2`.

**Spec:** the "Release workflows for the MFB embed-pdf forks" doc (https://claude.ai/artifact/T1TP5vPGrWpF4uxsv684Sd) plus the current-process map in this plan's Background section.

## Global Constraints

- Remotes: `fork` = `MFB-Technologies-Inc/embed-pdf-viewer` / `MFB-Technologies-Inc/embed-pdf-runtime`, `roanutil` = the retired repos, `origin` = upstream. Never push to `origin` or `roanutil`.
- Every release is a prerelease. Every release this train makes goes to `MFB-Technologies-Inc/embed-pdf-viewer`, the repo it runs in, and is published with the job's `GITHUB_TOKEN`.
- Runners: hosted only. `xcode-27` (preview) for macOS jobs, with `DEVELOPER_DIR=/Applications/Xcode_27.2.app/Contents/Developer`. No self-hosted runner.
- Toolchain pins: Rust `1.98.0` (`rust-toolchain.toml`), pnpm `10.34.0` (`package.json` `packageManager`), Node `24`, .NET `10.0.403` (`platforms/dotnet/global.json`), emsdk `3.1.72` (wasm32) and `4.0.0` (wasm32-eh), wasm-bindgen-cli `0.2.127`, binaryen `133`.
- `EMBEDPDF_NM` = the Rust 1.98.0 toolchain's `llvm-nm` on every Apple job (Xcode 27.2's `nm` can't read Rust 1.98 bitcode).
- No TypeScript behavior changes under `packages/**` (`packages/core-wasm` excepted). Scripts under `scripts/`, `platforms/` and `.github/` may change.
- Commits: no Claude footer, no session URL, no Co-Authored-By (embed-pdf repos rule).
- Repo prose (README, notes) is neutral: no names of other projects or people.
- Concurrency: one release train at a time (`concurrency: release-train`, `cancel-in-progress: false`).
- Jobs that publish declare `environment: release`, which has a required reviewer, and `permissions: contents: write`.

## Review Focus

1. A rerun after a partial failure (runtime published, Apple failed). Expected: published releases are detected and skipped, not recreated or failed on "tag exists". Task 2 and Task 9 tests pin it.
2. A cache entry whose key matches but whose archive is for another commit or target. Expected: the stamp check rejects it and the slice rebuilds. Task 2 test `rejects a staging dir from another commit`.
3. `dry_run: true`. Expected: every build and test job runs, nothing is published, tagged or pushed. Task 9 verification runs it.
4. The pin job racing a human push to the branch. Expected: the push fails fast on non-fast-forward and the train stops before any tag is made. Task 9 step.
5. Upstream's own workflows firing on the fork (it already happened: `Build PDF Runtime` failed on the 2026-11-19 20:09 push). Expected: none of upstream's 16 workflows run on the MFB forks. Task 10.

---

## Background: how each release is made today

| Release | Today | What CI must add |
|---|---|---|
| `runtime-<sha>` | `debian:trixie` amd64 container; fork `build-target.sh` + `package-target.sh` for linux-arm64, android-arm64, android-x64, wasm32 (emsdk 3.1.72), wasm32-eh (emsdk 4.0.0); hand notes | matrix build, `write-manifest.sh`, `gh release create` to the runtime fork |
| `runtime-payloads-<hash>` | `publish-engine-payload.mjs --only wasm` with a hand-written wasm32-eh stamp | stamp from the slice artifact; `RELEASE_REPO` override |
| `apple-engine-<hash>` | `publish-engine-payload.mjs --only apple` (buildFork x4, xcframework, 3 test runs, publish, pins nested manifest only) | stage split so tests run as parallel jobs; root manifest pin |
| `0.0.0-swift.N` | tag at pin commit, hand notes, no assets | tag + notes file |
| `rust-viewer-0.0.0-preview.N` | `pnpm --filter @mfb/embedpdf-rust-viewer build:all` then `pack:tarball` | toolchain install, payload fetch, notes file |
| `dotnet-engine-0.0.0-local-<sha12>` | `docs/research/upstream-catchup-2026-11-18/scripts/cu-c10/dotnet-release.sh` in the arm64 devcontainer | container job, generated `fonts.json`, guide file in repo |

## Job graph

```
t0 ─┬─ preflight (notes files, hashes, which releases already exist)
    ├─ slice matrix ×9 ─┬─ linux-arm64, android-arm64, android-x64, wasm32 ──┐
    │                   ├─ wasm32-eh ───────────────┬──────────────────────────┼─ runtime release
    │                   │                           └─ payload ─┐              │
    │                   └─ ios-arm64, ios-sim-arm64, darwin-static-arm64/x64 ─ apple build ─┬─ test iOS ─────┐
    │                                                                         ├─ test mac arm64 ┼─ apple publish ─ pin ─┬─ swift tag
    │                                                                         └─ test mac x64 ─┘                       ├─ viewer bundle release (needs payload)
    └─ dotnet build+validate ─────────────────────────────────────────────────────────────────────────────────────────┴─ dotnet release
```

Builds never wait on a publish they don't consume. The viewer bundle builds as soon as the payload exists; only its tag waits for the pin.

## Caching

| What | Key | Where |
|---|---|---|
| Fork slice (packaged staging dir + archive) | `fork-slice-v1-<target>-<runtime-src sha>-<FORK_RECIPE>-<hashFiles(target-args.sh, emsdk-version.sh, build-target.sh, package-target.sh, patches/**)>` | slice job; hit skips clone, sync and build |
| emsdk 3.1.72 / 4.0.0 | `emsdk-<version>-<os>-<arch>` | slice and payload jobs |
| depot_tools | `depot-tools-<os>-<arch>-<week>` | slice jobs |
| Cargo | `Swatinem/rust-cache@v2`, `shared-key` per job kind | payload, apple, viewer, dotnet |
| cargo-installed tools (`wasm-bindgen-cli 0.2.127`) | `taiki-e/install-action` with the pinned version | viewer |
| pnpm store | `actions/setup-node` `cache: pnpm` | payload, viewer |
| NuGet | `~/.nuget/packages` keyed on `hashFiles('platforms/dotnet/**/*.csproj', 'platforms/dotnet/global.json')` | dotnet |
| Published releases | preflight `gh release view` per tag | skips whole jobs |

The PDFium source tree (about 8 GB with Android deps) is not cached: it exceeds the 10 GB per-repo cache budget once two commits coexist. A slice cache hit makes it unnecessary.

---

### Task 1: Probe the hosted runners

**Files:**
- Create: `.github/workflows/release-probe.yml` (deleted in Task 11)

**Interfaces:**
- Produces: facts Tasks 4 and 7 rely on, recorded in the PR description: Xcode 27.2 path, Rosetta availability, free disk and cores per runner.

- [ ] **Step 1: Write the probe**

`workflow_dispatch` only. Three jobs:
- `xcode-27`: `xcodebuild -version` under `DEVELOPER_DIR=/Applications/Xcode_27.2.app/Contents/Developer`; `/usr/bin/pgrep oahd || softwareupdate --install-rosetta --agree-to-license`; `arch -x86_64 /usr/bin/true`; `xcrun simctl list devices available iPhone -j | jq '[.devices[][]|.name]'`; `df -h /`; `sysctl -n hw.ncpu hw.memsize`.
- `ubuntu-24.04` and `ubuntu-24.04-arm`: `df -h /`, `nproc`, `free -g`.

- [ ] **Step 2: Lint**

Run: `actionlint .github/workflows/release-probe.yml`
Expected: no output.

- [ ] **Step 3: Commit, push to `fork`, dispatch**

```bash
git add .github/workflows/release-probe.yml
git commit -m "ci: probe hosted runners for the release train"
git push fork HEAD
gh workflow run release-probe.yml -R MFB-Technologies-Inc/embed-pdf-viewer --ref <branch>
```
Expected: all three jobs green. If Rosetta can't be installed, stop and raise it: the x86_64 test job in Task 7 needs a different plan.

### Task 2: Script support for CI-fed slices and repo overrides

**Files:**
- Create: `scripts/release/stamp-fork-slice.mjs`
- Create: `scripts/release/stamp-fork-slice.test.mjs`
- Create: `scripts/release/build-fork-slice.mjs`
- Modify: `scripts/publish-engine-payload.mjs:40-44` (stamp template gains `"target"`), `:111` (`RELEASE_REPO`), apple half (`--stage`), `pinEngine` (root manifest)
- Modify: `platforms/apple/check-apple-engine.sh:24`
- Test: `scripts/publish-engine-payload.test.mjs` (create if absent; `node --test`)

**Interfaces:**
- Produces:
  - `node scripts/release/build-fork-slice.mjs <target> <out-dir>`: calls `buildFork(repo, target)` for the five `FORK_TARGETS`, or the fork's `build-target.sh` + `package-target.sh` for `linux-arm64|android-arm64|android-x64|wasm32`; copies the staging dir to `<out-dir>/staging` and the archive to `<out-dir>/`. Exit 0 on success.
  - `node scripts/release/stamp-fork-slice.mjs <target> <staging-dir>`: writes `$PERF_WORK_DIR/fork/<runtime-src sha>/<target>.json` as `{recipe: FORK_RECIPE, sha, target, seeded: false, staging, libSha256}` after checking `staging/BUILD-METADATA.json` `sha` and `target` against the gitlink. Exit 65 on mismatch.
  - `RELEASE_REPO = process.env.EMBEDPDF_RELEASE_REPO ?? 'MFB-Technologies-Inc/embed-pdf-runtime'` and `RUNTIME_REPO="${EMBEDPDF_RUNTIME_REPO:-MFB-Technologies-Inc/embed-pdf-runtime}"` in `check-apple-engine.sh`. The MFB defaults landed in `fce494ae8`; this adds only the overrides (`fetch-payload.sh` already has `EPDF_PAYLOAD_REPO`).
  - `publish-engine-payload.mjs --only apple --stage build`: everything up to and including `package-engine-framework.sh`, then exits leaving `platforms/apple/.build/publish/EmbedPDFBindings.xcframework.zip`. `--stage publish`: requires that zip, skips build and tests, creates the release, pins. No `--stage`: unchanged behavior.
  - `pinEngine` also rewrites the root `Package.swift` `url:` and `checksum:` lines of the `EmbedPDFBindings` binary target. The root URL's tag segment changes; its host already names `MFB-Technologies-Inc/embed-pdf-runtime` since `fce494ae8`.
- Consumes: `buildFork`, `forkStampMatches`, `FORK_RECIPE` from `perf-compare/scripts/build-arm.mjs` (export `FORK_RECIPE` and `FORK_TARGETS`).

- [ ] **Step 1: Write the failing tests**

In `scripts/release/stamp-fork-slice.test.mjs`, with a temp git repo whose `packages/engine/runtime/runtime-src` gitlink is `aaaa…` and a fake staging dir:
- `writes a stamp buildFork accepts`: after the CLI runs, `forkStampMatches(stamp, {recipe: FORK_RECIPE, sha, target: 'wasm32-eh'})` is `true`.
- `rejects a staging dir from another commit`: BUILD-METADATA `sha: 'bbbb…'` → exit code 65, no stamp file written.
- `rejects a staging dir for another target`: BUILD-METADATA `target: 'ios-arm64'` with CLI target `wasm32-eh` → exit 65.

In `scripts/publish-engine-payload.test.mjs`:
- `pins both manifests`: given copies of the two `Package.swift` files, `pinEngine('apple-engine-0123456789abcdef', 'c'.repeat(64))` leaves nested `engineRelease`/`engineChecksum` and root `url`/`checksum` all naming the new tag and checksum, and nothing else in either file changes (4 changed lines: nested release and checksum, root url and checksum).
- `honors EMBEDPDF_RELEASE_REPO`: with the env set to `x/y`, the printed dry-run release line names `--repo x/y`.

- [ ] **Step 2: Run them, expect failure**

Run: `node --test scripts/release/ scripts/publish-engine-payload.test.mjs`
Expected: FAIL (modules missing, `pinEngine` not exported).

- [ ] **Step 3: Implement**

Export `pinEngine` and make it take manifest paths (defaulting to the two real ones) so the test can point it at copies. Keep `--stage` parsing next to `--only`; `--stage` without `--only apple` is a usage error (exit 64).

- [ ] **Step 4: Run them, expect pass**

Run: `node --test scripts/release/ scripts/publish-engine-payload.test.mjs`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/release perf-compare/scripts/build-arm.mjs scripts/publish-engine-payload.mjs scripts/publish-engine-payload.test.mjs platforms/apple/check-apple-engine.sh
git commit -m "build(release): feed CI-built fork slices to the publish script"
```

### Task 3: Shared setup actions

**Files:**
- Create: `.github/actions/setup-rust/action.yml` (inputs: `targets`, `cache-key`; installs from `rust-toolchain.toml`, `rustup component add llvm-tools`, exports `EMBEDPDF_NM` on macOS, runs `Swatinem/rust-cache@v2` with `shared-key: ${{ inputs.cache-key }}`, then `cargo fetch --locked`)
- Create: `.github/actions/setup-node-pnpm/action.yml` (`pnpm/action-setup` with the `packageManager` version, `actions/setup-node` Node 24 `cache: pnpm`, `pnpm install --frozen-lockfile`)
- Create: `.github/actions/setup-emsdk/action.yml` (input `version`, `dir`; `actions/cache@v5` on `dir` keyed `emsdk-<version>-<runner.os>-<runner.arch>`; install + activate on miss; `embuilder build sysroot` when `version == 4.0.0`)
- Create: `.github/actions/setup-xcode-27/action.yml` (sets `DEVELOPER_DIR`, installs Rosetta if Task 1 showed it missing, picks an iPhone simulator id into `SIM_ID`)

**Interfaces:**
- Produces: four composite actions used by Tasks 4-8 by path (`./.github/actions/<name>`).

- [ ] **Step 1: Write the four actions** as specified above.
- [ ] **Step 2: Lint**

Run: `actionlint` (lints composite actions referenced from workflows; run after Task 4 too).
Expected: no output.

- [ ] **Step 3: Commit**

```bash
git add .github/actions
git commit -m "ci: shared setup actions for the release train"
```

### Task 4: Fork slice workflow

**Files:**
- Create: `.github/workflows/release-fork-slices.yml` (`on: workflow_call`, output `runtime_sha`)

**Interfaces:**
- Consumes: `scripts/release/build-fork-slice.mjs` (Task 2), setup actions (Task 3).
- Produces: one artifact per target named `fork-slice-<target>` holding `staging/` and `libembedpdf-runtime-<target>-<sha7>.tar.gz`. Output `runtime_sha` = the runtime-src gitlink.

- [ ] **Step 1: Write the workflow**

One job, `strategy.fail-fast: false`, matrix:

| target | runs-on | container |
|---|---|---|
| linux-arm64, android-arm64, android-x64, wasm32, wasm32-eh | `ubuntu-24.04` | `debian:trixie` (apt list from `embed-pdf-runtime-builds/c1116-17d8073d4/build-all.sh:19-54`) |
| ios-arm64, ios-sim-arm64, darwin-static-arm64, darwin-static-x64 | `xcode-27` | none |

Steps: checkout with `submodules: true` and `fetch-depth: 0`; restore the slice cache (key from the Caching table); on miss: free disk on Linux (remove `/usr/share/dotnet`, `/opt/ghc`, `/usr/local/lib/android` from the host via a pre-container step), setup-emsdk for wasm targets (3.1.72 for wasm32, 4.0.0 for wasm32-eh, into `runtime-src/third_party/emsdk`), restore depot_tools, run `build-fork-slice.mjs`, save cache; upload artifact. `timeout-minutes: 300`.

- [ ] **Step 2: Lint**

Run: `actionlint .github/workflows/release-fork-slices.yml`
Expected: no output.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/release-fork-slices.yml
git commit -m "ci: build fork PDFium slices in parallel with a per-commit cache"
```

### Task 5: Runtime and payload release workflows

**Files:**
- Create: `.github/workflows/release-runtime.yml` (`workflow_call`, inputs `runtime_sha`, `dry_run`)
- Create: `.github/workflows/release-payload.yml` (`workflow_call`, input `dry_run`)

**Interfaces:**
- Consumes: artifacts `fork-slice-{linux-arm64,android-arm64,android-x64,wasm32,wasm32-eh}`; `stamp-fork-slice.mjs`; secret `RELEASE_APP_ID`/`RELEASE_APP_KEY`.
- Produces: releases `runtime-<sha>` and `runtime-payloads-<hash>` on `MFB-Technologies-Inc/embed-pdf-runtime`; output `payload_tag`.

- [ ] **Step 1: release-runtime.yml**

`ubuntu-24.04`, `environment: release`. Skip if `gh release view runtime-<sha>` succeeds. Otherwise download the five artifacts; in a runtime-src checkout at `<sha>`, run `check-upstream-freshness.py --json upstream.json --notes notes.md` (no `--enforce`), `PDF_RUNTIME_RELEASE_BASE_URL=https://github.com/MFB-Technologies-Inc/embed-pdf-runtime/releases/download/runtime-<sha> PDF_RUNTIME_UPSTREAM_FILE=upstream.json write-manifest.sh`; `gh release create runtime-<sha> <archives> pdf-runtime-build.generated.json --repo MFB-Technologies-Inc/embed-pdf-runtime --target <sha> --title "EmbedPDF PDF Runtime <sha>" --notes-file notes.md --prerelease`. `dry_run` prints the command instead.

- [ ] **Step 2: release-payload.yml**

`ubuntu-24.04`. Download `fork-slice-wasm32-eh`; setup-node-pnpm, setup-rust (`wasm32-unknown-emscripten`), setup-emsdk 4.0.0 into `$PERF_WORK_DIR/emsdk`, `apt-get install libclang-dev`; `stamp-fork-slice.mjs wasm32-eh <staging>`; `EMBEDPDF_RELEASE_REPO=MFB-Technologies-Inc/embed-pdf-runtime node scripts/publish-engine-payload.mjs --only wasm` (`--dry-run` when `dry_run`); output the tag from `runtime-payload-hash.sh`. Publishing step under `environment: release` with the App token as `GH_TOKEN`.

- [ ] **Step 3: Lint** `actionlint` on both; expected no output.
- [ ] **Step 4: Commit**

```bash
git add .github/workflows/release-runtime.yml .github/workflows/release-payload.yml
git commit -m "ci: publish the fork runtime and wasm payload from slice artifacts"
```

### Task 6: .NET engine workflow

**Files:**
- Create: `.github/workflows/release-dotnet.yml` (`workflow_call`, inputs `dry_run`)
- Create: `platforms/dotnet/packaging/ci-fonts.sh` (writes the DejaVu `fonts.json` the 2026-11-18 release used: `/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf`, notice `/usr/share/doc/fonts-dejavu-core/copyright`, key `parity-dejavu`, with sha256s computed at run time)
- Create: `platforms/dotnet/EmbedPDF-CSharp-Existing-App.md` (copied verbatim from the `dotnet-engine-0.0.0-local-f37adb44799d` release asset)

**Interfaces:**
- Produces: artifact `dotnet-release` with the five assets named as in the 2026-11-18 release.

- [ ] **Step 1: Write the workflow**

`ubuntu-24.04-arm`, `container: mcr.microsoft.com/dotnet/sdk:10.0.403-trixie-arm64v8` (verify the tag exists; fall back to `debian:trixie` + `dotnet-install.sh --version 10.0.403`). `EPDF_IMAGE_ID` = the container image digest. NuGet cache; setup-rust; `fetch-libpdfium.sh linux-arm64` (pinned upstream archive). Then the steps of `dotnet-release.sh:12-93` in order, renaming outputs to the release asset names. Starts at t0: it depends on nothing else in the train.

- [ ] **Step 2: Lint** `actionlint`; expected no output.
- [ ] **Step 3: Commit**

```bash
git add .github/workflows/release-dotnet.yml platforms/dotnet/packaging/ci-fonts.sh platforms/dotnet/EmbedPDF-CSharp-Existing-App.md
git commit -m "ci: build and validate the .NET engine package"
```

### Task 7: Apple engine workflow

**Files:**
- Create: `.github/workflows/release-apple.yml` (`workflow_call`, input `dry_run`, output `apple_tag`, `checksum`)
- Modify: `.github/workflows/apple-swift.yml` (switch to `xcode-27` + `setup-xcode-27`; it stays the manual check)

**Interfaces:**
- Consumes: artifacts `fork-slice-{ios-arm64,ios-sim-arm64,darwin-static-arm64,darwin-static-x64}`; `--stage build|publish` (Task 2).
- Produces: release `apple-engine-<hash>`; outputs `apple_tag`, `checksum`; artifact `apple-manifests` with both pinned `Package.swift` files.

- [ ] **Step 1: Write the workflow**

Jobs, all `runs-on: xcode-27`:
1. `build`: skip all jobs if `check-apple-engine.sh` exits 0. Download the four slice artifacts, stamp each, setup-rust (four Apple targets + `cache-key: apple`), `CARGO_BUILD_JOBS=$(sysctl -n hw.ncpu)`, `publish-engine-payload.mjs --only apple --stage build`; upload the zip and the unpacked xcframework as artifact `apple-xcframework`.
2. `test-ios`, `test-mac-arm64`, `test-mac-x86_64` (`needs: build`, parallel): download `apple-xcframework`, then the matching test command from `publishApple` (`publish-engine-payload.mjs:911-940`) with `EMBEDPDF_PREBUILT_ENGINE=local`. Mac jobs stage the consumer with `macos-consumer.mjs`.
3. `publish` (`needs` all three tests, `environment: release`): `--stage publish`; upload both manifests as `apple-manifests`.

- [ ] **Step 2: Lint** `actionlint`; expected no output.
- [ ] **Step 3: Commit**

```bash
git add .github/workflows/release-apple.yml .github/workflows/apple-swift.yml
git commit -m "ci: build, test in parallel and publish the Apple engine"
```

### Task 8: Viewer bundle workflow

**Files:**
- Create: `.github/workflows/release-rust-viewer.yml` (`workflow_call`, inputs `version`, `ref`)

**Interfaces:**
- Consumes: the published `runtime-payloads-<hash>` (via `fetch:payload`, `EPDF_PAYLOAD_REPO=MFB-Technologies-Inc/embed-pdf-runtime`).
- Produces: artifact `rust-viewer-tarball` holding `mfb-embedpdf-rust-viewer-<version>.tgz`.

- [ ] **Step 1: Write the workflow**

`ubuntu-24.04`. Checkout `inputs.ref`; setup-node-pnpm; setup-rust (`wasm32-unknown-unknown`, `cache-key: viewer`); `taiki-e/install-action` for `wasm-bindgen-cli@0.2.127`; binaryen 133 from its GitHub release tarball, cached by version; `pnpm --filter @embedpdf/engine-runtime run fetch:payload`; `pnpm --filter @embedpdf/engine-runtime build`; `pnpm --filter @mfb/embedpdf-rust-viewer build:all`; `pnpm --filter @mfb/embedpdf-rust-viewer pack:tarball`; assert the tarball's `package.json` version equals `inputs.version`.

- [ ] **Step 2: Lint** `actionlint`; expected no output.
- [ ] **Step 3: Commit**

```bash
git add .github/workflows/release-rust-viewer.yml
git commit -m "ci: build the Rust viewer bundle tarball"
```

### Task 9: Release train orchestrator

**Files:**
- Create: `.github/workflows/release-train.yml`
- Create: `docs/releases/README.md` (one paragraph: notes files live here as `<tag>.md`, written before dispatch)

**Interfaces:**
- Consumes: Tasks 4-8.
- Inputs: `swift_version` (e.g. `0.0.0-swift.8`), `viewer_version` (e.g. `0.0.0-preview.4`), `dry_run` (boolean, default `true`).

- [ ] **Step 1: Write the orchestrator**

Jobs and `needs`:
- `preflight` (t0): verify `docs/releases/<swift tag>.md`, `docs/releases/rust-viewer-<viewer_version>.md`, `docs/releases/dotnet-engine.md` exist; verify `packages/rust-viewer-bundle/package.json` version equals `viewer_version`; compute `runtime_sha`, payload hash, apple hash; report which releases exist.
- `slices` → `release-fork-slices.yml` (t0)
- `dotnet` → `release-dotnet.yml` (t0)
- `runtime` (`needs: slices`), `payload` (`needs: slices`), `apple` (`needs: slices`)
- `viewer` (`needs: payload`) → `release-rust-viewer.yml` at `github.sha`
- `pin` (`needs: [apple, runtime]`, `environment: release`): download `apple-manifests`, commit both `Package.swift` files with message `build(apple): pin apple-engine-<hash>`, `git push fork HEAD:${{ github.ref_name }}` (no force; a non-fast-forward fails the job); output `pin_sha`. When the Apple release was skipped, `pin_sha = github.sha`.
- `swift-release`, `viewer-release` (`needs: [pin, viewer]`), `dotnet-release` (`needs: [pin, dotnet]`): `git tag <tag> <pin_sha>`, push the tag, `gh release create <tag> --verify-tag --prerelease --notes-file docs/releases/<file>` with the assets. The viewer tarball built at `github.sha`; the pin commit changes only the two manifests, so the tarball is the same bytes.

With `dry_run`, every publish, push and tag step prints its command and exits 0.

- [ ] **Step 2: Lint** `actionlint`; expected no output.
- [ ] **Step 3: Dry run on the fork**

```bash
git add .github/workflows/release-train.yml docs/releases/README.md
git commit -m "ci: release train orchestrator"
git push fork HEAD
gh workflow run release-train.yml -R MFB-Technologies-Inc/embed-pdf-viewer --ref <branch> -f swift_version=0.0.0-swift.8 -f viewer_version=0.0.0-preview.4 -f dry_run=true
```
Expected: all jobs green; no new release or tag (`gh release list -R MFB-Technologies-Inc/embed-pdf-runtime -L 3` unchanged). Record each job's wall time and the slice cache misses in the PR description.

- [ ] **Step 4: Rerun the dry run**

Expected: every slice job hits the cache and finishes in under 5 minutes.

### Task 10: Keep upstream's workflows off the forks

**Files:** none (repository settings).

- [ ] **Step 1: List what runs**

Run: `gh workflow list -R MFB-Technologies-Inc/embed-pdf-viewer` and the same for `embed-pdf-runtime`.

- [ ] **Step 2: Confirm with the user, then disable**

Disable every upstream workflow (all 16 viewer workflows except `apple-swift.yml`, and the runtime fork's three) with `gh workflow disable <name> -R <repo>`. Disabling, not editing, keeps upstream syncs conflict-free. Ask first: this changes repository settings.

Expected: `gh workflow list` shows them `disabled_manually`; a push to a research branch triggers nothing but the release workflows' own (none, they're dispatch-only).

### Task 11: First real release and cleanup

- [ ] **Step 1: Prerequisites the user owns**

A `release` environment on the viewer fork with a required reviewer. Notes files written under `docs/releases/`.

- [ ] **Step 2: Dispatch with `dry_run=false`**, approve the environment gate. Expected: six prereleases on the MFB repos, both manifests pinned to the new Apple engine in the pin commit, three tags at the pin commit.
- [ ] **Step 3: Verify consumers**

Run: `swift package resolve` in a scratch package depending on `.package(url: "https://github.com/MFB-Technologies-Inc/embed-pdf-viewer", exact: "<swift tag>")`; `EPDF_PAYLOAD_REPO` unset `fetch-payload.sh wasm32` in a clean checkout.
Expected: both succeed against the MFB repos.

- [ ] **Step 4: Remove the probe, update the docs**

Delete `.github/workflows/release-probe.yml`. Add a "Releasing" section to `platforms/apple/README.md` and `packages/rust-viewer-bundle/README.md` pointing at `release-train.yml`; Update the Claude doc's diagram to the job graph above.

```bash
git rm .github/workflows/release-probe.yml
git add platforms/apple/README.md packages/rust-viewer-bundle/README.md
git commit -m "docs: release through the release train"
```
