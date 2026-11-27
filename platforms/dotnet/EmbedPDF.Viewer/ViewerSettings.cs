namespace EmbedPDF;

/// <summary>
/// Presentation hints for an app's own chrome. CSS strings are returned for
/// the app to interpret; they paint nothing and change no engine authorization.
/// </summary>
/// <param name="Accent">The accent color, or <see langword="null"/>.</param>
/// <param name="PageBackground">The page background, or <see langword="null"/>.</param>
/// <param name="PageShadow">The page shadow, or <see langword="null"/>.</param>
public sealed record ViewerSettings(string? Accent, string? PageBackground, string? PageShadow);

/// <summary>A <see cref="ViewerSettings"/> field <see cref="ViewerSettingsPatch.Clear"/> can name.</summary>
public enum ViewerSettingsField
{
    /// <summary><see cref="ViewerSettings.Accent"/>.</summary>
    Accent,
    /// <summary><see cref="ViewerSettings.PageBackground"/>.</summary>
    PageBackground,
    /// <summary><see cref="ViewerSettings.PageShadow"/>.</summary>
    PageShadow,
}

/// <summary>
/// A change to <see cref="ViewerSettings"/>. Omitted fields stay unchanged;
/// <see cref="Clear"/> writes null. Setting and clearing the same field is
/// <c>invalid-input</c>.
/// </summary>
public sealed record ViewerSettingsPatch
{
    /// <summary>The new accent, or <see langword="null"/> to leave it.</summary>
    public string? Accent { get; init; }

    /// <summary>The new page background, or <see langword="null"/> to leave it.</summary>
    public string? PageBackground { get; init; }

    /// <summary>The new page shadow, or <see langword="null"/> to leave it.</summary>
    public string? PageShadow { get; init; }

    /// <summary>Fields to set to null.</summary>
    public IReadOnlyList<ViewerSettingsField> Clear { get; init; } = [];
}
