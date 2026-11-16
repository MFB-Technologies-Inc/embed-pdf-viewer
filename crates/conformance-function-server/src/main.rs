//! `function-server`: calls into the conformance function tables, for
//! `tooling/plugin-conformance/src/differential.ts`.
//!
//! Each stdin line is one request, `{"table": "geometry", "fn": "...",
//! "args": [...]}`, with canonical JSON arguments. Each gets one stdout line:
//! the outcome a vector records (`{"returned": ...}` or `{"threw": ...}`,
//! from [`plugin_conformance::call_function`], the path replay takes), or
//! `{"error": "..."}` when the request names no table or function, is not a
//! request, or the call panicked. It exits at the end of stdin.

use std::io::{BufRead, Write};

use plugin_conformance::{FunctionTable, call_function};
use serde_json::{Value, json};

/// The tables by the name a request gives.
fn tables() -> Vec<(&'static str, FunctionTable)> {
    vec![("geometry", epdf_geometry::harness::functions())]
}

fn answer(tables: &[(&str, FunctionTable)], line: &str) -> Value {
    let request: Value = match serde_json::from_str(line) {
        Ok(request) => request,
        Err(e) => return json!({ "error": format!("not JSON: {e}") }),
    };
    let (Some(table), Some(fn_name), Some(args)) =
        (request["table"].as_str(), request["fn"].as_str(), request["args"].as_array())
    else {
        return json!({ "error": "a request is {\"table\", \"fn\", \"args\": [...]}" });
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
