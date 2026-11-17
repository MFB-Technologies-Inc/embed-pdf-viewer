//! Times the native engine through `Host::call`. See `README.md`.
//!
//! ```text
//! engine-timer check <root>         the answer check against the corpus under <root>
//! engine-timer files <root>         every corpus file the check reads
//! engine-timer build                what this binary was built from, as JSON
//! engine-timer round <fixtures>     one timed round, one JSON line per row
//!               [--scale <n>]       divide every row's call counts by n
//!               [--dump <dir>]      write every row's answers to <dir>, to
//!                                   diff two builds that answer differently
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
            "usage: engine-timer check <root> | files <root> | build | round <fixtures> [--scale <n>] [--dump <dir>]"
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
        ["build"] => {
            println!(
                "{}",
                json!({
                    "commit": env!("ENGINE_TIMER_COMMIT"),
                    "dirty": env!("ENGINE_TIMER_DIRTY"),
                    "profile": env!("ENGINE_TIMER_PROFILE"),
                    "lto": env!("ENGINE_TIMER_LTO"),
                    "target": format!("{}-{}", std::env::consts::ARCH, std::env::consts::OS),
                })
            );
            ExitCode::SUCCESS
        }
        ["files", root] => {
            for file in check::Corpus::new(&PathBuf::from(root)).files() {
                println!("{file}");
            }
            ExitCode::SUCCESS
        }
        ["round", fixtures, rest @ ..] => {
            let mut scale = 1;
            let mut dump = None;
            for option in rest.chunks(2) {
                match option {
                    ["--scale", n] => match n.parse::<usize>() {
                        Ok(n) if n > 0 => scale = n,
                        _ => return usage(),
                    },
                    ["--dump", dir] => dump = Some(PathBuf::from(dir)),
                    _ => return usage(),
                }
            }
            if let Some(dir) = &dump {
                std::fs::create_dir_all(dir).expect("the dump directory");
            }
            rows::run(&PathBuf::from(fixtures), scale, dump.is_some(), |result| {
                if let Some(dir) = &dump {
                    let file = dir.join(format!("{}.txt", result.row.replace('/', "-")));
                    std::fs::write(&file, result.answers.join("\n") + "\n")
                        .unwrap_or_else(|e| panic!("write {}: {e}", file.display()));
                }
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
