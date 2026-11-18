# CU-C8a: the Rust binding surfaces for every platform

`research/catchup-2026-11-18-c8a`, cut from `research/catchup-2026-11-18-plugins` at `56430ccd4`,
makes the catch-up's new API reachable through the binding crates, and adds the D-1 transport
adapter on uniffi and `epdf-web`. `epdf-capi` gets nothing: it exposes no signature operation and
no plugin, so every item, D-1 included, is recorded as not applying (CU-C8a-D6). That is the one
place this job falls short of the brief's done list. `cargo test --workspace` passes 3,536 tests
with none failing. The one Rust core change is the client-side reply limit the brief asked for.
Decisions are in [`../decisions/CU-C8a.md`](../decisions/CU-C8a.md).

## Commits

| Commit | Summary |
|---|---|
| `7b8f20ecd` | `signature-core`: a `Replied` body over `max_reply_bytes` is `TooLarge` (`transport.rs` `attempt()`), with a test |
| `699902c8c` | uniffi signature surface, the D-1 foreign transport, `has_creation_awaiting_click`; `ViewerHost::set_signature_transport` |
| `d8f8948ab` | `epdf-viewer-host`: the standard command lists, `StandardCommandsPlatform` |
| `c318aad46` | `epdf-uniffi`: tests for the validation-info kinds through `PdfEngine::call` |
| `40cec1fb4` | `epdf-web`: the D-1 transport as a JavaScript function, through the signature config |
| the docs commit | `vectors/rust-coverage.json` regenerated, this report and the decisions |

## What each binding wraps

uniffi (Swift and Kotlin) has two layers: `epdf-uniffi`'s `PdfEngine::call`, one JSON wire call
for every engine kind, and `epdf-viewer-host`'s `ViewerHost` with a handle per plugin. `epdf-web`
builds the kernel and plugins through the conformance adapters, so a plugin member is reachable by
its upstream name and a JavaScript function crosses as `{"$fn": "js:<id>"}`; its engine is the
wasm payload behind `engine-bridge.ts`. `epdf-node` is `Engine::call` only. `epdf-capi` has seven
fixed read operations and no plugin.

## Mapping

uniffi names are on `SignatureHandle`, `AnnotationDraftsHandle`, `InteractionHandle`, `ViewerHost`
or the records named. Web names are the capability member or config key upstream uses. "n/a (D6)"
is CU-C8a-D6.

