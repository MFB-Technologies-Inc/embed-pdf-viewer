/**
 * `packages/core/signature`, the cryptographic half of digital signatures.
 * Replayed by `crates/plugins/signature-core/tests/conformance.rs`.
 *
 * The package is a library, not a plugin, but most of it is asynchronous
 * (WebCrypto), and function cases must be synchronous. So the parts that
 * return promises run as scenarios through a small workspace plugin,
 * `signature-core`, whose capability is one wrapper per exported function.
 * `parseDetachedCms`, `profileFor` and `certificateCommonName` are
 * synchronous and are function cases.
 *
 * Inputs come from `fixtures/signature/fixtures.json` (keys, certificates,
 * CMS blobs), generated once because key generation and ECDSA / RSA-PSS
 * signatures are random. Two adapter conventions:
 *
 *   - A point in time travels as the ISO 8601 string the API takes and
 *     returns (`signingTime`, `genTime`; 2ce3f7bc5).
 *   - The WebCrypto adapters (`webCryptoSigner`, `generateSigningKeyPair`,
 *     `selfSignedCertificate`, `createTestSigner`, `personalSigner`) run
 *     against a scripted WebCrypto: `generateKey` hands out fixture keys,
 *     `sign` signs RSASSA-PKCS1-v1_5 for real and answers other algorithms
 *     with fixed bytes, `getRandomValues` fills with 0x5a, and each call is
 *     logged as a callback. The Rust port takes WebCrypto as a host trait and
 *     its test host answers the same way.
 */
import { createHash, createPrivateKey, sign as nodeSign, webcrypto } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import type { AnyPlugin } from '@embedpdf/core';

import {
  attachSignatureTimestamp,
  completeSignature,
  addValidationInfo,
  buildDetachedCms,
  certificateCommonName,
  createTestCertificateAuthority,
  createTestSigner,
  ecdsaRawFromDer,
  generateSigningKeyPair,
  httpRevocationSource,
  httpTimestampAuthority,
  memoryKeyStore,
  parseDetachedCms,
  personalSigner,
  profileFor,
  remoteSigner,
  selfSignedCertificate,
  sign,
  validateChain,
  validateSignatures,
  verifyCmsSignature,
  verifyForCompletion,
  verifyTimestampToken,
  webCryptoSigner,
  type PersonalKeyRecord,
  type RawSigner,
} from '../../../../packages/core/signature/src/index';
import { buildTestToken } from '../../../../packages/core/signature/test/helpers/tokens';
import { certificateFromDer, parseCmsInternal } from '../../../../packages/core/signature/src/cms/parse';
import { timestampTokenAttribute } from '../../../../packages/core/signature/src/cms/attach';
import { addUnsignedAttribute } from '../../../../packages/core/signature/src/cms/splice';
import { signatureValueOf } from '../../../../packages/core/signature/src/cms/verify-timestamp';
import { checkRevocationAnswer, type RevocationAnswerCheck } from '../../../../packages/core/signature/src/revocation/answer';
import {
  buildCrl,
  buildOcspResponse,
  issueCertificate,
  type Issuer,
} from '../../../../packages/core/signature/src/signers/test-authority';
import { buildOcspRequest } from '../../../../packages/core/signature/src/revocation/request';

import type { FakeDocumentSpec, ResponseSpec } from '../fake-engine';
import { registerPlugin, type PluginEnv, type ScenarioCase, type Step } from '../scenario';
import type { FunctionCase, Suite } from '../vectors';

/** A point in time as the API spells it, from epoch milliseconds. */
const iso = (ms: number): string => new Date(ms).toISOString();
const genTime = (ms: number) => ({ kind: 'timestamp' as const, genTime: iso(ms), tokenVerified: true as const });

// ── fixtures ─────────────────────────────────────────────────────────────────

interface Fixtures {
  keys: Record<string, { kind: 'rsa' | 'p256' | 'p384' | 'p521'; pkcs8: string; spki: string }>;
  certs: Record<string, string>;
  cms: Record<string, string>;
  signatures: Record<string, string>;
}
const fixtures = JSON.parse(
  readFileSync(join(dirname(fileURLToPath(import.meta.url)), '../../fixtures/signature/fixtures.json'), 'utf8'),
) as Fixtures;

const bytesOf = (b64: string) => new Uint8Array(Buffer.from(b64, 'base64'));
const b64 = (bytes: Uint8Array) => Buffer.from(bytes).toString('base64');
const $b = (b64text: string) => ({ $bytes: b64text });
const cmsArg = (name: string) => {
  const value = fixtures.cms[name];
  if (!value) throw new Error(`[signature-core] no CMS fixture "${name}"`);
  return $b(value);
};
const certArg = (name: string) => {
  const value = fixtures.certs[name];
  if (!value) throw new Error(`[signature-core] no certificate fixture "${name}"`);
  return $b(value);
};
const sha256hex = (bytes: Uint8Array) => createHash('sha256').update(bytes).digest('hex');
const digestBytes = (n: number, seed: number) => new Uint8Array(n).map((_, i) => (i * 7 + seed) & 0xff);
const DIGEST_LENGTH = { sha256: 32, sha384: 48, sha512: 64 } as const;

/** The raw signature a fixture key makes over `data`: RSASSA-PKCS1-v1_5 is
 *  deterministic and is computed; RSA-PSS and ECDSA come from the table the
 *  fixture script recorded. */
function rawSignature(key: string, algorithm: RawSigner['algorithm'], hash: RawSigner['hash'], data: Uint8Array): Uint8Array {
  if (algorithm === 'RSA-PKCS1-v1_5') {
    const pk = createPrivateKey({ key: Buffer.from(fixtures.keys[key].pkcs8, 'base64'), format: 'der', type: 'pkcs8' });
    return new Uint8Array(nodeSign(hash, data, pk));
  }
  const recorded = fixtures.signatures[sha256hex(data)];
  if (!recorded) throw new Error(`[signature-core] no recorded ${algorithm} signature for these bytes; rerun fixtures/signature/generate.ts`);
  return bytesOf(recorded);
}

/** What a raw signer must answer for one build: run the build once with a
 *  capturing signer, and sign the bytes it asked for. */
async function signatureFor(input: {
  key: string;
  chain: string[];
  algorithm: RawSigner['algorithm'];
  hash: RawSigner['hash'];
  profile: 'pkcs7' | 'cades-b';
  digest: Uint8Array;
  signingTime?: number;
}): Promise<string> {
  let answer: Uint8Array | null = null;
  await buildDetachedCms({
    digest: input.digest,
    hash: input.hash,
    profile: input.profile,
    signingTime: input.signingTime === undefined ? undefined : iso(input.signingTime),
    signer: {
      kind: 'raw',
      certificateChain: input.chain.map((c) => bytesOf(fixtures.certs[c])),
      algorithm: input.algorithm,
      hash: input.hash,
      sign: async (data) => (answer = rawSignature(input.key, input.algorithm, input.hash, data)),
    },
  });
  return b64(answer!);
}

// ── the scripted WebCrypto ───────────────────────────────────────────────────

type Log = PluginEnv['log'];
type Subtle = Record<string, unknown>;

async function fixtureKeyPair(params: { name: string; hash?: unknown; namedCurve?: string }, extractable: boolean, fixture = 'leaf') {
  const subtle = webcrypto.subtle;
  const hashName = typeof params.hash === 'string' ? params.hash : (params.hash as { name?: string } | undefined)?.name;
  if (params.name === 'ECDSA') {
    const key = { 'P-256': 'p256', 'P-384': 'p384', 'P-521': 'p521' }[params.namedCurve ?? 'P-256'] ?? 'p256';
    const algorithm = { name: 'ECDSA', namedCurve: params.namedCurve ?? 'P-256' };
    return {
      privateKey: await subtle.importKey('pkcs8', bytesOf(fixtures.keys[key].pkcs8), algorithm, extractable, ['sign']),
      publicKey: await subtle.importKey('spki', bytesOf(fixtures.keys[key].spki), algorithm, true, ['verify']),
    };
  }
  const algorithm = { name: params.name, hash: hashName ?? 'SHA-256' };
  return {
    privateKey: await subtle.importKey('pkcs8', bytesOf(fixtures.keys[fixture].pkcs8), algorithm, extractable, ['sign']),
    publicKey: await subtle.importKey('spki', bytesOf(fixtures.keys[fixture].spki), algorithm, true, ['verify']),
  };
}

const describeKey = (key: CryptoKey) => ({
  type: key.type,
  extractable: key.extractable,
  algorithm: key.algorithm,
  usages: key.usages,
});

/** Fixed bytes for a signature WebCrypto would randomise. */
function fakeSignature(key: CryptoKey): Uint8Array {
  const algorithm = key.algorithm as { name: string; namedCurve?: string };
  if (algorithm.name === 'ECDSA') {
    const size = { 'P-256': 64, 'P-384': 96, 'P-521': 132 }[algorithm.namedCurve ?? 'P-256'] ?? 64;
    return new Uint8Array(size).map((_, i) => (i * 3 + 1) & 0xff);
  }
  return new Uint8Array(256).map((_, i) => (i * 5 + 2) & 0xff);
}

async function withWebCrypto<T>(log: Log, run: () => Promise<T>): Promise<T> {
  const subtle = webcrypto.subtle as unknown as Subtle;
  const real = {
    sign: webcrypto.subtle.sign.bind(webcrypto.subtle),
    exportKey: webcrypto.subtle.exportKey.bind(webcrypto.subtle),
  };
  const cryptoObject = globalThis.crypto as unknown as Record<string, unknown>;
  const realRandom = cryptoObject.getRandomValues;
  subtle.generateKey = async (params: { name: string }, extractable: boolean, usages: string[]) => {
    log('webcrypto.generateKey', [params, extractable, usages]);
    return fixtureKeyPair(params, extractable);
  };
  subtle.sign = async (params: { name: string }, key: CryptoKey, data: BufferSource) => {
    const bytes = new Uint8Array(ArrayBuffer.isView(data) ? data.buffer.slice(data.byteOffset, data.byteOffset + data.byteLength) : data);
    log('webcrypto.sign', [params, describeKey(key), bytes]);
    if (params.name === 'RSASSA-PKCS1-v1_5') return real.sign(params, key, bytes);
    return fakeSignature(key).buffer;
  };
  subtle.exportKey = async (format: 'spki', key: CryptoKey) => {
    log('webcrypto.exportKey', [format, describeKey(key)]);
    return real.exportKey(format, key);
  };
  cryptoObject.getRandomValues = <A extends ArrayBufferView>(array: A): A => {
    log('webcrypto.getRandomValues', [array.byteLength]);
    new Uint8Array(array.buffer, array.byteOffset, array.byteLength).fill(0x5a);
    return array;
  };
  try {
    return await run();
  } finally {
    delete subtle.generateKey;
    delete subtle.sign;
    delete subtle.exportKey;
    cryptoObject.getRandomValues = realRandom;
  }
}

/** A private key described by the vector: a fixture key imported with an
 *  algorithm, or (`fake`) an object with only `algorithm`, for algorithms
 *  WebCrypto would not import. */
interface KeySpec {
  readonly fixture?: string;
  readonly algorithm: { name: string; hash?: string; namedCurve?: string };
  readonly fake?: boolean;
}
async function importKeySpec(spec: KeySpec): Promise<CryptoKey> {
  if (spec.fake) return { algorithm: spec.algorithm } as unknown as CryptoKey;
  const fixture = fixtures.keys[spec.fixture ?? 'leaf'];
  return webcrypto.subtle.importKey('pkcs8', bytesOf(fixture.pkcs8), spec.algorithm, false, ['sign']);
}

const describeSigner = (signer: RawSigner) => ({
  kind: signer.kind,
  certificateChain: signer.certificateChain,
  algorithm: signer.algorithm,
  hash: signer.hash,
});

// ── the harness plugin ───────────────────────────────────────────────────────


// ── the D-1 transport, scripted ──────────────────────────────────────────────

/** One reply of a scripted endpoint: an HTTP status and body, or no answer (`fail`). */
type ScriptedReply = { status: number; body?: Uint8Array } | { fail: string };

/**
 * A `fetch` that answers from `replies` in order and records each request as the D-1 transport
 * sees it: the URL, the content type, the other headers in order, and the body's bytes.
 */
function scriptedFetch(replies: ScriptedReply[]) {
  const sent: { url: string; contentType: string | null; headers: { name: string; value: string }[]; body: Uint8Array }[] = [];
  let next = 0;
  const fetch = (async (url: RequestInfo | URL, init?: RequestInit) => {
    let contentType: string | null = null;
    const headers: { name: string; value: string }[] = [];
    for (const [name, value] of Object.entries((init?.headers ?? {}) as Record<string, string>)) {
      if (name === 'content-type') contentType = value;
      else headers.push({ name, value });
    }
    const raw = init?.body;
    const body = typeof raw === 'string' ? new TextEncoder().encode(raw) : new Uint8Array(raw as Uint8Array);
    sent.push({ url: String(url), contentType, headers, body });
    const reply = replies[next++];
    if (!reply) throw new TypeError('no recorded reply is left');
    if ('fail' in reply) throw new TypeError(reply.fail);
    return new Response(reply.body ? new Uint8Array(reply.body) : null, { status: reply.status });
  }) as typeof globalThis.fetch;
  return { fetch, sent };
}

/** A revocation answer check as plain JSON: its times as ISO strings. */
function answerJson(check: RevocationAnswerCheck) {
  if (check.says === 'nothing') return { says: check.says, kind: check.kind, problem: check.problem, detail: check.detail };
  return {
    says: check.says,
    kind: check.kind,
    thisUpdate: check.thisUpdate.toISOString(),
    nextUpdate: check.nextUpdate ? check.nextUpdate.toISOString() : null,
    responder: check.responder,
    ...(check.says === 'revoked' ? { revokedAt: check.revokedAt.toISOString(), reason: check.reason } : {}),
  };
}

/** An error as the probe reports it: its code, message and details. */
function errorOf(error: unknown) {
  const e = error as { code?: unknown; message?: unknown; details?: unknown };
  return { code: e.code ?? null, message: e.message ?? String(error), details: e.details ?? null };
}

