//! The divergence ledger: every kept Rust divergence from the TypeScript, and
//! where a vector observes it.
//!
//! `divergences.json` lists each divergence once, by `id`, with a `summary`,
//! the `reason` it is kept (citing the source), when it was `decided`, the
//! vector cases that observe it (`observedBy`), and the Rust test that pins
//! it (`pinnedBy`, `<path from the workspace root>::<test fn>`). An entry
//! needs at least one of `observedBy` and `pinnedBy`. An optional
//! `differentialSkip` lists functions whose generated inputs the
//! differential runner skips, `{ "fn", "when" }` (see [`DifferentialSkip`]).
//!
//! An observation names a vector file and case and one of two forms:
//!
//! - `movedCallback`: Rust logs `callback` in trace step `actualStep`, where
//!   the TypeScript logs it in `expectedStep`. Before comparing, the callback
//!   is moved back to where the TypeScript has it.
//! - `valueAt`: at `path` the TypeScript trace holds `expected` and Rust holds
//!   `actual`. Before comparing, the Rust value is replaced with `expected`.
//!   `path` is the trace path a replay failure prints: `$[<step>]` for the
//!   trace entry, then the `.member` and `[index]` segments
//!   [`crate::canonical::diff_entry`] prints after its `$` (so
//!   `step 2: $.settled[0].value` is `$[2].settled[0].value`).
//!
//! Everything else in the trace still compares exactly. An observation fails
//! loudly when it goes stale: when the trace no longer shows the divergence
//! the way the ledger says, or when its case never ran. A bad ledger (an
//! empty `id`, `summary` or `reason`, a repeated id, a malformed
//! observation, an observation of a vector file that does not exist or that
//! it names by an absolute or `..` path, or a
//! `pinnedBy` test that does not exist) fails every replay.

use std::path::Path;

use serde::Deserialize;
use serde_json::Value;

/// One kept divergence.
#[derive(Clone, Debug, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Divergence {
    pub id: String,
    pub summary: String,
    pub reason: String,
    /// When and where it was decided, free text (a date and a commit).
    pub decided: String,
    #[serde(default)]
    pub observed_by: Vec<Observation>,
    #[serde(default)]
    pub pinned_by: Option<String>,
    /// Generated inputs the differential runner
    /// (`tooling/plugin-conformance/src/differential.ts`) does not count as a
    /// mismatch. Each predicate is code in `src/differential/<crate>.ts`,
    /// keyed by this entry's `id`; `when` says in words what it matches.
    #[serde(default)]
    pub differential_skip: Vec<DifferentialSkip>,
}

/// One function the differential runner skips some inputs of.
#[derive(Clone, Debug, PartialEq, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DifferentialSkip {
    /// The function's name in the suite's table.
    #[serde(rename = "fn")]
    pub function: String,
    /// The inputs skipped, in words.
    pub when: String,
}

/// A vector case that observes a divergence, with exactly one form set.
#[derive(Clone, Debug, PartialEq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Observation {
    pub file: String,
    pub case: String,
    #[serde(default)]
    pub moved_callback: Option<MovedCallback>,
    #[serde(default)]
    pub value_at: Option<ValueAt>,
}

/// Rust logs `callback` at step `actual_step`; the TypeScript at
/// `expected_step`.
#[derive(Clone, Debug, PartialEq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct MovedCallback {
    pub callback: Value,
    pub actual_step: usize,
    pub expected_step: usize,
}

/// At `path` the TypeScript holds `expected` and Rust holds `actual`.
#[derive(Clone, Debug, PartialEq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ValueAt {
    pub path: String,
    pub expected: Value,
    pub actual: Value,
}

