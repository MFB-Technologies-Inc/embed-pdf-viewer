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
PLUGIN = {'crate': 'metadata', 'path': 'crates/plugins/metadata/src/capability.rs', 'owner': 'MetadataCapability', 'symbol': 'update'}
EXPORT = {'path': 'crates/epdf-viewer-host/src/documents.rs', 'symbol': 'rename_document'}
RENAME = ['EmbedPDF.DocumentInfo', 'System.String', 'System.Threading.CancellationToken']
SIGNATURE = 'EmbedPDF.PdfViewer.RenameAsync(EmbedPDF.DocumentInfo, System.String, System.Threading.CancellationToken)'


def manifest(member=MEMBER, **fields):
    return {'schemaVersion': 1, 'crates': {}, 'kernel': {'kind': 'core'}, 'members': [dict(member, **fields)]}


def dump(signatures=(SIGNATURE,)):
    return {'members': {'EmbedPDF.PdfViewer.RenameAsync', 'EmbedPDF.PdfViewer.OpenAsync'}, 'signatures': set(signatures)}


def check(row, dumped, member=MEMBER):
    # An empty crates map skips the crate inventory check, as the Swift check does.
    return coverage.validate_csharp(ROOT, row, [dict(member)], dumped, check_crates=False)


def ported(**target):
    return manifest(export=EXPORT, csharp=dict({'type': 'EmbedPDF.PdfViewer', 'member': 'RenameAsync', 'parameters': RENAME}, **target))


class CSharpTests(unittest.TestCase):
    def assertError(self, errors, text):
        self.assertTrue(any(text in error for error in errors), errors)

    def test_a_ported_member_passes(self):
        self.assertEqual(check(ported(), dump()), [])

    def test_a_missing_csharp_member_fails(self):
        self.assertError(check(ported(member='CloseAsync'), dump()), 'missing C#')

    def test_an_overload_with_other_parameters_fails(self):
        self.assertError(check(ported(parameters=['EmbedPDF.DocumentInfo']), dump()), 'missing C#')

    def test_a_method_mapping_needs_its_parameters(self):
        target = {'type': 'EmbedPDF.PdfViewer', 'member': 'RenameAsync'}
        self.assertError(check(manifest(export=EXPORT, csharp=target), dump()), 'needs parameters')

    def test_a_missing_rust_export_fails(self):
        row = manifest(export={'path': EXPORT['path'], 'symbol': 'no_such_export'},
                       csharp={'type': 'EmbedPDF.PdfViewer', 'member': 'RenameAsync', 'parameters': RENAME})
        self.assertError(check(row, dump()), 'missing Rust export')

    def test_a_pending_member_in_its_crates_wave_passes(self):
        self.assertEqual(check(manifest(PLUGIN, pending='phase-3-wave-A'), dump(), PLUGIN), [])

    def test_a_pending_member_in_another_wave_fails(self):
        self.assertError(check(manifest(PLUGIN, pending='phase-3-wave-B'), dump(), PLUGIN), 'wave')

    def test_an_unknown_pending_value_fails(self):
        self.assertError(check(manifest(PLUGIN, pending='later'), dump(), PLUGIN), 'wave')

    def test_a_crate_with_no_wave_cannot_be_pending(self):
        self.assertError(check(manifest(pending='phase-3-wave-A'), dump()), 'no Phase 3 wave')

    def test_an_exclusion_with_a_known_classification_passes(self):
        for classification in sorted(coverage.EXCLUSION_CLASSIFICATIONS):
            row = manifest(exclusion='No export.', classification=classification)
            self.assertEqual(check(row, dump()), [], classification)

    def test_an_exclusion_with_an_unknown_or_missing_classification_fails(self):
        self.assertError(check(manifest(exclusion='No export.', classification='later'), dump()), 'unknown exclusion classification')
        self.assertError(check(manifest(exclusion='No export.'), dump()), 'unknown exclusion classification')

    def test_an_unaccounted_rust_member_fails(self):
        row = {'schemaVersion': 1, 'crates': {}, 'kernel': {'kind': 'core'}, 'members': []}
        self.assertError(check(row, dump()), 'unaccounted Rust member')


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
