/**
 * The D-1 transport over `fetch` (`src/signature-transport.ts`): each
 * outcome against a stubbed `fetch` and a local server, a request through
 * upstream's own routes (`@embedpdf/core-signature/server`) run in-process,
 * and the Rust signature plugin signing over it: a `Uint8Array` body
 * crossing the wire both ways, a granted timestamp computed from the
 * request, and a long-term validation fill whose answers come from the test
 * certificate authority through `revocationRoute`.
 */
import { readFile } from 'node:fs/promises';
import { createServer, type Server } from 'node:http';
import type { AddressInfo } from 'node:net';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { afterAll, afterEach, beforeAll, describe, expect, test, vi } from 'vitest';
import * as asn1js from 'asn1js';
import * as pkijs from 'pkijs';
import { createLocalEngine } from '@embedpdf/engine';
import type { Kernel } from '@embedpdf/core';
import {
  createTestCertificateAuthority,
  createTestSigner,
  createTestTimestampAuthority,
} from '@embedpdf/core-signature';
import { revocationRoute, timestampRoute } from '@embedpdf/core-signature/server';
import { SignatureToken } from '@embedpdf/plugin-signature';

import { WasmKernel, set_host_bytes_source, set_host_release } from '../wasm-node/epdf_web.js';
import {
  createKernel,
  fetchTransport,
  formPlugin,
  httpRevocationSource,
  httpTimestampAuthority,
  interactionPlugin,
  setWasmKernel,
  signaturePlugin,
  stagePlugin,
  type TransportOutcome,
  type TransportRequest,
} from '../src';
import { hostBytes, releaseHost } from '../src/engine-bridge';

const here = dirname(fileURLToPath(import.meta.url));
const fixtures = resolve(here, '..', '..', 'engine', 'main', 'test', 'fixtures');

let unsigned: Uint8Array;
/** A one-page PDF drawn into the widget: one-step signing needs an appearance. */
let artwork: Uint8Array;
let kernel: Kernel | undefined;
let server: Server | undefined;

beforeAll(async () => {
  setWasmKernel(WasmKernel);
  set_host_bytes_source(hostBytes);
  set_host_release(releaseHost);
  unsigned = new Uint8Array(await readFile(resolve(fixtures, 'unsigned_sigfield.pdf')));
  artwork = new Uint8Array(await readFile(resolve(fixtures, 'signature_artwork.pdf')));
});
afterEach(async () => {
  await kernel?.destroy();
  kernel = undefined;
  await new Promise<void>((done) => (server ? server.close(() => done()) : done()));
  server = undefined;
  vi.unstubAllGlobals();
});

const QUERY = 'application/timestamp-query';

/** A request as the kernel hands one over (`request_json`). */
const request = (more: Partial<TransportRequest> = {}): TransportRequest => ({
  url: 'https://app.example/api/timestamp',
  contentType: QUERY,
  headers: [{ name: 'x-key', value: 'k' }],
  body: new Uint8Array([0x30, 0x03, 0x02, 0x01, 0x01]),
  timeoutMs: 10_000,
  maxReplyBytes: 1024 * 1024,
  ...more,
});

/** A `fetch` that answers through `answer` and keeps what it was sent. */
function stubFetch(answer: (url: string, init: RequestInit) => Promise<Response> | Response) {
  const sent: { url: string; init: RequestInit }[] = [];
  const fetch = vi.fn(async (url: string | URL | Request, init?: RequestInit) => {
    sent.push({ url: String(url), init: init! });
    return answer(String(url), init!);
  }) as unknown as typeof globalThis.fetch;
  return { fetch, sent };
}

/** A `fetch` that never answers until its signal aborts. */
const hangingFetch = (async (_: unknown, init?: RequestInit) =>
  new Promise((_resolve, reject) =>
    init!.signal!.addEventListener('abort', () =>
      reject(new DOMException('This operation was aborted', 'AbortError')),
    ),
  )) as unknown as typeof globalThis.fetch;

