//! The kernel's and this crate's `number_to_string` print every double
//! alike, on random inputs. Kept out of `tests/conformance.rs`, which
//! `rust-coverage` measures as replay: a random draw that lands on a tie
//! would change what replay reaches from run to run.

proptest::proptest! {
    /// The kernel keeps its own copy of `number_to_string` (it cannot
    /// depend on this crate); the two print every double alike.
    #[test]
    fn number_to_string_matches_js_digits(bits in proptest::num::u64::ANY) {
        let x = f64::from_bits(bits);
        proptest::prop_assert_eq!(
            epdf_kernel::js::number_to_string(x),
            epdf_geometry::js::number_to_string(x)
        );
    }

    #[test]
    fn number_to_string_matches_js_digits_on_integers(n in -(1_i64 << 55)..(1_i64 << 55)) {
        let x = n as f64;
        proptest::prop_assert_eq!(
            epdf_kernel::js::number_to_string(x),
            epdf_geometry::js::number_to_string(x)
        );
    }
}
