# CU-C4a: the engine half of signatures

`research/catchup-2026-11-18-c4a` ports CU-C3's work order items 1 to 7 and 12 (CU-C4, engine and
signatures) onto `9041d7510`. Every item is ported. The 6 `epdf-engine` failures CU-C3 listed now
pass, and `cargo test --workspace` has no failing name outside CU-C3's list: 3,487 passed and 14
failed, the 14 being CU-C4b's 5 `epdf-signature-core` tests and CU-C5's and CU-C6's 9. A new
generator records 39 engine cases for long-term validation and the timestamp field, and all replay.
One divergence that predates the range surfaced: a signature over unsaved edits lands one revision
later than upstream (CU-C4a-D4). Decisions are in [`../decisions/CU-C4a.md`](../decisions/CU-C4a.md).

## Commits

| Commit | Item | Summary |
|---|---|---|
| `ccdd45fc0` | 1 | `pdfium`: `dss_count`, `dss_data`, `dss_add`, `loaded_object_value` |
| `746b48aa4` | 4 | Judging policy 5: protectable `doc.sign` and `doc.metadata.modify`, `ruleNoChangesCertification`, `/Annots` held in its own object, unwritten objects, the timestamp after P=1 |
| `1983a8128` | 5 | `API_VERSION` 3.3, `VALIDATION_INFO_SINCE`, recorded in `vectors/changes/kinds.json` |
| `ec6068ca6` | 2 | `signatures.validationInfo` and `signatures.addValidationInfo`, the event, `operations.ts` rows, 96 kinds |
| `0739e11c8` | 3, 12 | A timestamp prepared with no field (`createTimestampField`), its `doc.sign` gate and its event field |
| `d0af51452` | 6 | The visual fill takes the right to sign |
| `80565e6a4` | 12 | `epdf-viewer-host` rows for both members; a kernel test that the new event marks a document unsaved |
| `d05f2dae7` | 2, 4 | `ProtectedDocument` from the session gate, and no protection enforced under `permit` (CU-C4a-D3) |
| `adc2ce544` | 7 | Generator `signature-ltv`, runner `tests/signature_ltv.rs`, both kinds `ported` |
| `1239df825` | 7 | `vectors/signature/{validation-info,timestamp-field,long-term}.json` |
| the docs commit | | This report, the decisions, the job's scripts |

## Items

1. Done (`ccdd45fc0`). `pdfium::dss_kind` names the `EPDF_DSS_*` kinds. `pdfium` tests store and
   read back two entries, read a long-term file's store, and read a trailer, a catalog and an absent
   object through `loaded_object_value`.
2. Done (`ec6068ca6`, `d05f2dae7`). `features::signature::validation_info` ports
   `validationInfo.ts`, `validationStore.ts`, `ValidationInfoReader` and `ValidationInfoMutator`. The
   write runs in a layer transaction under its `opId`, refuses while a signing is parked, is never
   undoable, and publishes `signatures.validationInfoAdded` only when it stored something. The 4
   `wire_contract` tests pass.
3. Done (`0739e11c8`). `SignaturePrepareInput.field` is optional for `kind: 'timestamp'`; the field is
   made on the candidate and answered by name, and the answer carries `fieldObjectNumber`.
4. Done (`746b48aa4`). `signature_protection` passes. The `analysis.test.ts` changes of the range are
   in `analysis/tests.rs` (3 tests changed, 20 added). The 6 test files CU-C3 named send and expect
   `policyVersion` 5.
5. Done (`1983a8128`). `change_ops` passes.
6. Done (`d0af51452`). The op already checked the field's group for signing; the host now refuses a
   session that can sign no field first, as `beginFieldWrite('sign')` does.
7. Done (`adc2ce544`, `1239df825`), with the three steps of CU-C4a-D4 listed. `validation-info.json`
   (10 cases) covers the 8 validation tests of `runSignatureConformance.ts`; `timestamp-field.json`
   (7) the 3 timestamp tests and the group-signer test; `long-term.json` (22) every file of
   `fixtures/signature-long-term` judged signature by signature, and `signature-validation-info.test.ts`.
   Every policy-5 verdict on those files matches the TypeScript.
12. Done (`0739e11c8`, `80565e6a4`, and the host gates and event in `ec6068ca6`). Both rows go
   through `translate.rs` with their entries as payloads. `getValidationInfo` decodes with
   `Decode::Result`, whose payload pass already turns nested `{payload}` into bytes, so no new decode
   shape was needed.

