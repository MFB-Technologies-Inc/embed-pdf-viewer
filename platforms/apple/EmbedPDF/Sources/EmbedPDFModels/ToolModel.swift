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
/// overwrites one that started later, so while ``task()`` runs an action's
/// own read can give way to the read its `toolChanged` event starts:
/// ``activeToolID`` follows when that one lands, which can be after the
/// action returns. A failed read, as when the document closes, empties the
/// model and sets ``error``; ``task()`` ends with one more refresh, so a
/// closed document leaves the model empty.
///
/// ``usable`` holds each registered tool whose `canUseTool` is `true`. A
/// tool registered or removed, and a change to the document's permissions,
/// is a state revision, so ``usable`` follows them while ``task()`` runs.
/// The hub sends no event when a `ToolPermission` the app registered starts
/// to answer differently: call ``refresh()`` after the state it reads from
/// changes.
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
    do {
      try await action()
      error = nil
      readFailed = false
    } catch {
      self.error = error
      readFailed = false
      return
    }
    await refresh()
  }

  /// Whether a picker shows `id`'s button as on.
  package func isSelected(_ id: ToolID) -> Bool { activeToolID == id }

  /// Whether a picker disables `id`'s button: the tool is not registered, or
  /// its permission refuses it.
  package func isDisabled(_ id: ToolID) -> Bool { !usable.contains(id) }

  /// A tap on `id`'s button in a picker: the active tool's button returns to
  /// the default tool, any other activates its tool.
  package func buttonTapped(_ id: ToolID) async {
    if isSelected(id) { await activateDefault() } else { await activate(id) }
  }
}
