//! Comparing canonical JSON (`tooling/plugin-conformance/src/canonical.ts`).
//!
//! Array order is compared. Object member order is compared too, except at
//! the paths `member-order-allow.json` names (see [`member_order_checked`]).
//! Numbers compare as
//! `f64`. Two integers below 2^53 in magnitude compare exactly. Anything else
//! compares with a relative tolerance of one part in 10^12. The tolerance is
//! there for transcendental functions: V8's `Math.sin` and the platform libm
//! may differ in the last bit, and that is not a difference in behaviour. It
//! is far below anything a coordinate or a zoom could carry, but not below an
//! epoch in milliseconds (about 2.5e12 scales it to 2.5 ms), which is why
//! integers are exempt: a timestamp off by one millisecond is a real
//! difference.

use std::collections::BTreeMap;

use serde_json::Value;

use crate::exceptions::Divergence;

pub const RELATIVE_TOLERANCE: f64 = 1e-12;

/// 2^53: every integer below this in magnitude is exact in an `f64`, as in
/// JavaScript's `Number.isSafeInteger`.
const SAFE_INTEGER_BOUND: f64 = 9_007_199_254_740_992.0;

fn is_safe_integer(x: f64) -> bool {
    x.fract() == 0.0 && x.abs() < SAFE_INTEGER_BOUND
}

pub fn numbers_equal(a: f64, b: f64) -> bool {
    if a == b {
        return true;
    }
    // Counts, ids and epoch milliseconds: no rounding can make two of these
    // differ, so any difference is behaviour.
    if is_safe_integer(a) && is_safe_integer(b) {
        return false;
    }
    let scale = a.abs().max(b.abs()).max(1.0);
    (a - b).abs() <= RELATIVE_TOLERANCE * scale
}

/// Whether object member order is compared: on unless
/// `PLUGIN_CONFORMANCE_MEMBER_ORDER=0`. Order is behaviour (a caller walking
/// `Object.keys` sees it). Where the TypeScript passes a caller's object
/// through, or one type gets its order from whichever function built it, a
/// typed Rust value can't follow; `member-order-allow.json` names those paths
/// (see [`allow_member_order`]).
pub fn member_order_checked() -> bool {
    static CHECKED: std::sync::OnceLock<bool> = std::sync::OnceLock::new();
    *CHECKED.get_or_init(|| {
        std::env::var_os("PLUGIN_CONFORMANCE_MEMBER_ORDER").is_none_or(|v| v != "0")
    })
}

thread_local! {
    /// The paths whose member order the current case doesn't compare.
    static ALLOWED: std::cell::RefCell<Vec<String>> = const { std::cell::RefCell::new(Vec::new()) };
    /// The paths of `ALLOWED` at which a member-order difference was skipped.
    static USED: std::cell::RefCell<Vec<String>> = const { std::cell::RefCell::new(Vec::new()) };
}

/// One `member-order-allow.json` entry: a trace path, with its array indexes
/// written `[]`, and the `divergences.json` id that says why its member order
/// is not compared.
#[derive(Clone, Debug, serde::Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Allowance {
    pub path: String,
    pub divergence: String,
}

/// `member-order-allow.json`: `{ file: { case: [Allowance, ...] } }`.
pub type AllowList = BTreeMap<String, BTreeMap<String, Vec<Allowance>>>;

/// Parse allow-list text. Every entry must be an [`Allowance`] whose
/// `divergence` names an entry of `ledger`, and whose path starts at `$`.
pub fn parse_allow_list(text: &str, ledger: &[Divergence]) -> Result<AllowList, String> {
    let list: AllowList = serde_json::from_str(text).map_err(|e| e.to_string())?;
    for (file, cases) in &list {
        for (case, entries) in cases {
            for Allowance { path, divergence } in entries {
                if !path.starts_with('$') {
                    return Err(format!("{file} / {case} / {path} is not a path from `$`"));
                }
                if !ledger.iter().any(|d| d.id == *divergence) {
                    return Err(format!(
                        "{file} / {case} / {path} cites {divergence}, which divergences.json \
                         does not list"
                    ));
                }
            }
        }
    }
    Ok(list)
}

/// The variable `pnpm run member-order:allow` sets for its cargo runs: a
/// JSON file `{ file: { case: [path, ...] } }` whose paths replace the
/// checked-in allow-list for that run. `replay:check` removes it.
pub const ALLOW_OVERRIDE_VAR: &str = "PLUGIN_CONFORMANCE_MEMBER_ORDER_ALLOW";