/// The form an observation takes.
pub enum Form<'a> {
    MovedCallback(&'a MovedCallback),
    ValueAt(&'a ValueAt),
}

impl Observation {
    /// The one form this observation sets, or why it does not set exactly
    /// one.
    pub fn form(&self) -> Result<Form<'_>, String> {
        match (&self.moved_callback, &self.value_at) {
            (Some(moved), None) => Ok(Form::MovedCallback(moved)),
            (None, Some(value)) => Ok(Form::ValueAt(value)),
            (None, None) => Err("sets neither movedCallback nor valueAt".into()),
            (Some(_), Some(_)) => Err("sets both movedCallback and valueAt".into()),
        }
    }
}

/// The workspace root, which `pinnedBy` paths are relative to.
pub fn workspace_root() -> std::path::PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR")).join("../..")
}

/// Every entry of `divergences.json`. Panics on an invalid ledger (see
/// [`validate`]).
pub fn ledger() -> &'static [Divergence] {
    static LIST: std::sync::OnceLock<Vec<Divergence>> = std::sync::OnceLock::new();
    LIST.get_or_init(|| {
        let list = parse(include_str!("../divergences.json"))
            .unwrap_or_else(|why| panic!("divergences.json: {why}"));
        if let Err(why) = validate(&list, &workspace_root()) {
            panic!("divergences.json: {why}");
        }
        list
    })
}

/// Parse ledger text.
pub fn parse(text: &str) -> Result<Vec<Divergence>, String> {
    serde_json::from_str(text).map_err(|e| e.to_string())
}

/// See the module documentation. `root` is the workspace root `pinnedBy`
/// paths resolve against.
pub fn validate(list: &[Divergence], root: &Path) -> Result<(), String> {
    for (index, entry) in list.iter().enumerate() {
        let id = &entry.id;
        for (field, text) in [("id", id), ("summary", &entry.summary), ("reason", &entry.reason)] {
            if text.trim().is_empty() {
                return Err(format!("entry {index} ({id:?}) has an empty {field}"));
            }
        }
        if list[..index].iter().any(|earlier| earlier.id == *id) {
            return Err(format!("{id} is listed twice"));
        }
        if entry.observed_by.is_empty() && entry.pinned_by.is_none() {
            return Err(format!("{id} has neither observedBy nor pinnedBy"));
        }
        for (at, obs) in entry.observed_by.iter().enumerate() {
            let name = format!("{id} / {} / {}", obs.file, obs.case);
            match obs.form().map_err(|why| format!("{name} {why}"))? {
                Form::MovedCallback(moved) => {
                    if moved.actual_step == moved.expected_step {
                        return Err(format!(
                            "{name} moves {} within step {}",
                            moved.callback, moved.actual_step
                        ));
                    }
                }
                Form::ValueAt(value) => {
                    parse_path(&value.path).map_err(|why| format!("{name}: {why}"))?;
                    if crate::canonical::equal(&value.expected, &value.actual) {
                        return Err(format!(
                            "{name} lists the same value on both sides at {}",
                            value.path
                        ));
                    }
                }
            }
            check_vector(&obs.file, root).map_err(|why| format!("{name}: {why}"))?;
            let earlier = list[..index].iter().flat_map(|e| &e.observed_by);
            if entry.observed_by[..at].contains(obs) || earlier.into_iter().any(|o| o == obs) {
                return Err(format!("{name} is listed twice"));
            }
        }
        for skip in &entry.differential_skip {
            for (field, text) in [("fn", &skip.function), ("when", &skip.when)] {
                if text.trim().is_empty() {
                    return Err(format!("{id} has a differentialSkip with an empty {field}"));
                }
            }
        }
        if let Some(pin) = &entry.pinned_by {
            check_pin(pin, root).map_err(|why| format!("{id} pinnedBy {pin}: {why}"))?;
        }
    }
    Ok(())
}

