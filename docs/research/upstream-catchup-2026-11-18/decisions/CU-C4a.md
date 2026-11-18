# CU-C4a decisions

Decisions made while porting the engine half of the CU-C3 work order (CU-C4 items 1 to 7 and 12)
on `research/catchup-2026-11-18-c4a`, cut from `9041d7510`. Each names the commit that carries it.

## CU-C4a-D1. Record the new engine cases at the public API, not at the worker boundary

Decision: `generate-signature-ltv-vectors.ts` (`adc2ce544`) records each step as one call of the
public API with a view of its answer (the fields the upstream tests assert), a refusal's code and
`details.field`, and the `signatures.validationInfoAdded` and `signatures.prepared` events. The
Rust runner `tests/signature_ltv.rs` builds the wire request each call stands for.
Reason: half the cases are refusals the TypeScript main thread gives before any worker request
exists (`LocalDocumentSignaturesService` gates), and the Rust `Host` merges those gates with the
worker. A signing also answers ids, digests and candidate bytes that no two engines share, which a
worker-level record (the `changes` generator's) would compare.
Cost: the runner carries a small table of call-to-request mappings that has to follow the API, and
a view leaves out every field the upstream tests don't assert.
Reverse: revert `adc2ce544` and `1239df825`.

## CU-C4a-D2. An outline item stands in for the attachment as the earlier catalog write

Decision: the case `no-undo-takes-it-back-and-the-undo-of-an-earlier-change-leaves-it` writes an
outline item where upstream's test creates an attachment.
Reason: both write the catalog, which is the point of the test. An attachment travels as a resource
payload inside `document.apply`, which the runner would need its own mapping for.
Cost: the case doesn't exercise the attachment path upstream's does.
Reverse: add an attachment call to the runner and the generator.

## CU-C4a-D3. `SessionAuthority::require` answers `ProtectedDocument`, and `permit` enforces nothing

Decision: `d05f2dae7` makes the session gate answer `ProtectedDocument` for a capability a signature
took away (`ScopeGuard.assertCapability`), and keeps the document's protection unenforced under
`signedDocumentPolicy: 'permit'` (`ScopeGuard.setProtection`), while the open answer still reports
it (`WorkerHost.probeProtection`).
Reason: policy 5 makes `doc.sign` protectable, and the new gates check it. Before this the gate said
`Forbidden` after a P=1 certification where upstream says `ProtectedDocument`, and a `permit`
session was refused a store upstream writes. Both showed in `validation-info.json`.
Cost: every gate that calls `require` on a protected capability now answers `ProtectedDocument`.
No existing test asserted otherwise (`cargo test -p epdf-engine`, 0 new failures).
Reverse: revert `d05f2dae7`.

## CU-C4a-D4. Three replayed steps are listed as a known divergence, not fixed

Decision: `signature_ltv.rs` lists three steps where this port lands a signature over unsaved edits
one revision later than upstream: `validation-info.json` case
`a-signature-made-after-it-seals-it-in-added-after-one-it-is-permitted` steps 3 (`revisionIndex`)
and 8 (`revisions`), and `timestamp-field.json` case
`a-timestamp-field-takes-a-name-no-other-field-has` step 2 (`revisionIndex`). Each must differ at
that pointer and nowhere else, so the replay fails when the port starts agreeing.
Reason: `SignatureMutator.openCandidate` seals unsaved edits in the signature's own revision (the
layer artifact a save would write). The Rust `prepare` takes `snapshot_bytes`, an incremental save,
so the edits get a revision of their own. The gap predates this range; these are the first vectors
that sign over unsaved edits. Porting the layer candidate path is outside CU-C4a's items.
Cost: until it is ported, a fill-and-sign through the Rust engine writes two revisions where
upstream writes one. The verdicts agree in every case recorded.
Reverse: port the layer path of `openCandidate` and delete the three entries.

## CU-C4a-D5. `fieldObjectNumber` rides the prepare answer

Decision: `0739e11c8` adds `fieldObjectNumber` beside `result` in the `signatures.prepare` answer.
Reason: upstream's worker payload carries it since `ba5f54d5f`, and the `Host` answers what the
worker answers.
Cost: none for a caller that reads `result`.
Reverse: drop the key in `host.rs`.
