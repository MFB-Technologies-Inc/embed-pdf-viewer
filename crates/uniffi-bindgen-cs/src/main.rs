//! C# binding generator for `epdf-viewer-host`, pinned to the workspace's uniffi.
//!
//! `--library` mode reads the metadata uniffi's proc macros bake into the built
//! library, and that reader has to be the same uniffi version that wrote it.
//! Like `crates/uniffi-bindgen` for Swift and Kotlin, the generator is a
//! workspace binary so it moves with the pin instead of drifting.
fn main() -> anyhow::Result<()> {
    uniffi_bindgen_cs::main()
}
