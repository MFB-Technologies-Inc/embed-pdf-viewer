//! zod 4 refuses an infinite number where zod 3 took one (7a878cf7f, #1151).
//! A request reaches the Rust host as JSON, which has no infinite number:
//! `JSON.stringify` writes `Infinity` as `null`, and a literal past the
//! largest double is refused before any op reads it. So no infinite number
//! reaches a Rust schema check, and the request is refused whole, as
//! `WireFormat`.

use epdf_engine::host::Host;
use serde_json::Value;

#[test]
fn a_number_past_the_largest_double_is_refused_before_any_op_reads_it() {
    let mut host = Host::new();
    let open = host.call_filled(
        r#"{"kind":"open.fatMem","effect":"open","jobId":1,"docId":"d","password":null}"#,
        vec![test_support::fixture(test_support::REPORT)],
    );
    assert!(!open.json.contains("\"error\""), "{}", open.json);
    for (job, width) in [(2, "1e999"), (3, "-1e999")] {
        let request = format!(
            r#"{{"kind":"document.apply","effect":"write","jobId":{job},"docId":"d","opId":"op{job}","change":{{"ops":[{{"type":"annotations.create","page":{{"kind":"objectNumber","objectNumber":3}},"data":{{"subtype":"square","rect":{{"x":10,"y":10,"width":{width},"height":10}}}}}}]}}}}"#
        );
        let answer: Value =
            serde_json::from_str(&host.call_filled(&request, Vec::new()).json).unwrap();
        assert_eq!(answer["error"]["code"], "WireFormat", "{width}: {answer}");
    }
}
