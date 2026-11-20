using System.Reflection;
using Microsoft.Extensions.Logging;
using GF = uniffi.epdf_ffi;
using GL = uniffi.epdf_plugin_i18n;

namespace EmbedPDF;

/// <summary>Which way a locale's text runs.</summary>
public enum TextDirection
{
    /// <summary>Left to right.</summary>
    Ltr,
    /// <summary>Right to left.</summary>
    Rtl,
}

/// <summary>A locale's translations.</summary>
/// <param name="Code">The locale code, such as <c>es</c>.</param>
/// <param name="Name">The name to show, such as <c>Español</c>.</param>
/// <param name="Translations">Flat keys (<c>zoom.in</c>) to text; plural forms are <c>key.one</c>, <c>key.other</c>.</param>
public sealed record LocalePack(string Code, string Name, IReadOnlyDictionary<string, string> Translations)
{
    /// <summary>The text direction; <see langword="null"/> is <see cref="TextDirection.Ltr"/>.</summary>
    public TextDirection? Direction { get; init; }

    /// <summary>
    /// A locale file in the web's shape (<c>{code, name, dir?, translations}</c>,
    /// translations nested or flat). Throws <see cref="PdfException"/> with
    /// <see cref="PdfErrorCode.InvalidInput"/> for a malformed file.
    /// </summary>
    public static LocalePack FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        NativeRuntime.EnsureLoaded();
        try
        {
            return Ffi.ToPublic(GL.EpdfPluginI18nMethods.LocalePackFromJson(json));
        }
        catch (Exception error)
        {
            throw PdfException.From(error);
        }
    }
}

/// <summary>A locale the viewer knows.</summary>
/// <param name="Code">The locale code.</param>
/// <param name="Name">The name to show.</param>
/// <param name="Direction">The text direction.</param>
/// <param name="Loaded">Whether its pack is loaded; a lazy locale is not until it is first used.</param>
public sealed record LocaleInfo(string Code, string Name, TextDirection Direction, bool Loaded);

/// <summary>A value a translation interpolates. A <see cref="Number"/> named <c>count</c> picks a plural form.</summary>
public abstract record I18nParam
{
    private protected I18nParam() { }

    /// <summary>Text.</summary>
    /// <param name="Value">The text.</param>
    public sealed record Text(string Value) : I18nParam;

    /// <summary>A number.</summary>
    /// <param name="Value">The number.</param>
    public sealed record Number(double Value) : I18nParam;

    /// <summary>Text.</summary>
    public static implicit operator I18nParam(string value) => new Text(value);

    /// <summary>A number.</summary>
    public static implicit operator I18nParam(double value) => new Number(value);
}

/// <summary>I18n settings.</summary>
/// <param name="Locale">The chosen locale, or <see langword="null"/> for the fallback.</param>
/// <param name="FallbackLocale">The locale used when a key is missing.</param>
/// <param name="Locales">The registered packs.</param>
public sealed record I18nSettings(string? Locale, string FallbackLocale, IReadOnlyList<LocalePack> Locales);

/// <summary>A change to <see cref="I18nSettings"/>; <see langword="null"/> keeps a value.</summary>
public sealed record I18nSettingsPatch
{
    /// <summary>New <see cref="I18nSettings.Locale"/>.</summary>
    public string? Locale { get; init; }

    /// <summary>Clears <see cref="I18nSettings.Locale"/>.</summary>
    public bool ClearLocale { get; init; }

    /// <summary>New <see cref="I18nSettings.FallbackLocale"/>.</summary>
    public string? FallbackLocale { get; init; }

    /// <summary>New <see cref="I18nSettings.Locales"/>.</summary>
    public IReadOnlyList<LocalePack>? Locales { get; init; }
}

/// <summary>An event from <see cref="I18nCapability.ObserveEvents"/>.</summary>
public abstract record I18nEvent
{
    private protected I18nEvent() { }

    /// <summary>The locale changed.</summary>
    /// <param name="Locale">The locale now in use.</param>
    /// <param name="PreviousLocale">The locale before.</param>
    public sealed record LocaleChanged(string Locale, string PreviousLocale) : I18nEvent;

    /// <summary>A lazy locale's pack failed to load.</summary>
    /// <param name="Locale">The locale.</param>
    /// <param name="Error">Why; a loader's <see cref="PdfException"/> keeps its code here.</param>
    public sealed record LocaleLoadFailed(string Locale, PluginErrorInfo Error) : I18nEvent;
}

/// <summary>
/// Serves lazy locale packs (<see cref="I18nConfig.LazyLocales"/>) when the
/// viewer first switches to one.
/// </summary>
/// <remarks>
/// Called on the thread pool, never on the kernel thread, so it may take its
/// time and may call the viewer. A <see cref="PdfException"/> it throws keeps
/// its code in <see cref="I18nEvent.LocaleLoadFailed"/>; any failure, or a
/// <see langword="null"/> pack, fails the switch with
/// <see cref="PdfErrorCode.OperationFailed"/>.
/// </remarks>
public interface ILocaleLoader
{
    /// <summary>The pack for <paramref name="code"/>.</summary>
    Task<LocalePack> LoadAsync(string code);
}

