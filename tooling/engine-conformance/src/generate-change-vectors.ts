/**
 * Emits `vectors/changes/*.json`: the change ops and reads upstream added
 * after 074a190c9, as the worker answers them.
 *
 *   pnpm --filter @embedpdf/tooling-engine-conformance run changes
 *   pnpm --filter @embedpdf/tooling-engine-conformance run changes:check
 *
 * `crates/epdf-engine/tests/change_ops.rs` replays it.
 *
 * | File | Upstream |
 * |---|---|
 * | `viewports.json` | `pages.list` carries each page's `/VP` viewports (726ec2dd6, c7f8d75ff) |
 * | `scale.json` | `pages.setScale` as a change op, and its undo (c7f8d75ff, 65b0f137e) |
 * | `rename.json` | an annotation update's `patch.nm` renames it (756f8e56b) |
 * | `annotation-copy.json` | `annotations.copy` (1481e4556, 9eead5531, 5496eb084) |
 * | `form-copy.json` | `forms.copy` (a390171cc, 2612ae1e1) |
 * | `widget-rename.json` | a widget update's `patch.nm` renames it, held to a free name (756f8e56b) |
 * | `kinds.json` | the version each change kind arrived in (a4805a492) |
 * | `field-purpose-format.json` | a field's purpose and format: reads, writes, undo, copies, rights (833b40015, #1147) |
 * | `field-groups.json` | a new field in no group, `groupId: null`, undo and copies (fce760aeb, #1140) |
 * | `signature-mark.json` | a signature field's mark as a PNG, a JPEG or a PDF, and no mark (fcb69611f, #1145) |
 * | `widget-rotation.json` | a mark in a turned signature field is fitted into its frame (c76603e3f, #1118) |
 *
 * # What is recorded
 *
 * A case is a list of steps against one freshly opened fixture. Each step is
 * one call of the public API (`doc.apply`, `doc.pages.list`, a page's
 * `annotations.list`, `doc.forms.list`), and what it records is what crossed
 * the worker boundary: every request the call sent, and the worker's answer
 * to each, `resolve` with its result or `reject` with its error's code (the
 * two engines word their refusals differently). The worker is what the Rust
 * `Host` replaces, so that is the level the two are compared at.
 *
 * `jobId` is the session's own counter and is recorded as `"<job>"`. Every
 * write names its `opId`, so an undo can name it and the answers are the
 * same on every run. A name the engine makes up for a copy (a UUIDv7) is
 * recorded as `"<uuid>"`, and a timestamp the clock wrote as `"<now>"`;
 * the Rust runner masks its own answers the same way. So that the mask
 * doesn't hide whether copies got names of their own, each exchange also
 * records how many distinct names it masked (`uuids`).
 *
 * A step may also be one verb of the forms service (`doc.forms.create`,
 * `update`, `updateWidget`, `setSignatureAppearance`, `copy`), each with the
 * op id it names so an undo can name it. A group whose fixture a
 * conformance suite builds in memory carries it as hex (`fixtureHex`), and
 * a case may open it with a scope and an identity of its own (`open`).
 * Bytes inside a request are recorded as `{ "$hex": ... }`; the runner
 * sends each as a payload the request names `{ "payload": n }`.
 *
 * Each `doc.apply` step also records the events the change publishes
 * (`changeEvents` over its answer): the main thread derives them, so they
 * aren't in the worker's answer, but the Rust `Host` answers them itself.
 */
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

import {
  LazyTransport,
  LocalEngine,
  toPageRef,
  type Transport,
  type WorkerRequest,
} from '../../../packages/engine/main/src/index';
import { createInlineTransport } from '../../../packages/engine/main/src/transport/InlineTransport';
import type { Change } from '../../../packages/engine/core/src/mutation/Change';
import {
  API_VERSION,
  FIELD_PURPOSE_AND_FORMAT_SINCE,
  lacksWhatArrivedIn,
  MARK_IMAGE_OR_NONE_SINCE,
  VALIDATION_INFO_SINCE,
} from '../../../packages/engine/core/src/wire/apiVersion';
import {
  CHANGE_KIND_SINCE,
  changeKindSince,
  changeKindsUnknownTo,
  type ChangeKind,
} from '../../../packages/engine/core/src/wire/changeKinds';
import { CHANGE_KINDS } from '../../../packages/engine/core/src/wire/changeSchemas';
import { changeEvents } from '../../../packages/engine/core/src/events/changeEvents';
import { pdfOf } from '../../../packages/engine/core/src/conformance/pdfOf';
import { FIELD_GROUPS_PDF } from '../../../packages/engine/core/src/conformance/runFieldGroupsConformance';
import { FIELD_PURPOSE_FORMAT_PDF } from '../../../packages/engine/core/src/conformance/runFieldPurposeFormatConformance';
import { TURNED_FIELDS_PDF } from '../../../packages/engine/core/src/conformance/runWidgetRotationConformance';
import {
  BANDS_JPEG,
  BANDS_PDF,
  BANDS_PNG,
  TWO_PAGE_PDF,
} from '../../../packages/engine/core/src/conformance/stampFixtures';
import type { ChangeResult } from '../../../packages/engine/core/src/mutation/Change';

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = resolve(packageRoot, '../..');
const vectorsRoot = join(packageRoot, 'vectors', 'changes');

/** Bump when the envelope changes shape. The Rust runner asserts it. */
const SCHEMA_VERSION = 1;

const VIEWPORTS = 'tooling/engine-conformance/fixtures/viewports.pdf';
const ANNOTATIONS = 'tooling/engine-conformance/fixtures/annotation_base.pdf';
const FORMS = 'tooling/engine-conformance/fixtures/form_fields.pdf';

/** A mark a step draws, by name: its bytes are in the group's `fixtures`. */
type Mark =
  | 'png'
  | 'jpeg'
  | 'pdf'
  | 'pdfKeyed'
  | 'pngKeyed'
  | 'twoPage'
  | 'twoPageKeyed'
  | 'markPdfKeyed'
  | 'notAPicture'
  | null;

type Step =
  | { readonly apply: Change; readonly opId: string }
  | { readonly read: 'pages.list' }
  | { readonly read: 'annotations.list'; readonly page: number }
  | { readonly read: 'forms.list' }
  | { readonly call: 'forms.create'; readonly draft: unknown; readonly opId: string }
  | { readonly call: 'forms.update'; readonly field: unknown; readonly patch: unknown; readonly opId: string }
  | {
      /** The first widget of field `widgetOf`, found by a `forms.list` the step records too. */
      readonly call: 'forms.updateWidget';
      readonly widgetOf: string;
      /** `'$rect'` for a member stands for the widget's `rect` as the list reads it. */
      readonly patch: Record<string, unknown>;
      readonly opId: string;
    }
  | {
      readonly call: 'forms.setSignatureAppearance';
      readonly field: unknown;
      readonly mark: Mark;
      readonly opId: string;
    }
  | { readonly call: 'forms.copy'; readonly fields: readonly unknown[]; readonly opId: string };

/** The scope and identity a case opens its fixture with; every scope when left out. */
interface Open {
  readonly scope?: readonly string[];
  readonly identity?: Record<string, string>;
}

interface Case {
  readonly name: string;
  readonly covers: string;
  readonly open?: Open;
  readonly steps: readonly Step[];
}

interface Group {
  readonly file: string;
  readonly kind: string;
  /** A fixture's repo path, or a fixture a suite builds in memory, by name. */
  readonly fixture: string | { readonly name: string; readonly bytes: Uint8Array };
  /** Bytes the Rust runner's own checks read, recorded as hex. */
  readonly fixtures?: Record<string, Uint8Array>;
  readonly cases: readonly Case[];
}

const page = (objectNumber: number) => toPageRef(objectNumber);
/** An annotation (or widget) on page 3, the one page of both annotation fixtures. */
const annot = (objectNumber: number) =>
  ({ kind: 'objectNumber', page: page(3), objectNumber }) as const;
const field = (objectNumber: number) => ({ kind: 'objectNumber', objectNumber }) as const;

/** The scale `measurements.test.ts` writes. */
const SCALE = {
  subtype: 'rectilinear',
  ratio: '1 in = 2 m',
  x: [{ unit: 'm', conversion: 0.25, precision: 100 }],
  distance: [{ unit: 'm', conversion: 1, precision: 100 }],
  area: [{ unit: 'm²', conversion: 1, precision: 100 }],
} as const;

const OTHER_SCALE = {
  subtype: 'rectilinear',
  ratio: '1 in = 4 ft',
  x: [{ unit: 'ft', conversion: 0.5, precision: 10 }],
  distance: [{ unit: 'ft', conversion: 1, precision: 10 }],
  area: [{ unit: 'ft²', conversion: 1, precision: 10 }],
} as const;

