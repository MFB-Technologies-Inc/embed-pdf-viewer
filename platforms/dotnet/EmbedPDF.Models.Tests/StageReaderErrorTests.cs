namespace EmbedPDF.Models.Tests;

/// <summary>
/// Port of <c>StageReaderErrorTests.swift</c>: <see cref="StageReaderModel.Error"/>
/// is cleared by a later success of the kind of call that set it, and by no
/// other. The engine offers no failure that later heals, so each test
/// records the failure through the model's own <c>Failed</c> and then makes
/// real calls.
/// </summary>
public class StageReaderErrorTests
{
    static readonly PdfException Failure = new(PdfErrorCode.OperationFailed, "a call the test failed");

    static async Task<(Reader Reader, PageRef Last)> OpenAsync(SingleThreadContext context, bool link)
    {
        var reader = await Reader.OpenAsync(context, "report", link: link);
        await reader.Model.LinkLoadsFinishedAsync();
        return (reader, (await reader.Viewer.GetPagesAsync(reader.Document))[^1].Page);
    }

    [Fact]
    public Task AGoodStageCallClearsTheErrorOfItsOwnKind() => SingleThreadContext.Run(async context =>
    {
        var (reader, _) = await OpenAsync(context, link: false);
        var model = reader.Model;
        Assert.Null(model.Error);
        model.Failed(StageReaderModel.Call.Resize, Failure);
        Assert.Same(Failure, model.Error);
        await model.SizeChangedAsync(390, 844, 2);
        Assert.Null(model.Error);
        // A resize ends in a read of the pages and the camera.
        model.Failed(StageReaderModel.Call.Read, Failure);
        await model.SizeChangedAsync(390, 844, 2);
        Assert.Null(model.Error);
        model.Failed(StageReaderModel.Call.Drag, Failure);
        await model.DragChangedAsync(0, 0);
        Assert.Null(model.Error);
        model.Failed(StageReaderModel.Call.Drag, Failure);
        await model.DragEndedAsync(0, 0);
        Assert.Null(model.Error);
        model.Failed(StageReaderModel.Call.DoubleTap, Failure);
        await model.DoubleTappedAsync(195, 422);
        Assert.Null(model.Error);
        model.Failed(StageReaderModel.Call.PageLookup, Failure);
        model.Tapped(new PointerInput(195, 422));
        await model.PointerCallsFinishedAsync();
        Assert.Null(model.Error);
        await reader.EndAsync();
    });

    [Fact]
    public Task AGoodToolReadClearsAFailedOne() => SingleThreadContext.Run(async context =>
    {
        var (reader, _) = await OpenAsync(context, link: false);
        var model = reader.Model;
        model.Failed(StageReaderModel.Call.ToolRead, Failure);
        await Observed.ExpectAsync(model, () => model.Error, null, () => reader.Interaction.ActivateToolAsync(ToolId.Ink));
        Assert.Equal(ToolId.Ink, model.ActiveToolId);
        await reader.EndAsync();
    });

    [Fact]
    public Task AGoodLinkLoadClearsAFailedOne() => SingleThreadContext.Run(async context =>
    {
        var (reader, last) = await OpenAsync(context, link: true);
        var model = reader.Model;
        model.Failed(StageReaderModel.Call.LinkLoad, Failure);
        await Observed.ExpectAsync(model, () => model.VisiblePages.Any(page => page.Page == last), true,
            () => model.Stage.GoToPageAsync(last));
        await model.LinkLoadsFinishedAsync();
        Assert.Null(model.Error);
        await reader.EndAsync();
    });

    [Fact]
    public Task AGoodLinkTapRegistrationClearsAFailedOne() => SingleThreadContext.Run(async context =>
    {
        var (reader, _) = await OpenAsync(context, link: true);
        var model = reader.Model;
        reader.Stop.Cancel();
        await reader.Observing;
        model.Failed(StageReaderModel.Call.LinkTapRegistration, Failure);
        var observing = model.RunAsync();
        await Observed.ExpectAsync(model, () => model.Error, null);
        await reader.Viewer.DisposeAsync();
        await observing.WaitAsync(TimeSpan.FromSeconds(10));
    });

    /// <summary>A failed link load stays through good calls of every other kind the reader makes without a link capability.</summary>
    [Fact]
    public Task AGoodCallOfAnotherKindKeepsTheError() => SingleThreadContext.Run(async context =>
    {
        var (reader, _) = await OpenAsync(context, link: false);
        var model = reader.Model;
        model.Failed(StageReaderModel.Call.LinkLoad, Failure);
        await model.SizeChangedAsync(390, 844, 2);
        await model.DragChangedAsync(0, -200);
        await model.DragEndedAsync(0, 0);
        await model.DoubleTappedAsync(195, 422);
        model.Tapped(new PointerInput(195, 422));
        await model.PointerCallsFinishedAsync();
        await Observed.ExpectAsync(model, () => model.ActiveToolId, ToolId.Ink, () => reader.Interaction.ActivateToolAsync(ToolId.Ink));
        Assert.Same(Failure, model.Error);
        // A newer failure replaces it, and only its own kind clears that one.
        var newer = new PdfException(PdfErrorCode.OperationFailed, "a newer failure");
        model.Failed(StageReaderModel.Call.Drag, newer);
        await model.SizeChangedAsync(390, 844, 2);
        Assert.Same(newer, model.Error);
        await model.DragChangedAsync(0, 0);
        await model.DragEndedAsync(0, 0);
        Assert.Null(model.Error);
        await reader.EndAsync();
    });
}
