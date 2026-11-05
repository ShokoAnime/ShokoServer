using System;
using System.Collections.Generic;
using OpenTelemetry.Context.Propagation;

namespace Shoko.Server.Telemetry;

/// <summary>
/// Writes no trace context to outgoing requests. The server's traces are for its admin alone, so AniDB, TMDB, Plex and
/// the rest of the services it calls must not be told about them.
/// </summary>
internal sealed class NoOutputTextMapPropagator : TextMapPropagator
{
    public override ISet<string>? Fields => null;

    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
    }

    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
        => context;
}
