# Rust port performance fixes: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Measure where the Rust port stands at the branch tip, give the harness fixtures that can see annotation and form costs, then land the 60-odd small fixes from the 2026-11-16 performance review that are Rust-only, per-interaction, and keep the conformance vectors byte-identical.

**Architecture:** Measurement comes first, because the last Rust arm ran on 2026-10-13 at `9d6c2eca1`, 2,064 commits back. Two shared test helpers (a PDFium call counter and a pointer-keyed answer-text memo) land before the fixes that need them. Fixes are grouped by the surface that pays for them (web kernel, plugins, engine, native hosts), and each group is timed as one `plugins-base`/`plugins-next` pair. Engine and native fixes are proven by call counts until the native engine timer in Task 28 exists.

**Tech Stack:** Rust (workspace crates under `crates/`), `perf-compare` (Node, Playwright, headless Chromium 140, Mac mini), Swift (`platforms/apple`), C# (`platforms/dotnet`).

**Spec:** `~/Downloads/embed-pdf-rust-port-perf.md` and `~/Downloads/embed-pdf-rust-port-perf-detail/*.md` (laptop only, not in the repository). Finding IDs below (`W1`, `S1`, `E1`, `N1`) are the summary's; IDs like `P5-F1` or `A5-1a` are the detail files'.

## Global Constraints

- Branch `research/multi-platform-rust-core-impl-plugins`; harness changes go on `research/viewer-perf-compare` and are merged into plugins after the result is recorded in `perf-compare/FINDINGS.md`.
- Push to `private-fork` only. Confirm with the user before every merge or push.
- Rust must match the TypeScript reference. No TS behavior changes to the upstream TS under `packages/**`. `packages/core-wasm` is the port's own JS bridge (added on this branch in `7a3554f97`, not on `main`) and may change.
- No changes under `.github/**`.
- Every fix keeps plugin-conformance and engine vectors byte-identical. A fix whose row says "vectors: check" runs the full vector suite in `rust-plugins` before commit.
- All work happens in the devcontainer `embed-pdf-perf` (image `embedpdf-dev`, volume `embedpdf-perf-workspace`, clone at `/workspaces/embed-pdf-viewer`, the host repository bind-mounted at `/host-repo` as remote `host`). Enter it with `docker exec -it embed-pdf-perf bash`. Fix tasks commit on `research/perf-fixes`; harness tasks (1 to 3) commit on `research/viewer-perf-compare` in a container worktree. Every task ends with `git push host HEAD:<branch>` so the host sees the commits. The only host-side work: Swift/Xcode steps (Task 26), `nm` on the Apple binary (Task 22), and launching timed runs.
- Performance is measured only on the Mac mini, `ssh andrew@MacMini2020` (fallback `andrew@100.105.247.125`), one run at a time. Arms build in the container; the run is launched from the host, because SSH keys live in the host's agent.
- Disk: check `df -h /System/Volumes/Data` and `docker system df` before every heavy build; under 50 GB free, stop and clean up first. Remove perf-compare arm worktrees and builds after each run, except the arms the next run needs. Never `git worktree prune`; remove only your own worktree by exact path. When the plan finishes, remove the `embed-pdf-perf` container and its volume after every branch is pushed to the host.
- If the 5-hour or 7-day usage limit reaches 90%, pause until it resets.
- Before calling any row a regression, compare the same build across earlier runs (`node scripts/summary.mjs`).
- Cargo work runs in the `embed-pdf` devcontainer; Swift and Xcode steps run on the Mac host.
- `cargo fmt -p <crate>` may reorder untouched lines; revert those before committing.
- Commits, PRs and releases carry no Claude footer, session URL or `Co-Authored-By`.
- Numbers in records come from scripts, never mental arithmetic.

## Review Focus

1. A page edit (insert, delete, move) after the kernel page index is built must not resolve a ref to the old index. Test in Task 6.
2. A permission change that arrives without a store change must not leave `allows()` answering from a stale memo. Test in Task 8.
3. A memo keyed on `Rc` pointer identity must not match a new value that reused a freed address. Test in Task 4.
4. The engine search corpus cache must miss after any mutation of the page's document. Test in Task 21.
5. A native render cancelled before it runs must still settle its caller with an abort, not hang. Test in Task 25.

---

## Phase A: measure the tip (harness branch)

### Task 1: Merge the pending probe fix and make reports honest

About 2 hours.

**Files:**
- Modify: `perf-compare/report.mjs:253-279` (`--against`), `pairTable` and the A/A table
- Test: `perf-compare/test/report.test.mjs` (create if absent)

- [ ] **Step 1:** On `research/viewer-perf-compare`, confirm `b102be86a` (probe visibility and decode tracking) is the branch tip and is not in plugins: `git log --oneline research/multi-platform-rust-core-impl-plugins..research/viewer-perf-compare`.
- [ ] **Step 2: Write failing tests.** `threshold column shows max(FLOOR, noise) per row` (feed a fixture run, assert the printed threshold equals `lib/stats.mjs` `max(FLOOR, noise)` for two rows); `--against exits non-zero with "cross-run comparison removed"`; `max column header is "max", not "p95"`.
- [ ] **Step 3:** Run `node --test perf-compare/test`. Expected: the three new tests fail.
- [ ] **Step 4:** Remove `--against` (print the message and exit 2). Add a `threshold` column to every pair row. Rename `p95` to `max`.
- [ ] **Step 5:** Run `node --test perf-compare/test`. Expected: pass.
- [ ] **Step 6:** Commit `perf-compare: drop --against, print each row's detection threshold`.

### Task 2: Re-pin and run the tip against 9d6c2eca1 and current main

About 3 hours of builds plus 1.25 hours on the mini.

**Files:**
- Modify: `perf-compare/arms.json`

