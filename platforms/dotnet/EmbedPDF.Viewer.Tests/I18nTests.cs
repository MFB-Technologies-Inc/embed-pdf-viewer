using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class I18nTests
{
    static readonly LocalePack English = new("en", "English", new Dictionary<string, string> { ["hello"] = "Hello", ["greet"] = "Hi {name}" });

    static readonly ILocaleLoader Resources = new EmbeddedResourceLocaleLoader(typeof(I18nTests).Assembly, "Locales.");

    static async Task<(PdfViewer Viewer, I18nCapability I18n)> Started(ILocaleLoader? loader = null, IReadOnlyList<string>? lazy = null)
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            I18n = new() { Locale = "en", Locales = [English], LazyLocales = lazy ?? ["es"], Loader = loader ?? Resources },
        });
        return (viewer, await viewer.GetI18nAsync());
    }

    static async Task<T> First<T>(IAsyncEnumerable<I18nEvent> events) where T : I18nEvent
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var e in events.WithCancellation(timeout.Token))
        {
            if (e is T found) return found;
        }
        throw new InvalidOperationException("the stream ended first");
    }

    [Fact]
    public async Task TranslatesAndInterpolates()
    {
        var (viewer, i18n) = await Started();
        await using var _ = viewer;
        Assert.Equal("Hello", await i18n.TranslateAsync("hello"));
        Assert.Equal("Hi Ana", await i18n.TranslateAsync("greet", new Dictionary<string, I18nParam> { ["name"] = "Ana" }));
        Assert.Equal("Fallback", await i18n.TranslateAsync("nope", fallback: "Fallback"));
        Assert.True(await i18n.HasKeyAsync("hello"));
        Assert.False(await i18n.HasKeyAsync("nope"));
        Assert.Equal(TextDirection.Ltr, await i18n.GetDirectionAsync());
    }

    [Fact]
    public async Task ALazySwitchReadsTheResourceAndAnnounces()
    {
        var (viewer, i18n) = await Started();
        await using var _ = viewer;
        var events = i18n.ObserveEvents();
        await i18n.SetLocaleAsync("es");
        var changed = await First<I18nEvent.LocaleChanged>(events);
        Assert.Equal(("es", "en"), (changed.Locale, changed.PreviousLocale));
        Assert.Equal("2 páginas", await i18n.TranslateAsync("pages", new Dictionary<string, I18nParam> { ["count"] = 2.0 }));
        Assert.Equal(["en", "es"], (await i18n.GetLocalesAsync()).Select(l => l.Code));
        Assert.Null(await i18n.GetLoadingLocaleAsync());
    }

    sealed class Refusing : ILocaleLoader
    {
        public Task<LocalePack> LoadAsync(string code) =>
            Task.FromException<LocalePack>(new PdfException("PluginError", PdfErrorCode.PermissionDenied, "offline policy"));
    }

    [Fact]
    public async Task ALoaderFailureKeepsItsCodeInTheEvent()
    {
        var (viewer, i18n) = await Started(new Refusing());
        await using var _ = viewer;
        var events = i18n.ObserveEvents();
        await Assert.ThrowsAsync<PdfException>(() => i18n.SetLocaleAsync("es"));
        var failed = await First<I18nEvent.LocaleLoadFailed>(events);
        Assert.Equal("permission-denied", failed.Error.Code.Value);
        Assert.Equal("offline policy", failed.Error.Message);
    }

    [Fact]
    public async Task AMissingPackFailsTheSwitch()
    {
        var (viewer, i18n) = await Started(new EmbeddedResourceLocaleLoader(typeof(I18nTests).Assembly, "Nowhere."));
        await using var _ = viewer;
        var error = await Assert.ThrowsAsync<PdfException>(() => i18n.SetLocaleAsync("es"));
        Assert.Equal(PdfErrorCode.OperationFailed, error.Code);
        Assert.Equal("en", await i18n.GetLocaleAsync());
    }

    sealed class Broken(Func<string, Task<LocalePack>> load) : ILocaleLoader
    {
        public Task<LocalePack> LoadAsync(string code) => load(code);
    }

    public static TheoryData<string> BrokenLoaders => ["null task", "null pack", "throws", "faults"];

    [Theory]
    [MemberData(nameof(BrokenLoaders))]
    public async Task ABrokenLoaderFailsTheSwitch(string kind)
    {
        Func<string, Task<LocalePack>> load = kind switch
        {
            "null task" => _ => null!,
            "null pack" => _ => Task.FromResult<LocalePack>(null!),
            "throws" => _ => throw new InvalidOperationException("synchronous"),
            _ => async _ => { await Task.Yield(); throw new IOException("disk"); },
        };
        var (viewer, i18n) = await Started(new Broken(load));
        await using var _ = viewer;
        var error = await Assert.ThrowsAsync<PdfException>(() => i18n.SetLocaleAsync("es").WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(PdfErrorCode.OperationFailed, error.Code);
        Assert.Equal("en", await i18n.GetLocaleAsync());
    }

    [Fact]
    public async Task DisposingTheViewerFromALoaderWorks()
    {
        using var watch = new CancellationTests.UnobservedWatch();
        await DisposeFromALoader();
        watch.Collect();
        Assert.Empty(watch.Seen);
    }

    static async Task DisposeFromALoader()
    {
        PdfViewer? viewer = null;
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new Broken(_ =>
        {
            viewer!.Dispose();
            disposed.TrySetResult();
            return Task.FromResult(English);
        });
        (viewer, var i18n) = await Started(loader);
        // The switch the loader's disposal cuts short is observed: left
        // unawaited, its instance-closed fault reaches the finalizer thread.
        var switching = i18n.SetLocaleAsync("es");
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var cut = await Record.ExceptionAsync(() => switching);
        Assert.True(cut is null || cut is PdfException { Code.Value: "instance-closed" }, cut?.ToString());
        var error = await Assert.ThrowsAsync<PdfException>(() => i18n.GetLocaleAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
    }

    /// <summary>
    /// I18n across viewer disposal leaves no unobserved task exception: every
    /// task the facade starts is observed, and the caller's own calls are
    /// awaited. A watch catches what the finalizer thread would rethrow.
    /// </summary>
    [Fact]
    public async Task I18nAcrossDisposalLeavesNoUnobservedTaskException()
    {
        using var watch = new CancellationTests.UnobservedWatch();
        for (var round = 0; round < 3; round++)
        {
            PdfViewer? viewer = null;
            var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var loader = new Broken(_ =>
            {
                viewer!.Dispose();
                disposed.TrySetResult();
                return Task.FromResult(English);
            });
            (viewer, var i18n) = await Started(loader);
            var events = i18n.ObserveEvents();
            var reading = Task.Run(async () => { await foreach (var _ in events) { } });
            var switching = i18n.SetLocaleAsync("es");
            await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Record.ExceptionAsync(() => switching);
            await reading.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAsync<PdfException>(() => i18n.TranslateAsync("annotation.ink"));
            i18n.Dispose();
        }
        watch.Collect();
        Assert.Empty(watch.Seen);
    }

    [Fact]
    public async Task AnUnknownLocaleIsNotFound()
    {
        var (viewer, i18n) = await Started();
        await using var _ = viewer;
        var error = await Assert.ThrowsAsync<PdfException>(() => i18n.SetLocaleAsync("fr"));
        Assert.Equal(PdfErrorCode.NotFound, error.Code);
    }

    [Fact]
    public async Task ARegisteredPackIsUsableAndRemovable()
    {
        var (viewer, i18n) = await Started();
        await using var _ = viewer;
        var registration = await i18n.RegisterLocaleAsync(new LocalePack("de", "Deutsch", new Dictionary<string, string> { ["hello"] = "Hallo" }));
        await i18n.SetLocaleAsync("de");
        Assert.Equal("Hallo", await i18n.TranslateAsync("hello"));
        await i18n.AddTranslationsAsync("de", new Dictionary<string, string> { ["bye"] = "Tschüss" });
        Assert.Equal("Tschüss", await i18n.TranslateAsync("bye"));
        await i18n.SetLocaleAsync("en");
        registration.Unregister();
        await Assert.ThrowsAsync<PdfException>(() => i18n.SetLocaleAsync("de"));
    }

    [Fact]
    public void AConfigWithALoaderEqualsItself()
    {
        var config = new I18nConfig { LazyLocales = ["es"], Loader = Resources };
        Assert.Equal(config, config with { });
        Assert.Equal(config.GetHashCode(), (config with { }).GetHashCode());
    }

    [Fact]
    public void ALocaleFileDecodes()
    {
        using var stream = typeof(I18nTests).Assembly.GetManifestResourceStream("Locales.es.json")!;
        var pack = LocalePack.FromJson(new StreamReader(stream).ReadToEnd());
        Assert.Equal(("es", "Español"), (pack.Code, pack.Name));
        Assert.Equal("{count} páginas", pack.Translations["pages.other"]);
        Assert.Equal(PdfErrorCode.InvalidInput, Assert.Throws<PdfException>(() => LocalePack.FromJson("{")).Code);
    }

    [Fact]
    public async Task SettingsRoundTrip()
    {
        var (viewer, i18n) = await Started();
        await using var _ = viewer;
        var settings = await i18n.GetSettingsAsync();
        Assert.Equal("en", settings.Locale);
        Assert.Contains(settings.Locales, pack => pack.Code == "en");
        await i18n.UpdateSettingsAsync(new I18nSettingsPatch { FallbackLocale = "es" });
        Assert.Equal("es", (await i18n.GetSettingsAsync()).FallbackLocale);
        await i18n.ResetSettingsAsync();
        Assert.Equal(settings.FallbackLocale, (await i18n.GetSettingsAsync()).FallbackLocale);
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, i18n) = await Started();
        await using var _ = viewer;
        using (var cancel = new CancellationTokenSource())
        {
            var reading = Task.Run(async () => { await foreach (var _ in i18n.ObserveEvents(cancel.Token)) { } });
            await Task.Delay(20);
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
        }
        await i18n.SetLocaleAsync("es");
    }

    [Fact]
    public async Task ADisposedViewersCapabilityThrows()
    {
        var (viewer, i18n) = await Started();
        await viewer.DisposeAsync();
        var error = await Assert.ThrowsAsync<PdfException>(() => i18n.GetLocaleAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
    }
}
