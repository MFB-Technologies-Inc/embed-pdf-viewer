import CoreGraphics
import CustomDump
import EmbedPDF
import EmbedPDFModels
import Testing

@Suite struct StageVisiblePageGeometryTests {
  let info = PageInfo(page: PageRef(pageObjectNumber: 1), index: 0, width: 612, height: 792, rotation: 0)

  /// The page drawn at half a view point per PDF point, turned `rotation`
  /// degrees by the stage.
  func visiblePage(rotation: Double) -> StageVisiblePage {
    let turned = rotation == 90 || rotation == 270
    let viewWidth = (turned ? info.height : info.width) / 2
    let viewHeight = (turned ? info.width : info.height) / 2
    return StageVisiblePage(
      page: info.page, pageIndex: 0, x: 0, y: 0, width: viewWidth, height: viewHeight,
      rotation: rotation, contentScale: 0.5, screenX: 0, screenY: 0,
      transform: StagePageTransform(
        rotation: rotation, viewWidth: viewWidth, viewHeight: viewHeight,
        contentWidth: info.width, contentHeight: info.height, deviceWidth: viewWidth,
        deviceHeight: viewHeight, renderScale: 0.5, viewScale: 0.5, baseScale: 0.5, zoom: 1),
      visibleRect: Rect(x: 0, y: 0, width: info.width, height: info.height))
  }

  /// A page the stage has not sized yet has a zero view width, and a
  /// malformed one can have a zero page width. Neither divides by zero.
  @Test func aZeroWidthMapsToZeroNotToInfinity() {
    var unsized = visiblePage(rotation: 0)
    unsized.transform.viewWidth = 0
    // Every overlay point is the page's top left corner.
    expectNoDifference(unsized.pagePoint(CGPoint(x: 100, y: 50), info: info), Point(x: 0, y: 0))
    expectNoDifference(
      unsized.localRect(Rect(x: 10, y: 20, width: 30, height: 40), info: info), .zero)

    let empty = PageInfo(page: info.page, index: 0, width: 0, height: 792, rotation: 0)
    let page = visiblePage(rotation: 0)
    expectNoDifference(page.pagePoint(CGPoint(x: 100, y: 50), info: empty), Point(x: 0, y: 0))
    expectNoDifference(page.localRect(Rect(x: 10, y: 20, width: 30, height: 40), info: empty), .zero)
  }

  @Test func geometryTakesItsTurnFromTheStage() {
    expectNoDifference(
      visiblePage(rotation: 90).geometry(for: info),
      PageGeometry(turn: .clockwise90, pageSize: Size(width: 612, height: 792)))
    expectNoDifference(visiblePage(rotation: 89.999).geometry(for: info).turn, .clockwise90)
  }

  @Test func unturnedPagePointScalesByTheDrawnSize() {
    let page = visiblePage(rotation: 0)
    let start = CGPoint(x: 100, y: 50)
    let point = page.pagePoint(start, info: info)
    expectNoDifference(point, Point(x: 200, y: 100))
    let local = page.localRect(Rect(x: point.x, y: point.y, width: 1, height: 1), info: info)
    #expect(abs(local.minX - start.x) < 0.001)
    #expect(abs(local.minY - start.y) < 0.001)
    #expect(abs(local.width - 0.5) < 0.001)
    #expect(abs(local.height - 0.5) < 0.001)
  }

  @Test func quarterTurnedPagePointComesBackToItsStart() {
    let page = visiblePage(rotation: 90)
    let start = CGPoint(x: 100, y: 50)
    let point = page.pagePoint(start, info: info)
    expectNoDifference(point, Point(x: 100, y: 592))
    // A clockwise quarter turn puts the rect's page origin at its top right
    // corner on screen.
    let local = page.localRect(Rect(x: point.x, y: point.y, width: 1, height: 1), info: info)
    #expect(abs(local.maxX - start.x) < 0.001)
    #expect(abs(local.minY - start.y) < 0.001)
    #expect(abs(local.width - 0.5) < 0.001)
    #expect(abs(local.height - 0.5) < 0.001)
  }

  @Test(arguments: [0.0, 90, 180, 270])
  func localPointInvertsPagePoint(rotation: Double) {
    let page = visiblePage(rotation: rotation)
    let start = CGPoint(x: 100, y: 50)
    let local = page.localPoint(page.pagePoint(start, info: info), info: info)
    #expect(abs(local.x - start.x) < 0.001)
    #expect(abs(local.y - start.y) < 0.001)
  }

  @Test func localPointIsWhereLocalRectPutsThePoint() {
    let page = visiblePage(rotation: 90)
    let point = Point(x: 100, y: 200)
    expectNoDifference(
      page.localPoint(point, info: info),
      page.localRect(Rect(x: point.x, y: point.y, width: 0, height: 0), info: info).origin)
  }

  @Test func localPointOnAnUnsizedPageIsZero() {
    var unsized = visiblePage(rotation: 0)
    unsized.transform.viewWidth = 0
    expectNoDifference(unsized.localPoint(Point(x: 100, y: 200), info: info), .zero)
  }
}
