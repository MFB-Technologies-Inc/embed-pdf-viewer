# CU-C10 decisions

Decisions made while preparing the release train for the 2026-11-27 catch-up, on
`research/catchup-2026-11-27-swift` from `8f95cf3bf`, on the Mac host (git, bash and python only).

## CU-C10-D1. The Android engine pin is committed with the .NET one

Decision: `platforms/android/embedpdf/gradle.properties` pins `android-engine-c82c25262037ff9c`,
the hash `android-engine-hash.sh` gives for the tree, in its own commit (`85352588a`), beside the
`EmbedPDF.Runtime.props` pin the brief asked for (`eaa1fd2b9`).
Reason: the brief named only the .NET pin, but `runtime-src` moved to `9472117ed` and the crates
changed, so the Android pin was stale too (`9d9ec62d682771fa`). The train's preflight fails a real
run with an `android_version` on a stale pin (`.github/workflows/release-train.yml:209-211`), and
`release-android.yml`'s "Check the engine pin" step stops one too
(`.github/workflows/release-android.yml:146-153`). The 2026-11-26 release pinned the same way
before its train (`0642f170c`).
Cost: one commit the brief didn't list.
Reverse: `git revert 85352588a`, and dispatch without an `android_version`.

## CU-C10-D2. Both pins name packages the train hasn't published yet

Decision: `EmbedPDF.Runtime.props` pins `0.0.0-engine.11ca75894cda26ea` and `gradle.properties`
pins `android-engine-c82c25262037ff9c` before either exists on GitHub Packages. With the pin equal
to the tree's hash, `check-dotnet-engine.sh` exits 0 and never asks the feed
(`platforms/dotnet/check-dotnet-engine.sh:59-62`); `check-android-engine.sh` likewise.
Reason: exit 0 means "the pin is current", not "the package is published". Exit 1 (stale, not on
the feed) and 2 (stale, on the feed) are the only verdicts that ask the feed, and an unpublished
runtime can't reach 2. The train's `runtime-pack` job runs the same script before it packs
(`.github/workflows/release-dotnet.yml:651-652`), so it needs exit 0, and `dotnet-viewer-release`
publishes that runtime. `a1ccd7080` pinned
`0.0.0-engine.39d10114bde3ec2a` the same way on 2026-11-26, before run 45963302053 published it.
Cost: from the plugins push until the train's publish jobs finish, the plugins tip names an
`EmbedPDF.Runtime` and an `embedpdf-bindings` that don't exist; a consumer restoring from the
tip in that window fails.
Reverse: none needed once the train publishes; before that, revert `eaa1fd2b9` and `85352588a`.

## CU-C10-D3. `dotnet-engine.md` stays as it is

Decision: `docs/releases/dotnet-engine.md` isn't edited.
Reason: nothing it says changed between `d250a68e3` and this branch. `epdf-capi` has no code
change (CU-C8-D9), the package layout, RIDs, fonts and `.github/actions/setup-dotnet` versions are
the same, and the notes never named a runtime commit.
Cost: the `dotnet-engine-0.0.0-local-<sha12>` release this train makes doesn't say it moved to
runtime `9472117ed`.
Reverse: edit the file before a later train.

## CU-C10-D4. The versions are the next integers

Decision: `0.0.0-swift.11`, `rust-viewer-0.0.0-preview.7`, `0.0.0-dotnet.1`, `0.0.0-android.2`.
Reason: the last tags on `fork` are `0.0.0-swift.10` (`89dde2cf9`) and `rust-viewer-0.0.0-preview.6`,
`0.0.0-dotnet.0`, `0.0.0-android.1` (all `d250a68e3`), and `gh release view` finds none of the new
four.
Cost: none.
Reverse: rename the four notes files and the `package.json` version.
