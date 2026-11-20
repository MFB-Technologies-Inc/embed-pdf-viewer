using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Answers every prompt with tiny.png, and counts the prompts.</summary>
sealed class TinyPngPicker : IAnnotationFilePicker
{
    int asked;

    public int Prompts => Volatile.Read(ref asked);

    public Task<AnnotationPickedFile?> PickAsync(AnnotationFilePromptRequest request)
    {
        Interlocked.Increment(ref asked);
        return Task.FromResult<AnnotationPickedFile?>(new AnnotationPickedFile(Fixture.Data("tiny.png"), "tiny.png", "image/png"));
    }
}

public sealed class AnnotationTests
{
    static readonly CancellationToken Cancelled = new(true);

    internal static async Task<(PdfViewer Viewer, DocumentInfo Document, AnnotationCapability Annotations)> Opened(string fixture = "annotation_kinds", bool selection = false)
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Selection = selection, Annotation = "" });
        var document = await viewer.OpenAsync(Fixture.Data(fixture));
        var annotations = await viewer.GetAnnotationAsync(document);
        await annotations.RefreshAsync();
        return (viewer, document, annotations);
    }

    static async Task<PageRef> FirstPage(PdfViewer viewer, DocumentInfo document) => (await viewer.GetPagesAsync(document))[0].Page;

    const string Square = """{"subtype": "square", "box": {"x": 72, "y": 72, "width": 100, "height": 50}}""";

    [Fact]
    public async Task LoadingListsTheFixturesAnnotations()
    {
        var (viewer, _, annotations) = await Opened();
        await using var _v = viewer;
        await annotations.WhenSyncedAsync();
        Assert.Equal(ResourceStatus.Ready, await annotations.GetStatusAsync());
        Assert.NotEmpty(await annotations.ListAsync());
    }

    [Fact]
    public async Task CreateUpdateDeleteIsObserved()
    {
        var (viewer, document, annotations) = await Opened("report");
        await using var _v = viewer;
        var page = await FirstPage(viewer, document);
        var events = annotations.ObserveEvents();
        var created = await annotations.CreateAsync(Square, page);
        await annotations.UpdateAsync(created, """{"contents": "From C#"}""");
        Assert.Equal("From C#", (await annotations.GetAsync(created))?.Contents);
        await annotations.DeleteAsync(created);
        var sawCreated = false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var e in events.WithCancellation(timeout.Token))
        {
            if (e is AnnotationEvent.Created c) { sawCreated = true; Assert.Equal(created, c.Annotation.AnnotationRef); }
            if (e is AnnotationEvent.Deleted) break;
        }
        Assert.True(sawCreated);
    }

    [Fact]
    public async Task AStampFromPngBytesIsCreated()
    {
        var (viewer, document, annotations) = await Opened("report");
        await using var _v = viewer;
        var page = await FirstPage(viewer, document);
        using var drafts = annotations.GetDrafts();
        await using var png = File.OpenRead(Fixture.Path("tiny.png"));
        var source = await AnnotationStampSource.FromStreamAsync(png, "image/png");
        var stamp = await drafts.PlaceStampAsync(source, new AnnotationStampPlacement(page, new PdfPoint(200, 200)), 80);
        Assert.Equal(AnnotationSubtype.Stamp, (await annotations.GetAsync(stamp))?.Subtype);
    }

    static async Task<IReadOnlyList<AnnotationSubtype>> PollSubtypes(AnnotationCapability annotations)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        IReadOnlyList<AnnotationSubtype> subtypes = [];
        while (subtypes.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
            subtypes = [.. (await annotations.ListAsync()).Select(a => a.Subtype)];
        }
        return subtypes;
    }

    [Fact]
    public async Task AFilePickerSuppliesARequestedStamp()
    {
        var (viewer, document, annotations) = await Opened("report");
        await using var _v = viewer;
        var page = await FirstPage(viewer, document);
        var picker = new TinyPngPicker();
        await annotations.SetFilePickerAsync(picker);
        await (await viewer.GetInteractionAsync(document)).ActivateToolAsync(ToolId.Stamp);
        using var drafts = annotations.GetDrafts();
        Assert.True(await drafts.RequestStampAtAsync(page, new PdfPoint(200, 200)));
        // The pick answers off the kernel thread and the stamp is placed after it.
        Assert.Equal([AnnotationSubtype.Stamp], await PollSubtypes(annotations));
        Assert.Equal(1, picker.Prompts);
    }

    sealed class ThrowingPicker : IAnnotationFilePicker
    {
        public int Prompts;
        public Task<AnnotationPickedFile?> PickAsync(AnnotationFilePromptRequest request)
        {
            Interlocked.Increment(ref Prompts);
            throw new InvalidOperationException("no file today");
        }
    }

    sealed class DisposingPicker(PdfViewer viewer) : IAnnotationFilePicker
    {
        public readonly TaskCompletionSource Disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<AnnotationPickedFile?> PickAsync(AnnotationFilePromptRequest request)
        {
            await viewer.DisposeAsync();
            Disposed.TrySetResult();
            return null;
        }
    }

    [Fact]
    public async Task AThrowingPickerDeclinesAndTheViewerKeepsWorking()
    {
        var (viewer, document, annotations) = await Opened("report");
        await using var _v = viewer;
        var page = await FirstPage(viewer, document);
        var picker = new ThrowingPicker();
        await annotations.SetFilePickerAsync(picker);
        await (await viewer.GetInteractionAsync(document)).ActivateToolAsync(ToolId.Stamp);
        using var drafts = annotations.GetDrafts();
        Assert.True(await drafts.RequestStampAtAsync(page, new PdfPoint(200, 200)));
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (Volatile.Read(ref picker.Prompts) == 0 && DateTime.UtcNow < deadline) await Task.Delay(20);
        Assert.Equal(1, picker.Prompts);
        await Task.Delay(100);
        Assert.Empty(await annotations.ListAsync());
        Assert.NotNull(await annotations.CreateAsync(Square, page));
    }

    [Fact]
    public async Task APickerMayDisposeTheViewer()
    {
        var (viewer, document, annotations) = await Opened("report");
        var page = await FirstPage(viewer, document);
        var picker = new DisposingPicker(viewer);
        await annotations.SetFilePickerAsync(picker);
        await (await viewer.GetInteractionAsync(document)).ActivateToolAsync(ToolId.Stamp);
        using var drafts = annotations.GetDrafts();
        Assert.True(await drafts.RequestStampAtAsync(page, new PdfPoint(200, 200)));
        await picker.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => annotations.ListAsync())).Code);
    }

    [Fact]
    public async Task SelectingAndClearing()
    {
        var (viewer, _, annotations) = await Opened();
        await using var _v = viewer;
        var first = (await annotations.ListAsync())[0];
        using var selection = annotations.GetSelection();
        await selection.SelectAsync([first.AnnotationRef]);
        Assert.Equal([first.AnnotationRef], await selection.GetRefsAsync());
        Assert.Equal([first.AnnotationRef], (await annotations.GetSelectedAsync()).Select(a => a.AnnotationRef));
        await selection.ClearAsync();
        Assert.Empty(await selection.GetRefsAsync());
    }

    [Fact]
    public async Task CreateFromSelectionMarksTheSelectedText()
    {
        var (viewer, document, annotations) = await Opened("report", selection: true);
        await using var _v = viewer;
        var page = await FirstPage(viewer, document);
        using var selection = await viewer.GetSelectionAsync(document);
        await selection.EnsureLoadedAsync(page);
        await selection.SelectPageAsync(page);
        using var drafts = annotations.GetDrafts();
        var refs = await drafts.CreateFromSelectionAsync(ToolId.Highlight);
        Assert.NotEmpty(refs);
        Assert.Equal(AnnotationSubtype.Highlight, (await annotations.GetAsync(refs[0]))?.Subtype);
    }

    [Fact]
    public async Task CreateLeftOutDoesNotSelect()
    {
        var (viewer, document, annotations) = await Opened("report");
        await using var _v = viewer;
        var page = await FirstPage(viewer, document);
        using var selection = annotations.GetSelection();
        _ = await annotations.CreateAsync(Square, page);
        var omitted = await selection.GetRefsAsync();
        _ = await annotations.CreateAsync(Square, page, new AnnotationCreateOptions { Select = false });
        var unselected = await selection.GetRefsAsync();
        var selected = await annotations.CreateAsync(Square, page, new AnnotationCreateOptions { Select = true });
        Assert.Empty(omitted);
        Assert.Empty(unselected);
        Assert.Equal([selected], await selection.GetRefsAsync());
    }

    [Fact]
    public async Task CreateFromSelectionLeftOutClearsTheTextSelection()
    {
        var (viewer, document, annotations) = await Opened("report", selection: true);
        await using var _v = viewer;
        var page = await FirstPage(viewer, document);
        using var selection = await viewer.GetSelectionAsync(document);
        await selection.EnsureLoadedAsync(page);
        using var drafts = annotations.GetDrafts();
        await selection.SelectPageAsync(page);
        Assert.NotEmpty(await drafts.CreateFromSelectionAsync(ToolId.Highlight));
        var omitted = await selection.HasSelectionAsync();
        await selection.SelectPageAsync(page);
        _ = await drafts.CreateFromSelectionAsync(ToolId.Highlight, clear: true);
        var cleared = await selection.HasSelectionAsync();
        await selection.SelectPageAsync(page);
        _ = await drafts.CreateFromSelectionAsync(ToolId.Highlight, clear: false);
        var kept = await selection.HasSelectionAsync();
        Assert.Equal(new[] { false, false, true }, new[] { omitted, cleared, kept });
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        var (viewer, document, annotations) = await Opened();
        await using var _v = viewer;
        await viewer.CloseAsync(document);
        var error = await Assert.ThrowsAsync<PdfException>(() => annotations.ListAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
        Assert.Equal("PluginError", error.Name);
    }

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(0, -1)]
    public async Task ATextSelectionOutsideUInt32IsInvalidInput(int start, int end)
    {
        var (viewer, _, annotations) = await Opened();
        await using var _v = viewer;
        var annotation = (await annotations.ListAsync())[0].AnnotationRef;
        using var drafts = annotations.GetDrafts();
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => drafts.SetTextSelectionAsync(annotation, start, end))).Code);
    }

    [Fact]
    public async Task ASubCapabilityHeldAloneKeepsTheViewerAndReleasesIt()
    {
        var (weak, tools) = await HoldTools();
        Assert.NotEmpty(await tools.GetToolsAsync());
        tools.Dispose();
        tools = null!;
        for (var i = 0; i < 10 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(20);
        }
        Assert.False(weak.IsAlive);
    }

    static async Task<(WeakReference, AnnotationToolsCapability)> HoldTools()
    {
        var (viewer, _, annotations) = await Opened();
        return (new WeakReference(viewer), annotations.GetTools());
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, _, annotations) = await Opened();
        await using var _v = viewer;
        using var stop = new CancellationTokenSource();
        var reading = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in annotations.ObserveEvents(stop.Token)) { }
            }
            catch (OperationCanceledException)
            {
            }
        });
        await Task.Delay(20);
        await stop.CancelAsync();
        await reading.WaitAsync(TimeSpan.FromSeconds(5));
        await annotations.RefreshAsync();
    }

    [Fact]
    public async Task EveryCancellableMemberCancelsBeforeItStarts()
    {
        var (viewer, _, annotations) = await Opened();
        await using var _v = viewer;
        var first = (await annotations.ListAsync())[0];
        var (annotation, page) = (first.AnnotationRef, first.Page);
        var before = await annotations.GetAsync(annotation);
        var count = (await annotations.ListAsync()).Count;
        using var selection = annotations.GetSelection();
        using var drafts = annotations.GetDrafts();
        using var threads = annotations.GetThreads();
        await selection.SelectAsync([annotation]);
        var png = Fixture.Data("tiny.png");
        var source = new AnnotationStampSource(png) { MimeType = "image/png" };
        Func<Task>[] calls =
        [
            () => annotations.RefreshAsync(Cancelled),
            () => annotations.WhenSyncedAsync(Cancelled),
            () => annotations.CreateAsync("""{"subtype": "square", "box": {"x": 0, "y": 0, "width": 10, "height": 10}}""", page, null, Cancelled),
            () => annotations.CreateAsync(new SquareAnnotationDraft(new PdfRect(0, 0, 10, 10)), page, null, Cancelled),
            () => annotations.UpdateAsync(annotation, """{"contents": "x"}""", Cancelled),
            () => annotations.UpdateAsync(annotation, """{"rotation": 90}""", Cancelled),
            () => annotations.ReorderAsync([annotation], new AnnotationPosition.End(), Cancelled),
            () => annotations.DeleteAsync(annotation, Cancelled),
            () => annotations.ExportAsync(null, null, Cancelled),
            () => annotations.ImportAsync("{}", "{}", Cancelled),
            () => selection.UpdateAsync("""{"opacity": 0.5}""", Cancelled),
            () => selection.UpdateAsync("""{"hidden": true}""", Cancelled),
            () => selection.DeleteAsync(Cancelled),
            () => selection.UpdateLinkAsync(null, Cancelled),
            () => selection.RotateByAsync(90, Cancelled),
            () => selection.ResetRotationAsync(Cancelled),
            () => selection.ToggleAsync(AnnotationTextFormat.Bold, Cancelled),
            () => selection.GroupAsync(Cancelled),
            () => selection.UngroupAsync(Cancelled),
            () => drafts.FinishCreationDraftAsync(Cancelled),
            () => drafts.EndTextEditAsync(Cancelled),
            () => drafts.CreateFromSelectionAsync(ToolId.Highlight, true, Cancelled),
            () => drafts.ArmStampAsync(source, null, Cancelled),
            () => drafts.PlaceStampAsync(source, new AnnotationStampPlacement(page, new PdfPoint(50, 50)), null, Cancelled),
            () => drafts.CreateAttachmentAsync(new AnnotationAttachmentFile(png, "tiny.png"), page, new PdfPoint(50, 50), Cancelled),
            () => drafts.ReadAttachmentAsync(annotation, Cancelled),
            () => threads.ReplyAsync(annotation, "x", Cancelled),
            () => threads.SetTextAsync(annotation, "x", Cancelled),
            () => threads.SetStatusAsync(annotation, AnnotationThreadStatus.Accepted, Cancelled),
            () => threads.SetMarkedAsync(annotation, true, Cancelled),
            () => threads.DeleteAsync(annotation, Cancelled),
            () => threads.DeleteThreadAsync(annotation, Cancelled),
            () => threads.SetLinkAsync(annotation, """{"kind": "uri", "uri": "https://example.invalid"}""", Cancelled),
            () => threads.ClearLinkAsync(annotation, Cancelled),
        ];
        foreach (var call in calls) await Assert.ThrowsAnyAsync<OperationCanceledException>(call);
        Assert.Equal(count, (await annotations.ListAsync()).Count);
        Assert.Equal(before?.FieldsJson, (await annotations.GetAsync(annotation))?.FieldsJson);
        Assert.Equal([annotation], await selection.GetRefsAsync());
        Assert.False(await drafts.HasArmedStampAsync());
    }

    [Fact]
    public async Task ANaNDraftNeverReachesTheEngine()
    {
        var (viewer, document, annotations) = await Opened("report");
        await using var _v = viewer;
        var page = await FirstPage(viewer, document);
        var error = await Assert.ThrowsAsync<PdfException>(() => annotations.CreateAsync(new InkAnnotationDraft([[new(double.NaN, 0)]]), page));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        Assert.Empty(await annotations.ListAsync());
    }
}