/// `file` names a vector suite: a relative path with no `..` component, and
/// `tooling/plugin-conformance/vectors/<file>.zst` or `<file>` exists under
/// `root`. A path that leaves the vector directory would name a file no
/// receipt reports, so its observation could never apply.
fn check_vector(file: &str, root: &Path) -> Result<(), String> {
    let relative = Path::new(file)
        .components()
        .all(|c| matches!(c, std::path::Component::Normal(_) | std::path::Component::CurDir));
    if file.starts_with('/') || !relative {
        return Err(format!(
            "vector file {file} is not a path relative to tooling/plugin-conformance/vectors"
        ));
    }
    // A wasm32 test runner has no workspace to read; native replays check.
    #[cfg(not(target_arch = "wasm32"))]
    {
        let path = root.join("tooling/plugin-conformance/vectors").join(file);
        let mut compressed = path.clone().into_os_string();
        compressed.push(".zst");
        if !path.exists() && !Path::new(&compressed).exists() {
            return Err(format!(
                "no vector file {file}(.zst) under tooling/plugin-conformance/vectors"
            ));
        }
    }
    #[cfg(target_arch = "wasm32")]
    let _ = (file, root);
    Ok(())
}

/// `pin` is `<path>::<fn name>`, and the file at `root/<path>` defines
/// `fn <fn name>(`.
fn check_pin(pin: &str, root: &Path) -> Result<(), String> {
    let Some((path, name)) = pin.rsplit_once("::") else {
        return Err("is not <path>::<test fn>".into());
    };
    if path.is_empty() || name.is_empty() {
        return Err("is not <path>::<test fn>".into());
    }
    // A wasm32 test runner has no workspace to read; native replays check.
    #[cfg(not(target_arch = "wasm32"))]
    {
        let text = std::fs::read_to_string(root.join(path))
            .map_err(|e| format!("cannot read {path}: {e}"))?;
        if !text.contains(&format!("fn {name}(")) {
            return Err(format!("{path} has no fn {name}("));
        }
    }
    #[cfg(target_arch = "wasm32")]
    let _ = root;
    Ok(())
}

/// The observations of one case of `file`, each with its divergence.
pub fn for_case(file: &str, case: &str) -> Vec<(&'static Divergence, &'static Observation)> {
    observations(ledger(), file, case)
}

/// [`for_case`] over any ledger.
pub fn observations<'a>(
    list: &'a [Divergence],
    file: &str,
    case: &str,
) -> Vec<(&'a Divergence, &'a Observation)> {
    list.iter()
        .flat_map(|d| d.observed_by.iter().map(move |o| (d, o)))
        .filter(|(_, o)| o.file == file && o.case == case)
        .collect()
}

/// Undo the observed divergence in `actual`, or say why the observation is
/// stale.
pub fn apply(obs: &Observation, expected: &[Value], actual: &mut [Value]) -> Result<(), String> {
    match obs.form()? {
        Form::MovedCallback(moved) => apply_moved(moved, expected, actual),
        Form::ValueAt(value) => apply_value_at(value, expected, actual),
    }
}

fn stale(why: impl std::fmt::Display) -> Result<(), String> {
    Err(format!("observation is stale: {why}"))
}

fn callbacks(trace: &[Value], step: usize) -> Option<&Vec<Value>> {
    trace.get(step)?.get("callbacks")?.as_array()
}

/// Move the callback back to where the TypeScript logs it.
fn apply_moved(
    moved: &MovedCallback,
    expected: &[Value],
    actual: &mut [Value],
) -> Result<(), String> {
    if crate::compare_trace(expected, actual).is_none() {
        return stale("the trace no longer differs");
    }
    let found: Vec<usize> = callbacks(actual, moved.actual_step)
        .map(|list| {
            list.iter().enumerate().filter(|(_, c)| **c == moved.callback).map(|(i, _)| i).collect()
        })
        .unwrap_or_default();
    let [from] = found[..] else {
        return stale(format!(
            "{} is logged {} times at step {}",
            moved.callback,
            found.len(),
            moved.actual_step
        ));
    };
    let Some(to) = callbacks(expected, moved.expected_step)
        .and_then(|list| list.iter().position(|c| *c == moved.callback))
    else {
        return stale(format!(
            "the TypeScript does not log {} at step {}",
            moved.callback, moved.expected_step
        ));
    };
    if callbacks(actual, moved.expected_step).is_none() {
        return stale(format!("the Rust trace has no step {}", moved.expected_step));
    }
    let callback = actual[moved.actual_step]["callbacks"]
        .as_array_mut()
        .expect("callbacks checked above")
        .remove(from);
    let target =
        actual[moved.expected_step]["callbacks"].as_array_mut().expect("callbacks checked above");
    target.insert(to.min(target.len()), callback);
    Ok(())
}

