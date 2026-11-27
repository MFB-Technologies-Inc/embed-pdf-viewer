//! `packages/engine/core/src/signature/analysis/evaluate.ts`: judge one
//! pairwise step. Every reference to a changed object, in the older and the
//! newer revision, must be claimed by a rule that inspected that use; an
//! unclaimed reference is `unexplained` and forbidden.
//!
//! Transcribed function for function, in the TypeScript's order, with its
//! key tables kept as tables. The corpus case behind every table entry is
//! cited in the TypeScript and not repeated. `StepContext`'s mutable half
//! (findings, claims, locks) is split into `State` so a rule can walk the
//! changes while it records.

use std::collections::{HashMap, HashSet};

use crate::features::forms::snapshot::{FormFieldFamily, FormFieldRef};
use crate::features::signature::protection::{derive_protection, level_allows, lock_covers};
use crate::features::signature::types::{
    DocumentFieldLock, ModificationLevel, SignatureDto, SignatureKind,
};

use super::pdf_value::{PdfValue, changed_keys, dict_entries, entry, pdf_value_equals, refs_of};
use super::types::{
    ChangeFinding, FindingVerdict, ObjectChange, ObjectChangeKind, ObjectChangeType,
    ObjectReadStatus, ObjectReferrer, RevisionAnalysis, RevisionField, RevisionStructure, SIDES,
    Side, StepInput, StepRestrictions, UnwrittenObject,
};
use super::verdict::conclude;

const TRAILER_KEYS: &[&str] = &[
    "Size",
    "Prev",
    "XRefStm",
    "Info",
    "ID",
    "Root",
    "Type",
    "W",
    "Index",
    "Filter",
    "DecodeParms",
    "Length",
    "Encrypt",
];
const CATALOG_KEYS: &[&str] = &["Metadata", "Extensions", "Version", "DSS", "AcroForm", "Perms"];
const ACROFORM_KEYS: &[&str] = &["DR", "DA", "Q", "SigFlags", "NeedAppearances", "Fields"];

/// `FIELD_KEYS_BY_LEVEL`.
const FIELD_KEYS_ANNOTATE: &[&str] = &[
    "V", "AS", "AP", "I", "DA", "Ff", "RV", "MK", "Q", "TU", "F", "Rect", "BS", "Opt", "DV",
    "MaxLen",
];
const FIELD_KEYS_FILL: &[&str] = &["V", "AS", "AP", "I", "RV", "DA", "Ff"];
/// `WIDGET_KEYS_BY_LEVEL`.
const WIDGET_KEYS_ANNOTATE: &[&str] = &["AS", "AP", "DA", "MK", "Q", "TU", "F", "Rect", "BS"];
const WIDGET_KEYS_FILL: &[&str] = &["AS", "AP", "DA"];
/// `SIGNED_FIELD_KEYS_BY_LEVEL`.
const SIGNED_FIELD_KEYS_ANNOTATE: &[&str] = &["V", "AP", "AS", "Ff", "TU", "Lock"];
const SIGNED_FIELD_KEYS_FILL: &[&str] = &["V", "AP", "AS", "Ff"];
const SIGNED_WIDGET_KEYS: &[&str] = &["AP", "AS"];
const FF_READ_ONLY: i32 = 1;

fn field_keys_for(level: ModificationLevel) -> &'static [&'static str] {
    if level == ModificationLevel::Annotate { FIELD_KEYS_ANNOTATE } else { FIELD_KEYS_FILL }
}
fn widget_keys_for(level: ModificationLevel) -> &'static [&'static str] {
    if level == ModificationLevel::Annotate { WIDGET_KEYS_ANNOTATE } else { WIDGET_KEYS_FILL }
}
fn signed_field_keys_for(level: ModificationLevel) -> &'static [&'static str] {
    if level == ModificationLevel::Annotate {
        SIGNED_FIELD_KEYS_ANNOTATE
    } else {
        SIGNED_FIELD_KEYS_FILL
    }
}

/// `NON_EFFECTIVE_RULES`.
const NON_EFFECTIVE_RULES: &[&str] = &["identical-rewrite", "orphan", "xref-container", "trailer"];

fn level_name(level: ModificationLevel) -> &'static str {
    match level {
        ModificationLevel::None => "none",
        ModificationLevel::Lta => "lta",
        ModificationLevel::Fill => "fill",
        ModificationLevel::Annotate => "annotate",
    }
}

/// `restrictionsOf`.
pub fn restrictions_of(before: &RevisionStructure) -> StepRestrictions {
    let protection = derive_protection(&before.signatures);
    StepRestrictions {
        level: protection.judged.unwrap_or(ModificationLevel::Annotate),
        locks: protection.field_locks,
    }
}

/// One side of a change's value.
fn side_value(c: &ObjectChange, side: Side) -> Option<&PdfValue> {
    match side {
        Side::Old => c.value.old.as_ref(),
        Side::New => c.value.new.as_ref(),
    }
}

/// `unwrittenObjectsToRead` (92db1287a): the objects the rules read that
/// `changes` does not hold, for the transport to read from the revisions'
/// bytes and hand over as `StepInput::unwritten`. One kind so far: the
/// annotation list of a page this step wrote, when the list is an object of
/// its own that the step left alone.
pub fn unwritten_objects_to_read(
    changes: &[ObjectChange],
    before: &RevisionStructure,
    after: &RevisionStructure,
) -> Vec<u32> {
    let pages: HashSet<u32> = before.pages.iter().chain(&after.pages).copied().collect();
    let written: HashSet<u32> = changes.iter().map(|c| c.object_number).collect();
    let mut out: Vec<u32> = Vec::new();
    for c in changes {
        if !pages.contains(&c.object_number) {
            continue;
        }
        for side in SIDES {
            if let Some(PdfValue::Ref(num)) = entry(side_value(c, side), "Annots")
                && !written.contains(num)
                && !out.contains(num)
            {
                out.push(*num);
            }
        }
    }
    out.sort_unstable();
    out
}

/// `evaluateStep`.
pub fn evaluate_step(input: StepInput) -> RevisionAnalysis {
    let changes = with_evidence_check(input.changes.clone());
    let StepRestrictions { level, locks } =
        input.restrictions.clone().unwrap_or_else(|| restrictions_of(&input.before));
    let level_in_force = input.level_override.unwrap_or(level);
    let ctx = StepContext::new(&input, &changes, level_in_force, &locks);
    let mut st = State::default();

    rule_revision_health(&ctx, &mut st);
    rule_xref_container(&ctx, &mut st);
    rule_identical_rewrite(&ctx, &mut st);
    if ctx.allows(ModificationLevel::Fill) {
        // Locks first: a frozen object is condemned before any rule could vouch for it.
        rule_field_lock(&ctx, &mut st);
        rule_trailer(&ctx, &mut st);
        rule_info(&ctx, &mut st);
        rule_metadata(&ctx, &mut st);
        rule_catalog(&ctx, &mut st);
        rule_acro_form(&ctx, &mut st);
        rule_dss(&ctx, &mut st);
        rule_signature_field_added(&ctx, &mut st);
        rule_signature_added(&ctx, &mut st);
        rule_unsigned_signature_appearance(&ctx, &mut st);
        rule_form_fill(&ctx, &mut st);
        rule_annotation(&ctx, &mut st);
    } else {
        rule_trailer(&ctx, &mut st);
        rule_no_changes_certification(&ctx, &mut st);
    }
    rule_unexplained(&ctx, &mut st);

    let effective = st.findings.iter().any(|f| {
        f.verdict == FindingVerdict::Permitted && !NON_EFFECTIVE_RULES.contains(&f.rule.as_str())
    });
    let verdict = conclude(&st.findings, effective);
    RevisionAnalysis {
        older: input.older,
        newer: input.newer,
        level_in_force,
        locks,
        verdict,
        findings: st.findings,
        changes,
    }
}

/// `withEvidenceCheck`.
fn with_evidence_check(changes: Vec<ObjectChange>) -> Vec<ObjectChange> {
    changes
        .into_iter()
        .map(|mut c| {
            if c.value.truncated
                || c.kind == ObjectChangeKind::Xref
                || c.kind == ObjectChangeKind::Objstm
            {
                return c;
            }
            let failed = |side: Side| {
                c.read.as_ref().is_some_and(|r| *r.get(side) == ObjectReadStatus::Failed)
            };
            let missing_old = c.present.old && (c.value.old.is_none() || failed(Side::Old));
            let missing_new = c.present.new && (c.value.new.is_none() || failed(Side::New));
            if missing_old || missing_new {
                c.value.truncated = true;
            }
            c
        })
        .collect()
}

