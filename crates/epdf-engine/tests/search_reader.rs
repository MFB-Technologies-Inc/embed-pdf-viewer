//! G5: searching a document, slice by slice.
//!
//! The pieces below this are pinned to the TypeScript by vectors: the fold,
//! the literal and regex matchers, the snippet, the charmap conversion and
//! the layout. What this covers is the pipeline that composes them, where the
//! failures are about budgets, ordering and resumption rather than about text.

mod common;

use epdf_engine::cancel::CancelToken;
use epdf_engine::error::EngineErrorCode;
use epdf_engine::features::search::matcher::SearchQuery;
use epdf_engine::features::search::reader::{SearchLimit, SearchMatch, SearchRequest, search};
use epdf_engine::identity::PageObjectNumber;
use epdf_engine::session::authority::SessionAuthority;
use epdf_engine::session::document::DocumentSession;

fn report() -> DocumentSession {
    let mut session = DocumentSession::new();
    session
        .open_with(
            test_support::fixture(test_support::REPORT),
            None,
            SessionAuthority::unrestricted(),
        )
        .expect("open");
    session
}

fn literal(text: &str) -> SearchQuery {
    SearchQuery { text: text.to_string(), ..SearchQuery::default() }
}

/// A needle rare enough that a scan finishes inside the default match budget.
///
/// "the" appears often enough in report.pdf to exhaust the 256-match default
/// after four pages, which is correct behaviour and useless for testing the
/// scan mechanics. "figure" appears 27 times across the document.
const RARE: &str = "figure";

fn request(query: SearchQuery) -> SearchRequest {
    SearchRequest { query, ..SearchRequest::default() }
}

fn pages(session: &mut DocumentSession) -> Vec<PageObjectNumber> {
    session.all_records().expect("records").iter().map(|r| r.page_object_number).collect()
}

#[test]
fn a_query_finds_matches_with_segments_and_a_snippet() {
    let mut session = report();
    let slice = search(
        &mut session,
        &SearchRequest { snippets: true, ..request(literal("the")) },
        &CancelToken::never(),
    )
    .expect("search");

    assert!(!slice.matches.is_empty(), "report.pdf contains 'the'");
    assert_eq!(slice.page_count, 14);
    for m in &slice.matches {
        assert!(m.count > 0, "a match covers no characters");
        assert!(!m.segments.is_empty(), "a match on real text has no segments to highlight");
        let snippet = m.snippet.as_ref().expect("a request for snippets carries one");
        assert_eq!(snippet.matched.to_lowercase(), "the", "the match part is not the match");
    }
}

#[test]
fn a_search_without_snippets_withholds_the_text_and_keeps_the_geometry() {
    // A permission distinction rather than a verbosity one. A snippet IS
    // extracted text, so leaving it out is the search for a document whose
    // owner denies extraction: the reader can still FIND, and nothing
    // readable crosses the boundary. The geometry is not text and stays.
    // Snippets are off unless asked for.
    let mut session = report();
    let slice =
        search(&mut session, &request(literal("the")), &CancelToken::never()).expect("search");

    assert!(!slice.matches.is_empty());
    for m in &slice.matches {
        assert!(m.snippet.is_none(), "a search without snippets leaked one");
        assert!(!m.segments.is_empty(), "a search without snippets withheld the geometry too");
    }
}

#[test]
fn a_fractional_limit_rounds_down_as_upstreams_clamp_does() {
    let mut session = report();
    let slice = search(
        &mut session,
        &SearchRequest {
            limit: Some(SearchLimit { pages: Some(2.9), ..Default::default() }),
            ..request(literal("the"))
        },
        &CancelToken::never(),
    )
    .expect("search");
    assert_eq!(slice.pages_searched, 2);

    let slice = search(
        &mut session,
        &SearchRequest {
            limit: Some(SearchLimit { pages: Some(0.0), ..Default::default() }),
            ..request(literal("the"))
        },
        &CancelToken::never(),
    )
    .expect("search");
    assert_eq!(slice.pages_searched, 1, "a limit below one still searches a page");
}

