import EmbedPDF
import EmbedPDFModels
import SwiftUI

/// Highlights the search's hits on one page a stage shows.
///
/// Each line of each hit on the page is filled with the search settings'
/// highlight color, and the lines of the active hit with its active color,
/// blended with the page under the overlay as the settings' blend mode says.
/// The engine paints no hits into the page's tiles, so a reader that shows
/// search draws them with this.
///
/// The overlay draws each hit where `StageVisiblePage.localRect(_:info:)`
/// puts it, from the overlay's top left, so size it to the page and place
/// it over the page's image, under anything that takes touches, such
/// as a ``TextSelectionOverlay``. An upright line is a rect with 2 pt
/// corners; a turned one is its four corners. It reads
/// `SearchModel.hits`, `SearchModel.activeHit` and `SearchModel.highlight`
/// and redraws when any of them changes, a settings update included.
///
/// It does not run `model`: one search model serves every page, so run it
/// once, as the ``SearchBar`` showing it does. The overlay is decoration
/// only. It is hidden from accessibility and takes no touches, so they
/// reach whatever is under it.
///
/// The colors and blend mode come from the settings unless given here, as
/// a web layer's CSS variables win over them. A setting's color is read as
/// CSS: `#rgb`, `#rgba`, `#rrggbb`, `#rrggbbaa`, `rgb()` and `rgba()`, the
/// 16 basic color names, `orange` and `transparent`. A color it can't read
/// draws the plugin's default for that slot, `#ffd500` or `#ff9632`.
public struct SearchHighlightOverlay: View {
  let model: SearchModel
  let visiblePage: StageVisiblePage
  let info: PageInfo
  let color: Color?
  let activeColor: Color?
  let blendMode: BlendMode?

  /// Creates an overlay for a page a stage shows, turned as the stage turns
  /// it, view rotation included.
  ///
  /// - Parameters:
  ///   - model: The search whose hits are drawn.
  ///   - visiblePage: The page as the stage reports it.
  ///   - info: The same page's `PageInfo`, for its unrotated size.
  ///   - color: The fill of a hit, over the setting's `color`.
  ///   - activeColor: The fill of the active hit, over the setting's `activeColor`.
  ///   - blendMode: How the fills blend with the page, over the setting's `blendMode`.
  public init(
    model: SearchModel, visiblePage: StageVisiblePage, info: PageInfo,
    color: Color? = nil, activeColor: Color? = nil, blendMode: BlendMode? = nil
  ) {
    self.model = model
    self.visiblePage = visiblePage
    self.info = info
    self.color = color
    self.activeColor = activeColor
    self.blendMode = blendMode
  }

  public var body: some View {
    let highlight = model.highlight
    let defaults = SearchModel.defaultHighlight
    let fill = color ?? Self.fill(highlight.color, or: defaults.color)
    let activeFill = activeColor ?? Self.fill(highlight.activeColor, or: defaults.activeColor)
    // Performance: every page filters the whole list, once per change of it.
    let hits = model.hits.filter { $0.page == visiblePage.page }
    let active = model.activeHit
    Canvas { context, _ in
      for hit in hits {
        let shading = GraphicsContext.Shading.color(hit == active ? activeFill : fill)
        for segment in hit.segments {
          context.fill(Self.path(segment.quad, on: visiblePage, info: info), with: shading)
        }
      }
    }
    // A blend mode set inside the canvas reaches only what the canvas drew,
    // so the whole canvas blends with the page under it.
    // Hits inside the canvas composite source-over, so overlapping hits don't
    // darken each other, unlike web and Android, where each fill blends alone.
    .blendMode(blendMode ?? Self.blendMode(highlight.blendMode))
    .allowsHitTesting(false)
    .accessibilityHidden(true)
  }

  /// The outline of one line of a hit in the overlay. A quad that is an
  /// axis-aligned box in page space, as the web layers test it, is a rect
  /// from its upper left corner to its lower right one.
  static func path(_ quad: Quad, on visiblePage: StageVisiblePage, info: PageInfo) -> Path {
    let upright = quad.upperLeft.y == quad.upperRight.y && quad.lowerLeft.y == quad.lowerRight.y
      && quad.upperLeft.x == quad.lowerLeft.x
    if upright {
      let box = Rect(
        x: quad.upperLeft.x, y: quad.upperLeft.y, width: quad.lowerRight.x - quad.upperLeft.x,
        height: quad.lowerRight.y - quad.upperLeft.y)
      return Path(roundedRect: visiblePage.localRect(box, info: info), cornerRadius: 2)
    }
    return Path { path in
      path.addLines(
        [quad.upperLeft, quad.upperRight, quad.lowerRight, quad.lowerLeft].map {
          visiblePage.localPoint($0, info: info)
        })
      path.closeSubpath()
    }
  }

  /// `css` as a color, or `fallback` when `css` can't be read.
  private static func fill(_ css: String, or fallback: String) -> Color {
    (cssColor(css) ?? cssColor(fallback))?.color ?? .clear
  }

