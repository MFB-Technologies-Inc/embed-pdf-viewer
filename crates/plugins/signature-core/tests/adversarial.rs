//! Hostile CMS bytes: inputs built to crash the reader or the verifier
//! rather than to fail verification. Each must come back as a verdict.

use base64::Engine as _;
use base64::engine::general_purpose::STANDARD;
use epdf_signature_core::cert::{AlgId, Spki};
use epdf_signature_core::oids::Digest;
use epdf_signature_core::{Cryptography, crypto, der, oids, parse, verify_cms_signature};

fn fixture(section: &str, name: &str) -> Vec<u8> {
    let path = concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../tooling/plugin-conformance/fixtures/signature/fixtures.json"
    );
    let json: serde_json::Value =
        serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap();
    STANDARD.decode(json[section][name].as_str().unwrap()).unwrap()
}

/// `depth` indefinite-length SEQUENCEs, each closed by end-of-contents.
fn nested_indefinite(depth: usize) -> Vec<u8> {
    let mut out = [0x30, 0x80].repeat(depth);
    out.extend([0, 0].repeat(depth));
    out
}

/// `depth` definite-length SEQUENCEs around a NULL.
fn nested_definite(depth: usize) -> Vec<u8> {
    let mut out = der::null();
    for _ in 0..depth {
        out = der::sequence(&[out]);
    }
    out
}

#[test]
fn deep_nesting_is_refused_not_a_stack_overflow() {
    let bytes = nested_indefinite(100_000);
    assert_eq!(der::from_ber(&bytes), None);
    assert_eq!(der::from_ber(&nested_definite(1_000)), None);
    assert_eq!(verify_cms_signature(&bytes), Cryptography::Invalid);
}

#[test]
fn modest_nesting_still_reads() {
    assert!(der::from_ber(&nested_definite(32)).is_some());
    assert!(der::from_ber(&nested_indefinite(32)).is_some());
}

/// The `psssha256` signer's own signature and key, checked under
/// RSASSA-PSS parameters whose saltLength INTEGER has `salt` as content.
fn verify_with_salt(salt: &[u8]) -> Result<bool, String> {
    let internal = parse::parse_cms_internal(&fixture("cms", "psssha256")).unwrap();
    let info = &internal.signer_info;
    let params = der::sequence(&[
        der::context(0, &[der::sequence(&[der::oid(oids::SHA256), der::null()])]),
        der::context(2, &[der::integer_bytes(salt)]),
    ]);
    let algorithm = AlgId { oid: oids::RSA_PSS.to_string(), params: der::from_ber(&params) };
    crypto::verify_with_public_key(
        &info.signed_attributes_der(),
        &info.signature,
        &internal.signer_certificate.spki,
        &algorithm,
        Some(internal.parsed.digest_algorithm),
    )
}

/// A 2047-bit modulus (so `s + n` still fits in its 256 bytes) and an
/// RSASSA-PSS SHA-256 signature over `data` with a 32-byte salt, both made
/// by `openssl dgst -sigopt rsa_padding_mode:pss`.
const PSS_2047_N: &[&str] = &[
    "6ed7a203edf0ea76992fd2b82e6f5e14245db90ec7fb7bb9046bf6f38764d6aebd4a09e877c626b3cf40364b",
    "0461dc5e50aeb0f6c2d3dc1ce409eb7613c1645a004706af8540353d5d096a95f064ce58fd4da341cab507b3",
    "e0d2b590c5f5b0726982930a3e552f8f2d0be6b07fa3e770a20712afe91e7d8984e1431608bc7408e8c08109",
    "d3abdcf39b3a8cac583284ac89bd012f83a68ee1071b0934b795b9c4d3037e8540102b3560b4926451f82d8e",
    "9b7c55ba370f3185461dedfff166d889e13ca6552e154b99e388d7821e8095b064b2fee7105e8d5d0d67e42a",
    "d76c87c99b90abea9284657c0bbfff7037e5e8e92216421c31fcf404473e527d77422a37",
];
const PSS_2047_SIG: &[&str] = &[
    "302fff1fce8097c81b14bd04ab84c6581b9c20df12028d1cee2a4b9ab3b0914080c380a20d3262825561e89a",
    "79dadf9a58566cb6a39c7ac27651b2ee2f489e32cf2a8a5ea5165d68b7ed19a8cd68fd863d6cb8ff6e1136dd",
    "87f71075a83d0e18b571e7d65ca60eeed7b622e621d5c6f3773e8ff15b00a6d4848760f44803932bb2c7d66d",
    "fc4fcf984ffb2bbcd870604b9ecdad0253c5eecd9543fabb08e14b36198630120b313b592647fed30817d052",
    "c2b97b1cce0a6bcf2c2245fb279a77a8d1f811b3e49ae97199b6533581d638f9c84d550f1317d3213223d8b6",
    "23c3df9314d5edb6bc9097f61508d5ed3249c2a49847bd37c0d5e87a2621abb09389e783",
];

