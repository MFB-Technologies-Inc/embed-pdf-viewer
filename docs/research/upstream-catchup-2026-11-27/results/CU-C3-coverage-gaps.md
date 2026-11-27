# CU-C3 coverage gaps

The lines upstream added in `c4e744345..9af4d9ee7` that no plugin vector reaches, read from
`tooling/plugin-conformance/vectors/coverage.json` at `8ea991f23` with `scripts/cu-c3/gaps.py`.
Each line names the upstream commits that last touched the uncovered lines (`git blame` at
`9af4d9ee7`; `07ce37d2a` is the merge of main into #1141's branch). "Unloaded" files are ported
source files no generator imports, so c8 reports nothing for them. No cases were added here
(CU-C3-D10); the job column says who adds them.

This is plugin-side coverage only. The engine packages (`packages/engine/**`) are not measured by
c8 here; their new behaviour is listed as engine cases in the work order (CU-C4 item 9).

| Package | Uncovered added lines | What | Job |
|---|---|---|---|
| `plugin/form` | 236 in 12 files, plus `state.ts` unloaded | groups and design group (`services/groups.ts`, `write/groups.ts`, `read/settings.ts` tint, `read/fields.ts`, `services/events.ts`), Tab order over radio stops and unsignable signature fields (`services/cursor.ts`, `write/focus.ts`, `services/tab-order.ts`), the control-attach API (`read/widgets.ts`), turned and grouped writes (`write/fields.ts`), `write/fill.ts` | D |
| `core/acrojs` | 21 (`dates.ts`, the host date parse) | `833b40015` (#1147) | D |
| `core/annotation` | 16 (`record/shape.ts` widget turn, `props.ts`, `update/selection-edits.ts` turned selection), plus `types.ts` unloaded | `23741e3fd`, `ccfcaf0fc` (#1118) | E |
| `plugin/annotation` | 4 (`read/view.ts`, a lent record's group accent), plus `index.ts` unloaded | `d6d7c25e2` (#1141) | E |

## gaps.py output

```
# 277 uncovered lines that c4e744345..9af4d9ee7 added, in 17 files
## core/acrojs (21)
- packages/core/acrojs/src/dates.ts: lines 40-47,57-63,81-86 (21; 833b40015)
## core/annotation (16)
- packages/core/annotation/src/props.ts: lines 94 (1; 23741e3fd)
- packages/core/annotation/src/record/shape.ts: lines 47-56 (10; 23741e3fd)
- packages/core/annotation/src/update/selection-edits.ts: lines 220-224 (5; ccfcaf0fc)
## plugin/annotation (4)
- packages/plugin/annotation/src/read/view.ts: lines 137-140 (4; d6d7c25e2)
## plugin/form (236)
- packages/plugin/form/src/model.ts: lines 487,571-578,582-583 (11; 23741e3fd d6d7c25e2)
- packages/plugin/form/src/read/fields.ts: lines 224-231 (8; d6d7c25e2)
- packages/plugin/form/src/read/settings.ts: lines 116-134,156-160 (24; d6d7c25e2)
- packages/plugin/form/src/read/widgets.ts: lines 107-115 (9; 869997aa0)
- packages/plugin/form/src/services/cursor.ts: lines 56,106,116-125,129-131,156-158,162,164-180,185 (37; 71196cfa6 cb379ad33)
- packages/plugin/form/src/services/events.ts: lines 34-35 (2; d6d7c25e2)
- packages/plugin/form/src/services/groups.ts: lines 99-103,112-122,131,138-144 (24; d6d7c25e2)
- packages/plugin/form/src/services/tab-order.ts: lines 47-49,53-56 (7; cbdb46811)
- packages/plugin/form/src/write/fields.ts: lines 171-172,283-316,332,334-335 (39; 07ce37d2a 23741e3fd d6d7c25e2)
- packages/plugin/form/src/write/fill.ts: lines 147,162 (2; fce760aeb)
- packages/plugin/form/src/write/focus.ts: lines 112,116-117,119-125,134-138,143-151,154-156,158-174,177-188,199 (57; 4a5479b4b 71196cfa6 cb379ad33)
- packages/plugin/form/src/write/groups.ts: lines 12-22,29-30,33-35 (16; d6d7c25e2)
## unloaded files upstream touched (3)
- packages/core/annotation/src/types.ts
- packages/plugin/annotation/src/index.ts
- packages/plugin/form/src/state.ts
```
