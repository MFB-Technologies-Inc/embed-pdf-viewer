using Xunit;

namespace EmbedPDF.Viewer.Tests;

sealed class RecordingFeedback(Action? onSelection = null) : IPlatformFeedback
{
    readonly Lock gate = new();
    readonly List<string> calls = [];

    public IReadOnlyList<string> Calls
    {
        get { lock (gate) return [.. calls]; }
    }

    public void Selection()
    {
        lock (gate) calls.Add("selection");
        onSelection?.Invoke();
    }

    public void Impact(FeedbackImpactWeight? weight) { lock (gate) calls.Add($"impact {weight}"); }

    public void Notify(FeedbackNotifyKind kind) { lock (gate) calls.Add($"notify {kind}"); }
}

public sealed class FeedbackTests
{
    [Fact]
    public async Task FeedbackReachesTheProvider()
    {
        var provider = new RecordingFeedback();
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Feedback = new() { Provider = provider } });
        using var feedback = await viewer.GetFeedbackAsync();
        await feedback.SelectionAsync();
        await feedback.ImpactAsync(FeedbackImpactWeight.Heavy);
        await feedback.ImpactAsync();
        await feedback.NotifyAsync(FeedbackNotifyKind.Success);
        Assert.Equal(["selection", "impact Heavy", "impact ", "notify Success"], provider.Calls);
    }

    // Feedback has no event stream of its own (its effects reach the
    // provider), so it has no DroppingTheStreamUnsubscribes.

    [Fact]
    public async Task ADisposedViewersCapabilityThrows()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Feedback = new() { Provider = new RecordingFeedback() } });
        var feedback = await viewer.GetFeedbackAsync();
        await viewer.DisposeAsync();
        var error = await Assert.ThrowsAsync<PdfException>(() => feedback.SelectionAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
    }

    [Fact]
    public async Task WithoutFeedbackTheAccessorThrows()
    {
        await using var viewer = await PdfViewer.CreateAsync();
        await Assert.ThrowsAsync<PdfException>(() => viewer.GetFeedbackAsync());
    }

    [Fact]
    public async Task TheDefaultProviderIsSilent()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Feedback = new() });
        using var feedback = await viewer.GetFeedbackAsync();
        await feedback.SelectionAsync();
        await feedback.ImpactAsync(FeedbackImpactWeight.Light);
        await feedback.NotifyAsync(FeedbackNotifyKind.Error);
    }

    sealed class ThrowingFeedback : IPlatformFeedback
    {
        public void Selection() => throw new InvalidOperationException("no haptics");
        public void Impact(FeedbackImpactWeight? weight) => throw new InvalidOperationException("no haptics");
        public void Notify(FeedbackNotifyKind kind) => throw new InvalidOperationException("no haptics");
    }

    [Fact]
    public async Task AThrowingProviderLeavesTheCallsWorking()
    {
        var logger = new RecordingLogger();
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Feedback = new() { Provider = new ThrowingFeedback() }, Logger = logger });
        using var feedback = await viewer.GetFeedbackAsync();
        await feedback.SelectionAsync();
        await feedback.ImpactAsync(FeedbackImpactWeight.Medium);
        await feedback.NotifyAsync(FeedbackNotifyKind.Warning);
        await feedback.SelectionAsync();
        Assert.Equal(4, logger.Entries.Count(e => e.Error is InvalidOperationException));
    }

    [Fact]
    public async Task DisposingTheViewerFromTheProviderThrowsInvalidOperation()
    {
        Exception? thrown = null;
        PdfViewer? viewer = null;
        var provider = new RecordingFeedback(() =>
        {
            try { viewer!.Dispose(); }
            catch (Exception error) { thrown = error; }
        });
        viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Feedback = new() { Provider = provider } });
        await using var _ = viewer;
        using var feedback = await viewer.GetFeedbackAsync();
        await feedback.SelectionAsync();
        Assert.IsType<InvalidOperationException>(thrown);
        await feedback.NotifyAsync(FeedbackNotifyKind.Success);
    }
}