#[test]
fn an_empty_needle_scans_nothing_rather_than_matching_everything() {
    // A whitespace-only needle folds to a single space, which would match
    // every collapsed run on every page. The scan is not started at all.
    let mut session = report();
    for text in ["", "   ", "\t\n"] {
        let slice =
            search(&mut session, &request(literal(text)), &CancelToken::never()).expect("search");
        assert!(slice.matches.is_empty(), "{text:?} matched something");
        assert_eq!(slice.pages_searched, 0, "{text:?} burned a scan");
        assert!(slice.next_cursor.is_none());
    }
}

#[test]
fn a_page_budget_stops_the_slice_and_yields_a_cursor() {
    let mut session = report();
    let slice = search(
        &mut session,
        &SearchRequest {
            limit: Some(SearchLimit { pages: Some(3.0), ..Default::default() }),
            ..request(literal("the"))
        },
        &CancelToken::never(),
    )
    .expect("search");

    assert_eq!(slice.pages_searched, 3);
    assert_eq!(slice.page_count, 14);
    assert!(slice.next_cursor.is_some(), "an unfinished scan produced no cursor");
}

#[test]
fn a_cursor_resumes_where_the_last_slice_stopped() {
    let mut session = report();
    let first = search(
        &mut session,
        &SearchRequest {
            limit: Some(SearchLimit { pages: Some(5.0), ..Default::default() }),
            ..request(literal(RARE))
        },
        &CancelToken::never(),
    )
    .expect("first");
    assert_eq!(first.pages_searched, 5);

    let second = search(
        &mut session,
        &SearchRequest {
            cursor: first.next_cursor.clone(),
            limit: Some(SearchLimit { pages: Some(5.0), ..Default::default() }),
            ..request(literal(RARE))
        },
        &CancelToken::never(),
    )
    .expect("second");
    assert_eq!(second.pages_searched, 10, "the second slice restarted rather than resumed");

    // And the two slices cover different pages.
    let first_pages: std::collections::BTreeSet<u32> =
        first.matches.iter().map(|m| m.page.get()).collect();
    let second_pages: std::collections::BTreeSet<u32> =
        second.matches.iter().map(|m| m.page.get()).collect();
    assert!(first_pages.is_disjoint(&second_pages), "the slices reported matches on the same page",);
}

#[test]
fn a_finished_scan_yields_no_cursor() {
    let mut session = report();
    let slice =
        search(&mut session, &request(literal(RARE)), &CancelToken::never()).expect("search");
    assert_eq!(slice.pages_searched, slice.page_count);
    assert!(slice.next_cursor.is_none());
}

#[test]
fn a_cursor_from_a_different_query_is_refused() {
    // A cursor is not transferable. Resuming one query's position inside
    // another would report matches for a needle nobody searched for.
    let mut session = report();
    let first = search(
        &mut session,
        &SearchRequest {
            limit: Some(SearchLimit { pages: Some(2.0), ..Default::default() }),
            ..request(literal("the"))
        },
        &CancelToken::never(),
    )
    .expect("first");

    let refused = search(
        &mut session,
        &SearchRequest { cursor: first.next_cursor.clone(), ..request(literal("different")) },
        &CancelToken::never(),
    )
    .expect_err("a cursor from another query");
    assert_eq!(refused.code, EngineErrorCode::InvalidArg);
    assert!(refused.message.contains("different query"), "{}", refused.message);
}

#[test]
fn a_cursor_from_before_a_mutation_is_refused() {
    // Resuming over mutated content could serve stale pages or, worse, text a
    // redaction just removed.
    let mut session = report();
    let first = search(
        &mut session,
        &SearchRequest {
            limit: Some(SearchLimit { pages: Some(2.0), ..Default::default() }),
            ..request(literal("the"))
        },
        &CancelToken::never(),
    )
    .expect("first");

    session.note_mutation();

    let refused = search(
        &mut session,
        &SearchRequest { cursor: first.next_cursor.clone(), ..request(literal("the")) },
        &CancelToken::never(),
    )
    .expect_err("a stale cursor");
    assert_eq!(refused.code, EngineErrorCode::InvalidArg);
    assert!(refused.message.contains("changed"), "{}", refused.message);
}

