# CU-C10 decisions

## CU-C10-D1. The runtime release reuses the `wasm32-eh` and Android archives and builds the other two

Decision: `runtime-af480d0ce420ed916db0de8d2e8827822ad7be1e` carries CU-C1's `wasm32-eh` archive,
CU-C8c's `android-arm64` and `android-x64` archives, and `linux-arm64` and `wasm32` archives built
for this job by `scripts/cu-c10/build-rest.sh` in a fresh `linux/amd64` `debian:trixie` container
(`PDF_RUNTIME_TARGET_OS_LIST="linux emscripten"`, emsdk 3.1.72), through `heavy-build.sh`.
Reason: the brief, and the payload. The wasm32 payload links CU-C1's `wasm32-eh` library, so a
rebuilt archive would make the payload's `libembedpdf.a` differ from the released one.
Cost: the five archives come from three containers, not one fresh checkout as the
`17d8073d4` release's did. The release notes say so.
Reverse: rebuild all five in one container and republish the payload from the new `wasm32-eh`.

## CU-C10-D2. Published past the upstream freshness gate

Decision: the runtime release went out although `check-upstream-freshness.py --enforce` exits 1:
PDFium `1f02bc7d9` is 13 upstream commits behind, the oldest 2.6 days old against a 2-day limit.
Reason: none of the 13 matches the script's security-fix pattern, and the release pins the fork
commit that merges upstream runtime `f2ea75972`, which is on the same PDFium. The `17d8073d4`
release did the same with a security fix missing; this one has `4e04b1315`.
Cost: the archives trail upstream PDFium by 13 commits.
Reverse: sync the fork to upstream PDFium and release again.

## CU-C10-D3. The payload's `wasm32-eh` stamp is written by hand, with no `source`

Decision: `perf-compare/.work/fork/af480d0ce…/wasm32-eh.json` names CU-C1's archive unpacked into
`wasm32-eh-staging/` (recipe 4, `seeded: false`, `libSha256`
`fc55c432157a980f3670214cf053d22ed10d8fa3673bff13028c0ad79fdf4e53`), so `buildFork` reuses it.
Reason: the archive is at exactly the pinned `runtime-src` commit, and `payload-notes.mjs` refuses
a `source` block in that case. Without a stamp the Mac would build `wasm32-eh` itself.
Cost: the stamp is taken on its own word for the recipe; `forkStampMatches` checks the library's
hash and its `BUILD-METADATA.json` (`af480d0ce`, `wasm32-eh`).
Reverse: delete the stamp and staging directory and let `buildFork` build the target.

## CU-C10-D4. No Apple dry run

Decision: `publish-engine-payload.mjs --only apple` ran once, for real, after the wasm half's dry
run and real run.
Reason: the Apple half runs its three Swift test runs before `gh release create`, so a failure
stops before anything is uploaded. A dry run would have repeated the 16-minute framework build and
the three runs.
Cost: the release notes the script writes were not previewed.
Reverse: run `--dry-run --only apple` first.

## CU-C10-D5. The pin commit also moves the rust-viewer bundle to `0.0.0-preview.3`

Decision: `f37adb447` changes both `Package.swift` pins and the bundle's `version` and README
install line.
Reason: the bundle's `buildInfo` records the commit and whether the tree was dirty, and its
release is tagged at the pin commit, so the version has to be in that commit. Neither file is an
input of the payload or Apple engine hash.
Cost: the pin commit is not only a pin.
Reverse: a separate version commit before the pin, which moves the pin off `1b2d18408`.

## CU-C10-D6. The tags were pushed before their releases were made

Decision: the three tags were created locally at `f37adb447` and pushed with the branches (step 7),
and the releases were then made on the pushed tags with `gh release create --verify-tag`, their
target set to `f37adb447`.
Reason: GitHub can only make a release at a commit it has, and the pin commit reached the fork
with the branch push.
Cost: none; the order of steps 6 and 7 is swapped.
Reverse: none needed.

## CU-C10-D7. The .NET package is linux-arm64 only

Decision: `dotnet-engine-0.0.0-local-f37adb44799d` carries one `linux-arm64` package, as the
`d59dbcafe24d` release did, built and validated in the project container with upstream's
`runtime-f2ea75972` `linux-arm64` archive and the DejaVu Sans font manifest.
Reason: the previous release's shape. The other RIDs were checked in CU-C8b, but there is no
multi-RID packing step, and osx-x64 cannot be validated on this Mac (CU-C8b-D6).
Cost: the package's manifest marks the other four RIDs `UNVALIDATED`.
Reverse: pack and validate each RID at the pin commit and merge them into one package.
