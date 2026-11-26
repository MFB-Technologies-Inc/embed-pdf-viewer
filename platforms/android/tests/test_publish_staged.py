"""publish-staged.sh against a local registry stand-in.

Run: python3 -m unittest platforms/android/tests/test_publish_staged.py
"""

import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import os
from pathlib import Path
import subprocess
import tempfile
import threading
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / 'publish-staged.sh'
ENGINE = 'embedpdf-bindings/0.0.0-engine.abc'
LIBRARY = 'embedpdf/1.0.0'


class Registry(BaseHTTPRequestHandler):
    """Answers GET with 200 for a path in `existing`, else `missing`, and keeps every PUT."""

    existing = set()
    missing = 404
    puts = []
    auth = []

    def do_GET(self):
        self.auth.append(self.headers.get('Authorization'))
        self.send_response(200 if self.path in self.existing else self.missing)
        self.end_headers()

    def do_PUT(self):
        body = self.rfile.read(int(self.headers['Content-Length']))
        self.puts.append((self.path, body))
        self.send_response(201)
        self.end_headers()

    def log_message(self, *args):
        pass


class PublishStagedTests(unittest.TestCase):
    def setUp(self):
        Registry.existing = set()
        Registry.missing = 404
        Registry.puts = []
        Registry.auth = []
        self.server = ThreadingHTTPServer(('127.0.0.1', 0), Registry)
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.directory = tempfile.TemporaryDirectory()
        self.group = Path(self.directory.name) / 'com/embedpdf'
        for entry in (ENGINE, LIBRARY):
            name, version = entry.split('/')
            folder = self.group / entry
            folder.mkdir(parents=True)
            for suffix in ('.pom', '.aar', '.module'):
                (folder / f'{name}-{version}{suffix}').write_text(f'{entry}{suffix}')
            (self.group / name / 'maven-metadata-local.xml').write_text('local')

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.directory.cleanup()

    def publish(self):
        env = dict(
            os.environ,
            GITHUB_ACTOR='actor',
            GITHUB_TOKEN='secret',
            MAVEN_REGISTRY_URL=f'http://127.0.0.1:{self.server.server_address[1]}',
        )
        return subprocess.run(['bash', str(SCRIPT), str(self.group), 'owner/repo'], env=env, capture_output=True, text=True)

    def paths(self):
        return [path for path, _ in Registry.puts]

    def test_every_file_goes_up_with_its_checksums_and_the_pom_last(self):
        result = self.publish()
        self.assertEqual(0, result.returncode, result.stderr)
        base = '/owner/repo/com/embedpdf/embedpdf/1.0.0/embedpdf-1.0.0'
        library = [path for path in self.paths() if path.startswith(base)]
        expected = []
        for suffix in ('.aar', '.module', '.pom'):
            expected.append(base + suffix)
            expected += [f'{base}{suffix}.{algorithm}' for algorithm in ('md5', 'sha1', 'sha256', 'sha512')]
        self.assertEqual(expected, library)
        uploaded = dict(Registry.puts)
        self.assertEqual(b'embedpdf/1.0.0.aar', uploaded[base + '.aar'])
        self.assertEqual(hashlib.sha1(b'embedpdf/1.0.0.aar').hexdigest().encode(), uploaded[base + '.aar.sha1'])
        self.assertEqual(30, len(Registry.puts))
        self.assertFalse(any('maven-metadata' in path for path in self.paths()))
        self.assertTrue(all(header and header.startswith('Basic ') for header in Registry.auth))
        self.assertNotIn('secret', result.stdout + result.stderr)

    def test_a_published_engine_is_skipped(self):
        Registry.existing = {f'/owner/repo/com/embedpdf/{ENGINE}/embedpdf-bindings-0.0.0-engine.abc.pom'}
        result = self.publish()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn('skipping embedpdf-bindings', result.stdout)
        self.assertFalse(any('embedpdf-bindings' in path for path in self.paths()))
        self.assertEqual(15, len(Registry.puts))

    def test_a_published_library_version_uploads_nothing(self):
        Registry.existing = {f'/owner/repo/com/embedpdf/{LIBRARY}/embedpdf-1.0.0.pom'}
        result = self.publish()
        self.assertEqual(1, result.returncode)
        self.assertIn('already published', result.stderr)
        self.assertEqual([], Registry.puts)

    def test_an_unreadable_registry_uploads_nothing(self):
        Registry.missing = 401
        result = self.publish()
        self.assertEqual(1, result.returncode)
        self.assertIn('HTTP 401', result.stderr)
        self.assertEqual([], Registry.puts)


if __name__ == '__main__':
    unittest.main()
