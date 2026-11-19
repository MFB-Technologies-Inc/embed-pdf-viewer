import CustomDump
import Testing

@testable import EmbedPDFUI

@Suite struct SearchHitRevealsTests {
  @Test func nothingWaitsBeforeAReport() {
    expectNoDifference(SearchHitReveals().waiting, nil)
  }

  @Test func aReportedHitWaitsUntilItsRevealFinishes() throws {
    var reveals = SearchHitReveals()
    reveals.hitReported()
    let request = try #require(reveals.waiting)
    reveals.finished(request)
    expectNoDifference(reveals.waiting, nil)
  }

  /// A view that reappears runs its task again with whatever waits, and
  /// reports no hit: nothing waits, so nothing is revealed.
  @Test func nothingWaitsWhenTheViewReappearsAfterAReveal() throws {
    var reveals = SearchHitReveals()
    reveals.hitReported()
    reveals.finished(try #require(reveals.waiting))
    expectNoDifference(reveals.waiting, nil)
  }

  @Test func eachReportIsItsOwnRequest() throws {
    var reveals = SearchHitReveals()
    reveals.hitReported()
    let first = try #require(reveals.waiting)
    reveals.hitReported()
    let second = try #require(reveals.waiting)
    #expect(first != second)
    reveals.finished(second)
    reveals.hitReported()
    #expect(reveals.waiting != nil)
    #expect(reveals.waiting != second)
  }

  @Test func anOlderRevealFinishingLeavesTheNewerWaiting() throws {
    var reveals = SearchHitReveals()
    reveals.hitReported()
    let first = try #require(reveals.waiting)
    reveals.hitReported()
    let second = try #require(reveals.waiting)
    reveals.finished(first)
    expectNoDifference(reveals.waiting, second)
  }
}
