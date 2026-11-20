using GF = uniffi.epdf_plugin_form;
using GH = uniffi.epdf_viewer_host;
using GM = uniffi.epdf_plugin_measurement;
using GR = uniffi.epdf_plugin_render;
using GRd = uniffi.epdf_plugin_redaction;
using GS = uniffi.epdf_plugin_search;
using GSg = uniffi.epdf_plugin_signature;
using GSt = uniffi.epdf_plugin_stage;
using GSp = uniffi.epdf_plugin_stamp;

namespace EmbedPDF;

// Each plugin's settings arrive with its capability in Phase 3; until then a
// config installs its plugin with the engine's defaults.

public sealed partial record SearchConfig
{
    internal GS.SearchConfig ToFfi() => new(Reveal is null ? null : Ffi.ToFfi(Reveal));
}

public sealed partial record RenderConfig
{
    internal GR.RenderConfig ToFfi() => new(FullPage is null ? null : Ffi.ToFfi(FullPage), Debug);
}

/// <summary>What a callback adapter needs from its viewer: the kernel thread mark and the logger.</summary>
internal sealed record PluginContext(KernelMark Mark, Microsoft.Extensions.Logging.ILogger Logger);

/// <summary>Installs the stage plugin: the viewport lens, one per document.</summary>
public sealed record StageConfig
{
    internal GSt.StageConfig ToFfi() => new();
}

/// <summary>Installs the form plugin. Needs <see cref="PdfViewerOptions.Interaction"/>.</summary>
public sealed record FormConfig
{
    internal GF.FormConfig ToFfi() => new();
}

/// <summary>Installs the redaction plugin. Needs <see cref="PdfViewerOptions.Annotation"/>.</summary>
public sealed record RedactionConfig
{
    internal GRd.RedactionConfig ToFfi() => new();
}

/// <summary>Installs the measurement plugin. Needs <see cref="PdfViewerOptions.Interaction"/> and <see cref="PdfViewerOptions.Annotation"/>.</summary>
public sealed record MeasurementConfig
{
    internal GM.MeasurementConfig ToFfi() => new();
}

/// <summary>Installs the stamp plugin.</summary>
public sealed record StampConfig
{
    internal GSp.StampConfig ToFfi() => new();
}

/// <summary>Installs the signature plugin. Needs <see cref="PdfViewerOptions.Form"/>.</summary>
public sealed record SignatureConfig
{
    internal GSg.SignatureConfig ToFfi() => new();
}

/// <summary>An owner's standard command list, for <see cref="PdfViewerOptions.StandardCommands"/>.</summary>
public enum StandardCommandList
{
    /// <summary>Zoom, the page moves and view rotation.</summary>
    Stage,
    /// <summary><c>tool:pointer</c>, <c>tool:pan</c>, <c>tool:zoom</c>, and <c>tool:&lt;id&gt;</c> for every other tool.</summary>
    Interaction,
    /// <summary><c>selection:copy</c>.</summary>
    Selection,
    /// <summary><c>annotation:delete</c>.</summary>
    Annotation,
    /// <summary><c>history:undo</c> and <c>history:redo</c>.</summary>
    History,
    /// <summary>The snapshot tool, and copy, save and clear for the snapshot.</summary>
    Render,
    /// <summary><c>document:download</c> and <c>document:print</c>.</summary>
    Document,
}

internal static class StandardCommandListMapping
{
    internal static GH.StandardCommandList ToFfi(this StandardCommandList list) =>
        list switch
        {
            StandardCommandList.Stage => GH.StandardCommandList.Stage,
            StandardCommandList.Interaction => GH.StandardCommandList.Interaction,
            StandardCommandList.Selection => GH.StandardCommandList.Selection,
            StandardCommandList.Annotation => GH.StandardCommandList.Annotation,
            StandardCommandList.History => GH.StandardCommandList.History,
            StandardCommandList.Render => GH.StandardCommandList.Render,
            StandardCommandList.Document => GH.StandardCommandList.Document,
            _ => throw Ffi.Undefined(list, "standard command list"),
        };
}
