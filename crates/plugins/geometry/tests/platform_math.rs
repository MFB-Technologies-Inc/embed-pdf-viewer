//! The kept platform math divergences, `PLATFORM-MATH-COS-SIN` and
//! `PLATFORM-MATH-ATAN2` in `crates/plugin-conformance/divergences.json`.
//!
//! Rust's `cos`, `sin` and `atan2` come from the platform math library, so
//! their last bit depends on the platform. Each test accepts the JavaScript
//! runtime's recorded answer or a double next to it, and then only the
//! outcome the TypeScript's own arithmetic gives from such an answer. That
//! holds on every platform whose library is within one ulp, and fails on a
//! larger difference or on any change to the arithmetic around the call.

use epdf_geometry::*;

/// Whether `rust` is `typescript` or a double next to it.
fn within_one_ulp(rust: f64, typescript: f64) -> bool {
    rust == typescript || rust == typescript.next_up() || rust == typescript.next_down()
}

fn pt(x: f64, y: f64) -> Point {
    Point { x, y }
}

#[test]
fn rotate_about_differs_only_by_one_ulp_of_the_platform_cos_and_sin() {
    // `Math.cos(-6)` and `Math.sin(-6)` in the JavaScript runtime, and the
    // TypeScript's `rotateAbout({ x: 0, y: -4915517297019373 }, -6)`.
    const COS: f64 = 0.9601702866503661;
    const SIN: f64 = 0.27941549819892586;
    let typescript: Mat2D = [COS, SIN, -SIN, COS, -1373471714452105.5, -195783644905449.0];
    let pivot = pt(0.0, -4915517297019373.0);

    let rotation = rotate(-6.0);
    let (cos, sin) = (rotation[0], rotation[1]);
    assert!(within_one_ulp(cos, COS), "cos(-6) is {cos:e}, the runtime's {COS:e}");
    assert!(within_one_ulp(sin, SIN), "sin(-6) is {sin:e}, the runtime's {SIN:e}");
    assert_eq!(rotation, [cos, sin, -sin, cos, 0.0, 0.0]);

    // The TypeScript's `T(c) · R · T(-c)` for a given cosine and sine. Fed the
    // runtime's, it is the TypeScript's matrix to the bit.
    let about = |c: f64, s: f64| {
        compose(
            translate(pivot.x, pivot.y),
            compose([c, s, -s, c, 0.0, 0.0], translate(-pivot.x, -pivot.y)),
        )
    };
    assert_eq!(about(COS, SIN), typescript);
    let allowed: Vec<Mat2D> = [COS.next_down(), COS, COS.next_up()]
        .into_iter()
        .flat_map(|c| [SIN.next_down(), SIN, SIN.next_up()].map(|s| about(c, s)))
        .collect();
    let rust = rotate_about(pivot, -6.0);
    assert!(allowed.contains(&rust), "{rust:?} is no one-ulp move of {typescript:?}");
    // The translation multiplies the cosine by 4.9e15, so the dev container's
    // cosine, one ulp lower, moves the last member by a whole unit, which a
    // comparison of safe integers sees.
    assert_eq!(about(COS.next_down(), SIN)[5], -195783644905450.0);
}

#[test]
fn a_degenerate_quad_is_labelled_only_as_one_ulp_of_the_platform_atan2_allows() {
    let quad = [pt(0.0, -1.0), pt(-1.4456042752816933e-19, 0.0), pt(0.0, 0.0), pt(0.0, 5e-324)];
    // Each corner's offset from the middle and `Math.atan2` of it in the
    // JavaScript runtime; the last two corners share an offset. The runtime
    // rounds the second up to 1.5707963267948968, the dev container down.
    let angles = [
        (3.6140106882042333e-20, -0.75, -1.5707963267948966),
        (-1.0842032064612701e-19, 0.25, 1.5707963267948968),
        (3.6140106882042333e-20, 0.25, 1.5707963267948966),
    ];
    for (dx, dy, typescript) in angles {
        let rust = angle_of([dx, dy, 0.0, 0.0, 0.0, 0.0]);
        assert!(
            within_one_ulp(rust, typescript),
            "atan2({dy:e}, {dx:e}) is {rust:e}, the runtime's {typescript:e}"
        );
    }

    // The labellings the TypeScript gives when each of those three calls
    // answers the runtime's value or a double next to it, over all 27 runs:
    // its own, and the one with the second corner's angle tied with the
    // corners on the y axis, which the dev container gives.
    let typescript = Quad {
        upper_left: pt(0.0, 0.0),
        upper_right: pt(0.0, 5e-324),
        lower_left: pt(0.0, -1.0),
        lower_right: pt(-1.4456042752816933e-19, 0.0),
    };
    let tied = Quad {
        lower_left: pt(-1.4456042752816933e-19, 0.0),
        lower_right: pt(0.0, -1.0),
        ..typescript
    };
    let rust = normalize_pdf_quad(quad);
    assert!(rust == typescript || rust == tied, "{rust:?}");
}
