/**
 * The command lists each owner ships from its `/commands` entry (#1091,
 * `d6325f5ba`, `2e65f29e8`; undo and redo from #1089, `33a7a17d0`), run
 * through the commands plugin next to their owners, and without them.
 * Replayed by `crates/plugins/commands/tests/lists.rs`.
 *
 * The `command-lists` factory builds `commandsPlugin` with
 * `{ ...config, commands: lists.flatMap(make) }`, each of `config.lists`
 * naming one owner's list. The host's part of a list (the clipboard, a file
 * download) logs `copySelection`, `copyImage` and `saveFile`.
 */
import '../probe';
import './commands';
import './history';
import './interaction';
import './i18n';
import './render';
import './selection';

import type { AnyPlugin } from '@embedpdf/core';

import { createDocumentCommands } from '../../../../packages/core/main/src/commands';
import { createAnnotationCommands } from '../../../../packages/plugin/annotation/src/commands';
import {
  commandsPlugin,
  type CommandsConfig,
} from '../../../../packages/plugin/commands/src/index';
import { CommandsToken } from '../../../../packages/plugin/commands/src/host-contract';
import { createHistoryCommands } from '../../../../packages/plugin/history/src/commands';
import { createInteractionCommands } from '../../../../packages/plugin/interaction/src/commands';
import { createRenderCommands } from '../../../../packages/plugin/render/src/commands';
import { createSelectionCommands } from '../../../../packages/plugin/selection/src/commands';
import { createStageCommands } from '../../../../packages/plugin/stage/src/commands';
import { renderPlugin } from '../../../../packages/plugin/render/src/index';
import {
  RenderToken,
  type RenderHostCapability,
} from '../../../../packages/plugin/render/src/host-contract';
import type { RenderConfig } from '../../../../packages/plugin/render/src/contract';
import type { PageLayout } from '../../../../packages/engine/core/src/index';

import type { FakeDocumentSpec } from '../fake-engine';
import {
  call as annotation,
  deleted,
  doc as annotationDoc,
  hydrate,
  objRef,
  square,
  write,
} from './annotation-live-fixtures';
import { registerPlugin, type PluginEnv, type ScenarioCase, type Step } from '../scenario';
import type { Suite } from '../vectors';

type ListName =
  | 'document'
  | 'history'
  | 'interaction'
  | 'selection'
  | 'annotation'
  | 'render'
  | 'stage';

function lists(env: PluginEnv) {
  const platform = {
    copySelection: () => {
      env.log('copySelection', []);
      return Promise.resolve(undefined);
    },
    copyImage: () => {
      env.log('copyImage', []);
      return Promise.resolve(undefined);
    },
    saveFile: (_bytes: unknown, fileName: string, contentType: string) => {
      env.log('saveFile', [fileName, contentType]);
    },
  };
  return {
    document: () => createDocumentCommands(platform),
    history: () => createHistoryCommands(),
    interaction: () => createInteractionCommands(),
    selection: () => createSelectionCommands(platform),
    annotation: () => createAnnotationCommands(),
    render: () => createRenderCommands(platform),
    stage: () => createStageCommands(),
  } satisfies Record<ListName, () => readonly unknown[]>;
}

registerPlugin('command-lists', {
  factory: (raw, env) => {
    const { lists: names = [], ...config } = (raw ?? {}) as { lists?: ListName[] } & CommandsConfig;
    const made = lists(env);
    return commandsPlugin({
      ...config,
      commands: names.flatMap((name) => made[name]()) as CommandsConfig['commands'],
    });
  },
  tokens: [CommandsToken],
});

/**
 * The render plugin, with a picture that has `blob()`: the fake engine's
 * picture is JSON, and `snapshot:save` reads `(await snapshot.image).blob()`.
 * The blob is the picture itself; the Rust side's `image_bytes` answers at the
 * same point. Each snapshot gets one such picture, made when first read.
 */
