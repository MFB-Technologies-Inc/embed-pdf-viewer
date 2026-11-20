using System.Collections.Concurrent;
using Jint;
using Jint.Runtime;

namespace EmbedPDF.Scripting.Jint;

/// <summary>Limits for each realm a <see cref="JintScriptEngineFactory"/> makes.</summary>
public sealed record JintScriptEngineOptions
{
    /// <summary>
    /// The deepest script call stack. Past it a script sees
    /// <c>RangeError: Maximum call stack size exceeded</c>, which the viewer
    /// reports as a budget failure. Default 1,000.
    /// </summary>
    public int MaxCallDepth { get; init; } = 1000;

    /// <summary>
    /// What one evaluation may allocate, in bytes. Past it the evaluation
    /// stops and the realm is poisoned. It counts the interpreter's own
    /// allocations too (parsing the viewer's 48 KB prelude among them), so
    /// it sits above the script budget's 16 MiB. Default 64 MiB.
    /// </summary>
    public long MaxMemoryBytes { get; init; } = 64L * 1024 * 1024;

    /// <summary>
    /// The stack of each realm's thread, in bytes; it bounds how deep
    /// <see cref="MaxCallDepth"/> can safely go. Default 16 MiB, at least 256 KiB.
    /// </summary>
    public int ThreadStackBytes { get; init; } = 16 * 1024 * 1024;

    /// <summary>
    /// How long one regular expression match may run. A match runs inside one
    /// interpreter step, where the evaluation's time limit can't stop it, and
    /// Jint fixes this timeout when a realm is built, so it can't follow each
    /// evaluation's limit. Default 300 ms: the default script budget (50 ms)
    /// plus the 250 ms the viewer waits past a limit before abandoning a realm.
    /// Past it the evaluation fails and, once its limit has passed, times out.
    /// </summary>
    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromMilliseconds(300);
}

/// <summary>
/// PDF JavaScript in Jint, one engine per realm. Pass it as
/// <c>new ActionsJavaScriptConfig(true) { Engine = new JintScriptEngineFactory() }</c>.
/// </summary>
/// <remarks>
/// Each realm runs on its own background thread. Scripts get no .NET
/// interop: no CLR types, namespaces or assemblies are exposed. The time
/// limit is enforced inside the interpreter, so a runaway script stops and
/// frees its thread; the realm is then poisoned, as the viewer expects.
/// </remarks>
public sealed class JintScriptEngineFactory : IScriptEngineFactory
{
    readonly JintScriptEngineOptions options;

    /// <summary>A factory with <paramref name="options"/>, or the defaults.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A limit is not positive, or the thread stack is under 256 KiB.</exception>
    public JintScriptEngineFactory(JintScriptEngineOptions? options = null)
    {
        options ??= new JintScriptEngineOptions();
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxCallDepth, 1, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxMemoryBytes, 1, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.ThreadStackBytes, 256 * 1024, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.RegexTimeout, TimeSpan.Zero, nameof(options));
        this.options = options;
    }

    /// <inheritdoc/>
    public IScriptEngine Create() => new JintRealm(options);
}

/// <summary>One realm: a Jint engine owned by one thread.</summary>
internal sealed class JintRealm : IScriptEngine
{
    /// <summary>How long past the limit to wait before abandoning a realm whose interpreter did not stop.</summary>
    static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(250);

    readonly BlockingCollection<Work> work = [];
    readonly Lock gate = new();
    readonly Deadline deadline = new();
    bool unusable;
    int busy;

    /// <summary>Whether the realm's thread is evaluating; for tests.</summary>
    internal bool Busy => Volatile.Read(ref busy) != 0;

    internal JintRealm(JintScriptEngineOptions options)
    {
        var ready = new ManualResetEventSlim();
        var thread = new Thread(() => Loop(options, ready), options.ThreadStackBytes)
        {
            IsBackground = true,
            Name = "EmbedPDF script realm",
        };
        thread.Start();
        ready.Wait();
        ready.Dispose();
    }

    public ScriptEvaluation Evaluate(string program, int timeLimitMs)
    {
        ArgumentNullException.ThrowIfNull(program);
        lock (gate)
        {
            if (unusable) return new ScriptEvaluation.Threw("the realm is disposed");
            var item = new Work(program, Math.Max(timeLimitMs, 0));
            work.Add(item);
            // The budget is the script's: the clock starts when the realm's
            // thread takes the work, not when it was posted.
            item.Started.Wait();
            if (!item.Done.Wait(TimeSpan.FromMilliseconds(item.TimeLimitMs) + Grace))
            {
                Poison();
                return new ScriptEvaluation.TimedOut();
            }
            if (item.Poisons) Poison();
            return item.Answer!;
        }
    }

    public void Dispose()
    {
        lock (gate) Poison();
    }

    void Poison()
    {
        if (unusable) return;
        unusable = true;
        work.CompleteAdding();
    }

    void Loop(JintScriptEngineOptions options, ManualResetEventSlim ready)
    {
        var engine = new Engine(o =>
        {
            // A regex match runs inside one interpreter step, out of the
            // deadline constraint's reach, so it gets its own timeout. Jint
            // fixes it when the engine is built, so it can't follow each
            // evaluation's limit.
            o.Constraints.RegexTimeout = options.RegexTimeout;
            o.LimitMemory(options.MaxMemoryBytes);
            o.Constraints.MaxExecutionStackCount = options.MaxCallDepth;
            o.Constraint(deadline);
        });
        // Warm the interpreter so the first real evaluation's budget isn't
        // spent compiling the interpreter itself.
        deadline.Until = long.MaxValue;
        engine.Evaluate("0");
        ready.Set();
        foreach (var item in work.GetConsumingEnumerable())
        {
            deadline.Until = Environment.TickCount64 + item.TimeLimitMs;
            Volatile.Write(ref busy, 1);
            item.Started.Set();
            try
            {
                var value = engine.Evaluate(item.Program);
                item.Answer = value.IsString()
                    ? new ScriptEvaluation.Value(value.AsString())
                    : new ScriptEvaluation.Threw("the program did not evaluate to a string");
            }
            catch (DeadlineExceededException)
            {
                item.Answer = new ScriptEvaluation.TimedOut();
                item.Poisons = true;
            }
            catch (MemoryLimitExceededException error)
            {
                item.Answer = new ScriptEvaluation.Threw($"out of memory: {error.Message}");
                item.Poisons = true;
            }
            catch (Exception error)
            {
                item.Answer = new ScriptEvaluation.Threw(error.Message);
            }
            Volatile.Write(ref busy, 0);
            item.Done.Set();
        }
    }

    sealed class Work(string program, int timeLimitMs)
    {
        public string Program { get; } = program;
        public int TimeLimitMs { get; } = timeLimitMs;
        public ManualResetEventSlim Started { get; } = new();
        public ManualResetEventSlim Done { get; } = new();
        public ScriptEvaluation? Answer { get; set; }
        public bool Poisons { get; set; }
    }

    /// <summary>Stops a script at the current evaluation's deadline.</summary>
    sealed class Deadline : Constraint
    {
        public long Until;

        public override void Check()
        {
            if (Environment.TickCount64 > Volatile.Read(ref Until)) throw new DeadlineExceededException();
        }

        public override void Reset() { }
    }

    sealed class DeadlineExceededException : Exception
    {
        public DeadlineExceededException() : base("Script execution exceeded its time limit") { }
    }
}
