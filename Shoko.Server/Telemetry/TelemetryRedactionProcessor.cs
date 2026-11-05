using System.Diagnostics;
using OpenTelemetry;

namespace Shoko.Server.Telemetry;

/// <summary>
/// Hides secrets in the attributes of every span that can hold a URL or a statement, plugin spans included. It runs
/// before the exporter, which only takes a span once every processor before it has seen it.
/// </summary>
internal sealed class TelemetryRedactionProcessor : BaseProcessor<Activity>
{
    private static readonly string[] _attributes = ["url.full", "url.query", "http.url", "db.query.text", "db.statement"];

    public override void OnEnd(Activity data)
    {
        foreach (var attribute in _attributes)
        {
            if (data.GetTagItem(attribute) is string value)
                data.SetTag(attribute, TelemetrySecretScrubber.Scrub(value));
        }
    }
}
