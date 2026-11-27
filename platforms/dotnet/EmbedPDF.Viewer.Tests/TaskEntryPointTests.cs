using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// <see cref="PageNavigator"/>, <see cref="PageZoom"/> and <see cref="ToolSwitcher"/>
/// add names, not behaviour. Every member forwards to the capability member
/// of the same name: reflection proves the two exist with the same
/// parameters and result, the source audit proves each facade member is a
/// one-line forward, and the equivalence tests run each member both ways on
/// fresh documents and compare what the engine reports.
/// </summary>
public sealed partial class TaskEntryPointTests
{
    static readonly Dictionary<Type, (Type Capability, string Property)> Facades = new()
    {
        [typeof(PageNavigator)] = (typeof(StageCapability), "Stage"),
        [typeof(PageZoom)] = (typeof(StageCapability), "Stage"),
        [typeof(ToolSwitcher)] = (typeof(InteractionCapability), "Interaction"),
    };

    static IEnumerable<MethodInfo> Members(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.Name != nameof(IDisposable.Dispose));

    public static TheoryData<Type> FacadeTypes => [typeof(PageNavigator), typeof(PageZoom), typeof(ToolSwitcher)];

    [Theory]
    [MemberData(nameof(FacadeTypes))]
    public void EveryFacadeMemberHasTheCapabilityMembersSignature(Type facade)
    {
        var capability = Facades[facade].Capability;
        foreach (var member in Members(facade))
        {
            var parameters = member.GetParameters().Select(p => p.ParameterType).ToArray();
            var twin = capability.GetMethod(member.Name, parameters);
            Assert.True(twin is not null, $"{facade.Name}.{member.Name} has no capability twin");
            Assert.Equal(twin.ReturnType, member.ReturnType);
            Assert.Equal(twin.GetParameters().Select(p => (p.Name, p.HasDefaultValue)), member.GetParameters().Select(p => (p.Name, p.HasDefaultValue)));
        }
    }

    [Theory]
    [MemberData(nameof(FacadeTypes))]
    public void EveryFacadeMemberIsAOneLineForward(Type facade)
    {
        var source = File.ReadAllText(SourcePath());
        var start = source.IndexOf($"public sealed class {facade.Name} : IDisposable", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = source.IndexOf("\n}\n", start, StringComparison.Ordinal);
        var property = Facades[facade].Property;
        var names = new List<string>();
        foreach (var line in source[start..end].Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("public Task", StringComparison.Ordinal)))
        {
            var name = MemberName().Match(line).Groups[1].Value;
            names.Add(name);
            Assert.True(line.EndsWith(';'), $"{facade.Name}.{name} spans more than one line");
            Assert.Contains($"=> {property}.{name}(", line);
        }
        Assert.Equal(Members(facade).Select(m => m.Name).Order(), names.Order());
        Assert.Equal(Steps(facade).Order(), names.Order());
    }

