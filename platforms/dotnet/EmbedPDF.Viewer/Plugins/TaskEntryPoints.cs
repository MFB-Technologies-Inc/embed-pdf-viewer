// Entry points named after what a caller wants to do, over capabilities
// named after the engine part that does it. Each one holds only the
// capability it forwards to, which it hands back for everything else, and
// every member is a one-line forward with the capability member's own name,
// signature and errors. TaskEntryPointTests keeps the two sides in step.

namespace EmbedPDF;

/// <summary>
/// Move through a document's pages: the current page, the page count, and
/// going to a page. Get one with <see cref="PdfViewer.GetPageNavigatorAsync"/>,
/// which needs what <see cref="PdfViewer.GetStageAsync"/> needs and throws what
/// it throws. For everything else use <see cref="Stage"/>.
/// </summary>
public sealed class PageNavigator : IDisposable
{
    internal PageNavigator(StageCapability stage) => Stage = stage;

    /// <summary>The capability every member forwards to.</summary>
    public StageCapability Stage { get; }

#pragma warning disable CS1591 // Each member is documented on the StageCapability member it forwards to.
    public Task<int> GetPageCountAsync(CancellationToken cancellationToken = default) => Stage.GetPageCountAsync(cancellationToken);
    public Task<int> GetCurrentPageIndexAsync(CancellationToken cancellationToken = default) => Stage.GetCurrentPageIndexAsync(cancellationToken);
    public Task<PageRef?> GetCurrentPageAsync(CancellationToken cancellationToken = default) => Stage.GetCurrentPageAsync(cancellationToken);
    public Task<bool> CanGoNextAsync(CancellationToken cancellationToken = default) => Stage.CanGoNextAsync(cancellationToken);
    public Task<bool> CanGoPreviousAsync(CancellationToken cancellationToken = default) => Stage.CanGoPreviousAsync(cancellationToken);
    public Task GoToPageIndexAsync(int index, StageGoToOptions? options = null, CancellationToken cancellationToken = default) => Stage.GoToPageIndexAsync(index, options, cancellationToken);
    public Task GoToPageAsync(PageRef page, StageGoToOptions? options = null, CancellationToken cancellationToken = default) => Stage.GoToPageAsync(page, options, cancellationToken);
    public Task GoToFirstPageAsync(StageGoToOptions? options = null, CancellationToken cancellationToken = default) => Stage.GoToFirstPageAsync(options, cancellationToken);
    public Task GoToLastPageAsync(StageGoToOptions? options = null, CancellationToken cancellationToken = default) => Stage.GoToLastPageAsync(options, cancellationToken);
    public Task NextPageAsync(StageGoToOptions? options = null, CancellationToken cancellationToken = default) => Stage.NextPageAsync(options, cancellationToken);
    public Task PreviousPageAsync(StageGoToOptions? options = null, CancellationToken cancellationToken = default) => Stage.PreviousPageAsync(options, cancellationToken);
#pragma warning restore CS1591

    /// <summary>Disposes <see cref="Stage"/>.</summary>
    public void Dispose() => Stage.Dispose();
}

/// <summary>
/// Zoom a document's view in and out and fit its pages to the viewport. Get
/// one with <see cref="PdfViewer.GetPageZoomAsync"/>, which needs what
/// <see cref="PdfViewer.GetStageAsync"/> needs and throws what it throws. For
/// everything else (zoom by a factor, around a point, the zoom tool's box,
/// rotation) use <see cref="Stage"/>.
/// </summary>
public sealed class PageZoom : IDisposable
{
    internal PageZoom(StageCapability stage) => Stage = stage;

