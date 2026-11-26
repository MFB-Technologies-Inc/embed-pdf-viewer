"""Packs the managed packages that sit on top of the EmbedPDF facade.

EmbedPDF.Models, EmbedPDF.Extensions.DependencyInjection and
EmbedPDF.Scripting.Jint are packed against a facade package that
package.py pack-facade made, at the facade's version. This lives apart from
package.py because package.py is an input of dotnet-engine-hash.sh: editing
it moves the EmbedPDF.Runtime version, and managed packaging never changes
the native library.

EmbedPDF.Models reads internal members of the facade
(InternalsVisibleTo("EmbedPDF.Models")), so it depends on exactly the
EmbedPDF version it was built against, [x.y.z]. The pack fails otherwise.
"""
import argparse
import os
from pathlib import Path
import re
import sys
import tempfile
import zipfile

from package import DOTNET, check_toolchain, run

# Package id: (project, exact EmbedPDF dependency).
LIBRARIES = {
    "EmbedPDF.Models": ("EmbedPDF.Models/EmbedPDF.Models.csproj", True),
    "EmbedPDF.Extensions.DependencyInjection":
        ("EmbedPDF.Extensions.DependencyInjection/EmbedPDF.Extensions.DependencyInjection.csproj", False),
    "EmbedPDF.Scripting.Jint": ("EmbedPDF.Scripting.Jint/EmbedPDF.Scripting.Jint.csproj", False),
}

def dependency_range(nuspec, package_id):
    """The version range a nuspec gives package_id, or None."""
    for match in re.finditer(r"<dependency\s+([^>]*?)/?>", nuspec):
        attributes = dict(re.findall(r'(\w+)="([^"]*)"', match.group(1)))
        if attributes.get("id") == package_id:
            return attributes.get("version")
    return None

def facade_version(path):
    match = re.fullmatch(r"EmbedPDF\.(\d[^/]*)\.nupkg", Path(path).name)
    if not match:
        raise ValueError(f"not an EmbedPDF facade package: {Path(path).name}")
    return match.group(1)

def check_library(package, package_id, version, exact):
    with zipfile.ZipFile(package) as bundle:
        names = bundle.namelist()
        nuspec = bundle.read(f"{package_id}.nuspec").decode()
    if f"lib/net10.0/{package_id}.dll" not in names:
        raise ValueError(f"{package_id}: lib/net10.0/{package_id}.dll missing from package")
    if any(name.endswith("/EmbedPDF.Viewer.dll") for name in names):
        raise ValueError(f"{package_id}: carries EmbedPDF.Viewer.dll, which belongs to the EmbedPDF package")
    found = dependency_range(nuspec, "EmbedPDF")
    if exact and found != f"[{version}]":
        raise ValueError(f"{package_id}: EmbedPDF dependency must be the exact range [{version}], found {found!r}")
    if not exact and found not in (version, f"[{version}]"):
        raise ValueError(f"{package_id}: EmbedPDF dependency must start at {version}, found {found!r}")

def pack_libraries(args):
    check_toolchain()
    facade = Path(args.facade).resolve()
    runtime = Path(args.runtime_package).resolve()
    version = facade_version(facade)
    if not runtime.name.startswith("EmbedPDF.Runtime."):
        raise ValueError(f"not an EmbedPDF.Runtime package: {runtime.name}")
    output = Path(args.out).resolve()
    output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="epdf-libraries-") as cache:
        env = dict(os.environ, NUGET_PACKAGES=cache)
        for package_id, (project, exact) in LIBRARIES.items():
            run(["dotnet", "pack", str(DOTNET / project), "-c", "Release", "-o", str(output),
                 f"-p:PackageVersion={version}", "-p:EmbedPdfPackRuntime=true",
                 f"-p:RestoreAdditionalProjectSources={runtime.parent}"], cwd=DOTNET, env=env)
            package = output / f"{package_id}.{version}.nupkg"
            if not package.is_file():
                raise ValueError(f"dotnet pack wrote no {package.name}")
            check_library(package, package_id, version, exact)
            print(f"Library package: {package}")

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="action", required=True)
    pack = sub.add_parser("pack-libraries", help="pack the models, DI and Jint packages against a facade package")
    pack.add_argument("--facade", required=True, help="the EmbedPDF.<version>.nupkg from package.py pack-facade")
    pack.add_argument("--runtime-package", required=True, help="the EmbedPDF.Runtime nupkg the facade depends on")
    pack.add_argument("--out", required=True)
    args = parser.parse_args()
    try:
        pack_libraries(args)
        return 0
    except (ValueError, RuntimeError, OSError, KeyError, zipfile.BadZipFile) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1

if __name__ == "__main__":
    sys.exit(main())
