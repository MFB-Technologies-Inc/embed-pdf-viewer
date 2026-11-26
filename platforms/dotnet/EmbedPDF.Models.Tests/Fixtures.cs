namespace EmbedPDF.Models.Tests;

/// <summary>The fixtures the Swift suite bundles, read where they live in the repository.</summary>
static class Fixture
{
    static readonly Dictionary<string, string> Paths = new()
    {
        ["report"] = "pdfium-multiplatform-prototype/fixtures/report.pdf",
        ["encrypted-hello"] = "pdfium-multiplatform-prototype/fixtures/encrypted-hello.pdf",
        ["features"] = "tooling/viewer-e2e/fixtures/features.pdf",
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

    public static byte[] Data(string name = "report") => File.ReadAllBytes(Path(name));

    /// <summary>A started viewer with <paramref name="configure"/>'s plugins.</summary>
    public static async Task<PdfViewer> StartedAsync(Func<PdfViewerOptionsBuilder, PdfViewerOptionsBuilder>? configure = null)
    {
        var builder = new PdfViewerOptionsBuilder();
        var viewer = new PdfViewer((configure?.Invoke(builder) ?? builder).Build());
        await viewer.StartAsync();
        return viewer;
    }

    /// <summary>The first item of <paramref name="source"/> that matches, within 10 seconds.</summary>
    public static async Task<T> First<T>(IAsyncEnumerable<object> source, Func<object, T?> match) where T : class
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var item in source.WithCancellation(timeout.Token))
        {
            if (match(item) is { } found) return found;
        }
        throw new InvalidOperationException("the stream ended first");
    }
}