    static string SourcePath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "platforms/dotnet/EmbedPDF.Viewer/Plugins/TaskEntryPoints.cs");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("TaskEntryPoints.cs");
    }

    [GeneratedRegex(@"\s(\w+)\(")]
    private static partial Regex MemberName();

    static IEnumerable<string> Steps(Type facade) =>
        facade == typeof(PageNavigator) ? NavigatorSteps.Keys : facade == typeof(PageZoom) ? ZoomSteps.Keys : ToolSteps.Keys;

    // Equivalence on the stage

    static readonly StageGoToOptions Instant = StageTests.Instant;

    /// <summary>Each step runs once against the capability and once against the facade; reads return text, moves return "".</summary>
    static readonly Dictionary<string, (Func<StageCapability, PdfViewer, DocumentInfo, Task<string>> Capability, Func<PageNavigator, PdfViewer, DocumentInfo, Task<string>> Facade)> NavigatorSteps = new()
    {
        ["GetPageCountAsync"] = (async (s, _, _) => $"{await s.GetPageCountAsync()}", async (f, _, _) => $"{await f.GetPageCountAsync()}"),
        ["GetCurrentPageIndexAsync"] = (async (s, _, _) => $"{await s.GetCurrentPageIndexAsync()}", async (f, _, _) => $"{await f.GetCurrentPageIndexAsync()}"),
        ["GetCurrentPageAsync"] = (async (s, _, _) => $"{await s.GetCurrentPageAsync()}", async (f, _, _) => $"{await f.GetCurrentPageAsync()}"),
        ["CanGoNextAsync"] = (async (s, _, _) => $"{await s.CanGoNextAsync()}", async (f, _, _) => $"{await f.CanGoNextAsync()}"),
        ["CanGoPreviousAsync"] = (async (s, _, _) => $"{await s.CanGoPreviousAsync()}", async (f, _, _) => $"{await f.CanGoPreviousAsync()}"),
        ["GoToPageIndexAsync"] = (async (s, _, _) => { await s.GoToPageIndexAsync(5, Instant); return ""; }, async (f, _, _) => { await f.GoToPageIndexAsync(5, Instant); return ""; }),
        ["GoToPageAsync"] = (async (s, v, d) => { await s.GoToPageAsync((await v.GetPagesAsync(d))[6].Page, Instant); return ""; },
            async (f, v, d) => { await f.GoToPageAsync((await v.GetPagesAsync(d))[6].Page, Instant); return ""; }),
        ["GoToFirstPageAsync"] = (async (s, _, _) => { await s.GoToFirstPageAsync(Instant); return ""; }, async (f, _, _) => { await f.GoToFirstPageAsync(Instant); return ""; }),
        ["GoToLastPageAsync"] = (async (s, _, _) => { await s.GoToLastPageAsync(Instant); return ""; }, async (f, _, _) => { await f.GoToLastPageAsync(Instant); return ""; }),
        ["NextPageAsync"] = (async (s, _, _) => { await s.NextPageAsync(Instant); return ""; }, async (f, _, _) => { await f.NextPageAsync(Instant); return ""; }),
        ["PreviousPageAsync"] = (async (s, _, _) => { await s.PreviousPageAsync(Instant); return ""; }, async (f, _, _) => { await f.PreviousPageAsync(Instant); return ""; }),
    };

    static readonly Dictionary<string, (Func<StageCapability, Task<string>> Capability, Func<PageZoom, Task<string>> Facade)> ZoomSteps = new()
    {
        ["GetZoomLevelAsync"] = (async s => $"{await s.GetZoomLevelAsync()}", async f => $"{await f.GetZoomLevelAsync()}"),
        ["GetZoomModeAsync"] = (async s => $"{await s.GetZoomModeAsync()}", async f => $"{await f.GetZoomModeAsync()}"),
        ["CanZoomInAsync"] = (async s => $"{await s.CanZoomInAsync()}", async f => $"{await f.CanZoomInAsync()}"),
        ["CanZoomOutAsync"] = (async s => $"{await s.CanZoomOutAsync()}", async f => $"{await f.CanZoomOutAsync()}"),
        ["ZoomInAsync"] = (async s => { await s.ZoomInAsync(); return ""; }, async f => { await f.ZoomInAsync(); return ""; }),
        ["ZoomOutAsync"] = (async s => { await s.ZoomOutAsync(); return ""; }, async f => { await f.ZoomOutAsync(); return ""; }),
        ["ZoomToAsync"] = (async s => { await s.ZoomToAsync(new StageZoomTarget.Level(2), new PdfPoint(100, 200)); return ""; },
            async f => { await f.ZoomToAsync(new StageZoomTarget.Level(2), new PdfPoint(100, 200)); return ""; }),
        ["FitWidthAsync"] = (async s => { await s.FitWidthAsync(); return ""; }, async f => { await f.FitWidthAsync(); return ""; }),
        ["FitPageAsync"] = (async s => { await s.FitPageAsync(); return ""; }, async f => { await f.FitPageAsync(); return ""; }),
    };

    static readonly Dictionary<string, (Func<InteractionCapability, Task<string>> Capability, Func<ToolSwitcher, Task<string>> Facade)> ToolSteps = new()
    {
        ["GetToolsAsync"] = (async i => string.Join(",", (await i.GetToolsAsync()).Select(t => t.Id)), async f => string.Join(",", (await f.GetToolsAsync()).Select(t => t.Id))),
        ["GetActiveToolIdAsync"] = (async i => $"{await i.GetActiveToolIdAsync()}", async f => $"{await f.GetActiveToolIdAsync()}"),
        ["GetDefaultToolIdAsync"] = (async i => $"{await i.GetDefaultToolIdAsync()}", async f => $"{await f.GetDefaultToolIdAsync()}"),
        ["HasToolAsync"] = (async i => $"{await i.HasToolAsync(ToolId.Zoom)}", async f => $"{await f.HasToolAsync(ToolId.Zoom)}"),
        ["ActivateToolAsync"] = (async i => { await i.ActivateToolAsync(ToolId.Zoom); return ""; }, async f => { await f.ActivateToolAsync(ToolId.Zoom); return ""; }),
        ["ActivateDefaultToolAsync"] = (async i => { await i.ActivateDefaultToolAsync(); return ""; }, async f => { await f.ActivateDefaultToolAsync(); return ""; }),
        ["PushToolAsync"] = (async i => { await i.PushToolAsync(ToolId.Zoom); return ""; }, async f => { await f.PushToolAsync(ToolId.Zoom); return ""; }),
        ["PopToolAsync"] = (async i => { await i.PopToolAsync(); return ""; }, async f => { await f.PopToolAsync(); return ""; }),
    };

    public static TheoryData<string> NavigatorStepNames => [.. NavigatorSteps.Keys];
    public static TheoryData<string> ZoomStepNames => [.. ZoomSteps.Keys];
    public static TheoryData<string> ToolStepNames => [.. ToolSteps.Keys];

    /// <summary>Sized, and on page 3 so every navigation step has somewhere to go.</summary>
    static async Task<(PdfViewer Viewer, DocumentInfo Document, StageCapability Stage)> OpenedStage()
    {
        var (viewer, document, stage) = await StageTests.Opened();
        await stage.GoToPageIndexAsync(3, Instant);
        return (viewer, document, stage);
    }

    /// <summary>What the engine reports once any tween has finished.</summary>
    static async Task<string> Observe(StageCapability stage)
    {
        for (var polls = 0; await stage.IsMovingAsync() && polls < 500; polls++) await Task.Delay(10);
        Assert.False(await stage.IsMovingAsync());
        return $"{await stage.GetCurrentPageIndexAsync()} {await stage.GetCurrentPageAsync()} {await stage.GetZoomLevelAsync()} {await stage.GetZoomModeAsync()} {await stage.GetCameraAsync()}";
    }

    static async Task CompareOnTheStage<TFacade>(
        Func<StageCapability, PdfViewer, DocumentInfo, Task<string>> viaCapability, Func<PdfViewer, DocumentInfo, Task<TFacade>> facade,
        Func<TFacade, PdfViewer, DocumentInfo, Task<string>> viaFacade)
    {
        var (viewer, document, stage) = await OpenedStage();
        await using var _v = viewer;
        var before = await Observe(stage);
        var capabilityRead = await viaCapability(stage, viewer, document);
        var capabilityState = await Observe(stage);

        var (facadeViewer, facadeDocument, facadeStage) = await OpenedStage();
        await using var _f = facadeViewer;
        var target = await facade(facadeViewer, facadeDocument);
        var facadeRead = await viaFacade(target, facadeViewer, facadeDocument);
        var facadeState = await Observe(facadeStage);

        Assert.Equal(capabilityRead, facadeRead);
        Assert.Equal(capabilityState, facadeState);
        if (capabilityRead.Length == 0) Assert.NotEqual(before, capabilityState);
    }

    [Theory]
    [MemberData(nameof(NavigatorStepNames))]
    public async Task PageNavigatorMemberMatchesTheStage(string name)
    {
        var (capability, facade) = NavigatorSteps[name];
        await CompareOnTheStage(capability, (v, d) => v.GetPageNavigatorAsync(d), facade);
    }

    [Theory]
    [MemberData(nameof(ZoomStepNames))]
    public async Task PageZoomMemberMatchesTheStage(string name)
    {
        var (capability, facade) = ZoomSteps[name];
        await CompareOnTheStage((st, _, _) => capability(st), (v, d) => v.GetPageZoomAsync(d), (f, _, _) => facade(f));
    }

    /// <summary>Starts on pan pushed over the default tool, so ActivateDefaultToolAsync and PopToolAsync both have something to change.</summary>
    static async Task<(PdfViewer Viewer, DocumentInfo Document, InteractionCapability Interaction)> OpenedInteraction()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var interaction = await viewer.GetInteractionAsync(document);
        await interaction.PushToolAsync(ToolId.Pan);
        return (viewer, document, interaction);
    }

    static async Task<string> Observe(InteractionCapability interaction) => $"{await interaction.GetActiveToolIdAsync()} {await interaction.GetCursorAsync()}";

    [Theory]
    [MemberData(nameof(ToolStepNames))]
    public async Task ToolSwitcherMemberMatchesTheHub(string name)
    {
        var (viaCapability, viaFacade) = ToolSteps[name];
        var (viewer, _, interaction) = await OpenedInteraction();
        await using var _v = viewer;
        var before = await Observe(interaction);
        var capabilityRead = await viaCapability(interaction);
        var capabilityState = await Observe(interaction);

        var (facadeViewer, facadeDocument, facadeInteraction) = await OpenedInteraction();
        await using var _f = facadeViewer;
        using var switcher = await facadeViewer.GetToolSwitcherAsync(facadeDocument);
        var facadeRead = await viaFacade(switcher);
        var facadeState = await Observe(facadeInteraction);

        Assert.Equal(capabilityRead, facadeRead);
        Assert.Equal(capabilityState, facadeState);
        if (capabilityRead.Length == 0) Assert.NotEqual(before, capabilityState);
    }

    [Fact]
    public async Task TheFacadesThrowWhatTheCapabilitiesThrow()
    {
        var (viewer, document, stage) = await OpenedStage();
        await using var _v = viewer;
        var navigator = await viewer.GetPageNavigatorAsync(document);
        var zoom = await viewer.GetPageZoomAsync(document);
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => stage.GoToPageIndexAsync(-1))).Code);
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => navigator.GoToPageIndexAsync(-1))).Code);

        var (toolViewer, toolDocument, interaction) = await OpenedInteraction();
        await using var _t = toolViewer;
        var switcher = await toolViewer.GetToolSwitcherAsync(toolDocument);
        Assert.Equal(PdfErrorCode.NotFound, (await Assert.ThrowsAsync<PdfException>(() => interaction.ActivateToolAsync("nobody-registered-this"))).Code);
        Assert.Equal(PdfErrorCode.NotFound, (await Assert.ThrowsAsync<PdfException>(() => switcher.ActivateToolAsync("nobody-registered-this"))).Code);

        await viewer.CloseAsync(document);
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => stage.GetZoomLevelAsync())).Code);
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => zoom.GetZoomLevelAsync())).Code);
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => navigator.GetPageCountAsync())).Code);
    }

    [Fact]
    public async Task TheAccessorsRefuseALeftOutPluginAsTheCapabilityAccessorsDo()
    {
        await using var viewer = await PdfViewer.CreateAsync();
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        Func<Task>[] calls =
        [
            () => viewer.GetStageAsync(document), () => viewer.GetPageNavigatorAsync(document), () => viewer.GetPageZoomAsync(document),
            () => viewer.GetInteractionAsync(document), () => viewer.GetToolSwitcherAsync(document),
        ];
        foreach (var call in calls) Assert.Equal(PdfErrorCode.Unsupported, (await Assert.ThrowsAsync<PdfException>(call)).Code);
    }
}
