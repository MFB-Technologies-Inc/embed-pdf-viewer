# CU-C9: merge plugins' moved tip and prepare the release

`research/catchup-2026-11-27b-swift` merges plugins `7a4673b42` with no conflict, and every row
passes on the combined tree: `cargo test --workspace` 3,704 of 3,704, all 36 engine `:check`
scripts, all 4 plugin checks, `replay:check` over 16,076 cases, the evidence tests at the start's
769 of 771, `rust-coverage:check` once the record is regenerated, and 628 `EmbedPDFTests` on the iOS
simulator, macOS arm64 and macOS x86_64. The .NET and Android pins are current, the four release
notes are written and `packages/rust-viewer-bundle/package.json` is `0.0.0-preview.9`. Nothing was
pushed, fast-forwarded or dispatched. Decisions are in
[`../decisions/CU-C9.md`](../decisions/CU-C9.md).

## Commits

| Commit | Summary |
|---|---|
| `9d66eb489` | Merge plugins `7a4673b42` (CU-C9-D1) |
| `1c42006dc` | `setButtonImage` docs list `permission-denied` in `Form.kt`, `Form.swift` and `Form.cs` (the final review's Minor) |
| `37996bdbb` | .NET pin `0.0.0-engine.7dd0c2b9e60f92a1` |
| `513fcb0ce` | Android pin `android-engine-1a68bff677299ac0` |
| `d60328b76` | Notes for `0.0.0-swift.12`, `rust-viewer-0.0.0-preview.9`, `0.0.0-dotnet.3`, `0.0.0-android.4`; `package.json` to `0.0.0-preview.9` |
| `37c92d414` | `tooling/plugin-conformance/vectors/rust-coverage.json` regenerated (CU-C9-D8) |
| the commit that adds this file | Report, `decisions/CU-C9.md`, `decisions.md`, `scripts/cu-c9/` |

All on the host worktree `.claude/worktrees/catchup-1127b-swift`. The merge went into the container
as bundle `CU-C9a`, where `/workspaces/wt/CU-C2` now has `research/catchup-2026-11-27b-swift`
checked out.

## Merge

`git fetch fork research/multi-platform-rust-core-impl-plugins` gave `7a4673b42`, so the tip had not
moved again. The merge base is `e3d6e7fbc`. Plugins brought the issue-39 action reader
(`6b71b4243`, `crates/epdf-engine/src/features/actions/reader.rs`, `crates/pdfium/src/action.rs`),
the stage uniffi default (`08cce28cd`), two Swift test fixes (`866ad6fac`, `0fa20cd23`), a fixture
line in `platforms/apple/build-xcframework.sh`, the morning's three pins and release notes, and
`packages/rust-viewer-bundle/package.json` at `0.0.0-preview.8`. No file conflicted.

## Re-verification, start against now

Start is CU-C6's head (`0b55dab9f` and `f69b5f3c5`) for the container rows and CU-C8's head
(`663cccac6`) for Swift. Now is `9d66eb489` for the container rows (CU-C9-D2) and the tree after
`1c42006dc` for Swift.

| Check | Start | Now |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,698 passed, 0 failed, 2 ignored | 3,704 passed, 0 failed, 2 ignored |
| engine `:check` scripts | 36 of 36 | 36 of 36 (`engconf.all` `27a10bbd`, unchanged) |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | 4 of 4 | 4 of 4 (`plugconf.all` `cf0bfd16`, unchanged) |
| plugin `replay:check` | 124 files, 16,076 cases | 124 files, 16,076 cases |
| plugin evidence tests | 769 of 771 | 769 of 771, `summary.txt` byte-identical (`6290cd61`) |
| `rust-coverage:check` | exit 0: 67,236 of 117,613 | exit 1 (stale); after the write, exit 0: 67,236 of 117,651, 38,231 only other tests reach, 708 files |
| `check-dotnet-engine.sh` | pin `1ba60f931477e2f9`, tree `7dd0c2b9e60f92a1`, exit 1 | exit 0, `current 0.0.0-engine.7dd0c2b9e60f92a1` |
| `check-android-engine.sh` | pin `64cf9e896bdd28bc`, tree `1a68bff677299ac0`, exit 1 | exit 0, `current android-engine-1a68bff677299ac0` |
| iOS simulator (iOS 27.2): `EmbedPDFTests`, `EmbedPDFModelsTests`, `EmbedPDFUITests` | 627, 147, 82 of 82 | 628, 147, 81 of 82; rerun 628, 147, 82 of 82 (CU-C9-D9) |
| macOS arm64 | 627, 147, 81 of 81 | 628, 147, 81 of 81 |
| macOS x86_64 | 627, 147, 81 of 81 | 628, 147, 81 of 81 |

The cargo start is CU-C6's, before CU-C7: 5 of the 6 more tests are CU-C7's uniffi tests (4 in
`crates/plugins/form/tests/ffi.rs`, 1 in `crates/epdf-viewer-host/tests/editing_workflows.rs`), and
plugins' `6b71b4243` adds 2 to `crates/epdf-engine/tests/action_reader.rs` and drops
`the_payloads_this_build_cannot_read_come_back_inert_not_degraded`. The 1 more `EmbedPDFTests` is
the `btn-chain` test in `FormTests.swift` (`866ad6fac`). The coverage record's only change is
`crates/epdf-engine/src/features/actions/reader.rs`, `0/200` to `0/238`: the issue-39 reader's new
lines, which the plugin replay doesn't reach. The 2 evidence failures are the start's
(`current-suites.test.ts`, the kept M27 NaN divergence and the test that reads its result).

The first iOS run failed one UI test, `importDownloadExportAndBothHomesUseOwnedBytes`, on its
60-second limit (`platforms/apple/EmbedPDF/Tests/EmbedPDFUITests/AttachmentModelTests.swift:65`), as
it did in the 2026-11-27 run's CU-C9 on a loaded Mac. Run again, the whole iOS run passed. Skipped
in every Swift run: `PointerLatencyTests`, `StageReaderDragLagTests`, the three signature route tests
(no routes address, CU-C9-D3), and on macOS `theRepositoryRootManifestExportsOnlyTheFacadeAndItsUI`.

The engine framework was rebuilt from the `9472117ed` slices (CU-C9-D3): `build-xcframework.sh`
printed `native graph verified`, and `package-engine-framework.sh` wrote a 116,834,573-byte zip,
SwiftPM checksum `64cc8f93d8b727152eee3ab57247c683bb4978be2e02f0a6e9672cfbae64e6b5`.
`platforms/apple/apple-engine-hash.sh` gives `44a23eb0573339c7` for the tree, the engine the train
will build and pin. Neither `Package.swift` was touched.

## setButtonImage docs

The three docs now name the error the FFI lists
(`crates/plugins/form/src/ffi/editing.rs:360`), in the same words: "`permission-denied` for one a
signature locked or the session may not fill". In
`platforms/android/embedpdf/embedpdf/src/main/kotlin/com/embedpdf/Form.kt` the paragraph was also
rewrapped, since one line ran to 138 columns. C# uses `<see cref="PdfErrorCode.PermissionDenied"/>`.

## Release notes

| File | What it says |
|---|---|
| `docs/releases/0.0.0-swift.12.md` | Upstream `cda9e82a4` (viewer still 3.4.0), engine `apple-engine-44a23eb0573339c7` replacing `ca2421677d81b1d9` (CU-C9-D5); the generated records' new members and `case .pushbutton`; `filledBy`, `filledByName`, `filledAt` read `null`; anyone fields, picture buttons, `setButtonImage(_:image:)` with its undo caveat and `permission-denied`; fills keep signatures (#1162), unsigned signature appearance (#1161) |
| `docs/releases/rust-viewer-0.0.0-preview.9.md` | The same four changes through the kernel, a new `runtime-payloads-<hash>`, no viewer UI change, the `filledBy` and undo line |
| `docs/releases/0.0.0-dotnet.3.md` | `EmbedPDF.Runtime` `0.0.0-engine.7dd0c2b9e60f92a1`; three positional records break, the inputs' `init` properties don't (CU-C9-D7); the rest as Swift's |
| `docs/releases/0.0.0-android.4.md` | `embedpdf-bindings` `0.0.0-engine.1a68bff677299ac0`; the generated records' new members; the rest as Swift's |

`docs/releases/dotnet-engine.md` is unchanged (CU-C9-D6). No release or tag exists yet for the four
versions (`gh release view` on `MFB-Technologies-Inc/embed-pdf-viewer`: "release not found" for
each).

## Release plan

After the controller fast-forwards `research/multi-platform-rust-core-impl-plugins` to this branch
and pushes it to `fork`, dispatch `.github/workflows/release-train.yml` on that branch with these
inputs (the workflow's `workflow_dispatch` inputs, `.github/workflows/release-train.yml`):

| Input | Value |
|---|---|
| `swift_version` | `0.0.0-swift.12` |
| `viewer_version` | `0.0.0-preview.9` |
| `dotnet_version` | `0.0.0-dotnet.3` |
| `android_version` | `0.0.0-android.4` |
| `android_packages_repository` | `MFB-Technologies-Inc/embed-pdf-viewer` |
| `dry_run` | `false` |

```sh
gh workflow run release-train.yml -R MFB-Technologies-Inc/embed-pdf-viewer \
  --ref research/multi-platform-rust-core-impl-plugins \
  -f swift_version=0.0.0-swift.12 -f viewer_version=0.0.0-preview.9 \
  -f dotnet_version=0.0.0-dotnet.3 -f android_version=0.0.0-android.4 \
  -f android_packages_repository=MFB-Technologies-Inc/embed-pdf-viewer -f dry_run=false
```

Review `summarize`'s publish plan at the `approve` gate. It should list `apple-engine-44a23eb0573339c7`,
a new `runtime-payloads-<hash>`, `rust-viewer-0.0.0-preview.9`, `0.0.0-dotnet.3` with
`EmbedPDF.Runtime` `0.0.0-engine.7dd0c2b9e60f92a1`, `0.0.0-android.4` with
`android-engine-1a68bff677299ac0`, `dotnet-engine-0.0.0-local-<sha12 of the dispatched commit>`
and `0.0.0-swift.12`. No runtime push: `runtime-src` is still `9472117ed`. Keep plugins frozen until
the run finishes; a push mid-run breaks `pin` (run `46101091208`).

## Evidence

Container logs in `/workspaces/logs/CU-C9/` of `embed-pdf-catchup1127b`; host logs in
`tmp/cu1127b/logs/CU-C9/` of the host worktree. sha256, first 8 hex digits.

| Check | Command | Exit | Log | sha256 |
|---|---|---|---|---|
| cargo, engine `:check`, plugin checks | `run.sh /workspaces/wt/CU-C2` (`table.sh … head suite replay evidence`) | 0 | `head/cargo.log`, `head/cargo.summary`, `head/engconf.all`, `head/plugconf.all` | `292ce66c`, `f60d8974`, `27a10bbd`, `cf0bfd16` |
| `replay:check` | same | 0 | `head/plugconf.replay:check.log` | `f8f21212` |
| evidence tests | same | 769 pass, 2 fail | `head/evidence/summary.txt` | `6290cd61` |
| `rust-coverage:check` | `T=/workspaces/target-cov rustcov.sh … rustcov check` | 1 (stale) | `rustcov/rust-coverage-check.log` (first run) | `b8f07a22` |
| `rust-coverage` write, then check | `rustcov.sh … rustcov write`, `rustcov.sh … rustcov check` | 0, 0 | `rustcov-write.out` | |
| framework | `build-xcframework.sh` with CU-C8-D1's `LIB_ROOT`, `EMBEDPDF_NM`, `CARGO_BUILD_JOBS=8`; `package-engine-framework.sh` | 0, 0 | `build-xcframework.log`, `package.log` | `caa6d9ee`, `8a075721` |
| Swift | `EMBEDPDF_TEST_DESTINATION=… scripts/cu-c8/swift-tests.sh . tmp/cu1127b/logs/CU-C9/swift local` | ios 65, macos-arm64 0, macos-x86_64 0 | `swift/ios.log`, `swift/macos-arm64.log`, `swift/macos-x86_64.log` | `0c14156b`, `cf31849a`, `05581e6a` |
| Swift iOS rerun | the script's iOS `xcodebuild test` alone | 0 | `swift/ios-rerun.log` | `54a09772` |
| pins | `platforms/dotnet/check-dotnet-engine.sh`, `platforms/android/check-android-engine.sh` at `d60328b76` and `37c92d414` | 0, 0 | (console) | |

Environment: image `embedpdf-dev:latest`, linux-arm64; Xcode 27.2 (27B52), Rust 1.100.0 on the Mac.

## Cleanup

Deleted: `/workspaces/target-cov` (7.0 GB) and bundle `CU-C9a` in the container; the host bundle;
`platforms/apple/.build` (1.6 GB), both `xcframework` directories under `platforms/apple/EmbedPDF`
(656 MB) and `target` (319 MB) in the host worktree; the job's simulator `cu-c9-iphone17pro`
(`81A07831`). Kept: `/workspaces/target` and the container worktrees for CU-C11's cleanup.

## Known gaps

- The pushed tip will name `EmbedPDF.Runtime` `0.0.0-engine.7dd0c2b9e60f92a1` and
  `android-engine-1a68bff677299ac0` before the train publishes them (CU-C9-D4).
- `AttachmentModelTests.importDownloadExportAndBothHomesUseOwnedBytes` can hit its 60-second limit
  on a loaded simulator (CU-C9-D9).
- A `setButtonImage` call is outside the history's undo on every platform, as upstream's is.