/// `ruleRevisionHealth`.
fn rule_revision_health(ctx: &StepContext<'_>, st: &mut State) {
    let Some(health) = &ctx.input.health else { return };
    let base = health.old;
    if base.sparse_xref || base.bare_references > 0 {
        let mut why = Vec::new();
        if base.sparse_xref {
            why.push(
                "its cross-reference table has no entry for some object numbers below /Size"
                    .to_string(),
            );
        }
        if base.bare_references > 0 {
            why.push(format!(
                "{} reachable indirect object(s) consist of a bare reference",
                base.bare_references
            ));
        }
        st.findings.push(ChangeFinding {
            rule: "base-unverifiable".into(),
            verdict: FindingVerdict::Incomplete,
            object_number: 0,
            edge: None,
            detail: Some(format!(
                "the signed revision cannot be verified against later changes by Acrobat ({}); no verdict is given rather than one Acrobat would contradict",
                why.join("; ")
            )),
        });
    }
    for side in SIDES {
        if !health.get(side).referrers_complete {
            st.findings.push(ChangeFinding {
                rule: "unexplained".into(),
                verdict: FindingVerdict::Incomplete,
                object_number: 0,
                edge: None,
                detail: Some(format!(
                    "the {} revision's reachability walk hit its budget; not every use of a changed object is known",
                    if side == Side::Old { "older" } else { "newer" }
                )),
            });
        }
    }
}

/// `sameEffectiveValue`.
pub fn same_effective_value(c: &ObjectChange) -> bool {
    let failed =
        |side: Side| c.read.as_ref().is_some_and(|r| *r.get(side) == ObjectReadStatus::Failed);
    c.change == ObjectChangeType::Modified
        && !c.value.truncated
        && !failed(Side::Old)
        && !failed(Side::New)
        && c.raw.old.is_some()
        && c.raw.new.is_some()
        && c.raw.old == c.raw.new
        && !c.stream_data_changed
}

// ---------------------------------------------------------------------------

/// An insertion-ordered map of fields, as a JavaScript `Map` iterates.
#[derive(Default)]
struct FieldMap {
    order: Vec<u32>,
    map: HashMap<u32, RevisionField>,
}

impl FieldMap {
    fn set(&mut self, key: u32, field: RevisionField) {
        if self.map.insert(key, field).is_none() {
            self.order.push(key);
        }
    }
    fn get(&self, key: u32) -> Option<&RevisionField> {
        self.map.get(&key)
    }
    fn has(&self, key: u32) -> bool {
        self.map.contains_key(&key)
    }
    fn iter(&self) -> impl Iterator<Item = (u32, &RevisionField)> {
        self.order.iter().map(|k| (*k, &self.map[k]))
    }
}

/// `RoleIndex`.
struct RoleIndex {
    field_by_obj: FieldMap,
    widget_to_field: HashMap<u32, u32>,
    /// Insertion order kept: `newSignatureFields` walks it.
    sig_fields: Vec<u32>,
    signed_fields: HashSet<u32>,
}

fn index_roles(s: &RevisionStructure) -> RoleIndex {
    let mut field_by_obj = FieldMap::default();
    let mut widget_to_field = HashMap::new();
    let mut sig_fields: Vec<u32> = Vec::new();
    for f in &s.fields {
        field_by_obj.set(f.object_number, f.clone());
        if f.family == FormFieldFamily::Signature && !sig_fields.contains(&f.object_number) {
            sig_fields.push(f.object_number);
        }
        for w in &f.widgets {
            widget_to_field.insert(*w, f.object_number);
        }
    }
    let signed_fields = s
        .signatures
        .iter()
        .filter(|sig| sig.signed)
        .filter_map(|sig| match sig.field {
            FormFieldRef::ObjectNumber { field } => Some(field),
            FormFieldRef::Fqn { .. } => None,
        })
        .collect();
    RoleIndex { field_by_obj, widget_to_field, sig_fields, signed_fields }
}

fn edge_key(e: &ObjectReferrer) -> String {
    format!("{}:{}", e.parent, e.label)
}

/// `StepContext`, read-only half.
struct StepContext<'a> {
    input: &'a StepInput,
    changes: &'a [ObjectChange],
    by_num: HashMap<u32, usize>,
    /// The objects the step left alone that the transport read for the rules.
    unwritten: HashMap<u32, &'a UnwrittenObject>,
    before: RoleIndex,
    after: RoleIndex,
    level: ModificationLevel,
    locks: &'a [DocumentFieldLock],
}

/// `StepContext`, what the rules record.
#[derive(Default)]
struct State {
    findings: Vec<ChangeFinding>,
    /// `${objectNumber}:${side}:${edge}` -> (rule, allow).
    claims: HashMap<String, (String, bool)>,
    /// Objects a lock forbids touching this step.
    locked: HashSet<u32>,
}

impl<'a> StepContext<'a> {
    fn new(
        input: &'a StepInput,
        changes: &'a [ObjectChange],
        level: ModificationLevel,
        locks: &'a [DocumentFieldLock],
    ) -> Self {
        let by_num: HashMap<u32, usize> =
            changes.iter().enumerate().map(|(i, c)| (c.object_number, i)).collect();
        let mut unwritten = HashMap::new();
        for kept in &input.unwritten {
            // A step's own object is read from the step, never from here.
            if !by_num.contains_key(&kept.object_number) {
                unwritten.insert(kept.object_number, kept);
            }
        }
        StepContext {
            input,
            changes,
            by_num,
            unwritten,
            before: index_roles(&input.before),
            after: index_roles(&input.after),
            level,
            locks,
        }
    }

    fn change(&self, n: u32) -> Option<&'a ObjectChange> {
        self.by_num.get(&n).map(|i| &self.changes[*i])
    }

    fn allows(&self, needed: ModificationLevel) -> bool {
        level_allows(Some(self.level), needed)
    }

    fn is_identical(&self, c: &ObjectChange) -> bool {
        same_effective_value(c)
    }

    /// `newlySigned`.
    fn newly_signed(&self) -> Vec<(u32, &'a SignatureDto)> {
        let mut out = Vec::new();
        for sig in &self.input.after.signatures {
            if !sig.signed {
                continue;
            }
            let FormFieldRef::ObjectNumber { field } = sig.field else { continue };
            if self.before.signed_fields.contains(&field) {
                continue;
            }
            out.push((field, sig));
        }
        out
    }
}

impl State {
    fn claim_edge(
        &mut self,
        c: &ObjectChange,
        side: Side,
        edge: &ObjectReferrer,
        rule: &str,
        allow: bool,
    ) {
        if self.locked.contains(&c.object_number) {
            return;
        }
        let key = format!("{}:{}:{}", c.object_number, side.name(), edge_key(edge));
        self.claims.entry(key).or_insert_with(|| (rule.to_string(), allow));
    }

    fn claim_all(&mut self, c: &ObjectChange, rule: &str, allow: bool) {
        for side in SIDES {
            for e in c.usage.get(side) {
                self.claim_edge(c, side, e, rule, allow);
            }
        }
    }

    fn claim_stable(&mut self, c: &ObjectChange, rule: &str, allow: bool) {
        let old_keys: HashSet<String> = c.usage.old.iter().map(edge_key).collect();
        let new_keys: HashSet<String> = c.usage.new.iter().map(edge_key).collect();
        for e in &c.usage.old {
            if new_keys.contains(&edge_key(e)) {
                self.claim_edge(c, Side::Old, e, rule, allow);
            }
        }
        for e in &c.usage.new {
            if old_keys.contains(&edge_key(e)) {
                self.claim_edge(c, Side::New, e, rule, allow);
            }
        }
    }

    fn is_claimed(&self, c: &ObjectChange, side: Side, edge: &ObjectReferrer) -> bool {
        self.claims.contains_key(&format!("{}:{}:{}", c.object_number, side.name(), edge_key(edge)))
    }

