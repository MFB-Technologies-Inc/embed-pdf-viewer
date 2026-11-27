using System.Runtime.CompilerServices;

namespace EmbedPDF.Models.Tests;

/// <summary>Port of <c>AnnotationModelTests.swift</c>.</summary>
public class AnnotationModelTests
{
    static async Task<(PdfViewer Viewer, DocumentInfo Document, AnnotationCapability Annotations)> OpenedAsync(string[]? scope = null)
    {
        var viewer = await Fixture.StartedAsync(b => b.Scope(scope).Interaction().Annotation());
        var document = await viewer.OpenAsync(Fixture.Data());
        return (viewer, document, await viewer.GetAnnotationAsync(document));
    }

    [Fact]
    public Task ANewModelAllowsNothing() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, annotations) = await OpenedAsync();
        var model = context.Watch(new AnnotationModel(annotations));
        Assert.False(model.CanRead);
        Assert.False(model.CanCreate);
        Assert.Null(model.Error);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task TheObservingModelFollowsTheKernelUntilTheDocumentCloses() => SingleThreadContext.RunWatched(async context =>
    {
        var (viewer, document, annotations) = await OpenedAsync();
        var model = context.Watch(new AnnotationModel(annotations));
        var observing = model.RunAsync();
        await Observed.ExpectAsync(model, () => model.CanCreate, true);
        Assert.Equal(await annotations.CanReadAsync(), model.CanRead);
        Assert.Equal(await annotations.CanCreateAsync(), model.CanCreate);
        await Observed.ExpectAsync(model, () => model.CanRead, false, () => viewer.CloseAsync(document));
        await observing.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(model.CanCreate);
        Assert.Equal(PdfErrorCode.InstanceClosed, model.Error?.Code);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ARefreshNotifiesObserversOfEachProperty() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, annotations) = await OpenedAsync();
        var model = context.Watch(new AnnotationModel(annotations));
        Assert.True(await Observed.FiresAsync(model, nameof(AnnotationModel.CanRead), () => model.RefreshAsync()));
        var other = context.Watch(new AnnotationModel(annotations));
        Assert.True(await Observed.FiresAsync(other, nameof(AnnotationModel.CanCreate), () => other.RefreshAsync()));
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task AScopeWithoutAnnotationsReadsFalse() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, annotations) = await OpenedAsync(["doc.open", "doc.render"]);
        var model = context.Watch(new AnnotationModel(annotations));
        await model.RefreshAsync();
        Assert.Equal(await annotations.CanReadAsync(), model.CanRead);
        Assert.False(model.CanRead);
        Assert.False(model.CanCreate);
        Assert.Null(model.Error);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ACancelledTaskReleasesTheModel() => SingleThreadContext.RunWatched(async context =>
    {
        var (viewer, _, annotations) = await OpenedAsync();
        await Observed.CollectedAsync(await ObserveAndCancel(context, annotations));
        await viewer.DisposeAsync();
    });

    [MethodImpl(MethodImplOptions.NoInlining)]
    static async Task<WeakReference> ObserveAndCancel(SingleThreadContext context, AnnotationCapability annotations)
    {
        var model = context.Watch(new AnnotationModel(annotations));
        using var stop = new CancellationTokenSource();
        var observing = model.RunAsync(stop.Token);
        await Observed.ExpectAsync(model, () => model.CanRead, true);
        stop.Cancel();
        await observing;
        return new WeakReference(model);
    }

    [Fact]
    public Task AnOlderReadCannotOverwriteANewerOne() => SingleThreadContext.RunWatched(async context =>
    {
        var (viewer, document, annotations) = await OpenedAsync();
        var model = context.Watch(new AnnotationModel(annotations));
        var (reached, release) = Observed.HoldFirstRead(model.Snapshots);
        var stale = model.RefreshAsync();
        await reached;
        await viewer.CloseAsync(document);
        await model.RefreshAsync();
        Assert.Equal(PdfErrorCode.InstanceClosed, model.Error?.Code);
        release();
        await stale;
        Assert.False(model.CanRead);
        Assert.False(model.CanCreate);
        Assert.Equal(PdfErrorCode.InstanceClosed, model.Error?.Code);
        await viewer.DisposeAsync();
    });
}
