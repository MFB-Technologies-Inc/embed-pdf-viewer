//! The built `function-server` binary, driven over stdin and stdout as
//! `tooling/plugin-conformance/src/differential.ts` drives it.

use std::io::{BufRead, BufReader, Write};
use std::process::{Command, Stdio};

use plugin_conformance::{canonical, load};
use serde_json::{Value, json};

/// Send `requests`, one per line, and read one reply line for each.
fn exchange(requests: &[Value]) -> Vec<Value> {
    let mut child = Command::new(env!("CARGO_BIN_EXE_function-server"))
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .spawn()
        .expect("spawn function-server");
    let mut stdin = child.stdin.take().unwrap();
    for request in requests {
        writeln!(stdin, "{request}").unwrap();
    }
    drop(stdin);
    let replies: Vec<Value> = BufReader::new(child.stdout.take().unwrap())
        .lines()
        .map(|line| serde_json::from_str(&line.unwrap()).expect("a reply is JSON"))
        .collect();
    assert!(child.wait().unwrap().success(), "function-server exits 0 at end of input");
    replies
}

fn case(file: &str, name: &str) -> Value {
    load(file)["cases"]
        .as_array()
        .unwrap()
        .iter()
        .find(|case| case["name"] == name)
        .unwrap_or_else(|| panic!("{file} has no case {name}"))
        .clone()
}

#[test]
fn function_server_answers_a_known_case() {
    let cases = [
        case("core/geometry-basics.json", "intersectRects/nan-in-a"),
        case("core/geometry-basics.json", "snapToDevice/negative-tie-rounds-toward-plus-infinity"),
        // A throw, so both envelopes are exercised.
        case("core/geometry-affine.json", "invert/singular-zero"),
    ];
    let requests: Vec<Value> = cases
        .iter()
        .map(|case| json!({ "table": "geometry", "fn": case["fn"], "args": case["args"] }))
        .collect();
    let replies = exchange(&requests);
    assert_eq!(replies.len(), cases.len());
    for (case, reply) in cases.iter().zip(&replies) {
        assert_eq!(canonical::diff(&case["outcome"], reply), None, "{}: {reply}", case["name"]);
    }
    assert!(cases[2]["outcome"].get("threw").is_some(), "the third case throws");
}

#[test]
fn function_server_reports_what_it_cannot_call() {
    let replies = exchange(&[
        json!({ "table": "nope", "fn": "identity", "args": [] }),
        json!({ "table": "geometry", "fn": "nope", "args": [] }),
        json!("not a request"),
        json!({ "table": "geometry", "fn": "identity", "args": [] }),
    ]);
    let errors: Vec<&str> = replies.iter().map(|r| r["error"].as_str().unwrap_or("")).collect();
    assert!(errors[0].contains("no table \"nope\""), "{errors:?}");
    assert!(errors[1].contains("no Rust function \"nope\""), "{errors:?}");
    assert!(!errors[2].is_empty(), "{errors:?}");
    let identity = json!({ "returned": [1, 0, 0, 1, 0, 0] });
    assert_eq!(canonical::diff(&identity, &replies[3]), None, "{}", replies[3]);
}
