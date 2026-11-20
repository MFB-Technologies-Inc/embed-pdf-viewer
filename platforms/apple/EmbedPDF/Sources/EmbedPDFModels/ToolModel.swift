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
/// overwrites one that started later, and that rule is the only arbitration
/// between the reads. A failed read, as when the document closes, empties
/// the model and sets ``error``; ``task()`` ends with one more refresh, so a
/// closed document leaves the model empty.
///
/// ``activate(_:)`` and ``activateDefault()`` write ``activeToolID``
/// themselves when the hub accepts, then start one refresh for ``tools``
/// and ``usable``. So ``activeToolID`` holds the action's result when the
/// action returns, with or without ``task()`` running, and ``tools`` and
/// ``usable`` can trail it by one read: the action's refresh is dropped
/// when a newer read supersedes it or the calling task is cancelled, and
/// the newer read, or the next one ``task()`` makes, brings them. After an
/// action whose task was cancelled, without ``task()`` running, call
/// ``refresh()``.
///
/// ``submitting`` is `true` while the hub call of ``activate(_:)`` or
/// ``activateDefault()`` is out, and `false` again before the action's
/// refresh starts. A second action asked for while it is `true` is ignored.
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
/// error of an action. A caller that polls it can miss a failure one of
/// those cleared.
@MainActor
@Observable
public final class ToolModel {
  /// The registered tools, in the hub's order.
  public private(set) var tools: [InteractionTool] = []
  /// `nil` before the first read lands and after a failed one.
  public private(set) var activeToolID: ToolID?
  public private(set) var usable: Set<ToolID> = []
  public private(set) var error: PDFError?
  /// Whether the hub call of an ``activate(_:)`` or ``activateDefault()``
  /// is out.
  public private(set) var submitting = false
  /// Whether ``error`` came from a read, which the next successful read
  /// clears, and not from an action.
  @ObservationIgnored private var readFailed = false
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
      initial: { if self.observation == session { await self.refresh() } },
      // Performance: runs for every coalesced state revision, and most are
      // a scroll or a zoom that leaves the tools alone. The viewer's
      // revision stream does not say what changed, so each one costs a full
      // read (see `refresh()`).
      refresh: { if self.observation == session { await self.refresh() } },
      receive: { event in
        // Gestures and cursor moves leave the tools alone.
        guard self.observation == session, case .toolChanged = event else { return }
        await self.refresh()
      })
    if observation == session { await refresh() }
  }

  public func refresh() async {
    await snapshots.run(load: {
      let tools = try await self.interaction.tools()
      let active = try await self.interaction.activeToolID()
      var usable: Set<ToolID> = []
      // Performance: one `canUseTool` round trip to the kernel thread per
      // registered tool, one after another, on every coalesced state
      // revision, a scroll or a zoom included, and once more after each
      // action. With the annotation plugin's tools that is 20 calls, 22
      // with the two reads above, per read, and a `ToolPermission.canUse()`
      // the app registered is asked at that rate. Rereading `usable` only
      // when `tools` differs or on `toolChanged` removes the cost, but a
      // permission can change with no revision and no event (see the type's
      // comment), so that needs a signal for permission changes first. A
      // single engine call that answers for every tool removes the round
      // trips without one.
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

  /// Makes `id` the active tool. ``activeToolID`` is `id` when this returns
  /// after the hub accepted. An id nobody registered sets ``error`` to
  /// `not-found` and leaves ``activeToolID``.
  public func activate(_ id: ToolID) async {
    await submit { () throws(PDFError) in
      try await self.interaction.activateTool(id)
      return id
    }
  }

  /// Returns to the hub's default tool. ``activeToolID`` is that tool when
  /// this returns after the hub accepted.
  public func activateDefault() async {
    await submit { () throws(PDFError) in
      try await self.interaction.activateDefaultTool()
      // Performance: one more round trip per return to the default tool,
      // inside the time `submitting` is set, because the hub call does not
      // say which tool is the default. An `activateDefaultTool` that
      // returns the tool's id removes it.
      return try await self.interaction.activeToolID()
    }
  }

  /// Runs one action's hub call with ``submitting`` set, writes the active
  /// tool it returns, and then refreshes. Nothing suspends between that
  /// write and the start of the refresh, so a read that started before the
  /// hub answered is superseded and cannot put the old tool back.
  private func submit(_ action: () async throws(PDFError) -> ToolID) async {
    guard !submitting else { return }
    submitting = true
    let active: ToolID
    do {
      active = try await action()
    } catch {
      submitting = false
      self.error = error
      readFailed = false
      return
    }
    submitting = false
    error = nil
    readFailed = false
    if activeToolID != active { activeToolID = active }
    // Performance: a full read after every action (see `refresh()`), and
    // while `task()` runs the action's own `toolChanged` event starts a
    // second one, the later superseding the earlier when they overlap.
    // Telling that event from another caller's needs an id on the event;
    // with it, the event's read could be skipped.
    await refresh()
  }

  /// Whether a picker shows `id`'s button as on: `id` is ``activeToolID``.
  /// `ToolPicker` uses it, and a picker an app builds itself can too.
  public func isSelected(_ id: ToolID) -> Bool { activeToolID == id }

  /// Whether a picker disables `id`'s button: ``submitting`` is `true`, or
  /// the tool is not in ``usable`` and not active. The active tool's button
  /// stays enabled when its tool stops being usable, so a tap on it can
  /// always return to the default tool. An id nobody registered is
  /// disabled. `ToolPicker` uses it, and a picker an app builds itself can
  /// too.
  public func isDisabled(_ id: ToolID) -> Bool {
    submitting || (!usable.contains(id) && !isSelected(id))
  }

  /// A tap on `id`'s button in a picker: the active tool's button returns to
  /// the default tool, any other activates its tool.
  package func buttonTapped(_ id: ToolID) async {
    if isSelected(id) { await activateDefault() } else { await activate(id) }
  }
}
