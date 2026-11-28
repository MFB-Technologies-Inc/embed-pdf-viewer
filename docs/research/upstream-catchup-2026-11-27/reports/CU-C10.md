# CU-C10: pushes and release

The release is prepared on `research/catchup-2026-11-27-swift`, which plugins fast-forwards to:
notes for the four releases, the viewer bundle at `0.0.0-preview.7`, and the .NET and Android
engine pins at the tree's hashes. `check-dotnet-engine.sh` and `check-android-engine.sh` both exit
0. The runtime fork was pushed before this job, and the release train hasn't run yet. Decisions
are in [`../decisions/CU-C10.md`](../decisions/CU-C10.md).

## Runtime push

The user approved the pushes and the release on 2026-11-27. The controller pushed
`catchup/2026-11-27-runtime` to `MFB-Technologies-Inc/embed-pdf-runtime`
`research/multi-platform-rust-core`, `af480d0ce..9472117ed`, a fast-forward (`af480d0ce` is an
ancestor of the merge). `git ls-remote fork research/multi-platform-rust-core` in
`~/repos/embed-pdf-runtime` answers `9472117edbc7d9d980888c2418573081a76f532f`, the commit
`packages/engine/runtime/runtime-src` names.

## Commits

On `research/catchup-2026-11-27-swift` from `8f95cf3bf`.

| Commit | Summary |
|---|---|
| `eaa1fd2b9` | `EmbedPDF.Runtime.props` pins `0.0.0-engine.11ca75894cda26ea`, was `39d10114bde3ec2a` |
| `85352588a` | `gradle.properties` pins `android-engine-c82c25262037ff9c`, was `9d9ec62d682771fa` (CU-C10-D1) |
| `6d54450b8` | `docs/releases/0.0.0-swift.11.md`, `rust-viewer-0.0.0-preview.7.md`, `0.0.0-dotnet.1.md`, `0.0.0-android.2.md`; `packages/rust-viewer-bundle/package.json` `0.0.0-preview.7` |
| the docs commit | this report, `decisions/CU-C10.md`, the `decisions.md` index, and `reports/CU-C9.md:9` reworded ("CU-C9's own commits published nothing and moved neither pin.") |

`docs/releases/dotnet-engine.md` is unchanged (CU-C10-D3).

## Versions

| Release | Version | Previous | Notes |
|---|---|---|---|
| Swift package | `0.0.0-swift.11` | `0.0.0-swift.10` (`89dde2cf9`) | `docs/releases/0.0.0-swift.11.md` |
| Rust viewer bundle | `rust-viewer-0.0.0-preview.7` | `preview.6` (`d250a68e3`) | `docs/releases/rust-viewer-0.0.0-preview.7.md` |
| C# viewer packages | `0.0.0-dotnet.1` | `0.0.0-dotnet.0` (`d250a68e3`) | `docs/releases/0.0.0-dotnet.1.md` |
| Android | `0.0.0-android.2` | `0.0.0-android.1` (`d250a68e3`) | `docs/releases/0.0.0-android.2.md` |
| C# engine | `dotnet-engine-0.0.0-local-<sha12 of the dispatched commit>` | `dotnet-engine-0.0.0-local-d250a68e3a8f` | `docs/releases/dotnet-engine.md` |

