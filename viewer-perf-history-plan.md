# Viewer perf history: design, plan and progress

This is the working document for the perf history: what it is, how it's built, what's done and
what's next. A session picking this up should read "Status" first, then the task it names.

Branch: `research/perf-history`, cut from `research/viewer-perf-compare` at `46d457847`. The work
happens in the `embed-pdf-perf` dev container, in the worktree `/workspaces/wt-perf-history`. No
Node, TypeScript or JavaScript runs on the Mac host.

## Status

Last updated 2026-11-18.

| Task | State |
| --- | --- |
| 1. Archive the old runs | done, `a745832fc` |
| 2. `lib/rows.mjs` | done, `6b06dc884` |
| 3. `lib/history.mjs` | done, `5426ee3fc`, extended in `db5605bee` |
| 4. `scripts/history-points.mjs` and `history/points.json` | done, `66b7c9032`: 19 points |
| 5. `scripts/history-record.mjs`, first run recorded | done, `db5605bee` |
| 6. `scripts/history-chart.mjs` and the page | done, `0b7f7c7a0`: 3 of 19 points plotted |
| 7. README section | done, `3ca600b8f` |
| 8. Build every point | not started. Next. Needs no Mac mini time |
| 9. Measure the points on the Mac mini | blocked: the Mac mini is busy, wait for the user's word |
| 10. Merge into the harness branch | blocked on review |

Nothing on this branch is pushed yet: it exists only in the `embed-pdf-perf` container's clone.
Pushing and merging need the user's confirmation.

### Progress log

- 2026-11-18: a temporary SSH key for the backfill, so a batch doesn't wait on a password-manager
  unlock. It is in the `embed-pdf-perf` container at `~/.ssh/perf-mini-temp` (outside the repo, no
  passphrase), authorized on the Mac mini with `restrict` and `expiry-time="202611202359"`, and
  used through the `perf-mini` alias in the container's `~/.ssh/config`, with the host key pinned in
  `~/.ssh/known_hosts`. So Task 9 runs `PERF_REMOTE=perf-mini node scripts/remote.mjs ...` from the
  container. `rsync` was missing from the container and is now installed with `apt-get`; a rebuilt
  container needs both again. After the last batch: delete the line ending
  `perf-history-temp-2026-11-18` from `~/.ssh/authorized_keys` on the Mac mini, delete
  `~/.ssh/perf-mini-temp*` in the container, and check that `ssh perf-mini true` is refused. If the
  backfill runs past 2026-11-20 the key has expired and needs a new one.
- 2026-11-18: the Mac mini also hosts a GitHub Actions runner, a LaunchAgent in `gui/<uid>` with
  its directory at `~/actions-runner`. `scripts/remote.mjs` now starts every remote run through
  `scripts/remote-guard.sh`, which waits for a running job to end, stops the runner service, runs,
  and starts the service again (README, "Timing on another machine"). Tested against a fake
  `launchctl` and `pgrep` in `test/remote-guard.test.mjs`; the real stop and start on the Mac mini
  have not been exercised yet, so watch the first lines of the first batch's log for
  `runner: stopping` and its end for `runner: starting`.
- 2026-11-18: Tasks 2 to 7. 110 tests pass (`node --test test/*.test.mjs`, 4 skipped as before).
  `history-points.mjs` chose 19 points: 12 milestones and 3 weekly commits of the port
  (`dfd2937d4`, `920a52876`, `bac82a2cd`), and 4 of upstream. 12 reuse an arm `arms.json` already
  had and 7 got a `hist-<sha9>` arm. Recorded `2026-11-18T05-05-31-149Z-macmini2020-desktop`, which
  measures 3 points: `main` (`2516e2786`), `main-now` (`ed064e0c8`) and `perf-fixes-end`
  (`a6d4d5d14`). The page was opened once in Chrome from `history/index.html`: the charts draw, the
  console is clean. 16 points are left to build and measure.
- 2026-11-18: added `since` to `points.json` (`2026-11-18T00:00:00.000Z`) after
  `2026-11-17T16-59-17-202Z` passed every other check. That run predates the probe sampling once a
  frame (`096115a21`) and times `hover` and `drawInk` as one row each, so its rows aren't the rows
  the later runs have. `phase-c-end` and `phase-d-end`, which it measured, are in the backfill.