    fn is_allowed(&self, c: &ObjectChange, side: Side, edge: &ObjectReferrer) -> bool {
        self.claims
            .get(&format!("{}:{}:{}", c.object_number, side.name(), edge_key(edge)))
            .is_some_and(|(_, allow)| *allow)
    }

    fn permitted(&mut self, c: &ObjectChange, rule: &str, detail: Option<String>) {
        if self.locked.contains(&c.object_number) {
            return;
        }
        self.findings.push(ChangeFinding {
            rule: rule.into(),
            verdict: FindingVerdict::Permitted,
            object_number: c.object_number,
            edge: None,
            detail,
        });
    }

    fn forbidden(
        &mut self,
        c: &ObjectChange,
        rule: &str,
        detail: String,
        edge: Option<&ObjectReferrer>,
    ) {
        let truncated = c.value.truncated;
        self.findings.push(ChangeFinding {
            rule: rule.into(),
            verdict: if truncated { FindingVerdict::Incomplete } else { FindingVerdict::Forbidden },
            object_number: c.object_number,
            edge: edge.map(edge_key),
            detail: Some(if truncated {
                format!("{detail} (judged on missing evidence: not proven)")
            } else {
                detail
            }),
        });
    }

    fn has_finding_for(&self, n: u32) -> bool {
        self.findings.iter().any(|f| f.object_number == n)
    }

    /// `claimSubtree`.
    fn claim_subtree(
        &mut self,
        ctx: &StepContext<'_>,
        side: Side,
        roots: &HashSet<u32>,
        accept: impl Fn(&str) -> bool,
        rule: &str,
    ) {
        self.claim_subtree_admitting(ctx, side, roots, |label, _| accept(label), rule, |_| true);
    }

    /// `claimSubtree` whose `accept` reads the edge's parent too (7d5691a5a):
    /// a push button's image is under its own field or widget only.
    fn claim_subtree_by_parent(
        &mut self,
        ctx: &StepContext<'_>,
        side: Side,
        roots: &HashSet<u32>,
        accept: impl Fn(&str, u32) -> bool,
        rule: &str,
    ) {
        self.claim_subtree_admitting(ctx, side, roots, accept, rule, |_| true);
    }

    /// `claimSubtree` with its `admit`: which changed objects may join.
    fn claim_subtree_admitting(
        &mut self,
        ctx: &StepContext<'_>,
        side: Side,
        roots: &HashSet<u32>,
        accept: impl Fn(&str, u32) -> bool,
        rule: &str,
        admit: impl Fn(&ObjectChange) -> bool,
    ) {
        let mut joined: HashSet<u32> = HashSet::new();
        let mut grew = true;
        while grew {
            grew = false;
            for c in ctx.changes {
                if roots.contains(&c.object_number) || joined.contains(&c.object_number) {
                    continue;
                }
                // An object the rule doesn't admit stays out, and so does what hangs off it.
                if self.locked.contains(&c.object_number) || !admit(c) {
                    continue;
                }
                let edges = c.usage.get(side);
                if edges.is_empty() {
                    continue;
                }
                let inside: Vec<usize> = edges
                    .iter()
                    .enumerate()
                    .filter(|(_, e)| {
                        (roots.contains(&e.parent) && accept(&e.label, e.parent))
                            || joined.contains(&e.parent)
                    })
                    .map(|(i, _)| i)
                    .collect();
                if inside.is_empty() {
                    continue;
                }
                for i in &inside {
                    self.claim_edge(c, side, &edges[*i], rule, true);
                }
                let all_allowed = edges
                    .iter()
                    .enumerate()
                    .all(|(i, e)| inside.contains(&i) || self.is_allowed(c, side, e));
                if all_allowed {
                    joined.insert(c.object_number);
                    grew = true;
                }
            }
        }
    }
}

// ---------------------------------------------------------------------------
// Rules. Each claims the exact references it inspected and records a finding.
// ---------------------------------------------------------------------------

fn rule_xref_container(ctx: &StepContext<'_>, st: &mut State) {
    for c in ctx.changes {
        if c.kind == ObjectChangeKind::Xref || c.kind == ObjectChangeKind::Objstm {
            st.claim_all(c, "xref-container", true);
            st.permitted(
                c,
                "xref-container",
                Some("cross-reference container; members are judged on their own".into()),
            );
        }
    }
}

fn rule_identical_rewrite(ctx: &StepContext<'_>, st: &mut State) {
    for c in ctx.changes {
        if !ctx.is_identical(c) {
            continue;
        }
        if ctx.allows(ModificationLevel::Fill) {
            st.claim_all(c, "identical-rewrite", true);
            st.permitted(
                c,
                "identical-rewrite",
                Some("written again with the sealed value".into()),
            );
        } else {
            st.claim_all(c, "identical-rewrite", false);
            st.forbidden(
                c,
                "identical-rewrite",
                format!(
                    "written again with the sealed value under level '{}' (not established for this level)",
                    level_name(ctx.level)
                ),
                None,
            );
        }
    }
}

/// `ruleNoChangesCertification` (462380c7a): after a certification that
/// allows no changes, nothing may follow. ISO 32000-2 lets validation data
/// and document timestamps through; Acrobat reports the certification as
/// altered for each, so every object such a revision writes is forbidden.
/// The trailer and the cross-reference containers are the revision's own
/// bookkeeping, and an object written again unchanged is judged by its own
/// rule.
fn rule_no_changes_certification(ctx: &StepContext<'_>, st: &mut State) {
    let rule = "no-changes-certification";
    for c in ctx.changes {
        if c.object_number == 0
            || c.kind == ObjectChangeKind::Xref
            || c.kind == ObjectChangeKind::Objstm
        {
            continue;
        }
        if ctx.is_identical(c) {
            continue;
        }
        st.claim_all(c, rule, false);
        st.forbidden(
            c,
            rule,
            format!(
                "{} after a certification that allows no changes: Acrobat reports the certification as altered (ISO 32000-2 allows validation data and document timestamps here, Acrobat does not)",
                change_kind_name(c.change)
            ),
            None,
        );
    }
}

fn rule_trailer(ctx: &StepContext<'_>, st: &mut State) {
    let Some(c) = ctx.change(0) else { return };
    let changed = changed_keys(c.value.old.as_ref(), c.value.new.as_ref());
    let bad: Vec<&str> =
        changed.iter().map(String::as_str).filter(|k| !TRAILER_KEYS.contains(k)).collect();
    let old_root = entry(c.value.old.as_ref(), "Root");
    let new_root = entry(c.value.new.as_ref(), "Root");
    if !bad.is_empty() {
        st.forbidden(c, "trailer", format!("trailer keys changed: {}", bad.join(", ")), None);
        return;
    }
    if changed.iter().any(|k| k == "Root") && !pdf_value_equals(old_root, new_root) {
        st.forbidden(c, "trailer", "the trailer points at a different catalog".into(), None);
        return;
    }
    if changed.iter().any(|k| k == "Encrypt")
        && !pdf_value_equals(
            entry(c.value.old.as_ref(), "Encrypt"),
            entry(c.value.new.as_ref(), "Encrypt"),
        )
    {
        st.forbidden(
            c,
            "trailer",
            "the trailer points at a different encryption dictionary".into(),
            None,
        );
        return;
    }
    st.claim_all(c, "trailer", true);
    st.permitted(c, "trailer", None);
}

/// `onlyEdges`.
fn only_edges(c: &ObjectChange, test: impl Fn(&ObjectReferrer) -> bool) -> bool {
    let mut all = c.usage.old.iter().chain(c.usage.new.iter()).peekable();
    all.peek().is_some() && all.all(test)
}

fn rule_info(ctx: &StepContext<'_>, st: &mut State) {
    for c in ctx.changes {
        if ctx.is_identical(c) {
            continue;
        }
        if c.object_number != 0 && only_edges(c, |e| e.parent == 0 && e.label == "Info") {
            st.claim_all(c, "info", true);
            st.permitted(c, "info", Some("document information dictionary".into()));
        }
    }
}

fn rule_metadata(ctx: &StepContext<'_>, st: &mut State) {
    let roots: HashSet<u32> = [ctx.input.before.root, ctx.input.after.root].into_iter().collect();
    for c in ctx.changes {
        if ctx.is_identical(c) {
            continue;
        }
        if only_edges(c, |e| roots.contains(&e.parent) && e.label == "Metadata") {
            st.claim_all(c, "metadata", true);
            st.permitted(c, "metadata", Some("XMP metadata stream".into()));
        }
    }
}

