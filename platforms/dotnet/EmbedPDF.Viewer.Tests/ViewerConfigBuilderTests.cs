using System.Reflection;
using Xunit;
using G = uniffi.epdf_viewer_host;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// <see cref="PdfViewerOptionsBuilder"/> has one method per generated
/// <c>ViewerConfig</c> field, and an unset field is the generated default.
/// <see cref="EveryFieldHasARow"/> fails when <c>ViewerConfig</c> gains a
/// field without a row here, and a row only passes when its method sets that
/// field and no other.
/// </summary>
public sealed partial class ViewerConfigBuilderTests
{
    public sealed record Row(string Field, Func<PdfViewerOptionsBuilder, PdfViewerOptionsBuilder> Set)
    {
        public override string ToString() => Field;
    }

    public static readonly IReadOnlyList<Row> Rows =
    [
        new("Scope", b => b.Scope(["doc.open"])),
        new("Metadata", b => b.Metadata()),
        new("Search", b => b.Search(new SearchConfig())),
        new("Render", b => b.Render(new RenderConfig())),
        new("Interaction", b => b.Interaction()),
        new("Feedback", b => b.Feedback(new FeedbackConfig())),
        new("Stage", b => b.Stage(new StageConfig())),
        new("Selection", b => b.Selection()),
        new("Link", b => b.Link()),
        new("Annotation", b => b.Annotation()),
        new("History", b => b.History()),
        new("Outline", b => b.Outline()),
        new("Attachment", b => b.Attachment()),
        new("Form", b => b.Form(new FormConfig())),
        new("Redaction", b => b.Redaction(new RedactionConfig())),
        new("Measurement", b => b.Measurement(new MeasurementConfig())),
        new("Stamp", b => b.Stamp(new StampConfig())),
        new("PageEdit", b => b.PageEdit()),
        new("ViewManager", b => b.ViewManager()),
        new("Shell", b => b.Shell()),
        new("I18n", b => b.I18n(new I18nConfig())),
        new("Commands", b => b.Commands()),
        new("StandardCommands", b => b.StandardCommands([StandardCommandList.History])),
        new("Actions", b => b.Actions(new ActionsConfig())),
        new("Signature", b => b.Signature(new SignatureConfig())),
    ];

    public static TheoryData<Row> RowData => [.. Rows];

    static readonly PropertyInfo[] ConfigFields = typeof(G.ViewerConfig)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.Name != "EqualityContract")
        .ToArray();

    /// <summary>Each field's value as text, so two configs compare field by field.</summary>
    static List<(string Label, string Value)> Fields(G.ViewerConfig config) =>
        ConfigFields.Select(property => (property.Name, Text(property.GetValue(config)))).ToList();

    static string Text(object? value) => value switch
    {
        null => "null",
        bool flag => flag ? "true" : "false",
        string text => $"\"{text}\"",
        System.Collections.IEnumerable list => "[" + string.Join(",", list.Cast<object>().Select(Text)) + "]",
        _ => value.GetType().Name,
    };

    [Fact]
    public void EveryFieldHasARow()
    {
        Assert.Equal(ConfigFields.Select(field => field.Name).Order(), Rows.Select(row => row.Field).Order());
        Assert.Equal(Rows.Count, Rows.Select(row => row.Field).Distinct().Count());
    }

    [Fact]
    public void AnUnconfiguredBuilderIsTheGeneratedDefault()
    {
        using var built = new PdfViewerOptionsBuilder().Build().ToFfi();
        using var generated = new G.ViewerConfig();
        Assert.Equal(Fields(generated), Fields(built));
    }

    [Theory]
    [MemberData(nameof(RowData))]
    public void AMethodSetsItsFieldAndNothingElse(Row row)
    {
        using var defaults = new G.ViewerConfig();
        using var config = row.Set(new PdfViewerOptionsBuilder()).Build().ToFfi();
        foreach (var ((label, value), (_, generated)) in Fields(config).Zip(Fields(defaults)))
        {
            if (label == row.Field)
            {
                Assert.True(value != generated, $"{row.Field} was not set");
            }
            else
            {
                Assert.True(value == generated, $"{row.Field} changed {label}");
            }
        }
    }

    [Fact]
    public void ABuilderIsAValue()
    {
        var baseline = new PdfViewerOptionsBuilder().Interaction();
        var changed = baseline.Selection();
        Assert.False(baseline.Build().Selection);
        Assert.True(changed.Build().Selection);
        Assert.True(changed.Build().Interaction);
    }

    [Fact]
    public void ABuilderStartsFromExistingOptions()
    {
        var options = new PdfViewerOptionsBuilder(new PdfViewerOptions { History = true }).Outline().Build();
        Assert.True(options.History && options.Outline && !options.Metadata);
    }

    [Fact]
    public void TheLoggerDefaultsToTheNullLogger() =>
        Assert.Same(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, new PdfViewerOptions().Logger);
}
