import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm, stat, utimes, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import zlib from 'node:zlib';

import { readVector, syncVector, writeVector } from '../src/vectors';

const text = `${JSON.stringify({ schemaVersion: 2, kind: 'function', cases: [{ name: 'a', args: [1, 2, 3] }] }, null, 2)}\n`;

function compress(level: number): Buffer {
  return zlib.zstdCompressSync(text, {
    params: { [zlib.constants.ZSTD_c_compressionLevel]: level },
  });
}

async function withDir(body: (dir: string) => Promise<void>): Promise<void> {
  const dir = await mkdtemp(join(tmpdir(), 'zst-test-'));
  try {
    await body(dir);
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
}

test('check ignores compressed byte differences', async () => {
  await withDir(async (dir) => {
    const level3 = compress(3);
    const level19 = compress(19);
    assert.notDeepEqual(
      level3,
      level19,
      'the two levels must write different bytes for the test to mean anything',
    );
    const path = join(dir, 'x.json');
    await writeFile(`${path}.zst`, level19);
    assert.equal(await readVector(path), text);
    assert.equal(await syncVector(path, text, 'check'), 'current');
    assert.equal(await syncVector(path, `${text} `, 'check'), 'stale');
  });
});

test("write leaves an unchanged suite's bytes alone", async () => {
  await withDir(async (dir) => {
    const path = join(dir, 'x.json');
    await writeVector(path, text, { compress: true });
    // Back-date the file so a rewrite within the same clock tick still shows.
    const past = new Date(Date.now() - 60_000);
    await utimes(`${path}.zst`, past, past);
    const before = await readFile(`${path}.zst`);
    const mtime = (await stat(`${path}.zst`)).mtimeMs;
    assert.equal(await syncVector(path, text, 'write'), 'current');
    assert.deepEqual(await readFile(`${path}.zst`), before);
    assert.equal((await stat(`${path}.zst`)).mtimeMs, mtime);
  });
});

test('write keeps a blob another compressor wrote when its text is unchanged', async () => {
  await withDir(async (dir) => {
    const path = join(dir, 'x.json');
    const level19 = compress(19);
    await writeFile(`${path}.zst`, level19);
    assert.equal(await syncVector(path, text, 'write'), 'current');
    assert.deepEqual(await readFile(`${path}.zst`), level19);
  });
});

test('write keeps the compressed form a suite already has, at level 3', async () => {
  await withDir(async (dir) => {
    const path = join(dir, 'x.json');
    await writeFile(`${path}.zst`, compress(19));
    assert.equal(await syncVector(path, `${text} `, 'write'), 'wrote');
    assert.equal(
      zlib.zstdDecompressSync(await readFile(`${path}.zst`)).toString('utf8'),
      `${text} `,
    );
    assert.deepEqual(
      await readFile(`${path}.zst`),
      zlib.zstdCompressSync(`${text} `, {
        params: { [zlib.constants.ZSTD_c_compressionLevel]: 3 },
      }),
    );
    await assert.rejects(stat(path), 'no plain file appears beside the compressed one');
  });
});

test('a suite with only a plain file still checks, and stays plain when asked', async () => {
  await withDir(async (dir) => {
    const path = join(dir, 'x.json');
    await writeFile(path, text);
    assert.equal(await syncVector(path, text, 'check'), 'current');
    assert.equal(await writeVector(path, `${text} `, { compress: false }), true);
    assert.equal(await readFile(path, 'utf8'), `${text} `);
    await assert.rejects(stat(`${path}.zst`));
  });
});

test('a write compresses a plain suite by default', async () => {
  await withDir(async (dir) => {
    const path = join(dir, 'x.json');
    await writeFile(path, text);
    assert.equal(await syncVector(path, `${text} `, 'write'), 'wrote');
    assert.equal(await readVector(path), `${text} `);
    await assert.rejects(stat(path), 'the plain file is gone');
  });
});

test('compressing a plain suite removes the plain file', async () => {
  await withDir(async (dir) => {
    const path = join(dir, 'x.json');
    await writeFile(path, text);
    assert.equal(await writeVector(path, text, { compress: true }), true);
    assert.equal(await readVector(path), text);
    await assert.rejects(stat(path));
  });
});

test('a missing suite reads as null and checks stale', async () => {
  await withDir(async (dir) => {
    const path = join(dir, 'x.json');
    assert.equal(await readVector(path), null);
    assert.equal(await syncVector(path, text, 'check'), 'stale');
  });
});

test('write removes a stale plain file beside a current compressed suite', async () => {
  await withDir(async (dir) => {
    const path = join(dir, 'x.json');
    await writeFile(`${path}.zst`, compress(19));
    await writeFile(path, `${text} stale`);
    assert.equal(await syncVector(path, text, 'write'), 'wrote');
    await assert.rejects(stat(path), 'the plain file is gone');
    assert.deepEqual(await readFile(`${path}.zst`), compress(19), 'the current blob is untouched');
  });
});

test('compression without zstd in this Node names the Node it needs', async () => {
  await withDir(async (dir) => {
    const path = join(dir, 'x.json');
    await writeFile(`${path}.zst`, compress(3));
    const node = zlib as unknown as Record<string, unknown>;
    const saved = { compress: node.zstdCompressSync, decompress: node.zstdDecompressSync };
    node.zstdCompressSync = undefined;
    node.zstdDecompressSync = undefined;
    const message = `vector compression needs Node 22.15 or later, running ${process.version}`;
    try {
      await assert.rejects(readVector(path), { message });
      await assert.rejects(writeVector(join(dir, 'y.json'), text, { compress: true }), { message });
    } finally {
      node.zstdCompressSync = saved.compress;
      node.zstdDecompressSync = saved.decompress;
    }
  });
});
