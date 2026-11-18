# CU-final decisions

The fix wave after the final whole-branch review, on `research/catchup-2026-11-18-final` cut from
`a6ef741b8`.

## CU-final-D1. An over-limit reply is `TooLarge` only for a 2xx

Decision: `transport::over_limit` (`crates/plugins/signature-core/src/transport.rs`) turns a
`Replied` body longer than `max_reply_bytes` into `TooLarge` when the status is 2xx, and into
`Replied { status, body: [] }` for any other status. Both `attempt` and `RecordedTransport::post`
use it. Pinned by `a_refusal_over_the_limit_is_its_status_with_no_body`
(`crates/plugins/signature-core/tests/transport.rs`): a 404 with a long body is refused at once as
`SignatureRefused` with one request sent, and a long 200 is still a retried no-answer.
Reason: the guard ignored the status, so a custom transport answering `replied(404, big body)` got
a retried `Network` error, where upstream and all four host transports refuse at once. The hosts
already read a long non-2xx as its status with no body (`HttpSignatureTransport.kt`,
`URLSessionSignatureTransport`, `packages/core-wasm/src/signature-transport.ts`).
Cost: a long refusal's own message is dropped, so the error says `HTTP 404` and not the route's
words. A host that kept the body under the limit still passes them through.
Reverse: restore the status-blind match in `attempt` and `RecordedTransport::post`.

## CU-final-D2. Native UUIDs and nonces draw from the OS

Decision: with no host source installed, `epdf-kernel`'s `identity::random_bytes` calls
`getrandom::fill` on every target but `wasm32`. `RandomState` stays as the fallback: the browser
build's until `epdf-web` installs its Web Crypto source, and a native build's only if `getrandom`
fails. `getrandom` 0.3 is a `cfg(not(target_arch = "wasm32"))` dependency of `epdf-kernel`, the
`0.3.4` `epdf-engine` already locks, so `Cargo.lock` gains one dependency edge and no crate. Pinned
by `identity::tests::a_native_draw_with_no_source_comes_from_the_os`, which also checks that the
fallback counter does not move.
Reason: `RandomState` keys are random per process but not a cryptographic source, and the RFC 3161
nonce is drawn from here (CU-C8a-D3).
Cost: `epdf-kernel` links into the Apple framework, which this job did not rebuild. CU-C10's
release build picks the change up; until then the local framework from CU-C9 carries the old
source.
Reverse: drop the target dependency and call `fallback_bytes` from `own_bytes` on every target.

## CU-final-D3. A Kotlin reply with no readable status is `failed`

Decision: `HttpSignatureTransport.exchange()` answers `failed("the reply is not HTTP")` when
`getResponseCode()` is negative, before recording the status. Pinned in
`platforms/android/tests/SignatureTest.kt` by a loopback socket that answers `HTTP/1.1 OK` with no
status code.
Reason: the code was sent as `replied(65535)` (`-1` as a `UShort`), a status no server sent.
Cost: none found. The Swift and browser transports never see a status they cannot read.
Reverse: remove the `code < 0` line.

## CU-final-D4. An empty host trust slot is no `trust` setting

Decision: `TrustPort` gains `is_set()`, true by default; `SlotTrust` answers whether its slot holds
a trust. `SignatureController::settings()`, the read every area uses, treats a port that is not set
as `trust: null`. Pinned by `an_empty_trust_slot_is_no_trust_setting`
(`crates/epdf-viewer-host/tests/viewer_host.rs`) and the updated
`the_new_settings_cross_and_gate_add_validation_info` (`crates/plugins/signature/tests/ffi.rs`).
Reason: the test failed before the change. `host_plugin` always installs `SlotTrust`, so with a
`revocation` route and no anchors `canAddValidationInfo` answered true and `addValidationInfo` went
on to "has no timestamp you trust". Upstream with `trust` unset answers false and refuses
`invalid-input`, "adding validation data needs the `revocation` and `trust` settings"
(`packages/plugin/signature/src/services/authority.ts`, `write/validation-info.ts`), as the Swift
doc on `addValidationInfo` promises.
Cost: the same read feeds validation and sealing. Validation cannot tell (no anchors and no trust
both give `unknown`), but sealing with a timestamp authority and an empty slot now accepts the
authority's token unjudged, as upstream does with no `trust`, where it refused it before. A trust a
host set that returns no anchors is still a setting.
Reverse: drop `is_set` and the filter in `settings()`.

## CU-final-D5. The review's record fixes

Decision: CU-C8a-D9's cost names the default moving from now to the earliest trusted timestamp;
CU-C4b-D3 says the host transport enforces `timeout_ms` and the client has no timer; D-1's index
line points at CU-C8a-D6 for .NET; `reports/CU-C9.md` names the export check below and no longer
names a local worktree path. The check: `llvm-nm -gUj` on the four `af480d0ce` Apple slice archives
finds `EPDFDoc_GetLoadedObjectValue`, `EPDFPage_AddViewportRaw`, `EPDFPage_GetViewportRaw` and
`EPDFPage_RemoveViewportRaw` in each, and each `libembedpdf.a` matches CU-C9's sha256.
`scripts/cu-c7/index-check.sh` also reads `CU-final-Dn` headings.
Reason: the final review's M4, I1 and I2, and the controller's ask.
Cost: none.
Reverse: revert the commit.