#[test]
fn a_malformed_cursor_is_refused_rather_than_ignored() {
    let mut session = report();
    for cursor in ["", "{}", "not json", r#"{"v":2,"seq":0,"key":"x","start":0,"scanned":0}"#] {
        let refused = search(
            &mut session,
            &SearchRequest { cursor: Some(cursor.to_string()), ..request(literal("the")) },
            &CancelToken::never(),
        )
        .expect_err("malformed");
        assert_eq!(refused.code, EngineErrorCode::InvalidArg, "{cursor:?}");
    }
}

#[test]
fn a_start_page_rotates_the_scan_order_and_wraps() {
    // Viewport-first: a viewer searches from the page being read, and the
    // scan wraps rather than stopping at the end.
    let mut session = report();
    let all = pages(&mut session);
    let from_middle = search(
        &mut session,
        &SearchRequest { from: Some(all[7]), ..request(literal(RARE)) },
        &CancelToken::never(),
    )
    .expect("search");

    assert_eq!(from_middle.pages_searched, 14, "the scan did not wrap");
    // The first match comes from page 7 or later, not from page 0.
    let first_page = from_middle.matches.first().expect("matches").page;
    let first_index = all.iter().position(|p| *p == first_page).expect("known page");
    assert!(first_index >= 7, "the scan began at the start rather than at the requested page");
}

#[test]
fn an_unknown_start_page_is_not_found() {
    let mut session = report();
    let missing = PageObjectNumber::new(99_999).expect("number");
    let refused = search(
        &mut session,
        &SearchRequest { from: Some(missing), ..request(literal("the")) },
        &CancelToken::never(),
    )
    .expect_err("unknown page");
    assert_eq!(refused.code, EngineErrorCode::NotFound);
}

#[test]
fn a_match_budget_stops_the_slice() {
    let mut session = report();
    let slice = search(
        &mut session,
        &SearchRequest {
            limit: Some(SearchLimit { matches: Some(1.0), ..Default::default() }),
            ..request(literal("the"))
        },
        &CancelToken::never(),
    )
    .expect("search");

    // The budget is checked between pages, so a page's matches are never
    // split: one page's worth may exceed the budget and that is deliberate.
    assert!(slice.pages_searched >= 1);
    assert!(slice.next_cursor.is_some(), "a budgeted slice produced no cursor");
}

#[test]
fn a_regex_query_finds_what_a_literal_one_would_not() {
    let mut session = report();
    let slice = search(
        &mut session,
        &request(SearchQuery {
            text: r"\bthe\b".to_string(),
            regex: true,
            ..SearchQuery::default()
        }),
        &CancelToken::never(),
    )
    .expect("search");
    assert!(!slice.matches.is_empty(), "a word-boundary pattern found nothing");
}

#[test]
fn an_invalid_regex_is_refused() {
    let mut session = report();
    let refused = search(
        &mut session,
        &request(SearchQuery {
            text: "(unclosed".to_string(),
            regex: true,
            ..SearchQuery::default()
        }),
        &CancelToken::never(),
    )
    .expect_err("invalid pattern");
    assert_eq!(refused.code, EngineErrorCode::InvalidArg);
}

#[test]
fn a_cancelled_search_reports_aborted() {
    let mut session = report();
    let cancel = CancelToken::never();
    cancel.cancel();
    assert_eq!(
        search(&mut session, &request(literal("the")), &cancel).expect_err("cancelled").code,
        EngineErrorCode::Aborted,
    );
}

#[test]
fn searching_a_closed_session_is_refused() {
    let mut session = report();
    session.close();
    assert_eq!(
        search(&mut session, &request(literal("the")), &CancelToken::never())
            .expect_err("closed")
            .code,
        EngineErrorCode::DocNotOpen,
    );
}