fn rule_catalog(ctx: &StepContext<'_>, st: &mut State) {
    let root = ctx.input.after.root;
    let Some(c) = ctx.change(root) else { return };
    if ctx.is_identical(c) {
        return;
    }
    let changed = changed_keys(c.value.old.as_ref(), c.value.new.as_ref());
    let has = |k: &str| changed.iter().any(|c| c == k);
    let bad: Vec<&str> =
        changed.iter().map(String::as_str).filter(|k| !CATALOG_KEYS.contains(k)).collect();
    if !bad.is_empty() {
        st.forbidden(
            c,
            "catalog-housekeeping",
            format!("catalog keys changed: {}", bad.join(", ")),
            None,
        );
        return;
    }
    // A DIRECT /AcroForm dictionary changes with the catalog: judge it by the
    // AcroForm rules, and let its default resources ride the catalog's edges.
    if has("AcroForm") && ctx.input.after.acro_form == 0 && ctx.input.before.acro_form == 0 {
        let problem = acro_form_problem(
            ctx,
            entry(c.value.old.as_ref(), "AcroForm"),
            entry(c.value.new.as_ref(), "AcroForm"),
        );
        if let Some(problem) = problem {
            st.forbidden(c, "acroform-housekeeping", problem, None);
            return;
        }
        let roots: HashSet<u32> = [root].into_iter().collect();
        for side in SIDES {
            st.claim_subtree(
                ctx,
                side,
                &roots,
                |label| label.starts_with("AcroForm/DR/"),
                "acroform-housekeeping",
            );
        }
    }
    if has("Perms") {
        let after = derive_protection(&ctx.input.after.signatures);
        let before = derive_protection(&ctx.input.before.signatures);
        if after.certification.is_none() || before.judged.is_some() {
            st.forbidden(
                c,
                "catalog-housekeeping",
                "/Perms may only appear with the certification signature, in the first signed revision".into(),
                None,
            );
            return;
        }
    }
    st.claim_all(c, "catalog-housekeeping", true);
    st.permitted(c, "catalog-housekeeping", None);
}

fn rule_acro_form(ctx: &StepContext<'_>, st: &mut State) {
    let acro = if ctx.input.after.acro_form != 0 {
        ctx.input.after.acro_form
    } else {
        ctx.input.before.acro_form
    };
    if acro == 0 {
        return;
    }
    let Some(c) = ctx.change(acro) else { return };
    if ctx.is_identical(c) {
        return;
    }
    if let Some(problem) = acro_form_problem(ctx, c.value.old.as_ref(), c.value.new.as_ref()) {
        st.forbidden(c, "acroform-housekeeping", problem, None);
        return;
    }
    st.claim_all(c, "acroform-housekeeping", true);
    st.permitted(c, "acroform-housekeeping", None);
    // Default resources (fonts for appearances) live under /DR.
    let roots: HashSet<u32> = [acro].into_iter().collect();
    for side in SIDES {
        st.claim_subtree(
            ctx,
            side,
            &roots,
            |label| label.starts_with("DR/"),
            "acroform-housekeeping",
        );
    }
}

/// `acroFormProblem`.
fn acro_form_problem(
    ctx: &StepContext<'_>,
    old: Option<&PdfValue>,
    new: Option<&PdfValue>,
) -> Option<String> {
    let changed = changed_keys(old, new);
    let bad: Vec<&str> =
        changed.iter().map(String::as_str).filter(|k| !ACROFORM_KEYS.contains(k)).collect();
    if !bad.is_empty() {
        return Some(format!("AcroForm keys changed: {}", bad.join(", ")));
    }
    if changed.iter().any(|k| k == "Fields") {
        let old_refs = refs_of(entry(old, "Fields"));
        let new_refs = refs_of(entry(new, "Fields"));
        if old_refs.len() > new_refs.len()
            || old_refs.iter().enumerate().any(|(i, n)| new_refs.get(i) != Some(n))
        {
            return Some("/AcroForm /Fields is not append-only".into());
        }
        let new_sig_fields = new_signature_fields(ctx);
        let strangers: Vec<String> = new_refs[old_refs.len()..]
            .iter()
            .filter(|n| !new_sig_fields.contains(n))
            .map(u32::to_string)
            .collect();
        if !strangers.is_empty() {
            return Some(format!(
                "/AcroForm /Fields gained non-signature fields: {}",
                strangers.join(", ")
            ));
        }
    }
    None
}

/// `appendedRefs`.
fn appended_refs(c: &ObjectChange, key: &str) -> Option<Vec<u32>> {
    let old_items = entry(c.value.old.as_ref(), key);
    let new_items = entry(c.value.new.as_ref(), key);
    let old_refs = refs_of(old_items);
    let new_refs = refs_of(new_items);
    if old_items.is_some_and(|v| !matches!(v, PdfValue::Array(_))) {
        return None;
    }
    if !new_items.is_some_and(|v| matches!(v, PdfValue::Array(_))) {
        return None;
    }
    if old_refs.len() > new_refs.len() {
        return None;
    }
    if old_refs.iter().zip(&new_refs).any(|(a, b)| a != b) {
        return None;
    }
    Some(new_refs[old_refs.len()..].to_vec())
}

/// `annotsHolders` (92db1287a): the array objects of this step that are one
/// page's /Annots and nothing else, with their page, in the step's order.
fn annots_holders(ctx: &StepContext<'_>) -> Vec<(u32, u32)> {
    let pages: HashSet<u32> =
        ctx.input.before.pages.iter().chain(&ctx.input.after.pages).copied().collect();
    let mut out = Vec::new();
    for c in ctx.changes {
        if c.kind != ObjectChangeKind::Array || c.value.truncated {
            continue;
        }
        let uses: Vec<&ObjectReferrer> = c.usage.old.iter().chain(&c.usage.new).collect();
        // Every use is one page's /Annots: nothing else points at the array.
        if uses.is_empty() || !uses.iter().all(|e| pages.contains(&e.parent) && e.label == "Annots")
        {
            continue;
        }
        let owners: HashSet<u32> = uses.iter().map(|e| e.parent).collect();
        if owners.len() != 1 {
            continue;
        }
        let only_references = [&c.value.old, &c.value.new].into_iter().all(|value| match value {
            None => true,
            Some(PdfValue::Array(items)) => refs_of(value.as_ref()).len() == items.len(),
            Some(_) => false,
        });
        if only_references {
            out.push((c.object_number, uses[0].parent));
        }
    }
    out
}

/// The page whose list `number` is, when it is one of `holders`.
fn holder_page(holders: &[(u32, u32)], number: u32) -> Option<u32> {
    holders.iter().find(|(n, _)| *n == number).map(|(_, page)| *page)
}

/// `PageList`: a page's annotation list on one side of a step, and where it
/// is kept.
struct PageList<'a> {
    refs: Vec<u32>,
    /// The object that holds the list when it is one of its own; 0 when the
    /// array is inside the page.
    object: u32,
    /// That object's change, when this step wrote it.
    holder: Option<&'a ObjectChange>,
}

/// `pageList`: a page's annotation list on one side, from the page, from the
/// list's own object when this step wrote it, or from the revision's bytes
/// (`StepInput::unwritten`). `None` when it cannot be read.
fn page_list<'a>(
    ctx: &StepContext<'a>,
    holders: &[(u32, u32)],
    page: &ObjectChange,
    side: Side,
) -> Option<PageList<'a>> {
    let Some(value) = entry(side_value(page, side), "Annots") else {
        return Some(PageList { refs: Vec::new(), object: 0, holder: None });
    };
    let num = match value {
        PdfValue::Array(_) => {
            return Some(PageList { refs: refs_of(Some(value)), object: 0, holder: None });
        }
        PdfValue::Ref(num) => *num,
        _ => return None,
    };
    let Some(holder) = ctx.change(num) else {
        let kept = ctx.unwritten.get(&num)?;
        if kept.read != ObjectReadStatus::Ok {
            return None;
        }
        let Some(PdfValue::Array(items)) = &kept.value else { return None };
        let refs = refs_of(kept.value.as_ref());
        if refs.len() != items.len() {
            return None;
        }
        return Some(PageList { refs, object: num, holder: None });
    };
    if holder_page(holders, holder.object_number) != Some(page.object_number) {
        return None;
    }
    side_value(holder, side).map(|list| PageList {
        refs: refs_of(Some(list)),
        object: holder.object_number,
        holder: Some(holder),
    })
}

