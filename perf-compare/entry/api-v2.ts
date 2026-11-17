// The viewer API after the upstream catch-up: main ed064e0c8 and the plugins
// branch from 2aaa8f841. Each operation is the same call as in entry/api-v1.ts
// under its new name: `viewer.on('opened')` is `documents.onOpened`,
// `viewer.execute` is `commands.execute` (the v1 handle discarded the same
// promise) with the tool commands' new ids, `goToPageIndex(i)` is
// `goToPage(i)` with an index, `getPageFrame` is `getPageBox`, the filter's
// `page` is `pages: [page]`, `listFields` is `list`, a rect-kind
// annotation's `geometry.bounds` is its `rect`, and a text value is `{ value }`.
import { StageToken, AnnotationToken, FormToken, type EmbedPdfViewerElement } from '@perf/door';
import { boxOf, type Box } from './box';

type Viewer = NonNullable<EmbedPdfViewerElement['viewer']>;
type PageRef = ReturnType<Viewer['documents']['listPages']>[number]['ref'];

export const apiGeneration = 'v2';

// v2 has the history plugin; `history:undo` runs `HistoryToken.undo()`. It
// also reads the annotation under the pointer (`getHovered`), which `hover`
// waits on.
export const capabilities: readonly string[] = ['history', 'hovered'];

export const onOpened = (v: Viewer, listener: () => void): (() => void) =>
  v.documents.onOpened(() => listener());

// The flows name commands by their v1 ids. Each v2 id activates the same
// interaction tool as the v1 command did (`activateTool('pointer')`, the
// `highlight` and the `ink` tool).
const COMMANDS: Readonly<Record<string, string>> = {
  'pointer:toggle': 'tool:pointer',
  'annotation:add-highlight': 'tool:highlight',
  'annotation:add-ink': 'tool:ink',
};

export const execute = (v: Viewer, command: string): void =>
  void v.commands.execute(COMMANDS[command] ?? command);

export const goToPageIndex = (v: Viewer, i: number): void =>
  v.get(StageToken).goToPage(i, { behavior: 'instant' });

export const pageFrame = (v: Viewer, page: PageRef) => v.get(StageToken).getPageBox(page);

export const annotationCountOn = (v: Viewer, page: PageRef): number =>
  v.get(AnnotationToken).list({ pages: [page] }).length;

export const annotationTotal = (v: Viewer): number => v.get(AnnotationToken).list().length;

export const annotationStatus = (v: Viewer): string => v.get(AnnotationToken).getStatus();

export const annotationsOn = (v: Viewer, page: PageRef): { subtype: string; box: Box }[] =>
  v
    .get(AnnotationToken)
    .list({ pages: [page] })
    .map((a) => ({ subtype: String(a.subtype).toLowerCase(), box: boxOf(a.rect) }));

export const fieldCount = (v: Viewer): number => v.get(FormToken).list().length;

export const formStatus = (v: Viewer): string => v.get(FormToken).getStatus();

export const fieldText = (v: Viewer, name: string): string | null => {
  const form = v.get(FormToken);
  const field = form.list().find((f) => f.name === name);
  const value = field ? form.getValue(field.ref) : null;
  return value && 'value' in value ? value.value : null;
};

// Done conditions read one field or annotation by its ref, found once before
// the timed window (entry/main.ts `watch`), not a scan of the list.
const form = (v: Viewer) => v.get(FormToken);
type FieldRef = ReturnType<ReturnType<typeof form>['list']>[number]['ref'];

export const fieldRef = (v: Viewer, name: string): FieldRef | null => form(v).list({ name })[0]?.ref ?? null;

export const fieldTextOf = (v: Viewer, ref: FieldRef): string | null => {
  const value = form(v).getValue(ref);
  return value && 'value' in value ? value.value : null;
};

export const annotationRef = (v: Viewer, page: PageRef, subtype: string) =>
  v
    .get(AnnotationToken)
    .list({ pages: [page] })
    .find((a) => String(a.subtype).toLowerCase() === subtype)?.ref ?? null;

export const annotationBoxOf = (
  v: Viewer,
  ref: NonNullable<ReturnType<typeof annotationRef>>,
): Box | null => {
  const a = v.get(AnnotationToken).get(ref);
  return a ? boxOf(a.rect) : null;
};

export const hoveredBox: ((v: Viewer) => Box | null) | null = (v) => {
  const a = v.get(AnnotationToken).getHovered();
  return a ? boxOf(a.rect) : null;
};