function signaturePlugin(env: PluginEnv): AnyPlugin {
  return {
    id: 'signature-core',
    token: env.token('signature-core'),
    create: (ctx) => ({
      api: {
      verifyCmsSignature: (cms: Uint8Array) => verifyCmsSignature(cms),
      buildDetachedCms: (input: Parameters<typeof buildDetachedCms>[0]) => buildDetachedCms(input),
      verifyForCompletion: (input: Parameters<typeof verifyForCompletion>[0]) => verifyForCompletion(input),
      validateChain: (cms: Uint8Array, trust: Parameters<typeof validateChain>[1], at?: Parameters<typeof validateChain>[2]) =>
        validateChain(cms, trust, at),
      sign: (documentId: string, input: Parameters<typeof sign>[1]) => sign(ctx.documentHandle(documentId)!, input),
      validateSignatures: (
        documentId: string,
        options?: Parameters<typeof validateSignatures>[1],
      ) => validateSignatures(ctx.documentHandle(documentId)!, options),
      remoteSign: (port: Parameters<typeof remoteSigner>[0], request: Parameters<ReturnType<typeof remoteSigner>['sign']>[0]) => {
        const signer = remoteSigner(port);
        return signer.sign(request);
      },
      timestampHttp: (
        options: Omit<Parameters<typeof httpTimestampAuthority>[0], 'fetch'>,
        request: Parameters<ReturnType<typeof httpTimestampAuthority>['sign']>[0],
        replies: ScriptedReply[],
      ) =>
        withWebCrypto(env.log, async () => {
          const { fetch, sent } = scriptedFetch(replies);
          try {
            return { sent, token: await httpTimestampAuthority({ ...options, fetch }).sign(request) };
          } catch (error) {
            return { sent, error: errorOf(error) };
          }
        }),
      revocationRoute: (
        options: Omit<Parameters<typeof httpRevocationSource>[0], 'fetch'>,
        request: Parameters<ReturnType<typeof httpRevocationSource>['fetch']>[0],
        replies: ScriptedReply[],
      ) =>
        (async () => {
          const { fetch, sent } = scriptedFetch(replies);
          try {
            return { sent, answer: await httpRevocationSource({ ...options, fetch }).fetch(request) };
          } catch (error) {
            return { sent, error: errorOf(error) };
          }
        })(),
      verifyTimestampToken: (input: Parameters<typeof verifyTimestampToken>[0]) => verifyTimestampToken(input),
      attachSignatureTimestamp: (input: Parameters<typeof attachSignatureTimestamp>[0]) => attachSignatureTimestamp(input),
      completeSignature: (documentId: string, input: Parameters<typeof completeSignature>[1]) =>
        completeSignature(ctx.documentHandle(documentId)!, input),
      checkRevocationAnswer: async (input: { kind: 'ocsp' | 'crl'; der: Uint8Array; certificate: Uint8Array; issuer: Uint8Array }) =>
        answerJson(
          await checkRevocationAnswer({
            answer: { kind: input.kind, der: input.der },
            certificate: certificateFromDer(input.certificate),
            issuer: certificateFromDer(input.issuer),
          }),
        ),
      addValidationInfo: (documentId: string, options: Parameters<typeof addValidationInfo>[1]) =>
        addValidationInfo(ctx.documentHandle(documentId)!, options),
      ecdsaRawFromDer: (der: Uint8Array, curve: Parameters<typeof ecdsaRawFromDer>[1]) => ecdsaRawFromDer(der, curve),
      memoryKeyStore: async (ops: { op: 'load' | 'save' | 'remove'; subject: string; record?: unknown }[]) => {
        const store = memoryKeyStore();
        const results: unknown[] = [];
        for (const step of ops) {
          if (step.op === 'load') results.push(await store.load(step.subject));
          else if (step.op === 'save') results.push(await store.save(step.subject, step.record as PersonalKeyRecord));
          else results.push(await store.remove(step.subject));
        }
        return results;
      },
      webCryptoSigner: (key: KeySpec, certificateChain: Uint8Array[], hash?: RawSigner['hash'], data?: Uint8Array) =>
        withWebCrypto(env.log, async () => {
          const signer = webCryptoSigner({ privateKey: await importKeySpec(key), certificateChain, hash });
          const described = describeSigner(signer);
          return data === undefined ? described : { ...described, signature: await signer.sign(data) };
        }),
      generateSigningKeyPair: (
        algorithm: Parameters<typeof generateSigningKeyPair>[0],
        hash: Parameters<typeof generateSigningKeyPair>[1],
        extractable: boolean,
      ) =>
        withWebCrypto(env.log, async () => {
          const pair = await generateSigningKeyPair(algorithm, hash, extractable);
          return { privateKey: describeKey(pair.privateKey), publicKey: describeKey(pair.publicKey) };
        }),
      selfSignedCertificate: (input: {
        key: KeySpec;
        commonName: string;
        hash: 'sha256' | 'sha384' | 'sha512';
        validityDays?: number;
      }) =>
        withWebCrypto(env.log, async () => {
          const pair = await fixtureKeyPair({ ...input.key.algorithm, hash: input.key.algorithm.hash }, false, input.key.fixture);
          return selfSignedCertificate({
            publicKey: pair.publicKey,
            privateKey: pair.privateKey,
            commonName: input.commonName,
            hash: input.hash,
            validityDays: input.validityDays,
          });
        }),
      createTestSigner: (options?: Parameters<typeof createTestSigner>[0], data?: Uint8Array) =>
        withWebCrypto(env.log, async () => {
          const signer = await createTestSigner(options);
          const described = { ...describeSigner(signer), certificate: signer.certificate, privateKey: describeKey(signer.privateKey) };
          return data === undefined ? described : { ...described, signature: await signer.sign(data) };
        }),
      personalSigner: (
        input: Omit<Parameters<typeof personalSigner>[0], 'store'> & { store?: Parameters<typeof personalSigner>[0]['store'];
          },
        times = 1,
      ) =>
        withWebCrypto(env.log, async () => {
          const store = input.store ?? memoryKeyStore();
          const out: unknown[] = [];
          for (let i = 0; i < times; i += 1) {
            const signer = await personalSigner({ ...input, store });
            out.push({ ...describeSigner(signer), certificate: signer.certificate, subject: signer.subject });
          }
          return out;
        }),
      },
    }),
  };
}

registerPlugin('signature-core', { factory: (_config, env) => signaturePlugin(env) });

// ── function cases ───────────────────────────────────────────────────────────

const parseFns = {
  parseDetachedCms: (cms: Uint8Array) => parseDetachedCms(cms),
  profileFor: (subFilter: 'adbe.pkcs7.detached' | 'ETSI.CAdES.detached') => profileFor(subFilter),
  certificateCommonName: (der: Uint8Array) => certificateCommonName(der),
};

const parseCases: FunctionCase[] = [
  ...Object.keys(fixtures.cms).map((name) => ({
    name: `parse-${name}`,
    fn: 'parseDetachedCms',
    args: [cmsArg(name)],
  })),
  {
    name: 'parse-empty',
    covers: 'An empty CMS is refused before BER parsing.',
    fn: 'parseDetachedCms',
    args: [$b('')],
  },
  {
    name: 'parse-trailing-zeros',
    covers: 'The /Contents padding after the CMS is ignored: BER parsing reads the first element.',
    fn: 'parseDetachedCms',
    args: [$b(b64(new Uint8Array([...bytesOf(fixtures.cms.rsaCades), ...new Uint8Array(64)])))],
  },
  { name: 'profile-pkcs7', fn: 'profileFor', args: ['adbe.pkcs7.detached'] },
  { name: 'profile-cades', fn: 'profileFor', args: ['ETSI.CAdES.detached'] },
  ...[
    'leaf', 'root', 'corpus', 'noCommonName', 'emptyCommonName', 'integerCommonName', 'bmpCommonName', 'leafP256',
    'universalStringCommonName', 'utf8AccentedCommonName', 'rdnSequence', 'attributeAsSet', 'attributeOneMember', 'signatureAsOctets',
  ].map((name) => ({
    name: `common-name-${name}`,
    fn: 'certificateCommonName',
    args: [certArg(name)],
  })),
  {
    name: 'common-name-not-der',
    covers: "A certificate that is not BER throws pkijs's AsnError.",
    fn: 'certificateCommonName',
    args: [$b('AQID')],
  },
  {
    name: 'common-name-not-certificate',
    covers: 'BER that is not a certificate throws.',
    fn: 'certificateCommonName',
    args: [$b(b64(new Uint8Array([0x30, 0x03, 0x02, 0x01, 0x05])))],
  },
];

// ── scenario helpers ─────────────────────────────────────────────────────────

const plugins = [{ factory: 'signature-core' }];
const JAN_2026 = Date.UTC(2026, 0, 15);
const start: Step = { op: 'call', target: 'kernel', method: 'start' };
const toJan2026: Step = { op: 'advance', ms: JAN_2026 };
const openDoc: Step = { op: 'call', target: 'documents', method: 'open', args: [{ kind: 'bytes', id: 'd', bytes: $b('') }] };
const call = (method: string, ...args: unknown[]): Step => ({ op: 'call', target: { token: 'signature-core' }, method, args });

/** Why these cases record no state; see `ScenarioCase` in `../scenario`. */
const UNSNAPSHOTTED = {
  snapshot: 'none',
  reason:
    'signature-core is a probe plugin with no slice. Each case compares the outcome and settled value of the core function it calls, and any engine reads it makes.',
} as const;

/** One scenario: the plugin, the clock at 2026-01-15, then `steps`. */
function scenario(name: string, covers: string, steps: Step[], engine?: Record<string, FakeDocumentSpec>): ScenarioCase {
  return {
    name,
    covers,
    plugins,
    ...UNSNAPSHOTTED,
    awaitCalls: true,
    ...(engine ? { engine: { documents: engine } } : {}),
    steps: [start, toJan2026, ...(engine ? [openDoc] : []), ...steps],
  };
}

const rawSignerArg = (chain: string[], algorithm: RawSigner['algorithm'], hash: RawSigner['hash'], answer: unknown) => ({
  kind: 'raw',
  certificateChain: chain.map(certArg),
  algorithm,
  hash,
  sign: { $fn: 'raw.sign', ...(answer as object) },
});

const prepared = (over: Record<string, unknown> = {}) => ({
  signingId: 'signing-1',
  digest: $b(b64(digestBytes(32, 3))),
  algorithm: 'sha256',
  byteRange: [0, 100, 8292, 50],
  contentsSizeBytes: 8192,
  subFilter: 'ETSI.CAdES.detached',
  expectedVersion: { baseSha256: 'base', editsVersion: 0 },
  expiresAt: null,
  ...over,
});

// ── verify ───────────────────────────────────────────────────────────────────

const verifyNames = [
  'rsaCades', 'rsaPkcs7', 'rsaSha384', 'rsaSha512', 'psssha256', 'psssha384', 'psssha512', 'rsa8192', 'pss8192',
  'ecP256', 'ecP384', 'ecP521', 'ecP256Sha512', 'pdfCertified', 'pdfApproval', 'pdfCompat', 'skiSid', 'withTimestamp',
  'unknownSignatureAlgorithm', 'sha1Digest', 'rsaCadesTampered', 'ecP256Tampered', 'ecP256GarbageSignature',
  'ecP256NotSequence', 'rsaShortSignature', 'essMismatch', 'essExplicitSha256', 'essSha1', 'essNoHash',
  'contentTypeNotData', 'contentTypeEmpty', 'noSignedAttrs', 'unknownDigest', 'twoSigners', 'noCertificates',
  'notSignedData', 'truncated', 'unknownCurve', 'ecNoParams', 'dsaKey',
  // Keys labelled with algorithms that are not key algorithms, then ECDSA and RSA-PSS encodings.
  'rsaKeyAsSha256WithRsa', 'rsaKeyAsOaep', 'rsaKeyAsEcdh', 'rsaKeyAsAesCbc', 'rsaKeyAsAesGcm', 'rsaKeyAsAesCfb', 'rsaKeyAsAesKw',
  'rsaKeyAsHmac', 'rsaKeyAsDh', 'rsaKeyAsSha1', 'rsaKeyAsPbkdf2', 'ecKeyAsEcdsaSha512', 'ecdsaSAsOctets', 'ecdsaRPadded',
  'ecdsaZeroIntegers', 'ecP521Sha384', 'pssSaltZero', 'pssSalt256', 'pssSha1', 'pssSha1ParamsOverSha256', 'pssUnknownHash',
];
const verifyCases: ScenarioCase[] = verifyNames.map((name) =>
  scenario(`verify-${name}`, `verifyCmsSignature over the ${name} fixture.`, [call('verifyCmsSignature', cmsArg(name))]),
);

// ── build ────────────────────────────────────────────────────────────────────

const leafChain = ['leaf', 'inter'];
const buildInput = async (over: {
  key?: string;
  chain?: string[];
  algorithm?: RawSigner['algorithm'];
  hash?: RawSigner['hash'];
  signerHash?: RawSigner['hash'];
  profile?: 'pkcs7' | 'cades-b';
  signingTime?: number;
  digest?: Uint8Array;
  answer?: unknown;
}) => {
  const hash = over.hash ?? 'sha256';
  const signerHash = over.signerHash ?? hash;
  const algorithm = over.algorithm ?? 'RSA-PKCS1-v1_5';
  const chain = over.chain ?? leafChain;
  const profile = over.profile ?? 'cades-b';
  const digest = over.digest ?? digestBytes(DIGEST_LENGTH[hash], 3);
  const answer =
    over.answer ??
    ({
      resolves: $b(
        await signatureFor({ key: over.key ?? 'leaf', chain, algorithm, hash, profile, digest, signingTime: over.signingTime }),
      ),
    } as const);
  return {
    digest: $b(b64(digest)),
    hash,
    profile,
    ...(over.signingTime === undefined ? {} : { signingTime: iso(over.signingTime) }),
    signer: rawSignerArg(chain, algorithm, signerHash, answer),
  };
};

