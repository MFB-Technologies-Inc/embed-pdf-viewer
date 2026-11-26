namespace EmbedPDF;

/// <summary>A form field, by its object number or its fully qualified name.</summary>
public abstract record FormFieldRef
{
    private protected FormFieldRef() { }

    /// <summary>The field's PDF object number.</summary>
    /// <param name="FieldObjectNumber">The object number.</param>
    public sealed record ObjectNumber(int FieldObjectNumber) : FormFieldRef;

    /// <summary>The field's fully qualified name, such as <c>address.city</c>.</summary>
    /// <param name="Name">The name.</param>
    public sealed record Fqn(string Name) : FormFieldRef;
}

/// <summary>A field's kind.</summary>
public enum FormFieldFamily
{
    /// <summary>A text field.</summary>
    Text,
    /// <summary>A checkbox.</summary>
    Checkbox,
    /// <summary>A radio group.</summary>
    Radio,
    /// <summary>A combo box.</summary>
    Combobox,
    /// <summary>A list box.</summary>
    Listbox,
    /// <summary>A push button.</summary>
    Pushbutton,
    /// <summary>A signature field.</summary>
    Signature,
    /// <summary>A field type this build does not model.</summary>
    Unknown,
}

/// <summary>Whether the document's form scripts run.</summary>
public enum FormValidation
{
    /// <summary>Run keystroke, validate, calculate and format scripts (needs the actions plugin with scripting on).</summary>
    Scripts,
    /// <summary>Run none.</summary>
    None,
}

/// <summary>What kind of form a document has.</summary>
public enum FormKind
{
    /// <summary>No form.</summary>
    None,
    /// <summary>An AcroForm.</summary>
    Acroform,
    /// <summary>An XFA form.</summary>
    Xfa,
}

/// <summary>What a widget activation's commit did.</summary>
public enum FormCommitStatus
{
    /// <summary>The value changed.</summary>
    Applied,
    /// <summary>It was already that value.</summary>
    Unchanged,
    /// <summary>A script or rule refused it.</summary>
    Rejected,
    /// <summary>It failed.</summary>
    Failed,
}

/// <summary>What setting a value did.</summary>
public enum FormSetValueStatus
{
    /// <summary>The value changed.</summary>
    Applied,
    /// <summary>It was already that value.</summary>
    Unchanged,
    /// <summary>A script or rule refused it.</summary>
    Rejected,
}

/// <summary>The control a field-settings property shows.</summary>
public enum FormSettingControl
{
    /// <summary>A text box.</summary>
    Text,
    /// <summary>A checkbox.</summary>
    Flag,
    /// <summary>A number.</summary>
    Number,
    /// <summary>An option list editor.</summary>
    Options,
    /// <summary>One choice of several.</summary>
    Choice,
    /// <summary>Several choices.</summary>
    Choices,
}

/// <summary>A pointer or focus event on a widget, for its scripts.</summary>
public enum FormWidgetEvent
{
    /// <summary>The pointer entered.</summary>
    CursorEnter,
    /// <summary>The pointer left.</summary>
    CursorExit,
    /// <summary>A press.</summary>
    MouseDown,
    /// <summary>A release.</summary>
    MouseUp,
    /// <summary>The widget gained focus.</summary>
    Focus,
    /// <summary>The widget lost focus.</summary>
    Blur,
}

/// <summary>An optional field member a <see cref="FormFieldPatch"/> clears.</summary>
public enum FormFieldPatchKey
{
    /// <summary>The alternate name (tooltip).</summary>
    AlternateName,
    /// <summary>The export mapping name.</summary>
    MappingName,
    /// <summary>The default value.</summary>
    DefaultValue,
    /// <summary>The maximum length.</summary>
    MaxLength,
}

/// <summary>Which fields a user may fill.</summary>
public abstract record FormFillable
{
    private protected FormFillable() { }

    /// <summary>Every field.</summary>
    public sealed record All : FormFillable;

    /// <summary>No field.</summary>
    public sealed record None : FormFillable;

    /// <summary>Fields of these kinds.</summary>
    /// <param name="Families">The kinds.</param>
    public sealed record Kinds(IReadOnlyList<FormFieldFamily> Families) : FormFillable;
}

/// <summary>The focus ring.</summary>
/// <param name="Color">Its colour, or <see langword="null"/> for none.</param>
public sealed record FormFocusSettings(string? Color);

/// <summary>How fields are drawn while filling.</summary>
/// <param name="Border">The border colour, or <see langword="null"/> for none.</param>
/// <param name="Background">The background colour.</param>
/// <param name="Color">The highlight colour.</param>
public sealed record FormFieldSettings(string? Border, string Background, string Color);

