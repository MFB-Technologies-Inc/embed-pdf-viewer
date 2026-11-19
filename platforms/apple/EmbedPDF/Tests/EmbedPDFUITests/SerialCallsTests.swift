import CustomDump
@testable import EmbedPDFUI
import Testing

@MainActor
@Suite(.timeLimit(.minutes(1)))
struct SerialCallsTests {
  @Test func callsRunInTheOrderEnqueued() async {
    let calls = SerialCalls()
    let log = Log()
    // An earlier call suspends for longer, so calls that only shared the
    // main actor would finish in reverse.
    for (number, yields) in [(1, 8), (2, 4), (3, 0)] {
      calls.enqueue {
        for _ in 0..<yields { await Task.yield() }
        log.order.append(number)
      }
    }
    await calls.enqueue {}.value
    expectNoDifference(log.order, [1, 2, 3])
  }

  @Test func aCallReturnsItsResult() async {
    let calls = SerialCalls()
    calls.enqueue { await Task.yield() }
    let result = await calls.enqueue { 7 }.value
    expectNoDifference(result, 7)
  }
}

@MainActor
private final class Log {
  var order: [Int] = []
}
