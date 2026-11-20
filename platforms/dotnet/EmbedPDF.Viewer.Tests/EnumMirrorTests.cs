using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// Public enums that convert to and from their generated twins by value list
/// the same cases in the same order.
/// </summary>
public sealed class EnumMirrorTests
{
    public static TheoryData<Type, Type> Pairs => new()
    {
        { typeof(SearchBlendMode), typeof(uniffi.epdf_plugin_search.SearchBlendMode) },
        { typeof(SearchScrollBehavior), typeof(uniffi.epdf_plugin_search.SearchScrollBehavior) },
        { typeof(SearchStatus), typeof(uniffi.epdf_plugin_search.SearchStatus) },
        { typeof(SearchResultStatus), typeof(uniffi.epdf_plugin_search.SearchResultStatus) },
        { typeof(SearchCancelReason), typeof(uniffi.epdf_plugin_search.SearchCancelReason) },
        { typeof(RenderRotation), typeof(uniffi.epdf_plugin_render.RenderRotation) },
        { typeof(RenderInvalidateScope), typeof(uniffi.epdf_plugin_render.RenderInvalidateScope) },
        { typeof(RenderEncodingFormat), typeof(uniffi.epdf_plugin_render.RenderEncodingFormat) },
        { typeof(PointerPhase), typeof(uniffi.epdf_ffi.PointerPhase) },
        { typeof(PointerKind), typeof(uniffi.epdf_ffi.PointerKind) },
        { typeof(PointerGesture), typeof(uniffi.epdf_ffi.PointerGesture) },
        { typeof(InteractionToolTouch), typeof(uniffi.epdf_plugin_interaction.InteractionToolTouch) },
        { typeof(FeedbackImpactWeight), typeof(uniffi.epdf_plugin_interaction.FeedbackImpactWeight) },
        { typeof(FeedbackNotifyKind), typeof(uniffi.epdf_plugin_interaction.FeedbackNotifyKind) },
        { typeof(TextDirection), typeof(uniffi.epdf_plugin_i18n.TextDirection) },
        { typeof(ActionOrigin), typeof(uniffi.epdf_plugin_actions.ActionOrigin) },
        { typeof(ActionPolicyDecision), typeof(uniffi.epdf_plugin_actions.ActionPolicyDecision) },
        { typeof(ActionResultStatus), typeof(uniffi.epdf_plugin_actions.ActionResultStatus) },
        { typeof(ActionNodeStatus), typeof(uniffi.epdf_plugin_actions.ActionNodeStatus) },
        { typeof(ActionDiagnosticCode), typeof(uniffi.epdf_plugin_actions.ActionDiagnosticCode) },
        { typeof(DocumentTriggerEvent), typeof(uniffi.epdf_plugin_actions.DocumentTriggerEvent) },
        { typeof(DocumentVerb), typeof(uniffi.epdf_plugin_actions.DocumentVerb) },
        { typeof(OpenSequence), typeof(uniffi.epdf_plugin_actions.OpenSequence) },
        { typeof(PageEventKind), typeof(uniffi.epdf_plugin_actions.PageEventKind) },
        { typeof(PdfAnnotationEventKind), typeof(uniffi.epdf_plugin_actions.PdfAnnotationEventKind) },
        { typeof(PdfFieldEventKind), typeof(uniffi.epdf_plugin_actions.PdfFieldEventKind) },
        { typeof(ScriptDiagnosticCode), typeof(uniffi.epdf_plugin_actions.ScriptDiagnosticCode) },
        { typeof(ScriptErrorKind), typeof(uniffi.epdf_plugin_actions.ScriptErrorKind) },
        { typeof(ScriptPhase), typeof(uniffi.epdf_plugin_actions.ScriptPhase) },
        { typeof(ZoomMode), typeof(uniffi.epdf_ffi.ZoomMode) },
        { typeof(StageAlign), typeof(uniffi.epdf_ffi.StageAlign) },
        { typeof(StageDirection), typeof(uniffi.epdf_ffi.StageDirection) },
        { typeof(StageSpreadMode), typeof(uniffi.epdf_ffi.StageSpreadMode) },
        { typeof(StageSizingMode), typeof(uniffi.epdf_ffi.StageSizingMode) },
        { typeof(StageFlow), typeof(uniffi.epdf_plugin_stage.StageFlow) },
        { typeof(StageLayout), typeof(uniffi.epdf_plugin_stage.StageLayout) },
        { typeof(StageOrientation), typeof(uniffi.epdf_plugin_stage.StageOrientation) },
        { typeof(StageRevealWord), typeof(uniffi.epdf_plugin_stage.StageRevealWord) },
        { typeof(StageScrollBehavior), typeof(uniffi.epdf_plugin_stage.StageScrollBehavior) },
        { typeof(StageSettingKey), typeof(uniffi.epdf_plugin_stage.StageSettingKey) },
        { typeof(SelectionDirection), typeof(uniffi.epdf_plugin_selection.SelectionDirection) },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void CasesMatchInOrder(Type mirror, Type generated)
    {
        Assert.Equal(Enum.GetNames(generated), Enum.GetNames(mirror));
        Assert.Equal(Enum.GetValues(generated).Cast<object>().Select(Convert.ToInt32), Enum.GetValues(mirror).Cast<object>().Select(Convert.ToInt32));
    }
}
