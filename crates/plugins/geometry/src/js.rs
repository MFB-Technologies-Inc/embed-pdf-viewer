//! JavaScript's numeric built-ins, where Rust's differ.
//!
//! The TypeScript is the oracle, so arithmetic that reaches a `Math.*` call
//! uses these. Every downstream port that rounds, clamps or prints a number
//! the way the TypeScript does uses them too.

/// `Math.round`: the nearest integer, ties toward +infinity. `f64::round`
/// breaks ties away from zero (`-2.5` is `-3` there, `-2` here), and the
/// `floor(x + 0.5)` shortcut is wrong at `0.49999999999999994` and past 2^52.
pub fn round(x: f64) -> f64 {
    let floor = x.floor();
    if x - floor >= 0.5 { floor + 1.0 } else { floor }
}

/// `Math.max(a, b)`: NaN if either is NaN (`f64::max` returns the other),
/// and `+0` over `-0`.
pub fn max(a: f64, b: f64) -> f64 {
    if a.is_nan() || b.is_nan() {
        f64::NAN
    } else if a == b {
        // Equal is only interesting for zeros: `+0` wins.
        if a.is_sign_negative() { b } else { a }
    } else if a > b {
        a
    } else {
        b
    }
}

/// `Math.min(a, b)`: NaN if either is NaN, and `-0` over `+0`.
pub fn min(a: f64, b: f64) -> f64 {
    if a.is_nan() || b.is_nan() {
        f64::NAN
    } else if a == b {
        // Equal is only interesting for zeros: `-0` wins.
        if a.is_sign_negative() { a } else { b }
    } else if a < b {
        a
    } else {
        b
    }
}

/// `Math.max(...values)`; `-Infinity` for none.
pub fn max_of(values: &[f64]) -> f64 {
    values.iter().fold(f64::NEG_INFINITY, |acc, &v| max(acc, v))
}

/// `Math.min(...values)`; `Infinity` for none.
pub fn min_of(values: &[f64]) -> f64 {
    values.iter().fold(f64::INFINITY, |acc, &v| min(acc, v))
}

/// `Math.sign`: `±0` and NaN map to themselves (`f64::signum(0.0)` is `1`).
pub fn sign(x: f64) -> f64 {
    if x > 0.0 {
        1.0
    } else if x < 0.0 {
        -1.0
    } else {
        x
    }
}

/// `Math.hypot(a, b)`, as V8 computes it (`math.tq`): an infinity wins over
/// NaN, then the values are normalized by the largest and summed with Kahan
/// compensation. The platform `hypot` can differ from it in the last bit, and
/// `normalizePdfQuad` compares these lengths against a threshold.
pub fn hypot(a: f64, b: f64) -> f64 {
    let values = [a, b];
    let mut max = 0.0_f64;
    let mut one_is_nan = false;
    for v in values {
        if v.is_nan() {
            one_is_nan = true;
        } else if v.abs() > max {
            max = v.abs();
        }
    }
    if max == f64::INFINITY {
        return f64::INFINITY;
    }
    if one_is_nan {
        return f64::NAN;
    }
    if max == 0.0 {
        return 0.0;
    }
    let mut sum = 0.0_f64;
    let mut compensation = 0.0_f64;
    for v in values {
        let n = v.abs() / max;
        let summand = n * n - compensation;
        let preliminary = sum + summand;
        compensation = (preliminary - sum) - summand;
        sum = preliminary;
    }
    sum.sqrt() * max
}

