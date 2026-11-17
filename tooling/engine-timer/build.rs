//! The libembedpdf rpath, which Cargo does not carry over from `pdfium-sys`
//! to this package's binary (`build_support::emit_rpath_only`), and a second
//! one at the binary's own directory, so a copy of the binary with the
//! library beside it runs on a machine without a checkout.

fn main() {
    build_support::emit_rpath_only();
    match std::env::var("CARGO_CFG_TARGET_OS").as_deref() {
        Ok("macos") => println!("cargo:rustc-link-arg=-Wl,-rpath,@executable_path"),
        Ok("linux") => println!("cargo:rustc-link-arg=-Wl,-rpath,$ORIGIN"),
        _ => {}
    }
}
