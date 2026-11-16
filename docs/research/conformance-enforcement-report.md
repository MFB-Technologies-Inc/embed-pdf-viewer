# Rust port conformance: findings and recommendations

Examined on 2026-11-16 at `research/multi-platform-rust-core-impl-plugins` `2216e1e3b` and
`research/multi-platform-rust-core-impl` `a7c75f5ad`.

We should keep the frozen-vector design and not replace it. TypeScript generates vectors, Rust
replays them, and CI regenerates to prove the vectors still describe the TypeScript. That gives
offline Rust tests and a reviewable record of every oracle change, and no alternative we looked at
gives both. What needs work is four enforcement holes around the vectors and the storage format.
Speed doesn't: the checks were measured and are already quick.

The checks were timed in the `embedpdf-dev` container (linux/arm64, 16 cores) at `2216e1e3b`. They
are already fast, and the speed section says why we'd leave most of it alone.

## What exists today

The `-impl` branch carries the engine corpus only: `tooling/engine-conformance` (61 vector files,
3.9 MB) replayed by `crates/epdf-engine/tests`. The `-plugins` branch adds
`tooling/plugin-conformance`, replayed by `crates/plugin-conformance` and one `tests/conformance.rs`
per plugin crate.

The plugin corpus is the large one. It holds 119 vector files with 1,465 scenario cases (13,926
trace steps) and 14,450 pure-function cases. The TypeScript side has a c8 coverage gate, recorded in
`vectors/coverage.json`: 72,853 of 77,093 statements (94.5%) and 14,365 of 15,719 branches (91.4%),
with 36 ported files that no generator imports. 479 of the 1,465 scenario cases set
`snapshot: "none"`, so their final state is never compared.

Three mechanisms let Rust differ from a vector. `crates/plugin-conformance/outcome-exceptions.json`
has one entry and can only express a callback logged in a different step.
`crates/plugin-conformance/member-order-allow.json` exempts object member order at 375 paths (the
count `tooling/plugin-conformance/README.md` gives for 2026-10-13). The engine's search gate has its
own `KNOWN_DIVERGENCES` list at `crates/epdf-engine/tests/search_dialect.rs:85`.

## Enforcement holes

### 1. Replay isn't proven

`vectors:check` decides a vector file is replayed by finding its path as a string literal in some
`.rs` file under `crates/`. The README says so itself at
`tooling/plugin-conformance/README.md:32`: "A literal in a test that never runs would still pass."
The same goes for a test behind a feature CI doesn't enable, and for a run narrowed by
`PLUGIN_CONFORMANCE_ONLY`, where `assert_suite` only requires that one case ran.

Let's have the Rust replay write a receipt: per vector file, the case names it ran and the
exceptions it applied. CI then compares the receipt to the generator's case list and fails on any
case that didn't run. We think this is about half a day. The cost is one more artifact passed
between the `cargo` and `vectors` jobs in `.github/workflows/plugin-rust.yml`, which are
independent today.

### 2. Kept divergences have no single ledger

The exception file holds one entry, and the kept Rust-only divergences number around twenty. The
rest are recorded in Rust unit tests, crate READMEs and the catch-up documents. A grep of
`tooling/plugin-conformance/src/suites` for the divergence ids we know of finds only H3. So for
most kept divergences we can't say whether the vectors observe them. Either a vector avoids the
behavior, which is a coverage gap nobody chose on the record, or the divergence is invisible to the
trace, which nobody has checked.

Let's keep one ledger file. Each entry names the divergence, the reason, and either the vector cases
that observe it or the Rust test that pins it when no vector can. `exceptions.rs` already fails on a
stale entry, and that check should cover every entry. The exception format needs a second form
beyond "moved callback": a different value at a named path. The member-order allowlist should move
under the same rule, so it can shrink freely and can only grow with a ledger entry.
`pnpm run member-order:allow` currently adds every failing path with no review step.

We think this is two to three days, most of it auditing which divergences a vector could observe.
The cost is that adding a divergence gets slower, which is the point.

### 3. Coverage is measured on one side only

The c8 gate answers "which TypeScript lines do the generators reach". Nothing answers "which Rust
lines does replay reach". A Rust branch that no vector enters is behavior with no oracle, and today
it is indistinguishable from verified code.

Let's run `cargo llvm-cov` over the conformance tests alone and record the result beside
`coverage.json`. Lines reached only by Rust unit tests are the list to triage: each needs a vector,
a ledger entry, or a reason it is Rust-only plumbing. Setup is about a day. The triage is
open-ended, and the first report will be long, because FFI glue and harness adapters will show up
as unreached and have to be excluded by hand.

### 4. Every input is hand-picked

The repository has no property, fuzz or mutation testing. All 15,915 cases are examples somebody
wrote, so the corpus checks the inputs we thought of.

