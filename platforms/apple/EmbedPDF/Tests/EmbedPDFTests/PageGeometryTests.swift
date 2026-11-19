import CoreGraphics
import CustomDump
import EmbedPDF
import Foundation
import Testing

extension BaseSuite {
  @Suite struct PageGeometryTests {
    let size = Size(width: 200, height: 100)

    @Test func normalizesRotationToAQuarterTurn() {
      expectNoDifference(PageGeometry(rotation: -90, pageSize: size).rotation, 270)
      expectNoDifference(PageGeometry(rotation: 450, pageSize: size).rotation, 90)
      expectNoDifference(PageGeometry(rotation: 359, pageSize: size).rotation, 0)
      _ = PageGeometry(rotation: .max, pageSize: size)
      _ = PageGeometry(rotation: .min, pageSize: size)
    }

    @Test func degreesReduceToAQuarterTurn() {
      expectNoDifference(RenderRotation(degrees: -90), .clockwise270)
      expectNoDifference(RenderRotation(degrees: 450), .clockwise90)
      expectNoDifference(RenderRotation(degrees: 89.6), .clockwise90)
      expectNoDifference(RenderRotation(degrees: -90.4), .clockwise270)
      expectNoDifference(RenderRotation(degrees: Int.max).degrees % 90, 0)
      expectNoDifference(RenderRotation(degrees: Int.min).degrees % 90, 0)
      expectNoDifference(RenderRotation(degrees: Double.nan), .none)
      expectNoDifference(RenderRotation(degrees: Double.infinity), .none)
      expectNoDifference(RenderRotation(degrees: 1e12).degrees % 90, 0)
    }

    @Test func turnReportsItsDegrees() {
      expectNoDifference(Self.turns.map(\.degrees), [0, 90, 180, 270])
      expectNoDifference(Self.turns.map(\.isQuarterTurn), [false, true, false, true])
      let geometry = PageGeometry(turn: .clockwise270, pageSize: size)
      expectNoDifference(geometry.rotation, 270)
      expectNoDifference(geometry, PageGeometry(rotation: 270, pageSize: size))
    }

    @Test(arguments: turns)
    func coreGraphicsValuesMatchPageValues(turn: RenderRotation) {
      let geometry = PageGeometry(turn: turn, pageSize: Size(width: 612, height: 792))
      let point = Point(x: 30, y: 20)
      let rect = Rect(x: 10, y: 20, width: 100, height: 12)
      expectNoDifference(Point(geometry.toDisplay(CGPoint(point))), geometry.toDisplay(point))
      expectNoDifference(Point(geometry.toPage(CGPoint(point))), geometry.toPage(point))
      expectNoDifference(Rect(geometry.toDisplay(CGRect(rect))), geometry.toDisplay(rect))
      expectNoDifference(Rect(geometry.toPage(CGRect(rect))), geometry.toPage(rect))
      expectNoDifference(Size(geometry.displayCGSize), geometry.displaySize)
    }

    @Test func imageSwapsItsSidesOnAQuarterTurn() throws {
      let image = try #require(Self.context(width: 4, height: 2)?.makeImage())
      let turned = try PageGeometry(turn: .clockwise90, pageSize: size).toDisplay(image)
      expectNoDifference([turned.width, turned.height], [2, 4])
    }

    @Test(arguments: [0, 90, 180, 270])
    func pointsRoundTrip(rotation: Int) {
      let geometry = PageGeometry(rotation: rotation, pageSize: size)
      let point = Point(x: 30, y: 20)
      expectNoDifference(geometry.toPage(geometry.toDisplay(point)), point)
    }

    @Test func quarterTurnMapsTopLeftToTopRight() {
      let geometry = PageGeometry(rotation: 90, pageSize: size)
      expectNoDifference(geometry.displaySize, Size(width: 100, height: 200))
      expectNoDifference(geometry.toDisplay(Point(x: 0, y: 0)), Point(x: 100, y: 0))
      expectNoDifference(
        geometry.toDisplay(Rect(x: 10, y: 20, width: 30, height: 40)),
        Rect(x: 40, y: 10, width: 40, height: 30))
      expectNoDifference(geometry.turn, .clockwise90)
    }

