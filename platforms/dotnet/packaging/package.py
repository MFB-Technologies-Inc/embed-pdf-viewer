"""Offline native packing and manual relocated-consumer validation."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile
import zipfile

from schema import validate

ROOT = Path(__file__).resolve().parents[3]
DOTNET = ROOT / "platforms/dotnet"
RIDS = {
    "linux-x64": ("x86_64-unknown-linux-gnu", "linux-x64", "libepdf_capi.so", "lib/libembedpdf.so"),
    "linux-arm64": ("aarch64-unknown-linux-gnu", "linux-arm64", "libepdf_capi.so", "lib/libembedpdf.so"),
    "osx-arm64": ("aarch64-apple-darwin", "darwin-arm64", "libepdf_capi.dylib", "lib/libembedpdf.dylib"),
    "osx-x64": ("x86_64-apple-darwin", "darwin-x64", "libepdf_capi.dylib", "lib/libembedpdf.dylib"),
    "win-x64": ("x86_64-pc-windows-msvc", "win32-x64", "epdf_capi.dll", "bin/embedpdf.dll"),
}

def read(path):
    return json.loads(Path(path).read_text())

def write(path, value):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_text(json.dumps(value, indent=2) + "\n")

def sha(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as source:
        for block in iter(lambda: source.read(1024*1024), b""):
            digest.update(block)
    return digest.hexdigest()

def file_record(path, base=ROOT):
    path = Path(path).resolve()
    return {"path": os.path.relpath(path, base), "sha256": sha(path), "bytes": path.stat().st_size}

def run(command, cwd=ROOT, env=None):
    result = subprocess.run(command, cwd=cwd, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    print(result.stdout, end="", flush=True)
    if result.returncode:
        raise RuntimeError(f"command failed ({result.returncode}): {command!r}")
    return result.stdout

def source_identity():
    commit = run(["git","-c","core.preloadIndex=false","rev-parse","HEAD"]).strip()
    diff = subprocess.check_output(["git","-c","core.preloadIndex=false","diff","HEAD","--binary","--","crates/epdf-capi","platforms/dotnet"],cwd=ROOT)
    untracked = subprocess.check_output(["git","-c","core.preloadIndex=false","ls-files","--others","--exclude-standard","--","crates/epdf-capi","platforms/dotnet"],cwd=ROOT,text=True).splitlines()
    for name in sorted(untracked):
        path = ROOT/name
        if path.is_file():
            diff += name.encode()+b"\\0"+path.read_bytes()
    result = {"commit":commit,"dirty":bool(diff)}
    if diff:
        result["diffSha256"] = hashlib.sha256(diff).hexdigest()
    return result

def host_rid():
    machine = platform.machine().lower()
    arch = {"aarch64": "arm64", "arm64": "arm64", "x86_64": "x64", "amd64": "x64"}.get(machine, machine)
    os_name = {"Linux": "linux", "Darwin": "osx", "Windows": "win"}.get(platform.system(), "unsupported")
    return f"{os_name}-{arch}"

def check_elf(name, dynamic, dependencies):
    for dependency in re.findall(r"\(NEEDED\).*?\[([^]]+)\]", dynamic):
        if "/" in dependency:
            raise ValueError(f"{name}: absolute dependency {dependency}")
    paths = re.findall(r"\((?:RUNPATH|RPATH)\).*?\[([^]]*)\]", dynamic)
    if name in ("libepdf_capi.so", "libepdf_viewer_host.so") and not paths:
        raise ValueError(f"{name}: relative $ORIGIN rpath required")
    for path in paths:
        if any(entry not in ("$ORIGIN", "$ORIGIN/") for entry in path.split(":")):
            raise ValueError(f"{name}: relative $ORIGIN rpath required: {path}")
    if "not found" in dependencies or "undefined symbol:" in dependencies:
        raise ValueError(f"{name}: missing dynamic dependency\n{dependencies}")

def check_exports(exported, prefix=""):
    required = ["epdf_abi_version","epdf_engine_create","epdf_engine_submit","epdf_job_wait",
                "epdf_job_cancel","epdf_job_release","epdf_engine_close","epdf_engine_release",
                "epdf_helper_call","epdf_result_view","epdf_result_release"]
    for name in required:
        if not re.search(r"\b"+prefix+name+r"(?:\s|$)", exported, re.M):
            raise ValueError(f"missing required C ABI export: {name}")

FOREIGN_LOADERS = {"linux-x64": "/lib64/ld-linux-x86-64.so.2", "linux-arm64": "/lib/ld-linux-aarch64.so.1"}

def list_dependencies(path, rid):
    """`ldd -r`, or the RID's own loader in trace mode when the host is another
    architecture (an emulated or cross-built slice), since ldd only knows its own."""
    if host_rid() == rid:
        return run(["ldd", "-r", str(path)])
    env = dict(os.environ, LD_TRACE_LOADED_OBJECTS="1", LD_WARN="yes", LD_BIND_NOW="yes")
    return run([FOREIGN_LOADERS[rid], str(path)], env=env)

def inspect(native, rid):
    details = {}
    for path in sorted(native.iterdir()):
        if not path.is_file() or path.suffix.lower() not in (".so", ".dylib", ".dll"):
            continue
        if rid.startswith("linux"):
            dynamic = run(["readelf", "-d", str(path)])
            dependencies = list_dependencies(path, rid)
            check_elf(path.name, dynamic, dependencies)
            for name, resolved in re.findall(r"^\s*(\S+) => (\S+)", dependencies, re.M):
                if name == "libembedpdf.so" and Path(resolved).resolve().parent != native.resolve():
                    raise ValueError(f"{path}: runtime resolves outside relocated package: {resolved}")
            symbols = run(["readelf", "--version-info", str(path)])
            versions = set(re.findall(r"GLIBC_(\d+(?:\.\d+)+)", symbols))
            if path.name == "libepdf_capi.so":
                exported = run(["nm", "-D", "--defined-only", str(path)])
                check_exports(exported)
            if path.name == "libepdf_viewer_host.so":
                check_viewer_exports(run(["nm", "-D", "--defined-only", str(path)]))
            details[path.name] = {"glibcRequired": max(versions, key=lambda v: tuple(map(int, v.split(".")))) if versions else None,
                                  "dependencies": dependencies, "dynamic": dynamic}
            architecture = run(["readelf", "-h", str(path)])
            expected = "AArch64" if rid.endswith("arm64") else "Advanced Micro Devices X86-64"
            if expected not in architecture:
                raise ValueError(f"{path}: wrong RID architecture")
        elif rid.startswith("osx"):
            deps = run(["otool", "-L", str(path)])
            load = run(["otool", "-l", str(path)])
            expected = "arm64" if rid.endswith("arm64") else "x86_64"
            if expected not in run(["lipo", "-archs", str(path)]).split():
                raise ValueError(f"{path}: wrong RID architecture")
            for dependency in [line.strip().split(" (")[0] for line in deps.splitlines()[1:]]:
                if not dependency.startswith(("@rpath/", "@loader_path/", "/usr/lib/", "/System/Library/")):
                    raise ValueError(f"{path}: build-machine install name {dependency}")
            for rpath in re.findall(r"cmd LC_RPATH\s+cmdsize \d+\s+path (.*?) \(offset", load):
                if rpath != "@loader_path":
                    raise ValueError(f"{path}: build-machine rpath {rpath}")
            if path.name == "libepdf_capi.dylib":
                check_exports(run(["nm", "-gU", str(path)]), "_")
            if path.name == "libepdf_viewer_host.dylib":
                check_viewer_exports(run(["nm", "-gU", str(path)]), "_")
            details[path.name] = {"dependencies": deps, "loadCommands": load}
        else:
            details[path.name] = {"dependencies": run(["dumpbin", "/DEPENDENTS", str(path)]),
                                  "headers": run(["dumpbin", "/HEADERS", str(path)])}
            if path.name == "epdf_capi.dll":
                check_exports(run(["dumpbin", "/EXPORTS", str(path)]))
            if path.name == "epdf_viewer_host.dll":
                check_viewer_exports(run(["dumpbin", "/EXPORTS", str(path)]))
            if "8664 machine (x64)" not in details[path.name]["headers"]:
                raise ValueError(f"{path}: wrong RID architecture")
    return details

def check_toolchain():
    if run(["dotnet", "--version"], cwd=DOTNET).strip() != "10.0.403":
        raise ValueError(".NET SDK 10.0.403 required")
    if not run(["rustc", "--version"]).startswith("rustc 1.98.0 "):
        raise ValueError("Rust 1.98.0 required")

def pack(args):
    check_toolchain()
    target, fork_target, wrapper, runtime_relative = RIDS[args.rid]
    pin_path = Path(args.runtime_manifest).resolve()
    pin = read(pin_path)
    cache = Path(os.environ.get("EPDF_RUNTIME_CACHE", ROOT / "packages/engine/runtime/build/cache"))
    archive = Path(args.runtime_archive or cache / f"libembedpdf-runtime-{fork_target}.tar.gz")
    if not archive.is_file():
        raise ValueError(f"missing offline checksum archive: {archive}; pack never downloads")
    if sha(archive) != pin["artifacts"][fork_target]["sha256"]:
        raise ValueError("runtime archive checksum differs from pinned manifest")
    build = Path(os.environ.get("CARGO_TARGET_DIR", ROOT / "target"))
    library = Path(args.native_lib or build / target / "release" / wrapper).resolve()
    if not library.is_file():
        raise ValueError(f"missing Cargo release library: {library}")
    output = Path(args.out).resolve()
    output.mkdir(parents=True, exist_ok=True)
    # A new staging directory prevents stale assets leaking into a package.
    stage = Path(tempfile.mkdtemp(prefix="epdf-stage-", dir=output))
    native = stage / "runtimes" / args.rid / "native"
    native.mkdir(parents=True)
    shutil.copy2(library, native / wrapper)
    with tarfile.open(archive) as bundle:
        members = {entry.name.removeprefix("./"): entry for entry in bundle.getmembers()}
        metadata = json.load(bundle.extractfile(members["BUILD-METADATA.json"]))
        if metadata["target"] != fork_target or metadata["sha"] != pin["sha"]:
            raise ValueError("runtime archive target/source identity mismatch")
        selected = [runtime_relative] + [name for name in members if name.startswith("LICENSES/") and members[name].isfile()]
        for name in selected:
            member = members[name]
            if not member.isfile():
                raise ValueError(f"runtime archive contains non-file asset: {name}")
            destination = native / Path(name).name if name == runtime_relative else stage / "notices" / Path(name).name
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(bundle.extractfile(member).read())
    font_path = Path(args.font_manifest).resolve()
    fonts = read(font_path)
    delivered_fonts = []
    for index, font in enumerate(fonts["fonts"]):
        source = (font_path.parent / font["path"]).resolve()
        notice = (font_path.parent / font["notice"]).resolve()
        if sha(source) != font["sha256"] or sha(notice) != font["noticeSha256"]:
            raise ValueError("font bytes/notice checksum differs from ordered font manifest")
        destination = stage / "fonts" / f"{index:03d}-{source.name}"
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
        shutil.copy2(notice, destination.with_suffix(destination.suffix + ".LICENSE"))
        delivered_fonts.append({**font, "path":destination.name,
                                "notice":destination.name+".LICENSE"})
    shutil.copy2(ROOT / "LICENSE", stage / "notices/EmbedPDF-LICENSE")
    shutil.copy2(font_path, stage / "fonts.json")
    shutil.copy2(pin_path, stage / "runtime.json")
    shutil.copy2(pin_path, native / "epdf-runtime.json")
    write(native / "epdf-fonts.json", {**fonts, "fonts":delivered_fonts})
    details = inspect(native, args.rid)
    write(stage / "native-inspection.json", details)
    write(native / "epdf-native-inspection.json", details)
    # Notices/fonts live alongside the native assets in NuGet as well as the archive.
    for path in (stage / "notices").iterdir():
        shutil.copy2(path, native / path.name)
    for path in (stage / "fonts").glob("*") if (stage / "fonts").exists() else []:
        shutil.copy2(path, native / path.name)
    source = source_identity()
    asset_identity = {"source":source,
                      "runtimeFork":pin["sha"],"runtimeArchiveSha256":sha(archive),
                      "runtimeTarget":fork_target,"rid":args.rid,"abi":1,
                      "files":[file_record(path, native) for path in sorted(native.iterdir()) if path.is_file()]}
    write(native / "epdf-assets.json", asset_identity)
    version = args.version
    if list(output.glob("package-manifest.*.json")):
        raise ValueError("output contains a prior package manifest; use a fresh per-RID output directory")
    run(["dotnet", "pack", str(DOTNET / "EmbedPDF/EmbedPDF.csproj"), "-c", "Release",
         "-o", str(output), f"-p:PackageVersion={version}", f"-p:EpdfNativeAssets={stage / 'runtimes'}"], cwd=DOTNET)
    packages = list(output.glob(f"EmbedPDF.Engine.{version}.nupkg"))
    if len(packages) != 1:
        raise ValueError("expected exactly one local nupkg")
    package = packages[0]
    with zipfile.ZipFile(package) as bundle:
        if "lib/net10.0/EmbedPDF.Engine.dll" not in bundle.namelist():
            raise ValueError("managed assembly missing from package")
        for path in native.iterdir():
            if f"runtimes/{args.rid}/native/{path.name}" not in bundle.namelist():
                raise ValueError(f"native asset missing from package: {path.name}")
    artifact = output / f"EmbedPDF.Engine.{version}.{args.rid}.tar.gz"
    with tarfile.open(artifact, "w:gz") as bundle:
        for path in sorted(stage.rglob("*")):
            if path.is_file():
                bundle.add(path, arcname=path.relative_to(stage))
    manifest = {"schemaVersion":1,"packageId":"EmbedPDF.Engine","version":version,
        "source":source,"runtimeFork":pin["sha"],
        "runtimeManifest":file_record(pin_path), "fontManifest":file_record(font_path), "abi":1,
        "rids":{rid:{"status":"PACKED_UNVALIDATED" if rid == args.rid else "UNVALIDATED",
                    "reason":"Local package produced; relocated validation and live parity required" if rid == args.rid else "No artifact/native validation supplied"} for rid in RIDS},
        "files":[file_record(path, output) for path in sorted(stage.rglob("*")) if path.is_file()] +
                [file_record(artifact, output),file_record(package, output)],
        "limitations":["Single RID local package; all five native legs and live parity are required for release acceptance"] + fonts.get("limitations", [])}
    validate(manifest, read(DOTNET / "package-manifest.schema.json"))
    write(output / f"package-manifest.{args.rid}.json", manifest)
    print(f"Local package: {package}")

def validate_package(args):
    record_path = Path(args.record).resolve()
    pin_path = Path(args.runtime_manifest).resolve()
    pin = read(pin_path)
    source = source_identity()
    source["runtimeFork"] = pin["sha"]
    environment = {"kind":"container" if os.environ.get("EPDF_IMAGE_ID") else "host",
        "os":platform.platform(),"arch":host_rid().split("-")[-1],
        "emulated":os.environ.get("EPDF_EMULATED","false").lower()=="true",
        "tools":{}}
    if environment["kind"] == "container":
        environment["imageId"] = os.environ["EPDF_IMAGE_ID"]
        environment["hostOs"] = os.environ.get("EPDF_HOST_OS","unspecified")
    record = {"schemaVersion":1,"slice":6,"rid":args.rid,"source":source,
        "environment":environment,"inputs":[file_record(pin_path),file_record(Path(args.font_manifest)),
            file_record(ROOT/"tooling/engine-conformance/fixtures/non_identity_text.pdf")],
        "commands":[],"artifacts":[],"limitations":["Live viewer parity is a separate required acceptance gate."]}
    command = [sys.executable, str(Path(__file__).resolve()), "smoke", "--rid", args.rid,
               "--package", str(Path(args.package).resolve()), "--manifest", str(Path(args.manifest).resolve())]
    result = subprocess.run(command,cwd=ROOT,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    print(result.stdout, end="")
    for tool, command_version in [("dotnet",["dotnet","--version"]),("rustc",["rustc","--version"]),("libc",["ldd","--version"])]:
        try:
            environment["tools"][tool] = subprocess.check_output(command_version,cwd=DOTNET,text=True,stderr=subprocess.STDOUT).splitlines()[0]
        except (OSError,subprocess.CalledProcessError):
            environment["tools"][tool] = "unavailable"
    record["commands"].append({"command":subprocess.list2cmdline(command),"cwd":str(ROOT),"exitCode":result.returncode,
                              "results":result.stdout[-16000:] or "No output"})
    if result.returncode == 0:
        manifest_path = Path(args.manifest).resolve()
        manifest = read(manifest_path)
        if environment["emulated"]:
            manifest["rids"][args.rid] = {"status":"container-emulated",
                "reason":"Built and tested under emulation on arm64; no native x64 run. Live parity remains required."}
        else:
            manifest["rids"][args.rid] = {"status":"VALIDATED",
                "reason":"Relocated package consumer passed on matching native architecture. Live parity remains required."}
        validate(manifest, read(DOTNET / "package-manifest.schema.json"))
        write(manifest_path, manifest)
    for path in [args.package,args.manifest]:
        if Path(path).is_file():
            record["artifacts"].append(file_record(Path(path)))
    if result.returncode:
        record["limitations"].append("Relocated validation failed; no compatibility or RID success claim.")
    if environment["emulated"]:
        record["limitations"].append("Status: container-emulated. Built and tested under emulation on arm64; no native x64 run. Emulation does not substitute for native-host evidence.")
    validate(record, read(DOTNET / "validation/record.schema.json"))
    write(record_path,record)
    return result.returncode

def smoke(args):
    if platform.system() == "Linux" and not os.environ.get("EPDF_IMAGE_ID"):
        raise ValueError("Linux validation requires the approved container EPDF_IMAGE_ID")
    if host_rid() != args.rid:
        raise ValueError(f"native host RID {host_rid()} differs from requested {args.rid}")
    manifest_path = Path(args.manifest).resolve()
    manifest = read(manifest_path)
    validate(manifest, read(DOTNET / "package-manifest.schema.json"))
    if manifest["rids"][args.rid]["status"] == "UNVALIDATED":
        raise ValueError("manifest has no packed artifact for requested RID")
    package = Path(args.package).resolve()
    entry = next((entry for entry in manifest["files"] if (manifest_path.parent/entry["path"]).resolve()==package),None)
    if entry is None or sha(package) != entry["sha256"]:
        raise ValueError("package checksum/identity differs from manifest")
    with zipfile.ZipFile(package) as bundle:
        prefix = f"runtimes/{args.rid}/native/"
        identity = json.loads(bundle.read(prefix+"epdf-assets.json"))
        for field in ["source","runtimeFork","abi"]:
            if identity[field] != manifest[field]:
                raise ValueError(f"packaged {field} identity differs from manifest")
        if identity["rid"] != args.rid:
            raise ValueError("packaged RID identity differs from requested RID")
        required = {RIDS[args.rid][2], Path(RIDS[args.rid][3]).name}
        if not required.issubset({asset["path"] for asset in identity["files"]}):
            raise ValueError("packaged native asset inventory must include wrapper and runtime dependency")
        for asset in identity["files"]:
            payload = bundle.read(prefix+asset["path"])
            if hashlib.sha256(payload).hexdigest() != asset["sha256"] or len(payload) != asset["bytes"]:
                raise ValueError(f"packaged native asset checksum mismatch: {asset['path']}")
    with tempfile.TemporaryDirectory(prefix="epdf-relocated-") as temporary:
        directory = Path(temporary)
        packages = directory / "local"
        packages.mkdir()
        shutil.copy2(package,packages/package.name)
        (directory/"global.json").write_text((DOTNET/"global.json").read_text())
        shutil.copy2(DOTNET/"packaging/Smoke.cs",directory/"Program.cs")
        shutil.copy2(ROOT/"tooling/engine-conformance/fixtures/non_identity_text.pdf",directory/"input.pdf")
        (directory/"Consumer.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><SelfContained>false</SelfContained><UseAppHost>false</UseAppHost><EnableRuntimePackDownload>false</EnableRuntimePackDownload><ImplicitUsings>enable</ImplicitUsings><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup><ItemGroup><PackageReference Include="EmbedPDF.Engine" Version="'+manifest["version"]+'"/></ItemGroup></Project>')
        env = os.environ.copy()
        for name in ["EPDF_NATIVE_DIR","PDF_RUNTIME_LIB_DIR","LD_LIBRARY_PATH","DYLD_LIBRARY_PATH","DYLD_FALLBACK_LIBRARY_PATH"]:
            env.pop(name,None)
        env["NUGET_PACKAGES"] = str(directory/"cache")
        run(["dotnet","restore","Consumer.csproj","--source",str(packages),"--runtime",args.rid],cwd=directory,env=env)
        run(["dotnet","build","Consumer.csproj","-c","Release","--no-restore","--runtime",args.rid,"-o",str(directory/"app")],cwd=directory,env=env)
        app = directory/"app"
        native = app
        wrapper = app/RIDS[args.rid][2]
        dependency = app/Path(RIDS[args.rid][3]).name
        inspect_assets = directory/"inspect"
        inspect_assets.mkdir()
        for path in [wrapper,dependency]:
            if not path.is_file():
                raise ValueError(f"fresh NuGet consumer is missing native asset {path.name}")
            shutil.copy2(path,inspect_assets/path.name)
        inspect(inspect_assets,args.rid)
        command = ["dotnet",str(app/"Consumer.dll"),str(directory/"input.pdf")]
        run(command,cwd=directory,env=env)
        for path in [dependency,wrapper]:
            hidden = path.with_suffix(path.suffix+".hidden")
            path.rename(hidden)
            result = subprocess.run(command,cwd=directory,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
            hidden.rename(path)
            if result.returncode == 0:
                raise ValueError(f"missing {path.name} unexpectedly succeeded; build-tree dependency escaped")
        backup = wrapper.with_suffix(wrapper.suffix+".backup")
        wrapper.rename(backup)
        shutil.copy2(dependency,wrapper)
        result = subprocess.run(command,cwd=directory,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
        wrapper.unlink()
        backup.rename(wrapper)
        if result.returncode == 0:
            raise ValueError("wrong-ABI native wrapper unexpectedly succeeded")
        print("PASS: relocated open/text/region/helper, ABI, missing-wrapper, missing-dependency and wrong-ABI checks")

# --- EmbedPDF.Runtime: the viewer host (epdf-viewer-host) and its PDFium ---
#
# Each RID is built and staged on its own host (stage-runtime), which writes a
# slice: runtimes/<rid>/native/ plus runtime-slice.json recording every file's
# hash. pack-runtime re-checks every slice against its record and packs all of
# them into one EmbedPDF.Runtime nupkg.

VIEWER_RIDS = {
    "linux-x64": ("x86_64-unknown-linux-gnu", "linux-x64", "libepdf_viewer_host.so", "lib/libembedpdf.so"),
    "linux-arm64": ("aarch64-unknown-linux-gnu", "linux-arm64", "libepdf_viewer_host.so", "lib/libembedpdf.so"),
    "osx-arm64": ("aarch64-apple-darwin", "darwin-arm64", "libepdf_viewer_host.dylib", "lib/libembedpdf.dylib"),
    "osx-x64": ("x86_64-apple-darwin", "darwin-x64", "libepdf_viewer_host.dylib", "lib/libembedpdf.dylib"),
    "win-x64": ("x86_64-pc-windows-msvc", "win32-x64", "epdf_viewer_host.dll", "bin/embedpdf.dll"),
}
VIEWER_SOURCES = ("Cargo.toml", "Cargo.lock", "rust-toolchain.toml", "crates", "platforms/dotnet")
SLICE_RECORD = "runtime-slice.json"
ENGINE_VERSION = re.compile(r"^0\.0\.0-engine\.[0-9a-f]{16}$")

def check_viewer_exports(exported, prefix=""):
    # Every uniffi library exports its contract version; the generated C#
    # calls it first and refuses a library from another uniffi.
    name = "ffi_epdf_viewer_host_uniffi_contract_version"
    if not re.search(r"\b"+prefix+name+r"(?:\s|$)", exported, re.M):
        raise ValueError(f"missing viewer host export: {name}")

def viewer_source_identity():
    commit = run(["git","-c","core.preloadIndex=false","rev-parse","HEAD"]).strip()
    diff = subprocess.check_output(["git","-c","core.preloadIndex=false","diff","HEAD","--binary","--",*VIEWER_SOURCES],cwd=ROOT)
    untracked = subprocess.check_output(["git","-c","core.preloadIndex=false","ls-files","--others","--exclude-standard","--",*VIEWER_SOURCES],cwd=ROOT,text=True).splitlines()
    for name in sorted(untracked):
        path = ROOT/name
        if path.is_file():
            diff += name.encode()+b"\\0"+path.read_bytes()
    result = {"commit":commit,"dirty":bool(diff)}
    if diff:
        result["diffSha256"] = hashlib.sha256(diff).hexdigest()
    return result

def relocate(host, rid):
    """Points the host at the PDFium beside it instead of the build tree."""
    if rid.startswith("linux"):
        run(["patchelf", "--set-rpath", "$ORIGIN", str(host)])
    elif rid.startswith("osx"):
        run(["install_name_tool", "-id", f"@rpath/{host.name}", str(host)])
        load = run(["otool", "-l", str(host)])
        for rpath in re.findall(r"cmd LC_RPATH\s+cmdsize \d+\s+path (.*?) \(offset", load):
            if rpath != "@loader_path":
                run(["install_name_tool", "-delete_rpath", rpath, str(host)])
        if "path @loader_path (offset" not in run(["otool", "-l", str(host)]):
            run(["install_name_tool", "-add_rpath", "@loader_path", str(host)])
        for dependency in [line.strip().split(" (")[0] for line in run(["otool", "-L", str(host)]).splitlines()[1:]]:
            if dependency.startswith("/") and Path(dependency).name == "libembedpdf.dylib":
                run(["install_name_tool", "-change", dependency, "@rpath/libembedpdf.dylib", str(host)])
        # install_name_tool invalidates the signature; arm64 refuses unsigned code.
        run(["codesign", "--force", "--sign", "-", str(host)])

def stage_runtime(args):
    # Staging builds nothing managed, so a slice row needs Rust but no .NET SDK.
    if not run(["rustc", "--version"]).startswith("rustc 1.98.0 "):
        raise ValueError("Rust 1.98.0 required")
    target, fork_target, host_name, runtime_relative = VIEWER_RIDS[args.rid]
    pin_path = Path(args.runtime_manifest).resolve()
    pin = read(pin_path)
    cache = Path(os.environ.get("EPDF_RUNTIME_CACHE", ROOT / "packages/engine/runtime/build/cache"))
    archive = Path(args.runtime_archive or cache / f"libembedpdf-runtime-{fork_target}.tar.gz")
    if not archive.is_file():
        raise ValueError(f"missing offline checksum archive: {archive}; staging never downloads")
    if sha(archive) != pin["artifacts"][fork_target]["sha256"]:
        raise ValueError("runtime archive checksum differs from pinned manifest")
    library = Path(args.native_lib).resolve()
    if not library.is_file() or library.name != host_name:
        raise ValueError(f"missing viewer host library {host_name}: {library}")
    output = Path(args.out).resolve()
    if output.exists() and any(output.iterdir()):
        raise ValueError(f"slice output must be new or empty: {output}")
    native = output / "runtimes" / args.rid / "native"
    native.mkdir(parents=True)
    shutil.copy2(library, native / host_name)
    with tarfile.open(archive) as bundle:
        members = {entry.name.removeprefix("./"): entry for entry in bundle.getmembers()}
        metadata = json.load(bundle.extractfile(members["BUILD-METADATA.json"]))
        if metadata["target"] != fork_target or metadata["sha"] != pin["sha"]:
            raise ValueError("runtime archive target/source identity mismatch")
        selected = [runtime_relative] + [name for name in members if name.startswith("LICENSES/") and members[name].isfile()]
        for name in selected:
            member = members[name]
            if not member.isfile():
                raise ValueError(f"runtime archive contains non-file asset: {name}")
            (native / Path(name).name).write_bytes(bundle.extractfile(member).read())
    shutil.copy2(ROOT / "LICENSE", native / "EmbedPDF-LICENSE")
    shutil.copy2(pin_path, native / "epdf-runtime.json")
    relocate(native / host_name, args.rid)
    details = inspect(native, args.rid)
    write(output / "native-inspection.json", details)
    record = {"schemaVersion":1,"rid":args.rid,"source":viewer_source_identity(),
              "runtimeFork":pin["sha"],"runtimeArchiveSha256":sha(archive),"runtimeTarget":fork_target,
              "files":[file_record(path, output) for path in sorted(native.iterdir()) if path.is_file()]}
    write(output / SLICE_RECORD, record)
    for entry in record["files"]:
        print(f"{entry['bytes']:>12}  {entry['path']}")
    print(f"Staged slice: {output}")

def read_slice(directory):
    directory = Path(directory).resolve()
    record = read(directory / SLICE_RECORD)
    if record.get("schemaVersion") != 1 or record.get("rid") not in VIEWER_RIDS:
        raise ValueError(f"{directory}: not a runtime slice record")
    recorded = {entry["path"] for entry in record["files"]}
    for entry in record["files"]:
        path = directory / entry["path"]
        if not path.is_file() or sha(path) != entry["sha256"] or path.stat().st_size != entry["bytes"]:
            raise ValueError(f"slice checksum mismatch: {entry['path']}")
    for path in (directory / "runtimes").rglob("*"):
        if path.is_file() and path.relative_to(directory).as_posix() not in recorded:
            raise ValueError(f"slice holds an unrecorded file: {path.relative_to(directory).as_posix()}")
    return directory, record

def engine_hash():
    return run(["bash", str(DOTNET / "dotnet-engine-hash.sh")]).strip().splitlines()[-1]

def pack_runtime(args):
    slices = [read_slice(directory) for directory in args.slice]
    rids = [record["rid"] for _, record in slices]
    for rid in rids:
        if rids.count(rid) > 1:
            raise ValueError(f"two slices for {rid}")
    if len({record["source"]["commit"] for _, record in slices}) > 1:
        raise ValueError("slices come from different source commits")
    if len({record["runtimeFork"] for _, record in slices}) > 1:
        raise ValueError("slices link different runtime forks")
    version = args.version or f"0.0.0-engine.{engine_hash()}"
    if not ENGINE_VERSION.match(version):
        raise ValueError(f"runtime package version must be 0.0.0-engine.<16 hex>: {version}")
    check_toolchain()
    pin_path = Path(args.runtime_manifest).resolve()
    if read(pin_path)["sha"] != slices[0][1]["runtimeFork"]:
        raise ValueError("slices link a runtime fork other than the pinned manifest's")
    output = Path(args.out).resolve()
    output.mkdir(parents=True, exist_ok=True)
    if list(output.glob("package-manifest.runtime.json")):
        raise ValueError("output contains a prior runtime manifest; use a fresh output directory")
    stage = Path(tempfile.mkdtemp(prefix="epdf-runtime-", dir=output))
    for directory, record in slices:
        for entry in record["files"]:
            destination = stage / entry["path"]
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(directory / entry["path"], destination)
        write(stage / "runtimes" / record["rid"] / "native" / "epdf-assets.json", record)
    run(["dotnet", "pack", str(DOTNET / "EmbedPDF.Runtime/EmbedPDF.Runtime.csproj"), "-c", "Release",
         "-o", str(output), f"-p:PackageVersion={version}", f"-p:EpdfNativeAssets={stage / 'runtimes'}"], cwd=DOTNET)
    package = output / f"EmbedPDF.Runtime.{version}.nupkg"
    if not package.is_file():
        raise ValueError(f"dotnet pack wrote no {package.name}")
    with zipfile.ZipFile(package) as bundle:
        names = set(bundle.namelist())
        for path in stage.rglob("*"):
            if path.is_file() and path.relative_to(stage).as_posix() not in names:
                raise ValueError(f"native asset missing from package: {path.relative_to(stage).as_posix()}")
        if any(name.startswith("lib/") for name in names):
            raise ValueError("the runtime package must carry no managed assembly")
    manifest = {"schemaVersion":1,"packageId":"EmbedPDF.Runtime","version":version,
        "source":viewer_source_identity(),"runtimeFork":slices[0][1]["runtimeFork"],
        "runtimeManifest":file_record(pin_path),"abi":1,
        "rids":{rid:{"status":"PACKED_UNVALIDATED","reason":"Packed from a staged slice; no consumer run yet"}
                    if rid in rids else {"status":"UNVALIDATED","reason":"No slice staged for this RID"} for rid in VIEWER_RIDS},
        "files":[file_record(path, output) for path in sorted(stage.rglob("*")) if path.is_file()] + [file_record(package, output)],
        "limitations":["A packed RID is not a validated RID: validate-runtime runs the consumer on that RID's host."]}
    validate(manifest, read(DOTNET / "package-manifest.schema.json"))
    write(output / "package-manifest.runtime.json", manifest)
    print(f"Runtime package: {package}")

FIXTURE = ROOT / "pdfium-multiplatform-prototype/fixtures/report.pdf"
FIXTURE_PAGES = 14
SCRUBBED_ENV = ["EPDF_NATIVE_DIR","PDF_RUNTIME_LIB_DIR","LD_LIBRARY_PATH","DYLD_LIBRARY_PATH","DYLD_FALLBACK_LIBRARY_PATH"]

def pinned_runtime_version():
    props = (DOTNET / "EmbedPDF.Runtime/EmbedPDF.Runtime.props").read_text()
    match = re.search(r"<EmbedPdfRuntimeVersion>(.*?)</EmbedPdfRuntimeVersion>", props)
    if not match:
        raise ValueError("EmbedPDF.Runtime.props has no EmbedPdfRuntimeVersion")
    return match.group(1)

def pack_facade(args):
    """Packs the EmbedPDF facade against the runtime package it depends on."""
    check_toolchain()
    runtime = Path(args.runtime_package).resolve()
    match = re.fullmatch(r"EmbedPDF\.Runtime\.(.+)\.nupkg", runtime.name)
    if not match:
        raise ValueError(f"not an EmbedPDF.Runtime package: {runtime.name}")
    if match.group(1) != pinned_runtime_version():
        raise ValueError(f"runtime package {match.group(1)} is not the pinned {pinned_runtime_version()}; run check-dotnet-engine.sh")
    if not (DOTNET / "EmbedPDF.Viewer/Generated/epdf_viewer_host.cs").is_file():
        raise ValueError("generated bindings are missing; run platforms/dotnet/generate-bindings.sh")
    output = Path(args.out).resolve()
    output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="epdf-facade-") as cache:
        env = dict(os.environ, NUGET_PACKAGES=cache)
        run(["dotnet", "pack", str(DOTNET / "EmbedPDF.Viewer/EmbedPDF.Viewer.csproj"), "-c", "Release", "-o", str(output),
             f"-p:PackageVersion={args.version}", "-p:EmbedPdfPackRuntime=true",
             f"-p:RestoreAdditionalProjectSources={runtime.parent}"], cwd=DOTNET, env=env)
    package = output / f"EmbedPDF.{args.version}.nupkg"
    with zipfile.ZipFile(package) as bundle:
        if "lib/net10.0/EmbedPDF.dll" not in bundle.namelist():
            raise ValueError("facade assembly missing from package")
        nuspec = bundle.read("EmbedPDF.nuspec").decode()
        if f'id="EmbedPDF.Runtime" version="{match.group(1)}"' not in nuspec:
            raise ValueError("facade package does not depend on the runtime package it was packed against")
    print(f"Facade package: {package}")

def smoke_runtime(args):
    emulated = os.environ.get("EPDF_EMULATED","false").lower() == "true"
    if platform.system() == "Linux" and not os.environ.get("EPDF_IMAGE_ID"):
        raise ValueError("Linux validation requires the approved container EPDF_IMAGE_ID")
    if emulated and args.rid != "linux-x64":
        raise ValueError("emulation is recorded only for linux-x64")
    if not emulated and host_rid() != args.rid:
        raise ValueError(f"native host RID {host_rid()} differs from requested {args.rid}")
    manifest_path = Path(args.manifest).resolve()
    manifest = read(manifest_path)
    validate(manifest, read(DOTNET / "package-manifest.schema.json"))
    if manifest["packageId"] != "EmbedPDF.Runtime":
        raise ValueError("manifest is not an EmbedPDF.Runtime manifest")
    if manifest["rids"][args.rid]["status"] == "UNVALIDATED":
        raise ValueError("manifest has no packed artifact for requested RID")
    package = Path(args.package).resolve()
    entry = next((entry for entry in manifest["files"] if (manifest_path.parent/entry["path"]).resolve()==package),None)
    if entry is None or sha(package) != entry["sha256"]:
        raise ValueError("package checksum/identity differs from manifest")
    host_name = VIEWER_RIDS[args.rid][2]
    pdfium_name = Path(VIEWER_RIDS[args.rid][3]).name
    with zipfile.ZipFile(package) as bundle:
        prefix = f"runtimes/{args.rid}/native/"
        identity = json.loads(bundle.read(prefix+"epdf-assets.json"))
        if identity.get("rid") != args.rid or identity.get("runtimeFork") != manifest["runtimeFork"]:
            raise ValueError("packaged slice identity differs from manifest")
        names = {Path(asset["path"]).name for asset in identity["files"]}
        if not {host_name, pdfium_name} <= names:
            raise ValueError("packaged native asset inventory must include the viewer host and PDFium")
        for asset in identity["files"]:
            payload = bundle.read(prefix+Path(asset["path"]).name)
            if hashlib.sha256(payload).hexdigest() != asset["sha256"] or len(payload) != asset["bytes"]:
                raise ValueError(f"packaged native asset checksum mismatch: {asset['path']}")
    facade = Path(args.facade).resolve()
    facade_version = re.fullmatch(r"EmbedPDF\.(.+)\.nupkg", facade.name).group(1)
    dotnet = os.environ.get("EPDF_DOTNET", "dotnet")
    with tempfile.TemporaryDirectory(prefix="epdf-runtime-consumer-") as temporary:
        directory = Path(temporary)
        feed = directory / "local"
        feed.mkdir()
        shutil.copy2(package, feed / package.name)
        shutil.copy2(facade, feed / facade.name)
        (directory/"global.json").write_text((DOTNET/"global.json").read_text())
        shutil.copy2(DOTNET/"packaging/ViewerSmoke.cs", directory/"Program.cs")
        shutil.copy2(FIXTURE, directory/"input.pdf")
        (directory/"Consumer.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>'
            '<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><UseAppHost>false</UseAppHost>'
            '<SelfContained>false</SelfContained><EnableRuntimePackDownload>false</EnableRuntimePackDownload></PropertyGroup>'
            '<ItemGroup><PackageReference Include="EmbedPDF" Version="'+facade_version+'"/></ItemGroup></Project>')
        env = os.environ.copy()
        for name in SCRUBBED_ENV:
            env.pop(name, None)
        env["NUGET_PACKAGES"] = str(directory/"cache")
        def consume(app, expect_code, expect_text):
            result = subprocess.run([dotnet, str(app/"Consumer.dll"), str(directory/"input.pdf")], cwd=directory, env=env,
                                    stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
            print(result.stdout, end="")
            if expect_code is None and result.returncode == 0:
                raise ValueError(f"{app.name}: expected a failure, got success")
            if expect_code is not None and result.returncode != expect_code:
                raise ValueError(f"{app.name}: exit {result.returncode}, expected {expect_code}")
            for text in expect_text:
                if text not in result.stdout:
                    raise ValueError(f"{app.name}: output lacks {text!r}")
        def hidden(path, body):
            moved = path.with_suffix(path.suffix + ".hidden")
            path.rename(moved)
            try:
                body()
            finally:
                moved.rename(path)
        passed = [f"PASS: rid={args.rid} pages={FIXTURE_PAGES}"]
        missing = ["NativeRuntimeNotFoundException", f"rid={args.rid}"]
        # A RID-specific build: NuGet copies the native files beside the app.
        run([dotnet,"restore","Consumer.csproj","--source",str(feed),"--runtime",args.rid],cwd=directory,env=env)
        run([dotnet,"build","Consumer.csproj","-c","Release","--no-restore","--runtime",args.rid,"--self-contained","false",
             "-o",str(directory/"app-rid")],cwd=directory,env=env)
        app = directory/"app-rid"
        consume(app, 0, passed)
        hidden(app/host_name, lambda: consume(app, 3, missing))
        hidden(app/pdfium_name, lambda: consume(app, None, []))
        # A portable build: the files stay under runtimes/<rid>/native.
        run([dotnet,"build","Consumer.csproj","-c","Release","-o",str(directory/"app-portable")],cwd=directory,env=env)
        app = directory/"app-portable"
        native = app/"runtimes"/args.rid/"native"
        if not (native/host_name).is_file():
            raise ValueError(f"portable build has no {native/host_name}")
        consume(app, 0, passed)
        hidden(native/host_name, lambda: consume(app, 3, missing))
        print("PASS: RID-specific and portable consumers, missing-host and missing-PDFium checks")

def validate_runtime(args):
    record_path = Path(args.record).resolve()
    pin_path = Path(args.runtime_manifest).resolve()
    pin = read(pin_path)
    source = viewer_source_identity()
    source["runtimeFork"] = pin["sha"]
    emulated = os.environ.get("EPDF_EMULATED","false").lower() == "true"
    environment = {"kind":"container" if os.environ.get("EPDF_IMAGE_ID") else "host",
        "os":platform.platform(),"arch":args.rid.split("-")[-1] if emulated else host_rid().split("-")[-1],
        "emulated":emulated,"tools":{}}
    if environment["kind"] == "container":
        environment["imageId"] = os.environ["EPDF_IMAGE_ID"]
        environment["hostOs"] = os.environ.get("EPDF_HOST_OS","unspecified")
    record = {"schemaVersion":1,"package":"EmbedPDF.Runtime","phase":1,"rid":args.rid,"source":source,
        "environment":environment,"inputs":[file_record(pin_path),file_record(FIXTURE)],
        "commands":[],"artifacts":[],"limitations":["Phase 1 checks loading and one document open; the facade API is Phase 2."]}
    command = [sys.executable, str(Path(__file__).resolve()), "smoke-runtime", "--rid", args.rid,
               "--package", str(Path(args.package).resolve()), "--facade", str(Path(args.facade).resolve()),
               "--manifest", str(Path(args.manifest).resolve())]
    result = subprocess.run(command,cwd=ROOT,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    print(result.stdout, end="")
    dotnet = os.environ.get("EPDF_DOTNET", "dotnet")
    for tool, command_version in [("dotnet",[dotnet,"--version"]),("rustc",["rustc","--version"]),("libc",["ldd","--version"])]:
        try:
            environment["tools"][tool] = subprocess.check_output(command_version,cwd=DOTNET,text=True,stderr=subprocess.STDOUT).splitlines()[0]
        except (OSError,subprocess.CalledProcessError):
            environment["tools"][tool] = "unavailable"
    record["commands"].append({"command":subprocess.list2cmdline(command),"cwd":str(ROOT),"exitCode":result.returncode,
                              "results":result.stdout[-16000:] or "No output"})
    if result.returncode == 0:
        manifest_path = Path(args.manifest).resolve()
        manifest = read(manifest_path)
        manifest["rids"][args.rid] = ({"status":"container-emulated",
            "reason":"Consumer passed under emulation on an arm64 host; no native x64 run."} if emulated else
            {"status":"VALIDATED","reason":"Local-feed consumer passed on the matching native architecture."})
        validate(manifest, read(DOTNET / "package-manifest.schema.json"))
        write(manifest_path, manifest)
    for path in [args.package,args.facade,args.manifest]:
        if Path(path).is_file():
            record["artifacts"].append(file_record(Path(path)))
    if result.returncode:
        record["limitations"].append("Consumer validation failed; no RID success claim.")
    if emulated:
        record["limitations"].append("Status: container-emulated. The x64 consumer ran under emulation on an arm64 host. Emulation does not substitute for native-host evidence.")
    validate(record, read(DOTNET / "validation/record.schema.json"))
    write(record_path,record)
    return result.returncode

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="action",required=True)
    pin_default = str(ROOT/"packages/engine/runtime/engine-runtime-build.json")
    stage = sub.add_parser("stage-runtime", help="stage one RID's viewer host and PDFium as a runtime slice")
    stage.add_argument("--rid",choices=VIEWER_RIDS,required=True)
    stage.add_argument("--native-lib",required=True)
    stage.add_argument("--out",required=True)
    stage.add_argument("--runtime-manifest",default=pin_default)
    stage.add_argument("--runtime-archive")
    merge = sub.add_parser("pack-runtime", help="pack staged slices into one EmbedPDF.Runtime nupkg")
    merge.add_argument("--slice",action="append",required=True)
    merge.add_argument("--out",required=True)
    merge.add_argument("--version")
    merge.add_argument("--runtime-manifest",default=pin_default)
    facade = sub.add_parser("pack-facade", help="pack the EmbedPDF facade against a runtime package")
    facade.add_argument("--runtime-package",required=True)
    facade.add_argument("--out",required=True)
    facade.add_argument("--version",default="0.0.0-local")
    for action in ["smoke-runtime","validate-runtime"]:
        command = sub.add_parser(action, help="consume the runtime and facade packages from a local feed")
        command.add_argument("--rid",choices=VIEWER_RIDS,required=True)
        command.add_argument("--package",required=True)
        command.add_argument("--facade",required=True)
        command.add_argument("--manifest",required=True)
        command.add_argument("--runtime-manifest",default=pin_default)
        if action == "validate-runtime":
            command.add_argument("--record",required=True)
    for action in ["pack","validate","smoke"]:
        command = sub.add_parser(action)
        command.add_argument("--rid",choices=RIDS,required=True)
        command.add_argument("--runtime-manifest",default=str(ROOT/"packages/engine/runtime/engine-runtime-build.json"))
        command.add_argument("--font-manifest",default=str(DOTNET/"packaging/fonts.json"))
        if action == "pack":
            command.add_argument("--out",required=True)
            command.add_argument("--native-lib")
            command.add_argument("--runtime-archive")
            command.add_argument("--version",default="0.0.0-local")
        else:
            command.add_argument("--package")
            command.add_argument("--manifest")
            if action == "validate":
                command.add_argument("--record",required=True)
    args=parser.parse_args()
    if args.action in ("validate","smoke"):
        directory = Path(os.environ.get("EPDF_PACKAGE_DIR", DOTNET/"artifacts"/args.rid))
        args.package = args.package or str(directory/"EmbedPDF.Engine.0.0.0-local.nupkg")
        args.manifest = args.manifest or str(directory/f"package-manifest.{args.rid}.json")
    try:
        return {"pack":pack,"validate":validate_package,"smoke":smoke,
                "stage-runtime":stage_runtime,"pack-runtime":pack_runtime,"pack-facade":pack_facade,
                "smoke-runtime":smoke_runtime,"validate-runtime":validate_runtime}[args.action](args) or 0
    except (ValueError,RuntimeError,OSError,KeyError,tarfile.TarError,zipfile.BadZipFile) as error:
        print(f"ERROR: {error}",file=sys.stderr)
        return 1

if __name__ == "__main__":
    sys.exit(main())
