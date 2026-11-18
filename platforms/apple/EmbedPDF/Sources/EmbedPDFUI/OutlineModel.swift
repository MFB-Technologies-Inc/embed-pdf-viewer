import EmbedPDF
public import EmbedPDFBindings
import Observation

@MainActor
@Observable
public final class OutlineModel {
  public private(set) var items: [OutlineItem] = []
  public private(set) var rows: [OutlineRow] = []
  public private(set) var selectedItems: [OutlineItemRef] = []
  public private(set) var currentItem: OutlineItemRef?
  public private(set) var status: ResourceStatus = .idle
  public private(set) var incomplete = false
  public private(set) var canEdit = false
  public private(set) var performing = false
  public private(set) var error: PDFError?
  public private(set) var lastActivation: LinkActivation?
  @ObservationIgnored private var errorIsReadFailure = false
  @ObservationIgnored private let viewer: PDFViewer
  @ObservationIgnored private let outline: OutlineCapability
  @ObservationIgnored private let snapshots = SnapshotRefresh()
  @ObservationIgnored private var observation: UInt64 = 0

  public init(viewer: PDFViewer, outline: OutlineCapability) {
    self.viewer = viewer
    self.outline = outline
  }

  public func task() async {
    observation &+= 1
    let session = observation
    defer { if observation == session { snapshots.invalidate() } }
    await observeCapability(viewer: viewer, events: outline.events,
      initial: {
        guard self.observation == session else { return }
        do {
          try await self.outline.ensureLoaded()
          guard self.observation == session, !Task.isCancelled else { return }
          if self.errorIsReadFailure { self.error = nil; self.errorIsReadFailure = false }
        } catch {
          guard self.observation == session, !Task.isCancelled else { return }
          self.errorIsReadFailure = true
          self.error = controlError(error)
        }
        await self.refresh()
      },
      refresh: { if self.observation == session { await self.refresh() } },
      receive: { _ in if self.observation == session { await self.refresh() } })
    if observation == session { await refresh() }
  }

  public func refresh() async {
    await snapshots.run(load: {
      let items = try await self.outline.list()
      let rows = try await self.outline.rows()
      let selected = try await self.outline.selectedItems()
      let current = try await self.outline.currentItem()
      let status = try await self.outline.status()
      let incomplete = try await self.outline.isIncomplete()
      let editable = try await self.outline.canEdit()
      return (items, rows, selected, current, status, incomplete, editable)
    }, apply: {
      let (items, rows, selected, current, status, incomplete, editable) = $0
      // Most revisions are a scroll or a zoom that leaves the outline as it
      // was: write only what changed, so the views reading these fields stay
      // valid. Items hold an action handle and aren't `Equatable`, so they're
      // compared field by field.
      if !items.elementsEqual(self.items, by: Self.sameItem) { self.items = items }
      if !rows.elementsEqual(self.rows, by: Self.sameRow) { self.rows = rows }
      if self.selectedItems != selected { self.selectedItems = selected }
      if self.currentItem != current { self.currentItem = current }
      if self.status != status { self.status = status }
      if self.incomplete != incomplete { self.incomplete = incomplete }
      if self.canEdit != editable { self.canEdit = editable }
    }, failure: {
      self.items = []; self.rows = []; self.selectedItems = []; self.currentItem = nil
      self.canEdit = false
      self.errorIsReadFailure = true
      self.error = controlError($0)
    })
  }

  private static func sameRow(_ a: OutlineRow, _ b: OutlineRow) -> Bool {
    a.depth == b.depth && a.hasChildren == b.hasChildren && a.expanded == b.expanded
      && sameItem(a.item, b.item)
  }

  private static func sameItem(_ a: OutlineItem, _ b: OutlineItem) -> Bool {
    guard a.itemRef == b.itemRef, a.parent == b.parent, a.title == b.title, a.target == b.target,
      a.open == b.open, a.color == b.color, a.bold == b.bold, a.italic == b.italic
    else { return false }
    // Each read lifts a new handle, so equal actions compare by their JSON.
    switch (a.action, b.action) {
    case (nil, nil): return true
    case let (a?, b?): return a === b || a.json() == b.json()
    default: return false
    }
  }

  public func reload() async { _ = await perform { try await self.outline.refresh() } }
  public func select(_ refs: [OutlineItemRef]) async { _ = await perform { try await self.outline.select(refs) } }
  public func expand(_ ref: OutlineItemRef) async { _ = await perform { try await self.outline.expand(ref) } }
  public func collapse(_ ref: OutlineItemRef) async { _ = await perform { try await self.outline.collapse(ref) } }
  public func toggle(_ ref: OutlineItemRef) async { _ = await perform { try await self.outline.toggle(ref) } }
  public func create(_ input: OutlineItemInput, options: OutlineCreateOptions = OutlineCreateOptions(select: true)) async -> OutlineItem? {
    await perform { try await self.outline.create(input, options: options) }
  }
  public func update(_ ref: OutlineItemRef, _ patch: OutlineItemPatch) async -> OutlineItem? {
    await perform { try await self.outline.update(ref, patch) }
  }
  public func move(_ refs: [OutlineItemRef], to parent: OutlineItemRef?, position: OutlinePosition = .end) async {
    _ = await perform { try await self.outline.move(refs, to: parent, position: position) }
  }
  public func delete(_ refs: [OutlineItemRef]) async { _ = await perform { try await self.outline.delete(refs) } }
  public func activate(_ ref: OutlineItemRef) async -> LinkActivation? {
    let result = await perform {
      try await self.outline.select([ref])
      return try await self.outline.activate(ref)
    }
    lastActivation = result
    return result
  }

  private func perform<Value>(_ action: () async throws -> Value) async -> Value? {
    guard !performing else { return nil }
    performing = true
    errorIsReadFailure = false
    error = nil
    defer { performing = false }
    do {
      let value = try await action()
      await refresh()
      return value
    } catch {
      await refresh()
      self.errorIsReadFailure = false
      self.error = controlError(error)
      return nil
    }
  }
}
