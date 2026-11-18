/**
 * The D-1 transport in the browser build: the one network door of the Rust
 * signature clients (`crates/plugins/signature-core/src/transport.rs`).
 * The Rust ports of upstream's `httpTimestampAuthority` and
 * `httpRevocationSource` build the request (the RFC 3161 query, the route's
 * JSON), retry, read the status and word every error; the host only posts.
 * Here the host is `fetch`, aimed at the app's own routes
 * (`timestampRoute()` and `revocationRoute()` in
 * `@embedpdf/core-signature/server`), as upstream's clients aim theirs.
 *
 * A transport crosses into the kernel as `{"$fn": "js:<id>"}` under the
 * signature config's `transport` key (`crates/plugins/signature/src/harness.rs`,
 * `transport_of`). The kernel calls it with the flat request, whose `body`
 * the wire decodes from `{"$bytes"}` to a `Uint8Array`, and reads back the
 * flat outcome, whose `body` the wire encodes the same way
 * (`crates/plugins/signature/src/transport.rs`, `request_json` and
 * `outcome_from_json`).
 */
import type {
  httpRevocationSource as HttpRevocationSource,
  httpTimestampAuthority as HttpTimestampAuthority,
} from '@embedpdf/plugin-signature';

export interface TransportHeader {
  name: string;
  value: string;
}

/** One POST, as the kernel hands it over. */
export interface TransportRequest {
  url: string;
  /** `application/timestamp-query` or `application/json`. */
  contentType: string;
  /** More headers, in order: an API key, a session token. */
  headers: TransportHeader[];
  body: Uint8Array;
  /** Answer `timedOut` when no whole reply came within this long. */
  timeoutMs: number;
  /** Answer `tooLarge` rather than read a reply past this many bytes. */
  maxReplyBytes: number;
}

/** What came back. A reply is `replied` whatever its status: the client reads it. */
export type TransportOutcome =
  | { kind: 'replied'; status: number; body: Uint8Array }
  | { kind: 'timedOut' }
  | { kind: 'tooLarge' }
  | { kind: 'failed'; message: string };

export type SignatureTransport = (request: TransportRequest) => Promise<TransportOutcome>;

export interface FetchTransportOptions {
  /** The `fetch` to use, for what `headers` can't do. Default: the global one, read per request. */
  fetch?: typeof globalThis.fetch;
}

/**
 * A transport over `fetch`. One request is one attempt, as one `ask` is in
 * upstream's clients: a POST with `content-type` and then the request's
 * headers (a later name wins, as in the clients' object spread), under an
 * `AbortController` that fires after `timeoutMs` and covers reading the
 * reply. A reply over `maxReplyBytes` (by its `content-length`, or once
 * that many bytes have been read) is cancelled and answers `tooLarge`, as
 * the routes' `readUpTo` does. A `fetch` that throws answers `failed` with
 * its message, and for a timestamp query in a page the clients' hint that
 * an authority is reached through a route; a body that breaks off answers
 * `failed` with `the reply broke off: ...`. Never rejects.
 */
export function fetchTransport(options: FetchTransportOptions = {}): SignatureTransport {
  return async (request) => {
    const send = options.fetch ?? globalThis.fetch;
    const headers: Record<string, string> = { 'content-type': request.contentType };
    for (const { name, value } of request.headers) headers[name] = value;
    const timeout = new AbortController();
    const timer = setTimeout(() => timeout.abort(), request.timeoutMs);
    try {
      let response: Response;
      try {
        response = await send(request.url, {
          method: 'POST',
          headers,
          body: new Uint8Array(request.body),
          signal: timeout.signal,
        });
      } catch (error) {
        if (timeout.signal.aborted) return { kind: 'timedOut' };
        // A browser reports a refused cross-origin call as a bare network error.
        const hint =
          request.contentType === 'application/timestamp-query' && 'document' in globalThis
            ? '. In a browser most authorities cannot be called directly: call yours through a route on your own server'
            : '';
        return { kind: 'failed', message: `${messageOf(error)}${hint}` };
      }
      const body = await readUpTo(response, request.maxReplyBytes);
      if (body === 'too-large') return { kind: 'tooLarge' };
      if (body instanceof Uint8Array) return { kind: 'replied', status: response.status, body };
      if (timeout.signal.aborted) return { kind: 'timedOut' };
      return { kind: 'failed', message: `the reply broke off: ${messageOf(body.error)}` };
    } finally {
      clearTimeout(timer);
    }
  };
}

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

/** The body, read up to `limit` bytes: its bytes, `too-large`, or the error it broke off with. */
async function readUpTo(
  response: Response,
  limit: number,
): Promise<Uint8Array | 'too-large' | { error: unknown }> {
  const declared = Number(response.headers?.get('content-length') ?? NaN);
  if (Number.isFinite(declared) && declared > limit) {
    await response.body?.cancel().catch(() => {});
    return 'too-large';
  }
  if (!response.body) {
    // A `Response` without a stream (a stubbed `fetch`): its bytes, then the limit.
    try {
      const bytes = new Uint8Array(await response.arrayBuffer());
      return bytes.byteLength > limit ? 'too-large' : bytes;
    } catch (error) {
      return { error };
    }
  }
  const reader = response.body.getReader();
  const chunks: Uint8Array[] = [];
  let size = 0;
  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > limit) {
        await reader.cancel().catch(() => {});
        return 'too-large';
      }
      chunks.push(value);
    }
  } catch (error) {
    return { error };
  }
  const bytes = new Uint8Array(size);
  let at = 0;
  for (const chunk of chunks) {
    bytes.set(chunk, at);
    at += chunk.byteLength;
  }
  return bytes;
}

/**
 * `httpTimestampAuthority(options)` for the Rust signature plugin: the same
 * options as upstream's, as a config the plugin's adapter turns into the
 * Rust client over {@link fetchTransport} (`options.fetch` included). Use
 * it as the `timestampAuthority` setting or a call's `timestampAuthority`.
 * It is a description, not a signer: only the Rust plugin can call it.
 */
export const httpTimestampAuthority: typeof HttpTimestampAuthority = (options) => {
  const { fetch, ...rest } = options;
  return { ...rest, transport: fetchTransport({ fetch }) } as unknown as ReturnType<
    typeof HttpTimestampAuthority
  >;
};

/**
 * `httpRevocationSource(options)` for the Rust signature plugin, as
 * {@link httpTimestampAuthority} is: the `revocation` setting.
 */
export const httpRevocationSource: typeof HttpRevocationSource = (options) => {
  const { fetch, ...rest } = options;
  return { ...rest, transport: fetchTransport({ fetch }) } as unknown as ReturnType<
    typeof HttpRevocationSource
  >;
};
