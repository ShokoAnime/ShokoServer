using System;
using Shoko.Abstractions.User;

namespace Shoko.Server.Models.Internal;

public class AuthTokens
{
    public int AuthID { get; set; }
    public int UserID { get; set; }
    public string DeviceName { get; set; } = null!;
    public string Token { get; set; } = null!;
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    ///   When the token was made, in the server's local time as <see cref="ExpiresAt"/> is; <c>null</c> for one made before this
    ///   was kept.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    ///   When the token was last used, in the server's local time, to within
    ///   <see cref="LastUsedPrecision"/>; <c>null</c> if never since this was kept.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    ///   How stale <see cref="LastUsedAt"/> may get: a use is only written once
    ///   the stored time is at least this old, so a busy token costs one write
    ///   per window.
    /// </summary>
    public static readonly TimeSpan LastUsedPrecision = TimeSpan.FromMinutes(5);

    /// <summary>
    ///   The token as the abstractions hand it out.
    /// </summary>
    /// <param name="user">The user it belongs to.</param>
    /// <returns>The token.</returns>
    public ApiToken ToApiToken(IUser user)
        => new(user, DeviceName, Token, ExpiresAt) { ID = AuthID, CreatedAt = CreatedAt, LastUsedAt = LastUsedAt };
}
