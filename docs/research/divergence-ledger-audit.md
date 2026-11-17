# Divergence ledger audit

`crates/plugin-conformance/divergences.json` holds 48 kept divergences: the 41 this audit entered, M13, which was in the ledger before the audit, four MO- entries, one per cause of the member-order differences the member-order allowlist exempts, and two PLATFORM-MATH- entries, one per math function family the geometry differential found rounding the last bit differently. 2 are observed by a vector case (one of them, M27, by a case added for the purpose) and also pinned; 46 are pinned by a Rust test only. 31 rows below name kept divergences with no test to pin them, so they are not in the ledger yet. 3 could not be tied to a decision.

Every replay passes today with only M13 in the ledger before this audit, so no existing vector case could observe any other divergence: a differing trace would already have failed. Observing one needs a new scenario case. The ledger applies to scenario suites only, so a divergence that a function suite would show (`formatShortcut("Ctrl+constructor")`, a `toString` face name) is pinned instead.

The sources were the Status section of `docs/research/rust-plugins-branch-review.md`, the Decisions and "Kept divergences to re-check" passages of `multiplatform-upstream-catchup*.md`, `docs/research/upstream-catchup-2026-11-07` and `-2026-11-12` (whose `decisions.md` records no new divergence), and the crate READMEs that call a behavior a divergence. Short ids in capitals are new; each entry's `reason` names the record it came from. The first table is rendered from the ledger by a script kept outside the repo.

## In the ledger

