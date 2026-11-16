# Conformance Enforcement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the four enforcement holes and the storage cost named in the 2026-11-16 conformance report, without replacing the frozen-vector design.

**Architecture:** The TypeScript generator under `tooling/plugin-conformance` stays the oracle and `crates/plugin-conformance` stays the replayer. We add a receipt the replay writes and the tooling checks, store suite files as `.json.zst`, move every kept divergence into one ledger the replay enforces, measure Rust coverage of replay alone, and add a differential runner for pure functions that starts with `epdf-geometry`.

**Tech Stack:** Rust (serde_json, `ruzstd`), TypeScript on Node 24 (`node:zlib` zstd, tsx, `fast-check`), `cargo-llvm-cov`.

**Spec:** `/Users/andrew/Downloads/embed-pdf-rust-conformance.md` (examined at `2216e1e3b`). Copy it into `docs/research/conformance-enforcement-report.md` in Task 1 so the plan's spec travels with the branch.

## Findings included

| # | Finding | Task |
|---|---|---|
| F1 | Hole 1: replay is proven by a string literal, not by a run | 2, 3 |
| F2 | Space: 221 MB of vectors per checkout, about 60x redundant | 4, 5 |
| F3 | Hole 2: kept divergences have no single ledger; exceptions can only express a moved callback | 6, 7 |
| F4 | Hole 2: `member-order:allow` grows the allowlist with no review | 8 |
| F5 | Hole 3: no measure of which Rust lines replay reaches | 9 |
| F6 | Hole 4: every input is hand-picked (first crate, `epdf-geometry`) | 10 |
| F7 | `cargo test` over the CI crate selection died at `epdf-text-core` with `libembedpdf.so` | 1 (verify only) |
| F8 | `historical/`: 193 `.out` and 99 `.txt` files nobody has checked | 11 (audit only) |

## Findings left out, and why

| Finding | Why it is out |
|---|---|
| CI wiring for the receipt (extra artifact between the `cargo` and `vectors` jobs in `.github/workflows/plugin-rust.yml`) | The plugins branch takes no `.github/**` changes. Task 3 gives a local `replay:check` command; wiring it into CI waits for the user's call. Cost: until then a skipped replay is only caught when someone runs the command. |
| The stale header in `plugin-rust.yml:11` ("None of these crates link PDFium") | Same rule, CI file. Task 1 confirms the build failure itself is fixed by `2aaa8f841`. |
| Nightly CI run of the differential generator | CI file again. Task 10 runs it locally with a fixed seed budget. |
| Differential generation for crates beyond `epdf-geometry`, and for scenario cases | The report scopes the first crate at three to five days and says scenario inputs can't be generated usefully. Next crates get their own plan once geometry shows the cost. |
| Triage of the Rust coverage report | The report calls it open-ended. Task 9 produces the first report and the exclusion list; each unreached line becoming a vector, a ledger entry or an exclusion is follow-up work. |
| Engine corpus (`tooling/engine-conformance`, 3.9 MB) receipts and compression | The holes in the report are measured on the plugin corpus. The engine corpus is small and its `search_dialect.rs:85` `KNOWN_DIVERGENCES` list is already a named, checked list. |
| Structural dedupe (drop `previous`, intern subtrees) | The report rejects it: schema change on both sides for less gain than compression. |
| Not committing vectors (CI artifact keyed on TS sources) | The report rejects it: `cargo test` would need Node. |
| Speed work: single-pass `coverage:check` (`coverage.ts:141-142`), one `tsx` runner for the engine checks | The report measured about 20 s and less than 38 s of savings and recommends none of it. |
| Profiling `search:check` (23.5 s) | The engine README describes deliberate exponential-backtracking measurements; the report found no evidence of waste. Revisit only if the engine checks become a bottleneck. |
| The 479 `snapshot: "none"` cases and the 36 ported files no generator imports | Recorded facts in the report with no recommendation. The Rust coverage report (Task 9) and the ledger audit (Task 7) will surface whichever of them matter. |

## Global Constraints

