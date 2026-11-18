import Foundation
import Network

/// An HTTP/1.1 server on a loopback port, for the signature transport tests.
/// Each request goes to `handler` with its connection; the handler answers
/// through `Reply`, which can hold, stall or cut the reply on purpose.
final class LoopbackServer: @unchecked Sendable {
  struct Request: Sendable {
    let method: String
    let path: String
    let query: String?
    let headers: [(name: String, value: String)]
    let body: Data

    func header(_ name: String) -> [String] {
      headers.filter { $0.name.lowercased() == name.lowercased() }.map(\.value)
    }
  }

  /// One request's answer. Every write is in order on the connection.
  struct Reply: Sendable {
    let connection: NWConnection

    /// The status line and headers. `length` nil sends no Content-Length:
    /// the body then ends when the connection closes.
    func head(_ status: Int, length: Int?) {
      var text = "HTTP/1.1 \(status) Status\r\ncontent-type: application/octet-stream\r\n"
      text += "x-content-type-options: nosniff\r\nconnection: close\r\n"
      if let length { text += "content-length: \(length)\r\n" }
      text += "\r\n"
      write(Data(text.utf8))
    }

    func write(_ data: Data) {
      connection.send(content: data, completion: .contentProcessed { _ in })
    }

    /// Close the write side once everything queued is sent.
    func end() {
      connection.send(content: nil, contentContext: .finalMessage, isComplete: true, completion: .contentProcessed { _ in })
    }

    func whole(_ status: Int, _ body: Data) {
      head(status, length: body.count)
      write(body)
      end()
    }
  }

  let port: UInt16
  private let listener: NWListener
  private let queue = DispatchQueue(label: "loopback-server", attributes: .concurrent)
  private let state: State

  private final class State: @unchecked Sendable {
    let lock = NSLock()
    var seen: [Request] = []
    var open: [NWConnection] = []
  }

  init(handler: @escaping @Sendable (Request, Reply) -> Void) async throws {
    let parameters = NWParameters.tcp
    parameters.requiredLocalEndpoint = .hostPort(host: "127.0.0.1", port: .any)
    let listener = try NWListener(using: parameters)
    self.listener = listener
    let state = State()
    self.state = state
    let ready = AsyncStream<UInt16?>.makeStream()
    listener.stateUpdateHandler = { state in
      switch state {
      case .ready: ready.continuation.yield(listener.port?.rawValue)
      case .failed, .cancelled: ready.continuation.yield(nil)
      default: break
      }
    }
    listener.newConnectionHandler = { connection in
      state.lock.withLock { state.open.append(connection) }
      connection.start(queue: DispatchQueue(label: "loopback-connection"))
      LoopbackServer.read(connection, Data()) { request in
        state.lock.withLock { state.seen.append(request) }
        handler(request, Reply(connection: connection))
      }
    }
    listener.start(queue: queue)
    var port: UInt16?
    for await value in ready.stream {
      port = value
      break
    }
    guard let port else { throw URLError(.cannotConnectToHost) }
    self.port = port
  }

  var base: String { "http://127.0.0.1:\(port)" }

  var requests: [Request] { state.lock.withLock { state.seen } }

  func stop() {
    listener.cancel()
    for connection in state.lock.withLock({ state.open }) { connection.cancel() }
  }

  private static func read(_ connection: NWConnection, _ buffer: Data, _ done: @escaping @Sendable (Request) -> Void) {
    connection.receive(minimumIncompleteLength: 1, maximumLength: 65_536) { data, _, isComplete, error in
      var buffer = buffer
      if let data { buffer.append(data) }
      if let request = parse(buffer) {
        done(request)
        return
      }
      if isComplete || error != nil { return }
      read(connection, buffer, done)
    }
  }

  /// A whole request, or nil while its head or body is still arriving.
  private static func parse(_ buffer: Data) -> Request? {
    guard let end = buffer.range(of: Data("\r\n\r\n".utf8)) else { return nil }
    let head = String(decoding: buffer[buffer.startIndex..<end.lowerBound], as: UTF8.self)
    var lines = head.components(separatedBy: "\r\n")
    let start = lines.removeFirst().split(separator: " ")
    guard start.count >= 2 else { return nil }
    let headers = lines.compactMap { line -> (name: String, value: String)? in
      guard let colon = line.firstIndex(of: ":") else { return nil }
      return (String(line[..<colon]), line[line.index(after: colon)...].trimmingCharacters(in: .whitespaces))
    }
    let length = headers.first { $0.name.lowercased() == "content-length" }.flatMap { Int($0.value) } ?? 0
    let body = buffer[end.upperBound...]
    guard body.count >= length else { return nil }
    let target = String(start[1])
    let parts = target.split(separator: "?", maxSplits: 1).map(String.init)
    return Request(
      method: String(start[0]), path: parts[0], query: parts.count > 1 ? parts[1] : nil,
      headers: headers, body: Data(body.prefix(length)))
  }
}