- 2026-11-18: gathered the old runs from the laptop (58), the Mac mini (7 more, the November runs)
  and the `embed-pdf-perf` container (7 more, all `c6b3fab5a0a0` container runs), 72 in all, and
  committed them under `perf-compare/history/archive/` (`a745832fc`). A scan of every file for
  home paths, user names, addresses and e-mail found none. The files do carry machine nicknames
  (`macmini2020`, `2023MBP16Grey`, `mac`), as `perf-compare/RESULTS.md` already does.

## What we want

A plot of every row of the perf report over time, one line per row, so a change in the port's
speed shows up as a step in a line instead of as two reports read side by side. A row that didn't
exist at first starts where it first appears. Rows where higher is better are charted apart from
rows where lower is better.

`perf-compare/RESULTS.md` can't give that. It holds one run and is overwritten by the next. Git
has 11 versions of it, each from a different set of arms. The raw data behind it
(`results/<run-id>/samples.jsonl`, `meta.json`, `gate.json`) is a better format, but `results/` is
gitignored, so before this work the runs lived on three machines and nowhere else.

## Why the old runs aren't the history

The 72 old runs span four hosts, two CPU profiles, 3-round and 10-round runs, and at least three
probe versions. `report.mjs` refuses to compare two runs for that reason, and `summary.mjs --drift`
refuses a pair whose probe hash differs. A line drawn through them would step where the machine or
the probe changed. So they are kept as a record in `history/archive/` and never plotted.

The history is measured again: chosen commits, built with today's harness and timed on one machine
(the Mac mini, `macmini2020`, Apple M1), 10 rounds, profile `desktop`. It begins at `since` in
`points.json`, 2026-11-18, the first day of runs with the probe sampling once a frame.

## Design

### Every point carries its own baseline

A history run times its points next to `main` and `main-aa`, as every standard run does. A point's
plotted value is its ratio to `main` (`2516e2786`) in the same run, or its difference for the rows
`report.mjs` treats as differences. `main` against `main-aa` gives that run's noise band. A change
in the OS, the browser or the probe moves `main` too, so the ratio survives it, and points from
different runs can share a line. The absolute medians are kept and can be shown, but a line of
absolute values is only trusted inside one run.

The cost: two extra arms in every run, about 42 min at 21 min an arm, and a history that reads
"relative to `main` at `2516e2786`", not in milliseconds. If `main` ever stops building, the
reference has to move and the old ratios need a bridge run with both references in it.

### Which commits

`history/points.json` lists them. A point belongs to a series:

- `plugins`: the first-parent line of `research/multi-platform-rust-core-impl-plugins`, from
  `bef223d9d` (2026-09-29, the first commit the harness measured). Recipe `rust-kernel`, so each
  commit is built the way it shipped.
- `upstream`: upstream `main` at the commits the port caught up to. Recipe `typescript`.

Points come from two sources. Milestones are written by hand, with a note saying why the commit
matters: a fix merged, a catch-up landed, a phase of `research/perf-fixes` ended. Interval points
are added by `scripts/history-points.mjs`: one target every 7 days from the series' `from` date,
taking the last first-parent commit dated on or before the target. A target with a point already
within 1 day of it adds nothing. `upstream` has no interval, only milestones.

Seven days is the pick because the port's history is 7 weeks long and bursty. Weekly targets plus
the milestones give 15 `plugins` points, 3 of them from the interval alone, and `upstream` has 4.
A point costs about 21 min of Mac mini time. 3 of the 19 are measured already, so the backfill is
16 arms, 5.6 h, plus `main`, `main-aa` and `main-now` in each batch: about 8.8 h over three
batches.

A point names the arm that builds it. Where `arms.json` already has an arm at that commit with the
series' recipe and kernel (`plugins`, `plugins-tip7`, `perf-fixes-end` and others), the point
uses it. Otherwise `history-points.mjs` adds a worktree and an arm named `hist-<sha9>`.

Some commits won't build. `plugins-base` (`9d6c2eca1`) can't be built in the arm64 container
(`perf-compare/README.md`, "The standard run"). Task 8 finds these; a point that can't be built
keeps its entry with a `skip` reason, so nobody tries it twice.

### What is stored

```
perf-compare/history/
  archive/<run-id>/   the 72 old runs. A record. Never plotted.
  runs/<run-id>/      the runs the charts are built from: samples.jsonl, gate.json, meta.json
  points.json         the series and their points
  data.json           generated: every row's values per point
  index.html          generated: the charts, self-contained
```

