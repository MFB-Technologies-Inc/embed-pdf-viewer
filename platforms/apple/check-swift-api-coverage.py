#!/usr/bin/env python3
"""Check the reviewed Swift API inventory against Rust and Swift source changes.

`scripts/check-api-coverage.py --platform swift` does the work; this keeps the
Swift check's command and module names.
"""
import importlib.util
from pathlib import Path
import sys

_spec = importlib.util.spec_from_file_location(
    'check_api_coverage', Path(__file__).resolve().parents[2] / 'scripts/check-api-coverage.py')
_shared = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_shared)

ROOT = _shared.ROOT
PURE = _shared.PURE
inventory = _shared.inventory
key = _shared.key
swift_matches = _shared.swift_matches
validate = _shared.validate

if __name__ == '__main__':
    sys.exit(_shared.main(platform='swift'))