/// <summary>The form plugin's settings.</summary>
/// <param name="Validation">Whether scripts run.</param>
/// <param name="Focus">The focus ring.</param>
/// <param name="Fields">How fields are drawn.</param>
/// <param name="Fillable">Which fields may be filled.</param>
public sealed record FormSettings(FormValidation Validation, FormFocusSettings Focus, FormFieldSettings Fields, FormFillable Fillable);

/// <summary>A change to the focus ring. Null members keep their value.</summary>
public sealed record FormFocusSettingsPatch
{
    /// <summary>The colour.</summary>
    public string? Color { get; init; }

    /// <summary>Removes the colour.</summary>
    public bool ClearColor { get; init; }
}

/// <summary>A change to how fields are drawn. Null members keep their value.</summary>
public sealed record FormFieldSettingsPatch
{
    /// <summary>The border colour.</summary>
    public string? Border { get; init; }

    /// <summary>Removes the border.</summary>
    public bool ClearBorder { get; init; }

    /// <summary>The background colour.</summary>
    public string? Background { get; init; }

    /// <summary>The highlight colour.</summary>
    public string? Color { get; init; }
}

/// <summary>A change to the form settings. Null members keep their value.</summary>
public sealed record FormSettingsPatch
{
    /// <summary>Whether scripts run.</summary>
    public FormValidation? Validation { get; init; }

    /// <summary>The focus ring.</summary>
    public FormFocusSettingsPatch? Focus { get; init; }

    /// <summary>How fields are drawn.</summary>
    public FormFieldSettingsPatch? Fields { get; init; }

    /// <summary>Which fields may be filled.</summary>
    public FormFillable? Fillable { get; init; }
}

/// <summary>One property a field's settings panel shows.</summary>
/// <param name="Key">The member it edits.</param>
/// <param name="Control">Its control.</param>
/// <param name="Label">Its label.</param>
/// <param name="LabelKey">Its label's translation key.</param>
/// <param name="Min">A number's minimum.</param>
/// <param name="Free">Whether a choice takes values outside its options.</param>
/// <param name="Options">A choice's options.</param>
public sealed record FormSettingProperty(string Key, FormSettingControl Control, string Label, string LabelKey, double? Min, bool? Free, IReadOnlyList<string>? Options);

/// <summary>A field's settings panel.</summary>
/// <param name="Properties">Its properties.</param>
/// <param name="ValuesJson">Their values, as JSON.</param>
public sealed record FormFieldPanelSettings(IReadOnlyList<FormSettingProperty> Properties, string ValuesJson);

/// <summary>An option of a choice field.</summary>
/// <param name="Label">What it shows.</param>
/// <param name="Value">Its export value.</param>
/// <param name="Selected">Whether it is selected.</param>
public sealed record FormChoiceOption(string Label, string Value, bool Selected);

/// <summary>An option to give a choice field.</summary>
/// <param name="Label">What it shows.</param>
/// <param name="Value">Its export value.</param>
public sealed record FormChoiceOptionInput(string Label, string Value);

/// <summary>A field's kind-specific state.</summary>
public abstract record FormFieldDetail
{
    private protected FormFieldDetail() { }

    /// <summary>A text field.</summary>
    public sealed record Text(string Value, string DefaultValue, int? MaxLength, bool Multiline, bool Password, bool Comb) : FormFieldDetail;

    /// <summary>A checkbox.</summary>
    public sealed record Checkbox(bool Checked, string ExportValue) : FormFieldDetail;

    /// <summary>A radio group.</summary>
    public sealed record Radio(string Value, bool RadiosInUnison, bool NoToggleToOff) : FormFieldDetail;

    /// <summary>A combo box.</summary>
    public sealed record Combobox(string Value, string DefaultValue, bool Edit, IReadOnlyList<FormChoiceOption> Options) : FormFieldDetail;

    /// <summary>A list box.</summary>
    public sealed record Listbox(IReadOnlyList<string> SelectedValues, bool MultiSelect, IReadOnlyList<FormChoiceOption> Options) : FormFieldDetail;

    /// <summary>A push button.</summary>
    public sealed record Pushbutton : FormFieldDetail;

    /// <summary>A signature field.</summary>
    public sealed record Signature(bool Signed) : FormFieldDetail;

    /// <summary>A field type this build does not model.</summary>
    public sealed record Unknown(string RawValue) : FormFieldDetail;
}

