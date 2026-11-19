import EmbedPDF
import SwiftUI

/// A search field over a `SearchModel`: the text field, previous and next
/// buttons, the model's `summary` and a clear button, with the model's
/// `error` below. Submitting the field runs `SearchModel.searchSubmitted()`.
/// Previous and next are disabled while the model has no hits. The clear
/// button empties the field and ends the session; it is disabled while the
/// field is empty, the model has no hits and its status is idle.
///
/// The bar runs the model's `task()`. An app does not also run `task()` for
/// a model it gives to a `SearchBar`: a second `task()` on one model
/// supersedes the first, so the app's own run stops refreshing the model
/// once the bar starts, and nothing refreshes it after the bar goes away.
///
/// `onActiveHit` is called whenever the bar observes the model's
/// `activeHit` change to a hit: the first hit of a new search, each previous
/// or next step, a move made through the `SearchCapability` or by the
/// plugin's own rerun after an edit, and the first value the bar loads after
/// it appears over a search that already exists. A query submitted again
/// whose first hit equals the hit active before it may or may not be
/// reported. It is not called when the hits clear. An app that scrolls to
/// the hit in this callback therefore scrolls once when the bar appears over
/// an existing search, can scroll to a hit that is already in view, and can
/// miss a scroll for a repeated query, so the scroll should be safe to
/// repeat and not the only way back to the active hit.
///
///     SearchBar(model: search) { hit in currentPage = hit.page }
@MainActor
public struct SearchBar: View {
  @Bindable private var model: SearchModel
  private let onActiveHit: (SearchHit) -> Void
  @Environment(\.dynamicTypeSize) private var dynamicTypeSize

  public init(model: SearchModel, onActiveHit: @escaping (SearchHit) -> Void = { _ in }) {
    self.model = model
    self.onActiveHit = onActiveHit
  }

  public var body: some View {
    let controls = dynamicTypeSize.isAccessibilitySize
      ? AnyLayout(VStackLayout(alignment: .leading, spacing: 8))
      : AnyLayout(HStackLayout())
    // Performance: this one body reads `text`, `activeHit`, `summary`,
    // `hitCount`, `canClear` and `error`, so each keystroke and each batch of hits a
    // running scan adds evaluates the whole bar again. That is one row of
    // five controls; the field and the navigation buttons in their own views
    // limit a keystroke to the field.
    VStack(alignment: .leading, spacing: 4) {
      controls {
        TextField("Search", text: $model.text)
          .textFieldStyle(.roundedBorder)
          .autocorrectionDisabled()
          .submitLabel(.search)
          .onSubmit { Task { await model.searchSubmitted() } }
          .accessibilityIdentifier("searchField")
        HStack {
          if let summary = model.summary {
            Text(summary).font(.caption).monospacedDigit().foregroundStyle(.secondary)
              .accessibilityIdentifier("searchSummary")
          }
          // A titled button with an icon-only style keeps the title as its
          // accessibility label.
          Button("Previous match", systemImage: "chevron.up") { Task { await model.previousButtonTapped() } }
            .disabled(model.hitCount == 0)
            .accessibilityIdentifier("searchPrevious")
          Button("Next match", systemImage: "chevron.down") { Task { await model.nextButtonTapped() } }
            .disabled(model.hitCount == 0)
            .accessibilityIdentifier("searchNext")
          Button("Clear search", systemImage: "xmark.circle.fill") {
            model.text = ""
            Task { await model.clearButtonTapped() }
          }
          .disabled(!model.canClear)
          .accessibilityIdentifier("searchClear")
        }
        .labelStyle(.iconOnly)
      }
      if let error = model.error {
        Text(error.detail).font(.caption).foregroundStyle(.red)
          .accessibilityIdentifier("searchError")
      }
    }
    // Performance: `activeHit` is compared by value on every body
    // evaluation, segments and snippet included. A hit holds a few segments,
    // so that is cheap; comparing `activeHitIndex` with a search generation
    // avoids it if hits grow heavy.
    .onChange(of: model.activeHit) { _, hit in
      if let hit { onActiveHit(hit) }
    }
    .task(id: ObjectIdentifier(model)) { await model.task() }
  }
}
