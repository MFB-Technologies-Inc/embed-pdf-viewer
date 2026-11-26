"""package-maven-zip.py on a staged repository of stand-in files.

Run: python3 -m unittest platforms/android/tests/test_package_maven_zip.py
"""

import hashlib
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile

SCRIPT = Path(__file__).resolve().parents[1] / 'package-maven-zip.py'
VERSION = '0.0.0-android.7'
ENGINE = '0.0.0-engine.0123456789abcdef'
NAME = f'embedpdf-android-maven-{VERSION}.zip'


class PackageMavenZipTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.root = Path(self.directory.name)
        self.staged = self.root / 'staged'
        for name in ('embedpdf', 'embedpdf-compose', 'embedpdf-javascript', 'embedpdf-models', 'embedpdf-bindings'):
            self.stage(name, ENGINE if name == 'embedpdf-bindings' else VERSION)
        folder = self.staged / f'com/embedpdf/embedpdf-bindings/{ENGINE}'
        (folder / f'embedpdf-bindings-{ENGINE}-native-debug-symbols.zip').write_bytes(b'symbols')

    def tearDown(self):
        self.directory.cleanup()

    def stage(self, name, version):
        folder = self.staged / f'com/embedpdf/{name}/{version}'
        folder.mkdir(parents=True)
        for suffix in ('.pom', '.aar', '.module'):
            (folder / f'{name}-{version}{suffix}').write_text(f'{name} {version}{suffix}')
        (folder.parent / 'maven-metadata-local.xml').write_text('local')

    def pack(self, out='out', version=VERSION, epoch='1764115200'):
        env = dict(os.environ, SOURCE_DATE_EPOCH=epoch)
        return subprocess.run(
            [sys.executable, str(SCRIPT), str(self.staged), version, str(self.root / out)],
            env=env, capture_output=True, text=True,
        )

    def test_the_zip_is_a_maven_repository(self):
        result = self.pack()
        self.assertEqual(result.returncode, 0, result.stderr)
        archive = zipfile.ZipFile(self.root / 'out' / NAME)
        names = set(archive.namelist())
        pom = f'com/embedpdf/embedpdf/{VERSION}/embedpdf-{VERSION}.pom'
        self.assertIn(pom, names)
        self.assertIn(f'com/embedpdf/embedpdf-bindings/{ENGINE}/embedpdf-bindings-{ENGINE}.aar', names)
        self.assertIn(f'com/embedpdf/embedpdf-bindings/{ENGINE}/embedpdf-bindings-{ENGINE}-native-debug-symbols.zip', names)
        self.assertFalse([n for n in names if 'maven-metadata-local' in n])
        metadata = archive.read('com/embedpdf/embedpdf-compose/maven-metadata.xml').decode()
        self.assertIn(f'<release>{VERSION}</release>', metadata)
        self.assertIn('<lastUpdated>20251126000000</lastUpdated>', metadata)
        data = archive.read(pom)
        self.assertEqual(archive.read(pom + '.sha256').decode(), hashlib.sha256(data).hexdigest())
        self.assertEqual(archive.read(pom + '.sha1').decode(), hashlib.sha1(data).hexdigest())
        self.assertIn('com/embedpdf/', names)

    def test_the_sha256_file_names_the_zip(self):
        self.assertEqual(self.pack().returncode, 0)
        out = self.root / 'out'
        digest = hashlib.sha256((out / NAME).read_bytes()).hexdigest()
        self.assertEqual((out / f'{NAME}.sha256').read_text(), f'{digest}  {NAME}\n')

    def test_the_zip_is_reproducible(self):
        self.assertEqual(self.pack('a').returncode, 0)
        self.assertEqual(self.pack('b').returncode, 0)
        self.assertEqual((self.root / 'a' / NAME).read_bytes(), (self.root / 'b' / NAME).read_bytes())

    def test_a_missing_artifact_fails(self):
        for path in sorted((self.staged / 'com/embedpdf/embedpdf-javascript').rglob('*'), reverse=True):
            path.unlink() if path.is_file() else path.rmdir()
        (self.staged / 'com/embedpdf/embedpdf-javascript').rmdir()
        result = self.pack()
        self.assertEqual(result.returncode, 65)
        self.assertIn('expected embedpdf, embedpdf-bindings', result.stderr)
        self.assertFalse((self.root / 'out' / NAME).exists())

    def test_a_library_at_another_version_fails(self):
        result = self.pack(version='0.0.0-android.8')
        self.assertEqual(result.returncode, 65)
        self.assertIn(f'embedpdf is staged as {VERSION}, not 0.0.0-android.8', result.stderr)

    def test_two_versions_of_one_artifact_fail(self):
        self.stage('embedpdf-models', '0.0.0-android.6')
        result = self.pack()
        self.assertEqual(result.returncode, 65)
        self.assertIn('embedpdf-models has 2 versions staged', result.stderr)

    def test_no_group_directory_is_a_usage_error(self):
        result = subprocess.run(
            [sys.executable, str(SCRIPT), str(self.root / 'nowhere'), VERSION, str(self.root / 'out')],
            capture_output=True, text=True,
        )
        self.assertEqual(result.returncode, 64)


if __name__ == '__main__':
    unittest.main()
