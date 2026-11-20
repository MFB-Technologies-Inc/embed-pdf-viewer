import EmbedPDF
import Foundation
import Observation

/// A document's search session for SwiftUI.
///
/// ``status``, ``activeHitIndex``, ``hitCount``, ``progress`` and ``hits``
/// are snapshots of the kernel, read again after each state revision and
/// search event while ``task()`` runs, and after each action. A snapshot is
/// one revision behind the kernel until its refresh lands: read right after a
/// change, it still holds the value from before. The `SearchCapability` reads
/// answer with the kernel's current value. A refresh that started earlier
/// never overwrites one that started later. A failed read, as when the
/// document closes, empties the model and sets ``error``; ``task()`` ends with
/// one more refresh, so a closed document leaves the model empty.
///
/// ``error`` is the latest failure of an action or a read. A successful
/// background refresh does not clear it; a successful ``searchSubmitted()``
/// or ``clearButtonTapped()`` does, so a caller that polls it can miss a
/// failure one of those cleared. After a refused ``searchSubmitted()``,
/// ``status`` stays `SearchStatus.error` through background refreshes until
/// the next submitted search or ``clearButtonTapped()``.
///
/// The search plugin reruns its query by itself after every confirmed edit.
/// Each search event rereads ``hits``, so a rerun that finds as many hits in
/// other places still reaches the model.
@MainActor
@Observable
public final class SearchModel {
  public var text = ""
  public private(set) var hits: [SearchHit] = []
  /// The active hit's position in `hits`, `nil` when no hit is active.
  public private(set) var activeHitIndex: Int?
  /// ``activeHitIndex`` as the engine's `UInt32`.
  @available(*, deprecated, message: "Use activeHitIndex, an Int?; this is the same position as a UInt32.")
  public var activeIndex: UInt32? { activeHitIndex.flatMap { UInt32(exactly: $0) } }
  public private(set) var status: SearchStatus = .idle
  /// Hits found so far in the whole document.
  public private(set) var hitCount = 0
  public private(set) var progress = SearchProgress(pagesSearched: 0, pageCount: 0)
  public private(set) var error: PDFError?
  private var pageHitCounts: [PageRef: Int] = [:]
  @ObservationIgnored private var query: SearchQuery?
  /// Set by a submitted search and cleared only when a hit list is applied,
  /// so a refresh that supersedes the submitted one still rereads it.
  @ObservationIgnored private var hitsOutdated = false
  /// Set by a refused ``searchSubmitted()``, so a background refresh keeps
  /// `status` at `.error` until the next submitted search or clear.
  @ObservationIgnored private var submissionRefused = false
  @ObservationIgnored let search: SearchCapability
  @ObservationIgnored let snapshots = SnapshotRefresh()
  @ObservationIgnored private var observation: UInt64 = 0

  public init(search: SearchCapability) {
    self.search = search
  }

  /// Hits found so far on one page, counted from ``hits``.
  public func hitCount(on page: PageRef) -> Int { pageHitCounts[page] ?? 0 }

  /// The hit at ``activeHitIndex`` in ``hits``, `nil` when no hit is active.
  ///
  /// The index and the list come from separate reads. A refresh already
  /// reading when a new search starts can apply the old search's index with
  /// the new search's list. For that one refresh the index either points
  /// past the list, which reads `nil`, or lands inside it, and this is then
  /// a hit of the new search that may not be its active one. The next
  /// refresh corrects it.
  public var activeHit: SearchHit? {
    guard let activeHitIndex, hits.indices.contains(activeHitIndex) else { return nil }
    return hits[activeHitIndex]
  }

  /// The active hit's place among the hits, counted from 1, as `"3 of 12"`;
  /// `nil` when no hit is active, which includes a session with no hits.
  ///
  /// While ``status`` is `SearchStatus.searching`, ``hitCount`` is the hits
  /// found so far and still grows, so the text ends in a plus sign:
  /// `"3 of 12+"`. After a cancelled or failed scan it is the count of the
  /// hits that scan kept, which is every hit the session navigates.
  ///
  /// The text is English, looked up in the app's main bundle under the keys
  /// `"%lld of %lld"` and `"%lld of %lld+"`. An app that words it another
  /// way builds its own from ``activeHitIndex``, ``hitCount`` and ``status``.
  public var summary: String? {
    Self.summary(activeHitIndex: activeHitIndex, hitCount: hitCount, status: status)
  }

  static func summary(activeHitIndex: Int?, hitCount: Int, status: SearchStatus) -> String? {
    guard let activeHitIndex, activeHitIndex < hitCount else { return nil }
    // Performance: each read looks the key up in the main bundle's strings
    // table and formats two numbers for the locale, and a view reads this on
    // every body evaluation, each keystroke in a search field included. That
    // is small for one label; a stored string rebuilt only when
    // `activeHitIndex`, `hitCount` or `status` changes removes it.
    return status == .searching
      ? String(localized: "\(activeHitIndex + 1) of \(hitCount)+")
      : String(localized: "\(activeHitIndex + 1) of \(hitCount)")
  }