const SEPT_2026 = Date.UTC(2026, 8, 11);
const SIGNING_TIMES = [
  Date.UTC(1950, 0, 1, 0, 0, 1), Date.UTC(1970, 0, 1), Date.UTC(1984, 1, 29, 6, 7, 8), Date.UTC(1999, 11, 31, 23, 59, 59, 999),
  Date.UTC(2000, 0, 1), Date.UTC(2000, 1, 29, 13, 45, 27), Date.UTC(2000, 2, 1, 1, 2, 3), Date.UTC(2023, 11, 31, 12),
  Date.UTC(2024, 0, 1), Date.UTC(2024, 1, 28), Date.UTC(2024, 1, 29), Date.UTC(2024, 2, 1), Date.UTC(2049, 11, 31, 23, 59, 59),
  ...Array.from({ length: 12 }, (_, month) => Date.UTC(1988, month, 1, month, month * 5, month * 4)),
  ...Array.from({ length: 12 }, (_, month) => Date.UTC(2037, month, 31 - (month % 3), 23 - month, 59 - month, 1 + month)),
  // Outside 1900 to 2099 asn1js writes a year of more than two characters as nothing, or a negative one.
  Date.UTC(2100, 0, 1, 1, 1, 1), Date.UTC(1899, 11, 31, 22, 58, 57), Date.UTC(1890, 5, 15),
  // Before 1 March of year 0 the day count is negative.
  Date.UTC(-5, 6, 20, 3, 4, 5), Date.UTC(-1, 1, 28, 12),
];
// A PKCS#7 build whose signer answers nothing: only the attributes matter.
const PKCS7_NO_SIGNATURE = await buildInput({ profile: 'pkcs7', answer: { resolves: $b('') } });
const buildCases: ScenarioCase[] = [
  scenario('build-rsa-cades', 'A CAdES-B CMS: content-type, message-digest and ESS signing-certificate-v2, signed over the attribute SET.', [
    call('buildDetachedCms', await buildInput({})),
  ]),
  scenario('build-rsa-pkcs7-time', 'A PKCS#7 CMS carries the signing-time attribute it was given, as UTCTime.', [
    call('buildDetachedCms', await buildInput({ profile: 'pkcs7', signingTime: SEPT_2026 })),
  ]),
  scenario('build-rsa-pkcs7-no-time', 'A PKCS#7 CMS without a signing time has only content-type and message-digest.', [
    call('buildDetachedCms', await buildInput({ profile: 'pkcs7' })),
  ]),
  scenario('build-cades-ignores-time', 'CAdES never carries signing-time, even when one is passed.', [
    call('buildDetachedCms', await buildInput({ signingTime: SEPT_2026 })),
  ]),
  scenario('build-rsa-sha384', 'sha384WithRSAEncryption and a 48-byte digest.', [
    call('buildDetachedCms', await buildInput({ hash: 'sha384' })),
  ]),
  scenario('build-rsa-sha512', 'sha512WithRSAEncryption and a 64-byte digest.', [
    call('buildDetachedCms', await buildInput({ hash: 'sha512' })),
  ]),
  ...(['sha256', 'sha384', 'sha512'] as const).map((hash) =>
    scenario(`build-pss-${hash}`, `RSASSA-PSS parameters for ${hash}: hash, MGF1 over the same hash, salt the hash length.`, []),
  ),
  scenario('build-ecdsa-p256', 'ECDSA: the raw r||s becomes a DER SEQUENCE of two INTEGERs.', []),
  scenario('build-ecdsa-p384', 'ECDSA over P-384 with SHA-384.', []),
  scenario('build-ecdsa-p521', 'ECDSA over P-521 in a PKCS#7 CMS; r and s are 66 bytes each.', []),
  scenario('build-ecdsa-odd-length', 'An ECDSA answer of odd length encodes as an empty signature.', [
    call('buildDetachedCms', await buildInput({ algorithm: 'ECDSA', chain: ['leafP256'], answer: { resolves: $b('AQID') } })),
  ]),
  scenario('build-ecdsa-leading-zeros', 'Leading zero bytes of r and s are stripped, and a high bit gets a zero byte.', [
    call(
      'buildDetachedCms',
      await buildInput({
        algorithm: 'ECDSA',
        chain: ['leafP256'],
        answer: { resolves: $b(b64(new Uint8Array(64).map((_, i) => (i < 3 ? 0 : i === 32 ? 0x80 : i)))) },
      }),
    ),
  ]),
  scenario('build-wrong-digest-length', 'A digest whose length does not fit the hash is refused before anything is signed.', [
    call('buildDetachedCms', await buildInput({ digest: digestBytes(20, 3), answer: { resolves: $b('') } })),
  ]),
  scenario('build-signer-hash-mismatch', 'A signer that hashes with another algorithm than the document was prepared with is refused.', [
    call('buildDetachedCms', await buildInput({ signerHash: 'sha384', answer: { resolves: $b('') } })),
  ]),
  scenario('build-no-certificate', 'A signer without a certificate is refused.', [
    call('buildDetachedCms', await buildInput({ chain: [], answer: { resolves: $b('') } })),
  ]),
  scenario('build-signer-rejects', 'A signer that rejects rejects the build with its error.', [
    call('buildDetachedCms', await buildInput({ answer: { rejects: { name: 'NotAllowedError', message: 'user cancelled' } } })),
  ]),
  scenario('build-signature-128-bytes', 'A 128-byte signature takes a long-form length.', [
    call('buildDetachedCms', await buildInput({ answer: { resolves: $b(b64(new Uint8Array(128).map((_, i) => i))) } })),
  ]),
  scenario(
    'build-signing-times',
    'Signing times across the UTCTime range: leap days, era and month edges, and a time of day.',
    SIGNING_TIMES.map((signingTime) => call('buildDetachedCms', { ...PKCS7_NO_SIGNATURE, signingTime: iso(signingTime) })),
  ),
  scenario('build-bad-certificate',
    "A chain entry that is not a certificate throws pkijs's error.", [
    call('buildDetachedCms', {
      ...(await buildInput({ answer: { resolves: $b('') } })),
      signer: { kind: 'raw', certificateChain: [$b('AQID')], algorithm: 'RSA-PKCS1-v1_5', hash: 'sha256', sign: { $fn: 'raw.sign', resolves: $b('') } },
    }),
  ]),
];
// The PSS and ECDSA builds answer from the recorded signatures, keyed by the bytes signed.
const recorded: Record<string, Parameters<typeof buildInput>[0]> = {
  'build-pss-sha256': { algorithm: 'RSA-PSS', hash: 'sha256' },
  'build-pss-sha384': { algorithm: 'RSA-PSS', hash: 'sha384' },
  'build-pss-sha512': { algorithm: 'RSA-PSS', hash: 'sha512' },
  'build-ecdsa-p256': { algorithm: 'ECDSA', key: 'p256', chain: ['leafP256', 'inter'] },
  'build-ecdsa-p384': { algorithm: 'ECDSA', key: 'p384', chain: ['leafP384', 'inter'], hash: 'sha384' },
  'build-ecdsa-p521': { algorithm: 'ECDSA', key: 'p521', chain: ['leafP521', 'inter'], hash: 'sha512', profile: 'pkcs7' },
};
for (const scenarioCase of buildCases) {
  const over = recorded[scenarioCase.name];
  if (over) (scenarioCase.steps as Step[]).push(call('buildDetachedCms', await buildInput(over)));
}

// ── the completion gate ──────────────────────────────────────────────────────

const gate = (cms: unknown, profile: 'pkcs7' | 'cades-b', over: Record<string, unknown> = {}) =>
  call('verifyForCompletion', { cms, prepared: prepared(over), profile });
const gateCases: ScenarioCase[] = [
  scenario('gate-cades-ok', 'A CAdES CMS over the prepared digest passes.', [gate(cmsArg('rsaCades'), 'cades-b')]),
  scenario('gate-pkcs7-ok', 'A PKCS#7 CMS passes the PKCS#7 profile, signing-time and all.', [gate(cmsArg('rsaPkcs7'), 'pkcs7')]),
  scenario('gate-too-large', 'A CMS bigger than the reserved /Contents is refused first.', [gate(cmsArg('rsaCades'), 'cades-b', { contentsSizeBytes: 100 })]),
  scenario('gate-malformed',
    "A CMS that does not parse is `malformed`, with the parser's message.", [gate(cmsArg('twoSigners'), 'cades-b')]),
  scenario('gate-malformed-type-error', 'A parser failure that is not a CmsError reports its text.', [gate(cmsArg('contentTypeEmpty'), 'cades-b')]),
  scenario('gate-algorithm-mismatch', 'A CMS digesting with another algorithm than prepared.', [gate(cmsArg('rsaSha384'), 'cades-b')]),
  scenario('gate-digest-mismatch', 'A CMS whose message digest is not the prepared digest.', [
    gate(cmsArg('rsaCades'), 'cades-b', { digest: $b(b64(digestBytes(32, 4))) }),
  ]),
  scenario('gate-digest-length-mismatch', 'A prepared digest of another length is a digest mismatch, not an error.', [
    gate(cmsArg('rsaCades'), 'cades-b', { digest: $b(b64(digestBytes(31, 3))) }),
  ]),
  scenario('gate-cades-without-ess', 'CAdES requires signing-certificate-v2.', [gate(cmsArg('rsaPkcs7NoTime'), 'cades-b')]),
  scenario('gate-cades-with-time', 'PAdES forbids signing-time; checked after the ESS attribute.', [gate(cmsArg('cadesWithTime'), 'cades-b')]),
  scenario('gate-pkcs7-with-time', 'The same CMS is refused for its signature, not its profile, under PKCS#7.', [gate(cmsArg('generalizedTime'), 'pkcs7')]),
  scenario('gate-signature-invalid', 'A signature that does not verify.', [gate(cmsArg('rsaCadesTampered'), 'cades-b')]),
  scenario('gate-exact-size', 'A CMS exactly as large as the reserved /Contents fits.', [
    gate(cmsArg('rsaCades'), 'cades-b', { contentsSizeBytes: bytesOf(fixtures.cms.rsaCades).length }),
  ]),
  scenario('gate-fractional-size', 'A reserved size that is not whole prints as JavaScript prints it.', [
    gate(cmsArg('rsaCades'), 'cades-b', { contentsSizeBytes: 100.5 }),
  ]),
  scenario('gate-signature-unsupported', 'An unsupported algorithm is refused with its own detail.', [gate(cmsArg('unknownCurve'), 'cades-b')]),
];

// ── trust ────────────────────────────────────────────────────────────────────

const anchors = (...names: string[]) => ({ anchors: { $fn: 'trust.anchors', resolves: names.map(certArg) } });
const chain = (cms: string, trust: unknown, at?: unknown) =>
  at === undefined ? call('validateChain', cmsArg(cms), trust) : call('validateChain', cmsArg(cms), trust, at);
const trustCases: ScenarioCase[] = [
  scenario('trust-no-port', 'No trust port is `unknown`: nothing was checked.', [chain('rsaCades', null)]),
  scenario('trust-no-anchors', 'A port with no anchors is `unknown` too.', [chain('rsaCades', anchors())]),
  scenario('trust-anchors-reject', 'A port whose anchors() rejects rejects the validation.', [
    chain('rsaCades', { anchors: { $fn: 'trust.anchors', rejects: { message: 'trust list unavailable' } } }),
  ]),
  scenario('trust-bad-anchor', 'An anchor that is not a certificate rejects the validation.', [
    chain('rsaCades', { anchors: { $fn: 'trust.anchors', resolves: [$b('AQID')] } }),
  ]),
  scenario('trust-unparseable-cms',
    "A CMS that does not parse is `untrusted` with the parser's message.", [chain('twoSigners', anchors('root'))]),
  scenario('trust-root', 'The chain engine starts from the LAST certificate the CMS carries (the intermediate here) and reaches the root.', [
    chain('rsaCades', anchors('root')),
  ]),
  scenario('trust-root-with-root-carried', 'A carried copy of the anchor is dropped before the path is built.', [chain('withRoot', anchors('root'))]),
  scenario('trust-intermediate-last', 'Carried order decides the start: [leaf, root, inter] starts from the intermediate.', [
    chain('interFirst', anchors('root')),
  ]),
  scenario('trust-expired-leaf-not-checked', 'An expired signer followed by a valid intermediate is trusted: the signer is never on the path.', [
    chain('leafExpired', anchors('root')),
  ]),
  scenario('trust-leaf-only-root', 'A lone signer whose issuer is neither carried nor trusted has no path.', [chain('leafOnly', anchors('root'))]),
  scenario('trust-leaf-only-intermediate', 'A lone signer trusted through its issuer.', [chain('leafOnly', anchors('inter'))]),
  scenario('trust-intermediate-anchor-carried', 'A carried certificate that is itself an anchor is a path of two copies.', [
    chain('rsaCades', anchors('root', 'inter')),
  ]),
  scenario('trust-self-signed-anchor', 'A self-signed signer trusted directly.', [chain('selfSigned', anchors('selfSigned'))]),
  scenario('trust-self-signed-other', 'A self-signed signer with an unrelated anchor.', [chain('selfSigned', anchors('root'))]),
  scenario('trust-other-root', 'An unrelated root is no path.', [chain('rsaCades', anchors('otherRoot'))]),
  scenario('trust-by-issuer-name', 'A signer without an authority key identifier finds its issuer by name.', [chain('noAkiLeaf', anchors('inter'))]),
  scenario('trust-expired-intermediate', 'An expired certificate on the path.', [chain('interExpired', anchors('root'))]),
  scenario('trust-expired-signer', 'An expired lone signer.', [chain('expiredLeaf', anchors('inter'))]),
  scenario('trust-not-yet-valid', "A validation time before every certificate's notBefore.", [
    chain('rsaCades', anchors('root'), genTime(Date.UTC(2005, 0, 1))),
  ]),
  scenario('trust-at-timestamp', 'A validation time inside the validity window.', [
    chain('rsaCades', anchors('root'), genTime(Date.UTC(2030, 5, 1))),
  ]),
  scenario('trust-at-now', 'An explicit `now` is the clock.', [chain('rsaCades', anchors('root'), { kind: 'now' })]),
  scenario('trust-ca-without-basic-constraints', 'An issuer with keyCertSign but no BasicConstraints.', [
    chain('underNoBasicLeaf', anchors('interNoBasic')),
  ]),
  scenario('trust-ca-without-cert-sign', 'A CA whose key usage lacks keyCertSign.', [chain('underNoCertSignLeaf', anchors('interNoCertSign'))]),
  scenario('trust-ca-unknown-critical', 'A CA with a critical extension pkijs cannot parse.', [chain('underUnknownLeaf', anchors('interUnknownCritical'))]),
  scenario('trust-issuer-not-ca', 'An issuer that says it is not a CA.', [chain('underNotCaLeaf', anchors('interNotCa'))]),
  scenario('trust-wrong-issuer-name', 'Found by key identifier, refused by name chaining.', [chain('wrongIssuerNameLeaf', anchors('inter'))]),
  scenario('trust-pss-certificate', 'A certificate signed with RSASSA-PSS (SHA-384) verifies against its issuer.', [chain('pssCertLeaf', anchors('inter'))]),
  scenario('trust-ec-leaf', 'An EC signer issued by an RSA intermediate.', [chain('ecP256Leaf', anchors('inter'))]),
  scenario('trust-ec-self-signed', 'An EC self-signed signer with an unrelated anchor: its own ECDSA signature is checked first.', [
    chain('ecSelfSigned', anchors('root')),
  ]),
  scenario('trust-ec-self-signed-anchor', 'An EC self-signed signer trusted directly.', [chain('ecSelfSigned', anchors('selfSignedP256'))]),
];

// The chain engine's policy and name-constraint half: each CMS carries
// [signer, CA, sub-CA] under the root, so the path is sub-CA, CA, root.
for (const name of Object.keys(fixtures.cms).filter((n) => n.startsWith('policy-'))) {
  trustCases.push(
    scenario(`trust-${name}`, `Certificate policies and name constraints: the ${name.slice(7)} chain.`, [chain(name, anchors('root'))]),
  );
}

// The chain engine's rules one at a time, and its policy half on paths the loop above does not build.
trustCases.push(
  scenario('trust-at-not-before', 'A validation time exactly at notBefore is inside the window.', [
    chain('rsaCades', anchors('root'), genTime(Date.UTC(2020, 0, 1))),
  ]),
  scenario('trust-at-not-after', 'A validation time exactly at notAfter is inside the window.', [
    chain('rsaCades', anchors('root'), genTime(Date.UTC(2045, 0, 1))),
  ]),
  scenario('trust-tampered-signer', 'A signer whose certificate signature does not verify has no issuer.', [chain('leafTampered', anchors('inter'))]),
  scenario('trust-self-signed-twin', 'A valid self-signature makes the certificate its only issuer, even when an anchor holds its key and name.', [
    chain('selfSigned', anchors('selfSignedTwin')),
  ]),
  scenario('trust-self-named-other-key', 'A certificate named as its own issuer whose self-signature fails is issued by the anchor of that name.', [
    chain('twinCaLeaf', anchors('twinCaAnchor')),
  ]),
  scenario('trust-empty-aki', 'An empty authority key identifier finds the issuer by name.', [chain('leafEmptyAki', anchors('inter'))]),
  scenario('trust-aki-issuer-only', 'An authority key identifier naming only the issuer matches nothing.', [chain('leafAkiIssuerOnly', anchors('inter'))]),
  scenario('trust-ca-critical-not-ber', 'A CA whose critical extension does not parse is not a CA.', [
    chain('underUnknownLeaf', anchors('interCriticalNotBer')),
  ]),
  scenario('trust-sha1-certificate', 'A certificate signed with sha1WithRSAEncryption verifies against its issuer.', [chain('leafSha1Signed', anchors('inter'))]),
  scenario('trust-sha384-certificate', 'A certificate signed with sha384WithRSAEncryption.', [chain('leafSha384Signed', anchors('inter'))]),
  scenario('trust-sha512-certificate', 'A certificate signed with sha512WithRSAEncryption.', [chain('leafSha512Signed', anchors('inter'))]),
  scenario('trust-ec-issuer',
    "An RSA signer issued by an EC CA: the issuer key's curve comes from its parameters.", [
    chain('leafUnderEc', anchors('selfSignedP256')),
  ]),
  scenario('trust-ca-path-length-only', 'BasicConstraints with a path length and no cA is no CA.', [
    chain('underNoBasicLeaf', anchors('interPathLengthOnly')),
  ]),
  scenario('trust-two-anchor-issuers', 'Two anchors issue the signer: the first shortest path wins.', [chain('leafOnly', anchors('inter', 'interTwin'))]),
  scenario('trust-shorter-path', 'An anchor issuer and a carried issuer: the shorter path wins.', [
    chain('leafLastCarried', anchors('root', 'interSelfSigned')),
  ]),
  scenario('trust-policy-two-certificates', 'A two-certificate path still runs the policy checks.', [chain('leafInhibitsOwnMapping', anchors('inter'))]),
  scenario('trust-anchor-constraints-ignored',
    "The anchor's own name constraints are not applied.", [chain('leafOnly', anchors('interExcludingLeaf'))]),
);

