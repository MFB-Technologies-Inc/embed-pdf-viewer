namespace EmbedPDF.Models.Tests;

/// <summary>
/// A model made on a thread whose dispatcher installs a fresh context
/// instance per callback still runs inline there: a setter's value reads back
/// at once, and notifications stay on the thread.
/// </summary>
public class FreshInstanceContextTests
{
    [Fact]
    public Task ASetterOnTheModelsThreadReadsBackAtOnce() => FreshInstanceContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync(b => b.Search(new SearchConfig()));
        var document = await viewer.OpenAsync(Fixture.Data());
        using var search = await viewer.GetSearchAsync(document);
        var model = context.Watch(new SearchModel(search));
        // After an await the thread runs a later callback, under a new instance.
        await Task.Yield();
        Assert.NotSame(model.Context, SynchronizationContext.Current);
        model.Text = "abc";
        Assert.Equal("abc", model.Text);
        Assert.True(model.CanClear);
        await model.SubmitSearchAsync();
        Assert.Equal(context.Thread, Thread.CurrentThread);
    });

    [Fact]
    public Task AnEntryPointOnTheModelsThreadRunsInline() => FreshInstanceContext.Run(async context =>
    {
        var probe = context.Watch(new Probe());
        await Task.Yield();
        var setting = probe.SetAsync(3);
        Assert.True(setting.IsCompleted);
        Assert.Equal(3, probe.Value);
        await setting;
    });

    [Fact]
    public Task AModelGivenAnotherModelsContextKnowsItsThread() => FreshInstanceContext.Run(async context =>
    {
        // The reader model hands its context to the session's models.
        var parent = context.Watch(new Probe());
        await Task.Yield();
        var child = context.Watch(new Probe(parent.Context));
        await Task.Yield();
        var setting = child.SetAsync(5);
        Assert.True(setting.IsCompleted);
        await setting;
    });

    [Fact]
    public async Task AModelGivenAnotherThreadsContextStillPosts()
    {
        // Given a context it was not made under, the model knows no thread
        // and falls back to the identity check: a call from elsewhere posts.
        var serial = new SerialSynchronizationContext();
        var probe = new Probe(serial);
        await probe.SetAsync(4);
        Assert.Equal(4, probe.Value);
        Assert.False(probe.Home.IsCurrent);
    }
}
