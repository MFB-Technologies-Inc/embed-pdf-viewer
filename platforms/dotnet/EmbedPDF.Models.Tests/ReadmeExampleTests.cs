using System.Globalization;

namespace EmbedPDF.Models.Tests;

/// <summary>The example in <c>platforms/dotnet/README.md</c>, "Binding the models to a UI", run against the fixture.</summary>
public class ReadmeExampleTests
{
    [Fact]
    public Task TheReadmeExampleRuns() => SingleThreadContext.Run(async context =>
    {
        var lines = new List<string>();
        // On the UI thread, so the models' notifications arrive there.
        await using var reader = context.Watch(new PdfReaderModel(ReaderFeatures.Search | ReaderFeatures.Links));
        await reader.OpenAsync(Fixture.Data());
        if (reader.Phase is not ReaderPhase.Ready ready) throw new InvalidOperationException($"{reader.Phase}");

        var pages = context.Watch(ready.Session.Reader);
        using var shown = new CancellationTokenSource();
        var following = pages.RunAsync(shown.Token); // for as long as the view shows the document
        pages.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StageReaderModel.VisiblePages)) lines.Add($"{pages.VisiblePages.Count} pages in view");
        };

        // What the view's own events turn into.
        await pages.SizeChangedAsync(800, 600, displayScale: 2);
        await pages.DragChangedAsync(0, -120);
        await pages.DragEndedAsync(0, -900);
        pages.Tapped(new PointerInput(400, 300) { Kind = PointerKind.Mouse });

        var search = context.Watch(ready.Session.Search!);
        search.Text = "the";
        await search.SubmitSearchAsync();
        Assert.Equal($"1 of {985.ToString("N0", CultureInfo.CurrentCulture)}", search.Summary);

        shown.Cancel();
        await following;
        Assert.NotEmpty(lines);
        Assert.Null(pages.Error);
    });
}
