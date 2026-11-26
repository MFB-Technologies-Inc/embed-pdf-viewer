using System.Globalization;
using System.Runtime.CompilerServices;
using EmbedPDF.Models;

namespace EmbedPDF.Models.Tests;

/// <summary>
/// The example in <c>platforms/dotnet/README.md</c>, "Binding the models to a
/// UI". <see cref="Example"/> is the README's code block line for line, which
/// <see cref="TheReadmeBlockIsTheExample"/> checks, so the two cannot drift.
/// It runs on a UI-like thread and with no context at all. The example writes
/// to <see cref="Console"/>, so these tests run alone.
/// </summary>
[Collection(nameof(UnobservedWatchTests))]
public class ReadmeExampleTests
{
    static async Task Example(string path)
    {
        // README begin
        // On the UI thread, so the models' notifications arrive there.
        await using var reader = new PdfReaderModel(ReaderFeatures.Search | ReaderFeatures.Links);
        await reader.OpenAsync(File.ReadAllBytes(path));
        if (reader.Phase is not ReaderPhase.Ready ready) return;

        var pages = ready.Session.Reader;
        using var shown = new CancellationTokenSource();
        var following = pages.RunAsync(shown.Token); // for as long as the view shows the document
        pages.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StageReaderModel.VisiblePages)) Console.WriteLine($"{pages.VisiblePages.Count} pages in view");
        };

        // What the view's own events turn into.
        await pages.SizeChangedAsync(800, 600, displayScale: 2);
        await pages.DragChangedAsync(0, -120);
        await pages.DragEndedAsync(0, -900);
        pages.Tapped(new PointerInput(400, 300) { Kind = PointerKind.Mouse });

        var search = ready.Session.Search!;
        search.Text = "the";
        await search.SubmitSearchAsync();
        Console.WriteLine(search.Summary); // "1 of 985"

        shown.Cancel();
        await following;
        // README end
    }

    static async Task<string> RunExampleAsync()
    {
        var output = new StringWriter();
        var previous = Console.Out;
        Console.SetOut(TextWriter.Synchronized(output));
        try { await Example(Fixture.Path("report")); }
        finally { Console.SetOut(previous); }
        return output.ToString();
    }

    static void AssertRan(string output)
    {
        Assert.Contains("pages in view", output);
        Assert.Contains($"1 of {985.ToString("N0", CultureInfo.CurrentCulture)}", output);
    }

    [Fact]
    public Task TheReadmeExampleRunsOnAUiThread() => SingleThreadContext.RunWatched(async () => AssertRan(await RunExampleAsync()));

    /// <summary>With no context the models make the private serial one, as in a console app.</summary>
    [Fact]
    public async Task TheReadmeExampleRunsWithNoContext()
    {
        using var unobserved = new UnobservedWatch();
        AssertRan(await Task.Run(RunExampleAsync));
        unobserved.Collect();
        Assert.True(unobserved.Seen.IsEmpty);
    }

    [Fact]
    public void TheReadmeBlockIsTheExample() => Assert.Equal(ExampleLines(), ReadmeLines());

    static List<string> ReadmeLines()
    {
        var readme = File.ReadAllText(Path.Combine(RepositoryRoot(), "platforms/dotnet/README.md"));
        var section = readme[readme.IndexOf("## Binding the models to a UI", StringComparison.Ordinal)..];
        var start = section.IndexOf("```csharp\n", StringComparison.Ordinal) + "```csharp\n".Length;
        var block = section[start..section.IndexOf("\n```", start, StringComparison.Ordinal)];
        // The block's namespace directives are the test file's own.
        return Normalize(block.Split('\n').Where(line => !System.Text.RegularExpressions.Regex.IsMatch(line, @"^using [\w.]+;$")));
    }

    static List<string> ExampleLines([CallerFilePath] string source = "")
    {
        var text = File.ReadAllText(source);
        var start = text.IndexOf("// README begin", StringComparison.Ordinal);
        var end = text.IndexOf("// README end", start, StringComparison.Ordinal);
        return Normalize(text[start..end].Split('\n').Skip(1));
    }

    static List<string> Normalize(IEnumerable<string> lines) =>
        lines.Select(line => line.Trim()).Where(line => line.Length > 0).ToList();

    static string RepositoryRoot([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "../../.."));
}