/// <summary>
/// Serves lazy packs from JSON files embedded in an assembly, in the web's
/// locale file shape. The pack for <c>es</c> is the manifest resource
/// <c>resourcePrefix + "es.json"</c>.
/// </summary>
/// <remarks>
/// MSBuild names an embedded <c>Locales/es.json</c>
/// <c>&lt;RootNamespace&gt;.Locales.es.json</c>; pass
/// <c>"&lt;RootNamespace&gt;.Locales."</c> as the prefix, or give the files a
/// <c>LogicalName</c> such as <c>Locales.%(Filename)%(Extension)</c> and pass
/// <c>"Locales."</c>.
/// </remarks>
/// <param name="assembly">The assembly that embeds the files.</param>
/// <param name="resourcePrefix">What precedes <c>&lt;code&gt;.json</c> in each resource's name.</param>
public sealed class EmbeddedResourceLocaleLoader(Assembly assembly, string resourcePrefix = "") : ILocaleLoader
{
    readonly Assembly assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
    readonly string resourcePrefix = resourcePrefix ?? throw new ArgumentNullException(nameof(resourcePrefix));

    /// <inheritdoc/>
    public async Task<LocalePack> LoadAsync(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var name = resourcePrefix + code + ".json";
        await using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new PdfException("NotFound", PdfErrorCode.NotFound, $"no {name} in {assembly.GetName().Name}");
        using var reader = new StreamReader(stream);
        return LocalePack.FromJson(await reader.ReadToEndAsync().ConfigureAwait(false));
    }
}

/// <summary>Installs the i18n plugin: locale packs and translations.</summary>
public sealed record I18nConfig
{
    /// <summary>The locale to start in; <see langword="null"/> is the fallback.</summary>
    public string? Locale { get; init; }

    /// <summary>The locale used when a key is missing; <see langword="null"/> is the engine's default.</summary>
    public string? FallbackLocale { get; init; }

    /// <summary>Packs registered at start.</summary>
    public IReadOnlyList<LocalePack> Locales { get; init; } = [];

    /// <summary>Codes whose packs <see cref="Loader"/> serves on first use.</summary>
    public IReadOnlyList<string> LazyLocales { get; init; } = [];

    /// <summary>
    /// Serves <see cref="LazyLocales"/>. The viewer holds it until it is disposed, so one that captures the
    /// viewer or a capability keeps an abandoned viewer from being collected;
    /// dispose the viewer, or keep the viewer out of it.
    /// </summary>
    public ILocaleLoader? Loader { get; init; }

    internal GL.I18nConfig ToFfi(PluginContext context) => new(
        Locale: Locale,
        FallbackLocale: FallbackLocale,
        Locales: [.. Arguments.NoNullElements(Locales ?? [], nameof(Locales)).Select(Ffi.ToFfi)],
        LazyLocales: Arguments.NoNullElements(LazyLocales ?? [], nameof(LazyLocales)),
        Loader: Loader is null ? null : new LoaderAdapter(Loader, context.Logger));

    /// <summary>Runs the app's loader on the thread pool and answers the kernel once.</summary>
    internal sealed class LoaderAdapter(ILocaleLoader loader, ILogger logger) : GL.LocaleLoader
    {
        public void Load(string code, GL.LocaleLoadCompletion completion) => _ = Task.Run(() => Answer(code, completion));

        internal async Task Answer(string code, GL.ILocaleLoadCompletion completion)
        {
            GL.LocalePack? pack = null;
            GF.HostFailure? failure = null;
            try
            {
                var loading = loader.LoadAsync(code);
                var loaded = loading is null ? null : await loading.ConfigureAwait(false);
                if (loaded is null)
                {
                    failure = new GF.HostFailure("InvalidResponse", $"The locale loader returned no pack for {code}.", "operation-failed");
                }
                else
                {
                    pack = Ffi.ToFfi(loaded);
                }
            }
            catch (Exception error)
            {
                logger.LogDebug(error, "Loading locale {Code} failed.", code);
                failure = FetcherAdapter.Failure(error, cancelled: false);
            }
            try
            {
                if (pack is not null) completion.Finish(pack);
                else completion.FailWith(failure!);
            }
            catch (Exception)
            {
                // The viewer shut down first; nobody waits for this answer.
            }
            finally
            {
                (completion as IDisposable)?.Dispose();
            }
        }
    }
}