/** A body of `chunks`, with no `content-length`. */
const streamed = (chunks: Uint8Array[], then?: 'error' | 'hang', signal?: AbortSignal) =>
  new ReadableStream<Uint8Array>({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(chunk);
      if (then === 'error') controller.error(new TypeError('terminated'));
      else if (then === 'hang') {
        signal?.addEventListener('abort', () =>
          controller.error(new DOMException('This operation was aborted', 'AbortError')),
        );
      } else controller.close();
    },
  });

describe('fetchTransport against a stubbed fetch', () => {
  test('replied: a POST of the body with content-type then the headers, any status, the bytes back', async () => {
    const { fetch, sent } = stubFetch(
      () => new Response(new Uint8Array([9, 8, 7]), { status: 201 }),
    );
    const outcome = await fetchTransport({ fetch })(request());
    expect(outcome).toEqual({ kind: 'replied', status: 201, body: new Uint8Array([9, 8, 7]) });
    expect(sent).toHaveLength(1);
    expect(sent[0].url).toBe('https://app.example/api/timestamp');
    expect(sent[0].init.method).toBe('POST');
    expect(sent[0].init.headers).toEqual({ 'content-type': QUERY, 'x-key': 'k' });
    expect(sent[0].init.body).toEqual(new Uint8Array([0x30, 0x03, 0x02, 0x01, 0x01]));
    expect(sent[0].init.signal).toBeInstanceOf(AbortSignal);

    // An error status is a reply too: what it means is the Rust client's to say.
    const refusing = stubFetch(
      () => new Response('{"reason":"not-allowed","message":"no"}', { status: 403 }),
    );
    const refused = await fetchTransport({ fetch: refusing.fetch })(request());
    expect(refused.kind === 'replied' && refused.status).toBe(403);
    expect(refused.kind === 'replied' && new TextDecoder().decode(refused.body)).toBe(
      '{"reason":"not-allowed","message":"no"}',
    );
    const busy = stubFetch(() => new Response(null, { status: 503 }));
    expect(await fetchTransport({ fetch: busy.fetch })(request())).toEqual({
      kind: 'replied',
      status: 503,
      body: new Uint8Array(),
    });
  });

  test('a later header wins over content-type, as in the clients spread', async () => {
    const { fetch, sent } = stubFetch(() => new Response(''));
    await fetchTransport({ fetch })(
      request({ headers: [{ name: 'content-type', value: 'text/plain' }] }),
    );
    expect(sent[0].init.headers).toEqual({ 'content-type': 'text/plain' });
  });

  test('the global fetch, read when the request is sent', async () => {
    const transport = fetchTransport();
    const { fetch, sent } = stubFetch(() => new Response('ok'));
    vi.stubGlobal('fetch', fetch);
    expect((await transport(request())).kind).toBe('replied');
    expect(sent).toHaveLength(1);
  });

  test('timedOut: no answer within timeoutMs aborts the fetch', async () => {
    const started = Date.now();
    expect(await fetchTransport({ fetch: hangingFetch })(request({ timeoutMs: 40 }))).toEqual({
      kind: 'timedOut',
    });
    expect(Date.now() - started).toBeGreaterThanOrEqual(35);
  });

  test('timedOut: the timeout covers reading the reply', async () => {
    const fetch = (async (_: unknown, init?: RequestInit) =>
      new Response(
        streamed([new Uint8Array([1])], 'hang', init!.signal!),
      )) as typeof globalThis.fetch;
    expect(await fetchTransport({ fetch })(request({ timeoutMs: 40 }))).toEqual({
      kind: 'timedOut',
    });
  });

  test('tooLarge: by content-length, or once more than maxReplyBytes is read', async () => {
    const declared = stubFetch(
      () => new Response(new Uint8Array(8), { headers: { 'content-length': '8' } }),
    );
    expect(await fetchTransport({ fetch: declared.fetch })(request({ maxReplyBytes: 7 }))).toEqual({
      kind: 'tooLarge',
    });

    // A body with no end: reading stops past the limit.
    const endless = stubFetch(
      () =>
        new Response(
          new ReadableStream<Uint8Array>({
            pull(controller) {
              controller.enqueue(new Uint8Array(4));
            },
          }),
        ),
    );
    expect(await fetchTransport({ fetch: endless.fetch })(request({ maxReplyBytes: 7 }))).toEqual({
      kind: 'tooLarge',
    });

    // Exactly the limit is a reply.
    const exact = stubFetch(() => new Response(streamed([new Uint8Array(4), new Uint8Array(3)])));
    const outcome = await fetchTransport({ fetch: exact.fetch })(request({ maxReplyBytes: 7 }));
    expect(outcome.kind === 'replied' && outcome.body.byteLength).toBe(7);
  });

  test('failed: a fetch that throws, with the route hint for a timestamp query in a page', async () => {
    const offline = stubFetch(() => {
      throw new TypeError('fetch failed');
    });
    expect(await fetchTransport({ fetch: offline.fetch })(request())).toEqual({
      kind: 'failed',
      message: 'fetch failed',
    });

    vi.stubGlobal('document', {});
    expect(await fetchTransport({ fetch: offline.fetch })(request())).toEqual({
      kind: 'failed',
      message:
        'fetch failed. In a browser most authorities cannot be called directly: call yours through a route on your own server',
    });
    // A revocation question goes to the app's own route: no hint.
    expect(
      await fetchTransport({ fetch: offline.fetch })(request({ contentType: 'application/json' })),
    ).toEqual({ kind: 'failed', message: 'fetch failed' });
  });

  test('failed: a reply that breaks off', async () => {
    const fetch = (async () =>
      new Response(streamed([new Uint8Array([1, 2])], 'error'))) as typeof globalThis.fetch;
    expect(await fetchTransport({ fetch })(request())).toEqual({
      kind: 'failed',
      message: 'the reply broke off: terminated',
    });
  });
});