`scripts/history-record.mjs` copies a finished run from `results/` to `history/runs/` after
checking it qualifies: started on or after `since`, `macmini2020`, Apple M1, profile `desktop`, at
least 10 rounds, not `--quick`, a clean harness, and `main` and `main-aa` both timed and through
the gate. A run that
fails prints each reason and copies nothing.

Raw samples are committed, not summaries. A run is 30 to 300 KB and packs to about a seventh of
that; the repo's pack is 1.30 GiB. Keeping the samples means a later change to the statistics
re-reads history instead of losing it.

`index.html` and `data.json` are generated but committed, because the Mac host can't run the
script that makes them and the page should open from a checkout.

### The page

One small chart per row, grouped by direction and then by flow. Every row today is lower-is-better
(`lib/stats.mjs`: "Every metric is lower-is-better"), so there is one direction group now; `better`
in `lib/rows.mjs` is where a higher-is-better metric would be declared. The x axis is commit time (`date` in `points.json` is the UTC
commit time to the second, so three points on one day keep their order).
The y axis is the ratio or difference to `main`, with the bootstrap interval as a bar on each
point. A point is drawn solid when it differs from `main` by more than the run's noise band and
hollow when it doesn't. A toggle switches to absolute medians, with `main`'s median from the same
run dashed beside them. A point's note shows on hover, and each chart has a table. Bundle sizes are rows too, from
`meta.json`'s `sizes`, without intervals.

When a point was measured in more than one run, the latest run wins.

`undo` rows have no `main` to compare with (`main` at `2516e2786` has no history plugin), so they
plot as absolute medians only.

## Working in the container

```sh
docker exec -it embed-pdf-perf bash
cd /workspaces/wt-perf-history/perf-compare
node --test test/*.test.mjs
```

`node_modules` there is a symlink to `/workspaces/wt-harness/perf-compare/node_modules`
(untracked). The container's clone has no local plugins branch; `history-points.mjs` reads it from
a remote, so fetch first:

```sh
P=research/multi-platform-rust-core-impl-plugins
git fetch --no-recurse-submodules host "refs/heads/$P:refs/remotes/host/$P"
```

A file brought in with `docker cp` arrives owned by root. `docker exec -u root embed-pdf-perf chown
-R node:node /workspaces/wt-perf-history/perf-compare` fixes it. The branch exists only in the
container's clone until it is pushed.

## Constraints

- Node, TypeScript and JavaScript run in the dev container only.
- No timed run on the Mac mini until the user says it's free. One run at a time there.
- No Node dependencies beyond what `perf-compare/package.json` has. The page uses no library.
- Tests are `node --test test/*.test.mjs`, run from `perf-compare/`.
- Commit messages follow the branch: `perf-compare: <what changed>`, no attribution trailers.
- Nothing in the repo names another project, a person or a private address.
- Push to `fork` only, and only after the user confirms.

## Tasks

### Task 1: archive the old runs (done)

`perf-compare/history/archive/<run-id>/` for all 72 runs. `a745832fc`.

### Task 2: `lib/rows.mjs`

`report.mjs` keeps `isDifference`, `rowLabel`, `rows` and `MEMORY` as module-private constants, and
importing `report.mjs` runs it. Move them to `lib/rows.mjs` unchanged and import them back into
`report.mjs`, so the history reads rows exactly as the report does.

- Create `perf-compare/lib/rows.mjs` exporting `isDifference(metric)`, `MEMORY`,
  `rowLabel(flow, fixture, metric)`, `rows(samples)`, and two new functions:
  `better(metric)` returning `'lower'` for every metric today, and `unit(metric)` returning
  `'bytes'` for `/Bytes$/`, `'ms'` for `/Ms$/`, else `'count'`.
- Modify `perf-compare/report.mjs` to import them. Its output must not change.
- Test `perf-compare/test/rows.test.mjs`: `isDifference('settleMs')`, `isDifference('zoom2SettleMs')`
  and `isDifference('jankFrames')` are true, `isDifference('mainJsMs')` is false;
  `rowLabel('open', 'ebook', 'readyMs')` is `open(ebook) / readyMs` and `rowLabel('scroll', 'large',
  'wallMs')` is `scroll / wallMs`; `rows` skips a sample with `error`; `unit` and `better`.
- Verify: `node --test test/*.test.mjs` passes, and `node report.mjs
  history/archive/2026-11-18T05-05-31-149Z-macmini2020-desktop` rewrites that run's `report.md`
  byte for byte (`git diff --stat` shows nothing).

### Task 3: `lib/history.mjs`

Pure functions, no file or git access, tested in `perf-compare/test/history.test.mjs`.

