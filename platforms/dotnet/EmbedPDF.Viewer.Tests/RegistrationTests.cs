using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class RegistrationTests
{
    [Fact]
    public async Task DisposeUnregistersAndReleasesTheHandle()
    {
        await using var reporting = await StreamRuleTests.ViewerThatReportsWithRegistration();
        var registration = reporting.Registration;
        registration.Dispose();
        // The handle is gone: the generated object refuses further calls.
        Assert.Throws<ObjectDisposedException>(() => registration.Handle.Unregister());
        // Both stay idempotent.
        registration.Unregister();
        registration.Dispose();
    }

    [Fact]
    public async Task UnregisterAloneKeepsTheHandleUntilDispose()
    {
        await using var reporting = await StreamRuleTests.ViewerThatReportsWithRegistration();
        var registration = reporting.Registration;
        registration.Unregister();
        registration.Handle.Unregister();
        registration.Dispose();
    }
}
