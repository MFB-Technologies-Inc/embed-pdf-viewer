# Try EmbedPDF C# bindings in an existing application

Use the `EmbedPDF.Engine` package downloaded as a GitHub release asset as a library in your existing app. You do not need a new console project or a source checkout.

## Package and platform

- Package: `EmbedPDF.Engine`, version `0.0.0-local`. This is a local evaluation package, not a published NuGet release.
- This package was built from viewer commit `f37adb44799db8ae50158343dcbe042241ee284d`. The `linux-arm64` PDFium inside it is upstream's runtime release `runtime-f2ea75972`, pinned by `packages/engine/runtime/engine-runtime-build.json`; the `runtime-src` checkout at that commit is runtime fork `af480d0ce`, which merges upstream runtime `f2ea75972`.
- Target framework: `net10.0`. The validated SDK is .NET `10.0.403`.
- Packaged native platform: Linux ARM64. Validation ran with glibc 2.41; this is the tested environment, not a declared minimum. Windows, macOS and other RIDs are not covered by this package.
- The package includes the managed library, both native libraries, the DejaVu Sans fallback font, manifests and license notices. No companion native NuGet package is needed.

The package source is [viewer commit f37adb44799db8ae50158343dcbe042241ee284d](https://github.com/roanutil/embed-pdf-viewer/tree/f37adb44799db8ae50158343dcbe042241ee284d). The [tested package consumer](https://github.com/roanutil/embed-pdf-viewer/blob/f37adb44799db8ae50158343dcbe042241ee284d/platforms/dotnet/packaging/Smoke.cs) and [C# source/API](https://github.com/roanutil/embed-pdf-viewer/tree/f37adb44799db8ae50158343dcbe042241ee284d/platforms/dotnet/EmbedPDF) are available for reference. Source checkout is optional.

The native libraries keep build-container paths in their debug and provenance metadata. Those strings are not personal host paths, credentials, or load locations. Deploy the bundled native libraries, font, manifests and notices together; they are loaded relative to the application output. The relocated Linux ARM64 package check for this build passed with `LD_LIBRARY_PATH` unset, using native `$ORIGIN` lookup.

## 1. Obtain the package

Download the package and checksum from this [GitHub prerelease](https://github.com/roanutil/embed-pdf-viewer/releases/tag/dotnet-engine-0.0.0-local-f37adb44799d). This is a release asset, not a NuGet.org publication. From your existing application's directory:

```bash
mkdir -p local-packages
cd local-packages
curl --fail --location --remote-name https://github.com/roanutil/embed-pdf-viewer/releases/download/dotnet-engine-0.0.0-local-f37adb44799d/EmbedPDF.Engine.0.0.0-local.nupkg
curl --fail --location --remote-name https://github.com/roanutil/embed-pdf-viewer/releases/download/dotnet-engine-0.0.0-local-f37adb44799d/EmbedPDF.Engine.0.0.0-local.nupkg.sha256
sha256sum --check EmbedPDF.Engine.0.0.0-local.nupkg.sha256
cd ..
```

Expected package SHA-256: `08a575929ca3d3821b9c8ed9f2949218be6b427e5112686c849f5221bb2447d9` (9,384,244 bytes). Keep the package unchanged; no Rust/PDFium build is required.

## 2. Configure the local NuGet feed

Add an `EmbedPDFLocal` entry to your app's `NuGet.Config`. Preserve existing configuration and feeds. If the file does not exist, create it with:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="EmbedPDFLocal" value="./local-packages" />
  </packageSources>
</configuration>
```

The relative feed path is resolved from this configuration file. If your app uses package source mapping, also permit `EmbedPDF.Engine` on `EmbedPDFLocal` in that mapping.

## 3. Reference the library

Your application's target framework must include `net10.0`. Replace `YourApp.csproj` with the actual project filename:

```bash
dotnet add ./YourApp.csproj package EmbedPDF.Engine --version 0.0.0-local
```

This adds the package reference to your existing project; it does not create another application.

## 4. Call the library from application code

Add this class to your existing application. It explicitly registers the bundled fallback font, opens PDF bytes and renders the first page:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Immutable;
using EmbedPDF.Engine;

public static class PdfPreview
{
    public static async Task<RgbaImage> RenderFirstPageAsync(byte[] pdf)
    {
        var font = await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "000-DejaVuSans.ttf"));

        await using var engine = await PdfEngine.CreateAsync(new()
        {
            StartupFonts = ImmutableArray.Create(
                new StartupFont("parity-dejavu", font)
                {
                    FamilyName = "DejaVu Sans",
                    Weight = 400,
                    Italic = false,
                    Fallback = true
                })
        });

        await using var document = await engine.OpenAsync(pdf);
        return await document.RenderAsync(
            new PageIndex(0), new RenderOptions { Scale = 1.5 });
    }
}
```

Call it inside an existing async application method:

```csharp
var image = await PdfPreview.RenderFirstPageAsync(pdfBytes);
```

Pass bytes for a PDF with at least one page. Use `image.Width`, `image.Height`, `image.Stride` and `image.Bytes` with your app's image API. The bytes are owned, straight-alpha RGBA8 pixels and remain valid after document and engine disposal. This is a library API; your application supplies its presentation and controls.

The facade also exposes page metadata, text, geometry and bounded literal search. The example creates and disposes an engine per call for a simple trial; a longer-lived application can initialize an engine once and dispose it at shutdown.

## 5. Build or publish for Linux ARM64

For a local build:

```bash
dotnet build ./YourApp.csproj -c Release --runtime linux-arm64
```

For deployment:

```bash
dotnet publish ./YourApp.csproj -c Release \
  --runtime linux-arm64 --self-contained false \
  --output ./publish
```

Run the app on Linux ARM64 with a compatible .NET 10 runtime. Deploy the entire publish directory, including both native libraries, the font, manifests and notices. Do not distribute only `EmbedPDF.Engine.dll`. The package avoids a local Rust or PDFium build.

The steps in sections 2 to 5 were run for this release in a Linux ARM64 container (glibc 2.41, SDK 10.0.403): `dotnet add`, `dotnet publish --runtime linux-arm64 --self-contained false`, and the `PdfPreview` class above rendering page 0 of `tooling/engine-conformance/fixtures/non_identity_text.pdf` to a 918x1188 image. Compatibility with a different deployment image or font bundle needs its own check.
