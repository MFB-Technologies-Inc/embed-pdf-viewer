using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EmbedPDF;

/// <summary>
/// An <see cref="ISignatureSigner"/> over an <see cref="X509Certificate2"/>
/// with an RSA or ECDSA private key, and its certificate chain. Build one
/// from a certificate the app holds, a PKCS#12 bundle or a certificate store.
/// </summary>
/// <remarks>
/// <para>
/// RSA keys answer PKCS#1 v1.5 signatures over SHA-256; ECDSA keys answer
/// <c>r || s</c>, each padded to the curve's size, which is what the engine
/// expects. The engine assembles the CMS itself, so the key only ever signs
/// the signed attributes. The signer never logs or exposes key material.
/// </para>
/// <para>
/// The chain is built once, at construction, with <see cref="X509Chain"/> in
/// <see cref="X509ChainTrustMode.CustomRootTrust"/> over the certificates
/// given, so no system store is consulted. When it does not build, the chain
/// is the leaf followed by the given certificates.
/// </para>
/// <para>
/// The signer owns <see cref="Certificate"/>'s key handle; dispose it when
/// the viewer no longer uses it. Linux has no system certificate store an
/// app can rely on, so callers there load a PKCS#12 file or bytes.
/// </para>
/// </remarks>
public sealed class X509CertificateSigner : ISignatureSigner, IDisposable
{
    readonly RSA? rsa;
    readonly ECDsa? ecdsa;
    readonly ReadOnlyMemory<byte>[] chain;
    readonly Lock gate = new();

    /// <summary>A signer over <paramref name="certificate"/>'s private key; <paramref name="intermediates"/> complete its chain.</summary>
    /// <exception cref="ArgumentException"><paramref name="certificate"/> has no RSA or ECDSA private key.</exception>
    public X509CertificateSigner(X509Certificate2 certificate, IEnumerable<X509Certificate2>? intermediates = null)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        var issuers = intermediates is null ? [] : Arguments.NoNullElements(intermediates, nameof(intermediates));
        Certificate = certificate;
        rsa = certificate.GetRSAPrivateKey();
        ecdsa = rsa is null ? certificate.GetECDsaPrivateKey() : null;
        if (rsa is null && ecdsa is null)
        {
            throw new ArgumentException("The certificate has no RSA or ECDSA private key.", nameof(certificate));
        }
        chain = BuildChain(certificate, issuers);
    }

    /// <summary>A signer over the first certificate with a private key in a PKCS#12 bundle; the bundle's other certificates complete its chain.</summary>
    /// <exception cref="CryptographicException">The bundle can't be read with <paramref name="password"/>.</exception>
    /// <exception cref="ArgumentException">No certificate in the bundle has a private key.</exception>
    public static X509CertificateSigner FromPkcs12(ReadOnlyMemory<byte> data, string? password = null)
    {
        var all = X509CertificateLoader.LoadPkcs12Collection(data.Span, password, KeyStorage);
        return FromCollection(all);
    }

    /// <summary>A signer from a PKCS#12 file; see <see cref="FromPkcs12(ReadOnlyMemory{byte}, string?)"/>.</summary>
    public static X509CertificateSigner FromPkcs12File(string path, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        return FromCollection(X509CertificateLoader.LoadPkcs12CollectionFromFile(path, password, KeyStorage));
    }

    /// <summary>A signer over the certificate with <paramref name="thumbprint"/> in a certificate store.</summary>
    /// <exception cref="ArgumentException">The store has no such certificate with a private key.</exception>
    public static X509CertificateSigner FromStore(string thumbprint, StoreName storeName = StoreName.My, StoreLocation location = StoreLocation.CurrentUser)
    {
        ArgumentNullException.ThrowIfNull(thumbprint);
        using var store = new X509Store(storeName, location);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
        var certificate = found.FirstOrDefault(c => c.HasPrivateKey)
            ?? throw new ArgumentException($"The store has no certificate {thumbprint} with a private key.", nameof(thumbprint));
        return new X509CertificateSigner(certificate, store.Certificates.Where(c => !ReferenceEquals(c, certificate)));
    }

    /// <summary>The signing certificate.</summary>
    public X509Certificate2 Certificate { get; }

    /// <inheritdoc/>
    public SignatureKeyAlgorithm Algorithm => ecdsa is null ? SignatureKeyAlgorithm.RsaPkcs1 : SignatureKeyAlgorithm.Ecdsa;

    /// <inheritdoc/>
    public SignatureDigest Digest => SignatureDigest.Sha256;

    /// <inheritdoc/>
    public IReadOnlyList<ReadOnlyMemory<byte>> GetCertificateChain() => chain;

    /// <inheritdoc/>
    /// <remarks>Signs on the thread pool, so the calling thread returns at once.</remarks>
    public Task<ReadOnlyMemory<byte>> SignAsync(ReadOnlyMemory<byte> message)
    {
        var copy = message.ToArray();
        return Task.Run(() => Sign(copy));
    }

    /// <summary>Signs <paramref name="message"/> on the calling thread.</summary>
    public ReadOnlyMemory<byte> Sign(ReadOnlyMemory<byte> message)
    {
        lock (gate)
        {
            return ecdsa is not null
                ? ecdsa.SignData(message.Span, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
                : rsa!.SignData(message.Span, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }

    /// <summary>Releases the private key handle. <see cref="Certificate"/> stays the caller's.</summary>
    public void Dispose()
    {
        rsa?.Dispose();
        ecdsa?.Dispose();
    }

    static X509KeyStorageFlags KeyStorage =>
        OperatingSystem.IsMacOS() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet;

    static X509CertificateSigner FromCollection(X509Certificate2Collection all)
    {
        var leaf = all.FirstOrDefault(c => c.HasPrivateKey) ?? throw new ArgumentException("No certificate in the bundle has a private key.");
        return new X509CertificateSigner(leaf, all.Where(c => !ReferenceEquals(c, leaf)));
    }

    static ReadOnlyMemory<byte>[] BuildChain(X509Certificate2 leaf, X509Certificate2[] issuers)
    {
        using var builder = new X509Chain();
        builder.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        builder.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        builder.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
        builder.ChainPolicy.DisableCertificateDownloads = true;
        foreach (var issuer in issuers)
        {
            builder.ChainPolicy.ExtraStore.Add(issuer);
            if (issuer.SubjectName.RawData.AsSpan().SequenceEqual(issuer.IssuerName.RawData)) builder.ChainPolicy.CustomTrustStore.Add(issuer);
        }
        builder.Build(leaf);
        var built = builder.ChainElements.Select(e => (ReadOnlyMemory<byte>)e.Certificate.RawData).ToArray();
        foreach (var element in builder.ChainElements) element.Certificate.Dispose();
        if (built.Length > 1 && built[0].Span.SequenceEqual(leaf.RawData)) return built;
        return [leaf.RawData, .. issuers.Where(i => !i.RawData.AsSpan().SequenceEqual(leaf.RawData)).Select(i => (ReadOnlyMemory<byte>)i.RawData)];
    }
}
