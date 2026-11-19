import EmbedPDF
import SwiftUI

/// A scrolling, zooming reader over a document's stage. Each visible page
/// sits where the stage put it and renders at the stage's render scale.
///
/// A model made with an `InteractionCapability` also gets the reader's
/// touches as pointer input. A drag pans under the default tool and goes to
/// the active tool under any other
/// (``StageReaderModel/forwardsDrags``); the route is chosen when the drag
/// starts and holds until it ends. A tap goes to the hub once either way: as
/// ``StageReaderModel/tapped(at:)`` when drags pan, and as the forwarded
/// drag's own down and up when they do not. A forwarded drag the system
/// cancels is cancelled at the hub too, so the tool does not commit it.
public struct StageReaderView<PageOverlay: View>: View {
  let model: StageReaderModel
  let pageOverlay: (StageVisiblePage) -> PageOverlay
  let onLinkActivation: @MainActor (LinkActivation) -> Void
  @Environment(\.displayScale) private var displayScale
  /// Where the open drag goes, chosen at its first change.
  private enum DragRoute { case pan, tool }
  /// The open drag's route, or `nil` when no drag is open.
  @State private var drag: DragRoute?
  /// True while a drag is down. SwiftUI resets it when it cancels the
  /// gesture without calling `onEnded`.
  @GestureState private var touching = false

  /// A reader that lays `pageOverlay` over each visible page, in the page's
  /// frame with its origin at the page's top left.
  /// ``StageVisiblePage/viewportPoint(_:)`` turns a point in it into a
  /// viewport point. The overlay's own gestures take priority over the
  /// reader's drag, whether that drag pans or goes to the active tool; set
  /// ``StageReaderModel/isPanEnabled`` to false when the overlay needs
  /// every touch that would pan.
  ///
  /// `onLinkActivation` receives each of the model's
  /// ``StageReaderModel/linkActivations`` while the view is shown. Opening a
  /// `.uri` is the app's job.
  public init(
    model: StageReaderModel, @ViewBuilder pageOverlay: @escaping (StageVisiblePage) -> PageOverlay,
    onLinkActivation: @escaping @MainActor (LinkActivation) -> Void = { _ in }
  ) {
    self.model = model
    self.pageOverlay = pageOverlay
    self.onLinkActivation = onLinkActivation
  }

  public var body: some View {
    ModelScope(model: model) { model in
      GeometryReader { geometry in
        ZStack(alignment: .topLeading) {
          ForEach(model.visiblePages, id: \.page.pageObjectNumber) { page in
            ZStack(alignment: .topLeading) {
              PageImageView(
                render: model.render, page: page.page,
                scale: StageReaderModel.renderScale(
                  page.transform.renderScale, contentWidth: page.transform.contentWidth,
                  contentHeight: page.transform.contentHeight, zooming: model.isZooming),
                revision: model.renderRevision(for: page.page))
                .frame(width: page.transform.viewWidth, height: page.transform.viewHeight)
              pageOverlay(page)
            }
            .frame(
              width: page.transform.viewWidth, height: page.transform.viewHeight, alignment: .topLeading)
            .offset(x: page.screenX, y: page.screenY)
          }
        }
        .frame(width: geometry.size.width, height: geometry.size.height, alignment: .topLeading)
        .clipped()
        .contentShape(Rectangle())
        // Masked rather than `gesture(_:isEnabled:)`, which needs iOS 18
        // while the package supports iOS 17. Overlay gestures stay live
        // either way. A drag the active tool takes is live with the pan off.
        .gesture(
          DragGesture(minimumDistance: 0)
            .updating($touching) { _, touching, _ in touching = true }
            .onChanged { value in dragChanged(value, model: model) }
            .onEnded { value in dragEnded(value, model: model) },
          including: model.isPanEnabled || model.forwardsDrags ? .all : .subviews
        )
        .onChange(of: touching) { _, touching in
          // After the turn `onEnded` runs in, so a drag that ended is not
          // taken for one SwiftUI dropped.
          if !touching { Task { dragDropped(model: model) } }
        }
        .simultaneousGesture(
          MagnifyGesture()
            .onChanged { value in model.magnifyChanged(value.magnification, at: value.startLocation) }
            .onEnded { _ in Task { await model.magnifyEnded() } }
        )
        // Simultaneous: the zero-distance drag above recognizes on touch-down
        // and would otherwise win every tap.
        .simultaneousGesture(
          SpatialTapGesture(count: 2, coordinateSpace: .local)
            .onEnded { value in Task { await model.doubleTapped(at: value.location) } }
        )
        // While drags are forwarded the drag above has already sent this
        // touch's down and up; a second pair would be a second tap.
        .simultaneousGesture(
          SpatialTapGesture(count: 1, coordinateSpace: .local)
            .onEnded { value in if !model.forwardsDrags { model.tapped(at: value.location) } }
        )
        .task { await model.task() }
        .task {
          for await activation in model.linkActivations { onLinkActivation(activation) }
        }
        .task(id: geometry.size) { await model.sizeChanged(geometry.size, displayScale: displayScale) }
      }
    }
  }

  /// Routes a drag's change. The first one picks the route, and a
  /// forwarded drag opens with a down where the touch began.
  private func dragChanged(_ value: DragGesture.Value, model: StageReaderModel) {
    let route = drag ?? (model.forwardsDrags ? .tool : .pan)
    let opening = drag == nil
    drag = route
    switch route {
    case .pan:
      Task { await model.dragChanged(translation: value.translation) }
    case .tool:
      if opening { model.pointerDown(at: value.startLocation) }
      if !opening || value.location != value.startLocation { model.pointerMoved(to: value.location) }
    }
  }

  private func dragEnded(_ value: DragGesture.Value, model: StageReaderModel) {
    let route = drag ?? .pan
    drag = nil
    switch route {
    case .pan:
      Task { await model.dragEnded(velocity: value.velocity) }
    case .tool:
      model.pointerUp(at: value.location)
    }
  }

  /// Closes a drag SwiftUI cancelled without calling `onEnded`. A forwarded
  /// one is cancelled at the hub; the model ends a pan the mask cut off.
  private func dragDropped(model: StageReaderModel) {
    guard let route = drag else { return }
    drag = nil
    if route == .tool { model.pointerCancelled() }
  }
}

extension StageReaderView where PageOverlay == EmptyView {
  /// A reader with no page overlay. `onLinkActivation` receives each of the
  /// model's ``StageReaderModel/linkActivations`` while the view is shown.
  public init(model: StageReaderModel, onLinkActivation: @escaping @MainActor (LinkActivation) -> Void = { _ in }) {
    self.init(model: model, pageOverlay: { _ in EmptyView() }, onLinkActivation: onLinkActivation)
  }
}
