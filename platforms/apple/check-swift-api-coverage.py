#!/usr/bin/env python3
"""Check the reviewed Swift API inventory against Rust and Swift source changes."""
# A thin wrapper: the check itself is scripts/check-api-coverage.py
# --platform swift, shared with the C# check. This file keeps its command line
# and the inventory/validate functions platforms/apple/tests import.
import argparse
import importlib.util
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
_SPEC = importlib.util.spec_from_file_location('check_api_coverage', ROOT / 'scripts/check-api-coverage.py')
_shared = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(_shared)

PURE = _shared.PURE
inventory = _shared.inventory
key = _shared.key
swift_matches = _shared.swift_matches
validate = _shared.validate_swift


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--allow-pending', action='store_true', help='during implementation only')
    return _shared.main_swift(parser.parse_args())


if __name__ == '__main__':
    sys.exit(main())