fn hex(parts: &[&str]) -> Vec<u8> {
    let text = parts.concat();
    (0..text.len()).step_by(2).map(|i| u8::from_str_radix(&text[i..i + 2], 16).unwrap()).collect()
}

#[test]
fn pss_signature_at_or_past_the_modulus_is_not_verified() {
    // `s + n` is the same value mod n as a valid `s`. `rsa` 0.9 verified it
    // when it fit in k bytes; 0.10 refuses `s >= n`, as OpenSSL does.
    let (n, signature) = (hex(PSS_2047_N), hex(PSS_2047_SIG));
    let mut n_der = vec![0];
    n_der.extend(&n);
    let spki = Spki {
        raw: Vec::new(),
        algorithm: AlgId {
            oid: oids::RSA_ENCRYPTION.to_string(),
            params: der::from_ber(&der::null()),
        },
        key: der::sequence(&[der::integer_bytes(&n_der), der::integer(65537)]),
    };
    let params = der::sequence(&[
        der::context(0, &[der::sequence(&[der::oid(oids::SHA256), der::null()])]),
        der::context(2, &[der::integer(32)]),
    ]);
    let algorithm = AlgId { oid: oids::RSA_PSS.to_string(), params: der::from_ber(&params) };
    let verify = |signature: &[u8]| {
        crypto::verify_with_public_key(b"data", signature, &spki, &algorithm, Some(Digest::Sha256))
    };

    let (mut past, mut carry) = (vec![0u8; n.len()], 0u16);
    for i in (0..n.len()).rev() {
        let total = u16::from(signature[i]) + u16::from(n[i]) + carry;
        past[i] = total as u8;
        carry = total >> 8;
    }
    assert_eq!(carry, 0);
    assert_eq!(verify(&signature), Ok(true));
    assert_eq!(verify(&n), Ok(false));
    assert_eq!(verify(&past), Ok(false));
}

#[test]
fn pss_salt_length_that_wraps_is_not_verified() {
    // h_len + s_len + 2 would wrap to 0; `rsa` 0.10 checks the add, and the
    // salt guard in `crypto.rs` refuses it first.
    assert_eq!(verify_with_salt(&[0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xde]), Ok(false));
}

#[test]
fn pss_salt_length_wider_than_u64_is_not_verified() {
    assert_eq!(verify_with_salt(&[0x01, 0, 0, 0, 0, 0, 0, 0, 0]), Ok(false));
}

#[test]
fn pss_salt_length_past_the_modulus_is_not_verified() {
    // 2048-bit key, SHA-256: 256 - 32 - 2 = 222 is the largest that fits.
    assert_eq!(verify_with_salt(&[0x00, 0xdf]), Ok(false));
    assert_eq!(verify_with_salt(&[0x01, 0x00, 0x00, 0x00, 0x14]), Ok(false));
}

