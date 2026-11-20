C# engine evaluation package, `EmbedPDF.Engine` version `0.0.0-local`, for .NET 10. This is a GitHub prerelease, not a NuGet.org publication. The release tag ends in the first 12 characters of the viewer commit the package was built from, and each package's provenance record names that commit in full.

Start with the attached `EmbedPDF-CSharp-Existing-App.md`. It walks through adding the package to an existing application on Linux arm64: download and check the package, add a local NuGet feed, reference the package, register the bundled font, and render a page. It's written for `linux-arm64`. On another RID, download that RID's package instead; the macOS and Windows packages carry no font for the guide's registration step to read.

## What's in a package

Each package carries the managed `net10.0` library, the native libraries for one RID, manifests and license notices. The native libraries are `epdf-capi` (the Rust engine's C ABI) and PDFium, which is upstream's runtime release archive for that RID, pinned by `packages/engine/runtime/engine-runtime-build.json`. No companion native package is needed.

There's one package per RID, and no multi-RID package:

| RID | Package |
|---|---|
| `linux-arm64` | `EmbedPDF.Engine.0.0.0-local.linux-arm64.nupkg` |
| `linux-x64` | `EmbedPDF.Engine.0.0.0-local.linux-x64.nupkg` |
| `osx-arm64` | `EmbedPDF.Engine.0.0.0-local.osx-arm64.nupkg` |
| `win-x64` | `EmbedPDF.Engine.0.0.0-local.win-x64.nupkg` |

All four have the same package id and version, `EmbedPDF.Engine 0.0.0-local`; the RID is only in the file name. NuGet can't tell them apart, so a feed should hold only the package for the RID it builds for. Each package comes with three more assets: `<package>.nupkg.sha256`, its checksum; `EmbedPDF.Engine.0.0.0-local.<rid>.provenance.json`, the package manifest `pack.sh` wrote, with the source commit, a dirty flag and every packed file's hash; and `EmbedPDF.Engine.0.0.0-local.validation.<rid>.json`, the record validation wrote. `osx-x64` isn't built.

The Linux packages bundle DejaVu Sans, from Debian's `fonts-dejavu-core`, as the fallback font under the key `parity-dejavu` (`platforms/dotnet/packaging/ci-fonts.sh`). The macOS and Windows packages bundle no fallback font: they're packed with the checked-in `platforms/dotnet/packaging/fonts.json`, which is empty. Bundled fonts aren't registered for you. Register them explicitly, as the guide shows.

## Validation

The release train built, tested and validated each package on its own RID's host, not under emulation (`.github/workflows/release-dotnet.yml`). The Linux packages ran in a `debian:trixie` container pinned by digest, on `ubuntu-24.04-arm` for `linux-arm64` and `ubuntu-24.04` for `linux-x64`; `osx-arm64` ran on `macos-15` and `win-x64` on `windows-2022`. Every row used .NET SDK 10.0.403, checked against Microsoft's published SHA-512 before it was unpacked.

On each row the train ran the facade tests (`dotnet test` on `platforms/dotnet/EmbedPDF.Tests`) against a debug `epdf-capi`, the parity self-test, and the packaging unit tests, then built a release `epdf-capi`, packed it with `pack.sh` and validated the package with `validate.sh` (`validate.ps1` on Windows). Validation (`platforms/dotnet/packaging/package.py`) refuses a host whose RID differs from the package's, and checks the package's hash against the manifest and every native file's hash against the package's own inventory. It then restores the package from a local feed into a fresh consumer project with its own NuGet cache, with `EPDF_NATIVE_DIR`, `PDF_RUNTIME_LIB_DIR`, `LD_LIBRARY_PATH`, `DYLD_LIBRARY_PATH` and `DYLD_FALLBACK_LIBRARY_PATH` unset, checks the native libraries' architecture, dependencies and exports, and runs `platforms/dotnet/packaging/Smoke.cs` on `tooling/engine-conformance/fixtures/non_identity_text.pdf`. Three negative checks follow: the app must fail with PDFium removed, with `epdf-capi` removed, and with PDFium copied in under `epdf-capi`'s name. Only when all of that passes does the manifest mark the RID `VALIDATED`; the train fails a row whose RID isn't. On `linux-arm64` the train also ran the guide's own steps in a fresh project and rendered page 0 of that fixture.

Live viewer parity isn't part of validation and wasn't run; each validation record lists it as a separate acceptance gate. The Linux packages were tested against the glibc in that `debian:trixie` image, which the guide states; that's the tested version, not a declared minimum.
