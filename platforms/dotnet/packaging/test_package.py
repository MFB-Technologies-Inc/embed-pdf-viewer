import json
from pathlib import Path, PureWindowsPath
import tempfile
import unittest
import copy
import os
import platform
import subprocess
import sys
import hashlib
import zipfile

from schema import validate

class SchemaTests(unittest.TestCase):
    def test_record_rejects_dirty_source_without_patch_hash(self):
        root = Path(__file__).resolve().parents[1]
        schema = json.loads((root / "validation/record.schema.json").read_text())
        record = {"schemaVersion":1,"slice":6,"rid":"linux-arm64",
            "source":{"commit":"a"*40,"dirty":True,"runtimeFork":"b"*40},
            "environment":{"kind":"container","os":"Linux","arch":"arm64","emulated":False,
                "tools":{},"imageId":"sha256:"+"c"*64},
            "inputs":[],"commands":[{"command":"test","cwd":".","exitCode":0,"results":"pass"}],
            "artifacts":[],"limitations":[]}
        with self.assertRaisesRegex(ValueError, "diffSha256"):
            validate(record, schema)
        record["source"]["diffSha256"] = "d"*64
        validate(record, schema)

    def test_record_rejects_container_without_digest_and_unknown_fields(self):
        with self.assertRaises(ValueError):
            validate({"kind":"container"}, {"type":"object","additionalProperties":False,"properties":{}})
        with self.assertRaises(ValueError):
            validate(True, {"type":"integer"})

class ManifestTests(unittest.TestCase):
    def test_emulated_status_is_distinct_and_only_linux_x64(self):
        root = Path(__file__).resolve().parents[1]
        schema = json.loads((root/"package-manifest.schema.json").read_text())
        states = schema["properties"]["rids"]["properties"]
        validate({"status":"container-emulated","reason":"No native x64 run"}, states["linux-x64"])
        for rid in ["linux-arm64","osx-arm64","osx-x64","win-x64"]:
            with self.assertRaises(ValueError):
                validate({"status":"container-emulated","reason":"No native run"}, states[rid])

class SingleRidTests(unittest.TestCase):
    def setUp(self):
        from package import RIDS
        self.root = Path(__file__).resolve().parents[1]
        self.schema = json.loads((self.root / "package-manifest.schema.json").read_text())
        file = {"path":"package.nupkg","sha256":"a"*64,"bytes":1}
        self.manifest = {"schemaVersion":1,"packageId":"EmbedPDF.Engine","version":"0.0.0-local",
            "source":{"commit":"a"*40,"dirty":False},"runtimeFork":"b"*40,
            "runtimeManifest":file,"fontManifest":file,"abi":1,
            "rids":{rid:{"status":"PACKED_UNVALIDATED" if rid == "linux-arm64" else "UNVALIDATED",
                         "reason":"test evidence"} for rid in RIDS},"files":[file],"limitations":[]}

    def test_schema_rejects_success_claims_for_other_rids(self):
        for rid in ["osx-x64", "win-x64", "linux-x64"]:
            for status in ["VALIDATED", "PACKED_UNVALIDATED"]:
                forged = copy.deepcopy(self.manifest)
                forged["rids"][rid]["status"] = status
                with self.subTest(rid=rid, status=status), self.assertRaises(ValueError):
                    validate(forged, self.schema)
        forged = copy.deepcopy(self.manifest)
        forged["rids"]["linux-x64"]["status"] = "container-emulated"
        with self.assertRaises(ValueError):
            validate(forged, self.schema)
        validate(self.manifest, self.schema)

    def test_schema_rejects_empty_file_evidence(self):
        self.manifest["files"] = []
        with self.assertRaises(ValueError):
            validate(self.manifest, self.schema)

    def run_tool(self, manifest, identity, action="smoke", empty_files=False):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            package = directory / "package.nupkg"
            with zipfile.ZipFile(package, "w") as bundle:
                bundle.writestr("runtimes/linux-arm64/native/epdf-assets.json", json.dumps(identity))
            manifest = copy.deepcopy(manifest)
            manifest["files"] = [{"path":package.name,"sha256":hashlib.sha256(package.read_bytes()).hexdigest(),"bytes":package.stat().st_size}]
            if empty_files:
                manifest["files"] = []
            path = directory / "manifest.json"
            path.write_text(json.dumps(manifest))
            command = [sys.executable, str(self.root / "packaging/package.py"), action,
                "--rid", "linux-arm64", "--package", str(package), "--manifest", str(path)]
            if action == "validate":
                command += ["--record", str(directory / "record.json")]
            env = dict(os.environ, EPDF_IMAGE_ID="sha256:"+"c"*64)
            return subprocess.run(command, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)

    def identity(self):
        return {key:self.manifest[key] for key in ["source","runtimeFork","abi"]} | {"rid":"linux-arm64","files":[]}

    @unittest.skipUnless(platform.system() == "Linux" and platform.machine() == "aarch64", "CLI regression requires native Linux ARM64 author container")
    def test_actual_tool_rejects_forged_claims_and_empty_assets(self):
        forged = copy.deepcopy(self.manifest)
        forged["rids"]["osx-x64"]["status"] = "VALIDATED"
        for action in ["smoke", "validate"]:
            result = self.run_tool(forged, self.identity(), action)
            self.assertNotEqual(0, result.returncode)
            self.assertIn("rids.osx-x64.status", result.stdout)
        result = self.run_tool(self.manifest, self.identity())
        self.assertNotEqual(0, result.returncode)
        self.assertIn("native asset inventory", result.stdout)
        result = self.run_tool(self.manifest, self.identity(), empty_files=True)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("files: too few items", result.stdout)

    @unittest.skipUnless(platform.system() == "Linux" and platform.machine() == "aarch64", "CLI regression requires native Linux ARM64 author container")
    def test_actual_tool_rejects_mismatched_rid(self):
        identity = self.identity() | {"rid":"win-x64"}
        result = self.run_tool(self.manifest, identity)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("packaged RID identity", result.stdout)