/// `Number.prototype.toString()` (ECMAScript `Number::toString`, radix 10):
/// the shortest round-trip digits, laid out in plain notation for exponents
/// from -7 to 20 and scientific (`1e+21`, `1.5e-7`) outside it. Rust's `{}`
/// never switches to scientific notation and prints `-0` as `-0`.
/// `epdf_kernel::js` carries the same function; this crate links the
/// kernel only for its harness, so it keeps its own copy, which
/// `number_to_string_is_the_kernel_one` compares with the kernel's.
pub fn number_to_string(x: f64) -> String {
    // An integer below 2^53 in magnitude is exact, and its shortest digits
    // are its decimal integer: one allocation, no layout.
    if x.trunc() == x && x.abs() < 9_007_199_254_740_992.0 {
        return (x as i64).to_string();
    }
    shortest_number_to_string(x)
}

/// [`number_to_string`] for any double: the shortest round-trip digits
/// laid out as `Number::toString` lays them out.
fn shortest_number_to_string(x: f64) -> String {
    if x.is_nan() {
        return "NaN".into();
    }
    if x == 0.0 {
        return "0".into();
    }
    if x < 0.0 {
        return format!("-{}", shortest_number_to_string(-x));
    }
    if x.is_infinite() {
        return "Infinity".into();
    }
    let (digits, exponent) = shortest_digits(x);
    let k = digits.len() as i32;
    let n = exponent + 1;
    if k <= n && n <= 21 {
        format!("{digits}{}", "0".repeat((n - k) as usize))
    } else if 0 < n && n <= 21 {
        let (int, frac) = digits.split_at(n as usize);
        format!("{int}.{frac}")
    } else if -6 < n && n <= 0 {
        format!("0.{}{digits}", "0".repeat((-n) as usize))
    } else {
        let e = n - 1;
        let sign = if e < 0 { '-' } else { '+' };
        let (first, rest) = digits.split_at(1);
        if rest.is_empty() {
            format!("{first}e{sign}{}", e.abs())
        } else {
            format!("{first}.{rest}e{sign}{}", e.abs())
        }
    }
}

/// The shortest decimal digits that round-trip `x` (positive, finite), and
/// the exponent of the first: `x = d.ddd × 10^exponent`. Of two equally
/// close candidates, the one ending in an even digit.
fn shortest_digits(x: f64) -> (String, i32) {
    // `{:e}` gives the shortest round-trip digits closest to `x`, but breaks
    // an exact tie upward where JavaScript picks the even digit.
    let sci = format!("{x:e}");
    let (mantissa, exponent) = sci.split_once('e').expect("LowerExp has an exponent");
    let digits: String = mantissa.chars().filter(char::is_ascii_digit).collect();
    let exponent: i32 = exponent.parse().expect("integer exponent");
    even_tie(x, &digits, exponent).unwrap_or((digits, exponent))
}

/// At an exact tie between `digits` and its neighbour, the one ending in an
/// even digit, if it round-trips.
fn even_tie(x: f64, digits: &str, exponent: i32) -> Option<(String, i32)> {
    // The candidates are multiples of 10^-places.
    let places = digits.len() as i32 - 1 - exponent;
    // `x = m * 2^q` with `m` odd has exactly `-q` decimal places, the last a
    // 5, so it is halfway between two candidates exactly when `-q` is one
    // more than theirs. With `places <= 0` the halfway point is farther from
    // the candidates than the half ulp a round trip allows: no tie there.
    if places < 1 || odd_binary_exponent(x) != -(places + 1) {
        return None;
    }
    // Fixed-precision formatting is exact: here, every digit of `x`.
    let exact = format!("{x:.*}", places as usize + 1);
    let floor: String = exact[..exact.len() - 1].chars().filter(char::is_ascii_digit).collect();
    let floor = floor.trim_start_matches('0');
    let even = if floor.is_empty() {
        // Zero is the even candidate, and never round-trips a positive `x`.
        return None;
    } else if floor.ends_with(['0', '2', '4', '6', '8']) {
        floor.to_string()
    } else {
        increment(floor)
    };
    if format!("{even}e-{places}").parse::<f64>() != Ok(x) {
        return None;
    }
    let exponent = even.len() as i32 - 1 - places;
    Some((even.trim_end_matches('0').to_string(), exponent))
}