/** A server on a free loopback port; `handle` answers each request. */
async function listen(
  handle: (
    body: Buffer,
    headers: Record<string, unknown>,
    res: import('node:http').ServerResponse,
  ) => void,
): Promise<string> {
  server = createServer((req, res) => {
    const parts: Buffer[] = [];
    req.on('data', (part: Buffer) => parts.push(part));
    req.on('end', () => handle(Buffer.concat(parts), req.headers, res));
  });
  await new Promise<void>((done) => server!.listen(0, '127.0.0.1', done));
  return `http://127.0.0.1:${(server!.address() as AddressInfo).port}/stamp`;
}

describe('fetchTransport against a local server', () => {
  test('replied and tooLarge over a real connection', async () => {
    let seen: { body: Buffer; headers: Record<string, unknown> } | undefined;
    const url = await listen((body, headers, res) => {
      seen = { body, headers };
      res.writeHead(200, { 'content-type': 'application/timestamp-reply' });
      res.end(Buffer.from([4, 5, 6]));
    });
    expect(await fetchTransport()(request({ url }))).toEqual({
      kind: 'replied',
      status: 200,
      body: new Uint8Array([4, 5, 6]),
    });
    expect([...seen!.body]).toEqual([0x30, 0x03, 0x02, 0x01, 0x01]);
    expect(seen!.headers['content-type']).toBe(QUERY);
    expect(seen!.headers['x-key']).toBe('k');
    expect(await fetchTransport()(request({ url, maxReplyBytes: 2 }))).toEqual({
      kind: 'tooLarge',
    });
  });

  test('timedOut when the server holds the reply', async () => {
    const url = await listen((_body, _headers, res) => {
      setTimeout(() => res.end('late'), 500);
    });
    expect(await fetchTransport()(request({ url, timeoutMs: 50 }))).toEqual({ kind: 'timedOut' });
  });

  test('failed when nothing listens', async () => {
    const url = await listen(() => {});
    await new Promise<void>((done) => server!.close(() => done()));
    server = undefined;
    const outcome = await fetchTransport()(request({ url }));
    expect(outcome.kind).toBe('failed');
  });
});

