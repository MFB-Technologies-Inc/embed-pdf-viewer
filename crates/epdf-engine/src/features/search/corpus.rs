//! The per-session page corpus a search reads instead of the page.
//!
//! G5. The engine's `pageCorpusCache.ts`
//! (`services/src/features/search/internal/`): each page's text snapshot and
//! its default fold, built the first time a search reads the page and reused
//! by every later slice and query until the document changes.
//!
//! The version key is the session's mutation sequence, which every write
//! moves (`crate::session::save::finish_mutation`, and `install`). An entry
//! from an earlier sequence is never served: the first lookup at a new
//! sequence empties the cache, so a page is keyed by its object number and
//! the sequence together. The session drops the whole cache on install and
//! close.
//!
//! Capped at [`MAX_CACHED_PAGES`], because search touches every page of an
//! arbitrarily large document. Past the cap the entry inserted first goes
//! first; a hit does not move an entry, as upstream's does not.

use std::collections::{HashMap, VecDeque};
use std::rc::Rc;

use super::fold::{FoldOptions, FoldedText, fold_text};
use crate::features::text::charmap::PageTextSnapshot;
use crate::identity::PageObjectNumber;

/// Most pages a session keeps (`MAX_CACHED_PAGES`).
pub const MAX_CACHED_PAGES: usize = 512;

/// One page's text, ready to match (`PageCorpus`).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PageCorpus {
    /// The page text as [`crate::features::text::reader::read_page_text`]
    /// read it, for converting match ranges to character space.
    pub snapshot: PageTextSnapshot,
    /// The default fold of `snapshot.text`, the one a literal query with no
    /// flags searches.
    pub folded: FoldedText,
    /// `snapshot.text` in UTF-16 code units, the space match ranges, snippets
    /// and the word tests index.
    pub units: Vec<u16>,
}

impl PageCorpus {
    pub fn new(snapshot: PageTextSnapshot) -> Self {
        let folded = fold_text(&snapshot.text, FoldOptions::default());
        let units = snapshot.text.encode_utf16().collect();
        Self { snapshot, folded, units }
    }

    /// The length of the page text in UTF-16 code units.
    pub fn text_len_utf16(&self) -> u32 {
        self.units.len() as u32
    }
}

/// A session's page corpora, valid at one mutation sequence.
#[derive(Debug, Default)]
pub struct SearchCorpus {
    sequence: u64,
    pages: HashMap<PageObjectNumber, Rc<PageCorpus>>,
    /// Insertion order, oldest first.
    order: VecDeque<PageObjectNumber>,
}

impl SearchCorpus {
    /// The page's corpus built at `sequence`, if there is one.
    pub fn get(&mut self, page: PageObjectNumber, sequence: u64) -> Option<Rc<PageCorpus>> {
        self.advance_to(sequence);
        self.pages.get(&page).cloned()
    }

    /// Keep a corpus built at `sequence`, evicting the oldest entry past the
    /// cap.
    pub fn insert(&mut self, page: PageObjectNumber, sequence: u64, corpus: Rc<PageCorpus>) {
        self.advance_to(sequence);
        if self.pages.insert(page, corpus).is_some() {
            self.order.retain(|kept| *kept != page);
        }
        self.order.push_back(page);
        while self.order.len() > MAX_CACHED_PAGES {
            if let Some(oldest) = self.order.pop_front() {
                self.pages.remove(&oldest);
            }
        }
    }

    /// Forget every page.
    pub fn clear(&mut self) {
        self.pages.clear();
        self.order.clear();
    }

    /// How many pages are kept.
    pub fn len(&self) -> usize {
        self.pages.len()
    }

    pub fn is_empty(&self) -> bool {
        self.pages.is_empty()
    }

    /// Empty the cache when the document has changed since it was filled.
    fn advance_to(&mut self, sequence: u64) {
        if sequence != self.sequence {
            self.clear();
            self.sequence = sequence;
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn page(n: u32) -> PageObjectNumber {
        PageObjectNumber::new(n).expect("a page object number")
    }

    fn corpus(text: &str) -> Rc<PageCorpus> {
        let char_count = text.encode_utf16().count() as u32;
        Rc::new(PageCorpus::new(PageTextSnapshot {
            text: text.to_owned(),
            char_count,
            char_map: Vec::new(),
        }))
    }

    #[test]
    fn an_entry_is_served_only_at_the_sequence_it_was_built_at() {
        let mut cache = SearchCorpus::default();
        cache.insert(page(3), 0, corpus("one"));
        assert_eq!(cache.get(page(3), 0).expect("kept").snapshot.text, "one");
        assert!(cache.get(page(3), 1).is_none());
        // The move to sequence 1 emptied it, so going back finds nothing.
        assert!(cache.get(page(3), 0).is_none());
    }

    #[test]
    fn past_the_cap_the_first_inserted_page_goes_first() {
        let mut cache = SearchCorpus::default();
        for n in 1..=MAX_CACHED_PAGES as u32 {
            cache.insert(page(n), 7, corpus("x"));
        }
        // A hit does not refresh an entry.
        assert!(cache.get(page(1), 7).is_some());
        cache.insert(page(10_000), 7, corpus("y"));
        assert_eq!(cache.len(), MAX_CACHED_PAGES);
        assert!(cache.get(page(1), 7).is_none());
        assert!(cache.get(page(2), 7).is_some());
        assert!(cache.get(page(10_000), 7).is_some());
    }

    #[test]
    fn a_corpus_carries_the_texts_utf16_units() {
        let built = corpus("a\u{1F600}b");
        assert_eq!(built.text_len_utf16(), 4);
        assert_eq!(built.units, "a\u{1F600}b".encode_utf16().collect::<Vec<_>>());
        assert_eq!(built.folded, fold_text("a\u{1F600}b", FoldOptions::default()));
    }
}