/// <summary>A checkbox's or radio button's states.</summary>
/// <param name="OnState">Its on appearance state.</param>
/// <param name="ExportValue">Its export value.</param>
/// <param name="Checked">Whether it is on.</param>
public sealed record FormToggleWidget(string OnState, string ExportValue, bool Checked);

/// <summary>One widget of a field.</summary>
/// <param name="AnnotationRef">Its annotation.</param>
/// <param name="ObjectNumber">Its object number.</param>
/// <param name="Page">Its page.</param>
/// <param name="Toggle">Its toggle states, for a checkbox or radio button.</param>
public sealed record FormWidget(AnnotationRef? AnnotationRef, int ObjectNumber, PageRef? Page, FormToggleWidget? Toggle);

/// <summary>A form field.</summary>
public sealed record FormField(
    FormFieldRef FieldRef, string Name, FormFieldFamily Family, bool Recovered, bool ReadOnly, bool Required, bool NoExport,
    string? AlternateName, string? MappingName, string? GroupId, FormFieldDetail Detail, IReadOnlyList<FormWidget> Widgets, string FieldsJson);

/// <summary>A document's form.</summary>
/// <param name="FormKind">What kind it is.</param>
/// <param name="NeedsAppearances">Whether the file asks viewers to draw appearances.</param>
/// <param name="Fields">Its fields.</param>
/// <param name="CalculationOrder">The calculation order; a null entry is a field the engine could not resolve.</param>
/// <param name="WidgetsJson">Its widgets, as JSON.</param>
public sealed record FormSnapshot(FormKind FormKind, bool NeedsAppearances, IReadOnlyList<FormField> Fields, IReadOnlyList<FormFieldRef?> CalculationOrder, string WidgetsJson);

/// <summary>What a validation found.</summary>
/// <param name="Valid">Whether every required field has a value.</param>
/// <param name="Missing">The required fields without one.</param>
public sealed record FormValidationResult(bool Valid, IReadOnlyList<FormField> Missing);

/// <summary>A widget annotation as the form editor reads it.</summary>
/// <param name="AnnotationRef">Its annotation.</param>
/// <param name="Page">Its page.</param>
/// <param name="Bounds">Its box, page space.</param>
/// <param name="FieldsJson">Its members, as JSON.</param>
public sealed record FormWidgetRecord(AnnotationRef AnnotationRef, PageRef Page, PdfRect Bounds, string FieldsJson);

/// <summary>Where calculations move to in the calculation order.</summary>
public abstract record FormCalculationPosition
{
    private protected FormCalculationPosition() { }

    /// <summary>First.</summary>
    public sealed record Start : FormCalculationPosition;

    /// <summary>Last.</summary>
    public sealed record End : FormCalculationPosition;

    /// <summary>Just before a field.</summary>
    public sealed record Before(FormFieldRef Field) : FormCalculationPosition;

    /// <summary>Just after a field.</summary>
    public sealed record After(FormFieldRef Field) : FormCalculationPosition;
}

/// <summary>A value to give a field.</summary>
public abstract record FormValueInput
{
    private protected FormValueInput() { }

    /// <summary>Text, or a choice's value; <see langword="null"/> clears it.</summary>
    /// <param name="Text">The value.</param>
    public sealed record Value(string? Text) : FormValueInput;

    /// <summary>A checkbox's state.</summary>
    /// <param name="IsChecked">On or off.</param>
    public sealed record Checked(bool IsChecked) : FormValueInput;

    /// <summary>A list box's selection.</summary>
    /// <param name="Values">The selected values.</param>
    public sealed record SelectedValues(IReadOnlyList<string> Values) : FormValueInput;
}

/// <summary>One value of a batch.</summary>
/// <param name="FieldRef">The field.</param>
/// <param name="Value">Its value.</param>
public sealed record FormValueEntry(FormFieldRef FieldRef, FormValueInput Value);

/// <summary>The control a widget fills with.</summary>
public abstract record FormFillControl
{
    private protected FormFillControl() { }

    /// <summary>A text box.</summary>
    public sealed record Text(string Value, bool Multiline, bool Password, int? MaxLength, bool Comb) : FormFillControl;

    /// <summary>A checkbox or radio button.</summary>
    public sealed record Toggle(bool Radio, bool Checked, string ExportValue) : FormFillControl;

    /// <summary>A combo or list box.</summary>
    public sealed record Choice(bool List, bool Edit, bool Multi, IReadOnlyList<FormChoiceOption> Options, IReadOnlyList<string> Selected) : FormFillControl;