#[test]
fn skip_resumes_by_position_without_a_cursor() {
    // The trusted resume. A caller that pins content versions externally,
    // which the cloud wire does by carrying the search epoch in its URL, has
    // no cursor to present and no sequence to check against; what it has is
    // how far it already got. Without this it would re-scan from page one
    // and re-serve every match it has already shown.
    let mut session = report();
    let whole = search(&mut session, &request(literal(RARE)), &CancelToken::never())
        .expect("the whole scan");

    let skipped = search(
        &mut session,
        &SearchRequest { skip: Some(7.0), ..request(literal(RARE)) },
        &CancelToken::never(),
    )
    .expect("the resumed scan");

    assert_eq!(skipped.pages_searched, whole.pages_searched, "the scan did not reach the end");

    let order = pages(&mut session);
    let skipped_pages: Vec<_> = order[..7].to_vec();
    let expected: Vec<_> =
        whole.matches.iter().filter(|m| !skipped_pages.contains(&m.page)).map(|m| m.page).collect();
    let actual: Vec<_> = skipped.matches.iter().map(|m| m.page).collect();
    assert_eq!(actual, expected, "a skipped scan is not the tail of the whole one");
    assert!(!expected.is_empty(), "the skip consumed the whole document");
}

#[test]
fn a_skip_past_the_end_scans_nothing_rather_than_wrapping() {
    // Clamped, not wrapped. A caller whose external pin is stale asks for a
    // position past the document and gets an empty slice, which reads as
    // "nothing left"; wrapping would silently re-serve page one as though it
    // were new.
    let mut session = report();
    let slice = search(
        &mut session,
        &SearchRequest { skip: Some(9_999.0), ..request(literal(RARE)) },
        &CancelToken::never(),
    )
    .expect("search");

    assert!(slice.matches.is_empty(), "a skip past the end still found matches");
    assert_eq!(slice.pages_searched, slice.page_count, "the skip was not clamped to the document");
    assert!(slice.next_cursor.is_none(), "a finished scan offered a cursor");
}

#[test]
fn a_start_page_and_a_skip_apply_together() {
    // Not either/or. `from` chooses where the scan ORDER begins and
    // `skip` how far into that order the caller already is, so a viewport-
    // first scan that resumes needs both. Treating them as alternatives
    // would make the second slice of a viewport-first search start over at
    // page one.
    let mut session = report();
    let order = pages(&mut session);
    let fifth = order[4];

    let from_fifth = search(
        &mut session,
        &SearchRequest { from: Some(fifth), ..request(literal(RARE)) },
        &CancelToken::never(),
    )
    .expect("the viewport-first scan");

    let resumed = search(
        &mut session,
        &SearchRequest { from: Some(fifth), skip: Some(3.0), ..request(literal(RARE)) },
        &CancelToken::never(),
    )
    .expect("the resumed viewport-first scan");

    // The rotated order is pages 5..14 then 1..4. Skipping three of it drops
    // the matches on the first three of those and keeps the rest, which is
    // only true if the rotation happened as well as the skip.
    let dropped: Vec<_> = order[4..7].to_vec();
    let expected: Vec<_> =
        from_fifth.matches.iter().filter(|m| !dropped.contains(&m.page)).map(|m| m.page).collect();
    let actual: Vec<_> = resumed.matches.iter().map(|m| m.page).collect();
    assert_eq!(actual, expected, "the start page and the skip did not both apply");
    assert!(!expected.is_empty(), "the fixture has nothing left after the skip");
}

#[test]
fn a_query_the_dialect_refuses_is_refused_before_any_page_is_read() {
    // Up front, not on the first page. A document with no pages would
    // otherwise accept a broken pattern and answer with an empty slice,
    // which a caller reads as "no matches" rather than "your query is
    // wrong". The refusal also names the issue, because a search box shows
    // it.
    let mut session = report();
    let error = search(
        &mut session,
        &request(SearchQuery { text: "a(?=b)".into(), regex: true, ..SearchQuery::default() }),
        &CancelToken::never(),
    )
    .expect_err("a lookaround is not in the dialect");
    assert_eq!(error.code, EngineErrorCode::InvalidArg);
    assert!(error.message.contains("lookaround"), "the issue is not named: {}", error.message);
}

