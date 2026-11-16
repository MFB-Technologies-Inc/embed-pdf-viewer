/**
 * What a vector write changed, case by case: the line `pnpm run vectors`
 * prints per file, so a regeneration can be reviewed without diffing the
 * compressed files.
 */
import { canonicalKey } from './canonical';

/** A vector file as written: the envelope's `cases`, each with its `name`. */
export interface RenderedSuite {
  readonly cases: readonly { readonly name: string }[];
}

export interface CaseDiff {
  readonly added: string[];
  readonly removed: string[];
  readonly changed: string[];
}

/** Cases matched by `name`; a case is changed when the `canonicalKey` of the
 *  whole case differs, so moved object members alone are no change. */
export function caseDiff(before: RenderedSuite | null, after: RenderedSuite): CaseDiff {
  const old = new Map((before?.cases ?? []).map((entry) => [entry.name, canonicalKey(entry)]));
  const names = new Set(after.cases.map((entry) => entry.name));
  const added: string[] = [];
  const changed: string[] = [];
  for (const entry of after.cases) {
    const key = old.get(entry.name);
    if (key === undefined) added.push(entry.name);
    else if (key !== canonicalKey(entry)) changed.push(entry.name);
  }
  const removed = [...old.keys()].filter((name) => !names.has(name));
  return { added, removed, changed };
}

/** How many changed names a summary line lists before `and N more`. */
const NAMED = 10;

/** `core/x.json: +2 -0 ~5 (changed: a, b, c, d, e)`, or null when nothing
 *  changed. */
export function describeCaseDiff(
  file: string,
  { added, removed, changed }: CaseDiff,
): string | null {
  if (!added.length && !removed.length && !changed.length) return null;
  const line = `${file}: +${added.length} -${removed.length} ~${changed.length}`;
  if (!changed.length) return line;
  const named = changed.slice(0, NAMED).join(', ');
  const more = changed.length > NAMED ? `, and ${changed.length - NAMED} more` : '';
  return `${line} (changed: ${named}${more})`;
}
