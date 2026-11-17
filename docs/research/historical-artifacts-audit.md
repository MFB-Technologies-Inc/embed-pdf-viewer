# `historical/` artifacts audit

`tooling/plugin-conformance/historical/` holds 520 files, 68,346,433 bytes (68.35 MB). The 22
tests under `tooling/plugin-conformance/test/` that name `historical/` read 229 of them, 58,281,474
bytes. Another 15 files (7,237,733 bytes) are named only by the private `test/*-evidence.ts`
capture scripts, and 3 only by README links. The remaining 273 files, 2,805,402 bytes, are read by
nothing. That includes all 193 `.out` files. Of the 99 `.txt` files, 93 are read by tests, 2 by
evidence scripts, and 4 by nothing.

Compressing the 197 unreferenced `.out` and `.txt` files would take them from 1,226,177 to 377,001
bytes, a saving of 849,176 bytes (0.85 MB, about 1.2% of the directory). The large files are the
`.json` inputs and runtimes the tests read, so this audit finds no big win in the unread files.

## Method

Each of the 22 tests was run once in the dev container with a preload module that wrapped the
`node:fs` and `node:fs/promises` read, stat, readdir and write calls and logged every path under
`historical/`. That resolves the built paths (`'../historical/' + task + '/current-inputs-runtime.json'`),
names taken from manifests (`integrity.json`, `accounting.json` rows, `name + '.txt'`) and `../`
hops between task directories. All 22 runs passed (647 tests, 0 failures), so no read was skipped by
an early failure. No test lists a directory and none writes under `historical/`.

The 11 `test/*-evidence.ts` scripts are not run by any package script or test, and they write into
`historical/`, so they were not run. Their references come from a static match of string literals
against their `historical/` root. `src/`, `crates/`, `scripts/`, `docs/`, both `package.json`
files and `tooling/plugin-conformance/README.md` were grepped for `historical/`. Nothing in `src/`
or `crates/` reads the directory; the hits there are comments and the `historicalTarget` helper.
The docs point at five directories in prose and link three files.

Compressed sizes use Node's `zlib.zstdCompressSync` at level 3, the level the vectors use. The
container has no `zstd` CLI.

## Referenced files, by test

| Test | Files | Bytes | Directories |
|---|---:|---:|---|
| `acrojs-current.test.ts` | 2 | 102,232 | `task-12b2c1` |
| `actions-current.test.ts` | 4 | 4,488,851 | `task-12b2c2`, `task-12b2c6` |
| `actions-lifecycle-current.test.ts` | 5 | 3,727,620 | `task-12b2c2`, `task-12b2c3` |
| `actions-script-current.test.ts` | 13 | 8,896,498 | `task-12b2c2` to `task-12b2c4` |
| `annotation-current-pure.test.ts` | 1 | 262,743 | `task-12b2b1a` |
| `annotation-live.test.ts` | 5 | 140,831 | `task-12b2b1b-i` |
| `annotation-live-ii.test.ts` | 8 | 203,820 | `task-12b2b1b-i`, `task-12b2b1b-ii` |
| `commands-current.test.ts` | 13 | 3,201,171 | `task-12b2c5` |
| `form-current.test.ts` | 9 | 1,819,833 | `task-12b2b2-i` |
| `form-live.test.ts` | 8 | 2,586,470 | `task-12b2b2-i`, `task-12b2b2-ii` |
| `form-script.test.ts` | 16 | 3,198,551 | `task-12b2b2-i` to `task-12b2b2-iii` |
| `i18n-current.test.ts` | 38 | 20,615,749 | `task-12b2c2` to `task-12b2c9` |
| `link-current.test.ts` | 29 | 9,851,726 | `task-12b2c2` to `task-12b2c6` |
| `measurement.test.ts` | 18 | 6,013,733 | `task-12b2b3`, `task-12b2b3-i`, `catchup-2026-11-12-measurement` |
| `metadata-current.test.ts` | 31 | 7,836,457 | `task-12b2c2` to `task-12b2c7` |
| `page-edit-current.test.ts` | 30 | 11,064,609 | `task-12b2c2` to `task-12b2c8` |
| `redaction.test.ts` | 3 | 4,590,276 | `task-12b2b3`, `task-12b2b3-ii` |
| `shell-current.test.ts` | 24 | 14,882,774 | `task-12b2c2` to `task-12b2c10` |
| `stamp-current.test.ts` | 4 | 4,113,174 | `task-12b2b3`, `task-12b2b3-iii` |
| `stamp-mutations.test.ts` | 3 | 7,393,388 | `task-12b2b3`, `task-12b2b3-iv` |
| `stamp-placement.test.ts` | 9 | 5,453,475 | `task-12b2b3`, `task-12b2b3-i` to `-v`, `catchup-2026-11-12-measurement` |
| `view-manager-current.test.ts` | 25 | 15,996,197 | `task-12b2c2` to `task-12b2c11` |

