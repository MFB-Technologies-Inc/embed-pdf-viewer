import EmbedPDF
import Observation

/// A document's interaction tools for SwiftUI: which are registered, which
/// is active, and which the user may use now.
///
/// ``tools``, ``activeToolID`` and ``usable`` are snapshots of the hub, read
/// again after each state revision and each `toolChanged` event while
/// ``task()`` runs, and after each action. A snapshot is one revision behind
/// the hub until its refresh lands; the `InteractionCapability` reads answer
/// with the hub's current value. A refresh that started earlier never
/// overwrites one that started later. A failed read, as when the document
/// closes, empties the model and sets ``error``; ``task()`` ends with one
/// more refresh, so a closed document leaves the model empty.
///
/// ``submitting`` is `true` from the start of ``activate(_:)`` or
/// ``activateDefault()`` until the call fails or the one read the action
/// starts after the hub answers has finished. State revisions and events
/// start no read in that time; one the action's read does not cover is
/// read once ``submitting`` is `false`. ``activeToolID``
/// holds the action's result when ``submitting`` clears, with two
/// exceptions: the action's task was cancelled, or a ``refresh()`` the app
/// called superseded the action's read. In both the result arrives with the
/// next read that lands. A second action asked for while ``submitting`` is
/// `true` is ignored.
///
/// ``usable`` holds each registered tool whose `canUseTool` is `true`. A
/// tool registered or removed is a state revision, so ``usable`` follows it
/// while ``task()`` runs. A tool's permission can start to answer
/// differently with no revision and no event: the document's rights after
/// an unlock or a signing, or a `ToolPermission` the app registered. Call
/// ``refresh()`` after such a change.
///
/// ``error`` is the latest failure of an action or a read. A successful
/// ``activate(_:)`` or ``activateDefault()`` clears it. A successful
/// background refresh clears the error of an earlier read and keeps the
/// error of an action.
@MainActor
@Observable
public final class ToolModel {
  /// The registered tools, in the hub's order.
  public private(set) var tools: [InteractionTool] = []
  /// `nil` before the first read lands and after a failed one.
  public private(set) var activeToolID: ToolID?
  public private(set) var usable: Set<ToolID> = []
  public private(set) var error: PDFError?
  /// Whether an ``activate(_:)`` or ``activateDefault()`` is in flight.
  public private(set) var submitting = false
  /// Whether ``error`` came from a read, which the next successful read
  /// clears, and not from an action.
  @ObservationIgnored private var readFailed = false
  /// Whether the action in flight has started its read.
  @ObservationIgnored private var flightReadStarted = false
  /// Whether a change was observed in this flight before its read started.
  @ObservationIgnored private var flightMissedChange = false
  /// The observed changes waiting for the action in flight to end.
  @ObservationIgnored private(set) var flightWaiters: [CheckedContinuation<Void, Never>] = []
  @ObservationIgnored let interaction: InteractionCapability
  @ObservationIgnored let snapshots = SnapshotRefresh()
  @ObservationIgnored private var observation: UInt64 = 0

  public init(interaction: InteractionCapability) {
    self.interaction = interaction
  }

  public func task() async {
    observation &+= 1
    let session = observation
    defer { if observation == session { snapshots.invalidate() } }
    await observeCapability(
      viewer: interaction.viewer, events: interaction.events,
      initial: { if self.observation == session { await self.observedChange() } },
      // Performance: runs for every coalesced state revision, and most are
      // a scroll or a zoom that leaves the tools alone. The viewer's
      // revision stream does not say what changed, so each one costs a full
      // read (see `read`), except while an action is in flight.
      refresh: { if self.observation == session { await self.observedChange() } },
      receive: { event in
        // Gestures and cursor moves leave the tools alone.
        guard self.observation == session, case .toolChanged = event else { return }
        await self.observedChange()
      })
    if observation == session { await refresh() }
  }