  /// The SwiftUI blend mode of the same name as `mode`.
  static func blendMode(_ mode: SearchBlendMode) -> BlendMode {
    switch mode {
    case .normal: .normal
    case .multiply: .multiply
    case .screen: .screen
    case .overlay: .overlay
    case .darken: .darken
    case .lighten: .lighten
    case .colorDodge: .colorDodge
    case .colorBurn: .colorBurn
    case .hardLight: .hardLight
    case .softLight: .softLight
    case .difference: .difference
    case .exclusion: .exclusion
    case .hue: .hue
    case .saturation: .saturation
    case .color: .color
    case .luminosity: .luminosity
    }
  }

  /// `text` as a CSS color: hex in 3, 4, 6 or 8 digits, `rgb()` or `rgba()`
  /// with commas or spaces and an optional `/ alpha`, channels as numbers
  /// or percentages, or one of the named colors. `nil` for anything else.
  static func cssColor(_ text: String) -> CSSColor? {
    let css = text.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    if css.hasPrefix("#") { return hexColor(css.dropFirst()) }
    if let named = namedColors[css] { return named }
    guard let open = css.firstIndex(of: "("), css.hasSuffix(")") else { return nil }
    let name = css[..<open].trimmingCharacters(in: .whitespacesAndNewlines)
    guard name == "rgb" || name == "rgba" else { return nil }
    let body = css[css.index(after: open)..<css.index(before: css.endIndex)]
    let halves = body.split(separator: "/", omittingEmptySubsequences: false)
    guard halves.count <= 2 else { return nil }
    let parts = halves[0].split(whereSeparator: { $0 == "," || $0 == " " })
      .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
      .filter { !$0.isEmpty }
    guard parts.count >= 3 else { return nil }
    var channels: [Double] = []
    for part in parts.prefix(3) {
      guard let value = channel(part, max: 255) else { return nil }
      channels.append(value / 255)
    }
    let alpha: Double?
    switch (halves.count, parts.count) {
    case (2, 3): alpha = channel(halves[1].trimmingCharacters(in: .whitespacesAndNewlines), max: 1)
    case (1, 4): alpha = channel(parts[3], max: 1)
    case (1, 3): alpha = 1
    default: alpha = nil
    }
    guard let alpha else { return nil }
    return CSSColor(red: channels[0], green: channels[1], blue: channels[2], opacity: alpha)
  }

  /// A channel: a number up to `max`, or a percentage of it, clamped.
  private static func channel(_ text: some StringProtocol, max: Double) -> Double? {
    let value = text.hasSuffix("%")
      ? Double(text.dropLast()).map { $0 / 100 * max }
      : Double(text)
    guard let value, value.isFinite else { return nil }
    return min(Swift.max(value, 0), max)
  }

  private static func hexColor(_ hex: Substring) -> CSSColor? {
    guard hex.allSatisfy(\.isHexDigit) else { return nil }
    let full: String
    switch hex.count {
    case 3, 4: full = String(hex.flatMap { [$0, $0] })
    case 6, 8: full = String(hex)
    default: return nil
    }
    guard let value = UInt32(full, radix: 16) else { return nil }
    // CSS puts alpha last.
    let rgba = full.count == 6 ? value << 8 | 0xff : value
    func byte(_ shift: UInt32) -> Double { Double(rgba >> shift & 0xff) / 255 }
    return CSSColor(red: byte(24), green: byte(16), blue: byte(8), opacity: byte(0))
  }

  private static let namedColors: [String: CSSColor] = [
    "black": CSSColor(0x000000),
    "silver": CSSColor(0xc0c0c0),
    "gray": CSSColor(0x808080),
    "grey": CSSColor(0x808080),
    "white": CSSColor(0xffffff),
    "maroon": CSSColor(0x800000),
    "red": CSSColor(0xff0000),
    "purple": CSSColor(0x800080),
    "fuchsia": CSSColor(0xff00ff),
    "magenta": CSSColor(0xff00ff),
    "green": CSSColor(0x008000),
    "lime": CSSColor(0x00ff00),
    "olive": CSSColor(0x808000),
    "yellow": CSSColor(0xffff00),
    "navy": CSSColor(0x000080),
    "blue": CSSColor(0x0000ff),
    "teal": CSSColor(0x008080),
    "aqua": CSSColor(0x00ffff),
    "cyan": CSSColor(0x00ffff),
    "orange": CSSColor(0xffa500),
    "transparent": CSSColor(red: 0, green: 0, blue: 0, opacity: 0),
  ]
}

/// A color read from CSS, each channel from 0 through 1 in sRGB.
struct CSSColor: Equatable {
  var red: Double
  var green: Double
  var blue: Double
  var opacity: Double

  var color: Color { Color(.sRGB, red: red, green: green, blue: blue, opacity: opacity) }
}

extension CSSColor {
  /// An opaque color from `0xrrggbb`.
  init(_ rgb: UInt32) {
    self.init(
      red: Double(rgb >> 16 & 0xff) / 255, green: Double(rgb >> 8 & 0xff) / 255,
      blue: Double(rgb & 0xff) / 255, opacity: 1)
  }
}
