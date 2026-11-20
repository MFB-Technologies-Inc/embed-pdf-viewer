using Microsoft.Extensions.Logging.Abstractions;
using GF = uniffi.epdf_ffi;
using GA = uniffi.epdf_plugin_actions;

namespace EmbedPDF;

/// <summary>What one evaluation came to.</summary>
public abstract record ScriptEvaluation
{
    private protected ScriptEvaluation() { }

    /// <summary>The program's completion value, which was a string.</summary>
    /// <param name="Json">The string.</param>
    public sealed record Value(string Json) : ScriptEvaluation;

    /// <summary>It threw, or completed with something that isn't a string.</summary>
    /// <param name="Detail">What happened.</param>
    public sealed record Threw(string Detail) : ScriptEvaluation;

    /// <summary>It ran past its time limit. The realm is unusable afterwards.</summary>
    public sealed record TimedOut : ScriptEvaluation;
}

/// <summary>
/// One JavaScript realm: globals persist across evaluations. The engine only
/// evaluates program text; the viewer builds every program.
/// </summary>
/// <remarks>
/// The kernel calls it on its own thread and waits for the answer, so give
/// up at the time limit. Never block waiting for the viewer here: a viewer
/// call queues behind this evaluation, so waiting for it deadlocks.
/// <see cref="PdfViewer.Dispose"/> throws here. An
/// exception from <see cref="Evaluate"/> reads as
/// <see cref="ScriptEvaluation.Threw"/> with its message; one from
/// <see cref="IDisposable.Dispose"/> is logged. Dispose is idempotent.
/// </remarks>
public interface IScriptEngine : IDisposable
{
    /// <summary>Evaluates <paramref name="program"/> within <paramref name="timeLimitMs"/> milliseconds.</summary>
    ScriptEvaluation Evaluate(string program, int timeLimitMs);
}

/// <summary>Makes one realm per document. Set it as <see cref="ActionsJavaScriptConfig.Engine"/>.</summary>
/// <remarks>Called on the kernel thread. An exception fails the document's scripting with its message.</remarks>
public interface IScriptEngineFactory
{
    /// <summary>A fresh realm.</summary>
    IScriptEngine Create();
}

/// <summary>What <see cref="ScriptEngineCheck.RunAsync"/> saw.</summary>
/// <param name="Alerts"><c>app.alert</c> messages, in order.</param>
/// <param name="Error">How the script failed, if it did.</param>
/// <param name="Disposed">Whether the run poisoned the realm.</param>
public sealed record ScriptCheck(IReadOnlyList<string> Alerts, ScriptExecutionError? Error, bool Disposed);

/// <summary>A host's check that its script engine works.</summary>
public static class ScriptEngineCheck
{
    /// <summary>
    /// Runs <paramref name="source"/> once in a fresh realm from
    /// <paramref name="factory"/>, outside any document, with the default
    /// budget, on the thread pool. An engine that cannot load the prelude
    /// throws <see cref="PdfException"/>.
    /// </summary>
    public static Task<ScriptCheck> RunAsync(IScriptEngineFactory factory, string source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        NativeRuntime.EnsureLoaded();
        var adapter = new ScriptEngineFactoryAdapter(factory, new PluginContext(new KernelMark(), NullLogger.Instance));
        return Task.Run(() =>
        {
            try
            {
                var check = GA.EpdfPluginActionsMethods.CheckScriptEngine(adapter, source);
                return new ScriptCheck(check.Alerts, check.Error is null ? null : Ffi.ToPublic(check.Error), check.Disposed);
            }
            catch (Exception error)
            {
                throw PdfException.From(error);
            }
        }, cancellationToken);
    }
}

/// <summary>Called on the kernel thread, marked; app exceptions never reach the kernel unmapped.</summary>
internal sealed class ScriptEngineFactoryAdapter(IScriptEngineFactory app, PluginContext context) : GA.ScriptEngineFactory
{
    public GA.ScriptEngine Create()
    {
        IScriptEngine engine;
        using (KernelCallback.Enter(context.Mark))
        {
            try
            {
                engine = app.Create() ?? throw new InvalidOperationException("The script engine factory returned no engine.");
            }
            catch (Exception error)
            {
                throw new GF.HostException.Failed(error.Message);
            }
        }
        return new EngineAdapter(engine, context);
    }

    sealed class EngineAdapter(IScriptEngine engine, PluginContext context) : GA.ScriptEngine
    {
        public GA.ScriptEvaluation Evaluate(string program, uint timeLimitMs) =>
            KernelCallback.Run<GA.ScriptEvaluation>(context.Mark, context.Logger, "A script engine's Evaluate", () =>
                engine.Evaluate(program, (int)Math.Min(timeLimitMs, int.MaxValue)) switch
                {
                    ScriptEvaluation.Value value => new GA.ScriptEvaluation.Value(value.Json ?? "null"),
                    ScriptEvaluation.TimedOut => new GA.ScriptEvaluation.TimedOut(),
                    ScriptEvaluation.Threw threw => new GA.ScriptEvaluation.Threw(threw.Detail ?? ""),
                    _ => new GA.ScriptEvaluation.Threw("the engine returned no evaluation"),
                },
                error => new GA.ScriptEvaluation.Threw(error.Message));

        public void DisposeEngine() => KernelCallback.Run(context.Mark, context.Logger, "A script engine's Dispose", engine.Dispose);
    }
}

public sealed partial record ActionsJavaScriptConfig
{
    /// <summary>
    /// The engine document JavaScript runs in. The viewer ships none;
    /// <c>JintScriptEngineFactory</c> in the <c>EmbedPDF.Scripting.Jint</c>
    /// package is one.
    /// </summary>
    public IScriptEngineFactory? Engine { get; init; }

    internal GA.ActionsJavaScriptConfig ToFfi(PluginContext context) => new(
        Enabled: Enabled,
        Engine: Engine is null ? null : new ScriptEngineFactoryAdapter(Engine, context),
        Budget: Budget is null ? null : Ffi.ToFfi(Budget),
        MaxScriptNodesPerDispatch: WireInteger.ToUInt32(MaxScriptNodesPerDispatch, "maxScriptNodesPerDispatch"));
}