| Id | Evidence | Decided |
| --- | --- | --- |
| M13 | observed: `core/acrojs-host.json` `dispose-pending-factory`, moved callback; pinned: `crates/plugins/acrojs/src/host.rs::a_realm_built_after_dispose_is_released_when_the_queue_drains` | 2026-10-23 22ca510e6 |
| M15 | pinned: `crates/plugins/actions/tests/external_uri.rs::only_allowed_schemes_reach_open_uri` | 2026-10-08 7d53dae0c |
| ACTIONS-NO-DEFAULT-SANDBOX | pinned: `crates/plugins/actions/tests/behavior.rs::scripting_on_without_a_factory_has_a_realm_whose_runs_fail` | Task 13.12 |
| ACROJS-FOREIGN-SANDBOX-OUTPUT | pinned: `crates/plugins/actions/tests/ffi_sandbox.rs::malformed_output_is_invalid_output` | 2026-11-07 catch-up (ix) |
| N6 | pinned: `crates/plugins/annotation/tests/behavior.rs::n6_a_record_whose_ref_has_no_key_is_refused_and_the_others_stay` | 2026-10-12 343fc063e |
| ANNOTATION-PROTOTYPE-KIND | pinned: `crates/plugins/annotation-core/tests/behavior.rs::a_name_object_prototype_has_is_a_kind_only_shown` | Task 13.7 (Ruling13.7) |
| ANNOTATION-CORE-SETSNAP | pinned: `crates/plugins/annotation-core/tests/behavior.rs::a_snap_patch_that_does_not_read_changes_nothing` | Task 13.7 |
| ANNOTATION-CORE-MEASURE-LOOSE | pinned: `crates/plugins/annotation-core/tests/engine_cross_check.rs::a_malformed_measure_the_typescript_throws_on_reads_loosely_in_both_copies` | Task 13.7, Task 14.3 |
| M20 | pinned: `crates/plugins/annotation-core/tests/hostile_geometry.rs::an_absurd_cloudy_border_draws_nothing` | 26e0aa3b0 |
| COMMANDS-DEFINITION-IDENTITY | pinned: `crates/plugins/commands/src/settings.rs::equal_definitions_share_a_handle` | Task 13.29 |
| COMMANDS-UNDECODED-SETTINGS | pinned: `crates/plugins/commands/src/settings.rs::a_member_that_does_not_decode_is_reported` | Task 13.29 |
| I18N-LOJBAN | pinned: `crates/plugins/i18n/src/script.rs::a_language_and_variant_alias_is_not_applied` | Task 13.27 |
| I18N-UNDECODED-SETTINGS | pinned: `crates/plugins/i18n/src/settings.rs::a_member_that_does_not_decode_is_reported` | Task 13.27 |
| INTERACTION-TOOL-IDENTITY | pinned: `crates/plugins/interaction/tests/api.rs::distinct_equal_settings_tools_count_as_a_change` | Task 13.11 |
| LINK-UNDECODABLE-DESTINATION | pinned: `crates/plugins/link/tests/behavior.rs::an_undecodable_destination_still_reveals_its_page` | Task 13.21 |
| MEASUREMENT-PRESETS-TYPED | pinned: `crates/plugins/measurement/tests/api.rs::a_presets_setting_the_typed_read_cannot_hold_is_refused` | Task 13.22 |
| METADATA-CHANGED-KEYS-PRIMITIVE | pinned: `crates/plugins/metadata/src/model.rs::a_previous_that_is_not_an_object_has_no_keys_here` | Task 13.26 |
| OUTLINE-LOOP | pinned: `crates/plugins/outline/src/model.rs::a_key_under_two_parents_is_listed_under_each_and_a_loop_ends` | Task 13.31 |
| FORM-INDEX-CACHE-MISS | pinned: `crates/plugins/form/src/services/index_cache.rs::a_miss_is_logged_and_shows_the_confirmed_form` | Task 13.18 |
| M25 | pinned: `crates/plugins/render/tests/api.rs::a_synchronous_tile_refusal_frees_its_slot` | 2026-10-11 f0b535982 |
| R7 | pinned: `crates/plugins/render/tests/api.rs::a_zero_tile_span_yields_no_tiles_instead_of_looping` | 2026-10-12 0695bc428 |
| RENDER-MALFORMED-EVENT | pinned: `crates/plugins/render/src/invalidation.rs::a_signing_repaints_its_widget_page_and_nothing_without_one` | Task 13.16 |
| RENDER-MALFORMED-SETTINGS | pinned: `crates/plugins/render/tests/behavior.rs::a_malformed_setting_reads_as_the_defaults_and_warns_once` | Task 13.16 |
| M23 | pinned: `crates/plugins/selection/tests/stale_text.rs::a_change_during_the_first_batch_fails_the_read` | 2026-10-11 e8205de75 |
| SELECTION-MALFORMED-ROW | pinned: `crates/plugins/selection/tests/malformed_registry.rs::a_page_beside_a_malformed_row_still_selects` | Task 13.15 (S1) |
| M27 | observed: `plugins/stage-capability.json` `grid-nan-columns`, value at `$[3].reports`; observed: `plugins/stage-capability.json` `grid-nan-columns`, value at `$[3].outcome`; observed: `plugins/stage-capability.json` `grid-nan-columns`, value at `$[4].outcome`; pinned: `crates/plugins/stage/tests/api.rs::a_nan_column_count_empties_the_grid_instead_of_throwing` | 2026-10-11 4965e0519 |
| R5 | pinned: `crates/plugins/stage/tests/api.rs::an_unreadable_page_row_keeps_the_others_at_their_indexes` | 2026-10-12 1bd756fb3 |
| STAGE-NON-INVERTIBLE | pinned: `crates/plugins/stage/tests/api.rs::a_non_invertible_page_transform_leaves_the_page_out` | Task 13.14 |
| STAGE-LOCK-ORDER | pinned: `crates/plugins/stage/tests/zoom_tools.rs::a_locks_settings_are_listed_in_setting_order` | 2026-11-03 bb4d814d9 (C5b) |
| UI-CORE-OWN-MEMBERS | pinned: `crates/plugins/ui-core/tests/behavior.rs::an_object_prototype_name_is_no_bar_menu_or_key_name` | Task 13.10 |
| UI-CORE-NULL-ENTRIES | pinned: `crates/plugins/ui-core/tests/behavior.rs::a_null_bar_or_panel_is_skipped_where_the_typescript_throws` | Task 13.10 |
| UI-CORE-STRING-RECORD | pinned: `crates/plugins/ui-core/src/value.rs::spreads_and_reads_members_as_javascript_does` | Task 13.10 |
| E3 | pinned: `crates/epdf-engine/src/features/xmp/xml.rs::a_reference_to_a_lone_surrogate_decodes_to_the_replacement_character` | 2026-11-02 f8e959eab (C4) |
| FALLBACK-TIER-NAN-ORDER | pinned: `crates/epdf-engine/tests/font_registry.rs::a_nan_tier_compares_equal_and_never_panics` | 2026-11-02 798f7b229 (C3) |
| FONTS-REGISTER-STRICT | pinned: `crates/epdf-engine/tests/font_settings.rs::the_register_wire_refuses_what_the_typescript_passes_unchecked` | 2026-10-15 7566552a2 |
| FONTS-RESOLVE-STRICT | pinned: `crates/epdf-engine/tests/document_fonts_wire.rs::malformed_runs_and_missing_annotations_are_refused` | 2026-11-02 C4 (c4/fonts) |
| FONT-SETTINGS-POLICY | pinned: `crates/epdf-engine/tests/font_settings.rs::an_unknown_embedding_policy_is_refused` | 2026-10-14 dd2e1c290 |
| PAGEREF-MALFORMED | pinned: `crates/epdf-engine/src/identity.rs::a_malformed_page_ref_is_an_invalid_argument` | 2026-10-15 3baec217b |
| EVENTS-SIGNING-ENVELOPE | pinned: `crates/epdf-engine/tests/signature_sign.rs::prepare_then_complete_seals_the_field` | 2026-10-15 517d0123d |
| NOOP-MUTATION-SEQUENCE | pinned: `crates/epdf-engine/tests/annotation_flatten.rs::a_flatten_that_applies_nothing_is_no_mutation` | 2026-10-15 87b61b533 |
| TEXT-LAYOUT-POINT-PAST-RUN | pinned: `crates/epdf-engine/src/features/text/layout.rs::a_point_on_a_glyph_the_run_claims_past_the_end_reads_no_further` | Task 14.3 |
| TEXT-LAYOUT-NEGATIVE-COUNT | pinned: `crates/epdf-engine/src/features/text/layout.rs::a_line_that_reads_backwards_has_no_characters` | Task 14.3 |
| MO-acrojs-event | pinned: `crates/plugins/acrojs/src/types.rs::a_script_event_prints_in_field_order_not_the_callers` | 2026-10-13 8cf572cf5 |
| MO-richtext-paragraph | pinned: `crates/plugins/annotation-core/tests/behavior.rs::a_normalized_paragraph_prints_runs_before_its_other_members` | 2026-10-13 8cf572cf5 |
| MO-ui-group-options | pinned: `crates/plugins/ui-core/tests/behavior.rs::a_group_prints_its_options_in_field_order_not_the_callers` | 2026-10-13 8cf572cf5 |
| MO-submit-request | pinned: `crates/plugins/actions/tests/behavior.rs::a_submit_request_prints_in_field_order_not_the_resolvers` | 2026-10-13 8cf572cf5 |
| PLATFORM-MATH-COS-SIN | pinned: `crates/plugins/geometry/tests/platform_math.rs::rotate_about_differs_only_by_one_ulp_of_the_platform_cos_and_sin` | 2026-11-17, after the geometry differential |
| PLATFORM-MATH-ATAN2 | pinned: `crates/plugins/geometry/tests/platform_math.rs::a_degenerate_quad_is_labelled_only_as_one_ulp_of_the_platform_atan2_allows` | 2026-11-17, after the geometry differential |

