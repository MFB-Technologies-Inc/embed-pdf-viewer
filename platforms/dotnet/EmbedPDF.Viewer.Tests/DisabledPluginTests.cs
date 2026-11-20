using System.Reflection;
using uniffi.epdf_viewer_host;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// A plugin left out of <see cref="PdfViewerOptions"/> is refused at its
/// accessor, before any call on a capability: the host asks the kernel
/// whether the plugin is installed ahead of any document lookup and answers
/// <c>unsupported</c>. One row per accessor;
/// <see cref="EveryPluginInTheOptionsHasARow"/> fails when the options gain a
/// plugin without one.
/// </summary>
/// <remarks>
/// Ported plugins' rows call the public accessor; the others call the
/// generated accessors through <c>PdfViewer.CallAsync</c> until their Phase 3
/// wave replaces them.
/// </remarks>
public sealed class DisabledPluginTests
{
    internal sealed record Accessor(
        string Field,
        Func<PdfViewerOptions, PdfViewerOptions> Prerequisites,
        Func<PdfViewerOptions, PdfViewerOptions> Install,
        Func<PdfViewer, DocumentInfo, Task> Call)
    {
        public async Task<(PdfViewer Viewer, DocumentInfo Document)> Viewer(bool installed)
        {
            var options = Prerequisites(new PdfViewerOptions());
            if (installed) options = Install(options);
            var viewer = await PdfViewer.CreateAsync(options);
            return (viewer, await viewer.OpenAsync(Fixture.Data("report")));
        }
    }

    static Func<PdfViewer, DocumentInfo, Task> Of<T>(Func<ViewerHost, string, Task<T>> accessor) where T : IDisposable =>
        async (viewer, document) => (await viewer.CallAsync(h => accessor(h, document.Id), default)).Dispose();

    static Func<PdfViewer, DocumentInfo, Task> Public<T>(Func<PdfViewer, DocumentInfo, Task<T>> accessor) where T : IDisposable =>
        async (viewer, document) => (await accessor(viewer, document)).Dispose();

    static Func<PdfViewer, DocumentInfo, Task> Global<T>(Func<ViewerHost, Task<T>> accessor) where T : IDisposable =>
        async (viewer, _) => (await viewer.CallAsync(accessor, default)).Dispose();

    static readonly Func<PdfViewerOptions, PdfViewerOptions> None = o => o;
    static readonly Func<PdfViewerOptions, PdfViewerOptions> Interaction = o => o with { Interaction = true };
    static readonly Func<PdfViewerOptions, PdfViewerOptions> Annotation = o => o with { Interaction = true, Annotation = "" };

    internal static readonly IReadOnlyList<Accessor> Accessors =
    [
        new("Metadata", None, o => o with { Metadata = true }, Public((v, d) => v.GetMetadataAsync(d))),
        new("Search", None, o => o with { Search = new() }, Public((v, d) => v.GetSearchAsync(d))),
        new("Render", None, o => o with { Render = new() }, Public((v, d) => v.GetRenderAsync(d))),
        new("Interaction", None, o => o with { Interaction = true }, Public((v, d) => v.GetInteractionAsync(d))),
        new("Feedback", None, o => o with { Feedback = new() }, Public((v, _) => v.GetFeedbackAsync())),
        new("Stage", None, o => o with { Stage = new() }, Public((v, d) => v.GetStageAsync(d))),
        new("Selection", Interaction, o => o with { Selection = true }, Public((v, d) => v.GetSelectionAsync(d))),
        new("Link", Interaction, o => o with { Link = true }, Public((v, d) => v.GetLinkAsync(d))),
        new("Annotation", Interaction, o => o with { Annotation = "" }, Of((h, id) => h.Annotation(id))),
        new("History", None, o => o with { History = true }, Public((v, d) => v.GetHistoryAsync(d))),
        new("Outline", None, o => o with { Outline = true }, Public((v, d) => v.GetOutlineAsync(d))),
        new("Attachment", None, o => o with { Attachment = true }, Public((v, d) => v.GetAttachmentsAsync(d))),
        new("Form", Interaction, o => o with { Form = new() }, Of((h, id) => h.Form(id))),
        new("Redaction", Annotation, o => o with { Redaction = new() }, Of((h, id) => h.Redaction(id))),
        new("Measurement", Annotation, o => o with { Measurement = new() }, Of((h, id) => h.Measurement(id))),
        new("Stamp", None, o => o with { Stamp = new() }, Global(h => h.Stamp())),
        new("PageEdit", None, o => o with { PageEdit = true }, Public((v, d) => v.GetPageEditAsync(d))),
        new("ViewManager", None, o => o with { ViewManager = true }, Public((v, _) => v.GetViewManagerAsync())),
        new("Shell", None, o => o with { Shell = true }, Public((v, d) => v.GetShellAsync(d))),
        new("I18n", None, o => o with { I18n = new() }, Public((v, _) => v.GetI18nAsync())),
        new("Commands", None, o => o with { Commands = true }, Public((v, _) => v.GetCommandsAsync())),
        new("Actions", None, o => o with { Actions = new() }, Public((v, d) => v.GetActionsAsync(d))),
        new("Signature", o => o with { Interaction = true, Form = new() }, o => o with { Signature = new() }, Of((h, id) => h.Signature(id))),
    ];

