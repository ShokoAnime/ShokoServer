using System.Diagnostics;
using OpenTelemetry;

namespace Shoko.Server.Telemetry;

/// <summary>
/// Hides secrets in the attributes of every span that can hold a URL or a statement, plugin spans included. It runs
/// before the exporter, which only takes a span once every processor before it has seen it.
/// </summary>
/// <param name="captureDatabaseStatements">
/// Whether statements may leave at all. Without it they are removed from every span, since not every instrumentation can
/// be told to leave them out.
/// </param>
internal sealed class TelemetryRedactionProcessor(bool captureDatabaseStatements = false) : BaseProcessor<Activity>
{
    private static readonly string[] _attributes = ["url.full", "url.query", "http.url", "db.query.text", "db.statement"];

    private static readonly string[] _statementAttributes = ["db.query.text", "db.statement"];

    public override void OnEnd(Activity data)
    {
        if (!captureDatabaseStatements)
        {
            foreach (var attribute in _statementAttributes)
            {
                if (data.GetTagItem(attribute) is not null)
                    data.SetTag(attribute, null);
            }
        }

        foreach (var attribute in _attributes)
        {
            if (data.GetTagItem(attribute) is string value)
                data.SetTag(attribute, TelemetrySecretScrubber.Scrub(value));
        }
    }
}
