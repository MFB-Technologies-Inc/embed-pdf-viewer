using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>The port of KeychainSignerTests.swift, over <see cref="X509CertificateSigner"/> and <see cref="TrustAnchors"/>.</summary>
public sealed class X509CertificateSignerTests
{
    static readonly byte[] Hello = "hello"u8.ToArray();

    static X509Certificate2 EcCertificate(out ECDsa key)
    {
        key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=EC test", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    [Fact]
    public async Task AnRsaKeySignsPkcs1()
    {
        using var signer = new X509CertificateSigner(TestKeys.Certificate());
        Assert.Equal(SignatureKeyAlgorithm.RsaPkcs1, signer.Algorithm);
        Assert.Equal(SignatureDigest.Sha256, signer.Digest);
        var signature = await signer.SignAsync(Hello);
        Assert.Equal(256, signature.Length);
        Assert.Single(signer.GetCertificateChain());
        using var check = TestKeys.Certificate().GetRSAPublicKey()!;
        Assert.True(check.VerifyData(Hello, signature.Span, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        Assert.Equal(signature.Length, signer.Sign(Hello).Length);
    }

    [Fact]
    public async Task AnEcKeySignsRawRAndS()
    {
        using var certificate = EcCertificate(out var key);
        using var _k = key;
        using var signer = new X509CertificateSigner(certificate);
        Assert.Equal(SignatureKeyAlgorithm.Ecdsa, signer.Algorithm);
        var signature = await signer.SignAsync("x"u8.ToArray());
        Assert.Equal(64, signature.Length);
        Assert.True(key.VerifyData("x"u8, signature.Span, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Fact]
    public void ACertificateWithoutAPrivateKeyIsRefused()
    {
        using var publicOnly = X509CertificateLoader.LoadCertificate(TestKeys.Certificate().RawData);
        Assert.Throws<ArgumentException>(() => new X509CertificateSigner(publicOnly));
        Assert.Throws<ArgumentNullException>(() => new X509CertificateSigner(null!));
    }

    [Fact]
    public async Task APkcs12FromBytesOrAFileSigns()
    {
        var bundle = TestKeys.Certificate().Export(X509ContentType.Pkcs12, "secret");
        using (var signer = X509CertificateSigner.FromPkcs12(bundle, "secret"))
        {
            Assert.Equal(256, (await signer.SignAsync(Hello)).Length);
        }
        var path = Path.Combine(Path.GetTempPath(), "embedpdf-" + Guid.NewGuid().ToString("N") + ".p12");
        try
        {
            await File.WriteAllBytesAsync(path, bundle);
            using var fromFile = X509CertificateSigner.FromPkcs12File(path, "secret");
            Assert.Equal(TestKeys.Certificate().RawData, fromFile.GetCertificateChain()[0].ToArray());
        }
        finally
        {
            File.Delete(path);
        }
        Assert.ThrowsAny<CryptographicException>(() => X509CertificateSigner.FromPkcs12(bundle, "wrong"));
    }

    [Fact]
    public void AChainBuildsLeafFirstFromTheGivenIssuer()
    {
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Test root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=Test leaf", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var issued = leafRequest.Create(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10), [1, 2, 3, 4]);
        using var leaf = issued.CopyWithPrivateKey(leafKey);

        using var signer = new X509CertificateSigner(leaf, [root]);
        Assert.Equal([leaf.RawData, root.RawData], signer.GetCertificateChain().Select(c => c.ToArray()));

        // An unrelated certificate does not join the chain's head: the leaf stays first.
        using var lone = new X509CertificateSigner(leaf, [TestKeys.Certificate()]);
        Assert.Equal(leaf.RawData, lone.GetCertificateChain()[0].ToArray());
    }

    [Fact]
    public void AnUnknownThumbprintIsNotInTheStore()
    {
        Assert.Throws<ArgumentException>(() => X509CertificateSigner.FromStore(new string('0', 40), StoreName.My, StoreLocation.CurrentUser));
    }

    [Theory]
    [InlineData("not base64 at all!")]
    [InlineData("bm90IGEgY2VydGlmaWNhdGU=")]
    public void ABadAnchorIsAnUnreadableCertificate(string body)
    {
        var pem = $"-----BEGIN CERTIFICATE-----\n{body}\n-----END CERTIFICATE-----";
        Assert.ThrowsAny<CryptographicException>(() => TrustAnchors.FromPem([pem]));
        Assert.ThrowsAny<CryptographicException>(() => TrustAnchors.FromPem(["no certificate here"]));
    }

    [Fact]
    public void ACertificatePemIsAnAnchor()
    {
        var anchors = TrustAnchors.FromPem([TestKeys.CertificatePem]);
        Assert.Single(anchors.Certificates);
        Assert.Equal(0x30, anchors.Certificates[0].Span[0]);
        Assert.Equal(anchors.Certificates, anchors.GetAnchors());
    }

    [Fact]
    public void AnchorsLoadFromEmbeddedResourcesAndCertificates()
    {
        var embedded = TrustAnchors.FromEmbeddedResources(typeof(X509CertificateSignerTests).Assembly, "Anchors.");
        Assert.Equal(TestKeys.Certificate().RawData, Assert.Single(embedded.Certificates).ToArray());
        Assert.Empty(TrustAnchors.FromEmbeddedResources(typeof(X509CertificateSignerTests).Assembly, "Nothing.").Certificates);
        var fromCertificates = TrustAnchors.FromCertificates([TestKeys.Certificate()]);
        Assert.Equal(embedded.Certificates[0].ToArray(), fromCertificates.Certificates[0].ToArray());
        var fromDer = new TrustAnchors([TestKeys.Certificate().RawData]);
        Assert.Single(fromDer.Certificates);
    }

    [Fact]
    public async Task TheTestCertificateAsAnAnchorIsTrusted()
    {
        var (viewer, _, signatures) = await SignatureTests.Opened("unsigned_sigfield", signer: false);
        await using var _v = viewer;
        viewer.SetSigner(new X509CertificateSigner(TestKeys.Certificate()));
        viewer.SetTrust(TrustAnchors.FromPem([TestKeys.CertificatePem]));
        var field = await SignatureTests.UnsignedField(signatures);
        await signatures.SignAsync(SignatureTests.SignInput(field));
        var verdict = await signatures.ValidateFieldAsync(field);
        Assert.Equal(SignatureTrustStatus.Trusted, verdict.Trust);
        Assert.Equal(SignatureSummary.Valid, verdict.Summary);
    }

    [Fact]
    public async Task AnEcSignerSealsAField()
    {
        using var certificate = EcCertificate(out var key);
        using var _k = key;
        var (viewer, _, signatures) = await SignatureTests.Opened("unsigned_sigfield", signer: false);
        await using var _v = viewer;
        var field = await SignatureTests.UnsignedField(signatures);
        var done = await signatures.SignAsync(SignatureTests.SignInput(field), new X509CertificateSigner(certificate));
        Assert.Equal(SignatureCompletionStatus.Completed, done.Status);
        Assert.Equal(SignatureIntegrity.Valid, (await signatures.ValidateFieldAsync(field)).Integrity);
    }
}
