# CU-C5 decisions

Decisions made while porting #1165 (`7d5691a5a`, `bfbaac1f2`) into the Rust core.

## CU-C5-D1. `API_VERSION` goes back to 3.4

Decision: `crates/epdf-engine/src/features/changes/kinds.rs:13` reads `"3.4"` again (`28e2224cb`).
CU-C4 (`db41381cb`) had moved it to 3.5.
Reason: upstream's `API_VERSION` is `apiVersionOf(version)` over `@embedpdf/engine-core`'s package
version (`packages/engine/core/src/wire/apiVersion.ts:31`), which is 3.4.0 at `cda9e82a4`. The range
adds `ANYONE_FIELDS_SINCE` and `BUTTON_IMAGE_AND_LAYOUT_SINCE` at 3.5 ahead of a release.
`vectors/changes/kinds.json` records `"apiVersion": "3.4"`, and
`change_ops::every_change_kind_has_the_version_it_arrived_in` checks that before the kind list, so
the failure CU-C4 recorded as "the change kinds moved" was this assertion first.
Cost: until upstream releases 3.5, a 3.5 server reads this build as 3.4 and holds back what arrived
in 3.5, exactly as it treats the TypeScript engine.
Reverse: none needed; the constant follows the package version at the next catch-up.

## CU-C5-D2. The picture-button runner is 19 change-vector cases plus a Rust test file

Decision: the tests of `runPictureButtonConformance.ts` whose outcome crosses the worker boundary
in one session (layouts and `hasImage` read, layout writes and refusals, each picture format, `null`,
the shared drawing's object numbers, refusals, the undo pair, the three tokens' rights, `addWidget`
with and without a layout, its undo, the merged split, copies) are the group
`tooling/engine-conformance/vectors/changes/picture-buttons.json`, replayed by
`change_ops::a_push_button_holds_a_picture`. The generator gains a `forms.setButtonImage` and a
`forms.addWidget` step and a copy's `offset` and `values`. What a vector doesn't record (pixels, a
saved file's image count, a signing, a save and reopen, an export into another document) is
`crates/epdf-engine/tests/picture_buttons.rs`, with `picture-button-analysis.test.ts` and
`shared-drawing-size.test.ts` beside it.
Reason: CU-C4-D2 made the same split for the anyone-fields runner, for the same reason.
Cost: the Rust file restates upstream's assertions; the noisy PNG of the size test is stored rather
than deflated (no deflate crate in the test dependencies), so it is about 17 KB by another route.
The vector case `it-takes-a-push-button-and-a-picture` records no exchange for GIF bytes, which
upstream refuses on the main thread (`resolvePictureBytes`); the Rust test
`bytes_that_are_no_picture_are_refused_by_the_write_too` covers the host's own refusal.
Reverse: give the generator pixel, save and sign steps.

## CU-C5-D3. The `allowsField` check is a unit test of `NativeSecurity`, not a `viewer_host.rs` test

Decision: `native_engine::tests::a_group_token_fills_in_a_field_anyone_fills_in` in
`crates/epdf-viewer-host/src/native_engine.rs` asks `NativeSecurity::allows_field("fill", …)` for
six of `runAnyoneFieldsConformance.ts`'s tokens against four targets. The controller's amendment
asked for it in `crates/epdf-viewer-host/tests/viewer_host.rs`.
Reason: no path through a viewer host reaches `NativeSecurity::allows_field` with a field's
`forAnyone`. The kernel's `PluginContext::allows_field` (`crates/epdf-kernel/src/plugin.rs:719-723`)
sends `{ groupId }` alone and memoizes by group, as upstream's plugin context does
(`packages/core/main/src/context.ts:264`). A `viewer_host.rs` test through the form plugin's
`can_fill` reads `false` for Dana's token on `reference`, because the plugin never asks with
`forAnyone`. That is upstream's behaviour, so the Rust kernel keeps it.
Cost: through the form plugin, on both upstream and Rust, a group's token is refused filling in a
field anyone fills in, although the engine's write would take it. This is reported as a concern
for CU-C6, not changed here (`packages/**` keeps upstream behaviour).
Reverse: once upstream's context passes the whole field, pass it in `plugin.rs` and move the test
to `viewer_host.rs`.

## CU-C5-D4. The change op's wire resource keys and placement schema are not ported

Decision: `pictureButton.test.ts`'s three schema tests (`ChangeOpWireSchema` with
`resources: { image }`, `WidgetPlacementSchema` with a `layout`, `AnnotationSchema` reading an older
row) have no Rust port. The op reaches the Rust engine as `{ field, image: { payload } | null }`,
the worker shape `Change.ts` sends.
Reason: the resource-key form is the cloud server's multipart contract; the Rust engine has no
`ChangeOpWireSchema` of its own, and the wire vectors (`wire:check`) already compare the schema
dumps. The older-row reads have Rust tests: `snapshot::tests::a_push_button_without_has_image_holds_none`
and CU-C4's `a_file_from_before_fields_anyone_fills_in_reads_for_anyone_false_and_one_is_kept`.
Cost: none beyond the three tests.
Reverse: port them if the Rust engine gains a server route.
