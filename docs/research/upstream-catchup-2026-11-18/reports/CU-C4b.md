# CU-C4b: the signature-core half of signatures

`research/catchup-2026-11-18-c4b` ports CU-C3's work order items 8 to 11 (CU-C4, engine and
signatures) onto `618824a53`. Items 8, 10 and 11 are done. Item 9 is ported except
`createTestCertificateAuthority`, which the vectors use at generation time only (CU-C4b-D4); after
fix round 1, 273 of the 2,148 uncovered `core/signature` lines still have no case. The 5
`epdf-signature-core` tests and all 77 replayed cases pass; `cargo test --workspace` fails 4
tests, all on CU-C3's list.
Decisions are in [`../decisions/CU-C4b.md`](../decisions/CU-C4b.md).

## Commits

| Commit | Items | Summary |
|---|---|---|
| `566881a44` | 8 | `validatePath` and the trust rewrite, parse bindings and tokens, `checkTimestampToken`, DER attribute order and serials, `cms/time.ts`, pkijs 4 and asn1js 4 encodings |
| `086ceb417` | 9, 10 | Verdicts with revocation, timestamps and `longTerm`; `judgement`, `long-term`, `revocation/*`; the D-1 transport |
| `10ead45d1` | 9, 10 | `attachSignatureTimestamp`, `completeSignature`, `timestampAuthority`, the RFC 3161 protocol, `httpTimestampAuthority`, `ecdsaRawFromDer` |
| `42a9d4da8` | 9, 11 | `addValidationInfo`; `core/signature-timestamp.json` (17 cases) |
| `6e6c8851f` | 9 | `core/signature-revocation.json` (11 cases) |
| `4d23a4bfc` | | Crate README |
| `8feabb0f5` | | `epdf-plugin-signature` builds against the new API, behaviour unchanged; `coverage.json` |
| `d7e432567` | | The plugin's `ffi` fake engine answers `signatures.getValidationInfo` |
| `62f25f22f` | | This report, the decisions, the job's scripts |
| `789f6146e` | 9 | Fix round 1: OCSP, CRL and TimeStampResp read through pkijs's schemas |
| `990c8a3ab` | 9 | Fix round 1: `tsa` fixtures, `core/signature-long-term.json` (15 cases), `max_reply_bytes` |
| the fix-round docs commit | | `coverage.json`, README, this report, the decisions |

## Items

8. Done (`566881a44`). `trust.rs` ports `validateChainAmong`, `validatePath`,
   `validateTimestampAuthority` and `firstWithoutIssuer`; `ValidationTime` reads `stampedAt` or
   `genTime` and reports both (`a425da7d3`). The four `TypeError` reasons of CU-C3-D7 replay as
   upstream gives them: they come from `validatePath` handing the engine the signer, not from pkijs
   4, whose chain engine decides as 3.4.0 did (CU-C4b-D1). `trust-policy-requireOnlyMapping` is a
   valid chain that upstream now reports untrusted (`(reading '0')`, path 3 before); the port does
   the same. DER attribute order (`15955a843`), DER serials (`6d6e76d3c`), `cms/time.ts`, ECDSA
   integers in the fewest octets, UTCTime as the year modulo 100 and a GeneralizedTime fraction
   without trailing zeros (`a841027e9`). The test authority's clock (`8a143eecf`) is used by the
   generator (CU-C4b-D4).
9. Partial. Ported: `cms/attach.ts`, `cms/splice.ts`, `cms/timestamp-protocol.ts`,
   `cms/verify-timestamp.ts`, `signers/ecdsa.ts`, `signers/timestamp-http.ts`, `gate.ts`
   timestamps, `sign.ts` (`sign_with`, `complete_signature`), `verdict.ts`, `judgement.ts`,
   `long-term.ts`, `collect.ts`, `revocation/{addresses,answer,fill,pool,request,source,status,http-source}`.
   Not ported: `signers/test-authority.ts` (CU-C4b-D4). 43 new cases in three files. Fix round 1
   added a `tsa` fixture certificate (critical `id-kp-timeStamping`, as upstream's
   `test/fixtures/long-term/timestamp-authority.cer`) and 15 cases: trusted signature and document
   timestamps, `longTerm.enabled`, attach and splice, `completeSignature`, the gate on tokens, and
   `checkRevocationAnswer` over 36 answers. Uncovered added lines in `core/signature`, by
   `scripts/cu-c3/gaps.py` (`gaps-after.txt`), from 2,148 to 273: `fill.ts` 54, `splice.ts` 37,
   `answer.ts` 27, `self-signed.ts` 21, `parse.ts` 19, `test-authority.ts` 19, `timestamp-http.ts`
   15, `trust.ts` 12, `webcrypto.ts` 11, `cms/timestamp.ts` 9, `verify-timestamp.ts` 8,
   `http-source.ts` 8, `gate.ts` 7, `judgement.ts` 5, `pool.ts` 5, `long-term.ts` 4,
   `addresses.ts` 4, `attach.ts` 3, `sign.ts` 2, `status.ts` 2, `collect.ts` 1. The rest of the
   range: 389 lines in 26 files.
