#!/usr/bin/env python3
"""Packs the artifacts android-consumer.sh staged as a Maven repository in a zip.

    package-maven-zip.py <staged-repo> <version> <out-dir>

<staged-repo> is the directory android-consumer.sh published to
(CONSUMER_MAVEN_REPO), holding com/embedpdf/<artifact>/<version>/. <version> is
the version of embedpdf, embedpdf-javascript, embedpdf-models and
embedpdf-compose; embedpdf-bindings carries its engine version,
0.0.0-engine.<hash>.

Writes <out-dir>/embedpdf-android-maven-<version>.zip and its .sha256, a line
`<hex>  <name>` that `sha256sum -c` reads. The zip's root is the repository
root, so an app unzips it into a directory and names that directory as a
Maven repository. Inside it, every artifact has a maven-metadata.xml in place
of Maven Local's maven-metadata-local.xml, and every file has the .md5, .sha1,
.sha256 and .sha512 a remote Maven repository serves beside it.

The zip is reproducible: entries are sorted, and every timestamp, including
the metadata's lastUpdated, is SOURCE_DATE_EPOCH (default: the commit time of
HEAD). Staging the same files twice gives the same bytes.

Exit 64: bad arguments. Exit 65: the staged repository isn't the five
artifacts at the expected versions.
"""

import hashlib
import os
import subprocess
import sys
import tempfile
import time
import zipfile
from pathlib import Path
from xml.sax.saxutils import escape

GROUP = "com/embedpdf"
GROUP_ID = "com.embedpdf"
PRODUCTS = ("embedpdf", "embedpdf-bindings", "embedpdf-compose", "embedpdf-javascript", "embedpdf-models")
ENGINE = "embedpdf-bindings"
CHECKSUMS = ("md5", "sha1", "sha256", "sha512")
SKIPPED = ("maven-metadata-local.xml",)


class StagingError(Exception):
    pass


def source_date_epoch(repo):
    value = os.environ.get("SOURCE_DATE_EPOCH")
    if value:
        return int(value)
    try:
        out = subprocess.run(
            ["git", "-C", str(repo), "log", "-1", "--format=%ct"], capture_output=True, text=True, check=True
        ).stdout.strip()
        return int(out)
    except (subprocess.CalledProcessError, ValueError, FileNotFoundError):
        return int(time.time())


def versions_of(group_dir, version):
    """{artifact: artifact version}, checked against PRODUCTS and <version>."""
    found = {}
    for artifact in sorted(p for p in group_dir.iterdir() if p.is_dir()):
        dirs = sorted(p.name for p in artifact.iterdir() if p.is_dir())
        if len(dirs) != 1:
            raise StagingError(f"{artifact.name} has {len(dirs)} versions staged ({', '.join(dirs)}); expected one")
        found[artifact.name] = dirs[0]
    if tuple(sorted(found)) != PRODUCTS:
        raise StagingError(f"staged {', '.join(sorted(found)) or 'nothing'}; expected {', '.join(PRODUCTS)}")
    for name, staged in found.items():
        if name == ENGINE:
            if not staged.startswith("0.0.0-engine."):
                raise StagingError(f"{ENGINE} is staged as {staged}, not 0.0.0-engine.<hash>")
        elif staged != version:
            raise StagingError(f"{name} is staged as {staged}, not {version}")
    return found


def metadata(artifact, version, stamp):
    updated = time.strftime("%Y%m%d%H%M%S", time.gmtime(stamp))
    return (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        "<metadata>\n"
        f"  <groupId>{GROUP_ID}</groupId>\n"
        f"  <artifactId>{escape(artifact)}</artifactId>\n"
        "  <versioning>\n"
        f"    <latest>{escape(version)}</latest>\n"
        f"    <release>{escape(version)}</release>\n"
        "    <versions>\n"
        f"      <version>{escape(version)}</version>\n"
        "    </versions>\n"
        f"    <lastUpdated>{updated}</lastUpdated>\n"
        "  </versioning>\n"
        "</metadata>\n"
    ).encode()


def entries(group_dir, found, stamp):
    """[(path in the zip, bytes)], sorted, with the metadata and checksums added."""
    files = {}
    for name, staged in found.items():
        for path in sorted((group_dir / name / staged).iterdir()):
            if path.is_file() and path.name not in SKIPPED and path.suffix.lstrip(".") not in CHECKSUMS:
                files[f"{GROUP}/{name}/{staged}/{path.name}"] = path.read_bytes()
        files[f"{GROUP}/{name}/maven-metadata.xml"] = metadata(name, staged, stamp)
    for path, data in list(files.items()):
        for algorithm in CHECKSUMS:
            files[f"{path}.{algorithm}"] = hashlib.new(algorithm, data).hexdigest().encode()
    return sorted(files.items())


def write_zip(target, items, stamp):
    # Zip timestamps are local-time fields with no zone and start in 1980.
    date_time = time.gmtime(max(stamp, 315532800))[:6]
    # Every parent directory of every file, so unzip tools that want
    # directory entries get them.
    directories = sorted({
        "/".join(path.split("/")[:depth]) + "/" for path, _ in items for depth in range(1, path.count("/") + 1)
    })
    with tempfile.NamedTemporaryFile(dir=target.parent, delete=False) as handle:
        partial = Path(handle.name)
    try:
        with zipfile.ZipFile(partial, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for directory in directories:
                info = zipfile.ZipInfo(directory, date_time)
                info.external_attr = (0o40755 << 16) | 0x10
                archive.writestr(info, b"")
            for path, data in items:
                info = zipfile.ZipInfo(path, date_time)
                info.external_attr = 0o100644 << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                archive.writestr(info, data)
        # NamedTemporaryFile makes the file 0600; a zip built in the dev
        # container must stay readable by the runner that uploads it.
        partial.chmod(0o644)
        partial.replace(target)
    except BaseException:
        partial.unlink(missing_ok=True)
        raise


def main(argv):
    if len(argv) != 4:
        print(__doc__.strip().splitlines()[2].strip(), file=sys.stderr)
        return 64
    staged, version, out = Path(argv[1]), argv[2], Path(argv[3])
    group_dir = staged / GROUP
    if not group_dir.is_dir() or not version or "/" in version:
        print(f"usage: package-maven-zip.py <staged-repo> <version> <out-dir>; no {GROUP} in {staged}", file=sys.stderr)
        return 64
    try:
        found = versions_of(group_dir, version)
    except StagingError as error:
        print(f"package-maven-zip.py: {error}", file=sys.stderr)
        return 65
    stamp = source_date_epoch(Path(__file__).resolve().parent)
    out.mkdir(parents=True, exist_ok=True)
    name = f"embedpdf-android-maven-{version}.zip"
    target = out / name
    write_zip(target, entries(group_dir, found, stamp), stamp)
    digest = hashlib.sha256(target.read_bytes()).hexdigest()
    (out / f"{name}.sha256").write_text(f"{digest}  {name}\n")
    print(f"{target}: {target.stat().st_size} bytes, sha256 {digest}")
    for artifact, artifact_version in sorted(found.items()):
        print(f"  {GROUP_ID}:{artifact}:{artifact_version}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
