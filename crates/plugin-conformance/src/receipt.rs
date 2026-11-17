//! Replay receipts: proof of which cases of a vector file actually ran.
//!
//! With `PLUGIN_CONFORMANCE_RECEIPTS=<dir>` set, every [`crate::assert_suite`]
//! call writes one receipt into `<dir>`, named
//! `<file with "/" replaced by "__">.<pid>.<n>.json`, where `n` counts the
//! receipts this process wrote. Two tests that replay the same file therefore
//! leave two receipts, and a reader merges them. A receipt is written even
//! when the suite fails, and records how many cases failed, `only` when
//! `PLUGIN_CONFORMANCE_ONLY` filtered the run, whether member order was
//! compared and whether the allowlist was replaced, the [`RUN_ID_VAR`],
//! [`HEAD_VAR`] and [`DIRTY_VAR`] of the run and the SHA-256 of the vector
//! text it replayed, so a checker can refuse a failed, partial, weakened,
//! mixed or stale replay.

use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicUsize, Ordering};

use serde::{Deserialize, Serialize};

/// The variable naming the run a receipt belongs to; `replay:check` sets it
/// to a fresh id for each cargo run.
pub const RUN_ID_VAR: &str = "PLUGIN_CONFORMANCE_RUN_ID";

/// The variable naming the commit a run replayed; `replay:check` sets it to
/// `git rev-parse HEAD`.
pub const HEAD_VAR: &str = "PLUGIN_CONFORMANCE_HEAD";

/// The variable saying whether the tree a run replayed had uncommitted
/// changes: `1` when it had, `0` when it had none. `replay:check` sets it
/// from `git status --porcelain`.
pub const DIRTY_VAR: &str = "PLUGIN_CONFORMANCE_DIRTY";

/// [`DIRTY_VAR`] read as a flag; `None` when it is unset or neither `0` nor
/// `1`.
pub fn dirty_from_env() -> Option<bool> {
    dirty_flag(std::env::var(DIRTY_VAR).ok().as_deref())
}

/// A [`DIRTY_VAR`] value as a flag: `1` is true, `0` false, anything else
/// (unset included) `None`.
fn dirty_flag(value: Option<&str>) -> Option<bool> {
    match value {
        Some("1") => Some(true),
        Some("0") => Some(false),
        _ => None,
    }
}

/// `bytes` hashed with SHA-256, as lowercase hex.
pub fn sha256_hex(bytes: &[u8]) -> String {
    use sha2::Digest;
    sha2::Sha256::digest(bytes).iter().map(|b| format!("{b:02x}")).collect()
}

/// What one [`crate::assert_suite`] call replayed.
#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Receipt {
    /// The vector file, relative to the vectors root.
    pub file: String,
    /// The [`RUN_ID_VAR`] of the run, when one was set.
    pub run_id: Option<String>,
    /// The [`HEAD_VAR`] of the run, when one was set.
    pub head: Option<String>,
    /// [`DIRTY_VAR`] of the run as a flag, when it was `0` or `1`.
    pub dirty: Option<bool>,
    /// The SHA-256 of the vector text replayed (decompressed), as hex; `None`
    /// for a suite passed in already loaded.
    pub vector_sha256: Option<String>,
    /// The `PLUGIN_CONFORMANCE_ONLY` filter, when one was set.
    pub only: Option<String>,
    /// The names of the cases that ran, in file order.
    pub ran: Vec<String>,
    /// How many of them failed, stale observations included.
    pub failures: usize,
    /// Whether object member order was compared
    /// ([`crate::canonical::member_order_checked`]).
    pub member_order: bool,
    /// Whether `PLUGIN_CONFORMANCE_MEMBER_ORDER_ALLOW` replaced the
    /// checked-in allowlist.
    pub allow_list_overridden: bool,
    /// The ids of the ledger divergences (`divergences.json`) whose
    /// observations were applied.
    pub exceptions: Vec<String>,
    /// The `member-order-allow.json` paths that skipped a member-order
    /// difference, by case.
    pub member_order_allowed: Vec<AllowanceUse>,
}

/// One allowed path that skipped a member-order difference in `case`.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct AllowanceUse {
    pub case: String,
    pub path: String,
}