const apply = (opId: string, change: unknown): Step => ({ apply: change as Change, opId });
const LIST: Step = { read: 'pages.list' };

const GROUPS: readonly Group[] = [
  {
    file: 'viewports.json',
    kind: 'pages.list.viewports',
    fixture: VIEWPORTS,
    cases: [
      {
        name: 'the-layout-carries-each-pages-viewports',
        covers:
          'Page 3 has a foreign viewport then the engine\'s own, in drawing order, the own one marked owned. Page 4 has none and carries no viewports key. Page 5 is turned a quarter: its geospatial viewport comes back in page space, not owned, with the measure the file has.',
        steps: [LIST],
      },
    ],
  },
  {
    file: 'scale.json',
    kind: 'document.apply.pages.setScale',
    fixture: VIEWPORTS,
    cases: [
      {
        name: 'a-scale-on-a-page-without-one',
        covers:
          'Page 4 gets a viewport of its own, over the visible box (its crop box), named EmbedPDF. The result names the page and carries the new layout; so does the read after it.',
        steps: [apply('scale:add', { ops: [{ type: 'pages.setScale', pages: [page(4)], measure: SCALE }] }), LIST],
      },
      {
        name: 'a-scale-replaces-the-engines-own-viewport',
        covers:
          'Page 3 already has one the engine owns: that one becomes the new scale over the visible page, in its place; the foreign one stays as it is.',
        steps: [apply('scale:replace', { ops: [{ type: 'pages.setScale', pages: [page(3)], measure: OTHER_SCALE }] }), LIST],
      },
      {
        name: 'null-removes-the-engines-own-viewport',
        covers: 'A null measure removes every viewport the engine owns on the page and leaves the foreign one.',
        steps: [apply('scale:remove', { ops: [{ type: 'pages.setScale', pages: [page(3)], measure: null }] }), LIST],
      },
      {
        name: 'null-on-a-page-with-none-of-its-own',
        covers: 'Page 5 has only a geospatial viewport, which the engine does not own: removing the scale leaves it.',
        steps: [apply('scale:none', { ops: [{ type: 'pages.setScale', pages: [page(5)], measure: null }] }), LIST],
      },
      {
        name: 'a-scale-on-a-turned-page',
        covers:
          'Page 5 is turned a quarter. Its own viewport covers the visible page in the file, and the layout reads it back in page space, after the geospatial one.',
        steps: [apply('scale:turned', { ops: [{ type: 'pages.setScale', pages: [page(5)], measure: SCALE }] }), LIST],
      },
      {
        name: 'one-change-scales-several-pages',
        covers:
          'Three pages of three sizes in one op: each takes the scale on its own visible box. The result names the first page; the item names them all.',
        steps: [apply('scale:many', { ops: [{ type: 'pages.setScale', pages: [page(3), page(4), page(5)], measure: SCALE }] }), LIST],
      },
      {
        name: 'a-page-named-twice-is-refused',
        covers: 'The same page twice is refused with InvalidArg before anything is written.',
        steps: [apply('scale:twice', { ops: [{ type: 'pages.setScale', pages: [page(4), page(4)], measure: SCALE }] }), LIST],
      },
      {
        name: 'an-unknown-page-is-refused',
        covers: 'An object number no page has refuses the change.',
        steps: [apply('scale:unknown', { ops: [{ type: 'pages.setScale', pages: [page(99)], measure: SCALE }] })],
      },
      {
        name: 'an-undo-puts-back-each-pages-scale',
        covers:
          'Three pages with three different scales before (another one, none, none of its own): the undo puts each back, in steps grouped by what the page had and its box.',
        steps: [
          apply('scale:set', { ops: [{ type: 'pages.setScale', pages: [page(3), page(4), page(5)], measure: SCALE }] }),
          apply('scale:undo', { undoOf: 'scale:set' }),
          LIST,
          apply('scale:redo', { undoOf: 'scale:undo' }),
          LIST,
        ],
      },
      {
        name: 'an-undo-leaves-a-page-whose-scale-moved-on',
        covers:
          'A page given another scale after the change is not the change\'s to undo: the undo skips it and puts back only the other page.',
        steps: [
          apply('scale:first', { ops: [{ type: 'pages.setScale', pages: [page(3), page(4)], measure: SCALE }] }),
          apply('scale:later', { ops: [{ type: 'pages.setScale', pages: [page(4)], measure: OTHER_SCALE }] }),
          apply('scale:undo-first', { undoOf: 'scale:first' }),
          LIST,
        ],
      },
    ],
  },
  {
    file: 'rename.json',
    kind: 'document.apply.annotations.rename',
    fixture: ANNOTATIONS,
    cases: [
      {
        name: 'an-update-renames',
        covers: 'patch.nm writes the annotation\'s own /NM on the same object; its replies still reply to it.',
        steps: [
          apply('rename:new', { ops: [{ type: 'annotations.update', ref: annot(5), patch: { nm: 'renamed-anchor' } }] }),
          { read: 'annotations.list', page: 3 },
        ],
      },
      {
        name: 'null-removes-the-name',
        covers: 'null removes /NM.',
        steps: [
          apply('rename:null', { ops: [{ type: 'annotations.update', ref: annot(5), patch: { nm: null } }] }),
          { read: 'annotations.list', page: 3 },
        ],
      },
      {
        name: 'an-empty-name-removes-it',
        covers: 'An empty name names nothing: it removes /NM, as a create given one writes none.',
        steps: [apply('rename:empty', { ops: [{ type: 'annotations.update', ref: annot(7), patch: { nm: '' } }] })],
      },
      {
        name: 'a-name-another-annotation-has-is-refused',
        covers: 'A new name must be free on the page (ISO 32000-2 §12.5.2): InvalidArg.',
        steps: [apply('rename:taken', { ops: [{ type: 'annotations.update', ref: annot(5), patch: { nm: 'a-reply' } }] })],
      },
      {
        name: 'the-name-it-has-is-no-change',
        covers: 'A name sent back as it reads is not a new name, and passes.',
        steps: [apply('rename:same', { ops: [{ type: 'annotations.update', ref: annot(5), patch: { nm: 'the-anchor' } }] })],
      },
      {
        name: 'a-name-an-earlier-op-freed',
        covers:
          'Checked against the page as the change\'s earlier ops left it: the first op frees the-anchor, and the second takes it.',
        steps: [
          apply('rename:swap', {
            ops: [
              { type: 'annotations.update', ref: annot(5), patch: { nm: 'was-the-anchor' } },
              { type: 'annotations.update', ref: annot(7), patch: { nm: 'the-anchor' } },
            ],
          }),
          { read: 'annotations.list', page: 3 },
        ],
      },
      {
        name: 'an-undo-puts-the-name-back',
        covers: 'The undo of a rename gives the annotation the name it had.',
        steps: [
          apply('rename:do', { ops: [{ type: 'annotations.update', ref: annot(5), patch: { nm: 'renamed-anchor' } }] }),
          apply('rename:undo', { undoOf: 'rename:do' }),
          { read: 'annotations.list', page: 3 },
        ],
      },
    ],
  },
  {
    file: 'annotation-copy.json',
    kind: 'document.apply.annotations.copy',
    fixture: ANNOTATIONS,
    cases: [
      {
        name: 'copy-one',
        covers:
          'One annotation copied onto its own page, where it was: a new object with the source\'s data, a name of its own, no links. The refMap maps the source to its copy.',
        steps: [
          apply('copy:one', { ops: [{ type: 'annotations.copy', refs: [annot(5)] }] }),
          { read: 'annotations.list', page: 3 },
        ],
      },
      {
        name: 'copy-moved-by-an-offset',
        covers: 'The offset is in page points from the top-left: x to the right, y down.',
        steps: [apply('copy:offset', { ops: [{ type: 'annotations.copy', refs: [annot(5)], offset: { x: 30, y: 40 } }] })],
      },
      {
        name: 'a-reply-copied-with-its-parent-replies-to-its-copy',
        covers:
          'The anchor and two replies (one a reply, one a group member): the copies form a group of their own, each reply replying to the anchor\'s copy with the same type.',
        steps: [
          apply('copy:group', { ops: [{ type: 'annotations.copy', refs: [annot(5), annot(7), annot(8)] }] }),
          { read: 'annotations.list', page: 3 },
        ],
      },
      {
        name: 'a-reply-copied-alone-stands-alone',
        covers: 'A reply whose parent is not copied loses its link.',
        steps: [apply('copy:alone', { ops: [{ type: 'annotations.copy', refs: [annot(7)] }] })],
      },
      {
        name: 'an-unsupported-kind-is-dropped',
        covers: 'A kind the engine does not model is left out, and dropped names it; the rest is copied.',
        steps: [apply('copy:drop', { ops: [{ type: 'annotations.copy', refs: [annot(10), annot(9)] }] })],
      },
      {
        name: 'a-reversed-rect-refuses-the-copy',
        covers:
          'Annotation 6 has its /Rect corners the other way round and reads with a negative width, which its create schema refuses: the whole change is refused with InvalidArg before the first write.',
        steps: [apply('copy:reversed', { ops: [{ type: 'annotations.copy', refs: [annot(9), annot(6)] }] })],
      },
      {
        name: 'one-named-twice-is-copied-twice',
        covers: 'Named twice, copied twice; the refMap has it twice.',
        steps: [apply('copy:twice', { ops: [{ type: 'annotations.copy', refs: [annot(9), annot(9)] }] })],
      },
      {
        name: 'the-copies-take-the-numbers-given',
        covers: 'objectNumbers names each copy, in the order of the refs.',
        steps: [apply('copy:numbers', { ops: [{ type: 'annotations.copy', refs: [annot(5), annot(9)], objectNumbers: [40, 41] }] })],
      },
      {
        name: 'copies-on-another-page',
        covers:
          'With page, the copies go on that page (a blank page the same change inserts at object 60), the same spot from its top-left, moved by the offset; a reply copied with its parent still replies to its copy there.',
        steps: [
          apply('copy:page', {
            ops: [
              { type: 'pages.insertBlank', spec: { size: { width: 300, height: 400 } }, objectNumbers: [60] },
              { type: 'annotations.copy', refs: [annot(5), annot(7)], page: page(60), offset: { x: 10, y: 10 } },
            ],
          }),
          { read: 'annotations.list', page: 60 },
        ],
      },
      {
        name: 'a-page-the-document-lacks-is-refused',
        covers: 'A page no page has refuses the copy before anything is read.',
        steps: [apply('copy:nopage', { ops: [{ type: 'annotations.copy', refs: [annot(5)], page: page(99) }] })],
      },
      {
        name: 'an-unknown-annotation-is-refused',
        covers: 'A ref no annotation has refuses the change.',
        steps: [apply('copy:unknown', { ops: [{ type: 'annotations.copy', refs: [annot(99)] }] })],
      },
      {
        name: 'an-undo-takes-the-copies-away',
        covers: 'The undo deletes the copies; the page reads as it did.',
        steps: [
          apply('copy:do', { ops: [{ type: 'annotations.copy', refs: [annot(5), annot(7)] }] }),
          apply('copy:undo', { undoOf: 'copy:do' }),
          { read: 'annotations.list', page: 3 },
        ],
      },
    ],
  },
  {
    file: 'form-copy.json',
    kind: 'document.apply.forms.copy',
    fixture: FORMS,
    cases: [
      {
        name: 'copy-a-text-field',
        covers:
          'A text field copied: a new field under a free name, with its widget on the same page, and its source\'s value.',
        steps: [apply('fcopy:text', { ops: [{ type: 'forms.copy', fields: [field(4)] }] }), { read: 'forms.list' }],
      },
      {
        name: 'copy-without-values',
        covers: 'values: false leaves the copy at its default value.',
        steps: [apply('fcopy:novalue', { ops: [{ type: 'forms.copy', fields: [field(4)], values: false }] })],
      },
      {
        name: 'copy-a-radio-group',
        covers: 'A radio group copies with both its widgets, moved by the offset.',
        steps: [
          apply('fcopy:radio', { ops: [{ type: 'forms.copy', fields: [field(9)], offset: { x: 0, y: 100 } }] }),
          { read: 'forms.list' },
        ],
      },
      {
        name: 'copy-one-widget-of-a-group',
        covers: 'widgets names the widgets to copy when not every one; without values the copy has its default.',
        steps: [apply('fcopy:widget', { ops: [{ type: 'forms.copy', fields: [field(9)], widgets: [annot(7)], values: false }] })],
      },
      {
        name: 'a-value-on-a-widget-left-out-is-refused',
        covers:
          'The group is on Blue, the export value of widget 8; copying only widget 7 with its value names a value no copied widget has, and the change is refused with InvalidArg.',
        steps: [apply('fcopy:widget-value', { ops: [{ type: 'forms.copy', fields: [field(9)], widgets: [annot(7)] }] })],
      },
      {
        name: 'the-copies-take-the-numbers-given',
        covers: 'objectNumbers: for each field, its own number, then one for each widget copied.',
        steps: [apply('fcopy:numbers', { ops: [{ type: 'forms.copy', fields: [field(9)], objectNumbers: [50, 51, 52] }] })],
      },
      {
        name: 'copies-on-another-page',
        covers:
          'With page, every copied widget goes on that page (a blank page the same change inserts at object 60), the same spot from its top-left.',
        steps: [
          apply('fcopy:page', {
            ops: [
              { type: 'pages.insertBlank', spec: { size: { width: 300, height: 400 } }, objectNumbers: [60] },
              { type: 'forms.copy', fields: [field(9)], page: page(60) },
            ],
          }),
          { read: 'forms.list' },
        ],
      },
      {
        name: 'an-unknown-field-is-refused',
        covers: 'A field ref no field has refuses the change.',
        steps: [apply('fcopy:unknown', { ops: [{ type: 'forms.copy', fields: [field(9999)] }] })],
      },
      {
        name: 'an-undo-takes-the-copies-away',
        covers: 'The undo deletes the copied fields and their widgets; the form reads as it did.',
        steps: [
          apply('fcopy:do', { ops: [{ type: 'forms.copy', fields: [field(4), field(9)] }] }),
          apply('fcopy:undo', { undoOf: 'fcopy:do' }),
          { read: 'forms.list' },
        ],
      },
    ],
  },
  {
    file: 'widget-rename.json',
    kind: 'document.apply.forms.updateWidget.rename',
    fixture: FORMS,
    cases: [
      {
        name: 'a-widget-update-renames',
        covers: 'patch.nm on forms.updateWidget writes the widget\'s own /NM, as an annotation update does.',
        steps: [
          apply('wrename:new', { ops: [{ type: 'forms.updateWidget', widget: annot(7), patch: { nm: 'red-widget' } }] }),
          { read: 'annotations.list', page: 3 },
        ],
      },
      {
        name: 'null-removes-the-widgets-name',
        covers: 'null removes /NM.',
        steps: [apply('wrename:null', { ops: [{ type: 'forms.updateWidget', widget: annot(7), patch: { nm: null } }] })],
      },
      {
        name: 'a-name-another-annotation-has-is-refused',
        covers:
          'A new name must be free on the page, as the change\'s earlier ops left it: the first op names widget 7 shared, so the second is refused with InvalidArg.',
        steps: [
          apply('wrename:taken', {
            ops: [
              { type: 'forms.updateWidget', widget: annot(7), patch: { nm: 'shared' } },
              { type: 'forms.updateWidget', widget: annot(8), patch: { nm: 'shared' } },
            ],
          }),
        ],
      },
    ],
  },
];

