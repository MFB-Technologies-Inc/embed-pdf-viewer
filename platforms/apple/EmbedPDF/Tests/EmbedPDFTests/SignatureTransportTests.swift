import CustomDump
@testable import EmbedPDF
import Foundation
import Security
import Testing

/// Set by the test run (`TEST_RUNNER_EMBEDPDF_SIGNATURE_ROUTES` through
/// xcodebuild) to the address of `platforms/android/tests/signature-routes.mjs`:
/// upstream's `timestampRoute` and `revocationRoute` on a loopback port.
/// Without it the route tests are skipped.
private let routes = ProcessInfo.processInfo.environment["EMBEDPDF_SIGNATURE_ROUTES"]

extension BaseSuite {
  @Suite struct SignatureTransportTests {
    /// Every answer one exchange gave.
    final class Answers: @unchecked Sendable {
      private let lock = NSLock()
      private var got: [String] = []
      private var bodies: [Data] = []

      var answer: SignatureTransportAnswer {
        SignatureTransportAnswer { [self] outcome in
          lock.withLock {
            switch outcome {
            case .replied(let status, let body):
              got.append("replied \(status) \(body.count)")
              bodies.append(body)
            case .timedOut: got.append("timedOut")
            case .tooLarge: got.append("tooLarge")
            case .failed(let message): got.append("failed \(message)")
            }
          }
        }
      }

      var all: [String] { lock.withLock { got } }
      var body: Data? { lock.withLock { bodies.first } }

      /// The first answer, waiting up to ten seconds for it.
      func first() async throws -> String {
        for _ in 0..<1000 {
          if let first = all.first { return first }
          try await Task.sleep(for: .milliseconds(10))
        }
        Issue.record("no answer within 10 s")
        return ""
      }
    }

    static func bytes(_ n: Int) -> Data { Data((0..<n).map { UInt8($0 % 251) }) }

    static func request(
      _ url: String, body: Data = Data([0x30, 0x03, 0x02, 0x01, 0x01]), timeoutMs: UInt32 = 10_000,
      maxReplyBytes: UInt32 = 1024 * 1024, headers: [SignatureTransportHeader] = [],
      contentType: String = "application/timestamp-query"
    ) -> SignatureTransportRequest {
      SignatureTransportRequest(
        url: url, contentType: contentType, headers: headers, body: body, timeoutMs: timeoutMs,
        maxReplyBytes: maxReplyBytes)
    }

    static func server() async throws -> LoopbackServer {
      try await LoopbackServer { request, reply in
        let later = { (ms: Int, work: @escaping @Sendable () -> Void) in
          DispatchQueue.global().asyncAfter(deadline: .now() + .milliseconds(ms), execute: work)
        }
        switch request.path {
        case "/echo":
          let status = request.query.flatMap { Int($0.replacingOccurrences(of: "status=", with: "")) } ?? 200
          reply.whole(status, Data("ok:\(request.body.count)".utf8))
        case "/hold":
          later(1500) { reply.whole(200, Data([1, 2])) }
        case "/stall-body":
          reply.head(200, length: 20)
          reply.write(bytes(10))
          later(1500) {
            reply.write(bytes(10))
            reply.end()
          }
        case "/error-stall":
          reply.head(429, length: 20)
          reply.write(bytes(10))
          later(1500) {
            reply.write(bytes(10))
            reply.end()
          }
        case "/broken":
          reply.head(200, length: 100)
          reply.write(bytes(10))
          later(100) { reply.connection.cancel() }
        case "/error-broken":
          reply.head(503, length: 100)
          reply.write(bytes(10))
          later(100) { reply.connection.cancel() }
        case "/long-length":
          reply.whole(200, bytes(2000))
        case "/long-stream":
          reply.head(200, length: nil)
          for _ in 0..<4 { reply.write(bytes(500)) }
          reply.end()
        case "/exact":
          reply.head(200, length: nil)
          reply.write(bytes(1000))
          reply.end()
        case "/error-long":
          reply.head(503, length: nil)
          reply.write(bytes(2000))
          reply.end()
        case "/error-body":
          reply.whole(404, Data("nope".utf8))
        default:
          reply.whole(500, Data())
        }
      }
    }

