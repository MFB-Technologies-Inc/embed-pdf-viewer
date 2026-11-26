"""Exit codes of check-android-engine.sh, with curl and gh replaced by stubs.

Run: python3 -m unittest platforms/android/tests/test_check_android_engine.py
"""

import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ANDROID = Path(__file__).resolve().parents[1]
SCRIPT = ANDROID / 'check-android-engine.sh'

# The stubs record their arguments and answer as the test says: curl prints
# CURL_STATUS as the HTTP code, or fails when it's empty; gh prints GH_AUTH as
# its token, or fails when it's empty.
CURL = '''#!/usr/bin/env bash
echo "$*" >> "$STUB_LOG"
[[ -n "$CURL_STATUS" ]] || { echo "curl: (6) Could not resolve host" >&2; exit 6; }
printf '%s' "$CURL_STATUS"
'''
GH = '''#!/usr/bin/env bash
[[ "$*" == "auth token" && -n "$GH_AUTH" ]] || exit 1
echo "$GH_AUTH"
'''


class CheckAndroidEngineTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.hash = subprocess.run(
            ['bash', str(ANDROID / 'android-engine-hash.sh')], check=True, capture_output=True, text=True
        ).stdout.strip()

    def run_check(self, pin, curl_status='', token='', gh_auth=''):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name, body in (('curl', CURL), ('gh', GH)):
                (root / name).write_text(body)
                (root / name).chmod(0o755)
            properties = root / 'gradle.properties'
            properties.write_text(f'embedpdf.engineRelease={pin}\n')
            log = root / 'curl.log'
            log.touch()
            env = {k: v for k, v in os.environ.items() if k not in ('GH_TOKEN', 'GITHUB_TOKEN')}
            env.update(PATH=f'{root}:{env["PATH"]}', STUB_LOG=str(log), CURL_STATUS=curl_status, GH_AUTH=gh_auth)
            if token:
                env['GH_TOKEN'] = token
            result = subprocess.run(
                ['bash', str(SCRIPT), '--properties', str(properties)], env=env, capture_output=True, text=True
            )
            return result, log.read_text()

    def test_a_current_pin_asks_nothing(self):
        result, calls = self.run_check(f'android-engine-{self.hash}')
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(f'current android-engine-{self.hash}\n', result.stdout)
        self.assertEqual('', calls)

    def test_a_stale_pin_asks_for_the_bindings_pom(self):
        result, calls = self.run_check('android-engine-0000000000000000', curl_status='404', token='t')
        self.assertEqual(1, result.returncode, result.stderr)
        version = f'0.0.0-engine.{self.hash}'
        self.assertIn(
            'https://maven.pkg.github.com/MFB-Technologies-Inc/embed-pdf-runtime/com/embedpdf/'
            f'embedpdf-bindings/{version}/embedpdf-bindings-{version}.pom',
            calls,
        )
        self.assertIn('x-access-token:t', calls)

    def test_a_published_engine_only_needs_the_pin(self):
        result, _ = self.run_check('android-engine-0000000000000000', curl_status='200', token='t')
        self.assertEqual(2, result.returncode, result.stderr)
        self.assertIn('commit the bump', result.stdout)

    def test_an_unreadable_registry_is_a_hard_error(self):
        result, _ = self.run_check('android-engine-0000000000000000', curl_status='401', token='t')
        self.assertEqual(69, result.returncode)
        self.assertIn('HTTP 401', result.stderr)

    def test_a_failed_request_is_a_hard_error(self):
        result, _ = self.run_check('android-engine-0000000000000000', token='t')
        self.assertEqual(69, result.returncode)
        self.assertIn('Could not resolve host', result.stderr)

    def test_no_token_is_a_hard_error(self):
        result, calls = self.run_check('android-engine-0000000000000000', curl_status='200')
        self.assertEqual(69, result.returncode)
        self.assertIn('no token', result.stderr)
        self.assertEqual('', calls)

    def test_the_gh_login_is_the_last_token_source(self):
        result, calls = self.run_check('android-engine-0000000000000000', curl_status='404', gh_auth='from-gh')
        self.assertEqual(1, result.returncode, result.stderr)
        self.assertIn('x-access-token:from-gh', calls)


if __name__ == '__main__':
    unittest.main()
