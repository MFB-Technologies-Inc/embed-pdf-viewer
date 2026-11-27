//! A field's purpose and the format a write gives it, as a prediction works
//! them out (`core/src/forms/purpose.ts`, `format.ts` and `datePattern.ts`,
//! 833b40015, #1147): the checks a write is refused by, and the two scripts
//! a date or a time format is written as. `epdf-engine`'s
//! `features/forms/format.rs` carries the same rules over its own types;
//! this is their transcription over the engine's JSON (Ruling13.15).

use epdf_kernel::Error;
use serde_json::{Map, Value, json};

use super::engine_error;

/// `FIELD_PURPOSE_MAX_LENGTH`, in UTF-16 code units.
const FIELD_PURPOSE_MAX_LENGTH: usize = 256;

/// JavaScript's `String.prototype.trim` white space.
fn js_space(c: char) -> bool {
    matches!(
        c,
        '\t' | '\n' | '\u{0B}' | '\u{0C}' | '\r' | ' ' | '\u{A0}' | '\u{1680}' | '\u{2000}'
            ..='\u{200A}'
                | '\u{2028}'
                | '\u{2029}'
                | '\u{202F}'
                | '\u{205F}'
                | '\u{3000}'
                | '\u{FEFF}'
    )
}

/// `isFieldPurpose`: 1 to 256 characters, with no white space at its start
/// or its end and no NUL.
fn is_field_purpose(purpose: &str) -> bool {
    let length = purpose.encode_utf16().count();
    (1..=FIELD_PURPOSE_MAX_LENGTH).contains(&length)
        && purpose.trim_matches(js_space) == purpose
        && !purpose.contains('\0')
}

/// `checkedPurpose(purpose)`: `null` for none (left out or `null`); one that
/// isn't a purpose is refused with `InvalidArg`, not trimmed.
pub fn checked_purpose(purpose: Option<&Value>) -> Result<Value, Error> {
    match purpose {
        None | Some(Value::Null) => Ok(Value::Null),
        Some(Value::String(text)) if is_field_purpose(text) => Ok(json!(text)),
        Some(_) => Err(engine_error(
            "InvalidArg",
            "a field's purpose is 1 to 256 characters with no space at its start or end and no \
             NUL character, or null for none"
                .into(),
            "purpose",
        )),
    }
}

const LONGEST_PATTERN: usize = 64;
/// What stands between date groups. A colon belongs to a time.
const SEPARATORS: &str = " /-.,";

#[derive(Clone, Copy, PartialEq, Eq)]
enum DatePart {
    Year,
    Month,
    Day,
}

#[derive(Clone, Copy, PartialEq, Eq)]
enum Written {
    Digits,
    Letters,
    Separator,
}

/// `DATE_GROUPS`: what each date group writes, and how.
fn date_group(run: &str) -> Option<(DatePart, Written)> {
    Some(match run {
        "yyyy" | "yy" => (DatePart::Year, Written::Digits),
        "mmmm" | "mmm" => (DatePart::Month, Written::Letters),
        "mm" | "m" => (DatePart::Month, Written::Digits),
        "dd" | "d" => (DatePart::Day, Written::Digits),
        _ => return None,
    })
}

/// `TIME` (`/(?:HH|H|hh|h):MM(?::ss)?(?: *tt)?/`) matched at `at`: where
/// the match ends.
fn time_at(text: &[u8], at: usize) -> Option<usize> {
    let rest = &text[at..];
    let hours = ["HH", "H", "hh", "h"]
        .iter()
        .find(|h| rest.starts_with(h.as_bytes()) && rest[h.len()..].starts_with(b":MM"))?;
    let mut end = at + hours.len() + 3;
    if text[end..].starts_with(b":ss") {
        end += 3;
    }
    let spaces = text[end..].iter().take_while(|b| **b == b' ').count();
    if text[end + spaces..].starts_with(b"tt") {
        end += spaces + 2;
    }
    Some(end)
}

