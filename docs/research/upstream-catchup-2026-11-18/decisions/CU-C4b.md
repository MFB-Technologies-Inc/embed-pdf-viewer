# CU-C4b decisions

## CU-C4b-D1. The four pkijs `TypeError` trust reasons are reproduced, not excepted

Decision: the four `core/signature-trust.json` cases whose reason is a JavaScript `TypeError` text
(CU-C3-D7) replay as they are. No outcome exception is proposed.
Reason: the texts come from `validatePath` (`30b269418`), not from pkijs 4 (`a841027e9`). The chain
engine decides the same in pkijs 3.4.0 and 4.0.1 (a whitespace-normalised diff of
`CertificateChainValidationEngine` shows formatting and inlined constants only), and the Rust engine
in `trust.rs` and `policy.rs`, untouched by this job, gives `(reading '1')`, `(reading '0')` twice
and `(reading 'certificatePolicies')` as soon as the signer is handed to it last. The README
precedent (`crates/plugins/signature-core/README.md`, "pkijs behaviour the vectors pin") covers it.
Cost: the port keeps copying a JavaScript runtime's error text for four malformed policy chains.
`trust-policy-requireOnlyMapping`, a valid chain, is untrusted upstream now (it was trusted with a
path of 3), and so it is here.
Reverse: not applicable while upstream returns these texts.

## CU-C4b-D2. The D-1 transport is one host trait over the crate's own `Promise`

Decision: `transport::HttpTransport::post(TransportRequest) -> Promise<TransportOutcome>`, with
plain strings, bytes and integers in both types. Retries, the 250 ms delay, what a status means and
both clients' error texts stay in Rust.
Reason: the brief's shape (bytes and URL in, status and bytes out, async, limits). The crate's other
host ports (`TrustPort`, `WebCrypto`, `PersonalKeyStore`) are already traits returning
`epdf_kernel::rt::Promise`, and each wrapper adapts those today, so a wrapper adapts this one the
same way: a uniffi async callback interface, a C callback with a completion handle, a JS function
returning a `Promise`.
Cost: a wrapper writes one adapter per platform. Timeouts and reply limits are enforced by the host,
which must report `TimedOut` and `TooLarge` itself.
Reverse: if CU-C8 or CU-C9 finds a binding that cannot carry the `Promise`, a blocking or
callback-style variant can sit beside it without touching the clients.

## CU-C4b-D3. The transport carries the routes' limits

Decision: a request over 64 KiB is refused before it is sent; a timestamp reply may be 1 MiB and a
revocation answer 2 MiB (`MAX_TIMESTAMP_REPLY_BYTES`, `MAX_REVOCATION_REPLY_BYTES`), passed to the
host as `max_reply_bytes`; 10 s per attempt by default.
Reason: the brief. On a native platform the transport stands where upstream's routes stand
(`server/timestamp-route.ts` 64 KiB and 1 MiB, `server/revocation-route.ts` 64 KiB and 2 MiB
default `maxBytes`).
Cost: upstream's browser clients (`httpTimestampAuthority`, `httpRevocationSource`) set no size
limit of their own. Neither client can build a request near 64 KiB, so the refusal is never reached
by a real call; a reply over the limit is no answer here where the browser client would read it.
Listed under proposed exceptions in the report.
Reverse: drop the check and pass `u32::MAX`.

## CU-C4b-D4. The test certificate authority is used at generation time and not ported

Decision: `signers/test-authority.ts` stays unported. `core/signature-revocation.json` builds its
fixtures with `createTestCertificateAuthority` under the scripted WebCrypto with its clock fixed
(`now`, upstream `8a143eecf`), and the cases replay its answers.
Reason: it is a test and demo utility; nothing in the package calls it. Its lines count as covered
when the generator runs it.
Cost: a Rust host has no `create_test_certificate_authority`. CU-C8's platform tests replay recorded
replies through `RecordedTransport` instead.
Reverse: port it beside `signers.rs` when a host needs one.

## CU-C4b-D5. Under the scripted WebCrypto every serial number is the same

Decision: the revocation cases keep `randomPositiveInteger`'s scripted output (eight `0x5a`
octets), so every certificate the test authority issues has serial `5a5a5a5a5a5a5a5a`, and the
case descriptions say what that does: the CRL that withdraws one signer lists the other, and the
authority's OCSP answers speak for each certificate.
Reason: changing the scripted WebCrypto would change every other signer vector.
Cost: no case shows an OCSP answer about another certificate of the same issuer being set aside.
Reverse: a counter in the scripted `getRandomValues`, and a full regeneration.
