using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>A value shared between a test and the delegates it hands the viewer.</summary>
sealed class Shared<T>(T value)
{
    readonly Lock gate = new();
    T current = value;

    public T Value
    {
        get { lock (gate) return current; }
    }

    public void With(Func<T, T> change)
    {
        lock (gate) current = change(current);
    }
}

public sealed class CommandsTests
{
    const string Undefined = """{"$undefined":true}""";
    const string Approved = """{"status":"approved"}""";
    const string LoneSurrogate = "{\"name\":\"\uD800\"}";

    internal static async Task<(PdfViewer Viewer, CommandsCapability Commands)> Started()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Commands = true });
        return (viewer, await viewer.GetCommandsAsync());
    }

    static async Task<T> First<T>(IAsyncEnumerable<CommandsEvent> events) where T : CommandsEvent
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var e in events.WithCancellation(timeout.Token))
        {
            if (e is T found) return found;
        }
        throw new InvalidOperationException("the stream ended first");
    }

    static readonly CommandKeyStroke CtrlL = new("l", Ctrl: true);

    [Fact]
    public async Task ASettingLockDisablesAndReportsItsRefusalUntilReleased()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        var held = new Shared<bool>(true);
        var ran = new Shared<int>(0);
        var handler = new CommandHandler(
            run: _ => { ran.With(n => n + 1); return Task.CompletedTask; },
            active: _ => true,
            locked: _ => held.Value ? new CommandSettingLock("kiosk") : null);
        _ = await commands.RegisterAsync(new CommandDefinition("view:single", "Single page") { Shortcuts = ["Ctrl+L"], Handler = handler });
        var locked = Assert.IsType<ResolvedCommand>(await commands.ResolveAsync("view:single"));
        Assert.True(locked.Visible && locked.Active && !locked.Enabled);
        Assert.Equal(CommandRefusalReason.Locked, (await commands.MatchStrokeAsync(CtrlL, isMac: false))?.Refusal?.Reason);
        Assert.Equal(new CommandExecuteResult.Rejected(CommandRejectReason.Disabled), await commands.ExecuteAsync("view:single"));
        Assert.Equal(0, ran.Value);
        held.With(_ => false);
        Assert.Equal(new CommandExecuteResult.Executed(), await commands.ExecuteAsync("view:single"));
        Assert.Equal(1, ran.Value);
    }

    [Fact]
    public async Task EveryHookReceivesTheDefaultAppStateJson()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        var seen = new Shared<List<string>>([]);
        void Record(CommandContext c) => seen.With(l => [.. l, c.AppStateJson]);
        var handler = new CommandHandler(
            run: c => { Record(c); return Task.CompletedTask; },
            enabled: c => { Record(c); return true; },
            active: c => { Record(c); return false; },
            visible: c => { Record(c); return true; },
            iconAccent: c => { Record(c); return null; },
            locked: c => { Record(c); return null; });
        _ = await commands.RegisterAsync(new CommandDefinition("app:inspect", "Inspect") { Handler = handler });
        _ = await commands.ResolveAsync("app:inspect");
        _ = await commands.ExecuteAsync("app:inspect");
        Assert.NotEmpty(seen.Value);
        Assert.All(seen.Value, value => Assert.Equal(Undefined, value));
    }

    [Fact]
    public async Task EveryHookReceivesTheWholeReplacementAppState()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        var seen = new Shared<List<string>>([]);
        var runs = new Shared<List<CommandContext>>([]);
        void Record(CommandContext c) => seen.With(l => [.. l, c.AppStateJson]);
        static bool IsApproved(CommandContext c) => c.AppStateJson == Approved;
        var handler = new CommandHandler(
            run: c => { Record(c); runs.With(l => [.. l, c]); return Task.CompletedTask; },
            enabled: c => { Record(c); return true; },
            active: c => { Record(c); return IsApproved(c); },
            visible: c => { Record(c); return true; },
            iconAccent: c => { Record(c); return new IconAccent(IsApproved(c) ? "#0f0" : "#f00", null); });
        using var registration = await commands.RegisterAsync(new CommandDefinition("app:inspect", "Inspect") { Handler = handler });
        await commands.DisableCategoryAsync("hidden");
        foreach (var json in new[] { """{"omitted":true,"status":"review"}""", Approved, "null" })
        {
            await commands.SetAppStateAsync(json);
            seen.With(_ => []);
            var resolved = await commands.ResolveAsync("app:inspect");
            Assert.Equal(new CommandExecuteResult.Executed(), await commands.ExecuteAsync("app:inspect", argsJson: """{"copy":true}"""));
            Assert.Equal(json == Approved, resolved?.Active);
            Assert.Equal(json == Approved ? "#0f0" : "#f00", resolved?.IconAccent?.Primary);
            // Each derivation is asked at most once per store change.
            Assert.True(seen.Value.Count >= 7, $"{seen.Value.Count}");
            Assert.All(seen.Value, value => Assert.Equal(json, value));
            Assert.Equal("""{"copy":true}""", runs.Value[^1].ArgsJson);
            Assert.Equal(["hidden"], await commands.GetDisabledCategoriesAsync());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppStateUpdatedDuringRunIsCurrentAfterCompletion(bool fails)
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        var ran = new Shared<List<string>>([]);
        await commands.SetAppStateAsync("""{"status":"review"}""");
        var handler = new CommandHandler(
            run: async c =>
            {
                ran.With(l => [.. l, c.AppStateJson]);
                await commands.SetAppStateAsync(Approved);
                if (fails) throw new IOException("full");
            },
            active: c => c.AppStateJson == Approved,
            locked: c => c.AppStateJson == Approved ? new CommandSettingLock("app-state") : null);
        using var registration = await commands.RegisterAsync(new CommandDefinition("app:update", "Update") { Shortcuts = ["Ctrl+L"], Handler = handler });
        var before = await commands.ResolveAsync("app:update");
        Assert.False(before?.Active);
        Assert.True(before?.Enabled);
        if (fails)
        {
            Assert.Equal(PdfErrorCode.OperationFailed, (await Assert.ThrowsAsync<PdfException>(() => commands.ExecuteAsync("app:update"))).Code);
        }
        else
        {
            Assert.Equal(new CommandExecuteResult.Executed(), await commands.ExecuteAsync("app:update"));
        }
        Assert.Equal(["""{"status":"review"}"""], ran.Value);
        var after = await commands.ResolveAsync("app:update");
        Assert.True(after?.Active);
        Assert.False(after?.Enabled);
        Assert.Equal(CommandRefusalReason.Locked, (await commands.MatchStrokeAsync(CtrlL, isMac: false))?.Refusal?.Reason);
    }

    [Fact]
    public async Task InvalidAppStatePreservesThePreviousValue()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        var seen = new Shared<List<string>>([]);
        var handler = new CommandHandler(enabled: c => { seen.With(l => [.. l, c.AppStateJson]); return true; });
        using var registration = await commands.RegisterAsync(new CommandDefinition("app:inspect", "Inspect") { Handler = handler });
        await commands.SetAppStateAsync("""{"kept":true}""");
        _ = await commands.ResolveAsync("app:inspect");
        foreach (var invalid in new[] { "{broken", LoneSurrogate })
        {
            Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => commands.SetAppStateAsync(invalid))).Code);
            // A refused value makes no store change, so the hook isn't asked again.
            _ = await commands.ResolveAsync("app:inspect");
        }
        Assert.NotEmpty(seen.Value);
        Assert.All(seen.Value, value => Assert.Equal("""{"kept":true}""", value));
    }

    [Fact]
    public async Task ACommandRunsAsync()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        var ran = new Shared<List<string>>([]);
        var handler = new CommandHandler(run: async c =>
        {
            await Task.Delay(10);
            ran.With(l => [.. l, c.CommandId]);
        });
        _ = await commands.RegisterAsync(new CommandDefinition("doc:save", "Save") { Handler = handler });
        var events = commands.ObserveEvents();
        Assert.Equal(new CommandExecuteResult.Executed(), await commands.ExecuteAsync("doc:save", argsJson: """{"copy":true}"""));
        Assert.Equal(["doc:save"], ran.Value);
        Assert.Equal("""{"copy":true}""", (await First<CommandsEvent.Executed>(events)).ArgsJson);
    }

    [Fact]
    public async Task ArgsWithAnUnpairedSurrogateAreInvalidInput()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        var ran = new Shared<bool>(false);
        _ = await commands.RegisterAsync(new CommandDefinition("doc:save", "Save")
        {
            Handler = new CommandHandler(run: _ => { ran.With(_ => true); return Task.CompletedTask; }),
        });
        var error = await Assert.ThrowsAsync<PdfException>(() => commands.ExecuteAsync("doc:save", argsJson: LoneSurrogate));
        Assert.Equal((PdfErrorCode.InvalidInput, "EncodingError"), (error.Code, error.Name));
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => commands.ExecuteAsync("doc:save", argsJson: "{"))).Code);
        Assert.False(ran.Value);
    }

    static async Task<PdfException> Failing(Func<CommandContext, Task> run)
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        _ = await commands.RegisterAsync(new CommandDefinition("doc:save", "Save") { Handler = new CommandHandler(run: run) });
        return await Assert.ThrowsAsync<PdfException>(() => commands.ExecuteAsync("doc:save"));
    }

    [Fact]
    public async Task AThrowingCommandFails() =>
        Assert.Equal(PdfErrorCode.OperationFailed, (await Failing(_ => throw new InvalidOperationException("full"))).Code);

    [Fact]
    public async Task AThrownPdfExceptionKeepsItsCode()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        _ = await commands.RegisterAsync(new CommandDefinition("doc:save", "Save")
        {
            Handler = new CommandHandler(run: _ => throw new PdfException("PluginError", PdfErrorCode.PermissionDenied, "no doc.annotate")),
        });
        var events = commands.ObserveEvents();
        var error = await Assert.ThrowsAsync<PdfException>(() => commands.ExecuteAsync("doc:save"));
        Assert.Equal((PdfErrorCode.PermissionDenied, "no doc.annotate"), (error.Code, error.Detail));
        var failed = await First<CommandsEvent.ExecutionFailed>(events);
        Assert.Equal((PdfErrorCode.PermissionDenied, "no doc.annotate"), (failed.Error.Code, failed.Error.Message));
    }

    [Fact]
    public async Task ACancelledRunIsOperationCancelled() =>
        Assert.Equal(PdfErrorCode.OperationCancelled, (await Failing(_ => throw new OperationCanceledException())).Code);

    [Fact]
    public async Task AnUncodedErrorIsOperationFailedWithItsMessage()
    {
        var error = await Failing(_ => Task.FromException(new IOException("the disk is full")));
        Assert.Equal((PdfErrorCode.OperationFailed, "the disk is full"), (error.Code, error.Detail));
    }

    [Fact]
    public async Task ADerivationGatesExecution()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        _ = await commands.RegisterAsync(new CommandDefinition("doc:save", "Save")
        {
            Handler = new CommandHandler(run: _ => Task.CompletedTask, enabled: _ => false),
        });
        Assert.Equal(new CommandExecuteResult.Rejected(CommandRejectReason.Disabled), await commands.ExecuteAsync("doc:save"));
        Assert.False(await commands.CanExecuteAsync("doc:save"));
    }

    [Fact]
    public async Task AShortcutMatches()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        _ = await commands.RegisterAsync(new CommandDefinition("zoom:in", "Zoom in") { Shortcuts = ["Mod+="] });
        Assert.Equal(new CommandShortcutMatch("zoom:in", null), await commands.MatchStrokeAsync(new CommandKeyStroke("=", Meta: true), isMac: true));
        Assert.Equal([new CommandShortcutBinding("zoom:in", "Mod+=")], await commands.GetShortcutsAsync());
    }

    [Fact]
    public async Task UnregisterRemovesTheCommand()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        var registration = await commands.RegisterAsync(new CommandDefinition("zoom:in", "Zoom in"));
        registration.Unregister();
        Assert.False(await commands.HasCommandAsync("zoom:in"));
    }

    [Fact]
    public async Task TheRegistryReadsAndTogglesCommands()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        _ = await commands.RegisterAsync([
            new CommandDefinition("zoom:in", "Zoom in") { Categories = ["zoom"] },
            new CommandDefinition("view:menu", "View") { Menu = "view-menu", Panel = new CommandPanel("sidebar") { Exclusive = "left" } },
        ]);
        Assert.Equal(["zoom:in", "view:menu"], await commands.GetCommandIdsAsync());
        Assert.Equal(new CommandMenuTarget("view-menu"), await commands.GetMenuTargetAsync("view:menu"));
        var summary = Assert.IsType<CommandSummary>(await commands.GetCommandAsync("view:menu"));
        Assert.Equal("left", summary.Panel?.Exclusive);
        Assert.False(summary.HasRunner);
        Assert.Equal(2, (await commands.GetCommandsAsync()).Count);
        Assert.Contains(await commands.SearchAsync("Zoom"), c => c.Id == "zoom:in");
        await commands.DisableCommandAsync("zoom:in");
        Assert.True(await commands.IsCommandDisabledAsync("zoom:in"));
        Assert.Equal(CommandRefusalReason.TurnedOff, (await commands.GetRefusalAsync("zoom:in"))?.Reason);
        await commands.EnableCommandAsync("zoom:in");
        await commands.SetDisabledCommandsAsync(["zoom:in"]);
        Assert.Equal(["zoom:in"], await commands.GetDisabledCommandsAsync());
        await commands.SetDisabledCommandsAsync([]);
        await commands.DisableCategoryAsync("zoom");
        Assert.True(await commands.IsCategoryDisabledAsync("zoom"));
        await commands.EnableCategoryAsync("zoom");
        await commands.SetDisabledCategoriesAsync(["zoom"]);
        Assert.Equal(["zoom"], await commands.GetDisabledCategoriesAsync());
    }

    static async Task<WeakReference> AbandonAfterUnregister()
    {
        var (viewer, commands) = await Started();
        var handler = new CommandHandler(run: async context => _ = await commands.GetCommandIdsAsync());
        var registration = await commands.RegisterAsync(new CommandDefinition("doc:list", "List") { Handler = handler });
        registration.Unregister();
        // A reply after the unregister means it has run.
        _ = await commands.GetCommandIdsAsync();
        await viewer.DisposeAsync();
        return new WeakReference(viewer);
    }

    [Fact]
    public async Task UnregisteringAHandlerThatHoldsTheCapabilityReleasesTheViewer()
    {
        var reference = await AbandonAfterUnregister();
        for (var i = 0; i < 20 && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(20);
        }
        Assert.False(reference.IsAlive);
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        _ = await commands.RegisterAsync(new CommandDefinition("zoom:in", "Zoom in"));
        using var cancel = new CancellationTokenSource();
        var reading = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in commands.ObserveEvents(cancel.Token)) { }
            }
            catch (OperationCanceledException) { }
        });
        await Task.Delay(20);
        cancel.Cancel();
        await reading;
        _ = await commands.ExecuteAsync("zoom:in");
    }

    [Fact]
    public async Task ADisposedViewersCapabilityThrows()
    {
        var (viewer, commands) = await Started();
        await viewer.DisposeAsync();
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => commands.GetCommandIdsAsync())).Code);
    }

    [Fact]
    public async Task AThrowingDerivationLeavesTheRegistryWorking()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        _ = await commands.RegisterAsync(new CommandDefinition("app:broken", "Broken")
        {
            Handler = new CommandHandler(run: _ => Task.CompletedTask, enabled: _ => throw new InvalidOperationException("no state")),
        });
        _ = await commands.RegisterAsync(new CommandDefinition("app:fine", "Fine") { Handler = new CommandHandler(run: _ => Task.CompletedTask) });
        var broken = await commands.ResolveAsync("app:broken");
        Assert.True(broken is null || !broken.Enabled);
        Assert.True((await commands.ResolveAsync("app:fine"))?.Enabled);
        Assert.Equal(new CommandExecuteResult.Executed(), await commands.ExecuteAsync("app:fine"));
    }

    [Fact]
    public async Task DisposeFromADerivationThrowsInvalidOperation()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        var seen = new Shared<Exception?>(null);
        _ = await commands.RegisterAsync(new CommandDefinition("app:inspect", "Inspect")
        {
            Handler = new CommandHandler(enabled: _ =>
            {
                try
                {
                    viewer.Dispose();
                }
                catch (Exception error)
                {
                    seen.With(_ => error);
                }
                return true;
            }),
        });
        Assert.True((await commands.ResolveAsync("app:inspect"))?.Enabled);
        Assert.IsType<InvalidOperationException>(seen.Value);
        Assert.NotEmpty(await commands.GetCommandIdsAsync());
    }

    [Fact]
    public async Task DisposeFromARunDisposes()
    {
        var (viewer, commands) = await Started();
        var disposed = new TaskCompletionSource();
        _ = await commands.RegisterAsync(new CommandDefinition("app:quit", "Quit")
        {
            Handler = new CommandHandler(run: context =>
            {
                _ = Task.Run(() => { viewer.Dispose(); disposed.SetResult(); });
                return Task.CompletedTask;
            }),
        });
        _ = await commands.ExecuteAsync("app:quit");
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => commands.GetCommandIdsAsync())).Code);
    }

    /// <summary>The commands case of PluginSettingsTests.swift.</summary>
    [Fact]
    public async Task CommandSettingsValidateOpenAppDataAndReset()
    {
        var (viewer, commands) = await Started();
        await using var _v = viewer;
        var registered = await commands.GetSettingsAsync();
        await commands.UpdateSettingsAsync(new CommandsSettingsPatch
        {
            DisabledCategories = ["edit"], DisabledCommands = ["print"], NotAllowed = NotAllowedMode.Disable, AppStateJson = "null",
        });
        var changed = await commands.GetSettingsAsync();
        Assert.Equal("null", changed.AppStateJson);
        await Assert.ThrowsAsync<PdfException>(() => commands.UpdateSettingsAsync(new CommandsSettingsPatch { AppStateJson = "{" }));
        var after = await commands.GetSettingsAsync();
        Assert.Equal(changed.DisabledCategories, after.DisabledCategories);
        Assert.Equal(changed.DisabledCommands, after.DisabledCommands);
        Assert.Equal((changed.NotAllowed, changed.AppStateJson), (after.NotAllowed, after.AppStateJson));
        await commands.ResetSettingsAsync();
        var reset = await commands.GetSettingsAsync();
        Assert.Equal(registered.DisabledCategories, reset.DisabledCategories);
        Assert.Equal(registered.DisabledCommands, reset.DisabledCommands);
        Assert.Equal((registered.NotAllowed, registered.AppStateJson), (reset.NotAllowed, reset.AppStateJson));
    }
}