## Divergence

`SignatureMutator.openCandidate` seals a session's unsaved edits in the signature's own revision.
The Rust `prepare` builds its candidate from an incremental save, so the edits get a revision of
their own and the signature lands one later. Three replayed steps show it and are listed in
`signature_ltv.rs` (CU-C4a-D4); each must differ at its pointer and nowhere else. The behaviour is
not from this range, but nothing measured it before. Porting the layer path of `openCandidate` is
left for the controller to assign.

## For CU-C4b

No `epdf-signature-core` call surface was added. The engine side CU-C4b and CU-C6 build on: `Host`
kinds `signatures.validationInfo` (answer `{certificates, ocspResponses, crls}`, each entry
`{payload: n}`) and `signatures.addValidationInfo` (each entry a request payload, answer
`{result: {added, meta}}`), and in `epdf-viewer-host` the kernel paths
`signatures.getValidationInfo` and `signatures.addValidationInfo`.

## Tests

All in container `embed-pdf-catchup1118`, worktree `/workspaces/wt/CU-C4a`,
`CARGO_TARGET_DIR=/workspaces/target`.

| Check | CU-C3 (`48568034c`) | Now (`1239df825`) |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,447 passed, 20 failed, 2 ignored | 3,487 passed, 14 failed, 2 ignored |
| engine `:check` scripts | 34 of 35 (`operations:check`) | 36 of 36 (`signature-ltv:check` added) |
| engine regeneration twice | identical | identical, 64 files, no diff to the tree |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | all pass | all pass |
| plugin `replay:check` | 118 cases in 6 files | the same 118 (CU-C4b, CU-C5, CU-C6) |
| TS `turbo run test` over `packages/**` | 98 of 98 | 98 of 98 |

The 14 failures against `../results/CU-C3-cargo-failures.md`: `epdf-signature-core` `conformance`
5 (CU-C4b), `epdf-plugin-signature` `behavior` 5 and `conformance` 1 (CU-C6),
`epdf-plugin-annotation` `tools_match_the_typescript`, `epdf-plugin-form`
`design_matches_the_typescript` and `epdf-plugin-interaction` `interaction_matches_the_typescript`
(CU-C5, CU-C6). The 6 `epdf-engine` names are gone and none is new.

## Evidence

Base `9041d7510`; tested head `1239df825`; the head is the commit that adds this file. Logs are in
`/workspaces/logs/CU-C4a/` in the container and `tmp/cu1118/logs/CU-C4a/` of the host viewer
checkout (ignored), hashed in its `SHA256SUMS`. Scripts are in [`../scripts/cu-c4a/`](../scripts/cu-c4a/).
sha256, first 8 hex digits.

| Check | Command | Exit | Log | sha256 |
|---|---|---|---|---|
| Prep (payload rebuilt, CU-C3-D8) | `heavy-build.sh … prep.sh` | 0 | `prep.sh` | `131eadd1` |
| Baseline own crates | `cargo test -p epdf-engine -p pdfium -p epdf-viewer-host` | 101 (the 6) | `base-cargo-mine.log` | `bd656678` |
| `changes` regeneration | `pnpm -s run changes` | 0 | `changes-run1.log` | `509f953a` |
| After run | `heavy-build.sh … after.sh` | 0 | `after.sh` | `4b5533d4` |
| Engine regeneration x2 | `regen-engine.sh` | 0 | `engregen-run{1,2}.sha`, `engregen-determinism.log` | both `d8ed8663`; `0c94355b` |
| Workspace tests | `suite.sh … cargo` | 101 | `after/cargo.log`, `after/cargo.failed` | `d59fe439`, `8cc7c8bd` |
| Engine `:check` | `suite.sh … engconf` | 0 | `after/engconf.all` | `27a10bbd` |
| Plugin checks | `suite.sh … plugconf`, `replay:check` | 0, 1 | `after/plugconf.all`, `after/plugconf.replay:check.log` | `09dd3f6f`, `23773cd1` |
| TS | `suite.sh … ts` | 0 | `after/ts.log` | `618706bb` |

## Known gaps

- CU-C4a-D4's revision gap, above.
- `rust-coverage:check` was not run (as CU-C3-D9).
- `runtime-src` names `af480d0ce`, which is only in the local runtime fork until it is pushed (D-3).