    @Test func imageTurnsClockwiseWithThePage() throws {
      // 2x1 bitmap: left pixel red, right pixel blue. A clockwise quarter
      // turn makes it 1x2 with red on top.
      let image = try #require(Self.bitmap(left: .red, right: .blue))
      #expect(try PageGeometry(rotation: 0, pageSize: size).toDisplay(image) === image)
      let turned = try PageGeometry(rotation: 90, pageSize: size).toDisplay(image)
      expectNoDifference([turned.width, turned.height], [1, 2])
      expectNoDifference(Self.pixel(turned, x: 0, y: 0), .red)
      expectNoDifference(Self.pixel(turned, x: 0, y: 1), .blue)
    }

    @Test func rotatedPageReportsItsTurnAndRendersToMatch() async throws {
      let viewer = try PDFViewer(ViewerConfig(render: RenderConfig(), pageEdit: true))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data("report"))
      let edit = try await viewer.pageEdit(for: document)
      let before = try await viewer.pages(of: document)[0]
      _ = try await edit.setRotation(.clockwise90, of: [before.page])
      let page = try await viewer.pages(of: document)[0]
      let geometry = page.geometry
      expectNoDifference(geometry.rotation, 90)
      expectNoDifference(geometry.displaySize, Size(width: before.height, height: before.width))
      let corner = Point(x: 10, y: 20)
      expectNoDifference(geometry.toPage(geometry.toDisplay(corner)), corner)
      let render = try await viewer.render(for: document)
      // report.pdf is portrait (612 by 792); turned, it renders landscape.
      let rendered = try await render.renderPage(
        page.page, options: RenderPageOptions(rotation: geometry.turn, width: 200))
      #expect(rendered.image.width > rendered.image.height)
      let thumbnail = try await render.renderThumbnail(page.page)
      #expect(thumbnail.image.height > thumbnail.image.width)
      let turned = try geometry.toDisplay(thumbnail.image)
      expectNoDifference(turned.width, thumbnail.image.height)
      expectNoDifference(turned.height, thumbnail.image.width)
      try await viewer.destroy()
    }

    static let turns: [RenderRotation] = [.none, .clockwise90, .clockwise180, .clockwise270]

    enum Pixel: Equatable { case red, blue, other }

    private static func context(width: Int, height: Int) -> CGContext? {
      CGContext(
        data: nil, width: width, height: height, bitsPerComponent: 8, bytesPerRow: width * 4,
        space: CGColorSpace(name: CGColorSpace.sRGB)!,
        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
    }

    private static func color(_ pixel: Pixel) -> CGColor {
      switch pixel {
      case .red: CGColor(srgbRed: 1, green: 0, blue: 0, alpha: 1)
      case .blue: CGColor(srgbRed: 0, green: 0, blue: 1, alpha: 1)
      case .other: CGColor(srgbRed: 0, green: 1, blue: 0, alpha: 1)
      }
    }

    private static func bitmap(left: Pixel, right: Pixel) -> CGImage? {
      guard let context = context(width: 2, height: 1) else { return nil }
      context.setFillColor(color(left))
      context.fill(CGRect(x: 0, y: 0, width: 1, height: 1))
      context.setFillColor(color(right))
      context.fill(CGRect(x: 1, y: 0, width: 1, height: 1))
      return context.makeImage()
    }

    /// The pixel at `(x, y)` counted from the top-left corner.
    private static func pixel(_ image: CGImage, x: Int, y: Int) -> Pixel {
      guard let context = context(width: image.width, height: image.height) else { return .other }
      context.interpolationQuality = .none
      context.draw(image, in: CGRect(x: 0, y: 0, width: image.width, height: image.height))
      guard let data = context.data else { return .other }
      let bytes = data.assumingMemoryBound(to: UInt8.self)
      let offset = y * context.bytesPerRow + x * 4
      switch (bytes[offset], bytes[offset + 1], bytes[offset + 2]) {
      case (255, 0, 0): return .red
      case (0, 0, 255): return .blue
      default: return .other
      }
    }
  }
}