#[test]
fn diacritic_sensitive_matching_is_refused_for_a_regex() {
    // The one rejected flag combination, and the one most likely to be
    // waved through: both flags are individually valid and the matcher
    // simply ignores the second. Ignoring it means a caller who asked for
    // "café" not to match "cafe" gets matches that do, with no indication
    // that the flag did nothing.
    let mut session = report();
    let error = search(
        &mut session,
        &request(SearchQuery {
            text: "caf.".into(),
            regex: true,
            match_diacritics: true,
            ..SearchQuery::default()
        }),
        &CancelToken::never(),
    )
    .expect_err("regex plus diacritics is refused");
    assert_eq!(error.code, EngineErrorCode::InvalidArg);
    assert!(
        error.message.contains("diacritics-with-regex"),
        "the issue is not named: {}",
        error.message,
    );
}

#[test]
fn a_cursor_will_not_resume_against_a_conflicting_start_page() {
    // The cursor owns the position. A `from` beside it that disagrees
    // is a caller bug, and letting the cursor quietly win would hand back
    // matches from a part of the document the caller did not ask about.
    let mut session = report();
    let order = pages(&mut session);
    let first = search(
        &mut session,
        &SearchRequest {
            limit: Some(SearchLimit { pages: Some(2.0), ..Default::default() }),
            ..request(literal(RARE))
        },
        &CancelToken::never(),
    )
    .expect("first");
    let cursor = first.next_cursor.expect("the scan is not finished");

    let error = search(
        &mut session,
        &SearchRequest {
            cursor: Some(cursor.clone()),
            from: Some(order[5]),
            ..request(literal(RARE))
        },
        &CancelToken::never(),
    )
    .expect_err("a conflicting start page is refused");
    assert_eq!(error.code, EngineErrorCode::InvalidArg);
    assert!(
        error.message.contains("`from` conflicts"),
        "the conflict is not named: {}",
        error.message
    );

    // And the same cursor without it still resumes, so the refusal is about
    // the conflict rather than about the cursor.
    search(
        &mut session,
        &SearchRequest { cursor: Some(cursor), ..request(literal(RARE)) },
        &CancelToken::never(),
    )
    .expect("the cursor alone resumes");
}

// ── `packages/engine/main/test/search.test.ts` (`runSearchConformance`) ──

/// Every match of a request over report.pdf, slice after slice.
fn collect_all(session: &mut DocumentSession, query: SearchQuery) -> Vec<SearchMatch> {
    let mut matches = Vec::new();
    let mut cursor = None;
    loop {
        let slice = search(
            session,
            &SearchRequest { cursor, ..request(query.clone()) },
            &CancelToken::never(),
        )
        .expect("search");
        matches.extend(slice.matches);
        match slice.next_cursor {
            None => return matches,
            next => cursor = next,
        }
    }
}

fn key(m: &SearchMatch) -> (u32, u32, u32) {
    (m.page.get(), m.start, m.count)
}

#[test]
fn matching_is_case_insensitive_by_default() {
    let mut session = report();
    let lower = collect_all(&mut session, literal("report"));
    let upper = collect_all(&mut session, literal("REPORT"));
    assert!(!lower.is_empty());
    assert_eq!(
        lower.iter().map(key).collect::<Vec<_>>(),
        upper.iter().map(key).collect::<Vec<_>>()
    );
}

#[test]
fn regex_flags_are_restrictions_match_case_and_whole_word_return_subsets() {
    // Each flag can only remove matches, never invent them.
    let mut session = report();
    let pattern = SearchQuery { text: "the".into(), regex: true, ..SearchQuery::default() };
    let all = collect_all(&mut session, pattern.clone());
    let all_keys: std::collections::HashSet<_> = all.iter().map(key).collect();
    assert!(!all.is_empty());
    for restricted in [
        SearchQuery { match_case: true, ..pattern.clone() },
        SearchQuery { whole_word: true, ..pattern.clone() },
    ] {
        let found = collect_all(&mut session, restricted.clone());
        assert!(found.len() <= all.len(), "{restricted:?} found more");
        for m in &found {
            assert!(all_keys.contains(&key(m)), "{restricted:?} invented {:?}", key(m));
        }
        // The fixture has "The" and "there", so each flag removes some.
        assert!(found.len() < all.len(), "{restricted:?} removed nothing, so the check is vacuous");
    }
}