class RuntimeTests(unittest.TestCase):
    """EmbedPDF.Runtime: several RIDs in one package, staged slices, the viewer host."""
    def setUp(self):
        from package import RIDS
        self.root = Path(__file__).resolve().parents[1]
        self.schema = json.loads((self.root / "package-manifest.schema.json").read_text())
        self.records = json.loads((self.root / "validation/record.schema.json").read_text())
        file = {"path":"package.nupkg","sha256":"a"*64,"bytes":1}
        self.manifest = {"schemaVersion":1,"packageId":"EmbedPDF.Runtime","version":"0.0.0-engine."+"0"*16,
            "source":{"commit":"a"*40,"dirty":False},"runtimeFork":"b"*40,"runtimeManifest":file,"abi":1,
            "rids":{rid:{"status":"PACKED_UNVALIDATED" if rid in ("linux-arm64","linux-x64") else "UNVALIDATED",
                         "reason":"test evidence"} for rid in RIDS},"files":[file],"limitations":[]}
        self.record = {"schemaVersion":1,"package":"EmbedPDF.Runtime","phase":1,"rid":"linux-arm64",
            "source":{"commit":"a"*40,"dirty":False,"runtimeFork":"b"*40},
            "environment":{"kind":"container","os":"Linux","arch":"arm64","emulated":False,
                "tools":{},"imageId":"sha256:"+"c"*64},
            "inputs":[],"commands":[{"command":"test","cwd":".","exitCode":0,"results":"pass"}],
            "artifacts":[],"limitations":[]}

    def test_runtime_manifest_allows_several_packed_rids(self):
        validate(self.manifest, self.schema)

    def test_engine_manifest_rule_unchanged(self):
        forged = copy.deepcopy(self.manifest)
        forged["packageId"] = "EmbedPDF.Engine"
        forged["fontManifest"] = forged["runtimeManifest"]
        with self.assertRaises(ValueError):
            validate(forged, self.schema)
        forged.pop("fontManifest")
        forged["rids"]["linux-x64"]["status"] = "UNVALIDATED"
        with self.assertRaisesRegex(ValueError, "fontManifest"):
            validate(forged, self.schema)

    def test_unknown_package_id_rejected(self):
        self.manifest["packageId"] = "EmbedPDF.Other"
        with self.assertRaises(ValueError):
            validate(self.manifest, self.schema)

    def test_record_requires_phase_for_runtime(self):
        validate(self.record, self.records)
        record = copy.deepcopy(self.record)
        record.pop("phase")
        with self.assertRaisesRegex(ValueError, "phase"):
            validate(record, self.records)
        record.pop("package")
        with self.assertRaisesRegex(ValueError, "slice"):
            validate(record, self.records)
        record["slice"] = 6
        validate(record, self.records)

    def test_viewer_host_needs_origin_runpath(self):
        from package import check_elf
        with self.assertRaisesRegex(ValueError, "relative"):
            check_elf("libepdf_viewer_host.so", "(RUNPATH) Library runpath: [/workspaces/x/lib]", "")
        with self.assertRaisesRegex(ValueError, "relative"):
            check_elf("libepdf_viewer_host.so", "(NEEDED) Shared library: [libembedpdf.so]", "")
        check_elf("libepdf_viewer_host.so", "(RUNPATH) Library runpath: [$ORIGIN]", "")

    def test_viewer_exports(self):
        from package import check_viewer_exports
        with self.assertRaisesRegex(ValueError, "contract_version"):
            check_viewer_exports("epdf_abi_version")
        check_viewer_exports("0000 T _ffi_epdf_viewer_host_uniffi_contract_version", "_")

    def slice(self, directory, rid="linux-arm64", commit="a"*40, dirty=False):
        native = directory / "runtimes" / rid / "native"
        native.mkdir(parents=True)
        (native / "libepdf_viewer_host.so").write_bytes(b"host")
        files = [{"path":f"runtimes/{rid}/native/libepdf_viewer_host.so",
                  "sha256":hashlib.sha256(b"host").hexdigest(),"bytes":4}]
        pin = json.loads((self.root.parents[1] / "packages/engine/runtime/engine-runtime-build.json").read_text())
        source = {"commit":commit,"dirty":dirty} | ({"diffSha256":"e"*64} if dirty else {})
        (directory / "runtime-slice.json").write_text(json.dumps({"schemaVersion":1,"rid":rid,
            "source":source,"runtimeFork":pin["sha"],"runtimeArchiveSha256":"c"*64,
            "runtimeTarget":rid,"files":files}))
        return native

    def pack_runtime(self, *slices, extra=()):
        with tempfile.TemporaryDirectory() as out:
            command = [sys.executable, str(self.root / "packaging/package.py"), "pack-runtime",
                       "--version", "0.0.0-engine."+"0"*16, "--out", out, *extra]
            for directory in slices:
                command += ["--slice", str(directory)]
            return subprocess.run(command, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)

    def test_pack_runtime_rejects_edited_slice(self):
        with tempfile.TemporaryDirectory() as temporary:
            native = self.slice(Path(temporary))
            (native / "libepdf_viewer_host.so").write_bytes(b"HOST")
            result = self.pack_runtime(Path(temporary))
            self.assertNotEqual(0, result.returncode)
            self.assertIn("slice checksum mismatch: runtimes/linux-arm64/native/libepdf_viewer_host.so", result.stdout)

    def test_pack_runtime_rejects_unrecorded_file(self):
        with tempfile.TemporaryDirectory() as temporary:
            native = self.slice(Path(temporary))
            (native / "extra.so").write_bytes(b"x")
            result = self.pack_runtime(Path(temporary))
            self.assertNotEqual(0, result.returncode)
            self.assertIn("unrecorded file", result.stdout)

    def test_pack_runtime_rejects_dirty_slice_unless_allowed(self):
        with tempfile.TemporaryDirectory() as temporary:
            self.slice(Path(temporary), dirty=True)
            result = self.pack_runtime(Path(temporary))
            self.assertNotEqual(0, result.returncode)
            self.assertIn("staged from a dirty tree", result.stdout)
            result = self.pack_runtime(Path(temporary), extra=["--allow-dirty"])
            self.assertNotEqual(0, result.returncode)
            self.assertNotIn("dirty", result.stdout)

    def test_pack_runtime_checks_archive_against_pin(self):
        with tempfile.TemporaryDirectory() as temporary:
            self.slice(Path(temporary))
            result = self.pack_runtime(Path(temporary))
            self.assertNotEqual(0, result.returncode)
            self.assertIn("linux-arm64: runtime archive differs from the pinned manifest", result.stdout)

    def test_slice_paths_are_package_paths_on_any_os(self):
        from package import package_path
        self.assertEqual("runtimes/win-x64/native/EmbedPDF-LICENSE",
            package_path(PureWindowsPath(r"C:\s\runtimes\win-x64\native\EmbedPDF-LICENSE"), PureWindowsPath(r"C:\s")))

    def test_pack_runtime_rejects_backslash_slice_paths(self):
        with tempfile.TemporaryDirectory() as temporary:
            self.slice(Path(temporary))
            record_path = Path(temporary) / "runtime-slice.json"
            record = json.loads(record_path.read_text())
            record["files"][0]["path"] = record["files"][0]["path"].replace("/", "\\")
            record_path.write_text(json.dumps(record))
            result = self.pack_runtime(Path(temporary))
            self.assertNotEqual(0, result.returncode)
            self.assertIn("slice paths must use forward slashes", result.stdout)

    def test_name_collisions_are_case_insensitive(self):
        from package import check_name_collisions
        with self.assertRaisesRegex(ValueError, "EmbedPDF.dll.*embedpdf.dll"):
            check_name_collisions(["lib/net10.0/EmbedPDF.dll"], ["runtimes/win-x64/native/embedpdf.dll"])
        check_name_collisions(["lib/net10.0/EmbedPDF.Viewer.dll"], ["runtimes/win-x64/native/embedpdf.dll"])

    def test_facade_assembly_does_not_collide_with_native_files(self):
        # A RID-specific build copies managed and native files into one
        # directory; Windows compares names without case.
        import re
        from package import VIEWER_RIDS, check_name_collisions
        csproj = (self.root / "EmbedPDF.Viewer/EmbedPDF.Viewer.csproj").read_text()
        assembly = re.search(r"<AssemblyName>(.*?)</AssemblyName>", csproj).group(1)
        native = [name for _, _, host, pdfium in VIEWER_RIDS.values() for name in (host, Path(pdfium).name)]
        check_name_collisions([f"lib/net10.0/{assembly}.dll"], native)

    def test_pack_runtime_rejects_duplicate_rid_and_mixed_sources(self):
        with tempfile.TemporaryDirectory() as first, tempfile.TemporaryDirectory() as second:
            self.slice(Path(first))
            self.slice(Path(second))
            result = self.pack_runtime(Path(first), Path(second))
            self.assertNotEqual(0, result.returncode)
            self.assertIn("two slices for linux-arm64", result.stdout)
        with tempfile.TemporaryDirectory() as first, tempfile.TemporaryDirectory() as second:
            self.slice(Path(first))
            self.slice(Path(second), rid="linux-x64", commit="d"*40)
            result = self.pack_runtime(Path(first), Path(second))
            self.assertNotEqual(0, result.returncode)
            self.assertIn("different source commits", result.stdout)

    def smoke_runtime(self, manifest, rid="linux-arm64", emulated=False):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            package = directory / "EmbedPDF.Runtime.0.0.0-engine.0000000000000000.nupkg"
            with zipfile.ZipFile(package, "w") as bundle:
                bundle.writestr(f"runtimes/{rid}/native/epdf-assets.json", "{}")
            facade = directory / "EmbedPDF.0.0.0-local.nupkg"
            facade.write_bytes(b"facade")
            manifest = copy.deepcopy(manifest)
            manifest["files"] = [{"path":package.name,"sha256":hashlib.sha256(package.read_bytes()).hexdigest(),"bytes":package.stat().st_size}]
            path = directory / "package-manifest.runtime.json"
            path.write_text(json.dumps(manifest))
            command = [sys.executable, str(self.root / "packaging/package.py"), "smoke-runtime", "--rid", rid,
                       "--package", str(package), "--facade", str(facade), "--manifest", str(path)]
            env = dict(os.environ, EPDF_IMAGE_ID="sha256:"+"c"*64, EPDF_EMULATED="true" if emulated else "false")
            return subprocess.run(command, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)

    @unittest.skipUnless(platform.system() == "Linux" and platform.machine() == "aarch64", "CLI regression requires native Linux ARM64 author container")
    def test_smoke_runtime_rejects_unpacked_rid(self):
        manifest = copy.deepcopy(self.manifest)
        manifest["rids"]["linux-arm64"]["status"] = "UNVALIDATED"
        result = self.smoke_runtime(manifest)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("no packed artifact", result.stdout)

    @unittest.skipUnless(platform.system() == "Linux" and platform.machine() == "aarch64", "CLI regression requires native Linux ARM64 author container")
    def test_smoke_runtime_rejects_foreign_rid_and_misplaced_emulation(self):
        result = self.smoke_runtime(self.manifest, rid="osx-arm64")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("differs from requested osx-arm64", result.stdout)
        result = self.smoke_runtime(self.manifest, emulated=True)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("emulation is recorded only for linux-x64", result.stdout)

