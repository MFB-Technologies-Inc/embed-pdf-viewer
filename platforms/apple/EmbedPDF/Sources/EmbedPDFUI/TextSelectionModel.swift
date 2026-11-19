import EmbedPDF
import Observation

/// Turns a drag over a page into a text selection and mirrors the
/// selection's rects onto the main actor. Every call runs after the calls
/// made before it, and drag points that arrive while an extension waits
/// collapse into the newest one.
///
/// The model observes by itself from `init` until the document closes or the
/// model is released; there is no task to run. Releasing it ends its event
/// subscription, so nothing buffers for a model no one holds.
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
  /// The last error the engine threw, kept until the next ``dragBegan(at:on:start:)``
  /// or ``clear()`` runs.
  public private(set) var error: PDFError?
  /// `extend(to:on:)` calls made so far.
  @ObservationIgnored var extensionCount = 0
  /// Runs after a refresh's reads return and before they are applied. Tests
  /// hold a refresh here.
  @ObservationIgnored var rectsReadReturned: (@MainActor () async -> Void)?
  @ObservationIgnored private let selection: SelectionCapability
  /// Orders the drag calls and every rects refresh. SwiftUI hands a
  /// gesture's change and end to separate callbacks, and each call leaves
  /// the main actor while it reaches the engine, so without this an end
  /// could reach the engine before the begin it closes, and an older rects
  /// read could overwrite a newer one.
  @ObservationIgnored private let calls = SerialCalls()
  /// The task following selection events, cancelled when the model is
  /// released.
  @ObservationIgnored private var observation: CancelOnRelease?
  /// The event-following task, which finishes when the document closes or
  /// the model is released. Tests await it.
  var observationTask: Task<Void, Never>? { observation?.task }
  /// The newest drag point no extension has applied yet.
  @ObservationIgnored private var pending: (point: Point, page: PageRef)?
  /// Whether an extension waits in the chain; it applies whatever
  /// `pending` holds when it runs.
  @ObservationIgnored private var extensionQueued = false

  /// Subscribes at once, so a selection made right after `init` still
  /// reaches ``rects``.
  public init(selection: SelectionCapability) {
    self.selection = selection
    let events = selection.events
    // Holds the model weakly, and only while it queues one event's refresh,
    // so the task never keeps the model alive.
    observation = CancelOnRelease(
      Task { [weak self] in
        for await event in events { self?.follow(event) }
      })
  }

  /// Queues the event's effect on ``rects`` behind the calls made so far, so
  /// a selection made elsewhere never lands out of order with a drag's own
  /// refresh.
  private func follow(_ event: SelectionEvent) {
    switch event {
    case .changed, .committed:
      enqueue { await $0.refreshRects() }
    case .cleared:
      enqueue { $0.rects = [:] }
    }
  }

  /// Starts a selection at `point` and clears ``error``. Returns at once;
  /// ``isSelecting`` turns true when the engine finds text at the point, and
  /// ``dragEnded()`` reports whether it did. With `.word`, a failure after
  /// the word is selected leaves it selected with ``isSelecting`` false and
  /// the error in ``error``.
  public func dragBegan(at point: Point, on page: PageRef, start: Start = .caret) {
    // A point left by a drag that never began belongs to that drag.
    pending = nil
    enqueue { await $0.applyBegin(at: point, on: page, start: start) }
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

  /// Ends the drag, keeping the selection. True when the drag began on text;
  /// false when the point had no text, the begin failed, or no drag was
  /// open. ``rects`` are final when this returns: every refresh queued
  /// before the end, a selection event's included, has been applied in order.
  @discardableResult
  public func dragEnded() async -> Bool {
    await enqueue { await $0.applyEnd() }.value
  }

  /// Clears the selection, ``rects`` and ``error``.
  public func clear() async {
    await enqueue { await $0.applyClear() }.value
  }

  @discardableResult
  private func enqueue<Result: Sendable>(
    _ work: @escaping @MainActor @Sendable (TextSelectionModel) async -> Result
  ) -> Task<Result, Never> {
    calls.enqueue { await work(self) }
  }

  private func applyBegin(at point: Point, on page: PageRef, start: Start) async {
    error = nil
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
      guard began else { return }
      isSelecting = true
    } catch {
      self.error = error
      return
    }
    // A point that arrived before or during the begin applies inside this
    // call, so an end queued behind the begin finds it applied.
    if pending != nil {
      await applyPending()
    } else {
      await refreshRects()
    }
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

  private func applyEnd() async -> Bool {
    pending = nil
    guard isSelecting else { return false }
    do {
      try await selection.endGesture()
    } catch {
      self.error = error
    }
    isSelecting = false
    return true
  }

  private func applyClear() async {
    error = nil
    do {
      try await selection.clear()
    } catch {
      self.error = error
    }
    rects = [:]
  }

  private func refreshRects() async {
    var next: [PageRef: [Rect]] = [:]
    do {
      for page in try await selection.selectedPages() {
        next[page] = try await selection.rects(on: page)
      }
    } catch {
      self.error = error
      return
    }
    await rectsReadReturned?()
    rects = next
  }
}
