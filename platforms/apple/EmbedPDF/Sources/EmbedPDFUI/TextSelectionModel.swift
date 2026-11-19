import EmbedPDF
import Observation

/// Turns a drag over a page into a text selection and mirrors the
/// selection's rects onto the main actor. Every call runs after the calls
/// made before it, and drag points that arrive while an extension waits
/// collapse into the newest one.
@MainActor
@Observable
public final class TextSelectionModel {
  /// What a drag's first point selects.
  public enum Start: Sendable {
    /// An empty selection at the point, grown by the drag.
    case caret
    /// The word under the point, grown by the drag.
    case word
  }

  /// Selected rects per page, in unrotated page points.
  public private(set) var rects: [PageRef: [Rect]] = [:]
  /// Whether a drag has begun on text and not yet ended.
  public private(set) var isSelecting = false
  public private(set) var error: PDFError?
  /// `extend(to:on:)` calls made so far.
  @ObservationIgnored var extensionCount = 0
  @ObservationIgnored private let selection: SelectionCapability
  /// The stream `init` subscribed, held until the first `task()` takes it.
  @ObservationIgnored private var subscribed: AsyncStream<SelectionEvent>?
  /// The last call, which the next one waits for. SwiftUI hands a gesture's
  /// change and end to separate tasks, and each call leaves the main actor
  /// while it reaches the engine, so without this an end could reach the
  /// engine before the begin it closes.
  @ObservationIgnored private var lastCall: Task<Void, Never>?
  /// The newest drag point no extension has applied yet.
  @ObservationIgnored private var pending: (point: Point, page: PageRef)?
  /// Whether an extension waits in the chain; it applies whatever
  /// `pending` holds when it runs.
  @ObservationIgnored private var extensionQueued = false
  /// Bumped when a rects refresh starts and when `rects` is emptied. A
  /// refresh writes only if this is unchanged when its reads land, so an
  /// older read never overwrites a newer one.
  @ObservationIgnored private var rectsEpoch = 0

  /// Subscribes at once, so a selection made before the first ``task()``
  /// still reaches ``rects``.
  public init(selection: SelectionCapability) {
    self.selection = selection
    subscribed = selection.events
  }

  /// Keeps ``rects`` in step with selections made elsewhere until the
  /// document closes or the task is cancelled. The first run consumes the
  /// subscription `init` made; each later run subscribes again. Run one at a
  /// time: concurrent runs are not supported.
  public func task() async {
    // Taken before the first suspension, so a restart misses nothing sent
    // after it begins.
    let events = subscribed ?? selection.events
    subscribed = nil
    for await event in events {
      guard !Task.isCancelled else { return }
      switch event {
      case .changed, .committed:
        await refreshRects()
      case .cleared:
        emptyRects()
      }
    }
  }

  /// Starts a selection at `point`. False when the point has no text; the
  /// model then stays idle until the next begin.
  public func dragBegan(at point: Point, on page: PageRef, start: Start = .caret) async -> Bool {
    await enqueue { await $0.applyBegin(at: point, on: page, start: start) }.value
  }

  /// Moves the selection's focus to `point`. Points that arrive while an
  /// extension waits replace each other, and only the newest reaches the
  /// engine.
  public func dragChanged(to point: Point, on page: PageRef) {
    pending = (point, page)
    guard !extensionQueued else { return }
    extensionQueued = true
    enqueue { model in
      model.extensionQueued = false
      // Before a begin succeeds, the point waits for it.
      guard model.isSelecting else { return }
      await model.applyPending()
    }
  }

  /// Ends the drag, keeping the selection and ``rects``.
  public func dragEnded() async {
    await enqueue { await $0.applyEnd() }.value
  }

  /// Clears the selection and ``rects``.
  public func clear() async {
    await enqueue { await $0.applyClear() }.value
  }

  @discardableResult
  private func enqueue<Result: Sendable>(
    _ work: @escaping @MainActor @Sendable (TextSelectionModel) async -> Result
  ) -> Task<Result, Never> {
    let previous = lastCall
    let call = Task { @MainActor in
      await previous?.value
      return await work(self)
    }
    lastCall = Task { _ = await call.value }
    return call
  }

  private func applyBegin(at point: Point, on page: PageRef, start: Start) async -> Bool {
    do {
      try await selection.ensureLoaded(page)
      let began: Bool
      switch start {
      case .caret:
        began = try await selection.beginGesture(at: point, on: page)
      case .word:
        began = try await selection.selectWord(at: point, on: page)
        if began { try await selection.beginGesture() }
      }
      guard began else { return false }
      isSelecting = true
    } catch {
      self.error = error
      return false
    }
    // A point that arrived before or during the begin applies inside this
    // call, so an end queued behind the begin finds it applied.
    if pending != nil {
      await applyPending()
    } else {
      await refreshRects()
    }
    return true
  }

  private func applyPending() async {
    guard let (point, page) = pending else { return }
    pending = nil
    do {
      if !(try await selection.isLoaded(page)) {
        try await selection.ensureLoaded(page)
      }
      extensionCount += 1
      try await selection.extend(to: point, on: page)
    } catch {
      self.error = error
      return
    }
    await refreshRects()
  }

  private func applyEnd() async {
    pending = nil
    if isSelecting {
      do {
        try await selection.endGesture()
      } catch {
        self.error = error
      }
    }
    isSelecting = false
  }

  private func applyClear() async {
    do {
      try await selection.clear()
    } catch {
      self.error = error
    }
    emptyRects()
  }

  private func emptyRects() {
    rectsEpoch += 1
    rects = [:]
  }

  private func refreshRects() async {
    rectsEpoch += 1
    let epoch = rectsEpoch
    var next: [PageRef: [Rect]] = [:]
    do {
      for page in try await selection.selectedPages() {
        next[page] = try await selection.rects(on: page)
      }
    } catch {
      self.error = error
      return
    }
    guard epoch == rectsEpoch else { return }
    rects = next
  }
}