// ── sign ─────────────────────────────────────────────────────────────────────

/**
 * A document with no validation data: `validateSignatures` reads the security store before it
 * judges (`readValidationPool`, 67ed292a6). Sticky, so a case that never validates leaves it
 * unasked.
 */
const NO_VALIDATION_INFO: ResponseSpec = {
  path: 'signatures.getValidationInfo',
  value: { certificates: [], ocspResponses: [], crls: [] },
};
const signDoc = (responses: ResponseSpec[], services = ['signatures']): Record<string, FakeDocumentSpec> => ({
  d: { pages: [], services, responses: services.length
      ? [...responses, NO_VALIDATION_INFO]
      : [
          ...responses,
          ...['prepare', 'list'].map((method) => ({
            path: `signatures.${method}`,
            error: { name: 'EngineError', code: 'Unsupported', message: 'unsupported' },
          })),
        ],
  },
});
const completed = { status: 'completed', signature: { index: 0, fieldName: 'Sig1' }, version: { sha256: 'new', sizeBytes: 10 } };
const field = { kind: 'fqn', name: 'Sig1' };
const rsaAnswer = async (profile: 'pkcs7' | 'cades-b', signingTime?: number, digest = digestBytes(32, 3), hash: RawSigner['hash'] = 'sha256') => ({
  resolves: $b(await signatureFor({ key: 'leaf', chain: leafChain, algorithm: 'RSA-PKCS1-v1_5', hash, profile, digest, signingTime })),
});
const signCall = (input: Record<string, unknown>) => {
  const { signer, ...rest } = input;
  return call('sign', 'd', { field, ...rest, key: signer });
};
const cmsSigner = (answer: unknown) => ({ kind: 'cms', sign: { $fn: 'cms.sign', ...(answer as object) } });

const signCases: ScenarioCase[] = [
  scenario('sign-no-service', 'An engine without signatures refuses at once.', [
    signCall({ signer: cmsSigner({ resolves: cmsArg('rsaCades') }) }),
  ], signDoc([], [])),
  scenario('sign-raw-cades',
    "prepare with CAdES and the signer's hash, build, gate, complete.", [
    signCall({ signer: rawSignerArg(leafChain, 'RSA-PKCS1-v1_5', 'sha256', await rsaAnswer('cades-b')) }),
  ], signDoc([
    { path: 'signatures.prepare', value: prepared() },
    { path: 'signatures.complete', value: completed },
  ])),
  scenario('sign-raw-pkcs7', 'The PKCS#7 sub-filter builds with signing-time from the clock.', [
    signCall({
      subFilter: 'adbe.pkcs7.detached',
      signer: rawSignerArg(leafChain, 'RSA-PKCS1-v1_5', 'sha256', await rsaAnswer('pkcs7', JAN_2026)),
    }),
  ], signDoc([
    { path: 'signatures.prepare', value: prepared({ subFilter: 'adbe.pkcs7.detached' }) },
    { path: 'signatures.complete', value: completed },
  ])),
  scenario('sign-raw-sha384',
    "A raw signer's hash is the prepared digest when none is asked for.", [
    signCall({
      signer: rawSignerArg(leafChain, 'RSA-PKCS1-v1_5', 'sha384', await rsaAnswer('cades-b', undefined, digestBytes(48, 3), 'sha384')),
    }),
  ], signDoc([
    { path: 'signatures.prepare', value: prepared({ algorithm: 'sha384', digest: $b(b64(digestBytes(48, 3))) }) },
    { path: 'signatures.complete', value: completed },
  ])),
  scenario('sign-cms-signer', 'A CMS signer gets the digest, algorithm and sub-filter; sha256 unless asked.', [
    signCall({ contentsSizeBytes: 16384, signer: cmsSigner({ resolves: cmsArg('rsaCades') }) }),
  ], signDoc([
    { path: 'signatures.prepare', value: prepared() },
    { path: 'signatures.complete', value: completed },
  ])),
  scenario('sign-explicit-digest', 'An explicit digest is passed to prepare as is.', [
    signCall({ digest: 'sha512', signer: cmsSigner({ resolves: cmsArg('rsaCades') }) }),
  ], signDoc([
    { path: 'signatures.prepare', value: prepared() },
    { path: 'signatures.complete', value: completed },
  ])),
  scenario('sign-gate-refuses', 'A CMS the gate refuses aborts the signing and rejects with SigningError.', [
    signCall({ signer: cmsSigner({ resolves: cmsArg('rsaSha384') }) }),
  ], signDoc([
    { path: 'signatures.prepare', value: prepared() },
    { path: 'signatures.cancel', value: { status: 'aborted' } },
  ])),
  scenario('sign-signer-rejects', 'A signer failure aborts and rethrows.', [
    signCall({ signer: cmsSigner({ rejects: { name: 'NetworkError', message: 'signing service down' } }) }),
  ], signDoc([
    { path: 'signatures.prepare', value: prepared() },
    { path: 'signatures.cancel', value: { status: 'aborted' } },
  ])),
  scenario('sign-complete-rejects', 'A failed complete aborts and rethrows the engine error.', [
    signCall({ signer: cmsSigner({ resolves: cmsArg('rsaCades') }) }),
  ], signDoc([
    { path: 'signatures.prepare', value: prepared() },
    { path: 'signatures.complete', error: { name: 'EngineError', message: 'stale base', code: 'StaleBase' } },
    { path: 'signatures.cancel', value: { status: 'already-completed' } },
  ])),
  scenario('sign-abort-rejects', 'An abort that fails is swallowed; the original error wins.', [
    signCall({ signer: cmsSigner({ resolves: cmsArg('twoSigners') }) }),
  ], signDoc([
    { path: 'signatures.prepare', value: prepared() },
    { path: 'signatures.cancel', error: { message: 'gone' } },
  ])),
  scenario('sign-prepare-rejects', 'A failed prepare rejects without an abort.', [
    signCall({ signer: cmsSigner({ resolves: cmsArg('rsaCades') }) }),
  ], signDoc([{ path: 'signatures.prepare', error: { name: 'EngineError', message: 'field is signed', code: 'Conflict' } }])),
  scenario('sign-raw-sha1-prepared', 'A raw signer over a document prepared with SHA-1 is refused by the build.', [
    signCall({ signer: rawSignerArg(leafChain, 'RSA-PKCS1-v1_5', 'sha256', { resolves: $b('') }) }),
  ], signDoc([
    { path: 'signatures.prepare', value: prepared({ algorithm: 'sha1' }) },
    { path: 'signatures.cancel', value: { status: 'aborted' } },
  ])),
  scenario('sign-build-throws', 'A raw signer whose build throws aborts.', [
    signCall({ signer: rawSignerArg([], 'RSA-PKCS1-v1_5', 'sha256', { resolves: $b('') }) }),
  ], signDoc([
    { path: 'signatures.prepare', value: prepared() },
    { path: 'signatures.cancel', value: { status: 'aborted' } },
  ])),
];

// ── validateSignatures ───────────────────────────────────────────────────────

const sigDto = (over: Record<string, unknown> = {}) => ({
  index: 0,
  field,
  fieldName: 'Sig1',
  widget: null,
  signed: true,
  kind: 'signature',
  filter: 'Adobe.PPKLite',
  subFilter: 'ETSI.CAdES.detached',
  byteRange: [0, 100, 8292, 50],
  contentsSizeBytes: 8192,
  coverage: 'whole-revision',
  revisionIndex: 0,
  signer: { name: 'Conformance Signer' },
  docMdp: null,
  catalogCertification: false,
  fieldMdp: null,
  lock: null,
  seedValue: null,
  ...over,
});
const snapshot = (signatures: unknown[], over: Record<string, unknown> = {}) => ({
  chainValid: true,
  revisions: [{ index: 0 }],
  signatures,
  protection: {},
  ...over,
});
const analysis = (current: Record<string, unknown>, over: Record<string, unknown> = {}) => ({
  current,
  basis: { source: 'persisted' },
  later: { revisionCount: 0, undoneObjectNumbers: [] },
  ...over,
});
const goodDigest = $b(b64(digestBytes(32, 3)));
const validate = (options?: unknown) =>
  options === undefined ? call('validateSignatures', 'd') : call('validateSignatures', 'd', options);
const contentsOf = (cms: string): ResponseSpec => ({ path: 'signatures.getContents', value: cmsArg(cms) });
const digestAnswer: ResponseSpec = { path: 'signatures.getDigest', value: goodDigest };

const validateCases: ScenarioCase[] = [
  scenario('validate-no-service', 'An engine without signatures refuses.', [validate()], signDoc([], [])),
  scenario('validate-empty', 'No signatures, no verdicts.', [validate()], signDoc([{ path: 'signatures.list', value: snapshot([]) }])),
  scenario('validate-valid-untrusted', 'Intact, verifying, no trust port, sealing the last revision: `valid-untrusted`, unchanged.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto(), sigDto({ index: 1, signed: false, fieldName: 'Empty' })]) },
    contentsOf('rsaCades'),
    digestAnswer,
  ])),
  scenario('validate-trusted', 'With an anchor that validates: `valid`.', [validate({ trust: anchors('root') })], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()]) },
    contentsOf('rsaCades'),
    digestAnswer,
  ])),
  scenario('validate-untrusted-reason', 'A chain that fails carries its reason.', [validate({ trust: anchors('otherRoot') })], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()]) },
    contentsOf('rsaCades'),
    digestAnswer,
  ])),
  scenario('validate-digest-mismatch', 'Bytes that do not hash to the message digest: integrity invalid.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()]) },
    contentsOf('rsaCades'),
    { path: 'signatures.getDigest', value: $b(b64(digestBytes(32, 9))) },
  ])),
  scenario('validate-partial-coverage', 'Partial coverage is invalid even when the digest matches.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto({ coverage: 'partial' })]) },
    contentsOf('rsaCades'),
    digestAnswer,
  ])),
  scenario('validate-chain-invalid', 'A broken revision chain makes integrity indeterminate without reading the CMS.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()], { chainValid: false }) },
  ])),
  scenario('validate-signed-by-truthiness', 'Only a truthy `signed` is judged, whatever its type: null, 0 and the empty string are skipped; 1, a string, an object and an array are not.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([
      sigDto({ index: 0, signed: null }),
      sigDto({ index: 1, signed: 0 }),
      sigDto({ index: 2, signed: '' }),
      sigDto({ index: 3, signed: 1 }),
      sigDto({ index: 4, signed: 'x' }),
      sigDto({ index: 5, signed: {} }),
      sigDto({ index: 6, signed: [] }),
    ], { chainValid: false }) },
  ])),
  scenario('validate-coverage-malformed', 'Malformed coverage on a valid chain is invalid.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto({ coverage: 'malformed' })]) },
  ])),
  scenario('validate-unsupported-cms', 'A CMS this engine cannot read is indeterminate, not tampering.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()]) },
    contentsOf('twoSigners'),
  ])),
  scenario('validate-malformed-cms', 'A malformed CMS is invalid.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()]) },
    contentsOf('noCertificates'),
  ])),
  scenario('validate-contents-rejects', 'An engine failure reading the CMS is indeterminate.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()]) },
    { path: 'signatures.getContents', error: { name: 'EngineError', message: 'io', code: 'Io' } },
  ])),
  scenario('validate-crypto-invalid', 'A signature that does not verify is invalid.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()]) },
    contentsOf('rsaCadesTampered'),
    digestAnswer,
  ])),
  scenario('validate-crypto-unsupported', 'An unsupported algorithm is indeterminate.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()]) },
    contentsOf('unknownCurve'),
    digestAnswer,
  ])),
  scenario('validate-no-revision', 'A signature that seals no whole revision has indeterminate modifications.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto({ revisionIndex: null })]) },
    contentsOf('rsaCades'),
    digestAnswer,
  ])),
  scenario('validate-forbidden', 'A forbidden change after the signature: invalid, with the object and rule.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()], { revisions: [{ index: 0 }, { index: 1 }] }) },
    contentsOf('rsaCades'),
    digestAnswer,
    {
      path: 'signatures.analyze',
      value: analysis(
        { verdict: 'forbidden', primary: { verdict: 'forbidden', objectNumber: 12, rule: 'page-content', detail: 'content stream replaced' } },
        { later: { revisionCount: 1, undoneObjectNumbers: [] } },
      ),
    },
  ])),
  scenario('validate-forbidden-no-detail', 'A forbidden primary without detail.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()], { revisions: [{ index: 0 }, { index: 1 }] }) },
    contentsOf('rsaCades'),
    digestAnswer,
    { path: 'signatures.analyze', value: analysis({ verdict: 'forbidden', primary: { verdict: 'forbidden', objectNumber: 3, rule: 'catalog' } }) },
  ])),
  scenario('validate-incomplete', 'An incomplete primary: indeterminate, with its detail.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()], { revisions: [{ index: 0 }, { index: 1 }] }) },
    contentsOf('rsaCades'),
    digestAnswer,
    { path: 'signatures.analyze', value: analysis({ verdict: 'indeterminate', primary: { verdict: 'incomplete', objectNumber: 7, detail: 'stream too large' } }) },
  ])),
  scenario('validate-incomplete-no-detail', 'An incomplete primary without detail says so.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()], { revisions: [{ index: 0 }, { index: 1 }] }) },
    contentsOf('rsaCades'),
    digestAnswer,
    { path: 'signatures.analyze', value: analysis({ verdict: 'indeterminate', primary: { verdict: 'incomplete', objectNumber: 7 } }) },
  ])),
  scenario('validate-permitted-undone', 'Permitted later changes, some undone; the working copy was asked for.', [
    validate({ until: 'working-copy', validationTime: genTime(Date.UTC(2030, 5, 1)) }),
  ], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()]) },
    contentsOf('rsaCades'),
    digestAnswer,
    {
      path: 'signatures.analyze',
      value: analysis(
        { verdict: 'permitted', primary: { verdict: 'permitted', objectNumber: 4 } },
        { basis: { source: 'working-copy' }, later: { revisionCount: 2, undoneObjectNumbers: [4] } },
      ),
    },
  ])),
  scenario('validate-analyze-rejects', 'A failed analysis is indeterminate with its message.', [validate()], signDoc([
    { path: 'signatures.list', value: snapshot([sigDto()], { revisions: [{ index: 0 }, { index: 1 }] }) },
    contentsOf('rsaCades'),
    digestAnswer,
    { path: 'signatures.analyze', error: { message: 'revision 1 unreadable' } },
  ])),
];