    func send(_ request: SignatureTransportRequest) async throws -> (Answers, Duration) {
      let answers = Answers()
      let clock = ContinuousClock()
      let started = clock.now
      URLSessionSignatureTransport().send(request, answer: answers.answer)
      _ = try await answers.first()
      return (answers, clock.now - started)
    }

    /// One POST of the body, `content-type` then the headers (a later one
    /// wins), and any status is `replied` with its body.
    @Test func aReplyOfAnyStatusIsReplied() async throws {
      let server = try await Self.server()
      defer { server.stop() }
      let body = Self.bytes(300)
      let headers = [SignatureTransportHeader(name: "x-key", value: "k"), SignatureTransportHeader(name: "x-session", value: "s")]
      var (answers, _) = try await send(Self.request("\(server.base)/echo?status=201", body: body, headers: headers))
      expectNoDifference(answers.all, ["replied 201 6"])
      expectNoDifference(answers.body.map { String(decoding: $0, as: UTF8.self) }, "ok:300")
      let sent = try #require(server.requests.last)
      expectNoDifference(sent.method, "POST")
      expectNoDifference(sent.body, body)
      expectNoDifference(sent.header("content-type"), ["application/timestamp-query"])
      expectNoDifference(sent.header("x-key"), ["k"])
      expectNoDifference(sent.header("x-session"), ["s"])

      // A request header named content-type is sent after it, and wins.
      _ = try await send(
        Self.request(server.base + "/echo", headers: [SignatureTransportHeader(name: "Content-Type", value: "application/json")]))
      expectNoDifference(server.requests.last?.header("content-type"), ["application/json"])

      (answers, _) = try await send(Self.request(server.base + "/error-body"))
      expectNoDifference(answers.all, ["replied 404 4"])
      expectNoDifference(answers.body.map { String(decoding: $0, as: UTF8.self) }, "nope")
    }

    /// The deadline covers waiting for the reply and reading it; a status
    /// outside 2xx in hand at the deadline is that status with no body.
    /// Each exchange answers exactly once, also after the held replies
    /// finish arriving.
    @Test func theDeadlineCoversTheWholeExchangeAndAnswersOnce() async throws {
      let server = try await Self.server()
      defer { server.stop() }
      let (held, heldTook) = try await send(Self.request(server.base + "/hold", timeoutMs: 300))
      expectNoDifference(held.all, ["timedOut"])
      #expect(heldTook >= .milliseconds(250) && heldTook <= .milliseconds(1400), "held for \(heldTook)")
      let (stalled, stalledTook) = try await send(Self.request(server.base + "/stall-body", timeoutMs: 300))
      expectNoDifference(stalled.all, ["timedOut"])
      #expect(stalledTook >= .milliseconds(250) && stalledTook <= .milliseconds(1400), "stalled for \(stalledTook)")
      let (errorStall, _) = try await send(Self.request(server.base + "/error-stall", timeoutMs: 300))
      expectNoDifference(errorStall.all, ["replied 429 0"])

      // The held replies have finished arriving; nothing more was answered.
      try await Task.sleep(for: .milliseconds(1700))
      for answers in [held, stalled, errorStall] {
        expectNoDifference(answers.all.count, 1)
      }
    }

