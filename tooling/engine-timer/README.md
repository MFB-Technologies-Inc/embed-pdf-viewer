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

`--out` writes every row, its samples and the run's metadata: a run id (also printed when the run
starts), the start and end times, the machine (host name, CPU model and count, memory, OS, Node
version, load before and after), the harness checkout's commit, and each binary's sha256 and
`engine-timer build` answer, which is the commit it was built from, whether tracked files differed
from that commit when it was built, and the `CARGO_PROFILE_RELEASE_LTO` override if one was set.

The statistics are the viewer harness's (`stats.mjs`): a bootstrap interval on the ratio of
medians, the A/A interval as the noise band, and a row's threshold is that band or 3%, whichever
is wider. A row that moves less than its threshold reads "no detectable difference".

The timer binary is a Cargo `bin`, which gets the release profile's `lto = true`. A build without
LTO is `CARGO_PROFILE_RELEASE_LTO=false cargo build -p engine-timer --release`.

Timings belong on an idle machine, one run at a time. On the M1 Mac mini, 10 rounds of three arms
took 7.4 minutes for two tip builds and 9.7 minutes with 0de4effd9 as A.
