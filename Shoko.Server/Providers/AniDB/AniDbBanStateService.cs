using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Enums;

namespace Shoko.Server.Providers.AniDB;

/// <summary>
/// Owns the two AniDB protocol ban states (UDP and HTTP) so they are a single,
/// first-class service rather than fields buried inside the connection handlers.
/// Any component that needs to read the ban details — the controllers, the
/// acquisition filters, the services — resolves this instead of the connection
/// handlers, which only carry the connection/session state and pass requests on.
/// </summary>
public sealed class AniDbBanStateService
{
    /// <summary>
    /// The ban state of the AniDB UDP protocol.
    /// </summary>
    public AniDbBanState Udp { get; }

    /// <summary>
    /// The ban state of the AniDB HTTP protocol.
    /// </summary>
    public AniDbBanState Http { get; }

    public AniDbBanStateService(ILogger<AniDbBanState> logger)
    {
        Udp = new(AnidbBanType.UDP, AniDbBanState.UdpResetLengthHours, logger);
        Http = new(AnidbBanType.HTTP, AniDbBanState.HttpResetLengthHours, logger);
    }
}
