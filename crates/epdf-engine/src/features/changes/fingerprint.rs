//! What a retry under the same `opId` must send (`changeFingerprint` in
//! `core/src/mutation/changeOutcome.ts`).
//!
//! The same ops give the same fingerprint whatever the order of their keys.
//! Bytes travel as payloads on this wire, not inside the change; an op that
//! carries them writes them where upstream's typed array sat, as the
//! `{"$bytes": ...}` its `canonicalJson` writes ([`bytes_token`]), once the
//! host has put the payloads in (`op::resolve_payloads`).

/// The fingerprint of a change as the wire sent it: its canonical JSON's
/// length and a 53-bit hash of it, both in base 36.
pub fn change_fingerprint(change: &serde_json::Value) -> String {
    let text = canonical_json(change);
    let (length, hash) = hash_units(text.encode_utf16());
    format!("{}-{}", base36(length), hash)
}

/// What `canonicalJson` writes for bytes, under `$bytes`: their length and
/// the same hash over them, both in base 36.
pub fn bytes_token(bytes: &[u8]) -> String {
    let (length, hash) = hash_units(bytes.iter().map(|byte| u16::from(*byte)));
    format!("{}-{}", base36(length), hash)
}

/// `value` as JSON with every object's keys sorted (`canonicalJson`).
fn canonical_json(value: &serde_json::Value) -> String {
    let mut out = Vec::new();
    write_canonical(&mut out, value);
    // Only `serde_json`'s writer and ASCII punctuation write here.
    String::from_utf8(out).unwrap_or_default()
}

/// [`canonical_json`] of `value`, written onto `out`.
fn write_canonical(out: &mut Vec<u8>, value: &serde_json::Value) {
    match value {
        serde_json::Value::Array(items) => {
            out.push(b'[');
            for (at, item) in items.iter().enumerate() {
                if at > 0 {
                    out.push(b',');
                }
                write_canonical(out, item);
            }
            out.push(b']');
        }
        serde_json::Value::Object(map) => {
            let mut entries: Vec<(&String, &serde_json::Value)> = map.iter().collect();
            // JavaScript compares strings by UTF-16 unit, which orders the
            // same as Rust's byte order except across the surrogate range;
            // compare units to agree everywhere.
            entries.sort_by(|(a, _), (b, _)| a.encode_utf16().cmp(b.encode_utf16()));
            out.push(b'{');
            for (at, (key, entry)) in entries.into_iter().enumerate() {
                if at > 0 {
                    out.push(b',');
                }
                write_json(out, key);
                out.push(b':');
                write_canonical(out, entry);
            }
            out.push(b'}');
        }
        other => write_json(out, other),
    }
}

/// `value` as `serde_json` writes it. Writing a string or a scalar into
/// memory does not fail.
fn write_json<T: serde::Serialize + ?Sized>(out: &mut Vec<u8>, value: &T) {
    let _ = serde_json::to_writer(out, value);
}

/// cyrb53 over UTF-16 units (`hashText`): how many units there were, and
/// the hash in base 36.
fn hash_units(units: impl IntoIterator<Item = u16>) -> (u64, String) {
    let mut h1: u32 = 0xdead_beef;
    let mut h2: u32 = 0x41c6_ce57;
    let mut length: u64 = 0;
    for unit in units {
        h1 = (h1 ^ u32::from(unit)).wrapping_mul(2_654_435_761);
        h2 = (h2 ^ u32::from(unit)).wrapping_mul(1_597_334_677);
        length += 1;
    }
    h1 = (h1 ^ (h1 >> 16)).wrapping_mul(2_246_822_507);
    h1 ^= (h2 ^ (h2 >> 13)).wrapping_mul(3_266_489_909);
    h2 = (h2 ^ (h2 >> 16)).wrapping_mul(2_246_822_507);
    h2 ^= (h1 ^ (h1 >> 13)).wrapping_mul(3_266_489_909);
    (length, base36(4_294_967_296 * u64::from(2_097_151 & h2) + u64::from(h1)))
}

fn base36(mut n: u64) -> String {
    const DIGITS: &[u8; 36] = b"0123456789abcdefghijklmnopqrstuvwxyz";
    if n == 0 {
        return "0".into();
    }
    let mut out = Vec::new();
    while n > 0 {
        out.push(DIGITS[(n % 36) as usize]);
        n /= 36;
    }
    out.reverse();
    String::from_utf8(out).unwrap_or_default()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn key_order_does_not_change_the_fingerprint_and_a_value_does() {
        let a = serde_json::json!({ "ops": [{ "type": "metadata.update", "patch": { "title": "A", "author": "B" } }] });
        let b = serde_json::json!({ "ops": [{ "patch": { "author": "B", "title": "A" }, "type": "metadata.update" }] });
        let c = serde_json::json!({ "ops": [{ "type": "metadata.update", "patch": { "title": "C", "author": "B" } }] });
        assert_eq!(change_fingerprint(&a), change_fingerprint(&b));
        assert_ne!(change_fingerprint(&a), change_fingerprint(&c));
    }

    #[test]
    fn bytes_hash_as_hash_bytes_does() {
        // Reference values from the TypeScript `hashBytes` run under node.
        assert_eq!(bytes_token(&[]), "0-wvjl67o803");
        assert_eq!(bytes_token(b"hello"), "5-19jqrdkps12");
        assert_eq!(bytes_token(&[0, 255, 128]), "3-1sqkae7qpd8");
    }

    #[test]
    fn the_hash_is_cyrb53() {
        // Reference values from the TypeScript `hashText` run under node.
        let hash = |text: &str| hash_units(text.encode_utf16()).1;
        assert_eq!(hash(""), "wvjl67o803");
        assert_eq!(hash(r#"{"a":1}"#), "18wxssjtft2");
        assert_eq!(hash("Smørrebrød 😀"), "2a2qgzsi1cp");
        assert_eq!(
            canonical_json(&serde_json::json!({ "b": 1, "a": [true, null] })),
            r#"{"a":[true,null],"b":1}"#
        );
    }
}