- Branch: `research/multi-platform-rust-core-impl-plugins`. Push to `private-fork` only, and only after the user confirms.
- No TypeScript behavior changes under `packages/**`. Tooling under `tooling/plugin-conformance` is in scope.
- No changes under `.github/**`.
- Vector content must not change in any task except Task 5's format switch, and Task 5 must prove the decompressed bytes equal today's files.
- Every numeric claim in a commit or doc comes from a script, not arithmetic by hand.
- `cargo fmt -p <crate>` only; revert reformatting of lines the task did not touch.
- Run cargo test suites in the `embedpdf-dev` container (linux/arm64); Mac host only if a step needs it.
- The repo must never name outside projects or people in code, docs or commits.
- Commits: no AI attribution footer, session URL or Co-Authored-By on this repo.
- No em dashes in docs prose added by this plan.

## Review Focus

1. A test that replays one vector file from two `#[test]` functions (or two crates) must merge into one receipt, not overwrite. Pinned in Task 2.
2. A run with `PLUGIN_CONFORMANCE_ONLY` set must never satisfy `replay:check`. Pinned in Task 3.
3. Node's bundled zstd and another machine's zstd can write different bytes for the same JSON; `vectors:check` must compare decompressed text, or it flaps between machines. Pinned in Task 4.
4. A ledger entry whose observing case is renamed or deleted must fail replay, not silently stop applying. Pinned in Task 6.
5. A differential failure on a NaN or `-0` input must be reported through the same canonical comparison the vectors use, so `-0` versus `0` and NaN equality agree with the frozen cases. Pinned in Task 10.

---

### Task 1: Baseline and spec copy

**Files:**
- Create: `docs/research/conformance-enforcement-report.md` (verbatim copy of the spec)

- [ ] **Step 1:** Copy the spec into the repo path above.
- [ ] **Step 2:** In the container, run the crate selection `.github/workflows/plugin-rust.yml` computes in its `crates` step, then `cargo test <that selection>`.
  Expected: no `libembedpdf.so` load error at `epdf-text-core --lib`; every crate after it runs. Record the wall time with `/usr/bin/time`.
  If it still fails, stop and report the output: F7 is not fixed by `2aaa8f841`.
- [ ] **Step 3:** Record baselines with a script (not by hand) into the commit message: `du -sb tooling/plugin-conformance/vectors`, file count, total case count across suite files.
- [ ] **Step 4:** Commit `docs(conformance): add the 2026-11-16 enforcement report`.

### Task 2: Replay receipts (Rust)

**Files:**
- Create: `crates/plugin-conformance/src/receipt.rs`
- Modify: `crates/plugin-conformance/src/lib.rs` (`assert_suite`, lines 92-189)
- Test: unit tests in `receipt.rs`

**Interfaces:**
- Produces: env var `PLUGIN_CONFORMANCE_RECEIPTS=<dir>`. When set, every `assert_suite` call writes `<dir>/<file with "/" replaced by "__">.<pid>.<n>.json`, where `n` is a process-wide `AtomicUsize`. Content:
  ```json
  { "file": "core/geometry-basics.json", "only": null, "ran": ["case-a", "case-b"],
    "exceptions": ["<ledger id>"], "memberOrderAllowed": ["case-a"] }
  ```
  `only` is the `PLUGIN_CONFORMANCE_ONLY` value or `null`. `exceptions` lists ledger ids applied (until Task 6 lands, the `reason` prefix of `outcome-exceptions.json` entries; Task 6 switches to ids).
- `pub fn write_receipt(dir: &Path, receipt: &Receipt) -> std::io::Result<PathBuf>` and `pub struct Receipt { file, only, ran, exceptions, member_order_allowed }` (serde, camelCase).

