/// Which reveal a reader owes its search bar. A hit the bar reports is one
/// request, and it waits until its reveal finishes. A view keys a task on
/// ``waiting``, so the task that runs when the view appears again, with no
/// new report, finds nothing to reveal.
struct SearchHitReveals: Equatable {
  /// The newest report whose reveal has not finished, or `nil` for none.
  private(set) var waiting: Int?
  private var reports = 0

  /// The bar reported an active hit: its reveal now waits, in place of an
  /// earlier one still waiting.
  mutating func hitReported() {
    reports &+= 1
    waiting = reports
  }

  /// The reveal for `request` returned. A newer report keeps waiting.
  mutating func finished(_ request: Int) {
    if waiting == request { waiting = nil }
  }
}
