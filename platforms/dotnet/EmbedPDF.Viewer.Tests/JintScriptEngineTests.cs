using System.Diagnostics;
using EmbedPDF.Scripting.Jint;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>The port of the Swift JavaScriptCore engine tests, plus the limits Jint adds.</summary>
public sealed class JintScriptEngineTests
{
    [Fact]
    public async Task AnAlertComesBackAsAnEffect()
    {
        var check = await ScriptEngineCheck.RunAsync(new JintScriptEngineFactory(), "app.alert('hi')");
        Assert.Equal(["hi"], check.Alerts);
        Assert.Null(check.Error);
        Assert.False(check.Disposed);
    }

    [Fact]
    public async Task AThrowIsAnExceptionAndKeepsTheRealm()
    {
        var check = await ScriptEngineCheck.RunAsync(new JintScriptEngineFactory(), "throw new Error('boom')");
        Assert.Equal(ScriptErrorKind.Exception, check.Error?.Kind);
        Assert.False(check.Disposed);
    }

    [Fact]
    public async Task ARunawayScriptTimesOut()
    {
        var clock = Stopwatch.StartNew();
        var check = await ScriptEngineCheck.RunAsync(new JintScriptEngineFactory(), "while (true) {}");
        Assert.Equal(ScriptErrorKind.Budget, check.Error?.Kind);
        Assert.True(check.Disposed);
        // The default budget is 50 ms; allow scheduling slack, not a hang.
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"took {clock.Elapsed}");
    }

    [Fact]
    public void TheRealmsThreadStartDoesNotCountAgainstTheBudget()
    {
        using var engine = new JintScriptEngineFactory().Create();
        Assert.Equal(new ScriptEvaluation.Value("1"), engine.Evaluate("JSON.stringify(1)", 50));
    }

    [Fact]
    public async Task ADeepRecursionIsABudgetFailure()
    {
        var check = await ScriptEngineCheck.RunAsync(new JintScriptEngineFactory(), "function f() { return f() + 1 } f()");
        Assert.Equal(ScriptErrorKind.Budget, check.Error?.Kind);
    }

    [Fact]
    public void ScriptsReachNoClrType()
    {
        using var engine = new JintScriptEngineFactory().Create();
        Assert.Equal(new ScriptEvaluation.Value("undefinedundefinedundefined"),
            engine.Evaluate("typeof System + typeof importNamespace + typeof clrHelper", 1000));
    }

    [Fact]
    public void AnAllocationPastTheLimitPoisonsTheRealm()
    {
        using var engine = new JintScriptEngineFactory(new JintScriptEngineOptions { MaxMemoryBytes = 8 * 1024 * 1024 }).Create();
        var answer = Assert.IsType<ScriptEvaluation.Threw>(engine.Evaluate(
            "var a = []; while (true) a.push(new Array(10000).join('x')); 'done'", 5000));
        Assert.StartsWith("out of memory", answer.Detail);
        Assert.Equal(new ScriptEvaluation.Threw("the realm is disposed"), engine.Evaluate("'1'", 1000));
    }

    [Fact]
    public void ANonStringIsThrownAndAScriptErrorKeepsTheRealm()
    {
        using var engine = new JintScriptEngineFactory().Create();
        Assert.Equal(new ScriptEvaluation.Threw("the program did not evaluate to a string"), engine.Evaluate("1", 1000));
        Assert.IsType<ScriptEvaluation.Threw>(engine.Evaluate("throw new Error('x')", 1000));
        Assert.Equal(new ScriptEvaluation.Value("2"), engine.Evaluate("var two = 2; JSON.stringify(two)", 1000));
        Assert.Equal(new ScriptEvaluation.Value("2"), engine.Evaluate("JSON.stringify(two)", 1000));
    }

    [Fact]
    public void ATimeoutPoisonsTheRealmAndDisposeIsIdempotent()
    {
        var engine = new JintScriptEngineFactory().Create();
        Assert.Equal(new ScriptEvaluation.TimedOut(), engine.Evaluate("while (true) {}", 50));
        Assert.Equal(new ScriptEvaluation.Threw("the realm is disposed"), engine.Evaluate("'1'", 1000));
        engine.Dispose();
        engine.Dispose();
    }

    [Fact]
    public void BadOptionsAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JintScriptEngineFactory(new JintScriptEngineOptions { MaxCallDepth = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JintScriptEngineFactory(new JintScriptEngineOptions { MaxMemoryBytes = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JintScriptEngineFactory(new JintScriptEngineOptions { ThreadStackBytes = 1024 }));
    }

    static async Task<(PdfViewer Viewer, ActionsCapability Actions, PdfActionTree AlertTree)> ScriptingViewer(IScriptEngineFactory factory)
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Actions = new() { OpenSequence = OpenSequence.Off, JavaScript = new(true) { Engine = factory } },
        });
        var document = await viewer.OpenAsync(Fixture.Data("document_actions"));
        var actions = await viewer.GetActionsAsync(document);
        using var willClose = await actions.GetActionTreeAsync(new ActionTreeSource.Document(DocumentTriggerEvent.WillClose));
        Assert.Contains("var willClose = true;", willClose!.Json);
        var alert = PdfActionTree.FromJson(willClose.Json.Replace("var willClose = true;", "app.alert('hi')"));
        return (viewer, actions, alert);
    }

    [Fact]
    public async Task AViewerRunsDocumentJavaScriptThroughJint()
    {
        var (viewer, actions, alert) = await ScriptingViewer(new JintScriptEngineFactory());
        await using var _ = viewer;
        Assert.True(await actions.IsScriptingEnabledAsync());
        var ui = new RecordingUI();
        await actions.SetUiHandlerAsync(ui);
        var result = await actions.ExecuteAsync(alert, ActionsTests.User);
        Assert.Equal([ActionNodeStatus.Executed], result.Nodes.Select(n => n.Status));
        Assert.Equal(["hi"], ui.Alerts);
    }

    /// <summary>Disposes the viewer from inside a realm's first evaluation.</summary>
    sealed class DisposingFactory(IScriptEngineFactory inner) : IScriptEngineFactory
    {
        public PdfViewer? Viewer;
        public Exception? Thrown;

        public IScriptEngine Create() => new Engine(inner.Create(), this);

        sealed class Engine(IScriptEngine inner, DisposingFactory owner) : IScriptEngine
        {
            public ScriptEvaluation Evaluate(string program, int timeLimitMs)
            {
                try { owner.Viewer?.Dispose(); }
                catch (Exception error) { owner.Thrown ??= error; }
                return inner.Evaluate(program, timeLimitMs);
            }

            public void Dispose() => inner.Dispose();
        }
    }

    [Fact]
    public async Task DisposingTheViewerFromAnEngineThrowsInvalidOperation()
    {
        var factory = new DisposingFactory(new JintScriptEngineFactory());
        var (viewer, actions, alert) = await ScriptingViewer(factory);
        await using var _ = viewer;
        factory.Viewer = viewer;
        await actions.SetUiHandlerAsync(new RecordingUI());
        await actions.ExecuteAsync(alert, ActionsTests.User);
        Assert.IsType<InvalidOperationException>(factory.Thrown);
        await actions.GetPolicyAsync();
    }
}