  /// Whether a clear button has anything to clear: text in the field, hits,
  /// or a session that is not idle. A running scan with no hits yet and a
  /// refused search's `SearchStatus.error` both count, with an empty field
  /// too. `SearchBar` enables its clear button on it, and a bar an app
  /// builds itself can too.
  public var canClear: Bool { !text.isEmpty || hitCount > 0 || status != .idle }

  public func task() async {
    observation &+= 1
    let session = observation
    defer { if observation == session { snapshots.invalidate() } }
    await observeCapability(
      viewer: search.viewer, events: search.events,
      initial: { if self.observation == session { await self.refresh() } },
      refresh: { if self.observation == session { await self.refresh() } },
      receive: { event in
        guard self.observation == session else { return }
        // A run started, finished or was cleared, the plugin's own rerun after
        // an edit included: the hit list may have moved even when its count
        // didn't.
        if event.changesHits { self.hitsOutdated = true }
        await self.refresh()
      })
    if observation == session { await refresh() }
  }

  public func refresh() async {
    await snapshots.run(load: {
      let status = try await self.search.status()
      // Performance: the active index here and the hit list below are two of
      // this refresh's bridge round trips, and a search that starts between
      // them pairs the old index with the new list (see `activeHit`). One
      // capability read that returns the index and the hits from the same
      // kernel state saves a round trip on each refresh that rereads the
      // hits, and closes that window.
      let activeHitIndex = try await self.search.activeHitIndex()
      let hitCount: Int = try await self.search.hitCount()
      let progress = try await self.search.progress()
      let query = try await self.search.query()
      // Between searches the hit list changes only with the query, the count
      // or the status, and a revision is often a scroll or a zoom: keep the
      // list then. A submitted search always rereads it, since a rerun after
      // an edit can find as many hits in other places.
      let unchanged = !self.hitsOutdated && query == self.query && hitCount == self.hits.count
        && status == self.status
      // The second of the two reads noted at `activeHitIndex` above.
      let hits: [SearchHit]? = unchanged ? nil : try await self.search.hits()
      return (status, activeHitIndex, hitCount, progress, query, hits)
    }, apply: { snapshot in
      // Write only what changed, so the views reading these fields stay
      // valid through a scroll or a zoom.
      let status = self.submissionRefused ? .error : snapshot.0
      if self.status != status { self.status = status }
      if self.activeHitIndex != snapshot.1 { self.activeHitIndex = snapshot.1 }
      if self.hitCount != snapshot.2 { self.hitCount = snapshot.2 }
      if self.progress != snapshot.3 { self.progress = snapshot.3 }
      self.query = snapshot.4
      if let hits = snapshot.5 {
        self.hitsOutdated = false
        if self.hits != hits {
          self.hits = hits
          self.pageHitCounts = hits.reduce(into: [:]) { counts, hit in counts[hit.page, default: 0] += 1 }
        }
      }
    }, failure: {
      self.status = .idle
      self.activeHitIndex = nil
      self.hitCount = 0
      self.progress = SearchProgress(pagesSearched: 0, pageCount: 0)
      self.query = nil
      self.hits = []
      self.pageHitCounts = [:]
      self.error = controlError($0)
    })
  }

  public func searchSubmitted() async {
    do {
      _ = try await search.search(SearchQuery(text: text))
      error = nil
      submissionRefused = false
    } catch {
      snapshots.invalidate()
      self.error = error
      submissionRefused = true
      status = .error
      return
    }
    hitsOutdated = true
    await refresh()
  }

  public func nextButtonTapped() async {
    do {
      _ = try await search.nextHit()
    } catch {
      self.error = error
      return
    }
    await refresh()
  }

  public func previousButtonTapped() async {
    do {
      _ = try await search.previousHit()
    } catch {
      self.error = error
      return
    }
    await refresh()
  }

  /// Scrolls the document's stage to the active hit, placed as the search
  /// settings say: the reveal a next or previous step already makes. The
  /// first hit of a new search becomes active without one, so a view calls
  /// this to bring it into view. Repeating it is safe, and it leaves the
  /// active hit as it is. A failure lands in ``error``.
  public func revealActiveHit() async {
    do {
      try await search.revealActiveHit()
    } catch {
      self.error = error
    }
  }

  public func clearButtonTapped() async {
    do {
      try await search.clear()
      error = nil
      submissionRefused = false
    } catch {
      self.error = error
      return
    }
    await refresh()
  }
}

extension SearchEvent {
  /// Whether the hit list can differ after this event: a run started, ended
  /// or was cleared. Progress and active-hit moves leave the list alone.
  fileprivate var changesHits: Bool {
    switch self {
    case .started, .completed, .cancelled, .failed, .cleared: true
    case .progressChanged, .activeHitChanged: false
    }
  }
}
