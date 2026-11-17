/**
 * `rehydrate` rebuilds a page's transform in JavaScript and throws if the
 * rebuild drifts from the kernel's own numbers.
 */
import { describe, expect, test, vi } from 'vitest';
import { pageTransform } from '@embedpdf/core-geometry';

import {
  canonicalText,
  decode,
  decodeText,
  primitiveArgsJson,
  type EngineBridge,
} from '../src/engine-bridge';
import { Wire } from '../src/proxy';
import { readBinarySources, rehydrate } from '../src/rehydrate';

const transform = pageTransform({
  pageSize: { width: 612, height: 792 },
  rotation: 0,
  scale: 1,
  baseScale: 1,
  dpr: 1,
});
const page = { width: 612, height: 792, contentScale: 1, transform: { ...transform } };

describe('rehydrating a page transform', () => {
  // The fixture is consistent, so the throw below is the drift's alone.
  test('a transform the rebuild matches passes', () => {
    expect(() => rehydrate('getPageFrame', page)).not.toThrow();
  });

  test('a transform that drifts from the rebuild throws', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const drifted = { ...page, transform: { ...transform, zoom: transform.zoom + 0.5 } };
    expect(() => rehydrate('getPageFrame', drifted)).toThrow(/differs from the kernel's in zoom/);
  });
});

describe('a Blob argument', () => {
  test('a File is read to a payload with its bytes, type and name', async () => {
    const file = new File([new Uint8Array([1, 2, 3])], 'mark.png', { type: 'image/png' });
    expect(await readBinarySources([file, { width: 1 }])).toEqual([
      { data: new Uint8Array([1, 2, 3]), mimeType: 'image/png', name: 'mark.png' },
      { width: 1 },
    ]);
  });

  test("a payload's or attachment's Blob data lends its type and name where they are absent", async () => {
    const file = new File([new Uint8Array([4])], 'notes.txt', { type: 'text/plain' });
    expect(await readBinarySources({ data: file, name: 'kept.txt' })).toEqual({
      data: new Uint8Array([4]),
      name: 'kept.txt',
      mimeType: 'text/plain',
    });
  });

  test('one that reaches the encoder unprepared throws', () => {
    const wire = new Wire({ host: () => undefined, hold: () => 0 } as unknown as EngineBridge);
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});
    expect(() => wire.encode([new Blob(['x'])], undefined, 'importLibrary')).toThrow(
      '[core-wasm] a Blob reached the encoder unprepared: importLibrary',
    );
    expect(error).toHaveBeenCalled();
  });
});

describe('a Blob a callback answers', () => {
  // The default file picker (`filePickerProvider` in
  // `packages/framework/react/src/annotation.tsx`) answers `{ data: file }`;
  // an attachment needs the file's name from it.
  test("a picker's File reaches Rust as bytes with its name and type", async () => {
    const wire = new Wire({ host: () => undefined, hold: () => 0 } as unknown as EngineBridge);
    const file = new File([new Uint8Array([7, 8])], 'notes.txt', { type: 'text/plain' });
    const picker = wire.encode(() => Promise.resolve({ data: file })) as { $fn: string };
    const answer = await wire.callJs(picker.$fn.slice('js:'.length), '[]');
    expect(answer).toEqual({
      data: { $bytes: btoa(String.fromCharCode(7, 8)) },
      name: 'notes.txt',
      mimeType: 'text/plain',
    });
  });
});

describe('answer text with no marker', () => {
  const host = (n: number) => ({ held: n });

  test('decode returns the same object for markerless text', () => {
    const parse = vi.spyOn(JSON, 'parse');
    const text =
      '{"pages":[{"index":0,"size":{"width":612.5}}],"label":"a$b","on":true,"none":null}';
    const value = decodeText(text, host);
    expect(parse).toHaveBeenCalledTimes(1);
    expect(value).toBe(parse.mock.results[0]?.value);
    parse.mockRestore();
    expect(value).toEqual(decode(JSON.parse(text), host));
  });

  test('answers what the full decode answers, markers or not', () => {
    const texts = [
      '[]',
      '[1,"two",[3,{"four":4}],null,false]',
      '"text"',
      '0',
      'null',
      '{}',
      // A `$` that begins a string value: no marker, so the full decode.
      '{"name":"$ref","list":["$host"]}',
      '{"bytes":{"$bytes":"AQI="},"held":{"$host":7},"gone":{"$undefined":true}}',
      '{"big":{"$number":"Infinity"},"deep":[{"$undefined":true}]}',
      // A key the full decode assigns as the copy's prototype.
      '{"__proto__":{"inherited":1},"own":2}',
    ];
    for (const text of texts) {
      const fast = decodeText(text, host);
      const full = decode(JSON.parse(text), host);
      expect(fast, text).toEqual(full);
      if (fast !== null && typeof fast === 'object') {
        expect(Object.getPrototypeOf(fast), text).toEqual(Object.getPrototypeOf(full));
        expect(Object.keys(fast), text).toEqual(Object.keys(full as object));
      }
    }
  });
});

describe('the answer cache key', () => {
  const hooks = { hold: () => 0, fn: () => ({ $fn: 'js:0' }), blob: () => 0 };

  test('cache key equals JSON.stringify for primitive args', () => {
    const covered: unknown[][] = [
      [],
      ['plain'],
      ['quo"te', 'back\\slash', 'new\nline', '\u0000\u001f', '  ', 'üñí 😀', '\ud800'],
      [0, -0, 1, -1, 0.1, 1e21, 1e-7, -1e-7, Number.MAX_VALUE, Number.MIN_VALUE, 2 ** 53 + 2],
      [true, false, null],
      ['mixed', 3, true, null, -0],
    ];
    for (const args of covered) {
      const key = primitiveArgsJson(args);
      expect(key, String(args)).toBe(JSON.stringify(args));
      expect(key, String(args)).toBe(canonicalText(args, hooks));
    }
    const elsewhere: unknown[][] = [
      [NaN],
      [Infinity],
      [-Infinity],
      [undefined],
      ['a', undefined],
      [{}],
      [[1]],
      [{ page: 1 }],
      [() => 1],
      // eslint-disable-next-line no-sparse-arrays
      [1, , 2],
    ];
    for (const args of elsewhere) expect(primitiveArgsJson(args), String(args)).toBeUndefined();
  });

  test("a primitive call reaches the kernel with the old key's text and is cached by it", () => {
    const sent: string[] = [];
    const rust = {
      capability: () => '{"getLabel":{"$ref":1,"pinned":true}}',
      take_evicted: () => '[]',
      core_revision: () => 0,
      status: () => 'started',
      state_revision: () => 0,
      invoke_ref: (_id: number, json: string) => {
        sent.push(json);
        return '{"returned":"label"}';
      },
      release_ref: () => {},
    };
    const wire = new Wire({ host: () => undefined, hold: () => 0 } as unknown as EngineBridge);
    wire.attach(rust as never);
    const { getLabel } = wire.capability('shell') as { getLabel: (...a: unknown[]) => unknown };
    expect(getLabel('q"x', -0, true, null)).toBe('label');
    expect(getLabel('q"x', -0, true, null)).toBe('label');
    expect(getLabel(NaN)).toBe('label');
    expect(getLabel()).toBe('label');
    expect(getLabel()).toBe('label');
    expect(sent).toEqual(['["q\\"x",0,true,null]', '[{"$number":"NaN"}]', '[]']);
  });
});