/// The allow-list entries for one case of `file`, from
/// `member-order-allow.json`, each path with its array indexes written `[]`.
/// Panics on an invalid list (see [`parse_allow_list`]), checked against the
/// ledger, even when [`ALLOW_OVERRIDE_VAR`] replaces it.
pub fn allowed_member_order(file: &str, case: &str) -> Vec<String> {
    type Paths = BTreeMap<String, BTreeMap<String, Vec<String>>>;
    static LIST: std::sync::OnceLock<Paths> = std::sync::OnceLock::new();
    let list = LIST.get_or_init(|| {
        let checked_in = parse_allow_list(
            include_str!("../member-order-allow.json"),
            crate::exceptions::ledger(),
        )
        .unwrap_or_else(|why| panic!("member-order-allow.json: {why}"));
        if let Some(over) = std::env::var_os(ALLOW_OVERRIDE_VAR) {
            let text = std::fs::read_to_string(&over)
                .unwrap_or_else(|e| panic!("{ALLOW_OVERRIDE_VAR}={over:?}: {e}"));
            return serde_json::from_str(&text)
                .unwrap_or_else(|e| panic!("{ALLOW_OVERRIDE_VAR}={over:?}: {e}"));
        }
        checked_in
            .into_iter()
            .map(|(file, cases)| {
                let cases = cases
                    .into_iter()
                    .map(|(case, entries)| (case, entries.into_iter().map(|a| a.path).collect()))
                    .collect();
                (file, cases)
            })
            .collect()
    });
    list.get(file).and_then(|cases| cases.get(case)).cloned().unwrap_or_default()
}

/// What [`allow_member_order`] returns: the allowance lasts until it drops.
pub struct MemberOrderAllowance(());

impl MemberOrderAllowance {
    /// The allowed paths at which the comparison skipped a member-order
    /// difference so far, each once, in the order first skipped. An allowed
    /// path missing here had no effect.
    pub fn used(&self) -> Vec<String> {
        USED.with(|used| used.borrow().clone())
    }
}

impl Drop for MemberOrderAllowance {
    fn drop(&mut self) {
        ALLOWED.with(|allowed| allowed.borrow_mut().clear());
        USED.with(|used| used.borrow_mut().clear());
    }
}

/// Skip the member-order comparison at `paths` until the returned guard
/// drops.
pub fn allow_member_order(paths: Vec<String>) -> MemberOrderAllowance {
    ALLOWED.with(|allowed| *allowed.borrow_mut() = paths);
    USED.with(|used| used.borrow_mut().clear());
    MemberOrderAllowance(())
}

/// `path` with every array index written `[]`, as the allow-list names it.
pub fn path_pattern(path: &str) -> String {
    let mut out = String::with_capacity(path.len());
    let mut chars = path.chars().peekable();
    while let Some(c) = chars.next() {
        out.push(c);
        if c == '[' && chars.peek().is_some_and(char::is_ascii_digit) {
            while chars.peek().is_some_and(char::is_ascii_digit) {
                chars.next();
            }
        }
    }
    out
}

/// Whether the member order at `path` is allowed to differ; called only
/// where it does, so an allowed path is recorded as used.
fn order_allowed(path: &str) -> bool {
    let pattern = ALLOWED.with(|allowed| {
        let allowed = allowed.borrow();
        let pattern = path_pattern(path);
        (!allowed.is_empty() && allowed.contains(&pattern)).then_some(pattern)
    });
    let Some(pattern) = pattern else { return false };
    USED.with(|used| {
        let mut used = used.borrow_mut();
        if !used.contains(&pattern) {
            used.push(pattern);
        }
    });
    true
}

/// `None` when equal, else the path to the first difference and both sides.
pub fn diff(expected: &Value, actual: &Value) -> Option<String> {
    diff_at("$", expected, actual, member_order_checked())
}

/// [`diff`] for a trace entry: the entry's own members are the runner's
/// envelope (`outcome`, `settled`, ...), which each runner writes in its own
/// order, so only the values inside them can have their member order compared.
pub fn diff_entry(expected: &Value, actual: &Value) -> Option<String> {
    diff_members("$", expected, actual, false, member_order_checked())
}

pub fn equal(expected: &Value, actual: &Value) -> bool {
    diff(expected, actual).is_none()
}

fn short(value: &Value) -> String {
    let text = value.to_string();
    if text.len() > 400 {
        format!("{}…", &text[..text.char_indices().nth(400).map_or(text.len(), |(i, _)| i)])
    } else {
        text
    }
}

fn diff_at(path: &str, expected: &Value, actual: &Value, ordered: bool) -> Option<String> {
    diff_members(path, expected, actual, ordered, ordered)
}