/// Write `receipt` into `dir` under a name no other receipt of this process
/// uses, and return its path. The text goes to a temporary name first and is
/// renamed into place, so a reader never sees half a receipt.
pub fn write_receipt(dir: &Path, receipt: &Receipt) -> std::io::Result<PathBuf> {
    static NEXT: AtomicUsize = AtomicUsize::new(0);
    let n = NEXT.fetch_add(1, Ordering::Relaxed);
    let name = format!("{}.{}.{n}.json", receipt.file.replace('/', "__"), std::process::id());
    std::fs::create_dir_all(dir)?;
    let path = dir.join(&name);
    let partial = dir.join(format!("{name}.partial"));
    std::fs::write(&partial, serde_json::to_vec_pretty(receipt)?)?;
    std::fs::rename(&partial, &path)?;
    Ok(path)
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::{Value, json};

    /// A fresh directory under the system temp dir, removed on drop.
    struct TempDir(PathBuf);

    impl TempDir {
        fn new(tag: &str) -> Self {
            static NEXT: AtomicUsize = AtomicUsize::new(0);
            let n = NEXT.fetch_add(1, Ordering::Relaxed);
            let dir = std::env::temp_dir()
                .join(format!("plugin-conformance-{tag}-{}-{n}", std::process::id()));
            let _ = std::fs::remove_dir_all(&dir);
            std::fs::create_dir_all(&dir).unwrap();
            TempDir(dir)
        }

        fn entries(&self) -> Vec<PathBuf> {
            let mut list: Vec<PathBuf> =
                std::fs::read_dir(&self.0).unwrap().map(|e| e.unwrap().path()).collect();
            list.sort();
            list
        }
    }

    impl Drop for TempDir {
        fn drop(&mut self) {
            let _ = std::fs::remove_dir_all(&self.0);
        }
    }

    fn receipt(ran: &[&str]) -> Receipt {
        Receipt {
            file: "a/b.json".into(),
            only: None,
            ran: ran.iter().map(|s| s.to_string()).collect(),
            ..Receipt::default()
        }
    }

    fn read(path: &Path) -> Receipt {
        serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap()
    }

    /// A one-case function suite that passes.
    fn one_case_suite() -> Value {
        json!({
            "schemaVersion": crate::SCHEMA_VERSION,
            "kind": "function",
            "cases": [{ "name": "one", "fn": "id", "args": [1], "outcome": { "returned": 1 } }],
        })
    }

    fn functions() -> crate::FunctionTable {
        vec![("id", |args: &[Value]| Ok(args[0].clone()))]
    }

    #[test]
    fn two_writes_for_one_file_keep_both() {
        let dir = TempDir::new("two-writes");
        let first = write_receipt(&dir.0, &receipt(&["case-a"])).unwrap();
        let second = write_receipt(&dir.0, &receipt(&["case-b"])).unwrap();
        assert_ne!(first, second);
        assert_eq!(dir.entries().len(), 2);
        assert_eq!(read(&first).ran, ["case-a"]);
        assert_eq!(read(&second).ran, ["case-b"]);
        let name = first.file_name().unwrap().to_str().unwrap();
        assert!(name.starts_with(&format!("a__b.json.{}.", std::process::id())), "{name}");
        assert!(name.ends_with(".json"), "{name}");
    }

    #[test]
    fn a_receipt_uses_the_wire_names() {
        let dir = TempDir::new("wire");
        let mut r = receipt(&["case-a"]);
        r.run_id = Some("run-1".into());
        r.head = Some("abc".into());
        r.dirty = Some(false);
        r.vector_sha256 = Some("00".into());
        r.only = Some("case".into());
        r.failures = 1;
        r.member_order = true;
        r.exceptions = vec!["M13".into()];
        r.member_order_allowed =
            vec![AllowanceUse { case: "case-a".into(), path: "$.returned".into() }];
        let path = write_receipt(&dir.0, &r).unwrap();
        let value: Value = serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap();
        assert_eq!(
            value,
            json!({
                "file": "a/b.json",
                "runId": "run-1",
                "head": "abc",
                "dirty": false,
                "vectorSha256": "00",
                "only": "case",
                "ran": ["case-a"],
                "failures": 1,
                "memberOrder": true,
                "allowListOverridden": false,
                "exceptions": ["M13"],
                "memberOrderAllowed": [{ "case": "case-a", "path": "$.returned" }],
            })
        );
    }

    #[test]
    fn only_0_and_1_say_whether_the_tree_had_changes() {
        assert_eq!(dirty_flag(Some("1")), Some(true));
        assert_eq!(dirty_flag(Some("0")), Some(false));
        for other in [None, Some(""), Some("true"), Some("yes"), Some(" 1"), Some("01")] {
            assert_eq!(dirty_flag(other), None, "{other:?}");
        }
        assert_eq!(dirty_from_env(), dirty_flag(std::env::var(DIRTY_VAR).ok().as_deref()));
    }

    #[test]
    fn an_unfiltered_receipt_writes_only_as_null() {
        let dir = TempDir::new("null-only");
        let path = write_receipt(&dir.0, &receipt(&["case-a"])).unwrap();
        let value: Value = serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap();
        assert_eq!(value["only"], Value::Null);
    }

    #[test]
    fn no_env_writes_nothing() {
        let dir = TempDir::new("no-env");
        crate::assert_loaded_suite(
            "a/b.json",
            &one_case_suite(),
            &crate::Registry::new(),
            &functions(),
            None,
            None,
        );
        assert!(dir.entries().is_empty());
    }

    #[test]
    fn a_suite_writes_what_it_ran() {
        let dir = TempDir::new("ran");
        crate::assert_loaded_suite(
            "a/b.json",
            &one_case_suite(),
            &crate::Registry::new(),
            &functions(),
            None,
            Some(&dir.0),
        );
        let [path] = &dir.entries()[..] else { panic!("one receipt: {:?}", dir.entries()) };
        let r = read(path);
        assert_eq!((r.file.as_str(), r.only, r.ran), ("a/b.json", None, vec!["one".to_string()]));
    }

    #[test]
    fn a_failing_suite_still_writes_its_receipt() {
        let dir = TempDir::new("failing");
        let mut suite = one_case_suite();
        suite["cases"][0]["outcome"] = json!({ "returned": 2 });
        let run = std::panic::catch_unwind(|| {
            crate::assert_loaded_suite(
                "a/b.json",
                &suite,
                &crate::Registry::new(),
                &functions(),
                None,
                Some(&dir.0),
            )
        });
        assert!(run.is_err(), "the suite differs, so it fails");
        let [path] = &dir.entries()[..] else { panic!("one receipt: {:?}", dir.entries()) };
        let r = read(path);
        assert_eq!((r.ran, r.failures), (vec!["one".to_string()], 1));
    }

    #[test]
    fn a_passing_suite_records_no_failures_and_the_member_order_mode() {
        let dir = TempDir::new("passing");
        crate::assert_loaded_suite(
            "a/b.json",
            &one_case_suite(),
            &crate::Registry::new(),
            &functions(),
            None,
            Some(&dir.0),
        );
        let [path] = &dir.entries()[..] else { panic!("one receipt: {:?}", dir.entries()) };
        let r = read(path);
        assert_eq!(r.failures, 0);
        assert_eq!(r.member_order, crate::canonical::member_order_checked());
        assert_eq!(r.run_id, std::env::var(RUN_ID_VAR).ok());
    }

    /// An allowance for `$.returned` of case `swap`, and one for case `keep`:
    /// a reply in the other order uses its allowance, one in the same order
    /// does not.
    #[test]
    fn a_receipt_lists_the_allowances_that_skipped_a_difference() {
        let suite = json!({
            "schemaVersion": crate::SCHEMA_VERSION,
            "kind": "function",
            "cases": [
                { "name": "swap", "fn": "swap", "args": [],
                  "outcome": { "returned": { "a": 1, "b": 2 } } },
                { "name": "keep", "fn": "keep", "args": [],
                  "outcome": { "returned": { "a": 1, "b": 2 } } },
            ],
        });
        let functions: crate::FunctionTable = vec![
            ("swap", |_: &[Value]| Ok(json!({ "b": 2, "a": 1 }))),
            ("keep", |_: &[Value]| Ok(json!({ "a": 1, "b": 2 }))),
        ];
        let allowed = |file: &str, _case: &str| {
            if file == "a/b.json" { vec!["$.returned".to_string()] } else { vec![] }
        };
        let dir = TempDir::new("allowed");
        crate::replay_suite(
            "a/b.json",
            &suite,
            &crate::Registry::new(),
            &functions,
            None,
            Some(&dir.0),
            crate::exceptions::ledger(),
            &allowed,
            None,
        );
        let [path] = &dir.entries()[..] else { panic!("one receipt: {:?}", dir.entries()) };
        let expected = if crate::canonical::member_order_checked() {
            vec![AllowanceUse { case: "swap".into(), path: "$.returned".into() }]
        } else {
            vec![]
        };
        assert_eq!(read(path).member_order_allowed, expected);
    }

    #[test]
    fn a_receipt_records_the_hash_of_the_vector_text_it_replayed() {
        let dir = TempDir::new("hash");
        let text = one_case_suite().to_string();
        crate::replay_suite(
            "a/b.json",
            &serde_json::from_str(&text).unwrap(),
            &crate::Registry::new(),
            &functions(),
            None,
            Some(&dir.0),
            crate::exceptions::ledger(),
            &|_: &str, _: &str| vec![],
            Some(sha256_hex(text.as_bytes())),
        );
        let [path] = &dir.entries()[..] else { panic!("one receipt: {:?}", dir.entries()) };
        let r = read(path);
        assert_eq!(r.vector_sha256.as_deref(), Some(sha256_hex(text.as_bytes()).as_str()));
        assert_eq!(r.head, std::env::var(HEAD_VAR).ok());
        assert_eq!(r.dirty, dirty_from_env());
        assert_eq!(
            r.allow_list_overridden,
            std::env::var_os(crate::canonical::ALLOW_OVERRIDE_VAR).is_some()
        );
        // The empty input's well-known digest.
        assert_eq!(
            sha256_hex(b""),
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
        );
    }

    #[test]
    fn a_filtered_run_records_its_filter() {
        let dir = TempDir::new("only");
        let mut suite = one_case_suite();
        suite["cases"]
            .as_array_mut()
            .unwrap()
            .push(json!({ "name": "two", "fn": "id", "args": [2], "outcome": { "returned": 2 } }));
        crate::assert_loaded_suite(
            "a/b.json",
            &suite,
            &crate::Registry::new(),
            &functions(),
            Some("two"),
            Some(&dir.0),
        );
        let [path] = &dir.entries()[..] else { panic!("one receipt: {:?}", dir.entries()) };
        let r = read(path);
        assert_eq!((r.only.as_deref(), r.ran), (Some("two"), vec!["two".to_string()]));
    }
}
