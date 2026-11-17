//! Re-emits the rpath for this crate's binary and tests.
//!
//! `plugin-conformance` links libembedpdf through `epdf-engine`. Cargo does
//! not propagate `cargo:rustc-link-arg` to a dependent package's bin and test
//! targets, so without this line the server links and then fails at load
//! with `Library not loaded: @rpath/libembedpdf.dylib`.
fn main() {
    build_support::emit_rpath_only();
}