sealed class RecordingPlatform : IStandardCommandsPlatform
{
    readonly Lock gate = new();
    readonly List<(byte[] Bytes, string Name, string Type)> files = [];

    public IReadOnlyList<(byte[] Bytes, string Name, string Type)> Saved
    {
        get { lock (gate) return [.. files]; }
    }

    public void SaveFile(byte[] bytes, string fileName, string contentType)
    {
        lock (gate) files.Add((bytes, fileName, contentType));
    }

    public Task CopyTextAsync(string text) => Task.CompletedTask;

    public Task CopyImageAsync(byte[] png) => Task.CompletedTask;
}

public sealed class StandardCommandsTests
{
    static PdfViewerOptions Options => new PdfViewerOptionsBuilder().Interaction().History().Selection().Annotation("").Commands()
        .StandardCommands([StandardCommandList.Document, StandardCommandList.History]).Build();

    [Fact]
    public async Task TheStandardCommandsJoinInUpstreamsOrderAndReachThePlatform()
    {
        await using var viewer = await PdfViewer.CreateAsync(Options);
        var platform = new RecordingPlatform();
        viewer.SetCommandsPlatform(platform);
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var commands = await viewer.GetCommandsAsync();
        var ids = await commands.GetCommandIdsAsync();
        Assert.True(ids.ToList().IndexOf("history:undo") < ids.ToList().IndexOf("document:download"), string.Join(",", ids));
        Assert.Equal(new CommandExecuteResult.Executed(), await commands.ExecuteAsync("document:download", document));
        var saved = Assert.Single(platform.Saved);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(saved.Bytes, 0, 4));
        Assert.Equal("application/pdf", saved.Type);
    }

    [Fact]
    public async Task WithoutAPlatformASaveSavesNothing()
    {
        await using var viewer = await PdfViewer.CreateAsync(Options);
        var platform = new RecordingPlatform();
        viewer.SetCommandsPlatform(platform);
        viewer.SetCommandsPlatform(null);
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var commands = await viewer.GetCommandsAsync();
        _ = await commands.ExecuteAsync("document:download", document);
        Assert.Empty(platform.Saved);
    }

    [Fact]
    public async Task AThrowingSaveFileIsDropped()
    {
        await using var viewer = await PdfViewer.CreateAsync(Options);
        viewer.SetCommandsPlatform(new ThrowingPlatform());
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var commands = await viewer.GetCommandsAsync();
        _ = await commands.ExecuteAsync("document:download", document);
        Assert.NotEmpty(await commands.GetCommandIdsAsync());
    }

    [Fact]
    public async Task SettingAPlatformAfterDisposalIsInstanceClosed()
    {
        var viewer = await PdfViewer.CreateAsync(Options);
        await viewer.DisposeAsync();
        Assert.Equal(PdfErrorCode.InstanceClosed, Assert.Throws<PdfException>(() => viewer.SetCommandsPlatform(null)).Code);
    }

    sealed class ThrowingPlatform : IStandardCommandsPlatform
    {
        public void SaveFile(byte[] bytes, string fileName, string contentType) => throw new IOException("no disk");

        public Task CopyTextAsync(string text) => Task.CompletedTask;

        public Task CopyImageAsync(byte[] png) => Task.CompletedTask;
    }
}