class NativeInspectionTests(unittest.TestCase):
    def test_consumer_inputs_exist(self):
        from package import FIXTURE, FIXTURE_PAGES, SCRUBBED_ENV
        self.assertTrue(FIXTURE.is_file())
        self.assertEqual(14, FIXTURE_PAGES)
        self.assertIn("LD_LIBRARY_PATH", SCRUBBED_ENV)

    def test_runner_image_is_recorded_from_the_hosted_runner(self):
        from package import runner_image
        self.assertEqual("ubuntu24 20261115.1", runner_image({"ImageOS":"ubuntu24","ImageVersion":"20261115.1"}))
        self.assertIsNone(runner_image({}))

    def test_foreign_rid_uses_its_own_loader(self):
        import package
        calls = []
        original = (package.host_rid, package.run)
        package.host_rid = lambda: "linux-arm64"
        package.run = lambda command, cwd=None, env=None: calls.append((command, env)) or ""
        try:
            # A Windows-style path: a foreign RID is always Linux, so the loader
            # gets forward slashes whatever OS runs the inspection.
            package.list_dependencies(PureWindowsPath(r"\x\libepdf_viewer_host.so"), "linux-x64")
            package.list_dependencies(PureWindowsPath(r"\x\libepdf_viewer_host.so"), "linux-arm64")
        finally:
            package.host_rid, package.run = original
        self.assertEqual(["/lib64/ld-linux-x86-64.so.2", "/x/libepdf_viewer_host.so"], calls[0][0])
        self.assertEqual("1", calls[0][1]["LD_TRACE_LOADED_OBJECTS"])
        self.assertEqual(["ldd", "-r", "/x/libepdf_viewer_host.so"], calls[1][0])

    def test_rejects_wrong_or_incomplete_abi_exports(self):
        from package import check_exports
        with self.assertRaisesRegex(ValueError, "epdf_abi_version"):
            check_exports("FPDF_InitLibrary")
        with self.assertRaisesRegex(ValueError, "epdf_engine_create"):
            check_exports("000000 T _epdf_abi_version", "_")

    def test_rejects_build_machine_rpath(self):
        from package import check_elf
        with self.assertRaisesRegex(ValueError, "relative"):
            check_elf("libepdf_capi.so", "Dynamic section: (RUNPATH) Library runpath: [/home/build/pdfium]", "")
        check_elf("libepdf_capi.so", "Dynamic section: (RUNPATH) Library runpath: [$ORIGIN]", "")

    def test_rejects_missing_dependency_and_absolute_dependency(self):
        from package import check_elf
        with self.assertRaisesRegex(ValueError, "missing"):
            check_elf("libepdf_capi.so", "(RUNPATH) Library runpath: [$ORIGIN]", "libembedpdf.so => not found")
        with self.assertRaisesRegex(ValueError, "absolute"):
            check_elf("libepdf_capi.so", "(NEEDED) Shared library: [/build/libembedpdf.so]\n(RUNPATH) Library runpath: [$ORIGIN]", "")