/// <summary>
/// The viewer's locale packs and translations. There is one per viewer and it
/// works before any document opens. Get it with
/// <see cref="PdfViewer.GetI18nAsync"/>; the viewer needs
/// <see cref="PdfViewerOptions.I18n"/>. After the viewer is disposed, every
/// call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
public sealed class I18nCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GL.I18nHandle handle;

    internal I18nCapability(PdfViewer viewer, GL.I18nHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Current settings, shared by every document using this plugin registration.</summary>
    public async Task<I18nSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Settings(), cancellationToken).ConfigureAwait(false));

    /// <summary>Merges the supplied fields; omission preserves them.</summary>
    public Task UpdateSettingsAsync(I18nSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (patch.Locales is not null)
        {
            foreach (var pack in Arguments.NoNullElements(patch.Locales, nameof(patch))) Ffi.Translations(pack);
        }
        return viewer.Run(() => handle.UpdateSettings(Ffi.ToFfi(patch)), cancellationToken);
    }

    /// <summary>Restores the values supplied at registration.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>
    /// The text for <paramref name="key"/> in the current locale, then the
    /// fallback locale, then <paramref name="fallback"/>, then the key itself.
    /// A <see cref="I18nParam.Number"/> named <c>count</c> picks a plural form.
    /// </summary>
    public Task<string> TranslateAsync(string key, IReadOnlyDictionary<string, I18nParam>? parameters = null, string? fallback = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        var values = parameters ?? new Dictionary<string, I18nParam>();
        Arguments.NoNullValues(values, nameof(parameters));
        return viewer.Run(() => handle.T(key, values.ToDictionary(pair => pair.Key, pair => Ffi.ToFfi(pair.Value)), fallback), cancellationToken);
    }

    /// <summary>Whether any loaded locale has <paramref name="key"/>.</summary>
    public Task<bool> HasKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return viewer.Run(() => handle.HasKey(key), cancellationToken);
    }

    /// <summary>The locale in use.</summary>
    public Task<string> GetLocaleAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.Locale(), cancellationToken);

    /// <summary>The locale's text direction.</summary>
    public async Task<TextDirection> GetDirectionAsync(CancellationToken cancellationToken = default) =>
        (TextDirection)(int)await viewer.Run(() => handle.Direction(), cancellationToken).ConfigureAwait(false);

    /// <summary>Registered packs, then lazy ones not loaded yet.</summary>
    public async Task<IReadOnlyList<LocaleInfo>> GetLocalesAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.Locales(), cancellationToken).ConfigureAwait(false))
            .Select(info => new LocaleInfo(info.Code, info.Name, (TextDirection)(int)info.Direction, info.Loaded))];

    /// <summary>The lazy locale being loaded for a switch, if any.</summary>
    public Task<string?> GetLoadingLocaleAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.LoadingLocale(), cancellationToken);

    /// <summary>
    /// Switches locale; completes once it is usable. Throws
    /// <see cref="PdfErrorCode.NotFound"/> for an unknown code,
    /// <see cref="PdfErrorCode.OperationFailed"/> when the pack fails to load and
    /// <see cref="PdfErrorCode.OperationCancelled"/> when a newer switch
    /// supersedes this one. <paramref name="cancellationToken"/> is checked
    /// before the switch is posted; it doesn't cancel the switch.
    /// </summary>
    public Task SetLocaleAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        return viewer.Run(() => handle.SetLocale(code), cancellationToken);
    }

    /// <summary>Registers a pack. Disposing the registration without unregistering keeps the pack; <see cref="Registration.Unregister"/> removes it.</summary>
    public async Task<Registration> RegisterLocaleAsync(LocalePack pack, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var ffi = Ffi.ToFfi(pack);
        return new(await viewer.Run(() => handle.RegisterLocale(ffi), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Merges flat keys (<c>zoom.in</c>) into a registered pack; later keys win.</summary>
    public Task AddTranslationsAsync(string code, IReadOnlyDictionary<string, string> translations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(translations);
        Arguments.NoNullValues(translations, nameof(translations));
        return viewer.Run(() => handle.AddTranslations(code, translations.ToDictionary(pair => pair.Key, pair => pair.Value)), cancellationToken);
    }

    /// <summary>
    /// Locale changes and load failures, subscribed when this returns. Ends
    /// when the viewer is disposed or <paramref name="cancellationToken"/>
    /// fires. Each call is a new subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<I18nEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. Calls after it throw <see cref="PdfErrorCode.InstanceClosed"/>.</summary>
    public void Dispose() => handle.Dispose();

    sealed class Relay() : EventRelay<GL.I18nEvent, I18nEvent>(Ffi.ToPublic), GL.I18nEventListener
    {
        public void OnEvent(GL.I18nEvent @event) => Deliver(@event);
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The viewer's i18n capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.I18n"/>.</summary>
    public async Task<I18nCapability> GetI18nAsync(CancellationToken cancellationToken = default) =>
        new(this, await CallAsync(h => h.I18n(), cancellationToken).ConfigureAwait(false));
}
