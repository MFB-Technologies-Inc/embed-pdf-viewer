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
        { typeof(NotAllowedMode), typeof(uniffi.epdf_plugin_commands.NotAllowedMode) },
        { typeof(CommandHook), typeof(uniffi.epdf_plugin_commands.CommandHook) },
        { typeof(CommandRejectReason), typeof(uniffi.epdf_plugin_commands.CommandRejectReason) },
        { typeof(CommandRefusalReason), typeof(uniffi.epdf_plugin_commands.CommandRefusalReason) },
        { typeof(AnnotationTextFormat), typeof(uniffi.epdf_plugin_annotation.AnnotationTextFormat) },
        { typeof(FormFieldFamily), typeof(uniffi.epdf_ffi.FormFieldFamily) },
        { typeof(FormValidation), typeof(uniffi.epdf_plugin_form.FormValidation) },
        { typeof(FormKind), typeof(uniffi.epdf_plugin_form.FormKind) },
        { typeof(FormCommitStatus), typeof(uniffi.epdf_plugin_form.FormCommitStatus) },
        { typeof(FormSetValueStatus), typeof(uniffi.epdf_plugin_form.FormSetValueStatus) },
        { typeof(FormSettingControl), typeof(uniffi.epdf_plugin_form.FormSettingControl) },
        { typeof(FormWidgetEvent), typeof(uniffi.epdf_plugin_form.FormWidgetEvent) },
        { typeof(FormFieldPatchKey), typeof(uniffi.epdf_plugin_form.FormFieldPatchKey) },
        { typeof(FormWidgetLayout), typeof(uniffi.epdf_plugin_form.FormWidgetLayout) },
        { typeof(AnnotationThreadChange), typeof(uniffi.epdf_plugin_annotation.AnnotationThreadChange) },
        { typeof(AnnotationRemeasureSkipReason), typeof(uniffi.epdf_plugin_annotation.AnnotationRemeasureSkipReason) },
        { typeof(AnnotationThreadStatus), typeof(uniffi.epdf_plugin_annotation.AnnotationThreadStatus) },
        { typeof(MeasureFraction), typeof(uniffi.epdf_plugin_measurement.MeasureFraction) },
        { typeof(MeasureLabelPosition), typeof(uniffi.epdf_plugin_measurement.MeasureLabelPosition) },
        { typeof(RedactionMarkKind), typeof(uniffi.epdf_plugin_redaction.RedactionMarkKind) },
        { typeof(RedactionPageStatus), typeof(uniffi.epdf_plugin_redaction.RedactionPageStatus) },
        { typeof(MeasurementKind), typeof(uniffi.epdf_plugin_measurement.MeasurementKind) },
        { typeof(MeasurementReadoutKind), typeof(uniffi.epdf_plugin_measurement.MeasurementReadoutKind) },
        { typeof(MeasurementScaleSource), typeof(uniffi.epdf_plugin_measurement.MeasurementScaleSource) },
        { typeof(MeasurementUnavailable), typeof(uniffi.epdf_plugin_measurement.MeasurementUnavailable) },
        { typeof(ScaleSkipReason), typeof(uniffi.epdf_plugin_measurement.ScaleSkipReason) },
        { typeof(StampAssetKind), typeof(uniffi.epdf_plugin_stamp.StampAssetKind) },
        { typeof(StampLibraryChangeReason), typeof(uniffi.epdf_plugin_stamp.StampLibraryChangeReason) },
        { typeof(DocumentFieldLockSource), typeof(uniffi.epdf_plugin_signature.DocumentFieldLockSource) },
        { typeof(SignatureCompletionStatus), typeof(uniffi.epdf_plugin_signature.SignatureCompletionStatus) },
        { typeof(SignatureCoverage), typeof(uniffi.epdf_plugin_signature.SignatureCoverage) },
        { typeof(SignatureCryptography), typeof(uniffi.epdf_plugin_signature.SignatureCryptography) },
        { typeof(SignatureDigest), typeof(uniffi.epdf_plugin_signature.SignatureDigest) },
        { typeof(SignatureFieldLockAction), typeof(uniffi.epdf_plugin_signature.SignatureFieldLockAction) },
        { typeof(SignatureFindingVerdict), typeof(uniffi.epdf_plugin_signature.SignatureFindingVerdict) },
        { typeof(SignatureIntegrity), typeof(uniffi.epdf_plugin_signature.SignatureIntegrity) },
        { typeof(SignatureKeyAlgorithm), typeof(uniffi.epdf_plugin_signature.SignatureKeyAlgorithm) },
        { typeof(SignatureKind), typeof(uniffi.epdf_plugin_signature.SignatureKind) },
        { typeof(SignatureLongTermGapReason), typeof(uniffi.epdf_plugin_signature.SignatureLongTermGapReason) },
        { typeof(SignatureMode), typeof(uniffi.epdf_plugin_signature.SignatureMode) },
        { typeof(SignatureModificationLevel), typeof(uniffi.epdf_plugin_signature.SignatureModificationLevel) },
        { typeof(SignatureModificationsBasis), typeof(uniffi.epdf_plugin_signature.SignatureModificationsBasis) },
        { typeof(SignatureModificationsVerdict), typeof(uniffi.epdf_plugin_signature.SignatureModificationsVerdict) },
        { typeof(SignatureRevocationStatus), typeof(uniffi.epdf_plugin_signature.SignatureRevocationStatus) },
        { typeof(SignatureSummary), typeof(uniffi.epdf_plugin_signature.SignatureSummary) },
        { typeof(SignatureTrustStatus), typeof(uniffi.epdf_plugin_signature.SignatureTrustStatus) },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void CasesMatchInOrder(Type mirror, Type generated)
    {
        Assert.Equal(Enum.GetNames(generated), Enum.GetNames(mirror));
        Assert.Equal(Enum.GetValues(generated).Cast<object>().Select(Convert.ToInt32), Enum.GetValues(mirror).Cast<object>().Select(Convert.ToInt32));
    }
}
