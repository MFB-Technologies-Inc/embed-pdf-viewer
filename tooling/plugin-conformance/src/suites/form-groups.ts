/**
 * The form controls and form signers surface (upstream c4e744345..9af4d9ee7):
 * the document's groups and the design group (#1141), a radio button's name
 * and place (#1136), the control-attach reads (#1138), the cursor passing
 * over a control that takes none (#1146, #1149), and a field placed upright
 * in the design group (#1118, #1141). Every case drives the shipping plugin.
 */
import type { ChangeItem, DocumentEvent, FormField } from '@embedpdf/engine-core/runtime';
import type { ScenarioCase, Step } from '../scenario';
import type { ScenarioSuite } from '../vectors';
import {
  scenario,
  call,
  text,
  radio,
  signature,
  row,
  placed,
  ref,
  objRef,
  pr,
  snapshot,
  apply,
  fieldAnswer,
  mutationMeta,
  fnSpec,
  ix,
  anno,
  tool,
  sample,
  list,
  doc,
  boot,
  FORM_ONLY,
  refused,
} from './form-live-fixtures';
import { LOCAL } from './annotation-live-fixtures';
import { square } from './annotation-current-fixtures';
import { call as untypedCall } from './form-current-fixtures';

/** attachFocusTarget with a callback literal for its target: the runner's host callables. */
const lend = (args: readonly unknown[]): Step => untypedCall('attachFocusTarget', args);

const BOX = { x: 10, y: 20, width: 100, height: 24 };
const at = (y: number) => ({ ...BOX, y });
const SELLER = text(1, 'seller_name', 'Sam', { groupId: 'seller', alternateName: 'Name' });
const BUYER = text(2, 'buyer_name', '', { groupId: 'buyer', required: true });
const NOBODY = text(3, 'notes', 'n');
const GROUPED = snapshot(
  [SELLER, BUYER, NOBODY],
  [
    row(101, SELLER.ref, 1, at(20)),
    row(102, BUYER.ref, 1, at(60)),
    row(103, NOBODY.ref, 1, at(100)),
  ],
);
const SET_GROUP = (groups: string[]) => ({
  rawScope: [
    'doc.open',
    'doc.forms.read',
    'doc.forms.fill',
    'doc.forms.modify',
    ...groups.map((group) => `fields:set-group:group=${group}`),
  ],
});

/** One change of several `forms.update` items, each field's own fact. */
const updatesAnswer = (n: number, fields: FormField[]) => {
  const meta = mutationMeta(n, fields);
  const items = fields.map(
    (field) => ({ type: 'forms.update', field, widgets: [], meta }) as unknown as ChangeItem,
  );
  const events = fields.map(
    (field, index): DocumentEvent =>
      ({
        type: 'forms.updated',
        field,
        widgets: [],
        meta,
        origin: { ...LOCAL, transaction: { id: meta.opId, index, count: fields.length } },
      }) as unknown as DocumentEvent,
  );
  return { value: { items, meta }, events };
};

const PLAN = radio(
  4,
  'plan',
  [
    { ...placed(104), onState: 'monthly', exportValue: 'monthly', checked: true },
    { ...placed(105), onState: 'yearly', exportValue: 'yearly', checked: false },
    { ...placed(106), onState: '', exportValue: '', checked: false },
  ],
  { alternateName: 'Plan', groupId: 'buyer', value: 'monthly' },
);
const RADIO_FORM = snapshot(
  [PLAN],
  [
    row(106, PLAN.ref, 1, at(100), { fieldKind: 'radio' }),
    row(104, PLAN.ref, 1, at(20), { fieldKind: 'radio' }),
    row(105, PLAN.ref, 1, at(60), { fieldKind: 'radio' }),
  ],
);

const A = text(1, 'a', '');
const B = text(2, 'b', '');
const C = text(3, 'c', '');
const SIGN = signature(4, 'sign');
const TAB_FORM = snapshot(
  [A, SIGN, B, C],
  [
    row(101, A.ref, 1, at(20)),
    row(104, SIGN.ref, 1, at(40), { fieldKind: 'signature' }),
    row(102, B.ref, 1, at(60)),
    row(103, C.ref, 1, at(100)),
  ],
);
/** A target that records the call and answers `returns`. */
const target = (name: string, over: Record<string, unknown> = {}) => ({
  focus: fnSpec(`${name}.focus`),
  ...over,
});