/// `TIME.exec`: the first time in `text`, as its start and its end.
fn first_time(text: &str) -> Option<(usize, usize)> {
    let bytes = text.as_bytes();
    (0..bytes.len()).find_map(|at| time_at(bytes, at).map(|end| (at, end)))
}

/// `stretchOf`: how each part of a stretch outside the time is written and
/// what each date group writes; `None` for anything a pattern isn't made of.
fn stretch_of(text: &str) -> Option<(Vec<Written>, Vec<DatePart>)> {
    let (mut written, mut groups) = (Vec::new(), Vec::new());
    let chars: Vec<char> = text.chars().collect();
    let mut at = 0;
    while at < chars.len() {
        let c = chars[at];
        if c.is_ascii_alphabetic() {
            let run = chars[at..].iter().take_while(|x| **x == c).count();
            let group: String = chars[at..at + run].iter().collect();
            let (part, how) = date_group(&group)?;
            written.push(how);
            groups.push(part);
            at += run;
        } else if SEPARATORS.contains(c) {
            written.push(Written::Separator);
            at += 1;
        } else {
            return None;
        }
    }
    Some((written, groups))
}

/// `isDatePattern`.
fn is_date_pattern(pattern: &str) -> bool {
    let length = pattern.encode_utf16().count();
    if !(1..=LONGEST_PATTERN).contains(&length) {
        return false;
    }
    let time = first_time(pattern);
    let (before, after) = match time {
        Some((start, end)) => (&pattern[..start], &pattern[end..]),
        None => (pattern, ""),
    };
    let (Some((before_written, before_groups)), Some((after_written, after_groups))) =
        (stretch_of(before), stretch_of(after))
    else {
        return false;
    };
    let groups: Vec<DatePart> = before_groups.into_iter().chain(after_groups).collect();
    let distinct = [DatePart::Year, DatePart::Month, DatePart::Day]
        .iter()
        .filter(|part| groups.contains(part))
        .count();
    if groups.is_empty() || distinct != groups.len() {
        return false;
    }
    let time_written: Vec<Written> = match time {
        Some((start, end)) => vec![
            Written::Digits,
            if pattern[start..end].ends_with("tt") { Written::Letters } else { Written::Digits },
        ],
        None => Vec::new(),
    };
    let time_start = before_written.len();
    let written: Vec<Written> =
        before_written.into_iter().chain(time_written).chain(after_written).collect();
    !written.windows(2).enumerate().any(|(at, pair)| {
        pair[0] == Written::Digits && pair[1] == Written::Digits && at != time_start
    })
}

/// `isTimePattern`: a time and nothing else, at most 64 characters.
fn is_time_pattern(pattern: &str) -> bool {
    pattern.len() <= LONGEST_PATTERN && time_at(pattern.as_bytes(), 0) == Some(pattern.len())
}

/// `PATTERN_RULE`: what a refused pattern is told, per kind.
fn pattern_rule(kind: &str) -> &'static str {
    if kind == "date" {
        "a date format's pattern is 1 to 64 characters: the groups yyyy yy mmmm mmm mm m dd d, at \
         least one of them and the year, the month and the day each once at most, with spaces and \
         / - . , between them, and at most one time (HH, H, hh or h, then :MM, then :ss and tt \
         when it has them); two groups written in digits never touch"
    } else {
        "a time format's pattern is a time and nothing else, at most 64 characters: HH, H, hh or \
         h, then :MM, then :ss when it has seconds, then tt when it has one"
    }
}

/// `checkedFormat(format)`: a date or a time with a pattern of its kind, or
/// `null` for none; anything else is refused with `InvalidArg`.
fn checked_format(format: &Value) -> Result<Value, Error> {
    if format.is_null() {
        return Ok(Value::Null);
    }
    let invalid = |message: &str, field: &str| engine_error("InvalidArg", message.into(), field);
    let kind = match format.get("kind").and_then(Value::as_str) {
        Some("other") => {
            return Err(invalid(
                "a format of kind 'other' is read, never written: write its scripts in actions",
                "format",
            ));
        }
        Some(kind @ ("date" | "time")) => kind,
        _ => {
            return Err(invalid(
                "a field's format is a date or a time with a pattern, or null for none",
                "format",
            ));
        }
    };
    let is_pattern = if kind == "date" { is_date_pattern } else { is_time_pattern };
    match format.get("pattern").and_then(Value::as_str) {
        Some(pattern) if is_pattern(pattern) => Ok(json!({ "kind": kind, "pattern": pattern })),
        _ => Err(invalid(pattern_rule(kind), "format.pattern")),
    }
}

