import CustomDump
import Testing

@testable import EmbedPDFUI

@Suite struct TextSelectionDragTests {
  @Test func theFirstChangeBeginsAndLaterChangesExtend() {
    var drag = TextSelectionDrag()
    expectNoDifference(drag.handle(.changed), .begin)
    expectNoDifference(drag.handle(.changed), .extend)
    expectNoDifference(drag.handle(.changed), .extend)
  }

  @Test func anEndEndsOnce() {
    var drag = TextSelectionDrag()
    _ = drag.handle(.changed)
    expectNoDifference(drag.handle(.ended), .end)
    // SwiftUI resets the gesture state after `onEnded`, which arrives here
    // as a cancel.
    expectNoDifference(drag.handle(.cancelled), nil)
    expectNoDifference(drag.handle(.disappeared), nil)
  }

  @Test func aCancelEndsAnOpenDrag() {
    var drag = TextSelectionDrag()
    _ = drag.handle(.changed)
    _ = drag.handle(.changed)
    expectNoDifference(drag.handle(.cancelled), .end)
    expectNoDifference(drag.handle(.ended), nil)
  }

  @Test func disappearingMidDragEndsIt() {
    var drag = TextSelectionDrag()
    _ = drag.handle(.changed)
    expectNoDifference(drag.handle(.disappeared), .end)
    expectNoDifference(drag.handle(.cancelled), nil)
  }

  @Test(arguments: [TextSelectionDrag.Event.ended, .cancelled, .disappeared])
  func nothingEndsWithNoDragOpen(event: TextSelectionDrag.Event) {
    var drag = TextSelectionDrag()
    expectNoDifference(drag.handle(event), nil)
  }

  @Test func aChangeAfterAnEndBeginsANewDrag() {
    var drag = TextSelectionDrag()
    _ = drag.handle(.changed)
    _ = drag.handle(.ended)
    expectNoDifference(drag.handle(.changed), .begin)
    expectNoDifference(drag.handle(.changed), .extend)
    expectNoDifference(drag.handle(.ended), .end)
  }
}
