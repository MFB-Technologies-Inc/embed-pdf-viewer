# CU-C5: commands, history and i18n

`research/catchup-2026-11-18-c5` ports CU-C3's work order section CU-C5 (#1091, #1089) onto
`aa7b9967f`. Items 1, 2, 3 and 5 are done; item 4 is not mirrored, by decision (CU-C5-D3). The 7
`canUse` replay cases pass, and `cargo test --workspace` fails 2 tests, both CU-C6's. Every owner
now ships its command list from Rust, and `plugins/commands/lists.json` (9 scenarios) replays them.
Decisions are in [`../decisions/CU-C5.md`](../decisions/CU-C5.md).

## Commits

| Commit          | Items | Summary                                                                                                              |
| --------------- | ----- | -------------------------------------------------------------------------------------------------------------------- |
| `5758bd17e`     | 1     | `Tool.can_use`, `InteractionCapability::can_use_tool`, the FFI method; annotation hub tools ask `annotations:create` |
| `fc9646de6`     | 3     | The 8 i18n tables printed from the TS again (`scripts/cu-c5/print-strings.mts`)                                      |
| `04fd30469`     | 2, 3  | The owners' command lists, `create_document_commands`, the `command-lists` suite and its Rust replay                 |
| `701e3332e`     | 1     | The interaction harness lists `canUseTool` among the capability's members                                            |
| `f982614a0`     | 2     | A case that runs `document:download`                                                                                 |
| the docs commit | 5     | `coverage.json`, this report, the decisions, the job's scripts                                                       |

## Items

1. Done (`5758bd17e`, `701e3332e`), after `d6325f5ba`. The annotation plugin registers both kinds of
   hub tool (`connect.rs` and `tools/registry.rs`) with a `can_use` that answers `can_create()`, the
   same `annotations:create` check upstream's `annotation.canCreate()` and
   `ctx.allows('annotations:create')` make. The interaction harness reads a vector tool's `canUse`,
   prints it, and registers the `canUse` of a tool a capability method returns as a `$ref`, as
   `canonical.ts` does. The 7 cases pass: `annotation-tools` `init-registers-the-tools`,
   `init-with-selection`, `handlers-outrank-a-lower-handler`, `fixed-bytes-stamp-tool`,
   `register-over-a-tool-and-take-it-back`; `form-design` `init-with-annotation`; `interaction`
   `defaults`.
2. Done (`04fd30469`, `f982614a0`), after `d6325f5ba` and `2e65f29e8`. Each owner has
   `src/commands.rs` behind a `commands` feature (CU-C5-D1). `createStandardCommands` is not ported
   (CU-C5-D2). The host's part is a trait per owner (CU-C5-D4).
3. Done (`fc9646de6`, `04fd30469`), after `33a7a17d0`. `history:undo` (`Mod+Z`) and `history:redo`
   (`Mod+Shift+Z`, `Mod+Y`). The tables also gained 5 keys from before this range (CU-C5-D5). The
   case `history-labels-in-every-language` reads both keys in all 8 languages, through `t()` and
   through the resolved labels.
4. Not mirrored (CU-C5-D3): Rust capabilities are concrete types, so no stand-in context exists.
5. Done. `coverage.json` regenerated; the evidence pins needed no change.

## Rust API for CU-C8 and CU-C9

Each owner's list needs its crate's `commands` feature, off in `epdf-viewer-host` today.

- `epdf_plugin_interaction`: `InteractionCapability::can_use_tool(&str) -> bool`,
  `InteractionHandle::can_use_tool(id: String)` (uniffi), `Tool::can_use: Option<ToolCanUse>`;
  `commands::create_interaction_commands() -> Vec<CommandItem>`, `commands::tool_command(&str)`.
- `epdf_plugin_history::commands::create_history_commands() -> Vec<CommandDef>`.
- `epdf_plugin_annotation::commands::create_annotation_commands()`.
- `epdf_plugin_stage::commands::create_stage_commands()`.
- `epdf_plugin_selection::commands::{create_selection_commands, SelectionCommandsPlatform}`.
- `epdf_plugin_render::commands::{create_render_commands, RenderCommandsPlatform}`.
- `epdf_plugin_commands::{create_document_commands, DocumentCommandsPlatform}`.

The uniffi `register_tool` record has no `can_use`, so a native host can't give its own tool a
permission yet.

## Tests

All in container `embed-pdf-catchup1118`, worktree `/workspaces/wt/CU-C5`,
`CARGO_TARGET_DIR=/workspaces/target`. The base column is CU-C4b's final run at `aa7b9967f`
(`/workspaces/logs/CU-C4b/after/`).

| Check                                                                 | Base (`aa7b9967f`)                | Now (`f982614a0`)                 |
| --------------------------------------------------------------------- | --------------------------------- | --------------------------------- |
| `cargo test --workspace --no-fail-fast`                               | 3,502 passed, 4 failed, 2 ignored | 3,507 passed, 2 failed, 2 ignored |
| engine `:check` scripts                                               | 36 of 36                          | 36 of 36                          |
| plugin vectors twice                                                  | identical                         | identical sha256 lists            |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | all pass                          | all pass                          |
| plugin `replay:check`                                                 | 13 cases in 4 files               | 6 cases in 2 files                |
| TS `turbo run test` over `packages/**`                                | exit 0                            | exit 0                            |
| uncovered added lines in the files this job touches (`gaps.py`)       | 20 lines, 9 files unloaded        | 42 lines, 2 files unloaded        |

The 2 failures are CU-C6's: `epdf-plugin-annotation` `tools_match_the_typescript`
(`ink-strokes-group`, `ink-cancel-inside-the-grouping-window`) and `epdf-plugin-signature`
`the_plugin_matches_the_typescript` (4 cases). No new failing name.

Left uncovered: `core/main/src/testing.ts` 241-257 (CU-C5-D3); `core/main/src/commands.ts` 51-53,
the `document:print` run, which needs a host printer the fake engine lacks;
`plugin/annotation/src/commands.ts` 26-29, `annotation:delete` with the annotation plugin present;
`plugin/render/src/commands.ts` 50-52, 62-72 and 88-91, the snapshot commands with a snapshot held.
The two file-name helpers have Rust unit tests (`commands/src/document.rs`,
`render/src/commands.rs`). Unloaded: `command-types.ts` (types only) and `standard.ts` (CU-C5-D2).

## Divergences

No behaviour divergence is known. Shape differences, each in the decisions: the command types'
crate (D1), no standard join (D2), and `save_image` beside `save_file` (D4).

## Evidence

Base `aa7b9967f`. Logs in `/workspaces/logs/CU-C5/` and `tmp/cu1118/logs/CU-C5/` of the host viewer
checkout; the first full run, before `701e3332e`, is in `round1/`. Scripts in
[`../scripts/cu-c5/`](../scripts/cu-c5/): `prep.sh` (CU-C4b's payload copied in), `after.sh`,
`suite.sh`, `env.sh`, `print-strings.mts`.
