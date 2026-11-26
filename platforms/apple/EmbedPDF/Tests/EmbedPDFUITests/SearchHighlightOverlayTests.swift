import CoreGraphics
import CustomDump
import EmbedPDF
import EmbedPDFModels
import SwiftUI
import Testing
#if canImport(UIKit)
import UIKit
#endif

@testable import EmbedPDFUI

/// The search highlights of one page, drawn over white so a multiplied fill
/// shows its own color.
@MainActor
@Suite(.serialized, .timeLimit(.minutes(1)))
struct SearchHighlightOverlayTests {
  /// A search for "the" in `report`, on the page of its active hit, with
  /// another hit on that page.
  struct Searched {
    let viewer: PDFViewer
    let search: SearchCapability
    let model: SearchModel
    let info: PageInfo
    let active: SearchHit
    let other: SearchHit
  }

  func searched() async throws -> Searched {
    let viewer = try PDFViewer(ViewerConfig(search: SearchConfig()))
    try await viewer.start()
    let document = try await viewer.open(pluginFixture())
    let search = try await viewer.search(for: document)
    let model = SearchModel(search: search)
    model.text = "the"
    await model.searchSubmitted()
    let active = try #require(model.activeHit)
    let other = try #require(
      model.hits.first { $0 != active && $0.page == active.page && !$0.segments.isEmpty })
    let info = try #require(try await viewer.pages(of: document).first { $0.page == active.page })
    return Searched(viewer: viewer, search: search, model: model, info: info, active: active, other: other)
  }

  /// The page drawn at one view point per PDF point, turned `rotation`
  /// degrees by the stage.
  func visiblePage(_ info: PageInfo, rotation: Double = 0) -> StageVisiblePage {
    let turned = rotation == 90 || rotation == 270
    let viewWidth = turned ? info.height : info.width
    let viewHeight = turned ? info.width : info.height
    return StageVisiblePage(
      page: info.page, pageIndex: info.index, x: 0, y: 0, width: viewWidth, height: viewHeight,
      rotation: rotation, contentScale: 1, screenX: 0, screenY: 0,
      transform: StagePageTransform(
        rotation: rotation, viewWidth: viewWidth, viewHeight: viewHeight,
        contentWidth: info.width, contentHeight: info.height, deviceWidth: viewWidth,
        deviceHeight: viewHeight, renderScale: 1, viewScale: 1, baseScale: 1, zoom: 1),
      visibleRect: Rect(x: 0, y: 0, width: info.width, height: info.height))
  }

  /// A renderer of `overlay` over `background`, at one pixel per view point.
  func renderer(
    _ overlay: SearchHighlightOverlay, on page: StageVisiblePage, background: Color = .white
  ) -> ImageRenderer<some View> {
    let renderer = ImageRenderer(
      content: ZStack {
        background
        overlay
      }
      .frame(width: page.transform.viewWidth, height: page.transform.viewHeight))
    renderer.scale = 1
    return renderer
  }

  /// The pixel at the middle of `hit`'s first line, as `page` shows it.
  func middle(of hit: SearchHit, in image: CGImage, page: StageVisiblePage, info: PageInfo) throws -> RGB {
    let quad = try #require(hit.segments.first).quad
    let box = Rect(
      x: quad.upperLeft.x, y: quad.upperLeft.y, width: quad.lowerRight.x - quad.upperLeft.x,
      height: quad.lowerRight.y - quad.upperLeft.y)
    let local = page.localRect(box, info: info)
    return try pixel(in: image, x: Int(local.midX), y: Int(local.midY))
  }

  /// The pixel at `(x, y)`, counted from the top left, in 8-bit sRGB.
  func pixel(in image: CGImage, x: Int, y: Int) throws -> RGB {
    let sRGB = try #require(CGColorSpace(name: CGColorSpace.sRGB))
    let context = try #require(
      CGContext(
        data: nil, width: image.width, height: image.height, bitsPerComponent: 8,
        bytesPerRow: image.width * 4, space: sRGB,
        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue))
    context.draw(image, in: CGRect(x: 0, y: 0, width: image.width, height: image.height))
    let bytes = try #require(context.data).assumingMemoryBound(to: UInt8.self)
    let offset = y * context.bytesPerRow + x * 4
    return RGB(red: Int(bytes[offset]), green: Int(bytes[offset + 1]), blue: Int(bytes[offset + 2]))
  }

  struct RGB: Equatable {
    var red: Int
    var green: Int
    var blue: Int

    /// Whether each channel is within 3 of `other`'s, for antialiasing and
    /// color conversion.
    func isClose(to other: RGB) -> Bool {
      abs(red - other.red) <= 3 && abs(green - other.green) <= 3 && abs(blue - other.blue) <= 3
    }
  }

  func expectColor(_ actual: RGB, _ expected: RGB, sourceLocation: SourceLocation = #_sourceLocation) {
    #expect(actual.isClose(to: expected), "expected \(expected), drew \(actual)", sourceLocation: sourceLocation)
  }

  @Test
  func aHitIsFilledWhereItsWordIs() async throws {
    let s = try await searched()
    let page = visiblePage(s.info)
    let image = try #require(renderer(SearchHighlightOverlay(model: s.model, visiblePage: page, info: s.info), on: page).cgImage)
    expectColor(try middle(of: s.other, in: image, page: page, info: s.info), RGB(0xffd500))
    // A corner of the page holds no hit.
    expectColor(try pixel(in: image, x: 1, y: 1), RGB(0xffffff))
    try await s.viewer.destroy()
  }

  @Test
  func theActiveHitUsesTheActiveColour() async throws {
    let s = try await searched()
    let page = visiblePage(s.info)
    let image = try #require(renderer(SearchHighlightOverlay(model: s.model, visiblePage: page, info: s.info), on: page).cgImage)
    expectColor(try middle(of: s.active, in: image, page: page, info: s.info), RGB(0xff9632))
    try await s.viewer.destroy()
  }

  @Test
  func aHitOnAnotherPageIsNotDrawn() async throws {
    let s = try await searched()
    var elsewhere = visiblePage(s.info)
    elsewhere.page = PageRef(pageObjectNumber: s.info.page.pageObjectNumber + 1000)
    let image = try #require(
      renderer(SearchHighlightOverlay(model: s.model, visiblePage: elsewhere, info: s.info), on: elsewhere).cgImage)
    expectColor(try middle(of: s.active, in: image, page: elsewhere, info: s.info), RGB(0xffffff))
    try await s.viewer.destroy()
  }

  /// The setting's multiply blends the fill with the page under the
  /// overlay, so the text under a hit stays readable.
  @Test
  func theFillBlendsWithThePageUnderIt() async throws {
    let s = try await searched()
    let page = visiblePage(s.info)
    let gray = Color(.sRGB, red: 128 / 255, green: 128 / 255, blue: 128 / 255)
    let image = try #require(
      renderer(SearchHighlightOverlay(model: s.model, visiblePage: page, info: s.info), on: page, background: gray).cgImage)
    // 0xffd500 multiplied by 0x808080.
    expectColor(try middle(of: s.other, in: image, page: page, info: s.info), RGB(red: 128, green: 107, blue: 0))
    try await s.viewer.destroy()
  }

  @Test
  func aTurnedPageMapsTheHitWithThePage() async throws {
    let s = try await searched()
    let page = visiblePage(s.info, rotation: 90)
    let image = try #require(renderer(SearchHighlightOverlay(model: s.model, visiblePage: page, info: s.info), on: page).cgImage)
    #expect(image.width > image.height)
    expectColor(try middle(of: s.active, in: image, page: page, info: s.info), RGB(0xff9632))
    expectColor(try middle(of: s.other, in: image, page: page, info: s.info), RGB(0xffd500))
    try await s.viewer.destroy()
  }

  @Test
  func aSettingsChangeRepaints() async throws {
    let s = try await searched()
    let page = visiblePage(s.info)
    let renderer = renderer(SearchHighlightOverlay(model: s.model, visiblePage: page, info: s.info), on: page)
    let before = try #require(renderer.cgImage)
    expectColor(try middle(of: s.other, in: before, page: page, info: s.info), RGB(0xffd500))
    try await s.search.updateSettings(
      SearchSettingsPatch(
        highlight: SearchHighlightPatch(color: "#00ff00", activeColor: "rgb(0 0 255)", blendMode: .normal)))
    await s.model.refresh()
    expectNoDifference(s.model.highlight.color, "#00ff00")
    // The renderer takes the change on a later turn of the main run loop.
    var after = try #require(renderer.cgImage)
    let deadline = ContinuousClock.now + .seconds(5)
    while try !middle(of: s.other, in: after, page: page, info: s.info).isClose(to: RGB(0x00ff00)),
      ContinuousClock.now < deadline
    {
      try await Task.sleep(for: .milliseconds(20))
      after = try #require(renderer.cgImage)
    }
    expectColor(try middle(of: s.active, in: after, page: page, info: s.info), RGB(0x0000ff))
    expectColor(try middle(of: s.other, in: after, page: page, info: s.info), RGB(0x00ff00))
    try await s.viewer.destroy()
  }

  @Test
  func anOverrideColourWins() async throws {
    let s = try await searched()
    let page = visiblePage(s.info)
    let overlay = SearchHighlightOverlay(
      model: s.model, visiblePage: page, info: s.info, color: Color(.sRGB, red: 1, green: 0, blue: 0))
    let image = try #require(renderer(overlay, on: page).cgImage)
    expectColor(try middle(of: s.other, in: image, page: page, info: s.info), RGB(0xff0000))
    expectColor(try middle(of: s.active, in: image, page: page, info: s.info), RGB(0xff9632))
    try await s.viewer.destroy()
  }

  @Test
  func anUnreadableColourFallsBackToTheDefault() async throws {
    let s = try await searched()
    let page = visiblePage(s.info)
    try await s.search.updateSettings(
      SearchSettingsPatch(highlight: SearchHighlightPatch(color: "var(--accent)", activeColor: "#ggg")))
    await s.model.refresh()
    expectNoDifference(s.model.highlight.color, "var(--accent)")
    let image = try #require(renderer(SearchHighlightOverlay(model: s.model, visiblePage: page, info: s.info), on: page).cgImage)
    expectColor(try middle(of: s.other, in: image, page: page, info: s.info), RGB(0xffd500))
    expectColor(try middle(of: s.active, in: image, page: page, info: s.info), RGB(0xff9632))
    try await s.viewer.destroy()
  }

  @Test(arguments: [
    ("#ffd500", CSSColor(red: 1, green: 0xd5 / 255, blue: 0, opacity: 1)),
    ("#fd5", CSSColor(red: 1, green: 0xdd / 255, blue: 0x55 / 255, opacity: 1)),
    ("#f008", CSSColor(red: 1, green: 0, blue: 0, opacity: 0x88 / 255)),
    ("#ff000080", CSSColor(red: 1, green: 0, blue: 0, opacity: 0x80 / 255)),
    (" Blue ", CSSColor(red: 0, green: 0, blue: 1, opacity: 1)),
    ("green", CSSColor(red: 0, green: 0x80 / 255, blue: 0, opacity: 1)),
    ("orange", CSSColor(red: 1, green: 0xa5 / 255, blue: 0, opacity: 1)),
    ("transparent", CSSColor(red: 0, green: 0, blue: 0, opacity: 0)),
    ("rgba(255, 0, 0, 0.5)", CSSColor(red: 1, green: 0, blue: 0, opacity: 0.5)),
    ("rgb(100% 0 0 / 25%)", CSSColor(red: 1, green: 0, blue: 0, opacity: 0.25)),
    ("rgb(0 0 255)", CSSColor(red: 0, green: 0, blue: 1, opacity: 1)),
    ("rgb(300, -5, 0)", CSSColor(red: 1, green: 0, blue: 0, opacity: 1)),
    ("rgba(255,0,0)", CSSColor(red: 1, green: 0, blue: 0, opacity: 1)),
  ] as [(String, CSSColor)])
  func cssColoursParse(text: String, color: CSSColor) {
    expectNoDifference(SearchHighlightOverlay.cssColor(text), color)
  }

  @Test(arguments: ["var(--accent)", "#ggg", "#12345", "rgb(1, 2)", "rgb(1, 2, 3, 4, 5)", "hsl(0 0% 0%)", "rgb(1 2 3"])
  func unreadableCSSIsNil(text: String) {
    expectNoDifference(SearchHighlightOverlay.cssColor(text), nil)
  }

  @Test
  func everyBlendModeMaps() {
    let modes: [(SearchBlendMode, BlendMode)] = [
      (.normal, .normal), (.multiply, .multiply), (.screen, .screen), (.overlay, .overlay),
      (.darken, .darken), (.lighten, .lighten), (.colorDodge, .colorDodge),
      (.colorBurn, .colorBurn), (.hardLight, .hardLight), (.softLight, .softLight),
      (.difference, .difference), (.exclusion, .exclusion), (.hue, .hue),
      (.saturation, .saturation), (.color, .color), (.luminosity, .luminosity),
    ]
    for (setting, mode) in modes {
      expectNoDifference(SearchHighlightOverlay.blendMode(setting), mode)
    }
  }

  #if canImport(UIKit)
  /// A label a caller puts on the overlay still makes no element, as the
  /// label would without the overlay's own hiding. macOS builds the
  /// accessibility tree of a hosting view only for an outside client, so
  /// this runs on iOS.
  @Test
  func theOverlayHasNoAccessibilityElement() async throws {
    let s = try await searched()
    let page = visiblePage(s.info)
    let window = UIWindow(frame: CGRect(x: 0, y: 0, width: page.transform.viewWidth, height: page.transform.viewHeight))
    let host = UIHostingController(
      rootView: SearchHighlightOverlay(model: s.model, visiblePage: page, info: s.info)
        .accessibilityLabel("Highlights")
        .frame(width: page.transform.viewWidth, height: page.transform.viewHeight))
    window.rootViewController = host
    window.makeKeyAndVisible()
    defer { window.isHidden = true }
    host.view.layoutIfNeeded()
    expectNoDifference(accessibilityElements(in: host.view), [])
    try await s.viewer.destroy()
  }

  /// The labels of the accessibility elements under `object`.
  func accessibilityElements(in object: NSObject) -> [String] {
    var found = object.isAccessibilityElement ? [object.accessibilityLabel ?? ""] : []
    let count = object.accessibilityElementCount()
    if object.accessibilityElements == nil, count != NSNotFound, count > 0 {
      for index in 0..<count {
        if let element = object.accessibilityElement(at: index) as? NSObject {
          found += accessibilityElements(in: element)
        }
      }
    }
    let children: [Any] = object.accessibilityElements ?? (object as? UIView)?.subviews ?? []
    for case let child as NSObject in children {
      found += accessibilityElements(in: child)
    }
    return found
  }
  #endif
}

extension SearchHighlightOverlayTests.RGB {
  init(_ hex: UInt32) {
    self.init(red: Int(hex >> 16 & 0xff), green: Int(hex >> 8 & 0xff), blue: Int(hex & 0xff))
  }
}
