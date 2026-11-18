// Upstream's timestampRoute and revocationRoute (D-2) on a loopback port, for
// SignatureTest.kt: the Kotlin transport posts to them over HTTP, as an app
// posts to its own server.
//
//   node platforms/android/tests/signature-routes.mjs <repository root>
//
// It prints one line, `READY <port>`, once it listens, and stops when its
// standard input closes. It reads `packages/core/signature/dist`, so build
// that package first (`pnpm --filter @embedpdf/core-signature build`).
//
// Behind the timestamp route is a stub authority that stamps the digest it
// is asked for with `createTestTimestampAuthority()`'s key. That authority
// writes no nonce (`buildTimestampToken`), and the Rust client checks the
// nonce, so while the stub signs, `TSTInfo.toSchema` sets the nonce read from
// the query (as `packages/core-wasm/test/signature-transport.test.ts` does).
// Behind the revocation route is `createTestCertificateAuthority()`.
//
//   GET  /material         the signer's chain and PKCS #8 key, the anchors
//   POST /api/timestamp    timestampRoute
//   POST /api/revocation   revocationRoute
//   GET  /log              what the stub authorities were asked
import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const root = resolve(process.argv[2] ?? '.');
const signatureDir = resolve(root, 'packages/core/signature');
const dist = (file) => import(pathToFileURL(resolve(signatureDir, 'dist', file)).href);
const { createTestCertificateAuthority, createTestTimestampAuthority } = await dist('index.js');
const { revocationRoute, timestampRoute } = await dist('server.js');
// The pkijs the package's dist imports; it has one entry for both conditions.
const require = createRequire(resolve(signatureDir, 'package.json'));
const pkijs = await import(pathToFileURL(require.resolve('pkijs')).href);

/** One DER element at `at`: its tag, where its contents start, where it ends. */
function element(bytes, at) {
  const tag = bytes[at];
  let length = bytes[at + 1];
  let start = at + 2;
  if (length & 0x80) {
    const octets = length & 0x7f;
    length = 0;
    for (let i = 0; i < octets; i++) length = length * 256 + bytes[start + i];
    start += octets;
  }
  return { tag, start, end: start + length };
}

/** TimeStampReq.messageImprint.hashedMessage. */
function queriedDigest(query) {
  const request = element(query, 0);
  const version = element(query, request.start);
  const imprint = element(query, version.end);
  const algorithm = element(query, imprint.start);
  const digest = element(query, algorithm.end);
  return query.slice(digest.start, digest.end);
}

/** TimeStampReq.nonce: the INTEGER after the imprint and an optional policy. */
function queriedNonce(query) {
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

function der(tag, contents) {
  const n = contents.byteLength;
  const length =
    n < 0x80 ? [n] : n < 0x100 ? [0x81, n] : n < 0x10000 ? [0x82, n >> 8, n & 0xff]
      : [0x83, n >> 16, (n >> 8) & 0xff, n & 0xff];
  const out = new Uint8Array(1 + length.length + n);
  out[0] = tag;
  out.set(length, 1);
  out.set(contents, 1 + length.length);
  return out;
}

/** TimeStampResp { status granted, token }. */
function granted(token) {
  const status = der(0x30, new Uint8Array([0x02, 0x01, 0x00]));
  const both = new Uint8Array(status.byteLength + token.byteLength);
  both.set(status);
  both.set(token, status.byteLength);
  return der(0x30, both);
}

let echoNonce = null;
const toSchema = pkijs.TSTInfo.prototype.toSchema;
pkijs.TSTInfo.prototype.toSchema = function () {
  // The serial number's class is the asn1js instance this pkijs encodes with.
  if (echoNonce) this.nonce = new this.serialNumber.constructor({ valueHex: echoNonce.slice().buffer });
  return toSchema.call(this);
};

const tsa = await createTestTimestampAuthority();
const ca = await createTestCertificateAuthority();
const signer = await ca.issueSigner({ commonName: 'Ada' });
const key = new Uint8Array(await crypto.subtle.exportKey('pkcs8', signer.privateKey));
const log = { timestampQueries: 0, nonces: [], asked: [] };

const stamping = async (_url, init) => {
  const query = new Uint8Array(await new Response(init.body).arrayBuffer());
  log.timestampQueries++;
  echoNonce = queriedNonce(query);
  log.nonces.push(echoNonce ? echoNonce.byteLength : 0);
  let token;
  try {
    token = await tsa.sign({ digest: queriedDigest(query), algorithm: 'sha256', subFilter: 'ETSI.RFC3161' });
  } finally {
    echoNonce = null;
  }
  return new Response(granted(token), { headers: { 'content-type': 'application/timestamp-reply' } });
};

const authorities = async (url, init) => {
  const address = String(url);
  const kind = address.endsWith('/ocsp') ? 'ocsp' : address.endsWith('/crl') ? 'crl' : 'issuer';
  log.asked.push(`${init?.method ?? 'GET'} ${address}`);
  const body = kind === 'ocsp' ? new Uint8Array(await new Response(init.body).arrayBuffer()) : undefined;
  const answer = await ca.revocation.fetch(kind === 'ocsp' ? { kind, url: address, body } : { kind, url: address });
  return new Response(new Uint8Array(answer), { status: 200 });
};

const routes = {
  '/api/timestamp': timestampRoute({ url: 'https://tsa.example/', fetch: stamping }),
  '/api/revocation': revocationRoute({ fetch: authorities, lookup: null, cache: false }),
};

const b64 = (bytes) => Buffer.from(bytes).toString('base64');
const json = (res, value) => {
  res.writeHead(200, { 'content-type': 'application/json' });
  res.end(JSON.stringify(value));
};

const server = createServer(async (req, res) => {
  try {
    const path = new URL(req.url, 'http://127.0.0.1').pathname;
    if (req.method === 'GET' && path === '/material') {
      return json(res, {
        chain: signer.certificateChain.map(b64),
        key: b64(key),
        anchors: [b64(ca.certificate), b64(tsa.certificate)],
        timestampCertificate: b64(tsa.certificate),
      });
    }
    if (req.method === 'GET' && path === '/log') return json(res, log);
    const route = routes[path];
    if (!route) {
      res.writeHead(404);
      return res.end();
    }
    const chunks = [];
    for await (const chunk of req) chunks.push(chunk);
    const headers = new Headers();
    for (const [name, value] of Object.entries(req.headers)) {
      if (typeof value === 'string') headers.set(name, value);
    }
    const answer = await route(
      new Request(`http://127.0.0.1${req.url}`, { method: req.method, headers, body: Buffer.concat(chunks) }),
    );
    res.writeHead(answer.status, Object.fromEntries(answer.headers));
    res.end(Buffer.from(await answer.arrayBuffer()));
  } catch (error) {
    res.writeHead(500);
    res.end(String(error?.stack ?? error));
  }
});

server.listen(0, '127.0.0.1', () => {
  process.stdout.write(`READY ${server.address().port}\n`);
});
process.stdin.on('end', () => server.close(() => process.exit(0)));
process.stdin.resume();