    /// `tooLarge` by `content-length` or once more than `maxReplyBytes` is
    /// read; exactly the limit is a reply; a long or broken body outside
    /// 2xx is its status with no body, and a broken 2xx body is `failed`.
    @Test func maxReplyBytesIsEnforced() async throws {
      let server = try await Self.server()
      defer { server.stop() }
      var (answers, _) = try await send(Self.request(server.base + "/long-length", maxReplyBytes: 1000))
      expectNoDifference(answers.all, ["tooLarge"])
      (answers, _) = try await send(Self.request(server.base + "/long-stream", maxReplyBytes: 1000))
      expectNoDifference(answers.all, ["tooLarge"])
      (answers, _) = try await send(Self.request(server.base + "/exact", maxReplyBytes: 1000))
      expectNoDifference(answers.all, ["replied 200 1000"])
      expectNoDifference(answers.body, Self.bytes(1000))
      (answers, _) = try await send(Self.request(server.base + "/error-long", maxReplyBytes: 1000))
      expectNoDifference(answers.all, ["replied 503 0"])
      (answers, _) = try await send(Self.request(server.base + "/error-broken", maxReplyBytes: 1000))
      expectNoDifference(answers.all, ["replied 503 0"])
      // A 2xx body that breaks off is no answer.
      (answers, _) = try await send(Self.request(server.base + "/broken", maxReplyBytes: 1000))
      #expect(answers.all.count == 1 && answers.all[0].hasPrefix("failed "), "\(answers.all)")
    }

    @Test func nothingListeningOrNoAddressIsFailed() async throws {
      let closed = try await Self.server()
      closed.stop()
      var (answers, _) = try await send(Self.request("http://127.0.0.1:\(closed.port)/api/timestamp"))
      #expect(answers.all.count == 1 && answers.all[0].hasPrefix("failed "), "\(answers.all)")
      (answers, _) = try await send(Self.request("not a url"))
      #expect(answers.all.count == 1 && answers.all[0].hasPrefix("failed "), "\(answers.all)")
    }

    /// Through the generated completion, as the viewer calls it.
    @Test func postAnswersThroughTheGeneratedCompletion() async throws {
      let server = try await Self.server()
      defer { server.stop() }
      let viewer = try PDFViewer(
        ViewerConfig(interaction: true, annotation: "", form: FormConfig(), signature: SignatureConfig(
          timestampAuthority: SignatureTimestampAuthority(url: server.base + "/error-body", retries: 0))))
      try await viewer.start()
      viewer.setSigner(try KeychainSignerTests.testSigner())
      viewer.setSignatureTransport(URLSessionSignatureTransport())
      let document = try await viewer.open(Fixture.data("unsigned_sigfield"))
      let signatures = try await viewer.signatures(for: document)
      _ = try await signatures.refresh()
      let field = try #require(try await signatures.unsignedFields().first).field
      // The authority answers 404: the Rust client refuses the timestamp,
      // so the signing fails, after one POST that reached the server.
      await #expect(throws: PDFError.self) {
        _ = try await signatures.sign(SignatureSignInput(field: field, mark: .source(bytes: try Fixture.data("signature_artwork"))))
      }
      expectNoDifference(server.requests.filter { $0.path == "/error-body" }.count, 1)
      try await viewer.destroy()
    }

    // MARK: - Upstream's routes

    struct Material {
      let chain: [Data]
      let key: Data
      let anchors: [Data]
      let timestampCertificate: Data

      static func load(_ routes: String) async throws -> Material {
        let raw = try await get(routes + "/material")
        let json = try #require(try JSONSerialization.jsonObject(with: raw) as? [String: Any])
        func data(_ value: Any?) throws -> Data {
          let text = try #require(value as? String)
          return try #require(Data(base64Encoded: text))
        }
        return Material(
          chain: try #require(json["chain"] as? [Any]).map(data), key: try data(json["key"]),
          anchors: try #require(json["anchors"] as? [Any]).map(data),
          timestampCertificate: try data(json["timestampCertificate"]))
      }

      func signer() throws -> KeychainSigner {
        let pem = "-----BEGIN PRIVATE KEY-----\n\(key.base64EncodedString(options: .lineLength64Characters))\n-----END PRIVATE KEY-----"
        let certificates = try chain.map { try #require(SecCertificateCreateWithData(nil, $0 as CFData)) }
        return try KeychainSigner(privateKey: try KeychainSigner.privateKey(pkcs8PEM: pem), certificates: certificates)
      }
    }

