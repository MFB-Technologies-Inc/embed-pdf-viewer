/// One selection drag's begin, extensions and single end, whichever way
/// SwiftUI reports the drag. `TextSelectionOverlay` hands it every gesture
/// event and does what it answers, so a drag begins once and ends exactly
/// once however the events arrive.
struct TextSelectionDrag: Equatable {
  /// What SwiftUI reported.
  enum Event: Equatable, Sendable {
    /// The drag has a new point.
    case changed
    /// The gesture's `onEnded` ran.
    case ended
    /// The gesture state reset: SwiftUI ended or cancelled the gesture.
    case cancelled
    /// The overlay left the hierarchy.
    case disappeared
  }

  /// What the overlay does with the model.
  enum Command: Equatable, Sendable {
    /// Begin a selection at the event's point.
    case begin
    /// Extend the selection to the event's point.
    case extend
    /// End the selection.
    case end
  }

  /// Whether a drag has begun and not yet ended.
  private(set) var isOpen = false

  /// The command `event` calls for, or `nil` for none: an end, a cancel or
  /// a disappearance with no drag open.
  mutating func handle(_ event: Event) -> Command? {
    switch event {
    case .changed:
      if isOpen { return .extend }
      isOpen = true
      return .begin
    case .ended, .cancelled, .disappeared:
      guard isOpen else { return nil }
      isOpen = false
      return .end
    }
  }
}
