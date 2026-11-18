# CU-C5 decisions

## CU-C5-D1. Command types stay in `epdf-plugin-commands`; each owner reaches them through a `commands` feature

Decision: `CommandDef`, `CommandFamily`, `CommandCtx` and `IconAccent` stay in
`epdf-plugin-commands`. Each owner crate (`history`, `interaction`, `annotation`, `selection`,
`render`, `stage`) has its list in `src/commands.rs` behind a `commands` feature, on by default, that
depends on `epdf-plugin-commands` with `default-features = false`. The documents' list
(`create_document_commands`, upstream `core/main/src/commands.ts`) lives in
`epdf-plugin-commands/src/document.rs`.
Reason: upstream moved the types to `@embedpdf/core` (`d6325f5ba`) so a plugin ships commands
without depending on the commands plugin. The Rust types can't follow: `CommandCtx` wraps the
kernel's `PluginContext`, but `LabelParamsFn` returns `epdf_plugin_i18n::Params`, and the kernel
crate depends on no plugin. Nothing in `epdf-plugin-commands` depends on an owner, so the owners'
dependency on it makes no cycle.
Cost: an owner that turns `commands` on builds the commands plugin, i18n, shell and ui-core with
it. `epdf-viewer-host` uses `default-features = false`, so it has no list until CU-C8 turns the
feature on. The dependency direction differs from upstream's `check:plugin-boundaries` rule.
Reverse: move the command types into a crate below the plugins (with `Params`) and drop the
feature.

## CU-C5-D2. `createStandardCommands` is not ported

Decision: there is no Rust `create_standard_commands`.
Reason: upstream keeps it as a deprecated join of the 3.2 lists (`standard.ts`), and the Rust port
never had it: `standard.ts` was unloaded at `cf7fdbe7a` too. A join in `epdf-plugin-commands` would
make it depend on every owner, the cycle D1 avoids. A host joins the lists it wants, as upstream's
framework packages do with `standardCommands`.
Cost: the native join (seven lists, counting history, as upstream's framework `standardCommands`)
lives in `epdf-viewer-host`, CU-C8's work.
Reverse: add the join in a crate above the owners.

## CU-C5-D3. `createTestCommandContext` is not mirrored

Decision: no Rust counterpart of `createTestCommandContext` (`core/main/src/testing.ts:241-257`).
Its 17 lines stay uncovered.
Reason: it builds a context over stand-in capabilities. Rust capabilities are concrete types that
`CommandCtx::get` downcasts to, so a stand-in can't take their place. The Rust cases run the lists
next to the real plugins instead (`plugins/commands/lists.json`).
Cost: the lists' TS unit tests (`test/commands.test.ts` in each owner) have no line-for-line Rust
twin; the scenarios and two name tests cover the same branches except the ones listed in the report.
Reverse: none needed unless the kernel grows capability stand-ins.

## CU-C5-D4. The host's part of the lists is a trait per owner

Decision: `DocumentCommandsPlatform::save_file(bytes: Value, file_name, content_type)`,
`SelectionCommandsPlatform::copy_selection(&SelectionCapability) -> Promise<Value>`, and
`RenderCommandsPlatform` with `copy_image(Promise<PageImage>) -> Promise<Value>`,
`image_bytes(PageImage) -> Promise<Value>` and `save_file(bytes: Value, file_name, content_type)`.
Reason: upstream's `snapshot:save` reads `(await snapshot.image).blob()`, a browser `Blob`. The
Rust `PageImage` is a handle the host reads pixels from, so the encoding step is the host's
`image_bytes`, taken at the same point: after the picture, before `listPages`. A rejected
`image_bytes` fails the command before `save_file`, as a `blob()` that rejects does upstream. File
name, content type and page number match upstream; `snapshot-commands` in
`plugins/commands/lists.json` pins them (fix round 1).
Cost: the render platform has three methods where upstream's has two.
Reverse: give `PageImage` a PNG byte read and drop `image_bytes`.

## CU-C5-D5. The i18n tables also gain the snapshot and marquee-zoom strings

Decision: the regenerated tables carry `commands.snapshot.{copy,save,clear}` and
`commands.tool.{zoom,snapshot}` besides `commands.history.{undo,redo}`.
Reason: the tables are the TS modules printed (`scripts/cu-c5/print-strings.mts`). Upstream added
those keys before this range (`94fbac3f4`, `a1ee5683f`), and the tables had missed them because no
vector read them. Printing only the history keys would mean editing the output by hand.
Cost: none known; the keys are data.
Reverse: not applicable.
