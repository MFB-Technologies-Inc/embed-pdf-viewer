using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EmbedPDF;

/// <summary>
/// Intervals for the calls worth timing: an <see cref="Activity"/> named
/// after the call under the <c>EmbedPDF</c> source, and its duration in the
/// <c>embedpdf.call.duration</c> histogram, tagged <c>call</c>. Both cost
/// nothing until a listener subscribes.
/// </summary>
internal static class Telemetry
{
    internal const string Name = "EmbedPDF";

    internal static readonly ActivitySource Source = new(Name);

    internal static readonly Meter Meter = new(Name);

    static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("embedpdf.call.duration", "ms", "Time from posting a call to its answer.");

    internal static async Task<T> Measured<T>(string name, Func<Task<T>> body)
    {
        using var activity = Source.StartActivity(name);
        var started = Stopwatch.GetTimestamp();
        try
        {
            return await body().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            activity?.SetStatus(ActivityStatusCode.Error, error.Message);
            throw;
        }
        finally
        {
            Duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, new KeyValuePair<string, object?>("call", name));
        }
    }
}
