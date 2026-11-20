import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { copyFileSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  buildChecks,
  libRootFor,
  parseArgs,
  pinEngine,
  releaseRepo,
  stagedRelease,
} from './publish-engine-payload.mjs';

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

test('--stage needs --only wasm or apple and build or publish', () => {
  assert.deepEqual(parseArgs(['node', 'x', '--only', 'wasm', '--stage', 'build']), {
    dryRun: false,
    only: 'wasm',
    stage: 'build',
  });
  assert.deepEqual(parseArgs(['node', 'x', '--only', 'apple', '--stage', 'publish', '--dry-run']), {
    dryRun: true,
    only: 'apple',
    stage: 'publish',
  });
  assert.deepEqual(parseArgs(['node', 'x']), { dryRun: false, only: null, stage: null });
  assert.match(parseArgs(['node', 'x', '--stage', 'build']).error, /needs --only/);
  assert.match(
    parseArgs(['node', 'x', '--only', 'wasm', '--stage', 'ship']).error,
    /build or publish/,
  );
  assert.match(parseArgs(['node', 'x', '--only', 'both']).error, /--only takes wasm or apple/);
});

test('refuses bad arguments before it checks anything', () => {
  const result = spawnSync(
    process.execPath,
    [path.join(repo, 'scripts/publish-engine-payload.mjs'), '--only', 'wasm', '--stage', 'ship'],
    { cwd: repo, encoding: 'utf8' },
  );
  assert.equal(result.status, 64);
  assert.match(result.stderr, /--stage takes build or publish/);
});

test('--stage publish needs no build tools', () => {
  for (const only of ['wasm', 'apple']) {
    assert.deepEqual(buildChecks({ only, stage: 'publish' }), {
      runtimeSrc: false,
      cargo: false,
      rustc: false,
    });
    for (const stage of [null, 'build']) {
      assert.deepEqual(buildChecks({ only, stage }), {
        runtimeSrc: true,
        cargo: true,
        rustc: true,
      });
    }
  }
});

// A --stage build output dir: the archive, its .sha256 and the facts.
function stagedDir(t, overrides = {}) {
  const dir = mkdtempSync(path.join(tmpdir(), 'staged-payload-'));
  t.after(() => rmSync(dir, { recursive: true, force: true }));
  const asset = 'engine-runtime-payload-wasm32.tar.gz';
  const bytes = Buffer.from('payload bytes');
  const digest = createHash('sha256').update(bytes).digest('hex');
  writeFileSync(path.join(dir, asset), bytes);
  writeFileSync(path.join(dir, `${asset}.sha256`), `${digest}  ${asset}\n`);
  const facts = {
    tag: 'runtime-payloads-0123456789abcdef',
    hash: '0123456789abcdef',
    head: 'a'.repeat(40),
    archiveSha256: digest,
    notes: 'notes',
    ...overrides,
  };
  writeFileSync(path.join(dir, `${asset}.json`), JSON.stringify(facts));
  return { dir, asset, facts };
}

test('takes a staged payload that matches the tree', (t) => {
  const { dir, asset, facts } = stagedDir(t);
  assert.deepEqual(stagedRelease({ dir, asset, tag: facts.tag, head: facts.head }), facts);
});

test('refuses a staged payload for another tag or commit', (t) => {
  const { dir, asset, facts } = stagedDir(t);
  assert.throws(
    () => stagedRelease({ dir, asset, tag: 'runtime-payloads-ffffffffffffffff', head: facts.head }),
    /is runtime-payloads-0123456789abcdef built at a{40}, but this tree is runtime-payloads-f{16}/,
  );
  assert.throws(
    () => stagedRelease({ dir, asset, tag: facts.tag, head: 'b'.repeat(40) }),
    /but this tree is .* at b{40}/,
  );
});

test('refuses a staged payload whose bytes changed', (t) => {
  const { dir, asset, facts } = stagedDir(t);
  writeFileSync(path.join(dir, asset), 'other bytes');
  assert.throws(
    () => stagedRelease({ dir, asset, tag: facts.tag, head: facts.head }),
    /hashes to [0-9a-f]{64}, not the [0-9a-f]{64} --stage build packed/,
  );
});

test('refuses a staged payload whose .sha256 disagrees', (t) => {
  const { dir, asset, facts } = stagedDir(t);
  writeFileSync(path.join(dir, `${asset}.sha256`), `${'0'.repeat(64)}  ${asset}\n`);
  assert.throws(
    () => stagedRelease({ dir, asset, tag: facts.tag, head: facts.head }),
    /\.sha256 doesn't name/,
  );
});

test('refuses a missing staged payload', (t) => {
  const { dir, asset, facts } = stagedDir(t);
  rmSync(path.join(dir, `${asset}.json`));
  assert.throws(
    () => stagedRelease({ dir, asset, tag: facts.tag, head: facts.head }),
    /--stage publish needs .* from --stage build/,
  );
});
