import EmbedPDF, { StageToken, SearchToken, SelectionToken } from '@perf/door';
import { setupKernel, kernelCounts } from '@perf/kernel';
// entry/api-v1.ts or api-v2.ts, whichever viewer API this arm's packages have
// (lib/api-generation.mjs): the calls that were renamed between the two.
import {
  capabilities,
  onOpened,
  execute,
  goToPageIndex,
  pageFrame,
  annotationCountOn,
  annotationTotal,
  annotationStatus,
  annotationsOn,
  fieldCount,
  formStatus,
  fieldText,
} from '@perf/api';
import type { PerfArm } from './globals';

declare const __PERF_ARM__: PerfArm;
const arm = __PERF_ARM__;
const doc = new URLSearchParams(location.search).get('doc') ?? 'ebook';

await setupKernel();

const el = EmbedPDF.init({
  target: '#viewer',
  src: `/${doc}.pdf`,
  // `registerLocalEngine` spreads this bag into `localEngine()`, which is
  // where `rustRouting` is read on the impl branch. Other branches ignore it.
  ...(arm.rustReads ? { engine: { rustRouting: { rustReads: true } } as never } : {}),
});

const ready = new Promise<void>((resolve) =>
  el.addEventListener(
    'epdf:ready',
    () => {
      performance.mark('perf:ready');
      resolve();
    },
    { once: true },
  ),
);

const v = () => el.viewer!;

const opened = ready.then(
  () =>
    new Promise<void>((resolve) => {
      const done = () => {
        performance.mark('perf:opened');
        resolve();
      };
      const off = onOpened(v(), () => {
        off();
        done();
      });
      // The document can finish opening before this listener attaches.
      if (v().documents.list().some((d) => d.status === 'ready')) {
        off();
        done();
      }
    }),
);

// The active document's page registry, not the stage's current spread.
const pages = () => v().documents.listPages();
const pageRef = (i: number) => pages()[i].ref;

function viewportOrigin(width: number, height: number): { x: number; y: number } {
  const all = el.shadowRoot?.querySelectorAll<HTMLElement>('*') ?? [];
  for (const node of all) {
    if (node.clientWidth === width && node.clientHeight === height) {
      const r = node.getBoundingClientRect();
      return { x: r.x + node.clientLeft, y: r.y + node.clientTop };
    }
  }
  throw new Error(`no element is the ${width}x${height} stage viewport`);
}

window.__perf = {
  arm,
  ready,
  opened,
  pageCount: () => pages().length,
  goToPageIndex: (i) => goToPageIndex(v(), i),
  currentPageIndex: () => v().get(StageToken).getCurrentPageIndex(),
  zoomTo: (z) => v().get(StageToken).zoomTo(z),
  zoomLevel: () => v().get(StageToken).getZoomLevel(),
  search: async (text) => {
    await v().get(SearchToken).search({ text });
    return v().get(SearchToken).getHitCount();
  },
  clearSearch: () => v().get(SearchToken).clear(),
  readSelection: () => v().get(SelectionToken).readText(),
  clearSelection: () => v().get(SelectionToken).clear(),
  capabilities,
  annotationCount: (pageIndex) => annotationCountOn(v(), pageRef(pageIndex)),
  annotationTotal: () => annotationTotal(v()),
  annotationStatus: () => annotationStatus(v()),
  annotations: (pageIndex) => annotationsOn(v(), pageRef(pageIndex)),
  fieldCount: () => fieldCount(v()),
  formStatus: () => formStatus(v()),
  fieldText: (name) => fieldText(v(), name),
  execute: (command) => execute(v(), command),
  pageBox: (i) => {
    // The frame is in stage-viewport px; the viewport element is found by its
    // size, so chrome above or beside it moves the box with it.
    const stage = v().get(StageToken);
    const frame = pageFrame(v(), pageRef(i));
    if (!frame) throw new Error(`page ${i} has no frame yet`);
    const { width, height } = stage.getViewportSize();
    const origin = viewportOrigin(width, height);
    return {
      x: origin.x + frame.screenX,
      y: origin.y + frame.screenY,
      width: frame.transform.viewWidth,
      height: frame.transform.viewHeight,
    };
  },
  kernelCounts,
};
