#!/usr/bin/env python3
"""Check a reviewed platform API inventory against Rust and platform source changes.

--platform swift checks platforms/apple/swift-api-coverage.json exactly as
platforms/apple/check-swift-api-coverage.py does. --platform csharp checks
platforms/dotnet/csharp-api-coverage.json against the public members of the
built EmbedPDF assemblies, which platforms/dotnet/tools/EmbedPDF.ApiDump lists.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
PURE = {'acrojs', 'annotation-core', 'fonts', 'form-core', 'geometry', 'signature-core', 'stage-core', 'text-core', 'ui-core'}


def inventory(root):
    result = []
    directories = [(directory, directory.name) for directory in sorted((root / 'crates/plugins').glob('*'))
                   if directory.name not in PURE]
    directories.append((root / 'crates/epdf-kernel', 'epdf-kernel'))
    for directory, crate in directories:
        for path in sorted((directory / 'src').rglob('*.rs')):
            if any(part in {'ffi', 'harness', 'tests'} for part in path.relative_to(directory).parts) or path.stem in {'ffi', 'harness'}:
                continue
            source = path.read_text()
            # Capability impls, including nested read/write modules. Kernel
            # Documents and Kernel are included; other kernel types are resources.
            for impl in re.finditer(r'^impl(?:<[^\n]*>)?\s+([\w:]+)(?:<[^\n{]*>)?\s*\{', source, re.M):
                owner = impl.group(1).split('::')[-1]
                if crate == 'epdf-kernel':
                    if owner not in {'Documents', 'Kernel'}:
                        continue
                elif not (owner.endswith('Capability') or owner in {'Core', 'ViewDemand'}):
                    continue
                depth, end = 1, impl.end()
                while end < len(source) and depth:
                    depth += (source[end] == '{') - (source[end] == '}')
                    end += 1
                for method in re.finditer(r'\bpub\s+(?:async\s+)?fn\s+(\w+)\s*[(<]', source[impl.end():end]):
                    result.append({'crate': crate, 'path': str(path.relative_to(root)),
                                   'owner': owner, 'symbol': method.group(1)})
    return result


def key(member):
    return member['path'], member['owner'], member['symbol']


def swift_matches(source, target):
    symbol = re.escape(target['symbol'])
    if 'labels' not in target:
        return re.search(r'\bpublic\s+(?:var|let|func)\s+`?' + symbol + r'`?\b', source) is not None
    for match in re.finditer(r'\bpublic\s+func\s+`?' + symbol + r'`?\s*\(', source):
        # Split only the declaration's top-level parameters: defaults and
        # function/generic types can contain their own commas/parentheses.
        parameters, start, stack = [], match.end(), []
        quoted, escaped = False, False
        for index in range(start, len(source)):
            char = source[index]
            if quoted:
                if escaped:
                    escaped = False
                elif char == '\\':
                    escaped = True
                elif char == '"':
                    quoted = False
                continue
            if char == '"':
                quoted = True
            elif char in '([{<':
                stack.append(char)
            elif char == ')' and not stack:
                parameters.append(source[start:index])
                break
            elif char in ')]}>':
                if stack and {'(': ')', '[': ']', '{': '}', '<': '>'}[stack[-1]] == char:
                    stack.pop()
            elif char == ',' and not stack:
                parameters.append(source[start:index])
                start = index + 1
        labels = []
        for parameter in parameters:
            if not parameter.strip():
                continue
            label = re.match(r'\s*`?(\w+)`?(?:\s+`?\w+`?)?\s*:', parameter)
            labels.append(label.group(1) if label else None)
        if labels == target['labels']:
            return True
    return False


def validate_swift(root, manifest, discovered, allow_pending=False):
    errors = []
    entries = {key(member): member for member in manifest['members']}
    if len(entries) != len(manifest['members']):
        errors.append('duplicate manifest member')
    actual = {key(member) for member in discovered}
    for member in discovered:
        if key(member) not in entries:
            errors.append(f"unaccounted Rust member: {member['path']}::{member['symbol']}")
    for identity, member in entries.items():
        if identity not in actual:
            errors.append(f'stale Rust member: {identity}')
        if member.get('exclusion'):
            continue
        if member.get('pending') and allow_pending:
            continue
        if not member.get('export') or not member.get('swift'):
            errors.append(f'incomplete mapping: {identity}')
            continue
        mappings = [member] + member.get('overloads', [])
        for mapping in mappings:
            for language, field, pattern in [
                ('Rust export', 'export', r'\bpub\s+(?:async\s+)?fn\s+{symbol}\s*[(<]'),
                ('Swift', 'swift', r'\bpublic\s+(?:var|let|func)\s+`?{symbol}`?\b'),
            ]:
                target = mapping.get(field)
                if not target:
                    errors.append(f'incomplete overload mapping: {identity}')
                    continue
                path = root / target['path']
                found = path.exists() and (
                    swift_matches(path.read_text(), target) if field == 'swift'
                    else re.search(pattern.format(symbol=re.escape(target['symbol'])), path.read_text())
                )
                if not found:
                    errors.append(f"missing {language}: {target}")
    actual_crates = {path.parent.name for path in (root / 'crates/plugins').glob('*/Cargo.toml')}
    if actual_crates and set(manifest['crates']) != actual_crates:
        errors.append('crate coverage does not match crates/plugins inventory')
    kernel = manifest.get('kernel', {})
    if (root / 'crates/epdf-kernel/src').exists() and kernel.get('kind') != 'core':
        errors.append('kernel coverage must explicitly classify core and internal resources')
    return errors


# The Phase 3 wave that ports each plugin crate's capability
# (docs/superpowers/plans/2026-11-20-dotnet-parity.md, "Phase 3"). A pending
# row must name its crate's wave, and a new plugin crate needs a row here.
WAVES = {
    'A': ['metadata', 'search', 'render', 'history', 'outline', 'attachment', 'page-edit', 'view-manager', 'shell'],
    'B': ['interaction', 'i18n', 'actions'],
    'C': ['stage', 'selection', 'link', 'commands'],
    'D': ['annotation', 'form'],
    'E': ['redaction', 'measurement', 'stamp', 'signature'],
}
WAVE_OF = {crate: f'phase-3-wave-{wave}' for wave, crates in WAVES.items() for crate in crates}
RUST_EXPORT = r'\bpub\s+(?:async\s+)?fn\s+{symbol}\s*[(<]'


def validate_csharp(root, manifest, discovered, dumped, check_crates=True):
    """Every Rust member is mapped to a public C# member, pending in its wave, or excluded.

    dumped is {'members': {"<type>.<member>"}, 'signatures': {"<type>.<method>(<parameter types>)"}}.
    A mapping to a method names its overload with 'parameters'.
    """
    errors = []
    entries = {key(member): member for member in manifest['members']}
    if len(entries) != len(manifest['members']):
        errors.append('duplicate manifest member')
    actual = {key(member) for member in discovered}
    for member in discovered:
        if key(member) not in entries:
            errors.append(f"unaccounted Rust member: {member['path']}::{member['symbol']}")
    methods = {signature.split('(', 1)[0] for signature in dumped['signatures']}
    for identity, member in entries.items():
        if identity not in actual:
            errors.append(f'stale Rust member: {identity}')
        kinds = [field for field in ('csharp', 'pending', 'exclusion') if member.get(field)]
        if len(kinds) != 1:
            errors.append(f'needs exactly one of csharp, pending or exclusion: {identity}')
            continue
        if member.get('exclusion'):
            continue
        if member.get('pending'):
            wave = WAVE_OF.get(member['crate'])
            if wave is None:
                errors.append(f"no Phase 3 wave for crate {member['crate']!r}: {identity}")
            elif member['pending'] != wave:
                errors.append(f"pending {member['pending']!r} is not the crate's wave {wave!r}: {identity}")
            continue
        target, export = member['csharp'], member.get('export')
        if not export:
            errors.append(f'incomplete mapping: {identity}')
            continue
        path = root / export['path']
        if not (path.exists() and re.search(RUST_EXPORT.format(symbol=re.escape(export['symbol'])), path.read_text())):
            errors.append(f'missing Rust export: {export}')
        name = f"{target['type']}.{target['member']}"
        if 'parameters' in target:
            if f"{name}({', '.join(target['parameters'])})" not in dumped['signatures']:
                errors.append(f'missing C#: {target}')
        elif name in methods:
            errors.append(f'needs parameters to name one overload: {target}')
        elif name not in dumped['members']:
            errors.append(f'missing C#: {target}')
    if check_crates:
        actual_crates = {path.parent.name for path in (root / 'crates/plugins').glob('*/Cargo.toml')}
        if actual_crates and set(manifest['crates']) != actual_crates:
            errors.append('crate coverage does not match crates/plugins inventory')
        kernel = manifest.get('kernel', {})
        if (root / 'crates/epdf-kernel/src').exists() and kernel.get('kind') != 'core':
            errors.append('kernel coverage must explicitly classify core and internal resources')
    return errors


def dump_csharp_members(root):
    """The public members of the built assemblies: names and method signatures."""
    result = subprocess.run(
        ['dotnet', 'run', '-c', 'Release', '--project', str(root / 'platforms/dotnet/tools/EmbedPDF.ApiDump')],
        cwd=root, capture_output=True, text=True)
    if result.returncode != 0:
        raise SystemExit(f'EmbedPDF.ApiDump failed:\n{result.stdout}{result.stderr}')
    return json.loads(result.stdout)


def main_swift(args):
    manifest = json.loads((ROOT / 'platforms/apple/swift-api-coverage.json').read_text())
    errors = validate_swift(ROOT, manifest, inventory(ROOT), args.allow_pending)
    for error in errors:
        print(error, file=sys.stderr)
    if errors:
        return 1
    mapped = sum(not member.get('exclusion') for member in manifest['members'])
    excluded = len(manifest['members']) - mapped
    print(f"Swift API accounting: {len(manifest['crates'])} plugin-directory crates + kernel, "
          f"{len(manifest['members'])} members ({mapped} mapped, {excluded} exclusions). "
          "Counts describe accounting, not implementation parity.")
    return 0


def main_csharp(args):
    manifest = json.loads((ROOT / 'platforms/dotnet/csharp-api-coverage.json').read_text())
    raw = json.loads(Path(args.members).read_text()) if args.members else dump_csharp_members(ROOT)
    dumped = {'members': set(raw['members']), 'signatures': set(raw['signatures'])}
    errors = validate_csharp(ROOT, manifest, inventory(ROOT), dumped)
    for error in errors:
        print(error, file=sys.stderr)
    if errors:
        return 1
    members = manifest['members']
    ported = sum(bool(member.get('csharp')) for member in members)
    pending = sum(bool(member.get('pending')) for member in members)
    excluded = sum(bool(member.get('exclusion')) for member in members)
    print(f'C# API accounting: {len(members)} members ({ported} ported, {pending} pending, {excluded} exclusions).')
    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--platform', choices=['swift', 'csharp'], required=True)
    parser.add_argument('--allow-pending', action='store_true', help='swift: during implementation only')
    parser.add_argument('--members', help='csharp: a JSON file from EmbedPDF.ApiDump, instead of building it')
    args = parser.parse_args()
    return main_swift(args) if args.platform == 'swift' else main_csharp(args)


if __name__ == '__main__':
    sys.exit(main())