/** A `fetch` that hands every request to a route speaking the Fetch API, in-process. */
const routeFetch = (route: (request: Request) => Promise<Response>) =>
  (async (url: string | URL | Request, init?: RequestInit) =>
    route(new Request(url, init))) as typeof globalThis.fetch;

describe('through upstream routes, in-process', () => {
  test('timestampRoute passes the query on and the reply back, with its status', async () => {
    const asked: { url: string; type: string | null; key: string | null; body: Uint8Array }[] = [];
    let status = 200;
    const authority = (async (url: string | URL | Request, init?: RequestInit) => {
      const headers = new Headers(init!.headers);
      asked.push({
        url: String(url),
        type: headers.get('content-type'),
        key: headers.get('x-authority-key'),
        body: new Uint8Array(init!.body as ArrayBuffer),
      });
      return new Response(new Uint8Array([0x30, 0x00]), {
        status,
        headers: { 'content-type': 'application/timestamp-reply' },
      });
    }) as typeof globalThis.fetch;
    const route = timestampRoute({
      url: 'https://tsa.example/',
      headers: { 'x-authority-key': 'secret' },
      fetch: authority,
    });
    const transport = fetchTransport({ fetch: routeFetch(route) });

    expect(await transport(request())).toEqual({
      kind: 'replied',
      status: 200,
      body: new Uint8Array([0x30, 0x00]),
    });
    expect(asked).toEqual([
      {
        url: 'https://tsa.example/',
        type: QUERY,
        key: 'secret',
        body: new Uint8Array([0x30, 0x03, 0x02, 0x01, 0x01]),
      },
    ]);

    status = 500;
    expect((await transport(request())).kind === 'replied').toBe(true);
    expect(await transport(request())).toMatchObject({ status: 500 });

    // An empty query is refused by the route itself.
    const refused = await transport(request({ body: new Uint8Array() }));
    expect(refused).toMatchObject({ kind: 'replied', status: 413 });
  });

  test('timestampRoute answers 504 when the authority does not', async () => {
    const route = timestampRoute({
      url: 'https://tsa.example/',
      fetch: hangingFetch,
      timeoutMs: 30,
    });
    const outcome = await fetchTransport({ fetch: routeFetch(route) })(request());
    expect(outcome.kind === 'replied' && outcome.status).toBe(504);
    expect(outcome.kind === 'replied' && new TextDecoder().decode(outcome.body)).toBe(
      'the timestamp authority did not answer within 30 ms',
    );
  });

  test('revocationRoute asks the authority for a question and refuses a private address', async () => {
    // A CRL's shape: one SEQUENCE whose first element is a SEQUENCE.
    const crl = new Uint8Array([0x30, 0x04, 0x30, 0x02, 0x05, 0x00]);
    const asked: { url: string; method?: string }[] = [];
    const authority = (async (url: string | URL | Request, init?: RequestInit) => {
      asked.push({ url: String(url), method: init?.method });
      return new Response(crl, { headers: { 'content-type': 'application/pkix-crl' } });
    }) as typeof globalThis.fetch;
    const route = revocationRoute({ fetch: authority, lookup: null, cache: false });
    const transport = fetchTransport({ fetch: routeFetch(route) });
    const question = (url: string) =>
      request({
        url: 'https://app.example/api/revocation',
        contentType: 'application/json',
        headers: [{ name: 'x-session', value: 's' }],
        body: new TextEncoder().encode(JSON.stringify({ kind: 'crl', url })),
        maxReplyBytes: 2 * 1024 * 1024,
      });

    expect(await transport(question('http://crl.example.com/a.crl'))).toEqual({
      kind: 'replied',
      status: 200,
      body: crl,
    });
    expect(asked).toEqual([{ url: 'http://crl.example.com/a.crl', method: 'GET' }]);

    const refused = await transport(question('http://127.0.0.1/a.crl'));
    expect(refused.kind === 'replied' && refused.status).toBe(403);
    expect(
      refused.kind === 'replied' && JSON.parse(new TextDecoder().decode(refused.body)).reason,
    ).toBe('not-allowed');
    expect(asked).toHaveLength(1);
  });
});

