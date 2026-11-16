import Foundation
import Testing

extension BaseSuite {
  /// `platforms/apple/README.md` says the package exports `EmbedPDF` and
  /// `EmbedPDFUI` and nothing else, so the generated `EmbedPDFBindings` layer
  /// stays internal. This fails if a `.library` product is added to either
  /// manifest, so the README and the manifests change together.
  @Suite struct ProductsTests {
    static let expected = ["EmbedPDF", "EmbedPDFUI"]

    /// This package's directory: Tests/EmbedPDFTests/<file> is three levels down.
    static func manifestURL(up extra: Int) -> URL {
      var directory = URL(fileURLWithPath: #filePath)
      for _ in 0..<(3 + extra) { directory.deleteLastPathComponent() }
      return directory.appending(path: "Package.swift")
    }

    static func manifest(up extra: Int) throws -> String {
      try String(contentsOf: manifestURL(up: extra), encoding: .utf8)
    }

    /// False when the package is tested outside the repository, as the engine publish
    /// stages it for macOS (scripts/publish-engine-payload.mjs, stageMacConsumer).
    static let insideTheRepository = FileManager.default.fileExists(
      atPath: manifestURL(up: 3).path(percentEncoded: false))

    static func products(in manifest: String) -> [String] {
      manifest.components(separatedBy: ".library(").dropFirst().compactMap { declaration in
        declaration.components(separatedBy: "\"").dropFirst().first
      }
    }

    @Test func thePackageManifestExportsOnlyTheFacadeAndItsUI() throws {
      #expect(Self.products(in: try Self.manifest(up: 0)) == Self.expected)
    }

    @Test(.enabled(if: insideTheRepository, "the package is staged outside the repository"))
    func theRepositoryRootManifestExportsOnlyTheFacadeAndItsUI() throws {
      // platforms/apple/EmbedPDF -> repository root is three more levels up.
      #expect(Self.products(in: try Self.manifest(up: 3)) == Self.expected)
    }
  }
}