/// `takesFormat(kind)`: a text field and a dropdown have a format.
fn takes_format(kind: &str) -> bool {
    matches!(kind, "text" | "combobox")
}

/// `formatWriteOf(write, kind)`: the format a write gives a field of `kind`,
/// checked; `None` when the write names none, `Some(null)` for `null`. A
/// format on a kind that has none, and one beside a script for one of its
/// own two events, are refused with `InvalidArg`.
pub fn format_write_of(write: &Value, kind: &str) -> Result<Option<Value>, Error> {
    let Some(named) = write.get("format").filter(|f| !epdf_kernel::is_undefined(f)) else {
        return Ok(None);
    };
    if !takes_format(kind) {
        return Err(engine_error(
            "InvalidArg",
            format!("'format' does not apply to a {kind} field"),
            "format",
        ));
    }
    let format = checked_format(named)?;
    for event in ["format", "keystroke"] {
        let written = write
            .get("actions")
            .and_then(|a| a.get(event))
            .is_some_and(|w| !epdf_kernel::is_undefined(w));
        if written {
            return Err(engine_error(
                "InvalidArg",
                format!(
                    "'format' writes the field's {event} script: name it or actions.{event}, not both"
                ),
                &format!("actions.{event}"),
            ));
        }
    }
    Ok(Some(format))
}

/// `formatScripts(format)`: the two scripts a writable format is written
/// as, `(format, keystroke)`, in Acrobat's wording. A time's keystroke script
/// names the standard time pattern of its shape by number.
fn format_scripts(format: &Value) -> (String, String) {
    let pattern = format["pattern"].as_str().unwrap_or("");
    if format["kind"] == "date" {
        (format!("AFDate_FormatEx(\"{pattern}\");"), format!("AFDate_KeystrokeEx(\"{pattern}\");"))
    } else {
        // `standardTimeLike`: with or without seconds, on a 12 or a 24 hour clock.
        let number =
            (if pattern.contains("ss") { 2 } else { 0 }) + u8::from(pattern.contains("tt"));
        (format!("AFTime_FormatEx(\"{pattern}\");"), format!("AFTime_Keystroke({number});"))
    }
}

/// `formatsEqual(left, right)`: both none, or the same kind and pattern.
pub fn formats_equal(left: &Value, right: &Value) -> bool {
    if left.is_null() || right.is_null() {
        return left.is_null() && right.is_null();
    }
    left.get("kind") == right.get("kind")
        && (left["kind"] == "other" || left.get("pattern") == right.get("pattern"))
}

/// `scriptTreeOf(script)`: one JavaScript action, as a read gives it.
fn script_tree_of(script: String) -> Value {
    json!({
        "root": { "type": "javascript", "subtype": "JavaScript", "script": script, "next": [] },
        "incomplete": false,
        "warnings": [],
    })
}

/// `formatActions(current, format)`: a field's actions after a format write,
/// the format's two scripts in place of the keystroke and format events, or
/// both events gone for no format. `None` when no event is left, as a field
/// without scripts reads.
pub fn format_actions(current: Option<&Value>, format: &Value) -> Option<Value> {
    let mut kept: Map<String, Value> =
        current.and_then(Value::as_object).cloned().unwrap_or_default();
    kept.shift_remove("keystroke");
    kept.shift_remove("format");
    if format.is_null() {
        return (!kept.is_empty()).then_some(Value::Object(kept));
    }
    let (format_script, keystroke) = format_scripts(format);
    let mut actions = Map::new();
    actions.insert("keystroke".into(), script_tree_of(keystroke));
    actions.insert("format".into(), script_tree_of(format_script));
    actions.extend(kept);
    Some(Value::Object(actions))
}
