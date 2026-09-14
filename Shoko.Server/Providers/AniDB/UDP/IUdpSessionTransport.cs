using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Server.Providers.AniDB.UDP;

/// <summary>
/// The operations the <see cref="AniDbUdpSession"/> needs from its owning
/// connection handler. These either touch handler-owned state the session
/// cannot own (the ban flag, the ping timers) or must be coordinated with the
/// handler's socket lock (a forced reconnection tears the socket down and
/// rebinds it).
/// </summary>
internal interface IUdpSessionTransport
{
    bool IsBanned { get; }

    Task ForceReconnectionAsync(CancellationToken cancellationToken);

    void UpdateState(AniDBStateUpdate update);

    void StopPinging();
}
