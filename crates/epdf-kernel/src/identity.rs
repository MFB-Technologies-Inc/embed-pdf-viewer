//! UUIDs share one byte source; hosts may scope deterministic entropy and time.
use std::{cell::RefCell, rc::Rc};
type Bytes = Rc<dyn Fn(&mut [u8; 16])>;
type Now = Rc<dyn Fn() -> f64>;
thread_local! { static SOURCE: RefCell<Option<(Bytes, Now)>> = const { RefCell::new(None) }; }
pub struct UuidSourceGuard(Option<(Bytes, Now)>);
impl Drop for UuidSourceGuard {
    fn drop(&mut self) {
        SOURCE.with(|source| *source.borrow_mut() = self.0.take());
    }
}
/// Install for this runtime thread; nested guards restore the prior provider.
pub fn with_uuid_source(bytes: Bytes, now: Now) -> UuidSourceGuard {
    UuidSourceGuard(SOURCE.with(|source| source.replace(Some((bytes, now)))))
}
/// Draws taken from the `RandomState` fallback, which also numbers them.
static FALLBACK_DRAWS: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(0);
/// The last resort: `RandomState`'s per-process keys, hashed with a counter.
/// The browser build uses it until epdf-web installs its Web Crypto source;
/// a native build only if the OS source fails.
fn fallback_bytes(bytes: &mut [u8; 16]) {
    use std::hash::{BuildHasher, Hasher};
    for chunk in bytes.chunks_mut(8) {
        let mut hash = std::collections::hash_map::RandomState::new().build_hasher();
        hash.write_u64(FALLBACK_DRAWS.fetch_add(1, std::sync::atomic::Ordering::Relaxed));
        chunk.copy_from_slice(&hash.finish().to_be_bytes());
    }
}
/// The kernel's own source: the OS on native targets.
#[cfg(not(target_arch = "wasm32"))]
fn own_bytes(bytes: &mut [u8; 16]) {
    if getrandom::fill(bytes).is_err() {
        fallback_bytes(bytes);
    }
}
#[cfg(target_arch = "wasm32")]
fn own_bytes(bytes: &mut [u8; 16]) {
    fallback_bytes(bytes);
}
fn random_bytes() -> [u8; 16] {
    let mut bytes = [0; 16];
    let source = SOURCE.with(|source| source.borrow().as_ref().map(|(bytes, _)| bytes.clone()));
    if let Some(source) = source {
        source(&mut bytes);
    } else {
        own_bytes(&mut bytes);
    }
    bytes
}
/// Fill `out` from the byte source UUIDs draw from: the host's when one is
/// installed (`epdf-web` installs `getrandom` over Web Crypto), else the
/// kernel's own: the OS on native targets.
pub fn fill_random(out: &mut [u8]) {
    for chunk in out.chunks_mut(16) {
        let bytes = random_bytes();
        chunk.copy_from_slice(&bytes[..chunk.len()]);
    }
}
fn format(mut bytes: [u8; 16], version: u8) -> String {
    bytes[6] = (bytes[6] & 15) | version << 4;
    bytes[8] = (bytes[8] & 63) | 128;
    let hex = bytes.iter().map(|b| format!("{b:02x}")).collect::<String>();
    format!("{}-{}-{}-{}-{}", &hex[..8], &hex[8..12], &hex[12..16], &hex[16..20], &hex[20..])
}
pub fn generate_uuid() -> String {
    format(random_bytes(), 4)
}
pub fn generate_uuid_v7(now: Option<f64>) -> String {
    let now = now.unwrap_or_else(|| {
        let source = SOURCE.with(|source| source.borrow().as_ref().map(|(_, now)| now.clone()));
        source.map_or_else(
            || {
                std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)
                    .unwrap_or_default()
                    .as_millis() as f64
            },
            |now| now(),
        )
    });
    let mut bytes = random_bytes();
    let mut time = now.floor().max(0.0);
    for i in (0..6).rev() {
        bytes[i] = (time % 256.0) as u8;
        time = (time / 256.0).floor();
    }
    format(bytes, 7)
}

#[cfg(all(test, not(target_arch = "wasm32")))]
mod tests {
    use super::*;
    use std::sync::atomic::Ordering;

    /// With no source installed, a native draw comes from the OS, not the
    /// `RandomState` fallback.
    #[test]
    fn a_native_draw_with_no_source_comes_from_the_os() {
        assert!(SOURCE.with(|source| source.borrow().is_none()));
        let before = FALLBACK_DRAWS.load(Ordering::Relaxed);
        let (a, b) = (random_bytes(), random_bytes());
        let mut out = [0u8; 40];
        fill_random(&mut out);
        assert_ne!(a, b);
        assert_ne!(out, [0u8; 40]);
        assert_ne!(generate_uuid(), generate_uuid());
        assert_eq!(FALLBACK_DRAWS.load(Ordering::Relaxed), before, "the fallback was taken");
    }

    /// An installed source still wins on native.
    #[test]
    fn an_installed_source_wins_over_the_os() {
        let _guard =
            with_uuid_source(Rc::new(|bytes: &mut [u8; 16]| bytes.fill(7)), Rc::new(|| 0.0));
        assert_eq!(random_bytes(), [7; 16]);
    }
}
