import assert from 'node:assert/strict';
import { copyFileSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { libRootFor, pinEngine, releaseRepo } from './publish-engine-payload.mjs';

const repo = fileURLToPath(new URL('..', import.meta.url));

test('pins both manifests', (t) => {
  const dir = mkdtempSync(path.join(tmpdir(), 'pin-engine-'));
  t.after(() => rmSync(dir, { recursive: true, force: true }));
  const sources = {
    nested: path.join(repo, 'platforms/apple/EmbedPDF/Package.swift'),
    root: path.join(repo, 'Package.swift'),
  };
  const copies = { nested: path.join(dir, 'nested.swift'), root: path.join(dir, 'root.swift') };
  for (const key of ['nested', 'root']) copyFileSync(sources[key], copies[key]);

  const tag = 'apple-engine-0123456789abcdef';
  const checksum = 'c'.repeat(64);
  t.mock.method(console, 'log', () => {});
  pinEngine(tag, checksum, copies);

  const changed = (key) => {
    const before = readFileSync(sources[key], 'utf8').split('\n');
    const after = readFileSync(copies[key], 'utf8').split('\n');
    assert.equal(after.length, before.length);
    return after.filter((line, i) => line !== before[i]);
  };
  assert.deepEqual(changed('nested'), [
    `let engineRelease = "${tag}"`,
    `let engineChecksum = "${checksum}"`,
  ]);
  assert.deepEqual(changed('root'), [
    `            url: "https://github.com/MFB-Technologies-Inc/embed-pdf-runtime/releases/download/${tag}/EmbedPDFBindings.xcframework.zip",`,
    `            checksum: "${checksum}"`,
  ]);
});

test('honors EMBEDPDF_RELEASE_REPO', (t) => {
  const saved = process.env.EMBEDPDF_RELEASE_REPO;
  t.after(() => {
    if (saved === undefined) delete process.env.EMBEDPDF_RELEASE_REPO;
    else process.env.EMBEDPDF_RELEASE_REPO = saved;
  });
  delete process.env.EMBEDPDF_RELEASE_REPO;
  assert.equal(releaseRepo(), 'MFB-Technologies-Inc/embed-pdf-runtime');
  process.env.EMBEDPDF_RELEASE_REPO = 'x/y';
  assert.equal(releaseRepo(), 'x/y');
});

test('lays stagings at any path out as one LIB_ROOT', (t) => {
  const dir = mkdtempSync(path.join(tmpdir(), 'lib-root-'));
  t.after(() => rmSync(dir, { recursive: true, force: true }));
  // As CI downloads them: one fork-slice-<target> artifact each, holding staging/.
  const stamps = ['ios-arm64', 'ios-sim-arm64', 'darwin-static-arm64', 'darwin-static-x64'].map(
    (target) => {
      const staging = path.join(dir, `fork-slice-${target}`, 'staging');
      mkdirSync(path.join(staging, 'lib'), { recursive: true });
      writeFileSync(path.join(staging, 'lib/libembedpdf.a'), target);
      return { target, staging };
    },
  );
  const root = libRootFor(stamps);
  t.after(() => rmSync(root, { recursive: true, force: true }));
  for (const { target } of stamps) {
    assert.equal(readFileSync(path.join(root, target, 'lib/libembedpdf.a'), 'utf8'), target);
  }
});