// ── signers ──────────────────────────────────────────────────────────────────

const rsaKey = (hash = 'SHA-256', name = 'RSASSA-PKCS1-v1_5'): KeySpec => ({ fixture: 'leaf', algorithm: { name, hash } });
const toSign = $b(b64(new TextEncoder().encode('signed attributes')));
const signerCases: ScenarioCase[] = [
  scenario('remote-signer', 'remoteSigner forwards the request to the port and answers with its CMS.', [
    call('remoteSign', { sign: { $fn: 'remote.sign', resolves: cmsArg('rsaCades') } }, {
      digest: goodDigest, algorithm: 'sha256', subFilter: 'ETSI.CAdES.detached',
    }),
  ]),
  scenario('memory-key-store', 'The memory store loads null, then what was saved, until it is removed.', [
    call('memoryKeyStore', [
      { op: 'load', subject: 'Ada' },
      { op: 'save', subject: 'Ada', record: { certificate: certArg('selfSigned') } },
      { op: 'load', subject: 'Ada' },
      { op: 'load', subject: 'Grace' },
      { op: 'remove', subject: 'Ada' },
      { op: 'load', subject: 'Ada' },
    ]),
  ]),
  ...(['SHA-256', 'SHA-384', 'SHA-512'] as const).map((hash) =>
    scenario(`webcrypto-rsa-${hash}`, `An RSASSA-PKCS1-v1_5 key's own hash (${hash}) decides the signer's hash.`, [
      call('webCryptoSigner', rsaKey(hash), [certArg('leaf')], 'sha512', toSign),
    ]),
  ),
  scenario('webcrypto-pss', 'An RSA-PSS key signs with the salt length of its hash.', [
    call('webCryptoSigner', rsaKey('SHA-384', 'RSA-PSS'), [certArg('leaf')], undefined, toSign),
  ]),
  scenario('webcrypto-ecdsa-default-hash', 'An ECDSA key carries no hash: sha256 unless given.', [
    call('webCryptoSigner', { fixture: 'p256', algorithm: { name: 'ECDSA', namedCurve: 'P-256' } }, [certArg('leafP256')], undefined, toSign),
  ]),
  scenario('webcrypto-ecdsa-hash', 'An ECDSA signer with an explicit hash.', [
    call('webCryptoSigner', { fixture: 'p384', algorithm: { name: 'ECDSA', namedCurve: 'P-384' } }, [certArg('leafP384')], 'sha384', toSign),
  ]),
  scenario('webcrypto-unsupported-algorithm', 'A key WebCrypto signs with but this package does not.', [
    call('webCryptoSigner', { fake: true, algorithm: { name: 'Ed25519' } }, [certArg('leaf')]),
  ]),
  scenario('webcrypto-unsupported-hash', 'An RSA key hashing with SHA-1.', [
    call('webCryptoSigner', { fake: true, algorithm: { name: 'RSASSA-PKCS1-v1_5', hash: { name: 'SHA-1' } } as never }, [certArg('leaf')]),
  ]),
  ...(
    [
      ['RSA-PKCS1-v1_5', 'sha256', true],
      ['RSA-PSS', 'sha384', false],
      ['ECDSA', 'sha512', false],
    ] as const
  ).map(([algorithm, hash, extractable]) =>
    scenario(`generate-${algorithm}`, `generateSigningKeyPair's WebCrypto parameters for ${algorithm}.`, [
      call('generateSigningKeyPair', algorithm, hash, extractable),
    ]),
  ),
  scenario('self-signed-rsa', 'A self-signed certificate over an RSA key: serial from getRandomValues, a day back to a year ahead.', [
    call('selfSignedCertificate', { key: rsaKey(), commonName: 'Ada Lovelace', hash: 'sha256' }),
  ]),
  scenario('self-signed-rsa-sha512-days',
    "The key's own hash wins over the one passed; a custom validity.", [
    call('selfSignedCertificate', { key: rsaKey('SHA-512'), commonName: 'Grace Hopper', hash: 'sha256', validityDays: 30 }),
  ]),
  scenario('self-signed-rsa-sha1', 'An RSA key hashing with SHA-1 signs its certificate with sha1WithRSAEncryption.', [
    call('selfSignedCertificate', { key: rsaKey('SHA-1'), commonName: 'Sha One', hash: 'sha256' }),
  ]),
  scenario('self-signed-validity-past-2100', 'A validity that ends after 2099 writes notAfter without its year, as asn1js does.', [
    call('selfSignedCertificate', { key: rsaKey(), commonName: 'Far Future', hash: 'sha256', validityDays: 30000 }),
  ]),
  scenario('self-signed-rsa-3072', 'A 3072-bit key: its 384-byte modulus survives the power-of-two rounding of the JWK import.', [
    call('selfSignedCertificate', { key: { ...rsaKey(), fixture: 'rsa3072' }, commonName: 'Long Key', hash: 'sha256' }),
  ]),
  ...(['P-384', 'P-521'] as const).map((namedCurve) =>
    scenario(`self-signed-ecdsa-${namedCurve}`, `A self-signed certificate over a ${namedCurve} key.`, [
      call('selfSignedCertificate', { key: { algorithm: { name: 'ECDSA', namedCurve } }, commonName: 'EC Person', hash: 'sha384' }),
    ]),
  ),
  scenario('self-signed-pss', 'A self-signed certificate over an RSA-PSS key.', [
    call('selfSignedCertificate', { key: rsaKey('SHA-384', 'RSA-PSS'), commonName: 'PSS Person', hash: 'sha384' }),
  ]),
  scenario('self-signed-ecdsa', 'A self-signed certificate over an ECDSA key, signed with the hash passed.', [
    call('selfSignedCertificate', { key: { algorithm: { name: 'ECDSA', namedCurve: 'P-256' } }, commonName: 'EC Person', hash: 'sha384' }),
  ]),
  scenario('test-signer-default', 'createTestSigner: an extractable RSA key pair, a self-signed certificate, a signer over it.', [
    call('createTestSigner', undefined, toSign),
  ]),
  scenario('test-signer-ecdsa', 'createTestSigner with ECDSA and a name.', [
    call('createTestSigner', { algorithm: 'ECDSA', hash: 'sha384', commonName: 'EC Tester' }),
  ]),
  scenario('test-signer-pss', 'createTestSigner with RSA-PSS.', [call('createTestSigner', { algorithm: 'RSA-PSS', hash: 'sha512' })]),
  scenario('personal-signer-memory', 'personalSigner creates the identity on first use and reuses it on the second.', [
    call('personalSigner', { subject: 'Ada Lovelace' }, 2),
  ]),
  scenario('personal-signer-store-port', 'personalSigner asks the store, generates a non-extractable key, and saves the record.', [
    call('personalSigner', {
      subject: 'Grace Hopper',
      algorithm: 'ECDSA',
      hash: 'sha384',
      store: {
        load: { $fn: 'store.load', resolves: null },
        save: { $fn: 'store.save', resolves: { $undefined: true } },
        remove: { $fn: 'store.remove', resolves: { $undefined: true } },
      },
    }),
  ]),
  scenario('personal-signer-store-fails', 'A store that cannot save rejects.', [
    call('personalSigner', {
      subject: 'Grace Hopper',
      store: {
        load: { $fn: 'store.load', resolves: null },
        save: { $fn: 'store.save', rejects: { name: 'QuotaExceededError', message: 'full' } },
        remove: { $fn: 'store.remove', resolves: { $undefined: true } },
      },
    }),
  ]),
];

// ── timestamps over HTTP, the revocation route, token verdicts ──────────────

/** The fixture `leaf` key as a timestamp authority: what the scripted endpoints sign tokens with. */
const leafAuthority: RawSigner = {
  kind: 'raw',
  certificateChain: [bytesOf(fixtures.certs.leaf!)],
  algorithm: 'RSA-PKCS1-v1_5',
  hash: 'sha256',
  sign: async (data) => rawSignature('leaf', 'RSA-PKCS1-v1_5', 'sha256', data),
};
/** The nonce `httpTimestampAuthority` sends under the scripted WebCrypto: eight 0x5a octets. */
const SCRIPTED_NONCE = new Uint8Array(8).fill(0x5a);
const TSA_DIGEST = digestBytes(32, 11);
const TSA_URL = 'https://tsa.example/stamp';
const tsaRequest = (over: Record<string, unknown> = {}) => ({
  digest: $b(b64(TSA_DIGEST)),
  algorithm: 'sha256',
  subFilter: 'ETSI.RFC3161',
  ...over,
});
const der = (tag: number, content: Uint8Array) => {
  const length = content.byteLength < 0x80 ? [content.byteLength] : [0x82, content.byteLength >> 8, content.byteLength & 0xff];
  return new Uint8Array([tag, ...length, ...content]);
};
/** A TimeStampResp that grants, with `token`. */
const grantedReply = (token: Uint8Array) =>
  $b(b64(der(0x30, new Uint8Array([...der(0x30, new Uint8Array([0x02, 0x01, 0x00])), ...token]))));
/** A TimeStampResp that rejects, naming the failure at `bit`, with a status text. */
const rejectedReply = (bit: number) => {
  const bits = new Uint8Array(Math.floor(bit / 8) + 1);
  bits[bit >> 3]! |= 0x80 >> (bit & 7);
  const text = der(0x30, der(0x0c, new TextEncoder().encode('try another hash')));
  const status = der(0x30, new Uint8Array([0x02, 0x01, 0x02, ...text, ...der(0x03, new Uint8Array([0, ...bits]))]));
  return $b(b64(der(0x30, status)));
};
const token = (over: Partial<Parameters<typeof buildTestToken>[0]> = {}) =>
  buildTestToken({
    digest: TSA_DIGEST,
    authority: leafAuthority,
    genTime: new Date(Date.UTC(2026, 0, 15, 12)),
    nonce: SCRIPTED_NONCE,
    ...over,
  });
const reply = (status: number, body?: unknown) => ({ status, ...(body === undefined ? {} : { body }) });
const jsonBody = (value: unknown) => $b(Buffer.from(JSON.stringify(value)).toString('base64'));
const ROUTE = 'https://app.example/revocation';

const longTermCases: ScenarioCase[] = [
  scenario('tsa-granted', 'httpTimestampAuthority: the RFC 3161 query it sends (D-1), and a granted reply with the nonce echoed.', [
    call('timestampHttp', { url: TSA_URL, retries: 0 }, tsaRequest(), [reply(200, grantedReply(await token()))]),
  ]),
  scenario('tsa-policy-headers', 'A policy and headers are sent; a token under another policy is refused.', [
    call(
      'timestampHttp',
      { url: TSA_URL, policy: '1.2.3.4', headers: { authorization: 'Bearer t', 'x-tenant': 'a' }, retries: 0 },
      tsaRequest(),
      [reply(200, grantedReply(await token()))],
    ),
  ]),
  scenario('tsa-policy-kept', 'A token under the policy asked for is the answer.', [
    call('timestampHttp', { url: TSA_URL, policy: '1.2.3.4', retries: 0 }, tsaRequest(), [
      reply(200, grantedReply(await token({ policy: '1.2.3.4' }))),
    ]),
  ]),
  scenario('tsa-sha384', 'A sha384 digest is asked for under sha384; a sha256 token for it is refused.', [
    call('timestampHttp', { url: TSA_URL, hash: 'sha384', retries: 0 }, tsaRequest({ digest: $b(b64(digestBytes(48, 2))), algorithm: 'sha384' }), [
      reply(200, grantedReply(await token())),
    ]),
  ]),
  scenario('tsa-other-digest', 'A token over another digest is refused.', [
    call('timestampHttp', { url: TSA_URL, retries: 0 }, tsaRequest(), [reply(200, grantedReply(await token({ digest: digestBytes(32, 99) })))]),
  ]),
  scenario('tsa-no-nonce', 'A token without the nonce sent does not answer this request.', [
    call('timestampHttp', { url: TSA_URL, retries: 0 }, tsaRequest(), [reply(200, grantedReply(await token({ nonce: undefined })))]),
  ]),
  scenario('tsa-rejected', 'A rejection names its failure and status text.', [
    call('timestampHttp', { url: TSA_URL, retries: 0 }, tsaRequest(), [reply(200, rejectedReply(0)), reply(200, rejectedReply(30))]),
  ]),
  scenario('tsa-rejected-unnamed-failure', 'A failure bit with no name.', [
    call('timestampHttp', { url: TSA_URL, retries: 0 }, tsaRequest(), [reply(200, rejectedReply(30))]),
  ]),
  scenario('tsa-granted-without-token', 'A grant without a token.', [
    call('timestampHttp', { url: TSA_URL, retries: 0 }, tsaRequest(), [reply(200, $b('MAUwAwIBAA=='))]),
  ]),
  scenario('tsa-unreadable', 'A reply that is no timestamp response, and a token that is no token.', [
    call('timestampHttp', { url: TSA_URL, retries: 0 }, tsaRequest(), [reply(200, $b('AQID'))]),
    call('timestampHttp', { url: TSA_URL, retries: 0 }, tsaRequest(), [reply(200, grantedReply(der(0x30, new Uint8Array([0x02, 0x01, 0x05]))))]),
  ]),
  scenario('tsa-http-statuses', 'A 4xx is a refusal; a 5xx, a 429 or a failure is no answer.', [
    call('timestampHttp', { url: TSA_URL, retries: 0 }, tsaRequest(), [reply(403)]),
    call('timestampHttp', { url: TSA_URL, retries: 0 }, tsaRequest(), [reply(503)]),
    call('timestampHttp', { url: TSA_URL, retries: 0 }, tsaRequest(), [reply(429)]),
    call('timestampHttp', { url: TSA_URL, retries: 0 }, tsaRequest(), [{ fail: 'getaddrinfo ENOTFOUND tsa.example' }]),
  ]),
  scenario('tsa-not-a-signer', 'A timestamp authority asked for a document signature.', [
    call('timestampHttp', { url: TSA_URL }, tsaRequest({ subFilter: 'ETSI.CAdES.detached' }), []),
  ]),
  scenario('route-ocsp', 'httpRevocationSource: the route request for an OCSP question (D-2), and the answer passed on.', [
    call('revocationRoute', { url: ROUTE, retries: 0 }, { kind: 'ocsp', url: 'http://ocsp.example', body: $b('MAMCAQE=') }, [reply(200, $b('BAUG'))]),
  ]),
  scenario('route-crl-issuer', 'The route request for a CRL and for an issuer, with headers.', [
    call('revocationRoute', { url: ROUTE, headers: { 'x-session': 's1' }, retries: 0 }, { kind: 'crl', url: 'http://crl.example/a.crl' }, [reply(200, $b('AQ=='))]),
    call('revocationRoute', { url: ROUTE, retries: 0 }, { kind: 'issuer', url: 'http://ca.example/ca.cer' }, [reply(200, $b('Ag=='))]),
  ]),
  scenario('route-refusals', 'A route that refuses, with and without its JSON; a 5xx, a 429 and a failure are no answer.', [
    call('revocationRoute', { url: ROUTE, retries: 0 }, { kind: 'crl', url: 'http://crl.example/a.crl' }, [
      reply(400, jsonBody({ reason: 'address', message: 'that address is not allowed' })),
    ]),
    call('revocationRoute', { url: ROUTE, retries: 0 }, { kind: 'crl', url: 'http://crl.example/a.crl' }, [reply(404, $b('bm8='))]),
    call('revocationRoute', { url: ROUTE, retries: 0 }, { kind: 'crl', url: 'http://crl.example/a.crl' }, [
      reply(502, jsonBody({ message: 'the authority did not answer' })),
    ]),
    call('revocationRoute', { url: ROUTE, retries: 0 }, { kind: 'crl', url: 'http://crl.example/a.crl' }, [reply(429)]),
    call('revocationRoute', { url: ROUTE, retries: 0 }, { kind: 'crl', url: 'http://crl.example/a.crl' }, [{ fail: 'connection refused' }]),
  ]),
  scenario('token-verdicts', 'verifyTimestampToken: without trust, trusting the authority, over another digest, and unreadable.', [
    call('verifyTimestampToken', { token: $b(b64(await token())), digest: $b(b64(TSA_DIGEST)), algorithm: 'sha256' }),
    call('verifyTimestampToken', {
      token: $b(b64(await token())),
      digest: $b(b64(TSA_DIGEST)),
      algorithm: 'sha256',
      trust: anchors('leaf'),
    }),
    call('verifyTimestampToken', { token: $b(b64(await token())), digest: $b(b64(digestBytes(32, 1))), algorithm: 'sha256', trust: anchors('root') }),
    call('verifyTimestampToken', { token: $b(b64(await token())), digest: $b(b64(TSA_DIGEST)), algorithm: 'sha384' }),
    call('verifyTimestampToken', { token: $b('AQID'), digest: $b(b64(TSA_DIGEST)), algorithm: 'sha256' }),
    call('verifyTimestampToken', {
      token: $b(b64(await token({ binding: null }))),
      digest: $b(b64(TSA_DIGEST)),
      algorithm: 'sha256',
    }),
  ]),
  scenario('ecdsa-raw-from-der', 'ecdsaRawFromDer: DER to r || s, and the bytes it refuses.', [
    call('ecdsaRawFromDer', $b(b64(der(0x30, new Uint8Array([...der(0x02, new Uint8Array([0, 0x80, 1])), ...der(0x02, new Uint8Array([5]))])))), 'P-256'),
    call('ecdsaRawFromDer', $b(b64(der(0x31, new Uint8Array([...der(0x02, new Uint8Array([1])), ...der(0x02, new Uint8Array([2]))])))), 'P-256'),
    call('ecdsaRawFromDer', $b(b64(der(0x30, der(0x02, new Uint8Array([1]))))), 'P-384'),
    call('ecdsaRawFromDer', $b(b64(der(0x30, new Uint8Array([...der(0x02, new Uint8Array(33).fill(1)), ...der(0x02, new Uint8Array([2]))])))), 'P-256'),
    call('ecdsaRawFromDer', $b('MAU='), 'P-521'),
  ]),
];

