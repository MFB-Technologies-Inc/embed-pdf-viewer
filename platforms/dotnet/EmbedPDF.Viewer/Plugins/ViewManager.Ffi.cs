using GV = uniffi.epdf_plugin_view_manager;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static PaneInfo ToPublic(GV.PaneInfo pane) => new(pane.Id, pane.DocumentIds, pane.ActiveDocumentId);

    internal static ViewManagerEvent ToPublic(GV.ViewManagerEvent e) => e switch
    {
        GV.ViewManagerEvent.PaneCreated c => new ViewManagerEvent.PaneCreated(c.Event.PaneId),
        GV.ViewManagerEvent.PaneRemoved r => new ViewManagerEvent.PaneRemoved(r.Event.PaneId),
        GV.ViewManagerEvent.FocusChanged f => new ViewManagerEvent.FocusChanged(f.Event.PaneId),
        GV.ViewManagerEvent.DocumentMoved m => new ViewManagerEvent.DocumentMoved(m.Event.DocumentId, m.Event.FromPaneId, m.Event.ToPaneId),
        _ => throw Unknown(e),
    };
}
