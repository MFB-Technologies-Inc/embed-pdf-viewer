using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public async Task AddEmbedPdfRegistersOneViewerThatOpensDocuments()
    {
        var services = new ServiceCollection().AddEmbedPdf(builder => builder.Metadata());
        await using var provider = services.BuildServiceProvider();
        var viewer = provider.GetRequiredService<PdfViewer>();
        Assert.Same(viewer, provider.GetRequiredService<PdfViewer>());
        Assert.True(provider.GetRequiredService<PdfViewerOptions>().Metadata);
        await viewer.StartAsync();
        Assert.Equal(14, (await viewer.OpenAsync(Fixture.Data("report"))).PageCount);
    }

    [Fact]
    public async Task DisposingTheProviderDisposesTheViewer()
    {
        PdfViewer viewer;
        await using (var provider = new ServiceCollection().AddEmbedPdf().BuildServiceProvider())
        {
            viewer = provider.GetRequiredService<PdfViewer>();
            await viewer.StartAsync();
        }
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => viewer.GetDocumentsAsync())).Code);
    }

    sealed class CountingFactory : ILoggerFactory
    {
        public int Created;
        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName)
        {
            Interlocked.Increment(ref Created);
            return NullLogger.Instance;
        }
        public void Dispose() { }
    }

    [Fact]
    public async Task TheLoggerComesFromTheRegisteredFactory()
    {
        var factory = new CountingFactory();
        await using var provider = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(factory)
            .AddEmbedPdf(new PdfViewerOptions())
            .BuildServiceProvider();
        _ = provider.GetRequiredService<PdfViewer>();
        Assert.Equal(1, factory.Created);
    }
}