// ── revocation answers, long-term validation, addValidationInfo ─────────────

/**
 * A test certificate authority and what it serves, made once under the scripted WebCrypto with
 * its clock at 2026-01-15 (upstream `8a143eecf`), so every byte is the same each run: a signer
 * it vouches for and one it withdrew a day before, their OCSP answers and its CRL, and a CAdES
 * signature by each.
 */
const authority = await withWebCrypto(() => {}, async () => {
  const at = new Date(JAN_2026);
  const ca = await createTestCertificateAuthority({ now: () => at });
  const good = await ca.issueSigner({ commonName: 'Long-Term Signer' });
  const withdrawn = await ca.issueSigner({ commonName: 'Withdrawn Signer' });
  ca.revoke(withdrawn, { at: new Date(JAN_2026 - 24 * 3600 * 1000), reason: 'key-compromise' });
  const root = certificateFromDer(ca.certificate);
  const ocspFor = async (certificate: Uint8Array) =>
    ca.revocation.fetch({
      kind: 'ocsp',
      url: 'http://test-authority.example/ocsp',
      body: await buildOcspRequest(certificateFromDer(certificate), root),
    });
  const digest = digestBytes(32, 21);
  const cmsBy = (signer: typeof good) => buildDetachedCms({ digest, hash: 'sha256', profile: 'cades-b', signer });
  return {
    root: ca.certificate,
    good: good.certificate,
    ocspGood: await ocspFor(good.certificate),
    ocspWithdrawn: await ocspFor(withdrawn.certificate),
    ocspUnknown: await ocspFor(ca.certificate),
    crl: await ca.revocation.fetch({ kind: 'crl', url: 'http://test-authority.example/crl' }),
    issuer: await ca.revocation.fetch({ kind: 'issuer', url: 'http://test-authority.example/issuer' }),
    digest,
    cmsGood: await cmsBy(good),
    cmsWithdrawn: await cmsBy(withdrawn),
  };
});
const $bytesOf = (b64text: string) => bytesOf(b64text);
const ltvTrust = { anchors: { $fn: 'trust.anchors', resolves: [$b(b64(authority.root))] } };
const stored = (over: { certificates?: Uint8Array[]; ocspResponses?: Uint8Array[]; crls?: Uint8Array[] } = {}) => ({
  certificates: (over.certificates ?? []).map((d) => $b(b64(d))),
  ocspResponses: (over.ocspResponses ?? []).map((d) => $b(b64(d))),
  crls: (over.crls ?? []).map((d) => $b(b64(d))),
});
const ltvSignature = {
  index: 0,
  field: { kind: 'fqn', name: 'Sig1' },
  fieldName: 'Sig1',
  widget: null,
  signed: true,
  kind: 'signature',
  filter: 'Adobe.PPKLite',
  subFilter: 'ETSI.CAdES.detached',
  byteRange: [0, 100, 8292, 50],
  contentsSizeBytes: 8192,
  coverage: 'whole-revision',
  revisionIndex: 0,
  signer: { name: 'Long-Term Signer' },
  docMdp: null,
  catalogCertification: false,
  fieldMdp: null,
  lock: null,
  seedValue: null,
};
/** A document whose one signature is `cms`, with `info` as its validation data. */
const ltvDoc = (cms: Uint8Array, info: ReturnType<typeof stored>, more: ResponseSpec[] = []): Record<string, FakeDocumentSpec> => ({
  d: {
    pages: [],
    services: ['signatures'],
    responses: [
      { path: 'signatures.list', value: { chainValid: true, revisions: [{ index: 0 }], signatures: [ltvSignature], protection: {} } },
      { path: 'signatures.getValidationInfo', value: info },
      { path: 'signatures.getContents', value: $b(b64(cms)) },
      { path: 'signatures.getDigest', value: $b(b64(authority.digest)) },
      ...more,
    ],
  },
});
const stampedAt = { kind: 'timestamp', stampedAt: iso(JAN_2026), tokenVerified: true };
const source = (answer: Uint8Array | null) => ({
  fetch: answer
    ? { $fn: 'revocation.fetch', resolves: $b(b64(answer)) }
    : { $fn: 'revocation.fetch', rejects: { name: 'EngineError', code: 'Network', message: 'no answer', details: { source: 'x' } } },
});
const stores = (added: Record<string, number>): ResponseSpec => ({
  path: 'signatures.addValidationInfo',
  value: { added: { certificates: 0, ocspResponses: 0, crls: 0, ...added } },
});

const revocationCases: ScenarioCase[] = [
  scenario('ltv-ocsp-good', "A stored OCSP answer from the authority's responder says the signer was good.", [
    call('validateSignatures', 'd', { trust: ltvTrust, validationTime: stampedAt }),
  ], ltvDoc(authority.cmsGood, stored({ ocspResponses: [authority.ocspGood] }))),
  scenario('ltv-crl-shared-serial', 'Under the scripted WebCrypto every serial is 5a5a5a5a5a5a5a5a, so the CRL that withdraws one signer lists the other: a CRL entry matches by serial number alone.', [
    call('validateSignatures', 'd', { trust: ltvTrust, validationTime: stampedAt }),
  ], ltvDoc(authority.cmsGood, stored({ crls: [authority.crl] }))),
  scenario('ltv-withdrawn', 'The CRL says the signer was withdrawn the day before: revoked, and the verdict is invalid.', [
    call('validateSignatures', 'd', { trust: ltvTrust, validationTime: stampedAt }),
  ], ltvDoc(authority.cmsWithdrawn, stored({ ocspResponses: [authority.ocspWithdrawn], crls: [authority.crl] }))),
  scenario('ltv-answers-about-others', 'Bytes that are no answer say nothing; the authority answers by serial, the same for every certificate here, so its OCSP answers speak for the signer.', [
    call('validateSignatures', 'd', { trust: ltvTrust, validationTime: stampedAt }),
  ], ltvDoc(authority.cmsGood, stored({ ocspResponses: [authority.ocspUnknown, authority.ocspWithdrawn, $bytesOf('AQID')], crls: [$bytesOf('AQID')] }))),
  scenario('ltv-now-without-answers', 'Judged now with nothing stored: no answer speaks for now.', [
    call('validateSignatures', 'd', { trust: ltvTrust, validationTime: { kind: 'now' } }),
    call('validateSignatures', 'd', { trust: ltvTrust }),
  ], ltvDoc(authority.cmsGood, stored())),
  scenario('ltv-asks-the-source', 'validateSignatures with a revocation source asks it for what the file lacks, and uses the answer.', [
    call('validateSignatures', 'd', { trust: ltvTrust, validationTime: stampedAt, revocation: source(authority.ocspGood) }),
  ], ltvDoc(authority.cmsGood, stored())),
  scenario('collect-needs-trust', 'addValidationInfo without trust is refused before anything is asked.', [
    call('addValidationInfo', 'd', {}),
  ], ltvDoc(authority.cmsGood, stored())),
  scenario('collect-no-timestamp', 'All or nothing: a signature with no trusted timestamp is refused, and nothing is stored.', [
    call('addValidationInfo', 'd', { trust: ltvTrust, revocation: source(authority.ocspGood) }),
  ], ltvDoc(authority.cmsGood, stored(), [stores({})])),
  scenario('collect-partial', 'partial: what there is is stored, and the verdicts say what is missing.', [
    call('addValidationInfo', 'd', { trust: ltvTrust, revocation: source(authority.ocspGood), partial: true }),
  ], ltvDoc(authority.cmsGood, stored(), [stores({ certificates: 2 })])),
  scenario('collect-partial-unanswered', 'partial with a source that does not answer, and with known answers.', [
    call('addValidationInfo', 'd', { trust: ltvTrust, revocation: source(null), partial: true }),
    call('addValidationInfo', 'd', {
      trust: ltvTrust,
      known: { certificates: [$b(b64(authority.issuer))], ocspResponses: [$b(b64(authority.ocspGood))], crls: [$b(b64(authority.crl))] },
      partial: true,
      maxCrlBytes: 10,
    }),
  ], ltvDoc(authority.cmsGood, stored(), [stores({ certificates: 2 })])),
  scenario('collect-fields', 'fields picks the signatures to cover; a field with no signature is NotFound.', [
    call('addValidationInfo', 'd', { trust: ltvTrust, fields: [{ kind: 'fqn', name: 'Sig1' }], partial: true }),
    call('addValidationInfo', 'd', { trust: ltvTrust, fields: [{ kind: 'objectNumber', objectNumber: 9 }] }),
  ], ltvDoc(authority.cmsGood, stored(), [stores({ certificates: 2 })])),
];

// ── trusted timestamps, document timestamps, attaching, and answers in detail ─

const HOUR_MS = 3600 * 1000;
const TOKEN_TIME = JAN_2026 + HOUR_MS / 2;
/** The fixture timestamp authority as a raw key: what its tokens are signed with. */
const tsaRaw = (cert = 'tsa'): RawSigner => ({
  kind: 'raw',
  certificateChain: [bytesOf(fixtures.certs[cert]!)],
  algorithm: 'RSA-PKCS1-v1_5',
  hash: 'sha256',
  sign: async (data) => rawSignature('tsa', 'RSA-PKCS1-v1_5', 'sha256', data),
});
const sha256 = (bytes: Uint8Array) => new Uint8Array(createHash('sha256').update(bytes).digest());
const goodSignatureValue = signatureValueOf(parseCmsInternal(authority.cmsGood).signerInfo);
const stamp = (over: Partial<Parameters<typeof buildTestToken>[0]> = {}) =>
  buildTestToken({ digest: sha256(goodSignatureValue), authority: tsaRaw(), genTime: new Date(TOKEN_TIME), ...over });
const sigToken = await stamp();
const cmsStamped = addUnsignedAttribute(authority.cmsGood, timestampTokenAttribute(sigToken));
const cmsWrongStamp = addUnsignedAttribute(authority.cmsGood, timestampTokenAttribute(await stamp({ digest: digestBytes(32, 5) })));
const docToken = await buildTestToken({
  digest: authority.digest,
  authority: tsaRaw(),
  genTime: new Date(TOKEN_TIME + HOUR_MS),
});
const tsTrust = (...extra: string[]) => ({
  anchors: { $fn: 'trust.anchors', resolves: [$b(b64(authority.root)), ...extra.map(certArg)] },
});
const tsaKey = (token: Uint8Array | null, hash?: string) => ({
  kind: 'cms',
  ...(hash ? { hash } : {}),
  sign: token ? { $fn: 'tsa.sign', resolves: $b(b64(token)) } : { $fn: 'tsa.sign', resolves: $b('AQID') },
});
const docTimestampSignature = {
  ...ltvSignature,
  index: 1,
  field: { kind: 'fqn', name: 'TS1' },
  fieldName: 'TS1',
  kind: 'timestamp',
  filter: 'Adobe.PPKLite',
  subFilter: 'ETSI.RFC3161',
  revisionIndex: 1,
  signer: null,
};
/** A signature in revision 0 and a document timestamp in revision 1 over the same bytes. */
const twoRevisionDoc = (info: ReturnType<typeof stored>, token = docToken): Record<string, FakeDocumentSpec> => ({
  d: {
    pages: [],
    services: ['signatures'],
    responses: [
      {
        path: 'signatures.list',
        value: { chainValid: true, revisions: [{ index: 0 }, { index: 1 }], signatures: [ltvSignature, docTimestampSignature], protection: {} },
      },
      { path: 'signatures.getValidationInfo', value: info },
      { path: 'signatures.getContents', args: [{ kind: 'fqn', name: 'Sig1' }], value: $b(b64(authority.cmsGood)) },
      { path: 'signatures.getContents', args: [{ kind: 'fqn', name: 'TS1' }], value: $b(b64(token)) },
      { path: 'signatures.getDigest', value: $b(b64(authority.digest)) },
      {
        path: 'signatures.analyze',
        value: { current: { verdict: 'permitted', primary: null }, basis: { source: 'persisted' }, later: { revisionCount: 1, undoneObjectNumbers: [] } },
      },
    ],
  },
});
const ltPrepared = (over: Record<string, unknown> = {}) =>
  prepared({ digest: $b(b64(authority.digest)), contentsSizeBytes: 16384, ...over });
const completes: ResponseSpec = { path: 'signatures.complete', value: { status: 'completed', signature: { index: 0, fieldName: 'Sig1' } } };

