using Microsoft.Extensions.Logging;
using GF = uniffi.epdf_ffi;
using GSg = uniffi.epdf_plugin_signature;

namespace EmbedPDF;

/// <summary>
/// A private key that never leaves the app, and its certificate chain.
/// <see cref="X509CertificateSigner"/> is the one this package ships.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GetCertificateChain"/>, <see cref="Algorithm"/> and
/// <see cref="Digest"/> run on the viewer's kernel thread, synchronously.
/// A <see cref="PdfException"/> thrown there fails the signing with its name
/// and code; another exception fails it with <c>operation-failed</c>. A throw
/// from <see cref="Algorithm"/> or <see cref="Digest"/> fails the signing
/// too, and <see cref="SignAsync"/> is not called for it.
/// Disposing the viewer there throws <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// <see cref="SignAsync"/> runs on the thread pool, and the viewer keeps
/// serving other calls while its task is pending, as a prompt waiting on the
/// user needs. A thrown <see cref="OperationCanceledException"/> fails the
/// signing with <c>operation-cancelled</c>, a <see cref="PdfException"/>
/// keeps its name and code, and the signing leaves nothing pending either
/// way. None of the members may block waiting for the viewer.
/// </para>
/// </remarks>
public interface ISignatureSigner
{
    /// <summary>The certificate chain, leaf first, each certificate DER.</summary>
    IReadOnlyList<ReadOnlyMemory<byte>> GetCertificateChain();

    /// <summary>The key's signature algorithm.</summary>
    SignatureKeyAlgorithm Algorithm { get; }

    /// <summary>The digest <see cref="SignAsync"/> hashes with.</summary>
    SignatureDigest Digest { get; }

    /// <summary>
    /// Signs <paramref name="message"/>, the DER of the CMS signed
    /// attributes, hashing it with <see cref="Digest"/>. Answers PKCS#1 v1.5
    /// or PSS bytes for RSA, and <c>r || s</c> (each padded to the curve's
    /// size) for ECDSA.
    /// </summary>
    Task<ReadOnlyMemory<byte>> SignAsync(ReadOnlyMemory<byte> message);
}

/// <summary>
/// The trust anchors validation uses. <see cref="TrustAnchors"/> is the one
/// this package ships.
/// </summary>
/// <remarks>
/// <see cref="GetAnchors"/> runs on the viewer's kernel thread,
/// synchronously; a throw is handled as <see cref="ISignatureSigner"/>'s
/// synchronous members' is. An empty list makes every verdict top out at
/// <see cref="SignatureSummary.ValidUntrusted"/>.
/// </remarks>
public interface ISignatureTrust
{
    /// <summary>The anchor certificates, DER.</summary>
    IReadOnlyList<ReadOnlyMemory<byte>> GetAnchors();
}

/// <summary>What one <see cref="ISignatureTransport.PostAsync"/> came to.</summary>
public abstract record SignatureTransportOutcome
{
    private protected SignatureTransportOutcome() { }

    /// <summary>A reply came, whatever its status.</summary>
    /// <param name="Status">The HTTP status.</param>
    /// <param name="Body">The reply's body.</param>
    public sealed record Replied(int Status, ReadOnlyMemory<byte> Body) : SignatureTransportOutcome;

    /// <summary>No whole reply within the request's timeout.</summary>
    public sealed record TimedOut : SignatureTransportOutcome;

    /// <summary>A 2xx reply ran past the request's reply limit.</summary>
    public sealed record TooLarge : SignatureTransportOutcome;

    /// <summary>No reply: the address, the connection or the reply failed.</summary>
    /// <param name="Message">What failed.</param>
    public sealed record Failed(string Message) : SignatureTransportOutcome;
}

/// <summary>
/// The viewer's HTTP stack for timestamps and revocation answers.
/// <see cref="HttpClientSignatureTransport"/> is the one this package ships.
/// </summary>
/// <remarks>
/// One call is one attempt; the engine keeps the protocol, retries and
/// what a status means. The transport enforces the request's
/// <see cref="SignatureTransportRequest.TimeoutMs"/> and
/// <see cref="SignatureTransportRequest.MaxReplyBytes"/>. <see cref="PostAsync"/>
/// runs on the thread pool; a throw or a <see langword="null"/> task answers
/// <see cref="SignatureTransportOutcome.Failed"/> with its message.
/// </remarks>
public interface ISignatureTransport
{
    /// <summary>Sends <paramref name="request"/> once.</summary>
    Task<SignatureTransportOutcome> PostAsync(SignatureTransportRequest request);
}

/// <summary>The signer behind the generated trait.</summary>
internal sealed class SignerAdapter(ISignatureSigner signer, KernelMark mark, ILogger logger) : GSg.SignatureSigner
{
    public byte[][] CertificateChain() => Synchronous(() =>
        (signer.GetCertificateChain() ?? throw new InvalidOperationException("The signer gave no certificate chain.")).Select(Bytes.Array).ToArray());

