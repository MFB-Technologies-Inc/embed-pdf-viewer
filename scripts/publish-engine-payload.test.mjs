import assert from 'node:assert/strict';
import { copyFileSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { pinEngine, releaseRepo } from './publish-engine-payload.mjs';

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