/// Replace the Rust value at the path with the TypeScript's.
fn apply_value_at(value: &ValueAt, expected: &[Value], actual: &mut [Value]) -> Result<(), String> {
    let path = &value.path;
    let segments = parse_path(path)?;
    match lookup(expected, &segments) {
        None => return stale(format!("the TypeScript trace has nothing at {path}")),
        Some(ts) if !crate::canonical::equal(&value.expected, ts) => {
            return stale(format!(
                "the TypeScript holds {ts} at {path}, not the listed {}",
                value.expected
            ));
        }
        Some(_) => {}
    }
    let Some(rust) = lookup_mut(actual, &segments) else {
        return stale(format!("the Rust trace has nothing at {path}"));
    };
    if crate::canonical::equal(&value.expected, rust) {
        return stale(format!("the Rust trace already holds the expected {rust} at {path}"));
    }
    if !crate::canonical::equal(&value.actual, rust) {
        return stale(format!(
            "the Rust trace holds {rust} at {path}, not the listed {}",
            value.actual
        ));
    }
    *rust = value.expected.clone();
    Ok(())
}

/// One step of a trace path.
#[derive(Clone, Debug, PartialEq)]
pub enum Segment {
    Index(usize),
    Member(String),
}

/// Parse a trace path (see the module documentation).
/// The first segment is the trace step, so it is always an index. A member
/// name runs to the next `.` or `[`, as [`crate::canonical::diff_entry`]
/// prints it unquoted.
pub fn parse_path(path: &str) -> Result<Vec<Segment>, String> {
    let bad = |why: &str| Err(format!("bad trace path {path:?}: {why}"));
    let Some(mut rest) = path.strip_prefix('$') else {
        return bad("does not start with $");
    };
    let mut segments = Vec::new();
    while !rest.is_empty() {
        if let Some(after) = rest.strip_prefix('[') {
            let Some(close) = after.find(']') else { return bad("unclosed [") };
            let digits = &after[..close];
            if digits.is_empty() || !digits.bytes().all(|b| b.is_ascii_digit()) {
                return bad("an index is not a number");
            }
            let Ok(index) = digits.parse() else { return bad("an index is too large") };
            segments.push(Segment::Index(index));
            rest = &after[close + 1..];
        } else if let Some(after) = rest.strip_prefix('.') {
            let end = after.find(['.', '[']).unwrap_or(after.len());
            if end == 0 {
                return bad("an empty member name");
            }
            segments.push(Segment::Member(after[..end].to_string()));
            rest = &after[end..];
        } else {
            return bad("expected . or [");
        }
    }
    if !matches!(segments.first(), Some(Segment::Index(_))) {
        return bad("does not start with the step, $[<step>]");
    }
    Ok(segments)
}

/// The value at `path` in `trace`, `None` when the trace has nothing there.
pub fn resolve<'a>(trace: &'a [Value], path: &str) -> Result<Option<&'a Value>, String> {
    Ok(lookup(trace, &parse_path(path)?))
}

fn lookup<'a>(trace: &'a [Value], segments: &[Segment]) -> Option<&'a Value> {
    let (Segment::Index(step), rest) = segments.split_first()? else { return None };
    rest.iter().try_fold(trace.get(*step)?, |value, segment| match segment {
        Segment::Index(i) => value.as_array()?.get(*i),
        Segment::Member(key) => value.as_object()?.get(key),
    })
}