// --- Recording at the worker boundary ---------------------------------------

/** Every name a copy makes up is a UUIDv7. */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

/**
 * A write stamps `modifiedAt` (and a copy its dates) from the clock. Every
 * date the fixtures carry is from 2024, so a timestamp after 2025 is the
 * clock's, and is recorded as `"<now>"`.
 */
const TIMESTAMP = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$/;
const CLOCK_AFTER = Date.parse('2025-01-01T00:00:00Z');

/** The distinct names `plain` masked since the last `takeUuids()`. */
let maskedUuids = new Set<string>();
function takeUuids(): number {
  const count = maskedUuids.size;
  maskedUuids = new Set();
  return count;
}

const hex = (bytes: Uint8Array) => Buffer.from(bytes).toString('hex');

function plain(value: unknown): unknown {
  if (value instanceof ArrayBuffer) return { $hex: hex(new Uint8Array(value)) };
  if (ArrayBuffer.isView(value)) {
    return { $hex: hex(new Uint8Array(value.buffer, value.byteOffset, value.byteLength)) };
  }
  if (Array.isArray(value)) return value.map(plain);
  if (typeof value === 'string' && UUID.test(value)) {
    maskedUuids.add(value);
    return '<uuid>';
  }
  if (typeof value === 'string' && TIMESTAMP.test(value) && Date.parse(value) >= CLOCK_AFTER)
    return '<now>';
  if (value === null || typeof value !== 'object') return value;
  return Object.fromEntries(
    Object.entries(value)
      .filter(([, entry]) => entry !== undefined)
      .map(([key, entry]) => {
        if (key === 'jobId') return [key, '<job>'];
        if (key === 'docId') return [key, '<doc>'];
        return [key, plain(entry)];
      }),
  );
}

