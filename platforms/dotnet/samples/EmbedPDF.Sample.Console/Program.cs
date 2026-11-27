// The EmbedPDF console sample. Usage:
//   EmbedPDF.Sample.Console <input.pdf> <output-dir> [--no-script]
// Runs one step at a time and prints "PASS <step>: <detail>" for each. The
// first step that fails prints "FAIL <step>: <type>: <message>" and the
// process exits 1; a usage error exits 64.
using EmbedPDF;
using EmbedPDF.Models;
using EmbedPDF.Sample;
using EmbedPDF.Scripting.Jint;
using Microsoft.Extensions.DependencyInjection;

if (args.Length is < 2 or > 3 || (args.Length == 3 && args[2] != "--no-script"))
{
    Console.Error.WriteLine("usage: EmbedPDF.Sample.Console <input.pdf> <output-dir> [--no-script]");
    return 64;
}
var input = args[0];
var outputDirectory = Directory.CreateDirectory(args[1]).FullName;
var runScript = args.Length == 2;
const string Query = "the";

var step = "di";
try
{
    // The DI extension builds the viewer from the options, as an app with a
    // service container would. Annotations need the interaction plugin.
    var services = new ServiceCollection()
        .AddEmbedPdf(options => options
            .Search(new SearchConfig())
            .Render(new RenderConfig())
            .Stage(new StageConfig())
            .Interaction()
            .Annotation()
            .History()
            .Actions(new ActionsConfig { JavaScript = new ActionsJavaScriptConfig(true) { Engine = new JintScriptEngineFactory() } }))
        .BuildServiceProvider();
    await using var _services = services;
    var viewer = services.GetRequiredService<PdfViewer>();
    await viewer.StartAsync();
    Pass($"PdfViewer from AddEmbedPdf, runtime {NativeRuntime.RuntimeIdentifier}");

    step = "open";
    var document = await viewer.OpenFileAsync(input);
    if (document.PageCount <= 0) throw new InvalidOperationException("the document has no pages");
    var firstPage = (await viewer.GetPagesAsync(document))[0].Page;
    Pass($"{Path.GetFileName(input)}, {document.PageCount} pages");

    step = "search";
    var search = await viewer.GetSearchAsync(document);
    var result = await search.SearchAsync(new SearchQuery(Query));
    if (result.HitCount <= 0) throw new InvalidOperationException($"no hits for \"{Query}\"");
    Pass($"\"{Query}\" {result.HitCount} hits ({result.Status})");

    step = "render";
    var render = await viewer.GetRenderAsync(document);
    var rendered = await render.RenderPageAsync(firstPage, new RenderPageOptions { Scale = 1 });
    var pngPath = Path.Combine(outputDirectory, "page-1.png");
    await using (var png = File.Create(pngPath))
    {
        Png.Write(png, rendered.Image);
    }
    Pass($"page 1 {rendered.Image.Width}x{rendered.Image.Height} -> {pngPath}");

    step = "annotate";
    var annotations = await viewer.GetAnnotationAsync(document);
    await annotations.WhenSyncedAsync();
    var before = (await annotations.ListAsync()).Count(a => a.Subtype == AnnotationSubtype.Square);
    var square = await annotations.CreateAsync(new SquareAnnotationDraft(new PdfRect(72, 72, 144, 72)), firstPage);
    var after = (await annotations.ListAsync()).Count(a => a.Subtype == AnnotationSubtype.Square);
    if (after != before + 1) throw new InvalidOperationException($"expected {before + 1} squares, found {after}");
    Pass($"square on page 1 ({square.GetType().Name})");

    step = "save";
    var saved = await viewer.SaveAsync(document);
    var savedPath = Path.Combine(outputDirectory, "annotated.pdf");
    await File.WriteAllBytesAsync(savedPath, saved);
    var reopened = await viewer.OpenAsync(saved, new OpenDocumentOptions { Activate = false });
    var reread = await viewer.GetAnnotationAsync(reopened);
    await reread.WhenSyncedAsync();
    var savedSquares = (await reread.ListAsync()).Count(a => a.Subtype == AnnotationSubtype.Square);
    if (savedSquares != after) throw new InvalidOperationException($"the saved copy has {savedSquares} squares, expected {after}");
    await viewer.CloseAsync(reopened);
    Pass($"{saved.Length} bytes -> {savedPath}, reopened with {savedSquares} square(s)");

    step = "model";
    // No SynchronizationContext in a console app: the model makes its own
    // and raises PropertyChanged there. A UI app constructs it on the UI
    // thread instead.
    // The model's snapshot lags the engine while a search runs, so the
    // sample follows it with RunAsync, as a view does while it is shown, and
    // waits for the search to complete before stepping to the next hit.
    var model = new SearchModel(search);
    var changes = 0;
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    model.PropertyChanged += (_, e) =>
    {
        Interlocked.Increment(ref changes);
        if (e.PropertyName == nameof(SearchModel.Status) && model.Status is SearchStatus.Complete) completed.TrySetResult();
    };
    using var following = new CancellationTokenSource();
    var run = model.RunAsync(following.Token);
    model.Text = Query;
    await model.SubmitSearchAsync();
    if (model.Status is not SearchStatus.Complete) await completed.Task.WaitAsync(TimeSpan.FromSeconds(60));
    await model.NextHitAsync();
    following.Cancel();
    await run;
    if (model.Error is { } modelError) throw modelError;
    if (model.ActiveHit is null || model.HitCount <= 0) throw new InvalidOperationException("the model has no active hit");
    Pass($"SearchModel \"{model.Summary}\", {Volatile.Read(ref changes)} property changes");

    step = "script";
    if (runScript)
    {
        var engines = new JintScriptEngineFactory();
        // The viewer gives a script 50 ms. On a cold runtime the first realm's
        // boot can spend longer than that compiling Jint itself, which reads as a
        // timed-out, disposed realm. One throwaway realm with a generous budget
        // warms the interpreter first; the check below still runs under the
        // viewer's own budget. An app that runs document scripts can do the same
        // at startup.
        var warmUp = await Task.Run(() =>
        {
            using var realm = engines.Create();
            return realm.Evaluate(WarmUpScript, 30_000);
        });
        if (warmUp is not ScriptEvaluation.Value) throw new InvalidOperationException($"the Jint warm-up failed: {warmUp}");
        var check = await ScriptEngineCheck.RunAsync(engines, "app.alert('sample ' + (6 * 7))");
        if (check.Error is { } scriptError) throw new InvalidOperationException($"{scriptError.Kind}: {scriptError.Message}");
        if (check.Alerts is not ["sample 42"]) throw new InvalidOperationException($"unexpected alerts: {string.Join(", ", check.Alerts)}");
        Pass($"Jint alert \"{check.Alerts[0]}\"");
    }
    else
    {
        Console.WriteLine("SKIP script: --no-script");
    }

    await viewer.CloseAsync(document);
    return 0;
}
catch (Exception error)
{
    Console.WriteLine($"FAIL {step}: {error.GetType().Name}: {error.Message}");
    return 1;
}

void Pass(string detail) => Console.WriteLine($"PASS {step}: {detail}");

partial class Program
{
    /// <summary>Touches what the viewer's prelude uses: functions, closures, objects, strings, a regex and JSON.</summary>
    const string WarmUpScript = """
        var made = [];
        function Field(name) { this.name = name; this.value = ''; }
        Field.prototype.set = function (v) { this.value = String(v).replace(/\s+/g, ' ').trim(); return this; };
        for (var i = 0; i < 50; i++) made.push(new Field('f' + i).set('  v ' + i * 2 + '  '));
        JSON.stringify(made.map(function (f) { return [f.name, f.value.length]; }).slice(0, 3));
        """;
}