/// `ordered` compares this object's member order; `nested` is the flag every
/// value below it gets.
fn diff_members(
    path: &str,
    expected: &Value,
    actual: &Value,
    ordered: bool,
    nested: bool,
) -> Option<String> {
    match (expected, actual) {
        (Value::Number(a), Value::Number(b)) => {
            let (a, b) = (a.as_f64().unwrap_or(f64::NAN), b.as_f64().unwrap_or(f64::NAN));
            if numbers_equal(a, b) { None } else { Some(format!("{path}: expected {a}, got {b}")) }
        }
        (Value::Array(a), Value::Array(b)) => {
            for (i, (x, y)) in a.iter().zip(b.iter()).enumerate() {
                if let Some(d) = diff_at(&format!("{path}[{i}]"), x, y, nested) {
                    return Some(d);
                }
            }
            if a.len() != b.len() {
                let extra = if a.len() > b.len() { &a[b.len()] } else { &b[a.len()] };
                return Some(format!(
                    "{path}: expected {} items, got {} (first unmatched: {})",
                    a.len(),
                    b.len(),
                    short(extra)
                ));
            }
            None
        }
        (Value::Object(a), Value::Object(b)) => {
            for (key, x) in a {
                match b.get(key) {
                    Some(y) => {
                        if let Some(d) = diff_at(&format!("{path}.{key}"), x, y, nested) {
                            return Some(d);
                        }
                    }
                    None => return Some(format!("{path}.{key}: missing (expected {})", short(x))),
                }
            }
            for (key, y) in b {
                if !a.contains_key(key) {
                    return Some(format!("{path}.{key}: unexpected {}", short(y)));
                }
            }
            // Same members, same values: the order is still behaviour, since
            // a caller that walks `Object.keys` sees it.
            if ordered && !a.keys().eq(b.keys()) && !order_allowed(path) {
                let order = |m: &serde_json::Map<String, Value>| {
                    m.keys().cloned().collect::<Vec<_>>().join(", ")
                };
                return Some(format!(
                    "{path}: member order: expected [{}], got [{}]",
                    order(a),
                    order(b)
                ));
            }
            None
        }
        _ if expected == actual => None,
        _ => Some(format!("{path}: expected {}, got {}", short(expected), short(actual))),
    }
}

/// A text key for matching arguments against a table: numbers normalized so
/// `1` and `1.0` agree, members sorted.
pub fn key(value: &Value) -> String {
    fn norm(value: &Value) -> Value {
        match value {
            Value::Number(n) => {
                let f = n.as_f64().unwrap_or(0.0);
                if f.fract() == 0.0 && f.abs() < 9.0e15 {
                    Value::from(f as i64)
                } else {
                    Value::from(f)
                }
            }
            Value::Array(items) => Value::Array(items.iter().map(norm).collect()),
            // Sorted: with `preserve_order` a map prints in insertion order,
            // and two argument objects that differ only in member order are
            // the same arguments.
            Value::Object(map) => {
                let mut entries: Vec<_> = map.iter().map(|(k, v)| (k.clone(), norm(v))).collect();
                entries.sort_by(|a, b| a.0.cmp(&b.0));
                Value::Object(entries.into_iter().collect())
            }
            other => other.clone(),
        }
    }
    norm(value).to_string()
}

#[cfg(test)]
mod tests {
    use super::{allow_member_order, diff_members, numbers_equal, parse_allow_list};
    use crate::exceptions::Divergence;
    use serde_json::json;

    #[test]
    fn member_order_is_compared_below_the_envelope_when_asked() {
        let (a, b) =
            (json!({"x": 1, "y": {"p": 1, "q": 2}}), json!({"y": {"q": 2, "p": 1}, "x": 1}));
        assert_eq!(diff_members("$", &a, &b, false, false), None);
        assert_eq!(
            diff_members("$", &a, &b, false, true).as_deref(),
            Some("$.y: member order: expected [p, q], got [q, p]")
        );
        let c = json!({"y": {"p": 1, "q": 2}, "x": 1});
        assert_eq!(diff_members("$", &a, &c, false, true), None);
        assert_eq!(
            diff_members("$", &a, &c, true, true).as_deref(),
            Some("$: member order: expected [x, y], got [y, x]")
        );
    }