/// An RSA public key with a `bits`-bit odd modulus (not a product of two
/// primes: nothing here needs the private half) and exponent `e`.
fn rsa_spki(bits: usize, e: u64) -> Spki {
    let mut n = vec![0x5au8; bits / 8];
    n[0] = 0x80 | 0x5a;
    *n.last_mut().unwrap() |= 1;
    let mut n_der = vec![0];
    n_der.extend(n);
    let key = der::sequence(&[der::integer_bytes(&n_der), der::integer(e)]);
    let algorithm =
        AlgId { oid: oids::RSA_ENCRYPTION.to_string(), params: der::from_ber(&der::null()) };
    Spki { raw: Vec::new(), algorithm, key }
}

fn verify_sha256_with_rsa(spki: &Spki) -> Result<bool, String> {
    let algorithm =
        AlgId { oid: oids::SHA256_WITH_RSA.to_string(), params: der::from_ber(&der::null()) };
    let signature = vec![0x11; (spki.key.len() - 8).max(1)];
    crypto::verify_with_public_key(b"data", &signature, spki, &algorithm, None)
}

#[test]
fn rsa_keys_up_to_webcrypto_s_limit_import() {
    // `vectors/core/signature-verify.json` (`verify-rsa8192`) verifies a real
    // 8192-bit signature; here a 16384-bit key imports and a junk signature
    // is simply false.
    for bits in [8192, 16384] {
        assert_eq!(verify_sha256_with_rsa(&rsa_spki(bits, 65537)), Ok(false), "{bits} bits");
    }
}

#[test]
fn rsa_keys_under_1024_bits_import() {
    // `rsa` refuses these by default; WebCrypto has no floor, so a short key
    // imports and a junk signature is false, not a DataError.
    for bits in [512, 768] {
        assert_eq!(verify_sha256_with_rsa(&rsa_spki(bits, 65537)), Ok(false), "{bits} bits");
    }
}

#[test]
fn rsa_keys_past_webcrypto_s_limit_never_verify() {
    // Node's WebCrypto imports a 16400-bit key and answers false to every
    // verify with it (OpenSSL's modulus limit), whatever its exponent.
    assert_eq!(verify_sha256_with_rsa(&rsa_spki(16400, 65537)), Ok(false));
    assert_eq!(verify_sha256_with_rsa(&rsa_spki(16400, 2)), Ok(false));
}

#[test]
fn lengths_past_the_input_are_refused_not_allocated() {
    // A definite length near u32::MAX, and one near u64::MAX, over a few bytes.
    for bytes in [
        vec![0x30, 0x84, 0xff, 0xff, 0xff, 0xff, 0x05, 0x00],
        vec![0x30, 0x88, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x05, 0x00],
        // Nine length octets: wider than any length the reader can hold.
        vec![0x30, 0x89, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0x05, 0x00],
        // The reserved length octet 0xff.
        vec![0x30, 0xff, 0x05, 0x00],
    ] {
        assert_eq!(der::from_ber(&bytes), None, "{bytes:02x?}");
        assert_eq!(verify_cms_signature(&bytes), Cryptography::Invalid, "{bytes:02x?}");
    }
}

#[test]
fn truncated_and_unterminated_encodings_are_refused() {
    let unterminated = [0x30, 0x80, 0x05, 0x00];
    let cut_tag = [0x1f, 0xff, 0xff];
    let huge_tag = [0x1f, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x7f, 0x00];
    for bytes in [&unterminated[..], &cut_tag[..], &huge_tag[..], &[0x30][..], &[][..]] {
        assert_eq!(verify_cms_signature(bytes), Cryptography::Invalid, "{bytes:02x?}");
    }
    assert_eq!(der::from_ber(&unterminated), None);
}

#[test]
fn every_prefix_of_a_real_cms_is_a_verdict() {
    // No cut of a valid signature panics or verifies.
    let whole = fixture("cms", "psssha256");
    assert_eq!(verify_cms_signature(&whole), Cryptography::Valid);
    for end in (0..whole.len()).step_by(7) {
        assert_ne!(verify_cms_signature(&whole[..end]), Cryptography::Valid, "cut at {end}");
    }
}