    /// <summary>A push button.</summary>
    public sealed record Button : FormFillControl;

    /// <summary>A signature field.</summary>
    public sealed record Signature(bool Signed) : FormFillControl;
}

/// <summary>A widget to fill, as a page's fill layer lists it.</summary>
public sealed record FormFillItem(
    FormFieldRef FieldRef, int AnnotObjectNumber, AnnotationRef? AnnotationRef, PdfRect Bounds, string LookJson, bool Disabled, string Label, FormFillControl Control);

/// <summary>The widget under a point.</summary>
/// <param name="AnnotObjectNumber">Its object number.</param>
/// <param name="Field">Its field.</param>
/// <param name="Bounds">Its box, page space.</param>
public sealed record FormWidgetHit(int AnnotObjectNumber, FormField Field, PdfRect Bounds);

/// <summary>Where a new widget goes.</summary>
public sealed record FormWidgetPlacement
{
    /// <summary>Its page.</summary>
    public PageRef? Page { get; init; }

    /// <summary>Its box, page space.</summary>
    public PdfRect? Bounds { get; init; }

    /// <summary>A checkbox's or radio button's export value.</summary>
    public string? ExportValue { get; init; }
}

/// <summary>A widget added to a field.</summary>
/// <param name="Field">The field now.</param>
/// <param name="WidgetJson">The widget, as JSON.</param>
public sealed record FormWidgetAdded(FormField Field, string WidgetJson);

/// <summary>What committing a value did.</summary>
public sealed record FormCommitResult(FormCommitStatus Status, bool Scripted, IReadOnlyList<string> Diagnostics, string? Error);

/// <summary>What setting a value did.</summary>
/// <param name="Field">The field now.</param>
/// <param name="Status">What happened.</param>
public sealed record FormSetValueResult(FormField Field, FormSetValueStatus Status);

/// <summary>A value a batch could not set.</summary>
public sealed record FormBatchFailure(FormFieldRef? FieldRef, string? Name, PluginErrorInfo Error);

/// <summary>A value a batch skipped.</summary>
/// <param name="Name">The field name.</param>
/// <param name="Reason">Why.</param>
public sealed record FormBatchSkip(string Name, string Reason);

/// <summary>What a batch of values did.</summary>
public sealed record FormBatchResult(IReadOnlyList<FormFieldRef> Applied, IReadOnlyList<FormBatchSkip> Skipped, IReadOnlyList<FormBatchFailure> Failed);

/// <summary>Which fields <see cref="FormCapability.GetFieldsAsync"/> returns.</summary>
public sealed record FormFilter
{
    /// <summary>Only this kind.</summary>
    public FormFieldFamily? Family { get; init; }

    /// <summary>Only fields with a widget on this page.</summary>
    public PageRef? Page { get; init; }

    /// <summary>Only this name.</summary>
    public string? Name { get; init; }
}

/// <summary>A new field and its first widget.</summary>
/// <param name="Family">Its kind.</param>
/// <param name="Name">Its name.</param>
/// <param name="Page">Its widget's page.</param>
/// <param name="Bounds">Its widget's box, page space.</param>
public sealed record FormCreateFieldInput(FormFieldFamily Family, string Name, PageRef Page, PdfRect Bounds)
{
    /// <summary>A choice field's options.</summary>
    public IReadOnlyList<FormChoiceOptionInput>? Options { get; init; }

    /// <summary>A checkbox's or radio button's export value.</summary>
    public string? ExportValue { get; init; }
}

/// <summary>A change to a field. Null members keep their value.</summary>
public sealed record FormFieldPatch
{
    /// <summary>Its partial name.</summary>
    public string? Name { get; init; }
    /// <summary>Read-only.</summary>
    public bool? ReadOnly { get; init; }
    /// <summary>Required.</summary>
    public bool? Required { get; init; }
    /// <summary>Left out of exports.</summary>
    public bool? NoExport { get; init; }
    /// <summary>The alternate name (tooltip).</summary>
    public string? AlternateName { get; init; }
    /// <summary>The export mapping name.</summary>
    public string? MappingName { get; init; }
    /// <summary>The default value.</summary>
    public string? DefaultValue { get; init; }
    /// <summary>A text field's maximum length, 0 or more.</summary>
    public int? MaxLength { get; init; }
    /// <summary>A choice field's options.</summary>
    public IReadOnlyList<FormChoiceOptionInput>? Options { get; init; }
    /// <summary>The optional members to clear.</summary>
    public IReadOnlyList<FormFieldPatchKey>? Clear { get; init; }
}

