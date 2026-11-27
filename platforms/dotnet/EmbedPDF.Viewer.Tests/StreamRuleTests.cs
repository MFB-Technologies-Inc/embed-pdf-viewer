using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class StreamRuleTests
{
    /// <summary>Throws from every pointer down, which the kernel reports as a <c>HostError</c>.</summary>
    sealed class ThrowingHandler : IInteractionHandler
    {
        public bool EnabledFor(InteractionTool tool) => true;
        public bool OnDown(PointerSample sample) => throw new PdfException("refused");
    }

    internal sealed record Reporting(PdfViewer Viewer, InteractionCapability Interaction, PageRef Page) : IAsyncDisposable
    {
        public Registration Registration { get; init; } = null!;

        public void Down() => Interaction.DispatchPointer(Pointer.Sample(PointerPhase.Down, Page));

        public async ValueTask DisposeAsync()
        {
            Interaction.Dispose();
            await Viewer.DisposeAsync();
        }
    }

    /// <summary>A viewer whose throwing handler reports one error per pointer down.</summary>
    internal static async Task<Reporting> ViewerThatReportsWithRegistration()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var interaction = await viewer.GetInteractionAsync(document);
        var registration = await interaction.RegisterHandlerAsync(new ThrowingHandler(), "throws", 5);
        return new Reporting(viewer, interaction, page) { Registration = registration };
    }

    internal static async Task<Reporting> ViewerThatReports()
    {
        var reporting = await ViewerThatReportsWithRegistration();
        // Releasing the handle without unregistering leaves the handler installed.
        reporting.Registration.Handle.Dispose();
        return reporting;
    }

    static async Task<List<string>> Take(int count, IAsyncEnumerable<PdfException> stream)
    {
        var names = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var error in stream.WithCancellation(timeout.Token))
        {
            names.Add(error.Name);
            if (names.Count == count) break;
        }
        return names;
    }

    [Fact]
    public async Task EveryReportedErrorObserverReceivesEveryError()
    {
        await using var reporting = await ViewerThatReports();
        var first = Take(3, reporting.Viewer.ObserveReportedErrors());
        var second = Take(3, reporting.Viewer.ObserveReportedErrors());
        for (var i = 0; i < 3; i++) reporting.Down();
        Assert.Equal(["HostError", "HostError", "HostError"], await first);
        Assert.Equal(await first, await second);
    }

    [Fact]
    public async Task CancellingOneErrorObserverLeavesTheOthersLiveAndDisposeFinishesThem()
    {
        var reporting = await ViewerThatReports();
        var survivor = Take(1, reporting.Viewer.ObserveReportedErrors());
        using (var cancellation = new CancellationTokenSource())
        {
            var cancelled = Task.Run(async () => { await foreach (var _ in reporting.Viewer.ObserveReportedErrors(cancellation.Token)) { } });
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        }
        reporting.Down();
        Assert.Equal(["HostError"], await survivor);
        await reporting.DisposeAsync();
        await using var late = reporting.Viewer.ObserveReportedErrors().GetAsyncEnumerator();
        Assert.False(await late.MoveNextAsync());
    }
}
