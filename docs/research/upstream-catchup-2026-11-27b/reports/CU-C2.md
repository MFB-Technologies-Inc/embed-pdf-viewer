# CU-C2 (with CU-C0): upstream merge, container, start state

`research/catchup-2026-11-27b-plugins` merges upstream main `cda9e82a4` into plugins `0afd87825`,
with `runtime-src` kept at `9472117ed`. The merge had no conflicts, as the `git merge-tree` probe
said, and needed no follow-up commit: the range touches no `crates/`, `platforms/` or `tooling/`
file and no lockfile or `package.json`. `cargo build --workspace --locked` is green and
`cargo test --workspace` passes in full at both commits: 3,630 passed, 0 failed, 2 ignored. The
breakage is on the vector side, as expected: 10 of 36 engine `:check` scripts and 3 of 4 plugin
checks fail at the merge, against 36 of 36 and 4 of 4 at the baseline. Decisions are in
[`../decisions/CU-C2.md`](../decisions/CU-C2.md).

## Commits

| Commit | Summary |
|---|---|
| `e744a671a` | The plan, cherry-picked from `7515755d0` on `research/catchup-2026-11-27b-plan` |
| `f26ea0593` | The plan's rulings, cherry-picked from `2e7360415` |
| `117945420` | Merge `upstream/cda9e82a4-tmp` (= `cda9e82a4`); `runtime-src` stays `9472117ed` |
| the commit that adds this file | CU-C2 report, decisions, `decisions.md` and scripts |

All on `research/catchup-2026-11-27b-plugins`, made on the host in
`/Users/andrew/repos/embed-pdf-viewer/.claude/worktrees/catchup-1127b`. No commit was made in the
container, so no bundle came back.

## Merge

On the host, after `git submodule update --init --reference /Users/andrew/repos/embed-pdf-runtime`
for `runtime-src`:

```
scripts/sync-upstream.sh --from upstream/cda9e82a4-tmp --runtime-commit 9472117ed
```

`upstream/cda9e82a4-tmp` is a local branch at `cda9e82a4`. The fork's
`research/multi-platform-rust-core` on GitHub is already at `9472117ed`, so the script reported
"already has runtime 9472117ed" and made no runtime merge (CU-C2-D2). The viewer merge went through
with no conflict and the script committed it. It ran without `--push`. Its closing note says
payload `4f2cb5fbc177da69` is not published; that is CU-C10's business, not this job's.

The merge brings 198 files, 11,555 insertions and 954 deletions, the same as
`git diff --stat 9af4d9ee7 cda9e82a4`. `git merge-base 0afd87825 cda9e82a4` is `9af4d9ee7`, the
previous target, 14 commits back. The lockfile is unchanged (CU-C2-D1).

## CU-C0: image, container, history

No image rebuild. `embedpdf-dev:latest` is `a3b58f11b8e0`, built `2026-11-27T08:30:12Z` from
`d670a1cbfb1064026012b53860dd75499fc6df46` (label `org.opencontainers.image.revision`), and
`python3 .devcontainer/check-pins.py` exits 0 in both `/workspaces/wt/base` and `/workspaces/wt/CU-C2`
("Development image pins match the repository."). `git diff --stat d670a1cbf 0afd87825 --
.devcontainer` is empty (CU-C2-D4).

The host clone is not shallow (`git rev-parse --is-shallow-repository` is `false`) and holds
`9af4d9ee7`; so does the container clone (3,457 commits reachable from it).

Container `embed-pdf-catchup1127b`, image `embedpdf-dev:latest` (`a3b58f11b8e0`), volume
`embedpdf-catchup1127b` at `/workspaces`, `--init`, `sleep infinity`, user `node`, `NPM_TOKEN=""`.
Git identity `Andrew Roan <roanutil@users.noreply.github.com>` is set globally. Use
`docker exec embed-pdf-catchup1127b bash -c '…'`.