What the train should build and publish, from the tree's hash scripts at `6d54450b8`, none of it
on the fork yet (`gh release view`, `gh api` on the org's packages, 2026-11-27):

| Artifact | Hash script | Value | Was |
|---|---|---|---|
| Apple engine | `platforms/apple/apple-engine-hash.sh` | `apple-engine-ddad56cdb86961ce` | `apple-engine-c55b85806b96c5b2` |
| wasm payload | `packages/engine/runtime/scripts/runtime-payload-hash.sh` | `runtime-payloads-4f2cb5fbc177da69` | `runtime-payloads-3b4e5f2a666a966b` |
| `EmbedPDF.Runtime` | `platforms/dotnet/dotnet-engine-hash.sh` | `0.0.0-engine.11ca75894cda26ea` | `0.0.0-engine.39d10114bde3ec2a` |
| `embedpdf-bindings` | `platforms/android/android-engine-hash.sh` | `0.0.0-engine.c82c25262037ff9c` | `0.0.0-engine.9d9ec62d682771fa` |
| PDFium runtime | the `runtime-src` gitlink | `runtime-<sha>` for `9472117ed` | `af480d0ce` |

## Checks

All on the Mac host; each script is bash and git only, and with a current pin neither engine check
calls `gh` or the registry.

| Check | Before the pins (`8f95cf3bf`) | After (`85352588a`) |
|---|---|---|
| `platforms/dotnet/check-dotnet-engine.sh` | exit 1: `pinned 0.0.0-engine.39d10114bde3ec2a`, `current 0.0.0-engine.11ca75894cda26ea`, unpublished | exit 0: `current 0.0.0-engine.11ca75894cda26ea` |
| `platforms/android/check-android-engine.sh` | stale: pin `9d9ec62d682771fa`, tree `c82c25262037ff9c` | exit 0: `current android-engine-c82c25262037ff9c` |
| `jq -r .version packages/rust-viewer-bundle/package.json` | `0.0.0-preview.6` | `0.0.0-preview.7` |
| notes files the preflight reads | missing for all four versions | present and non-empty |

The script's exit codes: 0 the pin is current; 1 stale and not on the feed; 2 stale but on the
feed, commit the pin; 64 bad arguments or no pin; 69 `gh` failed; 70 no hash
(`platforms/dotnet/check-dotnet-engine.sh:15-23`). Exit 0 needs only the pin to equal the tree's
hash, which is what the train's `runtime-pack` step checks (CU-C10-D2).

Dispatch inputs for `release-train.yml` on `research/multi-platform-rust-core-impl-plugins`, once
it is fast-forwarded to this branch and pushed: `swift_version` `0.0.0-swift.11`, `viewer_version`
`0.0.0-preview.7`, `dotnet_version` `0.0.0-dotnet.1`, `android_version` `0.0.0-android.2`,
`android_packages_repository` `MFB-Technologies-Inc/embed-pdf-viewer` (where the five
`com.embedpdf` packages of 0.0.0-android.1 live), `dry_run` `true` first, then `false`.

## Release train

Five runs. The release that shipped is run
[46105185430](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/actions/runs/46105185430) at
`66b33cdb1`, which is not the set of versions in the tables above: an `issue-39` merge landed on
plugins mid-release and moved every engine hash, so the last run published `preview.8`, `dotnet.2`
and `android.3` on new engines. `swift.11` kept its number because it never published before.

| Run | Commit | Kind | Result |
|---|---|---|---|
| [46039059363](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/actions/runs/46039059363) | `d3ef5cef0` | dry | Attempt 1 hung for over an hour in `actions/cache` restoring `emsdk-4.0.0-Linux-X64` in `release-payload.yml (build) / verify`; cancelled. Attempt 2 failed: the payload postlink check couldn't read upstream's compact `pdfFunctionSignatures` table (#1143). Fixed in `0afd87825`. |
| [46050833661](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/actions/runs/46050833661) | `0afd87825` | dry | Attempt 1: `release-dotnet.yml / consumer (linux-x64)` failed with "the model has no active hit" (`platforms/dotnet/samples/EmbedPDF.Sample.Console/Program.cs:109`). Attempt 2 green. |
| [46064004730](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/actions/runs/46064004730) | `0afd87825` | real | Never reached `approve`. `TextSelectionModelTests.RectsAreFinalAfterTheEndEvenWhenAnEventRefreshInterleaves` failed in both attempts, `SearchModelTests.ClosingTheDocumentEndsTheTaskAndEmptiesTheModel` in the second, and `SearchHighlightOverlayTests.aHitIsFilledWhereItsWordIs` in the first. Both .NET tests timed their waits; fixed in the tests only, `e3d6e7fbc` (0 failures in 120 local runs, 3 in 60 before). |
| [46080379002](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/actions/runs/46080379002) | `e3d6e7fbc` | real | Never reached `approve`. `aHitIsFilledWhereItsWordIs` drew black where the hit fill should be, in both attempts; the second also hit six 60 s time limits at once. The first `ImageRenderer` render on a loaded simulator can stall and return bad pixels; the test now renders until two renders agree, `0fa20cd23`. |
| [46101091208](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/actions/runs/46101091208) | `0fa20cd23` | real | Approved. Published `apple-engine-ddad56cdb86961ce`, `runtime-payloads-4f2cb5fbc177da69`, `runtime-9472117ed…`, `rust-viewer-0.0.0-preview.7`, `0.0.0-dotnet.1`, `0.0.0-android.2` and `dotnet-engine-0.0.0-local-0fa20cd23968`. `pin` failed: its push wasn't a fast-forward, because `89d95275e` (the `fix/issue-39-action-payloads` merge) reached plugins two minutes after dispatch. `0.0.0-swift.11` didn't publish and no pin landed. |
| [46105185430](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/actions/runs/46105185430) | `66b33cdb1` | real | Green on the first attempt, approved. |

The `issue-39` merge changed `crates/pdfium/src/action.rs` and the stage `uniffi` settings, so at
`89d95275e` every pin was stale: Apple `ca2421677d81b1d9`, .NET `1ba60f931477e2f9`, Android
`64cf9e896bdd28bc`. Pinning `ddad56cd` on top would have shipped Swift a binary without the fix.
Prep for the last run: `05c9ede7c` (.NET pin), `54fa68a14` (Android pin), `66b33cdb1` (notes for
`preview.8`, `dotnet.2`, `android.3`, and `swift.11` moved to the new engine).

Run 46105185430 published `apple-engine-ca2421677d81b1d9`, `runtime-payloads-3a9695da0a92494b`,
`rust-viewer-0.0.0-preview.8`, `0.0.0-dotnet.2`, `0.0.0-android.3`,
`dotnet-engine-0.0.0-local-66b33cdb15e2` and `0.0.0-swift.11`, all prereleases. `runtime-9472117ed…`
already existed and was skipped. The org's packages have `EmbedPDF.Runtime`
`0.0.0-engine.1ba60f931477e2f9` and `embedpdf-bindings` `0.0.0-engine.64cf9e896bdd28bc`. The pin
commit is `298bfb998` (`build(apple): pin apple-engine-ca2421677d81b1d9`), and `0.0.0-swift.11`
tags it. The releases from run 46101091208 stay published and are superseded.

After this release we stopped doing a separate dry run: a real run builds and tests everything a
dry run does before `approve`, and `summarize` shows the publish plan there.

Follow-ups:

- Give the setup-emsdk cache step a `timeout-minutes`, so a hung restore fails in minutes, not an
  hour.
- Nothing stops a push to plugins during a run. `pin` could rebase onto the branch tip when the tip
  doesn't change any hash input, or the train could refuse to start while other work is pending.
- `ToolModelTests.AnActivationCompletesWhileStateRevisionsKeepArriving`
  (`platforms/dotnet/EmbedPDF.Models.Tests/ToolModelTests.cs:413`) is timing based too and failed
  once in 30 local runs. It hasn't failed in CI.
- The .NET console sample's "no active hit" failure in run 46050833661 wasn't investigated.
