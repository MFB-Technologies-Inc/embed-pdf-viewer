#!/usr/bin/env bash
# CU-C10: the linux-arm64 .NET evaluation package at the checked-out commit, as
# dotnet-engine-0.0.0-local-d59dbcafe24d was made: the facade tests, the parity
# self-test, the packaging unit tests, epdf-capi release, pack.sh with the
# DejaVu Sans font manifest, validate.sh, then the existing-app guide's steps in
# a fresh project with its own NuGet cache.
set -uo pipefail
C=/workspaces/wt/CU-C10; OUT=/workspaces/logs/CU-C10/dotnet; FONTS=/workspaces/scratch/cu-c10/fonts/fonts.json
ARCHIVE=/workspaces/pdfium/archives/libembedpdf-runtime-linux-arm64-f2ea75972.tar.gz
MANIFEST=$C/packages/engine/runtime/engine-runtime-build.json
rm -rf "$OUT"; mkdir -p "$OUT"; R=$OUT/summary.txt; : > "$R"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 CI=true
export CARGO_TARGET_DIR=/workspaces/target CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0
export PDF_RUNTIME_LIB_DIR=$C/packages/engine/runtime/build/libpdfium
export LD_LIBRARY_PATH=$PDF_RUNTIME_LIB_DIR/linux-arm64/lib
export EPDF_IMAGE_ID=$(cat /workspaces/scratch/cu-c10/image-id) EPDF_HOST_OS=macos
cd "$C" || exit 2
echo "checkout $(git rev-parse HEAD) dirty=[$(git status --porcelain --untracked-files=no | wc -l)] start $(date -u +%FT%TZ)" >> "$R"
echo "dotnet $(cd platforms/dotnet && dotnet --version) $(rustc --version) $(ldd --version | head -1)" >> "$R"
cargo build --locked -p epdf-capi > "$OUT/capi-build.log" 2>&1; echo "capi build exit=$?" >> "$R"
EPDF_NATIVE_DIR=$CARGO_TARGET_DIR/debug dotnet test platforms/dotnet/EmbedPDF.Tests/EmbedPDF.Tests.csproj > "$OUT/dotnet-test.log" 2>&1; echo "dotnet test exit=$?" >> "$R"
grep -E "^(Passed|Failed)!|Total tests|Passed:|Failed:" "$OUT/dotnet-test.log" | tail -3 >> "$R"
(cd platforms/dotnet && dotnet run --project EmbedPDF.Parity -- self-test) > "$OUT/parity.log" 2>&1
echo "parity self-test exit=$? passed=$(grep -c '"status":"passed"' "$OUT/parity.log")" >> "$R"
python3 -m unittest discover -s platforms/dotnet/packaging -v > "$OUT/packaging-unittest.log" 2>&1
echo "packaging unittest exit=$? $(tail -1 "$OUT/packaging-unittest.log")" >> "$R"
cargo build --locked -p epdf-capi --release --target aarch64-unknown-linux-gnu > "$OUT/capi-release.log" 2>&1; echo "capi release build exit=$?" >> "$R"
env -u LD_LIBRARY_PATH -u DYLD_LIBRARY_PATH bash platforms/dotnet/pack.sh --rid linux-arm64 --out "$OUT/pack" --runtime-manifest "$MANIFEST" \
  --runtime-archive "$ARCHIVE" --font-manifest "$FONTS" > "$OUT/pack.log" 2>&1; echo "pack exit=$?" >> "$R"
env -u LD_LIBRARY_PATH -u DYLD_LIBRARY_PATH bash platforms/dotnet/validate.sh --rid linux-arm64 --runtime-manifest "$MANIFEST" --record "$OUT/validate-record.json" \
  --package "$OUT/pack/EmbedPDF.Engine.0.0.0-local.nupkg" --manifest "$OUT/pack/package-manifest.linux-arm64.json" > "$OUT/validate.log" 2>&1
echo "validate exit=$? $(tail -1 "$OUT/validate.log")" >> "$R"
# The guide's steps, in a fresh application with its own NuGet cache.
G=$OUT/guide; mkdir -p "$G/app/local-packages"; cp "$OUT/pack/EmbedPDF.Engine.0.0.0-local.nupkg" "$G/app/local-packages/"
(
  set -e
  cd "$G/app"
  unset LD_LIBRARY_PATH PDF_RUNTIME_LIB_DIR
  export NUGET_PACKAGES=$G/nuget-cache
  dotnet new console --framework net10.0 --name YourApp --output . > ../new.log 2>&1
  cat > NuGet.Config <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="EmbedPDFLocal" value="./local-packages" />
  </packageSources>
</configuration>
XML
  dotnet add ./YourApp.csproj package EmbedPDF.Engine --version 0.0.0-local > ../add.log 2>&1
  cat > PdfPreview.cs <<'CS'
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
CS
  cat > Program.cs <<'CS'
var pdfBytes = await System.IO.File.ReadAllBytesAsync(args[0]);
var image = await PdfPreview.RenderFirstPageAsync(pdfBytes);
System.Console.WriteLine($"rendered {image.Width}x{image.Height} stride={image.Stride} bytes={image.Bytes.Length}");
CS
  dotnet publish ./YourApp.csproj -c Release --runtime linux-arm64 --self-contained false --output ./publish > ../publish.log 2>&1
  ./publish/YourApp "$C/tooling/engine-conformance/fixtures/non_identity_text.pdf"
) > "$G/run.log" 2>&1
echo "guide exit=$? $(tail -1 "$G/run.log")" >> "$R"
(cd "$OUT/pack" && sha256sum EmbedPDF.Engine.0.0.0-local.nupkg && stat -c %s EmbedPDF.Engine.0.0.0-local.nupkg) >> "$R"
objdump -T "$OUT"/pack/*/lib*.so 2>/dev/null | grep -o 'GLIBC_[0-9.]*' | sort -uV | tail -1 >> "$R" || true
echo "end $(date -u +%FT%TZ)" >> "$R"; echo DOTNETDONE >> "$R"
