namespace EmbedPDF.Viewer.Tests;

/// <summary>The fixtures the Swift suite bundles, read where they live in the repository.</summary>
static class Fixture
{
    static readonly Dictionary<string, string> Paths = new()
    {
        ["report"] = "pdfium-multiplatform-prototype/fixtures/report.pdf",
        ["encrypted-hello"] = "pdfium-multiplatform-prototype/fixtures/encrypted-hello.pdf",
        ["colors"] = "pdfium-multiplatform-prototype/fixtures/colors.pdf",
        ["features"] = "tooling/viewer-e2e/fixtures/features.pdf",
        ["signed_two_approvals"] = "packages/engine/main/test/fixtures/signed_two_approvals.pdf",
        ["document_actions"] = "tooling/engine-conformance/fixtures/document_actions.pdf",
        ["annotation_kinds"] = "tooling/engine-conformance/fixtures/annotation_kinds.pdf",
        ["form_fields"] = "tooling/engine-conformance/fixtures/form_fields.pdf",
        ["tiny.png"] = "tooling/engine-conformance/fixtures/tiny.png",
        ["measure-foreign-viewport"] = "crates/test-support/fixtures/measure-foreign-viewport.pdf",
        ["stamps"] = "packages/default-stamps/en/stamps.pdf",
        ["signed_certified"] = "packages/engine/main/test/fixtures/signed_certified.pdf",
        ["unsigned_sigfield"] = "packages/engine/main/test/fixtures/unsigned_sigfield.pdf",
        ["signature_artwork"] = "packages/engine/main/test/fixtures/signature_artwork.pdf",
        ["TEST-ONLY-signer.pem"] = "packages/engine/main/test/fixtures/signature-compat/keys/TEST-ONLY-signer.pem",
        ["TEST-ONLY-private-key.pem"] = "packages/engine/main/test/fixtures/signature-compat/keys/TEST-ONLY-private-key.pem",
    };

    public static string Path(string name)
    {
        var relative = Paths[name];
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"missing fixture: {relative}");
    }

    public static byte[] Data(string name) => File.ReadAllBytes(Path(name));

    /// <summary>The first item of <paramref name="source"/> that matches, or a timeout failure after 10 seconds.</summary>
    public static async Task<T> First<T>(IAsyncEnumerable<object> source, Func<object, T?> match) where T : class
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var item in source.WithCancellation(timeout.Token))
        {
            if (match(item) is { } found) return found;
        }
        throw new InvalidOperationException("the stream ended first");
    }

    public static async Task<T> First<T>(IAsyncEnumerable<DocumentEvent> source) where T : DocumentEvent
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var item in source.WithCancellation(timeout.Token))
        {
            if (item is T found) return found;
        }
        throw new InvalidOperationException("the stream ended first");
    }
}
