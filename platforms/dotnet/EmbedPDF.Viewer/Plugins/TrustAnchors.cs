using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace EmbedPDF;

/// <summary>
/// Trust anchors the app supplies, DER: its organisation's root, say. The
/// package bundles none, so a viewer without anchors tops every verdict out
/// at <see cref="SignatureSummary.ValidUntrusted"/>.
/// </summary>
public sealed class TrustAnchors : ISignatureTrust
{
    /// <summary>Anchors from DER certificates, taken as given.</summary>
    public TrustAnchors(IEnumerable<ReadOnlyMemory<byte>> der)
    {
        ArgumentNullException.ThrowIfNull(der);
        Certificates = [.. der];
    }

    /// <summary>The anchor certificates, DER.</summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> Certificates { get; }

    /// <inheritdoc/>
    public IReadOnlyList<ReadOnlyMemory<byte>> GetAnchors() => Certificates;

    /// <summary>Anchors from PEM text, each string holding one or more <c>CERTIFICATE</c> blocks.</summary>
    /// <exception cref="CryptographicException">A string holds no certificate, or one that can't be read.</exception>
    public static TrustAnchors FromPem(IEnumerable<string> pem)
    {
        ArgumentNullException.ThrowIfNull(pem);
        return new TrustAnchors(Arguments.NoNullElements(pem, nameof(pem)).SelectMany(Pem).ToArray());
    }

    /// <summary>Anchors from certificates the app loaded.</summary>
    public static TrustAnchors FromCertificates(IEnumerable<X509Certificate2> certificates)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        return new TrustAnchors(Arguments.NoNullElements(certificates, nameof(certificates)).Select(c => (ReadOnlyMemory<byte>)c.RawData).ToArray());
    }

    /// <summary>
    /// Anchors from <paramref name="assembly"/>'s manifest resources whose
    /// names start with <paramref name="prefix"/> and end with <c>.pem</c>,
    /// <c>.crt</c>, <c>.cer</c> or <c>.der</c>, in name order. Each holds PEM
    /// text or one DER certificate.
    /// </summary>
    /// <exception cref="CryptographicException">A resource is not a readable certificate.</exception>
    public static TrustAnchors FromEmbeddedResources(Assembly assembly, string prefix)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(prefix);
        string[] extensions = [".pem", ".crt", ".cer", ".der"];
        var anchors = new List<ReadOnlyMemory<byte>>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal)
            && extensions.Any(e => n.EndsWith(e, StringComparison.OrdinalIgnoreCase))).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            if (bytes.Length > 0 && bytes[0] == 0x30) anchors.Add(Checked(bytes));
            else anchors.AddRange(Pem(Encoding.UTF8.GetString(bytes)));
        }
        return new TrustAnchors(anchors);
    }

    static IEnumerable<ReadOnlyMemory<byte>> Pem(string text)
    {
        var found = new List<ReadOnlyMemory<byte>>();
        var rest = text.AsMemory();
        while (PemEncoding.TryFind(rest.Span, out var fields))
        {
            if (rest.Span[fields.Label].SequenceEqual("CERTIFICATE"))
            {
                var der = new byte[fields.DecodedDataLength];
                if (!Convert.TryFromBase64Chars(rest.Span[fields.Base64Data], der, out var written))
                {
                    throw new CryptographicException("A PEM certificate block is not base64.");
                }
                found.Add(Checked(der.AsMemory(0, written).ToArray()));
            }
            rest = rest[fields.Location.End..];
        }
        if (found.Count == 0) throw new CryptographicException("The PEM text holds no readable certificate.");
        return found;
    }

    static ReadOnlyMemory<byte> Checked(byte[] der)
    {
        using var _ = X509CertificateLoader.LoadCertificate(der);
        return der;
    }
}
