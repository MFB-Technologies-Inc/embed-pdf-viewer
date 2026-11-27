using GS = uniffi.epdf_plugin_shell;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static ShellSurface ToPublic(GS.ShellSurface s) => new(s.Id, s.Open, s.Exclusive, s.PropsJson, s.FocusRequest);

    internal static ShellSnapshot ToPublic(GS.ShellSnapshot s) =>
        new([.. s.Surfaces.Select(x => new ShellSnapshotSurface(x.Id, x.Open, x.Exclusive, x.PropsJson))], s.OpenMenus);

    internal static GS.ShellSnapshot ToFfi(ShellSnapshot s) =>
        new([.. s.Surfaces.Select(x => new GS.ShellSnapshotSurface(x.Id, x.Open, x.Exclusive, x.PropsJson))], [.. s.OpenMenus]);

    internal static ShellEvent ToPublic(GS.ShellEvent e) => e switch
    {
        GS.ShellEvent.SurfaceOpened o => new ShellEvent.SurfaceOpened(o.Event.Id, o.Event.PropsJson),
        GS.ShellEvent.SurfaceClosed c => new ShellEvent.SurfaceClosed(c.Event.Id),
        GS.ShellEvent.MenuOpened o => new ShellEvent.MenuOpened(o.Event.Id),
        GS.ShellEvent.MenuClosed c => new ShellEvent.MenuClosed(c.Event.Id),
        _ => throw Unknown(e),
    };
}
