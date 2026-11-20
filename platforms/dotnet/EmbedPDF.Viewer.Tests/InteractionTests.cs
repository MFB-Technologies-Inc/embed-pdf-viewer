using System.Runtime.CompilerServices;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Records what reaches it. Captures on down unless <c>throwsOnDown</c>.</summary>
sealed class RecordingHandler(Exception? throwsOnDown = null, Action? onDownHook = null) : IInteractionHandler
{
    readonly Lock gate = new();
    readonly List<string> log = [];

    public IReadOnlyList<string> Entries
    {
        get { lock (gate) return [.. log]; }
    }

    public bool EnabledFor(InteractionTool tool) => true;

    public bool OnDown(PointerSample sample)
    {
        lock (gate) log.Add("down");
        onDownHook?.Invoke();
        if (throwsOnDown is not null) throw throwsOnDown;
        return true;
    }

    public void OnMove(PointerSample sample) { lock (gate) log.Add("move"); }

    public void OnUp(PointerSample sample) { lock (gate) log.Add("up"); }

    public void OnHover(PointerSample sample) { lock (gate) log.Add("hover"); }
}

static class Pointer
{
    public static PointerSample Sample(PointerPhase phase, PageRef page, double x = 10) =>
        new(phase, new PdfPoint(x, 10), new PointerModifiers())
        {
            Page = new PointerPageHit(page, new PdfPoint(x, 10)),
            PointerType = PointerKind.Touch,
        };
}

