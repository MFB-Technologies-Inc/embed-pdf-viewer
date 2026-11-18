# CU-C8a decisions

## CU-C8a-D1. The uniffi transport answers through a completion with one method per outcome

Decision: `SignatureTransport::post(request, completion)` is a foreign trait (`Send + Sync`), and
`SignatureTransportCompletion` has `replied(status, body)`, `timed_out()`, `too_large()` and
`failed(message)`. No flat outcome record crosses uniffi.
Reason: it is the pattern of `SignatureSigner::sign` and `SignatureSignCompletion`
(`crates/plugins/signature/src/ffi/ports.rs`), and a method per outcome is the flat form CU-C4b-D2
asks for without a kind-plus-optionals record. A dropped completion rejects, which the client counts
as `Failed`.
Cost: a Swift or Kotlin host calls one of four methods rather than returning a value.
Reverse: add a `SignatureTransportOutcome` enum and a `settle(outcome)` method beside them.

## CU-C8a-D2. The transport is a host slot, like the signer and the trust anchors

Decision: `ViewerHost::set_signature_transport` fills a slot that the settings' timestamp authority
and revocation route, and a call's own authority, read per request. Config and settings records
carry only data (`SignatureTimestampAuthority`, `SignatureRevocationRoute`). An empty slot answers
every request `Failed` with `no transport: set one on the viewer first`.
Reason: `ViewerConfig` and `SignatureConfig` derive `Debug`, `Default` and `PartialEq`, which a
foreign object field would break, and the signer and trust anchors already work this way.
Cost: one transport per viewer, not one per authority. A host that needs two stacks routes inside
its own `post` by `request.url`.
Reverse: an optional `Arc<dyn SignatureTransport>` member on each authority record.

## CU-C8a-D3. A native timestamp nonce comes from the process's hashing keys; tests may fix it

Decision: `NonceSource::Random` draws the 8 nonce octets from `RandomState`, as the kernel's UUID
source does (`crates/epdf-kernel/src/identity.rs`). `fixed_nonce_octet` (`fixedNonceOctet` on the
web) fills every octet with one value so a recorded reply can answer a known request.
Reason: upstream's client uses `globalThis.crypto`, which a native host does not have, and no other
part of `WebCrypto` is used by `httpTimestampAuthority`. No new dependency.
Cost: `RandomState` is not a CSPRNG. A nonce only has to differ between requests for the reply
check to work, and it does. `fixed_nonce_octet` is a test hook on a production record.
Reverse: draw from `getrandom` once a crate in the native build depends on it.

## CU-C8a-D4. The browser build takes the transport through the signature plugin's config

Decision: `epdf-web` builds plugins through the conformance adapters, so the signature adapter
(`crates/plugins/signature/src/harness.rs`) reads `timestampAuthority` and `revocation`, and a
call's `timestampAuthority`, three ways: `transport` (a `$fn` taking the flat request and
answering a promise of the flat outcome) beside `httpTimestampAuthority`'s or
`httpRevocationSource`'s options; upstream's port shape (`sign` or `fetch` a `$fn`); or scripted,
as the vectors write them. The flat JSON is `crate::transport::request_json` and
`outcome_from_json`.
Reason: a JavaScript function crosses only as `{"$fn": "js:<id>"}` into a harness callback, so the
adapter is where it can become an `HttpTransport`. Before this, a config's `timestampAuthority`
ignored a `sign` function and only replayed vector answers.
Cost: the adapter now does more than mirror the TS shim (`suites/signature-plugin.ts`). The
vectors use none of the new keys; `the_plugin_matches_the_typescript` replays unchanged.
Reverse: a dedicated `epdf-web` signature factory registered ahead of the adapter's.

## CU-C8a-D5. The standard commands are a `ViewerConfig` list over a platform slot

Decision: `ViewerConfig.standard_commands: Option<Vec<StandardCommandList>>` names the owner lists
the commands plugin registers, joined in upstream's framework `standardCommands` order
(`packages/framework/react/src/command-lists.ts`) whatever order they are named in. The host's
part is `StandardCommandsPlatform` (`save_file`, `copy_text`, `copy_image`), set with
`ViewerHost::set_commands_platform`. A snapshot reaches the platform as PNG bytes encoded in
`epdf-viewer-host`.
Reason: the interaction list holds a command family, which only the plugin's `commands` setting
takes, so the join is made when the plugin is built (CU-C5-D2). The platform slot matches the
signer slot.
Cost: changing the lists needs a new viewer. Without a platform, a copy fails `invalid-input` and a
save saves nothing.
Reverse: a runtime `register_command_family` on the commands plugin.

## CU-C8a-D6. `epdf-capi` gets no catch-up surface, the D-1 adapter included

Decision: no `epdf-capi` change. Every catch-up item is recorded as not applying to it.
Reason: `epdf-capi` exposes seven fixed engine operations (`OPEN_BYTES`, `CLOSE_DOCUMENT`,
`LIST_PAGES`, `READ_TEXT`, `READ_GEOMETRY`, `RENDER`, `SEARCH_LITERAL`, `crates/epdf-capi/README.md`)
and no plugin. It has no signature read, no signing and no save, so the validation-info kinds could
not be read alongside a signature or persisted, and nothing in it signs with a timestamp authority.
The brief makes the .NET D-1 transport conditional on that. A C transport would also need the
kernel's runtime, which `epdf-capi` does not run.
Cost: the brief's "D-1 adapters on all three" is not met for `epdf-capi`, and .NET cannot run a
timestamped signature or a long-term validation fill. CU-C8b's .NET rows for those are "not
applicable" until `epdf-capi` gains signature operations.
Reverse: add signature operations (`signatures.list`, `prepare`, `complete`, `validationInfo`,
`addValidationInfo`, save) to `epdf-capi`, then a C callback transport with a completion handle
and a kernel runtime to run the clients on.

## CU-C8a-D7. Engine kinds need no new export on uniffi or the node addon

Decision: `signatures.validationInfo`, `signatures.addValidationInfo` and `signatures.prepare`
without `field` reach Swift and Kotlin through `PdfEngine::call` and Node through `Engine::call`,
the one wire call each binding already has. `crates/epdf-uniffi/tests/validation_info.rs` pins the
first two.
Reason: both bindings route every kind through `epdf_engine::host::Host`.
Cost: a facade decodes the JSON itself, as for every other kind.
Reverse: not applicable.