## Why no trace sees the pinned ones

M15. A URI action whose scheme is not on the allow-list never reaches open_uri. Rust-only divergence: the TypeScript has the same gap. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", findings table (M15). No vector sends a URI with another scheme, so no trace sees it.

ACTIONS-NO-DEFAULT-SANDBOX. With javascript on and no sandbox factory, every sandbox build fails with NO_DEFAULT_SANDBOX. The TypeScript lazily loads QuickJS; Rust has no JavaScript engine, so the document gets its realm but each javascript node fails. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.12, and crates/plugins/actions/README.md. No vector runs a script in that configuration (no-sandbox-host closes the document first). A dynamic stamp arm fails the same way (crates/plugins/stamp/README.md).

ACROJS-FOREIGN-SANDBOX-OUTPUT. A foreign-engine sandbox output that does not decode as typed AcroJS output is invalid-output. Rust decodes the AcroJS output fully typed; the TypeScript isScriptOutput only checks that arrays hold records, so a malformed effect passes there. Recorded as a pre-existing divergence in docs/research/upstream-catchup-2026-11-07/decisions.md ("ix"). The conformance vectors drive a scripted sandbox, not a foreign engine, so no trace sees it.

N6. A record whose ref is of a kind no key is defined for is refused instead of keyed "undefined". Rust-only divergence from the Lows (part of N6): the TypeScript keys every such record "undefined" and collapses them into one. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13" (Lows) and crates/plugins/annotation/README.md, "Kept divergences". No vector holds such a ref.

ANNOTATION-PROTOTYPE-KIND. A kind name Object.prototype holds (constructor, toString) is an unknown kind that is only shown. Ruling13.7: the TypeScript kindNamed finds the inherited member and later reads throw. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.7 paragraph. The annotation-core harness prints the TypeScript answers for those names, so the 13 vector cases that name them compare against the TypeScript rather than the core: no trace can see the core behavior without changing the harness.

ANNOTATION-CORE-SETSNAP. A setSnap patch the typed SnapSettings cannot hold leaves the model unchanged. The TypeScript spreads the patch in. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.7 ("three refusals no vector reaches") and crates/plugins/annotation-core/README.md, "Not ported".

ANNOTATION-CORE-MEASURE-LOOSE. A malformed /Measure dictionary is read loosely where the TypeScript throws or prints "undefined". Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.7, and crates/plugins/annotation-core/README.md, "Not ported" (a null format entry, a missing x/distance/area list, a missing unit, a mistyped member). Since Task 14.3 epdf-engine reads it the same way. No vector holds one. The pin asserts both Rust copies' answers for three inputs the TypeScript throws on or prints "undefined" for: a missing unit, a null format entry and a missing distance list.

