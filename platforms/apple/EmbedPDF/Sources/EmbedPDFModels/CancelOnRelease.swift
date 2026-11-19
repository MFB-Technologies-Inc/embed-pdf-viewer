/// Cancels its task when released. A cancelled task leaves its event loop,
/// which ends the loop's subscription.
final class CancelOnRelease: Sendable {
  let task: Task<Void, Never>

  init(_ task: Task<Void, Never>) { self.task = task }

  deinit { task.cancel() }
}