    static func get(_ url: String) async throws -> Data {
      try await URLSession.shared.data(from: try #require(URL(string: url))).0
    }

    static func log(_ routes: String) async throws -> String {
      String(decoding: try await get(routes + "/log"), as: UTF8.self)
    }

    /// The nonce length of every timestamp query the helper has seen.
    static func nonces(_ routes: String) async throws -> [Int] {
      let json = try JSONSerialization.jsonObject(with: try await get(routes + "/log")) as? [String: Any]
      return try #require(json?["nonces"] as? [Int])
    }

    static func viewer(_ config: SignatureConfig, _ material: Material) async throws -> PDFViewer {
      let viewer = try PDFViewer(ViewerConfig(interaction: true, annotation: "", form: FormConfig(), signature: config))
      try await viewer.start()
      viewer.setSigner(try material.signer())
      viewer.setTrust(TrustAnchors(der: material.anchors))
      viewer.setSignatureTransport(URLSessionSignatureTransport())
      return viewer
    }

    static func unsigned(_ viewer: PDFViewer) async throws -> (SignatureCapability, FormFieldRef) {
      let document = try await viewer.open(Fixture.data("unsigned_sigfield"))
      let signatures = try await viewer.signatures(for: document)
      _ = try await signatures.refresh()
      return (signatures, try #require(try await signatures.unsignedFields().first).field)
    }

    static func mark() throws -> SignatureMark { .source(bytes: try Fixture.data("signature_artwork")) }

    /// A granted timestamp asked per call (`SignatureCallAuthority.http`)
    /// through `timestampRoute`, with no authority in the settings.
    @Test(.enabled(if: routes != nil)) func aGrantedTimestampThroughTheTimestampRoute() async throws {
      let routes = try #require(routes)
      let material = try await Material.load(routes)
      let authority = SignatureTimestampAuthority(
        url: routes + "/api/timestamp", headers: [SignatureTransportHeader(name: "x-session", value: "s")])
      let viewer = try await Self.viewer(SignatureConfig(), material)
      let (signatures, field) = try await Self.unsigned(viewer)
      let before = try await Self.nonces(routes)
      #expect(try await !signatures.settings().hasTimestampAuthority)
      let done = try await signatures.sign(
        SignatureSignInput(field: field, mark: try Self.mark(), timestampAuthority: .http(authority: authority)))
      #expect(done.signature.signed)
      let verdict = try await signatures.validate(field)
      let stamp = try #require(verdict.timestamp)
      expectNoDifference(stamp.cryptography, .valid)
      expectNoDifference(stamp.authorityName, "EmbedPDF test timestamp authority")
      expectNoDifference(stamp.authorityCertificate, material.timestampCertificate)
      expectNoDifference(verdict.summary, .valid)
      // One query, with an 8-octet nonce, and the token answered it.
      let after = try await Self.nonces(routes)
      expectNoDifference(Array(after.dropFirst(before.count)), [8])
      try await viewer.destroy()
    }

    /// A long-term fill from the settings' authority and route: `sign`
    /// seals, then fills; the settings patch; `addValidationInfo` asked
    /// directly.
    @Test(.enabled(if: routes != nil)) func aLongTermFillThroughTheRevocationRoute() async throws {
      let routes = try #require(routes)
      let material = try await Material.load(routes)
      let authority = SignatureTimestampAuthority(url: routes + "/api/timestamp")
      let revocation = SignatureRevocationRoute(url: routes + "/api/revocation")
      let viewer = try await Self.viewer(SignatureConfig(timestampAuthority: authority, revocation: revocation), material)
      let (signatures, field) = try await Self.unsigned(viewer)
      let settings = try await signatures.settings()
      #expect(settings.hasTimestampAuthority && settings.hasRevocation && !settings.documentTimestamp, "\(settings)")
      #expect(try await signatures.canAddValidationInfo())
      let done = try await signatures.sign(SignatureSignInput(field: field, mark: try Self.mark()))
      expectNoDifference(done.validationInfoError, nil)
      let added = try #require(done.validationInfo)
      #expect(added.added.ocspResponses > 0 && added.documentTimestamp == nil, "\(added)")
      let log = try await Self.log(routes)
      #expect(log.contains("POST http://test-authority.example/ocsp"), "\(log)")
      let verdicts = try await signatures.validate(SignatureValidateRequest(validationTime: .earliestTimestamp))
      let verdict = try #require(verdicts.first)
      expectNoDifference(verdicts.count, 1)
      expectNoDifference(verdict.summary, .valid)
      expectNoDifference(verdict.revocation, .good)
      #expect(verdict.longTerm.enabled && verdict.longTerm.missing.isEmpty && verdict.longTerm.documentTimestampedAt == nil,
        "\(verdict.longTerm)")

      // Without the route, addValidationInfo refuses.
      try await signatures.updateSettings(SignatureSettingsPatch(clearRevocation: true))
      #expect(try await !signatures.settings().hasRevocation)
      #expect(try await !signatures.canAddValidationInfo())
      await #expect {
        _ = try await signatures.addValidationInfo(field)
      } throws: { ($0 as? PDFError)?.code == .invalidInput }
      // With it back, everything is already in the document: nothing more is added.
      try await signatures.updateSettings(SignatureSettingsPatch(revocation: revocation))
      #expect(try await signatures.canAddValidationInfo())
      let again = try await signatures.addValidationInfo(field)
      expectNoDifference(again.added, SignatureValidationInfoCounts(certificates: 0, ocspResponses: 0, crls: 0))
      try await viewer.destroy()
    }

