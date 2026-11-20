using EmbedPDF;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers EmbedPDF with a service collection.</summary>
public static class EmbedPdfServiceCollectionExtensions
{
    /// <summary>Registers a <see cref="PdfViewer"/> with no plugin installed, as <see cref="AddEmbedPdf(IServiceCollection, PdfViewerOptions)"/> does.</summary>
    /// <param name="services">The collection to add to.</param>
    public static IServiceCollection AddEmbedPdf(this IServiceCollection services) => services.AddEmbedPdf(new PdfViewerOptions());

    /// <summary>
    /// Registers <see cref="PdfViewerOptions"/> built by <paramref name="configure"/>
    /// and one <see cref="PdfViewer"/> made from them. The viewer is created on
    /// first resolve and is not started: call <see cref="PdfViewer.StartAsync"/>
    /// once before using it. The container disposes it.
    /// </summary>
    /// <param name="services">The collection to add to.</param>
    /// <param name="configure">Chooses the plugins.</param>
    public static IServiceCollection AddEmbedPdf(this IServiceCollection services, Func<PdfViewerOptionsBuilder, PdfViewerOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        return services.AddEmbedPdf(configure(new PdfViewerOptionsBuilder()).Build());
    }

    /// <summary>
    /// Registers <paramref name="options"/> and one <see cref="PdfViewer"/>
    /// made from them. When the options keep the default null logger and the
    /// container has an <see cref="ILoggerFactory"/>, the viewer logs through
    /// a logger from that factory.
    /// </summary>
    /// <param name="services">The collection to add to.</param>
    /// <param name="options">What the viewer installs.</param>
    public static IServiceCollection AddEmbedPdf(this IServiceCollection services, PdfViewerOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        services.AddSingleton(provider =>
        {
            var resolved = provider.GetRequiredService<PdfViewerOptions>();
            if (ReferenceEquals(resolved.Logger, NullLogger.Instance) && provider.GetService<ILoggerFactory>() is { } factory)
            {
                resolved = resolved with { Logger = factory.CreateLogger<PdfViewer>() };
            }
            return new PdfViewer(resolved);
        });
        return services;
    }
}
