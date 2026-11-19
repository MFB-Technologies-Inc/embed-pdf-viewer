import CustomDump
import EmbedPDF
@testable import EmbedPDFModels
import Foundation
import Testing

@MainActor
@Suite(.serialized, .timeLimit(.minutes(1)))
struct ToolModelTests {
  struct Tools {
    let viewer: PDFViewer
    let document: DocumentInfo
    let interaction: InteractionCapability
    let model: ToolModel
    let fallback: ToolID
    let observing: Task<Void, Never>
  }

  /// A model over a viewer with the annotation tools, its `task()` running
  /// and its first snapshot read.
  func tools() async throws -> Tools {
    let viewer = try PDFViewer(ViewerConfig(interaction: true, annotation: ""))
    try await viewer.start()
    let document = try await viewer.open(pluginFixture("annotation_kinds"))
    let interaction = try await viewer.interaction(for: document)
    let model = ToolModel(interaction: interaction)
    let observing = Task { await model.task() }
    let fallback = try await interaction.defaultToolID()
    try await expectObserved({ model.activeToolID }, equals: fallback) {}
    return Tools(
      viewer: viewer, document: document, interaction: interaction, model: model, fallback: fallback,
      observing: observing)
  }

  /// A `canUse` a test flips.
  final class Gate: ToolPermission, @unchecked Sendable {
    private let lock = NSLock()
    private var open = false
    func set(_ open: Bool) { lock.withLock { self.open = open } }
    func canUse() -> Bool { lock.withLock { open } }
  }

  @Test func theFirstSnapshotListsTheRegisteredToolsAsUsable() async throws {
    let tools = try await tools()
    let kernel = try await tools.interaction.tools()
    expectNoDifference(tools.model.tools, kernel)
    #expect(kernel.contains { $0.id == ToolID.ink.rawValue })
    expectNoDifference(tools.model.usable, Set(kernel.map { ToolID($0.id) }))
    expectNoDifference(tools.model.error, nil)
    try await tools.viewer.destroy()
    await tools.observing.value
  }

  @Test func activateSetsTheActiveTool() async throws {
    let tools = try await tools()
    let model = tools.model
    try await expectObserved({ model.activeToolID }, equals: .ink) { await model.activate(.ink) }
    expectNoDifference(model.error, nil)
    let kernel = try await tools.interaction.activeToolID()
    expectNoDifference(kernel, .ink)
    try await tools.viewer.destroy()
    await tools.observing.value
  }

  @Test func aToolActivatedOnTheCapabilityReachesTheModelThroughItsEvent() async throws {
    let tools = try await tools()
    let model = tools.model
    try await expectObserved({ model.activeToolID }, equals: .ink) {
      try await tools.interaction.activateTool(.ink)
    }
    try await tools.viewer.destroy()
    await tools.observing.value
  }

  @Test func activateDefaultReturnsToTheDefaultTool() async throws {
    let tools = try await tools()
    let model = tools.model
    try await expectObserved({ model.activeToolID }, equals: .ink) { await model.activate(.ink) }
    try await expectObserved({ model.activeToolID }, equals: tools.fallback) { await model.activateDefault() }
    expectNoDifference(model.error, nil)
    try await tools.viewer.destroy()
    await tools.observing.value
  }

  @Test func anUnregisteredToolSetsNotFoundAndLeavesTheActiveTool() async throws {
    let tools = try await tools()
    let model = tools.model
    try await expectObserved({ model.activeToolID }, equals: .ink) { await model.activate(.ink) }
    await model.activate("nobody-registered-this")
    expectNoDifference(model.error?.code, .notFound)
    expectNoDifference(model.activeToolID, .ink)
    // The hub agrees, so no later read moves it.
    let kernel = try await tools.interaction.activeToolID()
    expectNoDifference(kernel, .ink)
    try await tools.viewer.destroy()
    await tools.observing.value
  }

  @Test func aLaterSuccessfulActivationClearsTheError() async throws {
    let tools = try await tools()
    let model = tools.model
    await model.activate("nobody-registered-this")
    expectNoDifference(model.error?.code, .notFound)
    await model.activate(.ink)
    expectNoDifference(model.error, nil)
    await model.activate("nobody-registered-this")
    expectNoDifference(model.error?.code, .notFound)
    await model.activateDefault()
    expectNoDifference(model.error, nil)
    try await tools.viewer.destroy()
    await tools.observing.value
  }

  @Test func aBackgroundRefreshKeepsAnActionsError() async throws {
    let tools = try await tools()
    let model = tools.model
    await model.activate("nobody-registered-this")
    await model.refresh()
    expectNoDifference(model.error?.code, .notFound)
    try await tools.viewer.destroy()
    await tools.observing.value
  }

  @Test func closingTheDocumentEndsTheTaskAndEmptiesTheModel() async throws {
    let tools = try await tools()
    let model = tools.model
    try await expectObserved({ model.activeToolID }, equals: .ink) { await model.activate(.ink) }
    try await tools.viewer.close(tools.document)
    await tools.observing.value
    expectNoDifference(model.tools, [])
    expectNoDifference(model.activeToolID, nil)
    expectNoDifference(model.usable, [])
    expectNoDifference(model.error?.code, .instanceClosed)
    try await tools.viewer.destroy()
  }

  @Test func aToolRegisteredLaterReachesTheModelWithItsPermission() async throws {
    let tools = try await tools()
    let model = tools.model
    let gate = Gate()
    let gated: ToolID = "gated"
    let registration = try await expectRegistered(gated, in: tools, permission: gate)
    #expect(!model.usable.contains(gated))
    #expect(model.isDisabled(gated))
    // The host's permission changes with no word from the viewer: the app
    // refreshes.
    gate.set(true)
    try await expectObserved({ model.usable.contains(gated) }, equals: true) { await model.refresh() }
    #expect(!model.isDisabled(gated))
    try await expectObserved({ model.tools.contains { $0.id == gated.rawValue } }, equals: false) {
      registration.unregister()
    }
    #expect(!model.usable.contains(gated))
    try await tools.viewer.destroy()
    await tools.observing.value
  }