- [ ] **Step 1: Failing tests** in `receipt.rs`:
  - `two_writes_for_one_file_keep_both`: write two receipts for `"a/b.json"` into a temp dir; assert two distinct files exist and both parse back with their own `ran` lists.
  - `no_env_writes_nothing`: with the env var unset, `assert_suite` on a one-case in-memory function suite leaves the temp dir empty (use a helper that takes the dir as `Option<&Path>` so the test doesn't mutate process env).
- [ ] **Step 2:** `cargo test -p plugin-conformance receipt` fails (module missing).
- [ ] **Step 3:** Implement. `assert_suite` collects `ran` names in the loop it already has (line 97), and writes the receipt before the final `panic!` so a failing suite still proves it ran. Write with `std::fs::write` to a temp name and rename, so a reader never sees half a file.
- [ ] **Step 4:** Tests pass; `cargo test -p plugin-conformance` passes.
- [ ] **Step 5:** Commit `feat(plugin-conformance): write a replay receipt per suite`.

### Task 3: `replay:check` (TypeScript)

**Files:**
- Create: `tooling/plugin-conformance/src/replay-check.ts`
- Modify: `tooling/plugin-conformance/package.json` (script `"replay:check": "tsx src/replay-check.ts"`)
- Modify: `tooling/plugin-conformance/README.md` (the paragraph at line 32 and the command block)
- Test: `tooling/plugin-conformance/test/replay-check.test.ts`

**Interfaces:**
- Consumes: receipt format from Task 2; suite list from `src/generate.ts` discovery.
- Produces: `export function compareReceipts(expected: Map<string, string[]>, receipts: Receipt[]): string[]` returning one problem line per failure (empty means pass). `replay-check.ts` main: create a temp dir, run `cargo test --workspace --tests` with `PLUGIN_CONFORMANCE_RECEIPTS` set and `PLUGIN_CONFORMANCE_ONLY` removed from the child env, read every receipt, build `expected` from the generator's case names (render mode, no write), print problems, exit 1 on any. `--receipts <dir>` skips the cargo run and reads an existing dir.

- [ ] **Step 1: Failing tests** for `compareReceipts`:
  - two receipts for one file whose `ran` lists union to the expected names: no problems.
  - one expected case absent from every receipt: problem `core/x.json: case "c" never ran`.
  - a vector file with no receipt: problem `core/x.json: no Rust test replayed it`.
  - any receipt with `only` non-null: problem naming the filter, even if all cases ran.
  - a receipt naming a file the generator doesn't write: problem `unknown vector file`.
- [ ] **Step 2:** `pnpm --filter @embedpdf/tooling-plugin-conformance exec node --import=tsx --test test/replay-check.test.ts` fails.
- [ ] **Step 3:** Implement. Keep the string-literal check in `vectors.ts:96-106` as the fast offline check; the README states that `replay:check` is the proof and the literal check is a lint.
- [ ] **Step 4:** Tests pass. Then in the container run `pnpm --filter @embedpdf/tooling-plugin-conformance run replay:check`. Expected: `119 vector files, 15915 cases, all replayed` (numbers from the run, not this plan). Any real gap it finds is a finding: list it in the commit and stop for the user before adding replays.
- [ ] **Step 5:** Commit `feat(plugin-conformance): check replay receipts against the generator's cases`.

### Task 4: Compressed vector writer and reader

**Files:**
- Modify: `tooling/plugin-conformance/src/vectors.ts` (`emit`, lines 193-248; `unaccounted`, 96-106)
- Modify: `crates/plugin-conformance/src/lib.rs` (`load`, lines 53-61) and `Cargo.toml`
- Modify: direct readers that bypass `load`: `crates/plugins/annotation/tests/ffi.rs:650`, `crates/plugins/selection/tests/engine_cross_check.rs:125`, `crates/epdf-capi/tests/helpers.rs:514`
- Create: `.gitattributes` entry `tooling/plugin-conformance/vectors/**/*.json.zst binary`
- Test: `crates/plugin-conformance/src/lib.rs` unit test; `tooling/plugin-conformance/test/zst.test.ts`

**Interfaces:**
- Produces: on disk a suite lives at `vectors/<name>.json.zst`; every caller keeps using the logical name `<name>.json` (so `assert_suite("core/geometry-basics.json", ...)` and the literal check are unchanged). `environment.json` and `coverage.json` stay plain JSON.
- Rust: `pub fn read_vector_text(file: &str) -> String` in `plugin-conformance`, used by `load` and by the three direct readers. Reads `<file>.zst` if present, else `<file>`, so the switch in Task 5 is one commit.
- Decoder: `ruzstd` (pure Rust), under `[target.'cfg(not(target_arch = "wasm32"))'.dependencies]`, because `epdf-web` builds this crate for wasm32 and a C `zstd-sys` build there is not worth the trouble. This departs from the report's `zstd` crate suggestion; the cost is a slower decoder, measured in Step 4.
- TS: `writeVector(path, text)` compresses with `zlib.zstdCompressSync(text, { params: { [zlib.constants.ZSTD_c_compressionLevel]: 3 } })`; `readVector(path)` decompresses. Check mode compares decompressed text to the rendered text and never compares compressed bytes. Write mode rewrites a file only when its decompressed text differs, so an unchanged suite keeps its blob.

- [ ] **Step 1: Failing tests:**
  - Rust `reads_zst_before_json`: temp dir with `x.json.zst` (fixture bytes produced by the TS writer, checked in under `crates/plugin-conformance/tests/data/`) and a different `x.json`; `read_vector_text` returns the `.zst` content.
  - TS `check ignores compressed byte differences`: compress the same text at level 3 and level 19, store the level-19 bytes, run check on the level-3 rendering; no stale report.
  - TS `write leaves an unchanged suite's bytes alone`: write, record mtime and bytes, write again, assert identical bytes.
- [ ] **Step 2:** Both fail.
- [ ] **Step 3:** Implement as above. Vectors on disk are still `.json` after this task.
- [ ] **Step 4:** `cargo test -p plugin-conformance` and the full conformance replay pass unchanged. Time `cargo test --tests` for the plugin crates before and after (no format switch yet, so expect no change).
- [ ] **Step 5:** Commit `feat(plugin-conformance): read and write zstd-compressed vector files`.

### Task 5: Switch the corpus to `.json.zst`, with a case-level diff summary

**Files:**
- Create: `tooling/plugin-conformance/src/case-diff.ts`
- Modify: `tooling/plugin-conformance/src/vectors.ts` (`emit` write mode prints the summary)
- Modify: `tooling/plugin-conformance/README.md`, "Files"
- Test: `tooling/plugin-conformance/test/case-diff.test.ts`
- Data: every `vectors/**/*.json` suite file becomes `*.json.zst`

**Interfaces:**
- Produces: `export function caseDiff(before: Suite | null, after: Suite): { added: string[]; removed: string[]; changed: string[] }`, comparing cases by `name` and by `canonicalKey` of the whole case. Write mode prints one line per file with any entry: `core/x.json: +2 -0 ~5 (changed: a, b, c, d, e)`, capped at 10 names per file with `and N more`.

- [ ] **Step 1: Failing tests** for `caseDiff`: added, removed, changed by trace, unchanged under member reordering only if `canonicalKey` treats it as equal (assert whichever `canonicalKey` does today, and name it in the test).
- [ ] **Step 2:** Fails.
- [ ] **Step 3:** Implement and wire into write mode.
- [ ] **Step 4:** Convert: a one-off script in the scratchpad (not committed) compresses each suite file, then deletes the `.json`. Verify with a second script that for every file `zstd -dc x.json.zst | cmp - <(git show HEAD:path/x.json)` is equal. Then `vectors:check`, `coverage:check`, `replay:check` all pass.
- [ ] **Step 5:** Measure with scripts and put the numbers in the commit message: `du -sb` of `vectors/` before and after, and the packed size added to history (`git count-objects -vH` before and after a `git gc` in a throwaway clone). The report estimates about 4 MB per full regeneration; record the real figure.
- [ ] **Step 6:** Commit `chore(plugin-conformance): store vector suites as zstd`.

### Task 6: Divergence ledger and the value-at-path exception

**Files:**
- Create: `crates/plugin-conformance/divergences.json`
- Modify: `crates/plugin-conformance/src/exceptions.rs` (whole file; rename stays, the module now loads the ledger)
- Delete: `crates/plugin-conformance/outcome-exceptions.json` (its one entry, M13, moves into the ledger)
- Modify: `crates/plugin-conformance/src/lib.rs` (`assert_suite` exception handling, lines 120-136 and 166-175)
- Test: unit tests in `exceptions.rs`

**Interfaces:**
- Ledger schema (serde, `deny_unknown_fields`):
  ```json
  [{ "id": "M13", "summary": "...", "reason": "...", "decided": "2026-10-23 22ca510e6",
     "observedBy": [
       { "file": "core/acrojs-host.json", "case": "dispose-pending-factory",
         "movedCallback": { "callback": {...}, "actualStep": 3, "expectedStep": 4 } },
       { "file": "...", "case": "...",
         "valueAt": { "path": "$[2].settled[0].value.order", "expected": ..., "actual": ... } } ],
     "pinnedBy": "crates/plugins/acrojs/src/host.rs::a_realm_built_after_dispose_is_released_when_the_queue_drains" }]
  ```
  Each entry needs `observedBy` non-empty or `pinnedBy` set (both allowed). `path` uses the trace path syntax `canonical::diff_entry` already prints.
- `pub fn ledger() -> &'static [Divergence]`, `pub fn for_case(file, case) -> Vec<(&'static Divergence, &'static Observation)>`, `pub fn apply(obs: &Observation, expected: &[Value], actual: &mut [Value]) -> Result<(), String>`.

- [ ] **Step 1: Failing tests:**
  - existing moved-callback tests, ported to the new types.
  - `value_at_replaces_the_rust_value`: trace differing only at the path; after `apply`, `compare_trace` is `None`.
  - `value_at_is_stale_when_rust_already_matches`: Rust holds `expected` at the path; `apply` errs with `stale`.
  - `value_at_is_stale_when_rust_holds_a_third_value`: errs naming both values.
  - `entry_without_observation_or_pin_is_invalid`.
  - `pinned_test_must_exist`: validation reads the named file relative to the workspace root and requires `fn <name>(`; a missing name errs.
  - Review Focus 4: `an_observation_whose_case_never_ran_fails` (port of today's check at `lib.rs:166-175`, now per observation).
- [ ] **Step 2:** Fail.
- [ ] **Step 3:** Implement. Receipts (Task 2) now list ledger ids in `exceptions`.
- [ ] **Step 4:** Full replay passes with M13 as the only observed entry.
- [ ] **Step 5:** Commit `feat(plugin-conformance): one divergence ledger with moved-callback and value-at-path forms`.

### Task 7: Ledger audit

**Files:**
- Modify: `crates/plugin-conformance/divergences.json`
- Possibly modify: suites under `tooling/plugin-conformance/src/suites/` (new cases that observe a divergence)
- Create: `docs/research/divergence-ledger-audit.md` (one row per divergence: id, observed or pinned, evidence)

**Interfaces:**
- Consumes: the Task 6 schema.
- Sources to enumerate, all of them: `docs/research/rust-plugins-branch-review.md` "Status, 2026-10-13"; the "Decisions" sections of `multiplatform-upstream-catchup*.md` at the repo root; `docs/research/upstream-catchup-2026-11-07` and `-2026-11-12`; crate READMEs (`grep -rn "divergen" crates/*/README.md crates/plugins/*/README.md`). Known ids to find at minimum: H3, M13, M14, M15, M23, M25, M27 (NaN part), R5, R7, N6 (part), stage close-cancel, FreeText rich-text font and schema, wire-kinds setFontSettings, finalizeCandidate and flatten, malformed PageRef, events envelope and `document.versioned`, fonts payload and strict numbers, no-op `mutationSequence`, fallback-tier NaN order, E3, E4, P1, P3, app_state JSON-only, stage locks order and release.

- [ ] **Step 1:** For each divergence, search the suites and the decompressed vectors for its behavior. Decide one of: (a) a case observes it, add `observedBy`; (b) no case observes it but one can, add the case to the suite, regenerate, add `observedBy`; (c) the trace cannot see it, add `pinnedBy` naming the Rust test and say why in `reason`.
  Adding a case in (b) is a vector change: show the user the list before regenerating.
- [ ] **Step 2:** Run the full replay and `replay:check`. Every entry validates and none is stale.
- [ ] **Step 3:** Write the audit doc with a script that renders `divergences.json` into the table, plus one paragraph per (c) entry on why no trace can see it.
- [ ] **Step 4:** Commit `docs(conformance): ledger every kept divergence`.

Budget: two to three days, per the report. If an id can't be traced to a decision record, list it in the audit doc as unresolved and ask the user; don't guess a reason.

### Task 8: Member-order allowlist under the ledger

**Files:**
- Modify: `crates/plugin-conformance/member-order-allow.json` (format)
- Modify: `crates/plugin-conformance/src/canonical.rs` (`allowed_member_order`, lines 56-70)
- Modify: `tooling/plugin-conformance/src/member-order-allow.ts`
- Modify: `crates/plugin-conformance/divergences.json` (entries the allowlist cites)
- Test: unit tests in `canonical.rs`; `tooling/plugin-conformance/test/member-order-allow.test.ts`

**Interfaces:**
- New format: `{ "<file>": { "<case>": [{ "path": "$.returned.paragraphs[]", "divergence": "MO-passthrough" }] } }`. `divergence` must name a ledger id; replay panics at load on an unknown id.
- The 375 current paths get ledger ids by cause. Start with two entries, `MO-passthrough` (the TypeScript passes a caller's object through) and `MO-builder-order` (one type's order depends on which function built it), the two causes the tool's header comment allows. Paths that fit neither are Rust order bugs: list them for the user, don't ledger them.
- `member-order:allow` no longer writes the allowlist. It writes failing paths to `member-order-candidates.json` (gitignored) and prints them. Moving a candidate into the allowlist is a hand edit that needs a `divergence` id. Removing entries needs nothing, so the list shrinks freely.

- [ ] **Step 1: Failing tests:** unknown divergence id rejected; old string-only entry rejected; tool run writes candidates and leaves the allowlist bytes unchanged.
- [ ] **Step 2:** Fail.
- [ ] **Step 3:** Implement; migrate the file with a scratchpad script that classifies by path pattern, then review the residue by hand.
- [ ] **Step 4:** Full replay passes; `--fresh` run of the tool produces no candidates outside the allowlist.
- [ ] **Step 5:** Commit `feat(plugin-conformance): member-order exemptions cite a ledger entry`.

### Task 9: Rust-side replay coverage

**Files:**
- Create: `tooling/plugin-conformance/src/rust-coverage.ts`
- Create: `crates/plugin-conformance/rust-coverage-exclude.json` (`[{ "glob": "crates/plugins/*/src/harness.rs", "reason": "replay adapter" }, ...]`)
- Create: `tooling/plugin-conformance/vectors/rust-coverage.json`
- Modify: `tooling/plugin-conformance/package.json` (scripts `rust-coverage`, `rust-coverage:check`)

**Interfaces:**
- `rust-coverage` runs, in the container with `cargo-llvm-cov` installed (`cargo install cargo-llvm-cov --locked`), two measurements over the plugin crates: replay only (`cargo llvm-cov --json --tests` filtered to the `conformance` test targets plus the function-table tests) and all tests. Output per source file: `{ "replayLines": "a/b", "unitOnlyLines": ["120-131", ...] }`, where unit-only means hit by all tests and not by replay. Uses the `ranges()` formatting already in `coverage.ts:130-139` (export it rather than copy it). Excluded globs are dropped before totals.
- `rust-coverage:check` reruns and fails only if the file is stale, like `coverage:check`. It is not a threshold gate.

- [ ] **Step 1: Failing test** `test/rust-coverage.test.ts`: given two small llvm-cov JSON fixtures, `unitOnly(replay, all)` returns the right ranges and drops excluded files.
- [ ] **Step 2:** Fails.
- [ ] **Step 3:** Implement; run it; fill the exclusion list with FFI glue and harness adapters, each with a reason.
- [ ] **Step 4:** `rust-coverage:check` passes twice in a row on the same tree (llvm-cov output stable). If it isn't stable, record that and drop the `:check` script rather than ship a flapping check.
- [ ] **Step 5:** Commit `feat(plugin-conformance): record which Rust lines replay reaches`. The commit message gives the totals and the five files with the most unit-only lines; triage is out of this plan.

### Task 10: Differential runner for `epdf-geometry`

**Files:**
- Create: `crates/plugin-conformance/src/bin/function-server.rs`
- Create: `tooling/plugin-conformance/src/differential.ts`
- Create: `tooling/plugin-conformance/src/differential/geometry.ts` (arbitraries per function)
- Modify: `tooling/plugin-conformance/package.json` (devDependency `fast-check`, script `differential`)
- Modify: `crates/plugin-conformance/divergences.json` schema: optional `"differentialSkip": [{ "fn": "...", "when": "<description>" }]`, with the predicate implemented in `differential/<crate>.ts` keyed by ledger id

**Interfaces:**
- `function-server` reads newline-delimited `{ "table": "geometry", "fn": "...", "args": [...] }` on stdin and writes `{ "returned": ... }` or `{ "threw": ... }` per line, using the same `scrub` and `to_canonical` as `assert_suite` (`lib.rs:148-158`). Tables are registered by name; the first is `epdf_geometry::harness::functions()`. A native process, not wasm: the report proposes wasm, but a stdio process needs no wasm build and reuses the native function tables. Cost: one child process per run, and it can't run in the browser.
- `differential.ts --crate geometry --runs 1000 --seed <n>`: for each function in the TS suite's `basicsFns`, `affineFns`, `pageFns`, `textQuadFns` (`suites/geometry.ts`) with an arbitrary defined, generates inputs, calls TS through the same path the generator uses, sends the inputs to `function-server`, and compares with `canonical.diff` semantics. On a mismatch, fast-check shrinks, then the tool prints a ready-to-paste function case `{ name: "diff-<fn>-<seed>", fn, args }` for the suite. A pinned seed becomes a permanent vector that way, through the normal generator.
- Arbitraries include NaN, `-0`, `±Infinity`, subnormals and values near `1e308`, since `geometry.ts` already has hand cases there.

- [ ] **Step 1: Failing tests:**
  - Rust: `function_server_answers_a_known_case`: feed one line from `core/geometry-basics.json`, assert the reply equals that case's `outcome`.
  - TS: `differential reports a planted mismatch`: run against a stub server that returns `0` where TS returns `-0`; expect one failure whose printed case has the shrunk input. Review Focus 5.
  - TS: `a skip from the ledger suppresses a known divergence`.
- [ ] **Step 2:** Fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `differential --crate geometry --runs 10000` with three seeds. Every mismatch is either a Rust bug (stop, report to the user with the printed case) or a TS-side quirk the user decides on. No ledger entry is added without the user.
- [ ] **Step 5:** Commit `feat(plugin-conformance): differential runner for pure function tables, starting with geometry`.

Budget: three to five days, per the report.

### Task 11: `historical/` audit (report only)

**Files:**
- Create: `docs/research/historical-artifacts-audit.md`

- [ ] **Step 1:** With a script, list every file under `tooling/plugin-conformance/historical/` and every reference to it from `tooling/plugin-conformance/{src,test}` (static `new URL(...)` paths and string-built ones like `'../historical/' + task + ...`). Resolve the dynamic ones by reading the `task` values in each test.
- [ ] **Step 2:** Write the audit: referenced files, unreferenced files with sizes, and the size compressing the unreferenced `.out`/`.txt` would save (measured with `zstd -3`, not estimated).
- [ ] **Step 3:** Commit the doc. Deleting or compressing anything under `historical/` is the user's decision after reading it.

---

## Order and time

Tasks 1 to 3 first, about a day: everything after relies on the receipt. Tasks 4 and 5 next, about a day, mechanical, and they free most of the 221 MB `vectors/` takes per checkout (Task 5 measures the real figure). Tasks 6 to 9, three to five days, with Task 7 the long one. Task 10 last, three to five days, because its skip filters come from the ledger. Task 11 fits anywhere and takes about an hour.
