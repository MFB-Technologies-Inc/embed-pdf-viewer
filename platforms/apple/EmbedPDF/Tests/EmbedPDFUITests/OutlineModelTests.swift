import CustomDump
import EmbedPDF
import EmbedPDFUI
import Testing

@MainActor
@Suite(.serialized, .timeLimit(.minutes(1)))
struct OutlineModelTests {
  @Test func cancellingInitialObservationDoesNotPoisonTheRetainedModel() async throws {
    let (viewer, _, _, model) = try await opened()
    let cancelled = Task { withUnsafeCurrentTask { $0?.cancel() }; await model.task() }
    await cancelled.value
    expectNoDifference(model.error, nil)
    let reopened = Task { await model.task() }
    defer { reopened.cancel() }
    try await expectObserved({ model.status }, equals: .ready) {}
    expectNoDifference(model.error, nil)
    try await viewer.destroy()
    await reopened.value
  }

  @Test func startingObservationPreservesAnActionFailure() async throws {
    let (viewer, _, _, model) = try await opened()
    await model.reload()
    let item = try #require(await model.create(OutlineItemInput(title: "Parent", clearTarget: true)))
    await model.move([item.itemRef], to: item.itemRef)
    let failure = try #require(model.error)
    let observing = Task { await model.task() }
    defer { observing.cancel() }
    try await expectObserved({ model.status }, equals: .ready) {}
    expectNoDifference(model.error, failure)
    try await viewer.destroy()
    await observing.value
  }

  func opened(scope: [String]? = nil, stage: Bool = false) async throws -> (PDFViewer, DocumentInfo, OutlineCapability, OutlineModel) {
    let viewer = try PDFViewer(ViewerConfig(scope: scope, stage: stage ? StageConfig() : nil, outline: true))
    try await viewer.start()
    let document = try await viewer.open(pluginFixture())
    let outline = try await viewer.outline(for: document)
    return (viewer, document, outline, OutlineModel(viewer: viewer, outline: outline))
  }

  @Test func rowsExpansionSelectionAndEditsComeFromTheCapability() async throws {
    let (viewer, _, _, model) = try await opened()
    await model.reload()
    expectNoDifference(model.status, .ready)
    let parent = try #require(await model.create(OutlineItemInput(title: "Parent", clearTarget: true)))
    let child = try #require(await model.create(OutlineItemInput(title: "Child", parent: parent.itemRef, clearTarget: true, color: "#ff0000")))
    await model.collapse(parent.itemRef)
    #expect(!model.rows.contains { $0.item.itemRef == child.itemRef })
    await model.expand(parent.itemRef)
    expectNoDifference(model.rows.first { $0.item.itemRef == child.itemRef }?.depth, 1)
    await model.select([child.itemRef])
    expectNoDifference(model.selectedItems, [child.itemRef])
    let updated = await model.update(child.itemRef, OutlineItemPatch(title: "Renamed", clear: [.color, .target]))
    expectNoDifference(updated?.color, nil)
    await model.move([child.itemRef], to: nil, position: .start)
    #expect(model.items.first { $0.itemRef == child.itemRef }?.parent == nil)
    await model.delete([child.itemRef, parent.itemRef])
    #expect(!model.items.contains { $0.itemRef == parent.itemRef })
    expectNoDifference(model.error, nil)
    _ = OutlinePanel(model: model)
    try await viewer.destroy()
  }