interface Exchange {
  request: unknown;
  answer?: unknown;
  /** How many distinct names the answer's `<uuid>` masks stand for. */
  uuids?: number;
}

function recordingEngine(): { engine: LocalEngine; take: () => Exchange[] } {
  let exchanges: Exchange[] = [];
  const byJob = new Map<unknown, Exchange>();
  const inner = new LazyTransport(() =>
    createInlineTransport({
      prefer: 'wasm',
      wasm: {
        print: (message: string) => process.stderr.write(`${message}\n`),
        printErr: (message: string) => process.stderr.write(`${message}\n`),
      },
    }),
  );
  const transport: Transport = {
    send(pack) {
      const request = pack.payload as WorkerRequest & { jobId?: unknown };
      const exchange: Exchange = { request: plain(request) };
      takeUuids();
      exchanges.push(exchange);
      byJob.set(request.jobId, exchange);
      inner.send(pack);
    },
    start: () => inner.start(),
    onMessage: (handler) =>
      inner.onMessage((message) => {
        const response = ((message as { payload?: unknown }).payload ?? message) as {
          kind: string;
          jobId: unknown;
          result?: unknown;
          error?: { code?: unknown };
        };
        const exchange = byJob.get(response.jobId);
        if (exchange && exchange.answer === undefined) {
          exchange.answer =
            response.kind === 'resolve'
              ? { resolve: plain(response.result) }
              : { reject: String(response.error?.code) };
          exchange.uuids = takeUuids();
        }
        handler(message);
      }),
    terminate: () => inner.terminate(),
  };
  return {
    engine: LocalEngine.fromTransport({ transport }),
    take: () => {
      const taken = exchanges;
      exchanges = [];
      return taken;
    },
  };
}

async function runCase(group: Group, entry: Case): Promise<unknown> {
  const bytes =
    typeof group.fixture === 'string'
      ? new Uint8Array(await readFile(join(repoRoot, group.fixture)))
      : group.fixture.bytes.slice();
  const { engine, take } = recordingEngine();
  try {
    const doc = await engine.open(
      { kind: 'bytes', id: `changes:${entry.name}`, bytes },
      {
        scope: [...(entry.open?.scope ?? ['*'])],
        ...(entry.open?.identity ? { identity: entry.open.identity } : {}),
      },
    );
    take();
    const steps: unknown[] = [];
    try {
      for (const step of entry.steps) {
        let events: unknown;
        try {
          if ('apply' in step) {
            const result = await doc.apply(step.apply, { opId: step.opId });
            events = plain(changeEvents(result as unknown as ChangeResult));
            takeUuids();
          } else if ('call' in step) {
            await callStep(doc, group, step);
          } else if (step.read === 'pages.list') await doc.pages.list();
          else if (step.read === 'annotations.list')
            await doc.page(toPageRef(step.page)).annotations.list();
          else await doc.forms.list();
        } catch (error) {
          if ((error as { code?: unknown })?.code === undefined) throw error;
        }
        steps.push({ ...step, exchanges: take(), ...(events === undefined ? {} : { events }) });
      }
    } finally {
      await doc.close();
    }
    return { name: entry.name, covers: entry.covers, steps };
  } finally {
    await engine.destroy();
  }
}

/** The mark a step names, as its caller passes it. */
function markOf(group: Group, mark: Mark): unknown {
  if (mark === null) return null;
  const bytes = (name: string) => group.fixtures![name]!.slice();
  switch (mark) {
    case 'png':
      return bytes('png');
    case 'jpeg':
      return bytes('jpeg');
    case 'pdf':
      return bytes('pdf');
    case 'pdfKeyed':
      return { pdf: bytes('pdf') };
    case 'pngKeyed':
      return { pdf: bytes('png') };
    case 'twoPage':
      return bytes('twoPage');
    case 'twoPageKeyed':
      return { pdf: bytes('twoPage') };
    case 'markPdfKeyed':
      return { pdf: bytes('markPdf') };
    case 'notAPicture':
      return new TextEncoder().encode('no picture at all');
  }
}

type Called = Extract<Step, { readonly call: string }>;

/** One verb of the forms service, with the op id the step names. */
async function callStep(doc: Awaited<ReturnType<LocalEngine['open']>>, group: Group, step: Called) {
  const forms = doc.forms as unknown as Record<string, (...args: unknown[]) => Promise<unknown>>;
  const options = { opId: step.opId };
  switch (step.call) {
    case 'forms.create':
      return forms.create!(step.draft, options);
    case 'forms.update':
      return forms.update!(step.field, step.patch, options);
    case 'forms.updateWidget': {
      const { fields, widgets } = await doc.forms.list();
      const ref = fields.find((f) => f.name === step.widgetOf)!.widgets[0]!.ref!;
      const row = widgets.find((w) => JSON.stringify(w.ref) === JSON.stringify(ref))!;
      const patch = Object.fromEntries(
        Object.entries(step.patch).map(([key, value]) => [key, value === '$rect' ? row.rect : value]),
      );
      return forms.updateWidget!(ref, patch, options);
    }
    case 'forms.setSignatureAppearance':
      return forms.setSignatureAppearance!(step.field, markOf(group, step.mark), options);
    case 'forms.copy':
      return forms.copy!(step.fields, options);
  }
}

// --- Cases of the 2026-11-27 catch-up ----------------------------------------

const fqn = (name: string) => ({ kind: 'fqn', name }) as const;
const create = (opId: string, draft: unknown): Step => ({ call: 'forms.create', draft, opId });
const update = (opId: string, name: string, patch: unknown): Step => ({
  call: 'forms.update',
  field: fqn(name),
  patch,
  opId,
});
const applyUpdate = (opId: string, name: string, patch: unknown, expect?: unknown): Step =>
  apply(opId, {
    ops: [{ type: 'forms.update', field: fqn(name), patch, ...(expect ? { expect } : {}) }],
  });
const undo = (opId: string, of: string): Step => apply(opId, { undoOf: of });
const FORMS_LIST: Step = { read: 'forms.list' };

/** `FIELD_PURPOSE_FORMAT_TOKENS`. */
const PF = {
  designer: { scope: ['doc.open', 'doc.render', 'doc.download', 'doc.forms.modify'] },
  filler: { scope: ['doc.open', 'doc.render', 'doc.forms.fill'] },
  reader: { scope: ['doc.open', 'doc.render', 'doc.forms.read'] },
} as const satisfies Record<string, Open>;

const DATE_FORMAT = 'AFDate_FormatEx("m/d/yy");';
const pfRect = (row: number) => ({ x: 40, y: 40 + row * 30, width: 200, height: 24 });
const placed = (row: number) => [{ page: toPageRef(3), rect: pfRect(row) }];

const PURPOSES_REFUSED: readonly unknown[] = [
  '',
  ' email',
  'email ',
  '\tname',
  'p'.repeat(257),
  'billing\u0000account',
  7,
  {},
];

const FORMATS_REFUSED: readonly unknown[] = [
  { kind: 'other' },
  { kind: 'number', pattern: '0.00' },
  { kind: 'date' },
  { kind: 'date', pattern: '' },
  { kind: 'date', pattern: 'm/d/yy");app.alert("x' },
  { kind: 'date', pattern: 'm\\d' },
  { kind: 'date', pattern: "d 'of' mmmm" },
  { kind: 'date', pattern: 'dddd, mmmm d' },
  { kind: 'date', pattern: 'yyy' },
  { kind: 'date', pattern: 'd/m/y' },
  { kind: 'date', pattern: 'yyyymmdd' },
  { kind: 'time', pattern: 'HH.MM' },
  { kind: 'time', pattern: 'H:M' },
  { kind: 'date', pattern: 'dd:mm:yyyy' },
  { kind: 'date', pattern: '/ - .' },
  { kind: 'date', pattern: `dd${' '.repeat(59)}yyyy` },
  { kind: 'date', pattern: 'yyyy/mm/mm/dd' },
  { kind: 'date', pattern: 'mmm mm d' },
  { kind: 'time', pattern: 'HH:MM s' },
  { kind: 'date', pattern: 'HH:MM' },
  { kind: 'date', pattern: 'h:MM:ss tt' },
  { kind: 'time', pattern: 'dd/mm/yyyy' },
  { kind: 'time', pattern: 'yyyy' },
  { kind: 'time', pattern: 'm/d/yy HH:MM' },
  'dd/mm/yyyy',
];

const SCRIPT = { type: 'javascript', script: 'event.rc = true;' } as const;
const DATE = { kind: 'date', pattern: 'dd/mm/yyyy' } as const;