#[test]
fn each_segments_page_space_rect_bounds_its_quad_on_a_host_answer() {
    use epdf_engine::host::Host;

    let mut host = Host::new();
    let opened = host.call_filled(
        &serde_json::json!({ "kind": "open.fatMem", "jobId": 1, "docId": "d1" }).to_string(),
        vec![test_support::fixture(test_support::REPORT)],
    );
    assert!(!opened.json.contains("\"error\""), "{}", opened.json);
    let response = host.call_filled(
        &serde_json::json!({ "kind": "search.query", "jobId": 2, "docId": "d1",
            "request": { "text": "report" } })
        .to_string(),
        Vec::new(),
    );
    let body: serde_json::Value = serde_json::from_str(&response.json).expect("JSON");
    let matches = body["result"]["matches"].as_array().expect("matches");
    assert!(!matches.is_empty(), "{body}");
    let mut checked = 0;
    for m in matches {
        assert!(m["count"].as_u64().expect("count") > 0);
        let segments = m["segments"].as_array().expect("segments");
        assert!(!segments.is_empty());
        for s in segments {
            let n = |v: &serde_json::Value| v.as_f64().expect("a number");
            let corners = ["upperLeft", "upperRight", "lowerLeft", "lowerRight"];
            let xs: Vec<f64> = corners.iter().map(|c| n(&s["quad"][c]["x"])).collect();
            let ys: Vec<f64> = corners.iter().map(|c| n(&s["quad"][c]["y"])).collect();
            let min = |v: &[f64]| v.iter().copied().fold(f64::INFINITY, f64::min);
            let max = |v: &[f64]| v.iter().copied().fold(f64::NEG_INFINITY, f64::max);
            let (x, y, w, h) = (
                n(&s["rect"]["x"]),
                n(&s["rect"]["y"]),
                n(&s["rect"]["width"]),
                n(&s["rect"]["height"]),
            );
            assert!(w > 0.0 && h > 0.0, "{s}");
            assert!((x - min(&xs)).abs() < 1e-3, "{s}");
            assert!((x + w - max(&xs)).abs() < 1e-3, "{s}");
            assert!((y - min(&ys)).abs() < 1e-3, "{s}");
            assert!((y + h - max(&ys)).abs() < 1e-3, "{s}");
            assert!(s["advance"] == 1 || s["advance"] == -1, "{s}");
            checked += 1;
        }
    }
    assert!(checked > 0);
}

#[test]
fn a_negative_skip_behaves_like_no_skip() {
    let mut session = report();
    let plain =
        search(&mut session, &request(literal(RARE)), &CancelToken::never()).expect("search");
    let negative = search(
        &mut session,
        &SearchRequest { skip: Some(-3.0), ..request(literal(RARE)) },
        &CancelToken::never(),
    )
    .expect("search");
    assert_eq!(
        negative.matches.iter().map(key).collect::<Vec<_>>(),
        plain.matches.iter().map(key).collect::<Vec<_>>()
    );
    assert_eq!(negative.pages_searched, plain.pages_searched);
}

#[test]
fn a_negative_page_limit_searches_one_page() {
    let mut session = report();
    let slice = search(
        &mut session,
        &SearchRequest {
            limit: Some(SearchLimit { pages: Some(-1.0), ..Default::default() }),
            ..request(literal("the"))
        },
        &CancelToken::never(),
    )
    .expect("search");
    assert_eq!(slice.pages_searched, 1);
    assert!(slice.next_cursor.is_some());
}

// ── The page corpus cache and the query compiled once (E5) ─────────────────