M20. A cloudy border or cloud outline past 1,000,000 curls or points is empty. The TypeScript allocates until memory runs out or loops. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", findings table (M20) and Task 13.7, and crates/plugins/annotation-core/README.md, "Not ported". A vector cannot record a run that never ends.

COMMANDS-DEFINITION-IDENTITY. A changed commands list reads back the first definition equal by sameValue, not the new object. Identity only: every data member and function is the same. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.29, and crates/plugins/commands/README.md, "Kept Rust-only divergences". The trace compares values, not identity, so no trace can see it.

COMMANDS-UNDECODED-SETTINGS. A commands settings value that does not decode is reported and read as empty. Only a write past update_settings can store one; the TypeScript resolves whatever is there. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.29, and crates/plugins/commands/README.md. No vector writes past the typed API.

I18N-LOJBAN. script_of skips the language-and-variant alias art-lojban. ICU says Latn (as jbo); Rust says no script, which negotiation reads as "cannot tell". Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.27, and crates/plugins/i18n/README.md. The 13 negotiateLocale cases in core.json do not name it.

I18N-UNDECODED-SETTINGS. An i18n settings value no update_settings wrote is reported and read as empty where it does not decode. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.27, and crates/plugins/i18n/README.md. No vector writes past the typed API.

INTERACTION-TOOL-IDENTITY. Settings tools compare by identity, so two distinct tools with equal data are a change. The TypeScript sameValue deep-compares a plain tool given without enables. Rust fires on_settings_changed and swaps the tools where the TypeScript does nothing. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.11, and crates/plugins/interaction/README.md. No vector reaches it.

LINK-UNDECODABLE-DESTINATION. A goto destination the stage cannot decode gets a bare reveal of its page and a report. For a known kind with an unreadable member the TypeScript places the camera from that member. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.21, and crates/plugins/link/README.md, "Where the port differs". No vector sends one.

MEASUREMENT-PRESETS-TYPED. The typed measurement settings read refuses a presets value that is not a list of presets. The TypeScript getSettings hands back whatever was written; the JSON read keeps that answer. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.22, and crates/plugins/measurement/README.md. No vector writes past the typed API.

METADATA-CHANGED-KEYS-PRIMITIVE. changed_keys lists no keys for a previous or next that is not an object. Object.keys on a string lists its indices in the TypeScript. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.26, and crates/plugins/metadata/README.md, "Rust-only divergences". No vector or engine answer sends one.

OUTLINE-LOOP. A loop in the outline tree's children is not entered again. The TypeScript recurses until the stack overflows or, in subtreeOf, never ends. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.31, and crates/plugins/outline/README.md, "Rust-only divergences". A vector cannot record a run that never ends.

FORM-INDEX-CACHE-MISS. A fields-mirror token the cache no longer holds is logged and shows the confirmed form. A Rust-only console line with no TypeScript counterpart. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.18, and crates/plugins/form/README.md, "What differs from the TypeScript". The pin and recency make it unreachable from a vector.

M25. A fetch_tile that answers Err at once fails that tile and frees its slot. Rust-only divergence: a synchronous throw in the TypeScript would strand the slot. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", findings table (M25) and Task 13.16, and crates/plugins/render/README.md. Only a Rust embedder of TileManager can answer that way; the TypeScript fetchTile is async, so no vector can.

R7. A zero tile span yields no tiles. Rust-only guard from the Lows: the TypeScript loop runs out of memory. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13" (Lows) and crates/plugins/render/README.md. A vector cannot record a run that never ends.

RENDER-MALFORMED-EVENT. An event missing or mistyping a member pixelChangeOf reads names no page for it. The TypeScript throws a TypeError in its listener or keys the ledger with the value as given. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.16, and crates/plugins/render/README.md, "Rust-only divergences". The pin covers a signing fact whose widget is null; the other malformed members are unpinned by name.

RENDER-MALFORMED-SETTINGS. A render settings value that does not decode reads as the defaults and warns once. The TypeScript resolves whatever is there. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.16, and crates/plugins/render/README.md. No vector writes past the typed API.

M23. A content change during readText fails the read with conflict. Rust-only divergence: the TypeScript has the same race. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", findings table (M23) and Task 13.15. No vector changes content mid-read.

SELECTION-MALFORMED-ROW. A page row whose ref does not decode is skipped by pageAtIndex. The TypeScript hands the raw value on. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.15 (S1 now covers it). No vector sends such a row.

R5. A scene with an unreadable page row lays out the other pages at their indexes. Rust-only divergence from the Lows: the TypeScript throws on every build, as for M27. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13" (Lows) and Task 13.14. No vector records the throw.

