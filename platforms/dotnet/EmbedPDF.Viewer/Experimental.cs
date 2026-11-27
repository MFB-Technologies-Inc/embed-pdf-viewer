namespace EmbedPDF;

/// <summary>
/// The diagnostic id that marks experimental members with
/// <c>[Experimental("EPDF001")]</c>. A caller opts in with
/// <c>#pragma warning disable EPDF001</c> or <c>&lt;NoWarn&gt;EPDF001&lt;/NoWarn&gt;</c>.
/// No member wears it yet: the experimental bridge benchmarks are compiled
/// only into a host built with the <c>manual-bridge</c> feature, which the
/// packaged runtime is not.
/// </summary>
internal static class Experimental
{
    internal const string Diagnostic = "EPDF001";
}
