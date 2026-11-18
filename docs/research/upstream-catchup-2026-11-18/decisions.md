# Decisions: upstream catch-up 2026-11-18

Every decision that resolved an ambiguity in this catch-up. Manager decisions are `D-n` (the plan's
"Decisions", accepted by the user on 2026-11-18); a job's decisions are `CU-Cn-Dn`, with the full
text (reason, cost, how to reverse) in [`decisions/`](decisions/). Each line here states the
decision and, where it has one, its cost, and links the job's record for the rest.

Targets: upstream viewer `c4e744345a4d81e655f75ccae91e5b513592f769`, upstream runtime
`f2ea7597287f26bdcb450602de6616ff6939d506`. Starting points: impl `a7c75f5ad`, plugins
`cf7fdbe7a`, runtime fork `17d8073d4`.

## NEW DIVERGENCE (none observed by a vector); recorded Rust-only differences

No NEW DIVERGENCE: no vector observes a difference. Two Rust-only differences are recorded,
neither visible to a caller that gets an answer:

- CU-C6-D7. When the timer that ends the pen drawing's wait finishes the drawing and the
  `finishInkDraft` commit throws, upstream's error escapes as an uncaught exception of the timer
  callback; Rust drops it (`crates/plugins/annotation/src/write/drafts.rs`
  `wait_for_next_stroke`), as `write/text_editing.rs` drops a failed `send_typing` from its timer.
  A timer callback has no caller to throw to. Recorded by CU-C7 at review's request.
- CU-C6-D2. The new `timestampAuthority` and `revocation` settings compare by handle identity where
  upstream's settings store compares deeply. This extends the divergence `SigningKey::same` already
  documents for keys to two more ports; no vector observes it.

The divergences accepted in earlier catch-ups stand as before
(`docs/research/upstream-catchup-2026-11-12/decisions.md`, `docs/research/rust-plugins-branch-review.md`).

## Proposed exceptions (open, for the user)

