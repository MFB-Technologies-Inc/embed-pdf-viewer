//! A field's purpose and its format (`core/src/forms/purpose.ts`,
//! `format.ts` and `datePattern.ts`, 833b40015, #1147).
//!
//! A purpose is what a field's value means (`email`, `date-signed`, a name
//! an app made up), kept in the field's `/EMBD_Metadata /Purpose`; the
//! engine reads nothing into it. A format is the shape a text field's or a
//! dropdown's value has. In the file it is the field's format script
//! (`/AA /F`) and keystroke script (`/AA /K`) as Acrobat writes them, so the
//! scripts are its one record: a read works the format out from them, and a
//! write composes them from a checked pattern.

use serde::{Deserialize, Serialize};
use serde_json::Value;

use crate::dto::Patch;
use crate::error::{EngineError, EngineErrorCode};
use crate::features::actions::reader::{ActionNodeKind, ActionTree};
use crate::features::actions::write::{FieldActionsPatch, FieldScriptWrite, JavascriptTag};
use crate::features::forms::snapshot::{FieldActions, FormFieldFamily};
use crate::features::xmp::xml::js_space;

// --- Purpose -----------------------------------------------------------------

/// The most characters (UTF-16 code units) a purpose has
/// (`FIELD_PURPOSE_MAX_LENGTH`).
pub const FIELD_PURPOSE_MAX_LENGTH: usize = 256;

/// `isFieldPurpose`: 1 to 256 characters, with no white space at its start or
/// its end and no NUL. The runtime takes a string up to its first NUL, so a
/// purpose with one would be stored cut short.
pub fn is_field_purpose(purpose: &str) -> bool {
    let length = purpose.encode_utf16().count();
    (1..=FIELD_PURPOSE_MAX_LENGTH).contains(&length)
        && purpose.trim_matches(js_space) == purpose
        && !purpose.contains('\0')
}

/// `checkedPurpose`: the purpose a write names, `None` for none (left out or
/// `null`). One that isn't a purpose is refused with `InvalidArg`, not
/// trimmed.
pub fn checked_purpose(purpose: Option<&Value>) -> Result<Option<String>, EngineError> {
    match purpose {
        None | Some(Value::Null) => Ok(None),
        Some(Value::String(text)) if is_field_purpose(text) => Ok(Some(text.clone())),
        Some(_) => Err(EngineError::with_message(
            EngineErrorCode::InvalidArg,
            "a field's purpose is 1 to 256 characters with no space at its start or end and no \
             NUL character, or null for none",
        )
        .with_details(serde_json::json!({ "field": "purpose" }))),
    }
}

// --- Date and time patterns --------------------------------------------------

/// The patterns `AFDate_Format(n)` names by number, in order
/// (`STANDARD_DATE_PATTERNS`).
pub const STANDARD_DATE_PATTERNS: [&str; 14] = [
    "m/d",
    "m/d/yy",
    "mm/dd/yy",
    "mm/yy",
    "d-mmm",
    "d-mmm-yy",
    "dd-mmm-yy",
    "yy-mm-dd",
    "mmm-yy",
    "mmmm-yy",
    "mmm d, yyyy",
    "mmmm d, yyyy",
    "m/d/yy h:MM tt",
    "m/d/yy HH:MM",
];

/// The patterns `AFTime_Format(n)` names by number, in order
/// (`STANDARD_TIME_PATTERNS`).
pub const STANDARD_TIME_PATTERNS: [&str; 4] = ["HH:MM", "h:MM tt", "HH:MM:ss", "h:MM:ss tt"];