/// `appendedTo`: what `after` has beyond `before`, when `before` is its
/// beginning.
fn appended_to(before: &[u32], after: &[u32]) -> Option<Vec<u32>> {
    if before.len() > after.len() || before.iter().zip(after).any(|(a, b)| a != b) {
        return None;
    }
    Some(after[before.len()..].to_vec())
}

/// `newSignatureFields`, in the after revision's field order.
fn new_signature_fields(ctx: &StepContext<'_>) -> Vec<u32> {
    ctx.after.sig_fields.iter().copied().filter(|n| !ctx.before.field_by_obj.has(*n)).collect()
}

fn rule_dss(ctx: &StepContext<'_>, st: &mut State) {
    let roots: HashSet<u32> = [ctx.input.before.root, ctx.input.after.root].into_iter().collect();
    for side in SIDES {
        st.claim_subtree(
            ctx,
            side,
            &roots,
            |label| label == "DSS" || label.starts_with("DSS/"),
            "dss",
        );
    }
    for c in ctx.changes {
        if c.usage.new.iter().any(|e| roots.contains(&e.parent) && e.label == "DSS")
            && !st.has_finding_for(c.object_number)
        {
            st.permitted(c, "dss", Some("document security store".into()));
        }
    }
}

fn rule_signature_field_added(ctx: &StepContext<'_>, st: &mut State) {
    let added = new_signature_fields(ctx);
    if added.is_empty() {
        return;
    }
    let rule = "signature-field-added";
    if !ctx.allows(ModificationLevel::Fill) {
        for n in &added {
            if let Some(c) = ctx.change(*n) {
                st.forbidden(
                    c,
                    rule,
                    format!("a signature field was added under level '{}'", level_name(ctx.level)),
                    None,
                );
            }
        }
        return;
    }
    let mut new_widgets: HashSet<u32> = HashSet::new();
    for n in &added {
        let field = ctx.after.field_by_obj.get(*n).expect("a new signature field is a field");
        if let Some(c) = ctx.change(*n) {
            st.claim_all(c, rule, true);
            st.permitted(c, rule, Some(format!("new signature field \"{}\"", field.name)));
        }
        for w in &field.widgets {
            new_widgets.insert(*w);
            if w == n {
                continue;
            }
            if let Some(wc) = ctx.change(*w) {
                st.claim_all(wc, rule, true);
                st.permitted(
                    wc,
                    rule,
                    Some(format!("widget of new signature field \"{}\"", field.name)),
                );
            }
        }
    }
    // The page that lists the new widget: /Annots append-only. The list is an
    // array inside the page, or an object of its own (Acrobat's way); when the
    // field is added it may stay where it is, move from one to the other, or
    // move to a new list object while the old one stays in the file unwritten
    // (92db1287a).
    let holders = annots_holders(ctx);
    for p in &ctx.input.after.pages {
        let Some(c) = ctx.change(*p) else { continue };
        if ctx.is_identical(c) {
            continue;
        }
        let changed = changed_keys(c.value.old.as_ref(), c.value.new.as_ref());
        if changed.len() != 1 || changed[0] != "Annots" {
            continue;
        }
        let (Some(before), Some(after)) =
            (page_list(ctx, &holders, c, Side::Old), page_list(ctx, &holders, c, Side::New))
        else {
            continue;
        };
        let Some(appended) = appended_to(&before.refs, &after.refs) else { continue };
        if !appended.iter().all(|n| new_widgets.contains(n)) {
            continue;
        }
        // A list that moved to another object must also have grown: a move
        // alone is not something a new field explains.
        let moved = before.object != after.object;
        if moved && appended.is_empty() {
            continue;
        }
        st.claim_all(c, rule, true);
        st.permitted(c, rule, Some("page /Annots gained the new signature widget".into()));
        let replaced = before.object != 0 && after.object != 0;
        let mut seen: Vec<u32> = Vec::new();
        for holder in [before.holder, after.holder].into_iter().flatten() {
            if seen.contains(&holder.object_number) {
                continue;
            }
            seen.push(holder.object_number);
            st.claim_all(holder, rule, true);
            st.permitted(
                holder,
                rule,
                Some(if replaced {
                    "page /Annots moved to another list object, with the new signature widget"
                        .into()
                } else {
                    "page /Annots moved into an object of its own, with the new signature widget"
                        .into()
                }),
            );
        }
    }
    // A list that already is an object of its own, and grows there: the page
    // itself is not written.
    for (number, _) in &holders {
        let Some(c) = ctx.change(*number) else { continue };
        if c.change != ObjectChangeType::Modified || ctx.is_identical(c) {
            continue;
        }
        if let Some(appended) =
            appended_to(&refs_of(c.value.old.as_ref()), &refs_of(c.value.new.as_ref()))
            && !appended.is_empty()
            && appended.iter().all(|n| new_widgets.contains(n))
        {
            st.claim_all(c, rule, true);
            st.permitted(
                c,
                rule,
                Some("page /Annots, an object of its own, gained the new signature widget".into()),
            );
        }
    }
    // A parent field whose /Kids gained the new field.
    for c in ctx.changes {
        if c.kind != ObjectChangeKind::Dictionary || ctx.is_identical(c) {
            continue;
        }
        let changed = changed_keys(c.value.old.as_ref(), c.value.new.as_ref());
        if changed.len() != 1 || changed[0] != "Kids" {
            continue;
        }
        if let Some(appended) = appended_refs(c, "Kids")
            && !appended.is_empty()
            && appended.iter().all(|n| added.contains(n) || new_widgets.contains(n))
        {
            st.claim_all(c, rule, true);
            st.permitted(c, rule, Some("field /Kids gained the new signature field".into()));
        }
    }
}

