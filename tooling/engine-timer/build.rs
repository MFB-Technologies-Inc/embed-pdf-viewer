//! The libembedpdf rpath, which Cargo does not carry over from `pdfium-sys`
//! to this package's binary (`build_support::emit_rpath_only`), and a second
//! one at the binary's own directory, so a copy of the binary with the
//! library beside it runs on a machine without a checkout.
//!
//! Also what `engine-timer build` reports: the commit the binary was built
//! from; whether the files it is built from (`BUILT_FROM`) differed from that
//! commit, which ones, and a digest of their diff; the compiler; and the
//! release profile's LTO setting when the environment overrode it.

use std::process::Command;

/// What the binary is built from, relative to the checkout's root. Anything
/// else in the checkout can differ without changing the binary.
const BUILT_FROM: &[&str] = &["Cargo.toml", "Cargo.lock", "crates", "tooling/engine-timer"];

fn run(program: &str, args: &[&str]) -> Option<String> {
    let out = Command::new(program).args(args).output().ok()?;
    // Only the end: a status line starts with a space.
    out.status.success().then(|| String::from_utf8_lossy(&out.stdout).trim_end().to_owned())
}

fn git(args: &[&str]) -> Option<String> {
    run("git", args)
}

/// FNV-1a, 64-bit, as the answer digest: enough to tell two diffs apart.
fn fnv(bytes: &[u8]) -> String {
    let mut hash: u64 = 0xcbf2_9ce4_8422_2325;
    for byte in bytes {
        hash ^= u64::from(*byte);
        hash = hash.wrapping_mul(0x0100_0000_01b3);
    }
    format!("{hash:016x}")
}

fn main() {
    build_support::emit_rpath_only();
    let commit = git(&["rev-parse", "HEAD"]).unwrap_or_else(|| "unknown".into());
    let root = git(&["rev-parse", "--show-toplevel"]);
    let in_root = |args: &[&str]| {
        let root = root.as_deref()?;
        let mut all = vec!["-C", root];
        all.extend_from_slice(args);
        all.push("--");
        all.extend_from_slice(BUILT_FROM);
        git(&all)
    };
    // Tracked files that differ from HEAD, staged or not, and new files.
    let status = in_root(&["status", "--porcelain", "--untracked-files=all"]);
    let (dirty, paths) = match &status {
        None => ("unknown", String::new()),
        Some(status) if status.is_empty() => ("false", String::new()),
        Some(status) => {
            let paths: Vec<&str> =
                status.lines().map(|line| line.get(3..).unwrap_or(line)).collect();
            ("true", paths.join(","))
        }
    };
    let diff = match dirty {
        "true" => {
            in_root(&["diff", "HEAD"]).map_or_else(|| "unknown".into(), |d| fnv(d.as_bytes()))
        }
        _ => String::new(),
    };
    let rustc = std::env::var("RUSTC").unwrap_or_else(|_| "rustc".into());
    let rustc = run(&rustc, &["-V"]).unwrap_or_else(|| "unknown".into());
    println!("cargo:rustc-env=ENGINE_TIMER_COMMIT={commit}");
    println!("cargo:rustc-env=ENGINE_TIMER_DIRTY={dirty}");
    println!("cargo:rustc-env=ENGINE_TIMER_DIRTY_PATHS={paths}");
    println!("cargo:rustc-env=ENGINE_TIMER_DIFF={diff}");
    println!("cargo:rustc-env=ENGINE_TIMER_RUSTC={rustc}");
    let lto = std::env::var("CARGO_PROFILE_RELEASE_LTO").unwrap_or_else(|_| "profile".into());
    println!("cargo:rustc-env=ENGINE_TIMER_LTO={lto}");
    println!(
        "cargo:rustc-env=ENGINE_TIMER_PROFILE={}",
        std::env::var("PROFILE").unwrap_or_default()
    );
    // A path that never exists: Cargo then reruns this script on every
    // build, so an unstaged edit to a crate the binary links can't leave an
    // older stamp in place. It costs a recompile and relink of this crate on
    // every `cargo build`.
    println!("cargo:rerun-if-changed=stamp-on-every-build");
    match std::env::var("CARGO_CFG_TARGET_OS").as_deref() {
        Ok("macos") => println!("cargo:rustc-link-arg=-Wl,-rpath,@executable_path"),
        Ok("linux") => println!("cargo:rustc-link-arg=-Wl,-rpath,$ORIGIN"),
        _ => {}
    }
}