// ── through the Rust kernel ─────────────────────────────────────────────────

/** One DER element at `at`: where its contents start and where it ends. */
function element(bytes: Uint8Array, at: number): { tag: number; start: number; end: number } {
  const tag = bytes[at]!;
  let length = bytes[at + 1]!;
  let start = at + 2;
  if (length & 0x80) {
    const octets = length & 0x7f;
    length = 0;
    for (let i = 0; i < octets; i++) length = length * 256 + bytes[start + i]!;
    start += octets;
  }
  return { tag, start, end: start + length };
}

/** An RFC 3161 query's digest: TimeStampReq.messageImprint.hashedMessage. */
function queriedDigest(query: Uint8Array): Uint8Array {
  const request = element(query, 0);
  const version = element(query, request.start);
  const imprint = element(query, version.end);
  const algorithm = element(query, imprint.start);
  const digest = element(query, algorithm.end);
  expect(digest.tag).toBe(0x04);
  return query.slice(digest.start, digest.end);
}

/** An RFC 3161 query's nonce, the INTEGER after the imprint and an optional policy. */
function queriedNonce(query: Uint8Array): Uint8Array | null {
  const request = element(query, 0);
  const version = element(query, request.start);
  let at = element(query, version.end).end;
  while (at < request.end) {
    const next = element(query, at);
    if (next.tag === 0x02) return query.slice(next.start, next.end);
    at = next.end;
  }
  return null;
}

/**
 * The nonce the next token answers with. The test authority's
 * `buildTimestampToken` writes none, so while this is set its TSTInfo
 * gets one as it is encoded: the same pkijs the authority uses, since
 * this package's `pkijs` links to the same install.
 */
let echoNonce: Uint8Array | null = null;
const tstInfoToSchema = pkijs.TSTInfo.prototype.toSchema;
beforeAll(() => {
  pkijs.TSTInfo.prototype.toSchema = function (this: pkijs.TSTInfo) {
    if (echoNonce) this.nonce = new asn1js.Integer({ valueHex: echoNonce.slice().buffer });
    return tstInfoToSchema.call(this);
  };
});
afterAll(() => {
  pkijs.TSTInfo.prototype.toSchema = tstInfoToSchema;
});

function der(tag: number, contents: Uint8Array): Uint8Array {
  const n = contents.byteLength;
  const length =
    n < 0x80
      ? [n]
      : n < 0x100
        ? [0x81, n]
        : n < 0x10000
          ? [0x82, n >> 8, n & 0xff]
          : [0x83, n >> 16, (n >> 8) & 0xff, n & 0xff];
  const out = new Uint8Array(1 + length.length + n);
  out[0] = tag;
  out.set(length, 1);
  out.set(contents, 1 + length.length);
  return out;
}

/** TimeStampResp { status granted, token }. */
function granted(token: Uint8Array): Uint8Array {
  const status = der(0x30, new Uint8Array([0x02, 0x01, 0x00]));
  const both = new Uint8Array(status.byteLength + token.byteLength);
  both.set(status);
  both.set(token, status.byteLength);
  return der(0x30, both);
}

/**
 * A stub authority behind `timestampRoute`: it stamps the digest it is
 * asked for with the test authority's key. The test authority writes no
 * nonce (`buildTimestampToken`), so the Rust client refuses the token at
 * its last check, past the algorithm and digest checks: proof that the
 * query reached the authority and the token came back byte for byte.
 */
