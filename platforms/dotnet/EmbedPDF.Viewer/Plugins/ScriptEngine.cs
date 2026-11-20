using GA = uniffi.epdf_plugin_actions;

namespace EmbedPDF;

public sealed partial record ActionsJavaScriptConfig
{
    internal GA.ActionsJavaScriptConfig ToFfi(PluginContext context) => new(
        Enabled: Enabled,
        Engine: null,
        Budget: Budget is null ? null : Ffi.ToFfi(Budget),
        MaxScriptNodesPerDispatch: WireInteger.ToUInt32(MaxScriptNodesPerDispatch, "maxScriptNodesPerDispatch"));
}