  /// A state revision or a `toolChanged` event. While an action is in
  /// flight it starts no read, so it cannot supersede the action's read or
  /// be superseded by it in turn. One that arrives before the action's read
  /// starts is in that read. A later one waits for the flight to end and
  /// reads then.
  func observedChange() async {
    if submitting {
      guard flightReadStarted else {
        flightMissedChange = true
        return
      }
      // Performance: one more full read per flight, after `submitting`
      // clears, so it never holds the buttons. The action's own
      // `toolChanged` causes it when the event arrives during the action's
      // read; telling that event from another caller's needs an id on the
      // event.
      await withCheckedContinuation { flightWaiters.append($0) }
    }
    await refresh()
  }

  public func refresh() async {
    await snapshots.run(load: {
      let tools = try await self.interaction.tools()
      let active = try await self.interaction.activeToolID()
      var usable: Set<ToolID> = []
      // Performance: one `canUseTool` round trip to the kernel thread per
      // registered tool, one after another, on every coalesced state
      // revision, a scroll or a zoom included. With the annotation plugin's
      // tools that is 20 calls, 22 with the two reads above, per revision,
      // and a `ToolPermission.canUse()` the app registered is asked at that
      // rate, and an action's flight lasts one such read. Rereading
      // `usable` only when `tools` differs or on `toolChanged` removes the
      // cost, but a permission can change with no revision and no event
      // (see the type's comment), so that needs a signal for permission
      // changes first. A single engine call that answers for every tool
      // removes the round trips without one.
      for tool in tools {
        let id = ToolID(tool.id)
        if try await self.interaction.canUseTool(id) { usable.insert(id) }
      }
      return (tools, active, usable)
    }, apply: { snapshot in
      // Most revisions are a scroll or a zoom: write only what changed, so
      // the views reading these fields stay valid.
      if self.tools != snapshot.0 { self.tools = snapshot.0 }
      if self.activeToolID != snapshot.1 { self.activeToolID = snapshot.1 }
      if self.usable != snapshot.2 { self.usable = snapshot.2 }
      if self.readFailed {
        self.readFailed = false
        self.error = nil
      }
    }, failure: {
      self.tools = []
      self.activeToolID = nil
      self.usable = []
      self.error = controlError($0)
      self.readFailed = true
    })
  }

  /// Makes `id` the active tool. An id nobody registered sets ``error`` to
  /// `not-found` and leaves ``activeToolID``.
  public func activate(_ id: ToolID) async {
    await submit { () throws(PDFError) in try await self.interaction.activateTool(id) }
  }

  /// Returns to the hub's default tool.
  public func activateDefault() async {
    await submit { () throws(PDFError) in try await self.interaction.activateDefaultTool() }
  }

  private func submit(_ action: () async throws(PDFError) -> Void) async {
    guard !submitting else { return }
    await flight(action)
    // A refused call starts no read, so a change observed while it was out
    // is read here, after `submitting` clears.
    if flightMissedChange {
      flightMissedChange = false
      await refresh()
    }
  }

  /// The time ``submitting`` is set: the hub call and, when the hub
  /// accepts, one read.
  private func flight(_ action: () async throws(PDFError) -> Void) async {
    submitting = true
    flightReadStarted = false
    flightMissedChange = false
    defer {
      submitting = false
      let waiting = flightWaiters
      flightWaiters = []
      for waiter in waiting { waiter.resume() }
    }
    do {
      try await action()
      error = nil
      readFailed = false
    } catch {
      self.error = error
      readFailed = false
      return
    }
    // This read starts after the hub answered, and no observed change
    // starts a read that could supersede it: one read ends the flight,
    // however many revisions arrive.
    flightReadStarted = true
    flightMissedChange = false
    await refresh()
  }

  /// Whether a picker shows `id`'s button as on.
  package func isSelected(_ id: ToolID) -> Bool { activeToolID == id }

  /// Whether a picker disables `id`'s button: an action is in flight, or the
  /// tool is not usable and not active. The active tool's button stays
  /// enabled, so it can always return to the default tool.
  package func isDisabled(_ id: ToolID) -> Bool {
    submitting || (!usable.contains(id) && !isSelected(id))
  }

  /// A tap on `id`'s button in a picker: the active tool's button returns to
  /// the default tool, any other activates its tool.
  package func buttonTapped(_ id: ToolID) async {
    if isSelected(id) { await activateDefault() } else { await activate(id) }
  }
}