fn rule_signature_added(ctx: &StepContext<'_>, st: &mut State) {
    let rule = "signature-added";
    let events = ctx.newly_signed();
    if events.is_empty() {
        return;
    }
    let added_this_step = new_signature_fields(ctx);
    let before_protection = derive_protection(&ctx.input.before.signatures);
    let after_protection = derive_protection(&ctx.input.after.signatures);
    let mut parents: HashSet<u32> = HashSet::new();
    let mut values: HashSet<u32> = HashSet::new();
    for (field_object_number, sig) in events {
        let needed = if sig.kind == SignatureKind::Timestamp {
            ModificationLevel::Lta
        } else {
            ModificationLevel::Fill
        };
        let kind = if sig.kind == SignatureKind::Timestamp { "timestamp" } else { "signature" };
        if !ctx.allows(needed) {
            if let Some(c) = ctx.change(field_object_number) {
                st.forbidden(
                    c,
                    rule,
                    format!("a {kind} was added under level '{}'", level_name(ctx.level)),
                    None,
                );
            }
            continue;
        }
        if sig.catalog_certification && before_protection.judged.is_some() {
            if let Some(c) = ctx.change(field_object_number) {
                st.forbidden(
                    c,
                    rule,
                    "a certification signature after another signature".into(),
                    None,
                );
            }
            continue;
        }
        let field = ctx.after.field_by_obj.get(field_object_number);
        parents.insert(field_object_number);
        for w in field.map(|f| f.widgets.as_slice()).unwrap_or_default() {
            parents.insert(*w);
        }

        let field_is_new = added_this_step.contains(&field_object_number);
        let mut allowed_field_keys: Vec<&str> = signed_field_keys_for(ctx.level).to_vec();
        if before_protection.judged.is_none() && !allowed_field_keys.contains(&"Lock") {
            allowed_field_keys.push("Lock");
        }
        let fc = ctx.change(field_object_number);
        if let Some(fc) = fc.filter(|_| field_is_new) {
            st.permitted(fc, rule, Some(format!("new field \"{}\" signed", sig.field_name)));
        } else if let Some(fc) = fc.filter(|fc| !ctx.is_identical(fc)) {
            let changed = changed_keys(fc.value.old.as_ref(), fc.value.new.as_ref());
            let bad: Vec<&str> = changed
                .iter()
                .map(String::as_str)
                .filter(|k| !allowed_field_keys.contains(k))
                .collect();
            if !bad.is_empty() {
                st.forbidden(
                    fc,
                    rule,
                    format!("signature field keys changed: {}", bad.join(", ")),
                    None,
                );
                st.claim_stable(fc, rule, false);
            } else if changed.iter().any(|k| k == "Ff")
                && !read_only_only_change(
                    fc,
                    ctx.before.field_by_obj.get(field_object_number).and_then(|f| f.flags),
                )
            {
                st.forbidden(
                    fc,
                    rule,
                    "signature field flags changed beyond ReadOnly".into(),
                    None,
                );
                st.claim_stable(fc, rule, false);
            } else {
                st.claim_stable(fc, rule, true);
                st.permitted(fc, rule, Some(format!("field \"{}\" signed", sig.field_name)));
            }
        }
        for w in field.map(|f| f.widgets.as_slice()).unwrap_or_default() {
            if *w == field_object_number || field_is_new {
                continue;
            }
            let Some(wc) = ctx.change(*w) else { continue };
            if ctx.is_identical(wc) {
                continue;
            }
            let bad: Vec<String> = changed_keys(wc.value.old.as_ref(), wc.value.new.as_ref())
                .into_iter()
                .filter(|k| !SIGNED_WIDGET_KEYS.contains(&k.as_str()))
                .collect();
            if !bad.is_empty() {
                st.forbidden(
                    wc,
                    rule,
                    format!("signature widget keys changed: {}", bad.join(", ")),
                    None,
                );
                st.claim_stable(wc, rule, false);
                continue;
            }
            st.claim_stable(wc, rule, true);
            st.permitted(wc, rule, Some("signature widget appearance".into()));
        }
        // The /V value and the /Lock dictionary installed with the signature.
        for c in ctx.changes {
            if c.change != ObjectChangeType::Added {
                continue;
            }
            let as_value =
                c.usage.new.iter().any(|e| e.parent == field_object_number && e.label == "V");
            let as_lock: Vec<&ObjectReferrer> = c
                .usage
                .new
                .iter()
                .filter(|e| e.parent == field_object_number && e.label == "Lock")
                .collect();
            let ty = entry(c.value.new.as_ref(), "Type");
            if as_value {
                let is_sig =
                    matches!(ty, Some(PdfValue::Name(v)) if v == "Sig" || v == "DocTimeStamp");
                if !is_sig && ty.is_some() {
                    continue;
                }
                for e in &c.usage.new {
                    if (e.parent == field_object_number && e.label == "V") || e.label == "DocMDP" {
                        st.claim_edge(c, Side::New, e, rule, true);
                    }
                }
                st.permitted(c, rule, Some("signature value".into()));
                values.insert(c.object_number);
            } else if !as_lock.is_empty() {
                let is_lock =
                    ty.is_none() || matches!(ty, Some(PdfValue::Name(v)) if v == "SigFieldLock");
                if !is_lock {
                    continue;
                }
                for e in as_lock {
                    st.claim_edge(c, Side::New, e, rule, true);
                }
                st.permitted(c, rule, Some("field lock installed with the signature".into()));
            }
        }
    }
    // /Perms: only with the certification, only in the first signed revision.
    if after_protection.certification.is_some() && before_protection.judged.is_none() {
        let root = ctx.input.after.root;
        for c in ctx.changes {
            if c.change == ObjectChangeType::Added
                && only_edges(c, |e| e.parent == root && e.label == "Perms")
            {
                st.claim_all(c, rule, true);
                st.permitted(c, rule, Some("certification permissions".into()));
            }
        }
    }
    for side in SIDES {
        st.claim_subtree(
            ctx,
            side,
            &parents,
            |label| label == "AP" || label.starts_with("AP/"),
            rule,
        );
    }
    if !values.is_empty() {
        st.claim_subtree(ctx, Side::New, &values, |_| true, rule);
    }
}

/// `ruleUnsignedSignatureAppearance`. A new appearance on a signature field that is not signed,
/// before the step or after it: the field and its widgets change `/AP` and nothing else, and every
/// object under the new `/AP` is new (removed ones may go). Acrobat does not count it as a change
/// after an approval signature (corpus v4/93, 94, 97, 99) and permits it under a P=2
/// certification (v4/101, 102). A signed field is left alone on purpose (v4/95): there the drawing
/// replaces the look the signature was made with.
fn rule_unsigned_signature_appearance(ctx: &StepContext<'_>, st: &mut State) {
    let rule = "signature-appearance";
    let mut owners: HashSet<u32> = HashSet::new();
    for (num, field) in ctx.before.field_by_obj.iter() {
        let Some(after) = ctx.after.field_by_obj.get(num) else { continue };
        if field.family != FormFieldFamily::Signature || after.family != FormFieldFamily::Signature
        {
            continue;
        }
        if ctx.before.signed_fields.contains(&num) || ctx.after.signed_fields.contains(&num) {
            continue;
        }
        let mut parts: Vec<u32> = vec![num];
        for w in &field.widgets {
            if !parts.contains(w) {
                parts.push(*w);
            }
        }
        if after.widgets.len() != field.widgets.len()
            || after.widgets.iter().any(|w| !parts.contains(w))
        {
            continue;
        }
        let written: Vec<&ObjectChange> =
            parts.iter().filter_map(|n| ctx.change(*n)).filter(|c| !ctx.is_identical(c)).collect();
        // A lock has already condemned what it covers.
        if written.is_empty() || written.iter().any(|c| st.locked.contains(&c.object_number)) {
            continue;
        }
        let beyond_appearance = |c: &ObjectChange| -> Vec<String> {
            changed_keys(c.value.old.as_ref(), c.value.new.as_ref())
                .into_iter()
                .filter(|k| k != "AP")
                .collect()
        };
        if written.iter().any(|c| !beyond_appearance(c).is_empty()) {
            // Only the appearance has a case behind it. A condemned field still claims its
            // stable edges: one cause in the report, not a cascade.
            for c in &written {
                let keys = beyond_appearance(c);
                if keys.is_empty() {
                    continue;
                }
                st.forbidden(
                    c,
                    rule,
                    format!(
                        "signature field \"{}\", not signed, keys changed: {}",
                        field.name,
                        keys.join(", ")
                    ),
                    None,
                );
                st.claim_stable(c, rule, false);
            }
            continue;
        }
        for c in &written {
            st.claim_stable(c, rule, true);
            st.permitted(
                c,
                rule,
                Some(format!("new appearance on signature field \"{}\", not signed", field.name)),
            );
        }
        owners.extend(parts);
    }
    if owners.is_empty() {
        return;
    }
    // Under the new /AP only new objects; under the old one only objects removed.
    st.claim_subtree_admitting(
        ctx,
        Side::New,
        &owners,
        |label, _| is_ap(label),
        rule,
        |c| c.change == ObjectChangeType::Added,
    );
    st.claim_subtree_admitting(
        ctx,
        Side::Old,
        &owners,
        |label, _| is_ap(label),
        rule,
        |c| c.change == ObjectChangeType::Freed,
    );
}

/// JavaScript's ToInt32, for `|` on a PDF number.
fn to_int32(v: f64) -> i32 {
    if !v.is_finite() {
        return 0;
    }
    (v.trunc().rem_euclid(4_294_967_296.0) as u64 as u32) as i32
}

/// `readOnlyOnlyChange`.
fn read_only_only_change(c: &ObjectChange, effective_before: Option<u32>) -> bool {
    let number = |v: Option<&PdfValue>| match v {
        Some(PdfValue::Number(n)) => Some(*n),
        _ => None,
    };
    let before = number(entry(c.value.old.as_ref(), "Ff"))
        .unwrap_or_else(|| f64::from(effective_before.unwrap_or(0)));
    let after = number(entry(c.value.new.as_ref(), "Ff")).unwrap_or(0.0);
    f64::from(to_int32(before) | FF_READ_ONLY) == after
}

fn is_ap(label: &str) -> bool {
    label == "AP" || label.starts_with("AP/")
}

