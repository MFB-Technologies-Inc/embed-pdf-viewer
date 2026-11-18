# CU-C3 cargo failures

`cargo test --workspace --no-fail-fast` at `48568034c` on `research/catchup-2026-11-18-plugins`:
3,447 passed, 20 failed, 2 ignored (`cf7fdbe7a`: 3,467 passed, 0 failed, 2 ignored). Every failure is
upstream `ed064e0c8..c4e744345` behaviour the Rust port doesn't have yet; no Rust changed in this
job. The crate of each test binary comes from `cargo test --no-run --message-format=json`
(`scripts/cu-c3/cargo-by-crate.py`), and the replayed cases from the "N of M cases differ" blocks of
the same log (`scripts/cu-c3/replay-cases.py`). `replay:check` reports the same six vector files.

## By crate and cause

| Crate | Test binary | Test | Cause | Upstream | Job |
|---|---|---|---|---|---|
| `epdf-engine` | `change_ops` | `every_change_kind_has_the_version_it_arrived_in` | "the API version moved": `changes/kinds.json` is at `apiVersion` 3.3 | `4cb85c91e` (#1081) | CU-C4 |
| `epdf-engine` | `signature_protection` | `protection_agrees_with_typescript` | `policyVersion` 4 where the vector has 5, and the two new protected capabilities | `462380c7a` (#1088) | CU-C4 |
| `epdf-engine` | `wire_contract` | `every_kind_states_the_effect_its_interface_does` | `signatures.validationInfo` (read) and `signatures.addValidationInfo` (write) have no stated effect | `67ed292a6`, `656c2fc42` (#1088) | CU-C4 |
| `epdf-engine` | `wire_contract` | `every_wire_kind_routes_to_an_implementation` | the vector has 96 kinds, the router 94 | as above | CU-C4 |
| `epdf-engine` | `wire_contract` | `the_envelope_kinds_agree_with_the_vector` | `signatures.addValidationInfo: opId`: a write kind with no envelope entry | as above | CU-C4 |
| `epdf-engine` | `wire_contract` | `the_page_space_boundary_knows_every_wire_kind_and_no_other` | the boundary lists neither new kind | as above | CU-C4 |
| `epdf-signature-core` | `conformance` | `parse_matches_the_typescript` | `core/signature-parse.json`, 1 case | `30b269418`, `a841027e9` | CU-C4 |
| `epdf-signature-core` | `conformance` | `sign_and_validate_match_the_typescript` | `core/signature-document.json`, 22 cases: verdicts gain `revocation`, validation reads `getValidationInfo` first | `67ed292a6`, `3aa832da5`, `4d7588274` | CU-C4 |
| `epdf-signature-core` | `conformance` | `signers_match_the_typescript` | `core/signature-signers.json`, 1 case: DER serial | `6d6e76d3c` (#1094) | CU-C4 |
| `epdf-signature-core` | `conformance` | `trust_matches_the_typescript` | `core/signature-trust.json`, 46 cases: `validatePath` builds the path with the signer in it and new reasons; pkijs 4 messages | `30b269418`, `a841027e9` (#1092) | CU-C4 |
| `epdf-signature-core` | `conformance` | `verify_build_and_gate_match_the_typescript` | `core/signature-verify.json`, 7 cases: DER attribute order, gate details | `15955a843` (#1090), `30b269418` | CU-C4 |
| `epdf-plugin-signature` | `conformance` | `the_plugin_matches_the_typescript` | `plugins/signature.json`, 32 cases: verdicts gain `revocation`, new settings defaults, `SignFieldResult` | `1c165508c`, `3b3d7fb18`, `7bd135d7f` | CU-C6 |
| `epdf-plugin-signature` | `behavior` | `a_free_asset_placement_falls_back_to_the_stamp_plugin_and_settles` | asserts the replay of `place-by-mode-visual` | as above | CU-C6 |
| `epdf-plugin-signature` | `behavior` | `a_refused_fill_and_a_failed_signer_clean_up` | asserts the replay of `fill-and-clear` | as above | CU-C6 |
| `epdf-plugin-signature` | `behavior` | `an_armed_mark_off_a_field_stays_a_stamp_creating_annotation_60` | asserts the replay of `an-armed-mark-over-a-field` | as above | CU-C6 |
| `epdf-plugin-signature` | `behavior` | `an_armed_mark_over_a_widget_never_lands_as_a_stamp` | as above | as above | CU-C6 |
| `epdf-plugin-signature` | `behavior` | `an_imported_mark_reaches_the_field_as_a_sign_request` | as above | as above | CU-C6 |
| `epdf-plugin-annotation` | `conformance` | `tools_match_the_typescript` | `plugins/annotation-tools.json`, 7 cases: 5 lack `canUse` on hub tools (#1091), `ink-strokes-group` and `ink-cancel-inside-the-grouping-window` keep a waiting pen drawing (#1096) | `d6325f5ba`, `d0601e65e` | CU-C5 (5), CU-C6 (2) |
| `epdf-plugin-form` | `conformance` | `design_matches_the_typescript` | `plugins/form-design.json`, `init-with-annotation`: `canUse` on hub tools | `d6325f5ba` (#1091) | CU-C5 |
| `epdf-plugin-interaction` | `conformance` | `interaction_matches_the_typescript` | `plugins/interaction.json`, `defaults`: `canUseTool` on the capability | `d6325f5ba` (#1091) | CU-C5 |

## Replayed cases that differ (118)

| Vector file | Cases |
|---|---|
| `core/signature-document.json` (22) | `sign-raw-pkcs7`, `validate-empty`, `validate-valid-untrusted`, `validate-trusted`, `validate-untrusted-reason`, `validate-digest-mismatch`, `validate-partial-coverage`, `validate-chain-invalid`, `validate-signed-by-truthiness`, `validate-coverage-malformed`, `validate-unsupported-cms`, `validate-malformed-cms`, `validate-contents-rejects`, `validate-crypto-invalid`, `validate-crypto-unsupported`, `validate-no-revision`, `validate-forbidden`, `validate-forbidden-no-detail`, `validate-incomplete`, `validate-incomplete-no-detail`, `validate-permitted-undone`, `validate-analyze-rejects` |
| `core/signature-parse.json` (1) | `parse-essSha1` |
| `core/signature-signers.json` (1) | `self-signed-validity-past-2100` |
| `core/signature-trust.json` (46) | `trust-bad-anchor`, `trust-root`, `trust-root-with-root-carried`, `trust-intermediate-last`, `trust-expired-leaf-not-checked`, `trust-leaf-only-root`, `trust-other-root`, `trust-expired-signer`, `trust-at-timestamp`, `trust-at-now`, `trust-ca-without-basic-constraints`, `trust-ca-without-cert-sign`, `trust-issuer-not-ca`, `trust-wrong-issuer-name`, `trust-policy-policiesMatch`, `trust-policy-explicitShared`, `trust-policy-mappingMapped`, `trust-policy-mappingUnknownIssuer`, `trust-policy-inhibitAny`, `trust-policy-pendingConstraints`, `trust-policy-pendingOnly`, `trust-policy-dnsPermitted`, `trust-policy-emailPermitted`, `trust-policy-emailInSubject`, `trust-policy-uriPermitted`, `trust-policy-ipPermitted`, `trust-policy-directoryPermitted`, `trust-policy-ipv6Permitted`, `trust-policy-directoryEmpty`, `trust-policy-directorySuffix`, `trust-policy-directoryDecoy`, `trust-policy-directoryPrefix`, `trust-policy-explicitThenInhibit`, `trust-policy-requireOnlyMapping`, `trust-policy-mappingPendingInTime`, `trust-policy-mappingMinInTime`, `trust-policy-mappingMappedExplicit`, `trust-policy-emailSubjectPermitted`, `trust-at-not-before`, `trust-at-not-after`, `trust-tampered-signer`, `trust-aki-issuer-only`, `trust-ca-critical-not-ber`, `trust-ca-path-length-only`, `trust-shorter-path`, `trust-policy-two-certificates` |
| `core/signature-verify.json` (7) | `verify-essSha1`, `build-rsa-pkcs7-time`, `build-ecdsa-leading-zeros`, `build-signing-times`, `gate-too-large`, `gate-fractional-size`, `gate-signature-unsupported` |
| `plugins/annotation-tools.json` (7) | `init-registers-the-tools`, `init-with-selection`, `handlers-outrank-a-lower-handler`, `fixed-bytes-stamp-tool`, `register-over-a-tool-and-take-it-back` (CU-C5); `ink-strokes-group`, `ink-cancel-inside-the-grouping-window` (CU-C6) |
| `plugins/form-design.json` (1) | `init-with-annotation` |
| `plugins/interaction.json` (1) | `defaults` |
| `plugins/signature.json` (32) | `reads-over-the-snapshot`, `signed-by-truthiness`, `get-signature-edges`, `authority-twins`, `certify-needs-the-config`, `validate-and-the-invalidation-edge`, `revisions-and-analysis`, `document-events`, `background-failures-are-reported`, `close-drops-the-pending-validation`, `fill-and-clear`, `fill-without-an-engine-door`, `the-sign-here-target`, `fill-an-asset-mark`, `two-phase-signing`, `abort-the-parked-signing`, `a-parked-signing-from-elsewhere`, `sign-one-shot`, `sign-with-a-per-call-signer`, `sign-with-a-raw-signer`, `sign-without-a-signer`, `place-by-mode-visual`, `place-by-mode-ask`, `place-an-asset-without-the-stamp-plugin`, `place-by-mode-sign-seals`, `an-armed-mark-over-a-field`, `an-armed-stamp-is-not-a-mark`, `an-armed-mark-reports-its-handler`, `a-legacy-stamp-tool-keeps-the-arm`, `a-mark-that-fails-to-land`, `no-handler-without-the-hub-and-the-stamp-plugin`, `the-connected-controller-loads-its-mirror` |

The most common first difference, in 50 of the signature cases, is a verdict without `revocation`
(the field `3aa832da5` added). In `core/signature-trust.json` the reason text changes in 27 cases and
only the path length in 19 (`CU-C3-trust-reasons.log`). Four cases now carry a JavaScript
`TypeError` from inside pkijs as their reason ("Cannot read properties of undefined (reading
'0')"), where two did before; a port that matches reason strings has to reproduce that text or
record it as an outcome exception (CU-C3-D7).

Each test's first panic line is in `after/cargo-by-crate.tsv` and each case's first difference in
`after/replay-cases.tsv`, in the job's logs.