  /// Most revisions are a scroll or a zoom, which leave the outline as it
  /// was. Rereading it then must not invalidate the views that show it; a
  /// real change still must.
  @Test func unchangedRevisionDoesNotNotify() async throws {
    let (viewer, document, outline, model) = try await opened()
    await model.reload()
    let page = try await viewer.pages(of: document)[1].page
    let parent = try #require(await model.create(OutlineItemInput(title: "Parent", target: .goto(destination: .fit(page: page)), color: "#ff0000", bold: true)))
    let child = try #require(await model.create(OutlineItemInput(title: "Child", parent: parent.itemRef, clearTarget: true)))
    await model.select([parent.itemRef])
    let shown = { @MainActor in
      _ = (model.items, model.rows, model.selectedItems, model.currentItem, model.status, model.incomplete, model.canEdit)
    }
    // Each read lifts the item's action as a new handle.
    #expect(model.items.contains { $0.action != nil })
    #expect(await !observationFires(shown) { await model.refresh() })
    #expect(try await observationFires({ model.items }) {
      _ = try await outline.update(parent.itemRef, OutlineItemPatch(title: "Renamed"))
      await model.refresh()
    })
    #expect(try await observationFires({ model.rows }) {
      try await outline.collapse(parent.itemRef)
      await model.refresh()
    })
    // The selection is compared before it's written, as the items are.
    #expect(await !observationFires({ model.selectedItems }) { await model.select([parent.itemRef]) })
    #expect(await observationFires({ model.selectedItems }) { await model.select([child.itemRef]) })
    expectNoDifference(model.selectedItems, [child.itemRef])
    expectNoDifference(model.error, nil)
    try await viewer.destroy()
  }

  @Test func twoModelsObserveTheSameViewerIndependently() async throws {
    let (viewer, _, outline, first) = try await opened()
    let second = OutlineModel(viewer: viewer, outline: outline)
    let firstTask = Task { await first.task() }
    let secondTask = Task { await second.task() }
    defer { firstTask.cancel(); secondTask.cancel() }
    try await expectObserved({ first.status }, equals: .ready) {}
    try await expectObserved({ second.status }, equals: .ready) {}
    try await expectObserved({ first.rows.contains { $0.item.title == "External" } }, equals: true) {
      try await expectObserved({ second.rows.contains { $0.item.title == "External" } }, equals: true) {
        _ = try await outline.create(OutlineItemInput(title: "External", clearTarget: true))
      }
    }
    try await viewer.destroy()
    await firstTask.value
    await secondTask.value
  }

  @Test func closedDocumentEndsObservationAndCannotEditItsReplacement() async throws {
    let (viewer, document, _, model) = try await opened()
    let task = Task { await model.task() }
    try await expectObserved({ model.status }, equals: .ready) {}
    try await viewer.close(document)
    await task.value
    expectNoDifference(model.rows.count, 0)
    expectNoDifference(model.canEdit, false)
    expectNoDifference(model.error?.code, .instanceClosed)
    let next = try await viewer.open(pluginFixture())
    let nextModel = OutlineModel(viewer: viewer, outline: try await viewer.outline(for: next))
    await nextModel.reload()
    expectNoDifference(nextModel.error, nil)
    let refused = await model.create(OutlineItemInput(title: "Old document"))
    #expect(refused == nil)
    #expect(!nextModel.items.contains { $0.title == "Old document" })
    try await viewer.destroy()
  }

  @Test func activationUsesTheRustStageAndDeniedEditsKeepTheirError() async throws {
    let (viewer, document, _, model) = try await opened(stage: true)
    let stage = try await viewer.stage(for: document)
    try await stage.setViewportSize(Size(width: 800, height: 600))
    await model.reload()
    let page = try await viewer.pages(of: document)[2].page
    let item = try #require(await model.create(OutlineItemInput(title: "Third page", target: .goto(destination: .fit(page: page)))))
    let events = stage.events
    _ = await model.activate(item.itemRef)
    for await event in events { if case let .pageChanged(change) = event, change.pageIndex == 2 { break } }
    expectNoDifference(model.lastActivation, .revealed)
    let index = try await stage.currentPageIndex()
    expectNoDifference(index, 2)
    try await viewer.destroy()
    let (restrictedViewer, _, _, restricted) = try await opened(scope: ["doc.open"])
    await restricted.refresh()
    expectNoDifference(restricted.error, nil)
    expectNoDifference(restricted.canEdit, false)
    let refused = await restricted.create(OutlineItemInput(title: "Denied"))
    #expect(refused == nil)
    expectNoDifference(restricted.error?.code, .permissionDenied)
    try await restrictedViewer.destroy()
  }
}