    /// <summary>The capability every member forwards to.</summary>
    public StageCapability Stage { get; }

#pragma warning disable CS1591 // Each member is documented on the StageCapability member it forwards to.
    public Task<double> GetZoomLevelAsync(CancellationToken cancellationToken = default) => Stage.GetZoomLevelAsync(cancellationToken);
    public Task<StageZoomMode> GetZoomModeAsync(CancellationToken cancellationToken = default) => Stage.GetZoomModeAsync(cancellationToken);
    public Task<bool> CanZoomInAsync(CancellationToken cancellationToken = default) => Stage.CanZoomInAsync(cancellationToken);
    public Task<bool> CanZoomOutAsync(CancellationToken cancellationToken = default) => Stage.CanZoomOutAsync(cancellationToken);
    public Task ZoomInAsync(CancellationToken cancellationToken = default) => Stage.ZoomInAsync(cancellationToken);
    public Task ZoomOutAsync(CancellationToken cancellationToken = default) => Stage.ZoomOutAsync(cancellationToken);
    public Task ZoomToAsync(StageZoomTarget target, PdfPoint? around = null, CancellationToken cancellationToken = default) => Stage.ZoomToAsync(target, around, cancellationToken);
    public Task FitWidthAsync(CancellationToken cancellationToken = default) => Stage.FitWidthAsync(cancellationToken);
    public Task FitPageAsync(CancellationToken cancellationToken = default) => Stage.FitPageAsync(cancellationToken);
#pragma warning restore CS1591

    /// <summary>Disposes <see cref="Stage"/>.</summary>
    public void Dispose() => Stage.Dispose();
}

/// <summary>
/// Pick the tool pointer input goes to: list the tools, read the active one,
/// and switch. Get one with <see cref="PdfViewer.GetToolSwitcherAsync"/>,
/// which needs what <see cref="PdfViewer.GetInteractionAsync"/> needs and
/// throws what it throws. For everything else use <see cref="Interaction"/>.
/// </summary>
public sealed class ToolSwitcher : IDisposable
{
    internal ToolSwitcher(InteractionCapability interaction) => Interaction = interaction;

    /// <summary>The capability every member forwards to.</summary>
    public InteractionCapability Interaction { get; }

#pragma warning disable CS1591 // Each member is documented on the InteractionCapability member it forwards to.
    public Task<IReadOnlyList<InteractionTool>> GetToolsAsync(CancellationToken cancellationToken = default) => Interaction.GetToolsAsync(cancellationToken);
    public Task<ToolId> GetActiveToolIdAsync(CancellationToken cancellationToken = default) => Interaction.GetActiveToolIdAsync(cancellationToken);
    public Task<ToolId> GetDefaultToolIdAsync(CancellationToken cancellationToken = default) => Interaction.GetDefaultToolIdAsync(cancellationToken);
    public Task<bool> HasToolAsync(ToolId id, CancellationToken cancellationToken = default) => Interaction.HasToolAsync(id, cancellationToken);
    public Task ActivateToolAsync(ToolId id, CancellationToken cancellationToken = default) => Interaction.ActivateToolAsync(id, cancellationToken);
    public Task ActivateDefaultToolAsync(CancellationToken cancellationToken = default) => Interaction.ActivateDefaultToolAsync(cancellationToken);
    public Task PushToolAsync(ToolId id, CancellationToken cancellationToken = default) => Interaction.PushToolAsync(id, cancellationToken);
    public Task PopToolAsync(CancellationToken cancellationToken = default) => Interaction.PopToolAsync(cancellationToken);
#pragma warning restore CS1591

    /// <summary>Disposes <see cref="Interaction"/>.</summary>
    public void Dispose() => Interaction.Dispose();
}

public sealed partial class PdfViewer
{
    /// <summary>A <see cref="PageNavigator"/> over the document's stage.</summary>
    public async Task<PageNavigator> GetPageNavigatorAsync(DocumentInfo document, CancellationToken cancellationToken = default) =>
        new(await GetStageAsync(document, cancellationToken).ConfigureAwait(false));

    /// <summary>A <see cref="PageZoom"/> over the document's stage.</summary>
    public async Task<PageZoom> GetPageZoomAsync(DocumentInfo document, CancellationToken cancellationToken = default) =>
        new(await GetStageAsync(document, cancellationToken).ConfigureAwait(false));

    /// <summary>A <see cref="ToolSwitcher"/> over the document's pointer hub.</summary>
    public async Task<ToolSwitcher> GetToolSwitcherAsync(DocumentInfo document, CancellationToken cancellationToken = default) =>
        new(await GetInteractionAsync(document, cancellationToken).ConfigureAwait(false));
}