/// The `q` of `x = m * 2^q` with `m` odd, for a positive finite `x`.
fn odd_binary_exponent(x: f64) -> i32 {
    let (m, e) = decompose(x);
    e + m.trailing_zeros() as i32
}

/// A decimal digit string plus one.
fn increment(digits: &str) -> String {
    let mut bytes = digits.as_bytes().to_vec();
    for byte in bytes.iter_mut().rev() {
        if *byte == b'9' {
            *byte = b'0';
        } else {
            *byte += 1;
            return String::from_utf8(bytes).expect("ASCII digits");
        }
    }
    format!("1{}", String::from_utf8(bytes).expect("ASCII digits"))
}

/// `x` as `m * 2^e` with an integer mantissa, for a positive finite `x`.
fn decompose(x: f64) -> (u64, i32) {
    let bits = x.to_bits();
    let exponent = ((bits >> 52) & 0x7ff) as i32;
    let fraction = bits & ((1u64 << 52) - 1);
    if exponent == 0 { (fraction, -1074) } else { (fraction | (1u64 << 52), exponent - 1075) }
}

/// Whether `x * 10^digits` lies exactly halfway between two integers, for a
/// positive finite `x`: `2 * x * 10^digits` is then an odd integer.
fn exact_tie(x: f64, digits: u32) -> bool {
    let (m, e) = decompose(x);
    let t = u128::from(m) * 5u128.pow(digits);
    // 2 * x * 10^digits = t * 2^(e + 1 + digits)
    let shift = e + 1 + digits as i32;
    match shift {
        s if s > 0 => false,
        0 => t % 2 == 1,
        s => t != 0 && t.trailing_zeros() == (-s) as u32,
    }
}

/// `Number.prototype.toFixed(digits)`, for up to 20 digits (the exact tie
/// test overflows `u128` past about 32, so more panics). The decimal is
/// the exact value of the double rounded half away from zero; Rust's `{:.N}`
/// breaks an exact tie to even, so a tie is written out one digit longer
/// (exactly, ending in 5) and rounded up by hand. A magnitude of `1e21` or
/// more prints as [`number_to_string`] does, and a negative number keeps its
/// sign even when it rounds to zero (`(-0.0001).toFixed(3)` is `-0.000`).
pub fn to_fixed(x: f64, digits: u32) -> String {
    assert!(digits <= 20, "to_fixed takes at most 20 digits, got {digits}");
    if x.is_nan() {
        return "NaN".into();
    }
    if x.abs() >= 1e21 {
        return number_to_string(x);
    }
    let negative = x < 0.0;
    let y = x.abs();
    let precision = digits as usize;
    let body = if y != 0.0 && exact_tie(y, digits) {
        round_up_tie(&format!("{:.*}", precision + 1, y))
    } else {
        format!("{:.*}", precision, y)
    };
    if negative { format!("-{body}") } else { body }
}

/// Drop the trailing `5` of an exact decimal and add one in the last place
/// kept: `0.03125` to `0.0313`, `2.5` to `3`, `9.95` to `10.0`.
fn round_up_tie(exact: &str) -> String {
    let mut digits: Vec<u8> = exact[..exact.len() - 1].bytes().collect();
    if digits.last() == Some(&b'.') {
        digits.pop();
    }
    let mut i = digits.len();
    loop {
        if i == 0 {
            digits.insert(0, b'1');
            break;
        }
        i -= 1;
        match digits[i] {
            b'.' => continue,
            b'9' => digits[i] = b'0',
            d => {
                digits[i] = d + 1;
                break;
            }
        }
    }
    String::from_utf8(digits).expect("ASCII digits")
}

#[cfg(test)]
mod tests {
    use super::*;

