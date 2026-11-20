"""Tests for scripts/check-api-coverage.py."""
import importlib.util
import os
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location('check_api_coverage', ROOT / 'scripts/check-api-coverage.py')
coverage = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(coverage)

MEMBER = {'crate': 'epdf-kernel', 'path': 'crates/epdf-kernel/src/kernel.rs', 'owner': 'Documents', 'symbol': 'rename'}
EXPORT = {'path': 'crates/epdf-viewer-host/src/documents.rs', 'symbol': 'rename_document'}


def manifest(**fields):
    return {'schemaVersion': 1, 'crates': {}, 'kernel': {'kind': 'core'}, 'members': [dict(MEMBER, **fields)]}


def check(row, dumped):
    # An empty crates map skips the crate inventory check, as the Swift check does.
    return coverage.validate_csharp(ROOT, row, [dict(MEMBER)], set(dumped), check_crates=False)


class CSharpTests(unittest.TestCase):
    def test_a_ported_member_passes(self):
        row = manifest(export=EXPORT, csharp={'type': 'EmbedPDF.PdfViewer', 'member': 'RenameAsync'})
        self.assertEqual(check(row, ['EmbedPDF.PdfViewer.RenameAsync']), [])

    def test_a_missing_csharp_member_fails(self):
        row = manifest(export=EXPORT, csharp={'type': 'EmbedPDF.PdfViewer', 'member': 'RenameAsync'})
        errors = check(row, ['EmbedPDF.PdfViewer.OpenAsync'])
        self.assertTrue(any('missing C#' in error for error in errors), errors)

    def test_a_missing_rust_export_fails(self):
        row = manifest(export={'path': EXPORT['path'], 'symbol': 'no_such_export'},
                       csharp={'type': 'EmbedPDF.PdfViewer', 'member': 'RenameAsync'})
        errors = check(row, ['EmbedPDF.PdfViewer.RenameAsync'])
        self.assertTrue(any('missing Rust export' in error for error in errors), errors)

    def test_a_pending_member_passes(self):
        self.assertEqual(check(manifest(pending='phase-3-wave-A'), []), [])

    def test_an_unknown_pending_value_fails(self):
        errors = check(manifest(pending='later'), [])
        self.assertTrue(any('unknown pending' in error for error in errors), errors)

    def test_an_unaccounted_rust_member_fails(self):
        row = {'schemaVersion': 1, 'crates': {}, 'kernel': {'kind': 'core'}, 'members': []}
        errors = coverage.validate_csharp(ROOT, row, [dict(MEMBER)], set(), check_crates=False)
        self.assertTrue(any('unaccounted Rust member' in error for error in errors), errors)


class SwiftTests(unittest.TestCase):
    def run_script(self, *arguments):
        environment = dict(os.environ, PYTHONDONTWRITEBYTECODE='1')
        return subprocess.run([sys.executable, *arguments], cwd=ROOT, capture_output=True, text=True, env=environment)

    def test_swift_mode_matches_the_apple_script(self):
        for flags in ([], ['--allow-pending']):
            apple = self.run_script('platforms/apple/check-swift-api-coverage.py', *flags)
            shared = self.run_script('scripts/check-api-coverage.py', '--platform', 'swift', *flags)
            self.assertEqual((apple.returncode, apple.stdout, apple.stderr),
                             (shared.returncode, shared.stdout, shared.stderr))


if __name__ == '__main__':
    unittest.main()