    // The two enum members have no error type, and a throw there would panic
    // the kernel thread. So a throw answers a placeholder and is remembered:
    // the next Sign fails with it, and the signer never signs with a key it
    // could not describe.
    Exception? described;

    public GSg.SignatureKeyAlgorithm Algorithm() => KernelCallback.Run(mark, logger, "ISignatureSigner.Algorithm",
        () => (GSg.SignatureKeyAlgorithm)(int)Ffi.Defined(signer.Algorithm, "key algorithm"), Remember(GSg.SignatureKeyAlgorithm.RsaPkcs1));

    public GSg.SignatureDigest Digest() => KernelCallback.Run(mark, logger, "ISignatureSigner.Digest",
        () => (GSg.SignatureDigest)(int)Ffi.Defined(signer.Digest, "digest"), Remember(GSg.SignatureDigest.Sha256));

    Func<Exception, T> Remember<T>(T placeholder) => error =>
    {
        Volatile.Write(ref described, error);
        return placeholder;
    };

    public void Sign(byte[] message, GSg.SignatureSignCompletion completion) => _ = Task.Run(async () =>
    {
        GF.HostFailure? failure = null;
        byte[]? signature = null;
        if (Interlocked.Exchange(ref described, null) is { } undescribed)
        {
            failure = FetcherAdapter.Failure(undescribed, cancelled: false);
        }
        else
        {
            try
            {
                var signing = signer.SignAsync(message) ?? throw new InvalidOperationException("The signer returned no task.");
                signature = Bytes.Array(await signing.ConfigureAwait(false));
            }
            catch (Exception error)
            {
                logger.LogDebug(error, "A signer failed.");
                failure = FetcherAdapter.Failure(error, cancelled: error is OperationCanceledException);
            }
        }
        try
        {
            if (failure is null) completion.Finish(signature!);
            else completion.FailWith(failure);
        }
        catch (Exception)
        {
            // The viewer shut down first; nobody waits for this answer.
        }
        finally
        {
            completion.Dispose();
        }
    });

    T Synchronous<T>(Func<T> body) => SignatureCallbacks.Kernel(mark, body);
}

/// <summary>The trust behind the generated trait.</summary>
internal sealed class TrustAdapter(ISignatureTrust trust, KernelMark mark) : GSg.SignatureTrust
{
    public byte[][] Anchors() => SignatureCallbacks.Kernel(mark, () =>
        (trust.GetAnchors() ?? throw new InvalidOperationException("The trust gave no anchors.")).Select(Bytes.Array).ToArray());
}

/// <summary>The transport behind the generated trait: answers the completion exactly once.</summary>
internal sealed class TransportAdapter(ISignatureTransport transport, ILogger logger) : GSg.SignatureTransport
{
    public void Post(GSg.SignatureTransportRequest request, GSg.SignatureTransportCompletion completion)
    {
        var publicRequest = Ffi.ToPublic(request);
        _ = Task.Run(async () =>
        {
            SignatureTransportOutcome outcome;
            try
            {
                var posting = transport.PostAsync(publicRequest) ?? throw new InvalidOperationException("The transport returned no task.");
                outcome = await posting.ConfigureAwait(false) ?? new SignatureTransportOutcome.Failed("The transport answered nothing.");
            }
            catch (Exception error)
            {
                logger.LogDebug(error, "A signature transport failed.");
                outcome = new SignatureTransportOutcome.Failed(error.Message);
            }
            try
            {
                switch (outcome)
                {
                    case SignatureTransportOutcome.Replied r when r.Status is >= 0 and <= ushort.MaxValue:
                        completion.Replied((ushort)r.Status, Bytes.Array(r.Body));
                        break;
                    case SignatureTransportOutcome.Replied r:
                        completion.Failed($"HTTP status {r.Status} is outside 0...{ushort.MaxValue}");
                        break;
                    case SignatureTransportOutcome.TimedOut:
                        completion.TimedOut();
                        break;
                    case SignatureTransportOutcome.TooLarge:
                        completion.TooLarge();
                        break;
                    case SignatureTransportOutcome.Failed f:
                        completion.Failed(f.Message ?? "The transport failed.");
                        break;
                    default:
                        completion.Failed("The transport answered an unknown outcome.");
                        break;
                }
            }
            catch (Exception)
            {
                // The viewer shut down first; nobody waits for this answer.
            }
            finally
            {
                completion.Dispose();
            }
        });
    }
}

internal static class SignatureCallbacks
{
    /// <summary>
    /// Runs a synchronous port member marked. A <see cref="PdfException"/>
    /// reaches the kernel with its name and code; anything else as
    /// <c>operation-failed</c>.
    /// </summary>
    internal static T Kernel<T>(KernelMark mark, Func<T> body)
    {
        using var _ = KernelCallback.Enter(mark);
        try
        {
            return body();
        }
        catch (PdfException error)
        {
            throw new GF.ViewerException.Kernel(error.Name, error.Code.Value, error.Detail);
        }
        catch (Exception error)
        {
            throw new GF.ViewerException.Kernel(error.GetType().Name, PdfErrorCode.OperationFailed.Value, error.Message);
        }
    }
}
