import importlib.util
from pathlib import Path
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[3] / 'scripts/check-api-coverage.py'
VIEWER = 'platforms/android/embedpdf/embedpdf/src/main/kotlin/com/embedpdf/PdfViewer.kt'


class KotlinCoverageTests(unittest.TestCase):
    def module(self):
        spec = importlib.util.spec_from_file_location('coverage', SCRIPT)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module

    def member(self, **mapping):
        return {'path': 'documents.rs', 'owner': 'Documents', 'symbol': 'list', **mapping}

    def validate(self, source, member, reference=None):
        module = self.module()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / VIEWER
            path.parent.mkdir(parents=True)
            path.write_text(source)
            reference = reference or {'members': [self.member(swift={'path': 'PDFViewer.swift', 'symbol': 'documents'})]}
            return module.validate_kotlin(root, {'members': [member]}, reference)

    def test_a_public_suspend_fun_is_found(self):
        member = self.member(kotlin={'path': VIEWER, 'symbol': 'documents'})
        self.assertEqual(self.validate('public suspend fun documents(): List<DocumentInfo> = x', member), [])

    def test_a_private_or_missing_symbol_fails(self):
        member = self.member(kotlin={'path': VIEWER, 'symbol': 'documents'})
        for source in ['private suspend fun documents() {}', 'public fun documentList() {}']:
            with self.subTest(source=source):
                errors = self.validate(source, member)
                self.assertEqual(len(errors), 1)
                self.assertIn('missing Kotlin', errors[0])

    def test_a_public_val_and_an_extension_are_found(self):
        for source in ['public val documents: Flow<Long> get() = x', 'public fun PdfViewer.documents(): Int = 0']:
            with self.subTest(source=source):
                self.assertEqual(self.validate(source, self.member(kotlin={'path': VIEWER, 'symbol': 'documents'})), [])

    def test_pending_needs_a_phase_name(self):
        self.assertEqual(self.validate('', self.member(pending='phase-3-a')), [])
        self.assertIn('pending is not a phase name', self.validate('', self.member(pending='later'))[0])

    def test_every_swift_member_is_listed(self):
        reference = {'members': [self.member(swift={'path': 'PDFViewer.swift', 'symbol': 'documents'}),
                                 {'path': 'documents.rs', 'owner': 'Documents', 'symbol': 'get'}]}
        errors = self.validate('', self.member(pending='phase-3-a'), reference)
        self.assertTrue(any('missing from the Kotlin manifest' in error for error in errors))

    def test_an_exclusion_matches_the_swift_manifest(self):
        errors = self.validate('', self.member(exclusion='internal'))
        self.assertTrue(any('exclusion differs' in error for error in errors))

    def test_a_path_outside_main_sources_fails(self):
        member = self.member(kotlin={'path': 'platforms/android/viewer/HttpSignatureTransport.kt', 'symbol': 'documents'})
        self.assertIn('outside', self.validate('', member)[0])


if __name__ == '__main__':
    unittest.main()