    /// Exactly halfway between two shortest candidates, JavaScript prints
    /// the one ending in an even digit.
    #[test]
    fn number_to_string_breaks_a_tie_to_even() {
        let cases: &[(&str, &str)] = &[
            ("1655218367829957.25", "1655218367829957.2"),
            ("18008701800628.8125", "18008701800628.812"),
            ("151546589707673.125", "151546589707673.12"),
            ("0.0239124298095703125", "0.023912429809570312"),
            ("1655218367829957.75", "1655218367829957.8"),
        ];
        for (exact, want) in cases {
            let x: f64 = exact.parse().unwrap();
            assert_eq!(number_to_string(x), *want, "{exact}");
            assert_eq!(number_to_string(-x), format!("-{want}"), "-{exact}");
        }
    }

    /// This copy against the kernel's, which the crate links only with
    /// `harness` (a default feature, so plain `cargo test` runs this).
    #[cfg(feature = "harness")]
    #[test]
    fn number_to_string_is_the_kernel_one() {
        let mut seed: u64 = 0x2545_f491_4f6c_dd1d;
        let mut next = || {
            seed = seed
                .wrapping_mul(6_364_136_223_846_793_005)
                .wrapping_add(1_442_695_040_888_963_407);
            seed
        };
        let decimal = [
            "1655218367829957.25",
            "1655218367829957.75",
            "18008701800628.8125",
            "151546589707673.125",
            "0.0239124298095703125",
            "0.1",
            "123.456",
            "1e21",
            "999999999999999900000",
            "1e-6",
            "1e-7",
            "4.35e-310",
            "2.2250738585072014e-308",
            "1.7976931348623157e308",
            "9007199254740993",
            "18014398509481988",
        ];
        let mut values: Vec<f64> = decimal.iter().map(|s| s.parse().unwrap()).collect();
        values.extend([0.0, -0.0, f64::NAN, f64::INFINITY]);
        values.extend((0..52).map(|bit| f64::from_bits(1 << bit)));
        values.extend((1..2047u64).map(|e| f64::from_bits(e << 52)));
        for k in 0..20_000u64 {
            // Exact ties between one-place candidates (the ulp from 2^50
            // to 2^51 is 0.25), one broken down and one up.
            let n = (1u64 << 50) + k * 56_294_995_342;
            values.push(n as f64 + 0.25);
            values.push(n as f64 + 0.75);
            values.push(f64::from_bits(next()));
        }
        for value in values {
            for x in [value, -value] {
                assert_eq!(number_to_string(x), epdf_kernel::js::number_to_string(x), "{x:e}");
            }
        }
    }

    #[test]
    fn to_fixed_takes_up_to_20_digits() {
        assert_eq!(to_fixed(0.1, 20), "0.10000000000000000555");
        assert!(std::panic::catch_unwind(|| to_fixed(0.1, 21)).is_err());
    }

    #[test]
    fn to_fixed_matches_javascript() {
        let cases: &[(f64, u32, &str)] = &[
            (0.0625, 3, "0.063"),
            (-0.0625, 3, "-0.063"),
            (0.125, 2, "0.13"),
            (2.5, 0, "3"),
            (9.9995, 3, "9.999"),
            (9.9375, 3, "9.938"),
            (1.005, 2, "1.00"),
            (-0.0001, 3, "-0.000"),
            (-0.0, 3, "0.000"),
            (123.456, 3, "123.456"),
            (1e21, 3, "1e+21"),
            (f64::NAN, 3, "NaN"),
            (0.03125, 4, "0.0313"),
            (99.9995, 3, "99.999"),
            (99.99951, 3, "100.000"),
        ];
        for (x, digits, want) in cases {
            assert_eq!(to_fixed(*x, *digits), *want, "{x}.toFixed({digits})");
        }
    }

