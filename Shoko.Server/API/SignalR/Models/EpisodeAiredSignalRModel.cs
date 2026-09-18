using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.API.v3.Models.Airing;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Everything that aired in one minute.
/// </summary>
/// <remarks>
/// One message per minute, not per airing: a simulcast on two stations is one
/// message carrying both, and a client groups <see cref="Airings"/> by episode,
/// by <c>LinkID</c> or by channel to get the shape it wants. Estimates are sent
/// too, flagged by each airing's own <c>IsEstimated</c>, and an estimated airing
/// is a prediction rather than a fact — there is no retraction if it later
/// moves. Nothing is replayed after downtime, so a client that was connected
/// through a server restart has a gap rather than a burst.
/// </remarks>
/// <param name="args">The event arguments.</param>
public class EpisodeAiredSignalRModel(EpisodeAiredEventArgs args)
{
    /// <summary>
    /// The minute that passed, in UTC.
    /// </summary>
    public DateTime AiredAt { get; } = args.AiredAt.ToUtc();

    /// <summary>
    /// The airings whose slot passed, in slot order, in the same shape the
    /// airings endpoint returns, so a client can render a pushed airing with the
    /// code it already has. Never empty.
    /// </summary>
    public IReadOnlyList<EpisodeAiring> Airings { get; } = args.Airings.Select(airing => new EpisodeAiring(airing)).ToList();
}
