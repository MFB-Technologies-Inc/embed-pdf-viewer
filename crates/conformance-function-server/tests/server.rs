//! The built `function-server` binary, driven over stdin and stdout as
//! `tooling/plugin-conformance/src/checks/differential.ts` drives it.

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

fn bits(x: f64) -> String {
    format!("{:016x}", x.to_bits())
}

fn math(name: &str, args: &[f64]) -> Value {
    json!({ "math": name, "args": args.iter().map(|&a| bits(a)).collect::<Vec<_>>() })
}

#[test]
fn function_server_answers_the_platform_math_library_to_the_bit() {
    // `-0`, subnormals and the non-finite values cross both ways: canonical
    // arguments would turn `-0` into `0`, and `serde_json` prints NaN as null.
    let inputs =
        [-6.0, -0.0, 0.0, 5e-324, -1e-310, 1e300, f64::NAN, f64::INFINITY, f64::NEG_INFINITY];
    let mut requests = Vec::new();
    let mut expected = Vec::new();
    for &x in &inputs {
        requests.push(math("cos", &[x]));
        expected.push(x.cos());
        requests.push(math("sin", &[x]));
        expected.push(x.sin());
        for &y in &inputs {
            requests.push(math("atan2", &[y, x]));
            expected.push(y.atan2(x));
        }
    }
    let replies = exchange(&requests);
    for ((request, reply), want) in requests.iter().zip(&replies).zip(&expected) {
        assert_eq!(reply["bits"].as_str(), Some(bits(*want).as_str()), "{request} -> {reply}");
    }
    // The ledger's recorded answers print as the TypeScript's test expects
    // them (`test/differential.test.ts`), so both ends share one encoding.
    #[allow(clippy::approx_constant)]
    let recorded =
        [(0.960170286650366, "3feeb9b7097822f5"), (1.5707963267948966, "3ff921fb54442d18")];
    for (x, hex) in recorded {
        assert_eq!(bits(x), hex);
    }
    assert_eq!(bits(-0.0), "8000000000000000");
}

#[test]
fn function_server_refuses_a_math_request_it_cannot_answer() {
    let replies = exchange(&[
        math("tan", &[1.0]),
        math("atan2", &[1.0]),
        json!({ "math": "cos", "args": [1.0] }),
        json!({ "math": "cos", "args": ["3ff"] }),
        // A probe is no table function: the tables do not hold it.
        json!({ "table": "geometry", "fn": "cos", "args": [1.0] }),
    ]);
    let errors: Vec<&str> = replies.iter().map(|r| r["error"].as_str().unwrap_or("")).collect();
    assert!(errors[0].contains("no math function \"tan\""), "{errors:?}");
    assert!(errors[1].contains("atan2 takes 2 arguments"), "{errors:?}");
    assert!(errors[2].contains("16 hex digits"), "{errors:?}");
    assert!(errors[3].contains("16 hex digits"), "{errors:?}");
    assert!(errors[4].contains("no Rust function \"cos\""), "{errors:?}");
}