async function stampingAuthority(echo = false) {
  const tsa = await createTestTimestampAuthority();
  const queries: Uint8Array[] = [];
  const fetch = (async (_url: unknown, init?: RequestInit) => {
    const query = new Uint8Array(init!.body as ArrayBuffer);
    queries.push(query);
    echoNonce = echo ? queriedNonce(query) : null;
    let token: Uint8Array;
    try {
      token = await tsa.sign({
        digest: queriedDigest(query),
        algorithm: 'sha256',
        subFilter: 'ETSI.RFC3161',
      });
    } finally {
      echoNonce = null;
    }
    return new Response(new Uint8Array(granted(token)), {
      headers: { 'content-type': 'application/timestamp-reply' },
    });
  }) as typeof globalThis.fetch;
  return { fetch, queries, certificate: tsa.certificate };
}

/** A test signer as a plain port: its `CryptoKey` does not cross. */
const plainPort = (signer: Awaited<ReturnType<typeof createTestSigner>>) => ({
  kind: signer.kind,
  algorithm: signer.algorithm,
  hash: signer.hash,
  certificateChain: signer.certificateChain,
  sign: signer.sign,
});

async function signingKey() {
  return plainPort(await createTestSigner());
}

async function signatureKernel(signature = signaturePlugin()) {
  const engine = createLocalEngine({ runtime: { prefer: 'wasm' } });
  const k = createKernel({
    engine,
    plugins: [stagePlugin(), interactionPlugin(), formPlugin(), signature],
  });
  kernel = k;
  await k.start();
  const ready = new Promise<void>((done) => {
    const off = k.documents.onOpened(() => {
      off();
      done();
    });
  });
  k.documents.openAll([
    { source: { kind: 'bytes', id: 'unsigned', bytes: unsigned }, scope: ['*'] },
  ]);
  await ready;
  const capability = k.capability(SignatureToken, 'unsigned');
  await capability.refresh();
  return capability;
}

const FIELD = { kind: 'fqn', name: 'sig' } as const;

async function refusal(promise: Promise<unknown>): Promise<Error> {
  try {
    await promise;
  } catch (error) {
    return error as Error;
  }
  throw new Error('expected a refusal');
}

