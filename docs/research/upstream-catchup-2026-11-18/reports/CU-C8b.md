# CU-C8b: the browser transport and the .NET regression rows

`research/catchup-2026-11-18-c8b`, cut from `research/catchup-2026-11-18-c8a` at `f4df85ab4`, gives
the browser build its D-1 transport: `fetchTransport()` in `packages/core-wasm`, posting to the
app's own routes, with `httpTimestampAuthority` and `httpRevocationSource` under upstream's names.
Its tests sign through upstream's real `timestampRoute` and `revocationRoute`, including a granted
timestamp and a long-term validation fill, so the plan's done line holds for the browser. The
browser table passes: `core-wasm` 131 of 131, `test:gc` 13 of 13, `epdf-web` 63 of 63. The .NET
rows pass on linux-arm64, linux-x64 (emulated) and osx-arm64. osx-x64's relocated smoke passes
under Rosetta, but `validate.sh` exits 1 at its record step (CU-C8b-D6). win-x64 was not run. No
upstream TS under `packages/**` changed. Decisions are in
[`../decisions/CU-C8b.md`](../decisions/CU-C8b.md).

## Commits

| Commit | Summary |
|---|---|
| `81dcdc120` | `core-wasm`: `src/signature-transport.ts`, its exports, `test/signature-transport.test.ts`; dev dependencies `@embedpdf/core-signature`, `pkijs`, `asn1js` |
| the docs commit | `scripts/cu-c8b/`, this report and the decisions |

## The browser transport

```ts
// packages/core-wasm/src/signature-transport.ts, exported from the package root
fetchTransport(options?: { fetch?: typeof globalThis.fetch }): SignatureTransport
type SignatureTransport = (request: TransportRequest) => Promise<TransportOutcome>
interface TransportRequest { url; contentType; headers: { name; value }[]; body: Uint8Array;
  timeoutMs; maxReplyBytes }
type TransportOutcome = { kind: 'replied'; status; body: Uint8Array } | { kind: 'timedOut' }
  | { kind: 'tooLarge' } | { kind: 'failed'; message }
httpTimestampAuthority(options)  // upstream's HttpTimestampAuthorityOptions
httpRevocationSource(options)    // upstream's HttpRevocationSourceOptions
```

An app writes `signaturePlugin({ timestampAuthority: httpTimestampAuthority({ url: '/api/timestamp' }),
revocation: httpRevocationSource({ url: '/api/revocation' }) })`, as with the TypeScript plugin.
The two helpers answer the `transport` config CU-C8a-D4 reads (CU-C8b-D3). The transport is one
attempt: `content-type` then the headers, an `AbortController` over `timeoutMs` that covers the
body, `tooLarge` past `maxReplyBytes`, `failed` for a thrown `fetch` (with upstream's route hint for
a timestamp query in a page) or a broken-off body, and `replied` for every status. Retries, the
250 ms delay, the status mapping and the error texts are the Rust clients' (CU-C8b-D1, CU-C8b-D2).
It never rejects. Those body outcomes are for a 2xx reply: any other status whose body can't be
read is `replied` with that status and no body, so the Rust client decides by the status, as
upstream's clients do (fix round 1).

## Tests

`packages/core-wasm/test/signature-transport.test.ts`, 23 tests (18, and 5 from fix round 1):