| Path | Contents |
|---|---|
| `/workspaces/embed-pdf-viewer` | Viewer clone from a host bundle of `research/catchup-2026-11-27b-plugins` and `upstream/cda9e82a4-tmp`, detached at `0afd87825`. Remotes: `hostbundle` (the bundle, now deleted), `fork` and `upstream` on GitHub. `refs/remotes/upstream/cda9e82a4` names the target. Not shallow. |
| `/workspaces/wt/CU-C2` | Worktree on `research/catchup-2026-11-27b-plugins` at `117945420`, prepped: dependencies, PDFium `7f399f838` linux-arm64, wasm32-eh `9472117ed`, a wasm32 payload without the Rust core, the linux-arm64 addon, all package builds, FreeType source. Target `/workspaces/target` (9.7 GB). |
| `/workspaces/wt/base` | Worktree detached at `0afd87825`, prepped the same way. Its target dir was deleted after the run. |
| `/workspaces/scripts/` | 2026-11-27's `scripts/` plus `freetype-src.sh` |
| `/workspaces/bin/clang` | The clang shim (`scripts/clang-shim.sh`). Keep `/workspaces/bin` first on `PATH` for any prep or runtime build. |
| `/workspaces/pdfium/archives/` | `libembedpdf-runtime-wasm32-eh-9472117ed.tar.gz` (CU-C2-D3) |
| `/workspaces/wasm32-eh-pin.json` | Pin for that archive, the same file as `scripts/wasm32-eh-pin.json` |
| `/workspaces/freetype-src` | FreeType `2b01fa2d6` (CU-C2-D6) |
| `/workspaces/logs/CU-C2/` | Logs; `head/` and `base/` hold the suite output |
| `/workspaces/bundles/` | Empty |

Both worktrees init `runtime-src` from the fork on GitHub, at `9472117ed`. Playwright's browsers
are in `/home/node/.cache/ms-playwright` inside the container, not the volume, so recreating the
container loses them; `scripts/prep-extra.sh` puts them back. Volume size after cleanup: 15 GB.
Host free space: 507 GiB at the start, 490 GiB at the end.

## Start state

All in the container: `scripts/prep.sh`, `scripts/prep-extra.sh`, then `scripts/suite.sh
<checkout> <out>` through `scripts/run-suites.sh`, head `117945420` and baseline `0afd87825`, one
after the other. Both checkouts pin the same PDFium (`7f399f838`). The baseline is measured fresh
(CU-C2-D5). Both wasm32 payloads report "all 1070 bound functions match their bindings".

| Check | Baseline `0afd87825` | Head `117945420` |
|---|---|---|
| `cargo build --workspace --locked` | (not run separately) | exit 0, one warning: unused dependency `uniffi` in `epdf-uniffi-bindgen-cs` |
| `cargo test --workspace --no-fail-fast` | 3,630 passed, 0 failed, 2 ignored | 3,630 passed, 0 failed, 2 ignored |
| engine `:check` scripts | 36 of 36 | 26 of 36 |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | 4 of 4 | 1 of 4: `test:harness` passes |
| TS `turbo run test` over `packages/**` | 97 of 98 tasks; 9,226 passed, 2 failed, 22 skipped | 97 of 98 tasks; 9,362 passed, 2 failed, 22 skipped (after the `core-fonts` rerun) |

The TS counts sum vitest's per-package `Tests` lines. The head's turbo run itself reported 96 of 98
tasks and one more failure, `@embedpdf/core-fonts` "the standard faces > are what the fork font
data gives", on `ENOENT` for `runtime-src/third_party/freetype/src/src/psnames/pstables.h`. That
file is a `DEPS` checkout the container lacked. After `scripts/freetype-src.sh` put it in both
checkouts, `pnpm --filter @embedpdf/core-fonts test` at the head passed 20 of 20, and the baseline's
TS run, which came after, passed it too (CU-C2-D6).

### Failures by crate

None. No crate has a failing test at either commit. The merge changed no Rust, so equal counts are
what we expected.

### Engine `:check` failures at head

| Script | Output |
|---|---|
| `signature-protection:check` | `DIFFERS signature/protection.json` |
| `annotation-read:check` | `DIFFERS annotation/read.json` |
| `forms:check` | `DIFFERS forms/snapshot.json` |
| `form-write:check` | `STALE forms/write.json` |
| `form-field:check` | `STALE forms/field.json` |
| `form-apply:check` | `DIFFERS forms/apply.json` |
| `pages-mutate:check` | `STALE pages/mutate.json` |
| `wire:check` | `DIFFERS wire/requests.json` |
| `changes:check` | `DIFFERS` on `scale`, `rename`, `annotation-copy`, `form-copy`, `widget-rename`, `field-purpose-format`, `field-groups`, `signature-mark`, `widget-rotation`, `kinds` (all under `changes/`) |
| `signature-ltv:check` | `DIFFERS signature/validation-info.json`, `signature/timestamp-field.json`, `signature/long-term.json` |

