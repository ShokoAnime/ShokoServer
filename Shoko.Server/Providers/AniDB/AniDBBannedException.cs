using System;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Server.Services.ErrorHandling;

namespace Shoko.Server.Providers.AniDB;

/// <summary>
/// An internal AniDB ban exception.
/// </summary>
[Serializable, SentryIgnore]
public class AniDBBannedException : Exception
{
    /// <summary>
    /// The type of ban that occurred.
    /// /// </summary>
    public required UpdateType BanType { get; init; }

    /// <summary>
    /// When the ban expires, in UTC.
    /// </summary>
    public required DateTime? BanExpires { get; init; }

    /// <summary>
    /// Registers the ban (idempotently) on the given state and builds the
    /// exception from the state's expiry.
    /// </summary>
    public static AniDBBannedException For(AniDbBanState state)
    {
        state.Ban();
        return new()
        {
            BanType = state.BanType is AnidbBanType.HTTP ? UpdateType.HTTPBan : UpdateType.UDPBan,
            BanExpires = state.BanExpiresUtc,
        };
    }
}
