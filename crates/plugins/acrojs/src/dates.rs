//! `dates.ts` (833b40015, #1147): dates by a pattern (`dd/mm/yyyy`,
//! `h:MM tt`), without running a script: what a form's date field shows and
//! takes, by the same rules its format and keystroke scripts run.
//!
//! The TypeScript installs the prelude into an object of its own and calls
//! its `util.printd` and `__acrojsParseDate` there. This crate ships the
//! prelude as data and runs no JavaScript, so the two are transcribed from
//! `prelude.ts` (`printd`, `parseDateFrom`) over ECMAScript's date arithmetic.

use serde::{Deserialize, Serialize};

/// `DateParts`: a calendar date and a time of day, as a form field shows
/// them. It has no time zone: a date in a field is the day its text says.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
pub struct DateParts {
    pub year: f64,
    /// 1 to 12.
    pub month: f64,
    /// 1 to 31.
    pub day: f64,
    /// 0 to 23.
    pub hour: f64,
    pub minute: f64,
    pub second: f64,
}

/// `DatePartsInput`: a date to write, its time of day midnight when left out.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
pub struct DatePartsInput {
    pub year: f64,
    pub month: f64,
    pub day: f64,
    #[serde(default)]
    pub hour: Option<f64>,
    #[serde(default)]
    pub minute: Option<f64>,
    #[serde(default)]
    pub second: Option<f64>,
}

/// The day `today` is, for the parts a text leaves out.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
pub struct Today {
    pub year: f64,
    pub month: f64,
    pub day: f64,
}

const MONTH_SHORT: [&str; 12] =
    ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
const MONTH_LONG: [&str; 12] = [
    "January",
    "February",
    "March",
    "April",
    "May",
    "June",
    "July",
    "August",
    "September",
    "October",
    "November",
    "December",
];

const MS_PER_DAY: f64 = 86_400_000.0;

/// ECMAScript `ToIntegerOrInfinity`.
fn integer(x: f64) -> f64 {
    if x.is_nan() { 0.0 } else { x.trunc() }
}

/// Days from 1970-01-01 to the first of `month` (1 to 12) of `year`.
fn days_from_civil(year: f64, month: f64) -> f64 {
    let y = if month <= 2.0 { year - 1.0 } else { year };
    let era = (y / 400.0).floor();
    let yoe = y - era * 400.0;
    let mp = (month + 9.0) % 12.0;
    let doy = ((153.0 * mp + 2.0) / 5.0).floor();
    let doe = yoe * 365.0 + (yoe / 4.0).floor() - (yoe / 100.0).floor() + doy;
    era * 146_097.0 + doe - 719_468.0
}

/// ECMAScript `MakeDay(year, month, date)`: `month` from 0, any of them
/// past its range carried over. NaN for a non-finite part.
fn make_day(year: f64, month: f64, date: f64) -> f64 {
    if !(year.is_finite() && month.is_finite() && date.is_finite()) {
        return f64::NAN;
    }
    let (y, m, dt) = (integer(year), integer(month), integer(date));
    let ym = y + (m / 12.0).floor();
    let mn = m.rem_euclid(12.0);
    days_from_civil(ym, mn + 1.0) + dt - 1.0
}

/// ECMAScript `TimeClip` over a day and a time in it.
fn make_date(day: f64, ms: f64) -> f64 {
    let t = day * MS_PER_DAY + ms;
    if !t.is_finite() || t.abs() > 8.64e15 { f64::NAN } else { integer(t) + 0.0 }
}

/// A time value's UTC parts: year, month from 0, day, hours, minutes,
/// seconds. `None` for NaN.
fn utc_parts(t: f64) -> Option<(f64, f64, f64, f64, f64, f64)> {
    if t.is_nan() {
        return None;
    }
    let days = (t / MS_PER_DAY).floor();
    let in_day = t - days * MS_PER_DAY;
    let z = days + 719_468.0;
    let era = (z / 146_097.0).floor();
    let doe = z - era * 146_097.0;
    let yoe = ((doe - (doe / 1460.0).floor() + (doe / 36_524.0).floor()
        - (doe / 146_096.0).floor())
        / 365.0)
        .floor();
    let doy = doe - (365.0 * yoe + (yoe / 4.0).floor() - (yoe / 100.0).floor());
    let mp = ((5.0 * doy + 2.0) / 153.0).floor();
    let day = doy - ((153.0 * mp + 2.0) / 5.0).floor() + 1.0;
    let month = if mp < 10.0 { mp + 3.0 } else { mp - 9.0 };
    let year = yoe + era * 400.0 + if month <= 2.0 { 1.0 } else { 0.0 };
    let hour = (in_day / 3_600_000.0).floor();
    let minute = ((in_day % 3_600_000.0) / 60_000.0).floor();
    let second = ((in_day % 60_000.0) / 1000.0).floor();
    Some((year, month - 1.0, day, hour, minute, second))
}

