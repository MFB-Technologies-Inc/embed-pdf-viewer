import EmbedPDF
import SwiftUI

/// A scrolling, zooming reader over a document's stage. Each visible page
/// sits where the stage put it and renders at the stage's render scale.
public struct StageReaderView<PageOverlay: View>: View {
  let model: StageReaderModel
  let pageOverlay: (StageVisiblePage) -> PageOverlay
  @Environment(\.displayScale) private var displayScale

  /// A reader that lays `pageOverlay` over each visible page, in the page's
  /// frame with its origin at the page's top left.
  /// ``StageVisiblePage/viewportPoint(_:)`` turns a point in it into a
  /// viewport point. The overlay's own gestures take priority over the
  /// reader's drag; set ``StageReaderModel/isPanEnabled`` to false when it
  /// needs every touch.
  public init(
    model: StageReaderModel, @ViewBuilder pageOverlay: @escaping (StageVisiblePage) -> PageOverlay
  ) {
    self.model = model
    self.pageOverlay = pageOverlay
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
        // either way.
        .gesture(
          DragGesture(minimumDistance: 0)
            .onChanged { value in Task { await model.dragChanged(translation: value.translation) } }
            .onEnded { value in Task { await model.dragEnded(velocity: value.velocity) } },
          including: model.isPanEnabled ? .all : .subviews
        )
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
        .task { await model.task() }
        .task(id: geometry.size) { await model.sizeChanged(geometry.size, displayScale: displayScale) }
      }
    }
  }
}

extension StageReaderView where PageOverlay == EmptyView {
  /// A reader with no page overlay.
  public init(model: StageReaderModel) {
    self.init(model: model) { _ in EmptyView() }
  }
}