fn lookup_mut<'a>(trace: &'a mut [Value], segments: &[Segment]) -> Option<&'a mut Value> {
    let (Segment::Index(step), rest) = segments.split_first()? else { return None };
    rest.iter().try_fold(trace.get_mut(*step)?, |value, segment| match segment {
        Segment::Index(i) => value.as_array_mut()?.get_mut(*i),
        Segment::Member(key) => value.as_object_mut()?.get_mut(key),
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn step(callbacks: Value) -> Value {
        json!({ "settled": [], "callbacks": callbacks, "status": "started" })
    }

    fn moved() -> Observation {
        Observation {
            file: "core/acrojs-host.json".into(),
            case: "c".into(),
            moved_callback: Some(MovedCallback {
                callback: json!({ "fn": "sandbox.dispose", "args": [1] }),
                actual_step: 0,
                expected_step: 1,
            }),
            value_at: None,
        }
    }

    fn value_at(path: &str, expected: Value, actual: Value) -> Observation {
        Observation {
            file: "core/acrojs-host.json".into(),
            case: "c".into(),
            moved_callback: None,
            value_at: Some(ValueAt { path: path.into(), expected, actual }),
        }
    }

    fn divergence(observed_by: Vec<Observation>, pinned_by: Option<&str>) -> Divergence {
        Divergence {
            id: "X1".into(),
            summary: "s".into(),
            reason: "r".into(),
            decided: "2026-11-16".into(),
            observed_by,
            pinned_by: pinned_by.map(str::to_string),
            differential_skip: vec![],
        }
    }

    fn root() -> std::path::PathBuf {
        workspace_root()
    }

    #[test]
    fn a_listed_moved_callback_compares_equal() {
        let dispose = json!({ "fn": "sandbox.dispose", "args": [1] });
        let other = json!({ "fn": "sandbox.run", "args": [1] });
        let expected = [step(json!([other])), step(json!([dispose]))];
        let mut actual = vec![step(json!([other, dispose])), step(json!([]))];
        apply(&moved(), &expected, &mut actual).unwrap();
        assert_eq!(crate::compare_trace(&expected, &actual), None);
    }

    #[test]
    fn a_listed_exception_that_no_longer_differs_fails() {
        let dispose = json!({ "fn": "sandbox.dispose", "args": [1] });
        let expected = [step(json!([])), step(json!([dispose]))];
        let mut actual = expected.to_vec();
        assert_eq!(
            apply(&moved(), &expected, &mut actual).unwrap_err(),
            "observation is stale: the trace no longer differs"
        );
    }

    #[test]
    fn an_exception_cannot_hide_any_other_difference() {
        let dispose = json!({ "fn": "sandbox.dispose", "args": [1] });
        let expected = [step(json!([])), step(json!([dispose]))];
        let mut moved_twice = vec![step(json!([dispose, dispose])), step(json!([]))];
        assert!(apply(&moved(), &expected, &mut moved_twice).unwrap_err().contains("2 times"));
        let mut also_wrong = vec![step(json!([dispose])), step(json!([]))];
        also_wrong[1]["status"] = json!("destroyed");
        apply(&moved(), &expected, &mut also_wrong).unwrap();
        assert!(crate::compare_trace(&expected, &also_wrong).is_some());
    }

    #[test]
    fn a_missing_expected_step_changes_nothing() {
        let dispose = json!({ "fn": "sandbox.dispose", "args": [1] });
        let expected = [step(json!([])), step(json!([dispose]))];
        let mut actual = vec![step(json!([dispose]))];
        let before = actual.clone();
        assert_eq!(
            apply(&moved(), &expected, &mut actual).unwrap_err(),
            "observation is stale: the Rust trace has no step 1"
        );
        assert_eq!(actual, before);
    }

    #[test]
    fn an_entry_within_one_step_is_refused() {
        let mut same = moved();
        same.moved_callback.as_mut().unwrap().expected_step = 0;
        let err = validate(&[divergence(vec![same], None)], &root()).unwrap_err();
        assert!(err.contains("within step 0"), "{err}");
    }

    #[test]
    fn a_duplicate_observation_is_refused() {
        let err = validate(&[divergence(vec![moved(), moved()], None)], &root()).unwrap_err();
        assert!(err.contains("listed twice"), "{err}");
        let other = Observation { case: "d".into(), ..moved() };
        assert_eq!(validate(&[divergence(vec![moved(), other], None)], &root()), Ok(()));
    }

    #[test]
    fn value_at_replaces_the_rust_value() {
        let mut want = step(json!([]));
        want["settled"] = json!([{ "value": { "order": [1, 2] } }]);
        let mut got = want.clone();
        got["settled"][0]["value"]["order"] = json!([2, 1]);
        let expected = [step(json!([])), step(json!([])), want];
        let mut actual = vec![step(json!([])), step(json!([])), got];
        assert!(crate::compare_trace(&expected, &actual).is_some());
        let obs = value_at("$[2].settled[0].value.order", json!([1, 2]), json!([2, 1]));
        apply(&obs, &expected, &mut actual).unwrap();
        assert_eq!(crate::compare_trace(&expected, &actual), None);
    }

    #[test]
    fn value_at_is_stale_when_rust_already_matches() {
        let mut want = step(json!([]));
        want["status"] = json!("ready");
        let expected = [want];
        let mut actual = expected.to_vec();
        let obs = value_at("$[0].status", json!("ready"), json!("started"));
        let err = apply(&obs, &expected, &mut actual).unwrap_err();
        assert!(err.contains("stale"), "{err}");
        assert_eq!(actual, expected);
    }

    #[test]
    fn value_at_is_stale_when_rust_holds_a_third_value() {
        let mut want = step(json!([]));
        want["status"] = json!("ready");
        let expected = [want];
        let mut actual = vec![step(json!([]))];
        actual[0]["status"] = json!("destroyed");
        let before = actual.clone();
        let obs = value_at("$[0].status", json!("ready"), json!("started"));
        let err = apply(&obs, &expected, &mut actual).unwrap_err();
        assert!(err.contains("stale"), "{err}");
        assert!(err.contains("\"destroyed\"") && err.contains("\"started\""), "{err}");
        assert_eq!(actual, before);
    }

    #[test]
    fn value_at_is_stale_when_the_typescript_holds_another_value() {
        let expected = [step(json!([]))];
        let mut actual = vec![step(json!([]))];
        actual[0]["status"] = json!("destroyed");
        let obs = value_at("$[0].status", json!("ready"), json!("destroyed"));
        let err = apply(&obs, &expected, &mut actual).unwrap_err();
        assert!(err.contains("stale") && err.contains("TypeScript"), "{err}");
    }

    #[test]
    fn value_at_on_a_missing_path_is_stale() {
        let expected = [step(json!([]))];
        let mut actual = expected.to_vec();
        let obs = value_at("$[3].status", json!("ready"), json!("started"));
        let err = apply(&obs, &expected, &mut actual).unwrap_err();
        assert!(err.contains("stale") && err.contains("$[3].status"), "{err}");
    }

    #[test]
    fn a_path_resolves_array_index_and_nested_member_segments() {
        let trace = vec![json!({ "a": [{ "b": { "c": 7 } }] }), json!({ "x": [[1, 2], [3]] })];
        assert_eq!(resolve(&trace, "$[0].a[0].b.c").unwrap(), Some(&json!(7)));
        assert_eq!(resolve(&trace, "$[1].x[0][1]").unwrap(), Some(&json!(2)));
        assert_eq!(resolve(&trace, "$[1]").unwrap(), Some(&trace[1]));
        assert_eq!(resolve(&trace, "$[0].a[1]").unwrap(), None);
        assert_eq!(resolve(&trace, "$[0].nope.c").unwrap(), None);
        assert_eq!(resolve(&trace, "$[2]").unwrap(), None);
    }

    #[test]
    fn a_malformed_path_is_refused() {
        for bad in ["", "$", "[0]", "$.a", "$[x]", "$[0", "$[0].", "$[0]..a", "$[0]a"] {
            assert!(parse_path(bad).is_err(), "{bad:?} parsed");
        }
        let err =
            validate(&[divergence(vec![value_at("$.status", json!(1), json!(2))], None)], &root())
                .unwrap_err();
        assert!(err.contains("$.status"), "{err}");
    }

    #[test]
    fn a_path_matches_what_a_replay_failure_prints() {
        let want = json!({ "settled": [{ "value": { "order": [1, 2] } }] });
        let got = json!({ "settled": [{ "value": { "order": [1, 3] } }] });
        let diff =
            crate::compare_trace(std::slice::from_ref(&want), std::slice::from_ref(&got)).unwrap();
        assert_eq!(diff, "step 0: $.settled[0].value.order[1]: expected 2, got 3");
        let trace = vec![got];
        assert_eq!(resolve(&trace, "$[0].settled[0].value.order[1]").unwrap(), Some(&json!(3)));
    }

    #[test]
    fn a_value_at_with_equal_sides_is_refused() {
        let err = validate(
            &[divergence(vec![value_at("$[0].status", json!(1), json!(1))], None)],
            &root(),
        )
        .unwrap_err();
        assert!(err.contains("same value"), "{err}");
    }

    #[test]
    fn an_observation_must_set_exactly_one_form() {
        let neither = Observation { moved_callback: None, ..moved() };
        assert!(
            validate(&[divergence(vec![neither], None)], &root()).unwrap_err().contains("neither")
        );
        let both = Observation {
            value_at: value_at("$[0].status", json!(1), json!(2)).value_at,
            ..moved()
        };
        assert!(validate(&[divergence(vec![both], None)], &root()).unwrap_err().contains("both"));
    }

    #[test]
    fn entry_without_observation_or_pin_is_invalid() {
        let err = validate(&[divergence(vec![], None)], &root()).unwrap_err();
        assert!(err.contains("X1") && err.contains("observedBy"), "{err}");
    }

    #[test]
    fn empty_id_summary_or_reason_is_invalid() {
        let ok = divergence(vec![moved()], None);
        for (field, entry) in [
            ("id", Divergence { id: " ".into(), ..ok.clone() }),
            ("summary", Divergence { summary: "".into(), ..ok.clone() }),
            ("reason", Divergence { reason: "".into(), ..ok.clone() }),
        ] {
            let err = validate(&[entry], &root()).unwrap_err();
            assert!(err.contains(field), "{field}: {err}");
        }
    }

    #[test]
    fn ids_are_unique() {
        let a = divergence(vec![moved()], None);
        let b = divergence(vec![Observation { case: "d".into(), ..moved() }], None);
        let err = validate(&[a, b], &root()).unwrap_err();
        assert!(err.contains("X1") && err.contains("twice"), "{err}");
    }

    #[cfg(not(target_arch = "wasm32"))]
    #[test]
    fn pinned_test_must_exist() {
        let here = "crates/plugin-conformance/src/exceptions.rs";
        let pinned = format!("{here}::pinned_test_must_exist");
        assert_eq!(validate(&[divergence(vec![], Some(&pinned))], &root()), Ok(()));
        let missing = format!("{here}::no_such_test_fn");
        let err = validate(&[divergence(vec![], Some(&missing))], &root()).unwrap_err();
        assert!(err.contains("no_such_test_fn"), "{err}");
        let no_file = "crates/plugin-conformance/src/nope.rs::pinned_test_must_exist";
        let err = validate(&[divergence(vec![], Some(no_file))], &root()).unwrap_err();
        assert!(err.contains("nope.rs"), "{err}");
        let no_name = "crates/plugin-conformance/src/exceptions.rs";
        assert!(validate(&[divergence(vec![], Some(no_name))], &root()).is_err());
    }

    #[cfg(not(target_arch = "wasm32"))]
    #[test]
    fn an_observation_must_name_an_existing_vector_file() {
        let missing = Observation { file: "core/no-such-suite.json".into(), ..moved() };
        let err = validate(&[divergence(vec![missing], None)], &root()).unwrap_err();
        assert!(
            err.contains("X1") && err.contains("core/no-such-suite.json") && err.contains("/ c"),
            "{err}"
        );
    }

    #[test]
    fn an_observation_file_must_stay_inside_the_vector_directory() {
        // Both name a file that exists, by a path no receipt would report.
        let absolute = root().join("tooling/plugin-conformance/vectors/core/acrojs-host.json");
        for file in [
            absolute.to_str().unwrap(),
            "../vectors/core/acrojs-host.json",
            "core/../core/acrojs-host.json",
        ] {
            let obs = Observation { file: file.into(), ..moved() };
            let err = validate(&[divergence(vec![obs], None)], &root()).unwrap_err();
            assert!(err.contains("not a path relative"), "{file}: {err}");
        }
    }

    #[test]
    fn unknown_fields_are_refused() {
        let text = r#"[{ "id": "X", "summary": "s", "reason": "r", "decided": "d",
            "pinnedBy": "a.rs::f", "extra": 1 }]"#;
        assert!(parse(text).unwrap_err().contains("extra"));
        let text = r#"[{ "id": "X", "summary": "s", "reason": "r", "decided": "d",
            "observedBy": [{ "file": "f", "case": "c",
              "valueAt": { "path": "$[0]", "expected": 1, "actual": 2, "why": 3 } }] }]"#;
        assert!(parse(text).unwrap_err().contains("why"));
    }

    /// `differentialSkip` parses with `fn` and `when`, and refuses an empty
    /// one or an unknown member.
    #[test]
    fn a_differential_skip_names_a_function_and_when() {
        let entry = |skip: &str| {
            format!(
                r#"[{{ "id": "X1", "summary": "s", "reason": "r", "decided": "d",
                      "pinnedBy": "crates/plugin-conformance/src/exceptions.rs::ids_are_unique",
                      "differentialSkip": [{skip}] }}]"#
            )
        };
        let list = parse(&entry(r#"{ "fn": "snapToDevice", "when": "dpr is 0" }"#)).unwrap();
        assert_eq!(
            list[0].differential_skip,
            vec![DifferentialSkip { function: "snapToDevice".into(), when: "dpr is 0".into() }]
        );
        validate(&list, &root()).unwrap();
        for (field, skip) in [
            ("fn", r#"{ "fn": " ", "when": "dpr is 0" }"#),
            ("when", r#"{ "fn": "snapToDevice", "when": "" }"#),
        ] {
            let err = validate(&parse(&entry(skip)).unwrap(), &root()).unwrap_err();
            assert!(err.contains("X1") && err.contains(field), "{field}: {err}");
        }
        assert!(parse(&entry(r#"{ "fn": "f", "when": "w", "extra": 1 }"#)).is_err());
    }

    #[test]
    fn the_checked_in_ledger_holds_m13() {
        let m13 = ledger().iter().find(|d| d.id == "M13").expect("M13");
        let [obs] = &m13.observed_by[..] else { panic!("one observation") };
        assert_eq!(
            (obs.file.as_str(), obs.case.as_str()),
            ("core/acrojs-host.json", "dispose-pending-factory")
        );
        assert!(matches!(obs.form(), Ok(Form::MovedCallback(_))));
        assert_eq!(for_case("core/acrojs-host.json", "dispose-pending-factory").len(), 1);
    }
}