const LONGEST_PATTERN: usize = 64;
/// What stands between date groups. A colon is not among them: it belongs
/// to a time.
const SEPARATORS: &str = " /-.,";

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum DatePart {
    Year,
    Month,
    Day,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum Written {
    Digits,
    Letters,
    Separator,
}

/// The date groups (`DATE_GROUPS`): what each writes, and how.
fn date_group(run: &str) -> Option<(DatePart, Written)> {
    Some(match run {
        "yyyy" | "yy" => (DatePart::Year, Written::Digits),
        "mmmm" | "mmm" => (DatePart::Month, Written::Letters),
        "mm" | "m" => (DatePart::Month, Written::Digits),
        "dd" | "d" => (DatePart::Day, Written::Digits),
        _ => return None,
    })
}

/// `TIME` (`/(?:HH|H|hh|h):MM(?::ss)?(?: *tt)?/`) matched at `at`: where the
/// match ends. The regular expression's alternatives are tried in its
/// order, its optional parts taken when they are there.
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

/// A stretch of a pattern outside its time (`stretchOf`): how each part is
/// written and what each date group writes; `None` for anything a pattern
/// isn't made of.
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

/// `isDatePattern`: date groups (`yyyy yy mmmm mmm mm m dd d`, at least one,
/// the year, the month and the day each once at most), at most one time
/// (`HH`, `H`, `hh` or `h`, then `:MM`, then `:ss`, then `tt`), and spaces
/// and `/ - . ,` between them, 1 to 64 characters. Two groups written in
/// digits never touch.
pub fn is_date_pattern(pattern: &str) -> bool {
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
    // A time starts in digits, and ends in them unless `tt` closes it.
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
    // The time's own two ends are one time, not two groups that touch.
    !written.windows(2).enumerate().any(|(at, pair)| {
        pair[0] == Written::Digits && pair[1] == Written::Digits && at != time_start
    })
}

/// `isTimePattern`: a time and nothing else, at most 64 characters.
pub fn is_time_pattern(pattern: &str) -> bool {
    pattern.len() <= LONGEST_PATTERN && time_at(pattern.as_bytes(), 0) == Some(pattern.len())
}

// --- Formats -----------------------------------------------------------------

/// A text field's or a dropdown's format, as a read gives it
/// (`FormFieldFormat`): a date or a time with its pattern, or `other` for
/// any other format script. A kind this side doesn't know reads as `other`.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "lowercase")]
pub enum FormFieldFormat {
    Date {
        pattern: String,
    },
    Time {
        pattern: String,
    },
    #[serde(other)]
    Other,
}

impl FormFieldFormat {
    /// `isWritableFormat`: a date or a time, what a write makes.
    pub fn is_writable(&self) -> bool {
        !matches!(self, Self::Other)
    }

    fn kind(&self) -> Option<(Kind, &str)> {
        match self {
            Self::Date { pattern } => Some((Kind::Date, pattern)),
            Self::Time { pattern } => Some((Kind::Time, pattern)),
            Self::Other => None,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum Kind {
    Date,
    Time,
}

/// What a format of one kind is made of (`CALLS`).
struct Calls {
    format: &'static str,
    numbered_format: &'static str,
    keystroke: &'static str,
    numbered_keystroke: &'static str,
    standard: &'static [&'static str],
    is_pattern: fn(&str) -> bool,
}

fn calls(kind: Kind) -> Calls {
    match kind {
        Kind::Date => Calls {
            format: "AFDate_FormatEx",
            numbered_format: "AFDate_Format",
            keystroke: "AFDate_KeystrokeEx",
            numbered_keystroke: "AFDate_Keystroke",
            standard: &STANDARD_DATE_PATTERNS,
            is_pattern: is_date_pattern,
        },
        Kind::Time => Calls {
            format: "AFTime_FormatEx",
            numbered_format: "AFTime_Format",
            keystroke: "AFTime_KeystrokeEx",
            numbered_keystroke: "AFTime_Keystroke",
            standard: &STANDARD_TIME_PATTERNS,
            is_pattern: is_time_pattern,
        },
    }
}

/// A name no call has: [`pattern_of_call`] then reads the other wording only.
const NO_CALL: &str = "";

/// What one call says: its name, and its one argument, a string or a number.
enum Argument<'a> {
    Text(&'a str),
    Number(usize),
}

/// `^([A-Za-z_]+)\(\s*(?:(["'])([^"'\\]*)\2|(\d{1,2}))\s*\)$` over `source`.
fn one_call(source: &str) -> Option<(&str, Argument<'_>)> {
    let name_end = source.find(|c: char| !(c.is_ascii_alphabetic() || c == '_'))?;
    if name_end == 0 {
        return None;
    }
    let name = &source[..name_end];
    let rest = source[name_end..].strip_prefix('(')?;
    let rest = rest.trim_start_matches(js_space);
    let (argument, rest) =
        if let Some(quote) = rest.chars().next().filter(|c| matches!(c, '"' | '\'')) {
            let body = &rest[1..];
            let end = body.find(['"', '\'', '\\'])?;
            if !body[end..].starts_with(quote) {
                return None;
            }
            (Argument::Text(&body[..end]), &body[end + 1..])
        } else {
            let digits = rest.bytes().take_while(u8::is_ascii_digit).count();
            if !(1..=2).contains(&digits) {
                return None;
            }
            (Argument::Number(rest[..digits].parse().ok()?), &rest[digits..])
        };
    let rest = rest.trim_start_matches(js_space);
    (rest == ")").then_some((name, argument))
}

/// `patternOfCall`: the pattern of `kind` that `script` names when it is
/// exactly one call of `by_pattern` with a string that is one, or of
/// `by_number` with a number of the kind's standard list. White space around
/// the call and one `;` after it are no part of it.
fn pattern_of_call(script: &str, kind: Kind, by_pattern: &str, by_number: &str) -> Option<String> {
    let mut source = script.trim_matches(js_space);
    if let Some(cut) = source.strip_suffix(';') {
        source = cut.trim_end_matches(js_space);
    }
    let (name, argument) = one_call(source)?;
    let calls = calls(kind);
    match argument {
        Argument::Text(pattern) if name == by_pattern => {
            (calls.is_pattern)(pattern).then(|| pattern.to_owned())
        }
        Argument::Number(number) if name == by_number => {
            calls.standard.get(number).map(|p| (*p).to_owned())
        }
        _ => None,
    }
}

/// `formatOfScript`: the format a format script gives its field, as Acrobat
/// reads it: a date or a time for exactly one of their calls, `other` for
/// anything else.
pub fn format_of_script(script: &str) -> FormFieldFormat {
    for kind in [Kind::Date, Kind::Time] {
        let c = calls(kind);
        if let Some(pattern) = pattern_of_call(script, kind, c.format, c.numbered_format) {
            return match kind {
                Kind::Date => FormFieldFormat::Date { pattern },
                Kind::Time => FormFieldFormat::Time { pattern },
            };
        }
    }
    FormFieldFormat::Other
}

/// `loneScriptOf`: the script of a tree that is one JavaScript action and
/// nothing after it.
fn lone_script_of<D>(tree: &ActionTree<D>) -> Option<&str> {
    let root = tree.root.as_ref().filter(|_| !tree.incomplete)?;
    match &root.kind {
        ActionNodeKind::Javascript { script } if root.next.is_empty() => Some(script),
        _ => None,
    }
}

/// `formatOfActions`: the format a field's scripts give it; `None` without a
/// format script. The format script alone decides, as in Acrobat. A format
/// action that isn't one script is `other`.
pub fn format_of_actions<D>(actions: Option<&FieldActions<D>>) -> Option<FormFieldFormat> {
    let tree = actions?.format.as_ref()?;
    Some(lone_script_of(tree).map_or(FormFieldFormat::Other, format_of_script))
}

/// `takesFormat`: a text field and a dropdown have a format.
pub fn takes_format(family: FormFieldFamily) -> bool {
    matches!(family, FormFieldFamily::Text | FormFieldFamily::Combobox)
}

/// `formatScripts`: the two scripts a format is written as, in Acrobat's
/// wording. A time's keystroke script names the standard time pattern of
/// its shape by number, the only keystroke call Acrobat has for a time.
/// `None` for `other`, which is never written.
pub fn format_scripts(format: &FormFieldFormat) -> Option<(String, String)> {
    let (kind, pattern) = format.kind()?;
    let c = calls(kind);
    let keystroke = match kind {
        Kind::Date => format!("{}(\"{pattern}\");", c.keystroke),
        Kind::Time => format!("{}({});", c.numbered_keystroke, standard_time_like(pattern)),
    };
    Some((format!("{}(\"{pattern}\");", c.format), keystroke))
}

/// `standardTimeLike`: the number of the standard time pattern with the
/// shape of `pattern`, with or without seconds, on a 12 or a 24 hour clock.
fn standard_time_like(pattern: &str) -> u32 {
    (if pattern.contains("ss") { 2 } else { 0 }) + u32::from(pattern.contains("tt"))
}

/// `scriptsOfFormat`: a format as a write of the field's scripts, both
/// events set for a date or a time, both removed for `None`.
pub fn scripts_of_format(format: Option<&FormFieldFormat>) -> FieldActionsPatch {
    let script = |source: String| {
        Patch::Set(FieldScriptWrite { kind: JavascriptTag::Javascript, script: source, next: None })
    };
    match format.and_then(format_scripts) {
        None => FieldActionsPatch {
            keystroke: Patch::Clear,
            format: Patch::Clear,
            ..FieldActionsPatch::default()
        },
        Some((format, keystroke)) => FieldActionsPatch {
            keystroke: script(keystroke),
            format: script(format),
            ..FieldActionsPatch::default()
        },
    }
}

/// `isKeystrokeOfFormat`: whether a field's keystroke script is the one its
/// `format` goes with. A date's is its keystroke call with the same pattern,
/// by pattern or by number; a time's is `AFTime_Keystroke(n)` with any number
/// of the list, or the call with the same pattern.
pub fn is_keystroke_of_format<D>(
    format: &FormFieldFormat,
    keystroke: Option<&ActionTree<D>>,
) -> bool {
    let (Some((kind, pattern)), Some(script)) = (format.kind(), keystroke.and_then(lone_script_of))
    else {
        return false;
    };
    let c = calls(kind);
    match kind {
        Kind::Time => {
            pattern_of_call(script, kind, NO_CALL, c.numbered_keystroke).is_some()
                || pattern_of_call(script, kind, c.keystroke, NO_CALL).as_deref() == Some(pattern)
        }
        Kind::Date => {
            pattern_of_call(script, kind, c.keystroke, c.numbered_keystroke).as_deref()
                == Some(pattern)
        }
    }
}

/// `formatsEqual`: both none, or the same kind and pattern.
pub fn formats_equal(left: Option<&FormFieldFormat>, right: Option<&FormFieldFormat>) -> bool {
    left == right
}

fn invalid(message: &str, field: &str) -> EngineError {
    EngineError::with_message(EngineErrorCode::InvalidArg, message)
        .with_details(serde_json::json!({ "field": field }))
}

/// What a refused pattern is told, per kind (`PATTERN_RULE`).
fn pattern_rule(kind: Kind) -> &'static str {
    match kind {
        Kind::Date => {
            "a date format's pattern is 1 to 64 characters: the groups yyyy yy mmmm mmm mm m dd \
             d, at least one of them and the year, the month and the day each once at most, with \
             spaces and / - . , between them, and at most one time (HH, H, hh or h, then :MM, \
             then :ss and tt when it has them); two groups written in digits never touch"
        }
        Kind::Time => {
            "a time format's pattern is a time and nothing else, at most 64 characters: HH, H, \
             hh or h, then :MM, then :ss when it has seconds, then tt when it has one"
        }
    }
}

/// `checkedFormat`: the format a write names, checked: a date or a time with
/// a pattern of its kind, or `None` for `null`. Anything else is refused
/// with `InvalidArg` before a byte is written.
pub fn checked_format(format: &Value) -> Result<Option<FormFieldFormat>, EngineError> {
    if format.is_null() {
        return Ok(None);
    }
    let kind = format.get("kind").and_then(Value::as_str);
    let kind = match kind {
        Some("other") => {
            return Err(invalid(
                "a format of kind 'other' is read, never written: write its scripts in actions",
                "format",
            ));
        }
        Some("date") => Kind::Date,
        Some("time") => Kind::Time,
        _ => {
            return Err(invalid(
                "a field's format is a date or a time with a pattern, or null for none",
                "format",
            ));
        }
    };
    let pattern = format.get("pattern").and_then(Value::as_str);
    match pattern {
        Some(pattern) if (calls(kind).is_pattern)(pattern) => {
            let pattern = pattern.to_owned();
            Ok(Some(match kind {
                Kind::Date => FormFieldFormat::Date { pattern },
                Kind::Time => FormFieldFormat::Time { pattern },
            }))
        }
        _ => Err(invalid(pattern_rule(kind), "format.pattern")),
    }
}

/// `formatWriteOf`: the format a write gives a field of `family`, checked;
/// `None` when the write names none (`Keep`), `Some(None)` for `null`. A
/// format on a family that has none, and a format beside a script for one of
/// its own two events, are refused with `InvalidArg`.
pub fn format_write_of(
    format: &Patch<Value>,
    actions: Option<&FieldActionsPatch>,
    family: FormFieldFamily,
) -> Result<Option<Option<FormFieldFormat>>, EngineError> {
    let named = match format {
        Patch::Keep => return Ok(None),
        Patch::Clear => Value::Null,
        Patch::Set(value) => value.clone(),
    };
    if !takes_format(family) {
        return Err(invalid(
            &format!(
                "'format' does not apply to a {} field",
                serde_json::to_value(family)
                    .ok()
                    .and_then(|v| v.as_str().map(str::to_owned))
                    .unwrap_or_default()
            ),
            "format",
        ));
    }
    let checked = checked_format(&named)?;
    if let Some(actions) = actions {
        for (event, write) in [("format", &actions.format), ("keystroke", &actions.keystroke)] {
            if !write.is_keep() {
                return Err(invalid(
                    &format!(
                        "'format' writes the field's {event} script: name it or actions.{event}, not both"
                    ),
                    &format!("actions.{event}"),
                ));
            }
        }
    }
    Ok(Some(checked))
}

#[cfg(test)]
mod tests {
    //! `core/test/forms/datePattern.test.ts` and `format.test.ts`.

    use super::*;
    use crate::features::actions::reader::ActionNode;

    fn tree(script: &str) -> ActionTree {
        ActionTree {
            root: Some(ActionNode {
                kind: ActionNodeKind::Javascript { script: script.to_owned() },
                subtype: "JavaScript".to_owned(),
                next: Vec::new(),
            }),
            incomplete: false,
            warnings: Vec::new(),
        }
    }

    fn date(pattern: &str) -> FormFieldFormat {
        FormFieldFormat::Date { pattern: pattern.to_owned() }
    }

    fn time(pattern: &str) -> FormFieldFormat {
        FormFieldFormat::Time { pattern: pattern.to_owned() }
    }

    fn refusal<T: std::fmt::Debug>(result: Result<T, EngineError>) -> (EngineErrorCode, Value) {
        let error = result.expect_err("refused");
        (error.code, error.details.as_ref().map_or(Value::Null, |d| d["field"].clone()))
    }

    #[test]
    fn a_date_pattern_is_date_groups_with_separators_and_at_most_one_time() {
        let spaced = format!("dd{}yyyy", " ".repeat(58));
        let mut yes: Vec<&str> = STANDARD_DATE_PATTERNS.to_vec();
        yes.extend([
            "dd/mm/yyyy",
            "yyyy-mm-dd",
            "dd.mm.yyyy",
            "d mmmm, yyyy",
            "d",
            "yyyy",
            "mmmm",
            "dmmmyy",
            "ddmmmmyyyy",
            "d H:MM",
            "d/m hh:MMtt",
            "dd.mm.yyyy h:MM  tt",
            "yyyy-mm-dd HH:MM:ss",
            "h:MM tt, d mmm",
            "h:MM ttdd",
            &spaced,
            "yyyy/mm/dd",
            "d mmmm yyyy",
            "mmm yyyy",
            "dd",
            "yy-m",
        ]);
        for pattern in yes {
            assert!(is_date_pattern(pattern), "{pattern}");
        }
    }

    #[test]
    fn what_is_no_date_pattern() {
        let long = format!("{}yyyy", "dd/".repeat(20));
        let spaced = format!("dd{}yyyy", " ".repeat(59));
        let mut no: Vec<&str> = STANDARD_TIME_PATTERNS.to_vec();
        no.extend([
            // A part written twice.
            "yyyy/mm/mm/dd",
            "dd/dd/mm/yyyy",
            "yyyy-yy-mm-dd",
            "d/m/yyyy d",
            "mmm mm d",
            "mmmm d, mmm yyyy",
            "d d",
            "yy yyyy",
            "m/d/yy HH:MM m",
            &long,
            // A time alone.
            "H:MM",
            "hh:MM tt",
            "hh:MMtt",
            "HH:MM:ss tt",
            "HH:MM, ",
            " h:MM",
            // Digits that touch.
            "yyyymmdd",
            "ddmmyyyy",
            "ddmm",
            "mmyy",
            "dmyy",
            "ddHH:MM",
            "yyyyh:MM tt",
            "HH:MMdd",
            "HH:MM:ssyyyy",
            // A time in another shape.
            "HH.MM",
            "HH.MM.ss",
            "HH MM",
            "HHMM",
            "H:M",
            "HH:M",
            "HH:MM.ss",
            "HH:MM ss",
            "MM:HH",
            "ss:MM:HH",
            "HH",
            "MM",
            "ss",
            "tt",
            "h tt",
            "tt h:MM",
            "HH:MM - HH:MM",
            "h:MM tt tt",
            "dd:mm:yyyy",
            "dd: mm",
            // Anything else.
            "",
            " ",
            "/-.",
            "yyy",
            "yyyyy",
            "mmmmm",
            "ddd",
            "dddd",
            "y",
            "s",
            "sss",
            "t",
            "HHH",
            "D",
            "Y",
            "x",
            "dd/mm/yyyy\"",
            "d 'of' mmmm",
            "dd\\mm",
            "dd;mm",
            "dd(mm)",
            "dd\nmm",
            "dd_mm",
            "yyyy年mm月",
            &spaced,
        ]);
        for pattern in no {
            assert!(!is_date_pattern(pattern), "{pattern:?}");
        }
    }

    #[test]
    fn a_time_pattern_is_a_time_and_nothing_else() {
        let spaced = format!("h:MM{}tt", " ".repeat(58));
        let mut yes: Vec<&str> = STANDARD_TIME_PATTERNS.to_vec();
        yes.extend([
            "H:MM",
            "hh:MM",
            "h:MM",
            "hh:MM tt",
            "hh:MMtt",
            "h:MM  tt",
            "H:MM:ss",
            "hh:MM:sstt",
            "HH:MM:ss tt",
            &spaced,
        ]);
        for pattern in yes {
            assert!(is_time_pattern(pattern), "{pattern}");
        }
        let too_long = format!("h:MM{}tt", " ".repeat(59));
        let mut no: Vec<&str> = STANDARD_DATE_PATTERNS.to_vec();
        no.extend([
            "dd/mm/yyyy",
            "yyyy",
            "d",
            "yyyy-mm-dd HH:MM:ss",
            "h:MM tt, d mmm",
            "h:MM ttdd",
            "d H:MM",
            "HH.MM",
            "HH MM",
            "HHMM",
            "H:M",
            "HH:M",
            "HH:MM.ss",
            "HH:MM ss",
            "HH:MM:s",
            "MM:HH",
            "MM:ss",
            "ss:MM:HH",
            "HH",
            "MM",
            "ss",
            "tt",
            "h tt",
            "tt h:MM",
            "HH:MM - HH:MM",
            "h:MM tt tt",
            "h:MM t",
            "HHH:MM",
            "hhh:MM",
            "",
            " ",
            " HH:MM",
            "HH:MM ",
            "HH:MM tt ",
            "HH:MM,",
            "at HH:MM",
            "HH:MM\"",
            "HH:MM\n",
            "HH:MM\\",
            &too_long,
        ]);
        for pattern in no {
            assert!(!is_time_pattern(pattern), "{pattern:?}");
        }
    }

    #[test]
    fn acrobats_date_and_time_calls_read_as_a_date_and_a_time() {
        assert_eq!(format_of_script("AFDate_FormatEx(\"m/d/yy\");"), date("m/d/yy"));
        assert_eq!(format_of_script("AFDate_FormatEx(\"dd/mm/yyyy\")"), date("dd/mm/yyyy"));
        assert_eq!(format_of_script("AFDate_FormatEx('dd.mm.yyyy');"), date("dd.mm.yyyy"));
        assert_eq!(format_of_script("  AFDate_FormatEx( \"yyyy-mm-dd\" ) ;\n"), date("yyyy-mm-dd"));
        assert_eq!(format_of_script("AFTime_FormatEx(\"HH:MM:ss\");"), time("HH:MM:ss"));
        assert_eq!(format_of_script("AFDate_Format(0);"), date("m/d"));
        assert_eq!(format_of_script("AFDate_Format(2);"), date("mm/dd/yy"));
        assert_eq!(format_of_script("AFDate_Format( 13 )"), date("m/d/yy HH:MM"));
        assert_eq!(format_of_script("AFTime_Format(3);"), time("h:MM:ss tt"));
    }

    #[test]
    fn every_other_script_is_another_format() {
        for script in [
            "",
            "AFNumber_Format(2, 0, 0, 0, \"\", true);",
            "AFPercent_Format(2, 0);",
            "AFSpecial_Format(0);",
            "event.value = \"x\";",
            "var seen = 1; AFDate_FormatEx(\"m/d/yy\");",
            "AFDate_FormatEx(\"m/d/yy\"); app.beep();",
            "AFDate_FormatEx(\"m/d/yy\");;",
            "AFDate_FormatEx(\"m/d/yy\")\n// a comment",
            "if (true) AFDate_FormatEx(\"m/d/yy\");",
            "AFDate_FormatEx(pattern);",
            "AFDate_FormatEx(\"m/d/yy\", 1);",
            "AFDate_FormatEx(\"m/d\" + \"/yy\");",
            "AFDate_FormatEx(\"m/d/yy');",
            "AFDate_FormatEx(\"m\\/d\");",
            "this.AFDate_FormatEx(\"m/d/yy\");",
            "afdate_formatex(\"m/d/yy\");",
            "AFDate_FormatEx(\"dddd, mmmm d, yyyy\");",
            "AFDate_FormatEx(\"yyyymmdd\");",
            "AFTime_FormatEx(\"HH.MM\");",
            "AFDate_FormatEx(\"\");",
            "AFDate_FormatEx(\"yyyy/mm/mm/dd\");",
            "AFDate_FormatEx(\"HH:MM\");",
            "AFDate_FormatEx(\"h:MM:ss tt\");",
            "AFTime_FormatEx(\"dd/mm/yyyy\");",
            "AFTime_FormatEx(\"yyyy\");",
            "AFTime_FormatEx(\"m/d/yy HH:MM\");",
            "AFDate_Format(14);",
            "AFDate_Format(-1);",
            "AFDate_Format(2.5);",
            "AFTime_Format(4);",
            "AFDate_KeystrokeEx(\"m/d/yy\");",
            "AFDate_Keystroke(0);",
            "AFDate_FormatEx(2);",
            "AFDate_Format(\"m/d/yy\");",
        ] {
            assert_eq!(format_of_script(script), FormFieldFormat::Other, "{script:?}");
        }
        let padded = format!("AFDate_Format(1){}x", " ".repeat(200_000));
        assert_eq!(format_of_script(&padded), FormFieldFormat::Other);
    }

    #[test]
    fn the_format_script_decides_and_one_that_is_not_one_script_is_another_format() {
        let actions = |format: Option<&str>, keystroke: Option<&str>| FieldActions::<
            crate::features::actions::destination::PdfDestination,
        > {
            format: format.map(tree),
            keystroke: keystroke.map(tree),
            validate: None,
            calculate: None,
        };
        assert_eq!(
            format_of_actions::<crate::features::actions::destination::PdfDestination>(None),
            None
        );
        assert_eq!(format_of_actions(Some(&actions(None, None))), None);
        assert_eq!(
            format_of_actions(Some(&actions(None, Some("AFDate_KeystrokeEx(\"m/d/yy\");")))),
            None
        );
        assert_eq!(
            format_of_actions(Some(&actions(
                Some("AFDate_FormatEx(\"m/d/yy\");"),
                Some("event.rc = true;")
            ))),
            Some(date("m/d/yy"))
        );
        let mut chained = tree("AFDate_FormatEx(\"m/d/yy\");");
        chained.root.as_mut().unwrap().next.push(tree("app.beep();").root.unwrap());
        let mut incomplete = tree("AFDate_FormatEx(\"m/d/yy\");");
        incomplete.incomplete = true;
        let mut rootless = tree("AFDate_FormatEx(\"m/d/yy\");");
        rootless.root = None;
        for format in [chained, incomplete, rootless] {
            let read = FieldActions {
                format: Some(format),
                keystroke: None,
                validate: None,
                calculate: None,
            };
            assert_eq!(format_of_actions(Some(&read)), Some(FormFieldFormat::Other));
        }
    }

    #[test]
    fn a_format_is_written_as_the_two_scripts_acrobat_writes_and_reads_back() {
        assert_eq!(
            format_scripts(&date("dd/mm/yyyy")),
            Some((
                "AFDate_FormatEx(\"dd/mm/yyyy\");".to_owned(),
                "AFDate_KeystrokeEx(\"dd/mm/yyyy\");".to_owned()
            ))
        );
        let numbered = |pattern: &str| format_scripts(&time(pattern)).unwrap().1;
        for (patterns, expected) in [
            (["HH:MM", "h:MM tt", "HH:MM:ss", "h:MM:ss tt"], [0, 1, 2, 3]),
            (["H:MM", "hh:MM tt", "hh:MM:ss", "H:MM:sstt"], [0, 1, 2, 3]),
        ] {
            for (pattern, n) in patterns.iter().zip(expected) {
                assert_eq!(numbered(pattern), format!("AFTime_Keystroke({n});"));
            }
        }
        let written = scripts_of_format(Some(&date("m/d")));
        assert_eq!(written.format.as_set().unwrap().script, "AFDate_FormatEx(\"m/d\");");
        assert_eq!(written.keystroke.as_set().unwrap().script, "AFDate_KeystrokeEx(\"m/d\");");
        assert!(written.validate.is_keep() && written.calculate.is_keep());
        let removed = scripts_of_format(None);
        assert!(removed.format.is_clear() && removed.keystroke.is_clear());
        for format in [date("dd/mm/yyyy"), date("mmmm d, yyyy"), time("HH:MM:ss")] {
            let (script, keystroke) = format_scripts(&format).unwrap();
            assert_eq!(format_of_script(&script), format);
            assert!(is_keystroke_of_format(&format, Some(&tree(&keystroke))));
        }
    }

    #[test]
    fn a_keystroke_script_is_the_formats_own_by_pattern_or_by_number_in_its_kind() {
        let d = date("m/d");
        let own = |format: &FormFieldFormat, script: &str| {
            is_keystroke_of_format(format, Some(&tree(script)))
        };
        assert!(own(&d, "AFDate_KeystrokeEx(\"m/d\");"));
        assert!(own(&d, "AFDate_Keystroke(0);"));
        assert!(!own(&d, "AFDate_Keystroke(1);"));
        assert!(!own(&d, "AFDate_KeystrokeEx(\"m/d/yy\");"));
        assert!(!own(&d, "AFTime_KeystrokeEx(\"m/d\");"));
        assert!(!own(&d, "event.rc = true;"));
        assert!(!is_keystroke_of_format::<crate::features::actions::destination::PdfDestination>(
            &d, None
        ));
        let t = time("HH:MM");
        assert!(own(&t, "AFTime_Keystroke(0);"));
        assert!(own(&t, "AFTime_Keystroke(3);"));
        assert!(!own(&t, "AFTime_Keystroke(4);"));
        assert!(own(&t, "AFTime_KeystrokeEx(\"HH:MM\");"));
        assert!(!own(&t, "AFTime_KeystrokeEx(\"h:MM tt\");"));
        assert!(!own(&t, "AFDate_KeystrokeEx(\"HH:MM\");"));
        assert!(!own(&t, "AFDate_Keystroke(0);"));
        assert!(!own(&t, "event.rc = true;"));
    }

    #[test]
    fn a_write_takes_a_date_or_a_time_with_a_pattern_of_its_kind_or_null() {
        use serde_json::json;
        assert_eq!(checked_format(&Value::Null).unwrap(), None);
        assert_eq!(
            checked_format(&json!({ "kind": "date", "pattern": "dd/mm/yyyy" })).unwrap(),
            Some(date("dd/mm/yyyy"))
        );
        assert_eq!(
            checked_format(&json!({ "kind": "time", "pattern": "HH:MM", "script": "x" })).unwrap(),
            Some(time("HH:MM"))
        );
        let invalid = (EngineErrorCode::InvalidArg, json!("format"));
        for format in [
            json!({ "kind": "other" }),
            json!({ "kind": "number", "pattern": "0.00" }),
            json!("dd/mm/yyyy"),
        ] {
            assert_eq!(refusal(checked_format(&format)), invalid, "{format}");
        }
        let pattern = (EngineErrorCode::InvalidArg, json!("format.pattern"));
        for given in
            [json!(""), json!("dddd"), json!("d\");x(\""), json!("d\\m"), Value::Null, json!(5)]
        {
            let format = json!({ "kind": "date", "pattern": given });
            assert_eq!(refusal(checked_format(&format)), pattern, "{format}");
        }
        assert_eq!(
            checked_format(&json!({ "kind": "date", "pattern": "m/d/yy HH:MM" })).unwrap(),
            Some(date("m/d/yy HH:MM"))
        );
        for given in ["HH:MM", "h:MM tt", "HH:MM:ss"] {
            assert_eq!(
                refusal(checked_format(&json!({ "kind": "date", "pattern": given }))),
                pattern
            );
        }
        for given in ["dd/mm/yyyy", "yyyy", "m/d/yy HH:MM", "h:MM tt, d mmm", "HH.MM", ""] {
            assert_eq!(
                refusal(checked_format(&json!({ "kind": "time", "pattern": given }))),
                pattern
            );
        }
        let told = |format: Value| checked_format(&format).unwrap_err().message;
        let date_rule = told(json!({ "kind": "date", "pattern": "HH:MM" }));
        assert!(date_rule.contains("a date format's pattern"));
        assert!(date_rule.contains("yyyy yy mmmm mmm mm m dd d, at least one of them"));
        let time_rule = told(json!({ "kind": "time", "pattern": "dd/mm/yyyy" }));
        assert!(time_rule.contains("a time format's pattern is a time and nothing else"));
        assert!(!time_rule.contains("yyyy"));
    }

    #[test]
    fn a_format_goes_on_a_text_field_and_a_dropdown_and_not_beside_its_own_scripts() {
        use serde_json::json;
        let d = json!({ "kind": "date", "pattern": "dd/mm/yyyy" });
        assert!(takes_format(FormFieldFamily::Text) && takes_format(FormFieldFamily::Combobox));
        assert!(!takes_format(FormFieldFamily::Listbox));
        assert_eq!(format_write_of(&Patch::Keep, None, FormFieldFamily::Text).unwrap(), None);
        assert_eq!(format_write_of(&Patch::Keep, None, FormFieldFamily::Checkbox).unwrap(), None);
        assert_eq!(
            format_write_of(&Patch::Set(d.clone()), None, FormFieldFamily::Text).unwrap(),
            Some(Some(date("dd/mm/yyyy")))
        );
        assert_eq!(
            format_write_of(&Patch::Clear, None, FormFieldFamily::Combobox).unwrap(),
            Some(None)
        );
        for family in [
            FormFieldFamily::Checkbox,
            FormFieldFamily::Radio,
            FormFieldFamily::Listbox,
            FormFieldFamily::Pushbutton,
            FormFieldFamily::Signature,
        ] {
            for format in [Patch::Set(d.clone()), Patch::Clear] {
                assert_eq!(
                    refusal(format_write_of(&format, None, family)),
                    (EngineErrorCode::InvalidArg, json!("format"))
                );
            }
        }
        let script = || FieldScriptWrite {
            kind: JavascriptTag::Javascript,
            script: "event.rc = true;".to_owned(),
            next: None,
        };
        let beside = |actions: FieldActionsPatch| {
            format_write_of(&Patch::Set(d.clone()), Some(&actions), FormFieldFamily::Text)
        };
        assert_eq!(
            refusal(beside(FieldActionsPatch {
                format: Patch::Set(script()),
                ..Default::default()
            })),
            (EngineErrorCode::InvalidArg, json!("actions.format"))
        );
        assert_eq!(
            refusal(beside(FieldActionsPatch {
                keystroke: Patch::Set(script()),
                ..Default::default()
            })),
            (EngineErrorCode::InvalidArg, json!("actions.keystroke"))
        );
        assert_eq!(
            refusal(beside(FieldActionsPatch { keystroke: Patch::Clear, ..Default::default() })),
            (EngineErrorCode::InvalidArg, json!("actions.keystroke"))
        );
        assert_eq!(
            beside(FieldActionsPatch {
                validate: Patch::Set(script()),
                calculate: Patch::Clear,
                ..Default::default()
            })
            .unwrap(),
            Some(Some(date("dd/mm/yyyy")))
        );
    }

    #[test]
    fn two_formats_are_the_same_by_kind_and_pattern() {
        let d = date("m/d");
        assert!(formats_equal(None, None));
        assert!(formats_equal(Some(&d), Some(&date("m/d"))));
        assert!(formats_equal(Some(&FormFieldFormat::Other), Some(&FormFieldFormat::Other)));
        assert!(!formats_equal(Some(&d), None));
        assert!(!formats_equal(Some(&d), Some(&time("m/d"))));
        assert!(!formats_equal(Some(&d), Some(&date("m/d/yy"))));
        assert!(!formats_equal(Some(&d), Some(&FormFieldFormat::Other)));
    }

    #[test]
    fn a_format_reads_as_its_kind_and_an_unknown_kind_as_other() {
        use serde_json::json;
        assert_eq!(
            serde_json::to_value(date("m/d")).unwrap(),
            json!({ "kind": "date", "pattern": "m/d" })
        );
        assert_eq!(
            serde_json::to_value(FormFieldFormat::Other).unwrap(),
            json!({ "kind": "other" })
        );
        let read: FormFieldFormat = serde_json::from_value(json!({ "kind": "number" })).unwrap();
        assert_eq!(read, FormFieldFormat::Other);
    }

    #[test]
    fn a_purpose_is_1_to_256_characters_with_no_space_at_either_end_and_no_nul() {
        let long = "p".repeat(256);
        for purpose in [
            "name",
            "date-signed",
            "acme:po number",
            &long,
            "naïve",
            "署名",
            "two\nlines",
            "a(b)\\c",
        ] {
            assert!(is_field_purpose(purpose), "{purpose}");
            assert_eq!(
                checked_purpose(Some(&Value::String(purpose.to_owned()))).unwrap().as_deref(),
                Some(purpose)
            );
        }
        let too_long = "p".repeat(257);
        for purpose in [
            "",
            " ",
            " name",
            "name ",
            "\tname",
            "name\n",
            "\u{a0}name",
            "na\0me",
            "\u{feff}name",
            &too_long,
        ] {
            assert!(!is_field_purpose(purpose), "{purpose:?}");
            assert_eq!(
                refusal(checked_purpose(Some(&Value::String(purpose.to_owned())))),
                (EngineErrorCode::InvalidArg, serde_json::json!("purpose"))
            );
        }
        // A character outside the basic plane counts as two, as JavaScript counts it.
        assert!(is_field_purpose(&"😀".repeat(128)));
        assert!(!is_field_purpose(&"😀".repeat(129)));
        assert_eq!(checked_purpose(None).unwrap(), None);
        assert_eq!(checked_purpose(Some(&Value::Null)).unwrap(), None);
        assert_eq!(
            refusal(checked_purpose(Some(&serde_json::json!(5)))),
            (EngineErrorCode::InvalidArg, serde_json::json!("purpose"))
        );
    }
}
