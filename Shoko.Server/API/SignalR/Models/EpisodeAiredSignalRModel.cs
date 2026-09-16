using System;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.API.v3.Models.Airing;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched as an airing's slot passes.
/// </summary>
/// <remarks>
/// One message per airing, not per episode: an episode running on three
/// channels sends three. Estimates are sent too, flagged by
/// <see cref="IsEstimated"/>, and an estimated message is a prediction rather
/// than a fact — there is no retraction if the estimate later moves. Nothing is
/// replayed after downtime, so a client that was connected through a server
/// restart has a gap rather than a burst.
/// </remarks>
/// <param name="args">The event arguments.</param>
public class EpisodeAiredSignalRModel(EpisodeAiredEventArgs args)
{
    /// <summary>
    /// The slot that passed, in UTC.
    /// </summary>
    public DateTime AiredAt { get; } = args.AiredAt.ToUtc();

    /// <summary>
    /// Whether the slot was computed from the schedule's other airings rather
    /// than reported by a provider.
    /// </summary>
    public bool IsEstimated { get; } = args.Airing.IsEstimated;

    /// <summary>
    /// The airing itself, in the same shape the airings endpoint returns, so a
    /// client can render a pushed airing with the code it already has.
    /// </summary>
    public EpisodeAiring Airing { get; } = new(args.Airing);
}
