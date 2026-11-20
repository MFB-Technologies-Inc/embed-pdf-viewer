using G = uniffi.epdf_viewer_host;
using GF = uniffi.epdf_ffi;

namespace EmbedPDF;

/// <summary>Maps between the generated records and the public ones.</summary>
internal static partial class Ffi
{
    internal static PluginErrorInfo ToPublic(GF.PluginErrorInfo error) =>
        new(new PdfErrorCode(error.Code), error.Message, error.Capability, error.Permission);

    internal static DocumentStatus ToPublic(G.DocumentStatus status) => status switch
    {
        G.DocumentStatus.Loading => DocumentStatus.Loading,
        G.DocumentStatus.Locked => DocumentStatus.Locked,
        G.DocumentStatus.Ready => DocumentStatus.Ready,
        _ => DocumentStatus.Failed,
    };

    internal static DocumentInfo ToPublic(G.DocumentInfo info) =>
        new(info.Id, info.Name, WireInteger.ToInt(info.PageCount, "page count"), ToPublic(info.Status),
            info.Error is { } error ? ToPublic(error) : null);

    internal static DocumentInfo? ToPublicOrNull(G.DocumentInfo? info) => info is null ? null : ToPublic(info);

    internal static PageRef ToPublic(GF.PageRef page) => new(WireInteger.ToInt(page.PageObjectNumber, "page object number"));

    internal static GF.PageRef ToFfi(PageRef page) => new(WireInteger.ToUInt32(page.PageObjectNumber, "page object number"));

    internal static PageInfo ToPublic(GF.PageInfo page) =>
        new(ToPublic(page.Page), WireInteger.ToInt(page.Index, "page index"), page.Width, page.Height,
            WireInteger.ToInt(page.Rotation, "page rotation"));

    internal static PageInfo? ToPublicOrNull(GF.PageInfo? page) => page is null ? null : ToPublic(page);

    internal static G.SaveMode ToFfi(SaveMode mode) => mode == SaveMode.Rewrite ? G.SaveMode.Rewrite : G.SaveMode.Incremental;

    internal static ViewerSettings ToPublic(G.ViewerSettings settings) => new(settings.Accent, settings.PageBackground, settings.PageShadow);

    internal static G.ViewerSettingsPatch ToFfi(ViewerSettingsPatch patch) =>
        new(patch.Accent, patch.PageBackground, patch.PageShadow, patch.Clear.Select(field => field switch
        {
            ViewerSettingsField.Accent => G.ViewerSettingsField.Accent,
            ViewerSettingsField.PageBackground => G.ViewerSettingsField.PageBackground,
            _ => G.ViewerSettingsField.PageShadow,
        }).ToArray());

    internal static DocumentEvent ToPublic(G.DocumentEvent @event) => @event switch
    {
        G.DocumentEvent.Opened opened => new DocumentEvent.Opened(ToPublic(opened.Document)),
        G.DocumentEvent.OpenFailed failed => new DocumentEvent.OpenFailed(failed.DocumentId, ToPublic(failed.Error)),
        G.DocumentEvent.Locked locked => new DocumentEvent.Locked(locked.DocumentId, locked.PasswordProvided),
        G.DocumentEvent.Closed closed => new DocumentEvent.Closed(closed.DocumentId),
        G.DocumentEvent.ActiveChanged active => new DocumentEvent.ActiveChanged(active.DocumentId),
        G.DocumentEvent.PagesChanged pages => new DocumentEvent.PagesChanged(pages.DocumentId),
        _ => throw new PdfException("RangeError", PdfErrorCode.InvalidState, $"unknown document event {@event.GetType().Name}"),
    };
}
