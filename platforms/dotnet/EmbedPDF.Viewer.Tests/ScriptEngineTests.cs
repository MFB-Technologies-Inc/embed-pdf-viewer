using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>The script engine adapters, with engines that misbehave.</summary>
public sealed class ScriptEngineTests
{
    sealed class Factory(Func<IScriptEngine> create) : IScriptEngineFactory
    {
        public IScriptEngine Create() => create();
    }

    sealed class Engine(Func<string, int, ScriptEvaluation> evaluate) : IScriptEngine
    {
        public int Disposals;
        public ScriptEvaluation Evaluate(string program, int timeLimitMs) => evaluate(program, timeLimitMs);
        public void Dispose() { Disposals++; throw new InvalidOperationException("dispose failed"); }
    }

    [Fact]
    public async Task AFactoryThatThrowsFailsTheCheck()
    {
        var error = await Assert.ThrowsAsync<PdfException>(() =>
            ScriptEngineCheck.RunAsync(new Factory(() => throw new InvalidOperationException("no realm")), "1"));
        Assert.Contains("no realm", error.Detail);
    }

    [Fact]
    public async Task AnEngineThatThrowsFailsThePreludeAndIsDisposed()
    {
        var engine = new Engine((_, _) => throw new InvalidOperationException("broken engine"));
        var error = await Assert.ThrowsAsync<PdfException>(() => ScriptEngineCheck.RunAsync(new Factory(() => engine), "1"));
        Assert.Contains("broken engine", error.Detail);
        Assert.Equal(1, engine.Disposals);
    }

    [Fact]
    public async Task AnEngineAnsweringTheWrongPreludeValueFailsTheCheck()
    {
        var engine = new Engine((_, _) => new ScriptEvaluation.Value("\"undefined\""));
        var error = await Assert.ThrowsAsync<PdfException>(() => ScriptEngineCheck.RunAsync(new Factory(() => engine), "1"));
        Assert.Contains("__acrojsRun is \"undefined\"", error.Detail);
    }

    [Fact]
    public async Task AnEngineThatTimesOutFailsThePrelude()
    {
        var engine = new Engine((_, limit) =>
        {
            Assert.Equal(1000, limit);
            return new ScriptEvaluation.TimedOut();
        });
        var error = await Assert.ThrowsAsync<PdfException>(() => ScriptEngineCheck.RunAsync(new Factory(() => engine), "1"));
        Assert.Contains("1000 ms", error.Detail);
    }

    [Fact]
    public async Task ACancelledTokenIsCancelled()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ScriptEngineCheck.RunAsync(new Factory(() => throw new InvalidOperationException()), "1", cancel.Token));
    }
}
