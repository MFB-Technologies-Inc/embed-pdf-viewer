# CU-C6 decisions

## CU-C6-D1. The waiting pen drawing is judged by `Rc` identity, as upstream judges it by reference

Decision: `drops_waiting_ink` (`epdf-annotation-core`, `update/draw.rs`) compares the first stroke
before and after a message with `Rc::ptr_eq`, and the store's re-run check compares the model's
draft with `Rc::ptr_eq`, where upstream (`6b424edce`) uses `===`.
Reason: `CreateInk.strokes` is `Vec<Rc<Vec<Point>>>` and `Model.draft` is `Option<Rc<Draft>>`, kept
so that a transition that keeps a member keeps its identity (`types.rs`). A stroke that joins the
drawing shares the earlier strokes' `Rc`s, as the TypeScript shares the arrays.
Cost: a transition that rebuilt an unchanged stroke would read as dropping the drawing. None does:
`core/annotation-update.json` `dropsWaitingInk` (3 cases, 12 messages) replays identical.
Reverse: not needed while the core keeps its `Rc` sharing.

## CU-C6-D2. The new signature ports are table handles compared by identity

Decision: the `timestampAuthority` and `revocation` settings are stored as handles
(`{"$signatureAuthority": n}`, `{"$signatureRevocation": n}`) in the registration's `PortTable`,
beside the key and trust handles, and declared `whole` with them. An authority is the same setting
when its `sign` is the same `Rc` and its `hash` is equal; a revocation source when it is the same
`Rc`.
Reason: the kernel's settings store holds JSON, and the existing ports already work this way
(`services/settings.rs`).
Cost: upstream's store compares settings with `sameValue`, a deep compare, so a plain
`{ kind: 'cms', sign }` object rebuilt over the same `sign` is no change there and is one here
only when its `Rc` is reused. This is the divergence `SigningKey::same` already documents for keys,
now for two more ports. No vector observes it.
Reverse: compare ports structurally where a port type allows it.

## CU-C6-D3. `SignFieldResult` stays the engine's JSON with two members added

Decision: `sign`, `complete_signature` and `place_mark`'s `signed` result resolve the engine's
`SignatureCompleteResult` JSON with `validationInfo` (`{ added, documentTimestamp }` or `null`) and
`validationInfoError` (the plugin error in its canonical `{"$error": ...}` form, or `null`), as the
other signature results are JSON (`contract.rs` header).
Reason: the plugin's results were JSON before, and the vectors compare that JSON.
Cost: the uniffi `SignatureCompleteResult` record does not carry the two members yet, and the
three new settings and the per-call authority have no uniffi field; those are CU-C8 and CU-C9's
wrapper items.
Reverse: a typed `SignFieldResult` struct beside the JSON.

## CU-C6-D4. Two FFI tests follow upstream's new behaviour

Decision: `crates/plugins/signature/tests/ffi.rs` `mode_defaults_to_visual_without_a_key` now
expects `can_fill` false with `doc.forms.fill` and no `doc.sign` (upstream `ceeb36bae`), and
`a_two_phase_signing_parks_then_completes` became
`a_two_phase_signing_parks_and_a_refused_cms_leaves_it_parked`: its CMS `30 00` is refused
`operation-failed` and the signing stays prepared (upstream `b08c0075d`: `completeSignature` runs
the completion gate).
Reason: both tests pinned the old behaviour.
Cost: no FFI test completes a two-phase signing now; that needs a CMS over the prepared digest,
which the FFI test's fake engine does not produce. The conformance case `two-phase-signing` still
completes one through the gate.
Reverse: give the FFI fake engine a fixture CMS that matches its digest.

## CU-C6-D5. The pen-drawing wait registers its settle flush when the controller is made

Decision: `Core::wire_drafts` registers the settle flush in `create_annotation_controller`, as
upstream's `createDrafts` does, and ignores the kernel's refusal for a plugin with no document, as
the draw handler's registration did before.
Reason: upstream moved the wait from the draw handler (registered at connect) to `write/drafts.ts`
(registered at create).
Cost: none observed.
Reverse: not needed.

## CU-C6-D6. API surface left for the wrappers

Decision: `ImportLibraryOptions.id` is on the uniffi `StampImportOptions` record (last member,
default `None`, so existing Swift and Kotlin calls compile unchanged). Everything else this job
added is Rust only, for CU-C8 and CU-C9: the settings `timestamp_authority`, `revocation` and
`document_timestamp`; the per-call `timestamp_authority` of `sign` and `prepare_signature`; the
`validationInfo` and `validationInfoError` result members; `add_validation_info` and
`can_add_validation_info`; `has_creation_awaiting_click` (new on upstream's public
`AnnotationHostCapability`, not in `annotation/src/ffi/drafts.rs`); and the uniffi
`SignatureVerdict` (`signature/src/ffi/records.rs`), which lacks the verdict's `revocation`,
timestamp and `longTerm` members.
Reason: the brief gives the wrappers to CU-C8 and CU-C9; a defaulted record member costs them
nothing.
Cost: the Swift wrapper does not pass `id` until CU-C9 adds it.
Reverse: remove the member.

## CU-C6-D7. A finish of the waiting drawing that throws is the caller's (fix round 1)

Decision: `finish_waiting_ink` and `finish_ink_draft` return the error of the `finishInkDraft`
commit. The store's commit, the tool-change listener, the settle flush, the harness and the uniffi
`finish_ink_draft` pass it on; the timer that ends the wait drops it.
Reason: upstream's `finishWaitingInk` lets `store.commit` throw, so the commit that called it
never runs the replacing message. The first port swallowed the error and ran the message, dropping
the drawing.
Cost: no test pins it. The commit throws only when the change queue is closed or refuses the
change, and no harness path reaches that while a drawing waits. The timer that ends the wait is
a deliberate Rust-only difference (recorded in CU-C7): upstream's timer lets the `finishInkDraft`
error escape as an uncaught exception of the timer callback, Rust drops it, the same convention as
`write/text_editing.rs` for a failed `send_typing` from its timer.
Reverse: not needed.
