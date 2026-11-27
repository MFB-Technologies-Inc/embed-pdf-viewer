namespace EmbedPDF.Models.Tests;

/// <summary>Port of <c>ToolModelTests.swift</c>.</summary>
public class ToolModelTests
{
    static readonly ToolId Nobody = new("nobody-registered-this");

    sealed record Tools(PdfViewer Viewer, DocumentInfo Document, InteractionCapability Interaction, ToolModel Model, ToolId Fallback, Task Observing);

    /// <summary>A model over a viewer with the annotation tools, its RunAsync running and its first snapshot read.</summary>
    static async Task<Tools> ToolsAsync(SingleThreadContext context)
    {
        var (viewer, document, interaction, model) = await UnobservedAsync(context);
        var observing = model.RunAsync();
        var fallback = await interaction.GetDefaultToolIdAsync();
        await Observed.ExpectAsync(model, () => model.ActiveToolId, fallback);
        return new Tools(viewer, document, interaction, model, fallback, observing);
    }

    /// <summary>A model with no RunAsync running: its only reads are the ones a test causes.</summary>
    static async Task<(PdfViewer, DocumentInfo, InteractionCapability, ToolModel)> UnobservedAsync(SingleThreadContext context)
    {
        var viewer = await Fixture.StartedAsync(b => b.Interaction().Annotation());
        var document = await viewer.OpenAsync(Fixture.Data("annotation_kinds"));
        var interaction = await viewer.GetInteractionAsync(document);
        return (viewer, document, interaction, context.Watch(new ToolModel(interaction)));
    }

