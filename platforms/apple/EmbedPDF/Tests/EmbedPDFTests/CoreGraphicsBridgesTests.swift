import CoreGraphics
import CustomDump
import EmbedPDF
import Testing

extension BaseSuite {
  @Suite struct CoreGraphicsBridgesTests {
    @Test func pointRoundTrips() {
      let point = Point(x: 10.5, y: -20.25)
      expectNoDifference(CGPoint(point), CGPoint(x: 10.5, y: -20.25))
      expectNoDifference(Point(CGPoint(point)), point)
    }

    @Test func sizeRoundTrips() {
      let size = Size(width: 612, height: 792.5)
      expectNoDifference(CGSize(size), CGSize(width: 612, height: 792.5))
      expectNoDifference(Size(CGSize(size)), size)
    }

    @Test func rectRoundTrips() {
      let rect = Rect(x: 10, y: 20, width: 100, height: 12.5)
      expectNoDifference(CGRect(rect), CGRect(x: 10, y: 20, width: 100, height: 12.5))
      expectNoDifference(Rect(CGRect(rect)), rect)
    }

    @Test func rectFromCoreGraphicsIsStandardized() {
      expectNoDifference(
        Rect(CGRect(x: 110, y: 32, width: -100, height: -12)),
        Rect(x: 10, y: 20, width: 100, height: 12))
    }

    @Test func quadTakesTheCornersOfARect() {
      let quad = Quad(Rect(x: 10, y: 20, width: 100, height: 12))
      expectNoDifference(quad.upperLeft, Point(x: 10, y: 20))
      expectNoDifference(quad.upperRight, Point(x: 110, y: 20))
      expectNoDifference(quad.lowerLeft, Point(x: 10, y: 32))
      expectNoDifference(quad.lowerRight, Point(x: 110, y: 32))
    }
  }
}
