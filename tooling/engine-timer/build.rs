//! The libembedpdf rpath, which Cargo does not carry over from `pdfium-sys`
//! to this package's binary (`build_support::emit_rpath_only`), and a second
//! one at the binary's own directory, so a copy of the binary with the
//! library beside it runs on a machine without a checkout.
//!
//! Also what `engine-timer build` reports: the commit the binary was built
//! from, whether tracked files differed from it, and the release profile's
//! LTO setting when the environment overrode it.

use std::process::Command;

fn git(args: &[&str]) -> Option<String> {
    let out = Command::new("git").args(args).output().ok()?;
    out.status.success().then(|| String::from_utf8_lossy(&out.stdout).trim().to_owned())
}

fn main() {
    build_support::emit_rpath_only();
    let commit = git(&["rev-parse", "HEAD"]).unwrap_or_else(|| "unknown".into());
    let dirty = git(&["status", "--porcelain", "--untracked-files=no"])
        .map_or("unknown", |status| if status.is_empty() { "false" } else { "true" });
    println!("cargo:rustc-env=ENGINE_TIMER_COMMIT={commit}");
    println!("cargo:rustc-env=ENGINE_TIMER_DIRTY={dirty}");
    let lto = std::env::var("CARGO_PROFILE_RELEASE_LTO").unwrap_or_else(|_| "profile".into());
    println!("cargo:rustc-env=ENGINE_TIMER_LTO={lto}");
    println!(
        "cargo:rustc-env=ENGINE_TIMER_PROFILE={}",
        std::env::var("PROFILE").unwrap_or_default()
    );
    println!("cargo:rerun-if-env-changed=CARGO_PROFILE_RELEASE_LTO");
    // A new commit, a checkout or a staged edit rebuilds the stamp.
    for path in ["HEAD", "index"] {
        if let Some(file) = git(&["rev-parse", "--git-path", path]) {
            println!("cargo:rerun-if-changed={file}");
        }
    }
    if let Some(head) = git(&["symbolic-ref", "-q", "HEAD"])
        && let Some(file) = git(&["rev-parse", "--git-path", &head])
    {
        println!("cargo:rerun-if-changed={file}");
    }
    println!("cargo:rerun-if-changed=build.rs");
    println!("cargo:rerun-if-changed=src");
    match std::env::var("CARGO_CFG_TARGET_OS").as_deref() {
        Ok("macos") => println!("cargo:rustc-link-arg=-Wl,-rpath,@executable_path"),
        Ok("linux") => println!("cargo:rustc-link-arg=-Wl,-rpath,$ORIGIN"),
        _ => {}
    }
}