10. Done (`086ceb417`, `10ead45d1`). See "D-1 transport trait".
11. Done (`42a9d4da8`). `core/signature-timestamp.json` pins the `RevocationRouteRequest` JSON for
    OCSP, CRL and issuer questions and the RFC 3161 query `httpTimestampAuthority` sends, with
    headers, policy and the nonce, over a scripted transport. `server/*` stays unported.

## D-1 transport trait

```rust
// crates/plugins/signature-core/src/transport.rs
pub struct TransportHeader { pub name: String, pub value: String }

pub struct TransportRequest {
    pub url: String,
    pub content_type: String,          // application/timestamp-query or application/json
    pub headers: Vec<TransportHeader>, // in order
    pub body: Vec<u8>,
    pub timeout_ms: u32,               // host answers TimedOut after this
    pub max_reply_bytes: u32,          // host answers TooLarge past this
}

pub enum TransportOutcome {
    Replied { status: u16, body: Vec<u8> },
    TimedOut,
    TooLarge,
    Failed { message: String },
}

pub trait HttpTransport {
    fn post(&self, request: TransportRequest) -> epdf_kernel::rt::Promise<TransportOutcome>;
}
```

A rejected promise counts as `Failed`. Limits: `MAX_REQUEST_BYTES` 65,536 (refused before
sending), `MAX_TIMESTAMP_REPLY_BYTES` 1,048,576, `MAX_REVOCATION_REPLY_BYTES` 2,097,152,
`DEFAULT_TIMEOUT_MS` 10,000, `RETRY_DELAY_MS` 250. Clients:
`http_timestamp_authority(transport, crypto, HttpTimestampAuthorityOptions)` and
`http_revocation_source(transport, HttpRevocationSourceOptions)`. `RecordedTransport` replays a list
of outcomes and keeps the requests. The trait follows the crate's other host ports (`TrustPort`,
`WebCrypto`), so each wrapper adapts the `Promise` as it does for those (CU-C4b-D2).

## Tests

All in container `embed-pdf-catchup1118`, worktree `/workspaces/wt/CU-C4b`,
`CARGO_TARGET_DIR=/workspaces/target`.

| Check | CU-C4a (`1239df825`) | Now (`990c8a3ab`) |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,487 passed, 14 failed, 2 ignored | 3,501 passed, 4 failed, 2 ignored |
| `epdf-signature-core` `conformance` | 0 of 5 | 8 of 8 (3 new files, 503 cases) |
| engine `:check` scripts | 36 of 36 | 36 of 36 |
| plugin vectors twice | | identical sha256 lists |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | all pass | all pass |
| plugin `replay:check` | 118 cases in 6 files | 13 cases in 4 files |
| TS `turbo run test` over `packages/**` | 98 of 98 | 98 of 98 |

The 4 failures: `epdf-plugin-annotation` `tools_match_the_typescript` (7 cases, CU-C5 and CU-C6),
`epdf-plugin-form` `design_matches_the_typescript` (1, CU-C5), `epdf-plugin-interaction`
`interaction_matches_the_typescript` (1, CU-C5), `epdf-plugin-signature`
`the_plugin_matches_the_typescript` (4 of 39, CU-C6: `SignFieldResult.validationInfo` and
`completeSignature`'s refusal text). No new failing name. The 5 `epdf-plugin-signature` `behavior`
tests on CU-C3's list now pass, and its replay went from 32 differing cases to 4. One test outside
the lists failed on the way, `tests/ffi.rs`
`without_anchors_a_good_signature_is_at_best_valid_untrusted`: its fake engine had no answer for
`signatures.getValidationInfo`, which every validation reads now; `d7e432567` adds one.

## Proposed exceptions

None for the trust reasons (CU-C4b-D1). One Rust-only behaviour to rule on, the transport's limits
(CU-C4b-D3): a request over 64 KiB is refused and a reply over 1 MiB (timestamp) or
`max_reply_bytes` (revocation, 2 MiB by default) is no answer, where upstream's browser clients
leave limits to the route.

Fix round 1 withdrew the second proposal. OCSP, CRL and TimeStampResp now go through pkijs's
schemas with the `compareSchema` port (`789f6146e`); the review's probe (`certStatus` `A0 00`,
good here and unreadable upstream) and a primitive `[1]` are cases in `answers-ocsp` and replay.
Two parts are still taken whole rather than walked: a certificate inside a `BasicOCSPResponse` and
an `IssuingDistributionPoint`'s `distributionPoint`. A certificate pkijs refuses is refused here by
`Certificate::read`, with the same `BasicOCSPResponse` schema text, so no verdict differs; a
`distributionPoint` is never read by either side. No residual text difference is known.

## Evidence

Base `618824a53`; head is the docs commit. Logs in `/workspaces/logs/CU-C4b/` and
`tmp/cu1118/logs/CU-C4b/` of the host viewer checkout. Scripts in
[`../scripts/cu-c4b/`](../scripts/cu-c4b/): `prep.sh`, `after.sh` (plugin vectors twice,
`coverage`, the suite, `replay:check`), `suite.sh`. Final cargo and `replay:check` are in
`after/` (fix round 1); the first round's logs are in `round0/`.
