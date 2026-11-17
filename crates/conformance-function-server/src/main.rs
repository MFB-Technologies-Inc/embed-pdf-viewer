//! `function-server`: calls into the conformance function tables, for
//! `tooling/plugin-conformance/src/checks/differential.ts`.
//!
//! Each stdin line is one request, `{"table": "geometry", "fn": "...",
//! "args": [...]}`, with canonical JSON arguments. Each gets one stdout line:
//! the outcome a vector records (`{"returned": ...}` or `{"threw": ...}`,
//! from [`plugin_conformance::call_function`], the path replay takes), or
//! `{"error": "..."}` when the request names no table or function, is not a
//! request, or the call panicked. It exits at the end of stdin.
//!
//! A request `{"math": "cos" | "sin" | "atan2", "args": [...]}` instead asks
//! what the platform math library answers, for the ledger's platform math
//! rule (`src/differential/platform-math.ts`). These are probes, not ported
//! functions, so they sit outside every function table: no vector, replay or
//! ledger skip can name them. Arguments and the answer cross as the 16 hex
//! digits of the double's bits (`{"bits": "..."}`), since canonical
//! arguments turn `-0` into `0` and `serde_json` prints NaN and the
//! infinities as `null`.

use std::io::{BufRead, Write};

use plugin_conformance::{FunctionTable, call_function};
use serde_json::{Value, json};

/// The tables by the name a request gives.
fn tables() -> Vec<(&'static str, FunctionTable)> {
    vec![("geometry", epdf_geometry::harness::functions())]
}

/// A double as the 16 lowercase hex digits of its bits.
fn to_bits_hex(x: f64) -> String {
    format!("{:016x}", x.to_bits())
}

fn from_bits_hex(text: &str) -> Option<f64> {
    (text.len() == 16 && text.bytes().all(|b| b.is_ascii_hexdigit()))
        .then(|| u64::from_str_radix(text, 16).ok().map(f64::from_bits))
        .flatten()
}

/// The platform library's answer, called as the ported code calls it:
/// `rad.cos()`, `rad.sin()` and `y.atan2(x)` on `f64`, as
/// `epdf_geometry::rotate` and `angle_of` and `normalize_pdf_quad` do.
fn math(name: &str, args: &[Value]) -> Value {
    let args: Option<Vec<f64>> = args.iter().map(|a| a.as_str().and_then(from_bits_hex)).collect();
    let Some(args) = args else {
        return json!({ "error": "math arguments are 16 hex digits of a double's bits" });
    };
    let answer = match (name, args.as_slice()) {
        ("cos", [x]) => x.cos(),
        ("sin", [x]) => x.sin(),
        ("atan2", [y, x]) => y.atan2(*x),
        ("cos" | "sin" | "atan2", _) => {
            return json!({ "error": format!("{name} takes {} arguments", if name == "atan2" { 2 } else { 1 }) });
        }
        _ => return json!({ "error": format!("no math function \"{name}\"") }),
    };
    json!({ "bits": to_bits_hex(answer) })
}

fn answer(tables: &[(&str, FunctionTable)], line: &str) -> Value {
    let request: Value = match serde_json::from_str(line) {
        Ok(request) => request,
        Err(e) => return json!({ "error": format!("not JSON: {e}") }),
    };
    if let (Some(name), Some(args)) = (request["math"].as_str(), request["args"].as_array()) {
        return math(name, args);
    }
    let (Some(table), Some(fn_name), Some(args)) =
        (request["table"].as_str(), request["fn"].as_str(), request["args"].as_array())
    else {
        return json!({ "error": "a request is {\"table\", \"fn\", \"args\": [...]} or {\"math\", \"args\": [...]}" });
    };
    let Some((_, functions)) = tables.iter().find(|(name, _)| *name == table) else {
        return json!({ "error": format!("no table \"{table}\"") });
    };
    call_function(functions, fn_name, args).unwrap_or_else(|why| json!({ "error": why }))
}

fn main() {
    let tables = tables();
    let stdout = std::io::stdout();
    let mut out = stdout.lock();
    for line in std::io::stdin().lock().lines() {
        let line = line.expect("read stdin");
        if line.trim().is_empty() {
            continue;
        }
        let reply = answer(&tables, &line);
        // One line per request, flushed: the driver waits on each reply.
        writeln!(out, "{reply}").and_then(|()| out.flush()).expect("write stdout");
    }
}

#[cfg(test)]
mod tests {
    use super::{from_bits_hex, to_bits_hex};

    #[test]
    fn bits_cross_exactly() {
        for x in [
            0.960170286650366,
            #[allow(clippy::approx_constant)]
            1.5707963267948966,
            -0.0,
            0.0,
            5e-324,
            -1e-310,
            f64::NAN,
            f64::INFINITY,
            f64::NEG_INFINITY,
        ] {
            assert_eq!(
                from_bits_hex(&to_bits_hex(x)).map(f64::to_bits),
                Some(x.to_bits()),
                "{x:e}"
            );
        }
        assert_eq!(from_bits_hex("+00000000000000a"), None);
        assert_eq!(from_bits_hex("0"), None);
    }
}
