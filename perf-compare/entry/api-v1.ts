// The viewer API before the upstream catch-up: main 2516e2786 and the plugins
// branch up to 9d6c2eca1. entry/api-v2.ts has the same operations for the API
// after it, and lib/api-generation.mjs picks one per arm at build time.
import { StageToken, AnnotationToken, type EmbedPdfViewerElement } from '@perf/door';

type Viewer = NonNullable<EmbedPdfViewerElement['viewer']>;
type PageRef = ReturnType<Viewer['documents']['listPages']>[number]['ref'];

export const apiGeneration = 'v1';

export const onOpened = (v: Viewer, listener: () => void): (() => void) => v.on('opened', listener);

export const execute = (v: Viewer, command: string): void => v.execute(command);

export const goToPageIndex = (v: Viewer, i: number): void =>
  v.get(StageToken).goToPageIndex(i, { behavior: 'instant' });

export const pageFrame = (v: Viewer, page: PageRef) => v.get(StageToken).getPageFrame(page);

export const annotationCountOn = (v: Viewer, page: PageRef): number =>
  v.get(AnnotationToken).list({ page }).length;