Rows overlap: the `*-current` tests each re-read the earlier tasks' `current-inputs-runtime.json`.
The union is 229 files.

Named only by evidence scripts (15 files): `task-12b2c1/` `original-inputs.json`,
`current-inputs-runtime.json`, `accounting.json`, `source-evidence.json`, `deferred-inputs.json`;
`task-12b2c2/` `original-inputs.json`, `accounting.json`, `original-runtime.json`,
`negative-channels.json`, `source-evidence.json`, `original-actions.ts.txt`; `task-12b2c3/`
`original-inputs.json`, `negative-channels.json`, `source-evidence.json`, `original-actions.ts.txt`.

Linked only from `tooling/plugin-conformance/README.md` (3 files): `task-12b2b2-i/README.md`,
`task-12b2b2-ii/README.md`, `task-12b2b2-iii/README.md`.

## Unreferenced files, by directory

| Directory | Files | Bytes | Contents | `.out`+`.txt` raw | zstd -3 |
|---|---:|---:|---|---:|---:|
| `task-12b2a-retired.json` | 1 | 11,626 | 1 json | 0 | 0 |
| `task-12b2b1a` | 3 | 58,538 | 1 md, 2 txt | 53,377 | 15,286 |
| `task-12b2b1b-i` | 2 | 26,815 | 1 md, 1 txt | 23,312 | 7,288 |
| `task-12b2b1b-ii` | 1 | 2,962 | 1 md | 0 | 0 |
| `task-12b2b3` | 1 | 1,615 | 1 md | 0 | 0 |
| `task-12b2b3-i` | 2 | 662,935 | 1 md, 1 json | 0 | 0 |
| `task-12b2b3-ii` | 1 | 5,889 | 1 md | 0 | 0 |
| `task-12b2b3-iii` | 1 | 6,772 | 1 md | 0 | 0 |
| `task-12b2b3-iv` | 1 | 5,866 | 1 md | 0 | 0 |
| `task-12b2b3-v` | 1 | 8,933 | 1 md | 0 | 0 |
| `task-12b2c1` | 14 | 83,628 | 10 out, 2 json, 1 diff, 1 md | 60,128 | 19,915 |
| `task-12b2c2` | 22 | 678,705 | 13 out, 3 json, 5 gz, 1 diff | 124,503 | 42,205 |
| `task-12b2c3` | 28 | 211,128 | 19 out, 3 json, 4 gz, 1 diff, 1 md | 182,350 | 35,316 |
| `task-12b2c4` | 36 | 149,285 | 26 out, 5 json, 3 gz, 1 diff, 1 py | 96,219 | 31,319 |
| `task-12b2c5` | 11 | 110,673 | 9 out, 1 gz, 1 py | 94,682 | 26,878 |
| `task-12b2c6` | 51 | 168,076 | 41 out, 8 gz, 2 py | 131,191 | 46,712 |
| `task-12b2c7` | 20 | 90,841 | 18 out, 1 gz, 1 py | 76,851 | 25,750 |
| `task-12b2c8` | 37 | 201,200 | 25 out, 3 json, 5 gz, 1 txt, 1 diff, 2 py | 145,671 | 49,259 |
| `task-12b2c9` | 13 | 107,841 | 11 out, 1 diff, 1 py | 78,712 | 25,863 |
| `task-12b2c10` | 15 | 106,572 | 12 out, 2 gz, 1 py | 82,159 | 25,894 |
| `task-12b2c11` | 12 | 105,502 | 9 out, 2 gz, 1 py | 77,022 | 25,316 |
| Total | 273 | 2,805,402 | | 1,226,177 | 377,001 |

`catchup-2026-11-12-measurement`, `task-12b2b2-i`, `task-12b2b2-ii` and `task-12b2b2-iii` have
no unreferenced files. The two largest unread files are JSON, not logs:
`task-12b2b3-i/current-inputs.json` (655,336 bytes) and `task-12b2c2/original-channels.json`
(519,241 bytes). The four unread `.txt` files are source snapshots:
`task-12b2b1a/annotation-pure.ts.txt`, `task-12b2b1a/annotation-repository.ts.txt`,
`task-12b2b1b-i/annotation-fixtures.ts.txt` and `task-12b2c8/fix1/original-page-edit-current.test.ts.txt`.

Unread is not the same as unrecorded. Manifests that tests do read (for example
`task-12b2c6/preservation.json` and `task-12b2c8/preservation.json`) name `.out` files, and
`task-12b2c8/fix1/integrity.json` records `.out` sha256 values. No test opens those `.out` files
today, so compressing them breaks no test, but the recorded hashes would stop matching the bytes on
disk.

Deleting or compressing anything under `historical/` is the user's decision; this audit changes
nothing there.