STAGE-NON-INVERTIBLE. A page transform that is not invertible leaves the page out of the page reads. The TypeScript throws "Mat2D.invert: non-invertible matrix". Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.14, and crates/plugins/stage/README.md. No vector reaches it; the zoom limits keep the camera at 5% or more.

STAGE-LOCK-ORDER. A stage lock's settings are checked and recorded in setting order, not the caller's key order. So the key order of locks can differ, and so can which refusal wins when one lock both conflicts and has invalid input (widened in C5b review round 1). Accepted 2026-11-03, recorded in multiplatform-upstream-catchup-2026-11-impl.md, "Three upstream commits, not one". Neither part is in a vector. The pin covers the key order only; the error precedence is unpinned.

UI-CORE-OWN-MEMBERS. Layout, menu, panel and key-name lookups read own members only. The TypeScript finds Object.prototype members for constructor and __proto__, so formatShortcut("Ctrl+constructor") prints "Ctrl+" there and "Ctrl+Constructor" here. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.10, and crates/plugins/ui-core/README.md. A function-suite case would show it, but the ledger applies to scenario cases only, so it is pinned.

UI-CORE-NULL-ENTRIES. A null bar or panel, or null layout data, is read as empty where the TypeScript throws a TypeError. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.10, and crates/plugins/ui-core/README.md. No vector holds one.

UI-CORE-STRING-RECORD. A bars, menus or panels record that is a string reads each half of a surrogate pair as U+FFFD. The TypeScript spreads the string into UTF-16 unit keys and keeps lone surrogates, which a Rust string cannot hold. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 13.10, and crates/plugins/ui-core/README.md. No vector holds one.

E3. An XML character reference to a lone surrogate decodes to U+FFFD in the XMP port. The TypeScript makes a lone surrogate a Rust string cannot hold. Accepted 2026-11-02, recorded in multiplatform-upstream-catchup.md, Task 8.3 ("Accepted 2026-11-02 ... E3") and multiplatform-upstream-catchup-2026-11-impl.md, "C4: the eight calls ported". The plugin vectors run a fake engine and never parse XMP. Pinned by a test added in Task 7.

FALLBACK-TIER-NAN-ORDER. With a NaN fallback tier, the reported fallback order follows a stable insertion sort. The TypeScript order depends on the JavaScript engine's sort with a comparator that is not total. PDFium's chain matches. Accepted 2026-11-02, recorded in multiplatform-upstream-catchup-2026-11-impl.md, "The tier at the FFI boundary". The plugin vectors run a fake engine.

FONTS-REGISTER-STRICT. fonts.register refuses missing font bytes, a fractional or boolean weight or italic (WireFormat) and a NUL in familyName (InvalidArg). The TypeScript passes these to its registrar unchecked. Kept 2026-10-15, recorded in multiplatform-upstream-catchup.md, Task 8.3 ("Kept divergences to re-check"). The plugin vectors run a fake engine. Pinned by a test added in Task 7; the bool-italic part was already pinned by italic_is_the_c_apis_number_rather_than_a_bool.

FONTS-RESOLVE-STRICT. document.fonts.resolve refuses wrongly typed run fields as WireFormat; a numeric weight takes ToInt32. The TypeScript passes such values to its Int32Array and string writers unchecked. Accepted 2026-11-02, recorded in multiplatform-upstream-catchup.md, Task 8.3 ("Accepted 2026-11-02 ... document.fonts.resolve"). The plugin vectors run a fake engine.

FONT-SETTINGS-POLICY. document.setFontSettings refuses an unknown embeddingPolicy as InvalidArg. The TypeScript passes undefined on. Kept 2026-10-14, recorded in multiplatform-upstream-catchup.md, Task 8.3 ("Kept divergences to re-check"). The plugin vectors run a fake engine.

PAGEREF-MALFORMED. A malformed PageRef (unknown kind, bad number) is refused as InvalidArg on the Rust worker wire. The TypeScript worker does not validate. Kept 2026-10-15, listed in multiplatform-upstream-catchup.md, Task 8.27 ("Kept divergences to re-check", 3baec217b). The plugin vectors run a fake engine.

EVENTS-SIGNING-ENVELOPE. Signing notices travel in an envelope-level event. Kept 2026-10-15 from the events port, recorded in multiplatform-upstream-catchup.md, Task 8.26 ("Kept divergences to re-check"). The plugin vectors run a fake engine; the pin asserts the completed notice in the answer's event member.

NOOP-MUTATION-SEQUENCE. A no-op flatten or redaction answer carries mutationSequence where the TypeScript sends only {tag, result}. Kept 2026-10-15, recorded in multiplatform-upstream-catchup.md, Task 8.6, Task 8.17 and Task 8.23 ("Kept divergences to re-check"); the forms.applyEffects part is obsolete (Task 8.20). The plugin vectors run a fake engine. The pin covers flatten; the redaction half is unpinned by name.