- `intervalTargets(from, to, days)`: ISO dates `from`, `from + days`, ... not after `to`.
  `intervalTargets('2026-09-29', '2026-10-14', 7)` is `['2026-09-29', '2026-10-06', '2026-10-13']`.
- `selectIntervalPoints({ commits, existing, from, to, days, nearDays = 1 })`: `commits` is the
  first-parent line as `[{ sha, date }]` (ISO timestamps, any order), `existing` the dates of
  points the series already has. Returns `[{ sha, date, target }]`, one per target that has no
  existing point within `nearDays` and has a commit dated on or before the end of the target day
  (UTC). The commit is the latest such one. A commit already chosen for an earlier target, or
  whose own date is within `nearDays` of an existing point, is not returned again.
- `armFor(config, { sha, recipe, kernel })`: the id of the first arm in `config.arms` whose
  worktree has that `sha` and `recipe`, whose `kernel` matches and whose `rustReads` is false, or
  `null`.
- `eligible(meta, gate, { machine, reference, noise })`: an array of reasons the run can't join
  the history, empty when it can. Reasons: wrong `host` or `cpuModel`, `profile` not `desktop`,
  `quick`, `rounds < 10`, `harnessDirty`, `reference` or `noise` arm not in `armsRun`, either one
  not passing the gate.
- `seriesData({ runs, points, reference, noise })`: `runs` is `[{ meta, gate, samples }]` sorted
  by run id. Returns `{ points, rows }` as in "data.json" below. A point is measured by a run when
  its arm is in `armsRun`, passed the gate, and `meta.arms[arm].sha` equals the point's `sha`. The
  latest run wins. Intervals come from `bootstrapRatio` or `bootstrapDifference` by `isDifference`,
  the noise from `calibrate`, the verdict from `comparePair` (`lib/stats.mjs`). A row whose
  reference arm has no samples in that run gets `rel: null`. Size rows (`flow: 'size'`) come from
  `meta.sizes`, as plain ratios with no interval.

`data.json`:

```json
{
  "reference": "main",
  "series": { "plugins": { "label": "Rust port" } },
  "points": [{ "sha": "…", "series": "plugins", "date": "2026-09-29", "why": "milestone",
               "note": "…", "arm": "plugins", "runId": "…" }],
  "rows": [{ "key": "scroll|large|mainJsMs", "label": "scroll / mainJsMs", "flow": "scroll",
             "metric": "mainJsMs", "kind": "ratio", "unit": "ms", "better": "lower",
             "values": [{ "point": 0, "n": 10, "median": 812.4, "ref": 402.1, "rel": 2.02,
                          "lo": 1.98, "hi": 2.07, "noise": 0.021, "verdict": "slower" }] }]
}
```

`points` holds only measured points, in date order; `values[].point` indexes it.

Tests to write: the `intervalTargets` case above; a target with an existing point 1 day away adds
nothing; a target with no commit before it adds nothing; two targets that resolve to the same
commit return it once; `armFor` reuses an arm and ignores one with `rustReads: true`; `eligible`
returns one reason per failed condition and `[]` for a good run; `seriesData` takes the later of
two runs for one point, ignores a run whose arm `sha` differs from the point's, gives `rel: null`
without reference samples, and calls a clear 2x row `slower`.

### Task 4: `scripts/history-points.mjs` and `history/points.json`

`node scripts/history-points.mjs` reads `history/points.json`, and for each series with an
`intervalDays` walks `git rev-list --first-parent --format=%H %cI <ref>`, adds the interval points
(`why: "interval"`), fills `date` and `arm` on every point that lacks them, adds the missing
`hist-<sha9>` worktrees and arms to `arms.json`, and rewrites both files. Running it twice changes
nothing. It prints each point it added.

`points.json` starts with the series definitions, `"reference": "main"`, `"noise": "main-aa"`,
`"machine": { "host": "macmini2020", "cpuModel": "Apple M1" }` and the milestones:

