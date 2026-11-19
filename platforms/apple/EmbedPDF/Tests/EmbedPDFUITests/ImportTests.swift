import EmbedPDFUI
import Testing

/// `EmbedPDFUI` re-exports the models, so a file that imports only it still
/// names them.
@Suite struct ImportTests {
  @Test func theUIImportNamesTheModels() {
    let model: StageReaderModel.Type = StageReaderModel.self
    #expect(String(reflecting: model) == "EmbedPDFModels.StageReaderModel")
  }
}
