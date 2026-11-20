using GF = uniffi.epdf_ffi;
using GI = uniffi.epdf_plugin_interaction;

namespace EmbedPDF;

internal static partial class Ffi
{
    /// <summary>A tool id's wire string; a default <see cref="ToolId"/> is <c>""</c>.</summary>
    internal static string Wire(ToolId id) => id.Value ?? "";

    internal static InteractionTool ToPublic(GI.InteractionTool tool) =>
        new(tool.Id, tool.Cursor, tool.Enables)
        {
            GapCursor = tool.GapCursor,
            Touch = tool.Touch is { } touch ? (InteractionToolTouch)(int)touch : null,
        };

    internal static GI.InteractionTool ToFfi(InteractionTool tool) => new(
        Id: Wire(tool.Id),
        Cursor: tool.Cursor ?? throw new ArgumentException("The tool has no cursor.", nameof(tool)),
        Enables: [.. tool.Enables ?? []],
        GapCursor: tool.GapCursor,
        Touch: tool.Touch is { } touch ? (GI.InteractionToolTouch)(int)touch : null);

    internal static PointerSample ToPublic(GF.PointerSample sample) =>
        new((PointerPhase)(int)sample.Phase, ToPublic(sample.Viewport),
            new PointerModifiers(sample.Modifiers.Shift, sample.Modifiers.Alt, sample.Modifiers.Ctrl, sample.Modifiers.Meta))
        {
            Page = sample.Page is { } hit
                ? new PointerPageHit(ToPublic(hit.Page), ToPublic(hit.Point)) { Scale = hit.Scale, Rotation = hit.Rotation, Zoom = hit.Zoom }
                : null,
            ClickCount = sample.ClickCount is { } clicks ? WireInteger.ToInt(clicks, "click count") : null,
            PointerType = sample.PointerType is { } kind ? (PointerKind)(int)kind : null,
            Gesture = sample.Gesture is { } gesture ? (PointerGesture)(int)gesture : null,
            Source = sample.Source,
        };

    internal static GF.PointerSample ToFfi(PointerSample sample) => new(
        Phase: (GF.PointerPhase)(int)sample.Phase,
        Viewport: ToFfi(sample.Viewport),
        Modifiers: new GF.Modifiers(sample.Modifiers.Shift, sample.Modifiers.Alt, sample.Modifiers.Ctrl, sample.Modifiers.Meta),
        Page: sample.Page is { } hit
            ? new GF.PointerPageHit(ToFfi(hit.Page), ToFfi(hit.Point), hit.Scale, hit.Rotation, hit.Zoom)
            : null,
        ClickCount: WireInteger.ToUInt32(sample.ClickCount, "clickCount"),
        PointerType: sample.PointerType is { } kind ? (GF.PointerKind)(int)kind : null,
        Gesture: sample.Gesture is { } gesture ? (GF.PointerGesture)(int)gesture : null,
        Source: sample.Source);

    internal static InteractionEvent ToPublic(GI.InteractionEvent e) => e switch
    {
        GI.InteractionEvent.ToolChanged c => new InteractionEvent.ToolChanged(c.Event.ToolId, c.Event.PreviousToolId),
        GI.InteractionEvent.GestureStarted g => new InteractionEvent.GestureStarted(g.Event.ToolId, ToPublicOrNull(g.Event.Page), (PointerKind)(int)g.Event.PointerType),
        GI.InteractionEvent.GestureEnded g => new InteractionEvent.GestureEnded(g.Event.ToolId, ToPublicOrNull(g.Event.Page), (PointerKind)(int)g.Event.PointerType),
        GI.InteractionEvent.GestureCancelled g => new InteractionEvent.GestureCancelled(g.Event.ToolId, ToPublicOrNull(g.Event.Page), (PointerKind)(int)g.Event.PointerType),
        GI.InteractionEvent.CursorChanged c => new InteractionEvent.CursorChanged(c.Event.Cursor),
        _ => throw Unknown(e),
    };

}