    public static TheoryData<string> Fields => [.. Accessors.Select(a => a.Field)];

    internal static Accessor Row(string field) => Accessors.Single(a => a.Field == field);

    static async Task ExpectUnsupported(Func<Task> call) =>
        Assert.Equal(PdfErrorCode.Unsupported, (await Assert.ThrowsAsync<PdfException>(call)).Code);

    [Fact]
    public void EveryPluginInTheOptionsHasARow()
    {
        var fields = typeof(PdfViewerOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            // Not plugins: the permission scope, the lists the commands plugin
            // takes, and the viewer's own services.
            .Except(["Scope", "StandardCommands", "DocumentFetcher", "Logger"]);
        Assert.Equal(fields.Order(), Accessors.Select(a => a.Field).Order());
        Assert.Equal(Accessors.Count, Accessors.Select(a => a.Field).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Fields))]
    public async Task APluginLeftOutIsUnsupportedAtTheAccessor(string field)
    {
        var accessor = Row(field);
        var (viewer, document) = await accessor.Viewer(installed: false);
        await using var _ = viewer;
        await ExpectUnsupported(() => accessor.Call(viewer, document));
    }

    [Theory]
    [MemberData(nameof(Fields))]
    public async Task AnInstalledPluginsAccessorSucceeds(string field)
    {
        var accessor = Row(field);
        var (viewer, document) = await accessor.Viewer(installed: true);
        await using var _ = viewer;
        await accessor.Call(viewer, document);
    }

    /// <summary>The refusal does not depend on the document: a closed one is still <c>unsupported</c>, not <c>not-found</c>.</summary>
    [Theory]
    [MemberData(nameof(Fields))]
    public async Task APluginLeftOutIsUnsupportedForAClosedDocument(string field)
    {
        var accessor = Row(field);
        var (viewer, document) = await accessor.Viewer(installed: false);
        await using var _ = viewer;
        await viewer.CloseAsync(document);
        await ExpectUnsupported(() => accessor.Call(viewer, document));
    }

    /// <summary>A document still loading is <c>unsupported</c>, not <c>not-ready</c>, when the plugin is left out.</summary>
    [Theory]
    [MemberData(nameof(Fields))]
    public async Task APluginLeftOutIsUnsupportedForALoadingDocument(string field)
    {
        var accessor = Row(field);
        await using var server = Loopback.Start(_ => Task.Delay(Timeout.Infinite));
        await using var viewer = await PdfViewer.CreateAsync(accessor.Prerequisites(new PdfViewerOptions()));
        using var cancellation = new CancellationTokenSource();
        var opening = viewer.OpenAsync(server.Url("held.pdf"), cancellationToken: cancellation.Token);
        DocumentInfo? loading = null;
        for (var i = 0; i < 500 && loading is null; i++)
        {
            loading = (await viewer.GetDocumentsAsync()).FirstOrDefault();
            if (loading is null) await Task.Delay(10);
        }
        Assert.NotNull(loading);
        Assert.Equal(DocumentStatus.Loading, loading.Status);
        await ExpectUnsupported(() => accessor.Call(viewer, loading));
        cancellation.Cancel();
        _ = await Record.ExceptionAsync(() => opening);
    }
}

public sealed partial class ViewerConfigBuilderTests
{
    /// <summary>The accessors <see cref="DisabledPluginTests"/> checks; here the options come from the builder.</summary>
    [Theory]
    [MemberData(nameof(DisabledPluginTests.Fields), MemberType = typeof(DisabledPluginTests))]
    public async Task APluginTheBuilderLeavesOutIsUnsupported(string field)
    {
        var accessor = DisabledPluginTests.Row(field);
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptionsBuilder(accessor.Prerequisites(new PdfViewerOptions())).Build());
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        Assert.Equal(PdfErrorCode.Unsupported, (await Assert.ThrowsAsync<PdfException>(() => accessor.Call(viewer, document))).Code);
    }

    [Theory]
    [MemberData(nameof(DisabledPluginTests.Fields), MemberType = typeof(DisabledPluginTests))]
    public async Task APluginTheBuilderInstallsHasAnAccessor(string field)
    {
        var accessor = DisabledPluginTests.Row(field);
        var row = Rows.Single(r => r.Field == field);
        await using var viewer = await PdfViewer.CreateAsync(row.Set(new PdfViewerOptionsBuilder(accessor.Prerequisites(new PdfViewerOptions()))).Build());
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        await accessor.Call(viewer, document);
    }
}
