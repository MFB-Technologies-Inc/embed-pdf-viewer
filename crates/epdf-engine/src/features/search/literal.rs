//! Literal search, over folded text.
//!
//! G5. A transcription of `core/src/search/literal.ts`.
//!
//! Both the haystack and the needle go through the same fold, so the
//! comparison is between two normalized forms. The word-character definition
//! lives here and is shared with the regex whole-word post-filter, so the
//! toggle means one thing in both modes: "caf" never whole-word matches inside
//! "café", regex or not.

use super::fold::{FoldOptions, FoldedText, fold_text, to_original_range_in};
use crate::features::text::TextRange;

/// A literal query.
#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct LiteralQuery {
    pub text: String,
    pub match_case: bool,
    pub match_diacritics: bool,
    pub whole_word: bool,
    pub ignore_whitespace: bool,
}

impl LiteralQuery {
    /// The fold both sides of this query need.
    ///
    /// The haystack must be folded with exactly these options. A corpus cache
    /// keys its pre-folded text on them, and the default fold is the
    /// persistable one; a query with other flags folds the page at query time.
    pub fn fold_options(&self) -> FoldOptions {
        FoldOptions {
            keep_case: self.match_case,
            keep_marks: self.match_diacritics,
            drop_whitespace: self.ignore_whitespace,
        }
    }
}

/// One definition of "word character" for the whole search subsystem.
///
/// Unicode letters, numbers and underscore, not the ASCII set JavaScript's
/// `\b` uses. The difference is the point: with `\b`, whole-word search would
/// mean something different for accented text than for ASCII, and the toggle
/// would behave differently in literal and regex mode.
fn is_word_char(c: char) -> bool {
    c.is_alphanumeric() || c == '_'
}

/// Whether the character ending at a UTF-16 index is a word character.
pub fn word_before(text: &str, utf16_index: usize) -> bool {
    if utf16_index == 0 {
        return false;
    }
    char_ending_at(text, utf16_index).is_some_and(is_word_char)
}

/// Whether the character starting at a UTF-16 index is a word character.
pub fn word_at(text: &str, utf16_index: usize) -> bool {
    char_starting_at(text, utf16_index).is_some_and(is_word_char)
}

fn char_starting_at(text: &str, utf16_index: usize) -> Option<char> {
    unit_char_starting_at(&text.encode_utf16().collect::<Vec<_>>(), utf16_index)
}

fn char_ending_at(text: &str, utf16_index: usize) -> Option<char> {
    unit_char_ending_at(&text.encode_utf16().collect::<Vec<_>>(), utf16_index)
}

/// [`word_before`] over text in UTF-16 code units.
fn unit_word_before(units: &[u16], utf16_index: usize) -> bool {
    if utf16_index == 0 {
        return false;
    }
    unit_char_ending_at(units, utf16_index).is_some_and(is_word_char)
}

/// [`word_at`] over text in UTF-16 code units.
fn unit_word_at(units: &[u16], utf16_index: usize) -> bool {
    unit_char_starting_at(units, utf16_index).is_some_and(is_word_char)
}

fn is_low_surrogate(unit: u16) -> bool {
    (0xDC00..=0xDFFF).contains(&unit)
}

/// The character at a UTF-16 index of well-formed text.
///
/// Inside a surrogate pair the character is the pair, which is what a word
/// test should see rather than half of one. Past the end there is none.
fn unit_char_starting_at(units: &[u16], utf16_index: usize) -> Option<char> {
    let unit = *units.get(utf16_index)?;
    let start =
        if is_low_surrogate(unit) && utf16_index > 0 { utf16_index - 1 } else { utf16_index };
    char::decode_utf16(units[start..].iter().copied()).next()?.ok()
}

/// The last character that starts before a UTF-16 index of well-formed text:
/// the one ending there, or the pair an index inside one splits.
fn unit_char_ending_at(units: &[u16], utf16_index: usize) -> Option<char> {
    let end = utf16_index.min(units.len());
    if end == 0 {
        return None;
    }
    unit_char_starting_at(units, end - 1)
}

/// Whether a character is a combining mark, by the same rule the fold uses.
///
/// The same function, not a second opinion. A literal search that disagreed
/// with the fold about what a mark is would refuse matches the fold had
/// already stripped the marks from.
fn is_mark(c: char) -> bool {
    super::fold::is_combining_mark(c)
}