TEXT-LAYOUT-POINT-PAST-RUN. A point read passes over a glyph the run claims that is not there. The TypeScript reads undefined and throws a TypeError. Input PDFium never produces. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 14.3 paragraph.

TEXT-LAYOUT-NEGATIVE-COUNT. A line range whose TypeScript count is negative has count 0 in the engine. TextRange counts in u32. Input PDFium never produces. Recorded in docs/research/rust-plugins-branch-review.md, "Status, 2026-10-13", Task 14.3 paragraph (and Task 13.15 for the selection copy).

PLATFORM-MATH-COS-SIN and PLATFORM-MATH-ATAN2. Rust's `cos`, `sin` and `atan2` come from the platform math library, which can round the last bit differently from the JavaScript runtime. Recorded in docs/research/conformance-verification.md. Only the function suites reach them, and the ledger's observations apply to scenario cases, so each is pinned on its recorded input. The differential accepts them through each entry's `differentialSkip` (tooling/plugin-conformance/README.md, "The divergence ledger").

## Kept, but not pinned by any test

These need a test or a scenario case before they can enter the ledger. Each was searched for by test name and by test body.

| Ids | Record | Behavior | Why unpinned |
| --- | --- | --- | --- |
| P3 | `multiplatform-upstream-catchup.md` Task 8.3, accepted 2026-11-02; `multiplatform-upstream-catchup-2026-11-impl.md` "C4: upstream c7f6a7958 ported" | The waits for a pending page approximate the TypeScript turn counts, in the page mirror and in render. | The page-view tests assert that a pending page resolves once the engine has it, which the TypeScript does too; no test asserts a Rust turn count. |
| C5-M3 | `multiplatform-upstream-catchup.md` Task 8.3, accepted 2026-11-03; `multiplatform-upstream-catchup-2026-11-impl.md` "Round 2 review fixes" | `CommandsSettings::app_state` carries JSON values only, where the TypeScript `appState` can hold functions and getters. | A type, not a behavior. `every_reader_wakes_once_a_command_ran` tests the closures that replace in-place state, not the restriction. |
| FREETEXT-FONT-LOOKUP | `multiplatform-upstream-catchup.md` Task 8.12 (kept 2026-10-14, `818eba296`) | The FreeText reader resolves registered fonts through the thread font registry, not a passed-in registrar. | Where the lookup comes from is not visible in any answer. The rich-text unit tests pass a lookup closure in, so they cover the other mechanism. |
| P1 | `multiplatform-upstream-catchup.md` Task 8.3, accepted 2026-11-02 | A page-view refresh that fails on the change-notify path goes to the kernel report; the TypeScript throws into `stage`. | No test makes a refresh fail on that path. |
| E4 | `multiplatform-upstream-catchup.md` Task 8.3, accepted 2026-11-02 | `page_label_sections` skips a section the runtime refuses to read. | The fork refuses no index below the count, so no test can reach the skip without a fault-injecting runtime. |
| STAGE-LOCK-RELEASE | `multiplatform-upstream-catchup-2026-11-impl.md` "Three upstream commits, not one", accepted 2026-11-03 | A lock release cannot throw, so a failed reframe on release is dropped. | No test makes the reframe fail. |
| STAGE-LOCK-ORDER (precedence part) | same, widened in C5b review round 1 | When one lock both conflicts and has invalid input, Rust may report the other refusal. | The order part is pinned; no test names the precedence. |
| STAGE-PATCH-IDENTITY | `rust-plugins-branch-review.md` Status, Task 13.14; `crates/plugins/stage/README.md` | `patchSettings` counts an object-valued setting as new, where the TypeScript writes nothing for the very object it holds. | No test by name or body search. |
| FINALIZE-CANDIDATE-PLAIN | `multiplatform-upstream-catchup.md` Task 8.26 (kept 2026-10-14, `dd2e1c290`) | `signatures.finalizeCandidate` opens a plain file document, not a layer. | The finalize tests check the sealed file, not the document kind. |
| EVENTS-VERSIONED-NATIVE | `multiplatform-upstream-catchup.md` Task 8.3 (kept 2026-10-15, `517d0123d`) | `document.versioned` is added in `epdf-viewer-host/src/native_engine.rs`, not the host. | No test asserts the native fact. |
| FREETEXT-STYLE-UNTYPED | `multiplatform-upstream-catchup.md` Task 8.12 (kept 2026-10-14, `818eba296`) | Rich-text input styles stay untyped JSON; the fork refuses bad values as `InvalidArg`. | No test names a bad style value. |
| FREETEXT-IDENTITY-FALLBACK | same | A font identity falls back to its registration name when PDFium has none. | `a_given_family_and_weight_are_the_identity` covers a given name, not the fallback. |
| SELECTION-NON-MARKER-LAYOUT | `rust-plugins-branch-review.md` Status, Task 13.15 | A `page.text.layout` answer that is neither a marker nor a snapshot reads as a page with no text. | No test by name or body search. |
| ANNOTATION-ICON-PLACEMENT | `rust-plugins-branch-review.md` Status, Task 13.17; `crates/plugins/annotation/README.md` | A refused icon placement is logged and the click counts as handled. | No test by name or body search. |
| FORM-PAINTER-DECODE, FORM-FILL-SURFACE-THREW, FORM-LEND-SERIALISE, FORM-ACTIVATION-NO-PAGE | `rust-plugins-branch-review.md` Status, Task 13.18; `crates/plugins/form/README.md` | Four Rust-only console lines and errors. | No test by name or body search. |
| PAGE-EDIT-INSERTED-NOT-ARRAY, PAGE-EDIT-EXTRACT-NOT-BYTES, PAGE-EDIT-NO-HANDLE, PAGE-EDIT-POSITION-NEITHER, PAGE-EDIT-HOST-BYTES-RELEASE | `rust-plugins-branch-review.md` Status, Task 13.19; `crates/plugins/page-edit/README.md` | Five malformed-answer and no-handle edges. | No test by name or body search. |
| SEARCH-INTEGER-HIT-INDEX | `rust-plugins-branch-review.md` Status, Task 13.20; `crates/plugins/search/README.md` | `goToHit` takes an integer, so `goToHit(1.5)` cannot be expressed. | A type, not a behavior: nothing to call. |
| LINK-CONTEXT-PAGE, LINK-LABEL-NON-STRING, LINK-LIST-IDENTITY, LINK-MALFORMED-RECORDS | `rust-plugins-branch-review.md` Status, Task 13.21; `crates/plugins/link/README.md` | Four link edges. | No test by name or body search. |
| MEASUREMENT-UNIT-LOOKUP, MEASUREMENT-UNIT-TYPEERROR, MEASUREMENT-READOUT-PER-CALL, MEASUREMENT-CALIBRATION-PAGE | `rust-plugins-branch-review.md` Status, Task 13.22; `crates/plugins/measurement/README.md` | Four measurement edges. | No test by name or body search. |
| REDACTION-UNDECODABLE-PAGE, REDACTION-APPLY-KEYS-AT-CALL | `rust-plugins-branch-review.md` Status, Task 13.23; `crates/plugins/redaction/README.md` | Two redaction edges. | No test by name or body search. |
| STAMP-ENGINE-HANDLE, STAMP-HOST-RENDER-RELEASE, STAMP-PAGE-REF-TYPEERROR, STAMP-SIBLING-LOOKUP, STAMP-PROTOTYPE-IDS | `rust-plugins-branch-review.md` Status, Task 13.24; `crates/plugins/stamp/README.md` | Five stamp edges. | No test by name or body search. |
| SIGNATURE-SETTINGS-IDENTITY, SIGNATURE-STRICT-EQ, SIGNATURE-SEAL-FORWARDS, SIGNATURE-IMPORT-TURN | `rust-plugins-branch-review.md` Status, Task 13.25; `crates/plugins/signature/README.md` | Four signature edges. | No test by name or body search. |
| SIGCORE-MEMBER-OVERRUN, SIGCORE-EXTENSION-WALK, SIGCORE-TIMESTAMP-TOKEN, SIGCORE-SIGNING-TIME | `rust-plugins-branch-review.md` Status, Task 13.9; `crates/plugins/signature-core/README.md` | Four reads that still differ from pkijs. | `asn1js_parity.rs` pins the walk, not these reads. |
| METADATA-NO-HANDLE-LOAD, METADATA-NO-HANDLE-WRITES | `rust-plugins-branch-review.md` Status, Task 13.26; `crates/plugins/metadata/README.md` | With no document handle a load rejects with `Error` and writes refuse `permission-denied`. | `writes_refuse_before_the_engine` covers a missing grant, not a missing handle. |
| I18N-LOCALES-BY-VALUE, I18N-TRANSLATOR-WEAK, I18N-INPUTS-OUTSIDE-TYPES | `rust-plugins-branch-review.md` Status, Task 13.27; `crates/plugins/i18n/README.md` | Three i18n edges. | No test by name or body search. |
| SHELL-PROTO-ID, SHELL-INHERITED-PROPS, SHELL-EXTRA-MEMBER, SHELL-MISSING-OPEN | `rust-plugins-branch-review.md` Status, Task 13.28; `crates/plugins/shell/README.md` | Four shell inputs not ported. | `object_prototype_names_read_as_the_typescript_reads_them` pins the converged reads, not these. |
| COMMANDS-FAMILY-COMMAND | `rust-plugins-branch-review.md` Status, Task 13.29; `crates/plugins/commands/README.md` | A family command result loses `id` and `shortcut`. | No test by name or body search. |
| VM-PROTOTYPE-PANE-IDS, VM-ORDER-WITHOUT-PANE | `rust-plugins-branch-review.md` Status, Task 13.30; `crates/plugins/view-manager/README.md` | Two view-manager edges. | No test by name or body search. |
| OUTLINE-SURROGATE-TITLE, OUTLINE-UNDECODABLE-DESTINATION | `rust-plugins-branch-review.md` Status, Task 13.31; `crates/plugins/outline/README.md` | Two outline edges. | No test by name or body search. |
| ATTACHMENT-ROW-IDENTITY, ATTACHMENT-BYTES-SPELLING, ATTACHMENT-NO-HOME, ATTACHMENT-UNDECODABLE-RECT-PAGE | `rust-plugins-branch-review.md` Status, Task 13.32; `crates/plugins/attachment/README.md` | Four attachment edges. | No test by name or body search. |
| FONTS-PROTOTYPE-FACE | `rust-plugins-branch-review.md` Status, Task 13.7; `crates/plugins/fonts/README.md` | `toString`, `__proto__` and `constructor` are unknown face names. | A `core/fonts.json` case would show it, but the ledger applies to scenario cases only. |