These are the forms, signature and picture-button work upstream brought (#1161, #1162, #1163,
#1165) and the wire kinds that came with it. Regeneration is CU-C3's.

### Plugin check failures at head

`vectors:check` reports 19 stale vectors: `vectors/core/annotation-{flags,props,hit,placement,update,view}.json`
and `vectors/plugins/{actions-script,annotation-hover,annotation-pure,annotation-repository,form-actions,form-core-lifecycle,form-core,form-design,form-fill,form-groups,form-scripting,signature,stamp-placement}.json`.
`coverage:check` stops with "the generators failed under coverage; fix vectors:check first".
`typecheck` reports 9 errors: `tooling/plugin-conformance/src/fake-engine.ts:183` lacks the new
`setButtonImage` capability, and 8 in `tooling/plugin-conformance/src/suites/form-current-fixtures.ts`
(`:77`, `:95`, `:109`, `:127`, `:143`, `:161`, `:170`, `:179`), one per field kind, where the
fixtures no longer match the field types upstream extended (`forAnyone`, `hasImage`).

Unlike 2026-11-27, the plugin checks left the tree clean: `crates/plugins/acrojs/prelude.js` did
not regenerate to a different file.

### Failures that are the environment, at both commits

`@embedpdf/core-signature` fails the same 2 tests at both commits as on 2026-11-27,
`answers another program made (certomancer) > OpenSSL reads the same answers the same way` and
`OpenSSL as the reader > the test authority's answers verify, and say what the checker reads in
them`. The fixtures' OCSP answers expired `2026-11-25T00:00:00Z`
(`packages/core/signature/test/revocation/interop.test.ts:82`). It is a date bomb, not a
regression.

The baseline's `coverage:check` passes here, where 2026-11-27 saw it fail in `c8` under node
26.13.1 at `d250a68e3`. Same image, so the fix is in the tree between `d250a68e3` and `0afd87825`.
We did not look for the commit.

## Evidence

| Check | Command | Exit | Log (in `/workspaces/logs/CU-C2/`) | sha256 (first 8) |
|---|---|---|---|---|
| Pin check | `python3 .devcontainer/check-pins.py` | 0, 0 | `check-pins.log` (base), `check-pins-head.log` | `41498074` (both) |
| Prep | `scripts/prep.sh` | 0, 0 | `prep-head.log`, `prep-base.log` | `99e2fa18`, `420cbb30` |
| Workspace build | `cargo build --workspace --locked` | 0 | `cargo-build.log` | `8b57d6ce` |
| cargo, head and base | `suite.sh … cargo` | 0, 0 | `head/cargo.log`, `base/cargo.log`, `*/cargo.summary` | `345b94f7`, `7d68aaa8`, `ec999ce3` (both) |
| engine `:check` | `suite.sh … engconf` | | `head/engconf.all`, `base/engconf.all` | `10ff96f1`, `27a10bbd` |
| plugin checks | `suite.sh … plugconf` | | `head/plugconf.all`, `base/plugconf.all` | `f339e7c9`, `0778cdb1` |
| TS tests | `suite.sh … ts` after `prep-extra.sh` | 1, 1 | `head/ts.log`, `base/ts.log` | `3eac9d74`, `878a6480` |
| `core-fonts` rerun | `pnpm --filter @embedpdf/core-fonts test` | 0 | `head/core-fonts-rerun.log` | `c9f30261` |

The baseline's `engconf.all` hashes `27a10bbd…`, the same as 2026-11-27's baseline at `d250a68e3`.

## Scripts

`scripts/` holds `env.sh`, `prep.sh`, `prep-extra.sh`, `suite.sh`, `cargo-by-crate.py`,
`clang-shim.sh` and `wasm32-eh-pin.json`, 2026-11-27's files with only the header comment changed,
plus two new ones: `freetype-src.sh` (CU-C2-D6) and `run-suites.sh`, the driver that ran prep-extra
and both suites.

## Cleanup

Removed: `/workspaces/target-base` (7.8 GB), the 314 MB viewer bundle in the volume and in the host
scratchpad; `fstrim` afterwards. Kept: container `embed-pdf-catchup1127b`, volume
`embedpdf-catchup1127b`, both worktrees, `/workspaces/target` and `/workspaces/freetype-src` for
the jobs that follow. On the host: branch `upstream/cda9e82a4-tmp` (a pointer to an upstream commit,
for the closing job to delete) and the worktree `.claude/worktrees/catchup-1127b` with its
`runtime-src` checkout. Nothing of the 2026-11-27 run (container `embed-pdf-catchup1127`, `cu-c8-*`,
their volumes, worktrees and branches) was touched; this job only read the wasm32-eh archive's
host copy under `/Users/andrew/repos/embed-pdf-runtime-builds/c1127-9472117ed/`.