    #[test]
    fn number_to_string_matches_javascript() {
        let cases: &[(f64, &str)] = &[
            (1.0, "1"),
            (-0.0, "0"),
            (0.1, "0.1"),
            (123.456, "123.456"),
            (1e20, "100000000000000000000"),
            (1e21, "1e+21"),
            (1.5e21, "1.5e+21"),
            (0.000001, "0.000001"),
            (1e-7, "1e-7"),
            (1.2345e-7, "1.2345e-7"),
            (5e-324, "5e-324"),
            (f64::INFINITY, "Infinity"),
            (f64::NEG_INFINITY, "-Infinity"),
            (f64::NAN, "NaN"),
            (-12.5, "-12.5"),
            (123456789012345680000.0, "123456789012345680000"),
            (9007199254740991.0, "9007199254740991"),
            (9007199254740992.0, "9007199254740992"),
            (9007199254740994.0, "9007199254740994"),
            (-9007199254740991.0, "-9007199254740991"),
            (-9007199254740992.0, "-9007199254740992"),
            (4503599627370497.0, "4503599627370497"),
            (1e15, "1000000000000000"),
            (-42.0, "-42"),
            (1152921504606846976.0, "1152921504606847000"),
            (0.5, "0.5"),
            (-0.5, "-0.5"),
        ];
        for (x, want) in cases {
            assert_eq!(number_to_string(*x), *want, "{x}");
        }
    }

    fn integers_near_2_53() -> impl proptest::strategy::Strategy<Value = f64> {
        use proptest::strategy::Strategy;
        (-(1_i64 << 55)..(1_i64 << 55)).prop_map(|n| n as f64)
    }

    proptest::proptest! {
        /// The integer fast path agrees with the shortest-digits layout,
        /// which `number_to_string_matches_javascript` pins to JavaScript.
        #[test]
        fn number_to_string_matches_js_digits(x in proptest::num::f64::ANY) {
            proptest::prop_assert_eq!(number_to_string(x), shortest_number_to_string(x));
        }

        #[test]
        fn number_to_string_matches_js_digits_on_integers(x in integers_near_2_53()) {
            proptest::prop_assert_eq!(number_to_string(x), shortest_number_to_string(x));
            proptest::prop_assert_eq!(number_to_string(-x), shortest_number_to_string(-x));
            proptest::prop_assert_eq!(number_to_string(x + 0.5), shortest_number_to_string(x + 0.5));
        }

        #[test]
        fn number_to_string_matches_js_digits_on_any_bits(bits in proptest::num::u64::ANY) {
            let x = f64::from_bits(bits);
            proptest::prop_assert_eq!(number_to_string(x), shortest_number_to_string(x));
        }
    }

    #[test]
    fn round_is_math_round() {
        for (x, want) in
            [(2.5, 3.0), (-2.5, -2.0), (-0.5, 0.0), (0.49999999999999994, 0.0), (-1.5, -1.0)]
        {
            assert_eq!(round(x), want, "{x}");
        }
        assert_eq!(round(4503599627370497.0), 4503599627370497.0);
        assert!(round(f64::NAN).is_nan());
        assert_eq!(round(f64::INFINITY), f64::INFINITY);
    }

    #[test]
    fn min_max_propagate_nan() {
        assert!(max(1.0, f64::NAN).is_nan());
        assert!(min(f64::NAN, 1.0).is_nan());
        assert!(max(0.0, -0.0).is_sign_positive());
        assert!(min(0.0, -0.0).is_sign_negative());
        assert_eq!(min_of(&[]), f64::INFINITY);
    }

    /// Only `normalizePdfQuad` calls `sign` inside this crate, never with 0;
    /// the downstream ports (annotation-core, stage) can pass either zero.
    #[test]
    fn sign_keeps_signed_zeros_and_nan() {
        assert_eq!(sign(3.0), 1.0);
        assert_eq!(sign(-3.0), -1.0);
        assert!(sign(0.0) == 0.0 && sign(0.0).is_sign_positive());
        assert!(sign(-0.0) == 0.0 && sign(-0.0).is_sign_negative());
        assert!(sign(f64::NAN).is_nan());
    }
}
