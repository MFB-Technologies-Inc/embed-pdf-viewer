using GL = uniffi.epdf_plugin_i18n;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static LocalePack ToPublic(GL.LocalePack pack) =>
        new(pack.Code, pack.Name, pack.Translations) { Direction = pack.Dir is { } dir ? (TextDirection)(int)dir : null };

    internal static GL.LocalePack ToFfi(LocalePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return new(
            Code: pack.Code ?? throw new ArgumentException("The pack has no code.", nameof(pack)),
            Name: pack.Name ?? throw new ArgumentException("The pack has no name.", nameof(pack)),
            Translations: (pack.Translations ?? new Dictionary<string, string>()).ToDictionary(pair => pair.Key, pair => pair.Value),
            Dir: pack.Direction is { } dir ? (GL.TextDirection)(int)dir : null);
    }

    internal static GL.I18nParam ToFfi(I18nParam value) => value switch
    {
        I18nParam.Text text => new GL.I18nParam.Text(text.Value),
        I18nParam.Number number => new GL.I18nParam.Number(number.Value),
        _ => throw new ArgumentException("An i18n parameter is null.", nameof(value)),
    };

    internal static I18nSettings ToPublic(GL.I18nSettings settings) =>
        new(settings.Locale, settings.FallbackLocale, [.. settings.Locales.Select(ToPublic)]);

    internal static GL.I18nSettingsPatch ToFfi(I18nSettingsPatch patch) => new(
        Locale: patch.Locale,
        ClearLocale: patch.ClearLocale,
        FallbackLocale: patch.FallbackLocale,
        Locales: patch.Locales is null ? null : [.. patch.Locales.Select(ToFfi)]);

    internal static I18nEvent ToPublic(GL.I18nEvent e) => e switch
    {
        GL.I18nEvent.LocaleChanged c => new I18nEvent.LocaleChanged(c.Event.Locale, c.Event.PreviousLocale),
        GL.I18nEvent.LocaleLoadFailed f => new I18nEvent.LocaleLoadFailed(f.Event.Locale, ToPublic(f.Event.Error)),
        _ => throw Unknown(e),
    };
}
