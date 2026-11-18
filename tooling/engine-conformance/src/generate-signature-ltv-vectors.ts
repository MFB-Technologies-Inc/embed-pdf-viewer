/**
 * Emits `vectors/signature/{validation-info,timestamp-field,long-term}.json`:
 * the engine behaviour of upstream's long-term validation (#1088) and the
 * signing fixes of #1102, as the public API answers it.
 *
 *   pnpm --filter @embedpdf/tooling-engine-conformance run signature-ltv
 *   pnpm --filter @embedpdf/tooling-engine-conformance run signature-ltv:check
 *
 * `crates/epdf-engine/tests/signature_ltv.rs` replays it.
 *
 * | File | Upstream |
 * |---|---|
 * | `validation-info.json` | `runValidationInfoTests` (`runSignatureConformance.ts`, 67ed292a6) |
 * | `timestamp-field.json` | the timestamp cases of `runSignatureConformance.ts` and `runFieldGroupsConformance.ts` (b1293b16b, ba5f54d5f) |
 * | `long-term.json` | `signature-validation-info.test.ts`, and every signature of `fixtures/signature-long-term` analyzed under policy 5 (462380c7a, 92db1287a) |
 *
 * # What is recorded
 *
 * A case opens one fixture with a scope and runs steps against it. A step
 * is one call of the public API, and what it records is a view of the
 * answer: the fields the upstream tests assert, no more, so that what the
 * two engines may word or number differently (a signing id, a digest, a
 * candidate's bytes) is never compared. The Rust runner sends the wire
 * request the call stands for and takes the same view of its answer. A
 * refusal records its code, and its `details.field` when it has one.
 *
 * Each step also records the events the call published, of the two kinds
 * this range added or changed: `signatures.validationInfoAdded` (its
 * `added` and `meta.opId`) and `signatures.prepared` (its `field`).
 *
 * A validation-data entry is recorded as its bytes in hex where the case
 * gives it, and an entry the engine read back as its SHA-256. An event's
 * `opId` is recorded only where the step named one. The field-groups
 * contract (`FIELD_GROUPS_PDF`) is built in memory, so its cases carry it
 * as hex.
 */