/// `Date.UTC(year, month, day)`: a year from 0 to 99 is 1900 and on.
fn date_utc(year: f64, month0: f64, day: f64) -> f64 {
    let year = if year.is_finite() && (0.0..=99.0).contains(&integer(year)) {
        1900.0 + integer(year)
    } else {
        year
    };
    make_date(make_day(year, month0, day), 0.0)
}

/// `String(number)`.
fn text(n: f64) -> String {
    crate::js::number_to_string(n)
}

/// `pad(value, 2)`: `String(value).padStart(2, '0')`.
fn pad(n: f64) -> String {
    let s = text(n);
    if s.encode_utf16().count() >= 2 { s } else { format!("{}{s}", "0".repeat(2 - s.len())) }
}

/// The token `printd` writes at `at` of `format`, longest first, and its
/// length: `/yyyy|mmmm|mmm|yy|mm|dd|HH|hh|MM|ss|tt|m|d|H|h|M/`.
fn printd_token(rest: &str) -> Option<&'static str> {
    ["yyyy", "mmmm", "mmm", "yy", "mm", "dd", "HH", "hh", "MM", "ss", "tt", "m", "d", "H", "h", "M"]
        .into_iter()
        .find(|token| rest.starts_with(token))
}

/// `util.printd(format, date)` with no script running: the date's UTC parts
/// as they are. Month names are English, `tt` is `am` or `pm`, and what isn't
/// one of the pattern's letter groups is written as it is.
fn printd(format: &str, t: f64) -> String {
    let parts = utc_parts(t);
    let (year, month, day, hour, minute, second) =
        parts.unwrap_or((f64::NAN, f64::NAN, f64::NAN, f64::NAN, f64::NAN, f64::NAN));
    let hour12 = if hour % 12.0 == 0.0 { 12.0 } else { hour % 12.0 };
    let month_name = |names: &[&str; 12]| {
        if month.is_nan() { "undefined".to_string() } else { names[month as usize].to_string() }
    };
    let mut out = String::new();
    let mut at = 0;
    while at < format.len() {
        let rest = &format[at..];
        let Some(token) = printd_token(rest) else {
            let c = rest.chars().next().unwrap_or_default();
            out.push(c);
            at += c.len_utf8();
            continue;
        };
        out.push_str(&match token {
            "yyyy" => text(year),
            "yy" => pad(year % 100.0),
            "mmmm" => month_name(&MONTH_LONG),
            "mmm" => month_name(&MONTH_SHORT),
            "mm" => pad(month + 1.0),
            "m" => text(month + 1.0),
            "dd" => pad(day),
            "d" => text(day),
            "HH" => pad(hour),
            "hh" => pad(hour12),
            "h" => text(hour12),
            "H" => text(hour),
            "MM" => pad(minute),
            "M" => text(minute),
            "ss" => pad(second),
            _ => (if hour < 12.0 { "am" } else { "pm" }).to_string(),
        });
        at += token.len();
    }
    out
}

/// `formatDate(date, pattern)`: `date` as `pattern` shows it, the text a
/// format script writes for it: `formatDate({ year: 2026, month: 3, day: 5 },
/// "dd/mm/yyyy")` is `05/03/2026`.
pub fn format_date(date: &DatePartsInput, pattern: &str) -> String {
    // `new Date(0)`, then `setUTCFullYear(year, month - 1, day)` and
    // `setUTCHours(hour ?? 0, minute ?? 0, second ?? 0, 0)`.
    let day = make_day(date.year, date.month - 1.0, date.day);
    let ms = date.hour.unwrap_or(0.0) * 3_600_000.0
        + date.minute.unwrap_or(0.0) * 60_000.0
        + date.second.unwrap_or(0.0) * 1000.0;
    printd(pattern, make_date(day, ms))
}

/// `\d` at byte `at`.
fn digit(text: &[u8], at: usize) -> bool {
    text.get(at).is_some_and(u8::is_ascii_digit)
}

