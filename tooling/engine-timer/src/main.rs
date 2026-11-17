//! Times the native engine through `Host::call`. See `README.md`.
//!
//! ```text
//! engine-timer check <root>         the answer check against the corpus under <root>
//! engine-timer files <root>         every corpus file the check reads
//! engine-timer round <fixtures>     one timed round, one JSON line per row
//!               [--scale <n>]       divide every row's call counts by n
//! ```

mod check;
mod rows;
mod wire;

use std::path::PathBuf;
use std::process::ExitCode;

use serde_json::json;

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let usage = || {
        eprintln!(
            "usage: engine-timer check <root> | files <root> | round <fixtures> [--scale <n>]"
        );
        ExitCode::from(2)
    };
    match args.iter().map(String::as_str).collect::<Vec<_>>().as_slice() {
        ["check", root] => {
            let failures = check::run(&check::Corpus::new(&PathBuf::from(root)));
            if failures.is_empty() {
                println!("check: every answer agrees with the vectors");
                ExitCode::SUCCESS
            } else {
                for failure in &failures {
                    eprintln!("  {failure}");
                }
                eprintln!("check: {} answer(s) differ from the vectors", failures.len());
                ExitCode::FAILURE
            }
        }
        ["files", root] => {
            for file in check::Corpus::new(&PathBuf::from(root)).files() {
                println!("{file}");
            }
            ExitCode::SUCCESS
        }
        ["round", fixtures, rest @ ..] => {
            let scale = match rest {
                [] => 1,
                ["--scale", n] => match n.parse::<usize>() {
                    Ok(n) if n > 0 => n,
                    _ => return usage(),
                },
                _ => return usage(),
            };
            rows::run(&PathBuf::from(fixtures), scale, |result| {
                let ns: Vec<u128> = result.samples.iter().map(|d| d.as_nanos()).collect();
                println!(
                    "{}",
                    json!({ "row": result.row, "digest": result.digest.hex(), "samplesNs": ns })
                );
            });
            ExitCode::SUCCESS
        }
        _ => usage(),
    }
}
