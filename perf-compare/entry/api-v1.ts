// The viewer API before the upstream catch-up: main 2516e2786 and the plugins
// branch up to 9d6c2eca1. entry/api-v2.ts has the same operations for the API
// after it, and lib/api-generation.mjs picks one per arm at build time.
import { StageToken, AnnotationToken, FormToken, type EmbedPdfViewerElement } from '@perf/door';
import { boxOf, type Box } from './box';

type Viewer = NonNullable<EmbedPdfViewerElement['viewer']>;
type PageRef = ReturnType<Viewer['documents']['listPages']>[number]['ref'];

export const apiGeneration = 'v1';

// What a flow can need beyond the operations below (lib/flows.mjs `needs`).
// v1 has no history plugin: its chrome's `history:undo` is a disabled no-op.
// Nor a hover read: the annotation model's `hovered` isn't on the public
// capability, so `hover` can't see that the arm handled the pointer.
export const capabilities: readonly string[] = [];

export const onOpened = (v: Viewer, listener: () => void): (() => void) => v.on('opened', listener);

export const execute = (v: Viewer, command: string): void => v.execute(command);

export const goToPageIndex = (v: Viewer, i: number): void =>
  v.get(StageToken).goToPageIndex(i, { behavior: 'instant' });

export const pageFrame = (v: Viewer, page: PageRef) => v.get(StageToken).getPageFrame(page);

export const annotationCountOn = (v: Viewer, page: PageRef): number =>
  v.get(AnnotationToken).list({ page }).length;

export const annotationTotal = (v: Viewer): number => v.get(AnnotationToken).list().length;

export const annotationStatus = (v: Viewer): string => v.get(AnnotationToken).getStatus();

// The page's annotations, with the box a move changes: the rect geometry for
// the kinds a move takes by their bounds, the visual bounds for the rest.
export const annotationsOn = (v: Viewer, page: PageRef): { subtype: string; box: Box }[] =>
  v
    .get(AnnotationToken)
    .list({ page })
    .map((a) => ({
      subtype: String(a.subtype).toLowerCase(),
      box: boxOf(a.geometry.kind === 'rect' ? a.geometry.bounds : a.bounds),
    }));

export const fieldCount = (v: Viewer): number => v.get(FormToken).listFields().length;

export const formStatus = (v: Viewer): string => v.get(FormToken).getStatus();

// By the field's own ref: v1's getValue answers null for an `fqn` ref.
export const fieldText = (v: Viewer, name: string): string | null => {
  const form = v.get(FormToken);
  const field = form.listFields().find((f) => f.name === name);
  const value = field ? form.getValue(field.ref) : null;
  return value && value.type === 'text' ? value.value : null;
};

// Done conditions read one field or annotation by its ref, found once before
// the timed window (entry/main.ts `watch`), not a scan of the list.
type Form = ReturnType<typeof form>;
type FieldRef = ReturnType<Form['listFields']>[number]['ref'];
const form = (v: Viewer) => v.get(FormToken);

export const fieldRef = (v: Viewer, name: string): FieldRef | null =>
  form(v).listFields({ name })[0]?.ref ?? null;

export const fieldTextOf = (v: Viewer, ref: FieldRef): string | null => {
  const value = form(v).getValue(ref);
  return value && value.type === 'text' ? value.value : null;
};

export const annotationRef = (v: Viewer, page: PageRef, subtype: string) =>
  v
    .get(AnnotationToken)
    .list({ page })
    .find((a) => String(a.subtype).toLowerCase() === subtype)?.ref ?? null;

export const annotationBoxOf = (
  v: Viewer,
  ref: NonNullable<ReturnType<typeof annotationRef>>,
): Box | null => {
  const a = v.get(AnnotationToken).get(ref);
  return a ? boxOf(a.geometry.kind === 'rect' ? a.geometry.bounds : a.bounds) : null;
};

export const hoveredBox: ((v: Viewer) => Box | null) | null = null;
