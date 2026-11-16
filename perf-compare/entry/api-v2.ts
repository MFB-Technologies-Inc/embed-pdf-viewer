// The viewer API after the upstream catch-up: main ed064e0c8 and the plugins
// branch from 2aaa8f841. Each operation is the same call as in entry/api-v1.ts
// under its new name: `viewer.on('opened')` is `documents.onOpened`,
// `viewer.execute` is `commands.execute` (the v1 handle discarded the same
// promise) with the tool commands' new ids, `goToPageIndex(i)` is
// `goToPage(i)` with an index, `getPageFrame` is `getPageBox`, and the
// filter's `page` is `pages: [page]`.
import { StageToken, AnnotationToken, type EmbedPdfViewerElement } from '@perf/door';

type Viewer = NonNullable<EmbedPdfViewerElement['viewer']>;
type PageRef = ReturnType<Viewer['documents']['listPages']>[number]['ref'];

export const apiGeneration = 'v2';

export const onOpened = (v: Viewer, listener: () => void): (() => void) =>
  v.documents.onOpened(() => listener());

// The flows name commands by their v1 ids. Both v2 ids activate the same
// interaction tool as the v1 command did (`activateTool('pointer')` and the
// `highlight` tool).
const COMMANDS: Readonly<Record<string, string>> = {
  'pointer:toggle': 'tool:pointer',
  'annotation:add-highlight': 'tool:highlight',
};

export const execute = (v: Viewer, command: string): void =>
  void v.commands.execute(COMMANDS[command] ?? command);

export const goToPageIndex = (v: Viewer, i: number): void =>
  v.get(StageToken).goToPage(i, { behavior: 'instant' });

export const pageFrame = (v: Viewer, page: PageRef) => v.get(StageToken).getPageBox(page);

export const annotationCountOn = (v: Viewer, page: PageRef): number =>
  v.get(AnnotationToken).list({ pages: [page] }).length;
