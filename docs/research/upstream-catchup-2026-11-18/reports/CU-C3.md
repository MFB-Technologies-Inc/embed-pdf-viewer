# CU-C3: plugins merge, vectors, and the work order

`research/catchup-2026-11-18-plugins` merges the impl catch-up (`474599496`, upstream `c4e744345`)
into plugins `cf7fdbe7a`, by way of CU-C1's record commit `af0c51003`. Two paths conflicted:
`pnpm-lock.yaml` and the generated `vectors/pages/mutate.json`. The engine vectors, the wire vector
and every plugin vector were regenerated twice each, byte-identical both times. The generators
needed four upstream follow-ups and no workaround. The evidence archives are back at their baseline
of 766 of 768. `cargo test --workspace` goes from 3,467 passed and 0 failed to 3,447 passed and
20 failed, all of it upstream behaviour the port doesn't have yet: 118 replayed cases in six vector
files and six `epdf-engine` contract tests. No Rust changed. The work order for CU-C4 to CU-C9 is
the last section. Decisions are in [`../decisions/CU-C3.md`](../decisions/CU-C3.md).

## Commits

| Commit | Summary |
|---|---|
| `af0c51003` | CU-C1's record, taken by fast-forward (CU-C3-D1) |
| `44dcf70da` | Merge `research/catchup-2026-11-18-impl` into plugins |
| `922c08d2a` | Engine vectors regenerated: `policyVersion` 5, two protected capabilities, `apiVersion` 3.3 |
| `3b37c715f` | Generators follow upstream (wire kinds 96, fake engine members, `getValidationInfo` answers, the ink case) |
| `98a477901` | Wire and plugin vectors regenerated, and `coverage.json` |
| `48568034c` | Evidence archives re-baselined (766 of 768, as at `cf7fdbe7a`) |
| `06acceca6` | Report, decisions, results, the job's scripts |
| review fix | Work-order item 12 for CU-C4, the native host's engine-call table (CU-C3-D11) |

## Merge

`git merge --no-ff --no-commit research/catchup-2026-11-18-impl` on `af0c51003`; the merge base is
`a7c75f5ad`, which is impl and an ancestor of plugins. `runtime-src` moved to `af480d0ce` without a
conflict, since plugins never changed it. Two conflicts, and no file outside them changed on both
sides (`CU-C3-both-sides.txt`):

| Path | Resolution |
|---|---|
| `pnpm-lock.yaml` | impl's, then `pnpm install --no-frozen-lockfile` for plugins' four importers (CU-C3-D2) |
| `tooling/engine-conformance/vectors/pages/mutate.json` | plugins' copy, regenerated in `922c08d2a` (CU-C3-D3) |

`cargo build --workspace --tests` passes on the merge with no Rust change.

## Engine vectors

`regen-engine.sh` twice on `44dcf70da`: identical logs and sha256 lists over 61 files
(`deterministic=yes`). Every group wrote except `wire`, which stopped on "expected 94 request kinds,
the union has 96" (fixed in `3b37c715f`, then regenerated twice, identical). Two more runs on the
rebuilt wasm32 payload (CU-C3-D8) changed nothing.

