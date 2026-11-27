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

TODO (controller): the dry run's id and result, the real run's id and result, the published
releases and packages, the pin commit and its `apple-engine` tag, and any re-run.