    /// How every `MO-` ledger entry is applied: an allowed path skips the
    /// member-order comparison at that object only.
    #[test]
    fn an_allowed_path_skips_the_member_order_comparison() {
        let a = json!({"y": {"p": 1, "q": 2}, "z": [{"p": 1, "q": 2}]});
        let b = json!({"y": {"q": 2, "p": 1}, "z": [{"q": 2, "p": 1}]});
        {
            let allowed = allow_member_order(vec!["$.y".into(), "$.z[]".into()]);
            assert_eq!(diff_members("$", &a, &b, false, true), None);
            assert_eq!(allowed.used(), ["$.y", "$.z[]"]);
        }
        let allowed = allow_member_order(vec!["$.y".into()]);
        assert_eq!(
            diff_members("$", &a, &b, false, true).as_deref(),
            Some("$.z[0]: member order: expected [p, q], got [q, p]")
        );
        assert_eq!(allowed.used(), ["$.y"]);
    }

    /// An allowed path whose member order agrees is not used: that
    /// allowance has no effect, and `replay:check` reports it.
    #[test]
    fn an_allowed_path_whose_order_agrees_is_not_used() {
        let a = json!({"y": {"p": 1, "q": 2}});
        let allowed = allow_member_order(vec!["$.y".into(), "$.gone".into()]);
        assert_eq!(diff_members("$", &a, &a.clone(), false, true), None);
        assert!(allowed.used().is_empty());
        drop(allowed);
        let b = json!({"y": {"q": 2, "p": 1}});
        let allowed = allow_member_order(vec!["$.y".into()]);
        assert_eq!(diff_members("$", &a, &b, false, true), None);
        assert_eq!(diff_members("$", &a, &b, false, true), None);
        assert_eq!(allowed.used(), ["$.y"], "each path once");
    }

    fn ledger_with(id: &str) -> Vec<Divergence> {
        crate::exceptions::parse(
            &json!([{
                "id": id,
                "summary": "s",
                "reason": "r",
                "decided": "d",
                "pinnedBy": "crates/plugin-conformance/src/canonical.rs::x"
            }])
            .to_string(),
        )
        .expect("a ledger")
    }

    #[test]
    fn an_allowance_cites_a_ledger_id() {
        let text = json!({"f.json": {"c": [{"path": "$.a", "divergence": "MO-x"}]}}).to_string();
        let list = parse_allow_list(&text, &ledger_with("MO-x")).expect("a cited id parses");
        assert_eq!(list["f.json"]["c"][0].path, "$.a");
        assert_eq!(
            parse_allow_list(&text, &ledger_with("MO-y")).expect_err("an unknown id"),
            "f.json / c / $.a cites MO-x, which divergences.json does not list"
        );
    }

    #[test]
    fn a_bare_path_allowance_is_rejected() {
        let text = json!({"f.json": {"c": ["$.a"]}}).to_string();
        let why = parse_allow_list(&text, &ledger_with("MO-x")).expect_err("a bare path");
        assert!(why.contains("expected struct"), "{why}");
        let text = json!({"f.json": {"c": [{"path": "$.a"}]}}).to_string();
        let why = parse_allow_list(&text, &ledger_with("MO-x")).expect_err("no divergence");
        assert!(why.contains("missing field `divergence`"), "{why}");
        let text = json!({"f.json": {"c": [{"path": "a", "divergence": "MO-x"}]}}).to_string();
        assert_eq!(
            parse_allow_list(&text, &ledger_with("MO-x")).expect_err("not a trace path"),
            "f.json / c / a is not a path from `$`"
        );
    }

    #[test]
    fn the_checked_in_allow_list_cites_the_ledger() {
        let list = parse_allow_list(
            include_str!("../member-order-allow.json"),
            crate::exceptions::ledger(),
        )
        .unwrap_or_else(|why| panic!("member-order-allow.json: {why}"));
        assert!(!list.is_empty());
    }

    #[test]
    fn integers_below_2_pow_53_compare_exactly() {
        // An epoch in milliseconds off by 2 ms used to pass the relative
        // tolerance (2.5e12 * 1e-12 = 2.5).
        assert!(!numbers_equal(2_559_009_906_002.0, 2_559_009_906_000.0));
        assert!(!numbers_equal(1_700_000_000_001.0, 1_700_000_000_000.0));
        assert!(numbers_equal(2_559_009_906_000.0, 2_559_009_906_000.0));
        assert!(!numbers_equal(-3.0, 3.0));
    }

    #[test]
    fn fractions_keep_the_relative_tolerance() {
        let x = 0.1_f64.sin();
        assert!(numbers_equal(x, f64::from_bits(x.to_bits() + 1)));
        assert!(numbers_equal(1.0, 1.0 + f64::EPSILON));
        assert!(!numbers_equal(1.0, 1.000_000_001));
    }
}
