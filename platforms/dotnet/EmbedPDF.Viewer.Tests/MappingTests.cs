using Xunit;
using G = uniffi.epdf_viewer_host;
using GF = uniffi.epdf_ffi;

namespace EmbedPDF.Viewer.Tests;

public sealed class MappingTests
{
    static readonly GF.PluginErrorInfo FfiError = new("operation-failed", "failed to open base document", "documents", null);

    [Fact]
    public void AFailedDocumentMapsEveryField()
    {
        var info = Ffi.ToPublic(new G.DocumentInfo("d1", "a.pdf", 14, G.DocumentStatus.Failed, FfiError));
        Assert.Equal(new DocumentInfo("d1", "a.pdf", 14, DocumentStatus.Failed,
            new PluginErrorInfo(PdfErrorCode.OperationFailed, "failed to open base document", "documents", null)), info);
    }

    [Fact]
    public void APageMapsItsRefAndIndex()
    {
        var page = Ffi.ToPublic(new GF.PageInfo(new GF.PageRef(7), 2, 612, 792, 270));
        Assert.Equal(new PageInfo(new PageRef(7), 2, 612, 792, 270), page);
        Assert.Equal(new GF.PageRef(7), Ffi.ToFfi(page.Page));
    }

    [Fact]
    public void APageRefBelowZeroIsInvalidInput() =>
        Assert.Equal(PdfErrorCode.InvalidInput, Assert.Throws<PdfException>(() => Ffi.ToFfi(new PageRef(-1))).Code);

    [Fact]
    public void EveryDocumentEventMapsToItsRecord()
    {
        var document = new G.DocumentInfo("d1", null, 1, G.DocumentStatus.Ready, null);
        Assert.Equal(new DocumentEvent.Opened(new DocumentInfo("d1", null, 1, DocumentStatus.Ready, null)),
            Ffi.ToPublic(new G.DocumentEvent.Opened(document)));
        Assert.Equal(new DocumentEvent.OpenFailed("d1", Ffi.ToPublic(FfiError)), Ffi.ToPublic(new G.DocumentEvent.OpenFailed("d1", FfiError)));
        Assert.Equal(new DocumentEvent.Locked("d1", true), Ffi.ToPublic(new G.DocumentEvent.Locked("d1", true)));
        Assert.Equal(new DocumentEvent.Closed("d1"), Ffi.ToPublic(new G.DocumentEvent.Closed("d1")));
        Assert.Equal(new DocumentEvent.ActiveChanged(null), Ffi.ToPublic(new G.DocumentEvent.ActiveChanged(null)));
        Assert.Equal(new DocumentEvent.PagesChanged("d1"), Ffi.ToPublic(new G.DocumentEvent.PagesChanged("d1")));
    }

    [Fact]
    public void APatchMapsItsClearListInOrder()
    {
        var patch = Ffi.ToFfi(new ViewerSettingsPatch
        {
            Accent = "#123456",
            Clear = [ViewerSettingsField.PageShadow, ViewerSettingsField.PageBackground],
        });
        Assert.Equal("#123456", patch.Accent);
        Assert.Null(patch.PageBackground);
        Assert.Equal([G.ViewerSettingsField.PageShadow, G.ViewerSettingsField.PageBackground], patch.Clear);
        Assert.Empty(Ffi.ToFfi(new ViewerSettingsPatch()).Clear);
    }

    [Fact]
    public void SettingsAndSaveModesMap()
    {
        Assert.Equal(new ViewerSettings("#fff", null, "none"), Ffi.ToPublic(new G.ViewerSettings("#fff", null, "none")));
        Assert.Equal(G.SaveMode.Rewrite, Ffi.ToFfi(SaveMode.Rewrite));
        Assert.Equal(G.SaveMode.Incremental, Ffi.ToFfi(SaveMode.Incremental));
    }

    [Fact]
    public void TheNamedPermissionsAreTheEnginesInOrder()
    {
        Assert.Equal(29, DocumentPermission.Named.Count);
        Assert.Equal(29, DocumentPermission.Named.Distinct().Count());
        Assert.Equal("doc.open", DocumentPermission.Named[0].Value);
        Assert.Equal("annotations:create", DocumentPermission.Named[^1].Value);
        Assert.Equal(DocumentPermission.Download, (DocumentPermission)"doc.download");
        Assert.Equal("doc.render.thumbnail", DocumentPermission.RenderThumbnail.ToString());
    }

    [Fact]
    public void ContentInclusionUsesTheEnginesSpelling()
    {
        Assert.Null(ContentInclusion.WhenPermitted.ToEngine());
        Assert.True(ContentInclusion.Always.ToEngine());
        Assert.False(ContentInclusion.Never.ToEngine());
    }
}