- [ ] **Step 1:** Set `plugins-base` to `9d6c2eca1` by hand (`pin.mjs` would move it to the tip). Pin `plugins-next` to the plugins tip with `node scripts/pin.mjs plugins-next`.
- [ ] **Step 2:** Add arm `main-now` (TypeScript recipe, `origin/main` at `ed064e0c8`) and arm `plugins-next-lto` (recipe `rust-kernel-lto`, same sha as `plugins-next`). Add pairs `[main, main-now]`, `[main-now, plugins-next]`, `[plugins-next, plugins-next-lto]`. Tag any arm sha not on a kept branch as `arm/<arm-id>`.
- [ ] **Step 3:** Build the five non-default arms locally, then run 10 rounds on the mini with arms `main main-aa main-now plugins-base plugins-next plugins-next-lto`.
- [ ] **Step 4:** `node scripts/summary.mjs results/<run-id>`. Record in `FINDINGS.md`: tip vs `9d6c2eca1`, tip vs `main-now`, `main` vs `main-now`, LTO on the current kernel. Every "no difference" row cites its threshold.
- [ ] **Step 5:** `node report.mjs --publish results/<run-id>` to refresh `RESULTS.md`.
- [ ] **Step 6:** Commit, confirm with the user, push `research/viewer-perf-compare`, merge into plugins.

### Task 3: Fixtures and flows that can see annotation and form costs

1 to 2 days, plus about 15 minutes per 10-round run.