  func expectRegistered(_ id: ToolID, in tools: Tools, permission: Gate) async throws -> Registration {
    var registration: Registration?
    let model = tools.model
    try await expectObserved({ model.tools.contains { $0.id == id.rawValue } }, equals: true) {
      registration = try await tools.interaction.registerTool(
        InteractionTool(id: id.rawValue, cursor: "crosshair", enables: []), permission: permission)
    }
    return try #require(registration)
  }

  @Test func aButtonIsSelectedForTheActiveToolAndDisabledForAnUnusableOne() async throws {
    let tools = try await tools()
    let model = tools.model
    #expect(!model.isSelected(.ink))
    #expect(!model.isDisabled(.ink))
    #expect(model.isDisabled("nobody-registered-this"))
    await model.activate(.ink)
    #expect(model.isSelected(.ink))
    #expect(!model.isSelected(.pan))
    #expect(!model.isDisabled(.pan))
    try await tools.viewer.destroy()
    await tools.observing.value
  }

  @Test func theActiveToolsButtonStaysEnabledWhenItsToolIsNoLongerUsable() async throws {
    let tools = try await tools()
    let model = tools.model
    let gate = Gate()
    gate.set(true)
    let gated: ToolID = "gated"
    _ = try await expectRegistered(gated, in: tools, permission: gate)
    await model.activate(gated)
    #expect(model.isSelected(gated))
    gate.set(false)
    try await expectObserved({ model.usable.contains(gated) }, equals: false) { await model.refresh() }
    // Still the way back to the default tool.
    #expect(model.isSelected(gated))
    #expect(!model.isDisabled(gated))
    await model.buttonTapped(gated)
    expectNoDifference(model.activeToolID, tools.fallback)
    #expect(model.isDisabled(gated))
    try await tools.viewer.destroy()
    await tools.observing.value
  }

  /// Every `toolChanged` the hub sends from now until the viewer is
  /// destroyed.
  func toolChanges(_ interaction: InteractionCapability) -> Task<[InteractionToolChangedEvent], Never> {
    let events = interaction.events
    return Task {
      var changes: [InteractionToolChangedEvent] = []
      for await event in events {
        if case let .toolChanged(change) = event { changes.append(change) }
      }
      return changes
    }
  }

  @Test func aSecondTapWhileAnActivationIsInFlightIsIgnored() async throws {
    let tools = try await tools()
    let model = tools.model
    let changes = toolChanges(tools.interaction)
    let first = Task { await model.buttonTapped(.ink) }
    let second = Task { await model.buttonTapped(.ink) }
    await first.value
    await second.value
    // Neither toggled off nor activated twice.
    expectNoDifference(model.activeToolID, .ink)
    expectNoDifference(model.submitting, false)
    try await tools.viewer.destroy()
    await tools.observing.value
    let seen = await changes.value
    expectNoDifference(
      seen,
      [InteractionToolChangedEvent(toolId: ToolID.ink.rawValue, previousToolId: tools.fallback.rawValue)])
  }

  @Test func aRequestForAnotherToolWhileAnActivationIsInFlightIsIgnored() async throws {
    let tools = try await tools()
    let model = tools.model
    let changes = toolChanges(tools.interaction)
    let first = Task { await model.activate(.ink) }
    let second = Task { await model.activate(.pan) }
    await first.value
    await second.value
    expectNoDifference(model.activeToolID, .ink)
    expectNoDifference(model.submitting, false)
    try await tools.viewer.destroy()
    await tools.observing.value
    let seen = await changes.value
    expectNoDifference(
      seen,
      [InteractionToolChangedEvent(toolId: ToolID.ink.rawValue, previousToolId: tools.fallback.rawValue)])
  }

  @Test func everyButtonIsDisabledWhileAnActivationIsInFlight() async throws {
    let tools = try await tools()
    let model = tools.model
    #expect(!model.submitting)
    var inFlight: [Bool] = []
    // The activation's own read: the hub has answered, the snapshot has not
    // landed.
    model.snapshots.readFinished = {
      inFlight = [model.submitting, model.isDisabled(.ink), model.isDisabled(.pan)]
    }
    await model.activate(.ink)
    model.snapshots.readFinished = nil
    expectNoDifference(inFlight, [true, true, true])
    expectNoDifference([model.submitting, model.isDisabled(.ink), model.isDisabled(.pan)], [false, false, false])
    expectNoDifference(model.activeToolID, .ink)
    try await tools.viewer.destroy()
    await tools.observing.value
  }

  @Test func aFailedActivationEndsTheFlight() async throws {
    let tools = try await tools()
    let model = tools.model
    await model.activate("nobody-registered-this")
    expectNoDifference(model.error?.code, .notFound)
    expectNoDifference(model.submitting, false)
    #expect(!model.isDisabled(.ink))
    try await tools.viewer.destroy()
    await tools.observing.value
  }

  @Test func tappingAnotherToolActivatesItAndTappingTheActiveToolReturnsToTheDefault() async throws {
    let tools = try await tools()
    let model = tools.model
    try await expectObserved({ model.activeToolID }, equals: .ink) { await model.buttonTapped(.ink) }
    try await expectObserved({ model.activeToolID }, equals: tools.fallback) { await model.buttonTapped(.ink) }
    try await tools.viewer.destroy()
    await tools.observing.value
  }
}