/// Every match of `needle` across the whole document, in one slice.
fn whole_document(needle: &str) -> SearchRequest {
    SearchRequest {
        limit: Some(SearchLimit { pages: Some(256.0), matches: Some(1024.0) }),
        ..request(literal(needle))
    }
}

/// Pages with at least one match, in scan order.
fn matched_pages(matches: &[SearchMatch]) -> Vec<PageObjectNumber> {
    let mut pages: Vec<PageObjectNumber> = Vec::new();
    for m in matches {
        if pages.last() != Some(&m.page) {
            pages.push(m.page);
        }
    }
    pages
}

#[test]
fn repeat_query_loads_no_text_page() {
    use pdfium::call_count::{self, Call};

    let mut session = report();
    // A needle no page holds: every page is read for its text and none for
    // its geometry.
    call_count::reset();
    let first = search(&mut session, &whole_document("zqxjv"), &CancelToken::never())
        .expect("first search");
    assert!(first.matches.is_empty());
    assert_eq!(call_count::get(Call::TextLoadPage), 14, "the first query reads every page");

    call_count::reset();
    let again = search(&mut session, &whole_document("zqxjv"), &CancelToken::never())
        .expect("repeat search");
    assert_eq!(again, first);
    assert_eq!(
        call_count::get(Call::TextLoadPage),
        0,
        "a repeat query at the same mutation sequence read page text again",
    );

    // A query with hits reads text from the cache too, and loads a text page
    // only for each matched page's geometry.
    let found = search(&mut session, &whole_document(RARE), &CancelToken::never())
        .expect("search with hits");
    assert!(!found.matches.is_empty());
    call_count::reset();
    let repeat = search(&mut session, &whole_document(RARE), &CancelToken::never())
        .expect("repeat search with hits");
    assert_eq!(repeat, found);
    assert_eq!(
        call_count::get(Call::TextLoadPage),
        matched_pages(&found.matches).len() as u64,
        "a repeat query loaded text pages beyond the matched pages' geometry",
    );
}

#[test]
fn query_after_edit_reloads_page() {
    use pdfium::call_count::{self, Call};
    use serde_json::{Value, json};

    let mut host = epdf_engine::host::Host::new();
    let (doc, _) = common::open_fixture(&mut host, test_support::REPORT);
    let query = |host: &mut epdf_engine::host::Host| -> Value {
        common::call(
            host,
            json!({ "kind": "search.query", "docId": doc,
                "request": { "text": "the", "limit": { "pages": 256, "matches": 1024 } } }),
        )
    };
    let hits_on = |slice: &Value, page: u64| -> usize {
        slice["matches"]
            .as_array()
            .expect("matches")
            .iter()
            .filter(|m| m["page"]["objectNumber"] == page)
            .count()
    };
    let pages = common::call(&mut host, json!({ "kind": "pages.list", "docId": doc }));
    let second = &pages["pages"][1];
    let page = second["ref"]["objectNumber"].as_u64().expect("a second page");
    let size = second["size"].clone();

    let before = query(&mut host);
    assert!(hits_on(&before, page) > 0, "page 2 has no hits to remove: {before}");
    call_count::reset();
    assert_eq!(query(&mut host), before);
    let cached = call_count::get(Call::TextLoadPage);

    // Redact the whole of page 2, which takes its text out of the content.
    common::call(
        &mut host,
        json!({ "kind": "annotations.create", "opId": "mark", "docId": doc,
            "page": common::page_ref(page),
            "draft": { "subtype": "redact", "interiorColor": "#000000",
                "rect": { "x": 0, "y": 0, "width": size["width"], "height": size["height"] } } }),
    );
    common::call(
        &mut host,
        json!({ "kind": "redaction.apply", "effect": "contentWrite", "opId": "apply",
            "docId": doc, "scope": { "pages": [common::page_ref(page)] } }),
    );

    call_count::reset();
    let after = query(&mut host);
    assert!(
        call_count::get(Call::TextLoadPage) > cached,
        "the query after the edit read no more text pages than a cached repeat",
    );
    assert_eq!(hits_on(&after, page), 0, "the hits still show the redacted text: {after}");
    let elsewhere =
        |slice: &Value| slice["matches"].as_array().expect("matches").len() - hits_on(slice, page);
    assert_eq!(elsewhere(&after), elsewhere(&before), "hits on unedited pages changed");
}

