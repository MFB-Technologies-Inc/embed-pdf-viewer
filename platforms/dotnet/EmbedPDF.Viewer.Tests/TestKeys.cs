using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EmbedPDF.Viewer.Tests;

/// <summary>The TEST-ONLY RSA key and certificate the Swift suite signs with.</summary>
static class TestKeys
{
    public static string CertificatePem => File.ReadAllText(Fixture.Path("TEST-ONLY-signer.pem"));

    public static string PrivateKeyPem => File.ReadAllText(Fixture.Path("TEST-ONLY-private-key.pem"));

    /// <summary>The test certificate with its private key.</summary>
    public static X509Certificate2 Certificate() => X509Certificate2.CreateFromPem(CertificatePem, PrivateKeyPem);

    /// <summary>A signer over the test key.</summary>
    public static ISignatureSigner Signer() => new RsaSigner(Certificate());

    sealed class RsaSigner(X509Certificate2 certificate) : ISignatureSigner
    {
        public IReadOnlyList<ReadOnlyMemory<byte>> GetCertificateChain() => [certificate.RawData];

        public SignatureKeyAlgorithm Algorithm => SignatureKeyAlgorithm.RsaPkcs1;

        public SignatureDigest Digest => SignatureDigest.Sha256;

        public Task<ReadOnlyMemory<byte>> SignAsync(ReadOnlyMemory<byte> message)
        {
            using var key = certificate.GetRSAPrivateKey()!;
            return Task.FromResult<ReadOnlyMemory<byte>>(key.SignData(message.Span, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
    }
}