/// <summary>How <see cref="FormCapability.DuplicateAsync"/> and <see cref="FormCapability.DuplicateWidgetsAsync"/> copy.</summary>
public sealed record FormDuplicateOptions
{
    /// <summary>The page the copied widgets go on. Left out, each goes on its original's page.</summary>
    public PageRef? Page { get; init; }

    /// <summary>How far each copied widget moves, in page points: <c>X</c> to the right, <c>Y</c> down. A NaN or an infinity is <see cref="PdfErrorCode.InvalidInput"/>.</summary>
    public PdfPoint? Offset { get; init; }

    /// <summary>Whether a new field gets its original's value. Left <see langword="false"/>, it starts empty.</summary>
    public bool Values { get; init; }
}

/// <summary>What a duplicate made.</summary>
/// <param name="Fields">The fields made, and each field that got a new button, as they are now.</param>
/// <param name="Widgets">The new widgets; for <see cref="FormCapability.DuplicateWidgetsAsync"/>, in the order of the widgets given.</param>
/// <param name="WidgetsJson">The new widgets as the engine wrote them, a JSON array in the same order.</param>
/// <param name="DroppedJson">What was left out and why, as JSON.</param>
public sealed record FormDuplicateResult(IReadOnlyList<FormField> Fields, IReadOnlyList<AnnotationRef> Widgets, string WidgetsJson, string DroppedJson);

/// <summary>What an import created.</summary>
/// <param name="Fields">The new fields.</param>
/// <param name="RefMapJson">Bundle refs to new refs, as JSON.</param>
/// <param name="DroppedJson">What the import left out and why, as JSON.</param>
public sealed record FormImportResult(IReadOnlyList<FormField> Fields, string RefMapJson, string DroppedJson);

/// <summary>What a click on a widget did.</summary>
public abstract record FormActivationResult
{
    private protected FormActivationResult() { }

    /// <summary>The widget's value changed (a checkbox or radio button).</summary>
    /// <param name="Result">The commit.</param>
    public sealed record Committed(FormCommitResult Result) : FormActivationResult;

    /// <summary>The click went to the widget's actions.</summary>
    public sealed record Dispatched : FormActivationResult;
}

/// <summary>How <see cref="FormCapability.FocusAsync"/> focuses.</summary>
public sealed record FormFocusOptions
{
    /// <summary>Selects the field's text.</summary>
    public bool Select { get; init; }

    /// <summary>The widget to focus, for a field with several.</summary>
    public AnnotationRef? Widget { get; init; }
}

/// <summary>How <see cref="FormCapability.FocusNextAsync"/> moves.</summary>
public sealed record FormFocusNextOptions
{
    /// <summary>Backward in tab order.</summary>
    public bool Backward { get; init; }

    /// <summary>Only to an empty field.</summary>
    public bool Empty { get; init; }

    /// <summary>Wraps around at the end.</summary>
    public bool Wrap { get; init; }

    /// <summary>Where to start; the focused field when left out.</summary>
    public FormFieldRef? From { get; init; }
}

/// <summary>Something the form plugin did.</summary>
public abstract record FormEvent
{
    private protected FormEvent() { }

    /// <summary>Focus moved.</summary>
    public sealed record FocusChanged(FormField? Field, AnnotationRef? Widget) : FormEvent;

    /// <summary>A value changed.</summary>
    public sealed record ValueChanged(FormField Field, string OriginJson) : FormEvent;

    /// <summary>A field was created.</summary>
    public sealed record FieldCreated(FormField Field, string OriginJson) : FormEvent;

    /// <summary>A field changed.</summary>
    public sealed record FieldUpdated(FormField Field, string OriginJson) : FormEvent;

    /// <summary>A field was deleted.</summary>
    public sealed record FieldDeleted(FormFieldRef FieldRef, string OriginJson) : FormEvent;

    /// <summary>A script or rule refused a value.</summary>
    public sealed record ValidationRejected(FormField Field, IReadOnlyList<string> Messages) : FormEvent;

    /// <summary>The form was re-read from the engine.</summary>
    public sealed record Resynced : FormEvent;
}

/// <summary>
/// A view's text input for a widget: the form plugin moves keyboard focus
/// to it. Called on the kernel thread; a throw is logged and dropped, and
/// disposing the viewer there throws <see cref="InvalidOperationException"/>.
/// </summary>
public interface IFormFocusTarget
{
    /// <summary>Take keyboard focus; <paramref name="select"/> selects its text.</summary>
    void Focus(bool select);
}
