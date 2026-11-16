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

/** Each case's label: its `name`, or `name#n` for the nth case (n from 2)
 *  that repeats a name, so cases sharing a name match by occurrence. */
function labels(cases: readonly { readonly name: string }[]): string[] {
  const seen = new Map<string, number>();
  return cases.map(({ name }) => {
    const n = (seen.get(name) ?? 0) + 1;
    seen.set(name, n);
    return n === 1 ? name : `${name}#${n}`;
  });
}

/** Cases matched by `name` and, among cases that share a name, by
 *  occurrence; a case is changed when the `canonicalKey` of the whole case
 *  differs, so moved object members alone are no change. */
export function caseDiff(before: RenderedSuite | null, after: RenderedSuite): CaseDiff {
  const beforeCases = before?.cases ?? [];
  const beforeLabels = labels(beforeCases);
  const old = new Map(beforeCases.map((entry, i) => [beforeLabels[i], canonicalKey(entry)]));
  const afterLabels = labels(after.cases);
  const present = new Set(afterLabels);
  const added: string[] = [];
  const changed: string[] = [];
  after.cases.forEach((entry, i) => {
    const label = afterLabels[i];
    const key = old.get(label);
    if (key === undefined) added.push(label);
    else if (key !== canonicalKey(entry)) changed.push(label);
  });
  const removed = beforeLabels.filter((label) => !present.has(label));
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