public sealed class InteractionTests
{
    static async Task<(PdfViewer Viewer, DocumentInfo Document, InteractionCapability Interaction, PageRef Page)> Opened()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        return (viewer, document, await viewer.GetInteractionAsync(document), page);
    }

    static async Task<T> First<T>(IAsyncEnumerable<InteractionEvent> events) where T : InteractionEvent
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var e in events.WithCancellation(timeout.Token))
        {
            if (e is T found) return found;
        }
        throw new InvalidOperationException("the stream ended first");
    }

    [Fact]
    public async Task ActivatingAToolIsObserved()
    {
        var (viewer, _, interaction, _) = await Opened();
        await using var _v = viewer;
        Assert.Equal(["pointer", "pan", "zoom"], (await interaction.GetToolsAsync()).Select(t => t.Id.Value));
        var events = interaction.ObserveEvents();
        await interaction.ActivateToolAsync(ToolId.Pan);
        var changed = await First<InteractionEvent.ToolChanged>(events);
        Assert.Equal(ToolId.Pan, changed.ToolId);
        Assert.Equal(ToolId.Pointer, changed.PreviousToolId);
        Assert.Equal(ToolId.Pan, await interaction.GetActiveToolIdAsync());
    }

    [Fact]
    public async Task AnUnknownToolThrowsNotFound()
    {
        var (viewer, _, interaction, _) = await Opened();
        await using var _v = viewer;
        var error = await Assert.ThrowsAsync<PdfException>(() => interaction.ActivateToolAsync("laser"));
        Assert.Equal(PdfErrorCode.NotFound, error.Code);
    }

    [Fact]
    public async Task AHandlerCapturesAGesture()
    {
        var (viewer, _, interaction, page) = await Opened();
        await using var _v = viewer;
        var handler = new RecordingHandler();
        using var registration = await interaction.RegisterHandlerAsync(handler, "recording", 5);
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Down, page));
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Move, page, 20));
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Up, page, 30));
        // A reply after the dispatches means they have run.
        await interaction.GetCursorAsync();
        Assert.Equal(["down", "move", "up"], handler.Entries);
    }

    [Fact]
    public async Task AThrowingHandlerReportsAndFails()
    {
        var (viewer, _, interaction, page) = await Opened();
        await using var _v = viewer;
        var handler = new RecordingHandler(new PdfException("refused"));
        using var registration = await interaction.RegisterHandlerAsync(handler, "throws", 5);
        var errors = viewer.ObserveReportedErrors();
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Down, page));
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Move, page, 20));
        Assert.Equal("HostError", (await FirstError(errors)).Name);
        await interaction.GetCursorAsync();
        // Not captured: the move hovered.
        Assert.Equal(["down", "hover"], handler.Entries);
    }

    [Fact]
    public async Task AHandlerThatThrowsAnyExceptionReportsHostError()
    {
        var (viewer, _, interaction, page) = await Opened();
        await using var _v = viewer;
        var handler = new RecordingHandler(new InvalidOperationException("not a host error"));
        using var registration = await interaction.RegisterHandlerAsync(handler, "throws", 5);
        var errors = viewer.ObserveReportedErrors();
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Down, page));
        var reported = await FirstError(errors);
        Assert.Equal("HostError", reported.Name);
        Assert.Contains("not a host error", reported.Detail);
    }

    static async Task<PdfException> FirstError(IAsyncEnumerable<PdfException> errors)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var error in errors.WithCancellation(timeout.Token)) return error;
        throw new InvalidOperationException("no error");
    }

    sealed class ThrowsOnMove : IInteractionHandler
    {
        public int Downs;
        public bool EnabledFor(InteractionTool tool) => true;
        public bool OnDown(PointerSample sample) { Interlocked.Increment(ref Downs); return true; }
        public void OnMove(PointerSample sample) => throw new InvalidOperationException("move");
        public void OnUp(PointerSample sample) => throw new InvalidOperationException("up");
        public bool ClaimsTouch(PointerSample sample) => throw new InvalidOperationException("claims");
    }

    [Fact]
    public async Task AHandlerThrowingFromAMemberWithNoErrorLeavesTheKernelWorking()
    {
        var (viewer, _, interaction, page) = await Opened();
        await using var _v = viewer;
        var handler = new ThrowsOnMove();
        using var registration = await interaction.RegisterHandlerAsync(handler, "throws-on-move", 5);
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Down, page));
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Move, page, 20));
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Up, page, 30));
        Assert.False(await interaction.WouldClaimTouchAsync(Pointer.Sample(PointerPhase.Down, page)));
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Down, page));
        Assert.Equal("default", await interaction.GetCursorAsync());
        Assert.Equal(2, Volatile.Read(ref handler.Downs));
    }

    [Fact]
    public async Task AHandlerThatCallsBackIntoTheViewerDoesNotDeadlock()
    {
        var (viewer, _, interaction, page) = await Opened();
        await using var _v = viewer;
        var answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler(onDownHook: () => _ = Task.Run(async () =>
        {
            try { answer.TrySetResult(await interaction.GetCursorAsync()); }
            catch (Exception) { answer.TrySetResult("failed"); }
        }));
        using var registration = await interaction.RegisterHandlerAsync(handler, "reenters", 5);
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Down, page));
        Assert.Equal("default", await answer.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task DisposingTheViewerFromAHandlerThrowsInvalidOperation()
    {
        var (viewer, _, interaction, page) = await Opened();
        await using var _v = viewer;
        var thrown = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler(onDownHook: () =>
        {
            try { viewer.Dispose(); thrown.TrySetResult(null); }
            catch (Exception error) { thrown.TrySetResult(error); }
        });
        using var registration = await interaction.RegisterHandlerAsync(handler, "disposes", 5);
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Down, page));
        Assert.IsType<InvalidOperationException>(await thrown.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal("default", await interaction.GetCursorAsync());
    }

    [Fact]
    public async Task DisposeAsyncFromAHandlerDisposesTheViewer()
    {
        var (viewer, _, interaction, page) = await Opened();
        var started = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler(onDownHook: () => started.TrySetResult(viewer.DisposeAsync().AsTask()));
        await interaction.RegisterHandlerAsync(handler, "disposes", 5);
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Down, page));
        await (await started.Task.WaitAsync(TimeSpan.FromSeconds(10))).WaitAsync(TimeSpan.FromSeconds(10));
        var error = await Assert.ThrowsAsync<PdfException>(() => interaction.GetCursorAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
    }

    [Fact]
    public async Task UnregisteringATool()
    {
        var (viewer, _, interaction, _) = await Opened();
        await using var _v = viewer;
        await interaction.RegisterToolAsync(new InteractionTool(ToolId.Ink, "crosshair", ["annotation-create"]));
        Assert.True(await interaction.HasToolAsync(ToolId.Ink));
        var registration = await interaction.RegisterToolAsync(new InteractionTool(ToolId.Ink, "crosshair", []), replace: true);
        registration.Unregister();
        await interaction.GetCursorAsync();
        Assert.False(await interaction.HasToolAsync(ToolId.Ink));
    }

    [Fact]
    public async Task APermissionDecidesWhetherAToolCanBeUsed()
    {
        var (viewer, _, interaction, _) = await Opened();
        await using var _v = viewer;
        var allowed = false;
        await interaction.RegisterToolAsync(new InteractionTool("laser", "crosshair", []), permission: () => allowed);
        Assert.False(await interaction.CanUseToolAsync("laser"));
        allowed = true;
        Assert.True(await interaction.CanUseToolAsync("laser"));
        await interaction.RegisterToolAsync(new InteractionTool("broken", "crosshair", []),
            permission: () => throw new InvalidOperationException("broken"));
        Assert.False(await interaction.CanUseToolAsync("broken"));
        Assert.True(await interaction.CanUseToolAsync(ToolId.Pan));
        Assert.False(await interaction.CanUseToolAsync("nobody"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static async Task<WeakReference> RegisterAndUnregisterAHandlerThatHoldsTheCapability()
    {
        var (viewer, _, interaction, _) = await Opened();
        var handler = new RecordingHandler(onDownHook: () => GC.KeepAlive(interaction));
        var registration = await interaction.RegisterHandlerAsync(handler, "holds", 1);
        registration.Unregister();
        // A reply after the unregister means it has run.
        await interaction.GetCursorAsync();
        await viewer.DisposeAsync();
        return new WeakReference(viewer);
    }

    [Fact]
    public async Task UnregisteringAHandlerThatHoldsTheCapabilityReleasesTheViewer()
    {
        var released = await RegisterAndUnregisterAHandlerThatHoldsTheCapability();
        for (var i = 0; i < 20 && released.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(10);
        }
        Assert.False(released.IsAlive);
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        var (viewer, document, interaction, _) = await Opened();
        await using var _v = viewer;
        await viewer.CloseAsync(document);
        var error = await Assert.ThrowsAsync<PdfException>(() => interaction.GetActiveToolIdAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, _, interaction, _) = await Opened();
        await using var _v = viewer;
        using (var cancel = new CancellationTokenSource())
        {
            var reading = Task.Run(async () => { await foreach (var _ in interaction.ObserveEvents(cancel.Token)) { } });
            await Task.Delay(20);
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
        }
        await interaction.ActivateToolAsync(ToolId.Pan);
    }

    [Fact]
    public async Task SettingsRoundTrip()
    {
        var (viewer, _, interaction, _) = await Opened();
        await using var _v = viewer;
        Assert.Equal(ToolId.Pointer, (await interaction.GetSettingsAsync()).DefaultTool);
        await interaction.UpdateSettingsAsync(new InteractionSettingsPatch { DefaultTool = ToolId.Pan });
        Assert.Equal(ToolId.Pan, await interaction.GetDefaultToolIdAsync());
        await interaction.ResetSettingsAsync();
        Assert.Equal(ToolId.Pointer, (await interaction.GetSettingsAsync()).DefaultTool);
    }

    [Fact]
    public async Task ToolStackAndCursors()
    {
        var (viewer, _, interaction, _) = await Opened();
        await using var _v = viewer;
        await interaction.PushToolAsync(ToolId.Pan);
        var pan = await interaction.GetActiveToolAsync();
        Assert.Equal(ToolId.Pan, pan.Id);
        Assert.NotEmpty(pan.Enables);
        Assert.True(await interaction.ActiveToolEnablesAsync(pan.Enables[0]));
        Assert.False(await interaction.ActiveToolEnablesAsync("no-such-behavior"));
        await interaction.PopToolAsync();
        Assert.Equal(ToolId.Pointer, await interaction.GetActiveToolIdAsync());
        Assert.NotNull(await interaction.GetToolAsync(ToolId.Zoom));
        Assert.Null(await interaction.GetToolAsync("nobody"));
        await interaction.SetToolCursorAsync(ToolId.Pointer, new Dictionary<string, string> { ["default"] = "crosshair" });
        await interaction.SetToolCursorAsync(ToolId.Pointer, null);
        await interaction.ClaimCursorAsync("hover", "pointer", 1);
        Assert.Equal("pointer", await interaction.GetCursorAsync());
        await interaction.ClaimCursorAsync("hover", null);
        Assert.False(await interaction.HasCursorClaimAsync());
        await interaction.ActivateToolAsync(ToolId.Pan);
        await interaction.ActivateDefaultToolAsync();
        Assert.Equal(ToolId.Pointer, await interaction.GetActiveToolIdAsync());
    }
}

public sealed class ToolIdTests
{
    [Fact]
    public void NamedToolsAreDistinctAndInReferenceOrder()
    {
        Assert.Equal(37, ToolId.Named.Count);
        Assert.Equal(37, ToolId.Named.Distinct().Count());
        Assert.Equal(ToolId.Pointer, ToolId.Named[0]);
        Assert.Equal("attachment", ToolId.Named[^1].Value);
        Assert.Equal("free-text-callout", ToolId.FreeTextCallout.Value);
    }

    [Fact]
    public void AStringIsATool()
    {
        ToolId pan = "pan";
        Assert.Equal(ToolId.Pan, pan);
        Assert.Equal("laser", new ToolId("laser").ToString());
    }
}