| Files | Change | Upstream |
|---|---|---|
| `changes/{annotation-copy,form-copy,rename,scale,widget-rename}`, `pages/mutate`, `signature/protection` | `policyVersion` 4 to 5 | `462380c7a` (#1088) |
| `signature/protection` | below a fill certification, `doc.metadata.modify` and `doc.sign` are protected too | `462380c7a` |
| `changes/kinds` | `apiVersion` 3.2 to 3.3, so the 3.3 kinds read as lacking from a 3.2 server | `4cb85c91e` (#1081) |
| `wire/requests` | `signatures.validationInfo` and `signatures.addValidationInfo` | `67ed292a6`, `656c2fc42` |

## Plugin generator and vectors

After the merge `tsc --noEmit` in `tooling/plugin-conformance` reported one error: the fake engine's
signatures service lacked `getValidationInfo` and `addValidationInfo`. With that fixed, the generator
stopped twice on "unanswered engine calls": every validating signature case now reads the security
store first, and `ink-cancel-inside-the-grouping-window` now creates the waiting ink annotation.
`3b37c715f` follows upstream for each (CU-C3-D4). `TZ=UTC pnpm run vectors` then ran twice with
identical sha256 lists. Ten files changed (`CU-C3-plugvec-casediff.log`):

| Vector file | Cases changed | Upstream |
|---|---|---|
| `core/signature-trust` | 46 of 96: the new `validatePath`, pkijs 4 | `30b269418`, `a841027e9` |
| `core/signature-document` | 33 of 35: `getValidationInfo` read, verdict `revocation` | `67ed292a6`, `3aa832da5` |
| `core/signature-verify`, `-parse`, `-signers` | 7, 1, 1: DER attribute order, DER serials | `15955a843`, `6d6e76d3c`, `a841027e9` |
| `plugins/signature` | 39 of 39: settings defaults, `SignFieldResult`, verdict `revocation` | `1c165508c`, `3b3d7fb18`, `7bd135d7f` |
| `plugins/annotation-tools`, `form-design` | 7, 1: hub tools carry `canUse`; a waiting pen drawing is kept | `d6325f5ba`, `d0601e65e` |
| `plugins/interaction` | 1: `canUseTool` | `d6325f5ba` |
| `coverage.json` | rewritten with `pnpm run coverage` | |

No vector gained a "is not a function" or "unanswered" string (`errgrep.mjs`). `core/signature-trust`
went from 2 to 4 cases whose reason is a `TypeError` from inside pkijs (CU-C3-D7).

## Evidence archives

`node --test` over `test/*.test.ts` and `test/*.test.mjs`. At `cf7fdbe7a`: 766 of 768, with 2
failures in `current-suites.test.ts` that predate this job (a case `grid-nan-columns` where the
archive expects `change-hooks`, and a `TypeError` reading index 6). After the regeneration: 758 of
768, 7 files failing. `48568034c` re-pins and changes one witness (CU-C3-D5). Final: 766 of 768,
the same 2 failures with the same messages.

## Tests

All in container `embed-pdf-catchup1118` with `CARGO_TARGET_DIR=/workspaces/target`.

| Check | Start `cf7fdbe7a` | Now `48568034c` |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,467 passed, 0 failed, 2 ignored | 3,447 passed, 20 failed, 2 ignored |
| engine `:check` scripts | 35 of 35 | 34 of 35: `operations:check` (2 kinds without rows) |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | all pass | all pass |
| plugin `replay:check` | pass: 119 files, 15,922 cases | fails: 118 cases in 6 files |
| plugin evidence tests | 766 of 768 | 766 of 768 (same 2) |
| TS `turbo run test` over `packages/**` | 98 of 98 | 98 of 98, on the rebuilt payload (CU-C3-D8) |

The cargo failures, by crate, cause and job, with every failing case, are in
[`../results/CU-C3-cargo-failures.md`](../results/CU-C3-cargo-failures.md).

## Coverage gaps

[`../results/CU-C3-coverage-gaps.md`](../results/CU-C3-coverage-gaps.md): 2,264 uncovered lines that
the range added, in 29 files, and 16 upstream-touched files no generator loads. `core/signature` has
2,148 of the lines; the rest are `core/main` (17), `plugin/signature` (88), `plugin/form` (8) and
`plugin/interaction` (3). Every plugin's new `commands.ts` is unloaded.

## Work order

Each item names its job. Wrapper items list what the Rust API has to expose once the core item
lands; CU-C8 covers `epdf-web` (browser), `epdf-uniffi` (Kotlin) and `epdf-capi` (.NET), and CU-C9
the Swift side (`epdf-uniffi` and `platforms/apple/EmbedPDF`). A "case" is a new engine vector case
or plugin-conformance case; "replay" is an existing case that fails today.

### CU-C4: engine and signatures

Failing now: the 6 `epdf-engine` tests and the 5 `epdf-signature-core` tests, 77 replayed cases
(`signature-trust` 46, `signature-document` 22, `signature-verify` 7, `-parse` 1, `-signers` 1).
`operations:check` needs rows for the 2 new kinds and the plan documents' kind count moves to 96.

1. `pdfium-sys` and `pdfium`: bindings for `EPDFDss_GetCount`, `EPDFDss_GetData`, `EPDFDss_Add`
   and `EPDFDoc_GetLoadedObjectValue` (`pdfium-sys` already generates them from the header; the safe
   wrappers are missing).
2. `epdf-engine`, new wire kinds `signatures.validationInfo` (read, `doc.forms.read`) and
   `signatures.addValidationInfo` (write, `doc.sign`, not undoable, refused with
   `ProtectedDocument` after a P=1 certification): routing, effects, envelope `opId`, page-space
   boundary, `operations.ts` rows, the `signatures.validationInfoAdded` event, and
   `validationInfoEntryProblem` with its 16 MiB entry limit
   (`ValidationInfoMutator.ts`, `validationStore.ts`). Fixes the 4 `wire_contract` tests.
3. `epdf-engine`, `signatures.prepare` without a `field` for `kind: 'timestamp'`: the engine makes
   a field on the candidate (`timestampField.ts`), answers `field`, and authorizes on `doc.sign`
   rather than the group (`ba5f54d5f`, #1102). The worker payload carries `fieldObjectNumber`.
4. `epdf-engine`, judging policy 5: `SIGNATURE_POLICY_VERSION` 5, `doc.metadata.modify` and
   `doc.sign` in `PROTECTABLE_CAPABILITIES`, `ruleNoChangesCertification`, a page's `/Annots` held
   in its own object (`annotsHolders`, `pageList`, `listChange`), `unwrittenObjectsToRead` read
   through `EPDFDoc_GetLoadedObjectValue`, and `restrictionsFor` judging a document timestamp after
   a P=1 certification on its own (`462380c7a`, `92db1287a`). Fixes `signature_protection`.
5. `epdf-engine`, `apiVersion` 3.3 and `VALIDATION_INFO_SINCE` (`4cb85c91e`). Fixes `change_ops`.
6. `epdf-engine`, `setSignatureAppearance` gated by `doc.sign` or `fields:sign`, not `doc.forms.fill`
   (`ceeb36bae`, #1102).
7. Engine cases: the 12 new tests of `runSignatureConformance.ts` and `runFieldGroupsConformance.ts`
   (validation data read, store, dedupe, malformed entry, undo, seal-in, permissions, P=2 and P=1
   certifications; timestamp with no field, its name, its permission; a group signer adding a
   timestamp), the `analysis.test.ts` additions for policy 5 (18 tests), and
   `packages/engine/main/test/signature-validation-info.test.ts`. No engine vector exercises any of
   this yet.
8. `epdf-signature-core`: `validatePath` and the trust rewrite (`30b269418`), pkijs 4 behaviour and
   messages (`a841027e9`, CU-C3-D7), DER attribute order (`15955a843`, #1090), DER serials
   (`6d6e76d3c`, #1094), `cms/time.ts`, the test authority's clock (`8a143eecf`, #1103). Fixes the
   77 replayed cases.
9. `epdf-signature-core`, the new modules: timestamp attach, splice and verify, the timestamp
   protocol, ECDSA raw signatures, `gate.ts` timestamps, `sign.ts` with a timestamp authority
   (`completeSignature`), `verdict.ts` with timestamp verdicts and judging at the earliest trusted
   timestamp, `collect.ts` (`addValidationInfo`), `judgement.ts`, `long-term.ts`, and
   `revocation/{addresses,answer,fill,pool,request,status}` (#1087, #1088). Cases for the 2,148
   uncovered lines.
10. D-1: the transport trait (request bytes and URL in, reply bytes out), used by the timestamp
    authority (`httpTimestampAuthority`'s client half) and the revocation source
    (`httpRevocationSource`'s client half), with a recorded-reply implementation for the harness.
    CU-C8 and CU-C9 need its exact signature from the CU-C4 report.
11. D-2: the client side of `RevocationRouteRequest` and of the timestamp request the route forwards,
    pinned by conformance cases; `server/*` stays unported.
12. The native host's half of `LocalDocumentSignaturesService.ts` (+89 lines in the range), which
    every native platform reaches through `epdf-viewer-host` and `epdf-engine`'s host (CU-C3-D11):
    - `crates/epdf-viewer-host/src/translate.rs`, the Rust counterpart of each `Local*Service.ts`
      `wirePack`: rows for `signatures.getValidationInfo` (kind `signatures.validationInfo`, an
      answer of three byte lists sent as payloads, a decode shape no row has yet) and
      `signatures.addValidationInfo` (kind `signatures.addValidationInfo`, an `opId`, each entry a
      request payload, a mutation answer). Its method list and its kind and decode map stop at
      `signatures.cancel` today. Tests in `crates/epdf-viewer-host/tests/translate.rs`.
    - `crates/epdf-engine/src/host.rs`: `getValidationInfo` gated by `doc.forms.read` and
      `addValidationInfo` by `doc.sign`; an entry list that isn't byte arrays is `InvalidArg`; a
      write that stored nothing is not finished as a mutation (`WorkerHost.ts`
      `handleSignaturesAddValidationInfo`) and publishes no event, one that stored something
      publishes `signatures.validationInfoAdded` with `added` and `meta`. The event joins the event
      enum in `crates/epdf-engine/src/session/save.rs`.
    - `signatures.prepare` with no `field`: the gate is `doc.sign` rather than
      `require_some_signing` (`gate('sign-some-field')`, `host.rs`), and the `signatures.prepared`
      event takes its `field` from the answer's `field` when the call named none (`save.rs`
      `SignaturesPrepared`), refusing with `Unknown` when neither has one.
    - `epdf-kernel` needs no change: its `changes_document` (`kernel.rs` near line 1702) excludes
      the same four events as upstream's `changesDocument` (`packages/core/main/src/kernel.ts`,
      unchanged in the range), so `signatures.validationInfoAdded` counts as a change to the
      document and marks it unsaved, as upstream does. A case that checks it belongs with item 7.

### CU-C5: commands, history, i18n (#1091, #1089)

Failing now: 7 replayed cases, all `canUse` or `canUseTool`: `plugins/annotation-tools.json` 5
(`init-registers-the-tools`, `init-with-selection`, `handlers-outrank-a-lower-handler`,
`fixed-bytes-stamp-tool`, `register-over-a-tool-and-take-it-back`), `plugins/form-design.json`
`init-with-annotation`, `plugins/interaction.json` `defaults` (CU-C3-D10).

1. `epdf-plugin-interaction`: `Tool.canUse` and `InteractionCapability::can_use_tool`
   (`d6325f5ba`). `epdf-plugin-annotation` and `epdf-plugin-form` register hub tools with
   `canUse = allows('annotations:create')`.
2. Command lists move to their owners: `createDocumentCommands` (`core/main/src/commands.ts`), and
   `createAnnotationCommands`, `createHistoryCommands`, `createInteractionCommands`,
   `createRenderCommands`, `createSelectionCommands`, `createStageCommands`; `CommandContext`,
   `CommandDefinition`, `CommandFamily` and `IconAccent` move to `core/main/src/command-types.ts`;
   `createStandardCommands` becomes their join. Cases, since all of these files are unloaded.
3. `history:undo` and `history:redo` (#1089) and `commands.history.{undo,redo}` in the 8 Rust string
   tables (`crates/plugins/i18n/src/strings/*.json`), which are printed from the TS strings.
4. `createTestCommandContext` (`core/main/src/testing.ts`, 17 lines), if the Rust harness mirrors it.
5. Evidence: `commands-current` and the c5 archive already pin the new `commands/src/contract.ts`;
   new cases will move `coverage.json`.

Wrappers (CU-C8, CU-C9): the per-plugin command lists and history commands through the commands
FFI, and `can_use_tool`.

### CU-C6: annotation draft slot, stamps, and the plugin side of signing (#1096, #1039, #1087, #1088, #1102)

Failing now: `plugins/signature.json` 32 replayed cases and 5 `epdf-plugin-signature` behaviour
tests; `plugins/annotation-tools.json` `ink-strokes-group` and
`ink-cancel-inside-the-grouping-window`.

1. `epdf-annotation-core`: `dropsWaitingInk`. `epdf-plugin-annotation`: the waiting pen drawing is
   kept when a message would drop it (`write/drafts`, `services/store`, the grouping window gone from
   `tools/handlers`), and `hasCreationAwaitingClick` on the host capability (`a41ab182c`).
2. `epdf-plugin-stamp`: `ImportLibraryOptions.id` (`a97140dd0`), with a case that passes one.
3. `epdf-plugin-signature`: settings `timestampAuthority`, `revocation`, `documentTimestamp`;
   per-call `timestampAuthority` on `sign` and `prepareSignature`; `SignFieldResult` with
   `validationInfo` and `validationInfoError`; `addValidationInfo`, `canAddValidationInfo`,
   `ValidationInfoAdded`; `validationTime` default `earliest-timestamp`; `canFill` taking what
   signing takes (`ceeb36bae`); `write/validation-info.ts` (85 uncovered lines). Depends on CU-C4
   items 8 to 10.
4. `epdf-plugin-form`: a widget of no size shows nowhere (`133030b8a`), a new field shows in the
   session's group at once (`134f24b77`, `sync/predict`, `write/fill`, 8 uncovered lines), and the
   `write/lending.ts` changes of #1088 and #1102.

Wrappers (CU-C8, CU-C9): the three settings, the per-call authority, `add_validation_info`,
`can_add_validation_info`, `SignFieldResult`'s two fields, the `earliest-timestamp` validation time,
the verdict's `revocation` and timestamp fields, and `ImportLibraryOptions.id`.

### CU-C8: browser, Kotlin, .NET

The D-1 transport on each (`fetch` to the D-2 routes in `epdf-web`, the Android HTTP stack in
Kotlin, `HttpClient` in .NET), with upstream's route limits (64 KiB request, 1 MiB reply, 10 s
default timeout); the CU-C4 engine read and write of validation data where a wrapper exposes engine
calls; and the CU-C5 and CU-C6 wrapper items above. The plugins reach the engine on every native
platform through `epdf-viewer-host`'s translation (CU-C4 item 12), so a platform test of a
long-term validation fill also exercises those rows. Tests: a timestamped signature and a long-term
validation fill against recorded replies on each platform.

### CU-C9: Swift

The same surface through `platforms/apple/EmbedPDF`, the D-1 transport on `URLSession`, and
`check-swift-api-coverage.py` with no new gap.

### Upstream PRs in `ed064e0c8..c4e744345`

| PR | Merge | Items |
|---|---|---|
| #1080 matrix red rows | `d51ef8fdd` | none: `isDev` in `engine/core/src/dev.ts` has no Rust counterpart, the rest is `tooling/bundler-matrix` |
| #1089 standard history commands | `4c668404f` | CU-C5 3 |
| #1087 signature timestamps | `3d167f772` | CU-C4 8, 9, 10; CU-C6 3; viewer chrome strings and UI not ported |
| #1088 long-term validation | `32571b4b7` | CU-C4 1 to 4, 7, 9, 10, 11, 12; CU-C6 3, 4; `cloudpdf` and docs not ported |
| #1039 viewer-app stamps | `540449699` | CU-C6 2; the viewer chrome app stamps are not ported |
| #1090 PKCS#7 attribute order | `c4e8a4962` | CU-C4 8 |
| #1091 plugin-owned commands | `d5d617f0d` | CU-C5 1, 2, 4, 5; framework packages not ported |
| #1092 pkijs 4 | `b59acec34` | CU-C4 8; `tooling/build` CJS bundling not ported |
| #1094 DER serial numbers | `e738c2bd8` | CU-C4 8 |
| #1093 judging policy as an engine value | `4b36e4335` | none beyond policy 5 (CU-C4 4): the Rust engine has no analysis token or cloud path to carry `engine` |
| #1096 draft slot keeps finished work | `6b424edce` | CU-C6 1 |
| #1081 version packages | `4b156274c` | CU-C4 5 |
| #1101 form control radius | `3c38eb569` | none: `framework/web` CSS |
| #1102 signing flow fixes | `b08c0075d` | CU-C4 3, 6, 7, 12; CU-C6 3, 4 |
| #1103 test authority clock | `c4e744345` | CU-C4 8 |

## Evidence

Base `cf7fdbe7a26e20863684bb7cced4de9cc6d47356`; merge source `474599496`; merge `44dcf70da`;
tested head `48568034c`; the head is the commit that adds this file. Logs are in
`/workspaces/logs/CU-C3/` in the container and `tmp/cu1118/logs/CU-C3/` of the host viewer checkout
(ignored), hashed in its `SHA256SUMS`. Scripts are in [`../scripts/cu-c3/`](../scripts/cu-c3/).
Hashes are sha256, first 8 hex digits.

| Check | Command | Exit | Log | sha256 |
|---|---|---|---|---|
| Baseline prep, suite, evidence | `heavy-build.sh … CU-C3-baseline.sh /workspaces/wt/CU-C3 base` | 0 | `CU-C3-suite-base.log`, `base/cargo.log`, `ev-base/summary.txt` | `e8d03f4b`, `3f537ee4`, `bb8f425d` |
| Baseline `replay:check` | `pnpm -s run replay:check` | 0 | `base/plugconf.replay:check.log` | `6dc06321` |
| Merge | `git merge --no-ff --no-commit research/catchup-2026-11-18-impl` | 1 (2 conflicts) | `CU-C3-merge.log` | `75d982db` |
| Lockfile | `pnpm install --no-frozen-lockfile` | 0 | `CU-C3-merge-pnpm-install.log`, `CU-C3-lockfile-diff.log` | `9c942ba6`, `109f997a` |
| Merge prep, typechecks, build, engine regen x2 | `heavy-build.sh … CU-C3-merge1.sh` | 0 | `CU-C3-cargo-build-merge.log`, `CU-C3-engregen-determinism.log` | `60907514`, `56b70145` |
| Plugin typecheck on the merge | `pnpm -s run typecheck` | 2 (1 error) | `CU-C3-plugconf-typecheck-merge.log` | `e45a6acb` |
| Engine regen, runs 1 and 2 | `regen-engine.sh` | 0 | `CU-C3-engregen-run{1,2}.sha` | both `cb82d554` |
| Wire and plugin regen x2 | `heavy-build.sh … CU-C3-regen2.sh` | 0 | `CU-C3-plugregen-run{1,2}.sha`, `CU-C3-wire-run1.sha` | both `8abf0c54`; `eb595a96` |
| Vector review | `zcasediff.mjs`, `reasons.mjs` | 0 | `CU-C3-plugvec-casediff.log`, `CU-C3-trust-reasons.log` | `90bcd1eb`, `4e5efa7b` |
| `coverage.json` | `pnpm -s run coverage` | 0 | `CU-C3-plug-coverage-write.log` | `7fdd91ed` |
| Evidence after regen, re-pins, final | `CU-C3-evrun.sh`, `srcpins.py`, `owners.py`, `chainpins.py` | 0 | `ev-regen/summary.txt`, `CU-C3-chainpins.log`, `ev-final/summary.txt` | `d043e2a2`, `b21afb3e`, `bb8f425d` |
| After suite | `heavy-build.sh … CU-C3-after.sh` | 0 (script) | `CU-C3-suite-after.log`, `after/cargo.log`, `after/plugconf.replay:check.log` | `0c9398c2`, `8fcab870`, `71c8e704` |
| Failures by crate and case | `cargo-by-crate.py`, `replay-cases.py` | 0 | `after/cargo-by-crate.tsv`, `after/replay-cases.tsv` | `2f6db48a`, `03726266` |
| wasm32 payload rebuild | `build:target wasm32` (CU-C3-D8) | 0 | `CU-C3-wasm32-rebuild.log` | `7f37e43a` |
| TS on the rebuilt payload | `CU-C3-suite.sh … ts` | 0 | `after-ts2/ts.log` | `0217da96` |
| Engine regen x2 and checks on it | `heavy-build.sh … CU-C3-engregen-fresh.sh` | 0 | `CU-C3-engregen-fresh-determinism.log`, `after-eng2/engconf.all` | `6392cace`, `6b03864f` |
| Coverage gaps | `python3 gaps.py` | 0 | `CU-C3-coverage-gaps.txt` | `690b778a` |

## Known gaps

- The 20 cargo failures and 118 replayed cases are the work order above, not fixed here.
- `operations:check` fails until CU-C4 adds the 2 rows and the plan documents say 96 kinds.
- The 2 evidence failures in `current-suites.test.ts` were there at `cf7fdbe7a`.
- `rust-coverage:check` was not run (CU-C3-D9).
- `runtime-src` names `af480d0ce`, which is only in the local runtime fork until it is pushed (D-3).
