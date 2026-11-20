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
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void CasesMatchInOrder(Type mirror, Type generated)
    {
        Assert.Equal(Enum.GetNames(generated), Enum.GetNames(mirror));
        Assert.Equal(Enum.GetValues(generated).Cast<object>().Select(Convert.ToInt32), Enum.GetValues(mirror).Cast<object>().Select(Convert.ToInt32));
    }
}