/// `imageOnlyMkChange`: did a push button's `/MK` change in its image (`/I`)
/// and nothing else? An image is filling the button in, as Acrobat counts
/// it: "not modified" after an approval signature, "permitted by the
/// certifying party" after a P=2 certification (7d5691a5a). Its caption,
/// colors and layout (`/CA`, `/BC`, `/BG`, `/TP`) are design. An `/MK` held
/// by reference is not read through, so a change of it stays design.
fn image_only_mk_change(c: &ObjectChange) -> bool {
    let before = entry(c.value.old.as_ref(), "MK");
    let after = entry(c.value.new.as_ref(), "MK");
    let dict_or_none =
        |value: Option<&PdfValue>| value.is_none_or(|v| matches!(v, PdfValue::Dict(_)));
    if !dict_or_none(before) || !dict_or_none(after) {
        return false;
    }
    let changed = changed_keys(before, after);
    !changed.is_empty() && changed.iter().all(|key| key == "I")
}

/// `isButtonImage`: a push button's image under its field or widget, the
/// `/MK /I` drawing and what it holds.
fn is_button_image(label: &str) -> bool {
    label == "MK/I" || label.starts_with("MK/I/")
}

fn rule_form_fill(ctx: &StepContext<'_>, st: &mut State) {
    let rule = "form-fill";
    let field_keys = field_keys_for(ctx.level);
    let widget_keys = widget_keys_for(ctx.level);
    let mut parents: HashSet<u32> = HashSet::new();
    // The push buttons among `parents`: their image is claimed with their
    // appearance.
    let mut buttons: HashSet<u32> = HashSet::new();
    let mut fields = FieldMap::default();
    for (n, f) in ctx.before.field_by_obj.iter() {
        fields.set(n, f.clone());
    }
    for (n, f) in ctx.after.field_by_obj.iter() {
        fields.set(n, f.clone());
    }
    for (num, field) in fields.iter() {
        if field.family == FormFieldFamily::Signature {
            continue;
        }
        let is_button = field.family == FormFieldFamily::Pushbutton;
        // `/MK` as filling in: a push button's image, and no other part of it.
        let image_only = |c: &ObjectChange| is_button && image_only_mk_change(c);
        let fc = ctx.change(num);
        let widget_changes: Vec<&ObjectChange> =
            field.widgets.iter().filter(|w| **w != num).filter_map(|w| ctx.change(*w)).collect();
        let owners: HashSet<u32> =
            std::iter::once(num).chain(field.widgets.iter().copied()).collect();
        let appearance_only: Vec<&ObjectChange> = if fc.is_none() && widget_changes.is_empty() {
            ctx.changes
                .iter()
                .filter(|c| {
                    SIDES.iter().any(|s| {
                        c.usage
                            .get(*s)
                            .iter()
                            .any(|e| owners.contains(&e.parent) && is_ap(&e.label))
                    }) && SIDES.iter().all(|s| {
                        c.usage
                            .get(*s)
                            .iter()
                            .all(|e| !is_ap(&e.label) || owners.contains(&e.parent))
                    })
                })
                .collect()
        } else {
            Vec::new()
        };
        if fc.is_none() && widget_changes.is_empty() && appearance_only.is_empty() {
            continue;
        }
        if !ctx.allows(ModificationLevel::Fill) {
            for c in fc
                .into_iter()
                .chain(widget_changes.iter().copied())
                .chain(appearance_only.iter().copied())
            {
                if !ctx.is_identical(c) {
                    st.forbidden(
                        c,
                        rule,
                        format!("form fill under level '{}'", level_name(ctx.level)),
                        None,
                    );
                }
            }
            continue;
        }
        if !appearance_only.is_empty() {
            for c in &appearance_only {
                st.permitted(
                    c,
                    rule,
                    Some(format!("appearance of field \"{}\" regenerated", field.name)),
                );
            }
            parents.insert(num);
            parents.extend(field.widgets.iter().copied());
            continue;
        }
        let mut ok = true;
        if let Some(fc) = fc.filter(|fc| !ctx.is_identical(fc)) {
            let changed = changed_keys(fc.value.old.as_ref(), fc.value.new.as_ref());
            let has = |k: &str| changed.iter().any(|c| c == k);
            let image = has("MK") && image_only(fc);
            let bad: Vec<&str> = changed
                .iter()
                .map(String::as_str)
                .filter(|k| !field_keys.contains(k) && !(*k == "MK" && image))
                .collect();
            if !bad.is_empty() {
                st.forbidden(
                    fc,
                    rule,
                    format!("field \"{}\" keys changed: {}", field.name, bad.join(", ")),
                    None,
                );
                st.claim_stable(fc, rule, false);
                ok = false;
            } else if has("Ff")
                && !read_only_only_change(
                    fc,
                    ctx.before.field_by_obj.get(num).and_then(|f| f.flags),
                )
            {
                st.forbidden(
                    fc,
                    rule,
                    format!("field \"{}\" flags changed beyond adding ReadOnly", field.name),
                    None,
                );
                st.claim_stable(fc, rule, false);
                ok = false;
            } else {
                st.claim_stable(fc, rule, true);
                let properties: Vec<&str> = changed
                    .iter()
                    .map(String::as_str)
                    .filter(|k| !matches!(*k, "V" | "AP" | "AS" | "I" | "RV"))
                    .filter(|k| !(*k == "MK" && image))
                    .collect();
                let detail = if properties.is_empty() && image {
                    format!("push button \"{}\" image changed", field.name)
                } else if properties.is_empty() {
                    format!("field \"{}\" filled", field.name)
                } else {
                    format!(
                        "field \"{}\" {}properties changed: {}",
                        field.name,
                        if has("V") || has("AS") { "filled, " } else { "" },
                        properties.join(", ")
                    )
                };
                st.permitted(fc, rule, Some(detail));
            }
        }
        for wc in &widget_changes {
            if ctx.is_identical(wc) {
                continue;
            }
            let changed = changed_keys(wc.value.old.as_ref(), wc.value.new.as_ref());
            let image = changed.iter().any(|k| k == "MK") && image_only(wc);
            let bad: Vec<String> = changed
                .into_iter()
                .filter(|k| !widget_keys.contains(&k.as_str()) && !(k == "MK" && image))
                .collect();
            if !bad.is_empty() {
                st.forbidden(
                    wc,
                    rule,
                    format!("widget of \"{}\" keys changed: {}", field.name, bad.join(", ")),
                    None,
                );
                st.claim_stable(wc, rule, false);
                ok = false;
                continue;
            }
            st.claim_stable(wc, rule, true);
            let what = if image { "image" } else { "appearance" };
            st.permitted(wc, rule, Some(format!("widget of \"{}\" {what}", field.name)));
        }
        if ok {
            parents.insert(num);
            parents.extend(field.widgets.iter().copied());
            if is_button {
                buttons.insert(num);
                buttons.extend(field.widgets.iter().copied());
            }
        }
    }
    // The appearances, and a push button's image (`/MK /I`), which its
    // appearance draws: the drawing, and what it holds.
    for side in SIDES {
        st.claim_subtree_by_parent(
            ctx,
            side,
            &parents,
            |label, parent| is_ap(label) || (buttons.contains(&parent) && is_button_image(label)),
            rule,
        );
    }
}

fn change_kind_name(change: ObjectChangeType) -> &'static str {
    match change {
        ObjectChangeType::Added => "added",
        ObjectChangeType::Modified => "modified",
        ObjectChangeType::Freed => "freed",
    }
}

/// `listChange`: whether a change of a page's annotation list adds or
/// removes a form widget that no rule permitted (a field shown or hidden).
fn list_touches_widget(st: &State, widgets: &HashSet<u32>, before: &[u32], after: &[u32]) -> bool {
    let removed = before.iter().filter(|n| !after.contains(n));
    let added = after.iter().filter(|n| !before.contains(n));
    removed.chain(added).any(|n| {
        widgets.contains(n)
            && !st
                .findings
                .iter()
                .any(|f| f.object_number == *n && f.verdict == FindingVerdict::Permitted)
    })
}