const PURPOSE_FORMAT_CASES: readonly Case[] = [
  {
    name: 'a-format-and-a-purpose-read-as-acrobat-decides',
    covers:
      "Each field's format from its format script alone, Acrobat's date and time calls in every wording, other scripts as other, a keystroke script alone as none; a purpose from /EMBD_Metadata /Purpose, one with space around it as none; list and signature fields carry no format.",
    open: PF.reader,
    steps: [FORMS_LIST],
  },
  {
    name: 'a-create-names-a-purpose-on-every-kind',
    covers: 'A text field, a checkbox, a signature field and a push button each take a purpose; left out or null, none.',
    open: PF.designer,
    steps: [
      create('pf:c1', { kind: 'text', name: 'made_email', purpose: 'email', widgets: placed(0) }),
      create('pf:c2', { kind: 'checkbox', name: 'made_consent', purpose: 'acme:consent', widgets: placed(1) }),
      create('pf:c3', { kind: 'signature', name: 'made_signature', purpose: 'signature', widgets: placed(2) }),
      create('pf:c4', { kind: 'pushbutton', name: 'made_button', purpose: 'initials', widgets: placed(3) }),
      create('pf:c5', { kind: 'text', name: 'made_plain', widgets: placed(4) }),
      create('pf:c6', { kind: 'text', name: 'made_none', purpose: null, widgets: placed(5) }),
      FORMS_LIST,
    ],
  },
  {
    name: 'an-update-sets-keeps-and-removes-a-purpose',
    covers:
      'A purpose is form design alone, on a field in a group the designer may not assign; left out it stays; null takes it away; none to take away is no change; a signature field has one like any other.',
    open: PF.designer,
    steps: [
      update('pf:u1', 'full_name', { purpose: 'organization' }),
      update('pf:u2', 'full_name', { required: true }),
      update('pf:u3', 'full_name', { purpose: null }),
      update('pf:u4', 'plain_one', { purpose: null }),
      update('pf:u5', 'sign_here', { purpose: 'initials' }),
    ],
  },
  {
    name: 'a-purpose-that-is-none-is-refused',
    covers:
      'An empty purpose, one with space at either end, one past 256 characters, one with a NUL, a number and an object are refused by an update and a create with InvalidArg; the longest is taken, and any other character is kept.',
    open: PF.designer,
    steps: [
      ...PURPOSES_REFUSED.flatMap((purpose, at) => [
        update(`pf:np${at}`, 'full_name', { purpose }),
        create(`pf:nc${at}`, { kind: 'text', name: 'never_made', purpose }),
      ]),
      update('pf:long', 'full_name', { purpose: 'p'.repeat(256) }),
      ...['two\nlines', 'caf\u00e9', '\u65e5\u4ed8', 'a(b)\\c', 'a\u0001b'].map((purpose, at) =>
        update(`pf:kept${at}`, 'full_name', { purpose }),
      ),
      FORMS_LIST,
    ],
  },
  {
    name: 'undo-puts-a-purpose-back',
    covers: 'Undo puts a purpose back and its undo sets it again; after a later change the undo puts it back by value.',
    open: PF.designer,
    steps: [
      applyUpdate('pf:set', 'full_name', { purpose: 'email' }),
      undo('pf:undo', 'pf:set'),
      undo('pf:redo', 'pf:undo'),
      applyUpdate('pf:removed', 'sign_here', { purpose: null }),
      applyUpdate('pf:later', 'sign_here', { required: true }),
      undo('pf:undo2', 'pf:removed'),
      FORMS_LIST,
    ],
  },
  {
    name: 'an-update-that-expects-a-purpose',
    covers: 'expect: { purpose } holds the update to a field that has it: null on full_name is a ChangeConflict, its own purpose applies.',
    open: PF.designer,
    steps: [
      applyUpdate('pf:e1', 'full_name', { purpose: 'email' }, { purpose: null }),
      applyUpdate('pf:e2', 'full_name', { purpose: 'email' }, { purpose: 'name' }),
    ],
  },
  {
    name: 'a-create-writes-a-date-and-a-time-as-acrobat-does',
    covers:
      "A text field's date, a text field's time (the keystroke script names the standard time pattern by number) and a dropdown's date are written as the two scripts Acrobat writes, with form design alone; left out or null, none.",
    open: PF.designer,
    steps: [
      create('pf:d1', { kind: 'text', name: 'made_date', format: { kind: 'date', pattern: 'dd/mm/yyyy' }, widgets: placed(0) }),
      create('pf:d2', { kind: 'text', name: 'made_time', format: { kind: 'time', pattern: 'h:MM tt' }, widgets: placed(1) }),
      create('pf:d3', {
        kind: 'combobox',
        name: 'made_dropdown',
        edit: true,
        format: { kind: 'date', pattern: 'mmm d, yyyy' },
        widgets: placed(2),
      }),
      create('pf:d4', { kind: 'text', name: 'made_plain', widgets: placed(3) }),
      create('pf:d5', { kind: 'text', name: 'made_none', format: null, widgets: placed(4) }),
    ],
  },
  {
    name: 'an-update-sets-replaces-and-removes-a-format',
    covers: 'A format is set, kept when left out, replaced by a time, removed by null with both scripts; a dropdown takes one.',
    open: PF.designer,
    steps: [
      update('pf:f1', 'plain_six', { format: { kind: 'date', pattern: 'yyyy-mm-dd' } }),
      update('pf:f2', 'plain_six', { required: true }),
      update('pf:f3', 'plain_six', { format: { kind: 'time', pattern: 'HH:MM:ss' } }),
      update('pf:f4', 'plain_six', { format: null }),
      update('pf:f5', 'dropdown_date', { format: { kind: 'date', pattern: 'd-mmm-yy' } }),
    ],
  },
  {
    name: 'a-format-owns-the-two-scripts',
    covers:
      "null removes another format with its keystroke script; with no format, a keystroke script alone stays; a format replaces a keystroke script it didn't write.",
    open: PF.designer,
    steps: [
      update('pf:o1', 'plain_five', { format: null }),
      update('pf:o2', 'plain_three', { format: null }),
      update('pf:o3', 'own_keystroke', { format: { kind: 'time', pattern: 'HH:MM' } }),
    ],
  },
  {
    name: 'the-format-a-field-has-changes-nothing',
    covers:
      "The format a field already has, in Acrobat's older numbered wording, changes nothing; an other format and a date with its own keystroke script stay through a write of something else.",
    open: PF.designer,
    steps: [
      update('pf:s1', 'plain_four', { format: { kind: 'date', pattern: 'm/d' } }),
      update('pf:s2', 'plain_five', { required: true }),
      update('pf:s3', 'own_keystroke', { alternateName: 'A date' }),
    ],
  },
  {
    name: 'a-format-that-is-none-is-refused',
    covers:
      'Another kind, a pattern of no kind, one whose writing does not read back, one with a part twice, one of the other kind and a bare string are refused by an update and a create with InvalidArg before anything is written; the longest pattern is taken; a date may carry a time.',
    steps: [
      ...FORMATS_REFUSED.flatMap((format, at) => [
        update(`pf:nf${at}`, 'plain_one', { format }),
        create(`pf:nfc${at}`, { kind: 'text', name: 'never_made', format }),
      ]),
      update('pf:longest', 'plain_one', { format: { kind: 'date', pattern: `dd${' '.repeat(58)}yyyy` } }),
      update('pf:withtime', 'plain_one', { format: { kind: 'date', pattern: 'dd/mm/yyyy HH:MM' } }),
      FORMS_LIST,
    ],
  },
  {
    name: 'a-format-goes-on-a-text-field-and-a-dropdown-only',
    covers:
      'A list and a signature field refuse a format and null; a checkbox create refuses one; a format beside actions.format, actions.keystroke or a null keystroke is refused; beside a validate script it goes.',
    steps: [
      ...['list_with_script', 'sign_here'].flatMap((name, at) => [
        update(`pf:fam${at}`, name, { format: DATE }),
        update(`pf:famnull${at}`, name, { format: null }),
      ]),
      create('pf:famcheck', { kind: 'checkbox', name: 'never_made', format: DATE }),
      ...[{ format: SCRIPT }, { keystroke: SCRIPT }, { keystroke: null }].flatMap((actions, at) => [
        update(`pf:beside${at}`, 'plain_six', { format: DATE, actions }),
        create(`pf:besidec${at}`, { kind: 'text', name: 'never_made', format: DATE, actions }),
      ]),
      update('pf:both', 'plain_six', { format: DATE, actions: { validate: SCRIPT } }),
    ],
  },
  {
    name: 'a-script-write-changes-the-format',
    covers: "A format read back is the scripts': a date script makes a date, other code makes other, removing it makes none.",
    steps: [
      update('pf:w1', 'plain_six', { actions: { format: { type: 'javascript', script: 'AFDate_FormatEx("d/m/yy")' } } }),
      update('pf:w2', 'plain_six', { actions: { format: { type: 'javascript', script: 'event.value = "x";' } } }),
      update('pf:w3', 'plain_six', { actions: { format: null } }),
    ],
  },
  {
    name: 'undo-puts-a-format-back',
    covers:
      'Undo puts back the older wording a format replaced, and its undo sets the format again; a removed number format comes back whole; after a later change the undo puts a set format back by value.',
    open: PF.designer,
    steps: [
      applyUpdate('pf:uf1', 'plain_four', { format: { kind: 'time', pattern: 'HH:MM' } }),
      undo('pf:uf1u', 'pf:uf1'),
      undo('pf:uf1r', 'pf:uf1u'),
      applyUpdate('pf:uf2', 'plain_five', { format: null }),
      undo('pf:uf2u', 'pf:uf2'),
      applyUpdate('pf:uf3', 'plain_six', { format: DATE }),
      applyUpdate('pf:uf3l', 'plain_six', { required: true }),
      undo('pf:uf3u', 'pf:uf3'),
      FORMS_LIST,
    ],
  },
  {
    name: 'after-a-later-change-the-undo-of-a-format-puts-back-what-it-replaced',
    covers: 'A number format and an older wording a format replaced come back as written after a later change to the field.',
    open: PF.designer,
    steps: [
      applyUpdate('pf:l1', 'plain_five', { format: DATE }),
      applyUpdate('pf:l1l', 'plain_five', { required: true }),
      undo('pf:l1u', 'pf:l1'),
      applyUpdate('pf:l2', 'plain_four', { format: { kind: 'time', pattern: 'HH:MM' } }),
      applyUpdate('pf:l2l', 'plain_four', { required: true }),
      undo('pf:l2u', 'pf:l2'),
      FORMS_LIST,
    ],
  },
  {
    name: 'the-undo-of-a-format-leaves-a-script-written-since',
    covers: 'A keystroke script written after a format is someone else\'s work: the undo is skipped and both scripts stay.',
    steps: [
      applyUpdate('pf:k1', 'plain_six', { format: DATE }),
      applyUpdate('pf:k2', 'plain_six', {
        actions: { keystroke: { type: 'javascript', script: 'event.rc = /^[0-9/]*$/.test(event.value);' } },
      }),
      undo('pf:k1u', 'pf:k1'),
      FORMS_LIST,
    ],
  },
  {
    name: 'the-undo-of-a-format-over-a-goto-puts-all-back',
    covers: 'With nothing changed since, the undo of a format over a script that goes on to a GoTo puts the script and the GoTo back.',
    open: PF.designer,
    steps: [applyUpdate('pf:g1', 'goes_on', { format: DATE }), undo('pf:g1u', 'pf:g1'), FORMS_LIST],
  },
  {
    name: 'the-undo-of-a-format-over-a-goto-after-a-change-puts-nothing-back',
    covers: "After a later change the GoTo can't go back by value, so neither script is touched and the undo is skipped.",
    open: PF.designer,
    steps: [
      applyUpdate('pf:g2', 'goes_on', { format: DATE }),
      applyUpdate('pf:g2l', 'goes_on', { required: true }),
      undo('pf:g2u', 'pf:g2'),
      FORMS_LIST,
    ],
  },
  {
    name: 'the-undo-of-a-script-over-a-goto-after-a-change-puts-nothing-back',
    covers: 'A script written over such actions follows the same rule.',
    steps: [
      applyUpdate('pf:g3', 'goes_on', { actions: { format: { type: 'javascript', script: 'event.value = event.value.trim();' } } }),
      applyUpdate('pf:g3l', 'goes_on', { required: true }),
      undo('pf:g3u', 'pf:g3'),
    ],
  },
  {
    name: 'an-update-that-expects-a-format',
    covers: 'expect: { format } holds the update to a field that has it: null on plain_one is a ChangeConflict, its own date applies.',
    open: PF.designer,
    steps: [
      applyUpdate('pf:x1', 'plain_one', { format: { kind: 'time', pattern: 'HH:MM' } }, { format: null }),
      applyUpdate('pf:x2', 'plain_one', { format: { kind: 'time', pattern: 'HH:MM' } }, { format: { kind: 'date', pattern: 'm/d/yy' } }),
    ],
  },
  {
    name: 'a-copy-keeps-a-purpose-and-a-format-without-the-script-right',
    covers:
      'Without doc.forms.script a copy keeps a purpose and a date or time format, written from the pattern, and reports nothing dropped.',
    open: PF.designer,
    steps: [
      update('pf:cp0', 'plain_one', { purpose: 'date-signed' }),
      { call: 'forms.copy', fields: ['plain_one', 'time_numbered', 'dropdown_date', 'sign_here'].map(fqn), opId: 'pf:cp1' },
    ],
  },
  {
    name: 'a-copy-without-the-script-right-leaves-out-what-is-no-date-or-time',
    covers: "An other format is left out and reported; a date's own keystroke script of other code is reported and the copy gets the format's own.",
    open: PF.designer,
    steps: [{ call: 'forms.copy', fields: ['plain_five', 'own_keystroke'].map(fqn), opId: 'pf:cp2' }],
  },
  {
    name: 'a-copy-with-the-script-right-keeps-the-scripts-as-written',
    covers: 'With doc.forms.script a copy keeps the older wording and a keystroke script of its own as they are.',
    steps: [{ call: 'forms.copy', fields: ['plain_four', 'own_keystroke'].map(fqn), opId: 'pf:cp3' }],
  },
  {
    name: 'both-take-form-design',
    covers:
      'A filler is refused a purpose and a format by an update and a create (Forbidden, doc.forms.modify) and reads both; a designer is refused the same format as a script of her own (doc.forms.script) and may set the format and a purpose.',
    open: PF.filler,
    steps: [
      update('pf:r1', 'plain_six', { purpose: 'email' }),
      update('pf:r2', 'plain_six', { format: { kind: 'date', pattern: 'm/d/yy' } }),
      update('pf:r3', 'plain_one', { format: null }),
      create('pf:r4', { kind: 'text', name: 'never_made', purpose: 'email' }),
      create('pf:r5', { kind: 'text', name: 'never_made', format: { kind: 'date', pattern: 'm/d/yy' } }),
      FORMS_LIST,
    ],
  },
  {
    name: 'a-designer-writes-a-format-not-its-script',
    covers: 'The same format as a script takes doc.forms.script, which a designer lacks; as a format, with a purpose, it is form design.',
    open: PF.designer,
    steps: [
      update('pf:r6', 'plain_six', { actions: { format: { type: 'javascript', script: DATE_FORMAT } } }),
      update('pf:r7', 'plain_six', { format: { kind: 'date', pattern: 'm/d/yy' }, purpose: 'date-signed' }),
    ],
  },
];