| Series | Commit | Why |
| --- | --- | --- |
| plugins | `bef223d9d` | the first commit the harness measured |
| plugins | `c92019192` | the state cache, wire and bump fixes merged |
| plugins | `373b7f73b` | the glyph view merged |
| plugins | `7455a5294` | the bridge encoding merged |
| plugins | `faa2c6189` | the geometry decode merged |
| plugins | `9d6c2eca1` | plain arguments and result text merged; the tip before the catch-ups |
| plugins | `49b09030c` | the first upstream catch-up merged |
| plugins | `3aa47e708` | the 2026-11-12 catch-up merged |
| plugins | `0de4effd9` | the base of `research/perf-fixes` |
| plugins | `af9ff5d2b` | `research/perf-fixes` Phase C |
| plugins | `0d18fb177` | `research/perf-fixes` Phase D |
| plugins | `a6d4d5d14` | the `research/perf-fixes` tip |
| upstream | `2516e2786` | the reference itself |
| upstream | `074a190c9` | upstream at the 2026-11-07 catch-up |
| upstream | `ed064e0c8` | upstream at the 2026-11-12 catch-up (`main-now`) |
| upstream | `c4e744345` | upstream `main` on 2026-11-17 |

Verify: `node --test test/*.test.mjs` passes (`test/config.test.mjs` loads `arms.json`), and a
second run of the script leaves `git status` clean.

### Task 5: `scripts/history-record.mjs`

`node scripts/history-record.mjs <results/run-id | history/archive/run-id>` checks the run with
`eligible`, then copies `samples.jsonl`, `gate.json` and `meta.json` to `history/runs/<run-id>/`.
An ineligible run prints its reasons and exits 1. A run already recorded exits 0 and says so.

Record `history/archive/2026-11-18T05-05-31-149Z-macmini2020-desktop` as the first run: 10 rounds
on the Mac mini with the current probe (`af209e7644d8`), a clean harness at `096115a21`, and arms
at two points, `main-now` (`ed064e0c8`) and `perf-fixes-end` (`a6d4d5d14`).

### Task 6: `scripts/history-chart.mjs` and the page

`node scripts/history-chart.mjs` reads `history/runs/*` and `history/points.json`, writes
`history/data.json` (`seriesData`) and `history/index.html`, which is `lib/history-page.html` with
the JSON placed at its `/*DATA*/null` marker. The page is plain HTML, CSS and JavaScript with
inline SVG, and reads no network resource.

Verify by opening `history/index.html`: both recorded points draw, light and dark, and the
absolute toggle works.

### Task 7: README section

Add "The history" to `perf-compare/README.md`: what `history/` holds, the three commands, and the
rule that only `history-record.mjs` writes to `history/runs/`.

### Task 8: build every point

In the container, for the 16 points without a recorded run (`node scripts/history-chart.mjs`
prints how many are plotted; `history/data.json` `points` lists them), in batches of six:

```sh
cd /workspaces/wt-perf-history/perf-compare
node scripts/build-entry.mjs main main-aa main-now <arm> <arm> ...
```

`build-entry.mjs` builds each named arm's worktree and entry. A worktree is several GB and a fork
build far more, so check free space first, build one batch, and remove the worktrees of a batch
once its run is recorded (`git worktree remove`). For a point that fails to build, add
`"skip": "<the reason>"` to it in `points.json` and move on. `history-points.mjs` leaves a skipped
point alone, and the batches below leave it out.

This needs no Mac mini time and can start before Task 9 is unblocked.

### Task 9: measure (blocked)

Wait for the user to say the Mac mini is free. Then one batch at a time:

```sh
PERF_REMOTE=perf-mini node scripts/remote.mjs --rounds 10 --profile desktop \
  --arms main,main-aa,main-now,<arm>,<arm>,...
node scripts/history-record.mjs results/<run-id>
node scripts/history-chart.mjs
```

Nothing extra is needed for the Actions runner on the Mac mini: `remote.mjs` stops it before the
run and starts it after (`scripts/remote-guard.sh`). A batch that begins while a job is running
waits up to an hour for it. After each batch, check that the runner is back:
`ssh user@remote-host 'launchctl print gui/$(id -u)/<label>'`, where the label is the plist name in
`~/actions-runner/.service`.

`main-now` rides along so the gate has an `undo` reference. Six points and the three fixed arms
are about 3.2 h a batch. Commit `history/runs/<run-id>/`, `data.json` and `index.html` after each
batch, and log the batch here.

### Task 10: merge

After review, merge `research/perf-history` into `research/viewer-perf-compare`, then that into
`research/multi-platform-rust-core-impl-plugins`, with the user's confirmation for each.

## Going forward

Every standard run on the Mac mini that qualifies gets recorded: `history-record.mjs`, then
`history-chart.mjs`. To put a new commit on the line, add it to `points.json` as a milestone, or
run `history-points.mjs` once a new 7-day target has passed, then build and measure it as in
Tasks 8 and 9.
