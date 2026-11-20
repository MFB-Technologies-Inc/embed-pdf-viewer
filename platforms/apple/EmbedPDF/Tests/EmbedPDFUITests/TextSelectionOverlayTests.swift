import CoreGraphics
import CustomDump
import EmbedPDF
import Testing

@testable import EmbedPDFUI

@Suite struct TextSelectionOverlayTests {
  let pageSize = Size(width: 612, height: 792)

  /// The frame's top left corner for a rect of no size at `point`, which is
  /// where `localRect` puts the point itself.
  func localPoint(_ point: Point, in size: CGSize, geometry: PageGeometry) -> CGPoint {
    TextSelectionOverlay.localRect(
      Rect(x: point.x, y: point.y, width: 0, height: 0), in: size, geometry: geometry
    ).origin
  }

  @Test func unturnedFramePointScalesToThePage() {
    let geometry = PageGeometry(turn: .none, pageSize: pageSize)
    let size = CGSize(width: 306, height: 396)
    expectNoDifference(
      TextSelectionOverlay.pagePoint(CGPoint(x: 153, y: 198), in: size, geometry: geometry),
      Point(x: 306, y: 396))
    expectNoDifference(
      TextSelectionOverlay.pagePoint(CGPoint(x: 100, y: 50), in: size, geometry: geometry),
      Point(x: 200, y: 100))
  }

  @Test func quarterTurnedFramePointTurnsBackToThePage() {
    let geometry = PageGeometry(turn: .clockwise90, pageSize: pageSize)
    // The page as shown is 792 by 612, drawn at half size.
    let size = CGSize(width: 396, height: 306)
    expectNoDifference(
      TextSelectionOverlay.pagePoint(CGPoint(x: 99, y: 153), in: size, geometry: geometry),
      Point(x: 306, y: 594))
  }

  @Test func framePointScalesEachAxisByItsOwnLength() {
    let geometry = PageGeometry(turn: .none, pageSize: pageSize)
    // Twice as wide as the page and as tall as it.
    let size = CGSize(width: 1224, height: 792)
    expectNoDifference(
      TextSelectionOverlay.pagePoint(CGPoint(x: 200, y: 100), in: size, geometry: geometry),
      Point(x: 100, y: 100))
  }

  @Test(arguments: [
    (RenderRotation.none, CGSize(width: 306, height: 396)),
    (.clockwise90, CGSize(width: 396, height: 306)),
    (.clockwise180, CGSize(width: 306, height: 396)),
    (.clockwise270, CGSize(width: 396, height: 306)),
  ])
  func localRectInvertsPagePoint(turn: RenderRotation, size: CGSize) {
    let geometry = PageGeometry(turn: turn, pageSize: pageSize)
    let start = CGPoint(x: 99, y: 153)
    let point = TextSelectionOverlay.pagePoint(start, in: size, geometry: geometry)
    let local = localPoint(point, in: size, geometry: geometry)
    #expect(abs(local.x - start.x) < 0.001)
    #expect(abs(local.y - start.y) < 0.001)
  }

  @Test func localRectScalesAPageRectToTheFrame() {
    let geometry = PageGeometry(turn: .clockwise90, pageSize: pageSize)
    // At 90 degrees `(x, y)` shows at `(792 - y, x)`, so the rect shows at
    // x 492...592, y 100...300, drawn at half size.
    expectNoDifference(
      TextSelectionOverlay.localRect(
        Rect(x: 100, y: 200, width: 200, height: 100), in: CGSize(width: 396, height: 306),
        geometry: geometry),
      CGRect(x: 246, y: 50, width: 50, height: 100))
  }

  @Test func aFrameWithNoLengthOnAnAxisMapsThatAxisToZero() {
    let geometry = PageGeometry(turn: .none, pageSize: pageSize)
    let local = CGPoint(x: 100, y: 50)
    expectNoDifference(
      TextSelectionOverlay.pagePoint(local, in: .zero, geometry: geometry), Point(x: 0, y: 0))
    expectNoDifference(
      TextSelectionOverlay.pagePoint(local, in: CGSize(width: 0, height: 396), geometry: geometry),
      Point(x: 0, y: 100))
    expectNoDifference(
      TextSelectionOverlay.pagePoint(local, in: CGSize(width: 306, height: 0), geometry: geometry),
      Point(x: 200, y: 0))
  }

  @Test func aPageWithNoLengthOnAnAxisDrawsNothingOnThatAxis() {
    let size = CGSize(width: 306, height: 396)
    let rect = Rect(x: 10, y: 20, width: 30, height: 40)
    expectNoDifference(
      TextSelectionOverlay.localRect(
        rect, in: size, geometry: PageGeometry(turn: .none, pageSize: Size(width: 0, height: 0))),
      CGRect(x: 0, y: 0, width: 0, height: 0))
    expectNoDifference(
      TextSelectionOverlay.localRect(
        rect, in: size, geometry: PageGeometry(turn: .none, pageSize: Size(width: 0, height: 792))),
      CGRect(x: 0, y: 10, width: 0, height: 20))
  }
}