class LibraryPackTests(unittest.TestCase):
    """EmbedPDF.Models, EmbedPDF.Extensions.DependencyInjection and EmbedPDF.Scripting.Jint."""
    def nupkg(self, directory, package_id, dependency_version, files=None):
        path = Path(directory) / f"{package_id}.0.0.0-local.nupkg"
        files = [f"lib/net10.0/{package_id}.dll"] if files is None else files
        nuspec = ('<?xml version="1.0" encoding="utf-8"?><package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">'
                  f'<metadata><id>{package_id}</id><version>0.0.0-local</version><dependencies><group targetFramework="net10.0">'
                  f'<dependency id="EmbedPDF" version="{dependency_version}" exclude="Build,Analyzers" /></group></dependencies></metadata></package>')
        with zipfile.ZipFile(path, "w") as bundle:
            bundle.writestr(f"{package_id}.nuspec", nuspec)
            for name in files:
                bundle.writestr(name, b"MZ")
        return path

    def test_dependency_range_reads_nuspec(self):
        from libraries import dependency_range
        nuspec = '<package><metadata><dependencies><dependency id="EmbedPDF" version="[1.2.3]" /></dependencies></metadata></package>'
        self.assertEqual("[1.2.3]", dependency_range(nuspec, "EmbedPDF"))
        self.assertIsNone(dependency_range(nuspec, "EmbedPDF.Runtime"))

    def test_models_dependency_must_be_exact(self):
        from libraries import check_library
        with tempfile.TemporaryDirectory() as temporary:
            loose = self.nupkg(temporary, "EmbedPDF.Models", "0.0.0-local")
            with self.assertRaisesRegex(ValueError, "exact"):
                check_library(loose, "EmbedPDF.Models", "0.0.0-local", exact=True)
            check_library(self.nupkg(temporary, "EmbedPDF.Models", "[0.0.0-local]"), "EmbedPDF.Models", "0.0.0-local", exact=True)
            with self.assertRaisesRegex(ValueError, "exact"):
                check_library(self.nupkg(temporary, "EmbedPDF.Models", "[0.0.0-local, )"), "EmbedPDF.Models", "0.0.0-local", exact=True)

    def test_minimum_dependency_for_extensions(self):
        from libraries import check_library
        with tempfile.TemporaryDirectory() as temporary:
            for version in ["0.0.0-local", "[0.0.0-local]"]:
                check_library(self.nupkg(temporary, "EmbedPDF.Extensions.DependencyInjection", version),
                              "EmbedPDF.Extensions.DependencyInjection", "0.0.0-local", exact=False)
            with self.assertRaisesRegex(ValueError, "EmbedPDF"):
                check_library(self.nupkg(temporary, "EmbedPDF.Scripting.Jint", "0.0.1"),
                              "EmbedPDF.Scripting.Jint", "0.0.0-local", exact=False)

    def test_library_must_carry_its_assembly(self):
        from libraries import check_library
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaisesRegex(ValueError, "lib/net10.0"):
                check_library(self.nupkg(temporary, "EmbedPDF.Models", "[0.0.0-local]", files=[]),
                              "EmbedPDF.Models", "0.0.0-local", exact=True)

    def test_library_must_not_carry_the_facade(self):
        from libraries import check_library
        with tempfile.TemporaryDirectory() as temporary:
            files = ["lib/net10.0/EmbedPDF.Models.dll", "lib/net10.0/EmbedPDF.Viewer.dll"]
            with self.assertRaisesRegex(ValueError, "EmbedPDF.Viewer.dll"):
                check_library(self.nupkg(temporary, "EmbedPDF.Models", "[0.0.0-local]", files=files),
                              "EmbedPDF.Models", "0.0.0-local", exact=True)

    def test_facade_version_comes_from_the_file_name(self):
        from libraries import facade_version
        self.assertEqual("0.0.0-local", facade_version(Path("/x/EmbedPDF.0.0.0-local.nupkg")))
        with self.assertRaisesRegex(ValueError, "EmbedPDF"):
            facade_version(Path("/x/EmbedPDF.Models.0.0.0-local.nupkg"))