/** `FIELD_GROUP_TOKENS`. */
const FG = {
  seller: { scope: ['doc.open', 'doc.render', 'fields:*:group=seller'], identity: {} },
  buyer: {
    scope: ['doc.open', 'doc.render', 'fields:fill:group=buyer', 'fields:sign:group=buyer'],
    identity: { userId: 'alice', displayName: 'Alice' },
  },
  designer: {
    scope: ['doc.open', 'doc.render', 'doc.forms.modify', 'fields:set-group:group=buyer'],
    identity: { userId: 'lisa', groupId: 'legal' },
  },
} as const satisfies Record<string, Open>;

const fgPlaced = (x: number) => [{ page: toPageRef(3), rect: { x, y: 200, width: 100, height: 20 } }];

const FIELD_GROUP_CASES: readonly Case[] = [
  {
    name: 'a-new-field-is-in-no-group-unless-its-draft-names-one',
    covers: "A designer whose own group is legal makes a field in no group, left out, null, or as one op of a change.",
    open: FG.designer,
    steps: [
      create('fg:c1', { kind: 'text', name: 'clause', widgets: fgPlaced(20) }),
      create('fg:c2', { kind: 'text', name: 'note', groupId: null, widgets: fgPlaced(140) }),
      apply('fg:c3', { ops: [{ type: 'forms.create', draft: { kind: 'text', name: 'annex', widgets: fgPlaced(260) } }] }),
      FORMS_LIST,
    ],
  },
  {
    name: 'naming-her-own-group-takes-no-right',
    covers: "Her own group takes no right, the buyer's takes fields:set-group she has, the seller's is Forbidden, by a create and an update.",
    open: FG.designer,
    steps: [
      create('fg:o1', { kind: 'text', name: 'clause', groupId: 'legal', widgets: fgPlaced(20) }),
      create('fg:o2', { kind: 'text', name: 'buyer_address', groupId: 'buyer', widgets: fgPlaced(140) }),
      create('fg:o3', { kind: 'text', name: 'seller_address', groupId: 'seller', widgets: fgPlaced(260) }),
      update('fg:o4', 'clause', { groupId: 'buyer' }),
      update('fg:o5', 'clause', { groupId: 'legal' }),
      update('fg:o6', 'clause', { groupId: 'seller' }),
    ],
  },
  {
    name: 'group-id-null-takes-a-field-out-of-its-group',
    covers: 'null takes a field out of a group she has no right to assign, with form design alone; a field in none stays in none; back in is an assignment again.',
    open: FG.designer,
    steps: [
      update('fg:n1', 'seller_name', { groupId: null }),
      update('fg:n2', 'buyer_name', { groupId: null }),
      update('fg:n3', 'total', { groupId: null }),
      update('fg:n4', 'seller_name', { groupId: 'seller' }),
      update('fg:n5', 'buyer_name', { groupId: 'buyer' }),
      FORMS_LIST,
    ],
  },
  {
    name: 'taking-a-field-out-of-its-group-takes-form-design',
    covers: 'The seller, who may set the seller group but not design the form, is refused null by a verb and by a change.',
    open: FG.seller,
    steps: [
      update('fg:s1', 'seller_name', { groupId: null }),
      applyUpdate('fg:s2', 'seller_name', { groupId: null }),
      FORMS_LIST,
    ],
  },
  {
    name: 'an-empty-group-id-is-refused',
    covers: 'An empty id, by a create and an update, and one past 256 characters are InvalidArg, not no group.',
    steps: [
      create('fg:e1', { kind: 'text', name: 'clause', groupId: '', widgets: fgPlaced(20) }),
      update('fg:e2', 'buyer_name', { groupId: '' }),
      update('fg:e3', 'buyer_name', { groupId: 'g'.repeat(257) }),
      FORMS_LIST,
    ],
  },
  {
    name: 'undo-puts-a-removed-group-back',
    covers: 'Undo puts a removed group back and its undo takes it out again; undo takes an assigned group away and its undo puts it back.',
    open: FG.designer,
    steps: [
      applyUpdate('fg:u1', 'seller_name', { groupId: null }),
      undo('fg:u1u', 'fg:u1'),
      undo('fg:u1r', 'fg:u1u'),
      applyUpdate('fg:u2', 'total', { groupId: 'buyer' }),
      undo('fg:u2u', 'fg:u2'),
      undo('fg:u2r', 'fg:u2u'),
      FORMS_LIST,
    ],
  },
  {
    name: 'undo-of-a-group-change-holds-after-a-later-change',
    covers: 'After a later change to the field, the undo puts the group back by value and keeps the later change.',
    open: FG.designer,
    steps: [
      applyUpdate('fg:l1', 'buyer_name', { groupId: null }),
      applyUpdate('fg:l1l', 'buyer_name', { required: true }),
      undo('fg:l1u', 'fg:l1'),
      applyUpdate('fg:l2', 'total', { groupId: 'buyer' }),
      applyUpdate('fg:l2l', 'total', { required: true }),
      undo('fg:l2u', 'fg:l2'),
      FORMS_LIST,
    ],
  },
  {
    name: 'an-update-that-expects-no-group',
    covers: 'expect: { groupId: null } on a field in a group is a ChangeConflict; on one in none it applies.',
    open: FG.designer,
    steps: [
      applyUpdate('fg:x1', 'seller_name', { groupId: 'buyer' }, { groupId: null }),
      applyUpdate('fg:x2', 'total', { groupId: 'buyer' }, { groupId: null }),
    ],
  },
  {
    name: 'a-copy-of-a-field-in-no-group-is-in-none',
    covers: "A copy of a field in no group is in none, of the buyer's keeps it; a copy of the seller's is Forbidden.",
    open: FG.designer,
    steps: [
      { call: 'forms.copy', fields: [fqn('total'), fqn('buyer_name')], opId: 'fg:cp1' },
      { call: 'forms.copy', fields: [fqn('seller_name')], opId: 'fg:cp2' },
    ],
  },
];

