using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// Port of <c>APICoverageTests.swift</c>: reads and permission probes the
/// coverage manifest lists, exercised against the engine. That
/// <c>scripts/check-api-coverage.py --platform csharp</c> finds each member in
/// the built assembly is the static half; these are the behavioural half.
/// </summary>
public sealed class ApiCoverageTests
{
    static readonly CancellationToken Cancelled = new(true);

    [Fact]
    public async Task CustomMetadataPreservesOmittedKeysAndClearsExplicitNull()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Metadata = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var metadata = await viewer.GetMetadataAsync(document);
        await metadata.RefreshCustomAsync();
        _ = await metadata.UpdateCustomAsync(new Dictionary<string, string?> { ["Project"] = "Apple", ["Keep"] = "yes" });
        var changed = await metadata.UpdateCustomAsync(new Dictionary<string, string?> { ["Project"] = null });
        Assert.False(changed.TryGetValue("Project", out var project) && project is not null);
        Assert.Equal("yes", changed["Keep"]);
        await metadata.RefreshCustomAsync();
        var snapshot = await metadata.GetCustomSnapshotAsync();
        Assert.Equal("yes", snapshot?["Keep"]);
        Assert.Equal(ResourceStatus.Ready, await metadata.GetCustomStatusAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => metadata.RefreshCustomAsync(Cancelled));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => metadata.UpdateCustomAsync(new Dictionary<string, string?> { ["Cancelled"] = "yes" }, Cancelled));
        await viewer.CloseAsync(document);
        await Assert.ThrowsAsync<PdfException>(() => metadata.GetCustomSnapshotAsync());
    }

    [Fact]
    public async Task PageSelectionUsesStableRefsAcrossReorder()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { PageEdit = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var edit = await viewer.GetPageEditAsync(document);
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        await edit.SelectAsync([page]);
        _ = await edit.MovePagesAsync([page], new PageEditPlacement.End());
        Assert.Equal([page], await edit.GetSelectedAsync());
        Assert.True(await edit.CanExtractAsync());
    }

    [Fact]
    public async Task FormValidationAndPermissionReadsAreExposed()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Interaction = true, Form = new FormConfig { Validation = FormValidation.None },
        });
        var document = await viewer.OpenAsync(Fixture.Data("form_fields"));
        using var form = await viewer.GetFormAsync(document);
        await form.RefreshAsync();
        var validation = await form.ValidateAsync();
        Assert.Equal(validation.Missing.Count == 0, validation.Valid);
        Assert.True(await form.CanRestoreAttributionAsync());
        Assert.True(await form.CanWriteScriptsAsync());
    }

    [Fact]
    public async Task StageDestinationAndZoomReadsAreExposed()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Stage = new StageConfig() });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var stage = await viewer.GetStageAsync(document);
        await stage.SetViewportSizeAsync(new PdfSize(800, 600));
        Assert.Equal(document.PageCount, await stage.GetPageCountAsync());
        _ = await stage.CanZoomInAsync();
        _ = await stage.CanZoomOutAsync();
        if (await stage.GetCurrentDestinationAsync() is { } destination)
            await stage.GoToDestinationAsync(destination);
    }

    [Fact]
    public async Task DeniedScopeDoesNotAdvertiseNewPermissions()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Scope = ["doc.open"], Interaction = true, Link = true, Annotation = "", Form = new FormConfig(),
            Redaction = new RedactionConfig(), Stamp = new StampConfig(), PageEdit = true, Signature = new SignatureConfig(),
        });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using (var edit = await viewer.GetPageEditAsync(document)) Assert.False(await edit.CanExtractAsync());
        using (var form = await viewer.GetFormAsync(document))
        {
            Assert.False(await form.CanRestoreAttributionAsync());
            Assert.False(await form.CanWriteScriptsAsync());
        }
        using (var signatures = await viewer.GetSignaturesAsync(document)) Assert.False(await signatures.CanReadRevisionAsync());
        using (var stamps = await viewer.GetStampsAsync()) Assert.False(await stamps.CanCreateFromAnnotationsAsync(document));
        using (var interaction = await viewer.GetInteractionAsync(document)) Assert.False(await interaction.HasCursorClaimAsync());
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        using (var link = await viewer.GetLinkAsync(document)) _ = await link.GetStatusAsync(page);
        using var redaction = await viewer.GetRedactionAsync(document);
        var annotation = new AnnotationRef.ObjectNumber(page, 999999);
        Assert.False(await redaction.CanUnmarkAsync(annotation));
        Assert.False(await redaction.CanUpdateLabelAsync(annotation));
    }
}
