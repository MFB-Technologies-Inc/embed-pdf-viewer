using GF = uniffi.epdf_ffi;
using GO = uniffi.epdf_plugin_form;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static FormFieldRef ToPublic(GF.FormFieldRef r) => r switch
    {
        GF.FormFieldRef.ObjectNumber n => new FormFieldRef.ObjectNumber(WireInteger.ToInt(n.FieldObjectNumber, "field object number")),
        GF.FormFieldRef.Fqn f => new FormFieldRef.Fqn(f.Name),
        _ => throw Unknown(r),
    };

    internal static GF.FormFieldRef ToFfi(FormFieldRef r) => r switch
    {
        FormFieldRef.ObjectNumber n => new GF.FormFieldRef.ObjectNumber(WireInteger.ToUInt32(n.FieldObjectNumber, "field object number")),
        FormFieldRef.Fqn f => new GF.FormFieldRef.Fqn(f.Name ?? throw new ArgumentException("A field name is required.")),
        null => throw new ArgumentException("A field ref is required."),
        _ => throw Unknown(r),
    };

    internal static GF.FormFieldFamily ToFfi(FormFieldFamily f) => (GF.FormFieldFamily)(int)Defined(f, "field family");

    internal static GO.FormValidation ToFfi(FormValidation v) => (GO.FormValidation)(int)Defined(v, "form validation");

    internal static GO.FormWidgetEvent ToFfi(FormWidgetEvent e) => (GO.FormWidgetEvent)(int)Defined(e, "widget event");

    internal static GO.FormFillable ToFfi(FormFillable f) => f switch
    {
        FormFillable.All => new GO.FormFillable.All(),
        FormFillable.None => new GO.FormFillable.None(),
        FormFillable.Kinds k => new GO.FormFillable.Kinds([.. (k.Families ?? throw new ArgumentException("Kinds needs its families.")).Select(ToFfi)]),
        _ => throw Unknown(f),
    };

    internal static FormFillable ToPublic(GO.FormFillable f) => f switch
    {
        GO.FormFillable.All => new FormFillable.All(),
        GO.FormFillable.None => new FormFillable.None(),
        GO.FormFillable.Kinds k => new FormFillable.Kinds([.. k.Families.Select(x => (FormFieldFamily)(int)x)]),
        _ => throw Unknown(f),
    };

    internal static FormSettings ToPublic(GO.FormSettings s) => new(
        (FormValidation)(int)s.Validation, new FormFocusSettings(s.Focus.Color), new FormFieldSettings(s.Fields.Border, s.Fields.Background, s.Fields.Color),
        ToPublic(s.Fillable));

    internal static GO.FormSettingsPatch ToFfi(FormSettingsPatch p) => new(
        p.Validation is { } v ? ToFfi(v) : null,
        p.Focus is { } f ? new GO.FormFocusSettingsPatch(f.Color, f.ClearColor) : null,
        p.Fields is { } x ? new GO.FormFieldSettingsPatch(x.Border, x.ClearBorder, x.Background, x.Color) : null,
        p.Fillable is { } fill ? ToFfi(fill) : null);

    internal static FormFieldPanelSettings ToPublic(GO.FormFieldPanelSettings s) => new(
        [.. s.Properties.Select(p => new FormSettingProperty(p.Key, (FormSettingControl)(int)p.Control, p.Label, p.LabelKey, p.Min, p.Free, p.Options))], s.ValuesJson);

    static IReadOnlyList<FormChoiceOption> Options(GO.FormChoiceOption[] options) => [.. options.Select(o => new FormChoiceOption(o.Label, o.Value, o.Selected))];

    internal static FormFieldDetail ToPublic(GO.FormFieldDetail d) => d switch
    {
        GO.FormFieldDetail.Text t => new FormFieldDetail.Text(t.Value, t.DefaultValue, t.MaxLength is { } m ? WireInteger.ToInt(m, "max length") : null, t.Multiline, t.Password, t.Comb),
        GO.FormFieldDetail.Checkbox c => new FormFieldDetail.Checkbox(c.Checked, c.ExportValue),
        GO.FormFieldDetail.Radio r => new FormFieldDetail.Radio(r.Value, r.RadiosInUnison, r.NoToggleToOff),
        GO.FormFieldDetail.Combobox c => new FormFieldDetail.Combobox(c.Value, c.DefaultValue, c.Edit, Options(c.Options)),
        GO.FormFieldDetail.Listbox l => new FormFieldDetail.Listbox(l.SelectedValues, l.MultiSelect, Options(l.Options)),
        GO.FormFieldDetail.Pushbutton => new FormFieldDetail.Pushbutton(),
        GO.FormFieldDetail.Signature s => new FormFieldDetail.Signature(s.Signed),
        GO.FormFieldDetail.Unknown u => new FormFieldDetail.Unknown(u.RawValue),
        _ => throw Unknown(d),
    };

    internal static FormField ToPublic(GO.FormField f) => new(
        ToPublic(f.FieldRef), f.Name, (FormFieldFamily)(int)f.Family, f.Recovered, f.ReadOnly, f.Required, f.NoExport, f.AlternateName, f.MappingName,
        f.GroupId, ToPublic(f.Detail),
        [.. f.Widgets.Select(w => new FormWidget(
            w.AnnotationRef is { } a ? ToPublic(a) : null, WireInteger.ToInt(w.ObjectNumber, "widget object number"), w.Page is { } p ? ToPublic(p) : null,
            w.Toggle is { } t ? new FormToggleWidget(t.OnState, t.ExportValue, t.Checked) : null))],
        f.FieldsJson);

    internal static FormField? ToPublicOrNull(GO.FormField? f) => f is null ? null : ToPublic(f);

    internal static IReadOnlyList<FormField> Fields(GO.FormField[] fields) => [.. fields.Select(ToPublic)];

    internal static FormSnapshot ToPublic(GO.FormSnapshot s) => new(
        (FormKind)(int)s.FormKind, s.NeedsAppearances, Fields(s.Fields), [.. s.CalculationOrder.Select(r => r is null ? null : ToPublic(r))], s.WidgetsJson);

    internal static FormWidgetRecord ToPublic(GO.FormWidgetRecord w) => new(ToPublic(w.AnnotationRef), ToPublic(w.Page), ToPublic(w.Bounds), w.FieldsJson);

    internal static GO.FormCalculationPosition ToFfi(FormCalculationPosition p) => p switch
    {
        FormCalculationPosition.Start => new GO.FormCalculationPosition.Start(),
        FormCalculationPosition.End => new GO.FormCalculationPosition.End(),
        FormCalculationPosition.Before b => new GO.FormCalculationPosition.Before(ToFfi(b.Field)),
        FormCalculationPosition.After a => new GO.FormCalculationPosition.After(ToFfi(a.Field)),
        _ => throw Unknown(p),
    };

    internal static GO.FormValueInput ToFfi(FormValueInput v) => v switch
    {
        FormValueInput.Value x => new GO.FormValueInput.Value(x.Text),
        FormValueInput.Checked c => new GO.FormValueInput.Checked(c.IsChecked),
        FormValueInput.SelectedValues s => new GO.FormValueInput.SelectedValues([.. Arguments.NoNullElements(s.Values ?? throw new ArgumentException("Selected values are required."), "values")]),
        null => throw new ArgumentException("A value is required."),
        _ => throw Unknown(v),
    };

    internal static FormValueInput ToPublic(GO.FormValueInput v) => v switch
    {
        GO.FormValueInput.Value x => new FormValueInput.Value(x.ValueValue),
        GO.FormValueInput.Checked c => new FormValueInput.Checked(c.CheckedValue),
        GO.FormValueInput.SelectedValues s => new FormValueInput.SelectedValues(s.Values),
        _ => throw Unknown(v),
    };

    internal static FormFillItem ToPublic(GO.FormFillItem i) => new(
        ToPublic(i.FieldRef), WireInteger.ToInt(i.AnnotObjectNumber, "annotation object number"), i.AnnotationRef is { } a ? ToPublic(a) : null,
        ToPublic(i.Bounds), i.LookJson, i.Disabled, i.Label, i.Control switch
        {
            GO.FormFillControl.Text t => new FormFillControl.Text(t.Value, t.Multiline, t.Password, t.MaxLength is { } m ? WireInteger.ToInt(m, "max length") : null, t.Comb),
            GO.FormFillControl.Toggle t => new FormFillControl.Toggle(t.Radio, t.Checked, t.ExportValue),
            GO.FormFillControl.Choice c => new FormFillControl.Choice(c.List, c.Edit, c.Multi, Options(c.Options), c.Selected),
            GO.FormFillControl.Button => new FormFillControl.Button(),
            GO.FormFillControl.Signature s => new FormFillControl.Signature(s.Signed),
            _ => throw Unknown(i.Control),
        });

    internal static FormWidgetHit ToPublic(GO.FormWidgetHit h) => new(WireInteger.ToInt(h.AnnotObjectNumber, "annotation object number"), ToPublic(h.Field), ToPublic(h.Bounds));

    internal static GO.FormWidgetPlacement ToFfi(FormWidgetPlacement p) => new(p.Page is { } page ? ToFfi(page) : null, p.Bounds is { } b ? ToFfi(b) : null, p.ExportValue);

    internal static FormCommitResult ToPublic(GO.FormCommitResult r) => new((FormCommitStatus)(int)r.Status, r.Scripted, r.Diagnostics, r.Error);

    internal static FormBatchResult ToPublic(GO.FormBatchResult r) => new(
        [.. r.Applied.Select(ToPublic)], [.. r.Skipped.Select(s => new FormBatchSkip(s.Name, s.Reason))],
        [.. r.Failed.Select(f => new FormBatchFailure(f.FieldRef is { } x ? ToPublic(x) : null, f.Name, ToPublic(f.Error)))]);

    internal static GO.FormFilter? ToFfi(FormFilter? f) => f is null ? null : new(f.Family is { } family ? ToFfi(family) : null, f.Page is { } p ? ToFfi(p) : null, f.Name);

    static GO.FormChoiceOptionInput[]? ToFfi(IReadOnlyList<FormChoiceOptionInput>? options) => options is null ? null :
        [.. Arguments.NoNullElements(options, "options").Select(o => new GO.FormChoiceOptionInput(o.Label ?? throw new ArgumentException("An option needs its label."), o.Value ?? throw new ArgumentException("An option needs its value.")))];

    internal static GO.FormCreateFieldInput ToFfi(FormCreateFieldInput i) => new(
        ToFfi(i.Family), i.Name ?? throw new ArgumentException("A field needs its name."), ToFfi(i.Page), ToFfi(i.Bounds), ToFfi(i.Options), i.ExportValue);

    internal static GO.FormFieldPatch ToFfi(FormFieldPatch p) => new(
        p.Name, p.ReadOnly, p.Required, p.NoExport, p.AlternateName, p.MappingName, p.DefaultValue, WireInteger.ToUInt32(p.MaxLength, "max length"),
        ToFfi(p.Options), [.. (p.Clear ?? []).Select(k => (GO.FormFieldPatchKey)(int)Defined(k, "patch key"))]);

    internal static GO.FormDuplicateOptions ToFfi(FormDuplicateOptions? o) =>
        new(o?.Page is { } page ? ToFfi(page) : null, o?.Offset is { } offset ? ToFfiOffset(offset) : null, o?.Values ?? false);

    internal static FormDuplicateResult ToPublic(GO.FormDuplicateResult r) =>
        new(Fields(r.Fields), [.. r.Widgets.Select(ToPublic)], r.WidgetsJson, r.DroppedJson);

    internal static FormImportResult ToPublic(GO.FormImportResult r) => new(Fields(r.Fields), r.RefMapJson, r.DroppedJson);

    internal static FormActivationResult ToPublic(GO.FormActivationResult r) => r switch
    {
        GO.FormActivationResult.Form f => new FormActivationResult.Committed(ToPublic(f.Result)),
        GO.FormActivationResult.Dispatched => new FormActivationResult.Dispatched(),
        _ => throw Unknown(r),
    };

    internal static GO.FormFocusOptions? ToFfi(FormFocusOptions? o) => o is null ? null : new(o.Select, o.Widget is { } w ? ToFfi(w) : null);

    internal static GO.FormFocusNextOptions? ToFfi(FormFocusNextOptions? o) => o is null ? null : new(o.Backward, o.Empty, o.Wrap, o.From is { } f ? ToFfi(f) : null);

    internal static FormEvent ToPublic(GO.FormEvent e) => e switch
    {
        GO.FormEvent.FocusChanged f => new FormEvent.FocusChanged(ToPublicOrNull(f.Field), f.Widget is { } w ? ToPublic(w) : null),
        GO.FormEvent.ValueChanged v => new FormEvent.ValueChanged(ToPublic(v.Field), v.OriginJson),
        GO.FormEvent.FieldCreated c => new FormEvent.FieldCreated(ToPublic(c.Field), c.OriginJson),
        GO.FormEvent.FieldUpdated u => new FormEvent.FieldUpdated(ToPublic(u.Field), u.OriginJson),
        GO.FormEvent.FieldDeleted d => new FormEvent.FieldDeleted(ToPublic(d.FieldRef), d.OriginJson),
        GO.FormEvent.ValidationRejected r => new FormEvent.ValidationRejected(ToPublic(r.Field), r.Messages),
        GO.FormEvent.Resynced => new FormEvent.Resynced(),
        _ => throw Unknown(e),
    };
}