The 14,450 function cases are pure, which makes them cheap to test differentially. We can compile
each crate's function table to wasm, load it in Node beside the shipping TypeScript, feed both the
same generated inputs, and compare with the existing canonical rules. This stores nothing. Start
with `epdf-geometry`, where inputs are numbers and rectangles. We think that first crate is three
to five days.

This is a supplement and not a replacement. Its costs are real: a wasm build in CI, a workflow for
pinning the seed of a failure as a permanent vector, and filters for the kept divergences so the
generator doesn't rediscover them every night. Scenario cases don't fit this approach, because
their inputs are scripted engine answers and step sequences that a generator can't invent usefully.

## Space

The plugin vectors are 231 MB on disk and `tooling/plugin-conformance/historical` is another 66 MB.
Every worktree and every container checkout pays that.

Git history is not the problem. All 1,439 versions of those blobs, 1,597 MB raw, pack to 22.2 MB,
because git deltas the text well.

The files are almost entirely redundancy. The same content is 88 MB without pretty-printing and
3.8 MB under `zstd -3`, about 60 times smaller. Two sources stand out. `stateChanges` records both
`previous` and `next` for every change (26 MB compact), though `previous` is always the prior
`next`. And function cases repeat their fixtures: `core/annotation-view.json` is 6.3 MB compact for
30 cases, of which 6.1 MB is subtrees that appear more than once.

We recommend committing the vectors as `.json.zst` and reading them through the `zstd` crate as a
dev-dependency. It is the smallest change that gets the whole 60x, and it keeps `cargo test` free of
Node.

The cost is twofold. GitHub stops showing vector diffs, though it already declines to render a
14 MB JSON diff. And git can't delta compressed blobs, so each full regeneration adds roughly 4 MB
of history where it adds very little today. That number is an estimate from the compressed total,
not a measurement. To replace the lost diff, the generator should print a case-level summary of
what was added, removed and changed, which is what `docs/research/catchup-plugin-vector-diffs.md`
did by hand.

Two alternatives are worth knowing. Structural dedupe (drop `previous`, intern repeated subtrees)
keeps text diffs but changes the schema on both sides and gets less than compression does. Not
committing vectors at all, and passing them between CI jobs as an artifact keyed on the TypeScript
sources, gets the working tree to zero but makes `cargo test` depend on a Node toolchain. We don't
think that trade is worth it.

`historical/` is separate. Suites and tests under `tooling/plugin-conformance` import from it, so
it can't just be deleted. It holds 193 `.out` and 99 `.txt` files that look like captured output
and would compress the same way. We haven't checked which of them anything reads.

## Speed

Measured in the container, one run each unless noted, at whole-second resolution:

| Check | Time |
|---|---:|
| plugin `vectors:check` (119 files) | 9 s, 8 s on a second run |
| plugin `coverage:check` | 23 s |
| plugin `test:harness` | 2 s |
| engine, all 35 `:check` scripts | 61 s |
| of which `search:check` | 23.5 s, on three runs |
| Rust plugin-layer test build, cold | 50 s |
| Rust `--test conformance` across the plugin crates (136 tests) | 12 s |
| Rust `epdf-engine` test build, cold | 21 s |
| Rust `cargo test -p epdf-engine` (1,506 tests) | 15 s |

So the conformance work itself is about two minutes, and none of the speed changes we considered
carries its weight. The plugin `vectors` job does run the whole generator three times, once for
`vectors:check` and twice inside `coverage:check` (`tooling/plugin-conformance/src/coverage.ts:141`
and `:142`), but a single-pass rewrite would save about 20 seconds. Merging the engine's separate
`tsx` processes into one runner would save less: the 34 scripts other than `search:check` total
38 seconds, most of it real work.

The one thing worth a look is `search:check`. It takes 23.5 seconds every time, more than a third
of the engine total, where `scope:check` takes 0.3. We didn't profile it. The engine README
describes exponential backtracking measurements on `(?:a+)+b`, so the time may be deliberate.

On the Rust side each suite parses its whole pretty-printed file into a `serde_json::Value`, and
136 conformance tests still finish in 12 seconds. Compressed input would cut the read from 231 MB
to 3.8 MB, which is a space win and not a meaningful time win.

One failure showed up while timing. `cargo test` over the crate selection
`.github/workflows/plugin-rust.yml` uses stops at `epdf-text-core --lib` with
`error while loading shared libraries: libembedpdf.so`, exit status 127. That workflow's header
says none of these crates link PDFium, which is no longer true of `epdf-text-core`. The run aborted
there, so we have no time for the full plugin-layer `cargo test`, and the crates after it in that
run went untested. Running `--test conformance` alone passes.

## Order of work

Receipt first (hole 1), because it is half a day and every later number depends on replay being
real. Then compressed storage, since it is mechanical and frees about 227 MB per checkout. Then the
ledger (hole 2) and Rust-side coverage (hole 3), which feed each other: the coverage report shows
where divergences hide. Differential generation (hole 4) goes last, because it needs the ledger's
filters to be usable.

No speed work is on this list. The generator takes nine seconds.