const withBlob = new WeakMap<object, Promise<unknown>>();
registerPlugin('snapshot-render', {
  factory: (config) => {
    const plugin = renderPlugin((config ?? {}) as RenderConfig) as AnyPlugin;
    return {
      ...plugin,
      create: (ctx) => {
        const { api, connect } = plugin.create!(ctx);
        const render = api as RenderHostCapability;
        const getSnapshot = () => {
          const held = render.getSnapshot();
          if (!held) return held;
          return {
            ...held,
            get image() {
              let image = withBlob.get(held);
              if (!image) {
                image = held.image.then((picture) => ({ ...picture, blob: async () => picture }));
                withBlob.set(held, image);
              }
              return image;
            },
          };
        };
        return { api: { ...render, getSnapshot }, connect };
      },
    } as AnyPlugin;
  },
  tokens: [RenderToken],
});

const box = { x: 0, y: 0, width: 600, height: 800 };
const page = { kind: 'objectNumber', objectNumber: 1 } as const;
const pages: PageLayout[] = [
  {
    index: 0,
    ref: page,
    label: null,
    size: { width: 600, height: 800 },
    rotation: 0,
    userUnit: 1,
    tabOrder: null,
    pdfCropBox: { left: 0, bottom: 0, right: 600, top: 800 },
    boxes: { media: box, crop: box, bleed: box, trim: box, art: box },
  },
];
const doc = (over: Partial<FakeDocumentSpec> = {}): FakeDocumentSpec => ({ pages, ...over });

const start: Step = { op: 'call', target: 'kernel', method: 'start' };
const open: Step = {
  op: 'call',
  target: 'documents',
  method: 'open',
  args: [{ kind: 'bytes', id: 'a', bytes: { $bytes: '' } }],
};
const cmd = (method: string, ...args: unknown[]): Step => ({
  op: 'call',
  target: { token: 'commands' },
  method,
  args,
});
const resolve = (id: string): Step => cmd('resolveCommand', id);
const on = (token: string, method: string, ...args: unknown[]): Step => ({
  op: 'call',
  target: { token, doc: 'a' },
  method,
  args,
});
const commandLists = (names: ListName[], config: Record<string, unknown> = {}) => ({
  factory: 'command-lists',
  config: { ...config, lists: names },
});

/** The history actor's one change: the square 10 moves to 1, undone to 0. */
const result = {
  items: [],
  meta: { affectedPages: [], cacheDelta: null, opId: 'answered', undoable: true },
};
const applied = (label: string): Step => ({ op: 'settle', label, value: result });
const applies = (count: number) =>
  Array.from({ length: count }, (_, n) => ({ path: 'apply', times: 1, defer: `c${n}` }));

/** The i18n plugin's eight languages, in `EMBEDPDF_STRINGS` order. */
const codes = ['en', 'nl', 'de', 'fr', 'es', 'zh-CN', 'sv', 'ja'];

/** A document the snapshot tool can take a picture of: every page image deferred as `r<n>`. */
const snapshotDoc: FakeDocumentSpec = {
  pages,
  security: {
    allows: [
      'doc.render',
      'doc.render.thumbnail',
      'doc.annotate.read',
      'doc.forms.read',
      'doc.content.copy',
    ],
  },
  responses: Array.from({ length: 8 }, (_, n) => ({
    path: 'page.render.image',
    times: 1,
    defer: `r${n}`,
  })),
};
const NO_MODIFIERS = { shift: false, alt: false, ctrl: false, meta: false };
/** A pointer sample over page 1 at page point (x, y), at scale 1. */
const pointer = (phase: 'down' | 'up', x: number, y: number): Step =>
  on('interaction', 'dispatchPointer', {
    phase,
    viewport: { x, y },
    modifiers: NO_MODIFIERS,
    page: { ref: page, point: { x, y }, scale: 1, rotation: 0 },
  });