/** One empty 300 × 300 page (object 3), the mark suite's `PAGE_PDF`. */
const MARK_PAGE_PDF = pdfOf([
  '<< /Type /Catalog /Pages 2 0 R >>',
  '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
  '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Contents 4 0 R /Resources << >> >>',
  '<< /Length 0 >>\nstream\n\nendstream',
]);

const MARK_DRAWING = '1 0 0 rg 0 0 40 40 re f 0 0 1 rg 40 30 80 10 re f';
/** The rotation suite's `MARK_PDF`: 120 × 40, red over its left third, a blue bar along the rest of its top. */
const MARK_PDF = pdfOf([
  '<< /Type /Catalog /Pages 2 0 R >>',
  '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
  '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 120 40] /Contents 4 0 R >>',
  `<< /Length ${MARK_DRAWING.length} >>\nstream\n${MARK_DRAWING}\nendstream`,
]);

const MARKS = {
  png: BANDS_PNG,
  jpeg: BANDS_JPEG,
  pdf: BANDS_PDF,
  twoPage: TWO_PAGE_PDF,
  markPdf: MARK_PDF,
};

const BANDS_BOX = { x: 30, y: 30, width: 150, height: 50 };
const signatureField = (opId: string, name: string, placement: Record<string, unknown> = { box: BANDS_BOX }, styled = false) =>
  create(opId, {
    kind: 'signature',
    name,
    widgets: [
      { page: toPageRef(3), ...placement, ...(styled ? { color: '#1a1a1a', interiorColor: '#eeeeee' } : {}) },
    ],
  });
const mark = (opId: string, name: string, which: Mark): Step => ({
  call: 'forms.setSignatureAppearance',
  field: fqn(name),
  mark: which,
  opId,
});

const SIGNATURE_MARK_CASES: readonly Case[] = [
  {
    name: 'a-png-a-jpeg-and-a-pdf-fill-a-signature-field',
    covers: 'A PNG, a JPEG, a PDF as its bytes and under { pdf } each fill a new signature field: the widget has a mark.',
    steps: [
      signatureField('sm:f1', 'png'),
      mark('sm:m1', 'png', 'png'),
      signatureField('sm:f2', 'jpeg', { box: { ...BANDS_BOX, y: 90 } }),
      mark('sm:m2', 'jpeg', 'jpeg'),
      signatureField('sm:f3', 'pdf', { box: { ...BANDS_BOX, y: 150 } }),
      mark('sm:m3', 'pdf', 'pdf'),
      signatureField('sm:f4', 'keyed', { box: { ...BANDS_BOX, y: 210 } }),
      mark('sm:m4', 'keyed', 'pdfKeyed'),
      FORMS_LIST,
    ],
  },
  {
    name: 'other-bytes-and-two-pages-are-refused',
    covers:
      'Bytes that are no PNG, JPEG or PDF are refused before anything is sent; an image under { pdf } is refused as a PDF; a two-page PDF is refused as bytes and under { pdf }; the field is left without a mark.',
    steps: [
      signatureField('sm:r0', 'sig'),
      mark('sm:r1', 'sig', 'notAPicture'),
      mark('sm:r2', 'sig', 'pngKeyed'),
      mark('sm:r3', 'sig', 'twoPage'),
      mark('sm:r4', 'sig', 'twoPageKeyed'),
      FORMS_LIST,
    ],
  },
  {
    name: 'no-mark-takes-the-mark-out',
    covers: "null takes the mark out of a styled field: the widget has no mark and draws its box again, and its style can change after.",
    steps: [
      signatureField('sm:c0', 'sig', { box: BANDS_BOX }, true),
      mark('sm:c1', 'sig', 'png'),
      mark('sm:c2', 'sig', null),
      FORMS_LIST,
    ],
  },
  {
    name: 'undo-of-a-clear-brings-the-mark-back',
    covers: 'The undo of a clear brings the mark back; its undo takes it out again.',
    steps: [
      signatureField('sm:u0', 'sig'),
      mark('sm:u1', 'sig', 'png'),
      mark('sm:u2', 'sig', null),
      undo('sm:u3', 'sm:u2'),
      FORMS_LIST,
      undo('sm:u4', 'sm:u3'),
      FORMS_LIST,
    ],
  },
  {
    name: 'a-mark-and-no-mark-are-ops-of-one-change',
    covers: 'A mark and no mark travel as ops of one change, which one undo takes back.',
    steps: [
      signatureField('sm:o0', 'a'),
      signatureField('sm:o1', 'b', { box: { ...BANDS_BOX, y: 120 } }),
      mark('sm:o2', 'b', 'png'),
      apply('sm:o3', {
        ops: [
          { type: 'forms.setSignatureAppearance', field: fqn('a'), appearance: BANDS_PNG.slice() },
          { type: 'forms.setSignatureAppearance', field: fqn('b'), appearance: null },
        ],
      }),
      FORMS_LIST,
      undo('sm:o4', 'sm:o3'),
      FORMS_LIST,
    ],
  },
  {
    name: 'no-mark-in-a-field-that-has-none-changes-nothing',
    covers: 'null on a field with no mark leaves its widget as it is.',
    steps: [signatureField('sm:n0', 'sig', { box: BANDS_BOX }, true), mark('sm:n1', 'sig', null), FORMS_LIST],
  },
  {
    name: 'a-field-with-two-boxes-is-filled-and-cleared-in-both',
    covers: 'Both widgets of one signature field get the mark, and both lose it.',
    steps: [
      create('sm:t0', {
        kind: 'signature',
        name: 'twice',
        widgets: [
          { page: toPageRef(3), box: BANDS_BOX },
          { page: toPageRef(3), box: { ...BANDS_BOX, y: 150 } },
        ],
      }),
      mark('sm:t1', 'twice', 'jpeg'),
      FORMS_LIST,
      mark('sm:t2', 'twice', null),
      FORMS_LIST,
    ],
  },
  {
    name: 'a-field-of-another-family-takes-no-mark',
    covers: 'A text field takes no mark and gives none up: InvalidArg both ways.',
    steps: [
      create('sm:x0', { kind: 'text', name: 'text', widgets: [{ page: toPageRef(3), box: BANDS_BOX }] }),
      mark('sm:x1', 'text', 'png'),
      mark('sm:x2', 'text', null),
    ],
  },
];

