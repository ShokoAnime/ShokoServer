using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Connectivity.Suspensions;

namespace Shoko.Server.Providers.AniDB.Suspensions;

/// <summary>
///   The AniDB UDP API: banned, overloaded, with a session it no longer
///   takes, or refusing the credentials. Fed by
///   <see cref="AnidbSuspensionMonitor"/>; the AniDB acquisition filters
///   gate its jobs, so it holds none.
/// </summary>
/// <param name="banStates">The ban states, to lift a ban.</param>
public sealed class AnidbUdpSuspensionProvider(AniDbBanStateService banStates) : ISuspensionProvider
{
    /// <inheritdoc />
    public string Name => "AniDB UDP";

    /// <inheritdoc />
    public string? Description => "The AniDB UDP API, used to identify files and keep the MyList.";

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
            banStates.Udp.Unban();
        return Task.CompletedTask;
    }
}