    static async Task EndAsync(Tools tools)
    {
        await tools.Viewer.DisposeAsync();
        await tools.Observing.WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>A permission a test flips.</summary>
    sealed class Gate
    {
        volatile bool open;
        public void Set(bool value) => open = value;
        public bool CanUse() => open;
    }

    static void SameTools(IReadOnlyList<InteractionTool> expected, IReadOnlyList<InteractionTool> actual) =>
        Assert.True(Structural.SameTools(expected, actual), "the tool lists differ");

    [Fact]
    public Task TheFirstSnapshotListsTheRegisteredToolsAsUsable() => SingleThreadContext.RunWatched(async context =>
    {
        var tools = await ToolsAsync(context);
        var kernel = await tools.Interaction.GetToolsAsync();
        SameTools(kernel, tools.Model.Tools);
        Assert.Contains(kernel, tool => tool.Id == ToolId.Ink);
        Assert.True(tools.Model.Usable.SetEquals(kernel.Select(tool => tool.Id)));
        Assert.Null(tools.Model.Error);
        await EndAsync(tools);
    });

    [Fact]
    public Task ActivateSetsTheActiveTool() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        await Observed.ExpectAsync(model, () => model.ActiveToolId, ToolId.Ink, () => model.ActivateAsync(ToolId.Ink));
        Assert.Null(model.Error);
        Assert.Equal(ToolId.Ink, await tools.Interaction.GetActiveToolIdAsync());
        await EndAsync(tools);
    });

    [Fact]
    public Task AToolActivatedOnTheCapabilityReachesTheModelThroughItsEvent() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        await Observed.ExpectAsync(tools.Model, () => tools.Model.ActiveToolId, ToolId.Ink, () => tools.Interaction.ActivateToolAsync(ToolId.Ink));
        await EndAsync(tools);
    });

    [Fact]
    public Task ActivateDefaultReturnsToTheDefaultTool() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        await Observed.ExpectAsync(model, () => model.ActiveToolId, ToolId.Ink, () => model.ActivateAsync(ToolId.Ink));
        await Observed.ExpectAsync(model, () => model.ActiveToolId, tools.Fallback, () => model.ActivateDefaultAsync());
        Assert.Null(model.Error);
        await EndAsync(tools);
    });

    [Fact]
    public Task AnUnregisteredToolSetsNotFoundAndLeavesTheActiveTool() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        await Observed.ExpectAsync(model, () => model.ActiveToolId, ToolId.Ink, () => model.ActivateAsync(ToolId.Ink));
        await model.ActivateAsync(Nobody);
        Assert.Equal(PdfErrorCode.NotFound, model.Error?.Code);
        Assert.Equal(ToolId.Ink, model.ActiveToolId);
        // The hub agrees, so no later read moves it.
        Assert.Equal(ToolId.Ink, await tools.Interaction.GetActiveToolIdAsync());
        await EndAsync(tools);
    });

    [Fact]
    public Task ALaterSuccessfulActivationClearsTheError() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        await model.ActivateAsync(Nobody);
        Assert.Equal(PdfErrorCode.NotFound, model.Error?.Code);
        await model.ActivateAsync(ToolId.Ink);
        Assert.Null(model.Error);
        await model.ActivateAsync(Nobody);
        Assert.Equal(PdfErrorCode.NotFound, model.Error?.Code);
        await model.ActivateDefaultAsync();
        Assert.Null(model.Error);
        await EndAsync(tools);
    });

    [Fact]
    public Task ABackgroundRefreshKeepsAnActionsError() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        await tools.Model.ActivateAsync(Nobody);
        await tools.Model.RefreshAsync();
        Assert.Equal(PdfErrorCode.NotFound, tools.Model.Error?.Code);
        await EndAsync(tools);
    });

    [Fact]
    public Task ClosingTheDocumentEndsTheTaskAndEmptiesTheModel() => SingleThreadContext.RunWatched(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        await Observed.ExpectAsync(model, () => model.ActiveToolId, ToolId.Ink, () => model.ActivateAsync(ToolId.Ink));
        await tools.Viewer.CloseAsync(tools.Document);
        await tools.Observing.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Empty(model.Tools);
        Assert.Null(model.ActiveToolId);
        Assert.Empty(model.Usable);
        Assert.Equal(PdfErrorCode.InstanceClosed, model.Error?.Code);
        await tools.Viewer.DisposeAsync();
    });

    static async Task<Registration> ExpectRegisteredAsync(ToolId id, Tools tools, Gate permission)
    {
        Registration? registration = null;
        var model = tools.Model;
        await Observed.ExpectAsync(model, () => model.Tools.Any(tool => tool.Id == id), true, async () =>
            registration = await tools.Interaction.RegisterToolAsync(new InteractionTool(id, "crosshair", []), permission: permission.CanUse));
        return registration!;
    }

    [Fact]
    public Task AToolRegisteredLaterReachesTheModelWithItsPermission() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        var gate = new Gate();
        var gated = new ToolId("gated");
        var registration = await ExpectRegisteredAsync(gated, tools, gate);
        Assert.DoesNotContain(gated, model.Usable);
        Assert.True(model.IsDisabled(gated));
        // The host's permission changes with no word from the viewer: the app refreshes.
        gate.Set(true);
        await Observed.ExpectAsync(model, () => model.Usable.Contains(gated), true, () => model.RefreshAsync());
        Assert.False(model.IsDisabled(gated));
        await Observed.ExpectAsync(model, () => model.Tools.Any(tool => tool.Id == gated), false, () =>
        {
            registration.Unregister();
            return Task.CompletedTask;
        });
        Assert.DoesNotContain(gated, model.Usable);
        await EndAsync(tools);
    });

    [Fact]
    public Task AButtonIsSelectedForTheActiveToolAndDisabledForAnUnusableOne() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        Assert.False(model.IsSelected(ToolId.Ink));
        Assert.False(model.IsDisabled(ToolId.Ink));
        Assert.True(model.IsDisabled(Nobody));
        await model.ActivateAsync(ToolId.Ink);
        Assert.True(model.IsSelected(ToolId.Ink));
        Assert.False(model.IsSelected(ToolId.Pan));
        Assert.False(model.IsDisabled(ToolId.Pan));
        await EndAsync(tools);
    });

    [Fact]
    public Task TheActiveToolsButtonStaysEnabledWhenItsToolIsNoLongerUsable() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        var gate = new Gate();
        gate.Set(true);
        var gated = new ToolId("gated");
        await ExpectRegisteredAsync(gated, tools, gate);
        await model.ActivateAsync(gated);
        Assert.True(model.IsSelected(gated));
        gate.Set(false);
        await Observed.ExpectAsync(model, () => model.Usable.Contains(gated), false, () => model.RefreshAsync());
        // Still the way back to the default tool.
        Assert.True(model.IsSelected(gated));
        Assert.False(model.IsDisabled(gated));
        await model.ButtonTappedAsync(gated);
        Assert.Equal(tools.Fallback, model.ActiveToolId);
        Assert.True(model.IsDisabled(gated));
        await EndAsync(tools);
    });

    /// <summary>Every ToolChanged the hub sends from now until the viewer is disposed.</summary>
    static Task<List<InteractionEvent.ToolChanged>> ToolChanges(InteractionCapability interaction)
    {
        var events = interaction.ObserveEvents();
        return Task.Run(async () =>
        {
            var changes = new List<InteractionEvent.ToolChanged>();
            await foreach (var item in events)
            {
                if (item is InteractionEvent.ToolChanged change) changes.Add(change);
            }
            return changes;
        });
    }

    [Fact]
    public Task ASecondTapWhileAnActivationIsInFlightIsIgnored() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        var changes = ToolChanges(tools.Interaction);
        var first = model.ButtonTappedAsync(ToolId.Ink);
        var second = model.ButtonTappedAsync(ToolId.Ink);
        await Task.WhenAll(first, second);
        // Neither toggled off nor activated twice.
        Assert.Equal(ToolId.Ink, model.ActiveToolId);
        Assert.False(model.Submitting);
        await EndAsync(tools);
        Assert.Equal([new InteractionEvent.ToolChanged(ToolId.Ink, tools.Fallback)], await changes.WaitAsync(TimeSpan.FromSeconds(10)));
    });

    [Fact]
    public Task ARequestForAnotherToolWhileAnActivationIsInFlightIsIgnored() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        var changes = ToolChanges(tools.Interaction);
        var first = model.ActivateAsync(ToolId.Ink);
        var second = model.ActivateAsync(ToolId.Pan);
        await Task.WhenAll(first, second);
        Assert.Equal(ToolId.Ink, model.ActiveToolId);
        Assert.False(model.Submitting);
        await EndAsync(tools);
        Assert.Equal([new InteractionEvent.ToolChanged(ToolId.Ink, tools.Fallback)], await changes.WaitAsync(TimeSpan.FromSeconds(10)));
    });

    [Fact]
    public Task EveryButtonIsDisabledWhileTheHubCallIsOut() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        Assert.False(model.Submitting);
        bool[] duringCall = [];
        model.HubCallStarting = () =>
        {
            duringCall = [model.Submitting, model.IsDisabled(ToolId.Ink), model.IsDisabled(ToolId.Pan)];
            return Task.CompletedTask;
        };
        await model.ActivateAsync(ToolId.Ink);
        model.HubCallStarting = null;
        Assert.Equal([true, true, true], duringCall);
        Assert.Equal([false, false, false], new[] { model.Submitting, model.IsDisabled(ToolId.Ink), model.IsDisabled(ToolId.Pan) });
        Assert.Equal(ToolId.Ink, model.ActiveToolId);
        await EndAsync(tools);
    });

    /// <summary>The guard covers the hub call alone: by the time the action's read has finished, the active tool is written and the buttons are back.</summary>
    [Fact]
    public Task TheButtonsAreEnabledAgainBeforeTheActivationsRead() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, _, model) = await UnobservedAsync(context);
        await model.RefreshAsync();
        bool[] duringRead = [];
        ToolId? activeDuringRead = null;
        model.Snapshots.ReadFinished = () =>
        {
            duringRead = [model.Submitting, model.IsDisabled(ToolId.Ink), model.IsDisabled(ToolId.Pan), model.IsSelected(ToolId.Ink)];
            activeDuringRead = model.ActiveToolId;
            return Task.CompletedTask;
        };
        await model.ActivateAsync(ToolId.Ink);
        model.Snapshots.ReadFinished = null;
        Assert.Equal([false, false, false, true], duringRead);
        Assert.Equal(ToolId.Ink, activeDuringRead);
        await viewer.DisposeAsync();
    });

    static async Task<(IReadOnlyList<InteractionTool> Tools, ToolId Active, HashSet<ToolId> Usable)> KernelSnapshotAsync(InteractionCapability interaction)
    {
        var tools = await interaction.GetToolsAsync();
        var usable = new HashSet<ToolId>();
        foreach (var tool in tools)
        {
            if (await interaction.CanUseToolAsync(tool.Id)) usable.Add(tool.Id);
        }
        return (tools, await interaction.GetActiveToolIdAsync(), usable);
    }

    /// <summary>The action writes the active tool itself. Here its read is dropped, so nothing else could have, and Tools trails by that one read.</summary>
    [Fact]
    public Task TheActiveToolIsTheRequestedOneWhenActivateReturnsWithNoTaskRunning() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, interaction, model) = await UnobservedAsync(context);
        model.Snapshots.ReadFinished = () => { model.Snapshots.Invalidate(); return Task.CompletedTask; };
        await model.ActivateAsync(ToolId.Ink);
        model.Snapshots.ReadFinished = null;
        Assert.Equal(ToolId.Ink, model.ActiveToolId);
        Assert.False(model.Submitting);
        Assert.Null(model.Error);
        Assert.Empty(model.Tools);
        Assert.Equal(ToolId.Ink, await interaction.GetActiveToolIdAsync());
        await model.RefreshAsync();
        var snapshot = await KernelSnapshotAsync(interaction);
        SameTools(snapshot.Tools, model.Tools);
        Assert.True(model.Usable.SetEquals(snapshot.Usable));
        Assert.Equal(ToolId.Ink, model.ActiveToolId);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task TheActiveToolIsTheDefaultWhenActivateDefaultReturnsWithNoTaskRunning() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, interaction, model) = await UnobservedAsync(context);
        var fallback = await interaction.GetDefaultToolIdAsync();
        await model.ActivateAsync(ToolId.Ink);
        model.Snapshots.ReadFinished = () => { model.Snapshots.Invalidate(); return Task.CompletedTask; };
        await model.ActivateDefaultAsync();
        model.Snapshots.ReadFinished = null;
        Assert.Equal(fallback, model.ActiveToolId);
        Assert.False(model.Submitting);
        await viewer.DisposeAsync();
    });

    /// <summary>A read that started before the hub answered holds the tool from before. Landing after the action, it must not put that tool back.</summary>
    [Fact]
    public Task AReadFromBeforeTheActivationDoesNotPutTheOldToolBack() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, _, model) = await UnobservedAsync(context);
        await model.RefreshAsync();
        var (reached, release) = Observed.HoldFirstRead(model.Snapshots);
        var stale = model.RefreshAsync();
        await reached;
        await model.ActivateAsync(ToolId.Ink);
        Assert.Equal(ToolId.Ink, model.ActiveToolId);
        release();
        await stale;
        Assert.Equal(ToolId.Ink, model.ActiveToolId);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task AFailedActivationEndsTheFlight() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        await tools.Model.ActivateAsync(Nobody);
        Assert.Equal(PdfErrorCode.NotFound, tools.Model.Error?.Code);
        Assert.False(tools.Model.Submitting);
        Assert.False(tools.Model.IsDisabled(ToolId.Ink));
        await EndAsync(tools);
    });

    [Fact]
    public Task TappingAnotherToolActivatesItAndTappingTheActiveToolReturnsToTheDefault() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        await Observed.ExpectAsync(model, () => model.ActiveToolId, ToolId.Ink, () => model.ButtonTappedAsync(ToolId.Ink));
        await Observed.ExpectAsync(model, () => model.ActiveToolId, tools.Fallback, () => model.ButtonTappedAsync(ToolId.Ink));
        await EndAsync(tools);
    });

    [Fact]
    public Task AnActivationCompletesWhileStateRevisionsKeepArriving() => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        var interaction = tools.Interaction;
        // Each registration and each removal is a state revision.
        var churning = true;
        var rounds = 0;
        var churn = Task.Run(async () =>
        {
            var tool = new InteractionTool(new ToolId("churn"), "crosshair", []);
            for (var i = 0; i < 3000 && Volatile.Read(ref churning); i++)
            {
                Registration registration;
                try { registration = await interaction.RegisterToolAsync(tool, replace: true); }
                catch (PdfException) { break; }
                registration.Unregister();
                Interlocked.Increment(ref rounds);
            }
            Volatile.Write(ref churning, false);
        });
        // Revisions are arriving before the activation starts. Swift waits to
        // see the churned tool in Tools; here the churn runs on the thread
        // pool and can outpace every read, so the test counts its rounds.
        while (Volatile.Read(ref rounds) < 50) await Task.Delay(5);
        await model.ActivateAsync(ToolId.Ink);
        var finishedDuringTheRevisions = Volatile.Read(ref churning);
        Volatile.Write(ref churning, false);
        await churn;
        Assert.True(finishedDuringTheRevisions);
        Assert.False(model.Submitting);
        Assert.Null(model.Error);
        // At rest the snapshots are the hub's.
        var kernel = await KernelSnapshotAsync(interaction);
        await Observed.UntilAsync(model, () => Structural.SameTools(model.Tools, kernel.Tools));
        await Observed.UntilAsync(model, () => model.Usable.SetEquals(kernel.Usable));
        Assert.Equal(kernel.Active, model.ActiveToolId);
        Assert.Equal(ToolId.Ink, kernel.Active);
        Assert.DoesNotContain(kernel.Tools, tool => tool.Id.Value == "churn");
        await EndAsync(tools);
    });

    /// <summary>Cancels an activation at <paramref name="point"/> and expects the model, once at rest with RunAsync running, to hold what the hub holds.</summary>
    static Task ExpectAtRestAfterCancelling(Func<CancellationTokenSource, ToolModel, Task> point) => SingleThreadContext.Run(async context =>
    {
        var tools = await ToolsAsync(context);
        var model = tools.Model;
        using var activation = new CancellationTokenSource();
        var activating = model.ActivateAsync(ToolId.Ink, activation.Token);
        await point(activation, model);
        await activating;
        model.Snapshots.ReadFinished = null;
        Assert.True(activation.IsCancellationRequested);
        Assert.False(model.Submitting);
        // The hub call has returned, so the hub's tool no longer moves.
        var kernel = await KernelSnapshotAsync(tools.Interaction);
        await Observed.ExpectAsync(model, () => model.ActiveToolId, kernel.Active);
        await Observed.UntilAsync(model, () => Structural.SameTools(model.Tools, kernel.Tools));
        await Observed.UntilAsync(model, () => model.Usable.SetEquals(kernel.Usable));
        Assert.False(model.Submitting);
        await EndAsync(tools);
    });

    [Fact]
    public Task AnActivationCancelledBeforeTheHubCallLeavesTheModelCurrent() =>
        ExpectAtRestAfterCancelling((activation, _) => activation.CancelAsync());

    [Fact]
    public Task AnActivationCancelledDuringTheHubCallLeavesTheModelCurrent() =>
        ExpectAtRestAfterCancelling(async (activation, model) =>
        {
            for (var i = 0; i < 1000 && !model.Submitting; i++) await Task.Yield();
            activation.Cancel();
        });

    /// <summary>The cancelled token drops the action's read. The active tool is already written, and the read RunAsync makes for the action's event brings the rest.</summary>
    [Fact]
    public Task AnActivationCancelledDuringItsReadLeavesTheModelCurrent() =>
        ExpectAtRestAfterCancelling((activation, model) =>
        {
            model.Snapshots.ReadFinished = () =>
            {
                if (model.ActiveToolId == ToolId.Ink) activation.Cancel();
                return Task.CompletedTask;
            };
            return Task.CompletedTask;
        });
}