const MARK_FRAME = { x: 150, y: 20, width: 120, height: 40 };

const WIDGET_ROTATION_CASES: readonly Case[] = [
  {
    name: 'a-mark-in-a-turned-field-is-fitted-into-its-frame',
    covers: 'A mark drawn into a signature field turned 90 has a mark, and its rect is its box with the sides swapped.',
    steps: [
      create('wr:c0', { kind: 'signature', name: 'turned', widgets: [{ page: toPageRef(3), box: { ...MARK_FRAME, y: 120 }, rotation: 90 }] }),
      mark('wr:m0', 'turned', 'markPdfKeyed'),
      FORMS_LIST,
    ],
  },
  {
    name: 'a-turn-turns-the-mark-and-undo-puts-it-back',
    covers:
      "A turn of a marked widget fits the mark again (appearance changed, the mark kept); Acrobat's Orientation, a new frame, fits it as a new field's; two undos put it back upright.",
    steps: [
      create('wr:t0', { kind: 'signature', name: 'marked', widgets: [{ page: toPageRef(3), box: MARK_FRAME }] }),
      mark('wr:t1', 'marked', 'markPdfKeyed'),
      { call: 'forms.updateWidget', widgetOf: 'marked', patch: { rotation: 90 }, opId: 'wr:t2' },
      { call: 'forms.updateWidget', widgetOf: 'marked', patch: { rotation: null, box: '$rect' }, opId: 'wr:t3' },
      create('wr:t4', { kind: 'signature', name: 'fresh', widgets: [{ page: toPageRef(3), box: { x: 40, y: -20, width: 40, height: 120 } }] }),
      mark('wr:t5', 'fresh', 'markPdfKeyed'),
      undo('wr:t6', 'wr:t3'),
      undo('wr:t7', 'wr:t2'),
      { read: 'annotations.list', page: 3 },
      FORMS_LIST,
    ],
  },
];

const CATCHUP_GROUPS: readonly Group[] = [
  {
    file: 'field-purpose-format.json',
    kind: 'forms.purposeAndFormat',
    fixture: { name: 'FIELD_PURPOSE_FORMAT_PDF', bytes: FIELD_PURPOSE_FORMAT_PDF },
    cases: PURPOSE_FORMAT_CASES,
  },
  {
    file: 'field-groups.json',
    kind: 'forms.fieldGroups',
    fixture: { name: 'FIELD_GROUPS_PDF', bytes: FIELD_GROUPS_PDF },
    cases: FIELD_GROUP_CASES,
  },
  {
    file: 'signature-mark.json',
    kind: 'forms.signatureMark',
    fixture: { name: 'PAGE_PDF', bytes: MARK_PAGE_PDF },
    fixtures: MARKS,
    cases: SIGNATURE_MARK_CASES,
  },
  {
    file: 'widget-rotation.json',
    kind: 'forms.widgetRotation.mark',
    fixture: { name: 'TURNED_FIELDS_PDF', bytes: TURNED_FIELDS_PDF },
    fixtures: MARKS,
    cases: WIDGET_ROTATION_CASES,
  },
];

// --- Change kinds -----------------------------------------------------------

/** The sides `changeKindsUnknownTo` is asked about: none said, older, this one, newer. */
const VERSIONS = [null, '2.9', '3.0', '3.1', '3.2', '3.3', '3.4', '4.0'] as const;

function kindsVector(): unknown {
  const kinds = [...CHANGE_KINDS];
  const asked = [...kinds, 'pages.someKindFromANewerServer'];
  return {
    schemaVersion: SCHEMA_VERSION,
    kind: 'wire.changeKinds',
    apiVersion: API_VERSION,
    // The version the validation-data routes and a prepare with no field arrived in (4cb85c91e).
    validationInfoSince: VALIDATION_INFO_SINCE,
    // A signature mark as an image, or none, and a field's purpose and format (fcb69611f, 833b40015).
    markImageOrNoneSince: MARK_IMAGE_OR_NONE_SINCE,
    fieldPurposeAndFormatSince: FIELD_PURPOSE_AND_FORMAT_SINCE,
    since: Object.fromEntries(kinds.map((kind) => [kind, changeKindSince(kind as ChangeKind)])),
    rows: CHANGE_KIND_SINCE,
    asked,
    unknownTo: VERSIONS.map((version) => ({
      version,
      unknown: changeKindsUnknownTo(version, asked),
    })),
    lacks: VERSIONS.flatMap((version) =>
      [null, '3.0', '3.1', '3.2', '3.3', '3.4'].map((since) => ({
        version,
        since,
        lacks: lacksWhatArrivedIn(version, since),
      })),
    ),
  };
}

// --- Output -----------------------------------------------------------------

function serialize(group: Group, cases: readonly unknown[]): string {
  const fixture =
    typeof group.fixture === 'string'
      ? { fixture: group.fixture }
      : { fixture: group.fixture.name, fixtureHex: hex(group.fixture.bytes) };
  const fixtures = group.fixtures
    ? { fixtures: Object.fromEntries(Object.entries(group.fixtures).map(([k, v]) => [k, hex(v)])) }
    : {};
  const head = JSON.stringify(
    { schemaVersion: SCHEMA_VERSION, kind: group.kind, ...fixture, ...fixtures },
    null,
    2,
  ).replace(/\n}$/, ',');
  const body = cases.map((entry) => `  ${JSON.stringify(entry)}`).join(',\n');
  return `${head}\n  "cases": [\n${body}\n  ]\n}\n`;
}

async function emit(relative: string, text: string, check: boolean): Promise<boolean> {
  const absolute = join(vectorsRoot, relative);
  let current: string | undefined;
  try {
    current = await readFile(absolute, 'utf8');
  } catch {
    current = undefined;
  }
  if (current === text) {
    process.stdout.write(`  ok      changes/${relative}\n`);
    return false;
  }
  if (check) {
    process.stdout.write(`  DIFFERS changes/${relative}${current === undefined ? ' (missing)' : ''}\n`);
    return true;
  }
  await mkdir(dirname(absolute), { recursive: true });
  await writeFile(absolute, text);
  process.stdout.write(`  written changes/${relative}\n`);
  return false;
}

async function main(): Promise<void> {
  const check = process.argv.includes('--check');
  let differs = false;
  for (const group of [...GROUPS, ...CATCHUP_GROUPS]) {
    const names = new Set<string>();
    for (const entry of group.cases) {
      if (names.has(entry.name)) throw new Error(`${group.file}: duplicate case ${entry.name}`);
      names.add(entry.name);
    }
    const cases: unknown[] = [];
    for (const entry of group.cases) cases.push(await runCase(group, entry));
    differs = (await emit(group.file, serialize(group, cases), check)) || differs;
  }
  differs = (await emit('kinds.json', `${JSON.stringify(kindsVector(), null, 2)}\n`, check)) || differs;
  if (differs) {
    process.stderr.write(
      '\nChange vectors differ. Regenerate with:\n' +
        '  pnpm --filter @embedpdf/tooling-engine-conformance run changes\n' +
        'and commit the diff.\n',
    );
    process.exitCode = 1;
  }
}

main().catch((error: unknown) => {
  process.stderr.write(
    `generate-change-vectors: ${error instanceof Error ? (error.stack ?? error.message) : String(error)}\n`,
  );
  process.exitCode = 1;
});
