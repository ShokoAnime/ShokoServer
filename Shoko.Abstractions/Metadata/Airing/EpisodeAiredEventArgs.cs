using System;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Event arguments for the moment an episode airs. This is dispatched as an
///   airing's slot passes, by the background ticker that keeps the next hour of
///   the schedule in memory.
/// </summary>
/// <remarks>
///   <para>
///     <b>One event per airing, not per episode.</b> An episode running on
///     three channels has three airings and dispatches three events, so a
///     consumer that wants "this episode aired" de-duplicates by episode
///     itself. This is the thing that is easiest to get wrong.
///   </para>
///   <para>
///     <b>Estimates are dispatched too.</b>
///     <see cref="IEpisodeAiring.IsEstimated"/> says which is which, and the
///     consumer decides whether to skip or act. An estimated event is a
///     prediction, not a fact: nothing is known to have aired, and there is no
///     retraction event if the estimate later moves. An estimate also carries
///     its own stable derived ID, so when a provider reports the real slot
///     afterwards that is a <em>different</em> airing dispatching its own
///     event. That is correct rather than a duplicate.
///   </para>
///   <para>
///     <b>Nothing is replayed after downtime.</b> A server that was off when a
///     slot passed never dispatches that event, because an hours-old prediction
///     is worse than none.
///   </para>
///   <para>
///     Handlers run on the ticker's own thread, so a slow handler holds up the
///     events behind it. Do the work somewhere else.
///   </para>
/// </remarks>
public class EpisodeAiredEventArgs : EventArgs
{
    /// <summary>
    ///   The airing whose slot passed, enriched as the service resolved it for
    ///   one episode. Where one stored airing is linked to several episodes the
    ///   event is still dispatched once, carrying one of those views;
    ///   <see cref="IEpisodeAiring.Episode"/> leads to the rest.
    /// </summary>
    public required IEpisodeAiring Airing { get; init; }

    /// <summary>
    ///   The slot that passed, in UTC. This is
    ///   <see cref="IEpisodeAiring.AiredAt"/> at the time the ticker read it,
    ///   kept here so a handler has the instant it fired for even if the
    ///   provider moves the airing afterwards.
    /// </summary>
    public required DateTime AiredAt { get; init; }
}