/**
 * Answers built with the test authority's builders under the scripted WebCrypto: a root and a
 * signer of their own, a delegated responder, and answers each branch of `checkRevocationAnswer`
 * takes. `wellFormed` is the issuer's own good answer, from which the malformed ones are cut.
 */
const answers = await withWebCrypto(() => {}, async () => {
  const at = new Date(JAN_2026);
  const keys = await generateSigningKeyPair('RSA-PKCS1-v1_5', 'sha256', true);
  const dates = { notBefore: new Date(JAN_2026 - 24 * HOUR_MS), notAfter: new Date(JAN_2026 + 365 * 24 * HOUR_MS) };
  const root: Issuer = {
    certificate: await issueCertificate({ kind: 'authority', commonName: 'Answers Root', publicKey: keys.publicKey, privateKey: keys.privateKey, ...dates }),
    privateKey: keys.privateKey,
  };
  const other: Issuer = {
    certificate: await issueCertificate({ kind: 'authority', commonName: 'Other Root', publicKey: keys.publicKey, privateKey: keys.privateKey, ...dates }),
    privateKey: keys.privateKey,
  };
  const signer = await issueCertificate({ kind: 'signer', commonName: 'Answers Signer', publicKey: keys.publicKey, issuer: root, ...dates });
  const responder: Issuer = {
    certificate: await issueCertificate({ kind: 'responder', commonName: 'Answers Responder', publicKey: keys.publicKey, issuer: root, ...dates }),
    privateKey: keys.privateKey,
  };
  const notResponder: Issuer = {
    certificate: await issueCertificate({ kind: 'signer', commonName: 'Not A Responder', publicKey: keys.publicKey, issuer: root, ...dates }),
    privateKey: keys.privateKey,
  };
  const expiredResponder: Issuer = {
    certificate: await issueCertificate({
      kind: 'responder', commonName: 'Expired Responder', publicKey: keys.publicKey, issuer: root,
      notBefore: new Date(JAN_2026 - 48 * HOUR_MS), notAfter: new Date(JAN_2026 - 24 * HOUR_MS),
    }),
    privateKey: keys.privateKey,
  };
  const foreignResponder: Issuer = {
    certificate: await issueCertificate({ kind: 'responder', commonName: 'Answers Responder', publicKey: keys.publicKey, issuer: other, ...dates }),
    privateKey: keys.privateKey,
  };
  const ocsp = (over: Partial<Parameters<typeof buildOcspResponse>[0]> = {}) =>
    buildOcspResponse({
      certificate: signer, issuer: root.certificate, signer: root, status: 'good',
      producedAt: at, thisUpdate: at, nextUpdate: new Date(JAN_2026 + HOUR_MS), ...over,
    });
  const crl = (over: Partial<Parameters<typeof buildCrl>[0]> = {}) =>
    buildCrl({ issuer: root, thisUpdate: at, nextUpdate: new Date(JAN_2026 + 7 * 24 * HOUR_MS), revoked: [], ...over });
  // pkijs is the signature package's dependency, not this tool's.
  const pkijs = await import('../../../../packages/core/signature/node_modules/pkijs/build/index.js');
  const make = (extnID: string, value: number[], critical = false) =>
    new pkijs.Extension({ extnID, critical, extnValue: new Uint8Array(value).buffer });
  const der = (cert: { toSchema: (b: boolean) => { toBER: (b: boolean) => ArrayBuffer } }) => new Uint8Array(cert.toSchema(true).toBER(false));
  const good = await ocsp();
  const asn1js = await import('../../../../packages/core/signature/node_modules/asn1js/build/index.js');
  /** The identifier the issuer's own answer would carry, changed by `change`. */
  const askedWith = async (change: (id: InstanceType<typeof pkijs.CertID>) => void) => {
    const id = new pkijs.CertID();
    await id.createForCertificate(signer, { hashAlgorithm: 'SHA-1', issuerCertificate: root.certificate });
    change(id);
    return id;
  };
  const SHA256_RSA = [0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x0b];
  const UNKNOWN_SIG = [0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x7f];
  const swapLast = (bytes: Uint8Array, from: number[], to: number[]) => {
    for (let i = bytes.length - from.length; i >= 0; i--) {
      if (from.every((b, j) => bytes[i + j] === b)) {
        const out = bytes.slice();
        out.set(to, i);
        return out;
      }
    }
    throw new Error('[signature-core] no bytes to swap');
  };
  const flipLast = (bytes: Uint8Array) => {
    const out = bytes.slice();
    out[out.length - 1] ^= 1;
    return out;
  };
  // An issuer whose key usage leaves out cRLSign, and a signer it issued.
  const plainIssuer: Issuer = {
    certificate: await issueCertificate({ kind: 'signer', commonName: 'No CRL Signing', publicKey: keys.publicKey, privateKey: keys.privateKey, ...dates }),
    privateKey: keys.privateKey,
  };
  const underPlain = await issueCertificate({ kind: 'signer', commonName: 'Under No CRL Signing', publicKey: keys.publicKey, issuer: plainIssuer, ...dates });
  void asn1js;
  const swap = (bytes: Uint8Array, from: number[], to: number[]) => {
    for (let i = 0; i + from.length <= bytes.length; i++) {
      if (from.every((b, j) => bytes[i + j] === b)) {
        const out = bytes.slice();
        out.set(to, i);
        return out;
      }
    }
    throw new Error('[signature-core] no bytes to swap');
  };
  return {
    root: der(root.certificate),
    otherRoot: der(other.certificate),
    signer: der(signer),
    ocsp: {
      good,
      byKey: await ocsp({ responderId: 'key' }),
      sha256Id: await ocsp({ idHash: 'SHA-256' }),
      revoked: await ocsp({ status: { revokedAt: new Date(JAN_2026 - HOUR_MS), reason: 'key-compromise' } }),
      revokedNoReason: await ocsp({ status: { revokedAt: new Date(JAN_2026 - HOUR_MS) } }),
      unknown: await ocsp({ status: 'unknown' }),
      openWindow: await ocsp({ nextUpdate: null }),
      delegated: await ocsp({ signer: responder }),
      notDelegated: await ocsp({ signer: notResponder }),
      expiredResponder: await ocsp({ signer: expiredResponder }),
      foreignResponder: await ocsp({ signer: foreignResponder }),
      otherCertificate: await ocsp({ certificate: root.certificate }),
      constructedGood: swap(good, [0x80, 0x00, 0x18, 0x0f], [0xa0, 0x00, 0x18, 0x0f]),
      primitiveRevoked: swap(good, [0x80, 0x00, 0x18, 0x0f], [0x81, 0x00, 0x18, 0x0f]),
      status1: new Uint8Array([0x30, 0x03, 0x0a, 0x01, 0x01]),
      status9: new Uint8Array([0x30, 0x03, 0x0a, 0x01, 0x09]),
      noBytes: new Uint8Array([0x30, 0x03, 0x0a, 0x01, 0x00]),
      otherType: new Uint8Array([0x30, 0x13, 0x0a, 0x01, 0x00, 0xa0, 0x0e, 0x30, 0x0c, 0x06, 0x05, 0x2b, 0x06, 0x01, 0x05, 0x05, 0x04, 0x03, 0x01, 0x02, 0x03]),
      innerGarbage: new Uint8Array([0x30, 0x16, 0x0a, 0x01, 0x00, 0xa0, 0x11, 0x30, 0x0f, 0x06, 0x09, 0x2b, 0x06, 0x01, 0x05, 0x05, 0x07, 0x30, 0x01, 0x01, 0x04, 0x02, 0x01, 0x02]),
      notDer: new Uint8Array([0x30, 0x80]),
      badSignature: (() => { const out = good.slice(); out[out.length - 1] ^= 1; return out; })(),
      unknownIdHash: await ocsp({ asked: await askedWith((id) => { id.hashAlgorithm.algorithmId = '1.2.3.4.5'; }) }),
      otherSerial: await ocsp({ asked: await askedWith((id) => { id.serialNumber = new asn1js.Integer({ value: 7 }); }) }),
      responderNotIncluded: swapLast(good, [...new TextEncoder().encode('Answers Root')], [...new TextEncoder().encode('Answers Rooz')]),
      unknownSignatureAlgorithm: swapLast(good, SHA256_RSA, UNKNOWN_SIG),
    },
    plainIssuer: der(plainIssuer.certificate),
    underPlain: der(underPlain),
    crlByPlainIssuer: await crl({ issuer: plainIssuer }),
    crl: {
      good: await crl(),
      revoked: await crl({ revoked: [{ serialNumber: signer.serialNumber, at: new Date(JAN_2026 - HOUR_MS), reason: 'superseded' }] }),
      revokedNoReason: await crl({ revoked: [{ serialNumber: signer.serialNumber, at: new Date(JAN_2026 - HOUR_MS) }] }),
      otherIssuer: await crl({ issuer: other }),
      delta: await crl({ extensions: [make('2.5.29.27', [0x02, 0x01, 0x01], true)] }),
      indirect: await crl({ extensions: [make('2.5.29.28', [0x30, 0x03, 0x84, 0x01, 0xff], true)] }),
      onlyCa: await crl({ extensions: [make('2.5.29.28', [0x30, 0x03, 0x82, 0x01, 0xff], true)] }),
      onlyUser: await crl({ extensions: [make('2.5.29.28', [0x30, 0x03, 0x81, 0x01, 0xff], true)] }),
      someReasons: await crl({ extensions: [make('2.5.29.28', [0x30, 0x04, 0x83, 0x02, 0x07, 0x80], true)] }),
      badScope: await crl({ extensions: [make('2.5.29.28', [0x04, 0x00], true)] }),
      unknownCritical: await crl({ extensions: [make('1.3.6.1.4.1.99999.2', [0x05, 0x00], true)] }),
      notDer: new Uint8Array([0x30, 0x80]),
      notCrl: new Uint8Array([0x30, 0x03, 0x02, 0x01, 0x01]),
      badSignature: flipLast(await crl()),
      unknownSignatureAlgorithm: swapLast(await crl(), SHA256_RSA, UNKNOWN_SIG),
    },
  };
});
const check = (kind: 'ocsp' | 'crl', der: Uint8Array, certificate = answers.signer, issuer = answers.root) =>
  call('checkRevocationAnswer', { kind, der: $b(b64(der)), certificate: $b(b64(certificate)), issuer: $b(b64(issuer)) });

/**
 * A path with an issuer the CMS leaves out: a root, an intermediate it issued, and a signer the
 * intermediate issued that names where its issuer's certificate is and nothing to ask about
 * itself. The CMS carries the signer alone. Made under the scripted WebCrypto.
 */
const orphan = await withWebCrypto(() => {}, async () => {
  const keys = await generateSigningKeyPair('RSA-PKCS1-v1_5', 'sha256', true);
  const dates = { notBefore: new Date(JAN_2026 - 24 * 3600 * 1000), notAfter: new Date(JAN_2026 + 365 * 24 * 3600 * 1000) };
  const root: Issuer = {
    certificate: await issueCertificate({ kind: 'authority', commonName: 'Orphan Root', publicKey: keys.publicKey, privateKey: keys.privateKey, ...dates }),
    privateKey: keys.privateKey,
  };
  const inter: Issuer = {
    certificate: await issueCertificate({ kind: 'authority', commonName: 'Orphan Intermediate', publicKey: keys.publicKey, issuer: root, ...dates }),
    privateKey: keys.privateKey,
  };
  const leaf = await issueCertificate({
    kind: 'signer', commonName: 'Orphan Signer', publicKey: keys.publicKey, issuer: inter,
    addresses: { issuer: 'http://orphan.example/inter.cer' }, ...dates,
  });
  const der = (c: { toSchema: (b: boolean) => { toBER: (b: boolean) => ArrayBuffer } }) => new Uint8Array(c.toSchema(true).toBER(false));
  const signer = webCryptoSigner({ privateKey: keys.privateKey, certificateChain: [der(leaf)], hash: 'sha256' });
  const pkijs = await import('../../../../packages/core/signature/node_modules/pkijs/build/index.js');
  // A degenerate PKCS #7 SignedData holding the intermediate: what some authorities serve.
  const bundle = new pkijs.ContentInfo({
    contentType: '1.2.840.113549.1.7.2',
    content: new pkijs.SignedData({
      version: 1,
      encapContentInfo: new pkijs.EncapsulatedContentInfo({ eContentType: '1.2.840.113549.1.7.1' }),
      certificates: [inter.certificate],
      signerInfos: [],
    }).toSchema(true),
  });
  return {
    root: der(root.certificate),
    inter: der(inter.certificate),
    bundle: new Uint8Array(bundle.toSchema().toBER(false)),
    cms: await buildDetachedCms({ digest: authority.digest, hash: 'sha256', profile: 'cades-b', signer }),
  };
});
const orphanTrust = { anchors: { $fn: 'trust.anchors', resolves: [$b(b64(orphan.root))] } };
/** A CMS with lengths written as indefinite, outside the SignedData: BER a reader must accept. */
const indefinite = (cms: Uint8Array) => {
  const outer = cms[1]! & 0x7f;
  const body = cms.subarray(2 + (cms[1]! & 0x80 ? outer : 0));
  const oid = body.subarray(0, 2 + body[1]!);
  const wrapped = body.subarray(oid.length);
  const inner = wrapped.subarray(2 + (wrapped[1]! & 0x80 ? wrapped[1]! & 0x7f : 0));
  return new Uint8Array([0x30, 0x80, ...oid, 0xa0, 0x80, ...inner, 0, 0, 0, 0]);
};
/** An unsigned attribute that is no timestamp: `oid` with a NULL value. */
const otherAttribute = (oid: number[]) => new Uint8Array([0x30, 6 + oid.length, 0x06, oid.length, ...oid, 0x31, 0x02, 0x05, 0x00]);
const withOthers = addUnsignedAttribute(
  addUnsignedAttribute(authority.cmsGood, otherAttribute([0x55, 0x04, 0x03])),
  otherAttribute([0x2a, 0x03]),
);

