# CU-C3 coverage gaps

The lines upstream added in `ed064e0c8..c4e744345` that no plugin vector reaches, read from
`tooling/plugin-conformance/vectors/coverage.json` at `98a477901` with `scripts/cu-c3/gaps.py`.
Each line names the upstream commits that last touched the uncovered lines (`git blame` at
`c4e744345`). "Unloaded" files are ported source files no generator imports at all, so c8 reports
nothing for them. No cases were added here (CU-C3-D6); the job column says who adds them.

| Package | Uncovered added lines | Job |
|---|---|---|
| `core/signature` | 2,148 in 24 files, plus 5 unloaded files | CU-C4 (timestamps, long-term validation, revocation, test authority); `server/*` stays unported (D-2), its route request format gets cases in CU-C4 |
| `core/main` | 17 (`testing.ts`, `createTestCommandContext`), plus `command-types.ts` and `commands.ts` unloaded | CU-C5 |
| `plugin/signature` | 88 (`write/validation-info.ts`, `services/authority.ts`), plus `index.ts` unloaded | CU-C6 |
| `plugin/form` | 8 (`write/fill.ts`, `134f24b77`) | CU-C6 |
| `plugin/interaction` | 3 (`controller.ts`, `canUseTool`), plus `commands.ts` unloaded | CU-C5 |
| `plugin/annotation`, `history`, `render`, `selection`, `stage`, `commands` | each plugin's new `commands.ts` unloaded; `commands/src/standard.ts` was unloaded at `cf7fdbe7a` too | CU-C5 |

Two more gaps don't show as uncovered lines because the code is data or already reached: the i18n
strings `commands.history.undo` and `commands.history.redo` (#1089, 8 locales) are in no vector, and
`ImportLibraryOptions.id` (#1039, `packages/plugin/stamp/src/write/libraries.ts`) runs in existing
cases without any case passing an `id`. CU-C5 and CU-C6 add those cases.

## gaps.py output

```
# 2264 uncovered lines that ed064e0c8..c4e744345 added, in 29 files
## core/main (17)
- packages/core/main/src/testing.ts: lines 241-257 (17; d6325f5ba)
## core/signature (2148)
- packages/core/signature/src/cms/attach.ts: lines 38-83,90-108,112-116,119-130,132-136 (87; 6c2640bd0 a94048554)
- packages/core/signature/src/cms/parse.ts: lines 283-296,316-319,363 (19; 30b269418 85bfd9342)
- packages/core/signature/src/cms/splice.ts: lines 29-74,78-85,88-93,95-103,107-108,111-116,119-134,160-209 (143; 15955a843 a94048554)
- packages/core/signature/src/cms/timestamp.ts: lines 49-50,56-59,94,145,148,152-156,160 (15; 15955a843 5ee481a3c 6d6e76d3c f474edfca)
- packages/core/signature/src/cms/verify-timestamp.ts: lines 87-185 (99; 30b269418 4d7588274)
- packages/core/signature/src/collect.ts: lines 67-161,164-187,190-213 (143; 0928d2d99)
- packages/core/signature/src/gate.ts: lines 112-124,132,142,146-173 (43; 30b269418 a94048554)
- packages/core/signature/src/judgement.ts: lines 76-100,107-116,123-125,130,243-247,272,276-277,306-313,321-361,364-429,431-433,439-441 (168; 0928d2d99 a425da7d3)
- packages/core/signature/src/long-term.ts: lines 139-150,153-181 (41; 0928d2d99 3aa832da5 a425da7d3)
- packages/core/signature/src/revocation/addresses.ts: lines 24-47,76-79,83-86 (32; 85bfd9342 d5ad0a8d1)
- packages/core/signature/src/revocation/answer.ts: lines 97-120,132-241,244-259,261-263,269-295,297-369,379-384,387-393,396-401,403-423 (293; 85bfd9342)
- packages/core/signature/src/revocation/fill.ts: lines 46-184,191-215,218-235 (182; 0928d2d99)
- packages/core/signature/src/revocation/http-source.ts: lines 48-125,128-138,141-144,146-150 (98; 0928d2d99)
- packages/core/signature/src/revocation/pool.ts: lines 53-55,63-70,91-92,113-114,182-186 (20; 0928d2d99 3aa832da5 7bd135d7f)
- packages/core/signature/src/revocation/request.ts: lines 13-24 (12; 85bfd9342)
- packages/core/signature/src/revocation/status.ts: lines 40-52 (13; 85bfd9342)
- packages/core/signature/src/sign.ts: lines 115,168,178-183,185-190,216-223,225,227-228,230 (26; 6c2640bd0 a94048554)
- packages/core/signature/src/signers/ecdsa.ts: lines 16-42 (27; a94048554)
- packages/core/signature/src/signers/self-signed.ts: lines 30-39,68,80-89 (21; 2b187fef0)
- packages/core/signature/src/signers/test-authority.ts: lines 47-59,73-198,211-288,291-341,378-516 (407; 30c4af6da 6d6e76d3c 85bfd9342 8a143eecf a841027e9 d5ad0a8d1)
- packages/core/signature/src/signers/timestamp-http.ts: lines 47-195 (149; 5ee481a3c 6d6e76d3c)
- packages/core/signature/src/signers/webcrypto.ts: lines 127-131,133-137,142 (11; 2b187fef0)
- packages/core/signature/src/trust.ts: lines 131-178,197-199,205-223,275-279 (75; 30b269418)
- packages/core/signature/src/verdict.ts: lines 175-182,215-216,248-261 (24; 0928d2d99 3aa832da5 4d7588274)
## plugin/form (8)
- packages/plugin/form/src/write/fill.ts: lines 152-156,171-173 (8; 134f24b77)
## plugin/interaction (3)
- packages/plugin/interaction/src/controller.ts: lines 218-220 (3; d6325f5ba)
## plugin/signature (88)
- packages/plugin/signature/src/services/authority.ts: lines 63-65 (3; 1c165508c)
- packages/plugin/signature/src/write/validation-info.ts: lines 34-51,60-96,99-118,132-141 (85; 1c165508c a425da7d3)
## unloaded files upstream touched (16)
- packages/core/main/src/command-types.ts
- packages/core/main/src/commands.ts
- packages/core/signature/src/cms/timestamp-protocol.ts
- packages/core/signature/src/revocation/source.ts
- packages/core/signature/src/server.ts
- packages/core/signature/src/server/public-address.ts
- packages/core/signature/src/server/revocation-route.ts
- packages/core/signature/src/server/timestamp-route.ts
- packages/plugin/annotation/src/commands.ts
- packages/plugin/commands/src/standard.ts
- packages/plugin/history/src/commands.ts
- packages/plugin/interaction/src/commands.ts
- packages/plugin/render/src/commands.ts
- packages/plugin/selection/src/commands.ts
- packages/plugin/signature/src/index.ts
- packages/plugin/stage/src/commands.ts
```
