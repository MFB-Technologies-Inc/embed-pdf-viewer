/// Runs main-actor calls one at a time, in the order they are enqueued. Each
/// call may suspend, and the next starts only after it returns.
@MainActor
final class SerialCalls {
  /// Finishes when every call enqueued so far has.
  private var last: Task<Void, Never>?

  /// Runs `work` after every earlier call. The call joins the chain before
  /// this returns, so a later `enqueue` always runs after it.
  @discardableResult
  func enqueue<R: Sendable>(_ work: @escaping @MainActor @Sendable () async -> R) -> Task<R, Never> {
    let previous = last
    let call = Task { @MainActor in
      await previous?.value
      return await work()
    }
    last = Task { _ = await call.value }
    return call
  }
}
