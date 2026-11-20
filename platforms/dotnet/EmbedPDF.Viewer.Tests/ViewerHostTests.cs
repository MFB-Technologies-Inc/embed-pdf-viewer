using System.Collections.Concurrent;
using uniffi.epdf_ffi;
using uniffi.epdf_plugin_commands;
using uniffi.epdf_viewer_host;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

// The generated types are internal (the generator default access modifier),
// so these tests see them through InternalsVisibleTo.
public sealed class ViewerHostTests
{
    const string ReportFixture = "pdfium-multiplatform-prototype/fixtures/report.pdf";

    internal static byte[] FixtureBytes(string relative) => Fixture(relative);

    static byte[] Fixture(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return File.ReadAllBytes(candidate);
            }
        }
        throw new FileNotFoundException($"missing fixture: {relative}");
    }

    sealed class RecordingDelegate : ViewerDelegate
    {
        public readonly ConcurrentQueue<(ulong Revision, int Thread)> Revisions = new();
        public readonly ConcurrentQueue<ViewerException> Reported = new();
        public void StateChanged(ulong revision) => Revisions.Enqueue((revision, Environment.CurrentManagedThreadId));
        public void Report(ViewerException error) => Reported.Enqueue(error);
    }

    sealed class RecordingListener : DocumentEventListener
    {
        public readonly TaskCompletionSource<DocumentInfo> Opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnEvent(DocumentEvent @event)
        {
            if (@event is DocumentEvent.Opened opened)
            {
                Opened.TrySetResult(opened.Document);
            }
        }
    }

    static async Task<T> WithHost<T>(ViewerConfig config, RecordingDelegate observer, Func<ViewerHost, Task<T>> body)
    {
        using var host = new ViewerHost(config, observer);
        await host.Start();
        try
        {
            return await body(host);
        }
        finally
        {
            await host.DestroyViewer();
            host.Shutdown();
        }
    }

    [Fact]
    public async Task OpensFixtureAndReadsPageCount()
    {
        var observer = new RecordingDelegate();
        var (info, pages) = await WithHost(new ViewerConfig(), observer, async host =>
        {
            var info = await host.OpenDocument(Fixture(ReportFixture), new OpenDocumentOptions(Name: "report.pdf"));
            var pages = await host.Pages(info.Id);
            return (info, pages);
        });
        Assert.Equal(DocumentStatus.Ready, info.Status);
        Assert.Equal("report.pdf", info.Name);
        Assert.Equal(14u, info.PageCount);
        Assert.Equal(14, pages.Length);
        Assert.Empty(observer.Reported);
    }

    [Fact]
    public async Task ViewerDelegateReceivesStateChangesFromRust()
    {
        var observer = new RecordingDelegate();
        var testThread = Environment.CurrentManagedThreadId;
        await WithHost(new ViewerConfig(), observer, async host =>
        {
            await host.OpenDocument(Fixture(ReportFixture), new OpenDocumentOptions());
            return host.StateRevision();
        });
        Assert.NotEmpty(observer.Revisions);
        var revisions = observer.Revisions.Select(r => r.Revision).ToArray();
        Assert.Equal(revisions.Order().ToArray(), revisions);
    }

    [Fact]
    public async Task DocumentListenerReceivesOpenedEvent()
    {
        var observer = new RecordingDelegate();
        var listener = new RecordingListener();
        var opened = await WithHost(new ViewerConfig(), observer, async host =>
        {
            using var subscription = host.SubscribeDocuments(listener);
            await host.OpenDocument(Fixture(ReportFixture), new OpenDocumentOptions(Name: "report.pdf"));
            var winner = await Task.WhenAny(listener.Opened.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.Same(listener.Opened.Task, winner);
            subscription.Cancel();
            return await listener.Opened.Task;
        });
        Assert.Equal(14u, opened.PageCount);
    }

    sealed class FailingRunner : CommandRunner
    {
        public int Derives;
        public int Accents;
        public void Run(CommandContext context, CommandCompletion completion) =>
            throw new InvalidOperationException("a refused command must not run");
        public bool Derive(CommandContext context, CommandHook hook)
        {
            Interlocked.Increment(ref Derives);
            throw new HostException.Failed("derive failed in C#");
        }
        public IconAccent? IconAccent(CommandContext context)
        {
            Interlocked.Increment(ref Accents);
            throw new HostException.Failed("accent failed in C#");
        }
        public CommandSettingLock? Locked(CommandContext context) => null;
    }

    sealed class ApprovingRunner : CommandRunner
    {
        public readonly ConcurrentQueue<string> Seen = new();
        public void Run(CommandContext context, CommandCompletion completion)
        {
            Seen.Enqueue(context.AppStateJson);
            completion.Finish();
        }
        public bool Derive(CommandContext context, CommandHook hook)
        {
            Seen.Enqueue(context.AppStateJson);
            return hook != CommandHook.Active || context.AppStateJson == "{\"status\":\"approved\"}";
        }
        public IconAccent? IconAccent(CommandContext context)
        {
            Seen.Enqueue(context.AppStateJson);
            return new IconAccent(Primary: context.AppStateJson == "{\"status\":\"approved\"}" ? "#0f0" : "#f00", Secondary: null);
        }
        public CommandSettingLock? Locked(CommandContext context) => null;
    }

    [Fact]
    public async Task CommandRunnerCallbacksCrossBothWays()
    {
        var observer = new RecordingDelegate();
        var approving = new ApprovingRunner();
        var failing = new FailingRunner();
        await WithHost(new ViewerConfig(Commands: true), observer, async host =>
        {
            var commands = await host.Commands();
            await commands.RegisterCommand(new CommandDefinition(
                Id: "app:inspect", LabelKey: "Inspect", Shortcuts: [], Categories: [], Runner: approving,
                Hooks: [CommandHook.Run, CommandHook.Enabled, CommandHook.Active, CommandHook.IconAccent]), false);
            await commands.SetAppStateJson("{\"status\":\"approved\"}");
            var resolved = await commands.ResolveCommand("app:inspect", null);
            Assert.NotNull(resolved);
            Assert.True(resolved.Active);
            Assert.Equal("#0f0", resolved.IconAccent?.Primary);
            await commands.Execute("app:inspect", null, null);

            await commands.RegisterCommand(new CommandDefinition(
                Id: "app:failure", LabelKey: "Failure", Shortcuts: [], Categories: [], Runner: failing,
                Hooks: [CommandHook.Enabled, CommandHook.IconAccent]), false);
            await commands.SetAppStateJson("{\"resolve\":1}");
            var refused = await commands.ResolveCommand("app:failure", null);
            Assert.NotNull(refused);
            Assert.False(refused.Enabled);
            Assert.Null(refused.IconAccent);
            return 0;
        });
        Assert.Contains("{\"status\":\"approved\"}", approving.Seen);
        Assert.Equal(1, failing.Derives);
        Assert.Equal(1, failing.Accents);
    }
}
