import EmbedPDF
import SwiftUI

/// A scrolling strip of page thumbnails over a `ThumbnailModel`.
///
/// The strip is a scroll view around a lazy stack along `axis`. Each cell is
/// 96 points across the strip and as long as the page's shape as shown makes
/// it, so a cell keeps its size when its image lands. A cell loads its
/// thumbnail when it appears and again when the engine invalidates its page,
/// and stops waiting when it leaves; until an image lands it is a plain
/// placeholder. The cell for `current` has an accent outline.
///
/// Each cell is a button that calls `onSelect` with its page. VoiceOver
/// reads a cell as "Page" and its one-based number, and `current` as
/// selected. The strip does not show the model's `error` and does not
/// scroll to `current`; the app does both where it needs them.
///
/// Give the strip the same `pages` the model was made from, and a new model
/// when the page list changes.
///
///     ThumbnailStrip(model: thumbnails, pages: pages, current: currentPage) { page in
///       currentPage = page.page
///     }
@MainActor
public struct ThumbnailStrip: View {
  private let model: ThumbnailModel
  private let pages: [PageInfo]
  private let current: PageRef?
  private let axis: Axis
  private let onSelect: (PageInfo) -> Void

  public init(
    model: ThumbnailModel, pages: [PageInfo], current: PageRef?, axis: Axis = .horizontal,
    onSelect: @escaping (PageInfo) -> Void
  ) {
    self.model = model
    self.pages = pages
    self.current = current
    self.axis = axis
    self.onSelect = onSelect
  }

  public var body: some View {
    let cells = ForEach(pages, id: \.page) { page in
      ThumbnailCell(model: model, page: page, isCurrent: page.page == current, axis: axis) {
        onSelect(page)
      }
    }
    switch axis {
    case .horizontal:
      ScrollView(.horizontal) { LazyHStack(spacing: ThumbnailCell.spacing) { cells }.padding(ThumbnailCell.spacing) }
    case .vertical:
      ScrollView(.vertical) { LazyVStack(spacing: ThumbnailCell.spacing) { cells }.padding(ThumbnailCell.spacing) }
    }
  }
}

@MainActor
private struct ThumbnailCell: View {
  static let spacing: CGFloat = 8
  /// The cell's length across the strip, in points.
  static let breadth: CGFloat = 96

  let model: ThumbnailModel
  let page: PageInfo
  let isCurrent: Bool
  let axis: Axis
  let select: () -> Void

  private struct LoadID: Hashable {
    let page: PageRef
    let revision: UInt64
  }

  var body: some View {
    Button(action: select) {
      Group {
        if let image = model.image(for: page.page) {
          Image(decorative: image, scale: 1).resizable()
        } else {
          Rectangle().fill(.quaternary)
        }
      }
      .aspectRatio(aspectRatio, contentMode: .fit)
      .frame(
        width: axis == .vertical ? Self.breadth : nil,
        height: axis == .horizontal ? Self.breadth : nil
      )
      .overlay { Rectangle().strokeBorder(isCurrent ? Color.accentColor : Color.clear, lineWidth: 2) }
      .contentShape(Rectangle())
    }
    .buttonStyle(.plain)
    .accessibilityLabel("Page \(page.index + 1)")
    .accessibilityAddTraits(isCurrent ? .isSelected : [])
    .task(id: LoadID(page: page.page, revision: model.revision(for: page.page))) {
      await model.load(page.page)
    }
  }

  /// Width over height of the page as shown; a square for a page with no
  /// area.
  private var aspectRatio: CGFloat {
    let size = page.geometry.displayCGSize
    return size.width > 0 && size.height > 0 ? size.width / size.height : 1
  }
}
