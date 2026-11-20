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
  const saved = process.env.EMBEDPDF_RELEASE_REPO;
  t.after(() => {
    if (saved === undefined) delete process.env.EMBEDPDF_RELEASE_REPO;
    else process.env.EMBEDPDF_RELEASE_REPO = saved;
  });
  delete process.env.EMBEDPDF_RELEASE_REPO;
  pinEngine(tag, checksum, copies);

  const changed = (key) => {
    const before = readFileSync(sources[key], 'utf8').split('\n');
    const after = readFileSync(copies[key], 'utf8').split('\n');
    assert.equal(after.length, before.length);
    return after.filter((line, i) => line !== before[i]);
  };
  // The pin also moves both URLs to the release repo, whichever repo they named.
  assert.deepEqual(changed('nested'), [
    `let engineRelease = "${tag}"`,
    `let engineChecksum = "${checksum}"`,
    `        url: "https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/releases/download/\\(engineRelease)/EmbedPDFBindings.xcframework.zip",`,
  ]);
  assert.deepEqual(changed('root'), [
    `            url: "https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/releases/download/${tag}/EmbedPDFBindings.xcframework.zip",`,
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
  assert.equal(releaseRepo(), 'MFB-Technologies-Inc/embed-pdf-viewer');
  process.env.EMBEDPDF_RELEASE_REPO = 'x/y';
  assert.equal(releaseRepo(), 'x/y');
});

test('pins the URLs to EMBEDPDF_RELEASE_REPO', (t) => {
  const dir = mkdtempSync(path.join(tmpdir(), 'pin-engine-repo-'));
  t.after(() => rmSync(dir, { recursive: true, force: true }));
  const copies = { nested: path.join(dir, 'nested.swift'), root: path.join(dir, 'root.swift') };
  copyFileSync(path.join(repo, 'platforms/apple/EmbedPDF/Package.swift'), copies.nested);
  copyFileSync(path.join(repo, 'Package.swift'), copies.root);
  const saved = process.env.EMBEDPDF_RELEASE_REPO;
  t.after(() => {
    if (saved === undefined) delete process.env.EMBEDPDF_RELEASE_REPO;
    else process.env.EMBEDPDF_RELEASE_REPO = saved;
  });
  process.env.EMBEDPDF_RELEASE_REPO = 'x/y';
  t.mock.method(console, 'log', () => {});
  pinEngine('apple-engine-0123456789abcdef', 'c'.repeat(64), copies);
  for (const key of ['nested', 'root']) {
    const urls = readFileSync(copies[key], 'utf8').match(
      /https:\/\/github\.com\/[^"]*\/releases\/download\//g,
    );
    assert.deepEqual(urls, ['https://github.com/x/y/releases/download/']);
  }
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