/// `/(\d{1,2}):(\d{2})(?::(\d{2}))?\s*(am|pm)?/i` matched at `start`: its
/// end, the hours, the minutes, the seconds and the marker.
fn time_at(text: &str, start: usize) -> Option<(usize, f64, f64, f64, String)> {
    let bytes = text.as_bytes();
    let number = |from: usize, to: usize| text[from..to].parse::<f64>().unwrap_or(f64::NAN);
    // `\d{1,2}` is greedy, and gives a digit back when the rest fails.
    for hours_len in [2, 1] {
        if !(0..hours_len).all(|i| digit(bytes, start + i)) {
            continue;
        }
        let mut at = start + hours_len;
        if bytes.get(at) != Some(&b':') || !digit(bytes, at + 1) || !digit(bytes, at + 2) {
            continue;
        }
        let hours = number(start, start + hours_len);
        let minutes = number(at + 1, at + 3);
        at += 3;
        let mut seconds = 0.0;
        if bytes.get(at) == Some(&b':') && digit(bytes, at + 1) && digit(bytes, at + 2) {
            seconds = number(at + 1, at + 3);
            at += 3;
        }
        let spaces: usize = text[at..]
            .chars()
            .take_while(|c| crate::js::is_whitespace(*c))
            .map(char::len_utf8)
            .sum();
        at += spaces;
        let mut marker = String::new();
        let next = text.get(at..at + 2).map(str::to_ascii_lowercase);
        if let Some(word @ ("am" | "pm")) = next.as_deref() {
            marker = word.to_string();
            at += 2;
        }
        return Some((at, hours, minutes, seconds, marker));
    }
    None
}

/// `parseDate(text, pattern, today)`: the date `text` says under `pattern`,
/// or `None` when it says none. The numbers in the text are the year, the
/// month and the day in the order the pattern has them; a month may be its
/// English name; a two-digit year below 50 is in this century; a time is
/// hours and minutes with a colon. A day its month doesn't have is no date.
/// `today` gives the year and the month the text leaves out, and a day left
/// out is the first.
pub fn parse_date(raw: &str, pattern: &str, today: &Today) -> Option<DateParts> {
    let text = crate::js::trim(raw);
    if text.is_empty() {
        return None;
    }
    let lower = text.to_lowercase();
    let named_month = MONTH_SHORT
        .iter()
        .position(|name| lower.contains(&name.to_lowercase()))
        .map(|index| index as f64 + 1.0);
    let time = text
        .char_indices()
        .map(|(at, _)| at)
        .find_map(|start| time_at(text, start).map(|found| (start, found)));
    let (mut hours, mut minutes, mut seconds) = (0.0, 0.0, 0.0);
    let date_text = match &time {
        Some((start, (end, h, m, s, marker))) => {
            hours = *h;
            minutes = *m;
            seconds = *s;
            if marker == "pm" && hours < 12.0 {
                hours += 12.0;
            }
            if marker == "am" && hours == 12.0 {
                hours = 0.0;
            }
            if hours > 23.0 || minutes > 59.0 || seconds > 59.0 {
                return None;
            }
            format!("{}{}", &text[..*start], &text[*end..])
        }
        None => text.to_string(),
    };
    let mut numbers: Vec<f64> = Vec::new();
    let bytes = date_text.as_bytes();
    let mut at = 0;
    while at < bytes.len() {
        if bytes[at].is_ascii_digit() {
            let end = at + bytes[at..].iter().take_while(|b| b.is_ascii_digit()).count();
            numbers.push(date_text[at..end].parse::<f64>().unwrap_or(f64::NAN));
            at = end;
        } else {
            at += 1;
        }
    }
    // `/yyyy|mmmm|mmm|yy|mm|dd|m|d/g` over the pattern: each part once, in
    // the order the pattern first names it.
    let mut order: Vec<char> = Vec::new();
    let mut at = 0;
    while at < pattern.len() {
        let rest = &pattern[at..];
        match ["yyyy", "mmmm", "mmm", "yy", "mm", "dd", "m", "d"]
            .into_iter()
            .find(|token| rest.starts_with(token))
        {
            Some(token) => {
                let kind = token.chars().next().unwrap_or('d');
                if !order.contains(&kind) {
                    order.push(kind);
                }
                at += token.len();
            }
            None => at += rest.chars().next().map_or(1, char::len_utf8),
        }
    }
    let (mut year, mut month, mut day) = (None, named_month, None);
    let mut cursor = 0;
    for kind in order {
        if kind == 'm' && named_month.is_some() {
            continue;
        }
        let Some(&value) = numbers.get(cursor) else { break };
        cursor += 1;
        match kind {
            'y' => year = Some(value),
            'm' => month = Some(value),
            _ => day = Some(value),
        }
    }
    if year.is_none() && month.is_none() && day.is_none() && time.is_none() {
        return None;
    }
    let year = match year {
        None => today.year,
        Some(y) if y < 100.0 => {
            if y < 50.0 {
                2000.0 + y
            } else {
                1900.0 + y
            }
        }
        Some(y) => y,
    };
    let month = month.unwrap_or(today.month);
    let day = day.unwrap_or(1.0);
    if !(1.0..=12.0).contains(&month) || !(1.0..=31.0).contains(&day) {
        return None;
    }
    let (probe_year, probe_month, probe_day, ..) = utc_parts(date_utc(year, month - 1.0, day))?;
    if probe_year != year || probe_month != month - 1.0 || probe_day != day {
        return None;
    }
    Some(DateParts { year, month, day, hour: hours, minute: minutes, second: seconds })
}
