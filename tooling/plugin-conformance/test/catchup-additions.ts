/**
 * Cases the 2026-11-18 catch-up added to the vectors after the evidence archives were cut:
 * `historical/catchup-2026-11-18/additions.json`, written by
 * `docs/research/upstream-catchup-2026-11-18/scripts/cu-c7/additions.mjs` from the vectors at the
 * catch-up's start and end. The evidence tests count them as later additions, as their lists of
 * the 2026-11-12 catch-up's cases do.
 */
import { readFileSync } from 'node:fs';

const archive = JSON.parse(
  readFileSync(new URL('../historical/catchup-2026-11-18/additions.json', import.meta.url), 'utf8'),
) as { additions: { file: string; name: string }[] };

/**
 * The 2026-11-27 catch-up's changes to those cases: the ones upstream's `9af4d9ee7` removed or
 * renamed (`removed`), and the cases it added in their place (`additions`), in
 * `historical/catchup-2026-11-27/additions.json`.
 */
const later = JSON.parse(
  readFileSync(new URL('../historical/catchup-2026-11-27/additions.json', import.meta.url), 'utf8'),
) as { removed: { file: string; name: string }[]; additions: { file: string; name: string }[] };

/**
 * The names the 2026-11-18 catch-up added to `file`, in vector order, less the ones the
 * 2026-11-27 catch-up removed, then the ones it added.
 */
export function caughtUp20261118(file: string): string[] {
  const gone = new Set(later.removed.filter((r) => r.file === file).map((r) => r.name));
  return [
    ...archive.additions.filter((a) => a.file === file && !gone.has(a.name)).map((a) => a.name),
    ...later.additions.filter((a) => a.file === file).map((a) => a.name),
  ];
}

/** The same, over the distinct files of `suites`, as `[file, name]` pairs. */
export function caughtUp20261118In(suites: readonly { file: string }[]): [string, string][] {
  return [...new Set(suites.map((s) => s.file))].flatMap((file) =>
    caughtUp20261118(file).map((name): [string, string] => [file, name]),
  );
}
