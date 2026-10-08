using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Connectivity.Suspensions;

namespace Shoko.Server.Providers.AniDB.Suspensions;

/// <summary>
///   The AniDB HTTP API, suspended while it bans us. Fed by
///   <see cref="AnidbSuspensionMonitor"/>; the AniDB acquisition filters
///   gate its jobs, so it holds none.
/// </summary>
/// <param name="banStates">The ban states, to lift a ban.</param>
public sealed class AnidbHttpSuspensionProvider(AniDbBanStateService banStates) : ISuspensionProvider
{
    /// <inheritdoc />
    public string Name => "AniDB HTTP";

    /// <inheritdoc />
    public string? Description => "The AniDB HTTP API, used to fetch anime.";

    /// <inheritdoc />
    public IReadOnlyList<Type> HeldProviderTypes => [];

    /// <summary>
    ///   Lifts the ban; nothing else is liftable.
    /// </summary>
    /// <param name="kind">The kind of the suspension.</param>
    /// <param name="token">Cancels the lift.</param>
    /// <returns>A completed task.</returns>
    public Task Lift(SuspensionKind kind, CancellationToken token)
    {
        if (kind is SuspensionKind.Banned)
            banStates.Http.Unban();
        return Task.CompletedTask;
    }
}
