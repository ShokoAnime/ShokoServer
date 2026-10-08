using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.API.v3.Models.Airing;

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
public class EpisodeAiredSignalRModel
{
    /// <summary>
    /// Builds the message from the event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    public EpisodeAiredSignalRModel(EpisodeAiredEventArgs args)
        : this(args.AiredAt, args.Airings) { }

    /// <summary>
    /// Builds the message from some of the event's airings, for a user who may
    /// not see every one of them.
    /// </summary>
    /// <param name="airedAt">The minute that passed.</param>
    /// <param name="airings">The airings to include.</param>
    public EpisodeAiredSignalRModel(DateTime airedAt, IEnumerable<IEpisodeAiring> airings)
    {
        AiredAt = airedAt.ToUtc();
        Airings = airings.Select(airing => new EpisodeAiring(airing)).ToList();
    }

    /// <summary>
    /// The minute that passed, in UTC.
    /// </summary>
    public DateTime AiredAt { get; }

    /// <summary>
    /// The airings whose slot passed, in slot order, in the same shape the
    /// airings endpoint returns, so a client can render a pushed airing with the
    /// code it already has. Never empty.
    /// </summary>
    public IReadOnlyList<EpisodeAiring> Airings { get; }
}
