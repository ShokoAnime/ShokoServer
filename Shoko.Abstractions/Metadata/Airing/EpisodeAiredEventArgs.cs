using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Everything that aired in one minute, handed to a subscriber of
///   <c>IAiringScheduleService.SubscribeToAirings</c> as that minute passes.
/// </summary>
/// <remarks>
///   <para>
///     <b>One dispatch per minute, not per airing.</b> A simulcast puts several
///     airings on the same minute — the same episode at 11:25 on both テレビ愛知
///     and テレビ東京 — and they arrive together, in slot order, as one list. A
///     consumer that wants "this episode aired, once" groups
///     <see cref="Airings"/> by <see cref="IEpisodeAiring.ShokoEpisode"/> or
///     <see cref="IEpisodeAiring.Episode"/>; one that wants a card per slot
///     groups by <see cref="IEpisodeAiring.LinkID"/>; one that wants a row per
///     channel takes the list as it is.
///   </para>
///   <para>
///     <b>The airings are the raw ones, tied to the entity they were read
///     for.</b> They are the views the subscription's own
///     <see cref="EpisodeAiringFilteringOptions"/> asked for, including its
///     <see cref="EpisodeAiringFilteringOptions.EntityAnchor"/>, so a
///     subscriber that anchored to shoko never sees an airing with no shoko
///     episode behind it.
///   </para>
///   <para>
///     <b>Estimates are dispatched too</b> unless the subscription turned them
///     off with <see cref="EpisodeAiringFilteringOptions.IncludeEstimates"/>.
///     <see cref="IEpisodeAiring.IsEstimated"/> says which is which. An
///     estimated airing is a prediction, not a fact: nothing is known to have
///     aired, and there is no retraction if the estimate later moves. An
///     estimate carries its own stable derived ID, so when a provider reports
///     the real slot afterwards that is a <em>different</em> airing dispatched
///     in its own minute. That is correct rather than a duplicate.
///   </para>
///   <para>
///     <b>Nothing is replayed after downtime,</b> nor for the time nobody was
///     subscribed. A slot that passed while the server was off, or while no
///     subscriber was listening, is stepped over rather than announced late,
///     because an hours-old prediction is worse than none. Read the gap back
///     with <c>GetAiringsInRange</c> if it matters.
///   </para>
///   <para>
///     <b>Handlers run on the ticker's own thread.</b> A slow handler holds up
///     the minute, and every other subscriber behind it. Enqueue the real work
///     — Shoko has a queue for exactly that — and return.
///   </para>
/// </remarks>
public class EpisodeAiredEventArgs : EventArgs
{
    /// <summary>
    ///   The minute that passed, in UTC, truncated to the minute the ticker
    ///   works on. Every airing in <see cref="Airings"/> had a slot at or
    ///   before it, kept here so a handler has the instant it fired for even if
    ///   a provider moves an airing afterwards.
    /// </summary>
    public required DateTime AiredAt { get; init; }

    /// <summary>
    ///   The airings whose slot passed, in slot order, and never empty: a
    ///   subscriber a minute has nothing for is not called at all.
    /// </summary>
    /// <remarks>
    ///   Where one stored airing is linked to several episodes it appears once
    ///   per episode it was resolved for, each view carrying that episode;
    ///   <see cref="IEpisodeAiring.Episode"/> leads to the rest.
    /// </remarks>
    public required IReadOnlyList<IEpisodeAiring> Airings { get; init; }
}
