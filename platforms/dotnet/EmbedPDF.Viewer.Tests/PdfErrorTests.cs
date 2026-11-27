using uniffi.epdf_ffi;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace EmbedPDF.Viewer.Tests;

public sealed class PdfErrorTests
{
    public static TheoryData<string, string> Known => new()
    {
        { nameof(PdfErrorCode.PermissionDenied), "permission-denied" },
        { nameof(PdfErrorCode.Unsupported), "unsupported" },
        { nameof(PdfErrorCode.NotFound), "not-found" },
        { nameof(PdfErrorCode.NotReady), "not-ready" },
        { nameof(PdfErrorCode.InvalidInput), "invalid-input" },
        { nameof(PdfErrorCode.Conflict), "conflict" },
        { nameof(PdfErrorCode.InstanceClosed), "instance-closed" },
        { nameof(PdfErrorCode.OperationCancelled), "operation-cancelled" },
        { nameof(PdfErrorCode.OperationFailed), "operation-failed" },
        { nameof(PdfErrorCode.InvalidState), "invalid-state" },
    };

    static PdfErrorCode Named(string name) =>
        (PdfErrorCode)typeof(PdfErrorCode).GetProperty(name)!.GetValue(null)!;

    [Theory]
    [MemberData(nameof(Known))]
    public void EachKnownCodeRoundTripsItsWireString(string name, string wire)
    {
        var code = Named(name);
        Assert.Equal(wire, code.Value);
        Assert.Equal(code, new PdfErrorCode(wire));
        Assert.Equal(wire, code.ToString());
    }

    [Fact]
    public void AnUnnamedCodeRoundTripsAsItsOwnString()
    {
        var code = new PdfErrorCode("quota-exceeded");
        Assert.Equal("quota-exceeded", code.Value);
        Assert.True(code == "quota-exceeded");
        foreach (var row in Known)
        {
            Assert.NotEqual(Named((string)row[0]), code);
        }
    }

    [Fact]
    public void AStringComparesWithACode()
    {
        var error = new PdfException(PdfErrorCode.NotFound, "no page");
        Assert.True(error.Code == "not-found");
        Assert.False(error.Code == "not-ready");
    }

    [Fact]
    public void TheShortConstructorNamesItAPluginError()
    {
        var error = new PdfException(PdfErrorCode.PermissionDenied, "no");
        Assert.Equal("PluginError", error.Name);
        Assert.Equal(PdfErrorCode.PermissionDenied, error.Code);
        Assert.Equal("no", error.Detail);
        Assert.Equal("Custom", new PdfException("Custom", PdfErrorCode.Conflict, "d").Name);
        Assert.Equal(PdfErrorCode.PermissionDenied, new PdfException("PluginError", PdfErrorCode.PermissionDenied, "no").Code);
    }

    [Fact]
    public void ACatchFilterMatchesTheCode()
    {
        static string Outcome(PdfErrorCode code)
        {
            try
            {
                throw new PdfException(code, "d");
            }
            catch (PdfException e) when (e.Code == PdfErrorCode.NotFound)
            {
                return "not found";
            }
            catch (PdfException e) when (e.Code == PdfErrorCode.PermissionDenied)
            {
                return "denied";
            }
            catch (PdfException)
            {
                return "other";
            }
        }
        Assert.Equal(["not found", "denied", "other"],
            new[] { Outcome(PdfErrorCode.NotFound), Outcome(PdfErrorCode.PermissionDenied), Outcome(PdfErrorCode.Conflict) });
    }

    [Fact]
    public void TheMessageIsTheDetail()
    {
        Exception error = new PdfException(PdfErrorCode.NotFound, "no page p9");
        Assert.Equal("no page p9", error.Message);
    }

    [Fact]
    public void AMessageOnlyExceptionIsAnOperationFailure()
    {
        var error = new PdfException("broke");
        Assert.Equal(PdfErrorCode.OperationFailed, error.Code);
        Assert.Equal("broke", error.Detail);
        Assert.Equal(nameof(PdfException), error.Name);
    }

    [Fact]
    public void AKernelErrorKeepsItsNameCodeAndDetail()
    {
        var mapped = PdfException.From(new ViewerException.Kernel("PluginError", "not-found", "no page p9"));
        Assert.Equal(("PluginError", PdfErrorCode.NotFound, "no page p9"), (mapped.Name, mapped.Code, mapped.Detail));
    }

    [Fact]
    public void AKernelErrorWithNoCodeIsAnOperationFailure()
    {
        Assert.Equal(PdfErrorCode.OperationFailed, PdfException.From(new ViewerException.Kernel("EngineError", null, "boom")).Code);
    }

    [Fact]
    public void ClosedAndDisposedAreInstanceClosed()
    {
        foreach (var error in new Exception[] { new ViewerException.Closed(), new ObjectDisposedException("ViewerHost") })
        {
            var mapped = PdfException.From(error);
            Assert.Equal(PdfErrorCode.InstanceClosed, mapped.Code);
            Assert.Equal("Closed", mapped.Name);
            Assert.Equal("The viewer has shut down.", mapped.Detail);
        }
    }

    [Fact]
    public void CancellationIsAnAbortError()
    {
        var mapped = PdfException.From(new OperationCanceledException());
        Assert.Equal(("AbortError", PdfErrorCode.OperationCancelled, "operation cancelled"), (mapped.Name, mapped.Code, mapped.Detail));
    }

    [Fact]
    public void APdfExceptionPassesThroughAndAForeignOneIsNamedByItsType()
    {
        var own = new PdfException(PdfErrorCode.Conflict, "d");
        Assert.Same(own, PdfException.From(own));
        var foreign = PdfException.From(new InvalidOperationException("nope"));
        Assert.Equal(("InvalidOperationException", PdfErrorCode.OperationFailed, "nope"), (foreign.Name, foreign.Code, foreign.Detail));
    }

    [Fact]
    public void ThreeStringsHaveOneReading()
    {
        // With a (code, detail, name) overload, three strings could bind either
        // way through the implicit string conversion. Only (name, code, detail) exists.
        Assert.Null(typeof(PdfException).GetConstructor([typeof(PdfErrorCode), typeof(string), typeof(string)]));
        var error = new PdfException("Custom", "not-found", "no page");
        Assert.Equal(("Custom", PdfErrorCode.NotFound, "no page"), (error.Name, error.Code, error.Detail));
    }

    [Fact]
    public void ADefaultCodeIsEmptyNotNull()
    {
        Assert.Equal("", default(PdfErrorCode).Value);
        Assert.Equal("", default(PdfErrorCode).ToString());
        Assert.Equal("", default(DocumentPermission).ToString());
    }
}
