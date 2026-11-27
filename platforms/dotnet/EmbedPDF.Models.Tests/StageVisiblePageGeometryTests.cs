namespace EmbedPDF.Models.Tests;

/// <summary>Port of <c>StageVisiblePageGeometryTests.swift</c>.</summary>
public class StageVisiblePageGeometryTests
{
    static readonly PageInfo Info = new(new PageRef(1), 0, 612, 792, 0);

    /// <summary>The page drawn at half a view point per PDF point, turned <paramref name="rotation"/> degrees by the stage.</summary>
    static StageVisiblePage VisiblePage(double rotation, PageInfo? info = null)
    {
        info ??= Info;
        var turned = rotation == 90 || rotation == 270;
        var viewWidth = (turned ? Info.Height : Info.Width) / 2;
        var viewHeight = (turned ? Info.Width : Info.Height) / 2;
        return new StageVisiblePage(Info.Page, 0, 0, 0, viewWidth, viewHeight, rotation, 0.5, 0, 0,
            new StagePageTransform(rotation, viewWidth, viewHeight, Info.Width, Info.Height, viewWidth, viewHeight, 0.5, 0.5, 0.5, 1),
            new PdfRect(0, 0, Info.Width, Info.Height));
    }

    /// <summary>A page the stage has not sized yet has a zero view width, and a malformed one can have a zero page width. Neither divides by zero.</summary>
    [Fact]
    public void AZeroWidthMapsToZeroNotToInfinity()
    {
        var page = VisiblePage(0);
        var unsized = page with { Transform = page.Transform with { ViewWidth = 0 } };
        // Every overlay point is the page's top left corner.
        Assert.Equal(new PdfPoint(0, 0), unsized.PagePoint(100, 50, Info));
        Assert.Equal(default, unsized.LocalRect(new PdfRect(10, 20, 30, 40), Info));
        var empty = Info with { Width = 0 };
        Assert.Equal(new PdfPoint(0, 0), page.PagePoint(100, 50, empty));
        Assert.Equal(default, page.LocalRect(new PdfRect(10, 20, 30, 40), empty));
    }

    [Fact]
    public void GeometryTakesItsTurnFromTheStage()
    {
        Assert.Equal(new PageGeometry(RenderRotation.Clockwise90, new PdfSize(612, 792)), VisiblePage(90).GetGeometry(Info));
        Assert.Equal(RenderRotation.Clockwise90, VisiblePage(89.999).GetGeometry(Info).Turn);
    }

    [Fact]
    public void UnturnedPagePointScalesByTheDrawnSize()
    {
        var page = VisiblePage(0);
        var point = page.PagePoint(100, 50, Info);
        Assert.Equal(new PdfPoint(200, 100), point);
        var local = page.LocalRect(new PdfRect(point.X, point.Y, 1, 1), Info);
        Assert.Equal(100, local.X, 3);
        Assert.Equal(50, local.Y, 3);
        Assert.Equal(0.5, local.Width, 3);
        Assert.Equal(0.5, local.Height, 3);
    }

    [Fact]
    public void QuarterTurnedPagePointComesBackToItsStart()
    {
        var page = VisiblePage(90);
        var point = page.PagePoint(100, 50, Info);
        Assert.Equal(new PdfPoint(100, 592), point);
        // A clockwise quarter turn puts the rect's page origin at its top
        // right corner on screen.
        var local = page.LocalRect(new PdfRect(point.X, point.Y, 1, 1), Info);
        Assert.Equal(100, local.X + local.Width, 3);
        Assert.Equal(50, local.Y, 3);
        Assert.Equal(0.5, local.Width, 3);
        Assert.Equal(0.5, local.Height, 3);
    }

    [Fact]
    public void AViewportPointAddsThePagesScreenOrigin()
    {
        var page = VisiblePage(0) with { ScreenX = 10, ScreenY = 20 };
        Assert.Equal(new PdfPoint(110, 70), page.ViewportPoint(100, 50));
    }
}