const GROUP_RADIO = radio(5, 'size', [
  { ...placed(105), onState: 'S', exportValue: 'S', checked: false },
  { ...placed(107), onState: 'L', exportValue: 'L', checked: false },
]);
const SPLIT_FORM = snapshot(
  [A, GROUP_RADIO, B],
  [
    row(105, GROUP_RADIO.ref, 1, at(10), { fieldKind: 'radio' }),
    row(101, A.ref, 1, at(40)),
    row(107, GROUP_RADIO.ref, 1, at(70), { fieldKind: 'radio' }),
    row(102, B.ref, 1, at(100)),
  ],
);

/** Two widgets of one field, one of another, and a square that is no widget. */
const PAIR = text(6, 'pair', '', {}, [placed(101), placed(104)]);
const ONE = text(7, 'one', '', {}, [placed(102)]);
const PAIR_FORM = snapshot(
  [PAIR, ONE],
  [row(101, PAIR.ref, 1, at(20)), row(102, ONE.ref, 1, at(60)), row(104, PAIR.ref, 1, at(140))],
);
/** The case with `annotations` as the annotation plugin's first read. */
const withAnnotations = (base: ScenarioCase, annotations: readonly unknown[]): ScenarioCase => {
  const documents = base.engine!.documents as Record<
    string,
    { responses: { path: string; value?: unknown }[] }
  >;
  const a = documents.a!;
  a.responses = a.responses.map((r) =>
    r.path === 'annotations.list' ? { ...r, value: { annotations } } : r,
  );
  return base;
};

const STAGE = { factory: 'stage', config: { viewUnitsPerPoint: 1, scrollBehavior: 'instant' } };