**Files:**
- Modify: `perf-compare/fixtures/generate.mjs`, `perf-compare/fixtures/fixtures.json`
- Modify: `perf-compare/lib/flows.mjs` (`FLOWS`, and `scroll`'s `startAt(0)` at `:204-209`, `:407-410`)

**Interfaces:**
- Produces: fixtures `annots.pdf` (400 pages, 1,200 annotations: 1,000 highlights, 150 ink, 50 stamps) and `forms.pdf` (1,000 AcroForm text and checkbox fields over 100 pages); flows `scrollMid`, `hover`, `dragAnnot`, `drawInk`, `typeField`, `undo`.

- [ ] **Step 1:** Generate both fixtures with hand-written annotation and AcroForm objects in `generate.mjs`. Assert in the generator that the TS `main` engine lists 1,200 annotations and 1,000 fields.
- [ ] **Step 2:** Add the six flows. `scrollMid` starts at page 300 of `large.pdf`. `hover` moves across 20 annotations on one page; `dragAnnot` drags one highlight 200 px; `drawInk` draws 3 strokes of 60 samples; `typeField` types 40 characters into one field; `undo` undoes 10 edits.
- [ ] **Step 3:** Run every new flow once locally on `main` and `plugins-next` (quick profile). Expected: each completes on both arms with the same answer.
- [ ] **Step 4:** Run 10 rounds on the mini with the four default arms (`plugins-base` = `plugins-next` = tip). This is the baseline for Phases C and D. Record it in `FINDINGS.md`.
- [ ] **Step 5:** Commit, confirm, push, merge into plugins.

---

## Phase B: shared test helpers

### Task 4: Pointer-keyed answer text in plugin-conformance

About 3 hours.

**Files:**
- Modify: `crates/plugin-conformance/src/harness.rs:16` (`Outcome`), `:635-651` (`outcome_text_raw`)
- Test: `crates/plugin-conformance/tests/live_harness.rs`

**Interfaces:**
- Produces: `Outcome::Text(Rc<str>)`; `pub struct TextMemo<T>` with `pub fn get_or_print(&self, value: &Rc<T>) -> Rc<str> where T: Serialize`. The memo holds the source `Rc<T>` and compares with `Rc::ptr_eq`. `outcome_text_raw` writes `Outcome::Text` verbatim.

- [ ] **Step 1: Failing tests.** `text_memo_prints_once_per_rc` (two calls with the same `Rc` return `Rc::ptr_eq` text; a print counter reads 1). `text_memo_misses_on_new_rc_at_reused_address` (drop the first `Rc`, allocate a new equal-sized one until the address repeats or 1,000 tries, assert the memo reprints). `outcome_text_is_byte_identical` (text equals `serde_json::to_value(&v).unwrap().to_string()`).
- [ ] **Step 2:** `cargo test -p plugin-conformance --test live_harness`. Expected: fail to compile.
- [ ] **Step 3:** Implement `Outcome::Text` and `TextMemo`.
- [ ] **Step 4:** Rerun. Expected: pass.
- [ ] **Step 5:** Commit `plugin-conformance: add Outcome::Text and a pointer-keyed TextMemo`.

### Task 5: PDFium call counter, and the kernel `allows` memo test seam

About 2 hours.

**Files:**
- Modify: `crates/pdfium/Cargo.toml` (feature `call-count`), `crates/pdfium/src/lib.rs`
- Modify: `crates/pdfium/src/{page_list.rs,form.rs,annot.rs,text.rs,signature.rs}` (count at `Document::page`, `form_model`, `raw_annotation`, `FPDFText_LoadPage`, `EPDFSig_LoadModel`)
- Test: `crates/pdfium/tests/documents.rs`

**Interfaces:**
- Produces: `pdfium::call_count::{reset(), get(Call) -> u64}` with `enum Call { LoadPage, FormLoadModel, GetAnnotRaw, TextLoadPage, SigLoadModel }`, thread-local, compiled only with feature `call-count`. Engine tests enable it through `[dev-dependencies]`.

- [ ] **Step 1: Failing test.** `load_page_is_counted`: open a 3-page fixture, call `Document::page(0)` twice, assert `get(Call::LoadPage) == 2`.
- [ ] **Step 2:** `cargo test -p pdfium --features call-count --test documents`. Expected: fail.
- [ ] **Step 3:** Implement the counter.
- [ ] **Step 4:** Rerun. Expected: pass. `cargo build -p pdfium --release` without the feature shows no counter symbols.
- [ ] **Step 5:** Commit `pdfium: count hot PDFium calls behind call-count`.

---

## Phase C: web kernel

Phase C ends with one 10-round run: `plugins-base` at the Task 3 baseline, `plugins-next` at the end of Task 12. Rows to watch: `jump`, `selectLarge`, `scroll`, `scrollMid`, `zoomLarge`, `hover`, `searchLarge`.

### Task 6: Kernel page index and borrowed page reads (W3, W4)

About 4 hours.

**Files:**
- Modify: `crates/epdf-kernel/src/plugin.rs:301-303` (`document()`), `:971-988` (`page_by_ref`); `crates/epdf-kernel/src/kernel.rs:3051-3067` (`list_pages`)
- Modify: `crates/plugin-conformance/src/scenario.rs:248`; callers `crates/plugins/render/src/controller.rs:360-379`, `crates/plugins/stage/src/services/scene.rs:106-116`, `crates/plugins/annotation/src/read/chrome.rs:116-123`, `crates/plugins/form/src/controller.rs:154-170`
- Test: `crates/epdf-kernel/tests/page_view.rs`, `crates/plugin-conformance/tests/kernel.rs`

**Interfaces:**
- Consumes: `TextMemo` (Task 4).
- Produces: `ControllerContext::document_rc(&self, id) -> Option<Rc<DocumentMeta>>`; `page_by_ref(&self, doc, r) -> Option<&Value>` backed by a `HashMap<u32, usize>` built once per `DocumentMeta.pages` `Rc`; `list_pages` returns `Rc<Value>` and the adapter prints it through `TextMemo`.

- [ ] **Step 1: Failing tests.** `page_by_ref_duplicate_object_number_resolves_first`; `page_by_ref_follows_page_move` (move page 3 to 0, assert the ref resolves to the new row); `list_pages_text_byte_identical_and_shared` (two reads with no store change return `Rc::ptr_eq` text).
- [ ] **Step 2:** `cargo test -p epdf-kernel --test page_view` and `cargo test -p plugin-conformance --test kernel`. Expected: fail.
- [ ] **Step 3:** Implement, then move the four callers to `document_rc()` and `page_by_ref`.
- [ ] **Step 4:** Rerun both, then the render, stage, annotation and form `conformance` tests. Expected: pass, vectors unchanged.
- [ ] **Step 5:** Commit `epdf-kernel: index pages by object number and share page lists`.

### Task 7: Selection geometry without the Value detour (W1)

About 4 hours.

**Files:**
- Modify: `crates/plugins/selection/src/controller.rs:236-240` (`hydrate`), `:278` (loader)
- Modify: `crates/epdf-kernel/src/page_mirror.rs:43-47`, `crates/plugins/selection/src/text_layout.rs:193-198` (`WireCell`), `:507-517`
- Test: `crates/plugins/selection/tests/geometry_decode.rs`

**Interfaces:**
- Produces: the loader uses `doc.call_answer` and decodes to `TextLayout` directly; `PageMirror` stops holding the `Value` tree; `WireCell` gets a key-peeking `Deserialize` that tries the right shape first.

- [ ] **Step 1:** Replace `from_value(snapshot.clone())` with `PageGeometrySnapshot::deserialize(snapshot)`. Run `cargo test -p epdf-plugin-selection`. Expected: pass. Commit.
- [ ] **Step 2: Failing test.** `loaded_page_keeps_one_geometry_copy`: after `ensure_loaded(page)`, the mirror holds no `Value` for the page (`mirror.raw_len(page) == 0`).
- [ ] **Step 3:** Move the loader to `call_answer`, drop the `Value` from `PageMirror`, write the `WireCell` visitor.
- [ ] **Step 4:** `cargo test -p epdf-plugin-selection` (all tests, including `geometry_decode`, `stale_text`, `conformance`). Expected: pass.
- [ ] **Step 5:** Commit `selection: decode page geometry straight from the answer`.

### Task 8: Memoize permission checks (W2)

About 3 hours. Vectors: check.

**Files:**
- Modify: `crates/epdf-kernel/src/plugin.rs:604-612` (`ControllerContext::allows`)
- Test: `crates/epdf-web/tests/engine.rs`, `crates/epdf-kernel/tests/runtime.rs`

**Interfaces:**
- Produces: `allows` caches `(member, args) -> bool` in a `RefCell<HashMap<(String, String), bool>>`, cleared when `core_revision()` or `store_change_count()` moves, and on any engine `security` event.

- [ ] **Step 1: Failing tests.** `allows_crosses_once_per_revision` (ten `allows` calls with one store revision make one `bridge.security` call). `allows_rereads_after_security_event` (an unlock event with no store change makes the next `allows` cross again and return the new answer).
- [ ] **Step 2:** `cargo test -p epdf-web --test engine`. Expected: fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Rerun, then the full plugin-conformance vectors in `rust-plugins`. Expected: pass.
- [ ] **Step 5:** Commit `epdf-kernel: memoize permission checks per store revision`.

### Task 9: Render and stage per-frame work (W5, W6, W11)

About 1 day.

**Files:**
- Modify: `crates/plugins/render/src/tile_manager.rs:365-369`, `:585-589`, `:629-633`, `:743-761`; `crates/plugins/render/src/raster_store.rs:376-407`; `crates/plugins/render/src/controller.rs:1786-1790`
- Modify: `crates/plugins/stage/src/services/scene.rs:314-374`; `crates/plugins/stage/src/connect.rs:36-40`, `:85-98`; `crates/plugins/view-manager/src/controller.rs:287-294`
- Modify: `crates/plugins/geometry/src/js.rs:105-127`, `crates/epdf-kernel/src/js.rs:67`
- Test: `crates/plugins/render/tests/behavior.rs`, `crates/plugins/stage/tests/api.rs`, `crates/plugins/geometry/tests/conformance.rs`

- [ ] **Step 1: Failing tests.** `number_to_string_matches_js_digits` (proptest over `f64`, compared with the existing JS digit table); `scene_rebuilt_once_per_input_change` (eight `build_scene` calls with unchanged inputs build one key); `eviction_order_unchanged_at_budget`; `stage_watcher_skips_unchanged_core` (a store change from another plugin makes no full read).
- [ ] **Step 2:** Run the three crates' tests. Expected: new ones fail.
- [ ] **Step 3:** Integer fast path in `number_to_string` (one `String`, below 2^53). Tile keys as `(to_bits, to_bits)` with `Rc<Vec<String>>` in `TileQueue` and one `key_suffix` per plan. `SceneInputs` memo in `build_scene`. Stage and view-manager watchers compare `core_revision()` and `Rc::as_ptr` first. `(used, index)` plus `swap_remove` in eviction. `slow_after_ms` reads the `resolved()` memo.
- [ ] **Step 4:** Rerun, plus each crate's `conformance`. Expected: pass.
- [ ] **Step 5:** Commit per crate (render, stage and view-manager, geometry).

### Task 10: Kernel runtime copies (W9, W11)

About 2 hours.

**Files:**
- Modify: `crates/epdf-kernel/src/rt.rs:506-507` (`settle`), `crates/epdf-web/src/engine.rs:117-119` (`args_json`), `crates/epdf-kernel/src/store.rs:524-534`
- Test: `crates/epdf-kernel/tests/runtime.rs`, `crates/epdf-kernel/tests/state_text.rs`

- [ ] **Step 1: Failing test.** `settle_without_awaiter_moves_result` (a settle with no waiter leaves the answer's `Rc` strong count at 1). `core_text_byte_identical` already pins `store.rs` output; confirm it covers a document with pages.
- [ ] **Step 2:** Run. Expected: the new test fails.
- [ ] **Step 3:** Move the result when nobody waits; `serde_json::to_string(args)`; `serde_json::to_writer` over `Js(&core)` in place of `to_value`.
- [ ] **Step 4:** Rerun both test files. Expected: pass.
- [ ] **Step 5:** Commit `epdf-kernel: stop copying settled answers and core state`.

### Task 11: Command resolution (W10)

About 1 day.

**Files:**
- Modify: `crates/plugins/commands/src/harness.rs:220-238`, `:314`, `:494-499`; `crates/plugins/commands/src/ffi.rs:84-91`; `crates/plugins/commands/src/contract.rs:26-32`, `:63-68`; `crates/plugins/commands/src/controller.rs:595`, `:599`, `:619`
- Test: `crates/plugins/commands/tests/behavior.rs`

**Interfaces:**
- Consumes: `TextMemo` (Task 4).
- Produces: `CommandCtx` carries `Rc<OnceCell<Rc<str>>>` for its printed context; the capability memo `found: RefCell<Vec<(AnyToken, Option<Rc<dyn Any>>)>>` ports the TS per-context memo; resolve keys become `HashMap<String, HashMap<String, _>>` with `Rc<Answers>`; `resolveCommand` prints through `TextMemo`.

- [ ] **Step 1: Failing tests.** `derivation_ctx_printed_once_per_store_change` (100 derivations print the context once); `capability_lookup_memoized_per_ctx`; `resolve_command_text_shared_until_change`.
- [ ] **Step 2:** `cargo test -p epdf-plugin-commands --test behavior`. Expected: fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** `cargo test -p epdf-plugin-commands`. Expected: pass, `conformance` unchanged.
- [ ] **Step 5:** Commit `commands: print derivation context and answers once per revision`.

### Task 12: Hover, search slices, redaction count, history (W7, S9, S11)

About 1 day. Vectors: check (S11, P5-F5, P5-F9).

**Files:**
- Modify: `crates/plugins/interaction/src/harness.rs:322`, `:595-596`, `:680`; `crates/plugins/selection/src/handler.rs:160`
- Modify: `crates/plugins/search/src/controller.rs:316-324`; `crates/epdf-kernel/src/engine.rs:173-187`
- Modify: `crates/plugins/redaction/src/read/pending.rs:173-176`, `:218-235`
- Modify: `crates/plugins/history/src/model.rs:138-168`, `:183`, `:204-209`; `crates/plugins/history/src/controller.rs:179-181`; `crates/plugins/history/src/changes.rs:185-187`; `crates/plugins/history/Cargo.toml:21` (serde `rc`)
- Test: `crates/plugins/{interaction/tests/conformance.rs, selection/tests/behavior.rs, search/tests/conformance.rs, redaction/tests/behavior.rs, history/tests/behavior.rs}`

- [ ] **Step 1: Failing tests.** `hover_does_not_reload_loaded_page`; `pending_count_hit_makes_no_annotation_list_call` (call log in `redaction/tests/behavior.rs` shows 0 `annotation.list` on the second read); `record_shares_existing_entries` (`Rc::ptr_eq` on every older undo entry after a record); `search_slice_decodes_without_value` (the `call_answer_with` path returns the same hits as today).
- [ ] **Step 2:** Run each crate's tests. Expected: new ones fail.
- [ ] **Step 3:** Skip `with_vector_sample` when live and borrow `&args[0]`; skip `ensure_loaded` when `is_loaded(page)`; search via `call_answer_with` and `Answer::decode`; redaction checks its memo on (annotation model `Rc`, pages `Rc`) first; history uses `Rc<HistoryEntry>` and `Rc<[Value]>`, and adds `pending_find(pred)`.
- [ ] **Step 4:** Rerun, then full vectors in `rust-plugins`. Expected: pass.
- [ ] **Step 5:** Commit per crate.
- [ ] **Step 6:** Run the Phase C pair on the mini (see Phase C header). Record in `FINDINGS.md`.

---

## Phase D: plugins that scale with annotations and fields

Phase D ends with one 10-round run on `annots.pdf` and `forms.pdf`. Rows to watch: `hover`, `dragAnnot`, `drawInk`, `typeField`, `undo`, `annotateLarge`.

### Task 13: Annotation record copies (S1, S5)

About 4 hours.

**Files:**
- Modify: `crates/plugins/annotation/src/sync/records.rs:114-116`, `:143-165`, `:167-179`, `:684-697`
- Modify: `crates/plugins/annotation-core/src/object.rs:297-301`, `:363-365`
- Modify: `crates/plugins/annotation/src/write/pointer.rs:52-72`, `crates/plugins/annotation/src/services/store.rs:469`; `crates/plugins/annotation-core/src/view.rs:273`, `:756-759`
- Modify: `crates/plugins/annotation/src/controller.rs:425-449` (delete the two no-op listeners; vectors: check)
- Test: `crates/plugins/annotation/tests/behavior.rs`, `crates/plugins/annotation-core/tests/conformance.rs`

- [ ] **Step 1: Failing test.** `put_copies_records_once`: with a clone counter like `form/tests/sharing.rs`, one `put` on a 1,000-record document copies the record map once, not four times.
- [ ] **Step 2:** `cargo test -p epdf-plugin-annotation --test behavior`. Expected: fail.
- [ ] **Step 3:** Build `Value::Object(Map)` by `insert`, moving `byk` and the order. Skip `predict_records` after the last op in `undo_of`. `Order::print` takes with `values.get_mut(i).and_then(Option::take)`. Add a defaulted `Json::is_present`. Build `Message::EditPointer` directly. Borrow the draft in `page_items`. Delete the two listeners.
- [ ] **Step 4:** Rerun, plus `annotation-core` `conformance`, plus full vectors. Expected: pass, dumps byte-identical.
- [ ] **Step 5:** Commit `annotation: move record maps instead of copying them`.

### Task 14: Hit testing, ink and drag (S2, S3)

About 1 day.

**Files:**
- Modify: `crates/plugins/annotation-core/src/kinds.rs:766-775`, `:997-1006`; `hit.rs:29-56`, `:86-127`; `view.rs:100-122`, `:205-272`; `update/draw.rs:215-220`; `update/edit.rs:243-276`; `snap.rs:65-137`
- Test: `crates/plugins/annotation-core/tests/behavior.rs`

**Interfaces:**
- Produces: `kind_named(name) -> Cow<'static, AnnotationKind>` from a `LazyLock` table; `painted_of` computes kind, style, shape and mode once and passes them to `is_filled` and `hit_stroke_of`; a snap-target cache keyed on `(Rc::as_ptr(&model.by_id), page, ids)`.

- [ ] **Step 1: Failing tests.** `kind_named_returns_static_for_known_kinds`; `hit_test_reads_kind_once_per_record` (debug counter); `move_drag_reuses_snap_targets` (second sample is a cache hit); `ink_draft_move_does_not_clone_strokes`.
- [ ] **Step 2:** `cargo test -p epdf-annotation-core --test behavior`. Expected: fail.
- [ ] **Step 3:** Implement, plus a `HashSet` of draft ids per `page_items` call and `CreateInk` built field by field.
- [ ] **Step 4:** `cargo test -p epdf-annotation-core`. Expected: pass, `conformance` unchanged.
- [ ] **Step 5:** Commit `annotation-core: compute hit inputs once and cache snap targets`.

### Task 15: Memoized reads print once (S4, S6, S14, S16)

About 1 day.

**Files:**
- Modify: `crates/plugins/annotation/src/read/render.rs:66-87`, `:252-312`; `read/chrome.rs:185-206`; `harness/mod.rs:620`, `:729`, `:742`; `comments/threads.rs:40-51`
- Modify: `crates/plugins/form/src/harness/mod.rs:243-248`
- Modify: `crates/plugins/outline/src/harness.rs:126-133`
- Modify: `crates/plugins/signature/src/harness.rs:342-353`
- Test: each crate's `behavior.rs` and `conformance.rs`

**Interfaces:**
- Consumes: `TextMemo` (Task 4). Page-read memos hold `Rc<Value>`. `lent_scenes` results that contain `$function` stay on the old path.

- [ ] **Step 1: Failing tests.** `list_page_items_shared_on_hit` (`Rc::ptr_eq`); `appearance_epoch_memoized_per_page_model`; `outline_list_printed_once_per_change`; `signature_list_printed_once_per_change`.
- [ ] **Step 2:** Run. Expected: fail.
- [ ] **Step 3:** Implement. Key the threads memo on the pages `Rc`.
- [ ] **Step 4:** Rerun all four crates' tests. Expected: pass.
- [ ] **Step 5:** Commit per crate.

### Task 16: Form and actions (S6, S7, S10)

About 1 day. Vectors: check (S7).

**Files:**
- Modify: `crates/plugins/form/src/services/looks.rs:41`, `:134-150`; `services/pictures.rs:20-61`; `model.rs:418-420`, `:647`; `ffi.rs:320`, `:430`
- Modify: `crates/plugins/signature/src/ffi/records.rs:22-24`
- Modify: `crates/plugins/actions/src/services/annotation_rows.rs:16-22`, `:52-56`, `:80`
- Test: `crates/plugins/form/tests/{behavior.rs,sharing.rs,ffi.rs}`, `crates/plugins/actions/tests/behavior.rs`

- [ ] **Step 1: Failing tests.** `shown_widgets_memo_hits_on_repeat` (`Rc::ptr_eq`, memo keyed on view `Rc`, truth `Rc`, `formatted.version()`, lending); `redraw_shares_positions` (`Rc::ptr_eq(by_key)` across a redraw); `page_trigger_rows_do_not_clone_all_widgets` (clone counter equals kept rows).
- [ ] **Step 2:** Run. Expected: fail.
- [ ] **Step 3:** Implement; `T::deserialize(value)` at the four `from_value(clone)` sites.
- [ ] **Step 4:** Rerun, then full vectors. Expected: pass.
- [ ] **Step 5:** Commit per crate.

### Task 17: Measurement readout (S15)

About 3 hours.

**Files:**
- Modify: `crates/plugins/measurement/src/read/scale.rs:80-86`; `crates/plugins/measurement/src/sync/viewports.rs:86-97`; an `Rc` record accessor in `crates/plugins/annotation/src/read/`
- Test: `crates/plugins/measurement/tests/api.rs`

- [ ] **Step 1: Failing test.** `readout_computed_once_over_two_reads`.
- [ ] **Step 2:** `cargo test -p epdf-plugin-measurement --test api`. Expected: fail.
- [ ] **Step 3:** Memo keyed on the record `Rc`; `getPageScale` checks its memo before cloning viewports.
- [ ] **Step 4:** `cargo test -p epdf-plugin-measurement`. Expected: pass.
- [ ] **Step 5:** Commit. Then run the Phase D pair on the mini and record it.

---

## Phase E: Rust engine (native hosts)

No browser signal. Each task asserts PDFium call counts (Task 5); Task 28 times them later.

### Task 18: Open without loading every page (E1)

About 3 hours. Vectors: check (a corrupt page no longer fails open, which moves toward TS).

**Files:**
- Modify: `crates/epdf-engine/src/access.rs:195-207`; `crates/epdf-engine/src/session/document.rs:686-711`, `:714-723`
- Test: `crates/epdf-engine/tests/document_session.rs`, `pages_mutate.rs`, `page_structure.rs`

- [ ] **Step 1: Failing test.** `open_400_pages_loads_no_page`: `get(Call::LoadPage) == 0` after open.
- [ ] **Step 2:** `cargo test -p epdf-engine --features pdfium/call-count --test document_session`. Expected: fail.
- [ ] **Step 3:** Use `document.page_object_number_by_index(i)` in both loops, read `page_count()` once, and look up `record_by_object_number` through `OwnedDocument::page_index`.
- [ ] **Step 4:** Rerun, plus `pages_mutate`, `page_structure`, and the engine vector corpus. Expected: pass.
- [ ] **Step 5:** Commit `epdf-engine: read page object numbers without loading pages`.

### Task 19: One serialization per answer, and borrowed decodes (E2, E9, E10)

About 4 hours. Vectors: check (serde error text).

**Files:**
- Modify: `crates/epdf-engine/src/host.rs:3094-3113`, `:3293`; `crates/epdf-engine/src/page_space/boundary.rs:194`, `:211`, `:378`, `:1017-1030`; `crates/epdf-engine/src/features/changes/op.rs:810`; `features/changes/fingerprint.rs:14-48`
- Test: `crates/epdf-engine/tests/{wire_contract.rs,geometry_corpus.rs,pages_corpus.rs,change_ops.rs}`

- [ ] **Step 1:** Confirm the four test files pass at HEAD and record `wire_contract.rs` error strings.
- [ ] **Step 2:** `HostResponse` carries the result `Value` to the boundary and `route_job` prints once. `T::deserialize(value)` in place of `from_value(clone)` at the listed sites. Fingerprint hashes over `encode_utf16()` without a `Vec<u16>`.
- [ ] **Step 3:** Rerun. Expected: byte-identical corpora, same error strings, `fingerprint.rs` reference values unchanged.
- [ ] **Step 4:** Commit `epdf-engine: serialize each answer once`.

### Task 20: Fewer form and signature model loads (E3, E4)

About 4 hours.

**Files:**
- Modify: `crates/epdf-engine/src/features/forms/write.rs:88-92`, `:284`, `:460-461`, `:495`, `:525`, `:545`; `features/changes/forms.rs:605`; `features/forms/widgets.rs:90`; `features/changes/fields.rs:863`
- Test: `crates/epdf-engine/tests/{form_write.rs,form_apply.rs,signature_protection.rs}`

- [ ] **Step 1: Failing tests.** `set_value_loads_model_at_most_twice`; `reset_loads_model_once`; `named_reset_builds_signature_model_once`.
- [ ] **Step 2:** Run with `call-count`. Expected: fail.
- [ ] **Step 3:** Pass the model the caller already holds; hoist `self.field_locks()` out of `named_targets` as `whole_form_targets` does.
- [ ] **Step 4:** Rerun. Expected: pass.
- [ ] **Step 5:** Commit.

### Task 21: Search corpus cache and one regex compile (E5)

About 1 day.

**Files:**
- Modify: `crates/epdf-engine/src/features/search/reader.rs:218-261`; `features/search/mod.rs:240-241`; `text/charmap.rs:92-94`; `search/snippet.rs:33`; `search/literal.rs:62-89`
- Test: `crates/epdf-engine/tests/{search_reader.rs,search_slice_protocol.rs,search_dialect.rs}`

**Interfaces:**
- Produces: a per-session cache of `(PageTextSnapshot, FoldedText, Vec<u16>)` keyed on `(page, mutation_sequence())`, capped at 512 pages, dropped on install and close.

- [ ] **Step 1: Failing tests.** `repeat_query_loads_no_text_page`; `query_after_edit_reloads_page` (Review Focus 4: edit page 2, query again, `TextLoadPage` count rises and hits reflect the edit); `regex_compiled_once_per_query`.
- [ ] **Step 2:** Run with `call-count`. Expected: fail.
- [ ] **Step 3:** Implement; compile the regex once in `search()` and pass it to `match_regex`; index UTF-16 offsets instead of rescanning per match.
- [ ] **Step 4:** Rerun all three, plus `vectors/search`. Expected: pass.
- [ ] **Step 5:** Commit.

### Task 22: Annotation lookup by index (E6)

About 4 hours. Vectors: check.

**Files:**
- Modify: `crates/pdfium/src/annot.rs` (new wrappers); `crates/epdf-engine/src/features/annotation/write/resolve.rs:81-97`; `write/create.rs:320-325`; `read/relationship.rs:76-96`
- Test: `crates/epdf-engine/tests/{annotation_update.rs,annotation_delete.rs,annotation_create.rs}`

**Interfaces:**
- Produces: `Document::raw_annotation_index_by_object_number(page, objnum) -> Option<usize>` (`EPDFPage_GetAnnotIndexByObjectNumberRaw`) and `raw_annotation_index_by_name(page, &str) -> Option<usize>` (`EPDFPage_GetAnnotIndexByNameRaw`).

- [ ] **Step 1:** Confirm the pinned `apple-engine-572d1dc31f32b352` and the container's PDFium export both symbols (`nm`). If either lacks them, stop and report.
- [ ] **Step 2: Failing test.** `update_makes_no_annot_probes`: `get(Call::GetAnnotRaw) == 0` for the lookup.
- [ ] **Step 3:** Add the wrappers and use them in `index_of` and `name_taken`. `deleted_with` builds a `HashMap<String, usize>` once and looks up by `&str`.
- [ ] **Step 4:** Rerun the three files plus `relationship.rs` unit tests. Expected: pass.
- [ ] **Step 5:** Commit.

### Task 23: Fewer byte copies (E8)

About 3 hours.

**Files:**
- Modify: `crates/pdfium/src/base.rs:101-103`, `document.rs:51-53`, `page.rs:101`, `:406`, `:470`; `crates/epdf-engine/src/host.rs:1235`, `:1280`, `:3632-3635`
- Test: `crates/pdfium/tests/documents.rs`; `crates/epdf-engine/tests/{render_pixels.rs,annotation_appearance.rs,thumbnail_render.rs,document_session.rs}`

- [ ] **Step 1: Failing test.** `open_from_vec_takes_ownership`.
- [ ] **Step 2:** Add `BaseDocument::from_vec` and `Document::from_vec`, use them in `open_with` and `install`; skip `FPDFBitmap_FillRect` on the transparent branch (buffer is already zero); move payloads instead of `to_vec()`.
- [ ] **Step 3:** Rerun. Expected: pixel-identical renders.
- [ ] **Step 4:** Commit.

---

## Phase F: native hosts

### Task 24: Shared review gate for Phase E

About 1 hour. Run the full engine vector corpus and `crates/epdf-viewer-host` tests in `rust-plugins`; record PDFium call counts before and after Tasks 18 to 23 for open, `pages.list`, `forms.setValue`, `search.query` and `annotations.update` on a 400-page file in `FINDINGS.md`.

### Task 25: Cancel queued native calls (N1a)

About 4 hours.

**Files:**
- Modify: `crates/epdf-viewer-host/src/engine_worker.rs:59-75`, `:89-94`; `crates/epdf-viewer-host/src/native_engine.rs:145-156`, `:937-947`
- Test: `crates/epdf-viewer-host/src/native_engine.rs` tests near `the_worker_takes_a_working_set_in_submission_order` (`:1341`)

- [ ] **Step 1: Failing tests.** `pre_cancelled_call_never_runs` (the host's call counter does not see it; the other replies keep submission order). `cancelled_call_settles_with_abort` (Review Focus 5: the caller's future resolves with the abort error).
- [ ] **Step 2:** `cargo test -p epdf-viewer-host`. Expected: fail.
- [ ] **Step 3:** `Request::Call` carries an `epdf_engine::CancelToken`; `with_abort` calls `cancel()`; the loop skips cancelled jobs and runs the rest with `host.call_with_cancel`.
- [ ] **Step 4:** Rerun. Expected: pass.
- [ ] **Step 5:** Commit.

### Task 26: Swift reader renders and observation (N2, N3, N9)

About 1 day, Mac host.

**Files:**
- Modify: `platforms/apple/EmbedPDF/Sources/EmbedPDFUI/StageReaderView.swift:19-24`, `PageImageView.swift:57`, `StageReaderModel.swift:39-45`, `:75-79`, `:127-143`, `:181-188`
- Modify: `apply` in `OutlineModel.swift`, `AttachmentModel.swift`, `SearchModel.swift`, `HistoryModel.swift`, `CapabilityObservation.swift`
- Test: `PageRenderResolutionTests.swift`, `StageReaderDragLagTests.swift`, `OutlineModelTests.swift`, `HistoryModelTests.swift`

- [ ] **Step 1: Failing tests.** `scalesWithinOneBandShareARender` (quantized through `PageRenderResolution.deviceWidthStep`, 128 px); `unchangedRevisionDoesNotNotify` (`withObservationTracking` stays quiet).
- [ ] **Step 2:** `swift test` in `platforms/apple/EmbedPDF`. Expected: fail.
- [ ] **Step 3:** Quantize `renderScale`; compare before assigning in each `apply`; drop `refresh()` from `applyDragChange` and take the camera from `cameraChanged`.
- [ ] **Step 4:** Rerun, including `a120HzDragKeepsUp`. Expected: pass.
- [ ] **Step 5:** Commit.

### Task 27: Native reply parsing, C# and the C API (N6, N7)

About 4 hours.

**Files:**
- Modify: `crates/epdf-viewer-host/src/native_engine.rs:145-151`, `:166-179`; `translate.rs:761-763`
- Modify: `platforms/dotnet/EmbedPDF/PdfDocument.cs:22-27`
- Modify: `crates/epdf-capi/src/operations.rs:142-144`; `crates/epdf-capi/src/ffi.rs:329-337`
- Test: `crates/epdf-viewer-host/tests/translate.rs`; `crates/epdf-capi/tests/{engine_reads.rs,lifecycle.rs}`; `EmbedPDF.Tests/FacadeTests.cs`

- [ ] **Step 1: Failing tests.** `malformed_reply_is_rejected_off_kernel_thread`; `failing_open_still_fails_with_probe`; C# `RepeatedRenderListsPagesOnce`.
- [ ] **Step 2:** Run. Expected: fail.
- [ ] **Step 3:** Parse the reply on the client thread and pass the envelope to `translate::decode`. C# caches the first `GetPagesAsync`. The C API parses only for operations 1 and 7 and checks `error` with `struct Probe { error: Option<IgnoredAny> }`.
- [ ] **Step 4:** Rerun. Expected: pass.
- [ ] **Step 5:** Commit per layer.

### Task 28: Native engine timer, and LTO on native archives

2 to 3 days. Needs the native PDFium build.

**Files:**
- Create: `tooling/engine-timer/` (release binary over `epdf_engine::Host::call`)
- Modify: `platforms/apple/build-xcframework.sh:129-131`; `platforms/dotnet/README.md:93`

- [ ] **Step 1:** The timer sends open, `pages.list`, `pages.geometry`, render, `search.query`, `annotations.list` and `forms.setValue` to `Host::call` on `large.pdf`, `annots.pdf` and `forms.pdf`; checks each answer against the engine vectors before timing; rotates two binaries A/B per round; reports the median of 10 rounds and the A/A spread.
- [ ] **Step 2:** Time `plugins-base` (Task 3 baseline) against the Phase E tip. Record in `FINDINGS.md`.
- [ ] **Step 3:** Replace `cargo build` with `cargo rustc -p epdf-viewer-host --target "$rust" --profile "$PROFILE" --crate-type staticlib`; change the C# documented command to `cargo rustc -p epdf-capi --release --crate-type cdylib`. Confirm `-C lto` appears in a `-v` build log.
- [ ] **Step 4:** Time LTO against non-LTO with the timer. Keep it only if a row moves past its threshold; record either way.
- [ ] **Step 5:** Full Swift suite on the new archive (Mac). Commit.

### Task 29: Correct stale numbers in the plan doc

About 30 minutes.

- [ ] Update `multiplatform-plan.md:64-69` to the current `RESULTS.md` values (4.002 ms, 562.98 µs), drop "rerun before gating" from `:67`, and note that the "V8 beats Rust wasm" suite built Rust with `opt-level = "z"` and no `wasm-opt`, and that the "+1 to 4%" shim tax is below that suite's 5% floor. Commit.

### Task 30: `packages/core-wasm` bridge fast paths (W8, W9, A1-F7)

About 4 hours. Part of Phase C: run it before the Phase C timing pair. `packages/core-wasm` is port code, not the protected upstream TS.

**Files:**
- Modify: `packages/core-wasm/src/engine-bridge.ts:365-382` (`decode` returns the input when `text.includes('"$')` is false); `packages/core-wasm/src/proxy.ts:783-828` (primitive-args cache key); `packages/core-wasm/src/kernel.ts:269-276` plus `crates/epdf-web/src/exports.rs:508-516` (`settings_revision(token)` export)
- Test: `packages/core-wasm/test/{rehydrate.test.ts,kernel.test.ts}`; `crates/epdf-web/tests/exports.rs`

Tests: `decode returns the same object for markerless text`; `cache key equals JSON.stringify for primitive args`; `getSettings crosses once per settings revision`.

---

## Findings implemented

| Summary ID | Detail IDs | Task |
| --- | --- | --- |
| Harness | `--against`, threshold column, `p95`, re-pin, `main-now`, `RESULTS.md`, fixtures, flows, mid-document scroll | 1, 2, 3 |
| Build | web LTO re-time; Apple and C# LTO | 2, 28 |
| W1 | P5-F1, P5-F2, P5-F10 | 7 |
| W2 | A1-F3, P4-02 | 8 |
| W3, W4 | A1-F1, A1-F11, P4-01, P5-F11, P2-F5, P3-F8 | 6 |
| W5, W6, W11 | P4-03 (1-4), P4-04, P4-05, P4-07, P4-10, P4-12a | 9 |
| W9, W11 | A1-F5a, A1-F8 | 10 |
| W10 | P6-2, P6-4, P6-5, P6-7 | 11 |
| W7, S9, S11 | P5-F4a, P5-F5, P5-F9, P5-F3, P6-3, P6-12 | 12 |
| S1, S5 | P2-F1.1, P2-F1.2, P2-F7, P1-F4.1, P1-F4.2, P1-F10, P1-F12a | 13 |
| S2, S3 | P1-F1a, P1-F2, P1-F3a, P1-F5, P1-F6 | 14 |
| S4, S6, S14, S16 | P2-F2, P2-F3, P2-F6, P2-F9 (threads memo), P3-F1b, O-1, S-3 (reads) | 15 |
| S6, S7, S10 | P3-F1, P3-F4.1, P3-F7a, P3-F7b, P7-F3 | 16 |
| S15 | M-1, M-2 | 17 |
| E1 | A2-F1, A3-F1 | 18 |
| E2, E9, E10 | A2-F2 step 1, A3-F2, A4-F6, A2-F6a, A5-3 (op and fingerprint) | 19 |
| E3, E4 | A5-1a, A5-9 / A6-F1a (`named_targets`) | 20 |
| E5 | A3-F3, A3-F4, A3-F5 | 21 |
| E6 | A4-F1, A4-F2 | 22 |
| E8 | A2-F3a, A2-F8, A3-F8, A4-F8, A2-F10c | 23 |
| N1 | N1 (a: cancellation) | 25 |
| N2, N3 | N2, N3 (compare before assign), N9 | 26 |
| N6, N7 | N5 (parse off the kernel thread), N6, N7 | 27 |
| Native timer | summary step 3, Rust side only | 28 |
| Older suites | `multiplatform-plan.md` stale numbers | 29 |
| W8, W9 (bridge) | A1-F2, A1-F4, A1-F7 | 30 |

## Findings left out, and why

Changes what the vectors pin, or when TS computes something. These need a TS change or a vector change the branch rules don't allow: P4-03 step 5 (early return on unchanged demand reorders fetches), P5-F4b (`enabledFor` cache, purity unstated), P6-1 (declared read-sets change callback timing), S13 / S-1 (a signature verdict cache changes the engine-call log), A3-F9 / E5 yield (changes `pagesSearched`), A4-F9 (needs a wire field TS lacks), P1-F4.3 and P1-F11 (byte-exact NaN and path spellings).

TS pays the same cost. Parity is the branch's contract, so these wait for a decision to diverge: P1-F9, P3-F2 / S8 (`canonical_effects`), P3-F6 (tab order), P5-F6, P5-F7, P7-F2, P7-F7, A3-F10, A3-F11, A4-F4 / E7 (rich-text guard, also a fork-contract risk), A5-2, A5-8, A6-F3 (outline writes), A6-F4 (rule costs), M-3.

Wide type changes that should follow the narrow memos once those are measured: P1-F1b (an `Rc` annotation with `OnceCell` fields, about 60 call sites), P1-F3b, P1-F5b, P1-F8 (`Id = Rc<str>`), P2-F1.3, P3-F1c, P3-F4.2/3, A2-F2 step 2 (typed convert), N12.

Session model caches whose staleness would be a correctness bug, deferred until an owned PDFium model handle and a pinned invalidation point exist: A5-1b (form model), A6-F1b (signature model). Task 20 takes the safe half of each.

Off by default or rare paths. Form scripting is off by default; stamps, transfer, bundles, captures and signing are per action, not per frame: P3-F5, P7-F1 (S8), P7-F4, P7-F5, P2-F8 (S12), A5-4, A5-5, A5-6, A5-7, A6-F2, A6-F5, A6-F6, A6-F7, A6-F8, A6-F9, A2-F3b, A2-F10b, A-1, O-3, P7-F6, P7-F8, P7-F9, P7-F10, S-2.

Micro or startup costs nothing can measure yet: A1-F6, A1-F8 (`Reflect::get` key), A1-F9, A1-F5b (pages identity on the wire, about 80 lines for one per-change cost), P6-6, P6-8 to P6-11, P6-13, P4-11, P4-12b to g, P5-F12, P5-F13, A2-F6b, A2-F7, A3-F12, A4-F3, A4-F5, A4-F7, A4-F10, P1-F7, P1-F12b, P2-F4, P2-F9 (rest), P3-F3, P3-F7c/d, P3-F9, S-4, M-4, F-1, O-4 (folds into Task 6 where it uses the page index), O-5 to O-7, PE-1 (same), N11, N14.

Known and already profiled as not paying, or needing their own design: A2-F9 and A3-F7 (text page per read), A3-F6 (`fold_text`), K1 / N4 (raster copies; the bridge design is undecided), P4-08, P4-09.

Native items that need the timer or Instruments first: N1b (priority heap), N3 steps 2 to 4 (coalescing `state_changed` changes the callback count), N4 / N5 (alpha flag, suspected only), N8, N10 (depends on whether `TiledPageModel` ships), N13, N17.

Harness items deferred: probe polling on Rust arms (biases against Rust only, small; documented in the README instead), a warm-visit flow, the full TS-engine-vs-Rust-engine comparison in the timer, B1 recommendations 1, 2, 4 and 5, wasm SIMD, PDFium `-flto`, `float_roundtrip` (can change parsed floats).

Excluded outright: A1-F10 (dormant code), A1-F12 (the claim that LTO applies to `epdf-web` is wrong), A2-F3c (needs a TS `wasm-runtime.ts` change, and the web ships the TS engine), A2-F10a (needs a fork change), A2-F10d (the detail file says it isn't a cost), A5-9 codec (not on a production path), A6-F9 `finalize_candidate` (a kept divergence), N16 (`epdf-uniffi` surface isn't shipped), wasm sizes in CI (the no-CI rule), Safari, Firefox and rotation balance (no rig; the measured order effect is 1.007x), `epdf-wasm` LTO (its build script is under `packages/**`, and nothing ships `epdf-wasm` on the web today).