fn rule_annotation(ctx: &StepContext<'_>, st: &mut State) {
    let rule = "annotation";
    let mut pages: Vec<u32> = Vec::new();
    for p in ctx.input.before.pages.iter().chain(&ctx.input.after.pages) {
        if !pages.contains(p) {
            pages.push(*p);
        }
    }
    let page_set: HashSet<u32> = pages.iter().copied().collect();
    let widgets: HashSet<u32> = ctx
        .before
        .widget_to_field
        .keys()
        .chain(ctx.after.widget_to_field.keys())
        .copied()
        .collect();
    let is_widget = |c: &ObjectChange| {
        if widgets.contains(&c.object_number) {
            return true;
        }
        let side = c.value.new.as_ref().or(c.value.old.as_ref());
        matches!(dict_entries(side).and_then(|e| e.get("Subtype")), Some(PdfValue::Name(v)) if v == "Widget")
    };
    let holders = annots_holders(ctx);
    let mut annots: Vec<&ObjectChange> = Vec::new();
    for c in ctx.changes {
        if c.kind != ObjectChangeKind::Dictionary || ctx.is_identical(c) {
            continue;
        }
        // Listed by a page: in the array inside the page, or in the page's
        // list object.
        let on_page = c.usage.old.iter().chain(&c.usage.new).any(|e| {
            (page_set.contains(&e.parent) && e.label.starts_with("Annots/"))
                || (holder_page(&holders, e.parent).is_some() && is_index_label(&e.label))
        });
        if on_page && !is_widget(c) {
            annots.push(c);
        }
    }
    if !ctx.allows(ModificationLevel::Annotate) {
        for c in annots {
            st.forbidden(
                c,
                rule,
                format!("annotation change under level '{}'", level_name(ctx.level)),
                None,
            );
        }
        return;
    }
    let mut parents: HashSet<u32> = HashSet::new();
    for c in &annots {
        st.claim_all(c, rule, true);
        st.permitted(c, rule, Some(format!("{} annotation", change_kind_name(c.change))));
        parents.insert(c.object_number);
    }
    for p in &pages {
        let Some(c) = ctx.change(*p) else { continue };
        if ctx.is_identical(c) {
            continue;
        }
        // A page whose /Annots a new signature field explains is already judged.
        if st.findings.iter().any(|f| {
            f.object_number == *p
                && f.rule == "signature-field-added"
                && f.verdict == FindingVerdict::Permitted
        }) {
            continue;
        }
        let changed = changed_keys(c.value.old.as_ref(), c.value.new.as_ref());
        if changed.len() == 1 && changed[0] == "Annots" {
            let before = page_list(ctx, &holders, c, Side::Old);
            let after = page_list(ctx, &holders, c, Side::New);
            let list_objects: Vec<&ObjectChange> =
                [before.as_ref().and_then(|l| l.holder), after.as_ref().and_then(|l| l.holder)]
                    .into_iter()
                    .flatten()
                    .collect();
            let (Some(before), Some(after)) = (before, after) else {
                // A list in an object this step left alone, which the
                // transport did not read or which does not read as a list
                // of references: what it held is not among the evidence.
                st.claim_all(c, rule, false);
                for holder in &list_objects {
                    st.claim_all(holder, rule, false);
                }
                st.forbidden(
                    c,
                    rule,
                    "page /Annots points at a list object this revision did not write, and that list could not be read: what it held cannot be compared".into(),
                    None,
                );
                continue;
            };
            if list_touches_widget(st, &widgets, &before.refs, &after.refs) {
                st.forbidden(c, rule, "page /Annots added or removed a form widget".into(), None);
                continue;
            }
            st.claim_all(c, rule, true);
            st.permitted(c, rule, Some("page /Annots changed".into()));
            for holder in &list_objects {
                st.claim_all(holder, rule, true);
                st.permitted(
                    holder,
                    rule,
                    Some("page /Annots, an object of its own, changed".into()),
                );
            }
        }
    }
    // A list that is an object of its own and changes there: the page itself
    // is not written (Acrobat adds a comment this way).
    for (number, _) in &holders {
        let Some(c) = ctx.change(*number) else { continue };
        if c.change != ObjectChangeType::Modified || ctx.is_identical(c) {
            continue;
        }
        if st
            .findings
            .iter()
            .any(|f| f.object_number == *number && f.verdict == FindingVerdict::Permitted)
        {
            continue;
        }
        if list_touches_widget(
            st,
            &widgets,
            &refs_of(c.value.old.as_ref()),
            &refs_of(c.value.new.as_ref()),
        ) {
            st.forbidden(c, rule, "page /Annots added or removed a form widget".into(), None);
            continue;
        }
        st.claim_all(c, rule, true);
        st.permitted(c, rule, Some("page /Annots, an object of its own, changed".into()));
    }
    for side in SIDES {
        st.claim_subtree(
            ctx,
            side,
            &parents,
            |label| is_ap(label) || label == "Popup" || label == "IRT",
            rule,
        );
    }
}

/// `/^\[\d+\]$/`: an array entry's label.
fn is_index_label(label: &str) -> bool {
    label.len() > 2
        && label.starts_with('[')
        && label.ends_with(']')
        && label[1..label.len() - 1].bytes().all(|b| b.is_ascii_digit())
}

fn rule_field_lock(ctx: &StepContext<'_>, st: &mut State) {
    if ctx.locks.is_empty() {
        return;
    }
    let locked_fields: Vec<&RevisionField> = ctx
        .input
        .before
        .fields
        .iter()
        .filter(|f| ctx.locks.iter().any(|l| lock_covers(&l.spec, &f.name)))
        .collect();
    let mut frontier: HashSet<u32> = HashSet::new();
    for f in &locked_fields {
        frontier.insert(f.object_number);
        frontier.extend(f.widgets.iter().copied());
    }
    // Their appearance subtrees (old side: what the lock froze).
    let mut grew = true;
    while grew {
        grew = false;
        for c in ctx.changes {
            if frontier.contains(&c.object_number) {
                continue;
            }
            if !c.usage.old.is_empty() && c.usage.old.iter().all(|e| frontier.contains(&e.parent)) {
                frontier.insert(c.object_number);
                grew = true;
            }
        }
    }
    for c in ctx.changes {
        if !frontier.contains(&c.object_number) || ctx.is_identical(c) {
            continue;
        }
        let field = locked_fields
            .iter()
            .find(|f| f.object_number == c.object_number || f.widgets.contains(&c.object_number));
        if let Some(field) = field.filter(|f| f.object_number == c.object_number) {
            let changed = changed_keys(c.value.old.as_ref(), c.value.new.as_ref());
            if changed.len() == 1
                && changed[0] == "Ff"
                && read_only_only_change(
                    c,
                    ctx.before.field_by_obj.get(c.object_number).and_then(|f| f.flags),
                )
            {
                st.claim_stable(c, "field-lock", true);
                st.permitted(
                    c,
                    "field-lock",
                    Some(format!("ReadOnly set on locked field \"{}\"", field.name)),
                );
                continue;
            }
        }
        st.locked.insert(c.object_number);
        st.forbidden(
            c,
            "field-lock",
            match field {
                Some(f) => format!("field \"{}\" is locked by the signature", f.name),
                None => "part of a locked field changed".into(),
            },
            None,
        );
    }
}

fn rule_unexplained(ctx: &StepContext<'_>, st: &mut State) {
    for c in ctx.changes {
        if c.value.truncated {
            st.findings.push(ChangeFinding {
                rule: "unexplained".into(),
                verdict: FindingVerdict::Incomplete,
                object_number: c.object_number,
                edge: None,
                detail: Some("value too large to inspect".into()),
            });
            continue;
        }
        if c.usage_incomplete {
            st.findings.push(ChangeFinding {
                rule: "unexplained".into(),
                verdict: FindingVerdict::Incomplete,
                object_number: c.object_number,
                edge: None,
                detail: Some(
                    "references could not be resolved within budget (depth, fan-out or total reads)".into(),
                ),
            });
            continue;
        }
        if st.locked.contains(&c.object_number) {
            continue;
        }
        for side in SIDES {
            for e in c.usage.get(side) {
                if !st.is_claimed(c, side, e) {
                    st.forbidden(
                        c,
                        "unexplained",
                        format!(
                            "{} referenced from object {} at {} and no rule explains it",
                            if side == Side::Old { "was" } else { "is" },
                            e.parent,
                            e.label
                        ),
                        Some(e),
                    );
                }
            }
        }
        if c.usage.old.is_empty() && c.usage.new.is_empty() && !st.has_finding_for(c.object_number)
        {
            st.permitted(c, "orphan", Some("referenced from nowhere reachable".into()));
        }
    }
}