## Unresolved

| Id | Behavior | Record |
| --- | --- | --- |
| A4ED-SCOPES | `a4ed2b228` enforces document scopes natively and keeps a locked document open until unlocked. | `multiplatform-upstream-catchup.md` Task 8.7 says the user decides it at Task 14. No record of that decision is in the repo; Task 14.3 converged parts of it (native create and import checks, parked locked opens). |
| FORM-FIELD-AFTER-LIST-DEFAULT | The engine `form_field_after` refuses a list default on a text field, which `formFieldAfter` writes as given. | `rust-plugins-branch-review.md` Status, Task 14.3: "stays out of the form crate's cross-check pending a decision". No decision is recorded. |
| TEXT-FONTS-PORT | The Rust annotation plugin lacks a port of `read/text-fonts.ts`. | `docs/research/upstream-catchup-2026-11-07/decisions.md` ("pa") calls it a divergence until a shared text crate exists. It reads as a missing port, not an accepted divergence. |

## Not ledgered: converged or not a divergence

| Id | Disposition |
| --- | --- |
| H3 | Not a divergence: the approved TypeScript change was superseded by upstream (`multiplatform-upstream-catchup.md` Decisions, item 1); the `reconcile/h3-*` cases replay with Rust and TypeScript equal. |
| M14 | Not a divergence: TypeScript changed (`0bef57fe5`, `5462de7d9`, `253bca66b`); `actions-script.json` `script-print-obeys-the-print-policy` replays equal. |
| STAGE-CLOSE-CANCEL | Converged: upstream PR #841 (`packages/plugin/stage/CHANGELOG.md`) cancels the stage frames at close in the TypeScript too. |
| EVENTS-DEST-INDEX | Obsolete: `destIndex` is gone with the neighbour positions (`1233d1349`); `crates/epdf-engine/tests/document_events.rs` asserts its absence. |
| MOVE-PAGES-END | Obsolete: `rust-plugins-branch-review.md` Status, Task 13.19 removed the ffi `move_pages_to_end` index; `'end'` is a position the engine resolves. |
| FLATTEN-DOUBLE-BUMP | Not a divergence: `annotations.flatten` bumps the sequence twice, copying the TypeScript (`multiplatform-upstream-catchup.md` Task 8.17). |
| NOOP-APPLY-EFFECTS | Obsolete: `forms.applyEffects` is gone (`multiplatform-upstream-catchup.md` Task 8.20). The flatten and redaction halves are NOOP-MUTATION-SEQUENCE. |
| d8027c034 | Converged: the absent-measure refusal matches upstream per Task 8.25 (`multiplatform-upstream-catchup.md` Task 13.22 checklist). |
| M26 | Not ledgered: `rust-plugins-branch-review.md` Status says `empty-width-ladder`, `empty-tile-ladder` and `empty-lattice-tile-scales` replay it, and they replay equal. |
| M36, M37 | Closed: `rust-plugins-branch-review.md` Status, Task 14.5 converged the form identity memos and the settings-table retention. |
| Native security state | Converged: `rust-plugins-branch-review.md` Status, Task 14.3. |
| Kernel page-space cache | `crates/epdf-kernel/README.md` "Deliberate differences": nothing can observe it. |
