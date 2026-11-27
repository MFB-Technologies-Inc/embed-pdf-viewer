using GL = uniffi.epdf_plugin_link;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static LinkItem ToPublic(GL.LinkItem l) => new(
        l.Id, ToPublic(l.Page), ToPublic(l.Bounds), l.TargetJson, l.Attached, l.AnnotationJson, l.HasAction,
        l.Hover is { } h ? new LinkHoverFlags(h.Enter, h.Exit) : null);

    internal static GL.LinkInput ToLinkInput(LinkItem link) =>
        new GL.LinkInput.Link(ToFfi(link.Page), link.Id ?? throw new ArgumentException("A link needs its id.", nameof(link)));

    internal static LinkResolution ToPublic(GL.LinkResolution r) => r switch
    {
        GL.LinkResolution.Destination d => new LinkResolution.Destination(d.DestinationJson),
        GL.LinkResolution.Uri u => new LinkResolution.Uri(u.UriValue),
        GL.LinkResolution.Named n => new LinkResolution.Named(n.Name),
        GL.LinkResolution.Reported x => new LinkResolution.Reported(x.TargetJson),
        _ => throw Unknown(r),
    };

    internal static LinkEvent ToPublic(GL.LinkEvent e) => e switch
    {
        GL.LinkEvent.Activated a => new LinkEvent.Activated(a.Event.TargetJson, ToPublic(a.Event.Activation)),
        GL.LinkEvent.Loaded l => new LinkEvent.Loaded(ToPublic(l.Event.Page)),
        _ => throw Unknown(e),
    };
}
