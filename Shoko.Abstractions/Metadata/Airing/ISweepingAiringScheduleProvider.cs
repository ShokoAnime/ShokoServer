using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Opt in to core-driven sweeping. An <see cref="IAiringScheduleProvider"/>
///   that also implements this is swept by the server on its own timer, so the
///   provider writes only the walk itself and none of the scheduling, pacing,
///   enablement or bookkeeping around it.
/// </summary>
/// <remarks>
///   <para>
///     Implementing this needs no DI registration of any kind. Core already
///     holds the instance it was handed in <c>AddParts</c> and sweeps that very
///     object, so the sweep passes the same <c>this</c> every write on
///     <c>IAiringScheduleService</c> is checked against. Nothing else of the
///     plugin's is involved in a sweep, so there is nothing for the container
///     to hand the provider to.
///   </para>
///   <para>
///     Core never learns what a chunk is. Syoboi walks anime ids, a weekly
///     guide walks weeks and another source walks shows, and all core does is
///     call <see cref="SweepAsync"/> with the cursor the last chunk returned
///     until a chunk answers <c>null</c>.
///   </para>
/// </remarks>
public interface ISweepingAiringScheduleProvider : IAiringScheduleProvider
{
    /// <summary>
    ///   Sweep one chunk of this provider's own source and answer where to
    ///   resume.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     <paramref name="cursor"/> is whatever the previous chunk returned,
    ///     or <c>null</c> for the first chunk of a new sweep. It is opaque to
    ///     core, which stores it and hands it back without ever reading it, so
    ///     use whatever suits the walk: an id, a date, a page token, a small
    ///     JSON blob. It must fit in 512 characters; a longer one is refused
    ///     and ends the sweep. A provider needing real state persists that
    ///     itself and keeps only an identifier in the cursor.
    ///   </para>
    ///   <para>
    ///     <paramref name="cancellationToken"/> is the chunk's whole budget: it
    ///     is cancelled at the deadline core set for this chunk, and also when
    ///     the server shuts down, so there is exactly one token to observe.
    ///     Watch it and return a cursor before it fires. If it fires first the
    ///     chunk is recorded as having timed out and the next one resumes from
    ///     the cursor this one was called with, so the work in flight is lost;
    ///     a rate limited source simply gets less done per chunk rather than
    ///     holding a worker hostage. It has no default on purpose, because a
    ///     sweep that ignores it is the problem this contract exists to solve.
    ///   </para>
    ///   <para>
    ///     Two things follow from ignoring it. Shutdown waits for the chunk in
    ///     flight, so a chunk that carries on holds the server up for the rest
    ///     of its budget, which can be ten minutes. The queue watchdog watches
    ///     the sweep job against a threshold above the budget, so the same
    ///     chunk is reported as a possible deadlock, naming the provider.
    ///   </para>
    /// </remarks>
    /// <param name="cursor">
    ///   Where to resume, as returned by the previous chunk, or <c>null</c> to
    ///   start a new sweep.
    /// </param>
    /// <param name="cancellationToken">
    ///   Cancelled at this chunk's deadline, and on shutdown.
    /// </param>
    /// <returns>
    ///   The cursor the next chunk should be called with, or <c>null</c> when
    ///   the sweep is finished and should not resume until the next interval.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///   The token was cancelled. Returning a cursor instead is always better,
    ///   since it keeps the progress.
    /// </exception>
    Task<string?> SweepAsync(string? cursor, CancellationToken cancellationToken);

    /// <summary>
    ///   Optional. How often this provider's own data is worth walking again,
    ///   as a suggestion. Defaults to none, which leaves the server's own
    ///   default in place.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     This is the same shape as
    ///     <see cref="IAiringScheduleProvider.AvailableKinds"/>: the provider
    ///     declares what it knows about its own source, and the user owns what
    ///     is actually used, which is
    ///     <see cref="AiringScheduleProviderInfo.SweepInterval"/>. A weekly
    ///     timetable and a calendar edited all day are genuinely different, and
    ///     this is where to say so, but it is a suggestion rather than a
    ///     setting: it seeds the value the first time the provider is seen, the
    ///     user's own choice wins from then on, and the server clamps whatever
    ///     comes out of that to no less than fifteen minutes.
    ///   </para>
    ///   <para>
    ///     It also only governs the gap between whole sweeps. The chunks of one
    ///     sweep follow each other as fast as the queue allows, and how long
    ///     each chunk may run is the server's alone, since a chunk holds a queue
    ///     worker and that is shared.
    ///   </para>
    /// </remarks>
    TimeSpan? SuggestedSweepInterval { get => null; }
}
