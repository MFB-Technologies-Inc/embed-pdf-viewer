using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed partial class IntegerSurfaceTests
{
    [Fact]
    public void AValueOutsideTheWireTypeThrowsInvalidInput()
    {
        var error = Assert.Throws<PdfException>(() => WireInteger.ToUInt32(-1, "index"));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        Assert.Equal("RangeError", error.Name);
        Assert.Equal("index -1 is outside 0...4294967295", error.Detail);
        Assert.Equal(0u, WireInteger.ToUInt32(0, "index"));
        Assert.Equal((uint)int.MaxValue, WireInteger.ToUInt32(int.MaxValue, "index"));
        Assert.Equal(-1L, WireInteger.ToInt64(-1));
    }

    [Fact]
    public void AResultPastIntThrowsInvalidState()
    {
        Assert.Equal(PdfErrorCode.InvalidState, Assert.Throws<PdfException>(() => WireInteger.ToInt(ulong.MaxValue, "count")).Code);
        // Unlike Swift's 64-bit Int, a C# int stops at 2^31 - 1.
        Assert.Equal(PdfErrorCode.InvalidState, Assert.Throws<PdfException>(() => WireInteger.ToInt(uint.MaxValue, "count")).Code);
        Assert.Equal(-1, WireInteger.ToInt(-1L, "index"));
        Assert.Equal(14, WireInteger.ToInt(14u, "count"));
        Assert.Equal("count 18446744073709551615 is outside -2147483648...2147483647",
            Assert.Throws<PdfException>(() => WireInteger.ToInt(ulong.MaxValue, "count")).Detail);
    }
}
