using System.Diagnostics;
using OpenTelemetry;

namespace Shoko.Server.Telemetry.Database;

/// <summary>
/// Puts the tally of a span's repository reads on it before it is exported.
/// </summary>
internal sealed class RepositoryReadsProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data)
        => DatabaseTelemetry.WriteRepositoryReads(data);
}