| Hand-off item | uniffi | capi | web |
|---|---|---|---|
| C4 2: `signatures.validationInfo` | `PdfEngine::call` (D7) | n/a (D6) | engine payload, reached by the plugin |
| C4 2: `signatures.addValidationInfo`, `ProtectedDocument` | `PdfEngine::call` (D7) | n/a (D6) | engine payload |
| C4 3: `signatures.prepare` without `field` | `PdfEngine::call` (D7) | n/a (D6) | engine payload |
| C4 4, 5: policy 5, `apiVersion` 3.3 | engine behaviour, no surface | n/a (D6) | engine behaviour |
| C4 12: `translate.rs` rows | done in CU-C4a, used by the plugin | n/a | n/a: native host only |
| D-1 transport | `SignatureTransport`, `SignatureTransportCompletion`, `set_signature_transport` | n/a (D6) | config `transport: (request) => Promise<outcome>` |
| D-1 clients' options | `SignatureTimestampAuthority`, `SignatureRevocationRoute` | n/a (D6) | `httpTimestampAuthority` / `httpRevocationSource` options beside `transport` |
| C5 1: `canUseTool` | `InteractionHandle::can_use_tool` (CU-C5) | n/a (D6) | `canUseTool` |
| C5 1: `ToolPermission` | `register_tool(tool, replace, permission)` (CU-C5) | n/a (D6) | a tool's `canUse` function |
| C5 2: owner command lists, standard join | `ViewerConfig.standard_commands: [StandardCommandList]`, `set_commands_platform(StandardCommandsPlatform)` | n/a (D6) | n/a: upstream's TS lists cross as JS command functions |
| C5 3: `history:undo`, `history:redo` | `StandardCommandList::History` | n/a (D6) | as the row above |
| C6 1: `hasCreationAwaitingClick` | `AnnotationDraftsHandle::has_creation_awaiting_click` | n/a (D6) | `hasCreationAwaitingClick` |
| C6 2: `ImportLibraryOptions.id` | `StampImportOptions.id` (CU-C6) | n/a (D6) | `importLibrary` option `id` |
| C6 3: setting `timestampAuthority` | `SignatureConfig.timestamp_authority`, patch `timestamp_authority` / `clear_timestamp_authority`, `SignatureSettings.has_timestamp_authority` | n/a (D6) | `timestampAuthority` (`transport`, `sign`, or scripted) |
| C6 3: setting `revocation` | `SignatureConfig.revocation`, patch `revocation` / `clear_revocation`, `has_revocation` | n/a (D6) | `revocation` (`transport`, `fetch`, or scripted) |
| C6 3: setting `documentTimestamp` | `SignatureConfig.document_timestamp`, patch and `SignatureSettings.document_timestamp` | n/a (D6) | `documentTimestamp` |
| C6 3: per-call authority | `SignatureSignInput.timestamp_authority`, `SignaturePrepareInput.timestamp_authority`: `SignatureCallAuthority::{Without, Http}` | n/a (D6) | call argument `timestampAuthority` (`null`, `transport`, or `sign`) |
| C6 3: `addValidationInfo` | `add_validation_info(field, cancel) -> SignatureValidationInfoAdded` | n/a (D6) | `addValidationInfo` |
| C6 3: `canAddValidationInfo` | `can_add_validation_info()` | n/a (D6) | `canAddValidationInfo` |
| C6 3: `validationInfo`, `validationInfoError` | `SignatureCompletion.validation_info`, `.validation_info_error: SignatureFailure` | n/a (D6) | result JSON members |
| C6 3: `earliest-timestamp` | `SignatureValidateRequest.validation_time: SignatureRequestedTime::EarliestTimestamp` (the type changed from `SignatureValidationTime`) | n/a (D6) | `validationTime: { kind: 'earliest-timestamp' }` |
| C6 3: verdict `revocation`, timestamp, `longTerm` | `SignatureVerdict.revocation`, `.revocation_reason`, `.timestamp: SignatureTimestampVerdict`, `.long_term: SignatureLongTermVerdict` | n/a (D6) | verdict JSON members |

`epdf-node`: the engine rows reach it through `Engine::call`; no plugin item applies.

## D-1 adapter signatures

uniffi (`crates/plugins/signature/src/ffi/transport.rs`):

```rust
#[uniffi::export(with_foreign)]
pub trait SignatureTransport: Send + Sync {
    fn post(&self, request: SignatureTransportRequest, completion: Arc<SignatureTransportCompletion>);
}
pub struct SignatureTransportRequest { url, content_type, headers: Vec<SignatureTransportHeader>,
    body: Vec<u8>, timeout_ms: u32, max_reply_bytes: u32 }
impl SignatureTransportCompletion { fn replied(status: u16, body: Vec<u8>); fn timed_out();
    fn too_large(); fn failed(message: String) }
ViewerHost::set_signature_transport(Option<Arc<dyn SignatureTransport>>)
```

Web (`crates/plugins/signature/src/transport.rs`, read by `harness.rs`): a config or call spec
`{ transport: fn, url, hash?, policy?, headers?, timeoutMs?, retries? }` for an
authority, `{ transport: fn, url, headers?, timeoutMs?, retries?, maxReplyBytes? }` for a route.
`fn(request) => Promise<outcome>` with `request = { url, contentType, headers: [{ name, value }],
body: {$bytes}, timeoutMs, maxReplyBytes }` and `outcome` one of `{ kind: 'replied', status, body }`,
`{ kind: 'timedOut' }`, `{ kind: 'tooLarge' }`, `{ kind: 'failed', message }`.

capi: none (CU-C8a-D6).

## Tests

Container `embed-pdf-catchup1118`, worktree `/workspaces/wt/CU-C8a`, `CARGO_TARGET_DIR=/workspaces/target`,
every crate source touched once first (CU-C7-D3). Start column is CU-C7's final run.

