public import EmbedPDFBindings
public import Foundation
import os

/// The viewer's signature transport on `URLSession`: the one POST a
/// timestamp authority or a revocation route sends. Set it with
/// `PDFViewer.setSignatureTransport(URLSessionSignatureTransport())`.
///
/// One `post` is one attempt. The Rust clients keep everything around it:
/// the RFC 3161 query, the route's JSON, retries and their delay, what a
/// status means and the error texts. So every status answers `replied`.
///
/// The transport enforces the request's limits, because the client has no
/// timer of its own:
/// - `timeoutMs` covers the whole exchange: connecting, sending and reading
///   the reply. Past it the answer is `timedOut` and the task is cancelled.
/// - `maxReplyBytes`: a 2xx reply whose `Content-Length` or body runs past
///   it is `tooLarge`, and reading stops there.
/// - A reply outside 200 to 299 whose body runs past the limit, breaks off
///   or is still arriving at the deadline is `replied` with its status and
///   no body, so the client decides by the status, as upstream's do.
///
/// The completion is answered exactly once, whichever of the reply, the
/// deadline or a failure comes first.
///
/// Supply a session to customize cookies, authentication or the proxy. Its
/// delegate still sees the task, but the transport reads the reply through a
/// delegate of its own on each task. On iOS, a cleartext `http://` address
/// also needs the app's App Transport Security settings to allow it.
public final class URLSessionSignatureTransport: SignatureTransport, Sendable {
  private let session: URLSession

  public init(session: URLSession = .shared) {
    self.session = session
  }

  public func post(request: SignatureTransportRequest, completion: SignatureTransportCompletion) {
    send(request, answer: SignatureTransportAnswer(completion))
  }

  /// `post` against any answer, so a test can count them.
  func send(_ request: SignatureTransportRequest, answer: SignatureTransportAnswer) {
    guard let url = URL(string: request.url), let scheme = url.scheme?.lowercased(),
      scheme == "http" || scheme == "https"
    else {
      answer.failed("the transport can't post to \(request.url): not an http or https address")
      return
    }
    var urlRequest = URLRequest(url: url)
    urlRequest.httpMethod = "POST"
    urlRequest.cachePolicy = .reloadIgnoringLocalAndRemoteCacheData
    // The task's own idle timer, as a backstop; the deadline below covers
    // the whole exchange.
    urlRequest.timeoutInterval = max(Double(request.timeoutMs) / 1000, 0.001)
    // content-type first, then the request's headers: a later one wins.
    urlRequest.setValue(request.contentType, forHTTPHeaderField: "content-type")
    for header in request.headers {
      urlRequest.setValue(header.value, forHTTPHeaderField: header.name)
    }
    urlRequest.httpBody = request.body

    let task = session.dataTask(with: urlRequest)
    let exchange = Exchange(task: task, limit: Int(request.maxReplyBytes), answer: answer)
    task.delegate = exchange
    // After an answer the deadline does nothing: only the first answer counts.
    DispatchQueue.global().asyncAfter(deadline: .now() + .milliseconds(Int(request.timeoutMs))) {
      exchange.expire()
    }
    task.resume()
  }
}

/// The four answers of one exchange, kept to the first. Wraps the generated
/// completion; tests count the calls through `init(_:)`'s closures.
final class SignatureTransportAnswer: Sendable {
  private let answered = OSAllocatedUnfairLock(initialState: false)
  private let deliver: @Sendable (Outcome) -> Void

  enum Outcome: Sendable, Equatable {
    case replied(UInt16, Data)
    case timedOut
    case tooLarge
    case failed(String)
  }

  init(_ completion: SignatureTransportCompletion) {
    deliver = { outcome in
      switch outcome {
      case .replied(let status, let body): completion.replied(status: status, body: body)
      case .timedOut: completion.timedOut()
      case .tooLarge: completion.tooLarge()
      case .failed(let message): completion.failed(message: message)
      }
    }
  }