describe('the Rust signature plugin over the transport', () => {
  test('a Uint8Array body crosses to the transport and back while signing with a timestamp', async () => {
    const authority = await stampingAuthority();
    const route = timestampRoute({ url: 'https://tsa.example/', fetch: authority.fetch });
    const inner = fetchTransport({ fetch: routeFetch(route) });
    const seen: { request: TransportRequest; outcome: TransportOutcome }[] = [];
    const transport = async (request: TransportRequest) => {
      const outcome = await inner(request);
      seen.push({ request, outcome });
      return outcome;
    };
    const signature = await signatureKernel();

    const error = await refusal(
      signature.sign({
        field: FIELD,
        signingKey: await signingKey(),
        appearance: artwork,
        timestampAuthority: {
          transport,
          url: 'https://app.example/api/timestamp',
          headers: { 'x-session': 's' },
          retries: 0,
        },
      } as never),
    );
    expect(error.message).toBe(
      "the timestamp authority at https://app.example/api/timestamp sent a token that doesn't answer this request: its nonce is not the one sent",
    );

    expect(seen).toHaveLength(1);
    const [{ request: sent, outcome }] = seen;
    expect(sent.body).toBeInstanceOf(Uint8Array);
    expect(sent).toMatchObject({
      url: 'https://app.example/api/timestamp',
      contentType: QUERY,
      headers: [{ name: 'x-session', value: 's' }],
      timeoutMs: 10_000,
      maxReplyBytes: 1024 * 1024,
    });
    // What the transport was handed is what reached the authority.
    expect(authority.queries).toEqual([sent.body]);
    expect(queriedDigest(sent.body).byteLength).toBe(32);
    expect(outcome.kind === 'replied' && outcome.body).toBeInstanceOf(Uint8Array);
    expect(signature.getSignature(FIELD)?.signed).toBe(false);
  });

  test('a granted timestamp: the signature is sealed with the token the route passed back', async () => {
    const authority = await stampingAuthority(true);
    const route = timestampRoute({ url: 'https://tsa.example/', fetch: authority.fetch });
    const signature = await signatureKernel();

    const signed: unknown = await signature.sign({
      field: FIELD,
      signingKey: await signingKey(),
      appearance: artwork,
      timestampAuthority: httpTimestampAuthority({
        url: 'https://app.example/api/timestamp',
        fetch: routeFetch(route),
      }),
    } as never);
    expect(signed).toMatchObject({
      status: 'completed',
      signature: { fieldName: 'sig', signed: true },
    });
    expect(authority.queries).toHaveLength(1);
    expect(queriedNonce(authority.queries[0]!)?.byteLength).toBe(8);
    const verdict = await signature.validateField(FIELD);
    expect(verdict.timestamp).toMatchObject({
      cryptography: 'valid',
      authorityName: 'EmbedPDF test timestamp authority',
    });
    expect(verdict.timestamp!.authorityCertificate).toEqual(authority.certificate);
  });

  test('a long-term fill: the revocation answers come through revocationRoute, and the verdict reads enabled', async () => {
    const ca = await createTestCertificateAuthority();
    const issued = await ca.issueSigner({ commonName: 'Ada' });
    const stamping = await stampingAuthority(true);
    const asked: string[] = [];
    const authorities = (async (url: string | URL | Request, init?: RequestInit) => {
      const address = String(url);
      const kind = address.endsWith('/ocsp') ? 'ocsp' : address.endsWith('/crl') ? 'crl' : 'issuer';
      asked.push(`${init?.method} ${address}`);
      const answer = await ca.revocation.fetch(
        kind === 'ocsp'
          ? { kind, url: address, body: new Uint8Array(init!.body as ArrayBuffer) }
          : { kind, url: address },
      );
      return new Response(new Uint8Array(answer), { status: 200 });
    }) as typeof globalThis.fetch;
    const revocation = revocationRoute({ fetch: authorities, lookup: null, cache: false });
    const timestamps = timestampRoute({ url: 'https://tsa.example/', fetch: stamping.fetch });
    const signature = await signatureKernel(
      signaturePlugin({
        trust: { anchors: async () => [ca.certificate, stamping.certificate] },
        revocation: httpRevocationSource({
          url: 'https://app.example/api/revocation',
          fetch: routeFetch(revocation),
        }),
        timestampAuthority: httpTimestampAuthority({
          url: 'https://app.example/api/timestamp',
          fetch: routeFetch(timestamps),
        }),
      } as never),
    );
    expect(signature.canAddValidationInfo()).toBe(true);

    const result = (await signature.sign({
      field: FIELD,
      signingKey: plainPort(issued),
      appearance: artwork,
    } as never)) as {
      validationInfo: { added: { ocspResponses: number }; documentTimestamp: unknown } | null;
      validationInfoError: unknown;
    };
    expect(result.validationInfoError).toBeNull();
    expect(result.validationInfo).toMatchObject({ documentTimestamp: null });
    expect(result.validationInfo!.added.ocspResponses).toBeGreaterThan(0);
    expect(asked.some((line) => line === 'POST http://test-authority.example/ocsp')).toBe(true);

    const [verdict] = await signature.validate();
    expect(verdict).toMatchObject({
      summary: 'valid',
      revocation: 'good',
      longTerm: { enabled: true, documentTimestampedAt: null, missing: [] },
    });
  });

  test('httpTimestampAuthority over fetch: the routes say no answer, and the client tries once more', async () => {
    let asked = 0;
    const authority = (async () => {
      asked++;
      return new Response(null, { status: 503 });
    }) as typeof globalThis.fetch;
    const route = timestampRoute({ url: 'https://tsa.example/', fetch: authority });
    const signature = await signatureKernel();

    const error = await refusal(
      signature.sign({
        field: FIELD,
        signingKey: await signingKey(),
        appearance: artwork,
        timestampAuthority: httpTimestampAuthority({
          url: 'https://app.example/api/timestamp',
          fetch: routeFetch(route),
        }),
      } as never),
    );
    expect(error.message).toBe(
      'the timestamp authority at https://app.example/api/timestamp did not answer: HTTP 503',
    );
    expect(asked).toBe(2);
  });
});
