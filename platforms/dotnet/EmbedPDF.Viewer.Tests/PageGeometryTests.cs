using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class PageGeometryTests
{
    static readonly PdfSize Size = new(200, 100);

    public static TheoryData<RenderRotation> Turns =>
        [RenderRotation.None, RenderRotation.Clockwise90, RenderRotation.Clockwise180, RenderRotation.Clockwise270];

    [Fact]
    public void NormalizesRotationToAQuarterTurn()
    {
        Assert.Equal(270, new PageGeometry(-90, Size).Rotation);
        Assert.Equal(90, new PageGeometry(450, Size).Rotation);
        Assert.Equal(0, new PageGeometry(359, Size).Rotation);
        _ = new PageGeometry(int.MaxValue, Size);
        _ = new PageGeometry(int.MinValue, Size);
    }

    [Fact]
    public void DegreesReduceToAQuarterTurn()
    {
        Assert.Equal(RenderRotation.Clockwise270, RenderRotationExtensions.FromDegrees(-90));
        Assert.Equal(RenderRotation.Clockwise90, RenderRotationExtensions.FromDegrees(450));
        Assert.Equal(RenderRotation.Clockwise90, RenderRotationExtensions.FromDegrees(89.6));
        Assert.Equal(RenderRotation.Clockwise270, RenderRotationExtensions.FromDegrees(-90.4));
        // int.MaxValue is 127 modulo 360 and int.MinValue is 232.
        Assert.Equal(RenderRotation.Clockwise90, RenderRotationExtensions.FromDegrees(int.MaxValue));
        Assert.Equal(RenderRotation.Clockwise270, RenderRotationExtensions.FromDegrees(int.MinValue));
        Assert.Equal(RenderRotation.None, RenderRotationExtensions.FromDegrees(double.NaN));
        Assert.Equal(RenderRotation.None, RenderRotationExtensions.FromDegrees(double.PositiveInfinity));
        Assert.Equal(RenderRotation.None, RenderRotationExtensions.FromDegrees(double.NegativeInfinity));
        // 1e12 is 280 modulo 360.
        Assert.Equal(RenderRotation.Clockwise270, RenderRotationExtensions.FromDegrees(1e12));
    }

    [Fact]
    public void TurnReportsItsDegrees()
    {
        var turns = new[] { RenderRotation.None, RenderRotation.Clockwise90, RenderRotation.Clockwise180, RenderRotation.Clockwise270 };
        Assert.Equal([0, 90, 180, 270], turns.Select(t => t.Degrees()));
        Assert.Equal([false, true, false, true], turns.Select(t => t.IsQuarterTurn()));
        var geometry = new PageGeometry(RenderRotation.Clockwise270, Size);
        Assert.Equal(270, geometry.Rotation);
        Assert.Equal(geometry, new PageGeometry(270, Size));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void PointsRoundTrip(int rotation)
    {
        var geometry = new PageGeometry(rotation, Size);
        var point = new PdfPoint(30, 20);
        Assert.Equal(point, geometry.ToPage(geometry.ToDisplay(point)));
    }

    [Theory]
    [MemberData(nameof(Turns))]
    public void RectsRoundTrip(RenderRotation turn)
    {
        var geometry = new PageGeometry(turn, new PdfSize(612, 792));
        var rect = new PdfRect(10, 20, 100, 12);
        Assert.Equal(rect, geometry.ToPage(geometry.ToDisplay(rect)));
    }

    [Fact]
    public void QuarterTurnMapsTopLeftToTopRight()
    {
        var geometry = new PageGeometry(90, Size);
        Assert.Equal(new PdfSize(100, 200), geometry.DisplaySize);
        Assert.Equal(new PdfPoint(100, 0), geometry.ToDisplay(new PdfPoint(0, 0)));
        Assert.Equal(new PdfRect(40, 10, 40, 30), geometry.ToDisplay(new PdfRect(10, 20, 30, 40)));
        Assert.Equal(RenderRotation.Clockwise90, geometry.Turn);
    }

    [Fact]
    public void APageInfoReportsItsGeometry()
    {
        var page = new PageInfo(new PageRef(4), 0, 612, 792, 90);
        Assert.Equal(new PageGeometry(RenderRotation.Clockwise90, new PdfSize(612, 792)), page.Geometry);
    }
}