const cases: ScenarioCase[] = [
  scenario(
    'groups-are-named-and-colored',
    'listGroups: the named groups in the app order, each id once, then the groups the fields are in, each in its color or one of eight; setGroups fires onGroupsChanged, the same list again changes nothing; a fill item says whose field it is and shows the group accent, none at tint 0.',
    [
      call('listGroups'),
      call('getSettings'),
      call('onGroupsChanged', [fnSpec('groupsChanged')]),
      call('setGroups', [
        [
          { id: 'buyer', label: 'Dana Smith', color: 'var(--signer-1)' },
          { id: 'witness' },
          { id: 'buyer', label: 'ignored' },
        ],
      ]),
      call('listGroups'),
      call('setGroups', [
        [{ id: 'buyer', label: 'Dana Smith', color: 'var(--signer-1)' }, { id: 'witness' }],
      ]),
      call('listWidgets', [pr(1)]),
      call('updateSettings', [{ fields: { tint: 0 } }]),
      call('listWidgets', [pr(1)]),
      call('setGroups', [[]]),
      call('listGroups'),
    ],
    GROUPED,
  ),
  scenario(
    'the-design-group-takes-a-group-the-user-may-set',
    'setDesignGroup names the group a tool places for, firing onDesignGroupChanged once per change; a group the user may not set is refused with permission-denied; canSetGroup asks the engine.',
    [
      call('getDesignGroup'),
      call('onDesignGroupChanged', [fnSpec('designGroupChanged')]),
      call('canSetGroup', ['buyer']),
      call('canSetGroup', ['seller']),
      call('setDesignGroup', ['seller']),
      call('setDesignGroup', ['buyer']),
      call('setDesignGroup', ['buyer']),
      call('getDesignGroup'),
      call('setDesignGroup', [null]),
      call('getDesignGroup'),
    ],
    GROUPED,
    [],
    { security: SET_GROUP(['buyer']) },
  ),
  scenario(
    'fields-move-to-a-group-in-one-step',
    'moveToGroup with null takes fields out of their groups in one change of the fields not already there, shown at once; the groups nobody is in leave the list; nothing to move sends nothing; a field the form lacks is not-found.',
    [
      call('moveToGroup', [[ref(1), ref(2), ref(3), ref(1)], null]),
      call('list'),
      call('listGroups'),
      call('moveToGroup', [[ref(3)], null]),
      call('moveToGroup', [[], 'buyer']),
      call('moveToGroup', [[ref(3), ref(9)], 'buyer']),
    ],
    GROUPED,
    [
      apply(
        updatesAnswer(1, [
          { ...SELLER, groupId: null },
          { ...BUYER, groupId: null },
        ]),
      ),
    ],
    { security: { allows: '*', allowsField: { 'set-group': true } } },
  ),
  scenario(
    'a-group-the-user-may-not-set-is-refused',
    'create, update and moveToGroup refuse a group the user may not set, naming fields:set-group; a field keeps its own group without the right; the settings panel offers the groups the user may set and the field own.',
    [
      call('create', [{ kind: 'text', name: 'x', groupId: 'seller' }]),
      call('update', [ref(2), { groupId: 'seller' }]),
      call('moveToGroup', [[ref(3)], 'seller']),
      call('getFieldSettings', [ref(1)]),
      call('setGroups', [[{ id: 'buyer', label: 'Buyer' }, { id: 'witness' }]]),
      call('getFieldSettings', [ref(1)]),
      call('getFieldSettings', [ref(3)]),
    ],
    GROUPED,
    [],
    { security: SET_GROUP(['buyer']) },
  ),
  scenario(
    'a-radio-button-names-its-option-and-its-place',
    'A radio button label is the group tooltip, then its option, then whose it is; inGroup is its place among the group buttons in Tab order.',
    [
      call('listWidgets', [pr(1)]),
      call('setGroups', [[{ id: 'buyer', label: 'Dana Smith' }]]),
      call('listWidgets', [pr(1)]),
    ],
    RADIO_FORM,
  ),
  scenario(
    'a-box-anchors-ui-and-says-whether-it-has-the-cursor',
    'getWidgetAnchor gives the box page and rect, null for a box the form lacks or that shows nowhere; isWidgetFocused follows the controls focus.',
    [
      call('getWidgetAnchor', [objRef(101)]),
      call('getWidgetAnchor', [objRef(103)]),
      call('getWidgetAnchor', [objRef(404)]),
      call('isWidgetFocused', [objRef(101)]),
      call('notifyWidgetEvent', [ref(1), objRef(101), 'focus']),
      call('isWidgetFocused', [objRef(101)]),
      call('isWidgetFocused', [objRef(102)]),
      call('listSelectedFields'),
    ],
    snapshot(
      [SELLER, BUYER, NOBODY],
      [
        row(101, SELLER.ref, 1, at(20)),
        row(102, BUYER.ref, 1, at(60)),
        row(103, NOBODY.ref, 1, at(100), { hidden: true }),
      ],
    ),
  ),
  scenario(
    'tab-passes-over-a-box-that-takes-no-cursor',
    'A target saying it takes no cursor (a signature field with nothing to sign it) is passed over by Tab and focus; one whose focus answers false is passed over too; focus of a field with no stop that takes it is false.',
    [
      lend([objRef(101), target('a')]),
      lend([
        objRef(104),
        target('sign', { takesCursor: { $fn: 'sign.takesCursor', returns: false } }),
      ]),
      lend([objRef(102), target('b', { focus: { $fn: 'b.focus', returns: false } })]),
      lend([objRef(103), target('c')]),
      call('focusNext'),
      call('notifyWidgetEvent', [ref(1), objRef(101), 'focus']),
      call('focusNext'),
      call('focusNext', [{ backward: true, from: ref(3) }]),
      call('focus', [ref(4)]),
      call('focus', [ref(2)]),
      call('focus', [ref(3), { select: true }]),
    ],
    TAB_FORM,
  ),
  scenario(
    'a-target-whose-takes-cursor-says-nothing-takes-it',
    'takesCursor answering nothing (undefined or null) counts as taking the cursor, as a target without takesCursor does: Tab and focus stop at it.',
    [
      lend([objRef(101), target('a', { takesCursor: { $fn: 'a.takesCursor' } })]),
      lend([
        objRef(104),
        target('sign', { takesCursor: { $fn: 'sign.takesCursor', returns: null } }),
      ]),
      lend([objRef(102), target('b')]),
      call('focusNext'),
      call('notifyWidgetEvent', [ref(1), objRef(101), 'focus']),
      call('focusNext'),
      call('focus', [ref(4)]),
    ],
    TAB_FORM,
  ),
  {
    name: 'a-box-that-comes-in-holding-nothing-sends-the-cursor-on',
    covers:
      'With the Stage, a stop not on screen is asked for; when it comes in and its focus takes nothing, it gets a turn, then the cursor goes on to the next stop; a box that comes in and takes the cursor ends the wait.',
    plugins: [...FORM_ONLY.slice(0, 2), STAGE, FORM_ONLY[2]!],
    engine: { documents: { a: doc([list(TAB_FORM)]) } },
    snapshot: ['form::a'],
    steps: [
      ...boot,
      call('focusNext'),
      lend([objRef(101), target('a', { focus: { $fn: 'a.focus', returns: false } })]),
      { op: 'advance', ms: 0 },
      lend([objRef(104), target('sign')]),
      { op: 'advance', ms: 0 },
      call('focus', [ref(3)]),
      lend([objRef(103), target('c')]),
      { op: 'advance', ms: 2000 },
    ] as Step[],
  },
  scenario(
    'a-radio-group-is-one-stop-where-its-first-button-comes',
    'From any button of a radio group, Tab goes on from the group stop: what comes after the group, each field once, and Shift+Tab what comes before it.',
    [
      lend([objRef(101), target('a')]),
      lend([objRef(102), target('b')]),
      lend([objRef(105), target('small')]),
      lend([objRef(107), target('large')]),
      call('notifyWidgetEvent', [ref(5), objRef(107), 'focus']),
      call('focusNext'),
      call('notifyWidgetEvent', [ref(5), objRef(107), 'focus']),
      call('focusNext', [{ backward: true }]),
      call('notifyWidgetEvent', [ref(1), objRef(101), 'focus']),
      call('focusNext'),
    ],
    SPLIT_FORM,
  ),
  withAnnotations(
    scenario(
      'selected-widgets-name-their-fields-once',
      "listSelectedFields gives each field with a selected widget once, in the annotation selection's order, and passes over a selected annotation that is no widget.",
      [
        tool('form-edit'),
        anno('selection.select', [[objRef(102), objRef(200), objRef(101), objRef(104)]]),
        anno('selection.getSelected'),
        call('listSelectedFields'),
      ],
      PAIR_FORM,
      [],
      {},
      true,
    ),
    [square(200)],
  ),
  scenario(
    'lent-widgets-carry-their-group-accent-and-orientation',
    "In form design each lent widget's item carries its field group's accent, fields.tint strong (d6d7c25e2, #1141), the same while nothing changes and anew when the groups change; a selection of widgets offers Orientation, their rotation as its value (23741e3fd, #1118).",
    [
      tool('form-edit'),
      anno('listPageItems', [pr(1)]),
      anno('listPageItems', [pr(1)]),
      call('setGroups', [[{ id: 'buyer', label: 'Dana Smith', color: '#16a34a' }]]),
      anno('listPageItems', [pr(1)]),
      anno('selection.select', [[objRef(101), objRef(102)]]),
      anno('selection.getProperties'),
    ],
    GROUPED,
    [],
    {},
    true,
  ),
  scenario(
    'a-drag-on-a-turned-page-previews-upright',
    "A palette tool's drag on a page shown turned previews the field as its release places it (previewPlacement with the press's display rotation, #1118): the preview item is the turned frame.",
    [
      tool('form-text'),
      ix('dispatchPointer', [
        sample('down', 100, 100, {
          page: { ref: pr(1), point: { x: 100, y: 100 }, scale: 1, rotation: 90 },
        }),
      ]),
      ix('dispatchPointer', [
        sample('move', 160, 140, {
          page: { ref: pr(1), point: { x: 160, y: 140 }, scale: 1, rotation: 90 },
        }),
      ]),
      anno('listPageItems', [pr(1)]),
      ix('dispatchPointer', [
        sample('up', 160, 140, {
          page: { ref: pr(1), point: { x: 160, y: 140 }, scale: 1, rotation: 90 },
        }),
      ]),
    ],
    snapshot([]),
    [refused('intended refusal of the placed field')],
    { security: { allows: '*', allowsField: { 'set-group': true } } },
    true,
  ),
  scenario(
    'a-tool-places-upright-in-the-design-group',
    'On a page shown turned, a palette tool places the field as the author sees it, its box and a quarter turn, in the design group.',
    [
      call('setDesignGroup', ['buyer']),
      tool('form-text'),
      ix('dispatchPointer', [
        sample('down', 100, 100, {
          page: { ref: pr(1), point: { x: 100, y: 100 }, scale: 1, rotation: 90 },
        }),
      ]),
      ix('dispatchPointer', [
        sample('up', 100, 100, {
          page: { ref: pr(1), point: { x: 100, y: 100 }, scale: 1, rotation: 90 },
        }),
      ]),
      anno('selection.getSelected'),
    ],
    snapshot([]),
    [
      apply(
        fieldAnswer(
          'forms.create',
          1,
          text(10001, 'text_1', '', { groupId: 'buyer' }, [placed(10002)]),
          [
            row(
              10002,
              ref(10001),
              1,
              { x: 88, y: 20, width: 24, height: 160 },
              {
                box: { x: 20, y: 88, width: 160, height: 24 },
                rotation: 270,
              },
            ),
          ],
        ),
      ),
    ],
    { security: { allows: '*', allowsField: { 'set-group': true } } },
    true,
  ),
];

export const suites: ScenarioSuite[] = [
  {
    kind: 'scenario',
    file: 'plugins/form-groups.json',
    describes:
      'Current form groups, design group, radio names and places, control anchors, and the cursor over controls that take none.',
    cases,
  },
];