const cases: ScenarioCase[] = [
  {
    name: 'history-undo-and-redo',
    covers:
      'createHistoryCommands: history:undo (Mod+Z) and history:redo (Mod+Shift+Z, Mod+Y) follow canUndo and canRedo, and run undo and redo.',
    plugins: [{ factory: 'history' }, { factory: 'history-actor' }, commandLists(['history'])],
    engine: { documents: { a: doc({ responses: applies(4) }) } },
    steps: [
      start,
      open,
      resolve('history:undo'),
      resolve('history:redo'),
      on('history-actor', 'stage', 'move', 10, 1),
      applied('c0'),
      resolve('history:undo'),
      cmd('execute', 'history:undo'),
      applied('c1'),
      resolve('history:undo'),
      resolve('history:redo'),
      cmd('execute', 'history:redo'),
      applied('c2'),
      resolve('history:redo'),
      cmd('listShortcuts'),
    ],
  },
  {
    name: 'history-labels-in-every-language',
    covers:
      "The i18n plugin's own strings: commands.history.undo and commands.history.redo (#1089) in its eight languages, as t() reads them and as the commands' labels.",
    plugins: [
      // Languages of the app's own with no strings: the labels are the i18n plugin's.
      {
        factory: 'i18n',
        config: { locales: codes.map((code) => ({ code, name: code, translations: {} })) },
      },
      commandLists(['history']),
    ],
    engine: { documents: { a: doc() } },
    steps: [
      start,
      open,
      ...codes.flatMap((code): Step[] => [
        { op: 'call', target: { token: 'i18n' }, method: 'setLocale', args: [code] },
        { op: 'call', target: { token: 'i18n' }, method: 't', args: ['commands.history.undo'] },
        { op: 'call', target: { token: 'i18n' }, method: 't', args: ['commands.history.redo'] },
        resolve('history:undo'),
        resolve('history:redo'),
      ]),
    ],
  },
  {
    name: 'history-commands-without-the-plugin',
    covers:
      'createHistoryCommands: without historyPlugin() both stay disabled, and running one is refused.',
    plugins: [commandLists(['history'])],
    engine: { documents: { a: doc() } },
    steps: [
      start,
      open,
      resolve('history:undo'),
      resolve('history:redo'),
      cmd('execute', 'history:undo'),
    ],
  },
  {
    name: 'interaction-tool-commands',
    covers:
      'createInteractionCommands: tool:pointer, tool:pan and tool:zoom with their keys, tool:<id> for every other tool, active with the tool, visible when the document has it, enabled as canUseTool says.',
    plugins: [{ factory: 'interaction' }, commandLists(['interaction'])],
    engine: { documents: { a: doc() } },
    steps: [
      start,
      open,
      on('interaction', 'registerTool', {
        id: 'ink',
        cursor: 'crosshair',
        enables: { $set: [] },
        canUse: { $fn: 'ink.canUse', seq: [{ returns: false }, { returns: true }] },
      }),
      on('interaction', 'registerTool', { id: 'free', cursor: 'default', enables: { $set: [] } }),
      cmd('listCommands'),
      resolve('tool:pointer'),
      resolve('tool:zoom'),
      resolve('tool:ink'),
      cmd('execute', 'tool:pan'),
      resolve('tool:pan'),
      resolve('tool:pointer'),
      cmd('execute', 'tool:free'),
      resolve('tool:free'),
      on('interaction', 'canUseTool', 'ink'),
      on('interaction', 'canUseTool', 'free'),
      on('interaction', 'canUseTool', 'nothing'),
      cmd('listShortcuts'),
    ],
  },
  {
    name: 'interaction-tool-commands-without-the-plugin',
    covers:
      'createInteractionCommands: without the interaction plugin a tool command is enabled but neither visible nor active, and the family names nothing.',
    plugins: [commandLists(['interaction'])],
    engine: { documents: { a: doc() } },
    steps: [start, open, cmd('listCommands'), resolve('tool:pointer'), resolve('tool:ink')],
  },
  {
    name: 'document-commands',
    covers:
      'createDocumentCommands: document:download (Mod+S) and document:print (Mod+P), disabled with no document, and refused where the document does not allow them.',
    plugins: [commandLists(['document'])],
    engine: { documents: { a: doc({ security: { allows: [] } }) } },
    steps: [
      start,
      resolve('document:download'),
      resolve('document:print'),
      open,
      resolve('document:download'),
      resolve('document:print'),
      cmd('getRefusal', 'document:download'),
      cmd('getRefusal', 'document:print'),
    ],
  },
  {
    name: 'document-download',
    covers:
      'createDocumentCommands: document:download reads the file and hands it to the host as the document name ending in .pdf, of type application/pdf.',
    plugins: [commandLists(['document'])],
    engine: {
      documents: {
        a: doc({
          security: { allows: ['doc.download'] },
          responses: [{ path: 'download', value: { $bytes: 'JVBERi0=' } }],
        }),
      },
    },
    steps: [start, open, resolve('document:download'), cmd('execute', 'document:download')],
  },
  {
    name: 'selection-commands',
    covers:
      'createSelectionCommands: selection:copy (Mod+C) is disabled with nothing selected, and disabled without the selection plugin.',
    plugins: [{ factory: 'interaction' }, { factory: 'selection' }, commandLists(['selection'])],
    engine: { documents: { a: doc() } },
    steps: [start, open, resolve('selection:copy'), cmd('execute', 'selection:copy')],
  },
  {
    name: 'snapshot-commands',
    covers:
      'createRenderCommands with a snapshot held: snapshot:copy hands the picture to the host in the press itself, snapshot:save waits for the picture, then names the file after the document and the page number (document p1.png, image/png), and snapshot:clear lets the snapshot go, which disables all three.',
    plugins: [{ factory: 'interaction' }, { factory: 'snapshot-render' }, commandLists(['render'])],
    engine: { documents: { a: snapshotDoc } },
    steps: [
      start,
      open,
      cmd('execute', 'tool:snapshot'),
      pointer('down', 100, 120),
      pointer('up', 400, 500),
      resolve('snapshot:copy'),
      cmd('execute', 'snapshot:copy'),
      cmd('execute', 'snapshot:save'),
      { op: 'settle', label: 'r0', value: { width: 512, height: 512 } },
      resolve('snapshot:clear'),
      cmd('execute', 'snapshot:clear'),
      resolve('snapshot:copy'),
      resolve('snapshot:save'),
      resolve('snapshot:clear'),
    ],
  },
  {
    name: 'annotation-delete',
    covers:
      'createAnnotationCommands with the annotation plugin: annotation:delete is disabled with nothing selected, enabled once an annotation the user may delete is selected, and deletes the selection.',
    plugins: [
      { factory: 'interaction' },
      { factory: 'annotation-live' },
      commandLists(['annotation']),
    ],
    engine: {
      documents: {
        a: annotationDoc([hydrate([square(1)]), write(1, [{ item: deleted(objRef(1, 1)) }])]),
      },
    },
    steps: [
      start,
      open,
      resolve('annotation:delete'),
      annotation('selection.select', [[objRef(1, 1)]]),
      resolve('annotation:delete'),
      cmd('execute', 'annotation:delete'),
      annotation('selection.getSelected'),
      resolve('annotation:delete'),
    ],
  },
  {
    name: 'owners-absent',
    covers:
      'createStageCommands, createSelectionCommands, createAnnotationCommands and createRenderCommands without their plugins: every command disabled, tool:snapshot hidden but allowed, and the lists in order.',
    plugins: [commandLists(['stage', 'selection', 'annotation', 'render'])],
    engine: { documents: { a: doc() } },
    steps: [
      start,
      open,
      cmd('listCommandIds'),
      cmd('listCommands'),
      resolve('zoom:fit-width'),
      resolve('tool:snapshot'),
      cmd('execute', 'zoom:in'),
      cmd('execute', 'snapshot:save'),
      cmd('listShortcuts'),
    ],
  },
];

export const suites: Suite[] = [
  {
    kind: 'scenario',
    file: 'plugins/commands/lists.json',
    describes:
      "The owners' command lists (#1091, #1089): history, the tools, the documents, the selection, the snapshot, the Stage and the annotation plugin's, through the commands plugin.",
    cases,
  },
];
