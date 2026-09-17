using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Event arguments for episode airing events. This is dispatched once per
///   write, after the write is done, carrying everything the write did to one
///   schedule's airings split into what it <see cref="Added"/>,
///   <see cref="Updated"/> and <see cref="Withdrawn"/>.
/// </summary>
/// <remarks>
///   <para>
///     <b>One event per write, not one per airing.</b> A whole-line write over
///     a long-running series touches thousands of rows, and a consumer seeing
///     them one at a time would be reading a half-written schedule. So the
///     three lists arrive together, once, when the schedule is whole again.
///   </para>
///   <para>
///     <b>The entry point is not visible here.</b> A whole-line write and a
///     delta write dispatch the same event with the same lists filled the same
///     way, so nothing downstream has to care which one a provider reached
///     for.
///   </para>
///   <para>
///     <b>Only what actually changed is listed.</b> A write that hands back an
///     airing exactly as it is stored neither rewrites the row nor reports it,
///     so an airing nothing happened to is in none of the three lists, and its
///     <see cref="IEpisodeAiring.LastUpdatedAt"/> does not move. A write that
///     changed nothing at all is still dispatched, with three empty lists and
///     a <see cref="Reason"/> of <see cref="UpdateReason.None"/>.
///   </para>
/// </remarks>
public class EpisodeAiringsUpdatedEventArgs : EventArgs
{
    private IReadOnlyList<IEpisodeAiring>? _airings;

    /// <summary>
    ///   Why the event was dispatched, as one coarse value for a consumer that
    ///   does not care for the split: <see cref="UpdateReason.Added"/> when the
    ///   write only added airings, <see cref="UpdateReason.Removed"/> when it
    ///   only withdrew them, <see cref="UpdateReason.None"/> when it changed
    ///   nothing, and <see cref="UpdateReason.Updated"/> for anything else,
    ///   including a write that did more than one of those things.
    /// </summary>
    public required UpdateReason Reason { get; init; }

    /// <summary>
    ///   The schedule the airings belong to.
    /// </summary>
    public required IAiringSchedule Schedule { get; init; }

    /// <summary>
    ///   The airings the write put on the schedule for the first time.
    /// </summary>
    public IReadOnlyList<IEpisodeAiring> Added { get; init; } = [];

    /// <summary>
    ///   The airings the write changed, which is any airing that kept its place
    ///   on the schedule while something about it moved: its slot, the slot it
    ///   was first scheduled for, its delay flag, its link, its url or the
    ///   episode behind it.
    /// </summary>
    public IReadOnlyList<IEpisodeAiring> Updated { get; init; } = [];

    /// <summary>
    ///   The airings the write took off the schedule's listing.
    /// </summary>
    /// <remarks>
    ///   Withdrawn is not the same as deleted. A slot still ahead of us is how
    ///   a source pre-empting an episode looks, so the row is kept without a
    ///   slot, with the slot it lost on
    ///   <see cref="IEpisodeAiring.OriginalAiredAt"/>, and it is listed here
    ///   because it is no longer on the schedule's line, not because it is
    ///   gone. A slot that has already passed, or one outside what the schedule
    ///   covers, is deleted as history and listed here too. Read the schedule
    ///   back with <c>IAiringScheduleService.GetAiringsForSchedule</c> to tell
    ///   the two apart: a row kept as a hiatus is still on it, without a slot,
    ///   while a deleted one is not.
    /// </remarks>
    public IReadOnlyList<IEpisodeAiring> Withdrawn { get; init; } = [];

    /// <summary>
    ///   Every airing the event is about, as <see cref="Added"/> then
    ///   <see cref="Updated"/> then <see cref="Withdrawn"/>. Which of the three
    ///   an airing came from is what says what happened to it, so a consumer
    ///   acting on the difference reads those rather than this.
    /// </summary>
    public IReadOnlyList<IEpisodeAiring> Airings => _airings ??= [.. Added, .. Updated, .. Withdrawn];
}
