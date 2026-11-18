# CU-C2: impl merge of upstream `c4e744345`

`research/catchup-2026-11-18-impl` merges upstream main `c4e744345` into impl `a7c75f5ad`, with
`runtime-src` at the runtime fork merge `af480d0ce`. Two paths conflicted, `runtime-src` and
`pnpm-lock.yaml`, as the plan's probe said. The generators typecheck against the new TS without
changes. Three vector files changed on regeneration, all from one upstream constant
(`policyVersion` 4 to 5), and two runs were byte-identical. The cargo failure list is the same 117
names before and after, with the same first message line for each (D-03). Decisions are in
[`../decisions/CU-C2.md`](../decisions/CU-C2.md).

## Commits

| Commit | Summary |
|---|---|
| `66d6f778e` | Merge upstream main `c4e744345`; `runtime-src` set to `af480d0ce`; lockfile re-resolved |
| `7a23f52aa` | Regenerated vectors: `policyVersion` 4 to 5 in `forms/field.json`, `forms/write.json`, `pages/mutate.json` |
| this commit | CU-C2 report, decisions and the job's scripts |

CU-C1's record branch is not merged here: it sits on plugins (CU-C2-D1).

## Merge

`git merge --no-ff c4e744345` on `a7c75f5ad`, 66 upstream commits since `ed064e0c8`. Two conflicts:

| Path | Resolution |
|---|---|
| `packages/engine/runtime/runtime-src` | `af480d0ce420ed916db0de8d2e8827822ad7be1e` (CU-C1's merge of upstream runtime `f2ea75972` into `17d8073d4`), set with `git update-index` (CU-C2-D2) |
| `pnpm-lock.yaml` | Upstream's, then `pnpm install --no-frozen-lockfile`. The fork's four tooling importers come back; Playwright re-resolves to 1.65.1 (CU-C2-D3) |

`.gitmodules` is impl's. `engine-runtime-build.json` is upstream's, release
`runtime-f2ea7597287f26bdcb450602de6616ff6939d506`; prep fetched its `linux-arm64` and `wasm32`
archives. Two files merged cleanly with changes from both sides:
`packages/engine/services/src/worker-host/WorkerHost.ts` and `tooling/consume-fixtures/src/run.ts`.
For each, the sorted added and removed lines of `git diff c4e744345` after the merge hash the same
as those of `git diff ed064e0c8 a7c75f5ad`. The set of files differing from upstream after the
merge is the set impl changed against `ed064e0c8` (`CU-C2-clean-merge-hunks.log`).

Checks on `66d6f778e`: `git merge-base --is-ancestor` exits 0 for both `c4e744345` and `a7c75f5ad`,
and `git ls-tree HEAD packages/engine/runtime/runtime-src` prints `af480d0ce…`.

## Engine conformance

`pnpm -s run typecheck` in `tooling/engine-conformance` exits 0 at `66d6f778e`.

The regeneration ran twice on `66d6f778e`, after a prep that built `wasm32` from the `f2ea75972`
library. Both runs printed the same log, and the sha256 lists of all 51 files under `vectors/` are
identical (`deterministic=yes`). 30 of 31 groups ran; `wire` fails before writing (CU-C2-D8).

| Groups | Result |
|---|---|
| `form-field`, `form-write`, `pages-mutate` | `request.authority.protection.policyVersion` 4 to 5 in 19, 18 and 24 cases; nothing else changed |
| the other 27 regenerable groups | byte-identical to impl |
| `wire` | not regenerated: "expected 89 request kinds, the union has 96" (94 at the baseline) |

The change traces to upstream `462380c7a` (PR #1088), which raised `SIGNATURE_POLICY_VERSION` in
`packages/engine/core/src/signature/protection.ts` from 4 to 5 (CU-C2-D7). The upstream signature,
command and stamp work in this range has no engine vector case on impl. Adding cases is port work.

## Tests

All in container `embed-pdf-catchup1118`, with `suite.sh`. Baseline at `a7c75f5ad`, after at
`7a23f52aa`.

| Check | Baseline `a7c75f5ad` | After `7a23f52aa` |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 550 passed, 117 failed | 550 passed, 117 failed (clean build, CU-C2-D6) |
| cargo failing names (`cargo.failed` sha256) | `898127a7…` | `898127a7…` |
| first failure message per test (`cargo.panics` sha256) | `65a74a4f…` | `65a74a4f…` |
| engine `:check` scripts | 30 of 32; `operations:check`, `wire:check` fail | 30 of 32; the same two fail (CU-C2-D8) |
| TS `turbo run test` over `packages/**` | 97 of 98 tasks; `@embedpdf/viewer` 583 of 585 | 98 of 98 tasks; `@embedpdf/viewer` 595 of 595 |

The cargo failure list is byte-identical to the 2026-11-12 one (also `898127a7…`), so
[`../../upstream-catchup-2026-11-12/results/CU-C2-cargo-failures.md`](../../upstream-catchup-2026-11-12/results/CU-C2-cargo-failures.md)
still lists every name and its cause. No Rust test reads `policyVersion`.

The two baseline TS failures are `test/fullscreen.test.ts > full screen > keeps a dialog open in
the viewer over it when the page makes it full screen` and `test/small-screens.test.ts > small
screens > slides in with its layer still and the whole viewer dimmed behind it` (CU-C2-D9). The
after run had 52 turbo cache hits, for packages whose inputs the merge didn't change.

## Evidence

Base `a7c75f5adf101c096cba894074991c989a9d4ffc`; upstream
`c4e744345a4d81e655f75ccae91e5b513592f769`; merge `66d6f778ec9d4c450dc8b419e0bf2dd209c1df3d`;
vectors `7a23f52aa315948c52d6701994d47b34dc05bb13`; head is the commit that adds this file. Logs are
in `/workspaces/logs/CU-C2/` in the container and in `tmp/cu1118/logs/CU-C2/` of the host viewer
checkout (ignored), hashed in its `SHA256SUMS`. Scripts are in [`../scripts/cu-c2/`](../scripts/cu-c2/).

| Check | Command | Exit | Log | Log sha256 |
|---|---|---|---|---|
| Baseline prep | `heavy-build.sh … bash CU-C2-prep.sh /workspaces/wt/CU-C2` | 0 | `CU-C2-prep-base.log` | `29ba8c9c…` |
| Baseline prep-extra | `bash /workspaces/prep-extra.sh /workspaces/wt/CU-C2` | 0 | `CU-C2-prep-extra-base.log` | `5eaec7ad…` |
| Baseline suite | `suite.sh /workspaces/wt/CU-C2 …/base cargo engconf ts` | 0 (script) | `CU-C2-suite-base.log`, `base/cargo.log`, `base/ts.log` | `38121e0d…`, `8e676202…`, `1023e507…` |
| Merge | `git merge --no-ff --no-commit c4e744345` | 1 (2 conflicts) | `CU-C2-merge.log` | `235edc39…` |
| Lockfile | `pnpm install --no-frozen-lockfile` | 0 | `CU-C2-merge-pnpm-install.log` | `a4769fa7…` |
| Clean-merge hunks | `git diff` line sets for both files, and the file set | 0 | `CU-C2-clean-merge-hunks.log` | `05b6402a…` |
| Merge prep | `CU-C2-prep.sh /workspaces/wt/CU-C2-merge` | 0 | `CU-C2-prep-merge.log` | `96eb3fd1…` |
| Generator typecheck | `pnpm -s run typecheck` in `tooling/engine-conformance` | 0 | `CU-C2-typecheck-merge.log` | `baf1c2ed…` |
| Regeneration, run 1 and 2 | `CU-C2-regen2x.sh /workspaces/wt/CU-C2-merge` | 0 | `CU-C2-regen-run1.log`, `CU-C2-regen-run2.log` | both `a91010c1…` |
| Determinism | `sha256sum` of every vector file after each run | 0 | `CU-C2-regen-determinism.log`, `CU-C2-regen-sha-run{1,2}.txt` | `e0d8fe32…`; lists both `6306a634…` |
| Vector diff | `git diff --stat`, `jdiff.py 66d6f778e` | 0 | `CU-C2-vector-diff.log` | |
| After prep, prep-extra | `CU-C2-prep.sh`, `prep-extra.sh` on `/workspaces/wt/CU-C2` | 0, 0 | `CU-C2-prep-after.log`, `CU-C2-prep-extra-after.log` | `4b9b60a0…`, `32e3f302…` |
| After suite | `suite.sh /workspaces/wt/CU-C2 …/after cargo engconf ts` | 0 (script) | `CU-C2-suite-after.log`, `after/cargo.log`, `after/ts.log` | `a5c2cecf…`, `0a97d599…`, `2fd0e53b…` |
| After cargo, clean build | `CARGO_TARGET_DIR=/workspaces/target CU-C2-suite.sh … cargo` | 0 (script) | `CU-C2-suite-after-cargo-fresh.log`, `after-cargo-fresh/cargo.log` | `dd5660e7…`, `b3f8641d…` |
| Failing names and messages | `diff` of `cargo.failed`; `panics.py` on both cargo logs | 0 | `base/cargo.failed`, `after-cargo-fresh/cargo.failed`, `*/cargo.panics` | `898127a7…` both; `65a74a4f…` both |

## Known gaps

- `wire:check` and `operations:check` still fail on impl, now also for the two new signature request
  kinds (CU-C2-D8).
- Playwright moved to 1.65.1 in the lockfile (CU-C2-D3).
- impl's Rust stays red on vector shape (D-03); the port is on plugins.
- `runtime-src` names `af480d0ce`, which exists only in the local runtime fork until
  `catchup/2026-11-18-runtime` is pushed (plan D-3).