class PngCheckTests(unittest.TestCase):
    """The console sample's PNG output, checked without an image library."""
    @staticmethod
    def png(width, height, ihdr_height=None, rows=None):
        import struct, zlib
        def chunk(kind, body):
            return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xffffffff)
        raw = rows if rows is not None else b"".join(b"\0" + b"\x10\x20\x30\xff" * width for _ in range(height))
        header = struct.pack(">IIBBBBB", width, ihdr_height or height, 8, 6, 0, 0, 0)
        return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b"")

    def test_valid_png_passes(self):
        from png_check import check_png
        check_png(self.png(2, 1), 2, 1)

    def test_bad_crc_fails(self):
        from png_check import check_png
        data = bytearray(self.png(2, 1))
        data[29] ^= 0xff  # last byte of the IHDR CRC
        with self.assertRaisesRegex(ValueError, "CRC"):
            check_png(bytes(data), 2, 1)

    def test_wrong_dimensions_fail(self):
        from png_check import check_png
        with self.assertRaisesRegex(ValueError, "IHDR"):
            check_png(self.png(2, 1), 2, 2)

    def test_short_image_data_fails(self):
        from png_check import check_png
        with self.assertRaisesRegex(ValueError, "size"):
            check_png(self.png(2, 2, rows=b"\0" + b"\0" * 8), 2, 2)

    def test_bad_filter_byte_fails(self):
        from png_check import check_png
        with self.assertRaisesRegex(ValueError, "filter"):
            check_png(self.png(1, 1, rows=b"\x07" + b"\0" * 4), 1, 1)

if __name__ == "__main__":
    unittest.main()