/// Whether the folded hit at `at` sits on word boundaries.
///
/// Checked on the folded plane, except under `ignore_whitespace`: dropping
/// whitespace glues neighbouring words together ("i n v o i c e 42" folds to
/// "invoice42"), so there the boundaries are read off the ORIGINAL text
/// around the mapped range.
///
/// `hay` is `haystack.folded` and `original` is `haystack.original`, both in
/// UTF-16 code units.
fn is_whole_word_hit(
    haystack: &FoldedText,
    hay: &[u16],
    original: &[u16],
    at: usize,
    needle_length: usize,
    query: &LiteralQuery,
) -> bool {
    if !query.ignore_whitespace {
        return !unit_word_before(hay, at) && !unit_word_at(hay, at + needle_length);
    }
    let Some(range) = to_original_range_in(haystack, original, at, needle_length) else {
        return false;
    };
    let (start, end) = (range.start as usize, (range.start + range.count) as usize);
    !unit_word_before(original, start) && !unit_word_at(original, end)
}

/// A literal query's needle in UTF-16 code units, folded with
/// [`LiteralQuery::fold_options`] as its haystack must be. `None` when the
/// folded needle is empty or only whitespace, which matches nothing.
pub fn fold_needle(query: &LiteralQuery) -> Option<Vec<u16>> {
    let needle = fold_text(&query.text, query.fold_options()).folded;
    // A needle that is empty or only whitespace would match every collapsed
    // space on the page, which is not what anybody typed it for.
    if needle.trim().is_empty() {
        return None;
    }
    Some(needle.encode_utf16().collect())
}

/// Every non-overlapping literal match, in the original text's UTF-16 space.
///
/// `haystack` must have been folded with [`LiteralQuery::fold_options`] for
/// this query. Folding it differently is not a wrong answer, it is an answer
/// to a different question, and nothing here can detect the mismatch.
pub fn match_literal(haystack: &FoldedText, query: &LiteralQuery) -> Vec<TextRange> {
    let Some(pin) = fold_needle(query) else {
        return Vec::new();
    };
    let original: Vec<u16> = haystack.original.encode_utf16().collect();
    match_folded_needle(haystack, &original, &pin, query)
}

/// [`match_literal`] with the needle already folded by [`fold_needle`] and
/// `haystack.original` already in UTF-16 code units, so a query folds its
/// needle once and a page encodes its text once.
pub fn match_folded_needle(
    haystack: &FoldedText,
    original: &[u16],
    pin: &[u16],
    query: &LiteralQuery,
) -> Vec<TextRange> {
    // Both strings are searched in UTF-16 units, because that is the space
    // the map is indexed in and the space the wire reports.
    let hay: Vec<u16> = haystack.folded.encode_utf16().collect();
    if pin.is_empty() || pin.len() > hay.len() {
        return Vec::new();
    }

    let mut out = Vec::new();
    let mut from = 0usize;
    while from + pin.len() <= hay.len() {
        let Some(offset) = hay[from..].windows(pin.len()).position(|w| w == pin) else {
            break;
        };
        let at = from + offset;

        if query.whole_word && !is_whole_word_hit(haystack, &hay, original, at, pin.len(), query) {
            from = at + 1;
            continue;
        }

        // A diacritic-sensitive fold is decomposed, so a bare needle would
        // otherwise match just the base letters of an accented character:
        // "cafe" ending immediately before the combining acute of "café". A
        // match may not end mid-character.
        if query.match_diacritics
            && at + pin.len() < hay.len()
            && unit_char_starting_at(&hay, at + pin.len()).is_some_and(is_mark)
        {
            from = at + 1;
            continue;
        }

        if let Some(range) = to_original_range_in(haystack, original, at, pin.len()) {
            out.push(range);
        }
        from = at + pin.len();
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn the_unit_lookups_see_a_surrogate_pair_whole_from_either_half() {
        let units: Vec<u16> = "x\u{1F600}y".encode_utf16().collect();
        let starting: Vec<_> = (0..=4).map(|i| unit_char_starting_at(&units, i)).collect();
        assert_eq!(starting, [Some('x'), Some('\u{1F600}'), Some('\u{1F600}'), Some('y'), None]);
        let ending: Vec<_> = [0, 1, 2, 3, 4, 9].map(|i| unit_char_ending_at(&units, i)).to_vec();
        assert_eq!(
            ending,
            [None, Some('x'), Some('\u{1F600}'), Some('\u{1F600}'), Some('y'), Some('y')]
        );
    }
}
