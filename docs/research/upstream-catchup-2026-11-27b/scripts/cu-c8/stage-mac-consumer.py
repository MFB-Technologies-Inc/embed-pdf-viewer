#!/usr/bin/env python3
"""CU-C9 (2026-11-27): platforms/apple/macos-consumer.mjs `stageMacConsumer` in
Python, so the macOS test runs stage their private consumer without Node.

    stage-mac-consumer.py <source package> <new private package>

Same checks and the same manifest edit as the .mjs: one `.iOS(.v17),`, two
CoreGraphics links, macOS 14 added after iOS 17, Foundation and AppKit links
added after each CoreGraphics link when absent. `.build` and `.swiftpm` are
not copied; symlinks are copied as links.
"""
import re
import shutil
import sys
from pathlib import Path


def manifest(text):
    count = lambda pattern: len(re.findall(pattern, text))
    if count(r'\.iOS\(\.v17\),') != 1 or count(r'\.linkedFramework\("CoreGraphics"\),') != 2:
        raise SystemExit('unsupported private consumer manifest platform/linker shape')
    if '.macOS(' in text and '.macOS(.v14)' not in text:
        raise SystemExit('private consumer manifest must target macOS14')
    foundation = count(r'\.linkedFramework\("Foundation"\)')
    appkit = count(r'\.linkedFramework\("AppKit", \.when\(platforms: \[\.macOS\]\)\)')
    if foundation not in (0, 2) or appkit not in (0, 2):
        raise SystemExit('unsupported partial private consumer manifest linker shape')
    if '.macOS(.v14)' not in text:
        text = text.replace('.iOS(.v17),', '.iOS(.v17), .macOS(.v14),', 1)

    def add(match):
        line, indent = match.group(0), match.group(1)
        if not foundation:
            line += f'\n{indent}.linkedFramework("Foundation"),'
        if not appkit:
            line += f'\n{indent}.linkedFramework("AppKit", .when(platforms: [.macOS])),'
        return line

    return re.sub(r'([ \t]*)\.linkedFramework\("CoreGraphics"\),', add, text)


def main(source, output):
    source, output = Path(source), Path(output)
    if output.exists():
        raise SystemExit(f'refusing existing private consumer: {output}')
    text = manifest((source / 'Package.swift').read_text())
    shutil.copytree(source, output, symlinks=True, ignore=shutil.ignore_patterns('.build', '.swiftpm'))
    (output / 'Package.swift').write_text(text)


if __name__ == '__main__':
    if len(sys.argv) != 3:
        raise SystemExit('usage: stage-mac-consumer.py <source-package> <new-private-package>')
    main(sys.argv[1], sys.argv[2])