const longTermCases2: ScenarioCase[] = [
  scenario('fill-issuer-fetched', 'An issuer the CMS leaves out is fetched from the address the signer names, and the path is followed.', [
    call('validateSignatures', 'd', { trust: orphanTrust, validationTime: stampedAt, revocation: source(orphan.inter) }),
  ], ltvDoc(orphan.cms, stored())),
  scenario('fill-issuer-bundle', 'The issuer address serves a PKCS #7 bundle; and one that serves nothing readable.', [
    call('validateSignatures', 'd', { trust: orphanTrust, validationTime: stampedAt, revocation: source(orphan.bundle) }),
    call('validateSignatures', 'd', { trust: orphanTrust, validationTime: stampedAt, revocation: source(new Uint8Array([1, 2, 3])) }),
  ], ltvDoc(orphan.cms, stored())),
  scenario('fill-names-no-address', 'A certificate that names no address to ask at, collected all or nothing.', [
    call('addValidationInfo', 'd', { trust: orphanTrust, revocation: source(orphan.inter), partial: true }),
  ], ltvDoc(orphan.cms, stored(), [stores({ certificates: 3 })])),
  scenario('fill-crl-over-limit', 'An answer that says nothing, then a list over maxCrlBytes: both named, nothing stored.', [
    call('addValidationInfo', 'd', { trust: tsTrust('tsa'), revocation: source(new Uint8Array([1, 2, 3])), maxCrlBytes: 2 }),
    call('addValidationInfo', 'd', { trust: tsTrust('tsa'), revocation: source(authority.crl), maxCrlBytes: 10 }),
  ], ltvDoc(cmsStamped, stored(), [stores({})])),
  scenario('fill-source-throws', 'A source that throws something other than an EngineError: a Network error naming the address.', [
    call('addValidationInfo', 'd', { trust: tsTrust('tsa'), revocation: { fetch: { $fn: 'revocation.fetch', rejects: { name: 'TypeError', message: 'socket hang up' } } } }),
  ], ltvDoc(cmsStamped, stored(), [stores({})])),
  scenario('splice-indefinite-and-ordered', 'Attaching to a CMS with indefinite lengths keeps them indefinite; attaching beside other unsigned attributes puts the token where DER orders it.', [
    call('attachSignatureTimestamp', { cms: $b(b64(indefinite(authority.cmsGood))), authority: tsaKey(sigToken) }),
    call('attachSignatureTimestamp', { cms: $b(b64(withOthers)), authority: tsaKey(sigToken) }),
  ]),
  scenario('route-max-reply-bytes', 'A revocation source with its reply limit set: an answer within it is passed on.', [
    call('revocationRoute', { url: ROUTE, retries: 0, maxReplyBytes: 1024 }, { kind: 'crl', url: 'http://crl.example/a.crl' }, [reply(200, $b('AQID'))]),
  ]),
  scenario('tsa-status-text-not-utf8', 'A TimeStampResp whose status text is a PrintableString does not fit the schema.', [
    call('timestampHttp', { url: TSA_URL, retries: 0 }, tsaRequest(), [
      reply(200, $b(b64(der(0x30, der(0x30, new Uint8Array([0x02, 0x01, 0x02, ...der(0x30, der(0x13, new TextEncoder().encode('no')))])))))),
    ]),
  ]),
  scenario('lt-timestamped-enabled', 'A signature with a trusted timestamp and a stored answer good at its time is long-term enabled.', [
    call('validateSignatures', 'd', { trust: tsTrust('tsa') }),
    call('validateSignatures', 'd', { trust: tsTrust('tsa'), validationTime: { kind: 'now' } }),
  ], ltvDoc(cmsStamped, stored({ ocspResponses: [authority.ocspGood] }))),
  scenario('lt-timestamped-no-answer', 'A trusted timestamp with nothing stored: no answer for the signer at that time.', [
    call('validateSignatures', 'd', { trust: tsTrust('tsa') }),
  ], ltvDoc(cmsStamped, stored())),
  scenario('lt-timestamp-untrusted', "Without the authority's certificate among the anchors, the timestamp proves no time to you.", [
    call('validateSignatures', 'd', { trust: tsTrust() }),
    call('validateSignatures', 'd', {}),
  ], ltvDoc(cmsStamped, stored({ ocspResponses: [authority.ocspGood] }))),
  scenario('lt-timestamp-not-its-own', 'A timestamp over other bytes is invalid; the signature stays valid.', [
    call('validateSignatures', 'd', { trust: tsTrust('tsa') }),
  ], ltvDoc(cmsWrongStamp, stored())),
  scenario('lt-tsa-purposes', 'An authority whose timestamping purpose is not critical, or not its only one, is not trusted to say the time.', [
    call('verifyTimestampToken', { token: $b(b64(await buildTestToken({ digest: TSA_DIGEST, authority: tsaRaw('tsaNotCritical'), genTime: new Date(TOKEN_TIME) }))), digest: $b(b64(TSA_DIGEST)), algorithm: 'sha256', trust: tsTrust('tsaNotCritical') }),
    call('verifyTimestampToken', { token: $b(b64(await buildTestToken({ digest: TSA_DIGEST, authority: tsaRaw('tsaTwoPurposes'), genTime: new Date(TOKEN_TIME) }))), digest: $b(b64(TSA_DIGEST)), algorithm: 'sha256', trust: tsTrust('tsaTwoPurposes') }),
    call('verifyTimestampToken', { token: $b(b64(await buildTestToken({ digest: TSA_DIGEST, authority: tsaRaw(), genTime: new Date(Date.UTC(2050, 0, 1)) }))), digest: $b(b64(TSA_DIGEST)), algorithm: 'sha256', trust: tsTrust('tsa') }),
    call('verifyTimestampToken', { token: $b(b64(await buildTestToken({ digest: TSA_DIGEST, authority: tsaRaw(), genTime: new Date(TOKEN_TIME) }))), digest: $b(b64(TSA_DIGEST)), algorithm: 'sha256', trust: { anchors: { $fn: 'trust.anchors', resolves: [] }, timestampAnchors: { $fn: 'trust.timestampAnchors', resolves: [certArg('tsa')] } } }),
    call('verifyTimestampToken', { token: $b(b64(await buildTestToken({ digest: TSA_DIGEST, authority: tsaRaw(), genTime: new Date(TOKEN_TIME) }))), digest: $b(b64(TSA_DIGEST)), algorithm: 'sha256', trust: { anchors: { $fn: 'trust.anchors', resolves: [certArg('tsa')] }, timestampAnchors: { $fn: 'trust.timestampAnchors', resolves: [] } } }),
  ]),
  scenario('lt-document-timestamp', 'A document timestamp after the signature: it vouches for the signature at its time, and is judged now itself.', [
    call('validateSignatures', 'd', { trust: tsTrust('tsa') }),
    call('validateSignatures', 'd', { trust: tsTrust('tsa'), validationTime: { kind: 'earliest-timestamp' } }),
  ], twoRevisionDoc(stored({ ocspResponses: [authority.ocspGood] }))),
  scenario('lt-document-timestamp-other-bytes', 'A document timestamp over other bytes: integrity invalid, the token judged against what it stamps.', [
    call('validateSignatures', 'd', { trust: tsTrust('tsa') }),
  ], twoRevisionDoc(stored(), await buildTestToken({ digest: digestBytes(32, 77), authority: tsaRaw(), genTime: new Date(TOKEN_TIME) }))),
  scenario('lt-document-timestamp-unreadable', 'A document timestamp whose contents are no token.', [
    call('validateSignatures', 'd', { trust: tsTrust('tsa') }),
  ], twoRevisionDoc(stored(), new Uint8Array([1, 2, 3]))),
  scenario('lt-collect-timestamped', 'addValidationInfo for a timestamped signature asks for the answer and stores it with the paths.', [
    call('addValidationInfo', 'd', { trust: tsTrust('tsa'), revocation: source(authority.ocspGood) }),
    call('addValidationInfo', 'd', { trust: tsTrust('tsa'), revocation: source(authority.ocspGood), partial: true }),
    call('addValidationInfo', 'd', { trust: tsTrust('tsa'), revocation: source(null) }),
    call('addValidationInfo', 'd', { trust: tsTrust('tsa'), revocation: source(authority.ocspWithdrawn) }),
  ], ltvDoc(cmsStamped, stored(), [stores({ certificates: 3, ocspResponses: 1 })])),
  scenario('lt-collect-stored', 'addValidationInfo when the file already holds the answer: nothing new to store.', [
    call('addValidationInfo', 'd', { trust: tsTrust('tsa') }),
  ], ltvDoc(cmsStamped, stored({ ocspResponses: [authority.ocspGood], certificates: [authority.good, authority.root, bytesOf(fixtures.certs.tsa!)] }), [stores({})])),
  scenario('attach-timestamp', "attachSignatureTimestamp: a token over the signature value, spliced in as an unsigned attribute; and what it refuses.", [
    call('attachSignatureTimestamp', { cms: $b(b64(authority.cmsGood)), authority: tsaKey(sigToken) }),
    call('attachSignatureTimestamp', { cms: $b(b64(authority.cmsGood)), authority: tsaKey(sigToken), trust: tsTrust('tsa') }),
    call('attachSignatureTimestamp', { cms: $b(b64(authority.cmsGood)), authority: tsaKey(sigToken), trust: tsTrust() }),
    call('attachSignatureTimestamp', { cms: $b(b64(cmsStamped)), authority: tsaKey(sigToken) }),
    call('attachSignatureTimestamp', { cms: $b(b64(authority.cmsGood)), authority: tsaKey(await stamp({ digest: digestBytes(32, 5) })) }),
    call('attachSignatureTimestamp', { cms: $b(b64(authority.cmsGood)), authority: tsaKey(await stamp({ hash: 'sha384', digest: digestBytes(48, 5) })) }),
    call('attachSignatureTimestamp', { cms: $b(b64(authority.cmsGood)), authority: tsaKey(null) }),
    call('attachSignatureTimestamp', { cms: $b(b64(authority.cmsGood)), authority: tsaKey(sigToken, 'sha384') }),
    call('attachSignatureTimestamp', { cms: $b('AQID'), authority: tsaKey(sigToken) }),
    call('attachSignatureTimestamp', { cms: $b(b64(new Uint8Array([...authority.cmsGood, 0, 0, 0]))), authority: tsaKey(sigToken) }),
    call('attachSignatureTimestamp', { cms: $b(b64(new Uint8Array([...authority.cmsGood, 7]))), authority: tsaKey(sigToken) }),
  ]),
  scenario('complete-signature', 'completeSignature: the gate, the timestamp, the gate again, complete; and its refusals.', [
    call('completeSignature', 'd', { prepared: ltPrepared(), cms: $b(b64(authority.cmsGood)), timestampAuthority: tsaKey(sigToken) }),
    call('completeSignature', 'd', { prepared: ltPrepared(), cms: $b(b64(authority.cmsGood)) }),
    call('completeSignature', 'd', { prepared: ltPrepared(), cms: $b(b64(authority.cmsGood)), timestampTrust: tsTrust('tsa') }),
    call('completeSignature', 'd', { prepared: ltPrepared({ digest: $b(b64(digestBytes(32, 9))) }), cms: $b(b64(authority.cmsGood)) }),
    call('completeSignature', 'd', { prepared: ltPrepared({ subFilter: 'ETSI.RFC3161' }), cms: $b(b64(docToken)), timestampAuthority: tsaKey(sigToken) }),
    call('completeSignature', 'd', { prepared: ltPrepared({ subFilter: 'ETSI.RFC3161' }), cms: $b(b64(docToken)), timestampTrust: tsTrust('tsa') }),
    call('completeSignature', 'd', { prepared: ltPrepared({ subFilter: 'ETSI.RFC3161' }), cms: $b(b64(docToken)), timestampTrust: tsTrust() }),
  ], { d: { pages: [], services: ['signatures'], responses: [completes] } }),
  scenario('gate-signature-timestamps', "The gate refuses a CMS whose timestamp stamps other bytes, and checks a document timestamp's token.", [
    call('verifyForCompletion', { cms: $b(b64(cmsWrongStamp)), prepared: ltPrepared(), profile: 'cades-b' }),
    call('verifyForCompletion', { cms: $b(b64(cmsStamped)), prepared: ltPrepared(), profile: 'cades-b' }),
    call('verifyForCompletion', { cms: $b(b64(await buildTestToken({ digest: authority.digest, authority: tsaRaw(), genTime: new Date(TOKEN_TIME), carriesCertificate: false }))), prepared: ltPrepared(), profile: 'timestamp' }),
    call('verifyForCompletion', { cms: $b(b64(await buildTestToken({ digest: authority.digest, authority: tsaRaw(), genTime: new Date(TOKEN_TIME), binding: null }))), prepared: ltPrepared(), profile: 'timestamp' }),
    call('verifyForCompletion', { cms: $b(b64(await buildTestToken({ digest: authority.digest, authority: tsaRaw(), genTime: new Date(TOKEN_TIME), binding: { version: 2, certHash: digestBytes(32, 1) } }))), prepared: ltPrepared(), profile: 'timestamp' }),
    call('verifyForCompletion', { cms: $b(b64(await buildTestToken({ digest: authority.digest, authority: tsaRaw(), genTime: new Date(TOKEN_TIME), binding: { version: 2, hashOid: '1.2.3.4' } }))), prepared: ltPrepared(), profile: 'timestamp' }),
    call('verifyForCompletion', { cms: $b(b64(await buildTestToken({ digest: authority.digest, authority: tsaRaw(), genTime: new Date(TOKEN_TIME), binding: { version: 1 } }))), prepared: ltPrepared(), profile: 'timestamp' }),
    call('verifyForCompletion', { cms: $b(b64(await buildTestToken({ digest: authority.digest, authority: tsaRaw(), genTime: new Date(TOKEN_TIME), swapsContent: true }))), prepared: ltPrepared(), profile: 'timestamp' }),
    call('verifyForCompletion', { cms: $b(b64(docToken)), prepared: ltPrepared({ algorithm: 'sha384' }), profile: 'timestamp' }),
  ]),
  scenario('answers-ocsp', 'checkRevocationAnswer over OCSP responses: each branch, the well-formed ones and the malformed.', [
    ...Object.values(answers.ocsp).map((der) => check('ocsp', der)),
    check('ocsp', answers.ocsp.good, answers.signer, answers.otherRoot),
  ]),
  scenario('answers-crl', 'checkRevocationAnswer over CRLs: each branch, the well-formed ones and the malformed.', [
    ...Object.values(answers.crl).map((der) => check('crl', der)),
    check('crl', answers.crl.good, answers.root, answers.root),
    check('crl', answers.crlByPlainIssuer, answers.underPlain, answers.plainIssuer),
  ]),
];

export const suites: Suite[] = [
  {
    kind: 'function',
    file: 'core/signature-parse.json',
    describes: 'packages/core/signature: parseDetachedCms over built, altered and third-party CMS; profileFor; certificateCommonName.',
    functions: parseFns,
    cases: parseCases,
  },
  {
    kind: 'scenario',
    file: 'core/signature-verify.json',
    describes: 'packages/core/signature: verifyCmsSignature, buildDetachedCms and the completion gate.',
    cases: [...verifyCases, ...buildCases, ...gateCases],
  },
  {
    kind: 'scenario',
    file: 'core/signature-trust.json',
    describes: "packages/core/signature: validateChain over pkijs's chain engine.",
    cases: trustCases,
  },
  {
    kind: 'scenario',
    file: 'core/signature-document.json',
    describes:
      "packages/core/signature: sign() and validateSignatures() over a document handle's signatures service.",
    cases: [...signCases, ...validateCases],
  },
  {
    kind: 'scenario',
    file: 'core/signature-signers.json',
    describes: 'packages/core/signature: the signer ports and WebCrypto adapters, against a scripted WebCrypto.',
    cases: signerCases,
  },
  {
    kind: 'scenario',
    file: 'core/signature-timestamp.json',
    describes:
      'packages/core/signature: httpTimestampAuthority and httpRevocationSource over a scripted transport (the D-1 request and reply, the D-2 route request), verifyTimestampToken, ecdsaRawFromDer.',
    cases: longTermCases,
  },
  {
    kind: 'scenario',
    file: 'core/signature-revocation.json',
    describes:
      'packages/core/signature: revocation answers from a test certificate authority, long-term validation, and addValidationInfo.',
    cases: revocationCases,
  },
  {
    kind: 'scenario',
    file: 'core/signature-long-term.json',
    describes:
      'packages/core/signature: trusted signature and document timestamps, long-term validation, attachSignatureTimestamp, completeSignature, the gate on tokens, and checkRevocationAnswer branch by branch.',
    cases: longTermCases2,
  },
];