| Check | CU-C7 (`5f4abdabc`) | Now |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,514 passed, 0 failed, 2 ignored | 3,536 passed, 0 failed, 2 ignored; 3,537 after fix round 1 (`fix1/final/cargo.log`) |
| `epdf-web` wasm tests | 59 passed in 8 binaries | 63 passed in 9 binaries (fix round 1) |
| `epdf-node` addon (`addon.mjs`, release) | 6 checks | 6 checks |
| `epdf-plugin-signature --features uniffi` | | 48 passed (lib 7, behavior 15, conformance 2, ffi 22, ffi_settings 2) |
| `rust-coverage:check` | exit 0 | stale at first (the new `signature/src/transport.rs` and the moved `signature-core` lines); regenerated with the package's `rust-coverage`, then exit 0: 68,348 of 118,503 lines, 38,496 only other tests reach, 701 files (76 excluded). Stale again after fix round 1; regenerated (`a3cc5ece2`), exit 0: 68,348 of 118,497, 38,490, 701 files (76 excluded) |

The 22 new cargo tests: signature-core 1, the signature plugin's transport and record units 6 and
its FFI 7, annotation FFI 0 (an assertion added), viewer-host 6, `epdf-uniffi` 2. The signature
conformance replay is unchanged by the adapter's new keys. Generated binding files: none are checked
in for uniffi, and `crates/epdf-capi/include/epdf.h` is untouched.

## For CU-C8b and CU-C9

A positive timestamped signature against recorded replies is not testable from Rust here. The
reply must echo the request's nonce, which no binding can fix (CU-C8a-D8), and its message imprint,
the hash of a signature value that covers a signing time from the clock.
`build_timestamp_token` also writes no nonce. Every test here proves the request, the four
outcomes and the errors, not a granted token. A long-term fill has the same limit: the
`TEST-ONLY` signer has no trusted timestamp, so `add_validation_info` refuses before asking
(`a_seal_with_a_revocation_route_reports_its_validation_data`). A platform test of either needs a
fixture document and replies generated together, or a reply computed from the request.

`SignatureValidateRequest.validation_time` changed type from `SignatureValidationTime` to
`SignatureRequestedTime` (CU-C8a-D9). The Swift wrapper's alias
(`platforms/apple/EmbedPDF/Sources/EmbedPDF/Plugins/Signature.swift:29`) needs a
`SignatureRequestedTime` counterpart, and Kotlin callers that set the field change the same way.

Hosts must enforce `timeout_ms` themselves: the client has no backstop timer, so a `post` that never
answers leaves the signing waiting. `max_reply_bytes` has the client-side check of `7b8f20ecd`.

`ViewerConfig.standard_commands` is ignored when `commands` is false: no commands plugin is built
to take the lists.

In the browser, the request's `body` reaches the transport function as the core-wasm proxy converts
`{$bytes}`. A core-wasm test of a `Uint8Array` body round trip through the JavaScript transport is
CU-C8b's.

## Evidence

Logs in `/workspaces/logs/CU-C8a/` and `tmp/cu1118/logs/CU-C8a/` of the host checkout:
`final/cargo.log` (`cargo.summary`), `final/epdf-web.log`, `final/addon.log`,
`final/rust-coverage-check.log`, `signature-crate.log`.

## Cleanup

Host free space (`df -g /System/Volumes/Data`) read 417 GB at the start and 406 GB at the end;
`/workspaces` went from 35 GB to 37 GB. Deleted, 7.1 GB inside the volume: `/workspaces/target-cov`
(6.7 GB, the coverage build) and `/workspaces/target/release` (437 MB, the addon build). The CU-C8a
bundle is deleted once the host has fetched the branch. No trim was run.

Fix round 1: a mis-quoted command ran a stray `cargo test --workspace` without the job's
environment (debuginfo and incremental on), which grew `/workspaces/target/debug` to 111 GB. All
of `/workspaces/target/debug` (and `target/tmp`) was deleted, since it mixed two profiles; the
round's coverage dir was deleted too. `/workspaces` is 8.1 GB after it, host free space read 331 GB
before a trim. CU-C8b rebuilds `target/debug`.

Kept for CU-C8b and CU-C9: `/workspaces/wt/CU-C8a`, which is CU-C7's worktree moved and switched to
this branch at the same commit (`git worktree move`), with its `node_modules`, the PDFium libraries
and the Rust-core payload; `/workspaces/target/wasm32-unknown-unknown`.
