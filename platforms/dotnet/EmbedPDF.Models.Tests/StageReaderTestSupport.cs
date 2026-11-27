namespace EmbedPDF.Models.Tests;

/// <summary>A sized reader with its RunAsync running and the tool state read once.</summary>
sealed record Reader(
    PdfViewer Viewer, DocumentInfo Document, StageReaderModel Model, StageCapability Stage,
    InteractionCapability Interaction, AnnotationCapability? Annotations, LinkCapability? Link,
    CancellationTokenSource Stop, Task Observing)
{
    public static async Task<Reader> OpenAsync(SingleThreadContext context, string fixture = "annotation_kinds", bool link = true, bool annotation = true)
    {
        var viewer = await Fixture.StartedAsync(b =>
        {
            b = b.Render(new RenderConfig()).Interaction().Stage(new StageConfig());
            if (link) b = b.Link();
            return annotation ? b.Annotation() : b;
        });
        var document = await viewer.OpenAsync(Fixture.Data(fixture));
        var stage = await viewer.GetStageAsync(document);
        var interaction = await viewer.GetInteractionAsync(document);
        var links = link ? await viewer.GetLinkAsync(document) : null;
        var model = context.Watch(new StageReaderModel(stage, await viewer.GetRenderAsync(document), interaction, links));
        var stop = new CancellationTokenSource();
        var observing = model.RunAsync(stop.Token);
        await model.SizeChangedAsync(390, 844, 2);
        await Observed.ExpectAsync(model, () => model.ActiveToolId is not null, true);
        AnnotationCapability? annotations = null;
        if (annotation)
        {
            annotations = await viewer.GetAnnotationAsync(document);
            // The fixture's own annotations are read before any test counts them.
            await annotations.RefreshAsync();
        }
        return new Reader(viewer, document, model, stage, interaction, annotations, links, stop, observing);
    }

    public async Task EndAsync()
    {
        await Viewer.DisposeAsync();
        await Observing.WaitAsync(TimeSpan.FromSeconds(10));
        Model.Dispose();
    }

    /// <summary>A viewport point inside the first visible page, <paramref name="fraction"/> of the way across and down it.</summary>
    public static PointerInput Point(StageReaderModel model, double fraction)
    {
        var page = model.VisiblePages[0];
        return new PointerInput(page.ScreenX + page.Transform.ViewWidth * fraction, page.ScreenY + page.Transform.ViewHeight * fraction);
    }

    public static async Task<int> InkCountAsync(AnnotationCapability annotations) =>
        (await annotations.ListAsync()).Count(info => info.Subtype == AnnotationSubtype.Ink);
}