import { createHash } from 'node:crypto';
import { mkdir, readdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

import { createLocalEngine, type LocalEngine } from '../../../packages/engine/main/src/index';
import { FIELD_GROUPS_PDF } from '../../../packages/engine/core/src/conformance/runFieldGroupsConformance';

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = resolve(packageRoot, '../..');
const vectorsRoot = join(packageRoot, 'vectors', 'signature');

/** Bump when the envelope changes shape. The Rust runner asserts it. */
const SCHEMA_VERSION = 1;

const FIX = 'packages/engine/main/test/fixtures';
const UNSIGNED = `${FIX}/unsigned_sigfield.pdf`;
const CERTIFIED = `${FIX}/signed_certified.pdf`;
const NO_CHANGES = `${FIX}/signature-long-term/ours-certified-p1-b-t.pdf`;
const ARTWORK = `${FIX}/signature_artwork.pdf`;
const LONG_TERM = `${FIX}/signature-long-term`;

const hex = (bytes: Uint8Array) => Buffer.from(bytes).toString('hex');
const unhex = (text: string) => new Uint8Array(Buffer.from(text, 'hex'));
const sha256 = (bytes: Uint8Array) => createHash('sha256').update(bytes).digest('hex');

/** `FAKE_CMS` and `PLACEHOLDER_CMS` of the conformance suites: one DER SEQUENCE. */
const FAKE_CMS = '3006020101020102';

/** `derEntry`: one DER object, a sequence around `label` as an octet string. */
function derEntry(label: string): string {
  const text = new TextEncoder().encode(label);
  return hex(Uint8Array.from([0x30, text.length + 2, 0x04, text.length, ...text]));
}
const CERT_A = derEntry('certificate a');
const CERT_B = derEntry('certificate b');
const ANSWER = derEntry('ocsp response');
const LIST = derEntry('crl one');
const LIST_2 = derEntry('crl two');
/** One DER object that no long-term file holds (`NEW_LIST`). */
const NEW_LIST = '300504036e6577';


type Entries = Partial<Record<'certificates' | 'ocspResponses' | 'crls', string[]>>;

interface Open {
  readonly scope?: readonly string[];
  readonly identity?: Record<string, string>;
  readonly signedDocumentPolicy?: 'protect' | 'permit';
}

type Step =
  | { readonly call: 'getValidationInfo' }
  /** `input` as hex entries; `'lastRead'` re-adds what the last read answered; `'notBytes'` sends strings. */
  | {
      readonly call: 'addValidationInfo';
      readonly input: Entries | 'lastRead' | 'notBytes';
      readonly opId?: string;
    }
  | { readonly call: 'prepare'; readonly input: Record<string, unknown>; readonly artwork?: true }
  | { readonly call: 'complete' }
  | { readonly call: 'cancel' }
  | { readonly call: 'listSignatures' }
  | { readonly call: 'listFields'; readonly widgetOf?: string }
  | { readonly call: 'analyze'; readonly input: Record<string, unknown> }
  | { readonly call: 'createTextField'; readonly name: string }
  | { readonly call: 'apply'; readonly change: Record<string, unknown>; readonly opId: string }
  | { readonly call: 'reopen' };

interface Case {
  readonly name: string;
  readonly covers: string;
  readonly fixture: string;
  readonly open?: Open;
  readonly steps: readonly Step[];
}

interface Group {
  readonly file: string;
  readonly kind: string;
  readonly cases: readonly Case[];
}

const EVERYTHING: Open = { scope: ['*'] };
const sigRef = { kind: 'fqn', name: 'sig' } as const;
const NOTES = { type: 'outline.create', parent: null, data: { title: 'Notes' } } as const;

// --- Cases ------------------------------------------------------------------

const VALIDATION_INFO: Group = {
  file: 'validation-info.json',
  kind: 'signatures.validationInfo',
  cases: [
    {
      name: 'a-document-without-any-reads-three-empty-lists',
      covers: 'runValidationInfoTests: a document without a store reads three empty lists.',
      fixture: UNSIGNED,
      open: EVERYTHING,
      steps: [{ call: 'getValidationInfo' }],
    },
    {
      name: 'entries-read-back-as-given-are-saved-and-stored-once',
      covers:
        'Entries read back as given; an unsaved write adds no revision; what the document holds and an entry named twice are stored once; a write that stores nothing publishes nothing; the saved file holds them, and a store it already has takes new entries beside its own.',
      fixture: UNSIGNED,
      open: EVERYTHING,
      steps: [
        { call: 'listSignatures' },
        {
          call: 'addValidationInfo',
          input: { certificates: [CERT_A, CERT_B], ocspResponses: [ANSWER], crls: [LIST] },
          opId: 'validation-info-first',
        },
        { call: 'getValidationInfo' },
        { call: 'listSignatures' },
        { call: 'addValidationInfo', input: { certificates: [CERT_A], crls: [LIST, LIST_2, LIST_2] } },
        { call: 'addValidationInfo', input: { certificates: [CERT_B], ocspResponses: [ANSWER] } },
        { call: 'addValidationInfo', input: {} },
        { call: 'reopen' },
        { call: 'getValidationInfo' },
        { call: 'addValidationInfo', input: { crls: [LIST, CERT_A] } },
        { call: 'getValidationInfo' },
      ],
    },
    {
      name: 'a-malformed-entry-stores-nothing-and-says-which-entry-it-is',
      covers:
        'A PEM header, a truncated entry, two objects in one entry and an empty one are each refused InvalidArg naming the entry, and the good entries beside them are not stored; a list that is not of byte arrays is InvalidArg too. Nothing is stored and nothing published.',
      fixture: UNSIGNED,
      open: EVERYTHING,
      steps: [
        {
          call: 'addValidationInfo',
          input: { certificates: [CERT_A, hex(new TextEncoder().encode('-----BEGIN CERTIFICATE-----'))], crls: [LIST] },
        },
        { call: 'addValidationInfo', input: { certificates: [CERT_A], crls: [LIST.slice(0, -2)] } },
        { call: 'addValidationInfo', input: { ocspResponses: [ANSWER + ANSWER] } },
        { call: 'addValidationInfo', input: { ocspResponses: [''] } },
        { call: 'addValidationInfo', input: 'notBytes' },
        { call: 'getValidationInfo' },
      ],
    },
    {
      name: 'no-undo-takes-it-back-and-the-undo-of-an-earlier-change-leaves-it',
      covers:
        'After a change that writes the catalog (an outline item here, an attachment upstream), the store is written and is not undoable: its undo is UndoUnavailable. The undo of the earlier change still runs and leaves the store.',
      fixture: UNSIGNED,
      open: EVERYTHING,
      steps: [
        { call: 'apply', change: { ops: [NOTES] }, opId: 'notes' },
        { call: 'addValidationInfo', input: { certificates: [CERT_A] }, opId: 'stored' },
        { call: 'apply', change: { undoOf: 'stored' }, opId: 'undo-stored' },
        { call: 'apply', change: { undoOf: 'notes' }, opId: 'undo-notes' },
        { call: 'getValidationInfo' },
      ],
    },
    {
      name: 'a-signature-made-after-it-seals-it-in-added-after-one-it-is-permitted',
      covers:
        'Stored before a signature, the entry is in the signed revision; a parked candidate refuses a store with SigningPending; stored after an approval it is permitted (rule dss), and the saved file has three revisions, the signature unchanged.',
      fixture: UNSIGNED,
      open: EVERYTHING,
      steps: [
        { call: 'addValidationInfo', input: { certificates: [CERT_A] } },
        { call: 'prepare', input: { field: sigRef } },
        { call: 'addValidationInfo', input: { certificates: [CERT_B] } },
        { call: 'complete' },
        { call: 'getValidationInfo' },
        { call: 'addValidationInfo', input: { ocspResponses: [ANSWER], crls: [LIST] } },
        {
          call: 'analyze',
          input: { since: { signatureIndex: 0 }, until: 'working-copy', detail: 'full' },
        },
        { call: 'reopen' },
        { call: 'listSignatures' },
        { call: 'analyze', input: { since: { signatureIndex: 0 } } },
        { call: 'getValidationInfo' },
      ],
    },
    {
      name: 'a-group-signer-may-not-store-it-and-may-read-it',
      covers: 'Signing the fields of a group is not the right to add validation data: Forbidden. Reading takes doc.forms.read.',
      fixture: UNSIGNED,
      open: { scope: ['doc.open', 'doc.forms.read', 'fields:sign:group=buyer'], identity: {} },
      steps: [
        { call: 'addValidationInfo', input: { certificates: [CERT_A] } },
        { call: 'getValidationInfo' },
      ],
    },
    {
      name: 'reading-needs-doc-forms-read-and-storing-needs-doc-sign',
      covers: 'Without doc.forms.read the read is Forbidden; doc.sign stores.',
      fixture: UNSIGNED,
      open: { scope: ['doc.open', 'doc.sign'], identity: {} },
      steps: [
        { call: 'getValidationInfo' },
        { call: 'addValidationInfo', input: { certificates: [CERT_A] } },
      ],
    },
    {
      name: 'a-certification-that-allows-form-filling-lets-it-through',
      covers: 'After a P=2 certification the store is written and the certification is still permitted.',
      fixture: CERTIFIED,
      open: EVERYTHING,
      steps: [
        { call: 'listSignatures' },
        { call: 'addValidationInfo', input: { certificates: [CERT_A] } },
        { call: 'analyze', input: { since: { signatureIndex: 0 }, until: 'working-copy' } },
      ],
    },
    {
      name: 'a-certification-that-allows-no-changes-refuses-it',
      covers: 'After a P=1 certification (enforced lta) a store is ProtectedDocument; reading stays possible.',
      fixture: NO_CHANGES,
      open: EVERYTHING,
      steps: [
        { call: 'listSignatures' },
        { call: 'addValidationInfo', input: { certificates: [CERT_A] } },
        { call: 'getValidationInfo' },
      ],
    },
    {
      name: 'with-protection-off-it-is-written-and-judged-forbidden',
      covers:
        'Under signedDocumentPolicy permit the engine writes it, and the judgement is what it was: Acrobat calls this certification changed (no-changes-certification).',
      fixture: NO_CHANGES,
      open: { scope: ['*'], signedDocumentPolicy: 'permit' },
      steps: [
        { call: 'addValidationInfo', input: { certificates: [CERT_A] } },
        { call: 'analyze', input: { since: { signatureIndex: 0 }, until: 'working-copy' } },
      ],
    },
  ],
};

const TIMESTAMP_FIELD: Group = {
  file: 'timestamp-field.json',
  kind: 'signatures.prepare.timestamp',
  cases: [
    {
      name: 'a-document-timestamp-needs-no-field-the-engine-makes-one',
      covers:
        'prepare({kind:"timestamp"}) answers Timestamp1; the live document gets the field only when the signing completes, and none when it is cancelled. The completed timestamp covers its whole revision, the field has one widget of no size on the first page, print and locked; the next gets Timestamp2 and the first stays permitted, its window explained by signature-field-added. Every prepared notice names the field. Reopened, the file has three revisions.',
      fixture: UNSIGNED,
      open: EVERYTHING,
      steps: [
        { call: 'listFields' },
        { call: 'prepare', input: { kind: 'timestamp' } },
        { call: 'listFields' },
        { call: 'cancel' },
        { call: 'listFields' },
        { call: 'prepare', input: { kind: 'timestamp' } },
        { call: 'complete' },
        { call: 'listSignatures' },
        { call: 'listFields', widgetOf: 'Timestamp1' },
        { call: 'prepare', input: { kind: 'timestamp' } },
        { call: 'complete' },
        { call: 'analyze', input: { since: { signatureIndex: 1 }, detail: 'full' } },
        { call: 'reopen' },
        { call: 'listSignatures' },
      ],
    },
    {
      name: 'a-timestamp-field-takes-a-name-no-other-field-has',
      covers: 'With a text field named Timestamp1 the timestamp field is Timestamp2, and the text field stays a text field.',
      fixture: UNSIGNED,
      open: EVERYTHING,
      steps: [
        { call: 'createTextField', name: 'Timestamp1' },
        { call: 'prepare', input: { kind: 'timestamp' } },
        { call: 'complete' },
        { call: 'listFields' },
      ],
    },
    {
      name: 'only-a-document-timestamp-may-leave-the-field-out',
      covers:
        'A signature with no field is InvalidArg naming field; a timestamp with an appearance is SignatureRefused; nothing was signed.',
      fixture: UNSIGNED,
      open: EVERYTHING,
      steps: [
        { call: 'prepare', input: {} },
        { call: 'prepare', input: { kind: 'timestamp' }, artwork: true },
        { call: 'listSignatures' },
      ],
    },
    {
      name: 'a-group-signer-may-not-add-a-timestamp',
      covers: "The right to sign a group's fields is not the right to add a field to the document: Forbidden.",
      fixture: UNSIGNED,
      open: { scope: ['doc.open', 'doc.forms.read', 'fields:sign:group=buyer'], identity: {} },
      steps: [{ call: 'prepare', input: { kind: 'timestamp' } }],
    },
    {
      name: 'a-certification-that-allows-no-changes-refuses-a-timestamp',
      covers: 'After a P=1 certification, a timestamp is ProtectedDocument.',
      fixture: NO_CHANGES,
      open: EVERYTHING,
      steps: [{ call: 'prepare', input: { kind: 'timestamp' } }],
    },
    {
      name: 'a-buyer-without-doc-sign-is-refused-a-timestamp',
      covers: 'runFieldGroupsConformance: the buyer token, which signs the buyer group, is Forbidden a timestamp.',
      fixture: 'fieldGroups',
      open: {
        scope: ['doc.open', 'doc.render', 'fields:fill:group=buyer', 'fields:sign:group=buyer'],
        identity: { userId: 'alice', displayName: 'Alice' },
      },
      steps: [{ call: 'prepare', input: { kind: 'timestamp' } }],
    },
    {
      name: 'a-buyer-with-doc-sign-adds-a-timestamp-and-signs-no-more-fields',
      covers:
        "With doc.sign besides, the buyer adds a timestamp (ETSI.RFC3161, kind timestamp); doc.sign widens nothing else: the seller's field is still Forbidden.",
      fixture: 'fieldGroups',
      open: {
        scope: ['doc.open', 'doc.render', 'fields:fill:group=buyer', 'fields:sign:group=buyer', 'doc.sign'],
        identity: { userId: 'alice', displayName: 'Alice' },
      },
      steps: [
        { call: 'prepare', input: { kind: 'timestamp' } },
        { call: 'complete' },
        { call: 'prepare', input: { field: { kind: 'fqn', name: 'seller_sig' } } },
      ],
    },
  ],
};

/** Every file of `fixtures/signature-long-term`, read and judged. */
async function longTerm(): Promise<Group> {
  const files = (await readdir(join(repoRoot, LONG_TERM))).filter((f) => f.endsWith('.pdf')).sort();
  const cases: Case[] = [];
  for (const file of files) {
    const fixture = `${LONG_TERM}/${file}`;
    const signatures = await countSigned(fixture);
    cases.push({
      name: file.replace(/\.pdf$/, ''),
      covers:
        'The store reads as an independent reader counts it, each entry once; what it holds is not stored again; every signed signature is judged under policy 5, net state and every step.',
      fixture,
      open: EVERYTHING,
      steps: [
        { call: 'listSignatures' },
        { call: 'getValidationInfo' },
        { call: 'addValidationInfo', input: 'lastRead' },
        ...Array.from({ length: signatures }, (_, index) => ({
          call: 'analyze' as const,
          input: { since: { signatureIndex: index }, detail: 'full' },
        })),
      ],
    });
  }
  for (const file of ['acrobat-lt.pdf', 'ours-b-t+lt-no-vri.pdf']) {
    cases.push({
      name: `an-entry-joins-${file.replace(/\.pdf$/, '')}`,
      covers:
        'An entry joins a list that is an object of its own (Acrobat) or one inside the store (pyHanko); the saved file keeps the loaded bytes as its beginning, reads the entry last, and the signature stays permitted.',
      fixture: `${LONG_TERM}/${file}`,
      open: EVERYTHING,
      steps: [
        { call: 'addValidationInfo', input: { crls: [NEW_LIST] } },
        { call: 'reopen' },
        { call: 'getValidationInfo' },
        { call: 'analyze', input: { since: { signatureIndex: 0 } } },
      ],
    });
  }
  return { file: 'long-term.json', kind: 'signatures.longTerm', cases };
}

async function countSigned(fixture: string): Promise<number> {
  const engine = createLocalEngine({ runtime: { prefer: 'wasm' } });
  try {
    const doc = await engine.open({ kind: 'bytes', id: 'count', bytes: await bytesOf(fixture) }, EVERYTHING);
    try {
      return (await doc.signatures.list()).signatures.filter((s) => s.signed).length;
    } finally {
      await doc.close();
    }
  } finally {
    await engine.destroy();
  }
}

// --- Running a case ---------------------------------------------------------

async function bytesOf(fixture: string): Promise<Uint8Array> {
  if (fixture === 'fieldGroups') return FIELD_GROUPS_PDF.slice();
  return new Uint8Array(await readFile(join(repoRoot, fixture)));
}

type Doc = Awaited<ReturnType<LocalEngine['open']>>;

const KINDS = ['certificates', 'ocspResponses', 'crls'] as const;

/** The view of a refusal: its code, and the field its details name. */
function refusal(error: unknown): unknown {
  const code = (error as { code?: unknown })?.code;
  if (code === undefined) throw error;
  const field = (error as { details?: { field?: unknown } }).details?.field;
  return { reject: String(code), ...(field === undefined ? {} : { field }) };
}

async function runCase(entry: Case): Promise<unknown> {
  const loaded = await bytesOf(entry.fixture);
  const open = entry.open ?? EVERYTHING;
  const engine = createLocalEngine({
    runtime: { prefer: 'wasm' },
    ...(open.signedDocumentPolicy ? { signedDocumentPolicy: open.signedDocumentPolicy } : {}),
  });
  const docs: Doc[] = [];
  let events: unknown[] = [];
  /** The opId the running step named: an event's is recorded only then, a minted one being the engine's own. */
  let named: string | undefined;
  const watch = (doc: Doc) =>
    doc.events.subscribe((event) => {
      if (event.type === 'signatures.validationInfoAdded') {
        events.push({
          type: event.type,
          added: event.added,
          ...(named === undefined ? {} : { opId: event.meta.opId }),
        });
      } else if (event.type === 'signatures.prepared') {
        events.push({ type: event.type, field: event.field });
      }
    });
  const openDoc = async (bytes: Uint8Array) => {
    const doc = await engine.open(
      { kind: 'bytes', id: `ltv:${entry.name}:${docs.length}`, bytes },
      { scope: [...(open.scope ?? ['*'])], ...(open.identity ? { identity: open.identity } : {}) },
    );
    docs.push(doc);
    watch(doc);
    return doc;
  };
  try {
    let doc = await openDoc(loaded);
    let lastRead: Record<string, Uint8Array[]> | null = null;
    let parked: { signingId: string; expectedVersion: unknown } | null = null;
    const steps: unknown[] = [];
    for (const step of entry.steps) {
      events = [];
      named = 'opId' in step ? step.opId : undefined;
      let outcome: unknown;
      const recorded: Record<string, unknown> = { ...step };
      try {
        switch (step.call) {
          case 'getValidationInfo': {
            const info = await doc.signatures.getValidationInfo();
            lastRead = info as unknown as Record<string, Uint8Array[]>;
            outcome = Object.fromEntries(KINDS.map((k) => [k, info[k].map(sha256)]));
            break;
          }
          case 'addValidationInfo': {
            let input: Record<string, unknown>;
            if (step.input === 'lastRead') {
              input = lastRead ?? {};
              recorded.input = Object.fromEntries(
                KINDS.map((k) => [k, (lastRead?.[k] ?? []).map(hex)]),
              );
            } else if (step.input === 'notBytes') {
              input = { certificates: ['MIIB'] };
            } else {
              input = Object.fromEntries(
                Object.entries(step.input).map(([k, list]) => [k, list!.map(unhex)]),
              );
            }
            const result = await doc.signatures.addValidationInfo(
              input,
              step.opId === undefined ? undefined : { opId: step.opId },
            );
            outcome = {
              added: result.added,
              undoable: result.meta.undoable,
              affectedPages: result.meta.affectedPages,
              ...(step.opId === undefined ? {} : { opId: result.meta.opId }),
            };
            break;
          }
          case 'prepare': {
            const input = step.artwork
              ? { ...step.input, appearance: { pdf: await bytesOf(ARTWORK) } }
              : step.input;
            const prepared = await doc.signatures.prepare(input as never);
            parked = { signingId: prepared.signingId, expectedVersion: prepared.expectedVersion };
            outcome = { subFilter: prepared.subFilter, ...(prepared.field ? { field: prepared.field } : {}) };
            break;
          }
          case 'complete': {
            const result = await doc.signatures.complete({
              signingId: parked!.signingId,
              cms: unhex(FAKE_CMS),
              expectedVersion: parked!.expectedVersion as never,
            });
            recorded.cms = FAKE_CMS;
            outcome = {
              status: result.status,
              kind: result.signature.kind,
              fieldName: result.signature.fieldName,
              coverage: result.signature.coverage,
              revisionIndex: result.signature.revisionIndex,
            };
            break;
          }
          case 'cancel': {
            outcome = { status: (await doc.signatures.cancel(parked!.signingId)).status };
            break;
          }
          case 'listSignatures': {
            const snapshot = await doc.signatures.list();
            outcome = {
              revisions: snapshot.revisions.length,
              enforced: snapshot.protection.enforced,
              signatures: snapshot.signatures.map(
                (s) => `${s.fieldName} ${s.kind} ${s.signed ? s.coverage : 'unsigned'}`,
              ),
            };
            break;
          }
          case 'listFields': {
            const form = await doc.forms.list();
            const view: Record<string, unknown> = {
              fields: form.fields.map((f) => `${f.name} ${f.kind}`),
            };
            if (step.widgetOf) {
              const field = form.fields.find((f) => f.name === step.widgetOf)!;
              const widget = form.widgets.find(
                (w) => JSON.stringify(w.field) === JSON.stringify(field.ref),
              )!;
              const firstPage = (await doc.pages.list()).pages[0]!.ref;
              view.widget = {
                onFirstPage: JSON.stringify(widget.page) === JSON.stringify(firstPage),
                width: widget.rect.width,
                height: widget.rect.height,
                print: widget.print,
                locked: widget.locked,
                hidden: widget.hidden,
              };
            }
            outcome = view;
            break;
          }
          case 'analyze': {
            const analysis = await doc.signatures.analyze(step.input as never);
            const findings = analysis.steps.flatMap((s) => s.findings);
            outcome = {
              verdict: analysis.verdict,
              current: analysis.current.verdict,
              policyVersion: analysis.policyVersion,
              steps: analysis.steps.length,
              refused: findings
                .filter((f) => f.verdict !== 'permitted')
                .map((f) => `${f.rule} ${f.objectNumber} ${f.verdict}`)
                .sort(),
              rules: [...new Set(findings.map((f) => f.rule))].sort(),
            };
            break;
          }
          case 'createTextField': {
            await doc.forms.create({ kind: 'text', name: step.name } as never, { opId: `field:${step.name}` });
            outcome = {};
            break;
          }
          case 'apply': {
            const result = await doc.apply(step.change as never, { opId: step.opId });
            outcome = { items: result.items.map((i) => i.type), undoable: result.meta.undoable };
            break;
          }
          case 'reopen': {
            const saved = await doc.download();
            const kept = Buffer.from(saved.subarray(0, loaded.length)).equals(Buffer.from(loaded));
            doc = await openDoc(saved);
            outcome = { prefixKept: kept };
            break;
          }
        }
        outcome = { resolve: outcome };
      } catch (error) {
        outcome = refusal(error);
      }
      await new Promise((r) => setTimeout(r, 0));
      steps.push({ ...recorded, outcome, events });
    }
    return {
      name: entry.name,
      covers: entry.covers,
      fixture: entry.fixture === 'fieldGroups' ? { hex: hex(loaded) } : entry.fixture,
      open,
      steps,
    };
  } finally {
    for (const doc of docs) await doc.close();
    await engine.destroy();
  }
}

// --- Output -----------------------------------------------------------------

function serialize(group: Group, cases: readonly unknown[]): string {
  const head = JSON.stringify({ schemaVersion: SCHEMA_VERSION, kind: group.kind }, null, 2).replace(
    /\n}$/,
    ',',
  );
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
    process.stdout.write(`  ok      signature/${relative}\n`);
    return false;
  }
  if (check) {
    process.stdout.write(`  DIFFERS signature/${relative}${current === undefined ? ' (missing)' : ''}\n`);
    return true;
  }
  await mkdir(dirname(absolute), { recursive: true });
  await writeFile(absolute, text);
  process.stdout.write(`  written signature/${relative}\n`);
  return false;
}

async function main(): Promise<void> {
  const check = process.argv.includes('--check');
  let differs = false;
  for (const group of [VALIDATION_INFO, TIMESTAMP_FIELD, await longTerm()]) {
    const names = new Set<string>();
    for (const entry of group.cases) {
      if (names.has(entry.name)) throw new Error(`${group.file}: duplicate case ${entry.name}`);
      names.add(entry.name);
    }
    const cases: unknown[] = [];
    for (const entry of group.cases) cases.push(await runCase(entry));
    differs = (await emit(group.file, serialize(group, cases), check)) || differs;
  }
  if (differs) {
    process.stderr.write(
      '\nSignature vectors differ. Regenerate with:\n' +
        '  pnpm --filter @embedpdf/tooling-engine-conformance run signature-ltv\n' +
        'and commit the diff.\n',
    );
    process.exitCode = 1;
  }
}

main().catch((error: unknown) => {
  process.stderr.write(
    `generate-signature-ltv-vectors: ${error instanceof Error ? (error.stack ?? error.message) : String(error)}\n`,
  );
  process.exitCode = 1;
});
