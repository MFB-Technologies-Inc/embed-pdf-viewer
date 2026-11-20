import EmbedPDF
import SwiftUI

/// One toggle button per tool in `tools`, in that order, on for the active
/// tool. A button is disabled while its tool is neither active nor in
/// `ToolModel.usable`, and every button while `ToolModel.submitting`, which
/// lasts for an activation's hub call.
/// Tapping a tool activates it; tapping the active tool returns to the
/// default tool. The picker runs the model's `task()`.
///
///     ToolPicker(model: tools, tools: [.ink, .highlight]) { id in
///       Label(id.rawValue.capitalized, systemImage: "pencil")
///     }
@MainActor
public struct ToolPicker: View {
  let model: ToolModel
  let tools: [ToolID]
  let label: (ToolID) -> Label<Text, Image>
  @Environment(\.dynamicTypeSize) private var dynamicTypeSize

  public init(model: ToolModel, tools: [ToolID], label: @escaping (ToolID) -> Label<Text, Image>) {
    self.model = model
    self.tools = tools
    self.label = label
  }

  public var body: some View {
    let buttons = dynamicTypeSize.isAccessibilitySize
      ? AnyLayout(VStackLayout(alignment: .leading, spacing: 8))
      : AnyLayout(HStackLayout())
    VStack(alignment: .leading, spacing: 4) {
      buttons {
        ForEach(tools, id: \.self) { id in
          // A toggle reports its on state to assistive technology.
          //
          // Performance: every button reads `activeToolID`, `usable` and
          // `submitting` in this one body, so a change to any of them
          // evaluates all the buttons again. One activation does that up
          // to twice: when `submitting` is set, and when the hub answers
          // and `submitting` and `activeToolID` change together. The read
          // after it adds a third only if `tools` or `usable` changed. That
          // is small for a handful of tools; a row per tool in its own view
          // limits each change to the buttons it affects.
          Toggle(isOn: Binding(get: { model.isSelected(id) }, set: { _ in Task { await model.buttonTapped(id) } })) {
            label(id)
          }
          .toggleStyle(.button)
          .disabled(model.isDisabled(id))
          .accessibilityIdentifier("toolPicker-\(id.rawValue)")
        }
      }
      if let error = model.error {
        Text(error.detail).font(.caption).foregroundStyle(.red)
          .accessibilityIdentifier("toolPickerError")
      }
    }
    .task(id: ObjectIdentifier(model)) { await model.task() }
  }
}
