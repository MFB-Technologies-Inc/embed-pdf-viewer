# Native engine timer

Times `epdf_engine::host::Host::call` on the native PDFium build, so a change to the engine can be
measured without a browser, a worker or the wasm bridge in the way. Two builds run against each
other, and a copy of the first build against itself, so every row has an A/A noise band.

The rows are seven wire operations on the viewer harness's three fixtures (`large.pdf`,
`annots.pdf` and `forms.pdf`, from `perf-compare/fixtures` on the harness branch): `open.fatMem`,
`pages.list`, `pages.geometry`, `pages.render` at scale 1, a whole-document `search.query` for
`the` (every slice, followed by cursor), `annotations.list`, and `forms.setValue` on `forms.pdf`.
`src/rows.rs` has the list and each row's call counts. `pages.geometry` and `pages.render` read a
different page on every call, so they measure a first read rather than a cached one.

The other rows open their document once and call it again and again, so their timed calls find what
the warm-up left: the page registry `pages.list` and `annotations.list` read, and the page text a
search caches per page. Those rows time the warm path. `search.query`, `pages.list` and
`annotations.list` also have a `-cold` row (`large/search.query-cold`, and so on), which opens a fresh
host and document for every call, untimed, and times that document's first call. A change to what
an open or a first read costs shows there and nowhere else.

## Running it

Build the binary once per commit you want to time, with the fork's library fetched for the host:

```sh
bash packages/engine/runtime/scripts/fetch-libpdfium.sh darwin-arm64
cargo build -p engine-timer --release
```

A commit older than the timer (b3f41cd38) has no `tooling/engine-timer`, so it is built with the
timer laid over it, uncommitted: in a separate checkout of that commit, copy `tooling/engine-timer`
from a commit that has it, add `"tooling/engine-timer"` to the `members` of the root `Cargo.toml`,
and build as above. Cargo adds the timer to `Cargo.lock`. That is how the 0de4effd9 arm in
`perf-compare/FINDINGS.md` (on `research/viewer-perf-compare`) was built: "0de4effd9 with the timer
source copied in uncommitted". That record doesn't say which commit's timer source was copied, or
whether `Cargo.toml` was edited by hand; we infer the member line because Cargo refuses to build a
package under the workspace root that isn't a member. The timer needs only `Host::with_determinism`,
`Host::fill_envelope`, `FixedClock`, `SeededIdSource` and `test_support::corpus::quantized` from
the commit under it, and 0de4effd9 has all five. Such a build reports itself as dirty, with
`Cargo.toml`, `Cargo.lock` and the timer's files in `dirtyPaths`.

Then compare two builds:

```sh
node tooling/engine-timer/compare.mjs --a base/engine-timer --b next/engine-timer \
  --fixtures <dir with large.pdf, annots.pdf, forms.pdf> --corpus . \
  --names base,next --rounds 10 --out results.json
```

`--corpus` is a checkout, or a copy of the files `engine-timer files .` lists at the same paths,
for a machine without one. The binary finds `libembedpdf` at the path it was built against or
beside itself, so a copy of both runs anywhere with the same OS and architecture.

## What a run does

Each build's answers are checked first. `engine-timer check` sends every operation through
`Host::call` on the conformance corpus and compares the answer with
`tooling/engine-conformance/vectors`, as the engine's own vector tests do, and a build that
disagrees isn't timed. Rendering and search have no wire-level vectors: a render is checked
against the sizes in `render/geometry.json`, and a search against the occurrences in
`pages.text/report.json`'s recorded text.

Every round then runs A, the A/A copy and B as separate processes, in an order that rotates by
round. Only the `Host::call` itself is inside the clock. A row's sample for a round is the median
of its calls, and the table reports the median over rounds, with the slowest round in brackets.
Each row's answers are digested, numbers at the corpus quantum (1e-4 point) and payloads byte
for byte, and the run stops if the builds answer any row differently, because a wrong answer can
be a fast one. The host's clock and id source are fixed, so a build answers the same bytes on
every run.

Each row also has a raw digest of the answers exactly as the host wrote them. When two builds
agree at the quantum and differ in the raw digest, the run goes on, prints a warning naming those
rows and records them as `belowQuantum` in the `--out` file. `engine-timer round <fixtures> --dump
<dir>` writes every answer, to diff the two builds.

The one such difference we know of is between 0de4effd9 and later commits. 0de4effd9 prints
`annotations[348].rect.width` on `annots.pdf` as `310.2000122070313` where later commits print
`310.20001220703125`, and some `pages.geometry` glyph widths move in their last digits the same
way. bfe5584c8 ("serialize each answer once") caused it. Before it, the page-space boundary
printed each answer in file space and parsed the text back with `serde_json::from_str` before
converting it (`convert_result` in `crates/epdf-engine/src/page_space/boundary.rs` at
bfe5584c8^). The timer links `epdf-engine` and `test-support` only, so its `serde_json` has no
`float_roundtrip`, and that parse can land one ULP off. We checked the annotation by experiment
with `serde_json` 1.0.151: its `/Rect` is `[54 727 364.2 739]`, the f32 right edge
`364.20001220703125` parses back as `364.2000122070313` without `float_roundtrip` and exactly with
it, and `right - left` then gives the two widths above. The glyph widths went through the same
parse; we did not check them value by value. The later value is the exact widening of PDFium's
f32, which is also what the TypeScript engine prints. A host that links `epdf-kernel` or a plugin
crate turns `float_roundtrip` on for the whole build, so the old parse moved nothing there.

`--out` writes every row, its samples and the run's metadata: a run id (also printed when the run
starts), the start and end times, the machine (host name, CPU model and count, memory, OS, Node
version, load before and after), the harness checkout's commit, and each binary's sha256 and
`engine-timer build` answer. That answer is the commit the binary was built from; `dirty`, whether
the files it is built from (`Cargo.toml`, `Cargo.lock`, `crates` and `tooling/engine-timer`)
differed from that commit, staged, unstaged or new; `dirtyPaths`, which ones; `diff`, an FNV-1a
digest of their `git diff HEAD`, so two dirty builds can be told apart; `rustc`, the compiler's
`rustc -V`; and the `CARGO_PROFILE_RELEASE_LTO` override if one was set. The build script reruns
on every build, so an edit Cargo relinks is never stamped with an older answer. The cost is that
`cargo build` recompiles and relinks the timer every time, about 30 s for a release build in the
dev container.

The statistics are the viewer harness's (`stats.mjs`): a bootstrap interval on the ratio of
medians, the A/A interval as the noise band, and a row's threshold is that band or 3%, whichever
is wider. A row that moves less than its threshold reads "no detectable difference".

The timer binary is a Cargo `bin`, which gets the release profile's `lto = true`. A build without
LTO is `CARGO_PROFILE_RELEASE_LTO=false cargo build -p engine-timer --release`.

Timings belong on an idle machine, one run at a time. On the M1 Mac mini, 10 rounds of three arms
took 9.7 minutes with 0de4effd9 as A (the run started 2026-11-17T07:55:56.639Z, and the next one
at 08:05:43.158Z) and about 8 minutes for two tip builds (08:05 to 08:13 UTC). Both runs are in
`perf-compare/FINDINGS.md` on `research/viewer-perf-compare`; their JSON, which holds the exact end
times, isn't committed.