- CU-C4b-D3. The D-1 transport carries upstream's route limits: a request over 64 KiB is refused
  before it is sent, a timestamp reply over 1 MiB or a revocation answer over `max_reply_bytes`
  (2 MiB by default, configurable as the route's `maxBytes` is) is no answer, and an attempt times
  out after 10 s. Upstream's browser clients (`httpTimestampAuthority`, `httpRevocationSource`)
  set no limit of their own and leave it to the routes. Cost: a reply over the limit, which the
  browser client would read, is no answer here. Reverse: drop the check and pass `u32::MAX`.

## Scope, authority and branches

- D-1. Timestamps and revocation answers come from the network through one host transport trait;
  every signature-core step is ported, and Swift, Kotlin, .NET and the browser each implement the
  transport (CU-C8). Cost: four HTTP implementations.
- D-2. `core/signature/src/server/*` routes stay unported; the browser build's transport speaks
  their wire format, pinned by conformance cases. Cost: a Rust-core browser build needs the TS
  routes on its server.
- D-3. Releases and pushes wait for CU-C10, on the user's go-ahead after the approved
  fast-forward. Cost: until then `runtime-src` names `af480d0ce`, which exists only in the local
  runtime fork.
- CU-C2-D1. impl's catch-up branch starts at `a7c75f5ad` without the CU-C1 record branch.
- CU-C3-D1. plugins' catch-up starts at `cf7fdbe7a`, fast-forwards to CU-C1's record commit
  `af0c51003`, then merges the impl catch-up (`44dcf70da`).
- CU-C3-D10. The plugin side of #1087 and #1088 goes to CU-C6 and `canUse` to CU-C5.
- CU-C3-D11. CU-C4 also gets the native host's engine-call table (`translate.rs`).
- CU-C7-D1. The port jobs ran on one chain, so `research/catchup-2026-11-18-plugins` is
  fast-forwarded `9041d7510..0e1586fc0` instead of merging three branches.

## Environment and tooling

- CU-C1-D2. The container's viewer clone comes from a bundle of the host repository.
- CU-C1-D3. linux-arm64 PDFium is upstream's release build of `f2ea75972`.
- CU-C1-D4. `wasm-bindgen` 0.2.127 comes from the image.
- CU-C1-D5. The wasm32-eh PDFium is a fork build in its own container (`scripts/build-wasm32-eh.sh`).
- CU-C1-D8. A probe crate outside the workspace proves the libraries load.
- CU-C2-D4. impl is prepared with a copy of `prep.sh` that doesn't name the rust-viewer package.
- CU-C2-D5. The impl merge was built in a second worktree while the baseline ran.
- CU-C2-D6. PDFium libraries refreshed and cargo rebuilt from clean after the merge.
- CU-C3-D8. The wasm32 payload is rebuilt after the runtime moved; `fetch-payload.sh` keeps a stale
  local build otherwise.
- CU-C7-D3. The start checkout builds in its own target dir and the head's wasm rows touch its
  sources first: in a shared target dir Cargo takes another checkout's newer build of a workspace
  crate as fresh. Cost: a second target dir while the start ran, deleted.
- CU-C7-D8. The container's global `safe.directory '*'` (set during CU-C3) is recorded and left;
  no repository in the container needs it now.

## Runtime fork

- CU-C1-D1. The fork's Android secure-random branch is kept in the merge.
- CU-C1-D6. No fork ledger entry or sync note for this merge.
- CU-C1-D7. The two untagged Android blocks `check-fork-tags.py` reports are left.
- CU-C2-D2. impl's `runtime-src` conflict is resolved by setting the gitlink directly to
  `af480d0ce` (`git update-index --cacheinfo`), without initialising or fetching the submodule.
  Cost: no checkout of the runtime source proves the pin builds from the worktree; CU-C1's
  `wasm32-eh` build of that commit is the only evidence it does.

## Lockfile and dependencies

- CU-C2-D3. impl takes upstream's `pnpm-lock.yaml` and lets pnpm re-resolve, which moved Playwright
  to 1.65.1.
- CU-C3-D2. plugins takes impl's lockfile, re-resolved for plugins' importers.
- CU-C7-D4. `@playwright/test` is pinned to `1.63.0` in `tooling/viewer-e2e` and
  `tooling/wasm-eh-check`. Cost: `next`'s optional peer keeps 1.65.1 in the lockfile; `pnpm dedupe`
  would drop it but rewrites 730 lockfile lines.

## Vectors, generators and evidence

- CU-C2-D7. impl commits the regenerated vectors, whose only change is `policyVersion` 4 to 5.
- CU-C2-D8. `wire:check` and `operations:check` stay failing on impl, as in 2026-11-12.
- CU-C2-D9. The baseline viewer failures are recorded without a rerun.
- CU-C3-D3. `pages/mutate.json`'s conflict takes plugins' copy, then regenerates.
- CU-C3-D4. The generators follow upstream, including one case's answers and covers.
- CU-C3-D5. Evidence archives re-baselined by re-pin, with one witness changed.
- CU-C3-D6. Coverage gaps are listed, not filled, in CU-C3.
- CU-C3-D7. Trust reasons that are pkijs 4 `TypeError` text go to CU-C4 as generated.
- CU-C3-D9. `replay:check` is in the check set.
- CU-C4a-D1. The new engine cases are recorded at the public API.
- CU-C4a-D2. An outline item stands in for upstream's attachment as the earlier catalog write.
- CU-C4b-D1. The four pkijs `TypeError` trust reasons are reproduced, not excepted.
- CU-C4b-D4. Upstream's test certificate authority is used at generation time and not ported.
- CU-C4b-D5. Under the scripted WebCrypto every serial number is `5a5a5a5a5a5a5a5a`.
- CU-C5-D5. The i18n tables also gain the snapshot and marquee-zoom strings.
- CU-C7-D6. `validation-info.ts` keeps three unreached paths (line 90's early return, line 95's
  success return, the `added > 0` side of `uncovered`); no case is added. Cost: the document
  timestamp's success path has no vector.

## Engine

- CU-C4a-D3. `SessionAuthority::require` answers `ProtectedDocument` for what a signature took
  away, and `signedDocumentPolicy: 'permit'` enforces nothing while the open still reports the
  protection. CU-C7 adds the two unit tests for it.
- CU-C4a-D4. `openCandidate`'s layer path is ported: unsaved edits share the signature's revision.
- CU-C4a-D5. `fieldObjectNumber` rides the `signatures.prepare` answer.

## Signature core

- CU-C4b-D2. The D-1 transport is `transport::HttpTransport::post(TransportRequest) ->
  Promise<TransportOutcome>` over the crate's own `Promise`.
- CU-C4b-D3. The transport carries the routes' limits (proposed exception, above).

## Plugins

- CU-C5-D1. Command types stay in `epdf-plugin-commands`; owners reach them through a `commands`
  feature.
- CU-C5-D2. `createStandardCommands` (deprecated upstream) is not ported.
- CU-C5-D3. `createTestCommandContext` is not mirrored; its 17 lines stay uncovered.
- CU-C5-D4. The host's part of each command list is a trait per owner; `snapshot:save` asks the
  host for `image_bytes` where upstream calls `blob()`. CU-C7 adds the Rust test of a refusing
  `image_bytes` (CU-C7-D5).
- CU-C6-D1. The waiting pen drawing is judged by `Rc` identity, as upstream judges it by reference.
- CU-C6-D2. The new signature ports are table handles compared by identity (above).
- CU-C6-D3. `SignFieldResult` stays the engine's JSON with `validationInfo` and
  `validationInfoError` added.
- CU-C6-D4. Two FFI tests follow upstream's new behaviour.
- CU-C6-D5. The pen-drawing wait registers its settle flush when the controller is made.
- CU-C6-D6. The Rust API surface left for the wrappers (CU-C8, CU-C9) is listed in
  `reports/CU-C6.md`.
- CU-C6-D7. A throwing finish of the waiting drawing goes to the caller; the timer drops it (above).
- CU-C7-D7. `signatures.addValidationInfo` moves to the end of `TRANSACTED_KINDS`, after the other
  writes, where the host's other kind lists put the signature kinds.

## Integration (CU-C7)

- CU-C7-D2. The start column measures `cf7fdbe7a` on a fresh checkout for the rows earlier jobs
  didn't run (payload, `core-wasm`, `test:gc`, `epdf-web`, the addon), with that commit's own
  PDFium pins, and cites CU-C3's same-day `cf7fdbe7a` run for the rest.
- CU-C7-D9. `vectors/rust-coverage.json`, stale at head, is regenerated in its own target dir and
  `rust-coverage:check` passes. Cost: the record's diff covers the whole port at once.
- CU-C7-D10. The five CU-C1 logs missing from the container are copied in, so CU-C1's sentence
  that its logs are in both places is true.
- CU-C7-D11. The eight evidence failures new since `cf7fdbe7a` are closed: the pen drawing case's
  stale answer opIds are fixed in its fixture, and the evidence tests count the catch-up's cases
  from a generated archive (`historical/catchup-2026-11-18/additions.json`). Cost: one more archive
  file the tests read.

Left for the final whole-branch review, not changed here: CU-C4a `open_candidate` with unchanged
bytes and edits relying on two fork checks agreeing; CU-C4b's client trusting the host's reply
limit; CU-C4b's `route-max-reply-bytes` vector, which only proves the option parses; CU-C4b's
content-type header override in `http_source.rs`.