    /// `SignatureCallAuthority.without`: no timestamp although the settings
    /// name an authority, and no query sent.
    @Test(.enabled(if: routes != nil)) func aCallWithoutAnAuthoritySendsNoQuery() async throws {
      let routes = try #require(routes)
      let material = try await Material.load(routes)
      let authority = SignatureTimestampAuthority(url: routes + "/api/timestamp")
      let viewer = try await Self.viewer(SignatureConfig(timestampAuthority: authority), material)
      let (signatures, field) = try await Self.unsigned(viewer)
      let queries = { (log: String) in
        log.firstRange(of: /"timestampQueries":(\d+)/).map { String(log[$0]) }
      }
      let before = queries(try await Self.log(routes))
      _ = try await signatures.sign(SignatureSignInput(field: field, mark: try Self.mark(), timestampAuthority: .without))
      let verdict = try await signatures.validate(field, SignatureValidateRequest(validationTime: .now))
      expectNoDifference(verdict.timestamp, nil)
      let after = queries(try await Self.log(routes))
      expectNoDifference(after, before)
      try await viewer.destroy()
    }

    // MARK: - The other catch-up surfaces

    final class Platform: StandardCommandsPlatform, @unchecked Sendable {
      private let lock = NSLock()
      private var files: [(bytes: Data, name: String, type: String)] = []
      var saved: [(bytes: Data, name: String, type: String)] { lock.withLock { files } }

      func saveFile(bytes: Data, fileName: String, contentType: String) {
        lock.withLock { files.append((bytes, fileName, contentType)) }
      }
      func copyText(text: String, completion: StandardCommandCompletion) { completion.finish() }
      func copyImage(png: Data, completion: StandardCommandCompletion) { completion.finish() }
    }

    final class Permission: ToolPermission {
      let allowed: Bool
      init(_ allowed: Bool) { self.allowed = allowed }
      func canUse() -> Bool { allowed }
    }