#[test]
fn regex_compiled_once_per_query() {
    use epdf_engine::features::search::build_count;

    let mut session = report();
    let query = SearchQuery { text: "fig(ure)?".into(), regex: true, ..SearchQuery::default() };
    build_count::reset();
    let slice = search(
        &mut session,
        &SearchRequest {
            limit: Some(SearchLimit { pages: Some(256.0), matches: Some(1024.0) }),
            ..request(query)
        },
        &CancelToken::never(),
    )
    .expect("search");
    assert_eq!(slice.pages_searched, 14);
    assert!(!slice.matches.is_empty());
    assert_eq!(build_count::validated(), 1, "the pattern was validated more than once");
    assert_eq!(build_count::compiled(), 1, "the pattern was compiled more than once");
}

/// A one-page PDF whose word `SECRET` sits under a redaction mark and whose
/// word `visible` does not.
fn marked_secret() -> Vec<u8> {
    let content = "BT /F1 18 Tf 60 90 Td (SECRET) Tj ET BT /F1 18 Tf 60 220 Td (visible) Tj ET";
    let objects = [
        "<< /Type /Catalog /Pages 2 0 R >>".to_string(),
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>".to_string(),
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Contents 4 0 R \
         /Resources << /Font << /F1 5 0 R >> >> \
         /Annots [<< /Type /Annot /Subtype /Redact /Rect [40 70 200 130] /IC [0 0 0] >>] >>"
            .to_string(),
        format!("<< /Length {} >>\nstream\n{content}\nendstream", content.len()),
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>".to_string(),
    ];
    let mut out = b"%PDF-1.7\n".to_vec();
    let mut offsets = Vec::new();
    for (i, object) in objects.iter().enumerate() {
        offsets.push(out.len());
        out.extend(format!("{} 0 obj\n{object}\nendobj\n", i + 1).as_bytes());
    }
    let xref = out.len();
    out.extend(format!("xref\n0 {}\n0000000000 65535 f \n", objects.len() + 1).as_bytes());
    for offset in offsets {
        out.extend(format!("{offset:010} 00000 n \n").as_bytes());
    }
    out.extend(
        format!(
            "trailer\n<< /Size {} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n",
            objects.len() + 1
        )
        .as_bytes(),
    );
    out
}

#[test]
fn a_committed_write_that_never_finished_is_not_searched_as_it_was() {
    // `document.applyChanges` commits each change on its own, and a change
    // that fails with a refusal it does not keep fails the job before it
    // finishes: the changes before it stay committed and the wire sequence
    // does not move. The corpus is keyed on the derived-cache sequence
    // (`cacheSeq`), which every commit moves, so the next search reads the
    // page again.
    let mut session = DocumentSession::new();
    session.open_with(marked_secret(), None, SessionAuthority::unrestricted()).expect("open");
    let page = pages(&mut session)[0];
    let found =
        search(&mut session, &request(literal("SECRET")), &CancelToken::never()).expect("search");
    assert_eq!(found.matches.len(), 1, "the fixture shows SECRET once");

    let sequence = session.mutation_sequence();
    let scope = epdf_engine::features::redaction::RedactionScope::Pages { pages: vec![page] };
    epdf_engine::features::changes::transaction::in_transaction(&mut session, "op-redact", |s| {
        epdf_engine::features::redaction::apply(s, &scope, &CancelToken::never())
    })
    .expect("the redaction commits");
    assert_eq!(session.mutation_sequence(), sequence, "nothing finished the write");

    let after =
        search(&mut session, &request(literal("SECRET")), &CancelToken::never()).expect("search");
    assert!(after.matches.is_empty(), "the redacted word is still found: {:?}", after.matches);
    let kept =
        search(&mut session, &request(literal("visible")), &CancelToken::never()).expect("search");
    assert_eq!(kept.matches.len(), 1, "the redaction took the unmarked word too");
}