| Group | Tests | What they prove |
|---|---|---|
| stubbed `fetch` | 8 | `replied` (method, header order, body, any status), a header overriding `content-type`, the global `fetch` read per request, `timedOut` on the request and on the body, `tooLarge` by `content-length` and by bytes read (exactly the limit is a reply), `failed` with and without the route hint, a broken-off body |
| non-2xx and misuse (fix round 1) | 3 | a 400 whose body breaks off, a 503 past the limit and a 429 whose body stalls are each `replied` with the status and no body; a locked body fails a 2xx without rejecting; `httpTimestampAuthority` used as a key rejects with upstream's `InvalidArg` text |
| loopback `node:http` server | 3 | `replied` and `tooLarge` over a real connection with the headers as sent, `timedOut` on a held reply, `failed` with nothing listening |
| upstream routes in-process | 3 | `timestampRoute` passes the query and the authority's status back and refuses an empty query (413), and answers 504 for a silent authority; `revocationRoute` asks a CRL address with GET and refuses `127.0.0.1` with 403 `not-allowed` |
| Rust signature plugin | 6 | a `Uint8Array` body crossing to the transport and back (the reply's token reaches the nonce check, past the algorithm and digest checks); a granted timestamp sealed into the signature, its verdict `cryptography: 'valid'` with the authority's certificate (CU-C8b-D4); a long-term fill whose OCSP answer comes from `createTestCertificateAuthority()` through `revocationRoute`, verdict `summary: 'valid'`, `revocation: 'good'`, `longTerm.enabled: true`; a 503 from the authority retried once, then `did not answer: HTTP 503`; fix round 1: a 400 with a broken body is `refused the request: HTTP 400`, code `SignatureRefused`, asked once, and a 503 with an oversized body is `did not answer: HTTP 503`, code `Network`, asked twice |

One-step signing needs an `appearance`. Without one, upstream's `sign` passes
`appearance: { pdf: null }` and the engine refuses it, in the Rust port and upstream alike, so the
tests pass `signature_artwork.pdf`.

## Results

Browser rows: container `embed-pdf-catchup1118`, worktree `/workspaces/wt/CU-C8b`, `TOUCH=1
scripts/cu-c8b/wasm.sh /workspaces/wt/CU-C8b head-wasm scripts/wasm32-eh-pin.json` (CU-C7's
`wasm.sh` without the addon rows), at `81dcdc120`. The before column is CU-C7's final table, and
CU-C8a for `epdf-web`.

| Check | Before | Now |
|---|---|---|
| Rust-core payload (`build-target.sh wasm32-eh`, emsdk 4.0.0) | exit 0 | exit 0 |
| `build:wasm` | exit 0 | exit 0 |
| `core-wasm` vitest | 108 of 108, 12 files | 126 of 126, 13 files (the 18 above); 131 of 131 after fix round 1 (`test:wasm`, `fix1/core-wasm-test.log`) |
| `core-wasm` `test:gc` | 13 of 13 | 13 of 13 |
| `core-wasm` `typecheck` | pass | pass |
| `epdf-web` wasm tests | 63 passed in 9 binaries | 63 passed in 9 binaries |

.NET rows: `scripts/cu-c8b/dotnet.sh <checkout> head <rid> <runtime archive>` at `81dcdc120`, with
the in-tree runtime manifest (upstream runtime release `f2ea75972`, which the fork's `af480d0ce`
merges; its native libraries are that release's). The 2026-11-12 column is that catch-up's final
table, linux-arm64 only. Its 33 tests became 35 with `ced89d817` (two `[Fact]`s, already in
`cf7fdbe7a`); `platforms/dotnet` and `crates/epdf-capi` are unchanged from `cf7fdbe7a`.

| Row | 2026-11-12 | linux-arm64 | linux-x64 | osx-arm64 | osx-x64 |
|---|---|---|---|---|---|
| where | container | container | `cu-c8b-x64`, Rosetta (D5) | Mac host | Mac host, Rosetta (D6) |
| `dotnet test` | 33 of 33 | 35 of 35 | 35 of 35 | 35 of 35 | not run (D6) |
| parity self-test | 16 of 16 | 16 of 16 | 16 of 16 | 16 of 16 | not run (D6) |
| packaging unit tests | OK | OK | OK (2 skipped) | OK (2 skipped) | not run (D6) |
| capi release build | exit 0 | exit 0 | exit 0 | exit 0 | exit 0 |
| `pack.sh` | PASS | PASS | PASS | PASS | PASS |
| `validate.sh` | PASS | PASS | PASS, `container-emulated` | PASS | smoke PASS, exit 1 at the record (D6) |

Mac steps and why: osx-arm64 and osx-x64 need a macOS host (`otool`, `lipo`, the darwin
libraries, and `validate.sh`'s host RID check). .NET SDK 10.0.403 was installed under the job's
scratch dir because the host has only 11.0.100. win-x64 is not run: there is no Windows host.

## Not done

- The timestamped signature and the long-term fill don't apply to .NET (CU-C8b-D7, CU-C8a-D6).
- osx-x64 is not validated: no Intel Mac, and the schema has no emulated status for it (D6).
- `decisions.md` has no index entries for CU-C8a or CU-C8b; `scripts/cu-c7/index-check.sh` would
  list both jobs' headings as missing.

## Evidence

Logs in `/workspaces/logs/CU-C8b/` and `tmp/cu1118/logs/CU-C8b/` of the host checkout:
`head-wasm/wasm.all` with each row's log, and `dotnet-head-<rid>/summary.txt` with
`dotnet-test.log`, `parity.log`, `pack.log`, `validate.log`, `validate-record.json` and
`pack/package-manifest.<rid>.json` (the packages themselves were deleted).

## Cleanup

Host free space (`df -g /System/Volumes/Data`) read 441 GB at the start and 437 GB at the end;
`/workspaces` went from 8.1 GB to 8.8 GB, the rebuilt `target/debug` and wasm builds. Deleted,
about 4.3 GB: container `cu-c8b-x64` and its image (`mcr.microsoft.com/dotnet/sdk:10.0.403`,
906 MB), `/workspaces/x64` (869 MB, Rust and the linux-x64 runtime), `/workspaces/target-x64`
(752 MB), `/workspaces/target/aarch64-unknown-linux-gnu` (219 MB), `/workspaces/target/release`
(209 MB), the linux-x64 runtime archive, the `bin`, `obj` and `__pycache__` outputs of
`platforms/dotnet`, the `.nupkg` and staging dir of each pack (about 150 MB across the four RIDs),
and on the host the two .NET SDKs, the darwin runtimes and the Cargo target (1.3 GB). The host
worktree for the Mac steps and the bundle are removed once used. No trim was run.

Kept for CU-C8c and CU-C9: `/workspaces/wt/CU-C8b` (CU-C8a's worktree moved and switched, with its
`node_modules`, PDFium libraries and payload), `/workspaces/target/debug` (476 MB) and the wasm
target dirs.
