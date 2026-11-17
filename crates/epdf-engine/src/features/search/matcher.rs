//! The whole match stage in one call.
//!
//! G5. A transcription of `core/src/search/matcher.ts`.
//!
//! Page text in, match ranges out, in the original text's space. An engine
//! with a pre-folded corpus for the default fold skips this and calls
//! [`super::literal::match_literal`] on the cached text directly; everything
//! else comes through here.

use super::corpus::PageCorpus;
use super::fold::{FoldOptions, fold_text};
use super::literal::{LiteralQuery, fold_needle, match_folded_needle, match_literal};
use super::{SearchRegexIssue, compile, dialect, match_compiled_regex, match_regex};
use crate::features::text::TextRange;

/// A query in either mode.
#[derive(Debug, Clone, PartialEq, Eq, Default, serde::Serialize, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SearchQuery {
    pub text: String,
    /// Every flag defaults to false rather than being required, because the
    /// wire omits a flag it is not setting and a missing `matchCase` means
    /// case-folded rather than malformed.
    #[serde(default)]
    pub regex: bool,
    #[serde(default)]
    pub match_case: bool,
    #[serde(default)]
    pub match_diacritics: bool,
    #[serde(default)]
    pub whole_word: bool,
    #[serde(default)]
    pub ignore_whitespace: bool,
}

impl SearchQuery {
    fn literal(&self) -> LiteralQuery {
        LiteralQuery {
            text: self.text.clone(),
            match_case: self.match_case,
            match_diacritics: self.match_diacritics,
            whole_word: self.whole_word,
            ignore_whitespace: self.ignore_whitespace,
        }
    }
}

/// Every match on a page.
///
/// Regex mode does not fold. That is the TypeScript's behaviour and it is the
/// right one: a pattern says what it means literally, and folding it first
/// would change what its character classes match.
pub fn match_page_text(
    text: &str,
    query: &SearchQuery,
) -> Result<Vec<TextRange>, SearchRegexIssue> {
    if query.regex {
        return match_regex(text, &query.text, query.match_case, query.whole_word);
    }
    let literal = query.literal();
    Ok(match_literal(&fold_text(text, literal.fold_options()), &literal))
}

/// A query prepared once to run against many pages: the regex compiled, or
/// the literal needle folded.
#[derive(Debug)]
pub enum CompiledQuery {
    Regex {
        regex: regex::Regex,
        anchored: bool,
        whole_word: bool,
    },
    /// `needle` is `None` when it folds to nothing, which matches nothing.
    Literal {
        literal: LiteralQuery,
        needle: Option<Vec<u16>>,
    },
}

impl CompiledQuery {
    /// Prepare `query`, whose pattern the caller has already validated
    /// (`validate_search_query`). Compiling can still refuse it, as
    /// [`match_page_text`] would on the first page.
    pub fn new(query: &SearchQuery) -> Result<Self, SearchRegexIssue> {
        if query.regex {
            return Ok(Self::Regex {
                regex: compile(&query.text, query.match_case)?,
                anchored: dialect::has_line_anchor(&query.text),
                whole_word: query.whole_word,
            });
        }
        let literal = query.literal();
        let needle = fold_needle(&literal);
        Ok(Self::Literal { literal, needle })
    }

    /// Every match on a page, as [`match_page_text`] finds them in the
    /// page's text. A literal query with no flags searches the corpus's
    /// cached default fold; any other folds the page text for this query.
    pub fn match_corpus(&self, corpus: &PageCorpus) -> Vec<TextRange> {
        match self {
            Self::Regex { regex, anchored, whole_word } => {
                match_compiled_regex(&corpus.snapshot.text, regex, *anchored, *whole_word)
            }
            Self::Literal { needle: None, .. } => Vec::new(),
            Self::Literal { literal, needle: Some(needle) } => {
                let options = literal.fold_options();
                if options == FoldOptions::default() {
                    return match_folded_needle(&corpus.folded, &corpus.units, needle, literal);
                }
                let folded = fold_text(&corpus.snapshot.text, options);
                match_folded_needle(&folded, &corpus.units, needle, literal)
            }
        }
    }
}
