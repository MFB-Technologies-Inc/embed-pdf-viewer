import EmbedPDF
import EmbedPDFModels
import Testing

/// The models build for a module that imports no UI target: this file
/// imports only `EmbedPDF` and `EmbedPDFModels`.
@Suite struct ImportTests {
  @Test func theModelsNeedNoUIImport() {
    let model: TextSelectionModel.Type = TextSelectionModel.self
    #expect(String(reflecting: model) == "EmbedPDFModels.TextSelectionModel")
  }
}