    @Test func theStandardCommandsJoinInUpstreamsOrderAndReachThePlatform() async throws {
      // Named out of order: the join keeps upstream's.
      let viewer = try PDFViewer(
        ViewerConfigBuilder().interaction().history().selection().annotation(settingsJSON: "").commands()
          .standardCommands([.document, .history])
      )
      try await viewer.start()
      let platform = Platform()
      viewer.setCommandsPlatform(platform)
      let document = try await viewer.open(Fixture.data("unsigned_sigfield"))
      let commands = try await viewer.commands()
      let ids = try await commands.commandIds()
      let undo = try #require(ids.firstIndex(of: "history:undo"))
      let download = try #require(ids.firstIndex(of: "document:download"))
      #expect(undo < download, "\(ids)")
      let executed = try await commands.execute("document:download", document: document)
      expectNoDifference(executed, .executed)
      let saved = platform.saved
      expectNoDifference(saved.count, 1)
      expectNoDifference(saved.first.map { String(decoding: $0.bytes.prefix(4), as: UTF8.self) }, "%PDF")
      expectNoDifference(saved.first?.type, "application/pdf")
      try await viewer.destroy()
    }

    @Test func aToolPermissionAnswersCanUseTool() async throws {
      let viewer = try PDFViewer(ViewerConfig(interaction: true))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data("report"))
      let interaction = try await viewer.interaction(for: document)
      _ = try await interaction.registerTool(
        InteractionTool(id: "swift-refused", cursor: "crosshair", enables: []), permission: Permission(false))
      _ = try await interaction.registerTool(
        InteractionTool(id: "swift-allowed", cursor: "crosshair", enables: []), permission: Permission(true))
      _ = try await interaction.registerTool(InteractionTool(id: "swift-open", cursor: "crosshair", enables: []))
      #expect(try await !interaction.canUseTool(ToolID(rawValue: "swift-refused")))
      #expect(try await interaction.canUseTool(ToolID(rawValue: "swift-allowed")))
      #expect(try await interaction.canUseTool(ToolID(rawValue: "swift-open")))
      #expect(try await !interaction.canUseTool(ToolID(rawValue: "nobody")))
      try await viewer.destroy()
    }

    @Test func noCreationAwaitsAClickWithNoToolArmed() async throws {
      let viewer = try PDFViewer(ViewerConfig(interaction: true, annotation: ""))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data("report"))
      let annotations = try await viewer.annotations(for: document)
      #expect(try await !annotations.drafts.hasCreationAwaitingClick())
      try await viewer.destroy()
    }

    @Test func stampImportOptionsIdNamesTheLibrary() async throws {
      let viewer = try PDFViewer(ViewerConfig(interaction: true, annotation: "", stamp: StampConfig()))
      try await viewer.start()
      let stamps = try await viewer.stamps()
      let library = try await stamps.importLibrary(Fixture.data("stamps"), options: StampImportOptions(id: "swift-library"))
      expectNoDifference(library, "swift-library")
      try await viewer.destroy()
    }

    @Test func theSignatureSettingsCarryTheNewFields() async throws {
      let viewer = try PDFViewer(ViewerConfig(interaction: true, annotation: "", form: FormConfig(), signature: SignatureConfig(
        timestampAuthority: SignatureTimestampAuthority(url: "https://tsa.example/"), documentTimestamp: true)))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data("unsigned_sigfield"))
      let signatures = try await viewer.signatures(for: document)
      var settings = try await signatures.settings()
      #expect(settings.hasTimestampAuthority && !settings.hasRevocation && settings.documentTimestamp, "\(settings)")
      try await signatures.updateSettings(SignatureSettingsPatch(clearTimestampAuthority: true, documentTimestamp: false))
      settings = try await signatures.settings()
      #expect(!settings.hasTimestampAuthority && !settings.documentTimestamp, "\(settings)")
      // No route and no anchors: nothing to fill with.
      #expect(try await !signatures.canAddValidationInfo())
      try await viewer.destroy()
    }
  }
}