  init(_ deliver: @escaping @Sendable (Outcome) -> Void) {
    self.deliver = deliver
  }

  /// `true` when this was the first answer and was delivered.
  @discardableResult
  func settle(_ outcome: Outcome) -> Bool {
    let first = answered.withLock { done in
      if done { return false }
      done = true
      return true
    }
    if first { deliver(outcome) }
    return first
  }

  var isSettled: Bool { answered.withLock { $0 } }

  func failed(_ message: String) { settle(.failed(message)) }
}

/// One POST's reply, read through the task's delegate.
private final class Exchange: NSObject, URLSessionDataDelegate, @unchecked Sendable {
  private let task: URLSessionDataTask
  private let limit: Int
  private let answer: SignatureTransportAnswer
  private let lock = NSLock()
  // Guarded by `lock`.
  private var status: Int?
  private var body = Data()
  /// The reply's `Content-Length` when its body arrives as sent (no
  /// content coding), to tell a body that broke off from a whole one.
  private var expected: Int?

  init(task: URLSessionDataTask, limit: Int, answer: SignatureTransportAnswer) {
    self.task = task
    self.limit = limit
    self.answer = answer
  }

  /// Answer once, then stop the task.
  private func finish(_ outcome: SignatureTransportAnswer.Outcome) {
    guard answer.settle(outcome) else { return }
    task.cancel()
  }

  private static func ok(_ status: Int) -> Bool { (200...299).contains(status) }

  /// A status outside 2xx whose body can't be read is that status with no body.
  private func unreadable(_ status: Int) {
    finish(.replied(UInt16(clamping: status), Data()))
  }

  /// The deadline: a status outside 2xx already in hand is that reply.
  func expire() {
    let code = lock.withLock { status }
    if let code, !Self.ok(code) {
      unreadable(code)
    } else {
      finish(.timedOut)
    }
  }

  func urlSession(
    _ session: URLSession, dataTask: URLSessionDataTask, didReceive response: URLResponse,
    completionHandler: @escaping @Sendable (URLSession.ResponseDisposition) -> Void
  ) {
    guard let http = response as? HTTPURLResponse else {
      finish(.failed("the reply is not HTTP"))
      completionHandler(.cancel)
      return
    }
    let code = http.statusCode
    let coding = http.value(forHTTPHeaderField: "content-encoding")?.lowercased()
    let length = coding == nil || coding == "identity" ? response.expectedContentLength : -1
    lock.withLock {
      status = code
      expected = length >= 0 ? Int(length) : nil
    }
    if response.expectedContentLength > Int64(limit) {
      if Self.ok(code) { finish(.tooLarge) } else { unreadable(code) }
      completionHandler(.cancel)
      return
    }
    completionHandler(answer.isSettled ? .cancel : .allow)
  }

  func urlSession(_ session: URLSession, dataTask: URLSessionDataTask, didReceive data: Data) {
    let (code, over) = lock.withLock { () -> (Int, Bool) in
      body.append(data)
      return (status ?? 200, body.count > limit)
    }
    guard over else { return }
    if Self.ok(code) { finish(.tooLarge) } else { unreadable(code) }
  }

  func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: (any Error)?) {
    let (code, reply, expected) = lock.withLock { (status, body, expected) }
    // A connection closed before the declared length can end the task
    // without an error.
    let error = error ?? (expected.map { reply.count < $0 } == true ? URLError(.networkConnectionLost) : nil)
    if let error {
      if let code, !Self.ok(code) {
        // The status came and the body broke off: the status decides.
        unreadable(code)
        return
      }
      let nsError = error as NSError
      if nsError.domain == NSURLErrorDomain && nsError.code == NSURLErrorTimedOut {
        finish(.timedOut)
      } else {
        finish(.failed("\(nsError.domain) \(nsError.code): \(nsError.localizedDescription)"))
      }
      return
    }
    guard let code else {
      finish(.failed("the request finished without a reply"))
      return
    }
    finish(.replied(UInt16(clamping: code), reply))
  }
}
