# CU-C4b: the signature-core half of signatures

`research/catchup-2026-11-18-c4b` ports CU-C3's work order items 8 to 11 (CU-C4, engine and
signatures) onto `618824a53`. Items 8, 10 and 11 are done. Item 9 is ported except
`createTestCertificateAuthority`, which the vectors use at generation time only (CU-C4b-D4), and
804 of the 2,148 uncovered `core/signature` lines still have no case. The 5 `epdf-signature-core`
tests and all 77 replayed cases pass; `cargo test --workspace` fails 4 tests, all on CU-C3's list.
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
| the docs commit | | This report, the decisions, the job's scripts |

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
   Not ported: `signers/test-authority.ts` (CU-C4b-D4). 28 new cases; the uncovered added lines
   in `core/signature` go from 2,148 to 804 (`scripts/cu-c3/gaps.py`, `gaps-after.txt`). What is
   left is mostly the paths that need a trusted timestamp: `attach.ts` (87), `splice.ts` (108),
   `judgement.ts` (103), `answer.ts` error branches, `fill.ts` (65), `gate.ts` (43), `long-term.ts`
   (41), `verify-timestamp.ts` (40), `trust.ts` (38). A trusted timestamp needs a timestamp
   authority certificate dated by a fixed clock, which `timestampAuthorityCertificate` does not
   take; that is the next step.
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

| Check | CU-C4a (`1239df825`) | Now (`d7e432567`) |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,487 passed, 14 failed, 2 ignored | 3,500 passed, 4 failed, 2 ignored |
| `epdf-signature-core` `conformance` | 0 of 5 | 7 of 7 (2 new files) |
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

None for the trust reasons (CU-C4b-D1). Two Rust-only behaviours to rule on:

- The transport's limits (CU-C4b-D3): a request over 64 KiB is refused and a reply over 1 MiB
  (timestamp) or 2 MiB (revocation) is no answer, where upstream's browser clients leave limits to
  the route.
- OCSP, CRL and TimeStampResp are read by structure with pkijs's schema texts, not by a rule-for-rule
  `compareSchema` walk; a malformed answer pkijs reads in part could be read differently. No vector
  reaches one (README, "Deliberate deviations").

## Evidence

Base `618824a53`; head is the docs commit. Logs in `/workspaces/logs/CU-C4b/` and
`tmp/cu1118/logs/CU-C4b/` of the host viewer checkout. Scripts in
[`../scripts/cu-c4b/`](../scripts/cu-c4b/): `prep.sh`, `after.sh` (plugin vectors twice,
`coverage`, the suite, `replay:check`), `suite.sh`. Final cargo and `replay:check` are in
`after3/` (after the two plugin-signature commits); the engine, plugin and TS checks in `after/`.
